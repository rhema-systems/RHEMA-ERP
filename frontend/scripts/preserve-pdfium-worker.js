const PDFIUM_WORKER_CHUNK = 'syncfusion-pdfium-worker';

class PreservePdfiumWorkerPlugin {
  apply(compiler) {
    compiler.hooks.compilation.tap(
      'PreservePdfiumWorkerPlugin',
      (compilation) => {
        compilation.hooks.processAssets.tap(
          {
            name: 'PreservePdfiumWorkerPlugin',
            stage:
              compiler.webpack.Compilation.PROCESS_ASSETS_STAGE_OPTIMIZE_SIZE -
              1,
          },
          () => {
            for (const chunk of compilation.chunks) {
              if (chunk.name !== PDFIUM_WORKER_CHUNK) continue;
              for (const file of chunk.files) {
                if (!file.endsWith('.js')) continue;
                // Next's minifier honors this flag. Syncfusion serializes this
                // function into a worker; SWC compression corrupts its text bounds.
                compilation.updateAsset(file, (source) => source, {
                  minimized: true,
                });
              }
            }
          }
        );
      }
    );
  }
}

function preservePdfiumWorker(config) {
  config.optimization.splitChunks.cacheGroups.pdfiumWorker = {
    test: /[\\/]@syncfusion[\\/]ej2-pdfviewer[\\/]src[\\/]pdfviewer[\\/]pdfium[\\/]pdfium-runner\.js$/,
    name: PDFIUM_WORKER_CHUNK,
    chunks: 'all',
    enforce: true,
    priority: 100,
  };
  config.plugins.push(new PreservePdfiumWorkerPlugin());
}

module.exports = { preservePdfiumWorker };
