// e2e runner: boots the Todo Api on a fixed port, waits until it is ready, runs the
// ijhttp .http suite against it, then tears the app down and propagates ijhttp's exit code.
//
// Dependency-free (Node stdlib only). Cross-platform. Invoked by `task e2e`, or directly:
//   node examples/e2e/run.mjs
//
// ijhttp (the JetBrains HTTP Client CLI) is self-provisioned into examples/e2e/tools on
// first run.
// It is a Java app, so a JDK 17+ must be present (JAVA_HOME, PATH, or a standard install
// location — e.g. `winget install Microsoft.OpenJDK.21`).

import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { existsSync, mkdirSync, readdirSync } from 'node:fs';
import http from 'node:http';
import net from 'node:net';

const here = dirname(fileURLToPath(import.meta.url));
const isWin = process.platform === 'win32';
const HOST = 'http://localhost:5099';
const API_PROJECT = join(here, 'Stella.Ergosfare.E2E.Api');
const TOOLS = join(here, 'tools');
const IJHTTP_BIN = join(TOOLS, 'ijhttp', isWin ? 'ijhttp.bat' : 'ijhttp');
const IJHTTP_URL = 'https://jb.gg/ijhttp/latest';
const READY_TIMEOUT_MS = 60_000;

function log(msg) {
  process.stdout.write(`[e2e] ${msg}\n`);
}

// ---- ijhttp provisioning -----------------------------------------------------------------

function ensureIjhttp() {
  if (existsSync(IJHTTP_BIN)) return true;

  mkdirSync(TOOLS, { recursive: true });
  const zip = join(TOOLS, 'ijhttp.zip');

  log('ijhttp not found — downloading the JetBrains HTTP Client CLI…');
  if (spawnSync('curl', ['-fL', '-o', zip, IJHTTP_URL], { stdio: 'inherit' }).status !== 0) {
    log('download failed. Fetch it manually from https://www.jetbrains.com/help/idea/http-client-cli.html');
    return false;
  }

  log('extracting…');
  const extract = isWin
    ? spawnSync('powershell', ['-NoProfile', '-Command',
        `Expand-Archive -LiteralPath "${zip}" -DestinationPath "${TOOLS}" -Force`], { stdio: 'inherit' })
    : spawnSync('unzip', ['-oq', zip, '-d', TOOLS], { stdio: 'inherit' });

  return extract.status === 0 && existsSync(IJHTTP_BIN);
}

// ---- Java resolution ---------------------------------------------------------------------

// ijhttp reads JAVA_HOME (or java on PATH). Return a JAVA_HOME to inject, or null when java
// is already reachable / nothing was found (in which case ijhttp prints its own guidance).
function resolveJavaHome() {
  if (process.env.JAVA_HOME && existsSync(process.env.JAVA_HOME)) return process.env.JAVA_HOME;

  const onPath = spawnSync(isWin ? 'where' : 'command', isWin ? ['java'] : ['-v', 'java'],
    { stdio: 'ignore', shell: true });
  if (onPath.status === 0) return null;

  if (!isWin) return null;
  const roots = [
    'C:\\Program Files\\Microsoft',
    'C:\\Program Files\\Eclipse Adoptium',
    'C:\\Program Files\\Java',
  ];
  for (const root of roots) {
    if (!existsSync(root)) continue;
    const candidates = readdirSync(root)
      .filter((d) => /jdk|jre/i.test(d))
      .sort()
      .reverse();
    for (const d of candidates) {
      const home = join(root, d);
      if (existsSync(join(home, 'bin', 'java.exe'))) return home;
    }
  }
  return null;
}

// ---- app lifecycle -----------------------------------------------------------------------

function waitForReady(url, timeoutMs, app) {
  const deadline = Date.now() + timeoutMs;
  return new Promise((resolve, reject) => {
    const attempt = () => {
      if (app.exitCode !== null || app.signalCode !== null) {
        return reject(new Error(`API exited before becoming ready (exit ${app.exitCode}). See its output above.`));
      }
      const req = http.get(url, (res) => {
        res.resume();
        if (res.statusCode === 200) return resolve();
        retry();
      });
      req.on('error', retry);
      req.setTimeout(2000, () => req.destroy());
    };
    const retry = () => {
      if (Date.now() > deadline) return reject(new Error(`app not ready after ${timeoutMs}ms`));
      setTimeout(attempt, 500);
    };
    attempt();
  });
}

function killTree(child) {
  if (!child || child.exitCode !== null || child.signalCode !== null) return;
  if (isWin) {
    spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
  } else {
    child.kill('SIGTERM');
  }
}

async function main() {
  // Do not accidentally run the assertions against an app started by Rider or another run.
  const occupied = await Promise.all(['127.0.0.1', '::1'].map(host => new Promise((resolve, reject) => {
    const probe = net.connect({ host, port: 5099 });
    probe.once('connect', () => { probe.destroy(); resolve(true); });
    probe.once('error', () => { probe.destroy(); resolve(false); });
    probe.setTimeout(2000, () => { probe.destroy(); reject(new Error(`Could not check ${host}:5099 before starting the API.`)); });
  })));
  if (occupied.some(Boolean)) {
    throw new Error('Port 5099 is already in use. Stop the running API before using the automated runner, or run todos.http against it in Rider.');
  }
  if (!ensureIjhttp()) return 127;

  const javaHome = resolveJavaHome();
  const childEnv = { ...process.env, ...(javaHome ? { JAVA_HOME: javaHome } : {}) };

  log('building the Todo Api…');
  const build = spawnSync('dotnet', ['build', API_PROJECT, '-c', 'Release', '--nologo', '-m:1'], {
    cwd: here, stdio: 'inherit',
  });
  if (build.error) throw build.error;
  if (build.status !== 0) return build.status ?? 1;

  log('starting the Todo Api…');
  const app = spawn('dotnet', ['run', '--project', API_PROJECT, '-c', 'Release', '--no-build', '--no-launch-profile', '--', '--urls', HOST], {
    cwd: here,
    env: { ...process.env, ASPNETCORE_URLS: HOST },
    stdio: ['ignore', 'inherit', 'inherit'],
  });
  let appExited = false;
  app.on('exit', () => { appExited = true; });

  try {
    log(`waiting for ${HOST}/health…`);
    await waitForReady(`${HOST}/health`, READY_TIMEOUT_MS, app);
    if (appExited) throw new Error('app exited before becoming ready');
    log('app is ready — running the ijhttp suite');

    const code = await new Promise((resolve) => {
      const ij = spawn(IJHTTP_BIN, [
        'http/todos.http',
        '--report', 'reports',
      ], { cwd: here, env: childEnv, stdio: 'inherit', shell: true });
      ij.on('error', (err) => { log(`ijhttp failed to start: ${err.message}`); resolve(1); });
      ij.on('exit', (c) => resolve(c ?? 1));
    });

    log(code === 0 ? 'suite passed ✔' : `suite failed (exit ${code})`);
    return code;
  } finally {
    log('stopping the app…');
    killTree(app);
  }
}

main()
  .then((code) => process.exit(code))
  .catch((err) => { log(`error: ${err.message}`); process.exit(1); });
