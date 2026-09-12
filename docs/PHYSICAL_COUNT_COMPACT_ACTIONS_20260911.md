# Physical count direct editing and compact actions — 11 September 2026

## Changes

- Count details now has Details, Items and Control history tabs. The separate Variance tab is removed; Items retains System, Counted and Variance columns with existing blind-count protection.
- Save Counts and Submit for approval share a compact action row. Repetitive instructions no longer consume the dialog footer.
- Search and direct quantity editing work in normal and full-page Items views. Saved count lines remain authoritative after manual corrections to an earlier Excel import. Re-uploading the spreadsheet is not required.
- Earlier imported files remain in upload history and may serve as supporting evidence. Submission still validates count ownership, review status, complete quantities, current published clean tenant-owned DMS evidence and independent approvals. Posting requirements are unchanged.
- The register View action is an accessible eye icon. Draft rows also have a red Cancel draft count icon, opening a required-reason confirmation popup. Keep draft dismisses it without cancellation. Failures retain the reason and display server detail/code.
- Walkthrough B20 updated in Markdown and HTML.

## Checks

- Core Release build: passed, no errors.
- 16 focused Core lifecycle tests passed, including manual correction after import and continued rejection of infected evidence.
- 54 frontend page/grid/review/upload tests passed. Coverage includes cancellation confirmation, whitespace rejection, failure retention, icon-only View, same-row Save/Submit, full-page search/edit/save without re-upload, pagination across 300 lines and blind-count protection.
- Focused TypeScript check passed.
- Production frontend build passed. Rehearsal API health returned 200; ports 5002 and 3002 run the updated API and frontend. Main UAT ports remain stopped.
- Live manager browser verification on PC-20260910-0001: three tabs, Save Counts and Submit for approval side by side, all seven item rows visible without the former oversized warning/footer, and no Excel re-upload warning. The eye-only register action opens the count. Saved quantities remain unchanged and Submit is not clicked.
- There are no Draft counts in the current rehearsal register; the cancel popup and its success/failure paths were verified by automated component tests, without creating or cancelling a live count.
- No database migration is needed for these changes. No count quantities, files, cancellations, submissions, approvals or stock postings were performed by these tests against either live database.

## Rehearsal runtime

The prepared API uses `C:/Users/micha/AppData/Local/Temp/tdc-count-manual-20260911/api`, with tested Core SHA256 `9BDEB3DC389684FDCCA328D85ADABF0A3E575DE7EAB0AA5202B09FEC3B7B6AA6`. The API launcher prefers that runtime when its ready marker is present. Main UAT remains stopped and was not restarted.

The existing count's missing exact locations remain a separate posting/submission constraint for nonzero variance; this change does not invent locations or bypass stock-location validation.
