# HR Leave Management — System Guide and Demonstration Workbook

**Status:** rewritten 2026-09-17, **after** waves A–E of
[`HR-LEAVE-CLOSURE-PLAN.md`](HR-LEAVE-CLOSURE-PLAN.md) were built. It describes the module as it
now stands, not as it was that morning. Where the demo database will not show what the code can do,
that is said in the step rather than smoothed over.

> ### ✅ Verified — and what the verification changed
>
> This guide was written from the source, and **`dev-harness/hr-leave` has since run it: 192
> assertions across four slices, green twice.** The three things this note used to tell you to be
> sceptical of — two-stage approval, the attendance days written on approval, and the "waiting on
> you" queue — are now the best-tested paths in the module.
>
> **The harness found five defects, three of them introduced by the closure build**, and two changed
> what this book says:
>
> - a refused date suggestion used to **destroy the approval workflow**, leaving the request
>   unapprovable. Chapter 7's send-back walk would have failed in front of a room;
> - the line manager **could not open the request they were asked to approve** — the queue in
>   chapter 11 listed a row that 403'd on click. An assigned approver can now read what they decide.
>
> **What is still NOT proved**, so you do not over-claim: no screen has been rendered by any test —
> there is no browser automation here, and frontend checking is type-checking. And the reminder
> engine in chapter 18 is proved through its run-now endpoints, not on its 24-hour timer.

**Scope:** the whole **Leave Management** group of the HR sidebar, the **Leave Types** rulebook
under Administration, the employee portal's leave screens, and the two surfaces that have no menu
entry of their own.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| — | *(group landing)* Leave Management | `/hr/leave` | 3 |
| — | *(Administration)* Leave Types | `/administration/hr/leave-types` | 4 |
| 1 | Requests | `/hr/leave/requests` | 5 |
| — | *(no menu entry)* New request | `/hr/leave/requests/new` | 6 |
| — | *(no menu entry)* The request | `/hr/leave/requests/[id]` | 7 |
| — | *(no menu entry)* Edit a draft | `/hr/leave/requests/[id]/edit` | 8 |
| 2 | **Calendar** *(new)* | `/hr/leave/calendar` | 9 |
| 3 | **Register** *(new)* | `/hr/leave/register` | 10 |
| 4 | Approvals | `/hr/leave/approvals` | 11 |
| 5 | Plans | `/hr/leave/plans` | 12 |
| 6 | Balances | `/hr/leave/balances` | 13 |
| 7 | Adjustments | `/hr/leave/adjustments` | 14 |
| 8 | Encashments | `/hr/leave/encashments` | 15 |
| 9 | Compliance | `/hr/leave/compliance` | 16 |
| 10 | Year-End | `/hr/leave/year-end` | 17 |
| — | *(API only, no screen)* Reminder engine | `api/hr/leave/reminders` | 18 |
| — | *(portal)* seven screens | `/me/leave/…` | 19 |

**Twenty-five screens**, five of which carry sub-tabs, two of which are batch runs that change
people's balances, and one — the reminder engine — which has no screen at all and is documented
because it runs every night whether anybody looks at it or not.

---

## This document is two things at once

Like the employees guide, this is a **reference** and a **script you can perform**. Every chapter
has the same five parts, and you can read only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Two chapters carry an extra part the others do not:

- **§4.4 — the setting trace.** Every field on the leave-type rulebook checked against its
  read-sites. It used to list eleven settings that were configurable and inert; **nine of those are
  now wired**, and the two that remain are somebody else's to act on. Read it before you answer a
  question about a switch.
- **§7.6 — the conversation.** The five things that can happen to a submitted request, in one
  place, because the request screen now does considerably more than approve and reject.

Two more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered (`LIVE WRITE 1` … `14`),
  and chapter 21 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the three rules that decide whether this demo works

The two rules this book used to open with were defects, and **both are fixed** — the desk's
*Submit request* now submits, and *Mark as paid* works. Do not demonstrate them as problems.

What replaces them is not a defect. It is the shape of the module, and it will still stop you dead
if you meet it for the first time in front of a room.

### Rule 1 — Leave takes **two** approvals, and one click is not enough

Leave requests, leave plans and leave encashments all run a **two-stage** workflow:

```
Draft ──submit──▶ [ Stage 1 · line manager ] ──▶ [ Stage 2 · HR confirmation ] ──▶ Approved
```

**Between the two stages the request is still `Pending`.** That is correct and it is what TDC
asked for — *"supervisor reviewing for approval … hr getting the final leave dates"* — but it has
three consequences you must know before you start:

| What you will see | Why |
|---|---|
| You press **Approve** and the status stays *Pending* | You cleared stage 1. Stage 2 is waiting |
| The request stays in the queue after you approve it | Because it is still waiting — on HR now |
| The workflow tab shows the chain advancing, not completing | Same reason |

**So: approve twice.** In this demo database `hr.head` holds both `Manager` and `HR`, so one
persona can clear both stages of *somebody else's* request — which is convenient for a demo and is
also the honest answer to "can one person do both?": they can, for other people, and the
organisation decides whether that is acceptable by who it grants the roles to.

> ⚠ **What `hr.head` cannot do is approve their own leave, at either stage.** Every leave service
> refuses an approval by the employee the record is about. Chapter 11 walks this deliberately,
> because "the system stopped me approving my own leave" is a better moment than any slide.

### Rule 2 — The Approvals screen shows what is waiting on **you**

It used to ask you to pick a manager and then listed that manager's direct reports' pending
requests. It no longer does either. It now asks the workflow engine which requests **the signed-in
user** may actually decide, and shows those.

- **If you log in as somebody with nothing assigned, the screen is empty.** That is right, not
  broken.
- A request leaves the queue the moment you decide it, and reappears in **HR's** view at stage 2.
- There is no manager picker any more, so you cannot browse somebody else's queue from here.

### Rule 3 — `hr.head` cannot run the year-end, retire a leave type, or delete configuration

The `HR` role holds `HR.Leave.Read`, `HR.Leave.Write` and `HR.Leave.Approve`. It does **not** hold
`HR.Leave.Admin`.

| Action | Result for `hr.head` |
|---|---|
| Year-End → Run carry-over / Run forfeiture | **403**, and the menu item is hidden |
| Leave Types → **⏻ Retire** on a row | **the button is not rendered** |
| Leave Types → delete a sub-type, allocation, eligibility rule or accrual policy | **the action is not offered** |
| Adjustments → **Delete** on a row | **403** |
| Leave reminders → run a sweep | **403** |

⚠ **This changed in a way that matters for a demo.** Those controls used to be drawn and then
refuse with a 403. They are now **hidden** from anyone without the Admin tier, so an HR officer no
longer sees a red button that cannot work. If you are demonstrating the permission model, that is
the point to make — *the system does not offer what it will not allow*. Chapter 17 is performed as
**admin** in a second window for exactly this reason, and you want that window open before you
start.

---

## Conventions

**Routes.** `/hr/leave/requests/[id]` is the file
`frontend/src/app/hr/leave/requests/[id]/page.tsx`. A segment in square brackets is a parameter.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in the solution. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — `LeaveRequest` lives in
`LeaveRequests`, `LeaveCategoryAllocation` in `LeaveCategoryAllocations`.

**Columns every table here carries.** Every entity in this guide derives from `TenantEntity`:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

> ⚠ **One place where soft delete is deliberately NOT used**, and it is worth knowing because it
> looks like an inconsistency: when leave is cancelled, the attendance days it wrote are **hard
> deleted**. The unique index on `(TenantId, EmployeeId, AttendanceDate)` is not filtered on
> `IsDeleted`, so a soft-deleted row would keep holding that employee's slot for that date and the
> next punch on that day would fail against a row nothing can see. Chapter 7 covers this.

