---
title: Journal Batch Replication Playbook
document_type: ai_agent_implementation_guide
source_system: RHEMA ERP
feature: General Ledger Journal Batches
feature_version: "1.0"
last_verified: "2026-07-29"
audience:
  - AI coding agents
  - finance-system architects
  - backend and frontend engineers
  - QA and release engineers
---

# Journal Batch Replication Playbook

## 1. Purpose

Use this document to reproduce the RHEMA ERP Journal Batch capability in
another application while respecting that application's existing finance
architecture.

This is not a request to copy class names or force a .NET design into another
stack. It is a behavioral, accounting-control, data-integrity, security, user
experience, and release-gate specification. The implementing agent must first
analyze the target application's finance module, map its existing concepts to
this design, and reuse its authoritative journal validation, approval, posting,
period-locking, tenant, audit, notification, and file-upload foundations.

The target outcome is a first-class batch aggregate above ordinary journal
entries:

```text
JournalBatch
  -> one or more independently balanced JournalEntry records
       -> two or more debit/credit JournalLine records
```

A journal batch is not the same as a multi-line journal entry:

- One journal entry has one header, one journal number, and multiple lines that
  collectively balance.
- One journal batch has one control header and contains several journal
  entries. Every member entry has its own journal number and must balance
  independently.
- The batch supplies data-entry controls, one approval workflow with per-entry
  decisions, partial posting runs, spreadsheet exchange, and full linked
  reversal.

Use the product term **Journal Batch**. Avoid **Batch Journal Entry**, which is
often confused with a single journal containing many lines or with spreadsheet
import.

## 2. Instructions to the implementing AI agent

### 2.1 Required behavior

Before changing code:

1. Read this document completely.
2. Inventory the target finance module using the discovery checklist in
   section 5.
3. Produce a short adaptation map identifying which existing components will
   be reused, extended, or added.
4. Identify blocking accounting-policy choices instead of silently inventing
   them.
5. Implement in the sequence in section 20.
6. Prove the feature against the release gates in sections 21 and 22.

### 2.2 Adapt, do not duplicate

The target agent must:

- reuse the existing journal entity and journal-line model;
- reuse the authoritative journal validator and ledger posting engine;
- reuse the existing fiscal-period and period-lock rules;
- reuse the existing workflow engine if it can support one batch workflow with
  item-level decisions;
- use the application's tenant, identity, permission, numbering, attachment,
  audit, and notification mechanisms;
- preserve existing standalone-journal behavior; and
- keep reports and ledgers dependent on posted journals and ledger movements,
  not on the batch header.

Do not create a second ledger posting engine, a separate chart of accounts, or
a parallel workflow system solely for journal batches.

### 2.3 Questions the target agent must answer

Derive these answers from code or configuration where possible. Ask the product
owner only when the answer materially changes accounting behavior:

1. Is the application tenant-aware, company-aware, legal-entity-aware, or
   single-organization?
2. What is the authoritative journal header, line, validator, and posting
   service?
3. Are journal totals stored in functional/base currency, transaction
   currency, or both?
4. How are fiscal periods represented, and what makes a period open, closed, or
   locked?
5. Does the workflow engine support sequential stages, role/user assignment,
   maker-checker rules, and a transactionally consistent domain callback?
6. Is partial entry approval inside one workflow acceptable to the client?
7. Can approved entries be posted in separate runs, or must every approved
   entry post together?
8. Which roles may create, submit, approve, post, reverse, import, export, and
   copy batches?
9. What is the maximum realistic batch size?
10. Are non-monetary statistical quantities posted through the GL or through a
    separate unit/statistical accounting module?
11. Is recurring generation or unattended auto-post required in the initial
    release? It was intentionally separated from RHEMA ERP v1.
12. What database engine and concurrency primitives are available?

## 3. Implemented v1 capability

The RHEMA ERP v1 feature includes:

- tenant-scoped, system-numbered Draft journal batches;
- required expected debit total in tenant base currency;
- optional expected journal count;
- actual debit, actual credit, variance, entry count, and line count;
- creation of journals inside the batch;
- attachment of eligible existing Draft manual GL journals;
- batch attachments and notes;
- validation before submission;
- a submission snapshot and content fingerprints;
- one batch workflow rather than one workflow per child journal;
- Approve or Reject decisions for every eligible entry at every workflow stage;
- mandatory reasons for rejected entries;
- final `Approved`, `PartiallyApproved`, or `Rejected` batch outcomes;
- selective posting of finally approved entries;
- atomic and idempotent posting runs;
- `PartiallyPosted` and `Posted` outcomes;
- copying all entries or only rejected entries into a clean Draft;
- full linked batch reversal;
- versioned `.xlsx` template generation;
- workbook preview, validation, idempotent commit, error workbook, and populated
  export;
- dedicated permissions and role grants;
- tenant isolation, audit integration, journal provenance, and standalone
  journal lifecycle guards;
- post-commit journal-owner notifications;
- web pages for register, creation, detail/review/posting, and import;
- accessibility, malicious-workbook, performance, transaction, idempotency, and
  concurrency release gates; and
- a dedicated real-SQL CI job that cannot pass by skipping its database tests.

The following are not part of v1:

- recurring batch templates;
- scheduled occurrence generation;
- unattended auto-submit or auto-post;
- batching AP, AR, payroll, fixed-asset, revaluation, opening-balance, or other
  system-generated journals;
- cross-period, cross-book, or cross-tenant batches;
- reopening only rejected items inside an already reviewed batch;
- mixing monetary and statistical control totals; and
- configurable debit/credit/hash control modes.

## 4. Non-negotiable accounting and integrity invariants

Treat these as executable business rules, not UI guidance.

### 4.1 Composition

1. A batch belongs to exactly one tenant/company/legal entity.
2. A batch belongs to one fiscal period and one accounting book.
3. The control currency is the tenant's base or functional currency.
4. A standard v1 batch contains manual GL journals only.
5. A journal belongs to at most one active batch.
6. Every member journal has its own immutable journal number.
7. Every member journal must balance independently.
8. A batch may contain one or more journals. A one-journal batch is valid when
   a control cover sheet or batch approval is still required.

### 4.2 Control totals

For a balanced monetary batch:

```text
ExpectedDebitTotal == ActualDebitTotal == ActualCreditTotal
Variance = ActualDebitTotal - ExpectedDebitTotal
```

`ExpectedDebitTotal` is the sum of member-journal debit totals in base currency.
Do not define it as debit plus credit; that doubles the economic amount of a
balanced journal.

`ExpectedJournalCount` is optional. When supplied, it must equal the active
member count before submission. It detects missing or duplicated entries that
an amount control may not reveal.

Actual totals are always calculated server-side from current member journals.
Never accept actual totals from the client.

### 4.3 Immutability and evidence

1. Draft header and membership changes are allowed only while the batch is
   Draft.
2. Submission stores totals/counts and cryptographic content fingerprints.
3. Submitted financial content is immutable.
4. Posting recalculates item fingerprints and rejects changed content.
5. Rejected entries remain immutable in the reviewed batch.
6. Corrections happen by copying rejected entries into a new Draft batch.
7. Approval and posting history is append-only.

### 4.4 Approval and posting

1. Submission starts exactly one batch workflow.
2. It must not start standalone workflows for child journals.
3. At each workflow stage, the assigned approver must decide every still
   eligible entry.
4. Permission alone is not approval authority; current workflow assignment must
   also be verified.
5. Only finally approved, unposted entries may be selected for posting.
6. Every posting run is all-or-nothing for its selected entries.
7. An earlier successful run remains posted if a later run fails.
8. Rejected entries never post.
9. The same idempotency key with the same selection returns the original run.
10. Reusing an idempotency key with another selection is a conflict.
11. Concurrent requests cannot post the same item twice.

### 4.5 Reversal

1. A source batch must be fully `Posted`, not `PartiallyPosted`.
2. Every posted source journal must still be unreversed.
3. Reversal creates new journal entries; it never mutates or deletes ledger
   history.
4. One reversal journal is generated for every posted source item.
5. Rejected source items are excluded because they never affected the ledger.
6. A reversal batch must be approved or rejected as a complete unit.
7. A reversal batch must post in one complete atomic run.
8. Source journals and source batch become reversed only after the reversal
   posting commits.
9. Only one active reversal batch may claim a source batch.

### 4.6 Side effects

Never broadcast “posted” notifications before the accounting transaction
commits. Use a post-commit dispatch step or a transactional outbox. A
notification failure after commit must be logged/retried without reporting the
accounting transaction as failed.

