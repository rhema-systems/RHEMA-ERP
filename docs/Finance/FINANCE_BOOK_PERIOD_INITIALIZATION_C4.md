# Finance book-period and initialization foundation — Stage C4

## Boundary

C4 supplies the readiness evidence that C3 deliberately left unresolved. It does not enumerate books,
decide applicability, create an AccountingEvent, or post a second representation of an economic event.
The central posting engine remains a one-book leaf executor and the C1
`PARALLEL_BOOK_POSTING_DISABLED` safeguard remains mandatory.

Posted journal lines and the C2 exact-book balance/exposure projections remain the financial source of
truth. Initialization records freeze reviewed reconciliation evidence; they do not manufacture balances,
duplicate historical journals, or turn `AccountCurrencyLink` configuration into accounting evidence.

## Exact-book period authority

`AccountingBookPeriod` has one tenant-qualified row for an exact `AccountingBookId` and `FiscalPeriodId`.
Its lifecycle is `Future`, `Open`, `Closed`, or `Locked`. Tenant fiscal-period and module locks remain the
outer authority. An exact-book period is an additional inner gate and can restrict posting, never reopen an
outer closed or locked period.

Creation starts in `Future`. Open, close, lock and reopen transitions require a reason, a published Finance
workflow, rowversion evidence and an independent checker. Request, workflow, mutation and audit evidence
are committed under the serializable Finance writer boundary. No delete endpoint exists.

The V2 posting boundary resolves the fiscal period and concrete accounting book first, then requires the
same-tenant book-period row to be `Open`. Missing authority fails as `ACCOUNTING_BOOK_PERIOD_REQUIRED`;
non-open authority fails as `ACCOUNTING_BOOK_PERIOD_NOT_OPEN`. Exact reversals use the same gate and cannot
cross books. Global period closure and module-lock errors continue to take precedence.

## Governed initialization

Each initialization attempt is versioned so rejected evidence remains immutable and a later reviewed attempt
can supersede it without rewriting history. Supported modes are:

- `IndependentOpeningBalances`: opening evidence must reconcile to the selected book's posted/C2 authority
  at the cutoff.
- `BaseBookCopyAtCutoff`: every mapped account must equal the authoritative source-book balance at cutoff.
- `BaseBalancesWithOpeningAdjustments`: the source balance plus the explicit adjustment must equal the proposed
  opening amount.

The preparation endpoint is read-only. It returns the exact tenant/book mapped-account set, active posting
classification identity, functional currency and server-derived authoritative signed amounts. Clients must
not infer this evidence from account names or classification captions.

Configuration requires exactly one line for every eligible enabled mapping, no extra or duplicate accounts,
canonical currency, nonnegative one-sided debit/credit values, a balanced total, a reason and a stable
idempotency key. Domain-separated evidence and reconciliation fingerprints bind tenant, target/source book,
cutoff and fiscal authority, mapping/classification/account identity, posted source evidence, amounts and
governance inputs. Retry with unchanged evidence returns the same attempt; changed evidence under the same
key fails closed.

Submission and approval rederive the evidence. A different authorized checker must complete the workflow;
intermediate approval steps remain pending. Rejection preserves its attempt and audit lineage. Mutation and
audit failure roll back together.

## Activation readiness

C3 activation approval now succeeds only when all of the following remain true inside the same governed
transaction:

1. the current initialization attempt is fully approved and its fingerprints still reconcile;
2. account coverage and active posting classifications remain complete;
3. the initialization cutoff is the exact end date of one tenant fiscal period; and
4. the required cutoff/first operational exact-book period authority exists and is open under its outer
   fiscal constraints.

Activation only changes the selected book's lifecycle/postability. It does not authorize automatic parallel
posting or bypass the C1 representation lock.

## Migration and operations

`AddAccountingBookPeriodInitializationFoundation` is an unapplied forward migration. Its first SQL operation
preflights canonical tenant/book/fiscal lineage and conflicting predecessor evidence before adding schema.
It intentionally rejects Active, postable, or pending-Active predecessor books because the predecessor has
no truthful immutable C4 readiness evidence to backfill. It never fabricates period authority or approved
initialization fingerprints; retained databases require the reviewed reset or explicit remediation path.

Operators must review migration-operation and guarded SQL Server success/fail-before-mutation/Down tests,
generate the migration script without connecting, and run EF's no-pending-model gate. The migration must not
be applied to configured `RHEMAERP` until the coordinated rehearsal/reset authorization is issued.

## Owner guidance

- Configure and approve exact-book periods after the tenant fiscal calendar is governed.
- Prepare initialization from the read-only server endpoint; never copy mutable UI captions as authority.
- Resolve every readiness blocker before requesting `Active`.
- Retain immutable attempts, fingerprints, workflow and audit evidence through corrections and reversals.
- Do not remove the exact-book period gate or tenant-scoped C1 lock when later orchestration is introduced;
  that later stage must prove its own applicability and atomic multi-book release contract first.
