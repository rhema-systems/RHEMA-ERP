# HR Leave Management — Closure Plan

**Status:** PLANNED 2026-09-17. Nothing in this document has been built. It is the complete
definition of what remains before the leave module can be called done, written from a
screen-by-screen source walk (`HR-LEAVE-SYSTEM-GUIDE.md`) plus a field-level trace of every
configurable setting and a requirements pass against what TDC has asked for.

**Owner:** whoever picks up leave next. **Read this before writing any code.**

---

## START HERE — the fresh-chat briefing

If you are starting a new session on the leave module, read these four things in this order and
nothing else first:

| # | Read | Why | Time |
|---|---|---|---|
| 1 | **This document's §1, §2 and §3** | what "done" means, what is already built, and the gap register | 15 min |
| 2 | `docs/HR/HR-LEAVE-SYSTEM-GUIDE.md` — the **two rules** block above chapter 1, then **§1.4** and **§4.4** | the two demo-blocking defects, the accrual arithmetic, and the eleven ghost settings | 20 min |
| 3 | **§4 of this document — the eleven decisions** | you cannot scope half the slices without answers; six need TDC | 10 min |
| 4 | `docs/HR/HR-WORKFLOW-ENGINE-INTEGRATION.md` | slices C1–C3 all touch the engine, and the recipe has traps | 10 min |

**Then** pick from §5's slice table. It is ordered so each slice is shippable on its own.

**The four governing rules that apply to this module** *(from `docs/HR/README.md`, repeated here
so they are not re-derived)*: payroll is another developer's — read it, never write it; HR posts
nothing to the GL, it records the money event in the Finance backlog; never build a module-specific
approval UI, plug into the workflow engine; ported services stamp `TenantId` explicitly.

**And the two house rules for this repo:** never run `dotnet build` — stop and ask; stage the
slice and hand over the commit message, never `git commit`.

---

## 1. What "done" means for this module

The leave module is done when all seven of these are true. Anything not on this list is out of
scope and §6 says so explicitly.

| # | Done when | Covered by |
|---|---|---|
| **D1** | **No screen lies.** Every control either works or is not rendered; every setting either changes behaviour or is removed from the form | slices A1–A3, B1–B3 |
| **D2** | **The end-to-end flow TDC described works, on the screens, in one pass** — employee proposes dates → supervisor approves, rejects, or sends back with different dates → employee answers → HR sees and holds the final dates | slices C1–C3 |
| **D3** | **Leave is visible as time, not just as rows** — a calendar the desk and the employee can both read, showing who is away when | slice E1 |
| **D4** | **Dates that matter are watched.** Upcoming leave, leave that has started and not been closed, mandatory leave not yet taken, carry-over about to expire — each produces a reminder, once | slice E2 |
| **D5** | **A date can be changed after approval** without cancelling and re-keying the request | slice C4 |
| **D6** | **The joins that exist in the schema are written** — attendance knows somebody is on leave, a request knows which plan it came from | slices D1, C3 |
| **D7** | **It is verified.** A `dev-harness/hr-leave` suite, run twice green, covering every write endpoint and every refusal in this document | slice F1 |

---

## 2. State of play — what is already built and working

**Do not re-audit any of this.** It was traced to source on 2026-09-17.

### 2.1 Working, complete, leave it alone

| Area | State |
|---|---|
| **The rulebook** | 9 leave types with sub-types, staff-level allocations (effective-dated), eligibility rules (gender / level / unit / position, with a gender AND-qualifier), and accrual policies. All read and enforced |
| **Entitlement resolution** | sub-type cap → effective-dated allocation → leave-type default, clamped to the annual ceiling. Correct |
| **Accrual** | monthly / quarterly / semi-annual / annual, incremental or full-grant, with a service bar and pro-rate-on-join. Computed live on every read |
| **The eight create checks** | eligibility · service access · past dates · date order · minimum notice · overlap · accrued balance · reliever. All fire, each with its own message |
| **Chargeable-day counting** | weekends and holidays excluded per leave type, holidays matched on overlap not containment |
| **The balance ledger** | six components, all derived from source rows by `LeaveBalanceRecalculationService`, recalculated automatically after every write. Nothing is hand-entered except an adjustment |
| **Adjustments** | signed, reason-coded, remarked, actor stamped from the token, with a live before/after preview on the form |
| **Relievers** | roster on the employee record, auto-filled by priority into both slots, skipping anyone unavailable, with the manager as a last resort — and the form says when it did it |
| **Reliever clash detection** | three sources (their own plan, their own live request, another plan naming them), one query per window, advisory not blocking |
| **The plan conversation** | draft → submit → approve / reject / **suggest different dates** → employee accepts or counters → resubmit. Complete, both sides |
| **Encashment** | four checks, server-derived payout from emoluments and the leave type's rate policy, one-per-request, create-and-submit in one transaction, `Processed` as the only balance-moving status |
| **Year-end** | carry-over (capped, set-not-stacked) and forfeiture (expiry + cut-off, posted as a named adjustment, idempotent) |
| **Compliance** | mandatory-leave register with Taken / Scheduled / Outstanding |
| **Workflow integration** | all three entities on the engine, with the `HrWorkflowFallbackAuthority` guards; no auto-approve |
| **Permissions** | four tiers, self-or-permission on every per-employee read and write, correct throughout |
| **Portal** | six screens, no employee picker anywhere, reliever choices from the caller's own roster |
| **Demo data** | the best-seeded module in HR — see the guide §2.1 |

