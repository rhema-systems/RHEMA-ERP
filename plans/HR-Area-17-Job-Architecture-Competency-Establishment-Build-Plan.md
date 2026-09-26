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
in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` for the single post-HR sweep, and read Finance
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

### Slice 1 — the read and error contracts (2026-08-19) — `run-slice1.mjs`, 145 assertions

Green twice; `probe-reads.mjs` now reports zero blanks.

**The read contract.** The probe built one fully populated fixture — taxonomy, staff level, salary
grade, a job description carried all the way to `Approved`, a competency, a position requirement,
an employee assessment, a budget with a line — and then read it back through every endpoint. It
found **12 reads returning a blank resolved name**. The `.Include` chains had drifted exactly as
that failure mode predicts: the by-id reads carried all ten job-description lookups, the list,
paged, by-position and by-status reads carried two. Same row, different content depending on which
endpoint fetched it, every one a 200.

Fixed with one chain per entity — `JobArchitectureQueryExtensions.WithLookups()` — called by
**26 read paths**: 7 in `JobAnalysisService`, 12 in `JobAnalysisRepository`, 7 in
`CompetencyRepositories`. Two of those were worse than a partial chain: the owned-read helpers for
`PositionCompetency` and `EmployeeCompetency` used the generic `GetByIdAsync`, which carries **no
navigations at all**, so `GET position-competencies/{id}` returned a requirement with no position
title, no competency code and no competency name.

⚠ The point of one shared chain rather than added includes: a `*Name` field with no matching
`.Include` is a blank column waiting to ship, and per-method chains decay the next time a lookup
FK is added. There are now 10 lookups on `JobDescription` and one place to add the eleventh.

**The error contract.** `GlobalExceptionHandlingMiddleware` replaces the detail of **both**
`ArgumentException` and `InvalidOperationException` with a fixed string, and these three services
raised nothing else — **76 throws, all mute**, with "not found" indistinguishable from a malformed
payload. `JobArchitectureException` classifies the refusal (NotFound → 404, InvalidState and
Conflict → 409, Invalid → 400) and **68 throws converted**: 53 NotFound, 9 InvalidState,
5 Conflict, 1 Invalid. The 8 left alone are all "No tenant is associated with the current user" —
infrastructure, not a rule a caller can act on.

Messages that now reach the user, none of which had ever been seen: *"Only draft job descriptions
can be submitted for review"*, *"Cannot update an approved job description. Create a new version
instead"*, *"Only submitted budgets can be approved"*, *"This competency is already assigned to
the position"*, *"An assessment record already exists for this employee–competency pair. Use the
update operation to record a re-assessment."* The harness asserts the message, not only the code.

**Three things the probe reported that are NOT defects**, each now stated in the harness so nobody
re-chases them:

1. `descriptions/position/{id}` and `descriptions/status/{status}` return
   `JobDescriptionSummaryDto` — a deliberate narrow projection. The register screen uses
   `descriptions/paged`, which carries the full DTO.
2. `employee-competencies/{id}/history` is empty after a create, correctly: the history snapshots
   the values a **re-assessment** replaced. Slice 1 asserts both directions — 0 rows after the
   create, 1 row after the update, holding the level it replaced.
3. `CompetencySkillIndicatorController` has its own `api/competency-skill-indicators` route, so the
   route-collision risk in §3.11 is cleared.

**Recorded, not fixed here:** `PositionCompetencyRepository.BulkReplaceForPositionAsync`
soft-deletes the existing set and inserts the new one in a single `SaveChanges`, straight into the
unfiltered `IX_PositionCompetency_Tenant_Position_Competency`. That is finding 7's third face and
it will 500 on the first bulk-set that re-includes a competency already present. Slice 5 owns it.

### Slice 2 — the job-description spine (2026-08-19) — `run-slice2.mjs`, 85 assertions

Green twice; slices 0 and 1 re-run green. **308 assertions in the area.**

**The good news first: all 11 child collections work.** Responsibilities, qualifications,
competencies, physical demands, working conditions, equipment/tools, equipment training, reporting
relationships, duty items, PPE requirements, medical requirements and responsibility KPIs each pass
create → read back → update → read the update. That is ~44 endpoints with no execution history,
and none of them was broken. `descriptions/{id}/details` assembles all of it in one payload, with
KPIs nested under their responsibility and training under its equipment item.

The first run failed 12 assertions. **Three were the application; nine were the harness** — worth
separating, because the harness ones each carry a lesson.

**Defect 1 — the document number repeats after a delete. Confirmed empirically:** two job
descriptions came back as `JD-2026-00058`. `CountAsync(live rows this year) + 1` reissues a number
the moment any row is soft-deleted, and with **no unique index on `JobDescriptionNumber`** nothing
catches it — it silently produces two documents with one number. Now takes the highest number
already issued and reads **through** the soft delete via `GetQueryableIncludingDeleted`: a deleted
job description has still consumed its number, and a reissued number is worse than a gap. This is
finding 8, and the quieter sibling of the area-13 counter trap — same cause, no 500 to announce it.

**Defect 2 — `CreateJobDescriptionDto` dropped 18 fields** that both the entity and
`UpdateJobDescriptionDto` carry: the entire classification, valuation and authority block. A create
form had to save and then immediately save again, and anything that skipped the second save left
the job-family / sub-family / level tables with no consumer at all — which is part of why they hold
zero rows. Added and mapped. Create and update responses now re-read through `WithLookups()` so a
write response and a subsequent GET agree.

**Defect 3, the sharp one — `CreateNewVersionAsync` silently emptied the classification.** It
copied title, summary, effective date and review cycle, and dropped job family, sub-family, level,
staff level, salary grade, union, occupation code and the whole valuation — while `CloneAsync`, the
*less* important path, copied every one of them. Versioning is the annual-review route: it is the
one that must not lose the record. Now copies the same block, asserted field by field on the
successor.

**The nine harness errors, and what each taught:**

- ⚠ **`versionNumber === 1` is asserting the database, not the code.** It came back 49, correctly:
  versions are per position (MAX + 1) and the fixture position already carried 48 job descriptions
  from area 6's runs and slices 0–1. Assert the *increment*.
- ⚠ **Superseding happens on approval of the successor, not on drafting it** — a draft must not
  retire the document the organisation is currently working to. My assertion demanded it at the
  wrong moment. The corrected harness drives the whole path, which had never once run: draft
  successor leaves the original Approved and un-superseded; approving it moves the original to
  `Superseded` with an expiry date and moves the position's `current` to the successor.
- ⚠ **`details.jobWorkingConditions` is the entity's navigation name; the DTO calls it
  `workingConditions`.** Third instance in three slices of the same trap — a name read off the
  wrong artefact. Caught by the harness rather than by a blank panel in the UI.

**And one thing that looked like a fourth defect and was not:** clone appeared to drop the job
family. It did not — it faithfully copied `null`, because defect 2 meant the source was never
classified in the first place. A symptom two steps downstream of its cause.

### Slice 3 — approval on the workflow engine, and FR-HR-134 (2026-08-19) — `run-slice3.mjs`, 36 assertions

Green twice; slices 0-2 re-run green afterwards, which is also the proof that the definition
retires cleanly. **344 assertions in the area.**

**Application 7 of the workflow recipe**, all five steps: catalog entry, adapter, a
`BuildEntityContextAsync` case, a display resolver, and `entityTypeMapping.ts`. The routing context
exposes job family, sub-family, level, staff level, criticality, intrinsic value, benchmark salary,
bargaining-unit flag and whether this is a first version — because "who approves a job description"
differs by family and by level, and a first issue is a different decision from an annual re-issue.

**Why it belongs on the engine.** Before this slice, approving a job description asked one
question: does the caller hold `HR.JobArchitecture.Admin`? But an approved job description fixes
what a role is accountable for and carries the valuation and suggested salary grade a pay decision
later rests on. Who signs that off is a tenant's decision, not a permission.

**Three design calls, and the reasoning:**

1. **The adapter sets the decision; the service applies the consequences.** Approving must also
   supersede the version it replaces, and an adapter is synchronous and sees only the entity handed
   to it. That is not tidiness: `OfferLetterService` selects a position's job description by
   `SupersededByVersionId == null`, so two unsuperseded approved versions make an offer letter
   ambiguous. `ApplyApprovalConsequencesAsync` is now shared by both routes, and runs only when the
   engine's outcome is actually `Approved` — a mid-chain step returns `Pending` and must retire
   nothing.
2. **Publishing a definition closes the direct route.** Leaving both open would let an Admin
   permission bypass the chain the tenant configured. An Admin now gets a 409 naming the workflow
   queue.
3. **`ApprovedById` is left alone by the adapter.** The engine hands back the approving *user*;
   `ApprovedById` is an Employee FK. The service resolves and stamps the employee — asserted, so a
   user id written into an employee column would fail rather than merely look odd.

⚠ **The slice's own defect, and it is Trap 3 for the third area running.** I gated the two workflow
endpoints on `HR.JobArchitecture.Write`, reasoning that relaxing from Admin was enough. It made the
action reachable by **nobody**: the holders of the permission are refused by the engine for not
being the assigned approver, and the assigned approver is refused by the permission. The harness
caught it because it mints an approver holding role `Employee` and nothing else — which is what a
job-family owner or department head actually looks like. Both endpoints now carry a plain
`[Authorize]`, with `CanUserApproveAsync` doing the work: it asks whether *this caller* is the
assigned approver of the step in front of *this record*, which is stricter than a permission, not
looser. **Restated for the next area: if the actor is named by the record or by a definition, a
permission gate can only get in the way.**

⚠ **And the name-versus-source trap, fourth instance in four slices.** I wrote
`GET /api/Workflow/instances/entity/{type}/{id}` from what it ought to be called; it 404s. The
endpoint the UI actually uses is `entity-summary`, with the type and id as **query** parameters
(`frontend/src/hooks/useWorkflowRecord.ts`). It also has to be read *while an approval is
outstanding* — once the chain completes there is no active instance and the fields the button
depends on are legitimately null. The assertion now covers `canCurrentUserApprove` from both sides,
because that flag decides whether the approve button renders: true for HR would mean the screen
offers an action the API then refuses, which reads to a user as a broken backend.

**FR-HR-134 asserted end to end on the direct route**, before any definition exists: approving a
second job description moves the position's `current` to it, retires the first to `Superseded` with
an expiry date, names its successor, and leaves **exactly one** approved version standing for the
position.

**Fixed in passing, in a file this slice already touched:** `ProbationPeriod` was registered in the
backend catalog by area 15b but never added to `entityTypeMapping.ts`, so its workflow tab never
appeared under the HR module filter.

### Slice 4 — the job architecture taxonomy (2026-08-19) — `run-slice4.mjs`, 60 assertions

Green twice; slices 0-3 re-run green. **404 assertions in the area.**

Fifteen endpoints, zero rows, and a service that was pure CRUD with no rules at all. What it now
enforces, and why each matters:

- **Codes are unique** per tenant, case-insensitively, on all three tables. None of them carries a
  unique index beyond its primary key, so two families could share the code `ENG`. ⚠ Enforced in
  the service **rather than by an index, deliberately**: an index does not know about the soft
  delete (the area-13 trap), so a deleted `ENG` would block a new one forever. Scoping the check to
  live rows gets the rule without that consequence — and the harness asserts a deleted family's
  code *is* reusable, so the distinction is pinned down rather than assumed.
- **Ranks are unique.** Two career levels at the same rank make "more senior than" unanswerable,
  which is the one question a ladder exists to answer. The refusal names the level already holding
  the rank.
- **A classification in use cannot be removed.** A soft delete hides the row from every list and
  leaves the foreign key intact, so job descriptions keep resolving a name HR can no longer see or
  edit — the classification becomes unmaintainable rather than going away.
- **The classification must hang together.** `JobFamilyId` and `JobSubFamilyId` arrive
  independently on the DTO and nothing related them, so a job description could be filed under
  family *Finance* and sub-family *Architecture* at once. No foreign key catches it because neither
  id is wrong on its own — and a screen with two dropdowns produces it the first time someone
  changes the family and not the sub-family. Validated on create **and** update.

**The starter seed (D-5)** is `JobArchitectureSeeder`, wired into `HrSeedOrchestrator`: 11 families,
29 sub-families and an 8-rung ladder, all read off the organisation units `TdcOrganogramSeeder`
already creates. Nothing invented. ⚠ `CareerLevel.SalaryGradeId` is left **null** on every level —
mapping a career ladder onto pay grades is a TDC decision with money attached and payroll owns the
grade store. The harness asserts it stays unset, so if someone links them later that line fails and
the decision is visible rather than silent.

⚠ **The slice's own defect: the seed step silently skipped, and a count assertion would have hidden
it.** The probe was `AnyAsync(x => x.TenantId == tenantId)` — "does this tenant have any job
family?" The harness had already created twenty of its own, so the orchestrator reported
`[skip] Job architecture — already seeded` and the starter vocabulary never landed. In production
that means **any tenant where one person had ever added a single job family would silently never
receive the starter set.** Generalised: *"is the table empty?" is the right signal for a
create-the-baseline seed and the wrong one for a starter-vocabulary seed* — the latter must ask
whether **its own** rows are present, because the table can be non-empty for reasons that have
nothing to do with it. It was caught only because the harness asserts the seeded rows **by code**;
`families.length >= 11` would have passed on the residue and told me nothing.

⚠ **And the area-13 lesson again, live: the rank rule immediately broke slices 0-2**, which each
created a level at a fixed rank (4, 5, 6) and now collided with the seeded ladder and with each
other. The rule is right; the fixtures were assuming a free field. `harnessRank()` in `setup.mjs`
derives a non-colliding rank from the stamp. **When you make a field unique, grep every fixture
that sets it — the compiler cannot help, and neither can the tests until they run.**

**One refusal, every obstacle.** Deleting a family in real use first reported only its sub-families,
so a user would clear four of them to be told about the job descriptions underneath. It now collects
both and states them together.

**Recorded for D-6:** `SalaryGrades` holds ten rows named `E2E RecD Band 141309` — area-6 harness
residue alongside the two real TDC grades (`M1 General Managers`, `M2 Heads of Department`). Same
shape as the 43 residue job descriptions; both belong in the area-close cleanup.

### Slice 5 — the competency framework (2026-08-19) — `run-slice5.mjs`, 44 assertions

Green twice; slices 0-4 re-run green. **448 assertions in the area.**

**Finding 7 closed, all three faces, by migration
`20260819020000_FilterCompetencyUniqueIndexesOnIsDeleted`.** All three competency unique indexes
were unfiltered, and `DeleteAsync` is a soft delete everywhere here, so a deleted row held its slot
forever. Verified live afterwards: all three now read `([IsDeleted]=(0))`.

The third face was the one that mattered most in practice, and it was not a rare path.
`BulkReplaceForPositionAsync` soft-deletes the whole current set and inserts the new one in a
**single `SaveChanges`** — which an unfiltered unique index rejects mid-transaction. So on
`position/{id}/bulk-set`, the endpoint a "manage this position's competencies" screen calls on
**every save**, keeping any competency across a save was a 500. The harness proves the fix by doing
it rather than by reading the schema: save the same list twice; then keep one, drop one, add one;
then bring the dropped one back.

⚠ **The migration is described in three places and they must agree** — the migration SQL, the EF
model in `ApplicationDbContext.HR.cs` (which is what `rebuild-db` builds from), and
`ApplicationDbContextModelSnapshot.cs` (so a later `migrations add` does not try to re-add the
filter). Updating only the first would have left `rebuild-db` recreating unfiltered indexes.

**Two build-system rules learned here, and they will recur in slices 7-8:**

1. ⚠ **A hand-written migration carries its `[DbContext]`/`[Migration]` attributes inline and must
   NOT be listed in `FastBuildMigrationMetadata`.** The two are mutually exclusive: that file
   supplies the attributes for migrations whose generated `.Designer.cs` is excluded from fast
   builds, so declaring both merges the partial classes and duplicates the attributes. 99 of this
   project's 285 migrations are hand-written this way and none appears in that file.
2. ⚠ **`dotnet ef` cannot run against a fast Debug build** — it needs
   `ApplicationDbContextModelSnapshot.cs`, which that build omits. The right switch is
   **`TdcFocusedEfToolingBuild`**, set as an **environment variable** (`dotnet ef` spawns several
   MSBuild invocations and `-p:` does not reach them all). It keeps the snapshot compiled while
   still skipping the historical designers — over 350 MB of generated C# — so it is Release-grade
   correctness at close to Debug speed. `--configuration Release` also works and is slower.

**A fourth instance of the slice-1 read shape, which slice 1 could not have caught.** Skill
indicators returned blank `competencyName` and `skillName`: the by-competency list included the
skill but not the competency, the by-skill list did the reverse, and the service's by-id helper
carried neither. Slice 1's probe missed it because there were no skills or indicators in the
database to put in a fixture — *an empty table hides a read defect as effectively as a correct
implementation does*. Now on a shared `WithLookups()` like the other four, with all three create
responses re-reading through it.

⚠ **And a DTO trap one level up from the usual one:** `UpdateCompetencySkillIndicatorDto` and
`UpdatePositionCompetencyDto` both inherit `Id` from `UpdateDtoBase`, and both controllers compare
it to the route id — so omitting it makes every update `"ID mismatch"`. Reading the DTO's own
declaration shows two properties and no `Id`. **Read the base class too.**

### Slice 6 — employee competency, gap analysis and the self tier (2026-08-19) — `run-slice6.mjs`, 57 assertions

Green twice; slices 0-5 re-run green. **505 assertions in the area.**

The gap analysis is the point of the competency half — the comparison between what a position
requires and what an employee has been assessed at — and it computes correctly. The fixture is built
so every status is an answer rather than a coincidence: four requirements, one met exactly, one
exceeded, one short, one never assessed. ⚠ The sharpest assertion is that **the never-assessed one
still appears**, with a null level and no status: a gap analysis that silently omits what has never
been looked at is the most misleading version of that screen there is.

**Two things built rather than exercised:**

1. **The assessor.** `CreateEmployeeCompetencyDto.AssessedById` was purely caller-declared, so an
   assessment could be attributed to anyone — and when omitted it simply stayed null, leaving an
   assessment with nobody accountable for it. It now defaults to the caller's employee id from the
   token. ⚠ Kept rather than stripped, unlike an approver: HR keying in a line manager's assessment
   from a paper form is **recording a fact about someone else**, which is the line the area-13 actor
   rule draws. `batch-assess` already stamped it from the token, so the two paths now agree.

2. **The self tier**, which slice 0 deliberately left closed. `me/profile` and `me/gaps` take the
   employee from the token, because the client `User` object carries no employee link — a browser
   has no id to put in `employee/{employeeId}`, so this could never have arrived as a relaxed
   permission. Probation's `reviews/to-conduct` precedent. Six assertions confirm it did not become
   a way in: a colleague's profile, a colleague's gaps, their **own** by-employee route, the
   assessment list, `qualified-for-position` and self-assessment are all still refused.

   ⚠ **A judgement call, recorded because it diverges from succession:** an employee may read their
   own gap analysis. Succession has no self tier at all because a readiness rating is an assessment
   filed *about* someone. A competency gap is different in kind — it is the list of what their own
   role requires and where they stand against it, which is the thing they need in order to close it.

⚠ **The fixture defect, and it is a new shape worth naming: the harness configured one position and
the code answered about another.** `GetGapsForEmployeeAsync` resolves the position **from the
employee record**, and `mintPlainActor` hired every actor into `positions[0]` regardless of which
position the slice configured — so the gap analysis correctly answered about eighteen requirements
accumulated there by earlier slices, and "the position requires four" was testing a different
position entirely. Fifteen assertions failed and every one of them was right to. **When a feature
resolves an entity indirectly (employee → position), the fixture has to set up the indirection, not
the endpoint's argument.** `mintActorWithRoles` now takes a position index.

### Slice 7 — the manpower budget, FR-HR-135, and area 6 coming alive (2026-08-19) — `run-slice7.mjs`, 49 assertions

Green twice; all of slices 0-6 re-run green. **554 assertions in the area.**

**Application 8 of the workflow recipe, and the first one in this module with a real chain.** FRD
FR-HR-135 names three steps — Department Head → HR → Managing Director — so the fixture publishes
three approval steps rather than one. That is what makes the assertion this slice exists for
possible: **after the department head approves, the budget is NOT approved and NOBODY is stamped as
the approver.** A single-step definition would have passed every assertion about who may approve
and none about what a mid-chain approval must not do. Only the Managing Director's approval — the
last in the chain — makes it real, and it is his employee id that lands in `ApprovedById`.

**Three defects fixed, and the third is in another area:**

1. `ApproveBudget` took **`[FromQuery] Guid approvedById`** — caller-declared, on the endpoint that
   authorises headcount and the money behind it. Now from the token. (Finding 10, closed.)
2. `RejectAsync` took a reason and **discarded it** — it reached a log line and nothing else, so a
   budget holder could see their budget refused with no way to find out why. New `RejectionReason`
   column (migration `20260819040000_AddManpowerBudgetRejectionReason`), written by both routes.
   The endpoint also took `[FromBody] string` — a bare JSON string a form must send quotes and all;
   it takes a DTO now. (Finding 11, closed.)
3. ⚠⚠ **A DRAFT manpower budget was authorising headcount.** `BuildBudgetCheckAsync` in
   `StaffRequisitionService` matched any `ManpowerBudgetLine` of the right position and fiscal year
   and merely *sorted* approved ones first, so a department typing "50" into a draft would constrain
   — or excuse — a requisition before anyone had ruled on it. That inverts the point of FR-HR-135's
   chain. **Nobody could have found it before this area**: there were no `ManpowerBudgetLine` rows
   in the database at all, so the branch had never once run with data. Only `Approved` or `Active`
   counts now.

**Area 6's enforcement is live for the first time**, and the harness follows one requisition all the
way through: "no budget line" while the budget is a draft, still "no budget line" mid-chain, and
only after full approval *"2 filled + 5 requested against an approved budget of 3"*. A one-post
requisition against the same budget reads "within budget", so the check is not merely a red light.

Also added: **an empty budget cannot be submitted.** It authorises no posts, so sending one up a
three-step chain wastes three people's time — and slice 8 derives the establishment from the lines,
so an empty one would approve an establishment of nothing.

⚠ **Two fixture lessons, both the area-13 shape, both caught only by re-running:**

- The empty-budget rule **broke slice 1**, which submitted a lineless budget. Correct rule, older
  fixture.
- Slice 7 was **not repeatable against itself**. The budget check matches on **position + fiscal
  year**, so the second run found the first run's *approved* budget and "a draft authorises
  nothing" failed — correctly. Varying the fiscal year is not a fix: `FiscalYear` is validated to
  2000-2100, which is 70 usable values and therefore a collision waiting to happen, and an approved
  budget cannot be deleted to clean up after itself. `mintPosition()` gives the run a position it
  owns, removing the shared axis instead of making a clash less likely. **A harness is not
  repeatable until it has been run twice in a row; "green" on a first run proves less than it
  looks.**

**Money events registered** in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`, including two findings for
the sweep rather than for a slice: `ManpowerBudget` carries **no currency at all**, and
`ActualSpent`/`Variance` **have no writer anywhere** — they can only come from Finance actuals, and
a budget's variance is permanently zero until the sweep decides who fills them. Also a **three-way**
training double-count: area 7's training budget, succession development activities, and
`ManpowerBudget.TrainingBudget`.

