# HR Bulk Operations Catalogue — Where Single-Item Actions Need a Bulk Equivalent

**Generated:** 2026-08-31
**Purpose:** A comprehensive, entity-by-entity list of HR actions that today only operate on one
record at a time, so each can be evaluated for a bulk/multi-select equivalent — reducing the
number of repetitive clicks a manager, HR officer, or admin has to make to get through a queue.

---

## 0. Relationship to the other docs in this folder

| Document | Focus |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Money-carrying entities and their Finance interaction. |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Engineering/business patterns from other modules HR hasn't adopted. |
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | What reports HR needs and how to build them. |
| **This document** | Which single-item HR actions need a bulk/multi-select equivalent, and how to build one safely. |

**One overlap worth naming up front:** §5 below (audit logging of the bulk act itself) is the same
gap this folder's cross-module sweep already flagged for semantic auditing generally (no
`IHrAuditService`). A bulk-action audit event is a natural first use case for that service once it
exists, rather than something to solve twice.

---

## 1. Executive summary

**HR already has bulk operations — just not where the queues are busiest.** Confirmed, working
bulk endpoints exist for: Payroll setup data (7 endpoints — bonus rules/exceptions, tax reliefs,
component exceptions, promotion arrears, overtime summaries, contribution opening balances),
Recruitment (7 endpoints — bulk shortlist/reject/move-stage/notify, auto-shortlist), Training
(bulk nominate, bulk record completion, bulk needs-assessment), Orientation (bulk enroll),
Competency (batch assess), Appraisal (bulk-assign cycle templates), and Attendance (bulk
acknowledge alerts, bulk import). That is real, proven infrastructure — the request/response
shapes, the "best-effort with per-item result" transaction model, and even a working frontend
multi-select pattern all already exist somewhere in this codebase.

**What's missing is coverage of the highest-volume queues.** A full enumeration of HR's ~40
controllers found **296+ single-item action endpoints**, of which only a handful have a bulk
sibling. Cross-referencing against the frontend confirms the gap is exactly where it hurts most:
**zero HR list/queue pages have checkbox multi-select today**, even though several are explicitly
manager- or HR-officer-facing approval queues that can run into the dozens or hundreds of pending
items (a manager with 50+ direct reports approving leave one row at a time; a claims team
adjudicating a daily batch of medical claims one row at a time).

**The single most important design rule, stated once so it isn't relearned per bulk endpoint
(§4.1 has the detail):** a bulk endpoint must call the exact same per-item service method /
workflow adapter as the single-item action, in a loop — never write directly to the database to
"batch" the change. This is the same principle this folder's other docs already apply to Finance
integration ("do not invent a parallel mechanism") — here it means a bulk-approve must still run
every authorization check, every workflow-engine transition, and every notification a single
approve would have triggered, for every item in the batch.

---

## 2. What already exists (do not rebuild these)

