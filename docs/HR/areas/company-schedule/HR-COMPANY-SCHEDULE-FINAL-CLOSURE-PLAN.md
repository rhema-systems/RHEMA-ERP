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
   contains. D-15 was settled at lane 1's source check (§ 1c). D-10, D-11, D-14 and D-16 were settled
   at lane 2's source check (2026-10-05, § 1c, all as refined there). **Two remain pending the user** —
   D-13 and D-18, both lane 3's.
2. ✅ **Lane 0 is done (2026-10-04): the migration is applied to UAT.** ✅ **Lane 1 is done (2026-10-05)**,
   slices 1a–1e (lane 1 State); its screen awaits lane 5's browser walk. ✅ **Lane 2 (events) is done (2026-10-05):**
   source-checked and its four decisions settled (§ 1c), slice 2a built and proved
   (2026-10-05), 2b, approval on the workflow engine (D-10), and 2c, who an event is for (D-16), the
   diaries and the intranet, 2d, guests, the register and tasks, 2e-1, who is told, 2e-2, delivered vs
   issued, 2e-3, calendar files and the overdue chase (its migration applied to UAT, 144 history rows), and
   2f, recurrence as a light series, in three slices (2f-1, 2f-2a, 2f-2b), and 2g, the registers' search, paging,
   export and dashboard (2g-1) and the event-against-event clash rule (2g-2), and 2h, files through the upload gate
   (C-18, F-54) and the drill's event (C-51) — none with a migration; its screens await lane 5's browser walk.
   **Lane 3 (rooms and bookings) is source-checked and D-13 and D-18 settled (lane 3 State), in four slices; 3a, the
   rules and guards, 3b-1, approval on the engine and the booker told, 3b-2, the hourly lapse and completion and
   no-show, 3c, staff booking from `/me` (D-13), 3d-1, a room for every date of a series, approved once and the booker
   told once, and 3d-2, an extended series bringing its rooms and the event page's Rooms card (D-12), built and proved
   (no migration; UAT has the real Room Booking Approval). ✅ Lane 3 is done (2026-10-06); its screens await lane 5's
   walk. **Lane 4** is source-checked and its five questions settled (lane 4 State), in three slices; 4a, milestone files
   and a yearly milestone's dates, 4b, HR reads Finance's fiscal calendar (D-6; a year Finance has not opened continues
   its sequence), and 4c, the company profile (the logo a versioned image, the Logo URL retired, PNG/JPEG of at most
   2 MB, the image buttons gated), built and proved. ✅ Lane 4 is done (2026-10-06). **Lane 5** (the screens) is
   source-checked and its four questions settled (lane 5 State), in two slices; 5a, the screens' removes, dates, landing,
   site pickers and sidebar test (no server change), and 5b, the diaries and the team schedule (unit heads read theirs),
   built and proved. ✅ Lane 5's code is done (2026-10-06); its browser walk (lane 5 State, items 1–8) is the user's.
   **Lane 7** (the company calendar) is source-checked and its four questions settled (lane 7 State), in two slices; 7a,
   the server (the calendar read, the staff event view, the invitee's own answer), and 7b, the screens (month bands and
   week, the room view, the staff event page, the menus), built and proved. ✅ Lane 7's code is done (2026-10-06); its
   browser walk (lane 7 State, items 9–12) is the user's. Next: **lane 6** — the harness, the guide, the registers and
   memory (lane 6's list), which closes the plan.** *As planned:* Lane 0 (§ 6): first count on UAT, read-only, the rows the migration must decide about (legacy
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

### 1b. Decisions raised by the review (2026-10-01) — seven settled, two pending the user

**D-12 and D-17 were settled by the user on 2026-10-01, as recommended; D-15 on 2026-10-04 at lane 1's
source check; D-10, D-11, D-14 and D-16 on 2026-10-05 at lane 2's source check, as refined in § 1c's
lane 2 table, which supersedes their rows here (✅ below).** D-13 and D-18 are pending; each is needed
before the lanes in its "Blocks" column.

| # | Question | Recommendation | Blocks |
|---|---|---|---|
| **D-10** ✅ | Approval of events and bookings: the workflow engine, or the one-click flag? | **The engine**, like every other HR approval: the four-step recipe, definitions of two steps or more, the auto-approve guard where no definition is published, and `preventInitiatorApproval` to stop self-approval. It gives the approver an inbox entry and a notification (C-3, the approver half of F-34). No schema: the engine links by entity type and id. If the flag is kept instead: refuse self-approval — the approver may not be the organiser or the booker. | Lanes 2, 3; harness definitions |
| **D-11** ✅ | Who is the organiser? | **An Organiser picker** defaulting to the signed-in employee; the creator stays in `CreatedBy`. Diaries, the clash check and the calendar use the organiser (F-42). | Lane 2 |
| **D-12** ✅ | Recurring events: a series, or independent occurrences? | **Settled 2026-10-01, as recommended: a light series, in which the occurrences are the series.** Each occurrence stays a full event, with its own number, guest list, RSVPs, attendance register, tasks and papers; `RecurrenceSeriesId` and `OccurrenceNumber` tie them together, and there is **no series table**. Series behaviour is a scope choice, "this occurrence / this and following / the whole series", on adding or removing a guest, editing, rescheduling and cancelling. A guest added to the series gets one invitation listing the dates and answers each date or all at once. "Book this room for every occurrence" makes one booking per date and lists the dates where the room is taken. A series action never changes an occurrence that is past or completed. A series needs an end date or a count, up to 52, and can be extended later. A monthly rule on a day a month lacks falls on that month's last day, counted from the first date; an occurrence on a holiday or company-wide closure is generated and flagged, not skipped. *Why: answers and attendance are per meeting, since people miss one week and not the next; independent occurrences would mean re-inviting everyone every week; and an ordinary event needs no special case in the clash check, the reminder sweep, the diaries or the calendar.* | Lane 2. Lane 0: no series table |
| **D-13** ✅ *(2026-10-05, as recommended; lane 3 State)* | May staff book rooms themselves? | **Yes, from the portal**: their own bookings only, the same room rules, approval-required rooms routed to the approver, staff cancel their own. HR keeps the desk. | Lanes 3, 7 |
| **D-14** ✅ | Calendar invites in the emails? | **Yes**: an `.ics` on invitation, reschedule and cancellation — a stable UID per event, SEQUENCE raised on each change, METHOD REQUEST and CANCEL. The email DTO already carries attachments; the templated send needs an overload. External guests answer from their mail client to the organiser, and HR records it at the desk, which answers F-36. | Lane 2 |
| **D-15** ✅ | Unpaid closures, and closures added after leave was approved? | **Settled 2026-10-04 at lane 1's source check, refined there into D-15a, D-15b and D-15c (§ 1c); the payroll half goes into the payroll hand-off rather than the register.** *As first recommended:* HR records the pay flag and **exposes closures to payroll read-only**, logged as a cross-module item, because payroll is another developer's module (F-51). **A closure created, moved or deleted re-charges the approved leave it overlaps**, and the employee is told (F-52). | Lane 1 |
| **D-16** ✅ | Who is "Management only"? | **The heads of organisation units** (`OrganizationUnit.HeadEmployeeId`), plus the organiser and participants; "Management" visibility uses the same population (F-43). | Lanes 2, 7 |
| **D-17** ✅ | The milestone link to one employee's award or certification (C-41)? | **Settled 2026-10-01, as recommended: dropped.** Company milestones are company facts: the guide's own walkthrough files the ISO 9001 quality certification as one, while a training-module certificate and a long-service award each belong to one employee. A milestone's evidence is its documents (D-3); awards and milestones meet, if anywhere, through a company event for the awards ceremony. C-41 closes as decided, not built, which is a decision rather than a deferral. | Lane 0: no link columns. Lane 4: nothing to build |
| **D-18** ✅ *(2026-10-05, as recommended; lane 3 State)* | Retiring a room that has future bookings? | **Offer "cancel these N bookings and tell their bookers"**, and refuse deletion once a room has any booking history — deactivate instead (F-49). | Lane 3 |

### 1c. Decisions from lane source checks

*Appended per lane as each is source-checked and settled with the user, before it is built.*

**Lane 0 (2026-10-04) — two data-step questions, settled by the user the same day: no data steps.**

| # | Question | Settled |
|---|---|---|
| **L0-1** | F-53: should the migration move rows saved against a department onto the unit with the same name? | **No — the unit replaces the department outright (D-5), and there is nothing to move.** No event or closure carries a `DepartmentId` on UAT, the dev database or the test-data database, live or deleted. The "legacy rows shown read-only and listed for HR" handling is dropped from lanes 1 and 2. `DepartmentId` stays in the schema only while today's code reads it; a later migration drops it once lanes 1 and 2 have moved every read to the unit. *The first recommendation kept a name-match step as protection for other databases; withdrawn, as it could never act.* |
| **L0-2** | D-1: should the migration rewrite old closures whose saved scope contradicts their type? | **No.** No closure contradicts D-1: UAT's one is a whole-company Full closure, and the other two databases have none. Lane 1 reads a closure's scope from its type and refuses a contradictory save from now on. |

**Lane 1 source check (2026-10-04, at b72535647) — what the code says.**
- **The closure service** (`CompanyScheduleService.cs:1628-1815`) has no validation on create or
  update. Its update answers with the unsaved entity rather than a re-read (the F-46 shape).
  `IsClosureDateAsync` (:1757) ANDs site and department as F-2 says, and so does the repository's
  copy (`CompanyScheduleRepository.cs:636`).
- **Re-charging approved leave needs no new arithmetic.** Reschedule and recall already do it:
  - the days: `CalculateLeaveDaysAsync`, the one definition of chargeable days;
  - the balance: `LeaveBalanceRecalculationService.RecalculateAsync`. `UsedDays` is derived from the
    approved requests, not stored, so re-deriving cannot give a day back twice (`LeaveService.cs:1389`);
  - attendance: `ReconcileAttendanceAsync` re-posts the request and drops days that are no longer
    chargeable.

  **Nothing re-charges approved leave today**, for closures (F-52) or for public holidays:
  `PublicHolidayService` create, update and delete touch no leave.
- **A finished leave year.** The year-end carry-over computes from balances ("set, not stacked").
  Re-charging leave in a year whose carry-over has run would strand the day returned in that year
  until the run was repeated.
- **`IHrWorkingDayCalculator`** (`HrWorkingDayCalculator.cs`) reads the default calendar's active
  Mandatory and SubstituteDay holidays. Nine services use it: leave, `LeaveChargeableDays`, the
  leave usage reader, leave reminders, the discipline deadlines, discipline reminders, two discipline
  services, and travel's attendance posting. Company-wide closures added there reach all nine.
- **The audience resolver's usual methods take the tenant from the signed-in user**
  (`HrAudienceResolver.cs:24`), and the nightly leave paths have nobody signed in. *Corrected while
  building 1a:* the resolver already has tenant-explicit methods, `ResolveForTenantAsync` and
  `UnitAncestryAsync`, and leave already uses the second. Lane 1 uses those, plus a batched
  `UnitAncestriesAsync` added in 1a, so the unit tree is read once per question.
- **Announcements:** `HrAnnouncementService` creates a draft and then publishes it, with no approval
  step. Publishing refuses an audience that reaches nobody (`:237-255`), so a closure whose scope
  holds no staff must not fail the closure's own save.
- **Payroll reads none of HR's days off** (read-only look; the module is not ours). It keeps its own
  `PayrollHolidays` (flat dates) and `PayrollNonWorkingDays` (weekday codes with overtime rates), and
  takes working and absent days as inputs on each pay line. The payroll hand-off
  (`integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md`) already records holidays as "two
  lists, no bridge" (§ 2.1), and HR's holiday-pay fields as "stored by HR, read by nothing" (§ 3,
  item 2). `BusinessClosure.IsPaidClosure` is the same shape. It is not yet in the cross-module
  register.

**Lane 1 decisions — ✅ settled by the user on 2026-10-04, all four as recommended.**

| # | Question | Recommendation |
|---|---|---|
| **D-15a** | A closure added, moved or deleted over leave already approved: re-charge the leave? | **Yes, with one routine.** For each approved, in-progress or completed request over the changed days, for the employees the closure covers: recompute its days, re-derive the balance, re-post attendance, and tell the employee the old and new count, in the app and by email. It reaches leave in the current and later leave years, **including leave already taken** — an emergency closure is often recorded after the day. A closure in a finished leave year re-charges nothing automatically: HR is shown the requests it overlaps, to adjust by hand. A Partial closure never re-charges, because it is still a working day. |
| **D-15b** | The same gap for public holidays: a holiday added after leave is approved does not give the day back either. Close it too? | **Yes, with the same routine,** called from `PublicHolidayService`'s create, update and delete (HR's own code). Otherwise a holiday and a closure on the same day would treat the same leave differently. |
| **D-15c** | Unpaid closures and payroll? | **HR keeps the pay flag it already has and makes closures readable through the same HR reader leave uses.** No payroll code is touched. Closures are added to the payroll hand-off beside holidays (§ 2.1 and the § 3 table) rather than as a new register entry, so the payroll owner sees all of HR's days off together. |
| **L1-1** | Announcing a closure: publish automatically on save, or prepare it for HR to send? | **Prepare it; HR sends it with one click.** Saving a non-working closure offers "Announce to the N staff it covers": an announcement already addressed by the closure's scope and worded from it, published when HR confirms. An announcement reaches every covered employee at once and cannot be unsent, and a closure is often typed, corrected, then confirmed; publishing on save would turn each correction into another broadcast. The staff whose leave changes are told individually anyway (D-15a). *The plan's text said "announced"; this is the source check's refinement.* |

**Lane 2 source check (2026-10-05, at b95423646) — what the code says.** *Three read-only passes:*
*the event core; notifications; approval, audience and the rest. Their key claims were then checked by*
*hand.* None of lane 2's findings is fixed, and the plan's line references into the event code still
hold. The event service is `CompanyEventService` (`CompanyScheduleService.cs:18-959`).

- **No validation and no state guards.**
  - Create, update and reschedule (`:329-475`) check no window, no RSVP deadline and no reminder
    settings. `[Required]` on a non-nullable date never fails, so an omitted date is saved as
    0001-01-01.
  - No lifecycle method has a guard:
    - Approve works on an event that needs no approval, and on the organiser's own;
    - a second Cancel overwrites the reason and emails everyone again;
    - Complete runs on a cancelled or future event;
    - Update writes any `Status` (Confirmed on an unapproved event; an "un-cancel" that leaves
      `IsCancelled` set), and writes the dates directly (F-37, R4-7.1).
  - **Nothing ever sets `Rescheduled` or `InProgress`** (C-7).
  - Reschedule resets no answers *(F-38's "answers reset" is the target, not today)*. It keeps the
    chase stamp on purpose (`:445-447`).
  - `EnsureExistsAsync` exists nowhere; it is new work. The model is the closure checks
    (`:2036-2053`).
- **F-10, corrected:** another tenant's id is **stored silently**, not a 500. The DbContext's tenant
  filter is inert, because nothing constructs it with a tenant (`ApplicationDbContext.cs:50-52, 10555`).
  Only a non-existent id gives a 500. A participant from another tenant is then emailed.
- **Bookings link to events by `RoomBooking.EventId`.** It is set from the body, never validated,
  and never acted on, so cancel, delete and reschedule leave linked bookings alone (F-38, F-39). On
  UAT none of the 19 bookings is linked.
- **The organiser (D-11).**
  - `OrganizerId` is set from the token on create and never changes.
  - ⚠ **`CreatedBy` / `CreatedById` are never stamped**: null on all 42 live events on UAT. So "the
    creator stays in `CreatedBy`" is only true once the service stamps it.
  - The diaries and the clash check read participants only (the organiser is absent unless invited).
  - The reminder sweep never reminds the organiser.
  - `HasConflictingEventAsync` (`CompanyScheduleRepository.cs:129`) is the only organiser-based check,
    and nothing calls it.
- **Approval (D-10).**
  - Fields: `RequiresApproval`, `ApprovedById` (an Employee FK) and `ApprovalDate`. There is no
    `IsApproved` and no Draft or awaiting-approval status: an event is created `Scheduled`, a booking
    `Tentative` or `Confirmed`.
  - No workflow code, entity type or definition exists for either; UAT has none either.
  - On UAT **no event requires approval** (0 of 42).
  - The pieces the engine route would use all exist:
    - the fallback guard `HrWorkflowFallbackAuthority`;
    - an end-to-end example in travel: entity type, submit, approve, adapter, display resolver,
      seed, frontend type;
    - the permission `HR.Company.Approve`, granted to the HR desk. Its description still says "team
      objectives and terms of reference".
  - Three traps:
    - with no submit step, the instance must start at create (as attendance does);
    - the adapter's default recall sets a "Draft" status that does not exist, so it must be
      written out;
    - the two-stage seeding helper cannot set `preventInitiatorApproval`, which in any case guards
      the creator, not the organiser. A service check against `OrganizerId` / `BookedById` is
      needed whatever is seeded (recipe trap 7).
  - Like every HR approval, approving from the generic `/workflow/inbox` does not reach the record
    (cross-module #15). The inbox links to the event page, where Approve works.
- **The audience (D-16).**
  - `Scope`, `Visibility`, `ShowOnCompanyCalendar`, `ShowOnIntranet` and `Priority` are stored and
    shown, and **read by nothing**.
  - `IHrAudienceResolver` has no rule for unit heads or managers.
  - Orientation already defines "Management" as **unit heads plus anyone named as a line manager**
    (`OrientationEnrollmentTriggerService.cs:1280-1287`).
  - ⚠ **Data:**
    - UAT: 39 of TDC's 42 seeded units have a head. The demo seeder appoints the employee with the
      lowest staff number in each unit.
    - TDC's own data, as measured 2026-08 (`IEmployeeRelationsResponderService.cs:12`): 2 of 48
      units had a head, and 5.8% of staff a line manager.
    - UAT has 488 line managers, and 14 people at the "Management Staff" level.
- **Notifications.**
  - Five templated emails: invitation, RSVP chase, reminder, rescheduled, cancelled
    (`CompanyScheduleEmailCatalog.cs`). Nothing in-app for events or bookings *(F-31 now stands for
    those only: closures announce in-app since 1d)*.
  - Nothing is sent on approve, on update (even when the dates change), complete, delete, a reply
    or a removal.
  - **F-33 is wider than written:** the per-event buttons refuse only a cancelled event, so they also
    run on completed, postponed and past events, and after the RSVP deadline. The hourly sweep does
    refuse an unapproved event.
  - F-35: no send skips an inactive employee. `StaffTravelNotices` does not either, so copying it
    does not deliver F-35.
  - **R4-6.3, sharper:**
    - every attempt counts as a delivery;
    - the invitation is marked Sent before it is sent;
    - the reminder and chase stamps are written on a run that delivered nothing, so the sweep never
      retries. Orientation's dispatcher already tells "no mail server" from "no address".
  - **The in-app model:** travel's `StaffTravelNotices` shape (one class owning the topics,
    never failing the act), publishing one event with a list of recipients as announcements do.
    Topics are in-app only; **email stays on the templated catalogue**, because the topic path
    queues rows (no delivery result), loses the catalogue's wording and drops attachments.
- **Calendar invites (D-14).**
  - `SendAsync` already takes attachments; only `SendForTenantAsync` lacks them, and its core
    supports them, so the overload is a few lines.
  - **Interviews already build an `.ics`** (`JobInterviewService.BuildInterviewIcs`, `:3085-3128`):
    a random UID on each send, `SEQUENCE:0`, no ORGANIZER, floating time, no CANCEL.
  - **SEQUENCE has nowhere to live:** there is no column for it.
  - Mail goes from the system address with no Reply-To, so an external guest's reply reaches the
    organiser only through the invite's ORGANIZER line.
  - External guests receive all five emails and have no way to answer. Anonymous token-link replies
    exist elsewhere (interview attendance, offers, client timesheets).
- **Smaller corrections:**
  - **F-58 (new):** `ToDetailDto` drops the original window (`CompanyScheduleMappingExtensions.cs:111`;
    only `ToDto`, `:78`, copies it). The event page reads the detail, so it never shows the original
    dates: a cause behind R4-6.1 and F-21.
  - **Recurrence (C-14):** the five old fields are create-only. `RecurrenceSeriesId` and
    `OccurrenceNumber` (lane 0) are read and written by nothing. UAT's two "recurring" events are
    harness rows.
  - **`CompanyEventCommitmentSource.WindowOf`** builds one window from the start day
    (R4-10A.2, confirmed).
  - The repository reads use containment, not overlap. None filters by tenant inside the query
    (F-30). Event, task, room and booking updates return the unsaved entity (F-46).
  - `CompanyEvent.OrganizationUnitId` (lane 0) is read and written by nothing; `DepartmentId` is
    used everywhere, the form included.
  - **Attachments:**
    - all 12 on UAT are path-only (F-54);
    - the event's panel is a local `ResourceCollectionTab`;
    - the shared `AttachmentsPanel` has no field for the attachment type (Agenda, Minutes…), so
      the event panel needs its own upload dialog, as union documents built.
  - **Tasks:** Completed through update sets no date. Complete has no guard. `Overdue` is set by
    nothing; the repository's computed query has no caller and uses server-local `DateTime.Today`.
  - **Participants:**
    - re-marking attendance overwrites the check-in (F-1);
    - check-out has no rule;
    - an external guest needs neither name nor email on the server, and duplicates are not
      refused;
    - a reply accepts any `InvitationStatus` and ignores the event in its route (F-11);
    - removal needs Admin and tells nobody;
    - C-21 and C-22 have no endpoints.
  - **The drill (C-51):**
    - drill numbers are typed by hand (there is no "DRL-" generator);
    - `EventName` is 100 characters, but a plan's name can be 200;
    - SHE already reminds about the next drill ("DrillDue"), so the company event must be created
      with reminders off;
    - the event is created server-side, so SHE users need no HR permission.
  - **UAT residue:** 402 active "E2E Closure …" units from the performance harness (2026-09-29 to
    10-01) appear in every unit picker on UAT. They are not this module's to delete.

**Lane 2 decisions — ✅ settled by the user on 2026-10-05, all four as recommended below** ("let's go with your recommendations for the four decisions"):

| # | Question | Recommendation |
|---|---|---|
| **D-10** | Approval of events (and lane 3's bookings): the workflow engine, or the one-click flag? | **The engine**, as recommended in § 1b. **What it means in code:** the instance starts at create for an event that needs approval ("awaiting approval" = needs approval and not yet approved, no new status); a one-stage definition per entity, addressed to the HR desk role, with `preventInitiatorApproval`; the service refuses the organiser (or booker) as approver, whoever created it; `HR.Company.Approve` is the fallback tier when no definition is published, its description corrected; recall written out in the adapter; an approved event that is rescheduled starts a fresh instance. Approving from `/workflow/inbox` does not reach the record (#15), as for every HR approval. **If the flag is kept instead:** the same guards (needs approval, not yet approved, not the organiser, `HR.Company.Approve`) and an in-app notice to the approvers. It is much less code, but it is the one approval in HR the inbox never shows. |
| **D-11** | Who is the organiser? | **An Organiser picker** defaulting to the signed-in employee, as recommended — and **the service now stamps `CreatedById` / `CreatedBy` on create**, since it never has. The organiser is always in the event's diary and clash check, and is reminded. |
| **D-14** | Calendar invites in the emails? | **Yes, built on the interview builder:** one shared invite builder with a **stable UID per event** (the event's id), ORGANIZER = the organiser's email, times in UTC, METHOD REQUEST and CANCEL, and the interview invites moved onto it. **SEQUENCE needs a stored counter:** one small migration (an `int` on the event, raised on each reschedule or cancellation). It is the first schema change after lane 0. External guests answer from their mail client to the organiser, and HR records the answer at the desk (F-36). |
| **D-16** | Who is "Management only"? | **Orientation's existing definition: unit heads plus anyone named as a line manager**, as one new audience rule in `IHrAudienceResolver`, so "Management" means one thing across HR. *(§ 1b recommended heads only. On TDC's own data both lists were nearly empty when measured in August, so the form shows how many people the event reaches before it is saved, and a "Management only" event that reaches nobody is warned about.)* |

---

## 2. Lane status

| Lane | Scope | Waits on | Status | Proof |
|---|---|---|---|---|
| **0** | Schema: one migration (series, unit and gate columns, milestone documents, unique guards, upload category; no data steps — L0-1, L0-2) | — | ✅ 2026-10-04 | applied on UAT: `__EFMigrationsHistory` 142 → 143, every object present (§ 6) |
| **1** | Closures: type drives scope through the audience resolver; `is-closure-date` fixed; leave and the statutory clocks read closures; approved leave and holidays re-charged; announcements on HR's click | — | ✅ 2026-10-05, slices 1a–1e (156/156 ×2; round-4 net 201/201 ×2); browser walk in lane 5 | `run-final-review.mjs` blocks 1a–1e |
| **2** | Events: validation, lifecycle guards, recurrence series, audience, in-app and email notices, attachments on the gate | — (D-10, D-11, D-14, D-16 ✅) | ✅ 2a–2h built and proved (759/759 ×2; round-4 net 210/210); 2e-3's migration on UAT (144); screens await lane 5's walk | events block |
| **3** | Rooms and bookings: rules, availability, guards, the booking lock, lapses, retirement, staff booking | D-13 ✅, D-18 ✅ (D-10 ✅) | ✅ 2026-10-06: 3a, 3b-1, 3b-2, 3c, 3d-1, 3d-2 built and proved (1002/1002 ×2; round-4 net 212/212); screens await lane 5's walk | rooms block |
| **4** | Milestones: documents, recurring projection. Fiscal: Finance's calendar, HR's retired. Company profile | — | ✅ done 2026-10-06: 4a (1027/1027 ×2), 4b (1066/1066 ×2), 4c (1094/1094 ×2) built and proved | milestones + fiscal block |
| **5** | Screens: the shared select re-test (F-27, in HEAD), removes, event page, diaries, landing, site picker, the sidebar gate test (F-57) | — | ◐ code done 2026-10-06: 5a (1101/1101 ×2), 5b (1116/1116 ×2); the browser walk (items 1–8) is the user's | browser walk |
| **7** | The company calendar (HR, staff, portal), the staff event view and the self-service reply | D-13 (D-16 ✅) | ◐ code done 2026-10-06: 7a (1147/1147 ×2), 7b (1147/1147 ×2, layout tests 7/7); the browser walk (items 9–12) is the user's | calendar block, two logins |
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

### Lane 1 — Closures (D-1, D-4, D-5, D-15a–c, L1-1; C-5, C-37, C-38, C-39, R4-10A.4, R4-13.1, F-2, F-24, F-28, F-29, F-51, F-52)

- [x] One validator in `BusinessClosureService` for create and update: the D-1 matrix; `EndDate` on or
      after `StartDate`; site and unit must exist in the tenant; an overlapping closure of the same
      scope refused with a sentence naming the other (C-39), recurrence included; a closure over a
      public holiday warned. *✅ 1a — and a scope with nobody in it warned too.*
- [x] **Scope through the existing audience resolver** *(✅ 1a for `IsClosureDateAsync` and
      `ClosureCommitmentSource`; ✅ 1b for leave; ✅ 1d for the announcement's audience)* (*review: the first draft invented a new scope
      helper*). A closure's scope is an audience rule — `AllEmployees`, `Location` or
      `OrganizationUnit`, which already reaches the unit's subtree — evaluated with
      `IHrAudienceResolver.IncludesAsync` / `ResolveAsync`. ⚠ A `Location` rule matches one location
      exactly; see C-16 in lane 5. Read through `IHrClosureCalendar`, which is tenant-explicit (the
      nightly leave paths have nobody signed in) and uses the resolver's tenant-explicit methods. Used by:
      - `IsClosureDateAsync` — comparing dates, not date-times (a time on the closure's last day answered
        false); a query with no scope answers company-wide closures only (C-37);
      - `ClosureCommitmentSource` (R4-13.1);
      - leave, below.
- [x] **Leave, redesigned by the review (F-28).** *✅ 1b — see the State below for what was proved and what
      was not.*
      - Company-wide closures that are not working days join `IHrWorkingDayCalculator`'s holiday set, so
        leave, the discipline statutory clocks (F-29) and the diary agree with no change at those call
        sites. So does travel's on-duty posting (`StaffTravelAttendancePosting`, § 3c): a trip day
        on a company-wide closure is not posted as on duty, which is travel's own rule for a holiday.
      - Site and unit closures become a per-employee overlay: the employee is threaded through
        `GetChargeableDaysAsync` / `CalculateLeaveDaysAsync`, and `LeaveUsageReader` and
        `LeaveReminderService` keep the company set once plus an overlay per employee.
      - The leave calendar draws them as "Closure: title".
- [x] **(D-15a, F-52) One re-charge routine.** *✅ 1c — see the State.* *1b found a gap for it to close: a closure that existed
      before 1c never changes, so it never triggers the routine — 1c also gives HR a one-time "re-charge
      for existing closures" pass (UAT has none to re-charge: no leave touches its one closure, 29–30
      Dec 2026).* A closure created, moved or deleted re-charges the approved leave it overlaps. It covers each Approved, InProgress or Completed request over the
      changed days (old and new, for a move), for the employees the closure covers:
      - `CalculateLeaveDaysAsync` recounts the days, with that employee's overlay;
      - `RecalculateAsync` re-derives the balance, for each leave year the request touches;
      - `ReconcileAttendanceAsync` re-posts attendance;
      - the employee is told the old and new count, in the app and by email.

      It reaches leave in the current and later leave years, **taken leave included**. A closure in a
      finished leave year (carry-over already run) re-charges nothing automatically, and the save's
      answer lists the requests it overlaps for HR to adjust by hand. A Partial closure never
      re-charges, because it is still a working day. A request whose count does not change is left
      alone, with no notice.
- [x] **(D-15b)** `PublicHolidayService` create, update and delete call the same routine for the
      tenant's staff. A holiday added after leave is approved gives the day back too. *✅ 1c — through
      `HolidayCalendarService`, which is where the screen writes holidays; `PublicHolidayService`'s
      writes have no caller and were left as they are.*
- [x] **(D-15c, F-51)** HR keeps `IsPaidClosure`. Closures, with the flag, are readable through the HR
      reader leave uses; no payroll code is touched. Closures join holidays in the payroll hand-off
      (`integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` § 2.1 and the § 3 table), *not*
      a new entry in the cross-module register. *✅ 1d — `IHrClosureCalendar.GetClosureDaysAsync` and
      `GET closures/employee-days`; the hand-off's § 2.1 paragraph and § 3 item 4.*
- [x] **(L1-1, F-34) Announce on HR's click, not on save.** *✅ 1d for the preview and the announce;
      ✅ 1e for the button and the real publish, proved on a unit of the suite's own (1e's proof).* Saving a non-working closure offers
      "Announce to the N staff it covers". It prepares an `HrAnnouncement` addressed by the closure's
      audience rule and worded from it (title, dates, whether staff are paid), and publishes it when HR
      confirms. Publishing raises the in-app topic, and email where the topic has it on. A scope that
      reaches nobody shows "no staff to tell" instead of the button and never fails the closure's
      save.
- [x] **(R4-10A.4, the holiday half — in no lane before the review)** *Lane 1's part ✅ 1a; the
      calendar's part is handed to lane 7, which now names it.* The diary's and the calendar's
      holidays come from `IHrWorkingDayCalculator` — the default calendar, active, mandatory and
      substitute days — not from every calendar. *✅ 1a for the diaries and the clash check, through
      the calculator's new `GetHolidaysAsync`, which names each day; the calendar is lane 7.*
- [x] The attendance side logged in `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` (A-92). *✅ 1d,
      differently: A-92 is updated where it already lives, the attendance guide's finding ledger, and
      marked half closed. Leave, the statutory clocks and travel's posting now read closures;
      attendance's own working-day builder is still open. The cross-module register is for other
      teams' defects, and attendance is HR's own.*
