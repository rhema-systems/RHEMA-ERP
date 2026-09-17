# HR Leave Management — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source — page components, forms, controllers, services,
the EF model, the workflow definitions and the demo seeders. It describes what the code is built
to do. Where the demo database will not show what the code can do, that is said in the step
rather than smoothed over.

**Scope:** the whole **Leave Management** group of the HR sidebar — all eight menu items — plus
the three screens that hang off the requests register without a menu entry, the four-screen
**Leave Types** rulebook under Administration, and the six self-service screens in the employee
portal that feed the desk's queues.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| — | *(group landing)* Leave Management | `/hr/leave` | 3 |
| — | *(Administration)* Leave Types | `/administration/hr/leave-types` | 4 |
| 1 | Requests | `/hr/leave/requests` | 5 |
| — | *(no menu entry)* New request | `/hr/leave/requests/new` | 6 |
| — | *(no menu entry)* The request | `/hr/leave/requests/[id]` | 7 |
| — | *(no menu entry)* Edit a draft | `/hr/leave/requests/[id]/edit` | 8 |
| 2 | Approvals | `/hr/leave/approvals` | 9 |
| 3 | Plans | `/hr/leave/plans` | 10 |
| 4 | Balances | `/hr/leave/balances` | 11 |
| 5 | Adjustments | `/hr/leave/adjustments` | 12 |
| 6 | Encashments | `/hr/leave/encashments` | 13 |
| 7 | Compliance | `/hr/leave/compliance` | 14 |
| 8 | Year-End | `/hr/leave/year-end` | 15 |
| — | *(portal)* six screens | `/me/leave/…` | 16 |

**Twenty-two screens**, five of which carry sub-tabs, and two of which are batch runs that
rewrite data for the whole organisation.

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

Chapter 4 carries one extra part the others do not: **§4.4, the ghost-setting trace** — every
field on the leave-type rulebook checked against its read-sites, and the eleven that are
configurable and inert. Read it before you answer a question about a switch.

Two more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered (`LIVE WRITE 1` … `12`),
  and chapter 18 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the two rules that decide whether this demo works

Leave is the most recognisable module in any HR system, and this one demonstrates well. But it
has **two traps that will stop you dead in front of the room**, and both of them look like
success right up to the moment they fail. Read these twice. They are why chapter 2 exists.

### Rule 1 — On the HR desk, never press **Submit request** on the new-request form

The desk's new-request form has two buttons: **Save as draft** and **Submit request**.

**Submit request does not submit.** It creates the row at status *Pending* and stops. It never
calls the submit endpoint, so **no approval workflow is ever started**. The request then sits in
a state it cannot leave:

- the detail page offers **Submit for Approval** only on a *Draft*, so there is no way forward;
- the detail page *does* offer **Approve** and **Reject**, enabled, and both return
  **401 — "You are not assigned as an approver for the current workflow step"**, because the
  engine has no instance to check;
- its days are already counted against the employee's balance as *Pending*;
- the only exit is **Cancel**.

**Always press *Save as draft*, then open the request and press *Submit for Approval*.** Two
clicks instead of one, and everything downstream works. The portal's own form does this correctly
— it creates and *then* submits — which is why chapter 16 is the honest way to show a request
being raised. This is finding **L-1**.

### Rule 2 — `hr.head` cannot run the year-end, retire a leave type, or delete anything

The `HR` role holds `HR.Leave.Read`, `HR.Leave.Write` and `HR.Leave.Approve`. It does **not** hold
`HR.Leave.Admin`. So for `hr.head`:

| Action | Result |
|---|---|
| Year-End → Run carry-over / Run forfeiture | **403.** The menu item is hidden from the sidebar, but the route still resolves if you type it |
| Leave Types → the **⏻ Retire** button on a row | **403** |
| Leave Types → delete a sub-type, allocation, eligibility rule or accrual policy | **403** |
| Adjustments → **Delete** on a row | **403** |

Chapter 15 is performed as **admin** in a second window. Everything else in this book is
`hr.head`. This is deliberate and correct — it is the difference between running the leave desk
and rewriting every balance in the organisation — but it means you need that window open before
you start.

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

**The permission ladder.** Four permissions gate this module:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Leave.Read` | every organisation-wide read — balances, adjustments, plans, encashments, compliance | **yes** |
| `HR.Leave.Write` | raise and amend leave for anyone, adjust a balance, close a request, recalculate, record an encashment payment, maintain the leave-type catalogue | **yes** |
| `HR.Leave.Admin` | **the year-end runs**, retiring a leave type, deleting adjustments and leave-type configuration | **no** |
| `HR.Leave.Approve` | interim authority to approve where *no* workflow definition is published — **does nothing on this database**, because a `LEAVE_REQUEST` definition is seeded and published | **yes** |

**Self-access is not a permission.** An employee reads and writes *their own* leave without
holding any of these. Every leave endpoint asks the same question — *are you the employee this
record is about, or do you hold the leave tier?* — in a helper called `CanActForEmployeeAsync` /
`CanActOnRequestAsync`. That is why the **Requests** and **Approvals** leaf items carry no
permission in the sidebar: they are open to all staff, and the API narrows what each person sees.

---

## 1. How leave management hangs together

### 1.1 One rulebook, one balance, one request — in that order

Recruitment is a chain; Employees is a hub. **Leave is a ledger with a rulebook in front of it.**
Three things, and they must be understood in this order:

```
    ┌──────────────────────── 1. THE RULEBOOK ────────────────────────┐
    │  LeaveType — Annual, Sick, Maternity, Paternity, Compassionate, │
    │  Casual, Study, Unpaid, Occupational Injury                     │
    │     ├── sub-types      (Sick → Hospitalisation, Out-patient…)   │
    │     ├── allocations    (by staff level: JNR 15, SNR 21, MGT 30) │
    │     ├── eligibility    (Maternity → Female; Paternity → Male)   │
    │     ├── accrual policy (1.75 days a month, after 12 months)     │
    │     └── the flags      (paid? counts weekends? needs a          │
    │                         reliever? carries over? encashable?)    │
    └─────────────────────────────┬───────────────────────────────────┘
                                  │  resolves an entitlement for
                                  ▼
    ┌──────────────────────── 2. THE BALANCE ─────────────────────────┐
    │  LeaveBalance — ONE ROW per employee × leave type × YEAR        │
    │                                                                 │
    │   Entitled + CarriedOver + Adjustments                          │
    │            − Used − Pending − Encashed  =  AVAILABLE            │
    │                                                                 │
    │  Everything after "Entitled" is RECOMPUTED from source rows.    │
    │  Nobody types a balance. The only hand-written number is an     │
    │  adjustment, and that carries a reason and an author.           │
    └─────────────────────────────┬───────────────────────────────────┘
                                  │  is spent by
                                  ▼
    ┌──────────────────────── 3. THE REQUEST ─────────────────────────┐
    │  LeaveRequest  LV2026000001                                     │
    │  Draft → Pending → Approved → Completed                         │
    │              ↘ Rejected   ↘ Cancelled                           │
    │  + a reliever, a handover note, and any evidence attached       │
    └─────────────────────────────────────────────────────────────────┘

   Beside the request, three satellites:
     • LeavePlan       — an INTENTION for the year. Costs nothing.
     • LeaveEncashment — days converted to cash instead of taken.
     • LeaveAdjustment — a signed correction with a reason code.
```

Three points follow, and they are the three worth landing in a room:

**1. Nobody types a balance.** `UsedDays`, `PendingDays`, `AdjustmentDays` and `EncashedDays` are
all recomputed from the rows that caused them, every time anything changes. There is no screen
anywhere in the product that lets a person overwrite "days used". If the number is wrong, the
underlying rows are wrong — and there is a **Recalculate** button that rebuilds it from them.
That is the whole argument for the design, and it is worth saying out loud.

**2. The policy figure and the earned figure are different numbers, on purpose.** Your annual
entitlement is 21 days on 1 January. What you may actually *take* on 1 March is what has accrued
by 1 March — three months at 1.75 days a month, so 5.25 days. The screen shows the first; the
server enforces the second. §1.4 is the arithmetic, and it is the most common source of "but the
screen said I had days left".

**3. A plan is not a request.** The planner exists so a department can see the year's shape in
January and settle the clashes then, rather than in July when three people ask for the same week.
A plan reserves nothing and costs nothing. The real request still has to be filed — and this is
the one join in the module that is **not** automated: there is no "turn this plan into a request"
button. Finding **L-9**.

### 1.2 The tables

| What | Entity | Table | Written from |
|---|---|---|---|
| The rulebook | `LeaveType` | `LeaveTypes` | Administration → Leave Types |
| Its sub-kinds | `LeaveSubType` | `LeaveSubTypes` | leave type → Sub-types tab |
| Days by staff level | `LeaveCategoryAllocation` | `LeaveCategoryAllocations` | leave type → Allocations tab |
| Who may take it | `LeaveTypeEligibility` | `LeaveTypeEligibilities` | leave type → Eligibility tab |
| How it builds up | `LeaveAccrualPolicy` | `LeaveAccrualPolicies` | leave type → Accrual tab |
| Allowances it carries | `LeaveTypeAllowance` | `LeaveTypeAllowances` | *no screen* — API only (finding **L-12**) |
| The ledger | `LeaveBalance` | `LeaveBalances` | never directly — created and recomputed by the service |
| A correction | `LeaveAdjustment` | `LeaveAdjustments` | Adjustments screen; the forfeiture run |
| The application | `LeaveRequest` | `LeaveRequests` | Requests screen, portal |
| Its evidence | `LeaveRequestAttachment` | `LeaveRequestAttachments` | request → Attachments tab |
| The year's intention | `LeavePlan` | `LeavePlans` | Plans screen, portal planner |
| Cash instead of days | `LeaveEncashment` | `LeaveEncashments` | Encashments screen, portal |
| Who covers for whom | `EmployeeReliever` | `EmployeeRelievers` | employee profile → **Relievers** tab |
| The working calendar | `PublicHoliday` | `PublicHolidays` | Administration → Holiday Calendars |

### 1.3 The four vocabularies worth memorising

Say these the way the system says them, and the room follows you.

| The system says | Not | Because |
|---|---|---|
| **Leave type** | "leave category" | a *category allocation* is a different thing — days per staff level |
| **Entitled** | "allocated" | entitled is what policy grants for the year; an allocation is the rule that produced it |
| **Accrued to date** | "available" | accrued is what has been earned so far; available is a policy figure (§1.4) |
| **Reliever** | "cover", "stand-in" | it is a named person on the record, with a second slot behind them |

And four states people confuse:

| State | Means | Days counted as |
|---|---|---|
| **Draft** | saved; nobody has been asked | *nothing* |
| **Pending** | with an approver right now | **Pending** — already deducted from Available |
| **Approved** | granted; not yet taken, or being taken | **Used** |
| **Completed** | taken, and the desk has closed it | **Used** *(unchanged — closing moves no numbers)* |

### 1.4 The arithmetic — the single most important page in this book

There are **two** availability figures and they are deliberately different.

**What the Balances screen shows you:**

```
Available  =  Entitled  +  CarriedOver  +  Adjustments  −  Used  −  Pending  −  Encashed
```

**What the server checks when somebody asks for leave:**

```
Takeable   =  AccruedToDate  +  CarriedOver  +  Adjustments  −  Used  −  Pending  −  Encashed
                    ▲
                    └── for a leave type WITH an accrual policy. Without one,
                        AccruedToDate = Entitled and the two figures are identical.
