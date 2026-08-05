# TDC Cross-Currency Bank Transfer and Reconciliation

**Implemented:** 2026-08-03  
**Migration:** `20260803213749_AddCrossCurrencyBankTransfers`  
**Limitation resolved:** `FIN-LIM-0021`  
**Scope:** Finance-owned bank transfer, posting, reversal, audit, and reconciliation only. No bank API or other module interface is introduced.

## 1. Intent and background

A transfer between bank accounts in different currencies is not one amount copied twice. The source bank loses its own native-currency amount, the destination bank receives a separately confirmed native-currency amount, and each leg has a functional-currency value based on an approved exchange-rate record. A difference between those functional values is a realised foreign-exchange gain or loss.

The existing paired `CashTransaction` transfer and central Finance posting engine remain authoritative. This slice strengthens that design rather than creating a parallel FX-transfer subsystem.

## 2. TDC control defaults

- GHS remains the tenant functional currency unless Finance Settings says otherwise.
- A foreign-currency leg uses an active, approved, effective-dated tenant exchange-rate record. Users cannot type an ungoverned rate into the transfer screen.
- The exchange-rate record id, value, source, effective date, and quote side are snapshotted on each bank leg for audit.
- If directional exchange-rate policy is enabled, each bank GL account's active currency link selects the rate type and quote side.
- Finance must confirm the destination bank's native amount before capture. A preview may calculate an indicative amount, but that derived value is not silently committed.
- Realised gain and realised loss post to their separate accounts configured in Finance Settings. Missing setup blocks posting atomically.
- Access scope is required for both source and destination bank accounts.
- A stable transfer-pair key makes capture safe to retry and prevents duplicate operational or accounting entries.

## 3. Operational flow

1. Finance selects the source bank account, destination bank account, transaction date, source amount, and description.
2. The server checks both account scopes and resolves each account's currency and GL mapping.
3. The preview resolves approved exchange-rate evidence and shows source and destination functional values, the cross rate, and the projected realised gain or loss.
4. For a cross-currency transfer, Finance confirms the destination native amount.
5. Capture creates one outgoing and one incoming `CashTransaction`, joined by `TransferPairId` and distinguished by `TransferLeg`.
6. The normal cash/bank approval workflow continues to apply.
7. Posting sends one balanced request through the central Finance posting engine.
8. Each bank statement line later matches its own native-currency leg independently.
9. If correction is necessary, the existing controlled reversal creates paired compensating legs using each original leg's retained amount, currency, and rate evidence.

## 4. Accounting treatment

For a transfer of USD 100 from a USD bank to a EUR bank where:

- USD 100 has a functional value of GHS 1,520;
- the destination confirms EUR 90 with a functional value of GHS 1,512; and
- the difference is therefore GHS 8,

the journal is:

| Account | Debit (GHS) | Credit (GHS) |
|---|---:|---:|
| Destination EUR bank | 1,512.00 | 0.00 |
| Realised FX loss | 8.00 | 0.00 |
| Source USD bank | 0.00 | 1,520.00 |

If the destination functional value is higher, the difference is credited to the configured realised FX gain account. A zero difference produces no gain/loss line.

Same-currency transfers continue to move the same native amount. When that common currency is foreign, both bank accounts must have aligned transaction-rate policies so the transfer cannot manufacture an artificial FX result.

## 5. Reconciliation and correction

Reconciliation never substitutes the functional value for the bank-statement amount. The outgoing source leg is matched using its source currency and amount; the incoming destination leg is matched using its destination currency and amount. The explicit `TransferLeg` field is authoritative, while the former transaction-number suffix convention is retained only as a development-data fallback.

A posted transfer reversal:

- refuses an incompatible reconciled or downstream-owned item;
- copies each original leg's native amount, currency, and rate snapshot;
- reverses the signed realised FX result;
- uses a new pair id for the compensating transfer; and
- retains original-to-reversal and journal lineage in the existing trace.

## 6. Data and API contract

The `CashTransaction` transfer legs now retain:

- `TransferPairId` and `TransferLeg`;
- `ExchangeRateId`, `ExchangeRate`, `ExchangeRateSource`, `ExchangeRateDate`, and `ExchangeRateQuoteSide`;
- `TransferCrossRate`; and
- signed `TransferFxGainLossBaseAmount`.

The filtered unique index on tenant, pair id, and leg ensures that a pair can contain at most one outgoing and one incoming row. The exchange-rate foreign key and lookup index preserve rate lineage without allowing a used rate record to be deleted.

Endpoints:

- `POST /api/finance/cash-transactions/transfer/preview` resolves the controlled plan without writing records.
- `POST /api/finance/cash-transactions/transfer` captures the confirmed paired transfer.

## 7. Audit and failure behaviour

Cross-currency capture, duplicate retry, successful posting, and blocked posting have dedicated Finance audit events. Their context includes pair id, native amounts, currencies, rate ids and values, functional values, and the realised FX outcome.

The transfer is rejected or posting is blocked when, among other controls:

- either bank account is missing, inactive, outside scope, or lacks a distinct GL account;
- an effective approved rate cannot be resolved for a foreign leg;
- a submitted rate id does not match the server-resolved transfer policy;
- the destination amount is not confirmed;
- the realised gain/loss account is not configured; or
- the fiscal period or normal cash/bank workflow gate prevents posting.

Posting validation happens before bank snapshots are moved. A failed control therefore cannot leave the GL and operational bank balances out of step.

## 8. Verification evidence

- `CashBankTransactionPostingMigrationTests`: preview, confirmed capture, idempotent retry, realised loss, realised gain, missing-mapping atomic failure, native-amount reversal, and same-currency regression.
- `BankReconciliationPostingMigrationTests`: independent matching of source and destination native-currency legs using explicit transfer-leg metadata.
- Frontend TypeScript type check for the preview-led transfer workspace.
- API/Data builds and EF pending-model-change check.
- Migration applied to `RHEMAERP` and schema verified for all eight columns plus the two intended indexes.

## 9. Demonstration narrative

The strongest stakeholder demonstration is to select two differently denominated bank accounts, enter a source amount, and pause on the server preview. Point out that the application has selected approved, dated rate evidence rather than trusting a browser-entered rate; that Finance confirms what the destination bank actually receives; and that the projected gain/loss is visible before capture. After posting, show the two native bank legs, the balanced functional-currency journal, and then explain how each bank statement reconciles to its own amount.

## 10. Honest boundary

This slice resolves the Finance accounting and reconciliation limitation. It does not send a payment instruction to a bank, retrieve a bank's executed conversion amount, import a live statement, or implement AP/AR third-currency settlement. Those are separate interface or settlement work packages; unsupported AP/AR cross-currency settlement remains tracked by `FIN-LIM-0022`.
