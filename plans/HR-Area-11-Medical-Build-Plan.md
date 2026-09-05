# HR Area 11 — Medical & Health: Build Plan

**Status:** in progress from 2026-08-17 · **Branch:** `hrdev` · **Canonical source:** this file, at
`plans/HR-Area-11-Medical-Build-Plan.md`

> **Read this at the start of every session working on area 11.** It exists so that nothing is
> lost to context truncation. If something here contradicts your memory of the area, this file
> wins — it records what was *measured*, with dates. Update it as slices close; keep the
> Findings register and the Status table current, because those are what a future session reads
> first.

---

## 1. How to use this document

| Section | When you need it |
|---|---|
| §2 Status | First thing, every session — what's done, what's next |
| §3 Ground truth | Before doubting whether something works; it records what was actually run |
| §4 Environment | Before running the harness or probing the API |
| §5 Decisions | Before re-opening a settled question |
| §6 Findings | Before writing a fix — check it isn't already logged or closed |
| §7 Slices | The actual work |
| §8 Unverified | Before assuming something was checked |
| §11 Deferred | Before assuming something was dropped — everything left out is listed with its trigger |

**Update rules.** Close a finding by moving its status to `fixed` with the slice number, don't
delete it. Add new findings as they're discovered, with evidence. When a slice closes, tick it in
§2 and record the real assertion count.

---

## 2. Status at a glance

| # | Slice | State | Assertions |
|---|---|---|---|
| 1 | Finish the gating job | ☑ **green 2026-08-17** | **93** |
| 2 | Cross-tenant adjudication + claim contract | ☑ **green 2026-08-17** | **26** |
| 3 | Self-service claims + actor hole | ☑ **green 2026-08-17** | **30** |
| 3a | Claim documents onto the upload gate (F-08) | ☑ **green 2026-08-17** | **22** |
| 4 | UI — reference & config | ☑ **green 2026-08-17** | tsc + lint clean, 17 routes verified |
| 5 | UI — health records | ☑ **green 2026-08-17** | tsc + lint clean, 10 routes verified |
| 6 | UI — claims, NHIS, dashboard | ☑ **green 2026-08-17** | tsc + lint clean, 18 routes verified |
| 7 | UI — clinical | ☑ **green 2026-08-17** | tsc + lint clean, 9 routes verified |

**Area 11 build plan complete.** Backend harnesses: **246 assertions** — 171 across slices
1/2/3/3a, plus **37** in `run-resolved-names.mjs` (the `.Include` audit) and **38** in
`run-dead-paths.mjs` (the never-executed-endpoint sweep). All green 2026-08-17.

⚠ **Both audits were run AFTER slices 4–7 shipped, and both found real defects.** The
resolved-names audit failed 20 of 37 — every detail screen built in slices 4–7 would have shown
blanks. The lesson is in §10; the short version is that a status-code assertion says nothing about
content, and a route check says nothing about whether a write path works.

Slices 1–3 are the backend hardening block and must land in order. 4–7 are the
`hr-frontend-port-plan` W1 recipe applied an eighth time, and 4 must precede 5–7 because every
downstream screen needs a facility to point at.

---

## 3. Ground truth — measured 2026-08-17

Everything in this section was **run**, not read. Do not re-derive it; do re-check it if the
branch has moved a long way.

### 3.1 The surface

- **11 controllers** + `MedicalControllerBase`, all under `src/ErpSystem.Api/Controllers/HR/`.
- **28 entities** in `src/ErpSystem.Core/Entities/HR/MedicalEntities.cs` (1,663 lines).
- **7 services** in one file: `src/ErpSystem.Core/Services/HR/MedicalServices.cs` (2,813 lines).
- DTOs: `src/ErpSystem.Core/DTOs/HR/MedicalDTOs.cs` (2,442) + `MedicalDashboardDtos.cs` (84).
- All services and repositories **are** DI-registered in `HrModuleServiceRegistration.cs`
  (lines ~324–351 repositories, ~569–576 services). No missing registrations.
- **No frontend exists at all** — no page, service, type, or sidebar entry.

### 3.2 What runs

| Probe | Result |
|---|---|
| Every read endpoint (30 routes) | **30/30 → 200.** No dead read paths. |
| Config spine: facility → provider → plan → policy; scheme; profile → exam | **7/7 → 201** |
| Claim chain: create → approve → pay | **works**, ends at `status=Paid` |
| Insurance utilisation | **real** — 400 consumed on approve, released to 0 on delete |
| Re-adjudication guard | **works** — refuses a second approval |
| `MedicalWorkflowException` mapping | mapped in `GlobalExceptionHandlingMiddleware`: `NotFound`→404, everything else→**422** |

**Conclusion: area 11 is not area 8.** The engine runs. This is harden-gate-and-build-UI, not
resurrection.

### 3.3 Data state

Every medical table held **0 rows** before the smoke pass. `Employees` has 1,650 rows and exactly
one medical-adjacent column (`BloodType`). The SHE health tables have area-10 harness data
(surveillance 10, first-aid 7, wellness 7, RTW 7) — but **7 of 10 surveillance rows and all 7 RTW
rows are soft-deleted**, which is why `GET api/safety/return-to-work` correctly returns `[]`
despite 7 rows existing. That is not a defect; the area-10 harness cleans up after itself.

### 3.4 Permission seed — live and correct

