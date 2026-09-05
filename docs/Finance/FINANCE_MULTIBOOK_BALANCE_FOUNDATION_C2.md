# Finance Multi-Book Balance Foundation — Stage C2

## Authority and grains

Posted, non-deleted `AccountTransaction` rows remain the accounting source of truth. C2 adds two rebuildable Finance read models; neither may manufacture accounting evidence:

- `AccountBalance`: one functional-currency period balance per tenant, account, accounting book, fiscal period, and functional currency. Its internal coordinate is always debit minus credit.
- `AccountCurrencyExposure`: one transaction-currency exposure per tenant, account, accounting book, and foreign transaction currency. It stores signed foreign and signed functional amounts. Base-currency lines never create exposure rows.

`AccountCurrencyLink` remains configuration. Its legacy balance columns are no longer advanced by central posting. `Account.Balance` is retained temporarily as a primary-book compatibility value only: exactly one active default posting book must be authoritative, and a non-default-book posting never changes it.

## Posting and parallel-book safety

The central V2 posting transaction updates the journal/event, budget effects, exact-book balance, exact-book exposure, and primary compatibility value together. Same-book retry therefore returns existing posting evidence without applying a second projection delta. Exact reversal posts signed opposite lines to the same relational book identity.

C1's tenant-scoped representation lock and `PARALLEL_BOOK_POSTING_DISABLED` gate remain mandatory. C2 does not enumerate books, introduce an orchestrator, or allow a second representation of the same economic source. One thousand posted independently to IFRS and Local is one thousand in each book; no exact-book API combines them, and `Account.Balance` remains the primary value only.

## Exact-book inquiry

`GET /api/finance/book-balances/accounts/{accountId}?accountingBookCode=...&fiscalPeriodId=...` requires `Finance.Read`. The stable book code is mandatory and must resolve to exactly one active, posting-enabled, same-tenant book for which the account has an enabled mapping. Unknown, ambiguous, cross-tenant, or unmapped scope fails closed.

The old general-ledger balance method is obsolete. During transition it resolves only the single active default posting book and only for functional currency; callers needing foreign or parallel-book values must use the exact-book contract.

## Reconciliation and rebuild

`IBookBalanceReadModelService.ReconcileAsync` recomputes one explicit tenant/book scope from posted transactions. A dry run reports row and amount drift without mutation. Applying a rebuild requires a reason, idempotency key, and a different approving user. It uses the same tenant SQL application lock as posting, replaces only that book's projections in one transaction, and records source fingerprint, actor/approver, drift, row counts, and completion evidence. A repeated identical idempotency key is a no-op; changed source evidence fails closed.

No public rebuild endpoint is exposed in C2. A future administrative command must retain the existing maker-checker and specific Finance permission conventions rather than exposing this service as an ordinary write API.

## Migration and reset readiness

`AddBookAwareBalanceFoundation` remains unapplied. Before any schema or data mutation it rejects dormant `AccountBalances` evidence with blank/pseudo/unknown/ambiguous/cross-tenant book identity, invalid functional currency, wrong-tenant account or fiscal period, or a duplicate authoritative grain. Stable book backfill uses byte-exact `Latin1_General_100_BIN2` plus length equality and preserves the book-code snapshot.

The later approved reset/reseed must apply this migration through the guarded rehearsal path. Operators must run the migration operation/discovery and EF no-pending-model gates; they must not manually populate projection rows or use `AccountCurrencyLink` balances as migration authority.
