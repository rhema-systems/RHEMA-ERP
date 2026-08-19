# HR Area 17 — Job Architecture, Competency & Establishment: Build Plan

**Started 2026-08-19.** Numbered **17**, and it absorbs **area 18** (manpower / requisition
budget) because the code already merged them: `ManpowerBudgetService` lives inside
`JobAnalysisService.cs` and the budget endpoints hang off `api/JobAnalysis/budgets`. Splitting
them into two areas would split one controller and one service file. Areas 17 and 18 are
therefore closed together by this plan.

Chosen over Awards (14) and HR Assets (16) on FRD evidence: **three Mandatory requirements**
(FR-HR-134, FR-HR-135, FR-HR-136) plus FR-HR-004, against **one** for Awards (FR-HR-113, a
report) and **none** for Assets. It also closes FRD **§A1.1 Organisation & Position
Management** — the first Level-1 HR section, and the last one still open.

---

## 1. How to use this document

Section 3 is measured ground truth, not intention — every claim was checked against the source
or the database on **2026-08-19** and is cited. Section 5 holds decisions that need the user.
Section 8 is the slice plan; section 9 is the log, appended as slices land.

Read 3.2 before touching code. This area is **not a skeleton like 15b and not a live engine
like 11 — it is a dead module**: 149 endpoints, ~2,900 lines of service, and every table empty
except 43 job descriptions that another area's test harness left behind.

---

## 2. Status at a glance

| | |
|---|---|
| Backend surface | **149 endpoints** across 5 controllers |
| | `api/JobAnalysis` — 88 (descriptions + 11 child collections + **budgets**) |
| | `api/hr/job-architecture` — 15 (families / sub-families / levels) |
| | `api/competencies` — 21 (+ skill indicators) |
| | `api/employee-competencies` — 17 |
| | `api/position-competencies` — 8 |
| Services | `JobAnalysisService.cs` 1,824 LOC (JD + manpower budget), `CompetencyServices.cs` 910, `JobArchitectureService.cs` 197 — **all five DI-registered** |
| Frontend | **none** — zero references to any of the five routes anywhere in `frontend/src` |
| Gate | **bare `[Authorize]` on all five controllers** — the whole area is ungated, as succession was |
| Harness | none |
| Data | see 3.2 — **everything is zero** except 43 residue job descriptions |
| FRD backing | **FR-HR-134 (M)**, **FR-HR-135 (M)**, **FR-HR-136 (M)**, FR-HR-004 (D) |

The four requirements, verbatim in intent:

- **FR-HR-134 (M)** — maintain **approved job descriptions against positions**.
- **FR-HR-135 (M)** — process **manpower requisitions** through the approval chain
  **Department Head → HR → Managing Director**.
- **FR-HR-136 (M)** — **verify the position against the approved establishment** before a
  vacancy may be approved.
- **FR-HR-004 (D)** — manpower / establishment planning linked to strategic planning cycles.

---

## 3. Ground truth — measured 2026-08-19

### 3.1 ✅ Everything is wired; nothing is dead by omission

All five services are registered (`HrModuleServiceRegistration.cs:502-512`), repositories
included. This is **not** the Procurement `SuppliersController` shape — no missing DI, no
unreachable controller. What is missing is that nothing has ever *called* them.

### 3.2 ⚠⚠ The module has never executed — measured counts

| Table | Rows |
|---|---|
| `JobDescriptions` | **43** — and see below |
| `JobResponsibilities` | **0** |
| `JobFamilies` / `JobSubFamilies` / `JobLevels` | **0 / 0 / 0** |
| `ManpowerBudgets` / `ManpowerBudgetLines` | **0 / 0** |
| `Competencies` / `CompetencySkillIndicators` | **0 / 0** |
| `PositionCompetencies` / `EmployeeCompetencies` / `EmployeeCompetencyHistories` | **0 / 0 / 0** |
| `EmployeePositions` (live) | 146 |

All **43 job descriptions are area-6 harness residue** — titles `E2E RecB Engineer 475655`,
`E2E RecD Engineer 969777`, `E2E JD Title 537421`, all created 2026-08-12, **all at Status=1
(Draft)**, none deleted, and **zero child rows of any kind**.