```

`AccruedToDate` is computed live, on every read, by `LeaveEntitlementService`:

| The policy says | Then accrued-to-date is |
|---|---|
| no active accrual policy | the **full** annual entitlement, from 1 January |
| `FullGrantOnEligibility` | the **full** annual entitlement, from the day service qualifies |
| `AccrueIncrementally` | `completed periods × rate`, capped at the annual entitlement |

For TDC's seeded **Annual Leave** — monthly, 1.75 days a period, from twelve months' service — a
long-serving employee has earned:

| On | Completed months | Accrued |
|---|---|---|
| 31 January | 1 | 1.75 |
| 31 March | 3 | 5.25 |
| **17 September** | **8** | **14.00** |
| 31 December | 12 | 21.00 |

So **in September the Balances screen overstates what can actually be taken by seven days.** In
January it overstates by nearly twenty-one. That is not a bug — the screen is showing the year's
entitlement, which is the right thing for a balance register to show — but you must know it,
because the refusal message quotes the *other* number:

> *Insufficient accrued leave balance. Available: 14 days, Requested: 20 days*

Two further rules the same service enforces, both of which make excellent demonstration moments:

- **Service access.** `MinServiceMonthsToAccess` on the leave type. Annual leave is 12 months,
  Casual 3, Study 24, Unpaid 12; Sick, Maternity, Paternity, Compassionate and Injury are 0.
  Below the threshold the request is refused outright — *"has not yet completed the minimum
  service period required to take this leave type"* — whatever the balance says.
- **Notice.** `MinDaysNotice`. Annual is 14 days, Maternity and Study 30, Paternity 7, Casual 2;
  Sick, Compassionate and Injury are 0. Counted from today to the start date, and **skipped for
  drafts**, so a draft can be saved at any time and is only refused on submission.

### 1.5 How a day becomes a chargeable day

`CalculateLeaveDaysAsync` walks the range one day at a time, and the leave type decides:

| Leave type flag | When false, that day is **not charged** |
|---|---|
| `CountWeekendsAsLeave` | Saturday and Sunday |
| `CountHolidaysAsLeave` | any date inside a `PublicHoliday` row in the tenant's calendar |

Two consequences worth saying:

- **Maternity and Unpaid leave count weekends and holidays; everything else does not.** That is
  correct — the Labour Act grants maternity leave in calendar weeks, not working days.
- **A holiday straddling the request's edge still counts.** The query matches any holiday row
  *overlapping* the range, not only those fully inside it, so a 30 Dec – 2 Jan closure is
  excluded from a 1–5 January request.

The form tells you the **calendar** span as you type; the **chargeable** total is computed by the
server and appears on the request once it is saved. They differ, and the form says so.

### 1.6 Where the approval actually happens

Leave is on the generic workflow engine. Three of the module's entities carry a
`WorkflowInstanceId`: `LeaveRequest`, `LeavePlan` and `LeaveEncashment`.

The seeded definitions, from `DatabaseSeedingService.EnsureHrWorkflowsSeededAsync`:

| Definition | Entity | Shape | Approver roles |
|---|---|---|---|
| **Leave Approval** | `LEAVE_REQUEST` | Draft → PendingApproval → Approved | Manager, HR, TenantAdmin |
| **Leave Plan Approval** | `LEAVE_PLAN` | Draft → PendingApproval → Approved | Manager, HR, TenantAdmin |
| **Leave Encashment Approval** | `LEAVE_ENCASHMENT` | Draft → PendingApproval → Approved | HR, Manager, TenantAdmin |

Single approval, one step, role-routed. Two facts follow, and you should be ready for both if
somebody in the room asks:

- **"The line manager approves" is expressed as "anyone in the Manager role approves".** The
  engine cannot route on a condition today (cross-module defect #3), so the record-level checks in
  each service do the narrowing. A tenant that wants a named chain publishes its own definition in
  Administration → Workflow, and from that moment the definition decides.
- **The seeded definitions do not set `PreventInitiatorApproval`.** So on this database
  `hr.head` *can* approve a request `hr.head` raised. It makes the demo easy to drive from one
  window; it is also finding **L-5**, and the right answer is a published definition with the
  guard turned on.

There is one deliberate exception to all of this. A leave type configured with **Requires
approval = off** skips the engine entirely: pressing Submit approves it there and then, and the
balance is deducted in the same transaction. That is the sanctioned way to make a low-risk leave
type self-service, and it is not the same thing as the auto-approve defect the rest of HR spent
2026-09-16 closing — the code says so, at length, at `LeaveService.cs:472`.

---

## 2. Before the room fills — the prep

**Time: 20–25 minutes, the evening before, alone.** Much shorter than the employees guide's
chapter 2, because leave is the **best-seeded module in the demo database**. Almost everything is
already there. What you are doing is checking it, writing down a few numbers, and opening a second
window.

### 2.1 What the demo database already holds

`TdcDemoLeaveCalendarSeeder` (an EF seeder, run by `New-UatDatabase.ps1`) installs the vocabulary;
`scenarios/020-leave-balances.mjs` and `021-leave-requests.mjs` build the transactional layer
**through the real API, as the personas**. Between them:

| What | State on a freshly built `ErpSystemDB_UAT` |
|---|---|
| Leave types | **9** — Annual, Sick, Maternity, Paternity, Compassionate, Casual, Study, Unpaid, Occupational Injury |
| Holiday calendar | **"Ghana Statutory Holidays"**, 13 holidays × 2 years (this year and next) |
| Sub-types | **3**, all on Sick Leave — Hospitalisation (30), Out-patient (7), Quarantine (14) |
| Allocations | **3**, all on Annual Leave — Junior 15, Senior 21, Management 30, effective 1 January |
| Eligibility rules | **2** — Maternity → Female, Paternity → Male |
| Accrual policies | **2** — Annual (monthly, 1.75, after 12 months), Sick (annual, full grant, no service bar) |
| Leave-type allowances | **3** — Annual carries Transport + Medical; Sick carries Medical |
| Balances | every active employee has **Annual, Sick and Casual** for this year |
| Requests | **12**, numbered `LV2026000001`… — a mix of approved, pending and one rejected |
| Attachment | **1** — a medical certificate on the new hire's sick leave |
| Reliever roster | **6 rows** across four employees |
| Leave plan | **1**, in Draft — the `staff` persona's December break |
| Encashment | **1** — 5 days of annual leave, **already Processed**, ref `TDC/PAY/2026/ENC-001` |

> **Everything in this book except chapter 15 can be demonstrated on that data.** Chapter 15
> (year-end) needs a second persona and a scoped run; §2.5 sets it up.

### 2.2 Pick your anchor employee

Everything in chapters 5–8 and 16 is performed against one person. Use **the `staff` persona**
(Efua Seidu) — she already has two annual-leave requests, a reliever roster, a draft plan and a
portal login, which is exactly the shape the walk needs.

| Role in the demo | Who | Write it here |
|---|---|---|
| **The subject** — her requests, her balances, her portal | the `staff` persona | `TDC/________` |
| **Her manager** — approves in chapter 9 | the `head.dev` persona | `TDC/________` |
| **Her reliever** — named on her requests | roster priority 1 | `_______________` |

Open `/hr/employees`, search her surname, open the row, and note the staff number.

### 2.3 Check the numbers you will quote

Open `/hr/leave/balances`, pick her in the **Employee** filter, and read off the Annual Leave row:

```
Entitled                                    : ____________
Adjustments (the opening balance loaded)    : ____________
Used                                        : ____________
Pending                                     : ____________
Available (the screen's figure)             : ____________
```

Now open `/me/leave` in the portal window as her, and read the Annual Leave card. It shows the
same **Available**. Good — the two sides agree.

⚠ **Expect Available to look generous — roughly double the entitlement.** The demo seeder loaded
each person's opening balance as an **adjustment** of +21 days on top of the leave type's 21-day
default. That is not a fault and it is not worth hiding: it is exactly how you load balances from
a legacy system on go-live day, and chapter 12 makes a virtue of it. If you would rather the
numbers read cleanly, §2.6 removes it — but read chapter 12's say-line first, because the
artefact is a better story than the tidy version.

### 2.4 Prove the two-step submit, once, tonight

This is the rehearsal that stops rule 1 biting you live.

1. `/hr/leave/requests` → **New Request**.
2. Employee: **anyone other than your anchor**. Leave type: **Compassionate Leave**. Start and
   end: **the demo date**. Reason: `Rehearsal — delete me`.
3. Press **Save as draft**. You land on the request; status reads **Draft**.
4. Press **Submit for Approval** → confirm. Status becomes **Pending**, and the **Workflow** tab
   now shows a live instance sitting on *PendingApproval*.
5. Press **Approve** → the dialog → confirm. Status becomes **Approved**.
6. Press **Cancel**, reason `Rehearsal`, to put the days back.

If step 4 or 5 misbehaves, **read chapter 20's L-1 before the demo**, not during it.

### 2.5 Set up chapter 15 (year-end) — or decide to cut it

Year-end is `HR.Leave.Admin`, which `hr.head` does not hold. Two choices:

| Option | Do this |
|---|---|
| **Show it** *(recommended)* | Open a third browser window signed in as **admin** (`Admin123!`) and pre-open `/hr/leave/year-end`. In the walk you switch to that window. Scope both runs to **one employee** — never the whole organisation |
| **Cut it** | Skip chapter 15 and say the sentence in §15's ▶ step 1 instead. Twenty seconds, and nothing is lost but the button |

⚠ **Never run an unscoped forfeiture on the demo database.** It posts a negative adjustment
against **every balance in the tenant**, and there is no undo short of a rebuild.

### 2.6 Optional — tidy the opening balances

Only if you decided in §2.3 that you want clean arithmetic. As **admin**, on
`/hr/leave/adjustments`, filter to your anchor employee and Annual Leave, and delete the
`Annual entitlement — opening balance` row. Available drops to the policy figure. Do it for your
anchor only; leaving everybody else's in place is fine and nobody will look.

### 2.7 Pre-open every screen

The web app compiles a route the first time it is opened. Open these now, one at a time, waiting
for each to paint, then leave the tabs open.

**Window A — hr.head** (your main window):

`/hr/leave` · `/hr/leave/requests` · `/hr/leave/requests/new` · one request's detail page ·
`/hr/leave/approvals` · `/hr/leave/plans` · `/hr/leave/balances` · `/hr/leave/adjustments` ·
`/hr/leave/encashments` · `/hr/leave/compliance` · `/administration/hr/leave-types` · the Annual
Leave detail page — **and click all five of its tabs**

**Window B — staff** (your anchor): `/me/leave` · `/me/leave/new` · `/me/leave/planner` ·
`/me/leave/encashments`

**Window C — admin** (`Admin123!`): `/hr/leave/year-end` — held in reserve for chapter 15.

### 2.8 The numbers to write in

```
Leave types (Administration → Leave Types)        : ____________
Requests for the anchor this year                 : ____________
Rows on /hr/leave/balances with no filter         : ____________
Encashments (should be 1, Processed)              : ____________
Compliance rows outstanding                       : ____________
Annual Leave: entitled / accrued today            : ______ / ______
```

### 2.9 Prep checklist

```
[ ] 2.2  anchor employee, manager and reliever written down
[ ] 2.3  the five balance figures read off and written in
[ ] 2.4  the two-step submit rehearsed, and the rehearsal request cancelled
[ ] 2.5  Window C (admin) open on /hr/leave/year-end, or chapter 15 cut
[ ] 2.6  opening-balance adjustment removed, if you chose to
[ ] 2.7  every screen pre-opened in the right window
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

Nine cards in three rows. It is a map, not a dashboard — there are no figures on it — and its job
is to give the room the shape of the module in one screen before you go anywhere.

### 👁 On the page

**Header.** Title *Leave Management*, subtitle *Requests, approvals, balances and the year-end
cycle.* No buttons.

**Three groups of cards**, each with a small grey heading:

| Group | Cards | Goes to |
|---|---|---|
| **Day to day** | **Requests** — *Raise leave and track it through approval.* | `/hr/leave/requests` |
| | **Approvals** — *Requests waiting on a manager's decision.* | `/hr/leave/approvals` |
| | **Plans** — *Leave planned ahead for the year.* | `/hr/leave/plans` |
| **Entitlement** | **Balances** — *Entitlement, accrual, usage and what remains.* | `/hr/leave/balances` |
| | **Adjustments** — *Manual corrections with an audit trail.* | `/hr/leave/adjustments` |
| | **Encashments** — *Convert unused days into cash.* | `/hr/leave/encashments` |
| **Periodic** | **Compliance** — *Who still owes mandatory leave.* | `/hr/leave/compliance` |
| | **Year-end** — *Carry-over and forfeiture runs.* | `/hr/leave/year-end` |
| | **Leave Types** — *Setup: entitlement, carry-over and accrual rules.* | `/administration/hr/leave-types` |

Every card is a link; the whole card is the click target.

### ▶ Walk it

**1 — Read the three headings, in order, without clicking anything.**

> "Leave splits into three kinds of work, and they belong to three different people.
>
> **Day to day** is the clerk's work — somebody asks for leave, somebody decides, and once a year
> a department sketches out who is away when.
>
> **Entitlement** is the ledger. How many days does a person have, where did the number come
> from, and what has been done to it. Almost nothing on these screens can be typed — the numbers
> are calculated. The one exception is adjustments, and that is why they carry a reason and a
> name.
>
> **Periodic** is the work you do twice a year and then forget about. Who still owes us their
> statutory leave. And in January, what carries over and what expires."

**2 — Point at the last card, *Leave Types*, and note that it goes somewhere else.**

> "The last card leaves this menu. The rules — how many days, who qualifies, what carries over —
> are configuration, not daily work, so they live under Administration with the rest of the
> setup. But it is the first thing to look at, because everything on the other eight screens is
> downstream of it."

**3 — Click *Leave Types*.** You are now in chapter 4.

### ⚙ Behind the page

No API calls at all. `frontend/src/app/hr/leave/page.tsx` renders a static `NavCardGrid`. The
cards are not permission-filtered — the sidebar does that, and the target screens gate
themselves.

---

## 4. `/administration/hr/leave-types` — the rulebook

### 📍 Where you are

**Sidebar:** Administration → HR → **Time, Attendance & Leave** → **Leave Types**, or the ninth
card on `/hr/leave` · `/administration/hr/leave-types` · as **hr.head** · **8 minutes**

### 📖 What it is

Every kind of leave the organisation grants, and the complete rule for each: how many days, who
qualifies, how it builds up through the year, what happens to what is left in December, and
whether it can be turned into cash. Nine types are configured. This is the screen that makes the
rest of the module true — change a number here and every balance resolved after it changes with
it.

### 👁 Screen 1 — the register

**Header.** Title *Leave Types*, subtitle *Entitlement, carry-over and encashment rules for each
kind of leave.* One button on the right: **New Leave Type**.

**Card header.** Title *Leave Types*, and a search box (256 px, magnifier icon), placeholder
*Search leave types…* — it filters **name and code**, client-side, with no debounce.

**The table.** Seven columns:

| Column | Shows |
|---|---|
| **Name** | a coloured dot (the type's calendar colour) then the name |
| **Code** | grey, e.g. `ANN`, `SICK`, `MAT` |
| **Default days** | right-aligned |
| **Max days** | right-aligned |
| **Rules** | up to five small badges — *Unpaid* · *Approval* · *Carry-over* · *Encashable* · *Mandatory* |
| **Status** | Active / Inactive |
| *(unlabelled)* | **⏻** — the retire button, only on active rows |

**The row is a link** — clicking anywhere but the ⏻ opens the detail page.

**The list includes inactive types** on purpose, so the register is the full picture; the Status
column tells them apart.

**The ⏻ retire dialog.** Title *Retire &lt;name&gt;?*, body: *"It stays on every request already
made against it and disappears from the pickers. There is no delete: a leave type is referenced by
its history."* Buttons **Cancel** / **Retire**.

### 👁 Screen 2 — the detail page, `/administration/hr/leave-types/[id]`

**Header.** The type's name as the title; beneath it the code and description. A **back arrow**
to the register. On the right: the Active/Inactive badge and an **Edit** button.

**Five tabs:** Overview · Sub-types · Allocations · Eligibility · Accrual.

**Overview** — four cards, each a three-column grid of label-over-value:

| Card | Rows |
|---|---|
| **Entitlement** | Default days / year · Max days / year · Minimum notice (days) · Min service to access (months) · Paid · Mandatory annual leave |
| **Counting & workflow** | Counts weekends · Counts holidays · Requires approval · Requires reliever · Has sub-types |
| **Carry-over & forfeiture** | Allow carry-over · Max carry-over days · Carry-over expires (months) · Forfeit unused after (months) |
| **Encashment** | Allow cash conversion · Rate basis · Rate per day · Working days / month · Allowance components *(a badge: "N linked")* |

**Sub-types tab.** A collection table — *N sub-types* with an **Add sub-type** button. Columns:
Sub-type · Description · Max days · Status. Row menu: **Edit**, **Delete**.

**Allocations tab.** Columns: Staff level · Sub-type *(or "All")* · Days · From · To *(or
"Open-ended")*. Add / Edit / Delete.

**Eligibility tab.** Columns: Rule · Applies to. Add / Delete *(no edit — a rule is replaced, not
amended)*.

**Accrual tab.** Columns: Frequency · Mode · Rate · Min service (months) · Pro-rate · Status.
Add / Edit / Delete.

**Empty states.** Each tab reads *No &lt;things&gt; yet* with its own line — the accrual tab's is
the one worth reading aloud: *"Without a policy, entitlement is granted from the allocation
alone."*

### 👁 Screen 3 — new / edit, `/administration/hr/leave-types/new` and `…/[id]/edit`

One form, five sections, in a card titled *Leave Type Details*.

| Section | Fields |
|---|---|
| *(unnamed)* | **Name** *(required)* · **Code** *(required, ≤ 20)* · Description · **Calendar colour** *(a colour picker; used on leave calendars)* · **Paid leave** *(switch — "Unpaid leave still consumes entitlement.")* |
| **Entitlement** | **Default days per year** *(required)* · **Max days per year** *(required)* · Minimum notice (days) · Min service to access (months) · **Count weekends** *(switch)* · **Count holidays** *(switch)* · **Mandatory annual leave** *(switch — "Subject to compliance tracking.")* · **Has sub-types** *(switch)* |
| **Workflow** | **Requires approval** *(switch)* · **Requires a reliever** *(switch)* |
| **Carry-over** | **Allow carry-over** *(switch)*. When on, two more appear: Max carry-over days · Carry-over expires after (months). Always visible: **Forfeit unused after (months)** |
| **Encashment** | **Allow cash conversion** *(switch)*. When on, three more appear: **Rate basis** *(Derived from emoluments / Manual)* · Rate per day · **Working days per month** *(required, default 22)* |

Then **Active** *(switch, edit only)* and the footer: **Cancel** / **Save**.

**Two validation rules live in the form**, and both fire before the server is called:

- *Max days cannot be below the default.*
- *Enter a rate, or derive it from emoluments* — a Manual rate basis with no rate is refused.

### ▶ Walk it

**1 — Read the register out loud, top to bottom.** Nine rows.

> "Nine kinds of leave. Three of them are statutory — annual, maternity and sick. The rest are
> policy: paternity, compassionate, casual, study, unpaid leave of absence, and leave for an
> injury sustained at work, which is raised from the safety module's incident record.
>
> Look at the badges in the Rules column. That is the whole policy in five words per row.
> Annual leave: needs approval, carries over, can be encashed, and is *mandatory* — the
> Corporation requires you to take it. Unpaid leave of absence: no *Approval* badge missing,
> but the first badge reads *Unpaid*, and that single flag changes what payroll does."

**2 — Open *Annual Leave*.** The Overview tab.

Walk the four cards in order. This is the densest minute of the demo and it earns its time.

> "Twenty-one days a year, up to a ceiling of thirty. Fourteen days' notice. **And you cannot
> take any of it until you have completed twelve months' service** — that is the Labour Act, and
> the system enforces it rather than trusting somebody to remember.
>
> Second card. Weekends do not count. Public holidays do not count. If you book the week of
> Easter you are charged three days, not five, and the system knows which Monday that is because
> the Ghanaian statutory calendar is loaded — including the moveable feasts and the two Eids,
> which are marked as estimates because they are gazetted a few weeks ahead.
>
> Third card. Carry over up to five days, and **they expire at the end of March**. That is the
> rule almost every organisation has and almost no system enforces. And below it — forfeit
> anything unused after fifteen months.
>
> Fourth card. These days can be converted to cash, and the rate is **derived**, not typed:
> monthly basic plus the linked allowances, divided by twenty-two working days. The badge says
> two allowance components are linked to this leave type. Transport and medical, in this case —
> so somebody encashing annual leave is paid at a rate that includes the allowances they would
> have received had they taken it."

**3 — Allocations tab.**

> "Twenty-one days was the default. Here is what actually applies. Junior staff fifteen — the
> Labour Act floor. Senior staff twenty-one. Management thirty. And each one is **effective
> dated**, so when the policy changes in two years you add a row with next January's date rather
> than overwriting the history of what people were entitled to this year."

**4 — Accrual tab.**

> "One-point-seven-five days a month, accrued incrementally, starting after twelve months'
> service, pro-rated if you join or leave mid-year.
>
> This is the line that makes the module honest. On the second of January you have not earned
> twenty-one days — you have earned one point seven five. If you try to book three weeks in
> February the system will tell you what you have actually accrued and refuse the rest. Most
> systems give you the whole year on day one and then argue about it in December."

**5 — Eligibility tab.** It is empty for Annual Leave. Go back and open **Maternity Leave**
instead, then its Eligibility tab: one row, *Gender → Female*.

> "Maternity leave is restricted by gender, paternity by the other. This is not an HR opinion —
> it is a rule on the leave type, and it means the leave type simply does not appear in the
> picker for somebody who cannot take it. Nobody has to refuse the request, because nobody can
> raise it."

While you are on Maternity, point at the Overview's second card:

> "And note: this is the one type where weekends and holidays **do** count. Statutory maternity
> leave is twelve weeks of calendar time, not twelve weeks of working days. Eighty-four days,
> extended to ninety-eight for a caesarean or a multiple birth."

**6 — Open *Sick Leave*, then the Sub-types tab.** Three rows.

> "Some kinds of leave need a finer grain. Sick leave splits three ways — hospitalisation, capped
> at thirty days; out-patient treatment, seven; and quarantine on public-health advice, fourteen.
> **The sub-type's cap overrides the leave type's allocation**, so the same twelve-day
> entitlement behaves differently depending on which sub-type the request is filed under."

**7 — ⚠ CAREFUL — do not press the ⏻ button.** It is `HR.Leave.Admin` and `hr.head` is refused.
If somebody asks what it does, read the dialog's own text:

> "There is no delete on a leave type. Ever. A type is referenced by every request ever made
> against it, so the only thing you can do is retire it — it leaves the pickers and stays on the
> history. That is finance-grade record-keeping applied to leave, and it is the right instinct."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| The register | `GET /api/hr/leave-types?activeOnly=false` | *(none — any internal user)* |
| The detail | `GET /api/hr/leave-types/{id}/detail` | *(none)* |
| New / Edit | `POST` / `PUT /api/hr/leave-types[/{id}]` | `HR.Leave.Write` |
| **⏻ Retire** | `PATCH /api/hr/leave-types/{id}/deactivate` | **`HR.Leave.Admin`** |
| Sub-types add / edit | `POST` / `PUT /api/hr/leave-types/sub-types[/{id}]` | `HR.Leave.Write` |
| Sub-type delete | `DELETE /api/hr/leave-types/sub-types/{id}` | **`HR.Leave.Admin`** |
| Allocations add / edit | `POST` / `PUT /api/hr/leave-types/allocations[/{id}]` | `HR.Leave.Write` |
| Allocation delete | `DELETE /api/hr/leave-types/allocations/{id}` | **`HR.Leave.Admin`** |
| Eligibility add | `POST /api/hr/leave-types/eligibility` | `HR.Leave.Write` |
| Eligibility delete | `DELETE /api/hr/leave-types/eligibility/{id}` | **`HR.Leave.Admin`** |
| Accrual add / edit | `POST` / `PUT /api/hr/leave-types/accrual-policies[/{id}]` | `HR.Leave.Write` |
| Accrual delete | `DELETE /api/hr/leave-types/accrual-policies/{id}` | **`HR.Leave.Admin`** |

**There is no `DELETE /api/hr/leave-types/{id}`** — the route does not exist and the server
answers 405. Deactivation is the only exit, deliberately.

**How an entitlement is resolved**, in `LeaveEntitlementService.ResolveAnnualEntitlementAsync`,
in this order — first hit wins:

1. the **sub-type's** `MaxDaysAllowed`, if the request names a sub-type;
2. the **effective-dated allocation** for the employee's staff level (read off their *position*,
   not the employee record);
3. the leave type's **`DefaultDaysPerYear`**.

Whatever that produces is then clamped to `MaxDaysPerYear` when one is set.

### ⚠ Known gaps

| | |
|---|---|
| **L-11 · Every delete on this screen is Admin, and every delete button is shown to a persona that cannot use it.** The five delete actions across the four tabs, and the ⏻ on the register, render unconditionally and are gated only on the server. An HR officer sees them and gets a 403. The employees module's Documents tab already models the fix — check the permission, hide the control | |
| **L-12 · The allowance components that drive the encashment rate have no screen.** `LeaveTypeAllowance` is settable only through `allowanceComponentIds` on the leave-type PUT. The Overview shows a count badge; nothing lists them, and nothing lets you change them. The demo data got its three rows from the API | |
| **L-13 · The leave-type PUT is a replace-set.** Sending it without `allowanceComponentIds` unlinks every allowance silently, and a partial body resets every flag on the type. The edit form re-sends the whole record, so the screen is safe — but any integration writing to this endpoint must read the detail first. Same shape as the replace-set convention elsewhere in HR | |

---

## 4.4 Ghost settings — what the rulebook lets you configure and the engine ignores

**Read this before you demonstrate the leave-type screens, and before you answer a question about
one of these switches.** Every field on the leave-type form and its four tabs was traced to its
read-sites on 2026-09-17. Most are honoured. **Eleven are not**, and three of them are switches a
stakeholder will reasonably assume mean something.

The rule for reading the tables below: *assigned* means the service writes it to the database;
*read* means something in the product changes its behaviour because of it.

### 4.4.1 Dead — configurable, saved, and read by nothing

| Setting | Where you set it | What the room will assume | What actually happens |
|---|---|---|---|
| **Pro-rate on exit** | leave type → Accrual tab | a leaver's final-year entitlement is reduced to the months they worked | **Nothing.** `LeaveEntitlementService` reads `ProRateOnJoin` and never `ProRateOnExit`. A leaver accrues as though they worked the whole year — and that figure feeds the final-settlement conversation |
| **Paid leave** | leave type → the top section | payroll withholds pay for an unpaid leave type | **Nothing outside this module.** `IsPaid` is mapped to the DTO, badged on the register and shown as *Paid: Yes* on a request. No attendance, payroll or export path reads it. **Unpaid Leave of Absence produces no deduction anywhere** |
| **Calendar colour** | leave type → the top section, and the form says *"Used on leave calendars"* | leave shows up colour-coded on a calendar | **It colours the dot on the leave-types register, and nothing else.** There is no leave calendar screen in the product |

⚠ **Pro-rate on exit is the one that matters.** It defaults to **on** in the form, it is displayed
back to you in the Accrual tab's *Pro-rate* column as *"join, exit"*, and it is inert. Do not
promise it.

### 4.4.2 The holiday calendar's settings that leave does not read

Leave counts chargeable days with its own inline loop (`LeaveService.CalculateLeaveDaysAsync`).
**HR has a second, more careful working-day calculator** — `HrWorkingDayCalculator`, which
discipline uses for its statutory appeal clocks — and leave does not use it. Four settings fall
through that gap:

| Setting | Honoured by `HrWorkingDayCalculator` | Honoured by leave | Consequence |
|---|---|---|---|
| **Substitution date** *(the Monday given in lieu when a holiday falls at the weekend)* | ✔ | **✘** | The Act 601 substitute Monday is charged as an ordinary leave day. The demo seeder populates this field for every weekend holiday, so it is live on this database |
| **Holiday → Active** | ✔ *(via the calendar)* | **✘** | Deactivating a holiday does not stop it reducing leave charges |
| **Which holiday calendar** | ✔ — takes the tenant's **default** calendar | **✘** — takes **every** holiday in the tenant | A second calendar (a site calendar, a calendar for expatriate staff) applies to everybody |
| **Observance type** *(Mandatory / Optional)* | ✘ | ✘ | **Zero read-sites anywhere in the solution.** An optional holiday is charged exactly like a mandatory one |

Two more on the same entity are dead across the whole product, not just leave: **Attracts holiday
pay** and **Holiday pay multiplier** have no reader anywhere. They are payroll-shaped, and payroll
is another owner's module — but nothing consumes them today, so nobody should be told they work.

### 4.4.3 Half-implemented — it resolves, then the result is thrown away

These are the subtle ones. The setting *is* read, the arithmetic *is* done — and then the answer
never reaches the stored balance, because **a `LeaveBalance` row is keyed on (employee, leave
type, year) and carries no sub-type**.

| Setting | What it resolves | Why it does not stick |
|---|---|---|
| **Sub-type → Max days** | `ResolveAnnualEntitlementAsync` takes the sub-type cap ahead of the allocation and the default — so a request naming *Hospitalisation* is pre-checked against 30 days | The balance the request is checked against, and the one the recalculation service creates, are both looked up **by leave type only**, and the recalculation resolves entitlement with `leaveSubTypeId: null`. **All three Sick sub-types share one 12-day pot.** The entity's own comment asks for exactly this to be enforced in the service layer |
| **Allocation → Sub-type** *(a staff-level allocation scoped to one sub-type)* | matched exactly, including the null case | same root cause — it can only ever affect a pre-flight check, never a stored figure |
| **Sub-type → Active** | — | `GetSubTypesAsync` applies **no `IsActive` filter**, so a retired sub-type still appears in the request form's Sub-type dropdown |
| **Leave type → Active** | the picker filters on it | the **service** does not. A retired leave type can still be used through the API by anyone who knows its id. The ⏻ retire dialog's promise — *"disappears from the pickers"* — is true of the pickers and not of the door behind them |

### 4.4.4 Dead joins — the schema says integrated, nothing writes it

Three foreign keys exist, are mapped end to end, and are set by no code path in the solution:

| Join | The promise in the schema | Reality |
|---|---|---|
| `StaffDailyAttendance.LeaveRequestId` + the `OnLeave` attendance status | *"Attendance days that were recorded as leave against this request"* — a navigation on `LeaveRequest` | **Approving leave never marks a single attendance day.** Nothing in the solution assigns `StaffAttendanceStatus.OnLeave` or writes `LeaveRequestId` on an attendance row. So **`DaysOnLeave` on the monthly summary — which the payroll export reads — is zero unless a clerk hand-edits the daily record**, and `LeaveRequest.AttendanceDays` is permanently empty |
| `LeaveRequest.LeavePlanId` | a request raised from an approved plan | never written — finding **L-9** |
| `MedicalExpenseClaim.LeaveRequestId`, commented *"Leave Integration"* | a medical claim tied to the sick leave it arose from | never written |

⚠ **The attendance one is the largest structural gap in the module** and it is worth knowing
before somebody asks "so does payroll know she was away?". The honest answer today is: *payroll
knows because the leave record says so; the attendance summary does not yet reflect it, and that
join is built but not wired.*

### 4.4.5 What is not a ghost — checked and working

So that this section is not read as a list of everything being broken: **twenty-one settings were
traced and are fully honoured.** Minimum notice · Requires approval · Requires a reliever ·
Min service to access · Allow carry-over and its cap · Carry-over expiry · Forfeit unused after ·
Mandatory annual leave · Count weekends · Count holidays · Allow cash conversion · the three
encashment rate settings · Default days · Max days *(as a ceiling on everything below it)* ·
Has sub-types *(it gates sub-type creation — the server refuses one on a type not flagged for
them)* · all four eligibility rule types, including the gender qualifier that ANDs onto an
org-scoped rule · accrual frequency, mode, rate, min-service and pro-rate-**on-join** ·
allocation effective dating.

The **Active** switch on the leave-type form also saves correctly — the edit page sends `isActive`
alongside the shared payload, which the shared payload builder itself omits. It is the one place
in this module where a suspicious-looking gap turned out to be covered.

---

## 5. `/hr/leave/requests` — the register

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Leave Management → **Requests** ·
`/hr/leave/requests` · as **hr.head** · **4 minutes**

### 📖 What it is

Leave applications, one employee at a time, with each one's position in its approval chain shown
beside it. The per-employee framing is not a UI choice — the API has no organisation-wide request
list, so a person must be chosen before anything can be shown. Finding **L-6**.

### 👁 On the page

**Header.** Title *Leave Requests*, subtitle *Requests and their position in the approval
workflow.* One button: **+ New Request**.

**Filters card.** Three controls in a row:

| Control | Values | Notes |
|---|---|---|
| **Employee** | an employee picker | **type at least 2 characters**; debounced 400 ms; searches **active employees only**. Resets to page 1 on change |
| **Year** | next year · this year · last year · the year before | resets to page 1 |
| **Status** | All statuses · Draft · Pending · Approved · Rejected · Cancelled · In progress · Completed | **client-side only** — it filters the page you already have, not the query |

**It opens on you.** The employee box pre-fills with the signed-in user's own employee record, so
a plain member of staff who lands here sees their own history rather than an empty picker and a
403. HR simply changes it.

**Requests card.** Title *Requests*, with a small spinner on the right while refetching.

| Column | Shows |
|---|---|
| **Request** | the number, bold — `LV2026000004` |
| **Leave type** | name, and ` · sub-type` where one is set |
| **From** / **To** | `YYYY-MM-DD` |
| **Days** | right-aligned — the **chargeable** total |
| **Status** | a status badge |
| **Awaiting** | the current workflow step and who it is with, e.g. *PendingApproval · A. Mensah*; `—` when there is no live instance |

**The row is a link** — the whole row opens the request.

**Paging.** 20 a page. *Page N of M · T requests*, with **Previous** / **Next**, shown only when
there is more than one page.

**Empty states.** No employee chosen: *Choose an employee — Leave history is listed per
employee.* Employee chosen, nothing matches: *No leave requests — Nothing matches these filters.*

### ▶ Walk it

**1 — Arrive with the screen already on your own name.** Say what that means before touching it.

> "This screen opens on whoever is signed in. If I were a storekeeper I would land here and see
> my own leave and nothing else — the same screen, the same URL, the same code. What decides
> what I see is not a menu, it is a check on every single request: *is this yours, or do you run
> the leave desk?* There is no second application for staff."

**2 — Put your anchor employee in the Employee box.** Type two letters of her surname, pause, pick
her from the list.

The table fills. Point at the **Awaiting** column.

> "Two useful things in one row. The status badge on the left is where the request is. The column
> on the right is where it is *sitting* — which step of the approval chain, and whose desk. That
> comes from the workflow engine, not from leave. Every module in the product that submits
> something for approval gets this column from the same place."

**3 — Point at the *Days* column against the *From* and *To* dates.** Find a request that spans a
weekend.

> "Look at this one — the eighth to the seventeenth. That is ten calendar days. The charge is
> eight. The two Saturdays and Sundays inside it are not annual leave, because annual leave is
> configured not to count them. You never do that arithmetic; the system does it against the
> holiday calendar when the request is saved."

**4 — Change Year to last year.** The table empties or shrinks.

> "Leave is annual. A balance is per employee, per leave type, per year — and so is this list.
> A request that starts on the twenty-eighth of December and ends on the third of January belongs
> entirely to the year it **started**. That is a rule you have to pick, and picking it explicitly
> is better than discovering later that half a request went missing."

Set it back to this year.

**5 — Set Status to *Rejected*.** One row appears *(the seeded rejection)*.

> "One rejected, with the reason on the record. We will look at the reason itself in a moment."

Set it back to **All statuses**.

**6 — Open a row.** You are now in chapter 7. *(If you are raising one live, press* **+ New
Request** *instead and go to chapter 6.)*

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The list | `GET /api/Leaves/employee/{employeeId}/history?year=&pageNumber=&pageSize=` | `LeaveService.GetEmployeeLeaveHistoryAsync` | **self-or-`HR.Leave.Read`** |
| The *Awaiting* column | `POST` batch workflow-summary read, `entityType=LeaveRequest` | workflow engine | *(none)* |
| The employee picker | `POST /api/hr/Employees/paged` | `EmployeeService` | *(none — the shared picker)* |

Rows come from `LeaveRequests`, filtered on **`StartDate.Year == year`**, ordered by
**`RequestDate` descending**. The route is `api/Leaves` — capital L, and **no `/hr` prefix**,
unlike every other route in this module.

### ⚠ Known gaps

| | |
|---|---|
| **L-6 · There is no organisation-wide request register.** `LeavesController` exposes history by employee and pending approvals by manager, and nothing else. "Show me everybody on leave next week" cannot be asked from any screen in the product. The reports catalogue lists it as the single most-wanted missing HR report | |
| **L-7 · The status filter is client-side.** It filters the 20 rows already fetched, so on an employee with 30 requests, filtering to *Approved* can show fewer than the employee actually has, with no warning. Status is not a query parameter on the endpoint | |
| **L-8 · The employee profile's "Open in Leave" link is ignored.** The Leave tab on `/hr/employees/[id]` links to `/hr/leave/requests?employeeId=…`, and this page never reads the query string — it always initialises from the signed-in user. The deep link lands on your own history | |

---

## 6. `/hr/leave/requests/new` — raising leave on somebody's behalf

### 📍 Where you are

**From:** `/hr/leave/requests` → **+ New Request** · `/hr/leave/requests/new` · as **hr.head** ·
**6 minutes**

### 📖 What it is

The desk's form for filing leave for a member of staff — somebody who phoned in, somebody without
a login, somebody whose paperwork arrived on a form. It is the same endpoint the portal uses, with
an employee picker on the front of it.

### 👁 On the page

**Header.** Title *New Leave Request*, subtitle *Request leave for an employee*, with a **back
arrow** to the register.

**One card**, titled *Leave Request*, subtitle *Submitting starts the approval workflow configured
for leave requests.*

| Field | Type | Notes |
|---|---|---|
| **Employee** | employee picker | required. ≥ 2 characters, 400 ms debounce, **active employees only** |
| **Leave type** | dropdown | required. Active types only |
| **Sub-type** | dropdown | optional, and **empty until a leave type with sub-types is chosen** |
| *(balance strip)* | read-only | appears once an employee and type are chosen — see below |
| **Start date** | date | required |
| **End date** | date | required; must be ≥ start |
| *(span line)* | read-only | *"N calendar days selected — weekends are not counted, so the chargeable total will be lower."* |
| **Reason** | textarea, 3 rows | **required**, ≤ 1000 |
| *(roster note)* | read-only | a blue panel, only when the roster filled a slot — see below |
| **Reliever** | employee picker | optional, unless the type requires one |
| **Second reliever** | employee picker | optional; cannot be set without a first |
| **Reliever notes** | textarea | optional, ≤ 1000 |
| **Handover notes** | textarea | optional, ≤ 2000 |

**The balance strip** reads: *Available for Annual Leave: **28** days (entitled 21, used 7,
pending 7).* It is the same figure the Balances screen shows — the **policy** figure, not the
accrued one (§1.4).

**The roster note** is a blue panel that appears when the employee's pre-defined relievers have
filled a slot:

> *Filled from this employee's reliever roster: Kofi Asante (priority 1), Ama Owusu (priority 2).
> Change either one freely — the roster is only a starting point, and what you save here is what
> counts for this request.*

It seeds **once per employee**, never over a value you have typed, never over one you have
deliberately cleared, and **never on an edit**.

**A red warning** under Reliever when the type requires one and none is set: *This leave type
expects a reliever.*

**Footer, three buttons:** **Cancel** · **Save as draft** · **Submit request**.

**Form-level validation**, before the server sees anything:

- *End date cannot be before the start date*
- *Set the first reliever before adding a second*
- *An employee cannot relieve themselves*

### ▶ Walk it

**🔴 LIVE WRITE 1 — this step creates a real leave request.**

⚠ **CAREFUL — read rule 1 at the top of this book before you start.** You are going to press
**Save as draft**, not **Submit request**. Do not improvise this step.

**1 — Choose the employee.** Type two letters of your anchor's surname, pick her.

**2 — Choose *Annual Leave*.** The balance strip appears. Let the room read it.

> "The moment I pick the person and the kind of leave, the form tells me what she has. Entitled
> twenty-one, used seven, seven more pending a decision. This is the single most common reason a
> leave request gets rejected two days later, and the clerk filling the form is the last person
> who normally gets to see it."

**3 — Open the *Sub-type* dropdown.** It is empty.

> "Empty, because annual leave has no sub-kinds. If I picked sick leave it would offer three —
> hospitalisation, out-patient, quarantine — and the cap would change with it."

**4 — Set the dates.** Pick a Monday about three weeks out, and the Friday of the week after.

The span line appears beneath: *12 calendar days selected — weekends are not counted, so the
chargeable total will be lower.*

> "Twelve calendar days. Watch what the system charges her when I save it — it will not be
> twelve."

**5 — Type the reason.** `Annual leave — family visit to Kumasi.`

**6 — Look at the Reliever fields.** For your anchor, the blue roster panel is showing and both
slots are pre-filled.

> "I did not fill these in. The employee has a **reliever roster** on her record — who covers for
> her, in order — and the form has read it and put priority one in the first slot and priority
> two in the second. It also checked that neither of them is away over these dates; anybody who
> is, it skips.
>
> And it tells me it did it. A form that fills a field without saying so is a form that has
> started lying to the person using it."

Clear the second reliever and re-pick somebody else, to show the override.

> "And they are a starting point, not a rule. What I save here is what counts for this request."

**7 — Type a handover note.** `Month-end reconciliation pack is in the shared drive; Kofi has the
key to the cabinet.`

> "This is the field the reliever actually reads on the day. It is not decoration — it travels
> with the approval to the manager who has to decide whether the section can spare her."

**8 — Press *Save as draft*.** 🔴

A toast: *Draft saved. Submit it when ready to start the approval workflow.* You land on the
request's own page. You are now in chapter 7.

> "Saved, and nobody has been asked anything. Notice what the system did **not** do — it did not
> check the fourteen days' notice, because a draft is not an application. You can save a draft the
> day you think of it."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| Leave types | `GET /api/hr/leave-types?activeOnly=true` | `LeaveTypeService` | *(none)* |
| Sub-types | `GET /api/hr/leave-types/{id}/sub-types` | | *(none)* |
| Balance strip | `GET /api/Leaves/employee/{id}/balances` | `LeaveService` | self-or-`HR.Leave.Read` |
| Reliever roster | `GET /api/hr/employee-relievers/employee/{id}` | `EmployeeRelieverService` | self-or-HR |
| Save as draft / Submit | `POST /api/Leaves` | `LeaveService.CreateLeaveRequestAsync` | **self-or-`HR.Leave.Write`** |

**The eight checks `CreateLeaveRequestAsync` runs, in order.** Each one is a different refusal
message, and each is worth knowing because you may trigger one live:

| # | Check | Refusal |
|---|---|---|
| 1 | employee and leave type exist in this tenant | *…not found* → 404 |
| 2 | **eligibility** (gender / unit / level / position rules) | *does not meet the eligibility criteria for the selected leave type* |
| 3 | **service access** (`MinServiceMonthsToAccess`) | *has not yet completed the minimum service period required* |
| 4 | start date is not in the past | *Leave start date cannot be in the past.* |
| 5 | end ≥ start | *Leave end date must be after or equal to start date.* |
| 6 | **minimum notice** — *skipped for drafts* | *requires at least N day(s) notice. Only M day(s) given.* |
| 7 | **no overlapping request** (Approved or Pending, this employee) | *Employee already has a leave request for this period.* |
| 8 | **accrued balance** covers the chargeable total | *Insufficient accrued leave balance. Available: X days, Requested: Y days* |

Then the relievers: an explicitly chosen one is validated hard (**not yourself**, **must be
Active**, **must be free over the dates**); empty slots are auto-filled from the roster by
priority, skipping anyone unavailable; and if a slot is still empty the employee's **line
manager** is proposed, if *they* are free. Only then, if the type requires a reliever and none was
found, is the submission refused: *This leave type requires a reliever, but none could be
assigned.* — **also skipped for drafts.**

**The number.** `LV{year}{000001}`, issued from the tenant's atomic number sequence and restarted
each January. Under concurrent creates the unique index on `(TenantId, RequestNumber)` catches a
collision and the service regenerates and retries, up to five times.

**The write is one transaction:** insert the request, then recalculate the balance. A recalc
failure rolls the request back rather than leaving an orphan with a stale balance.

### ⚠ Known gaps

| | |
|---|---|
| **L-1 · *Submit request* does not submit.** The button creates the row at *Pending* and never calls `POST /Leaves/{id}/submit`, so no workflow instance exists. The request cannot then be submitted (the detail page offers Submit only on a Draft) and cannot be approved (the engine has no instance, so `CanUserApproveAsync` refuses everybody), while its days are already deducted as Pending. **The portal's form does this correctly.** The fix is three lines in `frontend/src/app/hr/leave/requests/new/page.tsx` | |
| **L-14 · The balance strip shows the policy figure, not the takeable one.** The check on save uses accrued-to-date; the strip uses entitled. In September those differ by seven days on annual leave. The strip is the number the clerk trusts | |
| **L-15 · No attachment can be added while raising a request.** Evidence — a medical certificate — can only be attached after the request exists, from its detail page. The one case where that matters most is sick leave, where the certificate is the reason the request is being filed | |

---

## 7. `/hr/leave/requests/[id]` — the request itself

### 📍 Where you are

**From:** any row in the register, or straight from saving one · `/hr/leave/requests/[id]` · as
**hr.head** · **7 minutes**

### 📖 What it is

One leave application, everything on it, and every act that can be performed on it. The approval
itself is not implemented here — the shared workflow component is dropped onto the header, and it
is the same component the purchase order, the travel request and the disciplinary case use.

### 👁 On the page

**Header.** The request number as the title; beneath it *&lt;employee name&gt; · &lt;leave
type&gt;* with the sub-type in brackets if there is one. A **back arrow** to the register.

**On the right, a row of controls that changes with the status:**

| Control | Shown when | Does |
|---|---|---|
| *(status badge)* | always | the current status |
| **✏ Edit** | status is **Draft** | opens the edit form — chapter 8 |
| **Submit for Approval** | status is **Draft** | starts the workflow |
| **Approve** / **Reject** | status is **Pending** | the workflow decision, with a comment dialog |
| **Recall** | there is a live instance and you raised it | withdraws it back to Draft, with an optional reason |
| **✓✓ Close** | status is **Approved** or **InProgress** | marks the leave as taken and complete |
| **⊘ Cancel** *(red)* | status is **Draft**, **Pending** or **Approved** | withdraws it and releases the days |

**Three tabs:** Overview · Attachments · Workflow.

**Overview** — three cards and a conditional fourth:

| Card | Rows |
|---|---|
| **Leave** | Leave type · Sub-type · **Paid** · Start date · End date · **Total days** · Requested on · **From a plan** *(Yes/No)* |
| **Employee & cover** | Employee *(name and staff number)* · Reliever · Second reliever |
| **Reason & notes** | Reason · Handover notes · Reliever notes — all full-width, wrapping |
| **Outcome** *(only when cancelled or closed)* | Cancelled on · Cancellation reason · Closed on · Closure notes |

**Attachments tab.** An upload card *(only while the request is Draft or Pending)* with a **Choose
file** button, accepting `.pdf .png .jpg .jpeg .doc .docx`, help text *"Medical certificates and
supporting documents. Scanned on upload; max 10 MB."* Beneath it a table: file name, size, a
**download** icon and a **delete** icon.

**Workflow tab.** The shared record tab — the definition, the steps, who is on the current one,
the decision history, and any comments left by approvers.

**Two dialogs:**

- **Cancel leave request?** — *"The request is withdrawn and any pending days are released."*
  with a **required** Reason textarea.
- **Close leave request?** — *"Marks the leave as taken and complete."* with an **optional**
  Closure notes textarea.

### ▶ Walk it

You arrive on the **Draft** you created in chapter 6.

**1 — Read the Overview before doing anything.**

> "There is the chargeable total. I asked for twelve calendar days and the system has charged
> eight — the two weekends came out. Nobody did that arithmetic; the leave type said weekends do
> not count and the calendar said which days those were.
>
> *Paid: Yes.* That is what payroll reads. *From a plan: No* — we will come back to plans.
>
> And the reliever, on the record, by name. Not in a comment, not in an email — on the request,
> so that when this is approved the section knows who is covering, and so that if somebody tries
> to book **him** off over the same week, the system will say so."

**2 — 🔴 LIVE WRITE 2 — press *Submit for Approval*.** Confirm the dialog.

The status badge becomes **Pending**.

> "That is the point where the system stops being a form and starts being a process."

**3 — Open the *Workflow* tab.** This is the moment that sells the product.

> "Everything you have seen so far was leave. This is not. This is the workflow engine, and it is
> the same engine that approves a purchase order, a travel request, a staff promotion and a
> disciplinary decision. Leave does not have its own approval screen, its own delegation rules or
> its own e-signature, because a second implementation of a rule is where the rule starts to
> drift.
>
> The definition here is *Leave Approval*: draft, pending approval, approved. One step. It routes
> to the line manager or to HR. If the Corporation wants three levels for leave over ten days,
> that is authored in the workflow designer under Administration — not written in code, and not
> re-implemented in the leave module."

**4 — 🔴 LIVE WRITE 3 — press *Approve*.** A dialog opens for comments. Type `Approved — cover
arranged with the section.` and confirm.

The badge becomes **Approved**. The Workflow tab now shows the decision, the comment and the time.

⚠ **CAREFUL — you have just approved a request you raised.** Somebody may notice. Meet it head
on rather than hoping:

> "And yes — I raised that and I approved it, in the same window, thirty seconds apart. The
> engine supports a rule that forbids exactly that, and the definitions shipped with the system
> do not switch it on. For a live configuration you would, and then this demonstration would need
> two people. It is one checkbox on the workflow step, and it is the right question to ask."

**5 — Go back to the *Balances* screen in another tab and refresh** *(or wait until chapter 11)*.
Her Used has gone up by eight and Pending has come down by eight.

> "Nothing moved the balance by hand. The approval changed the request's status, and the balance
> was rebuilt from the requests behind it. Those two numbers are always a sum of rows, never a
> running total that somebody might have got wrong."

**6 — Open the *Attachments* tab.** It is empty for this request.

Instead, navigate to the **new hire's sick-leave request** *(seeded)* and open its Attachments
tab. One row: `medical-certificate-tema-general.pdf`.

> "This is a medical certificate. Three things happen to it that are not obvious from looking at
> a file name.
>
> It was **virus-scanned** on upload — every document door in HR is scan-mandatory and refuses
> the file if the scanner is not answering. It was **registered in the central document
> management system**, so it is one of the organisation's documents and not a loose file in a
> folder. And it is **not linked** — that download button does not point at a file path, it
> calls an endpoint that checks whether *you* are entitled to see this particular certificate.
>
> Sick-leave evidence is the most sensitive document in the HR module, and it is the one people
> most often end up emailing around."

**7 — 🔴 LIVE WRITE 4 — press *Close* on an approved request whose end date has passed.**

⚠ **The seeded requests are all in the future, so none of them can be closed** — the server
refuses with *"Cannot close leave request before end date."* To show Close live you need the
same-day request from §2.4's rehearsal, or you skip the button and say:

> "Once the leave has actually been taken, the desk closes it. That is a separate act from
> approving it, and it exists because in real life the two are weeks apart — and because
> somebody has to be able to answer 'did she actually go?' The closure carries a note and a date.
>
> It moves no numbers. The days were spent at approval."

**8 — Point at *Cancel* without pressing it.**

> "And cancellation — which is different from rejection. A rejection is a decision by somebody
> else. A cancellation is a withdrawal, it needs a reason, and it puts the days straight back.
> It is available right up to and including an approved request, because plans change."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The record | `GET /api/Leaves/{id}` | `LeaveService.GetLeaveRequestByIdAsync` | **self-or-`HR.Leave.Read`** |
| **Submit for Approval** | `POST /api/Leaves/{id}/submit` | `SubmitForApprovalAsync` | **self-or-`HR.Leave.Write`** |
| **Approve** | `PUT /api/Leaves/{id}/approve` | `ApproveLeaveAsync` | *(no permission — the workflow assignee check decides)* |
| **Reject** | `PUT /api/Leaves/{id}/reject` | `RejectLeaveAsync` | *(same)* |
| **Recall** | the generic workflow recall | engine | requester only |
| **Cancel** | `PUT /api/Leaves/{id}/cancel` | `CancelLeaveRequestAsync` | **self-or-`HR.Leave.Write`** |
| **Close** | `PUT /api/Leaves/{id}/close` | `CloseLeaveRequestAsync` | **`HR.Leave.Write`** *(not self)* |
| Attachments list | `GET /api/Leaves/{id}/attachments` | | self-or-`HR.Leave.Read` |
| Upload | `POST /api/Leaves/{id}/attachments` | via `HrDocumentUploadRequest` | self-or-`HR.Leave.Write` |
| Download | `GET /api/Leaves/attachments/{id}/download` | | **owner or `HR.Leave.Read`**, checked in the endpoint |
| Delete | `DELETE /api/Leaves/attachments/{id}` | | self-or-`HR.Leave.Write` |

**What Submit does.** `SubmitForApprovalAsync` first asks whether the leave type
`RequiresApproval`. If **not**, the request is approved there and then, the balance is recalculated
in the same transaction, and the engine is never involved — the sanctioned auto-approve. If it
does, the request goes to the engine through `HrWorkflowFallbackAuthority.SubmitAsync`, which
substitutes a *Pending* outcome for the engine's "no definition published, so approved" signal —
the defence-in-depth guard added across all 26 engine-wired HR services on 2026-09-16.

**What Approve does.** `HrWorkflowFallbackAuthority.ProcessApprovalAsync` → the engine's
`CanUserApproveAsync` (which reads the approval rows on the current step) → the engine's
`ProcessApprovalAsync` → `LeaveRequestWorkflowStatusAdapter` maps the outcome onto
`LeaveRequest.Status` → **one transaction** saves the request and recalculates the balance.

**The upload path.** Controlled upload (virus scan) → central DMS registration → the
`LeaveRequestAttachment` row, which carries `FileUploadRecordId`, `DocumentRecordId` and
`DocumentVersionId`. If the attachment row fails to write, the stored file and its DMS record are
**rolled back**, so nothing is orphaned in the document store.

**The status ladder** the adapter applies:

| Engine outcome | Leave status | Also set |
|---|---|---|
| Approved | `Approved` | `ApprovedById`, `ApprovedDate`; rejection reason cleared |
| Rejected | `Rejected` | `RejectionReason` |
| Recalled | `Draft` | approver and date cleared |
| *anything else* | `Pending` | approver and date cleared |

---

## 8. `/hr/leave/requests/[id]/edit` — amending a draft

### 📍 Where you are

**From:** the request → **✏ Edit** *(Draft only)* · `/hr/leave/requests/[id]/edit` · as
**hr.head** · **2 minutes**

### 📖 What it is

The same form as chapter 6, pre-filled, with the employee locked in and only one button. It exists
for one reason and the screen says so when it refuses you.

### 👁 On the page

**Header.** Title *Edit Leave Request*, subtitle the request number, back arrow to the request.

**The form** is the chapter 6 form with three differences:

- **no *Save as draft* button** — the only buttons are **Cancel** and **Save changes**;
- **the reliever roster does not re-seed.** Deliberate: a saved request's relievers are what was
  agreed, and re-reading master data that has moved on since would silently rewrite the record;
- **if the request is no longer a Draft**, the form is not rendered at all. In its place:

> **This request is no longer a draft**
> *Once submitted, a request is changed through the approval workflow rather than edited.*

### ▶ Walk it

**1 — Go to a draft and press Edit.** Change the end date by one day.

**2 — Press *Save changes*.** Toast: *Draft updated.* You are back on the request, and **Total
days has changed**.

> "A draft is yours to change. The total recalculated itself."

**3 — Now open an approved or pending request and try to reach the same screen** *(paste the URL
with `/edit` on the end)*. You get the refusal panel.

> "And once it has been submitted, it cannot be. Not because the button is hidden — the screen
> will tell you exactly why even if you type the address. A request under approval is changed by
> recalling it, or by rejecting it and raising another. What you cannot do is quietly move the
> dates under an approver who has already agreed to something else."

### ⚙ Behind the page

| Control | Call | Gate |
|---|---|---|
| **Save changes** | `PUT /api/Leaves/{id}/draft` | self-or-`HR.Leave.Write` |

`UpdateDraftAsync` refuses anything that is not a Draft, then re-runs eligibility and service
access **only if the leave type changed**, re-checks the overlap **only if the dates changed**,
and re-checks the balance **only if either changed** — adding this draft's own days back as
headroom, so an edit is never refused by the draft it is editing. When the type or the year moves,
**both** the old and the new balance buckets are recalculated.

---

## 9. `/hr/leave/approvals` — the manager's queue

### 📍 Where you are

**Sidebar:** … → Leave Management → **Approvals** · `/hr/leave/approvals` · as **hr.head** ·
**3 minutes**

### 📖 What it is

Everything sitting with one named manager, waiting for a decision. It is a **queue, not a second
approval screen** — the decision itself is taken on the request's own page, where the engine's
rules apply. The screen says so, in a panel, above the table.

### 👁 On the page

**Header.** Title *Leave Approvals*, subtitle *Requests waiting on a manager's decision.* One
button on the right: **Open approval inbox** → `/workflow/inbox`.

**An information panel**, grey, with an ⓘ:

> *Approve or reject from a request's own page — the workflow engine applies step order,
> delegation and any required checklists or signatures there.*

**Manager card.** One employee picker, half width. Nothing is shown until a manager is chosen.

**Pending requests card.** Seven columns: Request · Employee · Leave type · From · To · Days ·
Status. The row is a link to the request. Paged 20 at a time.

**Empty states.** No manager chosen: *Choose a manager — Pending approvals are listed per
approving manager.* Chosen, nothing waiting: *Nothing pending — This manager has no leave requests
awaiting a decision.*

### ▶ Walk it

**1 — Put your anchor's manager in the picker.** The queue fills — the request you submitted in
chapter 7 is there if you have not yet approved it.

> "Everything sitting on this manager's desk. Who, what kind, how long, and how many days it will
> cost the section."

**2 — Read the ⓘ panel aloud. This is the point of the screen.**

> "And notice what this screen will not let me do. There is no approve button here. This is a
> list, and the decision is taken on the request itself — because that is where the workflow
> engine enforces the step order, the delegation, the checklists and the signature if the
> definition asks for one. A tick box on a list would be a second implementation of the same
> rule, and the second one is always the one that drifts."

**3 — Press *Open approval inbox*.**

> "And this is the other half of the answer. A manager does not live in the leave module. Their
> inbox has leave requests next to purchase orders, travel approvals and staff movements, in one
> list, because that is how their day actually looks. This screen exists for the HR desk asking
> 'what is stuck with Mensah?' — not for Mensah."

Come back.

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The queue | `GET /api/Leaves/pending-approvals/{managerId}?pageNumber=&pageSize=` | `LeaveService.GetPendingApprovalsAsync` | **self-or-`HR.Leave.Read`** — a manager reads their own queue without any permission |

**⚠ This queue is built from the reporting line, not from the workflow.** The repository reads
`Employees` where `ManagerId == managerId`, then returns every one of their requests at status
**Pending**. It does not ask the engine who the current step is assigned to. On the seeded
definition those two answers happen to agree — the step routes to the Manager role, and the
manager of record is in it — but a tenant that publishes a definition routing to, say, a divisional
head would see this queue disagree with the engine. **`/workflow/inbox` is the authoritative
list.** Finding **L-10**.

---

## 10. `/hr/leave/plans` — the year, before it happens

### 📍 Where you are

**Sidebar:** … → Leave Management → **Plans** · `/hr/leave/plans` · as **hr.head** · **7 minutes**

### 📖 What it is

Every planned absence for a year, across the organisation, with a three-way conversation built in:
the employee proposes, the manager can approve, reject **or suggest different dates**, and the
employee then accepts or counters. A plan costs no days. It exists so the clashes are found in
January.

### 👁 On the page

**Header.** Title *Leave Plans*, subtitle *Planned leave for the year, approved ahead of the
actual request.*

**Year card.** One dropdown: next year · this year · last year.

**The collection.** A header line — *N leave plans* — with an **Add leave plan** button, then a
table:

| Column | Shows |
|---|---|
| **Employee** | name |
| **Leave type** | name, and ` · sub-type` if set |
| **From** / **To** | the planned dates |
| **Suggested** | the manager's counter-proposal, `from → to`, or `—` |
| **Reliever** | both names joined by ` · `, and a **red clash badge** where the server found conflicts |
| **Status** | Draft · Submitted · ChangesSuggested · Approved · Rejected · Cancelled |

**The clash badge** reads *"2 clashes"* and its tooltip lists them in full.

**The row menu (⋯)** — the items shown depend on the status:

| Item | Shown when |
|---|---|
| **Edit** | always *(the service refuses anything but a Draft)* |
| **Submit for approval** | Draft |
| **Approve** | Submitted or ChangesSuggested — with a confirmation |
| **Reject…** | Submitted or ChangesSuggested — opens a **required**-reason dialog |
| **Suggest different dates…** | Submitted — opens the suggestion dialog |
| **Accept suggested dates** | ChangesSuggested, and a suggestion exists |
| **Decline suggestion** | ChangesSuggested |
| **Cancel plan** *(red)* | anything not already Cancelled or Rejected — with a confirmation |
| **Delete** | *not offered* |

**The add / edit dialog** — *Plan leave ahead for the year.*

| Field | Notes |
|---|---|
| **Employee** | picker, required |
| **Leave type** | dropdown, required |
| **Start date** / **End date** | required; end ≥ start |
| **Reliever** | picker — **and underneath it, a live clash check** |
| **Second reliever** | picker — same |
| **Notes** | textarea, ≤ 1000 |

**The live clash check** is the best control on this screen. As soon as a reliever and both dates
are set, it asks the server and answers in one of three ways:

- *Checking the reliever's diary…*
- ✅ *Reliever: nothing in their diary over these dates.*
- ⚠ a full alert box: *Reliever: 2 clashes over these dates*, with a bulleted list —
  *"Kofi Asante has a submitted Annual Leave plan of their own over these dates (2026-11-02 –
  2026-11-13)"* — and beneath it: **You can still save the plan — this is a warning, not a rule.**