### Slice 8 — FR-HR-136, the approved establishment (2026-08-19) — `run-slice8.mjs`, 40 assertions

Green twice; all of area 17 re-run green, **and all of area 8** — 594 assertions here, 306 there.
**This is the requirement the area was chosen for, and the one three previous areas worked around.**

**The design turns on one column.** FR-HR-136 cannot be enforced against `ExpectedHeadcount`,
because that column cannot distinguish an authorised number from its own default — 132 of 146 live
positions carry `1`, untouched, while one holds over a thousand people. `EstablishmentApprovedOn`
answers the missing question: **null means nobody ever authorised a headcount, so nothing constrains
the post.** That is what makes `EstablishmentEnforcementMode` safe to default to **Block** where the
budget ladder defaults to Warn — warning about exceeding something three people authorised would
make the authorisation pointless.

What landed:

- **D-2**: budget approval writes `ExpectedHeadcount` from each line's `PlannedCount` and stamps the
  date — **only on the step that completes FR-HR-135's chain**, so a department head cannot set the
  headcount the other two are still deciding on. Asserted at each of the three steps.
- **FR-HR-136** enforced on requisition submit *and* approve. ⚠ It differs from the budget check
  beside it in a way worth stating: the budget check reads `ManpowerBudgetLine.CurrentFilled`, a
  number typed when the budget was written; this counts **who is actually in the post now**. A
  budget written in January and a requisition raised in November will disagree, and the live count
  is the true one.
