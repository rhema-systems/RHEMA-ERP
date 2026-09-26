# Hand-off to the Payroll module owner — three leave settings that only payroll can honour

**Raised by:** HR module work, 2026-09-17 (leave module closure, wave B3).
**Severity:** no defect, no data loss. Three configurable settings HR stores and cannot act on.
**Status:** open. HR stores all three correctly; nothing reads them.

This is a self-contained report — nothing in it requires reading HR's plans or code.

The rule HR is following is the one recorded in `docs/HR/README.md`: **payroll is another module's,
HR reads it and never writes it.** Each of the three settings below is something an HR user
configures, HR saves faithfully, and only payroll can turn into money. Rather than guess a formula,
HR has left them inert and labelled them on screen as payroll's — which is why a user may ask you
about them.

---

## 1. `LeaveType.IsPaid` — unpaid leave produces no deduction

**Where:** `LeaveType.IsPaid` (`src/ErpSystem.Core/Entities/HR/LeaveEntities.cs`), set on the leave
type form under Administration → HR → Leave Types.

**What HR does with it:** displays it. Nothing else in the solution reads it.

**What it means:** a leave type flagged unpaid is leave the employee takes without pay. HR knows
exactly who was on it and for how many chargeable days — `LeaveRequest.TotalDays` on an approved
request, and from the leave→attendance join (HR closure slice D1) the individual dates at status
`OnLeave` carrying `LeaveRequestId`.

**What HR needs from payroll:** a decision on where the deduction belongs. HR's position is that it
records the leave and the days; what a day of unpaid leave is worth, and which pay component it
reduces, is payroll's. **HR will not build a deduction for this.** If payroll wants HR to expose the
days in a particular shape, say which and HR will add the read.

---

## 2 & 3. `PublicHoliday.AttractsHolidayPay` and `HolidayPayMultiplier`

**Where:** `PublicHoliday` (`src/ErpSystem.Core/Entities/HR/StaffAttendanceEntities.cs`), set per
holiday under Administration → HR → Time, Attendance & Leave → Holiday Calendars.

**What HR does with them:** stores them. Neither has a read-site anywhere in the solution. As of
2026-09-17 the holiday form labels them "Recorded for payroll … HR stores it; payroll applies it",
so the screen no longer implies HR acts on them.

**What they mean:** `AttractsHolidayPay` says an employee who *works* a public holiday is paid a
premium; `HolidayPayMultiplier` is that premium (e.g. 2.0 for double time).

**What HR can supply:** attendance already records who worked which day. Hours worked on a date that
is a public holiday is a query payroll can run, or one HR can expose as a projection — say which.

**⚠ One related change you should know about.** As of 2026-09-17 a holiday whose `ObservanceType` is
**Optional** is treated by HR as a **working day** — leave taken on it is chargeable, and HR's
statutory clocks count it. Mandatory and Substitute Day close the office as before. If payroll's
holiday-pay logic assumes every `PublicHoliday` row is a non-working day, that assumption is now
narrower than it was.

---

## 4. What HR is asking for

Nothing urgent, and nothing blocking. Three things, in order of usefulness:

1. **Confirm the boundary** — that unpaid leave and holiday pay are payroll's to compute. If you
   disagree, say so before HR's sign-off, because HR's plan currently records this as settled.
2. **Name the shape you want.** If a projection from HR (days of unpaid leave per employee per pay
   period; hours worked on a holiday per employee) would help, HR will build it read-only.
3. **Note the Optional-holiday change above.**

Contact: the HR module owner. Related: `docs/HR/areas/leave/HR-LEAVE-CLOSURE-PLAN.md` (L-30, L-33, decision
D-6) and `docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` (L-D6).
