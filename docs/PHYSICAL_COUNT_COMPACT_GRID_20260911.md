# Physical-count Items grid — 11 September 2026

## Delivered

- Compact, single-line item rows, small quantity inputs and a sticky header inside the dialog's scrolling grid.
- Search across all item codes, names and locations; 25, 50 or 100 rows per page with first/previous/next/last controls.
- Full page expands the Items view to the browser viewport; Restore returns to the fixed-height dialog without discarding edits, search or pagination state.
- Save Counts retains and saves edits across all pages. Existing blind-count protection, review permissions and draft-only item removal remain unchanged.
- Customer walkthrough B20 updated in both Markdown and HTML.

## Verification

- 23 grid/page tests passed, including a synthetic 300-line count, page-size and boundary cases, search, removal of the final row on a page, retained edits and full-page/restore.
- 14 existing upload/review tests passed.
- Focused TypeScript check passed. Production Next.js build passed; the production build itself skips global lint/type validation as configured by the existing project.
- Rehearsal browser verified as manager: seven saved lines displayed, search for PVC returned one line and clearing restored seven; page-size choices worked; Full page measured 1267 x 912 at (0,0), and Restore returned to 1000 x 680. The grid header computes as sticky.
- Saved quantities remained 20, 40, 60, 80, 120, 60 and 58. No count quantities were edited or saved, and no submission, approval, posting or database write was performed for this UI change.
- An unrelated existing GET /finance/Currencies/base permission failure (403) was observed during page load. This change does not alter Finance permissions or currency resolution; do not describe this check as a completely error-free page load.

## Runtime scope

Only rehearsal frontend port 3002 was switched. API 5002 was not restarted. Main UAT 3000/5000 remains stopped and unchanged. No new database migration is needed for this UI change; the earlier review-decision migration's separate UAT status is unchanged.

The active production frontend remains `local-artifacts/physical-count-review-20260911/frontend`. An immutable snapshot of the previous build was served from `local-artifacts/physical-count-grid-20260911/frontend-serving` while rebuilding, so the running application did not read a partially written build. Build output is on C: because D: has little free space. Both paths are accepted by the existing isolated rehearsal launcher.
