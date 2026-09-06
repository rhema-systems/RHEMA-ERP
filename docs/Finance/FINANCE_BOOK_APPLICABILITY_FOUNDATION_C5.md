# Finance Accounting-Book Applicability Foundation (C5)

## Purpose and boundary

Stage C5 makes accounting-book selection a Finance-owned, governed decision. Producers provide only canonical originating-module, source-document-type, posting-action, and event-date evidence. They do not enumerate books. C5 resolves and can freeze a selection, but it does not create an AccountingEvent, journal, posting representation, balance, or exposure.

The C1 `PARALLEL_BOOK_POSTING_DISABLED` control remains authoritative. A frozen selection containing more than one full book is evidence for the later C6 orchestration stage, not permission to call the current leaf posting engine more than once.

## Deterministic resolution

- Approved rules match the exact normalized source triple and effective date.
- Equal highest-priority matches are rejected as ambiguous.
- An explicit rule may select only same-tenant `PrimaryFull` and `ParallelFull` books by stable ID. Its ordered code values are immutable snapshots, not selection keys.
- `Delta` books and pseudo selectors are never eligible for ordinary automatic applicability.
- If no approved rule matches, Finance selects exactly one primary/default full book. It never expands the fallback to all active books.
- Every selected book is validated without silently dropping failures: canonical identity, tenant lineage, active/postable lifecycle, effective date, current approved initialization and reconciliation evidence, enabled classified mappings, and the exact globally/module/book-open period for the event date.

Resolution returns explicit blockers and versioned, domain-separated hashes. Freezing requires the preview hashes, reruns the resolution inside the governed transaction, and stores immutable policy/rule/version, normalized input, ordered stable book IDs/code snapshots, readiness authority, actor, timestamp, and fingerprint evidence. Reusing an idempotency key is valid only for identical frozen evidence.

## Governance

Policy versions progress through Draft, PendingApproval, Approved, Rejected, and Retired states. Approval and retirement use independent maker-checker workflow decisions, reasons, rowversion checks, serializable tenant-scoped validation, and atomic Finance audit writes. Approved structure and any structure referenced by frozen selection evidence are immutable. There is no physical-delete or write-on-GET path.

Permissions are deliberately separate from general book/report access:

- `Finance.AccountingBooks.ApplicabilityPolicy.Read`
- `Finance.AccountingBooks.ApplicabilityPolicy.Manage`
- `Finance.AccountingBooks.ApplicabilityPolicy.Approve`
- `Finance.AccountingBooks.Applicability.Resolve`

## Migration and operations

The C5 forward migration is intentionally unapplied. It creates versioned policy/rule/selected-book/frozen-evidence authority with tenant-consistent relationships, canonical and immutable-evidence checks, and SQL Server overlap/write-protection guards. It creates no automatic parallel rules and fabricates no historical use evidence. Existing tenants therefore continue to resolve through the primary-only runtime fallback until reviewed policy versions are approved.

Before applying the migration to retained development data, run its fail-before-mutation preflight and the guarded SQL Server migration suite. Do not stamp past a blocker or seed parallel applicability by display name. Downgrade is allowed only when no C5 policy or selection evidence would be discarded.

## Future-owner constraints

Producer owners must keep sending stable source evidence and one economic intent; they must not reproduce this policy logic. C6 may consume the immutable selection evidence to orchestrate required representations atomically. It must retain stable IDs, exact code snapshots, idempotency, readiness fingerprints, and the C1 leaf-executor guard until book-aware orchestration and recovery are complete.