**The suggestion dialog.** *Suggest different dates* — *"The employee is asked to accept or
decline these dates."* Two date fields, both required, plus Notes.

### ▶ Walk it

**1 — Set the year to this year.** One plan is there: your anchor's December break, in **Draft**.

> "A leave plan is an intention. Nobody has asked for anything yet; no days have been reserved;
> nothing has been deducted. It is the answer to the question every head of department asks in
> January — *who is going to be away, and when?*"

**2 — Press *Add leave plan*.** Fill in an employee, Annual Leave, two weeks in a busy month.

**3 — In the *Reliever* box, deliberately pick somebody who is already committed** — the reliever
already named on one of the seeded requests works.

Wait two seconds. The amber alert appears.

> "This is the control TDC asked for by name after the second demonstration. We named a reliever
> and nobody ever asked whether that person would actually be there.
>
> The system now checks three different things, in one question. Does the reliever have a leave
> **plan** of their own over these dates? Do they have a live leave **request** over them — any
> status that is still alive? And are they **already named as somebody else's reliever** over the
> same window?
>
> And look at the last line. *You can still save the plan — this is a warning, not a rule.*
> Because leave gets cancelled and dates move, and a system that refuses a plan on a clash that
> will have evaporated by November is a system people learn to route around."

**4 — 🔴 LIVE WRITE 5 — change the reliever to somebody free** *(the message flips to the green
line)* **and save.** The plan appears at **Draft**.

