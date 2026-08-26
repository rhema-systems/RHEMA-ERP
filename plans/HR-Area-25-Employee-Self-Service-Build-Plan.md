# HR Area 25 — Employee Self-Service Portal: Build Plan

**Opened 2026-08-25.** The last consolidation area of the HR module: one personal portal for
every employee, over capabilities that 24 closed areas have already built and scattered.
This area is explicitly a **flagship**: the stated goal is that self-service is a standout
feature of the whole ERP, not a nav section. Design quality is in scope, not incidental.

**Tier C context:** area 25 (this), then 26 (public careers + candidate portal, subject to
cross-module #11b), then 27 (consultant client portal). Area 25 has **no blockers**.

---

## 1. How to use this document

Read §2 for where we are, §3 for the measured ground truth, §5 for the decisions (four are
already taken by the user, 2026-08-25), §7 for the slice plan. §8 is the running log — one
entry per slice as it closes, in the area-16 style. The standing rules apply throughout:
user runs builds (`user-runs-builds`), I stage and the user commits (`stage-user-commits`),
harnesses run in Staging with the JWT key (`hr-harness-run-environment`), kill the running
API before asking for a rebuild (`stop-backend-before-user-builds`).

## 2. Status at a glance

| Slice | Title | Status |
|---|---|---|
| 0 | Survey, fixtures, and the link census | **COMPLETE 2026-08-25** — 111 assertions ×2 green; census in `HR-Area-25-Slice0-Census.md` |
| 1 | Identity & access: employee-number login, the AD link gap, the portal gate | **COMPLETE 2026-08-25** — 33 assertions ×2 + 111 no-regression |
| 2 | The shell: `/me` layout, top-nav, landing, routing, the switcher | **COMPLETE 2026-08-25** — tsc/lint/route-resolution clean + 144 ladder |
| 3 | The dashboard: one personal aggregate | **COMPLETE 2026-08-25** — 37+35 assertions + 144 ladder |
| 4 | Move-in: leave + attendance | **COMPLETE 2026-08-25** — 68 assertions ×2 + 179 ladder; 2 backend fixes; 2 TDC data gaps recorded |
| 5 | Move-in: performance (appraisals, goals, dev plan, peer evals, check-ins, journal) | **COMPLETE 2026-08-25** — 47 assertions ×2 + 247 ladder; 12 pages re-homed + my-goals built; 3 backend fixes; 16 deep-links re-pointed |
| 6 | Move-in: training & learning (+ the owed bond self-accept) | **COMPLETE 2026-08-25** — 85 assertions ×2 + 294 ladder; 4 pages moved + 7 built; learner step-completion dead path fixed |
| 7 | Move-in: movements, career path, orientation, probation/confirmation, travel | **COMPLETE 2026-08-26** — 72 assertions ×2 + 379 ladder; 7 pages moved + 7 built (incl. oaths); the dead EmployeeAcceptancePending state fixed |
| 8 | Move-in: medical + safety | **COMPLETE 2026-08-26** — 75 assertions ×2 + 451 ladder; 4 new backend self-arms; appointment .Include fix; 6 screens moved + 7 built |
| 9 | Move-in: assets (+ owed acknowledge/respond), awards, discipline, grievances | **COMPLETE 2026-08-26** — 80 assertions ×2 + 526 ladder; 4 fixes (ack repeat, withdraw dead route, committee-result leak, notice text); 10 screens moved + 1 built |
| 10 | My payslips: the read-only payroll adapter | **COMPLETE 2026-08-26** — 35 assertions ×3 + 606 ladder; latest-by-PERIOD; payroll FYI #12 + defect #13 (profile create never worked) recorded |
| 11 | Approvals & tasks inbox, notifications | **COMPLETE 2026-08-26** — 98 assertions ×2 + 641 ladder; 3 platform defects found (#14/#15/#17), the notification-forgery hole closed |
| 12 | New capabilities: change requests, HR letters, announcements & acknowledgements | not started |
| 13 | Directory, my team, recruitment (job board, my applications, my panel) | not started |
| 14 | Closing audit: content audit, the two greps, route resolution, polish pass | not started |

## 3. Ground truth — measured 2026-08-25 (survey re-verifies in slice 0)

### 3.1 The spec
`D:\ERP Demo\HRApi\ErpSystem.BlazorServer\Shared\PortalTopNav.razor` is the authoritative
self-service nav (with `EmployeePortalLayout.razor` as the shell spec). Extracted: **40
destinations** across leave (my-requests, my-encashments, planner), performance
(my-appraisals, my-goals, my-development-plan, peer-evaluations, checkins, journal),
training (my-training, my-learning, my-waitlist, requests/create), movements (movements,
dashboard, notifications, career-path, secondments, acting-appointments), medical
(coverage, claims, appointments, health), safety (me, my-health, my-risk-assessments,
report), me (profile, assets, awards, orientation, attendance/punch), recruitment
(job-board, my-applications, interviews/my-panel), staff-travel/my-requests, and
workflow (my-approvals, my-tasks).

### 3.2 What already exists in RHEMA
- **Backend:** `EmployeePortalController` (`api/employee-portal`, 25 actions: dashboard,
  movements + respond, career-path, acting, secondments, notifications, assets +
  acknowledge + terms-document, asset-requisitions full CRUD + submit/recall,
  asset-surcharges + respond). `MedicalSelfServiceController`. Plus **55
  `me|my|mine|self`-shaped routes across ~30 HR controllers** (top: PerformanceAppraisals 6,
  AppraisalNotifications 4, StaffMovements 3, OrientationNotifications 3; singles across
  training, SHE, discipline, grievances, probation, oaths, mentoring, competency, travel,
  job applications, peer evals, journal, check-ins…). Every one of these was built with a
  **self-or-permission arm in W3** — the portal rides the *self* arm and needs **zero HR.\*
  grants**.
- **Frontend:** ~16 self-service screens scattered under `/hr/*`: `assets/me`, `awards/me`,
  `competencies/me`, `discipline/mine`, `grievances/mine`, `medical/my-claims`,
  `movements/mine`, `orientation/mine`, `recruitment/my-panel`, `safety/my-ppe`,
  `training/my-learning`, `training/my-training`, `travel/mine`, plus leave's my-* pages
  inside `/hr/leave`. A workflow inbox exists at `app/workflow/inbox`.
- **Shell precedent:** `app/external-portal/` — own `layout.tsx`, own route tree,
  role-based landing via `lib/auth-routing.ts` (`isExternalPortalUser` → `/external-portal`).
  We copy the shape, not the code.

### 3.3 Authentication — mostly already built
- `AuthController.Login` **already prefers LDAP/AD** when `tenant.LdapEnabled`
  (`LdapAuthenticationService`, Novell LDAP, per-tenant server/port/BaseDN, tested
  connection + user search endpoints exist). On first successful AD login it
  **auto-provisions** a local user: `AuthenticationProvider=LDAP`, **no roles, no employee
  link** — i.e. today an AD-provisioned employee lands with a dead portal. That gap is
  slice 1's core.
- `UserEmployeeLinkController` exists (`link-user-to-employee`, `unlink`, list). The link
  is the load-bearing identity piece: every `/me` endpoint resolves the actor through it.
  **Coverage is unmeasured** — slice 0 measures it (memory: the client `User` object
  carries no employee link, and area 15b found line managers unlinked).

### 3.4 Payslips
`PayrollPayslipSnapshot` (`PayrollEntities.cs:1930`): keyed by `EmployeeId`, carries
`EmployeeNumber`, `PayslipNumber`, `GeneratedAt`, gross/net/tax/contribution and a
self-contained `SnapshotJson`. Snapshots exist only where a run generated them
(`POST runs/{id}/payslips/snapshots`). A read-only `/me` adapter over this table touches
zero payroll code — the salary-projection-bridge pattern (`hr-salary-structure-bridge`).

### 3.5 Owed residuals that land here (recorded in prior areas)
1. **Bond self-accept screen** (training W3 slice 8 residual).
2. **Asset acknowledge/respond employee surface** (area 16 — by-design absent from desk).
3. **Training requests page mine-fallback** (W3 slice 8 residual — desk read owed a
   register tab; the employee-side page belongs here).
4. Area 16 D4: portal consolidation of the 13 asset routes on `EmployeePortalController`.

## 4. Requirements extraction

The Blazor `PortalTopNav` (§3.1) is the primary requirement set — parity with those 40
destinations, minus what deliberately stays elsewhere (§6). On top of parity, the user has
asked for **standard enterprise additions** to make the portal complete and standout; the
proposed set is §5 D7. FRD anchors: FR-HR-046/111/152 touched self-service surfaces in
their areas; no un-built Mandatory FRD requirement is known to land uniquely here —
slice 0 re-checks the FRD index for `portal|self-service` mentions.

## 5. Decisions

### D1 — Login identifier. ✅ DECIDED 2026-08-25 (user).
**Existing login + employee number.** Keep username/email + password with the existing
LDAP/AD preference (works when TDC enables `LdapEnabled`). Add **employee number** as an
accepted login identifier: a resolver in front of the existing flow — if the submitted
identifier matches no username/email but matches `Employee.EmployeeNumber` for a **linked**
user, authenticate that user by the normal path (local password or LDAP per their
`AuthenticationProvider`). No new auth scheme, no separate portal token. Ambiguity rule:
username/email match wins over employee-number match; an employee number matching an
UNlinked employee resolves to nothing (no user to authenticate).

### D2 — Shell. ✅ DECIDED 2026-08-25 (user).
**Distinct shell, same app.** A `/me` route tree with its own `layout.tsx` and top-nav
(Blazor `EmployeePortalLayout`/`PortalTopNav` as spec; `external-portal` as the code
shape). Dashboard-first, personal, warmer look — same design tokens, different chrome.
Same JWT, same session. **Landing:** a user whose only functional role is Employee lands
at `/me` after login (`auth-routing.ts` branch, the `isExternalPortalUser` pattern); desk
users land where they do today and get a persistent one-click switcher both ways
("My Self-Service" in the ERP chrome; "Back to ERP" in the portal chrome, shown only to
users with desk access). The hard visual transition at the switch is deliberate.

### D3 — The scattered my-* screens. ✅ DECIDED 2026-08-25 (user).
**Move, no redirects.** Each move-in slice re-homes its screens into the portal shell and
**deletes the old `/hr/*` routes in the same slice**, updating every sidebar/dashboard/
cross-screen link atomically. The closing route-resolution check (§7 slice 14) proves no
dangling references. Desk registers stay where they are — only the employee-as-subject
surfaces move.

### D4 — My payslips. ✅ DECIDED 2026-08-25 (user).
**Read-only adapter now.** An HR-owned `/api/employee-portal/payslips` (+ `/{id}`) reading
`PayrollPayslipSnapshot` for the token's employee only — list (period, number, gross, net)
+ detail rendered from `SnapshotJson` + print via the body-class pattern. **No payroll
code touched, no write, no recompute** (`payroll-ownership-boundary`). Flag to the payroll
owner as a consumed surface (add to `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`
as an FYI-not-defect, or the HR-Finance backlog's notes — slice 10 decides placement).
Empty-state copy explains payslips appear when payroll publishes them.

### D5 — The AD auto-provision gap: auto-link policy. *Recommendation, taken unless contradicted.*
On successful LDAP login (and provisioning), attempt an **exact-match auto-link**:
AD `mail` → `Employee.Email` (active, unlinked employees only), else `sAMAccountName` →
`EmployeeNumber`. Exactly one match → link + assign the `Employee` role. Zero or multiple
matches → provision as today and surface the user in an **HR "unlinked users" queue**
(slice 1 screen, extending `UserEmployeeLinkController`'s list with match suggestions and
bulk link). Never auto-link on fuzzy matches — a wrong link is an identity breach (one
person seeing another's payslips), the worst defect this area could ship.

### D6 — Personal-data changes go through a change request, not a direct write. *Recommendation, taken unless contradicted.*
Employees **view** their full profile; **low-risk fields** (phone, emergency contacts —
exact list settled in slice 12 against what the profile edit surface already allows) may
edit directly; **identity/payment-bearing fields** (bank details, name, DOB, SSNIT/TIN,
address) go through a `ProfileChangeRequest` — field, old→new value, evidence attachment
via the controlled upload gate, HR approves/rejects, approval applies the change and the
audit trail keeps who asked and who approved. Workflow-engine routing only if a
multi-step definition is wanted; first cut is an HR desk queue (the grievance-first-cut
precedent).

### D7 — The "standout" additions: what is in scope. *Recommendation, taken unless contradicted.*
**In (slices 11–13):** approvals/tasks inbox as a first-class portal page; unified
notifications; personal-data change requests (D6); **HR letter requests** (employment
confirmation / introduction letter: request + HR fulfils with an uploaded letter through
the document gate — no auto-generation in the first cut); **announcements** (HR publishes,
employees see on dashboard + list) with **policy acknowledgements** (publish a policy doc,
track who has acknowledged — feeds the dashboard); light **org directory** (name, photo,
position, unit, work contact — the lean-DTO read that W3 left open; no PII); **my team**
(managers see direct reports' lean cards + leave-today; links into desk screens they
already have permission for). **Out (recorded, not built):** payslip PDF generation
beyond print-view, kiosk/mobile apps, chatbots, benefits enrollment self-service beyond
what areas built, resignation self-filing (area 9b's separation initiation stays desk-side
unless TDC asks — note in `HR-OPEN-QUESTIONS-FOR-TDC.md`).

### D8 — The portal gate. *Recommendation, taken unless contradicted.*
No new permission family. Route gate = **authenticated + internal + linked employee**.
`/me/layout.tsx` resolves the link once (existing `me`/profile endpoint); unlinked users
get a friendly "your account isn't linked to an employee record yet — contact HR"
page, not twenty broken screens. Backend needs no new gate: `InternalOnly` (W3) plus each
endpoint's token-actor resolution already refuse outsiders and the unlinked respectively.
`hr.access` stays the desk gate; the portal deliberately does not require it — but since
Employee-role users hold it today, nothing changes for them either way.

### D9 — Area boundaries.
Attendance **punch** moves in only if the current punch surface is employee-usable
(slice 0 probes it; geofence/device flows stay desk-side). Consultant timesheets stay in
`/hr/consulting` (they are consultant-facing, area 27's neighbour). The **journal**
(private entries) moves in — it is the single most personal surface in the module.
Mentoring "my mentorships" moves in; program administration stays desk. Oaths of secrecy:
my-oath view + sign moves in if the self route proves usable (slice 0).

## 6. Scope

**In:** everything in §5 D2–D8; parity with the 40-link spec except items §5 D9 excludes;
the four owed residuals (§3.5); deletion of the moved `/hr/*` self screens.
**Out:** area 26/27 (other portals); any payroll write; GL/money events (none exist here —
payslip display is a read; letters and change requests carry no amounts); the org-authority
model (my-team uses `ManagerId` as-is and shows honestly-empty states — the 175/1,210
reality is TDC's data problem, recorded); separation self-filing (D7).

## 7. Slice plan

Every slice: harness `dev-harness/hr-portal/run-sliceN-*.mjs` (Staging + JWT key, clamd
stub where uploads are touched), run twice from different DB states, **plus the cumulative
no-regression ladder of this area's earlier runners**; `npx tsc --noEmit` + lint after
frontend work; migrations follow `migration-ownership-and-chain` (user scaffolds, I edit +
list in `FastBuildMigrationMetadata`, user updates). Two-actor rule throughout: probe as
the LINKED employee fixture, not as HR (HR bypasses its own guards). UI-payload probes
before any TypeScript for every form (enums as strings, datetime-local, null-vs-"" — the
area-12 lessons).

- **Slice 0 — survey, fixtures, link census.** Diagnostic only. (a) Census the 40 spec
  links → RHEMA endpoint each maps to → existing screen each maps to → verdict
  (exists / exists-on-desk / missing). (b) Probe **every** self endpoint as the linked
  employee — status AND body shape (an empty read gives no shape; seed minimal rows where
  needed). (c) Measure `UserEmployeeLink` coverage: users total / Employee-role users /
  linked / AD-provisioned-unlinked; measure `Employee.Email` and `EmployeeNumber`
  uniqueness (they gate D1+D5's exact-match rules — **measure before designing on them**,
  the ExpectedHeadcount lesson). (d) Fixtures: `portal.employee` (linked),
  `portal.unlinked` (Employee role, no link), `portal.manager` (linked, has direct
  reports), reuse `w3.hruser`. (e) Probe punch + oaths for D9. (f) FRD grep for
  portal/self-service mentions.
- **Slice 1 — identity & access.** D1 employee-number resolver in `AuthController` (+ rate
  limiting parity with login); D5 auto-link on LDAP provision + the HR unlinked-users
  queue screen (under `/administration/hr/`); D8 gate + unlinked landing page. Harness:
  login by employee number (linked, unlinked, ambiguous, wrong password), auto-link
  matrix (exact email / exact number / zero / multiple), queue list + link + suggestion
  correctness, unlinked user gets the friendly page not data.
- **Slice 2 — the shell.** `/me` tree: `layout.tsx`, top-nav (grouped like PortalTopNav),
  profile header, the switcher both ways, `auth-routing.ts` landing branch +
  login/dashboard redirect parity, sidebar entry for desk users. Placeholder landing.
  Design pass is part of this slice, not deferred.
- **Slice 3 — the dashboard.** Extend `GET api/employee-portal/dashboard` into the
  personal aggregate: leave balance + next holiday, pending-my-action counts (movements
  to respond, surcharges, confirmations, acknowledgements), assets held, next training,
  latest payslip stub (wired in slice 10), announcements stub (wired in slice 12),
  expiring documents (IDs/certs). Each tile deep-links. Harness asserts the numbers
  against independently-queried ground truth (the area-7 wrong-numbers lesson).
- **Slices 4–9 — move-ins** (grouping per §2 table). Per slice: probe the endpoints as
  employee, re-home the screens into the shell (rebuild to portal layout — not a file
  move), fill the spec links that have no screen yet, delete old routes + update links
  atomically (D3), close that domain's owed residual where it lands (bond self-accept →
  6; asset acknowledge/respond + terms-document → 9; training mine-fallback → 6).
  Slice 8 keeps the SHE↔Medical boundary (occ-health = HR.Medical side).
- **Slice 10 — payslips.** D4 adapter (self-only read, snapshot render, print), empty
  states, dashboard tile wiring, the payroll-owner FYI recorded. Harness: employee sees
  own list/detail only (cross-employee probe 404s), snapshot JSON renders the line items,
  desk user without link gets none.
- **Slice 11 — approvals inbox + notifications.** Portal my-approvals/my-tasks over the
  workflow engine's existing my-surfaces (embed/adapt `workflow/inbox`), unified
  notification list consolidating the per-area notification reads (appraisal,
  orientation, movements…), dashboard count wiring. No new approval UI is invented
  (W1 rule: never build a bespoke HR approval UI).
- **Slice 12 — change requests, letters, announcements.** D6 `ProfileChangeRequest`
  (entity + migration + HR queue + portal form + apply-on-approve + audit), HR letter
  requests (request → HR fulfils via document gate → employee downloads), announcements +
  policy acknowledgements (publish desk-side under `/administration/hr/` or `/hr/`,
  consume portal-side; acknowledgement tracking + dashboard nudge). The heavy backend
  slice — may split 12a/12b at build time.
- **Slice 13 — directory, my team, recruitment.** Lean org directory (search + unit
  browse, lean DTO only); my-team for managers (direct reports via `ManagerId`,
  honestly-empty state); internal job board + my-applications + my-panel move-ins
  (the internal job board's service-side visibility model stays as W3 left it).
- **Slice 14 — closing audit.** The content audit run twice; **the two greps** (portal
  service methods ↔ screens; non-GET portal routes ↔ services); route resolution over
  every portal page + every deleted route confirmed gone from all link sources; tsc +
  lint; a final design/consistency polish pass through every portal screen (empty
  states, loading, mobile width); memory + docs updates (this file's log, the area
  survey memory, TDC open questions if new ones arose).

## 8. Log

*(one entry per slice as it closes)*

### Slice 0 — survey, fixtures, link census. CLOSED 2026-08-25.

Diagnostic only; no product code touched. Full detail in **`HR-Area-25-Slice0-Census.md`**;
harness `dev-harness/hr-portal/run-slice0.mjs` (111 assertions, green twice from different
DB states) + `census-slice0.sql`.

- **The link census came back far cleaner than feared**: 7,329/7,349 users linked (99.7%),
  exactly **1** unlinked Employee-role user, 0 LDAP users, 0 duplicate/blank emails or
  employee numbers (live or incl. soft-deleted), 0 employee-number↔username collisions,
  0 integrity defects. **D1 and D5 are safe as designed; no ambiguity case exists live.**
  ManagerId: 213/7,438 (2.9%) — my-team stays honestly-empty.
- **Spec census: 19 SCREEN-EXISTS / 14 ENDPOINT-ONLY / 7 MISSING** of the 40. The MISSING
  seven (need backend work, not just screens): medical coverage self-read (#27), medical
  appointments self-read (#29), safety self hub (#33), my-risk-assessments self read (#34),
  occ-health my-surveillance (#35), the self employee-profile (#39), and my-tasks has an
  endpoint but no screen (#38 counted under E). **Leave is the biggest frontend hole** —
  zero self screens; its endpoints are id-bearing self-arm routes (no `/mine`).
- **Every existing self endpoint answers live** (~80 probed as a plain linked employee) and
  the **self-arm is two-sided** (own id 200 / other's id 403) on all six id-bearing routes.
- **D9 verdicts: punch MOVES IN** (token-actor, CheckIn+CheckOut proven live, geofence
  optional) and **oaths MOVE IN** (`mine` + `affirm` work; affirm is create-own,
  server-stamped, IP-recorded, deliberately id-less).
- **The D8 gate has its evidence**: an unlinked user gets a clean 403/400 wall (no 500s, no
  leaks) and `Auth/me` still answers — the friendly landing page is buildable. **But
  `Auth/me` carries no employeeId** (measured), so slice 1 must expose the link for the
  route gate; the harness asserts the gap so the assertion flips when slice 1 closes it.
- Fixture set minted per run: `hr` / `manager` / `employee` (reports to manager) /
  `unlinked` — prefix `a25v_`. The manager fixture proved `manager/me/direct-reports`.
- FRD sweep: no un-built Mandatory requirement lands uniquely here; parity target stays the
  Blazor PortalTopNav 40. Portal-bearing IDs AST-5/6/8, AWD-03/04/11 already have their
  backend surfaces.

### Slice 1 — identity & access. CLOSED 2026-08-25.

`run-slice1.mjs` 33 assertions ×2 green + the 111 slice-0 ladder (with its Auth/me
assertion deliberately flipped: the gap it recorded is now closed). No migration — no
schema change.

- **The matching rules live in ONE service** (`EmployeeLinkResolutionService`): D1's login
  resolver, D5's auto-link and the queue's suggestions all consume it. That seam is also
  the testability story — the auto-link fires only inside LDAP provisioning (unreachable
  without a live directory, and TDC has 0 LDAP users today), so the harness proves the
  matrix through the queue and what the queue shows IS what auto-link would do. Exact-match
  only; ambiguity always resolves to "no link".
- **D1**: an identifier matching no username/email resolves as an employee number through
  the link; the resolved user authenticates by their own provider (the LDAP bind uses the
  resolved username, not the number); inside the existing `AuthPolicy` rate limit; refusals
  indistinguishable from any failed login. Precedence proven live with a manufactured
  username↔number collision.
- **D5**: on LDAP auto-provision, exact-one match (AD mail → EmailAddress, else
  sAMAccountName → EmployeeNumber, unlinked employees only) links the account and grants
  the Employee role; failure never blocks the login. **Finding:** the employee API refuses
  duplicate emails (400), so email ambiguity is structurally impossible via the API — the
  flagged-ambiguous branch stays as defense-in-depth for imported data.
- **D8 surface**: `UserInfo.EmployeeId` at all four construction sites (login, refresh,
  me, select-tenant); linked → id, unlinked → null. Slice 2's route gate reads this.
- **The queue**: `GET api/UserEmployeeLink/unlinked-users` (+ `POST bulk-link`, per-pair
  guards) and the screen at `administration/user-employee-links/unlinked` — suggestion
  chips, "Link all exact matches", manual paged search; cross-linked with the existing
  links screen. Admin-gated like the rest of the controller.
- Noted in passing: the old `components/admin/UserEmployeeLinks.tsx` fetches
  `/employees?pageSize=1000`, a route that doesn't exist (`api/hr/Employees` has no GET
  list) — its employee picker has likely been empty forever. Not this slice's surface;
  worth folding into a later polish pass.

### Slice 2 — the shell. CLOSED 2026-08-25.

Frontend-only (no backend change; the 144-assertion ladder re-run green as no-regression).
Verification: tsc clean in the touched files, lint clean, every emitted href resolves to a
`page.tsx`. **Layout decision re-confirmed with the user: top-nav, no sidebar, not both**
— ESS users are casual users (Workday/SuccessFactors/BambooHR pattern), the dashboard
tiles are the primary navigation, mobile (punch!) collapses a top-nav cleanly, and the
sidebar↔topnav contrast IS the "which world am I in?" signal for switcher users.

- `app/me/layout.tsx` — the D8 gate: AuthGuard + external users routed home + the link
  resolved once from the fresh `Auth/me` (slice 1's `employeeId`); unlinked users get one
  friendly explanation page (with Back-to-ERP for desk users) instead of broken screens.
- `components/me/portal-top-nav.tsx` — the six spec groups + actions (approvals,
  notifications, theme, avatar menu with Back-to-ERP for desk users), mobile sheet;
  signature accent band so the portal reads as its own world on the same tokens.
  **D3 discipline encoded in the file:** groups list ONLY genuinely-self-service
  destinations; the `/hr/*` hrefs are re-pointed by each move-in slice in the same commit;
  no dead "coming soon" links ever.
- `app/me/page.tsx` — placeholder landing: greeting + three tile sections ("Things to act
  on" / "My work life" / "Benefits, kit & safety") over the 15 live destinations; slice 3
  replaces the copy with the real personal aggregate.
- `lib/auth-routing.ts` — `isEmployeeOnlyUser` (all roles == Employee) → lands at `/me`
  via `getAuthenticatedHomePath`, which all four post-login call sites already route
  through; `hasDeskAccess` gates the Back-to-ERP switcher. Desk side: a "My Self-Service"
  sidebar entry right under Dashboard.
- `types/index.ts` — `User.employeeId` so the slice-1 payload reaches the client typed.

### Slice 3 — the dashboard. CLOSED 2026-08-25.

`run-slice3.mjs` 37 then 35 assertions green (the two runs exercise both next-holiday
branches) + the 144 ladder. No migration.

- **"Extend the dashboard" landed as a SIBLING read**, `GET api/employee-portal/home`, not
  more fields on `EmployeePortalDashboardDto` — that payload is the movements dashboard
  consumed by `hr/movements/mine`, and its own docstring already states the rule: each
  screen pays only for what it shows. Home aggregates: waiting-on-me counts (movements /
  surcharges / acknowledgements), leave balances + next holiday (12-month window), assets
  held + open requisitions, training figures, documents expiring ≤90 days, and **typed
  stubs** for latest payslip (slice 10) and announcements (slice 12) so the frontend
  contract never changes shape twice.
- **Every figure is computed from the same service call the detail screen makes** and the
  harness asserts equality against the independent reads; the two seeded deltas prove the
  numbers move (a 30-day certificate surfaces with the right days-left; a requisition
  draft moves the open count by exactly one).
- **The no-vacuous-green rule got teeth**: the DEFAULT tenant had NO holiday calendar at
  all (finding — TDC's holiday data is unentered; recorded for seeding/ops, my-leave UX
  will show "none coming" honestly), so the run seeds a calendar + holiday when needed and
  asserts the real first-future-holiday selection.
- Frontend: `me-portal.service.ts` (typed getHome, no employee id by design) and the
  landing rebuilt — "Waiting on you" amber chips (only non-zero counts render; calm
  all-caught-up line otherwise), four at-a-glance stat cards (leave / next holiday /
  assets / learning) each deep-linking, expiring-documents list, quick-link tiles kept; if
  the aggregate read fails the tiles still render (degrade, don't die).

### Slice 4 — move-in: leave + attendance. CLOSED 2026-08-25.

`run-slice4.mjs` 68 assertions ×2 green + the 179 ladder (111/33/35). No migration. Spec
rows #2–#5 all carried verdict **E** — self-armed endpoints existed, zero self screens —
so this slice FILLED the census's biggest frontend hole rather than moving anything:
every `/hr/leave/*` and attendance screen is a desk register and stays (D3's "desk
registers stay" clause; nothing deleted).

- **The UI-payload probe ran first** (`probe-slice4.mjs`) and its findings shaped the
  slice: **(1) the DEFAULT tenant has ZERO leave types** — TDC's questionnaire-documented
  28d/15d entitlements were never seeded (the slice-3 holiday gap's sibling; harness seeds
  its own, ops/seeding owes the real ones); **(2) no active workflow definitions** for
  LeaveRequest/LeavePlan/LeaveEncashment — the harness publishes the 3-step bookend shape
  and ALSO asserts the no-definition reality on purpose; **(3) `RequestEncashmentAsync`
  was non-atomic** — a throwing workflow submit left an orphaned Submitted row behind a
  400, and every retry then refused with "already been encashed" (measured live; FIXED:
  create+submit now share one `ExecuteInTransactionAsync`, and the regression assertion
  counts rows across a forced refusal); **(4) two dormant fields on the punch path** —
  every punched day said `DayOfWeek=Sunday` and `ActualWorkHours` was never computed
  (FIXED in `ProcessLogInternalAsync`: real weekday stamped, old rows self-heal on touch,
  hours derived from a same-day punch pair; overnight pairs stay null honestly).
- **Screens (6, all portal-shaped):** `/me/leave` (balances + year-filtered requests),
  `/me/leave/new` + `[id]` + `[id]/edit` (create/detail/attachments/submit/cancel),
  `/me/leave/planner` (submit → manager suggestion → accept-or-counter — the
  respond-suggestion arm is the plan OWNER's act), `/me/leave/encashments` (anchored to an
  owned cash-convertible request; the amount is server-derived and the screen says so),
  `/me/attendance` (punch card + today + month table). Create-then-submit failures
  surface as "saved, but not submitted" — never as a lie in either direction.
- **The reliever picker is the roster, not a search.** The desk form's `EmployeePicker`
  rides `POST hr/Employees/paged` (EmployeeReadPolicy) which a plain employee cannot
  call — the portal form offers `employee-relievers/mine` + the server's auto-fill
  (roster by priority, then manager), and the copy explains the empty case. D8's
  no-new-grants rule held without exceptions.
- **Harness shape worth copying:** the no-definition leg runs BEFORE definitions are
  published (refusal + nothing-moves + the atomicity regression), then the engine leg
  proves the pair from the workflow memory — the employee cannot approve their own
  request/encashment, HR can — plus min-notice (draft exempt), overlap (a DRAFT does not
  claim the calendar — only Approved/Pending conflict; asserted both ways), cross-actor
  403s on all three writes, auto-approve for a `RequiresApproval=false` type (the
  no-definitions tenant reality), cancel restoring the balance, and home-aggregate
  equality re-checked after all the movement.
- **Convention every attendance screen must encode:** the logs read's `from`/`to` are
  DateTimes and `to` is midnight-EXCLUSIVE — same-day from/to returns nothing; send
  tomorrow. Asserted in the harness so it cannot regress silently.

### Slice 5 — move-in: performance. CLOSED 2026-08-25.

`run-slice5.mjs` 47 assertions ×2 green + the 247 ladder (111/33/35/68). No migration.
Unlike leave, this domain's screens mostly EXISTED and re-homed: spec rows #7/#9–#12 were
verdict-S, #8 (my goals) the one verdict-E hole built from nothing.

- **The move (D3, atomic):** 12 pages re-homed to `/me/performance/*` via `git mv` + adapt
  (hrefs, backHrefs, the portal's padding) — appraisals (list, [id], self-evaluation,
  appeal, appeal-status, appeal-outcome), peer-reviews (list, [id]), check-ins (list,
  [id]), journal, and the development-plan DETAIL. Deleted from the desk in the same
  change, with the sidebar, the performance hub, PIP/conversation "open the appraisal"
  deep-links (→ `team-appraisals/{id}`, the manager's view of the same record) and
  team-appraisals' check-ins button all re-pointed. **Built fresh:**
  `/me/performance/goals` (draft → submit → manager feedback → progress, the bespoke
  lifecycle deliberately OFF the workflow engine) and the portal development-plans list
  (mine/team). **Split:** the desk development-plans page is now the HR register only, and
  its rows deliberately open the PORTAL detail — one 700-line working surface, not one per
  world; the subject updating progress is its primary user.
- **16 backend deep-links re-pointed** across 6 services (appraisal/peer notifications,
  the HR cycle dashboard's nudges, cycle-open + deadline reminders, dev-plan activation and
  feedback): every URL whose RECIPIENT is the employee or a peer now lands in the portal;
  manager-directed ones stay desk. Recipient-by-recipient, read from each call site.
- **Three defects found by the probe, fixed and asserted by name:** (1) a journal EDIT
  wiped `EntryDate` to year 0001 — the update DTO's non-nullable DateTime was copied
  unguarded and no client ever sends it (`AppraisalMappingExtensions.UpdateEntity` now
  guards the default; the mine list orders by that date, so edited entries sank); (2) a
  goal PROGRESS ENTRY with no status stored 0 — outside the enum, which starts at 1 — and
  serialized as a bare number (`AddProgressEntryAsync` now derives Completed/InProgress
  from the percent); (3) the same shape on development-objective progress
  (`UpdateObjectiveProgressAsync`). The enum-starts-at-1 + optional-DTO-field combination
  is a repeatable trap: an "enum as string" convention silently emits numbers for any
  value without a name.
- **D8 held without new grants:** the two desk pickers that would have 403'd a plain
  actor (check-ins scheduling, dev-plan create) now pick from
  `employees/manager/me/direct-reports`; a check-in you run is with your own report anyway.
- Harness legs worth keeping: nomination-BEFORE-self-eval ordering (the cycle's peer
  minimum gates the submit); the peer's whole leg as the nominated manager (approve →
  `PeerEvaluations/me` → draft-with-evaluationId-in-body → submit); conductor-from-token
  proven on BOTH check-in lists; the dev-plan Draft-when-asked/Active-when-own contract;
  journal privacy proven two-sided against the manager's team view; and the unlinked
  wall, noting the module's own convention that /me READS answer honestly-empty for an
  unlinked account while writes refuse.

### Slice 6 — move-in: training & learning. CLOSED 2026-08-25.

`run-slice6.mjs` 85 assertions ×2 green + the 294 ladder (111/33/35/68/47). No migration.
Spec rows #13–#15 were verdict-S and re-homed; #16 (my waitlist) the verdict-E hole built
from nothing; both training residuals owed to this slice closed (bond self-accept, requests
mine-fallback). D9's mentoring move-in landed here too (mentoring lives under training).

- **The move (D3, atomic):** my-learning's 3 pages → `/me/learning/*` and the mentoring
  pair detail → `/me/mentoring/[id]` via `git mv` + adapt; `/me/training` rebuilt from
  my-training with a certificates tab (issued `training-completions/certificates/mine` +
  external `employee-certificates/mine`) and a pending-bond banner; my-training deleted.
  **Built fresh:** portal requests (new + [id] — self-shaped, no employee picker; the
  rejection reason finally reaches whoever raised it), the nominee's nomination detail
  (withdraw + bond cross-link), `/me/training/waitlist` (offer accept/decline + leave
  queue), and `/me/training/bonds` + `[id]` (the owed self-accept: full terms on screen,
  the dialog restates duration + amount, server stamps the window). **Split:** the desk
  keeps requests (register + on-behalf create + approve/reject) untouched; my-learning's
  org-wide tabs became the new `/hr/training/enrollments` register and mentoring became
  register-only — both registers' rows open the PORTAL detail (the slice-5 dev-plans
  one-working-surface precedent). Sidebar/hub/nav/dashboard/admin deep-links re-pointed;
  the two moved desk sidebar items replaced by Enrollments + Mentoring (both
  HR.Training.Read — mentoring's register read was always desk-gated).
- **The big find, measured then fixed: learner step completion was a DEAD PATH.** The
  evidence check and the step page both read only `EmployeeLearningPathStep.NominationId` —
  which nothing ever writes before first completion (the completion payload is its only
  writer, and it is applied AFTER the evidence check). So attendance never counted for a
  learner, the step page never showed their nomination, and only the HR override worked.
  `LearningPathService` now resolves the learner's live nomination BY PROGRAMME (withdrawn/
  rejected never count) for both the evidence check and the step-detail read model; the
  first harness run failed exactly there and passes now. Area-7's run6 never caught it
  because its evidenced-completion leg ran as HR (who bypasses the gate) — the two-actor
  rule again.
- **Two more fixes:** the workflow inbox's TrainingNomination ActionUrl pointed at
  `/hr/training/nominations?nominationId=` — a route that never existed; now the desk
  detail `/hr/training/nominations/{id}` with EntityNumber/Name filled like its neighbours
  (the approver is the reader). And the frontend `TrainingBondStatus` union was missing
  `Cancelled`, which the server emits — tsc caught it only because the new pages compare
  against it (a type written before the enum grew; the fiction-that-type-checks shape).
- **The probe's accidental discovery, asserted by name:** withdrawing a nomination CANCELS
  its still-pending bond (`CancelForNominationAsync`), and a cancelled bond can never be
  accepted — the portal withdraw dialog says so before the employee commits.
- Harness legs worth keeping: the rejection-reason promise read back as the requester; the
  bond matrix (cross-accept 422 "own service bond", self-accept stamps NO on-behalf actor,
  re-accept refused, cancelled refused); the waitlist offer lifecycle two-sided (respond
  before offer 422, the manager cannot answer the employee's offer, acceptance alone does
  NOT enrol); evidence-gating end-to-end as the LEARNER (unevidenced 422 → HR attendance →
  completable → 50% → step 2 unlocks → history row); mentoring note privacy two-sided
  (MENTOR-PRIVATE withheld from mentee and vice versa, non-participant 403); and the
  unlinked wall's per-route conventions recorded as measured (403 / 400 / honest-empty).
- **Residual recorded, not built:** employee feedback filing stays API-only. The self-armed
  POST exists and is proven, but there is no `feedback/mine` read to show what was filed or
  gate a duplicate (`SubmitFeedbackAsync` dedupes nothing) — a portal feedback form needs
  that read first. Parked for the slice-14 polish pass or a later backend addition.

### Slice 7 — move-in: movements, career path, orientation, probation, travel, oaths. CLOSED 2026-08-26.

`run-slice7.mjs` 72 assertions ×2 green + the 379 ladder (111/33/35/68/47/85). No
migration. Spec rows #17–#19 re-homed; #20–#22 (career timeline, acting, secondments)
built from nothing on the consumer-less `employee-portal` routes; #23's computed
notification feed re-pointed and left for slice 11's unified page. D9's oaths move-in
landed here (probation's neighbour), plus the probation "About me" surface.

- **THE find, measured then fixed: `EmployeeAcceptancePending` was a DEAD STATE.** After
  area 8 moved movement approvals onto the workflow engine, the status adapter landed every
  approved movement on `Approved` — ignoring `RequiresEmployeeAcceptance` — so the portal
  respond endpoint 409'd on every movement ever approved, and the old mine-page's "Awaiting
  your response" card could never show (probe-slice7 proved it live: create → submit →
  approve → respond → 409). `StaffMovementWorkflowStatusAdapter` now lands approval on
  `EmployeeAcceptancePending` when acceptance is required and unanswered;
  `RecordEmployeeResponseAsync` returns the movement to `Approved` on EITHER answer (the
  entity's historical post-response status — the answer lives on the flags, and the
  implement gate already refuses an unaccepted movement). Asserted by name, both legs,
  plus equality across pending-response / dashboard count / the slice-3 home aggregate.
- **The move (D3, atomic):** `/hr/movements/mine` rebuilt as `/me/movements` (dashboard
  stats + respond + history + checklist tasks + the manager's waiting-for-my-approval strip,
  which deliberately keeps DESK links — approval actions live where the workflow is);
  orientation mine (2 pages) → `/me/orientation`; travel mine (4 pages) → `/me/travel`
  (with `TravelRequestForm`'s portal branch re-pointed). **Built fresh:** `/me/movements/[id]`
  (From→To card + the respond act), career-path timeline, acting, secondments,
  `/me/probation` (my reviews + acknowledge — the natural-justice read shows the subject
  the full assessment, not just comments) and `/me/oath` (affirm + history). **Splits:**
  the desk probation reviews page is reviewer-queue-only (sidebar: "Reviews to Conduct");
  the desk oaths page is register-only; orientation desk registers' "view progress" rows
  open the portal detail.
- **The dangling-URL family, re-pointed by recipient:** portal movement notifications
  carried Blazor-era `/employee/movements/*` → `/me/movements/{id}` (asserted); travel
  notifications + the workflow display carried `/hr/travel/requests/{id}`,
  `/hr/travel/finance/advances/{id}` and `/hr/travel/compliance/documents/{id}` — routes
  that NEVER existed → `/hr/travel/{id}` (all these topics' recipients are the HR role,
  verified in the topic seeders), the advance via its request's Finance tab, and the
  documents reminder to `/hr/travel/dashboard` (least-wrong: employee-level travel
  documents have no desk register — recorded, not built). Movement reminders were audited
  and left alone: all recipients are the HR role, so their desk URLs are correct.
- **One more summary-vs-subject fix:** the portal acting read returned the register
  SUMMARY DTO, which deliberately omits the allowance and the covering-for name — exactly
  what the subject opens the page for. `GetDetailedByEmployeeIdAsync` (full DTO; the repo
  already includes the navs) now backs the portal route; caught by the harness because its
  fixture set an allowance where the probe's had not.
- Facts the harness records by name: probation duration comes from the STAFF CATEGORY
  (sending a different one is refused; omit it); the probation PERIOD read has no self arm
  by design (the subject's window is the reviews); oath re-affirmation is allowed and kept
  as history (the portal page shows newest-as-standing); travel cancel requires
  `cancellationReason`; checklist completion is owner-or-HR ("assigned to someone else").
- The unlinked wall as measured: portal + movements + probation + oaths 403, orientation +
  travel me 400 — no 500s, the D8 layout gate fronts them all.

### Slice 8 — move-in: medical + safety. CLOSED 2026-08-26.

`run-slice8.mjs` 75 assertions ×2 green + the 451 ladder (111/33/35/68/47/85/72). No
migration. The census's heaviest slice: 5 of its 7 M verdicts landed here — coverage (#27),
appointments (#29), the safety hub (#33), my-risk-assessments (#34) and surveillance (#35)
all needed NEW backend self-arms, built to the module's self-service law (`InternalOnly` +
token actor + explicit projection + foreign id = 404 lookup miss).

- **New self-arms (4 surfaces, zero new service methods for the lists):**
  `api/medical/me/insurance-policies` (list / `active` / `{id}` / `{id}/dependents` — the
  dependants only reachable THROUGH an owned policy, since the dependant DTO carries no
  employee id) and `api/medical/me/appointments` (list/detail, read-only: booking is the
  clinic's act) on `MedicalSelfServiceController`, whose remit widened from claims-only;
  `api/employee-health/me/surveillance` (+`/{id}`) on the occ-health self controller — it
  could NOT sit on `api/safety/occupational-health` because that class-level
  MedicalReadPolicy ANDs with anything per-action; and the two SHE one-liners
  `risk-assessments/for-acknowledgement/mine` and `incidents/mine` (the read the incident
  controller's own docstring had deferred to area 25 by name). Self projections are
  explicit: no `notes`/`cancellationReason` (HR commentary), no `IsFlaggedForReview`-class
  fields, no `DocumentPath` (server path), no recorder identity.
- **The probe-confirmed defect, fixed at the service:** `GetAppointmentByIdAsync` loaded NO
  navigations, so every appointment detail read returned `facilityName:""`,
  `physicianName:""`, `employeeName:""` while the list resolved them — the uneven-.Include
  shape (the same one area 11 fixed on the policy read, whose fix comment now has a sibling).
  Fixed with includes for all six navs the DTO maps; asserted by name in §2.
- **Utilisation had to MOVE (the numbers-must-move rule), and the first attempt was the
  lesson:** approving a `MedicalInsuranceClaim` does NOT consume the policy limit —
  `ConsumePolicyUtilizationAsync` runs on the EXPENSE-claim approval path, and only when the
  claim carries `InsurancePolicyId`. The harness now files the employee's own claim against
  their policy, HR approves it, and the active-coverage read shows utilised 90 / remaining
  49,910 exactly.
- **The move (D3, atomic):** 6 screens re-homed via `git mv` — my-claims →
  `/me/medical/claims`, my-ppe → `/me/safety/ppe`, and all four open report forms
  (incident, hazard, stop-work, environmental) under `/me/safety/report/*` with a new
  chooser page as the unified report-a-concern front door (#36's missing piece). **Built
  fresh:** `/me/medical` (coverage hub: active policy, utilisation bar, dependants),
  `/me/medical/appointments`, `/me/medical/health` (profile / conditions & allergies /
  exams / surveillance tabs — the surveillance detail shows the SUBJECT their full findings
  and restriction, the natural-justice rule), `/me/safety` (hub with live counts),
  `/me/safety/risk-assessments` (awaiting/signed split + the acknowledge dialog),
  `/me/safety/reports` (incidents / stop-work / environmental tabs). 21 link sources
  re-pointed in the same change: portal nav (the Health & Safety group went hub-first),
  landing tiles, desk sidebar (the 6 self entries deleted), both desk hubs, and the three
  desk registers' report CTAs (which now open the portal forms — the register row → portal
  detail precedent, in reverse).
- **Harness legs worth keeping:** the self-arm two-sided AS 404s (the manager's policy /
  appointment / surveillance ids are lookup misses, never 403s — the enumeration-proof
  shape); the acknowledge lifecycle (server signs as the token's employee even when the
  body names the manager, re-sign 422 with the message, DRAFT assessments neither listed
  nor signable, the desk per-employee read still SheReadPolicy-gated, the manager's `mine`
  independently unacknowledged); reporter stamping on all four report writes (an employee
  naming the manager as reporter is overwritten with themselves); and the unlinked wall
  (medical/me + employee-health/me refuse 400, SHE mine routes 403, no 500s).
- **Recorded residuals, deliberately not built:** a "hazards I reported" read (SheHazard
  has NO reporter column — needs a migration another slice should weigh), a PPE
  receipt-acknowledgement (net-new entity surface), and the desk `AddPolicyDependent`
  500-not-404 on a Guid.Empty dependant id (HR-side FK guard, out of this slice's surface).

### Slice 9 — move-in: assets, awards, discipline, grievances. CLOSED 2026-08-26.

`run-slice9.mjs` 80 assertions ×2 green + the 526 ladder (111/33/35/68/47/85/72/75). No
migration. The area-16 owed residual (asset acknowledge/respond + terms-document employee
surface) turned out to be ALREADY BUILT into the 952-line `/hr/assets/me` screen — it
closed by re-homing that screen into the portal and putting the whole surface under
harness for the first time, which is what surfaced the fixes.

- **Four defects found by probe/survey, fixed and asserted by name:** (1) **asset
  acknowledge was silently repeatable** — a second POST answered 200 and rewrote
  `AcknowledgementDate` to now (measured live; the surcharge sibling already 409'd); now
  409 "already acknowledged", and the refused repeat provably leaves the date alone.
  (2) **the frontend's `withdrawNomination` POSTed a route that never existed** (backend:
  `DELETE nominations/{id}`) — every Withdraw click had 404'd forever, the
  fiction-that-type-checks shape on a WRITE. (3) **the awards committee-result read was
  cycle-agnostic** — one pending review anywhere opened every cycle's scores to that
  reviewer; `IsInvolvedInCycleAsync` now scopes involvement to the requested cycle, proven
  two-sided (member reads their cycle, is refused another). (4) **discipline notices were
  acknowledgeable but unreadable** — the subject's case detail carried notification rows
  without their text; `StaffDisciplineNotificationSummaryDto` now carries `Content` +
  `AcknowledgedDate` (the case detail was already the subject's discovery path — the flat
  notifications route is desk-gated by design and stays so, asserted).
- **New backend, deliberately small:** `GET api/awards/me/types/{awardTypeId}` — the
  portal nomination form called the DESK type read (`HR.Awards.Read`) and 403'd for every
  plain employee the moment they picked a cycle (measured); the self arm projects only the
  nominator-facing fields (no eligibility windows, no budget limits, key-absence asserted).
- **The move (D3, atomic):** 10 screens via `git mv` — `/me/assets` (plus a NEW charge
  detail dialog: the by-id surcharge read had no screen; the run also asserts the
  drafted-charge concealment — an un-served charge reads 404, never 403), `/me/awards` + 4
  subpages, `/me/discipline` (with an appeals section over `appeals/mine`, a read that had
  NO caller anywhere), `/me/grievances` + new + detail (the split-audience detail moved
  wholesale on the one-working-surface precedent — griever, named responder and HR are all
  portal users; the desk register's rows open it). **Built fresh:** `/me/discipline/[id]`,
  the subject's case view — allegation, notices with acknowledge, decision + penalty,
  appeal filing inside FR-HR-180's window, the process clock. Sidebar: the six moved self
  entries deleted (awards' whole self block, My Assets, My Record, My Grievances; the
  grievances group is register-only now); desk hubs + grievance register rows + the
  discipline reminders queue and `DisciplineReminderService`'s GrievanceUnanswered
  ActionUrl re-pointed to the portal.
- **Harness facts worth keeping:** the tenant has NO `HrAssetRequisition` workflow
  definition (submit 409s "No active workflow definition" — the run publishes its own, the
  slice-4 pattern); requisition recall is the RAISER's alone (HR is told to reject
  instead); home-aggregate ≡ assets-summary equality on all three shared figures; awards
  `cycles/open` answers even an UNLINKED account by design (catalogue read,
  `TryGetWriteContext`) while `awards` (received) refuses 400 — asserted as measured.
- **Recorded, not built:** "nominations where I am the nominee" (no self read exists;
  arguably a TDC policy question), portal home counts for awards/discipline/grievances
  waiting-on-me figures (slice 11's unified inbox is the natural home), asset assignment
  by-id + plain-list portal routes stay service-covered-without-screens (their content is
  the held table + terms letter), and the awards mine-list's blank `nominatedByName`
  (cosmetic: the list is "mine").

### Slice 10 — my payslips: the read-only payroll adapter. CLOSED 2026-08-26.

`run-slice10.mjs` 35 assertions ×3 green + the 606 ladder (111/33/35/68/47/85/72/75/80).
No migration; D4 delivered exactly as decided — no payroll code touched, no write, no
recompute.

- **The adapter:** `GET api/employee-portal/payslips` (+`/{id}`) queries the frozen
  `PayrollPayslipSnapshots` table directly (`AsNoTracking`, token employee, joined to
  `PayrollRuns` for period columns; the medical-me direct-DbSet precedent — payroll's own
  reads REBUILD payslips live from the run, carry desk gates, and would defeat the
  frozen-snapshot point). The detail deserializes `SnapshotJson` server-side — payroll
  serializes it with no options, so the stored keys are **PascalCase**; deserializing into
  `PayrollPayslipDto` and returning it typed makes the response camelCase like every other
  payload. A blob that no longer parses returns its header with `payslip: null` and honest
  copy. **"Latest" is the newest PAY PERIOD, never `GeneratedAt`** — snapshots regenerate
  in place, so generation time moves without the payslip being new; the harness proves it
  by generating run B's snapshots BEFORE run A's. `home.LatestPayslip` (the slice-3 typed
  stub) is wired on the same ordering.
- **Frontend:** `/me/payslips` (period/number/gross/tax/net; the empty state explains
  payslips appear when payroll publishes them — the tenant's live truth, see below) and
  `/me/payslips/[id]` (company/employee block, earnings, deductions, totals, contributions,
  bank details; print reuses payroll's own `printing-payroll-payslip` body-class family so
  the paper matches the desk's). Landing: a "Latest payslip" stat card that renders ONLY
  when one exists (an all-zero money card would read as "you were paid nothing"), a
  My Payslips tile, and the nav group renamed "Time, Leave & Pay".
- **THE find — cross-module defect #13:** `POST payroll/employee-profiles` has NEVER worked
  for a new profile. `BaseEntity` pre-generates `Id`, so the payment-method rows the service
  attaches via navigation are discovered as MODIFIED — an UPDATE for a row never inserted —
  and the profile INSERT's `DefaultPaymentMethodId` FK 547s. Corroborated by the DB:
  **zero rows in `PayrollEmployeeProfiles` tenant-wide** — payroll has never enrolled an
  employee through its own API. Recorded (with the one-line-per-method fix), not fixed:
  the file is the payroll developer's. The harness seeds profiles by SQL and drives
  everything else (runs → calculate → snapshots) through payroll's real routes.
- **The consumed-surface FYI is #12** in the same doc: the columns + JSON shape HR now
  reads, and the recalculate-hard-deletes-snapshots behaviour employees will now notice —
  which the harness asserts as the graceful path (the payslip an employee saw yesterday
  404s after a recalc, returns as a NEW id after regeneration, and home follows it).
- Harness facts: an OPEN run blocks its pay period forever (the run never closes them —
  period pairs derive even/odd from a per-second base); snapshots need `calculate` +
  ≥1 employee in the run; the run's approval gate no-ops without an active payroll-run
  workflow definition; `Deductions` is EMPTY when no tax was computed (asserted as the
  measured reality); the unlinked wall answers 403 "not linked" in words.

### Slice 11 — approvals & tasks inbox + unified notifications. CLOSED 2026-08-26.

`run-slice11.mjs` 98 assertions ×2 green + the 641 ladder
(111/33/35/68/47/85/72/75/80/35). No migration. Spec rows **#37 (my approvals)** and
**#38 (my tasks — endpoint but never a screen)** close here, and **#40** moves off the desk
admin console. Probes kept: `probe-slice11.mjs`/`11b`/`11c`/`11d`/`11e` with their outputs.

- **THE find, and the reason this slice's inbox does not approve anything: approving from
  any GENERIC workflow surface consumes the approval and STRANDS the record.** The engine
  advances and completes the instance, but the module's `IWorkflowStatusAdapter` — the thing
  that actually writes `Approved` onto the entity — is only ever invoked by the module's own
  approve endpoint. Measured on all three generic paths (`approvals/{id}/process`,
  `steps/{id}/process`, and `workflow/platform/mobile/actions`, which is what the existing
  desk `/workflow/inbox` posts to): HTTP 200, `"status":"Succeeded"`, approval row gone,
  workflow `hasActiveInstance:false` — and the movement still `Submitted`, now with no
  pending approval that could ever move it. Recorded as **cross-module #15**; the portal
  inbox is therefore **read-and-navigate**, every row deep-linking to the record page whose
  module commands carry the whole outcome (which is also the W1 rule: never build a bespoke
  HR approval UI).
- **Its sibling, #14: the platform's own pending feeds have effectively never returned
  content.** `GET Workflow/approvals/pending` and `tasks/pending` return raw EF entity
  graphs; the `WorkflowApproval ↔ WorkflowStepInstance` cycle throws *after* the 200 status
  line, so the client gets **200 with a body that dies mid-stream** whenever the caller has
  ≥1 row, and a clean `[]` when they have none. The admin dashboard's `pendingApprovals`
  stat card swallows it in a `Promise.allSettled`. Both facts are asserted in §8 on purpose,
  so the assertions flip the day the platform team fixes them.
- **So the portal projects its own flat DTO** (`GET employee-portal/inbox`), and — the part
  that makes it a product rather than a feed — enriches every row through
  `IWorkflowEntityDisplayService`, the canonical ~70-entity-type resolver that none of the
  three platform feeds consult. Rows carry entity number, entity name, step, workflow and
  the record URL; the desk inbox shows a bare GUID. Two registers, plus a third: **action
  items**, the acknowledge/respond/accept acts the HR areas had scattered (movement
  response, asset acknowledgement, asset charge, discipline notice, grievance response,
  risk acknowledgement, training bond) — the consolidation slice 9 recorded as owed.
  Dedup rule: a step carrying somebody else's pending approval is listed as an approval and
  **never** duplicated into that person's tasks (asserted live, both ways).
- **The risk-acknowledgement family collapses to ONE summary row.** Slice 8's read returns
  every approved/active assessment in the tenant flagged per-caller, so a fresh employee
  "owes" dozens at once (25 in this tenant, measured) — per-row items would have drowned
  the register, and every one of them is signed on the same page anyway.
- **The forgery hole, found by the probe and closed here: `POST api/Notifications` was
  behind bare `[Authorize]`** and takes an arbitrary `RecipientId`, title, message and
  `actionUrl`. A plain Employee token planted a notification in another user's feed, 201.
  Combined with the engine's own "Approval Required" rows, that is a phishing surface the
  portal would have shown every employee. Now admin-gated like its `send-push` sibling —
  safe because modules write through `INotificationService` in-process and the one frontend
  wrapper had zero callers. Recorded as **#16** (the fix lives in a platform controller).
- **Unified notifications: four stores, one shape** (`GET employee-portal/my-notifications`).
  The general platform store is keyed by USER, appraisal and orientation by EMPLOYEE, and
  the movement rows are computed at request time. Read state stays with whichever store
  owns it — mark-read dispatches BY SOURCE from the client — and `unreadCount` is the SUM of
  the three persisted stores' own unread reads, never derived from the capped page. The
  computed movement rows honestly carry `isRead: null` / `canMarkRead: false` (their ids are
  regenerated on every request), and the screen says "Live" rather than offering a control
  that would silently do nothing.
- **A dangling-URL fix the feed would otherwise have surfaced:** `OrientationDataSeeder`
  wrote `NavigationUrl = "/my-orientations"`, a Blazor-era route that does not exist in the
  Next.js app → `/me/orientation`. Same family as slice 7's movement/travel re-points.
- **Route resolution went wider this slice:** all 71 `ActionUrl` values
  `WorkflowEntityDisplayService` can emit were resolved against the app tree, because the
  inbox is the first surface that shows them. **70 resolve; one does not** —
  `/finance/fixed-assets/register/{id}` has no page (only `…/edit`), recorded as **#17**.
  All 25 HR URLs are clean, which is slice 7's work holding.
- **D3 move-in:** `/hr/performance/notifications` (a pure employee-as-subject screen) and
  its `AppraisalNotificationsPanel` deleted, absorbed by the unified feed; the desk sidebar
  entry removed and the performance hub card re-pointed at `/me/notifications`. The portal
  nav's two action buttons — which until now left the portal for the desk `/workflow/inbox`
  and the `/notifications` **admin console** (an administration surface a plain Employee has
  no business seeing) — now point at `/me/inbox` and `/me/notifications` with live badges.
- **A vacuous green caught in the harness itself, worth keeping as a lesson:** the first
  §8 draft read `strandRow.id` where the DTO field is `approvalId`, so the process POST went
  to `/approvals/undefined/process` and did nothing — and "the record is still Submitted"
  passed *for the wrong reason*. Only the paired assertion ("…and the approval is gone")
  failed and exposed it. The leg now proves the act EXECUTED (HTTP 200) before asserting the
  strand, and the blind `catch {}` that hid the error is gone. **Assert the cause, not just
  the symptom — a call that never landed looks exactly like a call that did nothing.**
- Two more facts the run records by name: the ordinary Draft→Approval→Approved instance
  **completes** on approval (so its trailing manual bookend is never left pending — a
  user-assigned task is not reachable that way; the run publishes a manual-middle definition
  to produce a genuine one), and the bond leg is **seeded rather than compared against an
  empty read**, because a zero-to-zero equality proves nothing.
- **Residual recorded, not built:** `appraisalNotificationService` in the frontend now has
  no consumers (its screen moved into the unified feed) — a candidate for the slice-14
  polish sweep; and the SignalR live-push wiring that the desk `HeaderNotificationBell` uses
  is not wired into the portal (the portal reads on navigation and after every mutation).
