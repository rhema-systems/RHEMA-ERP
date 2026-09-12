// A minimal clamd stand-in for a DEMO LAPTOP or local verification ONLY.
//
// !! THIS IS NOT A VIRUS SCANNER. It answers "clean" to every file it is shown.
// !! Never run it on a shared, networked or production machine.
// Started and stopped by scripts/Start-DemoVirusScanner.ps1 — read that file's header first.
//
// The controlled-upload gate makes a clean scan mandatory for every hr-* category, which is
// correct — but it means the attachment write path cannot be exercised on a machine with no
// ClamAV. This speaks just enough of the INSTREAM/PING protocol to answer "clean", so the code
// AFTER the gate (the row write, the include re-read, download, delete) actually runs.
//
// It reports every file as clean. Never run it anywhere real.
import net from 'node:net';

const PORT = Number(process.env.CLAMD_PORT ?? 3310);

const server = net.createServer((socket) => {
  let mode = null;

  socket.on('data', (chunk) => {
    const asText = chunk.toString('latin1');

    if (mode === null) {
      if (asText.includes('PING')) {
        socket.write('PONG\0');
        return;
      }
      if (asText.includes('INSTREAM')) {
        mode = 'instream';
        // The rest of this chunk is chunk-length-prefixed payload; we do not need to parse it.
        // A zero-length chunk terminates the stream, which we detect below.
      }
    }

    if (mode === 'instream') {
      // Terminator is a 4-byte big-endian zero. Looking for four consecutive NULs at the end of
      // a chunk is good enough for a stub fed by a well-behaved client.
      if (chunk.length >= 4 && chunk.subarray(chunk.length - 4).every((b) => b === 0)) {
        socket.write('stream: OK\0');
        mode = null;
      }
    }
  });

  socket.on('error', () => {});
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`clamd stub listening on 127.0.0.1:${PORT} — reports everything clean`);
});
