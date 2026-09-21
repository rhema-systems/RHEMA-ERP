# HR Area 14 — Staff Awards & Recognition: Build Plan

**Opened 2026-08-21. ✅ COMPLETE 2026-08-22 — 14 slices, 618 assertions, 29 defects, 17 screens.** Area chosen by the user after the post-9b survey. The area is unusual in
this module: it inherits ~4,950 lines of ported code that has **never once executed**, and its
requirements come almost entirely from a TDC change document rather than from the FRD.

---

## 1. How to use this document

Read **section 3 (ground truth)** and **section 4 (the change document)** before writing any code.
Section 3 is measured, not assumed — every number in it came from the live DEFAULT tenant or from
the source on 2026-08-21. Section 4 is the requirement source; the FRD contributes exactly one
requirement to this area.

Section 8 is the running log. Each slice appends its own entry with its assertion count, what it
found, and what it changed. Do not edit an earlier slice's entry to make it agree with a later
decision — record the change where it happened.

---

## 2. Status at a glance

| | |
|---|---|
| **Area** | 14 — Staff Awards & Recognition |
| **Branch** | `hrdev` |
| **FRD requirements** | **FR-HR-113** (M) — "report on long-service-award eligibility". That is all of them. |
| **Primary requirement source** | `D:\Rhema\TDC ERPS\Staff Awards Changes.pdf` — see section 4 |
| **Backend today** | 15 entities, 100 endpoints, 1,852 lines of service, 1,355 lines of DTOs |
| **Backend proven** | *(at survey)* **Nothing.** Every awards table holds 0 rows |
| **Frontend today** | *(at survey)* none |
| **Status** | ✅ **COMPLETE 2026-08-22.** 14 slices, **618 assertions**, 29 defects, **17 screens** |
| **Harness** | `dev-harness/hr-awards/` — `run-slice0..12`, plus `probe-ui-payloads.mjs` |
| **Frontend** | **17 screens** across `/hr/awards`, `/hr/awards/me` and `/administration/hr/awards`, registered in all three navigation surfaces. Every meaningful write reachable; the attachment screens are deliberately absent (see the open defect) |
| **Owed to TDC** | what a long-service rung is worth (AWD-14); whether severity gates the disciplinary exemption (AWD-15, needs a schema change to discipline first) |

---

## 3. Ground truth — measured 2026-08-21

### 3.1 What exists

| File | Lines |
|---|---|
| `src/ErpSystem.Core/Entities/HR/AwardsEntities.cs` | 531 |
| `src/ErpSystem.Core/Services/HR/AwardsServices.cs` | 1,852 |
| `src/ErpSystem.Core/DTOs/HR/AwardsDTOs.cs` | 1,355 |
| `src/ErpSystem.Api/Controllers/HR/AwardsController.cs` | 993 |
| `src/ErpSystem.Core/Interfaces/HR/IAwardsServices.cs` | 212 |
| `src/ErpSystem.Data/Seeders/AwardDataSeeder.cs` | 428 |

**Fifteen entities**: `AwardType`, `AwardLevel`, `AwardTypeTarget`, `AwardBudget`,
`AwardNomination`, `AwardNomineeContribution`, `AwardNominationAttachment`, `TeamAwardNominee`,
`AwardCommittee`, `AwardCommitteeMember`, `AwardNominationReview`, `EmployeeAward`,
`TeamAwardRecipient`, `AwardAttachment`, `LongServiceAward`.

**One hundred endpoints** on `AwardsController`, in eight groups: types/levels/targets/budgets ·
employee awards · award attachments · nominations (+ team nominees, contributions, attachments) ·
committees (+ members) · reviews · long service.

**The parts of the model that are genuinely useful** and should be kept rather than redesigned:

- `AwardType` already carries the eligibility vocabulary the change document asks HR to configure:
  `MinServiceYears` / `MaxServiceYears`, `MinAge` / `MaxAge`, `MaxAwardsPerPeriod`,
  `MaxAwardsPerEmployee`, `RequiresFormalReview`, `MinRequiredReviewers`, `AutoGenerateNominees`,
  `IsTeamAward`, `HasLevels`, and the reward configuration (`HasMonetaryReward`, min/max amount,
  `HasCertificate`, `HasTrophy`, `LeaveDaysBonus`).
- `AwardTypeTarget` scopes an award to units/positions with `IsExclusion` **and** effective dating
  (`EffectiveFrom` / `EffectiveTo`). This is the right shape for both *who may win* and — reused —
  *who may vote*.
- `LongServiceAward` already models the milestone, the monetary amount, a leave-days bonus, other
  benefits, payment tracking, **and leave processing** (`LeaveProcessed`, `LeaveProcessedDate`,
  `LeaveId` — a hook into the leave module that nothing writes).
- An eligibility endpoint already exists: `GET types/{awardTypeId}/eligibility/{employeeId}`.

### 3.2 The measured holes

**1 — Nothing had ever run.** Every table was empty on DEFAULT before slice 0:

```
AwardTypes 0 · AwardNominations 0 · EmployeeAwards 0 · LongServiceAwards 0
```

**Slice 0 has now run the happy path, and the news is better than the row counts implied** — see
the log. The configuration chain, the nomination chain, committees, conferring an award and the
long-service chain all execute once given correct payloads. This is a *harden-and-extend* area,
not a resurrection. But treat each endpoint as unproven until a slice asserts its content: the
area-11 lesson stands, and a status-code harness proves the gate, not the feature.

**2 — The identity model is caller-supplied. Slice 0 measured exactly how bad, and it is not what
the parameter count suggests.** `AwardsController` does not extend `HrControllerBase`; it is a
plain `ControllerBase` on bare `[Authorize]`, with **24 × `[FromQuery] Guid tenantId`** and
**32 × `[FromQuery] Guid userId`** across the 100 endpoints. Probed on the running API:

- **Tenant is NOT open.** A guard rejects a mismatched tenant before the query runs:
  `GET types?tenantId=<other tenant>` → **403, "The supplied tenant does not match the
  authenticated tenant."** Same for a nonsense GUID. The 24 parameters are redundant noise that
  every caller must supply correctly, not a cross-tenant hole. Remove them for the sake of the
  callers, not for security.
- **The actor IS open, and it is confirmed by exploit.** `POST nominations` takes a separate
  `[FromQuery] Guid nominatedById`. Slice 0 created a nomination attributed to a colleague who had
  nothing to do with it, from an admin session. Attribution across this area is whatever the caller
  types.

The actor fix is slice 1. The tenant-parameter removal rides along with it as tidying.

**3 — There is no voting model at all.** `grep -rln "AwardVote|Ballot|VoteCount|CastVote" src`
returns nothing. The change document's central mechanism does not exist.

**4 — Committee review is approve/reject, not scoring.**

```csharp
public class AwardNominationReview : TenantEntity
{
    public Guid AwardNominationId { get; set; }
    public Guid ReviewerId { get; set; }
    public bool? Approved { get; set; } // Vote: Yes/No
    public string? Comments { get; set; }
    public DateTime? ReviewDate { get; set; }
}
```

The document requires members to **score**, with the winner decided on **highest average**. Two
existing endpoints — `reviews/approval-count` and `reviews/rejection-count` — encode the wrong
model and will have to go. With zero rows in the table this is a free change; it will not be free
later.

**5 — The seeder is deferred on purpose, and the reason is TDC's data.** `AwardDataSeeder` is
listed in `HrSeedOrchestrator.DeferredSteps` with this recorded reason:

> *"Still carry generic sample data from the standalone HR solution. The TDC HR questionnaire
> supplies real values … these should be rewritten against it before being seeded."*

So the empty tables are a decision, not an oversight. Somebody declined to seed fictional award
types. **Do not simply enable this seeder** — rewrite it against TDC's catalogue, or leave it
deferred and seed only what the harness needs.

**6 — FR-HR-113 has no live subjects.** Measured on DEFAULT:

| | |
|---|---|
| Employees, live | **5,579** |
| … carrying a `DateEmployed` | **2,103 (38%)** |
| … with 10+ years' service | **1** |
| … with 15, 20 or 25 years | **0** |

The long-service engine must therefore be built against fixtures and its **live sweep asserted
empty deliberately** — exactly as area 9b did with retirement at 60. And the 38% `DateEmployed`
coverage is a TDC data item: a long-service report is silently wrong for the other 62%, who look
like they have no service at all. This is the fourth appearance of the unmaintained-column shape
after `ExpectedHeadcount`, `HeadEmployeeId` and `ManagerId`.

**7 — But the voting population is real.** The one measurement that came back healthy:

| | |
|---|---|
| Employees, live | 5,579 |
| Users linked to an employee (`Users.EmployeeId`) | **5,520 (99%)** |

Staff voting is buildable against real people. Note the known frontend gap from area 15b: the
client-side `User` object carries **no** employee link, so every self-service route must resolve
the actor server-side from the token, as `StaffTravelMeController` and the medical self-service
surface already do.

### 3.3 Defects to fix in this area, not to leave

- The 56 caller-supplied identity parameters (hole 2).
- `AwardsController` on bare `[Authorize]` — it needs `HR.Awards.*` policies plus a self-or-HR
  check, per the house authz pattern.
- `reviews/approval-count` / `reviews/rejection-count` — wrong model, remove with hole 4.
- `ProcessAwardPayment` and `LongServiceAward.MonetaryAmount` are **money events**. They go in
  `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` for the post-module sweep. Do **not** invent an HR-side
  posting mechanism.

---

## 4. The change document — requirement extraction

Source: `D:\Rhema\TDC ERPS\Staff Awards Changes.pdf`, three sections. It is written as discussion
notes, so the requirements below are the extraction; each is given an ID so slices can cite it.
**Where the extraction interprets rather than quotes, section 5 records the decision.**

### 4.1 Nomination

| ID | Requirement | Exists? (at survey) | Delivered |
|---|---|---|---|
| **AWD-01** | HR sets up the eligibility criteria **first**; the criteria then qualify a set of employees | ✅ model exists (`AwardType` + `AwardTypeTarget`), never executed | ✅ slice 3 — six criteria were applied by nothing; now an evaluator that returns the qualified set **and the reasons anyone else failed** |
| **AWD-02** | Employees **or** management nominate — including employees nominating managers | ⚠ `NominatedById` exists; no employee-facing route, and the id is caller-supplied | ✅ slice 1 removed 60 forgeable ids; slice 4 built the employee route |
| **AWD-03** | Employees nominate **through the staff portal** | ❌ no self-service surface for awards | ✅ slice 4 `api/awards/me`; slice 11 the screen |
| **AWD-04** | Qualified/shortlisted nominees are **displayed on the portal** for voting | ❌ | ✅ slice 5 ballot |
| **AWD-05** | Staff **vote** on the nominees; the winner is decided by the vote | ❌ **nothing exists** | ✅ slice 5 — one vote per voter, results withheld until close, ties reported not broken |
| **AWD-06** | The electorate is configurable — "a section of the employees **or all of them**" | ❌ | ✅ slice 5 — an award with no electorate target is voted on by everyone, which is the "or all of them" half |
| **AWD-07** | **Not every award goes to a vote.** Some are a direct management selection | ❌ no selection-method discriminator | ✅ slices 2 + 8 — D-3's two independent axes, and direct conferral restricted to `ManagementDirect` |
| **AWD-08** | Some nominations arise from **performance or a target reached** | ⚠ `AutoGenerateNominees` flag exists, no engine | ✅ slice 7 — the flag was replaced by real triggers; the result reports every count including the zeros |
| **AWD-09** | A nominator states a **justification or reason** | ✅ `AwardNomination.Justification` | ✅ already present, asserted slice 4 |
| **AWD-10** | A **voter** may also state a justification or reason | ❌ part of the missing vote model | ✅ slice 5 |
| **AWD-11** | Awards are **listed on the portal for nomination before voting opens** — i.e. a nomination window, then a voting window | ❌ no dated cycle; only `Year`/`Quarter`/`Month` ints on the nomination | ✅ slice 3 — `AwardCycle` with both windows, and voting cannot open before nominations close |

