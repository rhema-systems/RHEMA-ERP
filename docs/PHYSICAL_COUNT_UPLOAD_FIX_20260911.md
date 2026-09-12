# Physical-count extract and evidence upload — 11 September 2026

## Changes

- Export omits Location when all saved count lines have no location. Counts with saved location names retain the populated column. No location is inferred or written back to an in-progress count.
- Excel import accepts both formats, including previously downloaded five-column sheets. Four-column sheets cannot bypass saved locations; duplicate or ambiguous identities, changed UOM/names, formulas and invalid quantities remain rejected.
- The preview also omits an empty Location column. Export still contains no system quantities, saved counts, variance or cost data.
- Details evidence button is labelled **Upload**, with an accessible file-input label. Failed uploads preserve the selected file for retry.
- B20 in both customer walkthrough files uses the same labels and column rules.

## Scanner recovery

- Root cause: no listener on the configured local ClamAV endpoint, 127.0.0.1:3310. API readiness reported the file-virus-scanner check unhealthy. No infected-file result was reported.
- Reused the installed ClamAV under `C:\Program Files\ClamAV`, refreshed its official signatures using its existing freshclam configuration, and started the existing loopback-only clamd configuration. API readiness then returned Healthy with all three checks passing.
- No upload policy, clean-scan requirement, permissions or database schema was relaxed or changed.
- Both rehearsal startup scripts now invoke `Start-LocalFileScanner.ps1`. It reuses a healthy local scanner, starts the existing installation when stopped, waits for PONG, and fails with an actionable error if unavailable. It does not install a Windows service, expose a network port, or start main UAT.
- Manual recovery: `& .\scripts\procurement\Start-LocalFileScanner.ps1`.

## Verification

- 41 focused frontend tests pass (17 workbook, 5 import dialog, 16 physical-count page, 3 service); scoped TypeScript compilation passes.
- Live authenticated rehearsal browser retried the user's already-selected file `PC-20260910-0001_CountSheet (2).xlsx`. It is now listed as Clean / Published.
- Retained DMS reference: `DMS-INVENTORY-20260911172713-505d502abcc842bbaea268ed0d9cc2bd`, linked only to count `3f137166-d3b1-44cf-a742-5a95c1a2498b` / PC-20260910-0001 in the rehearsal database. SQL confirms a successful ClamAV scan at 17:27:11 UTC.
- This was evidence attachment only. SQL confirms the count remains InProgress with 7 active lines, 0 counted lines and 0 attempts. No count completion, stock posting or main-UAT data change was performed.
- The selected workbook has six entered quantities and a blank quantity for TOOL-SCAN-001. Importing it previews six rows and skips the blank; attaching it alone does not import these quantities.
- Startup helper parses successfully and two consecutive calls reuse the same running scanner.

## Updated rehearsal page

- Production build completed successfully: `4CCnn9eJRYsxcuFJ9Ke07`, served from `local-artifacts/physical-count-upload-20260911/frontend`. All three changed application source files match the build inputs by hash.
- Rehearsal frontend restarted on 3002 only; API 5002 was not restarted. The initial cold page request exceeded 20 seconds, then the page and all 50 referenced JS/CSS assets returned 200. The rehearsal header and 5002-only connection policy remain present; main ports 3000/5000 remain stopped.
- Live John Manager session confirms **Upload**, the retained Clean / Published evidence, and a four-column import preview with six actual quantities from the user's existing five-column file. One blank row is skipped. Preview remains open; **Save counts & attach sheet** was not clicked.
- Workbook export/import bytes passed the focused round-trip tests. The automated in-app browser download click did not produce an accessible downloaded file, so browser-to-disk download completion remains unverified; this is not represented as a successful downloaded-file inspection.
- API readiness remains Healthy, including the scanner. No migration is required. Main-UAT database data was not modified.
