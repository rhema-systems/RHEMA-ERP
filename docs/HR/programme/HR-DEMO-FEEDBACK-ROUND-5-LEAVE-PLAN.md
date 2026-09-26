# HR demo feedback, round 5 — Staff Leave (`HR Demo Changes 180926.pdf`)

> **Status (2026-09-26): DECIDED, in build — M0 and lanes E, F, D, A, N, C, G and H done.** Drafted 2026-09-24 and reviewed the same day (six
> design errors, about ten half-answered bullets). On 2026-09-25 the user took decisions **A1–A7** and
> **B1–B7**, Ghanaian law and public-service practice were researched, and every leave-type setting was
> audited against the code. This version folds all of that in. **It cuts the anniversary leave year,
> in-service encashment and automatic forfeiture**, and spends the effort on making what exists honest
> and lawful.
>
> Plain-terms companion for stakeholders, kept in step with this plan:
> `docs/HR/areas/leave/HR-LEAVE-ROUND-5-WHAT-CHANGES.md`. **This file is the live record** (lane M0,
> created 2026-09-25 from the planning draft); § 8 at the end is the execution log.
>
> | Lane | State |
> |---|---|
> | **M0** | **DONE** 2026-09-25 — this file |
> | **E** | **DONE** 2026-09-25 — plans: relievers, the clash check, the approver's view · `run-round5-e.mjs` 119, green twice · § 8 |
> | **F** | **DONE** 2026-09-25 — the calendar for one employee, and a unit with everything beneath it · `run-round5-f.mjs` 20, green twice · § 8 |
> | **D** | **DONE** 2026-09-25 — cancel, recall by the line manager, coming back to work · `run-round5-d.mjs` 74, green twice · § 8 |
> | **A** | **DONE** 2026-09-25 — leave kinds (Annual, Maternity, Other) and a form that starts from the kind · `run-round5-a.mjs` 89, green twice (79, then 10 for the L-59 follow-up) · § 8 |
> | **N** | **DONE** 2026-09-25 — settings that do what they say: the audit's ghosts, misleading settings and bypasses; the demo data; four more defects found · `run-round5-n.mjs` 92, green twice · § 8 |
> | **C** | **DONE** 2026-09-25 — accrual: a period counts on its last day; the accrual statement; leave owed as at a date; a leave year that does not start in January · `run-round5-c.mjs` 112, green twice · § 8 |
> | **G** | **DONE** 2026-09-25 — the year-end, executed for real and fixed: expiry keeps what was taken in time, the reminder agrees with it, carry-over never moves lapsed days, not before the year ends, one pot per type · `run-round5-g.mjs` 55, green twice · § 8 |
> | **H** | **DONE** 2026-09-26 — casual leave beyond its limit: the ask, the split at the final approval, and the two parts kept as one absence; a leak found and closed · `run-round5-h.mjs` 142, green twice · § 8 |
> | J · I · K · L | not started, in that order — **J is next** |
> | M1–M4 | M1, M3, M4 done 2026-09-25 (explainer rewritten, TDC questions, memory); M2 per lane |
> | K-II | waits on TDC (R5-Q5) |

## Context

The staff-leave demo produced 27 bullets: questions ("what does Leave Year Starts do?"), rules the
stakeholders hold ("annual leave on the anniversary", "approved leave cannot be cancelled, only
recalled"), defects they met (plan relievers not populated, the approver can edit the whole plan,
"does the clash check work?"), and asks (set casual off against annual, calendar per employee, medical
board beyond excuse duty and for several employees, Finance computes the encashment, HOD/HR confirms a
resumption, due-for-leave reminders, an accrual utility). Every bullet is accounted for below as a
lane, a decision or a documented answer. If a bullet is missing, that is an error in this plan.

**The principle this version adds: simple for users, lawful, and nothing a user can set that does
nothing.** The stakeholders were told the module is simple, and it has become complicated. The rules
TDC needs are simple (next section); the complexity came from configurability for any client and from
taking every remark literally. So this round removes before it adds.

## Ghanaian law and practice (researched and verified 2026-09-25)

TDC is **TDC Ghana Limited** (a company since 2017, renamed December 2023). The **Labour Act 2003
(Act 651)** binds it in full. The Public Services Commission's *HR Policy Framework and Manual* (2015)
is the benchmark its conditions of service are likely modelled on, but does not bind it. **TDC's own
conditions of service / collective agreement governs, and we do not have it** (question R5-Q1).

