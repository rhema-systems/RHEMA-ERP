# HR Area 13 — Succession Planning & Talent Management: Build Plan

**Created 2026-08-18.** Area 13 of 27. Tier B, fourth of eight.

---

## 1. How to use this document

Read §2 and §3 before touching anything. §3 is measured, not inferred — every claim in it came
from a live call against the running API on 2026-08-18, and the probes that produced it are in
`D:\Rhema\TDC ERPS\dev-harness\hr-succession\`.

§3 records the area **as found**, before slice 0. It is deliberately not rewritten as slices land —
the findings register (§6) and the slice log (§9) carry current state.

The single most important thing in this document is **F-01**: the area is **not gated**. A plain
`Employee` with no HR role created a succession plan, read every plan in the tenant, and deleted a
plan — measured, not suspected. Succession data is the most confidential store in the HR module.

Unlike areas 11 and 12, the **spine is alive**: create → read → list → dashboard all work on the
first try. This is a hardening-and-porting area, not a resurrection.

---

## 2. Status at a glance

| | |
|---|---|
| Backend surface (in scope) | **7 controllers, 136 endpoints, 15 entities, 15 DbSets** |
| Adjacent, ownership undecided | `MentoringController` (19) and `TalentPoolController` (22) — see §4 |
| Backend wiring | ✅ complete — 10/10 services DI-registered |
| Reads | ✅ 23/23 parameter-free GETs return 200; **19 of them empty** (no data, not broken) |
| Writes | ✅ **alive** — create returns `SP-2026-0001`, detail read resolves, dashboard recomputes |
| Detail read | ✅ resolves `positionTitle`, candidate and competency collections present |
| Gating | ❌ **NONE** — all 9 controllers bare `[Authorize]`, zero method-level policies (F-01) |
| Permission family | ❌ absent — only `HR.Medical.*`, `HR.Policy.*`, `HR.Travel.*` are seeded |
| Tenant scoping | ✅ service-level, 77 `TenantId ==` filters in `SuccessionPlanServices` |
| Approvals | ⚠ **bespoke** submit/review/approve chain, not on the workflow engine |
| Actor integrity | ❌ `ReviewedById` / `ApprovedById` are **caller-declared on the body** (F-02) |
| Inbound integration | ✅ **live** — area 5 `SuccessionNominationHandler` writes `TalentPoolMember` |
| Frontend | **0 files** |

---

## 3. Ground truth — measured 2026-08-18

API running on `http://localhost:5000`, healthy. All figures from live calls.

### 3.1 The surface

| Controller | Endpoints | Route base |
|---|---:|---|
| `SuccessionPlanController` | 38 | `api/succession-plans` |
| `SuccessionCandidatesController` | 30 | `api/succession-candidates` |
| `TalentReviewsController` | 23 | `api/talent-reviews` |
| `TalentPoolsController` | 22 | `api/talent-pools` |
| `SuccessionDevelopmentController` | 16 | `api/succession-development` |
| `TalentPoolTypesController` | 5 | `api/talent-pool-types` |
| `SuccessionSearchController` | 2 | `api/succession` |
| **In-scope total** | **136** | |
| `TalentPoolController` | 22 | `api/talent-pool` — *recruitment-side*, see §4 |
| `MentoringController` | 19 | `api/mentoring` — *already built under area 7*, see §4 |

15 entities, all in `SuccessionPlanningEntities.cs`, all `TenantEntity`, all with DbSets in
`ApplicationDbContext.HR.cs:435-449`: `SuccessionPlan`, `SuccessionCompetencyRequirement`,
`SuccessionCandidate`, `SuccessionCandidateGap`, `SuccessionCandidateFeedback`,
`SuccessionDevelopmentActivity`, `SuccessionDevelopmentMilestone`, `SuccessionAction`,
`SuccessionPlanHistory`, `SuccessionDocument`, `TalentPool`, `TalentPoolTypeDefinition`,
`TalentPoolMember`, `TalentReviewSession`, `TalentReviewRating`.

### 3.2 The read sweep — `probe-reads.mjs`

23/23 parameter-free GETs returned 200. **0 failed.** 19 returned empty; the four with content:

- `/api/talent-pool-types` — **6 rows** (seeded by `TalentPoolTypeSeed.cs`)
- `/api/talent-pool/candidates` — 9 rows (recruitment candidates, area 6 data)
- `/api/succession-plans/dashboard` — an object, all counters zero
- `/api/talent-pool/analytics` — an object

Empty is the *expected* state here: nothing has ever created a succession plan. It is not the
area-12 shape where reads were empty because writes were broken.

### 3.3 The spine works — `probe-create.mjs`

Create with an HR actor: **201**, `planNumber` `SP-2026-0001`, `status` `Draft`. Then

- detail read resolves `positionTitle: "Accounts Officer"`, `candidates: []`, `competencyRequirements: []`
- `/api/succession-plans` → 1, `/all` → 1, `/no-successors` → 1
- `/dashboard` recomputed live: `totalActivePlans 1`, `totalCriticalPositions 1`,
  `highRiskPositions 1`, `positionsWithoutSuccessors 1`, `coveragePercentage 0`, plus a populated
  `riskHeatmap`

This is the first time the area has executed. Probe rows were deleted afterwards (`cleanup.mjs`);
`/all` is back to 0.

### 3.4 ⚠ The area is ungated — `probe-plain-write.mjs`

All nine controllers carry a bare class-level `[Authorize]`. A grep for method-level
`[Authorize(...)]` across all nine returns **zero matches**.

Measured with `a13v_p1B`, role `Employee`, no HR role of any kind:

| Attempt | Result |
|---|---|
| `GET /api/succession-plans` | **allowed** |
| `GET /api/succession-plans/{id}` | **allowed** |
| `GET /api/talent-reviews` | **allowed** |
| `GET /api/talent-pools` | **allowed** |
| `POST /api/succession-plans` | **201 — created `SP-2026-0003`** |
| `DELETE /api/succession-plans/{id}` | **204 — deleted it** |

Any authenticated user can read who is being lined up to replace whom, each candidate's readiness
and retention risk, and nine-box placements — and can author and destroy plans. There is no
permission family to gate against: `HrPermissions.cs` defines only `HR.Medical.*`, `HR.Policy.*`
and `HR.Travel.*`.

### 3.5 ⚠ Approval actors are caller-declared

`SubmitForReview` takes the actor from the token (`_currentUser.EmployeeId`, with the
not-linked-to-an-employee guard). But `Review` and `Approve` bind `ReviewedById` and `ApprovedById`
as **required Guids on the request body** (`SuccessionPlanDTOs.cs:211,228`). Combined with §3.4,
any signed-in account can approve a succession plan under any employee's name.

Same shape elsewhere in the area: `NominatedById` (talent pool member, ×2),
`FacilitatedById` (talent review session, ×3), `SnapshotCreatedById`. This is exactly the area-12
lesson — *a value the client cannot know is a value the client should not be sending.*

### 3.6 ⚠ The duplicate-plan rule arrives as a 500 — `probe-dup.mjs`

A second plan for a position that already has one returns **500 "An unexpected error occurred while
processing your request."** A different position, same year, returns 201. So the rule exists and
fires; it simply is not mapped to a status a caller can act on, and cannot explain itself.

> ⚠ **Corrected in place 2026-08-18 — this reading was wrong when written.** There is no
> duplicate-plan rule. What fired was a bare unique index, and the same shape had two other faces
> that this entry missed entirely. See §3.9, which replaces it. The paragraph is kept because the
> mistake is instructive: a single 500 was read as "one rule that cannot explain itself" when it
> was really "the code and the schema disagree about what exists."

### 3.7 ⚠ The write response is missing resolved names

`POST /api/succession-plans` returns `positionTitle: ""`; the detail read of the same row returns
`"Accounts Officer"`. A create form that renders the returned object shows a blank position until
refresh. This is the stale-nav shape in [[hr-ported-list-read-bugs]] and the same bug the area-12
UI probe caught.

### 3.8 ✅ Area 5 already feeds this area