## 5. Target-codebase discovery checklist

The implementing agent should search the target repository and complete this
table before designing files.

| Concern | Find in the target application | Decision |
|---|---|---|
| Journal header | Entity/model, statuses, number, totals, fiscal period, book, source module | Extend, do not replace |
| Journal lines | Debit/credit representation, currencies, dimensions, account link | Reuse unchanged where possible |
| Journal validation | Balance, account status, direct-posting, period, currency, dimensions | Extract/reuse as shared validator |
| Posting engine | Transaction boundary, posting-event identity, balance/ledger writes | Batch calls the same engine |
| Reversal engine | Existing reversal creation and link fields | Reuse line inversion rules |
| Fiscal periods | Open/closed/locked rules and posting-date resolution | Batch and every member must comply |
| Workflow | Entity type, instance, stage/task assignment, maker-checker | Add `JournalBatch` workflow type |
| Tenancy/company | Required scope key and query filters | Put scope on every new table/query/index |
| Current user | Stable user ID and display name | Required for audit and workflow |
| Numbering | Concurrency-safe sequence service | Add Journal Batch document type |
| Permissions | Policy/action mapping and role seeds | Add dedicated batch permissions |
| Audit | Business audit/event service | Record batch, item, run, import, reversal events |
| Notifications | In-app/SignalR/email/outbox | Dispatch only after commit |
| Attachments | Existing upload record and retention policy | Add batch-to-upload join |
| Spreadsheet | Current library, upload limits, security inspection | Prefer permissive library plus package inspection |
| API errors | Problem Details or established business-error envelope | Use conflict for stale/concurrent claims |
| Frontend | Routes, service layer, forms, table and permission handling | Add register/create/detail/import |
| Tests | Unit, integration, real DB, browser, accessibility | Add release gates; do not rely on mocks alone |
| CI | Database services and required jobs | Force real DB tests to execute, not skip |

Useful repository searches include equivalents of:

```text
JournalEntry
JournalLine OR AccountTransaction
PostJournal OR PostingEngine
ReverseJournal
FiscalPeriod OR AccountingPeriod
WorkflowInstance OR Approval
CurrentTenant OR CompanyId OR LegalEntityId
Permission OR Policy
DocumentNumber OR Sequence
AuditEvent
Notification OR Outbox OR SignalR
FileUpload
DbContext OR migration OR schema
```

### 5.1 Required adaptation report

Before implementation, the target agent should state:

```markdown
## Journal Batch Adaptation Map

- Stack/database:
- Tenant/company boundary:
- Existing journal aggregate:
- Existing authoritative validator:
- Existing posting engine:
- Existing reversal behavior:
- Period-lock source:
- Workflow engine and stage semantics:
- Numbering service:
- Permission system:
- Audit/notification mechanism:
- Spreadsheet library:
- Transaction/concurrency primitives:
- Planned new tables/models:
- Planned modified existing components:
- Blocking product decisions:
```

## 6. Domain model

Use an explicit membership entity. Do not put all batch review/posting fields
directly on the existing journal header.

### 6.1 Status axes

Persist orthogonal status axes as stable strings:

```text
BatchApprovalStatus:
  Draft
  PendingApproval
  PartiallyApproved
  Approved
  Rejected
  Cancelled

BatchPostingStatus:
  NotReady
  Ready
  Posting
  PartiallyPosted
  Posted

BatchReversalStatus:
  NotReversed
  ReversalPending
  Reversed

BatchType:
  Standard
  Reversal

ItemReviewStatus:
  Pending
  Approved
  Rejected

ItemPostingStatus:
  NotEligible
  Ready
  Posting
  Posted
  Failed

PostingRunStatus:
  Pending
  Posting
  Posted
  Failed

ImportStatus:
  Previewed
  Invalid
  Committed
  Expired
  Failed
```

Separate axes allow valid combinations such as:

```text
PartiallyApproved + PartiallyPosted + NotReversed
Approved + PartiallyPosted + NotReversed, with the latest posting run Failed
Approved + Posted + ReversalPending
```

Return a derived `displayStatus` for badges and lists instead of persisting
every composite state.

### 6.2 JournalBatch

Recommended fields:

| Field | Purpose |
|---|---|
| `id` | Primary key |
| `tenantId` or equivalent | Mandatory organization scope |
| `batchNumber` | System-generated, max 50 |
| `description` | Required, max 500 |
| `fiscalPeriodId` | One period for all members |
| `bookClassification` | Accounting book/ledger, max 20 |
| `controlCurrencyCode` | Base/functional currency, max 3 |
| `batchType` | Standard or Reversal |
| `approvalStatus` | Independent approval axis |
| `postingStatus` | Independent posting axis |
| `reversalStatus` | Independent reversal axis |
| `expectedDebitTotal` | Required monetary control, decimal with application currency precision |
| `expectedJournalCount` | Optional positive integer |
| `submittedDebitTotal` | Submission snapshot |
| `submittedCreditTotal` | Submission snapshot |
| `submittedJournalCount` | Submission snapshot |
| `submittedLineCount` | Submission snapshot |
| `contentFingerprint` | SHA-256 hex digest |
| `submittedByUserId`, `submittedAt` | Submission provenance |
| `approvedByUserId`, `approvedAt` | Optional final summary |
| `workflowInstanceId` | One batch workflow |
| `reviewCompletedAt` | Review completion |
| `postingCompletedAt` | All finally approved items posted |
| `reversalOfJournalBatchId` | Source link on reversal batch |
| `reversalReason` | Required for reversal |
| `isVoided`, `voidedByUserId`, `voidedAt`, `voidReason` | Preserve abandoned reversal attempts while releasing the source claim |
| `notes` | Max 2000 |
| `rowVersion` or equivalent | Optimistic concurrency |
| audit/soft-delete fields | Follow target conventions |

Do not store client-writable actual, approved, rejected, posted, or remaining
totals. Derive them from active members:

```text
actualDebitTotal = SUM(memberJournal.totalDebit)
actualCreditTotal = SUM(memberJournal.totalCredit)
variance = actualDebitTotal - expectedDebitTotal
approvedDebitTotal = SUM(item.journal.totalDebit WHERE item.reviewStatus=Approved)
rejectedDebitTotal = SUM(item.journal.totalDebit WHERE item.reviewStatus=Rejected)
postedDebitTotal = SUM(item.journal.totalDebit WHERE item.postingStatus=Posted)
remainingApprovedDebitTotal =
  SUM(item.journal.totalDebit WHERE reviewStatus=Approved AND postingStatus<>Posted)
```

### 6.3 JournalBatchItem

Recommended fields:

| Field | Purpose |
|---|---|
| `journalBatchId` | Batch owner |
| `journalEntryId` | Existing journal |
| `sequenceNumber` | Stable display/import order |
| `reviewStatus` | Pending/Approved/Rejected |
| `finalReviewedByUserId`, `finalReviewedAt` | Final outcome |
| `finalRejectionReason` | Required when rejected |
| `submittedContentFingerprint` | Journal snapshot hash |
| `postingStatus` | NotEligible/Ready/Posting/Posted/Failed |
| `postingClaimRunId`, `postingClaimedAt` | Current atomic posting claim; null outside an active run |
| `postedInRunId`, `postedAt` | Posting provenance |
| `reversalJournalBatchItemId` | Source-to-reversal item link |
| `rowVersion` | Optimistic concurrency |
| tenant/audit/soft-delete fields | Follow target conventions |

### 6.4 JournalBatchItemReview

This is append-only stage history:

| Field | Purpose |
|---|---|
| `journalBatchItemId` | Reviewed item |
| `workflowInstanceId` | Batch workflow |
| `workflowStepInstanceId` | Concrete task/stage instance when available |
| `workflowStageKey` | Stable key such as `order:name` |
| `decision` | Approved or Rejected |
| `comment` | Required for rejection |
| `decidedByUserId`, `decidedAt` | Actor/time |
| tenant/audit fields | Scope and provenance |

Items rejected at one stage do not continue to later stages. Approvals at an
intermediate stage are recorded in history, but the item becomes finally
`Approved` only when the workflow completes.

### 6.5 JournalBatchPostingRun

A run is the durable command and result for one selected subset:

| Field | Purpose |
|---|---|
| `journalBatchId` | Parent |
| `runNumber` | Monotonic within batch |
| `idempotencyKey` | Stable client request key |
| `status` | Pending/Posting/Posted/Failed |
| `requestedByUserId`, `requestedAt` | Request provenance |
| `startedAt`, `completedAt` | Execution timing |
| `selectedDebitTotal` | Server-derived selection total |
| `selectedEntryCount` | Server-derived count |
| `errorMessage` | Sanitized, bounded error |
| `rowVersion` | Concurrency |

