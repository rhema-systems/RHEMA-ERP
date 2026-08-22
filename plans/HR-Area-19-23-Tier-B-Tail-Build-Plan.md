# HR Areas 19–23 — the Tier B tail: Build Plan

Organization extras · Employee satellites · Unions · HR Settings · Dashboards

---

## 1. How to use this document

Section 3 is **measured**, not remembered — every count in it came from a live query against
`ErpSystemDB` on 2026-08-22, and every endpoint list came from reading the controller. Read
section 3 before writing a line of code; the whole point of this bundle is that five small
areas were never *built*, only *ported*, and the difference is only visible in the numbers.

Section 8 is the log. Each slice appends: what it found, what it changed, the assertion count,
and anything the next slice must not re-derive.

**Conventions that apply here and are not restated per slice:** the user runs all builds; I
stage and hand over the commit message; migrations are scaffolded by the user, listed by me
in `FastBuildMigrationMetadata`, and updated by the user; harnesses run against Staging with
the JWT key passed; every harness runs **twice**.

---

## 2. Status at a glance

| Area | Surface | Endpoints | Screens today | State |
|---|---|---|---|---|
| **19** | `api/Organogram` | 5 GET | 0 | engine looks sound, never rendered |
| **19** | `api/hr/teams` | — | — | **added in slice 4b**: 12 endpoints. `Team`/`TeamMember`/`TeamMemberHistory` had tables and EF config but no writer of any kind |
| **19** | `api/OrganizationUnitHistory` | 6 GET | 2 | **done**: slice 3 made it writable, slice 5 rendered it and found six more defects |
| **20** | `api/external-associates` | 11 | 1 of 11 (a picker) | register never built |
| **20** | `api/hr/employee-relievers` | 4 | 0 | **0 rows; zero readers anywhere** |
| **20** | `api/facility-services` | 5 | 0 | medical-owned, 2 rows |
| **20** | `api/employee-certificates` | 9 | ✅ area 7 | **done, out of scope** |
| **20** | `api/probations` | — | ✅ area 15b | **done, out of scope** |
| **21** | `api/hr/unions` | 10 | 0 | **0 rows, 0 agreements** |
| **22** | `api/hr/company-profile` | 2 | 0 | **0 rows**, read by 3 letter/email services |
| **22** | `api/hr/policy-settings` | 2 | 0 | 1 row, ~25 knobs, read by 11 services |
| **23** | `api/HRCycleDashboard` | 3 | ✅ area 5 | **done** |
| **23** | per-area dashboards ×7 | — | ✅ all built | **done**; only the `/hr` home is thin |

**Net unbuilt surface: 47 endpoints, 0 screens.** Areas 23 and half of 20 are already closed by
earlier areas — the bundle is smaller than its numbering suggests, and the weight is in 19, 21, 22.

---

## 3. Ground truth — measured 2026-08-22

### 3.1 Row counts, DEFAULT tenant, `ErpSystemDB`

```
Unions                          0
CollectiveBargainingAgreements  0
EmployeeRelievers               0
OrganizationUnitHistories       0
CompanyProfiles                 0
CompanyHrPolicySettings         1
ExternalAssociates              7     (ExternalAssociateDataSeeder)
FacilityServices                2
EmployeeCertificates           13     (area 7)
OrganizationUnits              41
Employees                    6232
```

Five of the nine stores in scope are **empty**. Two of those five are empty for a reason the
survey found, not for want of use — see D-1 and D-4.

### 3.2 What the port already got right

All six services were tenant-hardened by the blanket sweep `79a98afa` and carry the RHEMA
tenant-scoping comment. Expect **no** SQL 547 on the ordinary create paths, unlike areas 11–14.

- `OrganogramService` — five projections (units / positions / people / locations / teams) onto one
  `OrganogramNodeDto`, headcount rolled up per unit, tenant-scoped, `.Include`d, and its
  `HeadEmployee?.FullName` is evaluated **after** `ToListAsync`, so it does not hit the
  `FullName`-in-`OrderBy` trap from area 14. `TdcOrganogramSeeder` exists and the 41 units are real.
  This is a **harden-and-render** job, not a resurrection.
- `EmployeeRelieverService` — genuinely well built: self-reliever guard, per-employee priority
  uniqueness, `.Include`d reload after every write. Its problem is not quality (see D-3).
- `CompanyProfileProvider` — degrades to `Tenant` + `Company:*` config when no row exists, and is
  documented never to throw. So offer letters and probation letters **render**; they just render
  something no one in HR has ever been able to author.

### 3.3 The measured holes

**D-1 · The org-unit audit trail cannot write a row.** `OrganizationUnitService.MoveUnitAsync`
(`OrganizationStructureServices.cs:753`) and `ChangeHeadEmployeeAsync` (`:802`) both construct
`new OrganizationUnitHistory { … }` **without stamping `TenantId`**. `OrganizationUnitHistory`
is a `TenantEntity` and the DbContext auto-stamp is inert ([[hr-tenancy-stamping-gap]]), so the
save takes an FK violation. Both are exposed — `POST api/OrganizationUnits/{unitId}/move` and
`…/change-head` (`OrganizationUnitController.cs:342,369`) — and **neither has a frontend caller**,
which is why nobody has ever seen the 500. This single defect explains all six read endpoints
returning nothing, forever. Slice 0 must reproduce it before slice 3 fixes it.

**D-2 · The audit trail is bypassable even once it works — confirmed by reading, not assumed.**
`OrganizationUnitService.UpdateAsync` (`:607`) ends in `updateDto.UpdateEntity(entity)`, and that
mapping (`OrganizationStructureMappingExtensions.cs:333`) assigns **both** `ParentUnitId` **and**
`HeadEmployeeId`. It writes **no history row**. So the two operations the audit trail exists to
record are each reachable two ways: through the endpoint that records them and cannot save (D-1),
or through the plain `PUT` that saves and records nothing — and the `PUT` is the one the units edit
screen already calls. **The only working path is the silent one.**

**D-2b · Reparenting leaves every descendant's `Path` stale.** Both `UpdateAsync` (`:652`) and
`MoveUnitAsync` (`:773`) recompute `Path` for the moved node only. `UpdateAsync` even says so —
*"descendants are handled elsewhere if needed"* — and nothing, anywhere, handles them.