Read that as three separate facts:

1. `POST api/JobAnalysis/descriptions` is the **only** write in 149 endpoints that has ever run.
2. The **11 child collections** — responsibilities, qualifications, competencies, physical
   demands, working conditions, equipment/tools, reporting relationships, duty items, PPE,
   medical requirements, and responsibility KPIs — have **never once been written**. That is
   ~44 endpoints with no execution history.
3. **Submit → review → approve has never run**, so no job description has ever reached
   `Approved`. FR-HR-134 asks for *approved* job descriptions against positions; today the
   system holds none, and has no path that has been shown to produce one.

Apply [[hr-dead-path-defects]] throughout: run the happy path before building UI on it.

### 3.3 ⚠⚠ Area 6's budget enforcement has never had a budget to enforce against

`StaffRequisitionService.EnforceBudgetAsync` runs on **submit** and again on **approve**
(`:341-386`), and `BuildBudgetCheckAsync` (`:831-865`) looks up a `ManpowerBudgetLine` matched
on position + fiscal year. **There are zero `ManpowerBudgetLine` rows in the database.** Every
requisition ever raised has taken the `HasBudgetLine = false` branch.

So area 6 shipped the *consumer* of this area's data eleven weeks before the producer. The
enforcement mode setting, the Block/Warn/Off ladder and the `BudgetCheckPanel` on the
requisition screen are all real and all exercising the empty branch. **Slice 7 makes area 6's
rule live for the first time** — and per the area-12 lesson, giving a dead path a live value
turns its neighbours into defects, so expect the requisition tests to move.

### 3.4 ⚠⚠ FR-HR-136 is unimplemented, and it is the third encounter with unmaintained establishment data

Two halves, and only one exists:

- **The machinery exists.** `PositionVacancyService.GetEstablishmentOverviewAsync` (`:76`)
  computes an overview, `VacancyClassification.WithinEstablishment` (`HREnums.cs:5247`)
  classifies each vacancy, and `ReconcileAsync` (`:278-313`) opens a vacancy when a position is
  below establishment and closes it when back at establishment. Area 6 shipped a
  `/hr/recruitment/establishment` screen over it.
- **Nothing refuses.** `GetEstablishmentOverviewAsync` is called from exactly one place — the
  controller read (`PositionVacanciesController.cs:34`). **No approval path consults it.** The
  classification is informational. FR-HR-136 says *verify before a vacancy may be approved*;
  nothing verifies.

And the data is still what area 8 measured: `EmployeePositions.ExpectedHeadcount` is
**1 for 132 of 146 live positions and 4 for the other 14** — i.e. the column default, untouched.

This is the same wall that downgraded area 8's FR-HR-173 establishment rule to **advisory**
mid-slice, and the same shape as [[hr-deferred-modules]] §3. **The difference this time is that
the fix is in scope**: the manpower budget *is* the approved establishment, and this area builds
its maintenance path. That converts "work around missing data a fourth time" into "supply it".
See decision D-2.

### 3.5 ⚠⚠ Three unique indexes that a soft delete cannot release

The area-13 trap, pre-identified before it fires. All three are **unfiltered** — no
`IsDeleted` in the filter, because there is no filter at all:

| Index | Consequence the first time someone presses delete |
|---|---|
| `IX_Competency_Tenant_Code` | a deleted competency's code can never be reused — permanently |
| `IX_EmployeeCompetency_Tenant_Employee_Competency` | delete an assessment and that employee can **never be re-assessed** on that competency |
| `IX_PositionCompetency_Tenant_Position_Competency` | delete a position requirement and it can never be re-added |

`DeleteAsync` is soft everywhere in this codebase. With 0 rows in all three tables, none of
these has ever fired. They are 500s waiting for the first user. Fix in the slice that owns each
table, per [[hr-succession-area-survey]].

### 3.6 ⚠ The document-number generator counts rows