### 2.2 The two things a fresh session most often gets wrong

1. **There are two availability figures.** The screen shows `Entitled + Carried + Adj − Used −
   Pending − Encashed`; the server's create check substitutes **AccruedToDate** for Entitled. They
   differ by ~7 days on annual leave in September. Guide §1.4.
2. **`LeaveBalance` is keyed on (employee, leave type, year) with no sub-type.** Every "why doesn't
   the sub-type cap stick" question traces back to this one line.

---

## 3. The gap register

**37 findings from the source walk, plus 9 requirement gaps from TDC's stated flow, plus 4
infrastructure gaps = 50 items.** Grouped by what kind of work each is.

### 3.1 Defects — it is built and it is wrong

| ID | What | Severity | Fix size |
|---|---|---|---|
| **L-1** | Desk **Submit request** never calls the submit endpoint. Request lands at `Pending` with no workflow instance; cannot be submitted, cannot be approved, days already deducted, only Cancel exits | 🔴 blocking | 3 lines, frontend |
| **L-2** | Encashment **Mark as paid** sends `user?.id` into an `Employee` FK → constraint failure. The action is unusable | 🔴 blocking | 1 word, frontend |
| **L-3** | `GetAllLeaveBalancesAsync` never computes accrual, so the **Accrued** column on the only screen that shows it repeats Entitled | high | one `foreach`, backend |
| **L-7** | Requests status filter is client-side over the fetched page — can under-report silently | medium | add the query param |
| **L-8** | `/hr/leave/requests?employeeId=` deep link from the employee profile is ignored | low | read the search param |
| **L-10** | Approvals queue is built from `Employee.ManagerId`, not the workflow assignee | medium | small — or retire the screen for `/workflow/inbox` |
| **L-11** | Six delete/retire controls rendered to a persona that holds none of them | medium | permission-gate the controls |
| **L-13** | Leave-type PUT is a replace-set; a partial body unlinks every allowance and resets every flag | medium | document + guard, or move to PATCH |
| **L-14** | The new-request form's balance strip shows the policy figure, not the enforced one | medium | show both, once L-3 lands |
| **L-16** | Plan dialog has no sub-type field though the payload carries one | low | one field |
| **L-17** | A plan's `Year` comes from the list filter, not its own start date | medium | one line |
| **L-34** | `LeaveSubType.IsActive` never filtered — retired sub-types stay in the picker | low | one `Where` |
| **L-35** | `LeaveType.IsActive` enforced by the picker, not the service | low | one guard |

### 3.2 Ghost settings — configurable, saved, read by nothing

Each needs the same decision: **wire it, or take it off the form.** Guide §4.4 is the full trace.

| ID | Setting | Decision needed |
|---|---|---|
| **L-29** | **Pro-rate on exit** (Accrual tab) — defaults on, displayed back, engine reads only pro-rate-on-**join** | wire it into `GetAccruedAsOfAsync` using the separation date, or remove. ⚠ feeds the final settlement |
| **L-30** | **`LeaveType.IsPaid`** — display-only; unpaid leave produces no deduction anywhere | needs the payroll owner (§4 D-6) |
| **L-36** | **Calendar colour** — form says "used on leave calendars"; no calendar exists | becomes real with slice E1 |
| **L-31** | Leave ignores `PublicHoliday.SubstitutionDate` | adopt `HrWorkingDayCalculator` (slice B2) |
| **L-32** | Leave ignores `PublicHoliday.IsActive` and which calendar a holiday belongs to | same slice |
| **L-33** | `ObservanceType`, `AttractsHolidayPay`, `HolidayPayMultiplier` — zero read-sites anywhere in the solution | decide: wire, or drop from the holiday form |
| **L-28** | Sub-type caps and sub-type-scoped allocations never reach a balance | schema decision — §4 D-2 |

