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
2. **Attendance + consultant timesheets** — ✅ **DONE 2026-08-24, harness 47/47 green
   (+ 23/23 and 39/39 no-regression), grant rows verified in DB.** `HR.Attendance.{Read,Write,Admin}` over all 24 concrete
   controllers on `AttendanceControllerBase` (~275 actions). The split: org-wide reads
   (paged/status/pending/date/range, logs, alerts, biometrics, exports, dashboard, the
   consultant/engagement/invoice registers) → Read; manual corrections, log capture/process,
   apply-regularization, config writes, invoicing ops → Write; ALL deletes → Admin; punch and
   the request-shaped creates stay open (token-actor by construction — the base class's
   `TryGetTenantAndEmployee` resolves the actor); own-record reads, request update/delete
   (withdrawal), timesheet entries/confirmation → self-or-permission (`SelfOrPolicyAsync` etc.
   added to `AttendanceControllerBase`, resolving `IAuthorizationService` from RequestServices
   so 24 derived ctors stay untouched); approve/reject (regularizations, remote-work,
   timesheets) stay ungated — all three services are workflow-assignee-validated; shift/
   schedule/holiday/pay-period/geofence config READS stay open (employee-facing reference).
   Holes closed: any employee could edit/delete anyone's pending regularization or remote-work
   request (services check status, never the caller), read anyone's punches/records/biometric
   register, and amend anyone's timesheet entries. Two entity namespaces bit again:
   `StaffAttendance.ConsultantTimesheet` (the file's folder name is not its namespace).
   Sidebar: Attendance & Time and Consulting sections ride on HR.Attendance.Read.
   ⚠ ConsultantTimesheetDto.`ConsultantId` IS the employee FK (verified against the entity
   doc-comment) — self-checks key on it deliberately.
3. **Comp & Benefits** — ✅ **DONE 2026-08-25, harness 27/27 green (+ 47/47, 23/23, 39/39
   no-regression), grant rows verified in DB.** `HR.Compensation.{Read,Write,Admin}` over 7 controllers /
   71 actions (SalaryReviewProposals already role-gated, converts with the area-24 batch).
   The split: position pay, the pay-component master, salary grades/levels/notches, benefit
   grade-values, enrollment-by-policy and payroll-lines → Read; assignments, catalogue writes,
   enrollment/status/claim decisions, reconcile, the payroll syncs → Write; deletes AND
   deactivations → Admin (deactivating a pay component or benefit policy switches off pay);
   benefit CATALOGUE reads stay open (published policy — the grade-values money view does not);
   an employee's OWN pay components/summary/encashment-rate quote and their enrollments,
   balance, utilization claims, dependents and beneficiaries → self-or-permission (the
   medical-area decision: employees file their own claims and maintain their own beneficiary
   nominations). Guard-injection was scripted for the 9 enrollment record-scoped actions —
   verify the injected guards on the multi-line signatures when reviewing.
   ⚠ Script trap recorded: a Post|Put|Patch catch-all regex re-matched the deactivate line its
   own earlier rule had just gated, stacking Write on top of Admin (ANDed, functionally
   harmless, still wrong) — when rule N excludes rule M's lines, test the exclusion against the
   INSERTED text, not the matched line.
4. **Training** — ✅ **DONE 2026-08-25, harness `run-slice8-training.mjs` 90/90 green (+ 39/39,
   23/23, 47/47, 27/27 no-regression — 226 assertions total), grant rows verified in DB
   (HR → Read+Write; SuperAdmin/TenantAdmin → all three), re-verified green on the seeded rows
   after `seed-db`.** ⚠ Two operational traps caught here: **permission grants are seeded by the
   `seed-db` COMMAND, not at API startup** — a green harness straight after a build is running on
   the role-fallback handler, so run `seed-db` and re-verify before calling a slice done; and
   **`Permissions.Description` is nvarchar(500)** — an over-long description fails the whole
   permission SaveChanges, so keep new family descriptions under 500 chars. `HR.Training.{Read,Write,Admin}` over the 21 controllers carrying
   `[TrainingBusinessRulesAttribute]` (the definitive family marker — a `*Training*/Trainer/
   Learning/Mentor/Compliance` filename glob missed `EmployeeCertificatesController`; grep the
   FILTER, not the name). 178 scripted attribute inserts keyed by (file, method-name) — no verb
   catch-alls — plus hand edits for every self-or-permission action (per-controller private
   `SelfOrPolicyAsync`, the Emoluments idiom, in 10 controllers).
   The split: org-wide registers (requests/nominations paged, budgets, plans, vendors, trainers,
   pending-verification, needs assessments, enrollments, active pairs, bonds, unverified certs,
   compliance overdue, dashboard/analytics, status-history-free trainer reads) → Read; desk ops
   (create/update everywhere, request approve/reject — NO workflow integration exists for
   requests, attendance marking, completion record/verify — the `IsVerifiedByManager` flag had NO
   manager check and triggers skill write-back, certificate issue, schedule approve/cancel/
   complete, waitlist offer/promote, bond create/accept-on-behalf/record-exit/settle, pairing) →
   Write; deletes, budget approve + plan approve (spend authorization, manpower-budget precedent),
   certificate revoke, vendor blacklist/unblacklist, bond waive (forgives money) → Admin;
   catalogue/calendar reads (programs + materials/skills/competencies, program groups, category
   options, schedules incl. upcoming/open-for-registration/sessions, learning-path definitions,
   mentoring programmes, status history — the my-learning step page feeds on it, and the two
   nomination pending queues — the approver's survey surface) stay OPEN; self-or-permission:
   every by-employee read (11 routes), nomination create/submit/withdraw + feedback + follow-up
   (subject from body/record), request create/update/submit + delete-as-withdrawal (Admin arm),
   waitlist join/remove/respond, learning-path enroll/enrollment-read/recalculate/step-detail
   (recalculate is CALLED BY the my-learning screens — a Write-only gate would have broken the
   learner flow), employee-certificate CRUD (employees register their own external certs).
   **Kept validated-in-service, endpoint open:** nomination approve/reject (workflow-assignee via
   `CanUserApproveAsync` — but the LEGACY no-WorkflowInstanceId path accepted any caller posting
   `approverRole:"HR"`; now held to HR-shaped roles by `EnsureLegacyDecisionAllowed()` in the
   service), bond self-accept (service checks own-bond), learning-path step update (learner-or-HR
   + evidence rules), mentoring pair/session acts (`EnsurePairVisible`) — **except `ClosePairAsync`,
   the one pair mutation that skipped the gate (any internal user could end anyone's mentorship);
   it now calls `EnsurePairVisible`.** Manager-observation stays open, self-attributed (7%
   ManagerId coverage — org-authority model deferred). `TrainingCertificatesController`'s
   anonymous verify untouched (D-4). TrainingDashboard's `employee/{id}` role gate converted to
   Read. Frontend: sidebar-only (slices 5–7 shape) — Analytics/Completions/Compliance children +
   Service Bonds on HR.Training.Read, self-service children open; the Requests page's register
   tab now needs the desk read (self-service create still works; a mine-view fallback is a UI
   nicety left for the area's own backlog, as is a bond self-accept screen).
5. **Recruitment** — ✅ **DONE 2026-08-25, harness `run-slice9-recruitment.mjs` 66/66 green
   (+ 39/23/47/27/90 no-regression — 292 assertions total), `seed-db` run, grant rows verified
   in DB (HR → Read+Write; SuperAdmin/TenantAdmin → all three).** `HR.Recruitment.{Read,Write,Admin}` over 15
   controllers (the 14 carrying `[RecruitmentBusinessRules]` minus untouched JobInterview, plus
   RecruitmentDashboard and the ungated outlier PositionVacancies). 304 scripted placements —
   215 inserts on bare actions + 89 REPLACEMENTS of method-level `[Authorize(Roles=SuperAdmin,HR)]`
   (exactly the 89 the survey counted); 9 class-level role gates swapped (8 → InternalOnly +
   per-action tiers, RecruitmentDashboard → Read class-wide); dead HrRoles consts removed.
   ⚠ Script trap recorded: the "already gated" guard must inspect only the method's OWN contiguous
   attribute block — an N-line window above the declaration reads a neighbouring one-liner
   action's freshly inserted attribute and silently skips alternating methods (caught because the
   pass was re-run to convergence: 2nd pass +71, 3rd pass 0).
   The split: registers/exports/downloads → Read; desk ops (requisition hold/cancel/fulfill,
   vacancy lifecycle + postings publish/expire, candidate + application maintenance incl.
   shortlist/reject/bulk/notifications/reviews, offer prepare/issue/revoke/benefits/letters,
   hire create/status/confirm-start, check run + item pass/fail/waive, question bank/preset/
   pipeline/template writes) → Write; deletes + establishment reconcile → Admin.
   **Kept validated-in-service, endpoint open:** requisition approve/reject (workflow
   `CanUserApproveAsync` + self-approval SoD + budget/establishment enforcement — FR-HR-136 bites
   at the requisition), offer approve/reject-approval (workflow, no legacy fallback — the OLD
   class role gate could refuse the true assignee; now InternalOnly + engine), requisition
   create/update/submit/recall + raise-requisition (requester acts; `CanActAsRequesterAsync`'s
   desk arm converted role→Write policy), vacancy stage-assignment complete/skip
   (`RequireStageOwnership`), the internal job board (published vacancies, my-applications,
   internal drafts), and the whole **JobInterviewController** — untouched by design: its service
   carries a complete per-record model (`EnsureHr`/`EnsureCanReadInterviewAsync`/
   `EnsureCanScoreAsAsync` — panelists score only as themselves), role-anchored like mentoring.
   Self-or-permission hand edits: requisition GetById/GetDetail/requested-by/attachments
   list+download (download was tenant-scoped only — any internal user could pull any requisition
   file). Anonymous kept: public portal (tenant-header + 5/10min apply + 3/10min CV upload),
   offer-response single-use token, panelist email-token confirms; candidate portal scheme
   untouched. Frontend: sidebar-only — desk children + Establishment on HR.Recruitment.Read;
   Requisitions (manager entry point) and My Panel stay open. Residuals: requisition comments
   gated desk-side (no UI exists; requester thread access owed if comments ever surface),
   hiring-manager/recruiter vacancy reads at Read (no my-vacancies surface), interview service
   checks stay role-anchored (a permission holder without the HR role is refused there).
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
