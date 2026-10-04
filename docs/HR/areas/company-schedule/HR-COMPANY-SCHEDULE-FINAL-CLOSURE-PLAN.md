# HR Company Schedule — final closure plan

**What this is:** the single tracking document for the final, end-to-end closure of the Company
Schedule module — events with participants, attendance, attachments and tasks; meeting rooms and
bookings; milestones; business closures; fiscal years and periods; the two diaries; the reminder
sweep. It folds together the guide's finding ledger (`HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md` § 21:
C-1…C-51 and R4-2.1…R4-13.1), the two memories that carry the module's history, and a fresh
end-to-end read of every file in the module on 2026-10-01 (the service, controller, repository,
DTOs, mapping, diary service, email catalogue, sweep host, all 19 screens, both frontend contract
files and the harness). Three defects the user met by hand prompted it: a room's site not shown on
edit, a closure type that does nothing, and milestone "documents" that are a text box. The whole set
is turned into eight ordered lanes with checkboxes.

**A same-day independent review of this plan** (2026-10-01, § 9) re-checked every finding in source,
corrected the plan where it was wrong (the room-edit cause, the leave wiring, two guards that
contradicted other rules, three items marked "kept" without the user's ruling, two findings in the
wrong lane), and added thirty findings, F-27…F-56 (§ 3), and nine decisions, D-10…D-18 (§ 1b). All
of it is folded into the lanes below. The user settled D-12 and D-17 the same day, as recommended.

**Scope decision, 2026-10-01:** close all of it — the user asked for this to be the ultimate review of
the module, with every discovered issue fixed, and ruled that nothing is deferred (D-9). Development
has not started (the user: "don't start the actual development yet").

