const fs = require('fs');
const path = require('path');

if (process.env.RHEMA_SKIP_SYNCFUSION_ASSETS === '1') {
  console.log('Skipped Syncfusion PDF viewer asset preparation for this install.');
  process.exit(0);
}

const projectRoot = path.resolve(__dirname, '..');
const sourceDir = path.join(
  projectRoot,
  'node_modules',
  '@syncfusion',
  'ej2-pdfviewer',
  'dist',
  'ej2-pdfviewer-lib'
);
const targetDir = path.join(
  projectRoot,
  'public',
  'syncfusion',
  'ej2-pdfviewer-lib'
);

if (!fs.existsSync(sourceDir)) {
  throw new Error(`Syncfusion PDF viewer runtime not found: ${sourceDir}`);
}

const expectedFiles = ['pdfium.js', 'pdfium.wasm'];
for (const file of expectedFiles) {
  const sourceFile = path.join(sourceDir, file);
  if (!fs.statSync(sourceFile, { throwIfNoEntry: false })?.isFile()) {
    throw new Error(`Required Syncfusion PDF viewer runtime file is missing: ${sourceFile}`);
  }
}

// Replace the exact managed directory so removed vendor files cannot survive a
// package upgrade. Never remove the parent public/syncfusion directory.
fs.rmSync(targetDir, { recursive: true, force: true });
fs.mkdirSync(targetDir, { recursive: true });
fs.cpSync(sourceDir, targetDir, { recursive: true });

for (const file of expectedFiles) {
  const targetFile = path.join(targetDir, file);
  if (!fs.statSync(targetFile, { throwIfNoEntry: false })?.isFile()) {
    throw new Error(`Syncfusion PDF viewer asset verification failed: ${targetFile}`);
  }
}

console.log(
  `Prepared and verified ${expectedFiles.length} Syncfusion PDF viewer runtime files in ${targetDir}`
);