### 4.2 Award committee

| ID | Requirement | Exists? (at survey) | Delivered |
|---|---|---|---|
| **AWD-12** | Committee members **score** a nomination | ❌ `Approved` is `bool?` | ✅ slice 6 — `Approved` became `Score`; the desk's score-write endpoints were **removed**, because a member scores from their own surface and never on someone else's behalf |
| **AWD-13** | The winner is the nominee with the **highest average score** | ❌ | ✅ slice 6 — average, `MinRequiredReviewers` honoured, ties reported rather than broken |

### 4.3 Long service

| ID | Requirement | Exists? (at survey) | Delivered |
|---|---|---|---|
| **AWD-14** | Define the **basis** for long-service awards (the milestones and what each carries) | ⚠ `MinServiceYears`, `YearsOfService`, `MilestoneDate` exist; TDC's actual values **not supplied** | ✅ slice 9 — the ladder is rows HR owns, seeded at D-8's 10/15/20/25/30. ⚠ **Only the years are seeded**; what each rung is *worth* still needs TDC — see the open-questions doc |
| **AWD-15** | **Any negative record — such as disciplinary action — exempts an employee** | ❌ no link to area 9 | ✅ slice 9 — a per-award switch, **off by default** because TDC stated the rule under Long Service only. ⚠ "Which outcomes disqualify" cannot be built: `StaffDisciplinaryActions` has **no severity column** |
| **FR-HR-113** | Report on long-service-award eligibility (FRD, Mandatory) | ❌ no report definition | ✅ slice 10 — on the catalogue + provider + seeder + **migration** pattern, reading the sweep's own calculation so the report cannot promise what the button refuses |

**All fifteen change-document requirements and FR-HR-113 are delivered.** Two carry a residue that is
TDC's to answer rather than ours to build: what a long-service rung is worth (AWD-14), and whether
severity should matter to the disciplinary exemption (AWD-15, which needs a schema change to the
discipline module before it is even expressible).

---

## 5. Decisions

**D-1 — "The staff portal" means the employee's authenticated area of the main app, not the
external portal.** The document says employees nominate and vote "through the portal". Read
literally as the external portal, this area would block on Tier C. But 5,520 of 5,579 employees
already hold a linked login to the main application, and the module already has an established
employee self-service pattern inside it — `StaffTravelMeController` (`api/staff-travel/me`) and the
medical self-service surface. Awards self-service is built the same way: `api/awards/me`, actor
from the token, no employee id in any route or query parameter, someone else's record is a **404
not a 403**, and privileged operations have no route on that controller at all. This unblocks
AWD-03 and AWD-04 without waiting for Tier C. **Confirmed with the user 2026-08-21.**

**D-2 — Committee review becomes a score; approve/reject is removed.** `AwardNominationReview`
gains a numeric score against a defined scale and loses `Approved`. `reviews/approval-count` and
`reviews/rejection-count` are removed with it. The table has zero rows, so no data migration is
owed. The averaging engine must be tested the way the appraisal engine had to be — with **identical
scores** and with **three or more reviewers** — because that is what exposed the four defects that
made every appraisal score wrong.

**D-3 — Selection method becomes explicit on the award type.** AWD-05, AWD-07 and AWD-08 are three
values of one field, not three flags: *staff vote*, *committee decision*, *direct management
selection*, *automatic from performance/targets*. An award type declares how its winner is chosen,
and the engine refuses a route that the type does not declare. `AutoGenerateNominees` folds into
this rather than surviving alongside it.

**D-4 — An award cycle is a dated thing.** AWD-11 needs a nomination window and a voting window
with real open/close dates. The existing `Year` / `Quarter` / `Month` integers on the nomination
cannot express "nominations close Friday, voting opens Monday". A cycle entity owns the dates; the
nomination points at the cycle. `AwardType.Frequency` stays as the template that generates cycles.

**D-5 — The electorate reuses the `AwardTypeTarget` shape.** AWD-06 asks for "a section of the
employees or all of them", which is the same scoping problem as *who is eligible to win*, already
solved by `AwardTypeTarget` (target type, target id, inclusion/exclusion, effective dating). Reuse
the shape rather than inventing a second one; an empty electorate means all employees.

**D-6 — AWD-15 disqualification reads the discipline store; it does not copy it.** Area 9 owns
`StaffDisciplineCase`. Long-service eligibility asks discipline a question at evaluation time. The
disqualifying window (how far back a case counts, and which outcomes disqualify) is configuration,
not a constant — TDC has not stated it. Default it, make it visible, and add it to the TDC
open-questions list.

**D-7 — Money is recorded, not posted.** Per the standing HR↔Finance split, register
`ProcessAwardPayment`, `EmployeeAward` monetary amounts and `LongServiceAward.MonetaryAmount` in
`docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` and leave the plain payment fields as they are.

**D-9 — the eligibility endpoint gets paging and filters in slice 11, and keeps its reasons.**
`GET types/{id}/eligible` judges every active employee (5,579 on DEFAULT) and returns the eligible
**and** ineligible lists in full. That is right for slice 3, whose job is to make the criteria bite,
and the harness needs the whole set to prove the counts add up. **Settled with the user 2026-08-21:**
when the screen is built it gains paging plus a filter (eligible only / ineligible only / search by
name), and the reasons stay on whichever page is shown — collapsing them into counts would take away
the thing that lets HR tell a mis-set rule from a correct one. Until then the endpoint stays
complete and slow rather than fast and partial.

**D-8 — AWD-14 milestones are configurable, defaulted, and raised with TDC.** The document says
only "define the basis"; the milestones and what each carries are not stated. **Settled with the
user 2026-08-21:** build the ladder fully configurable through the admin surface, default it to
**10 / 15 / 20 / 25 / 30 years**, and add the real values to
`docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md`. Nothing is hard-coded, so TDC's answer is a data change and
not a code change. The deferred `AwardDataSeeder` is waiting on the same answer.

---

## 6. Scope

**In:** the fifteen existing entities brought to life; token identity and `HR.Awards.*` gating;
award cycles with nomination and voting windows; eligibility evaluation; employee and management
nomination; the staff vote; committee scoring and the highest-average outcome; direct management
selection; performance/target-driven nomination; the employee self-service surface; long-service
milestones with disciplinary disqualification; FR-HR-113 as a report; the awards register screens
and an admin configuration surface.

**Out:** the external portal skin (D-1); GL posting (D-7); any change to the discipline, leave or
performance modules beyond reading them; payroll.

---

## 6b. Risks carried, and one owed decision

**`Employee.YearsOfService` is wrong, and it is not this area's to fix quietly.**
`HREntities.cs:241` computes service as `DateTime.Today.Year - DateEmployed.Value.Year`, which
overstates by up to a year for anyone whose anniversary has not yet come round. Found in slice 3
while writing the eligibility evaluator, which does **not** use it.

It is a computed property on the shared `Employee` entity, surfaced through DTOs in succession,
HR core and others — all **closed** areas with harnesses written against its current behaviour.
Correcting it is a one-line change and a multi-area regression run, which is precisely the area-13
lesson: *a correct fix can invalidate an assumption held somewhere else entirely.* It therefore
needs its own decision, not a quiet edit inside area 14.

Raise it with the user at the end of this area, alongside the FR-HR-113 long-service work in
slice 9 — which is the first place the difference between "6 years" and "7 years" decides whether
somebody gets an award.

---

## 7. Slice plan

Provisional — slices are re-cut as findings land, and the log records any change.

| # | Slice | Delivers |
|---|---|---|
| 0 | ✅ Prove the ground | Run the happy path across all eight endpoint groups. Establish which of the 100 endpoints actually execute. Expect casualties. |
| 1 | ✅ Identity | Token tenant + token actor; `HrControllerBase`; remove all 56 caller-supplied id parameters; `HR.Awards.*` policies. |
| 2 | Types, levels, targets, budgets | ✅ **Done.** The configuration surface, with D-3's two axes, honest status codes, and target-name resolution. |
| 3 | Cycles and eligibility | ✅ **Done.** D-4 cycle entity with windows; AWD-01 eligibility evaluation producing the qualified set. |
| 4 | Nomination | ✅ **Done.** AWD-02, AWD-09; the gates, the employee surface, and self-nomination as a per-award setting. |
| 5 | The vote | ✅ **Done.** AWD-04/05/06/10 — the ballot, the electorate, one-vote-per-voter, the window, and the tally withheld until close. |
| 6 | Committee scoring | ✅ **Done.** D-2, AWD-12/13 — score replaces verdict, highest average wins, membership is the gate. |
| 7 | Automatic selection | ✅ **Done.** AWD-08 — candidates derived from performance and targets. *(AWD-07 was already delivered by slices 2–4; its remaining half is conferring, in slice 8.)* |
| 8 | The award is conferred | ✅ **Done.** Nomination → `EmployeeAward`, direct conferral (AWD-07), budget reserve/spend, presentation, and the money events registered (D-7). |
| 9 | Long service | ✅ **Done.** The ladder as data (AWD-14, D-8), the disciplinary exemption as a per-award switch (AWD-15), and the sweep — preview and run sharing one calculation. |
| 10 | FR-HR-113 | ✅ **Done.** The long-service eligibility report, on the catalogue + provider + seeder + **migration** pattern, reading the sweep's own calculation. |
| 11 | Screens | ✅ **Done.** The three screens, the payload probe that preceded them, D-9's paging, and the two employee routes the probe found missing. |
| 12 | Content audit | ✅ **Done.** All 76 GETs asserted for content; 11 defects found, every one behind a 200 or a plausible 4xx. |
| 13 | UI parity | ✅ **Done.** 17 screens. Every meaningful write reachable; 64 write endpoints had **2** reachable before this. |

---

## 8. Log

### Slice 0 — prove the ground. 2026-08-21. Diagnostic only, no production code changed.