`JournalBatchPostingRunItem` links a run to each selected item and, where the
posting engine exposes it, to the resulting posting-event/ledger-event ID.

### 6.6 JournalBatchAttachment

Use a tenant-aware join to the application's existing upload record. Do not
invent a second file-storage subsystem.

### 6.7 JournalBatchImportSession

Recommended fields:

- tenant/company scope;
- SHA-256/HMAC hash of the random preview bearer token; return the raw token
  once and never persist it;
- SHA-256 file hash;
- canonical normalized-payload hash used to bind idempotency;
- template version;
- sanitized original file name;
- uploader user ID;
- expiry, normally 30 minutes;
- status;
- journal count, line count, and error count;
- normalized payload JSON or structured staging rows;
- validation issues JSON or structured issue rows;
- idempotency key;
- committed batch ID;
- bounded error message; and
- audit fields.

Do not persist the raw workbook unless the application's retention policy
explicitly requires it.

## 7. Database constraints and indexes

Database constraints are part of the correctness model.

Add equivalents of:

```text
UNIQUE active (tenantId, batchNumber)
UNIQUE active (tenantId, journalEntryId) on JournalBatchItem
UNIQUE active (tenantId, journalBatchId, sequenceNumber)
UNIQUE (tenantId, journalBatchId, runNumber)
UNIQUE active (tenantId, journalBatchId, idempotencyKey)
UNIQUE active (tenantId, postingRunId, journalBatchItemId)
UNIQUE active (tenantId, itemId, workflowInstanceId, workflowStageKey)
UNIQUE active (tenantId, reversalOfJournalBatchId)
  WHERE source is not null AND reversal attempt is not voided
UNIQUE (tenantId, previewTokenHash)
UNIQUE active (tenantId, importIdempotencyKey) WHERE key is not null
```

Also add:

```text
CHECK expectedDebitTotal > 0
CHECK expectedJournalCount IS NULL OR expectedJournalCount > 0
CHECK sequenceNumber > 0
CHECK postingRun.runNumber > 0
CHECK postingRun.selectedEntryCount > 0
```

Index common register/queue paths:

```text
(tenantId, approvalStatus, fiscalPeriodId)
(tenantId, postingStatus, fiscalPeriodId)
(tenantId, createdAt)
(tenantId, batchId, itemReviewStatus, itemPostingStatus)
(tenantId, postingClaimRunId)
(tenantId, uploaderUserId, importExpiresAt)
```

Use restricted deletes for accounting relationships. Soft-delete only where the
existing system uses it, and adapt filtered/partial unique indexes to the target
database.

Do not backfill existing journals into batches. Existing journals remain
standalone.

## 8. Lifecycle and state transitions

### 8.1 Batch header

| Operation | Preconditions | Result |
|---|---|---|
| Create | Open/unlocked period, base currency configured | Draft + NotReady + NotReversed |
| Edit controls | Draft + current row version | Draft |
| Add/create/remove member | Draft | Draft |
| Delete | Draft and empty | Soft-deleted |
| Validate | Any readable state | Structured live result; no mutation required |
| Submit | Draft and valid | PendingApproval + NotReady |
| Withdraw | PendingApproval, no item review begun | Draft; clear submission/workflow snapshot |
| Review intermediate stage | Assigned approver; decisions for all pending items | Rejections final; survivors continue |
| Review final stage | Workflow completed | Approved or PartiallyApproved; Ready |
| Reject all | Assigned approver | Rejected + NotReady |
| Post subset | Approved/PartiallyApproved; selected items Ready | PartiallyPosted or Posted |
| Posting failure | Active run failed atomically | Preserve Ready/PartiallyPosted/Posted progress; run Failed |
| Copy | Source exists | New clean Draft |
| Create reversal | Source Posted and fully eligible | Source ReversalPending; linked reversal PendingApproval |
| Withdraw/cancel reversal | PendingApproval; posting has not begun | Atomically void attempt, release source claim, source NotReversed |
| Post reversal | Reversal approved as a unit | Source and reversal Reversed |

### 8.2 Child-journal ownership

Once a journal has an active batch item, direct standalone mutation endpoints
must reject:

- edit;
- delete;
- submit;
- withdraw/recall;
- approve;
- reject;
- post;
- reverse;
- attachment changes that affect the reviewed evidence.

Return a conflict such as:

```json
{
  "code": "JOURNAL_BATCH_OWNED",
  "message": "This journal belongs to batch JB-2026-00001. Use the journal batch workflow.",
  "batchId": "...",
  "batchNumber": "JB-2026-00001"
}
```

Enforce this in the domain/service layer or in every mutation boundary, not only
in one controller. Background jobs, internal services, and alternate APIs must
not bypass it.

Read models should expose:

```text
journalBatchId
journalBatchNumber
journalBatchItemId
```

The journal UI should show a “Controlled by journal batch …” banner, link to the
batch, and disable direct lifecycle actions.

## 9. Validation rules

Validation must run server-side against fresh database data during explicit
validation, submission, posting, reversal creation, and import commit.

### 9.1 Batch readiness

Block submission when:

- the batch is empty;
- actual debit does not equal actual credit;
- actual debit does not equal expected debit;
- expected journal count is present and mismatched;
- the fiscal period is missing, closed, or locked;
- the control currency is not the tenant base currency;
- any journal belongs to another tenant, period, or book;
- any journal is not an eligible manual GL journal;
- any journal is posted, reversed, recurring, revaluation-generated,
  auto-reversal-generated, or controlled by another workflow/batch;
- any journal fails the existing journal validator;
- any account is inactive, non-postable, a prohibited control account, or
  outside tenant scope;
- required exchange rates or dimensions are invalid; or
- a reversal item is not a generated inverse of an eligible posted source
  journal.

Return structured issues:

```json
{
  "isValid": false,
  "expectedDebitTotal": 10000.00,
  "actualDebitTotal": 9750.00,
  "actualCreditTotal": 9750.00,
  "variance": -250.00,
  "entryCount": 3,
  "expectedJournalCount": 3,
  "lineCount": 8,
  "issues": [
    {
      "code": "CONTROL_TOTAL_MISMATCH",
      "message": "Expected debit 10,000.00 does not equal actual debit 9,750.00.",
      "journalBatchItemId": null,
      "journalEntryId": null,
      "severity": "Error"
    }
  ]
}
```

Useful codes include:

```text
EMPTY_BATCH
BATCH_UNBALANCED
CONTROL_TOTAL_MISMATCH
EXPECTED_COUNT_MISMATCH
PERIOD_NOT_OPEN
PERIOD_MISMATCH
BOOK_MISMATCH
NOT_MANUAL_GL
JOURNAL_INVALID
INVALID_BATCH_REVERSAL
CONTENT_CHANGED_AFTER_SUBMISSION
```

### 9.2 Currency precision

RHEMA ERP rounds monetary controls to two decimals using midpoint rounding away
from zero. The target should use its currency precision service where one
exists. Never compare binary floating-point values for accounting equality.

## 10. Submission fingerprints

At submission, create a canonical payload per item containing at least:

- sequence number;
- journal ID and number;
- entry date;
- journal type;
- description and reference;
- accounting book;
- total debit and credit in base currency; and
- every active line ordered deterministically, including line number, account,
  debit, credit, currency, foreign amount, exchange rate, dimensions/segments,
  source reference, and description.

Serialize with deterministic naming/order and calculate SHA-256.

Every journal-line entity must expose an explicit positive `lineNumber` (or an
equivalent immutable sequence). Enforce uniqueness within a journal in the
database where the legacy data permits it, and always validate it at service
and import boundaries. Line numbers may be reassigned only while the journal
is Draft; they are immutable after submission. Fingerprints, dimension copies,
spreadsheet round trips, and reversals must never use `createdAt` ordering.

The batch fingerprint should include:

- batch number;
- fiscal period;
- book;
- control currency;
- expected debit total;
- expected journal count; and
- ordered item IDs, sequence numbers, and item fingerprints.

Pseudocode:

```text
itemFingerprint = SHA256(canonicalJson(itemFinancialPayload))
batchFingerprint = SHA256(canonicalJson(batchControls + orderedItemFingerprints))
```

