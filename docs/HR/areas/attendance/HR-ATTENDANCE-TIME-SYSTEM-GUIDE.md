# HR Attendance & Time Management — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source — page components, forms, controllers, services,
the EF model, the workflow definitions, the seeded permission map and the demo scenarios. It
describes what the code is built to do. Where the screen says one thing and the server does
another, the step says so rather than smoothing it over.

**Scope:** the whole **Attendance & Time** group of the HR sidebar — all eleven menu items —
plus the ten screens that hang off them without a menu entry, the **eight setup areas** under
Administration → HR → Time, Attendance & Leave, and the employee portal's **My Attendance**,
which is where the whole chain starts.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| — | *(group landing)* Attendance & Time | `/hr/attendance` | 3 |
| — | *(Administration)* setup hub | `/administration/hr/attendance` | 4 |
| — | *(Administration)* Work Schedules | `/administration/hr/attendance/work-schedules` | 5 |
| — | *(Administration)* Shift Rotations | `/administration/hr/attendance/shift-rotations` | 6 |
| — | *(Administration)* Holiday Calendars | `/administration/hr/attendance/holiday-calendars` | 7 |
| — | *(Administration)* Pay Periods | `/administration/hr/attendance/pay-periods` | 8 |
| — | *(Administration)* Geofence Zones | `/administration/hr/attendance/geofence-zones` | 9 |
| — | *(Administration)* Devices | `/administration/hr/attendance/devices` | 10 |
| — | *(Administration)* Alert Rules | `/administration/hr/attendance/alert-rules` | 11 |
| — | *(Administration)* Overtime Policies | `/administration/hr/attendance/overtime-policies` | 12 |
| 1 | Daily Attendance | `/hr/attendance/daily` | 13 |
| — | *(no menu entry)* Record a day | `/hr/attendance/daily/new` | 14 |
| — | *(no menu entry)* The day | `/hr/attendance/daily/[id]` | 15 |
| 2 | Attendance Records | `/hr/attendance/records` | 16 |
| 3 | Punch Logs | `/hr/attendance/logs` | 17 |
| 4 | Regularizations | `/hr/attendance/regularizations` (+ `new`, `[id]`) | 18 |
| 5 | Overtime Requests | `/hr/attendance/overtime` (+ `new`, `[id]`) | 19 |
| 6 | Remote Work | `/hr/attendance/remote-work` (+ `new`, `[id]`) | 20 |
| 7 | Monthly Summaries | `/hr/attendance/summaries` | 21 |
| 8 | Alerts | `/hr/attendance/alerts` | 22 |
| 9 | Bulk Imports | `/hr/attendance/imports` (+ `new`, `[id]`) | 23 |
| 10 | Payroll Exports | `/hr/attendance/payroll-exports` | 24 |
| 11 | Biometrics | `/hr/attendance/biometrics` | 25 |
| — | *(portal)* My Attendance | `/me/attendance` | 26 |

**Thirty-six screens** across three areas of the product, sitting on **twenty-four tables**.
This is the largest module in HR by surface area, and the only one whose output another
company's payroll clerk will read.

---

## This document is two things at once

Like the employees and leave guides, this is a **reference** and a **script you can perform**.
Every chapter has the same five parts, and you can read only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Three more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered
  (`LIVE WRITE 1` … `16`), and chapter 28 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.
- **🚫 DO NOT PRESS** — a control that renders for your persona and returns 403, or one that
  writes something you cannot undo inside a demo.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or
less as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the five rules that decide whether this demo works

Attendance is the most *visible* module in an HR system — everybody in the room has clocked in
somewhere — and this one demonstrates beautifully. But it has five behaviours that will make
you look wrong in front of people if you meet them for the first time live. Read these twice.
They are why chapter 2 exists.

### Rule 1 — A punch never computes lateness. Only a typed record does.

The self-service punch (`/me/attendance` → **Check in**) creates a raw log, folds it into the
day, records the GPS verification and derives the hours worked. It does **not** look at the
employee's work schedule. So it never sets `WorkScheduleId`, never sets the scheduled start or
end, never sets `PayPeriodId`, and **never marks anybody late** — the day is always created at
status *Present*, whatever the clock says.

`LateGracePeriodMinutes` is on the Work Schedule form, it saves, it round-trips — and **no
code in the solution ever reads it**. The same is true of the flexible-hours window, the core
hours, the mandatory break and the daily/weekly overtime caps.

**What this means for you:** the lateness on screen in the demo is *seeded* lateness — the demo
scenario typed it onto four specific days. If you punch in live at 11:00 to "show a late
arrival", the system will record you as Present, on time. Demonstrate lateness from the seeded
days (chapter 13), and be ready to say the honest sentence in § 1.5. This is finding **A-1**.

### Rule 2 — The bulk import screen writes nothing from a pasted CSV

`/hr/attendance/imports/new` parses your CSV in the browser, shows a tidy preview, stages the
batch and reports *"Completed — 30 of 30 rows applied"*. **Zero attendance records are
created.** The server only writes a day when the row carries an employee **GUID**, and the
browser parser reads the employee *number* from your file and then throws it away, sending
`employeeId: null` for every row. Every row is then marked successful without doing anything.

The seeded import in the demo database *did* work — the seeding script supplied real GUIDs. So
the **history** on `/hr/attendance/imports` is genuine and safe to show. **Do not run a new
import live.** This is finding **A-2**, and it is the single most demo-dangerous thing in the
module.

### Rule 3 — Recalculating a monthly summary fills in six figures out of twenty-two

**Recalculate** on `/hr/attendance/summaries` rebuilds the summary from the daily records. It
sets days present, days absent, days on leave, total overtime hours, number of late days and
total late minutes. It does **not** set total working days, total worked hours, attendance
percentage or punctuality percentage — and those are four of the ten columns on the very screen
you pressed the button on.

So a row you recalculate live goes from a plausible-looking summary to `Worked —`, `Attendance
0.0%`, `Punctuality 0.0%`. **Do not press Recalculate on a row you are about to talk about.**
Demonstrate it on a row you are willing to spoil, and say what the button is *for* rather than
what it fills in. This is finding **A-3**.

### Rule 4 — Payroll Exports is empty, and running one will report zero records

Two independent reasons, and they compound:

1. The demo dataset **deliberately creates no exports** (`StaffAttendancePayrollExports` is
   marked *excluded* in the coverage manifest), so the screen opens empty.
2. An export counts the monthly summaries attached to the chosen pay period — and
   **nothing ever attaches a summary to a pay period.** `PayPeriodId` on a summary is written
   by no code path. So the export completes, reports `0 employees / 0 records`, and looks
   broken.

Chapter 24 turns this into an architecture beat rather than a click. If you want a number on
screen, chapter 2 § 2.6 shows the one-line SQL that attaches last month's summaries to last
month's period. This is finding **A-4**.

### Rule 5 — `hr.head` cannot delete anything in this module

The `HR` role holds `HR.Attendance.Read`, `HR.Attendance.Write` and `HR.Attendance.Approve`. It
does **not** hold `HR.Attendance.Admin`, and **every delete in the module is Admin-gated**:

| Screen | Control that 403s for `hr.head` |
|---|---|
| Work Schedules | ⋯ → **Delete**, and **Remove shift** on the Shifts tab |
| Shift Rotations | ⋯ → **Delete** on a plan, a stage or a member |
| Holiday Calendars | ⋯ → **Delete** on a calendar or a holiday |
| Pay Periods | ⋯ → **Delete** |
| Geofence Zones | ⋯ → **Delete** |
| Devices | ⋯ → **Delete** |
| Alert Rules | ⋯ → **Delete** |
| Overtime Policies | ⋯ → **Delete** on a policy or an override |
| Biometrics | ⋯ → **Remove** *(but* **Revoke** *works — it is Write)* |

Every one of those buttons renders. None is hidden. They are gated only on the server, so the
only way to find out is to press one. **Chapters 5–12 mark every one.** Deleting is performed
once, deliberately, as **admin** in chapter 9. This is finding **A-5**.

### And one question you will be asked — buddy punching

Not a rule, but read it before you present. Within a minute of seeing the self-service punch
somebody will ask *"what stops me giving my password to a colleague to clock me in?"* The answer
is prepared, written out and honest in **§ 1.7**, with the say-line. Do not improvise it. Short
version: the punch proves a session and a device's location, never a person; only a biometric
taken at the instant of the punch closes that, which is exactly what the device register and the
biometric enrolment register in chapters 24 and 25 were built for — the missing piece is the
reader connector. Findings **A-93** and **A-94**.

---

## Conventions

**Routes.** `/hr/attendance/daily/[id]` is the file
`frontend/src/app/hr/attendance/daily/[id]/page.tsx`. A segment in square brackets is a
parameter.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in the solution. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — `StaffDailyAttendance`
lives in `StaffDailyAttendances`, `GeofenceZone` in `GeofenceZones`.

**Columns every table here carries.** Every entity in this guide derives from `TenantEntity`:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it explicitly |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

**The permission ladder.** Three permissions gate this module, plus one interim approval tier:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Attendance.Read` | every organisation-wide read — the dashboard, daily attendance, punch logs, requests, summaries, alerts, devices, biometrics, imports, exports, and all eight setup registers | **yes** |
| `HR.Attendance.Write` | correct a day, verify it, approve its exception, capture and process punches, apply an approved regularization, confirm overtime hours, finalise a summary, run an export, acknowledge alerts, enrol and revoke biometrics, close a pay period, and maintain **all** schedule / shift / rotation / holiday / pay-period / geofence / device / alert-rule / overtime-policy configuration | **yes** |
| `HR.Attendance.Admin` | **delete** — attendance rows, punch logs, alerts, biometric templates, imports, exports, summaries and every configuration row | **no** |
| `HR.Attendance.Approve` | interim authority to approve a regularization, an overtime request or a remote-work request **where no workflow definition is published**. All three *are* seeded and published on this database, so in practice this does nothing | **yes** |

**Self-access is not a permission.** An employee reads and writes *their own* attendance
without holding any of these. Every per-employee endpoint asks the same question — *are you the
employee this record is about, or do you hold the attendance tier?* — in a helper called
`SelfOrPolicyAsync` on `AttendanceControllerBase`. That is why **My Attendance** in the portal
needs no grant at all, and why the punch endpoint carries no `[Authorize]` beyond
*signed-in-and-internal*.

**The actor is always the token.** Every action endpoint in this module — verify, approve,
reject, confirm, finalise, export, enrol, acknowledge, close — takes the acting person from
`CurrentUser.EmployeeId`, not from the request body. The `approvedById` / `verifiedById` /
`exportedById` fields on the DTOs are required by model binding and then **ignored**; the
frontend sends a zero GUID for them on purpose. One consequence worth knowing:

> **A signed-in user whose account is not linked to an employee record gets
> `400 — "Your user account is not linked to an employee record"` on every action in this
> module, even with full permissions.** Reads are unaffected. If a demo account misbehaves,
> this is the first thing to check.

---

## 1. How attendance and time hangs together

### 1.1 One pipeline, five stages, one lock

Recruitment is a chain. Employees is a hub. Leave is a ledger. **Attendance is a pipeline with
a lock two-thirds of the way along.** Five stages, and they must be understood in this order:

```
 ┌──── 0. THE RULEBOOK (Administration → Time, Attendance & Leave) ────────────┐
 │  WorkSchedule      — 08:00–17:00, Mon–Fri, 40 h/week, grace periods         │
 │    └── ShiftDefinition  — Day 08:00–17:00 · Night 20:00–05:00 (+15%)        │
 │         └── ShiftRotationPlan → Stages → Members (who rides the rota)       │
 │  HolidayCalendar → PublicHoliday   — which days are not working days        │
 │  PayPeriod         — the cut-off window attendance is totalled against      │
 │  GeofenceZone      — where a phone punch is allowed to come from            │
 │  StaffAttendanceDevice — the readers on the gates                           │
 │  StaffAttendanceAlertRule — what should raise a flag, and at what number    │
 │  PositionOvertimePolicy (+ EmployeeOvertimeOverride) — who may claim OT     │
 └───────────────────────────────┬─────────────────────────────────────────────┘
                                 │  shapes
                                 ▼
 ┌──── 1. THE PUNCH ───────────────────────────────────────────────────────────┐
 │  StaffAttendanceLog — one row per event: CheckIn / CheckOut / BreakStart /   │
 │  BreakEnd, with a timestamp, a device serial, and GPS if the phone gave it   │
 │  ↳ AttendanceLocationVerificationLog — the geofence verdict for that punch   │
 │  IsProcessed = false until it has been folded into a day                     │
 └───────────────────────────────┬─────────────────────────────────────────────┘
                                 │  processed into
                                 ▼
 ┌──── 2. THE DAY ─────────────────────────────────────────────────────────────┐
 │  StaffDailyAttendance — ONE ROW per employee per date. 60 columns:           │
 │    scheduled in/out · actual in/out · hours worked · break                   │
 │    late? + minutes · early departure? + minutes                              │
 │    overtime? + hours + approved-by                                           │
 │    location · GPS in/out · geofence verdict · device · IP                    │
 │    remote? · leave link · holiday link · pay-period link                     │
 │    requires verification? · verified? · has exception? · exception approved? │
 │                                                                              │
 │  Three doors write it:  a processed punch · the Record-a-day form ·          │
 │                         a processed bulk-import row                          │
 │  One door corrects it:  an APPROVED and APPLIED regularization               │
 └───────────────────────────────┬─────────────────────────────────────────────┘
                                 │  rolled up into
                                 ▼
 ┌──── 3. THE MONTH ───────────────────────────────────────────────────────────┐
 │  StaffMonthlyAttendanceSummary — ONE ROW per employee × year × month         │
 │  Days present / absent / on leave / late · hours worked / overtime           │
 │  Attendance % · Punctuality %                                                │
 │                                                                              │
 │              🔒  IsFinalized  — THE LOCK. Once set:                          │
 │                  · the summary cannot be recalculated                        │
 │                  · it cannot be edited                                       │
 │                  · it cannot be deleted                                      │
 │                  · payroll may read it                                       │
 └───────────────────────────────┬─────────────────────────────────────────────┘
                                 │  handed over by
                                 ▼
 ┌──── 4. THE HAND-OFF ────────────────────────────────────────────────────────┐
 │  PayPeriod.Status: Open → Closed                                             │
 │  StaffAttendancePayrollExport — reference, date, who, target system,          │
 │  how many employees, how many records, outcome. An audit trail, not a file.  │
 └──────────────────────────────────────────────────────────────────────────────┘

 Beside the pipeline, three request types, each on the workflow engine:
   • StaffAttendanceRegularization — "my day is recorded wrong, please fix it"
   • StaffOvertimeRequest          — "may I work late?" then "he did work late"
   • RemoteWorkRequest             — "may I work from home next week?"

 And two registers that identify a person to a machine:
   • EmployeeBiometric        — the fingerprint template on the reader
   • StaffAttendanceDevice    — the reader itself
```

Four points follow, and they are the four worth landing in a room:

**1. A punch is not attendance.** It is a fact about a moment. It becomes attendance when it is
*processed* into a day. Keeping them apart is what lets a device sync three days late, lets a
failed match be found and fixed, and gives you a raw record that nobody edited. Every punch
carries its device, its coordinates and its raw payload, and `IsProcessed` tells you whether it
has been used yet.

**2. Nobody edits a month.** The monthly summary is derived from the days beneath it. There is
a **Recalculate** that rebuilds it from source, and there is a **Finalise** that locks it. There
is no screen anywhere that lets a person type "days present". If the month is wrong, a *day* is
wrong — and a day is wrong for a reason that has a name, a requester and an approver on it.

**3. The lock is the whole argument.** Payroll's objection to any attendance system is *"how do
I know this will not change after I have paid it?"* The answer is `IsFinalized`: once HR sets
it, the API itself refuses to recalculate, edit or delete that summary. Say this sentence out
loud in the demo; it is the one a finance director is listening for.

**4. A correction is a request, not an edit.** Nobody with a login can simply retype somebody's
clock-out. They raise a **regularization**: a numbered record with a type, a reason, the
requested times and an approval chain. And even after approval it does not touch the day until
somebody presses **Apply** — deliberately two steps, so an approved correction can be reviewed
before it changes reported hours.

### 1.2 The tables

Twenty-four, in the order the pipeline uses them.

| # | Table | One line |
|---|---|---|
| 1 | `WorkSchedules` | A named working pattern — hours, days, breaks, overtime rules, grace periods |
| 2 | `EmployeeWorkSchedules` | Which schedule a person is on, from when. **No screen writes this** (A-6) |
| 3 | `ShiftDefinitions` | A named shift inside a schedule — Day, Night, with allowance flags |
| 4 | `ShiftAssignments` | A person on a shift for a date window. **No screen writes this** (A-6) |
| 5 | `ShiftRotationPlans` | A cycle that moves crews through shifts on a rhythm |
| 6 | `ShiftRotationStages` | Position *n* in that cycle → which shift |
| 7 | `ShiftRotationMembers` | Who rides the plan, and which stage they are on now |
| 8 | `HolidayCalendars` | A set of public holidays, by country or region |
| 9 | `PublicHolidays` | One holiday entry — a date *range*, with observance type and pay multiplier |
| 10 | `PayPeriods` | The cut-off window. Open → Closed → Exported |
| 11 | `GeofenceZones` | A circle or polygon a phone punch must fall inside |
| 12 | `StaffAttendanceDevices` | A registered reader: id, model, IP, location, last sync |
| 13 | `EmployeeBiometrics` | An enrolled template — type, finger, quality, format, device |
| 14 | `StaffAttendanceLogs` | The raw punch |
| 15 | `AttendanceLocationVerificationLogs` | The geofence verdict for one punch |
| 16 | `StaffDailyAttendances` | **The centre of the module.** One employee, one date, 60 columns |
| 17 | `StaffAttendanceRecords` | A second, deliberately simple register: in, out, hours, status |
| 18 | `StaffAttendanceRegularizations` | A numbered request to correct a day |
| 19 | `StaffOvertimeRequests` | Pre-approval, then confirmation of hours actually worked |
| 20 | `RemoteWorkRequests` | A dated request to work off-site |
| 21 | `StaffMonthlyAttendanceSummaries` | The monthly roll-up, and the lock |
| 22 | `StaffBulkAttendanceImports` | A batch load, with counts and an error summary |
| 23 | `StaffBulkAttendanceImportRows` | One row of that batch, with its own success flag and error |
| 24 | `StaffAttendancePayrollExports` | The audit trail of a hand-off to payroll |

> Four more tables sit in the same entity file and are **out of scope** here:
> `ConsultantTimesheets`, `ConsultantTimesheetEntries`, `ClientTimesheetConfirmations`,
> `TimesheetInvoices` / `TimesheetInvoiceLinks`. They belong to the **Consulting** module, which
> has its own menu and its own client-facing confirmation flow. They share this module's
> permissions because they share its controllers' gating — see § 27.

### 1.3 The five vocabularies worth memorising

There are five enum families in this module and the room will hear all of them. Learn the
first two; look the others up.

**Attendance status** — ten values, the shape of a day:

| Value | Shown as | Counts toward attendance %? |
|---|---|---|
| `Present` | Present | ✅ numerator and denominator |
| `OnDuty` | On duty | ✅ numerator and denominator |
| `Late` | Late | ✅ — the person *was* at work; punctuality is measured separately |
| `HalfDay` | Half day | ✅ |
| `RemoteWork` | Remote work | ✅ |
| `Absent` | Absent | ❌ numerator, ✅ denominator |
| `OnLeave` | On leave | ❌ numerator, **✅ denominator by default** — see § 1.4 |
| `Weekend` | Weekend | excluded entirely |
| `PublicHoliday` | Public holiday | excluded entirely |
| `OffDay` | Off day | excluded entirely |

**Regularization type** — five values, *why* a day is wrong: Missing check-in · Missing
check-out · Wrong time entry · Forgot to mark · System error.

**Overtime type** — four: Weekday · Weekend · Holiday · Emergency.
**Overtime allowance type** — seven, used by the *policy* screen rather than the request:
Overtime · Night allowance · Shift differential · Weekend allowance · Holiday allowance ·
Transport allowance · Other.

**Alert trigger** — seven: Consecutive absences · Chronic lateness · Missing punch · Overtime
threshold reached · Excessive early departure · Unauthorised absence · Low attendance
percentage. **One of them does nothing at all** — see chapter 22.

**Location verification** — four: Within zone · Outside zone · Unverified · GPS unavailable.
The last two are not failures; they mean *the question could not be asked*, because the employee
has no work location, the location has no zone, or the browser gave no coordinates.

### 1.4 The arithmetic — the single most important page in this book

Three numbers get quoted in this module and all three are computed differently. Get these right
and nothing in the demo can trip you.

**① Hours worked on a day.**

```
    ActualWorkHours = ActualCheckOutTime − ActualCheckInTime      (rounded to 2 dp)
```

Derived **only** when both punches exist on the same day and out is later than in. An overnight
pair (out before in) is deliberately left `null` rather than going negative. Breaks are recorded
(`TotalBreakMinutes`) but **are not subtracted** — the paid-break flag on the schedule is stored
and never read. On the *Record a day* form you type the hours yourself, and nothing checks them
against the times you also typed.

**② Attendance rate — the dashboard figure.**

```
                 Present + OnDuty + Late + HalfDay + RemoteWork + (IsRemoteWork)
    rate  =  ───────────────────────────────────────────────────────────────────  × 100
              every day that is NOT Weekend, PublicHoliday or OffDay
```

Two things about it are deliberate and both are worth saying aloud:

- **Late and half-days are in the numerator.** The person *was* at work. Punctuality is a
  separate figure and penalising lateness twice would read as 0% attendance on a day when
  everyone arrived five minutes late.
- **Approved leave is in the *denominator* by default.** It was a scheduled working day the
  person did not attend. Days on leave are reported next to the rate so the reason stays
  visible. This is a tenant setting —
  `CompanyHrPolicySettings.AttendanceRateIncludesApprovedLeave` — and it is read **once** per
  dashboard so the headline, the trend and the chronic-absentee list can never disagree.

The same formula is computed in **three** places: today's tiles, the 7-day trend, and the
chronic-absentee ranking. If someone asks *"is the risk list using the same rule as the
headline?"*, the answer is yes, from a single read.

**③ Monthly attendance % and punctuality % — the payroll figures.**

```
    AttendancePercentage  = DaysPresent / TotalWorkingDays × 100
    PunctualityPercentage = (DaysPresent − DaysLate) / DaysPresent × 100