**The permission ladder.** Four permissions gate this module:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Leave.Read` | every organisation-wide read — the register, balances, adjustments, plans, encashments, compliance, and the calendar's *Everyone* scope | **yes** |
| `HR.Leave.Write` | raise and amend leave for anyone, adjust a balance, close a request, reschedule, recalculate, record an encashment payment, maintain the leave-type catalogue | **yes** |
| `HR.Leave.Admin` | **the year-end runs**, retiring a leave type, deleting adjustments and leave-type configuration, **forcing a reminder sweep** | **no** |
| `HR.Leave.Approve` | interim authority to rule where *no* workflow definition is published. **Does nothing on this database**, because the leave definitions are seeded and published | **yes** |

**Self-access is not a permission.** An employee reads and writes *their own* leave without holding
any of these. Every per-employee leave endpoint asks the same question — *are you the employee this
record is about, or do you hold the leave tier?* — in a helper called `CanActForEmployeeAsync` /
`CanActOnRequestAsync`. That is why the **Requests**, **Calendar** and **Approvals** leaf items
carry no permission in the sidebar: they are open to all staff, and the API narrows what each
person sees.

**Two exceptions to that, both deliberate:**

- **The Register** (chapter 10) carries `HR.Leave.Read`, because it reads across everybody by
  definition and there is no per-employee reading of it.
- **The Calendar's *My team* scope** (chapter 9) is the one place leave reads *down* the reporting
  line. A manager arranging cover has to see who is away; a calendar band carries a name, a leave
  type and dates, and nothing else — no reason, no balance.

---

## 1. How leave management hangs together

📖 **Say this to the room:**

> "Leave has a rulebook, a ledger and a conversation. The rulebook says what each kind of leave is
> and who may take it. The ledger says what each person has left. The conversation is how a request
> gets from an employee to an approved date in the calendar — and, now, onto the attendance record
> and into the payroll figures. Everything else on these screens is one of those three."

### 1.1 The rulebook — nine leave types and four things hanging off each

A **leave type** (`LeaveTypes`) is the top of the tree. The demo database seeds nine: annual,
sick, casual, maternity, paternity, compassionate, study, unpaid and leave-in-lieu.

Each one carries four child collections, all maintained on the tabs in chapter 4:

| Child | Table | What it decides |
|---|---|---|
| **Sub-types** | `LeaveSubTypes` | named variants of one type — *Sick → certified / uncertified* — each with its own optional annual cap |
| **Allocations** | `LeaveCategoryAllocations` | how many days a **staff level** gets, **effective-dated**, optionally scoped to one sub-type |
| **Eligibility rules** | `LeaveTypeEligibilities` | who may take it at all — by gender, organisation level, organisation unit or position |
| **Accrual policies** | `LeaveAccrualPolicies` | whether the entitlement arrives all at once or builds up through the year |

### 1.2 Entitlement — how many days a person actually gets

Three sources, in strict precedence, resolved by `LeaveEntitlementService`:

```
   1. the SUB-TYPE's own cap            (LeaveSubType.MaxDaysAllowed)
       ↓ if none
   2. the effective-dated ALLOCATION    (for this employee's staff level, this year)
       ↓ if none
   3. the leave type's DEFAULT          (LeaveType.DefaultDaysPerYear)

   …then clamped to LeaveType.MaxDaysPerYear if one is set.
```

### 1.3 Accrual — entitlement is not the same as availability

A leave type with an **accrual policy** does not hand over the year's entitlement on 1 January. It
builds up: monthly, quarterly, semi-annually or annually, either incrementally or as a full grant
once the employee clears a minimum-service bar.

Two switches shape the edges of the year, and **both are now read** — the second was inert until
the closure build:

| Switch | What it does |
|---|---|
| `ProRateOnJoin` | a mid-year joiner starts accruing from their hire date, not from January |
| `ProRateOnExit` | a leaver **stops** accruing on their last day |

> ⚠ **`ProRateOnExit` applies to incremental accrual only**, exactly as its twin does. A policy set
> to *full grant on eligibility* hands over the whole year the moment eligibility is reached, and a
> "full grant" that is then reduced is not a full grant. If TDC wants a leaver's full grant scaled
> down, that is a different setting and it has not been built. Recorded in the closure ledger.

### 1.4 ⚠ The two availability figures — and why you can now see both

This used to be the single most confusing thing in the module, and the fix was to stop hiding it.

There are two legitimate answers to "how many days do I have left":

| Figure | Formula | What it means |
|---|---|---|
| **Available** (policy) | `Entitled + Carried + Adjustments − Used − Pending − Encashed` | what the year owes you |
| **Can take now** (enforced) | the same sum, with **AccruedToDate** replacing Entitled | what you can actually book today |

On annual leave in September those differ by roughly the quarter of the year that has not accrued
yet. The server's create check has always used the second one. The screens used to show only the
first, so a clerk read 20, typed 20 and got *"Insufficient accrued leave balance"* — with the
screen and the server both telling the truth about different questions.

**Now every screen that shows a balance shows both**, leading with the one that binds:

- the new-request forms say **"can be taken now"** and explain the gap underneath when it exists;
- the Balances screen has a **Can take now** column beside **Available**;
- the balances CSV carries both.

Both come from one server-side definition (`LeaveService.EnforcedAvailableDays`), which the create
check also calls — so the number on the screen and the number in the refusal cannot drift apart.

### 1.5 The eight checks a new request faces

In order, in `LeaveService.CreateLeaveRequestAsync`. Each has its own message, so a refusal tells
you which one fired:

| # | Check | Refusal |
|---|---|---|
| 1 | **Eligibility** — gender / level / unit / position rules | *does not meet the eligibility criteria* |
| 2 | **Service access** — `MinServiceMonthsToAccess` | *has not yet completed the minimum service period* |
| 3 | **Past dates** | *cannot be in the past* |
| 4 | **Date order** | *end date must be after or equal to start date* |
| 5 | **Minimum notice** — skipped for drafts | *requires at least N day(s) notice* |
| 6 | **Overlap** with the employee's own live leave | *already has a leave request for this period* |
| 7 | **Accrued balance** | *Insufficient accrued leave balance. Available: X, Requested: Y* |
| 8 | **Reliever** — if the type requires one and none could be assigned | *requires a reliever* |

**Three checks were added by the closure build**, and they fire before the eight above:

| Check | Refusal |
|---|---|
| The leave **type** is retired | *has been retired and cannot be requested* |
| The **sub-type** is retired | *has been retired and cannot be requested* |
| The sub-type's **annual cap** would be exceeded | *capped at N day(s) a year … so M remain and this request asks for K* |

> The sub-type cap is enforced **across the year**, not per request. Capping a single request would
> be defeated by splitting one request into two, and *"caps days for this subtype"* plainly means
> the year.

### 1.6 Chargeable days — which days a request actually costs

Each leave type decides whether weekends and public holidays count
(`CountWeekendsAsLeave`, `CountHolidaysAsLeave`). What changed in the closure build is **where the
holidays come from**.

Leave used to run its own holiday query, matching on tenant alone. It therefore counted holidays
from *every* calendar including retired ones, ignored `PublicHoliday.IsActive`, and never saw a
`SubstitutionDate` — the working day given in lieu when a holiday falls on a weekend.

It now asks `IHrWorkingDayCalculator`, the same component HR's statutory disciplinary clocks use.
So leave and the rest of HR agree about which days the tenant does not work, and leave gained three
behaviours for free:

- only the **default, active** holiday calendar counts;
- an inactive holiday is ignored;
- a **substitution date** is a non-working day.

> ⚠ **And one behaviour changed.** A holiday whose **Observance** is *Optional* is now treated as a
> **working day** — the office is open and taking it is leave like any other. Only *Mandatory* and
> *Substitute Day* close the tenant. If a room asks why an optional holiday was charged, that is
> the answer, and it is flagged to the payroll owner in
> [`../HANDOFF-PAYROLL-LEAVE.md`](../HANDOFF-PAYROLL-LEAVE.md).

**The list of chargeable dates and the request's `TotalDays` are now one thing.** `TotalDays` is the
*count* of that list, and the attendance posting walks the *same* list — so the number of days
marked on the attendance register can never disagree with the number of days the request charges.

### 1.7 The ledger — six components, none of them typed by hand

`LeaveBalances` is keyed on **(employee, leave type, year)**. Six components:

| Component | Derived from |
|---|---|
| `EntitledDays` | the entitlement engine, at the moment the balance row was created |
| `CarriedOverDays` | the year-end carry-over run |
| `UsedDays` | approved requests |
| `PendingDays` | submitted-but-undecided requests |
| `EncashedDays` | encashments at status *Processed* |
| `AdjustmentDays` | the signed sum of manual adjustments |

**Only the last is hand-entered**, and it carries a reason code, a remark and the actor's employee
id. Everything else is re-derived from source rows by `LeaveBalanceRecalculationService` after
every write.

> ⚠ **`LeaveBalance` has no sub-type column.** All of Sick's sub-types share one pot. That is a
> deliberate decision (closure plan D-2) and it is why the sub-type cap is enforced as a query at
> request time rather than as a second balance row. Every *"why doesn't the sub-type cap stick"*
> question traces back to this one line.

### 1.8 The conversation — and it is now the same on both records

A leave **plan** is an intention for the year. A leave **request** is the actual application. Both
now support the same conversation, which they did not before:

```
Draft ──submit──▶ Submitted/Pending ──┬── approve ──▶ (stage 2) ──▶ Approved
                                      ├── reject ───▶ Rejected
                                      └── send back with dates ──▶ ChangesSuggested
                                                                      │
                                              employee accepts, or counters with their own
                                                                      │
                                                                      ▼
                                                              back to Pending
```

And an **approved request** can still move:

| Action | Effect |
|---|---|
| **Reschedule** | new dates, same request number, same history — and **the approval re-opens** |
| **Confirm** | records that the leave is still going ahead. Moves no days, changes no status |
| **Close** | ends it after the end date |
| **Cancel** | ends it at any point, releases the days |

> ⚠ **Rescheduling re-opens the approval on purpose.** An approval is an approval *of dates*;
> carrying it across to different ones would let the record claim an authority nobody gave. The
> dialog says so before you confirm.

### 1.9 What leave now touches outside its own menu

This is the part that changed most, and it is the best thing in the module to demonstrate.

| Where | What leave does there |
|---|---|
| **Attendance** | approving leave writes `StaffDailyAttendance` rows at status **On Leave**, one per chargeable day, each carrying the request id. `DaysOnLeave` on the monthly summary — **which the payroll export reads** — is derived from those rows |
| **Medical** | a medical expense claim can name the sick leave it arose from |
| **Notifications** | the nightly reminder engine raises five kinds of leave chase into the in-app feed |
| **Finance** | nothing is posted. The encashment payout is **recorded as a money event** in the integration backlog and waits for one Finance sweep |
| **Payroll** | nothing is written. Unpaid leave produces no deduction; that is payroll's to apply and is handed off |

### 1.10 The twelfth reminder engine

HR has eleven date-watching sweeps — assets, certifications, discipline, ID expiry, probation,
separation, SHE, movements, travel, teams, attendance. Leave, the module with more dates that
matter than any of them, had none. It now has one. Chapter 18.

> ⚠ **It warns and moves nothing.** Carry-over and forfeiture still belong to the year-end runs in
> chapter 17, which are deliberately **not** scheduled, because those two acts change people's
> entitlements and automating them is TDC's policy call. The engine tells somebody that carry-over
> is about to lapse; it does not lapse it. That distinction is worth making out loud if anybody
> asks why one is automatic and the other is not.

---
## 2. Before the room fills — the prep

**Thirty minutes the day before.** Every step here exists because something goes wrong without it.

### 2.1 Bring the system up, and confirm the definitions are the new ones

Start the API and the web app as usual. Then do **one check that did not exist before**, because
everything in rule 1 depends on it:

> **Is this database carrying the two-stage leave definitions?**

The two-stage ladder is installed by the workflow seeder on startup
(`EnsureLeaveWorkflowsSeededAsync`). A database built before 2026-09-17 and never restarted still
has the **single-stage** definition, in which one approval finishes a request — and the whole of
rule 1 will look wrong to you.

Two ways to tell, either is enough:

| Where | What you want to see |
|---|---|
| `/administration/workflow` → definitions, filtered to **Leave Approval** | **one active** definition, with **two** approval steps between Draft and Approved |
| Any submitted request's **Workflow** tab | a chain with two approval steps, not one |

If you see a single-stage definition, **restart the API** and look again — the seeder repairs it on
startup and retires the old row rather than leaving both active.

### 2.2 Choose your anchor employee

Everything reads better when one person runs through the whole book.

| Role in the demo | Who | Write it down |
|---|---|---|
| **The subject** — her requests, her balances, her portal | the `staff` persona | `TDC/________` |
| **Her manager** — clears stage 1 in chapter 11 | the `head.dev` persona | `TDC/________` |
| **HR** — clears stage 2 | `hr.head`, your main window | — |
| **Her reliever** — named on her requests | roster priority 1 | `_______________` |

Open `/hr/employees`, search her surname, open the row, and note the staff number.

### 2.3 Check the numbers you will quote

Open `/hr/leave/balances`, pick her in the **Employee** filter, and read off the Annual Leave row —
note that there are now **seven** figures to read, not six:

```
Entitled                                    : ____________
Accrued to date                             : ____________
Adjustments (the opening balance loaded)    : ____________
Used                                        : ____________
Pending                                     : ____________
Available (the policy figure)               : ____________
Can take now (the one that binds)           : ____________
```

Now open `/me/leave` in the portal window as her. It shows the same figures. Good — the two sides
agree, and they agree *because they call the same server-side definition*, which is worth saying if
anybody asks how you know.

⚠ **Expect Available to look generous — roughly double the entitlement.** The demo seeder loaded
each person's opening balance as an **adjustment** of +21 days on top of the leave type's 21-day
default. That is not a fault and it is not worth hiding: it is exactly how you load balances from a
legacy system on go-live day, and chapter 14 makes a virtue of it.

⚠ **And expect *Can take now* to be lower than *Available*.** That is §1.4, it is correct, and it
is one of the better small moments in the book. Do not "fix" it.

### 2.4 Rehearse the two-stage approval, once, tonight

This is the rehearsal that stops rule 1 biting you live. **It is different from the old one — there
are two approvals now.**

1. `/hr/leave/requests` → **New Request**.
2. Employee: **anyone other than your anchor**. Leave type: **Compassionate Leave**. Start and end:
   **the demo date**. Reason: `Rehearsal — delete me`.
3. Press **Submit request**. You land on the request; status reads **Pending** and the **Workflow**
   tab shows a live instance. *(This is the button that used to do nothing. It works now.)*
4. Press **Approve** → confirm. **Status stays *Pending*.** Read the workflow tab: the chain has
   advanced to the HR step. **This is rule 1 and you have just seen it.**
5. Press **Approve** again → confirm. **Now** status becomes **Approved**.
6. Open the **Overview** tab and read **Attendance days marked**. It should say *N of N* where N is
   the chargeable total. **This is the attendance join, and it is the thing most worth showing.**
7. Press **Cancel**, reason `Rehearsal`, to put the days back — and note that the attendance days
   come off again.

If step 4 or 5 misbehaves, re-read §2.1 — nine times out of ten the database is carrying the old
single-stage definition.

### 2.5 Set up chapter 17 (year-end) — or decide to cut it

Year-end is `HR.Leave.Admin`, which `hr.head` does not hold. Two choices:

| Option | Do this |
|---|---|
| **Show it** *(recommended)* | Open a third browser window signed in as **admin** (`Admin123!`) and pre-open `/hr/leave/year-end`. Scope both runs to **one employee** — never the whole organisation |
| **Cut it** | Skip chapter 17 and say the sentence in §17's ▶ step 1 instead. Twenty seconds, and nothing is lost but the button |

⚠ **Never run an unscoped forfeiture on the demo database.** It posts a negative adjustment against
**every balance in the tenant**, and there is no undo short of a rebuild.

### 2.6 Optional — prime the reminder engine so chapter 18 has something to show

The reminder engine sweeps nightly. If nothing has ever swept this database, its log is empty and
chapter 18 is a description rather than a demonstration.

As **admin**, `POST api/hr/leave/reminders/run` once. Then `GET api/hr/leave/reminders/log` should
return rows. It is safe to repeat — dispatch is deduped per item, which is itself the point chapter
18 makes.

⚠ **Preview first if you are unsure.** `GET api/hr/leave/reminders/preview` shows exactly what a
sweep *would* fire without firing it, and without claiming any dedupe keys.

### 2.7 Pre-open every screen

The web app compiles a route the first time it is opened. Open these now, one at a time, waiting
for each to paint, then leave the tabs open.

**Window A — hr.head** (your main window):

`/hr/leave` · `/hr/leave/requests` · `/hr/leave/requests/new` · one request's detail page ·
**`/hr/leave/calendar`** · **`/hr/leave/register`** · `/hr/leave/approvals` · `/hr/leave/plans` ·
`/hr/leave/balances` · `/hr/leave/adjustments` · `/hr/leave/encashments` · `/hr/leave/compliance` ·
`/administration/hr/leave-types` · the Annual Leave detail page — **and click all five of its tabs**

**Window B — staff** (your anchor): `/me/leave` · **`/me/leave/calendar`** · `/me/leave/new` ·
`/me/leave/planner` · `/me/leave/encashments`

**Window C — admin** (`Admin123!`): `/hr/leave/year-end` — held in reserve for chapter 17.

⚠ **The calendar is the slowest first paint** in the module — it draws a 42-cell grid and resolves
holidays. Open it twice.

### 2.8 The numbers to write in

```
Leave types (Administration → Leave Types)        : ____________
Requests for the anchor this year                 : ____________
Rows on /hr/leave/register with no filter         : ____________
Rows on /hr/leave/balances with no filter         : ____________
Encashments (should be 1, Processed)              : ____________
Compliance rows outstanding                       : ____________
Annual Leave: entitled / accrued / can take now   : ______ / ______ / ______
People away in the current month (calendar)       : ____________
```

### 2.9 Prep checklist

```
[ ] 2.1  TWO-STAGE definitions confirmed active — the single most important check
[ ] 2.2  anchor employee, manager and reliever written down
[ ] 2.3  the seven balance figures read off and written in
[ ] 2.4  the two-stage approval rehearsed, attendance days seen, rehearsal cancelled
[ ] 2.5  Window C (admin) open on /hr/leave/year-end, or chapter 17 cut
[ ] 2.6  one reminder sweep run, so chapter 18 has a log to show
[ ] 2.7  every screen pre-opened in the right window — calendar twice
[ ] 2.8  the numbers written in
[ ] Window A back on /hr/leave — your opening screen
```

⚠ **Do not refresh the browser during the demo unless a step tells you to.**

---

## 3. `/hr/leave` — the group landing

### 📍 Where you are

**Sidebar:** Human Resources → **Time & Leave** → **Leave Management** · `/hr/leave` · as
**hr.head** · **2 minutes**

### 📖 What it is

Eleven cards in three rows. It is a map, not a dashboard — there are no figures on it — and its job
is to give the room the shape of the module in one screen before you go anywhere.

### 👁 On the page

**Header.** Title *Leave Management*, subtitle *Requests, approvals, balances and the year-end
cycle.* No buttons.

**Three groups of cards**, each with a small grey heading:

| Group | Cards | Goes to |
|---|---|---|
| **Day to day** | **Requests** — *Raise leave and track it through approval.* | `/hr/leave/requests` |
| | **Calendar** — *Who is away, and when.* | `/hr/leave/calendar` |
| | **Register** — *Every request across the organisation, with export.* | `/hr/leave/register` |
| | **Approvals** — *Requests waiting on your decision.* | `/hr/leave/approvals` |
| | **Plans** — *Leave planned ahead for the year.* | `/hr/leave/plans` |
| **Entitlement** | **Balances** — *Entitlement, accrual, usage and what remains.* | `/hr/leave/balances` |
| | **Adjustments** — *Manual corrections with an audit trail.* | `/hr/leave/adjustments` |
| | **Encashments** — *Convert unused days into cash.* | `/hr/leave/encashments` |
| **Periodic** | **Compliance** — *Who still owes mandatory leave.* | `/hr/leave/compliance` |
| | **Year-end** — *Carry-over and forfeiture runs.* | `/hr/leave/year-end` |
| | **Leave Types** — *Setup: entitlement, carry-over and accrual rules.* | `/administration/hr/leave-types` |

Every card is a link; the whole card is the click target.

> **The reminder engine has no card**, because it has no screen — it runs nightly and reports into
> the notification feed. Chapter 18 covers it and says so explicitly, because a thing that acts on
> your data without a screen is exactly the thing an auditor asks about.

### ▶ Walk it

**1 — Read the three headings, in order, without clicking anything.**

> "Leave splits into three kinds of work, and they belong to three different people.
>
> **Day to day** is the clerk's work — somebody asks for leave, somebody decides, and you can see
> the whole organisation's leave either as a calendar or as a register you can export.
>
> **Entitlement** is the ledger. How many days does a person have, where did the number come from,
> and what has been done to it. Almost nothing on these screens can be typed — the numbers are
> derived, and the one thing a human can enter carries their name.
>
> **Periodic** is the work that happens once a year, or once a quarter, and changes a lot of rows
> at once. Which is why the two screens that do that are the only ones in this module an ordinary
> HR officer cannot open."

**2 — Point at Calendar and Register.**

> "These two are new. Before them, the only way to see leave was one employee at a time — you could
> answer 'what has Ama taken?' but not 'who is off in December?'. A leave system that cannot answer
> the second question is a filing cabinet."

---

## 4. `/administration/hr/leave-types` — the rulebook

### 📍 Where you are

**Sidebar:** Administration → **HR** → **Leave Types** · `/administration/hr/leave-types` · as
**hr.head** · **8 minutes**

### 📖 What it is

Every kind of leave the organisation recognises, and the rules attached to each. This is where the
entitlement numbers come from, where the accrual rules live, and where you answer any question that
starts "can somebody…". Four screens: a register, a detail page with five tabs, a create form and
an edit form.

### 👁 On the page — the register

**Header.** Title *Leave Types*, subtitle *Kinds of leave, their entitlement and their rules.*
One button: **+ New Leave Type**.

**Search box.** Filters by name, client-side.

| Column | Shows |
|---|---|
| **Name** | the type's name, with its code underneath |
| **Days / year** | `DefaultDaysPerYear` |
| **Carry over** | *Yes* + the cap, or *No* |
| **Paid** | *Yes* / *No* |
| **Status** | Active / Inactive badge |
| *(unlabelled)* | **⏻ Retire**, on active rows only |

⚠ **The ⏻ button is only rendered for `HR.Leave.Admin`.** As `hr.head` you will not see it at all.
That is rule 3, and it changed in the closure build — the button used to be drawn for everybody and
then refuse.

> **Say this if you are demonstrating the permission model:**
>
> "Notice what is *not* on this screen. I am signed in as the head of HR and there is no retire
> button on any row — not greyed out, not there. The system doesn't offer me something it is going
> to refuse. That is a small thing that makes a large difference to how much people trust it."

### 👁 On the page — the detail, and its five tabs

**Overview** is a read-only summary in four cards: identity, entitlement, the carry-over and
forfeiture policy, and the encashment rate settings, plus a count badge for linked allowance
components.

The other four tabs are editable child collections, all the same shape — a table, an **Add**
button, and a row menu with **Edit** and **Remove**:

| Tab | Rows are | The field that matters most |
|---|---|---|
| **Sub-types** | named variants | **Max days** — now an *annual* cap, enforced per request |
| **Allocations** | days per staff level | **Effective from / to** — this is how a policy change is dated rather than overwritten |
| **Eligibility** | who may take it | the **gender qualifier**, which ANDs onto an org-scoped rule |
| **Accrual** | how entitlement builds | **Frequency**, **Mode**, and the two pro-rate switches |

⚠ **Remove is `HR.Leave.Admin` on all four tabs and is hidden from `hr.head`.** Add and Edit are
`HR.Leave.Write` and are available. So an HR officer can add and correct configuration but cannot
destroy it — which is the right split and is worth one sentence if the room asks.

⚠ **Retired sub-types no longer appear in the pickers.** The rulebook tab still shows them, with an
*Inactive* badge, because that is what the Status column is for.

### ▶ Walk it

**1 — Open the register and count.**

> "Nine kinds of leave. Each one is a small rulebook of its own."

**2 — Open Annual Leave and read the Overview aloud, slowly.**

> "Twenty-one days a year. Carries over, capped at five, and the carried days expire at the end of
> March. Weekends don't count against it, public holidays don't count against it, and it can be
> converted to cash."

**3 — Click Accrual and make the point that decides most arguments.**

> "This is why the number on somebody's screen in March is not twenty-one. The entitlement is
> twenty-one for the year; it arrives monthly. In March you have accrued about five. The system
> will let you *plan* leave you haven't accrued — but it will not let you *book* it."

**4 — Click Allocations and point at the effective dates.**

> "When the policy changes, you don't overwrite the old number. You end one row and start another.
> That way a request raised last year is still judged by last year's rule, and nobody has to
> remember what the policy used to be."

**5 — Click Eligibility on Maternity Leave.**

> "Gender: female. That is the whole rule, and it is enforced when the request is raised, not when
> somebody notices."

---

## 4.4 The setting trace — what the rulebook lets you configure, and what reads it

**This section used to list eleven settings that were configurable, saved, displayed back, and
honoured by nothing.** The closure build wired nine of them. Two remain, and both are somebody
else's to act on. This is the section to read before you answer a question about a switch.

### 4.4.1 Now wired — settings that were inert and are not any more

| Setting | What it does now |
|---|---|
| **Sub-type → Max days** | enforced as an **annual cap per employee per sub-type**, checked when a request is raised and when its dates move. ⚠ It is *not* a separate balance — see §1.7 |
| **Sub-type → Active** | retired sub-types are filtered out of the request forms' picker **and refused by the service**, so the API door is shut too |
| **Leave type → Active** | the **service** now refuses a retired type, not just the picker. Moving a draft onto a retired type is refused as well |
| **Accrual → Pro-rate on exit** | a leaver stops accruing on their last day. ⚠ Incremental accrual only — see §1.3 |
| **Calendar colour** | real, because a calendar exists. It colours the bands in chapter 9, and a type with no colour set falls back to a generated palette rather than showing nothing |
| **Holiday → Observance type** | *Mandatory* and *Substitute Day* close the office; ***Optional* is a working day**, so leave taken on it is chargeable |
| **Holiday → Active** | an inactive holiday no longer suppresses a leave day |
| **Holiday → Substitution date** | the day given in lieu is now a non-working day for leave |
| **Which calendar a holiday belongs to** | only the tenant's **default, active** calendar counts |

### 4.4.2 Still not read by HR — and both belong to payroll

| Setting | Status |
|---|---|
| **`LeaveType.IsPaid`** | display-only. Unpaid leave produces no deduction anywhere, because **what a day of unpaid leave is worth is payroll's to decide**. HR records the leave and the days; the hand-off is written up in [`../HANDOFF-PAYROLL-LEAVE.md`](../HANDOFF-PAYROLL-LEAVE.md) |
| **`AttractsHolidayPay` / `HolidayPayMultiplier`** | stored, and now **labelled on the holiday form as payroll's** — *"HR stores it; payroll applies it"* — so the screen no longer implies HR acts on them |

> **If somebody asks "so is unpaid leave actually unpaid?":**
>
> "HR records that she was away, which days, and that the leave type is unpaid. Turning that into a
> deduction is payroll's job, and payroll is a separate module with its own owner. We've written
> the hand-off rather than guessing at somebody else's arithmetic."

### 4.4.3 The joins — all three are now written

The three foreign keys that existed, were mapped end to end, and were set by no code path:

| Join | State now |
|---|---|
| `StaffDailyAttendance.LeaveRequestId` + the `OnLeave` status | **written on approval**, one row per chargeable day, removed on cancel or reschedule. `DaysOnLeave` on the monthly summary — which the payroll export reads — is derived from these rows and is no longer always zero |
| `LeaveRequest.LeavePlanId` | **written**, by the *Raise the leave request* action on an approved plan (chapter 12) |
| `MedicalExpenseClaim.LeaveRequestId` | **has a writer** — the *Related sick leave* picker on the medical claim dialog |

⚠ **The attendance one has a deliberate limit worth knowing.** Leave never overwrites an
observation: if a day already carries a punch, or a note somebody wrote, leave leaves it alone and
counts it as skipped. That is why the request screen says **"Attendance days marked: N of M"** —
when N is less than M, some of those days already had attendance recorded, and **those days will
not reach the payroll export as leave**.

### 4.4.4 What is not a ghost — checked and working

So that this section is not read as a list of everything being broken: **twenty-one settings were
traced and are fully honoured.** Minimum notice · Requires approval · Requires a reliever · Min
service to access · Allow carry-over and its cap · Carry-over expiry · Forfeit unused after ·
Mandatory annual leave · Count weekends · Count holidays · Allow cash conversion · the three
encashment rate settings · Default days · Max days *(as a ceiling on everything below it)* · Has
sub-types · all four eligibility rule types, including the gender qualifier that ANDs onto an
org-scoped rule · accrual frequency, mode, rate, min-service and pro-rate-on-join · allocation
effective dating.

---
## 5. `/hr/leave/requests` — one person's history

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Requests** ·
`/hr/leave/requests` · as **hr.head** · **4 minutes**

### 📖 What it is

One employee's leave applications, with each one's position in its approval chain beside it. It is
deliberately per-employee — *"what has Ama taken this year?"*. The organisation-wide question,
*"who is off in December?"*, belongs to the **Register** (chapter 10) and the **Calendar**
(chapter 9), which is what those two were built for.

### 👁 On the page

**Header.** Title *Leave Requests*, subtitle *Requests and their position in the approval
workflow.* One button: **+ New Request**.

**Filters card.** Three controls in a row:

| Control | Values | Notes |
|---|---|---|
| **Employee** | an employee picker | **type at least 2 characters**; debounced 400 ms; searches **active employees only**. Resets to page 1 |
| **Year** | next year · this year · last year · the year before | resets to page 1 |
| **Status** | All · Draft · Pending · **Changes suggested** · Approved · Rejected · Cancelled · In progress · Completed | **server-side** — the count and the paging agree with it |

⚠ **The status filter used to be client-side**, filtering the page you already had while showing
the unfiltered total beside it. It now goes into the query, so "show me the rejected ones" means
all of them, and the count is theirs.

**It opens on the right person.** Two rules, in order:

1. If you arrived from an employee's profile — *Leave* tab → *Open in Leave* — the `?employeeId=`
   in the URL is honoured and the picker is labelled with their name.
2. Otherwise it pre-fills with **you**, so a member of staff who lands here sees their own history
   rather than an empty picker and a 403.

⚠ **The deep link used to be ignored**, so "open in Leave" landed on whoever the picker defaulted
to — somebody else's leave, presented as if it were theirs.

**Requests card.**

| Column | Shows |
|---|---|
| **Request** | the number, bold — `LV2026000004` |
| **Leave type** | name, and ` · sub-type` where one is set |
| **From** / **To** | `YYYY-MM-DD` |
| **Days** | right-aligned — the **chargeable** total |
| **Status** | a status badge |
| **Awaiting** | the current workflow step and who it is with; `—` when there is no live instance |

### ▶ Walk it

**1 — Land on it and point at the employee box before anything else.**

> "It has already picked me, because most people who open this screen are looking for their own
> leave. If I'm HR I just change the name."

**2 — Pick your anchor. Read the rows.**

**3 — Set Status to *Approved*, then back to *All statuses*.** Point at the count.

> "The count changes with the filter. That sounds obvious, and it is worth saying only because the
> alternative — filtering what is on your screen while telling you the total for everything — is a
> very easy thing to build by accident and a very hard thing to notice."

**4 — Point at *Awaiting*.**

> "This column is the approval chain, live. Not a status we copied onto the leave record — the
> workflow engine's own answer to 'whose desk is this on'."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The list | `GET /api/Leaves/employee/{id}/history?year=&status=&pageNumber=&pageSize=` | self-or-`HR.Leave.Read` |
| Awaiting | `GET /api/Workflow/entity-summary` *(batched)* | *(internal)* |

---

## 6. `/hr/leave/requests/new` — raising leave on somebody's behalf

### 📍 Where you are

**From:** Requests → **+ New Request**, or an approved plan's *Raise the leave request* ·
`/hr/leave/requests/new` · as **hr.head** · **6 minutes**

### 📖 What it is

The desk's form for filing leave for somebody else. The employee's own version is chapter 19 and
has no employee picker.

### 👁 On the page

**A blue panel, when you arrived from a plan.** *Raised from an approved leave plan. The dates and
relievers are the ones planned — change them here if they have moved, and the request will still be
linked to the plan.* Chapter 12 is where that journey starts.

**Fields:** Employee picker · Leave type · **Sub-type** · Start date · End date · Reason *(required,
1000 characters)* · Reliever · Second reliever · Reliever notes · Handover notes.

⚠ **The Sub-type dropdown offers only active sub-types.** A retired one is no longer pickable, and
— more to the point — no longer accepted by the API either.

**The balance strip** appears once an employee and a leave type are chosen:

> **Can be taken now for Annual Leave: 12 days** *(entitled 21, used 7, pending 2)*
> *21 days for the full year — this leave type accrues, so 9 of them have not accrued yet.*

⚠ **This is §1.4 on the screen, and it is new.** The headline figure is the one the server
enforces; the second line appears only when the two differ. The strip used to show the policy
figure alone, so a clerk read 21, typed 21, and got a refusal quoting a number they had never seen.

**The roster note** is a blue panel that appears when the employee's pre-defined relievers have
filled a slot. It seeds **once per employee**, never over a value you have typed, never over one
you have deliberately cleared, and **never on an edit**.

**Footer, three buttons:** **Cancel** · **Save as draft** · **Submit request**.

### ▶ Walk it

**🔴 LIVE WRITE 1 — this step creates a real leave request.**

**1 — Choose the employee.** Type two letters of your anchor's surname, pick her.

**2 — Choose *Annual Leave*.** The balance strip appears. Let the room read it.

> "The moment I pick the person and the kind of leave, the form tells me what she has — and it
> leads with the number that actually binds. Twelve days she can take today; twenty-one for the
> year, because annual leave accrues monthly and it's only September. Those are two different true
> answers to 'how much leave do I have', and the form gives you both rather than picking one and
> hoping."

**3 — Open the *Sub-type* dropdown.** It is empty.

> "Empty, because annual leave has no sub-kinds. Sick leave would offer three, and the cap would
> change with it."

**4 — Set the dates.** Pick a Monday about three weeks out, and the Friday of the week after.

> "Twelve calendar days. Watch what the system charges her — it will not be twelve, because annual
> leave doesn't count weekends, and if there's a public holiday in there it won't count that
> either."

**5 — Type the reason.** `Annual leave — family visit to Kumasi.`

**6 — Look at the Reliever fields.** For your anchor, the blue roster panel is showing.

> "I did not fill these in. The employee has a **reliever roster** on her record — who covers for
> her, in order — and the form has read it, put priority one in the first slot and priority two in
> the second, and checked that neither of them is away over these dates.
>
> And it tells me it did it. A form that fills a field without saying so is a form that has started
> lying to the person using it."

**7 — Type a handover note.** `Month-end reconciliation pack is in the shared drive; Kofi has the
key to the cabinet.`

**8 — Press *Submit request*.** 🔴

A toast: *Submitted — the leave request is on its way for approval.* You land on the request's own
page at status **Pending**, with a live workflow instance.

> ⚠ **This is the button that used to do nothing.** It created the row and never started the
> approval, so the request sat in a state it could not leave. It works now: it creates, then
> submits, and if the second half fails it says *"Saved, but not submitted"* rather than claiming
> success. Do not demonstrate the old behaviour — it is gone.

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| Leave types | `GET /api/hr/leave-types?activeOnly=true` | *(none)* |
| Sub-types | `GET /api/hr/leave-types/{id}/sub-types?activeOnly=true` | *(none)* |
| Balance strip | `GET /api/Leaves/employee/{id}/balances` | self-or-`HR.Leave.Read` |
| Reliever roster | `GET /api/hr/employee-relievers/employee/{id}` | self-or-HR |
| Save / Submit | `POST /api/Leaves` then `POST /api/Leaves/{id}/submit` | **self-or-`HR.Leave.Write`** |

**The checks** are §1.5 — the eight, plus the three the closure build added. **Minimum notice and
the reliever requirement are skipped for drafts**, because a draft is not an application.

**The number.** `LV{year}{000001}`, from the tenant's atomic number sequence, restarted each
January. Under concurrency the unique index catches a collision and the service regenerates.

**The write is one transaction:** insert, then recalculate the balance.

### ⚠ Known gaps

| | |
|---|---|
| **L-15 · No attachment can be added while raising a request.** Evidence — a medical certificate — can only be attached once the request exists. The case where that matters most is sick leave, where the certificate is the reason the request is being filed | |

---

## 7. `/hr/leave/requests/[id]` — the request itself

### 📍 Where you are

**From:** any row in the register, the queue, the calendar or the Register · as **hr.head** ·
**10 minutes**

### 📖 What it is

One leave application, everything on it, and every act that can be performed on it. **This chapter
changed more than any other in the closure build** — the screen used to offer approve, reject,
close and cancel. It now also sends a request back with different dates, answers that, moves an
approved request, and records that leave is still going ahead.

### 👁 On the page

**Header.** The request number as the title; beneath it *&lt;employee&gt; · &lt;leave type&gt;*.

**On the right, a row of controls that changes with the status:**

| Control | Shown when | Does |
|---|---|---|
| *(status badge)* | always | the current status |
| **✏ Edit** | **Draft** | opens the edit form — chapter 8 |
| **Submit for Approval** | **Draft** | starts the workflow |
| **Approve** / **Reject** | **Pending** | the workflow decision |
| **↩ Send back with dates** | **Pending** | propose different dates — **new** |
| **↩ Answer the suggestion** | **Changes suggested** | accept or counter — **new** |
| **🗓 Move dates** | **Approved**, not closed | reschedule — **new** |
| **✅ Still going ahead** | **Approved**, not yet confirmed | records the answer — **new** |
| **Recall** | there is a live instance and you raised it | withdraws it to Draft |
| **✓✓ Close** | **Approved** or **InProgress** | marks the leave taken and complete |
| **⊘ Cancel** *(red)* | Draft, Pending, Changes suggested or Approved | withdraws it and releases the days |

**Two panels above the tabs, each appearing only when it applies:**

- **Sent back with different dates** *(blue)* — what was asked for, what was suggested, and the
  approver's note.
- **Moved N times** *(grey)* — what it was originally approved for, what it says now, why it moved,
  and who moved it last.

**Three tabs:** Overview · Attachments · Workflow.

**Overview** — three cards and a conditional fourth:

| Card | Rows |
|---|---|
| **Leave** | Leave type · Sub-type · Paid · Start · End · Total days · Requested on · **From a plan** *(now the plan's own reference, not just Yes/No)* · **Still going ahead** · **Attendance days marked** |
| **Employee & cover** | Employee *(name and staff number)* · Reliever · Second reliever |
| **Reason & notes** | Reason · Handover notes · Reliever notes |
| **Outcome** *(cancelled or closed only)* | Cancelled on · Cancellation reason · Closed on · Closure notes |

⚠ **"Attendance days marked: N of M"** is the attendance join made visible. When N equals M every
chargeable day reached the attendance register. When N is smaller, some of those days already
carried a real attendance observation — a punch, or a clerk's note — and leave did not overwrite
it. **Those days will not reach the payroll export as leave.**

### 7.6 The conversation — the five things that can happen to a submitted request

Worth reading once before you perform chapter 7, because the buttons only make sense together.

| From | Act | To | Who |
|---|---|---|---|
| Pending | **Approve** | Pending *(stage 2)*, then Approved | the assignee for the current step |
| Pending | **Reject** | Rejected | same |
| Pending | **Send back with dates** | Changes suggested | same |
| Changes suggested | **Answer** — accept or counter | Pending, from the top | the employee *(or HR on their behalf)* |
| Approved | **Move dates** | Pending, from the top | HR, or the employee for their own |

⚠ **Two of those re-enter approval, and that is the point.** Answering a suggestion re-submits on
the settled dates. Moving an approved request **re-opens its approval** — because an approval is an
approval *of dates*, and carrying it across to different ones would let the record claim an
authority nobody gave. The dialog says so before you confirm.

### ▶ Walk it

**1 — You are on the request you just raised. Status *Pending*. Read the Leave card.**

> "Twelve calendar days became eight chargeable days. Two weekends came out. If there had been a
> public holiday in there, that would have come out too — and if that holiday were marked
> *optional*, it would have stayed in, because the office is open."

**2 — Click the *Workflow* tab.**

> "Two approval steps. The line manager first, then HR confirms the dates. That is what was asked
> for — the supervisor decides whether the section can spare her, and HR records the final dates."

**3 — Back to Overview. 🔴 LIVE WRITE 2 — press *Send back with dates*.**

Set the start a week later, keep the same length, and type:
`The year-end valuation runs that week — the following week is clear.`

Press **Send back**.

> "I'm the manager and the dates don't work. My options used to be approve or reject — and
> rejecting somebody's leave because the week is wrong is a blunt instrument. Now I can hand it
> back with dates that do work, and she decides.
>
> Notice the status: **Changes suggested**. Not rejected. It keeps its number, it keeps its
> history, and her days have been released while she thinks about it."

⚠ **CAREFUL — the dates you suggest are checked as you propose them.** If you pick a week she
cannot afford or that clashes with other leave, you are refused here and now. That is deliberate:
the alternative is that she accepts your suggestion and *she* gets the refusal.

**4 — 🔴 LIVE WRITE 3 — press *Answer the suggestion*.**

Leave it on **Accept their dates** and press **Accept and resubmit**.

> "She's agreed. The request moves to the new dates and goes back for approval — from the top,
> because the dates changed and nobody has approved these ones yet."

**5 — 🔴 LIVE WRITE 4 — press *Approve*. Then read the status.**

**It still says *Pending*.**

> "That's rule one. I've cleared the line manager's step. HR hasn't confirmed yet — and in this
> database I happen to be both, so I'll do it again."

**6 — 🔴 LIVE WRITE 5 — press *Approve* again.** Status becomes **Approved**.

**7 — Read *Attendance days marked*.**

> "Eight of eight. This is the part that matters most and is the hardest to see. Approving leave
> didn't just change a status on a leave record — it wrote eight days onto her **attendance**
> record, marked *On Leave*, each one pointing back at this request.
>
> Why does that matter? Because the monthly attendance summary counts those days, and the
> **payroll export reads the monthly summary**. Before this, approved leave was invisible to
> payroll unless a clerk hand-edited every single day."

**8 — 🔴 LIVE WRITE 6 — press *Still going ahead*.**

> "Three weeks between approving leave and the leave starting. Nobody was ever asked whether it's
> still happening, and nothing recorded the answer. Now the system asks — that's the reminder
> engine in chapter 18 — and this is where the answer lands."

**9 — 🔴 LIVE WRITE 7 — press *Move dates*.** Push the start two days later, reason:
`Client audit brought forward a week.`

Read the amber panel in the dialog before confirming.

> "It tells me before I commit: this re-opens the approval. The leave was approved for *those*
> dates, not for any dates. So it goes back through — and it keeps its number, its history and the
> reason it moved. The alternative, which is what most systems do, is cancel it and key it again —
> and then the number's gone, the approval's gone, and nobody can tell it ever moved."

Confirm. Status returns to **Pending**, and a grey **Moved once** panel appears showing the
original dates.

**10 — Approve twice more to put it back to Approved.** 🔴 **LIVE WRITES 8 and 9.**

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| Submit | `POST /api/Leaves/{id}/submit` | self-or-`HR.Leave.Write` |
| Approve / Reject | `PUT /api/Leaves/{id}/approve` · `/reject` | **not permission-gated** — the engine's assignee check is the gate, plus a refusal if the actor *is* the employee |
| Send back | `PUT /api/Leaves/{id}/suggest-changes` | same |
| Answer | `PUT /api/Leaves/{id}/respond-suggestion` | self-or-`HR.Leave.Write` |
| Move dates | `PUT /api/Leaves/{id}/reschedule` | self-or-`HR.Leave.Write` |
| Still going | `PUT /api/Leaves/{id}/confirm-observance` | self-or-`HR.Leave.Write` |
| Close / Cancel | `PUT /api/Leaves/{id}/close` · `/cancel` | `HR.Leave.Write` / self-or-write |

**Why approve is not permission-gated:** the approver is whoever the workflow engine assigned,
often a line manager with no HR permission at all. Gating it on a permission would refuse the very
people it is for. The service refuses everybody else per request.

**The attendance invariant.** `OnLeave` days exist for a request **if and only if** its status is
Approved, InProgress or Completed. Every transition above calls one reconciler, and the nightly
sweep calls it again over recently-changed requests — because a status can also be changed through
doors the leave module does not own, and a list of hooks is a list somebody stops maintaining.

⚠ **Reversal hard-deletes those attendance rows.** The unique index on
`(TenantId, EmployeeId, AttendanceDate)` is not filtered on `IsDeleted`, so a soft delete would
leave an invisible row holding that date and the employee's next punch on it would fail.

---

## 8. `/hr/leave/requests/[id]/edit` — amending a draft

### 📍 Where you are

**From:** a Draft request → **✏ Edit** · as **hr.head** · **2 minutes**

### 📖 What it is

The same form as chapter 6, seeded with the request, with one button instead of three.

### 👁 On the page

Identical fields to the new-request form, with two differences:

- the footer has **Cancel** and **Save changes** only;
- **the reliever roster does not re-seed.** A saved request's relievers are what was agreed;
  re-reading the roster would silently rewrite the record from master data that has moved on.

### ▶ Walk it

**1 — Open a Draft and press Edit. Change the end date by one day. Save.**

> "Only a draft can be edited. Once it has been submitted, the way to change the dates is to send
> it back or to move it — both of which leave a trail. An approved request that could be quietly
> edited would make the approval meaningless."

### ⚙ Behind the page

`PUT /api/Leaves/{id}` → `UpdateDraftAsync`, self-or-`HR.Leave.Write`. **Draft only** — the service
refuses anything else.

⚠ **Moving a draft onto a retired leave type is refused**, with the type named. The check is on
what you are choosing, not on what the draft already had.

---
## 9. `/hr/leave/calendar` — leave as time

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Calendar** ·
`/hr/leave/calendar` · as **hr.head** · **5 minutes**

### 📖 What it is

Who is away, and when. One component with three audiences, chosen by a dropdown: **Everyone**, **My
team**, **Mine**. The employee's own version lives at `/me/leave/calendar` and has no dropdown.

> **Say this:**
>
> "Every leave screen so far has been a list of rows. This is the same information as *time*, which
> is how anybody planning cover actually thinks about it. Two people off the same week is obvious
> here and invisible in a table."

### 👁 On the page

**Filters card:** **Whose leave** *(Everyone · My team · Mine)* and **Leave type** *(all, or one)*.

**The calendar card:**

| Control | Does |
|---|---|
| **‹ ›** | previous / next month |
| the month name | between the arrows |
| **Today** | jumps back to the current month |
| **List / Month** | switches the view |

**The legend** sits under the header: one swatch per leave type present in the month, named, plus
a dashed swatch labelled **Not yet approved**.

**The grid** is a Monday-first month, six rows of seven, with the leading and trailing days of the
neighbouring months dimmed. Each cell carries:

- the day number;
- **the holiday name**, small and grey, when the day is a public holiday — and the whole cell is
  shaded;
- up to **three bands**, one per person away that day, coloured by leave type;
- **+N more** when there are more than three.

**A band is solid when the leave is approved and a dashed outline when it is not.** Clicking one
opens the request.

**Today's cell carries a ring.**

**The list view** is the same data as rows: swatch, employee, leave type, dates, days, a status
badge for anything not yet approved, and the request number as a link.

⚠ **Colour comes from the leave type's own *Calendar colour* setting.** A type with none falls back
to a generated palette. This is the setting whose form has said *"used on leave calendars"* since
the port, with no calendar to use it.

### ▶ Walk it

**1 — Land on it in the current month, scope *Everyone*.**

> "Everybody who is away this month, coloured by the kind of leave."

**2 — Point at a shaded cell with a holiday name.**

> "The grey days are public holidays, and they're drawn *underneath* the leave rather than beside
> it. That's deliberate — it's the answer to 'why did a five-day request only cost her four days'.
> The answer is sitting right there on the grid."

**3 — Point at a dashed band.**

> "Dashed means asked for, not yet approved. A manager looking at next month needs to see both —
> what's agreed and what's coming — because the second one is what they still have a say over."

**4 — Switch *Whose leave* to *My team*.**

> "The same calendar, narrowed to the people who report to me, plus me. This is the one place in
> the leave module where a manager reads down their reporting line — and it's deliberate, because
> arranging cover is impossible otherwise. Note what a band carries: a name, a kind of leave, and
> dates. Not the reason. The manager needs to know she's away, not why."

**5 — Switch to *List*, then back.**

> "Same data, two shapes. The list is what you read down; the month is what you plan against."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The calendar | `GET /api/Leaves/calendar?from=&to=&scope=&leaveTypeId=&organizationUnitId=` | **per scope** — see below |

**The scope is authorized three different ways, and that is the whole design:**

| Scope | Gate |
|---|---|
| **Mine** | a linked employee record, and nothing else |
| **My team** | a linked employee record. The subject is taken **from the token**, never from a query parameter — otherwise "my team" would become a way to read anybody's reporting line |
| **Everyone** | `HR.Leave.Read` |

**What it shows:** requests overlapping the window at Approved, Pending, InProgress or Completed.
Draft, rejected and cancelled leave is not time anybody is away.

**Holidays** come from `IHrWorkingDayCalculator` — the same component that decides chargeable days
— so the calendar and the arithmetic cannot disagree.

⚠ **The range is capped at 400 days** per request, so one call cannot ask for every leave row the
tenant has ever had.

---

## 10. `/hr/leave/register` — every request, and a spreadsheet

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Register** ·
`/hr/leave/register` · as **hr.head** · **4 minutes**

### 📖 What it is

The organisation-wide list of leave requests, filterable six ways, with a CSV button. Until it
existed the only listing was one employee at a time, so *"every rejected request this quarter"* or
*"who is off in December"* could not be asked at all.

### 👁 On the page

**Header.** Title *Leave Register*, subtitle *Every leave request across the organisation.* One
button: **⬇ Export CSV**.

**Filters card**, six controls:

| Control | Notes |
|---|---|
| **From** / **To** | a date window. **Overlap, not containment** — a request running from December into January appears in both |
| **Status** | including *Changes suggested* |
| **Leave type** | |
| **Employee** | the picker |
| **Search** | request number, first or last name, or staff number |

Every change resets to page 1.

**The table:** Number · Employee *(with staff number underneath)* · Leave type *(and sub-type)* ·
From · To · Days · Status. Rows are clickable and open the request.

**Paging** at 25 a page, with the total above.

### ▶ Walk it

**1 — Land on it. Read the count.**

> "Every leave request in the organisation for this year. Four hundred and something rows."

**2 — Set Status to *Rejected*.**

> "Every rejected request this year, in one click. That is a question nobody could ask this system
> a week ago, and it is the kind of question that comes up the moment somebody alleges they were
> treated differently from a colleague."

**3 — Clear it, set the dates to December.**

> "Who is off over Christmas. The calendar answers this beautifully for one month at a time; the
> register answers it as a list you can sort, count and send to somebody."

**4 — Press *Export CSV*.** Nothing is written — an export only reads. Open the file.

> "Thirteen columns, and it opens in Excel. Every leave conversation ends with somebody asking for
> a spreadsheet, and the honest options are to build the export or to have people retype it."

⚠ **A small thing worth one sentence if the room has auditors in it:**

> "One detail you would never notice unless it went wrong: if somebody's name or a note begins with
> an equals sign, Excel treats it as a formula. The export escapes those. It is two lines of code
> and it is the difference between a spreadsheet and an attack."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The list | `GET /api/Leaves/register?from=&to=&status=&leaveTypeId=&employeeId=&organizationUnitId=&search=&pageNumber=&pageSize=` | `HR.Leave.Read` |
| Export | `GET /api/Leaves/register/export` *(same filters)* | `HR.Leave.Read` |

**Org-wide, so it carries the read tier** rather than self-or-permission — there is no per-employee
reading of this screen. An employee's own history is still `employee/{id}/history`.

**The paged read and the CSV share one query builder**, so the spreadsheet and the screen cannot
disagree about what matches. **The export is capped at 10,000 rows** and says so in the file when
it has been reached, rather than truncating silently.

**Two more exports exist** and are covered where they live: balances (chapter 13) and compliance
(chapter 16).

---

## 11. `/hr/leave/approvals` — what is waiting on you

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Approvals** ·
`/hr/leave/approvals` · as **hr.head** · **6 minutes**

### 📖 What it is

Leave requests at a step **you** own, with the ability to decide several at once. **This screen was
rebuilt in the closure build** and behaves differently from the one the old guide described.

### 👁 On the page

**Header.** Title *Leave Approvals*, subtitle *Requests waiting on a manager's decision.* One
button: **Open approval inbox** → `/workflow/inbox`, the cross-module queue.

**An explanatory panel:**

> Everything here is waiting on **you** — the workflow engine decides what that means, so a request
> appears once it reaches a step you own and leaves the moment you decide it. Leave is approved in
> two stages: the line manager first, then HR confirms the dates. Open a request to see where it
> sits, or select several to decide them together.

**Pending requests card.** When rows are selected, the header grows a count and two buttons:
**👍 Approve** and **👎 Reject**.

**The table:** a **select-all checkbox**, then per row a checkbox, Request · Employee · Leave type ·
From · To · Days · Status. Clicking a row opens the request; clicking its checkbox does not.

**The bulk dialog** lists exactly what you are about to decide — number, employee, dates — with a
comment box *(optional for approve, **required** for reject)*.

⚠ **There is no manager picker any more.** It used to ask you to choose a manager and then list
that manager's direct reports' pending requests. That answered a different question from the one
the screen asks, and on a two-stage workflow it was wrong in both directions: it kept showing a
manager what they had already approved, and **never showed HR the confirmation step at all**,
because HR is nobody's line manager.

### ▶ Walk it

**1 — Land on it as `hr.head`. Read the panel aloud.**

**2 — Point at the list.**

> "Everything here is at a step I own. Not 'my department's leave' — the engine's own answer to
> 'whose desk is this on'. When I decide one, it leaves this list. If it's a two-stage approval and
> I've only cleared the first, it comes back — at HR's step, which in this database is also me."

**3 — Select three rows with the checkboxes.** The header grows the two buttons.

**4 — 🔴 LIVE WRITE 10 — press *Approve*, read the dialog, confirm.**

> "A December approval queue is two hundred rows. Deciding them one page-load at a time is how
> people end up approving without reading.
>
> What the system is doing underneath is the important part: it is **not** doing anything clever.
> It decides each one separately, exactly as if I'd opened it and pressed the button — same
> authorization check, same workflow step, same audit entry. If one of them can't be approved, the
> other two still go through and it tells me which failed and why."

**5 — Read the toast.**

If any failed you get *"2 of 3 processed — 1 could not be: …"* with the reason. Say it out loud.

> "Two went through, one didn't, and it told me why rather than claiming success."

**6 — The moment worth waiting for.** Find a request **raised for `hr.head` themselves** — or raise
one in the portal window first — and try to approve it.

It is refused: *You cannot approve your own leave request. It has to be approved by someone else.*

> "That's the one rule you'd hope was there. I'm the head of HR, I hold every role in this module,
> and I cannot approve my own leave. Not hidden — refused, with a reason.
>
> And note *how* it's done. The obvious way is to tell the workflow engine 'the person who raised
> it can't approve it'. That's wrong here, because HR raises leave **for other people** all day —
> switch that on and every request HR files gets stuck with nobody able to clear it. So the rule is
> about the person the leave is *for*, not the person who typed it."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The queue | `GET /api/Leaves/my-approvals?pageNumber=&pageSize=` | any authenticated user — the answer is only ever about the caller |
| Bulk approve | `POST /api/Leaves/bulk-approve` | **not permission-gated** — each item is authorized on its own |
| Bulk reject | `POST /api/Leaves/bulk-reject` | same; a reason is required |

**How the queue is built.** Candidate requests are everything at *Pending* in the tenant — not just
the caller's direct reports — minus the caller's own leave. Each is then put to the workflow
engine's `CanUserApproveAsync`. If **no** definition is published, the engine refuses everybody, so
the queue instead mirrors what the fallback authority permits.

⚠ **The engine answers one request at a time**, so the candidate scan is **capped at 300**. That is
several times the largest real queue, but it is a cap and worth knowing.

**Bulk loops the real service call**, capped at 50. That is deliberate: the platform's generic
approval surfaces drive the workflow engine *without* invoking the module's status adapter, which
completes the workflow and leaves the record stuck at Submitted. A bulk path that reached for the
engine directly would reproduce that at scale — and would check authorization once per batch
instead of once per item.

---
## 12. `/hr/leave/plans` — the year, before it happens

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Plans** · `/hr/leave/plans` ·
as **hr.head** · **7 minutes**

### 📖 What it is

Intentions, not applications. A plan says *"I mean to be away then"*; nothing is reserved and no
days are deducted. It is the answer to the question every head of department asks in January — *who
is going to be away, and when?* — and, since the closure build, **a plan can now become the actual
request** rather than dead-ending.

### 👁 On the page

**A year selector** above the table.

**The table:** Employee · Leave type *(and sub-type)* · From · To · **Suggested** · Reliever ·
**Request** · Status.

⚠ **The *Request* column is new.** When a plan has been turned into a leave request it shows that
request's number as a link. When it has not, `—`. That column is `LeaveRequest.LeavePlanId`, a
foreign key that existed since the port and that nothing ever wrote.

**The row menu:**

| Action | Shown when |
|---|---|
| **Raise the leave request** | **Approved**, and no live request raised from it yet — **new** |
| **Submit for approval** | Draft |
| **Approve** | Submitted or ChangesSuggested |
| **Reject…** | Submitted or ChangesSuggested — required reason |
| **Suggest different dates…** | Submitted |
| **Accept suggested dates** | ChangesSuggested, and a suggestion exists |
| **Decline suggestion** | ChangesSuggested |
| **Cancel plan** *(red)* | anything not already Cancelled or Rejected |

**The add / edit dialog:** Employee · Leave type · **Sub-type** · Start · End · Reliever · Second
reliever · Notes.

⚠ **The Sub-type field is new.** The payload always carried `leaveSubTypeId` and the dialog never
offered it, so a plan could not say which variant of leave it was for. It appears only when the
chosen type has sub-types.

⚠ **A plan's year now comes from its own start date.** It used to be taken from the list filter, so
a January plan raised while you were looking at December was filed under the wrong year and then
shown by neither.

**The live clash check** sits under each reliever picker and is still the best control on the
screen. As soon as a reliever and both dates are set it asks the server:

- *Checking the reliever's diary…*
- ✅ *Reliever: nothing in their diary over these dates.*
- ⚠ *Reliever: 2 clashes over these dates*, with a bulleted list, and beneath it: **You can still
  save the plan — this is a warning, not a rule.**

### ▶ Walk it

**1 — Set the year to this year.** Your anchor's December break is there.

> "A leave plan is an intention. Nobody has asked for anything yet, no days have been reserved, and
> nothing has been deducted."

**2 — 🔴 LIVE WRITE 11 — press *Add leave plan*** and fill in an employee, Annual Leave, two weeks
in a busy month.

**3 — In the *Reliever* box, deliberately pick somebody already committed.**

The amber clash panel appears.

> "It has just read that person's diary — their own leave plans, their own live requests, and any
> other plan that already names them as cover — and it is telling me they are not free.
>
> And then it says: *you can still save this.* Which is right. The system does not know that Kofi's
> leave is about to be cancelled, or that two people can cover between them. It knows something I
> should see, and it shows me, and then it gets out of the way."

**4 — Save. Submit it. Approve it twice** *(two stages, as everywhere)*.

**5 — Open the row menu on the now-Approved plan. Press *Raise the leave request*.**

You land on the new-request form, **pre-filled** — employee, leave type, dates and both relievers —
with a blue panel saying it came from a plan.

> "This is the step that was missing. The plan was agreed in January; in November somebody has to
> actually apply for it. Before, they retyped dates they had already agreed, into a blank form, and
> nothing connected the two records.
>
> Everything here is still editable, because a plan is an intention and intentions move. But the
> request will carry the plan's id, and the plan will show the request's number."

**6 — Go back to Plans without saving. Point at the *Request* column.**

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The list | `GET /api/hr/leave-plans?year=` | `HR.Leave.Read` |
| Create / edit | `POST` · `PUT /api/hr/leave-plans/{id}` | self-or-`HR.Leave.Write` |
| Submit / Approve / Reject | `PATCH …/submit` · `/approve` · `/reject` | the engine's assignee |
| Suggest / Respond | `PATCH …/suggest-changes` · `/respond-suggestion` | assignee / self |
| Clash check | `GET /api/hr/leave-plans/reliever-clashes` | `HR.Leave.Read` |

**`PlannedBy` is stamped from the token** — it is an `Employees` foreign key, and both screens used
to send the login's user id, which is never an employee id.

**Raising the request is guarded four ways**: the plan must exist in this tenant, belong to that
employee, be **Approved**, and not already have a live request raised from it. A cancelled or
rejected request does not spend a plan — it becomes raiseable again.

---

## 13. `/hr/leave/balances` — the ledger

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Balances** ·
`/hr/leave/balances` · as **hr.head** · **6 minutes**

### 📖 What it is

Every balance in the organisation, with all of the working shown. Eleven columns, nine of which are
the arithmetic of the last two.

### 👁 On the page

**Header.** Two buttons: **⬇ Export CSV** and **↻ Recalculate**.

**Filters:** Year · Employee · Leave type.

**The table:**

| Column | |
|---|---|
| Employee *(with unit underneath)* · Leave type *(and sub-type)* | |
| **Entitled** | what the policy grants for the year |
| **Accrued** | what has actually accrued to date |
| Carried over · Adjustments · Used · Pending · Encashed | the working |
| **Available** | the policy figure |
| **Can take now** | **the figure the create check enforces** |

⚠ **Two columns here are new or fixed.** *Accrued* used to repeat *Entitled* — the org-wide read
never computed accrual, so the one screen that showed the column showed the wrong number. And *Can
take now* did not exist at all, which is why §1.4 was the module's most confusing feature.

**Recalculate** with no employee chosen refuses: *Choose an employee — Recalculation runs for one
employee at a time.*

### ▶ Walk it

**1 — Arrive unfiltered.** Let the table sit for a second.

> "Every balance in the Corporation for this year. One row per person, per kind of leave. And look
> at the shape of it — eleven columns, and nine of them are the *working* of the sum."

**2 — Read one row across, slowly.** Your anchor's Annual Leave row.

> "Entitled twenty-one — what the policy grants. Accrued fifteen — what has actually built up by
> today, because annual leave accrues monthly. Carried over, nothing. Adjustments, plus twenty-one,
> and I'll come back to that. Used, eight. Pending, seven. Encashed, nothing.
>
> Then two totals, and the difference between them is the single most useful thing on this screen.
> **Available** is what the year owes her. **Can take now** is what she can actually book today.
> They differ by the part of the year that hasn't accrued yet.
>
> There is no screen in this product where anyone can type into either of those columns. If one is
> wrong, one of the nine to its left is wrong, and you can see which."

**3 — The adjustment. Be honest; it plays well.**

> "Plus twenty-one in Adjustments. Where does that come from?
>
> This database was built as a new system on day one, with no history. So the opening balances were
> **loaded as adjustments** — one entry per person, per leave type, with a reason: *'Annual
> entitlement — opening balance'*, and the name of whoever loaded it.
>
> That is exactly what you will do on go-live. You will not import a balance into a column; you
> will post an opening entry that says where the number came from, so that in three years, when
> somebody disputes their days, the trail goes all the way back to the migration."

**4 — Press *Export CSV*.** Open it.

> "Fourteen columns, including both availability figures. If somebody wants to check our arithmetic
> against theirs, this is how they do it."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The table | `GET /api/Leaves/balances?year=&employeeId=&leaveTypeId=` | `HR.Leave.Read` |
| Export | `GET /api/Leaves/balances/export` *(same filters)* | `HR.Leave.Read` |
| Recalculate | `POST /api/Leaves/balances/recalculate` | `HR.Leave.Write` |

**Accrual is computed live on every read**, never stored. **Both availability figures come from one
server-side definition**, which the create check also calls.

### ⚠ Known gaps

| | |
|---|---|
| **L-19 · Recalculate is one employee at a time**, by design, with no organisation-wide run. Reasonable, but a policy correction applied to 900 people has no route through the UI | |

---

## 14. `/hr/leave/adjustments` — corrections, with a name on them

### 📍 Where you are

`/hr/leave/adjustments` · as **hr.head** · **6 minutes**

### 📖 What it is

Every manual correction ever made to anybody's leave balance. Not a log of changes — the actual
records, of which the balance is the sum.

### 👁 On the page

**Filters:** Employee · Leave type · Year.

**The table:** Employee · Leave type · Year · **Days** *(signed, green positive / red negative)* ·
Reason code · Remarks · Date · **By**.

**Delete** on a row is `HR.Leave.Admin` — a 403 for `hr.head`.

**The add dialog:** Employee · Leave type · Year · **the balance preview** · Days *(step 0.5, may
not be zero)* · Adjustment date · Reason code · Remarks *(required)*.

**The balance preview** updates live:

```
  Current balance · Annual Leave 2026                      [ 28 available ]
  Entitled   Accrued   Carried over   Adjustments   Used   Pending   Encashed
     21        15            0             21         8       7          0

  After this adjustment: 30.5 available.
```

Below zero it turns **red** — a warning, not a block. With no balance row yet, a dashed panel
explains that saving creates one at the type's default entitlement first.

### ▶ Walk it

**1 — Arrive and filter to your anchor.** The opening-balance rows are there.

> "Every correction ever made to anybody's leave balance. Read the last column. *By.* Every
> adjustment carries the name of the person who posted it, and there is no way to post one without
> a reason code and a remark."

**2 — 🔴 LIVE WRITE 12 — press *Add adjustment*.** Your anchor, Annual Leave, `+1.5`, reason code
*Correction*, remarks `Public holiday incorrectly charged — corrected after review.`

Let the room read the preview before you save.

> "It shows me the balance now, and the balance after. Before I commit, not after."

**3 — Save, and point at the new row.**

> "One line, signed, reasoned, dated and attributed. If this were a spreadsheet, that correction
> would be a changed cell and nobody would ever know."

### ⚙ Behind the page

`GET` / `POST /api/Leaves/adjustments` · `DELETE …/{id}` is Admin.

**`PerformedBy` is stamped from the token.** Both screens used to send the login's user id into an
`Employees` foreign key, so **every adjustment raised from a screen had failed on the constraint** —
the table was empty and the endpoint returned 500. An unlinked account is now refused with a
sentence rather than a constraint error.

---

## 15. `/hr/leave/encashments` — days into cash

### 📍 Where you are

`/hr/leave/encashments` · as **hr.head** · **5 minutes**

### 📖 What it is

Converting untaken leave into money. Four checks, a server-derived payout, and a lifecycle that
ends in *Processed* — which is the only status that moves a balance.

### 👁 On the page

**Filters:** Year · Status.

**The table:** Employee · Leave type · Year · Days · **Amount** · Status · Processed on ·
Reference · row actions.

**Row actions:** **Approve** *(Submitted / PendingApproval)* · **Reject…** *(required reason)* ·
**Mark as paid…** *(Approved — opens a required payment-reference dialog)*.

⚠ **"Mark as paid" works.** It used to send the login's user id into an `Employees` foreign key and
fail on the constraint every time, so the action was unusable. The processor is now stamped from
the caller's own employee record, and the field has been removed from the request body altogether —
so every caller, not just this screen, is fixed.

### ▶ Walk it

**1 — Arrive on this year.** One row, *Processed*.

> "One encashment, already paid. Read the amount — nobody typed it. The employee asked to convert
> days; the system worked out what those days are worth from her salary and the allowances the
> leave type is linked to."

**2 — Walk the lifecycle without pressing anything.**

> "Requested, approved — twice, like everything else here — then marked paid with a reference.
> Only that last step moves the balance. Approving an encashment doesn't take the days; paying it
> does."

**3 — Say the honest thing about Finance.**

> "And here is a boundary worth naming. This is real money leaving the company, and HR does **not**
> post it to the general ledger. HR records the event — who, how many days, how much, when, on
> whose signature — and Finance posts it in one sweep, once, for every module. Two systems posting
> the same money is how reconciliations turn into archaeology."

### ⚙ Behind the page

`GET /api/hr/leave-encashments?year=&status=` · `PATCH …/{id}/approve` · `/reject` · `/process`.

**The payout is derived server-side** from the employee's emoluments and the leave type's rate
policy. The caller cannot assert it.

### ⚠ Known gaps

| | |
|---|---|
| **L-20 · No encashment detail page and no audit of the rate.** The row shows the amount, not the rate, the basic pay, or which allowances fed it. Anybody disputing the figure has nothing to read | |
| **⚠ Two open questions with TDC, and both matter here.** **L-D7** — there are **two daily-rate formulas** live in the product: leave encashment uses *(basic + linked allowances) ÷ 22*, the separation settlement uses *monthly × 12 ÷ 365*. On GHS 6,000/month those differ by about **38%**. **L-D8** — FR-HR-046 says leave is encashed *"only on exit"*, and this in-service path exists anyway. **Do not assert either number as settled in front of TDC** | |

---

## 16. `/hr/leave/compliance` — who still owes their leave

### 📍 Where you are

`/hr/leave/compliance` · as **hr.head** · **4 minutes**

### 📖 What it is

Employees who have not taken the leave they are required to take. Driven by the
`MandatoryAnnualLeave` flag on a leave type.

### 👁 On the page

**A year selector**, an **⬇ Export CSV** button *(new)*, and a table: Employee · Leave type ·
Entitled · Taken · Scheduled · **Outstanding** *(amber)* · Status.

**Three statuses:** **Compliant** *(taken ≥ entitled)* · **Scheduled** *(pending covers the rest)* ·
**Outstanding**.

### ▶ Walk it

**1 — Arrive on this year.** Rows, mostly **Outstanding**.

> "Only one leave type is flagged as mandatory in our configuration, and it is annual leave.
>
> That flag is not administrative tidiness. In most of the world an employer is required to ensure
> annual leave is actually taken, and 'the employee didn't ask' is not a defence. It is also good
> practice for another reason entirely — the person who never takes leave is a fraud risk, because
> nobody else has ever done their job."

**2 — Read one row across.**

> "Twenty-one entitled. Eight taken. Seven scheduled — an application in flight. Six outstanding,
> in amber. Status *Outstanding*, because eight plus seven is fifteen, and fifteen is not
> twenty-one."

**3 — Find a row that reads *Scheduled*.**

> "Not compliant. She hasn't taken her leave, but she has **booked** all of it — and that is a
> materially different conversation for a head of department to have in October. A compliance list
> that only shows 'taken or not taken' produces a panic in November about people who are already
> booked."

**4 — Press *Export CSV*, and say what changed.**

> "This screen used to be a list you could not do anything with — no export, no way to act on a
> row. You can at least take it away now. And from this year, the system chases these rows itself:
> from September it starts reminding people who still owe leave. That's chapter 18."

### ⚙ Behind the page

`GET /api/Leaves/compliance?year=` · `GET /api/Leaves/compliance/export` — both `HR.Leave.Read`.

### ⚠ Known gaps

| | |
|---|---|
| **L-22 · Still read-only apart from the export.** No department filter, no link from a row to the employee or to raising leave on their behalf | |

---

## 17. `/hr/leave/year-end` — carry-over and forfeiture

### 📍 Where you are

`/hr/leave/year-end` · **as admin, in Window C** · **6 minutes**

### 📖 What it is

The two batch runs that close a leave year. The only screen in this module an ordinary HR officer
cannot open, and the only one that changes hundreds of records at once.

### 👁 On the page

**An amber warning panel** at the top, spelling out what each run does.

**Two cards**, each with a year selector, an optional **Employee** picker to scope the run, and a
button — **Run carry-over** and **Run forfeiture** *(red)*.

**Both are behind a confirmation dialog** that spells out the scope in words:

> *Unused days from 2025 will be carried into 2026 for every employee.*
> *Expired days in 2025 will be removed for every employee. This cannot be undone automatically.*

**After a run, a results panel:** badges for processed / affected / days, then a scrollable list of
per-balance notes.

### ▶ Walk it

⚠ **Switch to Window C (admin) now.** `hr.head` cannot see this menu item and cannot run either job.

**1 — Arrive and read the amber panel out loud.** Do not skip it; it is the point.

> "This is the only screen in the leave module that changes hundreds of records at once, and the
> system says so before it lets you near the buttons.
>
> Notice also that it is not on the leave clerk's menu at all. Running the leave desk and closing
> the leave year are different privileges. The person who approves your leave cannot rewrite
> everybody's balance."

**2 — 🔴 LIVE WRITE 13 — scope a carry-over to one employee and run it.**

Set **From year** to last year, pick **one** employee, press **Run carry-over**, read the
confirmation aloud, confirm.

> "One employee, last year into this year. And look at what it reports — how many balances it
> looked at, how many it actually changed, and how many days moved, with a line per balance.
>
> This is how you would do it for real. Run it for one person, check the number against what you
> expected, then run it for everybody."

**3 — Do not run forfeiture. Say why.**

> "I'm not going to run the second one. Forfeiture removes days, and on this database it would post
> a negative adjustment against every balance in the tenant. There is no undo."

**4 — The question that always comes, and the honest answer.**

> "Why isn't this automatic? Every other dated thing in this system is swept nightly.
>
> Because these two **move people's entitlements**. Every other sweep sends somebody a message.
> Whether the company should silently delete leave days on the 31st of March is a policy decision,
> and it is not ours to make by writing a timer. What the system *does* do — from this year — is
> warn people a month before their carried-over days lapse. It tells; it doesn't take."

### ⚙ Behind the page

`POST /api/hr/leave-year-end/carry-over` · `/forfeiture` — both **`HR.Leave.Admin`**.

**Carry-over is set-not-stacked** and capped at the leave type's `MaxCarryOverDays`. **Forfeiture
posts a named negative adjustment** and is idempotent.

**`PerformedBy` on a forfeiture is stamped from the token.** It used to be `Guid.Empty`, which no
employee has — so the forfeiture endpoint had never once succeeded, and its only test evidence was
a 403 check, so the insert had never run.

---
## 18. The reminder engine — the screen that isn't

### 📍 Where you are

**No sidebar entry, no page.** `api/hr/leave/reminders/*` · as **admin** · **4 minutes**

### 📖 What it is

A nightly sweep that watches five kinds of date and chases each one, once. HR has twelve of these
engines; leave — the module with more dates that matter than any of the others — had none until the
closure build.

> **Say this:**
>
> "Everything you have seen so far needed somebody to open a screen. This one doesn't. It runs every
> night, looks for dates that are about to matter, and tells the right person — once."

### 👁 What it watches

| Kind | Fires when | Window |
|---|---|---|
| **Leave starting soon** | approved leave is about to start and **nobody has confirmed it is still going** | 7 days |
| **Leave not closed** | leave ended and was never closed | 2 days after the end date |
| **Request awaiting a decision** | a request has sat undecided since it was raised | 5 days |
| **Mandatory leave outstanding** | somebody still owes statutory leave | from **month 9** |
| **Carry-over expiring** | carried days are about to lapse | 30 days |

⚠ **Those five windows are our numbers, not TDC's.** Each is a one-line change and each is logged
for confirmation as **L-D9**. Do not present them as policy.

⚠ **A 90-day backlog floor** stops the first sweep on an established database queuing years of
history. A comparable engine's first live run queued 275 reminders, of which 242 were history.

### 👁 The four endpoints

| Endpoint | Does |
|---|---|
| `POST api/hr/leave/reminders/run` | forces a sweep now. **Safe to repeat** |
| `GET …/preview?asOf=` | what a sweep *would* fire, **claiming nothing and sending nothing** |
| `GET …/runs?count=` | recent sweeps, newest first |
| `GET …/log?days=` | what has actually been dispatched |

All four are **`HR.Leave.Admin`** — nothing here is one person's own record, the log names people
across the whole tenant, and forcing a sweep is an administrative act.

### ▶ Walk it

**1 — In Window C (admin), call the preview endpoint.** Read a few rows aloud.

> "This is what the system *would* chase tonight, without chasing it. Four people whose leave starts
> next week and who nobody has asked whether they're still going. Two requests that have sat with a
> manager for over a week. Eleven people who still owe annual leave and it's September."

**2 — Call `run`, then `log`.**

**3 — Call `run` again, and read the result.** `remindersQueued` is **0**; `alreadySent` is not.

> "That is the whole engineering problem with reminders, solved. It ran again and sent nothing,
> because every one of those had already been sent.
>
> How? Each candidate produces a key — which record, which kind of reminder, which date, which
> escalation rung — and the key is written to the database **before** the notification goes out,
> with a uniqueness constraint on it. If two sweeps race, one of them loses on the constraint rather
> than both sending.
>
> Get that wrong in the other direction — send first, record afterwards — and a crash between the
> two means somebody gets the same reminder every night forever. That is the single fastest way to
> teach an organisation to ignore your system."

**4 — Say what it deliberately does not do.**

> "One more thing, and it is the line I would defend hardest. This engine **warns**. It does not
> move a single day. Carry-over and forfeiture — the two things that actually change what somebody
> is owed — are still the manual runs on chapter 17's screen, deliberately not on a timer.
>
> The engine will tell you a month out that Ama's five carried days lapse on the 31st of March. It
> will not lapse them."

### ⚙ Behind the page

`LeaveReminderService` → `LeaveReminderRuns` *(one row per sweep)* + `LeaveReminderDispatchLogs`
*(one row per reminder sent)*, with a **unique index on `(TenantId, DedupeKey)`** that is the
send-once guarantee rather than an optimisation.

Hosted daily by `LeaveReminderBackgroundService`, staggered 17 minutes behind the other engines so a
cold start does not run them all at once. It takes a distributed lock, so only one instance sweeps.

**Notifications carry the leave type and the request number and nothing else** — no reason, no
diagnosis, no balance. A reminder travels further than the record it is about, and sick leave makes
that a confidentiality matter rather than a matter of taste.

**The same sweep also reconciles attendance.** After the reminders, it checks that every
recently-changed leave request's attendance days match its status, and repairs any that do not —
logging a **warning** when it has to, because drift means something changed a leave status without
going through the leave service.

---

## 19. The portal — seven screens, and the employee's own view

### 📍 Where you are

**Window B**, signed in as your anchor · `/me/leave` · **10 minutes**

### 📖 What it is

The same data, the same tables, the same endpoints — asked for completely differently. **There is no
employee picker anywhere in this chapter**, and that is the point.

### 19.1 `/me/leave` — My Leave

Balance cards per leave type, a history list, and four buttons: **Calendar** *(new)* · **Planner** ·
**Encashments** · **New request**.

> "This is the same data, from the same tables, through the same endpoints. What is different is
> everything about how it is asked for — the employee never chooses an employee, because the only
> record they can reach is their own, and that is enforced on every single call, not by hiding a
> dropdown."

### 19.2 `/me/leave/calendar` — My leave calendar *(new)*

The chapter 9 component at scope **Mine**, with no scope selector. Her own leave, with public
holidays drawn underneath.

> "Her year, as time. And the grey days are the public holidays — which is how she works out that
> the week she wants only costs her four days, before she asks for it."

### 19.3 `/me/leave/new` — Request leave

The same fields as the desk form **minus the employee picker**, plus three differences worth
pointing out:

| Difference | What it looks like |
|---|---|
| **Notice is stated up front** | *"This leave type needs at least 14 days notice before it starts. Drafts can be saved any time."* |
| **Relievers are a dropdown, not a search** | the options are her **own roster**, labelled *"Kofi Asante (priority 1)"* |
| **When the roster is empty, it says what will happen** | *"You have no pre-defined relievers, so one will be assigned automatically when you submit — usually your manager."* |

▶ **Walk it. 🔴 LIVE WRITE 14 — this is the request the room watches go all the way through.**

1. **New request** → **Annual Leave**. The balance panel appears:
   *Your balance for Annual Leave: **12 days you can take now** (entitled 21, used 8, pending 7)*
   *21 days for the full year — this leave type accrues, so 9 of them have not accrued yet.*

> "Before I have typed a date, the form has told me three things: what I can take today, what the
> year owes me, and why those are different."

2. The notice line appears beneath it.
3. Set the dates — **a Monday at least three weeks out**, to the Friday of that week.
4. Reason: `Annual leave — family visit.`
5. Open the **Reliever** dropdown.

> "As an ordinary member of staff I cannot search the whole staff directory from here. I get my own
> roster — the people already agreed as my cover. The desk can pick anybody; I cannot, and I should
> not be able to."

6. **Submit request.**

> "Created and submitted in one action, and if the second half had failed it would have told me it
> was saved but not submitted — rather than claiming success and leaving me with a request nobody
> can approve."

### 19.4 `/me/leave/[id]` — her own request

Everything chapter 7 has, narrowed to what an employee may do:

| She can | She cannot |
|---|---|
| Edit a draft · Submit · Cancel · Attach a document | Approve · Reject · Send back · Close |
| **Answer suggested dates** *(new)* | |
| **Move the dates of approved leave** *(new)* | |
| **Confirm it is still going ahead** *(new)* | |

▶ **The moment worth performing.** Go back to Window A as `hr.head`, **send her request back with
different dates**, then return to Window B and refresh.

The blue **Sent back with different dates** panel is there, with the manager's note.

> "Her manager didn't reject it. He handed it back with a week that works, and told her why. She can
> take those dates, or propose her own — and either way it goes back for approval on whatever they
> settle on.
>
> The old choice was approve or reject. Rejecting somebody's leave because the *week* is wrong is a
> blunt instrument, and what actually happens in an office is a conversation. This is that
> conversation, on the record."

Press **Answer the suggested dates** → **Accept their dates** → **Accept and resubmit**.

### 19.5 `/me/leave/planner` — her own plans

Her plans for the year, with **Submit**, **Respond** when a manager has suggested changes, **Cancel**
— and, on an approved plan, **Raise the request** *(new)*, which is chapter 12's journey from her
side.

### 19.6 `/me/leave/encashments` — her own encashments

Read, and request. Approving and paying are the desk's.

### 19.7 `/me/leave/[id]/edit`

Draft only, same rules as chapter 8.

### ⚙ Behind the page

Every call is the **same endpoint** the desk uses. The narrowing is `CanActForEmployeeAsync` /
`CanActOnRequestAsync` on the server, not a hidden dropdown on the client.

> **Say this, because it is the security story in one sentence:**
>
> "If she opened the browser's developer tools and changed the employee id in the request to
> somebody else's, she would get a 403 — not a different person's leave. The screen isn't what's
> protecting that record."

---
## 20. Where leave shows up outside its own menu

**Six places now, and one of them is new enough to be the best thing in the book.**

| Where | What it shows | Route |
|---|---|---|
| **The attendance monthly summary** | a **Days on leave** column — **now populated by leave itself**, which is how payroll learns about it | `/hr/attendance/summaries` |
| **The attendance daily register** | days at status **On Leave**, each pointing back at the request that created it | `/hr/attendance/daily` |
| **The employee profile → Time & leave → Leave** | this year's balances as chips, then the request history, with **Open in Leave**. Read-only — every write is back in the leave module | `/hr/employees/[id]` |
| **The employee profile → Employment → Relievers** | the reliever roster. **The only screen that maintains it**, and priority 1 and 2 are what the leave form reads | `/hr/employees/[id]` |
| **A medical expense claim** | **Related sick leave** — the claim can name the leave it arose from | `/hr/medical/claims/[id]` |
| **The workflow inbox** | leave requests, plans and encashments awaiting *you*, beside every other kind of approval | `/workflow/inbox` |

### ▶ The cross-module moment worth performing

**Open `/hr/attendance/daily`, filter to your anchor and the week of the leave you approved.**

The days are there, marked **On Leave**.

> "This is the join that was missing, and it is worth being blunt about what it means.
>
> Before this, approving leave changed a leave record and nothing else. The attendance register
> didn't know she was away. And the attendance register is what the **payroll export** reads — so
> unless a clerk went and hand-edited every single day, payroll saw somebody who simply wasn't at
> work.
>
> Now the approval writes the days. One per chargeable day, each one pointing back at the request
> that created it, and they come off again if the leave is cancelled or moved."

⚠ **Two sentences to have ready if somebody probes it:**

> "It never overwrites an observation. If somebody actually clocked in on one of those days — which
> happens; people come in during their leave — the system leaves that day alone and tells you it
> skipped it. That's the 'eight of eight' figure on the request screen.
>
> And if a leave status ever gets changed some other way, there's a nightly reconciliation that
> puts the attendance register back in step and logs a warning, because that would mean a bug
> somewhere upstream."

**Two sentences on the employee profile:**

> "The employee record has a Leave tab, and it has no buttons. It is a window onto the leave module,
> not a second door into it — because a rule implemented from two screens is a rule implemented
> twice, and the second one drifts."

> "And the reliever roster lives on the employee's record rather than in the leave module, because
> it is a fact about the person's job — who can do it when they are not there — not a fact about any
> particular absence."

---

## 21. Reset — putting the database back

Do this after the room empties. Everything below is reversible; nothing needs a rebuild.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Request created, sent back, answered, approved twice, confirmed, moved, approved twice again** *(ch. 6–7, LW 1–9)* | Open it → **Cancel**, reason `Demonstration`. The days go straight back **and the attendance days come off with them**. ⚠ Cancel, do not close — a cancelled request is a clean withdrawal; a closed one is a completed absence that never happened |
| 2 | **Bulk approval** *(ch. 11, LW 10)* | Cancel each one you approved, as above. ⚠ Check `/hr/attendance/daily` afterwards — the attendance days should be gone. If any remain, the nightly reconciliation will remove them |
| 3 | **Leave plan created, submitted, approved** *(ch. 12, LW 11)* | The ⋯ menu → **Cancel plan**. A cancelled plan stays visible with its history, which is correct. If you also raised the request from it, cancel that first |
| 4 | **Adjustment posted** *(ch. 14, LW 12)* | **Admin only.** Window C → `/hr/leave/adjustments` → the row → **Delete**. Then check the Balances screen went back |
| 5 | **Portal request filed** *(ch. 19, LW 14)* | As the employee: open it → **Cancel request**, reason `Demonstration` |
| 6 | **Carry-over run** *(ch. 17, LW 13)* | It set next year's `CarriedOverDays` for one employee. Post a **negative adjustment** of the same size against that year, or accept it — it is one employee and it is arithmetically correct |
| 7 | **Forfeiture run**, if you ran it | Delete the `FORFEIT: unused leave (year-end/cut-off)` adjustment, as admin. ⚠ **Do this**, or the run will skip that balance next time, believing it has already forfeited |
| 8 | **Rehearsal request** *(§2.4)* | Cancel it, if you did not at the time |
| 9 | **Reminder sweep** *(§2.6)* | **Nothing to undo, and nothing you can undo.** The dispatch log is an audit record. Its only effect is that those particular reminders will not be sent again — which is the engine working |
| 10 | **Attachments uploaded** | The request's Attachments tab → the **🗑** icon. The controlled upload and its DMS record go with it |

**The one thing that cannot be put back** is the **request number**. `LV2026000013` is spent. The
counter only moves forward, by design — the same rule as staff numbers and requisition numbers.

**The clean option.** If any of that looks fiddly, the whole database rebuilds in 45–60 minutes:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
  Where-Object { $_.CommandLine -like '*ErpSystem.Api*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild costs you chapter 2's thirty minutes as well, and it renumbers every request in this
book.

---

## 22. The short path — 25 minutes

When the slot shrinks. Seven screens, in this order, and the story still lands.

| # | Screen | Minutes | The one thing |
|---|---|---|---|
| 1 | `/administration/hr/leave-types` → **Annual Leave** → Overview + Accrual | 5 | the whole policy on one screen; **"you cannot take leave you have not earned"** |
| 2 | `/me/leave/new` *(as the employee)* | 4 | **both balance figures** before a date is typed; the notice rule; the reliever roster; create-and-submit in one press |
| 3 | `/hr/leave/requests/[id]` → **send it back with dates**, then approve **twice** | 6 | the conversation, then rule 1 — *"the status is still Pending, because HR hasn't confirmed"* |
| 4 | the same request → **Attendance days marked**, then `/hr/attendance/daily` | 4 | **the cross-module moment** — *"payroll now knows she was away"* |
| 5 | `/hr/leave/calendar` | 3 | leave as time; holidays underneath; dashed = not yet approved |
| 6 | `/hr/leave/balances` | 2 | read one row across; **nobody types a balance**; Available vs Can take now |
| 7 | `/hr/leave/register` → **Export CSV** | 1 | *"every leave conversation ends in a spreadsheet"* |

Cut, in this order if you must: the register export, then the calendar, then balances.

**If you have only ten minutes**, do 2, 3 and 4. The employee asks, the conversation happens, the
approval reaches payroll. That is the module.

---

## 23. What this walk found — and what has since been fixed

The original walk, on the morning of 2026-09-17, found **thirty-seven** things. The closure plan
built waves A–E the same day. This table is the whole list with its current state, because a
findings index that quietly drops the fixed ones is a findings index nobody can audit.

**Twenty-eight are closed. Nine remain**, and none of them is blocking.

### ✅ Closed

| # | Where | Finding | Closed by |
|---|---|---|---|
| **L-1** | ch. 6 | The desk's *Submit request* did not submit | creates then submits, with an honest fallback |
| **L-2** | ch. 15 | *Mark as paid* was unusable — a login id into an `Employees` FK | stamped from the token; the field removed from the DTO, so every caller is fixed |
| **L-3** | ch. 13 | The *Accrued* column repeated *Entitled* | accrual computed on the org-wide read |
| **L-4** | ch. 13, §1.4 | No screen showed the figure the server enforces | **Can take now** on every balance surface, from one shared definition |
| **L-5** | §1.8 | The seeded workflows did not prevent self-approval | ⚠ **not** the engine flag — every leave service refuses an approval by the record's **subject**. See §1.8 and the ledger |
| **L-6** | ch. 10 | No organisation-wide request register | `/hr/leave/register` |
| **L-7** | ch. 5 | The status filter was client-side and under-reported | moved into the query |
| **L-8** | ch. 5 | The *Open in Leave* deep link was ignored | honoured, and the picker is labelled |
| **L-9** | ch. 12 | An approved plan could not become a request | *Raise the leave request*, and a **Request** column |
| **L-10** | ch. 11 | The queue came from the reporting line, not the assignee | rebuilt as *"waiting on you"*; ⚠ this became load-bearing the moment approval went two-stage |
| **L-11** | ch. 4 | Six delete/retire controls shown to a persona holding none of them | hidden, not 403'd |
| **L-14** | ch. 6 | The balance strip showed the policy figure | both figures, the enforced one leading |
| **L-16** | ch. 12 | The plan dialog had no sub-type field | added |
| **L-17** | ch. 12 | A plan's year came from the list filter | derived from its start date, server-side |
| **L-18** | ch. 13 | No export from the balance register | CSV from balances, the register and compliance |
| **L-21** | ch. 15 | The encashment payout was not a registered money event | registered as §Area 2 in the Finance backlog. ⚠ still **not posted** — that waits for one sweep |
| **L-23** | ch. 16 | Compliance never chased anything | the reminder engine chases from month 9 |
| **L-25** | ch. 17 | "The year-end isn't scheduled" | ⚠ **was never a gap.** It is a recorded decision — those runs move balances. The *reminder* half now exists |
| **L-27** | ch. 20 | **Approved leave never marked attendance** — the largest structural gap | written on approval, removed on cancel or reschedule, reconciled nightly |
| **L-28** | §1.7 | Sub-type caps never reached a balance | enforced as an **annual** cap at request time; one pot per type stays, by decision |
| **L-29** | §1.3 | Pro-rate on exit was dead | wired. ⚠ incremental accrual only |
| **L-31** | §1.6 | Leave ignored `SubstitutionDate` | adopted `IHrWorkingDayCalculator` |
| **L-32** | §1.6 | Leave ignored `IsActive` and which calendar a holiday belonged to | same |
| **L-33** | §4.4 | `ObservanceType` and the two holiday-pay fields read by nothing | `ObservanceType` wired — **Optional is a working day**. The pay pair labelled as payroll's and handed off |
| **L-34** | ch. 4 | `LeaveSubType.IsActive` never filtered | filtered in the pickers **and** refused by the service |
| **L-35** | ch. 4 | `LeaveType.IsActive` enforced by the picker, not the service | enforced in the service, on create and on moving a draft |
| **L-36** | ch. 9 | *Calendar colour* — no calendar existed | it does now, and the setting colours it |
| **L-37** | §4.4.3 | `MedicalExpenseClaim.LeaveRequestId` never written | the *Related sick leave* picker |

### ⬜ Still open

None of these blocks a demonstration. They are listed in the order worth fixing.

| # | Where | Finding | Severity |
|---|---|---|---|
| **L-13** | ch. 4 | **The leave-type PUT is a replace-set.** A partial body silently unlinks every allowance and resets every flag. The edit form re-sends the whole record so the screen is safe — but any integration writing to this endpoint must read the detail first | **medium — do this one first** |
| **L-30** | §4.4.2 | `LeaveType.IsPaid` produces no deduction. ⚠ **Payroll's, not HR's** — handed off | medium *(not ours)* |
| **L-12** | ch. 4 | Leave-type allowances — which drive the encashment rate — have no screen | medium |
| **L-15** | ch. 6 | No attachment can be added while raising a request — the sick-note case | medium |
| **L-20** | ch. 15 | No encashment detail page, and the derived rate is never shown | medium |
| **L-22** | ch. 16 | Compliance is still read-only apart from the export | medium |
| **L-24** | ch. 17 | No dry run on either year-end job | medium |
| **L-19** | ch. 13 | Recalculate is one employee at a time, with no organisation-wide run | low |
| **L-26** | ch. 17 | Carry-over reports *processed* for balances it skipped, so "nothing to carry" and "carried nothing" look identical | low |

### ⚠ And two questions that are TDC's, not ours

Neither is a defect. Both are live in the product in two readings at once, and **both must be
settled before sign-off**. Do not assert either in front of TDC.

| | |
|---|---|
| **L-D7 · Two daily-rate formulas.** Leave encashment uses *(basic + linked allowances) ÷ 22*; the separation settlement uses *monthly × 12 ÷ 365*. On GHS 6,000/month those differ by about **38%** |
| **L-D8 · In-service encashment.** FR-HR-046 says leave is encashed *"only on exit, no other route"* — and the module ships an in-service path, with annual leave flagged for cash conversion in the seed |

Both, with the other leave decisions, are in
[`../HR-OPEN-QUESTIONS-FOR-TDC.md`](../HR-OPEN-QUESTIONS-FOR-TDC.md) as **L-D1 … L-D9**.

---

## Appendix A — every route, in demo order

| Order | Route | Persona | Chapter |
|---|---|---|---|
| 1 | `/hr/leave` | hr.head | 3 |
| 2 | `/administration/hr/leave-types` | hr.head | 4 |
| 3 | `/administration/hr/leave-types/[id]` *(5 tabs)* | hr.head | 4 |
| 4 | `/hr/leave/requests` | hr.head | 5 |
| 5 | `/hr/leave/requests/new` | hr.head | 6 |
| 6 | `/hr/leave/requests/[id]` | hr.head | 7 |
| 7 | `/hr/leave/requests/[id]/edit` | hr.head | 8 |
| 8 | `/hr/leave/calendar` | hr.head | 9 |
| 9 | `/hr/leave/register` | hr.head | 10 |
| 10 | `/hr/leave/approvals` | hr.head | 11 |
| 11 | `/hr/leave/plans` | hr.head | 12 |
| 12 | `/hr/leave/balances` | hr.head | 13 |
| 13 | `/hr/leave/adjustments` | hr.head | 14 |
| 14 | `/hr/leave/encashments` | hr.head | 15 |
| 15 | `/hr/leave/compliance` | hr.head | 16 |
| 16 | `/hr/leave/year-end` | **admin** | 17 |
| 17 | `api/hr/leave/reminders/*` | **admin** | 18 |
| 18 | `/me/leave` | staff | 19 |
| 19 | `/me/leave/calendar` | staff | 19 |
| 20 | `/me/leave/new` | staff | 19 |
| 21 | `/me/leave/[id]` | staff | 19 |
| 22 | `/me/leave/[id]/edit` | staff | 19 |
| 23 | `/me/leave/planner` | staff | 19 |
| 24 | `/me/leave/encashments` | staff | 19 |
| 25 | `/hr/attendance/daily` | hr.head | 20 |

---

## Appendix B — the permission map, in one table

| Surface | Read | Write | Admin |
|---|---|---|---|
| Leave types and all four child tabs | *(any user)* | add, edit | **retire, delete** |
| Requests — own | *(self)* | *(self)* | — |
| Requests — anybody's | `HR.Leave.Read` | `HR.Leave.Write` | — |
| **Register** | **`HR.Leave.Read`** | — | — |
| **Calendar — Mine / My team** | *(a linked employee record)* | — | — |
| **Calendar — Everyone** | **`HR.Leave.Read`** | — | — |
| Approve / reject / send back | *(the workflow assignee, and never the record's subject)* | | |
| **Bulk approve / reject** | *(same, per item)* | | |
| Reschedule · confirm · answer a suggestion | *(self or `HR.Leave.Write`)* | | |
| Plans | `HR.Leave.Read` | `HR.Leave.Write` | — |
| Balances · Adjustments | `HR.Leave.Read` | `HR.Leave.Write` | **delete an adjustment** |
| Encashments | `HR.Leave.Read` | `HR.Leave.Write` | — |
| Compliance *(and its export)* | `HR.Leave.Read` | — | — |
| **Year-end runs** | — | — | **`HR.Leave.Admin`** |
| **Reminder engine — all four endpoints** | — | — | **`HR.Leave.Admin`** |

---

## Appendix C — related documents

| Document | For |
|---|---|
| [`HR-LEAVE-CLOSURE-PLAN.md`](HR-LEAVE-CLOSURE-PLAN.md) | **§ 0 is the current state of the module.** The gap register, the eleven decisions, and what F1 still owes |
| [`HR-WORKFLOW-ENGINE-INTEGRATION.md`](HR-WORKFLOW-ENGINE-INTEGRATION.md) | the two-stage definition, and the traps behind it — including why `PreventInitiatorApproval` is the wrong control here |
| [`HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md`](HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md) | the other end of chapter 20's join. ⚠ **Its chapters predate the leave→attendance write** |
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) § 3.3 | the leave reports, three of which are now delivered |
| [`../HANDOFF-PAYROLL-LEAVE.md`](../HANDOFF-PAYROLL-LEAVE.md) | the three settings HR stores and only payroll can honour |
| [`../HR-OPEN-QUESTIONS-FOR-TDC.md`](../HR-OPEN-QUESTIONS-FOR-TDC.md) | **L-D1 … L-D9**, the leave decisions TDC still owes |
| [`../HR-FINANCE-INTEGRATION-BACKLOG.md`](../HR-FINANCE-INTEGRATION-BACKLOG.md) § Area 2 | the encashment money event, and why nothing is posted yet |
| [`../HR-CLOSURE-LEDGER.md`](../HR-CLOSURE-LEDGER.md) | every decision behind the closure build, with its reasoning |

---

**End of the leave guide.** If something in it turns out to be wrong, the most likely reason is
that **F1 has not run yet** — see the honesty note at the top. Correct the book from the harness's
findings rather than from memory, and say so here when you do.