| Module | Operation | Endpoint | Request shape | Response shape |
|---|---|---|---|---|
| Payroll | Bonus rules | `POST bonus-rules/bulk` | Full DTOs + `IsSelected` flag | List of saved DTOs |
| Payroll | Bonus exceptions | `POST bonus-exceptions/bulk` | Full DTOs + overrides | List of saved DTOs |
| Payroll | Tax reliefs | `POST tax-reliefs/bulk` | Full DTOs | List of saved DTOs |
| Payroll | Component exceptions | `POST component-exceptions/bulk` | Full DTOs | List of saved DTOs |
| Payroll | Promotion arrears | `POST promotion-arrears/bulk` | Full DTOs | List of saved DTOs |
| Payroll | Overtime summaries | `POST overtime-summaries/bulk` | Full DTOs | List of saved DTOs |
| Payroll | Contribution opening balances | `POST contribution-opening-balances/bulk` | Full DTOs | List of saved DTOs |
| Recruitment | Bulk shortlist | `POST .../bulk-shortlist` | IDs + notes | `RecruitmentBulkOperationResultDto` (Succeeded/Skipped/per-item Results) |
| Recruitment | Bulk reject (application) | `POST job-applications/bulk-reject` | IDs + reason | Same shape |
| Recruitment | Bulk reject (pipeline stage) | `POST .../bulk-pipeline-reject` | IDs + reason | Same shape |
| Recruitment | Bulk move stage | `POST application-pipeline/bulk-move` | IDs + targetStageId | Same shape |
| Recruitment | Auto-shortlist | `POST .../auto-shortlist` | VacancyId + score threshold | Same shape |
| Recruitment | Notify shortlisted/rejected | `POST .../notify-shortlist` \| `notify-reject` | VacancyId | Same shape |
| Training | Bulk nominate | `POST training-nominations/bulk` | DTOs array | Counts + skip list |
| Training | Bulk record completion | `POST training/schedule/{id}/bulk-record` | Items array (per-person date/status) | `{createdCount, requestedCount, skipped[]}` — **the cleanest existing pattern, see §4.2** |
| Training | Bulk needs-assessment | `BulkCreateNeedsAssessment` | — | Counts + skip list |
| Orientation | Bulk enroll | `BulkEnrollOrientations` | — | List of enrollment DTOs |
| Competency | Batch assess | `BatchAssessCompetency`, `BulkSetPositionCompetencies` | — | Custom result DTOs |
| Appraisal | Bulk assign cycle templates | `BulkAssignCycleTemplates` | — | `IActionResult` |
| Attendance | Bulk acknowledge alerts | `BulkAcknowledgeAlerts` | — | Count |
| Attendance | Bulk import | `StaffBulkAttendanceImport` | File | Import record |
| Separations | Repair disciplinary orphans | `POST separations/repair/disciplinary-orphans` | `dryRun` flag | Repair summary |

**Frontend precedent already proven:**
- **`pickerSelectedIds` picker pattern** — used identically across 6 Payroll setup pages
  (bonus exceptions, allowances/deductions, opening balance, overtime summary, promotion arrears,
  tax relief): a `Set`/array of selected row IDs, a tri-state "select all" checkbox, one save call.
- **`BulkCompletionPanel`** (Training) — the most complete example: checkbox selection →
  one API call with an items array → **granular per-item results shown back to the user**,
  including *why* an item was skipped (e.g. "Already has completion record"). This is the pattern
  to copy for anything more complex than a plain approve/reject.
- **Recruitment pipeline page** — checkbox column + a bulk-action toolbar (`bulkAction` state of
  `'move' | 'reject'`) — the closest existing example of an approval-queue-style bulk UI.
- **`AdminDataTable`** (`frontend/src/components/admin/data-table.tsx`) — a generic table
  component with a `selectable`/`onSelectionChange` prop, currently only used in admin screens,
  not HR. Worth evaluating as the base for a shared HR bulk-action table rather than building a
  new one from scratch.

---

## 3. Master list — single-item actions and their bulk-need assessment

Legend — **Bulk today?**: ✅ yes · 🔲 no. **Priority**: 🔴 high (confirmed high-volume queue,
build first) · 🟡 medium (real benefit, lower volume/frequency) · 🟢 low (rarely batched in
practice, or inherently one-at-a-time by nature).

### 3.1 Leave

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit | `POST leaves/{id}/submit` | 🔲 | 🟢 | Employee self-service, one request at a time by nature |
| **Approve** | `POST leaves/{id}/approve` | 🔲 | 🔴 | Manager queue — the #1 candidate found; a manager with many reports approves dozens at a time |
| **Reject** | `POST leaves/{id}/reject` | 🔲 | 🔴 | Same queue, same priority |
| Cancel | `POST leaves/{id}/cancel` | 🔲 | 🟢 | Usually one-off |
| Recall | `POST leaves/{id}/recall` | 🔲 | 🟢 | Employee-initiated, one-off |
| Approve/reject (leave plan) | `POST leave-plans/{id}/approve` \| `/reject` | 🔲 | 🟡 | Same queue shape as leave requests, lower volume |
| Approve/reject (encashment) | `PATCH leave-encashments/{id}/approve` \| `/reject` | 🔲 | 🟡 | Periodic (e.g. year-end), can spike in volume |
| Deactivate leave type | `POST leave-types/{id}/deactivate` | 🔲 | 🟢 | Admin config, rare |

