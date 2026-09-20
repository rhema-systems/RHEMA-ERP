# HR workflow auto-approve — closure plan

**The mechanism.** `WorkflowIntegrationService.SubmitAsync` returns `WorkflowOutcome.Approved`
whenever no active workflow definition exists for the entity type — a deliberate "approval is not
configured, use the direct lifecycle" signal shared by nine modules. **Every HR status adapter maps
`Approved` to its own approved status.** So where no definition is published, pressing **Submit**
approves the record: no approver, no `CanUserApproveAsync`, and no segregation-of-duties check,
because those live in `ApproveAsync`, which is never called.

Found through recruitment (G-4.1, G-10.1 of `HR-RECRUITMENT-SYSTEM-GUIDE.md`). **25 HR services,
27 submit call sites.** Five are closed; **20 remain.**

---

## ⚠ CORRECTION, 2026-09-16 — read before anything else here

**An earlier version of this plan, the recruitment guide, the recruitment closure plan and several
code comments all said "no HR workflow definition is seeded anywhere in the solution". That is
false, and it was false when the recruitment guide first said it.**

`DatabaseSeedingService.EnsureHrWorkflowsSeededAsync` seeds **28 HR workflow definitions**, each
`IsActive = true` and `LifecycleStatus = Published`, covering every entity type in this programme —
`STAFF_REQUISITION`, `JOB_OFFER`, `EMPLOYEE_SEPARATION`, `LEAVE_REQUEST` and the rest. It landed in
`e0a94dc5` on **2026-09-02, twelve days before the guide walk.**

The name matching works: `SimpleWorkflowService.NormalizeEntityTypeKey` strips non-alphanumerics
and uppercases, so the `"StaffRequisition"` a service passes normalises to `STAFFREQUISITION` and
matches both the seeded code `STAFF_REQUISITION` and the seeded name `Staff Requisition`.
`GetActiveByEntityTypeAsync` filters on exactly the fields the seeder sets.

**So on a seeded tenant, `HasActiveApprovalWorkflowAsync` returns true and none of this fires.**

### What that changes, and what it does not

| | |
|---|---|
| **The severity claim was wrong** | Not "on every tenant, Submit was Approve". It is: *on a tenant where workflow seeding has not run, or where the definitions were unpublished, deleted, or never reached because the tenant predates the seeder.* |
| **The fix is still worth doing** | That state is real and reachable — it is how `TeamActivityService`'s author found it ("Found by the harness's no-definition assertion"), and how `EmployeeSalaryChangeRequestService`'s author found it. Both wrote their own guard. The failure mode is silent approval of pay changes and terminations, with no trace afterwards that nobody was asked. |
| **Two of the recruitment fixes stand regardless** | The **offer had no segregation-of-duties check at all**, and neither did **vacancy approval**. Those were real holes independent of any definition, and they are now closed. |
| **This programme is defence in depth** | Decided with the user on 2026-09-16 after the correction, with the severity understood. It is not closing a live hole on a properly provisioned tenant. |

### The lesson, which is the same one this module keeps teaching

The guide's own *"note on method"* already records four findings that were wrong because a search
stood in for reading the code. **This is the fifth, and it is the most expensive**, because it was
not a detail inside a finding — it was the premise under six of them, and it survived a full
closure pass. `grep` for the *absence* of a thing proves nothing; the seeder was one call away in
`DatabaseSeedingService` the whole time.

**Worth checking separately:** whether seeding has actually run on the live and UAT tenants. The
code seeds; that is not the same as the rows existing.

---

## The four-part shape — and why part 1 alone makes it worse

Fixing submit on its own **strands the record**. With no workflow instance,
`CanUserApproveAsync` returns `false` and `RecallWorkflowAsync` answers *"No active workflow
found"* — so a record that stops at a pending status becomes unapprovable, unrejectable and
unrecallable, with cancellation its only exit. That is exactly the shape of the recruitment
guide's G-4.2. **All four move together:**