Harness: `D:\Rhema\TDC ERPS\dev-harness\hr-awards\` (`run-slice0.mjs`, `run-slice0b.mjs`,
`probe-nom.mjs`). **35 probes, 30 executed on the first pass**; the five that did not are analysed
below. Three of the five were the harness's fault, one was a guard doing its job, and one is a real
defect.

**The area is alive.** Every group ran once given a correct payload:

- Configuration: `POST types` → `levels` → `targets` → `budgets`, and all their reads.
- **`GET types/{id}/eligibility/{employeeId}` returns `true`** — the eligibility evaluator AWD-01
  needs already executes.
- Nomination: created, numbered **`NOM-20260821-8CA55D`**, status `Draft`, then `submit` moved it
  to `Submitted`. The number generator works.
- Committees, committee members, conferring an award, presentation, payment, and the whole
  long-service chain including `long-service/{id}/process`.

**⚠ Defect A — `POST nominations` 500s on a foreign key when its actor parameter is omitted.**
The route takes `[FromQuery] Guid nominatedById` with no default and no validation. Omit it and it
binds to `Guid.Empty`, which is not an employee, and the insert dies on
`FK_AwardNominations_Employees_NominatedById` (SQL error 547) behind a generic 500. This is the
area-13 `FinalizedById` shape exactly: **a required, unknowable Guid that turns a missing parameter
into a server error instead of a refusal.** Slice 1 removes the parameter — the nominator is the
token — which deletes the defect rather than validating around it.

**⚠ Defect B — attribution is forgeable, confirmed by exploit.** With a real `nominatedById`
supplied, the API accepted a nomination attributed to an employee who had nothing to do with the
request, from an unrelated admin session. `nominatedByName` came back as that employee. Every
`userId`-bearing write in this area has the same shape.

**⚠ Defect C — a levelled award cannot be conferred at its level.** `AwardType.HasLevels` exists,
`AwardLevel` exists, `EmployeeAward` has a level — but `CreateEmployeeAwardDto` carries **no
`AwardLevelId`**, so the created award came back with `awardLevelId: undefined`. The level can only
be set later by an update, if at all. Slice 8's problem; recorded here.

**Not defects — corrections to the harness, kept because the next slice will hit them too:**

- `CreateAwardCommitteeDto` wants **`QuorumRequired`** (not `Quorum`) and a **required
  `EffectiveFrom`**.
- `CreateEmployeeAwardDto` requires **`Reason`**, and has no `Year` or `AwardLevelId`.
- `CreateAwardCommitteeMemberDto.Role` binds from a **string**, not an integer.

**Correction to the pre-slice survey:** the tenant parameters were called a cross-tenant hole
before anything was probed. They are not — a guard returns 403 on a mismatch. The claim was wrong
and section 3.2 now records what was measured instead.

**Fixture data left on DEFAULT by this slice** (award type `S0-EMP-MONTH`, level `S0-GOLD`, a
committee, two nominations, an employee award and a long-service row) is slice-0 debris. Slice 1
should clean it or claim it deliberately; either way, assert the cleanup — an unasserted cleanup
step is the area-15b lesson.

---

### Slice 1 — identity comes from the token. 2026-08-21, **30/30**, run twice.

Harness: `run-slice1.mjs` + `setup.mjs` (adapted from the area-9b actor minter). Three actors,
because a SuperAdmin can never test a gate: `a14v_<stamp>` (role **HR**, employee-linked),
`a14v_<stamp>B` (role **Employee**, nothing else), `a14v_<stamp>S` (the nomination subject).

**Delivered**

- `AwardsController` now extends `HrControllerBase`. **All sixty caller-supplied id parameters are
  gone** — 24 `tenantId`, 32 `userId`, and the four naming a domain actor — replaced by 47 token
  context guards.
- **100 policy gates**: 55 Read, 19 Write, 26 Admin. Catalogue administration (types, levels,
  targets, budgets, committees, members) and every delete are Admin; participation is Write.
  `HR.Awards.Read/Write/Admin` added to `HrPermissions` with the three policies, and HR staff
  granted Read + Write — deliberately not Admin, which the harness asserts.
- **Defect A is deleted, not guarded.** `nominatedById` was required, unknowable and unvalidated;
  omitting it bound `Guid.Empty` and produced a foreign-key 500. There is no parameter left to omit.
- **Defect B is closed, and the harness proves it with the original exploit.** The slice-0 call —
  `?nominatedById=<a colleague>&userId=<theirs>&tenantId=<another tenant>` — now attributes the
  nomination to the caller and lands it on the caller's own tenant. Every appended parameter is
  ignored rather than honoured.
- **The discarded `[Required]` field is fixed.** `AddTeamNominee` received the employee id twice —
  once as a query parameter, once as `CreateTeamAwardNomineeDto.EmployeeId`, which the mapper threw
  away. One source now, and the harness asserts a leftover parameter cannot override it.

**⚠ Defect D, found while verifying and swept rather than patched: a write response that is blank
where a read is populated.** The first run failed one assertion — `employeeName: ""` on the
team-nominee create, while an immediate re-read returned the name correctly. The cause is the
ported shape from `hr-ported-list-read-bugs`: the create maps the entity it just constructed, whose
navigations were never loaded, and `ToDto` renders an unloaded navigation as `string.Empty` instead
of failing. **A screen built on that response shows a blank row that fixes itself on refresh, which
reads as a UI fault and is not one.**

It was not one site. Seven mappers in this area render names off navigations, and three create
paths mapped an unloaded entity: `TeamAwardNominee`, `AwardCommitteeMember` and
`AwardNominationReview` (`AwardBudget` too, though its repository was already correct). Fixed at the
repository, following the precedent already in that file — `AwardBudgetRepository` overrides
`GetByIdAsync` with its `Include`, so the other two now do the same and the create paths re-read
before mapping. The update paths get it for free: they already load through `GetOwnedAsync`. All
four write responses are asserted, so the class cannot come back one slice at a time.

**Decisions taken here**

- **Slice-0 debris is claimed, not cleaned.** Deleting it would exercise the Admin delete paths,
  which are themselves unproven, so a cleanup step would have rested on untested code. The rows stay
  as fixtures for later slices. Every slice-1 fixture is stamped (`A14-<stamp>`) so runs never
  collide — the harness was run twice on different stamps and is green both times, which is the
  area-17 rule about a second run finding the first run's data.
- **`admin` can no longer raise a nomination**, and that is correct rather than a regression. It
  holds every permission and **no employee record**, and a nominator is an Employee FK. The refusal
  names what it needed: *"Raising a nomination requires your user account to be linked to an
  employee record."* Asserted, message included.

**Not fixed here, recorded for slice 8.** EF logs at startup that `AwardNomination.Award` and
`EmployeeAward.AwardNomination` were separated into two relationships, because `[ForeignKey]` is
declared on both sides. This is **not** the shadow-column bug — checked, and there are no `Id1`
columns; both FKs are real and intended. What it does mean is that EF treats them as two independent
one-way links, so nothing prevents a nomination pointing at award A while award A points at a
different nomination. Slice 8 confers awards from nominations and should assert the pair agrees.

**Also done:** the long-service basis, its disciplinary disqualification rule and the two data facts
behind them (38% `DateEmployed` coverage; one employee at 10 years, none beyond) are written up in
`docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` per decision D-8.

---

### Slice 2 — the configuration surface. 2026-08-21, **49/49**, run twice. Migration `AddAwardSelectionModel`.

Harness: `run-slice2.mjs`. Slice 1 re-run afterwards at 30/30 — no regression.

**D-3 revised: two axes, not one.** The plan called for a single "selection method". Building it
showed that TDC's note varies two things independently, so the field became two:

| | |
|---|---|
| `AwardNominationSource` | `OpenNomination` · `PerformanceTriggered` · `ManagementDirect` |
| `AwardWinnerDecision` | `StaffVote` · `CommitteeScore` · `ManagementDecision` |

The evidence is in the note itself: *"employees or management will do the nomination, and then staff
can vote"* pairs open nomination with a vote, *"some might not have to go through the employee vote
since management will decide and award"* pairs the **same** source with a management decision, and
the committee section pairs it with scoring. One field would have forced a fixed menu of
combinations and lost the ones TDC actually described. The harness asserts that pair of cases
specifically, because they are the whole argument for the split.

One pairing is refused: `ManagementDirect` with a vote or with scoring — there is no nomination
stage, so there is no candidate list. Refused **on create and on edit**; a rule that only guards the
front door lets an award type be edited into the impossible state afterwards.

**Defaults.** `NominationSource = OpenNomination`, `WinnerDecision = CommitteeScore`, on the column
and in the entity. Deliberately not `StaffVote`: every pre-existing row acquires a value, and for a
row nobody classified, "a committee decided" is an absence of information whereas "the staff voted"
would assert a ballot that never happened. Verified in SQL — all 7 existing rows backfilled to 1|2.

**`AutoGenerateNominees` dropped.** Mapped through all three DTOs, read by no logic anywhere. It
means what `NominationSource = PerformanceTriggered` now means, and two fields carrying one fact are
free to disagree — the same defect slice 1 fixed for the team-nominee employee id. The deferred
`AwardDataSeeder` sets the two new fields explicitly, with a note for whoever rewrites it against
TDC's catalogue.

**⚠ Defect E — 38 refusals the caller could not read, and ~18 of them lying about the status.**
`AwardsServices` threw `InvalidOperationException` at 38 sites, and
`GlobalExceptionHandlingMiddleware` replaces that exception's detail with the fixed string "The
operation is not valid for the current state of the object." Every rule fired correctly and then
said nothing.

Worse than the lost text: about eighteen are *"… not found"* and **all were answering 400**. No
client could distinguish a deleted award from a malformed payload, so none could decide whether to
re-fetch, show "no longer available", or highlight a field. Fixed with `AwardsWorkflowException` and
a middleware case — the fifth HR area to need this shape, after medical, succession, probation and
job architecture. **24 throws converted**: 18 → 404, 3 → 409 conflict, 2 → 409 state, 1 → 400.

The 14 `"No tenant is associated with the current user."` guards are deliberately left as they are:
since slice 1 the tenant comes from the token, so reaching one means the token itself is malformed,
and a generic 400 is the right answer to a should-never-happen.

**⚠ Defect F — `AwardTypeTargetDto.TargetName` was declared and written by nothing.** Always null,
in every path, so every eligibility rule read back as its kind and a blank: *"Employee: "*. The
reason it survived is structural — `AwardTypeTarget.TargetId` is **polymorphic** (organisation unit,
position, staff level or employee, by `TargetType`), so there is no navigation for EF to load and no
mapper could have filled it. Fixed with `IAwardTargetNameResolver`, batched **one query per kind**
rather than one per row. A target whose subject has since been deleted stays null rather than
getting an invented label: a dangling reference should look like one. Asserted on the write
response, on the list read, and across two different kinds.

**Also: `AwardTypeSummaryDto` did not carry the selection model.** Since this slice that is the main
thing distinguishing one award type from another, so the register would have listed several
identical-looking rows with no way to tell a voted award from a committee-scored one without opening
each. Added to the summary and its mapper.

**⚠ The lesson that cost the most: a green build is not a rebuilt binary, and I mis-diagnosed it
twice before measuring.** The summary fields returned `0` — an undefined enum value — through three
API restarts and one successful build, while the detail DTO returned the right names from the same
row. Source, DTO, mapper, DB and snapshot all checked out; there was one definition of each and no
second project compiling them.

What settled it was **turning on EF command logging and reading the SQL**: the list query selected
`[a].[NominationSource]` and `[a].[WinnerDecision]`, exactly as the detail query did. So the entity
arrived populated and the compiled mapper was dropping the values — a stale assembly that MSBuild
kept skipping, because the source (12:08:19) was already older than the DLL (12:08:34) and its
timestamp check saw nothing to do. `touch` on the two files broke the tie and the next build fixed
it.

Two intermediate conclusions stated before measuring were wrong — first "the binary must be current
because the DTO change is visible in the JSON", then the reverse. **Both were inferences from code
when a log line was available.** For a symptom that contradicts the source, get the runtime to say
what it did before reasoning about what it should have done.

---

### Slice 3 — cycles and eligibility. 2026-08-21, **51/51**, run twice. Migration `AddAwardCycles`.

Harness: `run-slice3.mjs`. Slices 1 and 2 re-run afterwards at 30/30 and 49/49 — no regression.

**Delivered: the cycle (D-4 / AWD-11).** New `AwardCycles` table and a nullable
`AwardNominations.AwardCycleId`. Eleven endpoints — the register, the two derived open-lists,
create, update, publish, cancel and delete.

Two design calls, both made to avoid a defect shape this area keeps producing:

- **Open and closed are derived from the dates, never stored.** `AwardCycleStatus` is a lifecycle
  flag (Draft / Published / Completed / Cancelled) and carries no "NominationsOpen" member. Whether
  a window is open is `Status == Published` plus the clock. A stored flag *and* a window are two
  facts about one thing, free to disagree the moment the close date passes.
- **Which windows a cycle must carry is read off slice 2's selection model.** A `StaffVote` award
  must have a voting window; anything else must not, because a voting window on an award nobody
  votes on describes a ballot that will never be held. A `ManagementDirect` award has no nomination
  stage, so it carries no nomination window. And **voting cannot open before nominations close** —
  TDC's note sequences them deliberately, and overlapping them would mean early ballots were cast
  against a different candidate list from later ones.

`PublishAsync` re-runs the window checks on the way out of draft, because the award type's selection
model can be edited after the cycle was drafted — a published cycle must not describe a stage its
award no longer has.

**⚠ Defect G — the eligibility criteria had never been consulted.** The existing evaluator checked
the award's *targets* — unit / position / staff-level scoping — and nothing else. Six fields on
`AwardType` appear **only in mappers**, mapped in and mapped out and applied by nothing:
`MinServiceYears`, `MaxServiceYears`, `MinAge`, `MaxAge`, `MaxAwardsPerEmployee`,
`MaxAwardsPerPeriod`. Those six are exactly the "eligibility criteria" TDC's note has HR set up
before anyone nominates, so *"management will set the criteria and then it will qualify some
employees"* could not work at all.

`AwardEligibilityEvaluator` applies all of them and **returns reasons**. The ineligible list comes
back deliberately: whoever sets the criteria has to see why an expected name is absent, or a
mis-set rule produces the same screen as a correct one.

Three further things it repairs:

- **`AwardTypeTarget`'s effective dating was never honoured** — a target that expired last year
  still scoped the award. The harness proves an expired exclusion stops excluding, and that an
  `asOf` inside its lifetime still shows it did.
- **Age was `Now.Year - DateOfBirth.Year`** — 40 for someone who is still 39 for another eleven
  months, which on a minimum-age rule admits people a year early. Now whole completed years.
- **A missing `DateEmployed` is said out loud** rather than passing silently, which matters because
  62% of live records are in that state.

**⚠ Recorded, deliberately not fixed: `Employee.YearsOfService` has the same wrong arithmetic.**
`HREntities.cs:241` computes `DateTime.Today.Year - DateEmployed.Value.Year`. It is a computed
property on the shared employee entity, read by succession and other **closed** areas whose
harnesses were written against its current behaviour. Changing it here would be the area-13 lesson
repeated — a correct fix invalidating an assumption held somewhere else. The evaluator does not use
it. It needs its own decision and its own regression run; see the risks section.

**A dead rule of my own, found by an assertion that failed for the wrong reason.** `CancelAsync`
guards against a whitespace-only reason, and that guard is **unreachable over HTTP**: `[Required]`
trims before testing, so `"   "` reads as missing and the model layer refuses it first. The guard is
kept for callers that are not the controller, and the harness now asserts where the refusal actually
comes from — naming the field in `errors` — rather than where it was assumed to. Worth noting that
the same reasoning applies to *any* service-level string guard behind a `[Required]` DTO field in
this codebase.

**Efficiency, fixed before it became the pattern.** The first cut of the cycle service counted
nominations with `GetAllAsync()` and looked the award type up per row — the shape that turns a cycle
register into a full scan per page. Replaced with `IAwardCycleRepository` (tenant-scoped queries, a
published-only read for the open-lists) and `GetCountsByCycleAsync` (one grouped query).

**Open, per decision D-9:** `GET types/{id}/eligible` judges every active employee and returns both
lists in full. Correct for now; slice 11 adds paging and filters and keeps the reasons.

---

### Slice 3b — one answer to "how long has this person worked here". 2026-08-21, **6/6**.

Harness: `run-slice3b.mjs`. Slices 1–3 re-run at 30/30, 49/49, 51/51.

**The change.** The HR module held three implementations of "whole years since a date" and only one
was right. `HrPolicyCalculations.Age` did the anniversary check correctly;
`Employee.YearsOfService` subtracted calendar years (`Today.Year - DateEmployed.Year`), reporting a
completed year on 1 January for somebody whose anniversary falls in December; and slice 3's
eligibility evaluator had grown a private fourth copy while fixing the second. All now call
`HrPolicyCalculations.CompletedYears(from, asOf)`, which takes an `asOf` because award eligibility
must re-check a rule as it stood on some other day.

**Why now rather than later — measured, not argued.** Computing both formulas across every employee
with a `DateEmployed`: **0 of 2,140 differ today.** The seeded dates cluster in January, so the
correction is a provable no-op on current data. That window is temporary: real employment dates
spread across twelve months, so roughly a third of employees would shift once migration lands, and
the change would then be entangled with the migration that caused it.

The deciding argument was not the arithmetic but the **inconsistency slice 3 had opened**: since
that slice, `/api/hr/Employees/{id}` and `/api/Awards/types/{id}/eligible/{employeeId}` could give
different service figures for the same person on the same day. Fixing only the evaluator had created
the two-sources-of-one-fact shape this area has spent three slices removing.

**A no-op cannot demonstrate itself**, so the harness builds the boundary deliberately: two
employees with the **same start year** (2016), one whose anniversary has passed and one whose has
not. The old formula reported 10 for both; the correct one reports 10 and 9. A 10-year eligibility
rule then admits the first and refuses the second, quoting the same figure the employee record
shows.

**⚠ Cross-area finding: area 14's harness fixtures have been failing area 13's harness since slice 1
— and it is not the arithmetic.** `hr-succession/run-slice9.mjs` asserts `fitScore === 0` for
`results[0]` of `candidate-search`. Measured today:

| candidates | score |
|---|---|
| every non-area-14 employee (52 of 100) | **0**, `yearsOfService: null` — no `DateEmployed` on record |
| area-14 fixtures at 2020-01-06 (44) | **60** — minted from **slice 1** |
| the slice-3b boundary pair (4) | **90 / 100** |

`SuccessionFitScoring.Score` averages only the signals that are non-null. With no performance, no
competency requirements and no potential, **tenure is the only signal**, so the score is simply
tenure ÷ 10 × 100. Area 14's `setup.mjs` mints employees with `dateEmployed` — so the moment slice 1
ran, the top of a score-ordered list stopped being a zero-scoring employee.

Under the *old* arithmetic those 2020 fixtures also scored 60 (`2026 − 2020 = 6`), so **slice 3b did
not cause this**. It has been failing for days and nobody looked.

The real fault is the assertion's shape: `results[0]` on a list ordered by score over the whole
workforce is owned by whichever area last created an employee. That is precisely the lesson area 13
recorded in its own plan — *demand a known row by id* — applied everywhere in that harness except
here.

**Re-anchored with the user's agreement, 2026-08-21.** `hr-succession/run-slice9.mjs` now selects
its sample by the property the assertions are actually about — a candidate with **no employment date
on record**, whose every fit signal is absent — instead of trusting whatever sorts first. The block
also gained its converse, which the old shape could never state: a candidate *with* a start date
scores above zero on tenure alone. **66/66**, up from 63; the fix added assertions rather than
deleting the failing one.

⚠ **The transferable point, and it is about harnesses rather than code.** Every area's fixtures
share one tenant and one database. An assertion anchored on "the first row of an ordered list" is
therefore owned by every other area, and it fails silently in the sense that nobody re-runs a closed
area's harness. Two habits follow: anchor on a row you created or can identify, and re-run the
harnesses of any area whose data your fixtures could reach.

---

### Slice 4 — nomination, and the employee's own surface. 2026-08-21, **44/44**, run twice. Migration `AddAwardSelfNomination`.

Harness: `run-slice4.mjs`. Slices 1, 2, 3, 3b re-run at 30/49/51/6.

**A nomination now has to mean something.** Before this slice it was a row: any award type, any
nominee, at any time. TDC's note describes a sequence — HR sets the criteria, the criteria qualify
some employees, those employees are nominated during a window, the result is voted on — and each
gate is one step of it refusing to be skipped:

| | |
|---|---|
| a direct-selection award | accepts no nominations at all |
| every nomination | must name its cycle; without one there is no window and nothing to vote against |
| the cycle | must be published, belong to that award, and be inside its nomination window |
| an individual nomination | must name a nominee; a team one must name a team |
| the nominee | must pass the criteria HR configured — the step nothing consulted before slice 3 |
| the same nominator | cannot nominate the same person twice in one cycle; different colleagues still can |

**`api/awards/me` — the first employee-facing surface (D-1).** Built on the area-12 pattern: no
employee id in any route or query parameter, someone else's nomination is a **404 not a 403** (a 403
confirms the id exists and turns the surface into an oracle for enumerating nomination ids), and
privileged operations have no route there at all rather than a guard a later edit could weaken.
Gated on bare `[Authorize]`, because holding no awards permission is the normal case for the people
it serves.

One deliberate asymmetry: **the employee candidate list returns eligible names only, with no
reasons.** The awards desk sees the ineligible names and why — that is how HR checks its own
criteria — but an employee choosing somebody to nominate has no business reading why a colleague
failed a rule.

**Self-nomination became a setting, on the user's decision, after asking what practice actually
does.** The first cut allowed it on the grounds that TDC's note states no rule and inventing a
prohibition would be a developer setting policy. That reasoning was right about not inventing and
wrong about the default: allowing is the permissive choice, and the awards the note describes — a
best employee award, staff voting, nominating managers — are exactly the kind enterprise practice
bars. Self-nomination is normal for innovation, suggestion and improvement awards, where the
achievement is one the nominee can evidence; it is barred for behavioural awards, where being chosen
by somebody else is the substance of the award.

So `AwardType.AllowSelfNomination` exists and **defaults to false**. Verified in SQL: no existing
award permits it. The harness asserts all three cases — a voted award refuses, a configured
Innovation award accepts, and an award nobody configured comes back `false`, which is the assertion
that actually protects the default. The TDC question narrowed from *"what should the rule be"* to
*"which awards should have it enabled"*, which is a catalogue answer rather than a policy debate.

**⚠ Slice 4's rules invalidated slices 1 and 2 — and that is the right way round.** Both create
nominations, neither knew about cycles, and both aborted the moment the cycle requirement landed.
Three separate repairs, each worth noting:

1. **A shared `openCycleFor` helper**, rather than the same fixture copied into four harnesses.
2. **It reads the award type first**, because slice 3 made the required windows depend on how the
   award is decided — a `StaffVote` award must carry a voting window and anything else must not.
   Working that out in the helper means no harness has to know how its own fixture is decided in
   order to open a cycle for it. The first version did not, and slice 2 failed on exactly that.
3. **Slice 1's exploit re-run got its own cycle.** It nominates the same subject twice on purpose,
   which slice 4's duplicate rule now refuses — so the assertion about *attribution* was being
   answered by a rule about *duplication*. A test that passes for the wrong reason is worse than one
   that fails.

The general point: **a new rule reaches backwards through every harness that predates it.** Running
only the new slice would have left three green-looking files that no longer execute.

---

### Slice 5 — the vote. 2026-08-21, **49/49**, run twice. Migration `AddAwardVoting`.

Harness: `run-slice5.mjs`. Slices 1, 2, 3, 3b, 4 re-run at 30/49/51/6/44.

**The centre of TDC's note, and nothing modelled it.** *"HR will setup the eligibility criteria,
then employees or management will do the nomination, and then staff can vote for who is supposed to
win."* Before this slice, a search for `AwardVote`, `Ballot` or `CastVote` across the whole solution
returned nothing at all.

**The electorate reuses the eligibility targets, told apart by `Purpose`.** The note asks for two
different sets — *"it will qualify some employees"* (who may win) and *"a section of the employees or
all of them can vote"* (who may vote) — and they are genuinely different: a department might nominate
from its own staff while the whole company votes. Same shape, same table, one column. **No electorate
targets means everybody votes**, which is the "or all of them" half of the sentence.

⚠ **That change carried its own trap, and the harness exists to catch it.** The eligibility evaluator
now filters on `Purpose = Eligibility`. Without that filter a rule saying "the whole company votes"
would silently have become a rule about who may **win** — the two-meanings-one-column shape this area
has produced repeatedly. The harness scopes an electorate down to one employee and then asserts
eligibility is still wide.

**Three judgement calls, each recorded in the code that makes them:**

1. **One vote per voter per cycle, enforced by a unique index rather than a service check.** The note
   asks *who is supposed to win* — a single choice, not approval of each nominee in turn. The index
   is filtered on `IsDeleted = 0` so a withdrawn ballot leaves room for a replacement; unfiltered, a
   soft-deleted row would lock a voter out of their own vote permanently. Changing your mind updates
   the ballot you cast; a second row would double-count the tally.

2. **The tally is withheld until voting closes — from the awards desk too.** A visible running count
   changes the result it reports: people break towards a leader, and an early lead in a small
   electorate is mostly noise. Turnout is shown throughout, because it says nothing about who is
   winning. Withholding it from `/results` while exposing per-nominee counts on the ballot would be
   the same disclosure by another route, so the harness asserts no count key appears on a ballot
   entry at all.

3. **A tie is reported, never broken.** Earliest nomination, alphabetical order — any rule this code
   invented would settle a real award on a basis nobody at TDC chose. The result names the tied
   nominations and says the committee decides.

Self-voting is governed by the same `AllowSelfNomination` setting rather than a second flag: both are
ways of advancing your own candidacy.

**⚠ A hollow assertion I caught in my own harness, and the reason it was hollow.** The first version
of the self-voting block asserted only that *self-nomination* was refused — so it never reached the
voting rule at all, and would have passed however that rule behaved. The case that matters is the one
where an employee is on the ballot **without having put themselves there**: a colleague nominates
them, and then they try to vote for their own nomination. Rewritten to set that up explicitly, and it
now also asserts they can still vote for somebody else — being on the ballot must not disenfranchise
you. This is the area-13 lesson restated: *a conditional or mis-aimed assertion is a skipped
assertion wearing a tick.*

**Two harness bugs of the same shape, worth naming because they will recur.** Both the self-voting
and the tie fixtures were first written against a cycle whose nomination window was **already closed**
— nominations were raised into a state they could never have been raised in. A cycle now has to be
*walked forward* in the harness the way a real one moves through time: nominations open, nominations
close, voting opens, voting closes. Fixtures that skip to the end state cannot be built by the rules
that govern the beginning.

**Not a defect: a transient 500 from `/api/auth/login`** during a batch of overlapping harness runs.
The API stayed healthy and the retry was clean. Recorded rather than diagnosed — it was not
reproduced and is not this area's code.

---

### Slice 6 — committee scoring. 2026-08-21, **42/42**, run twice. Migration `AddAwardCommitteeScore`.

Harness: `run-slice6.mjs`. Whole area re-run afterwards: **32 / 49 / 51 / 6 / 44 / 49 / 42 = 273
assertions**, all green.

**D-2 delivered.** `AwardNominationReview.Approved` (a `bool?`) becomes `Score` (1–100), and the
winner is the highest **average**. TDC's note is the whole specification: *"the committee members
will score, and the winner will be the one with the highest average score"*. Approve/reject cannot
rank anything — two nominations approved by everybody were indistinguishable, so the award could not
be decided from the data the committee had entered.

**⚠ Defect 11 — the score was already being thrown away, explicitly.** `SubmitCommitteeReviewDto`
carried `[Range(1, 100)] int? Score` before this slice. The entity had no such column. And
`AwardsMappingExtensions` contained the literal **`Score = null`**. A committee member could submit
87, receive **200 OK**, and the system would keep nothing and report `null` back. Every other dead
field in this area was merely unused; this one was nulled out on the way home. It also settles the
scale question without inventing anything — 1–100 was already chosen, just never stored.

**⚠ Defect 12 — `AssignToCommitteeAsync` had no endpoint at all.** Declared in the interface,
implemented in the service, called by nothing: no controller action, no other service, no job. That
was survivable while any HR user could score anything. The moment membership became the gate it was
fatal — a nomination can only be scored once assigned, and nothing could assign one. The committee
path would have been dead, and dead in the worst way: looking like a permissions problem rather than
a missing route. Found only because the harness needed to assign a nomination and I went to read the
route instead of guessing it — the first draft *did* guess, with a `.catch()` fallback onto a second
URL, which would have hidden the finding entirely.

**⚠ Defect 13 — `GetPendingReviewsAsync` could only ever return nothing.** "Pending" meant a review
row with a null `ReviewDate` — one of the placeholder rows the seeder created. A review row now *is*
a score and is stamped when written, so no such row can exist. Reimplemented as the question it
should always have asked: *which nominations are with a committee I sit on that I have not scored* —
a query over nominations, not reviews.

**The migration retires verdicts rather than converting them.** The doc comment first claimed the
table was empty; checking found **9 rows**, all `Approved = 1`, all fixtures from this area's own
slice 1. The scaffolded migration would have given each of them `Score = 0` — not a harmless default
but an **inversion**, recording a member who approved a nomination as scoring it zero out of a
hundred, and the scoring service counts rows, so those zeros would drag the average of the very
nominations their authors supported. They are soft-deleted with the reason in `DeletedBy`. Verified
in SQL: 0 live, 9 retired, none carrying a fabricated score.

The same reasoning removed `AwardDataSeeder`'s "pending review" placeholder rows: an empty review row
would count as a reviewer who had scored while contributing nothing to the mean.

**⚠⚠ The pattern worth naming: three slices, three instances of the same mistake.**

| slice | act gated on an HR permission | who actually performs it |
|---|---|---|
| 4 | nominating | any employee |
| 5 | voting | any employee in the electorate |
| 6 | scoring — and then *reading the scores* | committee members |

Each time the symptom was a **403 that reads like a misconfiguration rather than a design error**,
and each time it surfaced only because a harness ran as the real actor instead of as admin. The
sharpest instance was the last: my own doc comment on the desk's result endpoint argued that *"seeing
where colleagues stand is part of what they were appointed to do"* — while that endpoint required a
permission committee members do not hold. **The committee could score and could not see the
scores.**

The rule, now written into `AwardsMeController`: **if the person entitled to do a thing is identified
by a row rather than by a grant, the route belongs on the employee surface and the row is the
gate.** Scoring, the pending list, and a member's view of their own committee's result all moved
there; the desk keeps the reads, because HR must see what a committee scored without sitting on it.

**The mean is tested the way the appraisal engine taught us**: 87, 87, 87 → 87 (identical scores
catch a sum never divided, and three catch a divisor hard-coded to two), 60/70/80 → 70, and an
unscored nomination reports `null` rather than 0 — asserted to sort **last** rather than among the
zeros, because ordering it there would read as the committee having rejected it. `MinRequiredReviewers`
now bites: one score out of three is listed with its partial average and cannot win.

**⚠ And a coverage erosion I caught in my own harness.** Reordering slice 1 (assignment moves a
nomination to `UnderReview`, so it must be submitted first) silently **deleted** its
"a nomination submits with no userId parameter" assertion, leaving a stale section comment behind.
The file then passed 31/31 while testing less than it had before. Restored, and it is now 32/32.
*A harness that goes green after an edit has not necessarily kept doing what it did — count the
assertions, not just the failures.*

---

### Slice 7 — performance-triggered candidates. 2026-08-21, **27/27**, run twice. Migration `AddAwardPerformanceTriggers`.

Harness: `run-slice7.mjs`. Whole area re-run: **32 / 49 / 51 / 6 / 44 / 49 / 42 / 27 = 300
assertions**, all green.

**⚠ The slice was re-cut, and the honest version is smaller than the plan said.** Slice 7 was written
as *"direct and automatic selection — AWD-07 and AWD-08"*. Working through it, **AWD-07 was already
delivered** across earlier slices: slice 2 made `ManagementDirect` a real value of the selection
model, slice 3 refuses such an award a nomination window, and slice 4 refuses it nominations
outright. All that remains of AWD-07 is *conferring* the award, which is slice 8's subject. So slice
7 is AWD-08 alone. Recorded rather than padded — a slice that claims two requirements and delivers
one is the sort of bookkeeping that makes a plan stop being trustworthy.

**AWD-08 delivered.** *"Some of the nomination will be due to performance or target reached"* — two
triggers, so two nullable columns on `AwardType`: `MinPerformanceScore` (read from the appraisal
store) and `MinGoalsAchieved` (from completed goals). Both set means both must be satisfied: an award
asking for performance **and** targets should not settle for one. Neither set means the award
generates nobody **and says so**, rather than quietly finding nothing — there is no sensible default,
because a threshold is a policy TDC has not stated and any number would look authoritative.

**A generated nomination is a nomination, not a shortcut past the rules.** It passes the same
eligibility criteria, belongs to a cycle, obeys the nomination window, and is **Submitted rather than
Approved**: the trigger decides who *stands*, and the vote or committee still decides who *wins*.
Generating straight to a winner would let an appraisal score award a prize.

Two details worth keeping:

- **Only the most recent scored appraisal counts.** An employee with three appraisals is judged on
  their latest scored one, not their best ever — an award for current performance should not be won
  on a result from four years ago.
- **The nominee is recorded as their own nominator.** Nobody put them forward, and `NominatedById` is
  a required Employee foreign key — the very shape that made this area's slice-0 nomination endpoint
  fail with a 500. Leaving it blank was never an option; recording the employee says truthfully that
  the nomination arose from their own performance.

**⚠ The data, measured 2026-08-21, and why the harness is written the way it is.** The live store
holds **4,328 appraisals of which 18 carry an `OverallScore`**, and **16 employees** have a goal at
100%. This is the fourth appearance of the unmaintained-column shape, after `DateEmployed` (38%),
`ExpectedHeadcount` and `HeadEmployeeId`.

That shaped the assertions rather than the rule. The harness deliberately does **not** assert "a
candidate was found" — with 18 scored appraisals, a run matching nobody could be the truthful answer,
so such an assertion would fail for a reason that is not a defect. What it asserts instead is that
**a zero is attributable**: the run reports how much evidence it examined, and *"no trigger set"*,
*"nobody met the trigger"*, *"eligibility excluded them"* and *"already nominated"* are four
distinct notes, each checked in the case that produces it. A bare count of zero would make them
indistinguishable, and a screen showing `0 candidates` would blame the rule for the data.

It did in fact run on real data: **18 examined, 4 above a threshold of 80, 4 nominations created** —
and a second run created none, attributing them to `alreadyNominated` instead.

---

### Slice 8 — the award is conferred. 2026-08-21, **35/35**, run twice. Migration `AddAwardCycleToEmployeeAward`.

Harness: `run-slice8.mjs`. Whole area re-run: **32 / 49 / 51 / 6 / 44 / 49 / 42 / 27 / 35 = 335
assertions**, all green.

**⚠ Defect 14 — `CreateFromNominationAsync` had no endpoint.** Declared, implemented, called by
nothing: the second unreachable service method this area has produced, after
`AssignToCommitteeAsync` in slice 6. It is the **only** path from a decision to an award, so the
thing the whole area builds towards had no route to it.

It also had no rules whatsoever — any nomination in any state could be turned into an award,
repeatedly. It now refuses drafts, rejected and withdrawn nominations; refuses a second award from
the same nomination; and refuses a **team** nomination, whose `NomineeId ?? Guid.Empty` would have
produced a foreign-key violation surfacing as a 500 — slice 0's defect A exactly.

**⚠ Defect 15 — the budget never depleted.** `GetAvailableBudgetAsync` computes
`BudgetAmount - SpentAmount - ReservedAmount` and **nothing incremented either figure**, so
"available" always equalled the whole budget however many awards had been conferred and paid. Slice
2's own harness asserted that an untouched budget had its full amount available — true, and equally
true after a hundred payouts. *A budget that never depletes is worse than no budget: it looks like a
control and is not one.*

Conferral now **reserves** and payment **spends**, which is what the two columns were always for. An
award decided but unpaid is a real commitment; treating it as nothing until the money moves would let
a year be over-committed by exactly the amount awaiting payment.

The assertion that separates a correct implementation from a plausible one is the mismatched
payment: **release the promise, record the fact.** An award promised 1,000 and paid 900 releases
1,000 of reservation and books 900 of spend. Code that released what was *paid* would drift by 100
every time the two differed, and every other budget assertion would still have passed.

**AWD-07 completed.** Direct conferral is restricted to `ManagementDirect` awards; allowing it
elsewhere would let somebody hand out the prize while the ballot was still open. Slice 0's defect C
is closed with it: `CreateEmployeeAwardDto` now carries `AwardLevelId`, and a levelled award refuses
to be conferred at no level.

**Slice 1's EF note is discharged.** EF treats `AwardNomination.Award` and
`EmployeeAward.AwardNomination` as two independent one-way relationships, so nothing in the model
stops them disagreeing. The service now closes the loop by hand in both directions and the harness
asserts both ends.

**⚠⚠ The dominant defect class of this area, four more instances in one slice.** Every one is *a
field that is written correctly and cannot be read back*:

| | |
|---|---|
| `EmployeeAward.AwardCycleId` | the column did not exist; added, with the DTO and nine `Include`s |
| `EmployeeAwardDto.AwardNominationId` | written by the service, absent from the read DTO — an award could not be traced to the case made for it |
| `EmployeeAwardDto.AwardLevelId` | **a Gold award and a Bronze award were indistinguishable on every read** |
| nomination → `AwardNumber` | the `Award` navigation was included on **1 of 12** nomination reads, so the number came back blank on eleven of them |

Why this class survives review is worth stating: the write succeeds, the status code is right, the
database is correct, and the only symptom is a field quietly missing from a payload. **Nothing
fails.** It is the reason every assertion in this area checks content rather than status — and in
this slice the two assertions that failed were worth more than the thirty-three that passed.

**⚠ Two corrections I had to make to my own claims.**

1. I wrote `entity.AwardCycleId` on `EmployeeAward` **without checking the entity had that
   property** — the "field written from a name rather than read" mistake this area punishes, made by
   the author of the fixes for it. The user caught it in the IDE before a build. The design survived
   review; only the assumption did not. A scripted edit then put the new navigation on `AwardVote`
   instead, because it matched the first `[ForeignKey(nameof(AwardNominationId))]` in the file.

2. I reported defect 16 as "both ends of the nomination-award link are invisible". **Only one end
   was.** `AwardNominationDto` already exposed it as `AwardId`/`AwardNumber` in its Outcome block,
   already mapped — my harness checked a field name I had invented. Worse, adding my own
   `EmployeeAwardId`/`AwardNumber` pair alongside would have created **two fields for one fact**,
   precisely the shape this area has been removing since `AutoGenerateNominees`. The compiler caught
   it only because the names collided; a slightly different name would have compiled and shipped.

**D-7 honoured.** Every money event — award value, payment, amount paid, long-service value, level
value, budget, reservation, spend — is registered in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`, with a
note that the budget figures are the awards desk's own bookkeeping to reconcile against rather than
entries to import, and that `LongServiceAward.LeaveId` is a hook with no writer. Nothing posts to a
ledger.