### 3.2 Attendance & shift

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/reject regularization | `POST attendance-regularizations/{id}/approve` \| `/reject` | 🔲 | 🟡 | Monthly-cycle spikes plausible |
| Verify daily attendance | `POST daily-attendance/{id}/verify` | 🔲 | 🟡 | Supervisor confirming a shift's worth of records |
| Acknowledge alert | `POST attendance-alerts/{id}/acknowledge` | ✅ (bulk exists) | — | Already covered |
| Recalculate monthly summary | `POST monthly-attendance-summaries/{id}/recalculate` | 🔲 | 🟡 | Admin/payroll-period trigger — plausible to want "recalculate for whole department" |
| Assign/unassign shift | `POST shift-assignments/{id}/assign` \| `/unassign` | 🔲 | 🟡 | Roster-building is inherently multi-employee |
| Confirm/reject client timesheet | `POST client-timesheet-confirmations/{id}/confirm` \| `/reject` | 🔲 | 🟢 | Client-side action, typically per engagement |
| Approve/reject consultant timesheet | `POST consultant-timesheets/{id}/approve` \| `/reject` | 🔲 | 🟡 | Periodic batch by nature (all consultants, one period) |
| Approve/reject overtime request | `POST overtime-requests/{id}/approve` \| `/reject` | 🔲 | 🟡 | Can spike around period-end |
| Approve/reject remote-work request | `POST remote-work-requests/{id}/approve` \| `/reject` | 🔲 | 🟡 | Manager queue, moderate volume |

### 3.3 Recruitment & staff requisitions

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Shortlist application | `POST job-applications/{id}/shortlist` | 🔲 | 🔴 | Bulk-reject exists; bulk-shortlist of individual applications (not via the pipeline bulk-move) does not — check for gap |
| Reject application | `POST job-applications/{id}/reject` | ✅ (`bulk-reject`) | — | Covered |
| Move to stage | `POST job-applications/{id}/move-to-stage` | ✅ (pipeline `bulk-move`) | — | Covered |
| Withdraw | candidate-initiated | 🔲 | 🟢 | Candidate self-service |
| Publish/close/reopen vacancy | `POST job-vacancies/{id}/publish` \| `/close` \| `/reopen` | 🔲 | 🟢 | Rare, per-vacancy administrative act |
| **Shortlist approval** | (per-vacancy card, no queue page) | 🔲 | 🟡 | No queue page exists yet — building the bulk action and the queue page are the same piece of work; see `HR-REPORTS-CATALOGUE.md` §3.5 recruitment-pipeline register for the same missing page |
| Submit/approve/reject/recall requisition | `POST staff-requisitions/{id}/submit` \| `/approve` \| `/reject` \| `/recall` | 🔲 | 🟡 | HR/finance sign-off queue, batchable at headcount-planning time |
| Hold/cancel/fulfill requisition | `POST staff-requisitions/{id}/hold` \| `/cancel` \| `/fulfill` | 🔲 | 🟢 | Individual outcome per role |
| Link requisition to vacancy | `POST staff-requisitions/{id}/link-vacancy` | 🔲 | 🟢 | 1:1 act |

### 3.4 Performance, appraisal & PIP

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Start appraisal | `POST performance-appraisals/{id}/start` | 🔲 | 🔴 | **Opening a whole cycle for a department is inherently a bulk act** — starting 100+ individual appraisals one at a time at cycle-open is exactly the scenario the cross-module sweep's phase-gate discussion (see `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §7.4) already flags |
| Manager-assess / calculate score / add criterion score | `POST .../manager-assess` \| `/calculate-score` \| `/add-criterion-score` | 🔲 | 🟢 | Inherently per-employee, manager-specific content |
| **HR review / sign-off** | (implied HR-review queue) | 🔲 | 🔴 | Confirmed by frontend research as a 100+-item cycle-batch queue with no bulk support |
| Submit/approve/reject/recall PIP | `POST performance-improvement-plans/{id}/submit` \| `/approve` \| `/reject` \| `/recall` | 🔲 | 🟡 | Lower volume than appraisals but a real HR queue |
| Approve outcome recommendation | `POST appraisal-outcome-recommendations/{id}/approve` | 🔲 | 🟡 | End-of-cycle batch moment |
| Submit peer evaluation | `POST peer-evaluations/{id}/submit` | 🔲 | 🟢 | Individual voice, not a queue action |
| Delete calibration session / check-in / peer nomination | `DELETE .../{id}` | 🔲 | 🟢 | Admin cleanup, rarely more than one at a time |

