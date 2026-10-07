import { spawn, execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';

const baseUrl = (process.argv[2] ?? process.env.RHEMA_VPS_BASE_URL
  ?? 'https://63.141.230.56').replace(/\/$/, '');
const browserCandidates = [
  process.env.RHEMA_BROWSER_PATH,
  process.env.RHEMA_CHROME_PATH,
  `${process.env.ProgramFiles ?? 'C:\\Program Files'}\\Google\\Chrome\\Application\\chrome.exe`,
  `${process.env['ProgramFiles(x86)'] ?? 'C:\\Program Files (x86)'}\\Google\\Chrome\\Application\\chrome.exe`,
  `${process.env.ProgramFiles ?? 'C:\\Program Files'}\\Microsoft\\Edge\\Application\\msedge.exe`,
  `${process.env['ProgramFiles(x86)'] ?? 'C:\\Program Files (x86)'}\\Microsoft\\Edge\\Application\\msedge.exe`,
  process.env.LOCALAPPDATA
    ? `${process.env.LOCALAPPDATA}\\Google\\Chrome\\Application\\chrome.exe`
    : undefined,
].filter(Boolean);

async function reservePort() {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const address = server.address();
  const port = typeof address === 'object' && address ? address.port : 0;
  await new Promise((resolve) => server.close(resolve));
  if (!port) throw new Error('Could not allocate a browser debug port.');
  return port;
}

let browserPath;
for (const candidate of browserCandidates) {
  try {
    await fs.access(candidate);
    browserPath = candidate;
    break;
  } catch {
    // Try the next supported Chromium browser location.
  }
}
if (!browserPath) {
  throw new Error(`Chrome or Edge is required for browser smoke. Checked: ${browserCandidates.join(', ')}`);
}

const debugPort = await reservePort();
const profilePath = await fs.mkdtemp(path.join(os.tmpdir(), 'rhema-vps-smoke-'));
const chrome = spawn(browserPath, [
  '--headless=new',
  `--remote-debugging-port=${debugPort}`,
  `--user-data-dir=${profilePath}`,
  '--ignore-certificate-errors',
  '--no-first-run',
  '--no-default-browser-check',
  '--disable-background-networking',
  '--disable-component-update',
  '--disable-sync',
  '--disable-extensions',
  '--disable-features=Translate,MediaRouter',
  'about:blank',
], { stdio: ['ignore', 'ignore', 'pipe'] });
chrome.stderr.resume();

let socket;
let sequence = 0;
const pendingCommands = new Map();
const requests = new Map();
const current = {
  failedRequests: [],
  errorResponses: [],
  consoleErrors: [],
  exceptions: [],
  logErrors: [],
};

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

async function waitForProcessExit(child, timeoutMs) {
  if (child.exitCode !== null) return true;
  return new Promise((resolve) => {
    const timer = setTimeout(() => {
      child.removeListener('exit', onExit);
      resolve(false);
    }, timeoutMs);
    const onExit = () => {
      clearTimeout(timer);
      resolve(true);
    };
    child.once('exit', onExit);
  });
}

async function waitForDebugger() {
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`http://127.0.0.1:${debugPort}/json/list`);
      if (response.ok) {
        const pages = await response.json();
        // Endpoint-security extensions can register background pages before the requested tab.
        const target = pages.find((page) => page.type === 'page' && page.url === 'about:blank')
          ?? pages.find((page) => page.type === 'page'
            && !page.url.startsWith('chrome-extension://'));
        if (target?.webSocketDebuggerUrl) return target.webSocketDebuggerUrl;
      }
    } catch {
      // Chrome is still starting.
    }
    await delay(200);
  }
  throw new Error('Chrome DevTools endpoint did not become ready.');
}

function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++sequence;
    pendingCommands.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

function resetPageSignals() {
  requests.clear();
  for (const key of Object.keys(current)) current[key].length = 0;
}

async function evaluate(expression) {
  const response = await send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (response.exceptionDetails) {
    throw new Error(response.exceptionDetails.text ?? 'Browser evaluation failed.');
  }
  return response.result.value;
}