| | Change |
|---|---|
| **Submit** | Ask `HasActiveApprovalWorkflowAsync` first; with no definition hand the adapter `WorkflowOutcome.Pending` instead of the engine's `Approved`. Every HR adapter already maps `Pending` (its `default:` arm) to the module's own pending status — checked, all of them. |
| **Approve** | No-workflow branch: skip the engine, check fallback authority, apply `Approved`. |
| **Reject** | Same branch, applying `Rejected`. |
| **Recall** | Skip the engine call; the record returns to Draft via the adapter. |

Delivered through helpers on `HrWorkflowFallbackAuthority` so each service is a ~3-line change,
not ~20.

---

## Decisions taken with the user, 2026-09-16

### 1. A new `Approve` permission per module

**Not the Admin tier.** The investigation found the Admin tiers are *destructive-operations*
permissions, not approval permissions — `AdministerLeave`'s own description says
*"Approving leave is **NOT** this permission — approval belongs to the workflow assignee and is
validated per request by the workflow engine."* `AdministerDiscipline` says the same of deciding a
case; `AdministerCompensation` is about deleting grades and deactivating pay components. Using them
as the fallback approver would misuse them, and granting them to HR to enable approval would widen
HR's *destructive* reach as a side effect.

So: `HR.<Module>.Approve`, meaning **"may rule on a record of this kind when no workflow definition
is published"**. Interim by construction — once a definition names the approver, none of this code
runs — and trivially revoked then.

### 2. Five modules get the permission but **HR is not granted it**

Each carries a documented objection to HR approving, and the objections are requirement-driven:

| Module | Why HR is not granted it |
|---|---|
| **Separation** | FR-HR-092 puts the **Managing Director's** signature on a non-procedural termination and FR-HR-185 puts Internal Audit before the settlement. `HrPermissions` says granting HR this "would let HR sign off its own terminations and release its own payments". |
| **Probation** | Confirm / extend / terminate are management acts. The map warns: *"do not relax them before [the instance-level check], or the outcome becomes reachable by anyone HR-shaped."* |
| **Succession** | Approving a plan names a person as the intended successor; finalising calibration fixes a nine-box placement that feeds promotion decisions. Management acts, not record-keeping. |
| **JobArchitecture** | FR-HR-134: an approved job description is what a position is measured against, and carries the job valuation and suggested salary grade. |
| **ManpowerBudget** | Approving one **sets the approved establishment**, which gates whether a vacancy may be approved at all under FR-HR-136. |

These five still stop auto-approving. Their fallback authority simply resolves to nobody but
`SuperAdmin` / `TenantAdmin` / `Admin`, so an approval **stalls until a real authority acts or a
definition is published** — which is the documented intent, and safer than the alternative.

### 3. High-volume self-service records get the same treatment

Leave, attendance regularisation and overtime stop at pending like everything else. A request
nobody approved must not read as approved.

⚠ **Leave already has the right escape hatch and it is not this defect.**
`LeaveType.RequiresApproval = false` auto-approves on submission *by configuration*, deliberately,
for low-risk types — correct, documented, and untouched. The defect is only the path below it:
approval *is* required, no definition is published, and it approved anyway.

### 4. Retro-fix from the recruitment pass

`StaffMovement`, `PerformanceImprovementPlan` and `EmploymentActionProposal` were closed in the
recruitment pass using `AdministerMovements` / `AdministerPerformance` as the fallback tier — which
**HR does not hold**, so an HR user could no longer approve them. Both the misuse and the lockout
are corrected by moving them onto the new `Approve` permissions. `StaffRequisition` and `JobOffer`
move off `AdministerRecruitment` onto `HR.Recruitment.Approve` for the same reason.

---

## Lane status

