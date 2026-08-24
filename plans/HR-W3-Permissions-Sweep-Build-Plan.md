# HR W3 — Permissions Sweep (cross-cutting; blocks area 26)

**Started 2026-08-24.** The workstream the master backlog calls W3: seed the HR module-access
permissions, close the bare-`[Authorize]` surface before any self-registering member of the public
holds a main-scheme token (the area-26 prerequisite from the candidate-portal decision), and stage
the per-area `HR.X.{Read,Write,Admin}` conversion for the areas still on role gates.

## The measured position (2026-08-24, instrument re-runnable — §6)

Scope measured: `src/ErpSystem.Api/Controllers/HR` (245 concrete controller classes, 4,207
actions) plus the legacy root `Controllers/EmployeesController.cs` (`api/Employees`).

| Effective gate (per action) | Actions | Per class |
|---|---|---|
| **Bare `[Authorize]`** — any authenticated user, incl. a future ExternalUser candidate | **1,921** | 154 classes |
| Role gate (`Roles = ...`) — internal roles only, already ExternalUser-proof | 1,345 | 55 |
| HR permission policy (`HrPermissions.*`, 9 families) | 889 | 25 |
| `AllowAnonymous` — all deliberate (email-token confirms, certificate verify), all rate-limited | 30 | 5 |
| Portal scheme (`CandidatePortal` / `ConsultantClientPortal`) | 22 | 2 |
| No attribute at class level (every action individually gated) | 0 | 4 |

Zero actions are accidentally anonymous. The hole is the 1,921 bare-`[Authorize]` actions: today
"authenticated" means internal staff, but the moment area 26 puts candidates on the main JWT
scheme (decision in `candidate-portal-reuses-external-portal`), every one of them opens to an
anonymous member of the public who self-registered.