```

⚠ **Neither is computed by Recalculate.** Both columns are on the entity, both are on the
screen, and the recalculation routine sets neither — nor `TotalWorkingDays`, nor
`TotalWorkedHours`. On the demo database these figures read as whatever the seeding left. See
Rule 3 and finding **A-3**.

### 1.5 What the work schedule decides, and what it does not

This is the honest slide, and having it ready turns the module's weakest point into a
credibility moment. The Work Schedule form carries about 35 settings. Here is what each one
actually does today:

| Setting | Stored | Shown on screens | **Enforced by the server** |
|---|---|---|---|
| Name, type, active, default | ✅ | ✅ | ✅ (Default is read when picking a schedule) |
| Standard start / end / hours per day / per week | ✅ | ✅ | ❌ — displayed, never compared |
| Working days (Mon–Sun) | ✅ | ✅ | ❌ — a punch on a Sunday is still *Present* |
| Flexible start / end window | ✅ | ✅ | ❌ |
| Core hours | ✅ | ✅ | ❌ |
| Mandatory break, length, paid? | ✅ | ✅ | ❌ — breaks are never deducted |
| Allows overtime / requires pre-approval | ✅ | ✅ | ❌ — an overtime request is never refused for it |
| Max overtime per day / per week | ✅ | ✅ | ❌ — no cap is applied |
| **Late grace period (minutes)** | ✅ | ✅ | ❌ — **nothing in the solution reads it** |
| **Early-departure grace period** | ✅ | ✅ | ❌ — same |

> **Say it like this:**
>
> *"The schedule is the company's statement of what a working day is — and today it is
> exactly that: a statement. Lateness in this system is a fact somebody records, not a fact the
> clock infers. That is a deliberate first step for a corporation that has never had electronic
> attendance: you get the register, the corrections, the approvals and the audit trail first,
> and you turn on automatic lateness once everybody trusts the times. The rules are already
> captured — grace periods, core hours, break policy, overtime ceilings — so switching them on
> is configuration, not a rebuild."*

That is true, it is useful, and it is much stronger than being caught out by it.

### 1.6 Where the approval actually happens

Three record types in this module go through the **generic workflow engine**, and one important
action deliberately does not.

| Record | Entity type the engine knows | Definition seeded? | Approvers |
|---|---|---|---|
| Attendance regularization | `StaffAttendanceRegularization` | ✅ *Attendance Regularisation Approval* | roles: **Manager**, **HR**, **TenantAdmin** |
| Overtime request | `StaffOvertimeRequest` | ✅ *Overtime Approval* | same three roles |
| Remote-work request | `RemoteWorkRequest` | ✅ *Remote Work Approval* | same three roles |
| Verifying a day / approving its exception | — | — | **not a workflow**: a plain supervisor act with `HR.Attendance.Write` |

Five things follow, and each has bitten somebody:

1. **None of the three has a Draft.** Their status enums start at *Pending*, so the workflow
   starts the instant the record is created. There is **no Submit button anywhere in this
   module** — and that is correct, not missing. (Contrast the leave module, where the missing
   submit is a real defect.)
2. **The status is set by the engine, not by the screen.** The service relays your decision and
   an `IWorkflowStatusAdapter` writes the status. A multi-step definition would leave the record
   *Pending* after your approval. The seeded definitions are single-step, so one approval lands
   it on *Approved*.
3. **The definitions do not prevent initiator approval.** `hr.head` can approve what `hr.head`
   raised. Convenient for a one-window demo — say it out loud rather than hoping nobody notices.
4. **Approve and Reject are not permission-gated.** They are the assignee's act, checked per
   request by the engine. If you are not the assignee the buttons render **disabled**, with the
   step and the pending approver named beside them. They do not 401.
5. **Approving is not applying.** For a regularization there is a second, separate button —
   **Apply correction** — which writes the requested times onto the day. Until you press it the
   day is unchanged.

The **Awaiting** column on all three registers is read from the engine in one batched call per
page, so it tells you the live step and the pending approver, not something inferred from the
status. `/workflow/inbox` is the authoritative queue.

---

### 1.7 Buddy punching — the question you will be asked

Somebody in the room will ask it, usually within a minute of seeing the portal punch in chapter
26: *"what stops me giving my password to a colleague so they clock me in?"* Have this ready.
The honest answer is better than the defensive one, and it ends with a decision the room can
make rather than a weakness they found.

**Name the problem correctly first.** Buddy punching is not an authentication failure. The
system authenticates perfectly — it is simply being shown valid credentials by the wrong human.
It is a **presence-proof** problem, and no amount of password, session or login hardening
touches it, because every one of those controls is satisfied by a colleague holding a phone
that is signed in as you.

**What the punch proves today, precisely:**

| The chain proves… | …how | Does it stop buddy punching? |
|---|---|---|
| A valid session made the request | JWT; the actor is taken from the token, never from the payload | ❌ Credentials are shareable |
| *A device* was inside the fence | Browser geolocation → haversine or point-in-polygon against the zone | ❌ It proves the **device** was there, not the person |
| The punch is immutable and attributable | Raw log row, verification log row, both kept | ❌ Faithfully records the wrong person |
| Two-factor at sign-in | `TwoFactorAuthService` — TOTP, organisation-wide setting | ❌ A login-time control; the shared session is already open |
| The device is recognised | `DeviceSessionService` fingerprints browser, OS and model, and flags suspicious IP locations | ❌ Heuristic and spoofable, and it identifies a **browser**, not a person |

So the truthful statement is: **the module records attendance with an audit trail that is
better than most, and it does not currently prove who was standing there.** Finding **A-93**.

**Only one thing closes it: a biometric captured at the instant of the punch and matched against
a template the organisation enrolled.** You can lend a password. You cannot lend a fingerprint.
Everything else on the list below narrows the window; nothing else shuts it.

**The module was designed for exactly this, and stopped one component short.** Three things
already exist in the database and on screen:

- `StaffAttendanceDevices` — the reader register (chapter 24): vendor device id, type, IP, port,
  the location it stands at, last sync, pending sync count
- `EmployeeBiometrics` — the enrolment register (chapter 25): the template itself, its format,
  a quality score, the finger or body part, **who enrolled it and on which device**, and a
  revocation trail
- `StaffAttendanceLog.DeviceId`, `.DeviceSerialNumber` and `.RawData` — three columns waiting for
  a punch that came from hardware

What is missing is the half that closes the loop: **nothing polls a reader and no endpoint
accepts a device batch** (**A-21**), and **nothing matches a capture against an enrolled
template** (**A-85**).

**The two architectures that actually eliminate it:**

**① Fixed readers at the gate — the primary control, and the one to build.** The reader matches
the finger or face *on the device* and reports the employee it matched. Credentials never enter
the loop, so there is nothing to share. Building the device connector turns two existing
registers into a complete control and needs no schema change. It covers everybody who physically
arrives at a site — which at TDC is most of the organisation.

**② WebAuthn on a bound device — the secondary control, for field staff who cannot reach a
reader.** Two halves, and both are needed:

- **Device binding** — one registered phone per employee, with a key held in the phone's secure
  enclave, and a punch that must be signed by it. Lending a password stops working; a colleague
  would have to keep your phone.
- **User verification at punch time** — a WebAuthn ceremony with `userVerification: "required"`
  against the platform authenticator, run immediately before the punch, with the assertion sent
  as proof. This is the load-bearing half: it proves the **enrolled human's own face or finger**
  was present at that moment. A colleague can hold your phone; they cannot pass your Face ID.

There is no WebAuthn or passkey implementation anywhere in the solution today (**A-94**), so ②
is a build, not a switch. The existing device *fingerprinting* is not a substitute for it.

**What narrows the window but does not close it** — worth knowing so no one spends a month on
the wrong thing:

| Control | Effect |
|---|---|
| **Hard geofence enforcement** (built — a checkbox on the zone) | Narrows it to *"a colleague standing inside the fence"*. Real value, not a closure |
| Impossible-travel checks between consecutive punches | Catches the careless, not the determined |
| One device punching two employees within N minutes | Same — a detection, not a prevention |
| Mock-location detection | Closes remote spoofing, not on-site buddy punching |
| A kiosk QR code rotating every 30 seconds | Proves **proximity**, not **identity**. Only worth building alongside ② |
| 2FA at login, IP allow-lists, session limits | **No effect whatsoever** on this threat |

**The one hole neither architecture closes by itself: enrolment.** If a careless enroller lets A
register B's fingerprint under A's record, the control is defeated permanently and silently. The
register already stores `EnrolledById`, the enrolment device and a quality score, so the
remaining fix is procedural — two-person enrolment, re-verification after any revocation, and a
periodic audit of the register (which today has no expiry prompt at all — **A-87**). Liveness
detection belongs in the same paragraph: a reader without it can be defeated by a lifted print,
and that is a **procurement specification**, not code.

> **Say it like this:**
>
> *"Straight answer: today this proves a valid session and a location, and it does not prove
> which person was holding the phone. Nothing that works on passwords ever will — buddy punching
> isn't an authentication problem, it's a presence problem, and the only thing that solves it is
> a biometric taken at the moment of the punch. Which is exactly why this module already has a
> device register and a biometric enrolment register with quality scores and an enrolment audit
> trail — the readers are the answer, and the piece we haven't built yet is the connector that
> brings their punches in. For people on site that closes it completely, because they never
> touch a credential. For the field staff who can't reach a reader, the second answer is binding
> the punch to one registered phone and requiring the phone's own fingerprint or face
> immediately before it — at which point lending your password buys your colleague nothing. In
> the meantime the fence is already there, it can be switched to hard refusal today, and every
> punch is on the record with its coordinates, so the argument is at least evidenced. We'd
> rather tell you that plainly than let you find it in month three."*

That answer routinely lands better than the feature would have, because it shows the threat was
understood, the groundwork was laid, and the remaining work is named and small.

---

## 2. Before the room fills — the prep

**Time needed: 20 minutes, the evening before. Plus 5 minutes on the morning.**

Attendance needs less prep than Employees did — the demo seeders build almost everything. But
three things are time-sensitive (today's punches), one is missing (pay-period linkage) and one
must be *rehearsed* rather than prepared (the punch, which behaves differently depending on
where your laptop physically is).

### 2.1 What the demo database already holds

Two scenario modules build this module: `030-attendance.mjs` and `031-attendance-operations.mjs`.
Between them, on a freshly built `ErpSystemDB_UAT`:

| Table | What is there |
|---|---|
| `WorkSchedules` | **1** — *TDC Standard Office Hours*, Fixed, 08:00–17:00, 8 h/day, 40 h/week |
| `ShiftDefinitions` | **2** — *Day Shift (08:00–17:00)* and *Night Shift (20:00–05:00)*, the night one with a 15% differential and a night allowance |
| `EmployeeWorkSchedules` | **one per member of staff**, effective 1 January, reason *"Standard corporation office hours (Mon–Fri, 08:00–17:00)"* |
| `ShiftAssignments` | **40** — the first 40 staff on the Day Shift from a fortnight ago |
| `ShiftRotationPlans` | **1** — *Estate Security & Yard Duty Rotation*, Weekly, 7-day cycle from 5 January |
| `ShiftRotationStages` | **2** — *Week A — day watch*, *Week B — night watch* |
| `ShiftRotationMembers` | **5** — guards, drivers and yard staff (`TDC/00049`, `TDC/00050`, `TDC/00101–00103`) |
| `HolidayCalendars` | **1** — *Ghana Statutory Holidays* |
| `PublicHolidays` | **26** across this year and next — the Act 601 list, with substitute Mondays |
| `PayPeriods` | **12** — January … December of this year, monthly. Everything before last month is **Closed**; last month and this month are **Open** |
| `GeofenceZones` | **1** — *Tema Head Office — 200 m*, a circle, **soft** enforcement, linked to the `TEMA-HQ` location |
| `StaffAttendanceDevices` | **3** — *Main Gate Turnstile* (ZKTeco F18, fingerprint), *HR Block Entrance* (Hikvision face reader), *Estates Yard Gate* (ZKTeco RFID) |
| `EmployeeBiometrics` | **7** fingerprint templates, right index, quality 88, format ISO/IEC 19794-2 |
| `StaffAttendanceLogs` | **5** punches from *today*, from the five demo personas (+ any from the import) |
| `StaffDailyAttendances` | **~41** — today's five, five painted exception days, one open day, and 30 from the imported clock file |
| `StaffAttendanceRecords` | **~15** — 5 people × 3 days on the simple register |
| `StaffAttendanceRegularizations` | **1** — *Missing check-out*, raised by the new hire, **approved and applied** |
| `StaffOvertimeRequests` | **2** — one **Approved** (3 h, board-pack drawings), one **Pending** (5 h, Saturday site inspection) |
| `RemoteWorkRequests` | **1** — **Pending**, 2 days at Sakumono, equipment confirmed |
| `StaffAttendanceAlertRules` | **4** — chronic lateness ≥30, missing punch, unauthorised absence, overtime above 3 h |
| `StaffAttendanceAlerts` | a handful, fired against the 20 most recent days; **one is acknowledged** |
| `StaffBulkAttendanceImports` | **1** — `TDC-BIO-001_<last-month>_clockings.csv`, 30 rows, **Completed** |
| `StaffAttendancePayrollExports` | **0** — *deliberately excluded from the demo dataset* |

> **The four names you will say most:**
> **Kojo Ansah** (`new.hire`, Supervising Architect) — clocks in from Ashaiman, is late twice,
> forgot to clock out, raised the regularization.
> The **`staff`** persona (Project Coordinator) — raised both overtime requests and the
> remote-work request.
> **`head.dev`** (Head of Development) — the line manager who approves.
> **`hr.head`** (Head of HR & Administration) — you, for most of this book.

### 2.2 Confirm the day's punches exist — the only time-sensitive thing

Scenario 030 punches five people in **on the day the database was built**. If the database was
built yesterday, `/hr/attendance` will show *0 present today* and the tiles will look dead.

**On the morning of the demo, open `/hr/attendance` and look at the Present tile.**

- **If it reads 4 or 5** — you are fine, do nothing.
- **If it reads 0** — re-run just that scenario. It is an "ensure" step and safe:

```powershell
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
node scenarios.mjs --only 030
```

  That re-punches the five personas for today and leaves everything else alone. Takes about
  20 seconds.

- **If you cannot run the script**, fall back: sign in as `staff`, go to `/me/attendance`,
  press **Check in**. That is one person, but it makes the dashboard honest, and the punch is
  chapter 26's opening move anyway.

### 2.3 Rehearse the punch once, tonight, from the demo laptop

The geofence check compares your browser's coordinates with the *Tema Head Office — 200 m*
circle. Where you physically are decides what the room sees:

| Where the demo laptop is | The green box after Check in |
|---|---|
| Inside the Tema head-office compound | *"Location verified — Tema Head Office — 200 m"* |
| Anywhere else in Ghana or the world | *"Outside work zone (Tema Head Office — 200 m) — punch recorded with a location warning."* |
| Browser location blocked, or a desktop with no GPS | no box at all — the punch is accepted with `GPS unavailable` |

**All three are correct outcomes and the book uses each of them.** But decide which one you
will get *before* the room is watching, and write it on the line below:

> Tonight's rehearsal produced: ______________________________________________

If you get the "outside zone" message, that is the *better* demo — it proves the fence works.
Chapter 26 has the say-line for it.

⚠ **Chrome will ask permission for location the first time.** Grant it tonight, not live.

### 2.4 Check the numbers you will quote

Open these four and write the figures in. They are the ones you will say out loud.

| Screen | What to write down |
|---|---|
| `/hr/attendance` | Present ____ · Absent ____ · Late ____ · On leave ____ · Remote ____ · rate ____% of ____ active employees |
| `/hr/attendance` — *Needs attention* | Regularizations ____ · Overtime ____ · Remote work ____ · Alerts ____ · Unprocessed punches ____ |
| `/hr/attendance/daily` (last 30 days) | total records ____ |
| `/administration/hr/attendance/pay-periods` | the **current open period** is ______________ |

### 2.5 Decide who approves, and open the second window

The seeded definitions route to **Manager**, **HR** or **TenantAdmin**. `hr.head` holds HR, so
`hr.head` can approve everything in this book from one window — **including requests `hr.head`
raised**, because the definitions do not prevent initiator approval.

That is convenient but it is a weaker story. The stronger one is two windows:

- **Window A — `hr.head`** (your main window, everything else in this book)
- **Window B — `head.dev`** (the line manager, for the overtime approval in chapter 19)

Use a normal window and an incognito window so the two sessions do not fight over the token.

If you also want chapter 9's delete beat, you need a **third**: **`admin`**, which holds
`HR.Attendance.Admin`. If you would rather not juggle three, skip that one step — chapter 9
says exactly how.

### 2.6 Optional — make the payroll export report a real number (5 minutes)

Rule 4 explains why an export reports zero. If you would rather show a number than an
architecture diagram, attach last month's summaries to last month's pay period first. One
statement, run against the demo database only:

```sql
-- Attach last month's finalised/unfinalised summaries to last month's pay period,
-- so a payroll export has something to count. DEMO DATABASE ONLY.
UPDATE s
   SET s.PayPeriodId = p.Id
  FROM StaffMonthlyAttendanceSummaries s
  JOIN PayPeriods p
    ON p.TenantId = s.TenantId
   AND YEAR(p.StartDate) = s.[Year]
   AND MONTH(p.StartDate) = s.[Month]
 WHERE s.IsDeleted = 0
   AND s.[Year]  = YEAR(DATEADD(month, -1, GETDATE()))
   AND s.[Month] = MONTH(DATEADD(month, -1, GETDATE()));
```

Then close last month's period on `/administration/hr/attendance/pay-periods` (chapter 8,
LIVE WRITE 1) and the export in chapter 24 will report the real count.

⚠ Run it with `sqlcmd -I -b` or SSMS. Do **not** run it against the development database.

### 2.6b Optional — give one day something to verify (2 minutes)

Chapter 15 has two supervisor buttons — **Verify** and **Approve exception** — and on a fresh
demo database **neither will ever appear**, because nothing in the product sets the two flags
that reveal them (finding **A-43**: `RequiresVerification` and `HasException` are on the entity,
filterable in the API and absent from every create and update DTO).

Chapter 15 gives you a say-line that works without them. If you would rather press the button,
mark one day first. Pick **Kojo Ansah's missing-clock-out day** — the one you will be on screen
with anyway:

```sql
-- DEMO DATABASE ONLY. Flags one day for verification and gives it an open exception,
-- so the Verify and Approve-exception buttons render on its detail page.
UPDATE d
   SET d.RequiresVerification = 1,
       d.HasException         = 1,
       d.ExceptionReason      = N'Clocked in at the main gate; no out-punch recorded.'
  FROM StaffDailyAttendances d
  JOIN Employees e ON e.Id = d.EmployeeId
 WHERE d.IsDeleted = 0
   AND e.EmployeeNumber = N'<Kojo Ansah''s staff number>'
   AND d.ActualCheckInTime IS NOT NULL
   AND d.ActualCheckOutTime IS NULL;
```

Get the staff number from `/hr/attendance/daily` — it is under his name on the row. Run it with
`sqlcmd -I -b` or SSMS.

*Undo:* set both columns back to `0` and `ExceptionReason` to `NULL`. Chapter 28, row 6.

### 2.7 Pre-open every screen

Attendance has thirty-six screens and you will not have time to navigate to all of them. Open
these **eleven tabs** in this order before the room fills, and drive the demo by moving left to
right:

| Tab | Route | Used in |
|---|---|---|
| 1 | `/hr/attendance` | ch. 3 |
| 2 | `/administration/hr/attendance` | ch. 4–12 |
| 3 | `/hr/attendance/daily` | ch. 13, 15 |
| 4 | `/hr/attendance/logs` | ch. 17 |
| 5 | `/hr/attendance/regularizations` | ch. 18 |
| 6 | `/hr/attendance/overtime` | ch. 19 |
| 7 | `/hr/attendance/remote-work` | ch. 20 |
| 8 | `/hr/attendance/summaries` | ch. 21 |
| 9 | `/hr/attendance/alerts` | ch. 22 |
| 10 | `/hr/attendance/imports` | ch. 23 |
| 11 | `/me/attendance` *(second window, as `staff`)* | ch. 26 |

### 2.8 Prep checklist

- [ ] `/hr/attendance` shows a non-zero **Present** tile *(§ 2.2)*
- [ ] The punch was rehearsed once from this laptop, and the outcome is written down *(§ 2.3)*
- [ ] Browser location permission already granted for the app's origin
- [ ] The five numbers from § 2.4 are written down
- [ ] Window B open and signed in as `head.dev` *(§ 2.5)*
- [ ] Window C open as `admin` — **only if** you are doing chapter 9's delete beat
- [ ] *(optional)* § 2.6 SQL run and last month's period closed
- [ ] *(optional)* § 2.6b SQL run, if you want chapter 15's **Verify** button to exist
- [ ] Eleven tabs open in the order above *(§ 2.7)*
- [ ] You have read **Rules 1–5** and know which four buttons not to press

---

## 3. `/hr/attendance` — the group landing

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → **Attendance & Time** (the group header itself is
the link) · `/hr/attendance` · as **hr.head** · **4 minutes**

### 📖 What it is

> *"This is the attendance desk's morning. Everything an HR officer needs to know before nine
> o'clock is on one screen: who is in, who is not, who was late, what is waiting for a decision,
> and whether the machines on the gates are talking to us. It is a single request to the server,
> not eleven — and every tile is a link into the screen that will let you do something about it."*

### 👁 On the page

**Header**

| Control | What it is |
|---|---|
| Title | *Attendance & Time* |
| Subtitle | *"Today, 17 Sep 2026 — attendance, approvals and the hand-off to payroll."* — today's real date |
| Spinner (top right) | appears only while a background refetch is in flight |

**Row 1 — "Today · NN% attendance of NN active employees"** — five clickable tiles:

| Tile | Counts | Links to |
|---|---|---|
| **Present** | today's rows at status *Present* or *On duty* | `/hr/attendance/daily` |
| **Absent** *(red)* | today's rows at status *Absent* | `/hr/attendance/daily` |
| **Late** *(amber)* | today's rows with `IsLate` | `/hr/attendance/daily` |
| **On leave** | today's rows at status *On leave* | `/hr/leave/requests` |
| **Remote** | today's rows with `IsRemoteWork` | `/hr/attendance/remote-work` |

The heading's percentage is the § 1.4 ② formula; the employee count is every **active** employee
in the tenant.

**Row 2 — "Needs attention"** — five more tiles, all backlogs:

| Tile | Counts | Links to |
|---|---|---|
| Regularizations awaiting approval | regularizations at *Pending* | `/hr/attendance/regularizations` |
| Overtime awaiting approval | overtime requests at *Pending* | `/hr/attendance/overtime` |
| Remote work awaiting approval | remote-work requests at *Pending* | `/hr/attendance/remote-work` |
| Unacknowledged alerts | active **Critical + Warning** alerts (red if any are Critical) | `/hr/attendance/alerts` |
| Unprocessed punches | logs with `IsProcessed = false` | `/hr/attendance/logs` |

> ⚠ The alert tile deliberately leaves out **Info**-severity alerts. If the count looks lower
> than the Alerts screen's list, that is why.

**Row 3 — two cards side by side:**

- **Attendance, last 7 days** — a bar per day, drawn in plain CSS rather than a charting
  library. Each bar's height is that day's total record count against the busiest day; the
  **blue** portion is present, the **red** portion absent. Above each bar, the day's attendance
  rate; below it, the weekday abbreviation. Hovering gives
  *"04 Sep 2026 — 12 present, 1 absent, 2 on leave"*. Days with no records at all are drawn as
  a 2-pixel stub rather than left out, so the series has no gaps.
- **Current pay period** — the period covering today (preferring an **Open** one if two
  overlap), its status badge, its dates, the period's total overtime hours and approved
  overtime request count, the count of active devices and how many are awaiting sync, and a
  **View summaries** link. If no period covers today the card instead reads *"No pay period is
  open"* with a **Create one** link into Administration.

**Row 4 — two more cards:**

- **Top open alerts** — the five most severe, then most recent, active alerts: employee,
  trigger, a severity badge (Critical = red, Warning = grey, Info = outline) and when it fired.
  **All alerts** links out. Empty state: *"No open alerts — nothing needs acknowledging."*
- **Chronic absentees, last 30 days** — employees with **at least one** absence, ranked by
  absences then lateness, top five. Columns: employee (name over staff number), department,
  absent (red), late, attendance %. Employees with a clean record are excluded outright.

**Row 5 — "All screens"** — eleven navigation cards, one per menu item, each with a one-line
description. This is the fastest way to reach any screen in the module.

### ▶ Walk it

**1 — Open `/hr/attendance`.** Let the tiles land before you speak.

> *"Every attendance system in the world can tell you who clocked in. The question is what you
> do at nine o'clock on a Monday. This screen answers that in two rows: the first is the state
> of the organisation today, the second is the queue of things waiting for a human being."*

**2 — Read the heading percentage aloud**, then point at **Late**.

> *"Ninety-two percent attendance of a hundred and forty active staff. Note that people who
> came in late are counted as present — they were at work. Punctuality is measured separately,
> and it is in the second figure I will show you in a moment. Counting lateness twice would put
> a perfectly normal Monday at zero."*

**3 — Point at *On leave* and say the one thing a finance director listens for.**

> *"Approved leave sits in the denominator of this figure by default — it was a working day the
> person did not attend, and we show the reason right beside it rather than hiding it in the
> numerator. That default is a company setting; if TDC decides leave should be excluded, it
> changes in one place and every figure on this page follows, including the risk list."*

**4 — Move to *Needs attention* and count the backlog aloud.**

> *"Three regularisations, one overtime request, one remote-work request and four alerts. Each
> of those tiles is a link — the officer clicks the number, lands in the queue and works it."*

**5 — Click the *Unprocessed punches* tile** and let the Punch Logs screen open; then come
straight back.

> *"That one is the most operationally useful. A punch that has not been processed means a
> device synced late, or a card that we could not match to a person. In an attendance system
> those are the failures that cost you money, because the day never gets built and payroll
> never sees it."*

**6 — Hover one bar on the 7-day trend.**

> *"Seven days, and the same definition of attendance as the headline — the code computes it in
> three places and reads the policy setting once, so the headline, the trend and the risk list
> can never disagree with one another."*

**7 — Read the *Current pay period* card.**

> *"And this is the frame everything below is measured against: the pay period. September is
> open. When HR closes it, the month's attendance is locked and payroll can take it."*

**8 — Finish on *Chronic absentees*.**

> *"Last thirty days, ranked by absence then lateness, and deliberately only people who actually
> have absences — a risk list with clean records in it is noise. This is the list a Head of HR
> takes into a supervisors' meeting."*

### ⚙ Behind the page

| Element | Endpoint | Service | Reads |
|---|---|---|---|
| Everything on the page | `GET api/attendance-dashboard?asOf&trendDays=7&riskListSize=5` | `AttendanceDashboardService` | `StaffDailyAttendances`, `StaffAttendanceLogs`, `StaffAttendanceRegularizations`, `StaffOvertimeRequests`, `RemoteWorkRequests`, `StaffAttendanceAlerts`, `StaffAttendanceDevices`, `PayPeriods`, `Employees`, `CompanyHrPolicySettings` |

Gated on `HR.Attendance.Read`. `trendDays` is clamped 1–90 and `riskListSize` 1–50 — a silly
query string cannot fail the page. Every count is a grouped aggregate; no entity is materialised
just to be counted, and the chronic-absentee names come from one follow-up projection rather
than a join that would drag whole employee rows through the grouping.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-7 · The dashboard has no date control.** `asOf` is a supported query parameter and the page always sends today. There is no way to look at last Tuesday from this screen | |
| **A-8 · The trend length is fixed at 7.** `trendDays` accepts 1–90; the page hard-codes 7 | |

---

## 4. `/administration/hr/attendance` — the setup hub

### 📍 Where you are

**Sidebar:** Administration → HR → **Time, Attendance & Leave** ·
`/administration/hr/attendance` · as **hr.head** · **2 minutes**

### 📖 What it is

> *"Everything you have just seen is measured against rules, and the rules live here. Nine
> cards: what a working day is, what a shift is, which days are holidays, what a pay period is,
> where a phone may clock in from, which machines are on the gates, what should raise a flag,
> who may claim overtime — and leave types, which sit here for a reason."*

### 👁 On the page

A page header (*Time, Attendance & Leave Setup*, with the group description *"What counts as a
working day, a shift, an overtime hour and a leave entitlement."* and a back-link to the HR
Administration hub) above **nine navigation cards**:

| Card | Route | Chapter |
|---|---|---|
| **Work Schedules** — *The working patterns employees are assigned to.* | `…/work-schedules` | 5 |
| **Shift Rotations** — *Repeating shift cycles and the order crews move through them.* | `…/shift-rotations` | 6 |
| **Holiday Calendars** — *Public and company holidays, per calendar.* | `…/holiday-calendars` | 7 |
| **Pay Periods** — *The periods attendance is totalled and exported against.* | `…/pay-periods` | 8 |
| **Geofence Zones** — *The map areas a clock-in must fall inside to be accepted.* | `…/geofence-zones` | 9 |
| **Devices** — *Biometric and terminal devices, and the site each is registered to.* | `…/devices` | 10 |
| **Alert Rules** — *What an absence, a late arrival or a missed punch should raise.* | `…/alert-rules` | 11 |
| **Overtime Policies** — *When overtime is earned, at what multiplier, and up to what ceiling.* | `…/overtime-policies` | 12 |
| **Leave Types** — *Leave types with their sub-types, allocations and accrual policies.* | `/administration/hr/leave-types` | *(leave guide, ch. 4)* |

### ▶ Walk it

**1 — Open the hub and read the nine card titles down the page.** Do not click yet.

> *"Nine questions, and between them they define time at TDC. Notice the last one — leave types
> sits in the attendance setup rather than in its own corner. That is deliberate: a work
> schedule and a leave type answer exactly the same question from opposite ends — is this person
> expected at work today, yes or no."*

**2 — Say the sentence that explains why this is a separate menu.**

> *"Everything under Administration is set once and changed rarely. Everything under Human
> Resources is worked daily. An HR officer lives on the other side of the product; a systems
> administrator lives here. Same permissions tier, different rhythm."*

**3 — Click *Work Schedules*** and continue into chapter 5.

### ⚙ Behind the page

No API call at all — the cards come from `frontend/src/config/hr-setup-nav.ts`, the same file
that builds every other HR setup hub. Visibility of the *Administration* menu itself is a role
concern, not an attendance permission; each card's target enforces its own.

---

## 5. `/administration/hr/attendance/work-schedules` — what a working day is

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Work Schedules** ·
`/administration/hr/attendance/work-schedules` · as **hr.head** · **8 minutes**

### 📖 What it is

> *"A work schedule is the corporation's written answer to 'what is a day's work'. Start and end
> times, which days of the week, how long the break is and whether it is paid, whether overtime
> is allowed and up to what ceiling, and how many minutes late is actually late. One schedule
> can carry several shifts underneath it — the same 40-hour week, worked days or nights."*

### 👁 Screen 1 — the register

**Header:** title *Work Schedules*, description *"Standard hours, working days, breaks and
overtime rules that attendance is measured against."*, back-link to the setup hub, and a
**+ New Schedule** button.

**Table** — one card, eight columns:

| Column | Shows |
|---|---|
| **Name** | schedule name, plus an outline **Default** badge if it is the tenant default |
| **Type** | Fixed · Flexible · Shift · Compressed · Part time |
| **Hours** | `08:00 – 17:00` |
| **Per day** | right-aligned decimal |
| **Per week** | right-aligned decimal |
| **Shifts** | how many shift definitions hang off it |
| **Status** | Active / Inactive badge |
| ⋯ | row menu |

Clicking **anywhere on a row** opens the editor. The ⋯ menu holds **Edit** and 🚫 **Delete**.

Empty state: *"No work schedules yet — create a schedule before assigning employees or
recording attendance."* with a **+ New Schedule** button.

### 👁 Screen 2 — new / edit (`…/new`, `…/[id]/edit`)

The same form component in both places, **six cards down the page**. On the edit page it sits
under a **Settings** tab, with a **Shifts** tab beside it.

**Card 1 — Schedule**
- **Schedule name** *(required, ≤150)*
- **Type** *(required)* — Fixed · Flexible · Shift · Compressed · Part time
- **Description** *(≤1000)*
- **Default schedule** switch — *"Used for employees with no explicit assignment."*
- **Active** switch

**Card 2 — Standard hours**
- **Start time** *(required)* · **End time** *(required)*
- **Hours per day** *(required, 0–24, step 0.25)* · **Hours per week** *(required, 0–168)*

**Card 3 — Working days** — seven switch chips, Mon…Sun. **At least one must be on** —
the form refuses to save otherwise, with *"Select at least one working day"*.

**Card 4 — Flexible & core hours** — three switches, each revealing a pair of time fields when
turned on:
- **Flexible start** — *"Employees may clock in within a window."* → Earliest start / Latest start
- **Flexible end** → Earliest end / Latest end
- **Core hours** — *"A window everyone must be present for, regardless of flexi-time."* → Core
  hours start / Core hours end

**Card 5 — Breaks**
- **Mandatory break** switch → **Break length (minutes)** *(0–480; must be > 0 if the switch is
  on)* and **Paid break** switch — *"Counts toward worked hours."*

**Card 6 — Overtime & grace periods**
- **Allows overtime** switch → **Requires pre-approval** switch, **Max overtime / day**,
  **Max overtime / week**
- **Late grace (minutes)** *(0–480)* · **Early departure grace (minutes)** *(0–480)*

**Footer:** **Cancel** · **Create schedule** / **Save changes**.

Blank optional times are sent as `null`, not `""`. Break length is nulled when the break switch
is off; the overtime ceilings are nulled when overtime is off.

### 👁 Screen 3 — the Shifts tab (edit page only)

A table with **+ Add shift** above it. Columns: **Shift**, **Type**, **Hours** (`08:00 – 17:00`),
**Length** (`8h`), **Night** (Yes/No), **Status**. Row ⋯ menu: **Edit**, 🚫 **Remove shift**.

Dialog hint: *"Shifts let one schedule cover several working patterns, e.g. a day and a night
rota."* The dialog carries:

- **Shift name** *(required, ≤100)* · **Type** *(Morning · Afternoon · Evening · Night ·
  Rotating · Split)*
- **Description** *(≤500)*
- **Start time** *(required)* · **End time** *(required)*
- **Shift length (hours)** *(required, 0.5–24)* · **Display order**
- **Night shift** switch — *"Crosses midnight or falls in night hours."*
- **Attracts night allowance** switch
- **Allows overtime** switch
- **Has shift differential** switch → **Differential (%)** *(0–100, step 0.01)*
- **Active** switch

> ⚠ **The shift list returns summaries, not full records.** Opening an existing shift for edit
> repopulates only name, type, times, length, night flag, display order and active — the four
> allowance switches and the description fall back to their defaults. Pressing **Save changes**
> then writes those defaults over whatever was there. This is finding **A-9**; treat the shift
> dialog as create-only during a demo.

### ▶ Walk it

**1 — Open the register.** One row: *TDC Standard Office Hours*, Fixed, 08:00–17:00, 8 per day,
40 per week, 2 shifts, Active.

> *"One schedule, and it is the corporation's standard: eight to five, Monday to Friday, forty
> hours. Every member of staff is assigned to it — and 'assigned' is a dated record, so when TDC
> changes its hours we do not overwrite history, we start a new assignment from a date."*

**2 — Click the row** to open the editor. Stay on **Settings**.

**3 — Walk down the six cards without touching anything.** Name the ones that matter:

> *"Standard hours. Working days — seven switches, and at least one has to be on, because a
> schedule nobody works would make every day an off-day for everybody assigned to it. Flexible
> and core hours, for the professional grades. Breaks, with the question payroll always asks:
> is it paid. Overtime, with a ceiling per day and per week. And the grace periods — how many
> minutes late is late."*

**4 — ⚠ The honest sentence.** Say it here, once, and you will not have to defend it later:

> *"I want to be straight about something, because it is the right question to ask. Today these
> rules are captured and reported — they are not yet enforced automatically. Lateness in this
> system is recorded by a person, not inferred by the clock. For a corporation moving off paper
> that is the right first step: you get the register, the corrections, the approvals and the
> audit trail working, and everybody agrees the times are right. Then you switch on automatic
> lateness against these grace periods — and because the rules are already here, that is a
> configuration change, not a build."*

**5 — Switch to the *Shifts* tab.** Two rows: *Day Shift (08:00–17:00)* and
*Night Shift (20:00–05:00)*, the second flagged Night.

**6 — Click ⋯ → Edit on *Night Shift*** — but **do not save**. Point at the switches.

> *"Here is where money enters attendance. This shift crosses midnight, it attracts a night
> allowance, and it carries a fifteen percent shift differential. Those three flags are what an
> Estates guard's pay hangs on, and they are set once on the shift rather than once per person."*

Press **Cancel**. *(See A-9 — do not press Save.)*

**7 — 🚫 DO NOT PRESS ⋯ → Delete, on either the schedule or a shift.** Both are
`HR.Attendance.Admin` and `hr.head` is refused. If someone asks why the button is there:

> *"Deleting a work schedule is a different act from editing one — every employee assigned to it
> loses their schedule. That is an administrator's decision, and the server enforces it. What
> you are seeing is that the button has not yet been hidden from the people who cannot use it —
> a small tidy-up rather than a hole."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/work-schedules` | signed-in *(no attendance permission required to read)* |
| Editor load | `GET api/work-schedules/{id}` | signed-in |
| Create | `POST api/work-schedules` | `HR.Attendance.Write` |
| Update | `PUT api/work-schedules/{id}` | `HR.Attendance.Write` |
| Delete | `DELETE api/work-schedules/{id}` | **`HR.Attendance.Admin`** |
| Shifts tab list | `GET api/work-schedules/{id}/shifts` | signed-in |
| Add shift | `POST api/work-schedules/{id}/shifts` | `HR.Attendance.Write` |
| Edit shift | `PUT api/work-schedules/shifts/{shiftId}` | `HR.Attendance.Write` |
| Remove shift | `DELETE api/work-schedules/shifts/{shiftId}` | **`HR.Attendance.Admin`** |

