# HR Area 14 — Staff Awards & Recognition: Build Plan

**Opened 2026-08-21.** Area chosen by the user after the post-9b survey. The area is unusual in
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
| **Backend proven** | **Nothing.** Every awards table holds 0 rows |
| **Frontend today** | none |
| **Status** | plan written, no code |

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
  `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` for the post-module sweep. Do **not** invent an HR-side
  posting mechanism.

---

## 4. The change document — requirement extraction

Source: `D:\Rhema\TDC ERPS\Staff Awards Changes.pdf`, three sections. It is written as discussion
notes, so the requirements below are the extraction; each is given an ID so slices can cite it.
**Where the extraction interprets rather than quotes, section 5 records the decision.**

### 4.1 Nomination

| ID | Requirement | Exists? |
|---|---|---|
| **AWD-01** | HR sets up the eligibility criteria **first**; the criteria then qualify a set of employees | ✅ model exists (`AwardType` + `AwardTypeTarget`), never executed |
| **AWD-02** | Employees **or** management nominate — including employees nominating managers | ⚠ `NominatedById` exists; no employee-facing route, and the id is caller-supplied |
| **AWD-03** | Employees nominate **through the staff portal** | ❌ no self-service surface for awards |
| **AWD-04** | Qualified/shortlisted nominees are **displayed on the portal** for voting | ❌ |
| **AWD-05** | Staff **vote** on the nominees; the winner is decided by the vote | ❌ **nothing exists** |
| **AWD-06** | The electorate is configurable — "a section of the employees **or all of them**" | ❌ |
| **AWD-07** | **Not every award goes to a vote.** Some are a direct management selection | ❌ no selection-method discriminator |
| **AWD-08** | Some nominations arise from **performance or a target reached** | ⚠ `AutoGenerateNominees` flag exists, no engine |
| **AWD-09** | A nominator states a **justification or reason** | ✅ `AwardNomination.Justification` |
| **AWD-10** | A **voter** may also state a justification or reason | ❌ part of the missing vote model |
| **AWD-11** | Awards are **listed on the portal for nomination before voting opens** — i.e. a nomination window, then a voting window | ❌ no dated cycle; only `Year`/`Quarter`/`Month` ints on the nomination |

### 4.2 Award committee

| ID | Requirement | Exists? |
|---|---|---|
| **AWD-12** | Committee members **score** a nomination | ❌ `Approved` is `bool?` |
| **AWD-13** | The winner is the nominee with the **highest average score** | ❌ |

### 4.3 Long service

| ID | Requirement | Exists? |
|---|---|---|
| **AWD-14** | Define the **basis** for long-service awards (the milestones and what each carries) | ⚠ `MinServiceYears`, `YearsOfService`, `MilestoneDate` exist; TDC's actual values **not supplied** |
| **AWD-15** | **Any negative record — such as disciplinary action — exempts an employee** | ❌ no link to area 9 |
| **FR-HR-113** | Report on long-service-award eligibility (FRD, Mandatory) | ❌ no report definition |

**Six requirements need new model. Three need engines. One (AWD-14) still needs TDC data.**

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
`docs/HR-FINANCE-INTEGRATION-BACKLOG.md` and leave the plain payment fields as they are.

**D-8 — AWD-14 milestones are configurable, defaulted, and raised with TDC.** The document says
only "define the basis"; the milestones and what each carries are not stated. **Settled with the
user 2026-08-21:** build the ladder fully configurable through the admin surface, default it to
**10 / 15 / 20 / 25 / 30 years**, and add the real values to
`docs/HR-OPEN-QUESTIONS-FOR-TDC.md`. Nothing is hard-coded, so TDC's answer is a data change and
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

## 7. Slice plan

Provisional — slices are re-cut as findings land, and the log records any change.

| # | Slice | Delivers |
|---|---|---|
| 0 | Prove the ground | Run the happy path across all eight endpoint groups. Establish which of the 100 endpoints actually execute. Expect casualties. |
| 1 | Identity | Token tenant + token actor; `HrControllerBase`; remove all 56 caller-supplied id parameters; `HR.Awards.*` policies. |
| 2 | Types, levels, targets, budgets | The configuration surface, with the selection method of D-3. |
| 3 | Cycles and eligibility | D-4 cycle entity with windows; AWD-01 eligibility evaluation producing the qualified set. |
| 4 | Nomination | AWD-02, AWD-09; management route and the `api/awards/me` employee route (D-1). |
| 5 | The vote | AWD-04, AWD-05, AWD-06, AWD-10 — the vote model, the electorate, one-vote-per-voter, the window gate. |
| 6 | Committee scoring | D-2 model change, AWD-12, AWD-13 highest-average outcome. Test with identical scores and ≥3 reviewers. |
| 7 | Direct and automatic selection | AWD-07, AWD-08 — management selection, and nomination derived from performance/targets. |
| 8 | The award is conferred | Nomination → `EmployeeAward`; presentation; certificate; the payment record (D-7). |
| 9 | Long service | AWD-14 milestones, AWD-15 disqualification (D-6), the sweep — asserted empty on live data. |
| 10 | FR-HR-113 | The eligibility report, on the catalogue+service+seeder pattern used by procurement and inventory. |
| 11 | Screens | `/hr/awards`, `/hr/awards/me`, `/administration/hr/awards`. UI-payload probe mandatory. |
| 12 | Content audit | Every GET asserted for content, not status. **Run it twice.** |

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
`docs/HR-OPEN-QUESTIONS-FOR-TDC.md` per decision D-8.
