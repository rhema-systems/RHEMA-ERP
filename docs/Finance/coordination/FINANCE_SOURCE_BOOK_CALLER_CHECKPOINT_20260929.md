# Source-book caller remediation checkpoint

This is an implementation inventory, not a production sign-off. Exact baseline: `52ce3a71595ba18d26d40658c1a368743c44a853`. Isolated Finance work only; other-module implementation remains read-only. Never replace literal IFRS with literal BASE.

## Integrated first group

- Deposits/returned cheques: derive exact common original source book, retain native and functional amounts and approved commercial FX. Commits `d70914e7b`, `2e23e99b6`; focused20/20. Relational/UAT gates remain.
- Subledger adjustments/project capitalization: exact original/source authority and representation-specific asset register values. Commit `6dd452492`; focused20+7+5. Relational/UAT gates remain.
- Year-end: worker commits70eb22bbd,768f03d65,fe30feb3e; explicit book-local cycle and guarded close/reopen. Not yet integrated at this checkpoint.

## Shared authority prerequisite

Sol High cash worker owns first review unit in `RHEMA-ERP-finance-demo-ar-cash-authority-20260929`: immutable source-book authority plus origin rows, migration004 and helper tests. Freeze initial authority no later than approval; inherited sources must share exact book/currency; reversals bind original event/journal. No new caller may discover today's default after a historical approval. Posted legacy documents may be retained only from their exact source-held original journal/event; unposted legacy approvals need remediation/reapproval. No latest-event or replica guessing.

Root owns the combined snapshot and AP callers; worker owns AR invoice/payment and cash-transaction conversions after helper review. Keep producer intents/accounting events disabled unless separately certified.

## Remaining Finance callers — independent read-only inventory

Line references below describe worker/base sources and can shift during integration.

| Family | Evidence and risk | Required authority boundary |
|---|---|---|
| FixedAssetService direct capitalization | SubmitCapitalizationForApprovalAsync1434–1555 hashes approval snapshot; CapitalizeAsync1558–1719 hardcodesIFRS1658. | Freeze primary book in approval snapshot before dimension capture; reject stale legacy authority at posting. |
| FixedAssetService procurement capitalization | CapitalizeFromProcurementAsync1735–1909 hardcodesIFRS1815. ReceiptPostingEvidenceJson exists, but approval snapshot3548–3594 omits book. | Derive common exact receipt authority while building snapshot; verify original source and posted representation at handoff. |
| FixedAssetService reversal | PostCapitalizationReversalAsync2032–2168 gets exact reversal plan then hardcodesIFRS2092. | Inherit plan.OriginalJournalEntryId and retained event, never today's primary. |
| FixedAssetService source-driven register | AP/inventory adapters retain supplied journal/event IDs; ApplyCapitalizationAsync2743–2857 writes identical functional acquisition amounts to all active books, including Delta. | Validate exact source identity and reconcile only actual posted primary/parallel representations in their currencies; preserve existing history. This is the broader register counterpart of the locally corrected capital-project defect. |
| FinancePurchaseOrderReceiptPostingService | ApproveAndPostAsync33–79 / no-approval completion137–160 call request192–285 withIFRS280. Receipt lacks retained journal/event fields. HasPostedReceiptAsync178–190 accepts any posted source event, including replica. Approval stamps occur before post without a local transaction. | Freeze before approval or verified no-approval authorization; atomic source transition/post; helper binds exact primary posting and durable lineage. |
| InventoryReceiptFinancePostingService | PostAcceptedReceiptAsync29–82 is serialized, but source-event lookup55–60 chooses newest event across books; request77 hardcodesIFRS. | Freeze approved/source-independent authority within transaction, exact original replay, no newest-replica selection. |
| InventoryLandedCostFinancePostingService | PostLandedCostAsync164–254 requires posted valuation/movements, request249 hardcodesIFRS. | Inherit posted valuation/receipt authority where applicable; reject mixed sources. Do not simply pick today's primary for existing posted source costs. |
| SupplierDebitNoteService ordinary/allocated note | PostCoreAsync614–672 serialized; source retains journal/event. BuildPostingRequest1336–1652 defaultsIFRS1350 except allocated-return group. Linked ordinary invoice is ignored for book choice. | Linked notes inherit exact invoice authority; standalone note freezes primary before approval. Validate all dispatch/receipt authorities. |
| SupplierDebitNoteService reversal | ReverseCoreAsync741–802 uses retained posting plan thenIFRS774. | Exact original journal/book/currency inheritance. |
| SupplierDebitNoteService.InventoryReturns | EnsureReturnDispatchPostedAsync365–441 hardcodesIFRS425 despite original invoice journal and retained return posting IDs. | Inherit exact invoice/receipt authority before dimension capture; preserve original source lineage. |
| SupplierDebitNoteService allocated-return partials | ReturnAllocationPosting13–53 uses receipt book but only checks tenant/nondeleted; ReturnAllocations14–115 verifies common IDs/currency. | Also validate exact code, permitted lifecycle/type and original posted authority; no recoding from current defaults. |

## Coordinator/AR-cash inventory still in progress

- ARInvoiceService literal requests1688,1805; ARPaymentService1004,1046,2139,3290,3573; CashTransactionService1365,2143. Their source-event queries also require original/replica disambiguation.
- AP VendorInvoiceService3422,3541; VendorPaymentService1415,1458,2298,5518. Lease AP source authority must survive ordinary payment/void/reversal. Root and lease worker coordinate the shared invoice service.
- Existing explicit-source callers (opening balances, journal batches, recurring journals, FX revaluation, allocations, inventory issue/adjustment and other fixed-asset producers) still require the complete construction matrix/contract sweep; they are not silently marked passed.
- HRPayrollService literal5839 is an owner handoff, not authority to edit HR.
- FinancePostingDtos and FinanceExternalProducerDtos retain unsafe defaults until all production in-scope callers and owner dependencies pass explicit-authority contracts. No blanket default replacement is allowed.

## Next partitions

1. Finish and independently review shared helper/schema; then AR/cash and root AP conversions.
2. Finance-owned receipt/valuation and supplier-debit-note conversion units with source-specific tests.
3. FixedAsset direct/procurement/reversal and representation-aware register reconciliation, with immutable approval snapshot versioning and real-service rollback tests.
4. Complete construction matrix, owner handoffs, default-removal contract test and combined SQL/UAT verification.
