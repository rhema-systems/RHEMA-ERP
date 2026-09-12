# One landed-cost Post action

## Operator flow

1. Receiving account with `procurement.inventory.receive` and the receipt warehouse scope: save receipt costs, selecting the actual cost supplier for each charge.
2. **Allocate** and review the allocated amounts. Allocation itself neither posts nor creates invoices.
3. **Post**: confirm the supplier invoice reference for every charge and an initial invoice date. No tax selection is required here.
4. **Confirm Post** posts inventory and generates one AP draft per supplier/currency/bill reference. The returned invoice links identify the results.
5. AP officer: open each invoice → **Edit invoice** → select line **Tax treatment** and, for Standard, a purchase **Tax Group** → **Save Changes**. Correct the invoice date if necessary. The existing independent approval and posting processes follow.

For a voucher already Posted, **Retry Post → Finish invoice drafts** performs only the missing invoice handoff. Do not allocate or post inventory again and do not add replacement charges.

## Controls

- Existing Procurement capability enforcement checks the internal actor, receiving permission and source warehouse. This permits only source-bound draft creation as a posting side effect; no AP role or payment permission is added.
- AP owns supplier resolution, numbering, source dimensions, duplicate checks and draft creation. The combined action never approves, financially posts or pays an AP invoice.
- Billing metadata is retained before inventory posting. Inventory and invoice transactions are serialized and retries retain the existing posted value and invoice links. A partial response explicitly reports inventory posted / invoices pending.
- Posted cost values, allocations and the goods PO amount remain unchanged during invoice handoff. Invoice posting clears the original landed-cost accrual rather than capitalizing inventory again.
- Draft tax uses the existing integer column with `PendingReview = 5` (not the CLR default/sentinel). It does not imply exemption or zero rating. Submission, approval and posting reject unreviewed landed-cost tax.
- No new schema migration is needed. The earlier `20260909213000_LinkLandedCostsToSupplierInvoiceLines` migration remains a prerequisite. Main UAT is not migrated or restarted by this work.

## Verification

- Frontend: 32 posting/entry/summary/grouping tests and 13 tax-state tests passed.
- Affected-page typecheck reports only the existing duplicate `fiscalPeriodId` declarations in `frontend/src/types/finance.ts` (307, 351).
- Backend: API build passed with zero errors; all 32 focused landed-cost supplier-invoice tests passed. Coverage includes combined posting, already-posted recovery, duplicate prevention, grouping, and tax-review guards before workflow and financial posting.
- Rehearsal deployment: verified API binaries and the affected frontend files were copied to the rehearsal runtime, and only ports 5002 / 3002 were restarted. The rehearsal API health check returned HTTP 200 after warm-up. Main UAT on ports 5000 / 3000 was not restarted, migrated, or otherwise changed by this deployment.
- Live acceptance is still pending: the browser remained on a connection-error page after the restart, and browser automation could not navigate out of that error page. Reload the rehearsal login manually before resuming. No inventory posting or invoice creation was performed during this implementation check. Do not equate automated tests or the health response with live financial acceptance.
- Existing rehearsal LC26090977 (GHS 360) and user-created LC26099889 (GHS 550) are already Posted. The latter has Adom Construction Ltd / GHS 300 and Seabright Demo Goods Ltd / GHS 250 selected, but no invoice references. No supplier bill references were invented.
