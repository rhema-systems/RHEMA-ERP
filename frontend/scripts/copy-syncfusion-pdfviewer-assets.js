const fs = require('fs');
const path = require('path');

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

fs.mkdirSync(targetDir, { recursive: true });
fs.cpSync(sourceDir, targetDir, { recursive: true });
console.log(`Copied Syncfusion PDF viewer runtime to ${targetDir}`);