---

### Slice 9 — long service. 2026-08-21, **91/91**, run twice. Migration `AddLongServiceMilestoneAndDisciplinaryCheck`.

Harness: `run-slice9.mjs`. Whole area re-run: **32 / 49 / 51 / 6 / 44 / 49 / 42 / 27 / 35 / 91 =
426 assertions**, all green. (Slices 0 and 0b are surveys and report no summary.)

**AWD-14 — the ladder is data, not code.** TDC's note asks us to *"define the basis for the long
service awards"*, which is an instruction to build the mechanism rather than a statement of the
policy. So `LongServiceMilestones` holds one row per number of years per award, carrying what that
rung is worth, and the seed action fills in D-8's 10/15/20/25/30. It seeds **only the years** — the
money is left empty because TDC has not said what a twenty-year award is worth and a seeded figure
would look like an approved one.

**A pre-existing field turned out to be lying, and was not made to lie differently.**
`CompanyHrPolicy.LongServiceMilestoneYears` has documented itself since it was written as feeding
*"awards eligibility"*, and nothing had ever read it. The obvious move was to make it the authority.
That would have been wrong: `ICompanyHrPolicyProvider` returns a **coded-defaults instance** when a
tenant has never opened the settings page, so its value cannot be told apart from one HR actually
chose — every fresh tenant would silently have got 5/10/15/20/25 instead of the ladder that was
decided, with nothing showing that a default had beaten a decision. It is now **reported** in the
seed response so a screen can offer it in one click, and applied only when passed in explicitly.