- **Area 8's FR-HR-173 promoted from advisory to a block** — but only for posts with an authorised
  establishment. Everywhere else it stays the note it was. *The rule did not change; the data caught
  up with it.*
- **An admin path** for posts no budget covers, Admin-tier because it is the one place FR-HR-135's
  chain can be bypassed, leaving `EstablishmentSourceBudgetId` null so a screen can say the number
  came from HR rather than implying an approval that never ran.

⚠⚠ **Two defects in my own slice, both found by running ANOTHER AREA'S harness**, which is the
lesson worth keeping:

1. **The guard was on the wrong path.** The admin endpoint refused to establish a post below the
   number already in it; the **budget** path had no such check — and the budget path is the one that
   will carry most of the organisation. An approved budget quietly established a post for 1 with 62
   employees standing in it, after which every movement into that post was refused, correctly and
   uselessly, by a rule enforcing a number that was never achievable.
   `RequireEstablishmentIsAchievableAsync` now refuses it, naming each position and its live count,
   **at submit as well as at approval** — failing at step 3 of 3 after three people have spent time
   on it is the worst moment to discover a number that was wrong when it was typed.
2. **An establishment set in error could not be withdrawn.** Wrong budget approved, wrong number
   typed, and the post was permanently constrained by a figure nobody meant, with every requisition
   and movement against it refused forever. That is a trap, not a rule.
   `DELETE establishment/position/{id}` withdraws it. ⚠ `ExpectedHeadcount` is deliberately left as
   it stands: withdrawing says *"no longer authorised"*, not *"wrong"*, and every rule keys off
   `EstablishmentApprovedOn`.

