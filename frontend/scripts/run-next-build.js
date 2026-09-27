const { spawn } = require('node:child_process');
const path = require('node:path');

// The current route graph exceeds a 6 GiB V8 heap during optimized compilation.
// Keep this build-only and overridable; runtime servers do not inherit the limit.
const minimumHeapMb = process.env.NEXT_BUILD_MAX_OLD_SPACE_SIZE_MB || '12288';
const currentNodeOptions = process.env.NODE_OPTIONS || '';
const hasHeapLimit = /--max-old-space-size(?:=|\s+)/.test(currentNodeOptions);
const nodeOptions = hasHeapLimit
  ? currentNodeOptions
  : `${currentNodeOptions} --max-old-space-size=${minimumHeapMb}`.trim();

const child = spawn(
  process.execPath,
  [require.resolve('next/dist/bin/next'), 'build'],
  {
    cwd: path.resolve(__dirname, '..'),
    env: {
      ...process.env,
      NODE_OPTIONS: nodeOptions,
      NEXT_DIST_DIR: process.env.NEXT_DIST_DIR || '.next-production',
    },
    stdio: 'inherit',
  },
);

child.on('error', (error) => {
  console.error(`Unable to start the Next.js build: ${error.message}`);
  process.exitCode = 1;
});

child.on('exit', (code, signal) => {
  if (signal) {
    console.error(`Next.js build stopped by ${signal}.`);
    process.exitCode = 1;
    return;
  }

  process.exitCode = code ?? 1;
});