**AWD-15 — the exemption, and what a "negative record" is.** TDC stated the rule under their Long
Service heading, so it is a **per-award switch, off by default**: applying it to an Employee of the
Month award would be extending a policy they did not write. A negative record is *a disciplinary
action that reached a decision and was not dismissed* — a **draft** case is one nobody has been
formally accused in, a **dismissed** one is an exoneration, and a case **under appeal** still counts
because the decision stands until overturned. `DisqualifyingDisciplineMonths` is **null by default**,
which is the note read literally, so a value **relaxes** the rule rather than tightening it; a window
is a softening nobody asked for and defaulting to one would grant an amnesty nobody approved.

The assertion that carries this rule is not any of the ones about the switch working. It is **a
draft case must not disqualify**: an implementation that counted rows in the table would pass every
other disqualification assertion in the file and still deny a decade of service over an allegation
that has not been made.

**⚠⚠ Defect 17 — the sweep walked BACKWARDS down the ladder.** The duplicate guard excluded the
exact `(employee, rung)` pairs already granted, which looks equivalent to the correct rule and is
not. An employee first swept at twenty-two years is granted the twenty-year rung; on the next run
the fifteen- and ten-year rungs still satisfy *"reached, and not yet granted"*, so each subsequent
run granted one more, **descending**. Only the two employees in the fixture set past more than one
rung showed it — for everyone else the counts were identical either way. The rule is that a rung is
reachable only if it stands **above** the highest one the employee already holds: a milestone that
passed before the system was granting them is **missed, not owed**, and awarding it three seconds
after the twenty-year award would put two certificates on one wall in the wrong order.

