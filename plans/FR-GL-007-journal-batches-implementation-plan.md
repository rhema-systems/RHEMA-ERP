# FR-GL-007 - Journal Batches Implementation Plan

## Implementation status — 28 July 2026

The mandatory launch scope has been implemented in the current working tree:

- first-class batch header, membership, review history, posting runs, attachments, import sessions, and EF migration;
- independent expected debit and journal-count controls;
- per-entry approval/rejection and genuinely partial, idempotent posting runs;
- full linked batch reversal with complete-unit approval and posting;
- versioned `.xlsx` template, preview/validation, idempotent commit, error workbook, and populated export;
- copy-all and copy-rejected actions;
- permissions, workflow routing, numbering, audit hooks, legacy-entry ownership guards, frontend pages, and focused automated tests.

Recurring batch templates, scheduled generation, and unattended auto-post remain the conditional extension described below. They were not folded into the mandatory implementation because they require a durable scheduler, occurrence claiming/retry semantics, exception operations, and a separately governed system-posting identity.

They are scheduled as proposed versions 1.1–1.3 in [FR-GL-007-journal-batch-subsequent-versions.md](./FR-GL-007-journal-batch-subsequent-versions.md): recurring templates in September 2026, durable scheduled generation in October 2026, and controlled auto-post in November–December 2026. These targets assume an August 2026 initial-release stabilization period and must be reconfirmed during capacity planning.

## Production hardening status — 29 July 2026

- Reversal-batch construction now runs inside one execution-strategy-owned database transaction. Source `ReversalPending`, reversal batch, reversal journals, links, audit records, and submission state commit or roll back together.
- Batch posting suppresses journal-owner notifications inside the posting transaction and sends them only after the outer posting transaction commits. A post-commit notification failure is logged without misreporting the committed accounting result as failed.
- Workbook packages are inspected before ClosedXML loads them. Limits cover entry count, individual and total expanded size, compression ratio, unsafe paths, required Open XML parts, and worksheet count. VBA projects are rejected before parsing; formulas and external relationships remain prohibited.
- Automated gates cover posting failure/recovery, post-commit notification behavior, tenant isolation, permission mapping, import/commit/export round trips, malicious workbooks, 1,000-entry load performance, real SQL Server rollback, and concurrent idempotency/reversal claims.
- Playwright gates cover batch creation and spreadsheet preview/commit flows. Axe checks the register, creation, and import screens for serious or critical accessibility violations.

The SQL tests are intentionally opt-in and create/drop uniquely named disposable databases. The release pipeline must provide `RHEMA_TEST_SQLSERVER` with a SQL Server connection whose login may create and drop test databases. Run the journal-batch gate with:

```powershell
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter "FullyQualifiedName~JournalBatch"
npm --prefix e2e-tests run test:journal-batches
```

GitHub Actions now provides this connection from an isolated SQL Server 2022
service container in the `Journal Batch SQL Server Release Gate` job. The job
fails unless its TRX result proves that both SQL tests executed and passed, and
production deployment depends on that job. No production or shared environment
connection string is used.

Verification snapshot on 29 July 2026:

- API build: passed with 0 warnings and 0 errors.
- Journal batch service tests: 8 passed.
- Spreadsheet security and round-trip tests: 4 passed.
- Opt-in real SQL Server rollback/concurrency tests: 2 passed against a disposable local SQL Server database.
- Finance controller permission/tenant tests: 62 passed.
- Playwright journal-batch workflow and accessibility tests: 3 passed.
- Frontend TypeScript check: passed.
- Maintained legacy-project tests: 66 passed; current unit-accounting suites
  were migrated to the active repository, workflow, and transaction contracts.

The journal-batch gates are green. The unrelated `GhanaStatutoryTaxEngineTests`
DTO compilation issue remains owned separately. Historical finance tests that
target removed repositories/services are explicitly quarantined in
`ErpSystem.Tests`, with replacement coverage and remaining migration work
recorded in `tests/ErpSystem.Tests/LEGACY_TEST_MIGRATION.md`; all compatible
tests remain part of the normal solution gate.

## Requirement

Provide a first-class General Ledger journal batch that groups multiple independently balanced manual journal entries under one control total, review, approval, and posting lifecycle.

The product terminology should be **Journal Batch**. Avoid using "Batch Journal Entry" in the UI because that phrase can also mean spreadsheet import or mass line entry.

## Executive recommendation

Add a new `JournalBatch` aggregate above the existing `JournalEntry` aggregate:

```text
JournalBatch
  -> one or more JournalEntry records
       -> two or more AccountTransaction lines
```

The batch is the workflow and posting authority for its member entries. A journal entry can either be:

- standalone, preserving the current lifecycle; or
- controlled by one journal batch, in which case its submit, approve, reject, and post actions are performed only through that batch.

Launch with manual GL journals only. Do not batch AP, AR, fixed-asset, payroll, revaluation, opening-balance, or other system-generated journals in the first release.

The confirmed design baseline is:

1. The maker creates a Draft batch with a system-generated batch number, fiscal period, accounting book, expected debit total in base currency, and optional expected journal count.
2. The maker creates entries inside the batch or attaches eligible existing Draft manual entries.
3. Each member entry must balance independently.
4. The batch shows calculated actual debit, actual credit, entry count, line count, and variance.
5. Submission is blocked until all controls and journal validations pass.
6. Submission freezes the batch and starts one `JournalBatch` workflow. It does not start a workflow for every child entry.
7. At each workflow stage, the assigned approver records an Approve or Reject decision for every still-eligible entry. The batch therefore has one workflow but auditable per-entry outcomes.
8. Finally approved entries can be posted in one or more user-selected posting runs. Every posting run is atomic: either all entries selected for that run post or none does.
9. Rejected entries remain immutable in the submitted batch. A maker can copy them into a new Draft batch for correction without changing the evidence reviewed in the original batch.
10. A completed batch can be reversed through a linked reversal batch that reverses every posted member entry as one controlled operation.
11. Blank/import templates, import preview and commit, populated export, and row-level error reporting are part of launch scope.
12. Posted journal entries retain their individual journal numbers and their batch, approval, posting-run, and reversal provenance.

## Confirmed requirements and remaining client decisions

Partial review/posting, full batch reversal, and spreadsheet import/export are now confirmed launch requirements. The recommendations below are safe defaults for the remaining semantic decisions.

### 1. Control-total basis

Recommended: `ExpectedDebitTotal` is the aggregate debit amount in the tenant's base currency.

For a balanced batch:

```text
ExpectedDebitTotal == ActualDebitTotal == ActualCreditTotal
```

Do not define the expected total as debit plus credit unless the client explicitly requests a hash total. Debit plus credit doubles the economic value of an ordinary balanced journal.

### 2. Expected journal count

Recommended: optional, but when supplied it must match the actual number of entries before submission. This detects omitted or duplicated entries that an amount-only control may not reveal.

### 3. Posting behavior

Confirmed: partial posting is required.

Recommended safeguards:

- only finally approved, unposted entries are selectable;
- each posting request creates a durable posting run;
- the selected entries post in one database transaction, so the run is all-or-nothing;
- successful earlier runs remain posted if a later run fails;
- rejected entries are never posted; and
- the batch is `PartiallyPosted` until every finally approved entry is posted.

### 4. Batch composition

Recommended: all entries must share the tenant, fiscal period, accounting book, and base control currency. Entry dates, journal types, descriptions, and external references may differ.

### 5. Minimum batch size

Recommended: allow one or more entries, matching common ERP behavior. A one-entry batch can still be useful when the organization requires a control-total cover sheet and batch-level approval.

### 6. Meaning of "reverse the batch"

