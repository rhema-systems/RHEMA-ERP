# HR on the Workflow Engine — Recipe, Traps and Inherited Defects

**Created:** 2026-09-02, consolidating what eight separate wirings (2026-08-03 → 2026-08-23)
learned, re-verified against the code. **Why:** the patterns sweep counts adapters and the
integration map omitted the engine entirely; nothing in `docs/` says how HR plugs in or what
breaks. This is that page.

---

## 1. The rule

RHEMA has a comprehensive generic workflow engine. **Never build a module-specific approval UI —
reuse it.** Every approval in HR goes through it, with one deliberate exception (§6).

## 2. What is on the engine today

**15 adapter files** under `src/ErpSystem.Core/Services/Workflow/`, auto-discovered by assembly
scan (`WorkflowServiceCollectionExtensions`), no DI registration:

`HrAssets` (HrAssetRequisition, HrAssetTransfer) · `HrAttendance` (StaffAttendanceRegularization,
StaffOvertimeRequest, RemoteWorkRequest, ConsultantTimesheet) · `HrDiscipline` · `HrJobArchitecture`
· `HrJobOffer` · `HrLeave` · `HrMovement` · `HrPerformance` (appraisal templates/cycles) · `HrPip` ·
`HrProbation` · `HrProposal` (SalaryReviewProposal, EmploymentActionProposal) · `HrRequisition`
(StaffRequisition) · `HrSeparation` · `HrStaffTravel` · `HrSuccession`. `PayrollRunWorkflowStatusAdapter`
is payroll's own.

**How to tell whether a family is wired:** check the adapter registry
(`IWorkflowStatusAdapter.EntityTypes`), **not** the entity. Only 7 HR entities carry a
`WorkflowInstanceId` column (three Leave entities, `JobVacancy`, `EmployeeSeparation`,
`StaffRequisition`, `TrainingNomination`); Assets, Discipline, Movements, PIP, Probation and the
rest are wired with no such column.

**Not on the engine:** Medical (`MedicalExpenseClaim` create→approve→pay), Benefits
(`EmployeeBenefitEnrollment`), and all of SHE (permit-to-work, risk assessment, environmental
review) — bare `ApprovedById`/`ApprovedDate` pairs with service-level state rules. Same fix,
three areas.

## 3. The four-step plug-in recipe

Exemplars: `PayrollRunWorkflowStatusAdapter`, procurement's `ProcurementPlanService`,
`HrLeaveWorkflowStatusAdapters.cs`.

1. **Register the entity type.** Add it to `WorkflowEntityTypeCatalogService.GetDefaultEntityTypes()`,
   then `POST api/Workflow/entity-types/seed`. ⚠ The key is a **flat namespace shared across
   modules and it collides**: `AssetTransfer` already means Finance's fixed-asset transfer to
   `WorkflowEntityDisplayService`, `Asset` means Inventory's. HR's are `HrAssetRequisition` /
   `HrAssetTransfer`. Grep both `WorkflowEntityTypeCatalogService` and
   `WorkflowEntityDisplayService` for the bare class name before registering; under a colliding
   name an approver's notification deep-links to another module's screen and nothing errors.
2. **Write an `IWorkflowStatusAdapter`.** Map all four `WorkflowOutcome` members — Pending,
   Approved, Rejected, Recalled — onto the entity's status. Check the status enum first: if it has
   no member meaning "out for approval", add `PendingApproval` (statuses are stored as int and the
   DB is built from the EF model, so it is schema-safe); if it already has `Submitted`, use that.
   Rejection need not return to Draft (`Rejected` tells the requester someone ruled against it);
   Recall does. **Change the entity's C# default** if it is a live state (`PipStatus` defaulted to
   `Active`, so a plan created by any path that skipped status was live against a named employee
   with nobody approving it).
3. **Add a `BuildEntityContextAsync` case** in `src/ErpSystem.Api/Services/SimpleWorkflowService.cs`
   so routing can see the record's fields (amounts, day counts, `numberOfPositions`, `isBudgeted`,
   `isProcedural`). ⚠ Persist the record's own facts **before** calling `SubmitAsync` — the engine
   reads the entity back to build this context; set-but-unsaved values are invisible.
4. **Add a display resolver** in `WorkflowEntityDisplayService` (sets `ActionUrl` for notification
   deep-links — all 25 HR ones resolve to real pages) and add the type to
   `frontend/src/components/workflow/entityTypeMapping.ts`.

**Frontend:** `useWorkflowRecord` + `<WorkflowApprovalActions {...actionProps} />` +
`WorkflowTabTrigger`/`WorkflowTabContent`; `useWorkflowEntitySummaries` for list rows. The screen
never sets a status itself — refetch and let the adapter decide. `formatPendingApprovers` takes
the `pendingApprovers` array and returns `{short, full}`, not a string.

