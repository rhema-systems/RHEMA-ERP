# Finance multi-book identity foundation — Stage C1

Stage C1 establishes stable accounting-book identity without enabling parallel-book execution.

## Persisted authority

`JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` now retain both:

- `AccountingBookId`, the stable tenant-qualified relational identity; and
- `BookClassification`, the immutable accounting-book code snapshot used for audit and display.

The central V2 boundary resolves one concrete active tenant-owned book and propagates the same ID/code
pair to the header, every line, and the posting event. Composite foreign keys prevent a transaction or
event from being linked to a journal in another tenant or book. Runtime checks fail closed when retained
evidence disagrees before it is returned as a retry or used for an exact reversal.

Source/action uniqueness is scoped by tenant and `AccountingBookId`. Idempotency uniqueness is likewise
tenant + `AccountingBookId` + key. A same-book retry must reproduce the immutable request evidence; a
changed amount, account, line identity, currency/rate, dimension, date, or header payload is a conflict,
not a successful retry.

## Transitional parallel-book gate

The relational model can distinguish multiple book representations, but the current leaf posting path
cannot safely create them because `Account.Balance` and `AccountCurrencyLink` remain non-book-scoped.
Therefore, if the same source/action or idempotency identity already exists in another book, posting fails
with `PARALLEL_BOOK_POSTING_DISABLED`. SQL Server transaction-owned application locks close the concurrent
cross-book race. There is no bypass, book enumeration, or hidden orchestration path in C1.

Finance must not remove this gate until book-aware balances/exposures and the Finance-owned AccountingEvent
orchestrator have been independently approved and integrated.

## Forward migration contract

`20260905151918_AddStablePostingAccountingBookIdentity` remains unapplied. Its first operation is a
fail-before-mutation SQL preflight. Every retained header, transaction, and event code must resolve to
exactly one nondeleted AccountingBook in the same tenant; pseudo/blank/unknown codes and header-line-event
tenant or code disagreements stop the migration. Only then are nullable IDs added, deterministically
backfilled without changing code snapshots, made required, indexed, and constrained.

The bounded Down path first rejects any database containing book-qualified identities that the predecessor's
tenant-global uniqueness would conflate. Existing developer databases must never stamp past the preflight or
manually populate IDs. Resolve the diagnostic or use the separately approved clean reset/reseed process.

## Deferred work

C1 does not introduce AccountingEvent, applicability rules, book enumeration, automatic parallel posting,
book lifecycle/type/period readiness, or book-aware balance and FX exposure storage. Reports continue to
select one explicit book and must not sum alternative full books automatically.
