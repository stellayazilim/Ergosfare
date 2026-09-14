// Streams a real multipart HTTP request; never builds a whole-file Blob/Buffer.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile, readdir, stat, unlink } from 'node:fs/promises';
import { createReadStream } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const base = process.argv[2] ?? 'http://localhost:5099';
const folder = resolve(dirname(fileURLToPath(import.meta.url)), '../upload');
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const files = async () => new Set(await readdir(folder).catch(e => e.code === 'ENOENT' ? [] : Promise.reject(e)));
async function until(check, label) {
  const deadline = Date.now() + 10000;
  while (!(await check())) {
    if (Date.now() > deadline) throw new Error(`Timed out: ${label}`);
    await delay(25);
  }
}
const boundary = 'ergosfare-upload-test';
const prefix = Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="../../large.bin"\r\nContent-Type: application/octet-stream\r\n\r\n`);
const suffix = Buffer.from(`\r\n--${boundary}--\r\n`);
const headers = { 'Content-Type': `multipart/form-data; boundary=${boundary}` };
const timeout = setTimeout(() => { console.error('Upload suite timed out'); process.exit(1); }, 60000);
let storedPath;
try {
  const page = await fetch(base + '/streams/upload');
  assert.equal(page.status, 200);
  assert.match(await page.text(), /multipart\/form-data/);
  assert.equal((await fetch(base + '/upload.js')).status, 200);
  const before = await files();
  const chunk = Buffer.alloc(65536);
  for (let i = 0; i < chunk.length; i++) chunk[i] = i % 251;
  const chunks = 1024; // 64 MiB, generated a chunk at a time.
  const expected = createHash('sha256');
  let resume;
  const gate = new Promise(resolve => { resume = resolve; });
  const controller = new AbortController();
  async function* body() {
    yield prefix;
    for (let i = 0; i < chunks; i++) {
      if (i === 16) await gate;
      expected.update(chunk);
      yield chunk;
    }
    yield suffix;
  }
  const responseTask = fetch(base + '/streams/upload', {
    method: 'POST', headers, body: body(), duplex: 'half', signal: controller.signal
  });
  responseTask.catch(() => {});
  try {
    await until(async () => {
      for (const name of await files())
        if (!before.has(name) && (await stat(resolve(folder, name))).size > 0) return true;
      return false;
    }, 'handler writes to disk before request completes');
  } catch (error) { controller.abort(); throw error; }
  finally { resume(); }
  const response = await responseTask;
  assert.equal(response.status, 200, await response.clone().text());
  const receipt = await response.json();
  assert.match(receipt.storedName, /^[a-f0-9]{32}\.upload$/);
  storedPath = resolve(folder, receipt.storedName);
  assert.equal(receipt.fileName, 'large.bin');
  assert.equal(receipt.contentType, 'application/octet-stream');
  assert.deepEqual(JSON.parse(await readFile(storedPath + '.json', 'utf8')), receipt);
  assert.equal(receipt.bytes, chunk.length * chunks);
  assert.ok(receipt.chunks > 1);
  const expectedHash = expected.digest('hex');
  assert.equal(receipt.sha256, expectedHash);
  const diskHash = createHash('sha256');
  for await (const block of createReadStream(storedPath)) diskHash.update(block);
  assert.equal(diskHash.digest('hex'), expectedHash);
  assert.equal((await stat(storedPath)).size, receipt.bytes);

  const stable = await files();
  // A valid PCM WAV, rather than arbitrary bytes with a MIME label.
  const wav = Buffer.alloc(44 + 8);
  wav.write('RIFF', 0); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(8000, 24); wav.writeUInt32LE(16000, 28);
  wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(8, 40);
  [0, 12345, -12345, 32767].forEach((sample, i) => wav.writeInt16LE(sample, 44 + i * 2));
  const text = Buffer.from('UTF-8 text: \u{1F30D}\r\nSecond line\n', 'utf8');
  for (const sample of [
    { name: 'tone.wav', type: 'audio/wav', bytes: wav },
    { name: 'text.txt', type: 'text/plain; charset=utf-8', bytes: text },
    { name: 'unknown.bin', type: null, bytes: Buffer.from([0, 255, 13, 10, 128]) }
  ]) {
    const part = Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="${sample.name}"\r\n${sample.type ? `Content-Type: ${sample.type}\r\n` : ''}\r\n`);
    // Force framing and payload across many source chunks, including the WAV signature.
    async function* sampleBody() {
      yield part;
      for (let i = 0; i < sample.bytes.length; i += 3) yield sample.bytes.subarray(i, i + 3);
      yield suffix;
    }
    const uploaded = await fetch(base + '/streams/upload', {
      method: 'POST', headers, body: sampleBody(), duplex: 'half'
    });
    assert.equal(uploaded.status, 200);
    const saved = await uploaded.json();
    assert.match(saved.storedName, /^[a-f0-9]{32}\.upload$/);
    const savedPath = resolve(folder, saved.storedName);
    try {
      assert.equal(saved.contentType, sample.type);
      assert.equal(saved.bytes, sample.bytes.length);
      assert.equal(saved.sha256, createHash('sha256').update(sample.bytes).digest('hex'));
      assert.deepEqual(await readFile(savedPath), sample.bytes);
      assert.deepEqual(JSON.parse(await readFile(savedPath + '.json', 'utf8')), saved);
    } finally { await unlink(savedPath); await unlink(savedPath + '.json'); }
  }
  assert.deepEqual(await files(), stable);
  // Pre must reject missing/non-file sections without reaching the file-writing handler.
  for (const body of [
    Buffer.from(`--${boundary}--\r\n`),
    Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="description"\r\n\r\ntext${suffix}`),
    Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"\r\n\r\nnot-a-file${suffix}`)
  ]) {
    const rejected = await fetch(base + '/streams/upload', { method: 'POST', headers, body });
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /single file/);
    assert.deepEqual(await files(), stable);
  }
  // A present but empty file is valid; pre's first read is EOF, not a missing field.
  const empty = await fetch(base + '/streams/upload', {
    method: 'POST', headers, body: Buffer.concat([prefix, suffix])
  });
  assert.equal(empty.status, 200);
  const emptyReceipt = await empty.json();
  assert.match(emptyReceipt.storedName, /^[a-f0-9]{32}\.upload$/);
  const emptyPath = resolve(folder, emptyReceipt.storedName);
  try {
    assert.equal(emptyReceipt.bytes, 0);
    assert.equal(emptyReceipt.chunks, 0);
    assert.equal(emptyReceipt.sha256, createHash('sha256').digest('hex'));
    assert.equal((await stat(emptyPath)).size, 0);
  } finally { await unlink(emptyPath); await unlink(emptyPath + '.json'); }

  const extra = await fetch(base + '/streams/upload', {
    method: 'POST', headers,
    body: Buffer.concat([prefix, Buffer.from(`first\r\n--${boundary}\r\nContent-Disposition: form-data; name="description"\r\n\r\nextra`), suffix])
  });
  assert.equal(extra.status, 400);
  assert.deepEqual(await files(), stable);

  const cancel = new AbortController();
  async function* endless() {
    yield prefix;
    while (!cancel.signal.aborted) { yield chunk; await delay(5); }
  }
  const cancelled = fetch(base + '/streams/upload', {
    method: 'POST', headers, body: endless(), duplex: 'half', signal: cancel.signal
  });
  cancelled.catch(() => {});
  try {
    await until(async () => (await files()).size > stable.size, 'partial upload created');
  } finally { cancel.abort(); }
  await assert.rejects(cancelled);
  await until(async () => (await files()).size === stable.size, 'cancelled partial file removed');

  const malformed = await fetch(base + '/streams/upload', {
    method: 'POST', headers, body: Buffer.concat([prefix, Buffer.from('unfinished')])
  });
  assert.equal(malformed.status, 400);
  assert.equal((await files()).size, stable.size);
  const unsupported = await fetch(base + '/streams/upload', { method: 'POST', body: 'no multipart' });
  assert.equal(unsupported.status, 415);
  console.log(`PASS: page, raw multipart body, 64 MiB, disk writes before EOF, ${receipt.chunks} chunks, first-chunk integrity (SHA-256), WAV/UTF-8 byte identity, MIME metadata (including missing header), pre-validation, empty file, extra-section rejection, cancellation cleanup, malformed request cleanup`);
} finally {
  clearTimeout(timeout);
  if (storedPath) {
    await unlink(storedPath);
    await unlink(storedPath + '.json');
  }
}