**START HERE:**
0. **Re-checked against HEAD 1163bbc47 on 2026-10-04** (after the travel final closure and master merge
   #13 landed on hrdev; § 3c). The module's own backend and screens did not change. F-27 is already in
   HEAD — lane 5's item is a browser re-test, not a build. One new finding, F-57 (lane 5). The leave and
   Finance references are updated to HEAD; the drill service is named correctly (lane 2).
1. § 1a is settled. **§ 1b holds the nine decisions raised by the review.** D-12 (recurring events
   are a light series) and D-17 (no milestone link) are settled, and with them what the migration
   contains. **Seven remain pending the user** — D-10, D-11, D-13, D-14, D-15, D-16 and D-18 — each
   needed before the lanes § 2 lists against it; none blocks lane 0.
2. ✅ **Lane 0 is done (2026-10-04): the migration is applied to UAT.** Next is lane 1, whose source
   check needs D-15 settled first. *As planned:* Lane 0 (§ 6): first count on UAT, read-only, the rows the migration must decide about (legacy
   department scopes, closures that disagree with D-1, duplicate participant and attendance rows —
   F-45, F-53). Then the user scaffolds the one migration, it is rewritten as guarded SQL, the user
   builds, it is applied to UAT. Nothing in lanes 1–7 can be verified before this.
3. Then lanes 1 → 2 → 3 → 4 → 5 → 7 → 6, in that order. Each lane is source-checked and its decisions
   settled with the user (§ 1c) before it is built. Lane 6 (the harness and the docs) is last because
   it proves the other six.

---

## 1. Decisions

### 1a. Decisions taken with the user (2026-10-01)

| # | Decision | What it rules out |
|---|---|---|
| **D-1** | **A closure's TYPE drives its scope.** Full = the whole company; Site (enum `StationClosure`) = one site, from the Location tree; Organisation unit (enum `DepartmentClosure`, relabelled) = one organisation unit and everything beneath it; Partial = any scope, and the day **still counts as a working day** (reduced operations, not a shutdown). The server refuses inconsistent combinations. | The type as a label beside an independent "whole company" switch |
| **D-2** | **Recurring events generate their occurrences** on create: date-shifted copies, each with its own number, linked by a series id, capped at 52. *How the series behaves is D-12.* | "Repeats weekly, 10 times" stored and generating nothing (C-14) |
| **D-3** | **Milestone documents AND event attachments are real files** on the controlled-upload gate (scan, DMS registration, authorised download), in one migration. | The text-box "Related documents" and the path-only event attachment (C-18) |
| **D-4** | **Closures reach leave day-counting now**, scoped to the employee's site and unit. Attendance has **no working-day builder at all** (attendance guide A-92: it never reads public holidays either), so it is logged as a cross-module gap rather than wired. *How leave reads them was redesigned by the review — lane 1.* | The closure form's claim "Affects leave and attendance calculations" standing with neither true (C-5) |
| **D-5** | **Organisation level and unit replace Department everywhere in this module** — closures, events, the by-unit endpoints, the diaries, scope resolution. `Employee.DepartmentId` is legacy; scope is read from `Employee.OrganizationUnitId` and `Employee.LocationId`. | The legacy `Department` picker on the event and closure forms |
| **D-6** | **HR consumes Finance's fiscal calendar read-only; HR's own fiscal years and periods are retired.** Finance already owns `FiscalYears` / `FiscalPeriods` with status, lock and close workflows. HR's tables are read by nothing (C-42) and duplicate the concept a third time beside `CompanyHrPolicySettings.FiscalYearStartMonth`, which becomes the fallback when Finance has defined no year. The HR tables stay in the model this slice; dropping them is a later migration. | Repairing HR's copy (the tenant bug, C-43…C-48, R4-2.2) — they close by retirement |
| **D-7** | **A company calendar is built** — month and week views, one page for HR and for staff, the server deciding what each caller may see. It is the consumer that makes `ShowOnCompanyCalendar`, `ShowOnCalendar`, `Visibility` and `Scope` real. | Every screen a table and nothing to show a calendar flag on (C-9) |
| **D-8** | **The self-service invitation reply ships with the calendar.** The finish plan's D-02 said the self-or-permission check is owed "the day a `/me` calendar ships"; lane 7 ships one, so an invitee answers their own invitation from it, through a door that checks the participant is theirs. | A calendar that shows a staff member an invitation they cannot answer (R4-6.4) |
| **D-9** | **Nothing is deferred** (the user, on reading this plan's first residual register). Every finding the first draft parked is built in this round: server-side search, paging, date filter and CSV export for the event and booking registers (C-10…C-13, C-25); an event-against-event clash rule (C-15); closures that recur annually (C-38); the milestone link (C-41, *closed by decision D-17: not built*); the SHE emergency drill that schedules the company event it is (C-51); the team schedule's write half, the unit head's own read and a sub-unit filter (R4-10B.1, R4-10B.3, R4-10B.4); a diary that says when a source failed (R4-10A.3); an attendance row that can be removed and a participant that can be edited (C-21, C-22); Delete on the booking page (C-32); the company profile's seal buttons hidden without Admin and its logo as a versioned upload (C-49, C-50). | A residual register with "deferred" in it |

**Rules D-9 settles for the new items (recommendations, to be confirmed on each lane's source check):**
C-15 — two events whose audience is the whole company (`Scope = AllStaff`, or `Department` for the same
unit) may not overlap on the same site or company-wide: the server refuses; any other overlap is a
warning the form shows before save. C-21 — removing an attendance row is a register correction, on
`Write`, not an Admin destruction. C-38 — a closure marked *recurs every year* applies on the same
month and day of every later year. C-51 — recording a drill with a next-drill date creates (or moves)
a company event for it, organised by the drill's coordinator, and the event page says which drill it
is. C-50 — the logo joins the seal and the signature as a third versioned asset kind; **the review
recommends retiring the free-text `LogoUrl` once the upload exists rather than keeping it as a
fallback (F-55)**, with `Tenant.LogoUrl` the last fallback. C-41 — closed by D-17: not built; the certificate is a
milestone document.

**Reopened by the review.** The first draft recorded three items as kept without the user's ruling;
under D-9 none stands without one.
- **C-3** — approval as a one-click field rather than the workflow engine — becomes **D-10**.
- **C-16** — the site picker lists the whole location tree — becomes a lane 5 recommendation,
  confirmed at its source check: offer only location levels that allow employee assignment. ⚠ The
  audience resolver matches a location exactly, so a region chosen as a closure's "site" would reach
  nobody.
- **R4-3.1** — the landing answering a 403 with "Nothing scheduled" — is a live defect, owned by
  lane 5.

### 1b. Decisions raised by the review (2026-10-01) — two settled, seven pending the user

**D-12 and D-17 were settled by the user on 2026-10-01, as recommended (✅ below).** The other seven
are pending; each is needed before the lanes in its "Blocks" column, and none blocks lane 0.

| # | Question | Recommendation | Blocks |
|---|---|---|---|
| **D-10** | Approval of events and bookings: the workflow engine, or the one-click flag? | **The engine**, like every other HR approval: the four-step recipe, definitions of two steps or more, the auto-approve guard where no definition is published, and `preventInitiatorApproval` to stop self-approval. It gives the approver an inbox entry and a notification (C-3, the approver half of F-34). No schema: the engine links by entity type and id. If the flag is kept instead: refuse self-approval — the approver may not be the organiser or the booker. | Lanes 2, 3; harness definitions |
| **D-11** | Who is the organiser? | **An Organiser picker** defaulting to the signed-in employee; the creator stays in `CreatedBy`. Diaries, the clash check and the calendar use the organiser (F-42). | Lane 2 |
| **D-12** ✅ | Recurring events: a series, or independent occurrences? | **Settled 2026-10-01, as recommended: a light series, in which the occurrences are the series.** Each occurrence stays a full event, with its own number, guest list, RSVPs, attendance register, tasks and papers; `RecurrenceSeriesId` and `OccurrenceNumber` tie them together, and there is **no series table**. Series behaviour is a scope choice, "this occurrence / this and following / the whole series", on adding or removing a guest, editing, rescheduling and cancelling. A guest added to the series gets one invitation listing the dates and answers each date or all at once. "Book this room for every occurrence" makes one booking per date and lists the dates where the room is taken. A series action never changes an occurrence that is past or completed. A series needs an end date or a count, up to 52, and can be extended later. A monthly rule on a day a month lacks falls on that month's last day, counted from the first date; an occurrence on a holiday or company-wide closure is generated and flagged, not skipped. *Why: answers and attendance are per meeting, since people miss one week and not the next; independent occurrences would mean re-inviting everyone every week; and an ordinary event needs no special case in the clash check, the reminder sweep, the diaries or the calendar.* | Lane 2. Lane 0: no series table |
| **D-13** | May staff book rooms themselves? | **Yes, from the portal**: their own bookings only, the same room rules, approval-required rooms routed to the approver, staff cancel their own. HR keeps the desk. | Lanes 3, 7 |
| **D-14** | Calendar invites in the emails? | **Yes**: an `.ics` on invitation, reschedule and cancellation — a stable UID per event, SEQUENCE raised on each change, METHOD REQUEST and CANCEL. The email DTO already carries attachments; the templated send needs an overload. External guests answer from their mail client to the organiser, and HR records it at the desk, which answers F-36. | Lane 2 |
| **D-15** | Unpaid closures, and closures added after leave was approved? | HR records the pay flag and **exposes closures to payroll read-only**, logged as a cross-module item, because payroll is another developer's module (F-51). **A closure created, moved or deleted re-charges the approved leave it overlaps**, and the employee is told (F-52). | Lane 1 |
| **D-16** | Who is "Management only"? | **The heads of organisation units** (`OrganizationUnit.HeadEmployeeId`), plus the organiser and participants; "Management" visibility uses the same population (F-43). | Lanes 2, 7 |
| **D-17** ✅ | The milestone link to one employee's award or certification (C-41)? | **Settled 2026-10-01, as recommended: dropped.** Company milestones are company facts: the guide's own walkthrough files the ISO 9001 quality certification as one, while a training-module certificate and a long-service award each belong to one employee. A milestone's evidence is its documents (D-3); awards and milestones meet, if anywhere, through a company event for the awards ceremony. C-41 closes as decided, not built, which is a decision rather than a deferral. | Lane 0: no link columns. Lane 4: nothing to build |
| **D-18** | Retiring a room that has future bookings? | **Offer "cancel these N bookings and tell their bookers"**, and refuse deletion once a room has any booking history — deactivate instead (F-49). | Lane 3 |

### 1c. Decisions from lane source checks

*Appended per lane as each is source-checked and settled with the user, before it is built.*

**Lane 0 (2026-10-04) — two data-step questions, settled by the user the same day: no data steps.**

| # | Question | Settled |
|---|---|---|
| **L0-1** | F-53: should the migration move rows saved against a department onto the unit with the same name? | **No — the unit replaces the department outright (D-5), and there is nothing to move.** No event or closure carries a `DepartmentId` on UAT, the dev database or the test-data database, live or deleted. The "legacy rows shown read-only and listed for HR" handling is dropped from lanes 1 and 2. `DepartmentId` stays in the schema only while today's code reads it; a later migration drops it once lanes 1 and 2 have moved every read to the unit. *The first recommendation kept a name-match step as protection for other databases; withdrawn, as it could never act.* |
| **L0-2** | D-1: should the migration rewrite old closures whose saved scope contradicts their type? | **No.** No closure contradicts D-1: UAT's one is a whole-company Full closure, and the other two databases have none. Lane 1 reads a closure's scope from its type and refuses a contradictory save from now on. |

---

## 2. Lane status

| Lane | Scope | Waits on | Status | Proof |
|---|---|---|---|---|
| **0** | Schema: one migration (series, unit and gate columns, milestone documents, unique guards, upload category; no data steps — L0-1, L0-2) | — | ✅ 2026-10-04 | applied on UAT: `__EFMigrationsHistory` 142 → 143, every object present (§ 6) |
| **1** | Closures: type drives scope through the audience resolver; `is-closure-date` fixed; leave and the statutory clocks read closures; announcements | D-15 | ☐ | `run-final-review.mjs` closures block |
| **2** | Events: validation, lifecycle guards, recurrence series, audience, in-app and email notices, attachments on the gate | D-10, D-11, D-14, D-16 | ☐ | events block |
| **3** | Rooms and bookings: rules, availability, guards, the booking lock, lapses, retirement, staff booking | D-10, D-13, D-18 | ☐ | rooms block |
| **4** | Milestones: documents, recurring projection. Fiscal: Finance's calendar, HR's retired. Company profile | — | ☐ | milestones + fiscal block |
| **5** | Screens: the shared select re-test (F-27, in HEAD), removes, event page, diaries, landing, site picker, the sidebar gate test (F-57) | — | ☐ | browser walk |
| **7** | The company calendar (HR, staff, portal), the staff event view and the self-service reply | D-13, D-16 | ☐ | calendar block, two logins |
| **6** | Harness (`run-final-review.mjs`, regression net), guide, registers, memory | every lane | ☐ | both suites green twice |

---

## 3. The headline findings (verified in source on 2026-10-01, HEAD 9f4b3206c; re-checked at HEAD 1163bbc47 on 2026-10-04 — § 3c)

Beyond the § 21 ledger. File references are to `src/ErpSystem.Core/Services/HR/CompanyScheduleService.cs`
unless stated. The service, repository, mapping and controller are unchanged at 1163bbc47, so every
line reference into them still holds; references elsewhere are given at 1163bbc47.

### 3a. From the first read

**Wrong results**
- **F-1** `MarkAttendanceAsync` re-mark path stamps `CheckInTime = now` even when `Attended = false` (:754). The create path is correct.
- **F-2** `IsClosureDateAsync` ANDs site and department (:1763-1767): a site-wide closure answers **false** for a (site, department) query because its `DepartmentId` is null. Same shape in `CompanyScheduleRepository.cs:636`.
- **F-3** `FiscalYearRepository.GetCurrentFiscalYearAsync` / `GetByYearAsync` (`CompanyScheduleRepository.cs:664-676`) are not tenant-filtered and take the first row: another tenant's current year makes `fiscal-years/current` answer null. *Closes by D-6.*
- **F-4** `GetByDateRangeAsync` for events, bookings and closures uses containment (`Start >= from && End <= to`), so a record straddling the range edge is missed. The diary's closure source already does overlap. *Review: milestones have a single date, so containment is right for them.*
- **F-5** Repository "upcoming" queries use `DateTime.Today` (server-local); the rest of the module is UTC. *Review: harmless while the server clock is on Ghana time, which is GMT all year; fixed for consistency only.*
- **F-6** `MeetingRoomService.UpdateAsync` (:1109) skips `EnsureLocationExistsAsync` and the room-code uniqueness check: a bad site is a 500, a code collision is a bare 500 from the unique index, and a blank code on edit is stored as empty.
- **F-7** `RoomBookingService.CreateAsync` (:1318) checks `IsBookable` but not `IsActive`; `EventId` is never validated.
- **F-8** Approve, cancel, complete and reschedule on events (:379-493) and approve and cancel on bookings (:1386-1417) carry **no state guard** (C-19, C-20, C-30, C-31): approving a cancelled booking yields `Confirmed` beside `IsCancelled = true`; cancelling twice overwrites the reason and emails everybody again.
- **F-9** `UpdateCompanyEventDto.Status` is written blindly (`CompanyScheduleMappingExtensions.cs:264`, C-24): Cancelled without `IsCancelled`, a cancelled event un-cancelled by edit, dates moved with no original kept (R4-7.1).
- **F-10** No create or update validation anywhere for: end before start; all-day with times; RSVP deadline after the event; `SendReminders` with a null `ReminderDaysBefore` (R4-5.1); `RequiresRsvp` without a deadline; an external participant with neither name nor email; ids from another tenant (`LocationId`, `DepartmentId`, `EmployeeId`, `AssignedToId`, `EventId`), which surface as FK 500s.
- **F-11** `RespondToInvitationAsync` (:701) accepts `NotSent`, `Sent` and `NoResponse` as an "answer", and ignores the event in the route.
- **F-12** `UpdateTaskAsync` accepts `Status = Completed` without a `CompletionDate`; `Overdue` is set by nothing (C-23).
- **F-13** Fiscal: deleting the current year leaves none (R4-2.2); closing a period twice overwrites the date; a closed period can be edited and deleted; `SetAsCurrent` works on a Closed or Archived year; periods are not checked against the year (C-48); deleting a year leaves its periods live. *Close by D-6.*
- **F-14** Recurring milestones (`IsRecurringAnnually`) never appear in "upcoming" because the query reads the original `MilestoneDate` (C-40).
- **F-15** The availability search applies seats only (R4-9.1) and its booked-room subquery is not tenant-scoped (C-28). *Review: not the only unscoped read — see F-30.*
- **F-16** The diaries attribute every closure to everybody (R4-13.1) and omit the organiser unless on the guest list (R4-6.7).
- **F-17** Ghost settings, read by nothing: `Scope`, `Visibility`, `ShowOnCompanyCalendar`, `ShowOnIntranet`, `Priority`, `EstimatedAttendees`, and `ClosureType` itself (user item 2). *Review: and `IsPaidClosure` — F-51.*
- **F-18** Cancelling an event does not release its linked bookings (C-29); a room can be deleted or deactivated with live future bookings (C-36).

**Screens**
- **F-19** Room edit shows no site on load (user item 1). **Cause corrected by the review — F-27.** The first diagnosis (a remount race) and fix (key the select on its option count) were wrong: by the time the options arrive, the form already holds a blank.
- **F-20** Eight Admin-only removes still offered to `hr.head` (R4-6.6).
- **F-21** Event page: original window unshown (R4-6.1); "Rescheduled" prints the press timestamp as if it were the date; the Reminders card promises sends the sweep will not make (R4-6.2, R4-6.3).
- **F-22** Event edit offers Cancelled, Completed and Rescheduled in its status select (C-24).
- **F-23** Diaries draw a multi-day entry on its first day only (R4-10A.1, R4-10B.2).
- **F-24** Closure form: the type is decorative; "Affects leave and attendance calculations" is false (C-5).
- **F-25** Milestones: "Related documents" is a text box (user item 3).
- **F-26** Rooms delete toast blames bookings when the answer is a 403 (C-33).

### 3b. Found by the review (2026-10-01)

**Cross-cutting**
- **F-27 · lane 5 · HR-wide** The shared `SelectField` (`frontend/src/components/hr/employee/tabs/fields.tsx`) writes a blank into the form when its value arrives before its async options. Inside a `<form>`, Radix Select 2.2.6 keeps a hidden native `<select>` (`SelectBubbleInput`): when the value changes it sets the native value and dispatches `change`; with no `<option>` for that value yet the native value reads back `''`, Radix calls `onValueChange('')`, and `SelectField` stores it. Required pickers show the placeholder (the room's site, F-19); **optional pickers (`allowEmpty`) clear, and Save writes the blank to the database**. Intermittent: with the option list still cached the options exist first. Confirmed in the library code, not yet reproduced in a browser. **✅ In HEAD since 2026-10-02:** the travel final closure met the same defect on its policy form (its finding C6) and landed this plan's guard, `if (next === '') return;` (`fields.tsx:340`, commit `19f20f2f2`). Lane 5 re-tests the room edit page in a browser; nothing to build.
- **F-28 · lane 1** Leave's holiday set is tenant-wide: `LeaveService.GetChargeableDaysAsync` takes no employee, `LeaveUsageReader` loads one set and reuses it for every employee, and `LeaveReminderService` uses one set for every plan. A per-employee closure cannot be unioned into them, as the first draft planned. *At 1163bbc47: `GetChargeableDaysAsync` `LeaveService.cs:3941` (the holiday read :3949; `CalculateLeaveDaysAsync` :3956, called from create, update, recall, extension and reschedule); `LeaveUsageReader.cs:82`; `LeaveReminderService.cs:966`. The travel closure added `MarkTravelConflictsAsync` to LeaveService, which shifted every line after :1804 and does not touch day-counting.*
- **F-29 · lane 1** `IHrWorkingDayCalculator` ignores closures, so the discipline statutory clocks count a company-wide shutdown as working days. *At 1163bbc47 the calculator has a new consumer: travel's `StaffTravelAttendancePosting` (`:171`, travel lane 9a) skips its holidays when it posts trip days to attendance as on duty. Once company-wide non-working closures join that set (lane 1), travel will stop posting on-duty days on a closure as well, the same way it treats holidays. Lane 1's source check confirms that is wanted.*
- **F-30 · lane 2** Most repository list reads (events by range, organiser, status, category and upcoming; rooms by location, available and active; bookings by room, booker, range, status and pending; milestones; closures) load every tenant's rows and filter in memory. The guide's "C-28 is the only unscoped read" is wrong.

**Notifications**
- **F-31 · lane 2** The module raises no in-app notification at all; every notice is email only, and the demo database has no mail server. HR's in-app path for named recipients exists: `HrAnnouncementService.NotifyAudienceAsync` raises an `EntityActivityEvent` carrying `RecipientUserIds` on a topic seeded with in-app on.
- **F-32 · lane 2** `SendForTenantAsync` returns whether the email went; `NotifyParticipantsAsync` ignores it, so "sent", "reminded" and "chased" count attempts (R4-6.3).
- **F-33 · lane 2** Invitations are emailed at once for an event that still awaits approval, and "Send reminder now" and "Chase unanswered now" work on such an event, although the sweep refuses it.
- **F-34 · lanes 1, 2, 3** These tell nobody: a booking awaiting approval, a booking approved or cancelled, an event approved or postponed, an event's time, venue or meeting link changed, a participant removed, a task assigned or overdue, a closure announced.
- **F-35 · lane 2** Leavers are still invited and reminded: neither the add nor the send loop checks that the employee is active.
- **F-36 · lane 2** External guests are asked to confirm and given no way to answer.

**Events**
- **F-37 · lane 2** Edit compares only `StartDate`: a change of time, end date or the all-day switch is not treated as a move, so nobody is told and the reminder stamp stands.
- **F-38 · lane 2** Rescheduling leaves loose ends: the RSVP chase stays stamped after answers are reset, the RSVP deadline can fall after the new start, linked room bookings stay at the old time, and an approved event keeps its approval.
- **F-39 · lane 2** Deleting an event leaves its live bookings holding their rooms.
- **F-40 · lane 2** An event can be completed, and attendance marked, before it starts.
- **F-41 · lane 2** An event that needs no approval stays `Scheduled`, and the clash check calls an event firm only when `Confirmed` or `InProgress` — so it is never firm, even when everyone has accepted.
- **F-42 · lane 2** The organiser is whoever is signed in. Once organisers reach the diaries (R4-6.7), the HR officer who keyed in the board meeting is shown as committed to it.
- **F-43 · lanes 2, 7** "Management only" scope and "Management" visibility are defined nowhere; scenario 110's board meeting uses both.
- **F-44 · lane 2** The "Holiday" and "Milestone" event categories suggest effects they do not have: the office stays open, no milestone is recorded.
- **F-45 · lane 0** Participants and attendance rows have non-unique indexes and a check-then-insert, so two simultaneous requests can add the same employee twice.
- **F-46 · lane 2** Only creates re-read after saving; an update's response can carry a blank or stale site, unit, approver or assignee name.

**Rooms and bookings**
- **F-47 · lane 3** Two people can book the same slot at the same moment: the clash check and the insert are not locked together.
- **F-48 · lane 3** A Tentative booking whose start has passed never lapses; it stays under "awaiting approval" forever.
- **F-49 · lane 3** Deleting a room hides its booking history: the register joins each booking to its required room, and the soft-delete filter removes deleted rooms from that join.
- **F-50 · lane 3** Booking instants are written as UTC and read back unmarked, so a browser outside GMT shifts them.

**Closures**
- **F-51 · lane 1** `IsPaidClosure` ("Staff are paid") is read by nothing; an unpaid closure has to reach payroll.
- **F-52 · lane 1** Leave approved before a closure was added keeps its full charge: the request's days are fixed at approval.
- **F-53 · lane 0** Nothing links a `Department` to an `OrganizationUnit`, so existing department-scoped events and closures have no automatic home under D-5, and old department closures break D-1.

**Data, documents, fiscal**
- **F-54 · lanes 2, 6** Scenario 110 attaches event files as JSON paths, which breaks once attachments are uploads; existing path-only rows cannot be downloaded, because `HrDocumentDownload`'s legacy fallback knows only older folders.
- **F-55 · lane 4** The free-text `CompanyProfile.LogoUrl` is substituted into offer, probation, asset and interview letters and emails — the hazard the seal and signature uploads were built to remove — and any `HR.Company.Write` holder can set it.
- **F-56 · lane 4** D-6 misses a consumer: the manpower budget form computes its fiscal period from HR's start month (`fiscalPeriodFor`). And Finance's fiscal-year read documents a Finance permission it does not yet enforce, so a screen calling it directly would break the day it does. *At 1163bbc47 (`src/ErpSystem.Api/Controllers/Finance/FiscalPeriodController.cs`): `GET fiscal-years` :61, `fiscal-years/{id}` :90 and `fiscal-periods` :350 still document "Requires Finance.Read" and carry only the class-level `[Authorize]`; the merge added `GET fiscal-years/{id}/book-close-cycles` (:292), which does enforce `ViewFinance`, so the enforcement is arriving. `fiscalPeriodFor` is `ManpowerBudgetFormFields.tsx:21`, used there at :249 and by `manpower-budgets/new/page.tsx:49-51`.*

### 3c. Re-checked at HEAD 1163bbc47 (2026-10-04)

The plan was committed in `fe14a9ca0` on top of 9f4b3206c. hrdev then took the travel final closure
(lanes 1c–10) and master merge #13 (`1163bbc47`, PRs #276–#350). The re-check compared 9f4b3206c with
HEAD file by file.

**Unchanged — every finding stands as written:** `CompanyScheduleService.cs`,
`CompanyScheduleRepository.cs`, `CompanyScheduleMappingExtensions.cs`, `CompanyScheduleController.cs`,
every `frontend/src/app/hr/company-schedule/` screen, `HrWorkingDayCalculator.cs`,
`HrAudienceResolver.cs`, `LeaveUsageReader.cs`, `LeaveReminderService.cs`,
`IControlledFileUploadService.cs`, `HrAttachmentUpload.cs`, `HrAnnouncementService.cs`. Both
migration templates lane 0 names are still in the tree.

**Changed:**
- **F-27 is fixed in HEAD** (travel C6, `19f20f2f2`; § 3b). Lane 5 is a browser re-test.
- **`LeaveService.cs`** gained the travel-conflict marker; its line numbers moved (F-28 updated).
  Day-counting is untouched.
- **Finance's fiscal controller** changed: the year-end close is now per accounting book (an exact
  book and an idempotency key; `GET fiscal-years/{id}/book-close-cycles`, on `ViewFinance`), and
  `FiscalYears` gained a unique `(TenantId, Year)` index (`20261001231335_AddFiscalYearTenantYearInvariant`).
  Its three reads are still unenforced (F-56 updated). For lane 4 this means one Finance year per
  year label in a tenant, so `IHrFiscalCalendar`'s lookup is unambiguous. **The card's "status" is
  now a lane 4 source-check question:** a year's own `Status` / `IsClosed` against its per-book close
  cycles.
- **`ApplicationDbContext.HR.cs`** gained performance, probation and travel configuration. Nothing
  in it touches this module; lane 0's additions go beside the company-schedule block as planned.
- **A new working-day consumer**, travel's attendance posting (F-29 updated).

**Corrected in the plan:**
- **Lane 2, C-51:** there is no `EmergencyDrillService`. Drills are recorded by `SheEmergencyService`
  (`src/ErpSystem.Core/Services/HR/SafetyEmergencyGovernanceServices.cs`: `AddDrillAsync` :294,
  `UpdateDrillAsync` :312, `DeleteDrillAsync` :323).
- **Lane 2, notifications:** the travel closure built `StaffTravelNotices` (travel lane 8a,
  `src/ErpSystem.Core/Services/HR/StaffTravelNotices.cs`). It is a closer model for this module's
  per-event notices to named people than the announcement broadcast:
  - one topic per event and audience;
  - in the app and by email for a person with a login, by email alone for a person without one, and
    the desk told when nobody can be reached;
  - never told of one's own act;
  - never failing the act;
  - tenant-explicit for the sweep.

  `HrPendingApprovers` resolves the people an engine stage is waiting on (D-10).

**New:**
- **F-57 · lane 5** `frontend/src/components/layout/sidebar-hr-gates.test.ts` fails on
  `/hr/company-schedule/my-schedule` (`sidebar.tsx:1328`). The entry is deliberately ungated, because
  the server takes the employee from the token (round 4, D5). But it was never added to the test's
  `KEEP_OPEN` list, and the test's own rule says a self surface must be listed there with its reason.
  The travel closure recorded the failure as pre-existing and left it. `/hr/leave/calendar` fails the
  same test; that entry belongs to leave and is not this plan's. Lane 7's Company Calendar entry and
  lane 5's opened Team Schedule entry (R4-10B.3) must join `KEEP_OPEN` in the same change that ungates
  them.

**Migration state:** UAT holds 142 history rows, the repo's chain exactly, since merge #13 was applied
on 2026-10-04 with the user's go. Lane 0's migration will be the only one pending on UAT.

---

## 4. Lanes

### Lane 0 — Schema

One migration, `CompanyScheduleFinalReview`. The user scaffolds it; the body is rewritten as guarded
SQL — the column, index and foreign-key helpers are in
`SupersededMigrationsArchive/PreBaselineLateMerges/20260910013025_AddSubRecordGeographyAndRelationshipTypes.cs`,
the guarded data step in `20260930151521_PerformanceClosureRetireCycleInProgress.cs` (*review: the first
draft named only the data migration*); Designer and snapshot kept; the user builds; applied to UAT after
the user's go.

- [x] **Before writing it**, count on UAT (read-only): events and closures with a `DepartmentId`;
      closures whose type and scope disagree with D-1; duplicate (event, employee) participant and
      attendance rows. Each count decides a step below.
- [x] `CompanyEvent`: `RecurrenceSeriesId` (guid?), `OccurrenceNumber` (int?), `OrganizationUnitId`
      (guid?, FK `OrganizationUnits`, Restrict); index on the series id. **No series table (D-12):** the
      occurrences are the series.
- [x] `CompanyEvent` (D-9, C-51): `SourceEntityType` (nvarchar(50)?) and `SourceEntityId` (guid?) —
      the record that created the event, today the SHE emergency drill.
- [x] `BusinessClosure`: `OrganizationUnitId` (guid?, FK `OrganizationUnits`, Restrict).
      (D-9, C-38) `RecursAnnually` (bit NOT NULL DEFAULT 0).
- [x] **Legacy rows (F-53) — none; no data step (L0-1, L0-2).** The counts found no row with a
      `DepartmentId` and no closure contradicting D-1, so the unit replaces the department outright.
- ~~`CompanyMilestone`: `LinkedEntityType` / `LinkedEntityId`~~ — **not added: D-17 dropped the link.**
- [x] `CompanySealAssetKind` gains `Logo` (D-9, C-50) — an enum value, no column.
- [x] `EventAttachment`: `UploadedById` (guid?, FK `Employees`), `FileSizeBytes`, `FileUploadRecordId`,
      `DocumentRecordId`, `DocumentVersionId` — the `StaffRequisitionAttachment` pattern.
- [x] `CompanyMilestoneDocument` (new): `MilestoneId` FK, `FileName`, `FilePath`, `Description`,
      `UploadDate`, `UploadedById` FK `Employees`, `FileSizeBytes`, `FileUploadRecordId`,
      `DocumentRecordId`, `DocumentVersionId`.
- [x] **(F-45)** Unique filtered indexes: `EventParticipants (TenantId, EventId, EmployeeId)` where
      `EmployeeId IS NOT NULL AND IsDeleted = 0`, and `EventAttendances (TenantId, EventId, EmployeeId)`
      where `IsDeleted = 0`. The guard THROWs naming any duplicates rather than choosing which to lose.
- [x] `ControlledFileUploadCategories.HrCompanyScheduleAttachments = "hr-company-schedule-attachments"`,
      in the constants AND in the `SystemCleanScanRequired` block
      (`src/ErpSystem.Core/Interfaces/IControlledFileUploadService.cs`).
- [x] `ApplicationDbContext.HR.cs`: DbSet and configuration for the new table(s); FK configuration for
      the new columns.

**State (2026-10-04): ✅ DONE — applied to UAT, 143 history rows.** The counts were taken; the
entities, enum, upload category and DbContext were changed. The user scaffolded
`20261004224953_CompanyScheduleFinalReview`; its body was rewritten as guarded SQL and proved on a
restored copy of UAT. The user's build was green, with no pending model changes. With the user's go,
it was applied to UAT (§ 6).

*UAT counts, read-only, 2026-10-04 (`ErpSystemDB_UAT`, 142 history rows, one tenant):*

| What | UAT | Decides |
|---|---|---|
| Live events / closures with a `DepartmentId` | 0 of 4 / 0 of 1 (dev and test-data DBs: no events or closures at all) | L0-1: no step |
| Deleted events / closures with a `DepartmentId` | 0 / 0 | L0-1: no step |
| Closures disagreeing with D-1 | 0 — the one closure is Full, company-wide, non-working, paid | L0-2: no step |
| Duplicate live (tenant, event, employee) participant / attendance groups | 0 / 0 (17 participants, 1 external; 5 attendance rows) | F-45 guard passes |
| Live event attachments, all path-only (F-54) | 4 | lane 2 shows them "reference only"; no migration step |
| Recurring events / milestones with `RelatedDocuments` text | 0 / 0 | nothing to carry over |

*Made in source (unstaged until the migration joins it):*
- `CompanyScheduleEntities.cs`: the event's series, unit and source columns; the closure's unit and
  `RecursAnnually`; the attachment's upload-gate columns; `CompanyMilestoneDocument` with
  `CompanyMilestone.Documents`.
- `CompanySealAssetKind.Logo = 3`.
- `HrCompanyScheduleAttachments`, declared and in `SystemCleanScanRequired`.
- `ApplicationDbContext.HR.cs`: the DbSet, three Restrict FKs, the two F-45 unique filtered indexes
  (`UX_EventParticipant_Tenant_Event_Employee`, `UX_EventAttendance_Tenant_Event_Employee`), and
  indexes on the series id and the unit ids.
- **Beyond the list above:** an index on `(SourceEntityType, SourceEntityId)` for the drill's lookup
  of its event.
- `CompanyMilestoneDocument.UploadedById` is required, as `HrAttachmentUpload` always supplies one;
  `EventAttachment.UploadedById` is nullable for the four legacy rows.
- ⚠ From this commit, the Admin-only `POST company-profile/seal-assets/{kind}` accepts `Logo`, because
  the route binds the enum. Nothing reads a logo asset until lane 4.

### Lane 1 — Closures (D-1, D-4, D-5, D-15; C-5, C-37, C-38, C-39, R4-10A.4, R4-13.1, F-2, F-24, F-28, F-29, F-51, F-52)

- [ ] One validator in `BusinessClosureService` for create and update: the D-1 matrix; `EndDate` on or
      after `StartDate`; site and unit must exist in the tenant; an overlapping closure of the same
      scope refused with a sentence naming the other (C-39), recurrence included; a closure over a
      public holiday warned.
- [ ] **Scope through the existing audience resolver** (*review: the first draft invented a new scope
      helper*). A closure's scope is an audience rule — `AllEmployees`, `Location` or
      `OrganizationUnit`, which already reaches the unit's subtree — evaluated with
      `IHrAudienceResolver.IncludesAsync` / `ResolveAsync`. ⚠ A `Location` rule matches one location
      exactly; see C-16 in lane 5. Used by:
      - `IsClosureDateAsync` — comparing dates, not date-times (a time on the closure's last day answered
        false); a query with no scope answers company-wide closures only (C-37);
      - `ClosureCommitmentSource` (R4-13.1);
      - leave, below.
- [ ] **Leave, redesigned by the review (F-28).**
      - Company-wide closures that are not working days join `IHrWorkingDayCalculator`'s holiday set, so
        leave, the discipline statutory clocks (F-29) and the diary agree with no change at those call
        sites. So does travel's on-duty posting (`StaffTravelAttendancePosting`, § 3c). The source
        check confirms that a trip day on a closure should not post as on duty.
      - Site and unit closures become a per-employee overlay: the employee is threaded through
        `GetChargeableDaysAsync` / `CalculateLeaveDaysAsync`, and `LeaveUsageReader` and
        `LeaveReminderService` keep the company set once plus an overlay per employee.
      - The leave calendar draws them as "Closure: title".
- [ ] **(D-15, F-52)** A closure created, moved or deleted re-charges the approved leave it overlaps;
      the employee is told.
- [ ] **(D-15, F-51)** The pay flag is exposed to payroll read-only; entry in
      `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
- [ ] **(F-34)** A new or changed non-working closure is announced to the staff it covers — an
      `HrAnnouncement` with the closure's audience rule, which raises the in-app topic (and email where
      the topic has it on).
- [ ] **(R4-10A.4, the holiday half — in no lane before the review)** The diary's and the calendar's
      holidays come from `IHrWorkingDayCalculator` — the default calendar, active, mandatory and
      substitute days — not from every calendar.
- [ ] The attendance side logged in `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` (A-92).
- [ ] (D-9, C-38) `RecursAnnually`: the resolver path, `is-closure-date`, the diary source, the leave
      reader and the calendar all treat a recurring closure as covering the same month and day in
      every later year; the form has the switch ("Recurs every year — the year-end stocktake is typed
      once"); the register says "Every year".
- [ ] `closures/page.tsx`: the type decides what renders (Full → nothing; Site → the site select,
      required; Organisation unit → `OrganizationUnitPickerField`, required; Partial → the whole-company
      switch plus both pickers, and the working-day switch locked on, described). The working-day
      switch's description becomes honest. `organizationUnitId` / `organizationUnitName` replace
      `departmentId` on the wire. No legacy department rows exist (L0-1), so nothing is shown for them;
      once lanes 1 and 2 read only the unit, a later migration drops `DepartmentId` from both tables.

**State:** *(filled when it lands)*

### Lane 2 — Events (D-2, D-3, D-5, D-10, D-11, D-12, D-14, D-16; C-7, C-10…C-15, C-18…C-25, C-29, C-51, R4-5.1, R4-6.3, R4-6.4, R4-6.7, R4-7.1, R4-10A.2, F-1, F-8…F-12, F-30…F-46, F-54)

**Validation and references**
- [ ] One window validator for create, update and reschedule (end on or after start; times only when
      not all-day, end time after start time on a same-day event; RSVP deadline on or before the
      start; `SendReminders` ⇒ `ReminderDaysBefore` ≥ 0; `RequiresRsvp` ⇒ a deadline).
- [ ] `EnsureExistsAsync` (tenant-scoped, not deleted, refusing as a rule — a 422, not a "not found")
      for `LocationId`, `OrganizationUnitId`, participant `EmployeeId` (active), task `AssignedToId`.

**Lifecycle** (*review: two of the first draft's guards contradicted other rules*)
- [ ] **Approve:** requires approval and not yet approved, while Scheduled, Rescheduled or Postponed —
      the first draft's "Scheduled only" stranded a moved event, since a reschedule now sets
      Rescheduled. On the engine per D-10.
- [ ] **Cancel:** not cancelled or completed; cascades to the event's live linked bookings.
- [ ] **Reschedule:** not cancelled or completed; window valid; status → Rescheduled (C-7);
      Accepted/Tentative answers reset to Sent **and the RSVP chase stamp cleared**; refused when the
      RSVP deadline would fall after the new start unless a new deadline is given; **linked live
      bookings move with the event**, each re-checked for clashes, the reschedule refused naming any
      room that is taken; an approved event that moves returns to awaiting approval, as a moved booking
      does (D-10). (F-38)
- [ ] **Complete:** not cancelled, not already completed, **not before the event starts** (F-40).
- [ ] **Delete** (Admin): cancels the event's live linked bookings first, as cancel does (F-39).
- [ ] **Update:** refused on a cancelled or completed event. `Status` only Scheduled / InProgress /
      Postponed, and Confirmed only where no approval is required — the first draft allowed Confirmed,
      which made an unapproved event look approved. **Any change of start or end date or time, or of
      the all-day switch, goes through the reschedule path** (F-37, R4-7.1).
- [ ] **(F-41)** The clash check treats an accepted invitation to an event that needs no approval as
      firm once it is Scheduled or Rescheduled — no data change.

**Recurrence (D-2, D-12 — settled: a light series)**
- [ ] Create: a pattern and a count or end date required, up to 52 occurrences. Each occurrence is a
      full event with its own number, sharing `RecurrenceSeriesId` and carrying `OccurrenceNumber`.
      Dates are counted from the first, so a monthly rule on a day a month lacks falls on that month's
      last day; an occurrence on a public holiday or a company-wide closure is generated and flagged on
      its page.
- [ ] A scope choice — "this occurrence / this and following / the whole series" — on adding a guest,
      removing a guest, editing, rescheduling (the following move by the same offset) and cancelling.
      **A series action never changes an occurrence that is past or completed.**
- [ ] A guest added with series scope gets **one** invitation listing the dates (a new catalogue entry,
      `EventSeriesInvitation`), and answers each date or all at once — through the desk door and the
      D-8 reply door alike. Reminders and the RSVP chase stay per occurrence. If D-14 is taken, each
      occurrence carries its own calendar entry.
- [ ] "Book this room for every occurrence": one booking per date through lane 3's rules and lock;
      the dates where the room is taken are listed, the rest booked.
- [ ] "Extend the series": more occurrences after the last, on the same rule, within the cap.
- [ ] DTO gains `recurrenceSeriesId`, `occurrenceNumber`, `occurrenceCount`; the page shows
      "Occurrence 3 of 10" and links the series; the register filters by series.

**Audience and organiser** (*review: reuse the audience resolver; no new `EventAudience` helper*)
- [ ] The organiser per **D-11**; the organiser and the participants always see and are committed to
      the event.
- [ ] When `ShowOnCompanyCalendar` is on, the event's audience is an audience rule: `Scope = AllStaff`
      → `AllEmployees`; `Scope = Department` → the event's `OrganizationUnit` (its subtree);
      `ManagementOnly` and `Management` visibility per **D-16**; Private and Confidential reach
      participants and the organiser only. Evaluated with `IHrAudienceResolver`; used by
      `CompanyEventCommitmentSource` and lane 7. Form descriptions say exactly this.
- [ ] `ShowOnIntranet` publishes an `HrAnnouncement` to the same audience (*review: the first draft
      only relabelled it*); `Priority` stays a label and says so.
- [ ] **(R4-10A.2 — backend, moved here from lane 5)** `CompanyEventCommitmentSource` yields a window
      per day for a multi-day timed event (`WindowOf` takes the start day only today), so days two
      onward reach the clash check and the diaries.

**Participants, attendance, tasks**
- [ ] Participants: an external guest needs a name AND an email; duplicate external email refused; no
      adds to a cancelled or completed event; no inactive employee (F-35); replies limited to Accepted /
      Declined / Tentative, on the event in the route (F-11). **Removal moves to `Write`** — uninviting
      is organiser work — and the person is told.
- [ ] Attendance: the re-mark bug (F-1); no check-out before check-in or twice; none on a cancelled
      event or **before the event starts**.
- [ ] Tasks: `Completed` through update sets `CompletionDate`; complete-task guard; `Overdue` computed
      on read; the assignee is told on assignment and chased by the hourly sweep when overdue (F-34).
- [ ] (D-9, C-21) `DELETE events/{eventId}/attendance/{attendanceId}` on `Write`; (C-22)
      `PUT participants/{id}` on `Write` — role, required, special requirements, and the external
      guest's name, email and organisation.

**Notifications** (F-31…F-36; *review: the first draft only reworded the Reminders card*)
- [ ] Every company-schedule notice also goes **in-app** to internal recipients: an
      `EntityActivityEvent` with the recipients in `Data["RecipientUserIds"]`, on topics seeded with
      in-app on — the pattern in `HrAnnouncementService.NotifyAudienceAsync`. *At 1163bbc47 a closer
      model exists, travel's `StaffTravelNotices` (§ 3c): one notices class owning every topic, login /
      email-only / unreachable handled, the actor left out, never failing the act. Choose between the
      two at the source check.*
- [ ] Deliveries are counted from the email result; the Reminders card and the toasts show issued and
      delivered (R4-6.3).
- [ ] Invitations wait while an event awaits approval and go on approval; "Send reminder now" and
      "Chase unanswered now" refuse such an event, as the sweep does (F-33).
- [ ] New notices: event approved (to the organiser), postponed, changed in time, venue or link,
      participant removed.
- [ ] Every send skips inactive employees (F-35).
- [ ] Calendar invites and external replies per **D-14** (F-36).

**Reads and contracts**
- [ ] Range endpoints: overlap semantics; UTC dates throughout the repository; **the tenant filter
      inside every repository query** (F-30).
- [ ] Update responses re-read after save, as creates do (F-46).
- [ ] (F-44) "Holiday" and "Milestone" leave the category picker (the values kept for old rows), with
      a line pointing to public holidays and milestones.
- [ ] `events/department/{id}` → `events/unit/{id}`; the event form's Department picker →
      `OrganizationUnitPickerField`.
- [ ] (D-9, C-10…C-13, C-25) `GET events/search` and `GET bookings/search` — server-side filters
      (text, status, category, site, unit, organiser, from/to on overlap), sort and paging; `GET
      events/export` and `GET bookings/export` answering `text/csv` for the same filters (the
      `LeavesController` export precedent); `GET CompanySchedule/dashboard` — the landing's four lists
      and counts in one read. The two registers move onto them with page controls and a date filter.
- [ ] (D-9, C-15) `GET events/clashes?start&end&excludeId&scope&unit&site` listing the live events
      that overlap; create, update-with-dates and reschedule refuse an AllStaff-against-AllStaff (or
      same-unit Department) overlap on the same site or company-wide, with a sentence naming the
      other event; the form shows the rest as warnings before save.

**Attachments and the drill**
- [ ] Attachments on the gate: multipart `POST events/{id}/attachments` through
      `HrAttachmentUpload.ExecuteAsync`; `GET attachments/{id}/download` through
      `HrDocumentDownload.ServeAsync` after proving the event is the caller's tenant's;
      `CreateEventAttachmentDto` deleted; `AttachmentsPanel` becomes an upload panel with download
      links (the recruitment requisition pattern). **Legacy path-only rows read "reference only — no
      file stored", with no download link (F-54).**
- [ ] (D-9, C-51) `SourceEntityType` / `SourceEntityId`: `SheEmergencyService` (`AddDrillAsync` /
      `UpdateDrillAsync` / `DeleteDrillAsync` in `SafetyEmergencyGovernanceServices.cs`; *the first
      text named an `EmergencyDrillService` that does not exist*) creates a company
      event (category CompanyEvent, type Internal, organiser = the drill's coordinator, site = the
      drill's location, all-day on `NextDrillScheduledDate`, name "Emergency drill: plan name") when a
      drill is recorded with a next date, and moves it when that date changes; the event page shows
      "From emergency drill DRL-… (plan)" with a link; deleting the drill cancels the event.

**State:** *(filled when it lands)*

### Lane 3 — Rooms and bookings (D-10, D-13, D-18; C-8, C-28, C-30, C-31, C-32, C-33, C-36, R4-9.1, R4-12.1, F-6, F-7, F-15, F-18, F-34, F-47…F-50)

- [ ] Room update validates the site and the code's uniqueness (deleted rooms included, R4-12.1); a
      blank code is regenerated.
- [ ] **Retiring a room per D-18** (F-49): deactivating or deleting a room with future bookings offers
      "cancel these N bookings and tell their bookers"; deletion is refused once the room has any
      booking history — deactivate instead.
- [ ] Availability: tenant-scoped; `MaxBookingDurationHours` and `AdvanceBookingDays` applied; active
      and bookable only.
- [ ] Booking create: room active; `EventId` validated; a linked event's dates must contain the
      booking; seat check uses the larger of the booking's attendees and the event's estimate.
- [ ] **(F-47)** Create and update hold an application lock per room (`sp_getapplock` inside a
      transaction) around the clash check and the write.
- [ ] Booking update refused on a cancelled or completed booking; moving an approved booking's window
      on an approval-required room returns it to Tentative and clears the approval.
- [ ] **Approve:** Tentative and not cancelled — *review: the first draft also required the room to
      need approval, which strands bookings made before that switch was turned off*; on the engine per
      D-10. **Cancel:** not cancelled or completed.
- [ ] **(F-48)** The hourly sweep lapses a Tentative booking whose start has passed — cancelled with
      "Not approved before it started", the booker told.
- [ ] `Completed` by the hourly sweep for past Confirmed bookings; "Mark no-show" for HR on a past
      booking.
- [ ] **(F-34)** The approver is told a booking awaits them (or the engine inbox, D-10); the booker is
      told when it is approved, cancelled, lapsed or marked no-show — in-app and email.
- [ ] **(F-50)** Booking instants read back as UTC (`DateTime.SpecifyKind`), as the reminder stamps
      already are.
- [ ] **Staff booking per D-13**: a self-service door for the signed-in employee's own bookings.
- [ ] Rooms page: the delete toast says 403 when it is one; Delete hidden without Admin.
- [ ] (D-9, C-32) Delete on the booking detail page, Admin-only and hidden otherwise, beside the
      register's.

**State:** *(filled when it lands)*

### Lane 4 — Milestones, fiscal years, company profile (D-3, D-6, D-17; C-40, C-41, C-42…C-50, R4-2.2, F-3, F-13, F-14, F-25, F-55, F-56)

- [ ] `CompanyMilestoneDocument` entity, repository, service (add, list, delete); endpoints
      `POST milestones/{id}/documents` (multipart, Write), `GET milestones/{id}/documents` (Read),
      `GET milestones/documents/{docId}/download` (Read), `DELETE milestones/documents/{docId}`
      (Admin). `RelatedDocuments` stays as "References".
- [ ] The milestone dialog gains a Documents section after save: the list with download links and a
      multi-file upload control.
- [ ] Recurring milestones projected onto their next anniversary in upcoming and range reads; DTO
      gains `nextOccurrence` and `yearsSince`.
- ~~C-41, the milestone link~~ — **closed by D-17: not built.** The certificate is a milestone
      document (above); nothing to build here.
- [ ] **Fiscal (D-6).** An HR backend read of Finance's `FiscalYears` and periods, in-process —
      *review: the first draft had the browser call Finance's endpoint, which documents a Finance read
      permission it does not yet enforce (F-56)*. The two HR screens are replaced by one read-only
      **Fiscal calendar** card on it (years, status, periods; "Set up and closed in Finance", a link to
      Finance's screen). The HR `fiscal-years` / `periods` endpoints and `FiscalYearService` are removed
      from the controller and registration; tables and DTOs untouched this slice. *At 1163bbc47 Finance
      closes a year per accounting book (§ 3c): what the card calls a year's status — the year's own
      `Status` / `IsClosed`, or its book-close cycles — is settled at this lane's source check.*
- [ ] `IHrFiscalCalendar` answering the fiscal year for a date from Finance's years, falling back to
      `FiscalYearStartMonth` only when Finance has none; `StaffRequisitionService` uses it, **and so
      does the manpower budget form's fiscal period (`fiscalPeriodFor`, F-56)**; the settings screen
      calls the month the fallback.
- [ ] `110-company-schedule.mjs` stops seeding an HR fiscal year; `run-slice3.mjs` drops its fiscal
      assertions.
- [ ] (D-9, C-49, C-50) Company profile: the two seal buttons hidden without `HR.Company.Admin`;
      `CompanySealAssetKind.Logo` as a third versioned asset through the same replace/retire doors.
      **(F-55) The free-text `LogoUrl` is retired once the asset exists**: letters and emails read the
      logo asset, then `Tenant.LogoUrl`; the field leaves the profile form.

**State:** *(filled when it lands)*

### Lane 5 — Screens (C-1, C-16, R4-3.1, R4-6.1, R4-6.2, R4-6.3, R4-6.6, R4-10A.1, R4-10A.3, R4-10B.1…R4-10B.4, F-19…F-23, F-26, F-27, F-57)

- [ ] **The shared select (F-27) — a browser re-test; the guard is in HEAD.** The review's fix, ignoring
      `next === ''` in `SelectField`'s `onValueChange`, landed with the travel final closure (C6,
      `19f20f2f2`, `fields.tsx:340`). Re-test: a cold load of the room edit page with the location list
      not cached shows the site, and a Save keeps it; an optional picker on the event edit page survives
      a cold load and a Save. Only if the site is still blank: the room edit page seeds the form once
      both the room and the locations are loaded.
- [ ] **(F-57)** `/hr/company-schedule/my-schedule` joins `KEEP_OPEN` in `sidebar-hr-gates.test.ts`
      with its reason (self-service, keyed on the caller's token, round 4 D5), so the test passes for
      this module's entries. The opened Team Schedule entry (R4-10B.3, below) joins in the same change.
- [ ] `allowRemove={canDelete}` on the attachments and tasks panels and the closures / milestones tabs;
      the participants and attendance panels offer remove on `Write` (lane 2); room Delete hidden
      without `HR.Company.Admin`.
- [ ] Event detail: "Originally …" from the four original fields; "Moved on (timestamp) — reason"; the
      Reminders card states the sweep's own conditions (awaiting approval, Days before unset, the event
      past) and shows issued and delivered (lane 2).
- [ ] Event edit: the status select limited to what Update accepts.
- [ ] `my-schedule` and `team`: a multi-day entry drawn on every day it covers inside the range;
      entries that began before the range still drawn. (The backend half is lane 2's R4-10A.2.)
- [ ] (D-9, R4-10A.3) `PersonalScheduleDto` and `TeamScheduleDto` carry `incompleteSources`; both
      pages show a banner naming what could not be read.
- [ ] (D-9, R4-10B.1) "Schedule for this unit" on the team page opens the event form pre-filled
      (scope Department, the unit, the chosen day). (R4-10B.3) `team-schedule/{unitId}` is also
      allowed to the unit's head (`OrganizationUnit.HeadEmployeeId`, or the head of an ancestor) without
      `HR.Company.Write`; the sidebar entry opens to everybody, the unit list offers only what the
      caller may read, and the page says who may read what. (R4-10B.4) A sub-unit filter and a "direct
      members only" switch, with the member count shown.
- [ ] **(R4-3.1, live)** The landing: a card the caller cannot read says so, or is hidden — never
      "Nothing scheduled" on a 403.
- [ ] **(C-16, recommendation to confirm)** The site picker offers only location levels that allow
      employee assignment, keeping the level suffix — the audience resolver matches a location exactly
      (lane 1).

**State:** *(filled when it lands)*

### Lane 7 — The company calendar (D-7, D-8, D-13, D-16; C-9, C-26, C-35, R4-6.4)

- [ ] `GET CompanySchedule/calendar?from&to` (any signed-in internal user; 62-day cap) returning
      `CalendarEntryDto` rows: kind (Event, Closure, Milestone, Holiday, RoomBooking, Mine), label,
      start, end, all-day, colour key, reference, link target. `HR.Company.Read` holders see every
      event, closure, calendar milestone, holiday and booking; everybody else sees what the audience
      rules allow them (lane 2, D-16) and the closures that cover them (lane 1), plus calendar milestones
      and holidays, with their own diary (leave, travel, interviews, training, bookings) overlaid as
      Mine. **Closures and holidays appear once**, in the company layer, not again in Mine.
- [ ] **A staff view of an event** (*review: staff clicking through would otherwise land on HR-only
      pages and be refused*). `GET CompanySchedule/calendar/events/{id}` for anyone in the event's
      audience: name, when, where, link, organiser, and the caller's own invitation and answer; the
      meeting password only to participants and the organiser; no budget, no other guests' answers.
      Page `/me/calendar/events/[id]`; HR keeps the full page.
- [ ] `POST events/{id}/participants/{participantId}/reply` — the invitee's own answer (Accepted /
      Declined / Tentative, optional comment), any signed-in internal user, refused unless the
      participant row's `EmployeeId` is the caller's (D-8), **and refused on a cancelled or completed
      event and after the RSVP deadline**; the organiser is told of each answer. On a series it answers
      one date or all of them (D-12). The HR-desk `respond` door is unchanged.
- [ ] `/hr/company-schedule/calendar` (sidebar "Company Calendar", no permission — **listed in
      `KEEP_OPEN` with its reason, F-57**) and `/me/calendar`
      in the portal: month and week views as a hand-built grid with bands per entry broken at week
      boundaries (the `LeaveCalendar.tsx` approach; no new dependency); kind filters; legend;
      click-through; "Today"; an invitation entry offers Accept / Decline / Tentative to its invitee;
      a series shown per D-12; a room filter, which is the room view C-26/C-35 asked for; staff
      booking per D-13.
- [ ] The landing page's summary cards link to the calendar.

**State:** *(filled when it lands)*

### Lane 6 — Harness, guide, registers, memory

- [ ] `dev-harness/hr-company-schedule/run-final-review.mjs`: surname-identified fixtures of its own;
      minted logins switched off in `finally`; no "first N employees"; minted leave types switched off
      after. One assertion per rule above, in both positions, including the review's: leave across a
      scoped closure for two employees at different sites; a company-wide closure moving a discipline
      deadline; invitations held until approval; in-app notices counted by recipient; two parallel
      bookings for one slot; a lapsed Tentative booking; the staff event view's redaction; the reply
      door's refusals. Run twice from different states.
- [ ] If D-10 chooses the engine: definitions of two steps or more, and two approver fixtures for
      `preventInitiatorApproval`.
- [ ] `run-slice0..3.mjs` and `run-round4-d.mjs` re-run as the regression net; the assertions the new
      guards change (approve without `RequiresApproval`, the status edit, the fiscal block) updated.
- [ ] Scenario 110: event files uploaded as files (F-54), no HR fiscal year, `hr.head` books the
      second room (R4-2.3); the D suites get a tidy step (R4-2.1).
- [ ] The guide: § 21 "Final review" closing every finding this plan closes; the eight rules
      rewritten; **every changed chapter rewritten**, not only § 21 (events, bookings, rooms, closures,
      milestones, the diaries, the company profile; the fiscal chapters replaced by the fiscal calendar
      card; a calendar chapter); Appendix B's permission map gains the new doors; Appendix A's route
      list gains the calendar and loses the fiscal screens.
- [ ] `docs/HR/README.md`; `docs/HR/programme/HR-FINISH-PLAN.md` (D-02 closed by D-8; F-27 recorded as
      HR-wide); `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` (attendance A-92; payroll, unpaid closures);
      the configuration register (new endpoints and topics); the demo runbook's closure aside (in
      `dev-harness/`, outside the repo); memory `hr-company-schedule-guide-findings`.

**State:** *(filled when it lands)*

---

## 5. Residual register

Every finding from the guide's § 21, with its owner here. Nothing is dropped silently.

| Finding | Owner | Note |
|---|---|---|
| C-1 | Lane 5 | the remaining removes |
| C-2 | Lane 5 (show) + Lane 2 (Edit through reschedule) | |
| C-3 | **D-10, pending** | reopened by the review |
| C-4, C-6, C-17, C-27, C-34 | closed in round 4 | — |
| C-5 | Lane 1 | leave and the statutory clocks; attendance logged |
| C-7, C-14, C-18, C-19, C-20, C-23, C-24, C-29 | Lane 2 | C-14's series model settled by D-12 |
| C-8, C-30, C-31, C-33, C-36 | Lane 3 | C-36 per D-18 |
| C-28 | Lanes 2, 3 | the availability read and every other list read (F-30) |
| C-9 | Lane 7 | |
| C-10, C-11, C-12, C-13, C-25 | Lane 2 (D-9) | search, paging, date filter, CSV export, one dashboard read |
| C-15 | Lane 2 (D-9) | the clash rule and the warnings |
| C-16 | Lane 5 | reopened by the review; recommendation to confirm |
| C-21, C-22 | Lane 2 (D-9) | attendance row removable on Write; participant editable and removable on Write; the reply door (D-8) covers the invitee's side |
| C-26, C-35 | Lane 7 | the calendar's room filter is the room view |
| C-32 | Lane 3 (D-9) | |
| C-37, C-38, C-39 | Lane 1 | C-38 recurs annually (D-9) |
| C-40 | Lane 4 | |
| C-41 | **Closed by decision D-17** (2026-10-01) | not built: a company milestone is not one employee's award; the certificate is a milestone document |
| C-42…C-48 | Lane 4 | closed by retirement (D-6) |
| C-49, C-50 | Lane 4 (D-9) | buttons hidden; the logo a versioned asset; the free-text URL retired (F-55) |
| C-51 | Lane 2 (D-9) | the drill creates its event |
| R4-2.1, R4-2.2, R4-2.3, R4-2.4 | Lane 6 (R4-2.1: the D suites get a tidy step; R4-2.3: scenario 110's booking by `hr.head`) | R4-2.4 is the demo database, not the code |
| R4-3.1 | Lane 5 | **live** — the first draft had it as kept |
| R4-5.1, R4-6.1, R4-6.2, R4-6.4…R4-6.7, R4-7.1 | Lanes 2 and 5 | |
| R4-6.3 | Lane 2 (deliveries counted) + Lane 5 (the card) | |
| R4-9.1 | Lane 3 | |
| R4-10A.1, R4-10B.1…R4-10B.4, R4-10A.3 | Lane 5 | |
| R4-10A.2 | **Lane 2** (backend) + Lane 5 (screens) | the first draft had it in lane 5 only |
| R4-10A.4 | Lane 1 | closures and holidays both |
| R4-10A.5 | Lane 6 | the browser walk |
| R4-12.1 | Lane 3 | a typed code is checked against deleted rooms too |
| R4-13.1 | Lane 1 | |
| D-02 (finish plan) | Lane 7 | closed by D-8 |

---

## 6. Migration

One batch, lane 0. D-12 and D-17 settled its content — no series table, no milestone-link columns —
and L0-1 and L0-2 settled that it has no data steps (§ 1c). The user scaffolds (`ef.ps1 migrations add CompanyScheduleFinalReview`); the body is
rewritten as guarded SQL; the Designer and snapshot are kept; the user builds; applied to UAT with the
built DLL's `apply-migrations` after the user's go. Proof: `__EFMigrationsHistory` gains the row and
every new column, index and table exists on UAT. *UAT holds 142 history rows at 1163bbc47, the repo's
chain exactly, so this migration is the only one pending there (§ 3c).*

**State (2026-10-04): ✅ applied to UAT.** *Written, proved, built — the user's build green and
`has-pending-model-changes` clean — then applied, details at the end of this section.*
`20261004224953_CompanyScheduleFinalReview`: 33 guarded batches up, 26 down; the Designer and the
regenerated snapshot kept.
- **Left out of the scaffold:** two `ExternalPremiumChargeAmount` columns. They belong to master's
  hand-written `20261004103000_AddEstateListingPremiumChargeAmount`, which never updated the snapshot.
  The snapshot now records them; the snapshot's other change is `LoginPageStyle` moved into
  alphabetical order.
- **Kept, guarded:** EF's drop of `IX_EventParticipants_TenantId` and `IX_EventAttendances_TenantId`,
  done after the unique indexes exist.
- **F-45 duplicates are refused, not repaired:** errors 51520 (guests) and 51521 (attendance), each
  naming the event number and the staff number.
- **No data steps (L0-1, L0-2).**
- **Down refuses (51522)** while any row uses what Down would drop, and counts it.

*Proof, on `ErpSystemDB_CsL0Copy`, a COPY_ONLY restore of UAT, dropped afterwards with its backup.*
The SQL was rendered by a Python port of the helpers, mechanically compared with the C#: every
template, every argument and the refusal's seven counts are identical.
- Up applied, and applied again: no change. All objects present; six foreign keys trusted; both unique
  indexes filtered; row counts unchanged.
- Down returned the copy to its starting state, both `TenantId` indexes restored; a second Down
  changed nothing. Up applied again.
- With a recurring closure and a unit-only event, Down refused with 51522 counting both, and dropped
  nothing.
- A duplicate guest stopped Up with 51520 ("event EVT-2026-00001 / employee TDC/00001 (2 rows)").
  Once that was soft-deleted, a duplicate attendance row stopped it with 51521. Once that was
  soft-deleted too, Up completed, the indexes coexisting with the soft-deleted rows. A fresh live
  duplicate was then rejected (2601).
- The proof above was run on the first body, which also carried a department-to-unit name-match step.
  L0-1 then removed that step: two Up batches went, and Down's two unit counts lost their
  `AND [DepartmentId] IS NULL`, which existed only to spare mapped rows. The port was updated and
  re-compared with the C# (identical); the batch counts agree (33 + 26 = 59 calls). A re-run on a fresh
  copy was not done: the session's path protection blocks deleting the backup file it would leave
  under `C:\Program Files`. Applying the migration to UAT, after a backup, is the next proof of Up.

*Applied to UAT, 2026-10-04, with the user's go* (the merge #13 recipe: `apply-migrations` from the
built API, so no web host and no seeders).
- **Backup:** `MSSQL\Backup\ErpSystemDB_UAT_preCsL0_20261004.bak` (COPY_ONLY, CHECKSUM, verified, 634 MB),
  KEPT as UAT's restore point.
- **The compiled migration on a copy first:** restored as `ErpSystemDB_CsL0UatCopy`. Exit 0 in 27 s;
  history 142 → 143; every column, index and table present; six foreign keys trusted; row counts
  unchanged. The copy was dropped. This closes the re-run gap above: it ran the built migration
  itself, not the port.
- **UAT:** backup re-verified; exit 0 in 24 s; `__EFMigrationsHistory` **142 → 143**, with
  `20261004224953_CompanyScheduleFinalReview` present. The same probe as the copy: 5/5 event columns,
  2/2 closure columns, 5/5 attachment columns, the documents table, 10/10 new indexes, the two
  `TenantId` indexes gone, 6/6 foreign keys trusted, both unique indexes filtered. Rows unchanged:
  1 closure, 17 guests, 5 attendance, 4 attachments.
- **Log:** no errors; only the EF model warnings every start prints, none about this module.

---

## 7. Verification

- API in Staging on UAT with the JWT key and the four `RateLimiting__*` overrides
  (`dev-harness/hr-performance/tools/start-api-uat.ps1`); the clamd stub on 127.0.0.1:3310 for the
  upload assertions.
- `node run-final-review.mjs` twice from different states; then `run-slice0..3.mjs` and
  `run-round4-d.mjs`. After each run, count what it wrote (notifications by recipient, minted logins,
  minted leave types) before calling it clean.
- Browser walk: a cold load of the room edit page (F-27; the guard is in HEAD, so there is no "before"
  to see); create a room and edit it (site shown); an optional picker on the event edit page survives a cold load and a Save;
  create each closure type (form shape, server refusals); upload two documents to a milestone and
  download one; the calendar as `hr.head` and as a plain staff login, including the staff event view
  and a reply; if D-14, an invitation's calendar file opens in Outlook.

---

## 8. Status at HEAD 9f4b3206c (before any code of this plan)

- **FIXED in round 4 (5):** C-4, C-6, C-17, C-27, C-34.
- **PARTLY FIXED (3):** C-1 (two of ten removes hidden), C-2 (kept and emailed, not shown; Edit
  bypasses), C-5 (the clash check and the diaries read closures; leave and attendance do not).
- **LIVE (43):** every other C-finding, and all 26 R4-findings.
- **New from the first read (26):** F-1…F-26 (§ 3a). The review found F-4 and F-5 overstated and
  F-19's cause wrong; all three stay, annotated.
- **New from the review (30):** F-27…F-56 (§ 3b).
- **At HEAD 1163bbc47 (re-check, 2026-10-04; § 3c):** all of the above stand, except **F-27, FIXED in
  HEAD** by the travel final closure (C6). **New (1):** F-57.

---

## 9. Log

- **2026-10-01** — Module read end to end; three user-found defects confirmed in source; seven
  decisions taken (§ 1a) and D-8 added from the finish plan's D-02; this plan written and staged.
  The user read it and ruled **D-9: nothing deferred** — every parked finding was moved into a lane,
  and lane 0 grew four columns and an enum value for them.
- **2026-10-01, the same day** — **An independent review of the plan** (the user asked for one before
  any build). It re-checked every finding in source and changed the plan as follows; all of it is
  folded into the lanes above.
  - **Corrected:** the room-edit cause and fix (F-19 → F-27, HR-wide); the leave wiring (F-28: the
    holiday sets are tenant-wide, so company-wide closures join the working-day calculator and scoped
    ones become a per-employee overlay); the audience and scope helpers (the existing
    `IHrAudienceResolver` instead); the Approve guard ("Scheduled only" stranded moved events); the
    booking Approve guard (requiring the room to need approval stranded bookings); Edit allowed to
    confirm an unapproved event; R4-10A.2 moved to the backend lane; R4-3.1 is live, not kept; C-3,
    C-16 and R4-3.1 reopened, having been marked kept without the user's ruling; the fiscal read
    goes through HR's backend and the manpower budget form follows it; the free-text logo URL is
    retired rather than kept as a fallback; the migration template pointer; F-4 and F-5 annotated.
  - **Added:** F-27…F-56, and decisions D-10…D-18, pending the user.
  - **Next:** the user settles § 1b (D-12 and D-17 before lane 0); then lane 0's UAT counts.
- **2026-10-01, later** — The user settled **D-12** (a light series: the occurrences are the series, with
  a scope choice on guest, edit, reschedule and cancel actions, one invitation per guest, no series
  table) and **D-17** (the milestone link dropped; C-41 closed as decided), both as recommended. Lane 0
  needs no series table and loses the two milestone-link columns. Seven decisions remain pending
  (D-10, D-11, D-13…D-16, D-18); none blocks lane 0, which starts with the read-only UAT counts.
- **2026-10-04** — **Re-checked against HEAD 1163bbc47** (the travel final closure and master merge
  #13 had landed since `fe14a9ca0`); § 3c. The module's backend and screens are unchanged, so every
  finding and line reference into them stands.
  - F-27 is fixed in HEAD (travel C6), so lane 5's item becomes a browser re-test.
  - F-28, F-29 and F-56 carry the 1163bbc47 references.
  - Finance's per-book year close becomes a lane 4 source-check question.
  - Lane 2 names the real drill service (`SheEmergencyService`) and points at travel's
    `StaffTravelNotices` as the closer notice model.
  - New: F-57, the sidebar gate test (lane 5, with lane 7's entry).
  - No decision changed. Lane 0 starts.
- **2026-10-04, later** — **Lane 0 written.** The UAT counts were all zero. The user scaffolded
  `CompanyScheduleFinalReview`; the body was rewritten as guarded SQL and proved on a restored copy of
  UAT (§ 6). The user settled **L0-1 and L0-2: no data steps.** The unit replaces the department
  outright, and no row on any local database was saved against a department or contradicts D-1.
  The legacy-row handling leaves lanes 1 and 2, and `DepartmentId` is to be dropped by a later
  migration.
- **2026-10-04, later** — **Lane 0 done.** The user's build was green, with no pending model changes.
  With the user's go, the migration was applied first to a restored copy of UAT and then to UAT:
  history 142 → 143, every object present, no row changed; the backup is kept (§ 6). Next: lane 1,
  which needs D-15.
