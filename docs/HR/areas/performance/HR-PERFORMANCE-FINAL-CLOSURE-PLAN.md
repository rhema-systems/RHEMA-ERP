# HR Performance — final closure plan

**What this is:** the single tracking document for the final, end-to-end closure of the
Performance/Appraisal module. It folds together the three earlier documents in this folder
(`HR-PERFORMANCE-SYSTEM-GUIDE.md` findings P-1…P-70, `HR-APPRAISAL-SETTINGS-AUDIT.md`,
`HR-PERFORMANCE-SEED-GAP-PLAN.md` S-1…S-17), three fresh audits run on 2026-09-28 (backend logic,
frontend wiring, notifications/jobs/handlers) and a **same-day review of this plan** — five more
sweeps (authorization and data exposure, scoring-design dependencies, seed/demo/harness impact, every
other HR document, and the live status of P-1…P-70), with every High item and every build-breaking
claim re-read in source. The whole set is turned into sixteen ordered lanes with checkboxes.
**41 of the guide's code findings are still live at HEAD 143ae2efc**; P-27 was already fixed
(0ef42c223, 2026-08-09, before the guide was written). § 8 has the status of every P-finding.

**Scope decision, 2026-09-28:** close all of it — the user asked for the performance appraisal module
to be closed out completely. Nine decisions were taken before any code was written (§ 1a). The review
raised nineteen more, and the user settled all nineteen the same day, each on its recommendation
(§ 1b). Build starts in a fresh session.

**START HERE:**
1. § 1 is settled. One question stays outside this plan: D-18, the probation admin door, is the
   finish plan's decision (its lane 9). Only F1 waits on it.
2. ~~Migration batch 1 (§ 6): the user scaffolds, it is rewritten as guarded SQL, the user builds.~~
   **Done 2026-09-29** and applied to UAT; read § 6's State block before lane A — it lists what the
   batch decided that later lanes build on.
3. Build in the order of § 2. A lane is done when its harness suite is green twice, the regression
   set holds its count, the three documents in this folder carry the new state, and the slice is
   staged (the user commits).
4. **Verify every row against the source before building it.** The review found P-27 already fixed,
   E1's "no FE caller" false, and F3, F5, F7, G and H resting on premises that were wrong in source.
   Line numbers are as of 2026-09-28.

