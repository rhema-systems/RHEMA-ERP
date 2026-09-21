# HR Company Schedule — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source — page components, forms, the controller, the
service, the repositories, the EF model, the seeded permission map and the demo scenario. It
describes what the code is built to do. Where a screen promises something the server does not do,
the step says so rather than smoothing it over.

**Scope:** the whole **Company Schedule** group of the HR sidebar — both menu items — plus the six
screens that hang off them without a menu entry, the **four setup areas** under
Administration → HR → Company Schedule, and the **Company Profile** screen, which is a different
menu but the same permission family and the one place the Admin tier genuinely earns its keep.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| — | *(group landing)* Company Schedule | `/hr/company-schedule` | 3 |
| 1 | Events | `/hr/company-schedule/events` | 4 |
| — | *(no menu entry)* New event | `/hr/company-schedule/events/new` | 5 |
| — | *(no menu entry)* The event | `/hr/company-schedule/events/[id]` | 6 |
| — | *(no menu entry)* Edit an event | `/hr/company-schedule/events/[id]/edit` | 7 |
| 2 | Room Bookings | `/hr/company-schedule/bookings` | 8 |
| — | *(no menu entry)* Book a room | `/hr/company-schedule/bookings/new` | 9 |
| — | *(no menu entry)* The booking | `/hr/company-schedule/bookings/[id]` | 10 |
| — | *(Administration)* setup hub | `/administration/hr/company-schedule` | 11 |
| — | *(Administration)* Meeting Rooms | `…/company-schedule/rooms` (+ `new`, `[id]/edit`) | 12 |
| — | *(Administration)* Business Closures | `…/company-schedule/closures` | 13 |
| — | *(Administration)* Milestones | `…/company-schedule/milestones` | 14 |
| — | *(Administration)* Fiscal Years | `…/company-schedule/fiscal-years` | 15 |
| — | *(Administration)* The fiscal year | `…/company-schedule/fiscal-years/[id]` | 16 |
| — | *(Administration → Settings)* Company Profile | `/administration/hr/settings/company-profile` | 17 |

**Seventeen screens** over **thirteen tables**, with four collections nested inside a single event.

> **A word about this module's history, because it explains its shape.** Until 2026-08-28 the
> backend carried **ninety endpoints and no screen had ever called one of them** — the whole
> surface was dormant from the original port. All forty write endpoints are wired now, and the
> screens were written against a harness of 162 assertions. Two things had to be fixed before a
> single form could exist: five actor ids (`organizerId`, `approvedById`, `markedById`,
> `bookedById`, `announcedById`) arrived as **query parameters**, which would have made every one
> of them act-as-anyone the moment a screen existed; and `MeetingRoom.StationId` was a **required
> foreign key to an empty table with no endpoint**, which makes its own form unfillable. The first
> moved to the token. The second was repointed to the live `Location` tree. Both of those are
> worth a sentence in the demo — see § 1.6.

---

## This document is two things at once

Like the recruitment, employees, leave and attendance guides, this is a **reference** and a
**script you can perform**. Every chapter has the same five parts, and you can read only the ones
you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Three more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered
  (`LIVE WRITE 1` … `13`), and chapter 19 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.
- **🚫 DO NOT PRESS** — a control that renders for your persona and returns 403, or one that
  writes something you cannot undo inside a demo.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the six rules that decide whether this demo works

This is the friendliest module in HR to demonstrate — everybody understands a calendar and a
meeting room — and it has six behaviours that will catch you out live. Four of them are screens
promising something the server does not do. Read these twice.

### Rule 1 — There is a red **Delete** button on the event page and `hr.head` cannot use it

The `HR` role holds `HR.Company.Read`, `HR.Company.Write` and `HR.Company.Approve`. It does **not**
hold `HR.Company.Admin` — and **every delete in this module is Admin-gated**:

| Where | Control that 403s for `hr.head` |
|---|---|
| **The event page** | the **Delete** button — *destructive red, in the page header, beside Edit* |
| Event → Participants | ⋯ → **Remove participant** |
| Event → Attachments | ⋯ → **Remove attachment** |
| Event → Tasks | ⋯ → **Remove task** |
| Bookings register | ⋯ → **Delete** |
| Meeting rooms | ⋯ → **Delete** |
| Closures / Milestones | ⋯ → **Remove closure** / **Remove milestone** |
| Fiscal years | ⋯ → **Delete**, and **Remove period** |

The event's Delete is the dangerous one: it is the **only** red button in this module, it sits in
the header of the screen you will spend five minutes on, and its dialog reads
*"The event and everything recorded against it are removed."* Press it and you get a red error
toast in the middle of your best chapter. **Cancel is the action you want, and it works.** This is
finding **C-1**.

### Rule 2 — The reschedule dialog says the original dates are kept. They are not.

*"The original dates are kept on the record as history."* They are overwritten. `RescheduleEventAsync`
writes the new start and end straight over the old ones and sets three fields: `IsRescheduled`,
`RescheduledDate` — **which is the timestamp of the button press, not the new date** — and
`RescheduleReason`. Nothing anywhere stores what the event used to be.

So the record afterwards says *"Rescheduled 17 Sep 2026 — Board papers not ready"* next to dates
that look as if they were always the plan. **Do not say "and you can see what it was before".**
Say what is true, which is still good: *the change is recorded, with a date and a reason.* This is
finding **C-2**.

### Rule 3 — "Approve" here is a flag, not an approval chain

Every other request in HR — leave, overtime, regularisations, travel, requisitions — runs on the
corporation's workflow engine. **Nothing in this module does.** Approving an event and approving a
booking are single-click fields:

- no workflow instance, no definition, no step, no assignee check;
- **no entry in `/workflow/inbox`** — the approver is never told;
- anybody holding `HR.Company.Write` can approve anything, including their own;
- `ApproveEventAsync` sets the status to *Confirmed* whether or not the event was marked as
  requiring approval.

This is a defensible design for a room booking and a weaker one for a board meeting, and either
way it is **not** what the rest of the product does. Say it plainly — § 1.5 has the sentence. This
is finding **C-3**.

### Rule 4 — The room's booking rules are recorded and never enforced

The Meeting Room form carries **Longest booking (hours)** and **Book up to (days ahead)**. Both
save, both round-trip, and **no code reads either**. Neither is the room's **capacity** checked
against the booking's expected attendees.

Exactly **two** rules are enforced when a room is booked: the room must be flagged bookable, and
the time slot must be free. Those two are properly enforced, on both create and edit, and the
clash refusal is the best beat in the module — see chapter 9. Just do not promise the other three.
This is finding **C-4**.

### Rule 5 — Business closures reach nothing

The screen says a closure *"Affects leave and attendance calculations"*, the runbook aside says
closures *"feed leave day-counting and attendance the same as public holidays"*, and there is a
purpose-built `GET closures/is-closure-date` endpoint.

**No leave code path and no attendance code path reads `BusinessClosures`**, and no screen calls
that endpoint. A closure today is a well-structured announcement with an audience, a paid/unpaid
flag and a named announcer — and nothing downstream consumes it. Chapter 13 turns that into an
honest sentence. This is finding **C-5**.

### Rule 6 — Event and booking numbers can repeat

`EVT-2026-00007` and `BK-2026-00004` are generated by **counting the live rows** for the year. A
delete is a soft delete, so the count drops and the next record reuses the number — and the index
on both columns is **not unique**, so nothing stops it. Two events can carry the same number
silently.

Every other number generator in HR was hardened against exactly this (leave, overtime,
regularisations, remote work, imports, requisitions). These two were not. It will not break your
demo — but do not delete a seeded event and then create one, or you will be showing two
`EVT-2026-00004`s. This is finding **C-6**.

---

## Conventions

**Routes.** `/hr/company-schedule/events/[id]` is the file
`frontend/src/app/hr/company-schedule/events/[id]/page.tsx`. A segment in square brackets is a
parameter.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in the solution. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — `CompanyEvent` lives in
`CompanyEvents`, `RoomBooking` in `RoomBookings`. *(The two fiscal tables are the exception: they
are declared on the main `ApplicationDbContext`, not the HR partial.)*

**Columns every table here carries.** Every entity in this guide derives from `TenantEntity`:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it explicitly |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

**The permission ladder.** Three permissions gate this module, plus one interim tier that belongs
to a different feature entirely:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Company.Read` | every read — the company profile with its statutory numbers, the HR policy settings, the external-associate register, and the whole company schedule: events, rooms, bookings, milestones, closures and fiscal years | **yes** |
| `HR.Company.Write` | maintain the company profile; run the company schedule — events with their participants, attendance and tasks, rooms and bookings, milestones, closures, fiscal years and their periods. **Changing the HR policy settings is deliberately NOT this permission** | **yes** |
| `HR.Company.Admin` | **change the HR policy settings** — the procedural-absence threshold (FR-HR-092) and the budget/establishment enforcement modes (FR-HR-136) — **replace or retire the company seal**, and **delete** every company-schedule record | **no** |
| `HR.Company.Approve` | interim authority over **team objectives and terms of reference** where no workflow definition is published. It has nothing to do with the company schedule, despite the name | **yes** |

> The Admin tier's shape is worth reading twice, because it is the clearest statement of intent in
> the HR permission map. It is not "the senior version of Write". It is three specific things: the
> knobs that move a trust boundary, the corporation's seal, and destruction. An HR officer runs the
> calendar; an administrator decides what the calendar is allowed to mean.

**The actor is always the token.** The organiser of an event, the approver of a booking, the
person who marked attendance, the booker and the announcer of a closure are all taken from
`CurrentUser.EmployeeId`. **None of those fields is on any form**, and that is the point — they
used to be query parameters. One consequence:

> **A signed-in user whose account is not linked to an employee record cannot organise an event,
> book a room, mark attendance or announce a closure.** They get a clean `400` that writes nothing.
> The `admin` account is unlinked on this database, so this is load-bearing rather than
> theoretical — chapter 17 is the only place `admin` is used, and it is used for the seal.

---

## 1. How the company schedule hangs together

### 1.1 Two halves that meet in one place

Recruitment is a chain, employees is a hub, leave is a ledger, attendance is a pipeline.
**The company schedule is two registers that share a calendar.**

```
 ┌─────────────── THE CALENDAR HALF ────────────────────────────────────┐
 │                                                                       │
 │   CompanyEvent   EVT-2026-00001                                      │
 │   name · category · type · priority · when · where · audience         │
 │   visibility · budget · resources · catering · reminders              │
 │   Scheduled → Confirmed → InProgress → Completed                      │
 │            ↘ Cancelled  ↘ Postponed  ↘ Rescheduled                    │
 │                                                                       │
 │     ├── EventParticipant  — who is invited (employee OR external)     │
 │     │     NotSent → Sent → Accepted / Declined / Tentative            │
 │     ├── EventAttendance   — who actually turned up, in and out        │
 │     ├── EventTask         — before / during / after, assigned & due   │
 │     └── EventAttachment   — agenda, minutes, presentation, handout    │
 │                                                                       │
 │   Beside the events, three reference registers:                       │
 │     • CompanyMilestone  — anniversaries and achievements              │
 │     • BusinessClosure   — days the organisation is shut               │
 │     • FiscalYear → FiscalPeriod — the reporting windows               │
 └───────────────────────────────┬───────────────────────────────────────┘
                                 │  an event MAY be linked to a booking
                                 ▼
 ┌─────────────── THE ROOMS HALF ───────────────────────────────────────┐
 │                                                                       │
 │   MeetingRoom  RM-0001 · Boardroom · 18 seats · Tema Head Office      │
 │   facilities: projector · whiteboard · VC · audio · A/C               │
 │   rules: active? bookable? needs approval? max hours? days ahead?     │
 │                                                                       │
 │            ↓  is held for a window by                                 │
 │                                                                       │
 │   RoomBooking  BK-2026-00001                                          │
 │   room · from · to · purpose · seats · requirements · catering        │
 │   Tentative → Confirmed → Completed                                   │
 │            ↘ Cancelled  ↘ NoShow                                      │
 │                                                                       │
 │   ⛔ THE ONE HARD RULE IN THE MODULE:                                  │
 │      a room cannot be double-booked. Overlap is refused on            │
 │      create AND on edit, and the booking form asks the server         │
 │      which rooms are free before it offers you one.                   │
 └───────────────────────────────────────────────────────────────────────┘

 Everything on the left is HR's day-to-day work → /hr/company-schedule
 Everything set up once — rooms, closures, milestones, fiscal years —
                                                → /administration/hr/company-schedule