```
Permissions:  HR.Medical.Read, HR.Medical.Write, HR.Medical.Admin
HR          → Read, Write            (deliberately NOT Admin)
SuperAdmin  → Read, Write, Admin
TenantAdmin → Read, Write, Admin
```

Policies compose by OR-listing in `PermissionRequirement`, so `Admin ⊃ Write ⊃ Read` falls out
without granting all three. `PermissionAuthorizationHandler` short-circuits for SuperAdmin, so a
SuperAdmin token **cannot test gates** — use the HR actor.

---

## 4. Environment & harness

### 4.1 Running API

- `http://localhost:5000` — health at `/health`.
- **The harness must run in Staging.** In Development the exception page pre-empts the exception
  mapping, so every business-rule refusal reads as a 500 with a stack trace instead of a 422. This
  is the `hr-harness-run-environment` trap and it was hit during this survey. Pass the JWT key.
- ⚠ **Pass `--contentRoot`** when launching the DLL by absolute path. The host takes its content
  root from the working directory, so launching from anywhere else finds no `appsettings.json` and
  dies with `Connection string 'DefaultConnection' not found` — which looks like a Staging config
  problem and is not. Full recipe in `dev-harness/hr-medical/README.md`.
- ⚠ **Stopping it again: match the command line, not the process name.** Launched as
  `dotnet …\ErpSystem.Api.dll` the process is named **`dotnet`**, so
  `Get-Process -Name ErpSystem.Api` reports "not running" while it still holds every output DLL and
  the next build fails on a file lock. Use
  `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -like '*ErpSystem.Api\bin*' }`,
  and `dotnet build-server shutdown` if a lock persists.
- Kill the running `ErpSystem.Api` process before asking for a rebuild — it locks its own output
  DLLs (`stop-backend-before-user-builds`).

### 4.2 Actors — **three, not the usual two**

| Actor | Credentials | Holds | Use for |
|---|---|---|---|
| Admin | `admin` / `Admin123!` | SuperAdmin — bypasses permission checks | Setup, Admin-gated positive assertions |
| HR | `a10v_00294931` / `A10Verify123!` | role `HR` → Read + Write | **Every gate test.** Employee-linked. |
| Employee | any `a10v_*` without HR role | plain | Slice 3 self-service |

- HR actor's `employee_id`: `ab6c2543-2449-4b21-b373-57357777129e`
- Test subject employee: `A9135D35-9A12-40FE-BD41-33D5CF27BF64` (employee number `20260001`)
- Tenant: `00000000-0000-0000-0000-000000000001`
- ⚠ **`admin` is NOT linked to an employee record** — 18 of 1,609 users aren't, and `admin` is
  one. This is why the claim paths fail for it (see F-05).

### 4.3 Database

`Server=. ; Database=ErpSystemDB ; User Id=sa ; Password=<the password in appsettings.json>`

**Gotcha:** identity tables are `Users`, `UserRoles`, `AspNetRoles` — *not* `AspNetUsers`. Two
queries were lost to this. Role grants join `RolePermissions → Permissions` + `AspNetRoles`.

### 4.4 Smoke fixtures — created then cleared

The survey's smoke pass wrote 11 rows (facility, provider, plan, policy, scheme, health profile,
exam, 2 claims, approval, note). **Cleared 2026-08-17**, hard delete in FK order, verified back to
**0 rows across all 28 medical tables**; SHE health (31 rows) and `Employees` (1,650) untouched.

Cleared deliberately so slice 1's harness starts from the same 0 baseline the survey recorded —
hand-made rows sitting alongside would skew any count-based assertion. **Build fixtures in the
harness's own `setup.mjs`**, the way areas 7/9/10 do, not by hand.

The clear script is at `scratchpad/clear-smoke.sql` for the session; if you need it again, the
FK order is: notes → approvals → items → documents → claims → exam-documents → exams →
conditions → allergies → profiles → dependents → insurance-claims → policies → premiums →
network-facilities → provider-documents → plans → providers → tiers → schemes → pre-auths →
referrals → appointments → NHIS-documents → NHIS-claims → facility-services → physicians →
facilities.

### 4.5 Enum values (extracted, save a lookup)

```
HealthFacilityType          1 GeneralHospital  2 SpecializedHospital  3 Teaching  4 Clinic
MedicalInsuranceProviderType 1 HealthInsurance 2 Life  3 HMO  4 PPO
BloodGroup                  1 A+  2 A−  3 B+  4 B−  5 AB+ …
MedicalExamResult           1 Fit  2 FitWithRestrictions  3 TemporarilyUnfit  4 Unfit
MedicalExpenseType          1 Consultation  2 Medication  3 LabTests  4 Imaging  5 Surgery
ClaimStatus                 1 Pending 2 Submitted 3 SupervisorReview 4 HrReview 5 FinanceReview … 8 Paid
MedicalExpenseApprovalStatus 1 Pending  2 Approved  3 Rejected  4 Escalated
```

`AddMedicalExpenseClaimNoteDto` fields are `claimId`, `noteType`, **`content`** (not `note`),
`isInternal`.

---

## 5. Decisions taken — do not relitigate

### D-1. Employees file their own medical expense claims

Build it on the existing `/me` controller; do **not** defer to area 25.

The area-25 deferral rule (`hr-area-authz-pattern`) covers self-service *reads* — an onboarding
plan, a task queue — where withholding costs reach, not correctness. A reimbursement claim is the
**originating transaction**. HR-only filing means the process does not work for 1,650 staff.

