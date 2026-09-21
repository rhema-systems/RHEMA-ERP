const { spawn } = require('node:child_process');
const path = require('node:path');

const child = spawn(
  process.execPath,
  [require.resolve('next/dist/bin/next'), 'start', ...process.argv.slice(2)],
  {
    cwd: path.resolve(__dirname, '..'),
    env: {
      ...process.env,
      NEXT_DIST_DIR: process.env.NEXT_DIST_DIR || '.next-production',
    },
    stdio: 'inherit',
  },
);

child.on('error', (error) => {
  console.error(`Unable to start the Next.js production server: ${error.message}`);
  process.exitCode = 1;
});

child.on('exit', (code, signal) => {
  if (signal) {
    console.error(`Next.js production server stopped by ${signal}.`);
    process.exitCode = 1;
    return;
  }

  process.exitCode = code ?? 1;
});