Store item and batch fingerprints at submission. Recompute each selected item
immediately before posting. Fingerprints supplement, but do not replace,
immutability guards, row versions, database constraints, and transaction
isolation.

## 11. Core orchestration algorithms

### 11.1 Create a batch

```text
require authenticated actor and tenant
load fiscal period within tenant
require period open and unlocked
require control currency == tenant base currency
generate concurrency-safe batch number
create Draft batch with expected controls
save
audit JournalBatchCreated
return server-derived detail and allowed actions
```

Suggested number pattern:

```text
JB-{YYYY}-{#####}
```

Use the target numbering service. Never calculate `MAX(number)+1` without a
concurrency-safe sequence/lock.

### 11.2 Add or create a journal

For a journal created inside a batch:

```text
require batch Draft
force batch fiscal period and book onto create command
force source module to manual GL
call existing journal creation service
attach returned journal through the same eligibility function
```

For an existing journal:

```text
require same tenant
require Draft journal
require manual GL source
require same fiscal period and book
require not recurring/revaluation/auto-reversal/reversed
require no active standalone workflow
require no active batch membership
assign next stable sequence
save and audit
```

### 11.3 Submit

Use a retry/execution strategy appropriate to the database. One transaction
must own all submission changes:

```text
validate from fresh data
begin transaction
  reload Draft batch and items with journals/lines
  revalidate lifecycle
  calculate/store each item fingerprint
  reset item review=Pending and posting=NotEligible
  set child journals to Pending Approval
  calculate/store batch fingerprint and submission totals/counts
  set batch PendingApproval + NotReady
  start exactly one JournalBatch workflow
  store workflow instance ID
  save all
commit
audit/notify according to application outbox policy
```

If workflow start cannot participate in the database transaction, use an
outbox/saga with a recoverable `SubmissionPending` state. Do not leave a batch
Pending Approval without a workflow or a workflow without a matching batch
snapshot.

### 11.4 Withdraw

Allow only while Pending Approval and before any item-level review exists:

```text
cancel workflow
reset item review/posting states
clear item fingerprints
restore child journals to Draft/Withdrawn
clear batch submission/workflow snapshot
set batch Draft + NotReady
save and audit
```

### 11.5 Review a stage

```text
require batch PendingApproval and workflow instance
require caller is current assigned approver
load current workflow stage
eligibleItems = items still ReviewStatus.Pending
require exactly one decision for every eligible item
require rejection reason for each rejection
if reversal batch: require all decisions identical

begin transaction
  append review row for every eligible item
  mark rejected items final Rejected + NotEligible
  mirror rejection summary to child journal
  advance/reject current workflow step

  if workflow failed/cancelled or every item rejected:
    batch Rejected + NotReady
    if reversal batch, release source ReversalPending claim

  else if workflow completed:
    mark every surviving pending item final Approved + Ready
    mirror final approval to child journal
    batch = PartiallyApproved if any rejected else Approved
    batch PostingStatus = Ready

  save all domain and workflow changes
commit
audit immutable decisions
```

The target workflow adapter and the batch controller must not implement
different finalization logic. All entry decisions and child-state changes must
delegate to one batch-domain operation.

### 11.6 Partial posting run

Recommended flow:

```text
require batch Approved or PartiallyApproved
deduplicate selected item IDs
require non-empty selection

if run exists for idempotency key:
  require stored selection == requested selection
  return stored run

require every selected item belongs to batch
require every selected item Approved + Ready
if reversal batch:
  require selection == all approved reversal items
recompute and compare each item fingerprint

create durable Pending run and run-item selection rows

begin one accounting transaction
  atomically claim every selected item:
    UPDATE item
      SET postingStatus=Posting,
          postingClaimRunId=run.id,
          postingClaimedAt=now
      WHERE reviewStatus=Approved
        AND postingStatus=Ready
        AND postingClaimRunId IS NULL
  require affected row count == selected item count
  otherwise roll back and return 409 Conflict: this run lost the race
  reload run, batch, and claimed items
  mark run/batch Posting
  for each selected journal:
    call the existing posting engine in "batch-owned transaction" mode
    capture posting-event ID
    mark item Posted with run/time and clear its active claim
  mark run Posted
  set batch PartiallyPosted if approved-ready items remain, otherwise Posted
  if completed reversal batch, complete all source reversal links
  save
commit

after commit:
  dispatch owner/batch notifications or enqueue outbox messages
  log notification failure without rolling back accounting
  audit posted or partially posted result
```

Important integration contract:

- The ordinary journal post path may own a transaction and send notifications.
- The batch path needs a posting method that participates in the batch's outer
  transaction and suppresses notifications.
- After the outer commit, call a separate notification method or publish an
  outbox event.

On posting failure:

```text
roll back every selected journal and ledger artifact in this run
do not roll back earlier successful runs
mark the durable run Failed with a bounded/sanitized error
release any claim still owned by the failed run
derive header progress from approved item states:
  no approved items posted -> Ready
  some approved items posted -> PartiallyPosted
  all approved items posted -> Posted
audit failure
```

The unique run-item index prevents duplicate selection rows inside one run; it
does **not** arbitrate claims across different runs. The conditional
`Approved + Ready + unclaimed -> Posting, claimed by Run X` update is the
cross-run concurrency boundary. Perform all selected-item claims atomically;
do not let a run claim only a subset.

The posting-run record is the authoritative outcome of an attempt. Do not
overwrite meaningful header progress with a single `PostingFailed` state. A UI
may derive a composite label such as `Partially posted — last attempt failed`
from header progress plus the latest run.

### 11.7 Copy batch or rejected entries

Copying always creates a clean Draft:

- generate new batch and journal numbers;
- use a requested open period/date or the source values;
- copy header controls, journals, lines, currency, rates, and dimensions;
- recalculate expected total/count from selected items;
- optionally link the same immutable attachment records;
- do not copy workflow IDs, decisions, fingerprints, posting runs, posting-event
  links, reversal links, or approval/posting timestamps; and
- set source metadata/notes so provenance is auditable.

### 11.8 Full batch reversal

The reversal-construction orchestrator must own one outer database transaction.
Internal helper methods must reuse that transaction; they must not start nested
transactions.

```text
begin execution-strategy-owned transaction
  reload source with a concurrency claim/lock
  require source PostingStatus=Posted
  require source ReversalStatus=NotReversed
  require every posted source item unreversed
  find open period containing reversal date

  create new Draft batch:
    type=Reversal
    reversalOfBatchId=source.id
    expectedDebitTotal=sum(source posted item debit totals)
    expectedJournalCount=source posted item count
    book/currency copied from source
    reason stored

  set source ReversalPending

  for every posted source item:
    create a new journal with every line debit/credit inverted
    preserve account, currency, rate, dimensions, line order, and source links
    link source item to reversal item
    link original journal to reversal journal

  validate reversal batch
  store fingerprints and submit through workflow using transaction-aware helpers
  save all
commit
```

If any step fails, both source `ReversalPending` and every reversal artifact
must roll back.

Partial reversal is forbidden, so there is no `PartiallyReversed` state.
Historical reversal attempts are one-to-many from the source, while a filtered
unique constraint permits only one non-voided active attempt.

If a pending reversal is withdrawn, cancelled, or finally rejected before
posting, one database transaction must:

1. mark the reversal attempt `Cancelled`/`Rejected` and `isVoided=true`;
2. record actor, time, and reason;
3. clear active source-item reversal links owned by that attempt; and
4. return the source to `NotReversed`.

The voided attempt remains queryable for audit but no longer participates in
the active-reversal unique constraint. A replacement reversal may then be
created. Once a reversal has posted, cancellation is prohibited.

The reversal batch then follows approval. It cannot have mixed item decisions
and cannot be partially posted. After its atomic posting commits:

- mark original journals reversed;
- set reversal journal IDs/dates/reasons/types;
- mark source and reversal batch `Reversed`; and
- audit the complete link graph.

## 12. Service architecture

Recommended boundaries:

```text
JournalBatchService
  - header/membership validation and mutation
  - submission and workflow item outcomes
  - posting-run orchestration
  - copy
  - reversal orchestration
  - attachments

JournalBatchSpreadsheetService
  - template
  - package inspection
  - preview normalization and validation
  - idempotent commit
  - error workbook
  - populated export

ExistingJournalService
  - create/update ordinary journal
  - shared readiness validation
  - standalone post
  - batch-transaction post without premature notification
  - post-commit notification

ExistingPostingEngine
  - authoritative ledger and posting-event writes

ExistingWorkflowService
  - start/cancel
  - current stage and assignment
  - process stage action
```

