# HR Area 25 — Slice 0 census (measured 2026-08-25)

Companion to `HR-Area-25-Employee-Self-Service-Build-Plan.md` §7 slice 0. Everything here was
**measured**, not assumed: the DB numbers by SQL (`dev-harness/hr-portal/census-slice0.sql`),
the endpoint behaviour by the live harness (`dev-harness/hr-portal/run-slice0.mjs`, 111
assertions, green twice from different DB states), the route/screen map by reading the
controllers and `frontend/src/app`.

## 1. The link census (DEFAULT tenant) — gates D1/D5

| Metric | Value |
|---|---|
| Users / linked to an employee | 7,349 / 7,329 (99.7%) |
| Employee-role users unlinked | **1** |
| LDAP-provisioned users (total / unlinked) | 0 / 0 — LDAP not yet in use |
| Links to deleted employees / cross-tenant / dangling / employee shared by 2 users | 0 / 0 / 0 / 0 |
| Live employees / soft-deleted | 7,438 / 22 |
| `EmailAddress` blank / duplicated (live; also incl. soft-deleted) | 0 / **0** |
| `EmployeeNumber` blank / duplicated (live; also incl. soft-deleted) | 0 / **0** |
| Employee numbers colliding with any username/email | **0** |
| Employees with `ManagerId` set | 213 (2.9%) |