**5 — 🔴 LIVE WRITE 6 — ⋯ → *Submit for approval*.** Status becomes **Submitted**.

**6 — 🔴 LIVE WRITE 7 — ⋯ → *Suggest different dates…***. Move both dates two weeks later, and in
Notes type `Clashes with the year-end valuation; November works better for the section.` Send it.

Status becomes **ChangesSuggested**, and the **Suggested** column fills in.

> "This is the part most systems do not have. A manager has three answers to a leave plan, not
> two — yes, no, and *not then*. That third answer is the one that is actually used, and if the
> system does not carry it, it happens on the phone and nobody ever knows why the dates moved.
>
> Here the plan goes back to the employee with the manager's proposed dates and their reason
> attached. Behind the scenes the approval workflow has been cancelled, because there is nothing
> to approve until she answers — and a fresh one starts the moment she does."

**7 — ⋯ → *Accept suggested dates***, or show the employee's side of it in chapter 16.7. Status
returns to **Submitted**, on the new dates.

**8 — 🔴 LIVE WRITE 8 — ⋯ → *Approve*.** Confirm. Status becomes **Approved**.

**9 — Point at what does *not* happen next.**

> "And now the honest part. That plan is approved, and **nothing has been booked**. The actual
> leave request still has to be filed, by hand, matching these dates. There is no button that
> turns an approved plan into a request.
>
> The link exists in the data — a request can record which plan it came from, and the request
> screen has a field that says *From a plan: yes* — but nothing on any screen sets it. That is a
> gap and it is on the list."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The list | `GET /api/hr/leave-plans?year=` | `LeavePlanService.GetByYearAsync` | **`HR.Leave.Read`** |
| Clash check | `GET /api/hr/leave-plans/reliever-clashes?relieverId=&startDate=&endDate=&excludePlanId=` | `GetRelieverClashesAsync` | *(none — an employee planning their own leave needs this answer too)* |
| Add / Edit | `POST` / `PUT /api/hr/leave-plans[/{id}]` | | **self-or-`HR.Leave.Write`** |
| Submit | `PATCH /api/hr/leave-plans/{id}/submit` | | self-or-`HR.Leave.Write` |
| Approve / Reject / Suggest | `PATCH …/approve`, `…/reject`, `…/suggest-changes` | | *(no permission — the workflow assignee check decides)* |
| Accept / Decline a suggestion | `PATCH …/respond-suggestion` | | **self-or-`HR.Leave.Write`** — it is the plan owner's act |
| Cancel | `PATCH …/cancel` | | self-or-`HR.Leave.Write` |

