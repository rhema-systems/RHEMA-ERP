# TDC AP/AR Cross-Currency Settlement

Date: 2026-08-06

Limitation: `FIN-LIM-0022`

Migrations: `20260806095000_AddApArCrossCurrencySettlement`, `20260806143000_AddLineScopedCrossCurrencyDeductions`

## Stakeholder summary

TDC can now use a payment or receipt in one currency to settle an AP or AR invoice in another currency without pretending that the two native amounts are equal. This supports both common patterns:

- functional-currency cash settling a foreign invoice, such as GHS paying a USD supplier invoice; and
- third-currency cash settling a foreign invoice, such as EUR settling a USD receivable.

The maker records the amount that reduces the invoice and the separate amount consumed from the bank or receipt currency. Discounts and withholding are recorded against the particular invoice in that invoice's currency. Finance freezes both approved exchange-rate snapshots, converts each deduction independently into functional currency, posts through the central Finance engine, calculates realized FX from the invoice's historical carrying value, and retains the evidence through reversal, statutory reporting, and audit views.

### Demo wow factors

- The entry screen visibly distinguishes **Invoice Cash** from **Payment/Receipt Cash**; the system does not conceal a cross-rate inside one ambiguous amount.
- The payment detail shows both native values, making the commercial conversion understandable without reconstructing it from a journal.
- AP/AR control lines preserve the invoice's native clearing quantity while the bank/cash line preserves the actual cash currency.
- Realized gain or loss is derived from immutable functional-value evidence and is posted through the same controlled engine as the settlement.
- Auto-allocation skips cross-currency rows so a user must confirm the commercial amount pair; the server independently rejects missing, negative, or over-settling legs.
- Reversals copy and negate the original currency/rate snapshots, preserving an intelligible correction chain instead of rewriting history.
- AP discounts/WHT and AR discounts/WHT/VAT-WHT retain both invoice-native and independently rounded functional values, so a stakeholder can trace each statutory or accounting line without reverse-engineering a mixed-currency header.

## Why the model changed

The previous model used `AllocatedAmount` for two incompatible meanings: the amount removed from the invoice balance and the amount consumed from the payment. That is valid only when both documents share a currency. Cross-currency support therefore extends the existing allocation rather than creating a parallel settlement subsystem.

`AllocatedAmount` remains the invoice-currency reduction because aging and invoice balances already use that meaning. `PaymentCurrencyAmount` is the cash consumed from the payment or receipt. Each allocation also freezes:

- invoice and payment currency codes;
- invoice-settlement and payment exchange-rate ids and values;
- payment functional amount;
- total settlement functional amount; and
- separate functional discount, WHT, and VAT-WHT values; and
- whether the allocation is cross-currency.

The payment header stores its approved `ExchangeRateId` alongside the existing frozen rate value. A realized-FX event duplicates the payment-side evidence so the accounting event remains self-contained even if operational projections are rebuilt.

## Operational flow

1. The maker selects the supplier/customer, bank or liquidity account, payment date, and amount.
2. The selected account establishes the payment/receipt currency.
3. For a same-currency invoice, the established single-amount behavior remains available.
4. For a different-currency invoice, the maker enters both the invoice-currency cash reduction and payment-currency cash consumption.
5. Any discount, WHT, or VAT-WHT is entered on that invoice row, never as an ambiguous receipt/payment-header amount.
6. The API resolves tenant-owned, active, approved daily rates for the settlement date and freezes their ids and values.
7. Each deduction is converted and rounded independently because it posts to a different account and must reconcile to its own evidence.
8. The central posting engine records the bank/cash leg in payment currency, the deductions in invoice currency with exact functional overrides, and the AP/AR control leg in invoice currency with the actual functional settlement value.
9. The FX service compares that settlement value with the invoice's historical carrying amount and posts realized gain/loss where required.
10. Detail, statutory reporting, audit, FX, and reversal paths retain the same allocation evidence.

## Controls and intentional boundaries

Cross-currency invoice deductions are now line-scoped. AP supports invoice discount and WHT; AR supports invoice discount, WHT, and VAT-WHT. Header values are server-derived functional-currency roll-ups for reporting and cannot be used as an alternative write path. Statutory reports, AP certificates, remittances, and summaries use the functional snapshots rather than combining payment-currency cash with functional deductions.

Supplier and customer advances also remain functional-currency only. Advance application clears a previously posted advance control balance; that balance needs currency-lot evidence before foreign or third-currency application can be correct.

The advance boundary keeps `FIN-LIM-0022` partially resolved until currency-lotted supplier and customer advance work is implemented.

## Technical ownership notes

- `CrossCurrencySettlementCalculator` is a pure shared calculator used by AP and AR so rounding and functional-value rules cannot drift between subledgers.
- `VendorPaymentService` and `PaymentService` resolve approved rates, validate both native legs, persist snapshots, build settlement journals, and preserve reversal values.
- `CurrencyRevaluationService` uses the invoice currency for historical exposure and the allocation's frozen functional settlement amount for realized FX.
- `VendorPaymentAllocation`, `PaymentAllocation`, and `FxRealizedSettlement` carry the immutable evidence introduced by the migrations.
- Header WHT/VAT-WHT/discount amounts are functional-currency roll-ups; allocation-native amounts remain authoritative for invoice settlement and foreign-currency journal lines.
- Outstanding AP invoice responses now expose invoice currency so the client never assumes that all supplier invoices match the selected bank currency.

## Verification status

- Backend API build: passes with zero errors; repository warnings remain pre-existing.
- Frontend Finance changes: no Finance TypeScript errors; the repository-wide type check is currently blocked by unrelated Inventory and Reports errors on the synced baseline.
- Migration discovery and generated SQL: verified; the deduction migration contains only the seven intended allocation evidence columns and the migration-history insert.
- Focused calculator and FX scenarios are included in the API test project. Execution is currently blocked by unrelated inherited test-project compilation failures and must be rerun when that baseline is repaired.
- Database application and client UAT are separate deployment gates and have not been performed by this implementation slice.