```

Four points follow, and they are the four worth landing in a room:

**1. An event is a project, not a diary entry.** It carries a budget and a budget code, the
resources and the catering it needs, a task list split into before / during / after with owners and
due dates, the people invited with their replies, the people who actually came, and the papers. A
company durbar for a hundred and forty people is a small project, and this is the shape of one.

**2. The invitation list and the attendance register are two different lists, deliberately.** Who
was asked is not who came. Both are kept, and the gap between them is the useful number.

**3. The room clash is enforced by the database, not by the calendar being polite.** The booking
form asks *"which rooms are free between these two times"* and only offers those. Try it anyway
and the server refuses. That is the one rule in this module you can demonstrate by failing on
purpose.

**4. The split between HR and Administration is the same split as everywhere else in this
product.** Events and bookings change every day and live under Human Resources. Rooms, closures,
milestones and fiscal years are decided once a year and live under Administration. Same permission
tier, different rhythm.

### 1.2 The tables

Thirteen, in the order the module uses them.

| # | Table | One line |
|---|---|---|
| 1 | `CompanyEvents` | The event itself — 60 columns, from priority to catering |
| 2 | `EventParticipants` | Who is invited: an employee **or** an external guest, with their RSVP |
| 3 | `EventAttendances` | Who actually attended, with check-in, check-out and who marked it |
| 4 | `EventTasks` | What has to happen before, during and after — owner, due date, priority |
| 5 | `EventAttachments` | Agenda, minutes, presentation, handout, resource — by **reference** |
| 6 | `MeetingRooms` | A bookable room: site, placement, seats, facilities, booking rules |
| 7 | `RoomBookings` | A room held for a window, with a purpose and an optional linked event |
| 8 | `CompanyMilestones` | Anniversaries, achievements, launches, targets, certifications |
| 9 | `BusinessClosures` | Days the organisation is shut, company-wide or per site/department |
| 10 | `FiscalYears` | The reporting window, with exactly one marked current |
| 11 | `FiscalPeriods` | Quarters or months inside a year, each openable and closable |
| 12 | `CompanyProfiles` | The legal identity — statutory numbers, addresses, the letterhead |
| 13 | `CompanySealAssets` | The seal and specimen signature every generated HR letter is stamped with |

### 1.3 The vocabularies

Seven enum families. Two matter; the rest are look-ups.

**Event status** — seven values: *Scheduled* (where every event starts) · *Confirmed* (what
Approve sets) · *In progress* · *Completed* (what Complete sets) · *Cancelled* (what Cancel sets) ·
*Postponed* · *Rescheduled*. ⚠ **Rescheduling does not set *Rescheduled***, and nothing sets
*In progress* or *Postponed* — all three are reachable only by choosing them by hand in the edit
form's Status dropdown (**C-7**).

**Booking status** — five: *Tentative* (a room that needs approval starts here) · *Confirmed* (a
room that does not, and what Approve sets) · *Completed* · *Cancelled* · *No show*. ⚠ *Completed*
and *No show* are set by nothing (**C-8**).

The other five, for reference:

| Family | Values |
|---|---|
| **Event category** | Meeting · Training · Company Event · Deadline · Holiday · Conference · Social Event · Milestone |
| **Event type** | Internal · External · Client Meeting · Statutory · Board Meeting |
| **Location type** | On-site · Off-site · Virtual/Online · Hybrid |
| **Audience (scope)** | All Staff · Department · Selected Individuals · Management Only · External Only |
| **Visibility** | Public · Private · Department · Management · Confidential |
| **Participant role** | Organizer · Presenter · Attendee · Optional Attendee · Facilitator |
| **Invitation status** | Not Sent · Sent · Accepted · Declined · Tentative · No Response |
| **Room type** | Conference Room · Boardroom · Training Room · Huddle Room · Auditorium |
| **Closure type** | Full Closure · Partial Closure · Department Closure · Station Closure |
| **Milestone category** | Company Anniversary · Achievement · Product Launch · Target/Goal · Certification |
| **Fiscal year status** | Active · Closed · Archived |

### 1.4 What the room rules decide, and what they do not

Have this ready. Somebody in a facilities role *will* ask.

| Setting on a room | Stored | Shown | **Enforced when a room is booked** |
|---|---|---|---|
| **Can be booked** (`IsBookable`) | ✅ | ✅ | ✅ — *"This room is not available for booking"* |
| **Active** (`IsActive`) | ✅ | ✅ | ✅ — an inactive room is left out of the availability list |
| *(the time slot)* | — | — | ✅ — *"There is a conflicting booking for this time slot"*, on create **and** on edit |
| **Bookings need approval** | ✅ | ✅ | ✅ — the booking is created *Tentative* instead of *Confirmed* |
| **Seats** (`Capacity`) | ✅ | ✅ | ⚠ **half** — it filters the availability search, and a booking for 40 people in an 18-seat room is still accepted if you pick the room another way |
| **Longest booking (hours)** | ✅ | ✅ | ❌ — read by nothing |
| **Book up to (days ahead)** | ✅ | ✅ | ❌ — read by nothing |

And the same table for an event:

| Setting on an event | Enforced |
|---|---|
| **Requires approval** | ❌ — the event is usable whether or not it has been approved, and Approve works either way |
| **Requires RSVP** / **RSVP deadline** | ❌ — nothing chases, nothing closes |
| **Send reminders** / **Days before** | ❌ — **no reminder is ever sent**; `ReminderSentDate` is written by nothing |
| **Audience (scope)** | ❌ — it does not populate or restrict the participant list |
| **Visibility** | ❌ — every event is visible to everyone with the read permission |
| **Show on company calendar** / **Show on intranet** | ❌ — there is no company calendar screen and no intranet feed |
| **Recurrence** | ❌ — the pattern is stored and **no second occurrence is ever generated** |

> **Say it like this, once, in chapter 5, and you will not have to defend it later:**
>
> *"An event here is a complete record of an occasion — what it is, who is coming, what it costs,
> what has to be done and by whom. What it is not yet is an engine: it does not email the
> reminders, it does not chase the RSVPs, and a recurring event does not spawn its occurrences.
> Those are notification and scheduling jobs on top of a record that already carries every field
> they need. The register is the hard part and it is done."*

### 1.5 Where the approval happens — and why it is different here

**It does not.** This is the only approvable thing in HR that is not on the workflow engine.

| | Everywhere else in HR | Here |
|---|---|---|
| Who approves | the engine's assignee, per record | anyone with `HR.Company.Write` |
| How they know | `/workflow/inbox` | they do not |
| What is kept | definition, step, assignee, decision, timestamp, comments | an approver id and a date |
| Self-approval | blocked where the definition says so | always allowed |

Two honest ways to present it, and the second is better:

> *"Room bookings and internal events are approved in place rather than routed — a facilities
> approval is a two-second decision by whoever owns the room, and putting it through a
> multi-step engine would make it slower without making it safer. Where TDC decides an occasion
> does need a routed approval — a board meeting, an event with a fifty-thousand-cedi budget — the
> engine is already in the product and already approves leave, travel and requisitions. Wiring a
> company event onto it is the same four-step change we made for every other module."*

Do not claim there is a chain and then open a tab that shows a flag.

### 1.6 Two design decisions worth a sentence each

Both were made when the screens were built, and both answer a question a technical buyer asks.

**"Who is recorded as the organiser?"** — Every actor field in this module comes from the signed-in
user's token, and none of them is on a form. They used to arrive as query parameters, which would
have let any caller file an event under somebody else's name the moment a screen existed. They were
moved before the first form was written. *"You are recorded as the organiser"* on the new-event page
is the whole feature, said in five words.

**"What is the difference between Site and Location on a room?"** — They are two different fields
and both are required. **Site** is the entry in the corporation's location tree — Tema Head Office,
Ashaiman, Ho. **Location** is free text saying where in that site the room is — *"East wing, past
reception"*. The room's site FK originally pointed at a vestigial table that was empty, had no
endpoint and no repository, which made the room form unfillable; it was repointed to the live
location tree the rest of HR uses.

> ⚠ **The Site dropdown lists the whole location tree, not just sites** — Ghana (Country) and
> Greater Accra (Region) appear alongside Tema Head Office. Every option is suffixed with its level
> (`Tema Head Office · Site`) precisely so nobody files a boardroom under "Ghana", and the deepest
> levels sort to the bottom, nearest the cursor. Say *"pick the one that says Site"* and move on.

---

## 2. Before the room fills — the prep

**Time needed: 10 minutes the evening before.** This is the lightest prep of any HR module — the
demo scenario builds a genuinely complete calendar and nothing in it is time-stamped to the build
date the way attendance's punches are.

### 2.1 What the demo database already holds

`scenarios/110-company-schedule.mjs` builds all of it through the real API, as the real personas:

| Table | What is there |
|---|---|
| `FiscalYears` | **1** — *FY 2026*, 1 Jan – 31 Dec, marked **Current** |
| `MeetingRooms` | **3** — *Boardroom* (BRD, 18 seats, floor 4, VC + audio), *Conference Room A* (CONF-A, 30 seats, floor 2), *Huddle Room 1* (HUD-1, 6 seats, floor 3). All at Tema Head Office, all with projector, whiteboard and A/C |
| `RoomBookings` | **2** — *Management Committee — September* in the Boardroom (9–12, video link for Ho), and *Community 25 design review with the consultants* in Conference Room A (14–16), booked by `head.dev` |
| `CompanyEvents` | **4** — see below |
| `EventParticipants` | **~15** across the four events, including one **external** guest: *Nana Kwame Baffoe, Board Secretariat*, as a Facilitator |
| `EventAttendances` | **5** on the retreat — four present, one absent with a reason |
| `EventTasks` | **8** across the durbar, the board meeting and the fire drill — one already completed |
| `EventAttachments` | **4** — two board papers, the durbar programme, the retreat communiqué |
| `BusinessClosures` | **1** — *Year-end stocktake*, 29–30 December, whole company, **paid**, does not count as a working day |
| `CompanyMilestones` | **1** — *TDC 74th Anniversary*, 18 October, recurring annually |
| `CompanyProfiles` | **1** — the full legal identity: CS-1952-000118, TIN, VAT, SSNIT employer number, TDC House Community 1, the signatory and the letter footer |
| `CompanySealAssets` | **2** — a seal and a specimen signature, uploaded as **admin** |

**The four events, and why each is there:**

| Event | Shape | What it demonstrates |
|---|---|---|
| **Board of Directors — Q3 meeting** *(next fortnight)* | Board Meeting · Management Only · Management visibility · RSVP required · Boardroom · 14 expected | participants incl. an external, **two RSVPs already answered**, two board papers attached, two tasks |
| **Annual Staff Durbar** *(45 days out)* | Company Event · All Staff · **all-day** · GHS 48,000 budget, code HRA/EVT/2026/03 · 120 expected | the **budget and logistics** half — canopies, PA, staging, catering for 140 — and a four-task plan across all three stages |
| **Fire drill — Head Office** *(next week)* | Training · 10:00–11:00 · Medium priority | the cross-module link — this is the SHE emergency plan's next drill |
| **Management retreat — 2026 budget preparation** *(38 days ago)* | Meeting · Management Only · Volta Serene Hotel, Ho · GHS 62,000 | the **only completed event**: an attendance register, an actual attendance of 4, and a real outcome summary |

> **The retreat is the most valuable record on this database** and the easiest to miss, because the
> register sorts newest first and it is in the past. It is the only place the attendance tab and the
> outcome summary have anything on them. Chapter 6 opens it deliberately.

### 2.2 Check the two numbers you will quote

Open these and write them in. They are the only figures you will say out loud.

| Screen | What to write down |
|---|---|
| `/hr/company-schedule` | Next 30 days: ____ events · Bookings awaiting approval: ____ |
| `/hr/company-schedule/events` | total events: ____ |
| `/administration/hr/company-schedule/rooms` | bookable rooms: ____ |

⚠ **"Bookings awaiting approval" will almost certainly read 0**, and that is correct rather than
broken: none of the three seeded rooms has *Bookings need approval* switched on, so both seeded
bookings were created **Confirmed**. Chapter 12 turns one room on and chapter 9 then produces a
Tentative booking live, which is a much better demonstration than a pre-baked queue. Decide now
whether you are doing that, because it changes the order of chapters 9 and 12.

### 2.3 Decide whether you want the Tentative-booking beat

The strongest five minutes in this module is:

1. turn *Bookings need approval* on for the **Boardroom** *(chapter 12, LIVE WRITE 9)*;
2. book it live and watch the toast say *"It needs approval before it is confirmed."* *(chapter 9)*;
3. approve it *(chapter 10)*.

That needs chapter 12 **before** chapter 9. If you would rather run the book in order, do the
switch now, tonight, as part of prep — then chapter 9 works unchanged and chapter 12 just shows the
setting already on.

> Tonight I set *Bookings need approval* on: ☐ Boardroom  ☐ nothing — running in book order

### 2.4 Pick your clash

Chapter 9's best moment is a booking the server refuses. You need a room and a window that is
**already taken**. The seeded pair:

| Room | Taken from | To |
|---|---|---|
| Boardroom | next Tuesday 09:00 | next Tuesday 12:00 |
| Conference Room A | next Thursday 14:00 | next Thursday 16:00 |

Open `/hr/company-schedule/bookings` now and write the two real dates in:

> Boardroom busy: ______________  ·  Conference Room A busy: ______________

### 2.5 One window, one persona

Unlike leave and attendance, **this whole book runs as `hr.head` in a single window** — there is no
approval routing to demonstrate and no self-service side. The one exception is **chapter 17's seal
replacement**, which is `HR.Company.Admin`. If you want that beat, open a second window as
**`admin`**; if not, chapter 17 says exactly which step to skip.

### 2.6 Pre-open every screen

Six tabs, left to right:

| Tab | Route | Used in |
|---|---|---|
| 1 | `/hr/company-schedule` | ch. 3 |
| 2 | `/hr/company-schedule/events` | ch. 4–7 |
| 3 | `/hr/company-schedule/bookings` | ch. 8–10 |
| 4 | `/administration/hr/company-schedule` | ch. 11–16 |
| 5 | `/administration/hr/settings/company-profile` | ch. 17 |
| 6 | *(second window, as `admin`)* the same profile page | ch. 17 — **only if** doing the seal |

### 2.7 Prep checklist

- [ ] The three numbers from § 2.2 are written down
- [ ] You have decided about the Tentative-booking beat *(§ 2.3)* and done the switch if so
- [ ] The two busy windows from § 2.4 are written down
- [ ] Window B open as `admin` — **only if** you are doing chapter 17's seal
- [ ] Six tabs open in the order above *(§ 2.6)*
- [ ] You have read **Rules 1–6** and know that **the red Delete button on the event page 403s**

---

## 3. `/hr/company-schedule` — the group landing

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → **Company Schedule** (the group header is the link) ·
`/hr/company-schedule` · as **hr.head** · **3 minutes**

### 📖 What it is

> *"The corporation's own calendar. Not everybody's diary — the occasions the organisation itself
> has: board meetings, the staff durbar, a fire drill, the days the office is shut, the
> anniversaries worth marking. And underneath it, the practical half: who has which room and when."*

### 👁 On the page

**Header:** *Company schedule* — *"Events, room bookings, closures and the milestones on the
company calendar."* No back-link; this is a group root.

**Five navigation cards**, in a grid:

| Card | Goes to |
|---|---|
| **Events** — *Meetings, training days, conferences and company occasions.* | `/hr/company-schedule/events` |
| **Room bookings** — *Who has which room, and what is waiting on approval.* | `/hr/company-schedule/bookings` |
| **Meeting rooms** — *The rooms people can book and the rules for booking them.* | `/administration/hr/company-schedule/rooms` |
| **Business closures** — *Days the organisation is shut, company-wide or per site.* | `/administration/hr/company-schedule/closures` |
| **Milestones** — *Anniversaries, achievements and dates worth marking.* | `/administration/hr/company-schedule/milestones` |

> Three of the five jump into Administration. That is deliberate — an HR officer thinks
> *"where are the rooms"*, not *"is that setup or operations"* — and it is the only place in HR
> where a `/hr` landing links out of its own area.

**Four summary cards**, two by two:

| Card | Shows | Empty state |
|---|---|---|
| **Next 30 days** | up to six upcoming events: name over `date · category · organiser`, with a status badge | *"Nothing scheduled in the next month."* |
| **Bookings awaiting approval (N)** | up to six Tentative bookings: room over `date and time · booked by`, with a status badge. The count is in the card title | *"Nothing is waiting on a decision."* |
| **Closures ahead** *(next 60 days)* | up to six: title over the date or date range · *Whole company* or the site/department, with a **Paid** / **Unpaid** badge | *"No closures in the next two months."* |
| **Milestones ahead** *(next 90 days)* | up to six: title over `date · category` | *"Nothing coming up in the next quarter."* |

Nothing on this page is clickable except the five cards — the summary lists are read-only.

### ▶ Walk it

**1 — Open `/hr/company-schedule`.**

> *"Every organisation has a calendar; most of them have it in somebody's Outlook and a printed
> sheet on a notice board. This is the corporation's own, and it carries the things a company
> calendar actually needs to carry — not just when, but who is coming, what it costs, what has to
> be done before it and who is doing it."*

**2 — Read *Next 30 days* aloud.** Name the three:

> *"The board's Q3 meeting. The annual staff durbar. A fire drill at head office. Three
> occasions, three completely different shapes — and the same record behind all of them."*

**3 — Point at *Closures ahead*.**

> *"The year-end stocktake, the twenty-ninth and thirtieth of December, whole company, paid. Which
> is a different kind of calendar entry: not an occasion, a day the organisation is shut."*

**4 — Point at *Milestones ahead*.**

> *"And the seventy-fourth anniversary, on the eighteenth of October. TDC was founded in 1952 to
> develop the Tema township. That is on the calendar every year, because a corporation that
> forgets its own anniversary has forgotten something."*

**5 — If *Bookings awaiting approval* reads 0**, say so rather than skipping it:

> *"And nothing waiting on a decision, because none of our three rooms is set to need approval —
> which is a choice, made per room. I will turn that on for the boardroom in a moment and you will
> see what it changes."*

**6 — Click *Events*** and continue into chapter 4.

### ⚙ Behind the page

| Card | Endpoint |
|---|---|
| Next 30 days | `GET api/CompanySchedule/events/upcoming?daysAhead=30` |
| Bookings awaiting approval | `GET api/CompanySchedule/bookings/pending-approvals` |
| Closures ahead | `GET api/CompanySchedule/closures/upcoming?daysAhead=60` |
| Milestones ahead | `GET api/CompanySchedule/milestones/upcoming?daysAhead=90` |

All four gated on `HR.Company.Read`. Four separate requests — there is no aggregated dashboard
endpoint here as there is for attendance, and each card slices its own list client-side to six.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-9 · There is no calendar view anywhere in the module.** Every screen is a table. `ShowOnCompanyCalendar` is a field on both events and milestones, and there is no calendar for it to show on | |
| **C-10 · The landing is the only aggregate and it is four requests.** Not a problem at this size; worth knowing it does not scale the way the attendance dashboard does | |

---

## 4. `/hr/company-schedule/events` — the register

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Events** · `/hr/company-schedule/events` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Every occasion the organisation has scheduled, past and future, in one list — with its number,
> its category, when and where it is, who is running it and what state it is in."*

### 👁 On the page

**Header:** *Company events* — *"Meetings, training days, conferences and company-wide
occasions."*, back-link to the landing, and a **+ New event** button.

**Card header carries three controls**, right-aligned:

| Control | Behaviour |
|---|---|
| **Status** dropdown | *All statuses*, or one of the seven event statuses |
| **Category** dropdown | *All categories*, or one of the eight event categories |
| **Search** box | free text over **event name, event number, venue name and organiser** |

All three filter **in the browser** over the full list — there is no server-side search on this
screen. Sorting is fixed: **start date, newest first**.

**Seven columns:**

| Column | Shows |
|---|---|
| **Number** | `EVT-2026-00001`, monospaced |
| **Event** | the name, in bold |
| **Category** | *Company Event*, *Board Meeting*… (PascalCase split into words) |
| **When** | the start date, then `· all day` for an all-day event or `· 09:00` for a timed one |
| **Where** | the venue name, falling back to the site name, falling back to the location type |
| **Organiser** | the organiser's name |
| **Status** | a status badge |

**Rows are clickable** — the whole row opens the event.

**Empty states** distinguish the two cases: *"No events yet — schedule the first company event."*
with a button, versus *"No matching events — try a different search or filter."*

### ▶ Walk it

**1 — Open the register.** Four rows.

**2 — Read them across, top to bottom.** The point is the *variety*, not any one row:

> *"Four events, and look how different they are. A board meeting for fourteen people in the
> boardroom. An all-day durbar for a hundred and twenty on the forecourt. A one-hour fire drill.
> And a two-day management retreat at a hotel in Ho — which is the only one in the past, and the
> only one that is Completed."*

**3 — Point at the *Number* column.**

> *"Every event is numbered — EVT, the year, a sequence. Small thing, and it is the difference
> between 'the durbar' and a record somebody can reference in a memo."*

**4 — Use the *Category* filter.** Choose **Meeting**, then **Company Event**, then back to *All*.

> *"Categories, because a board meeting and a staff durbar do not belong in the same conversation
> even though they are on the same calendar."*

**5 — Type `durbar` in the search.**

> *"And search across the name, the number, the venue and the organiser."*

Clear it afterwards.

**6 — Click the *Management retreat* row** — the completed one — and continue into chapter 6.

⚠ **Do not open the board meeting first.** The retreat is the only event with attendance and an
outcome; leading with it makes chapter 6 twice as good, and you can come back to the board meeting
for the participants tab.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The grid | `GET api/CompanySchedule/events` | `HR.Company.Read` |

Table: `CompanyEvents`, with `Organizer`, `Department` and `SiteLocation` included. **The site
include on the list read was missing until the screens were built** — detail had it, list did not,
so every row's *Where* column fell back to the location type. Found by the harness on the first run.

**The API offers six reads this screen does not use:** paged, by date range, by organiser, by
department, by status and by category. All six are implemented server-side; the screen fetches
everything and filters in the browser (**C-11**).

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-11 · Everything is filtered client-side over an unpaged list.** `GET events` returns every event the tenant has ever had. Fine at four; not fine at four hundred, and `events/paged` already exists | |
| **C-12 · No date filter on the register.** The most obvious question — *"what is on this month?"* — cannot be asked here, though `events/range` exists and the landing page uses `events/upcoming` | |
| **C-13 · No export** | |

---

## 5. `/hr/company-schedule/events/new` — scheduling an occasion

### 📍 Where you are

**From:** the register → **+ New event** · `/hr/company-schedule/events/new` ·
as **hr.head** · **6 minutes**

### 📖 What it is

> *"Everything you need to say about an occasion before it happens — and the form is honest about
> which of those things the system will act on and which it is simply recording."*

### 👁 On the page

**Header:** *New event* — **"You are recorded as the organiser."** *(that one line is the whole
actor design, see § 1.6)*, back-link.

**Five cards.** The same component renders the edit page, so a handful of fields appear on one and
not the other — marked below.

**Card 1 — Basics**
- **Event name** *(required, ≤100)*
- **Description** *(≤1000)*
- **Category** *(required)* · **Type** *(required)*
- **Priority** *(required — Critical · High · Medium · Low)*
- **Status** — **edit only**. A new event is always created *Scheduled*

**Card 2 — When**
- **All-day event** switch — hides the two time fields when on
- **Start date** *(required)* · **End date** *(required, refused if before the start)*
- **Start time** · **End time** *(hidden when all-day)*
- **Repeats** switch — **create only** → **Pattern** *(required when on — Daily · Weekly ·
  Bi-weekly · Monthly · Quarterly · Annually)*, **Number of occurrences**, **Repeat until**,
  **Recurrence notes**

> ⚠ **Recurrence is create-only because the update DTO has no recurrence fields at all.** Rendering
> it on the edit form would silently discard it. And ⚠ **nothing generates the occurrences** — see
> § 1.4 and **C-14**.

**Card 3 — Where**
- **Location type** *(required — On-site · Off-site · Virtual/Online · Hybrid)*
- *When not purely virtual:* **Site** *(the location tree, clearable, "Not tied to a site")*,
  **Venue**, **Venue address**
- *When Virtual or Hybrid:* **Meeting link**, **Meeting password**

**Card 4 — Who**
- **Audience** *(required — All Staff · Department · Selected Individuals · Management Only ·
  External Only)* · **Department** *(clearable, "Company-wide")*
- **Estimated attendees** · **Visibility**
- **Requires RSVP** switch → **RSVP deadline** *(date and time)*
- **Show on company calendar** switch · **Show on intranet** switch

**Card 5 — Approval, budget and logistics**
- **Requires approval before it is confirmed** — **create only**
- **Has a budget** switch → **Budget amount**, and then **Budget code** *(create)* or
  **Actual cost** + **Budget code** *(edit)*
- **Required resources** · **Catering** · **Technical**
- **Send reminders** switch → **Days before**
- **Notes**

**Footer:** **Cancel** · **Schedule event**. On success you land on the new event with a toast
naming its number.

### ▶ Walk it

**1 — Press *+ New event*.** Read the subtitle aloud before touching anything:

> *"'You are recorded as the organiser.' There is no organiser field on this form, and there never
> will be — the system takes it from whoever is signed in. That was a deliberate change: these
> fields used to be settable by the caller, which would have meant anybody could file an event
> under somebody else's name."*

**2 — Fill in the basics.** Name it something real and dateable —
*"Quarterly Heads of Unit meeting"*. Category **Meeting**, Type **Internal**, Priority **High**.

**3 — Set the dates.** Pick a date next month; leave it as a timed event, 09:00 to 13:00.

**4 — Turn *Repeats* on** and choose **Quarterly**. Then say the honest sentence:

> *"Quarterly — and I will be straight about this one. The pattern is recorded on the event; it
> does not yet generate the next three occurrences for you. That is a scheduling job on top of a
> record that already carries the pattern, the count and the end date. Today it is a statement of
> intent, and it is visible on the event."*

Then turn it **off** again — a recurring event you leave behind makes chapter 19's reset messier.

**5 — Card 3, *Where*.** Location type **On-site**, Site → **Tema Head Office · Site**, Venue
*"Boardroom"*.

> *"Two fields, and they are not the same. The site is the entry in the corporation's location
> tree — the same tree the employee register and the geofences use. The venue is what you would
> write on the invitation."*

**6 — Card 4, *Who*.** Audience **Management Only**, Department **Company-wide**, Estimated
attendees **16**, Visibility **Management**, **Requires RSVP** on with a deadline a week before.

**7 — Card 5.** Turn **Has a budget** on, put **8,500** in, budget code *"HRA/EVT/2026/04"*.
Required resources *"Boardroom projector, video link for the Ho office."* Catering *"Tea, coffee
and a working lunch for sixteen."*

> *"And this is where an event stops being a diary entry. A budget with a code that Finance will
> recognise. The resources somebody has to physically produce. The catering somebody has to
> order. That is the difference between a calendar and a plan."*

**8 — 🔴 LIVE WRITE 1 — press *Schedule event*.** The toast names the number; you land on the
event.

> *"EVT-2026-00005. Scheduled."*

*Undo:* chapter 19. `hr.head` cannot delete it — **Cancel** is the exit.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Create | `POST api/CompanySchedule/events` | `HR.Company.Write` |
| Site dropdown | `GET api/Location` | location read |
| Department dropdown | `GET api/hr/departments` | lookup |

Table: `CompanyEvents`. The service sets `TenantId`, `OrganizerId` **from the token**, the event
number, and `Status = Scheduled` — then **re-reads the row before mapping it to a DTO**, because
the graph it just inserted still has null navigations and the response would otherwise answer
`organizerName: ""` and `locationName: null`. That was one of four defects the harness found in
code no screen had ever executed.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-14 · Recurrence generates nothing.** Pattern, count and end date are stored; no occurrence is ever created | |
| **C-6 · The event number counts live rows** — delete one and the next create reuses its number, and the index is not unique | |
| **C-15 · No conflict check of any kind on an event.** Two all-staff events on the same morning are accepted without a murmur. Only *rooms* are protected from double-booking, and an event is not a room booking | |
| **C-16 · The site dropdown lists the whole location tree** — every level, not just sites. Mitigated by suffixing the level on every option; see § 1.6 | |

---

## 6. `/hr/company-schedule/events/[id]` — the event, and its four collections

### 📍 Where you are

**From:** any row of the register · `/hr/company-schedule/events/[id]` · as **hr.head** ·
**10 minutes** — the longest chapter in the book, and the one worth the time

### 📖 What it is

> *"One occasion, in full: what it is, who was asked, who came, what has to be done and by whom,
> and the papers. Plus the four things you can do to it — approve it, move it, close it off, or
> call it off."*

### 👁 On the page

**Header:** the event name, subtitle `EVT-2026-00004 · organised by Akosua Mensah`, back-link, and
**up to five action buttons**:

| Button | Appears when | Does |
|---|---|---|
| **Approve** | `requiresApproval` **and** not yet approved **and** the event is open | fires immediately — no dialog. Sets the approver, the date and the status to *Confirmed* |
| **Reschedule** | the event is open | dialog: new start date/time, new end date/time, **reason** *(all four of date-start, date-end and reason required)* |
| **Complete** | the event is open | dialog: **actual attendance** and **outcome** |
| **Cancel** | the event is open | dialog: **reason** *(required)* |
| 🚫 **Delete** | **always** — *destructive red* | `HR.Company.Admin`. **403 for `hr.head`** |
| **Edit** | always | → the edit page |

*"Open"* means `!isCancelled && status !== 'Completed'`.

**Overview card** — a four-column grid of up to twenty-two fields: Status · Category · Type ·
Priority · Starts · Ends · Repeats · Audience · Location type · Site · Venue · Department ·
*Meeting link* (a **Join** hyperlink, shown only when the location type is not On-site) ·
Expected · Attended · Visibility · *Budget · Actual cost · Budget code* (only when it has a
budget) · *Approved by · date* (only when approved) · *Cancelled* · *Rescheduled*.

**Notes card** — rendered only if there is a description, an outcome summary or notes.

**Four tabs, each with a live count in its label:**

**Tab 1 — Participants (N).** *"Invite an employee, or add an external guest."* Six columns:
**Participant** · **Kind** (*Employee*, or the external organisation) · **Role** · **Required**
(*Yes* / *Optional*) · **Invitation** (a status badge) · **Responded** (a date).
Row ⋯ menu carries **Record accepted**, **Record declined**, **Record tentative** *(each hidden
when the participant is already at that status)* and 🚫 **Remove participant**.
**No Edit** — the API has no participant update, so rows are added and removed, never amended.

Dialog: **Employee** *(picker)* · *"Or add someone from outside the organisation:"* ·
**External name** · **External email** *(validated)* · **External organisation** · **Role**
*(required)* · **Attendance required** switch · **Special requirements**.
Two rules the form enforces: you must give **either** an employee **or** an external name, and
**not both**.

> ⚠ **Adding a participant *is* inviting them.** The service sets `InvitationStatus = Sent` and
> stamps `InvitationSentDate` on insert. There is no separate "send invitations" action — and no
> invitation is actually sent anywhere (**C-17**).

**Tab 2 — Attendance (N).** *"You are recorded as the person who marked it."* Six columns:
**Employee** · **Attended** (a *Present* / *Absent* badge) · **Checked in** · **Checked out** ·
**Reason** · **Marked by**. Row ⋯ carries **Check out** *(only on an attended row with no
check-out yet)*. **No Edit and no Remove** — marking the same employee again is how a record is
corrected, and there is no delete endpoint at all.

Dialog: **Employee** *(required)* · **Attended** switch → **Check-in time** *(when attended)* or
**Reason for absence** *(when not)* · **Notes**.

**Tab 3 — Tasks (N).** *"Everything that has to happen before, during and after the event."* Six
columns: **Task** · **Stage** · **Assigned to** · **Due** · **Priority** · **Status**.
Row ⋯ carries **Mark complete** *(hidden once Completed or Cancelled)*, **Edit** and 🚫 **Remove
task**.
Dialog: **Task** *(required, ≤1000)* · **Stage** *(Pre-Event Preparation · During Event ·
Post-Event Follow-up)* · **Priority** · **Assigned to** *(picker)* · **Due date** · **Status**
*(edit only — a new task is always Not Started)*.

**Tab 4 — Attachments (N).** *"Agendas, minutes, presentations and handouts for this event."* Four
columns: **File** · **Type** · **Description** · **Uploaded**. Row ⋯ carries 🚫 **Remove
attachment** only.
Dialog: **File name** *(required)* · **Type** *(Agenda · Minutes · Presentation · Handout ·
Resource Material)* · **File path** *(required)* · **Description**.

> ⚠ **This is a reference, not an upload.** The endpoint takes a file name and a stored path as
> JSON; no bytes move, there is no file picker and there is no download link. The row points at
> the document register rather than carrying the document (**C-18**).

### ▶ Walk it

Ten minutes. Take them.

**1 — Open the *Management retreat — 2026 budget preparation*.**

> *"The management retreat, five weeks ago. Two days at the Volta Serene in Ho, to settle the 2026
> operating budget and the manpower plan. This is the only event on this calendar that has already
> happened, which makes it the only one with a full record."*

**2 — Read the Overview card, following the grid.**

> *"Status Completed. Meeting, internal, management only — and visibility management, so it is not
> on the all-staff view. Sixty-two thousand cedis against MD/EVT/2026/01. Eighteen expected."*

**3 — Point at *Expected* and *Attended* side by side.**

> *"Eighteen expected, four attended — and that gap is the reason both numbers are on the record
> rather than one. In a paper system you get the invitation list or the register, never both."*

**4 — Scroll to the *Notes* card and read the outcome aloud.** This is the single best line on the
demo database:

> *"'Operating budget agreed at GHS 214 million with a 6% contingency; manpower plan referred back
> to HR for costing; Community 25 phasing endorsed.' That is what closing an event off means here.
> Not archiving it — writing down what it decided."*

**5 — Open the *Attendance* tab.**

> *"Five rows. Four present, with their check-in times against the register they signed. And one
> absent, with a reason: 'On annual leave; represented by the Deputy Internal Auditor.' That is
> what a minute secretary writes, and it is on the record rather than in somebody's notebook."*

**6 — Open the *Attachments* tab.**

> *"And the communiqué. One point of honesty: this holds a reference to the document rather than
> the document itself — the file name and where it is stored. Attachments across this product go
> through a single controlled upload gate, and wiring this register onto that gate is the change
> that makes it a download link."*

**7 — Now go back and open the *Board of Directors — Q3 meeting*.** This is where the live writes
happen.

**8 — Open the *Participants* tab.** Five employees and **one external**.

> *"Five internal, and Nana Kwame Baffoe from the Board Secretariat — an external participant,
> with his organisation and his email, as a Facilitator. An event's guest list is not the staff
> list, and the model knows the difference."*

**9 — Point at the *Invitation* column.** Two rows read **Accepted** and **Tentative** with
response dates; the rest read **Sent**.

> *"'Will attend.' 'Joining for the first session only.' The RSVPs are on the record."*

**10 — 🔴 LIVE WRITE 2 — press *Add participant*.** Pick an employee, Role **Attendee**,
Attendance required **on**. Add.

> *"And adding somebody is inviting them — the invitation goes out at the moment you add the row,
> there is no second 'send' button. Which is the right model; it is also the piece that is not yet
> wired to anything that sends an email."*

**11 — 🔴 LIVE WRITE 3 — on your new row, ⋯ → *Record accepted*.** The badge flips and the
Responded date fills.

> *"And HR records a reply that came back by phone. Which is the honest description of this
> control — it says 'record', not 'respond', because the person answering is not the invitee."*

**12 — Open the *Tasks* tab.** Two rows on the board meeting.

> *"Circulate the board pack seven days before — Critical, assigned to the Managing Director. Take
> the minutes and produce the draft within three working days — assigned to the Head of HR. Before
> and during, with owners and due dates."*

**13 — 🔴 LIVE WRITE 4 — *Add task*.** *"Book the video link to the Ho office"*, Stage
**Pre-Event Preparation**, Priority **High**, assign it to someone, due next week.

**14 — 🔴 LIVE WRITE 5 — ⋯ → *Mark complete* on it.** The status badge flips to *Completed*.

**15 — 🔴 LIVE WRITE 6 — press *Approve* in the header.** It fires with no dialog; the status goes
to **Confirmed** and *Approved by* appears on the Overview card.

Then say the honest sentence — **do not skip this one**:

> *"Approved. And I want to be precise about what that is, because everywhere else in this product
> approval means the workflow engine — a definition, a routed step, a named approver, an entry in
> their inbox. Here it is a decision recorded in place: who approved it and when. For a room
> booking that is the right weight. For a board meeting with a fifty-thousand-cedi budget, TDC may
> want it routed — and the engine is already in the product approving leave, travel and
> requisitions. Putting a company event onto it is the same four-step change we made for every
> other module."*

**16 — Show *Reschedule* without pressing it.** Open the dialog, then **Cancel** it.

> *"Moving an event needs a reason — the dialog will not submit without one."*

⚠ **Read Rule 2 before you say anything about history here.** The dialog's own line —
*"The original dates are kept on the record as history"* — is not true; the dates are overwritten
and nothing stores the old ones. If you want to demonstrate it, say:

> *"Move it, with a reason, and the record carries that it was moved, by when and why."*

**17 — 🚫 DO NOT PRESS *Delete*.** It is the red one, top right, and it 403s. If you want to make
the point:

> *"Notice that the destructive action and the reversible one are different buttons with different
> weight. Cancel keeps the event, its invitations, its attendance and its reason. Delete removes
> the lot — and it is an administrator's permission, not an HR officer's. I could not do it from
> this account if I wanted to."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/events/{id}/details` | `HR.Company.Read` |
| **Approve** | `POST api/CompanySchedule/events/{id}/approve` *(empty body)* | `HR.Company.Write` |
| **Cancel** | `POST …/{id}/cancel` | `HR.Company.Write` |
| **Reschedule** | `POST …/{id}/reschedule` | `HR.Company.Write` |
| **Complete** | `POST …/{id}/complete` | `HR.Company.Write` |
| **Delete** | `DELETE …/{id}` | **`HR.Company.Admin`** |
| Participants | `GET`/`POST …/{eventId}/participants` · `POST …/participants/respond` | Read / Write |
| Remove participant | `DELETE …/participants/{participantId}` | **`HR.Company.Admin`** |
| Attendance | `GET`/`POST …/{eventId}/attendance` · `POST …/attendance/{id}/checkout` | Read / Write |
| Tasks | `GET`/`POST …/{eventId}/tasks` · `PUT …/tasks/{id}` · `POST …/tasks/{id}/complete` | Read / Write |
| Remove task | `DELETE …/tasks/{id}` | **`HR.Company.Admin`** |
| Attachments | `GET`/`POST …/{eventId}/attachments` | Read / Write |
| Remove attachment | `DELETE …/attachments/{id}` | **`HR.Company.Admin`** |

