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

New posting events also persist a domain-separated `FINPOST-REQUEST-V1` SHA-256 fingerprint over the
normalized producer request, including origin/source identity, book, idempotency and reversal lineage,
ordered line/audit/currency/dimension evidence, and sorted budget-reservation identity. Historical events
cannot be assigned a truthful fingerprint from the predecessor schema; their fingerprint remains null and
an attempted retry fails with `LEGACY_POSTING_RETRY_UNAVAILABLE` for explicit reconciliation.

## Transitional parallel-book gate

The relational model can distinguish multiple book representations, but the current leaf posting path
cannot safely create them because `Account.Balance` and `AccountCurrencyLink` remain non-book-scoped.
Therefore, if the same source/action or idempotency identity already exists in another book, posting fails
with `PARALLEL_BOOK_POSTING_DISABLED`. SQL Server transaction-owned application locks close the concurrent
cross-book race. During C1 the lock is deliberately tenant-wide: it is coarser than case-insensitive SQL
equality and therefore prevents case-variant source/action/idempotency values from entering different book
representations concurrently. There is no bypass, book enumeration, or hidden orchestration path in C1.

Finance must not remove this gate until book-aware balances/exposures and the Finance-owned AccountingEvent
orchestrator have been independently approved and integrated.

## Forward migration contract

`20260905151918_AddStablePostingAccountingBookIdentity` remains unapplied. Its first operation is a
fail-before-mutation SQL preflight. Every retained header, transaction, and event code must resolve to
exactly one nondeleted AccountingBook in the same tenant; pseudo/blank/unknown codes and header-line-event
tenant or code disagreements stop the migration. Code resolution and lineage use SQL Server BIN2 collation
plus byte-length equality so lowercase and trailing-space snapshots cannot be accepted as canonical.
Only then are nullable IDs added, deterministically
backfilled without changing code snapshots, made required, indexed, and constrained.

The bounded Down path first rejects any database containing book-qualified identities that the predecessor's
tenant-global uniqueness would conflate. Existing developer databases must never stamp past the preflight or
manually populate IDs. Resolve the diagnostic or use the separately approved clean reset/reseed process.

## Deferred work

C1 does not introduce AccountingEvent, applicability rules, book enumeration, automatic parallel posting,
book lifecycle/type/period readiness, or book-aware balance and FX exposure storage. Reports continue to
select one explicit book and must not sum alternative full books automatically.