Supporting reasons, in enterprise terms:

- **Segregation of duties.** As built, HR both files and adjudicates, which makes the recorded
  `approverEmployeeId` meaningless to an auditor.
- **The code already assumes it.** `IsFlaggedForReview`, `Flag`/`Unflag` and the internal-notes
  split only make sense against a population you are policing. You do not build fraud-flagging for
  claims your own team typed in.
- **Area 25 inherits rather than retrofits** — the `/me` pattern already exists *in this area*.

**Scope line:** self-service gets file / list own / read own / items / documents. It gets
**none** of approve, pay, flag, unflag, or `GetNotes`. Internal notes are the trap — the
self-service path must never call that method, not merely default the flag off.

### D-2. The fallback verb fix lands in area 11 slice 1, not W3

It is the only place it can be proven — medical is the sole populated permission slice — and there
is no lockout risk today because the seed is already live. That window closes the moment W3 adds
unseeded permissions. See F-01.

### D-3. Facility / physician / facility-service **reads** stay open

They must **not** go behind `MedicalReadPolicy`, despite being medical-adjacent. D-1 has employees
filing their own claims, and `CreateMedicalExpenseClaimDto.FacilityId` is **required** — an
employee must be able to read the facility and physician registers to file at all. Gating those
reads in slice 1 would break slice 3 two slices later.

Correct split for all three: **reads on plain `[Authorize]`, writes on `MedicalWrite`, deletes on
`MedicalAdmin`.** `MedicalPhysiciansController`'s existing class-level `MedicalReadPolicy` must be
*relaxed* to match.

---

## 6. Findings register

| ID | Severity | Finding | Status |
|---|---|---|---|
| F-01 | **critical** | Role fallback grants any `HR.*` permission regardless of verb | ✅ **fixed slice 1** |
| F-02 | **high** | 3 controllers still on bare `[Authorize]` | ✅ **fixed slice 1** |
| F-03 | **high** | `ProcessApprovalAsync` never checks the claim's tenant | ✅ **fixed slice 2** |
| F-04 | medium | `CreateMedicalExpenseClaimDto.Items` silently discarded | ✅ **fixed slice 2** |
| F-05 | medium | Claim create/approve/note demand an employee-linked *caller* | ✅ **fixed slices 1 + 3** |
| F-06 | medium | On-behalf-of branch unreachable (permission + actor) | ✅ **fixed slice 1** |
| F-07 | low | ~40 reads filter tenant in memory after `GetAllAsync()` | open → **deferred, see §11** |
| F-08 | **high** | Claim documents take a caller-supplied `FilePath` | ✅ **fixed slice 3a** |
| F-09 | **high** | A claim could draw down another employee's insurance policy | ✅ **fixed slice 3** |
| F-10 | **high** | Detail reads resolved no navigation names — 20 of 37 blank | ✅ **fixed 2026-08-17** (post-slice-7 audit) |
| F-11 | medium | Provider documents: `FilePath` required and caller-supplied, no upload, no download | open → **deferred, see §11** |

### F-01 — Role fallback ignores the verb ✅ *empirically confirmed*

`HrPermissionRoleFallbackAuthorizationHandler` succeeds on *any* requirement whose permissions all
begin `HR.`, for anyone in `MedicalFallbackRoles`. It never inspects the verb.

**Proof.** The HR actor holds only `HR.Medical.Read` + `HR.Medical.Write` in the database. Both of
these are `MedicalAdminPolicy` routes:

```
204  DELETE api/employee-health/exams/{id}          (expected 403)
204  DELETE api/medical-expense-claims/{id}         (expected 403)  ← a PAID claim
```

**The framing that makes the fix obvious:** the fallback exists to stand in for the seed, so it
must grant *exactly* what the seed grants. Today it grants a strict superset.

**Blast radius grows with W3.** Today it's medical deletes. After W3 it is every HR area's Admin
verb, granted to every HR-role user at once.

### F-02 — Ungated controllers

`NHISClaimsController`, `HealthcareFacilitiesController`, `FacilityServicesController` all carry a
bare `[Authorize]`. NHIS is the one that matters: employee-linked claims with amounts, diagnoses
and documents, fully CRUD-able and deletable by any authenticated employee.

`EmployeeHealthSelfServiceController` is *also* bare `[Authorize]` — that one is **deliberate**
and documented in its XML comment. Do not "fix" it.

### F-03 — Cross-tenant adjudication

`ProcessApprovalAsync` loads the claim and checks only `claim == null`. `EnsureTenant(tenantId)`
validates the caller's tenant against itself — always true — and never the claim's. Every sibling
method (`ProcessPayment`, `Flag`, `Unflag`, `Delete`, `Update`) *does* check. The status check also
runs **before** any tenant check, making it an enumeration oracle.

### F-04 — Discarded child collection

`Items` is accepted on the create payload and silently dropped. Sent 1 item → DB has 0 rows. Items
only persist via `POST {claimId}/items`. Classic discarded-DTO shape from `hr-ported-list-read-bugs`.

### F-05 — Employee-linked caller required

`TryGetEmployeeWriteContext` demands the caller be employee-linked on claim create, approval and
notes — even when HR supplies `dto.EmployeeId` to file on behalf. 18/1,609 users are unlinked and
`admin` is one, so the seeded SuperAdmin cannot file, approve or annotate a claim.