| Lane | Services | State |
|---|---|---|
| 0 — permissions + helpers | `HrPermissions`, `HrWorkflowFallbackAuthority` | **built** |
| 1 — the consequential three | `StaffDisciplinaryCase`, `SalaryReviewProposal`, `AppraisalTemplate` | **built** |
| 2 — the four with objections | `Separation`, `Probation`, `SuccessionPlan`, `JobAnalysis` (JD + budget) | **built** |
| — already guarded, no change | `EmployeeSalaryChangeRequest`, `TeamActivityService` | **left alone.** Both already call `RequireApprovalWasActuallySought`, which **refuses the submit** when no definition is published rather than landing at pending. Stronger than this programme's fix, deliberate, and documented in their own remarks. They are also the evidence that the unseeded state is real — each author hit it and wrote a guard. |
| 3 — leave family | `Leave`, `LeavePlan`, `LeaveEncashment` | **built** |
| 4 — attendance family | `AttendanceCore`, `AttendanceOperations`, `OvertimeBiometric` | **built.** All three on `ApproveAttendance`. |
| 5 — the rest | `Assets` (requisition + transfer), `AssetSurcharge`, `StaffTravelRequest`, `TrainingNomination`, `Consultant` | **built.** `TeamActivity` dropped — see the row above; it already refuses the submit. |
| 6 — retro-fix | `StaffMovement`, `PIP`, `EmploymentActionProposal`, `StaffRequisition`, `JobOffer` | **built.** All five moved off the `Administer` tiers onto `ApproveMovements` / `ApprovePerformance` / `ApproveRecruitment`; the last two through `RecruitmentApprovalAuthority`, which pins the permission once. |
| 7 — verification sweep | all 26 engine-wired HR services | **built.** Found and closed a third decision verb (`LeavePlanService.SuggestChangesAsync`), two blind approver inboxes, and travel's missing self-approval rule. See the sweep section below. |

---

## Working rules

1. **Verify before fixing.** Read each service's submit path before changing it — `LeaveService`
   proved that not every auto-approve is the defect.
2. **All four parts, every time.** A service with submit fixed and recall not fixed is worse than
   one untouched.
3. **Check the adapter's `default:` arm** before assuming `Pending` lands somewhere sensible.
4. The user runs builds. Stop the API process first; it locks its DLLs.

## Lane 5 notes — what the shape had to bend around

| Service | Note |
|---|---|
| `AssetRequisition`, `AssetTransfer`, `AssetSurcharge` | Each already had a private `ProcessApprovalAsync` wrapping the engine in `RunWorkflowAsync`, which translates the engine's refusals into `AssetsWorkflowException` so the area's callers can read them. That wrapper is worth keeping, so these three do **not** call `HrWorkflowFallbackAuthority.ProcessApprovalAsync`; the no-definition branch is written inline in each private helper and calls `EnsureCanRuleWithoutWorkflow` directly. The helpers' return type changed from `WorkflowIntegrationResult` to `WorkflowOutcome`, so the four call sites now read `outcome` rather than `result.Outcome`. |
| `AssetRequisition`, `AssetTransfer`, `AssetSurcharge` — recall | All three had a live recall, so all three got part 4: the engine call is skipped when no definition is published and the adapter still returns the record to Draft. |
| `StaffTravelRequest` | **Had no segregation-of-duties check of its own** — `CanUserApproveAsync` was the whole gate, which is a fact about the definition and not about the record, so on an unconfigured tenant a holder of `ApproveTravel` could have approved their own trip. First left open on the grounds that the service could not resolve the caller's *employee* id; that turned out to be wrong — `ICurrentUserService.EmployeeId` carries it, and `JobInterviewService` already holds both current-user abstractions side by side for the same reason. **Now closed**: `RequireNotTheTraveller` runs on BOTH paths, like the equivalent rule in `StaffMovementService.ApproveAsync`, because the engine's own check is by ApplicationUser and this one is by Employee — so it still catches a traveller deciding through a second login. No controller change was needed. Travel has no recall — `CancelAsync` is a direct status change, not a workflow recall — so parts 1–3 plus this are the whole fix. |
| `ConsultantTimesheet` | Pinned to `ApproveAttendance`, not a new consultant tier. `ConsultantTimesheetsController` already gates every other timesheet action on the attendance read/write policies, so inventing `HR.Consultant.Approve` would have split one area's authority across two permissions. |
| `TrainingNomination` | **Different shape, worse bug.** Submit read `if (SubmitAsync(...).ExecutionResult.Success)` — true on *both* paths — so the configurable-workflow branch was taken on every tenant, the nomination approved itself on submission, and `EnsureBondForNominationAsync` raised a **service bond against the employee** for a course nobody had agreed to. The legacy Supervisor→HR chain the comment promised as the fallback was unreachable code. The fix asks `HasActiveApprovalWorkflowAsync` first, which restores the branch the comment always described. Approve and reject needed nothing: they already branch on `entity.WorkflowInstanceId.HasValue`, and with no definition there is no instance, so they fall to the legacy chain and its own `EnsureLegacyDecisionAllowed`. |
| `JobInterviewService` | Its `AdministerRecruitment` reference was **not** touched. It is the recruitment-desk read/write check (`IsHr`), not a fallback approval gate. |

