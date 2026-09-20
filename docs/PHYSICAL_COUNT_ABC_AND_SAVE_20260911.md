# Physical count ABC filtering and Save actions — 11 September 2026

## Changes

- Manual creation of a Cycle Count now populates only warehouse items with the selected ABC class. A/B/C selections, normalized lowercase input and the existing default C are covered. A class without matches produces an empty draft. Full Count does not inherit the default cycle-count class filter. Existing counts are not repopulated or rewritten.
- Save Counts remains visible for the assigned counter in In Progress/Under Review, below every normal dialog tab and inside the full-page Items view. It is disabled when there are no pending quantity edits. A successful quantity save displays a success toast.
- An outdated imported sheet now has a direct Upload revised count sheet button next to the submission warning. The wording distinguishes saved quantities from the outdated Excel source. The button opens the existing preview/import dialog and its Save count sheet action; pending quantity edits must be saved before opening it.
- Existing sheet validation, scan checks, source lineage, review/submission controls and stock-posting requirements are unchanged. Uploading a replacement still replaces the listed quantities with that file's values; the file must contain the user's corrections.
- Customer walkthrough B20 step 8 updated in Markdown and HTML.

## Verification

- Core Release build passed: 0 errors, 505 existing warnings.
- 14 focused Core lifecycle/ABC tests passed, including seven new manual-count scope cases.
- 42 frontend page/grid/review/upload tests passed. The page tests exercise saving quantity edits from Variance and opening the sheet-save dialog directly from the stale-sheet warning.
- Focused TypeScript validation passed. Production frontend build passed.
- Rehearsal API health returned 200. The loaded Core assembly matches the tested SHA256: `252A53B8A4FA3CE9903EF147BA1A51B1307EEF6850DA515840F8D3F0436BF627`.
- Live manager browser check: existing PC-20260910-0001 remains Under Review with 7 counted lines and 6 variances. Save Counts is visible and disabled beside Quantities saved. The revised-sheet button is visible on Variance and opens Upload count sheet, where Save count sheet is visible and disabled until a valid file is chosen. No file or quantities were saved, and no submission, approval or stock posting was performed by this check. The user's earlier quantity correction is preserved.
- No new live test counts were created; ABC A/B/C creation was exercised by isolated automated tests, not by inserting fixtures into the rehearsal database.

## Deployment and boundaries

Rehearsal only: API 5002 runs from `C:/Users/micha/AppData/Local/Temp/tdc-count-abc-20260911/api`; frontend 3002 runs from `local-artifacts/physical-count-review-20260911/frontend`. The existing launcher selects the prepared ABC runtime on restart. Main UAT 3000/5000 is stopped and was not changed. No new migration was created or applied; earlier migrations have their separately recorded UAT deployment status.

The original API Core DLL was locked after stopping its server. Its hash confirmed that the attempted overwrite had not replaced it. The old PDB was restored from backup, and a separate runtime was staged with the tested DLL/PDB instead. The original binary/log backup is in `C:/Users/micha/AppData/Local/Temp/tdc-count-abc-20260911-before`. No locking process was force-killed outside the verified rehearsal API. The restarted manager session was restored through normal sign-in.

The current legacy count still has no exact item locations. This is independent of the Save controls and remains a blocker for a controlled nonzero-variance adjustment; the change does not invent locations or bypass that check. The previous imported sheet is still outdated until the user supplies and saves a corrected file.
