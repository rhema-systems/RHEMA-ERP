# HR Leave Management — Closure Plan

**Status:** ✅ **COMPLETE 2026-09-17.** All six waves built and verified — 192 harness assertions,
green twice (see § 0). It is
the complete definition of what remains before the leave module can be called done, written from a
screen-by-screen source walk (`HR-LEAVE-SYSTEM-GUIDE.md`) plus a field-level trace of every
configurable setting and a requirements pass against what TDC has asked for.

**Owner:** whoever picks up leave next. **Read this before writing any code**, and read § 0 first —
it is the only part of this document that describes the code as it stands today.

---

## 0. Progress — what is built, and what that changed

| Wave | Slices | State |
|---|---|---|
| **A — stop the bleeding** | A1, A2, A3 | ✅ **DONE 2026-09-17** |
| **B — make the screens honest** | B1, B2, B3 | ✅ **DONE 2026-09-17** |
| **C — the flow TDC described** | C1–C4 | ✅ **DONE 2026-09-17** |
| **D — the joins** | D1–D3 | ✅ **DONE 2026-09-17** |
| **E — the features that finish it** | E1–E4 | ✅ **DONE 2026-09-17** |
| **F — prove it** | F1 | ✅ **DONE 2026-09-17 — 192 assertions, green twice** |

**Two migrations were added:** `20260917155507_AddLeaveRequestSuggestionAndReschedule` (eleven
columns on `LeaveRequests`) and `20260917163710_AddLeaveReminderEngine` (two tables). Both rewritten
as guarded SQL and registered in `FastBuildMigrationMetadata`.

### What waves A and B closed

**A1 · L-1** — the desk's *Submit request* now calls `POST /Leaves/{id}/submit` after a non-draft
create, with the portal's honest "Saved, but not submitted" fallback when only the workflow hand-off
fails. Before this the request landed at `Pending` with no workflow instance: unsubmittable,
unapprovable, days already deducted, Cancel the only exit.

**A2 · L-2** — *Mark as paid* worked at all. ⚠ **Fixed deeper than the plan said.** The plan called
for a one-word screen change (`user?.id` → `user?.employeeId`); instead `ProcessedByEmployeeId` is
now stamped **server-side** from the token and **removed from `ProcessLeaveEncashmentDto`**, because
this is the same defect finish-plan lane 4 fixed for adjustments and plans, and the ledger's own
lesson there was that a screen-only fix leaves the hole open to every other caller. An unlinked
account is refused with a message rather than a constraint failure.
**Callers must drop `processedByEmployeeId` from the body** — `hr-demo-smoke/021` already has.

**A3 · L-3, L-14** — accrual is computed on the org-wide balances read, so the **Accrued** column
stops repeating Entitled. Beyond the plan: `AccruedAvailableDays` is now on the balance DTO — the
figure the create check *actually enforces* — and the create check, both request forms and the
balances screen all read **one** definition of it (`LeaveService.EnforcedAvailableDays`). The forms
lead with "can be taken now" and explain the difference when the two figures disagree. This is the
answer to § 2.2's first trap: the two numbers can no longer drift, because there is only one.

**B1 · L-11, L-34, L-35** — the five tab deletes and the register's retire button are gated on
`HR.Leave.Admin` (`useLeavePermissions`), which the HR role does not hold; `ResourceCollectionTab`
grew an `allowRemove` prop for it. Retired sub-types leave the pickers (`GET …/sub-types?activeOnly=true`;
the rulebook tab still shows them). **`LeaveType.IsActive` and `LeaveSubType.IsActive` are now
enforced in the service**, on create and on moving a draft onto a retired type — not just by the
picker.

**B2 · L-31, L-32** — leave no longer runs its own holiday query. `IHrWorkingDayCalculator` gained
`GetHolidayDatesAsync`, and leave counts chargeable days against it, so leave and HR's statutory
clocks share one answer to "is this a holiday". Leave therefore now honours the **default calendar**,
**`PublicHoliday.IsActive`** (which the calculator itself was also missing) and **`SubstitutionDate`**.
Leave keeps its own per-type `CountWeekendsAsLeave` / `CountHolidaysAsLeave` switches — the shared
calculator is Monday-to-Friday by design and those switches are leave's, not its.