Expose transaction-aware internal methods where orchestration spans multiple
operations. Avoid public methods that each start their own transaction when a
batch reversal or import needs one atomic outer transaction.

## 13. API contract

Use the target application's route and error conventions. The RHEMA reference
uses:

| Method | Route | Purpose | Permission |
|---|---|---|---|
| GET | `/api/finance/journal-batches` | Paginated register/search/filter | View |
| GET | `/api/finance/journal-batches/{id}` | Detail, items, runs, allowed actions | View |
| POST | `/api/finance/journal-batches` | Create Draft | Create |
| PUT | `/api/finance/journal-batches/{id}` | Update Draft with row version | Edit |
| DELETE | `/api/finance/journal-batches/{id}` | Delete empty Draft | Delete |
| POST | `/{id}/entries` | Create journal inside batch | Create |
| POST | `/{id}/entries/attach` | Attach existing Draft journal | Create |
| PUT | `/{id}/entries/{journalEntryId}` | Update Draft member | Edit |
| DELETE | `/{id}/entries/{journalEntryId}` | Remove Draft member | Edit |
| POST | `/{id}/validate` | Structured live validation | View |
| POST | `/{id}/submit` | Freeze and start workflow | Submit |
| POST | `/{id}/withdraw` | Withdraw before review begins | Submit |
| POST | `/{id}/review-stage` | Decisions for all eligible items | Approve |
| POST | `/{id}/posting-runs` | Post selected approved item IDs | Post |
| POST | `/{id}/copy` | Copy all into new Draft | Copy |
| POST | `/{id}/copy-rejected` | Copy rejected into new Draft | Copy |
| POST | `/{id}/reversal-batch` | Create linked full reversal | Reverse |
| POST | `/{id}/attachments/{uploadId}` | Link existing upload | Create |
| DELETE | `/{id}/attachments/{uploadId}` | Unlink while Draft | Edit |
| GET | `/import-template` | Download versioned template | Import |
| POST | `/imports/preview` | Validate without accounting mutation | Import |
| POST | `/imports/{sessionId}/commit` | Idempotent atomic commit | Import |
| GET | `/imports/{sessionId}/errors` | Download row-level errors | Import |
| GET | `/{id}/export` | Populated export | Export |

Return row version and server-derived allowed-action flags such as:

```text
canEdit
canSubmit
canReview
canPostAny
canReverseBatch
```

The server remains authoritative. UI flags improve usability but do not replace
permission and lifecycle checks.

Use `409 Conflict` for:

- stale row version;
- journal already in a batch;
- changed post-submission fingerprint;
- duplicate idempotency key with another selection;
- duplicate import idempotency key with another normalized payload;
- concurrent posting/reversal claim; and
- direct mutation of a batch-owned journal.

## 14. Spreadsheet import and export

### 14.1 Library decision

RHEMA ERP replaced EPPlus with:

- **ClosedXML** for workbook creation and reading; and
- **DocumentFormat.OpenXml/Open XML SDK** for package-level security inspection.

ClosedXML and the Open XML SDK are permissively licensed. No EPPlus commercial
license configuration is required. The target should still monitor dependency
security advisories and confirm licenses through its own governance process.

### 14.2 Template version 1

Required sheets and columns:

#### `Instructions`

Explain template version, required sheets, client journal key, control total,
date format, amount rules, and the prohibition on formulas/macros/external
links.

#### `Batch`

```text
TemplateVersion
Description
FiscalPeriodId
BookClassification
ControlCurrencyCode
ExpectedDebitTotal
ExpectedJournalCount
Notes
```

#### `JournalEntries`

```text
ClientJournalKey
TransactionDate
JournalType
Description
Reference
Notes
```

`ClientJournalKey` is a workbook-local join key. It is not the persisted journal
number.

#### `JournalLines`

```text
ClientJournalKey
LineNumber
AccountNumber
TransactionType
Amount
CurrencyCode
ForeignAmount
ExchangeRate
Description
Reference
```

#### `Lookups`

Include tenant-scoped active account numbers/names/direct-posting flags and
open fiscal-period IDs/names/dates. Lookup data helps users but never replaces
server validation.

#### Populated export only: `ReviewAndPosting`

```text
ClientJournalKey
JournalNumber
ReviewStatus
ReviewerId
ReviewedAt
RejectionReason
PostingStatus
PostingRun
PostedAt
```

### 14.3 Upload and package hardening

The reference defensive parser ceilings are:

```text
HTTP/upload size:                 10 MB
maximum journal rows:             5,000
maximum line rows:               50,000
maximum ZIP package entries:      4,096
maximum expanded single entry:    64 MB
maximum total expanded size:      128 MB
maximum compression ratio:        500:1 for entries over 1 MB
maximum worksheets:               64
```

These values limit hostile or accidental resource consumption; they are not a
certified production performance envelope. Do not advertise 5,000 journals /
50,000 lines as supported operating capacity until production-like benchmarks
exercise preview, revalidation, commit, detail loading, approval, posting,
reversal, and export at those maxima against the real database and with explicit
time and memory budgets. Until then, publish a separately measured operational
limit or lower the configured limits to the tested envelope.

Before loading with the workbook library:

1. Require `.xlsx` extension and accepted MIME type.
2. Require a non-empty file within the upload limit.
3. Verify the ZIP/Open XML signature.
4. Reject unsafe absolute or `..` package paths.
5. Enforce entry count, expanded-entry size, total expanded size, and
   compression ratio.
6. Verify required Open XML parts such as `[Content_Types].xml` and
   `xl/workbook.xml`.
7. Open the package read-only with the Open XML SDK.
8. Enforce worksheet count.
9. Explicitly reject a VBA project before ClosedXML loads the workbook.
10. Reject external relationships.
11. After safe load, reject formulas in every used cell.

Do not trust the `.xlsx` extension, MIME type, ZIP metadata, or workbook
library alone.

### 14.4 Preview

Preview must not create batch, journal, line, or ledger records.

Preview:

- scans and validates the package;
- requires the exact required sheet names;
- enforces template version;
- parses rows into normalized server-side data;
- resolves account numbers within the current tenant;
- validates active/direct-posting/non-control account rules;
- validates period/date/book/base-currency rules;
- requires unique nonblank client journal keys;
- requires at least two lines per journal;
- requires positive amounts and unique line numbers;
- accepts only Debit or Credit;
- validates each journal independently balances;
- validates expected total and optional expected count;
- persists a tenant/user-bound short-lived import session;
- stores only a hash of the preview bearer token;
- stores file hash, a canonical normalized-payload hash, normalized data, and
  structured issues; and
- returns session ID, preview token, expiry, totals, counts, and issues.

### 14.5 Commit

Commit:

```text
load session by tenant and ID
require same uploader
hash supplied preview token and compare hashes in constant time
require unexpired Previewed status and no blocking errors
if session already committed:
  require the same idempotency key
  return existing batch
if tenant-scoped idempotency key already committed:
  require stored normalizedPayloadHash == current normalizedPayloadHash
  return existing batch
  otherwise return 409 Conflict
deserialize normalized payload
revalidate all reference data

begin transaction
  create one Draft batch
  create every journal and line through existing services
  link membership
  mark import session Committed with batch ID and idempotency key
commit
return created batch
```

If reference data changes after preview, mark the session Invalid and return
new issues. If commit fails, roll back every accounting record and mark the
session Failed outside the rolled-back transaction with a bounded error.

Build the normalized-payload hash from canonical business data, not workbook
bytes, ZIP metadata, row timestamps, source row numbers, or serializer accident.
Use fixed property order, normalized strings/codes and decimals, journals sorted
by client key, and lines sorted by client key plus immutable line number. This
allows a semantically identical re-preview to replay safely while ensuring the
same idempotency key cannot authorize a different batch.

### 14.6 Error workbook and export

The error workbook should include:

```text
Sheet
Row
Column
Value
Code
Severity
Message
```

The populated export should include current controls, journals, lines, review
outcomes, rejection reasons, posting runs, and posting timestamps.

## 15. Permissions, roles, workflow, and maker-checker

Add dedicated permissions:

```text
Finance.JournalBatches.View
Finance.JournalBatches.Create
Finance.JournalBatches.Edit
Finance.JournalBatches.Delete
Finance.JournalBatches.SubmitForApproval
Finance.JournalBatches.Approve
Finance.JournalBatches.Post
Finance.JournalBatches.Reverse
Finance.JournalBatches.Import
Finance.JournalBatches.Export
Finance.JournalBatches.Copy
```

