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

### 3.14 ⚠ Money hiding one level down, in a currency that did not exist

Measured 2026-08-18 (`probe-development.mjs`). `currencyCode: "ZZZ"` was **accepted and stored** on
a development activity. The only thing standing between that field and nonsense was
`MaxLength(3)` — `"banana"` was rejected for being four characters long, not for being imaginary.
A development budget denominated in a currency Finance has never heard of cannot be totalled or
reported against.

⚠ **And there are two doors into this entity.** `SuccessionDevelopmentActivityService` has
create/update, and `SuccessionCandidateService` has its own `AddDevelopmentActivity` /
`UpdateDevelopmentActivity` for the same rows. Guarding one would have left the other open. I found
the duplication only because my first edit landed in the wrong service — the two methods are nearly
identical. **Before validating a write, count the write paths.**

### 3.15 Development activities otherwise run clean

Everything else worked first time: create, the by-candidate and by-status reads, milestones, the
overdue-milestone queue, and completion stamping its date. Two behaviours worth recording rather
than "fixing":

- **Completing every milestone does not advance the activity's status** — see D-6.
- **`generate-from-position` returns `[]`** and will until area 17 lands. Empty is the correct
  answer, not a failure; the screen says so.

### 3.16 ⚠ Two more faces of the same disagreement, in talent pools

**A count derived from an unloaded navigation is silently zero — and zero looks like a fact.**
`GET /api/talent-pools/{id}` used a loader with no includes, and the mapper computes
`CurrentMemberCount` from `entity.Members`. A pool with one member reported **0 members, no type and
no owner**, while `with-members` sitting beside it reported all three correctly. The blank strings
are the familiar stale-nav shape; the *number* is worse, because a manager reading "0 members" does
not investigate, they conclude the pool is empty.

**Once removed from a pool, an employee could never rejoin it.** `RemoveMemberAsync` is a soft
removal — `IsActive = false`, row stays — and the duplicate check filters on `IsActive`, so it
passes. But `IX_TalentPoolMember_Tenant_Pool_Employee` is unique on `(TenantId, PoolId, EmployeeId)`
with **no filter**, so the insert hit the index and 500'd. Permanently, from an ordinary Remove.

That is the **fifth** instance in this area of one root cause: *the code's idea of "exists" and the
schema's disagree*. Plan numbering, the active-version slot, approval write-ordering, pool
membership, and the soft-deleted-membership variant underneath it. Re-joining now revives the
existing row, which is also the better record — it keeps the original enrolment and why they left.

### 3.17 ✅ The area-5 join runs — first execution ever

Driven end to end in `probe-area5-join.mjs` and asserted in `run-slice6.mjs`. Approving an appraisal
outcome recommendation of type `SuccessionNomination` returns status **`Actioned`** — the
controller documents `Approved`-but-not-`Actioned` as dispatch failure — with
`targetEntityType: TalentPoolMember`. The handler find-or-created the **"Appraisal Nominations"**
pool from `AppraisalSettings.SuccessionPoolName`, enrolled the employee at the configured default
readiness, attributed the nomination to whoever approved the recommendation, carried the notes
across, and proved idempotent on a repeat.

⚠ **Sound-on-inspection is not the same as executed.** This handler read as correct code for as long
as the area has existed and had never once run.

⚠ Two things learned driving it: area 5 gates on the **`HR` role by name**, so a `TenantAdmin` actor
is refused with "Only this employee's manager, or HR, can propose an outcome"; and
`/api/PerformanceAppraisals` **ignores `pageNumber`/`pageSize` and returns a bare array of all
4,328 rows**. The second is area 5's to fix, recorded here so the next person does not assume
`.items`.

### 3.18 ⚠ The finalize endpoint was dead, and it names the rule exactly

`FinalizeTalentReviewSessionDto.FinalizedById` was a **required** Guid the client had no way to
know. Omitting it sent `Guid.Empty`; the save died on
`FK_TalentReviewSessions_Employees_FinalizedById` with a 500. So the endpoint that closes a
calibration session could not be called at all — not merely spoofable, **unusable**. Confirming
calibration had the same shape and additionally accepted a backdated `2020-01-01`, stored verbatim.

