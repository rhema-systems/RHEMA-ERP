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

**Update rules.** Close a finding by moving its status to `fixed` with the slice number, don't
delete it. Add new findings as they're discovered, with evidence. When a slice closes, tick it in
§2 and record the real assertion count.

---

## 2. Status at a glance

| # | Slice | State | Assertions |
|---|---|---|---|
| 1 | Finish the gating job | ☐ not started | target ~90 |
| 2 | Cross-tenant adjudication + claim contract | ☐ not started | target ~40 |
| 3 | Self-service claims + actor hole | ☐ not started | target ~70 |
| 4 | UI — reference & config | ☐ not started | target ~40 |
| 5 | UI — health records | ☐ not started | target ~50 |
| 6 | UI — claims, NHIS, dashboard | ☐ not started | target ~60 |
| 7 | UI — clinical | ☐ not started | target ~40 |

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

`Server=. ; Database=ErpSystemDB ; User Id=sa ; Password=ewing25!`

**Gotcha:** identity tables are `Users`, `UserRoles`, `AspNetRoles` — *not* `AspNetUsers`. Two
queries were lost to this. Role grants join `RolePermissions → Permissions` + `AspNetRoles`.

### 4.4 Smoke fixtures left in the dev DB

Created 2026-08-17. Harmless, and usable as fixtures — clear them if you'd rather start clean.

```
facility  797bf770-1628-426e-aaa5-2b25610b754a   Smoke Hospital 804179M
provider  d1154446-35b5-4eeb-838b-bbe7b0947407
plan      b6560aa3-6934-4751-b4ba-4d7204681458
policy    fd2e3581-4646-423b-a995-a5bbc8f2a46f
scheme    ff27dad6-c9c0-4ab1-8b54-3b11dc4bb41d
profile   e0dcf8d9-db67-43f9-8e0c-7f642248a296
exam      a0073dcf-01af-49f9-bcab-9709f6b0f99d   (soft-deleted by the gate test)
claims    1 live + 1 soft-deleted
```

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
| F-01 | **critical** | Role fallback grants any `HR.*` permission regardless of verb | open → slice 1 |
| F-02 | **high** | 3 controllers still on bare `[Authorize]` | open → slice 1 |
| F-03 | **high** | `ProcessApprovalAsync` never checks the claim's tenant | open → slice 2 |
| F-04 | medium | `CreateMedicalExpenseClaimDto.Items` silently discarded | open → slice 2 |
| F-05 | medium | Claim create/approve/note demand an employee-linked *caller* | open → slice 3 |
| F-06 | medium | On-behalf-of branch unreachable (permission + actor) | open → slice 1/3 |
| F-07 | low | ~40 reads filter tenant in memory after `GetAllAsync()` | open → deferred |

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

### Slice 1 — Finish the gating job

**Target ~90 assertions.** Closes F-01, F-02, F-06 (partial).

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

### Slice 2 — Cross-tenant adjudication + claim contract

**Target ~40 assertions.** Closes F-03, F-04.

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

### Slice 3 — Self-service claims + actor hole

**Target ~70 assertions.** Implements D-1; closes F-05, F-06.

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

### Slices 4–7 — UI, the W1 recipe an eighth time

Each slice: `types/hr/<x>.ts` → `services/hr/<x>.service.ts` → pages → sidebar entry →
`PermissionGate`. The backend is proven by this point, so these are build-and-verify.

| # | Slice | Contents |
|---|---|---|
| 4 | Reference & config | facilities, physicians, providers/plans, schemes/tiers |
| 5 | Health records | profiles, conditions, allergies, exams + document upload |
| 6 | Claims | expense claims, adjudication queue, NHIS, dashboard |
| 7 | Clinical | pre-authorisations, referrals, appointments |

**4 must come first** — the registers are empty and everything downstream needs a facility to
point at.

**Exam document upload (slice 5)** already runs through the shared controlled-document gate with
malware scanning and DMS registration — do not rebuild it, wire to it.

**The dashboard (slice 6)** is already correct and tenant-scoped in SQL with proper `.Include`s.
It only needs a screen.

**Housekeeping during slice 4:** update `PermissionGate.tsx` — its "HR permissions are not seeded
yet, prefer `roles`" note is now **false** and will mislead the W3 sweep.

---

## 8. Known-unverified — do not assume these were checked

- **Repository `.Include` coverage against the detail DTOs.** Only the dashboard's was verified.
  All 16 shapes in `hr-ported-list-read-bugs` remain unchecked across the area.
- **~2,400 of 2,813 lines of `MedicalServices.cs`** were never read. The tenant-check sweep in
  slice 2 is the first pass over them.
- **Whether a second tenant exists** in the dev DB (needed for slice 2's probe).
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

**Item 8 for `docs/HR-OPEN-QUESTIONS-FOR-TDC.md`** — which payment route does TDC actually
operate? The module supports three: NHIS, insurance-billed, and out-of-pocket reimbursement. All
three tables were empty, so we cannot tell from the data.

Does not block slice 3 — the segregation-of-duties argument for self-filing holds either way — but
it changes how prominent self-filing should be in slice 6's UI.

---

## 10. Correction log

Things that were got wrong during the survey and corrected. Recorded so they are not re-derived.

- **Re-approve returning 500 is not a defect.** `MedicalWorkflowException` *is* mapped
  (`NotFound`→404, else→422). The 500 with a stack trace was the Development exception page
  pre-empting the mapping. Run the harness in Staging.
- **`GET api/safety/return-to-work` returning `[]` against 7 DB rows is not a defect.** All 7 are
  soft-deleted by the area-10 harness cleanup.
- **Gating facility/physician reads behind `MedicalReadPolicy` would be wrong** — it would break
  slice 3 two slices later. See D-3. The original slice sequence had this error.
