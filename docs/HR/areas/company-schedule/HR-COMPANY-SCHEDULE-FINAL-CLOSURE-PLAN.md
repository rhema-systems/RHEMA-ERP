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
   slices 1a–1e (lane 1 State); its screen awaits lane 5's browser walk. **Lane 2 (events) is under
   way:** source-checked and its four decisions settled (§ 1c), slice 2a built and proved
   (2026-10-05). **Next: 2b, approval on the workflow engine (D-10).** *As planned:* Lane 0 (§ 6): first count on UAT, read-only, the rows the migration must decide about (legacy
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
| **D-13** | May staff book rooms themselves? | **Yes, from the portal**: their own bookings only, the same room rules, approval-required rooms routed to the approver, staff cancel their own. HR keeps the desk. | Lanes 3, 7 |
| **D-14** ✅ | Calendar invites in the emails? | **Yes**: an `.ics` on invitation, reschedule and cancellation — a stable UID per event, SEQUENCE raised on each change, METHOD REQUEST and CANCEL. The email DTO already carries attachments; the templated send needs an overload. External guests answer from their mail client to the organiser, and HR records it at the desk, which answers F-36. | Lane 2 |
| **D-15** ✅ | Unpaid closures, and closures added after leave was approved? | **Settled 2026-10-04 at lane 1's source check, refined there into D-15a, D-15b and D-15c (§ 1c); the payroll half goes into the payroll hand-off rather than the register.** *As first recommended:* HR records the pay flag and **exposes closures to payroll read-only**, logged as a cross-module item, because payroll is another developer's module (F-51). **A closure created, moved or deleted re-charges the approved leave it overlaps**, and the employee is told (F-52). | Lane 1 |
| **D-16** ✅ | Who is "Management only"? | **The heads of organisation units** (`OrganizationUnit.HeadEmployeeId`), plus the organiser and participants; "Management" visibility uses the same population (F-43). | Lanes 2, 7 |
| **D-17** ✅ | The milestone link to one employee's award or certification (C-41)? | **Settled 2026-10-01, as recommended: dropped.** Company milestones are company facts: the guide's own walkthrough files the ISO 9001 quality certification as one, while a training-module certificate and a long-service award each belong to one employee. A milestone's evidence is its documents (D-3); awards and milestones meet, if anywhere, through a company event for the awards ceremony. C-41 closes as decided, not built, which is a decision rather than a deferral. | Lane 0: no link columns. Lane 4: nothing to build |
| **D-18** | Retiring a room that has future bookings? | **Offer "cancel these N bookings and tell their bookers"**, and refuse deletion once a room has any booking history — deactivate instead (F-49). | Lane 3 |

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
| **2** | Events: validation, lifecycle guards, recurrence series, audience, in-app and email notices, attachments on the gate | — (D-10, D-11, D-14, D-16 ✅) | ◐ 2a built and proved (250/250 ×2; round-4 net 207/207); 2b next | events block |
| **3** | Rooms and bookings: rules, availability, guards, the booking lock, lapses, retirement, staff booking | D-13, D-18 (D-10 ✅) | ☐ | rooms block |
| **4** | Milestones: documents, recurring projection. Fiscal: Finance's calendar, HR's retired. Company profile | — | ☐ | milestones + fiscal block |
| **5** | Screens: the shared select re-test (F-27, in HEAD), removes, event page, diaries, landing, site picker, the sidebar gate test (F-57) | — | ☐ | browser walk |
| **7** | The company calendar (HR, staff, portal), the staff event view and the self-service reply | D-13 (D-16 ✅) | ☐ | calendar block, two logins |
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
- [ ] *✅ 2a for the event's site, unit and organiser (an active employee); participants and task assignees are 2d.* `EnsureExistsAsync` (tenant-scoped, not deleted, refusing as a rule — a 422, not a "not found")
      for `LocationId`, `OrganizationUnitId`, participant `EmployeeId` (active), task `AssignedToId`.

**Lifecycle** (*review: two of the first draft's guards contradicted other rules*)
- [ ] *✅ 2a for the guards, and the organiser may not approve their own event; the engine is 2b.* **Approve:** requires approval and not yet approved, while Scheduled, Rescheduled or Postponed —
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
- [ ] *✅ 2a for the picker, the creator stamp, the diaries and the clash check; reminding the organiser is 2e.* The organiser per **D-11**; the organiser and the participants always see and are committed to
      the event.
- [ ] When `ShowOnCompanyCalendar` is on, the event's audience is an audience rule: `Scope = AllStaff`
      → `AllEmployees`; `Scope = Department` → the event's `OrganizationUnit` (its subtree);
      `ManagementOnly` and `Management` visibility per **D-16**; Private and Confidential reach
      participants and the organiser only. Evaluated with `IHrAudienceResolver`; used by
      `CompanyEventCommitmentSource` and lane 7. Form descriptions say exactly this.
- [ ] `ShowOnIntranet` publishes an `HrAnnouncement` to the same audience (*review: the first draft
      only relabelled it*); `Priority` stays a label and says so.
- [x] *✅ 2a (`CompanyEventRules.DailyWindows`).* **(R4-10A.2 — backend, moved here from lane 5)** `CompanyEventCommitmentSource` yields a window
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
- [ ] *✅ 2a for events (the repository's nine reads removed; the service's own tenant-scoped query); rooms, bookings, milestones and fiscal are their lanes'.* Range endpoints: overlap semantics; UTC dates throughout the repository; **the tenant filter
      inside every repository query** (F-30).
- [ ] *✅ 2a for events; tasks are 2d, rooms and bookings lane 3.* Update responses re-read after save, as creates do (F-46).
- [x] *✅ 2a; the server refuses them on create and on a change of category.* (F-44) "Holiday" and "Milestone" leave the category picker (the values kept for old rows), with
      a line pointing to public holidays and milestones.
- [x] *✅ 2a; the department is refused on the wire (D-5), and the page shows the unit.* `events/department/{id}` → `events/unit/{id}`; the event form's Department picker →
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
- **2e** notices:
  - the in-app notices class;
  - delivered vs issued (R4-6.3), F-33;
  - the new notices;
  - calendar invites (D-14, with its migration).
- **2f** recurrence as a light series (D-12).
- **2g** search, export, the dashboard and clashes (C-10…C-13, C-15, C-25) on the two registers.
- **2h** attachments on the gate (C-18, F-54) and the drill (C-51).

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
      HR-wide); `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` (attendance A-92); the payroll hand-off
      (closures beside holidays, D-15c);
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