Recommended duty split:

| Role family | Typical permissions |
|---|---|
| Finance user | View |
| Finance clerk | View, create, edit, delete Draft, import, export, copy |
| Accounts officer/senior accountant | Create, edit, submit, first-stage review where assigned |
| Finance manager | Approve where assigned, post, reverse |
| Financial controller | Final approval, post, reverse |
| Tenant/system administrator | Policy-granted permissions, still subject to workflow assignment and self-approval policy |

The RHEMA default workflow is:

```text
1. Accounts Officer Review
   roles: Accounts Officer or Senior Accountant

2. Finance Manager Approval
   role: Finance Manager

3. Financial Controller Final Approval
   role: Financial Controller
```

Adapt stage names and roles to the target organization. Preserve:

- one workflow instance for the batch;
- assignment checks on every review action;
- maker-checker/self-approval policy;
- per-item decisions at every stage;
- immutable decision history; and
- atomic synchronization of workflow, batch, item, and child-journal state.

## 16. Tenant/company isolation

Every read and mutation must fail closed:

- derive tenant/company from the authenticated context, not the request body;
- filter every batch, item, review, run, import, attachment, journal, account,
  period, workflow, and posting-event query;
- include tenant/company scope in unique indexes;
- resolve account numbers only within the tenant;
- bind import sessions to tenant and uploader;
- reject cross-tenant file-upload links;
- never use unrestricted `Find(id)`-style access for high-risk finance
  entities; and
- return “not found for this tenant” rather than revealing another tenant's
  record.

## 17. Audit and notifications

Record, as applicable:

```text
JournalBatchCreated
JournalBatchUpdated
JournalBatchDeleted
JournalBatchEntryAdded
JournalBatchEntryUpdated
JournalBatchEntryRemoved
JournalBatchValidated
JournalBatchSubmitted
JournalBatchWithdrawn
JournalBatchItemApproved
JournalBatchItemRejected
JournalBatchReviewStageFinalized
JournalBatchRejected
JournalBatchCopied
JournalBatchRejectedItemsCopied
JournalBatchImportPreviewed
JournalBatchImported
JournalBatchImportFailed
JournalBatchExported
JournalBatchPostingRunCreated
JournalBatchPartiallyPosted
JournalBatchPosted
JournalBatchPostingFailed
JournalBatchReversalCreated
JournalBatchReversed
JournalBatchReversalFailed
```

Audit context should include:

- expected/actual/approved/rejected/posted totals and counts;
- variance and currency;
- batch/item/journal IDs and numbers;
- actor and tenant;
- workflow instance, stage, and task;
- decisions and rejection reasons;
- content fingerprints;
- import session and file hash;
- posting run and posting-event IDs;
- source/reversal links; and
- failure/retry details.

Prefer a transactional audit/outbox mechanism for state-changing accounting
events.

Notify:

- maker on submission, rejection, review completion, posting completion/failure,
  and reversal completion/failure;
- assigned approvers through the workflow mechanism; and
- poster/controller when a batch is ready, if tenant policy enables it.

For posting notifications:

- enqueue or send only after commit;
- use stable deduplication IDs;
- prefer one batch-level summary when product requirements allow it;
- if individual journal-owner notifications are retained, send them only after
  the outer batch transaction commits; and
- treat notification failure as an operational delivery issue, not an
  accounting rollback.

## 18. Frontend experience

Recommended routes:

```text
/finance/journal-batches
/finance/journal-batches/new
/finance/journal-batches/{id}
/finance/journal-batches/import
```

### 18.1 Register

Show:

- batch number and description;
- period/book/currency;
- expected and actual debit;
- variance with prominent mismatch styling;
- entry count and posted count;
- derived display status;
- search;
- approval/posting filters;
- links to create, import, template, and detail.

### 18.2 Creation

Collect:

- description;
- open fiscal period;
- book;
- base control currency;
- expected debit total;
- optional expected journal count; and
- notes.

### 18.3 Detail/review/posting

Show:

- expected, actual, variance, entry count, and status summary cards;
- validation action and structured issues;
- member table with individual journal number, amount, review status, rejection
  reason, posting status, and actions;
- Draft journal create/edit/remove and attach-existing flows;
- one decision selector per pending item;
- required rejection-reason field;
- submit-all-current-stage-decisions action;
- checkboxes for finally approved Ready items;
- selected entry count and selected debit total;
- “Post selected” and “Select all ready”;
- posting-run history;
- copy all and copy rejected;
- export;
- full reversal action with reason/date confirmation; and
- source/reversal navigation.

For reversal batches, disable mixed decisions and partial posting in both UI and
server.

### 18.4 Import

Use a mandatory two-step experience:

```text
select .xlsx
-> preview
-> show counts/totals/issues
-> allow error workbook download
-> commit only when preview is valid and unexpired
-> navigate to the created Draft batch
```

### 18.5 Accessibility

At minimum:

- use real labels for all form controls;
- give icon-only buttons accessible names;
- use semantic headings and tables;
- provide status text in addition to color;
- announce validation/import outcomes;
- keep keyboard focus usable through review and import flows; and
- run automated serious/critical accessibility gates on register, create, and
  import pages.

## 19. Migration and backward compatibility

1. Add new tables and indexes without changing existing journal ownership.
2. Add a nullable/read-only journal-to-batch navigation or projection.
3. Add batch provenance fields to journal DTOs/read models.
4. Add lifecycle conflict guards to every standalone mutation path.
5. Register the new workflow entity type.
6. Register the journal-batch numbering definition.
7. Register services and permission policy mappings.
8. Add role grants and navigation.
9. Apply the migration through the application's normal deployment pipeline.
10. Do not backfill historical journals.

Regression requirements:

- standalone journal create/edit/submit/approve/post/reverse remains unchanged;
- subledger/system-generated journals remain standalone;
- period close still detects unposted member journals through ordinary journal
  state;
- ledger/report queries continue to use posted journals and ledger entries;
- existing journal numbers remain unchanged; and
- deleting/detaching a Draft batch item leaves the underlying eligible journal
  as a standalone Draft unless product policy says otherwise.

## 20. Recommended implementation sequence

### Phase 0: analysis and policy confirmation

- Complete section 5.
- Confirm control currency, workflow stages, partial posting, role split,
  maximum batch size, and v1 exclusions.
- Identify the authoritative transaction owner for posting/reversal/import.

### Phase 1: domain and database foundation

- Add statuses and entities.
- Add constraints, indexes, row versions, tenant scope, and migration.
- Add journal provenance navigation/projection.
- Add numbering definition.

Exit gate: schema applies cleanly; uniqueness and row-version behavior are
tested on the real database.

### Phase 2: Draft and control service

- Create/list/detail/update/delete.
- Add/create/update/remove member journals.
- Shared eligibility and validation.
- Attachments.
- Standalone journal ownership guards.

Exit gate: control mismatches and cross-tenant/membership violations are
blocked.

### Phase 3: workflow and per-entry review

- Register workflow entity and stages.
- Submit/withdraw transaction.
- Fingerprints and submission snapshot.
- Per-stage all-item decisions.
- Final partial/full approval outcomes.
- Permissions, role seeds, and audits.

Exit gate: one workflow, assignment checks, immutable history, and child states
remain consistent.

### Phase 4: posting runs

- Transaction-aware child posting path.
- Durable run/run-item model.
- Idempotency and concurrency claims.
- Atomic selected-run posting.
- Post-commit notification/outbox.
- Failure recovery state.

Exit gate: injected later-item failure rolls back the whole selected run, and
replay cannot duplicate ledger events.

### Phase 5: spreadsheet exchange

- Replace commercial workbook dependency if required.
- Template and lookups.
- pre-load package security inspection;
- preview normalization/validation;
- tenant/user token and expiry;
- transactional idempotent commit;
- error workbook and export.

Exit gate: malicious packages are rejected before workbook parsing and a valid
workbook round-trips.

### Phase 6: copy and full reversal

- Copy all/rejected.
- Atomic reversal construction.
- uniform reversal approval;
- one complete reversal posting run;
- original/reversal link completion.

Exit gate: construction and posting failures leave no partial reversal state.

### Phase 7: frontend and release hardening

- Register/create/detail/import UI.
- Existing journal banner/disabled actions.
- Accessibility.
- large-batch performance.
- real-database transaction/concurrency tests.
- production CI release gate.
- client UAT.

## 21. Automated test matrix

### 21.1 Domain/service