## Lane 7 — the verification sweep, and the three things it found

Ran after lanes 4–6 built. Method: every HR service holding `IWorkflowIntegrationService` (26 files) intersected with every HR service naming `HasActiveApprovalWorkflowAsync`, `HrWorkflowFallbackAuthority`, `RecruitmentApprovalAuthority` or `RequireApprovalWasActuallySought` (27 files). **Difference: empty** — no HR service is on the engine without a guard. Counts recorded so the green is not vacuous; the 27th is `RecruitmentApprovalAuthority` itself, which is a guard and not a caller.

A file-level sweep is not a call-site sweep, so it was followed by two call-site greps — every `ApplySubmitOutcome(` handed something other than a `submitOutcome`, and every `CanUserApproveAsync(` — read one by one. That is what found the three below. **Lesson: the third decision verb is where this hides.** Submit, approve, reject and recall were the four the plan named; nothing in the plan pointed at *suggest changes* or at an approver's *inbox*, and both were broken by exactly the same mechanism.

| Found | Service | What it was |
|---|---|---|
| **A third decision verb** | `LeavePlanService.SuggestChangesAsync` | Sends a submitted plan back to the employee with alternative dates. Gated on `CanUserApproveAsync`, then calls `CancelWorkflowAsync`. With no definition there is no instance, so it answered false for everybody and the plan could not be sent back — the strand bug in a fifth place, and only reachable *because* lane 3 stopped submit from auto-approving. Now two-pathed like approve and reject, falling to `ApproveLeave`. |
| **Two blind approver inboxes** | `StaffDisciplinaryCaseService.GetAwaitingMyApprovalAsync`, `StaffMovementService.GetAwaitingMyApprovalAsync` | Both ask the engine per record. With no instance they returned **empty for every user**, while records genuinely sat awaiting a decision — so the fix moved records into a queue nobody could see. Both now return the whole pending set to holders of the module's `.Approve` permission when nothing is published, and nothing to anyone else. |
| **A discarded failure that is actually fine** | `StaffMovementService.CancelAsync` | Calls `CancelWorkflowAsync` with no guard. Checked rather than assumed: `SimpleWorkflowService.CancelWorkflowAsync` returns `Success = false` / `"No active workflow found"` when there is no instance — it does **not** throw — and the result is discarded, so cancellation goes through on both paths. Left as it was, with a comment recording why. It would throw only if the `WorkflowEntityType` row itself were missing, which is a misconfiguration rather than the unseeded-definition case. |

### Verified safe without change

| Service | Why |
|---|---|
| `JobAnalysisService` (×2 submit sites) | Both already inside `IsApprovalWorkflowConfiguredAsync`, with a direct-status `else` arm. |
| `EmployeeSalaryChangeRequestService`, `TeamActivityService` | Their raw `ApplySubmitOutcome` and unguarded `RecallAsync` are unreachable on an unconfigured tenant: `RequireApprovalWasActuallySought` refuses the submit, so nothing can reach a pending status for them to act on. |
| `JobInterviewService` | Its `AdministerRecruitment` reference is the recruitment-desk read/write check (`IsHr`), not an approval fallback. Not touched. |
| Recall in `EmploymentActionProposal`, `PIP`, `JobOffer`, `StaffMovement`, `StaffRequisition` | All five already inside `HasActiveApprovalWorkflowAsync`, each with the requester-only rule restated on the unconfigured arm where the engine used to enforce it. |