`SuccessionNominationHandler` (registered at `HrModuleServiceRegistration.cs:429`) fires on approval
of an appraisal outcome recommendation of type `SuccessionNomination`. It reads
`AppraisalSettings.SuccessionPoolName` / `SuccessionDefaultReadiness`, **find-or-creates** the
tenant's nominations pool, is idempotent on re-nomination, ranks the new member, and resolves the
employee's latest performance rating. The code is sound.

`/api/talent-pools` is empty only because no such recommendation has been approved in this data set.
Proving that join end-to-end is a slice, not a fix.

### 3.9 ⚠⚠ A soft delete does not release a unique index — the area's central defect

Found while trying to run the slice-0 harness; it replaces §3.6. `DeleteAsync` is a **soft** delete,
but a unique index does not know that, so a deleted row keeps its slot forever. Three faces, all
reaching the caller as an unexplained 500:

1. **Plan numbering.** `GeneratePlanNumberAsync` was `GetQueryable().CountAsync(...) + 1`, and
   `GetQueryable()` excludes soft-deleted rows — while `IX_SuccessionPlan_Tenant_PlanNumber` does
   not. Delete one plan and the counter falls back onto a number the index still holds, so **every
   subsequent create fails, permanently**, from one ordinary press of the delete button.
2. **The active-version slot.** `UX_SuccessionPlan_ActiveVersion` is unique on
   `(PositionId, IsActiveVersion)` filtered to `IsActiveVersion = 1`, and the filter says nothing
   about `IsDeleted`. A soft-deleted plan therefore holds its position's only active slot with no
   live row anywhere to clear it — that position can never be planned for again.
3. **Approval ordering.** `ApproveAsync` cleared the predecessor's flag and raised the successor's
   in a *single* `SaveChanges`. Two rows may not both be active even mid-transaction, and when EF
   wrote the new row first the index rejected it.

Underneath all three sat **F-12**: `CreateAsync` set `IsActiveVersion = true` on a *draft*, so a
position with an approved plan could never receive a successor at all. That made the supersede
branch of `ApproveAsync` — the code that archives the predecessor and sets `SupersededByPlanId` —
**unreachable**, which is why `VersionNumber`, `SupersededByPlanId` and
`/position/{id}/versions` had never once had a second row to describe. Giving the branch a live
path is what exposed faces 2 and 3.

**The transferable lesson: a soft delete does not release a unique index.** Any counter, filtered
unique constraint, or "one active per X" rule in this codebase has the same latent shape. The
detection greps are `Count()` feeding a document number, and `HasFilter` on an index whose filter
omits `IsDeleted`.

### 3.10 ⚠ The detail read 500s the moment a plan is approved

SQL Server error 511: *"Cannot create a row of size 8067 which is greater than the allowable maximum
row size of 8060."* `GetWithFullDetailsAsync` joins six `Employee` navigations plus five collections
into one flat row, and there was **no `AsSplitQuery` anywhere** in `SuccessionPlanRepositories.cs`
across 174 `.Include(` calls. The row crosses 8060 bytes exactly when `ReviewedBy` and `ApprovedBy`
both fill in — so a plan becomes unreadable at the point it finishes its workflow.

The same 8060 shape area 8 met on create and area 12 fixed in its slice 0. Note why the §3.2 read
sweep missed it: the probe read a *draft*.

### 3.11 ⚠ The register shows a blank Position column

Every *filtered* list query on the repository (status, year, criticality, risk, due-for-review,
no-successors, incumbent, impending-vacancy) includes `Position`. The two *default* views did not —
`GetAllAsync` went through the generic repository and the paged read used a bare `GetQueryable()`.
Both map through the same summary mapper, whose `entity.Position?.Title ?? string.Empty` turns the
missing navigation into a blank rather than an error.

So the succession register — the first screen anyone opens — would have shown an empty Position
column on every row, beside filtered views that render it correctly. The position is the *subject*
of a succession plan. The summary DTO also carried no `PositionId`, so a row could not link to its
position.

### 3.12 ⚠ The assessment actor was a route to a forged recommendation

Measured 2026-08-18 while surveying slice 4 (`probe-candidates.mjs`). `AssessCandidateDto` carried a
**required** `AssessedById` and an `AssessmentDate`, both caller-declared. Posting an assessment
that named an unrelated employee stored it verbatim — `assessedByName` came back as an actor left
over from the *area-10* harness, someone who had never seen the candidate.