⚠ **Corrected in slice 3, and the correction matters: this is narrower than first written.** The
first draft of this entry said a stale path "no longer resolves". Nothing resolves paths.
`GetDescendantsAsync` and `GetAncestorsAsync` walk `ParentUnitId` recursively and never consult
`Path`; `Path` feeds `Depth` on the read models and nothing else. So the defect is that a moved
unit's descendants report the **wrong depth**, not that hierarchy queries break. Still worth fixing,
and fixed — but the reader sweep the plan demanded is what kept the claim honest.

**D-2c · The same operation obeys two contradictory hierarchy rules.** `MoveUnitAsync` requires the
unit to sit **exactly one level** below its new parent (`:736`); `UpdateAsync` allows level-skipping
and only requires it to be **somewhere below** (`:647`). Same move, two endpoints, different
answers. Slice 3 must pick one — and the `UpdateAsync` rule is the one the live data can satisfy.

**D-3 · The reliever roster has no reader.** `EmployeeReliever` is consumed by **nothing**. Leave
carries its own per-request `RelieverId` / `RelieverEmployeeId` (`LeaveEntities.cs:353,446`) and
`SimpleWorkflowService` reads those, not the roster. So the pre-defined reliever table is a
standalone store: 0 rows, 4 endpoints, no screen, and no downstream effect even if populated.
**This is a decision, not a bug** — see Decision 3.

**D-4 · Nobody can author the company's own identity.** `CompanyProfiles` is empty and its `PUT`
has never run. `ICompanyProfileProvider` feeds `OfferLetterService`, `ProbationLetterService` and
`TemplatedEmailService` — three closed areas render documents from fallback values today.

**D-5 · Twenty-five policy knobs, one row, no editor.** `CompanyHrPolicySettings` carries
compulsory/voluntary retirement age, gender-specific retirement, default probation months,
resignation and termination notice days, procedural absence days, five lead-day alert windows,
the long-service milestone list, default currency, fiscal year start, minimum working age,
`BudgetEnforcementMode`, `EstablishmentEnforcementMode`, the four succession fit weights and the
plan-number prefix. Eleven services read them — probation, separation ×2, succession ×3,
requisition, long-service, retirement calculations. **Every closed area is running on defaults
nobody can change.** This is the highest-leverage screen in the bundle.

**D-6 · Union writes answer with an unloaded graph.** `CreateAsync`/`UpdateAsync` return
`entity.ToDto()` against an entity fetched without agreements, while the reads use
`GetAllWithCountsAsync` / `GetByIdWithAgreementsAsync`. The classic stale-nav-on-write shape from
[[hr-ported-list-read-bugs]]. Harmless on create (a new union has no CBAs); wrong on update.
Confirm against `UnionDto` in slice 5.

**D-7 · Eleven of twelve external-associate endpoints are unreachable.** Only `search` is consumed,
by `PanelMemberPicker` in recruitment. The register, paging, activate/deactivate and
lookup-by-number have never been called from anywhere.

**D-8 · Authorization.** Every controller in this bundle is a bare `[Authorize]` except
`FacilityServicesController`, which is correctly on `HrPermissions.Medical*`. Policy settings is
the sharpest case: today **any authenticated user** can `PUT` the tenant's retirement age and flip
`EstablishmentEnforcementMode` from `Block` to `Warn`. That is an area-22 fix, not a W3 deferral.

---

## 4. Requirement backing

Unlike areas 9b–17, this bundle carries **no Mandatory FRD requirement of its own**. It is
infrastructure under requirements already delivered elsewhere:

| Surface | What it is actually under |
|---|---|
| Policy settings | the tuning surface for FR-HR-031 (probation length), 093 (retirement age), 136 (establishment enforcement), the succession fit model, and every lead-day reminder sweep in areas 8/9/9b/15b |
| Company profile | the letterhead behind FR-HR-046 offer letters and the FR-HR-032 confirmation letter |
| Organogram | FRD §2.3.2 — the HR & Administration lines *are* defined "from the organogram" |
| Unit history | the audit trail under FRD §A1.1 organisation management |
| Unions | peripheral: CBA cited in §1 references, union dues in payroll, union consultation notes in discipline. **No requirement asks HR to compute anything.** |

Consequence for scope: unions gets a **register**, not a module. Payroll owns dues.

---

## 5. Decisions

**Decision 1 · Areas 23 and the done half of 20 are out of scope.** `HRCycleDashboard` and all
seven per-area dashboards are built and reachable. `employee-certificates` closed in area 7,
`probations` in area 15b. Area 23's only residue is the `/hr` landing page, which today shows
employee counts over a nav grid. That becomes one slice, not an area.

**Decision 2 · Policy settings is admin-gated; company profile is HR-gated.** Policy settings
changes behaviour across eleven services and eight areas — it belongs under
`/administration/hr/settings` behind the administration roles. Company profile is HR's own
letterhead and sits beside it but opens to HR.

**Decision 3 · The reliever roster gets a screen and one reader.** Building the roster without
wiring it to leave would ship a third empty store. The reader is the leave request form: when an
employee has an active roster, the reliever picker defaults to priority 1 and offers the rest
first. Leave's own `RelieverId` stays authoritative per request — the roster is a *default
source*, never an override. This is additive to area 2 and touches its create form only.

**Decision 4 · Facility services lands on the area-11 healthcare-facility detail, not a new
screen.** `FacilityServicesController` extends `MedicalControllerBase` and is already on the
medical policies. Per [[she-medical-ownership-boundary]] it is medical's, and it belongs as a tab
on the facility it describes.

**Decision 5 · The organogram is one screen with five views**, switched by tab, not five screens.
The DTO is uniform by design; anything else duplicates a chart component five times.

> ⚠ **Reopened during slice 4, and the reopening changed the answer.** Slice 4 measured that the
> fifth view projected a table with no writer anywhere in the repository and dropped the tab, on the
> grounds that a view which can never contain anything is a broken promise on a screen. That was the
> wrong fix to the right observation: the entities, tables and EF configuration all existed and only
> the application layer was missing. **Slice 4b built the teams register instead and the tab came
> back.** Five views, as originally decided — see Decision 7.

**Decision 6 · Unit history renders as a tab on the unit detail plus one register.** A change log
without the thing it logs is unreadable; a register is still needed for the date-range and "what
changed this quarter" reads the controller already offers.

**Decision 7 · Teams get a full register, not a stub — membership included.** Taken in slice 4b,
after Decision 5 was reopened. A team with no members is a label, and `Team.MemberCount` is
`[NotMapped]` precisely so a service fills it, so membership is the feature rather than an extra.
`TeamMember` and `TeamMemberHistory` are therefore in scope with `Team`. Two fields stay unbuilt on
the form deliberately — `ShiftId` and `LocationId` — because each needs a picker from another area
and neither is worth guessing at before TDC says whether its teams use them.

