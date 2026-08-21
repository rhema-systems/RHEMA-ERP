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
| 5 | The vote | AWD-04, AWD-05, AWD-06, AWD-10 — the vote model, the electorate, one-vote-per-voter, the window gate. |
| 6 | Committee scoring | D-2 model change, AWD-12, AWD-13 highest-average outcome. Test with identical scores and ≥3 reviewers. |
| 7 | Direct and automatic selection | AWD-07, AWD-08 — management selection, and nomination derived from performance/targets. |
| 8 | The award is conferred | Nomination → `EmployeeAward`; presentation; certificate; the payment record (D-7). |
| 9 | Long service | AWD-14 milestones, AWD-15 disqualification (D-6), the sweep — asserted empty on live data. |
| 10 | FR-HR-113 | The eligibility report, on the catalogue+service+seeder pattern used by procurement and inventory. |
| 11 | Screens | `/hr/awards`, `/hr/awards/me`, `/administration/hr/awards`. UI-payload probe mandatory. **Also D-9: page the eligibility endpoint.** |
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