That is worse than the other caller-declared actors in §3.5, because the same call sets
`IsRecommended`, and `SelectCandidateAsync` refuses any candidate who is not recommended. So the
chain was: forge a recommendation in a colleague's name → select the candidate on the strength of
it → that candidate is now the named successor to the post. Two of the three steps look legitimate
in the audit trail.

Note how it was found. The endpoint returned 200 and stored a well-formed record every time; only
passing a *deliberately wrong* actor and then reading the row back showed it. **Posting the value
you expect proves nothing about whether the server was going to accept a value you did not.**

### 3.13 The candidate engine itself is healthy

Everything else in `probe-candidates.mjs` worked on the first attempt: nomination, the readiness and
retention-risk filters, `select` deselecting the previously selected candidate, feedback with a
resolved reviewer name, and the plan's rollup counters (`numberOfIdentifiedSuccessors`,
`hasReadyNowSuccessor`, `feedbackCount`/`supportCount`/`opposeCount`) all moving correctly. Unlike
the plan spine, this part needed hardening, not resurrection.

⚠ Two shapes worth knowing before building on it: the employee list is a **POST to
`/api/hr/Employees/paged`** — a GET 405s; and `bulk-rank` is a **PATCH taking a bare array**, not a
wrapper object.

---

## 4. Scope and boundaries — two calls needed before slice 1

**Mentoring belongs to area 7 and is already built.** `MentoringProgram` / `MentoringPair` /
`MentoringSession` DTOs live in `TrainingDTOs.cs`, and the screens exist at `/hr/training/mentoring`
and `/administration/hr/training/mentoring`. Recommendation: **leave it there**, and link to it from
a succession candidate's development view by reference. Same call as
[[she-training-ownership-boundary]].

**`api/talent-pool` is a different concept wearing the same word.** It injects
`ICandidateTalentSegmentService`, `IJobCandidateService` and `ICandidateEngagementEventService`, and
its entities (`CandidateTalentSegment`, `CandidateEngagementEvent`) live in `RecruitmentEntities.cs`.
It is a *candidate* CRM — segments, engagement events, vacancy matching — not the employee talent
pool of `api/talent-pools`. Area 6 already ships a third surface,
`api/recruitment-pipeline/talent-pool`, which the candidates screen uses.

Recommendation: **area 13 owns `api/talent-pools` (employees) only.** `api/talent-pool` is
recruitment's and should be folded into area 6's screens or retired against
`api/recruitment-pipeline/talent-pool` — record it, decide it, don't build it here. Naming that
collision in the UI matters: two screens both called "talent pool" would be a support problem.

---

## 5. Decisions needed

- **D-1 — gate design. ✅ SETTLED 2026-08-18: a new `HR.Succession.*` permission family**, mirroring
  Medical and Travel. Three rungs — `Read` / `Write` / `Admin`, Administer implying Write implying
  Read. The seeded `HR` role holds Read + Write; approving a plan, finalizing calibration, reading
  confidential documents, deleting anything, and administering talent pool types are Admin.
  Rationale: authoring a succession record is record-keeping, but **naming a successor to a post is
  a management act** — the same separation that stopped an HR-role user deleting a paid medical
  claim. Landed in slice 0.
- **D-2 — who may see a plan. ✅ SETTLED 2026-08-18: no self-access at all.** Succession inverts the
  self-service rule the rest of HR follows. A candidate's readiness level, retention-risk flag and
  nine-box placement are assessments made *about* them, not records belonging to them, so "self" is
  not a grant in this area and the `Employee` role receives nothing. Enforced in slice 0 and
  asserted by the harness (a plain actor cannot even read
  `/api/succession-plans/incumbent/{their own id}`). Any future "my development plan" screen must be
  fed by a separate, deliberately narrowed projection — never by relaxing `HrPermissions.RoleGrants`.
- **D-3 — approvals onto the workflow engine?** The bespoke Draft → UnderReview → Approved chain is
  the same shape travel had before slice 2. Recommendation: port it, per
  [[workflow-engine-integration]].