- unique tenant-scoped batch number;
- correct derived totals/counts/variance;
- independent journal balancing;
- control-total and expected-count mismatches;
- wrong tenant/period/book/currency;
- posted, reversed, recurring, generated, or already-batched journal rejection;
- open/closed/locked period rules;
- Draft-only mutation;
- stale row version;
- one workflow and no child workflows;
- fingerprint generation/change detection;
- every eligible item requires a decision;
- rejection requires a reason;
- intermediate versus final workflow stages;
- mixed outcomes -> PartiallyApproved;
- all rejected -> Rejected;
- copy all/rejected produces clean Draft;
- every direct child lifecycle bypass returns conflict.

### 21.2 Posting

- selected approved items post;
- unselected approved items remain Ready;
- rejected/pending/posted items cannot be selected;
- same idempotency key + same selection returns original run;
- same key + different selection conflicts;
- overlapping concurrent runs have one winner per item/key;
- the losing posting run's conditional claim affects zero/fewer rows and returns
  conflict without posting any selected item;
- later-item failure rolls back earlier items and ledger artifacts in that run;
- earlier successful runs remain posted;
- a failed later run preserves `PartiallyPosted` header progress and records the
  attempt as a failed posting run;
- posting remaining approved items -> Posted;
- notifications occur only after commit;
- notification failure does not undo committed accounting.

### 21.3 Reversal

- exactly one inverse journal per posted source item;
- amounts, signs, dates, accounts, rates, currencies, dimensions, descriptions,
  and source links are correct;
- rejected source items excluded;
- PartiallyPosted source blocked;
- pre-reversed source item blocked;
- concurrent reversal claims have one winner;
- construction failure restores source state and removes all artifacts;
- mixed review blocked;
- partial reversal posting blocked;
- posting failure rolls back the complete reversal run;
- success links every item/journal and marks source/reversal Reversed.

### 21.4 Spreadsheet

- required version/sheets/columns;
- valid template preview;
- formula rejection;
- VBA rejection before ClosedXML or equivalent parser;
- external relationship rejection;
- invalid ZIP/signature/path;
- compression-ratio and expanded-size rejection;
- excessive workbook/journal/line rows;
- unknown/cross-tenant/non-postable account;
- duplicate/unknown client journal key;
- invalid date/period/currency;
- too few or duplicate lines;
- unbalanced journal;
- control mismatch;
- preview creates no accounting rows;
- token bound to tenant/user and expires;
- reference data revalidated at commit;
- retry commit idempotent;
- commit failure leaves no partial batch;
- error workbook content;
- template -> preview -> commit -> export round trip.

### 21.5 API/security

- every action maps to the intended dedicated permission;
- workflow assignment is checked independently of permission;
- reads and mutations are tenant-filtered;
- imports and attachments cannot cross tenants/users;
- conflicts return stable error codes;
- filters and pagination are stable.

### 21.6 Frontend

- create controlled batch;
- control/variance display;
- validation error display;
- per-entry decisions and rejection reason;
- partial posting selection and run history;
- status-dependent action visibility;
- batch-owned journal banner;
- import preview/commit/error download;
- reversal confirmation;
- keyboard flow and no serious/critical accessibility violations.

### 21.7 Performance

The RHEMA baseline loads a 1,000-entry, 2,000-line batch detail within 20 seconds
in the test environment. Set a target-specific budget based on infrastructure
and user needs. Test realistic approval and posting selection sizes, not just
read performance.

## 22. Real-database and CI release gate

Mocked repositories and in-memory databases do not prove:

- transactions;
- filtered/partial unique indexes;
- row versions;
- isolation/locking;
- concurrent idempotency;
- reversal claims; or
- provider-specific query behavior.

Create disposable databases using a CI-only connection such as:

```text
RHEMA_TEST_SQLSERVER
```

The RHEMA GitHub Actions job:

1. starts an isolated SQL Server 2022 service container;
2. sets `RHEMA_TEST_SQLSERVER` using SQL authentication;
3. runs the two real-SQL transaction/concurrency gates;
4. writes a TRX test result;
5. fails if either test is skipped/not executed;
6. fails unless exactly two tests execute and pass;
7. uploads test results; and
8. makes production deployment depend on the gate.

Replicate the behavior, not necessarily the variable name or SQL Server. The
critical rule is that a missing database must fail the release gate rather than
silently skipping it.

Minimum real-database gates:

- rolling back reversal construction restores the source and leaves no reversal
  batch;
- two concurrent inserts with the same posting idempotency key produce one
  winner;
- two concurrent posting runs conditionally claiming the same Ready item produce
  exactly one item-level winner;
- two concurrent reversal claims for one source produce one winner; and
- ideally, execute the actual service-level posting and reversal orchestration
  with injected failure, not only raw database constraints.

Do not declare the repository production-ready while unrelated compile failures
prevent the full CI pipeline from executing. Either repair current tests or
explicitly quarantine tests for removed architecture with a migration register
and active replacement coverage. Never use broad wildcard exclusions that hide
new test failures.

## 23. Recommended UAT script

1. Create `JB-2026-00001` for an open period with expected debit 10,000 in base
   currency and expected count 3.
2. Add three independently balanced journals totalling 9,750.
3. Confirm actual debit and credit are 9,750 and variance is -250.
4. Confirm submission is blocked.
5. Correct the batch to total 10,000.
6. Submit and confirm one batch workflow starts and all financial content is
   frozen.
7. Confirm direct edit/approve/post/reverse on a member journal is blocked and
   links back to the batch.
8. At the first stage, approve entries 1 and 2 and reject entry 3 with a reason.
9. Complete the remaining stages for entries 1 and 2.
10. Confirm the batch is PartiallyApproved and entry 3 remains Rejected.
11. Post only entry 1; confirm the run is Posted, batch is PartiallyPosted, and
    entry 2 remains Ready.
12. Replay the same idempotency key and confirm no duplicate posting.
13. Post entry 2 and confirm batch is Posted.
14. Copy rejected entries and confirm a new Draft contains a clean copy of
    entry 3 with new numbers and no inherited review/posting history.
15. Create a full reversal batch, approve it uniformly, and post it in one run.
16. Confirm both source posted entries are reversed, links are complete, and
    source batch is Reversed.
17. Preview a workbook with a bad account and control mismatch; download errors.
18. Correct, preview, and commit it exactly once.
19. Export the imported batch and verify controls, lines, decisions, runs, and
    statuses.
20. Create/post/reverse a standalone journal to confirm backward compatibility.

## 24. Statistical journal control totals

A statistical journal records non-monetary quantities such as:

- labour hours;
- employee count;
- occupied beds;
- machine hours;
- production units; or
- floor area.

Its control is a quantity, not money:

```text
Expected statistical units: 1,250 hours
Actual statistical units:   1,248 hours
Variance:                       -2 hours
```

Do not mix currency and statistical quantities in one control total.

Recommended decision:

- monetary GL batch -> `ExpectedDebitTotal` in base currency;
- unit/statistical accounting batch -> `ExpectedUnitTotal` plus required unit
  of measure; and
- if the target posts non-monetary units to special GL accounts, confirm that
  model before adding `BatchType=Statistical`.

RHEMA ERP already has a separate Unit Accounting journal, so statistical
journal batching was not added to monetary Journal Batch v1.

## 25. Recurring templates, scheduled generation, and auto-post

These capabilities were intentionally separated because they require more than
copying a batch on a timer.

### Version 1.1: recurring batch templates

Indicative effort: 8-12 engineering days.

Include:

- versioned template header and journal/line templates;
- weekly, monthly, period-end, start/end, and maximum-occurrence rules;
- business-day adjustment;
- future-date preview;
- activate/pause;
- manual “generate now”;
- idempotent Draft generation;
- occurrence history; and
- no copied approvals, posting links, or prior occurrence IDs.

### Version 1.2: durable scheduled generation

Indicative effort: 10-15 engineering days.

Include:

- durable background worker;
- database-backed occurrence claiming;
- unique tenant/template/occurrence key;
- duplicate prevention during failover;
- retries/backoff;
- dead-letter status;
- inspect/retry/cancel/regenerate operator controls;
- metrics and alerts; and
- explicit tenant time-zone/daylight-saving behavior.

### Version 1.3: controlled auto-submit and auto-post

Indicative effort: 12-20 engineering days.

Include:

- opt-in disabled-by-default policy per template;
- dedicated non-interactive tenant-scoped system identity;
- separate enable/manage permissions;
- maker-checker rules;
- maximum amount and posting windows;
- open-period, account, rate, dimension, control, and unresolved-item
  revalidation;