Tables: `CompanyEvents`, `EventParticipants`, `EventAttendances`, `EventTasks`,
`EventAttachments`.

**Approve sends an empty POST on purpose.** The approver comes from the token; passing an id would
be act-as-anyone, which is exactly what this endpoint used to allow.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-1 · The red Delete button 403s for `hr.head`** — Rule 1 | |
| **C-2 · Rescheduling overwrites the dates and the dialog claims otherwise** — Rule 2 | |
| **C-19 · There is no lifecycle guard on the server.** The screen hides the four actions once an event is cancelled or completed; the API does not. A completed event can be cancelled, a cancelled one completed, and either rescheduled. Every other HR module refuses these | |
| **C-20 · Approve ignores `RequiresApproval`.** It sets *Confirmed* on any open event, whether or not one was asked for | |
| **C-17 · No invitation is sent.** `InvitationStatus = Sent` is a field, not an email | |
| **C-18 · Attachments are references, not files** — no upload, no download | |
| **C-21 · An attendance record cannot be removed.** There is no delete endpoint; a wrong row is corrected by marking again, which leaves both | |
| **C-22 · A participant cannot be edited.** No update endpoint — remove and re-add, and removal is Admin, so `hr.head` cannot correct a typo in an external guest's email at all | |
| **C-23 · `EventTaskStatus.Overdue` is never set.** A task past its due date stays *Not Started* | |