- **D-4 — nine-box source of truth.** `TalentReviewRating` carries performance and potential
  ratings that area 5 also computes. Is the review a manual calibration that may override the
  appraisal score, or a projection of it? Affects whether the grid is editable.
- **D-5 — Finance.** No money in this area on inspection. Confirm during slice 0 and record a nil
  entry in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` rather than leaving it unstated.

---

## 6. Findings register

| # | Finding | Severity | Status |
|---|---|---|---|
| F-01 | Area entirely ungated; a plain Employee created, read and deleted succession plans (§3.4) | **Critical** | **fixed, slice 0** |
| F-02 | `ReviewedById` / `ApprovedById` caller-declared on the approve path (§3.5) | **High** | **fixed, slice 1** |
| F-03 | ~~Duplicate-plan rule surfaces as an unexplained 500~~ — **misdiagnosed**, see F-09 | Medium | superseded |
| F-04 | Create response omits resolved `positionTitle` (§3.7) | Medium | **fixed, slice 2** |
| F-05 | `NominatedById` / `FacilitatedById` / `SnapshotCreatedById` caller-declared (§3.5) | Medium | partly — **`AssessedById` fixed, slice 4**; the rest belong to slices 6 and 7 |
| F-13 | A caller-declared assessor could manufacture a recommendation, which gates selection (§3.12) | **High** | **fixed, slice 4** |
| F-09 | **A soft delete does not release a unique index** — three faces, all 500s (§3.9) | **Critical** | **fixed, slice 2** |
| F-10 | The detail read 500s once a plan is approved — 8060-byte row (§3.10) | **Critical** | **fixed, slice 2** |
| F-11 | `positionTitle` blank on the default list reads while filtered views resolve it (§3.11) | **High** | **fixed, slice 2** |
| F-12 | Versioning had never worked; the supersede branch was unreachable (§3.9) | **High** | **fixed, slice 2** |
| F-06 | Two unrelated surfaces both named "talent pool" (§4) | Medium | open — needs D-1..D-2 |
| F-07 | Bespoke approval chain off the workflow engine (§3.5) | Low | open — needs D-3 |
| F-08 | `MentoringController` and `TalentPoolController` are still bare `[Authorize]` (§4) | Medium | **W3 sweep** — see below |

⚠ **F-08 is deliberately not fixed here.** Both controllers were measured ungated alongside the
other seven, but they belong to closed areas (7 training, 6 recruitment) that have no permission
family yet, and inventing `HR.Training.*` / `HR.Recruitment.*` for two controllers would pre-empt
the W3 sweep's design. They are recorded so the sweep starts with two known-ungated surfaces
already identified rather than rediscovering them.

---

## 7. Cross-module seams

- **← Area 5 performance.** Live and sound (§3.8). Also `AppraisalSettings.SuccessionPoolName` and
  `SuccessionDefaultReadiness` are already editable in the built settings screen.
- **← Area 8 movements.** `StaffMovement.SuccessionPlanId` and the `successionPlanId` /
  `successionPlanNumber` fields in `types/hr/movements.ts` were wired reference-only, awaiting this
  area. Closing that seam is a slice.
- **→ Area 17 competency.** `/api/succession-plans/competency-lookup` returns empty because no
  competencies exist yet. Succession competency requirements will stay hollow until area 17 lands —
  build the UI against the lookup and accept an empty list for now.
- **↔ Area 6 recruitment.** The `api/talent-pool` collision (§4).
- **Positions.** The area keys off `api/EmployeePositions` (146 rows). Note the area-8 lesson:
  `ExpectedHeadcount` is unmaintained, so any "critical position without cover" rollup must not
  silently depend on establishment data.

---

## 8. Proposed slices

| # | Slice | Notes |
|---|---|---|
| 0 | Gate the area | D-1/D-2 first. Permission family, seed, role fallback, re-test §3.4 as refusals. Fixes F-01. |
| 1 | Actor integrity | Token-derived actors on review/approve/nominate/facilitate. Fixes F-02, F-05. |
| 2 | Spine hardening | Duplicate rule → 409 with a message; resolved names on write responses. Fixes F-03, F-04. |
| 3 | Plans UI | Register, create, detail. Build the create form early — it is an actor audit. |
| 4 | Candidates & readiness | Nomination, readiness, gaps, feedback; the confidentiality rule from D-2. |
| 5 | Development activities | Milestones, overdue rollups; link to area 7 mentoring by reference. |
| 6 | Talent pools | Employee pools + members; prove the area-5 nomination join end-to-end (§3.8). |
| 7 | Talent reviews & nine-box | Calibration, finalize, the grid. Depends on D-4. |
| 8 | Approvals onto the workflow engine | Depends on D-3. Retire the bespoke chain. |
| 9 | Dashboard, search, movements seam | Coverage/risk heatmap, candidate fit, close the area-8 seam. |
| 10 | Content audit + UI payload probe | Per the area-11 and area-12 lessons — both are mandatory, neither is optional. |

Slice count is provisional; areas 11 and 12 both grew by two.

---

## 9. Slice log

### Slice 0 — gate the area (2026-08-18)

Closes **F-01**. Settles **D-1** and **D-2**.

- `HrPermissions.cs` — added the `HR.Succession.*` family: three permissions, three policies, a new
  `CategorySuccession`, and `ViewSuccession` + `MaintainSuccession` onto `HrStaffGrants`. The seed
  (`DatabaseSeedingService`) and the role fallback both read `All` / `RoleGrants` generically, so no
  further wiring was needed and tenants provisioned before the seed keep working.
- `ServiceCollectionExtensions.cs` — registered the three policies on the same
  Administer→Write→Read ladder as medical and travel.
- Seven controllers re-gated: **7 class-level read gates** (replacing the bare `[Authorize]`),
  **36 write gates**, **23 admin gates**. Every `[HttpDelete]` is Admin; so are plan review, plan
  approve, review finalize, rating calibration confirm, confidential documents, and all three
  talent-pool-type writes. `candidate-search` is a POST but a query, so it sits on Read.
- Harness: `run-slice0.mjs`. Re-runs each of the six calls §3.4 measured as succeeding for a plain
  Employee and requires a 403, spot-checks four more controllers so the gate is not just on plans,
  asserts D-2 by refusing a plain actor their own `incumbent/{id}` list, and walks the HR/TenantAdmin
  split through a real submit → review → approve → delete.

⚠ Not fixed here: **F-08** (see the register).

**Result: `run-slice0.mjs` — 32 passed, 0 failed.**

### Slices 1 and 2 — actor integrity and the write spine (2026-08-18)

Closes **F-02**, **F-04**, **F-09**, **F-10**, **F-11**, **F-12**. Supersedes **F-03**.
Harness `run-slice12.mjs` — **27 passed, 0 failed**.

Slice 1 — the actor comes from the token:
- `ReviewedById`, `ApprovedById`, `ReviewDate` and `ApprovalDate` **removed from the DTOs**; the
  actor is `_currentUser.EmployeeId` and the date is the clock. The harness sends `approvedById` on
  the body anyway and asserts it is *not* honoured.

Slice 2 — the write spine:
- **Numbering** derives from the highest sequence issued this year, counting deleted rows via
  `GetQueryableIncludingDeleted`. Numbers are never reused.
- **Drafts no longer claim the active-version slot**; approval raises it, which is what "active
  version" means. This is the fix that made versioning reachable.
- **Delete** stands `IsActiveVersion` down, so a soft-deleted plan stops holding its position.
- **Approval** clears whatever holds the slot — *including soft-deleted rows*, which the delete fix
  cannot reach retroactively — and saves that **before** raising the successor's flag. A deleted row
  is released silently rather than marked superseded; it was never anyone's predecessor.
- **`SuccessionConflictException`** (new, `ErpSystem.Core/Exceptions/`) → 409 with the message
  preserved, following the `MedicalWorkflowException` precedent. The in-progress rule now names the
  plan that blocks it.
- **`AsSplitQuery` on 18 queries** (142 includes) in `SuccessionPlanRepositories.cs`; chains with
  `Skip`/`Take` were skipped, since split queries need a stable order.
- **Create response** re-reads through the detail loader, so it carries resolved names.
- **A shared `SummaryQuery`** feeds both default list views, so they cannot drift from the filtered
  ones again; `PositionId` added to the summary DTO.

⚠ **Three harness lessons, all worth carrying:**
1. It tried to delete as `HR` and got a 403. **The gate was right and the harness was wrong** — a
   failing assertion is not automatically a failing product.
2. It picked positions by **fixed index**, so a plan left behind by a failed run blocked the next
   run with the very 409 the harness exists to assert. Both harnesses now select positions holding
   no plan in progress.
3. It sent `reviewedById` and `comments`, neither of which the DTOs have — **field names invented
   from the endpoint's name**, the area-12 lesson biting inside the harness rather than the UI.

### Slice 3 — plans UI (2026-08-18)

Harness `run-slice3-ui.mjs` — **49 passed, 0 failed**. `tsc` clean (the only errors in the tree are
pre-existing inventory ones), `eslint` clean.

Screens, all under `/hr/succession` (operational, not Administration — a succession plan is
transactional work, not reference data):

| Route | What it is |
|---|---|
| `/hr/succession` | Register, with five quick views: all, no successors, nobody ready now, due for review, vacancy expected |
| `/hr/succession/new` | Create — a draft |
| `/hr/succession/[id]` | Detail: overview, successors, competencies, actions, documents |
| `/hr/succession/[id]/edit` | Edit, replaced by an explanation once the plan is approved |

Plus `types/hr/succession.ts`, `services/hr/succession.service.ts`,
`components/hr/succession/SuccessionPlanForm.tsx`, and a sidebar entry. **No "my succession" entry,
and there must never be one** — D-2 is enforced in the navigation as well as the gate.

⚠ **Writing the types from the DTOs rather than the endpoint names caught six inventions** that
would all have compiled: the candidate summary has `currentReadiness`, not `readiness`; it has no
`successionPlanId` and no `overallFitScore`; `PositionCoverageRow` is keyed by `planId` despite the
name; competency requirements carry a `proficiencyScaleMax` so the scale is **not** assumed to be 5;
and `CompetencyLookupDto` has `code`/`competencyCategory`, not a `category` string. Every one was a
guess I had written down before reading the C#.

⚠ **The empty-string date trap is real here, and measured:** posting `targetSuccessionDate: ""`
returns **400**. The form's `orNull` helper is load-bearing, not defensive decoration.

⚠ **The three-rung ladder is visible in the UI.** An HR officer can open a plan and see the Approve
button, then be refused — correctly, because approval is Admin. The detail page surfaces the 403
with the server's message rather than swallowing it, and says plainly that the decision sits with a
tenant administrator. A screen must not hide a button on the strength of being able to read the
record.

### Slice 4 — candidates and readiness (2026-08-18)

Closes **F-13**, and the succession-plan half of **F-05**. Harness `run-slice4.mjs` —
**34 passed, 0 failed**. All four harnesses green together: 32 + 27 + 49 + 34 = **142 assertions**.
`tsc` and `eslint` clean.

Backend: `AssessedById` and `AssessmentDate` removed from `AssessCandidateDto`; the assessor is the
token and the date is the clock. The harness proves both by sending an unrelated employee **and** a
backdated `2020-01-01`, then asserting neither was honoured.

Frontend: `components/hr/succession/CandidatesPanel.tsx` replaces the read-only candidates tab —
nominate, re-rank (neighbour swap, one PATCH carrying both rows), assess, select, and per-candidate
feedback with its dispositions.

⚠ **Two UI decisions that encode a backend rule rather than duplicating it.** The Select button is
*disabled with an explaining tooltip* when a candidate is not recommended, because the server
refuses with a bare 400 and a user cannot tell a missing precondition from a broken button. And the
assess dialog states outright that there is no field to name a different assessor — an absence that
looks like an omission unless it is labelled as a decision.

⚠ **A harness lesson, third of its kind:** `run-slice12.mjs` asserted `versionNumber === 2` and
failed with `got 10`, because version numbers count every plan a position has ever carried and
earlier runs had left nine behind. The product was right. **An assertion on an absolute count is an
assumption that the fixture is pristine** — assert the delta instead. Same root as the fixed-index
position picker two slices ago.