`GenerateJobDescriptionNumberAsync` (`JobAnalysisService.cs:1447-1454`) builds
`JD-{year}-{count+1:D5}` from `CountAsync(...)`. There is **no unique index on
`JobDescriptionNumber`** (only the PK), so unlike area 13 this does not 500 — it **silently
mints duplicates** once any row is deleted or once two creates race. Same counter shape,
quieter failure. `BudgetNumber` uses the same generator pattern (`:1620`).

### 3.7 ⚠ Neither approval chain is on the workflow engine

`JobDescription` and `ManpowerBudget` are **absent from
`WorkflowEntityTypeCatalogService`** and **no `IWorkflowStatusAdapter` implements either**
(both greps empty). Instead both carry bespoke `submit` / `review` / `approve` / `reject`
endpoints writing `Status`, `ApprovedById`, `ApprovalDate` directly.

FR-HR-135 names its chain — **Department Head → HR → Managing Director** — which is precisely
what the engine models. Per [[workflow-engine-integration]] and the standing rule *never build a
bespoke HR approval UI*, both go onto the engine (applications 7 and 8 of the recipe). Note the
engine trap: a single-step definition silently auto-approves.

⚠ And the caller-declared-actor audit applies — **to the budget half only**. The job description
side is already clean: create, review and approve each call `GetCurrentEmployeeId()` and refuse a
caller with no employee link. But `ApproveBudget` takes **`[FromQuery] Guid approvedById`**, so the
approver of a manpower budget is whoever the caller names in the query string — on the very
endpoint that decision D-2 makes write the approved establishment. By the area-13 rule — *an act performed by the
caller at the moment of the call comes from the token; a fact about someone else does not* —
`ApprovedById` / `ReviewedById` / `PreparedById` all come from the token and must be stripped
from their DTOs.

### 3.8 ✅ The job architecture attaches through the job description, not the position

`EmployeePositions` has **no** FK to `JobFamily` / `JobSubFamily` / `JobLevel` — it carries
`OrganizationLevelId`, `StaffLevelId`, `Level`, `ExpectedHeadcount`, `SalaryGradeId`.
`JobDescriptions` carries **`JobFamilyId`, `JobSubFamilyId`, `JobLevelId`**.

That settles a design question without a schema change: the taxonomy classifies the *job
description*, and a position inherits its classification from its current approved JD. With
0 rows in all three taxonomy tables, the 15 architecture endpoints are a floating vocabulary
with no consumer yet.

### 3.9 ✅ The job description is richer than its name suggests — three seams

`JobDescriptions` also carries:

- **Job valuation** — `RoleIntrinsicValue`, `RoleCriticality`, `IndustryBenchmarkSalary`,
  `EstimatedSalaryLow/High`, `SuggestedSalaryGradeId`, `ValuationNotes`, plus a
  `descriptions/{id}/valuation` read. `SuggestedSalaryGradeId` points at the payroll-owned
  grade store — **read-only**, per [[payroll-ownership-boundary]] and
  [[hr-salary-structure-bridge]]. No grade editor here.
- **Authority** — `AutonomyLevel`, `DecisionMakingScope`, `FinancialAuthorityLimit`,
  `ApprovalAuthorityNotes`. Adjacent to the org-authority model deferred in
  [[hr-deferred-modules]] §3; **do not** try to derive disciplinary or approval authority from
  it in this area.
- **`UnionId` and `IsBargainingUnitRole`** — area 21 (Unions, `api/hr/unions`, 10 endpoints,
  currently unbuilt) is FK'd from here. See decision D-4.

### 3.10 ⚠ Money events for the Finance backlog