---

## 7. The slices

### Slice 1 — Finish the gating job ✅ green 2026-08-17, 93 assertions

Closed F-01, F-02, F-06, and the create half of F-05. Harness: `dev-harness/hr-medical/run-slice1.mjs`.

**As-built notes** (where it differed from the plan below):

- The map is `HrPermissions.RoleGrants` + `GrantsFor(role)`; `MedicalFallbackRoles` is gone and no
  code references it. The seeder calls `GrantsFor` rather than restating grants — **verified
  behaviour-preserving**, role grants in the DB are byte-identical after the change.
- **`"Admin"` was kept**, mapped to `AllNames`, against the survey's suggestion to drop it. No such
  role exists in the reference DB (checked against all 57), but removing it would silently revoke
  medical access in any environment that does have one — not a change to make unasked inside a
  security fix. Flagged in the XML doc to confirm, then promote to a constant or delete.
- **F-05's create half landed here.** Rewriting `Create` forced a choice of context helper, and
  `TryGetEmployeeWriteContext` was never justified on that path — `CreatedByUserId` is a *user*.
  It now uses `TryGetWriteContext`, so an unlinked HR account can file on behalf. Approval still
  requires a linked actor, correctly, because `ApprovedById` is an Employee FK — that half is
  slice 3.

**Changes**

1. `NHISClaimsController` → full `Read` / `Write` / `Admin` split. Genuinely medical data.
2. `HealthcareFacilitiesController`, `FacilityServicesController`, `MedicalPhysiciansController` →
   per **D-3**: reads plain `[Authorize]`, writes `MedicalWrite`, deletes `MedicalAdmin`.
3. **`HrPermissions.RoleFallbackGrants`** — an explicit role → permission map:
   ```
   SuperAdmin, TenantAdmin, Admin → AllNames
   Hr, LegacyHrUser               → [ViewMedicalRecords, MaintainMedicalRecords]
   ```
   The handler intersects the requirement against the caller's roles' granted set.
   `DatabaseSeedingService`'s `rolePermissionMap` reads the **same constant** so the two cannot
   drift. Rename `MedicalFallbackRoles` while you're there — it is about to stop being medical.
4. Delete the unreachable on-behalf branch in `MedicalExpenseClaimsController.Create`; make
   `EmployeeId` explicit and HR-only. Slice 3 builds the real self-service path.

**Harness asserts**

- All **24** `Admin`-gated routes → 403 for HR actor, 204 for admin actor.
  (EmployeeHealth 5, MedicalClinical 3, MedicalInsurance 6, MedicalExpenseClaims 3,
  MedicalBenefitSchemes 2, MedicalPhysicians 1, + NHIS 2, facilities 1, facility-services 1.)
- HR actor retains read + write on every controller — **no lockout**.
- Plain employee: **can** read facilities and physicians (D-3), **cannot** write them, **cannot**
  touch NHIS at all.

**Risk: none.** The seed is live, so tightening removes only what was never granted.

---

### Slice 2 — Cross-tenant adjudication + claim contract ✅ green 2026-08-17, 26 assertions

Closed F-03, F-04. Harness: `dev-harness/hr-medical/run-slice2.mjs`.

**The sweep resolved §8's biggest unknown.** Every fetch in `MedicalServices.cs` was checked
mechanically: **84 `GetByIdAsync` call sites, exactly 1 without a tenant check**; 71 other
single-entity fetches, 1 flagged and it is a false positive (line ~1532 loads a provider network
scoped by a policy that was tenant-checked two lines above). F-03 is isolated — the area's tenant
discipline is otherwise sound, and slice 2 did not grow.

**Cross-tenant probing needs a real second tenant.** `TenantId` carries an FK, so moving a row to a
made-up GUID fails with 547 rather than proving anything. The harness creates a real second
`Tenants` row (idempotent, every *default-tenant* flag OFF so it cannot disturb login routing) and
moves claims into it.

⚠ **Two harness traps found the hard way, both now fixed in `run-slice2.mjs`:**
- `sqlcmd` **exits 0 on SQL errors**. Without `-b`, a failed statement returns its error text as if
  it were a result and the assertions compare against garbage — the first run reported §1 failures
  that were really an invalid GUID literal (`a11f0re1…`, `r` is not hex) silently doing nothing.
- `sqlcmd` runs with **`QUOTED_IDENTIFIER OFF`**, and `Tenants` has indexes that refuse an INSERT
  under it (Msg 1934). The helper now prefixes `SET QUOTED_IDENTIFIER ON`.

**`ClaimStatus.Approved = 6`**, not 4 — the enum has review states in between (Pending=1,
Submitted=2, SupervisorReview=3, HrReview=4, FinanceReview=5, Approved=6, Rejected=7, Paid=8).

**Changes**

1. `ProcessApprovalAsync` — add the `claim.TenantId` check, **before** the status check.
2. **Sweep for the same shape.** Only ~15% of `MedicalServices.cs` was read during the survey.
   Grep every `GetByIdAsync` for a following `TenantId` comparison. Every sibling that *was* read
   had one, so the sweep confirms whether this is a single miss or a pattern.
3. `Items` — **honour it** rather than remove it; a claim form naturally submits its lines with
   the claim. ⚠ On the way past, check `Update`: it must **not** carry `Items`, or omitting them
   silently deletes every line (`replace-set-payload-convention`).

