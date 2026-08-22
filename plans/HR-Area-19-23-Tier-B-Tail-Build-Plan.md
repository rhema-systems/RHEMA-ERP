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
| **19** | `api/OrganizationUnitHistory` | 6 GET | 0 | **0 rows; both writers cannot save** |
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
*"descendants are handled elsewhere if needed"* — and nothing, anywhere, handles them. `Path` is
what `Depth` is computed from (`…MappingExtensions.cs:266,286,308`), so after one reparent of a
unit that has children, every descendant reports the wrong depth and a path that no longer
resolves. 41 units live, so this is small today and unbounded later.

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

**Decision 6 · Unit history renders as a tab on the unit detail plus one register.** A change log
without the thing it logs is unreadable; a register is still needed for the date-range and "what
changed this quarter" reads the controller already offers.

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
| 4 | **Organogram.** One screen, five views, the chart component. | 19 |
| 5 | **Unit history screens.** Register + the unit-detail tab. | 19 |
| 6 | **Unions and CBAs.** Register, detail, agreement collection, D-6 fix. | 21 |
| 7 | **Reliever roster.** D-9 (the unfiltered unique index), roster screen + the leave-form default (Decision 3). | 20 |
| 8 | **External associates.** D-10 (the reissued number), register, detail, activate/deactivate — the 10 unreached endpoints. | 20 |
| 9 | **Facility services** onto the area-11 facility detail. | 20 |
| 10 | **The HR home.** Real headline figures on `/hr`, and a reachability sweep of every HR route. | 23 |
| 11 | **Content audit.** Every GET, by id, asserting content not status. Run twice. | all |
| 12 | **UI parity.** endpoint → service → screen, the area-14 completeness check. | all |

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
