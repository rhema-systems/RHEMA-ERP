# Finance Accounting Book Lifecycle — C3

## Boundary

C3 makes an accounting book governed tenant master data. It does not create a
multi-book posting orchestrator. The V2 Finance posting engine remains a
single-book leaf executor, and `PARALLEL_BOOK_POSTING_DISABLED` remains the
required response when the same economic representation is attempted in a
second book.

`Account.Balance` remains a compatibility value for the one primary/default
book. Exact-book balances and currency exposure remain authoritative through
the C2 read models.

## Structure

Every live book has:

- a canonical stable code and stable relational ID;
- a structural type: `PrimaryFull`, `ParallelFull`, or `Delta`;
- a separately configured accounting purpose or principle;
- a lifecycle status: `Draft`, `Configuring`, `Initializing`, `Active`,
  `Suspended`, or `Retired`;
- effective dates and optimistic-concurrency evidence;
- functional-currency authority for full books; and
- a same-tenant base-book relationship only when it is a Delta book.

There must be exactly one tenant primary/default full book. Full books have no
base. A Delta book must reference another governed book in the same tenant and
the graph must be acyclic. Reporting currency is not an accounting book.

## Lifecycle and approval

Reading accounting books never seeds or repairs configuration. Book creation
starts non-posting in `Draft`; structural editing moves setup through
`Configuring`. Once initialization has started or accounting evidence exists,
the stable code, type, purpose, functional currency, base relationship and
effective structure are immutable.

Material transitions require a reason and a separate checker. Activation and
retirement are never direct edits. C3 records a pending request and its frozen
target/reason/maker evidence; a different authorised user must approve or
reject it. Mutation and Finance audit evidence are committed in one serializable
transaction, and stale row versions fail closed.

C3 intentionally rejects every transition to `Active`: C4 must first provide
book initialization and book-period readiness. Existing development books that
were already active and postable may be retained by the forward migration as
grandfathered evidence; this does not authorize a new activation or parallel
posting path.

Suspension stops new posting without erasing historical evidence. Retirement is
terminal and physical deletion is not exposed.

## Migration and deterministic seed

The C3 forward migration is unapplied. Its first operation is a fail-before-
mutation preflight. It requires canonical codes/currencies, one unambiguous
primary/default authority, tenant-consistent book evidence, and a valid base
graph. Stable C1 book IDs and immutable code snapshots are retained.

The Finance classification manifest still recognizes `IFRS`,
`LOCAL_STATUTORY`, and `MANAGEMENT` as deterministic seed instance codes, not
as semantic book types. On an empty C3 database it creates the primary and
parallel instances in non-posting `Configuring` state; C4 readiness is required
before activation. Repeated seeding preserves administrator-managed instances.

## Operating guidance

1. Resolve and review the one primary/default full book.
2. Configure purpose, currency, dates, and any Delta base relationship while
   the book is unused.
3. Resolve every lifecycle warning and invalid mapping.
4. Do not attempt activation until C4 initialization and period readiness are
   installed.
5. Never restore the removed opportunistic IFRS creation in account, Fixed
   Asset, depreciation, or valuation code. Missing governed setup is an
   actionable configuration error.

The migration must be reviewed with its operation/preflight tests and EF
no-pending-model gate before it is applied during the coordinated reset or
forward-upgrade rehearsal.