**Harness asserts**

- A tenant-B claim is a **404** (not 422) for a tenant-A approver, whatever its status.
- Items round-trip on create.
- Update with `Items` omitted leaves existing lines intact.

**Prerequisite:** confirm a second tenant exists in the dev DB for the cross-tenant probe.

---

### Slice 3 — Self-service claims + actor hole ✅ green 2026-08-17, 30 assertions

Implemented D-1; closed F-05's approval half and **F-09**. Harness: `run-slice3.mjs`.

**As-built:** the surface is a sibling controller, `MedicalSelfServiceController` at `api/medical/me`,
under the invariant `EmployeeHealthSelfServiceController` already states. Every id-addressed
operation resolves through one `LoadOwnClaimAsync` helper, and a claim belonging to somebody else is
a **404, not a 403**, so the surface cannot be used to enumerate claim ids.

**Reads are projected explicitly, not returned as DTOs — and it earned its keep immediately.**
`MedicalExpenseClaimSummaryDto` carries `IsFlaggedForReview`; returning the DTO would have told a
claimant their claim was flagged for fraud review. Keep projecting; do not "simplify" this later.

**F-09 was found while scoping this slice and had to be fixed before the surface opened.**
`ConsumePolicyUtilizationAsync` verified the policy's tenant, status and dependants but never that
the policy belonged to the claim's employee, so approving a claim naming a colleague's policy
consumed *their* annual limit. Only HR could mis-key that before; self-service would have made it
deliberately reachable by every employee. The fix also anchors the dependant lookup — once the
policy is known to be the claimant's, its dependants are theirs — which is why dependant claims are
safe to allow here.

**Deferred out of this slice: receipt upload (F-08).** See slice 3a.

**Changes**

1. New routes on `EmployeeHealthSelfServiceController` (or a sibling under the same rule): file own
   claim, list own, read own, add own items and documents.
2. The controller's existing invariant carries over verbatim — **no route or query parameter may
   carry an employee or owner id**, and any id-addressed child re-checks entitlement *through the
   parent*. That is Trap 2 in `hr-area-authz-pattern`, and a claim's items and documents are
   precisely the shape that bypasses it.
3. **Internal notes are unreachable here.** Do not call `GetInternalNotesAsync` from this surface.
4. **Actor hole, split by what the field actually is:**
   - `CreatedByUserId` is a *user* → **create** can drop the employee-link requirement when
     `EmployeeId` is supplied. An unlinked HR account should be able to file on behalf.
   - `ApprovedById` is an **Employee** FK (`hr-attendance-actor-conventions` shape) → **approval**
     genuinely needs an employee actor. Fix is a specific 422 instead of the current generic 400,
     plus documenting that adjudicators must be linked.

**Harness asserts**

Employee files own claim ✓ · cannot read another's by id ✓ · cannot approve / pay / flag ✓ ·
cannot see internal notes ✓ · cannot reach another's items or documents by id ✓ · unlinked HR
account *can* file on behalf but gets a specific refusal on approve ✓

---

### Slice 3a — Claim documents onto the controlled-upload gate ✅ green 2026-08-17, 22 assertions

Closed F-08. Harness: `run-slice3a.mjs` — **needs `node clamd-stub.mjs` alongside the API.**

**As-built beyond the plan below:** `HrMedicalClaimDocuments` was added to
`ControlledFileUploadCategories.SystemCleanScanRequired`. Every other HR personal-data category is
in that set, including `HrMedicalExamDocuments`; a receipt carries diagnosis, facility and amount,
so leaving it out would have let a tenant policy permit unscanned medical uploads. This is why the
harness needs a scanner where slices 1–3 did not.

**Not `HrAttachmentUpload`.** That helper resolves an uploading employee from the token and refuses
when there is none, because the rows it writes carry a required `UploadedById` Employee FK.
`MedicalExpenseDocument` has no such column, so reusing it would have locked out unlinked
administrative accounts — the same mistake F-05 was about. `MedicalClaimDocumentUpload` exists for
that reason and says so in its XML doc; do not "consolidate" the two.

⚠ **`apply-migrations` reports success against a stale assembly.** A migration must be compiled
into `ErpSystem.Data.dll` before `MigrateAsync` can see it — running the previously-built DLL prints
"Database migrations completed successfully" and applies nothing. It cost a false green here.
**Always verify against the database**, not the exit message:
`SELECT COL_LENGTH(...)` plus a `__EFMigrationsHistory` check.

⚠ **Register every scaffolded migration in `FastBuildMigrationMetadata.cs`.** Fast Debug builds
strip `Migrations\*.Designer.cs`, where a scaffolded migration keeps its `[Migration]` attribute —
without the entry it is invisible to startup `MigrateAsync`. It is `<Compile Remove>`d in the
default item group, so the entry is inert in a normal build and there is no duplicate-attribute
risk.

**The defect.** `POST api/medical-expense-claims/{claimId}/documents` takes
`CreateMedicalExpenseDocumentDto` with a caller-supplied **`FilePath`** — a client naming a path on
disk. This is the *identical* defect `EmployeeHealthController.AddExamDocument` was already fixed
for in this same area, where the XML doc records that it "let any authenticated user attach
arbitrary bytes on disk — including another tenant's — to a medical record".

