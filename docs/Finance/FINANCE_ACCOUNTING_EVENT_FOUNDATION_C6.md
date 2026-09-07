# Finance AccountingEvent Orchestration Foundation (C6)

## Purpose and release boundary

Stage C6 introduces a Finance-owned neutral `AccountingEvent` and an atomic group of exact-book representations. A producer supplies one economic intent. It must not enumerate accounting books or invoke the posting leaf once per book; Finance consumes the frozen C5 selection and owns representation ordering.

The orchestrator is disabled by default through `Finance:AccountingEvents:Enabled`. This stage does not activate producer cutover. Existing V1/V2 callers continue through the public single-book posting contract and remain protected by `PARALLEL_BOOK_POSTING_DISABLED`. Only the internal AccountingEvent leaf authority may execute sibling books, and only while its event and C5 selection are tracked in the caller-owned transaction.

## Canonical event and frozen selection

- Event identity is tenant-scoped and binds canonical originating module, document type and ID, posting action, idempotency key, event kind, version, and correction/reversal lineage.
- The versioned, domain-separated request fingerprint binds all normalized material header, line, dimension, currency, rate, tax, source, and budget-reservation evidence.
- An original event freezes C5 policy evidence inside the release transaction. The event retains the selection evidence ID and fingerprint; each representation retains ordered stable book ID, event version, immutable code snapshot, and authority fingerprint.
- Corrections and reversals use the target event's exact frozen ordered book set. They never resolve current policy, silently omit a book, or substitute a Delta book.
- Same-event/same-book retries return the existing representation only when immutable evidence matches. Changed evidence conflicts.

## Atomic release and failure evidence

The orchestrator opens one serializable, same-database transaction. C5 freezing, all selected full-book leaf postings, journal and Finance posting evidence, C2 balance/exposure projections, budget effects, AccountingEvent outcome, and audit evidence commit together or roll back together. Book results are persisted before the group transitions to `Posted`, so database authority can verify the complete frozen set.

Budget reservations describe the economic event, not each alternative representation. They are consumed once by the selected primary full book, or by the first frozen book if no primary is present, within the same transaction.

A failed release records no partial representation. After rollback, Finance opens a separate serialized transaction and appends durable failed-attempt evidence against the same stable event identity. An unchanged retry may proceed; a different payload, checker, or release reason conflicts. Attempt history is append-only and cannot be presented as a partially posted group.

## Governance and inquiry

Preparation and release use separate permissions and enforce maker/checker separation:

- `Finance.AccountingEvents.Read`
- `Finance.AccountingEvents.Prepare`
- `Finance.AccountingEvents.Orchestrate`

Read APIs expose the deterministic group result and an exact event/book result. They are tenant-scoped and never calculate a cross-book total. Audit records preparation, atomic posting, and post-rollback failure evidence.

## Migration and operations

The C6 migration is unapplied. It creates no events and performs no historical backfill. Its first operation validates the required C1-C5 schema and frozen-selection authority before mutation. Tenant-composite foreign keys, event/version/book uniqueness, canonical checks, immutable-evidence triggers, and a loss-refusing Down guard protect the new authority.

Before any future application, run the guarded SQL Server migration and orchestration tests against an exact disposable `RHEMAERP_GL_REHEARSAL_*` target. Never stamp past preflight, fabricate frozen evidence, or enable the feature until the independent cutover review has approved producer integration and operational recovery.

## Deliberate exclusions

C6 does not modify producer modules, enable automatic parallel posting, select Delta books, treat reporting currency as a book, or aggregate alternative-book balances into `Account.Balance` or `AccountCurrencyLink`. Owner-module state transitions are not introduced in this foundation; any later producer cutover must join the same database transaction through a separately reviewed adapter rather than weakening the atomic boundary.