### 3.3 Missing features — TDC asked for it and it does not exist

These are the nine derived from the flow described on 2026-09-17. **This is the section to read
first if you are checking the module against the requirement rather than against the code.**

| ID | TDC's words | What exists today | What is missing |
|---|---|---|---|
| **R-1** | *"staff submitting their suggested leave dates"* | ✅ both a **plan** (an intention for the year) and a **request** (the actual application) | nothing — but see R-3: the two are not joined |
| **R-2** | *"supervisor reviewing for approval"* | ✅ the workflow engine, one step, routed to the Manager/HR/TenantAdmin roles | the step is **role-routed, not line-manager-routed** (engine defect #3). "Any manager can approve any request" is the live behaviour |
| **R-3** | *"or sending back for correction with suggested dates"* | ✅ **on a leave PLAN** — `suggest-changes` → `ChangesSuggested` → employee accepts or counters | **✘ on a leave REQUEST.** `LeaveRequest` has no `SuggestedStartDate`/`SuggestedEndDate`/`ManagerSuggestionNotes`, no `respond-suggestion` endpoint, and the status adapter has no `ChangesSuggested` state. The generic engine's **Send back** governance action exists but the leave adapter does not map it, so the record's own status would not move |
| **R-4** | *"hr getting the final leave dates"* | ✅ HR can read every request | **✘ there is no HR step.** The seeded definition is **one** approval step where Manager **or** HR may approve and **one** approval is enough. "Supervisor approves, then HR confirms and records" is not the configured flow — it needs a two-stage definition |
| **R-5** | *"the leaves listed or in calendar"* | ✅ listed — but **per employee only**; there is no organisation-wide request register (L-6) | **✘ no calendar of any kind.** Nothing in the product draws leave on a calendar, for the desk, the manager, the team or the employee |
| **R-6** | *"leave due notices or alerts"* | ✘ nothing | **✘ there is no `LeaveReminderService`.** HR has **eleven** reminder engines — assets, certifications, discipline, ID expiry, probation, separation, SHE, movements, travel, teams, attendance — and none for leave. No notification is raised on submit, approve, reject, or an upcoming start date |
| **R-7** | *"confirming the upcoming leave will be observed"* | ✘ nothing | **✘ no pre-leave confirmation.** An approved request goes quiet between approval and its start date. Nobody is asked "is she still going?" and nothing records the answer |
| **R-8** | *"possible shifting of the leave to a different day even after the planning is done"* | ✘ | **✘ an approved request cannot be amended.** Edit is Draft-only and the service enforces it. The only route is **cancel and re-key**, which loses the number, the approval and the history. Same for an approved plan |
| **R-9** | *"holidays"* | ✅ calendar entity, 13 Ghanaian holidays × 2 years seeded, excluded from chargeable days | **✘ leave ignores three of the calendar's settings** (L-31, L-32) and **⚠ TDC has not confirmed who maintains the calendar or supplied the gazetted list** — an open question since 2026-08-20 |

**Four more that TDC has not named but that belong in the same conversation:**

| ID | What | Why it belongs |
|---|---|---|
| **R-10** | **Return to work / resumption.** `Close` exists, is `HR.Leave.Write`, refuses before the end date, and moves no numbers. Nobody is prompted to do it, so approved leave stays approved for ever | it is the other end of R-7 |
| **R-11** | **Leave register + balance export.** No org-wide request list (L-6), no CSV from anywhere (L-18). The reports catalogue lists these as the most-wanted missing HR reports | every leave conversation ends in "can I have that as a spreadsheet" |
| **R-12** | **Bulk approval.** The UAT plan already records "bulk/multi-select actions on approval queues (Leave, Travel, Medical claims) — not built" | a December approval queue is 200 rows |
| **R-13** | **Compassionate leave offset against annual leave** — recorded in the finish plan §2e as needing a design call; there is no offset or advance concept anywhere in the module | already an open `DECIDE` row |

### 3.4 Integration gaps — the joins that exist and are never written

| ID | Join | Consequence |
|---|---|---|
| **L-27** | `StaffDailyAttendance.LeaveRequestId` + `StaffAttendanceStatus.OnLeave` | **approving leave marks no attendance day.** `DaysOnLeave` on the monthly summary — which the **payroll export** reads — is zero unless a clerk hand-edits the daily record. `LeaveRequest.AttendanceDays` is permanently empty. **The largest structural gap in the module** |
| **L-9** | `LeaveRequest.LeavePlanId` | an approved plan cannot become a request; the planning cycle dead-ends and the employee re-keys their own dates |
| **L-37** | `MedicalExpenseClaim.LeaveRequestId` (commented *"Leave Integration"*) | a medical claim is never tied to the sick leave it arose from |
| **L-21** | Encashment payout → Finance | on the HR↔Finance backlog rows 23/24 (**both 🔴**), deferred to the single GL sweep. **Do not build a GL posting here** — record the money event |
| **X-1** | **Two different daily-rate formulas in one product.** Leave encashment uses `(basic + linked allowances) ÷ 22`; the separation settlement uses `monthly × 12 ÷ 365`. On GHS 6,000/month those differ by ~38% | open question with TDC since 2026-08-20 — §4 D-7 |
| **X-2** | **FR-HR-046 says leave is *"encashed only on exit, no other route"*** — and the leave module ships an in-service encashment path, with annual leave flagged `AllowCashConversion` in the seed | **this is a requirements conflict and it must be resolved before sign-off.** §4 D-8 |

### 3.5 Infrastructure gaps

| ID | What | Note |
|---|---|---|
| **V-1** | **There is no `dev-harness/hr-leave` suite.** Twenty-nine other HR areas have one; leave does not. Everything known about this module's behaviour came from reading source, not from running it | the single biggest risk in this plan |
| **V-2** | No leave rows in the **reports catalogue's** delivered set — leave register 🔲, balance report 🟡 no export, encashment register 🔲, leave liability 🔲 | R-11 |
| **V-3** | `LeaveType.MinDaysNotice` and friends are **per type**, but there is no **tenant-level leave policy** surface — no "leave settings" page where the module's own defaults live | may be unnecessary; decide in D-11 |
| **V-4** | Year-end is **deliberately not scheduled** (closure-ledger decision: carry-over and forfeiture move balances rather than raise reminders, so automating them is TDC's policy call). ⚠ The guide's L-25 calls this a gap — **it is a decision, not an oversight.** Correct the guide when you touch it | already decided |

---

## 4. Decisions needed — eleven, six of them TDC's

**Do not start slices C1–C4, D1 or E1 without the answers marked TDC.** Everything else can
proceed on the recommendation given.

| # | Decision | Recommendation | Who |
|---|---|---|---|
| **D-1** | **Does a leave REQUEST need send-back-with-suggested-dates (R-3), or is the PLAN the only place that conversation happens?** | **Build it on the request.** The plan is annual and optional; the request is where real dates are argued about. Mirror the plan's three fields + `respond-suggestion` endpoint + a `ChangesSuggested` leave status, and map the engine's Send back onto it | **TDC** |
| **D-2** | **Should a leave balance be per sub-type?** (L-28) — today all Sick sub-types share one pot | **No — keep one pot per leave type, and enforce the sub-type cap per request instead.** Per-sub-type balances multiply every row on the Balances screen and the year-end runs. A per-request cap delivers what the setting promises at a fraction of the cost | **TDC** (policy) |
| **D-3** | **Is HR a second approval step after the supervisor (R-4)?** | **Yes — publish a two-stage definition:** stage 1 line manager, stage 2 HR, with `PreventInitiatorApproval` on. It is configuration, not code, and it is what TDC described | **TDC** |
| **D-4** | **What does "confirming the upcoming leave will be observed" mean operationally (R-7)?** Options: (a) a reminder to the employee and manager N days out, no record; (b) an explicit confirm/defer action on the request; (c) both | **(c)** — the reminder is slice E2, the confirm action is a small addition to C4's amend path | **TDC** |
| **D-5** | **Who may shift an approved leave date (R-8), and does it need re-approval?** | **HR may amend; the employee may request an amendment; any change to the dates re-opens the approval.** Anything else lets an approval mean something it did not | **TDC** |
| **D-6** | **What should `IsPaid = false` actually do (L-30)?** | Needs the **payroll owner**: HR records the leave and the days, payroll decides the deduction. Raise it as a `docs/HANDOFF-PAYROLL-*.md`, do not guess | **payroll owner** |
| **D-7** | **One daily-rate basis, or two (X-1)?** | **One.** Whichever TDC names, both leave encashment and the separation settlement should use it | **TDC** |
| **D-8** | **FR-HR-046 — is in-service leave encashment permitted at all (X-2)?** | If not, `AllowCashConversion` should be **off on every seeded type** and the portal screen gated behind separation. If it is permitted, FR-HR-046's wording needs amending. **Do not ship both readings** | **TDC** |
| **D-9** | **Whose calendar is the leave calendar (R-5)?** Options: my leave · my team · my unit · organisation-wide | **Build one component, three entry points** — `/me/leave` (mine), `/hr/leave/calendar` (filterable, HR), and a team strip on the manager's view. Colour by leave type, which finally makes `CalendarColor` real | recommend |
| **D-10** | **Should leave adopt `HrWorkingDayCalculator` (L-31/32)?** | **Yes.** Discipline already uses it for statutory clocks; two working-day calculators in one product is one too many. It brings substitution dates, the default calendar and `IsActive` with it | recommend |
| **D-11** | **Is a tenant-level leave settings page needed (V-3)?** | **No, not yet.** Everything configurable today is genuinely per leave type. Revisit if D-4 or D-5 produce tenant-wide numbers (e.g. "confirm N days before") | recommend |

---

## 5. The slice plan

Ordered so each slice ships on its own and nothing blocks on something later. **Sizes are
relative** — S is under half a day, M is a day, L is two or more.

### Wave A — stop the bleeding *(no decisions needed, do these first)*

| Slice | What | Size | Closes |
|---|---|---|---|
| **A1** | Desk new-request calls submit after a non-draft create, with the portal's honest "saved, but not submitted" fallback | S | **L-1** |
| **A2** | `processedByEmployeeId` → `user?.employeeId` | S | **L-2** |
| **A3** | Compute accrual in `GetAllLeaveBalancesAsync`; show accrued **and** entitled on the request form's balance strip | S | **L-3, L-14** |

**Done when:** the demo can be driven end to end without the guide's two rules.

### Wave B — make the screens honest *(no TDC decisions)*

| Slice | What | Size | Closes |
|---|---|---|---|
| **B1** | Permission-gate the six admin-only controls; filter `LeaveSubType.IsActive`; enforce `LeaveType.IsActive` in the service | S | **L-11, L-34, L-35** |
| **B2** | Leave adopts `IHrWorkingDayCalculator` — substitution dates, the default calendar, `IsActive` | M | **L-31, L-32**, D-10 |
| **B3** | Ghost-setting sweep: wire pro-rate-on-exit **or** remove it; decide `ObservanceType` / holiday-pay fields; correct the plan `Year`; add the plan sub-type field; server-side status filter; honour the `employeeId` deep link | M | **L-29, L-33, L-17, L-16, L-7, L-8** |

**Done when:** §4.4 of the guide is empty except for items with an owning decision.

### Wave C — the flow TDC described *(needs D-1, D-3, D-5)*

| Slice | What | Size | Closes |
|---|---|---|---|
| **C1** | **Two-stage approval** — publish a Manager → HR definition with `PreventInitiatorApproval`, for requests, plans and encashments. Configuration + a seeder change | M | **R-4, L-5** |
| **C2** | **Send back a REQUEST with suggested dates** — three fields on `LeaveRequest`, a `ChangesSuggested` status, `suggest-changes` and `respond-suggestion` endpoints, the adapter arm, both screens. Mirror `LeavePlanService` exactly | **L** | **R-3, D-1** |
| **C3** | **Plan → request** — a *Raise the request* action on an approved plan, pre-filled from it, stamping `LeavePlanId` | M | **L-9, R-1** |
| **C4** | **Amend an approved request** — a reschedule path that keeps the number and the history, re-opens the approval, and records who moved it and why. Plus the "still going?" confirm action from D-4 | **L** | **R-8, R-7** |

**Done when:** an employee can propose, be sent back, answer, be approved by two people, and have
the dates moved once — all without anyone re-keying a request.

### Wave D — the joins

| Slice | What | Size | Closes |
|---|---|---|---|
| **D1** | **Leave → attendance.** On approval, write `StaffDailyAttendance` rows at `OnLeave` carrying `LeaveRequestId`, for each chargeable day; reverse on cancel. ⚠ Interacts with the attendance-rate denominator open question | **L** | **L-27** |
| **D2** | Sub-type cap enforced **per request** (per D-2), and `MedicalExpenseClaim.LeaveRequestId` written from the medical side | M | **L-28, L-37** |
| **D3** | Register the encashment money event in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`; align the daily-rate basis per D-7. **No GL posting** | S | **L-21, X-1** |

### Wave E — the features that make it feel finished *(needs D-9)*

| Slice | What | Size | Closes |
|---|---|---|---|
| **E1** | **The leave calendar** — one component, three entry points (mine / team / organisation), coloured by leave type, month and list views, holidays shown underneath | **L** | **R-5, L-36** |
| **E2** | **`LeaveReminderService`** — following the eleven-service house pattern exactly (`LeaveReminderLog`, unique `(TenantId, DedupeKey)`, claim-then-publish, `IAppEventBus`). Candidates: upcoming leave start, approved leave not closed after its end date, mandatory leave outstanding at N months, carry-over expiring, a request pending longer than N days | **L** | **R-6, R-10, L-23** |
| **E3** | **Leave register + exports** — an org-wide request list with real filters, plus CSV from the register, the balances screen and the compliance screen | M | **L-6, L-18, R-11** |
| **E4** | Bulk approve/reject on the queue, looping the real service call per item | M | **R-12** |

### Wave F — prove it

| Slice | What | Size | Closes |
|---|---|---|---|
| **F1** | **`dev-harness/hr-leave`** — every write endpoint, every refusal named in the guide, both personas, run twice green. ⚠ Two-actor rule: HR bypasses its own guards, so every self-or-permission check needs a non-HR actor | **L** | **V-1, D7** |

**F1 is not optional and it is not last in importance.** Everything in this plan was derived by
reading code. Until the harness runs, "fixed" means "looks fixed".

---

## 6. Explicitly out of scope

So nobody widens this later without saying so:

| Not doing | Why |
|---|---|
| **Any GL posting for leave encashment or leave liability** | HR records the money event; one Finance sweep posts everything after the module. Backlog rows 23/24 |
| **Payroll's treatment of unpaid leave** | payroll is another developer's module. D-6 raises it, it does not build it |
| **Scheduling the year-end runs** | already decided: they move balances rather than raise reminders, so automating them is TDC's policy call (V-4) |
| **Per-employee work schedules in leave day-counting** | `HrWorkingDayCalculator`'s own reasoning applies — a policy is a property of the process, not of the person |
| **Compassionate-leave offset / leave advances** | needs a design call first (R-13); it is a new concept, not a gap in an existing one |
| **A public, unauthenticated leave surface** | would be the application's first, and needs its own security review |

---

## 7. Traceability — where each requirement is answered

| TDC's requirement | Slice | Decision |
|---|---|---|
| Staff submit suggested leave dates | built | — |
| Supervisor reviews for approval | built, but role-routed | C1, **D-3** |
| Send back for correction with suggested dates | **C2** | **D-1** |
| HR gets the final leave dates | **C1** | **D-3** |
| Leaves listed | **E3** | — |
| Leaves in a calendar | **E1** | **D-9** |
| Leave due notices / alerts | **E2** | D-4 |
| Confirming upcoming leave will be observed | **C4 + E2** | **D-4** |
| Shifting leave after planning | **C4** | **D-5** |
| Holidays | built; **B2** for the ignored settings | D-10, and TDC still owes the gazetted list |

---

## 8. Related documents

| Document | For |
|---|---|
| `docs/HR/HR-LEAVE-SYSTEM-GUIDE.md` | the screen-by-screen source walk this plan is built on. **§4.4 is the ghost-setting trace; §1.4 is the accrual arithmetic** |
| `docs/HR/HR-WORKFLOW-ENGINE-INTEGRATION.md` | the recipe and traps for C1–C4 |
| `docs/HR/HR-REPORTS-CATALOGUE.md` §3.3 | the four leave reports, for E3 |
| `docs/HR/HR-BULK-OPERATIONS-CATALOGUE.md` §4.1 | how to build E4 safely |
| `docs/HR/HR-VERIFICATION-HARNESS-GUIDE.md` | environment traps and fixture conventions for F1 |
| `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` | the holiday calendar, the daily-rate basis and the attendance denominator are already logged there — **add D-1 … D-8 rather than starting a new list** |
| `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` | rows 23/24, for D3 |
| `docs/HR-CLOSURE-LEDGER.md` | record every decision from §4 in section A when it is answered |