This is the clearest statement of the rule the area kept rediscovering, and slice 7 wrote the line
down in the code:

> **An act performed by the caller at the moment of the call comes from the token. A fact about
> someone else does not.**

By that test `FinalizedById` and `ConfirmedById` had to go, while `FacilitatedById` (who chaired the
meeting) and `RatedById` (which manager gave the score) **stay caller-set** — a desk may legitimately
record either on someone else's behalf. The line is not "every Guid ending in Id".

### 3.19 ⚠ Half the freeze was missing — and a correction

`AddRatingAsync` already refused a finalized session. `UpdateRatingAsync` and `DeleteRatingAsync`
checked only `CalibrationConfirmed`, so an **uncalibrated** rating inside a closed session stayed
editable — which is the half that matters, since that is exactly the row someone would be tempted to
tidy up after the meeting.

> ⚠ **A correction to my own probe.** It reported "a finalized session can still be edited", which
> was wrong: the session had never finalized, because finalize itself was 500ing. **When a probe
> reports two defects at once, check whether the first one invalidates the second's premise.**

### 3.20 ⚠ "Latest confirmed" was non-deterministic

`GetLatestConfirmedRatingForEmployeeAsync` ordered by `Session.SessionDate` alone. Two sessions held
on the same day tie, and the winner was whatever the database returned first. Not cosmetic: this
value becomes the employee's cached rating on their talent pool member and the *previous placement*
a later session shows as their trend — so someone's trend could change between two reads with no
data having changed.

⚠ Found because the same harness **passed and then failed on identical input**. I had already
dismissed one failure of that assertion as my own fixture assumption and "fixed" the harness. Running
it twice is what proved the second failure was the product. **A test that passes and fails on
identical input is data, not noise.**

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
- **D-4 — nine-box source of truth. ✅ SETTLED 2026-08-18 (slice 7): a manual calibration that
  pre-fills Performance from the latest scored appraisal and lets the rater override it.**
  The premise of the question was half wrong: area 5 does **not** compute both ratings.
  `PotentialRating` exists nowhere outside `SuccessionPlanningEntities.cs` — appraisals carry an
  `OverallScore` and nothing about potential — so **one axis of the grid has no source in area 5 at
  all** and the nine box could never have been a projection. Performance is suggested via
  `GET /api/talent-reviews/rating-suggestion/{employeeId}`, which names the appraisal it came from;
  moving away from it prompts for a justification, because a calibration session exists to disagree
  with the paperwork and the disagreement is the part worth recording.
- **D-5 — Finance. ❌ WRONG WHEN WRITTEN, corrected 2026-08-18 in slice 5.** It said "no money in
  this area on inspection". There is money: `EstimatedCost`, `ActualCost` and `CurrencyCode` on
  **`SuccessionDevelopmentActivity`**. The survey missed it because it looked at the plan and the
  candidate — **the cost lives where the work happens, not where the record is filed**. Both events
  are now registered in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, along with an overlap the sweep
  must settle: a `Training`-type activity describes the same spend area 7 budgets through
  `TrainingBudget`, so a successor's course could be counted twice.
- **D-6 — does completing every milestone finish its activity? NO, deliberately (slice 5).**
  Measured: it does not, and I left it that way. Whether an activity is finished is a supervisor's
  judgement, not arithmetic over its milestones — a secondment whose checkpoints are all ticked may
  still be running. Auto-advancing would be inventing TDC's policy. The UI states the non-change in
  its toast, because an unexplained absence of a status change reads as a bug. Revisit if TDC says
  otherwise.

---

## 6. Findings register