async function waitForEvaluation(expression, predicate, description, timeoutMs = 60_000) {
  const deadline = Date.now() + timeoutMs;
  let lastValue;
  while (Date.now() < deadline) {
    lastValue = await evaluate(expression);
    if (predicate(lastValue)) return lastValue;
    await delay(200);
  }
  throw new Error(`${description} timed out. Last state: ${JSON.stringify(lastValue)}`);
}

async function navigate(relativeUrl) {
  resetPageSignals();
  const navigation = await send('Page.navigate', { url: `${baseUrl}${relativeUrl}` });
  if (navigation.errorText && navigation.errorText !== 'net::ERR_ABORTED') {
    throw new Error(`Navigation failed for ${relativeUrl}: ${navigation.errorText}`);
  }

  const deadline = Date.now() + 30_000;
  let ready = false;
  let lastState;
  while (Date.now() < deadline) {
    lastState = await evaluate(`({
      readyState: document.readyState,
      href: location.href,
      hasDocumentElement: Boolean(document.documentElement),
    })`);
    // Next.js can progressively render a usable page while the document remains
    // in "loading" because a streamed response or nonessential asset is open.
    // Route-specific UI checks below are the authoritative readiness gates.
    if (lastState.href !== 'about:blank' && lastState.hasDocumentElement) {
      ready = true;
      break;
    }
    await delay(100);
  }
  if (!ready) {
    throw new Error(
      `Page readiness timed out for ${relativeUrl}. Last state: ${JSON.stringify(lastState)}`,
    );
  }
  await delay(250);
  return evaluate('location.href');
}

function snapshotSignals() {
  return Object.fromEntries(Object.entries(current).map(([key, value]) => [key, [...value]]));
}

