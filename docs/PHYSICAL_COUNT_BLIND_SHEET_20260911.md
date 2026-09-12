# Physical-count Excel workflow — 11 September 2026

Follow-up: [extract location and upload recovery](PHYSICAL_COUNT_UPLOAD_FIX_20260911.md) supersedes the original five-column-only rule below. New exports omit Location when the saved count has none; older five-column sheets still work.

## Change

- The counting workbook contains one sheet and exactly five columns: Item Code, Item Name, UOM, Location, Counted Qty.
- Every exported Counted Qty is blank, including exports of previously counted records. System quantities, saved counts, variances, costs, notes and tracking data are not embedded in the workbook.
- In Progress → Items now provides Download count sheet and Upload count sheet. The upload previews rows before saving. Blank quantities are skipped; explicit zero is saved.
- Upload matches the item code and location to exactly one saved line and checks its name/UOM. Changed identities, duplicates, formulas and invalid quantities fail before writes.
- Saving uses the existing governed per-line record-item API with row version and a stable retry key. Existing tracking fields are retained. Successfully saved rows are not resent after a later row fails.
- The same Excel file is attached through central count evidence. If attachment fails after counts save, retry attaches the file without recording the quantities again.
- Protected saved inputs display a Saved placeholder, not an artificial zero. Clearing a manual input does not save zero.
- Complete Count and any independent recount still precede disclosure of blind-count variance results. Approval, evidence and posting controls are unchanged.

## Verification

- 36 focused tests pass: workbook structure/round trip, blank versus zero, input validation, draft editing, upload preview, governed request construction, partial retry and evidence retry.
- Focused TypeScript compilation passes.
- B20 in the Markdown and HTML customer walkthroughs now documents download, Excel entry, upload/preview, save/attach and completion in that order.
- This is a frontend-only change. No database migration or changes to stored stock/count quantities are needed.

## Rehearsal verification

- Production build completed successfully; build ID `ph5NQ6AYhjNBR2gb5fowF`. Rehearsal frontend restarted on 3002, API remains on 5002. Main UAT 3000/5000 remains stopped.
- Count page and all 50 referenced JS/CSS assets return 200; rehearsal identity header and API isolation policy are intact.
- Visible authenticated browser as John Manager confirms the two new count-sheet actions and blank first-count inputs.
- The old nine-column workbook was rejected before saving. A five-column QA copy of the user's existing entries previewed all seven matching rows, with quantities 0, 60, 25, 45, 23, 20 and 10. No save, completion, approval or posting was clicked.
- SQL before and after: PC-20260910-0001 remains InProgress, 7 active lines, 0 counted lines and 0 count attempts. No stock or main-UAT data was changed.
- The workbook generator's exported bytes passed round-trip structure tests, including absence of system/variance data. The in-app browser download event did not provide a file, so an actual browser-downloaded workbook was not verified. A separate five-column preview fixture was rendered and checked; it is not evidence of browser download completion.
- Existing unrelated browser warning: the manager's base-currency lookup returns 403 and the page uses its existing fallback. No count export or upload error appeared in the console.