⚠ **And a fixture defect with a moral: a helper that names itself to the front of a shared list
silently redirects everything else.** `mintPosition` created positions titled
`A17 Harness Position …`; `/api/EmployeePositions` returns them **sorted by title**, and
`mintActorWithRoles` hires into `positionList[0]`. So every actor minted by every slice thereafter
was hired into a harness position, which accumulated **125 employees** and then made the new
achievability guard refuse any budget naming it. Renamed to sort last, and slices 0 and 1 now give
their budget lines a position of their own.

### Slice 9 — analytics worth acting on (2026-08-19) — `run-slice9.mjs`, 25 assertions

Green twice; slices 0-8 re-run green. **619 assertions in the area.** Backend complete.

The existing `analytics` endpoint counted job descriptions correctly, and reported
`positionsCovered` **with no denominator** — "1 position covered" out of 2, or out of 146?
FR-HR-134 asks for approved job descriptions *against positions*, so coverage is the whole
reporting question. Added `totalPositions`, `positionsUncovered`, `positionsEstablished` and
`positionsOverStrength` — that last being the only number on the dashboard that is a **live problem
rather than a progress bar**, since every over-strength post refuses movements and requisitions
until someone resolves it.

`GET positions/uncovered` is the work list behind it, ordered by **how many people are doing a job
nobody has described**. ⚠ It distinguishes a position carrying an unapproved draft from one with
nothing at all: the first needs an approver, the second needs an author, and reporting them as the
same thing sends the wrong person to fix it.