**⚠ Defect 18 — the area's dominant class, produced while fixing its seventeenth instance.**
`DisqualifyOnDisciplinaryRecord` and `DisqualifyingDisciplineMonths` were added to the entity, given
a migration column, and read by the engine — with **no way to set or read either through the API**.
Every behavioural assertion about the switch would have passed against a field settable only in SQL.
Caught while writing the harness, by asking whether the thing being asserted was reachable at all.
The reachability assertions are kept deliberately apart from the behaviour ones for that reason.

**Two harness-environment corrections, both worth more than the slice.**

1. **The API was running in Development, not Staging.** `launchSettings.json` sets
   `ASPNETCORE_ENVIRONMENT=Development` and `dotnet run` applies the launch profile's variables
   **over** the ambient ones, so the environment variable did nothing. Two 409s came back as 500s
   carrying a raw stack trace, and the exception mapping they appeared to indict was correct all
   along. `--no-launch-profile` fixes it. **This was already recorded in memory and was not
   checked.** Every harness README under `dev-harness/` now carries the flag and a warning to
   confirm `Hosting environment: Staging` in the startup log rather than trusting the command.

2. **The discipline endpoints refuse a caller whose user account is not linked to an employee
   record**, and admin is not one. The HR actor is — and is also the actor who would really raise a
   case.

**⚠ What a live run finds, and why that is not a failure.** Measured 2026-08-21: of 5,579 employees
only **2,103** carry a `DateEmployed`, exactly **one** has ten completed years, and **none** has
fifteen. Every rung above ten has no live subjects at all. The engine is therefore proved against
fixtures, and the sweep result reports `employeesConsidered` and `withoutEmploymentDate` alongside
the qualified list so that a screen cannot present a result about the *data* as though it were a
result about the *staff*. This is the retirement-at-60 shape from area 9b and the `ExpectedHeadcount`
shape from area 8, for the third time.