Tables: `WorkSchedules`, `ShiftDefinitions`. The daily-attendance filter's *Work schedule*
dropdown reads `GET api/work-schedules/active`, and the *Record a day* form reads
`GET api/work-schedules/default` to suggest one.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-6 · There is no screen anywhere that assigns a work schedule or a shift to an employee.** `EmployeeWorkSchedules` and `ShiftAssignments` have full controllers, full services and typed frontend clients — and no page imports either client. The demo data was created through the API. An HR officer cannot move somebody onto the night rota without a developer, unless they are enrolled in a rotation **plan** (chapter 6), which is the only screen that touches per-person shift data | |
| **A-9 · The shift edit dialog silently blanks four fields** — see the box above | |
| **A-10 · The register is read by anyone signed in.** `WorkSchedulesController` puts `HR.Attendance.Write` on its writes but leaves every read at `InternalOnly`, unlike every other controller in the module. Not a leak of anything sensitive, but it is inconsistent | |

---

## 6. `/administration/hr/attendance/shift-rotations` — the rota

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Shift Rotations** ·
`/administration/hr/attendance/shift-rotations` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Security, drivers and the yard do not work a fixed shift; they rotate. A rotation plan is
> the pattern — a week of days, then a week of nights — plus the list of who rides it and which
> position in the cycle each person is currently on. Set it once in January and it describes the
> whole year."*

### 👁 Screen 1 — the register

**+ Add plan** above a table:

| Column | Shows |
|---|---|
| **Plan** | plan name |
| **Cycle** | Weekly · Fortnightly · Monthly · Quarterly |
| **Length** | `7 d` |
| **Starts** / **Ends** | dates (Ends shows an em dash when open-ended) |
| **Stages** | count |
| **Members** | count |
| **Status** | Active / Inactive |
| *(last)* | an **Open** link |
| ⋯ | **Stages & members**, **Edit**, 🚫 **Delete** |

**Add / Edit dialog** — hint *"Add the stages and members on the plan's own page once it
exists."*:
- **Plan name** *(required, ≤150)* · **Description**
- **Rotation cycle** *(required)* · **Cycle length (days)** *(required, 1–365)*
- **Start date** *(required)* · **End date** *(must not precede the start)*
- **Active** switch · **Notes**

> ⚠ **Cycle, cycle length and start date are fixed after creation.** The update DTO does not
> accept them, so the dialog shows them when editing and quietly drops them on save. This is
> correct, not a bug: members carry a *current stage* that is only meaningful relative to the
> original sequence, so changing the rhythm mid-flight would silently move everybody.

### 👁 Screen 2 — the plan (`…/shift-rotations/[id]`)

Header: plan name, subtitle *"Weekly rotation · 7-day cycle from 05 Jan 2026"*, an Active badge,
back-link. **Two tabs.**

**Tab 1 — Stages.** *"Stages run in order; each occupies its shift for the given number of
cycles."* Columns: **#** (stage order), **Shift**, **Hours**, **Cycles**, **Label**.
Dialog: **Shift** *(a dropdown of every active shift definition, labelled
`Day Shift (08:00–17:00)`)*, **Order** *(1–100)*, **Duration (cycles)** *(1–12)*, **Label**
*("e.g. Days, Nights")*.

**Tab 2 — Members.** *"Members advance one stage each cycle from the point they join."*
Columns: **Member** (employee, or organisation unit, or team), **Employee no.**, **Stage**,
**Joined**, **Exited**. Dialog: **Employee** *(searchable picker, required)*, **Join date**
*(required)*, **Current stage**, **Notes**.

> ⚠ **The create and update payloads disagree, on both tabs, and the screen reconciles it.**
> A stage is *created* with `durationCycles` + `label` but *updated* with `durationDays` +
> `notes` — the screen multiplies cycles by the plan's cycle length on the way out. A member is
> *added* with a join date but *updated* with only a current stage and notes — so the employee
> and the join date are effectively immutable after enrolment. Both are handled; neither is
> visible to you. Worth knowing if a number looks odd after an edit.

### ▶ Walk it

**1 — Open the register.** One row: *Estate Security & Yard Duty Rotation*, Weekly, 7 d, from
05 Jan, 2 stages, 5 members, Active.

> *"One rotation, and it covers the people who keep the estate running out of hours — guards,
> drivers and the yard. Weekly cycle. Two stages, five people on it."*

**2 — Click *Open*** (or ⋯ → **Stages & members**).

**3 — On the *Stages* tab**, read the two rows.

> *"Stage one, Week A, day watch — eight to five. Stage two, Week B, night watch — eight in the
> evening to five in the morning, and that is the shift carrying the fifteen percent
> differential we saw a moment ago. Each stage lasts one cycle, so a guard alternates week by
> week."*

**4 — Switch to *Members*.** Five rows, each with a staff number, a current stage and a join
date of 5 January.

> *"And here is who rides it, with the stage each of them is on right now. A member can be an
> individual, a whole organisation unit or a team — the model supports all three, so you can put
> 'the Estates yard' on a rota rather than five names."*

**5 — Click ⋯ → Edit on a member** to show the dialog, then **Cancel**.

> *"Only two things move after somebody is enrolled: which stage they are on, and the note
> against them. The employee and the join date are fixed, because a rotation is a sequence and
> you cannot re-date somebody into the middle of it without moving everyone else."*

**6 — 🚫 DO NOT PRESS Delete** on the plan, a stage or a member — all three are Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/shift-rotation-plans` | signed-in |
| Plan | `GET api/shift-rotation-plans/{id}` | signed-in |
| Create / update plan | `POST` / `PUT api/shift-rotation-plans[/{id}]` | `HR.Attendance.Write` |
| Stages | `GET .../{id}/stages/list` · `POST .../{id}/stages` · `PUT .../stages/{stageId}` | read: signed-in · write: `HR.Attendance.Write` |
| Members | `GET .../{id}/members/list` · `POST .../{id}/members` · `PUT .../members/{memberId}` | read: `HR.Attendance.Read` · write: `HR.Attendance.Write` |
| Shift dropdown | `GET api/shift-definitions/active` | signed-in |
| Every delete | `DELETE …` | **`HR.Attendance.Admin`** |

Tables: `ShiftRotationPlans`, `ShiftRotationStages`, `ShiftRotationMembers`, `ShiftDefinitions`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-11 · A rotation plan does not generate anything.** Stages and members are recorded, and `CurrentStageOrder` is a number a human edits. No job advances members through the cycle, and no `ShiftAssignment` is created from a plan. The rota describes the intention; the attendance rows do not know about it | |
| **A-12 · The member dialog only offers an employee.** The entity supports an organisation unit or a team, the API accepts both, and the screen always sends `null` for them. The columns render the names, so a unit- or team-level member created through the API displays correctly — it just cannot be created here | |

---

## 7. `/administration/hr/attendance/holiday-calendars` — which days are not working days

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Holiday Calendars** ·
`/administration/hr/attendance/holiday-calendars` · as **hr.head** · **5 minutes**

### 📖 What it is

> *"A holiday calendar decides which days are not working days. Attendance reads it, and so does
> leave — when the system counts how many days a leave request costs, this is the list it takes
> the holidays out against. One calendar per country or region, one of them marked as the
> default."*

### 👁 Screen 1 — the register

**+ Add calendar** above a table: **Calendar** (name, plus a **Default** badge), **Country**,
**Region**, **Holidays** (count), **Status**, a **Holidays** link, and a ⋯ menu with
**Manage holidays**, **Edit**, 🚫 **Delete**.

**Dialog** — hint *"Mark one calendar as default for employees with no specific assignment."*:
**Calendar name** *(required)* · **Description** · **Country** *(a dropdown of every country in
the reference data, clearable)* · **Region** · **Default calendar** switch · **Active** switch.

### 👁 Screen 2 — inside a calendar (`…/holiday-calendars/[id]`)

Header: calendar name, subtitle `Ghana` (country · region), an Active badge, and — top right —
a **year dropdown** offering next year, this year, and the two before.

**+ Add holiday** above a table filtered to the chosen year: **Holiday**, **From**, **To**,
**Observance**, **Holiday pay** (Yes/No), **Status**, ⋯ (**Edit**, 🚫 **Delete**).

**Dialog** — hint *"Recurring holidays repeat on the same dates each year; moveable feasts need
an entry per year."*:
- **Holiday name** *(required, ≤200)* · **Description**
- **From** *(required)* · **To** *(required, must not precede From)*
- **Observance** *(Mandatory · Optional · Substitute day)* · **Substitute day** *(a date)*
- **Attracts holiday pay** switch — *"Employees who work the day are paid at the multiplier
  below."* → **Pay multiplier** *(0.1–10, step 0.1, "e.g. 2 for double time")*
- **Recurs annually** switch — *"Turn off for moveable feasts that need entering each year."*
- **Active** switch

Two design points worth naming: a holiday is a **date range**, not a single day, so a two-day
observance is one row; and the **substitute day** is a field on the holiday rather than a second
row, so a holiday falling on a Sunday and observed on the Monday stays one record.

### ▶ Walk it

**1 — Open the register.** One row: *Ghana Statutory Holidays*, Ghana, **26** holidays, Default,
Active.

> *"One calendar, and it is the list gazetted under the Public Holidays Act — twenty-six entries
> across this year and next. It is marked default, so every employee without a more specific
> calendar gets it."*

**2 — Click *Holidays*** to open the calendar.

**3 — Read down the list.** New Year, Constitution Day, Independence Day, Good Friday, Easter
Monday, May Day, the two Eids, Founders' Day, Kwame Nkrumah Memorial Day, Farmers' Day,
Christmas, Boxing Day.

> *"Notice two things. The Eids are entered per year rather than marked as recurring, because
> they move with the lunar calendar — the form has a switch for exactly that. And where a
> holiday falls at a weekend, the substitute Monday is a field on the same record, not a second
> entry, so the calendar reads the way the gazette reads."*

**4 — Click ⋯ → Edit on Christmas Day**, point at **Attracts holiday pay** and the multiplier,
then **Cancel**.

> *"And here is the one the Estates guards care about. Christmas is a holiday — but the gate
> still has to be manned. Somebody works it, and this field says what that hour is worth.
> Holiday pay, at the multiplier the corporation sets."*

**5 — Change the year dropdown to next year** and let the list refetch.

> *"Next year is already loaded, so nobody is entering holidays in a hurry on the 31st of
> December."*

**6 — 🚫 DO NOT PRESS Delete** on a calendar or a holiday — Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/holiday-calendars` | signed-in |
| Calendar | `GET api/holiday-calendars/{id}` | signed-in |
| Create / update calendar | `POST` / `PUT api/holiday-calendars[/{id}]` | `HR.Attendance.Write` |
| Holidays for a year | `GET api/holiday-calendars/{id}/holidays?year=` | signed-in |
| Add / edit holiday | `POST .../{id}/holidays` · `PUT .../{id}/holidays/{holidayId}` | `HR.Attendance.Write` |
| Delete either | `DELETE …` | **`HR.Attendance.Admin`** |
| Country dropdown | `GET api/hr/countries` | reference data |

Tables: `HolidayCalendars`, `PublicHolidays`. The controller validates that `DateTo >= DateFrom`
and that a holiday belongs to the calendar in the URL before it will show, update or delete it.

Two other modules read this data through the same controller's global lookups:
`GET api/holiday-calendars/holidays/by-year/{year}` and `…/holidays/range?from&to` are what the
**leave** module uses to take holidays out of a leave day-count.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-13 · Nothing assigns a calendar to an employee.** `HolidayCalendar.IsDefault` exists and there is a `GET /default`, but no employee, department or location field points at a calendar. A second calendar for a second country would be created, displayed — and read by nobody | |
| **A-14 · `IsRecurringAnnually` generates nothing.** The flag is stored and there is a `GET …/holidays/recurring` read, but no job rolls a recurring holiday forward into next year. The demo data has two years because the seeder wrote two years | |

---

## 8. `/administration/hr/attendance/pay-periods` — the cut-off

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Pay Periods** ·
`/administration/hr/attendance/pay-periods` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"A pay period is the window attendance is totalled against, and the thing HR closes when the
> month's numbers are final. It is the handshake between attendance and payroll: while it is
> open, HR is still working; once it is closed, payroll can take the month."*

### 👁 On the page

**Card 1 — Current open period.** The period covering today: its name, its dates, a status
badge and *"N summaries · N exports"*. If none is open: *"No period is currently open. Create one
before recording attendance for a new cycle."*

**Card 2 — the register.** **+ Add pay period** above a table:

| Column | Shows |
|---|---|
| **Period** | e.g. *September 2026* |
| **Type** | Weekly · Bi-weekly · Semi-monthly · Monthly |
| **From** / **To** | the window |
| **Closed** | the date it was closed, or an em dash |
| **Exported** | the date it was exported, or an em dash |
| **Status** | Open · Pending close · Closed · Exported to payroll |

Row ⋯ menu: **Close period** *(shown only while the status is Open or Pending close)*, **Edit**,
🚫 **Delete**.

**Add / Edit dialog** — hint *"Periods should not overlap — attendance is matched to exactly
one."*: **Period name** *(required, "e.g. August 2026")* · **Type** *(required)* · **Start date**
*(required)* · **End date** *(required, must not precede the start)* · **Notes**.

**Close confirmation:** *"Close this pay period? Locks the period so attendance for it can no
longer be edited, and makes it available to payroll export."*

> ⚠ **The first half of that sentence is not true today.** Closing a period sets its status, its
> closed date and who closed it. Nothing in the daily-attendance service checks a pay period's
> status, so a day inside a closed period can still be created, edited, verified and
> regularised. The **real** lock in this module is `IsFinalized` on the monthly summary
> (chapter 21), which the API genuinely enforces. This is finding **A-15** — say "the summary is
> the lock", not "the period is the lock".

### ▶ Walk it

**1 — Open the screen** and read the *Current open period* card.

> *"September is open. Twelve periods, one per month, created for the whole year in advance —
> so nobody is inventing a cut-off date on the 26th."*

**2 — Scan the register.** Everything before last month is **Closed**; last month and this month
are **Open**.

> *"Look at the status column. Eight closed, two open. Closed means HR has signed the month off.
> The date it was closed and the person who closed it are both on the record."*

**3 — Click ⋯ on last month's period** and point at **Close period**, without pressing yet.

**4 — 🔴 LIVE WRITE 1 — press *Close period* and confirm.** The row moves to **Closed** and the
**Closed** column fills with today's date.

> *"That is the month handed over. From this point the payroll export screen will accept it —
> and only closed periods can be exported, so nobody can ship a month that HR is still working
> on."*

⚠ **If you plan to run the export in chapter 24, this step is required** — the export refuses
anything that is not Closed. *Undo:* chapter 28 has the one-line SQL; there is no reopen button.

**5 — Say the honest sentence about what closing does and does not do.**

> *"I will be precise about this, because it matters to payroll. Closing marks the period and
> gates the export. The hard lock — the one that stops a figure changing after payroll has read
> it — sits one level down, on the monthly summary per employee, and I will show you the API
> refusing to move it in a few minutes. Period-level editing locks are the next increment."*

**6 — 🚫 DO NOT PRESS Delete.** Admin — and in any case the service refuses to delete a closed
period.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Current open period | `GET api/pay-periods/current` | signed-in |
| Register | `GET api/pay-periods/paged` | signed-in |
| Create | `POST api/pay-periods` | `HR.Attendance.Write` |
| Update | `PUT api/pay-periods/{id}` | `HR.Attendance.Write` |
| **Close** | `POST api/pay-periods/{id}/close` | `HR.Attendance.Write` |
| Delete | `DELETE api/pay-periods/{id}` | **`HR.Attendance.Admin`** |

Table: `PayPeriods`. **Create refuses an overlap** — if an existing period already covers the
new start date the service answers *"An existing pay period already covers 2026-09-01."*
**Update refuses a closed period**, and so does **delete**. The **type** is fixed after creation
(it is not on the update DTO), which the screen handles by omitting it.

`ClosedById` is the closer's **employee** id from the token, so the audit trail points at a
person on the payroll rather than a login.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-15 · Closing a period locks nothing** — see the box above | |
| **A-16 · `PendingClose` is unreachable.** The status exists, the filter offers it and the close action is shown for it — but no code path ever sets it. A period goes Open → Closed in one step | |
| **A-17 · `ExportedToPayroll` is unreachable too.** Running an export writes an export row but never touches the period, so the **Exported** column on this screen stays blank for ever and the period never leaves *Closed*. Related to A-4 | |

---

## 9. `/administration/hr/attendance/geofence-zones` — where a punch may come from

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Geofence Zones** ·
`/administration/hr/attendance/geofence-zones` · as **hr.head** · **7 minutes**
*(one optional step needs* **admin**)*

### 📖 What it is

> *"If people clock in from their phones, the obvious question is 'from where'. A geofence zone
> is an area drawn on a map — a circle round the compound, or a polygon following the boundary
> — and every punch made from a phone is checked against it. Soft enforcement records a
> violation and lets the punch through; hard enforcement refuses it outright."*

### 👁 On the page

**+ Add zone** above a table:

| Column | Shows |
|---|---|
| **Zone** | zone name |
| **Shape** | Circle · Polygon |
| **Centre** | `5.66980, -0.01660`, or an em dash for a polygon |
| **Extent** | `200 m radius`, or `7 corners · ~340 m across` |
| **Enforcement** | **Hard** · **Soft** · **None** |
| **Status** | Active / Inactive |

Row ⋯ menu: **Edit**, 🚫 **Delete**.

**Add / Edit dialog** — wide (880 px), hint *"Soft enforcement records a violation; hard
enforcement blocks the punch outright."*:

- **Zone name** *(required, ≤150)* · **Shape** *(required — Circle or Polygon)*
- **Description**
- **When Shape = Circle:** **Centre latitude** *(−90…90, step 0.000001, required)*,
  **Centre longitude** *(−180…180, required)*, **Radius (metres)** *(1–100 000, required)*, then
  a **300 px live map** — click to place the centre, drag the pin to adjust, drag the edge
  handle to resize the radius, or press **Use my position** to drop it where you are standing.
- **When Shape = Polygon:** a **340 px map** — click each corner in turn, drag any corner to
  adjust — and below it a **Polygon coordinates (JSON)** textarea showing
  `[{"lat":5.6037,"lng":-0.1870}, …]` for pasting a surveyed boundary.
- **Soft enforcement** switch — *"Allow the punch but flag it as outside the zone."*
- **Hard enforcement** switch — *"Reject punches made outside the zone."*
- **Active** switch · **Notes**

The map and the numeric fields are two views of one value: type coordinates and the pin moves;
move the pin and the coordinates change.

**Validation is mirrored on both sides.** A circle needs a valid centre and a radius; a polygon
needs **at least three corners** — *"A polygon needs at least three corners. Click them on the
map."* The server refuses a polygon whose JSON does not parse, with the parser's own reason,
rather than storing something that would be silently skipped at every punch.

> **Where the link is made:** a zone is attached to a **Location**, not to an employee. The
> Location edit screen (`/administration/hr/location/locations/[id]/edit`) has a
> *Map & Attendance Zone* section with a **Geofence zone** dropdown. The chain at punch time is
> `Employee → LocationId → Location.GeofenceZoneId → GeofenceZone`. Break any link in that chain
> and the punch comes back *Unverified* with a message explaining which link was missing.

### ▶ Walk it

**1 — Open the register.** One row: *Tema Head Office — 200 m*, Circle,
`5.66980, -0.01660`, 200 m radius, **Soft**, Active.

**2 — Click ⋯ → Edit** and let the map render.

> *"An orange circle, two hundred metres, sitting over the head-office compound and its car
> park. Click anywhere to move the centre; drag the handle on the edge to change the radius; or
> stand at the gate with a phone and press 'Use my position'."*

**3 — 🔴 LIVE WRITE 2 (optional, and very effective) — drag the radius handle out to about 400 m
and press Save.** The list redraws with the new extent.

> *"That is the fence redrawn, live, by an HR officer with no GPS coordinates in their head."*

⚠ Drag *slowly*. The map only captures the pointer after the drag actually starts, so a fast
flick can land as a click and move the centre instead of the radius. *Undo:* re-edit and set the
radius back to 200, or run chapter 28's statement.

**4 — Switch the *Shape* dropdown to Polygon** to show the second map — and then switch **back
to Circle** and press **Cancel**.

> *"A compound with an irregular boundary gets a polygon instead — click the corners, or paste a
> surveyed boundary as coordinates. The check then asks whether the punch falls inside the
> outline and how far it is from the nearest edge."*

**5 — Point at the two enforcement switches and say the sentence that matters.**