`GET employee-competencies/gaps/organisation` is the training-needs view — the reason the
competency framework is worth maintaining. ⚠ It reports **three** outcomes, not two: meets, below,
and *never assessed*. Folding "never assessed" into "below requirement" would report a training need
the organisation has no evidence for, on the screen that decides training spend. The fixture puts
three employees in one position — one meeting, one short, one unassessed — so each number is an
answer rather than a coincidence.

⚠ **Deliberately not built, and asserted as absent so it reads as a decision rather than an
oversight: budget variance.** `ManpowerBudget.ActualSpent` and `.Variance` have no writer anywhere;
they can only come from Finance actuals. A variance chart would report zero and call it news. It is
registered in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` for the post-HR sweep.

**Two of my own mistakes, both the same shape as the defects this area keeps finding:**

- `GetUncoveredPositionsAsync` landed in `ManpowerBudgetService`, which has no job-description
  repository, while the controller called it on the job-description service. A coverage question
  about job descriptions belongs on the job-description service; moved, along with its interface
  declaration.
- The fixture moved employees between positions with `PUT /api/hr/Employees/{id}`, spreading the
  **read** DTO into the update — a 500. `mintActorWithRoles` now accepts a position **id**, so
  actors are hired into the right post rather than moved afterwards. *Written from the wrong
  artefact*, again, this time in the harness.

### Slice 10 — the job-description screens (2026-08-19) — `run-slice10-ui.mjs`, 43 assertions

Green twice; all ten backend harnesses re-run green. **662 assertions in the area.** Frontend `tsc`
clean (the 19 remaining errors are all pre-existing, in another developer's inventory module) and
`next lint` clean.

Four screens: the register with coverage tiles, the create form, the detail screen with nine tabs
over the eleven child collections, and a coverage work list at `/hr/job-descriptions/gaps`.

⚠ **That route is `/gaps` and not `/coverage` for a reason worth carrying forward:** the repository
`.gitignore` has a bare `coverage/` rule for test-coverage output, so the page was **silently
excluded from the commit** — added, type-checked, linted, and invisible to git without a word. Any
route directory named `coverage`, `dist` or `build` vanishes the same way. Renaming beats carrying a
`git add -f` that the next person will not know about. Plus `types/hr/job-architecture.ts`
and `services/hr/job-architecture.service.ts`, both written **from the C# DTOs**.

**Three decisions the screens encode, each of them a defect this area already paid for:**

1. ⚠ **The approve button renders off `canCurrentUserApprove`, never off a permission.** Once a
   tenant publishes a definition the approver is whoever it names — a job-family owner, a department
   head — and they hold no HR permission at all, while the direct route 409s for everyone. A
   permission check would hide the button from exactly the people who need it, and show it to people
   the API then refuses, which reads to a user as a broken backend.
2. ⚠ **Untouched optional pickers send `undefined`, not `''`.** The form maps `value || undefined`
   because an empty string on a Guid is a 400, not a null. The probe asserts **both** directions —
   that the bare form saves, and that the same call sending `''` would 400 — so the mapping is
   pinned as load-bearing rather than looking like defensive tidiness.
3. ⚠ **Changing the job family clears the sub-family.** Two independent dropdowns produce a
   cross-family mismatch the first time someone changes one and not the other, and the API refuses
   it (slice 4). The probe asserts the refusal, so the clearing behaviour has a reason on record.

**And the create form carries the classification**, which is the visible payoff of slice 2: one
save, not two. Before that fix a form had to save and immediately save again, and anything that
skipped the second left the taxonomy with no consumer.

The probe also pins two naming traps the screens would otherwise have shipped: the detail
collection is `workingConditions` (the entity navigation `JobWorkingConditions` would render
`(undefined)` in a tab count), and `statusName` carries the enum **name** — a screen comparing
against the `[Description]` label "Pending Review" would silently never match.

### Slice 11 — the competency screens (2026-08-19) — `run-slice11-ui.mjs`, 42 assertions

Green twice; all eleven earlier harnesses re-run green. **704 assertions in the area.** Frontend
`tsc` back to the 19-error pre-existing baseline, `next lint` clean.

Four screens: the framework under Administration (setup), the organisation gap view, the position
requirements editor, and an employee's own profile and gaps.

**Three things the screens are careful about, each a defect this area already paid for:**

1. ⚠ **"Below requirement" and "not assessed" stay separate all the way to the tiles.** They are
   two counts, never summed. An unknown is not a training need, and the two call for different
   actions — a course, or an assessment. The probe asserts them independently for the same reason.
2. ⚠ **A null level renders as "—", never as 0.** Rendering an unassessed competency as zero would
   tell someone they scored the lowest possible mark on something nobody has looked at.
3. ⚠ **The requirements editor sends the whole set on every save**, because `bulk-set` replaces
   rather than merges. That is also the endpoint that used to 500 whenever any competency survived
   an edit, so the probe drives the sequence a person actually performs — change a level, drop one,
   add one, save; then bring the dropped one back; then clear the lot — rather than one happy path.

The assessment form has **no assessor picker**: the API defaults it to the caller, and a value the
client cannot know is a value the client must not send. The self screen reads `me/profile` and
`me/gaps` for the same reason as always — the client `User` object still carries no employee link —
and three assertions confirm the self tier did not become a way in.

⚠ **One error worth noting because `tsc` caught what review would not:** the sidebar used a
`TrendingDown` icon that was never imported. It compiled in my head and failed in the compiler,
which is exactly the division of labour worth keeping — the type checker for what it can see, the
payload probe for what it cannot.

### Slice 12 — the manpower budget and establishment screens (2026-08-19) — `run-slice12-ui.mjs`, 41 assertions

Green twice; all twelve earlier harnesses re-run green. **745 assertions in the area.** `tsc` at the
19-error pre-existing baseline, lint clean. **Every screen in the area is now built.**

Four screens: the budget register, the create form, the budget detail with FR-HR-135's chain
visible, and the establishment admin screen.

⚠ **The budget detail is the only screen in the module where the same button means something
different to three people.** FR-HR-135's chain is Department Head → HR → Managing Director, so
"Approve" must appear for exactly one of them at a time and the budget must stay `Submitted` until
the last acts. That cannot be read off a permission, and the probe checks `canCurrentUserApprove`
**from all four seats** — the three approvers and the submitter — at each stage of the chain. It is
the clearest demonstration in the area of why the engine, not a role, answers that question.

**What the screens refuse to show, and why each absence is deliberate:**

- **No approver field on the create form.** The approval is stamped from the token of whoever
  completes the chain. Until slice 7 this endpoint took `approvedById` as a **query parameter**.
- **No `totalBudget` field** — the API computes it from the four component budgets; sending it would
  be sending a number the server is about to overwrite. The form shows the running total as a
  read-only figure instead.
- **No variance figure anywhere.** `actualSpent` and `variance` have no writer; presenting a
  permanent zero as a variance would be inventing news. The probe asserts both are zero so the
  absence stays a decision.
- **An unestablished position renders an em dash, not its headcount.** The number is the column
  default and means nothing — the same reason a null proficiency renders as "—" in slice 11.

The submit button is disabled until a budget has a line, so the API's refusal is explained before it
happens rather than after. And the establishment screen sorts **over-strength posts first**: those
are the ones refusing recruitment and movements right now, and a list sorted by name buries them.

### Slice 13 — the content audit (2026-08-19) — `audit-content.mjs`, 364 assertions

**78 of 78 GET endpoints exercised**, counted rather than claimed, each asserted by id against a
fixture built to give it something real to return. Stable across three consecutive runs; all
thirteen slice harnesses re-run green. **1,109 assertions in the area.**

⚠ **The audit failed 20 assertions on its first run, after twelve green slices** — the pattern area
11 recorded, confirmed again. Three of those were real, and none of them was a missing feature:

1. ⚠⚠ **A re-assessment wiped the assessor.** `UpdateEntity` assigned `dto.AssessedById`
   unconditionally, so re-assessing an employee without naming an assessor set it to **null** — the
   level changed, the date changed, and nobody was accountable for either. Slice 6 defaulted the
   assessor on **create** and I never looked at update, **which is the path people use more**. Five
   endpoints reported `assessedByName` as null and every one was right to.
2. ⚠ **`budgets/organization-unit/{id}/current` returned an arbitrary budget.** It filtered on unit
   + current year + Approved and then took `FirstOrDefault` **with no ordering**, so a unit with two
   approved budgets for one year answered differently on different calls. Now Active first, then
   most recently approved — which makes the READ agree with what the WRITE already does, since
   approving a budget overwrites `ExpectedHeadcount` (D-2) and the latest approval is therefore
   already the establishment in force.
3. ⚠ **`GET /api/competencies` is paged at 20**, and the audit asserted against it without a page
   size. *An assertion against a paged endpoint with no page size is an assertion about how much
   data the database happens to hold.*

**Defects 2 and 3 were found by running the audit TWICE.** The first run passed both — the fixture
happened to land on page one, and the unit happened to have one approved budget. **A single green
run of an audit proves less than it appears to; the second run is where non-determinism surfaces.**

⚠ **And the audit's own helper had to be fixed without becoming conditional.** Twelve failures came
from passing no id, so `rows.find(r => r.id === undefined)` matched nothing while the list held a
row. The obvious repair is `if (rows.length)` — which is exactly the trap area 13 named. What it
does instead: when an id is given the row must be found **by it**; when none is, the fixture still
guarantees a row, so a non-empty list is the assertion. Never an `if`.

---

## 10. Area status

**AREA 17/18 COMPLETE — 2026-08-19.** 14 slices, **1,109 assertions**, content audit clean at
**78/78 GET endpoints**, stable across three consecutive runs.

**Delivered:** FR-HR-134 (approved job descriptions against positions), FR-HR-135 (the manpower
requisition chain, Department Head → HR → Managing Director, on the workflow engine), FR-HR-136 (the
approved establishment, with teeth) and FR-HR-004's planning link in the form that is actually
actionable — coverage, the uncovered work list, and the organisation-wide competency gap. **Every
Mandatory requirement in FRD §A1.1, the last Level-1 HR section that was still open.**

**Screens:** `/hr/job-descriptions` (register, `new`, `[id]` with nine tabs, `gaps`),
`/hr/competencies` (organisation gaps, `me`, `positions/[positionId]`), `/hr/manpower-budgets`
(register, `new`, `[id]`), `/administration/hr/competencies`, `/administration/hr/establishment`.

**What it cost elsewhere, and paid back:** area 6's requisition budget enforcement runs against real
data for the first time (and no longer accepts a *draft* budget as authorisation); area 8's
FR-HR-173 rule is promoted from advisory to a block wherever an establishment was actually
authorised. Both were found by running **other areas' harnesses**, not this one's.

**Still open, recorded not forgotten:** `ManpowerBudget` carries no currency and nothing writes
`ActualSpent`/`Variance` — both registered in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` for the
post-HR sweep, along with a three-way training double-count. D-6's cleanup (43 residue job
descriptions and ten `E2E RecD Band` salary grades from area 6) is listed there too.