**B3 · L-29, L-33, L-17, L-16, L-7, L-8** —
· **Pro-rate on exit** is wired: a leaver stops accruing on their last day, mirroring
`ProRateOnJoin`. ⚠ It applies to **incremental accrual only**, exactly as its twin does; a
`FullGrantOnEligibility` policy is unaffected, because a full grant that is reduced is not a full
grant. If TDC wants a leaver's full grant scaled, that is a different setting.
· **`ObservanceType` is wired**: an **Optional** holiday is a working day, so taking it is leave like
any other. Mandatory and Substitute Day close the office. ⚠ This narrows an assumption other modules
may hold — flagged to payroll.
· **`AttractsHolidayPay` / `HolidayPayMultiplier`** stay recorded and are now *labelled* on the
holiday form as payroll's to apply, and raised in `docs/HR/integration/handoffs/HANDOFF-PAYROLL-LEAVE.md`. They are not
ghosts any more; they are somebody else's inputs.
· A plan's **`Year` is derived from its start date** server-side and removed from the create DTO, so
a January plan raised from the December list no longer vanishes from both.
· The plan dialog has a **sub-type field** (hidden when the type has none).
· The requests list filters **status server-side**, so the count and the paging agree with it.
· `/hr/leave/requests?employeeId=` is honoured, and the picker names whoever it is pointed at.

### What waves C, D and E closed

**C1 · R-4, L-5** — the three leave entity types now seed a **two-stage** ladder (line manager →
HR confirmation) via `EnsureLeaveWorkflowsSeededAsync`. ⚠ **`PreventInitiatorApproval` is
deliberately FALSE**, against this plan's own D-3. That flag guards the *initiator*; a leave
record's conflicted party is its *subject*, and HR raises leave for other people from the desk — on
a one-HR-user tenant (the demo tenant is exactly that) the flag strands every desk-raised request at
stage 2 with nobody able to clear it. Instead all three leave services refuse an approval by the
employee the record is about. **Consequence: one approve call no longer finishes a request.**

**C2 · R-3, D-1** — `suggest-changes` and `respond-suggestion` on a REQUEST, mirroring the plan's,
with `LeaveStatus.ChangesSuggested`. Sending back cancels the live approval and re-derives the
balance (the days stop being reserved); answering re-submits through the front door.

**C3 · L-9, R-1** — `LeaveRequest.LeavePlanId` is finally written. "Raise the leave request" on an
approved plan, on both desk and portal, pre-filling dates and relievers; the server checks the plan
is the right employee's, approved and not already spent; both plan lists show the request raised.

**C4 · R-8, R-7** — `reschedule` keeps the number and the history and **re-opens the approval**
(D-5: an approval is an approval OF DATES). A reason is mandatory. `confirm-observance` records
"still going ahead". The date checks that re-run on a move are a deliberate subset — past, order,
overlap and balance, but NOT eligibility, service access or minimum notice; see the helper's remarks.

**D1 · L-27, the largest structural gap** — `LeaveAttendancePostingService` writes `OnLeave` days
carrying `LeaveRequestId` on approval and removes them on cancel or reschedule, so `DaysOnLeave` on
the monthly summary — which the payroll export reads — stops being zero. Three things it does that
matter: it posts **only** when the engine actually finished (a two-stage first approval leaves the
request Pending); it **never overwrites an observation** (a punched or annotated day is skipped and
counted); and reversal is a **HARD delete**, because the unique index on
`(TenantId, EmployeeId, AttendanceDate)` is not filtered on `IsDeleted` while `GetQueryable()` hides
soft-deleted rows — a soft delete would leave an invisible row holding that employee's slot.

**D2 · L-28, L-37** — the sub-type cap is enforced as an **ANNUAL** cap, not the per-request one
this plan proposed: a single-request check is defeated by splitting one request into two. Still one
balance pot per leave type, as D-2 decided. The medical claim dialog gained a "Related sick leave"
picker, so `MedicalExpenseClaim.LeaveRequestId` finally has a writer.

**D3 · L-21, X-1** — leave's two money events registered in
`HR-FINANCE-INTEGRATION-BACKLOG.md` §Area 2, with both open questions attached and an explicit
*"do not build 2.1 before L-D8 is answered"*. No GL posting, per the rule.

**E1 · R-5, L-36** — the calendar: one component, three entry points (`/me/leave/calendar`,
`/hr/leave/calendar` with a scope selector covering team and organisation). `Mine` and `Team`
resolve from the token, never from a query parameter. Holidays drawn underneath as a background, so
the reason a five-day leave charges four is visible. `CalendarColor` is real at last.

**E2 · R-6, R-10, L-23** — `LeaveReminderService`, the twelfth HR engine, following the house
pattern exactly. Five sweeps: leave starting soon and unconfirmed, leave that ended and was never
closed, a request nobody has decided, mandatory leave outstanding from month 9, carry-over about to
lapse. ⚠ **It warns and moves no balances** — `LeaveYearEndService` stays unhosted for the reason
V-4 records, and that is why this engine CAN be scheduled when the year-end cannot.