**Also settled here.** The preview and the run are one calculation with a `commit` flag, so a screen
cannot promise something the button does not do. An **inactive** award type **refuses** to grant
rather than sweeping to nothing, because a silent empty result is indistinguishable from "nobody
qualified" and the two call for opposite responses. The unique index on `(AwardTypeId, Years)` is
filtered on `IsDeleted`, applying area 13's lesson rather than relearning it. And the award copies
the rung's value rather than referencing it, so repricing a rung cannot restate an award already
conferred.

---

### Slice 10 — FR-HR-113, the eligibility report. 2026-08-21, **65/65**, run twice. Migration `TDC0703HrAwardsReportCatalogue`.

Harness: `run-slice10.mjs`. Slice 9 re-run green at 91 through the restructure below. Area total:
**32 / 49 / 51 / 6 / 44 / 49 / 42 / 27 / 35 / 91 / 65 = 491 assertions.**

**The evaluator was restructured so the report cannot disagree with the button.** It now returns
**one verdict per employee** — `Eligible`, `Exempt`, `AlreadyGranted`, `NotYetAtMilestone`,
`ServiceUnknown` — and three surfaces project it: the sweep preview, the sweep run, and this report.
Computing the qualified set in one place and the report's rows in another would drift, and when it
drifts **it is the report that gets believed**. The assertion that holds this is not the matching
counts but the matching *names*: two lists of equal length can still name different people.

**The report shows the people it cannot answer for.** Measured 2026-08-21, 3,476 of 5,579 employees
have no employment date. A report of only the eligible would be a handful of rows and would read as
a statement about TDC's staff when it is a statement about TDC's employee records. `AlreadyGranted`
and `NotYetAtMilestone` are kept apart for the same reason: collapsed into "not eligible", a
twelve-year veteran and a new joiner become indistinguishable on the page.

**⚠ Defect 19 — the report was built, registered, executable, and unreachable.** The catalogue was
right, the provider was right, the seeder was wired — and `HrAwardsReportSeeder` only runs under
`seed-db` and at tenant provisioning, neither of which happens to a tenant that already exists. The
harness's first assertion caught it: **the report was not listed for the tenant.** The missing piece
was a fourth part of the pattern I had not read for — procurement and inventory each carry a
hand-written migration that inserts the rows for existing tenants. Third instance in this area of
*declared, implemented, called by nothing*, and the first one caught by an assertion written before
the run rather than by a symptom afterwards.

**⚠ Defect 20 — the refusals would have come back as a canned 500.** `ReportsController.ExecuteReport`
catches `InvalidOperationException` as a 400 carrying its text and **everything else as a 500 with a
fixed string**. `AwardsWorkflowException` derives from `Exception`, so both of the report's
refusals — *"which of these long-service awards did you mean?"* and *"no such award type"* — would
have reached the reader as *"An error occurred while executing the report."* The area's own exception
mapping only applies when an awards controller is the one in the path. Found by reading the calling
code before running, not by a failure. **The thing that decides your status code is not always the
thing you wrote** — the same lesson as slice 9's launch profile, in a different costume.

They are also now **400 rather than 404**, which is the truer reading: the resource this request
addresses is the *report*, and it was found; an award id inside `parameters` is data, so a bad one is
a malformed request.

**⚠ A field I renamed rather than shipped.** Projecting verdicts, I repointed
`DisciplinaryRecordsConsidered` at the count of exempt verdicts — which made the name false and
duplicated `disqualified.length`. It is now `DisciplinaryCheckApplied`, a genuinely distinct fact:
*"the rule ran and exempted nobody"* and *"the rule is switched off"* look identical on screen and
mean opposite things.

**Deliberate design notes.** The provider lives in the **API project**, unlike its Core-resident
siblings, because its gate is the ASP.NET `AwardsReadPolicy` — evaluated by two OR-ed handlers — and
re-deriving that from Core would be a third implementation of a rule that already has two, one that
would refuse a reader granted awards permissions through a custom role. The report carries, by
implication, who has a disciplinary record, so the gate has to be the real one. Paging is **in
memory**: completed-years arithmetic is C#, and pushing it into SQL would mean a second version of
the very calculation this report exists to share. With no award named and several long-service
awards it **refuses and names them**, because picking one would look authoritative while answering a
question nobody asked. And the statistics are computed over **every** verdict rather than the
filtered page, so a reader who has filtered to "Eligible" still sees how many people could not be
measured at all.

**One harness lesson.** The first draft looked for fixtures in page one. `pageSize` caps at 1000, the
tenant has ~5,600 serving employees, and `ServiceUnknown` sorts **last on purpose** — so the
"missing" row was the report being right and the harness reading it wrong. Fixture lookups now page.

---

### Slice 11 — the screens. 2026-08-22, **33/33**, run twice. No migration.

Harness: `run-slice11.mjs`. Whole area re-run: **32 / 49 / 63 / 6 / 48 / 49 / 42 / 27 / 35 / 91 / 65
/ 33 = 540 assertions**, all green. Frontend: `tsc --noEmit` **0 errors in awards**, ESLint clean,
all three routes registered in all three navigation surfaces.

**The probe came first, and it paid for itself before a line of TypeScript existed.**
`probe-ui-payloads.mjs` prints the real key set and value type of all 20 payloads the screens bind
to, building fixtures first so no list comes back empty — an empty array proves nothing about its
element shape, which is the difference between a probe and a guess. What it caught:

| | |
|---|---|
| **Four of my routes were fiction** | `levels/award-type/{id}`, `budgets/award-type/{id}`, `me/cycles`, `me/awards`. The service layer would have encoded the same guesses and compiled. |
| **Enums serialise as STRINGS** | with a parallel `*Name`. A type written from the C# enum would have been `number` and every filter would have matched nothing, silently. |
| **`AwardCommitteeMember.role` is free text** | max 100, not an enum. Passing `1` is a 400; a TS union would have type-checked. |
| **⚠ Defect 21** | `AwardCommitteeMemberDto.EmployeeNumber` on the DTO since the port, **never set by the mapper**. Every committee membership list showed names beside a blank column. The navigation is loaded — the name next to it proves that — so nothing ever failed. |

**⚠ A gap the probe found that no user had reported: `GET /api/awards/me/awards` did not exist.**
Every route on the employee surface was about *taking part* — nominating, voting, scoring — and there
was no way to see what you had actually **won**. It is the first thing anyone opens an awards screen
for. Added, along with `me/awards/long-service`, kept separate because a long-service record carries
a milestone and a service start date that a conferred award does not; merging them would produce a
table half of whose columns are blank on half the rows.

**D-9 delivered, and it turned out to have two halves.** `GET .../eligible` returned **two unbounded
lists** — every active employee gets a verdict, so 5,579 rows with reason arrays on a call a screen
makes the moment somebody picks an award. It is now one paged `items` list with a filter, and the
**ineligible keep their reasons**: filtering them out is the reader's choice, not the endpoint's,
because without them a mis-set rule and a correct one produce the same screen. The counts stay
computed over everybody — *"12 eligible"* beside a page of 12 rows says nothing, *"12 of 5,579"*
says everything.

The second half was found by a build error. `AwardsMeController` called the same service for the
**employee's candidate picker** — the one endpoint the entire workforce touches, opened the moment
anyone starts a nomination, returning most of 5,579 names. Now paged **and searchable**: nobody
scrolls five thousand names to find a colleague, they type one. It returns a plain
`PagedResult<AwardEligibilityVerdictDto>` rather than the desk's result DTO, because the desk's shape
carries `IneligibleCount` and this surface promises an employee never learns anything about why a
colleague failed — a plain list has nothing on it to leak by accident.

**⚠ My own grep hid that caller.** Sweeping for users of the removed lists I wrote
`grep -v "result.Eligible"` to drop the definition site, and it removed `return Ok(result.Eligible);`
— the one real caller. *An exclusion written to hide noise hid the signal.*

**⚠ I invented a third paging vocabulary and caught it in the probe output.** The new envelope said
`totalItems` / `hasNextPage` / `hasPreviousPage`; every other paged HR endpoint says `totalCount` /
`hasNext` / `hasPrevious`. The frontend already carries a warning comment about HR and Finance
disagreeing on exactly these names — a third dialect *inside one module* would be worse than either.
Renamed, with an assertion pinning it.

**Two claims of mine that were wrong, corrected in place.** I said the probe left *zero* unproven
shapes; it had left two — both review lists returned empty arrays, and "it returned 200" is not the
same as "I know what it returns". They are now probed after seating the actor on a committee and
scoring a nomination. And the slice-11 harness itself shipped a **vacuous assertion**: "each with a
reason" ran `[].every(...)` against an award nobody could fail, which returns true. A rule needs
somebody it refuses before it can be shown to explain itself.

**Also removed: a `.catch()` fallback that was hiding a 403.** The candidate-picker fixture was
wrapped in `.catch(() => login().then(retry))` because the previous block had left the winner's token
set — a fallback that would have swallowed any other refusal too. Slice 6 was caught doing this. Say
what the actor is instead of catching being wrong.

**Design notes.** `/hr/awards/me` is behind **no HR permission**, deliberately: nominating, voting
and scoring are acts every employee performs, entitlement read off the record rather than granted,
and gating it would lock the workforce out of the feature the area exists for — the area-15b trap.
The sidebar entry carries that note so nobody "fixes" it later. HR's nav entry is **"Awards &
Recognition" with a `Medal` glyph** because `/procurement/awards` already exists using `Award`, and
two indistinguishable sidebar rows is its own defect. Screens render an unpriced long-service rung as
**"not set"**, never `0.00`: a null there is a question TDC has not answered, and a zero answers it
for them.

**Recorded, not fixed: cross-module defect #4.** `tsc --noEmit` reports **19 errors, all in
Inventory**, none touched by HR. The cost is not the 19 — it is that a gate which is never green
stops being a gate, so the twentieth error lands unnoticed. Written up in
`docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` with the two worth looking at first: a duplicated
`isStockingUnit` whose two declarations disagree about optionality, and a dialog that renders three
fields its item type does not declare.

---

### Slice 12 — the content audit. 2026-08-22, **78/78**, run twice. No migration.

Harness: `run-slice12-audit.mjs`, **76 routes**, every one asserted for content rather than status.
Whole area re-run: **32 / 49 / 63 / 6 / 48 / 49 / 42 / 27 / 35 / 91 / 65 / 33 / 78 = 618
assertions**, all green.

**The first pass failed 19 of 78, and every failure sat behind a 200 or a plausible 4xx.** That is
the whole argument for this slice, and it is area 11's lesson arriving on schedule: a status-code
harness proves the *gate*, not the *feature*.

**⚠ Defect 22 — two endpoints that had never returned a row.** `committees/{id}/members` and its
`/active` twin ordered by `Employee.FullName`, a **computed property with no column behind it**. EF
cannot translate it, so both threw `InvalidOperationException` on every call and answered 400 — on
the endpoint whose entire purpose is listing a committee's members. The identical mistake is
documented in a comment at the top of that same repository file: found once, fixed there, left
standing here.

