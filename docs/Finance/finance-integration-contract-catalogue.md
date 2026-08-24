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
| FIN-INT-001 | Approved source transaction → GL | Calling module → Finance GL | Available | 1.1 | `IFinancePostingEngine.PostAsync` | Optional structured transaction dimensions are additive; mandatory enforcement requires adapter certification |
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
| FIN-INT-012 | Post-acceptance supplier return dispatch and valuation handoff | Procurement/Inventory → Finance AP/GRV/GL | Planned | 0.1 | `SupplierReturnFinanceAdapter.ConsumeDispatchAsync` (fail-closed) | Finance envelope validation is implemented; authoritative producer evidence and posting orchestration remain pending |
| FIN-INT-013 | Supplier return commercial resolution → AP, tax and settlement | Procurement → Finance AP/Tax/Cash | Planned | 0.1 | `SupplierReturnFinanceAdapter.ConsumeCommercialResolutionAsync` (fail-closed) | Finance envelope validation is implemented; producer lifecycle, durable correlation and AP/tax settlement remain pending |
| FIN-INT-015 | Approved Procurement demand → Finance budget commitment | Procurement → Finance Budget | Available | 1.0 | `IFinanceBudgetCommitmentService` | Finance provider is callable and tested; the Procurement lifecycle adapter remains Procurement-owned and pending |

## Contract-wide rules

1. The producer owns its source document, business approval, quantity/service evidence and stable source reference.
2. Finance owns account policy, fiscal-period validation, functional-currency balance, tax/subledger effects, journal creation, posting audit and reversal rules.
3. Each attempt carries the tenant ID, source ID, source type, human reference, origin module and a deterministic idempotency key.
4. The adapter saves the returned posting and journal references on the source document. A retry returns the original outcome rather than creating a second accounting event.
5. A customer or supplier balance is never represented by GL lines alone; the applicable AR/AP source and settlement records must exist.
6. Breaking request or outcome changes require a new major contract version and coordinated consumer migration. Additive evidence can use a minor version.
7. Producers provide operational dimension facts through `FinancePostingLineDto.Dimensions`; Finance resolves and owns the immutable set. Producers must not select stored dimension-set IDs or write dimension tables.

## Agreed post-acceptance Return-to-Vendor boundary

FIN-INT-012 and FIN-INT-013 cover goods that passed receipt inspection and were accepted, but are subsequently returned under a supplier claim such as a latent defect, warranty failure, recall, excess supply or another supplier-authorised reason. They do not replace either of these existing operational paths:

- goods rejected before acceptance remain in Procurement's governed receipt-inspection process and never enter accepted stock;
- internally damaged, obsolete, lost or expired stock with no supplier claim remains an Inventory adjustment or write-off.

### Ownership

| Capability | Authoritative owner |
|---|---|
| Return request, reason, supplier claim, PO/GRN lineage, approval, RMA/authorisation and dispatch evidence | Procurement |
| Warehouse/location, item/UOM, lot/serial quantities, physical outbound movement and carrying-cost/valuation layers | Inventory |
| AP or GRV treatment, supplier debit/credit application, tax, refund/cash, account policy, fiscal periods, journals, audit, reversal and idempotency | Finance |

The agreed boundary does **not** authorise direct cross-module table writes. Procurement and Inventory must publish immutable, tenant-scoped contract DTOs through the agreed adapter. Finance validates and persists its own subledger, tax, posting and audit records through Finance-owned services. Finance must not change Procurement return states or Inventory quantity/cost records.

### FIN-INT-012 — physical dispatch and valuation

This event occurs only after Procurement has approved the Return-to-Vendor dispatch and Inventory has posted the authoritative outbound movement. It does not assert that the supplier has issued or accepted a commercial credit.

The proposed request must carry at least:

- tenant, return and dispatch IDs, human reference, approval evidence and dispatch date;
- supplier, PO, GRN and optional supplier-invoice lineage;
- item, UOM, quantity, warehouse/location and applicable lot/serial evidence;
- Inventory movement and valuation-layer references, actual carrying cost and currency evidence;
- deterministic idempotency key, correlation ID, contract version and correction/reversal lineage.

