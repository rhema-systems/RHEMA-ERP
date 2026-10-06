const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const vm = require('node:vm');
const nextConfig = require('../next.config');

function configure({ dev = false, isServer = false } = {}) {
  const config = {
    optimization: {
      minimize: true,
      splitChunks: { cacheGroups: { framework: {} } },
    },
    plugins: [],
  };
  return nextConfig.webpack(config, { dev, isServer });
}

test('production isolates only the serialized PDFium worker and retains app minification', () => {
  const config = configure();
  const group = config.optimization.splitChunks.cacheGroups.pdfiumWorker;
  const installedWorker = require.resolve(
    '@syncfusion/ej2-pdfviewer/src/pdfviewer/pdfium/pdfium-runner.js'
  );
  assert.ok(group.test.test(installedWorker));
  assert.ok(group.test.test(installedWorker.replaceAll('\\', '/')));
  assert.equal(
    group.test.test(
      '/node_modules/@syncfusion/ej2-pdfviewer/src/pdfviewer/pdfviewer.js'
    ),
    false
  );
  assert.equal(group.test.test('/src/app/page.js'), false);
  assert.equal(group.enforce, true);
  assert.equal(config.optimization.minimize, true);
  assert.ok(config.optimization.splitChunks.cacheGroups.framework);
});

test('only worker JavaScript skips minification, before Next runs its minifier', () => {
  const config = configure();
  const markedAssets = [];
  const compilation = {
    chunks: [
      {
        name: 'syncfusion-pdfium-worker',
        files: ['static/chunks/worker.js', 'static/chunks/worker.js.map'],
      },
      { name: 'app/page', files: ['static/chunks/app.js'] },
    ],
    updateAsset(file, updateSource, info) {
      const source = { source: () => 'original worker source' };
      assert.equal(updateSource(source), source);
      markedAssets.push({ file, info });
    },
    hooks: {
      processAssets: {
        tap(options, run) {
          assert.ok(options.stage < 400);
          run();
        },
      },
    },
  };
  config.plugins[0].apply({
    webpack: { Compilation: { PROCESS_ASSETS_STAGE_OPTIMIZE_SIZE: 400 } },
    hooks: {
      compilation: {
        tap(_name, run) {
          run(compilation);
        },
      },
    },
  });
  assert.deepEqual(markedAssets, [
    { file: 'static/chunks/worker.js', info: { minimized: true } },
  ]);
});

test('development and server bundles retain their existing configuration', () => {
  for (const options of [{ dev: true }, { isServer: true }]) {
    const config = configure(options);
    assert.equal(config.plugins.length, 0);
    assert.equal(
      config.optimization.splitChunks.cacheGroups.pdfiumWorker,
      undefined
    );
  }
});

test('published runtime can load next.config without build helper scripts', () => {
  const source = readFileSync(
    path.join(__dirname, '../next.config.js'),
    'utf8'
  );
  const runtime = {
    module: { exports: {} },
    process: { env: { NODE_ENV: 'production' } },
    __dirname: '/published/frontend',
    require() {
      throw new Error('Build helper is unavailable in the runtime package');
    },
  };
  vm.runInNewContext(source, runtime);
  assert.equal(typeof runtime.module.exports.webpack, 'function');
});
