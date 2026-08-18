# Finance Integration Contract Catalogue

This catalogue tells module owners which Finance boundaries are callable now, which are planned, and which require a TDC ownership decision. The machine-checkable source is `FinanceIntegrationContractCatalog`; this page adds the operational context needed for design reviews.

## Status meanings

- **Available**: callable code and Finance-owned regression evidence exist. It is ready for development/UAT consumption, not automatically approved for production cutover.
- **Planned**: the business need is known, but producer and Finance owners must agree the source event and adapter before it becomes callable.
- **Decision required**: shared master-data ownership or policy must be agreed before code is safe.
- **Requirements clarification**: the supplied requirement does not define enough behaviour to design an interface.

## Catalogue

| ID | Contract | Producer → Finance owner | Status | Version | Entry point / proposed boundary | Requirement or limitation |
|---|---|---|---|---|---|---|
| FIN-INT-001 | Approved source transaction → GL | Calling module → Finance GL | Available | 1.0 | `IFinancePostingEngine.PostAsync` | Common control foundation |
| FIN-INT-002 | Accepted procurement stock receipt → inventory and GRV accrual | Procurement/Inventory → Finance GL | Available | 1.0 | `IInventoryReceiptFinancePostingService.PostAcceptedReceiptAsync` | Procurement/Finance reconciliation |
| FIN-INT-003 | Inventory adjustment → GL | Inventory → Finance GL | Available | 1.0 | `IInventoryAdjustmentFinancePostingService.PostAsync` | Inventory adjustment accounting |
| FIN-INT-004 | Inventory landed-cost valuation → GL | Inventory → Finance GL | Available | 1.0 | `IInventoryLandedCostFinancePostingService.PostLandedCostAsync` | Inventory valuation accounting |
| FIN-INT-005 | Sales return credit note → AR and GL | Sales → Finance AR | Available | 1.0 | `IReturnOrderService.PostCreditNoteAsync` | Sales return accounting |
| FIN-INT-006 | Fixed-asset sale → AR, tax and cash | Finance Fixed Assets → Finance AR/Tax/Cash | Available | 1.0 | `IAssetDisposalService.CompleteDisposalAsync` | FIN-LIM-0040 |
| FIN-INT-007 | Accepted procured asset → capitalization | Procurement → Finance Fixed Assets | Available | 1.0 | `IProcurementFixedAssetCapitalizationAdapter` | FIN-LIM-0028 |
| FIN-INT-008 | Sales billing → AR invoice | Sales → Finance AR | Planned | 0.1 | Joint adapter using `IInvoiceService` | FIN-LIM-0051 |
| FIN-INT-009 | Shared payment-method ownership | Sales/Procurement/etc. → Finance Cash | Decision required | 0.1 | Shared reference contract | FIN-LIM-0050 |
| FIN-INT-010 | Different customer and supplier payment terms | Sales/Procurement → Finance AR/AP | Decision required | 0.1 | Shared partner/payment-term contract | FIN-LIM-0053 |
| FIN-INT-011 | SH Fund, PF, ESB and fuel allocation | Owner not defined → Finance | Requirements clarification | 0.0 | Not yet designable | SRS-INT-004 |

## Contract-wide rules

1. The producer owns its source document, business approval, quantity/service evidence and stable source reference.
2. Finance owns account policy, fiscal-period validation, functional-currency balance, tax/subledger effects, journal creation, posting audit and reversal rules.
3. Each attempt carries the tenant ID, source ID, source type, human reference, origin module and a deterministic idempotency key.
4. The adapter saves the returned posting and journal references on the source document. A retry returns the original outcome rather than creating a second accounting event.
5. A customer or supplier balance is never represented by GL lines alone; the applicable AR/AP source and settlement records must exist.
6. Breaking request or outcome changes require a new major contract version and coordinated consumer migration. Additive evidence can use a minor version.

Use the [adapter checklist](finance-integration-adapter-checklist.md) before implementation and the [consumer-test template](finance-integration-consumer-test-template.md) before requesting review. FIN-INT-006 is documented as the first full [reference contract](fixed-asset-disposal-ar-tax-cash-contract.md).
