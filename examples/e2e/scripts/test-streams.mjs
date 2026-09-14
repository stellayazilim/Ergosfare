// Run against an already running API; this script does not build or launch dotnet.
import assert from 'node:assert/strict';
const base = process.argv[2] ?? 'http://localhost:5099';
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

async function connect(path) {
  const socket = new WebSocket(base.replace(/^http/, 'ws') + path);
  const received = [];
  socket.addEventListener('message', event => received.push(event.data));
  const closed = new Promise(resolve => socket.addEventListener('close', resolve, { once: true }));
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  return { socket, received, closed };
}
async function waitFor(test, label) {
  const deadline = Date.now() + 5000;
  while (!test()) {
    if (Date.now() > deadline) throw new Error(`Timed out: ${label}`);
    await pause(10);
  }
}

const timeout = setTimeout(() => { console.error('Stream suite timed out'); process.exit(1); }, 20000);
try {
  const input = await connect('/streams/input');
  for (const character of Array.from('Merhaba 👋\n')) input.socket.send(character);
  await pause(100);
  assert.deepEqual(input.received, [], 'input handler must wait for completion');
  input.socket.send('__END__');
  assert.equal((await input.closed).code, 1000);
  assert.deepEqual(input.received, ['Merhaba 👋\n']);

  const empty = await connect('/streams/input');
  empty.socket.send('__END__');
  await empty.closed;
  assert.deepEqual(empty.received, ['']);

  const duplex = await connect('/streams/duplex');
  let expected = '';
  let count = 0;
  for (const character of Array.from('Aş👋')) {
    expected += character;
    duplex.socket.send(character);
    count++;
    await waitFor(() => duplex.received.length === count, 'response before next input');
    assert.equal(duplex.received.at(-1), expected);
  }
  duplex.socket.send('__END__');
  assert.equal((await duplex.closed).code, 1000);
  assert.equal(duplex.received.length, 3);

  const parallel = await Promise.all([connect('/streams/duplex'), connect('/streams/duplex')]);
  parallel[0].socket.send('first'); parallel[1].socket.send('second');
  await waitFor(() => parallel.every(client => client.received.length === 1), 'isolated clients');
  assert.deepEqual(parallel.map(client => client.received[0]), ['first', 'second']);
  for (const client of parallel) client.socket.close();
  await Promise.all(parallel.map(client => client.closed));

  const binary = await connect('/streams/input');
  binary.socket.send(new Uint8Array([1, 2]));
  assert.equal((await binary.closed).code, 1008);

  const response = await fetch(base + '/streams/events');
  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-type'), /text\/event-stream/);
  const decoder = new TextDecoder();
  const characters = [], arrivals = [];
  let buffer = '', done = false;
  for await (const bytes of response.body) {
    buffer += decoder.decode(bytes, { stream: true });
    let boundary;
    while ((boundary = buffer.indexOf('\n\n')) >= 0) {
      const event = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
      if (event.startsWith('event: character\n')) {
        characters.push(JSON.parse(event.split('\ndata: ')[1]));
        arrivals.push(Date.now());
      } else if (event.startsWith('event: done\n')) done = true;
    }
  }
  assert.equal(characters.join(''), 'Hello world');
  assert.equal(characters.length, 11);
  assert.ok(done, 'explicit completion event');
  assert.ok(arrivals.at(-1) - arrivals[0] >= 900, 'SSE must arrive incrementally, not buffered at completion');

  const controller = new AbortController();
  const cancelled = await fetch(base + '/streams/events', { signal: controller.signal });
  await cancelled.body.getReader().read();
  controller.abort();
  assert.equal((await fetch(base + '/health')).status, 200);
  assert.equal((await fetch(base + '/streams.html')).status, 200);
  console.log('PASS: input, empty input, incremental duplex, Unicode, client isolation, invalid input, timed SSE, cancellation, browser page');
} finally {
  clearTimeout(timeout);
}
