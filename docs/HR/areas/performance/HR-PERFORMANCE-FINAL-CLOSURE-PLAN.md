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
   ~~Lane A~~ **Done 2026-09-29** (`run-final-scoring.mjs` 177/177 twice; § 4 lane A's State
   block). ~~Lane P~~ **Done 2026-09-29** (`run-final-privacy.mjs` 339/339 twice; § 4 lane P's
   State block — read its "refines or extends" list: it built a few rows beyond the plan's wording
   and brought two E10 bullets forward). ~~Lane B1~~ **Done 2026-09-29** (`run-final-gates.mjs`
   256/256 twice; B3–B5 went with it, and nine of the fourteen settings are now enforced; § 4 lane
   B's B1 State block — read its
   refinements and its *demo impact*: under strict gates 102 of APC2026's 107 appraisals sit at Goal
   Setting). Lane L is built in three slices (D-31): ~~L-a~~ **Done 2026-09-29**
   (`run-final-goalset.mjs` 157/157 twice; § 4 lane L's L-a State block — read § 1c first: a lock
   freezes what a goal is, not its year). ~~L-b~~ **Done 2026-09-29** (`run-final-goalkpis.mjs`
   244/244 twice; § 4 lane L's L-b State block — read its refinements: the period rule, what freezes
   a goals section, and P-71, the remand's dead end, which waits for lane C). ~~L-c~~ **Done
   2026-09-29** (261/261 twice; § 4 lane L's L-c State block — read its demo impact: *Lock set* now
   shows on five demo desks). **Lane L is complete; B2–B8 are next.** UAT was rebuilt on 2026-09-29
   (Efua 88.56, Cynthia 87.00 — lane A's settle path), then migrated in place to round 4's merge
   (104 history rows; `run-all` 1516/1525). **B2–B8 were source-checked the same night** — § 1d
   (D-32 tolerance to batch 2, D-33 two slices) and lane B's *source check* block. ~~Slice **B-v**
   (visibility)~~ **Done 2026-09-29** (`run-final-settings.mjs` 246/246 twice; lane B's B-v State
   block — one visibility rule, `AppraisalVisibility`, on every read of an evaluation). ~~Slice
   **B-w**~~ **Done 2026-09-29** (363/363 twice; regression 1877/1886; lane B's B-w State block —
   read its refinements and *B7 — where each flip lives*). **Lanes A, P, B and L are complete.**
   Lane C (one appeal machine) was source-checked on 2026-09-29 — § 1e (D-34 lapsed remands, D-35 the
   two-actor rule, D-36 two slices) and lane C's *source check* block. ~~Slice **C-a** (the
   machine)~~ **Done 2026-09-30** (`run-final-appeals.mjs` 209/209 twice; regression 2089/2098; lane
   C's C-a State block — P-71 fixed, and the `AppealReevaluationWindowDays` flip B7 left to lane C is
   in its d34). ~~Slice **C-b** (the reads)~~ **Done 2026-09-30** (source-checked first: § 1f D-37 a
   decided appeal opens, D-38 each item's score kept at the filing; `run-final-appeals.mjs` 397/397
   twice; regression 2309/2318; lane C's C-b State block — read its refinements: HR's changes go
   only on a contested row, and one score per row across the reads). **Lane C is complete.**
   ~~Lane **D**~~ **Done 2026-09-30** (source-checked first: § 1g D-39 HR's advance approves through
   the one path, D-40 counts only for the appraisee in Manager mode with anonymous reviews, D-41 a
   peer's reads list approved nominations only; `run-final-nominations.mjs` 186/186 twice;
   regression 2496/2505; lane D's State block — read its refinements and the harness changes
   it made in other suites). **Lane D is complete.** Lane E was source-checked the same day — § 1h
   (D-42 seven slices, D-43 an Open cycle, D-44 no Employee target, D-45 three columns to batch 2, D-46)
   and lane E's *source check* block. ~~Slice **E-a** (the appraisal routes)~~ **Done 2026-09-30**
   (`run-final-lifecycle.mjs` 130/130 twice; regression 2631/2640; lane E's E-a State block).
   ~~Slice **E-b** (calibration)~~ **Done 2026-09-30** (`run-final-lifecycle.mjs` 276/276 twice; regression
   2777/2786; lane E's E-b State block — read its refinements: a commit also skips what its manager
   submitted after the panel closed, and the grid says what a commit would take). ~~Slice **E-c** (cycle
   rules and D-14)~~ **Done 2026-09-30** (source-checked first: § 1i D-47–D-50; `run-final-lifecycle.mjs`
   391/391 twice; lane E's E-c State block — one scope rule, `AppraisalCycleScope`, and one door for
   targets; D-14's data migration applied to UAT, so APC2026 is Open). Slice E-d was source-checked on
   2026-09-30 and split in two — § 1j (D-51 two slices, D-52 withdraw only before final, D-53 no undo until
   lane N, D-54 both exits). ~~Slice **E-d1** (Withdrawn)~~ **Done 2026-09-30** (`run-final-lifecycle.mjs`
   542/542 twice; regression 3043/3052; lane E's E-d1 State block — HR's withdraw action and both exits, every read
   leaving a withdrawn appraisal out and every write refusing it; and the HR dashboard's load split, found by
   its first run). Slice E-d2 was source-checked the same day and split in two — § 1k (D-55 two slices, D-56 a
   close waits for finished work and lapsed appeal windows, D-57 any work blocks a delete, D-58 the dashboard's
   default cycle). ~~Slice **E-d2a** (the cycle's own rules)~~ **Done 2026-09-30** (`run-final-lifecycle.mjs`
   601/601 twice; regression 3104/3113; lane E's E-d2a State block — the close, the delete, reminders, the default
   cycle, 422s; and the harness's minute-long pauses traced to the API's rate limiter). ~~Slice **E-d2b** (the live
   cycle)~~ **Done 2026-09-30** (source-checked first: § 1l D-59–D-65; `run-final-lifecycle.mjs` 706/706 twice;
   regression 3226/3235; lane E's E-d2b State block — generation and an appraisal's work, goals and panels on an Open
   cycle only, the overlap at generation, the raw create gone; every suite now opens and tears down its cycles).
   ~~Slice **E-e** (templates and settings)~~ **Done 2026-10-01** (source-checked first: § 1m D-66–D-70;
   `run-final-lifecycle.mjs` 821/821 twice; regression 3352/3361; lane E's E-e State block — a template locked while
   anyone is scored on it or an open cycle has it, links pinned, a profile in use kept as it is and copied to change;
   every suite's teardown switches off the logins it minted, and the user cleaned UAT's notification backlog).
   ~~Slice **E-f** (goals, PIPs, conversations)~~ **Done 2026-10-01** (source-checked first: § 1n D-71–D-76;
   `run-final-lifecycle.mjs` 1016/1016 twice; regression 3549/3558; lane E's E-f State block — the at-risk lists watch
   every agreed goal, a goal goes before it is agreed and unlock restores its year, a PIP's meetings carry a stored status
   and a closed plan takes no writes, the appraisee reads their conversations and writes none). **Slice E-g (definitions
   and the rest) is next** — source-check it first, and read § 5's *Added by lane E-c* to *E-f* rows. **Before a long run,
   check free RAM and the stuck email queue** (§ 5, the second E-e row): either one starves SQL Server's query memory and
   reads time out at 30 s.
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
string, two HR users, clamd stub for uploads — see its README; start the API with its
`tools/start-api-uat.ps1`, which raises the API's rate limits for that process through the optional
`RateLimiting:*` settings, 2026-09-30 — the regression now takes about 5 minutes; it took 22, 17 of them queued at
the limiter); **UAT is the demo database, so no
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

### 1c. Decisions from lane L's source check — settled with the user, 2026-09-29

| # | Question | Decision (2026-09-29) | Affects |
|---|---|---|---|
| D-29 | **What "locked" means.** GWC's lock sets `Status = Locked`, and progress entries, check-in updates and interim reviews only move goals in a live execution status — so a lock ends the goal's year. D-16 locks the set at the goal-setting deadline, which would freeze every goal from February | **A lock freezes what the goal is, not its year.** Title, measure, target, weight and owner are fixed; progress and check-ins keep moving it. The lock is `IsLocked` + `LockedDate`; nothing sets `Status = Locked` any more, and a goal already in that status reads as approved and locked | L-a, L-b, B1's gate |
| D-30 | **Editing an approved goal.** The update copies owner, cycle and appraisal link from the payload — a UI edit clears the link, an edit can move a goal to another employee — and an approved goal's weight or target changes with no re-approval | **What an approved goal measures cannot be edited**; to change it, the manager rejects it back to the employee (the existing path). Owner, cycle and appraisal link never change through an edit | L-a |
| D-31 | **Lane L's size and order.** The check found the plan's five days to be seven or eight | **Three slices, each built, verified and staged on its own:** L-a governance (the lock, "lock goal set", governance at lock, edit hardening, the unlock fix, HR's advance, L0); L-b scoring (goal rows, the config-id keying, the forms, assessments, interim reviews); L-c screens (the section-kind picker, the frontend sites, snapshot labels, the manager's lock-set control, the lane suite) | Lane L |

### 1d. Decisions from lane B2–B8's source check — settled with the user, 2026-09-29

| # | Question | Decision (2026-09-29) | Affects |
|---|---|---|---|
| D-32 | **`KpiDefinition.TolerancePercent`.** At the seeded 5 %, four of the five demo tracks' scores move (Kwasi Danquah's sign-off in Book 3; Efua Seidu, Cynthia Sarpong and Kojo Fiadzo on the next rebuild — five submitted KPI rows sit 2–4 % under target). The criterion snapshot keeps a KPI's target, floor and ceiling (A12) but not its tolerance, so a live read would let a later edit of the definition restate scores | **Defer to batch 2** (lane F): a tolerance column on `PerformanceAppraisalCriterionConfigs`, captured at generation like the target, read by `KpiAchievementPercent`. The demo numbers are re-baselined once, then. B2–B8 ships with no migration | B2, F (batch 2), S |
| D-33 | **Lane B2–B8's size.** The source check widened the visibility rows (four more reads, two pre-sign-off leaks) and found `RequireEvidence` with no door and its links dropped — about three days | **Two slices, each built, verified and staged on its own.** **B-v** visibility: one rule for what a reader of an appraisal may see, applied on every read — the three flags and the two leaks. **B-w** write paths: goal approval, the mid-year's move, the conversation type, peer KPI scores, the soft-skill label, evidence, B6, B8. The new settings suite grows with each; B7's flips land across both | Lane B |

### 1e. Decisions from lane C's source check — settled with the user, 2026-09-29

| # | Question | Decision (2026-09-29) | Affects |
|---|---|---|---|
| D-34 | **A lapsed remand.** Once C3 reopens the manager's evaluation, a manager who misses the re-evaluation deadline is refused, and HR's final decision waits for a re-evaluation that cannot come | **HR may extend the deadline, and once it has passed may decide without the re-evaluation**: the pre-remand scores are restored (a manager's draft discarded) and HR records Upheld or Rejected | C3, C5 |
| D-35 | **Who may handle an appeal.** Nothing stops an HR officer picking up, deciding or finalising their own appeal, or one against an evaluation they wrote | **The two-actor rule, both ways**: refused on their own appraisal, and where they wrote the contested evaluation or are the appellant's line manager (who would re-evaluate). Another HR officer or an administrator handles it | C4, C7 |
| D-36 | **Lane C's size.** The check found seven defects beyond the rows — about 2.5–3 days | **Two slices, each built, verified and staged on its own.** **C-a** the machine: C1, C2, C3, C4, C5, C7, C8, D-34, D-35 and HR's decision page, which the new doors need. **C-b** the reads: C6 (KPI and goal rows appealable on the page), C9, the honest outcome, HR's review without a self draft, the guide's ch. 33. New suite `run-final-appeals.mjs`, grown by each | Lane C |

### 1f. Decisions from slice C-b's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-37 | **A decided appeal on HR's desk.** The queue's *Decided* tab links **View** to the appeal page, whose read refuses an Upheld or Rejected appeal — every decided appeal opens to an error, and the page's own *Decided* state is unreachable | **Open it read-only**: the review read answers for a decided appeal and carries the decision — who, when, the notes, the overall before → after; the page offers no action | C-b |
| D-38 | **What an appeal remembers of each item.** The status page's *Original score* is the manager's *current* score: after an upheld change it shows the new score, and during a remand the manager's re-scoring | **Record each appealed item's score at filing** on the appeal item (`AppraisalAppealItem.OriginalScore`, never written until now — no migration): a rated row's score on its scale, a measured row's achievement %. The status and outcome pages show *was → now* per item; appeals filed before it fall back to the remand snapshot, or "—" | C-b |

### 1g. Decisions from lane D's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-39 | **HR's advance past peer nomination.** It marks pending nominations Approved but creates no peer evaluation — "approved" peers with no form, told nothing | **Approve them properly, through the one approval path** the manager's approve uses: each peer's evaluation, the count, the notification. A peer who then does not submit is the next step's waiver | D1 |
| D-40 | **Anonymity in Manager mode.** The appraisee reads the peers the manager chose through the nomination reads (the summary, the list, a nomination by id); with one peer, the HR review's peer average is that person's score | **Counts only**: in Manager mode with anonymous reviews the appraisee's summary carries counts and statuses but no peer identity, and the list and by-id reads refuse the appraisee (403). Employee mode is unchanged — the appraisee chose the peers | D4, P |
| D-41 | **A peer's own reads** (`PeerNomination/peer/{id}`, `pending/{id}`) list nominations never approved — rejected ones too, with the reason written for the appraisee | **Approved only, no rejection reason**; the routes stay | D1, P |

### 1h. Decisions from lane E's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-42 | **Lane E's size.** The source check found some 12 days of code and harness rework, not 4 | **Seven slices, each built, verified and staged on its own, most severe first:** E-a the appraisal routes; E-b calibration; E-c cycle rules and D-14; E-d Withdrawn, the close, the live-cycle rule and the raw create's removal; E-e templates and settings; E-f goals, PIPs and conversations; E-g definitions, the rest of E10, E11 and E12. New suite `run-final-lifecycle.mjs`, grown by each | Lane E |
| D-43 | **A live cycle.** Generation refuses only a Closed cycle, and no evaluation write checks the cycle at all | **Generation and every evaluation write require an Open cycle** (the plan's rule). Every closure suite opens its cycles and, at the end, withdraws what is unfinished and closes them — a suite that dies mid-run leaves an Open test cycle, which the HR dashboard can pick as its default, so teardown runs in a `finally` | E-d, S8 |
| D-44 | **The raw create's replacement.** Deleting `POST /PerformanceAppraisals` (D-20) needs a way to generate one appraisal; an `Employee` target needs an `AppraisalCycleTarget.EmployeeId` column | **No migration**: the remaining fixtures (`buildFixture()`'s seven suites, `hr-portal` slice 5) take one position per case, as the closure fixtures do; the *Individual employee* target — offered by the form, resolved by nothing, used by no UAT row — goes, with its enum member (J's alternative) | E-c, E-d, J, S8 |
| D-45 | **Three E10 items need columns:** who rejected or dismissed a recommendation; a store for interim review scores apart from goal progress; `OverallPeriodScore` shown or dropped | **Migration batch 2 / lane F**, beside that batch's decider and author columns; lane E carries them | F, batch 2 |
| D-46 | The source check's other recommendations, taken as proposed | **D-17's reopen is built with lane N** (it writes the rating history N2 builds; lane E closes the accidental door). **An approved PIP's terms stay refused** — goal add and delete included — with no re-approval flow. **A settings profile is refused while any live appraisal uses it**, with a clone door. **Calibration gets a Cancel** (Pending or in progress), releasing its appraisals. **A Draft appraisal still takes its first save** (B1's refinement 9). **E11's negative cases run on a scratch database only**; **E12's repair runs on UAT's Rule 8 five only on the user's go** (S5) | E-a–E-g, N, S5 |

### 1i. Decisions from slice E-c's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-47 | **A target's "Resolves to".** The column read the typed estimate less exclusions it never loaded — both APC2026 targets showed 0 — while the guide (ch. 14, P-15), the TS type and the dialog all call it live (§ 5's E2 residual) | **Resolve it live in E-c**: the target's active staff whom the cycle appraises, after the exclusions; an inactive target reads 0. The estimate stays HR's planning figure | E-c, guide ch. 14 |
| D-48 | **How far the one scope rule goes.** Three copies: generation and the open's overlap check, the in-scope list (the open notice, the reminders, `GET {id}/employees`), and the coverage preview | **One resolver for all of them** (`AppraisalCycleScope`) — the excluded count and the live counts included — proven by APC2026's coverage preview matching field for field, and by new suite checks | E-c |
| D-49 | **A target's ids against its type.** The dialog sends the level a unit was picked under, so every unit target from the screen was refused ("exactly one id"); the server never matched the id to the type | **Keep the id the type names and clear the others** (the dialog's own hint); refuse a missing, unknown or other-tenant scope, or an unknown type | E-c |
| D-50 | **The pin on a Draft cycle that already has appraisals** (generation ran on Draft cycles until E-d; UAT holds hundreds of harness ones) | **Settings profile, year and type are fixed once the cycle is opened *or* has appraisals; the period once it has appraisals.** Name, code and phase deadlines stay editable | E-c |

### 1j. Decisions from slice E-d's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-51 | **E-d's size.** The source check found about 45 reads that would count a withdrawn appraisal, and all fifteen suites generating on Draft cycles by design (D-43's Open rule and the close's appeal windows rework every one) | **Two slices, each built, verified and staged on its own: E-d1 Withdrawn** — the action, the exit hooks, every reader; then **E-d2 the close and the live cycle** — the close, delete and generation rules, the Open rule for writes, the raw create's removal, every suite opening and tearing down its cycles. E-d2's own questions are asked at its source check | Lane E |
| D-52 | **Withdrawing a final appraisal.** A Governance appraisal HR has signed off is final, and its rating is already published to the talent pools | **Only before it is final**: from Draft, Active, or Governance before the sign-off. A leaver's signed-off appraisal stands — HR's audited advance takes it past the acknowledgment — and the exit hooks leave it, saying so | E-d1 |
| D-53 | **Undoing a withdrawal.** Generation skips anyone who already has an appraisal in the cycle, so a mistaken withdrawal cannot be regenerated | **Later: a reinstate joins D-17's audited reopen in lane N**, which builds the history both write to. E-d1 has no undo | E-d1, N |
| D-54 | **Which exits withdraw.** D-10 named the separation's completion; the direct termination route terminates anyone with no separation in flight | **Both exits** — each applies the exit in `EmployeeService`, so one hook covers both | E-d1 |

### 1k. Decisions from slice E-d2's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-55 | **E-d2's size.** The live-cycle rules (generation and every write need Open, the raw create removed) rework all fifteen suites; the cycle's own rules barely touch the harness | **Two slices: E-d2a the cycle's own rules** — the close, the delete, reminders, the dashboard's default cycle, 422s (the harness change: slices D and E withdraw, then close); then **E-d2b the live cycle** — generation and every write need Open, the overlap check at generation, the raw create removed, every suite opening and tearing down its cycles, with its own source check and questions | Lane E |
| D-56 | **When a cycle closes.** The close read no appraisal (26 of UAT's 34 Closed cycles hold unfinished ones) and cut open appeal windows | **Finished and windows lapsed**: refused while any appraisal is Draft, Active, Governance or Appealed, or any Completed one is still inside its appeal window (the refusal names the counts and the last window's end); then every Completed appraisal is closed with the cycle through the lifecycle — one with no score as it stands (none on UAT) | E-d2a |
| D-57 | **What stops a delete.** Only an opened cycle was refused, and the delete removed the cycle row alone | **Any work**: never once opened (as today), and refused while anything but its configuration points at it — appraisals (withdrawn included), goals, calibration sessions, check-ins, journal entries, review events, development plans — naming them; its targets, exclusions and template links are removed with it | E-d2a |
| D-58 | **The HR dashboard's default cycle** — the most recently updated Open cycle, so a harness or trial cycle left Open displaces the real one | **The Open cycle with the most appraisals in play**, then the most recently opened | E-d2a |

### 1l. Decisions from slice E-d2b's source check — settled with the user, 2026-09-30

| # | Question | Decision (2026-09-30) | Affects |
|---|---|---|---|
| D-59 | **Which writes need an Open cycle** (D-43 said "every evaluation write"; no write in the module read the cycle's status) | **The appraisal's work, the employee's goals and the calibration panels.** Every write on an appraisal or its children — the three forms (their goal assessments inside), nominations, HR's review and sign-off, the return, HR's advance and the deadline sweep, the acknowledgment, both response routes, appeals, conversations, review events, the date correction, the raw Draft → Active, the appraisal's attachments; the employee's goals (create, edit, delete, submit, approve, send back, lock, lock the set, unlock, progress, and a check-in's goal updates); calibration sessions (create, open, adjustments, commit). **Left as they are:** withdrawal and removal (HR can still clear an appraisal stranded on a cycle that is not open), closing a Completed appraisal, and the records that outlive or precede a running cycle — outcomes, PIPs, proposals, development plans, the journal, check-ins themselves, unit and company goals. The forms read not editable on a cycle that is not open | E-d2b |
| D-60 | **The overlap check at generation** — it ran only at the open, skipped when the targets reached nobody, and never again | **Refused, naming them**: generation refuses when anyone it would create is in the scope of another Open cycle of the same type and year, **or already holds an unwithdrawn appraisal in one**; the 422 names the cycle(s) and the people, and nothing is created — HR excludes them or closes the other cycle. The coverage preview's *safe to generate* says the same, and reads false while the cycle is not Open | E-d2b |
| D-61 | **The harness's teardown** — `withdrawAndClose` cannot finish a signed-off appraisal, one under appeal, or a Completed one inside its appeal window | **API first, SQL for the rest**: one `setup.mjs` helper every suite runs in a `finally` — withdraw what HR can through the API; then, by SQL and on the suite's own rows only, withdraw what the API will not and lapse the appeal windows; then close through the API and assert it (one check per suite). A never-opened scratch cycle is deleted. The close rule itself stays proven by the lifecycle suite's API-only E-d2a section | E-d2b, S8 |
| D-62 | **Two defects beyond the rows**: (a) the peer detail, draft and submit load any evaluation the caller is the evaluator of — the appraisee's self-evaluation or the manager's evaluation can be submitted through `api/PeerEvaluations/{id}/submit`, past their own gates, and where peers may not score KPIs the submit deletes that row's KPI scores; (b) HR's audited advance has no two-actor check | **Both in E-d2b**: the peer routes take peer evaluations only (any other row answers 404); HR's advance refuses its appraisee (403), and the deadline sweep skips and logs an HR officer's own | E-d2b |
| D-63 | **A0's proof** (`run-final-scoring`) — it re-weights template T2 through the API after generation, which the template lock refuses once T2's cycle is open | **Proved by SQL**: the API's refusal to re-weight T2 while its cycle is open is asserted, then T2's section weights are changed by SQL, the three checks run as before, and a `finally` restores them by SQL — which survives E-e's wider lock | E-d2b |
| D-64 | **`buildFixture()`'s light and full cycles** — both Annual in one year, both appraising the same person: the overlap rule refuses the second | **The full cycle becomes a MidYear cycle** (the type drives nothing but the by-type list, the edit pin and the overlap rule); each cycle generates over a post only its appraisee holds (D-44) | E-d2b, S8 |
| D-65 | **The raw create's probes** — W3 slice 10 asserts 403 for it (the deleted route answers 405), and hr-portal slice 5 hangs its fixtures off the tenant's first real post (so it has never run on UAT) | **W3's row asserts the route is gone** (404 or 405, as its removed-route row does; W3 still does not run on UAT — real staff). **hr-portal slice 5 mints its own post, generates over it, and runs once on UAT**; its probe drops the raw create | E-d2b, S8 |

### 1m. Decisions from slice E-e's source check — settled with the user, 2026-10-01

| # | Question | Decision (2026-10-01) | Affects |
|---|---|---|---|
| D-66 | **What the template lock covers** (it refused only while an open cycle had the template; the forms read the live template — sections, rows, questions, rated or measured — and the snapshot froze only weights, KPI targets and bands) | **Structure locked.** Locked while any appraisal is scored on the template or an open cycle has it: no section, item or band change, no reorder, no scope change, no delete — a change is made on a copy. Its name and description stay editable. An approved template whose structure changes (while unlocked) goes back to Draft; nothing changes while it awaits approval. Approve and reject act on a template awaiting approval only | E-e |
| D-67 | **When a settings profile is frozen** (E3 said "while an Open cycle uses it"; Draft cycles hold appraisals, and finished appraisals read visibility, anonymity, the appeal window and the outcome settings live) | **Once any appraisal uses it** — on any cycle running under it, finished and withdrawn ones included — its rules are frozen, compared field by field as the appraisals read them. Its name, deadline-risk bands, workload threshold and default HR reviewer stay editable (they change nothing an appraisal holds). Rules change on a **clone** (new `POST …/{id}/clone`), made the default or chosen by the next cycle; make-default is not guarded | E-e |
| D-68 | **The cycle↔template links** (the PUT re-pointed cycle and template unchecked; removing an open cycle's link released the lock; a closed cycle's links stayed writable; a template on a Draft cycle could be deleted) | **Links pinned.** A template can be **added** to an open cycle (for people not yet generated); a link is removed, switched off or re-prioritised only while no appraisal in its cycle is scored on its template; the PUT never re-points (a different template is a new link); a closed cycle's links are frozen; a cycle takes each template once. A template on any cycle is not deleted (it comes off first), and its sections, items and bands go with a delete. Generation and the coverage preview refuse a template that is not approved | E-e |
| D-69 | **The harness's logins** (every suite mints logins and leaves them active; a role-routed workflow step notifies every holder — 907 per template submission at E-d2b, eight demo personas among them) | **Switched off by SQL, and a one-off.** Each suite's teardown sets `IsActive = 0` on the logins that run minted, by id (hr-portal slice 5 likewise); a one-off switches off every existing harness login on UAT (`@e2e.local` and hr-portal's `a25v_`) — the user runs it or allows it. An inactive login gets no role notification; the reconciliation sweep leaves an admin's deactivation alone | E-e, S8 |
| D-70 | **Beyond the rows (E-e)** (each verified in source) | **Folded in:** the section and item PUTs refuse a body moving the row to another template or section (it moved them, past that template's lock and across tenants); a level template covers that level's employees (it matched everyone — no level template on UAT); the profile's field checks (each weight 0–1 — the `[Range(0,1)]` int-bounds trap —, the pool name's length, enum values, the default HR reviewer an employee of the tenant); the demo's free-text question built with the template by the performance seeder (an EF step added it to the approved, in-use template). **Folded regardless:** approve/reject preconditions, the duplicate-link guard, the lock's 409 on every template route (four answered 500), "can appeal" on the list and the appeal page reading the cycle (E-d2b residue). **Left to their lanes:** the two-actor rule on template approval (D-12, F); the profile PUT's missing concurrency token and racy name check (J) | E-e |

### 1n. Decisions from slice E-f's source check — settled with the user, 2026-10-01

| # | Question | Decision (2026-10-01) | Affects |
|---|---|---|---|
| D-71 | **The at-risk goal lists** — rule 2 (behind the straight line) is unreachable behind two pre-filters (due within 14 days under 60 %, or marked at risk), and widening them naively admits drafts and puts every live goal on the manager's tab, which does not filter | **Every agreed goal**: both lists admit agreed goals not completed (Approved, running, at risk; never drafts, pending or rejected), the manager's tab lists only goals at risk, and the tiles and columns count the same rule. The demo's list goes from 1 to 19 — the untouched approved goals at 0 % are behind at 75 % of the year | E-f |
| D-72 | **Goal deletes, progress and unlock** — the owner deletes an agreed goal, even a completed one; the goal PUT writes progress past the entries; an entry edit can make a sent-back goal look agreed; deleting the latest entry does not carry back; unlock sets Approved whatever the progress; HR unlocks its own goal | **Before agreement only**: the owner deletes a goal not yet agreed (draft, pending, rejected) — an agreed goal is sent back first — and a delete takes its progress entries; the PUT no longer writes progress; entry edits are refused on goals not agreed, and deleting the latest entry carries back; unlock restores In progress, Completed or Approved from the progress; HR cannot unlock its own goal | E-f |
| D-73 | **PIP review meetings** — `PipReviewMeeting.Status` (batch 1) has no writer, no DTO and no cancel route; "held" is "the date has passed" | **Stored, with writers**: booked = Scheduled; *Record meeting* = Held (a live plan, not a future date); a new Cancel = Cancelled (not a held one); a cancelled meeting takes no edits; the reads use the stored value, and *Next* skips cancelled ones. UAT backfill by § 6's rule (past = held: one demo meeting) as a script for the user. Closed plans take no writes, and an approved plan's goals stay frozen (D-46) | E-f |
| D-74 | **Who writes an appraisal conversation** — the appraisee books, edits, holds and deletes their own (two helpers admit them, and the booker becomes the scheduler); a held conversation's delete moves the appraisal back; type and ids come from the body; the held date is always today | **The line manager, the conversation's scheduler or conductor, or HR when not the subject** — create, edit, hold, delete; the appraisee reads. Type, appraisal, scheduler and conductor are pinned (the scheduler is who booked, the conductor who marks it held); the held date can be stated (not future, default today); a held conversation is never deleted. On withdrawal, unheld conversations are removed and open review events Cancelled (and Cancelled closes an event) | E-f |
| D-75 | **PIP numbering and the plan's own rules** — the recommendation handler numbers `PIP-APR-…` and skips the service's rules; approve and reject act from any status on the no-definition path; the plan's subject records their own outcome; the outcome value is unchecked; the number index is neither unique nor per tenant | **Folded, no migration**: the handler numbers `PIP-yyyy-NNNN` through the service's rule and keeps its checks (one live plan, an HR owner, a supervisor who is not the employee); approve and reject act on a pending plan only; the subject (HR included) does not complete, decide or delete their own plan; the outcome is checked. The number race goes to migration batch 2 | E-f, batch 2 |
| D-76 | **Beyond the rows (E-f)** | **Folded:** goal link ids checked (company, unit and parent goal, library, KPI — tenant, existence, same cycle; a parent the same employee's and not itself; the manager id server-set; P-22 closed as by design; a progress entry's review event the goal's appraisal's); the PIP goal PUT binds its validated DTO (percent 0–100), text lengths checked, the seven goal and meeting routes answer 422/404 not 500; a conversation's review event belongs to its appraisal, and the type error lists every type. **Not folded:** a final review held before its step (§ 5) | E-f |

---

## 2. Lane status

| Lane | Title | Size | Carries (§ 1b, all settled) | State |
|---|---|---|---|---|
| A | Scoring integrity and the settle path | 2.5 days | D-11, D-13, D-22 (A14–A16 only) | ☑ 2026-09-29 — 177/177 twice; staged |
| P | Privacy and access *(new — the review)* | 2 days | D-26 (P18 only) | ☑ 2026-09-29 — 339/339 twice; staged |
| B | One gate evaluator (B1 first); settings enforced or removed | 3.5 days (+1, D-33) | D-32, D-33 | ☑ 2026-09-29. B1 — 256/256 twice; B3–B5 with it; B1 enforces nine of the fourteen settings. B2's rest and B6–B8 in two slices (D-33): B-v — 246/246 twice; B-w — 363/363 twice (the suite holds both), regression 1877/1886; staged. The tolerance moved to batch 2 (D-32); `AppealReevaluationWindowDays`' flip is lane C's |
| L | Goal-driven KPI scoring | 7–8 days (D-31; was 5) | D-15, D-16, D-29, D-30, D-31 | ☑ 2026-09-29 — L-a 157/157 twice; L-b 244/244 twice; L-c 261/261 twice (one suite holds L-b and L-c); staged |
| C | One appeal machine | 2 days (+0.5–1, D-36) | D-22, D-34, D-35, D-36, D-37, D-38 | ☑ **complete 2026-09-30**; C10 was batch 1's. Two slices (D-36): C-a ☑ — 209/209 twice, regression 2089/2098, committed effb567f5. C-b ☑ — source-checked (§ 1f D-37, D-38), `run-final-appeals.mjs` 397/397 twice, regression 2309/2318; staged |
| D | Peer nomination and evaluation integrity | 1 day (1.5–2, the source check) | D-39, D-40, D-41 | ☑ 2026-09-30 — source-checked (§ 1g); `run-final-nominations.mjs` 186/186 twice, regression 2496/2505; staged |
| E | Lifecycle guards (appraisal, cycle, template, settings, goals, calibration, PIP, conversations, definitions) | ~12 days (D-42; was 4) | D-10, D-14, D-17, D-20, D-42–D-58 | ◐ source-checked 2026-09-30 (§ 1h); seven slices E-a…E-g. E-a ☑ (130/130 twice, regression 2631/2640), E-b ☑ (`run-final-lifecycle.mjs` 276/276 twice, regression 2777/2786), E-c ☑ (source-checked, § 1i; 391/391 twice, regression 2892/2901; D-14's migration on UAT), E-d split in two (§ 1j): E-d1 ☑ (542/542 twice, regression 3043/3052), E-d2 split in two (§ 1k): E-d2a ☑ (601/601 twice, regression 3104/3113), E-d2b ☑ (source-checked, § 1l; 706/706 twice, regression 3226/3235), E-e ☑ (source-checked, § 1m; 821/821 twice, regression 3352/3361), E-f ☑ (source-checked, § 1n; 1016/1016 twice, regression 3549/3558); staged. E-g next |
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

**Size:** about **53 working days, eleven weeks** (50 without lane N; lane L grew by three, D-31).
**Migrations:** batch 1 sits at the start of lane A and batch 2 at the start of lane F; batch 3 is
withdrawn (D-21). **Batch 1 is done and applied to UAT (2026-09-29, § 6 State); lanes A, P, B1 and
L are done (2026-09-29, each with its State block in § 4 — lane L has one per slice); B2–B8 are
next.** UAT was rebuilt
from the migration chain the same day and verified (S10's first rebuild, after lane B's gates).

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
    leaver impossible to close. *(Done in lane E-d1, 2026-09-30: HR's withdraw action and both exits —
    D-52 to D-54; the close that relies on it is E-d2's.)*
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

- [x] A0 **Freeze the section weight into the snapshot** (moved here from E4 by the review).
      `PerformanceAppraisalCriterionConfig` gains `AppraisalTemplateSectionId` + `SectionWeightUsed`
      (batch 1), backfilled from the live template for existing snapshots. Every share is computed
      from them:
      - `LoadCriterionScoringAsync` (PAS :898-914);
      - the legacy resolvers (PAS :921-958, :1889-1937);
      - PES :437, :471-472, :492.

      It must land before A3, because settle recomputes every weighted score from raw inputs.
- [x] A1 `AppraisalScoring.OverallScore` returns `decimal?`; nothing grades or syncs a null.
- [x] A2 `IPerformanceRatingResolver.ResolveGradeDefinitionIdAsync`; bands loaded once, ordered by
      min desc; delete `PAS.ResolveGradeAsync` (:242-255) and `CSS.ResolveGradeAsync` (:805-818);
      overlapping bands refused at definition save (P-6). The resolver requires `MappedRating`
      (`PerformanceRatingResolver.cs:75`) — a grade definition without one must be refused at save,
      not silently ungraded.
- [x] A3 `SettleScoreAsync` as above; `ApproveAndFinalizeAsync` settles inside its transaction with
      `publish:false` and publishes after commit (a swallowed EF failure inside a retrying
      transaction poisons tracked entities); delete the :4433-4442 block.
- [x] A4 Calibration commit (CSS :719-740):
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
- [x] A5 `AdjustedScore` = post-resolution overall only when it changed, else null;
      `AppraisalAppeal.OriginalOverallScore` captured at submit-appeal; `GetAppealStatusAsync`
      (:3167) and `GetEmployeeAppealOutcomeAsync` report it.
- [x] A6 MAP :312 stops assigning `OverallScore`; `ToDto` (:224) exposes `CalibratedOverallScore`.
- [x] A7 Every settle call site is wired:
      - HR finalise;
      - completion without HR review (:1986);
      - acknowledge → Completed;
      - appeal Upheld/Rejected;
      - post-remand finalise;
      - manual/auto advance → Completed;
      - calibration of a Completed appraisal.

      `SuccessionNominationHandler.cs:128` reads the settled score. Today calibration (CSS :736) and
      appeal resolution (PAS :3403, :3408, :3736) change the score without syncing.
- [x] A8 `TalentRatingSyncService` recency check (an older cycle's finalisation must not overwrite a
      newer rating); null score leaves the rating untouched and logs.
- [x] A9 Analytics "finalised scores" filter by status, not `OverallScore != null`
      (`PerformanceAnalyticsService.cs:61`).
- [x] A10 **One arithmetic path.** PES's own copy is replaced by the settle path's helpers:
      `CalculateAndSetWeightedScore` (:428-461), `RecomputePeerTotal` (:484-496) and
      `LoadCriterionSnapshotAsync` (:464-475). `AppraisalScoring.cs` stays the single arithmetic
      home.
- [x] A11 **Scores validated on the item's own scale.** Bands may top out below 100 —
      `AppraisalTemplateService.cs:797-817` checks only 0 ≤ low ≤ high ≤ 100 and no overlap. Inputs
      are validated 0..100 (PAS :1433, :2352; PES :230; DTO :1997; `EvaluationScoreForm.tsx:219-225`),
      and achievement is score ÷ top band (PAS :866-868), so an item can exceed 100 %. These are all
      validated from 0 to the item's top band:
      - self, manager and peer inputs;
      - calibration item adjustments;
      - appeal modifications (appeal `NewScore`, DTO :2956, has no range check today).
- [x] A12 **The target shown is the target scored.**
      - Manager and peer section builders read the snapshot's target/min/max, as the self form
        already does (:4713). Today they read the live goal: PAS :4840-4842, :4941; PES :584-586;
        HR review PAS :4121.
      - The frontend preview `kpiAchievementPercent` (`types/hr/appraisal-run.ts:965-972`) mirrors
        `AppraisalScoring.KpiAchievementPercent` (min→target segment, max cap, clamp).
- [x] A13 `CriterionScore.NumericScore` is `int?` while adjustments are `decimal?`: round
      explicitly (or widen in batch 1), never truncate silently.
- [x] A14 KPI adjustments per D-22: the `NumericScore` override is flagged on the row, so the forms,
      HR review, appeal page and PDF say "overridden by calibration/appeal".
- [x] A15 Historical scores per D-13: a read-only dry run of the settle over every Completed/Closed
      appraisal (stored vs settled overall, grade and talent rating) written to a report. Nothing is
      restated without a decision.
- [x] A16 D-11: drop `AppraisalHRReview.AdjustedOverallScore`/`AdjustmentReason` and
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

**State (2026-09-29): DONE — built, verified on UAT, staged.** No migration: batch 1 carried every
column. What exists now:
- **`AppraisalScoreService`** (`Services/HR/Appraisal/AppraisalScoreService.cs`, `IAppraisalScoreService`)
  is the one arithmetic path and the only writer of `OverallScore`: `SettleAsync` (steps 1–6 above),
  `PublishAsync`, `ScoreCriterionAsync`/`ScoreEvaluatorAsync`, `ValidateItemScoresAsync`/`GetScaleTopAsync`
  (A11) and `DryRunAsync` (A15, `GET PerformanceAppraisals/settle-dry-run`). PAS, PES, CSS and AWS
  use it; `CalculateOverallScoreAsync` is a delegate; the PAS and PES copies of the arithmetic, the
  PAS and CSS grade resolvers and PAS's direct talent sync are deleted.
- **Every route to Completed settles in the same save**: HR sign-off (inside its transaction,
  published after the commit), manager submit with nothing after it, acknowledgment, appeal
  upheld/rejected, post-remand finalise, HR's manual/auto advance, calibration of a Completed
  appraisal — and the two raw status routes (`PATCH PerformanceAppraisals/{id}/status`,
  `POST AppraisalWorkflow/{id}/transition`), which A7 did not list (lane E guards both).
- A0: generation writes `AppraisalTemplateSectionId`/`SectionWeightUsed`; scoring reads the frozen
  weight, and the four section builders show it (`ScoredWeight`).
- A2: one resolver (`ResolveGradeDefinitionIdAsync`); grade definitions refuse a half band, a band
  outside 0–100 or inverted, a band with no mapped rating, and an overlap with another active band.
- A12/A14: the manager, peer, submitted-view and HR-review builders read the snapshot's
  target/min/max and show the score's own achievement; a restated KPI is flagged on the manager
  form, HR review, appeal outcome and calibration dialog; the frontend preview mirrors the server.

**Where the build refines the rows above** (each deliberate; say if one should go back):
1. **The settle lives in its own service**, not in PAS: PES, CSS and AWS call it without taking a
   dependency on the 24-dependency appraisal service. B's `SyncLifecycleAsync` calls the same
   `SettleAsync`.
2. **A4's "null clears an earlier session's override" is narrowed**: a calibrated overall stands
   until a later session *restates that appraisal*. A session that adjusts its items clears it (the
   overall follows the items it just changed); a session that adjusts nothing for it leaves it (the
   panel accepted the score as it stood — clearing it would have dropped an agreed 80 back to 72).
3. **Settles A7 did not list**: the manager's submission settles *without publishing* when only the
   acknowledgment remains, so the employee acknowledges a settled score, not a blank; a remanded
   re-evaluation clears the calibrated overall, or the redo could never move the score.
4. **A2 threshold matching**: a band's minimum is its threshold, so a score between two published
   ranges (90.5 between 76–90 and 91–100) takes the lower band; the rating comes from the same band.
5. **"Final" for publishing** is Completed/Closed, or Governance with an approved HR review *and no
   pending remand* — a remanded appraisal still carries the sign-off from before its appeal.
6. **A13**: a calibration item adjustment must be a whole number (refused, not rounded); the commit's
   rounding stays only for rows recorded before the check.
7. **The commit saves per appraisal**, not in one outer transaction: a retrying strategy that re-ran
   the lambda after a partial save would see accepted changes and under-write. Each appraisal's
   adjustments, stamp and score commit together, and a re-run is idempotent.
8. **The snapshot is read with the query filters off** (its own soft delete applied): a template item
   or section deleted after generation used to read as a competency with section weight 0.
9. Evaluator totals are recomputed for **submitted** evaluations only; per-item achievement is clamped
   to 0–1, so one item past its scale cannot make up another's shortfall.
10. A14's flag is **derived** (a KPI row carrying `NumericScore`), with no column; the label is
    "overridden by calibration/appeal".
11. The HR review's peer summaries count **submitted** peers only.

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-scoring.mjs` **177/177, twice** — the plan's assertions with numbers that separate the
  fix from the defect (a calibrated 80 against a computed 72, not 70 against 72, which are both
  "Meets"), plus A2, A5, A6, A8, A9, A11 on all five input paths, A13, A14, A15.
- Regression (`run-all.mjs`): interim reviews 128/128, attachments 64/64, slice C 51/51, slice D
  20/20, gates 32/32, **slice E 17/26 — 9 stale**, verified in SQL: UAT's four seeded definitions are
  active but none sets `preventInitiatorApproval` (so "the submitter cannot approve" fails — F3's
  finding), and slice E looks them up by an `entityType` name the endpoint does not return.
  489/498 across the seven suites.
- API log: no error from the settle or the talent sync; the 85 save errors are defect #23 (payroll
  profile FK, one per employee created).
- Frontend: scoped `tsc` 0 errors over 72 files; eslint clean on the touched files.
- **The harness fixture change (part of S8)**: `buildFixture()` took the first real position with a
  unit — slice E generated appraisals for every real holder of that post (264 in one call, per the
  harness README) and the unit goals were raised in a real unit. It now mints its unit and position
  as `buildClosureFixture()` does; slices D and E close the cycle they open. Slice D's "an Open cycle
  stays Open" was vacuous until now: on UAT the open had always been refused.
- The fixture's appraisals come from generation through a **Position target per settings profile**:
  J's `Employee` target needs an `AppraisalCycleTarget.EmployeeId` column, which is in neither
  migration batch (recorded under J).

**A15's report — UAT, 2026-09-29** (18 Completed/Closed examined, 15 of them E2E). The three in
APC2026: Kojo Fiadzo 89.28 = 89.28; **Efua Seidu stored 89.40 → settled 88.56** (the panel's KPI
92 → 88 now counts); **Cynthia Sarpong stored 88.74 → settled 87.00** (the panel's overall 87.0 now
stands). No grade or rating would change, and none of the three is in a talent pool. Per D-13 nothing
is restated; HR restates one through the audited reopen (D-17) if it chooses. The system guide's
Rule 2 carries this.

**Found on the way, routed:**
- The HR review's **Correct dates** (`appraisal-run.service.ts updateHeader`) sends no score, so the
  mapper set the final score to **null** on every date correction — fixed by A6. The same call still
  **blanks `OverallComments`, strengths, areas, training needs, aspirations, every `Recommend*` flag
  and `RecommendationNotes`**, which the body does not carry either → **E1**.
- `[Range(0, 100)]` on a **decimal** DTO field has int bounds and rounds before comparing: 100.5
  passes (calibration `AdjustedScore`). The service rules hold; the attributes mislead → **J**.
- `TalentPoolMember.LatestPerformanceRating` is documented as a cache of the latest confirmed
  `TalentReviewRating` ("never write in isolation"), and the appraisal sync (Theme 9) writes it
  directly: two writers, two rules → **K** (succession's owner decides).
- HR's advance auto-submits a manager **draft**, which carries no total, so `PreCalibrationScore`
  stays null for it (the settle computes the total later) → **H3**.

### Lane P — Privacy and access *(new, from the review)*

P1–P6 were re-verified in source on 2026-09-28. P7–P17 come from the authorization sweep with file:line
— **verify each before building.**

- [x] P1 **The evaluator/criterion CRUD block** (PAC :433-664, eight routes). Delete it now rather
      than in E1: none has a screen caller, and `GET …/{appraisalId}/evaluations` (:466) hands the
      appraisee every evaluator row — `EvaluatorName`, `TotalScore`, `OverallNotes` and
      `Recommendation` (MAP :338-360) — because `CanAccessAppraisalAsync` (:743-753) admits the
      appraisee.
- [x] P2 **An appraisee-facing projection.** Before sign-off (Completed/Closed, or Governance with HR
      signed) it withholds `Recommend*`, `RecommendationNotes`, `RankIn*`, `PreCalibrationScore`,
      `AdjustedScore`, `OverallScore` and the grade. It applies to `GET …/{id}` and
      `/employee/{employeeId}` (PAC :127-170, MAP :224-262), `my-appraisals` (PAS :1312) and
      `me/trend` (`PerformanceAnalyticsService.cs:104-121`). Lane I's "hide overallScore on the list"
      becomes the server's job.
- [x] P3 **Calibration reads** — paged, {id}, by-cycle, participants, adjustments,
      adjustments/appraisal/{id}, matrix, appraisals/{id}/criteria, and attachments with download
      (`CalibrationSessionsController.cs:87-140, :349-367, :453-492, :589-608, :641-715`). Each needs
      the Read policy **or** panel participation, and a participant sees only the appraisals in the
      session's scope. The "reads stay open to any authenticated user" remark (:638-639) is
      rewritten.
- [x] P4 **PIP create** (`PerformanceImprovementPlansController.cs:41` — `AuthorRoles` includes
      Manager; `PerformanceImprovementPlanService.cs:278-317` checks only that the supervisor exists).
      A manager opens a PIP only for a direct report. `SupervisorId` defaults to the employee's
      manager and only HR changes it; `HROwnerId` must hold HR.
- [x] P5 **PIP update** (MAP :609-623): `EmployeeId`, `AppraisalId`, `SupervisorId`, `HROwnerId`,
      `Status` and `Outcome` are never copied from the body; Draft edits only (Active per E7).
- [x] P6 **Check-in create** (`CheckInsController.cs:285-304` checks only the conductor): the subject
      must be the caller's direct report, or the caller for a self-requested check-in, or the caller
      must hold HR. Check-in update (:327-352; MAP :1963-1977) cannot change `EmployeeId` or
      `ConductedById`, because `Redact` (:168-176) trusts `ConductedById`.
- [x] P7 Goal-update PUT (MAP :2024-2025; `CheckInService.cs:286-310`): `CheckInId` and
      `EmployeeGoalId` are never taken from the body (E10 adds the ownership check on add).
- [x] P8 Goal progress PUT/DELETE (`EmployeeGoalsController.cs:621-667`; EGS :448-495; MAP
      :1900-1902): `EmployeeGoalId` is never taken from the body, and only the recorder (or HR) edits
      or deletes an entry.
- [x] P9 Attachment delete — appraisal (PAC :856-879 → PAS :1163-1177) and check-in
      (`CheckInsController.cs:624-647` → `CheckInService.cs:400-411`): only the uploader or HR, and
      only before completion.
- [x] P10 Development plans (`DevelopmentPlansController.cs:73-83, :307-365`;
      `DevelopmentPlanService.cs:179-225`): the subject cannot delete or complete a plan authored by
      their manager or HR.
- [x] P11 `GET /UnitGoals/{id}/employee-goals` (`UnitGoalsController.cs:421-435`;
      `UnitGoalService.cs:243-264`): per-employee rows only for HR and the managers in that unit's
      line; counts for everyone else.
- [x] P12 `self-evaluation-context` (PAC :951-972; PAS :1341, :1368-1396): the manager sees self
      scores only once they are submitted, and then per `ShowSelfScoreToManager` (B2).
- [x] P13 PIP meeting comment and forms (`PipMeetingController.cs:362-393, :188-208, :235-252`;
      `PerformanceImprovementPlansController.cs:1199-1202`; MAP :683-684): the right of reply is the
      employee's own write, and supervisor forms cannot set `EmployeeComments` or `ConductedById`.
- [x] P14 Peer nomination writes (`PeerNominationController.cs:49-61, :227, :266, :298`): the
      nominated peer cannot edit or delete the nomination. Batch nominate (PAC :1324-1338; PNS
      :98-116, :430-441) honours `PeerNominationMode` and records the real nominator (D1/D4).
- [x] P15 `GET /CheckIns/paged` (:179-194) is redacted like every other read, and `IsHr`
      (:120-121) becomes the Read-policy check. Today TenantAdmin, Admin and "HR User" see private
      notes there and are refused elsewhere. P-27 is then fixed on every path.
- [x] P16 `GET /AppraisalCycleTarget/{id}/exclusions` (`AppraisalCycleTargetController.cs:206-221`):
      HR read only, because it carries the reasons.
- [x] P17 `GET /AppraisalWorkflow/{id}/phase` and `/editable/{role}`
      (`AppraisalWorkflowController.cs:27-46, :84-103`): a party to the appraisal, or HR.
- [x] P18 P-19 per D-26: keep the tenant-wide read and say so on the upload control.

The fallback-path self-approval found by the same sweep is lane F3.

**Assertion** (`run-final-privacy.mjs`): one matrix per route across appraisee, peer, unrelated staff
member, manager of another unit, and HR — expected status, **and the field absent from the body**, not
only hidden. No peer name appears in any appraisee payload while `PeerReviewsAnonymous` is on.
`privateNotes` are absent on every check-in path for everyone but the conductor and HR.

**State (2026-09-29): DONE — built, verified on UAT, staged.** No migration: the one new fact — who
wrote a development plan — uses `BaseEntity.CreatedById`, which every row already has. What exists now:
- **The release rule** (`Services/HR/Appraisal/AppraisalRelease.cs`): an outcome is released when the
  appraisal is Completed, Closed or Appealed, or in Governance with every required calibration
  committed, HR's sign-off given and no pending remand. Until then **the employee's own copy** — the
  appraisal by id, by employee, by status/year/paged, `my-appraisals`, `me/trend` and the HR review —
  carries no overall, adjusted, pre-calibration or calibrated score, grade, ranks, `Recommend*`,
  recommendation notes or manager narrative, and the HR review no manager evaluation, manager/peer/final
  score or HR remarks. `OutcomeReleased` rides on the four DTOs; the screens say *"Not yet released"*.
- **P3** — calibration reads are the desk's or a panellist's (participant or facilitator); the lists
  answer everyone else with only their own sessions; the criteria read is scope-checked for everyone
  (any session id opened any appraisal's criteria); a reader's **own** appraisal is out of every read,
  the grid's counts and average included.
- **P4/P5** — `CanOpenPlanForAsync` (line manager, or the Write desk; never yourself) gates create and
  `prepare`; the supervisor defaults to the line manager and only the desk names another; the HR owner
  must hold the performance Write tier by role or seeded permission (`PipAccess.EmployeeHoldsDeskAsync`);
  the edit carries content only and a draft only.
- **P6–P8** — check-ins open about yourself, a direct report, or anyone for the desk; subject, conductor
  and cycle fixed; private notes written only by the conductor; goal updates and progress entries never
  re-parented; a progress entry amended by its recorder or the desk.
- **P9** — attachment delete on appraisals and check-ins: the uploader, or the Write desk when it is not
  the subject; refused once the appraisal is Completed/Closed/Withdrawn/Appealed or the check-in held.
- **P10–P17** — development-plan authorship (`AuthorUserId`); unit-goal cascade rows for the desk and
  the unit line, `cascade-stats.averageProgressPercent` for everyone; the self-evaluation read
  (`SelfEvaluationView`); the PIP reply as `SetEmployeeCommentsAsync`; the peer-excluding nomination
  write gate and `EnsureMayNominate`; paged check-ins redacted, the desk by policy; exclusions HR-only;
  workflow reads for parties (appraisee, line manager, approved peer) or the desk.

**Where the build refines or extends the rows above** (each deliberate; say if one should go back):
1. **The HR review is withheld too.** P2 listed four routes; the employee's own appraisal page reads
   a fifth, `GET …/{id}/hr-review`, which carried the manager's evaluation, the final score and grade
   and HR's remarks (a draft included).
2. **The two-actor rule wherever lane P touched a gate**: an HR officer who is the subject is the
   subject — check-in notes and management, PIP access and management, the recommendations read, the
   manager's side of an appraisal (peer names under anonymity; nomination approve/reject), their own
   calibration row, a nomination naming them. The desk test used to come first on each.
3. **E10's goal-ownership check (X1) and its development-plan PUT bullet came forward** — the first
   let anyone move a colleague's goal through a check-in about themselves; the second would have let
   P10 be walked round.
4. **PIP drafts are hidden from the employee** (the `Draft` status's own contract), and create now
   checks the employee exists, the source appraisal is theirs, and neither supervisor nor HR owner is
   the employee.
5. **P5 is draft-only**, so E7 only decides whether a live plan's edit becomes a re-approval.
6. **P10's author** is the creating login; the three UAT plans predate the stamp and count as set for
   the employee. "Complete" includes **cancel**.
7. **P11's unit line** is the goal's raiser plus the head of its unit or any unit above
   (`UnitAncestryAsync`). Direct line managers of aligned employees are not included.
8. **P13's conductor** is whoever books the meeting — the body cannot name another (a check-in lets
   the desk name one; a PIP meeting does not).
9. **P14** refuses the peer's update, send-invitation and delete; D1's status and field narrowing
   and D4's no-approval-step and wording stay in lane D.
10. **P16** also answers 404 for an unknown target (it was a 500).
11. **P18 needed no code** — the note has been on the upload card since 0ef42c223.

**Not probed, or carried forward:**
- P15's TenantAdmin / "HR User" case is covered by the switch to the policy in code, not by a live
  login: the suite would have to mint a TenantAdmin login on the demo database.
- P12's second leg (`ShowSelfScoreToManager` off) — no fixture profile switches it off; B2 adds one.
- A check-in's `EmployeeComments` has no door for the employee and stays writable by the conductor —
  the P13 principle applies; lane E or I.
- An HR officer can still propose an outcome recommendation or record a calibration adjustment on
  their own appraisal — segregation of duties, F3.
- The PIP detail page still shows the subject workflow, goal and document controls the server refuses
  (cosmetic; lane I). A PIP's supervisor or HR owner cannot be changed after create (no door).
- Seen in the API log, not lane P: the HR/Identity reconciliation job fails every sweep for TDC/00052
  (`property.manager`) — `SingleOrDefault` over more than one match; pre-existing demo data.

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-privacy.mjs` **333/333, then 339/339** — six HR-side pairs added after the first run so
  every withheld field has a reader who does see it (pre-calibration score, grade, trend score and
  rating, HR review final score and manager evaluation) — and **339/339 again** inside `run-all`.
- Regression (`run-all.mjs`, eight suites): interim reviews 128/128, attachments 65/65 (+1: P3 seats
  the manager on the panel before the reader reads), slice C 51/51, slice D 22/22 (+2: the seating and
  a non-panellist's 403), **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 180/180 (+3: P1 moved
  its evaluator totals onto `step`-checked HR-review and peer-review reads), lane P 339/339. **834/843.**
- Frontend: scoped `tsc` over the 19 changed files, 0 errors (a planted error was caught).
- API log: the 84 save errors are all `FK_PayrollEmployeeProfiles_PayrollPaymentMethods_DefaultPaymentMethodId`
  (defect #23, one per employee created); every notification error is UAT's missing SMTP settings.
- The demo scenarios that touch these doors (060 check-ins and the PIP, 061 nominations and the panel,
  130 the single nomination) read correctly against the new rules. The W3 permissions suite (slice 10)
  was not run: its fixture resolves real TDC staff.

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

- [x] B1 `AppraisalGates` + `SyncLifecycleAsync` + the table above. *(2026-09-29 — the B1 State
      block below.)*
- [x] B2 Settings, each at its write path *(B1 enforces nine of the fourteen settings — the appeal
      pair, the minimum goals, manager goal approval, the three conversation switches, the
      acknowledgment switch and the peer window; the bullets it closed or started are marked.
      Slices B-v and B-w closed the rest, 2026-09-29 — their State blocks below — except the
      tolerance, which moved to batch 2 (D-32))*:
  - [x] `EnableAppeals`/`AppealWindowDays` → `SubmitAppealAsync`; window from
        `EmployeeAcknowledgedDate ?? hrEval.SubmittedDate ?? UpdatedAt` (not cycle end);
        `GetMyAppraisalsAsync` :1288-1296 uses the same `CanFileAppeal`; portal reads `canFileAppeal`.
        *(B1, server side. The portal's appeal button still keys off the status — lane I.)*
  - [x] `ShowPeerScoresToManager` → `GetManagerPeerEvaluationReviewAsync` (:2653) nulls peer scores
        until the manager has submitted. *(B-v — and on `hr-review`.)*
  - [x] `ShowSelfScoreToManager` → `GetManagerEvaluationContextAsync` nulls self item scores + self
        total until the manager has submitted (server-side; the React check stays). *(B-v — on four
        reads; the manager's form now shows the column once they have submitted.)*
  - [x] `ShowScoreBreakdownToEmployee` → every employee-facing DTO strips manager/peer criterion
        scores (`GetAppealPageDataAsync`, `GetAppealStatusAsync`, `GetEmployeeAppealOutcomeAsync`, the
        `/me/performance/appraisals/[id]` read); **regardless of the flag, the manager's leg is
        withheld from the appraisee before HR sign-off** (fixes the leak at :4138-4166; the page's
        comment at `me/performance/appraisals/[id]/page.tsx:37-38` claims the server already does).
        *(B-v — the appeal reads, the HR review and the goal assessments too.)*
  - [x] `MinGoalsPerEmployee` → first evaluation submit refused below it (symmetric with Max);
        `meetsMinGoalCount` surfaced in the manager governance view. *(The refusal is B1's GoalSetting
        gate; B-w put the minimum and maximum on the team desk — the verdict and the row.)*
  - [x] `RequireManagerGoalApproval` → when off, goal submit lands Approved directly. *(B-w.)*
  - [x] `RequireKickOffConversation` / `RequireMidYearConversation` / `RequireFinalConversation` →
        first-eval submit / manager submit / acknowledge-or-finalise refused without a completed
        conversation of that type; `CreateAppraisalConversationDto.Type` no longer defaults to KickOff.
        *(B1 holds the kick-off at the GoalSetting gate and the final conversation as its own step;
        B-w moved the mid-year to the manager's submission and made the type required.)*
  - [x] `AllowAcknowledgmentWithoutConversation` → acknowledge requires a completed FinalReview when off.
        *(B1 — the PendingConversation step.)*
  - [x] `PeerEvaluationOpenMode` → peer draft/submit refused before self-eval submit in `AfterSelfEval`
        (`IsEditableByRole` gains a caller; adds `manager` when remanded). *(B1.)*
  - [x] `AllowPeerKpiEvaluation` → enforced on draft save (:244-279) as well as submit. *(B-w.)*
  - [x] `AllowSelfSoftSkillRating` → keep behaviour; form label "Employees must score every
        behavioural criterion before submitting"; delete dead local PAS ~:1533. *(B-w.)*
  - [x] `AppraisalCompetency.RequireEvidence` → self/manager/peer submit refuse a scored item without
        an evidence link (the "required" marker at `EvaluationScoreForm.tsx:255` becomes true).
        *(B-w — with the door it lacked, and the saves storing the links.)*
  - [ ] `KpiDefinition.TolerancePercent` → used by `KpiAchievementPercent` (within tolerance = 100%).
        The demo KPIs carry 5 (`PerformanceAppraisalDataSeeder.cs:115`), so the demo scores move —
        lane S re-baselines them. **Moved to batch 2 (D-32)**: the snapshot needs the tolerance column
        first, or a later edit of the definition restates scores.
- [x] B3 *(Done with batch 1, 8ff0f448f — dropping the columns took the code, the form and the
      seeder lines with it; B1 re-checked: no live reference outside the legacy migration archive.)*
      Remove `IsManagerAuthoritative` and `RequireDevelopmentPlanUpdate`: entity, DTOs, MAP,
      form (`settings/[id]/page.tsx:485-490, 657-661`), the writers of
      `EvaluatorEvaluation.IsAuthoritative` (`AppraisalCycleService.cs:734`, PAS ~:2391) and its only
      reader `.OrderByDescending(e => e.IsAuthoritative)` (PAS :642). Drop the column too (batch 1),
      since it has no writer afterwards. The seeder sets both settings
      (`PerformanceAppraisalDataSeeder.cs:162, :189`), so S1 goes in the same slice or the build
      fails.
- [x] B4 Finalise honours `RequireSelfEvaluation` / `RequirePeerReviews` (PAS :4371/:4378 are
      unconditional today), as do `CanProceedToHRReview` (:4088) and `IsReadyForHRReview` (:4299).
      *(B1: all three read the gate pipeline, which skips a step the profile does not require.)*
- [x] B5 Delete the dead Peer/HR arms of `DetermineNextStatusAsync` with the method itself. *(B1.)*
- [x] B6 Settings profile validation: Min≤Max peers/goals, deadline bands ordered, weights sum 1;
      `IsDefault` flag replaces "newest row" (P-2) — batch 1 column; backfill rule in S9. *(B-w.
      The weights' sum was already checked. The backfill: the seeder flags* Standard Annual
      Appraisal *on a rebuild, and UAT's was flagged through the new door.)*
- [x] B7 **The gates suite flips every gate setting, not only the 14.** *(B1's suite and
      `run-final-settings.mjs` between them, 2026-09-29 — the B-w State block lists where each
      flip lives. Two stay open: `TolerancePercent` with its batch-2 column (D-32), and
      `AppealReevaluationWindowDays` in lane C.)* B1 re-implements the code
      behind the 36 already-enforced settings, so all of these are covered both ways:
      - `RequireSelfEvaluation`, `RequireManagerEvaluation`, `RequirePeerReviews`, `Min/MaxPeerEvaluators`;
      - `RequireCalibration`, `RequireHRReview`, `HRReviewTiming`, `RequireEmployeeAcknowledgment`;
      - `RequireGoalSetting`, `Min/MaxGoalsPerEmployee`, `RequireManagerGoalApproval`;
      - the three conversation switches, `AllowAcknowledgmentWithoutConversation`;
      - `PeerEvaluationOpenMode`, `AllowPeerKpiEvaluation`, `AllowSelfSoftSkillRating`;
      - `EnableAppeals`, `AppealWindowDays`, `AppealReevaluationWindowDays`;
      - the three visibility flags, `RequireEvidence`, `TolerancePercent`.
- [x] B8 **Transition report.** *(B-w — read-only, on the deadline-enforcement page.)* After B deploys, list the in-flight appraisals whose recorded state
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

**B1 State (2026-09-29): DONE — built, verified on UAT, staged.** No migration: the two new enum values
are stored as integers. What exists now:
- **`AppraisalGates`** (`Services/HR/Appraisal/AppraisalGates.cs`) — pure static, over an
  `AppraisalGateFacts` record: status and appeal state, calibration committed or sitting,
  acknowledgment, the evaluations, nominations, HR reviews and conversations held, the goals **by
  employee + cycle**, whether the set must be locked, and the steps HR advanced past. `Pipeline`
  (honours `HRReviewTiming`), `Resolve`, `Blocker` (the reason), `Check`/`EnsureAt` (throw
  `AppraisalGateException`, an `InvalidOperationException` naming the step — the controllers answer
  422), `ToPhase`, `ExpectedMajorStatus`, `CanFileAppeal`, `IsWaived`, `PeerWindow`, `Label`.
- **`AppraisalLifecycle`** — the one transition table (AWS's; `Withdrawn` has no exits), read by the
  workflow service and `UpdateStatusAsync` alike.
- **`AppraisalLifecycleService`** — the only facts loader (one projection, split query, the cycle's
  settings) with `GetStateAsync`/`GetStatesAsync`/`EnsureAtAsync`/`SyncAsync`. The sync moves the
  major status forward only — never out of Draft, never for an appealed, withdrawn or closed
  appraisal — and settles when the appraisal reaches Completed or the employee's end of the pipeline.
- Every write path in the table above; every reader — the phase endpoint (now with `subStatus`,
  `stepLabel`, `reason`), the HR cycle dashboard, the cycle's current phase (the modal step, *Not
  started* when none), *My Appraisals* (the action follows the step; `isOverdue`; `canFileAppeal`),
  the HR review's `canProceed`, the HR list's `isReadyForHR`, the overdue sweep, `IsEditableByRole`.
- Frontend: the phase rail follows the profile (Calibration and HR Review swap under
  `BeforeCalibration`; steps the profile does not require are not drawn); the types carry the new
  values and the step label; the deadline page's copy.

**Where the build refines or extends the rows above** (each deliberate; say if one should go back):
1. **A new evaluator, not an extension.** The gates read a facts record, not the entity, and
   `AppraisalAdvanceHelpers.cs` is deleted with no forwarding alias — nothing called it after the move.
2. **`SyncLifecycleAsync` became a service** with `EnsureAtAsync` beside it, and it is the only place
   the facts are loaded, so no reader can disagree with a writer.
3. **Two enum values:** `AppraisalSubStatus.Withdrawn = 16` (D-10's status needed a step) and
   `AppraisalPhase.PeerNomination = 9` (the phase enum had no nomination step).
4. **A waiver is the advance log.** A step is waived when an advance row's `FromSubStatus` names it,
   and only the four steps before the manager's can be (goal setting, nomination, self, peer). The
   self arm no longer submits the employee's draft — it stays a draft and does not count in the score.
   The goal arm approves goals by employee + cycle.
5. **HR's advance moves past the current step only**; any other target is refused with both named.
6. **Calibration has a sitting state.** An appraisal on a Pending or InProgress session reads
   *Calibration (In Progress)* and cannot be signed off. A commit that skips an appraisal releases it
   from the session (unless calibrated) and reports why — not at the step, not required, appealed or
   withdrawn.
7. **The mid-year conversation sits at the GoalSetting gate**, beside the kick-off; B2 moves it to the
   manager's submission.
8. **The published score waits for a required calibration** (`IsFinal`), so a settle before the panel
   commits publishes nothing.
9. **Draft → Active on the first submission** (self or manager), and the goal reason tells a draft
   goal from one awaiting approval.
10. **`IsEditableByRole` uses the step windows for Draft and Active alike** — it refused a peer on a
    Draft appraisal the peer service let through.
11. **HR's sign-off, the sync and the settle share one transaction**; the publish follows the commit,
    and the employee is told what the sign-off landed on (acknowledge, final conversation, completed).

**Found on the way, carried:**
- The calibration commit dialog counts every Governance appraisal as at the step; the portal's
  appeal button keys off the status, not `canFileAppeal` — lane I.
- B2's open halves: the mid-year on the manager's submission, `RequireManagerGoalApproval` off landing
  goals Approved, `meetsMinGoalCount` on the manager's view, the conversation DTO's `KickOff` default.
- A held conversation stamps `HeldDate = UtcNow` and nothing records when it really happened, so the
  demo's mid-years read 29 September — after three of the final reviews. A held check-in can be
  completed again (`CheckInService.CompleteAsync` re-stamps `ConductedDate` and overwrites the notes).
  Both lane E.
- **Fixed in the demo pack, 2026-09-29 (pre-existing, found here):** `060` re-completed Efua's Q3
  one-to-one on every run (it tested a `status` the DTO does not have) with `discussionNotes`, which
  the endpoint drops, so the check-in was held with no shared or private notes; `061`'s final reviews
  for the three finished tracks posted dropped fields too. Both now send the endpoints' fields, and
  `060` completes the check-in only while it is unheld or has no notes. Book 3's step, which sent the
  presenter to the conversations screen to record notes on a check-in the data holds as held, now
  walks the held check-in and schedules the next one (the guide's LIVE WRITE 5); claim [172] with it
  (S11). **The current UAT keeps the empty notes until it is rebuilt** — a held conversation cannot be
  completed again; a `060` re-run would restore the check-in's. The guide's Rule 6 and conversation
  counts (8 → 14) are corrected. B8's report is still owed.
- **The harness on UAT:** by the end of the day 121 E2E cycles of 2026 and 350 fixture staff sat
  there (184 from lane P's runs, 166 from B1's). TDC/00104, which Book 1 creates live, was taken at
  01:31 by a lane P closure fixture, and `verify-runbook` fails six headline counts until UAT is
  rebuilt — lane S8.
- **The demo pack took the first row the list served.** `060`/`061`/`062` took the first cycle of
  the year and `060` the first PIP; the harness's rows come first now. The first `060` re-run wrote
  into the closed cycle E2EINTL763902 — 15 goals for the five tracks, 3 unit goals, 2 targets and an
  exclusion, 4 progress entries, 2 required skills, 4 journal entries — and 2 meetings on a fixture's
  PIP. Fixed in the pack (the cycle by code, the PIP by employee); the 33 rows stay on UAT, in a closed
  E2E cycle, until the user removes them or rebuilds. The pack's SQL helper also passed the sa password
  on the `sqlcmd` command line, where a failed query printed it; it goes through `SQLCMDPASSWORD` now.

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-gates.mjs` **245/250** on the first build — five failures, one cause: an appraisal the
  commit skipped stayed linked to the session and read *Calibration (In Progress)*. Fixed on the read
  and on the commit, six assertions added, then **256/256** on the second build and **256/256 again**
  inside `run-all`.
- Regression (`run-all.mjs`, nine suites): interim reviews 128/128, attachments 65/65, slice C 51/51,
  slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182 (+2: its `a2`
  meets the self-evaluation gate and HR advances past it), lane P 340/340 (+1: its `a2` submits a
  self-evaluation first), lane B1 256/256. **1093/1102.**
- Frontend: scoped `tsc` 0 errors (a planted error was caught); ESLint clean.
- API log: no unhandled exception; the runs' 22 `SystemExceptionLogs` rows are handled 4xx — the
  suites' asserted refusals.
- Demo, on the current UAT after the second build: `060` and `061` re-run. Kwasi (TDC/00006) is at
  HR Review with the kick-off, mid-year and final review held; Kojo Ansah (TDC/00063) at Peer
  Evaluation — three goals, both setup conversations, two approved nominations, the self-evaluation
  of 28 September; Efua, Kojo Fiadzo and Cynthia Completed and untouched. `verify-runbook` 150/150
  names, `verify-tables` 586/586.

**Demo impact:** under the gates **102 of APC2026's 107 appraisals sit at Goal Setting** — 73 Draft
and 24 Active with no goals, 5 with fewer than the three the profile asks for — and the dashboard
reads them overdue. That is the strict reading of this profile, not a fault; a spread is lane S's,
and B8's report is HR's list to waive. Book 3's live sign-off of Kwasi lands him at Acknowledgment,
because his final review is already held.

**B2–B8 source check (2026-09-29, after round 4 merged; line numbers as of `f8899d915`).** Every open
row was live — none had been fixed on the way. Six go further than the rows above say:
- **The visibility flags reach more reads than listed.** Self scores reach the line manager through
  `manager-evaluation-context` (PAS `MapManagerItem` :4450-4454), `view-submitted-evaluation` (PAS
  :1333, the whole submitted self-evaluation), `hr-review` (PAS :3565 — `SelfEvaluation`, `SelfScore`)
  and `EmployeeGoals/by-appraisal` (EGS :118-160 — the goal self-assessment); peer scores through
  `manager-peer-evaluations` (PAS :2073) and `hr-review` (`PeerEvaluationSummary`, `PeerScore`). All
  admit the line manager (`CanAccessAppraisalAsync`, PAC). Lane P's `SelfEvaluationView` hides a
  submitted self-evaluation from the manager **for good** when the flag is off; the setting's contract
  is *until the manager has submitted*, and the React check on the manager's form hides it for good too.
- **Two leaks ignore the flags altogether.** `appeal-page-data` (PAS :2206) returns the manager's
  per-criterion scores and the overall with no release check — callable before HR's sign-off.
  `EmployeeGoals/by-appraisal` returns the **manager's** goal assessment to the appraisee at any time,
  and the employee's goal self-assessment to the manager as a **draft** (it is written on every self
  save, `SaveGoalAssessmentsAsync`, PAS :1255) — lane P12's rule, missed on this read.
- **`RequireEvidence` has no door and its links are dropped.** The competency DTOs (AppraisalDTOs
  :253-290) and the mapping never carry it, the criteria page cannot set it, the seeder writes false.
  `EvaluationItemInputDto.EvidenceLinks` arrives on every self, manager and peer save and **no save
  stores it** — `CriterionScore.EvidenceLinks` is read in five places and written nowhere.
- **`AllowPeerKpiEvaluation` reaches the score.** The peer draft save refuses a goal row (L7) but not
  a template KPI item (PES :283), and `ScoreEvaluatorAsync` scores every row present — a KPI score
  saved in a draft counts in the peer total, so in the overall.
- **`RequireManagerGoalApproval` off** also lets the GoalSetting gate count **drafts** toward the
  minimum (`AppraisalGates` :253 guards the draft check with the flag). With B2's "submit lands
  Approved", a draft is the employee's unsubmitted work; `GoalSetRules.LockBlocker` already refuses it.
- **The team desk's governance status** (`TeamGoalsQueryService.DeriveGovernanceStatus`) ignores the
  profile's minimum and maximum: one goal at weight 100 on a 3-goal profile reads *Structurally
  complete*. `EmployeeGoalSummaryDto.MeetsMinGoalCount` exists; no screen reads it.

Confirmed as written: the mid-year sits in the GoalSetting blocker (`AppraisalGates` :275) — the
gates suite never asserts it, so its move breaks no assertion; `CreateAppraisalConversationDto.Type`
defaults to `KickOff` (a body without a type creates a kick-off, which the gate counts — the fix is a
nullable `Type` with `[Required]`, since `[Required]` on a non-nullable enum is a no-op); the
`AllowSelfSoftSkillRating` local (PAS :1130) is dead and the form's label false (employees may always
rate soft skills; the flag makes it compulsory); nothing validates Min ≤ Max (peers, goals) or the
deadline bands (`DeadlineRiskHighDays` < `Medium` < `Low`); **UAT holds 117 profiles and no default**
(batch 1's backfill ran on the rebuilt, empty UAT) so `GET /default` answers with the newest —
today a harness gates profile — and the list page badges it. `AppealReevaluationWindowDays` only sets
the remand deadline (PAS :2935), which P-71 leaves unreachable: B7 covers it in lane C.

Demo impact, measured on UAT: APC2026's profile has all three visibility flags **on**, goal approval
on (3–6 goals), all three conversations required, peer KPI scoring and the soft-skill switch off, and
ordered deadline bands. `061` sends a type on every conversation and its peers score the two
competencies only, so nothing breaks; the mid-year's move only relaxes the scenario order. The
tolerance moves four tracks (D-32).

Found on the way, not lane B's: *Quality Defect Rate* is a lower-is-better KPI and
`KpiAchievementPercent` assumes higher is better (lane S, or v2's KPI direction); the appeal page
offers competencies only, never a goal row (lane C).

- [x] **Slice B-v — visibility (D-33).** *(2026-09-29 — the B-v State block below.)* One rule for what a reader of an appraisal may see
      (`AppraisalVisibility`, beside `AppraisalRelease`), applied on every read above:
      - the line manager sees self entries once the self-evaluation is submitted, and then only when
        `ShowSelfScoreToManager` is on **or they have submitted their own evaluation**; peer scores
        likewise per `ShowPeerScoresToManager`. The two-actor rule: the line manager is the manager
        even when they hold the desk;
      - the appraisee sees the manager's and peers' legs only once the outcome is released (P2's rule),
        and then criterion by criterion only when `ShowScoreBreakdownToEmployee` is on — otherwise the
        overall, the grade and the narrative;
      - nobody but the author reads a draft (the goal assessments' self side and manager side alike);
      - the reads: manager context, `view-submitted-evaluation`, `self-evaluation-context` (lane P's
        view re-based on the rule), `manager-peer-evaluations`, `hr-review`, `by-appraisal`, the three
        appeal reads (the page answers nothing before release); the screens say what is withheld, and
        the manager's form shows the self column once they have submitted.
- [x] **Slice B-w — write paths, B6, B8 (D-33).** *(2026-09-29 — the B-w State block below.)* Goal submit lands Approved when approval is off
      (no line manager needed then) and the gate holds drafts either way; the mid-year from the
      GoalSetting blocker to the manager's submission; `Type` required on a new conversation; peer
      KPI rows refused on the draft save and dropped at submit; the soft-skill label and the dead
      local; `RequireEvidence` given a door (DTOs, mapping, criteria page), the saves storing
      `EvidenceLinks`, the three submits refusing a scored item without one; the team desk's
      governance status and row through `GoalSetRules` with the minimum and maximum; B6 (the
      validation, `IsDefault` on the DTOs, a make-default door, `GET /default` by the flag, the
      list page's badge, the seeder flagging *Standard Annual Appraisal* — S1's line, brought
      forward); B8 (a read-only transition report for HR beside the audited advance).
- B7 lands across both slices in a new suite, **`run-final-settings.mjs`**: each setting B2 enforces,
  flipped both ways, plus the flips B1's suite never made (`RequireSelfEvaluation` off, goal approval
  off with goal setting on, the mid-year, peer KPI off, the soft-skill switch on).

**B-v State (2026-09-29): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **`AppraisalVisibility`** (`Services/HR/Appraisal/AppraisalVisibility.cs`) — pure static, beside
  `AppraisalRelease`: `ReaderOf` (the appraisee first, then the line manager, else the desk — the
  controllers admit nobody else to these reads), `For` → an `AppraisalView` (`SelfEntries`,
  `PeerScores`, `ManagerScores`, `ManagerNarrative`), `ForManager` (the manager's form, whoever opens
  it), and the facts three ways: `From(appraisal, settings)` for a read that has loaded them,
  `From(gateState, lineManagerId)` for one that holds the lifecycle state, `LoadAsync` — one
  projection — for the rest.
- **Every read of an evaluation asks it:** the manager's form (no self draft ever; the self entries
  as the view says; `SelfEvaluationSubmitted`/`SelfScoresWithheld`), `view-submitted-evaluation`
  (`EntriesWithheld`), `self-evaluation-context` (lane P's `SelfEvaluationView` re-based on the view,
  applied in the service; `SelfEntriesWithheld`), `manager-peer-evaluations` (drafts never; scores
  and comments as the view says; `ScoresWithheld`), `hr-review` (per leg; `ScoreBreakdownShown`,
  `SelfScoresWithheld`, `PeerScoresWithheld`; the narrative-only manager leg for a released appraisee
  without the breakdown), `EmployeeGoals/by-appraisal` (each side of the goal assessments), the appeal
  page (nothing before the release; item scores with the breakdown only), the appeal status and the
  appeal outcome (`ScoreBreakdownShown`).
- Frontend: the manager's form shows the self column once the manager has submitted (the server's
  flag, not the switch); the peer panel, the employee's outcome card and the three appeal pages say
  what is withheld.

**Where the build refines the rows above** (each deliberate; say if one should go back):
1. **A manager's draft reaches the desk**, as the HR review always showed it; it never reaches the
   appraisee (the release). The row said "nobody but the author reads a draft" for both sides of
   the goal assessments — that holds for the self side and for peers.
2. **The desk reads no self draft either** — on the HR review and the goal assessments, as P12 made it
   on the self-evaluation context. HR's review page is used after the submissions, so nothing it
   needs is lost.
3. **Peer comments go with peer scores** — the switch says "scores"; the rationale is anchoring, and a
   comment anchors as a number does. Who the peers are and whether each has submitted stay listed.
4. **A self or peer draft is withheld whatever the switches** — the manager's form showed a draft's
   scores, the peer review a draft peer's.
5. **The appraisal's attachments are not withheld** — the Evidence tab is the appraisal's, shared by
   both parties, not the self-evaluation's. (The self evidence links on the items go with the entries.)

**Found on the way, carried:** a peer scoring a goal row (`AllowPeerKpiEvaluation` on) does not appear
on `manager-peer-evaluations`, which lists competency items only (lane D); the appeal page offers
competencies only, never a goal row (lane C, already noted).

**Verified** (Staging API on `ErpSystemDB_UAT`, after the round-4 merge was migrated in place):
- `run-final-settings.mjs` **246/246, then 246/246** — 29 presence pairs against 30 marker absences;
  V2's manager moves from withheld to shown on the same six reads once they submit.
- Regression (`run-all.mjs`, twelve suites): interim reviews 133/133, attachments 65/65, **slice C
  49/51 — two assertions read the employee's DRAFT goal self-assessment as the manager**, the leak
  this slice closes: re-pointed to the employee's own read, with the manager's absence as the pair,
  then **52/52**; slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182,
  lane P 340/340, lane B1 256/256, lane L-a 157/157, lanes L-b/L-c 261/261, lane B2 246/246.
  **1763/1772** with slice C's re-run.
- API log: no unhandled exception; the errors are UAT's missing SMTP, defect #23's payroll-profile FK
  (one per fixture employee) and the HR/Identity reconciliation's TDC/00007.
- Frontend: scoped `tsc` over the 16 touched and dependent files, 0 errors (a planted error in a
  probe file was caught); ESLint clean.
- Demo: APC2026's profile shows all three, and its 98 unsubmitted self-evaluations hold no draft
  entries, so no walk changes.

**B-w State (2026-09-29): DONE — built, verified on UAT, staged.** No migration: the two new desk
verdicts are computed, never stored. What exists now:
- **Goal approval off** (`GoalWorkflowCommandService.SubmitGoalAsync`): the submit reads the cycle's
  profile. With `RequireManagerGoalApproval` off the goal lands **Approved** on submission, its
  approval date stamped, and an employee with no line manager may submit — a manager is needed only
  to route an approval. A goal whose cycle cannot be read is treated as needing approval.
- **The GoalSetting gate holds drafts either way** (`AppraisalGates`): with approval off it reads
  "one goal is still a draft, not yet submitted". A draft used to count toward the minimum.
- **The mid-year holds the manager's submission** (`AppraisalGates.EnsureManagerMaySubmit`, after
  `EnsureAtAsync` on the manager's submit): the appraisal waits *at* Manager Evaluation, the blocker
  names the mid-year while it is missing, and the manager's drafts are never held. A profile that
  requires only the mid-year has no goal-setting step now; the phase rail agrees.
- **A conversation names its type** (`AppraisalConversationService`): `Type` is nullable and
  `[Required]` on the create and update DTOs; a body with no type, or a number the enum does not
  define, is refused (400), and the mapping throws rather than assume one.
- **Peer KPI off** (`PeerEvaluationService`): the draft save refuses a template KPI row ("Peers do not
  score measured work (KPIs) in this cycle.") as L7 refused a goal row; the submit drops any barred
  row a draft still holds — soft-deleted, with a warning in the log — and totals the rest. The test is
  `AppraisalCriterionScoring.IsMeasured`.
- **Every behavioural criterion** (`AllowSelfSoftSkillRating`): the behaviour is unchanged; the
  settings form's label says what it does, the self-evaluation page tells the employee, and the dead
  local is gone.
- **Evidence** (`AppraisalEvidence`, new): the competency DTOs and the mapping carry
  `RequireEvidence`, and the criteria page sets it (a switch and a column); the self, manager and peer
  saves store `EvidenceLinks`; the three submits refuse a **scored** criterion that requires evidence
  and has no link, naming it. An unscored entry needs none; a goal row never does. The form's
  "required" marker was already wired to the flag, so it now shows.
- **The team desk** (`TeamGoalsQueryService`): the verdict holds a set to the cycle's minimum and
  maximum — **Below minimum** and **Above maximum**, after *Awaiting approval* and before *Invalid
  weight* — over the live goals (all but rejected). The row carries `LiveGoalCount`, `MinGoals`,
  `MaxGoals`, `MeetsMinGoalCount` and `WithinMaxGoalCount`; the page shows the hint and "(min n)" or
  "(max n)" beside the count. *Lock set* shows only on a complete set, so a set below the minimum no
  longer offers the lock that refuses it.
- **B6** (`AppraisalSettingsService`): create and update refuse a minimum above its maximum (peers,
  goals) and deadline-risk bands out of order (high ≤ medium ≤ low, none negative), each saying so
  (400). `IsDefault` is on the DTO; `POST /api/AppraisalSettings/{id}/make-default` (performance write
  policy) clears the previous default and sets the new one in one transaction, so the one-default
  index never sees two; `GET /default` answers the flagged profile, or 404; the default cannot be
  deleted. The list page badges it and offers *Make default*; the seeder flags *Standard Annual
  Appraisal* when the tenant has no default.
- **B8** (`AppraisalWorkflowService.GetTransitionReportAsync`, `GET
  /api/DeadlineEnforcement/transition-report/{cycleId}`): the cycle's in-flight appraisals (not
  Completed, Closed, Appealed or Withdrawn) whose records run ahead of where the gates hold them —
  each with the step it waits at and why, what is recorded ahead (`AppraisalGates.RecordedAhead`,
  the profile's own steps only) and whether HR can waive the step. It changes nothing; the
  deadline-enforcement page shows it as *Records ahead of the gates*, beside the audited advance HR
  waives with.

**Where the build refines the rows above** (each deliberate; say if one should go back):
1. **An edit could move a conversation to another appraisal** — the update copied the body's
   `AppraisalId` in, after the caller was authorised against the conversation's own appraisal. It is
   pinned now. Found on the way, not a row.
2. **A barred peer row is dropped at the submit, not refused** — the form cannot show a KPI row to be
   removed, so a refusal would strand the peer. The row's note goes with its score.
3. **The desk mirrors `GoalSetRules`' live count rather than calling it** — its rows are counts from
   one projection, not goal rows; the rule (live = not rejected) is the same one.
4. **Each evidence refusal takes its submit's existing shape** — the self and the manager a failed
   result (400), the peer a 422.
5. **`GET /default` no longer falls back to the newest profile** — no flag, no default (404). The
   demo pack's `060` asks for the default and then for *Standard Annual Appraisal* by name; it took
   the first row the list served, which a harness profile wins on UAT.

**B7 — where each flip lives.** `run-final-settings.mjs`: the three visibility flags (B-v), goal
approval **off** with goal setting on (W1, with the approval-on pair), the mid-year (W2), peer KPI
**off** (W3), the soft-skill switch **on** (W3; off on W1), `RequireEvidence` **on** (W3),
`RequireSelfEvaluation` **off** (W4), and B6. `run-final-gates.mjs` (B1): the sign-off chain and its
orders — calibration, HR review and `HRReviewTiming`, the final conversation, the acknowledgment and
`AllowAcknowledgmentWithoutConversation`, the manager step off, both peer windows, appeals off — and
the kick-off and goal approval on. `run-final-goalset.mjs` (L-a): the goal minimum and maximum at
the lock. Peer KPI **on** is lane A's `a3` (peers score KPIs), and evidence off is every other
suite's competency, scored with no link. Open: `TolerancePercent` (batch 2, D-32) and
`AppealReevaluationWindowDays` (lane C).

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-settings.mjs` **363/363, then 363/363** — B-v's 246 and B-w's 117: goal approval off
  (30 — with the manager-less submit, and its refusal on an approval-on profile), the conversation
  type and the re-parenting (11), the mid-year (12), peer KPI, the behavioural criteria and evidence
  on all three submissions (26 — a KPI row planted on a peer's draft is dropped at the submit: the
  total is 66; counting the planted row would make it 78), no self-evaluation and the transition
  report before and after HR's waiver (18), B6 (20).
- Regression (`run-all.mjs`, twelve suites): interim reviews 133/133, attachments 65/65, slice C
  52/52, slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane B1 256/256, lane L-a
  157/157, lanes L-b/L-c 261/261, lane B2 363/363. **Lane P 337/337 — three fewer, as expected**: its
  profile needs no goal approval, so its three goals are agreed on submission and the helper's
  approve step no longer runs (the diff against B-v's run is exactly those three lines). **Lane A
  died at `a3`'s manager form on a SQL timeout** (30 s); in the same two minutes a probe login took
  52 s. Neither is a B-w path — the manager form's read is unchanged since B-v — and lane A alone
  then passed **182/182**, with a read-only watch on SQL Server's waits that saw no request over a
  second. The stall is unexplained. **1877/1886** with lane A's rerun.
- API log: no exception from a B-w path. The errors are UAT's missing SMTP (the notification
  processor), defect #23's payroll-profile FK (135 — one per fixture employee, each with its
  fallback), the HR/Identity reconciliation every five minutes ("more than one element" reconciling
  `property.manager`, TDC/00052, whose manager TDC/00007 holds two logins), and lane A's timeout.
- Frontend: scoped `tsc` over the 13 touched files and 7 dependents, 0 errors (three errors planted in
  a probe file were all caught); ESLint clean on the 13.
- Demo: the phase of every one of APC2026's 107 appraisals, snapshotted before and after the build,
  is byte-identical; no team desk changes verdict (Kojo Ansah and Efua Seidu hold three goals each, on
  a 3–6 profile). UAT had **no default profile** — 117 profiles, and batch 1's backfill ran on the
  empty rebuild — so *Standard Annual Appraisal* was flagged through the new door, as the seeder does
  on a rebuild.

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

- [x] L0 **The template model (D-15).** *(L-a deleted the Tier 1 override. L-b: the section kind on
      the template's DTOs and the forms, and the template service's rules — one goals section, no
      items in it, a copy keeps it. L-c: the editor's picker and its text.)*
      - Templates are per population; assignment scopes already exist.
      - Section kinds are `Fixed` (competencies; shared KPIs with one target; free-text questions)
        and `EmployeeGoals`.
      - A template KPI item means "the same KPI and target for everyone on this template".
      - ~~Delete the Tier 1 override (EAC :310-322), so a goal is never counted twice.~~ *Done in L-a.*
      - The template editor explains the split in its own text.
- [x] L1 **Schema** (batch 1, completed by the review). *(Done with batch 1; lane L's source check
      found every column and all four indexes in the entities, the EF configuration, the model
      snapshot and the migration. Nothing writes the config ids yet — L-b.)*
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
- [x] L2 **Snapshot at goal-set lock.** *(L-b built the rows — `AppraisalGoalRowService`, the L-b
      State block; the deadline sweep that locks a set is H2's, D-16.)* *(L-a built the lock itself — the flag, D-29 — the manager's
      "lock goal set", HR's advance locking the agreed set, the unlock fix and the edit rules (D-30).
      The rows are L-b's. Premise corrected by the source check: `KpiDefinition` carries no default
      grade ranges to copy — a measured row scores by achievement %, a rated row takes the tenant's
      overall scale (L6).)*
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
- [x] L3 **Forms and scoring.** *(L-b built the server side — every site below but the frontend
      bullet, keyed by the criterion key; L-c built the frontend bullet.)* About 40 sites keyed by `TemplateItemId` move to the config id. The
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
- [x] L4 **Template editor.** *(L-c. The third bullet had nothing to change: the cycle's coverage
      preview names no criteria, and calibration's labels come from the snapshot since L-b.)*
      - A section kind picker. An `EmployeeGoals` section has no items and shows its weight and rule
        text; activation counts it complete.
      - **P-8:** activation and submit-for-approval (`AppraisalTemplateService.cs:922-931, :323`)
        stop demanding grade bands on a weight-0 free-text item.
      - Coverage and calibration screens read labels from the snapshot.
- [x] L5 **Governance at goal-set lock:** `Min/MaxGoalsPerEmployee` and weight sum 100 (P-20). The
      only existing weight guard (EGS :278-279, :306-307, :643-659) is never called, because the
      routed path is GWC :124-199. HR reads only. The guide chapter "who decides the KPIs" is written
      from this. *(L-a: `GoalSetRules.LockBlocker`, read by the set lock; the weight total is defined
      once — live goals only — and the team view follows it. The dead EGS guard is lane J's.)*
- [x] L6 *(L-b; the KPI definition's tolerance stays B2's.)* **Rated goals** (no numeric target, D-16) are scored on the tenant's overall grade scale as
      bands. Measured goals use actual vs target/min/max, with the KPI definition's tolerance
      (B2).
- [x] L7 **Neighbours.** *(L-a: interim reviews, check-ins and progress entries move a locked goal —
      D-29. L-b: the three bullets below; the period rule is defined in the L-b State block.)*
      - Peers score goal rows only when `AllowPeerKpiEvaluation` is on (PES :321-331, :521, :545).
      - Interim reviews read the locked goal set only (`AppraisalReviewEventService.cs:197-307` scores
        every cycle goal into `OverallPeriodScore` today).
      - A period-scoped goal (Q1, H1) is not counted again at year end.

**Assertion** (`run-final-goalkpis.mjs` — L-b wrote it with every row below but the free-text item;
L-c added that one, and the payload the screens send):
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

**L-a State (2026-09-29): DONE — built, verified on UAT, staged** (decisions D-29–D-31, § 1c). No
migration. What exists now:
- **`GoalSetRules`** (`Services/HR/Appraisal/GoalSetRules.cs`) — the goal-set rules in one place: a
  live goal is any not rejected; an agreed goal is approved or running (the old `Locked` status
  included); a lock is the flag (the old status still counts); the weight total is live goals only;
  `LockBlocker` says why a set cannot be locked, in the goal-setting gate's words. The gate's goal
  record reads the same rules.
- **The lock (D-29)** — `LockGoalAsync` sets `IsLocked` + `LockedDate` and leaves the status alone.
  Progress entries (add and amend), check-in goal updates and interim reviews move a locked goal; a
  goal still in the old `Locked` status reads as approved. Unlock returns such a goal to Approved.
- **`LockGoalSetAsync`** (`POST api/EmployeeGoals/lock-set`) — the direct manager locks every live,
  unlocked goal of the employee's set for a cycle once `LockBlocker` passes: all agreed, the count
  inside the cycle's minimum and maximum, the weights adding to 100.
- **The edit rules (D-30)** — an edit refuses a changed owner or cycle and never touches the
  appraisal link (the mapper no longer copies any of the three); a create derives the link on the
  server; an agreed goal's title, KPI, measure, target, minimum, maximum, unit, weight, period and
  success criteria are refused (description, priority, dates and alignment are not); the manager's
  **send-back** is `RejectGoalAsync` extended to an approved or running goal, not locked or completed.
- **HR's advance past goal setting** approves the submitted goals, locks the agreed set, and leaves
  drafts and rejected goals out.
- **L0** — the Tier 1 override is gone; the configuration service no longer reads goals (L-b brings
  them back for the goal rows).
- **Team views** — the locked count and Locked tab read the flag; a locked goal can be overdue; the
  weight total leaves rejected goals out.
- **Frontend** — *My goals* carries the fields its dialog does not show (an edit used to wipe the KPI,
  range, success criteria and alignment); *Record progress* stays on a locked goal there and on the
  desk's goal page; the lock banner says what a lock is; the desk's goal page has **Send back**.

**Where the build refines or extends the rows above** (each deliberate; say if one should go back):
1. **Send-back needed a code change.** The answer that led to D-30 called rejection "the existing
   path"; `RejectGoalAsync` refused an approved goal. It now accepts Approved, InProgress, OnTrack
   and AtRisk; Completed is refused (*"a completed goal's result stands"*), and a locked goal is
   refused as locked.
2. **HR's waiver approves only what was submitted.** It used to approve drafts and rejected goals too.
3. **The measured fields frozen by D-30** are title, KPI, measure, target, minimum, maximum, unit,
   weight, period and success criteria — what the manager agreed to score.
4. **The per-goal lock stays**, without the set rules; only the set lock checks the count and weights.
5. **The overdue rule now leaves rejected goals out** on the overview count, as the Overdue tab
   already did.
6. **No data migration for the old `Locked` status**: the rebuilt UAT has none, and code reads it as
   approved and locked.

**Found on the way, carried:**
- `GetPeerEvaluationDetailAsync` is one 12-way query. On the rebuilt UAT, with 0.8 GB of RAM free,
  it timed out on every peer save of `061` (the saves had committed; the reads back failed). A split
  query would not need a memory grant that large — lane L-b touches the peer form anyway.
- The evaluation saves accept goal assessments for any goal id — L-b (L3 rewrites them).
- With `RequireManagerGoalApproval` off, a goal never becomes agreed and so cannot be locked — B2.
- The manager's *Lock goal set* button — L-c. *Done in L-c.*

**Verified** (Staging API on the rebuilt `ErpSystemDB_UAT`):
- `run-final-goalset.mjs` **157/157**, then **157/157 again** inside `run-all`.
- Regression (`run-all.mjs`, ten suites, on the rebuilt UAT): interim reviews 128/128, attachments
  65/65, slice C 51/51, slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A
  182/182, lane P 340/340, lane B1 256/256, lane L-a 157/157. **1250/1259.** API log: no SQL timeout
  this time; the 270 save errors are all defect #23's payroll-profile key, one per fixture employee.
- Frontend: scoped `tsc` 0 errors over the two changed pages (a planted error was caught); ESLint
  clean. Demo pack: `verify-paths` clean after Book 3's goal aside was rewritten.
- The rebuilt demo under this build: Kwasi at HR Review, Kojo Ansah at Peer Evaluation, the three
  finished tracks Completed. The only locked goals in APC2026 are ten seeded ones on estate fixtures
  with no manager, so the team views show the demo nothing new.

**Demo impact:** none on scores — the seeded goals Tier 1 used to read carry the template's own
target. The rebuild itself moved Efua to **88.56** and Cynthia to **87.00** (lane A's settle path, as
its dry run said), and Efua's appeal window now runs to 6 October; the guide carries both.

**L-b State (2026-09-29): DONE — built, verified on UAT, staged** (decisions D-15, D-16, D-31). No
migration: batch 1 had every column and index. What exists now:
- **The criterion key** (`CriterionTemplateKey`). A template row keeps its template item id as its key,
  so every row written before L-b needs nothing; a goal row is keyed by its own snapshot row. A score,
  a remand snapshot row, an appeal item and a calibration adjustment resolve to the same key through
  `TemplateItemId ?? CriterionConfigId`; an adjustment is the overall by its `IsOverall` flag, no
  longer by a missing template item.
- **`AppraisalGoalRowService`** — the goals section's rows. One per locked goal in the year-end
  periods (full cycle, H2, Q4); weights normalised to 100 by largest remainder; the goal's own target,
  minimum, maximum and unit (`KpiTargetSource.Goal`); **measured** with a target, **rated** without
  (D-16), on the tenant's overall grade scale as bands. Rebuilt at a goal's lock, the set lock, HR's
  advance past goal setting (logged: *"Built the goals section: n goal row(s)."*), generation and an
  unlock; a rebuild that changes nothing writes nothing. Once a goal row is scored in a **submitted**
  evaluation the section is fixed: the unlock is refused (422) and a lock adds no row. A goal that
  leaves the set takes its row, and any draft score on it, with it.
- **Scoring** (`AppraisalScoreService`). Keyed by criterion. `AppraisalCriterionScoring.Resolve` says
  what an input names: a template item (what the forms have always sent), a snapshot row, or a goal
  row's key sent as `templateItemId` (so a form keyed by criterion may send its key) — and refuses an
  input that names none of the appraisal's criteria. One achievement rule serves the score and the
  assessment mirror.
- **The forms** (self, manager, peer, the submitted view). The goals section shows its rows — each
  with `criterionKey`, `scoringMethod`, `employeeGoalId` and the snapshot's label, target and unit;
  every section carries its `kind`. `TemplateItemId` is null on a goal row in every DTO that lists
  rows.
- **The saves.** Every score row carries its `CriterionConfigId` (a goal row's has no template item).
  The year-end goal assessments refuse a goal outside the employee's set — they took any goal id —
  and a goal row scored in the save is mirrored into its assessment (actual when measured, and the
  percentage) over the panel's figures; the panel's status, notes and evidence stay.
- **Calibration.** An adjustment names a template item or a snapshot row, refused when it is not the
  appraisal's; `IsOverall` is set, not inferred; the panel's criteria, the commit and the matrix pair
  by key; the adjustment lists load the criterion's name (they read "one criterion").
- **Appeals.** An appeal item may name a goal row (refused when not the appraisal's); the employee's
  status, HR's review, the resolution's restatements, the post-remand comparison and the outcome pair
  by key; the remand snapshot keeps `CriterionConfigId` and `ActualValue` (C5 needs both).
- **HR's review.** A goal row sits with the KPIs or the competencies by its measured flag, flagged
  `isGoal`.
- **Templates.** `Kind` on the section DTOs (an update without one keeps it); one goals section per
  template, no items in it, a section with items cannot become one, a copy keeps the kind;
  activation counts an empty goals section complete; the snapshot skips a goals section's items. The
  section endpoints answer 409 for these and for a template on a live cycle (they 500'd).
- **Neighbours (L7).** A peer sees the goal rows read-only and is refused a score on one unless
  `AllowPeerKpiEvaluation`; the full interim appraisal lists and scores the locked set only (422
  otherwise).
- **The peer form's read** is split per collection — the twelve-way query that timed out in L-a.

**Where the build refines or extends the rows above** (each deliberate; say if one should go back):
1. **The period rule.** L7 said a period-scoped goal is "not counted again at year end". The year-end
   section takes full-cycle, H2 and Q4 goals; a Q1, Q2, H1 or Q3 goal belongs to its interim review.
2. **What freezes the section**: a score in a submitted evaluation, not any score. A self-evaluation
   draft may be saved during goal setting, and would have frozen the section at its first locked goal.
3. **Stricter saves**: an input naming a template item outside the appraisal's snapshot is refused;
   it used to be scored against the live template.
4. **Peers**: refused a goal-row score when `AllowPeerKpiEvaluation` is off. A KPI row is still caught
   only at submission — lane D.
5. **Assessment ownership**, carried from L-a's "found on the way", is refused for any goal outside
   the employee's set for the cycle.
6. **The section endpoints' 409**, for every structural refusal on a section.

**Found on the way, carried:**
- **P-71 — the remand's dead end** (guide chapters 29 and 33, Appendices C and E). The remand leaves
  the manager's evaluation submitted, so the re-evaluation cannot be saved; the post-remand decision
  does not wait for one; *Rejected* keeps the current scores. The guide claimed the opposite on all
  three and offered a remand as a demo ending — corrected: never remand in the demo. **Decided with
  the user, 2026-09-29: fixed in lane C (C3, C5), not now.**
- The appeal page's list is still competencies only (C6); goal rows are appealable through the API.
- **L-c:** the section-kind picker and its text; the forms keyed by criterion — `toItemScores` keys
  by template item, so a goal row cannot be saved from today's screens; the goal assessment panel
  for goals with rows; the snapshot labels on the coverage and calibration screens; the manager's
  *Lock goal set* control. *Done in L-c (its State block, below).*
- The self and manager saves still include `appraisal.Goals`, which nothing reads now (lane J).

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-goalkpis.mjs` **244/244**, then **244/244 again** inside `run-all`.
- Regression (`run-all.mjs`, eleven suites): interim reviews 133/133 (+5: the locked set), attachments
  65/65, slice C 51/51, slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A
  182/182, lane P 340/340, lane B1 256/256, lane L-a 157/157, lane L-b 244/244. **1499/1508.** API
  log: no SQL timeout; every save error is defect #23's payroll-profile key, one per fixture employee;
  the notification sender's failures are Staging's missing SMTP.

**Demo impact:** none. No demo template has a goals section, and a template row scores, shows and
calibrates as before. The guide carries L-b's notes and P-71.

**L-c State (2026-09-29): DONE — built, verified on UAT, staged** (decisions D-15, D-16, D-31). No
migration. What exists now:
- **P-8** (`AppraisalTemplateService.ValidateTemplateWeightsAsync`): a free-text question with weight
  0 is never scored, so activation and submit-for-approval no longer demand its grade bands; a
  weighted question still needs them. The refusal names an item by its criterion, KPI or question —
  the check never loaded the criterion or the KPI, so it named them by id.
- **The submitted view** carries each row's `scoringMethod` (`SubmittedEvaluationItemDto`): a goal
  row has no KPI, so the screen could not otherwise tell a measured goal from a rated one.
- **The types** (`types/hr/appraisal-run.ts`, `appraisal.ts`, `calibration.ts`, `appeals.ts`,
  `goals.ts`): every row carries `criterionKey`, a nullable `templateItemId` and `criterionConfigId`;
  sections carry `kind`; `isMeasuredItem` reads the row's scoring method and `isGoalItem` its goal
  (`isKpiItem` is gone); `toItemScores(items, values)` keys by criterion and sends both ids; a
  calibration adjustment carries `criterionConfigId` and `isOverall`. `KpiTargetSource` was typed
  with values the server never sends; it is `'Goal' | 'Template'`.
- **The forms** (self, manager, peer) key by criterion: a goals section's card says what fills it, a
  goal row carries a **Goal** badge, and the input follows the row's measured flag. The peer form
  takes its locked rows from the server's `isScoreable` and sends only the rows the peer may score.
- **The goal assessment panel** takes the goals on the form (`scoredOnForm`) and greys out their
  actual and percentage — entered once, on the row.
- **Calibration**: the overall adjustment is found by `isOverall`; a criterion adjustment sends its
  template item or its snapshot row; goal rows carry the badge.
- **Appeals (HR)**: the restatements are built from the review's appealed criteria by key and send
  both ids; the comparison and the employee's outcome page key by criterion.
- **The template editor** (L4): the section dialog asks what fills the section — *This template's
  items* or *Each employee's locked goals* — and refuses the second, with the reason, when the
  template has a goals section or the section holds items; the goals card has its badge, no *Add
  item* and its rule text; a note explains the two kinds; a weight-0 question reads *Not scored*;
  the live checks flag a goals section that holds items.
- **The team desk**: *Lock set* on a report whose verdict is Structurally complete, behind a
  confirmation, with the server's reason on a refusal (`employeeGoalService.lockSet`); *Set locked*
  once every live goal is locked.
- **HR's review**: the Goal badges, and the tables' titles say when goals are in them.

**Where the build refines or extends the rows above** (each deliberate; say if one should go back):
1. **L4's third bullet had nothing to change.** The cycle's coverage preview names no criteria, and
   calibration's labels have come from the snapshot since L-b.
2. **The *Lock set* button follows the desk's verdict, not the lock's rule.** A rejected goal keeps
   the verdict at *In progress*, so the button waits for the employee to rework or delete it, though
   the lock leaves a rejected goal out; the count limits are the lock's alone, and a refusal's toast
   names them.
3. **The P-8 refusal names its items** (it named a criterion or KPI by id).

**Found on the way, carried:**
- **The HR/Identity reconciliation sweep fails every five minutes on Grace Danquah** (TDC/00052,
  `property.manager`). Her manager, Stephen Boateng (TDC/00007), has two active logins — the demo
  persona seeder binds `head.estate` and `authorised.signatory` to the same post — and the sweep's
  manager lookup (`HrIdentityReconciliationService.ResolveEligibleUserForEmployeeAsync`) expects one
  (`SingleOrDefault`). Not performance, and not HR's code (the Estate team's seeder, the platform's
  sweep): raised with the user for the cross-module register.
- The manager page highlights an appealed goal row by key, but a remand never reopens the form —
  P-71, lane C3. The appeal page still offers competencies only — C6.
- **Not browser-walked.** The screens were checked by type, lint and the payload they send (the
  suite's s6), not clicked. Nothing on the demo database has a goals section, so the first click
  through one is lane K's walks or the next demo rehearsal with a scratch template.

**Verified** (Staging API on `ErpSystemDB_UAT`):
- `run-final-goalkpis.mjs` **261/261** — L-b's 244 and 17 new: P-8 both ways on a scratch template,
  and a draft saved exactly as the screens send it (both ids on a template row, the snapshot row
  alone on a goal row, the values read back, a mismatched pair refused) — then **261/261 again**
  inside `run-all`.
- Regression (`run-all.mjs`, eleven suites): interim reviews 133/133, attachments 65/65, slice C
  51/51, slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182, lane P
  340/340, lane B1 256/256, lane L-a 157/157, lanes L-b and L-c 261/261. **1516/1525.** API log: no
  SQL timeout; every save error is defect #23's payroll-profile key (102, one per fixture employee);
  the notification sender's failures are Staging's missing SMTP; and the reconciliation failure
  above.
- Frontend: a scoped `tsc` over the 18 changed files and every file that imports a changed module —
  0 errors in them (the 52 it reports are in untouched modules: medical, civil engineering, estate,
  portal, reports; a planted error was caught); ESLint clean on all 18.

**Demo impact:** no score, form or calibration changes — no demo template has a goals section. Two
things show:
- **The team desk's *Lock set*.** Five APC2026 sets are complete (three agreed goals, weights 100)
  and unlocked, so the button shows on `md.tdc`'s desk (Kojo Fiadzo), `gm.ops`'s (Kwasi Danquah),
  `head.dev`'s (Kojo Ansah; Efua once chapter 19's LIVE WRITE 4 approves her fourth goal) and
  `she.manager`'s (Cynthia Sarpong), and each would lock. The guide's chapter 19 walk says not to
  press it — a locked goal cannot be deleted, which is how Appendix E puts back writes 3 and 4 — and
  its step 3 script, stale since B1's re-run, now names Kojo Ansah's set.
- **The editor**: the demo template's weight-0 question reads *Not scored* instead of a red *None*,
  and a re-activation would no longer be refused on it.

### Lane C — One appeal machine

- [x] C1 *(C-a.)* Delete legacy `FileAppealAsync` (:311-353), `ResolveAppealAsync(ResolveAppraisalAppealDto)`
      (:453-500), interface members (`IAppraisalServices.cs:64-65`), controller actions
      (`PerformanceAppraisalsController.cs:334-404`), their DTOs and FE types. No FE caller.
- [x] C2 *(C-a — `LatestAppealQuery`; the index was batch 1's.)* `GetLatestAppealAsync` (newest by `SubmittedDate`) replaces the four unordered lookups;
      one open appeal per appraisal (enforced by C10's index, not only a check).
- [x] C3 *(C-a — see its refinements.)* *(Confirmed live by L-b — P-71 in the guide; decided 2026-09-29 to wait for this lane.)*
      Remand **keeps `Status=Appealed`** (finding: Active made it a normal pipeline row), clears
      the manager's `SubmittedDate` and `CalibratedOverallScore`, sets the remand dates; the manager's
      remand re-evaluation settles (`publish:false`), stays Appealed with `CurrentAppealStatus=Remanded`,
      clears `AppealRemandDeadline`, notifies HR; `GetManagerEvaluationContextAsync` :2256
      `isEditable` gains the remand case; the manager's screen shows the remand deadline.
- [x] C4 *(C-a.)* `ResolveAppealAsync`: `Rejected` with `CriteriaModifications` → 422; `Upheld` mods write
      `NumericScore` (D-22, typed like calibration, validated per A11), clear `CalibratedOverallScore`,
      settle; both end Completed + publish. Justification required server-side (FE :105 substitutes
      text today).
- [x] C5 *(C-a — and a calibrated overall with it.)* *(L-b: the remand snapshot now writes `ActualValue` and `CriterionConfigId`, and the
      comparison pairs by criterion key — restore by that key.)* `FinalizePostRemandAppealAsync`: `Rejected` restores `NumericScore/ActualValue/Notes` from
      the remand snapshot (needs `AppraisalCriterionScoreSnapshot.ActualValue` — batch 1;
      `CreateManagerEvaluationSnapshotAsync` :2083-2096 writes it); `Upheld` keeps the re-evaluation;
      outcome text matches (:3837 is false today).
- [x] C6 *(C-b — see its State block.)* *(L-b: goal rows are appealable through the API, by their snapshot row.)* KPI items appealable again (the majority of the score): the appeal page lists KPI and goal
      rows, and a modification is the achievement-% override of D-22 — not an `ActualValue` change;
      remove the "deprecated" hard-empties (PAS :2836, :3398, :3673, :3877).
- [x] C7 *(B1's `CanFileAppeal`, and C-a.)* Appeal on a Governance appraisal refused; resolution never bypasses HR sign-off;
      `HRCanModifyScores` stays the guard.
- [x] C8 *(C-a.)* **P-50:** submitted appeal items must belong to this appraisal's snapshot and be in the
      appealable list. Today `TemplateItemId` is copied as sent (PAS :2910-2921; the legacy writer
      :334 likewise).
- [x] C9 *(C-b — and the peers' average and the weighted column, never set.)* **P-49 is worse than recorded.** HR's appeal review sets `Weight = 0` on every row
      (PAS :3319, since 2d8781248); it reads from the snapshot instead.
      `GetAppealReviewDataAsync` never loads `Items.TemplateItem` (PAS :3237-3239, :3305-3308), so
      the criterion names built at :3317 are probably blank. Probe first, then fix.
- [x] C10 *(Batch 1: `UX_AppraisalAppeal_OneOpenPerAppraisal`.)* One open appeal per appraisal becomes a filtered unique index (batch 1).

**Assertion** (`run-final-appeals.mjs`):
- `POST /{id}/appeal` → 404.
- Submit with `EnableAppeals=false` → 422; after the window → 422.
- An item from another appraisal → 422.
- Remand → manager submit → 200; post-remand review → 200.
- Finalise `Rejected` → manager scores equal pre-remand and `adjustedScore` null.
- `Upheld` → `adjustedScore == overallScore != originalOverallScore`; both end Completed.
- HR's appeal review carries real weights and criterion names.

**Lane C source check (2026-09-29, after B-w; line numbers as of `bd4941f71`).** Row by row:
- **C1 live.** `FileAppealAsync` (PAS :304) and the legacy `ResolveAppealAsync` (:446) sit behind
  `POST {id}/appeal` and `POST appeal/{appealId}/resolve` (controller :357-420, the performance write
  policy). No frontend, harness or demo-pack caller. The legacy resolve sets any status and
  `AdjustedScore` on any appeal — a back door past every rule below.
- **C2 live, and narrower than written.** Four lookups take any appeal of the appraisal: status
  (:2675), HR's review (:2832), the resolve (:2954), the pick-up (:3137); the other four order by
  `SubmittedDate`. Since B1 an appraisal takes one appeal ever (`CanFileAppeal` refuses on
  `HasAppeal`), so only legacy data holds two.
- **C3 live (P-71).** The remand sets `Active` (:3065) and never clears the manager's `SubmittedDate`,
  so the manager's save answers "already been submitted" (:1936) — its own comment says a remand
  clears it. The re-evaluation branch (:2061), unreachable, would set `Governance` and tell the
  employee, not HR. **The gates already read a remanded appraisal as an appeal row** (`AppraisalGates`
  :216 → *AppealUnderReview*, mapped to `Appealed`), and batch 1 moved remanded `Active` rows back to
  `Appealed`; the stored status contradicts the evaluator the moment a remand happens. Two things
  follow: `AppraisalRelease` counts `Appealed` as released, so keeping `Appealed` through a remand
  needs `!remanded` there (P2's rule withholds the provisional re-evaluation); and the visibility facts
  read "the manager has submitted", so a reopened evaluation would re-hide the self entries the
  manager has already seen. **The manager's screens already carry the remand** — the list badge, the
  banner with the deadline, the appealed rows marked (`team-appraisals/[id]` :177-371); only
  `isEditable` (:1763, not submitted and Active/Draft) keeps the form shut.
- **C4 live, and worse.** A `Rejected` or `Remanded` decision **with modifications applies them** and
  settles, so a rejected appeal can move the score. The decision is not validated (`[Required]` on a
  non-nullable enum is a no-op): `Submitted` or `UnderReview` is stored as a "resolution" with a
  resolved date. **A remanded appeal can be decided again through `resolve-appeal`**, skipping the
  re-evaluation and the post-remand decision. Done since lane A: each modification on the item's own
  scale (A11), a KPI's as an achievement % (D-22), the calibrated overall cleared, settle + publish,
  Completed. The server requires a justification per modification; HR's page sends *"Adjusted on
  appeal"* when the box is empty (:107), and sends typed scores with any decision.
- **C5 live.** `Rejected` keeps the current scores (:3363-3365), which after C3 would be the
  re-evaluation. The snapshot already keeps `ActualValue` and the snapshot row (L-b). **The finalise's
  re-evaluation check (:3348) is vacuous** — `SubmittedDate` is never cleared, so HR can finalise with
  no re-evaluation; the goal-KPI suite's s5 reaches its decision that way. A rejected remand must also
  bring back a calibrated overall, which the re-evaluation clears (its source is the newest overall
  `CalibrationRatingAdjustment`).
- **C6 live.** The appeal page offers competencies only (:2365); the status page labels a KPI
  *Competency* and names it *Item* (:2693-2720); the review, post-remand and outcome reads carry
  "deprecated" empties (:3020, :3307, :3540); the manager's `AppealedKpiIds` is always empty;
  `KpiScoreModificationDto` is dead (its frontend type is not). The API accepts a KPI appeal the page
  cannot offer — **the demo's one appeal (Cynthia Sarpong, `061`) and lane A's `a1` are both KPI
  appeals** (P-50).
- **C7 mostly done.** B1's `CanFileAppeal` refuses anything but a Completed appraisal inside the
  window with appeals on; A made `HRCanModifyScores` the guard. Left: C1's back door and C4's second
  decision on a remand.
- **C8 half done.** A goal row is checked against the appraisal (L3); a template item named directly
  is stored as sent (:2478), and nothing checks the appealable list.
- **C9 live — confirmed without a probe.** `Weight = 0` (:2921). The names: the review never loads the
  template item (:2832-2835), and only goal rows carry a label (`AppraisalGoalRowService` :173), so
  **every competency or KPI appeal reads with a blank name** on HR's review.
- **C10 done** in batch 1: `UX_AppraisalAppeal_OneOpenPerAppraisal` (Status 1–3, not deleted).

**Beyond the rows:**
1. **A lapsed remand is the next dead end.** Once C3 reopens the evaluation, a manager who misses the
   deadline is refused, and HR's final decision waits for a re-evaluation that cannot come (D-34).
2. **HR's appeal writes have no two-actor check.** An HR officer can pick up, decide and finalise their
   own appeal, or one against an evaluation they wrote. (Only the HR bundle holds
   `HR.Performance.Write`, so a line manager cannot.)
3. **HR's page asks for the post-remand review while it waits**, and the server refuses it until the
   re-evaluation, so the page never shows the due date it was written to show (:255-264).
4. **HR's review shows a self score from a draft** — a self-evaluation HR waived. B-v's desk rule
   missed this read.
5. **The outcome says an upheld appeal adjusted the scores whether or not it did** (:3480). The demo's
   appeal and the guide's recommended ending (ch. 33) are upheld without a change, on a profile where
   HR may not change scores.
6. **A remand on a profile with no manager evaluation** fails at the snapshot with a generic 400.
   *(Corrected at C-a's first run: this row also said a re-evaluation window of 0 set a remand's
   deadline in the past — the profile's DTOs hold both appeal windows to 1–30 days, so 0 never
   arrives. The check C-a first added for it was unreachable and was removed.)*
7. **The re-evaluation tells the employee** "your manager has completed your evaluation", not HR.

**Demo and harness impact.** `061`'s KPI appeal becomes one the page can produce; its "upheld" reads
honestly. The guide's ch. 33 walk tells the room KPIs cannot be appealed — C6 changes that narration;
its recommended ending (uphold, no change) stands. Suites: the goal-KPI suite's s5 finalises with no
re-evaluation and must re-evaluate first; the settings suite's three `appealableCompetencies` checks
follow C6's list; lane A's `a1` stays valid.

Settled the same night (§ 1e): D-34 extend or decide on a lapsed remand, D-35 the two-actor rule both
ways, D-36 two slices.

- [x] **Slice C-a — the appeal machine (D-36).** *(2026-09-30 — the C-a State block below.)* C1 the legacy pair deleted; C2 one latest-appeal
      read; C4 a decision is Upheld, Rejected or Remanded, on an open appeal that is not remanded, and
      only Upheld carries score changes; C3 the remand keeps `Appealed`, reopens the manager's form
      until its deadline, the release rule withholds while remanded, the re-evaluation stays
      `Appealed`, re-settles unpublished and tells HR; C5 `Rejected` restores the pre-remand scores and
      a calibrated overall; D-34 an extension door and a decision after the lapse; D-35 on the pick-up,
      the decisions and the extension; C8 every appealed item one of this appraisal's scored criteria;
      a remand refused where there is no manager evaluation; HR's decision page — the waiting state
      with its deadline, *Extend* and the lapse decision, a
      justification it does not invent, score changes sent only with *Uphold*.
- [x] **Slice C-b — the reads (D-36).** *(2026-09-30 — the C-b State block below; D-37, D-38 in § 1f.)* C6 KPI and goal rows appealable on the page (one list of
      appealable rows, keyed by criterion) and named on the status, review, post-remand and outcome
      reads; the dead KPI DTOs gone; C9 HR's review names and weights from the snapshot; the outcome
      says whether the scores moved; HR's review reads no self draft; the guide's ch. 33.

**C-a State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The legacy pair is gone (C1)** — `FileAppealAsync`, the legacy `ResolveAppealAsync`, their routes, six
  DTOs (two already had no caller) and the four appeal mappers they alone used.
- **One latest-appeal read (C2)** — `LatestAppealQuery` (newest by `SubmittedDate`) behind the status,
  HR's review, the decision and the pick-up.
- **The decision (C4)** — `ResolveAppealDto.ResolutionDecision` nullable and `[Required]`; the service
  takes Upheld, Rejected or Remanded, on an appeal that is neither decided nor remanded; score changes
  only with Upheld (a rejection or a remand carrying one is refused before the profile is asked). The
  decision's refusals answer 422, like every gated appraisal write (they were 400).
- **The remand (C3)** — stays **Appealed**; refused where no manager evaluation was submitted; the
  manager's evaluation **reopens** (`RemandOpen`: remanded, and the manager has not submitted since) —
  the save accepts writes, the form is editable until the deadline and does not read as submitted,
  the team list agrees. The re-evaluation stays Appealed, clears the deadline and a calibrated
  overall, re-settles unpublished, and tells the appeal's reviewer (it told the employee). The release
  rule withholds a remanded appraisal (`Appealed => !remanded`), and the gate's reason says whose move
  it is — "the manager re-evaluates by …" or "HR decides the appeal".
- **The final decision (C5, D-34)** — waits for the re-evaluation or a passed deadline (it checked a
  submission date nothing cleared); *Rejected* — or either decision after a lapse — restores the
  pre-remand scores (`RestorePreRemandScoresAsync`: inputs from the snapshot, rows added since removed,
  the total, the goal assessments re-mirrored) and a calibrated overall; `POST {id}/extend-remand` moves
  the deadline to a later day with a reason, and tells the manager. The post-remand review answers
  while the manager re-evaluates (`awaitingReevaluation`, `deadlinePassed`, `canDecide`, `canExtend`)
  and compares like with like (the overall appealed against the overall now; the manager's totals
  apart — it set the manager's total beside the overall).
- **The two-actor rule (D-35)** — `EnsureNotPartyToAppealAsync` on the pick-up, the decision, the
  extension and the final decision: not the appellant, not the author of the manager evaluation, not
  the appellant's line manager (403).
- **What an appeal may name (C8)** — a criterion the manager's submitted evaluation scored on this
  appraisal (`AppealableCriterionKeysAsync`), once, by either id; a template item named directly was
  stored as sent.
- **HR's decision page** — the waiting state with its deadline (it could not read it), *Extend the
  deadline*, *Decide on the original scores* after a lapse, a justification HR must write (it sent
  "Adjusted on appeal"), score changes sent only with *Uphold*, and dialogs that say what each
  decision does to the scores. The manager's page tells a re-submitted remand from an open one.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **The remand keeps the manager's submission date** — C3 said clear it. "Reopened" is read from
   the dates (remanded, and not re-submitted since), so a lapsed remand closes on the original record
   and a re-evaluation's date says when it came.
2. **The calibrated overall is cleared at the re-evaluation, not the remand** — so a lapsed remand
   leaves it untouched; a rejection after a re-evaluation brings it back, recognised from the appeal's
   `OriginalOverallScore` (nothing moves the scores between the filing and the remand).
3. **A lapsed remand decided *Upheld* keeps the original scores too** — the appeal is recorded as
   upheld, with nothing re-scored; the dialog says so. (The honest outcome text is C-b's.)
4. **The release rule changed with the status** — a remanded appraisal was withheld as Active or
   Governance; kept Appealed, it needed `!remanded`, or the provisional re-evaluation reached the
   employee.
5. **Found at the first run:** row 6 of the source check was half wrong — the profile's DTOs hold the
   re-evaluation window to 1–30 days — and the check C-a first added for it was unreachable; removed
   before the final build.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-appeals.mjs` (new) **209/209, then 209/209**, and a third time inside the regression —
  c1, c8, c4, d35, c3, c5 (with a calibrated overall on its own cycle), d34 (the deadline planted in
  the past by SQL), b6c. Every number exact: 75, 85.5 after a re-evaluation, 75 and 60 restored, the
  manager's rows read back by SQL.
- Regression (`run-all.mjs`, thirteen suites): interim reviews 133/133, attachments 65/65, slice C
  52/52, slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182, lane P
  337/337, lane B1 256/256, lane L-a 157/157, **lanes L-b/L-c 264/264** (261 + 3: s5 now waits for the
  re-evaluation — "awaiting" first, then the manager re-scores goal 1 at 90 %, and the rejection restores
  70; its restatement refusal answers 422, was 400), lane B2 363/363, lane C 209/209. **2089/2098.**
- API log: no exception from a C-a path — UAT's missing SMTP, defect #23's payroll-profile FK (138, one
  per fixture employee), the five-minutely HR/Identity reconciliation, and one run of the platform
  notification cleanup that failed and carried on.
- Frontend: scoped `tsc` over the 4 touched files and 5 that read the appeal types, 0 errors (three
  errors planted in a probe file were all caught); ESLint clean on the 4.
- Demo: every one of APC2026's 107 phases byte-identical to the snapshot after B-w; UAT holds no open or
  remanded appeal (15 upheld, 25 rejected), so nothing waits on the new rules. The guide's ch. 33 walk
  keeps its recommended ending (uphold); a remand is now a working, longer ending.

**C-b source check (2026-09-30, after C-a; line numbers as of `effb567f5`).** Row by row:
- **C6, the appeal page — live.** The list is built from rows whose template item has a competency
  (PAS :2478); KPIs are hard-empty (:2474) and a goal row is never listed. The page sends a template item
  only, so it could not send a goal row. **Demo:** Efua is offered Communication 84 and Teamwork 82; C6
  adds Sales Target Achievement (104 against 100 — 100 %) and Project Delivery Timeliness (restated to
  88 %).
- **C6, the status read — live, and worse than recorded.** Every template item is typed *Competency*
  (:2844); a KPI's name falls through to **"Item"** — the template item is never loaded (:2845) — and its
  target and actual read null. **Demo:** Cynthia Sarpong's status page reads "Item", "—".
- **C6, post-remand — names right, measured rows compare nothing.** Before and after are `NumericScore`
  (:3486, :3490): a KPI or measured goal row whose actual moved 80 → 90 reads "— → —", and the DTO's
  `ScoreChanged` says unchanged.
- **C6, the outcome — measured rows show no score.** `FinalScore` is `NumericScore` (:3806): Cynthia's two
  KPIs read "—". The appealed-items list calls a competency and a KPI alike "Criterion:" (:3778).
- **C6, the dead KPI DTOs — five classes, not one:** `AppealableKpiDto`, `AppealedKpiReviewDto`,
  `KpiScoreModificationDto`, `KpiScoreComparisonDto`, `FinalKpiScoreDto`; the lists and fields that carry
  them (`AppealableKpis`, `AppealedKpis`, `KpiModifications`, `KpiComparisons`, `FinalKpiScores`,
  `AppealItemSubmissionDto.EmployeeKpiTargetId`); the manager context's `AppealedKpiIds` (:1752); their
  TypeScript twins. No reader. The manager's page already marks KPI and goal rows by criterion key.
- **C9 — confirmed.** `Weight = 0` (:3052); every competency or KPI name blank (the template item is
  never loaded, :2963). **And two columns HR's page shows are never set** — *Peers* reads "—" and
  *Weighted* 0.0 on every row. The guide's walk (ch. 33 step 8) narrates "five numbers" on the row; a KPI
  row shows none of them (the manager's input is an actual; the column reads `NumericScore`).
- **The honest outcome — live.** Every upheld appeal "adjusted the scores" (:3755). **Demo:** Cynthia's
  appeal was upheld at 87 → 87, `AdjustedScore` null, and her outcome says her scores were adjusted. The
  status page's *Score now* tile reads "—" and "Unchanged so far" after a final decision.
- **HR's review reads a self draft — confirmed** (the per-criterion query has no submitted filter,
  :3032); the read asks no visibility rule at all.
- **The guide:** Rule 9 ("…and nothing else", :320), ch. 33 step 3 ("Not the KPIs"), step 8 (the five
  numbers), P-47, P-49, P-50 and the L-b/L-c notes. The demo pack's runbooks do not narrate it.

**Beyond the rows:**
1. **The status read leaks the re-evaluation during a remand.** It shows the manager's *current* score
   on each appealed item, guarded by the breakdown switch alone — no release rule. C-a made the remand
   reopen the manager's form, so the appellant watches the re-scoring (drafts included) that the release
   rule withholds everywhere else.
2. **HR cannot open a decided appeal** — the review read refuses Upheld and Rejected (:2971); the queue's
   *Decided* tab links there. UAT holds 81 decided appeals, Cynthia's among them, which P-50 offers to
   open in the demo (D-37).
3. **An HR officer who is the appellant reads HR's review of their own appeal as the desk.**
4. The submit resolves a pair of ids by the snapshot row and ignores a template item that names another
   row; the forms refuse such a pair since L-c, and the new page sends pairs.

Settled the same day (§ 1f): D-37 a decided appeal opens read-only; D-38 each appealed item's score is
recorded at filing. C-b stays one slice (D-36), about 2 days.

**C-b State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **One description of a row for every appeal read** — `PerformanceAppraisalService.AppealRows.cs`:
  `LoadAppealCriteriaAsync` describes each criterion from the appraisal's snapshot (the live template for
  a key an older appraisal's snapshot does not hold) — its kind (*Competency*, *KPI*, *Goal*, or
  *Question* for a template item that is neither), how it is scored, its name, its section and the
  section's weight, its weight within the section, its scale top, and a measured row's target and unit —
  in the forms' order; and reads every score as **one number: a rated row's score on its scale, a
  measured row's achievement %** (the actual against the target, or the percentage calibration or an
  appeal restated it to — D-22), with the actual beside it.
- **The appeal page (C6)** — `AppealableCriteria`, every criterion the manager's submitted evaluation
  scored, in one list keyed by criterion: the list the submit accepts (C8). The page groups it by section
  and sends each row back by both its ids, as the forms do.
- **The status, post-remand and outcome reads (C6)** — each row typed and named (a KPI read "Competency",
  "Item"); a measured row compared and shown by its achievement over its actual (every KPI scored by its
  actual read "—", and a re-evaluated actual read unchanged); the outcome's contested items by kind
  (*"KPI: …"*).
- **What an appeal remembers (D-38)** — each appealed item's score at the filing, in
  `AppraisalAppealItem.OriginalScore` (never written until now). The status shows *when you appealed*
  and, once decided, *now*; the outcome *was → now* on each contested row; HR's review *when appealed*.
  An appeal filed before it falls back to the remand snapshot, else "—".
- **HR's review (C9)** — names, weights and scale tops from the snapshot; every leg on the row's own terms;
  **the peers' average and the weighted contribution**, never set; **the visibility rule** (B-v) for its
  reader — no self-evaluation draft on the desk, an appellant who holds the desk reads as the appraisee;
  `PartyToAppealReason` when the reader may not act (D-35). **A decided appeal opens (D-37)**, with its
  officer, date, notes and the overall appealed against the overall now.
- **The status no longer leaks a remand** — the scores as they stand (and *Score now*) are withheld while
  the manager re-evaluates; the score when appealed stays. `ResolvedDate` only once decided.
- **The honest outcome** — *"upheld and your appraisal was re-scored: your overall score moved from 75 to
  85.5"*, *"upheld, but no score was changed: your overall score stays at 75"*, *"not upheld. The
  original scores stand: your overall score is 75"*; `ScoresChangedAfterAppeal` is the overall or a
  contested row.
- **The dead KPI surface is gone** — five DTO classes, seven properties, `AppealedKpiIds` and their
  TypeScript twins; `ItemWeightsAsync` and `CriterionName(CriterionScore)` lost their callers and went.
- **The screens** — the appeal form by section with each row's kind, weight and score as scored; the
  status's *When you appealed / Now* with the remand's withholding; the outcome's final scores with their
  actuals and *was → now*; HR's page with the named, weighted rows, the peers, the weighted column, a
  KPI's actual, a new-score input bounded by the row's scale (a KPI's an achievement %), the decided
  record, and no buttons for a party.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **HR's score changes go only on a contested criterion** (422 otherwise). C4 let an upheld appeal
   restate any criterion of the appraisal; HR's page offered the contested rows alone, and D-38's
   *was → now* covers only those.
2. **The submit refuses a pair of ids naming two rows** (400), as the forms have since L-c; the snapshot
   row decided and the template item was ignored.
3. **One score per row across the reads** — `PreRemandScore`, `PostRemandScore`, `SelfScore`,
   `ManagerScore` and the outcome's `FinalScore` became that one number (decimal); they were a
   `NumericScore`, which a measured row holds only when restated. A rated row reads as before.
4. **"Changed on appeal" is read, not written** — the kept score against the final one, after the
   decision; `AppraisalAppealItem.ScoreAdjusted` stays unused (residual register).
5. **A row's weight is its weight within its section, with the section beside it** — as the forms show
   them — not its share of the whole form.
6. **The status withholds by the release rule** — C-a's `Appealed => !remanded`, which the status read
   never asked; found by this slice's source check (*beyond the rows* 1).
7. **The goal-row half of C6 is asserted in the goal-KPI suite** (s4, s5), where goal rows are built; the
   appeals fixture has no goals section.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-appeals.mjs` **397/397, then 397/397**, and a third time inside the regression (C-a's 209 and
  C-b's 188) — the first run's 2 misses
  were my expectation of the kept score's text (the model maps `OriginalScore` as decimal(18,4), not the
  entity's (5,2); the values were right). Every number exact: 80 % kept at filing, 80 → 90 on the
  re-evaluated KPI, 75 → 85.5, 72 → 75.75 with a peer at 60, 75 unchanged on the upheld-with-no-change.
- Regression (`run-all.mjs`, thirteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52,
  slice D 22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182, lane P 337/337, lane
  B1 256/256, lane L-a 157/157, **lanes L-b/L-c 296/296** (264 + 32: the goal-row half of C6 in s4 and s5 —
  the appeal page lists both goals, a goal row's score when appealed, its weight and the manager's 60 % on
  HR's review, the outcome's *was → now* and wording, the post-remand comparison of a measured goal
  70 → 90), **lane B2 363/363** (its three appeal-page checks follow C6's list — the KPI row is in it),
  **lane C 397/397**. **2309/2318** — C-a's 2089/2098 plus exactly the 220 new; no assertion lost.
- API log: no error from an appeal endpoint — only UAT's missing SMTP, defect #23's payroll-profile FK and
  the five-minutely HR/Identity reconciliation.
- Frontend: scoped `tsc` over the 7 touched files and the 3 that read their types (HR's queue, the
  manager's team page, the appraisal-run service), 0 errors — the 3 errors planted in a probe file were
  all reported; ESLint clean on the 7. Not browser-walked.
- Demo (no score moves — C-b changes reads): every one of APC2026's 107 phases byte-identical before
  and after the regression, on the C-b build. The demo's one appeal
  (Cynthia Sarpong, upheld) **opens on HR's desk** — read as the desk after the build: *Project Delivery
  Timeliness*, a KPI in *Key Performance Indicators*, weight 50, the manager's 92 % on an actual of 92
  against 100, her own 96 %, peers "—" (peers do not score KPIs), overall 87 → 87, *when appealed* "—"
  (filed before D-38, no remand to recall it); it answered 400. By the paths the suite proves (b2), her
  outcome now reads *"upheld, but no score was changed: your overall score stays at 87"* (it said her
  scores were adjusted) and her status *KPI · Project Delivery Timeliness* (*Competency · Item*). Efua's
  appeal page offers four rows (Rule 9) — from SQL, not read as her (a persona login writes to UAT).
  The guide's ch. 33 walk: step 3's narration rewritten (it told the room KPIs cannot be appealed), step 8's
  five numbers now on the screen (Self 90, Peers 82, Manager 84, weight 50, weighted 16.8 — two showed),
  step 11 names the outcome's wording; the recommended ending (uphold) stands.

### Lane D — Peer nomination and evaluation integrity

- [x] D1 *(2026-09-30 — the D State block below. The nominator from the token was lane P14's.)*
      A nomination starts Pending on both routes — a posted non-Pending status → 422; the edit takes
      `DueDate`/`InstructionsToPeer` only, and only while Pending; not the appraisee, not their line
      manager, not an unknown employee, **on the batch route too** (`hr-portal/run-slice5.mjs:122`
      nominated the manager — fixed in the slice). Approval only through the one path,
      `StageApprovalAsync`, which creates the peer `EvaluatorEvaluation`.
- [x] D2 Counts exclude Rejected — both maximums, the summary (`ActiveNominations`, `CanSubmit`), the
      self-evaluation submit, the panel's *at max* and *N more needed* — so a replacement can be nominated.
- [x] D3 `send-invitation` removed — the route, the service method, its DTO and the client method;
      approval notifies.
- [x] D4 `PeerNominationMode.Manager`: the manager's nominations are approved as they are made, on
      both routes — the peer has the evaluation and is told; nobody is asked to approve; the panel's
      wording follows the mode. *(The access half was lane P14's: `EnsureMayNominate`.)*
- [x] D5 The peer's list and form read the nomination's due date, else the cycle's peer deadline, and
      the nominator's instructions; the dead decrement removed; only a pending nomination is withdrawn.
      *(The peer write's window was B1's: `AppraisalGates.PeerWindow`.)*
- [x] D6 The nominated peer cannot write the nomination (P14). *Done in lane P, 2026-09-29.*

**Assertion** (`run-final-nominations.mjs`):
- A raw POST with `nominationStatus: Approved` → 422, and no peer evaluation exists.
- A PUT changing `appraisalId`/`peerEmployeeId` leaves them unchanged.
- After one nomination is rejected, the employee can nominate another without hitting
  `MaxPeerEvaluators`.
- The peer's own PUT → 403.

*Built 2026-09-30:* every assertion above, and the suite's d3, d4, d5, d39, d40, d41, dpr and dwin
(its header lists them) — 186 in all.

**Lane D source check (2026-09-30, after C-b; line numbers as of `1521aadc9`).** Row by row:
- **D1 live, and wider than written.** `ToEntity` copies the posted status (mapping :1109): a raw POST
  creates an *Approved* nomination with no peer evaluation behind it. `UpdateEntity` copies seven fields
  (:1113-1122): a party can re-point a nomination at **another appraisal** (the edit window is checked
  on the original), swap the peer or the nominator, set it Approved or Rejected, or clear the invitation
  date that guards its deletion. Neither route refuses the appraisee's line manager as a peer (PNS :219,
  :429); the batch never checks its peers exist in the tenant (the single create does, :215).
- **D2 live in five places**: the single and batch maximum (PNS :228, :416), the summary's `canSubmit`
  (:377), the self-evaluation submit (PAS :956, the plan's ":1448") — which counts rejected nominations
  against the minimum *and* the maximum though the B1 gate it runs first counts live ones — and the
  panel's *at max* and *N more needed*.
- **D3 live**: the stub (PNS :331-350, `TODO`), its route, a client method no screen calls.
- **D4 half done** (P14 did the access half): in Manager mode the manager's nominations still land
  Pending and wait for the manager's own approval; nobody is told anything in that mode (PNS :488); the
  panel says "waiting for approval" in both modes.
- **D5 half done**: B1's peer window already refuses a peer write outside the self, peer and manager
  steps (`AppraisalGates.PeerWindow`). Left: the peer's list and form show the **cycle's** deadline,
  never the nomination's due date (PES :147, :220) — **on UAT every demo nomination is due 9 Oct and every
  peer sees 20 Nov** — and the dead decrement (PNS :316-322: approval stamps the invitation date, so the
  delete is refused before it).
- **D6 done** (P14).

**Carried in:** from L-b, a peer's KPI row caught only at submission — **done** by B-w (the draft save
refuses it, PES :285). From B-v, a peer's goal row missing on `manager-peer-evaluations` — **live and
wider**: the read lists competency rows only (PAS :2143), so a peer's KPI or goal score is invisible
wherever peers may score them; weight 0 on every row; a dead KPI list — C9's shape.

**Beyond the rows:**
1. **HR's advance past peer nomination approves pending nominations without creating the peers'
   evaluations** (AWS :309-325): no form, nobody told; the self-evaluation's "form open" notice then goes
   to them (PAS :1236), and the dashboard counts them outstanding for good (D-39).
2. **The peer never sees the nominator's instructions** — neither the peer's list nor the form carries
   them; the dialog asks "What would you like them to comment on?", and scenario 061 writes one.
3. **Manager mode with anonymous reviews**: the appraisee reads who the manager chose through the
   nomination reads — the privacy suite scans seven appraisee reads, not these (D-40).
4. **A peer's own reads** list nominations never approved, with the rejection reason written for the
   appraisee; no screen calls them (D-41).
5. Small: the summary's `canEdit` ignores Manager mode's longer window; Employee mode's "locked after
   self-evaluation submission" is a status rule (Active/Draft), so it holds only once the manager submits.

**Demo and harness impact.** UAT's 10 demo nominations are consistent (none names a manager, none is
an approval without an evaluation, every count matches); the peer page's *Due* reads 9 Oct, not 20 Nov
(guide ch. 28). `hr-portal/run-slice5.mjs:123` nominates the manager — fixed in the slice. The privacy
suite's P14 read by the nominated peer of a pending nomination follows D-41; the gates suite holds. New
suite `run-final-nominations.mjs`. About 1.5–2 days; one slice.

Settled the same day (§ 1g): D-39 the advance approves properly, D-40 counts only for the appraisee in
Manager mode with anonymous reviews, D-41 a peer's reads list approved nominations only.

**D State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **One approval path** — `PeerNominationService.StageApprovalAsync`: each pending nomination becomes
  Approved with its dates (and the due date, when one is given), the peer gets their
  `EvaluatorEvaluation` at the profile's peer weight (never a second one), and the appraisal's peer
  count moves; the caller saves, then `NotifyApprovedAsync` tells each peer, with the due date. Three
  callers: the manager's approve; a **Manager-mode nomination**, approved as it is made on either route
  (D4); and **HR's advance past peer nomination** (D-39), which set the status alone.
- **What a nomination may be (D1)** — Pending, or 422; the appraisee, their line manager and an employee
  who does not exist are refused on both routes; `UpdatePeerNominationDto` carries the due date and the
  instructions and nothing else, and only a pending nomination is edited or withdrawn (422).
- **Counts that leave a rejection out (D2)** — the summary's `ActiveNominations` (pending + approved)
  beside `TotalNominations`; `CanSubmit` and both maximums on the active count; the self-evaluation
  submit the same, as the B1 gate before it already was.
- **One window** — `NominationsEditable`: Employee mode while Draft or Active, Manager mode until
  completed or closed. The summary's `CanEdit` reads it (it ignored Manager mode's longer window), and
  the manager's approve and reject are held to it (they were taken on a completed appraisal too).
- **What the peer reads (D5)** — the nomination's due date, else the cycle's peer deadline, and the
  nominator's instructions, on the assignment list and the form (`PeerEvaluationService.NominationsBehindAsync`).
- **Who reads a nomination (D-40, D-41)** — `PeerNominationController.CanReadNominationAsync` and
  `CanReadAppraisalNominationsAsync`: the appraisee, except in Manager mode with anonymous reviews (then
  the summary sets `PeersWithheld` and lists none); the line manager; the peer once it is approved;
  the desk. A party reads as the party, desk or not. The peer's own lists (`peer/{id}`,
  `pending/{id}`) carry approved nominations only, with no rejection reason.
- **The manager's peer review** (B-v's carried item) — `PeerEvaluatorDetailDto.CriterionScores`, one row
  per criterion the peer scored, competency, KPI or goal, described as the appeal reads describe a row:
  C-b's `LoadAppealCriteriaAsync` is now `LoadCriterionRowsAsync` in
  `PerformanceAppraisalService.CriterionRows.cs` (renamed from `…AppealRows.cs`). The competency
  list at weight 0 and the always-empty KPI list went, with their two DTOs.
- **Gone** — `send-invitation` (D3), the dead decrement, the workflow service's unused nomination
  repository.
- **The screens** — the nomination panel's wording follows the mode (*"They have been asked for their
  feedback"* when the manager chooses; the dialog says so), its counts are the active ones, it shows
  *N not approved*, offers *Remove* on a pending row only, and tells a D-40 appraisee the count; the
  manager's *Peer feedback* lists every criterion with its kind, section, weight and score (a KPI's
  actual against its target); the peer's list and form show the instructions.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **Approval never calls a repository `UpdateAsync`.** A Manager-mode nomination is still being added
   in the caller's unit of work, and an update would turn its INSERT into an UPDATE of a row that does
   not exist; the tracked entities carry the changes.
2. **HR's advance approves through `StageApprovalAsync`, not `ApproveNominationsAsync`** (H3's
   wording): the advance is the decision and saves its own unit of work with its audit row, and the
   window must not refuse it. H3 keeps the peer-evaluation arm.
3. **The approve and reject are held to the window** — found by the source check (*beyond the rows* 5),
   not in the rows.
4. **The Employee-mode refusal says what the rule is** — *"Peer nominations are closed on this
   appraisal: it has moved past the evaluations"*; it said "after self-evaluation submission", which the
   status rule never checked.
5. **A withdrawn nomination frees its place** — the unique index is filtered on `IsDeleted`; d1 withdraws
   one and the count returns to 0.
6. **Withdrawing an approved peer is refused (422)** — the peer has been asked; replacing an evaluator is
   lane M's reassignment.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-nominations.mjs` **186/186, then 186/186**, and a third time inside the regression. No
  first-run misses. Each defect has an assertion the old code fails beside the one it passes (the raw
  Approved POST, the manager as peer on the batch, the unknown peer on the batch, the edit that moved
  the nomination, the replacement at "the maximum of 1", the self-evaluation's count, 13 Nov against the
  cycle's 27 Nov, the instructions, Manager mode's Approved, the advance's evaluation, the D-40 and D-41
  refusals, the KPI row).
- Regression (`run-all.mjs`, fourteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, slice D
  22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 182/182, **lane P 338/338** (337 + the D-41
  pair), lane B1 256/256, lane L-a 157/157, lanes L-b/L-c 296/296, **lane B2 363/363** (its peer-row check reads
  `criterionScores` now), lane C 397/397 (its C-b peer story approves through the new path), **lane D 186/186**.
  **2496/2505** — C-b's 2309/2318 plus exactly the 187 new; no assertion lost.
- API log: no error from a nomination or peer endpoint (19 expected refusals, logged as rule warnings) — only UAT's
  missing SMTP, defect #23's payroll-profile FK, the five-minutely HR/Identity reconciliation and the one
  login 500 below; no 500 from any other route.
- Frontend: scoped `tsc` over the 6 touched files and the 3 that read their types (the two appraisal pages
  and `appeals.ts`), 0 errors — the 3 errors planted in a probe file were all reported; ESLint clean on
  the 6. Not browser-walked.
- Demo: no score or step moves — every one of APC2026's 107 phases byte-identical before and after the
  regression; its 10 nominations (all approved, each with its evaluation, due 9 Oct) untouched. Three screens
  show it differently: the peers' *Peer Reviews* list reads **Due 9 Oct 2026**, the nomination's (it read the
  cycle's 20 Nov) — as does the form of the two peers yet to submit, on Kojo Ansah's appraisal; a submitted
  form shows no date — and the list and every form show 061's instructions; and the manager's *Peer feedback* tab — read as the desk
  after the build — lists each peer's *Communication* and *Teamwork* in *Core Competencies* at **50 %** each
  (they read 0 %, with a *Grade* of "—"): Efua's two peers 80 / 80 and 84 / 84. The demo nominates in
  Employee mode, so D-40 changes nothing it shows. The guide's ch. 28 walk names the date and the
  instructions; ch. 29's step 9 said *"Two of four peers submitted"* — the banner reads 2 of 2 — and now
  says so.

**Harness changes in the slice:** `run-final-privacy.mjs` — the nominated peer's read of a *pending*
nomination is refused now (D-41), paired with the read once it is approved (+1 → 338);
`run-final-settings.mjs` — the peer row's "no scores" check reads `criterionScores` (it passed
vacuously on the gone `competencyScores`); `hr-portal/run-slice5.mjs` nominates the HR actor, not the
line manager, and files the peer leg as them — **not run**: its fixtures hang off the tenant's first real
position (lane S8); `hr-w3-permissions/run-slice10-performance.mjs` loses its `send-invitation` row —
**not run** (it must not run on UAT). The demo pack's 061 holds: Employee mode, no manager nominated.

**Found on the way, not lane D's:** two logins of the same user at the same instant — mine as admin while
the regression's fixture logged in as admin — answered one of them 500 after 17 s:
`SecurityLogService.CreateSecurityLogAsync` hit a `DbUpdateConcurrencyException` (the platform's auth,
not HR's; offered for the cross-module register). No suite was affected.

### Lane E — Lifecycle guards

- [ ] E1 **Appraisal:**
      - **One `AppraisalLifecycle` table**, taken from AWS :27-70; the PAS :533 copy is deleted.
      - **Delete** `POST /{id}/calculate-score` (:309-317). The evaluator/criterion CRUD block goes in
        P1.
      - **Narrow `PUT /{id}`** to the correction the HR review's Correct dates button sends
        (`hr-review/[id]/page.tsx:103, :262`; `appraisal-run.service.ts:293-296`): dates and peer
        count only, with A6's `OverallScore` line gone. It is **not** deleted — the review found its
        caller. ⚠ *Live until then (found in lane A):* that caller sends only id, cycle, employee,
        status, dates and peer count, so every correction **blanks the manager's comments, strengths,
        areas, training needs, aspirations, every `Recommend*` flag and `RecommendationNotes`** (A6
        stopped it nulling the score).
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
        InProgress until D-14 lands. The demo cycle is InProgress. *(D-14 landed in E-c, 2026-09-30:
        a live cycle is Open, and APC2026 is Open.)*
      - **`AcknowledgeAppraisalAsync`** reads HR sign-off from `AppraisalHRReview` (manual advance
        writes that, AWS :550-560) and accepts comments.
      - ~~**Withdrawn** per D-10: an HR action with a reason; `SeparationService.CompleteSeparationAsync`
        (:1240) withdraws the leaver's open appraisal; a withdrawn appraisal leaves every score,
        denominator and close check.~~ *E-d1 — both exits (D-54), before the appraisal is final (D-52);
        the close check is E-d2's.*
- [ ] E2 **Cycle:** *(E-c did every bullet but the close and the Draft generation, which are E-d's.)*
      - ~~Create ignores the body `Status` (MAP :942).~~ *E-c.*
      - ~~Update refuses a settings-profile swap once `OpenedDate` is set (:353), and a year/type
        change once appraisals exist.~~ *E-c, as D-50 put it: profile, year and type once opened or
        with appraisals; the period once with appraisals.*
      - `CloseCycleAsync` (:970-994) refuses while any appraisal is not Completed, Closed or
        Withdrawn, and closes the Completed ones through the lifecycle (**no force flag**).
      - Generation is refused on Draft cycles (:590 refuses only Closed; the FE offers it at
        `cycles/[id]/page.tsx:327`).
      - ~~`AppraisalCycleStatus.InProgress` per D-14: removed, with its six readers, the seeder and a
        data migration.~~ *E-c.*
      - ~~The duplicate cycle-target CRUD (:1706-1796) is deleted in favour of
        `AppraisalCycleTargetService`, with cycle-status checks added. The demo pack's `060:38, :44`
        uses the duplicate (S3).~~ *E-c: the nested routes stay and delegate, so `060` is unchanged.*
      - ~~`CalculateExcludedEmployees` is implemented (it returns 0, :1117).~~ *E-c.*
      - ~~`GetEmployeesInScopeAsync` becomes the generation scope: drop the tenant-wide auto-discovery
        (:1514+) and the inactive targets (:1479).~~ *E-c, with leavers and the level rule too.*
- [x] E3 **Settings profile:** `UpdateAsync` refused while any Open/InProgress cycle uses it
      ("clone the profile"); allowed when all Draft/Closed. *(Lane E-e, 2026-10-01 — refused once any
      appraisal uses it, D-67: the cycle test was a wrong premise; the clone door added.)*
- [x] E4 **Template:** *(Lane E-e, 2026-10-01 — every bullet; D-66, D-68.)*
      - Structural edits reset `ApprovalStatus` to Draft. P-8 is fixed in L4 first, or re-approval
        of the demo template fails.
      - The lock covers any cycle with generated appraisals.
      - The section-weight snapshot moved to A0.
      - **P-7:** the editor's freeze mirrors the server rule instead of freezing on any assignment
        (`administration/hr/performance/templates/[id]/page.tsx:134-138`); the assignment DTO
        (DTO :3773-3789) carries the cycle status.
- [x] E5 **Goals:** *(lane E-f, 2026-10-01 — every bullet: unlock restores from the progress, the dead lock path deleted
      (the "two paths" premise was wrong), the edit and re-parenting rules were L-a's, P-22 closed as by design, rule 2
      reachable — and D-71/D-72/D-76's delete, progress and link rules; the E-f State block.)*
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
- [x] E6 **Calibration:** *(lane E-b, 2026-09-30 — every bullet; the E-b State block.)*
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
- [x] E7 **PIP:** *(lane E-f, 2026-10-01 — every bullet; the Active-plan edit stays a refusal and an approved plan's goals
      are frozen too (D-46, D-73); the numbering race is batch 2's; the E-f State block.)*
      - Goals, meetings and progress (:778-872) are refused on closed plans.
      - `UpdateAsync` (:483-516) on an Active plan is refused or re-approved; body re-parenting goes
        in P5. *(Lane P5 made it a draft-only edit and stopped the re-parenting: an Active plan is
        refused today. E7 only decides whether that refusal becomes a re-approval.)*
      - Handler numbering is aligned to `PIP-yyyy-NNNN`.
      - Meetings get a stored status (P-57, batch 1 column; backfill rule in § 6).
      - `UpdatePipGoalProgress` honours status and percent at create (P-55: status is forced to
        NotStarted at :787, and the create DTO has no percent, DTO :5209-5228).
- [x] E8 **Conversations:** *(lane E-f, 2026-10-01 — every bullet, through write helpers of their own (create used a second
      helper, and the booker became the scheduler), plus the stated held date and the withdrawn residue (D-74); the E-f
      State block.)*
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
      - ~~`CheckInService.AddGoalUpdateAsync` (:249-273) checks the goal belongs to the check-in's
        employee.~~ *Done in lane P (with P7): any user could open a check-in about themselves and move
        a colleague's goal through it.*
      - ~~`DevelopmentPlansController PUT` cannot change `EmployeeId`/`PlanStatus` (MAP :2152-2161);
        status changes only through `UpdateStatusAsync`.~~ *Done in lane P (with P10), which it
        would otherwise have bypassed.*
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

**Lane E source check (2026-09-30, after D; line numbers as of `6d9807840`).** Four read-only reviews
over disjoint rows, then every claim that decides a build read again in source. **The lane is about three
times the plan's four days** — some 11 days of code and harness rework — so it is built in slices (§ 1h).
Row by row:
- **E1 — the appraisal.**
  - *One transition table:* **done** (B1's `AppraisalLifecycle`; the PAS copy is gone).
  - *`calculate-score`:* **live, and worse than written.** It settles with no status check and publishes
    whenever the appraisal is final (`AppraisalScoreService.SettleAsync` :454), so it restates a finalised
    score and moves the talent-pool rating — against D-13's freeze. No screen or demo calls it; three
    suites do (`run-final-scoring` :74, `run-final-gates` :249, `run-final-goalkpis` :374), settling in
    mid-governance, where no lifecycle path settles.
  - *`PUT /{id}`:* **live.** The mapping (MAP :308-332) still copies the five narrative fields, the five
    `Recommend*` flags, `RecommendationNotes`, both ranks and `NextAppraisalDate`, and *Correct dates*
    sends none of them (`appraisal-run.service.ts:301-311`): **every correction blanks the manager's
    narrative and recommendations**. `PeerEvaluatorsCount` is a counter since lane D, so the narrowed PUT
    takes the dates alone. `run-final-scoring.mjs:294` corrects a Completed appraisal.
  - *Raw `POST`:* **live.** No screen or demo calls it; `buildFixture()` (seven suites), `hr-portal`
    slice 5 and the W3 suite do. `AppraisalCycleTarget` has no `EmployeeId` (ACS :1548-1550, "no longer
    stored"), no UAT target is of type Employee, and the target form offers *Individual employee*, which
    resolves to nobody.
  - *`PATCH /{id}/status` and the workflow `transition`:* **live.** Both allow the whole table:
    Active→Completed settles and publishes an appraisal nobody signed off; →Appealed makes an appeal with
    no appeal row; Appealed→Completed abandons an undecided appeal; Governance→Active skips the return's
    resets; Completed→Closed needs no score. PATCH answers 500 to a refusal, the transition 400. Demo
    `061:137` (dropped by S3) and `:366` (Draft→Active, which stays).
  - *Finalise:* **done in substance** (B1 holds it to PendingHRReview; Governance itself is not checked).
    *Return-to-manager:* **live** — it checks only the remarks and sets Active from **any** status (PAS
    :4551, the accidental reopen of D-17), keeps `IsCalibrated`, the session and the calibrated overall,
    keeps the HR evaluation's `SubmittedDate` (the page still reads *finalised*), leaves the
    acknowledgment standing, and saves three times with no transaction.
  - *Header update and delete:* **live** — no status check; both answer 500 to anything but a missing id.
  - *Self, manager and peer writes:* **partly.** Self and peer are held to their steps (B1); the manager's
    **draft** has no gate at all — it creates an evaluation and overwrites the narrative on a withdrawn or
    closed-cycle appraisal. **No write checks the cycle**, and a cycle closes with Active appraisals in it.
    B1 accepts a Draft appraisal's first save on purpose (its refinement 9); that stands.
  - *Acknowledgment:* reading the sign-off **done** (B1); comments **live** (the DTO carries the employee
    id only — demo `061:312` sends comments, dropped).
  - *Withdrawn (D-10):* **live** *(written since E-d1)*. Batch 1 and B1 gave it a member, columns and a gate step; nothing writes
    it, and the separation touches no appraisal. Readers that would count one: the HR dashboard
    (`HRCycleDashboardQueryService` :122-136 and what it feeds), the cycle's progress denominators (ACS
    :1028, :1055-1061), the manager's team summary and list, the HR review list, the employee trend, the
    calibration grid, the peer queue and the close.
- **E2 — the cycle.** Create stores the body's status (MAP :943; integers too): a cycle created Open skips
  the overlap check and stays deletable. Update changes everything once opened — profile, year, type,
  dates — and the overlap check runs only at the open. Close reads no appraisal. Generation refuses only
  Closed: a Draft cycle generates without the overlap check and can then be deleted with its appraisals.
  **D-14: UAT's APC2026 is back at InProgress** — the 2026-09-29 rebuild seeded it after batch 1, so § 6's
  "moves APC2026 to Open" held only until then. Its readers: the open's overlap check, the template lock,
  `CycleCoverageService`, `HRCycleDashboardQueryService`, PAS :1538, two TypeScript unions, two screens;
  its writers: the seeder and the create body. The duplicate target CRUD: **the screens and demo 060 use
  the cycle service's nested routes**, the harness the flat service — so the nested routes stay and
  delegate. `CalculateExcludedEmployees` returns 0. `GetEmployeesInScopeAsync` is not the generation
  scope (a half-wrong premise): it serves the open notification, the reminders and a route no screen
  calls; beside it the dead `CreateAppraisalInstancesAsync` is the snapshot-swallowing copy.
- **E3 — the settings profile:** **live**, and "allowed when Closed" would restate finished appraisals,
  which read the profile live (visibility, the appeal window).
- **E4 — the template:** a structural edit never resets `ApprovalStatus`, and edits run while
  PendingApproval; generation never re-checks Approved; the lock covers Open/InProgress assignments only
  (not a Draft cycle with appraisals, nor a Closed one), while the forms read the live template; a template
  assigned to a Draft cycle can be deleted and generation still uses it; the editor's freeze (P-7) reads
  any assignment. A0's snapshot: **done**.
- **E5 — goals:** L-a did three bullets (the edit rules, re-parenting, the set lock's weights). Unlock
  **partly** (Locked → Approved even with progress); "two lock paths" is a **wrong premise** (the
  service's lock has no caller — dead, deleted instead); the dead Submit/Approve/Reject/Lock **live**; P-22
  **live**; `GoalRiskEvaluator` rule 2 **live** — unreachable behind two pre-filters, and reaching it adds
  every agreed goal behind schedule to the at-risk list (on the demo, every approved goal still at 0 %).
- **E6 — calibration:** scoped to the step and re-settling Completed ones — **done** (A4, A7, B1).
  **Idempotency live, and it undoes appeals**: re-committing the session re-applies its adjustments to a
  Completed appraisal it adjusted (CSS :932-933, `adjustedHere`) — after an upheld appeal it rewrites the
  manager's criterion (:989), restores the calibrated overall (:883), re-settles and publishes, and appends
  the rationale again; the Commit button stays while any row is uncalibrated. The delete guard, the
  session delete's release, `Cancelled`'s door, Start vs Open and P-41 **live**; the update's re-target
  **partly** (confined to the session's scope).
- **E7 — PIP:** writes to a closed plan **live** (and the meeting controller answers 500 to a refusal); the
  Active-plan edit **done** (P5), though goal add, edit and delete still change an approved plan's terms;
  numbering, meeting status (P-57: the column has no writer — on the rebuilt UAT every meeting reads
  Scheduled) and P-55 **live**.
- **E8 — conversations:** **the appraisee can create, edit, complete and delete their own**
  (`CanAccessConversationAsync` admits `Appraisal.EmployeeId == me`, and the desk test comes first, so an
  HR officer who is the appraisee writes theirs); **deleting a held gate conversation moves the appraisal
  back** — the delete has no check and the gates read held conversations, so an appraisee can undo their
  kick-off. `Type` and the scheduler, conductor and review-event ids are copied from the body on edit;
  the held date is always today.
- **E9 — definitions:** all eight **live**. Every delete is soft, so the Restrict keys never fire — and the
  soft-delete filter then drops the rows that require the deleted definition from every query: delete a
  grade in use and the template items' bands and the snapshots' bands vanish from generation and the
  forms. None of the nine delete routes maps a refusal to 422. The cycle-target case is really E2's status
  rule; the template-assignment delete unlocks a template that generated appraisals use.
- **E10 — other:** two items done (P). `CloseAsync` stamps `ApprovedById` on a rejection — the entity has
  no decider columns (**schema**). The self-evaluation's blanket catch returns raw exception text as a 400,
  and **a first submit that fails part-way leaves a submitted self-evaluation with no scores**: it is saved
  early, with `SubmittedDate`, to get its id (PAS :1032, :1061), so every retry is refused "already
  submitted". Journal date and privacy (PATCH and PUT); check-in completion repeatable (demo `060:197`
  relies on it); a plan deleted at any status (`run-final-privacy.mjs:532` relies on it); the interim
  finalise's scores stored as each goal's latest progress (separating them is **schema**);
  `OverallPeriodScore` neither shown nor dropped; P-5 → S1.
- **E11:** built, never asserted. A negative case falls to the tenant-wide pool, which on UAT assigns and
  notifies a real HR officer — positives on UAT, negatives only on a scratch database.
- **E12:** bullet 1 is a **wrong premise** — the swallowing copy is the dead `CreateAppraisalInstancesAsync`;
  live generation rolls back (ACS :679, :719-725). Bullet 2 **live**: no repair action; the Rule 8 five are
  `APR-2026-001…005`; running it on UAT writes demo data (S5 — the user's go).

**Beyond the rows**, the ones that matter most: (1) the re-commit that undoes an upheld appeal (E6);
(2) the half-saved self-evaluation (E10); (3) the manager's ungated draft (E1); (4) HR's own appraisal —
finalise, return and correct skip the two-actor rule (D-35 covered appeals), as do the conversation and
goal-unlock desk paths; (5) a return leaves the acknowledgment standing; (6) the goal PUT copies
`ProgressPercent` (progress with no entry), the goal delete refuses only a lock, goal link ids are
unchecked; (7) a live calibration session can be re-scoped, and `FacilitatedById` comes from the body;
(8) the PIP goal PUT is unvalidated (up to 999.99 %); (9) the assignment PUT re-points a template past its
approval, and a target's type is never matched to its id; (10) unit-goal and journal edits re-point their
author or subject; (11) a rejected or dismissed recommendation can be re-closed; (12) a goal sent twice to
an interim finalise counts twice.

**Demo and harness impact.** D-14 needs APC2026 moved to Open again (a data-only migration). The risk
fix changes the demo's at-risk list. Demo pack: `060:39, :46` (the nested target routes — kept), `060:197`
(completes a held check-in again — S3), `060:240` (PIP goal progress on a Draft plan — P-55), `061:137`
(dropped), `061:249` (`/start`, folded into Open), `061:312` (acknowledgment comments, now kept). Harness:
the three `calculate-score` suites; `buildFixture()`'s seven and `hr-portal` slice 5 if the raw create
goes; every closure suite if generation needs an Open cycle (they generate on Draft cycles by design, 12
call sites in 8 suites, and UAT holds 352 Draft harness cycles); `run-final-scoring`'s A0 freeze
(:411-427) and :294; `run-final-settings` :639 (B8) and :672; `run-final-privacy` :215-219 (the grid's
averages) and :532; `run-interim-reviews` :277-281; `run-sliceD` :121 and `run-sliceE` :161 (they close
cycles with Draft appraisals).

**Slices (D-42)** — each built, verified on UAT and staged on its own; `run-final-lifecycle.mjs` grows
with each:
- [x] **Slice E-a — the appraisal routes.** *(2026-09-30 — the E-a State block below.)* `calculate-score` deleted; `PUT /{id}` takes the dates alone
      and is refused on a Completed, Closed, Appealed or Withdrawn appraisal; `PATCH /{id}/status` and the
      workflow `transition` allow Draft→Active and Completed→Closed (with a score) only — a 422 naming the
      owning action otherwise; return-to-manager held to HR's unsigned review in Governance, resetting the
      calibration and the HR sign-off in one transaction; finalise checks Governance; delete only a Draft
      appraisal, or an Active one with nothing submitted; the acknowledgment takes comments; the manager's
      draft held to the appraisal's status; HR's own appraisal (finalise, return, correct, delete) refused
      to its appraisee; the self-evaluation's submission saved whole or not at all, its blanket catch gone;
      422s where the routes answered 500. Harness: the three `calculate-score` suites and
      `run-final-scoring:294`; demo `061:137`.
- [x] **Slice E-b — calibration.** *(2026-09-30 — the E-b State block below.)* The commit skips what the session already calibrated (the appeal
      undo); the adjustment delete guarded as the update is; the update pinned to its appraisal; a live
      session's scope and facilitator pinned (`FacilitatedById` from the token); deleting a session
      releases its appraisals, and a Cancel (Pending or in progress) does the same; *Start* folded into
      Open (demo `061:249`); P-41 — the grid reads the settled and calibrated overall once committed.
- [x] **Slice E-c — cycle rules and D-14.** *(2026-09-30 — the E-c source check and State blocks below;
      § 1i D-47–D-50.)* D-14 (the member, its readers, the seeder, a data-only
      migration moving UAT's APC2026 to Open); create ignores the body's status; update refuses a profile,
      year or type change once opened and a date change once appraisals exist; the target routes — the
      nested ones delegate to the target service (the duplicate goes), the cycle pinned, the
      duplicate-scope check on update, refused on a Closed cycle; the *Individual employee* target goes
      (D-44); `CalculateExcludedEmployees`; the scope resolver's auto-discovery and inactive targets; the
      dead `CreateAppraisalInstancesAsync`.
- [x] **Slice E-d1 — Withdrawn.** *(2026-09-30 — the E-d source check and E-d1 State blocks below; § 1j
      D-51–D-54: E-d split in two.)* The withdraw action (a reason, the actor, from Draft, Active or
      Governance before it is final; a calibration seat released); both exits withdraw the leaver's
      unfinished appraisals (D-54); every reader leaves a withdrawn appraisal out, and every write on it is
      refused.
- [x] **Slice E-d2a — the cycle's own rules.** *(2026-09-30 — the E-d2 source check and E-d2a State blocks
      below; § 1k D-55–D-58: E-d2 split in two.)* The close refused while anything is unfinished or an
      appeal window is open, Completed → Closed through the lifecycle (D-56); a cycle deleted only while
      nothing but its set-up points at it, and the set-up with it (D-57); reminders for an Open cycle only;
      the dashboard's default cycle (D-58); the cycle actions' 422s. Harness: `run-sliceD`/`run-sliceE`
      withdraw, then close.
- [x] **Slice E-d2b — the live cycle.** *(2026-09-30 — the E-d2b source check and State blocks below; § 1l
      D-59–D-65.)* Generation and every evaluation write need an Open cycle (D-43),
      generation runs the overlap check; the raw `POST` deleted (D-20, D-44). Harness: every suite opens
      and tears down its cycles — a teardown withdraws what is unfinished and waits out, or lapses, the
      appeal windows (`withdrawAndClose` in `setup.mjs` is the start); `buildFixture()` and `hr-portal`
      slice 5 generate. Its own questions at its source check (D-55): which writes need Open, the raw
      create's ten harness callers, A0's template lock, `buildFixture`'s light/full overlap, the w3 probe.
- [x] **Slice E-e — templates and settings.** *(2026-10-01 — the E-e source check and State blocks below; § 1m
      D-66–D-70.)* The lock: any appraisal on the template, or an Open cycle
      assigned; a structural edit recalls an Approved template to Draft, and edits wait while
      PendingApproval; generation re-checks Approved; a template assigned to a cycle cannot be deleted; the
      assignment PUT pinned; `IsLocked` and its reason on the template DTO, read by the editor (P-7). The
      settings profile refused while any live appraisal uses it, with a clone door (D-46). Harness:
      `run-final-scoring`'s A0 freeze, `run-final-settings` :639, :672.
- [x] **Slice E-f — goals, PIPs, conversations.** *(2026-10-01 — the E-f source check and State blocks below the E-e ones;
      § 1n D-71–D-76; `run-final-lifecycle.mjs` 1016/1016 twice, regression 3549/3558.)* E5: the dead methods, unlock's status, P-22, the risk
      pre-filters (the demo's at-risk list changes), the goal PUT's progress, the delete rule, link ids,
      HR's own unlock. E7: writes to a closed plan and to an approved plan's goals refused (D-46), 422s,
      numbering, meeting status (writers, DTO, a UAT backfill), P-55. E8: the write rule (the line
      manager, the conductor or scheduler, the desk when not the subject — create included), `Type` and the
      actor ids pinned, a held conversation not deleted, the held date.
- [ ] **Slice E-g — definitions and the rest.** E9's eight guards and nine 422s; E10's no-schema items
      (journal date and privacy, a check-in completed once with its held date pinned, a plan deleted only
      as a Draft, a decided recommendation not re-closed, a goal counted once in an interim finalise);
      E11's assertions (positives on UAT); E12's repair action (UAT's Rule 8 five on the user's go).
      D-45's three items go to batch 2.

**E-a State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The raw status routes** — `AppraisalLifecycle.EnsureRawTransition`: `PATCH {id}/status` and the workflow
  `transition` open a Draft appraisal and close a Completed one with a score; any other move is a 422 naming
  the action that makes it (the manager's submission, the return, HR's decision on an appeal, the
  employee's appeal, the last step or HR's advance, the withdrawal). They no longer settle or publish.
- **`calculate-score` gone; `GET {id}/score-preview`** — the settle's arithmetic for one appraisal at any
  status, as the A15 dry run's row (computed, calibrated, settled, grade and rating beside what is
  stored), writing nothing; the desk's read, refused to the appraisee (`IAppraisalScoreService.PreviewAsync`
  — the dry run and the preview share one row builder).
- **The correction** — `UpdatePerformanceAppraisalDto` carries the year and the dates alone; refused once
  Completed, Closed, Appealed or Withdrawn, or when the end is not after the start.
- **The return** — in governance before the sign-off: at HR's review, or waiting for calibration while no
  panel sits (under BeforeCalibration HR has signed by then, so not at all); it resets `IsCalibrated`, the
  session, the calibrated overall and `PreCalibrationScore`, and the HR evaluation's submission, in one
  save, through the lifecycle table.
- **The sign-off** checks Governance as well as the step. **Removal** takes a Draft appraisal, or an Active
  one with nothing submitted.
- **The acknowledgment** keeps a comment (`AcknowledgeAppraisalDto.Comments` →
  `EmployeeAcknowledgmentComments`), which HR's review shows as the *Acknowledgment note*.
- **The manager's draft** is refused unless the appraisal is Draft or Active or its remand is open.
- **The self-evaluation's submission** is stamped in the save that carries its scores; the blanket catch is
  gone (a rule is a 422, anything else a logged 500).
- **HR's own appraisal** — the sign-off, the return, a correction, a removal, the raw routes and the score
  preview refuse its appraisee (403).
- **422s** on the correction, the raw PATCH, the removal and the transition (they answered 500, or 400).
- **The screens** — *Correct dates* without the peer count; *Remove* only while the appraisal is
  removable; *Return to manager* only at a step it can be made from; the acknowledgment note on the
  finalised banner.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **`calculate-score`'s read half stays, as `score-preview`** — the row deleted the route; the three suites
   that settled mid-flow read the preview, and HR can see what a sign-off would store.
2. **A return is also made while the appraisal waits for calibration** (AfterCalibration, before HR signs),
   not only at HR's review; a sitting panel refuses it.
3. **The correction takes the dates, not the peer count** — the count is the approvals' counter (lane D).
4. **The two-actor rule covers the appraisee** — an HR officer who is the appraisee's line manager may
   still sign it off; that is segregation of duties, F3's.
5. **The acknowledgment's comment has no box on the employee's page** — the written response is the
   employee's answer; the API keeps what a caller sends (the demo pack sends one) and HR's page shows it.
6. **The return leaves the stored overall until the next settle** — the settle path is `OverallScore`'s only
   writer; the remand does the same.
7. **A return made while the appraisal waits for calibration opens HR's review record** (its remarks live
   there), so the review later reads *in progress* — the same step.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-lifecycle.mjs` **130/130, then 130/130** (and a third time inside the regression) — the first
  run's one miss was my expectation of the review's step after a return (*in progress*, refinement 7).
- Regression (`run-all.mjs`, fifteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, slice D
  22/22, **slice E 17/26 — the same 9 stale**, gates 32/32, **lane A 187/187** (+5, the harness changes below),
  lane P 338/338, lane B1 256/256 (e8 reads the preview), lane L-a 157/157, lanes L-b/L-c 296/296 (s3 reads the
  preview), lane B2 363/363, lane C 397/397 (its remands re-evaluate through the gated draft), lane D 186/186,
  **lane E 130/130**. **2631/2640** — lane D's 2496/2505 plus exactly the 135 new; no assertion lost.
- API log: no request answered 500; no error from an appraisal route — the refusals logged as rule warnings. Only
  UAT's missing SMTP, defect #23's payroll-profile FK, the five-minutely HR/Identity reconciliation, and three
  failures of the platform notification service's thirty-second clean-up (a bulk UPDATE of old
  notifications) while slice E's suite was writing — the platform's, on no performance path.
- Frontend: scoped `tsc` over the 2 touched files, the appraisal types and the employee's appraisal page,
  0 errors — the 3 errors planted in a probe file were all reported; ESLint clean. Not browser-walked.
- Demo: no score or step moves — every one of APC2026's 107 phases byte-identical before and after the
  regression. The book's two live writes hold: chapter 31 signs Kwasi Danquah off at HR's review (in
  governance — the new check passes) and chapter 32 acknowledges with the screen's empty note. HR's
  review page changes: *Correct dates* without the peer count; *Remove* gone from a governance appraisal
  (it was hidden from `hr.head` already); *Return to manager* enabled only at a step it can be made from
  (Kwasi's, at HR's review, still is); an acknowledgment note on the finalised banner — no demo appraisal
  carries one today (the demo pack's 061 sent one and it was dropped; a rebuild keeps it). The demo pack's
  `061` opens drafts through the raw transition (Draft → Active — still allowed); its Governance → Active
  fallback is gone.

**Harness changes in the slice:** the three `calculate-score` suites read the preview — `run-final-scoring`
+5 → 187 (the correction moved before the sign-off, with the narrative and the peer count beside the
score; a correction once Completed refused; the A0 stored score untouched by the preview),
`run-final-gates` and `run-final-goalkpis` unchanged in count; the demo pack's `061` loses its Governance →
Active fallback (it called the raw transition).

**E-b State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The commit, once per appraisal** — `CommitSkipReason` (was `CalibrationSkipReason`) puts two reasons
  first: an appraisal this session calibrated (`IsCalibrated` and linked to it) is skipped — *"Already
  calibrated by this session: what has happened to it since stands"* — and, once the session has closed, an
  appraisal whose manager submitted after its `CompletedDate` is skipped: the panel never saw that
  evaluation. Run again, a commit re-applied every adjustment to a final appraisal it had adjusted — after an
  upheld appeal it wrote the criterion back, restored the calibrated overall, re-settled and published, and
  appended its rationale again — and an appraisal HR had returned, re-evaluated and back at the step took the
  old panel's decisions.
- **The link means one thing** — opening links only appraisals waiting for calibration (not calibrated, not
  linked), and HR's advance past calibration drops the link (the bypass arm in `AppraisalWorkflowService`),
  so "calibrated and linked to S" is S's commit and nothing else.
- **Cancel** — `POST {id}/cancel` with a reason (`CancelCalibrationSessionDto`, required, up to 1,000), from
  Pending or InProgress: `Cancelled`, the reason at the head of the meeting notes, the appraisals it holds
  released. A cancelled session takes no adjustment, edit, completion or commit.
- **Delete** releases the appraisals it holds — it left them linked to a deleted session, and the next
  opening passed them over as linked already.
- **Start folded into Open** — opening stamps `StartedDate`; `POST {id}/start`, `StartSessionAsync` and the
  screen's *Mark convened* are gone.
- **The facilitator** — the creator (from the token) at create, the opener at open; `FacilitatedById` is off
  both session DTOs.
- **The scope** — cycle, unit and level change only while the session is Pending (422 once open); a completed
  or cancelled session is not edited at all.
- **Adjustments** — the edit takes the score and rationale; a body naming another appraisal or criterion is
  refused (422). Recording, changing and removing all go through one guard (`EnsureSitting`): only while the
  session sits — the removal had no check.
- **P-41 and the grid** — a calibrated row reads its settled `OverallScore` unless this session is still
  proposing a new overall for it; *Pre-calibration* is the captured number, else the manager's total, else a
  settled score — the commit's order (the grid put a settled score before the manager's total). Each row
  carries `CommitSkipReason`: why a commit would leave it alone, or null.
- **The screen** — *Cancel session* (Pending or in progress, a reason required); *Mark convened* gone;
  *Commit ratings* shown only when a commit would take a row, its dialog counting those; the reason under a
  row's gate cell, for a row waiting for calibration or one the panel moved; a cancelled session read-only,
  with a banner.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **A commit also skips an appraisal whose manager submitted after the panel closed** — the row said "what
   the session already calibrated". A return (E-a) and a remand clear the calibration, so the old session's
   commit would otherwise put its decisions on the new evaluation — the same undo by another door.
2. **The grid says what a commit would do** (`CommitSkipReason`), and the Commit button and its dialog count
   it — they counted "not calibrated and in governance", which kept offering the undo.
3. **Opening links only what waits for calibration, and HR's advance drops the link** — so the first skip
   reads the link exactly.
4. **Cancel takes a reason**, kept at the head of the meeting notes (no column), like HR's other cancels.
5. **The creator facilitates until the session is opened** — the row said "from the token"; opening still
   records the opener.
6. **An adjustment edit that re-targets is refused (422), not ignored** — an adjustment is an audit record,
   and a request to move it is a mistake worth saying.
7. **A Pending session may still be re-scoped** — nothing is linked before it opens.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-lifecycle.mjs` **276/276, then 276/276** (and a third time inside the regression) — E-a's 130
  and E-b's 146; every E-b check would fail on the old code.
- Regression (`run-all.mjs`, fifteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, slice D 22/22,
  **slice E 17/26 — the same 9 stale**, gates 32/32, lane A 187/187 (its A11 session is deleted while open —
  the release path), lane P 338/338 (its grid averages read the live proposals, as before), lane B1 256/256
  (its skipped appraisal still leaves the session), lane L-a 157/157, lanes L-b/L-c 296/296 (a goal-row
  calibration), lane B2 363/363, lane C 397/397 (its calibrated overall restored after a rejected remand),
  lane D 186/186, **lane E 276/276**. **2777/2786** — E-a's 2631/2640 plus exactly the 146 new; no assertion
  lost.
- API log: no request answered 5xx; the calibration refusals logged as rule warnings. Only UAT's missing
  SMTP, defect #23's payroll-profile FK (one per fixture employee), the five-minutely HR/Identity
  reconciliation, and two failures of the platform notification service's thirty-second clean-up (the bulk
  UPDATE of old notifications, as at E-a) — the platform's, on no performance path.
- Frontend: scoped `tsc` over the calibration pages, their service and types, 0 errors — the 4 errors
  planted in a probe file were all reported; ESLint clean. Not browser-walked.
- Demo: no score or step moves — every one of APC2026's 107 phases byte-identical before and after the
  three runs, and both panels read the same before and after (Operations 42 rows, 3 calibrated; Finance &
  Administration 34, 1). What the screens show differently: **Efua Seidu's (TDC/00017) *Calibrated* cell
  reads 88.56, −2.24** — it was blank, the panel having restated one criterion (P-41); Cynthia Sarpong's
  reads 87 as before; neither panel offers *Commit ratings* — a commit would take no row, and each row's gate
  cell names why; *Mark convened* is gone, and *Cancel session* shows only on a Pending or open session (the
  demo has none). The guide's chapter 30 live walk creates, opens and closes a session over Corporate
  Planning & Communications: **its steps 9–10 — the *not committed* banner and the commit — have not been
  possible since the 2026-09-29 rebuild** (the department's nine are at Goal Setting, so a commit takes no
  one; the old screen counted none either); the guide now says so and gives the talking point. The demo
  pack's `061` drops its `/start` call and the facilitator in its create body — `hr.head` creates and opens,
  so the facilitator is who it was.

**Harness changes in the slice:** `buildLifecycleFixture()` gains g4–g7 (LG appraisees) and turns
`hrCanModifyScores` on for profile LG — an upheld appeal with a changed score; nothing else on that profile
reads it. The demo pack's `061` no longer calls `/start` or names a facilitator. `hr-w3-permissions/
run-slice10-performance.mjs` probes `/cancel` for the Write gate — it probed `/start`, which now answers 404
from routing and would pass vacuously (edited, not run).

**E-c source check (2026-09-30, after E-b; line numbers as of `7ce042063`).** The slice's rows, read again in
source before building; the user settled four questions (§ 1i, D-47–D-50). Row by row:
- **D-14 — live, as written.** UAT's APC2026 was back at InProgress (3) — the 2026-09-29 rebuild re-seeded it
  after batch 1 had moved it — beside 450 Draft cycles and 22 Closed ones; none Open. Readers: the open's overlap
  check (ACS :427), the template lock and its message (ATS :902), the coverage preview's *blocks opening* (CCS
  :255), the HR dashboard's default cycle (HRCDQS :94) and the manager's team cycles (PAS :1574); writers, the
  seeder (:317) and the create body. Frontend: the two unions (`goals.ts`'s also carried an `'Archived'` the API
  never had), `CycleSelect`, the cycle list's *Live* tile, three template-editor texts; the guide five times.
  `IsCycleActive` (PAS :4240) read Open only, so APC2026's HR review said `false` — no screen reads it. Book 3
  states no status.
- **Create — live.** The mapper stored the body's status, a name or a number: a cycle created Open had no opened
  date (so it stayed deletable), skipped the overlap check, and the dashboard counted it live. No harness or demo
  create sends one.
- **Update — live.** Only Closed was refused; profile, year, type, period and deadlines all moved on an opened
  cycle. "A date change once appraisals exist" is the **period** — generation copies it into each appraisal (ACS
  :703-705) — while the phase deadlines stay editable: the gates read them live, and *Edit dates* and
  `run-sliceD` move them on an opened cycle.
- **Target routes — live, and worse than written.** An edit copied the cycle id from the body on both route sets
  (MAP :1038) — into any cycle, another tenant's included, so the target vanished from its own. The nested create
  had no duplicate check (the flat one had), no edit checked, nothing checked the cycle's status (nor did the
  exclusion writes), and the nested list answered 500 for an unknown cycle. The cycle page and demo `060` use the
  nested target routes and the flat exclusion routes; the harness the flat target routes.
- **Individual employee (D-44)** — as lane E's check said: no column, it resolved nobody (ACS :1548), and no UAT
  target is one (351: 349 Position, 2 Unit). Once the member went, a number still bound (the string enum
  converter allows integers), so the service refuses an unknown type.
- **`CalculateExcludedEmployees`** — live: always 0.
- **The in-scope resolver — live, and wider than the row.** Besides the tenant-wide auto-discovery and inactive
  targets, it counted leavers and read a level target through the units at that level. It feeds the open notice,
  the reminders and `GET {id}/employees`. Measured on UAT: auto-discovery reached no one, and APC2026's list was
  102 before and after. Three copies of the rule: generation and the open (ACS), the in-scope list (ACS), the
  coverage preview (CCS).
- **`CreateAppraisalInstancesAsync`** — dead: private, no caller.

**Beyond the rows:** (1) **the target dialog could not make a unit target** — the cascading picker offers a unit
only under its level and writes the level into the form (`CascadingPicker.tsx:231, :245`), so every unit target
arrived with two ids and was refused (400); and a position target naming only a unit was accepted and covered
nobody. The demo's two unit targets were made by the API with one id. (2) **"Resolves to" was not live** — the
typed estimate less exclusions that were never loaded — so both APC2026 targets read 0, while the guide (ch. 14
and P-15) called it "the real, live number" (D-47). (3) The cycle and target routes answered 400 to a rule, and
the exclusion edit and removal 500.

**E-c State (2026-09-30): DONE — built, verified on UAT, staged.** One data-only migration (D-14). What exists now:
- **Three statuses** — `AppraisalCycleStatus` is Draft, Open, Closed (3 is not reused). Every reader reads Open;
  the seeder writes Open; `20260930151521_PerformanceClosureRetireCycleInProgress` moves any cycle at 3 to 2
  (guarded; Down leaves it; Estate's eleven scaffolded operations stripped — § 6). Applied to UAT: APC2026 Open.
- **Create** — always a Draft; `CreateAppraisalCycleDto` has no status.
- **Update** — once opened or with appraisals, the settings profile, year and type are fixed; once with
  appraisals, the period; the 422 names the fields. Name, code and phase deadlines still move. The cycle list's
  edit dialog greys the type and profile of an opened cycle and marks the year.
- **One scope rule** — `AppraisalCycleScope` (`LoadActiveTargetsAsync`, `ResolveCycleAsync`, `ResolveAsync` →
  `AppraisalCycleScopeResolution`: each active target's staff, the union, the excluded with their reasons, the
  scope). Read by generation, the open's overlap check (Open siblings only), `GetEmployeesInScopeAsync` (the open
  notice, the reminders, `GET {id}/employees`), the progress page's *Excluded*, each target's live count and the
  coverage preview. Active staff only; a unit and its subtree, walked a tier per query; a level by the employee's
  own level; exclusions most specific first (employee, position, unit and beneath, level), the older exclusion's
  reason on a tie.
- **One door for targets** — `AppraisalCycleTargetService`; the cycle's nested routes delegate, and the cycle
  service's copy is gone. The id the type names is kept and the others cleared (D-49); a missing, unknown or
  other-tenant scope, or an unknown type, is refused; one target per scope, on create and on edit; a target stays
  in its cycle (`UpdateAppraisalCycleTargetDto` has no cycle); through a cycle's route, only that cycle's targets;
  a closed cycle's targets and exclusions refuse every write; 422s for the rules, 404 for an unknown cycle's list.
  `ActiveEmployeeCount` is resolved live (D-47; the entity's computed property is gone).
- **D-44** — `AppraisalTargetType.Employee` is gone, with the breakdown's count and the page's row.
- **The coverage preview** — its rows in name order, the excluded last.
- **Dead code** — `CreateAppraisalInstancesAsync`, the auto-discovery, the in-scope list's resolver, the
  generation copy's private resolver and the preview's copy; the cycle service's unused template and position
  repositories (lane J's list names the latter).
- **The screens** — both unions are Draft/Open/Closed; `CycleSelect` and the *Live* tile read Open; the
  template editor's texts; the target dialog's three types, its edit no longer sending a cycle; no *Individual
  targets* row; the edit dialog's fixed fields; "another open cycle" in the open dialog and service comment.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **The "date" that freezes is the period** — the phase deadlines stay HR's to move (D-50).
2. **The pin also holds on a Draft cycle with appraisals** (D-50) — generation ran on Drafts until E-d.
3. **The coverage preview is on the one rule** (D-48), and **its rows have an order** — the unification changed the
   order of the scope's id list, and with it the preview's rows, which had none (no ORDER BY; pages cut from them
   could repeat or skip rows). Found by the before/after comparison.
4. **Exclusion writes are refused on a closed cycle too** — the row named the targets; an exclusion changes the
   scope the same way.
5. **A target's extra ids are cleared, not refused** (D-49) — the dialog's own hint said so.
6. **The in-scope list also drops leavers and reads a level as generation does** — both beyond the row.
7. **The open's overlap check counts only Open siblings** — InProgress went with D-14; Drafts were already
   advisory.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-lifecycle.mjs` **391/391, then 391/391** — E-a's 130, E-b's 146 and E-c's 115, all green on the first
  run. Apart from the paired positives, each E-c check asserts something the old code did not do — established
  by reading the old code, not by running the old build against the new checks.
- Regression (`run-all.mjs`, fifteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, slice D 22/22
  (its deadline edit on an opened cycle still passes), **slice E 17/26 — the same 9 stale**, gates 32/32, lane A
  187/187, lane P 338/338, lane B1 256/256, lane L-a 157/157, lanes L-b/L-c 296/296, lane B2 363/363 (its default
  profile moved and restored), lane C 397/397, lane D 186/186, **lane E 391/391**. **2892/2901** — E-b's 2777/2786
  plus exactly the 115 new; no assertion lost. Afterwards only APC2026 is Open, the default profile is *Standard
  Annual Appraisal*, and no cycle is at 3.
- API log: no request answered 5xx; the rule refusals logged as warnings. Only UAT's missing SMTP, defect #23's
  payroll-profile FK (179 fixture employees), the five-minutely HR/Identity reconciliation, and three timeouts of
  the platform notification service's clean-up (the bulk UPDATE of old `Notifications`, as at E-a and E-b).
- Frontend: scoped `tsc` over the cycle and template pages, `CycleSelect`, the appraisal and goal types and the
  appraisal service, 0 errors — the 4 errors planted in a probe file were all reported; ESLint clean. Not
  browser-walked.
- The migration: its SQL tested first on a scratch database (no table → skipped; two rows at 3 moved; a second
  run moved none; other statuses untouched); applied by the API's start after a COPY_ONLY backup
  (`ErpSystemDB_UAT_preEc_20260930.bak`, verified); the history row read back in SQL (105 rows, was 104); no cycle
  at 3.
- Demo: APC2026's 107 phases identical before and after — byte for byte after the migration, employee for employee
  after the three runs (the snapshot tool's rows had no order either; `tools/phase-snapshot.mjs` sorts them now);
  its calendar and its in-scope list (102) byte-identical; its coverage preview identical field for field and row
  for row, now in name order (the MD, excluded, still last).
  What the screens show differently: **APC2026 reads *Open*** (the list, the header, the overview; the HR review's
  `isCycleActive` is now true — nothing reads it); **the Targets tab's *Resolves to* reads 96 and 6** (MD's Office
  less the MD, and Internal Audit — they read 0 and 0); **the Progress tab's *Excluded* reads 1** (was 0) and has no
  *Individual targets* row; the target dialog offers three types and can make a unit target; the list's edit dialog
  greys APC2026's type and profile. The guide's chapters 13 and 14 say so.

**Harness changes in the slice:** `buildLifecycleFixture()` gains cycle EC (profile LN, template TLC, a Position
target estimated at 9), positions EC and ECX, and four staff without logins (ec1–ec3, ecx1 — the suite marks ec3
inactive by SQL: never a login's holder, whom the reconciliation sweep would switch off). `run-final-lifecycle.mjs`
gains E-c's 115 checks: it opens EC and closes it in a `finally` (a harness cycle left Open becomes the HR
dashboard's default), and removes its three scratch cycles and the bait template. No other suite or demo scenario
changed: every fixture target is a one-id Position target, `run-sliceD` edits deadlines only, and `060`'s nested
unit target and flat exclusion pass the new rules.

**E-d source check (2026-09-30, after E-c; line numbers as of `62fbbf29c`).** Three surveys — the close, delete,
generation and the raw create; the harness; every read of an appraisal — then the user split the slice (§ 1j,
D-51–D-54). What they found:
- **Withdrawn had no writer.** The member, its three columns and its gate step existed (batch 1, B1); no service
  set it, no route or DTO carried it, and neither exit touched an appraisal. About **45 reads counted a withdrawn
  appraisal**: every figure of the HR dashboard (its unit breakdown counted one as overdue; its average, histogram
  and attention list read its score and flags), the cycle's progress denominators and bottlenecks, the in-scope
  list (so the open notice and every phase reminder), the manager's team summary (as *Pending*) and list, the HR
  review desk (as *Not started*) and its reviewer load, the calibration scope (grid, links, adjustments, matrix),
  the peer queue and pending nominations, the outcome worklist and its approval (approving created PIPs, pay
  reviews and succession enrolments), the manager's diary and interim reviews, the employee trend, succession
  search and fit, the talent-rating suggestion, award triggers and PIP create/prepare. About thirty already left
  it out through the gates' `Withdrawn` step or a status list. **Manager mode let one take nominations** (anything
  short of Completed or Closed).
- **The close reads no appraisal**, the appeal window is enforced only at filing, a cycle delete is a soft delete
  that leaves every child row, generation runs on Draft cycles with no overlap check, no evaluation write checks
  the cycle, and the raw create has no screen but ten harness callers — E-d2's (D-51).
- **The harness is built on Draft cycles**: every closure suite generates on one by design, and the closes in
  `run-sliceD`/`run-sliceE` would be refused — also E-d2's.
- UAT held no withdrawn appraisal (377 Draft, 117 Active, 75 Governance, 7 Appealed, 400 Completed, 10 Closed).

**E-d1 State (2026-09-30): DONE — built, verified on UAT, staged.** No migration (batch 1 made the columns). What
exists now:
- **The action** — `POST api/PerformanceAppraisals/{id}/withdraw { reason }` (`AppraisalWithdrawalService`, HR
  write): 400 without a reason, 403 to the appraisee's own HR officer (the two-actor rule) and to anyone without
  the HR write permission, 404 for an unknown appraisal, 422 when it is already withdrawn, past governance, or
  **final** (D-52: signed off, and committed by the panel when the cycle calibrates — the settle's own
  `IsFinal`). It records the reason (≤ 1,000), the actor from the token (null when none) and the date; frees a
  seat at a panel that has not calibrated it (a committed calibration keeps its link); and **dismisses the
  outcomes proposed on it** — Proposed, or Approved and not yet actioned — with a note.
- **The lifecycle** — Draft, Active and Governance → Withdrawn in the one table; the raw status routes still
  refuse it, naming the action.
- **Both exits (D-54)** — `EmployeeService.TerminateEmployeeAsync` and `ApplySeparationOutcomeAsync` stage the
  leaver's unfinished appraisals as withdrawn in the exit's own save (*"Left the organisation on {date}: …"* —
  the termination's reason and notes, or the separation's number, type and date); a final one stands and the
  exit logs it (D-52).
- **Every read leaves it out** — the list in the source check above, each read changed at its source; the
  progress page's *Targeted* is now the scope, with *Appraisals* (in play) and *Withdrawn* beside it; the
  dashboard's feed records each withdrawal with its reason and actor.
- **History keeps it** — the appraisal's own reads (by id, by employee, the lists), HR's review and the trend
  show it labelled Withdrawn with its reason and actor, and **without its scores, grade or ranks, for every
  reader** (`AppraisalRelease.WithholdScores`) — the row keeps them.
- **Every write on it is refused** — outcomes, conversations, review events (and their evidence), responses (HR's
  route answered 500), HR review, the advance, PIPs, nominations in either mode, a new goal's link, the goal rows'
  rebuild; the sign-off, the return, the forms, a correction and a removal already were.
- **A peer who has left** is refused at the nomination and again at the approval (lane D's § 5 row).
- **The HR dashboard's load is split** — found by this slice's first run (the § 5 row).
- **The screens** — HR's review page: *Withdraw* with a reason dialog (the employee can read the reason), shown
  when the server would take it (`CanWithdraw` on the review read), and a red banner once withdrawn, whose
  outcome panel is read-only; the employee's own appraisal page says it was withdrawn, when and why; the phase
  rail says a withdrawn appraisal went no further (it drew every step done); the cycle's *Participation* card;
  the employee record's appraisals show the reason; the dashboard's feed description.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **The withdrawal dismisses the open outcomes** — without it a withdrawn appraisal's proposals sat on the
   worklist, and an approved-but-failed one could be re-dispatched.
2. **Unheld conversations and open review events are not cancelled** — refused, and off every queue;
   `AppraisalReviewStatus.Cancelled` has no writer, and bringing it to life is E-f's call (§ 5).
3. **Targeted reads the scope** (E-c's § 5 row) — the demo's reads 102 where it read 107.
4. **The appraisee can read the reason** — the dialog tells HR so.
5. **The in-scope list drops withdrawn appraisees**, so the open notice and the reminders skip them.
6. **The dashboard's split** — a pre-existing defect, fixed here because this slice's checks read the dashboard.

**Verified** (Staging API on `ErpSystemDB_UAT`, the final build):
- `run-final-lifecycle.mjs` **542/542, then 542/542** on the final build — E-a's 130, E-b's 146, E-c's 115 and
  E-d1's 151. On the first build the first run was 538/542 (the four dashboard reads, each timed out waiting for
  its memory grant) and the second 542/542 (by then the grant had been adjusted down); the dashboard was split
  and the final build run twice, with a read-only DMV monitor seeing no blocked or grant-waiting request. Apart
  from the paired positives, each E-d1 check asserts something the old code did not do — nothing wrote
  Withdrawn, and each read and write was read in the old source.
- Regression (`run-all.mjs`, fifteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, slice D 22/22,
  **slice E 17/26 — the same 9 stale**, gates 32/32, lane P 338/338, lane B1 256/256, lane L-a 157/157, lanes L-b/L-c
  296/296, lane C 397/397, lane D 186/186, **lane E 542/542**. **Lane A stopped at its first manager form and lane B2
  lost 13 checks to the same read timing out (five times, each a 500 after 30 s)** — the manager's evaluation form
  queuing for a ~523 MB memory grant while the platform's notification clean-up held locks and memory (§ 5's rows;
  cross-module defect #33). Rerun alone, as the harness does for a stall, **lane A 187/187 and lane B2 363/363**.
  **3043/3052** — E-c's 2892/2901 plus exactly the 151 new; no assertion lost. Afterwards only APC2026 is Open, the
  default profile is *Standard Annual Appraisal*, no cycle is at 3, and the thirty withdrawn appraisals are all the
  harness's (six per lifecycle run).
- API log: the only 5xx are those five; the rule refusals logged as warnings. Otherwise UAT's missing SMTP,
  defect #23's payroll-profile FK (222 fixture employees), the five-minutely HR/Identity reconciliation, and the
  platform notification clean-up's failed `UPDATE`s (now #33).
- Frontend: scoped `tsc` over the review, team-appraisal, own-appraisal, cycle and analytics pages, the phase
  rail, the employee record's tabs, the appraisal types and the appraisal service, 0 errors — the 5 errors
  planted in three probe runs were all reported; ESLint clean. Not browser-walked.
- Demo: APC2026 captured before any run (with the E-d1 build) and after them all — its 107 phases, calendar, coverage
  preview, targets, in-scope list (102), HR dashboard and HR review desk (107 rows) byte-identical, the split build
  included. What the screens show differently: the Progress tab's **Targeted reads 102** (it read 107) beside
  **Appraisals 107** and **Withdrawn 0**; HR's review page offers **Withdraw** on appraisals not yet final; the
  analytics page loads in about 2 s cold (it took 13 s, or timed out). The guide's chapters 14, 31 and 37 say so.

**Harness changes in the slice:** `buildLifecycleFixture()` gains cycles WD and WX (LG's profile) and WP (a new
Manager-mode peer profile, WP), positions WD, WP and WX, three staff with logins (wd1–wd3) and four without
(wp1, x1–x3 — the leavers: never a login's holder). `run-final-lifecycle.mjs` gains E-d1's section: it
terminates x1 and x2 through `POST api/hr/Employees/{id}/terminate` as the admin, raises x3's separation and
**sets it at SettlementApproved by SQL** before completing it through the route (the clearance, settlement and
Internal Audit's review are area 9b's, proven by hr-separation's suites), and reads `tools/api-uat.log` for the
exit's log line (the suite needs the API started by `tools/start-api-uat.ps1`). The lifecycle, appeals and
nominations suites now set their exit code, so `run-all` counts their failures.

**E-d2 source check (2026-09-30, after E-d1; line numbers as of `96031e47e`).** What it found in source and on UAT,
before the user split the slice (§ 1k, D-55–D-58):
- **The close read no appraisal.** `CloseCycleAsync` checked that the cycle had been opened and was not already
  closed, nothing more. **26 of UAT's 34 Closed cycles hold unfinished appraisals**; APC2026, the one Open cycle,
  holds 104 unfinished of 107. A close cut every open appeal window (the window is checked only at the filing, and
  only a Completed appraisal may appeal) and left the Completed appraisals Completed — only the raw status routes
  ever moved one to Closed. No Completed appraisal on UAT is without a score.
- **The delete took the cycle row alone**, refusing only an opened cycle. Generation and goal setting run on Draft
  cycles, so **UAT's 575 never-opened Drafts (544 with appraisals) hold 954 goals, 255 calibration sessions and 118
  check-ins** — each of which a delete left pointing at a hidden cycle, with the cycle's targets and template links.
- **Reminders** refused a Draft cycle only: a Closed one was reminded, and the analytics page offered it.
- **The dashboard's default cycle** was the most recently updated Open cycle — a harness cycle left Open, or an edit
  to a small one, took the dashboard.
- **The cycle's actions answered a rule with 400** — a bare string, or `{ message }` for reminders.
- **The harness:** `run-sliceD` and `run-sliceE` close their cycle fire-and-forget, over unfinished appraisals; the
  lifecycle suite opens EC and ECO and closes EC in a `finally`; the w3 probes use a dummy id (403 or 404, before
  any rule). The demo pack's `060` generates on APC2026 and swallows the refusal (its comment named the 400).

**E-d2a State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The close (D-56)** — `AppraisalCycleService.CloseCycleAsync` reads the cycle's appraisals. A 422 while any is
  Draft, Active, in governance or under appeal, counting each in the year's order (*"This cycle cannot be closed while
  appraisals are unfinished: 2 not started (Draft) and 1 in progress (Active). Each is completed — or withdrawn, if it
  will not be — before the cycle closes."*); then, through the gates' `CanFileAppeal`, a 422 while any Completed one
  is inside its appeal window, naming the day the last one closes; then every Completed appraisal moves to Closed
  through `AppraisalLifecycle` in the cycle's own save — one with no score as it stands — and the log counts them,
  and the scoreless.
- **The delete (D-57)** — a 422 when the cycle was ever opened (as before), or when anything but its set-up points at
  it: appraisals (withdrawn ones included), employee, unit and company goals, calibration sessions, check-ins,
  journal entries, review events and development plans, each counted in the refusal. Otherwise its targets, their
  exclusions and its template links are soft-deleted with it, and the log counts them.
- **Reminders** — an Open cycle only; a Closed one is told so.
- **The default cycle (D-58)** — `HRCycleDashboardQueryService.GetActiveCycleIdAsync`: the Open cycle with the most
  appraisals in play (not deleted, not withdrawn), then the latest opened, then the latest created.
- **422s** — open, close, generate, reminders and delete answer a rule through `BusinessRuleRejected`
  (`{ message }`); they answered 400.
- **The screens** — the cycle list offers *Delete* only to `HR.Performance.Admin`, and only on a cycle never opened
  (**P-12**: `hr.head` saw it and got a 403); the cycle page's close dialog says what a close needs; the analytics
  page's *Send reminders* is off unless the chosen cycle is Open. The cycle page already offered reminders and the
  close on an Open cycle only, and both pages show the server's message on a refusal.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **Reminders refuse a Closed cycle** — § 1k names reminders in the slice without the rule; a closed cycle has no
   deadlines left to chase.
2. **The close's refusal counts by status, in the year's order**, and the window refusal names the last window's
   end — so HR knows what to finish, or when to come back.
3. **P-12 is fixed here** — lane I's row owned it; the delete's new rule put the button's gate in this slice.
4. **The cycle list's *Delete* still shows on a never-opened Draft that holds work** — the list does not know what
   points at a cycle; the server refuses it, naming the work (§ 5).

**Verified** (Staging API on `ErpSystemDB_UAT`, the build):
- `run-final-lifecycle.mjs` **601/601, then 601/601** — E-a's 130, E-b's 146, E-c's 115, E-d1's 151 and E-d2a's 59,
  all green on the first run. Apart from the paired positives (opening and generating CL, finishing cl1 and cl2, the
  scratch cycle's set-up), each E-d2a check asserts something the old code did not do: it closed CL at once, deleted
  WD and left the scratch cycle's set-up behind, reminded a closed cycle, and defaulted to CL — the suite asserts CL
  was the Open cycle updated last. Established by reading the old code, not by running the old build.
- Regression (`run-all.mjs`, fifteen suites): interim reviews 133/133, attachments 65/65, slice C 52/52, **slice D
  23/23** and **slice E 18/27 — the same 9 stale** (each closes through `withdrawAndClose` now, and asserts it), gates
  32/32, **lane A 187/187**, lane P 338/338, lane B1 256/256, lane L-a 157/157, lanes L-b/L-c 296/296, **lane B2
  363/363**, lane C 397/397, lane D 186/186, **lane E 601/601**. **3104/3113** — E-d1's 3043/3052 plus exactly the 61
  new; no assertion lost; lanes A and B2, which the memory-grant stall stopped at E-d1, passed in the full run.
  Afterwards only APC2026 is Open, the default profile is *Standard Annual Appraisal*, and no cycle is at 3.
- **The pauses are the rate limiter.** `api.mjs` now logs any call over 10 s: 22 in the regression — 21 of 29–57 s
  (logins, employee and user creates, two dashboard reads, an appraisal read), **every one ending 29–31 s past a
  minute**: the API's fixed-window limiter (outside Development, 10 logins a minute per IP and 90 requests a minute to
  each local-account user — 300 to an LDAP one; every harness user is local — queued, not refused). The 22nd, a template's submit-for-approval at 10 s, sat behind the only block a
  read-only monitor (2 s samples) saw in the run: the platform notification dispatcher's claim query holding a range
  lock over a notification insert for ~3 s (added to cross-module defect #33). No memory-grant wait.
- API log: no request answered 5xx; the rule refusals logged as warnings. Otherwise UAT's missing SMTP, defect #23's
  payroll-profile FK (209 fixture employees) and the five-minutely HR/Identity reconciliation.
- Frontend: scoped `tsc` over the cycle list, the cycle page and the analytics page, 0 errors — the probe file's
  planted error was reported; ESLint clean. Not browser-walked.
- Demo: APC2026 captured before any run (on the E-d2a build) and after them all — its 107 phases, calendar, coverage
  preview, targets, in-scope list (102), progress, HR dashboard and HR review desk (107 rows) byte-identical, and
  identical to E-d1's last capture; **the dashboard's default cycle is APC2026** before and after. What the screens
  show differently: the cycle list's *Delete* shows only to the Admin tier (`hr.head` no longer sees it); the close
  dialog's text; the analytics page's *Send reminders* is greyed on a Draft or Closed cycle. The guide's chapters 13,
  14 and 37 say so.

**Harness changes in the slice:** `setup.mjs` gains `withdrawAndClose(cycleId, tokens, reason)` — it withdraws every
appraisal the HR review desk lists for the cycle (as the admin, who has no employee record, so the two-actor rule
refuses none; a final one refuses harmlessly), closes the cycle as HR and returns the close's answer. `run-sliceD`
and `run-sliceE` close through it and assert the close (+1 check each). `buildLifecycleFixture()` gains cycle CL
(profile LN, template TLC), position CL and three staff (cl1 with a login; cl2 and cl3 without).
`run-final-lifecycle.mjs` gains E-d2a's 59 checks: it opens CL, finishes it and closes it — **moving cl1's submission
and cl2's last change back 30 days by SQL** to lapse LN's seven-day appeal windows — and closes it in a `finally` if
the section dies; E-c's overlap refusal is now a 422. The demo pack's `060` comment names the 422. `api.mjs` logs
any call over 10 s to stderr (`⏱ slow request`), so a run's log says where it waited.

**E-d2b source check (2026-09-30, after E-d2a; line numbers as of `b363075ed`).** Three read-only surveys — every write
route in the module, the harness's cycles, and the screens, demo pack and seeders — then every claim that decides the
build read again in source; the user settled seven questions (§ 1l, D-59–D-65). What they found:
- **UAT:** one cycle is not the harness's — APC2026, Open, 107 appraisals. The other 776 are `E2E` cycles: 726 Draft (689
  holding appraisals, 1,232 of them) and 50 Closed (37 holding 111). An Open rule freezes harness rows only.
- **Generation refused only a Closed cycle** (ACS :655), and its comment said so on purpose ("generate and review before
  opening"); the controller's doc and the cycle list's already said *after opening*. The cycle page offered *Generate*
  on a Draft (`canGenerate = !isClosed`, `cycles/[id]/page.tsx:327`); the guide's ch. 14 table said "any non-Closed
  cycle" (:1831). No demo walk generates or writes on a Draft.
- **The overlap check lived only in the open** (ACS :477-510), skipped when the targets reached nobody, and never ran
  again: a target added to an open cycle, or a person moving posts mid-year, got a second appraisal of the same type and
  year (§ 5's E-c row). The coverage preview's *safe to generate* read template gaps and conflicts only (CCS :190).
- **No write in the module read the cycle's status** — only generation, the target and exclusion writes, reminders and the
  cycle's own edits did. Six writes pass the step gate (the self and manager submissions, the peer draft and submission,
  the acknowledgment, the sign-off); the rest check the appraisal's status their own way or not at all. The forms'
  *editable* flags read the appraisal's status alone.
- **The raw create** had no screen and no caller in `src/`; it checked nothing — not the cycle, not the employee — took no
  template, snapshot or evaluations, and answered a duplicate with 500. Its callers: `buildFixture()` (seven suites and
  `probe-lane3`), hr-portal slice 5 and its probe; W3 slice 10 asserted 403 for it, and would **fail** on the 405 the
  deleted route answers (not pass vacuously). The only other writer of an appraisal row is the demo seeder (APC2026 and
  the Rule 8 five, straight into the context).
- **The harness:** only slice E and cycle CL opened before generating; every other closure cycle generated on a Draft
  and stayed one. One pair would collide under the overlap rule — `buildFixture()`'s light and full cycles (both Annual,
  one year, one person). A0 re-weights template T2 through the API after generation, which the template lock refuses
  (409) once T2's cycle is open. The lifecycle suite asserted two states the rule makes unreachable: LG "a Draft with
  appraisals" (:749) and WD "never opened, holding appraisals" (:1176-1180). `withdrawAndClose` cannot finish a
  signed-off appraisal, one under appeal, or a Completed one inside its appeal window — and nearly every closure profile
  has seven-day appeals on. hr-portal slice 5 hangs its fixtures off the tenant's first real post, so generating over
  it would sweep real holders in.
- **The demo pack:** `060` generates on APC2026 (Open — unchanged); its fallback, when APC2026 is missing, creates a Draft
  and would now be refused. `061` and `062` write on APC2026 only.

**Beyond the rows** (each verified in source): (1) **the peer routes write any evaluation the caller is the evaluator
of** — the detail, draft and submit match on id and evaluator only (PES :281, :391; the assignment list alone filters
`EvaluatorRole.Peer`), so the appraisee's self-evaluation or the manager's evaluation can be submitted through
`api/PeerEvaluations/{id}/submit`, past the nomination count, the mid-year conversation gate, the narrative and
recommendations, the hand-off to HR's review and the pre-calibration capture — and where peers may not score KPIs, the
submit deletes that row's KPI scores (D-62); (2) **HR's advance has no two-actor check** (AWS :214) — E-a gave the rule to
the sign-off, the return, the correction, the removal and the raw routes (D-62); (3) creating a calibration session
validated nothing about its cycle, not even the tenant (the Open rule now reads it). Known already, left to their slices:
the conversation delete checks nothing (E-f); nominations in Manager mode on a Governance or Appealed appraisal and an
attachment on a withdrawn one (E-g); a cycle's template links are writable once it is closed (E-e).

**E-d2b State (2026-09-30): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The live-cycle rule (D-43, D-59)** — `AppraisalLiveCycle` (new): an appraisal's work is done while its cycle is Open.
  Each guarded write names its action and the cycle — *"The self-evaluation cannot be saved: its cycle, Annual 2026, has
  not been opened — an appraisal's work is done while its cycle is open."*, or *"… is closed."* — and answers 422. The step
  gates read it first: `AppraisalGateFacts` carries the cycle's status and name from the lifecycle's one projection, and
  `AppraisalGates.EnsureAt` refuses before any step rule, so the six gated writes (both submissions, the peer draft and
  submission, the acknowledgment, the sign-off) answer the cycle first.
- **What it covers** — on the appraisal: the self and manager forms (save and submit, their goal assessments inside), every
  nomination change, the hand-off to HR's review, the return, HR's advance, the acknowledgment and the sign-off, both
  response routes, the appeal (file, pick up, decide, move the re-evaluation deadline, finalise after a remand),
  conversations (book, change, remove, mark held), review events (book, change, submit, complete, record progress, attach,
  remove, remove an attachment, the interim finalise), the date correction, the raw Draft → Active (`PATCH {id}/status` and
  the workflow `transition`), and the appraisal's attachments; the employee's goals — set, edit, remove, submit, approve,
  send back, lock, lock the set, unlock, progress (add, change, remove) and a check-in's goal updates (add, change,
  remove); calibration — set up, move to another cycle, open, adjustments (record, change, remove), commit. **Left alone**
  (D-59): the withdrawal and the removal, closing a Completed appraisal, outcomes, PIPs, proposals, development plans, the
  journal, check-ins themselves, unit and company goals.
- **The forms read not editable** on a cycle that is not open — the self and manager contexts' `isEditable` and the
  workflow's `IsEditableByRole` fold the cycle in; the self-evaluation page's banner names it among the reasons.
- **Generation (D-60)** — on an Open cycle only: a Closed one is refused as before, a Draft with *"Appraisals are generated
  once the cycle is open: open it first — …"*. Then the overlap, over whoever it would create: anyone in the scope of
  another Open cycle of the same type and year, **or holding an unwithdrawn appraisal in one**, refuses the whole
  generation (422), naming each cycle and up to ten people (*"and N more"*); nothing is created. One reading,
  `AppraisalCycleScope.FindOpenOverlapsAsync`, serves the open, generation and the coverage preview — so the open now
  reads held appraisals too.
- **The coverage preview** — `GenerationBlockedBy`: generation's refusals in its own order (the cycle's status; no targets,
  no templates, or targets that reach nobody; no template; ties; the overlap over the people still to be appraised,
  naming the cycles). *Safe to generate* is "no reasons", so it reads false while the cycle is not Open.
- **The raw create is gone (D-20, D-44)** — `POST api/PerformanceAppraisals`, with its service method, numbering helper,
  DTO and mapper; the route answers 405. Generation is the one door to an appraisal row (the demo seeder aside).
- **Beyond the rows (D-62)** — the peer detail, draft and submit take peer evaluations only (any other row is 404); HR's
  advance refuses its own appraisee (403, *"You cannot advance your own appraisal: another HR officer or an administrator
  does."*) before the cycle rule; the deadline sweep runs on an Open cycle only (422; an unknown cycle 404) and leaves an
  HR officer's own appraisal alone, logging it and saying so in its result. Creating a calibration session reads its cycle
  — an unknown one, or another tenant's, is not found.
- **422s** — ten routes that let the rule fall through to a 500 answer it 422: the conversation delete, the review event
  delete and its attachment delete, the calibration create, the check-in's three goal-update routes, the goal's progress
  change and removal, and the deadline sweep.
- **The screens** — the cycle page offers *Generate* on an Open cycle only (it offered it on a Draft); its warning card
  lists the preview's reasons; the competing-cycles badge reads *Blocks opening* on a Draft, *Blocks generation* on an
  Open cycle, or *Advisory*; the generate dialog names the overlap refusal.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **The open reads held appraisals too** — D-60 names generation; one reading serves both, so a cycle cannot open over
   someone another open cycle already appraises (a person who moved posts after its generation).
2. **The preview says why** — D-60 asked that *safe to generate* agree with generation; `GenerationBlockedBy` lists the
   reasons, and the cycle page shows them.
3. **The cycle rule answers first** — before the step, the withdrawal and the appeal rules: on a closed cycle the refusal
   names the cycle, not the step. E-d2a's appeal on closed CL now reads that refusal.
4. **The self and manager forms read not editable; the peer form carries no such flag** and is refused on save (§ 5).
5. **The calibration create validates its cycle** — it validated nothing, not even the tenant (the source check's third
   item beyond the rows).
6. **The sweep's skip is visible** — D-62 asked for a log line; the result's messages carry it too, so HR sees which
   appraisal it left for another officer.
7. **hr-portal slice 5 mints its own unit as well as its posts** — D-65 named a post; a unit of its own keeps its manager
   chain and its org reads off real staff too.

**Verified** (Staging API on `ErpSystemDB_UAT`, the build; no pending migration, history 105):
- `run-final-lifecycle.mjs` **704/706 on its first run**: two of the suite's own checks asserted what E-d2b changes —
  E-c's *ec-scope* expected *safe to generate* on the Draft EC (it now asserts the exact reasons: not opened, and that
  alone), and E-d2a's appeal on closed CL expected the completed-appraisal text (the cycle's refusal answers first).
  Each rewritten one for one; then **706/706, and 706/706 again in the regression**. E-d2b's 107 checks — generation on
  an open cycle (5), the overlap in all its arms (24), the raw create gone (3), the writes on a cycle that is not open
  and the ones left alone (57), the peer routes (6), HR's advance and the sweep (10), the team summary now that WD and WX
  run (2) — plus the run's teardown (1); three of E-d2a's WD checks moved into it (601 + 108 − 3 = 706): WD now opens
  before it generates, so its never-opened-with-appraisals arm runs against LD, planted back to Draft by SQL. Each E-d2b check asserts what the old code did
  not do — it generated on a Draft, ran no overlap at generation, wrote on any cycle, took any evaluation on the peer
  routes and let HR advance their own — established by reading the old code, not by running the old build.
- Regression (`run-all.mjs`, fifteen suites, about six minutes): interim reviews 134/134, attachments 66/66, slice C
  53/53, **slice D 27/27**, **slice E 19/28 — the same 9 stale**, gates 33/33, lane A 188/188, lane P 339/339, lane B1
  257/257, lane L-a 158/158, lanes L-b/L-c 297/297, lane B2 364/364, lane C 398/398, lane D 187/187, **lane E 706/706**.
  **3226/3235** — E-d2a's 3104/3113 plus one teardown check in each of the fourteen other suites, the lifecycle suite's
  105, and **three of slice D's criteria checks that had never run**: they are conditional on the appraisal carrying
  criteria, and the raw-created one never did (the generated one does). No assertion lost.
- hr-portal slice 5 **48/48 on UAT** — its first run there: its cycle appraised only the employee it minted, in the unit
  it minted, and ended Closed.
- After the runs: only APC2026 is Open, no cycle is at 3, the default profile is *Standard Annual Appraisal*, and the
  run's 74 harness cycles are Closed.
- API log: no request answered 5xx (793 responses logged, every one a 4xx refusal). Otherwise UAT's missing SMTP (2,403
  failed sends), defect #23's payroll-profile FK (231 fixture employees), the HR/Identity reconciliation's one demo
  employee with two eligible logins (TDC/00052, three runs), and the platform's notification clean-up failing its bulk
  `UPDATE` 11 times in 1.5–5 s under the run's notification load. Six template submissions took 10–15 s; **each wrote
  some 1,800 notification rows** — its "Approval Required", in-app and by email, to 907 logins, eight demo personas
  among them: § 5's first E-d2b row. The log scan found it; E-d2b's code did not cause it (its rows go back to UAT's
  rebuild on 2026-09-29).
- Frontend: scoped `tsc` over the three changed files, 0 errors — the probe file's planted errors were reported; ESLint
  clean. Not browser-walked.
- Demo: APC2026 captured on the E-d2a build before any run, on this build before its runs, and after them all — its
  107 phases, calendar, progress, in-scope list (102), targets, HR dashboard, default cycle and review desk (107 rows)
  byte-identical across the three; the coverage preview identical row for row (103), its one change the new
  `generationBlockedBy: []` (APC2026 is Open and safe to generate). What the screens show differently: *Generate* on an
  Open cycle only (APC2026's page is unchanged), the warning card's reasons, the competing-cycles badge, the generate
  dialog and the self-evaluation banner. The guide's § 1.4 and chapters 14, 28 and 38 say so.

**Harness changes in the slice:** `setup.mjs` gains D-61's teardown — `tearDown(cycleIds, tokens)` in every suite's
`finally`: it withdraws, as the admin, what HR can through the API; then, by SQL on the run's own rows, withdraws what the
API will not and moves the Completed appraisals' appeal anchors back 60 days; then closes each cycle through the API (a
never-opened scratch cycle is deleted) and asserts the lot — one check per suite. Every builder opens its cycles
(`openCycles`); `buildFixture()` generates over a post only its subject holds (template TI) instead of raw-creating, and
its full cycle is MidYear (D-64); `buildLifecycleFixture()` adds E-d2b's posts, staff and tokens, profile LD (auto-lock on)
and cycles OA, OB, OC and LD. All fifteen `run-*.mjs` run inside `try`/`finally`. `run-final-lifecycle.mjs` gains
E-d2b's section; E-c's *ec-scope* and *ec-update* and E-d2a's *ed-close* and *ed-delete* are rewritten where the rule
changed what they can reach. `run-final-scoring.mjs` proves A0 by SQL (D-63): it asserts the 409 lock, re-weights T2 by
SQL and restores it in a `finally`. W3 slice 10's raw-create row is a gone-check (404 or 405; not run — real staff).
hr-portal: `setup.mjs` gains `mintHarnessPlace` (a unit and posts of its own) and `mintActors` options; `run-slice5.mjs`
generates over its own post; `probe-slice5.mjs` is retired (its fixture raw-created the appraisal). The demo pack's `060`
opens the Draft its fallback creates before generating.

**E-e source check (2026-10-01, after E-d2b; line numbers as of `28bec4085`).** Four read-only surveys — the template and
link backend, the settings profile, the harness / demo pack / seeders, and the screens with the workflow's notification
recipients — then every claim that decides the build read again in source; the user settled five questions (§ 1m,
D-66–D-70). What they found:
- **E4, the template — every row live.** The lock (`AssertTemplateNotInActiveCycleAsync`, ATS :889-907) refused only while
  an Open cycle had the template — not a Draft cycle with appraisals, nor a Closed one — and the template PUT, the
  activation, the clone, the workflow routes and both reorders never asked it. No write read `ApprovalStatus`: an approved
  template stayed approved after any edit, and edits ran while it awaited approval. Generation and the coverage preview
  never read the approval (ACS :663-674); the assignment checked it once. A template on a Draft or Closed cycle could be
  deleted; its sections, items and bands stayed. The link PUT rewrote the cycle and the template unchecked (MAP
  :1444-1450), and a closed cycle's links stayed writable. The DTO carried no lock (P-7: the editor froze on any active
  assignment). **One premise wrong:** a deleted template is *not* used by generation — its link drops out under the
  soft-delete filter, so its people fell to another template or to none, and appraisals already on it lost their form's
  sections.
- **Why the lock matters:** the snapshot freezes weights, KPI targets and bands only; the forms read the live template —
  sections, rows, questions, item text, whether a row is rated or measured (`CriterionTemplateKey` :67-70), and the
  self-evaluation's submit asks for every competency on the *live* template (PAS :1018-1023).
- **E3, the settings profile — live, its test wrong.** `UpdateAsync` read no cycle and no appraisal, and replaced all 48
  fields. "Refused while an Open cycle uses it" misses Draft cycles holding appraisals and Closed ones from before E-d2a; and
  finished appraisals read the profile live — the appeal window and the response switch on Completed ones; score
  visibility, peer anonymity and the nomination mode on any; the probation-extension and succession settings whenever an
  outcome is approved (`ProbationHandlers` :155-159, `SuccessionNominationHandler` :86-96). Only the evaluator weights
  (stamped on each evaluation), the review events and the remand deadline are copied per appraisal. No clone door existed.
- **The harness:** no API write breaks under the lock (A0 already asserts the 409; every template is built in Draft); one
  breaks under the profile guard — `run-final-settings.mjs` :643, B8's mid-cycle change — and :676 would pass vacuously
  (its invalid update aimed at a profile in use). A seeder (`TdcDemoAppraisalCustomQuestionSeeder`) added the demo's
  free-text question through EF to the approved template already on an open cycle. A full run mints 146 logins (35 HR,
  18 Manager), hr-portal five more, and nothing switched them off.
- **The screens:** the editor froze on any active link and stayed editable while pending; nothing warned that a change
  sends an approved template back to Draft; *Remove* showed on every list row; four lock refusals reached the user as
  "An error occurred…" (500); the settings editor was always editable, offered *Delete* on the default, had no clone and
  no in-use sign; the cycle page let an open cycle's link be removed or re-pointed. Chapter 4's walk toggles the demo
  profile's *Require peer reviews* (unsaved).
- **The notifications:** a role-routed step notifies every active holder of the role in the tenant (`NotificationTopicPublisher`
  :228 keeps `IsActive` users only) — so an inactive login gets nothing; the reconciliation sweep re-activates only what it
  suspended itself.

**Beyond the rows** (each verified in source): (1) the section and item PUTs copied the body's parent id (MAP :1208,
:1271; the controller checks only the row's own id) — a row could move into another template or tenant past its lock;
(2) the lock could be dodged by removing an open cycle's link; (3) a level-scoped template matched everyone with no
position or unit match (ACS :874-878, the preview, the resolver); (4) with no definition published, approve and reject
acted from any status; (5) the same template could be assigned twice; (6) the profile's weights were unbounded one by one
(`[Range(0,1)]` has int bounds), the pool name's length unchecked (a 500), enums and the default HR reviewer unchecked;
(7) the my-appraisals list and the appeal page said "can appeal" on a cycle that is not open. Left to their lanes: the
two-actor rule on template approval (D-12, F), the profile's concurrency token (J).

**E-e State (2026-10-01): DONE — built, verified on UAT, staged.** No migration. What exists now:
- **The template lock (D-66)** — `AppraisalTemplateService.GetLockReasonsAsync`: a template is locked while any appraisal
  is scored on it (withdrawn ones too — they stay on the record, on their form) or an active link puts it on an open
  cycle; the reason names both (*"107 appraisals are scored on it, and it is assigned to the open cycle 'Annual Performance
  Cycle 2026'"*). `EnsureStructureEditableAsync` holds every structural write — sections and items (add, edit, remove,
  reorder), bands, the scope on the template PUT, the delete — to it, and refuses all of them, and the rename, while the
  template awaits approval. A structural change to an approved, unlocked template sends it back to Draft through the
  adapter's recall outcome (`ReturnToDraftIfApproved`). Approve and reject act on a template awaiting approval only.
  The template DTO and its summary carry `IsLocked` and `LockReason` on every read (P-7).
- **The links and the delete (D-68)** — `AppraisalCycleTemplateService`: the PUT never re-points the cycle or the template
  (422); a link of a closed cycle, or one whose template its cycle's appraisals are scored on, is neither removed nor
  changed (409); a cycle takes each template once (422); an open cycle takes a new one. The rows carry the cycle's status
  and `TemplateInUseInCycle`. A template on any cycle is not deleted (409), and a delete takes its sections, items and
  bands. Generation refuses a cycle whose templates are not all approved (422, naming them), and the coverage preview
  lists the reason.
- **The profile guard (D-67)** — `AppraisalSettingsService`: once any appraisal sits on a cycle running under a profile,
  an update that changes a rule is refused (409, naming the fields) — the rules compared before and after over every
  scalar field but the editable and non-rule ones, so a field added later is frozen by default; a disabled evaluator's
  weight reads 0 on both sides. `SettingsName`, the three `DeadlineRisk*` bands, `ManagerWorkloadThreshold` and
  `DefaultHRReviewerId` stay editable. `POST …/{id}/clone` copies every rule and setting, not the default flag. Every read
  carries `IsInUse`, `InUseAppraisalCount` and `InUseCycleNames`. The save checks each weight (0–1), the pool name's
  length, the six enums and the default HR reviewer.
- **Beyond the rows (D-70)** — the section and item PUTs refuse a body moving the row (and the mappers no longer copy
  the parent id); a level template matches only its level's employees — generation, the preview, the resolver; "can
  appeal" on the list and the appeal page reads the cycle; the demo's free-text question is built by
  `PerformanceAppraisalDataSeeder` with the template, and `TdcDemoAppraisalCustomQuestionSeeder` is deleted.
- **409s and 404s** — every template lock refusal answers 409 (four answered 500); an activation's failure 400 (500); the
  by-cycle list of an unknown cycle 404 (500); a profile update's unknown id 404 (400).
- **The screens** — the template editor freezes on the server's lock, with its reason, and on a pending template, and
  says that a change sends an approved one back to Draft (the header refreshes after each change); the list reads
  *Locked* / *On a cycle* and offers *Remove* only on a template neither; the settings list badges *In use* and offers
  *Copy*; the editor greys an in-use profile's rules (sending them back as loaded), keeps its name, bands, threshold and
  reviewer live, and offers *Delete* only on a profile neither default nor in use; the cycle page's template rows with
  appraisals on them offer neither *Edit* nor *Remove*, and *Edit* never changes the template.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **A rename is not structure** — D-66's lock keeps the name and description editable; the scope, which decides who is
   scored on the template, is structural. The rename is refused only while the template awaits approval.
2. **An inactive link does not lock** — the lock counts active links to an open cycle (the old one counted any): an
   inactive link puts nobody on the template.
3. **The order is structure** — a reorder is held to the lock and sends an approved template back to Draft: the forms lay
   the template out in its order, live.
4. **Generation refuses any unapproved template on the cycle**, not only those someone would resolve to — the cycle's
   set-up is wrong either way.
5. **Activation through the template PUT validates** — it skipped the weights and bands the active-status route checks.
6. **The profile's comparison is over the entity's fields**, so a rule added later is frozen without touching the guard;
   make-default is not held (it changes which profile new cycles start from).
7. **Withdrawn appraisals count** for both locks — a withdrawn appraisal stays on the record, read on its form and under
   its profile's visibility.

**Verified** (Staging API on `ErpSystemDB_UAT`, the build; no pending migration — 102 active migrations, all in UAT's
history of 105):
- `run-final-lifecycle.mjs` **821/821 on its first run, and 821/821 again (run 4)** — E-a…E-d2b's 706 and E-e's 115:
  ee-approval (18), ee-lock (45, its two halves, the closed cycle's link checks with the second), ee-link (12),
  ee-appeal (5), ee-template (6), ee-level (8), ee-profile (21); the teardown's check now also counts the logins. Apart from the set-up steps, each asserts what the old
  code did not do — it edited a pending or approved template freely and kept it approved, generated on an unapproved
  one, locked on an open cycle only, answered 500 to four refusals, let a link be removed, re-pointed or doubled, let a
  closed cycle's links change, let a section or item move templates, matched a level template to everyone, said "can
  appeal" on a closed cycle, and saved any rule on a profile in use and any weight, pool name, enum or reviewer —
  established by reading the old code, not by running the old build.
- Two runs between them lost checks to SQL Server's memory: E-e's first regression (lifecycle 819/821 — two
  manager-form reads) and lifecycle run 3 (807/821 — seven calibration-grid reads and two manager-form reads): every
  failure a read that timed out at 30 s (11 × 500, the only 5xx), with the machine at 1.2 GB free and the platform's
  notification processor rescanning ~126,000 stuck emails (`RESOURCE_SEMAPHORE`; § 5). With VS Code reloaded (3.2 GB
  free) and the queue dead-lettered, run 4 and the second regression ran clean.
- Regression (`run-all.mjs`, fifteen suites, the second run): interim reviews 134/134, attachments 66/66, slice C 53/53, slice D
  27/27, **slice E 19/28 — the same 9 stale**, gates 33/33, lane A 188/188, lane P 339/339, lane B1 257/257, lane L-a
  158/158, lanes L-b/L-c 297/297, **lane B2 375/375**, lane C 398/398, lane D 187/187, **lane E 821/821**. **3352/3361** —
  E-d2b's 3226/3235 plus exactly the 126 new (115 lifecycle, 11 settings); no assertion lost; 4 min 47 s, no call over
  10 s (the template submissions took 10–15 s at E-d2b, writing ~1,800 notification rows each). Afterwards only APC2026
  is Open, and every login a run minted is inactive.
- hr-portal slice 5 **49/49 on UAT** — its cycle closed, its five logins switched off (the new check).
- API log: the only 5xx were the eleven timed-out reads above, all before the clean-up (the last at 01:26:50);
  otherwise UAT's missing SMTP, defect #23's payroll-profile FK (425 fixture employees), the notification clean-up's
  timeouts, and the HR/Identity reconciliation's TDC/00052.
- Frontend: scoped `tsc` over the nine changed files, 0 errors (the probe's two planted errors reported; it found one —
  `CreateAppraisalSettings` now omits the usage fields); ESLint clean. Not browser-walked.
- Demo: APC2026 captured on this build before the runs and after them all (the second regression and slice 5)
  — its 107 phases, calendar, coverage preview, targets, in-scope list (102), progress, HR dashboard and review desk
  byte-identical (against E-d2b's capture, only the dashboard's "days past deadline" moved, the date having turned). What
  the screens show differently: *Standard Employee Template 2026* reads **locked** — *"107 appraisals are scored on it, and
  it is assigned to the open cycle 'Annual Performance Cycle 2026'"* — so the editor's banner gives that reason (it said
  "assigned to a cycle"), the list reads *Locked* and offers no *Remove*; *Standard Annual Appraisal* reads **in use**
  (107 appraisals) — its rules greyed, *Copy* offered, *Delete* gone; APC2026's template row reads *Appraisals on it* and
  offers neither *Edit* nor *Remove*. Chapter 4's walk moved its toggle to a new, unsaved profile.

**UAT writes beyond the fixtures (each the user's, after a verified COPY_ONLY backup):** the 2,465 harness logins left
active by earlier runs switched off (`logins-oneoff.sql`, D-69 — now 1 HR and 9 Manager holders get a role-routed
notification, not ~900); the nine non-harness logins' 2,950 notifications about harness templates and PIPs soft-deleted,
and 126,816 queued emails dead-lettered (`clean-apply2.sql`; the demo's own notifications untouched).

**Harness changes in the slice:** `setup.mjs` records every login it mints and `tearDown` switches them off by SQL
(`deactivateMintedLogins`), its one check now reading *"… closed or deleted, and its N login(s) switched off"*; hr-portal's
`setup.mjs` the same, with slice 5's own check (49). `run-final-lifecycle.mjs` gains E-e's 115 (cycles EE and EF, template
TE and scratch TX, all its own). `run-final-settings.mjs`: B8's mid-cycle change is asserted refused (409, the field
named), W4 renamed and copied, the change then planted by SQL (D-63's pattern) — +8; the invalid update moved to a scratch
profile nobody uses and its text asserted — +3 (375).

**E-f source check (2026-10-01, after E-e; line numbers as of `2abd9c613`).** Four read-only surveys — the goals backend,
the PIP backend, conversations with the withdrawn residue, and the harness / demo pack / screens — then every claim that
decides the build read again in source, and UAT's own rows counted; the user settled six questions (§ 1n, D-71–D-76).
What they found:
- **E5, goals.** Unlock changed the status only from the old Locked one, and always to Approved (EGS :626) — a goal at 60 %,
  or a completed one, came back untouched; migration batch 1 had stamped Locked on every goal locked at the time (UAT
  has none, its rebuild came after). **"Two lock paths" is a wrong premise**: `EmployeeGoalService.LockGoalAsync` had no
  caller, nor had the old submit / approve / reject (dead, with `ValidateGoalWeightTotalAsync`). The approved-goal edit and
  the re-parenting were done in L-a. **P-22 is by design** — the library link records where the wording came from (the
  screens say so); the real defect was that no link id was checked. **Rule 2 of `GoalRiskEvaluator` was unreachable**: both
  pre-filters admitted only goals marked at risk and running goals due within 14 days under 60 % — rule 1's own test — so
  an approved goal nobody had touched was never on either list, and the manager's tab returned every candidate unfiltered.
  Also live: the goal PUT wrote progress past the entries (MAP :1771 — no entry, no recorder, no status); the owner could
  delete an agreed goal, even a completed one, and the delete left its entries; link ids (company, unit and parent goal,
  library, KPI, the submitted-to manager, an entry's review event) were saved as sent — another tenant's shown by name, a
  missing one a foreign-key 500; HR could unlock its own goal (`CanManageGoalAsync` tested the desk first); an entry edit on
  a goal sent back carried its status onto the goal — a goal the owner had changed read agreed again with no manager; and
  deleting the latest entry did not carry back.
- **E7, PIPs** (all 20 on UAT Active — 1 demo, 19 harness). A closed plan took every write: new meetings, goal edits,
  progress, replies, attachments. The Active-plan terms edit was done (P5), but goal add, edit and delete still changed an
  approved plan's terms (D-46). The handler numbered `PIP-APR-{date}-{hex}`, outside the tenant's sequence, and skipped the
  service's rules (one live plan; a supervisor fallback that could name the employee); the service's claimed race
  protection is not real — the index is neither unique nor per tenant. **P-57: nothing wrote a meeting's status** — no DTO
  carried it, no cancel route existed, and every reader took "held" to mean "the date has passed" (on UAT one demo meeting is
  past). **P-55:** a new goal's status was forced to Not started and its percent and notes had nowhere to go. Beyond the
  rows: with no published definition, approve and reject acted from any status — reject sent a plan in force back to Draft,
  where it could be deleted; the plan's subject (an HR officer) could record their own outcome; the outcome number was cast
  unchecked; the PIP goal PUT bound a model with no limits (150 % stored, 1000 or a long title a 500); seven goal and meeting
  routes answered 500 to a refusal.
- **E8, conversations.** The appraisee could book, edit, hold and delete their own — through the API and from the screens;
  dropping one clause was not enough, since create used a second helper and whoever booked became the scheduler. Deleting a
  held conversation moved the appraisal back (the gates read held ones) and could undo HR's audited advance. `Type`, the
  scheduler, the conductor and the review event were copied from the body; the held date was always today. Withdrawn
  residue: a withdrawn appraisal's conversations and review events could still be deleted; some lists showed its unheld
  conversations as Overdue; `AppraisalReviewStatus.Cancelled` had no writer and no reader.
- **The harness and demo:** no suite had the appraisee write a conversation; four checks pinned old behaviour (settings'
  edit-with-no-type 400, and three that survive the build as written); 061 sent `conductedById` and every held conversation
  read "held on build day"; 060's PIP goals were sent with 30 % (dropped) and its past meeting would stay Scheduled.
- **The screens:** the appraisee was offered Save and Mark held; Edit, Delete and Unlock showed on goals the server would
  refuse; the meeting page offered Record on a closed plan and had no Cancel; the Reviews tab read "Absent" for a meeting not
  yet held; the team at-risk tile would disagree with its overview column once rule 2 was reachable.

**Not folded (§ 5):** a final review held before its step (the transition report flags it; nothing refuses it); the PIP
number race (batch 2's unique per-tenant index); a review event's own `ConversationId` cross-link.

**E-f State (2026-10-01): DONE — built, verified on UAT, staged.** No migration (the meeting status column is batch 1's).
What exists now:
- **The at-risk rule (D-71)** — `GoalSetRules.RiskWatched` (Approved, In progress, On track, At risk and the old Locked):
  the evaluator watches only those, both lists' pre-filters admit exactly those, and the manager's At risk tab lists what
  the evaluator flags, sorted by severity. The overview's and the progress tab's at-risk counts, and the HR goals tile
  (`GetGoalSummaryAsync`), count by the evaluator. On the demo the org-wide list goes from 1 goal to 19 (17 approved and
  untouched behind the straight line, 1 in progress behind it, 1 marked at risk).
- **Goals (D-72, D-76)** — `EmployeeGoalService`: a goal is deleted only before it is agreed (draft, pending, sent back)
  and not locked, its progress entries with it; the PUT writes no progress (`UpdateEmployeeGoalDto` has none); an entry is
  corrected or removed only on an agreed goal, and correcting or removing the latest carries the goal back
  (`CarryBackToGoal` — while the goal still shows that entry's percent); unlock refuses the goal's own employee (403, the
  desk included) and restores the Locked or Approved status from the progress (`GoalSetRules.RunningStatusFromProgress` —
  Completed at 100 %, In progress with progress or entries, else Approved). `EnsureLinksBelongAsync` checks the company and
  unit goal (the cycle's), the parent (the same employee's, in the cycle, never itself or a goal under it), the library item
  and the KPI (the tenant's) — on an edit only a link that changes; an entry's review event is its goal's appraisal's. The
  create no longer takes a submitted-to manager. The dead submit / approve / reject / lock and the weight check are gone.
- **PIPs (D-73, D-75, D-76)** — `PerformanceImprovementPlanService`: a closed plan (Completed, Unsuccessful, Cancelled) takes
  no meeting, goal, progress, reply or document; a plan's goals change only while it is a draft (out for approval: recall
  first; in force: frozen); progress is recorded on a draft (its start) and while in force. A meeting is booked Scheduled;
  `RecordReviewMeetingAsync` (*Record meeting*) makes it Held — a plan in force, not before its date; `CancelReviewMeetingAsync`
  (`POST api/PipMeeting/{id}/cancel?pipId=`) makes a booked one Cancelled; a cancelled meeting takes no edit or reply, a held
  one is not cancelled or deleted. Every read takes the stored status (the DTO, the meeting form and schedule, the plan's
  detail and its next meeting, the dashboard's held and next). A new goal keeps the status, percent and notes it is sent
  with (P-55); the goal PUT's request carries the entity's limits. Approve and reject act on a plan awaiting approval only;
  the subject — HR included — is refused approve, reject, the outcome and delete (`PipAccess.IsSubjectAsync`, 403); the
  outcome is one of the list. `PipNumbering` numbers every plan `PIP-yyyy-NNNN`, deleted drafts' numbers kept; the
  recommendation handler uses it, raises no second plan for an employee on one (the recommendation stays Approved), never
  names the employee as supervisor, and makes the approving HR officer the plan's owner.
- **Conversations (D-74, D-76)** — `AppraisalConversationsController`: create, edit, hold and delete are the line manager's,
  the conversation's scheduler's or conductor's, or the desk's — never the subject's, tested before the desk; reads are as
  before. The scheduler is the booker and the conductor whoever marks it held, from the token; an edit carries the date,
  agenda, notes and review event only (`UpdateAppraisalConversationDto` lost the type, appraisal, actors and held state); the
  held date can be stated, not in the future; a held conversation is never deleted, nor one of a withdrawn appraisal; a
  review event named is the appraisal's; the type error lists every type; the DTO names the appraisee. The line manager's
  diary carries their reports' conversations. `AppraisalWithdrawalService` removes the unheld conversations and cancels the
  open review events, and every review-event write refuses a Cancelled one; its delete refuses a withdrawn appraisal's.
- **422s and 404s** — the PIP goal routes, the plan controller's meeting update and delete, and the meeting controller's
  create, update, record, comment and the new cancel answer 422 to a rule and 404 with a body to an unknown id (500s
  before); the goal and meeting reads 404 an unknown plan; a goal link refused 422.
- **The screens** — the goals list offers Delete only before agreement and Unlock never on the viewer's own goal; the goal
  page amends entries only on an agreed goal and words the lock as it works; the self-service edit keeps the goal's cycle
  and sends no progress; the at-risk page names the three rules and what they watch; the PIP page shows each review's
  status (attendance once held) and hides Schedule, Record outcome and the status select from the subject; the meeting page
  offers Record only on a booked meeting of a plan in force (disabled before its date), Cancel on a booked one, Save until
  the plan closes, and says why when it is read-only; the conversation page is read-only to the appraisee, shows the type
  fixed and takes the held date; the appraisal page's Schedule is hidden from the appraisee.

**Where the build refines the rows** (each deliberate; say if one should go back):
1. **A held PIP meeting is not deleted** — D-73 named the cancel; the delete was the same hole as the conversations' (D-74).
2. **Closed means documents too** — D-73's "closed plans take no writes" covers the attachment upload and delete (§ 5: a
   closing letter goes on before the outcome); the delete now asks the record first, then removes the file.
3. **Unlock also corrects an Approved goal with progress** (the demo seeder's 55 % / 48 % goals): the status its progress
   gives it, as for the old Locked one.
4. **The carry-back respects check-ins** — a check-in moves a goal's progress without an entry, so an amendment carries back
   only while the goal still shows the amended entry's percent (§ 5).
5. **The line manager's diary** now includes their reports' conversations — with the conductor fixed at hold, one HR booked
   would otherwise be in nobody's diary.
6. **The PIP goal clamp keeps decimals** (62.5 % was stored as 62), and a meeting's PUT keeps a held meeting's date in the past.
7. **The PIP goal PUT keeps its request shape** — `PipGoalRequest` carries the limits (the screens send it), rather than
   binding `UpdatePipGoalDto`, which needs ids no caller sends.

**Verified** (Staging API on `ErpSystemDB_UAT`, the build; no pending migration — 105 in code, all 105 in UAT's history):
- `run-final-lifecycle.mjs` **1016/1016 on its first run, and 1016/1016 again in the regression** — E-a…E-e's 821, three more
  in ed-write (the withdrawn appraisal's unheld conversation gone, its review event Cancelled, the event's delete refused),
  and E-f's 192 on cycle FG: ef-risk (20), ef-goal (49), ef-links (19), ef-pip (73), ef-conv (30), the set-up (2, with the
  teardown's check). Apart from the set-up steps, each asserts what the old code did not do — it listed no untouched
  approved goal and counted the AtRisk status alone, wrote progress through the goal PUT, let an agreed goal be deleted and
  left its entries, let a sent-back goal's entry be corrected, kept a removed entry's percent, unlocked to Approved whatever
  the progress, let HR unlock its own goal, saved any link id, dropped a PIP goal's status and percent, approved a draft and
  re-approved or rejected a plan in force, let an approved plan's goals change, read a past meeting as held, had no cancel,
  took every write on a closed plan, numbered `PIP-APR-…`, raised a second plan, let the subject decide their own plan,
  closed on an undefined outcome, let the appraisee book, edit, hold and delete their own conversation, took the actors from
  the body, held on today only, and deleted a held conversation — established by reading the old code, not by running it.
- Regression (`run-all.mjs`, fifteen suites, 134 s): interim reviews 134/134, attachments 66/66, slice C 53/53, slice D
  27/27, **slice E 19/28 — the same 9 stale**, gates 33/33, lane A 188/188, lane P 339/339, lane B1 257/257, lane L-a
  158/158, lanes L-b/L-c 297/297, **lane B2 377/377**, lane C 398/398, lane D 187/187, **lane E 1016/1016**. **3549/3558** —
  E-e's 3352/3361 plus exactly the 197 new (195 lifecycle, 2 settings); no assertion lost; no call over 10 s. Afterwards
  only APC2026 is Open, no harness login is active, and the runs' PIP drafts are deleted (their closed plans stay, as the
  privacy suite's do).
- hr-portal slice 5 **49/49 on UAT**. W3 slice 10 (whose unlock probe the survey flagged) was **not run**: it takes the
  tenant's first two employees as subjects, real staff on UAT; its probe is settled in source — the goal lookup answers 404
  before the caller's employee is read.
- API log: **no 5xx response at all**; 204 `ERR` lines, every one defect #23's payroll-profile FK (one per fixture
  employee), and the HR/Identity reconciliation's TDC/00052 twice.
- The runs' notifications to real users: 260 template approvals and 30 PIP approval requests to the 9 HR and Manager
  holders (§ 5, the first E-e row — PIP submissions now feed it too).
- Frontend: scoped `tsc` over the changed files, 0 errors; ESLint clean. Not browser-walked (§ 5).
- Demo: APC2026 captured on this build before the runs and after them all — its 107 phases, calendar, coverage preview,
  targets, in-scope list (102), progress, HR dashboard and review desk byte-identical to E-e's capture and to each other.
  What the screens show differently: HR's org-wide at-risk list for APC2026 lists **19 goals** (it listed 1), and head.dev's
  team tab more than Turnaround; the demo PIP's reviews show their status — 28 Sep *Held* (after the backfill below; it read
  *Scheduled*) and 30 Oct *Scheduled*, the next meeting; its three goals stay *Not started* with
  no percent (made before P-55; a rebuild sends 30 % In progress); the demo conversations read as before, the appraisee now
  named — their held dates stay the build day until a rebuild sends 061's stated dates.

**UAT write beyond the fixtures (run on the user's request, after a verified COPY_ONLY backup,
`ErpSystemDB_UAT_preEfPipBackfill_20261001.bak`):** the demo PIP's one past review meeting — PIP-2026-0001's, 28 September —
moved Scheduled → Held (`ef-pip-backfill.sql`, § 6's rule; asserted count 1, committed). The plan's next meeting now reads
30 October.

**Harness changes in the slice:** `setup.mjs`'s lifecycle fixture gains post FG, cycle FG (profile LG, a Draft its section
opens) and f1, f2 and fHr with logins; `run-final-lifecycle.mjs` gains E-f's 192 and re-asserts ed-write's withdrawn
conversation and review event (+3); `run-final-settings.mjs`: the edit with no type is accepted and an edit naming another
type leaves it (+2, 377). The demo pack: 061 sends no `conductedById` and states each held date (the scheduled one); 060
sends no `conductedById` and records any past review still Scheduled as held.

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
        evaluations, and auto-submit drafts with scores. *(The nomination arm is done — lane D, D-39,
        through `StageApprovalAsync`, the path beneath the approve. The peer-evaluation arm stands: it
        submits every unsubmitted peer evaluation, empty ones included, on HR's reason.)*
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
      (`goal-risk-settings/page.tsx:114-118`, Admin), every Delete for the HR role (P-4/9/17; P-12, the
      cycle list's, done in lane E-d2a) — gated on the controller's policy with the sidebar's permission helper.
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
- [x] **P-7:** the template editor's freeze mirrors the server (E4). *(Lane E-e, 2026-10-01.)*
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
      - ~~"Withdraw appraisal", with a reason, on the HR review~~ *(E-d1)* and the cycle's appraisal list
        (D-10) — the cycle page has no appraisal list; the HR review desk, the employee record and the
        dashboard's feed reach the review page.
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

Group 4 — types: ~~`AppraisalCycleStatus` union aligned (`types/hr/goals.ts:103` has `'Archived'`,
`types/hr/appraisal.ts:29` does not; `InProgress` removed per D-14)~~ *(done in E-c: both are
Draft/Open/Closed)*; C# enum members missing from TS unions added (`Withdrawn`, section `Kind`).

**Verification:** scoped `tsconfig` type-check in two groups (full `tsc` crashes), eslint on touched
files, and the persona walk in § 7.

### Lane J — Dead code and contracts

- [ ] Dead interfaces with no implementation: `IEvaluatorEvaluationService`, `ICriterionScoreService`,
      `IAppraisalEmployeeResponseService`, `IAppraisalAttachmentService`, `IPipReviewMeetingService`
      (`IAppraisalServices.cs:207-267`).
- [ ] Dead members: the four `GetAttachmentAsync` (:97, :723, :793, :981);
      `IEffectiveAppraisalConfigurationService.ResolveForEmployeeAsync` (:1079);
      `ICalibrationSessionService.GetScopedAppraisalIdsAsync` (:968) made private; PAS
      `GetOwnedAppealAsync` (:135); ~~`AppraisalCycleService.CreateAppraisalInstancesAsync` (finish plan
      9.26)~~ *(deleted in E-c)*; `GoalRiskApplicationService` (`GoalRiskService`, never registered).
- [x] ~~**`ResolveEmployeesFromTargetsAsync` gains the `Employee` case**~~ — *settled otherwise by D-44 in E-c,
      2026-09-30: the member went, with its count and its dialog option; the resolver is now
      `AppraisalCycleScope`.* (finish plan 9.26 second half):
      `AppraisalTargetType.Employee = 4` exists (`HREnums.cs:1477`), but only Position,
      OrganizationUnit and OrganizationLevel resolve. D-20 and S8 need it, so harness fixtures can
      generate one appraisal.
      ⚠ *Found building lane A (2026-09-29):* it also needs a **column** — `AppraisalCycleTarget` has
      no `EmployeeId` (the service calls it "deprecated in entity redesign", `AppraisalCycleService.cs`
      ~:1543), and neither migration batch adds one. Either batch 2 adds it (before lane F), or the
      enum member goes and fixtures keep one Position per case, as `buildClosureFixture()` does.
- [ ] Unused injected fields: PAS `_kpiEvaluationSnapshotRepository` (so `KpiSnapshots` at :3574 is
      always empty — decide with C6), `PeerEvaluationService` `_gradeRepository` /
      `_appraisalCompetencyRepository`, ~~`AppraisalCycleService` `_positionRepository`~~ *(removed in E-c)*,
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
- [x] D-11's columns and B3's `EvaluatorEvaluation.IsAuthoritative` leave the model with their
      DTO members and mapper lines. *(Done with batch 1, 8ff0f448f; re-checked in lane B1.)*

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
      - ~~Drop `IsManagerAuthoritative` (:162) and `RequireDevelopmentPlanUpdate` (:189) — in the same
        slice as B3.~~ *Done with batch 1.*
      - Set `IsDefault`; ~~set the cycle status per D-14 (:314)~~ *(done in E-c: the seeder writes Open)*.
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
      COUNTS, RUNBOOK): one after lane B (the gates) and one at the end. *(The first ran on 2026-09-29,
      after B1: DEMO DATASET COMPLETE, and the five demo tracks landed where the books need them. Every
      peer-evaluation save in `061` answered 500 after a 30-second SQL timeout — the machine had 0.8 GB
      of RAM free — though each save had committed; see L-a's State block. Book 0's rule stands: check
      free memory before a rebuild.)*
- [ ] S11 **Runbook Book 3.**
      - Claims in `runbook-claims.json`: [167] manager authority, removed; [171] locking, now a
        goal-set lock; ~~[172] Q3 one-to-one notes, completion not repeatable~~ *done 2026-09-29 with
        Book 3's check-in step (:74): the held check-in, then schedule the next one. When lane E
        makes completion unrepeatable, `060`'s repair of a held check-in with no notes becomes a
        refusal it already swallows*; [178] recommendations,
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
| ~~Nothing told in Manager nomination mode~~ | PNS :464-489 | D/G — done in lane D, 2026-09-30 (the peers are told at approval) |
| ~~PIP handler numbering `PIP-APR-…` vs `PIP-yyyy-NNNN`~~ | `PipRecommendationHandler.cs:103` | E7 — done in lane E-f, 2026-10-01 (`PipNumbering`, shared with the service; the race is batch 2's) |
| ~~`AppraisalCycleTarget.EstimatedEmployeeCount` client-supplied; `ActiveEmployeeCount` can go negative~~ | MAP :1044; entity :647 | E2 — done in lane E-c, 2026-09-30 (the count is resolved live, D-47; the estimate stays HR's planning figure) |
| `PerformanceAppraisal.DevelopmentPlanId/RankInPosition/RankInUnit/NextAppraisalDate` never computed | entity | J (N5 computes `DevelopmentPlanId`) |
| ~~`EmployeeAcknowledgmentComments` written only by manual advance~~ | AWS :605 | E1 — done in lane E-a, 2026-09-30 (the acknowledgment keeps its comment) |
| ~~`PeerNomination.DueDate` ignored~~ | PNS :525 | D5 — done 2026-09-30 |
| Settings profile validation (Min≤Max, bands ordered) | `AppraisalSettingsService` | B6 |
| ~~Calibration `FacilitatedById` from body; session `UpdateEntity` ignores Status~~ | MAP :2481 | E6 — done in lane E-b, 2026-09-30 (the token's; the status was the lifecycle's already) |
| Pre-remand snapshot copies no `ActualValue`; KPI snapshots never written | PAS :2085-2093 | C5 |
| ~~`AppraisalScoring.OverallScore` 0 vs null~~ | `AppraisalScoring.cs:143` | A1 — done 2026-09-29 |
| ~~Analytics includes pre-final scores~~ | `PerformanceAnalyticsService.cs:61` | A9 — done 2026-09-29 |
| ~~Talent sync no recency check~~ | `TalentRatingSyncService.cs:51-82` | A8 — done 2026-09-29 |
| Manager resolution inlined ~57 times, snapshot vs live, no head-of-unit fallback (an employee with no manager cannot submit goals, `GoalWorkflowCommandService.cs:148`) | module-wide | M2 (evaluator record) + D-28 (no HR fallback — decision 6) |
| ~~`GetEmployeesInScopeAsync` tenant-wide auto-discovery~~ | `AppraisalCycleService.cs:1467-1514` | E2 — done in lane E-c, 2026-09-30 (one scope rule, `AppraisalCycleScope`) |
| Interim finalise writes goal `ProgressPercent` | `AppraisalReviewEventService.cs:263-285` | E10 |
| ~~`AppraisalCycleStatus.InProgress` never assigned; `IsCycleActive` checks only Open (PAS :4157)~~ | 6 readers | D-14 / E2 — done in lane E-c, 2026-09-30 (the member, its readers, the seeder and a data migration) |
| Salary figures readable by anyone with Performance Read | proposals controllers | F3 (policy) |
| Recall with no definition has no initiator check | salary/employment services | F9 |
| ~~Frontend: `AppraisalCycleStatus` 'Archived' in one TS file only~~ | `types/hr/goals.ts:103` | I — done in lane E-c, 2026-09-30 (both unions are Draft/Open/Closed) |
| Frontend: `PerformanceAttachmentsPanel` "visible to anyone who can see this record" while the appraisee has no evidence tab | `:220` | I |
| Frontend: salary "Raise the salary change" and "Mark applied" independent | `proposals/salary-review/[id]/page.tsx:178-190` | F5 |
| *Added by the review:* | | |
| ~~P-7 editor freezes on any assignment; the server refuses only Open/InProgress~~ | `templates/[id]/page.tsx:134-138` | E4 + I — done in lane E-e, 2026-10-01 (the editor reads the server's `isLocked` and reason; the assignment rows carry the cycle status) |
| P-8 activation demands bands on a weight-0 free-text item; submit-for-approval too | `AppraisalTemplateService.cs:922-931, :323` | L4 |
| P-11 goal-risk thresholds tenant-wide | `GoalRiskSetting.cs:19-56` | v2 |
| P-13 generation repairs nothing; ~~snapshot failures swallowed~~ | `AppraisalCycleService.cs:623-631, :1366-1377` | E12 — the swallowing copy, the dead `CreateAppraisalInstancesAsync`, was deleted in lane E-c; the repair action remains |
| ~~P-22 goal-library link fixed at creation~~ | DTO :4258 | E5 — closed as by design in lane E-f, 2026-10-01 (it records where the wording came from; the link is now checked at create, D-76) |
| P-24 at-risk filters by the employee's unit and level | `AtRiskGoalsQueryService.cs:155-159` | kept — disclosed on screen (:176) |
| P-25 no export on the at-risk list | `AtRiskGoalsController.cs:65` | D-23 (catalogue programme) |
| ~~P-41 calibration grid reads the adjustment record~~ | CSS :875-876, :896 | E6 — done in lane E-b, 2026-09-30 |
| P-43 Finalise not disabled by the calibration gate | `hr-review/[id]/page.tsx:282-285` | I |
| P-44 no un-finalise; the API can reopen by accident today | PAS :4472-4497 | D-17 / E1 |
| P-49 HR's appeal review sets every weight to 0; criterion names probably blank | PAS :3319, :3237-3239 | C9 |
| P-50 appealed items unchecked, not even that they belong to the appraisal | PAS :2910-2921 | C8 |
| P-51 Result column links only salary/employment; the other pages now exist | `types/hr/outcomes.ts:99-102` | I |
| P-54 no effective date on proposals | entities :1287-1345 | D-19 / F5 |
| Finish plan 9.3 raw create takes no snapshot | PAS :269-297 | D-20 / E1 |
| ~~Finish plan 9.26 `Employee` target case missing~~ | `AppraisalCycleService` resolver | J — settled by D-44 in lane E-c, 2026-09-30: the member went instead |
| ~~Manager and peer forms show the live target; frontend preview formula differs~~ | PAS :4840, PES :584; `appraisal-run.ts:965-972` | A12 — done 2026-09-29 |
| ~~Scores accepted above an item's top band; appeal `NewScore` unchecked~~ | `AppraisalTemplateService.cs:797-817`; DTO :2956 | A11 — done 2026-09-29 |
| Goals set before generation invisible to the appraisal | EGS :221-229 | L2 |
| The only goal-weight guard is never called | EGS :278-279, :306-307, :643-659 | L5 |
| HR's advance approves goals but never locks them | AWS :404-421 | H3 / L2 |
| Assessments read with `ToDictionary`; no unique index | EGS :138 | L1 |
| ~~`IsAuthoritative` column loses its writer with B3~~ | `AppraisalCycleService.cs:734`; PAS ~:2391 | B3 / J — done with batch 1 |
| APR numbering: ledger D-28 says harmless, J said collides | PAS :563-569 | J |
| Rank in unit shown on the employee record | `HR-EMPLOYEES-SYSTEM-GUIDE.md:1214-1218` | J |
| Seeded definitions route by role; conditional routing does not route | cross-module #3; `DatabaseSeedingService.cs:505-545` | F3 |
| Check-in ↔ company objective link unconfirmed | ledger :2178 | D-27 |
| Round 3 walks owed (T3 tab; proposal → salary tab) | round 3 plan :170, :242 | K |
| Harness fixtures and `run-sliceE` would resolve to real UAT staff | `setup.mjs:26-27, :178`; README :77-79 | S8 — *the real-position half done in lane A (the fixture mints its unit and position); the raw create remains, D-20* |
| The medium privacy items P7–P17 | lane P | P |
| *Added by lane A (2026-09-29):* | | |
| `[Range(0, 100)]` on decimal DTO fields has int bounds and rounds first — 100.5 passes | e.g. `CreateCalibrationRatingAdjustmentDto.AdjustedScore` | J (the service rules hold) |
| Two writers of the talent-pool rating cache: the appraisal sync, and the documented "latest confirmed `TalentReviewRating`" rule | `SuccessionPlanningEntities.cs:680-692` | K (succession's owner) |
| `PreCalibrationScore` null when HR's advance auto-submits a manager draft | AWS ManagerEvaluation arm | H3 |
| Slice E's 9 definition assertions stale on UAT (seeded names; no `preventInitiatorApproval`) | `run-sliceE.mjs:18-31, :84-88` | F3 / S |
| *Added by lane C-b (2026-09-30):* | | |
| `AppraisalAppealItem.ScoreAdjusted` and `.ResolutionNotes` never written — "changed on appeal" is read from the kept score | `PerformanceEntities.cs` (appeal item) | J |
| An upheld change's justification is appended to the manager's criterion `Notes`, rewriting the manager's comment; the appeal item's `ResolutionNotes` is the natural home | `ResolveAppealAsync` (the modifications loop) | J |
| `AppraisalAppealItem.OriginalScore` declares `decimal(5,2)`; the model maps `decimal(18,4)` (a model-wide decimal rule) — harmless, the attribute misleads | entity; model snapshot | J |
| The C-b screens (appeal form, status, outcome, HR's review) not browser-walked | 5 pages | K |
| *Added by lane D (2026-09-30):* | | |
| `PeerNomination.ApprovedByManagerId` is never written — no approval path records who approved (the manager, Manager mode, HR's advance; the advance's log row names HR) | `StageApprovalAsync` | J |
| ~~A nominated peer is checked to exist in the tenant, not to be employed — a leaver can be nominated~~ | `EnsurePeersMayBeNominatedAsync` | E — done in lane E-d1, 2026-09-30 (a peer who has left is refused at the nomination and again at the approval; HR's advance is E-d1's row below) |
| `GET PeerNomination/peer/{id}` and `pending/{id}` have no screen (D-41 kept them, approved only) | `PeerNominationController` | I |
| The lane D screens (nomination panel, peer feedback, the peer's list and form) not browser-walked | 4 files | K |
| `hr-portal/run-slice5.mjs` and `hr-w3-permissions/run-slice10-performance.mjs` edited for lane D, not run | dev-harness | S8 |
| *Added by lane E-a (2026-09-30):* | | |
| An HR officer who is the appraisee's line manager may sign it off (the two-actor rule covers the appraisee) | `ApproveAndFinalizeAsync` | F3 |
| HR's review page offers its actions to an HR officer reading their own appraisal; the server refuses them (403) | `hr-review/[id]/page.tsx` | I |
| `probe-lane3-appraisals.mjs` still sends the old header (a probe, not in `run-all`) | dev-harness | S8 |
| The E-a screens (HR's review page) not browser-walked | 1 page | K |
| *Added by lane E-b (2026-09-30):* | | |
| A participant can be added, removed or marked on a completed or cancelled session — the screen hides it; the service does not refuse | `AddParticipantAsync`, `RemoveParticipantAsync`, `RecordAttendanceAsync` | E-g |
| Two sessions over one appraisal: a completed but uncommitted session keeps its links, so the next opening passes those appraisals over; a commit does not skip an appraisal another sitting session holds | `OpenSessionAsync`, `CommitSkipReason` | E-g |
| After HR's return, the grid's *Manager proposed* reads the old total until the manager re-submits (the evaluation keeps its `TotalScore`) | `ManagerEvaluationsAsync` | E-g |
| A cancel tells no one — nor was the panel told it had been convened | `CancelSessionAsync` | G |
| A session's particulars edit and its delete have no screen (`update`, `remove` have no caller) | `calibration.service.ts` | I |
| The E-b screen (the calibration session page) not browser-walked | 1 page | K |
| `hr-w3-permissions/run-slice10-performance.mjs` edited for E-b, not run | dev-harness | S8 |
| *Added by lane E-c (2026-09-30):* | | |
| A cycle's code can still change after generation; its appraisals' numbers carry the code they were generated under | `UpdateAsync`, `GenerateAppraisalNumber` | J (numbering) |
| The coverage preview resolves every other cycle of the same type and year for its overlap card — on UAT some 450 harness Drafts, two queries each, per preview | `ComputeScopeOverlapsAsync` | S8 (teardown) / § 7 item 8 |
| `GET api/AppraisalCycleTarget/type/{type}` has no caller, and now resolves a live count for each cycle it lists | `AppraisalCycleTargetController` | J |
| ~~The progress page's *Targeted* is the count of appraisals, not the scope (APC2026: 107 against 102 — the five Rule 8 appraisals)~~ | `GetCycleProgressAsync` | E — done in lane E-d1, 2026-09-30 (*Targeted* reads the scope, 102; *Appraisals* 107 and *Withdrawn* beside it) |
| An exclusion may name no scope at all, and then leaves out nobody; nothing refuses it | `AddExclusionAsync` | E-g |
| ~~Opening a cycle with no targets skips the overlap check, and a target added to an open cycle is never checked~~ | `OpenCycleAsync` | E-d2b — done in lane E-d2b, 2026-09-30 (generation runs the overlap check over whoever it would create — in another open cycle's scope, or holding an unwithdrawn appraisal there, D-60 — and the open reads held appraisals too) |
| The E-c screens (the cycle list's edit dialog, the target dialog, the Coverage and Progress tabs) not browser-walked | 2 pages | K |
| *Added by lane E-d1 (2026-09-30):* | | |
| HR's advance approves a pending nomination whose peer has left — the nomination and the explicit approval refuse one; the advance's approval path does not check | `StageApprovalAsync` (the advance) | E-g |
| Manager mode takes nominations on a Governance or an Appealed appraisal ("until it is completed or closed"); only Withdrawn was added | `NominationsEditable` | E-g |
| A leaver's appraisal waiting only for the acknowledgment, in a cycle without HR review, is not final by the settle's rule (no sign-off), so the exit withdraws it — though its outcome was already released to the employee | `AppraisalScoreService.IsFinal` against `AppraisalRelease.IsReleased` | E-g (one reading of "final") |
| ~~A withdrawn appraisal's unheld conversations and open review events stay as rows — refused, and off every queue — not cancelled; `AppraisalReviewStatus.Cancelled` has no writer~~ | `AppraisalConversationService`, `AppraisalReviewEventService` | E-f — done 2026-10-01 (D-74: the withdrawal removes the unheld conversations and cancels the open events; Cancelled closes an event) |
| An attachment can still be added to a withdrawn appraisal (the add checks nothing; the delete refuses) | `PerformanceAppraisalService.AddAttachmentAsync` | E-g |
| A staff movement's `BasedOnAppraisalId` is taken unchecked — the tenant, the employee, a withdrawn appraisal | `StaffMovement` (mapper :352, :387) | J |
| The employee's own response route answers a rule with 400 and a bare string; HR's answers 422 with `{ message }` | `PerformanceAppraisalsMeController.AddMyResponse` | J |
| The withdrawal tells no one — the appraisee and the manager (lane G's list has it) | `AppraisalWithdrawalService` | G |
| ~~The manager's team-cycle summary lists Open cycles only, so the harness's Draft withdrawal cycles cannot check its counts~~ | `GetTeamAppraisalCyclesAsync` | E-d2b — done in lane E-d2b, 2026-09-30 (WD and WX open before they generate; the lifecycle suite's ed2b-team reads their rows counting the in-play appraisals alone) |
| ~~The HR dashboard loads its collections in one query~~: SQL Server sized its sort at ~2 GB on every fresh compile, asked for the per-query maximum (~716 MB) and used under 1 MB; with workspace memory busy it queued for the grant (`RESOURCE_SEMAPHORE`) past the 30 s timeout — a 500 for a three-appraisal cycle, 13 s cold on APC2026 | `BuildDashboardAsync` | E — done in lane E-d1, 2026-09-30 (split queries; found by E-d1's first lifecycle run) |
| The split dashboard's queries still ask much more memory than they use — the root query's ideal grant is ~274 MB (it pulls every column of three employees per row), the five collection queries' 89–118 MB, for a few KB of rows; first calls 2.3 s on APC2026, 0.25 s on a small cycle | `BuildDashboardAsync` (entity includes) | J / § 7 (project the columns the dashboard reads) |
| The HR review desk's unfiltered list (every appraisal with both its employees' wide rows) timed out once at the API's first start, then answered in about a second | `GetHRReviewListAsync` | J / § 7 (measure its grant as E-d1 did the dashboard's) |
| **The manager's evaluation form's read is one query of 19 joins that asks SQL Server for ~523 MB on a fresh compile and uses 648 KB.** In E-d1's regression it queued for that grant past 30 s five times — lane A stopped at its first manager form, lane B2 lost 13 checks — while the platform's 30-second notification clean-up held locks and memory (cross-module defect #33). The same family as the dashboard's; the calibration session's participant read also queued. The harness memory's "transient stall" is this | `GetManagerEvaluationContextAsync` | § 7 / J — a memory-grant sweep: split or project each heavy read, measuring each as E-d1 did |
| No probe of the new withdraw route in the permissions sweep | `hr-w3-permissions/run-slice10-performance.mjs` | S8 |
| The E-d1 screens (HR's review page — Withdraw and its banner; the cycle's Participation card; the employee record's appraisals; the phase rail) not browser-walked | 5 files | K |
| *Added by lane E-d2a (2026-09-30):* | | |
| UAT's harness cycles closed before D-56 — 26, every one `E2E` — hold 78 unfinished appraisals, and the 551 never-opened harness Drafts that hold appraisals can no longer be deleted through the API (D-57); only a SQL purge clears them. *Since E-d2b* their appraisals take no write at all (a cycle that is not open refuses every one; a withdrawal and a removal still go through), and every harness run tears its own cycles down (D-61), so the pile stops growing | UAT (harness data) | S8 (teardown and purge) |
| The cycle list offers *Delete* on a never-opened Draft that holds work — the list does not know what points at a cycle; the server refuses it, naming the work | `cycles/page.tsx` | I |
| A cycle cannot close before its last appeal window lapses: there is no waiver for an employee who will not appeal | `CloseCycleAsync` | kept (D-56) |
| The E-d2a screens (the cycle list's *Delete*, the close dialog's text, the analytics page's *Send reminders*) not browser-walked | 3 pages | K |
| *Added by lane E-d2b (2026-09-30):* | | |
| **The harness's appraisal-template approvals notify every harness login on UAT, and eight demo personas with them.** Each submission's "Approval Required" goes, in-app and as a queued email, to every holder of the HR or Manager role — 907 logins, 898 of them harness-minted (`@e2e.local`). The suites mint logins every run and never deactivate them (2,460 on UAT, 1,832 minted on 2026-09-30), so each run notifies more than the last: 9,843 in-app rows on 2026-09-29, 112,139 on 2026-09-30 — some 1,800 rows per submission, and the regression's template submissions take 10–15 s. **The nine other logins holding either role** — eight demo personas (`hr.head`, `md.tdc`, `gm.ops`, `head.dev`, `head.estate`, `sales.manager`, `property.manager`, `authorised.signatory`) and the seeded `manager` — each hold 249 unread approvals of harness templates (every one `E2E …` or portal slice 5's) and 17 of harness PIPs (the 18th, PIP-2026-0001, is the demo's own); `hr.head` 498 and 34. UAT's Notifications table holds 279,064 rows; 69,867 emails wait Pending and 56,585 Failed (no SMTP) — all to `@e2e.local`, `@demo.tdc.local` or `@example.com`, except 198 to a seeded `…@default.com` login, a real domain. Found by E-d2b's log scan; it began before E-d2b and not from its code | UAT (harness data), `dev-harness` | S8 — **done in lane E-e, 2026-10-01** (D-69): every suite's teardown switches off the logins its run minted; the user switched off the 2,465 earlier ones (`logins-oneoff.sql`), then cleared the nine logins' 2,950 harness notifications and dead-lettered 126,816 queued emails (`clean-apply2.sql`), each after a verified COPY_ONLY backup. What remains — the tenant's own holders still get each run's approvals — is the first E-e row |
| The peer's evaluation form carries no *editable* flag — on a cycle that is not open its save and submission answer 422 with no read-only form first (the self and manager forms read not editable) | `PeerEvaluationDetailDto`, `peer-reviews/[id]/page.tsx` | I |
| The goal screens' cycle picker (`CycleSelect`) lists every cycle, draft and closed ones included; a goal set or moved on one is refused, naming the cycle | `CycleSelect.tsx` | I (offer open cycles for new goals) |
| hr-portal's other slices still hang their actors off the tenant's first real post (`readReferenceData`); slice 5 alone mints its own unit and posts (`mintHarnessPlace`) | `dev-harness/hr-portal/setup.mjs` | S8 |
| `hr-w3-permissions/run-slice10-performance.mjs` edited for E-d2b (the raw create's row is a gone-check), not run — real staff | dev-harness | S8 |
| `hr-portal/probe-slice5.mjs` retired: its fixture raw-created the appraisal; it stops where that write was | dev-harness | S8 |
| No suite runs the deadline sweep with `AutoLockOnDeadline` off — the lifecycle suite's LD proves it on | `AdvanceOverdueAppraisalsAsync` | B7 / S |
| The E-d2b screens (the cycle page's *Generate* on an open cycle only, its warning card's reasons and competing-cycles badge; the self-evaluation banner) not browser-walked | 2 pages | K |
| *Added by lane E-e (2026-10-01):* | | |
| **The tenant's own HR and Manager holders still receive every harness run's approval notifications** — the demo personas and the seeded `manager` (1 HR, 9 Manager on UAT). D-69 stopped the harness logins piling up (the teardown switches off what a run mints; the 2,465 earlier ones are off), not the real recipients: each regression adds some 25 template approvals to their bells. Cleared on 2026-10-01 (2,950 rows, by the user's `clean-apply2.sql`); it refills with every run — E-f's verification added 260 template approvals and 30 PIP approval requests to the nine | UAT, `dev-harness` | S8 (a teardown that clears the run's notifications for non-harness recipients — the user's call) |
| **The platform's notification processor rescanned the stuck email queue every few seconds** — some 126,000 Pending/Failed emails (UAT has no SMTP): its `TOP … ORDER BY` claim waited 20 s and more for query memory (`RESOURCE_SEMAPHORE`), with the clean-up's table-wide `UPDATE`, and starved heavy reads — 11 timed-out 500s in E-e's regression and lifecycle run 3 (the manager's form, the calibration grid), on a machine with 1.2 GB free. Dead-lettered on 2026-10-01 (126,816 rows); it refills while UAT runs without SMTP | `UnifiedNotificationService` (claim), UAT | cross-module #33; S8 |
| A calibration grid read that fails reads as an empty grid, so a check of what the grid *no longer* lists passes vacuously (`ed-read`: "no longer lists wd1 or wd3" passed while the read 500'd) | `run-final-lifecycle.mjs` `gridOf` | S (assert the read) |
| The template list's *Edit* dialog offers the scope fields on a locked template; the server refuses a scope change (409, with the reason) | `templates/page.tsx` | I |
| The template PUT still takes `IsActive` from the body, so a body that omits it deactivates the template (activation through it now validates) | `UpdateAppraisalTemplateDto` | J |
| The template copy's *"a name is required"* check cannot be reached over HTTP — `[Required]` answers first; moving it to a rule (it was an `ArgumentException`, a 404) changed nothing observable | `AppraisalTemplateService.CloneAsync` | J |
| `ResolveTemplateForEmployeeAsync` picks a tie by priority where generation and the preview report a conflict | `AppraisalCycleTemplateService` | J |
| The profile's validate-weights endpoint allows ±0.01 where a save allows ±0.005; the create DTO defaults `MinPeerEvaluators` to 0 where the entity says 1; the PUT is a full replace with no concurrency token and a racy name check (D-70) | `AppraisalSettingsService`, DTOs | J |
| The demo seeder's free-text question is proven by reading, not by a rebuild from empty | `PerformanceAppraisalDataSeeder` | S10 (the next rebuild) |
| The E-e screens (the template editor's lock and banners, the template list's *Locked* column and *Remove*, the settings editor's in-use lock and *Copy*, the cycle page's pinned rows and fixed template on edit) not browser-walked | 7 files | K |
| *Added by lane E-f (2026-10-01):* | | |
| **A final review can be held before its step** — a FinalReview marked held during goal setting passes the final gate later; the transition report flags it (`AppraisalGates` :419), nothing refuses it (D-76, not folded) | `AppraisalConversationService.CompleteAsync` | B (a step check on hold) |
| **Two PIPs created at the same moment can take the same number** — `PipNumbering` reads the taken numbers and picks the next; the `PipNumber` index is neither unique nor per tenant (D-75) | `ApplicationDbContext.HR` :3644 | batch 2 (a filtered unique index on `TenantId, PipNumber`) |
| A review event's own `ConversationId` is copied from the body unchecked — the conversation side is checked since E-f (D-76), the event side is not | `AppraisalReviewEventService`, MAP | J |
| A goal's progress also moves through check-ins, which record no progress entry; an entry correction or removal carries back only while the goal still shows that entry's percent (a later check-in leaves it alone), and with no entry left a goal falls to 0 % even if a check-in had moved it | `EmployeeGoalService.CarryBackToGoal`, `CheckInService` | J (one progress ledger) |
| Deleting a parent goal leaves its children pointing at a deleted row (the delete takes the goal's entries, not its children's link) | `EmployeeGoalService.DeleteAsync` | J |
| A closed PIP's documents are frozen too (D-73's "no writes") — HR cannot file a closing letter after the outcome; attach it before | `PerformanceImprovementPlanService` attachments | F (say if a closed plan should take documents) |
| The demo check-in note still says *"Turnaround is the one at-risk goal"*; with D-71 head.dev's team tab lists more | `hr-demo-smoke/scenarios/060-performance.mjs` :202 | S |
| The E-f screens (the goals list's Delete / Unlock, the goal page's entry amendments and unlock, the at-risk page, the PIP page's Reviews status and subject-hidden actions, the meeting page's Record / Cancel / closed banner, the conversation page's appraisee view, type and held date, the appraisal page's Schedule) not browser-walked | 11 files | K |

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
      Scheduled. *(E7 built in E-f, 2026-10-01. UAT's 2026-09-29 rebuild came after batch 1, so its
      backfill never saw the demo pack's meetings: the one past demo meeting was moved to Held by
      `ef-pip-backfill.sql` on 2026-10-01 — the same rule, asserted count 1, after a COPY_ONLY backup —
      and 060 records any past review still Scheduled as held from now on.)*
    - The code still keyed by template item reads the now-nullable id through
      `CriterionTemplateKey.TemplateKey()` (29 call sites in PAS, PES and CSS), which throws, naming
      the row, if a goal row ever reaches it. **Lane L3's worklist is `grep TemplateKey(`.**
    - `CriterionScore.NumericScore` stays `int` (A13's round-explicitly option).
    - D-14 stays split as § 1b assigns it: batch 1 migrates the data; E2 removes the enum member,
      its readers and the seeder's `InProgress`. `IsDefault` is not set by the seeder until S1.
      *(Done in E-c, 2026-09-30. The 2026-09-29 rebuild had seeded APC2026 at InProgress again after
      batch 1 ran, so a second, data-only migration — `20260930151521_PerformanceClosureRetireCycleInProgress`,
      the same guarded `UPDATE … SET Status = 2 WHERE Status = 3`, Down leaves it — went with the enum's
      removal and the seeder's fix. Its scaffold carried Estate's eleven pending operations, which were
      stripped (the snapshot keeps them). Applied to UAT after a COPY_ONLY backup,
      `ErpSystemDB_UAT_preEc_20260930.bak`: 105 history rows, APC2026 Open.)*
    - The rating history's table is **`AppraisalScoreChanges`** (plural): the name S6 lists.
    - ⚠ **The scaffold emitted `RenameColumn(RequireDevelopmentPlanUpdate → IsDefault)`**: two `bit`
      columns left and one arrived. Applied, it would have crowned every profile the default. The
      SQL drops the old column and adds the new one.
- **Batch 2 (before lane F):**
  - `SalaryReviewProposal` / `EmploymentActionProposal`: `SubmittedById` + `SubmittedDate` (F3),
    `ActionedById` (Employee FK, null) + `ActionedDate` (F5);
  - an author column on `PerformanceImprovementPlan` (F3);
  - a filtered unique index on `PerformanceImprovementPlans (TenantId, PipNumber)` replacing the global
    non-unique one (D-75, E-f's § 5 row) — existing duplicates resolved first in the same guarded
    script (UAT: check by tenant; the handler's old `PIP-APR-…` numbers are distinct);
  - `EmploymentActionProposal.SourcePipId` (null FK) and `EffectiveDate` (D-19);
  - the `PerformanceSweepRun` and `PerformanceSweepDispatch` tables;
  - the `AppraisalOutcomeRecommendation` filtered unique index (appraisal, type) where not
    Cancelled/Rejected. **Existing duplicates are resolved first, in the same guarded script**, or
    the index creation fails;
  - a KPI tolerance column on `PerformanceAppraisalCriterionConfigs` (D-32), snapshotted at
    generation from the template item's `KpiDefinition.TolerancePercent` and read by
    `KpiAchievementPercent` (B2's tolerance bullet, moved here). Existing rows: backfill from the
    definition or leave null (= no tolerance, today's scoring) — decide at the scaffold; the demo is
    re-baselined once, after it.
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
  TenantAdmin/Admin/"HR User" (P15) — *closed by lane P, below.*
- **FIXED by lane A (2026-09-29, 3 of the LIVE list above):** P-6 (A2 — overlapping bands refused
  at save), P-39 (A3 — a calibrated overall survives sign-off), P-40 (A4 — item adjustments, KPI
  included, reach the score). Finalised appraisals scored before the fix keep their stored numbers
  (D-13); lane A's State block lists the two on UAT that a settle would move.
- **FIXED by lane P (2026-09-29):** P-27 on every path — HR's paged list was never redacted, and the
  check-in's subject no longer reads the notes even as HR (P15). P-34's server half *before release* —
  the appraisee's HR review no longer carries the manager's evaluation and scores, the final score,
  the grade or HR's remarks until the outcome is released (P2); the breakdown after release, per
  `ShowScoreBreakdownToEmployee`, stays B2. P-19 is **kept** by decision D-26 (P18) — the upload
  card has said so since 0ef42c223.
- **FIXED by lane B1 (2026-09-29):** P-3 and P-48 (the appeal window, counted from the
  acknowledgment), P-43 (Finalise reads the gates), P-46 (the acknowledgment waits for the final
  conversation unless the switch lets it go first), P-64, P-66, P-67 (one evaluator for every write,
  the rail, the dashboard and the advance), P-65 on the server (the portal's button is lane I), P-70's
  refusal (its manager-view flag is B2). P-68 went with batch 1. **P-62** is down to one ghost —
  *Managers see peer scores*, B2 — and **P-63** and **P-69** stay B2's.
- **FIXED by lane E-d2a (2026-09-30):** P-12 — the cycle list offers *Delete* only to `HR.Performance.Admin`,
  and only on a cycle that was never opened.
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