- [x] (D-9, C-38) *✅ 1a rules, range and upcoming reads, `is-closure-date` and the diary source;
      ✅ 1b the leave reader; ✅ 1e the form's switch and the register's "Every year"; the calendar is
      handed to lane 7, which now names it.* `RecursAnnually`: the resolver path, `is-closure-date`, the diary source, the leave
      reader and the calendar all treat a recurring closure as covering the same month and day in
      every later year; the form has the switch ("Recurs every year — the year-end stocktake is typed
      once"); the register says "Every year". *✅ 1a for the rules, the range and upcoming reads,
      `is-closure-date` and the diary source; the leave reader is 1b, the form 1e, the calendar lane 7.*
- [x] *✅ 1e — see the State; not yet walked in a browser (lane 5's walk).* `closures/page.tsx`: the type decides what renders (Full → nothing; Site → the site select,
      required; Organisation unit → `OrganizationUnitPickerField`, required; Partial → exactly ONE of
      the whole company, a site or a unit, and the working-day switch shown on and locked, described).
      *1a: a partial closure takes one scope, not a site and a unit together, because the audience
      resolver unions its rules and "this unit at that site" could not be expressed.* The working-day
      switch's description becomes honest. `organizationUnitId` / `organizationUnitName` replace
      `departmentId` on the wire. No legacy department rows exist (L0-1), so nothing is shown for them;
      once lanes 1 and 2 read only the unit, a later migration drops `DepartmentId` from both tables.

**State (2026-10-05): ✅ lane 1 built and proved, slices 1a–1e.** It is not yet walked in a browser;
that is lane 5's walk. Slices: **1a** closure rules and
scope · **1b** leave counts closures · **1c** the re-charge routine for closures and holidays · **1d**
announce on HR's click, payroll reader and hand-off · **1e** the closures screen.

*1a — what was built:*
- `BusinessClosureRules`, pure: scope from type, the D-1 checks, which normalise the two derived
  flags and refuse real contradictions, yearly occurrences, overlap, and the candidate query.
- `IHrClosureCalendar`, tenant-explicit: closures in a range, coverage of given employees, the
  closure-date check.
- `IHrAudienceResolver.UnitAncestriesAsync`, which reads the unit tree once.
- `IHrWorkingDayCalculator.GetHolidaysAsync`, the same days as before, each with its holiday's name.
- `BusinessClosureService`: the validator and its warnings; the update re-read (F-46); the tenant
  filter inside every read (F-30); range reads by overlap (F-4); the site filter (company-wide closures
  and the site's own); `is-closure-date?organizationUnitId=` replacing `departmentId`.
- `ClosureCommitmentSource`: closures go to the people they cover (R4-13.1); holidays come from the
  calculator (R4-10A.4).
- The closure repository's six custom reads are removed.
- DTOs: `organizationUnitId`/`Name`, `scopeDescription`, `recursAnnually`, `warnings`.

*Refusals are 422 with the rule's sentence* — this controller's `CompanyScheduleBusinessRulesAttribute`,
not the 400 the first suite draft expected.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` block 1a: **79/79 on two clean passes.** It covers the D-1 refusals and the
  allowed cases, C-39 (a yearly repeat included), the closure-date check (C-37, F-2, C-38, a time on
  the last day), the diaries (A, B and C by site and unit), ranges, the site filter, both warnings
  and the re-read.
- Regression, once each: `run-slice0` 24/24, `run-slice1` 32/32, `run-slice2` 62/62, `run-slice3`
  44/44, `run-round4-d` 39/39.
- **The first pass found two harness errors, not code errors.** It expected 400, and its cleanup
  deleted the login, which answers 500 because the login has security-log rows. That left one login
  active. Fixed: refusals expect 422, and the login is switched off and the counts are of what was
  actually removed.
- **The round-4 suites switch off nothing.** The 15 logins they minted were switched off by hand
  (`tools/switch-off-login.mjs`). Their 10 employees, 6 events, 1 room and 3 bookings stay on UAT:
  R4-2.1, lane 6's tidy step.
- API log: only cross-module #23 (the payroll profile FK, on every fixture employee) and the first
  pass's user delete.

*1b — what was built (2026-10-05):*
- **The working-day calculator's day-off set** (`GetHolidayDatesAsync`, `AddWorkingDaysAsync`,
  `CountWorkingDaysAsync`) adds the days of every company-wide closure that is a day off. Leave, the
  discipline clocks (F-29) and travel's on-duty posting read it unchanged. It is read for the range
  asked, because a yearly closure has no last day. `GetHolidaysAsync` stays holidays only, so the
  diary does not list a closure twice.
- `IHrClosureCalendar.GetClosureDatesAsync`: each employee's closure days off.
- **Leave's `GetChargeableDaysAsync` takes the employee** and adds those days. All ten call sites pass
  the employee they hold. The leave usage reader and the leave reminder sweep add each person's own
  closures, read once per run, tenant-explicit.
- **The leave calendar names a company-wide closure day "Closure: title"**. A site or unit closure is
  not drawn on that layer, which runs under every row.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a + 1b: **93/93 on two clean passes.** 1b reads what leave would charge
  for a working week through `GET /api/Leaves/excess-preview`, which counts with
  `CalculateLeaveDaysAsync` and saves nothing. It measures before and after each closure:
  - company-wide: A, B and C each lose a day;
  - a site: A and C lose one, B (another site) is charged in full;
  - a unit: A and B (beneath it) lose one, C is charged in full;
  - partial: nobody loses one;
  - a yearly closure: next year's repeat is not charged.

  The leave calendar names the company-wide day and does not draw the site day. Deleting the closure
  charges the day again.
- The first pass failed one harness step: it deleted a closure as HR, which needs HR.Company.Admin —
  a correct refusal. Fixed to delete as admin.
- **Not driven end to end:** the discipline clocks and travel's posting. They share the loader that
  the leave calendar's layer proves; driving them means opening a disciplinary case or approving a
  trip on UAT.
- Regression, once each: `run-slice0` 24/24, `run-slice1` 32/32, `run-slice2` **62/62 on its third
  run**, `run-slice3` 44/44, `run-round4-d` 39/39.
  - `run-slice2`'s first two runs died at the event detail read. That is a 30-second SQL timeout on a
    single query joining four `Employee` records: 1.3 GB of 23.4 GB RAM was free, and SQL Server was
    trimmed to under 1 GB. The read is untouched by 1b. Restarting the API freed memory and the suite
    passed.
- **The leave harness, a targeted pass of the suites that count days** (`dev-harness/hr-leave`, on the
  user's go):
  - `run-slice1-lifecycle` **72/75**. The 3 failures are the README's documented UAT gap: UAT was
    seeded with the two-stage ladders directly, so there is no old single-stage row to supersede.
  - `run-slice2-attendance` **31/31**; `run-slice5-recall` **51/51**.
  - **`run-slice4-reads` NOT run.** Its section [6] runs the real tenant-wide leave reminder sweep,
    and the preview (`GET /api/hr/leave/reminders/preview`, which claims nothing) said that sweep
    would send **4,203 reminders** on UAT: 4,193 "annual leave outstanding", 7 awaiting a decision,
    2 not closed, 1 available. ⚠ The API's hosted sweep would send them the first time it stays up
    past its start delay.
  - Clean-up after the 1b run:
    - the run's 4 minted leave types were switched off, back to TDC's 9;
    - the leave fixture logins (`leave.hr`, `leave.mgr`, `leave.emp`, `leave.hr2`) were created by
      this run, since UAT was rebuilt on 2026-09-28, and were switched off — they are HR and Manager
      logins whose passwords are in the harness README. A future leave run switches them on first.
- Residue: 21 more round-4 `csv_` logins were switched off by hand. 8 events, 1 room, 3 bookings and
  14 employees stay on UAT (R4-2.1).

*1c — what was built (2026-10-05):*
- **`ILeaveService.RechargeForDaysOffChangeAsync`.** Approved, in-progress and completed leave touching
  the changed days is recounted with the one chargeable-days definition (with the employee's overlay).
  Each request whose count changed gets, in order:
  - the count and the balance in one transaction (`RecalculateAsync` re-derives from `TotalDays`, so
    nothing is given back twice);
  - the attendance days re-posted (`ReconcileAttendanceAsync`);
  - a notice to the employee.

  A request in a finished leave year (before the current one) is listed in `notRecharged` for HR, not
  recounted. A request that fails is reported in `failures` and the rest go on.
- **The notice:** a new leave notice kind, `LeaveReminder.LeaveRecharged`, in the app and by email.
  The employee gets it when they have a login, and HR (`…Hr`) when they have none. It names the leave
  type and number, never a reason.
- **Triggers:**
  - closure create, update and delete, over the closure's days before and after (a yearly closure to
    two years past today);
  - holiday add, update and delete in `HolidayCalendarService`, over the holiday's days and its day in
    lieu;
  - a calendar becoming or ceasing to be active or default, or an active calendar deleted: the full
    recount.

  The answers carry `leaveRecharge`; the three deletes answer 200 with the recount instead of 204.
- **The one-time pass:** `POST /api/CompanySchedule/closures/recharge-leave`, Company Admin, for
  closures that predate the recount. *Beyond the plan:* `?dryRun=true` answers the same list, saving
  nothing and telling nobody, because the pass touches every granted request in the open years.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–1c: **117/117 on two clean passes.** 1c raises approved leave for
  the suite's own HR actor (a person WITH a login), using a minted type that needs no approval.
  - A closure added over it: the answer lists the recount; the count and the attendance days drop by
    one; the employee is told in the app.
  - Renamed: nothing recounted, nobody told again.
  - Moved off: recounted back.
  - Deleted where it covered nothing: nothing recounted.
  - A holiday added, then removed: recounted each way.
  - A request backdated by SQL into last year: listed in `notRecharged`, count unchanged.
  - A count planted wrong: the dry run lists it and saves nothing; the real pass puts it right; a
    second pass finds nothing of ours.
- **The dry run listed nothing but the suite's own requests**, so the real one-time pass ran on UAT —
  and showed that no real leave on UAT has a stale count today.
- Each recount sent one in-app notice (Sent) to the fixture's login and one email copy (Failed: UAT
  has no mail server). Nobody else was told.
- **Not exercised:** the HR fallback for an employee without a login, because on UAT it would message
  every HR user of the demo.
- Regression: the round-4 net **201/201**. Leave slices 1/2/5 scored **72/75 · 31/31 · 51/51**; the 3
  are the documented UAT gap.
- Clean-up, verified in SQL: 0 harness logins left on (19 switched off, the leave fixtures among them);
  9 active leave types; no harness holiday or granted harness leave; the one live closure is the
  stocktake. API log: only cross-module #23.

*1d — what was built (2026-10-05):*
- **Payroll's read (D-15c).** `IHrClosureCalendar.GetClosureDaysAsync(tenantId, employeeIds, from, to,
  includePartial)` answers, per employee, each closure day with:
  - the closure's id and title;
  - the pay flag;
  - whether the day is worked.

  A partial closure is a working day, so it is left out unless asked for. Over HTTP it is
  `GET /api/CompanySchedule/closures/employee-days` (`HR.Company.Read`). It takes up to 500 employees
  and a year at a time, and an id from another tenant answers no days. It is a pull: nothing is pushed
  into payroll, and no payroll code is touched.
- **The hand-off.** `HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` gains a § 2.1 paragraph on closures (what
  they are, the pay flag, where to read them). It also gains § 3 item 4: an unpaid closure day produces
  no deduction.
- **Announce on HR's click (L1-1).**
  - `GET closures/{id}/announcement` (`HR.Company.Write`) is the preview. It gives the number of active
    staff covered, whether it can be announced, and the title, summary and body.
  - `POST closures/{id}/announce` (`HR.Company.Write`; the announcer is the signed-in person's
    employee record) creates an `HrAnnouncement` and publishes it. The announcement is:
    - addressed by the closure's own audience rule, the same rule leave and the diaries use;
    - shown until the day after the closure ends;
    - about the next occurrence, for a yearly closure.
  - Refused, 422 with the reason, before any draft is made, when:
    - the closure is over;
    - nobody is covered.
  - The wording:
    - title: "Closure: …", or "Reduced operations: …" for a partial closure;
    - summary: "{who} will be closed on {dates}.";
    - body: the reason, then "Staff are paid for these days." or "These days are unpaid.", then a note
      that leave over them no longer counts them;
    - for a yearly closure, a line saying it repeats.
- **A-92** is updated in the attendance guide's ledger, not the cross-module register (lane 1's
  checklist says why).

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–1d: **134/134 on two clean passes**. 1d has 17 assertions.
  - **Payroll's read:** a person at the closure's site has the paid company day and the unpaid site
    day. A person at another site has only the company day. Each day names its closure. A partial
    closure is left out by default and comes back flagged as worked when asked for. More than a year
    is refused.
  - **The preview:** a unit with nobody in it reaches 0 and offers no announcement. Announcing it is
    refused, saying why. So is announcing a closure that is over. A live closure's preview is titled
    as a closure, says who, when and why, says staff are paid, and reaches the unit's staff.
- **⚠ Not proved in 1d: the real publish** *(proved in 1e, on a unit of the suite's own)*. The suite announces for real only when the closure reaches
  nobody but the run's own people. The fixture unit reaches 44 active people, so it skipped. That unit
  is harness residue: `A25Ver Unit S53H0TG`, made by a harness on 2026-10-01. Its 52 employees are all
  harness-made, the round-4 suites' among them (R4-2.1). The publish itself is
  `HrAnnouncementService`'s existing, unchanged path. To prove it, either:
  - 1e's suite gives the run a throwaway unit of its own; or
  - lane 6's tidy step empties that unit.
- Regression: the round-4 net **201/201**.
- Notices, read in SQL: the recount notices of block 1c went only to the run's two fixture logins (in
  the app, Sent; by email to `@example.com`, Failed: no mail server). No announcement was created.
- Clean-up, verified in SQL: 0 harness logins left on (the run's own, then the round-4 suites' 15,
  switched off); 9 active leave types; no fixture employee left; the one live closure is the stocktake.
- API log:
  - Cross-module #23 (the payroll profile FK), on every fixture employee.
  - Mail failures (no SMTP on UAT).
  - Two errors from master's HR/Identity reconciliation sweep (`HrIdentityReconciliationService`,
    20 s after start, then on an interval), neither caused by 1d:
    - one caught this run's login in the middle of its teardown, after its employee was deleted
      ("Sequence contains no elements");
    - one is UAT data: `property.manager` and employee TDC/00052 ("more than one element").

  *Logged 2026-10-05 as cross-module **#39**, on the user's go.* The first error recurs: `property.manager` has failed
  in every run since the rebuild, 261 times, because that employee's manager has two active demo logins.

*1e — what was built (2026-10-05):* the closures screen,
`frontend/src/app/administration/hr/company-schedule/closures/page.tsx`. Frontend only; no backend
change.
- **"What closes" decides the form.** The choices are whole company, one site, one organisation unit,
  or reduced operations, each with a line saying what it covers:
  - a site closure shows the site select, required. Its note says a region covers nobody (C-16, lane 5).
  - a unit closure shows `OrganizationUnitPickerField`, required.
  - reduced operations asks for exactly one of the whole company, a site or a unit.

  The form sends only the scope the kind uses, so a site left over from an earlier choice is not sent
  and refused. `departmentId` is gone from the wire: the server refuses it, so today's screen failed
  whenever a department was picked.
- **"A working day" is shown locked**, with the reason: off for a closure (leave over it is not
  charged), on for reduced operations. The server sets it from the type.
- **"Recurs every year"** is a switch. The register shows "Every year" under the dates, and a "Covers"
  column with the server's `scopeDescription`.
- **After a save or a removal, a panel above the list keeps the result** until dismissed:
  - the warnings;
  - the leave recounted (request, person, type, dates, old → new days);
  - the leave left for HR in a finished year;
  - any failures.

  After saving a closure that is a day off and not over, the panel offers "Announce to the N staff it
  covers", with N from the preview. It says "nobody to tell" when nobody is covered.
- **Announcing** (`ClosureAnnounceDialog`) shows the reach and the exact title, summary and body, as
  text. It publishes only on its button. Each row's menu has "Announce to staff…" until the closure
  is over.
- **Also:**
  - the delete answers the recount, which the panel shows;
  - `is-closure-date` takes `organizationUnitId` on the client too;
  - the HR landing page's closure list shows the scope sentence; it showed "—" for unit closures;
  - the "Announced by" column is renamed "Recorded by", which is what it holds.

*Proof:*
- Type-check: `tsconfig.company-schedule.json` (new, scoped to the module), **0 errors**; the four
  touched files are confirmed in its file list. ESLint on the touched files: clean, after three
  non-null assertions were replaced.
- `run-final-review.mjs` blocks 1a–1e: **156/156 on two clean passes.** 1e has 22 assertions:
  - **The screen's payloads**, one per kind, built the way the form builds them. Each is saved with
    the scope sentence the list will show, and is a working day only for reduced operations.
    Reduced operations at one site and at one unit are new on the screen. A reduced-operations day
    stays "not a closure date" at its site.
  - **The real publish (L1-1),** on a unit and a post the run makes beneath P, holding only H, the
    HR actor:
    - the preview reaches exactly 1;
    - the announcement is published, to 1, addressed by that unit, in the preview's words, and shown
      until the day after the closure;
    - it is in H's own announcements (`/api/employee-portal/announcements`);
    - H's login is told in the app, and nobody else is told.
- **The suite changed:**
  - H is minted into the run's own unit. On UAT no unit can be made outside P: P is the root of
    TDC's structure, and C's unit "General" is the only unit of a second, one-level structure. No
    earlier assertion reads H's unit.
  - Clean-up deletes the post and the unit after the employees, and archives as the HR login. The
    admin login has no employee record and is refused (403), which is how the first full run left
    its announcement live; the next run's clean-up archived it.
  - `tools/sql.mjs` reads retry twice and report sqlcmd's own words: one read failed once in 1c,
    then answered at once by hand.
- Regression, the round-4 net, **201/201 twice** (lane close).
- In SQL afterwards: each run's announcement notified only that run's login, in the app, with no
  email. All three are archived. No `CSF` unit, post or employee is left, and no harness login is on;
  9 leave types; the one live closure is the stocktake.
- API log: only #23, #39 and the mail failures. One `/health` answered 500 on the API's first second,
  and 200 four seconds later.
- **Not done: a browser walk of the screen** (lane 5). The payload checks build the form's shape in
  the suite; they do not drive the page.

### Lane 2 — Events (D-2, D-3, D-5, D-10, D-11, D-12, D-14, D-16; C-7, C-10…C-15, C-18…C-25, C-29, C-51, R4-5.1, R4-6.3, R4-6.4, R4-6.7, R4-7.1, R4-10A.2, F-1, F-8…F-12, F-30…F-46, F-54)

**Validation and references**
- [x] *✅ 2a (`CompanyEventRules`); a missing date, which bound as 0001-01-01, is refused too.* One window validator for create, update and reschedule (end on or after start; times only when
      not all-day, end time after start time on a same-day event; RSVP deadline on or before the
      start; `SendReminders` ⇒ `ReminderDaysBefore` ≥ 0; `RequiresRsvp` ⇒ a deadline).
- [x] *✅ 2a for the event's site, unit and organiser (an active employee); ✅ 2d for guests (active) and task assignees (active when newly assigned), and the register's employee (this tenant's, a leaver allowed).* `EnsureExistsAsync` (tenant-scoped, not deleted, refusing as a rule — a 422, not a "not found")
      for `LocationId`, `OrganizationUnitId`, participant `EmployeeId` (active), task `AssignedToId`.

**Lifecycle** (*review: two of the first draft's guards contradicted other rules*)
- [x] *✅ 2a for the guards (the organiser may not approve their own event); ✅ 2b on the engine, with Reject (which cancels the event) beside it.* **Approve:** requires approval and not yet approved, while Scheduled, Rescheduled or Postponed —
      the first draft's "Scheduled only" stranded a moved event, since a reschedule now sets
      Rescheduled. On the engine per D-10.
- [x] *✅ 2a.* **Cancel:** not cancelled or completed; cascades to the event's live linked bookings.
- [x] *✅ 2a; a move to the same window is refused, and a move that names no times keeps the hours. The bookings move without lane 3's room limits, which lane 3 applies.* **Reschedule:** not cancelled or completed; window valid; status → Rescheduled (C-7);
      Accepted/Tentative answers reset to Sent **and the RSVP chase stamp cleared**; refused when the
      RSVP deadline would fall after the new start unless a new deadline is given; **linked live
      bookings move with the event**, each re-checked for clashes, the reschedule refused naming any
      room that is taken; an approved event that moves returns to awaiting approval, as a moved booking
      does (D-10). (F-38)
- [x] *✅ 2a.* **Complete:** not cancelled, not already completed, **not before the event starts** (F-40).
- [x] *✅ 2a; answers 200 with the bookings cancelled.* **Delete** (Admin): cancels the event's live linked bookings first, as cancel does (F-39).
- [x] *✅ 2a; an edit that moves the window needs a reason and takes the reschedule path.* **Update:** refused on a cancelled or completed event. `Status` only Scheduled / InProgress /
      Postponed, and Confirmed only where no approval is required — the first draft allowed Confirmed,
      which made an unapproved event look approved. **Any change of start or end date or time, or of
      the all-day switch, goes through the reschedule path** (F-37, R4-7.1).
- [x] *✅ 2a (`CompanyEventRules.IsFirm`).* **(F-41)** The clash check treats an accepted invitation to an event that needs no approval as
      firm once it is Scheduled or Rescheduled — no data change.

**Recurrence (D-2, D-12 — settled: a light series)**
- [x] *✅ 2f-1 (`CompanyEventSeries`), with "Weekdays" added (the user's ruling); the flag is also warned of on the
      save, and an occurrence on a yearly 29 February falls on the 28th.* Create: a pattern and a count or end date required, up to 52 occurrences. Each occurrence is a
      full event with its own number, sharing `RecurrenceSeriesId` and carrying `OccurrenceNumber`.
      Dates are counted from the first, so a monthly rule on a day a month lacks falls on that month's
      last day; an occurrence on a public holiday or a company-wide closure is generated and flagged on
      its page.
- [x] *✅ 2f-2a (adding and removing a guest, and the desk's answer) and 2f-2b (editing — only what changed, the
      user's ruling —, rescheduling and cancelling).* A scope choice — "this occurrence / this and following / the whole series" — on adding a guest,
      removing a guest, editing, rescheduling (the following move by the same offset) and cancelling.
      **A series action never changes an occurrence that is past or completed.**
- [x] *✅ 2f-2a at the desk, with a calendar file per date and one notice per guest per action (the user's ruling);
      the D-8 reply door is lane 7's.* A guest added with series scope gets **one** invitation listing the dates (a new catalogue entry,
      `EventSeriesInvitation`), and answers each date or all at once — through the desk door and the
      D-8 reply door alike. Reminders and the RSVP chase stay per occurrence. If D-14 is taken, each
      occurrence carries its own calendar entry.
- [ ] *Moved to lane 3 (the user's ruling at 2f's source check), on its rules and lock.* "Book this room for every occurrence": one booking per date through lane 3's rules and lock;
      the dates where the room is taken are listed, the rest booked.
- [x] *✅ 2f-1: from any occurrence, by a count or to a date, copied from the latest occurrence; its approval is
      shared like the create's.* "Extend the series": more occurrences after the last, on the same rule, within the cap.
- [x] *✅ 2f-1, with the series list on the page (each occurrence flagged where it falls on a day off).* DTO gains `recurrenceSeriesId`, `occurrenceNumber`, `occurrenceCount`; the page shows
      "Occurrence 3 of 10" and links the series; the register filters by series.

**Audience and organiser** (*review: reuse the audience resolver; no new `EventAudience` helper*)
- [x] *✅ 2a for the picker, the creator stamp, the diaries and the clash check; ✅ 2e-1: the organiser is reminded, on the guest list or not, and told of an approval or a rejection.* The organiser per **D-11**; the organiser and the participants always see and are committed to
      the event.
- [x] *✅ 2c (`CompanyEventRules.AudienceRuleOf` / `CalendarAudienceOf`, the resolver's new Management rule); lane 7 reads the same rule.* When `ShowOnCompanyCalendar` is on, the event's audience is an audience rule: `Scope = AllStaff`
      → `AllEmployees`; `Scope = Department` → the event's `OrganizationUnit` (its subtree);
      `ManagementOnly` and `Management` visibility per **D-16**; Private and Confidential reach
      participants and the organiser only. Evaluated with `IHrAudienceResolver`; used by
      `CompanyEventCommitmentSource` and lane 7. Form descriptions say exactly this.
- [x] *✅ 2c, on HR's click from the event page (L1-1's rule), never on save.* `ShowOnIntranet` publishes an `HrAnnouncement` to the same audience (*review: the first draft
      only relabelled it*); `Priority` stays a label and says so.
- [x] *✅ 2a (`CompanyEventRules.DailyWindows`).* **(R4-10A.2 — backend, moved here from lane 5)** `CompanyEventCommitmentSource` yields a window
      per day for a multi-day timed event (`WindowOf` takes the start day only today), so days two
      onward reach the clash check and the diaries.

**Participants, attendance, tasks**
- [x] *✅ 2d; ✅ 2e-1 tells the removed person (by email and in the app — not one never invited).* Participants: an external guest needs a name AND an email; duplicate external email refused; no
      adds to a cancelled or completed event; no inactive employee (F-35); replies limited to Accepted /
      Declined / Tentative, on the event in the route (F-11). **Removal moves to `Write`** — uninviting
      is organiser work — and the person is told.
- [x] *✅ 2d; the check-in must also fall on one of the event's days and not be still to come.* Attendance: the re-mark bug (F-1); no check-out before check-in or twice; none on a cancelled
      event or **before the event starts**.
- [x] *✅ 2d for the completion date, the guard and Overdue on read (`isOverdue`; Overdue refused as a status to set); ✅ 2e-1 tells the assignee (added, or passed to them); ✅ 2e-3 the overdue chase, once, stamped (`OverdueChasedAt`), to the assignee only.* Tasks: `Completed` through update sets `CompletionDate`; complete-task guard; `Overdue` computed
      on read; the assignee is told on assignment and chased by the hourly sweep when overdue (F-34).
- [x] *✅ 2d.* (D-9, C-21) `DELETE events/{eventId}/attendance/{attendanceId}` on `Write`; (C-22)
      `PUT participants/{id}` on `Write` — role, required, special requirements, and the external
      guest's name, email and organisation.

**Notifications** (F-31…F-36; *review: the first draft only reworded the Reminders card*)
- [x] *✅ 2e-1: `CompanyScheduleNotices`, travel's model — 12 topics, in the app only, seeded on first use; email stays on the catalogue; nobody told of their own act but a reminder or a chase.* Every company-schedule notice also goes **in-app** to internal recipients: an
      `EntityActivityEvent` with the recipients in `Data["RecipientUserIds"]`, on topics seeded with
      in-app on — the pattern in `HrAnnouncementService.NotifyAudienceAsync`. *At 1163bbc47 a closer
      model exists, travel's `StaffTravelNotices` (§ 3c): one notices class owning every topic, login /
      email-only / unreachable handled, the actor left out, never failing the act. Choose between the
      two at the source check.*
- [x] *✅ 2e-2: counted from the email result and the in-app notice; an invitation Sent, and a reminder or chase stamped, only once it reached somebody; undelivered invitations sent again by a button; the card (now "Invitations and reminders") and every toast show issued and reached.* Deliveries are counted from the email result; the Reminders card and the toasts show issued and
      delivered (R4-6.3).
- [x] *✅ 2e-1, the wider F-33 too: the buttons refuse what the sweep refuses (closed, postponed, awaiting approval, begun; a chase with no replies asked or after the reply-by date), by one rule both read.* Invitations wait while an event awaits approval and go on approval; "Send reminder now" and
      "Chase unanswered now" refuse such an event, as the sweep does (F-33).
- [x] *✅ 2e-1, five new catalogue emails, with not-approved told to the organiser too; a new time is the existing rescheduled notice.* New notices: event approved (to the organiser), postponed, changed in time, venue or link,
      participant removed.
- [x] *✅ 2d: the shared send loop and the invitation both skip a leaver, and a leaver cannot be invited.* Every send skips inactive employees (F-35).
- [x] *✅ 2e-3: `HrCalendarFile`, one stable UID per event, SEQUENCE stored and raised with each change; REQUEST and CANCEL on every change (the user's ruling); the organiser on the file (or the mail server's address) for external replies, recorded at the desk; interviews on the same builder.* Calendar invites and external replies per **D-14** (F-36).

**Reads and contracts**
- [ ] *✅ 2a for events (the repository's nine reads removed; the service's own tenant-scoped query); ✅ 2d for guests, the register and tasks (their repositories' custom reads removed); rooms, bookings, milestones and fiscal are their lanes'.* Range endpoints: overlap semantics; UTC dates throughout the repository; **the tenant filter
      inside every repository query** (F-30).
- [ ] *✅ 2a for events; ✅ 2d for tasks (a reassignment answered with the old assignee's name) and guests; rooms and bookings lane 3.* Update responses re-read after save, as creates do (F-46).
- [x] *✅ 2a; the server refuses them on create and on a change of category.* (F-44) "Holiday" and "Milestone" leave the category picker (the values kept for old rows), with
      a line pointing to public holidays and milestones.
- [x] *✅ 2a; the department is refused on the wire (D-5), and the page shows the unit.* `events/department/{id}` → `events/unit/{id}`; the event form's Department picker →
      `OrganizationUnitPickerField`.
- [x] *✅ 2g-1, export on the register's read permission (the user's ruling); the series filter rides on the search.* (D-9, C-10…C-13, C-25) `GET events/search` and `GET bookings/search` — server-side filters
      (text, status, category, site, unit, organiser, from/to on overlap), sort and paging; `GET
      events/export` and `GET bookings/export` answering `text/csv` for the same filters (the
      `LeavesController` export precedent); `GET CompanySchedule/dashboard` — the landing's four lists
      and counts in one read. The two registers move onto them with page controls and a date filter.
- [x] *✅ 2g-2, as the user ruled: refused only whole-company against whole-company or a unit against the same unit, in
      the same place (the same site, or either with no site); audiences as `AudienceRuleOf` reads them; a series' every
      date; extend too.* (D-9, C-15) `GET events/clashes?start&end&excludeId&scope&unit&site` listing the live events
      that overlap; create, update-with-dates and reschedule refuse an AllStaff-against-AllStaff (or
      same-unit Department) overlap on the same site or company-wide, with a sentence naming the
      other event; the form shows the rest as warnings before save.

**Attachments and the drill**
- [x] *✅ 2h, as listed; removal moved to Write (the user's ruling), and a cancelled event takes no file.* Attachments on the gate: multipart `POST events/{id}/attachments` through
      `HrAttachmentUpload.ExecuteAsync`; `GET attachments/{id}/download` through
      `HrDocumentDownload.ServeAsync` after proving the event is the caller's tenant's;
      `CreateEventAttachmentDto` deleted; `AttachmentsPanel` becomes an upload panel with download
      links (the recruitment requisition pattern). **Legacy path-only rows read "reference only — no
      file stored", with no download link (F-54).**
- [x] *✅ 2h, as listed, for everyone and never blocked (the user's ruling); clearing the next date, or deleting the plan, cancels it too.* (D-9, C-51) `SourceEntityType` / `SourceEntityId`: `SheEmergencyService` (`AddDrillAsync` /
      `UpdateDrillAsync` / `DeleteDrillAsync` in `SafetyEmergencyGovernanceServices.cs`; *the first
      text named an `EmergencyDrillService` that does not exist*) creates a company
      event (category CompanyEvent, type Internal, organiser = the drill's coordinator, site = the
      drill's location, all-day on `NextDrillScheduledDate`, name "Emergency drill: plan name") when a
      drill is recorded with a next date, and moves it when that date changes; the event page shows
      "From emergency drill DRL-… (plan)" with a link; deleting the drill cancels the event.
      *Source check (§ 1c):*
      - the drill number is typed by hand;
      - the name is cut to the event's 100 characters;
      - the event is made with reminders off (SHE sends "DrillDue" itself);
      - it is made server-side, so SHE users need no HR permission.

**State (2026-10-05): source-checked (§ 1c); D-10, D-11, D-14, D-16 settled the same day.** Slices, each
built, proved twice and handed over on its own:
- **2a** windows, references and lifecycle guards:
  - the update-dates-through-reschedule path, C-7, F-37…F-40;
  - the organiser (D-11) and the `CreatedBy` stamp;
  - clash firmness (F-41) and the multi-day window (R4-10A.2);
  - reads: overlap, tenant inside the query, re-read after update, F-58's original dates;
  - the unit replacing the department, and F-44.
- **2b** approval (D-10).
- **2c** the audience (D-16), the calendar commitment source and `ShowOnIntranet`.
- **2d** participants, attendance, tasks: F-1, F-11, F-35, C-21, C-22, removal on Write.
- **2e** notices, in three parts (the user, 2026-10-05: "go with both" — the split, and the overdue
  chase below):
  - **2e-1** the in-app notices class; the new notices (an event approved, postponed, or changed in
    time, venue or link; a guest removed; a task assigned); the organiser reminded (D-11); F-33
    (invitations wait for approval; the per-event buttons refuse what the sweep refuses).
  - **2e-2** ✅ delivered vs issued (R4-6.3). ⚠ Until Platform fixes **#40**, the hourly sweep's sends find no mail
    server on a server that has one (nobody is signed in), so 2e-2 counts them as not delivered — truthfully.
  - **2e-3** ✅ calendar invites (D-14) and the overdue-task chase (F-34), with the one migration: the
    event's invite sequence counter and the task's "chased at" stamp. **The chase is sent once**, on the
    first sweep after the due date, and stamped so the hourly sweep never repeats it (the user's choice
    over a repeating chase).
    *Source check (2026-10-05):*
    - **Sending a calendar file.** The production mailer sends attachments only; it has no inline calendar
      part. The `.ics` therefore goes as an attachment, typed `text/calendar; method=…`, as the interview
      invites already do. `SendForTenantAsync` gains the attachments its core already supports.
    - **The interview builder** (`JobInterviewService.BuildInterviewIcs`) mints a random UID per email, so
      every reschedule adds a second calendar entry. It uses floating local times and has no ORGANIZER,
      no CANCEL and no stored sequence. No suite asserts on it.
    - **UAT has one task the first sweep after 2e-3 would chase:** scenario 110's board-pack task,
      `TDC/00001`, due 4 Oct.

    *Rulings by the user (2026-10-05, all as recommended):*
    - **Every change carries the calendar file:**
      - invitation, moved, and venue or link changed: METHOD REQUEST;
      - cancelled, not approved, and a guest taken off the list: CANCEL;
      - postponed: CANCEL, and the move to a new date re-sends the entry as a REQUEST.

      Reminders, chases and task emails carry none.
    - **Interviews:** a stable UID per interview on the shared builder, with no counter. A moved interview
      updates the entry by its newer DTSTAMP, as RFC 5546 allows, and recruitment needs no schema change.
    - **The overdue chase goes to the assignee only**, by email and in the app. The organiser sees Overdue on
      the event page.
    - **Cross-module #43 is registered.**
- **2f** ✅ recurrence as a light series (D-12).
  *Source check (2026-10-05):*
  - The five recurrence fields are saved on create and generate nothing (C-14).
  - `RecurrenceSeriesId` and `OccurrenceNumber` are read and written by nothing.
  - The edit ignores recurrence.
  - Approval asks the engine per event (`HasActiveApprovalInstanceAsync`).
  - Event numbers come from an atomic counter, so a series can take up to 52 at once.

  *Rulings by the user (2026-10-05, all as recommended):*
  - **Approved once, for the series:** the first occurrence goes to the engine. Approving any occurrence approves
    every occurrence of the series still awaiting approval with no approval of its own under way; rejecting
    cancels them. An occurrence moved on its own later is approved on its own (D-10). Deciding an occurrence that
    shares an approval under way elsewhere is refused, naming that one.
  - **One notice per guest per series action,** listing the dates, with a calendar file per occurrence. Two new
    templates: `EventSeriesInvitation` (D-12's) and `EventSeriesChanged` (moved, changed, cancelled or removed,
    saying which). 52 templates.
  - **"Book this room for every occurrence" moves to lane 3,** on its rules and lock.
  - **A new "Weekdays" pattern** (Monday to Friday). "Daily" stays every calendar day. An enum value only.

  *Split in two:*
  - **2f-1** ✅ (2026-10-05; built and proved, below):
    - generating a series: up to 52, a count or an end date (one, not both), the patterns with Weekdays, and
      the monthly last-day and 29 February rules, counted from the first date;
    - the series approval;
    - the holiday and closure flag on an occurrence's page;
    - "Occurrence k of n", the series list, and the register's series filter;
    - extending a series;
    - the pattern locked on edit.
  - **2f-2:**
    - the scope choice on guests, edit, move and cancel, never touching a past or completed occurrence;
    - the two series notices;
    - answering per date or for the series at the desk. The D-8 reply door is lane 7's.

    *Source check (2026-10-05, at HEAD 34cbccc83):*
    - **Approving a series sends N invitations.** It invites each covered occurrence's waiting guests one occurrence
      at a time, so a guest on five waiting dates gets five invitations.
    - **Cancelling or deleting occurrence 1 strands a series awaiting approval.** That occurrence carries the
      approval, and cancelling or deleting it withdraws the approval on the engine. The rest are left waiting with
      nothing under way, and with a definition published nothing can decide them.
    - **Extending ignores a series move.** It anchors on occurrence 1's original date, so after a "this and
      following" move it would continue on the old timing.
    - **Moving an approved series would start one approval per date.** Each move clears its occurrence's approval
      and starts a fresh one.
    - **The email renderer** lists dates through a raw `{{{token}}}` that the application builds, declared `IsHtml`
      on the descriptor (the template editor checks it). A guest's rows across a series are matched by employee, or
      by an outside guest's address.

    *Rulings by the user (2026-10-05, all as recommended):*
    - **An edit with series scope copies only what that edit changed;** a difference set on one date on purpose is
      kept.
    - **Extending invites the latest occurrence's guests to the new dates:** one series invitation each, or waiting
      for approval.
    - **Two slices:**
      - **2f-2a** ✅ (2026-10-05; built and proved, below): guests and answers with scope; the series invitation,
        including the approval's and the extension's;
      - **2f-2b** ✅ (2026-10-05; built and proved, below): edit, move and cancel with scope; the approval passed on
        when its occurrence is cancelled or deleted; extending after a series move; one approval for a moved series.

    **2f is done** (2f-1, 2f-2a, 2f-2b). "Book this room for every occurrence" is lane 3's.

    The four findings above are fixed as defects: the first in 2f-2a, the other three in 2f-2b. Removing a guest
    with series scope needs the series-changed email, so **both templates come in 2f-2a** (52), and 2f-2b adds its
    kinds of change to the same email.
- **2g** ✅ search, export, the dashboard and clashes (C-10…C-13, C-15, C-25) on the two registers.
  *Source check (2026-10-05, at HEAD after 2f-2b):*
  - **The registers:** both load every row (`GET events`, `GET bookings`) and filter, sort and page nothing on the
    server. 2f-1's series filter is in the browser too.
  - **The landing page** makes four reads: upcoming events (30 days), bookings awaiting approval, closures and
    milestones.
  - **No export exists.** The leave module's CSVs are the precedent (`LeaveService.CsvCell`): a UTF-8 byte-order
    mark, every cell quoted, and a cell starting `= + - @` prefixed so a spreadsheet does not run it.
  - **No event-against-event clash rule exists (C-15).** 2a's check is people's diaries (the panel commitments), not
    one event's audience against another's.

  *Rulings by the user (2026-10-05, all as recommended):*
  - **C-15, as D-9:** the server refuses only a whole-company event against a whole-company event, or a unit's
    against the same unit's. Any other overlap (a whole-company event against a unit's, say) is a warning the form
    shows before saving.
  - **In the same place:** both at the same site, or either with no site (online, or company-wide). Two whole-company
    events at different sites at once are allowed.
  - **Export** on the register's own read permission (`HR.Company.Read`), as the leave register's is.
  - **Two slices:** 2g-1 ✅ (2026-10-05; built and proved, below), search, paging, the date filter, CSV export and
    the one-read dashboard, with both registers moved onto them; 2g-2 ✅ (2026-10-05; built and proved, below), the
    clash rule, the clashes check, and the form's warnings before save. **2g is done.**

  *Applied without a ruling (stated here):* a clash counts live events only, not cancelled, completed or postponed
  ones, and includes those awaiting approval. On a series every date is checked; a refused date refuses the
  action, named, as 2f-2b's refusals do.
- **2h** ✅ (2026-10-05; built and proved, below) attachments on the gate (C-18, F-54) and the drill (C-51). **2h closes
  lane 2.**
  *Source check (2026-10-05, at HEAD c01a398cf):*
  - **Attachments:**
    - lane 0 added `UploadedById`, `FileSizeBytes` and `FileUploadRecordId`;
    - the upload category `HrCompanyScheduleAttachments` exists and is registered scan-mandatory, its limits coming
      from the tenant's upload policies;
    - `HrDocumentDownload.ServeAsync` serves a file from its upload record alone (clean scans only), so no central
      document columns are needed;
    - the endpoint still takes JSON (`CreateEventAttachmentDto`, a name and a path), and removal is Admin;
    - UAT holds 25 rows, every one path-only. Four are on live demo events (scenario 110's board agenda and Q2
      minutes, durbar programme and retreat communique), and 21 on harness events since deleted;
    - scenario 110 and `run-slice2` post JSON attachments.
  - **The drill:**
    - `EmergencyDrill` carries `DrillNumber` (typed by hand), `DrillName`, `CoordinatorId` (required),
      `LocationId` and `NextDrillScheduledDate`; its plan has `PlanName`;
    - drills are shown on the emergency plan's page (`/hr/safety/emergency/{planId}`);
    - UAT has one drill with a next date: DRILL-2026-001, 13 December 2026, Ashaiman Market (demo data);
    - no event has a source yet.

  *Rulings by the user (2026-10-05, all as recommended):*
  - **A drill's event is for everyone and is never blocked:** a drill neither refuses another event nor is refused;
    overlaps with it are warnings only, so recording a drill in SHE can never fail because of an HR event.
  - **UAT's one drill gets its event once,** by saving it again through the SHE API at the proof.
  - **The demo papers become real files:** UAT's four are replaced by small generated PDFs through the new upload,
    and scenario 110 uploads files.
  - **Removing an attachment is Write,** as removing a guest moved to Write in lane 2d.

*2h — what was built (2026-10-05): files through the gate, and the drill's event (D-3, D-9; C-18, C-51, F-54). It
closes lane 2.*
- **No migration** (lane 0 added the attachment's upload columns and the event's source).
- **Attachments are files** (C-18, F-54):
  - `POST events/{id}/attachments` is multipart (a file, its type, a note; 25 MB) through `HrAttachmentUpload` on
    `HrCompanyScheduleAttachments`: scanned, stored, registered in the document register. The row keeps the uploader,
    the size, the upload record and the document-register ids, as the other HR doors keep them.
    `CreateEventAttachmentDto` is gone.
  - **Refused before a byte is stored** (`CompanyEventRules.RefuseAttaching`, asked by the controller and again by the
    service): a cancelled event ("… is cancelled, so no file can be added to it"; a completed one may still take its
    minutes), and a file that does not say what it is.
  - `GET attachments/{id}/download` (Read) serves the file from the document register, else its upload record, clean
    scans only. A path-only row from before answers 404, "Reference only — no file stored…". No path a caller once
    typed is ever served.
  - Removing a file is Write (the user's ruling); it was Admin. The row is soft-deleted, and the stored file stays in
    the document register as the retained record.
  - The event page's Attachments tab: an "Add a file" box (type, a note, the upload field) and the list — file, type,
    note, size, added — with a Download action, and "Reference only — no file stored" on a path-only row. A cancelled
    event shows no box.
- **The drill's event** (C-51; `CompanyEventService.SyncDrillEventAsync`, called by `SheEmergencyService` after its own
  save, server-side):
  - a drill saved with a next date makes one event: "Emergency drill: {plan}" (cut to 100 characters), all day on that
    date, at the drill's site, organised by its coordinator, for everyone, public, on the company calendar, high
    priority, no approval, no reminders (Safety sends its own "DrillDue"), its source the drill;
  - a new next date moves the same event, as a reschedule does (it reads Rescheduled; anyone invited is told);
  - clearing the next date, deleting the drill, or **deleting its plan** cancels it, saying why. A plan's delete
    leaves its drills, so nothing else would ever have touched their events. A date set again makes a new event;
  - **never blocked** (the user's ruling): no clash is checked for it, and `ClashOf` makes any overlap with a drill's
    event a warning, both ways;
  - a failure to keep the event in step is logged and never undoes the drill;
  - the event's detail carries `Source`: "Emergency drill DRILL-2026-001 — {drill} ({plan})", linked to the plan's
    page in Safety; "…, since deleted" with no link once the drill or its plan is gone. The event page shows it as a
    "From …" line under the title.
- ⚠ **Found by the proof, fixed, and a latent 2f-2a defect beside it:** `GetQueryable().IgnoreQueryFilters()` cannot see
  deleted rows — the generic repository's `GetQueryable` filters them with a `Where` of its own (the trap eight other
  services already warn of). A deleted drill read as "no longer recorded". The same read in 2f-2a's
  `ExtendSeriesAsync` meant deleted occurrences did **not** hold their place: a series whose latest date was deleted
  had it made again under the same number, and one whose first date was deleted had every new date counted from the
  second. Both now read `GetQueryableIncludingDeleted`; the series block gained the case the 2f suites never ran.
- **UAT, once, on the user's rulings** (`dev-harness/hr-company-schedule/tools/apply-2h-uat.mjs`, dry run by default):
  the four papers on live demo events (EVT-2026-00001's agenda and Q2 minutes, EVT-2026-00002's programme,
  EVT-2026-00004's communique) were uploaded as small generated PDFs as `hr.head`, and their path-only rows removed;
  DRILL-2026-001 was saved again as `she.officer`, which made **EVT-2026-02641**, 13 December 2026, Ashaiman Market. A
  second dry run finds nothing. ⚠ They were "scanned" by the local scanner stub, which passes every file: they are
  generated samples, and nobody may say UAT's files were scanned.
- **The demo pack:** scenario 110 uploads its four papers as files, replacing a path-only row of the same name;
  scenario 170 saves each seeded drill with a next date and no event once more through the SHE API (SheDataSeeder
  writes DRILL-2026-001 past the service).

*Proof (UAT, API in Staging, the local scanner stub on 3310 for the runs only):*
- `run-final-review.mjs` blocks 1a–2h: **759/759 on two clean passes**, the blocking watcher beside both and silent.
  2h has **40 assertions**, in a window 160 days past the others and checked empty of live events; the series block
  one more:
  - **a file:** stored with its size, its uploader (the signed-in person), its type and note; a clean scan on the
    gate's record and registered in the document register; it downloads as the same bytes, as a PDF; listed on the
    event with its file;
  - **refused:** a program file, by the gate; a file that does not say what it is, before a byte was stored; neither
    left a row; a cancelled event, saying so, before a byte was stored;
  - **a path-only row from before:** `hasFile` false; its download answers 404, "Reference only — no file stored";
  - **removal** on Write, by the HR desk;
  - **the drill** (Safety's doors as the run's own SHE Manager, linked to the coordinator — the SHE API refuses a
    login with no employee, which `admin` is): one event made, with the name, day, site, organiser, audience,
    visibility, calendar, approval, reminders and status above; its source and the detail's line and link; a
    whole-company event at its site that day allowed with a warning naming it, and the form's check listing it as a
    warning; a new next date moving the same event onto a day the whole company already has an event there; a save
    that leaves the date alone moving nothing; clearing the date cancelling it, saying why; a date set again making a
    new one; deleting the drill cancelling that, saying why, its source "since deleted" with no link; a drill with no
    next date making nothing; deleting the plan cancelling its other drill's event, saying so;
  - **the series:** with the first and last of three deleted, one more is number 4, on the rule.
- **Regression:** the round-4 net **210/210** (slice 2 uploads its agenda and checks a file is stored); recruitment
  **58/58**; the templates probe **30/30**; `run-lane-n` **109/115**, the six section-J failures of #40.
- **The API log:** no request answered 500, and no drill sync was logged as failing. Its ERR lines are the known kinds:
  notifications with no mail server, payroll's foreign key on minted fixtures (#23), and the unique indexes refusing
  the suites' duplicate guests and register rows.
- **After the runs:** every harness login is off (28; the no-email one by SQL, #42). The R4D requisition's notices to
  real staff were withdrawn twice (43, then 0), 0 live. The API and the scanner stub are stopped.

*2g-2 — what was built (2026-10-05): event against event (D-9; C-15).*
- **No migration.**
- **The rule** (`CompanyEventRules.ClashOf`, pure; `EventClash` None, Warning, Refused). Two events clash when:
  - both are live: not cancelled, completed or postponed; one awaiting approval counts;
  - they are in the same place: the same site, or either with no site (the user's ruling);
  - they are at the same time: their days overlap and, when both are timed, their hours. A multi-day event holds its
    hours on each of its days, as the diaries read it; an all-day or untimed event holds the whole day;
  - at least one is for more than its guest list. Two guest-list meetings are the people diaries' business.

  **Refused** when both are for the whole company, or both for the same unit. **Warned of** otherwise. "For" is the
  audience as `AudienceRuleOf` reads it: visibility narrows, scope widens, so a private event is for its guests
  whatever its scope.
- **Where it is checked** (`RefuseClashAsync`; the candidates are the live events whose days touch, at a site that
  could be the same, leaving out the event itself and its own series):
  - **create:** every date of a series, before an event number is taken; a refused date is named ("Occurrence 2,
    …");
  - **extend:** every new date, the same way;
  - **edit:** only when the edit changes what a clash depends on — the window, the site, the audience (scope,
    visibility, unit), or bringing the event back to live. An event already beside another, from before the rule, can
    still have its description corrected;
  - **reschedule:** before its rooms move; a series move checks every date and refuses all, named, as 2f-2b's
    refusals do.

  Each refusal names the other event, its time, whom both are for and where, and says what to do.
- **Warnings:** the overlaps the server allows come back as warnings on the create, the edit, the extension and the
  move (`CompanyEventChangeDto.Warnings`, new), named by date for a series.
- **`GET events/clashes`** (`EventClashQueryDto`, Read): the events a proposed window, audience and site would clash
  with, each refused or warned of, with the save's own sentence. The event being edited and its series are left out.
- **The form:** under "Who it is for" the event form shows them as the dates are typed — refused in red, warnings in
  amber. A new recurring event's line says only its first date is checked there, and every date on saving. The
  reschedule toast carries the warnings.
  - ⚠ **Caught before the build:** the form's check debounced its query object, which is new on every render, so it
    would have set state every 400 ms for ever. It debounces a string key.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2g-2: **718/718 on two clean passes**, first time, the blocking watcher beside
  both and silent. The clash queries ask for at most 5 MB and run in under 10 ms. No live `CSF-` event or mail-server
  row was left. 2g-2 has **39 assertions**, in a window 100
  days past the others and checked empty of live events:
  - **whole company:**
    - a second event at the same time and site is refused, naming the first;
    - at another site it is allowed, with nothing to warn of;
    - with no site (online) it is refused;
    - one starting as the first ends is allowed;
  - **a unit:**
    - beside a whole-company event it is allowed, with a warning naming it;
    - a second event for the same unit is refused, naming the first;
    - another unit's is allowed and warned of;
    - a private event is allowed and warned of;
  - **what counts:**
    - a cancelled event does not count; one awaiting approval does;
    - a three-day event holds its hours each day, but not the hours between;
    - an all-day event holds the whole day;
  - **moves and edits:**
    - a move onto another's time is refused, naming it, as is a move onto a day with one awaiting approval;
    - an edit taking an event to the same site is refused;
    - an edit making a private event public is refused;
    - a description-only edit of an event already beside another is allowed;
  - **a series:**
    - a weekly series whose second date lands on the event is refused, naming "Occurrence 2", and none of its dates
      is made;
    - extending onto it is refused, naming "Occurrence 3";
    - moving a series onto it is refused with nothing moved, and the series stays where it was;
  - **the form's check:** the whole-company event refused; the unit's and the private one warned of; the one after not
    listed. Each comes with the save's sentence, and the event being edited is left out.
- **The suites the new refusal could have broken:** none puts two whole-company or same-unit events at the same time
  and place. The round-4 net's events are guest-list or management events, 2c's whole-company events fall on separate
  days, and scenario 110's are on different days.
- **Regression:** the round-4 net **209/209**; `hr-recruitment/run-round4-d` **58/58**; the templates probe **30/30**;
  `run-lane-n` **109/115**, the six section-J failures of #40, its fixture tenant removed.
- **The API log:** no request answered 500, and no query timed out. Its ERR lines are the known kinds: notifications
  with no mail server, the sink's bounces and `run-lane-n`'s test send, master's identity sweep, payroll's foreign
  key on minted fixtures (#23), and the race proofs.
- **After the runs:** every harness login is off (the no-email one by SQL, #42). No company-event notice to anyone real
  is live. The R4D requisition's approval notices to md.tdc, managing.director, hr.head and hr.officer were withdrawn
  twice, 0 live.

*2g-1 — what was built (2026-10-05): the registers' search, paging, export, and the dashboard (D-9; C-10…C-13, C-25).*
- **No migration.**
- **`GET events/search`** (`CompanyEventSearchDto`):
  - filters: text in the name, number, venue or organiser's name; status; category; site; unit; organiser; series;
    and a date range, by overlap as lane 2a's range read;
  - sorted: newest first by default, oldest first, by name or number, a series in its own order;
  - paged: 1–200 a page, a size out of range refused 400;
  - each page's ids are sorted on narrow rows first, then only those events are loaded with their names (2e-3's
    memory-grant lesson). Measured on UAT: under 1 MB of working memory and 5 ms or less a query.
- **`GET bookings/search`** (`RoomBookingSearchDto`): text in the number, room, purpose or booker's name; status;
  room; dates by overlap; sorted and paged the same way.
- **`GET events/export` and `GET bookings/export`:** CSV for the same filters, on the register's read permission
  (the user's ruling).
  - The leave register's conventions (`CompanyScheduleCsv`): a UTF-8 byte-order mark, every cell quoted, and a
    cell a spreadsheet would run as a formula prefixed with an apostrophe.
  - Narrow rows, up to 10,000.
  - The events file names the site, unit, audience, organiser, status, approval and the series place ("2 of 4").
- **`GET dashboard`:** the landing page's four lists in one read (the next 30 days' events, the bookings awaiting
  approval, the next 60 days' closures, the next 90 days' milestones). It made four.
- **Screens:**
  - both registers search as you type (a short pause), filter by status (and category), take a From–To date,
    page 25 at a time with a count of everything the filters find (`RegisterPager`), and export what they show;
  - 2f-1's series view rides on the search;
  - the landing page reads the dashboard.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2g-1: **679/679 on two clean passes**, first time, the blocking watcher beside
  both. **The watcher logged nothing at all:** no request waited over 3 s, the first passes since 2e with none. No
  live `CSF-` event or mail-server row was left. 2g-1 has **21 assertions**:
  - **the events register:**
    - by text, the block's five, newest first; oldest first on asking;
    - page 2 of 3 at two a page, with the way back and on;
    - by status, category, organiser and site;
    - a date range finding the three-day event that touches the day;
    - the text found in a venue;
    - one series in its own order with its place;
    - a page of 500 refused;
  - **the events export:** `text/csv` with a byte-order mark, the header, the same five rows in the search's order,
    a name beginning `=` guarded, the cancelled one saying so;
  - **the bookings register:** by text newest first, by a date range, paged; its export, the header and the three;
  - **the dashboard** answering the four lists the landing page read one by one.
- **The harness's notice withdrawal (lane 6's item) proved:** 5 s of work for 132 events, against about 50 s per
  statement before, and nothing waited behind it.
- **Regression:** the round-4 net **209/209**; `hr-recruitment/run-round4-d` **58/58**; the templates probe **30/30**;
  `run-lane-n` **109/115**, the six section-J failures of #40. Its fixture tenant was removed by its own clean-up.
- **The API log:** no request answered 500, and no query timed out. Its ERR lines are the known kinds:
  - notifications with no mail server;
  - the sink's bounces and `run-lane-n`'s test send;
  - master's identity sweep;
  - payroll's foreign key on minted fixtures (#23);
  - the race proofs.
- **After the runs:** every harness login is off (the no-email one by SQL, #42). No company-event notice to anyone
  real is live. The R4D requisition's approval notices to md.tdc, managing.director, hr.head and hr.officer were
  withdrawn twice, 0 live.

*2f-2b — what was built (2026-10-05): edit, move and cancel across a series (D-12).*
- **No migration.** The edit, reschedule and cancel requests carry `SeriesScope`. The edit's own `Scope` is who the
  event is for, so the name differs.
- **The edit, split:**
  - `ApplyEditAsync` applies one event's edit: its checks, a move when the window changes, and what its guests would
    hear of, without saving or telling;
  - `TellEditAsync` tells, optionally to some guests only;
  - a single edit is apply, save, tell, as before.
- **An edit with a series scope** (`UpdateSeriesAsync`):
  - what changed is measured against the date it was made from, field by field (`SnapshotOf`; an empty text and none
    are the same);
  - each date takes only those fields (the user's ruling), so a difference set on one date on purpose is kept unless
    this edit changed that field;
  - a new window moves each date by the same number of days, to the new times when they changed;
  - a new reply-by date keeps its distance from each date's start;
  - every date is applied before anything is saved, so one refused date refuses all, naming it ("EVT-…, Monday …:
    … Nothing was changed.").
- **A move with a series scope** (`RescheduleSeriesAsync`): each date moves by the same number of days, keeping its
  own hours unless new ones are given. A new reply-by date keeps its distance. One refusal refuses all, named.
- **A cancellation with a series scope** (`CancelSeriesAsync`): each date is cancelled with its rooms, and "this and
  following" ends the series there. Their approvals under way are withdrawn.
- **Told once per guest per kind of change** (`TellAcrossAsync`):
  - moved, changed (venue, link or site), postponed or cancelled;
  - a guest on one of the dates gets the single-date notice, on several the series-changed email, with each date's
    calendar update (REQUEST) or cancellation, and a postponed date's entry never sent back;
  - moved carries the reason and asks for answers again; changed names the new venue or link; cancelled carries the
    reason.
- **The three remaining defects of the source check, fixed:**
  - **The approval passes on (finding 2):** when the date carrying a series' approval is cancelled (singly or with a
    scope) or deleted, `PassSeriesApprovalOnAsync` starts it on the next date still waiting, unless one is already
    under way. `CancelApprovalAsync` now says whether it withdrew one.
  - **Extending follows a series move (finding 3):** the new dates carry the shift from the rule that the series'
    latest two consecutive dates share. A date moved on its own differs from its neighbours and is not followed.
  - **One approval for a moved series (finding 4):** a series move clears each date's approval and starts one, on the
    first date; its decision covers the rest.
- **Screens:**
  - the edit page asks "Which dates" on a series, with what the other dates take;
  - the reschedule and cancel dialogs ask too (`SeriesScopeField`, new);
  - the toasts list the dates and who was told (`describeSeriesChange`).

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2f-2b: **658/658 on two clean passes**, the blocking watcher beside both: no
  memory-grant wait. The longest wait (about 50 s each pass) was the suite's own notice withdrawal for 124 events (lane
  6's new item). No live `CSF-` event, series row or mail-server row was left. 2f-2b has **39 assertions**:
  - **An edit takes only what changed:**
    - a new description from the second date, this and following: dates two to four take it, and the third date's
      own venue stays; nobody is told, since guests don't hear of a description;
    - a new venue on every date replaces the third's own, since this edit changed the venue; B is told once, one
      notice for the four dates and no other.
  - **Moves:**
    - an edit moving the third date a day later, to 10:00, this and following: the third and fourth move, the
      first two stay; B is told once;
    - every date a week later by reschedule, each keeping its own hours; B is told once;
    - moving both dates of another series a day earlier, where the second's own reply-by date would fall after its
      start: refused, naming the second, and the first did not move either.
  - **Cancelling this and following** from the third date: the third and fourth are cancelled, the first two go on, and
    B is told once. Cancelling again from there is refused: nothing still to come.
  - **Extending (finding 3):** a series moved a day later extends a day later; a series whose last date moved alone
    extends on the rule.
  - **Approval (UAT's real definition):**
    - the date carrying it cancelled: the approval passes to the next date waiting, and deciding that one approves
      the rest;
    - the date carrying it deleted: the same;
    - an approved series moved together: every date waits again, one approval under way on the first, deciding
      another refused naming the first, and its decision covers all.
  - **Through the sink:**
    - a series moved: one email, "Moved: … — 3 dates", a REQUEST per date with the same UIDs at SEQUENCE 1 and the
      new times, with the reason;
    - this and following cancelled: one email for two dates, a CANCEL each at SEQUENCE 2;
    - an edit of one date alone sends the single-date email.
- **Regression:** the round-4 net **209/209**; `hr-recruitment/run-round4-d` **58/58**; the templates probe **30/30**;
  `run-lane-n` **109/115**, the six section-J failures of #40.
- **`run-lane-n` repaired (harness, not ours).** It could not start: its pre-clean of the fixture tenant an earlier
  run left failed on a foreign key. The API's background sweeps walk every tenant, the fixture's too while it exists,
  and leave rows on it (69 notification topics, eight sweep run logs, a SHE monthly report). That is also why the
  2f-2a run's own tidy-up had failed. Its pre-clean and tidy-up now clear every row that points at the fixture tenant,
  in passes, before the tenant. The leftover is gone and the run removed its own.
- **The API log:** no request answered 500. Its ERR lines are the known kinds:
  - notifications with no mail server;
  - the sink's bounces and `run-lane-n`'s test send;
  - master's identity sweep;
  - payroll's foreign key on minted fixtures (#23);
  - the race proofs;
  - the procurement calendar sweep failing on the fixture tenant (`run-lane-n`'s, gone now);
  - two timeouts of the notification service's poll behind the suite's own withdrawal (lane 6's item).

  None comes from the series code.
- **After the runs:** every harness login is off (the no-email one by SQL, #42, so no 500 this time). No
  company-event notice to anyone real is live. The R4D requisition's approval notices to md.tdc,
  managing.director, hr.head and hr.officer were withdrawn twice, 0 live.

*2f-2a — what was built (2026-10-05): guests and answers across a series (D-12).*
- **No migration.** `SeriesScope` (`ThisOccurrence`, `ThisAndFollowing`, `WholeSeries`) is asked for, never stored.
- **Which dates** (`SeriesTargetsAsync`):
  - "This and following" is by date, as a calendar reads it, from the occurrence acted on;
  - a date that has started, been completed or been cancelled is never touched, and is counted as left alone;
  - "this date only", or any action on a single event, is the date alone, as before.
- **Adding a guest** with a series scope (`POST events/{id}/participants`, `scope`):
  - the guest's own checks run once: an outside guest's name and address, a leaver refused;
  - a date they are already on is passed over, not refused;
  - every date added and none left is refused with a sentence;
  - the dates awaiting approval wait for it (F-33).
- **Answering at the desk** with a scope (`participants/respond`, `scope`): the same answer on that person's
  invitations to the chosen dates still to come. A date they are not on is passed over; none at all is refused.
- **Taking a guest off** with a scope (`DELETE participants/{id}?scope=`): off every chosen date still to come that
  they are on. Each date that had invited them raises its calendar sequence and sends a cancellation.
- **Told once per guest per action (the user's ruling):**
  - several dates: one email listing them, with a calendar file per date, and one notice in the app on the first;
  - one date: the single-date email, as before;
  - a date that never invited them is not mentioned;
  - the person is matched across dates by employee, or an outside guest by address.
- **The two emails** (52 templates): `EventSeriesInvitation` and `EventSeriesChanged`. The date list is a raw
  `{{{SeriesDates}}}` built by the application, every value encoded, and declared `IsHtml` on the catalogue.
  `EventSeriesChanged` carries a title, a sentence, a reason and whether nothing is required, so 2f-2b's moved,
  changed and cancelled reuse it.
- **Two in-app topics** (15): `SeriesInvited.Guest` and `SeriesChanged.Guest`, seeded on first use as before.
- **The approval (finding 1):** a decision covering a series invites each waiting guest once for all its dates
  (`InviteWaitingAcrossAsync`). It was one invitation per date.
- **Extending (the user's ruling):**
  - the latest live occurrence's guests are put on the new dates, whatever they answered for that date, bar a
    leaver;
  - they are invited once each, listing the new dates, or with the approval when the new dates need one;
  - the answer says how many were carried, and who the invitations reached.
- **The answers** say what was done: the dates by number, those passed over, those left alone, those waiting, and
  who the one notice reached. Respond and remove now answer that instead of a message and a 204.
- **Screens:**
  - the guest dialog asks "Which dates" on a series, this date by default;
  - each guest row has "Answer for several dates…" and "Take off several dates…" (`SeriesGuestDialog`, new);
  - the toasts list the dates and the reach;
  - the extend dialog and toast say the latest date's guests are invited.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2f-2a: **619/619 on two clean passes**, the blocking watcher beside both: no
  memory-grant wait. The longest wait each pass (about 42 s) was the suite's own notice withdrawal at its clean-up,
  now a scan for 100 events, holding the notification service's old-notice clean-up behind it — the harness's cost.
  No live `CSF-` event, series row or mail-server row was left. 2f-2a has **49 assertions**:
  - **With no mail server (UAT as it is), in the app:**
    - B added to every date from the second: on all four in date order, Sent on each, one notice for the four;
    - adding B again from the third: refused, already on every date;
    - A from the third: the third and fourth, told once and reaching nobody (no login, no mail server);
    - A on every date: the two missing, the rest passed over;
    - with the first date cancelled and the second moved into the past (the run's own row, by SQL), an outside guest
      added to every date reaches the last two, two left alone;
    - B accepts every date at the desk: the two still to come, the others as they were;
    - A declines this and following, and an answer with no scope is for its own date alone;
    - B off this and following: off the last two, still on the first two, one "No longer invited … 2 dates" notice
      and no single-date one;
    - A off every date: told once, reaching nobody; an outside guest never reached is taken off and told nothing;
    - answering from a started date for following dates they are on none of: refused;
    - a scope on a single event is that event alone.
  - **Through the sink:**
    - added to three dates: one email, the series invitation, listing the three numbers, with three REQUEST files
      (each date's own UID and time), and each date Sent;
    - taken off two dates: one "No longer invited" email with two CANCEL files at SEQUENCE 1;
    - a scope reaching one date sends the single-date invitation with one file;
    - a series awaiting approval: the guest is on all three dates and waits; the approval (through UAT's real
      definition) sends one series invitation with three files;
    - extending by two from the first date carries the latest date's two guests (a decline included, not the
      first date's own guest), invites each once with a file per new date, and starts their answers afresh.
- **Suites changed:**
  - `run-final-review.mjs`: the topic count is 15 (2e-1's check);
  - `tools/mail-sink.mjs`: `calendarsOf` (every calendar file of a mail) and `htmlOf`;
  - `tools/probe-new-templates.mjs`: the two series emails, 52;
  - `hr-templates/run-lane-n.mjs`: `TOTAL` 52, and B7's ready-made-HTML list gains the two series emails'
    `SeriesDates`. Its first run failed only B7, on exactly that.
- **Regression:**
  - the round-4 net **209/209**;
  - `hr-recruitment/run-round4-d` **58/58**;
  - the templates probe **30/30**: 52 listed, both new emails at their shipped wording with described tokens, each
    previewing with no problem and no token left unfilled;
  - `run-lane-n` **109/115**, the six section-J failures of #40, as at 2e-3.
- **The API log:** no request answered 500. Its ERR lines are the known kinds:
  - notifications with no mail server to send through;
  - the sink's "bounce" addresses;
  - `run-lane-n`'s own test send through its sink;
  - master's identity sweep;
  - payroll's profile foreign key on minted fixtures (#23);
  - 2d's duplicate-key race proofs.

  None comes from the series code. Two 500s came afterwards, from switching off `run-lane-n`'s two no-email HR
  logins through `PUT /api/User` (#42); they were switched off by SQL instead.
- **After the runs:** every harness login is off (0 active); no company-event notice to anyone real is live; the R4D
  requisition's approval notices to md.tdc, managing.director, hr.head and hr.officer were withdrawn twice (#36),
  0 live. The helpdesk's SLA notices at the API's start are that module's own.

*2f-1 — what was built (2026-10-05): a recurring event is a series (D-2, D-12, C-14).*
- **No migration.** `RecurrenceSeriesId` and `OccurrenceNumber` came with lane 0, and `OriginalStartDate` with 2a.
  `RecurrencePattern` gains **`Weekdays = 7`** (the user's ruling); the column is an int, so the value needs nothing.
- **The rule** (`CompanyEventSeries`, new):
  - **Dates come from the first date and the occurrence's index,** never from the previous occurrence:
    - every day; every weekday (Monday to Friday); weekly; every two weeks;
    - monthly and quarterly by `AddMonths`, so the 31st falls on a shorter month's last day and is the 31st
      again after it;
    - yearly by `AddYears`, so 29 February falls on the 28th in a common year.
  - **`Plan` refuses, each with a sentence:**
    - no pattern;
    - both a count and an end date, or neither;
    - fewer than 2;
    - more than 52 (`MaxOccurrences`);
    - an end date before the first date, or one that leaves a single occurrence;
    - a weekday series starting at a weekend;
    - an occurrence longer than the gap between occurrences.
- **Create makes the whole series in one save.** The first occurrence takes a new `RecurrenceSeriesId` and
  `OccurrenceNumber` 1. Each further occurrence is a full event with its own number:
  - it copies everything the series shares, with its end date and reply-by date moved by the same amount;
  - it copies nothing that belongs to one meeting: no guests, register, papers, tasks, approval, outcome or stamps.

  A one-off event has its recurrence fields cleared. The answer carries `occurrenceCount`, and the toast says
  "Series scheduled — N occurrences".
- **Approved once, for the series (the user's ruling):**
  - Only the first occurrence goes to the engine.
  - Approving it approves every occurrence still awaiting approval with no approval of its own under way
    (`SharingApprovalAsync`), and sends their waiting invitations.
  - Rejecting cancels them, with their rooms, raises their calendar sequence, and tells any guest who held one.
  - Deciding an occurrence whose series' approval is under way on another one is refused, naming that one
    (`EnsureNotSharedElsewhereAsync`): "… is approved with its series: approve EVT-… (occurrence 1), and the
    decision covers this occurrence too."
  - An occurrence moved after approval is approved on its own (D-10, unchanged).
- **A day the company does not work** (`DayOffNotesAsync`): an occurrence on a public holiday (by name, the day in
  lieu said) or a company-wide non-working closure (by title) is made, never skipped. The save warns of each,
  naming its event number and date, and the occurrence's page and the series list carry the flag. A site or unit
  closure is not a company day off. The read covers one span when it is under 400 days, and goes event by event
  when it is longer (a yearly series).
- **Extending** (`POST events/{id}/series/extend`, Write):
  - from any occurrence, by a count (1–51) or to a date, one not both;
  - on the rule, counted from occurrence 1's original date, so moving the first occurrence alone does not move
    the rule;
  - numbered after the highest occurrence, deleted ones included, and copied from the latest live occurrence,
    with its edits;
  - the series' approval rule applies to what is added;
  - refused on a single event, and past 52.
- **The pattern is locked on edit:** the update carries no recurrence fields, so an edit keeps the occurrence in its
  series.
- **Screens:**
  - **The event page:** "occurrence k of n" in the header; a day-off banner; the Repeats detail; and the series
    card (`EventSeriesCard`, new):
    - every occurrence, linked, with its status and flag;
    - "Open in the register";
    - "Extend the series", with room left shown out of 52.
  - **A row saved as repeating before series existed** says so: nothing was ever made from it. UAT had two, both
    `run-slice2` residue, deleted at this proof, so none is left.
  - **The register:** "k of n" beside an occurrence, and a `?series=` filter that lists one series in order.
  - **The form:** the patterns are named ("Every weekday (Mon–Fri)"), and it checks count or end date (one,
    not both, 2–52) before the server does.
- **Reads:** the register and the single read fill `occurrenceCount` with one grouped count. The series list is a
  narrow projection. Measured on UAT after both passes: the largest ideal grant of any query on the series column
  was 5 MB, and the slowest ran in 15 ms.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2f-1: **570/570 on two clean passes**, with the blocking watcher beside both: no
  memory-grant wait. The longest stall was the suite's own clean-up: its notice withdrawal scans `Notifications` for
  85 events now, took up to 36 s, and held the dispatcher's poll behind it. That is the harness's cost, not the
  product's. 2f-1 has **54 assertions**:
  - **Nine refusals, each with its reason, and nothing written.**
  - **Weekly ×4:**
    - four occurrences a week apart, numbered 1–4, four event numbers;
    - occurrence 3 is a full event in the series, at the same time and venue, with its reply-by date moved two
      weeks;
    - four rows share the series id, and no table holds the series;
    - the register reads "3 of 4".
  - **The rule's dates:**
    - monthly from 31 August 2028: 31 Aug, 30 Sep, 31 Oct, 30 Nov;
    - yearly from 29 February 2032: then the 28th;
    - weekdays from a Thursday: Thursday, Friday, then Monday on;
    - every day from a Friday: the weekend included;
    - every two weeks until a date: the three that fall on or before it.
  - **Approval** (UAT's real definition, two HR logins):
    - every occurrence waits;
    - one approval under way, on occurrence 1 alone;
    - deciding occurrence 2 is refused, naming occurrence 1;
    - approving 1 confirms all three;
    - rejecting a two-date series cancels both, with the reason.
  - **Edit and extend:**
    - an edit keeps occurrence 4 in its series;
    - extended by 2 from occurrence 2: numbered 5 and 6, on the rule, with occurrence 4's new venue; six in all;
    - extended to a date: the two that fall on or before it;
    - past 52, both a count and a date, and a single event: all refused.
  - **Days off:** a weekly ×3 over the run's own holiday and company-wide closure is made whole. The save names
    both; each occurrence carries its own flag (none on the ordinary one), and the holiday's page says so.
  - **Clean-up, each pass:** 85/85 events (every occurrence tracked), the holiday, and 30 closures. A SQL check
    after pass 1: 0 live `CSF-` events, 0 live series rows; the 8 live `CSF-` notices are 1e's announcements to
    each run's own fixture login (switched off).
- **Residue removed:** two live `run-slice2` events from earlier runs today that died before their clean-up
  (`CS-153120`, `CS-086672`, "quarterly review (revised)"), deleted through the API. They had no live notices.
- **Regression:**
  - the round-4 net **209/209** (`run-slice0` 29, `run-slice1` 32, `run-slice2` 65, `run-slice3` 44,
    `run-round4-d` 39). `run-slice2` rose from 61: the series check, and three more deletions;
  - `hr-recruitment/run-round4-d` **58/58**.
- **The API log:** no request answered 500. The 127 ERR lines are all known:
  - 71 notifications with no mail server to send through (UAT);
  - 6 refusals from the sink's "bounce" addresses (2e-2, 2e-3);
  - 3 from master's identity sweep;
  - EF save failures from payroll's profile foreign key on minted fixtures (#23) and from 2d's duplicate-key race
    proofs.

  None comes from the series code.
- **After the runs:**
  - the 21 harness logins the net and recruitment made are switched off (0 active, all dates);
  - all 64 company-event notices the runs sent to the HR desk were withdrawn by the suites;
  - the R4D requisition's approval notices to md.tdc, managing.director, hr.head and hr.officer were withdrawn,
    twice, 10 s apart (#36). 0 live.
- **`run-slice2.mjs`** made a quarterly ×4 and deleted only the first. It now asserts the series is made whole and
  deletes all four.

*2e-3 — what was built (2026-10-05): calendar files (D-14) and the overdue chase (F-34).*
- **The migration** `20261005143701_CompanyScheduleInvitesAndTaskChase`, scaffolded by the user and rewritten as
  guarded SQL (the Designer and the six-line snapshot change kept):
  - `CompanyEvents.CalendarSequence` (int, default 0, named constraint);
  - `EventTasks.OverdueChasedAt` (datetime2 NULL).

  There is no backfill: a task already overdue is chased once (the user's ruling). There is no THROW, so it needs no
  preflight pin. Proved on a throwaway database (8/8: Up twice, the default on existing and new rows, Down twice, Up
  again), then applied to UAT: a COPY_ONLY backup `ErpSystemDB_UAT_before_cs2e3.bak` first, then the built DLL's
  `apply-migrations`. History 143 → 144; both columns are present; all 604 events read 0.
- **One calendar builder, `HrCalendarFile`**, for company events and interviews:
  - a stable UID: `company-event-<id>` / `interview-<id>` `@rhema-erp`;
  - the caller's SEQUENCE, a DTSTAMP, METHOD REQUEST or CANCEL, and STATUS CONFIRMED or CANCELLED;
  - times in UTC, or whole days with an exclusive end;
  - ORGANIZER and ATTENDEE with a quoted CN; **the recipient alone as attendee**;
  - text escaped, lines CRLF and folded at 75 octets (never inside a character).

  It is sent as an attachment typed `text/calendar; method=…; charset=UTF-8`, the only way the platform's mailer
  sends. `SendForTenantAsync` gains an overload with attachments; no caller changes.
- **Which emails carry it** (the user's ruling):
  - invitation (on add, on approval, on a corrected address, on a re-send): REQUEST;
  - moved: REQUEST;
  - a new venue, site or link: REQUEST;
  - postponed: CANCEL;
  - cancelled, and not approved: CANCEL;
  - taken off the list: CANCEL to that guest alone.

  The sequence rises before each change's save: a move, a new venue or link, a postponement, a cancellation, a
  rejection, and the removal of a guest who was invited. **A postponed event holds no entry:** an invitation to it,
  or a new venue for it, sends the email without a calendar file, and the move to a new date sends the entry again.
  Reminders, chases, the approval email and task emails carry none.
- **The organiser on the file** is the organiser's own address, or the mail server's sending address when they have
  none, so an outside guest's Accept or Decline reaches someone (F-36: HR records it at the desk).
- **Interviews** use the same builder, with one UID per interview: a reschedule now replaces the calendar entry
  instead of adding a second. SEQUENCE stays 0 (no counter, the user's ruling), so the newer DTSTAMP wins. Times are
  in UTC, where they used to be floating. There is still no ORGANIZER: an interview names none.
- **The overdue chase (F-34):**
  - The hourly sweep (and HR's run-now) chases each open task past its due date. The rule is `IsOverdue`'s: due
    before today, not completed or cancelled.
  - It skips a cancelled or deleted event's tasks and a leaver's, who would never be reached and would be tried
    every hour.
  - It goes to the assignee only (the user's ruling), by a new email, `EventTaskOverdue`, and in the app, on the
    13th topic, `CompanySchedule.TaskOverdue.Assignee`.
  - It is stamped only when it reached them (2e-2's rule), so it is sent once. One that reached nobody stays due.
  - A new due date or a new assignee clears the stamp.
  - The run reports `tasksChased` and `tasksLeftDue`, apart from the events' people counts.
  - The Tasks tab shows "assignee chased" beside Overdue.
- **The templates register** (§ 2.7) lists 50, with the new email's row; `run-lane-n` expects 50. The event page's
  card says that invitations carry a calendar entry and which changes send the update.
- **Not done here:** deleting an event (Admin) still tells nobody, so it sends no CANCEL. Interview cancellations send
  no CANCEL either, as before; neither was in the rulings.
- **Found by the proof, fixed — the event page's detail read (`GetDetailByIdAsync`):**
  - **What failed:** the first two passes after the migration stopped in block 2a with a 500 on
    `GET events/{id}/details`, after 30 s.
  - **Measured:** a watcher (a DMV query every 2 s) caught the read waiting on `RESOURCE_SEMAPHORE`, a memory-grant
    queue, with no blocker. The statement EF sent, re-run with `OPTION (RECOMPILE)` and an actual plan, asked for
    **387 MB** of working memory for one Sort: the cap; its estimate was 61 PB, from the four collections' joined
    rows, each carrying a whole `Employee`. It **waited 29.4 s** for the grant on an idle server, and used **0 KB**.
  - **Why now:** UAT's SQL Server has 2 GB. Before the migration a cached plan, shrunk by memory-grant feedback, asked
    1.5 MB. The migration changed `CompanyEvents`, the plan recompiled, and a first run that never finishes never
    teaches the server to ask for less. So every detail read failed, and would after any deploy on a small server.
  - **Fixed:** the event is read with its single-row references, then each collection (guests, the register,
    attachments, tasks) as its own seek on its event index, with no sort; the context attaches them. The same
    family as the memory note on single-query grants.
  - **The suite's clean-up** died on pass two, when its notice withdrawal was a deadlock victim (1205) against the
    notification dispatcher. It now retries and never throws. What that pass left was removed by a new tool,
    `tools/cleanup-stamp.mjs` (stamp `VE32BU`).

*Proof (UAT, API in Staging, the build with the detail-read fix):*
- **The fix measured first:** on its first compile, each statement of the new detail read asks for **0 KB** of
  working memory and runs in 1–2 ms. The demo board meeting's page read took 950 ms, the first call of a fresh API.
- `run-final-review.mjs` blocks 1a–2e-3: **516/516 on two clean passes**, with the blocking watcher beside both: no
  memory-grant wait. 2e-3 has 40 assertions:
  - **The chase, with no mail server:**
    - the sweep chases B's overdue task (a login) and stamps it;
    - it leaves A's due (no login, no mail server);
    - it never touches a task not yet due, a completed one, or one on a cancelled event;
    - B is told in the app once, on the 13th topic, email off;
    - a second sweep does not chase again;
    - a new due date clears the stamp and the task is chased once more;
    - a task passed to B is chased for B.
  - **Through the sink, every email's calendar file read back:**
    - an invitation: `text/calendar; method=REQUEST; charset=UTF-8`, the event's UID, SEQUENCE 0, CONFIRMED, UTC
      times, the organiser by name and address, the recipient alone as attendee (asked to answer, required), the
      description escaped and whole once unfolded (non-ASCII included), the venue as location;
    - moved: REQUEST, the same UID, SEQUENCE 1. A new venue: SEQUENCE 2. Taken off the list: CANCEL to that guest
      alone, SEQUENCE 3, not asked to answer, and nobody else sent one. Postponed: CANCEL, 4;
    - a guest invited while postponed, and a new venue while postponed, get the email with no calendar file;
    - moved to a new date: REQUEST at 6, to both guests;
    - a reminder carries none;
    - cancelled: CANCEL at 7, and the stored sequence is 7;
    - an all-day event as whole dates, the end exclusive;
    - an organiser with no address: the mail server's sending address stands in;
    - every line within 75 octets, every line CRLF;
    - an overdue task's assignee with no login is chased by email once a mail server takes it, the email carrying
      no calendar file.
- **Interviews** (`tools/probe-interview-ics.mjs`, **10/10**): the recruitment round4-d suite runs through the sink.
  On its interview with a candidate, as its own HR officer, the probe then sends the invitation, moves it to a free
  afternoon, and notifies the panel.
  - Three interview calendar files are caught: REQUEST, SEQUENCE 0, UTC, the recipient alone as attendee.
  - Each UID is `interview-<id>@rhema-erp` and names a real interview.
  - **The candidate's invitation and the move carry one UID** (26 Oct 09:00Z → 11 Nov 14:00Z): one calendar entry,
    updated, where a random UID made a second.
  - round4-d alone sends no interview invitation; the probe's first cut, which relied on it, failed for that reason
    and was extended.
- Regression:
  - the round-4 net **205/205** (29 · 32 · 61 · 44 · 39);
  - `hr-recruitment/run-round4-d` **58/58** (and 58/58 twice more inside the probe);
  - `tools/probe-new-templates.mjs` **24/24**: the six newer emails listed, described, shipped, previewed. 50 listed;
  - `hr-templates/run-lane-n` **109/115**: section A checks the register against the screen both ways at 50; the six
    failures are section J, cross-module #40, as at 2e-1. UAT has no SMS credentials, checked before the run (#41).
- **On UAT's demo data, as the user ruled:** the first sweep chased scenario 110's board-pack task (TDC/00001) once.
  `md.tdc` holds that one notice; the task is stamped.
- API log (the session after the fix):
  - no request answered 500;
  - #23 (45 fixture hires), #39 twice;
  - eleven F-45 race refusals, each answered 422;
  - 316 notification-dispatcher emails refused (no SMTP);
  - #41's SMS attempts, all refused (no provider enabled);
  - the sinks' deliberate refusals, now including invitations carrying a calendar file;
  - `run-lane-n`'s deliberate tarpit;
  - no company-schedule notice failed to be raised.
- **Clean-up:**
  - nothing of the runs left: events, closures, employees, mail settings, template rows;
  - 38 harness logins switched off, one by SQL (#42);
  - every notice the runs raised is withdrawn and re-checked after the API stopped, including the recruitment
    fixtures' requisition approvals to real staff (three runs, plus four rows of an older session's).
  - The watcher's long waits were the suite's own notice withdrawal: a scan of `Notifications`, which holds back the
    dispatcher's read for up to 20 s. That is the pairing behind pass two's deadlock; it retries now.
- The screens type-check and lint clean. They are not yet walked in a browser (lane 5).

*2e-2 — what was built (2026-10-05): delivered vs issued (R4-6.3, F-32).*
- **Four rulings by the user before the build** (2026-10-05, all as recommended):
  - a notice in the app counts as delivered, beside an email the mail server took;
  - an invitation that reached nobody is sent again by a button, not by the sweep;
  - a reminder or chase that reached some is stamped, and those not reached are not retried;
  - no stored counts: the toasts carry the numbers, so there is no migration in 2e-2.
- **Counted from the result.** The email's own result (`SendForTenantAsync`'s bool, within the 10 s wait) is read at
  last. `CompanyScheduleNotices.TellAsync` answers whom it reached: the employees with an active login it was raised
  to. Every notice now answers `CompanyEventNoticeResultDto`:
  - `issued` (the people it was for) and `reached` (email taken, or in the app), with `notReached`;
  - `emailed`, `emailsNotTaken` and `toldInApp`;
  - `mailServerSetUp` (read untracked by the tenant named, never through the sender's lookup, #40), and `stamped`.
  The counts are of people: a person reached both ways counts once.
- **Where it answers:**
  - "Send reminder now" and "Chase unanswered now" answer it in place of `{ sent }`, which counted every address
    tried;
  - cancel, reschedule and reject carry it as `told`;
  - an edit carries it as `told` when its notice (a move, a postponement, a new venue or link) went out;
  - the sweep's run gains `peopleIssued`, `peopleReached`, `emailsNotTaken`, `toldInApp`, `remindersLeftDue` and
    `chasesLeftDue`. `emailsSent` now counts emails taken.
- **An invitation is Sent only once it reached its guest.** It was Sent on insert, before anything was sent, and
  the approval marked the waiting ones Sent before sending. Now it is not sent, and undated, until the email is
  taken or the in-app notice is raised. It is saved guest by guest.
  - Whoever invites themselves counts as reached: they know, though nobody is told of their own act.
  - A guest who reached nobody reads **Not delivered** on the guest list. "Waits for approval" is kept for an
    event that still awaits approval.
  - A change (moved, postponed, changed, cancelled, uninvited) still goes only to the guests who were invited. A
    guest the invitation never reached never heard of the event.
- **Sending them again:** `POST events/{id}/invitations/send` (Write), the page's "Send the undelivered
  invitations (n)". It is refused for:
  - a closed event;
  - one awaiting approval (its approval sends them);
  - one that has begun (`CompanyEventRules.RefuseInviting`);
  - one whose invitations all reached their guests ("nothing to send").

  A guest who has since left is skipped (F-35).
- **A small F-33 gap closed on the way:** correcting an outside guest's address re-sent the invitation even while
  the event awaited approval. It now waits for the approval like every other.
- **A reminder or chase is stamped only once it reached somebody** (`StampIfReachedAsync`). One that reached
  nobody stays due: the button may be pressed again, and every pass of the hourly sweep tries it until the event
  begins or the reply-by date passes. It never repeats an in-app notice, since one that reached nobody raised none.
  An event with nobody to tell is neither stamped nor reported, so a guest added later is still reminded.
  ⚠ Under **#40** the hourly host's emails find no mail server even where one is set up, so on such a server only
  the in-app notices reach anybody until Platform fixes it. The sweep says so in its counts.
- **The event page** ("Invitations and reminders", lane 5's R4-6.3 card, built here since it reads these fields):
  - invitations: "n of m delivered — k not delivered", or "k wait for the approval";
  - the reminder and the chase: Sent / Chased on a date, **"Due since … — it has reached nobody yet. The hourly
    sweep tries again."**, or "Goes on …". The detail read carries the sweep's two days (`reminderDueOn`,
    `rsvpChaseDueOn`) so the page uses the sweep's rule, not its own;
  - an amber line when no mail server is set up, saying who is still reached (employees with a login, in the app);
  - the re-send button beside the reminder and chase buttons.
- **The toasts** say "3 of 4 reached — 0 by email, 3 in the app. 1 not reached: no mail server is set up, and they
  have no login to be told in the app." (`noticeReach.ts`). A reminder or chase that reached nobody toasts **not
  delivered**, and says it stays due.
  - The edit page's "Everybody invited is told", and the reschedule toast's, are replaced by the count.
  - Adding or correcting a guest the invitation did not reach says why.
- **The approval email to the organiser** says how many waiting invitations reached their guests, not how many
  were tried.
- Not counted, since no screen shows them: telling a removed guest and a task's assignee. Their sends return the
  email's result like every other; nothing reads it.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2e-2: **476/476 on two clean passes**. 2e-2 has 46 assertions. Its first half
  runs on UAT as it is (no mail server); its second through the run's own SMTP sink (`tools/mail-sink.mjs`, which
  refuses any "bounce" address), an `EmailSettings` row for a few minutes, deleted in a `finally`:
  - **With no mail server:**
    - a guest with a login is Sent and dated (the app reached them); one without a login, and an outside guest,
      are not sent and undated (both read Sent before);
    - the page is told there is no mail server, and the sweep's two days;
    - a reminder counts people: 4 issued, 1 reached, 4 emails not taken, stamped. A chase: 3 issued, 1 reached;
    - a reminder and a chase that reached nobody are **not stamped**, so both stay due;
    - the undelivered invitations sent again reach nobody, and stay not sent;
    - the re-send is refused with nothing to send, while awaiting approval, once begun, and when cancelled;
    - a move, an edit (a new venue) and a cancellation each answer `told`: 1 issued, 1 reached. That is the guest
      the invitation reached, not the two it never did; an edit that told nobody carries none;
    - **the sweep** (run-now, after checking that nothing else on UAT was due) stamps the reminder that reached B
      and leaves the one that reached nobody due: 5 issued, 1 reached, 0 emails taken, 5 not, 1 in the app.
  - **Through the sink:**
    - the page sees the mail server;
    - the undelivered invitations sent again are both taken, Sent and dated; the sink holds one for each;
    - an address the sink refuses stays not sent. Corrected, it is sent again, Sent and dated, to the new address;
    - the reminder left due now goes to all four by email, and is stamped;
    - a reminder that reached some (B by email and in the app, C by email, not the refused address) is stamped:
      3 issued, 2 reached.
  - UAT is back to no mail server after each pass; the orientation outbox was checked empty before the sink opened,
    and no orientation row changed in the window.
- **Suites changed for 2e-2:**
  - 2d: the reminder's counts read `issued`. A corrected outside address is still not delivered with no mail server
    (2e-2 proves the delivery through the sink);
  - 2e-1: the reminder and chase read issued and reached. Whoever invites themselves is Sent. The approval sends
    the waiting invitations: H, who has a login, is reached; A, who has none, is not delivered (both read Sent);
  - `run-round4-d` reads issued and reached in place of `sent`;
  - `run-slice2`: a comment only.
- Regression:
  - the round-4 net **205/205** (`run-slice0` 29, `run-slice1` 32, `run-slice2` 61, `run-slice3` 44,
    `run-round4-d` 39);
  - `hr-recruitment/run-round4-d` **58/58**;
  - `hr-templates/run-lane-nb2` **36/36**: the sweep through its own sink (reminded and chased once, the lead, a
    moved date, approval, the tenant's wording). It had not been run since round 4. Its D1 now reads issued, reached
    and emailed, 2/2/2, since 2e-1 reminds the organiser too.
- **Found on the way, not ours — a security defect, proved, and registered as cross-module #43 on the user's word
  (2026-10-05, at 2e-3's source check):**
  - **The SMTP password is written back to the database in plain text.**
    - `SettingsService.GetEmailSettingsAsync` loads the mail-settings row tracked and writes the decrypted
      password into it.
    - The mail sender calls it on every send, in the request's own unit of work, so any save later in that
      request stores the password in plain text.
    - Every company-schedule notice saves after its emails, and so does almost every other sender.
    - `tools/probe-smtp-password-write.mjs` saved a dummy password through the settings endpoint (stored
      encrypted, 64 characters). It then pressed "Send reminder now" as an HR officer (a server on a closed port,
      so no email). Read again, the stored value was **the password in plain text**.
  - **The same probe found two related facets:**
    - the settings read (`GET /api/Settings/email`, TenantAdmin) answers the password in plain text;
    - saving mail settings writes the password in plain text into `AuditLogs` (`NewValues`, and `OldValues` on an
      update).
  - `AuditLogs` is append-only (trigger `TR_AuditLogs_AppendOnly`), so the probe's one audit row, with its dummy
    password for a server that does not exist, **stays on UAT**. Everything else the probe made is removed: the
    settings row, the event, its notice, the login (switched off) and the employee.
  - Under #40 the hourly sweeps load no settings, so today only signed-in sends write it back.
- API log:
  - no request answered 500;
  - #23 (payroll profile at each fixture hire), #39 once;
  - nine F-45 race refusals, each answered 422;
  - the sink's seven deliberate refusals (six bounce addresses, the probe's closed port);
  - 113 notification-dispatcher emails refused (UAT has no SMTP);
  - the shared notification service's bulk clean-up of old notices failed twice, as at 2d.
  - No company-schedule notice failed to be raised.
- **Clean-up:**
  - nothing of the runs left: events, bookings, rooms, mail settings, template rows;
  - 24 harness logins switched off (the slice suites', recruitment's and N-b2's);
  - every notice the runs raised is withdrawn, re-checked after the API stopped. That includes N-b2's approval
    request, which went to every HR-role login.
  - **Also withdrawn, from earlier sessions today:** the recruitment suites' "R4D Clash" requisition approvals
    and an "E2E RecD" offer approval, live on `md.tdc`, `managing.director`, `hr.head` and `hr.officer`
    (86 + 43 + 32 + 7 rows across all recipients).
  - What real staff still have live from today is not harness: the demo fire drill's reminder, the leave-planning
    reminder, and the overdue demo staff movements.
- The screens type-check (scoped `tsconfig.company-schedule.json`) and lint clean. They are not yet walked in a
  browser (lane 5).

*2e-1 — what was built (2026-10-05): who is told.*
- **`CompanyScheduleNotices`, travel's model (§ 1c):**
  - One topic per notice and audience, `CompanySchedule.{Notice}.{Guest|Organiser|Assignee}`: 12 in all.
  - The topics are seeded the first time a notice is raised, so a tenant needs no seed run. UAT has
    them now.
  - **In the app only.** Email stays on the templated catalogue, which has a delivery result, the
    tenant's own wording, and (2e-3) room for the calendar invite.
  - Recipients are the active logins linked to the employees told. An employee with no login is told by
    email alone.
  - **Nobody is told of their own act**, in the app or by email. A reminder and a chase are the
    exceptions: they are about the date, so the sender is reminded too.
  - **Links:** a guest's opens My Schedule at the event's first day (`?from=`, which the page now
    reads), since every employee can open it. An organiser's or an assignee's opens the event page.
  - It never fails the act: it is raised after the save, and logged if it cannot be.
- **What is told, by email and in the app:**
  - an invitation; the RSVP chase; the reminder;
  - moved, cancelled;
  - **new:** postponed; a new venue, site or joining link without a new time; taken off the guest list;
    a task given (added, or passed to someone new);
  - **new, to the organiser:** approved; not approved, with why. The organiser is also **reminded**
    (D-11), on the guest list or not, and only once.
  - A change goes only to guests who were invited. Someone still waiting for the approval never heard
    of the event and is not told.
- **Five new catalogue emails:** `EventApproved`, `EventPostponed`, `EventChanged`,
  `EventGuestRemoved`, `EventTaskAssigned`. Each has its § 2.7 row in the configuration register, which
  now counts 49 templates. `hr-templates/run-lane-n.mjs` expects 49.
- **F-33:**
  - An event awaiting approval invites nobody; its guests wait as "Waits for approval".
  - The final approval sends every waiting invitation and tells the organiser.
  - **The buttons refuse what the hourly sweep refuses**, by one rule both read
    (`CompanyEventRules.RefuseReminding` / `RefuseChasing`):
    - cancelled or completed;
    - postponed;
    - awaiting approval;
    - begun;
    - for a chase, also an event that asks for no replies, or whose reply-by date has passed.

    The buttons used to refuse only a cancelled event. The page shows the buttons only where they
    apply.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2e-1: **429/429 on two clean passes** (passes one and three). 2e-1
  has 42 assertions, each an exact count of rows in `Notifications` for the recipient's login:
  - invited, with My Schedule's link at the event's day;
  - whoever invites themselves is not told;
  - the 12 topics, in the app only;
  - reminded and chased (the sender included), the organiser reminded as organiser, with the event
    page's link;
  - each F-33 refusal;
  - an approval sending the waiting invitations and telling the organiser;
  - a rejection telling the organiser, and not a guest never invited;
  - a venue and a link changed, an edit that changes neither telling nobody, postponed, moved,
    cancelled, uninvited (and not one never invited);
  - a task told, one given to oneself not told, a reassignment told again, an edit keeping the
    assignee not told.
- **Pass two** passed every block assertion. Its clean-up left 7 notices live, for 2e-1's
  approval-gated event, `hr.head` and `hr.officer` among them. They were soft-deleted, then saved
  back live at 10:54:49 by the dispatcher, which was retrying their emails at that moment (#36).
  - Withdrawn by hand, with 36 more left by earlier sessions on the run's deleted events, which the
    old link-only clean-up had never matched: 43 in all.
  - The clean-up now withdraws twice, 10 s apart, by the event's id as well as the link (in
    `run-slice0` too).
  - After the runs: 0 live on any run event; `hr.head` and `hr.officer` 0 live (38 withdrawn each).
- Regression:
  - the round-4 net **205/205**. `run-round4-d`'s two reminder counts each rose by one (its organiser
    is now reminded). Its chase ran on an event that asked for no replies, which F-33 now refuses, so
    that event now asks for replies;
  - `hr-recruitment/run-round4-d` **58/58**.
- **The Letter & Email Templates screen** (Administration → HR Settings), asked by the user:
  - `hr-templates/run-lane-n.mjs` **109/115**. A1 (49 listed), A6–A8 (the register against the screen,
    both ways), B (every template's tokens), C (every shipped default passes the save checks) and D–I,
    K, L passed.
  - **The 6 failures are all section J,** the anonymous careers registration's activation email: "No
    email settings configured" while the suite's mail sink was configured. 2e-1 does not touch that
    road. The suite scored 115/115 at round 4, and master has changed the sign-up since (three
    `AuthController` commits). Not fixed here.
  - **`tools/probe-new-templates.mjs` 21/21** on the five new templates by name: listed, named and
    described, shipped wording, described tokens, a preview with every token filled; one saved as the
    tenant's own, shown so in the list, test-sent ("no mail server"), reset to the shipped wording;
    its row then deleted outright.
- **Found on the way, not ours** — logged in the cross-module register as **#40–#42** (the user's word, 2026-10-05):
  - **#40, section J's cause:** since master `de8ad4fb2` (2026-10-03) the mail-settings lookup takes the tenant from the
    signed-in user, so an email sent with nobody signed in finds no mail server. The tenant `SendForTenantAsync` names
    chooses the wording only. ⚠ This includes **every HR background send, the hourly company-schedule reminders and
    chases among them**, on a server that has SMTP (read from the code; UAT has none).
  - **#41, the new sign-up texts a phone code.** `run-lane-n` registers candidates with random real-format
    Ghana numbers, and the API tried mNotify for each; all four failed (no credentials on UAT), so
    nothing was sent. With working credentials it would text strangers.
  - **#42, `PUT /api/User` answers 500 for a login with no email** ("Email '' is invalid"), so such a
    login cannot be switched off through the API. `run-lane-n`'s own no-email officer was switched off
    by SQL.
- API log:
  - three 500s, all that user update;
  - #23, #39;
  - the F-45 race refusals;
  - the mail sink's deliberate refusals;
  - UAT's missing SMTP settings.
  - No company-schedule notice failed to be raised.
- Clean-up: 31 harness logins switched off (one by SQL, above). UAT's `EmailSettings` is back to none,
  and no `EmailTemplates` row is left for CompanySchedule.
- The screens type-check and lint clean. They are not yet walked in a browser (lane 5).

*2d — what was built (2026-10-05): guests, the register and tasks.*
- **Guests:**
  - An outside guest needs a name and an email address; the invitation goes to the address.
  - A guest is an employee or someone from outside, never both.
  - A new employee guest must be this tenant's and still employed (F-10, F-35): a 422 naming them.
  - Nobody is invited twice, by employee or by address (in any letter case).
  - Two requests inviting the same person at once both pass the check; F-45's unique index refuses the
    second, and the service answers that as the same 422, never a 500.
  - **Answers (F-11):** only accepted, declined or tentative, and only for a guest of the event in the
    route (another event's route is a 404).
  - **Correcting a guest (C-22, `PUT participants/{id}`, Write):** role, required, special
    requirements, and an outside guest's name, address and organisation. An employee guest stays who
    they are. A corrected address is sent the invitation, since the first went nowhere.
  - **Uninviting is on Write** (it needed Admin). Someone uninvited can be invited again.
  - **A cancelled or completed event's guest list is its record:** no invitation, answer, correction
    or uninviting.
  - **F-35, the sends:** the shared send loop (reminders, the RSVP chase, rescheduled, cancelled) and
    the invitation both skip a leaver.
- **The register:**
  - It is taken once the event has started, and never for a cancelled event.
  - **F-1:** marking someone again, which is how the register is corrected, keeps the check-in unless a
    new one is given. It used to stamp the moment of the correction, for an absence too. An absence now
    carries no check-in or check-out.
  - A check-in must fall on one of the event's days and not be still to come.
  - Check-out is refused without a check-in, refused twice, and refused while the check-in is still to
    come.
  - The person must be this tenant's, but may since have left: they still attended.
  - **Removing a row (C-21, `DELETE events/{eventId}/attendance/{attendanceId}`, Write)** is a
    correction, through the row's own event only.
  - Simultaneous marks of one person: one row; the index's refusal is answered as a 422.
- **Tasks:**
  - **Overdue is worked out on read** (`isOverdue`: due before today and still open) and is refused as
    a status to set. Nothing set the stored Overdue, and the repository's query read the server's
    local date.
  - **F-12:** completed through the edit, a task gets its completion date; taken back out of
    Completed, it loses the date and the notes.
  - Complete is refused twice, and refused on a cancelled task.
  - A new assignee must be this tenant's and still employed. An assignee who has since left stays on a
    task already theirs.
  - **F-46:** the edit's answer is re-read, so a reassigned task names the new assignee, not the old.
- **Reads (F-30):** guests, the register and tasks are read on the service's own tenant-scoped queries.
  Their repositories' custom reads are removed (none filtered by tenant; this service was the only
  caller).
- **The screens:**
  - the guest tab edits guests (the employee locked) and freezes on a closed event;
  - the register tab marks once the event has started, edits a row by marking it again, removes rows,
    and offers Check out only where it applies;
  - the task tab shows Overdue beside the status and no longer offers it as a status.
- **Left for 2e (notices):** telling a removed guest, and telling and chasing a task's assignee (F-34).

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2d: **387/387 on two clean passes** (passes two and three). 2d has
  86 assertions:
  - each guest refusal, and that the refusals wrote nothing;
  - a leaver refused, then left out of a reminder (three guests reminded, two with C made a leaver);
  - four simultaneous invitations of one person: each 201 or 422, one row;
  - answers, corrections (a corrected address re-invited), uninviting on Write, re-inviting;
  - a cancelled event's list frozen;
  - the register before the start and on a cancelled event refused;
  - check-in times refused, F-1 (the check-in kept, the same row), check-out rules, a leaver on the
    register;
  - a row removed through its own event only, then marked afresh; four simultaneous marks: one row;
  - tasks: assignee checks, overdue on read, Overdue refused, the completion date stamped and cleared,
    the new assignee named, complete twice and on a cancelled task, a leaver kept on their task.
  - In the API log, the F-45 index refused simultaneous rows 14 times on each table across the three
    passes, every one answered 422.
- **Pass one** passed all 386 block assertions. Its clean-up failed one check: its read of published
  announcements timed out (30 s, a 500), so its two announcements stayed live until pass two swept
  them. The database stalled from about 09:36:12 to 09:36:58: the shared notification service's bulk
  clean-up of old notices (`UnifiedNotificationService.cs:1787`) failed twice just before, and its
  dispatcher's own read timed out in the same window. No company-schedule code was involved; the same
  read answered in 23–46 ms afterwards. Recorded here, not as a defect, unless it recurs.
- Regression:
  - the round-4 net, **205/205**. `run-slice2` is 61: it marked a register a fortnight before its
    event with a check-in still to come, then checked it out, which is how UAT got its **12 register
    rows checked out before they checked in** (round-4 residue, R4-2.1; left as they are). It now
    asserts the refusal;
  - `hr-recruitment/run-round4-d` (guests and answers on the clash check), **58/58**.
- `hr-templates/run-lane-nb2` also invites guests and records answers. Its payloads were read against
  the new rules and fit them; it was not run, since it stands up a mail sink.
- API log: the one 500 above; otherwise #23, #39 and UAT's missing SMTP settings.
- Clean-up: nothing of the runs left live (events, announcements). 21 harness logins switched off.
  `hr.head` and `hr.officer` have 0 live event notices (29 withdrawn each).
- The screens type-check and lint clean. They are not yet walked in a browser (lane 5).

*2c — what was built (2026-10-05): who an event is for (D-16), the diaries and the intranet.*
- **One audience rule per event (`CompanyEventRules.AudienceRuleOf`).** The visibility can only narrow
  the audience; the scope sets how wide it is:
  - Private or Confidential: the guests and the organiser only;
  - a Department visibility: the unit and everything beneath it, whatever the scope. A Management
    visibility: management, whatever the scope;
  - Public follows the scope: everyone, the unit, or management. "Selected" and "External only" are
    their guest lists.

  The event is on the company calendar and in people's diaries only when `ShowOnCompanyCalendar` is
  on (`CalendarAudienceOf`). Lane 7 reads the same rule.
- **Management (D-16) is a rule in the audience resolver:**
  - It is a new rule type, `Management` (7): every active employee who heads a live unit, or who is
    the line manager of an active employee.
  - Orientation used to compute the same two sets in its own service, without dropping people who had
    left. It now reads this rule, so "Management" means one thing across HR.
  - Like Everyone, a Management rule names no record (`HrAudienceTargets.NeedsTarget`). The
    announcement service's three Everyone checks now go through that method.
  - No schema change: the type is stored as an int.
- **The resolver can answer for a few people** (`IncludedAmongForTenantAsync`): which of the given
  employees a set of rules includes. The narrowing happens in the query, so a diary of three people
  does not load the whole company.
- **The diary and clash source:**
  - An event on the company calendar is in the diary of everyone it is for. It is soft: it never
    blocks.
  - The guests and the organiser are entered as 2a left them.
  - An event that is off the calendar, or private, reaches only its guests and organiser.
- **The reach before the save:**
  - `GET events/audience-preview?scope&visibility&organizationUnitId` (read tier) counts the active
    staff a choice reaches.
  - The form shows "For: … — n active staff", and warns when that is nobody (management with no unit
    heads or line managers named, or an empty unit).
  - The save does not refuse such an event. Create and update answer with `warnings`, which the
    toasts show.
  - The event carries `audienceDescription`, which the event page shows as "For".
- **`ShowOnIntranet`, on HR's click (L1-1's rule):** the switch marks an event to be announced.
  Nothing is sent on save.
  - `GET events/{id}/announcement` previews the words and the reach.
  - `POST events/{id}/announce` (write tier; the caller's employee record is the publisher) publishes
    an `HrAnnouncement` in the Event category, to the event's own audience rule. It stays up until the
    day after the event ends.
  - The announcement is refused (422, saying why) when the event is not marked, is closed or over, is
    still awaiting approval, is for its guests only, or reaches nobody.
  - The words are the server's: when, where, what, and who organises it. They never include the
    meeting password.
  - The event page's "Announce on the intranet" button opens a dialog. It shows the preview as text,
    and the count on its button.
- **The form says what each control does:** Audience, Visibility, and the two Show switches. Priority
  says it is "a label for HR's own sorting; it changes nothing about the event".

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2c: **301/301 on two clean passes**. 2c has 31 assertions:
  - **Reach, against SQL oracles:**
    - everyone;
    - a unit's subtree, named;
    - management (orientation's oracle);
    - a Management visibility narrowing a public all-staff event;
    - private and "selected guests" events reaching their guest lists only.
  - **An empty unit** reaches 0, and the warning comes both before the save and on it. The save is not
    refused.
  - **The diaries:**
    - an all-staff event is soft in the diary of someone not invited, but absent when it is off the
      calendar or private;
    - a unit event is in B's diary and not C's;
    - a management event is in C's diary and not B's. For the run, C is made B's line manager by SQL
      and then unmade.
  - **The intranet:**
    - "not marked", "guests only" and "awaiting approval" are each refused with the reason, both in
      the preview and as a 422;
    - the preview reaches the run's own one-person unit and is worded from the event;
    - the announcement is published as an Event to that one person, addressed by the unit, shown
      until the day after the event, and is in that person's own announcements.
- Regression:
  - the round-4 net, **208/208**;
  - `hr-orientation/run-round4-i`, **184/184**. Orientation's Management population now reads the
    resolver, and A14 checks it against its SQL oracle.
- **No message reached real staff:**
  - each run's one announcement went to a unit the run made, with one person in it, and both were
    archived;
  - the approval notices to `hr.head` and `hr.officer` were withdrawn: 38 each today, 0 live after the
    API stopped.
- API log: no request answered 500. The errors are:
  - #23, including a salary-basis insert at a hire;
  - #39, two reconciliation failures;
  - 108 email sends refused, because UAT has no SMTP settings.
- Clean-up: nothing of the run's is left. The review suite switches off its own logins; 21 more (from
  round-4, recruitment and orientation) were switched off.
- The screens type-check and lint clean. They are not yet walked in a browser (lane 5).

*2b — what was built (2026-10-05): approval on the workflow engine (D-10).*
- **The HR recipe, applied a ninth time:**
  - a status adapter (`CompanyEventWorkflowStatusAdapter`, `HrCompanyScheduleWorkflowStatusAdapters.cs`):
    - Approved → Confirmed, or stays Postponed;
    - Pending or Recalled → awaiting approval (confirmed back to scheduled). Recall is written out,
      because the default sets a "Draft" the enum lacks;
    - Rejected → cancelled, "Not approved: reason".
  - the entity type `CompanyEvent` in the catalogue (no other module uses the key);
  - an inbox title "name on date" linking to the event page;
  - routing fields (category, type, scope, budget, attendees, unit, organiser) for when conditional
    routing works (#3);
  - `CompanyEvent` in the frontend's workflow type list.
- **The seeded definition:** `COMPANY_EVENT`, one approval step for the HR desk (HR, TenantAdmin as
  backstop), with the creator barred (`preventInitiatorApproval`). It reaches a database through the
  seeders, not an API start.
- **The service:**
  - an event that needs approval starts its approval when it is created (no draft). With no published
    definition it waits and is never auto-approved (`HrWorkflowFallbackAuthority`);
  - Approve keeps 2a's record rules, then the engine decides. With no approval under way (no
    definition, an older event, or a start that failed) `HR.Company.Approve` decides, so the event is
    never stuck;
  - **Reject** (`POST events/{id}/reject`, reason required) cancels the event, its room bookings with
    it, and tells everybody invited. The event has no other state for "not going ahead". **Confirmed
    by the user on 2026-10-05** ("yes, keep that").
  - Cancel and delete withdraw an approval still under way; a move of an approved event starts a
    fresh one.
  - `HR.Company.Approve`'s description now names events.
- **The event page:** the shared approval actions (who it waits for, Approve, Reject with a reason;
  Recall off) and a Workflow tab, in place of the bespoke Approve button.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2b: **275/275 on two clean passes**; 2b has 25 assertions:
  - **no definition:** the event waits (not approved on creation), and the approve tier approves it;
  - **a definition naming one approver** (a second HR login, linked to B, so the engine's notices
    reach the run's people only):
    - creating the event starts the approval, naming the approver;
    - the creator, who is not the organiser, is refused by the ENGINE (403, not named and barred);
    - the named approver approves it, and the approval completes;
    - the approver is told, linked to the event page, and nobody outside the run is told;
    - a move clears the approval and starts a fresh one, approved again;
    - the approver may not approve an event they organise (422);
    - a rejection with no reason is refused; a rejection cancels the event, saying why, with its room
      booking;
    - a cancellation withdraws an approval under way.
- Regression: the round-4 net **207/207**. `run-slice0`'s approval now runs the no-definition path.
- **A harness defect found and fixed:** `GET /api/Workflow/definitions` is PAGED (25 of 118). The
  first run's opening guard and its retire read page one, so both were vacuous and the definition
  stayed published. The leftover named an approver login already switched off, so the next run's
  approval starts were refused by the engine ("no independent active user… eligible"). They fell back
  to the approve tier as designed, three times in the log. The suite now filters by entity type with a
  wide page. ⚠ Other HR harnesses that list definitions unfiltered have the same blind spot.
- **UAT changes:** the suite's entity-type seed added exactly one type, `CompanyEvent` (217 → 218;
  nothing switched off or updated). Three harness definitions, all retired. 15 CompanyEvent instances,
  all completed or cancelled. Nothing of the run's left; 15 harness logins switched off.
- **UAT's definition, published on the user's word (2026-10-05):** "Company Event Approval", id
  `74c589fc…`.
  - **Why not the seeder:** start-up seeding is off. `seed-workflows` touches every module (Finance,
    help desk SQL, procurement, estates, legal…). And the seeder SKIPS this type on UAT anyway: its
    existence check counts any definition for the type, retired harness ones included.
  - **How:** `dev-harness/hr-company-schedule/tools/publish-company-event-definition.mjs` builds it in
    the seeder's exact shape (Draft → PendingApproval for the roles HR and TenantAdmin, initiator
    barred, distinct approvers → Approved), re-reads it (3 steps, 2 transitions) and is idempotent. A
    rebuilt database gets it from the seeder.
  - **What it means on UAT:** an event that needs approval asks the whole HR desk, which on UAT is two
    logins, `hr.head` and `hr.officer`. Its creator may not approve it, so one approves what the other
    creates.
  - **The suites, adapted:**
    - every approval is by a second HR login (B in the review suite, a minted officer in `run-slice0`);
    - block 2b runs the engine checks through the real definition when it is live, and skips the
      no-definition path (proved twice before);
    - both suites withdraw the notices their events raise (soft delete by SQL, re-checked after the API
      stopped: 0 live).
  - **Re-proof against it:** `run-final-review.mjs` **270/270 on two clean passes**. That is 275 less
    the five checks that need no definition: the waiting event, the tier's approval, publishing the
    harness definition, "only the run's people told", and retiring it. The round-4 net is **208/208**,
    `run-slice0` gaining the notice check. The desk was asked 18 times each across the runs, all
    withdrawn. No 500s; no refused starts.
- API log: #23, #39, the three refused starts above; no request answered 500.

*2a — what was built (2026-10-05):*
- **`CompanyEventRules`, pure.** It holds:
  - the window check, one for create, edit and reschedule;
  - firmness (F-41) and awaiting-approval;
  - the categories that belong elsewhere (F-44);
  - the per-day windows of an event (R4-10A.2).

  The window check refuses, with the sentence to act on:
  - a missing date (it bound as 0001-01-01);
  - an end before the start, or one time without the other;
  - asking for replies without a deadline, or a deadline after the start;
  - reminders without a lead, or a negative lead.

  It drops what does not apply: an all-day event's times, an unused deadline, an unused lead.
- **The event service:**
  - **References:** the site, the unit and a new organiser are checked in the tenant, as a 422 (F-10).
  - **D-5 and D-11:** a department is refused; an event for a unit needs the unit; the organiser is
    chosen, defaulting to the caller, and the creator is stamped (`CreatedBy` was null on every event).
  - **Status by edit:** only Scheduled, In progress or Postponed, or Confirmed where no approval is
    needed. Cancel, Complete and Reschedule have their own actions (F-37).
  - **Approve:** refuses an event that needs no approval, one already approved, one in another
    status, and the organiser's own.
  - **Cancel twice, complete before the start, and editing a closed event** are refused.
  - **One move path, `MoveAsync`, for Reschedule and an edit that changes the window:**
    - the event's live room bookings move by the same amount, and the move is refused, naming the
      room, if a room is taken;
    - the first original window is kept;
    - the status becomes Rescheduled (C-7);
    - the reminder and chase stamps are cleared;
    - an approval is cleared;
    - accepted and tentative answers go back to Sent.
  - **Cancel and delete** cancel the live linked bookings (F-39). Cancel, reschedule and delete answer
    `CompanyEventChangeDto`.
  - **Reads:** the lists run on one tenant-scoped query, the repository's nine custom reads are
    removed (F-30), ranges use overlap, "upcoming" uses UTC, and `events/unit/{id}` replaces the
    department read. Updates re-read (F-46).
  - **Mapping:** one filler for the list and detail reads, so the detail now carries the original
    window (F-58).
- **The diary and clash source:** the organiser is committed whether invited or not (D-11); every
  day of the event counts; an accepted, firm, timed event is hard.
- **The screens:**
  - the form gains the organiser picker, the unit picker in place of the department, the categories
    for a new event, the statuses an edit may set, and a reason box when the dates move;
  - the event page gains a new RSVP deadline on reschedule, shows Complete only once started and Edit
    only while open, toasts what moved or was cancelled, and shows the unit.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–2a: **250/250 on two clean passes**; 2a has 94 assertions. They
  cover each refusal (422 and its sentence), what is dropped, the organiser and the creator, the unit
  read, overlap, the diary (organiser, day two, firm and awaiting-approval), the status, approval and
  move rules, a room booking moved, refused on a clash, cancelled and deleted with its event, complete
  before and after the start, and the re-read. The creator reads back as the HR login's name with its
  user id.
- Regression:
  - the round-4 net **207/207**. `run-slice0` is now 28 and `run-slice2` 64, both updated for 2a's
    rules: the organiser approving their own event is refused, a plain edit sends its RSVP deadline
    and keeps its times, and completing before the start is refused;
  - `hr-recruitment/run-round4-d` (the interview clash check, which reads the changed source)
    **58/58**.
- API log: only #23 and #39; no request answered 500.
- Clean-up: none of the run's events, bookings or room left. 20 harness logins (round-4's and the
  recruitment suite's) switched off. The recruitment suite leaves its documented E2E residue
  (README), and the round-4 suites theirs (R4-2.1).
- **Demo pack:** scenario 110's board meeting asked for replies with no deadline, which 2a now
  refuses. It now sends one, three days before the meeting (`dev-harness/hr-demo-smoke`, outside the
  repo).
- Not yet walked in a browser (lane 5).

### Lane 3 — Rooms and bookings (D-10, D-13, D-18; C-8, C-28, C-30, C-31, C-32, C-33, C-36, R4-9.1, R4-12.1, F-6, F-7, F-15, F-18, F-34, F-47…F-50)

- [x] *✅ 3a; the create checks deleted rooms' codes too.* Room update validates the site and the code's uniqueness (deleted rooms included, R4-12.1); a
      blank code is regenerated.
- [x] *✅ 3a: listed, offered and cancelled, deletion refused with history; ✅ 3b-1: the bookers told.* **Retiring a room per D-18** (F-49): deactivating or deleting a room with future bookings offers
      "cancel these N bookings and tell their bookers"; deletion is refused once the room has any
      booking history — deactivate instead.
- [x] *✅ 3a; every room and booking list read is tenant-scoped in its query (F-30).* Availability: tenant-scoped; `MaxBookingDurationHours` and `AdvanceBookingDays` applied; active
      and bookable only.
- [x] *✅ 3a; an event must also be still to happen; a start and an end are required.* Booking create: room active; `EventId` validated; a linked event's dates must contain the
      booking; seat check uses the larger of the booking's attendees and the event's estimate.
- [x] *✅ 3a, through `IUnitOfWork.ExecuteInTransactionAsync` + `AcquireTransactionLockAsync` (the update locks
      only when the window moves).* **(F-47)** Create and update hold an application lock per room (`sp_getapplock` inside a
      transaction) around the clash check and the write.
- [x] *✅ 3a; a no-show is refused too.* Booking update refused on a cancelled or completed booking; moving an approved booking's window
      on an approval-required room returns it to Tentative and clears the approval.
- [x] *✅ 3a: the guards, and never the booker; ✅ 3b-1: on the engine, with Not approve (which cancels) beside it.* **Approve:** Tentative and not cancelled — *review: the first draft also required the room to
      need approval, which strands bookings made before that switch was turned off*; on the engine per
      D-10. **Cancel:** not cancelled or completed.
- [x] *✅ 3b-2; HR's run-now runs it too.* **(F-48)** The hourly sweep lapses a Tentative booking whose start has passed — cancelled with
      "Not approved before it started", the booker told.
- [x] *✅ 3b-2: completion silent; no-show from the start, a completed one too, for good (the user's ruling).* `Completed` by the hourly sweep for past Confirmed bookings; "Mark no-show" for HR on a past
      booking.
- [x] *✅ 3b-1: the approver through the engine (inbox and notice, linked to the booking page); the booker of approved,
      not approved and cancelled any way, in the app and by email; ✅ 3b-2: lapsed and no-show.* **(F-34)** The approver is told a booking awaits them (or the engine inbox, D-10); the booker is
      told when it is approved, cancelled, lapsed or marked no-show — in-app and email.
- [x] *✅ 3a (`RoomBookingRules.AsUtc`, on the way in and out).* **(F-50)** Booking instants read back as UTC (`DateTime.SpecifyKind`), as the reminder stamps
      already are.
- [x] *✅ 3c: `api/CompanySchedule/me` and three portal pages; others' bookings as busy times only; no event link, no
      past start (the user's rulings).* **Staff booking per D-13**: a self-service door for the signed-in employee's own bookings.
- [x] *✅ 3a.* Rooms page: the delete toast says 403 when it is one; Delete hidden without Admin.
- [x] *✅ 3a.* (D-9, C-32) Delete on the booking detail page, Admin-only and hidden otherwise, beside the
      register's.
- [x] *✅ 3d-1: booked for every date, approved once, told once; ✅ 3d-2: an extended series brings its rooms, and the event
      page's Rooms card.* **(D-12, moved here from lane 2f on the user's word, 2026-10-05)** "Book this room for every occurrence"
      of a series (lane 2f-1's `RecurrenceSeriesId`): one booking per date, each through this lane's rules and
      lock; the dates where the room is taken are listed, the rest booked. A past or completed occurrence is
      never booked.

**State:**

*Source check (2026-10-05, at HEAD 20b6f4c7d) — every item above is still open; nothing in lanes 0–2 built any of it
beyond 2a's linked bookings moving and cancelling with their event and 2g-1's booking register:*
- **Rooms:**
  - F-6: `MeetingRoomService.UpdateAsync` checks neither the site nor the code, and stores a blank code;
  - R4-12.1: create's code check reads live rooms only, but `IX_MeetingRoom_Tenant_RoomCode` covers deleted ones, so
    a deleted room's code is a 500;
  - C-36, F-49: delete and deactivate ignore future bookings; delete hides the room's history;
  - F-15, C-28, R4-9.1: the availability read's booked-room subquery is not tenant-scoped, and it applies seats only;
  - F-30: the room lists (by site, available, active) load every tenant's rows and filter in memory;
  - F-46: the update answers without a re-read, so a changed site's name is stale;
  - C-33: the Rooms page (under Administration) blames bookings for a 403, and shows Delete without Admin.
- **Bookings:**
  - F-7: create checks `IsBookable` but not `IsActive`; `EventId` is never checked, nor that the event's days hold
    the booking; the seat check ignores the event's `EstimatedAttendees`;
  - F-47: the clash check and the insert are not locked;
  - F-8 (C-30, C-31): approve and cancel have no state guard; an edit is allowed on a cancelled or completed
    booking; approval is a Write action, not on the engine, and nothing stops the booker approving their own;
  - F-48: no lapse for a Tentative booking whose start has passed, no Completed sweep, and no no-show action
    (`BookingStatus.NoShow` exists, unused);
  - F-34: no booking notice of any kind, in-app or email;
  - F-50: `StartDateTime` / `EndDateTime` read back unmarked (`SpecifyKind` is applied only to the event's
    reminder stamps);
  - F-30: the booking lists (by room, booker, range, status, pending) load every tenant's rows;
  - C-32: no Delete on the booking page;
  - **F-58 (new):** moving an event rewrites its linked bookings' `BookingDate` — "Booked on" on the booking page —
    to the new start date (`MoveLinkedBookingsAsync`, lane 2a);
  - D-12: a booking links one event; nothing books a series;
  - D-13: no staff door. Staff self-service lives under `/me` (`StaffTravelMeController` is the pattern: the token is
    the actor, another's record answers 404, privileged routes absent by construction).
- **The engine (D-10):** `RoomBooking` is not in the workflow entity-type catalogue, and has no adapter and no
  definition.
- **UAT (read-only):**
  - three demo rooms (BRD, CONF-A, HUD-1), none needing approval; no booking is Tentative or linked to an event,
    none overlaps another, and none was approved by its booker;
  - one demo booking, BK-2026-00001 (Boardroom, 1 October, Confirmed, past) — the Completed sweep will close it;
  - **34 live "R4D Room" rooms with 76 future Confirmed bookings are harness residue:** this module's
    `run-round4-d.mjs` makes a room and three bookings every run and removes neither (R4-2.1's tidy step, owed by
    lane 6).

*Rulings by the user (2026-10-05, all as recommended):*
- **D-13 — yes, from `/me`:** a "Room bookings" page where staff book any active room under the same rules, and see,
  change and cancel only their own. Others' bookings show as busy times, with no purpose or booker. A room needing
  approval routes to the approver. HR keeps the desk.
- **D-18 — offer to cancel them:** deactivating or deleting a room with future bookings lists them and offers
  "cancel these N and tell their bookers"; a room with any booking on record cannot be deleted, only deactivated.
- **Four slices:** 3a rules and guards (rooms, availability, the booking checks, the lock, UTC, retiring a room);
  3b approval on the engine, the booking notices (in-app and email), the hourly lapse and completion, no-show; 3c
  staff booking from `/me`; 3d "book this room for every date" of a series.
- **UAT's residue cleaned in 3a:** the 34 "R4D Room" rooms and their 76 bookings removed through the API, and
  `run-round4-d.mjs` given its clean-up (R4-2.1, moved here from lane 6), before the retirement rule lands.
  *Second ruling the same day ("this module's only"):* the 125 live events this module's `run-round4-d.mjs` left ("R4D
  A|C|Gate|Moving|Notify|Diary …") are removed too; the recruitment suite's 26 ("R4D Board meeting…", "R4D
  Declined…") stay — its interviews refer to them, and that suite owes its own tidy step. Tool:
  `dev-harness/hr-company-schedule/tools/remove-r4d-rooms.mjs` (dry run by default).

*3b rulings by the user (2026-10-05, all as recommended):*
- **The booker is told of every outcome, in the app and by email:** approved, not approved, cancelled any way (by the
  desk, with its event, by retiring the room, or lapsing unapproved at its start), marked a no-show. Completion is
  silent; nobody is told of their own act. Three emails (approved, cancelled, no-show) and four in-app topics.
- **No-show:** any confirmed booking whose start has passed, including one the sweep has completed; no undo; the
  booker is told.
- **A real "Room booking approval" definition** (the HR desk, the booker barred): the seeder seeds it, and a tool
  publishes it on UAT once. No UAT room needs approval yet.
- **Two slices:** 3b-1, approval on the engine (approve, a new Reject, re-approval after a move) and the booker notices
  on every path; 3b-2, the hourly lapse and completion, and no-show.

*3c source check (2026-10-06, at HEAD 381eaf78d):* the booking service needs almost nothing — create, update and cancel take
the tenant from the token, apply every room rule under the room's lock, start the approval on the engine and tell the booker
(never of their own act); the engine's withdrawal checks no caller, so a staff booker cancelling a waiting booking ends its
approval. What is missing is the door: every room and booking route is on `HR.Company.*`; there is no staff read of the
rooms, their availability or their busy times; and the booker's in-app notices link to the HR booking page, which staff
cannot open. **Found:** nothing refuses a booking whose start has already passed, on either door.

*3c rulings by the user (2026-10-06, all as recommended):*
- **No event link from `/me`:** a staff booking stands alone; a room for a company event stays the desk's (3d books series).
- **The booker's notices open the portal page** (`/me/room-bookings/{id}`) for every booker, HR officers included; the
  approver's inbox link stays on the HR page.
- **No past start from `/me`:** a new booking, or a changed start, may not be in the past; an unchanged start on a booking
  already under way is fine (it can be extended). The HR desk may still record a booking after the fact.
- **A reason to cancel**, as at the desk.

*3d source check (2026-10-06, at HEAD 1eda2ad49):* a series is its occurrences (`RecurrenceSeriesId`); series actions
reach dates through `SeriesTargetsAsync` (never a started, completed or cancelled one); a booking linked to an occurrence
already moves and cancels with it, and a series move is refused whole when any date's room is taken. Nothing books a
series: a booking links one event, chosen on the HR booking form; the event page shows no room at all. Two precedents
from 2f (the user's rulings for events): a series is approved once, and each guest is told once per series action.

*3d rulings by the user (2026-10-06, all as recommended):*
- **Approved once for the set:** a series booked in a room that needs approval — the first date goes to the engine and
  its decision covers every date still waiting; deciding another is refused, naming the one that carries it; if the
  carrying date is cancelled, deleted or lapses, the approval passes to the next; a date later moved on its own is
  approved on its own.
- **The booker told once per act,** listing the bookings and dates, when one act approves, does not approve or cancels
  several of their bookings (series cancel and retiring a room included): one new email template, one in-app topic.
- **Extending a series books the latest date's rooms** for the new dates where free, the taken dates listed; the person
  extending is the booker.
- **Two ways in:** the booking form's "every date from this one on" when the linked event is in a series, and a Rooms
  card on the event page (its bookings; "Book a room" prefilled, with the series option on a series).
- *Defaults stated at the check:* each date's booking keeps its distance from that date's start (as a moved event moves
  its rooms); scope "this date and following" or "every date still to come"; a date the room cannot take (held, already
  booked for it, too far ahead, too many people) is listed with its reason and the rest booked under one lock; if none
  can be, the request is refused.
- **Two slices:** 3d-1 — booking a series, approval once for the set, the booker told once; 3d-2 — the extension's rooms
  and the event page's Rooms card.

*3d-2 — what was built (2026-10-06): an extended series brings its rooms; the event's rooms on its page (D-12). **Lane 3 is
done.***
- **No migration**, no template, no topic.
- **`IRoomBookingService.CarryRoomsAsync`**, called by `ExtendSeriesAsync` after its save: each room the latest date still
  holds is booked for the new dates by whoever extends — at the same distance from each date's start, for the same length,
  its purpose, people and requirements copied — through 3d-1's per-date core (`BookDatesAsync`, extracted), which here
  never refuses: a room out of use, or a date a room cannot take, is listed. On a room needing approval the new dates are
  approved once. The extension's answer gains `rooms`; each date not booked is a warning naming the room. A login with no
  employee link carries nothing and is told to book them (it cannot be a booker); a failure to carry never fails the
  extension.
- **`GET events/{id}/bookings`** (Read): an event's bookings, any status, in time order.
- **Screens:** the event page's **Rooms card** (`EventRoomsCard`): its bookings with their status, linked; "Book a room"
  and, on a series date, "Book for this and following dates", for whoever may book while the event is open. The booking
  form reads `?event=&scope=` — the event chosen even beyond the upcoming list, its times and name filled where empty. The
  extend dialog says rooms come too, and its toast lists them.

*Proof (UAT, API in Staging; the real Room Booking Approval live):*
- `run-final-review.mjs` blocks 1a–3d-2: **1002/1002 on two clean passes**, first time. 3d-2 has **13 assertions**: the
  latest date holding three rooms (two by series, one alone); an event's bookings any status, in time order; extended by
  three — three answers; the open room booked for the free dates, the held one listed naming its booking; each linked to
  its new date, booked by the extender, its purpose copied; the approval room's new dates Tentative, the first carrying
  the approval, only it under way; the room booked alone carried at its own 08:30 and the dates beyond its 30 days listed;
  the dates not booked among the warnings; extended by admin — the date made, no room, told why.
- **Regression:** the round-4 net **212/212**; recruitment **59/59**; the templates probe **42/42**; `run-lane-n`
  **109/115**, the six section-J failures of #40.
- **The API log:** no request answered 500; nothing failed to carry, start, withdraw or tell; each pass carried 1, 2 and 3
  dates as the suite expects. The ERR lines are the known kinds. The blocking watcher logged one 1.9 s lock wait (the
  notification dispatcher writing back notices it could not email), under its 3 s mark, and nothing else in two passes.
- **After the runs:** every RoomBooking approval the runs started is finished (12 completed, 22 cancelled); no CSF booking
  is live; no company-schedule notice to a real login is live; every harness login is off; the R4D requisition's notices
  withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*3d-1 — what was built (2026-10-06): a room for every date of a series, approved once, the booker told once (D-12).*
- **No migration.** One email (`BookingsChanged`, "Room Bookings Changed": 56 templates), one in-app topic (20).
- **`POST bookings/series`** (Write; `IRoomBookingService.CreateForSeriesAsync`): the window given for the linked occurrence;
  each date in the scope still to come (`CompanyEventSeries.Reach`, now shared with the event series actions) booked at
  the same distance from its own start, for the same length; each through the single booking's rules — its event's days,
  the room's limits and seats, no live booking holding the room, the room not already booked for that date — under the
  room's one lock in one transaction. A date it cannot take is listed with the reason, the rest booked; none bookable,
  nothing booked (422). The answer: booked, not booked, how many dates were left alone (started, completed, cancelled),
  and which booking carries the approval.
- **Approved once for the set** (`RoomBookingDesk`; the set is the same booker's bookings of the same room for the same
  series, read from the bookings and their events — no table): only the first date asks; approving covers every date still
  waiting with nothing of its own under way, not approving cancels them; deciding another is refused, naming the carrier
  (`RefuseSharedElsewhereAsync`). When the carrier is cancelled, deleted or lapses, the approval passes to the next date
  still to come (`PassApprovalOnAsync`) — started through `IWorkflowService.StartApprovalWorkflowAsAsync` in the name of
  whoever started the one withdrawn, so the hourly lapse can pass it on with nobody signed in, and the approver is not
  barred. The withdrawals now answer who started what they withdrew. Dates moved with their series ask again once per set
  (the event service's flush groups them).
- **Told once:** when one act approves, does not approve or cancels several of a booker's bookings — the set decided, a
  series cancelled, a room taken out of use — each booker gets one notice (`BookingsChanged`: "Approved: 5 room
  bookings", linked to `/me/room-bookings`) and one email listing them, each with its reason. One booking keeps the
  single-booking notice.
- **Screens:** the HR booking form's "Which dates" when the linked event is in a series ("Book the dates"), the free-room
  search said to check this date only, and a result card listing what was booked and not. The event summary carries
  `recurrenceSeriesId` and `occurrenceNumber`. `SeriesScopeField` takes its closing clause.

*Proof (UAT, API in Staging; the real Room Booking Approval live):*
- `run-final-review.mjs` blocks 1a–3d-1: **989/989 on two clean passes**, first time. 3d-1 has **51 assertions**:
  - **booking a series:** every date still to come but the one the room is held on, the held date naming the booking; the
    cancelled date counted as left alone; each linked to its own date; each half an hour before its own start — the date
    moved to 14:00 at 13:30; booking again refused, each date already booked; asked from the fourth with "every date",
    the dates inside the room's 25 days booked and the rest listed as too far ahead; too many people on every date
    refused; one date, and a single event, refused;
  - **approved once:** Tentative throughout, only the first under way; deciding the third refused, naming the first; B
    approves the first — all five Confirmed by B, nothing left under way; the booker told once in the app (linked to
    their bookings), never per date, and by one email listing all five;
  - **the series moved:** every date waiting again, one approval asking for all, on the first;
  - **told once:** a room retired with five of the booker's bookings — one notice, one email saying why; the series
    cancelled — eight bookings in two rooms cancelled, no approval left, one notice and one email;
  - **not approved:** deciding the second refused, naming the carrier; not approving the first cancels all four with the
    reason, one notice and one email;
  - **passed on:** the first cancelled by its booker — the second carries it; the second deleted — the third; the third
    lapsing in the sweep — the fourth, started in the booker's name; B approves the fourth and covers the fifth.
- **Regression:** the round-4 net **212/212**; recruitment **59/59**; the templates probe **42/42** (its own total moved to
  56 — the one pin missed at the build; the screen was right); `run-lane-n` **109/115**, the six section-J failures of #40,
  its TOTAL 56 and B7 with the new raw `BookingList`.
- **The API log:** no request answered 500; no approval failed to start, withdraw or pass on, and no booker notice failed;
  six pass-ons, three a pass. The ERR lines are the known kinds. The blocking watcher's first pass logged the suite's own
  notice counts scanning `Notifications` (3–6 s parallel waits, no blocker) and sub-second lock waits on notice inserts;
  its second pass nothing.
- **After the runs:** every RoomBooking approval the runs started is finished (10 completed, 20 cancelled); no CSF booking
  is live; no company-schedule notice to a real login is live; every harness login is off; the R4D requisition's notices
  withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*3c — what was built (2026-10-06): staff book rooms from the portal (D-13).*
- **No migration**, no template, no in-app topic.
- **`CompanyScheduleMeController`** (`api/CompanySchedule/me`, `InternalOnly`, no `HR.Company.*`; the travel portal's
  pattern — the booker is the token, anyone else's booking is a 404, the desk's acts have no route):
  - `GET rooms` — in use and open for booking, with their rules (`IMeetingRoomService.GetBookableRoomsAsync`);
  - `GET rooms/available` — the desk's availability read; the room summary now says `requiresApproval`;
  - `GET rooms/busy?from&to` — whole days, at most 31: each live booking's room and time; the caller's own marked with
    their id; nobody else's purpose, booker or number (`IRoomBookingService.GetBusyTimesAsync`);
  - `GET/POST room-bookings`, `GET/PUT room-bookings/{id}`, `POST room-bookings/{id}/cancel` (a reason, as at the desk).
- **The portal's own rules** (the user's rulings): the payload's `eventId` is dropped; the start may not be past —
  `RoomBookingRules.RefuseSelfServiceStart`, with **15 minutes' grace** so a room taken "from 10:00" at five past still
  books, and an unchanged start (to the minute) allowed so a booking under way can run on. Everything else is the desk's
  service, unchanged: the room's limits, the seats, the lock, the approval on the engine, the booker told.
- **The booker's notices** open `/me/room-bookings/{id}` (`CompanyScheduleNotices.BookingLink`), for every booker; the
  approver's inbox link stays on the HR page.
- **Screens:** `/me/room-bookings` (a day board of when the rooms are held, and the caller's bookings, upcoming and past),
  `/me/room-bookings/new` (the desk's form without the event, the board with the time being booked drawn green or red,
  the chosen room's rules), `/me/room-bookings/[id]` (change, cancel with a reason; the room's day). `RoomDayBoard`
  (`components/hr/company-schedule`) draws others' bookings grey and the caller's own as links. "Room Bookings" in the
  portal's Company menu and on its landing page.
- **Seen in the proof, not changed:** a clash refusal names the booking that holds the slot ("already booked then:
  BK-2026-…, 09:00–10:00") — to staff too. It is neither purpose nor booker, and the time is on the board anyway.

*Proof (UAT, API in Staging; the real Room Booking Approval live):*
- `run-final-review.mjs` blocks 1a–3c: **938/938 on two clean passes**, first time. 3c has **62 assertions**, as a login
  holding the Employee role only, linked to fixture A:
  - **the control:** the HR register and the desk's booking door refuse that login (403);
  - **the rooms:** only those in use and open for booking, with their rules; free rooms, which need approval, not a held,
    too long or too full one;
  - **busy times:** the desk's booking as its room and time only — no purpose, booker or number anywhere in the answer;
    a cancelled one not held; more than 31 days and a backwards span refused;
  - **booking:** Confirmed and theirs; the event in the payload dropped; on the board as theirs; a past start, too many
    people, a held time and a closed room refused; one from ten minutes ago taken;
  - **their own only:** the list holds theirs and not the desk's; the desk's booking a 404 to read, change and cancel; an
    unknown id a 404; approve, not approve, no-show and delete absent (404/405);
  - **changing:** moved and re-worded; not to a past start; a booking under way extended with its start unchanged, but its
    start not moved back;
  - **approval:** staff refused at the desk's door; Tentative with the approval under way on the engine; B approves; the
    booker told, the notice opening the portal page; moved — waiting again;
  - **cancelling:** a reason needed; cancelled with it; the approval withdrawn; nobody told of their own act; not again,
    not changed;
  - **the desk:** sees the staff booking, booked by A; an HR officer's own notice opens the portal page too; a login linked
    to nobody is told so (400).
- **Regression:** the round-4 net **212/212**; recruitment **59/59**; the templates probe **39/39**; `run-lane-n`
  **109/115**, the six section-J failures of #40, its TOTAL 55.
- **The API log:** no request answered 500; the portal's 422s are each an intended refusal; no approval failed to start
  or withdraw, and no booker notice failed. The ERR lines are the known kinds (payroll's foreign key on minted fixtures,
  #23; 2d's unique-index race proofs; notices with no mail server; the sinks' bounce addresses; identity reconciliation
  catching fixture logins mid-teardown). The blocking watcher's first pass logged eight sub-second lock waits on
  notification inserts in block 2b, and its second pass nothing.
- **After the runs:** every RoomBooking approval the runs started is finished (6 completed, 10 cancelled); no
  company-schedule notice to a real login is live; every harness login is off (28); the R4D requisition's notices
  withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*3b-2 — what was built (2026-10-06): the sweep's booking half, and no-show (F-48, F-34).*
- **No migration.** One email (55 templates), one in-app topic (19).
- **`IRoomBookingService.SweepAsync(tenantId, now)`** — tenant-explicit, safe with nobody signed in — called by the hourly
  `CompanyScheduleReminderBackgroundService` per tenant (after the events' reminders, in its own try) and by HR's
  `POST reminders/run`, whose answer gains `bookingsLapsed` and `bookingsCompleted`:
  - **lapse (F-48):** a booking still Tentative when its start comes is cancelled, "Not approved before it started.",
    saved, its approval withdrawn, its booker told ("not approved");
  - **completion:** a confirmed booking whose end has passed is Completed, silently (the user's ruling).
  - ⚠ **Found while building:** the integration's withdrawal (`IWorkflowIntegrationService.CancelWorkflowAsync`) resolves
    the instance by the signed-in user's tenant and records the signed-in user — with nobody signed in it throws, and
    a lapsed booking's approval would have gone on asking the desk. `RoomBookingDesk.WithdrawLapsedApprovalAsync` finds
    the instance itself and asks the engine (`IWorkflowEngine.CancelWorkflowAsync`) in the name of the login that
    started it — the activity log's foreign key needs a real user.
- **No-show** (`POST bookings/{id}/no-show`, Write; `RoomBookingRules.RefuseNoShow`): a confirmed booking whose start has
  passed, one the sweep completed too; never undone; a booking to come, a cancelled one or one already a no-show is
  refused with its reason. The booker is told — "Room Booking No-Show" email, `BookingNoShow` topic.
- **Screens:** the booking page's "Mark no-show", with a confirmation that says it cannot be undone.
- **UAT:** the first hourly pass after the deploy — nobody signed in — completed BK-2026-00001 (the demo booking of 1
  October), as the source check foresaw.

*Proof (UAT, API in Staging; the real Room Booking Approval live):*
- `run-final-review.mjs` blocks 1a–3b-2: **876/876 on two clean passes**, first time, the blocking watcher silent. 3b-2
  has **25 assertions**, its mail through the run's own sink:
  - **the sweep** (HR's run-now): the booking still Tentative at its start lapsed — cancelled with the reason, its
    approval withdrawn on the engine, the booker told in the app and by email; the ended confirmed booking Completed,
    with no notice and no email; a booking under way left as it is;
  - **no-show:** refused for a booking to come and a cancelled one; a booking under way marked, its booker told in the
    app and by email; a completed one marked too; not again; and a no-show cannot be changed.
- **Regression:** the round-4 net **212/212**; recruitment **59/59**; the templates probe **39/39**; `run-lane-n`
  **109/115**, the six section-J failures of #40, its TOTAL 55.
- **The API log:** no request answered 500; no sweep, withdrawal or notice failed. The hourly pass ran with nobody
  signed in (one completion) and each run-now lapsed one booking and completed one.
- **After the runs:** every RoomBooking approval the runs started is finished (9 completed, 16 cancelled); no
  company-schedule notice to a real login is live; every harness login is off (28; the no-email one by SQL, #42); the
  R4D requisition's notices withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*3b-1 — what was built (2026-10-05): a booking's approval on the engine, and its booker told (D-10, F-34; C-8).*
- **No migration.** Two emails (54 templates), three in-app topics (18).
- **`RoomBookingDesk`** (`Services/HR/CompanySchedule`, scoped): what the booking, room and event services share — the
  approval on the engine (start, decide, withdraw) and telling the booker (in the app and by email, raced against ten
  seconds; never of their own act; never failing the act).
- **The engine, as for events (lane 2b):** `RoomBooking` in the entity-type catalogue, the inbox display (the room and
  day, linked to the booking page), the routing context, and `RoomBookingWorkflowStatusAdapter` (Approved → Confirmed;
  Rejected → cancelled "Not approved: …"; Pending and Recalled → Tentative). The seeder seeds "Room Booking Approval"
  (HR and TenantAdmin, the initiator barred).
  - a booking of a room that needs approval starts its approval at creation; a confirmed one that moves — by its own
    edit, or with its event — waits again with a fresh one;
  - **Approve** decides through the engine when an approval is under way, the approve tier (`HR.Company.Approve`) when
    none is; a definition with another stage leaves it Tentative;
  - **Not approve** (new, `POST bookings/{id}/reject`, Write): a reason; never the booker; cancels it;
  - cancelling, deleting, retiring the room or cancelling its event withdraws an approval still under way.
- **The booker told** (the user's ruling): approved; not approved; cancelled by somebody else — the desk, with its
  event, or by retiring its room (D-18's "tell their bookers"). Emails "Room Booking Approved" and "Room Booking
  Cancelled" (the latter worded "not approved" or "cancelled", with the reason); topics `BookingApproved`,
  `BookingNotApproved`, `BookingCancelled` to the `Booker`. The notice links to the booking page — until 3c gives staff
  their own.
  - ⚠ An event act that cancels or moves bookings queues them and tells their bookers after its own save
    (`FlushBookingOutcomesAsync`, on all ten paths that call `CancelLinkedBookingsAsync` or `MoveAsync`), so nobody hears
    of a change that did not save.
- **Screens:** the booking page's "Not approve" with a reason; the approval's toast says when another stage is still to
  come; the retirement dialog says the bookers are told.
- **UAT, once, on the user's ruling:** "Room Booking Approval" published by `tools/publish-room-booking-definition.mjs`
  (`29417732-…`; HR and TenantAdmin, the initiator barred; a second run does nothing). No UAT room needs approval.

*Proof (UAT, API in Staging):*
- **Before the definition:** `run-final-review.mjs`, 3b-1 in its no-definition mode — **36/36**: a booking waits with
  nothing under way and the approve tier approves it; the booker told, in the app and by email; then the run's own
  definition naming B. (That run's one failure was 2e-1's topic count, 15 → 18, corrected.)
- **With the real definition:** blocks 1a–3b-1 **851/851 on two clean passes**, the blocking watcher silent. 3b-1 has
  **31 assertions** in this mode, its mail through the run's own sink:
  - **the engine:** an approval under way, which B can approve; the booker refused; B approves — Confirmed, by B, the
    approval complete; the approver told with a link to the booking page; the booker told in the app and by email;
  - **not approved:** the booker refused; a blank reason refused; cancelled "Not approved: …", the approval finished,
    the booker told with the reason;
  - **moved:** Tentative again with a fresh approval;
  - **cancelled:** by the desk — approval withdrawn, the booker told with the reason; by the booker — told nothing;
    with its event — the booker told; by retiring its room — told it was taken out of use.
- **Regression:** the round-4 net **212/212**; recruitment **59/59**; the templates probe **36/36** (the two new emails);
  `run-lane-n` **109/115**, the six section-J failures of #40, its TOTAL 54.
- **The API log:** no request answered 500; no approval failed to start or withdraw, and no booker notice failed. The
  ERR lines are the known kinds (payroll's foreign key on minted fixtures, #23; the unique indexes' race proofs).
- **After the runs:** every RoomBooking approval the runs started is finished (5 completed, 8 cancelled); no
  company-schedule notice to a real login is live; every harness login is off (the no-email one by SQL, #42); the R4D
  requisition's notices withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*3a — what was built (2026-10-05): the rules and guards (F-6, F-7, F-8, F-15, F-30, F-46, F-47, F-49, F-50, F-58; C-28,
C-30, C-31, C-32, C-33, C-36; R4-9.1, R4-12.1; D-18).*
- **No migration.**
- **`RoomBookingRules`** (beside `CompanyEventRules`, pure): `AsUtc`; `IsLive` (Tentative or Confirmed, not cancelled);
  `RefuseWindow` (a start and an end, the end after the start — `[Required]` on a non-nullable `DateTime` does nothing,
  and a missing start bound as year 1); `RefuseEditing`, `RefuseApproving` (Tentative, not cancelled, never the booker),
  `RefuseCancelling`; `RefuseOutsideEvent`; `SeatsNeeded`; `Cancel` (the one place a booking is cancelled — the booking,
  room and event services all use it).
- **Rooms:**
  - the room and booking list reads are on the services' own tenant-scoped queries; the unscoped repository reads are
    deleted (F-30; two had no caller);
  - the edit checks the site and the code (F-6), against every room including deleted ones, and the create does too
    (R4-12.1, which was a 500); a blank code is issued afresh; the edit answers with an untracked re-read (F-46);
  - **D-18:** `GET rooms/{id}/retirement` (Read) lists the bookings still to come and counts those on record;
    deactivating with bookings to come is refused, naming them, unless `CancelFutureBookings` says to cancel them —
    each "{room} was taken out of use."; deleting is refused while any booking is on record (a deleted booking is not),
    pointing to deactivating;
  - availability: in use and bookable, seating enough, no live booking in the window, and each room's own longest
    booking and furthest-ahead limits (R4-9.1); a backwards window is refused.
- **Bookings:**
  - create: a start and an end; the room in use and bookable (F-7); the event this tenant's and still to happen, the
    booking on its days, and seats for the larger of the booking's count and the event's estimate; the clash check and
    the write under one lock per room in one transaction (F-47), the clash naming the booking that holds the slot;
  - a clash is a live booking — Tentative or Confirmed — everywhere, the event's own move included;
  - update: refused once cancelled, completed or a no-show; the same checks; locked only when the window moves; a
    confirmed booking that moves on a room needing approval waits for approval again;
  - approve and cancel guarded (F-8); a cancellation needs a reason;
  - the times read back as UTC (F-50);
  - ⚠ the edited paths save by tracking, not `UpdateAsync`: `Update()` marks the whole loaded graph modified — the room,
    the people, the event, and through cancelled bookings their bookers' Employee rows.
- **The event's move (lane 2a's helper):** "Booked on" is no longer rewritten to the new start (**F-58**, new), and the
  room's furthest-ahead limit applies to a moved booking (lane 2a left that to lane 3).
- **Screens:** the Rooms page has Deactivate and a retirement dialog (`RoomRetireDialog`: the bookings still to come,
  "Cancel N bookings and deactivate"; a room with history offers "Deactivate instead"); Delete hidden without Admin, and
  a 403 says so (C-33); the room edit page asks before switching a room off. The booking page has Delete for Admin
  (C-32), offers actions only while the booking holds its room, and never Approve to its booker. Until 3b nobody is
  told of a cancellation, and the dialog does not say they are.
- **Harness:** `run-final-review.mjs` block 3a; `run-slice1` asserts the two approval refusals (its booking is
  Confirmed and approved by its own booker before); both round-4 D suites clean up after themselves (R4-2.1).

*Proof (UAT, API in Staging):*
- **UAT's residue first** (`tools/remove-r4d-rooms.mjs --apply`, on the user's rulings): 76/76 bookings, 34/34 rooms and
  125/125 events removed; the 3 demo rooms and the recruitment suite's 26 events untouched.
- `run-final-review.mjs` blocks 1a–3a: **820/820 on two clean passes**, first time, the blocking watcher silent. 3a has
  **61 assertions**, on its own rooms and events near today:
  - **a room's edit:** a bad site refused; another room's code refused, naming it; a blank code issued afresh; a new
    site answering with its own name; a deleted room's code refused, saying so;
  - **retiring:** the bookings still to come listed (the cancelled one left out), three on record, not deletable;
    deactivating without the word refused, naming them; deleting refused, pointing to deactivating; deactivating with
    it cancels them, saying why, the cancelled one keeping its reason; a room out of use cannot be booked; a room with
    nothing on record deleted;
  - **availability:** offered inside its rules; not for longer, further ahead, more people, a held window, or out of
    use; a backwards window refused;
  - **a booking:** no start refused; a missing event, another day, too many seats counting the event's estimate, and a
    cancelled event refused; a linked booking made, its times read back as UTC; a booking over it refused, naming it;
  - **an event's move:** its booking moved, "Booked on" kept (F-58); too far ahead for the room refused;
  - **the lock:** four identical bookings at once — one made, three refused, one row;
  - **guards:** a blank reason refused; a cancelled booking not edited, cancelled again or approved;
  - **approval:** Tentative on a room needing it; its booker refused; a second HR login approves it; again refused; an
    edit keeping the time keeps the approval; a new time waits again.
- **Regression:** the round-4 net **212/212** (slice 1 +1, round4-d's tidy +1); recruitment **59/59** (its tidy +1);
  the templates probe **30/30**; `run-lane-n` **109/115**, the six section-J failures of #40. After the runs no
  "R4D Room" is left; the recruitment suite still leaves two events a run (its own tidy step, not this lane's).
- **The API log:** no request answered 500 and no lock failed to be taken; the clash refusals name the booking. Its
  ERR lines are the known kinds: payroll's foreign key on minted fixtures (#23), the unique indexes refusing the suites'
  duplicate guests and register rows, and notifications with no mail server.
- **After the runs:** every harness login is off (28; the no-email one by SQL, #42); the R4D requisition's notices to
  real staff withdrawn twice (43, then 0), 0 live; nothing of this module's reached anyone real in the hour. The API
  and the scanner stub are stopped.

### Lane 4 — Milestones, fiscal years, company profile (D-3, D-6, D-17; C-40, C-41, C-42…C-50, R4-2.2, F-3, F-13, F-14, F-25, F-55, F-56)

- [x] *✅ 4a; the delete on Write (the user's ruling, as event files).* `CompanyMilestoneDocument` entity, repository, service (add, list, delete); endpoints
      `POST milestones/{id}/documents` (multipart, Write), `GET milestones/{id}/documents` (Read),
      `GET milestones/documents/{docId}/download` (Read), `DELETE milestones/documents/{docId}`
      (Admin). `RelatedDocuments` stays as "References".
- [x] *✅ 4a: a "Files" dialog from the milestone's row (the generic dialog has no room for files), one file at a time.*
      The milestone dialog gains a Documents section after save: the list with download links and a
      multi-file upload control.
- [x] *✅ 4a: and `occurrenceDate`; the range read answers one row per occurrence.* Recurring milestones projected onto their next anniversary in upcoming and range reads; DTO
      gains `nextOccurrence` and `yearsSince`.
- ~~C-41, the milestone link~~ — **closed by D-17: not built.** The certificate is a milestone
      document (above); nothing to build here.
- [x] *✅ 4b; the year's status beside each book whose latest close stands (the user's ruling).* **Fiscal (D-6).** An HR backend read of Finance's `FiscalYears` and periods, in-process —
      *review: the first draft had the browser call Finance's endpoint, which documents a Finance read
      permission it does not yet enforce (F-56)*. The two HR screens are replaced by one read-only
      **Fiscal calendar** card on it (years, status, periods; "Set up and closed in Finance", a link to
      Finance's screen). The HR `fiscal-years` / `periods` endpoints and `FiscalYearService` are removed
      from the controller and registration; tables and DTOs untouched this slice. *At 1163bbc47 Finance
      closes a year per accounting book (§ 3c): what the card calls a year's status — the year's own
      `Status` / `IsClosed`, or its book-close cycles — is settled at this lane's source check.*
- [x] *✅ 4b; also the dates for a year by its number (the manpower forms' period).* `IHrFiscalCalendar` answering the fiscal year for a date from Finance's years, falling back to
      `FiscalYearStartMonth` only when Finance has none; `StaffRequisitionService` uses it, **and so
      does the manpower budget form's fiscal period (`fiscalPeriodFor`, F-56)**; the settings screen
      calls the month the fallback.
- [x] *✅ 4b; and `run-slice14-company.mjs` reads the calendar instead.* `110-company-schedule.mjs` stops seeding an HR fiscal year; `run-slice3.mjs` drops its fiscal
      assertions.
- [x] *✅ 4c; and every image a PNG or JPEG of at most 2 MB (the user's rulings).* (D-9, C-49, C-50) Company profile: the two seal buttons hidden without `HR.Company.Admin`;
      `CompanySealAssetKind.Logo` as a third versioned asset through the same replace/retire doors.
      **(F-55) The free-text `LogoUrl` is retired once the asset exists**: letters and emails read the
      logo asset, then `Tenant.LogoUrl`; the field leaves the profile form.

**State:**

*Source check (2026-10-06, at HEAD d8c948773):*
- **Milestones:** lane 0's `CompanyMilestoneDocument` table exists, read and written by nothing — no upload, list, download.
  `IsRecurringAnnually` is read by nothing; the upcoming read takes server-local "today" and compares the milestone's own
  date, so a yearly anniversary is never upcoming after its first year (UAT's one milestone, "TDC 74th Anniversary",
  recurring, 18 October). The service filters tenants in memory, validates nothing, answers an edit without a re-read
  (F-30, F-46 shapes). The screen is the generic list-and-dialog (`ResourceCollectionTab`), with no room for files; its
  Delete calls an Admin route. Precedent for the yearly rule: `BusinessClosureRules.OccurrencesIn` (C-38) — same month and
  day each year, 29 February on 28 February otherwise.
- **Fiscal:** HR's `FiscalYear`/`FiscalPeriod` (singular tables) are read by nothing but their own service, routes and two
  screens; UAT holds one HR year ("FY 2026", from scenario 110), no periods. Requisitions label the year with
  `HrFiscalYear.For` from `FiscalYearStartMonth` (UAT: 1). Finance's calendar is read in-process today
  (`HrFinanceActualsService` → `IFiscalPeriodService`). ⚠ **Corrections to this plan:** Finance's GET routes DO require
  `Finance.Read` (a global convention, `FinancePermissionAuthorizationConvention`) — F-56's premise is wrong, the
  in-process read stands; F-3's line references are stale (now `CompanyScheduleRepository.cs:252-264`). Finance's year-end
  close is per accounting book (`YearEndBookCloseCycle`) and never writes the year's own `IsClosed`/`Status` — only the
  seeder does (UAT: FY2025 Closed by the seeder, no cycles; FY2026 Open). Finance years end at 23:59:59. The manpower
  budget form already reads policy settings on `HR.Company.Read`.
- **Company profile:** `CompanySealAssetKind.Logo = 3` exists (lane 0) and the Admin route accepts it; nothing reads it.
  Six letter renderers fill `{{CompanyLogoUrl}}` from the free-text `LogoUrl`; `Tenant.LogoUrl` applies only when NO
  profile row exists. Seals are embedded as `data:` images (`GetCurrentAsDataUriAsync`). The page's seal buttons check no
  permission (its comment says they do). The seal upload accepts any allowed file (PDF, Word…). UAT: no logo anywhere; one
  current seal and signature.

*Rulings by the user (2026-10-06, all as recommended):*
- **A year's status on the Fiscal calendar card:** the year's own status AND each accounting book whose year-end is closed
  (from Finance's book-close cycles; a reopened one is not).
- **The logo embedded in letters**, as the seal and signature are (a `data:` image); the asset-terms email body may not
  show it in some webmail.
- **The free-text Logo URL retired** (F-55): letters read the uploaded logo, then `Tenant.LogoUrl` — which now applies
  whether or not a profile row exists; the field leaves the form and the API ignores it.
- **Seal, signature and logo: PNG or JPEG only**, refused on the server otherwise (new finding).
- **A milestone file is removed on `HR.Company.Write`**, as event files are; deleting the milestone stays Admin.
- *Defaults stated at the check:* a recurring milestone follows the closures' yearly rule; the upcoming read answers each
  milestone's next occurrence from today (UTC, Ghana's time); the range read answers one row per occurrence in the range;
  the DTO gains `occurrenceDate`, `nextOccurrence` and `yearsSince`. The fiscal year for a date is Finance's year covering
  it (its own `Year` label), else the start-month fallback, per date; HR reads Finance's calendar on `HR.Company.Read`.
- **Three slices:** 4a milestones (files, the yearly projection, the service's tenant scoping); 4b fiscal (Finance's
  calendar card, `IHrFiscalCalendar` for requisitions and the manpower form, HR's fiscal screens and routes retired, the
  suites); 4c company profile (the logo asset, the Logo URL retired, image-only uploads, the seal buttons gated).

*4a — what was built (2026-10-06): milestone files, and a yearly milestone's dates (D-3, C-40).*
- **No migration** (lane 0 made the table), no template, no topic.
- **`CompanyMilestoneRules`** (pure): the closures' yearly rule (29 February on the 28th in common years), the next
  occurrence, years since, a date required.
- **The service:** tenant-scoped queries (the repository's four custom reads deleted — every tenant's rows, the own date
  only, server-local "today"); create and edit answer with a re-read (F-46); the upcoming read at each milestone's next
  occurrence from today (UTC); the range read one row per occurrence, at most five years; the DTO gains `occurrenceDate`,
  `nextOccurrence`, `yearsSince` and `documentCount`; deleting a milestone takes its files.
- **Files:** `POST milestones/{id}/documents` (Write; the milestone resolved before a byte is stored), `GET
  milestones/{id}/documents` and `GET milestones/documents/{id}/download` (Read), `DELETE milestones/documents/{id}` (Write,
  the user's ruling) — through the gate (scanned, DMS-registered), the event files' category.
- **Screens:** the milestones page's "Files" from each row (`MilestoneFilesDialog`: upload, download, remove), "Next" (with
  the anniversary) and "Files" columns, the free text called "References", Delete only for Admin; the landing's
  milestones at their coming occurrence, "74th anniversary".
- **Demo data:** scenario 110's milestone dated from the founding (1952), so it counts as the 74th. UAT's own row is still
  dated 2026-10-18 (it reads as the first) — re-dating it is the user's call.

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–4a: **1027/1027 on two clean passes**, first time. 4a has **25 assertions**: no date
  refused; a past one-off with no next occurrence; a yearly one founded ten years ago at its next anniversary, the 10th;
  upcoming within 90 days the anniversary, not the past one-off nor the one 200 days away, within a year that one too; 29
  February on the 28th in common years and the 29th in 2028, counted 1–4; a range over two anniversaries, the 10th and
  11th; more than five years and a backwards range refused; an edit re-read; a file through the gate — its name, size,
  note and uploader, a clean scan, in the DMS, the same bytes back, listed and counted; a missing milestone refused
  before a byte is stored; a program file refused; removed by the HR desk; the milestone's delete refused the HR desk,
  done by Admin, its files gone with it.
- **Regression:** the round-4 net **212/212** (slice 3's milestone checks unchanged); recruitment **59/59**; the templates
  probe **42/42**; `run-lane-n` **109/115**, the six section-J failures of #40.
- **The API log:** no request answered 500; the milestone routes' refusals are the intended ones. The blocking watcher
  logged one 3.6 s parallel wait on the notification poll at the API's start, and nothing in the second pass.
- **After the runs:** no CSF milestone, file or booking is live; no company-schedule notice to a real login is live; every
  harness login is off; the R4D requisition's notices withdrawn twice (43, then 0). The API and the scanner stub are
  stopped.

*4b — what was built (2026-10-06): the fiscal calendar is Finance's (D-6, C-42, F-56).*
- **No migration**, no template, no topic. HR's `FiscalYear`/`FiscalPeriod` tables, entities, DTOs and mapping stay (a
  later migration drops them); UAT's one HR year ("FY 2026", scenario 110) is left in its table, read by nothing.
- **`IHrFiscalCalendar`** (`Services/HR/CompanySchedule/HrFiscalCalendar.cs`), in-process on Finance's
  `IFiscalPeriodService` (tenant from the token): the calendar — Finance's years in date order with their periods, the
  year's own status and lock, and each accounting book whose **latest** close cycle is not *Reopened* (book code and name,
  *Closing* or *Closed*, when); the year for a date — Finance's year covering it (compared as days: Finance's years end at
  23:59:59), else Finance's sequence continued, else `HrFiscalYear.For` from the policy month; the dates for a year by its number — Finance's year of that
  number, else Finance's sequence continued (below), else the policy month's year. Answers carry `source` (*Finance* /
  *Projected* / *Fallback*).
- **Routes:** `GET fiscal-calendar`, `GET fiscal-calendar/year?date=`, `GET fiscal-calendar/period?year=` (Read; 400 with
  the reason for no date or a year outside 1900–2200). **Retired:** the 16 `fiscal-years` / `periods` routes,
  `FiscalYearService`, `IFiscalYearRepository`/`IFiscalPeriodRepository` and their registrations.
- **Requisitions:** `StaffRequisitionService`'s budget check and budget link ask `YearForDateAsync`, not the policy month.
- **Screens:** one read-only **Fiscal calendar** page (`…/company-schedule/fiscal-calendar`; a card per year — status,
  Locked, each book "year-end closed"/"closing", the period grid; "Set up and closed in Finance" links to
  `/finance/fiscal-years`; the fallback month named) replaces the two Fiscal Years pages (deleted); the hub card and the
  setup navigation renamed. The manpower budget form, the new-budget page and *Plan from the establishment* take a
  year's period from Finance's year of that number first (`fiscalPeriodFor` with Finance's years; the hint says so). The
  policy screen's field reads "Fiscal year starts (when Finance has no year)".
- **Suites (dev-harness):** `run-final-review.mjs` block 4b; `run-slice3.mjs` loses its fiscal sections (21 assertions,
  44 → 23); `hr-w3-permissions/run-slice14-company.mjs` reads `fiscal-calendar` and drops the fiscal-year delete (2
  fewer); scenario 110 no longer seeds an HR year.
- **⚠ The block writes to Finance (the user's ruling, 2026-10-06):** one temporary `YearEndBookCloseCycle` on a Finance
  year for the default book, to prove the per-book display both ways. Finance's trigger
  `TR_YearEndBookCloseCycles_ImmutableEvidence` refuses every delete of close evidence, so the suite removes its rows in
  ONE transaction that switches the trigger off, deletes them by id, and switches it on (all rolled back on failure), and
  asserts the trigger is on afterwards. It also moves the policy month to July by SQL and back in a `finally`, to prove
  that nothing it answers moves while Finance has years.
- **A year Finance has not opened (the user's rulings, 2026-10-06, after the first build).** As first built, a date no
  Finance year covered fell to the policy month — independent of Finance: with a July month and Finance's calendar-year
  FY2026, 15 March 2027 was labelled 2026, the label Finance's Jan–Dec 2026 also carries; and a Finance that labels a year
  by the calendar year it ends in clashed even with matching months. **Ruled:** continue Finance's sequence by Finance's
  own rule for its next year (`FiscalPeriodService.CreateFiscalYearCoreAsync` refuses anything else) — numbered one
  higher, from the day after the last ends, twelve months each (`AddYears` from the origin, so the years tile); before
  the first year, the same backwards. Answers carry `source` *Projected*; the calendar names `nextYear`. **The policy
  month answers only while Finance has no fiscal year at all**, and the policy screen shows it read-only while Finance
  has one ("Finance's calendar decides: its years start on 1 January (next, FY2027)"). The manpower forms' `fiscalPeriodFor`
  mirrors the server (its `addYears` copies .NET's 29 February). `fiscal-calendar/year` refuses a date outside 1900–2200.

*Proof (UAT, API in Staging, after the second build):*
- `run-final-review.mjs` blocks 1a–4b: **1066/1066 on two clean passes**, first time. 4b has **39 assertions**: staff
  refused the calendar; Finance's own route refuses the HR desk (403 — why the read is in-process); Finance's two years and
  their 24 periods equal to SQL; the fallback month and the book-close count against SQL; the year for today, a year's
  last day (Finance's 23:59:59) and first day, a year by its number — Finance's; no date, a date past 2200 and a year of
  1800 refused with the reason; FY2027 named as Finance must open it; the day after FY2026 and a date in 2028 continued;
  2029 by number; the day before FY2025 and 2023 by number, backwards; the month moved to July — said by the calendar,
  and none of those six answers moved; a requisition's budget check for Finance's year and for a year continued; the month
  back; a temporary BASE-book close on FY2025 — on its card by the book's name, on no other year, read by Finance's own
  route as its own; reopened, gone; a second close under way, shown as the latest; the rows removed with Finance's trigger
  on again, the card as it was; HR's old list, current year and create all 404.
- **After each pass, by SQL:** Finance's trigger enabled, no close-cycle row, the policy month 1.
- **Regression:** the round-4 net **191/191** (29 + 33 + 66 + 23 + 40; slice 3 lost its 21 fiscal assertions);
  recruitment **59/59** (its requisitions submit through the budget check 4b changed); the templates probe **42/42**;
  `run-lane-n` **109/115**, the six section-J failures of #40. **Not run:** `hr-w3-permissions/run-slice14-company.mjs`
  (UAT has no `w3.*` logins; 4b proves the same two doors) and `hr-jobarch/run-slice7.mjs` / `run-r5.mjs` (they publish a
  ManpowerBudget definition of their own, and UAT has the real "Manpower Budget Approval" live — not run on UAT).
- **The API log:** no request answered 500; the ERR kinds are the known ones (payroll's foreign key #23 on minted
  fixtures, the participant and attendance unique-index races, notifications with no mail server, the sinks' bounces, one
  identity reconciliation). One EF "command error" on the notification service's own 90-day clean-up, with no exception
  reaching the service (it logged "Deleted 0") — not 4b's code.
- **The blocking watcher:** 78 waits in pass one, 15 in pass two (4a: one), all on `Notifications` (414k rows, 292k live):
  the suite's own unindexed count oracles and the notification service's poll and clean-up running parallel scans of
  3–6 s, notification inserts queued 2.4–3.6 s behind them. Concentrated in the first pass after the API's cold start;
  no request timed out. None touches the fiscal calendar. Lane 6's suite work: index-friendly oracles, or a
  `CreatedAt >= runSince` bound on each.
- **After the runs:** every harness login off; no company-schedule notice to a real login live; no RoomBooking approval
  running; the R4D requisition's notices withdrawn twice (43, then 0). The API and the scanner stub are stopped.

*4c — what was built (2026-10-06): the company profile — the logo, image-only uploads, the doors (D-9, C-49, C-50, F-55).*
- **No migration** (lane 0 added `CompanySealAssetKind.Logo = 3`), no template, no topic. One new ruling at the source
  check: **each image at most 2 MB** (every letter carries it embedded).
- **The logo** is the third versioned image, through the seal's doors (`POST seal-assets/Logo`, `…/Logo/retire`,
  `HR.Company.Admin`). `ICompanyProfileProvider.GetLogoAsync(tenant)` answers what letters carry: the logo in force as a
  `data:` image (`ICompanySealAssetService.GetCurrentAsDataUriForTenantAsync`, clean scans only), else `Tenant.LogoUrl` —
  now whether or not a profile row exists — else nothing. The six renderers (offer, probation, HR letter requests, asset
  terms, interview papers, test papers) call it; it is separate from the profile read so a templated email's name lookup
  never loads an image.
- **The free-text logo URL is retired** (F-55): gone from both profile DTOs and the mapping, so a `logoUrl` still sent is
  ignored and none is answered; the column stays, unread, until a later migration.
- **Images only** (`CompanySealAssetRules.RefuseImage`): a PNG or JPEG by extension, declared type AND first bytes (a
  renamed file is refused), at most 2 MB — checked in the controller before the gate stores a byte (the gate's type list
  is a tenant setting). An image kind that does not exist is a 400 with the reason; "nothing in force" names the image.
- **Screen:** the profile page loses its "Logo URL" field; the panel is "Logo, seal & signature" with a Company logo row;
  Upload/Replace/Withdraw only for `HR.Company.Admin` (until now the comment said so and nothing checked); the file picker
  takes PNG/JPEG, and the page refuses a wrong type or over 2 MB before sending.
- **Suites (dev-harness):** `run-final-review.mjs` block 4c; `hr-tierb-tail/run-slice1.mjs` asserts `logoUrl` ignored
  (and fixes its stale round-trip expectation for the two seal URLs, legacy read-only since probation lane 3a-ii).

*Proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–4c: **1094/1094 on two clean passes**, first time. 4c has **28 assertions**: staff
  refused the images; the HR desk refused upload and withdrawal, nothing stored; the profile answers no logo URL and
  ignores one sent (the column unchanged); a PDF, a text file named `.png`, a PNG over 2 MB, a PDF seal and a GIF
  signature refused with the reason, none stored, the seal and signature in force untouched; a kind that does not exist
  refused; with no logo uploaded a letter carries the tenant's own logo (UAT has a profile row — before 4c, none); a JPEG
  logo accepted, a PNG replacing it (the JPEG retired with the reason), scanned clean and in the DMS; the letter then
  carries the PNG embedded, byte for byte, not the tenant's; a second withdrawal "no logo in force"; withdrawn, the
  tenant's again; the tenant's logo restored, no logo at all; the run's logos hidden from the history, its letter
  request cancelled.
- **After each pass, by SQL:** `Tenants.LogoUrl` and `CompanyProfiles.LogoUrl` null as found; only UAT's own seal and
  signature visible (one current each); no pending CSF letter request.
- **Regression:** the round-4 net **191/191**; recruitment **59/59** (offer letters through the changed renderer); the
  templates probe **42/42**; `run-lane-n` **109/115** (section J, #40); `hr-tierb-tail/run-slice1.mjs` **68/68** (it
  writes and restores UAT's real profile). Not run: `hr-probation/run-lane3aii-seal.mjs` — it replaces and withdraws the
  tenant's seal in force (4c's block proves the same doors without touching it).
- **The API log:** no request answered 500; the ERR kinds are the known ones; no image read failed.
- **The blocking watcher:** 43 waits, 34 of 2 s or more (longest 6.7 s), concentrated in the five minutes after the
  API's start — all on `Notifications` (the notification service's poll, 90-day clean-up and topic read, inserts queued
  behind them), as at 4b; none on the profile or image tables.
- **After the runs:** every harness login off; no live company-schedule notice to a real login; no RoomBooking approval
  running; the R4D requisition's notices withdrawn twice (43, then 0). The API and the scanner stub are stopped.
- ✅ **Lane 4 is done (2026-10-06):** 4a, 4b and 4c built and proved; its screens await lane 5's walk.

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
- [ ] *✅ the card part in lane 2's 2e-2 ("Invitations and reminders": due-and-unreached, the sweep's days, the mail-server line, issued and reached in the toasts); the original window and "Moved on" remain.* Event detail: "Originally …" from the four original fields; "Moved on (timestamp) — reason"; the
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

**State:**

*Source check (2026-10-06, at HEAD f62293f53; two read-only surveys, the key points re-read by hand):*
- **Already done by earlier lanes:** the event edit's status select offers only what Update accepts (Scheduled, In
  progress, Postponed, Confirmed when no approval is pending, and the current status — lane 2); the rooms register's
  failure toast says "needs Company Admin" on a 403 (F-26); Delete on the event page, the bookings register and page, the
  rooms register and the milestones tab is Admin-only. `CompanyEventRules.EditableStatuses` is dead code.
- **F-27:** the guard is in HEAD (`fields.tsx:336-342`). The room edit page seeds its form before the locations load on a
  cold cache (`rooms/[id]/edit/page.tsx:35-37`), which is the case the guard covers — the browser re-test is the user's.
- **F-57:** `KEEP_OPEN` lacks `/hr/company-schedule/my-schedule` and `/hr/leave/calendar`; the test fails on both.
- **Removes (R4-6.6, F-20):** event tasks (`DELETE tasks/{id}`, Admin) and closures (`DELETE closures/{id}`, Admin) are
  offered to everyone; participants, attendance and attachments (Write routes) are offered without a check. On UAT no
  role holds `HR.Company.Read` without Write (HR, TenantAdmin, SuperAdmin hold both).
- **Event page (R4-6.1, F-21):** the four `Original*` fields reach the DTO and are rendered nowhere; "Rescheduled" prints
  the press timestamp as if it were the new date.
- **Diaries (F-23):** both pages bucket an entry on its start day only; the team page drops one that began before the
  range. The server splits events per day (lane 2, R4-10A.2); leave, travel, holidays and training come as one spanning
  entry, closures end at midnight of their last day, and the training source picks an arbitrary session
  (`FirstOrDefault` without an order).
- **R4-10A.3:** no `incompleteSources`; each source's failure is logged and swallowed — cancellation too.
- **Team schedule (R4-10B.1/3/4):** `team-schedule/{unitId}` is `HR.Company.Write` only, always the subtree; the unit list
  is every unit; no "schedule for this unit"; the event form reads no query. UAT: 39 of 451 active units have a head.
- **R4-3.1:** the landing's one dashboard read has no error branch — on a 403 all four cards print their empty sentence.
- **C-16:** the three site pickers list the whole tree (`GET /Location`). UAT: Country (1) and Region (2) levels do not
  allow employee assignment and hold nobody; Site / Office (8) allows it and holds all 4,998 placed staff. Matching is by
  exact location (`HrAudienceResolver`, `HrClosureCalendar`).

*Rulings by the user (2026-10-06, all as recommended):*
- **C-16 confirmed:** the closure, event and room site pickers offer only active locations whose level allows staff to be
  placed there; a record keeps showing its current location even if it no longer qualifies.
- **Team schedule:** unit heads only (the head of a unit sees it and every unit beneath it), beside the HR desk; not line
  managers.
- **The landing without the permission:** the cards hidden; one line saying the summary is for the HR desk, with links
  to My Schedule and — for a unit head — Team Schedule.
- **F-57:** the Leave Calendar joins `KEEP_OPEN` too (its own reason); its default "Everyone" view refusing staff is
  logged in the leave workstream's ledger, not fixed here.
- **Two slices:** 5a — removes, the event page's dates, the landing, the site pickers, the sidebar test; 5b — the diaries
  (every day an entry covers, the failed-source banner, the training source's session) and the team schedule (unit
  heads, the readable units, sub-units and direct members, "Schedule for this unit").

*5a — what was built (2026-10-06): screens only — no server change, no migration.*
- **Removes (R4-6.6, F-20):** an event task's Remove only for `HR.Company.Admin` (its route's tier); a closure's likewise;
  participants, the register and files added, changed and removed only with `HR.Company.Write` (their routes'), the
  upload box too. Every remove in the module now matches its route.
- **The event page (R4-6.1, F-21):** "Originally" — the window before the first move, from the four `Original*` fields
  ("Not recorded" for a move before they were kept) — and "Moved on (date) — reason" in place of "Rescheduled", which
  printed the press as if it were a date.
- **The landing (R4-3.1, the user's ruling):** without `HR.Company.Read` the dashboard is not asked for; one card says the
  summary is the HR desk's and links to My Schedule (5b adds Team Schedule for a unit head). A failed read says so and
  shows no cards — never "Nothing scheduled" on a failure.
- **The site pickers (C-16, the user's ruling):** `useSiteOptions` (`siteOptions.ts`) — active locations at levels that
  allow employee assignment (from `GET api/LocationLevel`), the record's own location always kept, the whole tree when a
  tenant marks no level; on the closure, event and room forms.
- **The sidebar test (F-57):** `KEEP_OPEN` gains My Schedule and the Leave Calendar, each with its reason; the committed
  test failed on exactly those two, and passes (4/4). The Leave Calendar's "Everyone" default refusing staff is **L-97**
  in the leave guide's § 23 ledger.
- **Already done before 5a, recorded here:** the status select (lane 2), the rooms toast (F-26), the Admin-only deletes on
  the event page, bookings, rooms and milestones.
- **Suites:** `run-final-review.mjs` block 5a — the screens' premises on the server (staff refused the dashboard; the
  desk refused a task's and a closure's delete, Admin deleting the task; the levels readable by the desk; nobody placed
  where the picker hides). The screens themselves are the browser walk's:

*5a — proof (UAT, the 4c build — no server change, so no build was needed):*
- `run-final-review.mjs` blocks 1a–5a: **1101/1101 on two clean passes**, first time; 5a has **7 assertions**. The run now
  takes over ten minutes (run it in the background).
- The sidebar test: the committed version fails on exactly My Schedule and the Leave Calendar; the new one passes, 4/4.
  The scoped type-check and lint pass.
- **Regression:** the round-4 net **191/191**, recruitment **59/59**, the templates probe **42/42**, `run-lane-n`
  **109/115** (section J, #40). No request answered 500; the ERR kinds are the known ones.
- **The blocking watcher:** 84 waits, 60 of 2 s or more (longest 7.0 s), all the notification service's poll, 90-day
  clean-up and inserts and the suite's own `Notifications` counts — as at 4b and 4c; the table grows with every run.
- **After the runs:** every harness login off; no live company-schedule notice to a real login; no RoomBooking approval
  running; the R4D requisition's notices withdrawn. The API and the scanner stub are stopped.

*5b — what was built (2026-10-06): the diaries and the team schedule (R4-10A.1/3, R4-10B.1–4, F-23). No migration.*
- **Who reads a team schedule (R4-10B.3, the user's ruling):** `team-schedule/{unitId}` drops its Write policy — the HR
  desk (`HR.Company.Write`, checked per request) reads any unit, a unit's head (`HeadEmployeeId`, of the unit or of any
  unit above it, active units only) reads theirs; anyone else is refused with the reason. New `GET
  team-schedule/units`: every active unit for the desk, else the units the caller heads and everything beneath them
  (with a readable path, and which they head themselves). The tree is read once per request (451 units on UAT).
- **What a diary carries (R4-10A.3, R4-10B.4):** `incompleteSources` on both diaries — each source that failed, by name
  (a cancelled request is now thrown, not counted as a failure); each member's own unit (id and name), and the team's
  unit name.
- **The training source (F-23):** every timed session of a course in the window, in date order (it reported one, from an
  unordered read); without a timed session, the course's hours on each of its days in the window; without hours, the
  course's own days — never the whole window. One day, as the clash check asks, answers as before.
- **Screens:** both diaries draw an entry on every day of the range it covers, one begun before the range from its first
  day (`diaryDays.ts`), My Schedule naming the run ("7 Oct to 11 Oct"); `IncompleteDiaryBanner` on both. The team page
  lists only the units the caller may read (a head opens on theirs; somebody who heads nothing is told who the page is
  for and pointed to My Schedule), a sub-unit filter and "Direct members only" with "n of m people", each member's unit
  under their name; for the desk, "Schedule for this unit" and each day's heading open the event form pre-filled
  (`events/new?scope=Department&unit=&date=` — the form reads it). The sidebar entry opens to everybody and joins
  `KEEP_OPEN`; the landing's "for the HR desk" card links Team Schedule for a head.
- **Suites:** `run-final-review.mjs` block 5b (fixture A made head of the run's own unit, which gains a child; one
  temporary Approved nomination for H on a real course with several timed sessions — UAT's 33 nominations are all
  Submitted, which the diary does not show).

*5b — proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–5b: **1116/1116 on two clean passes**, first time; 5b has **15 assertions**: nobody's
  head offered no unit and refused with the reason; the diary's `incompleteSources` empty; the desk offered every
  active unit (SQL); A, made head of the run's unit, offered it and its new child — nothing else — marked as theirs by
  path; its schedule every active member of the subtree (SQL), each with their unit, the team named, nothing
  incomplete; the child read (the ancestor rule); the unit above refused; the head cleared, refused again; an Approved
  nomination showing every timed session of its course in the window, in order (SQL), then removed.
- **After the runs, by SQL:** no CSF nomination (33, all UAT's own), no CSF unit live.
- **Regression:** the round-4 net **191/191**; recruitment **59/59** (its interview clash checks read the training
  source); the templates probe **42/42**; `run-lane-n` **109/115** (#40); the sidebar test **4/4**. No request answered
  500; the ERR kinds are the known ones; no diary source failed.
- **The blocking watcher:** 84 waits, 56 of 2 s or more (longest 7.9 s), all on `Notifications` — none on the unit tree,
  the employees or training.
- **After the runs:** every harness login off; no live company-schedule notice to a real login; no RoomBooking approval
  running; the R4D requisition's notices withdrawn. The API and the scanner stub are stopped.
- ✅ **Lane 5's code is done (2026-10-06): 5a and 5b built and proved.** What remains is the browser walk below.

*5a — the browser walk (the user's; frontend `npm run dev` against UAT):*
1. **F-27:** a room's edit page opened cold (a fresh tab) shows its site; Save keeps it. An event edit with "Not tied to a
   site" survives a cold load and a Save.
2. As **hr.head**: an event's Tasks rows offer no Remove; Participants, Attendance and Files do. The closures list offers
   no Remove. As an **admin**: Remove on both.
3. A **moved event**: "Originally …" with its first window, and "Moved on (date) — reason".
4. The **landing** as a plain employee: one card, "for the HR desk", linking My Schedule — no "Nothing scheduled". As
   hr.head: the four cards.
5. The **site picker** on the closure, event and room forms: the eight sites only (no Ghana, no regions). A record saved
   on a region (if any) still shows it when edited.

*5b — the browser walk (the user's):*
6. **My Schedule** with a week's leave or a multi-day closure in range: it sits under every day it covers, each saying "…
   to …".
7. **Team Schedule as hr.head:** every unit offered; a sub-unit filter and "Direct members only" change the "n of m
   people"; "Schedule for this unit" (and a day's heading) open the new-event form with Department, the unit and the
   day filled in.
8. **Team Schedule as a unit head who is not on the HR desk** (UAT: 39 units carry a head — log in as one): their unit
   opens by itself, only their subtree is offered, no "Schedule for this unit". **As a plain employee:** the page says
   who it is for and links My Schedule; the landing's card links no Team Schedule.

### Lane 7 — The company calendar (D-7, D-8, D-13, D-16; C-9, C-26, C-35, R4-6.4)

- [ ] `GET CompanySchedule/calendar?from&to` (any signed-in internal user; 62-day cap) returning
      `CalendarEntryDto` rows: kind (Event, Closure, Milestone, Holiday, RoomBooking, Mine), label,
      start, end, all-day, colour key, reference, link target. `HR.Company.Read` holders see every
      event, closure, calendar milestone, holiday and booking; everybody else sees what the audience
      rules allow them (lane 2, D-16) and the closures that cover them (lane 1), plus calendar milestones
      and holidays, with their own diary (leave, travel, interviews, training, bookings) overlaid as
      Mine. **Closures and holidays appear once**, in the company layer, not again in Mine.
      *Handed over from lane 1 (2026-10-05), whose other readers already do both:*
      - holidays come from `IHrWorkingDayCalculator.GetHolidaysAsync`: the default calendar, active,
        mandatory and substitute days, each named. This is R4-10A.4's calendar half.
      - closures come through `IHrClosureCalendar`, and a closure that recurs every year is drawn on
        every later year. This is C-38's calendar half.
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

**State:**

*Source check (2026-10-06, at HEAD b47322fb2; two read-only surveys, the key points re-read by hand):*
- **No calendar read exists** in the module (only the fiscal calendar). HR's nearest reads — `dashboard`, `events/range`
  (its summary DTO has no end time, visibility, scope or calendar flag), `closures/range` (original dates, not the
  yearly repeats), `bookings/range` — are all `HR.Company.Read`.
- **Who sees an event is computed, not stored:** `CompanyEventRules.CalendarAudienceOf` (the audience rule when
  `ShowOnCompanyCalendar`; none for Private / Confidential), resolved with `IncludedAmongForTenantAsync`. The diary's event
  source adds guests (any visibility, not declined, not cancelled) and the organiser.
- **The answer door is the HR desk's:** `POST events/{id}/participants/respond` (Write) — answers only, the guest on the
  event, a series by `SeriesScope`; it does NOT check the reply deadline, `RequiresRsvp`, awaiting approval, `NotSent`
  or a started date, and raises no notice. No self-service door; no staff read of one event. `MeetingPassword` is on the
  event DTO and kept out of emails, `.ics` files and announcements.
- **Notices:** 20 in-app topics, none for "a guest answered"; 17 company-schedule email templates. The guest link opens
  `/hr/company-schedule/my-schedule?from=&event=`, and the page ignores `event=`.
- **Milestones:** `ShowOnCalendar` is stored and read by nothing (the range read ignores it). Yearly ones expand with
  `CompanyMilestoneRules.OccurrencesIn`. **Closures:** `IHrClosureCalendar.GetClosuresAsync` (rows, recurring included) +
  `BusinessClosureRules.OccurrencesIn`, `CoverageAsync` for who they cover. **Holidays:** `GetHolidaysAsync` — one row
  per day, named, substitute days flagged.
- **Rooms:** the staff door's busy read (`me/rooms/busy`, 31 days) carries only room, time and whether it is theirs.
- **Duplicates the calendar must avoid:** the diary's Closure and Holiday kinds, its Event kind (an audience event is also
  on the company layer) and the caller's own bookings.
- **Screens:** no calendar component anywhere (no library; `date-fns` and `react-day-picker`, a picker, only).
  `LeaveCalendar.tsx` says it draws bands broken at week boundaries; it draws a chip per day. No `/me/calendar`, no
  portal entry, no sidebar "Company Calendar"; no Accept / Decline UI for an invitee.

*Rulings by the user (2026-10-06, all as recommended):*
- **The organiser is told of each answer in the app only** — one notice per answer (who, which answer, the comment; a
  series answer lists the dates); no email, no new template.
- **The month view draws a several-day entry as one band** across its days, broken at each week's end (built here — the
  leave calendar has no bands to copy).
- **A guest's notices open the staff event page**, `/me/calendar/events/{id}`; organisers and the HR desk keep the full
  page.
- **An invitee may answer** once the invitation has been sent (never while the event awaits approval), until the reply
  deadline, or — with none — until the event starts; never on a cancelled or completed event. The HR desk's door is
  unchanged.
- **Two slices:** 7a — the server (the calendar read, the staff event view, the reply door, the organiser's notice, the
  guest link, `ShowOnCalendar` for milestones); 7b — the screens (the calendar for HR and in the portal, month and week,
  filters, legend, the room view, the staff event page with its answer, booking from the calendar, the landing's links,
  the sidebar and portal entries).

*7a — what was built (2026-10-06): the server. No migration; one in-app topic (21), no email template.*
- **`GET calendar?from&to&roomId`** (any internal user; at most sixty days — the diary's own limit, not the plan's 62) —
  `ICompanyCalendarService` (`Services/HR/CompanySchedule/CompanyCalendarService.cs`). The desk (`HR.Company.Read`, checked
  per request): every live event (awaiting approval marked), every closure occurrence, every booking. Anyone else: events
  they organise, are invited to (once the invitation has gone; a declined one too, so they can change it), or are the
  calendar audience of (never Private / Confidential, never while awaiting approval) — each audience rule resolved once;
  the closures covering them; their own bookings. Everybody: the calendar milestones (`ShowOnCalendar`, a ghost until now;
  every anniversary in range), the holidays (one band per run) and their own leave, travel, interview panels and
  training as Mine — closures, holidays, events and bookings never repeated in Mine. Each event entry carries the
  caller's invitation, answer and whether they may answer. **The room view:** `roomId` narrows it — the desk sees its
  bookings in full, anyone else "Room: booked" with no purpose, number or id unless it is theirs (lane 3c).
- **`GET calendar/events/{id}`** — the event as staff see it: for its organiser, a guest, its audience or the desk;
  anyone else 404. What, when, where, the link, the rooms booked for it, the organiser, who it is for, the caller's own
  invitation and whether they may answer; the password only to a guest or the organiser; no budget, no other guest.
- **`POST events/{id}/participants/{participantId}/reply`** — the invitee's own answer: their row or 404;
  `CompanyEventRules.RefuseSelfAnswer` (422 with the reason); on a series every date still answerable; saved by
  tracking; the organiser told in the app (`CompanySchedule.Answered.Organiser`, never of their own answer).
- **The guest link** (`CompanyScheduleNotices.GuestLink`) now opens `/me/calendar/events/{id}` — every guest notice. ⚠ That
  page is 7b's; between the two commits a guest's link opens a page not yet built.
- **Suites:** `run-final-review.mjs` block 7a; 2e-1's guest-link pin (the portal page) and topic count (21) moved.

*7a — proof (UAT, API in Staging):*
- `run-final-review.mjs` blocks 1a–7a: **1147/1147 on two clean passes.** The first pass failed one assertion of the
  suite's own: the off-calendar event and the off-calendar milestone share their words, and the desk (rightly) sees the
  event — the milestone counts now filter by kind; the next two passes were clean. 7a has **31 assertions**: staff see
  their invitation, the whole-company event and their series dates, never the private or off-calendar ones; their
  invitation on the entry (theirs, Sent, answerable, the portal link); the company-wide closure once, not the other
  site's; the calendar milestone only; nobody else's booking, and the room as "booked" with nothing of it; no Mine entry
  repeating the company layer; sixty-one days refused; the desk sees every event (HR links), both closures, the calendar
  milestone only, the booking in full; the guest's view with link and password and their invitation, the audience's
  without, a private event 404 to staff and open to the desk; a non-answer 422, somebody else's invitation 404, their
  own accepted with the comment and the organiser told once; a passed reply-by date, an invitation not sent and a begun
  event each 422 with the reason; a series answered for its three dates, the organiser told once.
- **Regression:** the round-4 net **191/191**, recruitment **59/59**, the probe **42/42**, `run-lane-n` **109/115** (#40).
  No request answered 500; the ERR kinds are the known ones.
- **The blocking watcher:** 100 waits over three passes, 67 of 2 s or more, none on the calendar's tables. The longest yet,
  **11.1 s, is the notification service's own poll** (`SELECT TOP … maxRetryAttempts` over `Notifications`, a parallel
  scan) — the table grows with every run; lane 6 takes the suite's share, and the poll's own index is Platform's.
- **After the runs:** every harness login off; no live company-schedule notice to a real login; no RoomBooking approval
  running; the R4D requisition's notices withdrawn. The API and the scanner stub are stopped.

*7b — what was built (2026-10-06): the screens — no server change, no migration.*
- **`CompanyCalendar.tsx`**, one component for both doors: month (six Monday-start weeks) and week; a several-day entry
  ONE band across its days, broken at each week's end with ◂ ▸ (the user's ruling), packed into lanes, four to a week
  row and "+n more" per day (which opens the week); the week view with the all-day and several-day bands on top and the
  timed entries listed under their day; Today, previous and next; kind chips that are both the filter and the legend,
  and a dashed swatch for "waiting for your answer". The layout is `calendarLayout.ts` with its own unit tests (7).
- **Clicking an entry** opens its card — what and when, its badges (awaiting approval, you organise it), the caller's own
  invitation with Accept / "May attend" / Decline, a note, and on a series which dates (`InvitationAnswer.tsx`, shared
  with the event page) — and "Open" for its page.
- **The room view:** a room picker (the desk's room register, or the rooms staff may book) narrows the calendar to that
  room; "Book this room" opens the booking form for the day — the desk's form for the desk, the portal's otherwise; both
  forms now take `?date=&room=` (09:00–10:00, searched at once).
- **Pages:** `/hr/company-schedule/calendar` (sidebar "Company Calendar", no permission, in `KEEP_OPEN`);
  `/me/calendar` (portal Company menu, "Calendar"); `/me/calendar/events/[id]` — the staff event page the guest's notice
  opens: when, where, rooms, joining (and the password, when the server gives it), organiser, who it is for, the reply-by
  date, and "Your invitation" with the answer; "Open the HR page" for the desk; a lookup miss says so.
- **The landing:** a "Company calendar" card first, "See the calendar" on each summary card, and the Company Calendar
  link in the "for the HR desk" card.
- **No new server assertion:** 7a's block proves every read and write these screens make.

*7b — proof (UAT, the 7a build — no server change, so no build was needed):*
- The band layout's unit tests **7/7** (one band across days; broken at the week's end and carried in from the week
  before; out of the week left out; lanes shared and reused; all-day above a meeting; an end before the start read as the
  start). The sidebar test **4/4** with the Company Calendar in `KEEP_OPEN`. The scoped type-check and lint pass.
- `run-final-review.mjs` **1147/1147 on two clean passes**; the round-4 net **191/191**, recruitment **59/59**, the probe
  **42/42**, `run-lane-n` **109/115** (#40). No request answered 500; the ERR kinds are the known ones.
- **The blocking watcher:** 111 waits, 82 of 2 s or more (longest 7.7 s), all on `Notifications`, as before.
- **After the runs:** every harness login off; no live company-schedule notice to a real login; no RoomBooking approval
  running. The API and the scanner stub are stopped.
- ✅ **Lane 7's code is done (2026-10-06): 7a and 7b built and proved.** Its browser walk (items 9–12) is the user's.

*7b — the browser walk (the user's):*
9. **As a member of staff, `/me/calendar`:** an invitation drawn dashed; click it — Accept with a note; it turns solid,
   and the organiser has the notice. A several-day closure or a week's leave as one band, broken with ▸ at the week's
   end; "+n more" opens the week.
10. **The notice:** a guest's "You are invited" opens `/me/calendar/events/{id}` and answers from there; an event not for
    them says it was not found.
11. **The room view:** pick a room — others' bookings read "booked"; "Book this room" opens the portal form with the day
    and the room filled in. As hr.head, the desk's form instead, and every booking in full.
12. **As hr.head, the HR menu's Company Calendar:** every event (a private one too), both kinds of closure, each opening its
    HR page; the landing's "See the calendar" links.

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
- [ ] *(2h: the event files are uploaded as files, replacing a path-only row of the same name; scenario 170 saves
      each seeded drill once more through the SHE API so its event is made.)* Scenario 110: event files uploaded as files (F-54), no HR fiscal year, `hr.head` books the
      second room (R4-2.3); the D suites get a tidy step (R4-2.1).
- [ ] The guide: § 21 "Final review" closing every finding this plan closes; the eight rules
      rewritten; **every changed chapter rewritten**, not only § 21 (events, bookings, rooms, closures,
      milestones, the diaries, the company profile; the fiscal chapters replaced by the fiscal calendar
      card; a calendar chapter); Appendix B's permission map gains the new doors; Appendix A's route
      list gains the calendar and loses the fiscal screens.
- [ ] `docs/HR/README.md`; `docs/HR/programme/HR-FINISH-PLAN.md` (D-02 closed by D-8; F-27 recorded as
      HR-wide); `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` (attendance A-92); the payroll hand-off
      (closures beside holidays, D-15c);
      the configuration register (new endpoints and topics); the demo runbook's closure aside (in
      `dev-harness/`, outside the repo); memory `hr-company-schedule-guide-findings`.
- [x] *✅ Built 2026-10-05, after 2f-2b, and proved at 2g-1's run: two passes — by `EntityId` in one list, and by
      link only over rows created since the run began. 5 s of work for 132 events; the blocking watcher logged
      nothing.*
      **(Found at 2f, 2026-10-05) The suite's notice withdrawal grows with the run.** Its clean-up withdraws the
      run's notices with one `UPDATE … WHERE EntityId = … OR ActionUrl LIKE '%…%'` over every event the run made. That
      scans `Notifications`, and holds the notification service's own clean-up behind it: 36 s at 85 events (2f-1),
      42 s at 100 (2f-2a), 50 s at 124 (2f-2b). **At 124 the notification service's poll timed out (30 s) behind it,
      once per pass, logged as an ERR** (`ProcessPendingNotificationsAsync`; it polls again, nothing lost). Withdraw by
      `EntityId` (a seek) first, and match the `ActionUrl` only for notices that name the event in their link alone
      (the engine's approval notices), in batches of events.

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
| C-8, C-30, C-31, C-33, C-36 | Lane 3 (✅ 3a: C-30, C-31 guards, C-33, C-36 per D-18; the approval engine, C-8 and the bookers told in 3b–3c) | C-36 per D-18 |
| C-28 | ✅ Lanes 2, 3 (3a: rooms and bookings) | the availability read and every other list read (F-30) |
| C-9 | Lane 7 | |
| C-10, C-11, C-12, C-13, C-25 | Lane 2 (D-9) | search, paging, date filter, CSV export, one dashboard read |
| C-15 | Lane 2 (D-9) | the clash rule and the warnings |
| C-16 | Lane 5 | reopened by the review; recommendation to confirm |
| C-21, C-22 | Lane 2 (D-9) | attendance row removable on Write; participant editable and removable on Write; the reply door (D-8) covers the invitee's side |
| C-26, C-35 | Lane 7 | the calendar's room filter is the room view |
| C-32 | ✅ Lane 3, slice 3a (D-9) | |
| C-37, C-38, C-39 | Lane 1 | C-38 recurs annually (D-9) |
| C-40 | Lane 4 | |
| C-41 | **Closed by decision D-17** (2026-10-01) | not built: a company milestone is not one employee's award; the certificate is a milestone document |
| C-42…C-48 | Lane 4 | closed by retirement (D-6) |
| C-49, C-50 | Lane 4 (D-9) | buttons hidden; the logo a versioned asset; the free-text URL retired (F-55) |
| C-51 | ✅ Lane 2, slice 2h (D-9) | the drill creates its event, for everyone, never blocked; UAT's one drill given its event |
| R4-2.1, R4-2.2, R4-2.3, R4-2.4 | Lane 6 (R4-2.1 ✅ in lane 3a: both D suites tidy their rooms, bookings and — this module's — events; R4-2.3: scenario 110's booking by `hr.head`) | R4-2.4 is the demo database, not the code |
| R4-3.1 | Lane 5 | **live** — the first draft had it as kept |
| R4-5.1, R4-6.1, R4-6.2, R4-6.4…R4-6.7, R4-7.1 | Lanes 2 and 5 | |
| R4-6.3 | ✅ Lane 2, slice 2e-2 (deliveries counted, and the card built with them); lane 5 walks it | the card is "Invitations and reminders" |
| R4-9.1 | ✅ Lane 3, slice 3a | |
| R4-10A.1, R4-10B.1…R4-10B.4, R4-10A.3 | Lane 5 | |
| R4-10A.2 | **Lane 2** (backend) + Lane 5 (screens) | the first draft had it in lane 5 only |
| R4-10A.4 | Lane 1 | closures and holidays both |
| R4-10A.5 | Lane 6 | the browser walk |
| R4-12.1 | ✅ Lane 3, slice 3a | a typed code is checked against deleted rooms too |
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

**A second migration, lane 2e-3 (D-14, F-34), ✅ applied to UAT on 2026-10-05:**
`20261005143701_CompanyScheduleInvitesAndTaskChase`. It adds `CompanyEvents.CalendarSequence` (int, default 0) and
`EventTasks.OverdueChasedAt`. Guarded SQL, no THROW, so no preflight pin. Proved 8/8 on a throwaway database, then
applied with the built DLL after a COPY_ONLY backup. History 143 → 144. Details in lane 2's 2e-3 section.

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
- **2026-10-04, later** — **Lane 1 source-checked (§ 1c).** No approved leave is re-charged today, by a
  closure or by a holiday. The re-charge parts already exist in reschedule and recall. Payroll reads
  none of HR's days off, and its hand-off already records holidays the same way. The audience
  resolver is bound to the signed-in user's tenant. The user settled **D-15a** (re-charge, taken
  leave included, finished years by hand), **D-15b** (holidays too), **D-15c** (pay flag into the
  payroll hand-off) and **L1-1** (announce on HR's click), all as recommended. Six of § 1b's decisions
  remain pending. Next: build lane 1.
- **2026-10-05** — **Lane 1, slice 1a built and proved** (closure rules and scope; lane 1 State).
  `run-final-review.mjs` scored 79/79 on two clean passes, and the round-4 net 201/201. The first pass
  corrected the suite, not the code: refusals here are 422. It also showed that a login which has
  signed in cannot be deleted, so it is switched off instead. 16 harness logins left active on UAT
  (1 of this suite's, 15 of the round-4 suites') were switched off. Next: 1b, leave counts closures.
- **2026-10-05, later** — **Lane 1, slice 1b built and proved** (leave counts closures; lane 1 State).
  `run-final-review.mjs` scored 93/93 on two clean passes, and the round-4 net 201/201. `run-slice2` needed
  a third run: a 30-second SQL timeout under memory pressure, not 1b. Found: a closure that predates 1c
  never triggers the re-charge, so 1c gains a one-time pass. Not run: the leave harness, pending the
  user. Next: 1c, the re-charge routine.
- **2026-10-05, later** — **Lane 1, slice 1c built and proved** (the re-charge; lane 1 State).
  `run-final-review.mjs` scored 117/117 on two clean passes. The round-4 net was 201/201 and leave
  slices 1/2/5 72/75 · 31/31 · 51/51, the 3 being the documented UAT gap. The one-time pass gained a
  dry run. Holidays are hooked where the screen writes them, `HolidayCalendarService`. The real pass
  showed no stale count on UAT. *Repaired:* the `### Lane 2 — Events` heading, which 1a's State edit
  had swallowed (missing since `d206db230`). Next: 1d, announce on HR's click, and the payroll reader
  and hand-off.
- **2026-10-05, later** — **Lane 1, slice 1d built and proved** (announce on HR's click, payroll's read;
  lane 1 State). `run-final-review.mjs` scored 134/134 on two clean passes, and the round-4 net
  201/201.
  - **Not proved: the real publish.** The fixture unit is harness residue that reaches 44 people, so
    the suite's guard skipped it.
  - **A-92 is kept in the attendance guide's ledger** and marked half closed. The plan had said the
    cross-module register, which is for other teams' defects.
  - The log showed two errors from master's identity reconciliation sweep, not caused by 1d.

  Next: 1e, the closures screen.
- **2026-10-05, later** — **Lane 1 done: slice 1e built and proved** (the closures screen; lane 1 State).
  The user's two decisions:
  - prove the real publish on a unit of the suite's own;
  - log the reconciliation errors. They are cross-module **#39**: the sweep fails on `property.manager`
    in every run, because that employee's manager has two demo logins.

  Results:
  - `run-final-review.mjs` scored 156/156 on two clean passes. The real announcement reached the
    run's one login and nobody else.
  - The round-4 net scored 201/201 twice, for the lane close.
  - The screen type-checks clean in a new scoped config.

  The calendar halves of R4-10A.4 and C-38 are handed to lane 7 in writing.

  Next: lane 2. Its source check comes first, and it waits on D-10, D-11, D-14 and D-16.
- **2026-10-05, later** — **Lane 2 source-checked** (§ 1c). Three read-only passes, with the key
  claims checked by hand. No lane 2 finding is fixed.
  - **Corrections:**
    - F-10: another tenant's id is stored silently, because the DbContext's tenant filter is inert;
    - F-33 is wider than written;
    - F-38's "answers reset" is the target, not today;
    - there is no `EnsureExistsAsync` to reuse;
    - the drill has no "DRL-" numbers, and its plan name can overflow the event's.
  - **New: F-58**, the event page never shows the original dates, because `ToDetailDto` drops them.
  - **Facts the four decisions turn on:**
    - `CreatedBy` is never stamped on events;
    - neither events nor bookings have a submit step;
    - interviews already build an `.ics`, but SEQUENCE has no column;
    - unit heads are plentiful on UAT only because the demo seeder appoints them.

  D-10, D-11, D-14 and D-16 are put to the user with refined recommendations. Proposed slices in
  lane 2's State.
- **2026-10-05, later** — **D-10, D-11, D-14, D-16 settled** by the user, all as refined in § 1c.
- **2026-10-05, later** — **Lane 2, slice 2a built and proved** (windows, references, organiser,
  diary, lifecycle; lane 2 State). `run-final-review.mjs` scored 250/250 on two clean passes, with
  94 checks in 2a. The round-4 net was 207/207 and the recruitment clash suite 58/58. No request
  answered 500.
  - **Suites updated for the new rules:** `run-slice0`'s HR approved its own event; `run-slice2`
    edited times without a reason, and completed an event before it began.
  - **The demo pack's scenario 110** would have lost its board meeting on rebuild (replies asked, no
    deadline); it now sends one.

  Next: 2b, approval on the workflow engine.
- **2026-10-05, later** — **Lane 2, slice 2b built and proved** (approval on the engine; lane 2
  State). `run-final-review.mjs` scored 275/275 on two clean passes; the round-4 net 207/207.
  - **Rejecting cancels the event:** the user's ruling is to be confirmed.
  - **A harness defect:** the workflow definitions listing is paged, which made a guard and a retire
    vacuous. Fixed; other HR harnesses share the blind spot.
  - **UAT:** the `CompanyEvent` entity type is registered. The user then asked for the definition: it is
    published (by a tool in the seeder's shape, because the seeder skips it on UAT). The suites run
    against it, 270/270 twice and the round-4 net 208/208.

  Next: 2c, the audience.
- **2026-10-05, later** — **Rejecting an event cancels it: confirmed by the user** ("yes, keep that").
- **2026-10-05, later** — **Lane 2, slice 2c built and proved**: who an event is for (D-16), the
  diaries and the intranet (lane 2 State). `run-final-review.mjs` scored 301/301 on two clean passes,
  with 31 checks in 2c. The round-4 net was 208/208, and orientation's triggers suite 184/184. No
  request answered 500.
  - **Management is one rule across HR:** the audience resolver has it, and orientation's own copy
    now reads it.
  - **The intranet:** announcing is HR's click from the event page, as for closures. It is proved on
    the run's own one-person unit.
  - **No real staff told:** every run notice to `hr.head` and `hr.officer` is withdrawn.

  Next: 2d, participants, attendance and tasks.
- **2026-10-05, later** — **Lane 2, slice 2d built and proved**: guests, the register and tasks (lane 2
  State). `run-final-review.mjs` scored 387/387 on two clean passes, with 86 checks in 2d. The round-4
  net was 205/205, and the recruitment clash suite 58/58.
  - **F-1, F-11, F-12, F-35, C-21 and C-22 closed;** F-30 and F-46 closed for these collections.
  - **F-45's race is answered:** simultaneous rows are refused by the unique index as a 422, proved
    under load.
  - **Pass one's clean-up** hit a 30 s database stall caused by the shared notification service's bulk
    clean-up (one 500). Recorded, not registered.
  - **`run-slice2`** made UAT's 12 "checked out before checked in" rows; it now asserts the refusal.
  - **Handed to 2e:** telling a removed guest, and an assignee told and chased (F-34).

  Next: 2e, the notices.
- **2026-10-05, later** — **2e split in three, and the overdue chase sent once**, both by the user
  ("go with both"). The chase's stamp column joins the 2e-3 migration, so the chase moves to 2e-3.
- **2026-10-05, later** — **Lane 2, slice 2e-1 built and proved**: who is told (lane 2 State).
  `run-final-review.mjs` scored 429/429 on two clean passes, with 42 checks in 2e-1. The round-4 net
  was 205/205 and the recruitment clash suite 58/58.
  - **In the app, through `CompanyScheduleNotices`;** five new catalogue emails; F-33 in full; the
    organiser reminded and told of the decision.
  - **The user asked whether the new emails are on the templates screen:** yes. They are listed,
    described, previewed, saved, test-sent and reset, proved on UAT (probe 21/21). `run-lane-n` scored
    109/115: section J's careers activation email, untouched by this slice.
  - **#36 again:** pass two's clean-up was written back by the dispatcher. The clean-up now
    withdraws twice.
  - **Found, not ours, logged as #40–#42:** since master `de8ad4fb2` an email sent with nobody
    signed in finds no mail server (section J's cause, and every background send — our hourly sweep
    too); the new sign-up's SMS code, sent to the templates suite's random numbers (all failed on
    UAT); `PUT /api/User` 500s on a login with no email.

  Next: 2e-2, delivered vs issued (R4-6.3).
- **2026-10-05, later** — **2e-2's four rulings, by the user before the build, all as recommended:**
  - a notice in the app counts as delivered;
  - an undelivered invitation is sent again by a button;
  - a reminder that reached some is stamped;
  - no stored counts (no migration in 2e-2).
- **2026-10-05, later** — **Lane 2, slice 2e-2 built and proved**: delivered vs issued (lane 2 State).
  `run-final-review.mjs` scored 476/476 on two clean passes, with 46 checks in 2e-2: half with no mail server,
  half through the run's own SMTP sink. The round-4 net was 205/205, the recruitment clash suite 58/58, and
  `run-lane-nb2` (the sweep through a sink, not run since round 4) 36/36. No request answered 500.
  - **R4-6.3 and F-32 closed:**
    - every answer counts the people reached, not the addresses tried;
    - an invitation is Sent, and a reminder or chase stamped, only once it reached somebody;
    - the event page's card (now "Invitations and reminders") says what is due and has reached nobody, and when
      there is no mail server.
  - **A small F-33 gap closed:** a corrected outside address no longer invites anyone while the event awaits
    approval.
  - **Found, not ours, proved, awaiting the user's word to register (#43):** a send followed by a save in one
    request writes the SMTP password back in plain text; the settings read answers it in plain text; and saving
    the settings writes it into the append-only audit log. One audit row with the probe's dummy password stays on
    UAT.
  - **Withdrawn while cleaning up:** earlier sessions' recruitment fixture approvals, live on real staff.

  Next: 2e-3, calendar invites (D-14) and the overdue-task chase, with one migration (the event's invite sequence
  and the task's "chased at"), which the user scaffolds.
- **2026-10-05, later** — **2e-2 committed** (`06176845a`). **2e-3 source-checked**, and four rulings taken, all as
  recommended (lane 2 State):
  - every change carries a calendar file;
  - interviews get a stable UID, no counter;
  - the overdue chase goes to the assignee only;
  - #43 is logged in the cross-module register.

  The model change (`CompanyEvent.CalendarSequence`, `EventTask.OverdueChasedAt`) is made; the user scaffolds.
- **2026-10-05, later** — **Lane 2, slice 2e-3 built and proved**: calendar files (D-14) and the overdue chase
  (F-34) (lane 2 State).
  - **The migration:** scaffolded, rewritten guarded, proved on a throwaway database, applied to UAT after a backup
    (history 144). Already-overdue tasks are chased once (the user's ruling, "as recommended").
  - **The detail read:** the first two passes found the event page's detail read failing after the migration. It
    asked for 387 MB of working memory on a fresh compile, waited 29 s, and used none. It is rebuilt as the event
    plus four seeks, each asking 0 KB; that needed one more build.
  - **The harness:** the suite's clean-up now survives a deadlock with the dispatcher.
  - **Results:** `run-final-review.mjs` scored 516/516 on two clean passes, with 40 checks in 2e-3 and calendar files
    read back from the sink. The interview probe scored 10/10: one UID across an invitation and its move. The round-4
    net was 205/205, recruitment 58/58, the templates probe 24/24, and `run-lane-n` 109/115 (section J, #40). No
    request answered 500.
  - **Demo data:** the board-pack task was chased once, as ruled.

  Next: 2f, recurrence as a light series (D-12).
- **2026-10-05, later** — **2e-3 committed** (`8c82b6401`). **2f source-checked**, and four rulings taken, all as
  recommended (lane 2 State):
  - a series is approved once;
  - one notice per guest per series action;
  - "book the room for every occurrence" moves to lane 3;
  - a new "Weekdays" pattern.

  2f is split in two: 2f-1 (the series, its approval, the day-off flag, extending, the screens) and 2f-2 (the scope
  choice, the two series notices, answering per date or for the series).
- **2026-10-05, later** — **Lane 2, slice 2f-1 built and proved**: a recurring event is a series (D-2, D-12, C-14)
  (lane 2 State). No migration.
  - **Built:**
    - every occurrence made on create, up to 52, on the first date's rule;
    - one approval for the series;
    - an occurrence on a public holiday or a company-wide closure made, warned of and flagged;
    - extending from any occurrence;
    - "occurrence k of n", the series card and the register's series filter.
  - **Results:** `run-final-review.mjs` scored 570/570 on two clean passes, with 54 checks in 2f-1; no memory-grant
    wait, and the largest grant of a series query was 5 MB. The round-4 net was 209/209 (`run-slice2` now deletes
    all four occurrences of its quarterly event) and recruitment 58/58. No request answered 500.
  - **Cleaned:** two `run-slice2` events left live by earlier runs today, the run's logins, and the R4D
    requisition's notices to real staff.

  Next: 2f-2, the series scope choice on guests, edit, move and cancel, and the two series notices
  (`EventSeriesInvitation`, `EventSeriesChanged`: 52 templates).
- **2026-10-05, later** — **2f-1 committed** (`34cbccc83`). **2f-2 source-checked** at that HEAD (lane 2 State). Four
  defects found:
  - approving a series sent one invitation per date;
  - cancelling or deleting occurrence 1 strands a series awaiting approval;
  - extending ignores a series move;
  - moving an approved series would start one approval per date.

  Three rulings taken, all as recommended: an edit with series scope copies only what it changed; extending invites
  the latest date's guests; 2f-2 is split into 2f-2a (guests and answers) and 2f-2b (edit, move, cancel). Both series
  emails come in 2f-2a, which removing a guest from several dates needs.
- **2026-10-05, later** — **Lane 2, slice 2f-2a built and proved**: guests and answers across a series (D-12) (lane 2
  State). No migration.
  - **Built:**
    - a guest added to, answered for and taken off this and following dates or every date, never a date that has
      started, been completed or been cancelled;
    - one notice per guest per action: the series invitation and the series-changed email (52 templates), with a
      calendar file per date, and two in-app topics (15);
    - the series' approval invites each waiting guest once (the first defect);
    - extending carries the latest date's guests and invites them once.
  - **Results:** `run-final-review.mjs` scored 619/619 on two clean passes, with 49 checks in 2f-2a, half through the
    sink; no memory-grant wait. The round-4 net was 209/209, recruitment 58/58, the templates probe 30/30 and
    `run-lane-n` 109/115 (section J, #40) once its ready-made-HTML list named the two new emails. No request
    answered 500 in the runs.
  - **Harness cost noted:** the suite's notice withdrawal now scans for 100 events and takes about 42 s.

  Next: 2f-2b, edit, move and cancel with a series scope, and the other three defects.
- **2026-10-05, later** — **2f-2a committed** (`19eec4b19`). **Lane 2, slice 2f-2b built and proved**: edit, move and
  cancel across a series (D-12) (lane 2 State). No migration. **2f is done.**
  - **Built:**
    - the edit, the move and the cancellation take a series scope;
    - an edit copies only what it changed (the user's ruling), and a move shifts each date by the same days;
    - one refused date refuses all, named;
    - each guest is told once per kind of change.
  - **The three remaining defects fixed:** the series' approval passes on when its date is cancelled or deleted; a
    moved approved series is approved once; extending follows a series move but not a lone one.
  - **Results:** `run-final-review.mjs` scored 658/658 on two clean passes, with 39 checks in 2f-2b, first time; no
    memory-grant wait. The round-4 net was 209/209, recruitment 58/58, the templates probe 30/30 and `run-lane-n`
    109/115 (section J, #40). No request answered 500.
  - **Harness:** `run-lane-n` could not start; the API's sweeps leave rows on its fixture tenant. Its clean-up now
    clears them. The suite's own notice withdrawal (50 s at 124 events) made the notification service's poll time out
    twice: lane 6's item, raised.

  Next: 2g, search, export, the dashboard and clashes on the two registers; then 2h.
- **2026-10-05, later** — **2f-2b committed.** The suite's slow notice withdrawal fixed first (lane 6's item; harness
  only). **2g source-checked** and four rulings taken, all as recommended (lane 2 State): C-15 as D-9; "the same place"
  is the same site, or either with no site; export on the register's read permission; two slices.
- **2026-10-05, later** — **Lane 2, slice 2g-1 built and proved**: the registers' search, paging, export, and the
  dashboard (D-9; C-10…C-13, C-25) (lane 2 State). No migration.
  - **Built:**
    - both registers searched, filtered, sorted and paged on the server, with a date filter;
    - CSV exports for the same filters;
    - the landing page in one read.
  - **Results:** `run-final-review.mjs` scored 679/679 on two clean passes, with 21 checks in 2g-1, first time. The
    blocking watcher logged nothing on either pass; the harness's withdrawal took 5 s, against about 50 s before. The
    round-4 net was 209/209, recruitment 58/58, the templates probe 30/30 and `run-lane-n` 109/115 (section J, #40).
    No request answered 500, and no query timed out.

  Next: 2g-2, the clash rule (C-15), the clashes check and the form's warnings before save.
- **2026-10-05, later** — **2g-1 committed** (`cb5a16cab`). **Lane 2, slice 2g-2 built and proved**: event against
  event (D-9; C-15) (lane 2 State). No migration. **2g is done.**
  - **Built:**
    - the rule as the user ruled it: refused only whole-company against whole-company or a unit against the same unit,
      in the same place (the same site, or either with no site), and warned of otherwise;
    - checked on create (a series' every date, before a number is taken), extend, edit (only when it changes what a
      clash depends on) and reschedule (a series too);
    - `GET events/clashes` and the form's red and amber lines before save.
  - **Results:** `run-final-review.mjs` scored 718/718 on two clean passes, with 39 checks in 2g-2, first time; the
    blocking watcher was silent. The round-4 net was 209/209, recruitment 58/58, the templates probe 30/30 and
    `run-lane-n` 109/115 (section J, #40). No request answered 500, and no query timed out.

  Next: 2h, attachments on the upload gate (C-18, F-54) and the drill's event (C-51). It closes lane 2.
- **2026-10-05, later** — **2g-2 committed** (`c01a398cf`). **2h source-checked**, four rulings taken, all as
  recommended (lane 2 State). **Lane 2, slice 2h built and proved**: files through the gate and the drill's event (D-3,
  D-9; C-18, C-51, F-54) (lane 2 State). No migration. **Lane 2 is done.**
  - **Built:**
    - event files uploaded through the gate, downloaded, removed on Write; path-only rows read "reference only";
    - a drill's next date kept as an all-day event for everyone, never blocked, moved and cancelled with the drill
      or its plan, and shown on the event as where it came from;
    - UAT's four demo papers replaced by files and DRILL-2026-001's event made, once (the user's rulings).
  - **Found by the proof:** `GetQueryable().IgnoreQueryFilters()` cannot see deleted rows. It hid a deleted drill,
    and — in 2f-2a's extend, committed — made deleted occurrences lose their place. Both fixed.
  - **Results:** `run-final-review.mjs` scored 759/759 on two clean passes, with 40 checks in 2h; the blocking
    watcher was silent. The round-4 net was 210/210, recruitment 58/58, the templates probe 30/30 and `run-lane-n`
    109/115 (section J, #40). No request answered 500.

  Next: lane 3, rooms and bookings — source-check it, and put D-13 and D-18 to the user.
- **2026-10-05, later** — **2h committed** (`20b6f4c7d`). **Lane 3 source-checked** (lane 3 State): every item open,
  one new finding (F-58, an event's move rewrote "Booked on"), and UAT holding the round-4 suites' residue. **D-13 and
  D-18 settled**, four slices, and the residue cleaned in 3a — all as recommended; the residue's events too, this
  module's only (a second ruling). **Lane 3, slice 3a built and proved**: the rules and guards (lane 3 State). No
  migration.
  - **Built:**
    - rooms: the edit's checks, codes against deleted rooms, retiring a room per D-18, availability by each room's rules,
      tenant-scoped reads;
    - bookings: the room, event, day and seat checks, the per-room lock, the state guards, UTC times;
    - an event's move keeps "Booked on" (F-58) and the room's furthest-ahead limit;
    - the Rooms page's Deactivate and retirement dialog, Delete for Admin only on both pages;
    - UAT's residue removed: 76 bookings, 34 rooms, 125 events; both round-4 D suites now tidy up.
  - **Results:** `run-final-review.mjs` scored 820/820 on two clean passes, with 61 checks in 3a, first time; the
    blocking watcher was silent. The round-4 net was 212/212, recruitment 59/59, the templates probe 30/30 and
    `run-lane-n` 109/115 (section J, #40). No request answered 500.

  Next: 3b — approval on the workflow engine, the booking notices (in-app and email), the hourly lapse and completion,
  no-show.
- **2026-10-05, later** — **3a committed** (`3fa2eb07a`). **3b's four rulings taken**, all as recommended (lane 3 State):
  the booker told of every outcome, in the app and by email; no-show from the start, no undo; a real Room Booking
  Approval, seeded and published on UAT once; two slices. **Lane 3, slice 3b-1 built and proved**: a booking's approval
  on the engine and its booker told (lane 3 State). No migration.
  - **Built:**
    - `RoomBookingDesk`, shared by the booking, room and event services;
    - RoomBooking on the engine — catalogue, display, context, adapter, the seeder's definition — with Approve on it,
      a new Not approve, and re-approval after a move;
    - the booker told on every path but their own act, in the app and by email (two emails, 54 templates; three
      topics, 18); an event's act tells after its save;
    - UAT's real "Room Booking Approval" published once.
  - **Results:** 3b-1 36/36 before the definition; then `run-final-review.mjs` 851/851 on two clean passes, 31 checks
    in 3b-1; the blocking watcher silent. The round-4 net was 212/212, recruitment 59/59, the templates probe 36/36 and
    `run-lane-n` 109/115 (section J, #40). No request answered 500.

  Next: 3b-2 — the hourly lapse of an unapproved booking at its start and the completion of past ones, and no-show.
- **2026-10-06** — **3b-1 committed** (`d9cb27c8b`). **Lane 3, slice 3b-2 built and proved**: the sweep's booking half
  and no-show (lane 3 State). No migration. **3b is done.**
  - **Built:**
    - the hourly pass and HR's run-now lapse a booking still Tentative at its start and complete an ended confirmed one;
    - a lapsed booking's approval withdrawn on the engine with nobody signed in (the integration's withdrawal throws
      without a user — found while building);
    - no-show, for good, its booker told (one email, 55 templates; one topic, 19).
  - **Results:** `run-final-review.mjs` scored 876/876 on two clean passes, with 25 checks in 3b-2, first time; the
    blocking watcher was silent. The round-4 net was 212/212, recruitment 59/59, the templates probe 39/39 and
    `run-lane-n` 109/115 (section J, #40). No request answered 500.

  Next: 3c — staff booking from `/me` (D-13): their own bookings, others' as busy times only.
- **2026-10-06** — **3b-2 committed** (`381eaf78d`). **Lane 3, slice 3c source-checked, its four questions settled by the
  user as recommended (lane 3 State), built and proved**: staff book rooms from the portal.
  - **Built:** `api/CompanySchedule/me` (the rooms, what is free, busy times, their own bookings); the portal's past-start
    rule; the booker's notices opening `/me/room-bookings/{id}`; three portal pages with a day board. No migration, no
    template, no topic.
  - **Results:** `run-final-review.mjs` scored 938/938 on two clean passes, with 62 checks in 3c, first time. The
    watcher's second pass was silent; its first logged eight sub-second lock waits in block 2b, none in 3c. The round-4
    net was 212/212, recruitment 59/59, the templates probe 39/39 and `run-lane-n` 109/115 (section J, #40). No request
    answered 500.

  Next: 3d — "book this room for every date" of a series (D-12), which closes lane 3.
- **2026-10-06** — **3c committed** (`1eda2ad49`). **Lane 3, slice 3d source-checked, its four questions settled by the
  user as recommended (lane 3 State), split in two; 3d-1 built and proved** (booking a series, approved once, told once).
  - **Results:** `run-final-review.mjs` scored 989/989 on two clean passes, with 51 checks in 3d-1, first time. The
    round-4 net was 212/212, recruitment 59/59, the templates probe 42/42 (after its own total was moved to 56) and
    `run-lane-n` 109/115 (section J, #40). No request answered 500; the watcher's second pass was silent.

  Next: 3d-2 — an extended series brings its rooms; the event page's Rooms card. It closes lane 3.
- **2026-10-06** — **3d-1 committed** (`18c8c8664`). **Lane 3, slice 3d-2 built and proved** (an extended series brings its
  rooms; the event page's Rooms card) — **lane 3 is done.**
  - **Results:** `run-final-review.mjs` scored 1002/1002 on two clean passes, with 13 checks in 3d-2, first time. The
    round-4 net was 212/212, recruitment 59/59, the templates probe 42/42 and `run-lane-n` 109/115 (section J, #40). No
    request answered 500.

  Next: lane 4 — milestones (documents, recurring projection), fiscal (Finance's calendar, HR's retired), the company
  profile. Source-check first; its decisions to the user.
- **2026-10-06** — **3d-2 committed** (`d8c948773`). **Lane 4 source-checked** (two read-only surveys — Finance's calendar,
  the company profile — and the milestones by hand); five questions settled by the user as recommended; two corrections to
  this plan (F-56's premise; Finance's year-end close is per book). **Slice 4a built and proved:** milestone files, and a
  yearly milestone's dates.
  - **Results:** `run-final-review.mjs` scored 1002 + 25 = 1027/1027 on two clean passes, first time. The round-4 net was
    212/212, recruitment 59/59, the templates probe 42/42 and `run-lane-n` 109/115 (section J, #40). No request answered
    500.

  Next: 4b — HR reads Finance's fiscal calendar (D-6): the card, `IHrFiscalCalendar` for requisitions and the manpower
  form, HR's fiscal screens and routes retired.
- **2026-10-06** — **4a committed** (`080759034`). **Slice 4b built and proved:** HR reads Finance's fiscal calendar
  in-process; HR's fiscal years, their 16 routes and two screens retired; one read-only Fiscal calendar page. Two rulings
  by the user: the suite may add a temporary Finance book close (and, since Finance's trigger refuses its delete, switch
  the trigger off for that one delete in one transaction); and, after the first build, a year Finance has not opened
  continues Finance's sequence, the policy month answering only with no Finance year at all (shown read-only meanwhile).
  - **Results:** `run-final-review.mjs` scored 1027 + 39 = 1066/1066 on two clean passes, first time. The round-4 net was
    191/191 (slice 3's 21 fiscal assertions retired), recruitment 59/59, the templates probe 42/42 and `run-lane-n` 109/115
    (section J, #40). No request answered 500.

  Next: 4c — the company profile (D-9, C-49, C-50, F-55).
- **2026-10-06** — **4b committed** (`e14e5195e`). **Slice 4c built and proved:** the logo a third versioned image through
  the seal's Admin doors, embedded in the six letter renderers through `ICompanyProfileProvider.GetLogoAsync` (else the
  tenant's own logo, now with a profile row too); the free-text Logo URL retired; every image a PNG or JPEG of at most
  2 MB (the size limit the user's ruling at the source check), checked before the gate stores a byte; the image buttons
  hidden without `HR.Company.Admin`.
  - **Results:** `run-final-review.mjs` scored 1066 + 28 = 1094/1094 on two clean passes, first time. The round-4 net was
    191/191, recruitment 59/59, the templates probe 42/42, `run-lane-n` 109/115 (section J, #40) and the tier-B profile
    suite 68/68. No request answered 500. **Lane 4 is done.**

  Next: lane 5 — the screens (C-1, C-16, R4-3.1, R4-6.x, R4-10A/B, F-19…F-27, F-57). Source-check first.
- **2026-10-06** — **4c committed** (`f62293f53`). **Lane 5 source-checked** (two read-only surveys; four rulings, all as
  recommended). **Slice 5a built and proved** — screens only: every remove matches its route's tier; the event page's
  original window and "Moved on"; the landing without the permission; sites-only pickers; the sidebar test's
  `KEEP_OPEN` (and L-97 logged for leave).
  - **Results:** `run-final-review.mjs` 1094 + 7 = 1101/1101 on two clean passes; the round-4 net 191/191, recruitment
    59/59, the probe 42/42, `run-lane-n` 109/115 (#40); the sidebar test 4/4 (the committed one fails on the two).

  Next: 5b — the diaries and the team schedule. Then the browser walk of lane 5's screens (the user's).
- **2026-10-06** — **5a committed. Slice 5b built and proved:** the team schedule read by a unit's head (and above), the
  readable-units list, each member's unit, the sub-unit filter and direct members, "Schedule for this unit" into a
  pre-filled event form; both diaries drawing an entry on every day it covers and naming a source they could not read;
  the training source reporting every session.
  - **Results:** `run-final-review.mjs` 1101 + 15 = 1116/1116 on two clean passes; the round-4 net 191/191, recruitment
    59/59, the probe 42/42, `run-lane-n` 109/115 (#40); the sidebar test 4/4. **Lane 5's code is done**; its browser walk
    (items 1–8) is the user's.

  Next: lane 7 — the company calendar (D-7, D-8, D-13, D-16; C-9, C-26, C-35, R4-6.4). Source-check first.
- **2026-10-06** — **5b committed. Lane 7 source-checked** (two read-only surveys; four rulings, all as recommended).
  **Slice 7a built and proved** (the calendar read, the staff event view, the invitee's own answer; the organiser told in
  the app; guest notices to the portal page) — 1147/1147 on two clean passes after one pass's own-suite naming fault.
  **7a committed. Slice 7b built and proved** — the screens, no server change: month bands broken at the week, the week
  view, filters as the legend, the room view and booking from it, the staff event page, the menus and the landing's links.
  - **Results:** `run-final-review.mjs` 1147/1147 on two clean passes; the layout tests 7/7; the sidebar test 4/4; the
    round-4 net 191/191, recruitment 59/59, the probe 42/42, `run-lane-n` 109/115 (#40). **Lane 7's code is done.**

  Next: lane 6 — the harness, the guide, the registers, memory. The browser walks of lanes 1–5 and 7 are the user's.
