# AR customer-advance application and date clarity UAT

## Objective

- Correct misleading date presentation in the AR receipt allocation screen and partner detailed ledger.
- Define a governed, discoverable workflow for applying an existing posted customer advance (payment on account) to an open customer invoice without recording cash twice.

## Scope and location

- Worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\bp-workflow-fixed-asset-transaction`
- Branch: `codex/business-partner-workflow-fixed-asset-transaction`
- Exact base: `dd6fb224a1eaa7512087ed3bf0dd767d490c0f12`
- Existing branch commits before this workstream: `14f1bb5c6`, `52e41f6aa`, `5ccce718c`

## Confirmed current behavior

- A new receipt is allocated only when the submitted `PaymentCreateDto.Allocations` contains explicit invoice allocation rows.
- A receipt submitted with no allocation rows is posted as a customer advance.
- A later invoice does not automatically consume that advance.
- A posted customer advance can already be applied through `POST /api/ar/payments/{customerPaymentId}/allocate` (route prefix as configured by the consolidated AR controller). The service posts the advance-to-AR reclassification and realized FX through the central Finance posting engine.
- The UI exposes that operation only from Customer Receipts > receipt action menu > **Allocate Receipt**. The new-receipt/invoice settlement screen cannot select an existing on-account balance directly.

## Completed changes

- Renamed the receipt allocation invoice column from `Date` to `Due date`; its value is `invoice.dueDate`.
- Split partner detailed-ledger presentation into `Business date` (the balance-ordering/accounting date) and `Posted date / time` (the actual posting timestamp).
- Added both date fields to the component's CSV export.
- Missing posting timestamps now render as `-` instead of silently falling back to the business date.
- Added focused regression coverage for the new labels and independent date semantics.

## Changed files

- `frontend/src/app/finance/ar/payments/new/page.tsx`
- `frontend/src/app/finance/ar/payments/new/page.test.ts`
- `frontend/src/components/finance/PartnerDetailedLedgerReport.tsx`
- `frontend/src/components/finance/PartnerDetailedLedgerReport.datetime.test.tsx`

## Verification

- PASS: `npm test -- --run src/components/finance/PartnerDetailedLedgerReport.datetime.test.tsx src/components/finance/PartnerDetailedLedgerReport.supplier-identity.test.tsx src/app/finance/ar/payments/new/page.test.ts`
- Result: 3 files, 10 tests passed.
- PASS: `git diff --check` (line-ending warnings only).
- Full `npm run type-check` was stopped after several minutes without output; it did not report a TypeScript error before termination. This remains outstanding.

## Proposed implementation: apply payment on account

1. Extend the frontend `PaymentQuery` and `arService.getPayments` serialization to support the existing API filter `HasUnallocatedAmount=true`.
2. Add a customer-scoped `available advances` query after the customer is selected. Request posted receipts with an unapplied amount and exclude credit notes/reversed or diagnostic records. Prefer exposing `IsCustomerAdvance` in `CustomerPaymentDto` (or add a dedicated application-candidate endpoint) so eligibility is explicit rather than inferred.
3. On the receipt/invoice settlement page, add a mode switch: **Record new receipt** / **Apply payment on account**. In application mode, show each eligible advance lot with receipt number, receipt date, currency, original amount, applied amount, and available amount.
4. Require the user to select one exact advance lot. Reuse the existing `paymentId` application path and outstanding-invoice grid; do not create a new payment and do not repost cash.
5. Preserve current currency-lot rules. For cross-currency settlement, require the receipt-currency amount and approved settlement-rate evidence already enforced by `AllocatePostedCustomerAdvanceAsync`.
6. Add a direct **Apply payment on account** action from an invoice/customer account view, preselecting the customer and invoice where possible. Keep Customer Receipts > **Allocate Receipt** as an alternative entry point.
7. Refresh invoice balance, receipt unapplied balance, customer balance, and settlement reports after success; navigate to the receipt trace so the reclassification journal is auditable.
8. Add API/service tests for eligibility, same-customer enforcement, over-allocation, currency-lot handling, repeat application, and no second cash posting. Add UI tests for no eligible balance, single/multiple advances, preselected invoice, and success/error refresh behavior.

## Acceptance criteria

- A user can select an existing posted customer advance for the same customer and apply it to an open invoice.
- The workflow creates no new cash receipt and no duplicate bank/liquidity posting.
- The selected advance's unapplied amount and the invoice balance both reduce by the same governed settlement evidence.
- Ineligible, reversed, fully applied, wrong-customer, or diagnostic advances cannot be selected or posted.
- The application appears in receipt trace, customer ledger, and settlement read model with correct source lineage.
- No automatic application occurs merely because an invoice is created; application remains an explicit auditable user action.

## Remaining work

- Commit the completed date-clarity changes and this ledger.
- Run the full frontend type-check when a longer verification window is available.
- Implement the payment-on-account workflow only after explicit authorization to move from planning to implementation.

## Authorization boundaries

- Authorized: local date-label/report corrections, focused tests, investigation, and implementation planning.
- Not authorized: payment-on-account feature implementation, database changes, migrations, push, pull request creation, merge, or deployment.