| Rule | Source | Consequence here |
|---|---|---|
| ≥ 15 working days "in any calendar year of continuous service"; a leaver gets leave "in proportion to the period of service in the calendar year" | Act 651 s.20(1), s.30(1) | The calendar year is the law's frame → **A1** |
| No waiting period in the Act; public service: leave "must be earned … calculated from the date a public servant starts work", pro-rata after 6 months | s.20; PSC 4.25.1.1, 4.25.1.2 C | The joiner rule is TDC's → **A2**, R5-Q1 |
| "Any agreement to relinquish the entitlement to annual leave or to forgo such leave is void"; public service pays accrued leave only "at the end of one's service" | s.31; PSC 4.25.1.2 I | **A3** — exit only (and TDC's FR-HR-046) |
| No leave pay on dismissal without notice | s.30(3) | **B5** |
| Recall only "in cases of urgent necessity"; the remainder is kept; the employer bears reasonable expense | s.25, s.26 | **B1**, D2 |
| Certified sickness during annual leave is not computed as part of the leave | s.24 | Documented procedure (not built) |
| Unused leave "forfeited, unless it is rescheduled or deferred with the written approval of Management"; at most two years' accumulation | PSC 4.25.1.2 G, I | **B7** |
| Maternity ≥ 12 weeks on full pay, in addition to annual leave; ≥ 2 more weeks for an abnormal or multiple birth; certified extensions | s.57 | The Maternity kind → **A4** |
| Casual leave ≤ 10 working days a year, for urgent private affairs; excess from annual leave | PSC (casual leave); Admin Instructions 317–319 | **A5** |
| Incapacity % and compensation are Workmen's Compensation concepts (injury on duty); no pay cut during treatment | PNDCL 187 s.2 | **A6** step two; the INJ leave type |
| Holidays changed in 2025: 1 July restored, Founders' Day 21 September (4 August dropped), Shaqq Day added, midweek holidays movable | Public Holidays and Commemorative Days (Amendment) Act 2025 (reported by GBC, MyJoyOnline) | N4 data fix; R5-Q4 |

Sources: Act 651 (https://www.gipc.gov.gh/wp-content/uploads/2023/05/LABOUR-ACT-2003-ACT-651.pdf —
sections 20–31 and 57 checked against the text); PSC Manual 2015
(https://psc.gov.gh/wp-content/uploads/2022/01/HR-POLICY-FRAMEWORK-MANUAL.pdf — quotes checked); Civil
Service Administrative Instructions (draft, 2020); PNDCL 187 (melr.gov.gh); *Adrah v ECG* [2018] via
Ghana Law Hub. Memory: `ghana-leave-law-reference`.

## Decisions

Taken by the user on 2026-09-25 unless marked. The four decisions of 2026-09-24 are shown with what
became of them.

| # | Decision |
|---|---|
| R5-D1 | ~~Build anniversary leave-year mode~~ — **reversed by A1.** |
| R5-D2 | Keep live accrual; no posting job; build the accrual statement — **kept** (lane C). |
| R5-D3 | Cancel rule — **kept, amended by B2**: the employee cancels Draft / Pending / ChangesSuggested; HR may cancel approved leave through its first day, reason required; after that, recall only; Completed, never. |
| R5-D4 | ~~In-service encashment with a Sent-to-Finance stage~~ — **replaced by A3.** |
| **A1** | **No anniversary leave year.** The company (calendar) leave year stays; the hire date decides when someone qualifies and starts earning. |
| **A2** | **Keep today's joiner configuration** (ANN: access after 12 months; accrual `MinServiceMonths` 12) and drop step C3. If TDC's rule is "earn from day one", add the first-rollover protection (C3′). |
| **A3** | **Encashment on exit only.** In-service off on the demo; "HR decides the days, Finance the money" applies to the leaver's settlement; no Sent-to-Finance stage. TDC's answer to L-D8 is still owed. |
| **A4** | **Kinds: Annual, Maternity, Other.** Annual absorbs `MandatoryAnnualLeave`. Maternity carries its statutory rules (lane A3). Sick leave is Other: its behaviour already lives in its evidence settings. *(Maternity included now — the user's clarification, 2026-09-25.)* |
| **A5** | **Casual beyond its limit = two linked requests** (casual days + annual days for the excess). The employee may ask on the portal; HR confirms. |
| **A6** | **Medical board in two steps.** Step one now: purpose (including injury on duty), physicians as members, documents, cancel/dissolve, a relevant-and-recent gate, the board defects. Step two after TDC: several employees per board, incapacity %, compensation. |
| **A7** | **"Leave owed as at a date" report**, days only; Finance values it. |
| **B1** | The employee's line manager (HOD) or HR may recall, reason required. The remaining days are kept (as today); expenses go through a claim. |
| **B2** | HR may cancel approved leave up to and including its first day. |
| **B3** | Early return only with the HOD's approval (unused days restored). A late return is flagged as absence without leave for HR and payroll; nothing is charged automatically. |
| **B4** | A request raised from an approved plan keeps both approvals; approvers see "matches the approved plan". |
| **B5** | The leaver's settlement pays annual leave only: the calendar year's pro-rata share plus unexpired carried days; nothing on summary dismissal. |
| **B6** | One "annual leave not yet planned or taken" reminder to employee, manager and HR, skipping anyone not yet eligible; plus "you can now take annual leave". |
| **B7** | Carry-over: the automatic cap and expiry (fixed). HR records an approved deferral beyond the cap as an adjustment. Forfeiture off for TDC. |

## Questions for TDC (added to `HR-OPEN-QUESTIONS-FOR-TDC.md`, § Round 5, on 2026-09-25)

| # | Question | Interim default |
|---|---|---|
| R5-Q1 | The conditions of service / collective agreement: days by grade, the first-year rule, carry-over, casual and compassionate days, sick-pay tiers and when a board sits, maternity weeks | Demo figures; A2 |
| R5-Q2 | In-service encashment (L-D8) and the daily-rate basis (L-D7) | Exit only; rates unchanged |
| R5-Q3 | Is a leave allowance or salary advance paid when staff proceed on annual leave? | None |
| R5-Q4 | Who keeps the holiday calendar current (the 2025 changes; holidays the President moves)? | HR; demo list updated (N4) |
| R5-Q5 | How TDC's medical board works (composition, trigger) and how injury-on-duty cases are handled | Step one only |

## What exploration found (verified in code 2026-09-24/25, branch `hrdev`)

**The draft's findings — all confirmed by the review:**

- The plan form's live reliever clash check **always says the reliever is free**: `leavePlanService.getRelieverClashes` passes `{ params: {...} }` to `apiService.get`, which serialises it as `?params=[object Object]`; the API returns 400 "A reliever is required", and the component treats the error as "no clashes" (`frontend/src/services/hr/leave.service.ts:429-436`, `frontend/src/app/hr/leave/plans/page.tsx:111-127`). The global retry rule never skips a 4xx either (`frontend/src/lib/react-query.tsx:19` reads `error.response.status`; `api.service.ts:337-339` sets `error.status`).
- Plans never prefill relievers from the profile roster (`EmployeeReliever`); only requests do (`LeaveService.AutoFillRelieversAsync`). The plan pickers are free search with no `initialLabel`, so on Edit they look blank. The portal planner has no relievers at all, and an employee's Draft edit wipes HR-set relievers and sub-type (replace-set, `LeavePlanService.cs:187-197`).
- There is **no plan detail modal**: what the stakeholders called one is the generic "Edit leave plan" dialog, fully editable on every status, which fails on save for anything but Draft (`LeavePlanService.cs:177-178`). The actions live only in the row "⋯" menu. "Cancel plan" is offered on Approved plans, takes no reason and does not cancel the workflow instance; "Decline suggestion" sends no dates and always fails (`:370-371`).
- An employee can cancel their own **Approved and even In-progress** request (`LeaveService.CancelLeaveRequestAsync`, `:2056-2087`, refuses only Cancelled/Completed).
- There is **no resume step**; Close is HR-only (`LeavesController.cs:1200-1201`), sets Completed, records no closer and no return date.
- The calendar has **no employee filter** (the API has no `employeeId`; the page does not pass the component's `organizationUnitId`).
- Accrual is computed on read, never stored. **Off-by-one**: the as-of date is clamped to the last day of the year and a period is credited only once fully elapsed, so monthly accrual reaches 11/12 within the year (`LeaveEntitlementService.cs:198-203`, `:357-374`).
- Year-end: forfeiture never sets `IsDryRun` (`LeaveYearEndService.cs:194`); expiry zeroes carried days even when used (`:211-225`); the carry-over target lookup ignores `LeaveSubTypeId` (`:134-139`); the source year is never reduced; nothing stops carry-over for a year that has not ended. `ProRateFirstYearEntitlement` counts calendar months under a non-January year (`LeaveEntitlementService.cs:146-157`).
- Reminders: five sweeps, **HR role only, in-app only** (`LeaveReminderService.cs:285-301`); the settings' XML docs claim employee/manager/approver recipients that do not exist. No "due for leave" kind.
- Medical board: one `EmployeeId` per board, a free-text `Reason` only, no attachments, no disability/compensation fields; the member dialog never offers the physician register although the API accepts `physicianId`.
- Encashment: the server computes the amount once, at request time; "Mark as paid" posts the journal to Finance (`LEAVE_ENCASHMENT_PROCESSED`). The portal sends the calendar year. The guard reads the whole-year `AvailableDays`, not *can take now*.

**Found by the review (2026-09-24):**

- `MandatoryAnnualLeave` **already exists** and drives the compliance register (`LeaveService.cs:1922-1951`) and reminder sweep 4 (`LeaveReminderService.cs:421-457`). Sweep 4 ignores the service gate (it nags joiners who cannot take leave) and compares the calendar month (`:426`).
- **The first day of leave**: recall refuses an effective date on or before the start date and says "cancel instead" (`LeaveService.cs:1019-1022`), while the nightly sweep may already have moved the request to InProgress (`:2241-2268`).
- **Plan approvers cannot reach the plans page**: LEAVE_PLAN's stage 1 is the line manager (`DatabaseSeedingService.cs:605-608`), but `/hr/leave/plans` needs `HR.Leave.Read`, held only by HR and admin roles (`HrPermissions.cs:827-852`); the approvals-inbox link `?planId=` (`WorkflowEntityDisplayService.cs:173`) is never read; Approve/Reject are offered on ChangesSuggested and return 403; plan approve/reject have no status check (`LeavePlanService.cs:252-298`).
- Create refuses any request over the accrued balance (`LeaveService.cs:331-337`), so an over-limit casual request can only come from the desk.
- Recall is HR-only (`LeavesController.cs:1029-1030`).
- Encashed days are held back only once Processed (`LeaveBalanceRecalculationService.cs:172-180`).
- **The exit settlement sums every balance of every leave type and every year**, whole-year figures, capped at 56 days, whatever the separation reason (`SeparationService.cs:2234-2249`).
- The sick-leave board gate accepts **any** concluded board about the employee, whatever its purpose or date (`LeaveService.cs:684-691`). A board can conclude with zero members; an omitted Outcome binds to 0; facility and exam ids are stored unchecked, so a bad one is a 500.

**Found by the settings audit (2026-09-25)** — three ghosts, about twelve misleading settings, three
bypasses. The configuration register and the guide both said "zero ghosts" (first corrections made
2026-09-25).

- **Ghosts:** `IsPaid` (no server reader — payroll's L-D6); the tenant `EncashmentWorkingDaysPerMonth` (the type's own divisor always wins and the form's minimum is 1, `EmolumentService.cs:492-498`); `LeaveAccrualPolicy.IsActive` (no DTO field, no control — a policy can only be deleted).
- **Misleading:** `MaxDaysPerYear` only lowers the entitlement (`LeaveEntitlementService.cs:163-164`), so a type with Default 0 / Max 90 (UNPAID, INJ) can never be booked; accrual frequency falls one period short in-year, `Annual` accrues 0 all year, `PerPayPeriod` is Monthly; `CarryOverExpiryMonths` wipes carried days already used; `ForfeitUnusedAfterMonths` affects no leave-taking (a closed year cannot be booked) — it only closes a cash window; eligibility rules are OR'd (`LeaveTypeService.cs:684`) under a "restrict" label, and the gender qualifier on an organisation rule cannot be set from the tab; `HasSubTypes` only blocks creating sub-types; a sub-type's cap replaces the whole entitlement for a request carrying it (`LeaveEntitlementService.cs:92-97`); `LeaveCategoryAllocation.LeaveSubTypeId` almost never applies; `ProRateOnExit` changes a figure no payout reads; `MandatoryAnnualLeave`'s doc comment claims the forfeiture run reads it.
- **Bypasses:** save-as-draft then submit skips minimum notice, the reliever requirement, the sub-type cap and the balance check — submit re-checks only the medical evidence (`LeaveService.cs:516-580`), and the request form tells sick-leave users to take that route; the board threshold ignores Pending requests (`:663-665`); reschedule, suggested dates and the counter-proposal can lengthen a request without re-running the evidence gate.
- **Demo data:** UNPAID and INJ can never be booked; SICK's description promises a certificate rule that is off; MAT's 30-day notice would refuse a premature birth; the holidays are the 2019 list (`src/ErpSystem.Data/Seeders/TdcDemoLeaveCalendarSeeder.cs:370-389`: Founders' Day 4 August, no 1 July, no Shaqq Day); scenario 020's opening adjustments double ANN / SICK / CAS (the guide tells this as the go-live migration story); nobody reaches their annual allocation within the year (13.75 / 19.25 / 27.5 of 15 / 21 / 30 — the off-by-one).

## Traceability — every PDF bullet → where it lands

| PDF bullet | Where |
|---|---|
| "Leave Year Starts" — what, why, can it go, impact on the calculation | M (answer: the company leave-year boundary — January, the calendar and financial year; it decides which year a day counts against and when carry-over and expiry fall; the first-leave date comes from the hire date and the service rule) |
| Annual leave on the anniversary; 12 months qualifying; 2 days a month; plan after 12 months | A1 + A2 + C1 (the hire date drives qualification and first earning; the year is the calendar year) |
| How forfeiture after unused (months) works | G + M (answer) + B7 (off for TDC) |
| Carry-over agrees with the financial year; encashment provision | A1 (calendar year = financial year) + C6 (A7) |
| Before the 2nd anniversary, only accrued days | the existing accrued guard (unchanged) |
| Spread annual leave across months | E6 + M |
| Annual treated specially; maternity marked; the rest limit-only; staff level; HR decides the days | A (+ the existing `LeaveCategoryAllocation`, suggest-changes) |
| Set casual off against annual (raised twice) | H |
| Cancel vs recall; cancel only before approval; the superior recalls | D (B1, B2) |
| Schedule annual, apply as of right; HR may reschedule; maternity when due | A3 (Maternity), E6 (B4 badge), M |
| Take part of your leave before it is due | the existing *can take now* + C2 |
| Reminder when due for leave, to HR and the employee | I |
| HR-run accrual utility | C2 (statement) + M |
| Search one employee in the HR calendar | F |
| Approver edits only relievers; relievers from the profile; search fallback | E |
| Does the plan clash check work? | E1 (it does not; fixed and asserted) |
| Actions inside the plan details modal; what the approver's Cancel does | E4, E5 |
| Balances show annual; an overview for the rest | J |
| Medical board: beyond excuse duty, physicians, attachments, cancel vs dissolve, several employees, disability and compensation | K (step one) + K-II (step two) |
| Encashment: HR days, Finance amount, marked paid | L (on the leaver's settlement) |
| Does the leave year-end work? | G |
| Resumption confirmed by HOD/HR | D3 |

## Lanes

Order: **E → F → D → A → N → C → G → H → J → I → K → L → M.** Lane ~~B~~ (the anniversary year) is
removed by A1; K step two (K-II) waits for R5-Q5.

Each lane: its own harness suite `dev-harness/hr-leave/run-round5-<lane>.mjs`, green twice; slices
1–13 (515) and neighbours at baseline — **except C1, which deliberately changes month-end figures:
re-baseline those assertions to exact numbers, both positions**; the user builds; migrations are
scaffolded by the user and rewritten as guarded SQL; stage, the user commits.

### Lane E — Plans: relievers, the clash check, the approver's view

- **E1 Fix the clash check.** `getRelieverClashes` passes the query object directly, as the other `apiService.get` callers do. `RelieverClashCheck` renders an error state ("couldn't check"), never "free" on a failure. **E1b** fix the global retry rule to read the status `api.service.ts` actually sets (every 4xx is retried three times app-wide today). Harness: a reliever with a known live request over the dates returns ≥ 1 clash.
- **E2 Relievers from the roster.** `LeavePlanService.CreateLeavePlanAsync` fills empty slots from `EmployeeReliever` by priority (extract `AutoFillRelieversAsync` into a helper both services use) and validates chosen relievers (not the employee, active) on create and update. Plan form: the roster first (`employeeRelieverService.getForEmployee`), search as the fallback when it is empty; `initialLabel` on Edit for the employee and both relievers.
- **E3 The approver edits relievers only.** New `PATCH api/hr/leave-plans/{id}/relievers` (the current workflow assignee or `HR.Leave.Write`; Submitted / ChangesSuggested / Approved; not-self, active). `UpdateLeavePlanAsync` stays Draft-only; add the missing `dto.EmployeeId` authz check on PUT.
- **E4 A detail dialog the approver can reach.** On `/hr/leave/plans`: fields read-only, relievers editable (E3), the actions inside — Approve / Reject / Suggest dates / Cancel — each shown only when it can succeed for this viewer (**never Approve/Reject while ChangesSuggested**: the suggestion cleared the workflow instance and both 403 today). The page admits **the current workflow assignee** (a read arm like the request's) and **opens the dialog from `?planId=`**, so the approvals-inbox link lands on the plan. Service: approve/reject refuse plans that are not awaiting a decision. Fix *Decline suggestion* (opens the counter-dates dialog). Portal planner: a relievers section, which also stops the Draft edit wiping HR's relievers and sub-type.
- **E5 Cancel semantics.** Owner: Draft / Submitted / ChangesSuggested. HR: also Approved with no live request, reason required. **Every cancel with a live workflow instance cancels it.** Approvers without write lose the button.
- **E6 Spreading, and "as of right".** Assert several non-overlapping plans and requests per employee per year are accepted (only overlap is refused, `LeavePlanService.cs:85-101`); an "Add another period" affordance on the form. **B4:** a request raised from an approved plan with unchanged dates shows approvers a "matches the approved plan" badge on the request page and in the approvals list; no workflow change.
- Files: `LeavePlanService.cs`, `LeavePlansController.cs`, `LeaveDTOs.cs`, `frontend/src/app/hr/leave/plans/page.tsx`, `frontend/src/app/me/leave/planner/page.tsx`, `leave.service.ts`, `react-query.tsx`, the request page and approvals list (badge).

### Lane F — Calendar per employee

- `GET api/Leaves/calendar` gains `employeeId` (scope Organisation, `HR.Leave.Read`). `/hr/leave/calendar` gains an `EmployeePicker` and passes the existing `organizationUnitId` filter. Plans as dashed bands (`GetCalendarAsync` shows requests only): record, do not build unless cheap.

### Lane D — Cancel, recall, resume (B1, B2, B3)

- **D1 The cancel rule (R5-D3 + B2)** in `CancelLeaveRequestAsync` and the controller arms: subject → Draft / Pending / ChangesSuggested; `HR.Leave.Write` → also **Approved or InProgress while `StartDate >= today`** (the first day, whatever the nightly sweep has already done), reason required; after the first day → refused, naming Recall; Completed → refused, naming Close. Cancel the workflow instance when Pending. The portal hides Cancel on Approved.
- **D2 Recall by the HOD (B1).** Recall's gate (`LeavesController.cs:1029-1030`) gains the employee's line manager (`Employee.ManagerId`) beside `HR.Leave.Write`; the service still refuses the subject; the reason prompt reads "urgent necessity (Labour Act s.25)". Unchanged: the days are restored. Expenses (s.26): documented as an ordinary claim, not built.
- **D3 Resumption (B3).** `LeaveRequest.ResumptionReportedDate` + `ResumptionReportedById`, `ClosureConfirmedById`, `OverstayDays`. `PUT api/Leaves/{id}/report-resumption` (self; InProgress, or Approved past its end date). Close becomes the confirmation — the employee's manager (a new arm) or `HR.Leave.Write`; it sets Completed, `ClosureDate`, `ClosureConfirmedById`.
  - **Early** (reported before the end date): allowed only when the HOD confirms it; confirming curtails the leave exactly as a recall does (reason "early resumption approved"), so the unused days return.
  - **Late** (reported after the first working day after the end date): the confirmation records `OverstayDays` (working days) and shows "overstayed N working days"; nothing is charged; HR and payroll decide (public-service practice: pay forfeited for the absence, possible discipline).
  - Portal: "I'm back at work"; desk and manager: "Confirm resumption". Sweep 2 chases the confirmer once a resumption is reported, HR when nothing is. Migration `AddLeaveResumption` (guarded).
- Harness: cancel refused in each forbidden status and allowed on the first day in both Approved and InProgress; HOD recall allowed and a peer manager refused; resumption on time, early and late; balance neutral through every transition (slice 5's invariant).

### Lane A — Leave kinds (A4) and a form that starts from the kind

- **A1** `LeaveTypeCategory { Other = 0, Annual = 1, Maternity = 2 }` on `LeaveType.Category`; at most one active Annual per tenant (mirror the single-accrual-policy guard in `LeaveTypeService`). Migration `AddLeaveTypeCategory` (guarded): Annual where `MandatoryAnnualLeave = 1` or code `ANN`, Maternity where code `MAT`. **Retire `MandatoryAnnualLeave`**: the compliance register and sweep 4 read `Category == Annual`; drop the column in the same migration.
- **A2 Annual's readers**, each asserted in both positions: the balances default view (J); plans accepted only for Annual (`LeavePlanService` refuses others; the planner offers only Annual); in-service encashment only for Annual (off anyway — A3); the set-off target (H); the reminders (I); the portal home tile; the compliance register; **the exit settlement (L2)**.
- **A3 Maternity's rules** (the category's readers): no minimum-notice rule (a birth can come early); the approver may Confirm, or Reject for a missing or invalid certificate (the reason says so), but may **not** suggest other dates or reschedule — the service refuses, the UI hides; the certificate is required through the type's existing evidence settings (`RequiresMedicalCertificate` on, `SelfCertificationDays` 0 — attachment kind "Excuse duty (medical certificate)"). Extensions (s.57(3)–(5): +2 weeks for an abnormal or multiple birth; certified illness) are an HR adjustment with the certificate attached — documented, not built. Eligibility (female) and calendar-day counting stay type settings.
- **A4 The leave-type form starts from the kind** (the simplification). The kind comes first. **Annual** shows the service gate, build-up, carry-over and encashment (marked exit-only). **Maternity** shows the certificate and the extension note. **Other** shows one number — **days per year (the limit)** — plus paid, approval, reliever, notice, counting and eligibility. Everything else goes under "Advanced". Copy under each kind says what it drives.
- **A5 Other kinds show the limit**: the request form reads "Limit 5 · 2 used · 3 left" instead of a balance; HR gives fewer days with suggest-changes. Sick leave is Other, with its evidence settings.
- Documented answers: staff-level entitlement = `LeaveCategoryAllocation`; "HR decides the days" = suggest-changes; maternity "when it's due, it's due" = A3.

### Lane N — Settings that do what they say (the audit)

For each finding, the simplest honest option: make it bind, relabel it truthfully, or remove it.

- **N1 The ghosts.** The tenant `EncashmentWorkingDaysPerMonth`: remove it from the policy page (the type's divisor always rules). `LeaveAccrualPolicy.IsActive`: add the switch (DTO + update) — the one-active-policy guard already depends on it. `IsPaid`: relabel "Paid (payroll decides what unpaid leave deducts — L-D6)".
- **N2 The misleading.** Accrual frequency: remove *Per pay period* and incremental *Annual* from the picker (C1 fixes the rest). `MaxDaysPerYear`: Annual only, labelled "highest allocation allowed" (it clamps allocations); hidden for Other and Maternity (the limit is the days per year). Eligibility tab: "Anyone matching **any** rule may take this leave". `HasSubTypes`: derived from having active sub-types; the checkbox goes. A sub-type's cap limits that sub-type inside the type's pot and no longer replaces the entitlement. The allocation's sub-type: hidden (always "all sub-types"). `ProRateOnExit`: binds once L2 reads accrued-to-exit. The reminder doc comments: true once lane I routes them. `ForfeitUnusedAfterMonths`: help text that says what it does; off for TDC (B7).
- **N3 The bypasses.** Submit re-runs the create checks — notice (except Maternity), the reliever requirement, the sub-type cap, the accrued balance counting the employee's other Pending requests; editing a draft validates its sub-type and cap. The board threshold counts Pending requests; reschedule, suggested dates and the counter-proposal re-run the evidence gate when they lengthen a request.
- **N4 Demo data.** Holidays to the 2025 list (the seeder and a UAT data script): 1 July Republic Day, Founders' Day 21 September, 4 August removed, Shaqq Day the day after Eid al-Fitr. UNPAID and INJ get their limits as days per year (90 and 180). SICK: the certificate rule on (self-certification 3; the board threshold waits on R5-Q1). MAT: notice 0. Scenario 020's opening adjustments stay (the guide's migration story), noted.
- **N5 Docs.** `HR-CONFIGURATION-REGISTER.md` and `HR-LEAVE-SYSTEM-GUIDE.md` corrected to the audit (first pass 2026-09-25), then per lane.

### Lane C — Accrual: the counting fix, the statement, the as-at report

- **C1 Off-by-one.** A period completes on its last day: `CompletedPeriods` counts a period complete when `asOf >= periodEnd`, so 31 December credits December (24/24 at year end; quarterly and half-yearly likewise). Assert exact figures at year end and on 1 January through an as-of parameter on the snapshot endpoint.
- **C2 The accrual statement.** `GET api/Leaves/balances/{id}/accrual-statement?asOf=` → the policy, the rate and its provenance, one line per completed period (period, days, running total), the as-at date, the cap. A panel on the balance detail and on `/me/leave`; "Accrued as at <date>" wherever Accrued shows. This is the answer to "run a utility that accrues up to a date".
- **C3 Dropped (A2).** The demo keeps access 12 / accrual `MinServiceMonths` 12 (`dev-harness/hr-demo-smoke/scenarios/021-leave-requests.mjs:142`). **C3′ — only if R5-Q1 says "earn from day one":** first-rollover protection — the days a joiner earned before access carry over uncapped, and expire N months after the access date.
- **C4 Correctness under a non-January year** (low priority — TDC runs January): `ProRateFirstYearEntitlement`'s month arithmetic; sweep 4's month (`LeaveReminderService.cs:426`); controller and portal defaults through `LeaveYear.For`.
- **C6 "Leave owed as at a date" (A7).** Days only, annual leave, per active employee as at a chosen date: accrued to that date + unexpired carried − used − pending − encashed. A screen and a CSV under Balances; Finance values it.

### Lane G — Year-end, proved (company year only)

- A harness slice that **executes** carry-over and expiry for real on a fixture employee — and forfeiture once, for the product — and asserts the ledger before and after, both bases, run twice.
- Fixes: the forfeiture result sets `IsDryRun`; **expiry removes only the unused carried part** (carried days are used first), so sweep 5's skip rule (`LeaveReminderService.cs:492`) and the run agree; the carry-over target matches `LeaveSubTypeId`; carry-over for a year that has not ended is refused unless `dryRun`.
- **Dropped: the "CARRIED FORWARD" deduction on the source year.** With the exit settlement reading only the current year (L2) and in-service encashment off (A3), nothing reads the closed year's leftover — and with no automatic rollover there is no PerformedBy problem left to solve.
- B7: HR records an approved deferral beyond the cap as an adjustment on the new year ("Deferred with the approval of …"). Documented answers for the three settings on the demo values (5 / 3 / 15), and that forfeiture is off for TDC.

### Lane H — Casual beyond its limit (A5)

- `LeaveType.AllowOffsetAgainstAnnual` (Other kinds only); `LeaveRequest.SplitFromRequestId` (the annual part points at the casual part). Migration `AddLeaveRequestSplit` (guarded).
- **Asking.** When a request exceeds the type's available days and the flag is on, the form offers "Charge the extra N days to my annual leave (HR decides)". With the tick, create accepts the request if annual *can take now* covers the excess and the employee has passed annual's service gate.
- **Approving.** At the final (HR) approval the service splits it: the casual request keeps its first K chargeable days (K = casual available), and a new Annual request covers the rest — consecutive, the same relievers, Approved by the same approver, linked. The desk has the same tick.
- What comes free: balances need no change (each request counts against its own type); a recall truncates the later, annual part first; attendance posts both; both request pages show the link ("2 days of this absence were charged to Annual Leave").

### Lane J — Balances: annual first

- The default filter is Annual; an **Overview** switch shows every type. The annual view lists **every active employee** (leavers excluded), computing a live snapshot where no row exists (read-only, no minting). Portal: *Can take now* beside *Available*; the home tile uses Annual. C6's report sits here.

### Lane I — Reminders that reach people (B6)

- **I1 Routing.** Put the subject's EmployeeId (and manager) on each leave reminder event and route per kind through the existing notification-topic publisher (its `UserFromEmployeeIdData` recipient type), email on — lighter than porting the orientation digest (K-a); decide at build. LeaveStartingSoon → the employee; CarryOverExpiring → the employee; RequestAwaitingDecision → the current approver; LeaveNotClosed → the confirmer once a resumption is reported, HR otherwise.
- **I2 One chase.** Sweep 4 becomes "Annual leave not yet planned or taken": Annual kind, skips anyone inside the service gate, counts months of the leave year, goes to employee + manager + HR. Its month stays `MandatoryLeaveChaseFromMonth`. No second chase and no new settings.
- **I3 New: "You can now take annual leave"** — once, when the employee crosses the Annual service gate → employee + HR.
- The settings' doc comments corrected; the register updated.

### Lane K — Medical board, step one (A6)

- **K1 Purpose:** `MedicalBoardPurpose { ExtendedSickLeave, InjuryOnDuty, FitnessForDuty, MedicalRetirement, Other }` beside the free-text reason; the screen wording is no longer sick-leave-only.
- **K3 Physicians and pickers:** a physician arm on the member dialog (`medical-reference.service.ts` search); facility and exam pickers on the create form; the service validates those ids (tenant, existence, same employee) instead of letting the foreign key fail as a 500.
- **K4 Attachments** through the upload gate (the `hr-attachment-slice` pattern and the Medical exam-documents precedent; the harness plants rows by SQL because of ClamAV).
- **K5 Cancel vs dissolve:** one status, two words — Requested → "Cancel the request"; Convened → "Dissolve the board" (reason required; sittings kept); `CancelledOn` / `CancelledById` recorded; the detail reads back which happened.
- **K6 The sick-leave gate accepts only a relevant, recent board:** purpose ExtendedSickLeave (or Other), concluded on or after the start of the leave year being counted (`LeaveService.cs:684-691`).
- **K7 Defects:** conclude requires at least one member; Outcome required and a defined value; exactly one member kind per member row.
- **K-II, after R5-Q5:** several employees per board (subject rows, a finding each), incapacity % and recommended compensation for injury-on-duty cases (PNDCL 187), separation's bridge.
- The guide's § 7b and the medical guide updated.

### Lane L — Encashment on exit only (A3, B5)

- **L1** In-service encashment off on the demo tenant (the tenant seed in `ApplicationDbContext.HR.cs` and a UAT data change); the portal encashment screen hidden when it is off; FR-HR-046 honoured; TDC's L-D8 answer recorded when it comes.
- **L2 The leaver's settlement** (`SeparationService.AddLeaveEncashmentLineAsync`, `:2227-2272`): Annual kind only; the days = the current leave year's accrued-to-exit (so `ProRateOnExit` binds) + unexpired carried − used − pending − encashed; no line when the separation reason is `SummaryDismissal` (Act s.30(3); `HREnums.cs:3611`); the FR-HR-152 56-day cap stays, as a setting (L2b). The amount is marked **indicative**, and Finance confirms or corrects it at the settlement's Finance step — check what that step can already edit before building anything; a hand-off note to Finance.
- **L2b The 56-day cap becomes a setting** (the user's decision, 2026-09-25). It is a constant today (`SeparationService.cs:2232`, `encashmentCapDays = 56m`), visible on no screen and changeable only by a release.
  - A company setting on `CompanyHrPolicySettings`, working name `SettlementLeaveDaysCap` (`int?`), **default 56**: FR-HR-152 is TDC's own requirement, so nothing needs asking. Empty means no cap, so the setting always does something.
  - On the HR Policy Settings page beside the settlement's daily-rate basis (`SettlementDaysPerYear`), because both govern the leaver's leave line. The settlement reads it in place of the constant.
  - Migration `AddSettlementLeaveDaysCap`: the user scaffolds it; it is rewritten as guarded SQL with the real default, 56, for every existing tenant row, never the scaffold's 0.
  - Proved in both positions: a fixture leaver owed more days than a low cap is paid the cap; with the cap raised they are paid in full. A row in `HR-CONFIGURATION-REGISTER.md`.
  - ⚠ After L2 the cap rarely binds for TDC: this year's share (at most the 30-day top allocation) plus at most 5 carried days is 35. The point is that the cap is visible and changeable, not a change in what anybody is paid.
- **L3 Defensive, small — it matters only if a client switches in-service on:** encashment holds its days from approval (Approved counts against availability); the guard reads *can take now*; the portal sends the leave-year label; encashment draws only on the current leave year.
- Not built — A3's fallback if a client enables in-service encashment: days above the statutory 15 only, once those are taken, posted in two steps like awards (recognise at hand-off, settle when paid).

### Lane M — Documentation and answers

- **M0** The repo plan doc (this file) with a status block and the § 8 execution log (the round 4 shape).
- **M1** The explainer `HR-LEAVE-ROUND-5-WHAT-CHANGES.md` — rewritten to these decisions on 2026-09-25 and updated as lanes land. It doubles as the stakeholders' plain answer to each question in the PDF.
- **M2** The guide `HR-LEAVE-SYSTEM-GUIDE.md` per lane (§ 1.3b, 4, 4b, 7, 7b, 9, 12, 13, 15, 17, 18, 21, 23) and `HR-CONFIGURATION-REGISTER.md` for every new or changed setting (first corrections 2026-09-25).
- **M3** `HR-OPEN-QUESTIONS-FOR-TDC.md` § Round 5 (R5-Q1…Q5) — added 2026-09-25.
- **M4** Memory: `hr-demo-feedback-round5`, `ghana-leave-law-reference`.

### Noted, not built

- The anniversary leave year (A1) — only if TDC's conditions require it; the design notes stay in `HR-LEAVE-ENTITLEMENT-AND-YEAR-PLAN.md` § 2.3.
- In-service encashment's Finance hand-off (A3).
- Certified sickness during annual leave (Act s.24): a documented procedure — HR recalls from the first sick day (reason "certified sickness, Act s.24") and raises a sick-leave request; the employee takes the rest of the annual leave later.
- A joiner's part period (lane C): accrual credits whole periods only, so a window opening mid-month loses the stretch after its last whole period in the year. The explainer's worked example (18 days) states it and the accrual statement names it. Crediting it pro rata is a question for R5-Q1, not a defect.
- The desk leave screens' year pickers (adjustments, compliance, encashments, plans, register, requests) still open on the calendar year (lane C4 moved the portal's, the balances page's and every controller default; lane G the year-end page's). Invisible on a January leave year; a one-line change each (`useLeaveYear`).
- Recall expenses (s.26): an ordinary claim.
- A leave allowance (R5-Q3): a payroll element, if TDC pays one.
- Sick-pay tiers (full pay, then half pay): payroll's.
- K-II; plans on the calendar.

## Conventions that bind every lane (from memory)

- Never run `dotnet build`; stop `ErpSystem.Api` by command line first, ask the user to build, continue from their result. Stage; the user commits.
- The user scaffolds each migration; I rewrite it as guarded SQL with real defaults; the API applies migrations at startup.
- Ported services stamp `TenantId` explicitly; the workflow engine auto-approves with no published definition (the harness uses two actors; HR bypasses its own guards).
- Harness environment: Staging + `JwtSettings__SecretKey` + the UAT connection string; run `clamd-stub.mjs` for uploads; count ` ERR]`; assert exact numbers in both positions; never assert a total over a scope other fixtures can grow into.
- Before any tenant-wide write on UAT (reminders, data fixes), measure its reach in SQL first.

## Verification

1. Per lane: the new suite green twice; `dev-harness/hr-leave` slices 1–13 at 515, except the C1 month-end assertions (re-baselined to exact numbers, both positions); neighbours (`hr-medical`, `hr-portal`, `hr-separation`, `hr-finance`) at baseline.
2. Settings (lane N): every setting kept is asserted in both positions; every setting removed is gone from the DTO, the form and the register.
3. Browser walks on UAT (plan doc § 5): the plan clash check + roster prefill + approve inside the dialog, **as the line manager arriving from the inbox link**; cancel on the first day, refused the day after; HOD recall; report-and-confirm resumption on time, early and late; the calendar for one employee; the balances annual view and the leave-owed report; casual 7 days → 5 + 2; a board with a physician member and an attachment, dissolved after convening; a leaver's settlement line (annual only).
4. The guide and the explainer re-read against the live database after the last lane; the findings ledger § 23 corrected, not appended.

## 8. Execution log

Newest lane last. Each entry: what was built, what changed from the plan and why, the suite and its
count, the neighbours re-run, and anything found in passing.

### M0 — the live record · DONE 2026-09-25

This file, copied from the planning draft after the decisions of 2026-09-25. The explainer, the guide
§ 23 (L-38…L-48), the configuration register's correction table and the TDC questions (R5-Q1…Q5)
landed the same day in commit `1000f7516`.

### E — Plans: relievers, the clash check, the approver's view · DONE 2026-09-25

**Built.**

- **E1** — the clash probe asked about nobody: `getRelieverClashes` wrapped its query in `{ params }`,
  so every check sent `?params=[object Object]`, the server answered 400, and the form showed the
  reliever as free. The query is passed directly now, and `RelieverClashCheck` has an error state
  ("couldn't check their diary"). **E1b**: the app-wide retry rule reads `error.status`, which is what
  `api.service.ts` sets, so a 4xx is no longer retried three times.
- **E2** — a new plan fills empty reliever slots from the employee's roster by priority, skipping
  anyone with a clash or not at work. Explicit picks are never overridden. Relievers are refused when
  they are the employee, the same person in both slots, not the company's employee, or not at work,
  on create, edit and the relievers PATCH. The desk form shows roster chips first and falls back to
  search; the portal planner shows the roster only.
- **E3** — `PATCH api/hr/leave-plans/{id}/relievers` is open to the current workflow assignee and
  `HR.Leave.Write`, for Submitted, ChangesSuggested and Approved plans only. PUT gained the missing
  `dto.EmployeeId` check, so an owner can no longer move their draft onto someone else.
- **E4** — `LeavePlanDetailDialog`, opened from `?planId=`, so the inbox link lands on the plan. It
  shows the fields read-only and the relievers editable, and offers each action only when it can
  succeed for the viewer. GetById gained the third read arm (the current workflow assignee), as the
  request has. Approve and reject refuse a plan not awaiting a decision (`EnsureAwaitingDecision`);
  on ChangesSuggested they now say "waiting for the employee" instead of a bare 403. "Decline" became
  "Propose other dates".
- **E5** — cancelling records `CancellationDate` and `CancellationReason` (migration
  `20260925152858_AddLeavePlanCancellation`, guarded SQL, proved Up ×2 and Down ×2 on a scratch
  database; on UAT the history row and both columns are verified in SQL). The owner can cancel until
  approval. After approval only HR can, with a reason, and only while no live request has been raised
  from the plan. Cancelling a submitted plan withdraws its workflow instance. The endpoint accepts an
  empty body.
- **E6** — several plans a year, asserted (only an overlap is refused), plus "Plan another period"
  on both forms. **B4**: `LeaveRequestDto.MatchesApprovedPlan` (plan Approved and exactly its
  dates), shown as a badge on the request page and the approvals list.

**Changed from the plan, and why.**

1. **The roster fill is not shared with `LeaveService`** (E2 said to extract one helper). A plan
   judges a reliever's availability by the plan clash check (own plans, own live requests, named on
   another plan); a request judges it by approved or in-progress requests only. A shared helper
   would have made the form and the fill disagree on one of the two sides.
2. **The approver gets the roster on the plan itself** (`LeavePlanDto.RelieverRoster`). The roster
   endpoint is self-or-`HR.Employee.Read`, and the line manager holds neither.
3. **E1b was fixed app-wide** in `react-query.tsx`. The defect was there, not in the leave service.
4. **`ResourceCollectionTab` gained three additive props** (`canEditItem`, `onOpenItem`,
   `openItemLabel`). It is a shared component; no existing caller changes.
5. **The reject dialog requires a reason.** The server still accepts a blank one and stores
   "Rejected", so the API contract is unchanged.
6. **⚠ "At work" means Active OR on probation, for requests as well as plans.** This was not in the
   plan. On UAT, 2,191 of 2,399 employees are on probation (imported long-serving staff included; see HR finish plan Lane 11) and only 177 are
   Active. So the "active" rule that E2/E3 copied from the request path refused 91% of TDC's staff as
   relievers, and the roster came back almost empty. TDC ruled on 2026-09-23 (round 4 lane O, the
   Maintenance technician pool) that probation is a contract status, not an availability.
   `LeaveService.CanCover` now carries that rule for both paths, and the refusal names what the
   person is instead (suspended, on leave, …). **Found in passing:** the request's roster fill never
   validated what it filled, so a suspended roster entry went straight onto a request. It now skips
   anyone not at work.

**Suite.** `dev-harness/hr-leave/run-round5-e.mjs`: **119 assertions, green twice** on the final
version. Earlier versions went 110 twice, then 119. It uses dedicated fixtures only: four reliever
employees (two on probation, one suspended) and one leave type, switched off between runs so the
demo's pickers never offer it. It also re-proves finish-plan lane 4's plan claims ([2b]). Lane 4
itself was not run, because its fixtures are the tenant's first twelve employees, which on UAT are
real TDC staff. After the runs, SQL confirms no live plan, roster row or workflow instance is left on
any fixture. The API log shows no errors from this work: all 3,204 ` ERR]` lines are the known
notification-sender noise, plus three failed notification-table updates from a background sweep and
one Procurement calendar failure (a tenant with no active user).

**Neighbours.**

| Suite | Result |
|---|---|
| hr-leave slice 1 (lifecycle) | **72/75**. The 3 failures are environmental: "the old single-stage definition was superseded". UAT was seeded directly with the two-stage ladders, so there is no older row (1 total, 1 active). The same holds for all three entity codes. |
| hr-leave slice 4 (reads) | **54/54** |
| hr-leave slice 13 (leave year, plans) | **14/14**. The first attempt stopped on three plans stranded by the 2026-09-23 run on UAT; see below. |

Not run:

- Slices 2–3 and 5–12 reach no code lane E changed: no relievers, no plans, no approvals queue.
- W3 slice 5 has no fixture users on UAT, and would link a new login to a real employee. It reads
  only the unchanged plan list.
- hr-portal slice 4's plan assertions were written for a tenant with no leave definitions ("submit
  is REFUSED"), so they have been stale on any database since 2026-09-17.
- hr-medical, hr-separation and hr-finance call no leave method lane E changed, except the request
  read, which slices 1 and 4 cover.

**Found in passing.**

- **The hr-leave harness had been reading the wrong database since the switch to UAT.** Its `SQL`
  helper said `ErpSystemDB` while the API served `ErpSystemDB_UAT`. It now defaults to UAT, with a
  `HARNESS_DB` override, as the round-4 suites do. **The 2026-09-23 run of slice 13 on UAT proved
  nothing:** its leave-year switch and its clean-up both went to the dev database, so its three plans
  were stranded on UAT (still filed under 2026, which is the tell). They were soft-deleted on
  2026-09-25 after checking that no request referenced them.
- **12 active harness leave types sit on UAT beside TDC's 9 real ones** ("C1 Year", "G2 Cash",
  "Ledger Cash" and others), so every leave-type picker in the demo offers them. They were left by
  earlier runs and have not been touched. The five that this lane's neighbour runs created were
  switched off.
- **Probation elsewhere is now HR finish plan Lane 11**, a fix separate from round 5, at the user's
  request (2026-09-25). *Corrected the same day:* this entry first listed four "Active only"
  filters. Checked properly, two of them are not defects:
  - the active-employee count is a deliberate split of the HR home's headcount;
  - the organogram badge labels a status and leaves nobody out.

  The two real ones are SHE's PPE-compliance KPI and the appraisal HR-reviewer pick, and the
  request's last-resort reliever (the line manager, never checked for being at work) joined them.
  **The real cause is data:** about 1,800 of the 2,191 "probationers" were hired years ago. The
  employee import has no confirmation-date column, so the hire path put every imported
  long-serving employee on probation, and 1,795 probation records are live past their end date.
- **The plan forms offer every active leave type, maternity included.** Narrowing by kind is lane
  A's (A4).

### F — The calendar for one employee · DONE 2026-09-25

**Built.**

- **`GET api/Leaves/calendar` takes `employeeId`**, which shows one person's leave. It is applied
  **after** the scope, so it narrows and never widens:
  - in *My team* it is one of the caller's reports or nobody;
  - in *Mine* it is the caller or nobody;
  - *Everyone* still needs `HR.Leave.Read`.
- **`organizationUnitId`** (*Everyone* only) now means the unit **and every unit beneath it**,
  through `IHrAudienceResolver.UnitSubtreeAsync`, the walk the staff directory uses.
- **`/hr/leave/calendar`** gains *One employee* (the `EmployeePicker`) and a level-then-unit picker.
  Both are shown and sent for *Everyone* only. The calendar's title says whose calendar it is, and
  the empty state reads "No leave this month" for one person.

**Changed from the plan, and why.**

1. **The unit filter includes sub-units** (the plan said to pass the existing filter). The existing
   filter matched the exact unit, so choosing a directorate would have shown only the people
   attached to the directorate itself. Nothing had ever sent it, so no screen changes meaning.
2. **`employeeId` narrows every scope, not only *Everyone*** (the plan scoped it to Organisation).
   Because it is applied after the scope, it cannot widen what a scope shows, so allowing it
   everywhere costs nothing, and the suite proves it. The page offers it on *Everyone* only.
3. **Plans on the calendar were not built** (the plan: "unless cheap"). It was not cheap, for two
   reasons:
   - in *My team* they would show a manager plans that the plan gates let them read only while the
     plan is at their approval step;
   - a plan with a raised request would appear twice.

   It stays under "Noted, not built".

**Suite.** `dev-harness/hr-leave/run-round5-f.mjs`: **20 assertions, green twice**. It moves two of
lane E's reliever fixtures into chosen units for the run, and back after. Its key assertion is the
parent unit: A sits in "Development Department", beneath "Operations Directorate", and the
directorate's calendar must include A. An exact-match filter would fail it.

**Neighbours:**

| Suite | Result |
|---|---|
| hr-leave slice 4 (the calendar's own slice) | 54/54 |
| `run-round5-e.mjs` | 119/119 |
| hr-leave slice 1 | 72/75, the same three environmental failures |

The API log shows no calendar errors. The two leave types those runs created were switched off.

### D — Cancel, recall, coming back to work · DONE 2026-09-25

**Built.**

- **D1, cancel.** The employee cancels a Draft, Pending or ChangesSuggested request. HR also cancels
  Approved or InProgress leave up to and including its first day, with a reason. After the first
  day it is refused, naming Recall; closed, rejected and cancelled leave is refused, naming why.
  Cancelling a Pending request withdraws its workflow instance. The portal offers Cancel only before
  approval.
- **D2, recall by the line manager.** The recall gate is the leave write tier **or the employee's
  line authority**. The reason prompt names the urgent necessity of Labour Act s.25. The service
  still refuses the employee.
- **D3, coming back.**
  - Migration `20260925174049_AddLeaveResumption` adds five nullable columns, in guarded SQL,
    proved Up ×2 and Down ×2 on a scratch database and verified on UAT.
  - `PUT {id}/report-resumption`, "I'm back at work", is for the employee only.
  - Close becomes the confirmation, by the desk or the line authority and never the employee:
    - **early** is accepted only on the employee's own report, and cuts the leave short through
      the recall truncation, recorded with reason "Early resumption approved";
    - **late** records `OverstayDays`: working days from the expected return day up to the day
      before the return, counting Monday to Friday less holidays. Nothing is charged.
  - The read carries `ExpectedReturnDate` and `ResumptionTiming`.
  - `GET resumptions-to-confirm` is the line manager's queue, shown on the Approvals page.
- **`viewerActions` on the single read**: the server decides which of Cancel, Recall, Report and
  Confirm to offer, because two rules turn on who the viewer is and on today's date.

**Changed from the plan, and why.**

1. **"The line manager (HOD)" is the supervisor OR the head of the employee's unit or any unit
   above.** B1 treats the two as one; TDC's definitions (finish plan lane 7) separate them
   (`Employees.ManagerId`, `OrganizationUnits.HeadEmployeeId`). So both count, walked upwards as
   discipline resolves head-of-department authority (FR-HR-080). `LeaveService.IsLineAuthorityAsync`
   holds the rule; if B1 meant the supervisor only, the change is one line.
2. **The line authority can read their people's requests** (a fourth read arm). This is not in the
   plan, but recall and confirmation are unusable without it. It is narrower than giving the Manager
   role `HR.Leave.Read`.
3. **A "Returns to confirm" queue for the line manager.** Also not in the plan: without it the
   manager has no way to find a reported return, because the reminders cannot reach them yet.
4. **Sweep 2 is not re-routed.** Leave reminders still reach the HR role only, and per-person
   delivery is lane I's job. **Moved to lane I.**
5. **An early return needs the employee's report.** This refines B3 ("early return only with the
   HOD's approval"): an early date nobody reported is the employer calling someone back, which is a
   recall.
6. **Five columns, not four.** `ResumptionDate` (the day back) is its own fact, separate from when it
   was reported.
7. ⚠ **Found and fixed: a cancelled request could be approved back to life** (guide § 23, L-55).
   - Approve and reject checked no status.
   - Every request cancelled while Pending before this lane still holds a live approval.
   - The generic workflow recall applies the status adapter directly.

   Now approve and reject refuse anything not Pending (`EnsureRequestAwaitingDecision`), and the
   adapter never moves a Cancelled or Completed request. Lane E gave plans the first half.

**Suite.** `dev-harness/hr-leave/run-round5-d.mjs`: **74 assertions, green twice**. It raises leave in
the future, approves it through both stages, then moves it in SQL to stand for time passing. Its
first run failed 73/74, because it raised requests in the next leave year and then moved them back;
the header records that trap.

**Neighbours.** All thirteen hr-leave slices are at their recorded counts. Slice 1 is 72/75, the same
three environmental failures. `run-round5-e.mjs` is 119 and `run-round5-f.mjs` is 20. The API log
shows nothing from this lane, only the known noise and one defect-#23 payroll save failure from an
employee created by a slice.

**Found in passing (harness).**

- `reset.mjs` now retires leave the API refuses to cancel, because cancel refuses leave after its
  first day.
- Slice 11 stored a NULL hire date as the text `'NULL'` and crashed restoring it on UAT. That left
  leave.hr's employee with a 2025-04-01 hire date, which was restored, and the capture now uses
  `ISNULL`.
- The 31 leave types the neighbour runs created were switched off.

### A — Leave kinds, and a form that starts from the kind · DONE 2026-09-25

**Built.**

- **A1, the kind.** `LeaveTypeCategory { Other = 0, Annual = 1, Maternity = 2 }` on
  `LeaveType.Category`.
  - A tenant has at most one **active** Annual type. It is refused at both doors: saving a type as
    Annual, and switching an Annual type back on. The refusal names the type that already is.
  - `MandatoryAnnualLeave` is retired. Migration `20260925184521_AddLeaveTypeCategory` adds the
    kind, makes ONE type per tenant Annual (`ANN` first, then an active flagged type, then the
    oldest; never a deleted one) and `MAT` Maternity, then drops the flag. It is guarded SQL, proved
    Up ×2, Down ×2 and Up again on a scratch database over four tenant shapes, and verified on UAT:
    ANN Annual, MAT Maternity, the three flagged harness types Other.
  - `category` is optional on save: null means Other on create and **unchanged** on update, so a
    caller echoing a type without it cannot turn Annual Leave into Other.
- **A2, annual leave's readers — this lane's share.**
  - Plans: `LeavePlanService` refuses a plan, or an edit, of any other kind, naming it. Both
    planners offer the Annual type only. Plans of another kind made earlier are left alone.
  - In-service encashment: annual leave only, asked after the tenant's switch and before the
    type's own flag.
  - The compliance register and reminder sweep 4 read `Category == Annual`.
  - The portal home's balance tile prefers the Annual balance. `leaveTypeCategory` is on the
    balance, the request and the portal balance.
  - Still to come, in their lanes: the balances view (J), the set-off target (H), the reminders (I),
    the exit settlement (L2).
- **A3, maternity.**
  - No notice rule, whatever the type says.
  - The approver confirms or rejects. The service refuses suggest-changes and reschedule for
    maternity, saying to cancel and raise it again if the dates are wrong; both request pages hide
    those actions.
  - The certificate: TDC's MAT set through the API on UAT to certificate on, 0 self-certification
    days and no board threshold. The seeder sets the same for a fresh build.
- **A4, the form starts from the kind.** Kind cards come first, each saying what it drives, then only
  what the kind needs. The rest sits under *Advanced settings*, which opens itself when a field inside
  it fails. For Other, the hard cap is raised to the limit when it is lower (L-39).
- **A5, the limit.** For an Other kind both request forms read *Limit 5 · 2 used · 1 waiting · 2 left*
  instead of the balance panel.

**Changed from the plan, and why.**

1. ⚠ **Maternity's board threshold is cleared, as well as the certificate switched on.** The plan said
   "RequiresMedicalCertificate on, SelfCertificationDays 0". But the certificate switch also arms the
   board rule, 90 days a year by default, and the s.57 extension on top of 84 days makes 98: a new
   mother would have been sent to a medical board (guide § 23, L-58). The suite proves both positions.
2. **The one UAT data change of this lane is MAT's evidence settings**, made through HR's own door
   (`PUT leave-types/{id}` with the whole type echoed). The certificate was off, with 3
   self-certification days and a board at 90. Exactly those three fields changed.
3. **The maternity guard runs before the approver checks in suggest-changes**, so a non-approver's
   refusal says the request is maternity leave. Request ids are GUIDs and the read is tenant-scoped,
   so it was left.
4. **A request raised from a plan is the plan's leave** (guide § 23, L-59). Found here: the
   new-request page pre-filled the plan's type and left it editable, a draft's edit could change it,
   and *matches the approved plan* compared dates only, so a sick-leave request could use up an annual
   plan and carry the badge. **The user decided the same evening** to hold the request to the plan's
   leave, and it followed as its own commit: the service refuses another type when the request is
   raised and when a draft of it is edited (`RequirePlansLeaveTypeAsync`); both request forms lock the
   type, on the edit pages too; the badge compares the type as well, so a link made before the rule
   claims no match. The shared `SelectField` gained an optional `disabled`, which no other caller
   passes.
5. **The kind cards say only what is built.** Annual's card names plans, cashing in, the compliance
   register, the reminder and the portal home. The balances view and the leaver's settlement are
   added to it in their lanes.

**Suite.** `dev-harness/hr-leave/run-round5-a.mjs`: **89 assertions, green twice** — 79 for the lane,
then 10 for the L-59 follow-up ([3b]: both doors, and the badge in both positions, one of them a link
planted in SQL as it would have been made before the rule). Its harness types
are switched on for the run and off after. One of them is made Annual while switched off and put back
to Other, so the tenant keeps exactly one Annual type, which the suite asserts at the start and the
end. TDC's ANN is saved once with an unchanged echo, and its row (37 columns, all but the audit stamp)
is hashed before and after.

**Harnesses adapted to the kind.**

- Slices 6 [6] and 9 [5] planted their fixture balance on a type flagged mandatory. They now plant it
  on the tenant's own Annual type, for the one fixture employee, and delete it by id.
- Slice 6 [1]'s switch-ON position now asserts that a non-annual type is refused, two assertions
  where there were three, so **slice 6 is 32, was 33**. The stored rate basis it also asserted moves
  to lane L, with in-service encashment itself.
- `run-round5-e.mjs` and slice 13 make their plans on the tenant's Annual type. Their requests stay on
  harness types, because annual leave has a twelve-month service gate. Slice 13 now switches its two
  types off. Since the L-59 follow-up, lane E [8]'s two requests raised from a plan are annual leave
  (the fixture has no hire date, so the gate lets it through, and it accrues from 1 January); the
  annual balance the engine opens for them is deleted after the run, as is lane A [3b]'s, so the
  fixture never shows in the demo's compliance register. Lane E: 119, green twice.
- `hr-finance/run-slice2.mjs` cashes in on a harness type. It makes that type Annual by SQL for § 1
  only, then puts it back to Other and switched off, and restores the tenant's in-service switch (it
  used to leave it on). § 1 passed in full. The run was 54/56: the two failures were catalogue counts
  (11 events, 4 routed), stale since lane 8's later slices grew the catalogue to 26 and 7. They were
  re-baselined and not re-run, because each run mints three employees and posts journals on UAT.
- Demo scenario 021 echoes `category` instead of the retired flag.
- Not run, and now stale on plans: `hr-portal/run-slice4.mjs` (plans and encashment on its own Other
  type; it also retires tenant workflow definitions, so it is not for UAT) and `hr-finish-lane4`
  (real TDC staff, not for UAT).

**Neighbours.** All thirteen hr-leave slices are at their recorded counts, except slice 6 at 32
(above). Slice 1 is 72/75, the same three environmental failures. `run-round5-e.mjs` is 119,
`run-round5-f.mjs` 20 and `run-round5-d.mjs` 74. The API log shows nothing from this lane: 4,400
notification-processing lines and the notification clean-up failing under them, three defect-#23
payroll saves (the employees hr-finance minted), three award refusals logged as 500, and one
procurement calendar job.

After the L-59 follow-up, the same neighbours again: slices 1–13 at their counts (slice 1 72/75, slice 6
32), `run-round5-f.mjs` 20, `run-round5-d.mjs` 74, and `run-round5-e.mjs` 119 twice with its [8]
requests on annual leave. The API log held only the notification noise, its clean-up and the
procurement job; the 24 harness types the slices made were switched off.

**Found in passing.**

- The portal home reads balances by calendar year, not the leave year (guide § 23, L-60). Lane J.
- UAT had 36 harness leave types switched on: 24 from this lane's neighbour runs and 12 left from
  runs on 20 and 23 September, including a `G2 Mandatory` one. All were switched off, leaving TDC's
  nine.
- **Doc comments detached from their members.** This lane's one-Annual guard had been inserted between
  the accrual guard's doc comment and its method; moved. Three older ones are in committed code and
  were left for a tidy-up: the requisition-enforcement enum's summary in `HREnums.cs`, lane E's
  `EnrichRelieverClashesAsync` summary in `LeavePlanService.cs`, and `RefuseSelfApproval`'s in
  `LeaveService.cs`. Each is an XML-doc warning only; the build is unaffected.

### N — Settings that do what they say · DONE 2026-09-25

**Built.**

- **N1, the ghosts.**
  - An accrual policy can be switched off. `CreateLeaveAccrualPolicyDto.IsActive` is null for
    active on create and unchanged on update, and the one-in-force rule follows the switch: a second
    policy may be saved off, and switched on once the first is off. The tab has an *In force* switch.
  - The tenant's *encashment working days per month* is off the policy page, because each leave
    type's own figure always ruled. The column stays; the page's comparison uses a type's 22.
  - *Paid leave* says it is a label: what unpaid leave deducts is payroll's (L-D6).
- **N2, the misleading.**
  - Incremental *Annual* accrual is refused anew, as *PerPayPeriod* was; rows carrying it stay
    editable.
  - The maximum days binds on annual leave only (`ApplyCeiling`), as the **highest allocation
    allowed**, and is hidden for other kinds, where the days per year are the limit. That alone
    makes unpaid and injury leave bookable.
  - A sub-type counts inside its type's pot. `ResolveAnnualEntitlementAsync` no longer lets a
    sub-type's cap replace the entitlement, and it reads staff-level allocations for the whole type:
    before, an allocation missed every request that carried a sub-type. Allocations are stored for
    the whole type, and the tab's sub-type picker is gone.
  - *Has sub-types* is derived from active sub-types (`SyncHasSubTypesAsync`); the checkbox and its
    refusal are gone. Creating a sub-type honours *Active*.
  - The eligibility tab says rules are OR'd, and a unit, level or position rule can carry a gender,
    which narrows that rule alone.
  - *Forfeit unused after* says what it does. Not here: *pro-rate on exit* binds when L2 reads it
    (its help now says no payout reads it yet), and the reminder comments when lane I routes them.
- **N3, the bypasses.**
  - Submit re-runs the create checks (`EnsureStillSubmittableAsync`): notice for a draft (never
    maternity), the reliever requirement, the balance counting the employee's other pending
    requests, and the sub-type cap.
  - A draft's edit checks its sub-type and cap, measures the balance as it stands, and saves the
    second reliever.
  - The medical board counts Pending requests. A move that lengthens a request — reschedule,
    suggested dates, the counter-proposal — re-runs the evidence gate. Answering a suggestion is
    measured against the days the request holds, which is none while it waits.
- **N4, demo data.**
  - Seeder: the 2025 holidays; UNPAID 90 and INJ 180 days a year; SICK's certificate after 3 days
    (the board threshold waits on R5-Q1); MAT notice 0; ANN forfeiture off (B7).
  - UAT, through the app's own doors: the same five type settings (exactly those fields changed),
    and the 2026 and 2027 holidays — 4 August removed, 21 September renamed Founders' Day, Republic
    Day and Shaqq Day added, Shaqq Day observed the Monday after in both years.

**Changed from the plan, and why.**

1. ⚠ **Found and fixed: submitting a draft never recalculated the balance** (guide § 23, L-61).
   A submitted draft's days went on showing as free, so the balances page understated pending leave
   and the next request was checked against the stale figure. Only the workflow route missed it.
   Found by this lane's suite on its first run (87/91). No UAT balance was stale when measured.
2. ⚠ **Found and fixed: the holiday screen blanked what its edit dialog does not show** (L-62). The
   list rows it fills the dialog from lacked the description, the observed date, the pay multiplier
   and the recurring flag, so any edit wrote them back empty. Renaming 21 September on UAT did exactly
   that to both years' pay multiplier; restored to 2.0. The rows carry all four now.
3. **Also found and fixed:** a draft's edit gave back days it never held and dropped the second
   reliever (L-63); answering a suggestion was measured the same way (L-64); creating a sub-type
   ignored *Active* (part of L-44).
4. **The tenant divisor is off the page, not out of the database** — N1 asked for the page.
5. **UAT data through the API, not a SQL script.** The changes are audited; the demo's HR head is
   recorded against the holiday changes, and admin removed the two 4 August rows, which needs
   HR.Attendance.Admin.
6. **A vacuous pass caught.** On its first run the suite's retired-sub-type check passed on the cap
   refusal, because the sub-type was named "R5N Retired" and the create bug had made it active. The
   sub-type is renamed, the check asserts the exact sentence, and the create fix has its own check.

**Suite.** `dev-harness/hr-leave/run-round5-n.mjs`: **92 assertions, green twice**, every rule in
both positions on harness types of its own that count calendar days.

**Neighbours.** All thirteen hr-leave slices are at their recorded counts (slice 1 72/75, slice 6 32),
and slices 3, 7, 10 and 11, which read the rules this lane changed, among them.
`run-round5-e.mjs` 119, `run-round5-f.mjs` 20, `run-round5-d.mjs` 74, `run-round5-a.mjs` 89.

**Found in passing.**

- Chapter 21 item 12 of the guide resets the tenant divisor this lane took off the page; it still
  works (the value is stored) but no longer matters.

### C — Accrual: the counting fix, the statement, leave owed as at a date · DONE 2026-09-25

**Built.**

- **C1, a period counts on its last day.** `LeaveEntitlementService` lays the periods out from the
  window's start (each measured from the start, never chained, so a window opening on the 31st does
  not drift) and credits every period whose last day has come.
  - 31 December credits December: monthly reaches 24 of 24 days inside the year, quarterly four
    quarters, half-yearly two halves. A stored incremental *Annual* row credits its year on the
    last day; before, it credited nothing, ever.
  - A leaver whose last day ends a month keeps that month.
  - Nothing moves on a day that is not a period's last day. No stored figure changed: accrual is
    worked out on read, and UAT holds 2026 balances only.
- **One working, every reader.** The arithmetic is now two pure methods (`ResolveEntitlement`,
  `WorkOut`) over facts loaded once: the type's rules for the year, and the employee's hire date,
  last day and staff level (`LeaveAccrualSubject`). The single-employee reads, a new batch read
  (`GetSnapshotsAsync`) and the statement all call them. A snapshot took about ten queries and now
  takes four. The report reads 2,376 employees in about 0.3 s.
- **C2, the accrual statement.** `GET api/Leaves/balances/{id}/accrual-statement?asOf=`, open to the
  employee and the leave read tier. It returns the rule, the entitlement and its source (the
  staff-level allocation named by grade, or the type's days; the ceiling and first-year scaling when
  they apply), the rate (derived or fixed), one line per completed period with a running total, the
  period under way, and the date it is worked out to and why (the year end, or a leaver's last day).
  - Every balance read now carries `accruedAsOf`, from the same working.
  - Screens: a row on `/hr/leave/balances` opens a new balance detail (the nine figures, the
    statement with a date box, requests, adjustments, cashed-in days). *My Leave* says *built up N
    as at <date>* and opens the employee's own statement. The *Accrued* column says as at when, and
    both request forms add the date to *not accrued yet*.
- **C4, a leave year that does not start in January.**
  - `LeaveYear.MonthOf` gives the month of the leave year.
  - First-year pro-rating counts leave-year months: an April-start joiner hired in February gets
    2/12, not 11/12.
  - Reminder sweep 4 compares the month of the leave year. The setting's label and help on the
    policy page say so.
  - `ILeaveYearContext.CurrentYearAsync` replaces `DateTime.Today.Year` in eleven controller
    defaults across three controllers.
  - `GET api/Leaves/leave-year` and a `useLeaveYear` hook open *My Leave*, the planner and the
    balances page on the current leave year.
- **C6, leave owed as at a date.** `GET api/Leaves/balances/owed?asOf=` and `/export`, with a screen
  at `/hr/leave/balances/owed` reached from Balances. It covers TDC's annual leave, per employee, in
  days. Owed = built up + carried in (unexpired) + adjustments − taken − cashed in; booked and
  awaiting approval are shown beside it.

**Changed from the plan, and why.**

1. ⚠ **Owed is *built up and not yet taken*, not the plan's formula.** The plan subtracted pending
   and every approved day, which is *can take now*, the days free to book. Leave approved for
   November has not been had on 30 September, so it is owed, and the explainer had already promised
   Finance "built up and not yet taken". Both are shown beside *Owed* (Booked, Awaiting). **For the
   user to confirm.** If the plan's figure was meant, it is the row's Owed − Booked − Awaiting, one
   line to change.
2. **Adjustments are in owed.** The plan's formula left them out, but opening balances, B7's
   approved deferrals and forfeiture are all adjustments.
3. **"Unexpired carried" is defined:** in full until the carry-over expiry. From the expiry on, only
   the carried days *taken* before it count, because carried days are used first; the rest lapsed.
   ⚠ Lane G's expiry run should agree: today it zeroes carried days even when used (L-42), so a
   report dated after a run of it would under-count those.
4. **"Every active employee" is everybody on the books at the date:** hired on or before it, and
   either still serving (`HrServingEmployees`, suspended included) or gone only since (last day on
   or after the date). A leaver from before the date was paid through their settlement.
   Adjustments and cashed-in days are labels on the year and count whatever their date. *Taken* is
   dated, and leave straddling the date counts only its chargeable days up to it.
5. **The statement is per balance**, the plan's route, so an employee with no balance row has none
   until lane J lists everybody. The report works such employees out live and mints nothing.
6. **There was no snapshot endpoint** to put the plan's as-of parameter on. The statement's `asOf`
   is the vehicle, and the report's.
7. **The balance detail had no screen** (guide § 23, L-66). It had an endpoint and nothing that
   called it. Built as the "balance detail" the plan put the statement panel on.
8. ⚠ **Found and fixed while designing C1: a derived rate that does not divide** (L-65). Accrual
   was the periods × (entitlement ÷ periods) at full precision. Once December was credited, twelve
   periods of 10/12 came to 9.9999…, and a ten-day request would have been refused on 31 December.
   The total is now entitlement × periods ÷ 12, rounded once: exactly 10, and 4.17 after five
   months.
9. **C4's portal defaults cover *My Leave* and the planner.** `/me/leave/encashments` is lane L's
   (it rewrites that screen, including L3's leave-year label). The desk pages' year pickers are
   under *Noted, not built*.

**Suite.** `dev-harness/hr-leave/run-round5-c.mjs`: **112 assertions, green twice**. Its first run was
111/112, and the failure was its own. It asserted that 31 December, asked of its own year, is a
year-end clamp; it is simply the date asked. The product was right.

- Every C1 figure is asserted on both days of its boundary: the day before a period ends and the
  day it ends.
- C6 raises five real annual-leave requests for leave.emp on TDC's ANN (one moved into March by
  SQL, in the same leave year) and plants 5 carried and 1 cashed-in day. It asserts owed exactly as
  at five dates, including both days of the carry-over lapse.
- C4 moves the tenant's leave year and chase month by SQL for seconds, restored in a `finally` and
  on exit, as slice 13 does.
- Shared rows it touches (leave.emp's hire date; lane E's B, C and X) are captured with `ISNULL`
  and asserted restored.

**Re-baselined, as the plan said C1 would:** slices 11 and 12 derived completed months as this month
minus the start month, one short on a month's last day; slice 4's *accrued ≠ entitled* and *can take
now < available* are false on 31 December once December counts. All three are exact figures now,
true every day. The counts are unchanged (54, 27, 20).

**Neighbours.** Every hr-leave suite, in order, in one pass after the lane's suite was green:

- **Slices 1–13 are at their recorded counts.** Slice 1 is 72/75, with the same three
  environmental failures (UAT has no superseded single-stage definitions). Slices 4, 11 and 12 are
  re-baselined, at 54, 27 and 20.
- **The round 5 suites:** `run-round5-e.mjs` 119, `-f` 20, `-d` 74, `-a` 89, `-n` 92.
- **`run-round5-c.mjs` was 112 a third time**, run last, after the eighteen others on the same
  database.
- **Cleanup:** the 24 harness types that slices 1–12 minted were switched off, measured first.
  Only TDC's nine are active. leave.emp, lane E's fixtures and the tenant's settings are as found.
- **Not run:** hr-medical, hr-portal, hr-separation and hr-finance. None calls anything this lane
  changed except the year a call gets when it names none, and on UAT's January leave year that is
  the calendar year, as before.

**Found in passing.**

- ⚠ **The guide's own worked example was wrong** (§ 1.3b): a Junior qualifying on 1 May has "10 days
  by 31 December of year two". The engine said 8.75 until this lane. It is now what happens.
- On UAT the owed report as at 31 December 2026 reads **47,289.75 days** across 2,376 employees,
  including 2,037 days of opening-balance adjustments: scenario 020's +21 go-live entries on top of
  entitlements that already give the days. It is a demo figure and not a realistic provision, and
  the guide's § 13b says so.
- The API log holds nothing from this lane, over the whole run: 8,200 notification-processing lines,
  the notification clean-up failing under them (15, every one checked), and the procurement calendar
  job. No line mentions leave, accrual or entitlement.
- **Three more detached doc comments in committed code**, beside lane A's three, for the same
  tidy-up: `CountsAsTaken`'s summary in `LeaveService.cs` follows another member's remarks,
  `ILeaveServices.cs`'s attendance-reconcile summary follows the calendar's remarks, and
  `LeavesController.cs` has two summaries stacked on the tenant-wide recalculation. None is this
  lane's; a scan of every file it touched found no new one.

### G — The year-end, proved · DONE 2026-09-25

**Built.**

- **Expiry keeps what was taken in time (L-42).** Carried days are used first. When the window
  closes, the carried days covered by leave taken on or before the last usable day stay, and only
  the rest expire. The run used to zero them all, which charged the days somebody had taken a
  second time. Leave *booked* for after the deadline does not save them.
- **One definition of "used by a date": `ILeaveUsageReader`.** It returns, per employee, the days
  of a leave type taken or booked on or before a date, counted by the walk that charged them;
  leave straddling the date counts only its days up to it. Three readers use it: the expiry run,
  reminder sweep 5 and lane C's *leave owed* report, whose own walk it replaces. The walk itself
  moved out of `LeaveService` into `LeaveChargeableDays`, unchanged, and the charge uses it from
  there.
- **Sweep 5 warns about exactly the days the run will remove.** It compared the year's used days
  with the carried days, so leave booked for June hid the warning while the March days lapsed
  anyway.
- **Carry-over:**
  - it is refused for a year that has not ended, naming the day it can run; a preview is always
    allowed;
  - one pot per leave type: a stray second row is examined and named, not carried over the first;
  - the new year's balance is created at type level.
- **The forfeiture preview says it is one.** `IsDryRun` was never set.
- **`LeaveYearEndResult.TotalDaysExpired`**, and the summary sentence is now the first note. The
  screen shows *carried days expired*, the forfeiture card and its confirmation say what actually
  happens, and the page opens on last leave year.

**Changed from the plan, and why.**

1. ⚠ **Carry-over also applies the lapse to the year it closes** (guide § 23, L-67). This was not in
   the plan. If nobody ran expiry that year, the days that lapsed at the end of March would have
   been carried forward a second time. Carry-over now counts the closing year's carried days only
   as far as they were still usable, by the expiry's own rule, whether or not the expiry was run.
2. **"The carry-over target matches `LeaveSubTypeId`" became "one pot per leave type".** The plan
   predates lane N's decision that a balance is kept per type. So the target is the type's own
   balance, found as before, and a new one is created for the type rather than copying a
   sub-type. UAT holds no sub-type balances and no duplicate rows, so no data moves.
3. **Found and fixed: a forfeiture preview counted days its own expiry was about to remove**
   (L-68). The real run expired first and then read the balance it had just reduced. The preview
   reduced nothing, so it would have forfeited the lapsing days too. Both now read the carried
   days after the lapse.
4. **Expiry changes the carried figure itself rather than posting an adjustment**, as the plan
   says. An adjustment would leave a trace, but lane C's report and the reminder both read "carried
   days still usable" as the row's figure capped by the days used in time. That holds before and
   after a run only if the run writes the same figure. The run's notes say what lapsed and why.
5. **A harness actor that can run forfeiture.** It needs the leave admin tier *and* an employee
   record (the adjustment's `PerformedBy`), which no persona had, so the positive path was never
   asserted (slice 9). `leave.admin` is a TenantAdmin linked to its own fixture employee, minted
   on the first run.

**Suite.** `dev-harness/hr-leave/run-round5-g.mjs`: **55 assertions, green twice**. Its first run was
53/54, and the failure was its own: it read the run's total, which also held a second pot. It now
reads the ledger rows. Every run is real and scoped to leave.emp, on the suite's own three types,
with the ledger asserted before and after:

- carry-over refused for this year, and previewed;
- 2025 → 2026 carried, set not stacked, the cap moved up and down, the source year untouched;
- both bases, where Earned carries 10 against Granted's 15, the 10 depending on lane C's December;
- expiry keeping 3 of 5, with sweep 5 warning about the other 2 where the old rule stayed silent;
- lapsed days not travelling again;
- one pot per type;
- a B7 deferral surviving a re-run;
- forfeiture executed once, with the adjustment's actor the linked admin.

**Neighbours.** Every hr-leave suite, in order, in one pass after the lane's suite was green:

- **Slices 1–13 are at their recorded counts.** Slice 1 is 72/75, the same three environmental
  failures. Slices 9 and 12 run the year-end previews tenant-wide and are unchanged (37, 20): a
  preview is still allowed for a year that has not ended.
- **The round 5 suites:** `run-round5-e.mjs` 119, `-f` 20, `-d` 74, `-a` 89, `-n` 92.
- **`run-round5-c.mjs` 112.** This matters here: its *leave owed* figures now come through the
  shared reader and did not move.
- **`run-round5-g.mjs` was 55 a third time**, run last.
- **Cleanup:** the 24 harness types slices 1–12 minted were switched off, measured first. Only
  TDC's nine are active. leave.emp, lane E's fixtures and the tenant's settings are as found, and
  leave.emp holds no row in the closed year.
- **The API log holds nothing from this lane:** 2,790 notification-processing lines, the clean-up
  failing under them (13), the procurement calendar job, and one defect-#23 payroll save (minting
  `leave.admin`'s employee).

**Found in passing.**

- `leave.admin` is a TenantAdmin with a known password on UAT, like the `admin` account itself. It
  is recorded in the harness README.

### H — Casual leave beyond its limit · DONE 2026-09-26

**Built.**

- **The setting and the columns.**
  - `LeaveType.AllowOffsetAgainstAnnual`: may the days asked for beyond this type's limit be charged
    to annual leave? Only an **Other** kind that **requires approval** may have it, refused at both
    doors. On update, a save that omits it leaves it alone, as for the kind.
  - `LeaveRequest.ChargeExcessToAnnual`: the request asks for it.
  - `LeaveRequest.SplitFromRequestId`: on the annual part, the request it continues. A foreign key
    with no navigation, and indexed.
  - Migration `AddLeaveRequestSplit`, guarded SQL: every existing type and request off, the index and
    key in their own batch. Proven on a scratch database (Up twice, Down twice, Up again, 13 checks)
    before UAT applied it at startup.
- **Asking.** One balance check serves every way into a request: create, draft edit, submit, and
  each move (an approver's suggested dates, the employee's answer, a reschedule). It keeps each
  one's own refusal word for word, and has one way past it:
  - The type keeps its first **whole** days, as many as it has left; annual leave takes the rest of
    the absence, counted by annual leave's own rules.
  - The extra days face annual leave's eligibility, its service gate, and what it can take now.
  - A request on a type that allows it, but that did not ask, is told it could.
  - New `GET api/Leaves/excess-preview` (one's own, or the leave read tier) gives the forms the
    figures before anything is saved: the days the dates cost, what is left, the excess, and why
    annual leave cannot take it when it cannot.
- **Approving.** Whether the split can happen is asked again at every stage, before the engine: the
  type's figure and annual leave's both move while a request waits. At the **final** approval:
  - the request keeps its first days;
  - a new Annual Leave request covers the rest, numbered inside the approval's transaction, with the
    same approval and relievers, `SplitFromRequestId` set, and no workflow instance (nobody's queue
    gains anything);
  - both balances are re-derived, and attendance is posted for both.
- **One absence afterwards.**
  - Cancelling the first part cancels the annual part, which can also be cancelled alone.
  - A recall from either page cuts the annual part first. Back before the annual part begins, it is
    cancelled, and the first part is cut short if the day falls inside it.
  - The return is reported and confirmed on the annual part. A day back before it began closes the
    first part (early, cut short, if inside it) and cancels the annual part as never taken.
  - Moving either part is refused.
  - The screens offer only what applies, and the first part's *due back* is after the annual part.
  - Reminder sweep 1 skips the annual part; sweep 2 skips the first part while the annual part
    runs.
- **Screens.**
  - The leave-type form: an Other type has the switch under its limit.
  - Both request forms: an offer panel with the tick, from the preview.
  - Both request pages: a panel that says what approving would split off, or names the other half.
  - The approvals list: an *Extra days to annual leave* badge.
  - *Move dates* is hidden on a split pair.
- **Demo data.** The seeder switches `CAS` on. UAT's `CAS` was switched on through the API, with the
  whole record echoed from the detail read; every other field was unchanged, and that was asserted.

**Changed from the plan, and why.**

1. **While it waits, the whole request sits on its own type.** Casual leave can read *−2 left*
   until the split, and annual leave holds nothing for it. So both are asked again at the approval,
   and the approval is refused, saying why, when annual leave can no longer take the days. The
   plan's "balances need no change" is true after the split; this is what it means before it.
2. **The two parts are one absence for every act, not only recall.** The plan named recall.
   Cancel, the return and a reschedule needed the same treatment:
   - confirming the return on the first part would have recorded an overstay for days spent on
     annual leave;
   - sweep 2 would have chased a first part whose absence was still running;
   - moving one part alone would leave a gap or an overlap.
3. **A type that sends days to annual leave must require approval** (not in the plan). HR decides the
   charge at approval (A5), so a type approved automatically would charge annual leave with nobody
   deciding.
4. **The extra days face annual leave's eligibility, service gate and balance, not its notice or
   reliever rules.** They are part of a request that met the first type's rules, and they keep its
   relievers.
5. **Whole days, and nothing left means no split.** The type keeps ⌊left⌋ days. With less than one
   day left, the request is refused and told to be raised as annual leave, rather than being turned
   into annual leave whole.
6. **The preview endpoint is new.** The forms had no chargeable-day count ("the chargeable total will
   be lower"), and the offer has to name the days.

**Suite.** `dev-harness/hr-leave/run-round5-h.mjs`: **142 assertions, green twice.**
- Its first run was 140/141. The failure was a leak, found and closed (below).
- A second assertion for the same leak in *send back with dates* made it 140/142 before the fix.
- It ran 142 and 142 after the fix.
- It uses leave.emp, two types of its own (limit 3, working days, as annual leave counts), and TDC's
  own Annual Leave. The weeks are Monday to Friday, with no public holiday.
- Past weeks are reached by moving requests in SQL, as lane D does.
- It retires everything it raised, and opened an annual balance only for the run.

**Neighbours.** Every hr-leave suite, in order, in one pass after the lane's suite was green:

- **Slices 1–13 are at their recorded counts, but for one run of slice 4.** Slice 1 is 72/75, the
  same three environmental failures. Slice 4 read 51/52 on this lane's litter (Found in passing,
  below); with the litter retired it read 54/54 alone. Slices 3, 5 and 7 cover the request paths
  this lane changed (cancel, recall, close, evidence) and are unchanged: 33, 51, 34.
- **The round 5 suites:** `run-round5-e.mjs` 119, `-f` 20, `-d` 74 (cancel, recall and the return,
  on requests that are not split), `-a` 89, `-n` 92 (the suggested-dates door among them), `-c`
  112, `-g` 55.
- **`run-round5-h.mjs` was 142 a third time**, run last, with the cleanup that retires every
  request it raised.
- **Cleanup:** the 25 harness types the older slices minted were switched off, measured first (all
  created by these runs, none of TDC's). Only TDC's nine are active, and `CAS` is the one with the new
  setting on. No request of the suite's is left on leave.emp, in any status.
- **The API log holds nothing from this lane:** 22 failed notification clean-up commands (one of
  them a deadlock victim), the notification clean-up's own error, and one procurement calendar
  failure. No request answered 500.

**Found in passing.**

- ⚠ **L-69: a refusal told a non-approver about the employee's leave.** Approve checked whether annual
  leave could still take the extra days before it checked who was approving. So a manager whose stage
  had passed got *"… only 1 day(s) of it can be taken now"* (400) instead of *not your step* (401).
  *Send back with dates* has always validated the dates before the approver, so its refusals could
  quote the employee's balance, and with this lane their annual leave too. Both now ask
  `EnsureMayDecideAsync` first: the engine's own question, or the approval permission when no
  definition is published.
- ⚠ **The suite's own litter broke slice 4.** Its early runs left 60 cancelled requests on leave.emp
  in the next six weeks. Slice 4 reads leave.emp's register for the next 30 days, a page of 100
  newest first, and its own request fell off the page: 51/52, the two assertions under
  `if (found)` never ran. The 60 were retired (measured; none held attendance), and the suite now
  retires every request it raises, cancelled ones included, and asserts it did.