**Consequences:** the D1 employee-number resolver and the D5 exact-match auto-link are safe
on this data — no ambiguity case exists live today, and D1's "username/email wins" rule has
zero collisions to arbitrate. My-team (slice 13) will be honestly-empty for ~97% of managers
(the known org-data gap, TDC's to fix). The slice-1 unlinked-users queue opens with exactly
one row.

## 2. The 40 spec destinations — endpoint / screen / verdict

Verdicts: **S** = screen exists · **E** = endpoint only, screen missing/HR-shaped · **M** =
no self-shaped endpoint at all. Tally: **19 S · 14 E · 7 M**.

| # | Spec destination | Backend (verb + route) | Existing screen | V |
|---|---|---|---|---|
| 1 | /me home | `GET api/employee-portal/dashboard` (movements+assets only — slice 3 extends) | — | E |
| 2 | attendance punch | `POST api/staff-attendance-logs/punch` (token actor, InternalOnly); history `GET .../employee/{id}` self-arm | — (`hr/attendance/logs` is HR's register) | E |
| 3 | leave planner | `GET api/hr/leave-plans/employee/{id}` self-arm | `hr/leave/plans` (HR-shaped) | E |
| 4 | my leave requests | `GET api/Leaves/employee/{id}/history` + `/balances` self-arm; **no /mine route** | `hr/leave/requests` (HR-shaped, employee picker) | E |
| 5 | my encashments | `GET api/hr/leave-encashments/employee/{id}` self-arm | `hr/leave/encashments` (HR register) | E |
| 6 | my travel | `api/staff-travel/me/*` (6 actions: list/detail/create/update/submit/cancel) | `hr/travel/mine` (+new/[id]/edit) | S |
| 7 | my appraisals | `GET api/PerformanceAppraisals/my-appraisals/me` | `hr/performance/appraisals` | S |
| 8 | my goals | `GET api/EmployeeGoals/by-employee/{id}` self-arm; **no /mine** | `hr/performance/employee-goals` (HR-shaped) | E |
| 9 | my development plan | `GET api/DevelopmentPlans/mine` (+`/my-team`) | `hr/performance/development-plans` (mine tab) | S |
| 10 | peer evaluations | `GET api/PeerEvaluations/me` + draft/submit | `hr/performance/peer-reviews` | S |
| 11 | my check-ins | `GET api/CheckIns/me` + `/me/conducting` | `hr/performance/check-ins` | S |
| 12 | my journal | `GET api/PerformanceJournal/mine` | `hr/performance/journal` | S |
| 13 | my training | `training-completions/mine`, `training-requests/mine`, `training-nominations/mine`, `certificates/mine` | `hr/training/my-training` | S |
| 14 | request training | `POST api/training-requests` (+submit; InternalOnly) | `hr/training/requests/new` | S |
| 15 | my learning paths | `GET api/learning-paths/enrollments/mine` | `hr/training/my-learning` (+steps) | S |
| 16 | my waitlist | `GET api/training-waitlist/employee/{id}` self-arm; **no /mine** | — | E |
| 17 | my orientations | `GET api/employee-orientations/mine`; notifications `api/orientation-notifications/mine*` | `hr/orientation/mine` (+[id]) | S |
| 18 | career portal dash | `GET api/employee-portal/dashboard` | `hr/movements/mine` (combined) | S |
| 19 | my movements | `api/employee-portal/movements*` (+respond); `api/staff-movements/employee/me`, `checklist/mine` | `hr/movements/mine` | S |
| 20 | career timeline | `GET api/employee-portal/career-path` | — (`career-paths/[employeeId]` is HR-shaped) | E |
| 21 | acting appointments | `GET api/employee-portal/acting-appointments` | — (`hr/movements/acting` is HR register) | E |
| 22 | my secondments | `GET api/employee-portal/secondments` | — | E |
| 23 | movement notifications | `GET api/employee-portal/notifications` (computed) | — | E |
| 24 | internal job board | `GET api/job-vacancies/published`; apply `POST api/job-applications/apply-internal` (+draft/submit) | `hr/recruitment/job-board` | S |
| 25 | my applications | `GET api/job-applications/my-applications` | folded into job-board ("Already Applied") | E |
| 26 | my panel interviews | `GET api/job-interviews/me/panelist-slots` | `hr/recruitment/my-panel` | S |
| 27 | my medical coverage | **none** — `api/medical-insurance/employees/{id}/policies*` is MedicalReadPolicy, no self arm | — | **M** |
| 28 | my medical claims | `api/medical/me/expense-claims*` (8 actions) | `hr/medical/my-claims` | S |
| 29 | my appointments | **none** — `api/medical-clinical/employees/{id}/appointments` is MedicalReadPolicy, no self arm | — | **M** |
| 30 | my health (occ-health) | `api/employee-health/me/*` (profile/conditions/allergies/exams/download) | — (`hr/medical/health` is HR's) | E |
| 31 | my awards | `api/awards/me/*` (20 actions: awards, nominate, vote, committee reviews) | `hr/awards/me` (+4 subpages) | S |
| 32 | my assets | `api/employee-portal/assets*` (+acknowledge, terms-document, requisitions, surcharges) | `hr/assets/me` | S |
| 33 | my safety hub | **no hub**; pieces exist: `api/safety/ppe/issuances/mine`, `safety/stop-work/mine`, `safety/environmental/incidents/mine` | — (`hr/safety/my-ppe` covers PPE only) | **M** |
| 34 | my risk assessments | **none** — `for-acknowledgement/{id}` read is SheReadPolicy (desk); only the acknowledge POST self-defaults | — | **M** |
| 35 | my health & wellbeing | **none** — occ-health surveillance `by-employee/{id}` is MedicalReadPolicy, no self arm | — | **M** |
| 36 | report a concern | `POST api/safety/incidents`, `api/safety/hazards`, `api/safety/stop-work` (deliberately ungated beyond InternalOnly) | 4 separate `hr/safety/report-*` screens, no unified page | S |
| 37 | my approvals | `GET api/Workflow/approvals/pending` (+process); `GET api/workflow/platform/mobile/inbox` | `workflow/inbox` | S |
| 38 | my tasks | `GET api/Workflow/tasks/pending` (+steps/process) — inbox screen reads only the approvals feed | — | E |
| 39 | my profile | **none** — `Auth/me` is identity-only (no employeeId!); `employees/{id}/profile` is EmployeeReadPolicy | `profile` is the USER-account page | **M** |
| 40 | notifications bell | `api/Notifications` (+unread, unread-count, mark-read, mark-all, preferences) | `notifications` + NotificationCenter | S |

## 3. What the harness proved live (run-slice0.mjs, 111 assertions ×2)

- **Every self read above answers 200 JSON as a plain linked employee** — ~80 endpoints
  probed, including all 25 `EmployeePortalController` routes' read side, the five
  self-prefixed controllers, and the per-area `mine` singles. No dead paths found.
- **The self-arm is two-sided** on the id-bearing routes (leave history/balances/plans/
  encashments, training-waitlist, attendance logs, goals): own id 200, another's id 403.
- **D9 punch: MOVE IN.** `POST staff-attendance-logs/punch` works for a plain employee —
  token actor, CheckIn and CheckOut both proven, geofence fields optional, immediate
  processing on. The punch screen belongs in the portal.
- **D9 oaths: MOVE IN.** `GET hr/oaths-of-secrecy/mine` + `POST .../affirm` both work.
  Affirm takes **no id by design** — it creates the caller's own oath, server-stamps the
  date, records the IP. (Slice-0 note: nothing stops a second affirmation; re-affirmation
  appears intentional. Watch it in the move-in slice.)
- **The unlinked user hits a clean refusal wall**: 403 (permission-shaped) or 400
  (no-employee-shaped) across the surface, **no 500s, no data leaks**, and `Auth/me` still
  works — so D8's friendly "not linked yet" page is buildable purely client-side.
- **`Auth/me` carries NO employee link** — measured keys: identity, tenant, roles,
  permissions only. The D8 route gate needs slice 1 to expose the link (extend the
  Auth/me payload or a dedicated `/me` resolution endpoint). The harness asserts the
  gap so the assertion flips deliberately when slice 1 closes it.
- **Manager side works**: `employees/manager/me/direct-reports` resolves via `ManagerId`
  (fixture employee appears for their manager), `DevelopmentPlans/my-team` answers.
- A fresh employee's 67 empty list reads are recorded in the run output — the move-in
  slices must seed their own domains before deep-probing content (the empty-read lesson).

## 4. FRD sweep (slice 0(f))

The TDC FRD itself is not in the repo (sources live at `D:\Rhema\TDC ERPS\*.docx/pdf`);
in-repo hits are secondary citations. Portal-bearing requirement IDs already delivered or
landing here: **AST-5/6/8** (asset terms/requisition/acknowledge — `employee-portal`
routes exist, screens land in slice 9), **AWD-03/04/11** (portal nomination/voting —
`awards/me` + screens exist), FR-HR-046/111/152 anchors touched in their own areas. No
un-built Mandatory FRD requirement was found that lands uniquely in area 25 — the spec
parity target remains the Blazor `PortalTopNav` 40. Two secondary anchors worth keeping:
the oaths self-surface is **FR-HR-030** (controller docstring, corroborated by
`frontend/src/services/hr/probation.service.ts:292`), and the desk sidebar entry point for
the slice-2 switcher lives at `frontend/src/components/layout/sidebar.tsx` (~line 777,
next to the existing oath entry).

## 5. Fixtures

`dev-harness/hr-portal/setup.mjs`: prefix `a25v_`, password `A25Verify123!`, DEFAULT
tenant. Four actors per run: `hr` (HR role), `manager` (Employee+Manager, linked),
`employee` (Employee, linked, reports to manager), `unlinked` (Employee role, **no
employee record**). Runs leave fixtures behind; safe to repeat.