| # | Finding | Severity | Status |
|---|---|---|---|
| F-01 | Area entirely ungated; a plain Employee created, read and deleted succession plans (§3.4) | **Critical** | **fixed, slice 0** |
| F-02 | `ReviewedById` / `ApprovedById` caller-declared on the approve path (§3.5) | **High** | **fixed, slice 1** |
| F-03 | ~~Duplicate-plan rule surfaces as an unexplained 500~~ — **misdiagnosed**, see F-09 | Medium | superseded |
| F-04 | Create response omits resolved `positionTitle` (§3.7) | Medium | **fixed, slice 2** |
| F-05 | Caller-declared actors across the area (§3.5) | Medium | **closed** — `AssessedById` s4, `NominatedById` s6, `FinalizedById`+`ConfirmedById` s7. `FacilitatedById` / `RatedById` **kept** by design, see §3.18 |
| F-13 | A caller-declared assessor could manufacture a recommendation, which gates selection (§3.12) | **High** | **fixed, slice 4** |
| F-09 | **A soft delete does not release a unique index** — three faces, all 500s (§3.9) | **Critical** | **fixed, slice 2** |
| F-10 | The detail read 500s once a plan is approved — 8060-byte row (§3.10) | **Critical** | **fixed, slice 2** |
| F-11 | `positionTitle` blank on the default list reads while filtered views resolve it (§3.11) | **High** | **fixed, slice 2** |
| F-12 | Versioning had never worked; the supersede branch was unreachable (§3.9) | **High** | **fixed, slice 2** |
| F-06 | Two unrelated surfaces both named "talent pool" (§4) | Medium | open — needs D-1..D-2 |
| F-07 | Bespoke approval chain off the workflow engine (§3.5) | Low | open — needs D-3 |
| F-08 | `MentoringController` and `TalentPoolController` are still bare `[Authorize]` (§4) | Medium | **W3 sweep** — see below |
| F-14 | `currencyCode: "ZZZ"` accepted and stored; nothing checked it against Finance (§3.14) | **High** | **fixed, slice 5** |
| F-15 | Activity create/update responses omit resolved names — the stale-nav shape, third time | Medium | **fixed, slice 5** |
| F-16 | Activity ownership rules threw `ArgumentException`, whose message the middleware discards | Medium | **fixed, slice 5** |
| F-17 | Pool detail reported **0 members** for a pool with members — a count from an unloaded nav (§3.16) | **High** | **fixed, slice 6** |
| F-18 | Once removed from a pool an employee could **never rejoin it** — unique index vs soft removal (§3.16) | **High** | **fixed, slice 6** |
| F-19 | `NominatedById` caller-declared on pool membership | Medium | **fixed, slice 6** |
| F-20 | **`finalize` was dead** — a required, unknowable `FinalizedById` sent `Guid.Empty` and 500'd on an FK (§3.18) | **Critical** | **fixed, slice 7** |
| F-21 | Calibration confirm honoured a caller-named confirmer and a backdated `2020-01-01` (§3.18) | **High** | **fixed, slice 7** |
| F-22 | A finalized session's **uncalibrated** ratings stayed editable (§3.19) | Medium | **fixed, slice 7** |
| F-23 | `latest-confirmed` ordered by session date **with no tie-break** — non-deterministic (§3.20) | Medium | **fixed, slice 7** |
| F-24 | Review session detail and rating writes returned blank names — the wrong-loader mistake again | Medium | **fixed, slice 7** |

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

### Slice 5 — development activities and milestones (2026-08-18)

Closes **F-14**, **F-15**, **F-16**. Settles **D-6**, and corrects **D-5**. Harness
`run-slice5.mjs` — **36 passed, 0 failed**. All five green: 32 + 27 + 49 + 34 + 36 =
**178 assertions**. `tsc` and `eslint` clean.

Backend:
- `SuccessionCurrencyGuard` — a single static guard called from **both** write paths, validating
  `CurrencyCode` against Finance's `ICurrencyService`. Read-only, like travel's bridge; a missing
  code is fixed in Finance, not invented here. A cost with no currency is also refused.
- `SuccessionValidationException` → **400 with the message preserved**. The ownership rules threw
  `ArgumentException`, which the middleware rewrites to "Invalid argument provided." — a rule that
  fires correctly but cannot say what to change is barely a rule.