⚠ **Teams carry no FRD requirement of their own**, and that remains true after building the
register. The entity is clearly modelled for a matrix organisation — `AllocationPercent`,
`IsPrimary`, `MaxMembers`, `ProjectCode`, `CostCenterCode` — so the capability is real, but whether
TDC runs project teams, task forces or committees is still an open question for them. The register
being empty is now a statement about TDC's data rather than about the product.

---

## 6. Scope

**In:** organogram screens; unit-history fix + screens; the external-associate register; the
reliever roster + its leave wiring; the union and CBA register; company profile; policy settings;
the `/hr` home dashboard; facility services onto area 11; authorization for all of the above.

**Out:** union dues and any deduction (payroll owns — [[payroll-ownership-boundary]]);
org-authority modelling, i.e. actually populating unit heads and manager links — the screen to do
it is in scope, the data programme is not ([[hr-deferred-modules]]); GL posting (none of this moves
money, so nothing is even owed to `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`); the W3 permission
*seeding* sweep, which stays a separate cross-cutting workstream.

**Risk carried:** slice 3 changes an area-1 service (`OrganizationStructureServices`) that every
later area reads through. Area 13's lesson applies directly — *a correct fix can invalidate an
assumption held elsewhere*. Before merging slice 3, grep every reader of `HeadEmployeeId`.

---

## 7. Slice plan

| # | Slice | Area |
|---|---|---|
| 0 | **Prove the ground.** Call all 47 endpoints, record status + body. Reproduce D-1, confirm D-2 and D-6. Diagnostic only, no production code. | — |
| 1 | **Company profile.** Gate, the fields the three letter services actually read, editor screen. | 22 |
| 2 | **Policy settings.** Admin gate, ~25 knobs grouped by the area that reads each, validation (fit weights sum, retirement age ordering). | 22 |
| 3 | **The unit audit trail.** Stamp the tenant (D-1), write history from the update path and close the bypass (D-2), cascade `Path` to descendants (D-2b), reconcile the two hierarchy rules (D-2c). | 19 |
| 4 | **Organogram.** One screen, the chart component, the split gate (D-8), the rollup (D-21), on-strength headcount (D-27), stable order (D-28), a cycle guard. | 19 |
| 4b | **Teams register.** Team + membership + history CRUD, the filtered unique index (D-29), screens, and the organogram's fifth view restored. | 19 |
| 5 | **Unit history screens.** Register + the unit-detail tab. | 19 |
| 6 | **Unions and CBAs.** Register, detail, agreement collection, D-6 fix. | 21 |
| 7 | **Reliever roster.** D-9 (the unfiltered unique index), roster screen + the leave-form default (Decision 3). | 20 |
| 8 | **External associates.** D-10 (the reissued number), register, detail, activate/deactivate — the 10 unreached endpoints. | 20 |
| 9 | **Facility services** onto the area-11 facility detail. | 20 |
| 10 | **The HR home.** Real headline figures on `/hr`, and a reachability sweep of every HR route. | 23 |
| 11 | **Content audit.** Every GET, by id, asserting content not status. Run twice. | all |
| 12 | **UI parity.** endpoint → service → screen, the area-14 completeness check. | all |

Migrations so far: **two.** `IX_EmployeeRelievers_EmployeeId_Priority` (slice 7, pending) and `20260822133728_FilterTeamCodeUniqueIndexOnSoftDelete` (slice 4b, applied). Both are the same defect — a unique index over a soft-deleting store — which is now the most reliably recurring shape in this bundle.

Expected migrations: **at least one, contrary to the first draft of this plan.** Slice 0 found D-9,
and a unique index that must exclude soft-deleted rows is a schema change:
`IX_EmployeeRelievers_EmployeeId_Priority` needs a `WHERE IsDeleted = 0` filter. Slice 8 may need
the same for `AssociateNumber`, depending on whether D-10 is fixed by filtering the generator or by
making the column genuinely unique. The user scaffolds each one; I list it in
`FastBuildMigrationMetadata` and the user updates.

