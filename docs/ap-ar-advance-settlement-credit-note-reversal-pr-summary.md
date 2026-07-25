# AP/AR Advance Settlement and Sales Credit-Note Correction

## Scope

This Finance-only change resolves `FIN-LIM-0045` for functional-currency supplier/customer advances and `FIN-LIM-0012` for Sales credit-note correction. It preserves posted GL as the accounting source of truth.

## Accounting Design

- Supplier advance origin: `Dr Supplier Advance / Cr Bank or Cash`.
- Supplier advance application: `Dr AP Control / Cr Supplier Advance`.
- Customer advance origin: `Dr Bank or Cash / Cr Customer Advance`.
- Customer advance application: `Dr Customer Advance / Cr AR Control`.
- Sales credit-note correction: `IFinancePostingEngine.GetReversalPlanAsync` derives inverse lines from the original journal; a new reversal event/journal is posted and linked to the immutable original source document.

All application/reversal postings use tenant-scoped account validation, fiscal-period validation, idempotency keys, and `IFinancePostingEngine`. The application and source/document updates run in serializable transactions to prevent duplicate use of an advance or duplicate credit-note reversal.

## Reporting and Diagnostics

`SubledgerUnappliedSettlementBalance` is a tenant-scoped, rebuildable read model for unapplied payments/receipts. It is intentionally separate from AP/AR invoice aging. Advance allocations reduce an invoice only when their immutable, posted advance-application event is linked; a missing link is diagnosed and excluded rather than allowing the cash-origin journal to settle AP/AR. The dedicated AP/AR report endpoints rebuild the projection from posted payment/receipt events and allocations and audit report generation.

## Safe Boundaries

- Foreign-currency advances are rejected pending advance-specific FX application/settlement logic.
- AP payment and AR receipt reversals are not included and remain `FIN-LIM-0009` / `FIN-LIM-0010`.
- Compatibility `CustomerPayment.IsCreditNote` remains `FIN-LIM-0013`.
- No direct balance mutation or parallel posting path is introduced.

## Migration

`20260717110000_AddAdvanceSettlementAndCreditNoteReversalFoundation` adds the account mappings, advance flags, immutable application/reversal references, and read-model table. It is additive and does not create, alter, or delete posted journals.

## Verification

- `ErpSystem.Data`, `ErpSystem.Api`, and `ErpSystem.Api.Tests` build successfully.
- Focused AP payment, AR receipt, Sales credit-note, and settlement read-model tests: `72/72` passed.
- Finance go-live regression slice: `429/429` passed.

## Review Notes

The code comments mark the cross-module boundaries in `VendorPaymentService`, `PaymentService`, `ReturnOrderService`, and `ApplicationDbContext` so Sales, AP, AR, reporting, and migration owners can resolve merge conflicts without replacing the Finance posting-engine controls.
