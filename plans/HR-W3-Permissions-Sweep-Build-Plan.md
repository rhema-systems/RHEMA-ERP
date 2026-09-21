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
  in `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` for the payroll owner; until it lands,
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
6. **Performance** — ✅ **DONE 2026-08-25, harness `run-slice10-performance.mjs` 167/167 green
   (+ 39/23/47/27/90/66 no-regression — 459 assertions total), `seed-db` run, grant rows verified
   in DB (HR → Read+Write; SuperAdmin/TenantAdmin → all three), re-verified green on the seeded
   rows.** Two harness-side fixture lessons: an actor-guarded desk op (cycle open) answers 401
   for the unlinked HR fixture AFTER the Write gate admits it — assert not-403, not "answers";
   and a refusal probe's body must satisfy every ModelState rule incl. `[MinLength(1)]` on
   collections, or the 400 answers before the code guard. `HR.Performance.{Read,Write,Admin}` over
   all 36 controllers of the area (427 actions): 143 scripted policy placements (4 class + 43
   method-level role-gate REPLACEMENTS + 96 inserts, placement script keyed by (file, method) with
   own-contiguous-block gating and one-pass bottom-up splicing — ⚠ new script trap: per-method
   splicing shifts every later declaration index; collect ALL insertion points first, then splice
   once, or alternating methods land off-by-N) plus hand edits in 18 controllers.
   The split: org-wide registers (appraisal/appeal/check-in/development-plan/PIP registers,
   review-events by cycle, recommendation worklist, rating distribution, org-wide at-risk, HR
   cycle dashboard, cycle progress/coverage, goal-library usage, company-goal dashboard,
   goal-risk thresholds) → Read; desk ops (cycle lifecycle incl. open/close/generate/reminders,
   template/criteria/grade/settings/target authoring incl. template approve, appraisal
   create/update/raw-status/score, HR review approve-and-finalize/return-to-manager/progress,
   appeals begin/resolve/finalize, evaluator+criterion-score maintenance, calibration end to end,
   recommendation decide, deadline enforcement, company/strategic/library/KPI writes, goal
   unlock) → Write; deletes of records and catalogue + goal-risk reset → Admin (cycle-target and
   cycle-template unassign stay Write: same-object authoring, not data destruction).
   **The actor-diverse arms** (the reason this slice went last): every prior per-record helper's
   HR-role arm converted to a tiered policy check via `IAuthorizationService` (PipAccess shared
   helper, check-ins, conversations, review events, development plans + feedback, journal
   shared-arm, links, analytics trend), and NEW ownership guards added where the service never
   asked who was calling — appraisal GetById/self-eval-context/hr-review/evaluations/responses,
   peer-nomination CRUD (parties = peer/appraisee/manager), nomination batch (appraisee) vs
   approve/reject (manager-only), manager-peer-evaluations (manager-only, anonymity),
   employee-goal CRUD/progress (subject+manager), by-employee-id notification routes and
   single-notification read (recipient), recommendation by-appraisal (manager-only — a proposed
   termination is not its subject's to see before HR decides), check-in create-in-another's-name.
   **Kept open, validated elsewhere:** every token-actor self surface (self/manager/peer
   evaluation, acknowledge, appeals, /me and /mine routes), goal submit/approve/reject/lock (the
   bespoke goal workflow holds the direct manager), PIP approve/reject (workflow engine
   assignee), TeamGoals + GoalDetail (token-manager-scoped in the service), UnitGoals and
   CompanyGoals reads (the cascade is meant to be seen), calibration READS (area-5 decision:
   panellists need the grid — harness carries a canary), reference catalogues (cycles, templates,
   grades, KPIs, library selector, strategic goals). **Residuals recorded:** PIP authoring stays
   on `AuthorRoles` (SuperAdmin/HR/Manager — a permission would lock line managers out; a
   seeded-permission-only author is refused there), CheckIns `Redact` keeps the sync role check
   (a Read-holder without the role sees private notes redacted — safe direction), journal private
   entries exclude even permission holders (deliberate, matches the HR exclusion), calibration
   reads tenant-open per area-5. Frontend: sidebar-only — nine desk children on
   HR.Performance.Read, Deadline Enforcement on Write, all self/manager surfaces open.
7. **Foundation/Employee core** — ✅ **DONE 2026-08-25 (slice 11), harness
   `run-slice11-foundation.mjs` 142/142 green + full no-regression
   (39/23/47/27/90/66/167 = 601 assertions total), `seed-db` run, grant rows verified in DB
   (HR → Read+Write; SuperAdmin/TenantAdmin → all three), harness run against the seeded
   rows in Staging.** `HR.Employee.{Read,Write,Admin}` (category "HR - Employee Records &
   Foundation") over the employee master and the foundation registers: 244 scripted placements
   (226 inserts + 18 role-gate replacements in OrganizationUnit/Teams/Union/Organogram — their
   `WriteRoles`/`PeopleRoles` consts removed) across 27 controllers, plus hand edits
   (EmployeeRelievers' `MayTouch` role arm → tiered `MayTouchAsync(id, policy)`).
   **The line on the employee master (the decision that shaped the slice):** LEAN directory
   reads stay open — POST paged (the single shared `EmployeePicker`, imported by ~120 files,
   is the only picker and it calls exactly this), by-unit/level/location, by-id, by-number,
   by-email, direct-reports, management-chain, stats (the /hr landing tiles) and the dead
   technician routes — the summary `EmployeeDto` carries name/org/contact only. The PII reads
   are Read: `{id}/details` / `{id}/profile` (+ by-number twins) and all 14 sub-record familes
   (incl. bank details, salary assignments, guarantors — previously ANY internal user could
   pull ANY employee's salary, SSN/TIN, DOB, home address and bank account). All writes incl.
   sub-record verify/set-primary/activate → Write; deletes AND the legacy lifecycle quartet
   (activate/deactivate/terminate/reinstate — the separation module is the governed exit
   path) → Admin. Legacy root `api/Employees`: lean list reads stay open (live consumers:
   finance fixed-assets transfers, maintenance pickers, UserEmployeeLinks admin screen);
   its `{id}`/by-number reads returned the same `EmployeeDetailDto` PII → Read; writes
   Write, delete/terminate Admin. Foundation registers: ALL reads open (pickers feed every
   module — org/location structures/levels/units, positions lists, teams, unions, staff
   levels, skills, qualifications, identification types, reason codes, countries, the
   bank/branch reference master, departments, organogram units/positions/locations/teams);
   writes → Write; deletes → Admin; Organogram `people` (everyone's email) keeps its old
   SA/TA/HR reach as Read; `EmployeePositions/{id}` + `code/{code}` → Read (only the by-id
   reads Include the `PositionAmount` benefit money — list reads don't and stay open for the
   requisition/succession forms). EmployeePortal untouched (token-actor by construction,
   service-enforced `EnsureSelfOrHr` — verified). Relievers: reads self-or-Read, writes
   self-or-Write (row-derived owner, unchanged shape). Frontend: sidebar Employees item →
   HR.Employee.Read (the picker rides the open paged read); admin.hr keeps the setup tree.
   RoleGrants: HR → Read+Write.
   **⚠ Slice-6 gap closed here: SEVEN AttendanceControllerBase controllers the attendance
   conversion missed** (its "all 24 concrete controllers" census was wrong — the family has 31):
   StaffDailyAttendance (18), StaffOvertimeRequests (14), StaffMonthlyAttendanceSummaries (10),
   PositionOvertimePolicies (11), EmployeeOvertimeOverrides (6), StaffBulkAttendanceImports (9),
   EmployeeWorkSchedules (8) — 76 bare actions with live screens. Same split as slice 6:
   registers → AttendanceRead, desk ops → Write, deletes → Admin, by-employee reads
   self-or-Read (base-class `SelfOrPolicyAsync`), overtime request update/delete
   self-or-Write (withdrawal shape; `GetOwnedAsync` had NO caller check). Real holes closed:
   **file-overtime-as-anyone** (create takes `dto.EmployeeId` — now self-or-Write, the leave
   shape), edit/delete anyone's pending overtime request, and **confirm-actual-hours had no
   supervisor check at all** (any employee could set their own actual overtime hours → pay);
   confirm is now Write — a residual: the true supervisor confirm needs the org-authority
   model, until then the desk holds it. Position overtime policy reads stay open (employee-
   facing reference) except `{id}/overrides` (per-person rates) → Read.
   Residuals recorded: no self-profile screen exists (nothing self-facing broke; if one is
   built it must ride a narrowed self projection or self-or arms on the sub-record reads);
   `LeaveRequestForm` still reads relievers by client-supplied id where `getMine()` exists
   (works — self id on the self surface, desk holds Read — but is the warned-against
   pattern); the maintenance inspectors admin page calls a nonexistent
   `GET /api/hr/employees?department=` route (already broken before this slice).
8. **SHE** — ✅ **DONE 2026-08-25 (slice 12), harness `run-slice12-she.mjs` 82/82 green + full
   no-regression (142 + 39/23/47/27/90/66/167 = 683 assertions total), `seed-db` run, grant
   rows verified in DB (HR → Read+Write; SuperAdmin/TenantAdmin → all three).
   Defect found by the no-regression pass and fixed here: the count-based daily number
   generators (overtime requests, regularizations, bulk-import references) regenerated a
   soft-deleted row's number and 500'd on the unique index — any employee who filed and
   withdrew a request blocked the tenant's next one that day; all three now derive the day's
   max via `GetQueryableIncludingDeleted` (⚠ trap: this repo's soft delete is a MANUAL `Where`
   in `GetQueryable()`, so `IgnoreQueryFilters()` is a silent no-op — the first fix attempt
   used it and changed nothing).** `HR.She.{Read,Write,Admin}` (category
   "HR - Safety, Health & Environment") over 26 controllers, 485 scripted ops (339 inserts +
   126 method-gate replacements + 20 class-gate swaps to InternalOnly), verb-mechanical:
   reads → Read, writes AND every desk decision (incident close, permit approve/suspend,
   stop-work resolve/clear, audit close, the environmental review ladder incl.
   management-approve/issue-clearance/approve-commencement) → Write, deletes → Admin — the
   SHE desk keeps its exact reach; a future SHE-officer role (residual DR-10) can now be
   granted the family without the HR role, and TenantAdmin (previously excluded by the
   SuperAdmin/HR class gates) gains the area. The 6 reporting controllers' method-level
   `HrRoles` gates converted the same way; their four in-code `isHr` on-behalf arms (incident/
   env-incident report-as, stop-work raise-as, risk-assessment sign-as) converted to
   `HoldsPolicyAsync(SheWrite)` on a new base-class helper. Open by design, unchanged:
   incident/hazard/environmental-incident creates, stop-work raise + mine, PPE issuances/mine,
   risk-assessment acknowledgements. SheOccupationalHealth + SheReturnToWork stay on
   HR.Medical.* (the slice-9 boundary). **Defect fixed en route: the employee report-incident
   form's type picker fed on the desk-gated `reference/incident-types` reads and silently
   403'd — every employee-filed incident arrived unclassified; the two list reads are now
   open (the leave-type precedent).** Frontend: the 31 SHE desk sidebar children gated on
   HR.She.Read (occ-health/RTW on HR.Medical.Read); the 5 self-service items stay open.
   Residuals: the frontend comments claiming `api/Location` is HR-gated are wrong (it is
   open); the env-report screen's sentinel-id comment mis-describes the desk arm.
9. **Orientation, Assets, Movements, Discipline** — ✅ **DONE 2026-08-25 (slice 13), harness
   `run-slice13-role-areas.mjs` 100/100 green + full no-regression (683 = 39/23/47/27/90/66/
   167/142/82 — 783 assertions total), `seed-db` run, grant rows verified in DB (HR → 8 =
   4×Read+Write; SuperAdmin/TenantAdmin → 12 each). Two probes falsified on first run were
   deliberate opens, not defects: `Assets/types` (the AST-6 requisition form's type picker)
   and `staff-movements/awaiting-my-approval` (a token-scoped approver worklist) — both open
   before the slice too, now asserted as opens.** FOUR families in one slice — safe because every one of the 366 endpoint role
   gates across the 25 controllers was HR-desk-shaped (SuperAdmin/HR variants; zero
   decision-role attributes — approvals and natural justice live in the services where their
   areas put them): `HR.Orientation.*` (6 orientation controllers + OnboardingPlan/
   OnboardingPlanTemplate, whose SA/HR class gates the census initially missed),
   `HR.Assets.*` (Assets' 110 method gates + AssetReminders), `HR.Movements.*` (movements +
   promotions/transfers/secondments/acting/demotions/career-paths/reminders),
   `HR.Discipline.*` (cases, sub-entities, support, the offenses/action-types lookups,
   grievances, reminders). 530 scripted ops (165 inserts + 350 method replacements + 15 class
   swaps), verb-mechanical; deliberate opens untouched (orientation mine, discipline
   mine/confirmations, grievance raise+mine — HR still cannot file one — asset AST-6/AST-8
   self-service, portal surfaces). Six in-code role arms converted to tiered `HoldsAsync`
   checks (movements GetById/attachment-download/checklist-complete; discipline case
   GetById/process-clock reads, report-as arms, appeal read, evidence-download privilege
   redaction). Dead consts removed (16 HrRoles + DisciplineLookupRoles); PIP's AuthorRoles
   and HrLegacyFileMigration's SuperAdmin gate deliberately kept. **Slice-11 census gap
   closed: `OrganizationUnitHistoryController` (SA/TA/HR class gate) → class-wide
   HR.Employee.Read.** Frontend: 20 desk sidebar items gated on the family Reads (orientation
   5, assets 11, movements 2, discipline 2 + grievance register); My-* and Awaiting My
   Confirmation stay open. Remaining role-gated surface after this slice: 85 actions = PIP
   authoring (deliberate), separations MD/IA anchors (deliberate), company settings/profile +
   EmploymentActionProposals + SalaryReviewProposals + ExternalAssociates (slice 14), and the
   SuperAdmin-only legacy file migration tool.
10. **Company/administration tail (slice 14, FINAL)** — ✅ **DONE 2026-08-25, harness
    `run-slice14-company.mjs` 85/85 green + the full no-regression ladder
    (39/23/47/27/90/66/167/142/82/100 = 783 — 868 assertions total), `seed-db` run first and the
    grant rows verified in DB before the run (HR → Read+Write; SuperAdmin/TenantAdmin → all
    three), so the green proves the seeded rows, not just the role fallback — and HR's Admin-tier
    403s prove the fallback stays verb-aware. W3 IS COMPLETE: area 26 unblocks (subject to
    cross-module #11).** ONE new family `HR.Company.{Read,Write,Admin}` (category
    "HR - Company & Administration"; HR → Read+Write like everywhere) over four controllers:
    **CompanyProfile** (GET → Read, PUT → Write — HR keeps the letterhead write; the record
    carries TIN/VAT/SSNIT so reads stay gated), **CompanyHrPolicySettings** (GET → Read; PUT →
    **Admin, the one deliberate Admin-tier write** — the knobs move trust boundaries, the
    FR-HR-092 procedural-absence threshold and the FR-HR-136 enforcement modes, and the old
    role gate held the write to SuperAdmin/TenantAdmin; granting HR `HR.Company.Admin` is TDC's
    one-line change if it ever wants HR to hold them), **ExternalAssociates** (reads incl. the
    panel-picker search → Read, create/amend/activate/deactivate → Write, delete → Admin —
    same HR reach as the old SA/TA/HR gate minus delete, the standard trade), and the
    **dormant CompanySchedule** (90 bare actions, NO screen has ever called any — events/
    participants/attendance/attachments/tasks, meeting rooms, bookings, milestones, closures,
    fiscal years+periods; 50 reads → Read, 30 writes/decisions → Write, 10 deletes → Admin,
    scripted). **Nothing self-service is drawn there on purpose**: every actor id (organizerId,
    bookedById, approvedById, markedById, announcedById) is client-supplied, so an open
    "book a room"/"respond to invitation" surface would be act-as-anyone — residual: if a
    calendar/room-booking UI is ever built, move those paths to token actors first, then open
    with self-or-permission arms.
    **The proposals ride `HR.Performance.*`** (they are the downstream half of the appraisal
    outcome-recommendation flow, and the UI puts them under /hr/performance/proposals):
    EmploymentActionProposals + SalaryReviewProposals reads → Read (desk-only, per the slice-10
    rule that a proposed outcome is not its subject's to see); submit, the salary figure PUT,
    mark-actioned/mark-applied (status-checked only in the services) → Write;
    **approve/reject/recall carry NO permission** — both services validate
    `CanUserApproveAsync` (approve/reject, no legacy fallback) or the engine's initiator check
    (recall), and the old SuperAdmin/HR class gate would have refused a non-HR assignee a
    published definition names (the slice-9 offer shape). Residual: a non-HR workflow assignee
    (e.g. an MD) can act but cannot read the register — the `ApprovalReaderGrants` MD-reader
    precedent is the fix shape when TDC publishes such a definition.
    **Slice-9 census gap closed: `TalentPoolController`** (`api/talent-pool`, recruitment's
    candidate CRM — pooled candidates, segments, engagement events, analytics, vacancy
    matching; NOT succession's `api/talent-pools`). Bare since the port, no screen calls it,
    zero in-code gates: any internal user could read pooled candidates' profiles and run desk
    writes. Onto `HR.Recruitment.*` verb-mechanically (10 reads → Read, 9 desk ops → Write,
    segment-catalogue + engagement-event deletes → Admin; the candidate-segment unassign stays
    Write per the slice-10 same-object rule).
    Frontend: NO changes — the proposals sidebar item was already on HR.Performance.Read
    (slice 10), external-associates + the settings pages ride the admin.hr section gate, and
    CompanySchedule has no UI. Deliberate keeps re-confirmed by the closing census: PIP
    AuthorRoles, Separations MD/IA anchors, the SuperAdmin migration tool — and the un-policied
    remainder is all service-gated/self/portal surfaces plus payroll (#11).

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

## Slice 15 — the 2026-09-03 permissions review: menu gates, DR-10 SHE roles, self-service split

**Trigger:** the stakeholder demo showed HR menu items other modules would have hidden. A
solution-wide review found one mechanism (Permission/RolePermission rows → `PermissionAuthorizationHandler`;
sidebar `filterNavItems`) in four dialects, and that HR was the most complete backend but had
**79 sidebar leaves with no gate of their own** while `hr.access` is granted to every internal role.

**Built and VERIFIED 2026-09-03** — full ladder **915/915** on the rebuilt API (39/129/23/47/27/90/66/167/142/100/85; slice 12 grew from 82 to 129 with the Safety Officer fixture and the HR-refused-on-write lines). Database checked after the startup seed: both roles present, `she.access` seeded and granted to Safety Officer/SHE Manager/HR only, `HR.She.*` relabelled "Safety (SHE)", `HR.She.Write` revoked from HR (log line "Revoked 1 permission grant(s)"), 13 SafetyCompliance topics re-addressed (HR system rows soft-deleted, both SHE roles added). ⚠ The `she.officer` persona re-cast only applies where the persona exists (UAT database via `seed-hr-demo`); the dev database has no persona to check.

**Built:**
- Sidebar: 36 leaves + the Payroll group gated on their family's Read (Payroll on
  `HR.Compensation.Read`, menu-only — #11 still owns the API); My Competencies moved to
  `/me/competencies`; Training Certificates defaults to the desk view and is gated. The 14 leaves
  that stay open (confirmed by the user) are pinned by `sidebar-hr-gates.test.ts` — a new leaf
  without a permission fails the test unless it is added to KEEP_OPEN with a reason.
- Three defects from the page trace: `training-requests` "All" tab called a route that does not
  exist (now the paged read); facilities/physicians write buttons now follow Medical Write/Admin;
  Leave Requests opens on the caller's own history. Unit Goals reads are OPEN BY DESIGN
  (controller remarks: the cascade must be visible) — left open, listed in KEEP_OPEN.
- DR-10: `Constants.Roles.SafetyOfficer` / `SheManager`; `HrPermissions` grants them She R/W
  (+Admin for the manager) and Medical R/W; **HR drops to `HR.She.Read`** via the new
  `HrPermissions.RoleRevocations` map, applied by a delete pass after the add-only grant loop;
  `she.access` module gate (SHE roles, HR, admins) + `/hr/safety/layout.tsx`; SHE reminder topics
  re-addressed to the SHE roles (system rows only); permission Category relabelled "Safety (SHE)"
  in place (HR-owned rows only); `she.officer` persona re-cast (RetiredRoles removes HR).
- Harness: `resolveFixtureEmployees()` + `ensureUser`/`tenantIdFromToken` shared in `api.mjs`;
  slice 12 extended with a `w3.sheofficer` fixture (W3SHE employee), the seed/grant assertions,
  and HR-refused-on-write.

**Verification order after the build:** start in Staging → startup seeds roles/grants/revocation →
`run-w3.mjs` then `run-slice12-she.mjs`, then the rest of the ladder; `npx vitest run
src/components/layout/sidebar-hr-gates.test.ts`.

**Administration follow-up (same day, after the user asked whether the Roles/Users screens needed
restructuring):** the Roles screen needs no restructuring — it renders the database permission
rows grouped by Category, so the 21 HR families, the "Safety (SHE)" family and the two module
gates appear on their own; the Users screen's role picker reads `/api/role`, so the SHE roles
appear on their own. Three real gaps were closed: (1) the SHE reference-data settings sat under
Administration → HR behind `admin.hr`, which the SHE roles do not hold — moved beside HR as
Administration → Safety (SHE), gated `admin.hr` OR `HR.She.Write` in both the sidebar and the
administration layout (a `/administration/hr/safety` branch placed BEFORE the `/administration/hr`
one); (2) `HR`, `Safety Officer` and `SHE Manager` were plain roles an admin could rename or
delete although the seed map, the fallback handler and the role-anchored attributes key on the
exact names — added to `Constants.Roles.IsProtectedSystemRole` (the seeder promotes existing rows
to `IsSystemRole` on the next start; the row and name are protected, the permissions stay
editable) and to the Roles screen's protected list; (3) documented for the user, NOT changed:
seeded roles are code-managed — the grant loop is add-only and RoleRevocations deletes, so an
admin's removal from a seeded role returns on the next restart and a re-grant of a revoked one
is deleted; tenant-specific shapes belong in custom roles.

**Settings split (same day, user asked "should Safety setups be separate from HR setups?" — yes):**
`admin.she` seeded (Administration Modules; SHE Manager + SuperAdmin/TenantAdmin/Admin — NOT
Safety Officer, NOT HR); the SHE settings tree moved from `/administration/hr/safety` to
`/administration/safety` (git mv, 9 pages, every link and runbook path repointed), gated
`admin.she` in the sidebar node and the administration layout; the API's configuration writes
(SheReferenceDataController POST/PUT ×12, SheInspectionChecklistController POST/PUT ×4, PPE
types/requirements POST/PUT) moved from `HR.She.Write` to `HR.She.Admin` so an officer uses the
catalogue but does not edit it. Waste types stay at Write (managed from the desk screen, not the
settings tree). Harness: `CONFIG_WRITES` list + `w3.shemanager`/W3SHM fixture — officer and HR
refused, manager reaches; admin.she grant assertions.
**Verified 2026-09-03:** full ladder **964/964** on the rebuilt API (slice 12 = 178; the other ten unchanged); `admin.she` rows checked in the database — granted to SHE Manager, SuperAdmin, TenantAdmin only.
**All Settings page + personas (same day):** the SHE settings landed in "General Administration" on
the All Settings page because `AllSettingsPage.buildSettingsSections` only treats a fixed list of
Administration children as module cards — added `Safety (SHE)` (ordered after Human Resources;
test pins it and asserts no SHE link falls into General Administration). Demo cast re-cut for
the role pair: `she.officer` = Safety Officer on the Environmental Officer post (fallback HSE
Assistant — `Persona` now takes preferred titles, the workforce leaves every 6th post vacant);
new `she.manager` = SHE Manager on the HSE Supervisor post (Josephine Appiah, TDC/00071). The
persona seeder re-binds an existing user whose post changed. Runbooks 0/4 + cheat sheet + UAT doc
updated; she.officer's name/number is read off the seed log after the rebuild.
**Desk doors for reports (same day):** the desk register buttons for incidents, hazards and
stop-work pointed at the self-service forms, which file as the logged-in user, so a walk-in or
phoned-in report could only be recorded AS the officer — although the API had honoured
`reportedById`/`raisedById` for SHE Write holders since W3 slice 12. Extracted the three forms
into `components/hr/safety/report/{Incident,Hazard,StopWork}*Form.tsx` with `mode: self | desk`;
the /me pages are wrappers; new desk doors `/hr/safety/{incidents,hazards,stop-work}/new` name
the reporter (incident: EmployeePickerField; stop-work: EmployeePicker) and land on the new
record. Hazards carry NO reporter column (recorded residual) — the desk door says so. The
environmental desk register already had its own form with a reporter picker. Slice 12 proves:
officer names a colleague → record shows the colleague; employee naming a colleague → forced
back onto themselves (incident + stop-work).
**Hazard reporter (2026-09-04, user asked for the column):** `SheHazard.ReportedById` (+ `ReportedBy`
nav, `ReportedDate`), nullable — every earlier hazard has no reporter. DTOs/mapping/includes
carry it; `SheHazardController.Create` now stamps the token's employee and honours a named
reporter only for SHE Write (same arm as incidents/stop-work); the desk door gets a required
"Reported by" picker; the detail page shows it. Migration: user scaffolds
`AddSheHazardReporter`, I guard it and list it in FastBuildMigrationMetadata. Slice 12 gains the
hazard on-behalf + forced-self lines.