- stable idempotency keys;
- transactional outbox;
- exception queue rather than infinite retries;
- operator recovery; and
- complete policy/template/occurrence/system-actor audit history.

Auto-post must not bypass approval unless the client explicitly approves a
straight-through policy for that template.

Before implementation, the client must decide:

1. which templates, if any, may bypass human approval;
2. who may enable or change auto-post;
3. permitted posting time zone/windows;
4. behavior when a period closes;
5. retry and escalation thresholds; and
6. the operational owner of the exception queue.

## 26. Production readiness checklist

### Domain and accounting

- [ ] Each member journal independently balances.
- [ ] Control total and optional count are server-enforced.
- [ ] Only eligible manual GL journals can join v1.
- [ ] Existing posting engine is reused.
- [ ] Standalone journals remain compatible.

### Integrity and concurrency

- [ ] Tenant/company scope is on all queries, entities, and relevant indexes.
- [ ] One active batch membership per journal is database-enforced.
- [ ] Row-version/concurrency conflicts are handled.
- [ ] Fingerprints freeze submitted financial content.
- [ ] Posting run and import idempotency keys are database-enforced.
- [ ] Concurrent posting/reversal claims have one winner.

### Transactions

- [ ] Submission is atomic with workflow linkage or uses a recoverable outbox.
- [ ] Every selected posting run is atomic.
- [ ] Reversal construction is one outer transaction with no nested transaction
      ownership.
- [ ] Reversal posting is one complete run.
- [ ] Import commit is atomic.
- [ ] Failure-recovery state is persisted outside rolled-back accounting work.

### Security

- [ ] Dedicated permissions are registered and tested.
- [ ] Workflow assignment is checked.
- [ ] Maker-checker/self-approval policy is preserved.
- [ ] Child standalone lifecycle bypasses are blocked.
- [ ] Import and attachments cannot cross tenant/user boundaries.

### Spreadsheet

- [ ] No commercial EPPlus dependency remains unless intentionally licensed.
- [ ] `.xlsx` signature/package validation happens before parser load.
- [ ] VBA, formulas, and external links are rejected.
- [ ] ZIP-bomb and row limits are enforced.
- [ ] Preview creates no accounting rows.
- [ ] Raw preview bearer tokens are never persisted.
- [ ] Commit is user/tenant/token/expiry/idempotency bound.
- [ ] Import idempotency is bound to a canonical normalized-payload hash and
      payload mismatch returns conflict.
- [ ] Error workbook and populated export work.

### Side effects and audit

- [ ] Posted notifications occur only after commit.
- [ ] Notification failure cannot roll back committed accounting.
- [ ] Audit includes controls, decisions, runs, imports, and reversal links.

### Release gates

- [ ] Unit/service tests pass.
- [ ] Real-database rollback, row-version, unique-index, and concurrency tests
      pass.
- [ ] Real-database tests cannot silently skip in release CI.
- [ ] Successful import/commit/export round trip passes.
- [ ] Malicious workbook tests pass.
- [ ] 1,000-entry or target-size performance gate passes.
- [ ] Browser workflow and accessibility gates pass.
- [ ] Client UAT completes.

## 27. Definition of done

The feature is complete only when:

1. A permitted user can create a unique Draft batch.
2. It can contain multiple independently numbered and balanced manual journals.
3. API and UI show expected/actual debit, actual credit, variance, entry count,
   and line count.
4. Invalid controls or members block submission.
5. Submission freezes content and starts exactly one batch workflow.
6. Assigned approvers decide every eligible item at every stage with immutable
   history and rejection reasons.
7. Mixed decisions produce PartiallyApproved.
8. Rejected entries never post and can be copied to a clean Draft.
9. Child lifecycle actions cannot bypass the batch.
10. Any post-submission content change blocks posting.
11. Any valid selected subset of finally approved items can be posted.
12. Every run is atomic and idempotent.
13. The batch correctly becomes PartiallyPosted or Posted.
14. Full linked reversal creates, approves, and atomically posts one inverse
    journal for every posted source item.
15. Versioned template, secure preview, idempotent commit, error workbook, and
    populated export work.
16. Copy all/rejected produces clean numbers and no inherited lifecycle state.
17. Posted journals retain normal journal and posting-event provenance.
18. Standalone journal behavior is unchanged.
19. Tenant isolation, permissions, workflow assignment, concurrency, audit,
    notifications, and release CI are verified.

## 28. RHEMA ERP reference implementation map

The source implementation used to derive this playbook is:

```text
Core domain
  src/ErpSystem.Core/Entities/Finance/JournalBatch.cs
  src/ErpSystem.Core/DTOs/Finance/JournalBatchDtos.cs
  src/ErpSystem.Core/Interfaces/Finance/IJournalBatchService.cs
  src/ErpSystem.Core/Entities/Finance/JournalEntry.cs
  src/ErpSystem.Core/DTOs/Finance/JournalEntryDtos.cs
  src/ErpSystem.Core/Interfaces/Finance/IJournalEntryService.cs

Database
  src/ErpSystem.Data/Configuration/JournalBatchConfiguration.cs
  src/ErpSystem.Data/Migrations/20260728211925_AddJournalBatches.cs
  src/ErpSystem.Data/ApplicationDbContext.cs

Backend
  src/ErpSystem.Api/Services/Finance/GL/JournalBatchService.cs
  src/ErpSystem.Api/Services/Finance/GL/JournalBatchSpreadsheetService.cs
  src/ErpSystem.Api/Services/Spreadsheets/SpreadsheetSecurityInspector.cs
  src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs
  src/ErpSystem.Api/Controllers/Finance/JournalBatchController.cs
  src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs
  src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs
  src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs
  src/ErpSystem.Api/Services/DatabaseSeedingService.cs

Shared configuration
  src/ErpSystem.Shared/FinancePermissions.cs
  src/ErpSystem.Shared/FinanceAuditEvents.cs
  src/ErpSystem.Core/Interfaces/Numbering/IDocumentNumberingService.cs
  src/ErpSystem.Data/Services/DocumentNumberingService.cs

Frontend
  frontend/src/types/journal-batches.ts
  frontend/src/services/finance/journal-batch-data.service.ts
  frontend/src/app/finance/journal-batches/page.tsx
  frontend/src/app/finance/journal-batches/new/page.tsx
  frontend/src/app/finance/journal-batches/[id]/page.tsx
  frontend/src/app/finance/journal-batches/import/page.tsx
  frontend/src/app/finance/journal-entries/[id]/page.tsx

Tests and CI
  tests/ErpSystem.Api.Tests/Services/Finance/JournalBatchServiceTests.cs
  tests/ErpSystem.Api.Tests/Services/Finance/JournalBatchSpreadsheetServiceTests.cs
  tests/ErpSystem.Api.Tests/Services/Finance/JournalBatchSqlServerReleaseGateTests.cs
  tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs
  e2e-tests/tests/journal-batches.spec.ts
  .github/workflows/ci-cd.yml

Planning and deferred scope
  plans/FR-GL-007-journal-batches-implementation-plan.md
  plans/FR-GL-007-journal-batch-subsequent-versions.md
```

RHEMA-specific technologies:

```text
Backend: ASP.NET Core 8 / C#
ORM/database: Entity Framework Core 8 / SQL Server
Frontend: Next.js / React / TypeScript
Workbook: ClosedXML 0.105.1
Package inspection: DocumentFormat.OpenXml 3.5.1
Browser tests: Playwright + axe-core
CI: GitHub Actions + SQL Server 2022 service container
```

These technologies are examples, not requirements.

## 29. Final handoff format for the target AI agent

When finished, report:

```markdown
## Journal Batch Replication Result

### Adaptation decisions
- Existing journal validator reused:
- Existing posting engine reused:
- Transaction owner:
- Workflow model:
- Tenant/company model:
- Spreadsheet implementation:
- Deviations from this playbook and reasons:

### Implemented
- Domain/schema:
- Services:
- API:
- Permissions/workflow:
- Frontend:
- Audit/notifications:
- Import/export:
- Copy/reversal:

### Verification
- Unit/service:
- Real database:
- Concurrency/idempotency:
- Spreadsheet security and round trip:
- Frontend/browser/accessibility:
- Performance:
- Full repository CI:

### Remaining decisions or risks
- ...

### Deferred versions
- Recurring templates:
- Scheduled generation:
- Auto-post:
- Statistical batches:
```

Do not claim production readiness based only on compilation, mocked posting, an
in-memory database, or a happy-path UI test.