- Activity create/update now re-read through the details loader, so responses carry
  `candidateEmployeeName` and `supervisorName`. Third instance of the stale-nav shape in this area.

Frontend: `DevelopmentPanel.tsx`, opened per candidate from the candidates table. Activities with
their costs in the row's own currency, milestones with an overdue marker, and a currency picker fed
from **Finance's list** rather than a free-text box — offering a text box would be offering a way to
fail. A `Mentoring`-type activity links to `/hr/training/mentoring` and says the programme lives
there: the §4 boundary call made visible in the UI rather than only recorded in this document.

⚠ **My own mistake, worth keeping.** Removing a method by slicing from its doc comment to the next
occurrence of `CreateAsync(` deleted **seven read methods** that sat in between, and the build caught
it as seven missing interface members. A text-range delete anchored on a pattern that recurs is not
a delete, it is a gamble. Recovered with `git show HEAD:<file>`, which is why committing each slice
before starting the next one is worth the ceremony.

### Slice 6 — talent pools and the area-5 join (2026-08-18)

Closes **F-17**, **F-18**, **F-19**, and the succession side of **F-05**. Harness `run-slice6.mjs` —
**44 passed, 0 failed**. All six green: 32 + 27 + 49 + 34 + 36 + 44 = **222 assertions**. `tsc` and
`eslint` clean.

Backend:
- Pool detail loads its members, so `currentMemberCount` is real; create/update/add-member re-read
  so names resolve, including `talentPoolTypeName`, which is **two hops out** and needed a
  `ThenInclude` the member loader did not have.
- Re-joining a pool revives the existing membership instead of inserting past a unique index; the
  lookup deliberately includes soft-deleted rows, because `GetMembershipAsync` filters them out
  while the index does not.
- The nominator comes from the token. Area 5's handler is the one legitimate exception — it writes
  `TalentPoolMember` directly and sets the nominator to whoever approved the recommendation.
- The duplicate-member rule returns **409 naming the pool** instead of the middleware's
  "The operation is not valid for the current state of the object."

Frontend: `/hr/succession/pools` register, `[id]` detail with member management, and two dialogs.
The register states plainly that area 5 adds members on its own — a screen whose contents can change
without anyone using it should say so.

⚠ **The harness caught itself again, and it is the same mistake a third time.** It asserted the
nominator was *this run's* actor, but the handler is idempotent and returned a membership created by
an earlier probe run, nominated by that run's actor. The product was right. It now picks an appraisal
whose employee is not already pooled. **Absolute assertions against a fixture you did not create
will fail eventually** — the same root as `versionNumber === 2` and the fixed-index positions.

### Slice 7 — talent reviews, calibration and the nine box (2026-08-18)

Closes **F-20**, **F-21**, **F-22**, **F-23**, **F-24**, and the last of **F-05**. Settles **D-4**.
Harness `run-slice7.mjs` — **46 passed, 0 failed**, and run three times consecutively to prove the
tie-break. All seven green: 32 + 27 + 49 + 34 + 36 + 44 + 46 = **268 assertions**. `tsc` and
`eslint` clean.

Backend: finalize and confirm-calibration take their actor from the token and their date from the
clock; the session-level freeze extended to rating update and delete; `latest-confirmed` given a
deterministic tie-break; session detail and rating writes moved onto the loaders that resolve names;
`GET /api/talent-reviews/rating-suggestion/{employeeId}` added for D-4.

Frontend: `/hr/succession/reviews` register, `[id]` detail with the grid and a ratings table, plus
`NineBoxGrid`, `TalentReviewFormDialog` and `TalentRatingDialog`.

⚠ **A UI decision worth recording: performance has five values and the grid has three columns.**
`Unsatisfactory` folds into the left column and `Outstanding` into the right. Without that fold, a
rating at either extreme would **vanish from the grid** — the worst possible failure for the one
screen whose entire job is showing where people sit. Cell counts include the folded rows.