**E3 · L-6, L-18, R-11** — `/hr/leave/register`, org-wide with real filters, plus CSV from the
register, balances and compliance. The register query is shared by the paged read and the CSV so
they cannot disagree. ⚠ CSV cells starting `=`, `+`, `-` or `@` get a leading apostrophe — without
it a name beginning with one executes as a formula in Excel.

**E4 · R-12** — bulk approve/reject on the queue, capped at 50, looping the **real** per-request
service call. Cross-module defect #15 is exactly what a cleverer bulk path would reproduce. Uses the
bulk catalogue's §4.2 result shape, and the screen names the items that refused.

### What this did NOT change

**There is still no `dev-harness/hr-leave` suite (V-1)**, so everything above was verified by
reading, by type-checking and by two clean builds — **not by running**. Wave F1 remains the single
biggest risk in this plan, and "fixed" still means "looks fixed" until it runs.

### F1 — the harness, and what it found (2026-09-17)

**`dev-harness/hr-leave`: 192 assertions across four slices, green twice against the rebuilt API.**
V-1 is closed. The suite's own README carries the running recipe, the fixture conventions and the
eight traps it hit.

| Slice | Assertions | Covers |
|---|---|---|
| 1 · lifecycle | 75 | the two-stage ladder (C1), submit (A1), send back and answer (C2), reschedule and confirm (C4) |
| 2 · attendance | 31 | the leave → attendance join and its reversal (D1/L-27), the reconciler |
| 3 · guards | 33 | self-approval, retired types and sub-types, the annual sub-type cap, the engine assertion pair, permission tiers |
| 4 · reads | 53 | the two balance figures, calendar, register, three CSV exports, the queue, the reminder engine |

**It found five defects. ⚠ THREE WERE INTRODUCED BY THIS CLOSURE BUILD** — which is the class of
defect a code review does not catch, and the entire argument for having built the suite.

| # | What | Fixed by |
|---|---|---|
| **1** | ⚠ **A refused suggestion destroyed the approval workflow.** The gap-2 date validation ran AFTER the block that cancels the live instance, so an approver proposing invalid dates left the request at `Pending` with no instance — unapprovable, unsubmittable. **Exactly the L-1 shape wave A existed to fix, reintroduced by the fix for a different gap** | validation moved before the workflow branch |
| **2** | **`LeaveRequestDto` carried no `ApprovedById` / `ApprovedDate` / `RejectionReason`** — on the entity, on no DTO, so nothing could show who approved leave or when | added to the DTO and the mapper |
| **3** | ⚠ **The line manager could not READ the request they were assigned to approve.** Self-or-`HR.Leave.Read`, and `Manager` holds no HR permission — so the queue listed a row that 403'd on click. The gate's comment said *"deliberately NOT self-or-manager"*, which was coherent while the Manager stage was optional and stopped being so when C1 made it mandatory | `CanReadRequestAsync` gained a third arm: the current workflow assignee. Narrower than a role grant — one request, one person, only while it sits at their step |
| **4** | **The attendance reconciler was reachable from nothing but the 24-hour timer** — untriggerable, unprovable, and useless for repairing drift today | `POST api/hr/leave/reminders/reconcile-attendance` |
| **5** | *(pre-existing)* the employee-numbering change had silently broken the shared fixture helper, which minted its actor by supplying a staff number the system now issues itself | the helper identifies its actor by surname |

**What green does not prove**, recorded so nobody reads more into it than is there: no screen has
been rendered — there is no browser automation, and frontend verification remains `tsc` on a scoped
tsconfig; the reminder engine's 24-hour schedule and distributed lock are untested (only its
run-now endpoints are); and **L-D7 and L-D8 are deliberately unasserted**, because encoding a guess
about the daily-rate basis or about whether in-service encashment is permitted would turn an open
question into a fixed requirement.

---

### Closed after wave E, before the harness (2026-09-17)

**Both open gaps are now shut, and one more that wave C created.**

**The attendance invariant is convergent, not hook-driven.** `OnLeave` days exist for a request iff
its status is Approved/InProgress/Completed. Leave's four transitions call
`ReconcileAttendanceAsync`; the nightly sweep calls `ReconcileRecentAttendanceAsync` over requests
changed in the last 14 days. That closes the recall hole *without* enumerating doors — which matters
because the generic workflow recall calls the status adapter directly and never touches
`LeaveService`, and any list of hooks is a list somebody stops extending. **Drift the sweep repairs
is logged as a WARNING**, because it means a real bug upstream. ⚠ The posting service no longer
reads an ambient tenant: it takes one, so it behaves identically in a request and on a timer.

