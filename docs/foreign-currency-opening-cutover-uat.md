# Foreign-Currency Opening Cutover UAT

## Purpose

This exercise proves that a foreign-currency bank balance, supplier payable, and customer receivable retain their native USD values while the general ledger receives the correct GHS functional values. It also proves that Migration Clearing returns to exactly zero.

This is an additive extension of the completed functional-currency opening exercise. It must not be used to replace or edit the posted GHS opening evidence.

## Release gates

Do not create any USD opening document until all of the following are true:

- the governed foreign-bank opening and foreign AP/AR opening contracts are deployed;
- the opening-invoice exchange-rate migration is applied;
- the authoritative exchange-rate correction is deployed;
- the retained FINANCE-DEMO Daily Mid quote is approved, active, effective on 1 January 2025, and stores `GHS/USD Rate = 12.5` and `Inverse Rate = 0.08`;
- the USD rate has not been used by an earlier transaction with the reversed orientation;
- January 2025 is open for IFRS posting;
- `TDC-DEMO-USD-001`, `TDC-DEMO-SUP-003`, and `TDC-DEMO-CUS-003` remain active and tenant-owned; and
- no USD opening already exists for the selected bank, supplier, or customer.

Correct a retained unused rate only through the audited Exchange Rates screen. Do not update the database directly and do not reseed FINANCE-DEMO.

## Controlled schedule

Use the same approved Daily Mid rate ID for all three sources.

| Source | Native evidence | Functional value | Migration Clearing |
| --- | ---: | ---: | ---: |
| Supplier opening bill — `TDC-DEMO-SUP-003` | USD 70,000 | GHS 875,000 AP credit | GHS 875,000 debit |
| Customer opening invoice — `TDC-DEMO-CUS-003` | USD 20,000 | GHS 250,000 AR debit | GHS 250,000 credit |
| Governed bank opening — `TDC-DEMO-USD-001` | USD 50,000 | GHS 625,000 Bank debit | GHS 625,000 credit |
| **Control total** |  |  | **GHS 875,000 debit = GHS 875,000 credit** |

The operator-entered bank amount is USD 50,000. The governed options response must display the approved rate ID and derive GHS 625,000; the browser must not be trusted to supply the functional amount.

## Execution sequence

1. In Finance → Exchange Rates, verify the exact approved Daily Mid rate and retain its record ID, source, effective date, approval evidence, rate `12.5`, and inverse `0.08`.
2. Create the USD supplier opening bill for `TDC-DEMO-SUP-003`, dated 1 January 2025 and due 31 January 2025, with native total USD 70,000 and a unique approved source-schedule reference.
3. Complete independent AP approval, then use the explicit AP opening post action. Confirm the AP control line carries USD 70,000 and the rate ID while Migration Clearing is functional-currency-only at GHS 875,000.
4. Create the USD customer opening invoice for `TDC-DEMO-CUS-003` on the same dates, with native total USD 20,000 and its own approved source-schedule reference.
5. Complete independent AR approval and canonical posting. Confirm the AR control line carries USD 20,000 and the rate ID while Migration Clearing is functional-currency-only at GHS 250,000.
6. On Finance → Opening Balances, set 1 January 2025, the January 2025 fiscal period, and IFRS. Open Governed Sources and select `TDC-DEMO-USD-001`.
7. Confirm the server returns the bank as eligible with USD currency, the exact approved rate ID, rate `12.5`, and derived GHS 625,000. Enter native amount USD 50,000 and the approved bank schedule reference.
8. Prepare the immutable bank batch, validate it, complete independent approval, and post it once. Confirm the bank snapshot is USD 50,000 while the bank GL debit is GHS 625,000.
9. Reconcile the USD bank against a USD 50,000 statement. Book balance and statement balance must both be USD 50,000 with zero difference.

## Final reconciliation

Do not sign off an intermediate clearing balance. After all three sources are posted, require:

- Migration Clearing: GHS 875,000 debit and GHS 875,000 credit, closing zero;
- USD bank: native USD 50,000 and functional GHS 625,000;
- USD AR: native USD 20,000 and functional GHS 250,000;
- USD AP: native USD 70,000 and functional GHS 875,000;
- Trial Balance: GHS 2,355,000 total debits and credits when added to the existing GHS opening population;
- Balance Sheet: GHS 2,295,000 assets equal GHS 1,115,000 liabilities plus GHS 1,180,000 equity;
- Cash Flow: GHS 1,375,000 beginning and ending cash when no later cash movement exists; and
- Income Statement: zero, because opening sources must not create current-period revenue or expense.

## Stop-lines

Stop without posting if the rate is `0.08`, the inverse is `12.5`, the selected rate ID changes, any source uses rate `1`, the bank options response lacks approved rate evidence, a browser-supplied functional amount differs from the server result, a control line loses its native amount, the clearing line carries USD, the bank snapshot is stored as GHS 625,000, or Migration Clearing does not close to exactly zero.