Harness: `D:\Rhema\TDC ERPS\dev-harness\hr-tierb-tail\`, following the area-14 layout —
`api.mjs`, `setup.mjs`, `run-slice0..12`, `probe-ui-payloads.mjs`, `README.md`.

**Non-negotiable, carried from areas 12–14:** write `probe-ui-payloads.mjs` before any TypeScript;
give every probed list real data or record it UNPROVEN; never assert on data the harness did not
create; run the content audit twice.

---

## 8. Log

### Slice 0 — prove the ground. 2026-08-22, **16/16**, run twice. Diagnostic only, no production code changed.

71 calls across the whole bundle under three actors. Harness:
`dev-harness/hr-tierb-tail/` (`api.mjs`, `setup.mjs`, `run-slice0.mjs`, `README.md`).

**Corrections to section 2 and 3, from execution:**

- The endpoint count is **47, not 44** — `api/external-associates` has 11 endpoints, not 12, and
  the two audit-trail writers on `OrganizationUnitController` were not counted at all.
- **D-6 was half wrong.** The union `PUT` really does answer with no agreements (confirmed: the
  union demonstrably has one, the `PUT` response reports zero). But the predicted blank `UnionName`
  on the agreement write **does not occur** — `AddAgreementAsync` calls `GetOwnedUnionAsync` first,
  which pulls the Union into the change tracker, and EF fixup wires the nav before `ToDto()` reads
  it. The field is populated **by accident of tracking**, not by an `Include`. The harness now
  asserts the accident, so that moving the ownership check or making it `AsNoTracking` is caught
  here rather than on a screen.
- **`OrganogramService` is confirmed live**: 41 unit nodes, 304 position nodes, 6,233 people nodes,
  11 location nodes. `teams` returns **zero** nodes — the `Teams` table is empty, so that fifth view
  is UNPROVEN, not working. `locations` without `structureId` answers a clean 400.

**Confirmed as written:**

- **D-1 — both audit-trail writers 500.** `POST …/change-head` and `POST …/move` each take an
  unhandled exception and answer 500. Nothing else in the bundle 500s.
- **D-2 — the bypass is real and total.** `PUT api/OrganizationUnit/{id}` with a changed
  `headEmployeeId` returns **200**, and the unit's history is still empty afterwards. The recording
  path cannot save; the saving path does not record.

**Found by execution, not predicted by the plan — and both are the same lesson:**

- **D-9 · A soft delete does not release the reliever's unique index.**
  `IX_EmployeeRelievers_EmployeeId_Priority` is UNIQUE with **no filter**, while `DeleteAsync` is a
  soft delete. Create a priority-1 reliever, delete it, create another at priority 1 → **500**. The
  service's own clash pre-check reads through the soft-delete filter, sees nothing and approves the
  write; SQL then rejects it, so the caller gets an opaque 500 instead of the clean
  *"a priority-N reliever is already set"* the service was written to give. ⚠ The first version of
  this probe bumped the row to priority 2 before deleting it, so the deleted row sat at 2, the
  re-create at 1 sailed through, and **the harness reported the defect absent**. A reproduction that
  does not recreate the collision proves nothing.
- **D-10 · Nor the external associate's number.** `GenerateAssociateNumberAsync` takes the highest
  existing `EXT-nnnn` through the soft-delete filter, so a deleted associate's number is invisible
  to the generator and is reissued. Measured on DEFAULT: **four rows already carry `EXT-0008`**, all
  of them deleted fixtures. `AssociateNumber` is not unique in the database, and
  `GET /number/{n}` resolves it with a `FirstOrDefault`, so it answers with whichever row the engine
  reaches first.

  **This is [[hr-succession-area-survey]]'s lesson recurring in a second area, with two faces in one
  bundle. Whenever a store soft-deletes, every uniqueness claim over it — index or generator — is
  wrong until proven otherwise.** Slices 7 and 8 own these.

- **D-8 · The authorization hole is live, and wider than "untidy".** A **plain `Employee`** — no HR
  role, no admin role — was able to: `PUT api/hr/policy-settings` (the tenant's retirement age,
  probation length and establishment enforcement mode), `PUT api/hr/company-profile`, `POST
  api/hr/unions`, and `GET api/Organogram/people` returning **all 6,237 staff**. ⚠ The first version
  of this probe sent a *changed* retirement age and got a 400 — a **validation** refusal, not an
  authorization one — which looked like the gate holding. Writing the settings back exactly as read
  isolated the only question being asked and returned 200. **A refusal is only evidence about the
  gate when nothing else could have produced it.**

**Also noted, for the read-contract work in slices 5 and 8:**

- `OrganizationUnitHistory/unit/{id}/latest` and `/active` answer **404 with a message** when a unit
  has no history. For `latest` that is arguable; for `active`, which is a collection read, "none
  yet" is not "not found". Decide in slice 5.
- `POST api/external-associates` refuses an unlinked caller outright — *"Your user account is not
  linked to an employee record."* The `admin` login is SuperAdmin and not employee-linked, so the
  entire register is unreachable to it. This is why `setup.mjs` exists here at all.

**Ground established.** Nothing in the bundle is unreachable through a routing or wiring fault: 68
of 71 calls answered, and the three 500s are the two D-1 writers plus the D-9 collision. The work
ahead is content, gates and screens — not resurrection.

### Slice 1 — the company profile. 2026-08-22, **69/69**, run twice. No migration.

Harness `run-slice1.mjs`; UI payloads probed first with `SLICE=1 node probe-ui-payloads.mjs`.
Screens: `/administration/hr/settings` (landing) and `/administration/hr/settings/company-profile`.

**What changed**

1. **The gate.** `CompanyProfileController` moved off its bare `[Authorize]` onto
   `SuperAdmin,TenantAdmin,HR`, **read as well as write**. Slice 0's four D-8 assertions for this
   surface flip here from 200 to 403, and the flip is the proof. Gating it touches no document:
   the letter and email services read the profile through `ICompanyProfileProvider`, never the route.
2. **The write response could not name its own country.** `UpdateAsync` mapped the entity it had
   just written, whose `Country` / `CountryOfIncorporation` navigations are absent on a first save
   and **stale** when the country changes — so `countryName` came back blank or wrong beside a
   correct id. It now re-reads through the provider. The harness asserts the harder half: change the
   country, and the response must carry the *new* name.
3. **Two fields no template could reach.** `SignatureImageUrl` and `CompanySealImageUrl` are
   persisted, mapped both ways and were editable in principle — but neither ever entered the token
   dictionary, so no letter template could reference them however it was written, and the only image
   a letter could render was the logo. Both are now tokens in `OfferLetterService` and
   `ProbationLetterService`. An unused token is not substituted, so no existing template changes.
   **Building an editor for two fields that provably go nowhere would have shipped this area's own
   signature defect.**

**Found while building, recorded not fixed**

- **`legalFormName` is redundant.** It returns `LegalForm.ToString()`, so it repeats the member name
  the client already has, and it does **not** carry the `[Description]` label ("Limited Liability
  Company", "Non-Governmental Organisation"). Nothing in the repository reads `[Description]` on any
  enum in any module, so adding a resolver here would set a cross-module precedent this slice has no
  mandate for. Labels live client-side in `COMPANY_LEGAL_FORMS`.
- **The tenant's persisted legal name is `"Default Tenant"`.** Slice 0's round-trip probe created
  the profile row from the provider's fallback, so what was previously an unsaved default is now a
  saved one. Behaviour is unchanged — letters said "Default Tenant" before too — but it is now
  visible and editable, and **TDC should set the real legal name and registered address**. The screen
  shows a banner while nothing has been authored.

⚠ **Two harness lessons, both mine before they were the product's**

- **A DataAnnotations refusal is not mute.** `rejects(..., 400, 'legal name')` failed against
  *"One or more validation errors occurred."* — because a ModelState 400 puts the sentence in
  `errors`, keyed by field, and leaves `title` fixed. The rule fired correctly and said exactly the
  right thing; the probe looked in one of the three places it could be. `api.mjs` now searches all
  three, which every later slice inherits.
- **A TypeScript union of plausible enum members compiles and matches nothing.** The first draft of
  `company-profile.ts` invented `CompanyLimitedByGuarantee`, `Cooperative` and
  `NonGovernmentalOrganisation`, and missed the two real members `Ngo` and `StatutoryBody`. `tsc`
  had nothing to say about any of it. **Read the enum, never the endpoint name** — the area-12 rule,
  earned again.

**Verification:** frontend `tsc` clean for every touched file (the only errors repository-wide are
the pre-existing Inventory ones — cross-module defect #4); `next lint` clean on all six files.

### Slice 2 — HR policy settings. 2026-08-22, **100/100**, run twice. No migration.

Harness `run-slice2.mjs`; payloads probed first with `SLICE=2 node probe-ui-payloads.mjs`.
Screen: `/administration/hr/settings/policy`. Slice 1 re-run green (69/69) alongside.

**Two defects the plan did not predict, both in the DTO layer, both worse than D-5**

- **D-13 · FR-HR-092's trust boundary was unreachable in both directions.**
  `ProceduralAbsenceDays` — the days of unauthorised absence beyond which HR may terminate someone
  **without the Managing Director's signature** — has been on the entity since area 9b and is read
  live by `SeparationService.cs:468`. It was on **neither DTO** and in neither mapping direction, so
  it could not be seen and could not be changed. The entity's own remark says it is a setting rather
  than a constant "so widening the exception later is a configuration change and not a new trust
  boundary". It was neither: a constant of 10 wearing a setting's clothes.

- **D-14 · `EstablishmentEnforcementMode` was decoupled from its entity in both directions.** It sits
  on both DTOs and was assigned by **neither** `ToDto` nor `ApplyUpdate`. The write half is area 14's
  signature defect exactly — accepted every value, kept none, answered 200. The read half is worse:
  `BudgetEnforcementMode` has **no zero member** (Off=1, Warn=2, Block=3), so the unassigned property
  left the DTO carrying `(BudgetEnforcementMode)0` — *a value the enum does not define* — serialised
  as a bare `0` that maps to no member name. The database said `3` (Block); every reader of the
  endpoint was told `0`. This is the setting that decides whether exceeding an authorised
  establishment blocks a vacancy or merely warns — the requirement area 17/18 added
  `EstablishmentApprovedOn` specifically to make enforceable.

  ⚠ **The generalisation worth keeping: when an enum has no zero member, an unassigned property is
  not "the default" — it is a value outside the type.** A round-trip probe cannot find it, because
  writing back what you read reproduces the same wrong value on both sides. Slice 0's `PUT {...s}`
  did exactly that and passed.

**Three consistency rules added**, each of which was reachable and each of which let a saved setting
mean something other than what it said:

- **All four succession fit weights at zero** saved cleanly and then lost to `FitScoreWeights.Default`
  inside `SuccessionCandidateSearchService` — the screen would show 0/0/0/0 while succession ranked
  on 35/30/20/15. The weights are relative and need not sum to anything; they just cannot all be
  nothing.
- **`LongServiceMilestoneYears` is free text** with only a length cap. `"ten,fifteen"` saved happily
  and read back as no milestones at all, because the consumer drops unparseable entries rather than
  throwing — correct of it, but it means junk is silent. Now validated as whole years 1–100; **blank
  is still legal** and means "no tenant-wide milestones".
- **`DefaultCurrencyCode`** had a 3-character cap and no check, so `"GH"` was accepted.

**The gate, and a decision to revisit if TDC disagrees.** Read = SuperAdmin/TenantAdmin/HR;
**write = SuperAdmin/TenantAdmin**. HR works inside these numbers daily and must see them, but three
knobs here are the trust boundaries the settings exist to hold (FR-HR-092, and the two FR-HR-136
enforcement modes) — and moving a boundary should not belong to the function it constrains. Adding
`Constants.Roles.Hr` to `WriteRoles` is the whole change if TDC wants HR to hold the write; the
controller says so in place.

**The screen groups by the area that reads each knob**, not by data type — the only framing in which
the consequence of a change is visible. Termination authority and establishment enforcement each get
their own card with the reasoning; the fit weights render their effective normalised share, because
relative weights are meaningless as raw numbers; and the long-service field says plainly that it does
**not** govern the awards ladder ([[hr-awards-area-survey]] D-8), which lives in its own rows.

**Verification:** frontend `tsc` and `next lint` clean on all five touched files.

### Slice 3 — the organisation-unit audit trail. 2026-08-22, **49/49**, run twice. No migration.

Harness `run-slice3.mjs`. Full regression alongside: slice 0 **16/16**, slice 1 **69/69**,
slice 2 **100/100**. Backend only — the screens are slice 5.

**The four planned fixes**

- **D-1** — one shared `RecordHistoryAsync` stamps `TenantId`. Three call sites write history now
  rather than two, and building the row in one place is what stops them drifting apart again.
- **D-2** — `UpdateAsync` records both operations it used to perform silently, via a new optional
  `ChangeReason` on `UpdateOrganizationUnitDto`. A plain **rename writes nothing**: an audit trail
  that logs everything is one nobody reads.
- **D-2b** — breadth-first `Path` cascade to descendants. See the corrected entry in §3.3: the blast
  radius is a wrong `Depth`, not a broken hierarchy query.
- **D-2c** — reconciled onto the **permissive** rule (parent must be at a higher tier; level-skipping
  allowed). Deliberately: TDC's structure skips levels, so adopting the strict rule would have broken
  the path that works in order to agree with the one that was dead.

**Three defects found while fixing those, each bigger than the one before**

- **D-15 · Fixing the writes would have opened a cross-tenant leak.** Every read on
  `OrganizationUnitHistoryService` except `GetByDateRangeAsync` was tenant-blind; `GetPagedAsync`
  took `GetQueryable()` whole, counted every row in the table and paged across all tenants. **It was
  invisible only because the table was empty, and the table was empty because D-1 stopped the
  writers saving.** The two therefore had to land together — [[hr-succession-area-survey]]'s lesson
  running in reverse: *fixing a writer turns its readers into defects.* Now explicitly tenant-scoped
  throughout, with a `CreatedAt` tiebreaker so a day's worth of same-date changes cannot shuffle
  between calls (area 17/18's unordered-`FirstOrDefault` lesson, one page wider).

- **D-16 · The change log could not name anything.** All four `*Name` fields on
  `OrganizationUnitHistoryDto` were hardcoded `null` behind the comment *"Would need to load
  separately if needed"*. They are needed — this is a log whose entire job is to say *moved from A to
  B* and *head changed from X to Y*. Resolved in two batched lookups, composing employee names in
  memory because `Employee.FullName` is `[NotMapped]` and throws when projected server-side.

- **D-18 · Every business rule on `OrganizationUnitController` answered 500 and said nothing.** No
  action caught `InvalidOperationException`, and `OrganizationUnitService` throws it **42 times** —
  one root per structure, no cycles, this level requires a head, cannot deactivate a unit with active
  children, duplicate code, duplicate name in level. All of them arrived as
  `500 "An error occurred while…"`. This is area 15b's mute-rule shape in a new place, and it was
  slice 3's to fix because the two endpoints this slice resurrected throw `InvalidOperationException`
  for every legitimate refusal. Sixteen handlers added.

- **D-17 · A required head froze 18 of 41 live units against any edit at all.** `CreateAsync` never
  enforced `RequiresHead`; `UpdateAsync` always did. Measured on DEFAULT 2026-08-22: every level
  except Section carries `RequiresHead`, and **not one unit at those levels has a head** — so
  renaming a Department was refused because of a field the edit never touched (and, until D-18, was
  refused with a canned 500). Narrowed so the rule constrains its own operation: **removing** a
  required head is still refused; inheriting an absent one no longer freezes the record. The
  create-side gap is **recorded, not closed** — enforcing it there would block unit creation outright
  for a tenant that has no unit-head data at all.

  ⚠ **This puts a number on the org-authority gap in [[hr-deferred-modules]].** It was recorded as
  "FR-HR-080/181 cannot derive authority because 0/41 units have a head". The missing data was also
  silently freezing **44% of the org structure** against editing.

⚠ **Harness lessons, all three mine before they were the product's**

- **It tried to mint its own root unit.** Only one root is allowed per structure and TDC already has
  it. Fixtures now hang off the real root — and cleanup never deletes it, because it is borrowed.
- **A no-op cannot prove a cascade.** The D-2b section moved a unit to the parent it was already
  under, then asserted its path had changed. The assertion correctly failed. Dedicated fixtures now
  guarantee the mover genuinely moves and genuinely has something beneath it.
- **A probe that is harmless because the feature is broken stops being harmless the moment you fix
  it.** Slice 0 mutated a live TDC unit, which was safe while both writers 500'd. After this slice it
  began stamping a real unit's audit trail every run — and an audit log has no delete, by design.
  Slice 0 now creates and removes a throwaway unit instead.

**Slice 0's assertions were flipped, not relaxed.** D-1, D-2 and two of the four D-8 holes now assert
the *fixed* state, each labelled with the slice that closed it; the two still open (unions, the
organogram) still assert the hole and are labelled with the slice that will close them. Slice 0 stays
runnable as a regression guard.

**Residue left on live data:** 9 history rows on the real `Administration` unit, all tagged
`t19v_193041/193050/193060_*`, written by slice-0 runs between the D-1 fix and the throwaway-unit fix.
They cannot be removed through the product — the change log deliberately has no delete endpoint.
Removing them needs direct SQL, which is the user's call:
`DELETE FROM OrganizationUnitHistories WHERE ChangeReason LIKE 't19v[_]%' OR ChangeReason IS NULL AND …`
— better done by the nine ids listed in the session, since a NULL reason is also legitimate.

### Slice 4 — the organogram. 2026-08-22, **60/60**, run twice. No migration.

Harness `run-slice4.mjs`; payloads probed first with `SLICE=4 node probe-ui-payloads.mjs`.
Screen: `/hr/organogram`. Backend: the gate, the counting, the ordering, a cycle guard.

**Slice 0 said the engine ran. It did — and four of the five things it returned were wrong.**
None of it was visible without rendering the payload, which is the whole argument for the
probe-before-TypeScript rule: reading `OrganogramService` finds none of it.

| Dimension | Nodes | Unlinked | Depth | Widest branch |
|---|---|---|---|---|
| units | 41 | 1 (a real root) | 4 | 6 |
| positions | 304 | 181 | 7 | 181 |
| people | 6,287 | **6,105** | 2 | 6,105 |
| locations | 11 | 1 | 2 | 7 |
| teams | 0 | — | — | — |

**D-8 · The gate, split deliberately.** `people` moved to `SuperAdmin,TenantAdmin,HR`; units,
positions, locations and teams stay open to any authenticated user. Four of the five dimensions
describe the *company* — restricting them would be gating the noticeboard. `people` is the
personnel register: slice 0 measured a plain `Employee` pulling all 6,237 staff in one unpaged call
with **every work email address in `meta`**. Slice 0's assertion flips to 403, and slice 4 also
asserts the other four still answer 200 — otherwise "fixing" the hole by closing the whole
controller would pass, and would take the org chart away from the people it exists to inform.

**D-21 · §3.2's claim that headcount is "rolled up per unit" was false.** Measured: nine of TDC's
41 units reported **zero** staff while holding children full of them — `Managing Director's Office`,
`HR / Administration Department` and `Operations Directorate` all read 0. `TotalEmployeeCount` added
to the DTO and computed as a subtree sum after the tree is normalised, on the three dimensions that
carry a headcount and explicitly **not** on locations, where a rolled-up `0` would be a number
invented for rows that have none.