> *"And this is the decision every organisation has to make deliberately. Soft: record the
> punch, flag it, let the supervisor look. Hard: refuse it — the person genuinely cannot clock
> in from the wrong place. TDC is on soft, which is the right way to start, because the first
> month of GPS data always teaches you something about your own pin."*

**6 — *(Optional, needs the `admin` window)* Show a delete being refused, then done properly.**
As `hr.head`, 🚫 press ⋯ → **Delete** on the zone. You get a 403 toast.

> *"Refused — and correctly. Deleting a fence changes what every punch in the organisation is
> checked against. That is an administrator's act, not an HR officer's."*

Switch to Window C (`admin`), show the same menu working, and then **Cancel** rather than
actually deleting. If you skipped the third window, skip this step entirely — do not press the
button just to show the error unless you have said the sentence first.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/geofence-zones` | signed-in |
| Create | `POST api/geofence-zones` | `HR.Attendance.Write` |
| Update | `PUT api/geofence-zones/{id}` | `HR.Attendance.Write` |
| Delete | `DELETE api/geofence-zones/{id}` | **`HR.Attendance.Admin`** |
| Link to a location | `PUT api/Location/{id}` *(the `geofenceZoneId` field)* | location permissions |

Tables: `GeofenceZones`, and `Locations.GeofenceZoneId`. Verification at punch time is
`GeofenceVerificationService`, which writes one `AttendanceLocationVerificationLogs` row per
punch that had either a configured zone or coordinates.

**The distance maths:** a circle uses the haversine formula against the centre; a polygon uses a
point-in-polygon test plus the distance to the nearest edge — inside or out. Polygon zones were
skipped as *Unverified* until 2026-09-03; they are enforced now.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-18 · Only the first zone linked to a location is checked.** The verification service takes `zones.FirstOrDefault()`. A site with a main compound and a separate yard, both fenced, gets one of them — whichever the database returns first | |
| **A-19 · Enforcement is checked per zone, not per employee.** There is no way to say "hard for the gate staff, soft for the engineers". The switch is on the fence | |
| **A-20 · Device punches are not geofenced.** The check only runs where GPS coordinates arrive — i.e. from phones and the web portal. A fixed reader on a wall sends none, so its punches are always *GPS unavailable*. That is the right behaviour (the reader *is* the location) but it is worth saying, because someone will ask | |

---

## 10. `/administration/hr/attendance/devices` — the readers on the gates

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Devices** ·
`/administration/hr/attendance/devices` · as **hr.head** · **4 minutes**

### 📖 What it is

> *"The register of physical clocking hardware. Every punch that arrives from a machine carries
> the machine's own identifier, and this is where that identifier is matched to a name, a model,
> a location and a network address — so that when a gate stops reporting, somebody knows which
> gate and where to go."*

### 👁 On the page

**+ Add device** above a table:

| Column | Shows |
|---|---|
| **Device** | device name |
| **ID** | the vendor's own device identifier |
| **Type** | Fingerprint · Face recognition · RFID card · Iris · Palm · QR code · PIN pad · Mobile app · Web portal |
| **Location** | the linked location's name, else the free-text description |
| **Last sync** | date and time, or an em dash |
| **Pending** | unprocessed punches still sitting on the device |
| **Status** | Active / Inactive |

Row ⋯ menu: **Record sync**, **Edit**, 🚫 **Delete**.

**Add / Edit dialog** — hint *"The device ID must match the identifier the hardware reports with
its punches."*: **Device ID** *(required, ≤100)* · **Device name** *(required)* · **Type**
*(required)* · **Manufacturer** · **Model** · **Firmware version** · **Location description**
*(required, "e.g. Main gate, ground floor")* · **IP address** · **Port** *(0–65535)* ·
**Active** switch · **Notes**.

> ⚠ **Three fields are create-only.** `deviceId`, `manufacturer` and `deviceType` are not on the
> update DTO, so the dialog shows them when editing and drops them on save. The device ID in
> particular must never change — it is what incoming punches carry.

**Record sync** confirmation: *"Stamps the device as synced now and clears its pending-punch
count. Use after a manual pull."*

### ▶ Walk it

**1 — Open the register.** Three rows:

| Device | Type | Where |
|---|---|---|
| Main Gate Turnstile — Head Office *(ZKTeco F18)* | Fingerprint | Pedestrian turnstile, main gate |
| HR Block Entrance — Face Reader *(Hikvision DS-K1T341)* | Face recognition | Ground floor lobby, HR block |
| Estates Yard Gate — Card Reader *(ZKTeco SCR100)* | RFID card | Vehicle gate, Estates yard, Community 1 |

> *"Three readers, three technologies, one register. A fingerprint turnstile at the main gate, a
> face reader in the HR block, and a card reader on the yard gate where the drivers come
> through. Each has its vendor ID, its model, its firmware and its IP address — which matters,
> because these are devices on a network and somebody has to keep them patched."*

**2 — Point at *Last sync* and *Pending*.**

> *"Two columns that earn their place. Last sync tells you when the device last handed over its
> punches; pending tells you how many are still sitting on it. A device that has not synced
> since Friday with two hundred pending punches is a Monday-morning problem, and it is visible
> from the dashboard as well."*

**3 — Click ⋯ → Edit on the Main Gate Turnstile**, walk the fields, **Cancel**.

> *"Note the top field — the device ID. That is the vendor's own identifier, and it is what
> every punch this machine sends carries. Set once at registration; the update endpoint does not
> even accept it."*

**4 — 🔴 LIVE WRITE 3 (optional) — ⋯ → *Record sync* → confirm.** The **Last sync** column
stamps to now and **Pending** clears to 0.

> *"Recording a sync after a manual pull. In a full deployment a connector does this
> automatically; the button is here so a human can log the manual case, because a sync that
> happened and was not recorded looks exactly like a device that has stopped."*

*Undo:* nothing to undo — it records an operational fact, not a business one.

**5 — 🚫 DO NOT PRESS Delete.** Admin.

**6 — The link into the rest of the module:**

> *"Every punch on the Punch Logs screen carries the serial of the device it came from. That is
> the join — this register gives that serial a name, a place and an owner."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-attendance-devices` | `HR.Attendance.Read` |
| Register a device | `POST api/staff-attendance-devices` | `HR.Attendance.Write` |
| Update | `PUT api/staff-attendance-devices/{id}` | `HR.Attendance.Write` |
| **Record sync** | `POST api/staff-attendance-devices/{id}/sync` | `HR.Attendance.Write` |
| Delete | `DELETE api/staff-attendance-devices/{id}` | **`HR.Attendance.Admin`** |

Table: `StaffAttendanceDevices`. Two reads exist that no screen uses:
`GET …/external/{externalDeviceId}` (look a device up by its vendor id — what a device
connector would call) and `GET …/overdue-sync` (devices that have not synced recently). The
dashboard's *devices awaiting sync* figure comes from `PendingSyncCount > 0`, not from that
second endpoint.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-21 · There is no device connector.** Nothing polls a reader, and no endpoint accepts a device's batch. Punches reach the system through the self-service punch, the manual log-capture endpoint or a bulk import. The register, the serial on every log and the `GET /external/{id}` lookup are the hooks a connector would use — the connector itself is not built | |
| **A-22 · The *Location* dropdown is not on the form.** `locationId` is always sent as `null`, so the Location column falls back to the free-text description. The FK works and the demo data sets it through the API | |
| **A-23 · Record sync always sends 0.** The confirmation has no field for the pending count; the screen hard-codes zero | |

---

## 11. `/administration/hr/attendance/alert-rules` — what should raise a flag

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Alert Rules** ·
`/administration/hr/attendance/alert-rules` · as **hr.head** · **5 minutes**

### 📖 What it is

> *"Nobody reads an attendance register line by line. A rule says 'tell me when this pattern
> appears' — three days absent, half an hour late twice in a month, a day with only one punch,
> overtime past three hours — and the system raises an alert against that employee for somebody
> to acknowledge."*

### 👁 On the page

