// Run from frontend: node scripts/verify-tailwind-sources.cjs
const fs = require('node:fs');
const path = require('node:path');
const postcss = require('postcss');
const tailwind = require('@tailwindcss/postcss');
(async () => {
 const started = Date.now();
 const from = path.resolve('src/app/globals.css');
 const result = await postcss([tailwind()]).process(fs.readFileSync(from, 'utf8'), { from });
 const deps = result.messages.filter(m => m.type === 'dependency').map(m => m.file || '');
 const archived = deps.filter(f => f.includes('.next-before-'));
 if (archived.length) throw new Error('Archived build entered CSS dependencies.');
 for (const selector of ['.flex {', '.grid {', '.bg-primary {']) {
   if (!result.css.includes(selector)) throw new Error('Missing application utility: ' + selector);
 }
 fs.writeFileSync('../local-artifacts/qs-scoped-tailwind.css', result.css);
 console.log(JSON.stringify({elapsedMs: Date.now()-started, cssBytes: Buffer.byteLength(result.css), dependencyCount: deps.length, archivedDependencies: archived.length, requiredUtilities: 'PASS'}));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