**D-27 · Every headcount counted leavers.** 79 terminated employees were staff on all three charts;
unit-assigned headcount fell 6,262 → 6,183 once filtered. The predicate is lifted verbatim from
`EmployeeService.cs:2385` rather than restated, so the organogram and the rest of HR cannot drift on
what "a member of staff" means. Position occupancy is unmoved today (52 either way — no post is held
*only* by a leaver) but the vacancy flag was latent: the first departure would have left a post
reading "filled".

⚠ **An EF-untranslatable predicate compiles.** The first version wrote `OnStrength` as a static
method and called it inside `.Where()`. That builds, reads correctly, and throws at runtime. It is
now an `Expression<Func<Employee, bool>>` applied as its own `.Where()` clause.

**D-28 · Two of five dimensions came back unordered**, so sibling order shuffled between identical
calls. Area 17/18's unordered-read lesson, one page wider.

**A cycle guard, added before anything needed it.** `Build()` re-rooted *missing* parents but not
*looping* ones. Nothing stops `Employee.ManagerId` forming a loop — the org-unit service refuses
cycles, the employee service does not — and a looped group is reachable from no root, so the client
either drops those people silently or spins building the tree. `DetachCycles` breaks each loop at
one node.

**D-19 · The people dimension is not a hierarchy, and the screen has to say so.** 181 of 6,286
employees have a manager recorded (2.9%); 0 of 41 units have a head. Not slice 4's to fix — it is
the org-authority data programme in [[hr-deferred-modules]] — but a chart drawn without comment
reads as a *flat organisation* rather than an *unpopulated* one. The coverage panel states which,
computed live from the payload, so the day the data lands the panel stops saying it. The harness
asserts the same numbers, so that day is noticed rather than discovered.