`ManpowerBudgets` carries `SalaryBudget`, `BenefitsBudget`, `RecruitmentBudget`,
`TrainingBudget`, `TotalBudget`, `ActualSpent`, `Variance`. Per
[[hr-finance-integration-split]]: **no GL posting in this area** — register every one of these
in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` for the single post-HR sweep, and read Finance
`Currency` / `ExchangeRate` if the budget needs a currency at all (today it does not carry one —
see D-5).

### 3.11 Read-contract risks to check in slice 1

The recurring [[hr-ported-list-read-bugs]] shapes, unverified because nothing has ever read
these endpoints with data: missing `.Include` on the 11 child collections and on
`JobFamily`/`JobSubFamily`/`JobLevel`/`Position`/`StaffLevel`/`Union` navigations; stale
navigations on write responses; and `descriptions/{id}/details` vs `descriptions/{id}` divergence.
`api/competencies` has a **second controller class in the same file** (skill indicators, from
line 169) — confirm its route is `api/competency-skill-indicators` and that its `{id}` routes do
not collide with the parent's.

---

## 4. Scope and boundaries

**In scope**

- All 149 endpoints across the five controllers, gated, exercised and audited.
- FR-HR-134 (approved JD per position), FR-HR-135 (requisition chain on the engine),
  FR-HR-136 (establishment verification with teeth), FR-HR-004 (planning link).
- The competency framework end to end: definition → position requirement → employee assessment
  → gap analysis → history.
- The manpower budget end to end, including making area 6's dormant enforcement live.
- Screens for all of it, plus the establishment maintenance screen that supplies
  `ExpectedHeadcount`.

**Out of scope / hand-offs**

- **Payroll grades** — read-only. No grade editor ([[payroll-ownership-boundary]]).
- **GL posting** — registered in the Finance backlog, posted in the post-HR sweep
  ([[hr-finance-integration-split]]).
- **Org-authority model** — still deferred and still data-blocked ([[hr-deferred-modules]] §3).
  The JD's authority fields are recorded, not used to derive who may approve what.
- **Area 21 Unions** — decision D-4.
- **W3 permission sweep** — this area seeds its own families, as every area has.

---

## 5. Decisions — TAKEN 2026-08-19

All six settled with the user before slice 0. The reasoning each option was weighed against is
kept below the ruling.

| # | Ruling |
|---|---|
| **D-1** | **Three permission families** — `HR.JobArchitecture.*`, `HR.Competency.*`, `HR.ManpowerBudget.*` |
| **D-2** | **Budget approval writes `ExpectedHeadcount`** — the approved budget *is* the approved establishment |
| **D-3** | **Reuse area 6's Off/Warn/Block ladder**, defaulting to Block |
| **D-4** | **Fold the union lookup in** — one setup screen, otherwise the FK is dead |
| **D-5** | **Screens + a small starter seed** from the 27 existing `Skills` rows; population raised with TDC |
| **D-6** | **Purge the 43 residue job descriptions** at area close |


**D-1 · Permission families.** Proposal: three, because the audiences differ —
`HR.JobArchitecture.*` (job descriptions + families/levels), `HR.Competency.*` (framework,
position requirements, employee assessment), `HR.ManpowerBudget.*` (budgets, lines,
establishment). Alternative: one `HR.JobAnalysis.*` family for all 149. Per
[[hr-area-authz-pattern]], role gates plus a self-or-HR check where a record names the actor —
an employee should be able to read **their own** competency profile and their position's
job description.

**D-2 · Does an approved manpower budget write the establishment?** ⚠ The load-bearing decision.
FR-HR-136 needs `ExpectedHeadcount` to mean something. Options:
 (a) **Budget approval writes `ExpectedHeadcount`** onto each position in its lines — the budget
     *is* the approved establishment, one maintained artefact, and FR-HR-004's planning link
     falls out of it. Costs a writer onto another area's column.
 (b) **Separate establishment maintenance screen**, budget references it. Cleaner ownership,
     but two things to maintain and they will diverge.
 (c) Keep advisory, ship the screen, decide later — a fourth encounter with the same wall.
Recommendation: **(a)**, with the budget line as the single source and an admin screen for
positions not covered by any budget.

**D-3 · Does FR-HR-136 block, or warn?** Once (D-2) supplies data: does approving a vacancy
outside the establishment get **refused**, or warned with an override + reason? Area 6 already
has a `BudgetEnforcementMode` Off/Warn/Block ladder for requisitions — reusing that shape for
establishment is the consistent answer. Also: does area 8's FR-HR-173 advisory rule get promoted
to blocking at the same time?

**D-4 · Union FK.** `JobDescriptions.UnionId` and `IsBargainingUnitRole` need
`api/hr/unions` (area 21, 10 endpoints, no screens). Fold the union lookup into this area as a
small setup screen, or leave the FK unset until area 21? Recommendation: **fold it in** — it is
one lookup screen and the FK is otherwise dead.

**D-5 · Seed or empty?** `JobFamilies`, `JobLevels` and `Competencies` are all empty. Ship empty
admin screens for TDC to populate, or seed a starter framework? A competency framework nobody
populates is another `ExpectedHeadcount`. Recommendation: ship the screens, seed a **small**
starter set derived from the 27 existing `Skills` rows and the 146 positions, and raise
population as an open question for TDC.

**D-6 · The 43 residue job descriptions.** Area-6 harness rows on the reference DB, all draft,
all childless. Purge them, or leave them as fixture data? They will appear in every register
screenshot.

---

## 6. Findings register

Pre-identified before slice 0; numbered as they are confirmed in the log.

| # | Finding | Where | Section |
|---|---|---|---|
| 1 | Whole area ungated — bare `[Authorize]` on 5 controllers, 149 endpoints | all | 2 |
| 2 | 11 child collections (~44 endpoints) never executed | `JobAnalysisService` | 3.2 |
| 3 | Submit→review→approve never run; no approved JD exists | `JobAnalysisService:407-480` | 3.2 |
| 4 | Area 6 budget enforcement runs against an empty table | `StaffRequisitionService:831` | 3.3 |
| 5 | FR-HR-136 has machinery but no gate — nothing consults the establishment | `PositionVacancyService:76` | 3.4 |
| 6 | `ExpectedHeadcount` unmaintained: 132/146 at 1 | `EmployeePositions` | 3.4 |
| 7 | 3 unique indexes a soft delete cannot release | competency tables | 3.5 |
| 8 | Document number from `CountAsync` — silent duplicates | `JobAnalysisService:1447` | 3.6 |
| 9 | Two bespoke approval chains, neither on the workflow engine | JD + budget | 3.7 |
| 10 | **`POST budgets/{id}/approve` takes `[FromQuery] Guid approvedById`** — the approver is caller-declared, on the endpoint that (D-2) will write the establishment. `CreateEmployeeCompetencyDto.AssessedById` is the same shape. ⚠ Corrected 2026-08-19: the *job description* side is clean — create, review and approve all resolve the actor from the token via `GetCurrentEmployeeId()`. | `JobAnalysisController` budget region | 3.7 |
| 11 | `[HttpPost("budgets/{id:guid}/reject")]` takes `[FromBody] string reason` — a bare string body, which no form can send as JSON without quoting; check it is not dead | `JobAnalysisController` | slice 7 |

---

## 7. Cross-module seams

| Seam | Direction | Rule |
|---|---|---|
| Payroll salary grades | read | `SuggestedSalaryGradeId` read-only; no editor ([[payroll-ownership-boundary]]) |
| Finance GL | deferred | register budget money events; post in the post-HR sweep ([[hr-finance-integration-split]]) |
| Area 6 recruitment | this area supplies | budget lines make `EnforceBudgetAsync` live; JDs feed `Requisition.JobDescription` |
| Area 8 movements | this area supplies | FR-HR-173 advisory rule may be promoted once establishment is maintained |
| Area 13 succession | consumes | competency gaps feed readiness; check `EmployeeCompetency` reads there |
| Area 5 performance | consumes | responsibility KPIs vs `KpiDefinitions` — confirm they are not two parallel stores |
| Area 21 unions | decision D-4 | `JobDescriptions.UnionId` |
| Workflow engine | both chains | applications 7 and 8 of the recipe |

---

## 8. Proposed slices

Each backend slice ships with a harness file in `dev-harness/hr-jobarch/`, run in **Staging**
with the JWT key, two actors ([[hr-harness-run-environment]]).

| Slice | Content |
|---|---|
| **0** | Gate the area — three permission families (D-1), self-or-HR on own JD / own competency profile. Assert the **positive** case, not only refusals. |
| **1** | Read and error contracts across all five controllers: `.Include` sweep, paged registers, `details` vs plain, the skill-indicator route collision, a typed exception so service rules stop being mute. |
| **2** | The job-description spine: create → all 11 child collections → version / clone / supersede. Fixes finding 8. |
| **3** | JD approval onto the workflow engine; strip the caller-declared actors (finding 10); **FR-HR-134** — a position's current *approved* JD, and the `Requisition.JobDescription` seam area 6 left `[NotMapped]`. |
| **4** | Job architecture taxonomy: families / sub-families / levels + JD classification (3.8). |
| **5** | Competency framework: competencies, skill indicators, position requirements, `bulk-set`. Fixes two of the three indexes in finding 7. |
| **6** | Employee competency: assessment, `batch-assess`, gaps, qualified-for-position, history. Third index. Check the succession seam. |
| **7** | Manpower budget spine + lines + critical positions, onto the workflow engine with the **FR-HR-135** chain; makes area 6's enforcement live (finding 4) — re-run the area-6 harness. |
| **8** | **FR-HR-136** — establishment maintenance (D-2) and the verification gate (D-3); revisit area 8's advisory rule. |
| **9** | Analytics: JD due-review, competency gap analysis, budget variance, **FR-HR-004** planning link. |
| **10–12** | Screens, each with a **UI-payload probe** (`run-sliceN-ui.mjs`). Build the create forms early — they are actor audits. |
| **13** | Content audit over all GET endpoints, unconditional and by id. |

---

## 9. Slice log

### Slice 0 — gate the area (2026-08-19) — `run-slice0.mjs`, 78 assertions

Green twice. Three permission families, nine permissions, nine policies, and **all 149 endpoints
gated per-action** — 82 `HR.JobArchitecture.*`, 46 `HR.Competency.*`, 21 `HR.ManpowerBudget.*`.
Per-action rather than class-level because stacked `[Authorize]` attributes are ANDed, so a
class-level default would make slice 6's self tier unreachable.

Both sides of the ladder asserted, positive half first:

- **HR (Read + Write)** authors a job description with a responsibility, submits and reviews it,
  builds the family / sub-family / level taxonomy, defines a competency, sets a position
  requirement, assesses an employee, drafts a manpower budget with a line, and submits it.
- **HR is refused** on the two approvals and every deletion (9 assertions).
- **TenantAdmin** approves the job description — the first job description in this database ever
  to reach `Approved` — and approves the budget.
- **A plain Employee is refused on all five controllers** (24 assertions). Every one of those
  calls returned 200 before this slice.

Seeding needed no edit: `DatabaseSeedingService` and
`HrPermissionRoleFallbackAuthorizationHandler` both read `HrPermissions.RoleGrants`, so the nine
permissions seed and the HR role picks up Read + Write immediately.

**Finding 10 corrected, and finding 11 recorded.** The job-description side is clean — create,
review and approve all resolve the actor via `GetCurrentEmployeeId()` and refuse an unlinked
caller. The budget side is not: `POST budgets/{id}/approve` takes `[FromQuery] Guid approvedById`.
Slice 0 asserts the current shape so slice 7's fix is a visible change rather than a silent one.

**Three fixture defects worth carrying forward**, all of the same family — a payload written from
a name rather than from the DTO:

1. `ResponsibilityType` has no `Primary` — the members are `Core` / `Secondary` / `Occasional`.
2. `CreateJobLevelDto` requires `Name` as well as `Code` and `Rank`.
3. ⚠ `GET employee-competencies/employee/{id}/gaps` is **not a list**. It returns an
   `EmployeePositionCompetencyGapSummaryDto` — an object carrying `competencyGaps` inside it. The
   first version of the assertion said `Array.isArray(...)` and failed for the right reason. The
   frontend type for this must be written from the DTO, never from the route name.

**Also confirmed:** the API binds enum names from JSON (`"Core"`, `"Technical"`, `"High"` all
bound), so the harness and the eventual UI can send strings.

---

## 10. Area status

**IN PROGRESS** — slice 0 of 13 landed 2026-08-19. **78 assertions.**