### 3.5 Probation

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Extend probation | `POST probations/{id}/extend` | 🔲 | 🟢 | Individual decision per employee |
| Submit/acknowledge/complete review | `POST probations/reviews/{id}/submit` \| `/acknowledge` \| `/complete` | 🔲 | 🟢 | Personal to the employee/reviewer |
| Confirm/terminate | `POST probations/{id}/confirm` \| `/terminate` | 🔲 | 🟢 | High-consequence, should probably stay deliberately single-item even if a cohort shares a confirmation date |

### 3.6 Staff movements (transfers, promotions, secondments)

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit | `POST staff-movements/{id}/submit` | 🔲 | 🟢 | Requester-initiated |
| **Approve** | `POST staff-movements/{id}/approve` | 🔲 | 🔴 | Confirmed HIGH-volume approval queue by frontend research; org-restructuring events can move many employees at once |
| Reject / recall / cancel | `POST staff-movements/{id}/reject` \| `/recall` \| `/cancel` | 🔲 | 🟡 | Same queue as approve, lower frequency of use |
| Respond (employee acceptance) | `POST staff-movements/{id}/respond` | 🔲 | 🟢 | Employee-personal act |
| Handover / return | `POST staff-movements/{id}/handover` \| `/return` | 🔲 | 🟢 | Individual, checklist-driven |

### 3.7 Staff discipline

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Schedule hearing / record hearing / appeal / close | `POST .../schedule-hearing` \| `/record-hearing` \| `/appeal` \| `/close` | 🔲 | 🟢 | Each case is legally distinct — deliberately **not** a good bulk candidate; due-process fairness argues for individual handling. Listed here to confirm it was considered, not overlooked. |
| Delete case | `DELETE .../{id}` | 🔲 | 🟢 | Admin cleanup |

### 3.8 Employee relations & grievances

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Escalate / withdraw | `POST employee-relations/{id}/escalate` \| `/withdraw` | 🔲 | 🟢 | Subject-employee-initiated, case-specific |
| **Assign responder** | `POST employee-relations/{id}/assign` | 🔲 | 🟡 | HR triage action — plausible to assign several new cases to a responder in one pass |
| Respond | `POST employee-relations/{id}/respond` | 🔲 | 🟢 | Substantive per-case testimony, should stay individual |
| HR interpretation | `POST employee-relations/{id}/hr-interpretation` | 🔲 | 🟢 | Same reasoning as Discipline — a considered, case-specific judgement |
| Add/remove party | `POST employee-relations/{id}/parties` | 🔲 | 🟢 | Per-case |