**The chart carries no new dependency.** `reactflow` and `recharts` are both already in the tree and
neither ships a tree layout; the DTO's own comment assumes `d3-org-chart`, which is not installed.
Connectors are CSS borders. Three things make a 6,105-wide fan survive: subtrees mount nothing until
opened, sibling rows page at 40, and search expands the ancestors of its hits.

⚠ **Two harness lessons, both mine**

- **A parent with children is not a parent with staff.** The first D-21 assertion demanded that
  every hollow parent report a non-zero total; it correctly failed on `Development Control Unit`,
  `Estates Department` and `MIS Unit`, which have children and nobody anywhere beneath them. The
  invariant is arithmetic — *total = own count + children's totals* — and checking it exactly is
  also stronger, because it catches a rollup that double-counts as readily as one that under-counts.
- **A fixture that moves an employee must respect the rules on the way.** The first D-27 probe built
  a throwaway unit and moved the subject into it, and was refused: *"Selected position does not
  belong to the specified organization unit."* A real rule doing its job. Proving a headcount needs
  no new unit at all — the delta across a termination is the honest measurement, and because the
  subject is minted in the run, the unit ends on the count it started with.

**Verification:** frontend `tsc` clean on every touched file (repository-wide errors are the 19
pre-existing Inventory ones — cross-module defect #4); `next lint` clean.

### Slice 4b — the teams register. 2026-08-22, **88/88**, run twice. Migration `20260822133728_FilterTeamCodeUniqueIndexOnSoftDelete`.

Harness `run-slice4b.mjs`. Screens: `/administration/hr/organization/teams`, `…/teams/new`,
`…/teams/[id]`. Full regression alongside: slice 0 **16/16**, 1 **69/69**, 2 **100/100**,
3 **49/49**, 4 **60/60**.

**This slice exists because Decision 5 was reopened, and reopening it was right.**

Slice 4 shipped four tabs and dropped the fifth, on the grounds that `Team` had exactly one consumer
in the entire repository — `OrganogramService` itself — with no controller, no service, no seeder
and no writer of any kind, so it projected a table nothing could ever fill. That reasoning had three
legs and **one does not hold**: "don't ship an empty store" fails against slice 1, where
`CompanyProfiles` also had zero rows and got its editor anyway. Empty means nobody built the screen,
not that nobody wants it. Asked why the register could not simply be built, the honest answer was
that it could.

**What was already there, measured before writing a line:** three entities (`Team`, `TeamMember`,
`TeamMemberHistory`), three real tables (31 / 20 / 18 columns), `DbSet`s at
`ApplicationDbContext.HR.cs:48-50`, and complete EF configuration at `:790-880` — indexes, foreign
keys, delete behaviours. Three enums. **Nothing was missing but the application layer**, which is
why this is one slice rather than an area.

**D-29 · The soft-delete/unique-index trap, third occurrence in this bundle.**
`IX_Team_Tenant_Code` was UNIQUE with no filter over a soft-deleting store, so a dissolved team
would hold its code for ever: the service's duplicate check reads through the soft-delete filter,
sees nothing, approves the write, and SQL then rejects it — an opaque 500 in place of the sentence
the service was written to give. This is D-9 (the reliever priority) and D-10 (the reissued
associate number) a third time. **Whenever a store soft-deletes, every uniqueness claim over it —
index or generator — is wrong until proven otherwise.** Found by reading the configuration before
writing the service, rather than by a 500 afterwards.

The migration is guarded SQL rather than the scaffolded `DropIndex`/`CreateIndex` pair, and the
order matters: an `IF NOT EXISTS … CREATE` on its own finds the *unfiltered* index already sitting
under the same name, skips, and records the migration as applied — leaving the defect in place and
looking fixed. Its `Down` can legitimately fail once the filter has been live, because soft-deleted
rows may by then share a code with a live one; that is stated in place, because silently dropping
the uniqueness would be the worse answer.

**The rules the entity implied and nothing enforced.** The modelling was thoughtful, so most of them
were discoverable from the fields:

- **A leaver cannot join a team or lead one** — the same predicate as everywhere else in HR. Without
  it, a terminated employee walks back onto the organogram through the teams dimension, straight out
  the side of slice 4's D-27 fix.
- **`IX_TeamMember_Team_Employee` is deliberately not unique**, because leaving a team and rejoining
  it later is two legitimate rows. So the duplicate rule is the service's to hold, and it must ask
  *"is one of them current"* rather than *"does one exist"* — a `COUNT` through the wrong filter
  would refuse a legitimate rejoin.
- **`MaxMembers` was a field nothing read.** Now enforced on add.
- **`IsPrimary` had to be exclusive per employee**, or the flag means nothing.
- **Parent-team cycles are refused** rather than rendered around. Slice 4 added `DetachCycles` to
  survive one; better not to create one.
- **Removing a member ends the membership rather than deleting it.** A team's record of who was on
  it is part of what the register is for, so the row stays and carries its leaving date. The harness
  asserts the row's *survival*, not a status code.

**One definition of "on the team now"** — `IsCurrentMembership`, in the mapping extensions and
reused by the roster, the member count and the organogram, so the three cannot disagree. It is
evaluated in memory rather than restated as an EF predicate for exactly that reason.

**The organogram's fifth view came back, and gained a real number.** Teams now carry a live member
count (membership current *and* the person on strength) with `MaxMembers` as the expected headcount,
and roll up like the other headcount dimensions. Slice 4's two teams assertions were **flipped, not
deleted** — one said teams carry no rolled-up count, the other that the projection is empty. Both
were true only while nothing could write the table. The replacement asserts the chart shows exactly
what the register holds, and prints UNPROVEN rather than passing vacuously when the register is
empty — which on DEFAULT it still is, because TDC has authored no teams yet.

⚠ **A harness lesson that was neither the harness's fault nor the product's:** a run inspected with
`node run-slice4b.mjs | head -40` was killed by SIGPIPE partway through and never reached its
cleanup, leaving three live fixture teams behind — which then surfaced as two *correct* failures in
slice 4. **Truncating a harness's output truncates the harness.** Use `tail`, or redirect to a file.

**Left deliberately unbuilt:** `ShiftId` and `LocationId` are on the entity and go out as `null`
from the form. Shift needs the attendance shift-definition picker and location the site picker;
both are easy, and both are better added once TDC has said whether its teams use them than guessed
at now.

**Residue on live data:** 24 soft-deleted fixture teams and their ended memberships, plus the usual
`T19V` employees. The register itself reads clean — 0 live teams — and a soft-deleted team no longer
holds its code, which is the point of the migration.

### Slice 5 — the unit change-log screens. 2026-08-22, **111/111**, run twice. No migration.

Harness `run-slice5.mjs`; payloads probed first with `SLICE=5 node probe-ui-payloads.mjs`. Screens:
`/administration/hr/organization/unit-history` (the register) and a **Change log** tab on
`…/units/[id]/edit`. Full regression alongside: slice 0 **16/16**, 1 **69/69**, 2 **100/100**,
3 **49/49**, 4 **60/60**, 4b **88/88**. Bundle total: **493 assertions**.

**Slice 3 made the audit trail able to write. Rendering it found six more defects, and the largest
had been there since the port.**

**D-30 · The register showed 9 of 66 rows and reported 66.** `Scoped()` `.Include`d
`h.OrganizationUnit` — a **required** navigation whose principal carries the global `!IsDeleted`
query filter — so EF composed it as an INNER JOIN and dropped every history row about a unit that
had since been dissolved. `CountAsync` strips includes, so the envelope went on counting them.
Measured on DEFAULT 2026-08-22 before the fix:

```
envelope says totalCount = 66 , totalPages = 1
the page actually carries  = 9 rows        (86% invisible)
distinct units visible     = 1             of the 40 the table holds rows for
```

The rows an audit trail exists for are precisely the ones about things that no longer exist. The
`Include` is gone and the unit's name resolves in `ResolveNamesAsync` instead, alongside the parent
and head names.

⚠ **`GetQueryable().IgnoreQueryFilters()` does not read through a soft delete — and that was my bug,
caught by the harness rather than by reading.** `GenericRepository.GetQueryable()` welds
`.Where(e => !e.IsDeleted)` in as an **ordinary predicate**; `IgnoreQueryFilters` lifts the
DbContext's *global* filter and leaves the repository's own `Where` standing. The call compiles,
reads exactly as though it worked, and changes nothing — the first run failed on precisely the two
assertions that asked a dissolved unit to name itself. The correct call is
`GetQueryableIncludingDeleted(predicate)`. A sweep found three other uses of the same shape
(`CompanyProfileProvider`, `CertificateVerificationService`, `TrainingCompletionService`) and **all
three are correct as written** — each wants to bypass only the *tenant* filter, and two say
"soft-deleted rows are still excluded" in place.

**D-31 · Three filters that were silently ignored.** The probe measured it rather than inferring it:
`?unitId=…`, `?startDate=2099-01-01` and `?changeType=Restructure` each returned `totalCount 66`,
identical to unfiltered, because `GetPagedAsync` took a page number and nothing else. So the read
Decision 6 says the register exists for — *what changed in this unit last quarter* — could not be
asked at all. Four server-side filters now, counted **after** narrowing. An unrecognised
`changeType` is **refused with a 400 listing the valid values**, not ignored: from the screen, a
filter that silently does nothing is indistinguishable from one that matched everything. `pageSize`
is clamped at 200 and both endpoints refuse an inverted date range.

**D-32 · The change type was derived where no read could reach it.** `ToDetailDto` has classified
Restructure / Leadership Change / Other since the port and **nothing in the repository has ever
called that mapper**, so every consumer was left to re-derive "reparent or change of head?" from
four nullable ids. Lifted to `OrganizationUnitChangeTypes`, put on the base DTO, and reused by
`ToDetailDto`. The rule genuinely has to be written twice — `Classify` in memory, `Predicate` in SQL
— so the harness holds a **third, independent** statement and asserts the filtered page equals the
classified set, per type. Two statements of one rule is the shape that drifts.

**D-33 · The log could not say who.** `createdBy: ""` on every live row. There is no global auditing
interceptor in this codebase — each service stamps `CreatedBy` itself — and the writer did not. A
change log that records what changed and why but not **who** is missing the column the question is
usually asked about.

**D-34 · `EffectiveTo` was modelled and never written**, so an effective-dated log could only ever
say "from", and the register would have rendered a column blank for ever. Now closed **per series**,
which is the part that had to be right: a unit's reporting line and its leadership move
independently, so ending the head record because somebody reparented the unit would make the log
state something that never happened. Four assertions exist only to pin that down.

**D-35 · The edit form could not supply a reason.** `ChangeReason` has been on
`UpdateOrganizationUnitDto` since slice 3 and nothing sent it, so every row the only working write
path produced carried `changeReason: null`. The reason box appears only when the parent or the head
actually differs from what the form loaded — a rename writes no history row, so asking why would be
asking for something nothing keeps.

**D-36 · The two dedicated endpoints recorded changes that did not happen — found by the filter, not
by reading.** `Other` is a classification no real change can produce, and it had **nine rows**. Every
one was `previousParentId == newParentId`: `UpdateAsync` has always guarded on
`parentChanged`/`headChanged`, while `MoveUnitAsync` and `ChangeHeadEmployeeAsync` never did, so
re-sending a unit's current parent stamped the trail with *"moved from A to A"*. Three of the nine
are against the real `Administration` unit and **they cannot be removed**, because a change log has
no delete. Both endpoints now return `true` without recording — validation still runs first, so a
self-parent or a cycle is still refused. The harness asserts "this run added no `Other` rows" rather
than "there are none", because the historic nine are permanent.

**One screen defect that no API assertion could have caught.** The parent-unit hint on the unit form
read *"Must be exactly one level above this unit."* That is the strict rule slice 3 **abandoned** in
D-2c — TDC's structure skips levels, so both write paths were reconciled onto the permissive one and
the form went on telling people otherwise. Also corrected: the head hint now says a required head
cannot be cleared once assigned, which is what D-17 left true.

**Decision 6, adjusted in the doing and worth stating.** The plan said "a tab on the unit detail plus
one register". There is no unit detail screen — the units register navigates straight to
`…/[id]/edit` — and building a read-only detail page to host one tab would have added a screen whose
whole content is the form rendered twice. The tab went on the **edit** screen instead, which is
where the two recorded changes are actually made: the record of the last restructure now sits beside
the control that performs the next one. Both surfaces render one `UnitChangeLog` component, so the
register and the tab cannot describe the same row differently.

⚠ **A harness lesson about the harness's own escaping.** Editing `run-slice5.mjs` through a shell
heredoc collapsed a `\n` escape into a real newline inside a JS string literal, splitting a
`console.log` across two lines and breaking the file — twice, because the repair used the same
escaping. Building the backslash with `chr(92)` in the Python patch script is what fixed it, and
`node --check` after every scripted edit catches it in one step. That is the corollary to the
README's "edit with Python, never `perl -i` or PowerShell" rule.

**Residue on live data:** this slice's fixture units are soft-deleted and their history rows stay
behind on purpose — after D-30 a deleted unit no longer takes its history with it, which is the whole
point. One older fixture unit, `T19V130556OrgUnit`, is still live from an earlier slice's run; it is
inert, but it is a 42nd unit in every unit listing, and removing it is a one-line delete whenever the
user wants it gone.
