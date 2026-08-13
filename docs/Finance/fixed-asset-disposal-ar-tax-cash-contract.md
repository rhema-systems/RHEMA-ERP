# FIN-INT-006 — Fixed Asset Disposal to AR, Tax and Cash

Version 1.0 · Finance-owned reference integration · Resolves the implementation portion of FIN-LIM-0040

## Intent

When TDC sells an asset, the asset register, statutory output tax, customer receivable or immediate receipt, cash/bank destination and GL derecognition must tell one coherent story. FIN-INT-006 makes the Fixed Asset disposal service the orchestrator while reusing the existing AR, tax, payment-allocation and posting services.

This is the reference for future adapters because it demonstrates the central rule: coordinate existing owners; do not recreate their business logic.

## Participants and ownership

| Participant | Owns | Does not own |
|---|---|---|
| Fixed Assets / `IAssetDisposalService` | Approved disposal, asset/book evidence, proceeds, settlement mode and orchestration | AR invoice calculations, receipt allocation internals or direct journal persistence |
| AR / `IInvoiceService` | Buyer invoice, tax calculation, receivable posting and AR reporting evidence | Asset derecognition or disposal approval |
| AR/Cash / `IPaymentService` | Immediate receipt, allocation, cash/bank destination and receipt posting | Disposal gain/loss policy |
| Finance posting engine | Period/lock checks, balanced journals, idempotency, origin metadata and posting audit | Producer workflow approval |

## Preconditions

- The disposal is approved under maker-checker control and the asset/book is eligible for disposal.
- Disposal-date depreciation and any required component/partial-disposal evidence are complete.
- Sale proceeds are positive and a same-tenant buyer is selected.
- Settlement mode is `CreditSale` or `ImmediateReceipt`.
- Standard-rated sales have an active Sales/Both tax group; zero-rated, exempt and out-of-scope treatments do not smuggle a tax group into the request.
- Immediate receipts have an authorised payment method and valid cash/bank destination in the receipt currency.
- The Finance period is open and all disposal, proceeds-clearing, tax, receivable and receipt mappings are valid.

## Processing outcomes

### Credit sale

1. Complete the approved asset derecognition through the Finance posting engine.
2. Create and post a linked AR invoice for the buyer, proceeds and selected tax treatment.
3. Keep the invoice open for normal collection, aging, statement and follow-up processing.
4. Store disposal ↔ invoice ↔ journal/posting references and set settlement status to `Invoiced`.

### Immediate receipt

1. Perform the credit-sale steps so tax and buyer/subledger evidence are identical to a credit sale.
2. Create a linked customer receipt for the gross invoice amount.
3. Allocate and post that receipt to the linked invoice through `IPaymentService`.
4. Store disposal ↔ invoice ↔ payment ↔ journal/posting references and set settlement status to `Settled`.

Creating the invoice first is intentional. It gives taxable cash sales the same statutory tax snapshot and AR audit lineage as credit sales, while the immediate allocation closes the receivable.

## Idempotency and failure behaviour

- Disposal completion and each downstream posting use stable source identities and Finance idempotency protection.
- Existing linked invoice/payment evidence is reused on a safe retry; a second economic sale must not be created.
- The orchestration runs in the disposal completion transaction. A downstream exception leaves the disposal unsettled/failed and prevents a false completed outcome.
- Changed approval-sensitive details invalidate stale approval before posting.
- The original transaction remains traceable; corrections use Finance void/reversal lifecycles rather than editing posted journals.

## Contract evidence

- `FixedAssetDisposalFoundationTests.CreditSaleCreatesLinkedArInvoiceAgainstTheDisposalClearingAccount`
- `FixedAssetDisposalFoundationTests.ImmediateSaleCreatesAndPostsReceiptAllocatedToLinkedInvoice`
- destination/currency rejection tests in `FixedAssetDisposalFoundationTests`
- `FixedAssetDisposalSettlementMigrationTests`
- `FinanceIntegrationContractFoundationTests.DisposalContractShouldExposeExistingFinanceOwnedOrchestration`

Representative taxable credit sales and immediate receipts must still be run in TDC UAT after applying the migration. That is release evidence, not an unimplemented feature.
