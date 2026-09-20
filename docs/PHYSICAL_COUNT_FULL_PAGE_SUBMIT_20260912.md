# Physical-count full-page submission action

- The same fixed action footer is visible in dialog and full-page Items modes. Save Counts and Submit for approval stay on one row outside the scrolling grid.
- Removed the duplicate full-page-only Save Counts implementation. Switching views preserves the shared action state and all unsaved quantity edits.
- Submit for approval and its confirmation button use the red/destructive visual variant. This is a colour change only; submission does not approve or post stock.
- Existing status, review permission, complete-count, evidence, unsaved-edit and busy-state checks remain in force.
- Walkthrough B20 updated in Markdown and HTML.

## Separate backend issue — subsequently resolved

This UI change alone did not fix the reproduced backend failure on rehearsal PC-20260911-0004. A subsequent valuation repair aligned the database trigger with location-specific costs, and the user authorized publication of the existing approval workflow. The saved count was then successfully submitted through the visible UI and now shows **Stores Approval**, without approval or stock posting. See [valuation and submission verification](PHYSICAL_COUNT_VALUATION_SUBMISSION_20260912.md).

## Verification

- 60 focused page/grid/review-action tests passed. The first parallel run hit one 5-second timeout in an existing location-picker test; the serial run passed all tests with a 15-second ceiling.
- Focused TypeScript check passed.
- No database migration or accounting mutation is part of this UI change. Read-only main UAT history confirms three existing migrations are pending: `20260909213000_LinkLandedCostsToSupplierInvoiceLines`, `20260911210000_PhysicalCountReviewDecisions`, and `20260912003000_WarehouseDefaultLocations`. Normal updated API startup calls `Database.MigrateAsync` unless startup initialization is skipped; verify migration history, as local Development can continue after an initialization failure. Main UAT was not started or migrated in this task.
- Initial isolated build under the C: temporary directory failed because Next.js resolved D: dependency paths as relative imports. The retry stages source on the project D: drive, with cached build output retained under the temporary directory. No live server was stopped for this failed build.
- Same-drive production build passed (exit 0), build ID `pp9LiTBNRSLl8L-im96CN`. Next.js skips its broad lint/type checks by existing project configuration; the separate focused TypeScript check and 60 tests above passed.
- Deployed through the guarded rehearsal production launcher on port 3002. The count route returned HTTP 200; the frontend became ready in 1035ms. API port 5002 remained on the existing process; main UAT ports 3000 and 5000 remained stopped.
- Visible browser check as John Manager on PC-20260911-0004: Items -> Full page retains the fixed Save Counts and red Submit for approval buttons on one row at the bottom. Opened the submission confirmation and verified its red button, then dismissed it with Cancel. No quantities were edited or saved and no submission, approval or posting request was made.
- At the end of the original UI-only check, the saved nine-line count remained Under review. The subsequent backend repair and successful submission are recorded separately above.