**Keep the domain's own rules in the service, and run them before `CanUserApproveAsync`.**
Budget enforcement, "only the Managing Director may sign a non-procedural termination", "the
subject cannot act" — these are facts about the record, not routing choices, and a refusal must
explain itself in the record's terms rather than "you are not assigned as an approver". They also
hold when the definition is missing or wrong. **Leave the terminal step off the engine**: Applied /
Actioned / Completed records that the owning module *did the work*, not that anyone approved it —
a direct action guarded by "only from Approved".

## 4. Authoring a definition — the traps, in the order they bite

| # | Trap | Symptom | Rule |
|---|---|---|---|
| 1 | **A single-step definition silently auto-approves everything.** One Approval step and no transitions → "End step completed" without ever requesting approval, even with `preventInitiatorApproval: true` and a populated approver role | Everything submitted is instantly Approved; a test file full of green | Author `Manual "Draft" → Approval(s) → Manual "Approved"` with a transition between each pair. The bookends are load-bearing. Re-read the created definition and confirm `steps: 3, transitions: 2` and the approver rule survived |
| 2 | **The entity-type seed was all-or-nothing** (fixed 2026-08-03): `Code` was derived UPPER_SNAKE with no cap against `nvarchar(50)`; one 52-char estate name killed the batch, leaving 49 of 136 types and **no HR types** | Submits refuse with "is not configured"; PayrollRun workflows dead too | Now truncates with a hash suffix. If types look missing, run the seed and check for length failures first |
| 3 | **A misnamed field in an approver rule publishes happily and can never be approved.** `WorkflowAssignmentRuleDto`'s field is `Role`; `roleName` is silently dropped by model binding | Create 201, publish 200, `pendingApprovers: []`, `canCurrentUserApprove` false for everyone, "You are not assigned as an approver" on every approve | After publishing any hand-authored definition, submit one record and check `GET api/Workflow/entity-summary` lists a pending approver before trusting it |
| 4 | **Conditional routing does not route** — cross-module defect **#3**. `CreateStepsAndTransitionsAsync` stores the whole `WorkflowConditionDto` as JSON; `WorkflowEngine` hands that blob to `WorkflowConditionEvaluator`, which expects a bare expression (`isProcedural == true`). The evaluator swallows its own exceptions; nothing logs | The branch taken tracks **priority alone**, both ways round; the definition reads back intact | **Do not design a definition that depends on a condition** until #3 is fixed (four call sites in `WorkflowEngine` change together). Name every eligible authority on one step and let the service decide from the record |
| 5 | **Priority is HIGHEST-wins.** `AdvanceFromStepAsync` orders `OrderByDescending(t => t.Priority)` | A conditional branch authored at priority 1 against a default at 2 can never be taken even once conditions work | Author the default lowest |
| 6 | **`condition` is a structured object on create**, `{ conditionType: 'Expression', expression: '…' }` | A bare string is refused outright — the lucky failure | — |
| 7 | **`preventInitiatorApproval` is the WRONG control when the record is *about* a third party.** It guards "nobody approves their own request", where the initiator is the beneficiary. For an exit, a disciplinary case, anything HR raises *about* an employee, the initiator gains nothing and the conflicted party is the **subject** — already barred by role | Left true on separations it silently rewrote FR-HR-092's "HR may approve a procedural termination" into "two HR officers must"; caught only because an older harness started failing | Ask who benefits before setting it. Any fixture with it true needs **two** users of the approving role or the submitter hits their own gate |
| 8 | **Entity types and definitions are TENANT-scoped** | Seeding as a SuperAdmin on another tenant returns 200 and lands the types where nothing looks | Seed and publish as an actor on the tenant under test |
| 9 | **Retire, don't delete.** `DELETE api/Workflow/definitions/{id}` 404s on a published definition; `POST …/{id}/retire` works and preserves the audit trail — and it is role-gated (`SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager`) | A harness helper retiring as a module actor gets 403; if it swallows that, leftovers accumulate and the engine chooses between **several active definitions with unspecified precedence** | Retire as an admin; assert exactly one active definition per type |
| 10 | **The controller wraps its payloads**: create → `{ success, data }`, listing → `{ success, data: [...], metadata }` | `.id` off the envelope is `undefined` | Read `.data` |
| 11 | A definition that fails publish validation still leaves a Draft row | The name is taken on retry | Delete the draft |
| 12 | Approval steps require `configuration.approvalConfig.approverRules` or publish fails; transitions accept `fromStepOrder`/`toStepOrder` (what makes one-POST authoring possible) | — | — |

## 5. Platform defects HR inherits (recorded in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`)

