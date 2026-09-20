# Physical count: one current count sheet

## User flow

1. Open an In Progress count → Details → Current count sheet → Upload.
2. Select the completed Excel file and check the preview.
3. Click Save count sheet. Quantities and the current-file designation are saved together.
4. To correct it, use Replace and save the revised file. Previous files remain in collapsed history.
5. Complete Count is unavailable until all quantities are saved and evidence is available.

Supporting attachments do not import quantities. Existing files uploaded through the former evidence-only button are labelled Attachment only, not guessed to be the counting source.

## Implementation

- New source-scoped multipart `POST /api/inventory/physical-counts/{id}/count-sheet`.
- Existing tenant/warehouse permission checks, clean-virus-scan requirement and central DMS storage retained.
- The server reads the actual workbook. It validates item identity, UOM, location, quantities and all source rows; formulas, duplicates, missing rows and invalid values are rejected.
- Zero means counted as zero. Blank uncounted rows remain uncounted. Replacements cannot silently omit a previously saved quantity.
- SQL Server execution strategy wraps a serializable transaction for all line updates, central document registration and the final count-sheet audit marker. Existing count and line row versions reject stale changes.
- Stable request key and file/version hash support retries without duplicate count attempts after a successful commit.
- A later manual quantity edit removes the previous sheet's current designation. Imported-sheet histories cannot substitute for a revised current sheet at completion.
- Existing append-only CountRecorded audit payload retains the source version. No new database column, enum value or migration.
- B20 is updated in both Markdown and HTML.

## Verification

- Release API build: passed, 0 errors (existing warnings remain).
- Focused API parser, source-history and controller-security tests: 28 passed.
- Focused page, import-dialog and spreadsheet-parser tests: 40 passed.
- Scoped physical-count TypeScript check: passed.
- Multipart service tests: 4 passed. Total focused automated tests: 72 passed.
- Live rehearsal baseline: PC-20260910-0001 remains InProgress, 7 active lines, 0 counted lines and 0 count attempts. Its two existing supporting attachments are preserved.
- The user explicitly reserved Save for themselves. Do not save their workbook, complete, approve or post this count as verification.

## Local build location

D: ran low on space while staging a rebuildable cache. Cleanup was blocked; no files were deleted. The API runtime is under `%LOCALAPPDATA%/Temp/tdc-count-current-20260911/api` on C:. The frontend runtime is `local-artifacts/physical-count-current-20260911/frontend-serving` on D:, with its `.next` build output junction pointing to C:. Application and dependency paths stay on the same drive; an initial all-C build failed because Next.js generated cross-drive module paths. The source remains in the regular repository. Rehearsal launchers accept these exact runtimes and retain port/database/outbound isolation. Main UAT remains stopped and its database is not changed by this fix.

The API restart invalidated the prior login token. The first sign-in timed out during cold startup and was cancelled, not rejected for credentials. A later visible sign-in succeeded as the supplied rehearsal manager account.

## Rehearsal handoff

- Corrected production build completed successfully (build ID `KN1L48u8uDPMuBl4ImkQu`). Frontend PID 5688 on 3002; updated API PID 31724 on 5002. Main ports 3000/5000 are not listening.
- Visible UI verified: Current count sheet has an Upload action; the two existing files are collapsed under history and labelled Attachment only. Complete Count is disabled for the uncounted draft quantities.
- Opened `PC-20260910-0001_CountSheet (3).xlsx` in the new preview: all seven quantities were recognised, with no blank rows and no blank Location column. Preview values: 20, 40, 60, 80, 120, 60, 58 in the workbook's item order.
- Left the preview open for the user. Save count sheet was not clicked. No completion, approval or posting occurred.
- Read-only SQL after preview confirms InProgress, 0/7 counted, 0 count attempts, and exactly the same two attached files.
- The live save/replace transaction remains for the user to exercise; the successful live preview is not being presented as a completed live save.