---

## 7. `/hr/company-schedule/events/[id]/edit` — amending an occasion

### 📍 Where you are

**From:** the event → **Edit** · `/hr/company-schedule/events/[id]/edit` · as **hr.head** ·
**2 minutes**

### 📖 What it is

> *"The same authoring form, with two halves swapped: what you can only set when an event is
> created is gone, and what only makes sense afterwards has appeared."*

### 👁 On the page

**Header:** *Edit \<event name\>* — **"Recurrence is set when the event is created and cannot be
changed here."**, back-link to the event.

The form is chapter 5's, with four differences:

| Field | Create | Edit |
|---|---|---|
| **Status** | absent — always *Scheduled* | **present** — all seven statuses |
| **Repeats** and the four recurrence fields | present | **absent** |
| **Requires approval** | present | **absent** — *"once an event exists, approval is an action, not a checkbox"* |
| **Actual cost** | absent | **present**, beside the budget amount |

**Footer:** **Cancel** · **Save changes**.

### ▶ Walk it

**1 — From the board meeting, press *Edit*.**

**2 — Point at the *Status* dropdown in Card 1** — it is the visible difference.

> *"And here is the one field that only exists after the event does. Seven states, and three of
> them — In progress, Postponed, Rescheduled — are only reachable from this dropdown. The buttons
> on the event set Confirmed, Completed and Cancelled; anything else is a judgement somebody makes
> by hand."*

**3 — Point at the header line about recurrence.**

> *"And what has gone. Recurrence is set once, when the event is created, because changing the
> pattern of something that has already started repeating is a different and much harder question.
> The form does not show it rather than showing it and quietly ignoring it — which is what would
> happen, because the update endpoint has no recurrence fields at all."*

**4 — Turn *Has a budget* on if it is not already**, and point at **Actual cost**.

> *"And what has appeared. A budget is what you asked for; the actual cost is what it came to. You
> cannot know the second one when you are creating the event, so the form does not ask."*

**5 — Press *Cancel*.** Nothing to write here — the live writes are all on the event page.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Load | `GET api/CompanySchedule/events/{id}` | `HR.Company.Read` |
| Save | `PUT api/CompanySchedule/events/{id}` | `HR.Company.Write` |

Table: `CompanyEvents`. The update read includes the three navigations, so the response carries
resolved names without a re-read — unlike create.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-24 · The status dropdown accepts any transition.** *Completed* → *Scheduled* is allowed, and setting *Cancelled* here does **not** set `IsCancelled`, the cancellation date or a reason — so an event cancelled from the edit form still shows its action buttons and reads as open everywhere except the status badge | |

---

## 8. `/hr/company-schedule/bookings` — who has which room

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Room Bookings** · `/hr/company-schedule/bookings` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Every room held, by whom, when and what for — and the approvals still outstanding. This is the
> screen a receptionist lives on."*

### 👁 On the page

**Header:** *Room bookings* — *"Who has which room, when, and which bookings are still waiting on
approval."*, back-link, and a **Book a room** button.

**Card header:** a **Status** dropdown (*All statuses* + the five booking statuses) and a
**Search** box over **booking number, room name, purpose and booker**. Both filter in the browser;
sorting is fixed to **start date, newest first**.