| # | Defect | Consequence for HR |
|---|---|---|
| **#3** | Conditional routing never routes (trap 4) | Every per-record routing rule in every module is dead; `BuildEntityContextAsync` exists for nothing until fixed |
| **#14** | `Workflow/approvals/pending` and `tasks/pending` return raw cyclic EF graphs; serialisation throws **after** the 200 status line whenever the caller has ≥ 1 row; `[]` when they have none | The platform inbox feeds die exactly when they have content. Fix = project DTOs |
| **#15 — the severe one** | `approvals/{id}/process`, `steps/{id}/process` and `workflow/platform/mobile/actions` (what the desk `/workflow/inbox` posts to) drive `WorkflowEngine.ProcessStepAsync` only. The module's `IWorkflowStatusAdapter` — which writes the outcome onto the entity — is invoked **only by the module's own approve endpoint**. Measured on all three paths: 200, approval consumed, workflow completed, **entity still `Submitted` with no pending approval left that could ever move it** | **This is why HR's portal inbox (area 25 slice 11) is read-and-navigate**: rows deep-link to the record, whose module commands carry the whole outcome. Any bulk-approve must call the module's service, never the generic surface (bulk catalogue §4.1). The recall path already does it right (`TryApplyRecallStatusAsync`) — the approve paths need the same |
| #16 | `POST api/Notifications` let anyone plant a notification in anyone's feed | **Fixed** (one-line role gate) |
| #17 | `/finance/fixed-assets/register/{id}` has no page (workflow display emits it) | Finance's; all 25 HR ActionUrls resolve |

**A behaviour that is not a defect but bites:** the generic recall button in
`WorkflowApprovalActions` calls the engine directly (`workflowApiService.recallWorkflowEntity`),
not the service's `/recall` endpoint — a service-level "only the requester may recall" rule holds
for API callers and is bypassed by that button (PIP and requisitions both have this split).

## 6. The deliberate exception — `EmployeeGoal` stays bespoke

Decided 2026-08-05: goal approval stays on `GoalWorkflowCommandService`. `GoalStatus` has two
writers (approval lifecycle + execution states written by `AddProgressEntryAsync`), so an adapter
would diverge from the entity the moment progress is logged; the shape is per-goal, per-employee,
per-cycle, single-step, always the direct manager from the reporting line (enforced from the
token — trap 3's exact failure mode if it became a publishable definition); and the governance
layer (`TeamGovernanceStatus`, weight balance, set completeness) has no engine equivalent. **Cost:**
goals do not appear in `/workflow/inbox`, no approval-history record, no deep-links. **Trigger to
revisit:** goal approval needing anything other than the direct manager — at which point the
engine unit is the employee's whole goal set for the cycle, a new aggregate.

## 7. The assertion pair every wiring must carry

A run where submit → Submitted and approve → Approved proves nothing: the adapter alone produces
it with the engine bypassed. Assert instead that (a) with **no published definition** a submit is
*refused*, and (b) with a definition routed to an authority the caller does not hold, the
**engine** refuses the approve after the controller's role gate has already admitted them. Area 16
was 129/129 before these two were added; one then failed, and the failure was real.

Also from the harness side: `run-slice15` in `hr-separation` must run first because it is the
only slice that publishes the `EmployeeSeparation` definition the others borrow; workflow-validated
approve/reject endpoints stay **ungated** by permission (`CanUserApproveAsync` is the check — a
permission would refuse line-manager approvers), so a harness employee must get **404 not 403** on
a proposal approve to prove the gate did not refuse the assignee path.

## 8. Actor conventions worth knowing before wiring an area

- The actor id the engine wants (user id) is not always the id the entity's FK wants (employee
  id). Attendance's `ApprovedById` is an Employee FK, unlike Leave's — the service needs both.
- Three of the four attendance types have no Draft state; the workflow starts at creation.
- `StaffRequisitionStatus` already had `Submitted`; the proposals and PIP needed `PendingApproval`
  added. Look before adding.

See `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.3, `HR-BULK-OPERATIONS-CATALOGUE.md` §4.1,
`HR-MODULE-INTEGRATION-MAP.md` row 28, `HR-SHE-INTEGRATION-AND-BOUNDARIES.md` §5.

## Tenth application — `HrEmployeeSalaryChangeRequest` (round 3, lane S, 2026-09-11)

Adapter `HrSalaryChangeWorkflowStatusAdapter`; the service APPLIES the change inside the approve
call when the outcome is Approved (HR's half, then payroll's monthly basic through payroll's own
upsert), so an approved request cannot sit un-applied. Two things worth copying and one trap:

- **TRAP 7 — the SEEDED definitions do not bar the initiator.** `EnsureHrWorkflowsSeededAsync`
  builds one-step, role-routed definitions with no `PreventInitiatorApproval`; where the requester
  holds one of the named roles (HR is in most lists) the engine lets them approve their own record.
  Put the record-level rule in the SERVICE, before `CanUserApproveAsync`: lane S refuses the
  requester and the subject; lane F3 narrowed to the owning unit's head. Assume the definition
  alone does not protect you.
- **TRAP 5 cannot be asserted on a tenant whose default definition is seeded** — retiring a seeded
  definition is TRAP 6's re-activation problem. Guard it in code; assert instead that exactly one
  live definition exists, asked the way the engine asks (normalised type, `IsActive` AND
  `Published`, every page).
- Save the decision BEFORE applying it, then apply in a second SaveChanges: a failure in applying
  must never lose the approval. Stamp each half as it goes through so a retry repeats nothing.