**Why it was not fixed in slice 3.** `MedicalExpenseDocument` lacks the three DMS columns its
sibling `EmployeeMedicalExamDocument` already has (`FileUploadRecordId`, `DocumentRecordId`,
`DocumentVersionId`), and there is no upload category for claim documents. So the fix needs a
**migration** plus a new category — too much to carry inside a security slice, and a migration
deserves its own review.

**The work**

1. Add the three DMS columns to `MedicalExpenseDocument` + migration (guard-everything style; the
   snapshot must be regenerated — see `repo-db-and-hr-conventions`).
2. Add `ControlledFileUploadCategories.HrMedicalClaimDocuments = "hr-medical-claim-documents"`.
3. Replace the JSON POST with a multipart upload through `IHrControlledDocumentService`, modelled
   on `EmployeeHealthController.AddExamDocument` — including the rollback on failure and the DMS
   registration with an access profile.
4. Add a download endpoint via `HrDocumentDownload.ServeAsync` (a document that cannot be retrieved
   is pointless).
5. **Then** open upload + download on `MedicalSelfServiceController`, scoped through
   `LoadOwnClaimAsync` exactly like the item routes.

⚠ The clamd stub must run alongside the API if the new category is scan-mandatory, as area 10's
document slices needed (`node clamd-stub.mjs`).

### Slices 4–7 — UI, the W1 recipe an eighth time

Each slice: `types/hr/<x>.ts` → `services/hr/<x>.service.ts` → pages → sidebar entry →
`PermissionGate`. The backend is proven by this point, so these are build-and-verify.

| # | Slice | Contents |
|---|---|---|
| 4 ✅ | Reference & config | facilities, physicians, providers/plans, schemes/tiers |
| 5 ✅ | Health records | profiles, conditions, allergies, exams + document upload |
| 6 ✅ | Claims | expense claims, adjudication queue, NHIS, dashboard |
| 7 ✅ | Clinical | pre-authorisations, referrals, appointments |

**4 must come first** — the registers are empty and everything downstream needs a facility to
point at.

#### Slice 4 as-built (2026-08-17)

`types/hr/medical.ts` · `services/hr/medical-reference.service.ts` · `app/hr/medical/` (hub,
facilities, physicians, insurance + `[id]`, schemes + `[id]`) · sidebar group **Medical & Health**.

- Built on **`ResourceListPanel`** (standalone registers) and **`ResourceCollectionTab`**
  (plans under a provider, tiers under a scheme) rather than hand-rolled tables — that is the
  house pattern for exactly this shape and it kept four registers to roughly one screen each.
- **Enums are strings over the wire**, so the types are string unions with `*_OPTIONS` label
  arrays. Do not use the numeric values from `HREnums.cs` in the frontend.
- **Summary DTOs are narrower than create DTOs.** `HealthcareFacilitySummary` has no
  `physicalAddress`, `MedicalInsuranceProviderSummary` has no `licenseNumber`/`address`/`email`,
  and `PhysicianSummary` carries a single `fullName`. `toForm` therefore cannot round-trip those
  fields, so the edit dialog re-collects them instead of silently posting blanks. If an edit ever
  needs to preserve them untouched, switch `toForm` to fetch the full record first.
- **Physician verify is one-way** — no un-verify endpoint exists, so the action is hidden on
  already-verified rows rather than offered and refused.
- Sub-limits on plans and tiers are **caps within the annual limit, not cover on top of it**. The
  dialogs say so, because entering them as additions overstates entitlement and the service
  enforces the former.

**Verification** (no browser automation available in this environment): `tsc --noEmit` clean — 19
errors remain and all 19 are pre-existing in `src/app/inventory` and `src/services/inventory*`,
none in HR; `eslint` clean on every new file; and `dev-harness/hr-medical/route-check.mjs` confirms
all **17** API paths the service calls resolve against a running API, which is the failure a
typecheck cannot catch.

**Exam document upload (slice 5)** already runs through the shared controlled-document gate with
malware scanning and DMS registration — do not rebuild it, wire to it.

#### Slice 5 as-built (2026-08-17)

`types/hr/medical.ts` (health-record section) · `services/hr/medical-health.service.ts` ·
`app/hr/medical/health/` (profiles list + exams-due tab, and `[id]` with conditions / allergies /
examinations) · hub card + sidebar entry.

- **Severe and anaphylactic allergies are pinned to the profile header**, not left inside a tab.
  They are the one thing on the file someone may need in seconds, and a tab hides them.
- **The employee picker disappears on edit.** The update DTO has no `employeeId`, so a profile
  cannot be moved to another person; offering the control would imply an action that cannot happen.
- ⚠ **The exam-document upload route takes no id** — `examId` travels as a **form field**
  (`POST api/employee-health/exam-documents`), unlike every other HR upload where the parent is in
  the path. Noted in the service because it is easy to "correct" into a 404.
- **`AttachmentsPanel` was made generic** over a new structural `AttachmentLike` (`id`, `fileName`,
  `description`, `uploadDate`, optional `uploadedByName`) instead of being tied to
  `AppraisalAttachment`. Examination documents record the uploader as a *user*, so they have no
  `uploadedByName`; casting them to satisfy the old signature needed three
  `as unknown as any`, which would have hidden precisely the mismatch types exist to catch.
  Backward compatible — `AppraisalAttachment` satisfies the constraint, and every existing caller
  still typechecks.