**Discovery made by the harness run (2026-08-24), which sharpens the model above:** the platform
already has `Middleware/ExternalUserAccessMiddleware.cs` — a deny-by-default **path-prefix
allowlist** for `ExternalUser`-role tokens, registered between authentication and authorization.
So the bare-`[Authorize]` hole is *latent*, not live, everywhere except under an allowlisted
prefix — and it is **live today** under `/api/procurement`, which is allowlisted wholesale
(proven: a fresh external account read `business-partners` and `partner-categories`, 200; now
cross-module defect #11 part (a)). The sweep below is therefore the **second layer**: its refuse
path is currently shadowed by the middleware on HR routes, and becomes load-bearing the day
area 26 widens the allowlist to serve candidates — which it must. Two gates keyed to the same
role, by design; the middleware says where externals may go, endpoint gates say what they may do
there.

## Decisions

- **D-1 · Seeding is runtime, not migration.** `hr.access` and `admin.hr` seed through
  `DatabaseSeedingService.SeedPermissionsAsync` alongside `HrPermissions.All` / Finance — the same
  path every HR.* permission already uses. The `HasData` block in `ApplicationDbContext` uses fixed
  sequential GUIDs (insertion is fragile) and would demand a scaffolded migration for no gain.
- **D-2 · `hr.access` is "may enter the HR module UI", granted broadly to internal roles**
  (SuperAdmin, TenantAdmin, Admin, Manager, Employee, ReadOnly, HR, legacy "HR User", both MD
  spellings, Internal Audit — helpdesk trio deliberately excluded). Rationale: `/hr` was previously
  ungated at the layout, and non-HR staff legitimately use surfaces under it (peer evaluations,
  team goals, acknowledgements, payroll screens). The gate's *purpose* is excluding ExternalUser;
  fine-grained control stays with per-screen gates and the API. Roles absent on a tenant are
  skipped by the seeder loop — the legacy spelling costs nothing where already migrated.
- **D-3 · `admin.hr` goes to SuperAdmin, TenantAdmin, Admin, HR (+ legacy).** HR practitioners
  maintain their own reference data (leave types, org structures); granting them `admin.hr` gives
  them the Administration → HR screens the sidebar previously hid from them, while the new route
  gate now excludes everyone else (previously any authenticated user could reach
  `/administration/hr/...` by URL — the sidebar was the only gate). *User veto point: if TDC wants
  HR setup admin-only, drop the two HR rows from the `admin.hr` grant.*
- **D-4 · The InternalOnly sweep is a pure tightening.** Every bare `[Authorize]` in
  `Controllers/HR` (class- or method-level) becomes `[Authorize(Policy = "InternalOnly")]`
  (authenticated AND NOT ExternalUser — registered at `ServiceCollectionExtensions.cs:1207`, same
  literal the Ehc controllers use). No internal user's behavior changes; ExternalUser tokens lose
  1,921 actions. The four no-class-attribute controllers additionally get a class-level
  InternalOnly (defense in depth; their methods stay individually gated — stacked `[Authorize]` is
  ANDed). `TrainingCertificatesController` keeps no class gate: its only action is the deliberately
  anonymous verify link. The legacy root `EmployeesController` (`api/Employees`) is swept too.
- **D-5 · `PayrollController.cs` is NOT edited** (87 bare actions, `api/hr/payroll`) — payroll is
  another developer's module (`payroll-ownership-boundary`). The exact one-line change is recorded
  in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` for the payroll owner; until it lands,
  area 26 must treat payroll as exposed.
- **D-6 · The rest of the API is measured, recorded, not edited.** Bare `[Authorize]` outside HR
  is other teams' surface; counts per module go into the cross-module defects doc as the area-26
  prerequisite. Recommendation recorded there: each module sweeps its own, or the platform sets an
  authorization **FallbackPolicy** of InternalOnly (which would also close endpoints with *no*
  auth metadata) — a platform decision for finalization, not an HR edit.
- **D-7 · Frontend gates.** `/hr` layout requires `hr.access`; the administration layout gets an
  `/administration/hr` → `admin.hr` branch (same pattern as maintenance/fleet/project); the
  sidebar's HR section carries `permissions: ['hr.access']` and Administration → HR becomes
  role-OR-`admin.hr`. `hasAnyPermissionAccess` auto-passes SuperAdmin/TenantAdmin and `*`.
- **D-8 · Per-area permission families are staged, not blanket-converted** (§5). Converting 154
  controllers to `HR.X.Read/Write/Admin` in one pass without per-area actor audits would repeat
  the area-11 lesson in reverse — breaking employee self-actions (acknowledge, respond, my-*)
  that must stay self-or-HR, not HR-only. Each family lands the way the existing nine did: with
  its area's audit and harness.

## Slices

### Slice 1 — `hr.access` / `admin.hr` (backend seed + grants) ✅ this session
`DatabaseSeedingService`: two permission seeds (categories "Module Access" / "Administration
Modules", matching the platform's `project.access` / `admin.project-management`), grants per D-2/D-3
appended to `rolePermissionMap` (new entries for Manager, Employee, ReadOnly, Admin, legacy
"HR User"; concat onto the existing SuperAdmin/TenantAdmin/HR/MD/IA entries).

**Defect found and fixed while in the file:** `rolePermissionMap` used the indexer-style
initializer (`[key] = value`), which silently **overwrites** duplicate keys — and
`"Managing Director"` appeared twice: Finance's deliberately-narrow executive entry, then area
9b's HR entry (`Constants.Roles.ManagingDirector` is the same string). The HR entry had been
erasing the MD's Finance grants since 9b landed. Merged into one entry with a warning comment;
the second key removed. *Generalizable check: any seeded map keyed by role name, grep for a
constant and its literal spelling appearing as separate keys.*

### Slice 2 — InternalOnly sweep ✅ this session
Scripted line-exact replace across `Controllers/HR` (UTF-8/BOM/CRLF-preserving Node script — the
PowerShell bulk-edit trap is real), class-level attributes added to `OathsOfSecrecyController`,
`ProbationController`, `SeparationsController`, legacy root `EmployeesController` swept,
`PayrollController.cs` excluded per D-5. Verified by re-running the inventory instrument: bare
count must fall 154 → 1 class (payroll) / 1,921 → 87 actions (payroll).

### Slice 3 — frontend route + nav gates ✅ this session
Per D-7. Screen-level `PermissionGate` stays on `HR_ROLES`/`HR_ADMIN_ROLES` roles until each
area's permission family lands (slice 5) — the gate component itself documents this split.

### Slice 4 — the W3 harness ✅ written this session, runs after next build
`dev-harness/hr-w3-permissions/run-w3.mjs`: mints an ExternalUser-role user and an HR-role user
via the admin API (pass the token's `tenant_id` explicitly — `CreateUser` does not fall back to
the caller's tenant and 547s), then asserts (a) a broad per-area sample of swept GETs 403s for
the external token — note this proves the **stack** (the middleware answers first on HR routes;
the swept attribute is the second layer behind it), (b) the same sample does NOT 403 for HR,
(c) the deliberate anonymous endpoints still answer without a token, (d) legacy `api/Employees`
refuses the external token, (e) the procurement live hole (#11a) and payroll's middleware-only
shielding are *observed* as canaries that flip when their owners act.
**First run 2026-08-24: 39/39 green** — and the run itself surfaced the middleware (the payroll
probe 403'd where the model said it would be admitted; chasing that found
`ExternalUserAccessMiddleware` and the procurement hole).

### Slice 5+ — per-area permission families (staged, one area per slice, harness-verified)
Extend `HrPermissions` per area, register policies, convert class gates, extend `RoleGrants`
(both the seed and the fallback read it — never let them drift), swap the area's screens from
role-based `PermissionGate` to permissions. Order (risk-ascending, self-service-heavy last):
1. **Leave** — ✅ **DONE 2026-08-24, harness 23/23 green + 39/39 no-regression, grant rows
   verified in DB.** `HR.Leave.{Read,Write,Admin}`: org-wide reads → Read; adjustments, close,
   recalculate, encashment payment, type-catalogue writes → Write; year-end jobs, deactivate,
   deletes → Admin; file/amend/submit/cancel/own-reads → **self-or-permission** (ownership
   helpers on all three controllers, evaluated via `IAuthorizationService` so seed AND fallback
   count); approve/reject/suggest-changes deliberately ungated — the workflow engine validates
   the assignee per request, and a permission would break line-manager approvers; leave-type
   READS stay open (the request form feeds on them — medical's facility-register precedent).
   Real holes closed en route: anyone could file leave AS anyone (body employeeId, unchecked),
   cancel anyone's approved leave, delete anyone's leave attachment, run tenant-wide year-end.
   Sidebar: HR-desk leave items gate on HR.Leave.Read, Year-End on Admin; Requests/Approvals
   stay open (self-service and manager surfaces).
   ⚠ Recipe note for the next slices: a service method named like `GetOwnedX` may be a plain
   tenant-scoped fetch with no caller check — read it; and an attachment DTO carries
   `LeaveRequestId`, not the nav (`attachment.LeaveRequest` was this slice's one build error).
2. **Attendance + consultant timesheets** (self clock-in/regularization surfaces)
3. **Comp & Benefits** (71 bare)
4. **Training** (266 bare; nominations/self-enrollment are employee acts)
5. **Recruitment** (147 bare; panelist confirms stay anonymous-tokened)
6. **Performance** (350 bare — the largest and most actor-diverse: self, peer, manager, HR;
   do last of the big areas, with the area-5 actor matrix open)
7. **Foundation/Employee core** (280 bare — pickers feed every module; follow medical's
   precedent of leaving name-lookup reads open to internal users)
8. **SHE** (employee incident reporting is an any-internal-actor act by design)
9. Orientation, Assets, Movements, Discipline: mostly role-gated already — convert
   role → permission mechanically, per-area.
10. HR settings/company + satellites + org extras + dashboards.

The recipe and the two closing greps (service methods ↔ screens, non-GET routes ↔ service) from
area 16 apply to every slice.

## What W3 explicitly does NOT do
- No payroll edits (D-5), no other-module edits (D-6).
- No per-area family conversions without their own slice + harness (D-8).
- No `RoleGrants` widening by prefix — add per role, per area; the map is the single source for
  seed AND fallback.

## Verification
- Inventory instrument re-run (§6) after slice 2: numbers above.
- `npx tsc --noEmit` after slice 3: no new errors (pre-existing inventory errors excepted).
- Backend build: user-run.
- Harness: user runs `run-w3.mjs` in Staging with the JWT key after the build
  (`hr-harness-run-environment`).

## §6 The instrument
`node` script over `Controllers/HR` classifying every class and every action's *effective* gate
(method attr, else class attr, walking attribute lines above declarations; catches the four
area base classes — `SheApiControllerBase`, `AttendanceControllerBase`, `MedicalControllerBase`,
`HrControllerBase` — that a naive `ControllerBase` match misses, which is how a first pass
under-counted by 76 controllers). Outputs `hr-actions.json` / `hr-classes.json`. Kept in the
session scratchpad; cheap to regenerate — the regexes are in this plan's git history.
