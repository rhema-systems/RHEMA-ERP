# Physical count draft editing and dialog layout

## Changes

- Draft records expose **Edit draft**. **Details** supports optional notes; **Items** supports saved additions and confirmed removals.
- The item selector searches warehouse-assigned items by code/name, excludes items already in the count, and requires an active location. System quantities are not displayed or sent by the add form.
- Add/remove operations use the existing authenticated count endpoints. Their existing server-side Draft guards remain. No stock, approval or posting logic was changed.
- Failed mutations retain the dialog and selections and display the returned error. A successful mutation followed by a refresh error closes the editor and asks for a register refresh to avoid duplicate retries.
- Details, Items, Variance and Control history share a 680px dialog (maximum 90% viewport height), with scrolling content and a fixed footer.
- Evidence wording explains the count sheet's purpose and that no file is needed to prepare a draft. The existing evidence requirement before count completion remains.
- Walkthrough B20 now includes Edit draft, Add item, Remove, optional notes and the evidence explanation.

## Architecture evidence

Read-only inspection of the original `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\test scripts\TDC ERP Architecture and Design Document (002).docx`, section **16.1 Stores, Inventory, and Fixed Asset Design**, confirms that the solution must maintain **stock-taking evidence** among the inventory records.

The document does not prescribe the exact UI timing or a minimum file count. Requiring a clean, saved evidence file before **Complete Count** is the current application's implementation. A completed count sheet or reconciliation record lets independent reviewers compare the evidence with the entered quantities.

## Validation and scope

- Focused TypeScript compilation passed: `tsc -p tsconfig.physical-counts.json`.
- 16 frontend/service tests passed, covering draft notes, confirmed removal, failure/retry, plain-text and coded server errors, draft-only visibility, add-item scope/payload, fixed tabs and the evidence control.
- No migration, database repair, count start, approval, adjustment or posting was performed for this change.
- This does not certify previously open count-scope creation or nonzero-variance valuation behavior. The new-count form still lacks category/location scope selectors. Warehouse/location reconciliation must be checked before starting the prepared UAT scenario.
- No live browser-control tool was available during this turn, so component tests are not presented as an authenticated browser acceptance run.

## Rehearsal deployment verification — 11 September 2026

- Final production build passed with build ID `F2d9SEo9Ud8C0wuss9zZn`. Both changed runtime source files matched the regular checkout by SHA-256 before startup.
- Started only the isolated rehearsal using `Start-LocalPoRehearsal.ps1`: frontend `127.0.0.1:3002` (PID 25812), API `127.0.0.1:5002` (PID 29944), outbound blocker `127.0.0.1:5519` (PID 14496). These PIDs are a startup snapshot, not permanent identifiers.
- The launcher verified database `RhemaERP_PO_Rehearsal_20260909` on `RHEMA-MICHAEL\SQL2017` and the rehearsal outbound configuration before starting. No migration was needed or applied.
- Physical-count page and all 50 referenced JavaScript/CSS assets returned HTTP 200. The served count-page script contains the new draft action, evidence guidance and fixed-height dialog class.
- Rehearsal identity header and API isolation policy passed; API `/health/live` returned 200. Main UAT ports 3000 and 5000 remained stopped.
- HTTP/asset checks do not establish authenticated browser acceptance. No saved count was edited, started, approved or posted during this verification.

## Follow-up: live draft removal repair — 11 September 2026

- A subsequent authenticated browser attempt to remove `SPARK-PLUG-001` from rehearsal draft `PC-20260910-0001` reproduced HTTP 500, reference `0HNOG3MS8O6B5:00000002`.
- Root cause: `GenericRepository.DeleteAsync` soft-deletes the row, while `TR_PhysicalCountItems_ControlledMutation` rejected every `IsDeleted` change, including Draft. Transactional SQL reproduced error 51923 (`INV_COUNT_LINE_SOURCE_IMMUTABLE`) in both databases.
- Added SQL-only EF migration `20260911170000_AllowDraftPhysicalCountLineRemoval`. It permits only active-to-soft-deleted transitions under an active Draft parent. Started-count history, tenant/item/location identity, snapshot quantity, cost, first-count and recount guards remain in place. The migration includes a guarded downgrade and does not need a model-snapshot change.
- Applied only this migration to local `RhemaERP` and `RhemaERP_PO_Rehearsal_20260909`, through the target-restricted `Repair-LocalPhysicalCountDraftRemoval.ps1`. No other pending migrations were applied. Both enabled triggers have SHA-256 `DCE774193ECC8BA69DFEDCDBF64586A7A4C8D61E4823126E223C2A7881FF7BDC` after the repair (original: `070EAA382709071C399F4EB2ADB644B594146DA00EDB123A5B77DE551EB0056E`). Reapplication was verified as idempotent in rehearsal.
- `Test-LocalPhysicalCountDraftRemoval.ps1` passed ten SQL regression cases per database after the fix: draft removal, immutable cost/snapshot, tenant foreign key, first-count/recount action guards, started soft/hard deletion, mixed-state update, and restoration protection. All fixtures and mutations were rolled back; no test fixtures remain. Nine baseline cases per database had already confirmed the original behavior.
- Data project compilation passed with zero errors. Existing unrelated compiler warnings remain.
- Retried the actual **Remove item** action in the signed-in rehearsal browser: the confirmation closed and **Items (8)** loaded. SQL confirmed `TotalItems=8`, eight active lines, and `SPARK-PLUG-001` retained as soft-deleted. The user-authorized removal affects this rehearsal draft only, not the inventory item master or stock balances.
- UAT retained its original Cancelled nine-line count and Posted two-line count. No UAT count data was committed by testing. Main UAT remains stopped; rehearsal stayed running without a server restart or frontend rebuild.