**House rules that apply to every lane** (from the HR programme's memory, not repeated in each lane):
the user runs builds — never `dotnet build`; stop `ErpSystem.Api` by command line before the user
builds; migrations are scaffolded by the user and rewritten as guarded SQL; the harness lives at
`D:\Rhema\TDC ERPS\dev-harness\hr-performance\` outside the repo (Staging + JWT key + UAT connection
string, two HR users, clamd stub for uploads — see its README); **UAT is the demo database, so no
fixture may resolve to real staff** (lane S8); never `python -`; PowerShell bulk edits mangle UTF-8;
a suite that dies at a gate keeps its recorded count — read the failure list; **a demo-pack scenario
or harness suite that a lane's new rule breaks is fixed in that lane's slice** (lane S), or the next
UAT rebuild goes red.

---

## 1. Decisions

### 1a. Decisions taken with the user (2026-09-28)

| # | Question | Decision |
|---|---|---|
| 1 | The 14 settings that do not enforce what they say | **Implement 12 at their write paths; remove two** — `IsManagerAuthoritative` (contradicts the weighted model) and `RequireDevelopmentPlanUpdate` (no development-plan section exists on the self form). Relabel `AllowSelfSoftSkillRating` to what it does |
| 2 | Notifications are portal-only in-app rows; goals, check-ins, proposals and recommendations tell nobody | **Move actionable events onto the platform notification topics** (in-app + email, desk bell), following leave's round-5 pattern. *Amended by the review:* the portal feed already merges platform notifications, so a person with a login gets **only** the platform row; the employee-keyed row stays for people without a login (D-21, lane G) |
| 3 | Nothing runs on a schedule; `AutoLockOnDeadline` is a button | **Nightly performance sweep** per tenant, honouring the setting, with a run/dispatch log and a screen |
| 4 | Two records for one fact / chains that end in a receipt | Manager checkboxes create recommendations; PIP Termination/Demotion/Transferred raises an EmploymentActionProposal; **segregation of duties in code** + MD read access; salary proposal "mark applied" reads the salary-change approval back. *Amended by the review:* that request (`EmployeeSalaryChangeRequest`) is HR's own entity, not payroll's (F5); no seeded HR definition bars the initiator, so segregation needs a submitter column and a per-type approver rule (F3, D-12) |
| 5 | Evaluation sequence | **Strict**: a manager cannot submit until the self-evaluation and the peer minimum are in (drafts allowed); HR's audited manual advance is the way past a missing step |
| 6 | HR desk goal actions | **Hide Submit/Approve/Reject/Lock from HR**; owner (Submit) and direct manager only. No HR override — goal approval stays bespoke and on the reporting line |
| 7 | Stakeholder feedback: what a person is appraised on is agreed between staff and manager; HR is governance | The module already routes goal approval to the direct manager and HR approves nothing. What contradicts the stakeholders is that **only a KPI on the template is scored** (a locked goal only overrides that item's target, and only if locked before generation; goal weights never enter the score). **Build goal-driven KPI scoring now** (lane L), not per-employee templates. How templates and personal KPIs fit together: D-15 |
| 8 | Enterprise essentials missing | **Printable appraisal record (PDF)** and **reassign evaluator mid-cycle** are in scope (lane M) |
| 9 | Version 2 | Rating-distribution enforcement / forced ranking, dotted-line second manager, evaluation delegation, probation-type branching, continuous kudos, external raters. The review adds lane N's v2 list |

### 1b. Decisions raised by the review — settled with the user, 2026-09-28

The user took every recommendation: seven in two rounds of questions (D-16 was asked as two), then
"accept the other twelve". D-12 records the refined rule the user chose.

| # | Question | Decision (2026-09-28) | Affects |
|---|---|---|---|
| D-10 | **Can an appraisal be withdrawn?** No status means "not appraised" (`AppraisalStatus` is Open/Draft/Active/Governance/Appealed/Completed/Closed, `HREnums.cs:1499`), and neither separation nor movements touch appraisals. With E1 (delete only unsubmitted, Admin-tier) and E2 (no close while anything is open, no force), one leaver — or the demo's 73 never-opened drafts — blocks a cycle for good | Add `Withdrawn` with reason, actor and date, allowed from Draft/Active/Governance; excluded from scores, dashboard denominators and close checks. `SeparationService.CompleteSeparationAsync` (:1240) withdraws an open appraisal; the transfer path flags it to HR | E1, E2, G, I, batch 1 |
| D-11 | **May HR restate a score at sign-off?** `AppraisalHRReview.AdjustedOverallScore`/`AdjustmentReason` reach no endpoint and nothing applies them (MAP :2383, :2410); `AppraisalAppealItem.RevisedScore` is dead; finish plan lane 5b (:945-953) owes this question | **No** — calibration and appeal are the two restatement paths. Drop the three dead columns | A16, J, batch 1 |
| D-12 | **Who approves when the author may not?** Seeded routing (`DatabaseSeedingService.cs:505-545`): salary and employment proposals → MD, TenantAdmin **and HR**; PIP and template → HR, Manager, TenantAdmin. The demo tenant has one HR user (`hr.head`) | Salary and employment proposals → **MD or TenantAdmin only**: their two seeded definitions move from the `executive` role set to the existing `mdOnly` set. `executive` also routes `HR_EMPLOYEE_SALARY_CHANGE_REQUEST` and stays as it is. **PIPs** → HR approves a plan a line manager wrote; a plan HR wrote goes to the employee's line manager or TenantAdmin. **Templates** → another HR officer, or TenantAdmin. The services enforce these rules on both paths. The seeder only adds, so existing tenants keep HR on the proposal stage, and the service rule is the control | F3 |
| D-13 | **Historical scores.** Completed appraisals were scored with unsubmitted drafts counted and item adjustments ignored, and the acknowledgment-only path left some with no score. Re-settling moves talent-pool ratings | **Report, then freeze.** A15's dry run lists stored vs corrected score, grade and talent rating. Finalised scores stay; HR restates an individual appraisal only through the audited reopen (D-17) | A15 |
| D-14 | **`AppraisalCycleStatus.InProgress`.** Written only by `PerformanceAppraisalDataSeeder.cs:314` (the demo cycle APC2026); no service assigns it; `IsCycleActive` (PAS :4157) already treats it as inactive | **Remove it**: migrate InProgress → Open; fix its six readers (`AppraisalCycleService.cs:423`, `AppraisalTemplateService.cs:880`, `CycleCoverageService.cs:255`, `HRCycleDashboardQueryService.cs:91`, PAS :2126, and the TS unions), the seeder and the demo claims. Settle **before** E1's "cycle Open" guards are written, or they lock the demo | E1, E2, S1, batch 1 |
| D-15 | **Templates and personal KPIs.** Do per-employee KPIs mean a template per employee? Should the KPI item type leave the template? | **Few templates, by population** (e.g. staff, supervisors and managers, probation). Assignment scopes and priority already exist (`AppraisalCycleTemplateService`, the cycle's Coverage tab). A template fixes the structure, section weights, rating scale, competencies and a **goals section**; each employee's goals section is filled from the goal plan agreed with the manager. **Keep the KPI item type, narrowed** to shared KPIs with one target for everyone on that template (attendance, safety, a corporate scorecard measure). Personal KPIs — even standard ones with personal targets — are **goals**, created from the goal library for consistency. **Drop the Tier 1 override** where a locked goal replaces a template KPI item's target (`EffectiveAppraisalConfigurationService.cs:310-322`); under lane L it would count twice | L0, S2 |
| D-16 | **Goal rows (lane L).** `GoalMeasurementType` is NumericAbsolute/PercentageTarget/Boolean/Range, with no "rated" kind; a goal with no target can only score through `ActualValue`, which scores 0 (`AppraisalScoring.cs:80-81`). Nothing locks a goal **set**: one goal at a time, direct manager only (GWC :375-424, :494-500), and HR's advance approves goals but never locks them (AWS :404-421) | A goal with a numeric target is **measured** (actual vs target/min/max); one without is **rated** on the tenant's overall grade scale. The set is locked by the manager's "lock goal set" action. At the goal-setting deadline the nightly sweep locks approved sets when `AutoLockOnDeadline` is on; otherwise the GoalSetting gate stays open and HR's audited advance locks the set | L2, L6, H2 |
| D-17 | **Reopen after finalise (P-44).** Today `ReturnToManagerAsync` (PAS :4472-4497) has no status check, so the API can reopen a finalised appraisal by accident; E1 closes that door and leaves none | An Admin-tier "reopen to HR review" with a mandatory reason, written to the rating history (N2); the employee and manager are told (G) | E1, G, I |
| D-18 | **Probation approvals.** Every `HR.*.Admin` approve door is held only by SuperAdmin/TenantAdmin and needs an employee link (finish plan lane 9, cross-module #25 item 1). F1's Extend/Confirm-Probation actions depend on it | Settle it in the finish plan before lane F | F1 |
| D-19 | **Effective date on proposals (P-54).** Neither proposal carries one | Salary: take it from the linked `EmployeeSalaryChangeRequest`. Employment action: add `EffectiveDate` (batch 2) | F5, batch 2 |
| D-20 | **Raw `POST /PerformanceAppraisals`.** Creates an appraisal with no criterion snapshot (finish plan 9.3; PAS :269-297); every harness fixture is built through it (`hr-performance/setup.mjs:178`) | **Delete it**; generation is the only door. Needs J's `Employee` target case so a fixture can generate one appraisal | E1, J, S8 |
| D-21 | **Notification store.** The portal feed already merges platform `Notification` rows (`EmployeePortalController.cs:724-790`; `NotificationTopicPublisher.cs:513-530` writes them), and the unread badge sums both stores. Rounds 4 and 5 set two rules: one digest per person per run plus one HR summary per run, and anyone who cannot be told goes to HR with the reason (691 of 2,377 UAT staff have no login) | Per recipient: a login → the platform topic only; no login → the employee-keyed `AppraisalNotification` row plus the HR summary. Sweep-originated notices are digests. **Withdraw batch 3**: the table is the record for people without a login (round 4 lane K-a, decision 3) | G, H, § 6 |
| D-22 | **How an item adjustment is stored for a KPI item.** A4/C4 write `NumericScore` (an achievement-% override); C6 said KPI appeals modify `ActualValue`. Under the `NumericScore`-first branch (PAS :864) a later `ActualValue` change would be ignored | One representation: an adjustment to a KPI item is an **achievement-% override in `NumericScore`**, flagged so every screen says "overridden by calibration/appeal". The employee's actual stays as recorded | A14, C4, C6 |
| D-23 | **Catalogue rows for performance.** Bulk operations (appraisal start, HR sign-off, goal approve and salary-proposal approve are 🔴; PIP, recommendation and employment action 🟡), reports (completion register, rating distribution, goal achievement, PIP register), the line-manager guide — all in the catalogue programme the finish plan excludes | **The three scale items**: the Appraisal Completion Register with export, the Rating Distribution report, and bulk HR sign-off (N7). The rest stays in the catalogue programme | N7 |
| D-24 | **Enterprise-practice additions (lane N) and the questions for TDC** | **Build N1–N6**; lane N's v2 list is recorded. The TDC questions go to `HR-OPEN-QUESTIONS-FOR-TDC.md` (lane K): their current appraisal form; whether a reviewing/countersigning officer signs after the manager; merit or increment policy by rating; eligibility of new joiners | N, K |
| D-25 | **One competency library.** Appraisal criteria (`AppraisalCriteriaController` serves `AppraisalCompetency`) and job architecture's `/api/competencies` are two stores; templates use the first | v2: make job architecture's catalogue the master and link appraisal criteria to it. Now: record it and retire nothing | J |
| D-26 | **P-19**: unit-goal attachments are tenant-readable, and a code remark says deliberately (`UnitGoalsController.cs:343-347`) | Keep (a departmental target is not personal data) and say so on the upload control | P18 |
| D-27 | **Check-in ↔ company objective link** (ledger :2178, finish plan lane 2e :494): TDC to confirm intent | Ask TDC with D-24's questions | K |
| D-28 | **Goal approval for an employee with no line manager.** GWC :148 refuses the submit; § 5 once proposed an HR fallback, which contradicts decision 6 and trips goal-approval-stays-bespoke's revisit trigger | Keep the refusal, word it "you have no line manager on record — HR has been told", and send HR a data-fix notice (lane G). HR fixes the reporting line; it does not approve | G, § 5 |

---

## 2. Lane status

| Lane | Title | Size | Carries (§ 1b, all settled) | State |
|---|---|---|---|---|
| A | Scoring integrity and the settle path | 2.5 days | D-11, D-13, D-22 (A14–A16 only) | ☐ |
| P | Privacy and access *(new — the review)* | 2 days | D-26 (P18 only) | ☐ |
| B | One gate evaluator (B1 first); settings enforced or removed | 3.5 days | — | ☐ |
| L | Goal-driven KPI scoring | 5 days | D-15, D-16 | ☐ |
| C | One appeal machine | 2 days | D-22 | ☐ |
| D | Peer nomination and evaluation integrity | 1 day | — | ☐ |
| E | Lifecycle guards (appraisal, cycle, template, settings, goals, calibration, PIP, conversations, definitions) | 4 days | D-10, D-14, D-17, D-20 | ☐ |
| F | Recommendations, proposals, probation, PIP chain, segregation of duties | 2.5 days | D-12, D-19; **F1 waits on D-18** (the finish plan's lane 9) | ☐ |
| G | Notifications on the platform topics | 5 days | D-21, D-28 | ☐ |
| H | Nightly sweep and the advance path | 2.5 days | D-16, D-21 | ☐ |
| I | Frontend wiring | 5 days | — | ☐ |
| J | Dead code and contracts | 1.5 days | D-11, D-25 | ☐ |
| M | Printable record and reassign evaluator | 3.5 days | — | ☐ |
| N | Enterprise-practice additions *(new, severable)* | 3 days | D-23, D-24 (the v2 items wait on TDC's answers) | ☐ |
| S | Seed, demo data and harness *(new)* | 4 days | D-14, D-15 | ☐ |
| K | Docs, registers, harness, memory, finish plan | 3 days, continuous | — | ☐ |

**Order:** A → P → B1 → L → B2–B8 → C → D → E → F → G → H → I → J → M → N → S → K.
- **B1 comes before L**, because L's goal-set gate lives in `AppraisalGates`.
- **P can start as soon as A is staged**; it touches none of the scoring code.
- **Lane S is continuous.** A demo-pack or harness break caused by a lane's new rule is fixed in that
  lane's slice: B's gates break `061` on the next rebuild; E's guards break `sliceC`/`sliceE`; F's
  segregation breaks `061`/`070`/`100`. The shape gaps S-1…S-17 are built at the end.

**Size:** about **50 working days, ten weeks** (47 without lane N). **Migrations:** batch 1 sits at
the start of lane A and batch 2 at the start of lane F; batch 3 is withdrawn (D-21). **Batch 1 is
done and applied to UAT (2026-09-29, § 6 State); lane A is next.**

---

## 3. The headline findings (verified in source, not only by the audit agents)

Abbreviations: PAS = `src/ErpSystem.Core/Services/HR/Appraisal/PerformanceAppraisalService.cs`,
AWS = `AppraisalWorkflowService.cs`, CSS = `CalibrationSessionService.cs`, PNS =
`PeerNominationService.cs`, PES = `PeerEvaluationService.cs`, EGS = `EmployeeGoalService.cs`,
GWC = `GoalWorkflowCommandService.cs`, EAC = `EffectiveAppraisalConfigurationService.cs`,
MAP = `src/ErpSystem.Core/Services/HR/Extensions/AppraisalMappingExtensions.cs`, PAC =
`src/ErpSystem.Api/Controllers/HR/PerformanceAppraisalsController.cs`, DTO =
`src/ErpSystem.Core/DTOs/HR/AppraisalDTOs.cs`, all under `Services/HR/Appraisal/` unless stated.
Line numbers are as of 2026-09-28.

1. **No item-level score adjustment has ever changed a result.** Calibration's per-criterion
   adjustments (CSS ~:765-800) and HR's appeal `CriteriaModifications` (PAS ~:3378-3405) write only
   `CriterionScore.NumericScore`; `RecomputeEvaluatorTotalAsync` (PAS ~:965-990) sums the stored
   `WeightedScore`. Broader than P-40 (which was about KPI items only).
2. **HR finalise wipes a committed calibration restatement** (P-39): `ApproveAndFinalizeAsync` →
   `CalculateOverallScoreAsync` recomputes from the legs unconditionally (PAS :167-231) and
   pushes the uncalibrated number to talent pools (PAS :4437). Talent sync runs on one of six
   completion paths.
3. **Unsubmitted drafts count.** `CalculateOverallScoreAsync` has no `SubmittedDate` filter
   (PAS :200-206): a saved-but-unsubmitted self or peer draft moves the final score.
4. **The acknowledgment-only path never computes a score.** HR review and calibration off,
   acknowledgment on: manager submit → Governance → acknowledge → Completed with `OverallScore`
   null, no grade, no talent sync.
5. **Appeal remand is unreachable**: remand sets Active but never clears the manager's
   `SubmittedDate` (only return-to-manager does, PAS :4503), so `SaveManagerEvaluationAsync` refuses
   (:2399) and the remand re-evaluation branch (:2502-2514) is dead. A second legacy appeal machine
   (`POST /{id}/appeal` :311-353, `POST appeal/{id}/resolve` :453-500) has no guards. Appeal lookups
   are unordered `FirstOrDefault` (:3114, :3239, :3353, :3508).
6. **No status guards** on `ApproveAndFinalizeAsync` (:4354) and `ReturnToManagerAsync` (:4472);
   raw `PUT /{id}` writes `OverallScore` from the body (MAP :312); raw evaluator/criterion CRUD
   (:573-802) re-parents records; two identical transition tables (AWS :27-70, PAS :533-541) and
   five direct `Status =` writes that bypass both.
7. **Raw `POST/PUT api/PeerNomination` copies `NominationStatus`, `NominatedById`,
   `PeerEmployeeId`, `AppraisalId` from the body** (MAP :1098-1121; PNS :216/:265 do not override).
8. **Extend-Probation recommendations always crash**: `ProbationHandlers.cs:173` passes
   `CurrentUserProvider.UserId` into `ProbationService.ExtendAsync`'s `actorEmployeeId` (:380-381),
   which lands in `ProbationExtension.ExtendedById`, an Employee FK; the outcome service swallows it and
   the row sits Approved-not-Actioned.
   Confirm-Probation never completes on a seeded tenant (`ProbationService.ConfirmAsync` refuses while
   the PROBATION_PERIOD definition is published, which it is).
9. **No performance hosted service exists** (checked every `AddHostedService`); deadline reminders
   and auto-lock are buttons (`DeadlineEnforcementController`); every performance notification is an
   `AppraisalNotification` row shown only in the portal inbox; no email; goals, check-ins,
   proposals, recommendations and calibration commit tell nobody; deadline reminders go to every
   in-scope employee for every phase.
10. **No segregation of duties** on PIP, salary, employment-action, template or recommendation
    approvals; the MD role cannot read the proposals it approves; the generic `/workflow/inbox`
    approve strands the four performance entity types at PendingApproval.
11. **The appraisee can edit and close the manager's conversation record**
    (`AppraisalConversationsController.CanAccessConversationAsync` :84-87 grants write to
    `Appraisal.EmployeeId == me`); `GetHRReviewAsync` sends the manager's leg to the appraisee at any
    stage (:4138-4166; only the peer block is redacted).
12. **HR desk offers goal Submit/Approve/Reject/Lock that the server refuses for HR**; a
    Custom-frequency cycle can never get interim reviews (no screen calls `interimReviewService.create`);
    91 client service methods have no caller.
13. **Only a KPI on the template is scored** (decision 7): `EffectiveAppraisalConfigurationService`
    :290-345 resolves targets from the locked goal with the same `KpiDefinitionId`, else the template
    default; `EmployeeGoal.Weight` is read by no scoring code; year-end goal assessments are stored
    beside, not inside, the score.

**Added by the review (2026-09-28):**

14. **Six High privacy exposures** that the audits missed (lane P):
    - The appraisee reads every evaluator row through `GET …/{id}/evaluations` (PAC :466; the access
      check admits the appraisee, :743-753): peer names and scores under anonymity, and the manager's
      total, notes and recommendation before sign-off.
    - The appraisee reads the manager's `Recommend*` flags (PIP and termination included), notes, and
      pre-calibration and overall scores through `GET …/{id}` (MAP :224-262).
    - Every signed-in staff member can read calibration sessions, the matrix, adjustments with
      rationale and per-criterion scores (`CalibrationSessionsController.cs:31`, class-level
      `InternalOnly` only).
    - Any Manager-role user can draft a PIP on any employee
      (`PerformanceImprovementPlansController.cs:41`).
    - PIP edit re-parents the employee, supervisor, HR owner and appraisal (MAP :609-623).
    - Anyone can create a check-in about anyone (`CheckInsController.cs:285-304`) and so become a
      party to that employee's goal updates.

    The same sweep found the Manager role holds no Performance policy, and tenant isolation holds
    everywhere it looked.
15. **Six of this plan's premises were wrong in source:**
    - E1's raw `PUT /{id}` has a screen caller: the HR review's **Correct dates**
      (`hr-review/[id]/page.tsx:103`).
    - F3's "the engine already has `preventInitiatorApproval`" is false on every seeded HR definition
      (`DatabaseSeedingService.cs:596`).
    - F5's salary-change request is HR's own entity (5491f0676), not payroll's.
    - F7's fix edited another developer's `WorkflowController`.
    - G's dual write would duplicate every notice in the portal feed.
    - H's nightly dispatch retry cannot run without a signed-in user (all six handlers).
16. **Section weight is not snapshotted, and the peer service scores on its own.**
    `LoadCriterionScoringAsync` reads `TemplateItem.Section.Weight` live (PAS :902, :926-933), and PES
    keeps a second copy of the arithmetic (:428-496). A settle that recomputes from raw inputs would
    re-score an old appraisal on today's template.
17. **Lane L's first schema would not have built.**
    - `CriterionScore.TemplateItemId` and `AppraisalCriterionScoreSnapshot.TemplateItemId` are
      required.
    - A null `CalibrationRatingAdjustment.TemplateItemId` **means the overall** (CSS :733, :875), so
      adjusting a goal row would have overwritten the overall score.
    - About 40 sites key a dictionary by `TemplateItemId` and throw on the first null.
18. **Goals set before generation are invisible to the appraisal.** A goal links to an appraisal only
    if the appraisal exists when the goal is created (EGS :221-229); generation never back-links.
19. **An appraisal cannot be withdrawn** (D-10). E1 and E2 as first written made a cycle with one
    leaver impossible to close.
20. **Snapshot-less appraisals have two live sources and no repair.** Generation swallows snapshot
    failures (`AppraisalCycleService.cs:1366-1377`), and the raw create never snapshots
    (PAS :269-297). This is P-13, and the cause of Rule 8.
21. **The manager and peer forms show a different target from the one scored.** PAS :4840 and
    PES :584 read the live goal; the self form reads the snapshot (PAS :4713). Two related defects:
    - The frontend KPI preview ignores min/max and clamping (`appraisal-run.ts:965-972` vs
      `AppraisalScoring.cs:77-102`).
    - Grade bands may top out below 100 while every score input accepts 0–100.
22. **Lane B's gates are all ON in the seeded demo profile** (`PerformanceAppraisalDataSeeder.cs:170-192`:
    min 3 goals; kick-off, mid-year and final conversations; manager goal approval).
    `061-appraisal-evaluations.mjs` holds no mid-year conversation, so without lane S the next UAT
    rebuild goes red.

---

## 4. Lanes

### Lane A — Scoring integrity and the settle path

**Design: two single points of truth.**
- **`AppraisalGates`** (lane B) owns the pipeline.
- **`PerformanceAppraisalService.SettleScoreAsync`** is the only writer of `OverallScore`, in this
  order:
  1. Recompute every `WeightedScore` from raw inputs through the submit-path
     `CalculateAndSetWeightedScoreAsync` (PAS ~:860-880).
  2. Recompute evaluator totals via `RecomputeEvaluatorTotalAsync`.
  3. Take the legs: **submitted** evaluations with a scored item, grouped by role.
  4. `computed = AppraisalScoring.OverallScore(legs)` — now `decimal?`, null when nothing scored.
  5. `OverallScore = CalibratedOverallScore ?? computed`, graded via the one
     `PerformanceRatingResolver`, then saved.
  6. Publish to `TalentRatingSyncService` when final (`Completed`/`Closed`, or `Governance` with a
     submitted HR evaluation).

`CalculateOverallScoreAsync` stays as a one-line delegate. `SyncLifecycleAsync(appraisalId)` glues
gates and settle: after any pipeline write → `Resolve` → `ExpectedMajorStatus` →
`AppraisalLifecycle.EnsureTransition` → if Completed → settle + publish. `DetermineNextStatusAsync`
(PAS :1945-2009) and the AWS Governance→Completed block (:622-639) are deleted in its favour.

- [ ] A0 **Freeze the section weight into the snapshot** (moved here from E4 by the review).
      `PerformanceAppraisalCriterionConfig` gains `AppraisalTemplateSectionId` + `SectionWeightUsed`
      (batch 1), backfilled from the live template for existing snapshots. Every share is computed
      from them:
      - `LoadCriterionScoringAsync` (PAS :898-914);
      - the legacy resolvers (PAS :921-958, :1889-1937);
      - PES :437, :471-472, :492.

      It must land before A3, because settle recomputes every weighted score from raw inputs.
- [ ] A1 `AppraisalScoring.OverallScore` returns `decimal?`; nothing grades or syncs a null.
- [ ] A2 `IPerformanceRatingResolver.ResolveGradeDefinitionIdAsync`; bands loaded once, ordered by
      min desc; delete `PAS.ResolveGradeAsync` (:242-255) and `CSS.ResolveGradeAsync` (:805-818);
      overlapping bands refused at definition save (P-6). The resolver requires `MappedRating`
      (`PerformanceRatingResolver.cs:75`) — a grade definition without one must be refused at save,
      not silently ungraded.
- [ ] A3 `SettleScoreAsync` as above; `ApproveAndFinalizeAsync` settles inside its transaction with
      `publish:false` and publishes after commit (a swallowed EF failure inside a retrying
      transaction poisons tracked entities); delete the :4433-4442 block.
- [ ] A4 Calibration commit (CSS :719-740):
      - Item adjustments write `NumericScore` for **both** item types (D-22). A KPI item with
        `NumericScore` set is scored as an achievement-percent override by the `NumericScore`-first
        branch at PAS :864.
      - Adjustments are validated against the item's own scale at `AddRatingAdjustmentAsync` (:397)
        — see A11.
      - `CalibratedOverallScore = overallAdjustment?.AdjustedScore`; null clears an earlier session's
        override.
      - One settle per scoped appraisal **at the calibration step**; the others are reported as
        skipped, not stamped `IsCalibrated`.
      - The per-item `SaveChangesAsync` at :798 is removed.
- [ ] A5 `AdjustedScore` = post-resolution overall only when it changed, else null;
      `AppraisalAppeal.OriginalOverallScore` captured at submit-appeal; `GetAppealStatusAsync`
      (:3167) and `GetEmployeeAppealOutcomeAsync` report it.
- [ ] A6 MAP :312 stops assigning `OverallScore`; `ToDto` (:224) exposes `CalibratedOverallScore`.
- [ ] A7 Every settle call site is wired:
      - HR finalise;
      - completion without HR review (:1986);
      - acknowledge → Completed;
      - appeal Upheld/Rejected;
      - post-remand finalise;
      - manual/auto advance → Completed;
      - calibration of a Completed appraisal.

      `SuccessionNominationHandler.cs:128` reads the settled score. Today calibration (CSS :736) and
      appeal resolution (PAS :3403, :3408, :3736) change the score without syncing.
- [ ] A8 `TalentRatingSyncService` recency check (an older cycle's finalisation must not overwrite a
      newer rating); null score leaves the rating untouched and logs.
- [ ] A9 Analytics "finalised scores" filter by status, not `OverallScore != null`
      (`PerformanceAnalyticsService.cs:61`).
- [ ] A10 **One arithmetic path.** PES's own copy is replaced by the settle path's helpers:
      `CalculateAndSetWeightedScore` (:428-461), `RecomputePeerTotal` (:484-496) and
      `LoadCriterionSnapshotAsync` (:464-475). `AppraisalScoring.cs` stays the single arithmetic
      home.
- [ ] A11 **Scores validated on the item's own scale.** Bands may top out below 100 —
      `AppraisalTemplateService.cs:797-817` checks only 0 ≤ low ≤ high ≤ 100 and no overlap. Inputs
      are validated 0..100 (PAS :1433, :2352; PES :230; DTO :1997; `EvaluationScoreForm.tsx:219-225`),
      and achievement is score ÷ top band (PAS :866-868), so an item can exceed 100 %. These are all
      validated from 0 to the item's top band:
      - self, manager and peer inputs;
      - calibration item adjustments;
      - appeal modifications (appeal `NewScore`, DTO :2956, has no range check today).
- [ ] A12 **The target shown is the target scored.**
      - Manager and peer section builders read the snapshot's target/min/max, as the self form
        already does (:4713). Today they read the live goal: PAS :4840-4842, :4941; PES :584-586;
        HR review PAS :4121.
      - The frontend preview `kpiAchievementPercent` (`types/hr/appraisal-run.ts:965-972`) mirrors
        `AppraisalScoring.KpiAchievementPercent` (min→target segment, max cap, clamp).
- [ ] A13 `CriterionScore.NumericScore` is `int?` while adjustments are `decimal?`: round
      explicitly (or widen in batch 1), never truncate silently.
- [ ] A14 KPI adjustments per D-22: the `NumericScore` override is flagged on the row, so the forms,
      HR review, appeal page and PDF say "overridden by calibration/appeal".
- [ ] A15 Historical scores per D-13: a read-only dry run of the settle over every Completed/Closed
      appraisal (stored vs settled overall, grade and talent rating) written to a report. Nothing is
      restated without a decision.
- [ ] A16 D-11: drop `AppraisalHRReview.AdjustedOverallScore`/`AdjustmentReason` and
      `AppraisalAppealItem.RevisedScore` (batch 1), with their DTO members and MAP :2383, :2410.

**Risks** (carried from the original design package):
- Legacy appraisals with no snapshot fall back to per-item resolvers; thread the scoring map through.
- Some reads assumed Active during remand (`GetManagerEvaluationContextAsync` `isEditable`,
  `TeamMemberAppraisalDto`).
- `HRCycleDashboardQueryService`'s fixed include lists need `ManualAdvanceLogs`.

**Fixture** (`buildClosureFixture()` in `setup.mjs`):
- profile "closure": all gates on, appeals 7 days, peers off, final conversation off;
- profile "lite": self + manager only, no calibration, no HR review, acknowledgment on — for the
  completion-without-HR paths;
- a template with one KPI and one competency at equal weight, two overlapping grade bands, and one
  talent-pool member.

The fixture's appraisals are generated through an `Employee` target (J), never the raw create.

**Assertion** (`run-final-scoring.mjs`):
- KPI actual 50/target 100 + competency 80 at equal weight → manager 65.
- Item adjust competency → 100 → 75.
- Overall adjust 70 → overall 70, `preCalibrationScore` 65; HR approve → still 70; talent-pool rating
  reflects 70.
- No submitted legs → null overall and no grade.
- A saved-not-submitted self-evaluation with scores does not move the overall.
- The identical-value invariant with THREE peers holds, through the peer path as well.
- Editing a template section's weight after generation leaves the settled score unchanged.
- The "lite" profile settles a non-null score, grade and talent rating at acknowledgment — one
  assertion per settle path in A7.
- A score above the item's top band is refused.

### Lane P — Privacy and access *(new, from the review)*

P1–P6 were re-verified in source on 2026-09-28. P7–P17 come from the authorization sweep with file:line
— **verify each before building.**

- [ ] P1 **The evaluator/criterion CRUD block** (PAC :433-664, eight routes). Delete it now rather
      than in E1: none has a screen caller, and `GET …/{appraisalId}/evaluations` (:466) hands the
      appraisee every evaluator row — `EvaluatorName`, `TotalScore`, `OverallNotes` and
      `Recommendation` (MAP :338-360) — because `CanAccessAppraisalAsync` (:743-753) admits the
      appraisee.
- [ ] P2 **An appraisee-facing projection.** Before sign-off (Completed/Closed, or Governance with HR
      signed) it withholds `Recommend*`, `RecommendationNotes`, `RankIn*`, `PreCalibrationScore`,
      `AdjustedScore`, `OverallScore` and the grade. It applies to `GET …/{id}` and
      `/employee/{employeeId}` (PAC :127-170, MAP :224-262), `my-appraisals` (PAS :1312) and
      `me/trend` (`PerformanceAnalyticsService.cs:104-121`). Lane I's "hide overallScore on the list"
      becomes the server's job.
- [ ] P3 **Calibration reads** — paged, {id}, by-cycle, participants, adjustments,
      adjustments/appraisal/{id}, matrix, appraisals/{id}/criteria, and attachments with download
      (`CalibrationSessionsController.cs:87-140, :349-367, :453-492, :589-608, :641-715`). Each needs
      the Read policy **or** panel participation, and a participant sees only the appraisals in the
      session's scope. The "reads stay open to any authenticated user" remark (:638-639) is
      rewritten.
- [ ] P4 **PIP create** (`PerformanceImprovementPlansController.cs:41` — `AuthorRoles` includes
      Manager; `PerformanceImprovementPlanService.cs:278-317` checks only that the supervisor exists).
      A manager opens a PIP only for a direct report. `SupervisorId` defaults to the employee's
      manager and only HR changes it; `HROwnerId` must hold HR.
- [ ] P5 **PIP update** (MAP :609-623): `EmployeeId`, `AppraisalId`, `SupervisorId`, `HROwnerId`,
      `Status` and `Outcome` are never copied from the body; Draft edits only (Active per E7).
- [ ] P6 **Check-in create** (`CheckInsController.cs:285-304` checks only the conductor): the subject
      must be the caller's direct report, or the caller for a self-requested check-in, or the caller
      must hold HR. Check-in update (:327-352; MAP :1963-1977) cannot change `EmployeeId` or
      `ConductedById`, because `Redact` (:168-176) trusts `ConductedById`.
- [ ] P7 Goal-update PUT (MAP :2024-2025; `CheckInService.cs:286-310`): `CheckInId` and
      `EmployeeGoalId` are never taken from the body (E10 adds the ownership check on add).
- [ ] P8 Goal progress PUT/DELETE (`EmployeeGoalsController.cs:621-667`; EGS :448-495; MAP
      :1900-1902): `EmployeeGoalId` is never taken from the body, and only the recorder (or HR) edits
      or deletes an entry.
- [ ] P9 Attachment delete — appraisal (PAC :856-879 → PAS :1163-1177) and check-in
      (`CheckInsController.cs:624-647` → `CheckInService.cs:400-411`): only the uploader or HR, and
      only before completion.
- [ ] P10 Development plans (`DevelopmentPlansController.cs:73-83, :307-365`;
      `DevelopmentPlanService.cs:179-225`): the subject cannot delete or complete a plan authored by
      their manager or HR.
- [ ] P11 `GET /UnitGoals/{id}/employee-goals` (`UnitGoalsController.cs:421-435`;
      `UnitGoalService.cs:243-264`): per-employee rows only for HR and the managers in that unit's
      line; counts for everyone else.
- [ ] P12 `self-evaluation-context` (PAC :951-972; PAS :1341, :1368-1396): the manager sees self
      scores only once they are submitted, and then per `ShowSelfScoreToManager` (B2).
- [ ] P13 PIP meeting comment and forms (`PipMeetingController.cs:362-393, :188-208, :235-252`;
      `PerformanceImprovementPlansController.cs:1199-1202`; MAP :683-684): the right of reply is the
      employee's own write, and supervisor forms cannot set `EmployeeComments` or `ConductedById`.
- [ ] P14 Peer nomination writes (`PeerNominationController.cs:49-61, :227, :266, :298`): the
      nominated peer cannot edit or delete the nomination. Batch nominate (PAC :1324-1338; PNS
      :98-116, :430-441) honours `PeerNominationMode` and records the real nominator (D1/D4).
- [ ] P15 `GET /CheckIns/paged` (:179-194) is redacted like every other read, and `IsHr`
      (:120-121) becomes the Read-policy check. Today TenantAdmin, Admin and "HR User" see private
      notes there and are refused elsewhere. P-27 is then fixed on every path.
- [ ] P16 `GET /AppraisalCycleTarget/{id}/exclusions` (`AppraisalCycleTargetController.cs:206-221`):
      HR read only, because it carries the reasons.
- [ ] P17 `GET /AppraisalWorkflow/{id}/phase` and `/editable/{role}`
      (`AppraisalWorkflowController.cs:27-46, :84-103`): a party to the appraisal, or HR.
- [ ] P18 P-19 per D-26: keep the tenant-wide read and say so on the upload control.

The fallback-path self-approval found by the same sweep is lane F3.

**Assertion** (`run-final-privacy.mjs`): one matrix per route across appraisee, peer, unrelated staff
member, manager of another unit, and HR — expected status, **and the field absent from the body**, not
only hidden. No peer name appears in any appraisee payload while `PeerReviewsAnonymous` is on.
`privateNotes` are absent on every check-in path for everyone but the conductor and HR.

### Lane B — One gate evaluator; settings enforced or removed

**B1 is built before lane L**; B2–B8 come after it.

**`AppraisalGates`** is pure static. It extends `AppraisalSubStatusResolver` in
`AppraisalAdvanceHelpers.cs` and keeps the old name as a forwarding alias. Its members:
- `Pipeline(settings)` — honours `HRReviewTiming`;
- `Resolve(appraisal, settings)`;
- `Check/EnsureAt(step)` — throws `InvalidOperationException` naming the blocking step;
- `ToPhase`, `ExpectedMajorStatus`, `CanFileAppeal`, `IsWaived(a, step)`.

`AWS.GetCurrentPhase` (:142-229) becomes `ToPhase(Resolve(...))`, and
`AppraisalCycleService.DetermineCurrentPhase` (:1080-1100) returns the modal `Resolve` result.

**Gate changes:**
- A remanded appeal resolves to `AppealUnderReview` regardless of `Status`.
- The PeerNomination gate counts `!= Rejected`.
- The PendingConversation gate exists only when `RequireFinalConversation &&
  !AllowAcknowledgmentWithoutConversation`.
- The GoalSetting gate requires the goal set locked when the template has an `EmployeeGoals` section
  (lane L). It also counts goals by employee + cycle, not by `appraisal.Goals` (L2).

Write path → step required (`EnsureAt`), then `SyncLifecycleAsync`:

| Write path | Step |
|---|---|
| self submit (PAS :1437) | SelfEvaluation |
| peer draft/submit (`PeerEvaluationService`) | SelfEvaluation in `AfterSelfEval` mode, else PeerEvaluation |
| manager submit (PAS :2485) | ManagerEvaluation (bypass when `Status==Appealed && CurrentAppealStatus==Remanded`) |
| calibration commit, per appraisal | PendingCalibration (else skip + report) |
| HR finalise | PendingHRReview |
| conversation complete | none, then sync |
| acknowledge (replaces :2745-2761) | PendingAcknowledgment |
| submit appeal | `CanFileAppeal` |
| manual advance | target must equal `Resolve()` |

- [ ] B1 `AppraisalGates` + `SyncLifecycleAsync` + the table above.
- [ ] B2 Settings, each at its write path:
  - [ ] `EnableAppeals`/`AppealWindowDays` → `SubmitAppealAsync`; window from
        `EmployeeAcknowledgedDate ?? hrEval.SubmittedDate ?? UpdatedAt` (not cycle end);
        `GetMyAppraisalsAsync` :1288-1296 uses the same `CanFileAppeal`; portal reads `canFileAppeal`.
  - [ ] `ShowPeerScoresToManager` → `GetManagerPeerEvaluationReviewAsync` (:2653) nulls peer scores
        until the manager has submitted.
  - [ ] `ShowSelfScoreToManager` → `GetManagerEvaluationContextAsync` nulls self item scores + self
        total until the manager has submitted (server-side; the React check stays).
  - [ ] `ShowScoreBreakdownToEmployee` → every employee-facing DTO strips manager/peer criterion
        scores (`GetAppealPageDataAsync`, `GetAppealStatusAsync`, `GetEmployeeAppealOutcomeAsync`, the
        `/me/performance/appraisals/[id]` read); **regardless of the flag, the manager's leg is
        withheld from the appraisee before HR sign-off** (fixes the leak at :4138-4166; the page's
        comment at `me/performance/appraisals/[id]/page.tsx:37-38` claims the server already does).
  - [ ] `MinGoalsPerEmployee` → first evaluation submit refused below it (symmetric with Max);
        `meetsMinGoalCount` surfaced in the manager governance view.
  - [ ] `RequireManagerGoalApproval` → when off, goal submit lands Approved directly.
  - [ ] `RequireKickOffConversation` / `RequireMidYearConversation` / `RequireFinalConversation` →
        first-eval submit / manager submit / acknowledge-or-finalise refused without a completed
        conversation of that type; `CreateAppraisalConversationDto.Type` no longer defaults to KickOff.
  - [ ] `AllowAcknowledgmentWithoutConversation` → acknowledge requires a completed FinalReview when off.
  - [ ] `PeerEvaluationOpenMode` → peer draft/submit refused before self-eval submit in `AfterSelfEval`
        (`IsEditableByRole` gains a caller; adds `manager` when remanded).
  - [ ] `AllowPeerKpiEvaluation` → enforced on draft save (:244-279) as well as submit.
  - [ ] `AllowSelfSoftSkillRating` → keep behaviour; form label "Employees must score every
        behavioural criterion before submitting"; delete dead local PAS ~:1533.
  - [ ] `AppraisalCompetency.RequireEvidence` → self/manager/peer submit refuse a scored item without
        an evidence link (the "required" marker at `EvaluationScoreForm.tsx:255` becomes true).
  - [ ] `KpiDefinition.TolerancePercent` → used by `KpiAchievementPercent` (within tolerance = 100%).
        The demo KPIs carry 5 (`PerformanceAppraisalDataSeeder.cs:115`), so the demo scores move —
        lane S re-baselines them.
- [ ] B3 Remove `IsManagerAuthoritative` and `RequireDevelopmentPlanUpdate`: entity, DTOs, MAP,
      form (`settings/[id]/page.tsx:485-490, 657-661`), the writers of
      `EvaluatorEvaluation.IsAuthoritative` (`AppraisalCycleService.cs:734`, PAS ~:2391) and its only
      reader `.OrderByDescending(e => e.IsAuthoritative)` (PAS :642). Drop the column too (batch 1),
      since it has no writer afterwards. The seeder sets both settings
      (`PerformanceAppraisalDataSeeder.cs:162, :189`), so S1 goes in the same slice or the build
      fails.
- [ ] B4 Finalise honours `RequireSelfEvaluation` / `RequirePeerReviews` (PAS :4371/:4378 are
      unconditional today), as do `CanProceedToHRReview` (:4088) and `IsReadyForHRReview` (:4299).
- [ ] B5 Delete the dead Peer/HR arms of `DetermineNextStatusAsync` with the method itself.
- [ ] B6 Settings profile validation: Min≤Max peers/goals, deadline bands ordered, weights sum 1;
      `IsDefault` flag replaces "newest row" (P-2) — batch 1 column; backfill rule in S9.
- [ ] B7 **The gates suite flips every gate setting, not only the 14.** B1 re-implements the code
      behind the 36 already-enforced settings, so all of these are covered both ways:
      - `RequireSelfEvaluation`, `RequireManagerEvaluation`, `RequirePeerReviews`, `Min/MaxPeerEvaluators`;
      - `RequireCalibration`, `RequireHRReview`, `HRReviewTiming`, `RequireEmployeeAcknowledgment`;
      - `RequireGoalSetting`, `Min/MaxGoalsPerEmployee`, `RequireManagerGoalApproval`;
      - the three conversation switches, `AllowAcknowledgmentWithoutConversation`;
      - `PeerEvaluationOpenMode`, `AllowPeerKpiEvaluation`, `AllowSelfSoftSkillRating`;
      - `EnableAppeals`, `AppealWindowDays`, `AppealReevaluationWindowDays`;
      - the three visibility flags, `RequireEvidence`, `TolerancePercent`.
- [ ] B8 **Transition report.** After B deploys, list the in-flight appraisals whose recorded state
      contradicts the new gates (e.g. manager submitted while the self-evaluation was not) for HR to
      waive through the audited advance. Nothing is moved automatically.

**Assertion** (`run-final-gates.mjs`, the two-position rule): for each gate toggled both ways, the
phase endpoint, the HR dashboard row and the 422 text name the same step:
- goals unapproved → self submit refused;
- self not submitted → manager submit refused;
- calibration required and uncommitted → approve refused;
- conversation required → acknowledge refused; with `AllowAcknowledgmentWithoutConversation=true`
  the same acknowledge succeeds.

With a visibility flag off, the field is absent from the payload, not only hidden. Every setting in
B7 is covered.

### Lane L — Goal-driven KPI scoring

**Facts:**
- `EffectiveAppraisalConfigurationService.BuildEffectiveConfig` (:290-345) resolves a template KPI
  item's target in two tiers: Tier 1 is the employee's **locked** goal with the same
  `KpiDefinitionId`; Tier 2 is the template default (`KpiTargetSource.Goal/Template`).
- The snapshot row is `PerformanceAppraisalCriterionConfig` (`TemplateItemId` required, `WeightUsed`
  int, KPI target/min/max, grade ranges).
- Scoring runs on snapshot shares (`AppraisalScoring.CriterionShare(sectionWeight, WeightUsed)`).
- `EmployeeGoal.Weight` (int) is read by no scoring code.
- Year-end `EmployeeGoalAppraisalAssessment` rows are stored beside the score.

- [ ] L0 **The template model (D-15).**
      - Templates are per population; assignment scopes already exist.
      - Section kinds are `Fixed` (competencies; shared KPIs with one target; free-text questions)
        and `EmployeeGoals`.
      - A template KPI item means "the same KPI and target for everyone on this template".
      - Delete the Tier 1 override (EAC :310-322), so a goal is never counted twice.
      - The template editor explains the split in its own text.
- [ ] L1 **Schema** (batch 1, completed by the review).
      - `AppraisalTemplateSection.Kind` (`Fixed=1`, `EmployeeGoals=2`; the entity default is Fixed,
        because the seeder's sections carry no kind).
      - `PerformanceAppraisalCriterionConfig`: `TemplateItemId` nullable, plus `EmployeeGoalId` (null
        FK, Restrict), `ItemLabel`, the goal's measurement fields (measured/rated flag per D-16,
        `Unit`, `MeasurementType`, display order), and A0's `AppraisalTemplateSectionId` +
        `SectionWeightUsed`.
      - `CriterionScore.TemplateItemId` **nullable** — the original plan had this and the staged
        version dropped it; without it a goal score cannot be inserted — plus `CriterionConfigId`
        (nullable, since legacy appraisals have no snapshot; Restrict; backfilled from appraisal +
        template item).
      - `AppraisalCriterionScoreSnapshot.TemplateItemId` nullable, plus `CriterionConfigId`.
      - `CalibrationRatingAdjustment.CriterionConfigId`, plus an explicit `IsOverall` flag
        (backfilled `TemplateItemId IS NULL`). Today a null item **means the overall** (CSS :712-713,
        :733, :875; `calibration/[id]/page.tsx:179, :182, :652`; `calibration.ts:16-19`), so adjusting
        a goal row would overwrite the overall score.
      - `AppraisalAppealItem.CriterionConfigId`.
      - Filtered unique indexes: config (appraisal, TemplateItemId), config (appraisal,
        EmployeeGoalId), CriterionScore (evaluation, CriterionConfigId), and
        EmployeeGoalAppraisalAssessment (goal, appraisal). The last matters because EGS :138 reads the
        assessments with `ToDictionary` and crashes on a duplicate.
- [ ] L2 **Snapshot at goal-set lock.**
      - `GoalWorkflowCommandService.Lock` and a new manager "lock goal set" action rebuild the
        `EmployeeGoals` section rows for the employee's appraisal in the cycle: one row per locked
        goal, with `WeightUsed = goal.Weight` normalised to 100 within the section, target/min/max
        from the goal, `KpiTargetSource.Goal`, and grade ranges from the KPI definition's defaults or
        the rated scale (L6).
      - The goal ↔ appraisal link is by **employee + cycle**. Today `PerformanceAppraisalId` is set
        only when a goal is created after its appraisal exists (EGS :221-229), and generation never
        back-links (`AppraisalCycleService` has no goal write). So goals set before generation are
        invisible to the forms, the gates and the advance path.
      - What locks the set when the manager does not is decided by D-16. The advance's GoalSetting
        arm (AWS :404-421) approves goals but never locks them.
      - Generation before lock leaves the section empty and the GoalSetting gate open.
      - Unlock removes rows while they are unscored and is refused once scored.
      - Goal edit and delete are refused while the goal is locked (EGS :243, :258).
- [ ] L3 **Forms and scoring.** About 40 sites keyed by `TemplateItemId` move to the config id. The
      review's sweep lists them:
      - scoring core: PAS :848-861, :898-914 (`ToDictionary` throws on a null key), :980-982;
      - form builders: PAS :4624-4700, :4760-4818, :4874-4921; PES :464-475, :507-563;
      - save paths: PAS :1550-1566, :2427-2446, keyed on `EvaluationItemInputDto.TemplateItemId`,
        a `[Required] Guid` at DTO :1992;
      - HR review: PAS :4098-4124, :4191-4215; manager peer review: :2681-2682;
      - appeals: PAS :2840-2856, :3124, :3302, :3638-3652, :3846-3873;
      - calibration: CSS :475-487;
      - frontend: `types/hr/appraisal-run.ts:254-255, :351, :421`, `toItemScores` :981-995 (null keys
        collide), `countScored` :1005, `EvaluationScoreForm.tsx:47, :137-142`,
        `team-appraisals/[id]/page.tsx:111, :152, :344`, `peer-reviews/[id]/page.tsx:66`,
        `self-evaluation/page.tsx:74`, `types/hr/appeals.ts`, `types/hr/calibration.ts`.

      The input type follows the row's measured/rated flag, not `kpiDefinitionId`
      (`appraisal-run.ts:946`). `AppraisalScoring` is unchanged. Goal achievement is entered once
      (`ActualValue`) and mirrored into `EmployeeGoalAppraisalAssessment` (self → `SelfFinal*`,
      manager → `ManagerFinal*`).
- [ ] L4 **Template editor.**
      - A section kind picker. An `EmployeeGoals` section has no items and shows its weight and rule
        text; activation counts it complete.
      - **P-8:** activation and submit-for-approval (`AppraisalTemplateService.cs:922-931, :323`)
        stop demanding grade bands on a weight-0 free-text item.
      - Coverage and calibration screens read labels from the snapshot.
- [ ] L5 **Governance at goal-set lock:** `Min/MaxGoalsPerEmployee` and weight sum 100 (P-20). The
      only existing weight guard (EGS :278-279, :306-307, :643-659) is never called, because the
      routed path is GWC :124-199. HR reads only. The guide chapter "who decides the KPIs" is written
      from this.
- [ ] L6 **Rated goals** (no numeric target, D-16) are scored on the tenant's overall grade scale as
      bands. Measured goals use actual vs target/min/max, with the KPI definition's tolerance
      (B2).
- [ ] L7 **Neighbours.**
      - Peers score goal rows only when `AllowPeerKpiEvaluation` is on (PES :321-331, :521, :545).
      - Interim reviews read the locked goal set only (`AppraisalReviewEventService.cs:197-307` scores
        every cycle goal into `OverallPeriodScore` today).
      - A period-scoped goal (Q1, H1) is not counted again at year end.

**Assertion** (`run-final-goalkpis.mjs`):
- Two staff on one template with different locked goals are scored on different items with
  different targets.
- Goal weights 60/40 → shares 0.6/0.4 of the section.
- Unlock after scoring → 422.
- The identical-value invariant holds with goal rows.
- A goal set **before** generation is scored.
- A calibration adjustment on a goal row changes that row, not the overall.
- An appeal on a goal row resolves.
- A remand with goal rows restores them.
- A weight-0 free-text item activates with no bands.

### Lane C — One appeal machine

- [ ] C1 Delete legacy `FileAppealAsync` (:311-353), `ResolveAppealAsync(ResolveAppraisalAppealDto)`
      (:453-500), interface members (`IAppraisalServices.cs:64-65`), controller actions
      (`PerformanceAppraisalsController.cs:334-404`), their DTOs and FE types. No FE caller.
- [ ] C2 `GetLatestAppealAsync` (newest by `SubmittedDate`) replaces the four unordered lookups;
      one open appeal per appraisal (enforced by C10's index, not only a check).
- [ ] C3 Remand **keeps `Status=Appealed`** (finding: Active made it a normal pipeline row), clears
      the manager's `SubmittedDate` and `CalibratedOverallScore`, sets the remand dates; the manager's
      remand re-evaluation settles (`publish:false`), stays Appealed with `CurrentAppealStatus=Remanded`,
      clears `AppealRemandDeadline`, notifies HR; `GetManagerEvaluationContextAsync` :2256
      `isEditable` gains the remand case; the manager's screen shows the remand deadline.
- [ ] C4 `ResolveAppealAsync`: `Rejected` with `CriteriaModifications` → 422; `Upheld` mods write
      `NumericScore` (D-22, typed like calibration, validated per A11), clear `CalibratedOverallScore`,
      settle; both end Completed + publish. Justification required server-side (FE :105 substitutes
      text today).
- [ ] C5 `FinalizePostRemandAppealAsync`: `Rejected` restores `NumericScore/ActualValue/Notes` from
      the remand snapshot (needs `AppraisalCriterionScoreSnapshot.ActualValue` — batch 1;
      `CreateManagerEvaluationSnapshotAsync` :2083-2096 writes it); `Upheld` keeps the re-evaluation;
      outcome text matches (:3837 is false today).
- [ ] C6 KPI items appealable again (the majority of the score): the appeal page lists KPI and goal
      rows, and a modification is the achievement-% override of D-22 — not an `ActualValue` change;
      remove the "deprecated" hard-empties (PAS :2836, :3398, :3673, :3877).
- [ ] C7 Appeal on a Governance appraisal refused; resolution never bypasses HR sign-off;
      `HRCanModifyScores` stays the guard.
- [ ] C8 **P-50:** submitted appeal items must belong to this appraisal's snapshot and be in the
      appealable list. Today `TemplateItemId` is copied as sent (PAS :2910-2921; the legacy writer
      :334 likewise).
- [ ] C9 **P-49 is worse than recorded.** HR's appeal review sets `Weight = 0` on every row
      (PAS :3319, since 2d8781248); it reads from the snapshot instead.
      `GetAppealReviewDataAsync` never loads `Items.TemplateItem` (PAS :3237-3239, :3305-3308), so
      the criterion names built at :3317 are probably blank. Probe first, then fix.
- [ ] C10 One open appeal per appraisal becomes a filtered unique index (batch 1).

**Assertion** (`run-final-appeals.mjs`):
- `POST /{id}/appeal` → 404.
- Submit with `EnableAppeals=false` → 422; after the window → 422.
- An item from another appraisal → 422.
- Remand → manager submit → 200; post-remand review → 200.
- Finalise `Rejected` → manager scores equal pre-remand and `adjustedScore` null.
- `Upheld` → `adjustedScore == overallScore != originalOverallScore`; both end Completed.
- HR's appeal review carries real weights and criterion names.

### Lane D — Peer nomination and evaluation integrity

- [ ] D1 `ToEntity` forces Pending + nominator from the token (PNS validates appraisee or manager);
      `UpdateEntity` copies only `DueDate`/`InstructionsToPeer`; update refused unless Pending;
      a posted non-Pending status → 422; `PeerEmployeeId` ≠ appraisee and ≠ manager, **on the batch
      route too** (`hr-portal/run-slice5.mjs:122` nominates the manager today — lane S8). Approval
      only through the batch endpoints (the only path that creates the peer `EvaluatorEvaluation`,
      PNS :530-541).
- [ ] D2 Counts exclude Rejected (PNS :211, :360-361, :398; PAS :1448) so a replacement can be nominated.
- [ ] D3 `send-invitation` stub (PNS :314-333, `// TODO: Send email`) removed — approval notifies;
      its client method (`appraisal-run.service.ts:403-405`, no screen caller) goes with it.
- [ ] D4 `PeerNominationMode.Manager`: manager nominates, nominator recorded as manager, no
      approval step; wording per mode (PNS :430 records the appraisee today); the appraisee is not
      admitted to batch nominate in Manager mode (PAC :1324-1338).
- [ ] D5 Peer submit checks appraisal + cycle status; peer `DueDate` (PNS :525) shown on the assignment
      instead of the cycle deadline (`PeerEvaluationService.cs:128`); dead decrement at PNS :299-305 removed.
- [ ] D6 The nominated peer cannot write the nomination (P14).

**Assertion** (`run-final-nominations.mjs`):
- A raw POST with `nominationStatus: Approved` → 422, and no peer evaluation exists.
- A PUT changing `appraisalId`/`peerEmployeeId` leaves them unchanged.
- After one nomination is rejected, the employee can nominate another without hitting
  `MaxPeerEvaluators`.
- The peer's own PUT → 403.

### Lane E — Lifecycle guards

- [ ] E1 **Appraisal:**
      - **One `AppraisalLifecycle` table**, taken from AWS :27-70; the PAS :533 copy is deleted.
      - **Delete** `POST /{id}/calculate-score` (:309-317). The evaluator/criterion CRUD block goes in
        P1.
      - **Narrow `PUT /{id}`** to the correction the HR review's Correct dates button sends
        (`hr-review/[id]/page.tsx:103, :262`; `appraisal-run.service.ts:293-296`): dates and peer
        count only, with A6's `OverallScore` line gone. It is **not** deleted — the review found its
        caller.
      - **Raw `POST /PerformanceAppraisals`** is deleted per D-20. It needs J's `Employee` target case
        and S8's fixtures in the same slice.
      - **`PATCH /{id}/status`** and `POST appraisal-workflow/{id}/transition` allow **Draft→Active
        and Completed→Closed only** (the latter needs a non-null score); anything else is a 422 that
        names the owning action.
      - **Finalise** requires Governance with HR unsigned. **Return-to-manager** requires the same,
        and resets `IsCalibrated/CalibrationSessionId/CalibratedOverallScore`. Reopen is D-17.
      - **Header update** is refused on Completed/Closed/Appealed/Withdrawn. **Delete** is allowed
        only for Draft, or Active with no submission.
      - **Self / manager / peer writes** check appraisal Active and a **live** cycle: Open, or
        InProgress until D-14 lands. The demo cycle is InProgress.
      - **`AcknowledgeAppraisalAsync`** reads HR sign-off from `AppraisalHRReview` (manual advance
        writes that, AWS :550-560) and accepts comments.
      - **Withdrawn** per D-10: an HR action with a reason; `SeparationService.CompleteSeparationAsync`
        (:1240) withdraws the leaver's open appraisal; a withdrawn appraisal leaves every score,
        denominator and close check.
- [ ] E2 **Cycle:**
      - Create ignores the body `Status` (MAP :942).
      - Update refuses a settings-profile swap once `OpenedDate` is set (:353), and a year/type
        change once appraisals exist.
      - `CloseCycleAsync` (:970-994) refuses while any appraisal is not Completed, Closed or
        Withdrawn, and closes the Completed ones through the lifecycle (**no force flag**).
      - Generation is refused on Draft cycles (:590 refuses only Closed; the FE offers it at
        `cycles/[id]/page.tsx:327`).
      - `AppraisalCycleStatus.InProgress` per D-14: removed, with its six readers, the seeder and a
        data migration.
      - The duplicate cycle-target CRUD (:1706-1796) is deleted in favour of
        `AppraisalCycleTargetService`, with cycle-status checks added. The demo pack's `060:38, :44`
        uses the duplicate (S3).
      - `CalculateExcludedEmployees` is implemented (it returns 0, :1117).
      - `GetEmployeesInScopeAsync` becomes the generation scope: drop the tenant-wide auto-discovery
        (:1514+) and the inactive targets (:1479).
- [ ] E3 **Settings profile:** `UpdateAsync` refused while any Open/InProgress cycle uses it
      ("clone the profile"); allowed when all Draft/Closed.
- [ ] E4 **Template:**
      - Structural edits reset `ApprovalStatus` to Draft. P-8 is fixed in L4 first, or re-approval
        of the demo template fails.
      - The lock covers any cycle with generated appraisals.
      - The section-weight snapshot moved to A0.
      - **P-7:** the editor's freeze mirrors the server rule instead of freezing on any assignment
        (`administration/hr/performance/templates/[id]/page.tsx:134-138`); the assignment DTO
        (DTO :3773-3789) carries the cycle status.
- [ ] E5 **Goals:**
      - `UnlockGoalAsync` (:512-521) restores `InProgress` when progress exists, else `Approved`.
        Today it leaves `Status=Locked`, so the goal can never take progress again.
      - `EmployeeGoalService.LockGoalAsync` (:499) delegates to the workflow command (two lock
        paths today).
      - `UpdateAsync` (:239-251) on an Approved or in-execution goal is refused, or returns the goal
        to PendingApproval when its target or weight changes.
      - The DTO no longer re-parents `EmployeeId` / `PerformanceAppraisalId` (MAP :1818-1820).
      - Delete the dead `SubmitForApproval/Approve/Reject/LockGoalAsync` (`IAppraisalServices.cs:743-755`).
      - Goal-set weight 100 is enforced at lock (L5).
      - **P-22:** the goal-library link can be re-pointed (`UpdateEmployeeGoalDto`, DTO :4258, has no
        `GoalLibraryId`).
      - `GoalRiskEvaluator` rule 2 is made reachable: the pre-filters at
        `AtRiskGoalsQueryService.cs:167` and `TeamGoalsQueryService.cs:237` admit all live goals.
- [ ] E6 **Calibration:**
      - Commit is idempotent and scoped to appraisals at the calibration step; Completed appraisals
        re-settle.
      - `DeleteRatingAdjustmentAsync` (:535) is guarded like update (:517).
      - `UpdateRatingAdjustmentDto` cannot re-target the appraisal (MAP :2587).
      - Deleting a session (:165-176) clears `CalibrationSessionId` on its appraisals.
      - `Cancelled` gets a cancel endpoint and UI (or is removed).
      - `StartSessionAsync` (:212) is redundant with Open and is folded in; the demo pack's
        `061:228` calls `/start` (S3).
      - **P-41:** the grid reads the appraisal's settled and calibrated overall, not the adjustment
        record (`GetCalibrationMatrixAsync`, CSS :875-876, :896).
- [ ] E7 **PIP:**
      - Goals, meetings and progress (:778-872) are refused on closed plans.
      - `UpdateAsync` (:483-516) on an Active plan is refused or re-approved; body re-parenting goes
        in P5.
      - Handler numbering is aligned to `PIP-yyyy-NNNN`.
      - Meetings get a stored status (P-57, batch 1 column; backfill rule in § 6).
      - `UpdatePipGoalProgress` honours status and percent at create (P-55: status is forced to
        NotStarted at :787, and the create DTO has no percent, DTO :5209-5228).
- [ ] E8 **Conversations:**
      - The write policy is manager, conductor or HR only: drop `Appraisal.EmployeeId == me` from
        the write branch of `CanAccessConversationAsync`.
      - The DTO cannot re-parent `AppraisalId` (MAP :2347) or change `Type`.
      - Deleting a completed gate conversation is refused.
      - The body's `ScheduledById`/`ConductedById` are ignored (controller :230-233). The demo
        pack's `061:145, :261` sends them (S3).
- [ ] E9 **Definition in-use guards:** grade definitions (`AppraisalGradeDefinitionService.cs:69`),
      KPIs (:121), competencies (`AppraisalCriteriaService.cs:120`), goal library (:168), company
      (:153) / unit (:286) goals, cycle targets (:210), template assignments (:171) refuse delete
      while referenced.
- [ ] E10 **Other:**
      - `CheckInService.AddGoalUpdateAsync` (:249-273) checks the goal belongs to the check-in's
        employee.
      - `DevelopmentPlansController PUT` cannot change `EmployeeId`/`PlanStatus` (MAP :2152-2161);
        status changes only through `UpdateStatusAsync`.
      - `AppraisalOutcomeService.CloseAsync` (:214) stamps the rejecter fields, not `ApprovedById`.
      - `SaveSelfEvaluationAsync`'s blanket catch (:1723) logs and rethrows non-business errors.
      - Journal `entryDate` is honoured (`PerformanceJournalService.cs:169`, P-28).
      - `EnablePrivateJournal` is checked in `SetPrivacyAsync` (:201).
      - `CheckInService.CompleteAsync` (:175) is not repeatable; the demo pack's `060:175` and runbook
        claim [172] follow (S3, S11).
      - `DevelopmentPlanService.DeleteAsync` (:179) allows only Draft.
      - `AppraisalReviewEvent` finalise no longer writes goal `ProgressPercent` (:263-285);
        `run-interim-reviews.mjs:260-263` asserts it today (S8).
      - `OverallPeriodScore` is shown as context on the year-end manager form, or dropped.
      - P-5's truncation exists only in the seeder (`PerformanceAppraisalDataSeeder.cs:133`), so it
        moves to S1.
- [ ] E11 Lane 11's "the HR reviewer must be at work" rule in `AssignHRReviewerAsync` (2c73a55a9) is
      built but has never been run: assert it (finish plan :1576-1577).
- [ ] E12 **Snapshot repair (P-13).**
      - Generation stops swallowing snapshot failures (`AppraisalCycleService.cs:1366-1377`): the
        appraisal is not created, and the failure is reported to the caller.
      - An HR action rebuilds a missing criterion snapshot for an appraisal no evaluation has scored.
        The five Rule 8 fixture appraisals are its first users (S5).

**Assertion** (`run-final-lifecycle.mjs`): each guard above is probed once (422) and its happy path
once (200). Specifically:
- Create a cycle with `status: Open` → stored Draft.
- Close with an Active appraisal → 422; withdraw it → close succeeds.
- A settings update while an open cycle uses it → 422.
- Unlock a locked goal → `InProgress`, and a progress entry is accepted.
- Deleting a calibration session clears the appraisal's session id.
- Correct dates still works, and `overallScore` in its body is ignored.
- Snapshot repair gives a Rule 8 appraisal its snapshot.

### Lane F — Recommendations, proposals, probation, PIP chain, segregation of duties

- [ ] F1 Extend-Probation handler passes `CurrentUserProvider.EmployeeId` (refuse with a message
      when unlinked); Confirm-Probation goes through the probation workflow submit / fallback
      authority instead of `ConfirmAsync`. **Waits for D-18.**
- [ ] F2 Manager `Recommend*` ticks (incl. `RecommendAward`, never written today, PAS :2477) create
      Proposed `AppraisalOutcomeRecommendation` rows on manager submit (idempotent per
      appraisal+type — batch 2 filtered unique index); dashboards
      (`HRCycleDashboardQueryService.cs:596-601, 793-802`) count rows, not booleans. The demo pack's
      `061:300-306` stops creating its own (S3).
- [ ] F3 **Segregation of duties, rewritten by the review.** The premise "the engine path already has
      `preventInitiatorApproval`" is false:
      - no seeded HR definition sets it — `DatabaseSeedingService.cs:596` explains why; only the
        vendor payment at :1717 does;
      - `WorkflowDTOs.cs:220` defaults it to false;
      - the harness's own E2E definitions are the exception.

      The rule is therefore:
      - **In each service, on both paths** — engine and fallback — comparing Employee ids.
      - **The comparison needs a submitter.** `SalaryReviewProposal` and `EmploymentActionProposal`
        have no proposer, submitter or decider field, and the PIP has only supervisor and HR owner
        (entities :1287-1345). Batch 2 adds `SubmittedById` (Employee FK) and `SubmittedDate` to both
        proposals and an author to the PIP. `CreatedById` is often null (cross-module #6).
      - **Approver rules per D-12**, enforced in the services before `CanUserApproveAsync`:
        - salary and employment proposals → MD or TenantAdmin only;
        - a PIP a line manager wrote → HR; a PIP HR wrote → the employee's line manager or
          TenantAdmin;
        - a template → another HR officer, or TenantAdmin.

        The two proposal specs in `EnsureHrWorkflowsSeededAsync` (`DatabaseSeedingService.cs:505-554`)
        move from `executive` to the existing `mdOnly` role set. `executive` stays, because it also
        routes `HR_EMPLOYEE_SALARY_CHANGE_REQUEST`. The seeder only adds, so existing tenants'
        published definitions keep HR on the stage; there the service rule is the control.
      - **The fallback path's self-approval is closed:** `SalaryReviewProposalService.cs:159-179`,
        `PerformanceImprovementPlanService.cs:599-646` and `HrWorkflowFallbackAuthority.cs:67-70`
        (whose comment says callers enforce it).
      - **Recommendations:** `ApproveAsync` (:122) refuses the recommender; `ProposeAsync` (:87)
        requires the appraisal Completed and refuses duplicates.
      - **The MD role gains a proposals-read policy** (`HrPermissions.cs:800-803` holds only
        `ViewSeparation` today).
      - **Conditional routing still does not route** (cross-module #3), so narrowing to "this
        employee's line" stays in the services.
- [ ] F4 PIP `CompletePipAsync` (:239-245) with Termination/Demotion/Transferred raises an
      `EmploymentActionProposal` (Proposed) linked by `SourcePipId` (batch 2); `PipOutcome.Transferred` handled.
- [ ] F5 **Salary proposal "mark applied", rewritten by the review.** `EmployeeSalaryChangeRequest` is
      HR's own entity (5491f0676, "a change of pay is a request, approved on the engine and applied to
      HR and payroll"), not payroll's, and `SourceProposalId` already exists
      (`EmployeeSalaryChangeRequest.cs:75`; `EmployeeSalaryChangeRequestService.cs:170`).
      - `MarkAppliedAsync` (:224-240) requires an approved request with
        `SourceProposalId == proposal.Id`.
      - Approving that request flips the proposal to Applied, inside HR's code.
      - **No payroll ask and no cross-module entry.** `HR-PAYROLL-BOUNDARY.md:13-17` would put such
        an ask in a handoff file anyway.
      - Employment `MarkActionedAsync` (:231-246) stamps `ActionedById/ActionedDate` (batch 2, both
        entities).
      - Notes append rather than overwrite the rejection history.
      - The effective date follows D-19.
- [ ] F6 Handler idempotency ignores Rejected/Cancelled records (`SalaryReviewHandlers.cs:74-81`,
      `EmploymentActionHandlers.cs:76-83`, `PipRecommendationHandler.cs:78-84`); PIP handler applies
      the one-live-PIP rule (`PerformanceImprovementPlanService.cs:284-291`); succession pool owner
      fallback never the nominee (`SuccessionNominationHandler.cs:83`); PIP supervisor fallback never
      the HR approver (`PipRecommendationHandler.cs:88-90`).
- [ ] F7 **Stranded engine approvals, rewritten by the review.** Do **not** edit
      `WorkflowController.TryApplyPostApprovalIntegrationAsync` (:2655): it is another developer's code
      (git log: Michael Marmah), and today it handles only procedure cases and `SERVICE_REQUEST`.
      - Cross-module #15 stays the platform's fix. It has **three** generic paths:
        `approvals/{id}/process`, `steps/{id}/process` and the mobile actions.
      - HR's side: the nightly sweep (H2) finds the four performance entity types whose workflow
        instance is Completed or Cancelled while the entity is still PendingApproval, applies the
        status adapter and raises the PipOpened / decided notice.
      - The HR screens keep deep-linking to the module's own approve.
      - Add a note to #15 saying so.
- [ ] F8 Approval display names withhold the employee's name for PIP and employment action
      (`WorkflowEntityDisplayService.cs:436-457`; mirror discipline :403-409).
- [ ] F9 Recall without a published definition checks the initiator (salary :212 area, employment);
      stale "inoperable until a definition is published" comments removed; salary `Reject` binds the
      right DTO.

**Assertion** (`run-final-chain.mjs`):
- An Extend-Probation recommendation approves and **actions**: a `ProbationExtension` row exists
  with an employee actor. Confirm-Probation completes.
- Manager ticks → Proposed rows.
- A proposer approving their own proposal → 403/422 on **both** the engine and fallback paths.
- D-12's matrix: HR approving a salary proposal → refused; a manager approving a PIP a manager
  wrote → refused; the employee's line manager approving a PIP HR wrote → 200; HR approving its own
  template → refused.
- MD can read a salary proposal.
- A PIP closed with Termination → a Proposed employment action exists.
- Mark-applied without an approved salary change → 422.
- A stranded engine approval is reconciled by the sweep.

### Lane G — Notifications on the platform topics

Pattern: `src/ErpSystem.Core/Services/HR/LeaveReminderService.cs` — `EnsureTopicsAsync` (:474),
`PublishAsync` (:330, `IAppEventBus` + `EntityActivityEvent`), recipient rules
`UserFromEmployeeIdData` / `UsersFromData` / `Role HR` fallback with `{{Why}}` (:384-439), dispatch
log. Build `PerformanceNotificationPublisher` (Core) with one topic per event × audience.

**Store rule (D-21, rewritten by the review).** The portal's My Notifications
(`EmployeePortalController.cs:724-790`) already merges the platform store with the appraisal store,
and the unread count sums both. So:
- A recipient **with a login** gets only the platform topic: an in-app `Notification` row
  (`NotificationTopicPublisher.cs:513-530`) plus email. No parallel `AppraisalNotification` row, or
  every notice shows twice and the badge doubles.
- A recipient **without a login** (691 of 2,377 on UAT) gets the employee-keyed `AppraisalNotification`
  row, and HR is told with the reason.
- **Digests.** Notices raised by the sweep are one digest per person per run and one HR summary per
  run (round 4 lane K-a decision 1; round 5 rule 6). Each item is still claimed and logged on its
  own. Notices raised by a person's action stay one per event.
- **Templates.** They go in the catalogue, and background senders use `SendForTenantAsync`. Every
  new template gets a `HR-CONFIGURATION-REGISTER.md` § 2.7 row, or `hr-templates/run-lane-n.mjs`
  [A6–A8] goes red. Raw-HTML tokens are declared `IsHtml`.
- **Deep links per audience:** the employee goes to `/me/performance/…`, the manager to the team
  pages, HR to the desk. A notice with no page to open is half a feature.

Events → audiences:

- [ ] appraisal generated (employee, manager) — today NO CALL
- [ ] self-eval open/due (employee) — `SelfEvalWindowOpen` never raised
- [ ] self-eval submitted (manager; peers in AfterSelfEval) — exists, moves
- [ ] manager-eval due (manager) — manual only today
- [ ] manager submitted (employee; HR reviewer when HR review on) — exists; remove the false
      "you will be notified" promise (:2643) when HR review is off by delivering the notice
- [ ] peer nomination pending (manager, Employee mode) / approved (peer) / rejected (employee)
- [ ] peer submitted, all peers in (manager); `PeerEvaluationReminder` via the sweep
- [ ] calibration session scheduled / participant added (the participant) — today only "panel
      complete" is sent (CSS :264-289)
- [ ] calibration committed (managers of calibrated appraisals) — NO CALL today
- [ ] HR review ready (assigned reviewer, fallback Role HR) — failure swallowed at :2606 today
- [ ] finalised / acknowledgment requested (employee) **on every completion path**
- [ ] acknowledged (manager, HR)
- [ ] appeal filed (Role HR + manager), resolved/remanded (employee, manager)
- [ ] goal submitted (manager) / approved / rejected / locked (employee) — NO CALL today
      (`GoalWorkflowCommandService.cs:124/204/289/375`); goal set locked (employee)
- [ ] goal submit refused because the employee has no line manager → HR data-fix notice (D-28)
- [ ] goal at risk (employee + manager, weekly, from the sweep)
- [ ] interim review opened / due / self-submitted / completed (employee, manager) —
      `AppraisalReviewEventService` has no notification call at all
- [ ] check-in scheduled/due (both), conversation scheduled (the other party) / due
- [ ] PIP in force (employee, supervisor, HR owner — also after F7), meeting scheduled/due (all
      three), closed
- [ ] development plan created-Active / feedback (employee)
- [ ] recommendation proposed (Role HR) / dispatch failed (Role HR, from the sweep's report)
- [ ] proposal decided (initiator + employee where appropriate)
- [ ] deadline approaching/passed (**the step owner only** — fixes `AppraisalCycleService.cs:505-574`
      sending every phase to everyone)
- [ ] auto-advanced (employee + manager + HR) — `AutoLocked` never raised today
- [ ] appraisal withdrawn (employee, manager — D-10); evaluator reassigned (old + new manager — M2);
      reopened (employee, manager — D-17)
- [ ] desk bell shows performance rows; portal inbox shows each **once**; unresolvable recipient →
      HR summary with the reason; the three dead `AppraisalNotificationType` members raised or deleted.

**Assertion** (`run-final-notify.mjs`, SMTP sink pattern from `hr-orientation/run-round4-k.mjs`):
- One email per audience type is captured and decoded, with an in-app row for the same event.
- A person with a login sees each notice **once** in the portal feed, and the unread count moves by
  exactly one.
- An appraisee with no login appears once in the HR summary, with the reason.
- An event with no manager lands on HR with the reason.

### Lane H — Nightly sweep and the advance path

- [ ] H1 `PerformanceSweepService.RunForTenantAsync(tenantId, trigger, triggeredByUserId)` +
      `PerformanceSweepBackgroundService` (24 h, lease `bg:appraisal-deadlines`, per-tenant loop,
      one tenant's failure does not starve the rest — copy
      `src/ErpSystem.Api/Services/HR/LeaveReminderBackgroundService.cs:70-117`). Every service the
      sweep calls gets a tenant-parameterised core. The review lengthened the list:
      - `AppraisalNotificationService`, `AppraisalCycleService`, AWS;
      - the settle path (`SettleScoreAsync`/`SyncLifecycleAsync`) and `TalentRatingSyncService`;
      - `AtRiskGoalsQueryService`/`GoalRiskEvaluator`;
      - the PIP, check-in and conversation services used for due notices;
      - the goal-set lock snapshot (EAC);
      - the stranded-workflow reconciliation (F7).

      Today these read `ICurrentUserProvider.TenantId` and throw with no signed-in user. `TenantId`
      is stamped on every write.
- [ ] H2 What it does:
      - Deadline reminders to the step owner (dedupe key `kind:appraisalId:step:deadline:tier`),
        delivered as digests (G).
      - `AdvanceOverdueAppraisalsAsync` only when `AutoLockOnDeadline`, **submission steps only**
        (never calibration, HR, conversation or acknowledgment), then notifies.
      - Goal-set lock at the goal-setting deadline per D-16.
      - At-risk goal notices; PIP meeting, check-in and conversation due notices.
      - **Failed recommendation dispatches are reported, not retried.** All six handlers read the
        tenant from the signed-in user (`Handlers/*.cs` `GetTenantId`), and the probation ones the
        actor too (`ProbationHandlers.cs:118, :173`). Retry stays a person's action on the worklist.
      - Stranded-workflow reconciliation (F7).
      - A run header and dispatch rows (`PerformanceSweepRun`, `PerformanceSweepDispatch` — batch 2)
        with `runs`/`log` endpoints, and a screen at `/hr/performance/deadline-enforcement`, renamed
        "Deadlines & sweep" with run-now kept.
      - `AppraisalManualAdvanceLog.AdvancedByEmployeeId` becomes nullable (null = the sweep).
- [ ] H3 Advance path (AWS :377-747):
      - The target must equal `Resolve()`; Draft, Appealed and Withdrawn appraisals are skipped.
      - The manager placeholder gets `EvaluatorId = Employee.ManagerId` (refused when null) and the
        profile weight. Today it gets the HR actor and weight 1 (:503-517).
      - Peer arms approve through `IPeerNominationService.ApproveNominationsAsync`, which creates the
        evaluations, and auto-submit drafts with scores.
      - A gate HR cannot satisfy is a **waiver**, recorded as the `AppraisalManualAdvanceLog` row
        (`AppraisalGates.IsWaived`).
      - The calibration arm runs only from the manual endpoint, stamps `PreCalibrationScore`, and
        leaves `CalibrationSessionId` null. Today it sets `IsCalibrated` with no session (:523-530).
      - The GoalSetting arm **locks** the approved set (D-16), not only approves it (:404-421).
      - Governance→Completed goes through `SyncLifecycleAsync`, so the score is settled.
      - `Success` only when the sub-status changed; `Advanced` counts real moves.
      - The `AutoLockOnDeadline` label and the settings audit say "nightly".
      - Decision 5 makes this form HR's main tool, so it gets a picker (P-60, lane I).

**Assertion** (`run-final-sweep.mjs`):
- Advance with a wrong target → 422.
- Advancing ManagerEvaluation creates an evaluation for the employee's manager at the profile weight.
- Run-now with only the calibration deadline passed → `advanced: 0`; with the self-evaluation
  deadline passed → `advanced: 1`.
- The last governance step → Completed with a non-null score.
- A stranded engine approval is reconciled.
- The run row has `CompletedAt`.

Then let the first **scheduled** run fire on UAT and read the log and the `Unrouted` count.
`AutoLockOnDeadline` is OFF on the demo profile, so advance must report 0. Measure the reach in SQL
first: APC2026's goal-setting and mid-year deadlines have passed.

### Lane I — Frontend wiring

Group 1 — actions that render and 403:
- [ ] HR desk goal Submit/Approve/Reject/Lock (`employee-goals/page.tsx:582-600`,
      `employee-goals/[id]/page.tsx:168-191`) shown to owner (Submit) / direct manager only.
- [ ] PIP "Record outcome" (`pip/[id]/page.tsx:298-308`, needs PerformanceWrite), "New plan"
      (`pip/page.tsx:107-112`), check-in "Record as held" for the subject
      (`me/performance/check-ins/[id]/page.tsx:215-221`), interim-review side buttons
      (`interim-reviews/[id]/page.tsx:344-350, 387-393`), goal-risk "Reset to defaults"
      (`goal-risk-settings/page.tsx:114-118`, Admin), every Delete for the HR role (P-4/9/12/17) —
      gated on the controller's policy with the sidebar's permission helper.
- [ ] Conversation Save/Mark held hidden from the appraisee (`conversations/[id]/page.tsx:171-181`,
      `ConversationsPanel.tsx:155-157`).
- [ ] `PerformanceAttachmentsPanel` `canDelete` follows `readOnly` and the uploader (P9).
- [ ] `/hr/performance/page.tsx` landing cards gated like the sidebar (`sidebar.tsx:1383-1411`).
- [ ] New check-in offered only for the caller's reports, or the caller themselves (P6).

Group 2 — screens that lie or can only be empty:
- [ ] HR-review competency Weight (PAS :4198 hardcodes 0) and KPI Target (:4207 never set) from
      the snapshot (`hr-review/[id]/page.tsx:631, 670-672`).
- [ ] **P-43:** Finalise enabled from the server's gate — `canFinalise` + reason on the HR review DTO —
      not `self && manager && peers` (`hr-review/[id]/page.tsx:282-285`; PAS :4088).
- [ ] Analytics "Award" tile (`analytics/page.tsx:485`) reads recommendation rows (F2).
- [ ] Check-in follow-up / employee comments (`me/performance/check-ins/[id]/page.tsx:231-266`):
      complete form gains the fields; `checkInService.update` gets a caller.
- [ ] Portal list "Acknowledge appraisal" (`me/performance/appraisals/page.tsx:132-146`) only after
      HR sign-off; `overallScore` withheld by the server until finalised (P2); `hasAppeal` /
      `currentAppealStatus` read (`[id]/page.tsx:251-269`).
- [ ] Self-eval wording follows `showSelfScoreToManager` (`self-evaluation/page.tsx:284`); peer
      "attributed to you" wording follows `PeerReviewsAnonymous` (`peer-reviews/[id]/page.tsx:200`)
      and the appraisee gets a peer-feedback view when not anonymous (N1's threshold applies).
- [ ] Deadline-enforcement page: dead `success === false` branch (:85-99), help text (:244-247).
- [ ] Calibration page (`calibration/[id]/page.tsx:783`): inputs seeded from `adjustedScore`,
      cleared between rows; the overall dialog keys on `IsOverall`, not a null item (L1).
- [ ] Query keys: `CyclePhaseDatesDialog.tsx:93-96` (page uses `appraisal-cycle-progress` /
      `-calendar`), `me/performance/development-plans/[id]/page.tsx:117-120` (list key is
      `['me','development-plans',scope]`).
- [ ] `JournalEntryDialog.tsx:89-96` "Related goal" picker for own entries.
- [ ] `team-goals/page.tsx:292` → `employee-goals?employeeId=` honoured (`employee-goals/page.tsx:162`).
- [ ] `pip/new` "From appraisal" reachable (link from HR review passes `appraisalId`+`employeeId`,
      :32-36); PIP create sends `HROwnerId` (`PipViewModels.cs:24`).
- [ ] Error states distinguished from empty states: `me/performance/appraisals/[id]` (:47-51),
      journal (:55-75), `cycles/[id]` coverage (:211-215, 816-819), `GoalAssessmentPanel` (:64-68),
      `PeerNominationPanel` (:69), `OutcomeRecommendationsPanel` (:90), `PerformanceAttachmentsPanel` (:119).
- [ ] Dev-plan register paginates past 100 (`hr/performance/development-plans/page.tsx:58`);
      desk link stays in the desk shell (:176).
- [ ] "Open the appraisal" links from `pip/[id]/page.tsx:321-324` and
      `conversations/[id]/page.tsx:198-202` go to a route the viewer can open.
- [ ] `hr-review/[id]/page.tsx:297` phase rail gets `settings`; :328-331 "Ready to finalise" wording
      follows `requireEmployeeAcknowledgment`.
- [ ] Analytics "Send reminders" only on live cycles (`analytics/page.tsx:202-209`); check-ins
      default tab by role (`me/performance/check-ins/page.tsx:59,128`); dev-plan employee view hides
      the status picker/draft banner (`[id]/page.tsx:283-330, 460-463`); feedback author name from a
      DTO field, not `createdBy` (:483, :670).
- [ ] `OutcomeRecommendationsPanel.tsx:245-276` dismiss branch reachable; worklist Dismiss asks for
      a reason and gains Reject (`recommendations/page.tsx:243-272`).
- [ ] `DevelopmentSkillSuggestions.tsx:82-86` "Add as objective" gets its `onUse` from the plan page.
- [ ] **P-51:** the Result column links training requests, PIPs and probation periods too — the
      pages exist (`app/hr/training/requests/[id]`, `hr/performance/pip/[id]`, `hr/probation/[id]`);
      a talent-pool member links to the pool page (`types/hr/outcomes.ts:99-102`).
- [ ] **P-7:** the template editor's freeze mirrors the server (E4).
- [ ] Rating guidance (N4) and the "overridden by calibration/appeal" marker (A14) on every form.

Group 3 — screens for server-supported actions, and controls for this plan's new endpoints:
- [ ] **Interim review create/reschedule/delete** on the cycle's Interim reviews tab
      (`interimReviewService.create` :67 has no caller; Custom frequency is dead without it).
- [ ] Calibration attachments panel (`calibration.service.ts:182-213`); session edit/cancel (:55-60).
- [ ] PIP draft edit/delete (`pip.service.ts:81,86`); PIP recall wired to `pipService.recall` (:109).
- [ ] Development plan header edit/delete (`development.service.ts:67,71`); feedback withdraw (:134).
- [ ] Check-in edit/reschedule/delete (`appraisal-run.service.ts:459,463`); goal-update edit (:488).
- [ ] Conversation delete for HR (`conversations.service.ts:57`).
- [ ] Template section/item reorder using the reorder endpoints (`appraisal.service.ts:292,329`).
- [ ] Portal: draft-goal delete + progress history; `/me/performance/interim-reviews` on the portal
      nav (`portal-top-nav.tsx:90-99`); "My PIPs" and "My conversations" portal routes; My
      Development Plans tile on `/me` (`app/me/page.tsx:68-78`).
- [ ] Portal goal form sends `companyGoalId`, `unitGoalId`, `kpiDefinitionId`, `successCriteria`,
      `minValue`, `maxValue` (`me/performance/goals/page.tsx:140-153`).
- [ ] **New controls** (an endpoint without a control is not a feature):
      - "Lock goal set" on the manager's team goals (L2).
      - "Withdraw appraisal", with a reason, on the HR review and the cycle's appraisal list (D-10).
      - "Reassign manager" on the HR review (M2).
      - "Reopen to HR review" per D-17.
      - "Rebuild snapshot" (E12).
      - The advance/waiver form with an appraisal picker and a mandatory reason, replacing the GUID
        box (P-60; `deadline-enforcement/page.tsx:204-209`).
      - The rating-history panel (N2).
      - "Print record" and "Print goal plan" (M1, N6).
      - "Start next development plan" (N5).
- [ ] Delete the client methods that still have no caller after this lane (of the 91 listed by the
      audit) — re-run the two greps first.

Group 4 — types: `AppraisalCycleStatus` union aligned (`types/hr/goals.ts:103` has `'Archived'`,
`types/hr/appraisal.ts:29` does not; `InProgress` removed per D-14); C# enum members missing from TS
unions added (`Withdrawn`, section `Kind`).

**Verification:** scoped `tsconfig` type-check in two groups (full `tsc` crashes), eslint on touched
files, and the persona walk in § 7.

### Lane J — Dead code and contracts

- [ ] Dead interfaces with no implementation: `IEvaluatorEvaluationService`, `ICriterionScoreService`,
      `IAppraisalEmployeeResponseService`, `IAppraisalAttachmentService`, `IPipReviewMeetingService`
      (`IAppraisalServices.cs:207-267`).
- [ ] Dead members: the four `GetAttachmentAsync` (:97, :723, :793, :981);
      `IEffectiveAppraisalConfigurationService.ResolveForEmployeeAsync` (:1079);
      `ICalibrationSessionService.GetScopedAppraisalIdsAsync` (:968) made private; PAS
      `GetOwnedAppealAsync` (:135); `AppraisalCycleService.CreateAppraisalInstancesAsync` (finish plan
      9.26); `GoalRiskApplicationService` (`GoalRiskService`, never registered).
- [ ] **`ResolveEmployeesFromTargetsAsync` gains the `Employee` case** (finish plan 9.26 second half):
      `AppraisalTargetType.Employee = 4` exists (`HREnums.cs:1477`), but only Position,
      OrganizationUnit and OrganizationLevel resolve. D-20 and S8 need it, so harness fixtures can
      generate one appraisal.
- [ ] Unused injected fields: PAS `_kpiEvaluationSnapshotRepository` (so `KpiSnapshots` at :3574 is
      always empty — decide with C6), `PeerEvaluationService` `_gradeRepository` /
      `_appraisalCompetencyRepository`, `AppraisalCycleService` `_positionRepository`,
      `AppraisalTemplateService` `_competencyRepository`, `EffectiveAppraisalConfigurationService`
      `_appraisalRepository`, `GoalDetailQueryService` `_clock`, `PerformanceAnalyticsService` `_logger`.
- [ ] `UpdateStatusOnDraft` unused params (PAS :2018); `const bool peerEvaluationComplete = false`
      (:1233) computed; peer review DTO placeholders (:2688-2697); "placeholder" comments (:2847,
      :3319).
- [ ] Enum members: `AppraisalStatus.Open` (migrate rows, batch 1); `AppraisalReviewStatus.InProgress/
      Cancelled`; `AppraisalResponseStatus.Submitted/Recalled`; `GoalProgressStatus.Cancelled`
      (handle or drop; falls to `_ => goal.Status` at `EmployeeGoalService.cs:386`, `CheckInService.cs:242`);
      `ConversationType` unused members (expose or drop); `ReviewEventType` never generated
      (`AppraisalCycleService.cs:899-905`); `AppraisalType.*` has no branches (record as v2);
      `AppraisalAdvanceHelpers.cs:203` default arm returns `Appealed` contrary to its comment.
- [ ] Stale comments: AWS :768 `SetTenantId`, `GoalRiskSetting.cs:13` "NOT tenant-scoped",
      `SalaryReviewProposalService.cs:212` "returns to Draft"; `AppraisalCycleService` employee ids
      into `CreatedBy` (:705, :1352); HR-review list filter vs computed status (PAS :4257 vs :4312);
      three "goal readiness" definitions folded into `AppraisalGates`.
- [ ] Two appraisal-numbering schemes (PAS :563-569 count+1 vs `AppraisalCycleService.cs:1458`):
      reconcile with ledger D-28 ("confirmed harmless" because no unique index exists). Duplicate
      business numbers are still wrong, so use one generator with the highest-issued idiom; add the
      unique index only if the data is clean.
- [ ] `AppraisalCompetency` store residue (finish plan :1138-1139): `AppraisalCriteriaController` is
      `AppraisalCompetencyController`; one-library direction per D-25 (v2), recorded now.
- [ ] Definition re-seed also re-seeds topics (`DatabaseSeedingService.cs:499-503` vs :441-448).
- [ ] `PerformanceAppraisal.DevelopmentPlanId` is computed by N5; `RankInPosition`, `RankInUnit` and
      `NextAppraisalDate` are dropped or computed. The employees guide shows "Rank in unit" on the
      employee record (`HR-EMPLOYEES-SYSTEM-GUIDE.md:1214-1218`), so update that screen with the
      decision.
- [ ] D-11's columns and B3's `EvaluatorEvaluation.IsAuthoritative` leave the model with their
      DTO members and mapper lines.

### Lane M — Printable record and reassign evaluator

- [ ] M1 **`GET api/PerformanceAppraisals/{id}/record.pdf`.**
      - Who: HR read, or the appraisee or manager of the record.
      - When: only Completed/Closed, or Governance after HR sign-off.
      - Built on the existing HR document machinery (the renderer behind `HrLetterRequest` and the
        offer letter). The only HTML-capable PDF path is
        `IHtmlToPdfRenderer` → `HtmlToPdfRenderer` (Syncfusion DocIO). Its markup must be well-formed
        XHTML, and **a `rem`-styled table renders as an empty box**, so styles use px.
      - Content: header, cycle, employee, manager, per-section criterion scores by role (goal rows
        with their labels), overall + grade, calibration note, HR remarks, acknowledgment date +
        comment, appeal outcome, and the rating history (N2).
      - Buttons on the HR review page and the portal appraisal page.
      - The template is registered in the catalogue with its tokens declared, plus a § 2.7 row.
- [ ] M2 **`POST api/PerformanceAppraisals/{id}/reassign-manager`** (HR write).
      - Refused once the manager evaluation is submitted.
      - Moves the unsubmitted manager `EvaluatorEvaluation` (and its draft scores) to the new
        evaluator.
      - Gates, notices and `SaveManagerEvaluationAsync`'s authorization read the evaluator record,
        not `Employee.ManagerId` (PAS ~:2344 compares `appraisal.Employee.ManagerId` with
        `saveDto.ManagerId` today).
      - Writes an audit row with a reason.
      - Controls: a **"Reassign manager" button on the HR review page**, plus the offer on the
        staff-movement transfer path.

**Assertion** (`run-final-record.mjs`):
- The PDF returns 200 with the right content type only after sign-off, and 422 before.
- **The file is opened and its table cells carry the expected words**: the employee's name, a
  criterion label and the overall. A PDF that is a PDF is not a record that reads.
- After reassign, the new manager can open and submit, the old one cannot, and the notice goes to
  the new one.

### Lane N — Enterprise-practice additions *(new, severable, D-23/D-24)*

These are what an enterprise appraisal module is expected to do and this one does not. Each is
small; the lane can be cut without touching the others.

- [ ] N1 **Anonymity threshold.** With `PeerReviewsAnonymous` on, the appraisee sees peer feedback
      only as an aggregate of **at least three** submissions. Below that, peer scores still count, but
      the appraisee sees none, because two peers are guessable (the demo profile allows two).
- [ ] N2 **Rating history.** `AppraisalScoreChange` records appraisal, from, to, grade from/to,
      source (settle, calibration, appeal, advance, reopen), actor, reason and date (batch 1).
      `SettleScoreAsync` writes a row whenever the settled overall changes after the first settle.
      Shown on the HR review and printed (M1).
- [ ] N3 **Justification for extreme ratings.** A manager rating in an item's lowest or highest band
      requires a comment. It is a template-level switch, enforced server-side and tested both ways.
- [ ] N4 **Rating guidance on every form.** The band's description sits beside the input.
      `gradeDescription` already reaches `types/hr/appraisal-run.ts:241`, and nothing renders it.
- [ ] N5 **Start next development plan** from a completed appraisal, pre-filled from the items rated
      below expectations and the training recommendation. This computes the dead
      `PerformanceAppraisal.DevelopmentPlanId` (J).
- [ ] N6 **The agreed goal plan is printable** as a performance agreement: goals, weights, targets,
      the employee's submit date and the manager's lock date. Built with M1.
- [ ] N7 **D-23's scale items:**
      - the Appraisal Completion Register (by unit, status and overdue step, with export);
      - the Rating Distribution report (by unit and grade — shown, not enforced; decision 9 still
        defers enforcement);
      - bulk HR sign-off for appraisals at the HR step with no open flag, each still settled and
        logged individually.

**Version 2, recorded here and not built:**
- unfinished goals carried forward into the next cycle;
- a merit guideline (rating → default increase), pending TDC's policy;
- eligibility by hire date — exclusion rules are by level, unit, position or employee only
  (`AppraisalCycleTargetExclusion`);
- a second-level countersign step, if TDC's form has one (ask first; it is a pipeline step on
  `AppraisalGates`);
- rating-consistency flags for calibration;
- role-based technical competencies from job architecture (D-25);
- a cycle-level readiness gate (`HR-CROSS-MODULE-PATTERNS-SWEEP.md:143, :496-503`);
- the cycle year read from `fiscal-years/current` (company schedule C-42).

**Assertion** (`run-final-enterprise.mjs`):
- Two anonymous peers → no peer block in the appraisee's payload; three → an aggregate with no names.
- A calibration restatement writes a history row.
- An extreme rating without a comment → 422 with the switch on, and 200 with it off.
- A development plan created from an appraisal links back to it.

### Lane S — Seed, demo data and harness *(new)*

**Owns S-1…S-17 (all still NOT DONE at 2026-09-28), Rule 8's five appraisals, P-56's duplicate
meetings, the C# seeders, and every demo-pack or harness break that another lane's new rule causes.**
§ 7 item 6 requires the shape assertions green, which cannot happen without this lane.

- [ ] S1 **`PerformanceAppraisalDataSeeder`** (run by `HrDemoSeedOrchestrator.cs:297-305`,
      `seed-hr-demo`):
      - Drop `IsManagerAuthoritative` (:162) and `RequireDevelopmentPlanUpdate` (:189) — in the same
        slice as B3.
      - Set `IsDefault`; set the cycle status per D-14 (:314).
      - P-5: stop truncating codes (:133).
      - Fixture goals (:435-491): three per employee, weights totalling 100, locked through the
        unified path. Today they are `IsLocked=true` with `Status=Approved`, two each, with
        progress but no entries.
      - The five snapshot-less fixture appraisals (:417-431): rebuilt through E12, or removed.
      - Templates marked Approved without the workflow (:198-301): rebuilt through the service once
        D-15's template exists (S2).
      - `TdcDemoAppraisalCustomQuestionSeeder.cs:114-126` writes a free-text item into an Approved,
        in-use template. It relies on L4's no-bands rule, and adds the item before approval.
- [ ] S2 **The demo template** becomes D-15's model: a goals section plus core competencies (and a
      shared KPI section if TDC has one), and the demo goals are the goal plans agreed in `060`. The
      guide's worked example (Efua Seidu's 89.40) is re-derived.
- [ ] S3 **Demo-pack scenarios** (`dev-harness/hr-demo-smoke/scenarios`):
      - `060`: at least three goals per person, including TDC/00063 (`060:89-111`), locked as a set;
        `unitGoalId`/`companyGoalId`/`goalLibraryId` passed (S-1, S-16, `060:123`); targets through
        the kept target service (`060:38, :44`); the check-in completed once (E10; runbook claim
        [172]).
      - `061`:
        - a **mid-year conversation per track**, since the profile requires it;
        - drafts saved on Active appraisals only (today `UpdateStatusOnDraft`, PAS :2014-2021,
          activates on first save);
        - the Governance→Active transition fallback removed (`061:131`);
        - no create-and-self-approve of recommendations (`061:300-306` — F2 auto-creates them, F3
          refuses self-approval);
        - the conductor taken from the persona (`061:145, :261`);
        - `/start` folded into open (`061:228`);
        - the KPI adjustment and overall restatement behaviour updated (`061:232, :236`).
      - `070:333-334` and `100:176-177`: propose on **Completed** appraisals and approve as a second
        persona (`md.tdc`); otherwise EmploymentActionProposals and SalaryReviewProposals come out
        empty.
      - `062:68`: one Draft plan (S-14).
      - `130:474-479`'s PeerNomination POST is dormant — keep it that way.
- [ ] S4 **Shape gaps S-1…S-17**, built as ensure-steps driven by status (the scenario rule),
      in the seed-gap plan's wave order.
- [ ] S5 **UAT data repair:** P-56's duplicate meetings (8 → 2); Rule 8's five appraisals through E12.
- [ ] S6 **Manifest** (`demo-coverage-manifest.csv`):
      - add `GoalRiskSetting,performance,required,` — the table name is singular
        (`ApplicationDbContext.HR.cs:4190`);
      - add `PerformanceSweepRun`, `PerformanceSweepDispatch` and `AppraisalScoreChange` as excluded
        log tables under their exact names (a wrong name is only "absent" and does not fail);
      - keep `AppraisalNotifications`, since batch 3 is withdrawn.
- [ ] S7 **Shape assertions.** `verify-tables.mjs` gains shape assertions — today it only counts rows
      (:46-58). The first ten come from the seed-gap plan § 4.
- [ ] S8 **Harness** (`dev-harness/`):
      - **Fixtures:** `hr-performance/setup.mjs:178` builds appraisals through the raw create, so it
        moves to generation with an **`Employee` target on a minted employee** (J). `run-sliceE.mjs`
        (:121-125) generates for every employee on a real position (264 in one call); on UAT that
        is real staff, so it too moves to an `Employee` target. The README gains the UAT/JWT setup
        and the no-real-staff rule.
      - **Suites the new rules break:**
        - `hr-w3-permissions/run-slice10-performance.mjs:142, :224` (deleted routes);
        - `hr-performance/probe-lane3-appraisals.mjs:83-111` (the header PUT is narrowed);
        - `run-sliceC.mjs:24, :63, :119` (open the cycle; Draft→Active first);
        - `run-sliceE.mjs:41` (section kind), `:125`;
        - `run-interim-reviews.mjs:260-263` (inverted per E10);
        - `hr-portal/run-slice5.mjs:122-169` (nominate a colleague, not the manager; an Active
          appraisal);
        - `hr-succession/run-slice6.mjs:127-164` (a Completed appraisal, a second approver, and a
          rewritten idempotency check);
        - `probe-area5-join.mjs:51, :66`.
- [ ] S9 **`IsDefault` backfill.** Batch 1 picks the profile of the newest cycle that is not a
      fixture (on UAT, "Standard Annual Appraisal"), **never "the newest profile"**, which would crown
      a harness-minted one (`hr-performance/setup.mjs:137-155`, `hr-portal/run-slice5.mjs:42`).
      *Built in batch 1 as "the most appraisals, then the latest cycle year, then the oldest"*, because
      nothing defines a fixture (§ 6 State). S1 still sets `IsDefault` in the seeder: on a rebuild
      the backfill runs against empty tables.
- [ ] S10 **Two full rebuilds green** (`scripts/New-UatDatabase.ps1`, exit 0: SCENARIOS, REQUIRED,
      COUNTS, RUNBOOK): one after lane B (the gates) and one at the end.
- [ ] S11 **Runbook Book 3.**
      - Claims in `runbook-claims.json`: [167] manager authority, removed; [171] locking, now a
        goal-set lock; [172] Q3 one-to-one notes, completion not repeatable; [178] recommendations,
        now auto-created with self-approval refused; [179] the appeal window, now enforced; [183] a
        failed PIP now raises an employment action.
      - Lines in `book-3-development-and-governance.html`: :61, :70, :72, :84, :85, :88, including
        the say-line "every one of those stages has a deadline the system enforces".
      - The never-click lists (`cheat-sheet.html:103`, `book-0:207`) gain the performance run-now.
      - `runbook-claims.json` is regenerated from the books.

### Lane K — Docs, registers, harness, memory, finish plan

- [ ] **Performance guide.** A **re-verification pass against the rebuilt UAT**, not only a status
      column:
      - the nine rules (Rules 1, 2 and 9 are reversed by A, C and E6);
      - the numbers at :724-771 and the data card at :4659-4682;
      - the live writes at :4800-4818 (#9 now calibrates only the appraisals at the step; #12 needs a
        FinalReview conversation; #13 is refused seven days after the build);
      - § 1.6 rewritten;
      - every P-row carries its § 8 status;
      - a "who decides the KPIs" chapter (D-15, L0).
- [ ] **Settings audit** re-verdicted (target: 48 enforced, 0 ghost, 2 removed).
- [ ] **Seed-gap plan:** every S-row carries a status.
- [ ] **`docs/HR/programme/HR-CONFIGURATION-REGISTER.md`:**
      - a performance section folding in the settings audit wholesale (:12, :728, :732 say "not
        looked at");
      - :74's "14 of 50" evidence refreshed;
      - a § 2.7 row for every new template and document (G, M1, N6);
      - the "44 templates / eight catalogues" count at :303-304 updated.
- [ ] **`docs/HR/programme/HR-CLOSURE-LEDGER.md`:** § C's "PerformanceAppraisals — BUILD" rows (:494-498,
      `calculate-score`) and D-28 reconciled with E1/J; lane 5b's question (:945-953) answered by
      D-11.
- [ ] **`docs/HR/programme/HR-FINISH-PLAN.md`:**
      - the pointer says performance has **no** sweep today (lane 10's eleven do not include one);
      - lane 9 rows #9.3, #9.4 and #9.26 marked as owned here (E1/D-20, F1, J);
      - lane 11's reviewer rule verified (E11);
      - D-18 settled there.
- [ ] **`docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`:** #15 gains "HR reconciles on its side (F7); no
      edit to `WorkflowController`"; #3 gains "performance definitions route by role; narrowing
      stays in the services". **No payroll entry** (F5).
- [ ] **`docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md`:** D-24's questions and D-27.
- [ ] **Round 3 walks still owed:** the T3 "Appraisals & goals" tab, and proposal →
      `?tab=salary&fromProposal=` (`HR-DEMO-FEEDBACK-ROUND-3-PLAN.md:170, :242`).
- [ ] **Harness:** the suites listed per lane under `dev-harness/hr-performance/` are each green
      twice; the six existing suites and S8's list are re-run (stale assertions fixed, not
      deleted); the README table is updated.
- [ ] **Memory:** the position after each lane goes in `hr-performance-final-closure`.

---

## 5. Residual register (low severity, owned by a lane; nothing from the audits or the review is dropped)

| Finding | Where | Lane |
|---|---|---|
| Deadline-reminder dedupe defeated by countdown titles ("closes in 5 days") | `AppraisalCycleService.cs:549-562` | G |
| Calibration-completed notice tells participants to "commit", which only HR can do | CSS :264-295 | G |
| Conversation scheduled by the employee does not tell the manager; PIP meeting notice to employee only | `AppraisalConversationService.cs:185`; PIP :192-206 | G |
| Nothing told in Manager nomination mode | PNS :464-489 | D/G |
| PIP handler numbering `PIP-APR-…` vs `PIP-yyyy-NNNN` | `PipRecommendationHandler.cs:103` | E7 |
| `AppraisalCycleTarget.EstimatedEmployeeCount` client-supplied; `ActiveEmployeeCount` can go negative | MAP :1044; entity :647 | E2 |
| `PerformanceAppraisal.DevelopmentPlanId/RankInPosition/RankInUnit/NextAppraisalDate` never computed | entity | J (N5 computes `DevelopmentPlanId`) |
| `EmployeeAcknowledgmentComments` written only by manual advance | AWS :605 | E1 |
| `PeerNomination.DueDate` ignored | PNS :525 | D5 |
| Settings profile validation (Min≤Max, bands ordered) | `AppraisalSettingsService` | B6 |
| Calibration `FacilitatedById` from body; session `UpdateEntity` ignores Status | MAP :2481 | E6 |
| Pre-remand snapshot copies no `ActualValue`; KPI snapshots never written | PAS :2085-2093 | C5 |
| `AppraisalScoring.OverallScore` 0 vs null | `AppraisalScoring.cs:143` | A1 |
| Analytics includes pre-final scores | `PerformanceAnalyticsService.cs:61` | A9 |
| Talent sync no recency check | `TalentRatingSyncService.cs:51-82` | A8 |
| Manager resolution inlined ~57 times, snapshot vs live, no head-of-unit fallback (an employee with no manager cannot submit goals, `GoalWorkflowCommandService.cs:148`) | module-wide | M2 (evaluator record) + D-28 (no HR fallback — decision 6) |
| `GetEmployeesInScopeAsync` tenant-wide auto-discovery | `AppraisalCycleService.cs:1467-1514` | E2 |
| Interim finalise writes goal `ProgressPercent` | `AppraisalReviewEventService.cs:263-285` | E10 |
| `AppraisalCycleStatus.InProgress` never assigned; `IsCycleActive` checks only Open (PAS :4157) | 6 readers | D-14 / E2 |
| Salary figures readable by anyone with Performance Read | proposals controllers | F3 (policy) |
| Recall with no definition has no initiator check | salary/employment services | F9 |
| Frontend: `AppraisalCycleStatus` 'Archived' in one TS file only | `types/hr/goals.ts:103` | I |
| Frontend: `PerformanceAttachmentsPanel` "visible to anyone who can see this record" while the appraisee has no evidence tab | `:220` | I |
| Frontend: salary "Raise the salary change" and "Mark applied" independent | `proposals/salary-review/[id]/page.tsx:178-190` | F5 |
| *Added by the review:* | | |
| P-7 editor freezes on any assignment; the server refuses only Open/InProgress | `templates/[id]/page.tsx:134-138` | E4 + I |
| P-8 activation demands bands on a weight-0 free-text item; submit-for-approval too | `AppraisalTemplateService.cs:922-931, :323` | L4 |
| P-11 goal-risk thresholds tenant-wide | `GoalRiskSetting.cs:19-56` | v2 |
| P-13 generation repairs nothing; snapshot failures swallowed | `AppraisalCycleService.cs:623-631, :1366-1377` | E12 |
| P-22 goal-library link fixed at creation | DTO :4258 | E5 |
| P-24 at-risk filters by the employee's unit and level | `AtRiskGoalsQueryService.cs:155-159` | kept — disclosed on screen (:176) |
| P-25 no export on the at-risk list | `AtRiskGoalsController.cs:65` | D-23 (catalogue programme) |
| P-41 calibration grid reads the adjustment record | CSS :875-876, :896 | E6 |
| P-43 Finalise not disabled by the calibration gate | `hr-review/[id]/page.tsx:282-285` | I |
| P-44 no un-finalise; the API can reopen by accident today | PAS :4472-4497 | D-17 / E1 |
| P-49 HR's appeal review sets every weight to 0; criterion names probably blank | PAS :3319, :3237-3239 | C9 |
| P-50 appealed items unchecked, not even that they belong to the appraisal | PAS :2910-2921 | C8 |
| P-51 Result column links only salary/employment; the other pages now exist | `types/hr/outcomes.ts:99-102` | I |
| P-54 no effective date on proposals | entities :1287-1345 | D-19 / F5 |
| Finish plan 9.3 raw create takes no snapshot | PAS :269-297 | D-20 / E1 |
| Finish plan 9.26 `Employee` target case missing | `AppraisalCycleService` resolver | J |
| Manager and peer forms show the live target; frontend preview formula differs | PAS :4840, PES :584; `appraisal-run.ts:965-972` | A12 |
| Scores accepted above an item's top band; appeal `NewScore` unchecked | `AppraisalTemplateService.cs:797-817`; DTO :2956 | A11 |
| Goals set before generation invisible to the appraisal | EGS :221-229 | L2 |
| The only goal-weight guard is never called | EGS :278-279, :306-307, :643-659 | L5 |
| HR's advance approves goals but never locks them | AWS :404-421 | H3 / L2 |
| Assessments read with `ToDictionary`; no unique index | EGS :138 | L1 |
| `IsAuthoritative` column loses its writer with B3 | `AppraisalCycleService.cs:734`; PAS ~:2391 | B3 / J |
| APR numbering: ledger D-28 says harmless, J said collides | PAS :563-569 | J |
| Rank in unit shown on the employee record | `HR-EMPLOYEES-SYSTEM-GUIDE.md:1214-1218` | J |
| Seeded definitions route by role; conditional routing does not route | cross-module #3; `DatabaseSeedingService.cs:505-545` | F3 |
| Check-in ↔ company objective link unconfirmed | ledger :2178 | D-27 |
| Round 3 walks owed (T3 tab; proposal → salary tab) | round 3 plan :170, :242 | K |
| Harness fixtures and `run-sliceE` would resolve to real UAT staff | `setup.mjs:26-27, :178`; README :77-79 | S8 |
| The medium privacy items P7–P17 | lane P | P |

---

## 6. Migrations (user scaffolds; rewritten as guarded SQL; two batches)

**Rules that apply to both batches:**
- Declare **no** column type on a new decimal: `ApplicationDbContext.ConfigureDecimalPrecision`
  overrides it to `decimal(18,4)`, or `(18,2)` when the name contains Cost/Price/Amount/Total/Salary.
  The original `decimal(5,2)` / `(18,2)` specs were fiction.
- Any data operation is raw SQL.
- A new non-nullable column gets its real default in the `ADD`, never a follow-up `UPDATE`.
- Read every scaffolded `RenameColumn` as suspect.
- Validate each guard in isolation on a scratch database, then against UAT.

UAT is built by the migration chain from empty and then seeded, so a backfill runs against an empty
table there. Backfills are for databases that already hold data.

- **Batch 1 (before lane A):**
  - **Settings and appraisal:**
    - drop `AppraisalSettings.IsManagerAuthoritative`, `AppraisalSettings.RequireDevelopmentPlanUpdate`
      and `EvaluatorEvaluation.IsAuthoritative`;
    - add `AppraisalSettings.IsDefault` (bit, one per tenant; backfill per S9);
    - add `PerformanceAppraisal.CalibratedOverallScore` (null), backfilled from the newest overall
      `CalibrationRatingAdjustment` per calibrated appraisal. This sets the column only; the stored
      `OverallScore` changes when something re-settles (A15, D-13).
    - add `Withdrawn` handling (`AppraisalStatus` member + `WithdrawnReason`, `WithdrawnById` (Employee
      FK, null), `WithdrawnDate`) per D-10.
  - **Appeals:**
    - add `AppraisalAppeal.OriginalOverallScore` (null);
    - add a filtered unique index for one open appeal per appraisal (C10).
  - **Snapshots and scoring keys:**
    - add `AppraisalCriterionScoreSnapshot.ActualValue`, and make its `TemplateItemId` nullable with a
      new `CriterionConfigId`;
    - add `PerformanceAppraisalCriterionConfig.AppraisalTemplateSectionId` + `SectionWeightUsed`
      (A0; backfilled from the live template);
    - lane L: `AppraisalTemplateSection.Kind` (default Fixed), config `TemplateItemId` nullable +
      `EmployeeGoalId` + `ItemLabel` + the measurement fields;
    - `CriterionScore.TemplateItemId` nullable + `CriterionConfigId` (backfilled from appraisal +
      template item);
    - `CalibrationRatingAdjustment.CriterionConfigId` + `IsOverall` (backfilled
      `TemplateItemId IS NULL`);
    - `AppraisalAppealItem.CriterionConfigId`;
    - L1's four filtered unique indexes.
  - **Advance log and other additions:**
    - make `AppraisalManualAdvanceLog.AdvancedByEmployeeId` nullable;
    - add `PipReviewMeeting.Status`, backfilled from the date the screen infers it from today —
      past = held, future = scheduled (P-57);
    - add the `AppraisalScoreChange` table (N2, if lane N is kept);
    - drop `AppraisalHRReview.AdjustedOverallScore`/`AdjustmentReason` and
      `AppraisalAppealItem.RevisedScore` (D-11).
  - **Data fixes:**
    - `AppraisalStatus.Open` rows → Active;
    - cycle `InProgress` → Open (D-14);
    - remanded appraisals sitting at Active → Appealed (C3);
    - goals with `IsLocked=1` and `Status≠Locked` → Locked, and `Status=Locked` with `IsLocked=0`
      → InProgress when progress exists, else Approved (E5);
    - manual-advance `IsCalibrated=1` rows with no session → a waiver row in
      `AppraisalManualAdvanceLog` (H3).
  - **State (2026-09-29): DONE — scaffolded as `20260928231446_PerformanceClosureBatch1`, rewritten
    as guarded SQL (68 batches up, 67 down), built, and applied to UAT** (backed up first as
    `Backup\ErpSystemDB_UAT_before_batch1.bak`). On UAT: the history row is present, the 17 tables'
    columns, indexes, defaults and keys are identical to the tested post-Up schema (668 lines), and
    the backfill and repair counts match. Tested on
    a restored copy of UAT, green twice (18 checks): Up, Up again (no row or schema change), Down (the
    15 tables' columns, indexes, defaults and keys identical to UAT's), Up again (identical to the first
    Up); a duplicate open appeal stops Up with nothing half-done; Down refuses a goal row and a
    withdrawn appraisal, and removes only its own waivers. On UAT's data it sets 1 default profile, 1
    calibrated overall, 408 frozen section weights, 52 score config ids, 2 PIP meeting statuses, moves
    APC2026 to Open and 10 flagged goals to Locked. Where the SQL refines this list:
    - **S9 has no definition of "fixture"**, so the default is the profile the tenant has appraised
      the most people with, then the latest cycle year, then the oldest. A test run's profile carries
      a handful, and no harness naming lives in a production migration. HR moves the flag (B6).
    - **The goal repair follows the workflow's own lock**: a flagged goal becomes Locked only from
      Approved/InProgress/AtRisk/OnTrack/Completed (the lock's source statuses); a flag on a Draft,
      PendingApproval or Rejected goal is a lock that path refuses, so the flag is cleared instead.
    - **`IX_AppraisalSettings_TenantId` is dropped**: EF counts the default-profile index as covering
      the tenant key and removed the unfiltered one from the model.
    - **The waiver rows** read `PendingCalibration → CalibrationWaived`, no actor, marked
      `CreatedBy = migration:PerformanceClosureBatch1` (so Down can remove them). None on UAT.
    - The scaffold's rename (below) happened as predicted and is a drop and an add.

    Choices the model made, that later lanes build on:
    - **Every new column whose writer arrives in a later lane is nullable, and null means "derive
      as before"**: `SectionWeightUsed`/`AppraisalTemplateSectionId` (read the live section),
      `CriterionConfigId` on score, remand snapshot, adjustment and appeal item (key by template
      item), the goal-row fields `ScoringMethod` (enum `CriterionScoringMethod`, null = the item
      type decides), `DisplayOrder`, `ItemLabel`, `Unit`, `MeasurementType`. So rows written
      between batch 1 and their lane are never misread, and every backfill can be re-run
      (`WHERE … IS NULL`).
    - **One writer is wired now**, because a default would be a wrong answer: `IsOverall =
      TemplateItemId IS NULL` in the adjustment mapper, create and update — the backfill's rule,
      kept true until lane L gives goal rows their own meaning.
    - `PipReviewMeeting.Status` (`PipMeetingStatus` Scheduled/Held/Cancelled) and
      `AppraisalTemplateSection.Kind` (`AppraisalSectionKind`) carry model defaults (1) so the
      scaffold emits them. Meeting writers stay with E7: a meeting held before E7 lands stays
      Scheduled.
    - The code still keyed by template item reads the now-nullable id through
      `CriterionTemplateKey.TemplateKey()` (29 call sites in PAS, PES and CSS), which throws, naming
      the row, if a goal row ever reaches it. **Lane L3's worklist is `grep TemplateKey(`.**
    - `CriterionScore.NumericScore` stays `int` (A13's round-explicitly option).
    - D-14 stays split as § 1b assigns it: batch 1 migrates the data; E2 removes the enum member,
      its readers and the seeder's `InProgress`. `IsDefault` is not set by the seeder until S1.
    - The rating history's table is **`AppraisalScoreChanges`** (plural): the name S6 lists.
    - ⚠ **The scaffold emitted `RenameColumn(RequireDevelopmentPlanUpdate → IsDefault)`**: two `bit`
      columns left and one arrived. Applied, it would have crowned every profile the default. The
      SQL drops the old column and adds the new one.
- **Batch 2 (before lane F):**
  - `SalaryReviewProposal` / `EmploymentActionProposal`: `SubmittedById` + `SubmittedDate` (F3),
    `ActionedById` (Employee FK, null) + `ActionedDate` (F5);
  - an author column on `PerformanceImprovementPlan` (F3);
  - `EmploymentActionProposal.SourcePipId` (null FK) and `EffectiveDate` (D-19);
  - the `PerformanceSweepRun` and `PerformanceSweepDispatch` tables;
  - the `AppraisalOutcomeRecommendation` filtered unique index (appraisal, type) where not
    Cancelled/Rejected. **Existing duplicates are resolved first, in the same guarded script**, or
    the index creation fails.
- **Batch 3: withdrawn by the review** (D-21). `AppraisalNotification` stays: it is the record for
  people without a login.

The dev DB cannot migrate (use UAT for chain-built checks); UAT is the demo DB — no real-staff
fixtures; every suite mints its own.

---

## 7. Verification

1. API in Staging with the JWT key and the UAT connection string (harness README); stop
   `ErpSystem.Api` before the user builds; the user builds and reports.
2. Per lane: new suite green **twice**, previous suites re-run (read the failure list, not the
   count). The scoring lane must run the identical-value invariant with three peers, through both
   the manager path and the peer path.
3. **Frontend:** scoped `tsconfig` type-check in two groups; eslint on touched files. Four personas
   (staff, the `head.dev` manager, `hr.head`, admin) walk:
   - goal set + lock → self-eval → peer → manager → calibration (item + overall) → HR review →
     finalise → acknowledge → appeal → remand → re-finalise;
   - PIP → outcome → employment proposal → approve (a different user) → mark actioned;
   - an Extend-Probation recommendation approves and actions;
   - two staff on one template scored on different goals;
   - the PDF record and the goal plan print, **opened and read**;
   - a manager reassign moves the evaluation;
   - a leaver's appraisal is withdrawn and the cycle closes.
4. **Notifications:** the SMTP sink captures one email per audience type; the desk bell shows a
   performance row; the portal inbox shows it **once**; an unresolvable recipient lands in the HR
   summary with the reason.
5. **Sweep:** run-now on UAT after measuring reach in SQL; then the first **scheduled** run — read
   the run row's `CompletedAt` and `Unrouted`.
6. **Instruments:** the `scripts/hr-coverage` 01∩02 intersection is re-run (client methods with no
   caller drop by ~90); `verify-tables.mjs` passes with the new shape assertions (S7).
7. **Privacy:** `run-final-privacy.mjs` green, with the field-absence checks, not only status codes.
8. **Scale:** one run on a scratch copy of UAT with a cycle generated for the whole workforce (~2,400
   staff against the demo's 107 appraisals). Time generation, the HR dashboard, the gate evaluator
   over the pipeline list, a calibration commit over a directorate, and the nightly sweep. No SQL
   timeout; each screen answers within its budget.
9. **Demo:** two full UAT rebuilds green (S10), and Book 3's claims pass.
10. **Docs:** every P-/S- row carries a status; the settings audit is re-verdicted; the guide is
    re-verified; the registers, ledger, finish plan and memory are updated.

---

## 8. P-1…P-70 at HEAD 143ae2efc (the review's status pass, 2026-09-28)

Only one commit touched the performance services after the guide was written: 2c73a55a9 (an HR
officer on probation can be the HR reviewer). It changes none of these.

- **LIVE — owned by a lane (41):** P-2 B6 · P-4, P-9, P-12, P-17 I · P-6 A2 · P-7 E4 + I · P-8 L4 ·
  P-11 v2 · P-13 E12 · P-19 D-26 · P-20 L5 · P-22 E5 · P-24 kept (disclosed) · P-25 D-23 · P-28 E10 ·
  P-32 I · P-39, P-40 A (P-40 is wider than written: calibration item adjustments are inert for every
  item type) · P-41 E6 · P-43 I · P-44 D-17 · P-46, P-48 B2 · P-47 C6 · P-49 C9 (worse than written) ·
  P-50 C8 · P-51 I (its "screen not built" reason is out of date) · P-54 D-19 · P-55, P-57 E7 ·
  P-62…P-70 B · P-1 (the `admin.hr` gate — by design, recorded).
- **PARTIAL (3):** P-3 — `AppealWindowDays` unenforced (B2), `AppealReevaluationWindowDays`
  enforced (PAS :2361, :3454). P-5 — only the seeder truncates (S1). P-33 — the Active/Draft split
  is intended, and the screen has said so since 2afab21de; the guide's text is out of date.
- **FIXED (1):** P-27 (0ef42c223, before the guide), except `GET /CheckIns/paged` for
  TenantAdmin/Admin/"HR User" (P15).
- **BY-DESIGN (9):** P-15, P-16, P-21, P-23, P-30, P-34 (its server half — the manager's scores sent
  to the appraisee — is B2/P2), P-37, P-45, P-60 (lane I adds a picker anyway, because decision 5
  makes the form HR's main tool).
- **DATA → lane S (16):** P-10, P-14, P-18, P-26, P-29, P-31, P-35, P-36, P-38, P-42, P-52, P-53,
  P-56, P-58, P-59, P-61.

---

## 9. Review log (2026-09-28, the same day the plan was written)

The user asked whether the staged plan captured everything needed to close the module out. Five
read-only sweeps and a source re-check answered "not yet". This version folds all of it in. What
changed:

- **New lanes:**
  - **P**: the six High privacy exposures and twelve Medium items the audits missed.
  - **N**: enterprise-practice additions, severable.
  - **S**: seed, demo and harness — S-1…S-17 had no owner, and lane B would have turned the next UAT
    rebuild red.
- **Decisions:** § 1b's nineteen were raised by the review and settled with the user the same day,
  each on its recommendation. The user's template question is D-15. D-12 was refined into fixed
  approver rules for PIPs and templates. It is implemented through the existing `mdOnly` role set, so
  the salary-change request keeps its own routing.
- **Premises corrected in source:**
  - E1: the header PUT is kept, narrowed.
  - F3: no seeded definition bars the initiator, and the proposals have no submitter.
  - F5: the salary-change request is HR's own.
  - F7: another developer's controller is left alone; HR reconciles in the sweep.
  - G: one write per recipient, digests, and batch 3 withdrawn.
  - H: failed dispatches are reported, not retried.
  - The finish-plan pointer's "lane 10's performance sweep".
  - The claim that "every earlier defect is still live".
- **Scoring:** A0 (section-weight snapshot, moved from E4 and ahead of settle) and A10–A16.
- **Lane L:** a complete schema. The staged version had dropped the original's nullable
  `CriterionScore.TemplateItemId`; the review also found the calibration "null means overall" trap,
  the goal-linking defect and the missing set lock. Plus L0, L6 and L7.
- **Lifecycle:** withdrawal (D-10), snapshot repair (E12), and the `InProgress` decision ahead of the
  "cycle Open" guards.
- **Restored from the original plan:** the risks paragraph, and the fixture spec with the "lite"
  profile.
- **Migrations:** batch lists completed with the data fixes the first draft missed; decimal types
  corrected.
- **Verification:** privacy, scale and demo-rebuild gates added; the PDF assertion reads the file.
- **Size:** about ten weeks, up from six.
