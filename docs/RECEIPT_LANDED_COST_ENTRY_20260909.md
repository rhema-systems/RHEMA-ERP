# Receipt-time landed cost entry — 9 September 2026

## Operator flow

Saved receipt → **Landed Cost → Add receipt costs**. Enter one or more charges, currency/rate, optional reference, and choose all received stock items or a specific received PO line. **Save draft costs** does not require a PO estimate, alter the supplier PO total, allocate costs, post inventory, or create an invoice.

Select the saved voucher → **Edit draft costs** to correct a draft or replace copied estimates with actual amounts. Shared charges support value, quantity or equal allocation. Item-specific charges retain the exact PO-line identity and are not prorated like a PO estimate: the entered actual amount applies to this receipt.

**Copy PO estimates (optional)** remains a separate starting point. It refuses to overwrite an existing draft. Do not enter both the estimate and its actual replacement as separate charges. The server and UI lock editing after allocation, approval, posting or cancellation. A genuinely additional later charge can use another draft; correcting already posted values requires the existing controlled adjustment process, not this draft editor.

## Safety and verification

- Draft create/update use a serializable transaction inside the EF execution strategy. Validation precedes changes; errors roll back and clear tracked state.
- Retried creation uses a stable request ID; an identical retry returns the same voucher. Reusing it for changed content is rejected. Draft edits check a content token to reject stale overwrites.
- Validates positive amounts/rates, currency consistency, limits, and receipt-specific targets. Existing saved supplier references are preserved when editing.
- Existing stock allocation, inventory posting, Finance posting and approval rules were not changed. No database migration or account/role change was needed; the existing target column was confirmed in both local copies.
- API build passed with 0 errors and existing warnings. Latest Core assembly rebuilt successfully after the final service edits.
- 21 focused service/regression tests passed, including direct create without estimates, targeted charges, proration, invalid input, retry deduplication, stale/posted edit rejection, preserving an existing draft, and transaction rollback.
- 8 receipt-entry UI tests passed; 11 GRN control regression tests passed separately. Focused TypeScript and whitespace checks passed.
- Walkthrough HTML and Markdown updated with receipt-time entry, draft editing, separate posting and avoiding duplicate charges.

## Runtime boundary

- Main UAT API/frontend processes (5000/3000) were not stopped or updated. Their regular source files include the changes but need a planned build/switch before demonstrating them there.
- Rehearsal API on 5002 now loads the validated API/Core assemblies; deployed hashes were verified equal to the build. Previous assemblies are preserved in `local-artifacts/receipt-landed-costs-20260909/api-before-update`.
- The three frontend source changes were mirrored to the guarded rehearsal preview on 3002. That frontend was restarted after its hot reload became unresponsive.
- Rehearsal API health returned HTTP 200 after warming up, and the restarted frontend reported Ready. All three mirrored UI files have no normalized diff from regular source.
- Before the test, both databases had zero landed-cost vouchers, active cost lines and posted vouchers. No receipt cost, inventory posting or invoice was created during implementation.
- **Live draft save/reopen acceptance passed:** after the user signed in as `manager`, the visible rehearsal browser saved and reopened receipt costs on REC260003. Allocation, inventory posting and Finance posting were not exercised by this draft-only test.

## Visible rehearsal draft test

- Receipt: `490b5d42-1a70-438a-9240-08de2223df82` (REC260003), on `127.0.0.1:3002` with the REHEARSAL banner and John Manager signed in.
- Used **Landed Cost → Add receipt costs**, not **Copy PO estimates**. Entered simulated shared freight of GHS 300 and item-specific handling of GHS 50 for PVC Pipe 50mm.
- **Save draft costs** created `LC26090977` (`ee31c562-7304-4e72-a573-1c687ac471a4`), total GHS 350. A full browser reload and reopening Landed Cost retained both charges, targets, references and notes.
- **Edit draft costs** changed shared freight to GHS 310. Saving and refreshing retained the same voucher, total GHS 360, with two active cost lines; the GHS 50 handling charge retained PO-line ID `0549f1df-3e99-4340-bd5e-7333521412cf`.
- Read-only SQL confirmed the rehearsal voucher is Draft, allocated amount zero, zero allocation rows, and no approval or posting dates. The voucher remains clearly labelled LOCAL REHEARSAL ONLY with a note that it is not an actual supplier invoice and must not be posted.
- Main UAT database `RhemaERP` still had zero landed-cost vouchers and zero active cost lines after this test. No main UAT runtime switch, allocation, inventory posting, invoice creation or Finance action was performed.

## Subsequent allocation/posting rehearsal — user requested

- After the draft-only test, the user explicitly requested allocation and posting in rehearsal. The pending stock-movement currency API deployment was paused; no API restart occurred.
- Clicked **Allocate** on LC26090977 in the visible rehearsal browser as `manager`. Success: status Allocated, total and allocated GHS 360, unallocated zero. PVC Pipe received GHS 276.54 (freight 226.54 + targeted handling 50); Barcode Device Kit received GHS 83.46.
- Clicked **Post to Inventory** once. The request returned HTTP 400: `Write-off Expense Account is required as the configured landed-cost variance account.` Correlation ID: `0HNOEKIKM05AH:00000011`.
- Read-only SQL confirmed the failed attempt left the voucher Allocated, PostedDate null, with no valuation movements or Finance posting event/journal for this voucher. Main UAT still had zero landed-cost vouchers.
- Root cause traced to the Inventory FIFO calculation in `LandedCostService.PostToInventoryOnlyAsync`: `totalAllocatedQty = group.Sum(x => x.Quantity)` counts the same received line once per charge. The PVC receipt has one FIFO layer with original/remaining quantity 20, but freight and handling each carry allocation quantity 20, producing a denominator of 40. This incorrectly calculates a 50% inventory share and GHS 138.27 variance although all 20 units from this receipt remain.
- The distinct receipt quantity must be used for the FIFO retained-stock calculation, and multiple charge allocations for one receipt line must retain correct layer linkage. No write-off mapping, Finance code, inventory valuation method or stock quantity was changed to force posting through. Calculation repair and retry remain pending user direction.

## Authorized calculation repair and retry — passed

The user subsequently authorized the repair and retry. After the tested FIFO fix was deployed to rehearsal, the visible **Post to Inventory** action on LC26090977 succeeded as John Manager at `2026-09-09T20:43:04.6986145`. SQL confirmed two value-only movements totalling GHS 360, zero variance, and one balanced Finance journal (`7fea7287-b105-44b2-93e2-e6f6966f44fc`). Both PVC charges retain the same FIFO layer. Main UAT still has zero landed-cost vouchers. See `LANDED_COST_FIFO_AND_SOURCE_LINKAGE_20260909.md` for the regression and deployment evidence. Earlier “do not post” notes describe the original draft-only test; this later posting was explicitly authorized by the user.
