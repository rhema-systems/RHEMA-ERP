# TDC AP/AR Cross-Currency Settlement

Date: 2026-08-07

Limitation: `FIN-LIM-0022`

Migrations: `20260806095000_AddApArCrossCurrencySettlement`, `20260806143000_AddLineScopedCrossCurrencyDeductions`

## Stakeholder summary

TDC can now use a payment or receipt in one currency to settle an AP or AR invoice in another currency without pretending that the two native amounts are equal. This supports both common patterns:

- functional-currency cash settling a foreign invoice, such as GHS paying a USD supplier invoice; and
- third-currency cash settling a foreign invoice, such as EUR settling a USD receivable.

The maker records the amount that reduces the invoice and the separate amount consumed from the bank or receipt currency. Discounts and withholding are recorded against the particular invoice in that invoice's currency. Finance freezes both approved exchange-rate snapshots, converts each deduction independently into functional currency, posts through the central Finance engine, calculates realized FX from the invoice's historical carrying value, and retains the evidence through reversal, statutory reporting, and audit views.

Posted supplier and customer advances use the same evidence model. The original payment or receipt is the immutable currency lot: its native amount and approved origin rate establish the advance carrying value. When that lot is later applied, Finance values the invoice at the approved application-date rate and posts the difference as realized FX in the application journal.

### Demo wow factors

- The entry screen visibly distinguishes **Invoice Cash** from **Payment/Receipt Cash**; the system does not conceal a cross-rate inside one ambiguous amount.
- The payment detail shows both native values, making the commercial conversion understandable without reconstructing it from a journal.
- AP/AR control lines preserve the invoice's native clearing quantity while the bank/cash line preserves the actual cash currency.
- Realized gain or loss is derived from immutable functional-value evidence and is posted through the same controlled engine as the settlement.
- Auto-allocation skips cross-currency rows so a user must confirm the commercial amount pair; the server independently rejects missing, negative, or over-settling legs.
- Reversals copy and negate the original currency/rate snapshots, preserving an intelligible correction chain instead of rewriting history.
- AP discounts/WHT and AR discounts/WHT/VAT-WHT retain both invoice-native and independently rounded functional values, so a stakeholder can trace each statutory or accounting line without reverse-engineering a mixed-currency header.
- A foreign advance can be applied partially or across currencies without losing its origin rate; the detail view shows how much of the original currency lot remains.
- Reversing an advance application restores both the invoice and the original currency lot with a linked negative allocation and compensating journal—no posted history is edited.

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
11. If the payment/receipt was posted without an invoice, it becomes a supplier/customer advance currency lot at its approved origin rate.
12. The allocation workspace locks that posted header, accepts the invoice-native and lot-native application amounts, and resolves the approved application-date invoice rate.
13. The application journal releases the advance at historical carrying value, clears AP/AR control at application value, and posts the balancing realized gain or loss.
14. A controlled application reversal copies and negates the two native amounts, both rate snapshots, and both functional values, restoring the lot for later use.

## Controls and intentional boundaries

Cross-currency invoice deductions are now line-scoped. AP supports invoice discount and WHT; AR supports invoice discount, WHT, and VAT-WHT. Header values are server-derived functional-currency roll-ups for reporting and cannot be used as an alternative write path. Statutory reports, AP certificates, remittances, and summaries use the functional snapshots rather than combining payment-currency cash with functional deductions.

Before posting, AP and AR reconstruct the expected functional evidence from each allocation's native amounts and frozen rates. A missing or contradictory cross-currency snapshot fails closed; the service never substitutes a `1.0` foreign rate. The narrowly scoped normalization path applies only to deterministic same-currency draft evidence. AP also re-runs configured WHT policy at posting and rejects a stale header total or an allocation total that no longer agrees with the statutory calculation.

The AP/AR control and deduction accounts must be configured as multi-currency accounts with an effective link for every invoice currency they accept. Supplier/customer advance and bank/liquidity accounts likewise need an effective link for the advance currency. This preserves the posting engine's existing account-currency governance instead of weakening it for subledger convenience.

Foreign supplier/customer advance creation and application are now supported. The full payment or receipt cannot be reversed while an active posted advance application exists; each application must first be reversed so the control balance and currency lot remain reconstructable. Advance application intentionally supports cash-only allocation—discount and withholding adjustments remain in their dedicated workflows.

No additional migration was required for the advance extension. The dual native amounts, dual approved-rate snapshots, functional values, application journal/event links, and reversal lineage introduced by `20260806095000_AddApArCrossCurrencySettlement` already provide the necessary immutable lot evidence. `FIN-LIM-0022` is therefore resolved in code; deployment and client workflow UAT remain release gates.

## Technical ownership notes

- `CrossCurrencySettlementCalculator` is a pure shared calculator used by AP and AR so rounding and functional-value rules cannot drift between subledgers.
- `VendorPaymentService` and `PaymentService` resolve approved rates, validate both native legs, persist snapshots, build settlement journals, and preserve reversal values.
- `CurrencyRevaluationService` uses the invoice currency for historical exposure and the allocation's frozen functional settlement amount for realized FX.
- `VendorPaymentAllocation`, `PaymentAllocation`, and `FxRealizedSettlement` carry the immutable evidence introduced by the migrations.
- `VendorPayment` and `CustomerPayment` are the advance-lot headers; their currency, rate id/value, total, and effective allocation facts determine the remaining native lot balance.
- Posted advance applications use their own posting event rather than changing the original cash journal. Application reversals post against that event and add a linked negative allocation fact.
- Header WHT/VAT-WHT/discount amounts are functional-currency roll-ups; allocation-native amounts remain authoritative for invoice settlement and foreign-currency journal lines.
- Outstanding AP invoice responses now expose invoice currency so the client never assumes that all supplier invoices match the selected bank currency.

## Verification status

- Backend API build: passes with zero errors; repository warnings remain pre-existing.
- Frontend Finance changes: no Finance TypeScript errors; the repository-wide type check is currently blocked by unrelated Inventory and Reports errors on the synced baseline.
- Migration structure: verified by a focused Up/Down regression test covering all seven AP/AR deduction evidence columns.
- Four focused same-currency and foreign-currency advance regressions pass through the real Finance posting engine. They cover USD lots applied to EUR invoices, approved rate evidence, multi-currency account links, realized FX, immutable application reversal, restored lot/invoice balances, and rejection of tax deductions from the cash-only advance path.
- The Finance integration workflow now includes the two foreign-advance contracts and raises its exact consumer count from 26 to 28. The existing SQL Server schema contract remains at four because this extension reuses the already verified allocation-evidence columns and requires no new migration.
- Local database verification: `20260806143000_AddLineScopedCrossCurrencyDeductions` was applied to `RHEMAERP` on 2026-08-07, its migration-history row was confirmed, and all seven AP/AR deduction-evidence columns were queried directly from SQL Server. The older `RhemaERP_UAT_DryRun` database was deliberately left unchanged because it has a large mixed-module migration backlog; updating it would not be an isolated Finance dry run. A current shared UAT deployment and client workflow UAT therefore remain separate release gates.