**An approver's suggested dates are validated when proposed.** `SuggestChangesAsync` now runs the
same date checks the employee faces. The accept path still re-checks — time passes, and another
request can eat the balance meanwhile.

**⚠ L-10 was upgraded and fixed, because C1 made it load-bearing.** The approvals queue was
"direct reports' Pending requests". Under the two-stage ladder the request stays `Pending` between
stages, so that query showed a manager what they had already approved and **never showed HR the
confirmation step at all** — HR is nobody's `ManagerId`, so the step C1 introduced had no inbox.
`GET api/Leaves/my-approvals` now asks the engine which requests the caller may actually decide;
the screen's manager picker is gone with it (it was also a way to read somebody else's queue). The
old endpoint survives for existing callers, marked superseded. ⚠ The queue asks the engine per
candidate, so it scans a capped 300 Pending requests.

**Still open and deliberately deferred until after F1:** L-12, L-13, L-15, L-19, L-20, L-22, L-24,
L-26 — all convenience gaps from the system guide, none of them correctness. **L-13 (the leave-type
PUT is a replace-set that silently unlinks every allowance) is the one worth doing first** of those.

**Superseded — these two were the open gaps, now closed above:**

1. **A request recalled through the generic workflow surface would strand its attendance days.**
   Recall goes through `WorkflowController` and calls the adapter directly, bypassing `LeaveService`.
   Not reachable today — recall applies to in-flight requests, which have no attendance days — but
   the hole is real. The fix is either a reconciliation pass or routing recall through the module
   service, and both are bigger than wave D.
2. **`RespondToSuggestionAsync` re-runs the balance check even when the employee simply accepts the
   approver's own suggested dates.** So an approver can propose a window the employee's balance will
   not carry, and the *employee* gets the refusal. Safe, but unkind; catching it at suggest-time
   needs the approver's dialog to read the employee's balance.

**Five reminder windows are assumptions, not TDC's numbers:** 7 days before a start, 2 days'
closure grace, 5 days undecided, month 9 for mandatory leave, 30 days for carry-over. Each is
documented at its constant.

### Decisions taken to get here

All eleven of § 4 are now recorded in `docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` as **L-D1 … L-D8** (the
three marked *recommend* were taken as recommended and need no TDC answer). Six are **built to the
recommendation** so the module is not left half-finished waiting; TDC can still overturn them.
**L-D7 (the daily-rate basis) and L-D8 (whether in-service encashment is permitted at all) are
deliberately NOT built** — both readings are live in the product at once, and guessing would ship a
third.

---

## START HERE — the fresh-chat briefing

If you are starting a new session on the leave module, read these four things in this order and
nothing else first:

| # | Read | Why | Time |
|---|---|---|---|
| 1 | **This document's §1, §2 and §3** | what "done" means, what is already built, and the gap register | 15 min |
| 2 | `docs/HR/areas/leave/HR-LEAVE-SYSTEM-GUIDE.md` — the **two rules** block above chapter 1, then **§1.4** and **§4.4** | the two demo-blocking defects, the accrual arithmetic, and the eleven ghost settings | 20 min |
| 3 | **§4 of this document — the eleven decisions** | you cannot scope half the slices without answers; six need TDC | 10 min |
| 4 | `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | slices C1–C3 all touch the engine, and the recipe has traps | 10 min |

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

### 3.3b Raised after the closure build (2026-09-17, by the module owner)

Two real requirements that waves A–E did not cover, recorded here so they are not lost. **Both are
features, not fixes**, and both were deferred until after F1.

> ▶ **Both have since moved to `HR-LEAVE-RESIDUE-CLOSURE-PLAN.md`, and R-14 is BUILT** (2026-09-17,
> slice G1 — 51 assertions in `dev-harness/hr-leave` slice 5). Read that plan's § 0 for what the
> code now does; the sketch below is what was proposed, not what was built. **The one deviation
> worth knowing:** the fix is in `PostAsync`, which now prunes days outside the request's current
> range, not in `ReverseAsync` as sketched below — so the invariant converges on the day set and no
> future caller has to remember the rule. R-15 is planned there too, in full, with the medical board
> as a Medical-module record.

| ID | What | Why it is not covered by anything built |
|---|---|---|
| **R-14** | **Recall from leave — an employee called back before their end date.** | There is **no correct action** for this today. *Cancel* releases every day including the ones already taken; *Close* refuses before the end date; *Reschedule* records that the leave moved, which is a different fact. The only route is cancel-and-re-key a shorter request, losing the number and the approval. ⚠ **And nothing writes `LeaveStatus.InProgress`** — every reference in the solution reads or filters on it and no code path sets it, so the system has no notion of leave that is currently happening, which is the precondition for recalling somebody from it |
| **R-15** | **Excuse duty, and a medical board recommendation for extended sick leave.** | Raised by stakeholders. **Nothing exists** — no certification rule, no typed evidence, no board. Sick leave is an ordinary leave type with optional untyped attachments |

#### R-14 — what curtailment has to be, and the defect it exposes

The enterprise shape is **curtailment**, and it is deliberately not an amendment:

- it **truncates** rather than cancels — days up to the recall date stand as taken, days after are
  restored to the balance;
- it is the **employer's** act, recorded as its own event (who, when, why, effective when), because
  the consequences differ from an employee changing their mind: restored days are often protected
  from the normal carry-over expiry, and recall costs may be reimbursable;
- the record **keeps its number and its approval** — the leave was validly approved and then
  interrupted, not wrongly granted.

Sketch: `RecalledOn` / `RecalledById` / `RecallReason` / `DaysRestored` on `LeaveRequest`, a
`PUT /{id}/recall` taking an effective date, `EndDate` truncated and `TotalDays` recomputed — the
balance then re-derives itself, because `UsedDays` comes from approved requests. Plus a small job
that sets `InProgress` when leave starts, so the action is offered only where it means something.

> ⚠ **A defect in what wave D already built, which only curtailment exposes.**
> `ReconcileAttendanceAsync` posts when a request counts as taken and reverses when it does not —
> but `PostAsync` only **adds**. Shortening `EndDate` while the status stays `Approved` would leave
> the now-out-of-range attendance days behind. Reschedule is safe because it drops to `Pending`
> first, so the reverse runs. **`ReverseAsync` needs to remove days outside the request's current
> range**, and that fix belongs in the same slice.

#### R-15 — where excuse duty and the medical board would live

⚠ **The thresholds below are inferred from general Ghanaian practice, not from TDC.** Logged for
them as **L-D10**; do not build to these numbers without an answer.

| Piece | Where it belongs |
|---|---|
| `RequiresMedicalCertificate` + the self-certification threshold *(commonly 2–3 days)* | **leave type** — it is a rule about a kind of leave |
| `MedicalBoardThresholdDays` — cumulative sick days in a year beyond which a board must sit | **leave type** |
| The certificate itself | **leave attachment, but typed** — `ExcuseDuty` vs `MedicalBoardRecommendation` — so the gate can check the right one is present, which an untyped file cannot |
| The board, its sitting, its members, its recommendation | **the medical module**, referenced from leave — the bridge-by-reference pattern SHE↔Medical already uses |
| A "retire on medical grounds" outcome | **separation**, which already has retirement |

Enforcement point is **submit**: a sick-leave request over the threshold without the required
evidence is refused, naming what is missing — the same shape as the eight existing create checks.

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
| **D-6** | **What should `IsPaid = false` actually do (L-30)?** | Needs the **payroll owner**: HR records the leave and the days, payroll decides the deduction. Raise it as a `docs/HR/integration/handoffs/HANDOFF-PAYROLL-*.md`, do not guess | **payroll owner** |
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
| **D3** | Register the encashment money event in `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`; align the daily-rate basis per D-7. **No GL posting** | S | **L-21, X-1** |

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
| `docs/HR/areas/leave/HR-LEAVE-SYSTEM-GUIDE.md` | **Rewritten 2026-09-17 after this build**, so it now describes the module as it stands rather than as the evidence this plan was drawn from. §4.4 is the setting trace (nine of the eleven ghosts are wired); §1.4 is the accrual arithmetic; **§23 is the original 37 findings with their current state** |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | the recipe and traps for C1–C4 |
| `docs/HR/catalogues/HR-REPORTS-CATALOGUE.md` §3.3 | the four leave reports, for E3 |
| `docs/HR/catalogues/HR-BULK-OPERATIONS-CATALOGUE.md` §4.1 | how to build E4 safely |
| `docs/HR/operations/HR-VERIFICATION-HARNESS-GUIDE.md` | environment traps and fixture conventions for F1 |
| `docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` | the holiday calendar, the daily-rate basis and the attendance denominator are already logged there — **add D-1 … D-8 rather than starting a new list** |
| `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` | rows 23/24, for D3 |
| `docs/HR/programme/HR-CLOSURE-LEDGER.md` | record every decision from §4 in section A when it is answered |
