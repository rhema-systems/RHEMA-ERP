'use strict';

const fs = require('fs');
const http = require('http');
const https = require('https');
const net = require('net');

const LISTEN_HOST = '0.0.0.0';
const LISTEN_PORT = 8443;
const CERTIFICATE_PATH = 'C:\\RhemaERP\\certs\\lego-ip\\certificates\\rhemaerp-ip.crt';
const PRIVATE_KEY_PATH = 'C:\\RhemaERP\\certs\\lego-ip\\certificates\\rhemaerp-ip.key';

function selectTarget(url = '/') {
  const path = url.split('?')[0].toLowerCase();
  if (path === '/api' || path.startsWith('/api/') || path === '/health' || path.startsWith('/health/')) {
    return { host: '127.0.0.1', port: 5000, name: 'api' };
  }
  return { host: '127.0.0.1', port: 3001, name: 'frontend' };
}

function forwardedHeaders(request) {
  const headers = { ...request.headers };
  const remoteAddress = request.socket.remoteAddress || '';
  headers.host = request.headers.host || '149.102.145.190:8443';
  headers['x-forwarded-host'] = headers.host;
  headers['x-forwarded-proto'] = 'https';
  headers['x-forwarded-port'] = String(LISTEN_PORT);
  headers['x-forwarded-for'] = headers['x-forwarded-for']
    ? `${headers['x-forwarded-for']}, ${remoteAddress}`
    : remoteAddress;
  return headers;
}

const server = https.createServer({
  cert: fs.readFileSync(CERTIFICATE_PATH),
  key: fs.readFileSync(PRIVATE_KEY_PATH),
}, (request, response) => {
  const target = selectTarget(request.url);
  const upstream = http.request({
    hostname: target.host,
    port: target.port,
    method: request.method,
    path: request.url,
    headers: forwardedHeaders(request),
  }, (upstreamResponse) => {
    response.writeHead(upstreamResponse.statusCode || 502, upstreamResponse.headers);
    upstreamResponse.pipe(response);
  });

  upstream.setTimeout(120000, () => upstream.destroy(new Error('Upstream request timed out.')));
  upstream.on('error', (error) => {
    console.error(`${new Date().toISOString()} proxy ${target.name} error: ${error.message}`);
    if (!response.headersSent) {
      response.writeHead(502, { 'content-type': 'text/plain; charset=utf-8' });
    }
    response.end('Upstream service unavailable.');
  });
  request.pipe(upstream);
});

server.on('upgrade', (request, clientSocket, head) => {
  const target = selectTarget(request.url);
  const upstreamSocket = net.connect(target.port, target.host, () => {
    const headers = forwardedHeaders(request);
    let requestHead = `${request.method} ${request.url} HTTP/${request.httpVersion}\r\n`;
    for (const [name, value] of Object.entries(headers)) {
      if (Array.isArray(value)) {
        for (const item of value) requestHead += `${name}: ${item}\r\n`;
      } else if (value !== undefined) {
        requestHead += `${name}: ${value}\r\n`;
      }
    }
    upstreamSocket.write(`${requestHead}\r\n`);
    if (head && head.length) upstreamSocket.write(head);
    clientSocket.pipe(upstreamSocket).pipe(clientSocket);
  });

  upstreamSocket.on('error', (error) => {
    console.error(`${new Date().toISOString()} websocket ${target.name} error: ${error.message}`);
    clientSocket.destroy();
  });
  clientSocket.on('error', () => upstreamSocket.destroy());
});

server.on('clientError', (error, socket) => {
  console.error(`${new Date().toISOString()} client error: ${error.message}`);
  socket.end('HTTP/1.1 400 Bad Request\r\n\r\n');
});

server.listen(LISTEN_PORT, LISTEN_HOST, () => {
  console.log(`${new Date().toISOString()} HTTPS proxy listening on ${LISTEN_HOST}:${LISTEN_PORT}`);
});

function shutdown() {
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(1), 10000).unref();
}

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