**Verification:** `tsc` clean (same 19 pre-existing inventory errors), `eslint` clean, and
`route-check-health.mjs` confirms all **10** health-record paths resolve, with profile, exam,
condition and allergy creates exercised as its fixtures.

**The dashboard (slice 6)** is already correct and tenant-scoped in SQL with proper `.Include`s.
It only needs a screen.

#### Slice 6 as-built (2026-08-17)

`services/hr/medical-claims.service.ts` (four services: HR caseload, self-service, NHIS,
dashboard) · `app/hr/medical/` — `dashboard`, `claims` + `[id]`, `my-claims`, `nhis`.

- **The decisions from slices 1–3 are what the UI is shaped around.** Adjudication controls
  disappear once a decision exists rather than being offered and refused, because the API answers
  a second decision with 422. Payment appears only on an approved claim. NHIS submit shows only on
  a draft, settle only once the scheme has decided.
- **`my-claims` is a separate screen from `claims`, and that is the point** — the function raising
  a claim must not be the function approving it. Self-service has no approve, pay, flag or note
  affordance anywhere, and its sidebar entry is deliberately outside the medical-permission group
  because the API scopes it by token.
- **Flagging is never disclosed to the claimant.** The flag banner says so explicitly, and
  `OwnMedicalClaimSummary` has no flag field to leak.
- **A lines-vs-total mismatch is surfaced** on the claim detail before approval — the itemised
  lines and the stated total are independent inputs and disagreeing is worth seeing.
- Two **stale-state bugs were caught in review, not by the compiler**: the approve/reject buttons
  originally set state then fired the mutation in a `setTimeout`, and the NHIS dialogs read the
  selected claim back out of state. Both now pass the value as a mutation argument. `tsc` accepted
  both versions — this is the class of bug only reading catches.
- Claim document uploads are recorded as **`Receipt`**; per-type upload needs a custom control
  rather than the shared `AttachmentsPanel`, whose upload callback carries only a description.
  Worth revisiting if TDC wants invoices and discharge summaries distinguished.

**Verification:** `tsc` clean (same 19 pre-existing inventory errors), `eslint` clean, and
`route-check-claims.mjs` confirms all **18** paths resolve — 13 as the HR actor and 5 as an
ordinary employee against the self-service surface.

**Housekeeping during slice 4:** update `PermissionGate.tsx` — its "HR permissions are not seeded
yet, prefer `roles`" note is now **false** and will mislead the W3 sweep.
⚠ **Not done** — still open, see §11.

#### Slice 7 as-built (2026-08-17)

`services/hr/medical-clinical.service.ts` · `app/hr/medical/clinical/page.tsx` (three tabs:
pre-authorisations, referrals, appointments) · hub card + sidebar entry.

- **One page with three tabs, not three pages.** These are the same job at three points in a
  visit's life, and each list is modest; splitting them would have meant three sidebar entries for
  what an HR officer thinks of as one screen.
- **Actions appear only where the record's state admits them** — approve/reject on an undecided
  request, complete on a live referral, check-in on a booked appointment and check-out only once
  checked in. Offering them elsewhere would simply be refused by the service.
- **Create forms were not built.** Pre-authorisations need a policy, referrals need two
  facility/physician pairs, and appointments need a slot — all three are better authored from the
  employee's own record than from a global register, and the API supports it. See §11.

**Verification:** `tsc` clean, `eslint` clean, **9** clinical routes resolve.

---

## 8. Known-unverified — do not assume these were checked

- ~~**Repository `.Include` coverage against the detail DTOs.**~~ ✅ **AUDITED AND FIXED 2026-08-17**
  — `run-resolved-names.mjs`, **37 assertions**. It failed **20 of 37** on the first run: every
  detail read resolved no names at all. Root cause: `GenericRepository.GetByIdAsync(id)` loads no
  navigations and the medical services never used the overload that takes includes, so **list reads
  worked and by-id reads did not** — the uneven-`.Include` shape. `GetClaimsPagedAsync` built its
  own query without includes while the pending and flagged queues had them, and the claim create
  response returned a freshly-constructed entity whose navigations were never loaded. See F-10.
- ~~**~2,400 of 2,813 lines of `MedicalServices.cs`** were never read.~~ **Partly resolved:**
  slice 2 swept every fetch in the file for tenant checks (84 `GetByIdAsync` + 71 other
  single-entity fetches). That sweep looked *only* at tenancy — `.Include` coverage above is a
  different pass over the same code.
- ~~**Whether a second tenant exists** in the dev DB.~~ **Resolved slice 2** — there was only one;
  `run-slice2.mjs` now creates a real second `Tenants` row (idempotent, all default-tenant flags
  off) for cross-tenant probes.
- **Whether anything here should meet the workflow engine.** Claim approval is currently bespoke
  single-step — the same shape as the `goal-approval-stays-bespoke` exception. Leave it unless
  multi-step medical approval is wanted.
- **The SHE↔Medical bridge.** `SheOccupationalHealthSurveillance` references `HealthcareFacility`
  by FK, but there is **no** link to `EmployeeHealthProfile` / `EmployeeMedicalExam`. The
  "bridge by reference" from the `she-medical-ownership-boundary` slice-9 call is **owed, not
  built**. Note the dependency: SHE surveillance cannot name an examining facility until slice 4
  populates that register.

---

## 9. Open questions for TDC