Finance will determine the valid AP/GRV, return-clearing and GL treatment from Finance configuration. The producer must not manufacture a balanced journal or supply Finance account IDs as its accounting result.

### FIN-INT-013 — supplier commercial resolution

This later event records the supplier's authoritative commercial outcome: credit note, cash refund, replacement, repair/warranty resolution or an agreed future credit. It is separate because dispatch and commercial acceptance can occur on different dates and in different fiscal periods.

The proposed request must carry at least:

- tenant and resolution IDs plus the related return/dispatch reference from FIN-INT-012;
- resolution type, supplier reference, acceptance/effective date and supporting evidence;
- original supplier-invoice lineage where applicable;
- commercial, discount and tax values with transaction/functional currency and exchange-rate evidence;
- deterministic idempotency key, correlation ID, contract version and correction/reversal lineage.

Finance owns AP application, GRV or return-clearing settlement, input-tax correction, refund/cash treatment and GL posting. A balanced control-account journal without AP aging/application and tax evidence is not a complete FIN-INT-013 outcome.

Both entries remain **Planned**. Agreement on ownership is necessary but is not implementation evidence. Each may become **Available** only after a callable Finance consumer exists and the Procurement/Inventory producer consumer-contract tests, retry/idempotency tests, failure-state tests and Finance subledger/reconciliation tests pass.

## FIN-INT-015 — Procurement commitment to Finance budget control

Finance is the sole owner of adopted budget cells, functional-currency availability,
reservations and GL-derived actuals. Procurement owns requisitions, purchase orders,
receipts, cancellations, returns, approvals and their segregation-of-duties rules.
Procurement must call `IFinanceBudgetCommitmentService`; it must not write Finance budget,
reservation, operation or journal tables.

The available Finance provider supports:

- listing eligible adopted expense-budget cells for a date and optional account/segment;
- retrieving `approved - posted actual - active reservations` for an exact budget entry;
- evaluating and reserving one or more producer lines, aggregated by Finance budget entry;
- changing an active reservation to an absolute target amount with optimistic version evidence;
- releasing an active reservation with a reason;
- reducing or consuming a commitment only after the Finance adapter validates authoritative
  source lineage and the exact posted source type/ID/action, event and journal exist.

Every mutation requires a stable source document ID/type/reference/version, stable source-line
IDs, a budget date, an idempotency key and a correlation ID. Tenant and actor identity are
derived from the authenticated Finance context and are not accepted as producer authority.
Foreign-currency amounts require the exact approved Finance exchange-rate record; Finance
stores transaction and functional values as evidence.

The initial Procurement consumer must implement this lifecycle without double counting:

1. Draft requisition: availability check only.
2. Approved requisition: reserve once.
3. Amended requisition: set the absolute desired reservation; do not apply a blind delta.
4. Rejected/cancelled requisition: release the remaining reservation.
5. Purchase order: inherit the requisition commitment; do not reserve it again.
6. Accepted receipt: after its Finance posting succeeds, reduce the remaining commitment by
   the receipted portion. The posted expense/inventory/GRNI journal is the actual evidence.
7. Supplier invoice: post through AP and clear GRNI as applicable; do not create a second
   budget actual or reservation.
8. Cancellation, return or reversal: post governed compensating Finance evidence and adjust
   or release only the remaining commitment implied by the authoritative source lifecycle.

`ApplyPostingOutcomeAsync` never writes an actual amount. Actuals are derived exclusively from
posted `AccountTransaction` rows on the exact budget account and fiscal period. This prevents
receipt and supplier-invoice stages from counting the same expenditure twice.

See the detailed [Procurement budget commitment contract](procurement-finance-budget-commitment-contract.md).

Use the [adapter checklist](finance-integration-adapter-checklist.md) before implementation and the [consumer-test template](finance-integration-consumer-test-template.md) before requesting review. FIN-INT-006 is documented as the first full [reference contract](fixed-asset-disposal-ar-tax-cash-contract.md).