**+ Add rule** above a table: **Rule**, **Trigger**, **Severity** (Critical red / Warning grey /
Info outline), **Threshold**, **Open alerts** (count), **Status**. Row ⋯ menu:
**Activate** / **Deactivate** *(the label flips with the row's state)*, **Edit**, 🚫 **Delete**.

**Add / Edit dialog** — hint *"Rules are evaluated when a daily attendance record is created or
changed."* ⚠ **That sentence is not true** — see the gaps.

- **Rule name** *(required, ≤150)* · **Description**
- **Trigger** *(required)* — Consecutive absences · Chronic lateness · Missing punch · Overtime
  threshold reached · Excessive early departure · Unauthorised absence · Low attendance
  percentage
- **Severity** *(required)* — Info · Warning · Critical
- **Threshold** *(required, 0–10 000, step 0.01)* · **Window (days)** *(1–365)*
- A live hint under the two numbers that names the unit for the chosen trigger:
  *"Fires at 3 consecutive days absent"* · *"…late arrivals in the window"* ·
  *"…missing punches in the window"* · *"…overtime hours in the window"* ·
  *"…% attendance, below which the alert fires"*
- **Notify in app** switch · **Notify by email** switch
- **Requires acknowledgement** switch — *"The alert stays open until someone acknowledges it."*
- **Active** switch

> ⚠ **The trigger type is fixed after creation.** Alerts already raised reference it, so the
> update DTO omits it.

### ▶ Walk it

**1 — Open the register.** Four rules:

| Rule | Trigger | Severity | Threshold |
|---|---|---|---|
| Chronic lateness — 30 minutes or more | Chronic lateness | Warning | 30 |
| Missing punch | Missing punch | Info | 1 |
| Unauthorised absence | Unauthorised absence | **Critical** | 1 |
| Overtime above 3 hours in a day | Overtime threshold reached | Warning | 3 |

> *"Four rules, and each is a sentence the corporation has decided is worth interrupting
> somebody about. Half an hour late is a warning. A day with only one punch is information — it
> cannot be paid as it stands, but it is usually a forgotten clock-out. An absence with no leave
> application against it is critical, because that is a disciplinary matter waiting to happen.
> And overtime past three hours is a warning for the Head of HR to check against an approved
> request."*

**2 — Click ⋯ → Edit on *Chronic lateness*** and point at the threshold hint.

> *"Thirty — and the system tells you what thirty means for this trigger: thirty minutes on an
> arrival. Change the trigger and the unit under the box changes with it, so nobody has to
> remember whether they are typing minutes, days or a percentage."*

**3 — Point at the two notification switches and the acknowledgement switch, then Cancel.**

> *"In-app and email, and then the important one: does this alert stay open until a human being
> says they have seen it. For chronic lateness, yes — an alert nobody acknowledges is an alert
> nobody acted on."*

**4 — 🔴 LIVE WRITE 4 (optional) — ⋯ → *Deactivate* on the *Missing punch* rule**, show it flip
to Inactive, then ⋯ → **Activate** to put it back.

> *"Rules are switched off rather than deleted, because the alerts they have already raised
> point back at them."*

**5 — 🚫 DO NOT PRESS Delete.** Admin.

**6 — Set up the honest sentence for chapter 22** — better said here than there:

> *"One thing to know about how these run today: they are evaluated on demand rather than on a
> schedule. There is an endpoint that runs every rule against a day, and the alerts you will see
> in a moment were produced by it. What is not yet built is the nightly job that calls it for
> yesterday's attendance. The rules, the thresholds, the severities and the alert lifecycle are
> all here; the clock that drives them is the next increment."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-attendance-alert-rules` | `HR.Attendance.Read` |
| Create | `POST api/staff-attendance-alert-rules` | `HR.Attendance.Write` |
| Update | `PUT api/staff-attendance-alert-rules/{id}` | `HR.Attendance.Write` |
| **Activate / Deactivate** | `POST api/staff-attendance-alert-rules/{id}/toggle-active` | `HR.Attendance.Write` |
| Delete | `DELETE api/staff-attendance-alert-rules/{id}` | **`HR.Attendance.Admin`** |
| *(evaluation)* | `POST api/staff-attendance-alerts/evaluate/{dailyAttendanceId}` | `HR.Attendance.Write` |

Tables: `StaffAttendanceAlertRules`, `StaffAttendanceAlerts`.

### ⚠ Known gaps

These are the most consequential in the module after the five rules, and they are all in one
place — the evaluator.

| Gap | |
|---|---|
| **A-24 · Nothing calls the evaluator.** `POST …/alerts/evaluate/{id}` exists, the frontend client has a method for it, and **no screen calls it and no background job calls it**. Rules never fire on their own. The dialog's own hint claims they are evaluated on create or change; they are not | |
| **A-25 · The evaluation window is ignored.** `EvaluationWindowDays` is on every rule, on the form and on the entity, and the evaluator never reads it. Every trigger is judged on **one day in isolation** | |
| **A-26 · Consecutive absences is not consecutive.** It fires on a single absent day, and does not compare the threshold at all. `UnauthorisedAbsence` is identical to it, and neither checks whether a leave request covers the day | |
| **A-27 · Low attendance percentage never fires.** It is the one trigger with no branch in the evaluator at all — it falls through to *false* | |
| **A-28 · The rule's scope is ignored.** A rule can be scoped to an organisation unit, a position or a single employee. The evaluator loads *all* active rules and applies every one to whoever's day it was handed. A rule written for one person fires for everybody | |
| **A-29 · Nothing is notified.** `NotifyByEmail`, `NotifyInApp` and `NotifyRecipientsJson` are stored and read by nothing. No email, no in-app notification | |
| **A-30 · No de-duplication.** Evaluating the same day twice raises the same alerts twice. The demo seeder guards against this by running the evaluator only while the alert table is empty | |
| **A-31 · `TriggerValue` is never set.** The column that should record *"4 consecutive absences when the threshold was 3"* is left null, so the alert can say which rule fired but not by how much | |

---

## 12. `/administration/hr/attendance/overtime-policies` — who may claim

### 📍 Where you are

**Sidebar:** Administration → HR → Time, Attendance & Leave → **Overtime Policies** ·
`/administration/hr/attendance/overtime-policies` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Overtime eligibility is a property of the post, not of the person. A driver's job runs past
> five; a General Manager's does not attract overtime at any hour. So the rule is set once on
> the position and everybody holding it inherits it — and where an individual's circumstances
> genuinely differ, there is a per-employee override with a reason and an approver on it."*

### 👁 On the page

**Card 1 — Position.** A single dropdown listing every position in the establishment. **Nothing
else on the page renders until you choose one** — the API is keyed by position and there is no
tenant-wide "all policies" read.

Before a choice: *"Choose a position — overtime policies are held per position."*

**Card 2 — Policies for that position.** **+ Add policy** above a table:

| Column | Shows |
|---|---|
| **Allowance** | Overtime · Night allowance · Shift differential · Weekend allowance · Holiday allowance · Transport allowance · Other |
| **Eligibility** | **Exempt** (red) · **Eligible** (solid) · **Not eligible** (grey) |
| **Max / day** · **Max / week** | hours, or an em dash |
| **Pre-approval** | Yes / No |
| **Effective** · **Expires** | dates |

Row ⋯ menu: **Show overrides** / **Hide overrides** *(a toggle)*, **Edit**, 🚫 **Delete**.

**Policy dialog** — hint *"One policy per allowance type. Supersede an old rule with a new
effective date rather than editing history."*: **Allowance type** *(required)* · **Eligible**
switch · **Exempt** switch — *"Excluded from this allowance regardless of hours worked."* →
**Exemption reason** *(required when Exempt is on)* · **Max hours / day** · **Max hours / week**
· **Requires pre-approval** switch · **Effective from** *(required)* · **Expires** *(must not
precede the effective date)* · **Notes**.

**Card 3 — Employee overrides** *(appears only after pressing "Show overrides" on a policy)*.
**+ Add override** above a table: **Employee**, **Allowance**, **Eligibility**, **Reason**,
**Effective**, **Expires**. Dialog: **Employee** *(picker, required)* · **Allowance type** ·
**Eligible** switch · **Exempt** switch · **Override reason** *(required)* · **Effective from**
*(required)* · **Expires**.

> **Exempt beats Eligible.** When both switches are on, the entity's rule is that exemption
> wins. The eligibility badge shows *Exempt* in red for exactly that case.

### ▶ Walk it

**1 — Open the screen.** The empty state is the point:

> *"Nothing here until you pick a post — because that is how the model thinks. Overtime is a
> property of the job."*

**2 — Choose a position that has a policy** — the demo data sets one against the **General
Manager – Operations**. If the dropdown is long, type to filter.

**3 — If the position has no policy**, read the empty line aloud; it is a good one:

> *"'Without a policy, this position falls back to its work schedule's overtime settings.' So
> silence here is not a gap — it means the standard rule applies."*

**4 — Show the *exempt* case.** For the General Manager, the demo carries an **employee
override** rather than a position policy: Overtime, **Exempt**, reason *"Management grade M1 —
overtime is not payable; exemption recorded so the claim screen refuses it."*

> *"Management grade M1. Overtime is not payable, and the exemption is recorded with a written
> reason — not left as an absence of a rule. That is what an auditor wants to see: a decision
> with words on it, an effective date and an expiry."*

**5 — Press ⋯ → *Show overrides* on a policy** and show the second table.

> *"And the exceptions. A driver whose hours genuinely run late; a guardsman on the night watch
> whose night allowance is approved for the year. Every override carries a reason, an approver
> and a date range — you cannot create a silent one."*

**6 — 🚫 DO NOT PRESS Delete** on a policy or an override. Admin.

**7 — The honest sentence, and it is short:**

> *"These rules are the register of who may claim. They are read by the people who approve, and
> they are not yet read by the request form itself — so an overtime request from an exempt
> post will still submit and has to be refused by the approver. Wiring the refusal into
> submission is a small change on a rule that is already captured."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Position dropdown | `GET api/hr/employee-positions` | job-architecture read |
| Policies for a position | `GET api/position-overtime-policies/position/{positionId}` | signed-in |
| Create / update policy | `POST` / `PUT api/position-overtime-policies[/{id}]` | `HR.Attendance.Write` |
| Overrides for a policy | `GET api/position-overtime-policies/{id}/overrides` | `HR.Attendance.Read` |
| Add / update override | `POST .../{id}/overrides` · `PUT .../overrides/{overrideId}` | `HR.Attendance.Write` |
| Delete either | `DELETE …` | **`HR.Attendance.Admin`** |

Tables: `PositionOvertimePolicies`, `EmployeeOvertimeOverrides`. There is also
`GET api/position-overtime-policies/position/{id}/active` — the "what applies today" read a
request form would call — and a whole second controller,
`api/employee-overtime-overrides`, for reading a person's overrides directly. Neither is used
by any screen.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-32 · Nothing reads these policies.** The overtime request service does not check eligibility, does not check exemption and does not apply the daily or weekly ceiling — even though the entity's own documentation says it should. An exempt employee can raise and have approved an overtime request. This is the gap to close first in this area | |
| **A-33 · The position and allowance type are immutable after creation**, correctly — but the edit dialog still shows the allowance dropdown and drops it on save | |
| **A-34 · The override's approver is the token, not a chosen person.** `approvedById` is sent as a zero GUID and overwritten server-side with whoever is signed in. So "approved by" on an override means "created by". Consistent with the rest of the module, but the field's name promises more | |

---

## 13. `/hr/attendance/daily` — the register

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Daily Attendance** · `/hr/attendance/daily` ·
as **hr.head** · **8 minutes**

### 📖 What it is

> *"This is the attendance register — the centre of the whole module. One row per person per
> day, with the times, the hours, the status and every flag that makes a day worth looking at.
> And it is properly searchable: employee, date range, status, work schedule, pay period and six
> yes/no/either flags, all composing into one query."*

### 👁 On the page

**Header:** title *Daily Attendance*, description *"The full day-by-day record — hours worked,
lateness, exceptions and verification."*, back-link to `/hr/attendance`, and a **Record a day**
button (outline) top right.

**Filters card** — a **Clear** button top right, then four groups:

*Row 1 — three fields*
| Control | Behaviour |
|---|---|
| **Search** | free text, matched against first name, last name and staff number. Debounced 400 ms |
| **Employee** | a searchable employee picker — narrows to one person |
| **Status** | *Any status*, or one of the ten attendance statuses |

*Row 2 — four fields*
| Control | Behaviour |
|---|---|
| **From** | date, defaults to **30 days ago** |
| **To** | date, defaults to **today** |
| **Work schedule** | *Any schedule*, or one of the active schedules |
| **Pay period** | *Any period*, or one of the first 50 pay periods |

*Row 3 — six flag chips.* Each chip **cycles through three states on click**: neutral outline
(*any*) → solid (*yes*) → red (*"Not late"*, i.e. *no*) → back to neutral. A counter beside the
label reads *"N active"*. The six: **Late** · **Overtime** · **Remote** · **Has exception** ·
**Verified** · **Needs verification**.

*Row 4 — sorting.* **Sort by** — Date · Employee · Status · Hours worked · Overtime · Minutes
late. **Direction** — Descending (default) · Ascending.

Changing **any** filter resets to page 1.

**Results card** — the title is a live count (*"143 records"*), with a spinner while refetching.
Seven columns:

| Column | Shows |
|---|---|
| **Employee** | name over staff number |
| **Date** | `04 Sep 2026` over the weekday |
| **In** / **Out** | `08:05` / `17:10`, em dash when missing |
| **Worked** | `8h 5m` — right-aligned |
| **Status** | a status badge (Absent red, Late/Half-day/On-leave/Remote grey, Present solid, Weekend/Holiday/Off-day outline) |
| **Flags** | up to five badges: `Late 47m` · `OT 3h 15m` · `Remote` · **`Exception`** (red) · `Verified` |

**Rows are clickable** — the whole row opens `/hr/attendance/daily/[id]`.

**Paging:** 25 per page, *"Page 1 of 6 · 143 records"* with **Previous** / **Next**. Old rows
stay on screen while a filter change refetches, rather than flashing empty.

**Empty state:** *"No attendance records — nothing matches these filters."* with a **Clear
filters** button.

### ▶ Walk it

**1 — Open the register.** It lands on the last 30 days, newest first.

> *"The register. A hundred and forty-three days of attendance in the last month, newest first
> — and every one of those rows is one person on one date."*

**2 — Point at the *Flags* column** on a row that has badges.

> *"The flags are what turn a register into a work queue. Late, with the minutes. Overtime, with
> the hours. Remote. Exception — in red, because that is a day that will not roll up cleanly.
> And Verified, which is a supervisor saying 'I have looked at this and the hours are right'."*

**3 — Demonstrate the flag chips.** Click **Late** once — it goes solid and the list narrows.

> *"The filters are tri-state. One click: show me only late days. Two clicks —"*

Click **Late** again — it turns red and reads *"Not late"*.

> *"— show me only days that were *not* late. Three clicks and it is back to 'either'. Six of
> those, and they compose with everything above, so 'show me every unverified day with an open
> exception in the September pay period' is four clicks and no report request."*

**4 — Build that exact query in front of the room.** Click **Has exception** to solid, click
**Verified** to red (*"Not verified"*), choose the current month in **Pay period**.

> *"And that is the officer's Monday morning list."*

Press **Clear** afterwards.

**5 — Now find the story.** Type `Kojo` into **Search**.

> *"Kojo Ansah, our new Supervising Architect. Four days in the last fortnight."*

**6 — Read his days aloud.** Two are **Late** (47 minutes and 32 minutes), one has no clock-out
at all.

> *"Forty-seven minutes late, then thirty-two a couple of days later — the Ashaiman road. And
> here is the interesting one: clocked in at three minutes past eight and no clock-out at all.
> That is the day he left through the yard gate after a site visit and never passed the main
> gate reader. Hold that thought — we are going to fix it properly in a few minutes."*

**7 — ⚠ Do not offer to "show the system flagging someone late live".** Lateness is recorded,
not inferred (Rule 1). If the question comes, use § 1.5's sentence.

**8 — Change *Sort by* to *Minutes late*, Descending.**

> *"Sorted by how late. That is the conversation a Head of HR has with a supervisor, and it took
> one click rather than a report."*

**9 — Click Kojo's missing-clock-out row** to open chapter 15.

### ⚙ Behind the page

| Element | Endpoint | Service |
|---|---|---|
| The grid | `POST api/staff-daily-attendance/search?pageNumber&pageSize` | `StaffDailyAttendanceService.SearchAsync` |
| Work schedule dropdown | `GET api/work-schedules/active` | |
| Pay period dropdown | `GET api/pay-periods/paged?pageNumber=1&pageSize=50` | |

Gated on `HR.Attendance.Read`. **POST rather than GET** because the filter carries a status
array and six tri-state flags; paging stays on the query string so a link can page without
re-posting the body — the same shape as `POST api/hr/Employees/paged`.

Server-side the filter composes with **AND** and a null value leaves that dimension alone, so an
empty filter behaves exactly like the plain paged read. Sorting always carries a **secondary
sort on employee name**, because a date-sorted day of attendance ties constantly and paging
would otherwise be unstable.

Table: `StaffDailyAttendances`, joined to `Employees` for the name, the number and the
organisation-unit filter (attendance has no unit of its own — it is a property of the person).

**The API accepts four filters the screen does not offer:** organisation unit, location,
minimum minutes late and minimum overtime hours. All four are implemented server-side.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-35 · The deep link from the employee profile does not work.** The Employees module's *Attendance* tab has an **Open in Attendance** button pointing at `/hr/attendance/daily?employeeId=<guid>`. This page never reads the query string, so it opens unfiltered. A one-line fix; a visible one, because the button is on every employee | |
| **A-36 · Four server-side filters are unreachable** — organisation unit, location, minimum lateness, minimum overtime. "Everybody in Estates who was more than 20 minutes late" is one request away and no screen makes it | |
| **A-37 · No export.** There is no CSV or Excel button on this screen, or on any screen in the module. The register is the most-requested attendance report in the FR set and it can only be read on screen | |

---

## 14. `/hr/attendance/daily/new` — recording a day by hand

### 📍 Where you are

**From:** `/hr/attendance/daily` → **Record a day** · `/hr/attendance/daily/new` ·
as **hr.head** · **5 minutes**

### 📖 What it is

> *"Not every site has a reader, and not every day goes to plan. This is the fallback: an HR
> officer records a day directly, with everything a punch would have produced and a few things
> it cannot — the reason for a status, whether it was late and by how much, whether overtime was
> worked."*

### 👁 On the page

**Header:** *Record Attendance* — *"Enter a day by hand — for sites without devices, or to fill
a gap."*, back-link to the register.

**Card 1 — Who and when**
- **Employee** *(searchable picker, required)*
- **Date** *(required, defaults to today)* · **Status** *(required, defaults to Present)*
- **Work schedule** *(dropdown of active schedules, clearable)*
- A line of helper text: *"Will be assigned to the open pay period: September 2026."*
- **Status reason** *(textarea, ≤500)*

**Card 2 — Hours**
- **Check in** · **Check out** *(time fields)*
- **Hours worked** *(0–24, step 0.25)* · **Break (minutes)** *(0–1440)*

**Card 3 — Flags** — four switches, each revealing a field when on:
- **Late arrival** → **Minutes late** *(required to be > 0 when the switch is on)*
- **Early departure** → **Minutes early**
- **Overtime worked** → **Overtime hours** *(required to be > 0 when on)*
- **Worked remotely** → **Remote location**
- **Notes** *(≤1000)*

**Footer:** **Cancel** · **Record attendance**. On success it navigates straight to the new
day's detail page.

Two behaviours worth knowing:

- **The pay period is filled in for you.** If you leave it blank the form sends the current open
  period, so the day rolls into the right month. This is the *only* place in the module that
  sets `PayPeriodId`.
- **The hours are not checked against the times.** Type 08:00 in, 17:00 out and 3 hours worked
  and it will save all three. The server derives hours only when *processing a punch*, never
  here.

**One record per person per day.** Creating a second is refused with *"A daily attendance record
already exists for employee … on 2026-09-17."*

### ▶ Walk it

**1 — Press *Record a day* from the register.**

> *"Every attendance system needs an honest back door, because the alternative is a spreadsheet
> on somebody's desk. This is ours — and note that it is not a quiet one: it is a permissioned
> screen, the record carries who created it, and the day it makes is the same row a device would
> have made."*

**2 — Fill it in for a site without a reader.** Pick an employee; leave the date as today; leave
the status as **Present**; choose **TDC Standard Office Hours**; type **08:15** in and **17:00**
out; **8.75** hours worked.

**3 — Point at the pay-period line before going further.**

> *"See that line — it will be assigned to the open pay period, September. That is the link that
> makes this day countable at month end, and this form is the one place in the product that
> sets it."*

**4 — Turn on *Late arrival*** and watch **Minutes late** appear. Type **15**.

> *"And here is the thing I flagged at the start, said plainly. Lateness is a fact a human being
> records, with a reason beside it. Fifteen minutes. The system is not inferring it from the
> schedule — yet."*

**5 — 🔴 LIVE WRITE 5 — press *Record attendance*.** You land on the new day's detail page with
a *Recorded* toast.

*Undo:* chapter 28. `hr.head` cannot delete it from the UI (Admin), so the undo is SQL or an
`admin` session.

**6 — ⚠ CAREFUL — do not record a day for somebody you have already shown.** The second create
is refused, and a red toast in the middle of a demo reads as a fault even when it is a correct
refusal. Pick a name you have not used.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Create | `POST api/staff-daily-attendance` | `HR.Attendance.Write` |
| Schedule dropdown | `GET api/work-schedules/active` | |
| Pay period default | `GET api/pay-periods/current` | |

Table: `StaffDailyAttendances`. `CreatedBy` carries the acting employee's id from the token.
`DayOfWeek` is derived from the date. The duplicate check runs before the insert and answers
409-shaped `InvalidOperationException`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-38 · The form offers nine of the day's sixty fields.** No location, no GPS, no device, no verification flag, no exception, no leave or holiday link, no scheduled times. The scheduled times in particular matter: the demo seeder sets them through the API and this form cannot, so a hand-recorded day shows `Scheduled start —` on its detail page | |
| **A-39 · Hours are not validated against the times, in either direction.** And no server-side derivation runs for a hand-recorded day | |
| **A-40 · There is no edit screen for a day.** `PUT api/staff-daily-attendance/{id}` exists and is fully implemented; no page calls it. Once a day is recorded the only way to change its times is a **regularization** — which is arguably the right answer, and is worth saying that way rather than apologising for it | |

---

## 15. `/hr/attendance/daily/[id]` — one day, in full

### 📍 Where you are

**From:** any row of the register · `/hr/attendance/daily/[id]` · as **hr.head** · **7 minutes**

### 📖 What it is

> *"Everything known about one person on one date: what they were scheduled to do, what they
> actually did, where they were when they did it, which machine saw them, and whether anybody
> has checked it. Plus the raw punches underneath it and any request to correct it."*

### 👁 On the page

**Header:** `Kojo Ansah · 11 Sep 2026`, subtitle `Friday · TDC Standard Office Hours`,
back-link to the register, and on the right: a **status badge** plus, conditionally, two buttons:

| Button | Appears when | Does |
|---|---|---|
| **Approve exception** *(outline, shield icon)* | `HasException` and not yet approved | opens a dialog with a **Notes** box |
| **Verify** *(solid, badge icon)* | `RequiresVerification` and not yet verified | opens a dialog with a **Notes** box |

**Three tabs.**

**Tab 1 — Overview**, four information cards:

*Hours* — Scheduled start · Scheduled end · Scheduled hours · Actual in · Actual out · Worked ·
Break · Late (`Yes (47 min)` / `No`) · Early departure.

*Overtime* — Overtime (Yes/No) · Overtime hours · Approved · Approved by.

*Location & device* — Location · Check-in place · Check-out place · **Check-in verification**
(*Within zone* / *Outside zone* / *Unverified* / *Gps unavailable*) · Check-out verification ·
Geofence zone · Check-in device · Check-out device · Remote work.

*Period & verification* — Pay period · Public holiday · Requires verification · Verified ·
Verified by · Verified on.

*Notes & exceptions* — rendered only when there is something to show: the exception (marked
*(approved)* or *(open)*) with its reason and approver, the status reason, the verification
notes and the day's notes.

**Tab 2 — Punches (N)** — every raw log folded into this day: **Time**, **Type** (Check in /
Check out / Break start / Break end), **Device** (serial), **Processed** (a `Processed` or
`Pending` badge). Empty state: *"This day was recorded without any device or self-service
punches."*

**Tab 3 — Regularizations (N)** — requests raised against this day: **Number**, **Type**,
**Raised**, **Status**, **Applied**. Rows are clickable into the regularization. Empty state
carries a **Raise a regularization** button which opens the new-regularization form with this
day already selected.

**Confirmation dialogs.** *Verify:* *"Confirms the recorded hours are correct so the day can
roll into the monthly summary."* *Approve exception:* *"Accepts the exception on this day so it
stops blocking the summary."* Both take optional notes.

### ▶ Walk it

**1 — Open Kojo Ansah's missing-clock-out day** from the register.

> *"One person, one day, and everything the organisation knows about it."*

**2 — Read the *Hours* card.** In at 08:03, out **em dash**, worked **em dash**.

> *"Clocked in at three minutes past eight. No clock-out. No hours. And that is exactly what the
> system should say — it does not guess, and it does not quietly put seventeen hundred in the
> box. An unfinished day stays unfinished until somebody accounts for it."*

**3 — Move to *Location & device*.** This is the card that earns the module its credibility.

> *"Where and how. The site. The place the punch was made. The verification verdict against the
> geofence. The zone it was checked against. And the device — TDC-BIO-001, the main gate
> turnstile, which is a row on the device register with a model, a firmware version and an IP
> address."*

**4 — Now open a *different* day to show the geofence flag: go back and open the new hire's
clock-in from Ashaiman.** Check-in verification reads **Outside zone**.

> *"And here is the one that makes people sit up. Outside zone. He clocked in from Ashaiman
> Market, about three and a half kilometres from the head-office fence. The punch was accepted —
> we are on soft enforcement — but it is recorded, it is on the day, and it is visible to his
> supervisor without anybody going looking."*

**5 — Switch to the *Punches* tab.**

> *"And underneath the day, the raw events it was built from — each with its time, its type, its
> device and whether it has been processed. The day is a derived thing; these are the facts."*

**6 — Switch to *Regularizations*.** On Kojo's missing-clock-out day there is one, already
**Applied**.

> *"And here is the correction he asked for. We will open it in a moment, but note what it means
> that it lives here: the day carries its own history of being disputed."*

**7 — ⚠ CAREFUL — the two supervisor buttons will not be on the page.** **Verify** appears only
when a day has `RequiresVerification = true`, and **Approve exception** only when it has
`HasException = true` — and **nothing on this database has either**, because no create form and
no API DTO carries those two flags (A-43). On a fresh demo database the header shows the status
badge and nothing else.

Do **not** say "let me verify this day" and then hunt for a button. Do one of these two instead:

**7a — Say it from the *Period & verification* card**, which is always there:

> *"Two acts belong to a supervisor rather than to a workflow, and they are recorded here rather
> than in an approval chain. Verify — 'I have looked at these hours and they are right', with
> the name and the date. And approve the exception — 'yes, this day was odd and I accept it'.
> Neither is a request, so neither needs a chain; a manager is just doing their job, and the
> record keeps who and when."*

**7b — Or prepare one before the demo** with § 2.6b's two-line SQL, and then the **Verify**
button is there and you can press it.

**8 — 🔴 LIVE WRITE 6 — *only if you ran § 2.6b.* Press *Verify*, add a note, confirm.** The
*Verified* badge appears in the Flags column back on the register, and the *Period &
verification* card fills with your name and the time.

> *"Verified — and the register now carries the badge, so an officer scanning the month can see
> at a glance which days a supervisor has actually looked at."*

*Undo:* chapter 28 (SQL — there is no unverify).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/staff-daily-attendance/{id}` | **self-or-`HR.Attendance.Read`** |
| Verify | `POST api/staff-daily-attendance/verify` | `HR.Attendance.Write` |
| Approve exception | `POST api/staff-daily-attendance/{id}/approve-exception` | `HR.Attendance.Write` |

The detail read is **self-or-permission**, checked *after* the fetch because ownership is only
knowable once the row is loaded. An employee opening their own day is allowed; anybody else
needs the read tier.

`VerifiedById` is the verifier's **employee** id from the token — an `Employees` foreign key,
not a login id.

Tables: `StaffDailyAttendances` with `StaffAttendanceLogs` and
`StaffAttendanceRegularizations` included.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-41 · Approving an exception overwrites the day's Notes.** `ApproveExceptionAsync` writes the dialog's comment into `Notes` — the day's own notes field — rather than into an approval-comment column. Anything that was in Notes is lost | |
| **A-42 · The exception approver is never recorded.** `ExceptionApprovedById` is on the entity, on the DTO and on this page (*"Approved by …"*), and the service never sets it. The line renders blank for ever | |
| **A-43 · Nothing ever sets `RequiresVerification` or `HasException`.** Neither flag is on `CreateStaffDailyAttendanceDto` or `UpdateStaffDailyAttendanceDto`, so no screen and no API caller can turn either on — yet both are filterable on the register, both drive the two buttons on this page, and both have dedicated reads (`pending-verification`, `open-exceptions`). **Consequence for a demonstration: neither button ever appears.** The verification and exception features are complete except for the field that starts them. See § 2.6b | |
| **A-44 · No GPS coordinates are shown, only the verdict.** The day carries check-in and check-out latitude and longitude; the page shows the status and the zone name but never the numbers or a map | |

---

## 16. `/hr/attendance/records` — the simple register

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Attendance Records** · `/hr/attendance/records` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"A second, deliberately simpler register: one row per person per day with a time in, a time
> out, the hours and a status. No schedule, no geofencing, no exceptions. It is for the sites and
> the situations where all anybody needs is a mark in a book — and it exists so that the full
> record does not get filled with half-facts."*

### 👁 On the page

**Header:** *Attendance Records* — *"The simple clock-in / clock-out register, one row per
employee per day."*, back-link.

**Date card** — a single **Showing** date input, defaulting to today. **Changing it refetches**
the whole list; the date is part of the query key, so you never see yesterday's rows under
today's heading.

**Records panel** — **+ Add record** above a table:

| Column | Shows |
|---|---|
| **Employee** | name |
| **Date** | `17 Sep 2026` |
| **In** / **Out** | `08:02` / `17:05` |
| **Worked** | `8h 5m`, right-aligned |
| **Status** | status badge |

Row ⋯ menu: **Edit**, **Remove record**.

**Dialog** — hint *"For the fuller picture — schedules, geofencing, exceptions — use Daily
Attendance instead."*: **Employee** *(picker, required)* · **Date** *(required)* · **Status**
*(required)* · **Check in** · **Check out** · **Worked hours** *(0–24, step 0.25)* · **Overtime
hours** · **Notes**. Validation refuses a check-out identical to the check-in.

> ⚠ **Editing drops the employee and the date.** They identify the row, so the update endpoint
> does not accept them; the dialog shows them and ignores them on save. Changing a row's date
> means deleting it and adding another.

Empty state: *"Nothing recorded for 17 Sep 2026."*

### ▶ Walk it

**1 — Open the screen.** Today's date, and — depending on the day — either a handful of rows or
an empty state.

**2 — Set the date back a few days** to a date the seeder populated, and let the rows appear
(five people, 08:02 in, 17:05 out, 8h 5m).

**3 — Say why it exists.** This is the whole chapter:

> *"Two registers, and people always ask why. The full one you have just seen carries sixty
> columns — schedules, geofences, verification, exceptions — because that is what a head office
> with readers on the gates needs. This one carries six. It is for the depot, the outstation,
> the contractor's gang: a name, a date, a time in, a time out. Keeping them apart means the
> serious register never fills up with rows where two thirds of the columns are empty and
> nobody can tell whether that means 'no' or 'we did not ask'."*

**4 — Click ⋯ → Edit on a row**, show the six fields, **Cancel**.

**5 — 🚫 DO NOT PRESS ⋯ → *Remove record*.** Like every delete in this module it is
`HR.Attendance.Admin` and `hr.head` is refused. Point at it instead:

> *"Correcting a row here is an HR officer's job; removing one is not. A row may already sit
> inside a month somebody has signed off, so deletion is an administrator's act right across
> this module — and the server, not the screen, is what enforces it."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The day's list | `GET api/staff-attendance-records/date/{date}` | `HR.Attendance.Read` |
| Add | `POST api/staff-attendance-records` | `HR.Attendance.Write` |
| Edit | `PUT api/staff-attendance-records/{id}` | `HR.Attendance.Write` |
| Remove | `DELETE api/staff-attendance-records/{id}` | **`HR.Attendance.Admin`** |

Table: `StaffAttendanceRecords`. The controller also offers per-employee reads, a date-range
read and a by-status read — none of which this screen uses. The per-employee and per-day reads
are **self-or-permission**, so an employee could be shown their own simple register; no screen
does.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-45 · One day at a time, one date at a time.** There is a date-range read on the controller (`GET …/range?from&to`) and this screen only ever asks for a single day. There is no way to see a week | |
| **A-46 · The two registers never meet.** `StaffAttendanceRecord` and `StaffDailyAttendance` are independent tables. A row here does not appear in Daily Attendance, is not counted by the dashboard, and is **not read by the monthly summary** — which sums `StaffDailyAttendances` only. So a site recorded entirely on the simple register produces a monthly summary of zero. This is the most important thing to know about this screen and the seeded demo data has rows in both | |
| **A-47 · Overtime hours are on the create form and not on the table.** The column exists on the entity and the DTO; the grid shows Worked but not Overtime | |

---

## 17. `/hr/attendance/logs` — the raw punches

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Punch Logs** · `/hr/attendance/logs` ·
as **hr.head** · **5 minutes**

### 📖 What it is

> *"Every clock event, exactly as it arrived, before anybody interpreted it. A punch is only a
> fact about a moment — it becomes attendance when it is processed into a day. Keeping the two
> apart is what lets a device sync three days late, lets an unmatched card be found, and gives
> you a record nobody edited."*

### 👁 On the page

**Header:** *Punch Logs* — *"Raw check-in and check-out events from devices and the self-service
clock."*, back-link.

**Filter card** — one dropdown, **Show**: **Unprocessed only** *(the default)* · **All punches**.

**Results card** — *"N punches"* with a refetch spinner. Six columns:

| Column | Shows |
|---|---|
| **Employee** | name |
| **When** | `17 Sep 2026 08:04` — converted to local time, because a punch is a real instant |
| **Type** | Check in · Check out · Break start · Break end |
| **Device** | the device serial, or an em dash for a portal punch |
| **Processed** | a `Processed` (outline) or `Pending` (grey) badge |
| *(last)* | a **Process** button — **only on unprocessed rows** |

**Process** runs immediately, with a spinner on the button, and a toast: *"Processed — the punch
was folded into daily attendance."* It refreshes both this list and the daily-attendance
queries.

**Empty state** — *"Nothing unprocessed — every punch has been folded into a daily attendance
record."* on the default filter; *"No punches have been recorded yet."* on *All*.

> The default is *Unprocessed only* on purpose: the unprocessed ones are the interesting ones.

### ▶ Walk it

**1 — Open the screen.** On a healthy demo database it lands on the empty state.

> *"And that is the answer you want. Nothing unprocessed means every punch this organisation has
> taken has been turned into a day. When this list is not empty, something needs a human: a
> device synced late, or a card the system could not match to a person."*

**2 — Switch *Show* to *All punches*.** The list fills.

**3 — Read a row across.**

> *"Kojo Ansah, eight-oh-four this morning, Check in, from TDC-BIO-001 — the main gate
> turnstile. Processed. And underneath that row, in the database, is the geofence verification
> for that exact punch: the coordinates, the zone it was checked against, the distance, and the
> verdict."*

**4 — Point at a portal punch** (device shows an em dash).

> *"That one has no device, because it came from a phone. Same table, same pipeline — the
> channel is a property of the punch, not a different system."*

**5 — If (and only if) an unprocessed row exists — 🔴 LIVE WRITE 7 — press *Process***.

> *"And that is the punch becoming attendance: the system finds or creates that person's day,
> writes the in or the out onto it, derives the hours if both ends are now present, records the
> location verdict, and marks the punch used. One button, and the day appears on the register."*

If nothing is unprocessed, say the sentence instead of pressing anything:

> *"There is a Process button per row for exactly that case. In a full deployment a device
> connector does this on a schedule; the button is the manual path, and it is also how you
> recover a batch that arrived after the month was worked."*

*Undo:* none — processing is one-way. A wrongly processed punch is corrected by regularising the
day it produced.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Unprocessed | `GET api/staff-attendance-logs/unprocessed` | `HR.Attendance.Read` |
| All | `GET api/staff-attendance-logs/paged?pageNumber=1&pageSize=100` | `HR.Attendance.Read` |
| **Process** | `POST api/staff-attendance-logs/{id}/process` | `HR.Attendance.Write` |
| *(the portal's punch)* | `POST api/staff-attendance-logs/punch` | **signed-in only** — the actor is the token |
| *(manual capture)* | `POST api/staff-attendance-logs` | `HR.Attendance.Write` |
| Delete | `DELETE api/staff-attendance-logs/{id}` | **`HR.Attendance.Admin`** |

Tables: `StaffAttendanceLogs`, `AttendanceLocationVerificationLogs`, `StaffDailyAttendances`.

**What processing actually does**, in order:
1. refuses if the punch is already processed;
2. re-runs the geofence check and **rejects outright** if the zone is hard-enforced and the
   punch is outside it;
3. finds the employee's day for that date, or creates one at status **Present**;
4. writes the in-time (only if the day has none yet) or the out-time (always overwrites);
5. corrects the day's `DayOfWeek` — rows minted before this path stamped it defaulted to Sunday;
6. copies the coordinates, the place, the verification status and the zone onto the day;
7. derives `ActualWorkHours` **only** when both times exist and out is after in;
8. writes one verification-log row if there was a zone or coordinates;
9. marks the punch processed, stamps the date and links it to the day.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-1 (again) · Processing never consults the work schedule.** No `WorkScheduleId`, no scheduled times, no `PayPeriodId`, no lateness, and the status is always *Present* — see Rule 1 | |
| **A-48 · No filters and no search.** No employee, no date range, no device, no type. The controller has per-employee and per-device reads with date ranges; the screen offers neither. On a live tenant this list is unusable after a fortnight | |
| **A-49 · Break punches are captured and never used.** `BreakStart` and `BreakEnd` are in the enum, the portal cannot send them, and processing ignores them — a break punch would be marked processed without touching `BreakStartTime` or `TotalBreakMinutes` | |
| **A-50 · There is no "process all".** A device that syncs 200 punches needs 200 clicks | |
| **A-51 · A second out-punch silently overwrites the first.** Step 4 above always overwrites the out-time, so a person who punches out, comes back and punches out again ends with the later time and no record of the first on the day (the raw log keeps both) | |

---

## 18. `/hr/attendance/regularizations` — correcting a day, properly

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Regularizations** ·
`/hr/attendance/regularizations` (+ `/new`, `/[id]`) · as **hr.head** · **10 minutes**

### 📖 What it is

> *"The most important screen in this module, and the one that answers the question everybody
> asks about electronic attendance: what happens when the machine is wrong. Nobody retypes
> somebody's clock-out. They raise a numbered request, with a type, a reason and the times they
> are asking for, and it goes through an approval chain. And even after approval it does not
> touch the day until somebody presses Apply."*

### 👁 Screen 1 — the register

**Header:** *Attendance Regularizations* — *"Requests to correct a recorded day, and where each
sits in its approval chain."*, back-link, **+ New Request** button.

**Filter card** — one **Show** dropdown: **Awaiting approval** *(the default)* · **All
requests** · then the four statuses — Pending · Approved · Rejected · Applied.

**Results card** — *"N requests"*. Seven columns:

| Column | Shows |
|---|---|
| **Number** | `REG-20260911-00001` |
| **Employee** | name |
| **Attendance date** | the day being corrected |
| **Type** | Missing check-in · Missing check-out · Wrong time entry · Forgot to mark · System error |
| **Raised** | date and time |
| **Status** | badge — shows **Applied** in place of the status once applied |
| **Awaiting** | **the live workflow step and its pending approver**, read from the engine |

Rows are clickable into the detail page. Empty state: *"No regularizations — nothing matches
this filter."*

> The **Awaiting** column is not inferred from the status. It comes from one batched call to the
> workflow engine for every row on the page, so it names the step the record is actually sitting
> on and who can move it.

### 👁 Screen 2 — new (`…/regularizations/new`)

**Card 1 — Which day.** Two modes:

*Arrived from a day* (`?attendanceId=…`, which is how the **Raise a regularization** button on
the day's detail page opens it): a single line — *"Correcting **11 Sep 2026** for Kojo
Ansah."* Nothing to choose.

*Arrived from the register*: **Employee** *(picker, required)* and **Attendance date** *(a date
input, defaulting to today)*. As soon as both are set the page looks the day up and shows one of
three things in a bordered box:

- a spinner — *"Looking up that day…"*;
- the day it found — *"11 Sep 2026 · in 08:03 · out — · Present"* with a **Use this day**
  button that turns into **Selected**;
- or the refusal — *"No attendance was recorded for that date. A regularization can only
  correct an existing record."*

**Card 2 — The correction.**
- **What went wrong** *(required)* — the five regularization types
- **Corrected check-in** · **Corrected check-out** *(time fields — **at least one is required**)*
- A live line beneath them: *"Currently recorded as in 08:03, out —."*
- **Reason** *(required, ≤1000)*
- **Supporting documents** *(textarea, ≤2000, "References to any evidence provided")*

**Footer:** **Cancel** · **Submit for approval**. On success: *"REG-20260917-00001 was raised and
sent for approval."* and you land on the detail page.

> ⚠ **Supporting documents is a text box, not an upload.** It takes a reference — a document
> number, a file name, "see email of 11 Sep" — and nothing is attached. Say "a reference to the
> evidence", not "attach the evidence".

### 👁 Screen 3 — the request (`…/regularizations/[id]`)

**Header:** the regularization number, subtitle `Kojo Ansah · Missing check out on 11 Sep 2026`,
back-link, and on the right:

- a **status badge** (**Applied** once applied);
- the **workflow approval actions** — **Approve** and **Reject**, each opening a comment dialog.
  They render **disabled** with the step and pending approver named beside them if you are not
  the assignee. They appear only while the status is *Pending*;
- **Apply correction** *(solid, wand icon)* — **only** when the status is *Approved* and it has
  not been applied.

**There is no Submit button, and that is correct** — the workflow starts when the request is
created.

**Two tabs.**

*Overview* — three cards:
- **Request** — Employee (name and number) · Attendance date · Type · Raised on · Requested
  check-in · Requested check-out
- **Reason** — the free text, plus the supporting-documents reference if there is one
- **Outcome** — Approved by · Approved on · Approval comments · Rejected on · Rejection reason ·
  **Applied** (*"Not yet applied"* or the date and time)

Below the cards, an **Open the attendance record** button.

*Workflow* — the shared workflow tab: the definition, the steps, who is assigned to each, the
decision history, and any comments.

**Apply confirmation:** *"Apply this correction? Writes the requested times onto the daily
attendance record. Reported hours will change."*

### ▶ Walk it

This is the chapter to slow down on. Ten minutes, and it is the module's argument.

**1 — Open the register.** The default filter is *Awaiting approval*.

**2 — Switch *Show* to *All requests*.** One row: Kojo Ansah's missing check-out, **Applied**.

**3 — Set the scene before opening it.**

> *"Remember Kojo's day: clocked in at three minutes past eight, no clock-out, no hours. He left
> through the Estates yard gate after a site visit and never passed the main gate reader. In a
> paper system that is a note to HR and an argument at the end of the month. Here it is a
> record."*

**4 — Click the row.**

**5 — Read the *Request* card.**

> *"Request REG-dash-the-date-dash-one. Missing check-out. Raised on the eleventh. And the
> correction he is asking for: out at a quarter past five."*

**6 — Read his reason aloud — it is the demo's best line.**

> *"'Left through the Estates yard gate after a site visit and did not pass the main gate
> reader. Supervisor can confirm I was on site until 17:15.' That is the whole case for a system
> like this. The dispute is written down, by the person disputing, with the evidence they are
> offering."*

**7 — Read the *Outcome* card.**

> *"Approved by his Head of Development: 'Confirmed — he was with me at Sebrepor. Approved.'
> Approved on the eleventh. Applied on the eleventh."*

**8 — Switch to the *Workflow* tab.**

> *"And this is not a status field somebody typed. This ran on the corporation's approval
> engine — the same engine that approves a leave request, a requisition or a purchase order.
> The definition, the step, the person it was assigned to, the decision and the timestamp. If
> TDC wants attendance corrections to need two signatures instead of one, that is a change to
> the definition on this screen, not a change to the attendance module."*

**9 — Go back and raise a live one.** Press **+ New Request**.

**10 — Pick an employee and a date that has a record.** Watch the look-up box resolve and press
**Use this day**.

> *"It will only let me correct a day that exists. You cannot regularise your way into an
> attendance record that was never taken — that would be a different and much more serious
> thing."*

**11 — Choose *Wrong time entry*, set a corrected check-out, and write a real reason.**

**12 — 🔴 LIVE WRITE 8 — press *Submit for approval*.** You land on the new request, at
**Pending**, with the workflow already running.

> *"Raised, numbered and already in somebody's inbox — there is no separate 'submit' step to
> forget, because a correction that is not asking for anything has no reason to exist."*

**13 — Press *Approve*, add a comment, confirm.** The status moves to **Approved** and
**Apply correction** appears.

> *"Approved. And notice what has *not* happened: the attendance record has not changed."*

**14 — Now make the point that wins the room. Open the day in a second tab** (or use **Open the
attendance record**) and show the out-time still blank. Come back.

**15 — 🔴 LIVE WRITE 9 — press *Apply correction* and confirm.**

> *"And now it has. Two deliberate steps. Approving accepts the request; applying changes the
> reported hours. They are separate because an approved correction is still worth a second look
> before it moves a number that payroll is going to read — and because 'who approved it' and
> 'who applied it' are two different names on the record."*

**16 — Open the day again** and show the out-time now filled.

⚠ **Do not promise the hours will recalculate.** They will not — see the gaps. If the day had no
in-time either, the Worked column stays blank. Choose a day whose in-time is already there and
talk about the *time*, not the *hours*.

*Undo for LIVE WRITEs 8 and 9:* chapter 28. An applied regularization cannot be deleted through
the API at all.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Awaiting approval | `GET api/staff-attendance-regularizations/pending-approval` | `HR.Attendance.Read` |
| All / by status | `GET …/paged` · `GET …/status/{status}` | `HR.Attendance.Read` |
| Detail | `GET …/{id}` | **self-or-`HR.Attendance.Read`** |
| Day look-up | `GET api/staff-daily-attendance/employee/{employeeId}/date/{date}` | self-or-Read |
| **Create** | `POST api/staff-attendance-regularizations` | **signed-in only** — see A-52 |
| Amend | `PUT …/{id}` | self-or-`HR.Attendance.Write`, and only while *Pending* |
| **Approve / Reject** | `POST …/{id}/approve` · `…/reject` | **not permission-gated** — the workflow assignee's act |
| **Apply** | `POST …/{id}/apply` | `HR.Attendance.Write` |
| Withdraw | `DELETE …/{id}` | self-or-`HR.Attendance.Write`; refused once *Applied* |
| The Awaiting column | batched workflow entity summaries | |

Tables: `StaffAttendanceRegularizations`, `StaffDailyAttendances`.

**The number.** `REG-yyyyMMdd-00001`, and it is generated from the day's **maximum over all
rows including soft-deleted ones** — because a withdrawn request keeps its number in the unique
index, and the old count-based scheme regenerated it and 500'd the tenant's next request.

**Approve does not set the status.** The service relays the decision to the workflow engine and
`StaffAttendanceRegularizationWorkflowStatusAdapter` writes the outcome. Two identities are
carefully kept apart here: the **user** id goes to the engine (which identifies approvers by
login), and the **employee** id goes into `ApprovedById`, which is a real foreign key to
`Employees`. Writing one into the other is a constraint violation, and the service's own
comments say so.

**Apply** copies `RequestedCheckInTime` and `RequestedCheckOutTime` onto the day (each only if
present), sets the status to *Applied*, sets `IsApplied` and `AppliedDate`, and stamps the
applier.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-52 · Anyone signed in can raise a regularization for anyone.** `POST` takes the employee from the body and has **no self-or-permission check** — unlike the overtime create right beside it, which does. A clerk could raise a correction against the Managing Director's attendance. The record is auditable, so it is not silent, but the gate is missing | |
| **A-53 · Applying does not recompute the day's hours.** The corrected times are written and `ActualWorkHours` is left exactly as it was. So Kojo's day now reads in 08:03, out 17:15 — and Worked still reads an em dash. This is the most user-visible defect in the module and it is about four lines of code | |
| **A-54 · There is no edit screen.** `PUT` is implemented and refuses anything past *Pending*; no page calls it. A requester who mistypes a time must withdraw and re-raise — and there is no withdraw button either | |
| **A-55 · Supporting documents is free text, not an upload** — see the box in Screen 2 | |
| **A-56 · The employee cannot see their own regularizations in the portal.** `/me/attendance` has no requests section; the API's self-read exists and nothing calls it. The demo's own regularization was raised through the API as the new hire, not through a screen | |

---

## 19. `/hr/attendance/overtime` — asking, approving, then confirming

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Overtime Requests** ·
`/hr/attendance/overtime` (+ `/new`, `/[id]`) · as **hr.head**, with **head.dev** in window B ·
**9 minutes**

### 📖 What it is

> *"Overtime is the only part of attendance that turns directly into money, so it has three
> levels rather than one. Somebody asks before the work — that is the pre-approval. A manager
> agrees. And then, after the work, the supervisor confirms the hours that were actually put in
> — which is the figure payroll pays against, not the figure that was requested."*

### 👁 Screen 1 — the register

**Header:** *Overtime Requests* — *"Pre-approval of planned overtime, then confirmation of the
hours actually worked."*, back-link, **+ New Request**.

**Filter card** — **Show**: **Awaiting approval** *(default)* · **Awaiting supervisor
confirmation** · **All requests** · then the five statuses — Pending · Approved · Rejected ·
Completed · Cancelled.

> The second option is the one to point at. A request that has been approved *looks* finished,
> and the hours it will be paid on have not been recorded yet. That filter is the queue nobody
> thinks to build.

**Results card** — eight columns: **Number** (`OT-20260917-00001`) · **Employee** · **Overtime
date** · **Type** (Weekday · Weekend · Holiday · Emergency) · **Planned** (right-aligned,
`3h`) · **Actual** (right-aligned, em dash until confirmed) · **Status** · **Awaiting** (the
live workflow step and approver).

### 👁 Screen 2 — new (`…/overtime/new`)

**Card 1 — When**
- **Employee** *(picker, required)*
- **Overtime date** *(required)* · **Type** *(required, defaults to Weekday)*
- **Planned start** *(required, defaults to 17:00)* · **Planned end** *(required, defaults to
  20:00)*
- **Planned hours** *(required, 0.5–24, step 0.25, defaults to 3)*
- Helper: *"Calculated from the window above; adjust it if breaks make the paid time shorter."*

> **The hours follow the window automatically** — change either time and the hours recompute,
> **wrapping past midnight** so a 20:00–05:00 night stint reads 9 rather than −15. You can then
> override the number by hand.

**Card 2 — Why**
- **Purpose** *(required, ≤1000)*
- **Task details** *(≤2000)*

**Footer:** **Cancel** · **Submit for approval**.

### 👁 Screen 3 — the request (`…/overtime/[id]`)

**Header:** the request number, subtitle `Ama Boateng · 3h on 24 Sep 2026`, back-link, and on
the right: the status badge, the **Approve / Reject** workflow actions (while *Pending*), and —
when the status is *Approved* and it has not been confirmed — **Confirm hours worked**.

**Two tabs.**

*Overview* — four cards:
- **Planned overtime** — Employee · Overtime date · Type · Planned start · Planned end ·
  Planned hours · Raised on
- **Purpose** — the free text and the task details
- **Pre-approval** — Approved by · Approved on · Comments · Rejected on · Rejection reason
- **Hours actually worked** — Actual hours · Confirmed by · Confirmed on · Supervisor notes

Plus an **Open the attendance record** button, *shown only if the request is linked to a day*.

*Workflow* — the standard tab.

**Confirm dialog:** *"Confirm hours worked — this is the figure payroll pays against; it
replaces the planned hours."* It carries **Actual overtime hours** *(number, 0.5–24, step 0.25,
pre-filled with the planned figure)* and **Notes** *("Optional — explain any difference from the
plan")*. Confirming moves the status to **Completed**.

### ▶ Walk it

**1 — Open the register on *All requests*.** Two rows: one **Approved** (3 h, weekday), one
**Pending** (5 h, weekend).

> *"Two requests. One approved, one waiting. And a column most systems do not have: planned
> versus actual."*

**2 — Open the Pending one — the Saturday site inspection.**

> *"Five hours, Saturday, type Weekend — which matters, because a Saturday hour and a Tuesday
> hour are not worth the same. The purpose is written down: a site inspection at Sebrepor."*

**3 — Switch to window B, signed in as `head.dev`.** Open the same request.

> *"Now I am the line manager rather than HR."*

**4 — 🔴 LIVE WRITE 10 — press *Approve*, add a comment, confirm.**

> *"Approved. Before the work, not after — which is the entire point of a pre-approval. Nobody
> is negotiating overtime at the end of the month."*

⚠ **If *Approve* is greyed out for `head.dev`**, the engine has not assigned this step to them — the seeded definition routes to the **Manager**, **HR** and **TenantAdmin** roles, and `head.dev` holds Manager only if the persona seeding gave it to them. The button will name the pending approver beside it. Do not fight it: say the line below, switch back to window A and approve as `hr.head`.

> *"And the button tells me who it is waiting for — it does not let me guess. Approval here is the engine's decision about who I am, not the screen's."*

**5 — Back in window A as `hr.head`, refresh the register and switch *Show* to *Awaiting
supervisor confirmation*.** The request you just approved is there.

> *"And here is the queue I promised you. It is approved, so it looks done — and the number that
> will actually be paid has not been recorded. That is the gap every manual overtime process
> falls into, and it is a filter here."*

**6 — Open it and press *Confirm hours worked*.** The dialog opens pre-filled with 5.

**7 — Change it to 4 and write a note** — *"Finished earlier than planned; the drainage survey
was not needed."*

**8 — 🔴 LIVE WRITE 11 — confirm.** Status moves to **Completed** and the **Actual** column
fills with 4.

> *"Planned five, worked four, paid four — with a supervisor's name and a written reason for the
> difference. That is the sentence a payroll manager needs, and it is on the record rather than
> in somebody's memory."*

**9 — Say the boundary out loud.** Somebody will ask what happens next:

> *"And this is where attendance stops and payroll begins. This module's job is to establish
> that four hours of authorised weekend overtime were worked by this person on that date, with
> approvals and an audit trail. What a weekend hour is worth is a payroll question, and it lives
> in the payroll module with the rest of the pay rules."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Awaiting approval | `GET api/staff-overtime-requests/pending-approval` | `HR.Attendance.Read` |
| **Awaiting confirmation** | `GET …/pending-supervisor-confirmation` | `HR.Attendance.Read` |
| All / by status / by range | `GET …/paged` · `…/status/{status}` · `…/range?from&to` | `HR.Attendance.Read` |
| Detail | `GET …/{id}` | self-or-Read |
| **Create** | `POST api/staff-overtime-requests` | **self-or-`HR.Attendance.Write`** — filing for somebody else is a desk act |
| Amend | `PUT …/{id}` | self-or-Write, *Pending* only |
| Approve / Reject | `POST …/{id}/approve` · `…/reject` | not permission-gated — the assignee's act |
| **Confirm actual hours** | `POST …/{id}/confirm` | `HR.Attendance.Write`, *Approved* only |
| Withdraw | `DELETE …/{id}` | self-or-Write, *Pending* only |

Table: `StaffOvertimeRequests`. Number format `OT-yyyyMMdd-00001`, generated from the day's
maximum including soft-deleted rows — the original collision that taught the whole codebase this
lesson was found here, by a create-then-withdraw probe.

**Three lifecycle rules the service enforces:** only a *Pending* request can be amended, only a
*Pending* one can be decided, and only an *Approved* one can have hours confirmed. Confirming
sets `SupervisorConfirmedById` from the token and moves the status to *Completed* in one step.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-32 (again) · No eligibility check at submission.** The entity's own documentation says a request should be refused when the employee's position is not eligible for overtime, or is exempt. Nothing checks. The General Manager's recorded exemption does not stop a request being raised and approved for them | |
| **A-57 · No ceiling is applied.** `MaxOvertimeHoursPerDay` / `PerWeek` on the schedule and on the position policy are read by nothing. A 24-hour request is accepted | |
| **A-58 · Confirmed overtime never reaches the day, or the month.** `AttendanceId` on the request is never set, so the *Open the attendance record* button never appears; the day's `IsOvertime`, `OvertimeHours` and `OvertimeApproved` are never written; and the monthly summary sums the **day's** overtime hours — so a confirmed, approved overtime request contributes **zero** to the month it belongs to. The only overtime that reaches a summary is overtime typed onto a day by hand. **This is the most consequential gap in the module**, because it is the one that costs money | |
| **A-59 · `Cancelled` is unreachable.** The status exists and no endpoint sets it. A request is withdrawn by deletion, which is a soft delete and disappears rather than showing as cancelled | |
| **A-60 · There is no portal screen for overtime.** An employee cannot raise their own overtime request; `/me/attendance` has no form. The demo's two requests were created through the API as the `staff` persona | |

---

## 20. `/hr/attendance/remote-work` — working off-site

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Remote Work** ·
`/hr/attendance/remote-work` (+ `/new`, `/[id]`) · as **hr.head** · **5 minutes**

### 📖 What it is

> *"A dated request to work somewhere other than the office, approved by the same engine as the
> rest. It exists so that a day marked 'remote' on the attendance register has a document behind
> it rather than being an assertion — and so that the equipment question gets asked before
> somebody spends a fortnight on a laptop with no VPN."*

### 👁 Screen 1 — the register

**Header:** *Remote Work Requests* — *"Work-from-home and off-site requests, approved through the
workflow."*, back-link, **+ New Request**.

**Filter card** — **Show**: **Awaiting approval** *(default)* · **All requests** · then Pending ·
Approved · Rejected · Cancelled.

**Results card** — eight columns: **Number** (`RWR-…`) · **Employee** · **From** · **To** ·
**Days** (right-aligned) · **Raised** · **Status** · **Awaiting**.

### 👁 Screen 2 — new (`…/remote-work/new`)

One card, six fields:
- **Employee** *(picker, required)*
- **From** *(required)* · **To** *(required — refused if earlier than From)*
- **Remote location** *(≤500, "e.g. Home — Accra")*
- **Reason** *(required, ≤1000)*
- **Equipment confirmed** switch — *"The employee has confirmed a suitable setup: connectivity,
  workspace and any kit they need."*

**Footer:** **Cancel** · **Submit for approval**.

### 👁 Screen 3 — the request (`…/remote-work/[id]`)

**Header:** the request number, subtitle `Ama Boateng · 22 Sep 2026 – 23 Sep 2026 (2 days)`,
back-link, status badge and the **Approve / Reject** workflow actions while *Pending*.

**Two tabs.** *Overview* — three cards: **Request** (Employee · From · To · **Working days** ·
Remote location · Equipment confirmed · Raised on), **Reason**, and **Outcome** (Approved by ·
Approved on · Comments · Rejected on · Rejection reason). *Workflow* — the standard tab.

There is no extra action beyond approve and reject — no apply, no confirm.

### ▶ Walk it

**1 — Open the register** on the default *Awaiting approval*. One row: 2 days at Sakumono,
Pending.

**2 — Open it.**

> *"Two days, from a Tuesday to a Wednesday, at Sakumono. The reason is written down —
> preparing the Q4 progress report, quieter at home. And the field that stops this being a
> conversation in a corridor: equipment confirmed. Yes."*

**3 — Point at *Working days*.**

> *"Two days."*

⚠ **The label says "Working days" and the number is calendar days.** A Friday-to-Monday request
reads **4**, not 2. Do not build a say-line on it; if someone spots it, it is finding **A-61**
and it is a one-line fix. Pick a Mon–Tue or Tue–Wed request to demonstrate and the distinction
never comes up.

**4 — 🔴 LIVE WRITE 12 — press *Approve*, comment, confirm.** Status moves to **Approved**. *(As `hr.head`, in window A — the same role routing as chapter 19.)*

**5 — Say the honest sentence about what approval does.**

> *"Approved. And I will be straight about what that does today: it is a decision on a record,
> with a name and a date. What it does not yet do is walk forward across those two days and mark
> them 'remote' on the attendance register — that link exists in the data model and is not yet
> wired. So today the register shows remote days because somebody recorded them; tomorrow it
> shows them because this request was approved."*

**6 — Show the register's remote days anyway** — `/hr/attendance/daily`, flag chip **Remote** on.

> *"And here is what that looks like when it is there: a remote day on the register, with the
> location the person worked from."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Awaiting approval | `GET api/remote-work-requests/pending-approval` | `HR.Attendance.Read` |
| All / by status / by range | `GET …/paged` · `…/status/{status}` · `…/range?from&to` | `HR.Attendance.Read` |
| Detail | `GET …/{id}` | self-or-Read |
| **Create** | `POST api/remote-work-requests` | **signed-in only** — same gap as A-52 |
| Amend | `PUT …/{id}` | self-or-Write, *Pending* only |
| Approve / Reject | `POST …/{id}/approve` · `…/reject` | not permission-gated — the assignee's act |
| Withdraw | `DELETE …/{id}` | self-or-Write |

Table: `RemoteWorkRequests`. Number generated from the day's maximum including soft-deleted
rows — this generator is the one whose fix note explains the whole pattern: a unique index with
no `IsDeleted` filter plus a soft delete meant a count-based scheme reissued a number the
deleted row still held, and every later create in that tenant failed with a bare 500 naming
neither the column nor the constraint.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-61 · "Working days" counts calendar days.** `RequestedDays = |EndDate − StartDate| + 1`, with no weekend or holiday exclusion, and the entity's own comment says it should exclude both | |
| **A-62 · An approved request marks no days.** `StaffDailyAttendance.RemoteWorkRequestId` exists, the navigation collection exists, and nothing writes it. The remote days on the register were typed, not derived | |
| **A-52 (again) · Anyone signed in can raise one for anyone** | |
| **A-63 · `Cancelled` is unreachable**, like overtime's | |
| **A-64 · No portal screen.** An employee cannot raise their own remote-work request | |
| **A-65 · The amend endpoint reads a field the create endpoint does not write.** `UpdateRemoteWorkRequestDto` carries `WorkLocation` where the create carries `RemoteLocation`; the mapper handles it, but the two DTOs disagree on the name of the same column | |

---

## 21. `/hr/attendance/summaries` — the month, and the lock

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Monthly Summaries** · `/hr/attendance/summaries` ·
as **hr.head** · **7 minutes**

### 📖 What it is

> *"The month, per person — and the only figures payroll is ever shown. Days present, absent, on
> leave and late; hours worked and hours of overtime; attendance and punctuality as percentages.
> Two things can happen to a row: it can be rebuilt from the days underneath it, or it can be
> locked. Once it is locked the system itself refuses to change it."*

### 👁 On the page

**Header:** *Monthly Attendance Summaries* — *"Per-employee roll-ups of days, hours and
punctuality — the figures payroll reads."*, back-link.

**Period card** — three dropdowns:
- **Year** — next year, this year, and the two before
- **Month** — January … December
- **Show** — **All summaries** · **Not yet finalised**

**Results card** — *"N summaries"*, eleven columns:

| Column | Shows |
|---|---|
| **Employee** | name over staff number |
| **Present** · **Absent** · **Leave** · **Late days** | counts, right-aligned |
| **Worked** · **Overtime** | `152h 30m`, right-aligned |
| **Attendance** · **Punctuality** | `94.1%`, right-aligned |
| **State** | **Finalised** (solid) or **Open** (grey) |
| ⋯ | **Recalculate** and **Finalise** — *both disabled once the row is finalised* |

**Recalculate confirmation** *(destructive styling)*: *"Rebuilds the totals from the daily
attendance records. Any manual corrections to the summary will be lost."*

**Finalise confirmation**: *"Locks the figures so payroll can export them. Corrections after
this need the summary reopening."*

Empty state: *"No summaries for this period — summaries are built from daily attendance.
Recalculate once the month has data."*

> ⚠ **There is no way to create a summary for somebody who has none.** Recalculate lives on an
> existing row, and the endpoint that would create one takes an employee, a year and a month
> that the screen has no form for. On a live tenant, a new joiner's first month appears only if
> somebody calls the API. This is finding **A-66**.

### ▶ Walk it

**1 — Open the screen.** It lands on this month.

**2 — Switch *Month* to last month**, which the demo populated.

> *"Last month, per person. Seven people in the demo; in the live system it is everybody."*

**3 — Read one row across.**

> *"Days present, days absent, days on leave, days late. Hours worked, hours of overtime.
> Attendance and punctuality as percentages. That is the whole of what payroll is shown — not
> sixty columns of daily detail, a dozen numbers per person per month."*

**4 — Find the **Finalised** row** (the demo finalises one) and stop there.

> *"And here is the lock. This row is finalised. Watch what the system will not let me do."*

**5 — Click ⋯ on the finalised row.** **Recalculate** and **Finalise** are both greyed out.

> *"Both actions gone — and not just hidden on the screen. If you called the API directly it
> would refuse: 'A finalized monthly summary cannot be recalculated.' It also cannot be edited
> and it cannot be deleted. That is the answer to the question every finance director asks about
> an attendance system: how do I know this will not change after I have paid it."*

**6 — Now finalise one yourself. Click ⋯ on an open row → *Finalise*** and read the
confirmation aloud.

**7 — 🔴 LIVE WRITE 13 — confirm.** The **State** flips to **Finalised** and the menu closes
behind it.

> *"Locked, with the date and the name of the person who locked it on the record."*

**8 — ⚠ Do *not* press Recalculate on a row you are talking about.** If you want to demonstrate
it, say the sentence rather than pressing the button:

> *"The other action rebuilds a month from the days beneath it — which is what you do when a
> regularisation has changed a day after the roll-up was taken. It is deliberately destructive:
> it discards anything typed onto the summary and goes back to source. And it is refused once
> the month is locked, which is the order you want those two rules in."*

If you *do* press it (on a row you do not mind spoiling), be ready: **Worked**, **Attendance**
and **Punctuality** will go to `—`, `0.0%`, `0.0%`. That is finding **A-3**, and the honest line
is short:

> *"And there is the one thing that is not finished: the rebuild fills the day counts and the
> overtime and does not yet fill the hours and the percentages. The formulas are documented, the
> columns are there, the screen reads them — the routine is four lines short."*

*Undo for LIVE WRITE 13:* chapter 28 — there is no "reopen" button, by design.

**9 — Close on the boundary.**

> *"And this row is the hand-off. Attendance's job ends here: this person, this month, these
> days and these hours, locked. What happens to it next is payroll's."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The grid | `GET api/staff-monthly-attendance-summaries/year/{year}/month/{month}` | `HR.Attendance.Read` |
| *Not yet finalised* | `GET …/unfinalized?year&month` | `HR.Attendance.Read` |
| **Recalculate** | `POST …/recalculate?employeeId&year&month` *(no body)* | `HR.Attendance.Write` |
| **Finalise** | `POST …/{id}/finalize` | `HR.Attendance.Write` |
| *(edit — no screen)* | `PUT …/{id}` | `HR.Attendance.Write`, refused once finalised |
| Delete | `DELETE …/{id}` | **`HR.Attendance.Admin`**, refused once finalised |

Table: `StaffMonthlyAttendanceSummaries`, built from `StaffDailyAttendances`.

**What Recalculate actually sets** — six fields, from the employee's days in that month:

```
    DaysPresent        = days at status Present
    DaysAbsent         = days at status Absent
    DaysOnLeave        = days at status OnLeave
    TotalOvertimeHours = Σ day.OvertimeHours
    NumberOfLateDays   = days with IsLate
    TotalLateMinutes   = Σ day.LateMinutes
```

**What it does not set** — sixteen fields, including `TotalWorkingDays`, `TotalWorkedHours`,
`AttendancePercentage`, `PunctualityPercentage`, `DaysLate`, `DaysRemoteWork`, `PublicHolidays`,
`Weekends`, `DaysHalfDay`, `TotalScheduledHours`, `TotalUndertimeHours`, `TotalBreakHours`,
`TotalEarlyDepartureMinutes`, `NumberOfEarlyDepartureDays` and `PayPeriodId`.

`FinalizedById` is the finaliser's **employee** id from the token.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-3 · Recalculate fills six of twenty-two fields** — see above and Rule 3 | |
| **A-66 · A summary cannot be created from any screen** — see the box in *On the page* | |
| **A-67 · Nothing generates summaries on a schedule.** There is no month-end job. Every summary on the demo database was produced by the seeding script calling recalculate in a loop | |
| **A-68 · `DaysLate` and `NumberOfLateDays` are two columns for one fact, and each screen reads a different one.** Recalculate sets `NumberOfLateDays`; this screen reads it and is right. The **employee profile's Attendance tab** reads `DaysLate` — which recalculate never sets — so the same month shows the correct late count here and **0** on the employee's own record | |
| **A-69 · Nothing attaches a summary to a pay period.** `PayPeriodId` is on the entity, on the DTO, in the API's `by-pay-period` read and in the payroll export's counting query — and no code path writes it. This is what makes chapter 24 report zero | |
| **A-70 · No export.** The figures payroll reads can only be read on a screen | |

---

## 22. `/hr/attendance/alerts` — what the rules caught

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Alerts** · `/hr/attendance/alerts` ·
as **hr.head** · **5 minutes**

### 📖 What it is

> *"The rules from the setup screen, having fired. Each alert names the employee, the rule, what
> it caught and how serious it is — and it stays open until a human being says they have seen
> it. Acknowledging is deliberately not the same as fixing."*

### 👁 On the page

**Header:** *Attendance Alerts* — *"Absence, lateness, missing-punch and overtime alerts raised
by the alert rules."*, back-link. When rows are selected, an **Acknowledge N** button appears
top right.

**Filter card** — **Show**: **Unacknowledged** *(default)* · **All alerts** · then
*Severity: Info / Warning / Critical* and *Status: Active / Acknowledged / Resolved /
Dismissed*.

**Results card** — eight columns, the first a checkbox:

| Column | Shows |
|---|---|
| ☑ | select — **only enabled on rows at status *Active***; the header checkbox selects all selectable rows |
| **Employee** | name |
| **Rule** | the rule's name |
| **Trigger** | *Chronic lateness*, *Missing punch*, … |
| **Severity** | badge — Critical red, Warning grey, Info outline |
| **Raised** | date and time |
| **Detail** | the trigger description — *"Rule 'Chronic lateness — 30 minutes or more' triggered for attendance on 11 Sep 2026."* |
| **Status** | Active · Acknowledged · Resolved · Dismissed |

**Acknowledge dialog:** *"Acknowledge N alerts? Acknowledging records that someone has seen the
alert. It does not resolve the underlying attendance issue."* with an optional **Comments** box.
The result toast reports how many were acknowledged.

> Bulk is the default shape here on purpose: a rule firing across a department produces one row
> per employee, and clearing them one at a time is not a job.

### ▶ Walk it

**1 — Open the screen** on the default *Unacknowledged*.

> *"The rules, having fired. Each one names the person, the rule, what it caught and how serious
> the corporation has decided that is."*

**2 — Read a Critical row aloud.**

> *"Critical — unauthorised absence. Somebody marked absent with no leave application against
> the day. That is not an attendance problem, it is the start of a disciplinary one, and the
> corporation has said it wants to be interrupted about it."*

**3 — Switch *Show* to *All alerts*** and find the acknowledged one.

> *"And one that has been acknowledged: 'Seen — the employee has been spoken to and the pattern
> is being watched.' Note the wording on the button, because it is careful: acknowledging
> records that a human being has seen this. It does not claim the problem is solved."*

**4 — Switch back to *Unacknowledged*, tick two or three rows** using the checkboxes.

**5 — 🔴 LIVE WRITE 14 — press *Acknowledge N*, add a comment, confirm.** The rows move to
**Acknowledged** and drop out of the default filter.

> *"Three at once, with one comment. A rule that fires across a department produces a row per
> person, and an HR officer should be able to work that in one action."*

*Undo:* chapter 28 (SQL — there is no un-acknowledge).

**6 — Now say the honest sentence, which you set up back in chapter 11.**

> *"Two things about how this runs today, and I would rather you heard them from me. First, the
> rules are evaluated on demand — there is an endpoint that runs every rule against a day, and
> what you are looking at came from it. The nightly job that calls it for yesterday's attendance
> is not yet built, so these do not appear on their own. Second, the evaluation window — 'three
> late arrivals in thirty days' — is captured on every rule and not yet applied; today each rule
> is judged on a single day. The rules, the thresholds, the severities and the whole
> acknowledge-and-resolve lifecycle are here. The scheduler and the window are the next
> increment, and they are days of work, not months."*

**7 — Point at the *Status* column one more time.**

> *"Four states: active, acknowledged, resolved, dismissed. The first two are wired to this
> screen. Resolving and dismissing are on the record and not yet on a button."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Unacknowledged | `GET api/staff-attendance-alerts/unacknowledged` | `HR.Attendance.Read` |
| All | `GET …/paged?pageNumber=1&pageSize=100` | `HR.Attendance.Read` |
| By severity | `GET …/severity/{severity}` | `HR.Attendance.Read` |
| By status | *(no endpoint — the screen fetches 200 paged rows and filters in the browser)* | |
| Acknowledge one | `POST …/{id}/acknowledge` | `HR.Attendance.Write` |
| **Bulk acknowledge** | `POST …/bulk-acknowledge` | `HR.Attendance.Write` |
| *(evaluate)* | `POST …/evaluate/{dailyAttendanceId}` | `HR.Attendance.Write` — **called by nothing** |
| Delete | `DELETE …/{id}` | **`HR.Attendance.Admin`** |

Tables: `StaffAttendanceAlerts`, `StaffAttendanceAlertRules`. `AcknowledgedById` is an
`Employees` foreign key from the token.

### ⚠ Known gaps

All of A-24 … A-31 from chapter 11 land on this screen. The two extra ones here:

| Gap | |
|---|---|
| **A-71 · There is no Resolve and no Dismiss.** Both statuses exist, both have a full set of columns on the entity (`ResolvedById`, `ResolvedDate`, `ResolutionNotes`, `DismissedById`, `DismissedDate`, `DismissalReason`), and **no endpoint sets either**. An alert's life ends at *Acknowledged* | |
| **A-72 · Status filtering is done in the browser** over the first 200 rows, because no by-status endpoint exists. On a live tenant with thousands of alerts, the status filters quietly lie | |

---

## 23. `/hr/attendance/imports` — a month of clockings at once

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Bulk Imports** ·
`/hr/attendance/imports` (+ `/new`, `/[id]`) · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Attendance data arrives in batches: a month of clockings exported from a reader, a
> contractor's timesheet, a spreadsheet from an outstation. A batch is **staged** first and
> **processed** second — so it can be inspected, and its bad rows found, before anything is
> written to attendance."*

### 🚫 **READ RULE 2 BEFORE THIS CHAPTER.** Do not run a new import live.

### 👁 Screen 1 — the register

**Header:** *Bulk Attendance Imports* — *"Batch loads of attendance data, staged for review
before they are applied."*, back-link, **+ New import**.

**Results card** — nine columns: **Reference** (`ATT-IMP-20260810-00001`) · **Source** (CSV ·
Excel · API · Biometric device · Manual entry) · **File** · **Imported** · **By** · **Rows** ·
**OK** · **Failed** *(red when non-zero)* · **Status** (Pending · Processing · Completed ·
Failed · Partial success). Rows are clickable.

Empty state: *"No imports yet — stage a batch to load attendance from a spreadsheet or device
extract."*

### 👁 Screen 2 — new (`…/imports/new`)

**Card 1 — Source.** **Source type** *(dropdown, defaults to CSV)* · **CSV file** *(a file
picker — reads the file in the browser and fills the textarea below)* · **Notes**.

**Card 2 — Rows.** A monospaced **Paste rows** textarea, placeholder
`employeeNumber, date (YYYY-MM-DD), checkIn (HH:mm), checkOut (HH:mm)` /
`EMP001,2026-08-03,08:05,17:10`, with the helper *"A header row is detected and skipped. Each
line is also sent verbatim, so the server can re-parse anything this preview misreads."*

Below it, once anything parses: *"30 rows parsed — showing the first 10."* and a preview table —
**#**, **Employee ref**, **Date** *(in red and reading `unparsed` if it is not `YYYY-MM-DD`)*,
**In**, **Out**.

**Footer:** **Cancel** · **Stage N rows** *(disabled until something parses)*.

> 🚫 **This screen stages rows that will never be applied.** The preview reads the employee
> reference from your file and the payload sent to the server carries `employeeId: null` for
> every row. The server writes a day only when that field is a real GUID, and then marks the row
> successful regardless. You will get *"Staged"*, then *"Import Completed — 30 of 30 rows
> applied"*, and nothing in `StaffDailyAttendances`. **Finding A-2. Do not run this live.**

### 👁 Screen 3 — the batch (`…/imports/[id]`)

**Header:** the reference, subtitle `CSV · TDC-BIO-001_2026-08_clockings.csv · staged 10 Aug 2026
09:14 by Akosua Mensah`, back-link, the status badge, and — **only while the status is
*Pending*** — a **Process batch** button.

**Three stat cards:** **Total rows** · **Applied** · **Failed** *(red when non-zero)*.

**Error summary card** — rendered only when the batch has one; the server's JSON summary of
validation errors, in red.

**Rows card** — a **All rows / Failed only** dropdown above a table: **#** · **Employee** ·
**Date** · **In** · **Out** · **Result** (`OK` outline / `Failed` red) · **Error**.

**Process confirmation:** *"Applies N staged rows to attendance. Rows that fail validation are
reported but the rest still go through."*

### ▶ Walk it

**1 — Open the register.** One row: last month's clock file from the main gate, 30 rows,
30 OK, 0 failed, **Completed**.

> *"One batch. Last month's clocking export from the main gate reader — thirty rows, thirty
> applied, none failed, and the file name it came from is on the record."*

**2 — Click the row.**

**3 — Read the three stat cards, then the rows table.**

> *"Thirty rows, and each one is kept individually: the employee it matched, the date, the times
> — and, if it had failed, the reason it failed, on that row rather than in a log file somebody
> has to go and find."*

**4 — Switch the dropdown to *Failed only*.**

> *"'No failed rows — every row in this batch was accepted.' Which is the point of the screen:
> when it is not that, this is where an HR officer goes, and they get a line per bad row with
> the error on it."*

**5 — Explain staging, which is the design decision worth naming.**

> *"And note the two-step shape. A batch is staged first — it sits here, inert, with every row
> visible and nothing written. Then somebody presses Process. Until they do, the batch has
> changed nothing. That is what lets you take a file from a contractor, look at it, find the
> four rows with a bad staff number, and decide — rather than discovering it a week later in a
> monthly summary."*

**6 — Point at *Source type* on the register.**

> *"Five sources: CSV, Excel, an API, a biometric device extract, or manual entry. The record
> keeps which, so a year later you can tell a hand-keyed month from a machine-read one."*

**7 — 🚫 DO NOT press *+ New import*.** If asked to demonstrate a load live, say:

> *"The import path for a real go-live is the device export, matched on staff number — and that
> matching is the piece we are finishing. What is complete is everything around it: the staging,
> the per-row audit, the error reporting and the two-step apply."*

If you want to *show* the form without running it, open it, paste two lines, let the preview
render, and press **Cancel**. The preview is honest — it is the payload that is not.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-bulk-attendance-imports/paged` | `HR.Attendance.Read` |
| Batch | `GET …/{id}` | `HR.Attendance.Read` |
| Rows / failed rows | `GET …/{id}/rows` · `…/{id}/rows/failed` | `HR.Attendance.Read` |
| **Stage** | `POST api/staff-bulk-attendance-imports` | `HR.Attendance.Write` |
| **Process** | `POST …/{id}/process` | `HR.Attendance.Write` |
| Delete | `DELETE …/{id}` | **`HR.Attendance.Admin`**, refused while *Processing* |

Tables: `StaffBulkAttendanceImports`, `StaffBulkAttendanceImportRows`, `StaffDailyAttendances`.
Reference format `ATT-IMP-yyyyMMdd-00001`, generated from the day's maximum including
soft-deleted rows.

**What Process does per row:** looks for an existing day for that employee and date; if there is
none **and the row carries an employee id**, creates one at status *Present* with the row's in
and out times; marks the row successful. The status ends **Completed** when nothing failed and
**Partial success** otherwise.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-2 · The screen's own CSV path resolves no employee, so no rows are written** — Rule 2 | |
| **A-73 · A row with no employee id is reported as a success.** The `if` that skips it has no `else`, so the row is marked `IsSuccess = true` with no error message. Nothing anywhere says the row did nothing | |
| **A-74 · A row whose day already exists is also a silent success.** The batch cannot update an existing day, and does not say it skipped one | |
| **A-75 · `CreatedAttendanceId` is never set.** The column exists to link a row to the day it made, so you could trace an imported day back to its source line. It is left null | |
| **A-76 · `ErrorSummary` is never written.** The card on the detail page will never render | |
| **A-77 · No hours are derived.** An imported day carries in and out and a null `ActualWorkHours` — the derivation lives only in the punch-processing path | |
| **A-78 · There is no real file upload.** The API takes rows as JSON; the file picker reads the file in the browser. So a 20 000-row month is a 20 000-element JSON body | |

---

## 24. `/hr/attendance/payroll-exports` — the hand-off

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Payroll Exports** · `/hr/attendance/payroll-exports` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"The audit trail of attendance being handed to payroll. Not a file — a record that on this
> date, this person sent this many employees' figures for this pay period to this system, and
> this was the outcome. It answers 'was August sent, and by whom' without anybody opening an
> email folder."*

### 🚫 **READ RULE 4 BEFORE THIS CHAPTER.**

### 👁 On the page

**Header:** *Payroll Exports* — *"Hand-offs of finalised attendance to payroll, and the outcome
of each run."*, back-link, **New export** button.

**Results card** — eight columns: **Reference** (`PAY-EXP-20260917-00001`) · **Pay period** ·
**Exported** · **By** · **Target** · **Employees** · **Records** · **Status** (Pending · In
progress · Completed · Failed · Partial success).

Empty state: *"No exports yet — close a pay period and finalise its summaries, then run an
export."*

**New export dialog:** *"Export attendance to payroll — only closed pay periods can be exported.
Finalise the summaries first, or the run will report gaps."*
- **Pay period** — a dropdown of **closed** periods only, showing `August 2026 (01 Aug 2026 –
  31 Aug 2026)`. If there are none: *"No closed periods available"*.
- **Target system** — free text, *"Optional — e.g. the payroll system this batch went to"*.
  Defaults server-side to `Payroll`.

The result toast reports *"Exported — N records for N employees"*, or the status and the error
detail if it did not complete.

### ▶ Walk it

The honest version of this chapter is stronger than the clicking version. Do it this way.

**1 — Open the screen.** It is empty.

> *"Empty — and that is the correct state for a demo database, because we do not fabricate a
> hand-off that never happened. Let me show you what it is for, and then show you the shape of
> the record."*

**2 — Press *New export*** and open the **Pay period** dropdown.

> *"The first rule is visible before you press anything: only **closed** pay periods are in this
> list. You cannot ship a month that HR is still working on. And the API enforces it, not the
> dropdown — an export against an open period is refused outright."*

**3 — Choose last month** *(which you closed in chapter 8, LIVE WRITE 1)*, type a target system
name, and **read the dialog's warning aloud**.

> *"'Finalise the summaries first, or the run will report gaps.' Which is exactly the
> relationship: the export counts the locked monthly summaries for that period. Locking is the
> gate; this is the delivery note."*

**4 — Decide here, based on your prep:**

**If you ran § 2.6's SQL** — 🔴 **LIVE WRITE 15 — press *Run export*.** A row appears with a
reference, today's date and time, your name, the target system, the employee and record counts,
and **Completed**.

> *"And that is the hand-off recorded. A reference, a period, a date, a person, a destination
> and a count — which is what an auditor asks for when they want to know that August's
> attendance reached payroll and who sent it."*

**If you did not** — **press Cancel** and say this instead:

> *"I will not run it on this database, because it would report zero — and I would rather tell
> you why than have you wonder. The export counts the monthly summaries attached to the pay
> period, and attaching a summary to a period is the one link in this chain that is not yet
> written. The lock works. The period gating works. The audit record works. The join between a
> locked month and a pay period is the piece we are finishing, and it is one assignment in the
> recalculation routine."*

That is a better answer than a zero on screen, and it is true.

**5 — Close on the boundary, and mean it.**

> *"And notice what this screen is *not*. It is not a payroll calculation, it is not a payslip,
> and it does not decide what an overtime hour is worth. Payroll is a separate module with its
> own owner. Attendance's contract is narrow and it is deliberate: these people, this period,
> these days and these hours, locked, with an audit trail of when it was handed over."*

*Undo for LIVE WRITE 15:* chapter 28.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-attendance-payroll-exports/paged` | `HR.Attendance.Read` |
| Closed-period dropdown | `GET api/pay-periods/status/Closed` | signed-in |
| **Run export** | `POST api/staff-attendance-payroll-exports/export` | `HR.Attendance.Write` |
| Delete | `DELETE …/{id}` | **`HR.Attendance.Admin`** |

Tables: `StaffAttendancePayrollExports`, `PayPeriods`, `StaffMonthlyAttendanceSummaries`.

**What the export does:** loads the pay period; **refuses anything that is not `Closed`**; counts
the monthly summaries whose `PayPeriodId` is that period; writes one export row with a
reference, the date, the actor's employee id, the target system, `TotalRecords` = that count and
status **Completed**.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-4 / A-69 · The count is always zero** because nothing sets `PayPeriodId` on a summary | |
| **A-79 · `TotalEmployees` is never set.** The column is on the entity, on the DTO and is a column on this screen; the export writes only `TotalRecords`. The **Employees** column reads 0 on every row for ever | |
| **A-80 · No payload is produced.** `ExportPayloadSnapshot` is designed to hold the serialised batch for audit and is never written. Nothing is sent anywhere, and there is no file to download — this is a record *that* a hand-off happened, not the hand-off | |
| **A-81 · The period is not marked exported.** `PayPeriod.ExportedDate`, `ExportedById` and the `ExportedToPayroll` status are never set, so the Pay Periods screen's **Exported** column stays blank and the period never leaves *Closed* (A-17) | |
| **A-82 · The status is hard-coded to Completed.** `Failed` and `PartialSuccess` are unreachable; `ErrorDetails` is never written. The screen handles all three outcomes | |
| **A-83 · The reference generator counts live rows.** Unlike the four other generators in this module it uses a count rather than the maximum, so deleting an export can reissue a reference — the exact collision the others were all fixed for | |

---

## 25. `/hr/attendance/biometrics` — teaching the machines who people are

### 📍 Where you are

**Sidebar:** … → Attendance & Time → **Biometrics** · `/hr/attendance/biometrics` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"A fingerprint reader on a gate does not know anybody's name. It matches a template, and this
> is the register of which template belongs to whom — with the quality of the capture, the
> standard it was encoded in, the device that took it, and when. And, more importantly, the
> record of revoking one."*

### 👁 On the page

**Header:** *Employee Biometrics* — *"Fingerprint, face and other templates that let devices
recognise an employee."*, back-link.

**Employee card** — one searchable picker. **Nothing else renders until you choose somebody** —
templates are listed per employee. Before a choice: *"Choose an employee — templates are
enrolled and listed per employee."*

**Templates panel** — **+ Add template** above a table:

| Column | Shows |
|---|---|
| **Type** | Fingerprint · Face · Iris · Palm · Vein · Retina |
| **Position** | the finger, or the free-text body part |
| **Quality** | `88%`, right-aligned |
| **Enrolled** | the date |
| **Status** | Active / Inactive |

Row ⋯ menu: **Revoke** *(destructive styling; shown only on active rows)* and 🚫 **Remove
template**. **There is no Edit** — deliberately: a template is re-enrolled, not amended.

**Dialog** — hint *"Capture the template on the enrolment device, then paste the encoded value
here."*:
- **Biometric type** *(required)*
- **Finger** *(a dropdown of the ten finger positions — shown only when the type is
  Fingerprint)*, otherwise **Body part** *(free text, "e.g. Left iris")*
- **Template data** *(textarea, required, ≤2000 — "The encoded template from the enrolment
  device")*
- **Template format** *("e.g. ISO 19794-2")* · **Quality score (0–100)**
- **Enrolment device ID** · **Device model**

**Revoke confirmation:** *"Revoke this template? Deactivates it so devices stop matching against
it, while keeping the enrolment history."*

### ▶ Walk it

**1 — Open the screen** and read the empty state.

> *"Per employee, because that is the only way anyone ever asks the question: has this person
> got a fingerprint on the gate."*

**2 — Pick the new hire, Kojo Ansah.** One row: Fingerprint, Right index, 88%, enrolled, Active.

> *"One template. Right index finger. Quality eighty-eight out of a hundred — and that number
> matters operationally, because a low-quality capture is why somebody stands at a turnstile
> pressing their thumb four times every morning. Below a threshold you re-enrol them rather than
> letting them fight the gate."*

**3 — Click ⋯ → *Add template*** to open the dialog, and walk it without saving.

> *"Type, finger, the encoded template from the reader, the standard it is in — ISO 19794-2, so
> it is portable between vendors rather than locked to whoever supplied the turnstile — and the
> device that took it."*

Press **Cancel**.

**4 — Say the sentence about the data itself. It matters and people ask.**

> *"And to be clear about what is stored: this is a mathematical template, not an image. It is
> written once at enrolment and is never read back into this screen — the field is write-only in
> practice. Nobody with a login can retrieve somebody's fingerprint from this system."*

**5 — Point at *Revoke* in the ⋯ menu** without pressing it.

> *"And the action that matters for leavers. Revoke deactivates the template — the gate stops
> matching against it immediately — and keeps the enrolment history, with the date and the
> reason. That is the difference between an audit trail and a deletion. On the day somebody
> leaves, their access stops and the record of them having had it does not."*

**6 — 🚫 DO NOT PRESS *Remove template*.** Admin — and it is the wrong action anyway:

> *"Removing outright is an administrator's act and it is only ever right when the enrolment was
> a mistake in the first place."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Templates for an employee | `GET api/employee-biometrics/employee/{employeeId}` | `HR.Attendance.Read` |
| **Enrol** | `POST api/employee-biometrics/enrol` | `HR.Attendance.Write` |
| **Revoke** | `POST api/employee-biometrics/{id}/revoke` | `HR.Attendance.Write` |
| Remove | `DELETE api/employee-biometrics/{id}` | **`HR.Attendance.Admin`** |
| *(update — no screen)* | `PUT api/employee-biometrics/{id}` | `HR.Attendance.Write` |

Table: `EmployeeBiometrics`. `EnrolledById` is the enroller's employee id from the token.
Revoking sets `IsActive = false`, `RevokedDate` and `RevokedReason`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-84 · The revoke reason is not asked for.** The endpoint takes one and the screen hard-codes *"Revoked from the biometrics screen"*. So the leaver's record says why in the most useless possible way | |
| **A-85 · Nothing matches against these templates.** There is no verification endpoint and no device connector (A-21). The register is the enrolment side of a loop whose other half is not built | |
| **A-86 · The enrolment register is `HR.Attendance.Read`.** Anybody who can see the attendance dashboard can list who has a template enrolled, on which finger and at what quality. **The template data itself is never returned by any read** — `EmployeeBiometricDto` has no such field and the mapper only writes it inward — so this is a metadata exposure, not a biometric one. Worth narrowing anyway | |
| **A-87 · No expiry or re-enrolment prompt.** `QualityScore` exists precisely to drive one; nothing reads it | |

---

## 26. `/me/attendance` — the portal, and where the whole chain starts

### 📍 Where you are

**Portal:** Time, Leave & Pay → **My Attendance** · `/me/attendance` · **as the `staff`
persona**, in window B · **6 minutes**

### 📖 What it is

> *"Everything you have seen so far is the desk's view. This is the other ninety-five percent of
> the organisation — one screen, two buttons, and their own month. The punch here is the same
> punch a turnstile makes: same table, same processing, same geofence check. The channel is a
> property of the event, not a different system."*

### 👁 On the page

**Header:** *My attendance* — *"Punch in and out, and see how your month is recorded."*,
back-link to the portal home.

**Card 1 — Today · Thursday, 17 September**
- **Checked in** — a large time, or an em dash
- **Checked out** — the same
- **Hours** — shown only once the day has hours
- **Check in** button *(solid, large)* — **disabled once checked in and not yet checked out**
- **Check out** button *(outline, large)* — **disabled until checked in**
- *(after a punch)* a **verification box**: green with a shield when *Within zone* —
  *"Location verified — Tema Head Office — 200 m"*; neutral with a pin otherwise —
  *"Outside work zone (Tema Head Office — 200 m) — punch recorded with a location warning."*
- **Today's punches** — a row of pills: `In · 08:04`, `Out · 17:06`

**Card 2 — This month** — a table of every day so far, newest first: **Date** (`Thu 11 Sep`) ·
**In** · **Out** · **Hours** · **Status** (a coloured badge — Present green, Absent red, On
leave blue) · and a flags column carrying `⚠ late 47m`, `✓ overtime`, `remote`.

Empty state: *"No attendance recorded this month yet — your first punch creates today's
record."*

**How location is handled:** the browser is asked for a position with a 5-second timeout, and a
refusal, a timeout or a device with no GPS all resolve to *no coordinates* rather than an error.
Punching **must work on a desktop with no GPS at all**, so location is offered, never demanded.

### ▶ Walk it

This is the chapter to end the demo on. It is short, it is human, and it closes the loop.

**1 — Switch to window B, signed in as the `staff` persona**, and open
**Time, Leave & Pay → My Attendance**.

> *"And this is what the other hundred and forty people see. Not a register — their own day."*

**2 — Read the two big numbers.** Checked in at 08:04, checked out an em dash.

**3 — 🔴 LIVE WRITE 16 — press *Check out*.**

Chrome may prompt for location the very first time — you granted it in prep (§ 2.3), so it
should not.

**4 — Read the green box, or the neutral one, depending on where you are.**

*If you are at Tema:*
> *"'Location verified — Tema Head Office, two hundred metres.' The punch was checked against the
> fence we drew on the map earlier, and it was inside it."*

*If you are anywhere else — which is the better demo:*
> *"'Outside work zone — punch recorded with a location warning.' And that is the system doing
> exactly what it was told. We are on soft enforcement, so the punch is accepted — the person
> genuinely might be at a site — but it is flagged, it is on the record, and their supervisor
> will see it on the day. Switch that fence to hard enforcement and this punch would have been
> refused."*

*If there is no box at all:*
> *"No location on this machine — and the punch still worked. That is deliberate: clocking in
> must not depend on a laptop having GPS. The day records that the question could not be asked,
> which is different from failing to answer it."*

> 🎤 **This is where the buddy-punching question comes.** It usually arrives right after the
> verification box — *"so what stops me giving my password to a colleague?"* **§ 1.7 is the
> answer**, with the say-line written out. Short version: this proves a session and a location,
> not a person; only a biometric taken at the instant of the punch closes it; the device register
> and the biometric enrolment register are already built and the reader connector is the missing
> piece. Do not improvise this one.

**5 — Point at the *Today's punches* pills.**

> *"And there is the raw event — the same row that appears on the Punch Logs screen the HR
> officer was looking at five minutes ago. One table. A turnstile punch and a phone punch are
> the same kind of fact."*

**6 — Scroll to *This month*.**

> *"And their own month. Every day, the times, the hours and the status — with the flags on the
> right. That is the screen that stops the argument at the end of the month, because the person
> has been able to see it all along."*

**7 — Now close the loop: switch back to window A as `hr.head`, open `/hr/attendance/logs` and
refresh.** The punch you just made is there.

> *"Same punch. Thirty seconds ago, from a phone, in a self-service screen — and here it is on
> the HR desk with its coordinates and its verification, already processed into today's
> attendance."*

> ⚠ **Do not say "with its device".** The **Device** column is **empty** for a portal punch — the
> punch endpoint captures no device identity at all, though the table has three columns for it.
> That is finding **A-93**, and it is the foundation of the buddy-punching answer in § 1.7.

**8 — Optionally finish on `/hr/attendance`** and let the dashboard tiles redraw.

> *"And there it is on the dashboard. Punch, day, month, lock, hand-off — and the whole thing
> started with one person pressing one button on their phone."*

*Undo for LIVE WRITE 16:* chapter 28 — or nothing, since a real clock-out is the honest state of
the demo database.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Today's record | `GET api/staff-daily-attendance/employee/{me}/date/{today}` | **self** |
| Today's punches | `GET api/staff-attendance-logs/employee/{me}?from&to` | **self** |
| The month | `GET api/staff-daily-attendance/employee/{me}?from&to` | **self** |
| **Punch** | `POST api/staff-attendance-logs/punch` | **signed-in only** — the actor is the token |

Table: `StaffAttendanceLogs` → `StaffDailyAttendances`, plus
`AttendanceLocationVerificationLogs`.

**The punch endpoint is deliberately the least gated thing in the module.** It takes no employee
id — the server decides who punched from the token — and carries only the log type, optional
coordinates, an optional place name and `processImmediately`. The portal always sends
`processImmediately: true`, so the punch becomes a day in the same request and the screen can
show the result.

> ⚠ **A date-range quirk worth knowing** if you ever call the logs read yourself: `from` and
> `to` are **DateTimes** and `to` is exclusive at midnight, so `from=today&to=today` returns
> nothing. The portal sends tomorrow as `to` for exactly this reason.

### ⚠ Known gaps

| Gap | |
|---|---|
| **A-88 · The portal cannot raise anything.** No regularization, no overtime request, no remote-work request — all three are desk-only screens, and all three have self-service APIs sitting ready. The employee can see their month go wrong and has no way to say so. This is the most valuable single addition to this module | |
| **A-89 · Break punches cannot be made.** The two buttons are Check in and Check out; `BreakStart` and `BreakEnd` are in the enum and unreachable from any screen (and unused by processing — A-49) | |
| **A-90 · The month is the month to date only.** `from` is the first of this month and `to` is today; there is no month picker and no way to look back | |
| **A-91 · Check in is disabled once checked out.** The guard is *"disabled when checked in and not checked out"* — so after a clock-out the Check in button re-enables, and a second check-in is ignored by the server anyway (processing only writes the in-time when the day has none). Harmless, but the button promises something it will not do | |
| **A-93 · The punch proves a session and a location, never a person — and captures no device identity.** `PunchAsync` writes the log type, the coordinates and an optional place name, and leaves `DeviceId`, `DeviceSerialNumber` and `RawData` null on every portal punch. With no reader connector (A-21) and no biometric matching (A-85), **nothing in the module distinguishes a punch made by the employee from one made by a colleague holding their credentials.** The three empty columns mean device binding has somewhere to land with no schema change. **§ 1.7** is the full answer | |
| **A-94 · There is no user-verification ceremony to bind to a punch.** The solution has TOTP two-factor (`TwoFactorAuthService`) and heuristic device fingerprinting (`DeviceSessionService`); it has **no WebAuthn, FIDO2 or passkey implementation anywhere**. Both existing controls act at sign-in and neither proves presence, so there is currently nothing that could be required at the moment of a punch | |

---

## 27. Where attendance shows up outside its own menu

Seven places. Two of them are worth a minute of the demo; the rest are for the questions.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Employee profile → Time & leave → Attendance** (`/hr/employees/[id]`) | That person's monthly summaries for a chosen year: month, `Present N / M`, absent, on leave, late, attendance %, finalised. With an **Open in Attendance** button | **Yes** — 60 seconds, and it closes the "can I see one person's whole record" question. ⚠ Two columns read wrong there: **Late** reads `daysLate`, which recalculate never sets (A-68), and **Present** reads `N / totalWorkingDays`, which it also never sets. So it renders `18 / 0`. Show it as *"the same month, from the person's own file"* and keep moving |
| **Workflow inbox** (`/workflow/inbox`) | Regularizations, overtime and remote-work requests assigned to the signed-in person, alongside every other approval in the ERP. Each carries a readable title — *"Missing check-out on 11 Sep 2026"*, *"3 hrs on 24 Sep 2026"*, *"22 Sep – 23 Sep 2026 (2 days)"* — and a link straight to the record | **Yes** — 60 seconds, and it is the best answer to *"does a manager have to live in the HR module?"* No: they live in one inbox |
| **Leave module** | The day-count on a leave request takes public holidays out, reading them through `GET api/holiday-calendars/holidays/by-year/{year}` — the very calendar in chapter 7 | Mention it in chapter 7, do not navigate |
| **Location setup** (`/administration/hr/location/locations/[id]/edit`) | The *Map & Attendance Zone* section, where a geofence zone is attached to a site. This is the only place the link is made | Mention it in chapter 9 |
| **Company Schedule → Business Closures** (`/administration/hr/company-schedule/closures`) | Company-declared non-working days, site-specific or organisation-wide | ⚠ **Do not claim these reach attendance.** *Since 2026-10-05 (company-schedule final closure, lane 1b), leave reads them:* a closure that is a day off is not charged as leave — a site or unit closure only for the people it covers — and HR's statutory clocks and travel's on-duty posting skip the company-wide ones. Attendance's own register still consults neither closures nor holidays. Finding **A-92** |
| **Consulting → Timesheets** | A consultant's client-site hours, on the same workflow engine, gated by the *same* attendance permissions — `ConsultantTimesheetsController` checks `HR.Attendance.*` on every action, which is why the permission descriptions mention timesheets | Only if a consulting question comes up |
| **Payroll** (`/hr/payroll/overtime-summary`) | Payroll's own overtime view, owned by another team | Name the boundary; do not open it |

---

## 28. Reset — putting the database back

Do this after the room empties. Most of it is reversible without a rebuild.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Pay period closed** *(ch. 8, LW 1)* | No reopen button — the status enum has no path back. SQL: `UPDATE PayPeriods SET Status = 1, ClosedDate = NULL, ClosedById = NULL WHERE PeriodName = '<name>'` |
| 2 | **Geofence radius changed** *(ch. 9, LW 2)* | Re-edit the zone and set the radius back to **200**. Or `UPDATE GeofenceZones SET RadiusMetres = 200 WHERE ZoneName LIKE 'Tema Head Office%'` |
| 3 | **Device sync recorded** *(ch. 10, LW 3)* | Nothing to undo — it records an operational fact. Leave it |
| 4 | **Alert rule toggled** *(ch. 11, LW 4)* | You re-activated it in the same step. If not: ⋯ → **Activate** |
| 5 | **Day recorded by hand** *(ch. 14, LW 5)* | `hr.head` cannot delete it. As **admin**: `DELETE api/staff-daily-attendance/{id}`. Or SQL: `UPDATE StaffDailyAttendances SET IsDeleted = 1 WHERE Id = '<id>'` |
| 6 | **Day verified** *(ch. 15, LW 6)*, and § 2.6b's flags | No unverify. SQL: `UPDATE StaffDailyAttendances SET IsVerified = 0, VerifiedById = NULL, VerifiedDate = NULL, VerificationNotes = NULL, RequiresVerification = 0, HasException = 0, ExceptionApproved = 0, ExceptionReason = NULL WHERE Id = '<id>'`. ⚠ If you also pressed **Approve exception**, the day's `Notes` were overwritten by your comment (A-41) — set them back too |
| 7 | **Punch processed** *(ch. 17, LW 7)* | One-way by design. Leave it — a processed punch is the correct state |
| 8 | **Regularization raised** *(ch. 18, LW 8)* | If you did **not** apply it: as `hr.head`, `DELETE api/staff-attendance-regularizations/{id}` (self-or-Write, refused once applied) |
| 9 | **Correction applied** *(ch. 18, LW 9)* | Two rows to put back — the day's times and the request. SQL: `UPDATE StaffDailyAttendances SET ActualCheckOutTime = <old> WHERE Id = '<dayId>'` then `UPDATE StaffAttendanceRegularizations SET IsDeleted = 1 WHERE Id = '<regId>'`. **Write the old time down before you press Apply** |
| 10 | **Overtime approved** *(ch. 19, LW 10)* | Leaving it approved is the honest state. To reset: `UPDATE StaffOvertimeRequests SET Status = 1, ApprovedById = NULL, ApprovalDate = NULL WHERE RequestNumber = '<no>'` — and the workflow instance will disagree with it, so prefer to leave it |
| 11 | **Hours confirmed** *(ch. 19, LW 11)* | `UPDATE StaffOvertimeRequests SET Status = 2, ActualOvertimeHours = NULL, SupervisorConfirmedById = NULL, SupervisorConfirmedDate = NULL, SupervisorNotes = NULL WHERE RequestNumber = '<no>'` |
| 12 | **Remote work approved** *(ch. 20, LW 12)* | Same shape as row 10. Prefer to leave it |
| 13 | **Summary finalised** *(ch. 21, LW 13)* | No reopen, by design. SQL: `UPDATE StaffMonthlyAttendanceSummaries SET IsFinalized = 0, FinalizedDate = NULL, FinalizedById = NULL WHERE Id = '<id>'` |
| 14 | **Alerts acknowledged** *(ch. 22, LW 14)* | `UPDATE StaffAttendanceAlerts SET Status = 1, AcknowledgedById = NULL, AcknowledgedDate = NULL, AcknowledgementNotes = NULL WHERE Id IN (…)` |
| 15 | **Payroll export run** *(ch. 24, LW 15)* | As **admin**: `DELETE api/staff-attendance-payroll-exports/{id}`. ⚠ Deleting one can make the next export reuse its reference (A-83) — harmless on a demo database |
| 16 | **Portal check-out** *(ch. 26, LW 16)* | Leave it. A real clock-out is the honest state, and undoing it would leave the punch log and the day disagreeing |
| — | **§ 2.6's SQL** | `UPDATE StaffMonthlyAttendanceSummaries SET PayPeriodId = NULL WHERE …` — or leave it; it is the state the product is heading for anyway |

**The three things that cannot be put back** are the **numbers**: `REG-…`, `OT-…`,
`PAY-EXP-…`. The counters only move forward, by design. Nobody will notice.

**The clean option.** If any of that looks fiddly, the whole database rebuilds in 45–60 minutes:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
  Where-Object { $_.CommandLine -like '*ErpSystem.Api*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild costs you chapter 2's prep as well, and it renumbers every request in this book. It
also resets **today's punches** to the rebuild date — which is fine, because that is what § 2.2
is for.

---

## 29. The short path — 25 minutes

When the slot shrinks. Eight screens, in this order, and the story still lands. Everything in
this path is either a read or a safe write.

| # | Screen | Min | The one thing |
|---|---|---|---|
| 1 | `/hr/attendance` | 3 | read the two rows of tiles; **"lateness counts as present; punctuality is measured separately"** |
| 2 | `/me/attendance` *(as `staff`, window B)* | 4 | **press Check out**; read the geofence box aloud — verified *or* outside the zone, both are wins |
| 3 | `/hr/attendance/logs` — refresh | 2 | the same punch, on the HR desk, with its device and its verdict. **"One table. A turnstile and a phone are the same kind of fact"** |
| 4 | `/hr/attendance/daily` — search `Kojo` | 4 | the flags; the day with **no clock-out and no hours**; *"it does not guess"* |
| 5 | `/hr/attendance/regularizations/[id]` — the applied one, then its **Workflow** tab | 5 | read his reason aloud; then **approve ≠ apply**; then *"this is not attendance, this is the engine"* |
| 6 | `/hr/attendance/overtime` — *Awaiting supervisor confirmation* | 3 | **planned vs actual**; the queue nobody thinks to build |
| 7 | `/hr/attendance/summaries` — the **finalised** row | 3 | open the ⋯ menu and show both actions greyed. **"The API refuses. That is the answer to 'will this change after I have paid it'"** |
| 8 | `/administration/hr/attendance/geofence-zones` | 1 | the map, and *soft vs hard enforcement* |

Cut, in this order if you must: the geofence screen *(you already showed the fence working in
step 2)*, then the overtime queue, then the punch logs.

**Do not put in the short path:** bulk imports, payroll exports, or anything that recalculates.

---

## 30. What this walk found

**Ninety-four findings.** Most are small. Five stop or spoil a demonstration, and **six** are the
ones to fix before this module is called finished — they are the ones where a screen tells the
user something the server did not do.

Two of the ninety-four are of a different kind and sit together in **§ 1.7**: **A-93** and
**A-94** are not defects in a screen, they are the **unclosed half of a control the module was
designed around**. Buddy punching is the one question a stakeholder is certain to ask, and
§ 1.7 is the prepared answer — read it before any demonstration, whether or not you intend to
raise it yourself.

### The five that affect a demonstration

| # | Where | Finding |
|---|---|---|
| **A-1** | punch processing | **Lateness is never computed.** No code reads the schedule, the grace period, the working days or the break rules. A punch always lands the day at *Present*. § 1.5 has the sentence that turns this into a credibility moment |
| **A-2** | `/hr/attendance/imports/new` | **A pasted CSV writes nothing and reports success.** The browser drops the employee reference and sends `employeeId: null`; the server skips the row and marks it OK. Do not run an import live |
| **A-3** | `/hr/attendance/summaries` | **Recalculate fills 6 of 22 fields** — not hours worked, not working days, not attendance %, not punctuality %. A row you recalculate on stage loses four of its ten columns |
| **A-4** | `/hr/attendance/payroll-exports` | **An export always reports zero**, because nothing attaches a monthly summary to a pay period (A-69). The screen is also empty on a fresh demo database, by design |
| **A-5** | every setup screen | **`hr.head` cannot delete anything.** Every delete in the module is `HR.Attendance.Admin`; every delete button renders anyway |

### The six worth fixing first

| # | Where | Finding | Why it is first |
|---|---|---|---|
| **A-58** | overtime | **Confirmed overtime never reaches the day or the month.** `AttendanceId` is never set, the day's overtime fields are never written, and the monthly summary sums the *day's* hours — so an approved, confirmed overtime request contributes nothing to the month | It is the one that costs money |
| **A-53** | regularizations | **Applying a correction does not recompute the day's hours.** The times change; `ActualWorkHours` does not. About four lines | The most user-visible wrong number in the module |
| **A-24** | alerts | **Nothing calls the evaluator.** Rules never fire on their own — no screen and no job calls `POST …/alerts/evaluate/{id}`. The rules, thresholds, severities and lifecycle are all built | A whole feature that is one scheduled job short |
| **A-3** | summaries | **Finish the recalculation.** The sixteen unset fields include the four the screen shows | Four of ten columns on the payroll-facing screen |
| **A-69 / A-4** | summaries → export | **Set `PayPeriodId` when a summary is recalculated.** One assignment | It is the join that makes the hand-off real |
| **A-6** | schedules / shifts | **No screen assigns a work schedule or a shift to an employee.** Both controllers, both services and both typed clients exist; no page imports them | Nobody can run the module without a developer |

### Everything else, by area

| # | Area | Finding |
|---|---|---|
| A-7 | dashboard | No date control; `asOf` is supported and always today |
| A-8 | dashboard | Trend length hard-coded to 7 of a supported 1–90 |
| A-9 | work schedules | The shift edit dialog repopulates from a summary and blanks four fields on save |
| A-10 | work schedules | The register's reads carry no attendance permission, unlike every other controller |
| A-11 | rotations | A plan generates nothing — no job advances members, no shift assignment is created |
| A-12 | rotations | The member dialog only offers an employee; unit and team are always `null` |
| A-13 | holidays | Nothing assigns a calendar to an employee, department or location |
| A-14 | holidays | `IsRecurringAnnually` generates nothing; next year's rows were seeded |
| A-15 | pay periods | **Closing a period locks no attendance** — the confirmation says it does |
| A-16 | pay periods | `PendingClose` is unreachable |
| A-17 | pay periods | `ExportedToPayroll` is unreachable; the **Exported** column never fills |
| A-18 | geofence | Only the **first** zone linked to a location is checked |
| A-19 | geofence | Enforcement is per zone, never per employee or per role |
| A-20 | geofence | Device punches carry no GPS, so they are always *GPS unavailable* — correct, but say it |
| A-21 | devices | **There is no device connector.** Nothing polls a reader; no endpoint accepts a device batch |
| A-22 | devices | The form never sets `locationId`; the Location column falls back to free text |
| A-23 | devices | *Record sync* always sends a pending count of 0 |
| A-25 | alerts | `EvaluationWindowDays` is ignored; every rule is judged on one day |
| A-26 | alerts | *Consecutive absences* fires on a single day and ignores its threshold; *Unauthorised absence* is identical and never checks for a leave request |
| A-27 | alerts | *Low attendance percentage* has no branch at all — it can never fire |
| A-28 | alerts | A rule's unit / position / employee scope is ignored; every rule applies to everybody |
| A-29 | alerts | No notification is ever sent — email, in-app and the recipient list are unread |
| A-30 | alerts | No de-duplication; evaluating a day twice doubles its alerts |
| A-31 | alerts | `TriggerValue` is never set, so an alert cannot say by how much |
| A-32 | overtime | No eligibility or exemption check at submission, contrary to the entity's own documentation |
| A-33 | overtime policies | The edit dialog shows the allowance type and drops it on save |
| A-34 | overtime policies | An override's "approved by" is the token, i.e. "created by" |
| A-35 | daily | The employee profile's **Open in Attendance** deep link is ignored — the page never reads `?employeeId` |
| A-36 | daily | Four server-side filters unreachable: org unit, location, min lateness, min overtime |
| A-37 | daily | **No export** — on this or any screen in the module |
| A-38 | daily/new | The form offers 9 of the day's 60 fields; no scheduled times, no location, no device |
| A-39 | daily/new | Hours are not validated against the times, in either direction |
| A-40 | daily | No edit screen for a day; `PUT` is implemented and called by nothing |
| A-41 | daily/[id] | Approving an exception **overwrites the day's Notes** |
| A-42 | daily/[id] | `ExceptionApprovedById` is never set; the page's "Approved by" is blank for ever |
| A-43 | daily/[id] | **Nothing ever sets `RequiresVerification` or `HasException`** — the verification feature is complete except for the line that starts it |
| A-44 | daily/[id] | Coordinates are stored and never shown; no map on the day |
| A-45 | records | One day at a time; the date-range read is unused |
| A-46 | records | **The simple register is invisible to the monthly summary**, which sums the full register only |
| A-47 | records | Overtime hours are on the form and not on the table |
| A-48 | logs | No filters at all — no employee, date, device or type. Unusable on a live tenant after a fortnight |
| A-49 | logs | Break punches are in the enum, unsendable, and ignored by processing |
| A-50 | logs | No "process all" — 200 synced punches is 200 clicks |
| A-51 | logs | A second out-punch silently overwrites the first on the day |
| A-52 | regularizations / remote work | **Anyone signed in can raise either for anyone** — no self-or-permission check on create, unlike overtime |
| A-54 | regularizations | No edit screen and no withdraw button; `PUT` and `DELETE` are implemented |
| A-55 | regularizations | *Supporting documents* is free text, not an upload |
| A-56 | regularizations | The employee cannot see their own in the portal |
| A-57 | overtime | No daily or weekly ceiling is applied |
| A-59 | overtime | `Cancelled` is unreachable |
| A-60 | overtime | No portal screen — an employee cannot raise their own request |
| A-61 | remote work | *"Working days"* counts **calendar** days |
| A-62 | remote work | An approved request marks no attendance days |
| A-63 | remote work | `Cancelled` is unreachable |
| A-64 | remote work | No portal screen |
| A-65 | remote work | Create and update DTOs name the same column differently |
| A-66 | summaries | A summary cannot be created from any screen |
| A-67 | summaries | No month-end job; every summary came from a scripted loop |
| A-68 | summaries | `DaysLate` and `NumberOfLateDays` are two columns for one fact, and the employee profile reads the one recalculate does not set |
| A-70 | summaries | No export |
| A-71 | alerts | **No Resolve and no Dismiss** — both statuses and all six columns exist; no endpoint sets either |
| A-72 | alerts | Status filtering happens in the browser over 200 rows |
| A-73 | imports | A row with no employee id is reported as a **success** with no error |
| A-74 | imports | A row whose day already exists is a silent success too |
| A-75 | imports | `CreatedAttendanceId` is never set — no trace from a day back to its source line |
| A-76 | imports | `ErrorSummary` is never written; the card can never render |
| A-77 | imports | No hours are derived for an imported day |
| A-78 | imports | No real file upload — a 20 000-row month is a 20 000-element JSON body |
| A-79 | exports | `TotalEmployees` is never set; the column reads 0 for ever |
| A-80 | exports | No payload is produced and nothing is sent anywhere |
| A-81 | exports | The period is never marked exported |
| A-82 | exports | The status is hard-coded to *Completed*; `Failed` and `PartialSuccess` are unreachable |
| A-83 | exports | The reference generator counts live rows — the collision every other generator was fixed for |
| A-84 | biometrics | The revoke reason is hard-coded to *"Revoked from the biometrics screen"* |
| A-85 | biometrics | Nothing matches against the templates; there is no verification endpoint |
| A-86 | biometrics | The enrolment register is `HR.Attendance.Read` (the template data itself is never returned by any read) |
| A-87 | biometrics | No expiry or re-enrolment prompt; `QualityScore` is read by nothing |
| A-88 | portal | **The portal cannot raise anything** — no regularization, no overtime, no remote work, though all three have self-service APIs |
| A-89 | portal | Break punches cannot be made |
| A-90 | portal | The month view is month-to-date only; no picker, no history |
| A-91 | portal | **Check in** re-enables after a check-out and does nothing if pressed |
| A-92 | company schedule | Business closures reach neither attendance nor leave, despite the runbook's claim. *2026-10-05: **half closed** — leave, the statutory clocks and travel's on-duty posting read them since company-schedule lane 1b (`IHrWorkingDayCalculator`, `IHrClosureCalendar`). Attendance still has no working-day builder of its own and reads neither closures nor holidays: **open, attendance's to build** — kept in this ledger, because attendance is HR's own area, not another team's (the cross-module register is for those)* |
| **A-93** | **punch / portal** | **Buddy punching is unmitigated.** The punch proves a session and a device location, never a person; no device identity is captured even though the table has three columns for it. With A-21 (no reader connector) and A-85 (no biometric matching) unbuilt, a shared credential is indistinguishable from the employee. **§ 1.7** |
| **A-94** | **platform** | **No WebAuthn, FIDO2 or passkey anywhere in the solution**, so there is no user-verification ceremony that could be required at the instant of a punch. TOTP two-factor and device fingerprinting both act at sign-in and neither proves presence. **§ 1.7** |

### What is genuinely strong here

It is worth being as precise about this as about the gaps, because it is a long list too:

- **The pipeline is right.** Punch → day → month → lock → hand-off, with the raw punch kept
  separately and immutably. That is the architecture a payroll auditor wants, and most products
  at this price point do not have it.
- **`IsFinalized` is enforced by the API, not by a screen.** Recalculate, edit and delete are all
  refused. This is the single best thing in the module.
- **Corrections are requests, on the corporation's own approval engine**, with a number, a
  written reason, an approval history — and a deliberate second step before they change a
  reported hour.
- **The geofence works, on circles and polygons, with soft and hard enforcement**, and it writes
  a verification log per punch rather than just a flag.
- **The daily register's search is the best filter surface in HR** — twelve composing dimensions
  including six tri-state flags.
- **The dashboard is one request**, with the attendance rate defined once and computed
  identically in three places.
- **Every number generator was hardened against the soft-delete collision**, with the reasoning
  written into the code.
- **The actor is always the token**, and the employee-versus-user identity distinction is handled
  carefully and documented at every call site that matters.

---

## Appendix A — every route, in demo order

| # | Route | Chapter | Persona |
|---|---|---|---|
| 1 | `/hr/attendance` | 3 | hr.head |
| 2 | `/administration/hr/attendance` | 4 | hr.head |
| 3 | `/administration/hr/attendance/work-schedules` | 5 | hr.head |
| 4 | `/administration/hr/attendance/work-schedules/new` | 5 | hr.head |
| 5 | `/administration/hr/attendance/work-schedules/[id]/edit` | 5 | hr.head |
| 6 | `/administration/hr/attendance/shift-rotations` | 6 | hr.head |
| 7 | `/administration/hr/attendance/shift-rotations/[id]` | 6 | hr.head |
| 8 | `/administration/hr/attendance/holiday-calendars` | 7 | hr.head |
| 9 | `/administration/hr/attendance/holiday-calendars/[id]` | 7 | hr.head |
| 10 | `/administration/hr/attendance/pay-periods` | 8 | hr.head |
| 11 | `/administration/hr/attendance/geofence-zones` | 9 | hr.head *(+ admin)* |
| 12 | `/administration/hr/attendance/devices` | 10 | hr.head |
| 13 | `/administration/hr/attendance/alert-rules` | 11 | hr.head |
| 14 | `/administration/hr/attendance/overtime-policies` | 12 | hr.head |
| 15 | `/hr/attendance/daily` | 13 | hr.head |
| 16 | `/hr/attendance/daily/new` | 14 | hr.head |
| 17 | `/hr/attendance/daily/[id]` | 15 | hr.head |
| 18 | `/hr/attendance/records` | 16 | hr.head |
| 19 | `/hr/attendance/logs` | 17 | hr.head |
| 20 | `/hr/attendance/regularizations` | 18 | hr.head |
| 21 | `/hr/attendance/regularizations/new` | 18 | hr.head |
| 22 | `/hr/attendance/regularizations/[id]` | 18 | hr.head |
| 23 | `/hr/attendance/overtime` | 19 | hr.head |
| 24 | `/hr/attendance/overtime/new` | 19 | hr.head |
| 25 | `/hr/attendance/overtime/[id]` | 19 | hr.head **+ head.dev** |
| 26 | `/hr/attendance/remote-work` | 20 | hr.head |
| 27 | `/hr/attendance/remote-work/new` | 20 | hr.head |
| 28 | `/hr/attendance/remote-work/[id]` | 20 | hr.head |
| 29 | `/hr/attendance/summaries` | 21 | hr.head |
| 30 | `/hr/attendance/alerts` | 22 | hr.head |
| 31 | `/hr/attendance/imports` | 23 | hr.head |
| 32 | `/hr/attendance/imports/new` | 23 | hr.head — **view only** |
| 33 | `/hr/attendance/imports/[id]` | 23 | hr.head |
| 34 | `/hr/attendance/payroll-exports` | 24 | hr.head |
| 35 | `/hr/attendance/biometrics` | 25 | hr.head |
| 36 | `/me/attendance` | 26 | **staff** |
| — | `/hr/employees/[id]` → Time & leave → Attendance | 27 | hr.head |
| — | `/workflow/inbox` | 27 | head.dev |

---

## Appendix B — the permission map, in one table

Every endpoint in the module, by what it needs. `hr.head` holds **Read**, **Write** and
**Approve**, and **not Admin**.

| Tier | Actions |
|---|---|
| **No permission — signed in and internal** | the self-service **punch**; reading work schedules, shift definitions, rotation plans, holiday calendars, pay periods and geofence zones; **creating a regularization**; **creating a remote-work request**; **approving or rejecting** any of the three request types *(the workflow assignee's act, validated per request)* |
| **Self, or `HR.Attendance.Read`** | reading one day, one attendance record, one regularization, one overtime request, one remote-work request, one monthly summary; a person's own punches, days, records, requests and alerts |
| **`HR.Attendance.Read`** | the dashboard; every organisation-wide register — daily attendance search, records by date, punch logs, all three request registers, monthly summaries, alerts, alert rules, devices, biometrics, imports and their rows, payroll exports; rotation members; overtime overrides |
| **`HR.Attendance.Write`** | record a day · update a day · **verify** a day · **approve its exception** · capture a punch manually · **process** a punch · **apply** a regularization · amend a request *(self-or)* · **confirm overtime hours** · **recalculate** a summary · **finalise** a summary · update a summary · **stage** and **process** an import · **run a payroll export** · **acknowledge** alerts *(single and bulk)* · **enrol** and **revoke** biometrics · **close** a pay period · **record a device sync** · **toggle** an alert rule · create and update every configuration row *(schedules, shifts, rotations, stages, members, calendars, holidays, pay periods, zones, devices, alert rules, overtime policies, overrides, employee work schedules, shift assignments)* |
| **`HR.Attendance.Admin`** — ***`hr.head` is refused*** | **almost every delete in the module**: days, attendance records, punch logs, alerts, biometric templates, imports, payroll exports, summaries, work schedules, shifts, rotation plans, stages, members, holiday calendars, holidays, pay periods, geofence zones, devices, alert rules, overtime policies, overrides, employee work schedules, shift assignments |
| **`HR.Attendance.Approve`** | interim authority to decide a regularization, an overtime request or a remote-work request **where no workflow definition is published**. All three are published here, so it does nothing on this database |

> Three deletes are **not** Admin, and they are the withdrawal-shaped ones: a regularization, an
> overtime request and a remote-work request can each be removed by **their own owner, or by the
> desk with Write** — because withdrawing your own request is not an administrative act. The
> service still refuses an *applied* regularization and a non-*Pending* overtime request.

---

## Appendix C — related documents

| Document | What it adds |
|---|---|
| `docs/HR/areas/leave/HR-LEAVE-SYSTEM-GUIDE.md` | The other half of "is this person expected at work today". Its chapter 4 is the Leave Types rulebook that sits beside the attendance setup |
| `docs/HR/areas/employees/HR-EMPLOYEES-SYSTEM-GUIDE.md` | The profile whose *Time & leave → Attendance* tab reads this module's summaries |
| `docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md` | The first guide in this series; the format's origin |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | How the three request types reach the engine, and why attendance's `ApprovedById` needs both identities |
| `docs/HR/integration/HR-PAYROLL-BOUNDARY.md` | What attendance owes payroll and what it must not decide |
| `docs/HR/catalogues/HR-REPORTS-CATALOGUE.md` | § 3.4 is the attendance reporting gap, including the missing register export (A-37) |
| `docs/HR/integration/HR-MODULE-INTEGRATION-MAP.md` | Where attendance sits against leave, payroll, SHE and consulting |
| `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` | Defects owned by other teams that touch this module |
| `dev-harness/hr-demo-smoke/scenarios/030-attendance.mjs`, `031-attendance-operations.mjs` | Exactly what the demo database holds, and how to rebuild it |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 2 is the ten-step version of this guide, for the standard demo pack |

---

*End of the HR Attendance & Time Management System Guide.*
