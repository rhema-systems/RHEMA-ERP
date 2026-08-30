# Foreign-Currency Governed Bank Opening PR Summary

## Summary

This change extends the Finance-owned governed bank-opening workflow to foreign-currency bank accounts without weakening the free-form opening-balance controls.

The operator enters the opening amount in the bank account's native currency. The server resolves and freezes an approved, effective exchange-rate record for the selected opening date, derives the functional-currency value, and posts both values through the central Finance posting engine.

## Accounting Contract

- The bank line stores and posts the native debit, functional debit, exchange-rate ID, and exchange-rate date.
- The migration-clearing line remains functional-currency-only.
- `BankAccount.CurrentBalance` and `AvailableBalance` remain native-currency snapshots.
- The posted GL remains the functional-currency accounting source of truth.
- Bank reconciliation consumes native transaction evidence for a foreign bank and fails closed when that evidence is absent.
- Migration sign-off compares a foreign bank's native snapshot with native posted transaction evidence rather than a functional GL amount.
- Cash/bank ledger reporting keeps its functional movement totals but does not present a false snapshot variance between unlike currencies.

## Controls

- Foreign bank creation requires the exact approved rate selected by the dated governed-options response.
- Rate type, quote side, currency pair, effective date, approval status, and account-link policy are revalidated at create, submit, and post.
- Mapping or rate drift invalidates the governed evidence.
- Generic/free-form foreign-currency opening lines remain rejected.
- Posting and retries retain the existing maker-checker, idempotency, posting-event, and immutable-backlink controls.

## UI

The Governed Sources panel displays:

- the entered native bank amount;
- the derived functional amount;
- the approved rate, type, quote side, source, and effective date; and
- native and functional currency codes without a hard-coded currency symbol.

## Scope Boundary

This PR covers governed bank-account openings only. It does not claim completion of foreign-currency AP/AR opening-invoice provenance, foreign statutory withholding openings, cross-currency settlement, or a FINANCE-DEMO foreign-bank fixture. Those require their own source-document and representative-data contracts.

No database migration or production data mutation is required by this change.

## Verification

- Foreign USD bank opening: USD 50,000 at approved GHS/USD rate 12.5 posts GHS 625,000 while retaining the USD 50,000 bank snapshot.
- Missing rate evidence is rejected before persistence.
- Migration sign-off reconciles the native bank snapshot to native posted evidence.
- Existing functional-currency governed bank, reconciliation, ledger, export, and posting regressions remain green.
