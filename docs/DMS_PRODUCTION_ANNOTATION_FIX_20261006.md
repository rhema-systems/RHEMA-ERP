# Production PDF Annotation Fix

## Failure and Cause

Legal uses the shared Central DMS document viewer. In an optimized production
build, the PDF rendered normally but highlighting selected nothing. Browser
inspection showed individual text characters with zero height and zero font size.

Syncfusion constructs its PDFium worker by serializing `PdfiumRunner` with
`toString()`. Next's production JavaScript compression changes that worker's
behavior: the same test PDF has invalid text bounds after compression and valid
bounds when the worker is preserved. Free-text and ink annotations worked in the
baseline reproduction; the confirmed failure was text selection/highlighting.

## Change

`frontend/scripts/preserve-pdfium-worker.js` isolates the installed Syncfusion
`pdfium-runner.js` in a dedicated client chunk and marks that chunk as already
minimized before Next's minifier runs. Other client chunks retain normal
minification. Development and server compilation are unchanged.

`frontend/next.config.js` applies the fix automatically for production client
builds. The helper is required inside the build hook so the published runtime can
load its configuration without the build scripts directory.

## Verification

- Built a small Next production harness using the actual shared viewer and dialog,
  the repository's Next configuration, installed Syncfusion runtime assets, and a
  generated PDF with known text. Served with `next start`, not `next dev`.
- Baseline: highlighting failed; text bounds had zero height/font size.
- Disabling client minification restored bounds and highlighting.
- Final scoped fix: dedicated worker chunk emitted, other client code remained
  minified, text bounds had positive height, and drag-to-highlight succeeded both
  in the standalone viewer and Central DMS dialog.
- Exported annotation JSON contained a `Highlight` annotation with nonzero bounds.
- Final browser error log was empty.
- `node --test scripts/preserve-pdfium-worker.test.js`: 4 passed.
- Focused Vitest viewer/dialog suites: 10 passed.

The browser reproduction used locally installed Next 15.5.3, React 19.1.0, and
Syncfusion PDF Viewer 34.1.30. The lockfile specifies Next 15.5.24 and React 19.1.5;
a clean locked-dependency release build and live Legal document check remain
deployment verification steps. The full ERP production build was not run.

## Release

Build a fresh frontend artifact from this change. Do not reuse a previous frontend
build. Deploy the new build and its static chunks together, then reload the Legal
document and verify selecting text, highlighting, and saving annotations. This
work changed local source only; it did not deploy or modify case/database data.

For vendor context, Syncfusion's [Next.js deployment guidance](https://help.syncfusion.com/document-processing/pdf/pdf-viewer/react/depoyment-integration/nextjs-getting-started)
also flags minification compatibility. This fix preserves only the affected worker
instead of disabling application-wide minification.