✅ **Added to `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` as item 7, 2026-08-17** — "How does TDC actually
pay for medical treatment?" It asks for the rough share of spend across NHIS, insurer-billed and
out-of-pocket reimbursement, and whether employees are expected to claim money back at all.

Does not block anything — all three routes are built and working. It changes emphasis and where
staff are pointed, not whether the module functions. The doc's intro and heading numbering were
updated with it (items 1–4 and 7 need an answer; 5, 6 and 8 are for information), and the
completed-areas count moved from 13 to 14.

---

## 11. Deferred register — everything consciously left out, and where it lands

Nothing here is forgotten; each item has a home or an explicit trigger. Add to this list rather
than dropping a deferral into a commit message.

| What | Why deferred | Where it lands |
|---|---|---|
| ~~**F-08** — claim documents on a caller-supplied path~~ | ~~Needs 3 new columns + a migration~~ | ✅ **done, slice 3a** — slice 6 unblocked |
| **F-07** — ~40 reads filter tenant in memory after `GetAllAsync()` | Correct, just wasteful; invisible at current data volumes (every medical table was empty) | No slice yet. **Trigger:** the first list screen that feels slow in slice 4–7, or the first tenant with real claim volume |
| **Employee edit / withdraw of their own claim** | D-1's scope line was file / list / read / items / documents. A claim filed in error currently needs HR to delete it | **Trigger:** first UI feedback in slice 6. Small — an update route through `LoadOwnClaimAsync`, restricted to `Pending` |
| **SHE ↔ Medical bridge by reference** | The slice-9 call in `she-medical-ownership-boundary` was never built; surveillance has no link to `EmployeeHealthProfile` / `EmployeeMedicalExam` | **Trigger:** slice 5 (health records UI), where the absence becomes visible. Note SHE surveillance already FKs `HealthcareFacility`, so slice 4 partially unblocks it |
| **Workflow engine for claim approval** | Currently bespoke single-step, same shape as the `goal-approval-stays-bespoke` exception | **Trigger:** TDC asking for multi-step medical approval. Recipe in `workflow-engine-integration` |
| **Which payment route TDC operates** | All three tables were empty, so it cannot be inferred | Open question §9 — does not block, but shapes slice 6's UI |
| **Clinical create forms** (pre-auth, referral, appointment) | Slice 7 shipped the registers and the workflow actions but no authoring forms; all three are better raised from an employee's own record than a global list | **Trigger:** first time HR needs to raise one in the system rather than record one already raised |
| ~~**`PermissionGate.tsx` stale note**~~ | ~~Says HR permissions are not seeded~~ | ✅ **done 2026-08-17.** Both claims were stale — the three `HR.Medical.*` permissions are seeded, and only the role `HR` exists (no `HR User`). Note now states which areas are permission-gated vs role-gated, and the two medical exceptions (open facility reads, token-scoped self-service) |
| **Per-type claim document upload** | `AttachmentsPanel`'s upload callback carries only a description, so every claim document is recorded as `Receipt` | **Trigger:** TDC wanting invoices and discharge summaries distinguished |
| **No frontend test coverage** | This environment has no browser automation, so UI verification was types + lint + route resolution only | **Trigger:** a UI regression, or the first e2e harness in `e2e-tests/` |

| **F-11** — provider documents take a required caller-supplied `FilePath` | Same shape as F-08 but **not a read sink** — no DMS columns, and no download route at all, so nothing serves the file. The feature records a path to a document the system never received and can never return. Not wired into any UI, so nothing is broken for a user today | **Trigger:** anyone wiring provider documents into the insurance screen. Fix is slice 3a's recipe a third time: 3 DMS columns + migration + category + multipart + download |
| **Policy dependants never executed** | `EmployeeDependents` is empty and creating one belongs to another module, so the add/update dependant paths could not be driven | **Trigger:** the first employee dependant fixture. The rest of the insurance lifecycle is now covered |

## 10. Correction log

Things that were got wrong during the survey and corrected. Recorded so they are not re-derived.

- **Re-approve returning 500 is not a defect.** `MedicalWorkflowException` *is* mapped
  (`NotFound`→404, else→422). The 500 with a stack trace was the Development exception page
  pre-empting the mapping. Run the harness in Staging.
- **`GET api/safety/return-to-work` returning `[]` against 7 DB rows is not a defect.** All 7 are
  soft-deleted by the area-10 harness cleanup.
- **Gating facility/physician reads behind `MedicalReadPolicy` would be wrong** — it would break
  slice 3 two slices later. See D-3. The original slice sequence had this error.
- ⚠ **The backend defect check was incomplete through slices 4–7, and I said it was done.**
  Authorization and tenancy were checked thoroughly (171 assertions). **Content correctness and
  write-path execution were not**, and §8 had flagged the first of those on day one as "the largest
  remaining defect risk" before four UI slices were built on top of it. When finally run:
  resolved names failed **20 of 37**, and ~24 state-changing endpoints — eleven of them wired to
  slice 6 and 7 screens — had never executed once.

  **The generalisable lesson:** a harness that asserts status codes and authorization proves the
  gate, not the feature. Three things have to be checked separately, and none implies the others:
  *can the caller in?* (status), *did the right bytes come back?* (content), *did the state
  actually move?* (execution). Route resolution proves the least of all — it only shows a handler
  is registered. Run all three **before** building UI, not after.
