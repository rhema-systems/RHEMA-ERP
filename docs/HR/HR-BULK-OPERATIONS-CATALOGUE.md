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

## ⚠ Vetting pass, 2026-09-02 — corrections

| Where | Verdict | Corrected fact |
|---|---|---|
| §1 — "~40 controllers", "296+ single-item action endpoints" | **Both undercounts.** | `Controllers/HR/` holds **262 controller classes** (266 files) including the 28 SHE ones; `{id}/<verb>` POST/PATCH/PUT action routes number **799** (95 of them SHE). Top verbs: approve 40, submit 33, reject 31, complete 29, cancel 20, close 19, deactivate 17, activate 14, verify 13, recall 11, acknowledge 10. |
| §1 — "zero HR list/queue pages have checkbox multi-select" | **WRONG (and §2 contradicts it).** | Two HR pages have real multi-select: `hr/attendance/alerts/page.tsx` (toggle-all + `bulkAcknowledge`) and `hr/recruitment/vacancies/[id]/pipeline/page.tsx` (`bulkAction` toolbar); plus the six payroll `pickerSelectedIds` pages. None of the 🔴 approval queues in §3 has it — that part stands. |
| §2 — Recruitment `notify-shortlist` / `notify-reject` | **Do not exist anywhere in `src/`.** | Recruitment has **five** bulk endpoints, not seven. Rows removed. |
| §2 — route strings | Several wrong. | Real routes: Payroll `api/hr/payroll/setup/bonus-rules/bulk`, `setup/bonus-exceptions/bulk` (+ the other five under `api/hr/payroll/`); Recruitment `POST api/job-applications/vacancy/{vacancyId}/bulk-shortlist\|bulk-reject\|auto-shortlist` (vacancy-scoped) and `POST api/applications/bulk-move\|bulk-pipeline-reject` (prefix `api/applications`); Training bulk record is **`POST api/training-completions/bulk`**, needs-assessment `POST api/training-needs-assessments/bulk`; Orientation `POST api/employee-orientations/bulk-enroll`; Competency `POST api/employee-competencies/batch-assess`, `PUT api/position-competencies/position/{positionId}/bulk-set`; Appraisal `POST api/AppraisalCycleTemplates/bulk-assign/{cycleId}`; Attendance `POST api/staff-attendance-alerts/bulk-acknowledge`; Separations `POST api/hr/separations/repair/disciplinary-orphans?dryRun=true` (dryRun defaults **true**). The method names the table quoted (`BulkCreateNeedsAssessment`, `BulkEnrollOrientations`, …) are not the real method names. |
| §2 — bulk endpoints the table missed | Five. | `PATCH api/succession-candidates/bulk-rank` (used by `CandidatesPanel.tsx`); `POST api/talent-pool/bulk` (returns `RecruitmentBulkOperationResultDto`); `POST api/training-nominations/attendance/bulk`; `POST api/PerformanceAppraisals/{appraisalId}/peer-nominations/batch`; `POST api/hr/separations/contract-expiries/sweep`. Adjacent: `UserEmployeeLinkController` `bulk-link`. |
| §3 — route strings throughout | **Indicative, not verified — several are wrong.** | Spot-check of ten: leave approve/reject/cancel/close are **`PUT api/leaves/{id}/…`** and **no `recall` route exists**; `PATCH api/hr/leave-encashments/{id}/approve` ✓; `POST api/staff-movements/{id}/approve` ✓; `POST api/SalaryReviewProposals/{id}/approve` ✓; `POST api/EmployeeGoals/{goalId}/approve` ✓; assets is **`requisitions`** (plural); `POST api/staff-travel/requests/{id}/approve` ✓; `POST api/hr/employee-relations/{id}/assign` ✓; letters are **`api/hr/letter-requests/{id}/issue`**; benefits has **no `{id}/enroll` or `/withdraw`** — real is `POST api/hr/employee-benefit-enrollments`, `POST {id}/status`, `POST reconcile`. SHE: **no `she-*` prefixes** — `POST api/safety/permits/{id}/approve` (permits have `/close`, not `/cancel`), `api/safety/audits/{id}/close` (+ `/cancel`), `api/safety/stop-work/{id}/cancel`, `api/safety/environmental/reviews/{id}/approve` (+ `management-approve`, `approve-commencement`), `api/safety/risk-assessments/{id}/approve`. **Before building any bulk sibling, read the route off the controller; do not copy it from §3.** |
| §4.5 — "every existing bulk operation uses best-effort processing" | **Overstated.** | Recruitment `BulkRejectAsync` is true per-item best-effort (try/catch, Succeeded/Skipped). Training `BulkRecordCompletionAsync` skips invalid items but persists the rest in **one** `SaveChangesAsync` (atomic under EF's implicit transaction). Payroll `SaveBonusRulesAsync` is a replace-set with one save that throws outright — **all-or-nothing**. None uses an explicit transaction. The recommendation (best-effort, per-item result) stands; the claim that it is already uniform does not. |
| §4.6 — "no BulkOperation.Completed event exists" | CONFIRMED. | |

---

## 1. Executive summary

**HR already has bulk operations — just not where the queues are busiest.** Confirmed, working
bulk endpoints exist for: Payroll setup data (7 endpoints — bonus rules/exceptions, tax reliefs,
component exceptions, promotion arrears, overtime summaries, contribution opening balances),
Recruitment (5 endpoints — bulk shortlist/reject/pipeline-reject/move-stage, auto-shortlist), Training
(bulk nominate, bulk record completion, bulk needs-assessment), Orientation (bulk enroll),
Competency (batch assess), Appraisal (bulk-assign cycle templates), and Attendance (bulk
acknowledge alerts, bulk import). That is real, proven infrastructure — the request/response
shapes, the "best-effort with per-item result" transaction model, and even a working frontend
multi-select pattern all already exist somewhere in this codebase.

**What's missing is coverage of the highest-volume queues.** A full enumeration of HR's 262
controllers (28 of them SHE) found **799 `{id}/<verb>` action endpoints**, of which about two
dozen have a bulk sibling. Cross-referencing against the frontend confirms the gap is exactly
where it hurts most: only two HR pages have checkbox multi-select today (attendance alerts and
the recruitment pipeline), plus the six payroll setup pages — **none of the approval queues in §3
does**, even though several are explicitly manager- or HR-officer-facing queues that can run into
the dozens or hundreds of pending items (a manager with 50+ direct reports approving leave one
row at a time; a claims team adjudicating a daily batch of medical claims one row at a time).
*(Counts corrected 2026-09-02.)*

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
| Payroll (payroll dev's) | Bonus rules | `POST api/hr/payroll/setup/bonus-rules/bulk` | Full DTOs + `IsSelected` flag | List of saved DTOs — **replace-set, all-or-nothing** |
| Payroll (payroll dev's) | Bonus exceptions | `POST api/hr/payroll/setup/bonus-exceptions/bulk` | Full DTOs + overrides | List of saved DTOs |
| Payroll (payroll dev's) | Tax reliefs | `POST api/hr/payroll/tax-reliefs/bulk` | Full DTOs | List of saved DTOs |
| Payroll (payroll dev's) | Component exceptions | `POST api/hr/payroll/component-exceptions/bulk` | Full DTOs | List of saved DTOs |
| Payroll (payroll dev's) | Promotion arrears | `POST api/hr/payroll/promotion-arrears/bulk` | Full DTOs | List of saved DTOs |
| Payroll (payroll dev's) | Overtime summaries | `POST api/hr/payroll/overtime-summaries/bulk` | Full DTOs | List of saved DTOs |
| Payroll (payroll dev's) | Contribution opening balances | `POST api/hr/payroll/contribution-opening-balances/bulk` | Full DTOs | List of saved DTOs |
| Recruitment | Bulk shortlist | `POST api/job-applications/vacancy/{vacancyId}/bulk-shortlist` | IDs + notes | `RecruitmentBulkOperationResultDto` (Succeeded/Skipped/per-item Results) — true per-item best-effort |
| Recruitment | Bulk reject (application) | `POST api/job-applications/vacancy/{vacancyId}/bulk-reject` | IDs + reason | Same shape |
| Recruitment | Bulk reject (pipeline stage) | `POST api/applications/bulk-pipeline-reject` | IDs + reason | Same shape |
| Recruitment | Bulk move stage | `POST api/applications/bulk-move` | IDs + targetStageId | Same shape |
| Recruitment | Auto-shortlist | `POST api/job-applications/vacancy/{vacancyId}/auto-shortlist` | VacancyId + score threshold | Same shape |
| Recruitment | Talent-pool bulk add | `POST api/talent-pool/bulk` | DTOs array | `RecruitmentBulkOperationResultDto` |
| Training | Bulk nominate | `POST api/training-nominations/bulk` | DTOs array | Counts + skip list |
| Training | Bulk record completion | `POST api/training-completions/bulk` (`BulkRecordCompletion`) | Items array (per-person date/status) | `{createdCount, requestedCount, skipped[]}` — **the cleanest existing result shape, see §4.2**; note it persists the valid subset in one save |
| Training | Bulk attendance | `POST api/training-nominations/attendance/bulk` | Items array | Counts |
| Training | Bulk needs-assessment | `POST api/training-needs-assessments/bulk` (`BulkCreate`) | DTOs array | Counts + skip list |
| Orientation | Bulk enroll | `POST api/employee-orientations/bulk-enroll` (`BulkEnroll`) | — | List of enrollment DTOs |
| Competency | Batch assess / bulk set | `POST api/employee-competencies/batch-assess` (`BatchAssess`), `PUT api/position-competencies/position/{positionId}/bulk-set` | — | Custom result DTOs |
| Appraisal | Bulk assign cycle templates | `POST api/AppraisalCycleTemplates/bulk-assign/{cycleId}` (`BulkAssign`) | — | `IActionResult` |
| Appraisal | Batch peer nominations | `POST api/PerformanceAppraisals/{appraisalId}/peer-nominations/batch` | IDs | — |
| Succession | Bulk rank candidates | `PATCH api/succession-candidates/bulk-rank` | Ranked IDs | — (used by `CandidatesPanel.tsx`) |
| Attendance | Bulk acknowledge alerts | `POST api/staff-attendance-alerts/bulk-acknowledge` | IDs | Count — **has a working multi-select UI** (`hr/attendance/alerts`) |
| Attendance | Bulk import | `StaffBulkAttendanceImport` (`api/staff-bulk-attendance-imports`) | File | Import record |
| Separations | Repair disciplinary orphans | `POST api/hr/separations/repair/disciplinary-orphans?dryRun=true` | `dryRun` (default **true**) | Repair summary |
| Separations | Contract-expiry sweep | `POST api/hr/separations/contract-expiries/sweep` | — | Sweep summary |

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

⚠ **Route strings in this section are indicative** (vetting 2026-09-02 found roughly a third of
the spot-checked ones wrong in verb, prefix or spelling — see the corrections block). The
*assessment* columns are the content; read the real route off the controller before building.

### 3.1 Leave

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Submit | `POST api/leaves/{id}/submit` | 🔲 | 🟢 | Employee self-service, one request at a time by nature |
| **Approve** | `PUT api/leaves/{id}/approve` | 🔲 | 🔴 | Manager queue — the #1 candidate found; a manager with many reports approves dozens at a time. Workflow-validated (`CanUserApproveAsync`) — the bulk loop must call the same service method |
| **Reject** | `PUT api/leaves/{id}/reject` | 🔲 | 🔴 | Same queue, same priority |
| Cancel / close | `PUT api/leaves/{id}/cancel` \| `/close` | 🔲 | 🟢 | Usually one-off |
| Recall | — | — | — | **No recall route exists on `LeavesController`** (the generic workflow recall button calls the engine directly); row kept so the absence is recorded |
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
| **Approve requisition** | `POST api/assets/requisitions/{id}/approve` | 🔲 | 🟡 | HR/procurement approving a batch of requests — workflow-validated (`HrAssetRequisition` adapter) |

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
| **Enroll / change status** | `POST api/hr/employee-benefit-enrollments` (create), `POST …/{id}/status`, `POST …/reconcile` — there is **no** `{id}/enroll` or `/withdraw` route (corrected 2026-09-02) | 🔲 | 🟡 | Annual open-enrollment window is a real batch moment; the "mass benefit application" row in the closure ledger §F is this item |

### 3.18 HR policies, letters & announcements

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Publish/archive policy | `POST hr-policies/{id}/publish` \| `/archive` | 🔲 | 🟢 | Rare, deliberate, one-at-a-time by nature |
| Publish/retract announcement | `POST hr-announcements/{id}/publish` \| `/retract` | 🔲 | 🟢 | Same |
| Issue/cancel letter request | `POST api/hr/letter-requests/{id}/issue` \| `/cancel` | 🔲 | 🟡 | A batch of confirmation-of-employment letters (e.g. for a bank/embassy drive) is a plausible real scenario |
| Acknowledge policy (employee) | `POST my-policies/{id}/acknowledge` | 🔲 | 🟢 | Employee self-service |

### 3.19 Succession

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/reject succession plan | `POST succession-plans/{id}/approve` \| `/reject` | 🔲 | 🟡 | Annual talent-review cycle batch moment |

### 3.20 Safety, Health & Environment (SHE)

| Action | Route | Bulk today? | Priority | Notes |
|---|---|---|---|---|
| Approve/close permit-to-work | `POST api/safety/permits/{id}/approve` \| `/close` (no `/cancel`) | 🔲 | 🟢 | Site-specific, individually assessed; approval is gated on hazards/controls/gas-test sections (FR-PTW-002) |
| Close / cancel audit | `POST api/safety/audits/{id}/close` \| `/cancel` | 🔲 | 🟢 | One-off |
| Approve environmental review / risk assessment | `POST api/safety/environmental/reviews/{id}/approve` (+ `management-approve`, `approve-commencement`) \| `api/safety/risk-assessments/{id}/approve` | 🔲 | 🟢 | Individually assessed, deliberately not batchable; the review has a statutory ladder |
| Cancel stop-work order | `POST api/safety/stop-work/{id}/cancel` | 🔲 | 🟢 | Safety-critical, should stay individual |

*(SHE routes corrected 2026-09-02 — all 28 SHE controllers live under `api/safety/*`; the earlier
`she-*` prefixes did not exist. See `HR-SHE-INTEGRATION-AND-BOUNDARIES.md`.)*

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

`SeparationsController`'s repair endpoint already supports a `dryRun` flag (default true), and so
does `HrLegacyFileMigrationController`'s `run`. Reuse this for any
bulk action where an item might be silently ineligible (wrong status, already processed, outside
the actor's authority) — show the user "47 of 50 selected will be approved; 3 are not eligible
because X" **before** they commit, not as a surprise in the result screen afterward.

### 4.5 Transaction model: best-effort, consistently

The existing bulk operations are **not** uniform *(corrected 2026-09-02)*: Recruitment's
`BulkRejectAsync` is true per-item best-effort (try/catch around each `RejectAsync`); Training's
`BulkRecordCompletionAsync` skips ineligible items but persists the rest in one `SaveChangesAsync`;
Payroll's `SaveBonusRulesAsync` is a replace-set that throws outright (all-or-nothing). None uses an
explicit transaction. **Standardise on the Recruitment shape** for approval-type bulk actions —
per-item try/catch, per-item result, partial success reported — so a single malformed row doesn't
block 49 good ones and, more importantly, so a workflow-engine refusal on item 12 does not roll
back the eleven approvals the engine already recorded. The one exception to flag: once
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