Sweeping the whole data layer afterwards found **four** uses of `FullName` inside `OrderBy`/`ThenBy`:
the two above, one in Procurement (recorded as cross-module defect #5, not fixed — not ours), and two
that are perfectly safe because `EmployeeReferee.FullName` and `JobCandidateReferee.FullName` are
real mapped columns. **The name alone does not tell you which kind you have.** That is why this class
survives review.

**⚠ Defects 23–28 — six missing `.Include`s, all the same shape:** a `*Name` that exists, serialises,
and is always blank.

| endpoint | blank | cause |
|---|---|---|
| `committee-members/employee/{id}` | `employeeName` | `Employee` not loaded on a list *about* that employee |
| `nominations/{id}/reviews` | `nominationNumber` | `AwardNomination` not loaded |
| `reviews/reviewer/{id}` + `me/reviews` | `reviewerName` | three navigations loaded, **not the one the query is keyed on** |
| `team-nominees/employee/{id}` | `employeeName` | `Employee` not loaded |
| `long-service/{id}` | `awardTypeName` | `AwardType` not loaded |
| `long-service/employee/{id}` | every name | no navigation loaded at all |

**⚠ Defect 29 — `budgetCode` was minted by nothing.** Not `[Required]` on the create DTO and
generated nowhere, so every budget carried an empty string: blank on four list endpoints, and
invisible to `GetByBudgetCodeAsync`, which is the only lookup built for it. Every other numbered
record in this area mints its own identifier; this one was left to the caller and then never asked
for. The service now generates `AWB-{year}-{6 hex}`.

**Four of the nineteen failures were the audit's own, not the product's**, and separating them
mattered more than fixing them: I sent `Eligibility` to a route parameter that binds `AwardScope`
(the wrong enum entirely), checked `isActive` where the payload says `isActiveMember`, called two
lookups with a made-up GUID and recorded the correct 404 as a failure, and treated "you have not
voted" — also a 404 — as a fault. *An audit that cannot tell "this endpoint is broken" from "I asked
for something that does not exist" is measuring itself.*

**⚠ I reached for a migration to make a red assertion green.** The last failure was
`budgets/year-range` reporting a blank code — from rows created before the generation fix. I wrote a
backfill migration for it. The user asked whether it was necessary, and it was not: `rebuild-db`
stamps migrations as applied **without executing them**, an empty table makes it a no-op, and — the
decisive fact — **the awards module has never been usable**, so no real budget exists anywhere to
backfill. The blank rows were my own harness fixtures.

The assertion was the thing that was wrong. It checked `rows()[0]` — whichever row happened to sort
first, which on a shared tenant is some other run's leftover. It now finds the budget *this run
created* and checks that. **Asserting on data the harness did not create proves as little as
asserting on an empty list**, which is a rule stated at the top of the very file that broke it.

**One list is exempt, and named rather than silently passed.** `nominations/{id}/attachments` needs a
real file through the upload gate and this harness has no clamd stub; its row shape is proved by the
attachment slice. An unproven shape nobody names becomes a shape nobody checks.

---


---

## ⚠ Open defect — award attachments bypass the upload gate (found 2026-08-22, slice 13)

**Not fixed. No UI was built on it, deliberately.**

`POST api/Awards/nominations/{id}/attachments` and `POST api/Awards/{id}/attachments` take
`FileName` and **`FilePath`** as ordinary body fields on `CreateAwardNominationAttachmentDto` /
`CreateAwardAttachmentDto`. The caller supplies the path; the server stores it. **No file is
uploaded through these endpoints at all.**

Compare an area that was fixed — separation posts multipart `FormData` at
`{id}/documents`, so the file goes through the shared upload gate and is scanned before anything is
recorded.

### Why this is not merely untidy

| | |
|---|---|
| **The path is caller-supplied** | A client can point an attachment row at any path the server can read. What comes back on download is whatever that path holds. |
| **Nothing is scanned** | The upload gate is where virus scanning happens. These endpoints never reach it. |
| **The record can lie** | An attachment can name a file that was never uploaded, or that belongs to another tenant's record. |

### Why no screen was built

An attachments panel here would be a form whose whole job is to submit a server file path. Building
it would turn a dormant hole into a reachable one — the opposite of what the UI work is for. The
shared `AttachmentsPanel` component is ready and the screens have a place for it; what is missing is
the endpoint, not the front end.

### What a fix needs

1. Change both endpoints to accept `IFormFile` and route it through the same upload gate the
   separation and probation attachments use — the one the HR attachment slice already wired four
   other dead paths onto. Awards was ported afterwards and was not included.
2. Drop `FilePath` from the create DTOs entirely. A path the caller can name is the defect; keeping
   it as "optional" keeps the hole.
3. Re-point the download route at the stored, server-generated path.
4. Then build the attachments panel — it is a small screen once the endpoint is honest.

Until then `nominations/{id}/attachments` and `{id}/attachments` remain read-only in the audit, which
is why slice 12 records the attachment list as **exempt** from its empty-list rule rather than
proving its row shape.

## Area 14 — complete

**13 slices, 618 assertions, 29 defects.** All fifteen change-document requirements (AWD-01 to
AWD-15) and FR-HR-113 delivered; see the tables in section 4 for what each slice closed.

**The area's signature defect, stated once for whoever reads this next.** Twenty-nine defects, and
the largest single group — more than half — is one shape: *a field that exists, type-checks,
serialises, and carries nothing.* It is written correctly, stored correctly, and quietly missing from
the payload. The write succeeds, the status code is right, the database is correct, and **nothing
fails.** Every instance was found by asserting content; not one would have been found by asserting
status.

The second group is its sibling: *whole features that had never once executed* — `AssignToCommitteeAsync`,
`CreateFromNominationAsync`, `GetPendingReviewsAsync`, the FR-HR-113 report, and two committee reads
that threw on every call. Declared, implemented, wired, and unreachable.

**What is still owed, and by whom.** Two answers are TDC's rather than ours: what each long-service
rung is worth, and whether severity should matter to the disciplinary exemption — the latter needs a
severity column on `StaffDisciplinaryActions` before it is even expressible. Both are in
`docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md`. Every money event is registered in
`docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` for the post-module sweep; **nothing in this area posts to a
ledger.** `LongServiceAward.LeaveId` remains a hook with no writer.

---

### Slice 13 — UI parity. 2026-08-22. Frontend only — no migration, no backend change, 618 assertions unchanged.

**17 awards screens.** `tsc --noEmit`: **0 errors in awards**; ESLint clean; every route registered
in all three navigation surfaces. The whole harness suite re-run green.

**⚠ This slice exists because slice 11 shipped read-only surfaces and reported "screens" as done.**
The user asked *"so we're done with all the screens for area 14?"* — the honest count was **64 write
endpoints, 2 of them reachable**. Nominating, voting, scoring and conferring — the four acts the area
exists for — could not be performed from the UI at all. Slice 11's own log cites area 12's lesson
that *"the create form is an actor audit"*; it then didn't build one.

| surface | screens |
|---|---|
| Employee | landing, nominate, nomination detail, vote, score |
| Desk | register, nomination detail, confer directly, award detail, who qualifies, results, long service |
| Admin | setup, catalogue, award detail (levels/scope/budgets), cycles, committees |

**The user asked twice more, and was right both times.**

*Second ask.* Seven service methods had no caller, and the worst was structural: **after a vote
closed or a committee scored, nobody could see who won.** `getVoteResults` and `getCommitteeResults`
existed and no screen called either — the outcome of the entire process had no surface. Also found:
the nominate screen told employees *"individual members are added afterwards by the awards desk"* and
**the desk had no such UI** — a promise written into copy that the product could not keep; long-service
awards were granted by the sweep and then had nowhere to go (`long-service/{id}/process` was reachable
by nothing); a committee member could score and never see the result; and edit existed nowhere.

*Third ask.* The check being run was **service → screen**, which structurally cannot see endpoints the
service never wrapped. Switching to **endpoint → service → screen** found **24 unwrapped routes**,
including **17 delete routes against one wrapper**. A whole verb was missing. Also missing: a vote
could be *replaced* but never *withdrawn* (different acts — one moves a vote, the other removes a
voter), `pending/presentations` (an award could be conferred, paid, and quietly never handed over),
and `long-service/upcoming`.

Sixteen routes remain unwrapped and both groups are deliberate: **7 attachment routes** (see the open
defect above) and **9 convenience variants** of endpoints already wrapped — `committees/active`,
`levels/active`, `types/category/{category}` and their kin, all filtered client-side from a list that
already carries the flag.

**⚠⚠ The enum trap, found by checking a comment I had written.** The scope picker's comment said
"4 = Employee scope". It was correct **by luck** — the number came from a harness fixture, not from
reading the enum:

| value | `AwardTargetType` (the DTO field) | `AwardScope` (the route parameter) |
|---|---|---|
| 1 | OrganizationUnit | **Employee** |
| 2 | Position | Position |
| 3 | StaffLevel | StaffLevel |
| 4 | **Employee** | OrganizationUnit |

Same four members, **opposite order**, and the middle two coincide — so a mix-up is **right half the
time**, which is the worst possible shape: it survives casual testing and mis-scopes only at the two
ends. Reasoning it out from the parameter name (`scope`) would have produced 1 and scoped awards to
org units while believing they were scoped to people. Now a named `TARGET_TYPE` map with that table
beside it.

**⚠ The probe was extended after the fact, and immediately proved the point.** The scope picker's
three option sources were built from types read out of the frontend *source*, not from payloads read
off the wire — the exact shortcut `probe-ui-payloads.mjs` exists to prevent. Adding them, **two of
the three routes I wrote were wrong**: `/StaffLevels` is `/hr/staff-levels` and `/OrganizationUnits`
is `/OrganizationUnit`, singular. Caught by reading `baseUrl` out of each service. The label fields
are now confirmed against live payloads — `title`, `name`, `name` — rather than inferred.

**Design decisions carried in the screens rather than left to be discovered.**

- Cycles hide the voting-window fields unless the award is decided by a staff vote — the API refuses
  one either way round, so the form states the rule before the refusal does.
- An empty electorate scope means **everyone** votes, and the scope tab says so. An empty table would
  otherwise read as an oversight.
- Blank money stays **null**, never `0` — on a milestone rung and an award alike.
- A team nomination shows **why** it cannot be conferred rather than offering a button whose write
  would violate a foreign key.
- Quorum is asked of the **server**, not counted client-side: a count that disagreed with the scoring
  engine would tell HR a committee was ready when the engine thinks otherwise.
- Only a **draft** cycle can be deleted; a published one is cancelled, because the nominations raised
  against it must keep something to belong to.
- Deleting an award type is gated on `isTypeInUse` asked of the server — not guessed from
  `awardCount`, which counts only conferred awards while nominations, cycles, levels, targets and
  budgets all reference a type too.

**What UI verification here does and does not mean.** There is no browser automation in this
environment, so "verified" means `tsc`, ESLint, route resolution and payload shapes proven against a
running API. It does **not** mean anything has been seen to render.