try {
  const webSocketUrl = await waitForDebugger();
  socket = new WebSocket(webSocketUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });

  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data.toString());
    if (message.id) {
      const command = pendingCommands.get(message.id);
      if (!command) return;
      pendingCommands.delete(message.id);
      if (message.error) command.reject(new Error(message.error.message));
      else command.resolve(message.result ?? {});
      return;
    }

    if (message.method === 'Network.requestWillBeSent') {
      requests.set(message.params.requestId, message.params.request.url);
    } else if (message.method === 'Network.loadingFinished') {
      requests.delete(message.params.requestId);
    } else if (message.method === 'Network.loadingFailed') {
      const url = requests.get(message.params.requestId) ?? '(unknown request)';
      requests.delete(message.params.requestId);
      if (!message.params.canceled) {
        current.failedRequests.push({ url, error: message.params.errorText });
      }
    } else if (message.method === 'Network.responseReceived') {
      const { status, url } = message.params.response;
      if (status >= 400) current.errorResponses.push({ status, url });
    } else if (message.method === 'Runtime.consoleAPICalled'
      && message.params.type === 'error') {
      current.consoleErrors.push(message.params.args
        .map((argument) => argument.value ?? argument.description).join(' '));
    } else if (message.method === 'Runtime.exceptionThrown') {
      current.exceptions.push(message.params.exceptionDetails?.text
        ?? 'Unhandled browser exception');
    } else if (message.method === 'Log.entryAdded'
      && message.params.entry.level === 'error') {
      current.logErrors.push(message.params.entry.text);
    }
  });

  await Promise.all([
    send('Page.enable'),
    send('Runtime.enable'),
    send('Network.enable'),
    send('Log.enable'),
  ]);

  const results = {};
  results.loginUrl = await navigate('/login');
  const loginUiExpression = `(() => {
    const visible = (element) => {
      if (!element) return false;
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden'
        && rect.width > 0 && rect.height > 0;
    };
    const inputs = [...document.querySelectorAll('input')].filter(visible);
    const buttons = [...document.querySelectorAll('button')].filter(visible);
    return {
      loginCardPresent: Boolean(document.querySelector('[data-login-card]')),
      userFieldVisible: inputs.some((input) => ['text', 'email'].includes(input.type)),
      passwordFieldVisible: inputs.some((input) => input.type === 'password'),
      signInVisible: buttons.some((button) => button.innerText.trim().toLowerCase() === 'sign in'),
      inputTypes: inputs.map((input) => input.type),
      buttonLabels: buttons.map((button) => button.innerText.trim()).filter(Boolean),
      loadingMessage: document.querySelector('[data-login-loading-style]')?.textContent?.trim() ?? null,
    };
  })()`;
  results.loginUi = await waitForEvaluation(
    loginUiExpression,
    (state) => state.loginCardPresent
      && state.userFieldVisible
      && state.passwordFieldVisible
      && state.signInVisible,
    'Login form readiness',
  );
  await delay(500);
  results.loginSignals = snapshotSignals();

  results.supplierUrl = await navigate('/supplier-application');
  const supplierUiExpression = `(() => {
    const visible = (element) => {
      if (!element) return false;
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden'
        && rect.width > 0 && rect.height > 0;
    };
    const tabs = [...document.querySelectorAll('[role="tab"]')]
      .filter(visible).map((tab) => tab.innerText.trim());
    return {
      applyForTokenVisible: tabs.includes('Apply for token'),
      tokenLoginVisible: tabs.includes('Token login'),
    };
  })()`;
  results.supplierUi = await waitForEvaluation(
    supplierUiExpression,
    (state) => state.applyForTokenVisible && state.tokenLoginVisible,
    'Supplier application tabs',
  );
  await delay(500);
  results.supplierSignals = snapshotSignals();

  results.portalRedirectUrl = await navigate('/supplier-application/portal');
  results.portalRedirectUrl = await waitForEvaluation(
    'location.href',
    (href) => new URL(href).pathname === '/supplier-application',
    'Supplier portal redirect',
  );
  await delay(500);
  results.portalSignals = snapshotSignals();
  results.adminRedirectUrl = await navigate(
    '/administration/procurement/supplier-applicant-access');
  results.adminRedirectUrl = await waitForEvaluation(
    'location.href',
    (href) => new URL(href).pathname === '/login',
    'Protected administration redirect',
  );
  await delay(500);
  results.adminSignals = snapshotSignals();

  const allSignals = [
    results.loginSignals,
    results.supplierSignals,
    results.portalSignals,
    results.adminSignals,
  ];
  results.totalMaterialErrors = allSignals.reduce((total, signals) => total
    + signals.failedRequests.length
    + signals.errorResponses.filter((response) => response.status >= 500).length
    + signals.consoleErrors.length
    + signals.exceptions.length
    + signals.logErrors.length, 0);

  results.passed = Boolean(
    results.loginUi.userFieldVisible
    && results.loginUi.passwordFieldVisible
    && results.loginUi.signInVisible
    && results.supplierUi.applyForTokenVisible
    && results.supplierUi.tokenLoginVisible
    && new URL(results.portalRedirectUrl).pathname === '/supplier-application'
    && new URL(results.adminRedirectUrl).pathname === '/login'
    && results.totalMaterialErrors === 0
  );

  console.log(JSON.stringify(results));
  console.log(`BROWSER_SMOKE|${results.passed ? 'PASS' : 'FAIL'}|errors=${results.totalMaterialErrors}`);
  if (!results.passed) process.exitCode = 1;
  try {
    await send('Browser.close');
  } catch {
    // Chrome can close the DevTools socket before acknowledging Browser.close.
  }
} finally {
  if (socket?.readyState === WebSocket.OPEN) socket.close();
  const closedNormally = await waitForProcessExit(chrome, 3_000);
  if (!closedNormally && chrome.exitCode === null) {
    try {
      execFileSync('taskkill.exe', ['/PID', String(chrome.pid), '/T', '/F'], {
        stdio: 'ignore',
      });
    } catch {
      // Browser may already have exited.
    }
  }
  await waitForProcessExit(chrome, 3_000);
  let cleanupError;
  for (let attempt = 0; attempt < 15; attempt += 1) {
    try {
      await fs.rm(profilePath, {
        recursive: true,
        force: true,
        maxRetries: 3,
        retryDelay: 250,
      });
      cleanupError = undefined;
      break;
    } catch (error) {
      cleanupError = error;
      await delay(1_000);
    }
  }
  if (cleanupError) {
    console.error(`BROWSER_PROFILE_CLEANUP_WARNING|${cleanupError.code ?? cleanupError.message}`);
  }
}