**Nine columns:** **Number** *(monospaced)* · **Room** · **From** · **To** *(both full date and
time, in the browser's locale)* · **Purpose** · **Seats** · **Booked by** · **Status** · ⋯

Rows are clickable into the booking. The ⋯ menu carries:

| Item | Shown when |
|---|---|
| **Approve** | status is *Tentative* and not cancelled |
| **Cancel** | not cancelled and not *Completed* — opens a dialog needing a **reason** |
| 🚫 **Delete** | **always**, in red — `HR.Company.Admin`, **403 for `hr.head`** |

Cancel's dialog: *"\<room\> — the slot is released for someone else."*
Delete's confirmation: *"Cancelling keeps the record and the reason. Deleting removes it
entirely."* — a good sentence you cannot act on.

### ▶ Walk it

**1 — Open the register.** Two rows.

> *"Two rooms held. The Management Committee has the boardroom for three hours on Tuesday morning
> — with a video link to the Ho office in the requirements. And Development has Conference Room A
> on Thursday afternoon for the Community 25 design review with the consultants."*

**2 — Point at *Booked by*.**

> *"And note the second one was not booked by HR. It was booked by the Head of Development, from
> his own account — the system records who booked it, from the token, not from a field somebody
> typed."*

**3 — Point at the *Status* column.** Both read **Confirmed**.

> *"Both confirmed, because neither of those rooms is set to need approval. Which is a per-room
> decision, and I will show you what changes when you turn it on."*

**4 — Open the *Status* filter** and read the five values.

> *"Tentative, Confirmed, Completed, Cancelled, No show. The last two matter to a facilities
> manager more than anyone else — a room held and not used is a room somebody else could have
> had."*

⚠ Do not promise *Completed* or *No show* will ever appear — nothing sets them (**C-8**).

**5 — 🚫 DO NOT PRESS ⋯ → *Delete*.** Admin. Use the sentence from its own dialog:

> *"'Cancelling keeps the record and the reason. Deleting removes it entirely.' Which is exactly
> right, and it is why cancelling is an HR officer's action and deleting is an administrator's."*

**6 — Press *Book a room*** and continue into chapter 9.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The grid | `GET api/CompanySchedule/bookings` | `HR.Company.Read` |
| Approve | `POST …/bookings/{id}/approve` *(empty body)* | `HR.Company.Write` |
| Cancel | `POST …/bookings/{id}/cancel` | `HR.Company.Write` |
| Delete | `DELETE …/bookings/{id}` | **`HR.Company.Admin`** |

Table: `RoomBookings`, with `Room` and `BookedBy` included. The API also offers paged, by room, by
booker, by date range, by status and pending-approvals reads — the screen uses none of them except
pending-approvals, on the landing page (**C-25**).

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-25 · Unpaged and filtered client-side**, like the events register, with six unused server reads including a date range | |
| **C-8 · *Completed* and *No show* are unreachable.** Nothing marks a booking used or unused after the fact, so the facilities question the status enum was built to answer cannot be asked | |
| **C-26 · No room view.** There is no "show me the boardroom's week", though `bookings/room/{roomId}` exists. This is the single most requested view of a booking register and it is one screen away | |

---

## 9. `/hr/company-schedule/bookings/new` — the availability search

### 📍 Where you are

**From:** the register → **Book a room** · `/hr/company-schedule/bookings/new` ·
as **hr.head** · **6 minutes** — **the best chapter in the module**

### 📖 What it is

> *"Booking a room the right way round: say when you need it and how many of you there are, and
> the system tells you what is free. Not a list of every room and a disappointment at the end."*

### 👁 On the page

**Header:** *Book a room* — **"You are recorded as the person booking."**, back-link.

**Card 1 — When and how many**
- **From** *(date and time, required)* · **To** *(date and time, required — refused if not after
  the start)*
- **Expected attendees** *(required, at least 1)*
- **Find free rooms** button — **disabled until there is a valid window**

**Card 2 — Which room** — three states:

| State | Shows |
|---|---|
| before the search | *"Set the window first — rooms are offered once there is a start and an end to check them against."* |
| searching | *"Checking what is free…"* with a spinner |
| nothing free | *"Nothing free in that window — try a different time, or a smaller number of attendees."* |
| rooms free | a **Room** dropdown (`Boardroom · Tema Head Office · seats 18`) **and** the same rooms as badges below it with their capacities |

**Card 3 — What for**
- **Purpose** *(required, ≤500)*
- **Linked event** *(a dropdown of the next 120 days' events, `EVT-2026-00001 — Board of
  Directors…`, clearable: "Not linked to an event")*
- **Special requirements** · **Catering**
- **Notes**

**Footer:** **Cancel** · **Book room**. The toast names the booking number and, when the room
needs approval, adds *"It needs approval before it is confirmed."*

The failure toast is deliberately worded for the race: *"The room may have been taken while you
were filling this in."*

### ▶ Walk it

This is the chapter to slow down on. Do the refusal first — it is more convincing than the success.

**1 — Press *Book a room*.** Point at Card 2's empty state before touching anything.

> *"Notice what it will not do. There is no room dropdown yet, because the question 'which room'
> has no answer until you have said when. Offering every room and letting the booking fail at the
> end is the same screen built backwards."*

**2 — Now demonstrate the clash. Set From and To to the window you wrote down in § 2.4** — the
Boardroom's Tuesday morning. Expected attendees **12**.

**3 — Press *Find free rooms*.**

> *"Three rooms in this building, and the boardroom is not on the list — because the Management
> Committee has it. The system is not warning me; it is not offering it."*

**4 — Now force the refusal, which is the moment worth having.** Change **To** to extend an hour
past the existing booking — so the window still overlaps — and press **Find free rooms** again.
The boardroom is still absent. Then say:

> *"And if I got round the picker — pasted a room id, used the API directly, had two people
> booking at the same instant — the server refuses it anyway. The clash check runs on the way in,
> not just on the way out, and it runs again on every edit. A room in this system genuinely cannot
> be double-booked."*

⚠ **Do not try to actually force a 409 in front of the room.** The picker will not let you select
an excluded room, so there is nothing to click; describing it is the honest version and it lands
just as well.

**5 — Now book something real. Set a window that is free** — a different day, 14:00 to 16:00.
Expected attendees **10**. Press **Find free rooms**.

**6 — Read the dropdown option aloud.**

> *"'Boardroom · Tema Head Office · seats 18.' The site and the capacity are on the option, so
> nobody books a huddle room for a management meeting by accident."*

**7 — Pick a room, fill in Card 3.** Purpose *"Quarterly Heads of Unit meeting"*. **Link it to the
event you created in chapter 5** from the *Linked event* dropdown.

> *"And this is where the two halves of the module meet. The room is held for the event — one
> record, and the booking knows what it is for."*

**8 — 🔴 LIVE WRITE 7 — press *Book room*.**

*If you did the § 2.3 switch on the Boardroom, the toast reads:*

> *"'BK-2026-00003 — Boardroom. It needs approval before it is confirmed.' Because that room is set
> to need approval, and the booking came out Tentative rather than Confirmed. A room that does not
> need approval books straight through. Same form, different room, different outcome — and the
> rule is on the room, not on the person."*

*If you did not:*

> *"'BK-2026-00003 — Boardroom.' Confirmed immediately, because that room does not require
> approval. Turn that setting on for a room and the same booking comes out Tentative and waits for
> somebody — I will show you the switch in a moment."*

*Undo:* chapter 19 — **Cancel** it, with a reason.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| **Find free rooms** | `GET api/CompanySchedule/rooms/available?startDateTime&endDateTime&minCapacity` | `HR.Company.Read` |
| Linked-event dropdown | `GET api/CompanySchedule/events/upcoming?daysAhead=120` | `HR.Company.Read` |
| **Book** | `POST api/CompanySchedule/bookings` | `HR.Company.Write` |

Tables: `MeetingRooms`, `RoomBookings`.

**What availability actually does:** collects the room ids with any booking that is not cancelled
and whose window overlaps the requested one, then returns the rooms that are **active**,
**bookable**, not in that list, and — when a capacity is given — big enough. Ordered by name.

**What create does:** loads the room and refuses it if it is not bookable; runs the same overlap
test again; generates the booking number; and sets the status from the **room's** `RequiresApproval`
— *Tentative* if it needs one, *Confirmed* if it does not. Then re-reads before mapping, for the
same navigation reason as events.

**The overlap test**, in full, because somebody will ask:

```
    same room
      AND not cancelled
      AND existing.Start <  requested.End
      AND existing.End   >  requested.Start
```

Which is the correct half-open interval test: a booking ending at 12:00 and one starting at 12:00
do **not** clash.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-4 · The room's max duration and advance-booking window are not enforced** — Rule 4 | |
| **C-27 · Capacity is a filter, not a rule.** The availability search respects it; the create endpoint does not. Book through the API for 40 people in the 6-seat huddle room and it is accepted | |
| **C-28 · The availability query is not tenant-filtered.** The repository collects booked room ids across every tenant before the service filters the *rooms* by tenant. Harmless — room ids do not collide across tenants — but it is the only read in the module that does not scope explicitly | |
| **C-6 · The booking number counts live rows** and can repeat after a delete | |
| **C-29 · A linked event is a label, not a link with consequences.** Cancelling the event does not touch the booking, and cancelling the booking does not touch the event | |

---

## 10. `/hr/company-schedule/bookings/[id]` — one booking

### 📍 Where you are

**From:** any row of the bookings register · `/hr/company-schedule/bookings/[id]` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"One held room, with everything about it editable in place while it is still open — except the
> room itself."*

### 👁 On the page

**Header:** the **room name** as the title, subtitle `BK-2026-00001 · booked by Akosua Mensah`,
back-link, and up to two buttons:

| Button | Appears when |
|---|---|
| **Approve** | status is *Tentative* and the booking is open |
| **Cancel** | the booking is open — dialog needing a **reason** |

There is **no Delete on this page** — it is only on the register's row menu. *(Which is arguably
backwards, but it means the dangerous button is not next to the ones you want.)*

**Booking card** — a four-column grid: **Status** · **Booked on** · **From** · **To** ·
**Linked event** · **Approved by** *(name and date)* · and, when cancelled, **Cancelled** with its
date and reason.

**Details card** — and this is the interesting part: it is **a live edit form while the booking is
open**, and a read-only grid once it is cancelled or completed.

*While open:*
- a grey note: *"To move this to a different room, cancel it and book again — that is the only way
  the availability check runs against the new room."*
- **From** · **To** *(date and time, required, end must be after start)*
- **Purpose** *(required)* · **Expected attendees** *(required, ≥1)*
- **Special requirements** · **Catering** · **Notes**
- **Save changes**

*Once closed:* the same five values as plain read-only rows.

> ⚠ **The room is not on the form, deliberately.** `UpdateRoomBookingDto` has no `roomId`, so
> rendering a room picker would be rendering a field the server ignores. **Editing the times *does*
> re-run the clash check** — so you can extend a booking into a free hour and be refused if you
> extend into a taken one.

### ▶ Walk it

**1 — Open the booking you created in chapter 9.**

**2 — Read the Booking card.**

> *"The number, when it was booked, the window, and the event it is held for."*

**3 — 🔴 LIVE WRITE 8 — if it is Tentative, press *Approve*.** The status flips to **Confirmed**
and *Approved by* fills with your name and the time.

> *"Approved, and the room is now held firmly rather than provisionally. Same caveat as an event:
> this is a decision recorded in place, not a routed approval — and for a meeting room that is the
> right weight."*

**4 — Move to the *Details* card and read the grey note aloud.** It is a good design statement:

> *"'To move this to a different room, cancel it and book again — that is the only way the
> availability check runs against the new room.' Which is a decision, not a limitation. If you
> could swap the room in a dropdown here, the only thing standing between you and a double booking
> would be the same check running again in a different place. Making it a cancel-and-rebook means
> there is exactly one path into a room, and it is the one with the availability search on it."*

**5 — Extend the booking by an hour** — change **To** — and press **Save changes**.

> *"And changing the window re-runs the clash check. That is not a create-time rule, it is the
> rule."*

*(Counts as part of LIVE WRITE 8; undo with the same cancel.)*

**6 — Show *Cancel* without pressing it** — unless you are resetting now, in which case this is
your undo. The dialog needs a reason:

> *"Cancelling releases the slot for somebody else, and it needs a reason. The booking stays on the
> record with that reason on it — which is the difference between cancelling and deleting, and it
> is why one of those is an HR officer's job."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/bookings/{id}` | `HR.Company.Read` |
| Save changes | `PUT api/CompanySchedule/bookings/{id}` | `HR.Company.Write` |
| Approve | `POST …/{id}/approve` *(empty body)* | `HR.Company.Write` |
| Cancel | `POST …/{id}/cancel` | `HR.Company.Write` |

Table: `RoomBookings`. Update re-runs `HasConflictingBookingAsync` with the booking's **own id
excluded**, so saving an unchanged window does not clash with itself.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-30 · Approve has no guard.** The screen shows it only on a *Tentative* booking; the service will confirm anything, including an already-cancelled booking | |
| **C-31 · Cancel does not check the status either.** A cancelled booking can be cancelled again, overwriting the first reason and date | |
| **C-32 · There is no delete on the detail page**, only on the register — inconsistent, though in practice a helpful accident | |

---

## 11. `/administration/hr/company-schedule` — the setup hub

### 📍 Where you are

**Sidebar:** Administration → HR → **Company Schedule** ·
`/administration/hr/company-schedule` · as **hr.head** · **1 minute**

### 📖 What it is

> *"Four things the corporation decides once a year and then lives with: which rooms exist, which
> days it is shut, which dates it marks, and what its financial year is."*

### 👁 On the page

A header (*Company Schedule* — *"Meeting rooms, closures, milestones and fiscal years."*, back-link
to the HR Administration hub) above **four navigation cards**:

| Card | Route | Chapter |
|---|---|---|
| **Meeting Rooms** — *Rooms, their facilities, and the rules for booking them.* | `…/rooms` | 12 |
| **Business Closures** — *Days the organisation is shut, company-wide or per site.* | `…/closures` | 13 |
| **Milestones** — *Anniversaries, achievements and other calendar dates.* | `…/milestones` | 14 |
| **Fiscal Years** — *Reporting windows and the periods inside them.* | `…/fiscal-years` | 15 |

### ▶ Walk it

**1 — Open the hub.** Read the four titles.

> *"Four cards, and the sentence that explains why they are over here rather than with the events:
> an HR officer touches an event every week and a fiscal year once a year. Same permission, very
> different rhythm — so the daily work is under Human Resources and the annual decisions are under
> Administration."*

**2 — Click *Meeting Rooms*.**

### ⚙ Behind the page

No API call — the cards are hard-coded on the page, mirroring the group in
`frontend/src/config/hr-setup-nav.ts`. Each target enforces its own permission.

---

## 12. `/administration/hr/company-schedule/rooms` — the rooms

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Meeting Rooms** ·
`…/rooms` (+ `/new`, `/[id]/edit`) · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Every room somebody can book, with what is in it and the rules for booking it. This is where
> the availability search gets its answers from."*

### 👁 Screen 1 — the register

**Header:** *Meeting rooms* — *"The rooms people can book, and the rules for booking them."*,
back-link, **+ New room**, and a **Search** box in the card header (over **room name, room code,
site and placement**). Sorted by room name.

**Ten columns:**

| Column | Shows |
|---|---|
| **Code** | `BRD`, monospaced, or an em dash |
| **Room** | the name, bold |
| **Site** | the location-tree entry |
| **Where** | `Main · 4 · Tema Head Office` — building, floor and placement joined |
| **Seats** | capacity |
| **Type** | Conference · Boardroom · Training · Huddle · Auditorium |
| **Facilities** | small badges: *Projector · Whiteboard · VC · Audio · A/C* |
| **Bookable** | **Yes** · **With approval** · **No** |
| **Status** | Active / Inactive |
| ⋯ | **Edit**, 🚫 **Delete** |

**Rows are not clickable** — only the ⋯ menu moves you on.

Delete's confirmation is the best-written one in the module: *"\<room\> will be removed. Deactivate
it instead if it has booking history worth keeping."* ⚠ Its failure toast says *"It may have
bookings against it"*, which is misleading — for `hr.head` the failure is always a 403, never a
constraint (**C-33**).

### 👁 Screen 2 — new / edit (`…/rooms/new`, `…/rooms/[id]/edit`)

One form, three cards.

**Card 1 — The room**
- **Room name** *(required, ≤100)* · **Room code** *("Generated if left blank")*
- **Site** *(required — the location tree, with each option suffixed by its level)* ·
  **Room type** *(required)*
- **Where in the site** *(required, ≤200, "e.g. East wing, past reception")*
- **Building** · **Floor**
- **Seats** *(required, ≥1)*
- **Description**

**Card 2 — Facilities** — five switches: **Projector** · **Whiteboard** · **Video conferencing** ·
**Audio system** · **Air conditioning**, plus **Anything else** *(free text)*.

**Card 3 — Booking rules**
- **Active** switch · **Can be booked** switch
- *When bookable:* **Bookings need approval** switch — *"A booking stays Tentative until somebody
  approves it."* — and **Longest booking (hours)** and **Book up to (days ahead)**, both
  placeholdered *"No limit"*

**Footer:** **Cancel** · **Add room** / **Save changes**.

> **Room code is generated when blank** as `RM-0001` from a count of live rooms — and the duplicate
> check only runs on a code you type, not on a generated one, so a generated code can collide with
> an existing one after a delete (**C-34**).

### ▶ Walk it

**1 — Open the register.** Three rows.

> *"Three rooms, all at head office. The Boardroom on the fourth floor, eighteen seats, with video
> conferencing and an audio system. Conference Room A on the second, thirty seats. And Huddle Room
> 1 on the third, six seats — for the meetings that do not need a table."*

**2 — Point at the *Facilities* column.**

> *"Projector, whiteboard, video conferencing, audio, air conditioning — because 'which room' is
> usually decided by 'can we get the Ho office on the screen', not by how many chairs there are."*

**3 — Point at *Bookable*.** All three read **Yes**.

> *"All three bookable, none requiring approval. Watch what the third value does."*

**4 — ⋯ → *Edit* on the Boardroom.** Walk the three cards.

**5 — On Card 1, stop at *Site* and *Where in the site*** — this is § 1.6's second sentence:

> *"Two fields that sound the same and are not. The site is the entry in the corporation's location
> tree — the same tree the employee register uses, and the same one the attendance geofences hang
> off. 'Where in the site' is what you would say to a visitor: east wing, past reception. Both are
> required, because a room with a site and no placement is a room nobody can find."*

**6 — Point at the level suffix in the Site dropdown.**

> *"And every option says what level it is, because that list is the whole tree — Ghana and Greater
> Accra are in there alongside Tema Head Office. Rather than guess which level counts as a site for
> every tenant, it tells you and lets you choose."*

**7 — Card 3 — 🔴 LIVE WRITE 9 — turn *Bookings need approval* on** and press **Save changes**.
The register's **Bookable** column now reads **With approval**.

> *"And that is the third value. Nothing else changed — same room, same capacity, same facilities.
> But the next person who books it gets a Tentative booking instead of a confirmed one, and
> somebody has to say yes. The rule lives on the room, not on the person booking and not on a
> global setting, because 'the boardroom needs approval and the huddle room does not' is exactly
> how a real organisation works."*

*Undo:* chapter 19 — turn it back off.

**8 — Point at the two numeric fields underneath it, and be honest:**

> *"Two more rules sitting beside it — the longest a single booking may be, and how far ahead
> people may book. Those are recorded and not yet enforced; today the enforced rules are 'is the
> room bookable' and 'is the slot free'. Which happen to be the two that actually prevent an
> argument."*

**9 — 🚫 DO NOT PRESS ⋯ → *Delete*.** Admin. Use its own dialog line:

> *"'Deactivate it instead if it has booking history worth keeping' — which is the right answer
> almost always. A room that is gone is not a room that never existed."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/rooms` | `HR.Company.Read` |
| Load one | `GET …/rooms/{id}` | `HR.Company.Read` |
| Create | `POST …/rooms` | `HR.Company.Write` |
| Update | `PUT …/rooms/{id}` | `HR.Company.Write` |
| Delete | `DELETE …/rooms/{id}` | **`HR.Company.Admin`** |
| Site dropdown | `GET api/Location` | location read |

Table: `MeetingRooms`. Create **validates the site exists in this tenant before saving** — without
that check a bad id reached SaveChanges and came back as an unhandled FK violation reading
*"Something went wrong while processing your request"*, which looks like an outage rather than a
bad selection. Found by the harness on the first run, and the tenant half of the check matters as
much as the existence half.

The API also offers `rooms/paged`, `rooms/location/{locationId}` and `rooms/active` — none used by
this screen.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-4 · Max duration and advance-booking days are stored and never read** | |
| **C-33 · The delete failure toast guesses the wrong reason** — it blames bookings when the real answer is a 403 | |
| **C-34 · A generated room code can collide.** The count-based generator skips the duplicate check that a typed code gets | |
| **C-35 · There is no room detail page.** `rooms/[id]` has only an `edit` child, so there is nowhere to see a room's bookings — which is the thing a facilities manager wants most (and see **C-26**) | |
| **C-36 · Deactivating a room does not touch its future bookings.** They stay confirmed and the room simply stops appearing in the availability search | |

---

## 13. `/administration/hr/company-schedule/closures` — the days the office is shut

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Business Closures** · `…/closures` ·
as **hr.head** · **5 minutes**

### 📖 What it is

> *"A holiday calendar says which days the country is off. This says which days **TDC** is off —
> the year-end stocktake, a site closed for rewiring, a department away at a conference. Over and
> above the statutory calendar, and announced by a named person."*

### 👁 On the page

**Header:** *Business closures* — *"Days the organisation is closed, company-wide or for one site
or department."*, back-link.

**+ Add closure** above a table. Dialog hint: **"You are recorded as the person announcing it."**

**Seven columns:**

| Column | Shows |
|---|---|
| **Title** | the closure's title |
| **Type** | *Full Closure* · *Partial Closure* · *Department Closure* · *Station Closure* |
| **When** | one date, or `29 Dec → 30 Dec` for a range |
| **Applies to** | **Whole company**, or the site and department joined |
| **Paid** | a **Paid** / **Unpaid** badge |
| **Working day** | *Counts* / *Does not count* |
| **Announced by** | the announcer's name |

Row ⋯: **Edit**, 🚫 **Remove closure**.

**Dialog** *(wider than standard, 640px)*:
- **Title** *(required, ≤200)*
- **Type** *(required)*
- **From** *(required)* · **To** *(required — refused if before the start)*
- **Affects the whole company** switch — *"Turn this off to close a single site or department."*
- *When off:* **Site** *(clearable, "Any site")* and **Department** *(clearable, "Any
  department")*
- **Staff are paid** switch
- **Counts as a working day** switch — *"Affects leave and attendance calculations."*
- **Reason** · **How it was communicated**

The form enforces one cross-field rule: **company-wide, or a site, or a department — you cannot
leave all three blank**.

### ▶ Walk it

**1 — Open the screen.** One row: *Year-end stocktake*, Full Closure, 29 Dec → 30 Dec, Whole
company, **Paid**, Does not count.

**2 — Read it across.**

> *"The year-end stocktake. Two days, the twenty-ninth and thirtieth of December, the whole company.
> Staff are paid. And it does not count as a working day — which is the field that matters, because
> it is the difference between 'you were off and it cost you nothing' and 'you were off and it came
> out of your leave'."*

**3 — ⋯ → *Edit*** and open the dialog. Turn **Affects the whole company** off to reveal the two
pickers, then turn it back on.

> *"And a closure does not have to be the whole company. Turn that off and you close one site — the
> Ashaiman office for rewiring — or one department. Which is how closures actually happen."*

**4 — Point at *Announced by* and the dialog hint.**

> *"'You are recorded as the person announcing it.' Same rule as the event organiser and the room
> booker — the system takes it from whoever is signed in. A closure is an announcement, and an
> announcement without a name on it is a rumour."*

**5 — 🔴 LIVE WRITE 10 — press *Add closure*.** Title *"Ashaiman office — electrical rewiring"*,
Type **Station Closure**, a two-day window next month, **Affects the whole company** off, Site
**Ashaiman**, **Staff are paid** on, **Counts as a working day** off, Reason *"Main distribution
board replacement; the office cannot be occupied."*, communication *"Circular to the Ashaiman
staff and a notice at the gate."*

> *"One site, two days, paid, not a working day — with a reason and a record of how it was
> communicated."*

**6 — Now the honest sentence, and it is the one this chapter is for.** Read Rule 5 first.

> *"And here is where I have to be straight with you, because the switch you just watched me set
> says 'affects leave and attendance calculations'. Today it does not. A closure is recorded — with
> its scope, its dates, whether it is paid, whether it counts, and who announced it — and there is
> an endpoint built specifically to ask 'is this date a closure for this site'. What has not been
> wired is the two places that should call it: the leave day-counter and the attendance day-builder.
> Both of them already take the statutory holiday calendar out of their arithmetic, so this is the
> same join, one level up. It is a small change on a record that is already the right shape."*

That is a much better thirty seconds than being asked and improvising.

**7 — 🚫 DO NOT PRESS ⋯ → *Remove closure*.** Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/closures` | `HR.Company.Read` |
| Create | `POST …/closures` | `HR.Company.Write` |
| Update | `PUT …/closures/{id}` | `HR.Company.Write` |
| Delete | `DELETE …/closures/{id}` | **`HR.Company.Admin`** |
| *(the unused one)* | `GET …/closures/is-closure-date?date&locationId&departmentId` | `HR.Company.Read` |

Table: `BusinessClosures`. Create stamps `AnnouncedById` from the token and `AnnouncementDate` to
now. Four more reads exist and none is used: by range, by type, by location, and upcoming — though
the landing page uses upcoming.

**What `is-closure-date` would answer:** any closure whose window covers the date, narrowed to
`AffectsAllStations || LocationId == …` when a location is given and the same for a department.
⚠ Called with **neither**, it answers true for *any* closure, including a single-department one —
so a caller must pass the scope it means (**C-37**).

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-5 · Closures reach nothing.** No leave and no attendance code path reads `BusinessClosures`, and no screen calls `is-closure-date`. The screen's own switch description and the demo runbook's aside both claim otherwise — Rule 5 | |
| **C-37 · `is-closure-date` with no scope over-reports** — it counts a department closure as a company one | |
| **C-38 · A closure has no recurrence.** The year-end stocktake happens every year and has to be entered every year, unlike a public holiday, which has an `IsRecurringAnnually` flag | |
| **C-39 · Nothing prevents overlapping closures**, or a closure overlapping a public holiday | |

---

## 14. `/administration/hr/company-schedule/milestones` — the dates worth marking

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Milestones** · `…/milestones` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"The corporation's own history, on the calendar. Anniversaries, achievements, launches, targets
> met, certifications won. Not operational — but the kind of thing an organisation regrets losing
> track of."*

### 👁 On the page

**Header:** *Company milestones* — *"Anniversaries, achievements and other dates worth marking on
the company calendar."*, back-link.

**+ Add milestone** above a table. Dialog hint: *"A date the organisation wants remembered."*

**Five columns:** **Title** · **Category** *(Company Anniversary · Achievement · Product Launch ·
Target/Goal · Certification)* · **Date** · **Repeats** *(Every year / Once)* · **On calendar**
*(Yes / Hidden)*. Row ⋯: **Edit**, 🚫 **Remove milestone**.

**Dialog:** **Title** *(required, ≤200)* · **Category** *(required)* · **Date** *(required)* ·
**Description** · **Why it matters** · **Related documents** · **Repeats every year** switch ·
**Show on the calendar** switch.

### ▶ Walk it

**1 — Open the screen.** One row: *TDC 74th Anniversary*, Company Anniversary, 18 October, Every
year, Yes.

**2 — ⋯ → *Edit*** and read the description and significance aloud.

> *"'Founded 1952 to develop the Tema township.' Significance: 'Founding of the Corporation.'
> Repeats every year, shows on the calendar. It is a small screen and it is the one a Managing
> Director notices, because it is the only place in an HR system that is about the organisation
> rather than about the staff."*

**3 — Point at *Why it matters* and *Related documents*.**

> *"Two fields that are not decoration. The significance is what somebody writes in the anniversary
> circular ten years from now. The related documents is where the founding instrument lives."*

**4 — 🔴 LIVE WRITE 11 *(optional)* — *Add milestone*.** Title *"ISO 9001:2015 certification"*,
Category **Certification**, a date this year, **Repeats every year** off, **Show on the calendar**
on.

> *"A certification is a milestone that does not repeat — the anniversary does, the award does
> not."*

**5 — Be honest about *Show on the calendar*, briefly:**

> *"And that switch is waiting for a screen that does not exist yet — there is no calendar view in
> this module, everything is a table. Which is the most obvious thing to build next, and it is a
> rendering job over data that is already here."*

**6 — 🚫 DO NOT PRESS ⋯ → *Remove milestone*.** Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/milestones` | `HR.Company.Read` |
| Create / update | `POST` / `PUT …/milestones[/{id}]` | `HR.Company.Write` |
| Delete | `DELETE …/milestones/{id}` | **`HR.Company.Admin`** |

Table: `CompanyMilestones`. Paged, by-category, by-range and upcoming reads exist and are unused
except upcoming, on the landing page.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-9 · `ShowOnCalendar` has no calendar to show on** | |
| **C-40 · `IsRecurringAnnually` generates nothing.** The 74th anniversary does not become the 75th; the date stays 18 October 1952-plus-74 for ever unless somebody edits it. Same shape as the public-holiday flag in attendance | |
| **C-41 · Milestones are not linked to anything.** A long-service award, a certification in the training module and a milestone here are three unconnected records of the same fact | |

---

## 15. `/administration/hr/company-schedule/fiscal-years` — the reporting windows

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Fiscal Years** · `…/fiscal-years` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"The financial years the corporation plans against — and the one that is current, which is what
> the budgeting, appraisal and manpower screens cut their cycles by."*

### 👁 On the page

**Header:** *Fiscal years* — *"The reporting windows the organisation plans against, and the
periods inside them."*, back-link, **+ New fiscal year**.

**Seven columns:** **Year** · **Name** *(with a **Current** badge where applicable)* · **From** ·
**To** · **Periods** *(a count)* · **Status** *(Active · Closed · Archived)* · ⋯.
Sorted by year, **newest first**. **Rows are clickable** into the year.

Row ⋯: **Set as current** *(hidden on the current year)*, 🚫 **Delete**.

**New-year dialog** — *"The year number cannot be changed afterwards — everything else can."*:
**Year** *(required, 2000–2100)* · **Name** *(required, ≤200, pre-filled `FY 2026`)* · **From**
*(pre-filled 1 January)* · **To** *(pre-filled 31 December)* · **Make this the current year**
switch — *"Only one year is current at a time."*

> ⚠ **The year range is `[Range(2000, 2100)]` on the DTO.** Typing 1999 gives a validation error,
> not a bug. This one caught the harness.

Creating a duplicate year is refused: *"Fiscal year 2026 already exists."*

### ▶ Walk it

**1 — Open the screen.** One row: **2026**, *FY 2026*, 1 Jan – 31 Dec, **Current**, Active.

> *"One year. January to December, marked current — and only one can be, which the system
> enforces rather than trusting somebody to untick the old one."*

**2 — Say why an HR module owns a fiscal year**, because it is a fair question:

> *"And this is worth a sentence, because 'why does HR own a financial year' is a reasonable
> thing to ask. It is here because HR cuts its own cycles by it: the appraisal year, the manpower
> budget, the training plan and the leave year all need to know what the corporation's year is. If
> TDC decides Finance should own this and HR should read it, that is a sensible consolidation and
> it is on the list. Today it is HR's own entity, and it is honest to say so."*

**3 — ⋯ → *Set as current* is hidden on this row** — point that out:

> *"And the action is not offered on the year that is already current, rather than being offered
> and doing nothing."*

**4 — 🚫 DO NOT PRESS ⋯ → *Delete*.** Admin — and its dialog warns that the periods go with it.

**5 — Click the row** and continue into chapter 16.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/fiscal-years` | `HR.Company.Read` |
| Create | `POST …/fiscal-years` | `HR.Company.Write` |
| **Set as current** | `POST …/fiscal-years/{id}/set-current` | `HR.Company.Write` |
| Delete | `DELETE …/fiscal-years/{id}` | **`HR.Company.Admin`** |

Tables: `FiscalYears`, `FiscalPeriods`. Also available and unused: paged, by year number, current,
and by status.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-42 · No other module reads the fiscal year.** `fiscal-years/current` exists; the appraisal cycle, the manpower budget and the training plan each carry their own year field. The consolidation the say-line describes is not started | |
| **C-43 · Overlapping years are allowed.** Only the year *number* is unique; two years can cover the same dates | |
| **C-44 · Nothing generates the periods.** Creating FY 2026 does not create its four quarters or twelve months — every one is typed by hand on the next screen | |

---

## 16. `/administration/hr/company-schedule/fiscal-years/[id]` — the year and its periods

### 📍 Where you are

**From:** any row of the fiscal-years register · `…/fiscal-years/[id]` · as **hr.head** ·
**4 minutes**

### 📖 What it is

> *"One reporting window, editable, with the quarters or months inside it — and a one-way door on
> each of them."*

### 👁 On the page

**Header:** the year's name, subtitle `Year 2026 · 4 periods`, back-link, and on the right a
**Current** badge *(when it is)* plus a status badge.

**"The year" card** — an edit form:
- **Name** *(required)* · **Status** *(Active · Closed · Archived)*
- **From** *(required)* · **To** *(required, must be after the start)*
- **This is the current year** switch
- **Save changes**

> ⚠ **The year number is not on the form.** The update DTO has no year field, so it is shown in the
> subtitle and cannot be edited — create-only, as the create dialog warned.

**Periods panel** — **+ Add period** above a table. Dialog hint: *"Quarters, months or halves
inside this year."*

**Seven columns:** **#** *(period number)* · **Period** · **Type** *(Quarter · Month)* · **From** ·
**To** · **Status** *(an **Open** / **Closed** badge)* · **Closed on**.

Row ⋯: **Close period** *(hidden once closed — and it **asks for confirmation**: "There is no
re-open action — closing is final from this screen.")*, **Edit**, 🚫 **Remove period**.

**Dialog:** **Number** *(required, 1–12)* · **Type** *(required)* · **Name** *(required, "e.g. Q1,
January")* · **From** *(required)* · **To** *(required, after the start)*.

Adding a period number that already exists in the year is refused: *"Period number 1 already
exists for this fiscal year."*

### ▶ Walk it

**1 — Open FY 2026.**

**2 — If the year has no periods** *(the demo scenario creates the year but not its periods)*,
say so and build one — that is a better demonstration than a pre-filled list:

> *"The year exists and its periods do not, which is exactly the state a new tenant is in on day
> one. Let me add the first quarter."*

**3 — 🔴 LIVE WRITE 12 — *Add period*.** Number **1**, Type **Quarter**, Name **Q1**,
From 1 January, To 31 March. Add.

> *"Q1. And the number has to be unique inside the year — add a second period one and it is
> refused by name, not by a constraint violation."*

*(Add Q2 as well if you want the close action to be less lonely — same shape.)*

**4 — ⋯ → *Close period* on Q1** and **read the confirmation aloud before pressing**:

> *"'There is no re-open action — closing is final from this screen.' Which is the right way to
> warn somebody: not 'are you sure', but what specifically they will not be able to undo."*

Then confirm. The badge flips to **Closed** and **Closed on** stamps.

*Undo:* chapter 19 — SQL, or leave it; a closed Q1 in September is the honest state.

**5 — Point at the year's *Status* dropdown in the card above.**

> *"And the year itself has three states — Active, Closed, Archived. A period closes when the
> month's numbers are final; the year closes when the audit is done; and it is archived when
> nobody is going to look at it again."*

**6 — 🚫 DO NOT PRESS ⋯ → *Remove period*** or the year's Delete. Both Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/fiscal-years/{id}/details` | `HR.Company.Read` |
| Save the year | `PUT …/fiscal-years/{id}` | `HR.Company.Write` |
| Periods | `GET`/`POST …/fiscal-years/{id}/periods` · `PUT …/periods/{id}` | Read / Write |
| **Close a period** | `POST …/periods/{id}/close` | `HR.Company.Write` |
| Remove a period | `DELETE …/periods/{id}` | **`HR.Company.Admin`** |

Tables: `FiscalYears`, `FiscalPeriods`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-45 · Closing a period locks nothing.** `IsClosed` and `ClosedDate` are set and no module consults them — unlike attendance's monthly-summary lock, which the API itself enforces | |
| **C-46 · There is genuinely no re-open**, as the confirmation says. A period closed by mistake needs SQL | |
| **C-47 · Period numbers are capped at 12** by the form, so a 13-period (four-week) calendar cannot be built here | |
| **C-48 · Periods are not validated against the year.** A period can start before the year does and end after it, and nothing checks that the periods cover the year without gaps or overlaps | |

---

## 17. `/administration/hr/settings/company-profile` — the letterhead and the seal

### 📍 Where you are

**Sidebar:** Administration → HR → **Settings** → **Company Profile** ·
`/administration/hr/settings/company-profile` · as **hr.head**, with **admin** in window B ·
**5 minutes**

### 📖 What it is

> *"A different menu, the same permission — and the reason this module's Admin tier exists. This is
> the corporation's legal identity: the statutory numbers, the registered address, the signatory —
> and the seal that every offer letter and confirmation letter this system generates is stamped
> with."*

> **Why it is in this book.** It is gated on `HR.Company.Read` / `Write` / `Admin`, it is built by
> the same demo scenario, and it is the **only screen in the whole permission family where the
> Admin tier does something other than delete**. If you are demonstrating what an administrator is
> for, this is the screen.

### 👁 On the page

**Header:** *Company Profile* — *"The legal-employer details that appear on offer letters,
confirmation letters and outgoing email."*

**Four form cards, then one action card.**

**Card 1 — Legal identity** — Legal name *(required)* · Trading name · Legal form · Registration
number · Date of incorporation · Country of incorporation.

**Card 2 — Statutory & tax** — Tax identification number (TIN) · VAT number · SSNIT employer
number · Postal code · Other statutory registrations.

**Card 3 — Registered address & contact** — Registered address · City · Region · Country ·
Digital address (GhanaPost GPS) · Primary phone · Website · HR email · General email.

**Card 4 — Documents & signature** — Default signatory name · Default signatory title · Logo URL ·
Offer acceptance instructions · Document footer text. Then **Save changes**.

**Card 5 — Seal & signature** *(the interesting one)*
> *"The images embedded in generated offer and confirmation letters. Replacing one retires the
> image it supersedes rather than overwriting it, so it stays possible to say which letters carry
> which seal."*

Two rows — **Company seal** and **Authorised signature** — each showing:
- a badge: **In force** or **None**
- the current image's file name, *"in force since"* date and who uploaded it; or, when there is
  none, *"No image is in force. Letters render without one, and any legacy image the tenant was
  carrying is used until you upload a replacement."*
- an **Upload** / **Replace** button *(a hidden file input, images only)*
- a **Withdraw** button *(only when one is in force)*
- a **Previously used** list underneath, once anything has been superseded

🚫 **Both Replace and Withdraw are `HR.Company.Admin`.** For `hr.head` they render and 403.

### ▶ Walk it

**1 — Open the page as `hr.head`.** Scroll the four cards without editing.

> *"This is TDC as a legal entity rather than as an employer. CS-1952-000118. The TIN, the VAT
> number, the SSNIT employer number. TDC House, Community 1, Off Hospital Road, Tema — with the
> GhanaPost digital address, because that is how somebody actually finds it."*

**2 — Stop on Card 4 and read the footer text aloud.**

> *"And this is not reference data that sits in a drawer. Every letter this system generates — an
> offer of appointment, a confirmation at the end of probation, a service letter — is rendered from
> this record. The signatory, the acceptance instructions, and that footer line, on every one."*

**3 — Scroll to *Seal & signature* and read the card description aloud.** It is the best sentence
on the screen:

> *"'Replacing one retires the image it supersedes rather than overwriting it, so it stays possible
> to say which letters carry which seal.' That is a versioned corporate seal. Three years from now,
> somebody disputes a letter; you can say which seal was in force on that date and who put it
> there."*

**4 — Point at the *In force since* line and the *Previously used* list.**

> *"In force since, and who uploaded it. And underneath, the ones it replaced."*

**5 — 🚫 As `hr.head`, do not press *Replace*.** Instead, make the permission point — this is the
cleanest example of it in the product:

> *"And here is the one place in this whole area where an administrator is genuinely a different
> person from an HR officer. I can read this, I can change the address and the signatory, I can
> change what the footer says. I cannot change the seal. Replacing the corporation's seal is
> `HR.Company.Admin` — the same tier that changes the policy thresholds and deletes records. Which
> is right: the seal is what makes a letter binding."*

**6 — 🔴 LIVE WRITE 13 *(optional — needs window B as `admin`)*.** Switch to the administrator
window, press **Replace** on *Authorised signature*, pick any small image, and give the reason.
The row restamps and the old image drops into *Previously used*.

> *"Same screen, different person, and now it works. The old image is not gone — it is retired, with
> its dates, so a letter from last month is still explicable."*

*Undo:* nothing to undo — the point of the design is that nothing is overwritten. Leave both images
in place.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The profile | `GET api/hr/company-profile` | `HR.Company.Read` |
| Save changes | `PUT api/hr/company-profile` | `HR.Company.Write` |
| Seal history | `GET api/hr/company-profile/seal-assets` | `HR.Company.Read` |
| **Replace a seal** | `POST api/hr/company-profile/seal-assets/{kind}` *(multipart)* | **`HR.Company.Admin`** |
| **Withdraw a seal** | `POST api/hr/company-profile/seal-assets/{kind}/retire` | **`HR.Company.Admin`** |

Tables: `CompanyProfiles`, `CompanySealAssets`. The seal upload goes through the product's
controlled upload gate — unlike the event attachments in chapter 6, which are references.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-49 · Both seal buttons render for a persona that cannot use them.** Same shape as every delete in this module: gated only on the server | |
| **C-50 · `logoUrl` is a text field, not an upload** — the one image on this screen that is not versioned | |

---

## 18. Where the company schedule shows up outside its own menu

Five places. One is worth a minute of the demo; the rest are for the questions.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Every generated HR letter** | The offer of appointment, the confirmation letter, the service letter — all rendered from the **Company Profile**: the legal name, the registered address, the signatory, the acceptance instructions, the footer line and the **seal**. Chapter 17 is where that record lives | **Yes, by reference** — say it while you are on chapter 17; do not navigate |
| **Recruitment → Interviews** | A panel interview's location can name a room — the demo's shortlist panel is *"in the Boardroom"* | Mention it if recruitment came first |
| **SHE → Emergency plans** | The *Fire drill — Head Office* event in this module **is** the SHE emergency plan's next scheduled drill. Two records, one occasion, and nothing joins them (**C-51**) | Mention it in chapter 4; it is a good illustration of a real integration that is not yet made |
| **Leave and Attendance** | ⚠ **Nothing.** Business closures reach neither, despite both screens' wording and the runbook's aside — Rule 5 and **C-5** | Say it honestly in chapter 13; do not claim the link |
| **Appraisals, manpower budgets, training plans** | Each carries its own year rather than reading `fiscal-years/current` (**C-42**) | Only if asked |

---

## 19. Reset — putting the database back

Do this after the room empties. **Almost nothing here needs SQL**, because the module's own
`Cancel` actions are the reversible half of every destructive pair — which is the design point you
spent the demo making.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Event created** *(ch. 5, LW 1)* | `hr.head` cannot delete it. Open it → **Cancel**, reason `Demonstration`. A cancelled event keeps its participants, its tasks and its reason, which is the correct end state anyway |
| 2 | **Participant added** *(ch. 6, LW 2)* | ⋯ → **Remove participant** is Admin. Either leave it — an extra attendee on a board meeting is harmless — or remove it from the `admin` window |
| 3 | **RSVP recorded** *(ch. 6, LW 3)* | Nothing to undo; it is a response on a row you added. If you removed the row, it went with it |
| 4 | **Task added and completed** *(ch. 6, LW 4–5)* | ⋯ → **Edit** the task back to *Not Started* if you want it pristine; removing it is Admin |
| 5 | **Event approved** *(ch. 6, LW 6)* | No un-approve. SQL: `UPDATE CompanyEvents SET ApprovedById = NULL, ApprovalDate = NULL, Status = 1 WHERE EventNumber = '<no>'`. Or leave it — an approved board meeting is the honest state |
| 6 | **Room booked** *(ch. 9, LW 7)* | Open it → **Cancel**, reason `Demonstration`. **The slot is released**, which is what matters for the next rehearsal |
| 7 | **Booking approved / extended** *(ch. 10, LW 8)* | Covered by row 6 — cancel the booking and both go with it |
| 8 | **"Bookings need approval" turned on** *(ch. 12, LW 9)* | ⋯ → **Edit** the Boardroom → turn it **off** → **Save changes**. ⚠ **Do this**, or the next rehearsal's § 2.2 count will not be zero and chapter 8's say-line stops being true |
| 9 | **Closure added** *(ch. 13, LW 10)* | ⋯ → **Remove closure** is Admin. From the `admin` window, or SQL: `UPDATE BusinessClosures SET IsDeleted = 1 WHERE Title = 'Ashaiman office — electrical rewiring'` |
| 10 | **Milestone added** *(ch. 14, LW 11)* | Same shape as row 9, on `CompanyMilestones` |
| 11 | **Fiscal period added and closed** *(ch. 16, LW 12)* | No re-open. SQL: `UPDATE FiscalPeriods SET IsClosed = 0, ClosedDate = NULL WHERE Id = '<id>'`, or `IsDeleted = 1` to drop the period entirely. **Or leave it** — a closed Q1 in September is correct |
| 12 | **Seal replaced** *(ch. 17, LW 13)* | **Nothing.** The whole point is that nothing was overwritten; the previous image is retired, not lost. Leave it |

**The one thing that cannot be put back** is the **numbers** — `EVT-…` and `BK-…`. And here they
are worse than elsewhere: because both generators **count live rows** (**C-6**), soft-deleting your
demo event makes the *next* event reuse its number, and the index does not stop it. **Prefer
cancelling to deleting for that reason too**, not only the audit one.

**The clean option.** The whole database rebuilds in 45–60 minutes:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='ErpSystem.Api'" |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild renumbers every event and booking in this book, and costs you chapter 2's prep as well.
For this module it is almost never worth it — twelve of the thirteen live writes undo in the UI.

---

## 20. The short path — 20 minutes

When the slot shrinks. Six screens, and the story still lands.

| # | Screen | Min | The one thing |
|---|---|---|---|
| 1 | `/hr/company-schedule` | 2 | read the four cards; **"not a diary — the corporation's own calendar"** |
| 2 | `/hr/company-schedule/events/[id]` — **the Management retreat** | 5 | the Overview, then **expected 18 vs attended 4**, then read the **outcome summary** aloud. Then the Attendance tab |
| 3 | The same event's **Tasks** tab, or the board meeting's | 2 | before / during / after, with owners and due dates. **"An event is a small project"** |
| 4 | `/hr/company-schedule/bookings/new` | 5 | **set a window that is already taken and press *Find free rooms*** — the boardroom is not offered. *"A room in this system cannot be double-booked."* Then book a free one |
| 5 | `/administration/hr/company-schedule/rooms` — edit the Boardroom | 3 | **Site vs Where in the site**; then *Bookings need approval*, and what it changes |
| 6 | `/administration/hr/settings/company-profile` — the **Seal** card | 3 | *"replacing one retires the image it supersedes"*; then **"I cannot change the seal, and that is the point"** |

Cut, in this order if you must: the company profile *(say the sentence about letters instead)*,
then the rooms screen, then the tasks tab.

**Do not put in the short path:** closures *(the honest caveat needs more time than the screen
does)*, milestones, or fiscal years.

---

## 21. What this walk found

**Fifty-one findings.** This module is in better shape than its history suggests — it was dormant
for a year and was built, wired and harnessed in one pass, and it shows. Six behaviours will catch
you out in a demonstration; **five** are the ones to fix before it is called finished.

### The six that affect a demonstration

| # | Where | Finding |
|---|---|---|
| **C-1** | every screen | **`hr.head` cannot delete anything** — every delete is `HR.Company.Admin`, and the **event page carries a red Delete button in its header** |
| **C-2** | event → Reschedule | **The dialog says the original dates are kept. They are overwritten** and nothing stores them |
| **C-3** | events, bookings | **Approve is a flag, not the workflow engine** — no instance, no inbox entry, no assignee check, self-approval always allowed |
| **C-4** | rooms, bookings | **Max booking duration and advance-booking days are recorded and never enforced.** Only *bookable* and *the time clash* are |
| **C-5** | closures | **Business closures reach nothing** — no leave and no attendance code path reads them, despite the screen's own wording |
| **C-6** | events, bookings | **Numbers can repeat.** Both generators count live rows and neither index is unique, so a delete makes the next record reuse the number |

### The five worth fixing first

| # | Where | Finding | Why it is first |
|---|---|---|---|
| **C-5** | closures | **Wire closures into the leave day-counter and the attendance day-builder.** Both already exclude statutory holidays; this is the same join, and `is-closure-date` is already built for it | The feature is complete except for the two callers |
| **C-19** | events | **No lifecycle guard on the server.** A completed event can be cancelled, a cancelled one completed, and either rescheduled. Every other HR module refuses these | It is the class of bug that only appears once somebody uses the API |
| **C-2** | events | **Keep the original dates when rescheduling** — or stop the dialog claiming it does | The screen currently states something false |
| **C-6** | events, bookings | **Move both number generators to the day's maximum including soft-deleted rows**, the way every other HR generator was fixed | A duplicated number is silent and permanent |
| **C-26 / C-35** | bookings, rooms | **Build the room view** — `bookings/room/{roomId}` already exists and there is nowhere in the product to see one room's week | The most-requested view of a booking register, one screen away |

### Everything else, by area

| # | Area | Finding |
|---|---|---|
| C-7 | events | *In progress*, *Postponed* and *Rescheduled* are reachable only from the edit form's dropdown — **rescheduling does not set *Rescheduled*** |
| C-8 | bookings | *Completed* and *No show* are set by nothing, so the facilities question the enum exists for cannot be asked |
| C-9 | events, milestones | **There is no calendar view anywhere in the module** — every screen is a table, and `ShowOnCalendar` has nothing to show on |
| C-10 | landing | The only aggregate is four separate requests, sliced to six rows each in the browser |
| C-11 | events | The register is **unpaged and filtered client-side**, with six unused server reads |
| C-12 | events | No date filter on the register, though `events/range` exists |
| C-13 | events | No export |
| C-14 | events | **Recurrence generates nothing** — pattern, count and end date are stored; no occurrence is created |
| C-15 | events | **No conflict check of any kind on an event** — two all-staff events on the same morning are accepted |
| C-16 | events, rooms, closures | The Site dropdown lists the whole location tree, mitigated by suffixing each option with its level |
| C-17 | participants | **No invitation is ever sent** — `InvitationStatus = Sent` is a field, not an email |
| C-18 | attachments | **References, not files** — no upload, no download, no link to the document register |
| C-20 | events | Approve ignores `RequiresApproval` and confirms any open event |
| C-21 | attendance | An attendance record **cannot be removed** — no delete endpoint; a wrong row is corrected by marking again, leaving both |
| C-22 | participants | A participant **cannot be edited** — and removal is Admin, so `hr.head` cannot fix a typo in an external guest's email at all |
| C-23 | tasks | `EventTaskStatus.Overdue` is never set; a task past its due date stays *Not Started* |
| C-24 | events | The edit form's status dropdown accepts any transition, and setting *Cancelled* there does **not** set `IsCancelled`, the date or a reason |
| C-25 | bookings | Unpaged and filtered client-side, with six unused server reads |
| C-27 | bookings | **Capacity is a filter, not a rule** — the availability search respects it; create does not |
| C-28 | bookings | The availability repository query is **not tenant-filtered** before the service narrows the rooms. Harmless, and the only unscoped read in the module |
| C-29 | bookings | A linked event is a label — cancelling either does not touch the other |
| C-30 | bookings | Approve has no status guard on the server |
| C-31 | bookings | Cancel has none either; a cancelled booking can be cancelled again, overwriting the first reason |
| C-32 | bookings | Delete is on the register's row menu and not on the detail page — inconsistent, if convenient |
| C-33 | rooms | The delete failure toast blames bookings when the real answer is a 403 |
| C-34 | rooms | A **generated** room code skips the duplicate check a typed one gets, and is count-based |
| C-36 | rooms | Deactivating a room does not touch its future bookings |
| C-37 | closures | `is-closure-date` called with no scope counts a department closure as a company one |
| C-38 | closures | A closure has **no recurrence**, unlike a public holiday — the year-end stocktake is retyped every year |
| C-39 | closures | Nothing prevents overlapping closures, or a closure over a public holiday |
| C-40 | milestones | `IsRecurringAnnually` generates nothing — the 74th anniversary never becomes the 75th |
| C-41 | milestones | Milestones link to nothing — a long-service award, a training certification and a milestone are three unconnected records of one fact |
| C-42 | fiscal years | **No other module reads the fiscal year.** `fiscal-years/current` exists; appraisals, manpower budgets and training plans each carry their own year |
| C-43 | fiscal years | Overlapping years are allowed — only the year *number* is unique |
| C-44 | fiscal years | Creating a year **generates no periods** — all four quarters are typed by hand |
| C-45 | fiscal periods | **Closing a period locks nothing.** No module consults `IsClosed` — contrast attendance's summary lock, which the API enforces |
| C-46 | fiscal periods | There is genuinely no re-open, as the confirmation honestly says |
| C-47 | fiscal periods | Period numbers are capped at 12, so a 13-period calendar cannot be built |
| C-48 | fiscal periods | Periods are not validated against their year — they may start before it, end after it, overlap, or leave gaps |
| C-49 | company profile | Both seal buttons render for a persona that cannot use them |
| C-50 | company profile | `logoUrl` is a text field, not an upload — the one image here that is not versioned |
| C-51 | cross-module | The fire drill exists twice — as an event here and as the SHE emergency plan's next drill — and nothing joins them |

### What is genuinely strong here

Worth being as precise about as the gaps, because for a module that was dormant a year ago this
list is long:

- **The double-booking rule is real and it is enforced in the right place.** The clash test runs on
  create **and** on edit, with the booking's own id excluded on edit, using a correct half-open
  interval. The booking form asks the server what is free rather than offering everything and
  failing at the end. This is the single best-built thing in the module.
- **Every actor comes from the token, and none of them is on a form.** All five used to be query
  parameters. *"You are recorded as the organiser"* is the whole design, said in five words on the
  screen it applies to.
- **The Admin tier means something.** Not "senior Write" — three specific things: the knobs that
  move a trust boundary, the corporation's seal, and destruction. The seal in particular is
  **versioned rather than overwritten**, so which letters carry which seal stays answerable.
- **An event is modelled as a project**, not a diary entry: budget with a code, resources, catering,
  a three-stage task list with owners and due dates, an invitation list and a separate attendance
  register.
- **Create responses are re-read before mapping**, so a screen rendering the create result shows
  real names rather than blanks — a defect the harness found in four of five create paths and all
  five now avoid.
- **A bad site id is refused with a sentence**, not a 500 reading *"Something went wrong"*.
- **The confirmation dialogs say what you cannot undo**, specifically: *"There is no re-open
  action"*, *"Cancelling keeps the record and the reason"*, *"Deactivate it instead if it has
  booking history worth keeping"*. That is rarer than it should be.
- **Create-only and update-only fields are rendered conditionally rather than rendered and
  ignored** — recurrence, requires-approval, status, actual cost, the room on a booking, the year
  on a fiscal year. Every one of those is a field that would have silently discarded input.

---

## Appendix A — every route, in demo order

| # | Route | Chapter | Persona |
|---|---|---|---|
| 1 | `/hr/company-schedule` | 3 | hr.head |
| 2 | `/hr/company-schedule/events` | 4 | hr.head |
| 3 | `/hr/company-schedule/events/new` | 5 | hr.head |
| 4 | `/hr/company-schedule/events/[id]` | 6 | hr.head |
| 5 | `/hr/company-schedule/events/[id]/edit` | 7 | hr.head |
| 6 | `/hr/company-schedule/bookings` | 8 | hr.head |
| 7 | `/hr/company-schedule/bookings/new` | 9 | hr.head |
| 8 | `/hr/company-schedule/bookings/[id]` | 10 | hr.head |
| 9 | `/administration/hr/company-schedule` | 11 | hr.head |
| 10 | `/administration/hr/company-schedule/rooms` | 12 | hr.head |
| 11 | `/administration/hr/company-schedule/rooms/new` | 12 | hr.head |
| 12 | `/administration/hr/company-schedule/rooms/[id]/edit` | 12 | hr.head |
| 13 | `/administration/hr/company-schedule/closures` | 13 | hr.head |
| 14 | `/administration/hr/company-schedule/milestones` | 14 | hr.head |
| 15 | `/administration/hr/company-schedule/fiscal-years` | 15 | hr.head |
| 16 | `/administration/hr/company-schedule/fiscal-years/[id]` | 16 | hr.head |
| 17 | `/administration/hr/settings/company-profile` | 17 | hr.head **+ admin** |

---

## Appendix B — the permission map, in one table

`hr.head` holds **Read**, **Write** and **Approve**, and **not Admin**.

| Tier | Actions |
|---|---|
| **`HR.Company.Read`** | every read in the module — the landing's four summaries; events (list, paged, detail, details-with-collections, by range / organiser / department / status / category, upcoming); participants; attendance; tasks; attachments; rooms (list, paged, detail, by location, **available**, active); bookings (list, paged, detail, by room, by booker, by range, by status, pending approvals); milestones; closures **and `is-closure-date`**; fiscal years and their periods; the company profile and its seal history |
| **`HR.Company.Write`** | create and update an **event** · **approve** it · **cancel** it · **reschedule** it · **complete** it · add a **participant** · record an invitation **response** · mark **attendance** · **check out** · add / update / **complete** a **task** · add an **attachment** · create and update a **room** · create and update a **booking** · **approve** a booking · **cancel** a booking · create and update a **milestone**, a **closure**, a **fiscal year** and a **period** · **set the current fiscal year** · **close a period** · update the **company profile** |
| **`HR.Company.Admin`** — ***`hr.head` is refused*** | **every delete in the module**: events, participants, attachments, tasks, rooms, bookings, milestones, closures, fiscal years, fiscal periods. **Plus** — and this is the part that is not about destruction — **replacing or withdrawing the company seal**, and **changing the HR policy settings** (the procedural-absence threshold, FR-HR-092; the budget and establishment enforcement modes, FR-HR-136) |
| **`HR.Company.Approve`** | **nothing in this module.** Despite the name it is the interim authority over **team objectives and terms of reference** where no workflow definition is published. Company-schedule approvals are plain `Write` |

> **Two absences worth naming.** There is **no self-service tier** — an invitee cannot answer their
> own invitation, because `participants/respond` takes a participant id and sits on the desk's Write
> policy, so it means *"HR records the response"*. A genuine reply screen needs a self-or-permission
> check against the participant's own employee id first; that is the module's one recorded open
> decision (D-02). And there is **no approver tier** — see Rule 3.

---

## Appendix C — related documents

| Document | What it adds |
|---|---|
| `docs/HR/areas/attendance/HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md` | The neighbouring module, and the other half of "is this a working day?" Its finding **A-92** is this guide's **C-5** from the other side |
| `docs/HR/areas/leave/HR-LEAVE-SYSTEM-GUIDE.md` | The holiday calendars a closure sits *over and above* |
| `docs/HR/areas/employees/HR-EMPLOYEES-SYSTEM-GUIDE.md` | The employee picker every participant, task owner and attendance row uses |
| `docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md` | The first guide in this series; the format's origin. Its offer letters are rendered from chapter 17's profile and seal |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | The engine this module deliberately does not use — the four-step recipe Rule 3's say-line refers to |
| `docs/HR/integration/HR-MODULE-INTEGRATION-MAP.md` | Where the company schedule sits against leave, attendance, SHE and recruitment |
| `dev-harness/hr-company-schedule/README.md` | The 162-assertion harness, and the four defects it found in code no screen had executed |
| `dev-harness/hr-demo-smoke/scenarios/110-company-schedule.mjs` | Exactly what the demo database holds, and how to rebuild it |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 7 is the three-step version of this guide, for the standard demo pack |

---

*End of the HR Company Schedule System Guide.*