**`PlannedBy` is stamped from the token, never from the payload** — it is an `Employee` foreign
key, and the login's user id both screens used to send was never one. Until that was fixed
(finish-plan lane 4, 2026-09-01) no plan raised from either screen had ever satisfied the
constraint.

**The clash query runs once per window, not once per row.** `EnrichRelieverClashesAsync` reads all
plans and all live requests across the whole listed range in two queries, then matches in memory —
so a 200-plan year costs two round trips, not six hundred.

**Suggest-changes is a third decision verb** and it needed the same two paths approve and reject
have. With a definition published it checks `CanUserApproveAsync`, then **cancels** the running
workflow instance; with none published it falls back to the `HR.Leave.Approve` tier. Either way
`WorkflowInstanceId` is cleared, and `RespondToSuggestionAsync` starts a fresh one.

### ⚠ Known gaps

| | |
|---|---|
| **L-9 · A plan cannot become a request.** `LeaveRequest.LeavePlanId` exists, the detail page reads it, and nothing writes it. The planning cycle stops at "approved" and the employee re-types the same dates into a different form | |
| **L-16 · The plan dialog has no sub-type field.** The schema and the payload both carry `leaveSubTypeId`; the form never renders it, so a sick-leave plan cannot record which kind | |
| **L-17 · A plan's year comes from the page filter, not its dates.** The list's Year dropdown is sent as the plan's `Year`, so a plan created while viewing 2026 but dated January 2027 is filed under 2026 and disappears from next year's list. *(The employee's own counter-proposal path does set it from the start date — the two disagree.)* | |

---

## 11. `/hr/leave/balances` — the ledger

### 📍 Where you are

**Sidebar:** … → Leave Management → **Balances** · `/hr/leave/balances` · as **hr.head** ·
**6 minutes**

### 📖 What it is

Every balance in the organisation for a year, with the full arithmetic exposed — ten columns, and
each one is a component of the sum rather than a number somebody typed. The screen that answers
"how many days does she have left, and how do you know?"

### 👁 On the page

**Header.** Title *Leave Balances*, subtitle *Entitlement, accrual, usage and what remains.* One
button on the right: **⟳ Recalculate**.

**Filters card.** Three controls: **Employee** *(picker, optional)* · **Leave type** *(All leave
types, or one)* · **Year**.

**The table** — ten columns, everything after the first two right-aligned:

| Column | Is |
|---|---|
| **Employee** | name, with the organisation unit beneath it in small grey |
| **Leave type** | name, and ` · sub-type` if set |
| **Entitled** | what policy grants for the year |
| **Accrued** | what has been earned so far — **see the gap below** |
| **Carried over** | brought in from last year by the year-end run |
| **Adjustments** | the signed sum of every correction |
| **Used** | the sum of **approved** requests |
| **Pending** | the sum of **pending and in-progress** requests |
| **Encashed** | the sum of **processed** encashments |
| **Available** | bold — the result of the sum |

**No paging.** The whole year is returned in one call, ordered by surname, then forename, then
leave type.

**Empty state.** *No balances — Balances appear once leave types have allocations and employees
are entitled.*

**Recalculate** with no employee chosen refuses in a toast: *Choose an employee — Recalculation
runs for one employee at a time.*

### ▶ Walk it

**1 — Arrive unfiltered.** A long table. Let it sit for a second.

> "Every balance in the Corporation for this year. One row per person, per kind of leave. And
> look at the shape of it — ten columns, and eight of them are the *working* of the sum."

**2 — Read one row across, left to right, slowly.** Pick your anchor's Annual Leave row.

> "Entitled twenty-one — that is what the policy grants. Carried over, nothing yet. Adjustments,
> plus twenty-one — and I will come back to that one, because it is the most interesting number
> on the screen. Used, eight — that is the request we approved a moment ago. Pending, seven —
> that is her other application, still with her manager. Encashed, nothing.
>
> Available: the arithmetic of the six columns to its left.
>
> There is no screen in this product where anyone can type into that last column. If it is wrong,
> one of the six is wrong, and you can see which."

**3 — The adjustment. This is the moment to be honest and it plays well.**

> "Plus twenty-one in Adjustments. Where does that come from?
>
> This database was built as a new system on day one, with no history. So the opening balances
> were **loaded as adjustments** — one entry per person, per leave type, with a reason:
> *'Annual entitlement — opening balance'*, and the name of whoever loaded it.
>
> That is exactly what you will do on go-live. You will not import a balance into a column; you
> will post an opening entry that says where the number came from, so that in three years, when
> somebody disputes their days, the trail goes all the way back to the migration."

*(If you removed it in §2.6, say instead: "Notice Adjustments is zero. When you go live, that
column will carry your opening balances — an entry per person with a reason, not a number typed
into a box.")*

**4 — Filter to your anchor and press *Recalculate*.**

Toast: *Recalculated — Balances rebuilt from entitlement and usage.* The numbers do not change.

> "And nothing moved, which is the point. Recalculate throws away the cached figures and rebuilds
> Used, Pending, Adjustments and Encashed from the underlying rows. If it ever produced a
> different answer, that would mean something had written to a balance behind the system's back —
> and you would want to know.
>
> One thing it deliberately does **not** touch: Entitled and Carried over. Those are decisions,
> not sums. Changing the policy does not retroactively rewrite what somebody was entitled to this
> year — you would post an adjustment, with a reason."

**5 — Set the Leave type filter to *Sick Leave*** and read one row.

> "Twelve days sick leave. Note there is no service bar on this one — sick leave is available on
> your first day, because nobody schedules illness around a probation period. And its accrual is
> configured as a full grant rather than a monthly build-up, for the same reason."

**6 — ⚠ The *Accrued* column.** If somebody asks why Accrued equals Entitled on every row:

> "On this screen, accrued is showing the full entitlement rather than what has actually been
> earned to date. The organisation-wide read does not compute the accrual — the per-employee one
> does, and it is the figure the system actually enforces when you ask for leave. So on an annual
> leave row in September, the number the server would allow is around fourteen days, not
> twenty-one. That is a known gap on this column."

*(Have this sentence ready. It is the most likely question on this screen.)*

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The table | `GET /api/Leaves/balances?year=&employeeId=&leaveTypeId=` | `LeaveService.GetAllLeaveBalancesAsync` | **`HR.Leave.Read`** |
| **Recalculate** | `POST /api/Leaves/balances/recalculate` | `LeaveBalanceRecalculationService` | **`HR.Leave.Write`** |

`RecalculateAsync` finds or creates the balance row *(creating it at the entitlement the
entitlement engine resolves)*, then re-derives four numbers with four aggregate queries:

| Column | From |
|---|---|
| `UsedDays` | `SUM(TotalDays)` of requests at **Approved**, `StartDate.Year == year` |
| `PendingDays` | `SUM(TotalDays)` of requests at **Pending** or **InProgress** |
| `AdjustmentDays` | `SUM(Days)` of every adjustment for that employee / type / year |
| `EncashedDays` | `SUM(DaysEncashed)` of encashments at **Processed** |

It **never writes** `EntitledDays` or `CarriedOverDays`, and it is called automatically after
every create, draft edit, submit, approve, reject, cancel, adjustment and encashment payment — so
the numbers are current without anyone pressing the button. The button is for the case where
something was written outside the service.

### ⚠ Known gaps

| | |
|---|---|
| **L-3 · The only screen with an *Accrued* column is the only read that does not compute it.** `GetAllLeaveBalancesAsync` returns the mapper's default, which is `EntitledDays`. `GetEmployeeLeaveBalancesAsync` — the portal, the profile tab, the request form — overrides it with the real figure. So the column reads correctly *nowhere it is displayed*, and correctly everywhere it is not. One `foreach` in the service closes it | |
| **L-4 · Available overstates what can be taken, for any accruing leave type.** §1.4. Not a defect in itself — the screen is showing the policy figure — but with L-3 unfixed there is no column anywhere that shows the enforced one | |
| **L-18 · No export.** A balance register with no CSV is the single most-requested HR report. The reports catalogue marks it 🟡 for exactly this reason | |
| **L-19 · Recalculate is one employee at a time, by design, with no organisation-wide run.** Reasonable, but it means a policy correction applied to 900 people has no route through the UI | |

---

## 12. `/hr/leave/adjustments` — corrections, with a name on them

### 📍 Where you are

**Sidebar:** … → Leave Management → **Adjustments** · `/hr/leave/adjustments` · as **hr.head** ·
**5 minutes**

### 📖 What it is

The only place in the module where a human number enters a balance — and it is deliberately the
most instrumented screen in it. Every adjustment is signed, dated, reason-coded, free-texted and
reversible, and the form shows you the balance you are about to move before you move it.

### 👁 On the page

**Header.** Title *Leave Adjustments*, subtitle *Manual corrections to a leave balance, with an
audit trail.*

**Filters card.** Four controls: **Employee** *(picker)* · **Leave type** · **Year** ·
**Search** *(a plain box, placeholder "Remarks or employee…", sent to the server)*.

**The collection.** *N adjustments*, with an **Add adjustment** button, then a table:

| Column | Shows |
|---|---|
| **Employee** | name |
| **Leave type** | name, and ` · sub-type` |
| **Year** | |
| **Days** | a badge — **`+21`** *(filled)* for a credit, **`-3`** *(outline)* for a deduction |
| **Reason code** | the coded reason, or `—` |
| **Remarks** | the free text |
| **Date** | the adjustment date |
| **By** | **the employee who posted it** |

**Row menu:** **Edit**, **Delete** *(red)*.

**The add / edit dialog** — *Correct a leave balance. Use a negative value to deduct days.*

| Field | Notes |
|---|---|
| **Employee** | picker, required |
| **Leave type** | dropdown, required |
| **Year** | number, required |
| *(balance preview)* | **the best control on the screen — see below** |
| **Days (negative deducts)** | number, step 0.5, required, **may not be zero** |
| **Adjustment date** | date, defaults to today |
| **Reason code** | dropdown, filtered to reason codes in the `LeaveAdjustment` or `General` category |
| **Remarks** | textarea, **required**, ≤ 500 |

**The balance preview** sits between the leave type and the days, and updates live:

```
  Current balance · Annual Leave 2026                      [ 28 available ]
  Entitled   Carried over   Adjustments   Used   Pending   Encashed
     21            0            21          8       7          0

  After this adjustment: 30.5 available.
```

If the result would go below zero the last line turns **red**: *"…the balance would go negative."*
— a warning, not a block.

If no balance row exists yet, a dashed panel says so: *"No balance row exists yet for this leave
type in 2026. Saving creates one at the type's default entitlement, then applies this adjustment
to it."*

### ▶ Walk it

**1 — Arrive and filter to your anchor.** The opening-balance rows are there.

> "Every correction ever made to anybody's leave balance. Not a log — the actual records, and the
> balance is the sum of them.
>
> Read the last column. *By.* Every adjustment carries the name of the person who posted it, and
> that is not taken from the form — it comes from the login. You cannot post an adjustment in
> somebody else's name, and you cannot post one from a system account that is not a member of
> staff. The system will refuse it and tell you to get your account linked to an employee
> record."

**2 — Press *Add adjustment*.** Choose your anchor, **Annual Leave**, this year.

The balance preview appears, fully populated.

> "And here is the control TDC asked for. Before this, the person correcting a balance had to open
> another screen to see what they were correcting. Now the form shows the whole balance — every
> component — before anything is saved."

**3 — Type `2.5` into Days.** Watch the last line of the preview change.

> "And it previews the result. Two and a half days added takes her from twenty-eight available to
> thirty and a half."

Now type `-40` instead. The line turns red.

> "And if I were about to push her negative, it says so — in red, before I save. It does not stop
> me, because there are real situations where a balance must go negative: somebody took leave
> they had not yet earned, and the correction has to reflect that. What the system will not do is
> let it happen quietly."

**4 — 🔴 LIVE WRITE 9 — set Days back to `2.5`, pick a reason code, and type the remarks.**

Reason code: whatever the lookup offers — *Correction* or *Opening balance*. Remarks:
`Two and a half days restored — public holiday incorrectly charged in March.`

Press **Save**. Toast: *Adjustment added.* The row appears at the top, with a green **`+2.5`**
badge and your name in the **By** column.

**5 — Switch to the Balances tab and refresh.** Adjustments has moved from 21 to 23.5 and
Available has moved with it.

> "Two screens, one number, and nobody typed it into either of them."

**6 — ⚠ Do not press *Delete*.** It is `HR.Leave.Admin` and `hr.head` is refused. If asked:

> "Deleting an adjustment is a different privilege from posting one, deliberately. Posting one is
> the leave desk's daily work. Removing one is rewriting the audit trail, and that sits with the
> administrator."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The list | `GET /api/Leaves/adjustments?year=&employeeId=&leaveTypeId=&search=` | `GetAllAdjustmentsAsync` | **`HR.Leave.Read`** |
| Balance preview | `GET /api/Leaves/employee/{id}/balances?year=` | | self-or-`HR.Leave.Read` |
| Reason codes | `GET` the shared `ReasonCode` lookup | | *(none)* |
| **Add** | `POST /api/Leaves/adjustments` | `CreateStandaloneAdjustmentAsync` | **`HR.Leave.Write`** |
| **Edit** | `PUT /api/Leaves/adjustments/{id}` | `UpdateAdjustmentAsync` | **`HR.Leave.Write`** |
| **Delete** | `DELETE /api/Leaves/adjustments/{id}` | | **`HR.Leave.Admin`** |

`CreateStandaloneAdjustmentAsync` **finds or creates** the balance row — at the leave type's
`DefaultDaysPerYear` when it has to create one — posts the adjustment, and recalculates, all in
one transaction.

**`PerformedBy` is stamped from the token's employee id**, never the payload. It is an `Employee`
foreign key, and until finish-plan lane 4 (2026-09-01) the screen sent the login's **user** id —
so no adjustment raised from the desk had ever been saved, and the table was empty. The house rule
throughout HR: *an actor column names an employee, and an unlinked account cannot be that actor.*

**Two fields, one name.** The entity calls the free text `Reason`; the screen calls it
**Remarks**, and puts the coded `ReasonCode` above it under the label **Reason code**. TDC asked
for the coded reason; the entity's own comment agrees with the renaming.

---

## 13. `/hr/leave/encashments` — days into cash

### 📍 Where you are

**Sidebar:** … → Leave Management → **Encashments** · `/hr/leave/encashments` · as **hr.head** ·
**5 minutes**

### 📖 What it is

Requests to be paid for leave instead of taking it, and the three-step life of one: requested,
approved, paid. The amount is **never typed by the employee** — the server derives it from their
emoluments and the leave type's rate policy.

### 👁 On the page

**Header.** Title *Leave Encashments*, subtitle *Requests to convert unused leave days into cash.*

**Filters card.** Two dropdowns: **Year** and **Status** *(All statuses, or one of Draft ·
Submitted · PendingApproval · Approved · Rejected · Processed · Cancelled)*.

**The table** — eight columns: Employee · Leave type · Year · **Days** *(right)* · **Amount**
*(right, thousands-separated)* · Status · **Payment ref** · a **⋯** menu.

**The ⋯ menu**, status-dependent:

| Item | Shown when |
|---|---|
| **Approve** | Submitted or PendingApproval |
| **Reject** *(red)* | Submitted or PendingApproval — opens a **required**-reason dialog |
| **Mark as paid** | Approved — opens a dialog with a **required** Payment reference |

**Rows are not links.** There is no encashment detail page.

**Empty state.** *No encashments — Encashment requests appear here once employees convert unused
days.*

### ▶ Walk it

**1 — Arrive.** One row: the seeded encashment, **Processed**, 5 days, with a payment reference.

> "One request, and it has been all the way through. Five days of annual leave converted to cash
> instead of taken. Approved, then paid, with the payment voucher reference on the record."

**2 — Point at the *Amount* column and make the point that matters.**

> "Now — where did that figure come from? Not from the employee. The encashment form in the
> portal does not have an amount box at all.
>
> The leave type carries a **rate policy**. For annual leave it is set to *derived from
> emoluments*: the system takes the employee's monthly basic pay, adds the allowances that are
> **linked to this leave type** — transport and medical, in our configuration — and divides by
> twenty-two working days. That is the daily rate, and it is multiplied by the days requested.
>
> The alternative is a flat rate per day, typed once on the leave type, which some organisations
> prefer for simplicity. Either way it is a **policy decision made once**, not a negotiation
> per employee."

**3 — Explain the three statuses, pointing at the badge.**

> "Three states, and the middle one is the one people skip.
>
> **Submitted** — the request exists and it is with an approver, through the same workflow engine
> as everything else.
>
> **Approved** — somebody with the authority has agreed the days can be converted. And that is
> where it stops, because approving a conversion is not the same as paying for it.
>
> **Processed** — the payment has actually been made, and the voucher reference is on the record.
> That is the only status that moves the balance. Until the money is paid, the days are not gone."

**4 — ⚠ CAREFUL — do not press *Mark as paid* on a live row.**

The screen sends the login's **user** id where the endpoint expects an **employee** id, and the
foreign key refuses it. You get a red toast and a 500. This is finding **L-2**.

If you want to show the dialog, open it and **cancel out of it**:

> "And the last step is a payment reference. The system is not paying anybody — payroll does that
> — but it records *which* payment discharged *this* conversion, so the days and the money are
> tied together on one record."

**5 — Set the Status filter to *Submitted*.** Empty, unless the portal walk in chapter 16 has run.

> "Nothing waiting, which is what you want to see. If there were, the menu on that row would offer
> approve and reject, and the rejection would want a reason."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The table | `GET /api/hr/leave-encashments?year=&status=` | `GetAllEncashmentsAsync` | **`HR.Leave.Read`** |
| **Approve** | `PATCH /api/hr/leave-encashments/{id}/approve` | | *(no permission — the workflow assignee check decides)* |
| **Reject** | `PATCH …/reject` | | *(same)* |
| **Mark as paid** | `PATCH …/process` | `MarkAsProcessedAsync` | **`HR.Leave.Write`** |
| Request one | `POST /api/hr/leave-encashments` | `RequestEncashmentAsync` | **self-or-`HR.Leave.Write`** — the portal's door |

**The four checks on a request:**

1. the leave request has not already been encashed — *"This leave request has already been
   encashed."* (**one encashment per leave request**, enforced by the relationship);
2. the leave type has `AllowCashConversion` — *"This leave type does not allow cash conversion."*;
3. a balance row exists for that type and year;
4. `AvailableDays >= DaysEncashed` — *"Insufficient balance. Available: X days, Requested: Y."*

**Then the amount.** `EmolumentService.GetEncashmentDailyRateAsync`:

```
  Manual basis            →  the leave type's EncashmentRatePerDay
  DerivedFromEmoluments   →  (monthly basic + linked allowances) ÷ EncashmentWorkingDaysPerMonth
```

rounded to two decimals, multiplied by the days. The `amountPaid` on the request body is used
**only** if that derivation yields nothing — it is a fallback for a rate-less configuration, not
an offer.

**Create and submit are one transaction.** A workflow submit that throws must roll the insert back
too, or the caller sees an error while an orphaned *Submitted* row survives — and every retry then
refuses with *"already been encashed."* Found live during area 25.

**Processed is the only status that moves a balance.** `EncashedDays` is derived by the
recalculation service from encashments at **Processed** — the single source of truth — so the
encashment service never touches `UsedDays` directly, and a recalculation cannot erase the
deduction.

### ⚠ Known gaps

| | |
|---|---|
| **L-2 · *Mark as paid* sends a user id where an employee id is required.** `frontend/src/app/hr/leave/encashments/page.tsx` sends `processedByEmployeeId: user?.id`; `LeaveEncashment.ProcessedByEmployeeId` is a restricted foreign key to `Employees`. The write fails on the constraint. **The action is unusable from the UI.** The portal's own encashment request is unaffected; the seeded row got its value from the API. One-line fix: send `user?.employeeId` | |
| **L-20 · No encashment detail page and no audit of the rate.** The row shows the amount, not the rate, the basic pay, or which allowances fed it. Anybody disputing the figure has nothing to read | |
| **L-21 · The encashment payout is not registered as a Finance money event.** It is on the HR↔Finance backlog *(rows 23 and 24 of the entity sweep — leave encashment posting and leave liability)*, deferred to the single GL sweep, and it is one of the two rows flagged 🔴 | |

---

## 14. `/hr/leave/compliance` — who still owes their leave

### 📍 Where you are

**Sidebar:** … → Leave Management → **Compliance** · `/hr/leave/compliance` · as **hr.head** ·
**3 minutes**

### 📖 What it is

Mandatory leave is leave the organisation requires you to take — and not taking it is the
organisation's problem, not yours. This screen is the list of people who still owe it, with the
year's arithmetic on each row.

### 👁 On the page

**Header.** Title *Mandatory Leave Compliance*, subtitle *Employees who have not yet taken their
required leave.*

**Year card.** One dropdown: this year · last year · the year before. *(No next year — the
question is only meaningful about a year in progress or closed.)*

**The table** — seven columns:

| Column | Is |
|---|---|
| **Employee** | name |
| **Leave type** | the type flagged mandatory |
| **Entitled** | entitled **+ carried over** for the year |
| **Taken** | the *Used* figure — approved requests |
| **Scheduled** | the *Pending* figure — applications not yet decided |
| **Outstanding** | `Entitled − Taken − Scheduled`, floored at zero, **amber when above zero** |
| **Status** | **Compliant** · **Scheduled** · **Outstanding** |

**The three statuses** are computed, not stored:

| Status | When |
|---|---|
| **Compliant** | Taken ≥ Entitled — they have had their leave |
| **Scheduled** | Taken + Scheduled ≥ Entitled — they have booked it, not yet taken it |
| **Outstanding** | neither — they have not booked enough of it |

**Empty state.** *Nothing to report — No leave type is flagged as mandatory, or everyone is
compliant.*

### ▶ Walk it

**1 — Arrive on this year.** Rows, mostly **Outstanding**, with amber figures.

> "Only one leave type is flagged as mandatory in our configuration, and it is annual leave.
>
> That flag is not administrative tidiness. In most of the world an employer is required to
> ensure annual leave is actually taken, and 'the employee didn't ask' is not a defence. It is
> also good practice for another reason entirely — the person who never takes leave is a fraud
> risk, because nobody else has ever done their job."

**2 — Read one row across.**

> "Twenty-one days entitled. Eight taken. Seven scheduled — that is an application in flight.
> Six outstanding, in amber. And the status says *Outstanding*, because eight plus seven is
> fifteen and fifteen is not twenty-one."

**3 — Find a row that reads *Scheduled*** *(or explain the state)*.

> "This one reads **Scheduled**, not compliant. She has not taken her leave yet, but she has
> booked all of it, and that is a materially different conversation for a head of department to
> have in October. The screen distinguishes them, which is the whole reason it exists — a
> compliance list that shows only 'taken or not taken' produces a panic in November about people
> who are already booked."

**4 — Point at what is missing, before anybody asks.**

> "What this screen does not have — and should — is a way to act on it. There is no *remind*
> button, no export, no filter by department. Today it is a register you read and then act on
> elsewhere. It answers the question correctly; it just does not help you do anything about it
> yet."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| The table | `GET /api/Leaves/mandatory-compliance?year=` | `GetMandatoryLeaveComplianceAsync` | **`HR.Leave.Read`** |

One query over `LeaveBalances`, joined to `LeaveTypes` where `MandatoryAnnualLeave` is true and
the type is active, excluding deleted employees, ordered by surname. The arithmetic is done in
memory over the rows. `Entitled` on this screen is **`EntitledDays + CarriedOverDays`**, which is
not the same as the Balances screen's *Entitled* column — carried-over days count toward what you
are required to use.

### ⚠ Known gaps

| | |
|---|---|
| **L-22 · Read-only, with no action.** No reminder, no bulk notification, no export, no department filter, no link from a row to the employee or to raising leave on their behalf. The finding that matters most on this screen is that it is a list you cannot do anything with | |
| **L-23 · The forfeiture run and this screen never meet.** The leave type's `ForfeitUnusedAfterMonths` is the consequence of being on this list, and nothing on the screen says when that date is or how many days are at risk | |

---

## 15. `/hr/leave/year-end` — carry-over and forfeiture

### 📍 Where you are

**Sidebar:** … → Leave Management → **Year-End** — *(hidden from `hr.head`)* ·
`/hr/leave/year-end` · **as admin, in Window C** · **4 minutes**

### 📖 What it is

Two batch runs that close one leave year and open the next. Both rewrite balances in bulk, neither
is automatically reversible, and both are behind `HR.Leave.Admin` for exactly that reason.

### 👁 On the page

**Header.** Title *Leave Year-End*, subtitle *Carry unused entitlement into the new year, then
forfeit what expires.*

**An amber warning panel** across the top:

> ⚠ *Both runs update leave balances in bulk and are not automatically reversible. Scope a run to
> one employee first to confirm the numbers before running it for everyone.*

**Two cards, side by side.**

**Carry-over** — *Moves unused days from the closing year into the next, capped by each leave
type's carry-over limit.*

| Field | Notes |
|---|---|
| **From year** | number, defaults to **last year** |
| **Employee (optional)** | picker — *"Leave blank to run for every employee."* |
| **Run carry-over** | the button *(default styling)* |

**Forfeiture** — *Removes carried-over or unused days that have passed their expiry window.*

| Field | Notes |
|---|---|
| **Year** | number, defaults to **last year** |
| **As of date (optional)** | date — *"Defaults to today."* |
| **Employee (optional)** | picker |
| **Run forfeiture** | the button, **red** |

**Both are behind a confirmation dialog** that spells out the scope in words:

> *Unused days from 2025 will be carried into 2026 for every employee.*
> *Expired days in 2025 will be removed for every employee. This cannot be undone automatically.*

**After a run, a results panel** appears under the card:

```
  Last carry-over run
  [ 412 processed ]  [ 87 affected ]  [ 311 days carried over ]
  • Carried over 5 day(s) for employee …, leave type Annual Leave.
  • …
```

Badges for processed / affected / days, then a scrollable list of per-balance notes.

### ▶ Walk it

⚠ **Switch to Window C (admin) now.** `hr.head` cannot see this menu item and cannot run either
job.

**1 — Arrive and read the amber panel out loud.** Do not skip it; it is the point.

> "This is the only screen in the leave module that changes hundreds of records at once, and the
> system says so before it lets you near the buttons.
>
> Notice also that it is not on the leave clerk's menu at all. Running the leave desk and closing
> the leave year are different privileges. The person who approves your leave cannot rewrite
> everybody's balance."

**2 — 🔴 LIVE WRITE 10 — scope a carry-over to one employee and run it.**

Set **From year** to last year, pick **one** employee, press **Run carry-over**, read the
confirmation aloud, confirm.

The results panel appears.

> "One employee, last year into this year. And look at what it reports — how many balances it
> looked at, how many it actually changed, and how many days moved, with a line per balance.
>
> This is how you would do it for real. Run it for one person, check the number against what you
> expected, then run it for everybody."

**3 — Explain the cap without needing to demonstrate it.**

> "It does not carry everything. Each leave type has its own carry-over rule: annual leave allows
> a maximum of five days, and the run takes the lower of what is left and that cap. A type with
> carry-over switched off — sick leave, compassionate leave — is skipped entirely, and what is
> unused at the end of December simply ends.
>
> And it **sets** the new year's carried-over figure rather than adding to it, so running it twice
> by accident does not double anybody's days."

**4 — Now the second card. 🔴 LIVE WRITE 11 — scope a forfeiture to the same employee and run it.**

⚠ **Never run this one unscoped on the demo database.**

> "Forfeiture is the other half, and it does two different things.
>
> First, it **expires carried-over days** whose window has closed. Annual leave carries over five
> days and they are usable until the end of March — after that they are gone, and this run is what
> makes that true.
>
> Second, it **forfeits unused accrual** after the leave type's cut-off. And here is the part
> worth watching: it does not quietly zero a column. It **posts a negative adjustment**, with the
> reason *'FORFEIT: unused leave (year-end/cut-off)'*, in the name of the administrator who ran
> it, and then recalculates the balance from it.
>
> So a forfeiture appears on the Adjustments screen, like every other correction, with a reason
> and a name. In three years' time, when somebody asks where their days went, there is a row that
> answers."

**5 — Switch to the Adjustments screen and find the row.** *(Filter to the same employee.)*

> "There it is."

**6 — One last point about running it twice.**

> "And it checks before it posts. If a forfeiture adjustment with that reason already exists
> against the balance, it skips it. The run is safe to repeat — which matters, because the one
> thing you can guarantee about a year-end job is that somebody will run it twice."

### ⚙ Behind the page

| Control | Call | Service | Gate |
|---|---|---|---|
| **Run carry-over** | `POST /api/hr/leave-year-end/carry-over?fromYear=&employeeId=` | `LeaveYearEndService.ProcessCarryOverAsync` | **`HR.Leave.Admin`** |
| **Run forfeiture** | `POST /api/hr/leave-year-end/forfeiture?year=&asOf=&employeeId=` | `ProcessForfeitureAsync` | **`HR.Leave.Admin`** |

**Carry-over**, per balance in the closing year:

1. skip unless the leave type has `AllowCarryOver`;
2. skip if `AvailableDays <= 0`;
3. `carryAmount = min(available, MaxCarryOverDays)` when a cap is set;
4. find or create next year's balance *(creating it at the entitlement the engine resolves for
   that year)* and **set** — not add — its `CarriedOverDays`.

**Forfeiture**, per balance:

1. if `CarryOverExpiryMonths` is set and the as-of date has passed `1 January + N months`, zero
   `CarriedOverDays`;
2. if `ForfeitUnusedAfterMonths` is set and the as-of date has passed that cut-off, and no
   forfeiture adjustment already exists for the balance, post `-AvailableDays` as a
   `LeaveAdjustment` and recalculate.

`PerformedBy` on that adjustment is **the administrator who ran the job**. Until finish-plan lane 4
it was `Guid.Empty` — "system-posted" — and since `PerformedBy` is a required `Employee` foreign
key and no employee has the empty id, **every forfeiture ever posted would have failed on the
constraint.** The endpoint's only harness evidence had been a 403 check, so the insert had never
actually run.

### ⚠ Known gaps

| | |
|---|---|
| **L-24 · There is no dry run.** "Scope it to one employee first" is the warning panel's advice and the only safeguard; the runs write immediately. A preview mode returning the same result object without persisting would cost little and remove the whole class of accident | |
| **L-25 · Neither run is scheduled.** Both are buttons a human must remember to press, in January and in April. There is no recurring job and no reminder anywhere in the product | |
| **L-26 · Carry-over does not report what it *skipped*.** A balance with nothing to carry and a leave type with carry-over switched off both silently increment "processed" and never appear in the notes, so a run that carried nothing looks the same as a run that found nothing to carry | |

---

## 16. The portal — six screens, and the honest way to raise a request

### 📍 Where you are

**Window B**, signed in as your anchor employee · `/me/leave` · **10 minutes**

### 📖 What it is

The employee's own leave, on screens built for a person rather than a desk: no employee picker
anywhere, no organisation-wide reads, and reliever choices that come from their own roster rather
than a search they are not allowed to run.

> **This is where you raise the live request in the demo** — the portal's form creates *and*
> submits, so the workflow starts properly. See rule 1.

### 16.1 `/me/leave` — My Leave

**Header.** Title *My Leave*, subtitle *Your balances and requests. Approvals travel through the
configured workflow.* Back arrow to `/me`. Three buttons: **Planner** · **Encashments** ·
**New request**.

**Balances section.** A heading *BALANCES* with a small **year** dropdown on the right, then a
card per leave type:

```
  🌴 ANNUAL LEAVE
  28 days available
  entitled 21 · used 8 · pending 7 · carried over 0
```

**Requests section.** *REQUESTS IN 2026*, then a row per request — leave type, the request number,
the dates, the day count, and a coloured status badge. Each row links to the request.

**Empty states.** *No leave balances recorded for 2026. Balances appear once HR sets up your
entitlements.* / *No requests in 2026. When you file one it will appear here with its approval
state.*

▶ **Walk it.**

> "This is the same data, from the same tables, through the same endpoints. What is different is
> everything about how it is asked for — the employee never chooses an employee, because the only
> record they can reach is their own, and that is enforced on every single call, not by hiding a
> dropdown."

### 16.2 `/me/leave/new` — Request leave

**The form**, titled *Request leave*, subtitle *Submitting sends the request into the approval flow
configured for leave.*

The same fields as the desk form **minus the employee picker**, plus three differences that are
worth pointing out:

| Difference | What it looks like |
|---|---|
| **Notice is stated up front** | *"This leave type needs at least 14 days notice before it starts. Drafts can be saved any time."* |
| **Relievers are a dropdown, not a search** | the options are the employee's **own roster**, labelled *"Kofi Asante (priority 1)"* |
| **When the roster is empty, it says what will happen** | *"You have no pre-defined relievers, so one will be assigned automatically when you submit — usually your manager. Ask HR to set up your reliever roster if someone specific should cover for you."* |

Footer: **Cancel** · **Save as draft** · **Submit request**.

▶ **Walk it.**

**🔴 LIVE WRITE 12 — this is the request the room watches go through.**

1. **New request** → Leave type **Annual Leave**.
2. The balance panel appears: *Your balance for Annual Leave: 28 days available…*
3. The notice line appears beneath it.

> "Before I have typed a date, the form has told me two things: what I have, and that I need to
> give a fortnight's notice."

4. Set the dates — **a Monday at least three weeks out**, to the Friday of that week.
5. Reason: `Annual leave — family visit.`
6. Open the **Reliever** dropdown.

> "And here is a small decision that matters. As an ordinary member of staff I cannot search the
> employee register — I have no business doing so. So this is not a search box; it is my own
> reliever roster, the people HR has already agreed cover for me, in priority order.
>
> And if I leave it empty, the system fills it: my roster first, and if nobody there is free, my
> line manager. The note under the box says exactly that, because a form that quietly decides
> something on your behalf should say that it did."

7. Press **Submit request**. 🔴

Two toasts, in order: the request is created, then *Submitted — Your leave request is on its way
for approval.* You land on the request.

> "Notice what the portal did that the desk form does not: it created the request **and** sent it.
> If the second half had failed it would have said so honestly — *'saved, but not submitted'* —
> rather than pretending the whole thing failed, because the request would exist either way."

### 16.3 `/me/leave/[id]` — my request

**Header.** The leave type as the title, the request number and *requested &lt;date and time&gt;*
beneath it, a status badge on the right.

**Action row**, status-dependent: **✏ Edit draft** *(Draft)* · **Submit for approval** *(Draft or
Pending)* · **⊘ Cancel request** *(Draft, Pending or Approved)*.

**Details card.** Dates *(with the day count in brackets)* · Paid · Reason · Reliever · Second
reliever · Reliever notes · Handover notes — and, when cancelled, the date and reason.

**Attachments card.** **Attach a document** *(hidden once cancelled or completed)*, then a row per
file with a **download** and a **delete** icon. Empty state:

> *Nothing attached. Supporting documents (e.g. a medical certificate) are stored privately and
> only people entitled to this request can open them.*

**The cancel dialog** wording changes with the status: *"This request is already approved —
cancelling gives the days back to your balance."* versus *"The request will be closed and will not
be considered for approval."*

▶ **Walk it.**

> "My request, and only the actions that are actually available to me. Nothing greyed out,
> nothing that will refuse me — if the server would say no, the button is not there.
>
> And the attachment note. A sick note is the most sensitive document an employee ever hands to
> HR. It does not sit in a folder; it is scanned, catalogued in the document management system,
> and served only through a check on who is asking."

### 16.4 `/me/leave/[id]/edit`

The chapter 16.2 form, pre-filled, with **Save changes** as the only submit button and no *Save as
draft*. Draft only.

### 16.5 `/me/leave/planner` — my planner

**Header.** Title *Leave planner*, subtitle *Sketch your leave for the year, submit it, and settle
the dates with your manager before filing the real requests.* A **year** dropdown and a **Plan
leave** button.

**A card per plan:** leave type, the dates and notes, the status badge, and actions:

| Action | Shown when |
|---|---|
| **✏** *(edit)* and **Submit** | Draft |
| **Respond** | ChangesSuggested |
| **⊘** *(cancel)* | Draft, Submitted or ChangesSuggested |

**When the manager has suggested dates**, a blue panel appears inside the card:

> *Your manager suggests 9 Nov 2026 – 20 Nov 2026 — "Clashes with the year-end valuation;
> November works better for the section."*

**When rejected**, the reason appears in red.

**The plan dialog** — *"A plan is an intention, not a request — days are only charged when you
file the leave request itself."* Leave type · From · To · Notes.

**The respond dialog** — *"Your manager suggested different dates… Accept them, or counter with
your own — either way the plan goes back for review."* Two counter-date fields and Notes, with two
buttons: **Counter with my dates** and **Accept suggestion**.

▶ **Walk it.** *(This is the other half of chapter 10.7 — do them together if you can.)*

Open the plan you sent back in chapter 10 and press **Respond**.

> "And here is the conversation from the other side. My manager has come back with different
> dates and told me why. I have two answers, and the system carries both: I accept, or I counter
> with my own — and either way the plan goes back for review, with a fresh approval started.
>
> That loop is three clicks and it replaces a fortnight of email."

Press **Accept suggestion**. Toast: *Suggestion accepted — The plan went back for review.* The
plan returns to **Submitted**, on the manager's dates.

### 16.6 `/me/leave/encashments` — my encashments

**Header.** Title *My encashments*, subtitle *Convert unused leave days to cash where your leave
type allows it.* A year dropdown and a **Request encashment** button.

**A card per encashment:** the leave type and days, then the year, the amount payable, the paid
date, the payment reference and any notes; a status badge; and the rejection reason in red where
there is one.

**The request dialog** — *"Pick the leave request the days belong to. The payout is calculated by
HR from your emoluments and the leave type's rate policy."*

| Field | Notes |
|---|---|
| **Leave request** | a dropdown of *your* requests that are on a cash-convertible leave type, not already encashed, and not cancelled or rejected. When there are none: *"Encashment needs a leave request on a cash-convertible leave type that has not been encashed yet."* |
| **Days to encash** | number, min 0.5, step 0.5, with *"N days available for Annual Leave"* beneath it. **The button stays disabled if you exceed the balance** |
| **Notes** | textarea |

**There is no amount field.** Deliberately.

▶ **Walk it.**

> "Two things this dialog does not have, and both are the point.
>
> It has no amount box. The employee does not propose what they should be paid; the system derives
> it from their emoluments and the leave type's rate policy, on the server, where it cannot be
> edited.
>
> And it has no free choice of leave type. An encashment hangs off **a specific leave request** —
> days you had booked and are now converting — on a type whose rules permit it. That is what ties
> the money to a record, and it is why one request can only ever be encashed once."

### 16.7 Where the portal sends things

| The employee does | The desk sees it at |
|---|---|
| Files a leave request | `/hr/leave/requests` *(their row)*, and `/hr/leave/approvals` under their manager |
| Submits a leave plan | `/hr/leave/plans` at **Submitted** |
| Responds to a suggestion | the same plan, back at **Submitted**, on the new dates |
| Requests an encashment | `/hr/leave/encashments` at **Submitted** |
| Attaches a certificate | the request's **Attachments** tab |

And the other direction — everything HR does from the desk appears on the employee's own screens
within one refresh, because both sides read the same endpoints.

---

## 17. Where leave shows up outside its own menu

Four places, and they are worth a minute each if the room is interested in how the modules fit
together.

| Where | What it shows | Route |
|---|---|---|
| **The employee profile → Time & leave → Leave** | this year's balances as chips, then the request history, with a year picker and an **Open in Leave** button. **Read-only** — every write is back in the leave module | `/hr/employees/[id]` |
| **The employee profile → Employment → Relievers** | the reliever roster: order, reliever, their position and unit, active or *"Not in use"*. **This is the only screen that maintains it**, and priority 1 and 2 are what the leave form reads | `/hr/employees/[id]` |
| **The workflow inbox** | leave requests, plans and encashments awaiting *you*, alongside every other kind of approval in the product | `/workflow/inbox` |
| **The attendance monthly summary** | a **Days on leave** column, which is how payroll learns about it | `/hr/attendance/summaries` |

Two sentences worth having ready:

> "The employee record has a Leave tab, and it has no buttons. It is a window onto the leave
> module, not a second door into it — because a movement, a leave request or a salary change
> implemented from two screens is a rule implemented twice, and the second one drifts."

> "And the reliever roster lives on the employee's record rather than in the leave module, because
> it is a fact about the person's job — who can do it when they are not there — not a fact about
> any particular absence."

---

## 18. Reset — putting the database back

Do this after the room empties. Everything below is reversible; nothing needs a rebuild.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Draft request created** *(ch. 6, LW 1)* | It became LW 2 and 3. See row 2 |
| 2 | **Request submitted and approved** *(ch. 7, LW 2–3)* | Open it → **Cancel**, reason `Demonstration`. The days go straight back. ⚠ Cancel, do not close — a cancelled request is a clean withdrawal, a closed one is a completed absence that never happened |
| 3 | **Draft edited** *(ch. 8)* | Nothing to undo if you cancelled the request in row 2 |
| 4 | **Leave plan created, submitted, suggested, approved** *(ch. 10, LW 5–8)* | The ⋯ menu → **Cancel plan**. A cancelled plan stays visible with its history, which is correct |
| 5 | **Adjustment posted** *(ch. 12, LW 9)* | **Admin only.** Window C → `/hr/leave/adjustments` → the row → **Delete**. Then check the Balances screen went back |
| 6 | **Carry-over run** *(ch. 15, LW 10)* | It set next year's `CarriedOverDays` for one employee. Post a **negative adjustment** of the same size against that year, or accept it — it is one employee and it is arithmetically correct |
| 7 | **Forfeiture run** *(ch. 15, LW 11)* | Delete the `FORFEIT: unused leave (year-end/cut-off)` adjustment it posted, as admin. The balance recalculates. ⚠ **Do this**, or the run will skip that balance next time, believing it has already forfeited |
| 8 | **Portal request filed** *(ch. 16, LW 12)* | As the employee: open it → **Cancel request**, reason `Demonstration`. Or approve it first from the desk if you want to leave a complete example behind |
| 9 | **Rehearsal request** *(§2.4)* | Cancel it, if you did not at the time |
| 10 | **Attachments uploaded** | The request's Attachments tab → the **🗑** icon. The controlled upload and its DMS record are removed with it |

**The one thing that cannot be put back** is the **request number**. `LV2026000013` is spent. The
counter only moves forward, by design — the same rule as staff numbers and requisition numbers.
Nobody will notice, and a demo that has to explain a gap in a number sequence has bigger problems.

**The clean option.** If any of that looks fiddly, the whole database rebuilds in 45–60 minutes:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
  Where-Object { $_.CommandLine -like '*ErpSystem.Api*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild costs you chapter 2's twenty minutes as well, and it renumbers every request in this
book.

---

## 19. The short path — 25 minutes

When the slot shrinks. Seven screens, in this order, and the story still lands.

| # | Screen | Minutes | The one thing |
|---|---|---|---|
| 1 | `/administration/hr/leave-types` → **Annual Leave** → Overview + Accrual | 6 | the whole policy on one screen; **"you cannot take leave you have not earned"** |
| 2 | `/me/leave/new` *(as the employee)* | 4 | the balance and the notice rule before a date is typed; the reliever roster; **create-and-submit in one press** |
| 3 | `/hr/leave/requests/[id]` → Overview, then the **Workflow** tab | 5 | twelve calendar days charged as eight; then *"this is not leave, this is the engine"*. **Approve it** |
| 4 | `/hr/leave/balances` | 4 | read one row across; **nobody types a balance**; the opening-balance adjustment story |
| 5 | `/hr/leave/plans` — the clash check | 3 | name a busy reliever and let the amber alert appear; *"a warning, not a rule"* |
| 6 | `/hr/leave/compliance` | 2 | mandatory leave is the **employer's** obligation; Taken vs Scheduled vs Outstanding |
| 7 | `/hr/leave/year-end` *(as admin)* | 1 | read the amber panel; say *"a forfeiture is an adjustment with a name on it"* |

Cut, in this order if you must: year-end *(say the sentence instead)*, then compliance, then the
plan clash check.

---

## 20. What this walk found

Thirty-seven findings. **Two are blocking for a demonstration** and both have small fixes. Eleven
more are **ghost settings** — things the rulebook lets you configure that the engine does not read
— and §4.4 is the full trace; four of those are high severity. Nine others are worth closing
before the module is called finished.

| # | Where | Finding | Severity |
|---|---|---|---|
| **L-1** | ch. 6 | **The desk's *Submit request* button does not submit.** It creates the row at Pending with no workflow instance, after which it can neither be submitted nor approved, while its days are already deducted. Cancel is the only exit. The portal's form does it correctly | 🔴 **blocking** |
| **L-2** | ch. 13 | ***Mark as paid* is unusable.** The screen sends the login's user id where `ProcessedByEmployeeId` needs an employee id; the foreign key refuses it. One-word fix | 🔴 **blocking** |
| **L-3** | ch. 11 | **The *Accrued* column shows the entitlement, not the accrual.** The organisation-wide balance read returns the mapper's default; only the per-employee read overrides it. So the one screen that displays accrual is the one that does not compute it | **high** |
| **L-4** | ch. 11, §1.4 | **Available overstates what can be taken** for any accruing type — by seven days on annual leave in September. Correct as a policy figure; with L-3 unfixed, no screen anywhere shows the enforced one | **high** |
| **L-5** | §1.6 | **The seeded leave workflows do not prevent self-approval.** `PreventInitiatorApproval` is false on all three, so the raiser can approve their own leave, plan and encashment | **high** |
| **L-6** | ch. 5 | **No organisation-wide request register.** History by employee and pending-by-manager are the only list reads. "Who is off next week" cannot be asked | **high** |
| **L-7** | ch. 5 | The status filter is client-side over the fetched page, so it can under-report without saying so | medium |
| **L-8** | ch. 5 | The employee profile's *Open in Leave* deep link passes `?employeeId=`, which the page never reads | low |
| **L-9** | ch. 10 | **An approved plan cannot become a request.** `LeaveRequest.LeavePlanId` is read by the UI and written by nothing. The planning cycle dead-ends | **high** |
| **L-10** | ch. 9 | The approvals queue is built from the reporting line, not from the workflow assignee. Correct today by coincidence; wrong the moment a tenant publishes its own definition | medium |
| **L-11** | ch. 4 | Six delete/retire controls on the leave-type screens are rendered to a persona that holds none of them | medium |
| **L-12** | ch. 4 | Leave-type allowances — which drive the encashment rate — have no screen at all | medium |
| **L-13** | ch. 4 | The leave-type PUT is a replace-set; a partial body silently unlinks every allowance and resets every flag | medium |
| **L-14** | ch. 6 | The form's balance strip shows the policy figure, so the clerk's number and the server's differ | medium |
| **L-15** | ch. 6 | No attachment can be added while raising a request — the sick-note case, exactly | medium |
| **L-16** | ch. 10 | The plan dialog has no sub-type field, though the payload carries one | low |
| **L-17** | ch. 10 | A plan's year comes from the list filter, not its own start date, so a January plan raised in December vanishes | medium |
| **L-18** | ch. 11 | No export from the balance register | medium |
| **L-19** | ch. 11 | Recalculate is one employee at a time with no organisation-wide run | low |
| **L-20** | ch. 13 | No encashment detail page, and the derived rate is never shown — only the result | medium |
| **L-21** | ch. 13 | The encashment payout is not registered as a Finance money event *(HR↔Finance backlog rows 23, 24 — both 🔴)* | **high** |
| **L-22** | ch. 14 | The compliance screen is read-only: no reminder, export, department filter, or link to act | medium |
| **L-23** | ch. 14 | Compliance never names the forfeiture date the leave type carries, which is the consequence of being on the list | low |
| **L-24** | ch. 15 | No dry run on either year-end job | medium |
| **L-25** | ch. 15 | Neither year-end job is scheduled or reminded. ⚠ **Corrected 2026-09-17: the absence of a schedule is a recorded decision, not an oversight** — carry-over and forfeiture move balances rather than raise reminders, so automating them is TDC's policy call (`HR-CLOSURE-LEDGER.md`). What is genuinely missing is the *reminder* half | low |
| **L-26** | ch. 15 | Carry-over reports *processed* for balances it skipped, so "nothing to carry" and "carried nothing" look identical | low |
| **L-27** | §4.4.4 | **Approved leave never marks attendance.** Nothing writes `StaffDailyAttendance.LeaveRequestId` or the `OnLeave` status, so `DaysOnLeave` on the monthly summary — which the payroll export reads — stays at zero, and `LeaveRequest.AttendanceDays` is permanently empty. The largest structural gap in the module | **high** |
| **L-28** | §4.4.3 | **Sub-type caps never reach a balance.** `LeaveBalance` is keyed on (employee, type, year) with no sub-type, and the recalculation resolves entitlement with a null sub-type — so all three Sick sub-types share one 12-day pot and `MaxDaysAllowed` only ever affects a pre-flight check | **high** |
| **L-29** | §4.4.1 | **Pro-rate on exit is dead.** Defaults on, displayed back in the Accrual tab, read by nothing. A leaver accrues as though they worked the full year — and that figure feeds the final settlement | **high** |
| **L-30** | §4.4.1 | **`LeaveType.IsPaid` is display-only.** No attendance, payroll or export path reads it, so an unpaid leave type produces no deduction anywhere | **high** |
| **L-31** | §4.4.2 | **Leave ignores `SubstitutionDate`.** The Act 601 substitute Monday is charged as an ordinary leave day. `HrWorkingDayCalculator` — which discipline uses — honours it; leave has its own inline loop | medium |
| **L-32** | §4.4.2 | **Leave ignores `PublicHoliday.IsActive` and the holiday calendar.** It reads every holiday in the tenant regardless of which calendar it belongs to or whether it is active | medium |
| **L-33** | §4.4.2 | `ObservanceType`, `AttractsHolidayPay` and `HolidayPayMultiplier` have **zero read-sites anywhere in the solution** | medium |
| **L-34** | §4.4.3 | `LeaveSubType.IsActive` is never filtered, so a retired sub-type stays in the request form's dropdown | low |
| **L-35** | §4.4.3 | `LeaveType.IsActive` is enforced by the picker and not by the service — a retired type is still usable through the API | low |
| **L-36** | §4.4.1 | `CalendarColor` is collected, validated as a hex colour, stored — and used only for the dot on the leave-types register. There is no leave calendar | low |
| **L-37** | §4.4.4 | `MedicalExpenseClaim.LeaveRequestId`, commented *"Leave Integration"*, is never written | low |

**Two fixes close the demonstration risk entirely.**

`L-1` is three lines: make the desk's new-request page call `leaveService.submit(created.id)`
after a non-draft create, exactly as `/me/leave/new` already does, and report a failed submit
honestly instead of silently. `L-2` is one word: `user?.employeeId` in place of `user?.id`. Both
are in the frontend; neither touches the API.

**Four of the ghost settings need a decision, not just a fix.** `IsPaid` (L-30), pro-rate-on-exit
(L-29), the sub-type pot (L-28) and the attendance join (L-27) are each a switch or a foreign key
that somebody configured in good faith and that changes nothing. Until they are wired or removed,
**do not promise any of them in a demonstration** — §4.4.1's table is written so you can read the
right answer off it if you are asked.

**The most valuable thing this module could gain next** is **L-9** — a *Raise the request* action
on an approved leave plan. Every other finding here is a refinement of something that works. That
one is a whole half of a feature that stops one step short of paying for itself: the organisation
plans its year, settles the clashes, gets the approvals — and then everybody re-types their dates
into a different form, at which point the plan and the request can quietly disagree and nothing
notices.

---

## Appendix A — every route, in demo order

| # | Route | Persona | Writes |
|---|---|---|---|
| 1 | `/hr/leave` | hr.head | — |
| 2 | `/administration/hr/leave-types` | hr.head | — |
| 3 | `…/leave-types/[id]` — Overview, Allocations, Accrual | hr.head | — |
| 4 | `…/leave-types/[id]` — Eligibility *(Maternity)*, Sub-types *(Sick)* | hr.head | — |
| 5 | `/hr/leave/requests` | hr.head | — |
| 6 | `/hr/leave/requests/new` | hr.head | **LIVE 1** — creates a draft |
| 7 | `/hr/leave/requests/[id]` | hr.head | **LIVE 2** — submits · **LIVE 3** — approves |
| 8 | `/hr/leave/requests/[id]/edit` | hr.head | amends a draft |
| 9 | `/hr/leave/approvals` | hr.head | — |
| 10 | `/workflow/inbox` | hr.head | — *(shown, not used)* |
| 11 | `/hr/leave/plans` | hr.head | **LIVE 5** — creates · **LIVE 6** — submits · **LIVE 7** — suggests · **LIVE 8** — approves |
| 12 | `/hr/leave/balances` | hr.head | *(Recalculate — no net change)* |
| 13 | `/hr/leave/adjustments` | hr.head | **LIVE 9** — posts an adjustment |
| 14 | `/hr/leave/encashments` | hr.head | — ⚠ **do not press Mark as paid** |
| 15 | `/hr/leave/compliance` | hr.head | — |
| 16 | `/hr/leave/year-end` | **admin** | **LIVE 10** — carry-over · **LIVE 11** — forfeiture *(both scoped to one employee)* |
| 17 | `/me/leave` | staff | — |
| 18 | `/me/leave/new` | staff | **LIVE 12** — creates **and submits** |
| 19 | `/me/leave/[id]` | staff | — |
| 20 | `/me/leave/planner` | staff | responds to the suggestion from row 11 |
| 21 | `/me/leave/encashments` | staff | — *(dialog shown, not submitted)* |
| 22 | `/hr/employees/[id]` → Leave, Relievers | hr.head | — |

*(LIVE 4 — closing a request — is conditional; see chapter 7 step 7.)*

## Appendix B — the permission map, in one table

| Act | Permission | `hr.head`? |
|---|---|---|
| Read the leave-type catalogue and its five tabs | *(none)* | ✔ |
| Read your own requests, balances, plans and encashments | *(none — an ownership check)* | ✔ |
| Read a manager's own approval queue | *(none — an ownership check)* | ✔ |
| Read anyone's balances, adjustments, plans, encashments, compliance | `HR.Leave.Read` | ✔ |
| Raise, amend or cancel leave for anyone | `HR.Leave.Write` | ✔ |
| Submit anyone's request or plan | `HR.Leave.Write` | ✔ |
| **Close** a completed request | `HR.Leave.Write` *(not self)* | ✔ |
| Post or amend a balance adjustment | `HR.Leave.Write` | ✔ |
| Recalculate a balance | `HR.Leave.Write` | ✔ |
| Record an encashment payment | `HR.Leave.Write` | ✔ *(but see L-2)* |
| Create or amend a leave type and its configuration | `HR.Leave.Write` | ✔ |
| Upload or delete an attachment | `HR.Leave.Write` *(or the owner)* | ✔ |
| **Approve / reject a request, plan or encashment** | *(none — the workflow assignee check)* | ✔ *via the Manager/HR role on the seeded step* |
| **Run carry-over or forfeiture** | `HR.Leave.Admin` | **✘** |
| **Retire a leave type** | `HR.Leave.Admin` | **✘** |
| **Delete a sub-type, allocation, eligibility rule or accrual policy** | `HR.Leave.Admin` | **✘** |
| **Delete an adjustment** | `HR.Leave.Admin` | **✘** |
| Approve where no workflow definition is published | `HR.Leave.Approve` | ✔ *(inert here — a definition is published)* |

## Appendix C — related documents

| Document | For |
|---|---|
| `docs/HR/HR-EMPLOYEES-SYSTEM-GUIDE.md` | the employee record this module hangs off; its chapter 5 covers the Relievers tab and the Leave tab from the other side |
| `docs/HR/HR-RECRUITMENT-SYSTEM-GUIDE.md` | the same treatment for recruitment; the first of the series |
| `docs/HR/HR-WORKFLOW-ENGINE-INTEGRATION.md` | the four-step recipe behind chapters 7, 10 and 13, and the auto-approve defect the guards in `HrWorkflowFallbackAuthority` close |
| `docs/HR/HR-REPORTS-CATALOGUE.md` §3.3 | the four leave reports — register, balance, encashment register, liability — and which exist |
| `docs/HR/HR-FINANCE-ENTITY-SWEEP.md` | rows 23 and 24: the encashment payout and the leave-liability figure, both deferred to the GL sweep |
| `docs/HR/HR-PAYROLL-BOUNDARY.md` | what payroll reads from leave, and why the encashment stops at a payment reference |
| `docs/UAT-DEMO-DATABASE.md` + `scripts/New-UatDatabase.ps1` | building `ErpSystemDB_UAT`; `TdcDemoLeaveCalendarSeeder` is the leave vocabulary |
| `dev-harness/hr-demo-smoke/scenarios/020-leave-balances.mjs`, `021-leave-requests.mjs` | exactly what the demo data is and how it was built |
| **`docs/HR/HR-LEAVE-CLOSURE-PLAN.md`** | **what remains before this module is done** — the 50-item gap register, the eleven decisions, and the slice plan. **Start there for any leave work; this guide is the evidence behind it** |
| `dev-harness/hr-demo-smoke/runbook/` | Books 0–4 and Book R — the printed demo pack this guide supplements |