Recommended: the command is available when every approved entry has been posted and no posted member has already been reversed. It creates a linked reversal batch containing one reversing journal for every posted member and routes that reversal batch through approval and one atomic posting run.

For a source batch containing rejected entries, "all" means all entries that were approved and posted; rejected entries never affected the ledger and therefore have nothing to reverse. Block the command for `PartiallyPosted` batches and when any member has already been reversed, with a clear exception report.

### 7. Recurring and scheduled behavior

Recommended: include **Copy batch** immediately because it is low risk. Treat recurring batch generation and unattended auto-post as an explicit launch extension with separate acceptance criteria and a feature flag. See the effort assessment below.

## Current implementation assessment

### Foundations to reuse

- `JournalEntry` is already a balanced accounting document with individual numbers, totals, fiscal-period linkage, attachments, audit data, approval state, posting state, and reversal linkage.
- `JournalEntryService` already validates manual posting accounts, currency data, fiscal periods, double-entry balance, approval state, and posting eligibility.
- `IFinancePostingEngine` already creates idempotent posting events and joins an existing database transaction when one is active. This allows a batch coordinator to wrap multiple entry postings in one outer transaction.
- The workflow platform supports tenant-specific entity types, sequential approval definitions, approval summaries, maker-checker assignment, recall, and the unified Finance approval workbench.
- `PaymentBatch` provides a local example of a numbered aggregate with members and shared workflow, although GL posting must use posting-run atomicity rather than per-item catch-and-continue behavior.
- Document numbering can seed missing definitions for existing tenants.
- Finance audit, notification, permission, attachment, and sidebar patterns already exist.
- `RecurringJournalTemplate`, occurrence entities, recurrence calculation, business-day adjustment, and database schema already exist and can be generalized. However, there is no production recurring-journal service, controller, occurrence worker, or API-backed frontend; the current recurring screens use demo data.
- ClosedXML is the repository's workbook library for imports and exports. It is MIT-licensed and is paired with the Open XML SDK for low-level inspection of macros and external relationships.

### Gaps to close

- No `JournalBatch` entity, table, DTO, service, controller, numbering definition, or frontend route exists.
- `JournalEntry` has no general-purpose batch membership.
- Current journal submission, approval, rejection, and posting operate on one entry at a time.
- `RevaluationBatchNumber` and `ImportBatchReference` are tracking strings only; they are not batch aggregates and must not be repurposed.
- The Finance approval workbench and workflow display/status services explicitly enumerate supported entity types and therefore require `JournalBatch` integration.
- Current entry posting sends entry-level notifications during its service call. Batch posting must prevent notifications from being emitted before the enclosing transaction commits.
- Current validation and posting construction contain private journal-service logic that should be extracted or safely reused rather than duplicated.
- No durable journal-batch import session, per-entry review history, posting-run aggregate, or linked batch-reversal aggregate exists.
- No existing background worker generates recurring journal occurrences, and the recurring-journal UI is currently demo-only.

## Launch scope

### Included

- Create, view, edit, validate, submit, withdraw before review begins, approve/reject by entry, post, and delete an empty Draft journal batch.
- System-generated batch number.
- Required expected debit total in base currency.
- Optional expected journal count.
- Derived actual debit, credit, entry count, line count, and variance.
- Create a new manual journal entry within a batch.
- Attach and detach eligible existing Draft manual GL entries.
- Batch attachments and notes.
- One batch-level workflow and maker-checker enforcement.
- Per-entry Approve/Reject decisions at every workflow stage, including approve-all/reject-all conveniences and mandatory rejection reasons.
- Partial posting of selected finally approved entries.
- Atomicity and idempotency within each posting run through the existing finance posting engine.
- Unified Finance approval workbench integration and a focused batch approval queue.
- Batch provenance on journal list/detail pages.
- Batch-owned entries cannot use the standalone reversal endpoint.
- Batch-level reversal through a linked reversal batch covering every posted, unreversed member.
- Downloadable `.xlsx` batch import template.
- Server-side workbook preview/validation, idempotent commit, and downloadable row-level error report.
- Export of an existing batch, including controls, journal/line data, review outcomes, and posting results.
- Copy an existing batch to a new Draft batch with new batch/journal numbers and no copied approvals or posting links.
- Full audit and notification coverage.

### Deferred

- Batching system-generated or subledger journals.
- Cross-period, cross-book, or cross-tenant batches.
- Removing one entry from a submitted or approved batch without restarting approval.
- Debit/credit/hash control-total modes selectable by tenant.
- Statistical journal batching, unless the client confirms that its non-monetary statistics must be posted through GL rather than the existing Unit Accounting module.

### Conditional launch extension: recurring batches and auto-post

The mandatory launch scope (batch core, per-entry review, partial posting runs, spreadsheet exchange, full reversal, copy, UI, and automated tests) is approximately **8-12 engineer-weeks** in this repository. With two engineers splitting backend/workflow and frontend/import work, plan roughly **5-7 calendar weeks plus client UAT**, provided the current high-conflict finance changes are stabilized first.

Repository-adjusted estimate for one experienced full-stack engineer, after the batch foundation is stable:

| Capability | Difficulty | Indicative effort | Why |
|---|---:|---:|---|
| Copy batch | Low | 2-3 development days | Deep-copy header, journals, lines, and optional attachments into a clean Draft with new numbers and dates. |
| Recurring batch template and manual generation | Medium | 7-10 development days | Generalize the existing single-journal template/occurrence schema to a versioned batch containing multiple journal templates. |
| Scheduled generation and auto-submit | Medium-high | 5-8 development days | Add a production worker, tenant/time-zone handling, business calendar, idempotent occurrence claiming, retries, exception queue, and notifications. |
| Scheduled auto-post after approval | High | 7-12 development days | Add unattended posting policy, system actor, amount ceilings, period/lock/rate revalidation, safe retries, monitoring, and operational recovery. |

Together, these are approximately **3-5 additional engineering weeks**, including focused automated tests but excluding client UAT latency. The existing recurrence calculator reduces date-rule work, but the lack of a production occurrence service/worker means this is not merely enabling a switch.

Recommended immediate boundary:

1. Ship Copy batch with the mandatory launch.
2. If recurrence is needed at launch, generate a Draft batch on schedule and optionally auto-submit it.
3. Enable auto-post only per approved template, behind a tenant feature flag, with a separate permission, maximum batch amount, an open-period check, exact control-total match, no unresolved/rejected entries, and an exception queue. Auto-post must never bypass required approval.

### What "statistical journal control totals" means

A statistical journal records **non-monetary quantities** for analysis or allocation: for example, 1,250 labour hours, 87 employees, 42 occupied beds, 6,400 machine hours, or 9,500 square metres. Its control total is the expected quantity, not a currency amount.

Example:

```text
Expected statistical units: 1,250 hours
Actual imported units:      1,248 hours
Variance:                       -2 hours
Result: block completion until corrected
```

Products such as PeopleSoft expose control debits, credits, statistical units, and line count separately. This is useful when statistics are posted to statistical ledger accounts and later drive allocations or KPIs.

Rhema ERP already has a separate `UnitJournalEntry` model for non-financial quantities. Therefore the recommended scope is:

- keep `ExpectedDebitTotal` for monetary GL journal batches;
- use `ExpectedUnitTotal` plus a required unit of measure for future Unit Accounting batches; and
- do not mix currency and statistical quantities in one control total.

If the client uses "statistical journal" to mean non-monetary GL accounts rather than Unit Accounting, confirm that accounting model before adding a `Statistical` batch type.

Research references:

- [Oracle: creating journal batches and debit control totals](https://docs.oracle.com/cd/A60725_05/html/comnls/us/gl/journals.htm)
- [PeopleSoft: debit, credit, statistical-unit, and line-count controls](https://docs.oracle.com/cd/E41948_01/fscm92pbh1/eng/fscm/fglr/task_CreatingJournalEntries-9f4be1.html)
- [ClosedXML package and licence](https://www.nuget.org/packages/ClosedXML)

## Domain and data design

### Orthogonal status model

Persist three strongly typed status axes rather than one expanding composite enum:

```text
ApprovalStatus: Draft | PendingApproval | PartiallyApproved | Approved | Rejected
PostingStatus:  NotReady | Ready | Posting | PartiallyPosted | Posted | PostingFailed
ReversalStatus: NotReversed | ReversalPending | PartiallyReversed | Reversed
```

This allows valid combinations such as `PartiallyApproved + PartiallyPosted + NotReversed` without inventing a new enum member for every combination. The API also returns a derived `DisplayStatus` for simple list filters and badges.

`Posting` is transient for an active posting run. `PartiallyPosted` means at least one finally approved entry is posted and at least one remains unposted. `Posted` means every finally approved entry is posted; rejected entries do not prevent completion.

`ReversalPending`, `PartiallyReversed`, and `Reversed` describe the source batch's reversal progress. The linked reversal batch has its own approval/posting states. Persist all enum values as stable strings rather than ordinals.

### `JournalBatch`

Add `src/ErpSystem.Core/Entities/Finance/JournalBatch.cs`, based on `BusinessEntity` or the tenant-aware audited base that supplies the required audit fields.

Recommended fields:

- Identity and context:
  - `Id`
  - `TenantId`
  - `BatchNumber`, maximum 50
  - `Description`, maximum 500
  - `FiscalPeriodId`
  - `BookClassification`, maximum 20
  - `ControlCurrencyCode`, maximum 3
  - `ApprovalStatus`
  - `PostingStatus`
  - `ReversalStatus`
- Maker-entered controls:
  - `ExpectedDebitTotal`, `decimal(18,2)`
  - `ExpectedJournalCount`, nullable integer
- Derived review/posting controls, calculated server-side:
  - submitted/approved/rejected/pending entry counts and debit totals
  - posted and remaining-approved entry counts and debit totals
- Submission snapshot:
  - `SubmittedDebitTotal`, nullable `decimal(18,2)`
  - `SubmittedCreditTotal`, nullable `decimal(18,2)`
  - `SubmittedJournalCount`, nullable integer
  - `SubmittedLineCount`, nullable integer
  - `ContentFingerprint`, maximum 64
- Lifecycle:
  - `SubmittedByUserId`, `SubmittedAt`
  - `ReviewCompletedAt`
  - `PostingCompletedAt`
  - `ReversalBatchId`, nullable GUID
  - `ReversalOfJournalBatchId`, nullable GUID
  - `BatchType` (`Standard` or `Reversal`)
  - `Notes`, maximum 2000
- Concurrency:
  - `[Timestamp] RowVersion`
- Navigation:
  - `FiscalPeriod`
  - `Items`
  - `PostingRuns`
  - `Attachments`

Actual totals and variance are derived from active member `JournalEntry` headers. Review and posting totals are derived from `JournalBatchItem` state:

```text
ActualDebitTotal  = SUM(JournalEntry.TotalDebitAmount)
ActualCreditTotal = SUM(JournalEntry.TotalCreditAmount)
Variance          = ActualDebitTotal - ExpectedDebitTotal
ApprovedDebitTotal = SUM(finally approved item debit totals)
RejectedDebitTotal = SUM(finally rejected item debit totals)
PostedDebitTotal   = SUM(posted, finally approved item debit totals)
```

Do not allow a client to write actual totals. The submitted totals are immutable evidence of what was reviewed; they are not the source for live calculations.

### `JournalBatchItem`

Use an explicit membership entity rather than putting the mutable batch lifecycle directly on `JournalEntry`. Partial decisions and partial posting require auditable state per member.

Recommended fields:

- `JournalBatchId`
- `JournalEntryId`
- `SequenceNumber`
- `ReviewStatus`: `Pending`, `Approved`, or `Rejected`
- `FinalReviewedByUserId`, `FinalReviewedAt`
- `FinalRejectionReason`, maximum 1000
- `SubmittedContentFingerprint`, maximum 64
- `PostingStatus`: `NotEligible`, `Ready`, `Posting`, `Posted`, or `Failed`
- `PostedInRunId`, nullable GUID
- `PostedAt`
- `ReversalJournalBatchItemId`, nullable GUID
- row version and tenant/audit fields

The journal entry belongs to at most one active batch item. Membership changes are permitted only while the batch is Draft.

### `JournalBatchItemReview`

Record each workflow-stage decision without overwriting history:

- `JournalBatchItemId`
- `WorkflowInstanceId`
- `WorkflowStageId` or stable stage key
- `WorkflowTaskId`
- `Decision`: `Approved` or `Rejected`
- `Comment`
- `DecidedByUserId`, `DecidedAt`
- tenant/audit fields

Every assigned approver must decide every item still eligible at that stage. Items rejected at an earlier stage do not proceed to later stages. The final `JournalBatchItem.ReviewStatus` is a summary derived when the workflow finishes.

### `JournalBatchPostingRun` and `JournalBatchPostingRunItem`

Persist each partial-post command:

- run ID/number, batch ID, actor, requested/started/completed timestamps
- status: `Pending`, `Posting`, `Posted`, or `Failed`
- idempotency key and sanitized error
- selected batch-item IDs and their posting results

Each run is transactionally atomic. Previous successful runs are not rolled back by a later run.

### `JournalEntry` changes

Add a `JournalBatchItem` navigation or queryable read-only provenance projection. Do not duplicate review/posting state on `JournalEntry`; its existing posting state remains the ledger truth.

### `JournalBatchAttachment`

Add a tenant-aware join entity matching the existing journal attachment pattern:

- `JournalBatchId`
- `FileUploadRecordId`
- unique index on `(TenantId, JournalBatchId, FileUploadRecordId)`

### Database constraints and indexes

Add:

- unique index on `(TenantId, BatchNumber)`
- indexes on `(TenantId, ApprovalStatus, FiscalPeriodId)` and `(TenantId, PostingStatus, FiscalPeriodId)`
- index on `(TenantId, CreatedAt)`
- unique index on `JournalBatchItem (TenantId, JournalEntryId)` for active rows
- unique index on `JournalBatchItem (TenantId, JournalBatchId, SequenceNumber)` for active rows
- unique index on `JournalBatchPostingRun (TenantId, JournalBatchId, IdempotencyKey)`
- unique review index on item/workflow-stage/task as appropriate to prevent duplicate decisions
- foreign keys with restricted delete behavior
- check constraint `ExpectedDebitTotal > 0`
- check constraint `ExpectedJournalCount IS NULL OR ExpectedJournalCount > 0`
- row-version concurrency token

Do not backfill batch items. Existing journals remain standalone and continue through the current lifecycle.

## Control and validation rules

### Entry eligibility

An existing journal can be added only when:

- it belongs to the current tenant;
- it is not deleted;
- it is unbatched;
- it is a manual GL journal;
- its posting status is Draft;
- it has no active journal-entry workflow;
- its fiscal period matches the batch;
- its book classification matches the batch;
- it has not been posted, reversed, or generated as a reversal; and
- its source provenance does not identify a subledger or automated process.

Use a positive allow-list for manual GL sources rather than trying to enumerate every disallowed source.

### Batch readiness

Submission and posting must run server-side validation over fresh database data. A batch is ready only when:

- the batch is in the required lifecycle state;
- it has at least one active member;
- the fiscal period is open and not locked for GL;
- every member passes the existing manual-journal account, line, currency, dimension, book, and balance validation;
- every member has at least one debit and one credit line;
- every member is independently balanced;
- actual batch debit equals actual batch credit;
- actual batch debit equals expected debit;
- expected journal count, when supplied, equals actual count;
- all members share the batch tenant, period, book, and control currency basis; and
- no member has an active standalone workflow or incompatible status.

Use exact decimal comparison at the stored currency precision. Do not introduce an undisclosed monetary tolerance.

After review and during partial posting, keep the original expected total unchanged and expose reconciliations:

```text
SubmittedDebitTotal = ApprovedDebitTotal + RejectedDebitTotal
ApprovedDebitTotal  = PostedDebitTotal + RemainingApprovedDebitTotal
```

The expected total controls whether the maker entered the complete batch; it is not rewritten to the approved subset. This preserves the original cover-sheet evidence while making every excluded or unposted amount visible.

### Content fingerprint

At submission, calculate a deterministic SHA-256 fingerprint over the material batch and entry content:

- batch number, fiscal period, book, control currency, expected total, and expected count;
- ordered batch-item IDs, journal IDs, and sequence numbers;
- journal number, date, type, description, reference, book, total debit, and total credit;
- ordered accounting lines including account, debit/credit, currency, rate, dimensions, and references; and
- attachment IDs when attachments form part of approval evidence.

Store the fingerprint and submitted totals/counts. Recalculate and compare it:

- before each approval outcome;
- before each posting run;
- before creating a reversal batch; and
- before an approved posting retry.

If it differs, cancel or invalidate the approval state and return the batch to a correction path. Do not silently approve or post changed content.

## Lifecycle and state transitions

```text
Draft
  -> PendingApproval      submit after successful validation

PendingApproval
  -> Approved             every entry finally approved
  -> PartiallyApproved    at least one entry finally approved and at least one rejected
  -> Rejected             every entry rejected
  -> Draft                maker withdrawal/recall

Rejected
  -> terminal             copy rejected entries into a new Draft batch

Approved
  -> Posting              post all or a selected subset after revalidation

PartiallyApproved
  -> Posting              post all or a selected subset of approved entries

Posting
  -> PartiallyPosted      the run commits and approved entries remain unposted
  -> Posted               the run commits and every approved entry is now posted
  -> PostingFailed        the current run rolls back

PostingFailed
  -> Posting              retry the same unchanged selection or create a new run

PartiallyPosted
  -> Posting              post another selected subset
  -> Posted               all approved entries have posted

Posted
  -> ReversalPending      create and submit the linked full reversal batch
  -> PartiallyReversed    only through pre-existing individual reversals
  -> Reversed             linked reversal batch posts atomically
```

Batch-item and child-journal states are synchronized:

| Event | Batch item | Child journal |
|---|---|---|
| Draft | Pending | Draft |
| Submit | Pending | Pending Approval / Pending |
| Entry rejected at final review | Rejected | Rejected |
| Entry approved at final review | Approved / Ready | Approved |
| Selected posting run begins | Posting | Approved until commit |
| Selected posting run commits | Posted | Posted |
| Selected posting run fails | Ready or Failed with run evidence | Approved |

Do not create child `JournalEntry` workflow instances for batched entries.

Submitted content is immutable. Do not reopen only the rejected items inside the same batch because that would invalidate the control package while retaining sibling approvals. Provide **Copy rejected entries to new batch** and **Copy entire batch** instead. A rare administrator-only cancellation/reopen of an entirely unposted batch may be added later, but it must invalidate every review decision and restart workflow.

## Journal-entry lifecycle guards

Modify the existing journal service/controller so a batched entry cannot bypass batch controls:

- Direct request approval: reject and link the user to the batch.
- Direct approve/reject: reject.
- Direct post: reject, including attempts through alternate finance endpoints.
- Direct delete: reject until the entry is detached from a Draft batch.
- Direct edit: reject; batch entry editing uses a batch-scoped endpoint that delegates to shared journal mutation logic and verifies the parent remains Draft.
- Direct reversal: permit only after the member is Posted and only if a full batch reversal is not in progress. Preserve original batch-item provenance. An individual reversal marks the source batch `PartiallyReversed` and blocks the later "reverse all" command until resolved.

Extend journal DTOs with read-only batch provenance:

- `JournalBatchId`
- `JournalBatchNumber`
- `JournalBatchApprovalStatus`
- `JournalBatchPostingStatus`
- `JournalBatchReversalStatus`
- `JournalBatchItemId`
- `BatchSequenceNumber`
- `BatchReviewStatus`
- `BatchPostingRunNumber`
- `IsBatchControlled`

## Service architecture

### `IJournalBatchService`

Add methods for:

- list/filter batches;
- get detail;
- create/update Draft header;
- delete an empty Draft;
- add/create/remove/update entries;
- validate;
- submit;
- withdraw;
- record per-item review decisions and finalize workflow-stage outcomes;
- copy all or only rejected entries to a new Draft batch;
- create/retry posting runs over selected approved items;
- create, approve, post, and inspect a linked full reversal batch;
- create an import preview, commit it idempotently, export errors, generate a template, and export a populated batch;
- manage attachments; and
- retrieve audit history.

### Shared journal validation

Extract the material private validation in `JournalEntryService` into a reusable internal service, for example `IManualJournalValidator`.

It should support:

- Draft validation for batch submission;
- Approved validation for posting;
- standalone and batch-controlled contexts; and
- returning structured validation issues as well as throwing for command operations.

Both standalone and batch journal flows must call the same validator.

### Shared posting preparation

Extract construction of the manual-journal posting request from `JournalEntryService` into a shared component. The batch service should not call HTTP endpoints and should not duplicate posting rules.

Recommended posting-run algorithm:

1. Create the EF execution strategy.
2. Begin a Serializable database transaction.
3. Reload the batch, selected batch items, journal entries, lines, period, and required account data.
4. Verify batch/item statuses, row versions, final per-entry approval, workflow completion, and fingerprints.
5. Create or claim one posting run by client idempotency key and mark its selected items Posting.
6. Prevalidate every selected entry before posting the first one.
7. Call the existing posting engine for each selected item in sequence. The engine joins the current transaction.
8. Verify a posted result and posting event for every selected item.
9. Mark the run Posted and derive the batch as `PartiallyPosted` or `Posted`.
10. Record batch and child audit events.
11. Commit.
12. After commit, publish one batch notification rather than one notification per child.

On failure:

- roll back every change in the failed run, including journal, posting-event, balance-snapshot, exchange-rate-use, item, and batch changes;
- clear the change tracker;
- reload the batch/run;
- record the run as Failed with a sanitized error in a separate recovery write;
- record a batch posting-failed audit event; and
- do not emit a success notification.

Do not catch and continue inside a posting run. Partial posting is achieved by choosing a smaller run, not by committing only the entries that happened to succeed.

### Idempotency

- Reusing the same posting-run idempotency key and selection returns the existing result.
- A different payload with the same key returns `409 Conflict`.
- Calling post when all approved entries are already Posted returns the completed batch without adding events.
- Batch-item status/fingerprint, posting-run uniqueness, and existing per-entry `FinancePostingEvent` uniqueness remain authoritative.
- A posting retry after a rolled-back transaction creates no duplicates.
- Overlapping concurrent runs for the same item must block or return a conflict; disjoint selections may serialize initially for simpler accounting safety.

### Full batch reversal

The batch reversal command must not mutate or delete the original posting. It creates a `BatchType=Reversal` journal batch linked by `ReversalOfJournalBatchId`.

Algorithm:

1. Require the source batch to be `Posted`, every approved item to be posted, and every posted item to be unreversed.
2. Revalidate the period/date selected for reversal and require a reason.
3. In one transaction, create a reversal batch and one reversing journal entry per posted source item, preserving source entry and line references.
4. Set the reversal batch expected debit total and expected count from the generated reversal entries.
5. Freeze the generated content and route the reversal batch through normal approval.
6. Require Approve All for a full reversal; do not allow partial review or partial posting of a generated full-reversal batch.
7. Post the reversal batch in one atomic posting run.
8. Link every original item to its reversing item and mark the source batch `Reversed`.

If generation fails, create no partial reversal batch. If reversal posting fails, no reversal entry in that run may reach the ledger.

### Spreadsheet import/export

Use a stable, versioned workbook contract:

- `Instructions`: template version, accepted formats, examples, and validation rules.
- `Batch`: one data row containing description, period/date basis, book, control currency, expected debit total, expected journal count, and notes.
- `JournalEntries`: client journal key, date, journal type, description, reference, and optional source key.
- `JournalLines`: client journal key, line number, account code, debit, credit, currency, exchange rate, description, reference, and supported dimensions.
- `Lookups`: protected reference lists where practical; server validation remains authoritative.
- populated exports additionally include `ReviewAndPosting` with entry decision, reviewer/date, rejection reason, posting run, and posting status.

Import is a two-step server-side process:

1. `Preview`: scan the workbook, enforce file/row/cell limits, reject formulas/macros/external links where unsupported, resolve tenant-owned accounts/dimensions, calculate controls, and return normalized rows plus row/cell errors without creating accounting records.
2. `Commit`: consume a short-lived, tenant/user-bound preview token; verify the file hash/template version and revalidate reference data; then create the Draft batch, journals, lines, and import audit record in one transaction.

Store an import session with file hash, template version, uploader, status, expiry, row counts, error counts, committed batch ID, and idempotency key. Do not persist the raw workbook beyond the product's file-retention policy.

The implementation uses ClosedXML under the MIT licence, with the Open XML SDK for workbook security inspection. No commercial workbook-library licence configuration is required.

## API design

Add `[Route("api/finance/journal-batches")]`:

### Query and detail

- `GET /` - filters for status, period, date range, creator, batch number, and free text; paginated.
- `GET /{id}` - batch header, derived controls, entry summaries, attachments, workflow summary, and allowed actions.
- `GET /pending-approval` - focused approver queue; the unified workbench remains authoritative.
- `GET /{id}/audit-trail`.

### Draft maintenance

- `POST /` - create Draft batch and generate its number.
- `PUT /{id}` - update Draft header with row version.
- `DELETE /{id}` - soft-delete only an empty Draft batch.
- `POST /{id}/entries` - create a new journal inside the batch.
- `POST /{id}/entries/{journalEntryId}` - attach an existing eligible Draft journal.
- `PUT /{id}/entries/{journalEntryId}` - update a member through shared journal mutation logic.
- `DELETE /{id}/entries/{journalEntryId}` - detach a Draft member, leaving it as a standalone Draft.
- `POST /{id}/validate` - structured errors, warnings, actual totals, counts, and variance.
- `POST /{id}/copy` - copy the whole batch to a clean Draft.
- `POST /{id}/copy-rejected` - copy rejected entries to a clean Draft.

### Workflow and posting

- `POST /{id}/submit`
- `POST /{id}/withdraw`
- `POST /{id}/review-decisions` - submit item-level Approve/Reject decisions for the caller's current workflow task.
- `POST /{id}/approve-all`
- `POST /{id}/reject-all`
- `POST /{id}/finalize-review-stage`
- `POST /{id}/posting-runs` - post selected approved item IDs with an idempotency key.
- `GET /{id}/posting-runs`
- `GET /{id}/posting-runs/{runId}`
- `POST /{id}/posting-runs/{runId}/retry`
- `POST /{id}/reversal-batch` - preview/validate and create the linked full reversal batch.

Approval endpoints must verify workflow assignment; they must not infer approval from the permission alone.

### Spreadsheet exchange

- `GET /import-template?version=1`
- `POST /imports/preview`
- `GET /imports/{sessionId}`
- `GET /imports/{sessionId}/errors`
- `POST /imports/{sessionId}/commit`
- `GET /{id}/export`

### Attachments

- `POST /{id}/attachments/{fileUploadRecordId}`
- `DELETE /{id}/attachments/{fileUploadRecordId}`
- `GET /{id}/attachments`

Use Problem Details or the API's existing validation response convention for business-rule failures. Return `409 Conflict` for row-version, membership, fingerprint, or concurrent lifecycle conflicts.

## DTO design

Add:

- `JournalBatchListItemDto`
- `JournalBatchDetailDto`
- `JournalBatchEntrySummaryDto`
- `JournalBatchItemReviewDto`
- `JournalBatchReviewDecisionDto`
- `JournalBatchPostingRunDto`
- `CreateJournalBatchPostingRunDto`
- `CreateJournalBatchReversalDto`
- `JournalBatchImportPreviewDto`
- `JournalBatchImportIssueDto`
- `CommitJournalBatchImportDto`
- `CreateJournalBatchDto`
- `UpdateJournalBatchDto`
- `CreateJournalBatchEntryDto`
- `UpdateJournalBatchEntryDto`
- `JournalBatchQueryDto`
- `JournalBatchValidationResultDto`
- `JournalBatchValidationIssueDto`
- `JournalBatchActionDto`
- `JournalBatchAttachmentDto`

The detail DTO should expose explicit calculated fields:

- `ActualDebitTotal`
- `ActualCreditTotal`
- `DebitVariance`
- `IsBalanced`
- `IsControlTotalMatched`
- `ActualJournalCount`
- `IsExpectedJournalCountMatched`
- `ActualLineCount`
- `PendingReviewCount`
- `ApprovedEntryCount`
- `ApprovedDebitTotal`
- `RejectedEntryCount`
- `RejectedDebitTotal`
- `PostedEntryCount`
- `PostedDebitTotal`
- `RemainingApprovedEntryCount`
- `RemainingApprovedDebitTotal`
- `CanEdit`
- `CanSubmit`
- `CanReview`
- `CanPostAny`
- `CanReverseBatch`

Do not make the frontend reproduce lifecycle eligibility rules from status alone.

## Workflow integration

Add the canonical workflow entity type `JournalBatch`.

Seed **Journal Batch Approval** using the existing Finance stages:

1. Accounts Officer review.
2. Finance Manager approval.
3. Financial Controller final approval.

The exact client workflow remains tenant-configurable after seeding.

Integration points that must all be updated:

- `DatabaseSeedingService` finance workflow entity/type definitions.
- `FinanceWorkflowStatusAdapter` entity-type list and status handling.
- `WorkflowEntityDisplayService` batch number, description, and detail URL.
- `FinanceApprovalsController` supported key list, queue facts, approval outcome, rejection outcome, and amount/currency metadata.
- `WorkflowController` generic recall/correction paths, or the equivalent workflow-domain completion hook.
- Batch detail workflow summary calls using `JournalBatch`.
- Focused batch approval queue.

All approval entry points must delegate item decisions and final domain changes to `IJournalBatchService`. Avoid having the batch controller and unified workbench implement separate state-transition logic.

The synchronous `FinanceWorkflowStatusAdapter` can update the batch header, but it cannot load the items, reviews, and child journals needed for partial outcomes. Submission, each review-stage finalization, and recall must therefore invoke the batch service in the same unit of work so batch, item, child, and workflow states cannot drift.

Per-stage behavior:

1. The assigned approver receives one workflow task for the batch.
2. The task UI requires an Approve/Reject decision for every item that survived the prior stage.
3. Reject requires an item-specific reason; approve-all/reject-all only prefill the decisions.
4. Finalizing the stage writes immutable `JournalBatchItemReview` rows and completes the workflow task.
5. Rejected items leave the later-stage population. If none remain, end the workflow and mark the batch Rejected.
6. On the final configured stage, surviving items become Approved/Ready and the batch becomes Approved or PartiallyApproved.

Approval routing and authority thresholds should use the validated actual debit total in base currency. Because submission requires actual to equal expected, the two values should agree; using actual prevents a maker-entered control from understating approval authority.

## Numbering

Add:

```text
Module: Finance
Document type: JournalBatch
Display name: General Journal Batch
Default format: JB-{YYYY}-{#####}
Reset: Yearly
Allow manual entry: false
```

Update:

- backend `FinanceDocumentTypes`;
- `DocumentSequenceDefaults`;
- `DocumentNumberingService.GetExistingNumbersAsync`;
- frontend `FinanceDocumentTypes`; and
- relevant numbering tests.

Because `EnsureDefaultsAsync` inserts missing definitions, existing tenants receive the new sequence without destructive backfill.

## Permissions and roles

Add dedicated permissions because a batch action has a larger accounting impact than an individual entry action:

- `Finance.JournalBatches.View`
- `Finance.JournalBatches.Create`
- `Finance.JournalBatches.Edit`
- `Finance.JournalBatches.Delete`
- `Finance.JournalBatches.SubmitForApproval`
- `Finance.JournalBatches.Approve`
- `Finance.JournalBatches.Post`
- `Finance.JournalBatches.Reverse`
- `Finance.JournalBatches.Import`
- `Finance.JournalBatches.Export`
- `Finance.JournalBatches.Copy`
- conditional `Finance.JournalBatches.ManageRecurring`
- conditional `Finance.JournalBatches.EnableAutoPost`

Update:

- `FinancePermissions`;
- permission seed data;
- default role mappings;
- `FinancePermissionPolicyMap` with an explicit `JournalBatchPolicy`; and
- controller security tests.

Recommended default duty split:

- Finance users/clerks: view and, where appropriate, create/edit.
- Accounts Officers and Senior Accountants: create, edit, and submit.
- Finance Managers: approve/reject when assigned by workflow.
- Financial Controllers: final approval and post.
- Tenant/Super Admin: all permissions, still subject to workflow assignment and self-approval policy.

## Audit and notifications

Add finance audit events:

- `JournalBatchCreated`
- `JournalBatchUpdated`
- `JournalBatchEntryAdded`
- `JournalBatchEntryRemoved`
- `JournalBatchControlChanged`
- `JournalBatchValidated`
- `JournalBatchSubmitted`
- `JournalBatchWithdrawn`
- `JournalBatchApproved`
- `JournalBatchItemApproved`
- `JournalBatchItemRejected`
- `JournalBatchReviewStageFinalized`
- `JournalBatchRejected`
- `JournalBatchCopied`
- `JournalBatchRejectedItemsCopied`
- `JournalBatchImportPreviewed`
- `JournalBatchImported`
- `JournalBatchImportFailed`
- `JournalBatchExported`
- `JournalBatchPostingStarted`
- `JournalBatchPostingRunCreated`
- `JournalBatchPosted`
- `JournalBatchPartiallyPosted`
- `JournalBatchPostingFailed`
- `JournalBatchDuplicatePostingAttempt`
- `JournalBatchReversalCreated`
- `JournalBatchReversed`
- `JournalBatchReversalFailed`
- `JournalBatchDeleted`

Audit metadata should include expected/actual/approved/rejected/posted totals, counts, variance, content fingerprint, entry IDs/numbers, item decisions and reasons, actor, workflow instance/task/stage, import session, posting run, and reversal links.

Notify:

- maker when submitted, any item is rejected, review completes, a posting run completes/fails, or reversal completes/fails;
- assigned approvers through the workflow notification foundation; and
- poster/controller when an approved batch is ready to post, if tenant notification rules enable it.

Emit one batch-level posted notification. Do not flood the maker with one posted notification for every member.

## Frontend plan

### Routes

Add:

- `/finance/journal-batches`
- `/finance/journal-batches/new`
- `/finance/journal-batches/[id]`
- `/finance/journal-batches/[id]/edit`
- `/finance/journal-batches/approvals`
- `/finance/journal-batches/import`

### Navigation

Under General Ledger, add **Journal Batches** adjacent to **Journal Entries**. Keep the existing standalone journal approval queue, but label the new focused queue **Journal Batch Approval Queue**.

### List page

Show:

- batch number;
- description;
- fiscal period;
- expected debit;
- actual debit and credit;
- variance;
- entry count versus expected count;
- status;
- approved/rejected/posted/remaining counts and totals;
- creator;
- submitted/posted dates; and
- warning/error indicator.

Provide filters for batch number, status, period, creator, and date.

### Create/edit experience

Use a batch workspace rather than placing all journal lines in one giant table:

1. Batch controls: description, period, book, expected debit, expected count, and notes.
2. Entries table: journal number, date, type, description/reference, debit, credit, balance, and validation state.
3. Actions: create entry, attach existing Draft entry, spreadsheet import, edit, detach, view, and delete after detach.
4. Live control panel: expected, actual debit, actual credit, variance, counts, and readiness.
5. Attachments and supporting evidence.
6. Submit action with validation summary.

### Approval experience

Approvers must be able to:

- see maker, period, book, expected and actual totals, counts, and variance;
- review the immutable submitted values;
- expand or open every journal and its lines;
- see attachments and audit trail;
- see workflow stage and pending approvers; and
- select entries and apply Approve or Reject in bulk;
- override the decision entry by entry;
- provide a mandatory reason for each rejection;
- see prior-stage decisions and comments without changing them; and
- finalize the stage only when every eligible entry has a decision.

The batch detail also needs posting selection with Approved/Unposted filters, selected totals/counts, posting-run preview, a typed confirmation, and posting-run history.

For a completed batch, show **Reverse entire batch** only when every posted entry is unreversed. The preview lists the reversal date/period, all journals to reverse, total, and reason before creating the controlled reversal batch.

### Import/export experience

- Download the current versioned blank template.
- Drag/drop or select an `.xlsx` file.
- Display a preview grouped by batch, journal, and lines.
- Show errors by sheet, row, column, value, and corrective message.
- Allow an error workbook download.
- Disable commit while any blocking issue remains.
- On commit, navigate to the new Draft batch and show its import provenance.
- On batch detail, export a populated workbook containing both accounting content and review/posting outcomes.

### Existing journal pages

For a batch-controlled entry:

- show a **Controlled by Journal Batch JB-...** banner and link;
- hide or disable direct submit, approve, reject, post, and delete actions;
- show the batch status;
- permit normal viewing and voucher output; and
- preserve standalone behavior when there is no active `JournalBatchItem`.

Add optional batch number/status columns and filters to the journal-entry list.

### Frontend types and service

Add journal-batch types to `frontend/src/types/finance.ts` or a focused `journal-batches.ts`, plus methods in `finance-data.service.ts` or a dedicated `journal-batch-data.service.ts`.

Use the API-provided `Can...` fields and structured validation issues. Do not duplicate server business rules in React.

## Migration and backward compatibility

Create one additive migration:

- create `JournalBatches`;
- create `JournalBatchItems`;
- create `JournalBatchItemReviews`;
- create `JournalBatchPostingRuns` and `JournalBatchPostingRunItems`;
- create `JournalBatchImportSessions`;
- create `JournalBatchAttachments`;
- add indexes, constraints, and foreign keys; and
- update the model snapshot.

Deployment behavior:

- existing journal rows remain unchanged and standalone;
- existing journal-entry workflow instances remain valid;
- no batch is inferred from `ImportBatchReference` or `RevaluationBatchNumber`;
- existing APIs remain backward compatible except that newly batched entries reject direct lifecycle mutations; and
- document sequence defaults and workflow definitions are seeded idempotently for every tenant.

Before applying the migration in production:

- back up the database;
- verify there is no object named `JournalBatches`;
- run the migration in UAT;
- verify the new sequence and workflow for more than one tenant; and
- smoke-test existing standalone journal creation, approval, posting, and reversal.

## Backend implementation map

Expected files/components:

### Core

- `Entities/Finance/JournalBatch.cs`
- `Entities/Finance/JournalBatchItem.cs`
- `Entities/Finance/JournalBatchItemReview.cs`
- `Entities/Finance/JournalBatchPostingRun.cs`
- `Entities/Finance/JournalBatchImportSession.cs`
- `Entities/Finance/JournalEntry.cs`
- batch approval/posting/reversal enum definitions
- `DTOs/Finance/JournalBatchDtos.cs`
- `Interfaces/Finance/IJournalBatchService.cs`
- reusable manual-journal validation/posting contracts
- spreadsheet exchange contracts
- `Interfaces/Numbering/IDocumentNumberingService.cs`
- `Services/Workflow/FinanceWorkflowStatusAdapters.cs`
- `Services/Workflow/WorkflowEntityDisplayService.cs`

### Data

- `ApplicationDbContext.cs`
- optional `Configuration/JournalBatchConfiguration.cs`
- `Services/DocumentNumberingService.cs`
- EF migration and snapshot

### API

- `Services/Finance/GL/JournalBatchService.cs`
- `Services/Finance/GL/JournalBatchSpreadsheetService.cs`
- `Services/Finance/GL/JournalBatchReversalService.cs`
- refactored `JournalEntryService.cs`
- `Controllers/Finance/JournalBatchController.cs`
- guarded `JournalEntryController.cs`
- `Controllers/Finance/FinanceApprovalsController.cs`
- generic `WorkflowController.cs` recall/correction integration
- `Authorization/FinancePermissionPolicyMap.cs`
- `Extensions/ServiceCollectionExtensions.cs`
- `Services/DatabaseSeedingService.cs`

### Shared

- `FinancePermissions.cs`
- `FinanceAuditEvents.cs`

### Frontend

- finance types
- journal-batch data service
- list/new/detail/edit/approval pages
- journal list/detail provenance changes
- sidebar navigation
- document-numbering type constant

## Test strategy

### Domain/service tests

- Generates a unique tenant-scoped batch number.
- Rejects cross-tenant batch access and entry membership.
- Rejects nonmanual, posted, reversed, already batched, wrong-period, or wrong-book entries.
- Allows attaching and detaching an eligible Draft manual entry.
- Calculates debit, credit, variance, journal count, and line count correctly.
- Blocks submission on control-total mismatch.
- Blocks submission on expected-count mismatch.
- Blocks submission when any child is unbalanced or otherwise invalid.
- Stores and verifies the submission fingerprint.
- Starts one batch workflow and no child workflows.
- Records immutable item decisions for each workflow stage.
- Requires a decision for every eligible item and a reason for every rejection.
- Excludes earlier-stage rejected items from later stages.
- Derives Approved, PartiallyApproved, or Rejected correctly.
- Copies rejected items or the entire batch into a clean Draft without approval/posting provenance.
- Synchronizes child states on submit, item approval/rejection, withdrawal, and posting.
- Blocks every direct child lifecycle bypass.
- Rejects stale row versions.

### Workflow tests

- Batch appears in the unified Finance approval queue with the correct amount, currency, reference, and detail URL.
- Only the currently assigned approver can act.
- Maker cannot self-approve when maker-checker is active.
- Intermediate workflow approval does not mark the batch finally Approved.
- Final approval updates each surviving item and child exactly once.
- Mixed item outcomes produce `PartiallyApproved`; all-rejected produces `Rejected`.
- Rejection history remains immutable and recall is allowed only before any final decisions/posting.
- Focused and unified approval routes produce identical outcomes.

### Posting tests

- A selected set of Approved items posts and creates one posting event per selected entry.
- Unselected Approved items remain Ready and the batch becomes `PartiallyPosted`.
- Rejected, pending, already-posted, changed, or mismatched items cannot enter a run.
- Closed/locked period blocks the selected run.
- An injected failure on a later selected entry rolls back every earlier entry and ledger artifact in that run without rolling back prior successful runs.
- Retry by idempotency key succeeds without duplicates.
- Replaying a successful run is idempotent; reusing its key with a different selection conflicts.
- Overlapping concurrent posting attempts cannot post an item twice.
- Posting the remaining Approved items derives `Posted`.
- Notifications are emitted only after commit and only once at batch level.

### Reversal tests

- A completed batch generates one linked reversal journal per posted source item.
- Reversal amounts, signs, dates, descriptions, dimensions, source links, expected total, and count are correct.
- A `PartiallyPosted` source or any pre-reversed item blocks full-batch reversal with a detailed exception.
- Generated full-reversal batches require approve-all and cannot be partially posted.
- An injected failure rolls back the entire reversal posting run.
- Successful reversal links every item and marks the source batch Reversed.
- Replaying reversal creation/posting is idempotent.

### Spreadsheet tests

- The generated template has the documented version and required sheets/columns.
- A valid multi-journal workbook previews and commits to exactly one Draft batch.
- Unknown account, cross-tenant identifier, duplicate client key, invalid date/period, formula, excessive rows, unbalanced entry, and control mismatch produce precise row/cell errors.
- Preview creates no accounting rows.
- Commit requires the same user, tenant, file hash, and unexpired token and revalidates reference data.
- Retrying commit is idempotent and cannot duplicate journals.
- Error workbook and populated export contain the expected controls and statuses.
- File-size, row-count, and decompression limits are enforced.

### Compatibility tests

- Existing standalone journal create/edit/submit/approve/post/reverse tests remain green.
- Recurring and system-generated journals remain standalone.
- Fiscal-period close checks continue to detect all unposted member entries through existing `JournalEntry` statuses.
- Reports and detailed ledger continue to use posted journal entries without depending on the batch.
- Individual reversal of a posted member preserves the original batch-item link and audit context and prevents accidental full-batch double reversal.

### API/security tests

- Every action maps to its intended dedicated permission.
- Read queries are tenant-filtered.
- Business conflicts return the expected status and structured error.
- Batch attachment access is tenant-safe.
- Import preview/commit tokens cannot cross users or tenants.
- Posting/reversal idempotency and concurrency conflicts return the expected response.
- Pagination and filters are stable.

### Frontend tests

- Type and amount mapping.
- Control panel and variance rendering.
- Per-entry review selection, required rejection reason, and stage finalization.
- Posting selection, selected totals, and posting-run history.
- Action visibility for every composite status.
- Batched-entry banner and lifecycle blocking.
- Approval drill-down and rejection reason.
- Import preview/error correction and export download.
- Batch reversal preview and confirmation.
- Error rendering for concurrency and validation conflicts.
- Type check, lint check, unit tests, and production build.

## Delivery sequence

### PR 1 - Domain foundation and migration

- Batch, item, review, posting-run, import-session, attachment, and reversal relationships; enums, constraints, and row versions.
- DTO shells.
- numbering definition.
- DbContext configuration and migration.
- model and numbering tests.

Exit gate: database migration is additive, tenant-safe, and existing journal tests pass.

### PR 2 - Batch draft and control service

- service/interface registration.
- CRUD, membership, derived totals, structured validation, attachments.
- copy entire batch and copy rejected entries.
- shared manual-journal validator extraction.
- direct child edit/delete/submit guards.
- service and controller tests.

Exit gate: a batch can be prepared, copied, and validated but cannot yet be submitted or posted.

### PR 3 - Workflow, permissions, audit, and notifications

- permissions and policies.
- workflow entity and definition seed.
- status adapter and display resolver.
- unified approval workbench facts/outcomes.
- submit, withdraw, item decisions, per-stage finalization, and terminal review state.
- fingerprint and immutable submitted snapshot.
- workflow and security tests.

Exit gate: focused and unified approval paths agree, and child workflows cannot be created.

### PR 4 - Partial posting runs

- shared posting request builder.
- durable posting runs, selection validation, and outer Serializable transaction.
- rollback/retry/idempotency handling.
- post-commit batch notification.
- failure-injection and concurrency tests.

Exit gate: a forced failure leaves no selected member or ledger artifact posted for that run, while prior successful runs remain intact.

### PR 5 - Spreadsheet import/export

- versioned template and populated export.
- preview/commit session, validation, and idempotency.
- row/cell errors and downloadable error workbook.
- workbook security/size limits and licence decision.
- import/export tests.

Exit gate: a valid workbook creates one Draft batch exactly once; invalid data creates no accounting records and is fully diagnosable.

### PR 6 - Batch reversal

- linked full-reversal batch generation.
- approve-all and atomic-post constraints.
- reversal provenance and source-state derivation.
- failure, concurrency, and idempotency tests.

Exit gate: a qualifying completed batch is fully reversed with one action and no partial ledger result is possible.

### PR 7 - Frontend

- types and API client.
- batch list, workspace, detail, per-entry approval queue, posting runs, import/export, copy, and full reversal.
- sidebar.
- journal provenance and disabled direct actions.
- frontend tests and accessibility pass.

Exit gate: maker and approver can complete the workflow end to end without using raw APIs.

### PR 8 - Optional recurring extension

- generalize/version recurring templates for a multi-journal batch.
- production occurrence generator and operational exception queue.
- scheduled Draft generation and optional auto-submit.
- separately permissioned/feature-flagged auto-post policy and system actor.
- scheduler, retry, failover, time-zone, period-roll, and monitoring tests.

Exit gate: duplicate workers cannot generate or post duplicate occurrences, and every unattended failure is visible and safely retryable.

### PR 9 - UAT hardening

- load/performance test realistic large batches.
- tenant isolation and authorization review.
- accounting sign-off on control-total semantics.
- accounting sign-off on partial-review/posting and full-reversal semantics.
- import template compatibility and malicious-workbook review.
- migration rehearsal.
- audit evidence and notification review.
- user documentation.

Exit gate: signed UAT scenarios and no unresolved high-severity accounting or security findings.

## Acceptance criteria

The feature is complete when:

1. A user with permission can create a uniquely numbered Draft journal batch.
2. The batch can contain multiple independently numbered, independently balanced manual journal entries.
3. The UI and API show expected debit, actual debit, actual credit, variance, entry count, and line count.
4. A mismatch or invalid member prevents submission.
5. Submission produces exactly one batch workflow and freezes all financial content.
6. At every workflow stage, the assigned approver must Approve or Reject each eligible entry, with reasons for rejections and immutable decision history.
7. Mixed final decisions produce a PartiallyApproved batch; rejected entries never post and can be copied to a new Draft.
8. No child entry can be submitted, approved, rejected, deleted, or posted outside its batch lifecycle.
9. Any content change invalidates the submitted fingerprint and prevents approval, posting, or reversal generation.
10. A user can post any valid selected subset of finally approved entries.
11. Each posting run is atomic and idempotent; a failure commits zero entries or ledger artifacts for that run without undoing earlier runs.
12. The batch becomes PartiallyPosted or Posted based on the remaining approved items.
13. A completed, fully eligible batch can generate, approve, and atomically post a linked reversal batch for all of its posted entries.
14. The system provides a versioned spreadsheet template, non-persisting preview with row/cell errors, idempotent import commit, error workbook, and populated export.
15. A user can copy a whole batch or rejected entries into a clean Draft with new numbers and no inherited approval/posting state.
16. Successful posting preserves individual journal numbers and creates ordinary ledger/posting events for each entry.
17. Existing standalone journals behave exactly as before.
18. Every operation is tenant-safe, permission-controlled, concurrency-protected, audited, and represented correctly in both approval interfaces.

## Implementation risks

- **Dual workflow mutation paths:** the module controller, generic workflow adapter, and unified approval workbench can drift unless all final outcomes delegate to the batch service.
- **Workflow/item-state drift:** per-stage decisions, final item state, child journal state, and workflow completion must be committed by one domain operation.
- **Premature notifications:** existing entry posting can notify inside the service call; batch success notifications must occur after the outer commit.
- **Private duplicated rules:** copying journal validation or posting-request construction would cause standalone and batch behavior to diverge.
- **Stale header totals:** always validate against live entries and lines; do not trust client totals or submitted snapshots as current truth.
- **Posting-run overlap:** row versions, item-level claims, idempotency keys, fingerprints, lifecycle guards, and transaction isolation are all required; none is sufficient alone.
- **Large posting/reversal runs:** atomic runs hold locks across selected members. Load-test realistic upper bounds and add a tenant-configurable selection maximum if evidence requires it; full reversal must still be one controlled operation.
- **Spreadsheet attack surface:** enforce upload/decompression/row/cell limits, MIME/signature checks, formula/external-link policy, tenant-safe lookups, short-lived import sessions, and sanitized errors.
- **Workbook library maintenance:** ClosedXML and the Open XML SDK are permissively licensed, but dependency versions and security advisories must still be reviewed during routine upgrades.
- **Unattended auto-post:** scheduler failover, duplicate execution, closed periods, stale rates, disabled accounts, changed dimensions, and system-actor audit all create operational risk. Keep auto-post separately permissioned and feature-flagged.
- **Overlapping in-progress work:** several high-conflict integration files in the current worktree already have unrelated modifications, including DbContext, workflow, approval, permission, numbering, audit, sidebar, and finance types. Stabilize or commit those changes before implementation and merge this feature in the PR order above.

## Recommended UAT script

1. Create `JB-2026-00001` for an open period with expected debit GHS 10,000 and expected count 3.
2. Add three balanced entries totalling GHS 9,750.
3. Confirm the batch shows debit and credit of GHS 9,750 and variance of GHS -250.
4. Confirm submission is blocked.
5. Correct the entries to GHS 10,000 and confirm all three remain independently balanced.
6. Submit and confirm the batch and children become Pending Approval and cannot be edited or posted directly.
7. At the first stage, approve entries 1 and 2 and reject entry 3 with a reason; confirm entry 3 does not proceed.
8. At all remaining stages, approve entries 1 and 2 and confirm the batch becomes PartiallyApproved.
9. Post only entry 1 and confirm the batch becomes PartiallyPosted, entry 2 remains Ready, and entry 3 remains Rejected.
10. Repeat the same request/idempotency key and confirm no duplicate event.
11. Post entry 2 and confirm the batch becomes Posted.
12. Copy rejected entries and confirm a new Draft contains a clean copy of entry 3 with new numbers and no approval history.
13. Create the full reversal batch, approve all generated reversal entries, post it, and confirm both original posted entries reverse atomically and the source batch becomes Reversed.
14. Import an equivalent three-entry workbook: first preview a bad account/control mismatch, download the errors, correct it, and commit exactly once.
15. Export the imported batch and verify controls, entry/line data, review decisions, posting runs, and statuses.
16. Create and post a standalone journal to confirm backward compatibility.