**Note:** confirmed by frontend research as a HIGH-priority queue page (paged, 25/page, "Waiting
on HR" metric on the HR home page) — but most of its *actions* are individually substantive
(escalation responses, HR interpretations) rather than administrative. The bulk opportunity here
is triage (**Assign**), not the substantive decisions.

### 3.9 Training & development

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit/approve/reject nomination | `POST training-nominations/{id}/submit` \| `/approve` \| `/reject` | 🔲 (create is bulk; approve/reject are not) | 🟡 | Bulk nominate exists; bulk-*approving* a batch of nominations does not |
| Approve/reject/submit training request | `POST training-requests/{id}/approve` \| `/reject` \| `/submit` | 🔲 | 🟡 | Same shape |
| Approve/reject/publish/complete schedule | `POST training-schedules/{id}/approve` \| `/reject` \| `/publish` \| `/complete` | 🔲 | 🟢 | Per-course-run, moderate volume |
| Verify certificate | `POST training-certificates/{code}/verify` | 🔲 | 🟢 | One-off lookup by nature |

### 3.10 Payroll & compensation

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Close pay period | `POST pay-periods/{id}/close` | 🔲 | 🟢 | One period at a time by design (ties to the Finance sweep's proposed period-lock, §4.2 of `HR-FINANCE-ENTITY-SWEEP.md`) |
| Deactivate pay component | `POST pay-components/{id}/deactivate` | 🔲 | 🟢 | Rare admin act |
| **Approve/reject salary review proposal** | `POST salary-review-proposals/{id}/approve` \| `/reject` | 🔲 | 🔴 | Annual salary-review cycles are the textbook bulk-approval scenario — potentially hundreds of proposals reviewed in a short window |
| Approve/reject employment action proposal | `POST employment-action-proposals/{id}/approve` \| `/reject` | 🔲 | 🟡 | Same shape, typically lower volume than salary review |

### 3.11 Goals

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit/approve/reject employee goal | `POST employee-goals/{id}/submit` \| `/approve` \| `/reject` | 🔲 | 🔴 | Same cycle-batch shape as appraisals — goal-setting season means a manager approving goals for their whole team at once |
| Delete unit/company/team goal | `DELETE .../{id}` | 🔲 | 🟢 | Admin cleanup |

### 3.12 Assets

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| **Assign asset** | `POST assets/{id}/assign` | 🔲 | 🟡 | Onboarding a cohort (e.g. new-hire laptops) is a natural batch moment |
| Transfer / submit-transfer / recall-transfer | `POST assets/{id}/transfer` \| `/submit-transfer` \| `/recall-transfer` | 🔲 | 🟢 | Typically one asset, one move |
| Dispose | `POST assets/{id}/dispose` | 🔲 | 🟡 | End-of-life batches (e.g. annual laptop refresh) are plausible |
| Acknowledge receipt | `POST assets/{id}/acknowledge` | 🔲 | 🟢 | Employee-personal act |
| **Approve requisition** | `POST assets/requisition/{id}/approve` | 🔲 | 🟡 | HR/procurement approving a batch of requests |

### 3.13 Separations

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit/approve/reject | `POST separations/{id}/submit` \| `/approve` \| `/reject` | 🔲 | 🟢 | Each separation is legally/financially distinct (final settlement, see the Finance sweep) — deliberately not a strong bulk candidate despite being high-stakes |
| Notice decision / complete | `POST separations/{id}/notice-decision` \| `/complete` | 🔲 | 🟢 | Same reasoning |
| Repair disciplinary orphans | — | ✅ (bulk exists, `dryRun` param) | — | Already covered, and a good template for a "preview before commit" bulk pattern (§4.3) |

### 3.14 Awards & recognition

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/confer nomination | (per-nomination workflow) | 🔲 | 🟡 | Confirmed by frontend research: seasonal (annual awards cycle) bulk moment for committee sign-off |

### 3.15 Travel

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit | `POST staff-travel/requests/{id}/submit` | 🔲 | 🟢 | Requester-initiated |
| **Approve/reject travel request** | `POST staff-travel/requests/{id}/approve` \| `/reject` | 🔲 | 🔴 | Confirmed HIGH-priority daily approval-desk queue |
| Cancel / complete | `POST staff-travel/requests/{id}/cancel` \| `/complete` | 🔲 | 🟢 | Individual outcome |
| Approve/withdraw travel policy | `POST staff-travel-policies/{id}/approve` \| `/withdraw` | 🔲 | 🟢 | Rare admin/config act |
| Send timesheet invoice | `POST timesheet-invoices/{id}/send` | 🔲 | 🟢 | Per-client |

### 3.16 Medical & health

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| **Approve/reject/flag medical claim** | (medical-claims decision endpoints) | 🔲 | 🔴 | Confirmed HIGH-priority — claims teams process a daily batch; each item still needs individual amount adjudication so a "bulk approve" must support per-item amount overrides, not just a blanket approve (see §4.2's per-item-payload pattern) |
| Verify physician | `POST medical-physicians/{id}/verify` | 🔲 | 🟢 | One-off credentialing act |

### 3.17 Benefits

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Activate/deactivate employee bank | `POST employee-banks/{id}/activate` \| `/deactivate` | 🔲 | 🟢 | Individual, sensitive (bank detail) act — see masking guidance in `HR-REPORTS-CATALOGUE.md` §5 |
| **Enroll/withdraw benefit** | `POST benefit-enrollments/{id}/enroll` \| `/withdraw` | 🔲 | 🟡 | Annual open-enrollment window is a real batch moment (confirmed by frontend research) |

### 3.18 HR policies, letters & announcements

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Publish/archive policy | `POST hr-policies/{id}/publish` \| `/archive` | 🔲 | 🟢 | Rare, deliberate, one-at-a-time by nature |
| Publish/retract announcement | `POST hr-announcements/{id}/publish` \| `/retract` | 🔲 | 🟢 | Same |
| Issue/cancel letter request | `POST hr-letter-requests/{id}/issue` \| `/cancel` | 🔲 | 🟡 | A batch of confirmation-of-employment letters (e.g. for a bank/embassy drive) is a plausible real scenario |
| Acknowledge policy (employee) | `POST my-policies/{id}/acknowledge` | 🔲 | 🟢 | Employee self-service |

### 3.19 Succession

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/reject succession plan | `POST succession-plans/{id}/approve` \| `/reject` | 🔲 | 🟡 | Annual talent-review cycle batch moment |

### 3.20 Safety, Health & Environment (SHE)

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/cancel permit-to-work | `POST she-permit-to-work/{id}/approve` \| `/cancel` | 🔲 | 🟢 | Site-specific, individually assessed |
| Close audit | `POST she-audits/{id}/close` | 🔲 | 🟢 | One-off |
| Approve environmental review / risk assessment | `POST she-environmental-review/{id}/approve` \| `she-risk-assessment/{id}/approve` | 🔲 | 🟢 | Individually assessed, deliberately not batchable |
| Cancel stop-work order | `POST she-stop-work/{id}/cancel` | 🔲 | 🟢 | Safety-critical, should stay individual |

### 3.21 Client/contractor engagements

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Activate/complete/suspend engagement | `POST client-engagements/{id}/activate` \| `/complete` \| `/suspend` | 🔲 | 🟢 | Per-contract |

---

## 4. How to build these

### 4.1 The golden rule: loop the real service call, never bypass it

A bulk endpoint's job is orchestration, not a shortcut. For every ID in the request, it must call
the **exact same** application-service method (or workflow-adapter transition) the single-item
endpoint calls — same authorization check, same workflow-engine state transition, same
notification trigger, same audit write. This matters more in HR than almost anywhere else in this
codebase because so many of these actions are gated by the workflow engine (15 adapter files
confirmed in `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.3) and by per-relationship authorization (a
manager may only approve their own reports' leave — a bulk endpoint that checked authorization
once "for the batch" instead of once per item would be a real access-control hole).

### 4.2 Standardize on one response shape going forward

Three different shapes exist today (Recruitment's `Succeeded`/`Skipped`/`Results[]`; Finance's
`TotalCount`/`SuccessCount`/`FailureCount`/aggregate `Errors[]`; Training's
`createdCount`/`requestedCount`/`skipped[]` with a reason per skip). **Training's shape is the one
to standardize on** for new HR bulk endpoints — it is the only one of the three that tells the
user *why* a specific item didn't go through, which is exactly what someone bulk-approving 50
items needs to see for the 3 that failed. A house-standard DTO:

```csharp
public class HrBulkActionResultDto
{
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public List<HrBulkActionItemResult> Results { get; set; } = new();
}

public class HrBulkActionItemResult
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? Reason { get; set; }   // why it was skipped/failed, null on success
}
```

### 4.3 Request shape: IDs alone vs. per-item payloads

Two real shapes are needed, and both already have a working example:
- **Common action, same value for every item** (e.g. bulk-approve with one shared comment): a
  list of IDs plus one optional shared field — the Recruitment bulk-shortlist/reject shape.
- **Per-item values differ** (e.g. bulk-record training completion, where each person has their
  own completion date/status; bulk-approve medical claims, where each claim needs its own approved
  amount): a list of small per-item DTOs — the Training `bulk-record` shape. **Do not force a
  per-item-payload scenario into an IDs-only endpoint** — that was avoided correctly in the one
  place it mattered (Training) and should stay avoided for Medical Claims, which has the same
  shape of problem (§3.16).

### 4.4 Preview/dry-run before committing, for anything consequential

`SeparationsController`'s repair endpoint already supports a `dryRun` flag. Reuse this for any
bulk action where an item might be silently ineligible (wrong status, already processed, outside
the actor's authority) — show the user "47 of 50 selected will be approved; 3 are not eligible
because X" **before** they commit, not as a surprise in the result screen afterward.

### 4.5 Transaction model: best-effort, consistently

Every existing bulk operation in this codebase uses best-effort processing (partial success is
acceptable and reported per item) rather than all-or-nothing. **Keep doing this** for approval-type
bulk actions — a single malformed row shouldn't block 49 good ones. The one exception to flag: once
Payroll migrates its journal posting to `IFinancePostingEngine` (per `HR-FINANCE-ENTITY-SWEEP.md`),
any bulk action that ends in a GL posting should think harder about atomicity per Finance's own
rules — that is a Finance-owned decision, not something to default silently either way.

### 4.6 Audit the bulk act itself, not just each item

Today, a bulk action's audit trail is scattered across per-item log lines (confirmed: no
"BulkOperation.Completed" event exists anywhere). Once `IHrAuditService` exists (recommended in
`HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.4), a bulk action is exactly the kind of event worth a
single semantic record: who ran it, what filter/selection they used, how many succeeded/failed,
and — for sensitive actions (salary review, medical claims, bank details) — enough detail to
reconstruct the decision later.

### 4.7 Notifications: batch the digest, don't spam

Bulk-approving 50 leave requests should still notify 50 employees, but consider a single
manager-facing summary notification ("You approved 47 of 50 leave requests") alongside the
per-employee notifications, rather than 50 separate manager-facing pings. This is a UX
consideration layered on top of the reminder-notification gap already flagged in
`HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.1 — worth fixing both together rather than separately.

### 4.8 Frontend: two templates, pick based on complexity

- **Simple IDs-only actions** (approve/reject with a shared comment): copy the Payroll setup
  pages' `pickerSelectedIds` pattern — a checkbox column, a tri-state "select all," one button.
- **Per-item-payload actions** (differing amounts/dates per record): copy `BulkCompletionPanel`'s
  pattern — per-row editable fields for the selected rows, one submit, a results dialog listing
  what succeeded and why anything was skipped.

Consider promoting the `AdminDataTable` component's `selectable` prop into a shared HR table
component (as the frontend research recommended) once 2-3 of the 🔴-priority pages are built, so
the 4th one doesn't re-invent selection state again.

---

## 5. Suggested build priority

Ranked by confirmed queue volume (frontend research) and how many separate clicks it currently
takes to clear a typical queue:

1. **Leave approve/reject** — the single most-cited high-volume manager queue.
2. **Travel request approve/reject** — confirmed daily-desk queue.
3. **Medical claims approve/reject/flag** — confirmed daily batch-processing queue; needs the
   per-item-payload shape (§4.3) because each claim needs its own approved amount.
4. **Staff movement approve** — confirmed high-volume, especially around reorganisations.
5. **Salary review proposal approve/reject** — seasonal but very high-volume in its window.
6. **Employee goal approve/reject** and **appraisal HR-review sign-off** — both are cycle-batch
   scenarios (goal-setting season, appraisal-cycle close) where the entire point of the queue is
   that it arrives all at once.
7. **Training nomination approve/reject** (the bulk-*create* exists; bulk-*approve* does not).
8. **Employee-relations case assignment** (triage only — see §3.8's caveat about which actions in
   this area are and aren't good bulk candidates).
9. Everything else in §3, roughly in the order listed within each sub-domain.

**Deliberately excluded from bulk, and why:** Discipline hearings/decisions, Separation
approvals, SHE approvals, and Grievance substantive responses are each flagged 🟢 not because bulk
support is technically hard, but because each of these is a legally or safety-significant,
individually-reasoned decision where batching would work against the due-process/individual-
judgement purpose the workflow exists for in the first place. If TDC specifically asks for bulk
support in one of these areas, treat it as a policy question to raise, not a default to build.
