# HR Company Schedule — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source; updated 2026-09-24 for round 4 of the demo feedback;
**rewritten whole on 2026-10-06, in the company-schedule final closure's lane 6** (slices 6b–6e-2), against what
lanes 0–7 built. Every chapter is re-read in the code and against UAT's own rows, and the walks fork where UAT differs
from a database the demo pack builds today (§ 2.1). Where this guide and `HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md`
disagree, **the plan is right**: its § 5 gives every finding's owner, and each lane's *State* says what was built and
how it was proved. The guide describes what the code does; where a screen offers something the server refuses, the
step says so.

> ⚠ **Not yet walked in a browser:** the screens lanes 1–7 changed. § 21 lists the twelve walks (the plan's lane 5
> items 1–8 and lane 7 items 9–12); every server rule behind them is proved by the review suite on UAT (1,152
> assertions, two clean passes).

**Scope:** the whole **Company Schedule** group of the HR sidebar (*Human Resources → Company Schedule*) —
**five menu items** — plus the screens that hang off them without a menu entry; the **four setup areas** under
Administration → HR → Company Schedule; the **Company Profile** screen, which is another menu but the same
permission family and the one place the Admin tier genuinely earns its keep; and the **three self-service
screens** in the employee portal (*Company → Calendar* and *Company → Room Bookings*).

| # | Menu item | Route | Chapter |
|---|---|---|---|
| — | *(group landing)* Company Schedule | `/hr/company-schedule` | 3 |
| 1 | Company Calendar — every employee; the server decides what each one sees | `/hr/company-schedule/calendar`, and in the portal `/me/calendar` | 3a |
| 2 | Events | `/hr/company-schedule/events` | 4 |
| — | *(no menu entry)* New event | `/hr/company-schedule/events/new` | 5 |
| — | *(no menu entry)* The event | `/hr/company-schedule/events/[id]` | 6 |
| — | *(portal, no menu entry)* The event as staff see it | `/me/calendar/events/[id]` | 6a |
| — | *(no menu entry)* Edit an event | `/hr/company-schedule/events/[id]/edit` | 7 |
| 3 | Room Bookings | `/hr/company-schedule/bookings` | 8 |
| — | *(no menu entry)* Book a room | `/hr/company-schedule/bookings/new` | 9 |
| — | *(no menu entry)* The booking | `/hr/company-schedule/bookings/[id]` | 10 |
| 4 | My Schedule — every employee | `/hr/company-schedule/my-schedule` | 10A |
| 5 | Team Schedule — the HR desk and unit heads | `/hr/company-schedule/team` | 10B |
| — | *(portal)* Room Bookings — staff book for themselves | `/me/room-bookings` (+ `new`, `[id]`) | 10C |
| — | *(Administration)* setup hub | `/administration/hr/company-schedule` | 11 |
| — | *(Administration)* Meeting Rooms | `…/company-schedule/rooms` (+ `new`, `[id]/edit`) | 12 |
| — | *(Administration)* Business Closures | `…/company-schedule/closures` | 13 |
| — | *(Administration)* Milestones | `…/company-schedule/milestones` | 14 |
| — | *(Administration)* Fiscal Calendar — Finance's, read-only | `…/company-schedule/fiscal-calendar` | 15 |
| — | *(retired)* HR's own fiscal years | — the screens were removed in lane 4b | 16 |
| — | *(Administration → Settings)* Company Profile | `/administration/hr/settings/company-profile` | 17 |

**Twenty-four pages** over **twelve tables of the module's own**: four collections nested inside a single event,
and a file store for milestones. Two retired fiscal tables stay in the database, read by nothing, and Finance's
fiscal calendar is read and never written. The diaries and the calendar have no table of their own. They read
each person's commitments from seven sources, four of them in other modules: interviews, leave, travel and
training. New chapters are lettered (3a, 6a, 10C) so that no chapter number used elsewhere moved.

> **A word about this module's history, because it explains its shape.** Until 2026-08-28 the
> backend carried **ninety endpoints and no screen had ever called one of them** — the whole
> surface was dormant from the original port. Before a single form could exist, five actor ids
> arrived as **query parameters** (act-as-anyone the moment a screen existed) and moved to the token, and the
> room's site was a **required foreign key to an empty table** and was repointed to the live `Location` tree.
> Round 4 (2026-09-24) made it send email and gave it two diaries. **The final closure (2026-10-01 to 10-06)**
> then re-read every file, added 58 findings (F-1…F-58) to the 77 that the guide's first two editions listed, and
> built all of them in seven lanes, deferring nothing (decision D-9): closures that move leave, events on the
> workflow engine, recurring series, real files, staff booking, a company calendar, Finance's fiscal year. What
> is still open is other modules' work and the browser walks — § 21.

---

## This document is two things at once

Like the recruitment, employees, leave, attendance and travel guides, this is a **reference** and a
**script you can perform**. Every chapter has the same five parts, and you can read only the ones
you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Four more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered, and chapter 19 tells you how
  to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.
- **🚫 DO NOT PRESS** — a control that writes something you cannot undo inside a demo, or that reaches
  people who are not in the room (Rule 1). Since lane 5a every remove and delete is shown only to the tier its
  route needs, so the old "renders and answers 403" kind is gone; a refusal that remains — an organiser
  approving their own event, say — is a sentence saying why, and safe to press.
- **UAT / Rebuilt** — where a step depends on the database, it says what each one shows (§ 2.1).

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the seven rules that decide whether this demo works

This is still the friendliest module in HR to demonstrate — everybody understands a calendar and a meeting
room — and since the final closure it is also one of the most connected. A closure moves leave, an event goes
through the workflow engine, and the calendar decides what each person may see. Seven behaviours will catch you
out live. Read them twice.

They replace round 4's eight. That edition's Rules 1–6 are fixed: every remove is shown only to whoever may use it,
a moved event shows where it was, approval is a real chain, the availability search applies every room rule,
closures reach leave, and numbers cannot repeat. Its Rule 7 (no mail server) is Rule 3 below, and its Rule 8 (rebuild
first) is now § 2.1, because the module's own suites tidy up after themselves.

| Rule | In one line |
|---|---|
| **1** | A closure is a day off now: saving one re-counts people's leave, and on UAT those are TDC's own staff |
| **2** | Approval runs on the workflow engine, and nobody approves their own: you need two HR personas |
| **3** | Nothing is emailed on either demo database: show the bell, and read "reached" |
| **4** | A recurring event is every one of its dates at once |
| **5** | Everybody sees their own calendar, so show it in two windows |
| **6** | The hourly sweep sends reminders and settles bookings by itself |
| **7** | The fiscal year is Finance's |

### Rule 1 — A closure is a day off now, and saving one re-counts people's leave — on UAT, real people's

Since lane 1 a business closure is no longer a note on a calendar. What it does depends on its kind (chapter 13):

| Kind on the form | Who is off | What it changes |
|---|---|---|
| **Whole company** | everybody | a day off for **leave** (not charged), for the **discipline** deadlines (a five-working-day appeal window steps over it), and for **travel** (a trip day on it is not posted to attendance as on duty) — exactly as a public holiday |
| **One site** | the staff placed at that site | a day off in **their** leave |
| **One organisation unit** | the unit and every unit beneath it, wherever its staff sit | a day off in **their** leave |
| **Reduced operations** | nobody | nothing — it is still a working day |

**Saving one re-counts leave already granted** (decision D-15a). Every approved, under-way or finished leave request
over its days, for the people it covers, is counted again; the balance and the attendance days are re-posted; and
each person whose count changes is told, in the app and by email. The screen lists them: *"Leave recounted (N) —
balances and attendance updated, and each employee told"*. Moving or deleting the closure does the same in reverse,
and so does a public holiday added to the holiday calendar (D-15b).

**On UAT that is TDC's staff list:** 4,949 people numbered `TDC/…` beside the demo personas. A whole-company closure
saved live over a day when anybody has leave changes those people's balances and sends each of them a notice. So:

- **UAT:** make the live closure a **Reduced operations** one. It is still a working day, so it re-counts nothing
  and tells nobody, and the form, the scope and the register are all there to show. Explain the day-off half from
  the seeded *Year-end stocktake*.
- **Rebuilt:** every employee belongs to the demo pack, so a whole-company closure over somebody's leave is safe,
  and it is the strongest beat in chapter 13.
- 🚫 **Two buttons publish to everybody they cover, and their notices cannot be unsent:** *Announce to the N staff
  it covers* on a closure, and *Announce on the intranet* on an event. The announcement itself can be archived
  afterwards under HR → Announcements; the notices it sent stay sent. On UAT a whole-company announcement reaches
  about five thousand people — the screens count 5,001 active staff. Press them only for a unit or a scope you know,
  or on a rebuilt database.

Two things still do not read closures, and both are someone's open item: **attendance**, which has no working-day
builder at all (attendance guide A-92, HR's own), and **payroll**, which can read the days and their *Staff are
paid* flag through HR but does not yet act on an unpaid one (the payroll hand-off, § 3 item 4).

### Rule 2 — Approval runs on the workflow engine, and nobody approves their own

Since lanes 2b and 3b-1 (decision D-10), an event marked **Requires approval**, and a booking of a room marked
**Bookings need approval**, go through the corporation's workflow engine like every other approval in HR.

- **It waits.** The event stays *Scheduled*, the booking *Tentative*, each marked as awaiting approval. The event's
  invitations are held (*Not sent*) and go out the moment it is approved; its reminders and its chase wait too;
  and the company calendar shows it to nobody but the HR desk and its organiser until then.
- **The engine asks the HR desk.** Both databases carry the two definitions, *Company Event Approval* and *Room
  Booking Approval*, each addressed to the HR role, which on both is **`hr.head` and `hr.officer`**. Each is asked
  in the inbox and in the app, with a link to the record's page.
- **Nobody approves their own.** The event's organiser, the booking's booker and whoever created it are refused,
  with the reason. So the beat takes **two personas**: `hr.head` creates the event, `hr.officer` approves it.
- **Decide on the record's own page** — *Approve* and *Reject* on the event, *Approve* and *Not approve* on the
  booking. The generic inbox (`/workflow/inbox`) lists the request and opens the page, but a decision taken there
  does not reach the record; that is true of every HR approval (cross-module #15).
- **Reject cancels.** A rejected event is cancelled with its rooms; a booking not approved is cancelled. Both
  tell the organiser or the booker.
- **A move asks again.** An approved event that is rescheduled, or an approved booking whose time changes on a room
  that needs approval, goes back to the approver.
- **A series is approved once.** The first date carries the request, and deciding it decides every date still
  waiting (Rule 4).
- **Nothing is approved by default.** With no definition published, the record still waits, and anyone holding
  `HR.Company.Approve` decides. Neither demo database is in that state.

### Rule 3 — Nothing is emailed on either demo database: show the bell, and read "reached"

**No mail server is set up on UAT or on a rebuilt database** (`EmailSettings` is empty). The module's seventeen
emails — invitations, reminders, chases, moves, cancellations, a task given or overdue, a booking approved,
cancelled or marked a no-show, and more (§ 1.6) — are each attempted and go nowhere. Even where a mail server is set
up, an email sent with nobody signed in finds none until Platform fixes **cross-module #40**. That covers
everything the hourly sweep sends.

**But the app tells people.** Since lane 2e-1 every company-schedule notice also goes **in the app** — the bell —
to everybody it is for who has a login. That is your demonstration: a second window, signed in as the person told.

**And the screens count truthfully** (lane 2e-2). Every send reports how many people it was **for** and how many it
**reached** — by an email a mail server accepted, or by the bell. On a demo database an internal guest with a login
is reached through the bell, and an outside guest is never reached. An invitation is marked *Sent*, and a reminder
or a chase stamped, only once it has reached somebody. One that reached nobody stays due, for the button and for the
next sweep.

> *"Everybody invited is told — in the app straight away, and by email wherever a mail server is set up. This
> database has none, so here is what they see."* Then open the bell in the second window, or the wording under
> **Letter & Email Templates** (§ 18).

### Rule 4 — A recurring event is every one of its dates at once

Since lane 2f (decisions D-2 and D-12), saving an event that repeats creates **every occurrence** at once, up to
fifty-two, ending on a date or after a count. Each is a full event with its own number (`EVT-…`), guest list,
answers, attendance register, tasks and papers, and reads *"Occurrence 3 of 10"*. A weekly meeting for a year is
fifty-two rows in the register the moment you press Save.

- **Every series action asks how far:** *this date only*, *this and following dates* or *every date in the series* —
  for adding or removing a guest, editing, rescheduling, cancelling and booking a room. It never changes a date that
  has passed or is completed.
- **A date on a public holiday or a whole-company closure** is made anyway, and flagged on its page and in the
  series list.
- **Keep a live series short** (three dates), and clean it up with **Cancel → every date in the series**. Cancel is
  on the HR desk's tier; delete is an administrator's.

### Rule 5 — Everybody sees their own calendar, so show it in two windows

The **Company Calendar** (lane 7, decision D-7) is one page in two places: the HR menu, and *Company → Calendar*
in the portal. The server decides what each person sees.

| Who | Sees |
|---|---|
| **The HR desk** (`HR.Company.Read`) | every live event — a private one too, and one awaiting approval, marked so — every closure, and every booking in full |
| **Anybody else** | the events they organise, are invited to (once the invitation has gone) or are the **audience** of — an event marked *Show on company calendar*, read through its scope and visibility, never a private or confidential one, never one awaiting approval; the closures that cover them; their own bookings, and anybody else's only as *"booked"* |
| **Everybody** | the milestones marked *Show on calendar*, the public holidays, and their own leave, travel, interview panels and training |

So the same week looks different to `hr.head` and to `staff`. That is the point of the feature, and two windows make
it in ten seconds. A member of staff who opens an event lands on the staff event page (chapter 6a): no budget, no
other guest's answer, and the meeting password only for its guests and organiser. Their own invitation is answered
from it.

### Rule 6 — The hourly sweep sends reminders and settles bookings by itself

Twenty-three minutes after the API starts, and every hour after that, one pass:

- sends each event's **reminder**, *Days before* its start, once;
- **chases unanswered invitations** once, two days before the reply-by date (the policy's setting, chapter 18);
- **chases an overdue task** once, to whoever it is assigned to;
- cancels a booking **still Tentative when its start comes** — *"Not approved before it started."* — and tells its
  booker;
- marks a **confirmed booking Completed** once its end has passed, silently.

So a database days after its build has moved on its own: the seeded booking is *Completed*, and the seeded events'
reminders have gone (§ 2.2). A booking left awaiting approval past its start vanishes from the queue by itself — the
right answer, but say so before somebody asks where it went. The event page's **Send reminder now** and **Chase
unanswered now** do the same for one event, and count as the sweep's send.

### Rule 7 — The fiscal year is Finance's

Nothing in this module creates, closes or edits a fiscal year any more (lane 4b, decision D-6). *Administration → HR
→ Company Schedule → Fiscal Calendar* shows **Finance's** years and periods read-only, with each accounting book's
year-end close, and *Set up and closed in Finance* opens Finance's own screen. On UAT that is FY2025 (closed) and
FY2026 (open); a date after FY2026 is labelled by continuing Finance's sequence — FY2027 from 1 January 2027, the
year Finance must open next. The HR policy setting *Fiscal year starts* is read-only while Finance has a year.

> *"HR does not keep its own year. It reads the one Finance keeps, so a requisition's budget year and the accounts'
> year cannot disagree."*

---

## Conventions

**Routes.** `/hr/company-schedule/events/[id]` is the file
`frontend/src/app/hr/company-schedule/events/[id]/page.tsx`, and a portal route such as `/me/calendar` is under
`frontend/src/app/me/`. A segment in square brackets is a parameter. Every server route in this guide is under
`api/CompanySchedule` unless it says otherwise; the staff doors are under `api/CompanySchedule/me`.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in this module. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — `CompanyEvent` lives in
`CompanyEvents`, `RoomBooking` in `RoomBookings`.

> ⚠ **HR's two fiscal tables are retired, and their names are still a trap.** HR's `FiscalYear` and `FiscalPeriod`
> are configured with **no** `DbSet<>`, so each table is named after its class, singular: **`FiscalYear`** and
> **`FiscalPeriod`**. Since lane 4b nothing reads or writes them; they stay in the model until a later migration
> drops them, and UAT's one row (*FY 2026*) is left there, unused. The plural **`FiscalYears`** and
> **`FiscalPeriods`** are **Finance's**, and they are now the calendar this module reads (Rule 7, chapter 15).

**Columns every table here carries.** Every entity in this guide derives from `TenantEntity`:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it explicitly |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

**The permission ladder.** Four permissions gate this module, and five of its pages need none:

| Permission | Grants | Held by the `HR` role? |
|---|---|---|
| `HR.Company.Read` | every read of the desk — the landing's summary, both registers and their CSV exports, every event, room, booking, milestone and closure with their collections and files, the whole company calendar, Finance's fiscal calendar (read for HR in the server), payroll's read of closure days; also the company profile with its statutory numbers, the HR policy settings and the external-associate register | **yes** |
| `HR.Company.Write` | run the company schedule — create, edit, reschedule, cancel and complete events; guests, answers at the desk, the register, files and tasks, and **removing a guest, a register row or a file**; reminders and chases now; announcing an event or a closure; rooms, bookings (cancel, mark a no-show), a booking for every date of a series; milestones and their files; closures; any unit's team schedule; the company profile. **Changing the HR policy settings is deliberately NOT this permission** | **yes** |
| `HR.Company.Admin` | **change the HR policy settings** — the procedural-absence threshold (FR-HR-092) and the budget/establishment enforcement modes (FR-HR-136); **replace or retire the logo, the seal and the signature**; **delete** an event, a task, a booking, a room (only one with no booking history), a closure or a milestone; and run the one-time leave re-count for closures that existed before lane 1 | **no** |
| `HR.Company.Approve` | decide an event or a booking that needs approval **where no workflow definition is published** — the fallback tier (Rule 2); and the same for team objectives and terms of reference. Its description still names events only, not bookings | **yes** |
| *(none — any internal login)* | the **Company Calendar**, **My Schedule**, **Team Schedule** (the server allows the HR desk any unit and a unit's head their own), the **staff event page** and the invitee's own answer, and **staff room booking** in the portal. The server decides, per person, what each one may see or do | — |

> The Admin tier's shape is worth reading twice, because it is the clearest statement of intent in
> the HR permission map. It is not "the senior version of Write". It is three specific things: the
> knobs that move a trust boundary, the corporation's seal, and destruction. An HR officer runs the
> calendar; an administrator decides what the calendar is allowed to mean.

**The actor is the token, and the organiser is chosen.** The booker, the person who marked attendance, the
uploader, the announcer, the creator of an event and the approver are all taken from the signed-in person's
employee record. None of those is on a form — they used to arrive as query parameters. Since lane 2a (decision
D-11) **the organiser** is the one exception: the event form has an *Organiser* picker, defaulting to you, so an HR
officer can file the board meeting under the Managing Director. The creator is stamped beside them. One
consequence stands:

> **A signed-in user whose account is not linked to an employee record cannot create an event, book a room, mark
> attendance or announce a closure.** They get a clean refusal that writes nothing. The `admin` account is unlinked
> on both demo databases, so this is load-bearing rather than theoretical: chapter 17 is the only walk that uses
> `admin`, for the logo, the seal and the signature (§ 19's reset uses it too, for the removes `hr.head` may not make).

---

## 1. How the company schedule hangs together

### 1.1 Two halves that meet in one place

Recruitment is a chain, employees is a hub, leave is a ledger, attendance is a pipeline.
**The company schedule is two registers that share a calendar.**

```
 ┌─────────────── THE CALENDAR HALF ─────────────────────────────────────┐
 │                                                                        │
 │   CompanyEvent   EVT-2026-00001                                        │
 │   name · category · type · priority · when · where (site, venue, link) │
 │   organiser · who it is for (scope, unit, visibility) · budget ·       │
 │   resources · catering · replies asked · reminders                     │
 │   Scheduled → Confirmed → Completed      (needs approval? it waits)    │
 │       ↘ Rescheduled  ↘ Postponed  ↘ In progress (by hand) ↘ Cancelled  │
 │   one of a SERIES?  occurrence 3 of 10, linked by a series id          │
 │                                                                        │
 │     ├── EventParticipant  — who is invited (employee OR outside guest) │
 │     │     Not sent → Sent → Accepted / Declined / Tentative            │
 │     ├── EventAttendance   — who actually came, in and out              │
 │     ├── EventTask         — before / during / after, owner and due date│
 │     └── EventAttachment   — agenda, minutes, papers: real files        │
 │                                                                        │
 │   Beside the events, three reference registers:                        │
 │     • CompanyMilestone (+ its files) — anniversaries, achievements     │
 │     • BusinessClosure  — days off for everybody, a site or a unit      │
 │     • the fiscal calendar — FINANCE's years and periods, read-only     │
 └───────────────────────────────┬────────────────────────────────────────┘
                                 │  an event's rooms are bookings linked to it
                                 ▼
 ┌─────────────── THE ROOMS HALF ────────────────────────────────────────┐
 │                                                                        │
 │   MeetingRoom  BRD · Boardroom · 18 seats · Tema Head Office           │
 │   facilities: projector · whiteboard · VC · audio · A/C                │
 │   rules: active? bookable? needs approval? longest booking? days ahead?│
 │                                                                        │
 │            ↓  is held for a window by                                  │
 │                                                                        │
 │   RoomBooking  BK-2026-00001   — by the HR desk, or by staff (portal)  │
 │   room · from · to · purpose · seats · requirements · catering         │
 │   Tentative (waiting for approval) → Confirmed → Completed             │
 │            ↘ Cancelled  ↘ No show                                      │
 │                                                                        │
 │   ⛔ A room cannot be double-booked: the clash is checked under a lock │
 │      on create AND edit, and the search offers only rooms whose own    │
 │      rules — seats, longest booking, days ahead — the booking meets.   │
 └────────────────────────────────────────────────────────────────────────┘

 Above both: the COMPANY CALENDAR and the two DIARIES, which hold nothing of their own —
 they draw each person's events, bookings, closures, holidays, milestones, leave, travel,
 interview panels and training, and show each person only what is theirs to see.

 Day-to-day work → /hr/company-schedule        Set up once a year → /administration/hr/company-schedule
 Every employee   → /me/calendar, /me/room-bookings, and the HR menu's calendar and diaries
```

Six points follow, and they are the six worth landing in a room:

**1. An event is a project, not a diary entry.** It carries a budget and a budget code, the
resources and the catering it needs, a task list split into before / during / after with owners and
due dates, the people invited with their replies, the people who actually came, and the papers. A
company durbar for a hundred and forty people is a small project, and this is the shape of one.

**2. The invitation list and the attendance register are two different lists, deliberately.** Who
was asked is not who came. Both are kept, and the gap between them is the useful number.

**3. An event knows who it is for, and that decides who sees it.** Its *scope* (all staff, one organisation unit
and everything beneath it, management, or its guest list), its *visibility* (public, a unit, management, private,
confidential) and *Show on company calendar* together make one audience. That audience is what the calendar and
the diaries show it to, what the clash rule compares, and what *Announce on the intranet* reaches. The form says
how many people that is before you save. "Management" means unit heads and anybody named as a line manager — the
same rule orientation uses (decision D-16).

**4. The room clash is enforced by the database, not by the calendar being polite.** The booking form asks *"which
rooms are free between these two times, for this many people"* and offers only rooms whose own rules the booking
meets. Try it anyway and the server refuses — also for two people pressing *Book* at the same moment, because the
check and the write are taken under one lock. Two whole-company events at the same time in the same place are
refused the same way (chapter 5); any other overlap is a warning.

**5. The split between HR, Administration and the portal is the same split as everywhere else in this product.**
Events and bookings change every day and live under Human Resources. Rooms, closures and milestones are decided
once a year and live under Administration, beside the fiscal calendar Finance keeps. Every employee has the
calendar, the two diaries, the staff event page and room booking, and sees only what is theirs.

**6. It tells people, and it can say what anybody is committed to.** Inviting, moving, postponing, changing,
cancelling, uninviting, a task given or overdue, a booking decided or lapsed, an answer to the organiser: each is
told in the app and by email (§ 1.6), and an invitation carries a calendar file. An hourly sweep sends the
reminders and chases (Rule 6). And the calendar and the two diaries, *My Schedule* and *Team Schedule*, assemble
anybody's weeks from the same seven sources the interview clash check reads: events, room bookings, interview
panels, training, leave, travel, and closures with public holidays. ⚠ On both demo databases the emails go nowhere
and the bell is the demonstration (Rule 3).

### 1.2 The tables

Twelve of the module's own, in the order it uses them, then the ones it reads or has retired.

| # | Table | One line |
|---|---|---|
| 1 | `CompanyEvents` | The event itself — 78 columns, from priority to catering. The final closure added the organisation unit (replacing the department, D-5), the series id and occurrence number (D-12), the event it came from (an emergency drill, C-51), and the calendar-file sequence (D-14) |
| 2 | `EventParticipants` | Who is invited: an employee **or** an outside guest, with their answer. One row per person per event — a unique index refuses a second |
| 3 | `EventAttendances` | Who actually attended, with check-in, check-out and who marked it. One row per person per event |
| 4 | `EventTasks` | What has to happen before, during and after — owner, due date, priority, and when its overdue chase went |
| 5 | `EventAttachments` | Agenda, minutes, presentation, handout, resource — **real files** through the upload gate since lane 2h (scanned, filed in the document store); an older row that only named a path reads *"reference only"* |
| 6 | `MeetingRooms` | A bookable room: site, placement, seats, facilities, booking rules |
| 7 | `RoomBookings` | A room held for a window, with a purpose and, from the desk, an optional linked event |
| 8 | `CompanyMilestones` | Anniversaries, achievements, launches, targets, certifications — a yearly one falls every year |
| 9 | `CompanyMilestoneDocuments` | A milestone's files — the certificate, the citation — through the same upload gate *(new in the final closure, D-3)* |
| 10 | `BusinessClosures` | Days off: for the whole company, one site or one organisation unit, or reduced operations; once or every year |
| 11 | `CompanyProfiles` | The legal identity — statutory numbers, addresses, the letterhead's text |
| 12 | `CompanySealAssets` | The logo, the seal and the specimen signature every generated HR letter carries — each versioned, PNG or JPEG |

**Read, never written:** Finance's `FiscalYears`, `FiscalPeriods` and `YearEndBookCloseCycles` — the fiscal
calendar (chapter 15). **Retired:** HR's own `FiscalYear` and `FiscalPeriod`, read by nothing since lane 4b
(Conventions). **Written beside the module's own:** `Notifications` (the bell), the workflow engine's instances (an
approval), `HrAnnouncements` (an announced closure or event), and the leave and attendance rows a closure re-counts
(Rule 1).

### 1.3 The vocabularies

Two families matter, and every value in them is now reached by something; the rest are look-ups.

**Event status** — seven values, and *awaiting approval* is not one of them: it is an event that needs approval
and has not had it (Rule 2), whatever its status.

| Status | Set by |
|---|---|
| *Scheduled* | where every event starts |
| *Confirmed* | approval, on an event that needs it; or the edit form, on one that does not |
| *Rescheduled* | **Reschedule** — and an edit that moves the dates, which goes the same way and asks for a reason |
| *Postponed* | the edit form; the guests are told, and their calendar entry is withdrawn until a new date |
| *In progress* | the edit form only — nothing sets it by itself |
| *Completed* | **Complete**, never before the event has started |
| *Cancelled* | **Cancel**, **Reject**, or a series cancelled from an earlier date; its rooms are released |

**Booking status** — five, every one reached since lane 3:

| Status | Set by |
|---|---|
| *Tentative* | a booking of a room that needs approval, while it waits |
| *Confirmed* | a booking of a room that does not, or an approved one |
| *Completed* | the hourly sweep, once a confirmed booking's end has passed (Rule 6) |
| *Cancelled* | **Cancel** (with a reason); **Not approve**; the sweep, for one still Tentative at its start; retiring its room; cancelling or deleting its event |
| *No show* | **Mark no-show**, by the desk, after the start — for good |

The rest, for reference:

| Family | Values |
|---|---|
| **Event category** | Meeting · Training · Company Event · Deadline · Conference · Social Event. *Holiday* and *Milestone* are refused for a new event, because each has its own register — public holidays in the holiday calendar, milestones in chapter 14; older rows keep them |
| **Event type** | Internal · External · Client Meeting · Statutory · Board Meeting |
| **Priority** | Critical · High · Medium · Low — a label only, and the form says so |
| **Location type** | On Site · Off Site · Virtual · Hybrid |
| **Audience (scope)** | All Staff · Department (an organisation unit and everything beneath it) · Selected (the guest list) · Management Only · External Only |
| **Visibility** | Public · Private · Department · Management · Confidential |
| **Repeats** | Every day · Every weekday (Mon–Fri) *(new)* · Every week · Every two weeks · Every month · Every quarter · Every year |
| **Series scope** | This date only · This and following dates · Every date in the series |
| **Participant role** | Organizer · Presenter · Attendee · Optional · Facilitator |
| **Invitation status** | Not Sent · Sent · Accepted · Declined · Tentative · No Response. An answer is one of Accepted, Declined or Tentative — *"May attend"* on the staff screens |
| **Attachment type** | Agenda · Minutes · Presentation · Handout · Resource Material |
| **Task stage** / **status** | Preparation · During Event · Follow Up / Not Started · In Progress · Completed · Cancelled. *Overdue* is worked out from the due date when the task is read, never set |
| **Room type** | Conference Room · Boardroom · Training Room · Huddle Room · Auditorium |
| **Closure kind** | Whole company · One site · One organisation unit · Reduced operations *(on the wire: Full, Station, Department, Partial)* |
| **Milestone category** | Company Anniversary · Achievement · Product Launch · Target/Goal · Certification |
| **Company image** | Logo · Seal · Signature |

### 1.4 What the settings decide

Have this ready. Somebody in a facilities role *will* ask about the rooms, and somebody from the executive office
about the events. **Every setting on both forms now does what it says**; the four ghosts round 4 listed are gone.

| Setting on a room | **What it decides** |
|---|---|
| **Can be booked** | a room that cannot is not offered by the search, and is refused on save |
| **Active** | an inactive room is not offered and cannot be booked. Making a room inactive, or deleting it, with bookings still to come **lists them and offers to cancel them and tell their bookers** (D-18) |
| *(the time slot)* | *"There is a conflicting booking for this time slot"* — on create **and** on edit, under one lock, so two people pressing *Book* at once cannot both get it |
| **Bookings need approval** | the booking waits *Tentative* and the engine asks the HR desk; the booker may not approve it; an approved booking moved to another time asks again (Rule 2) |
| **Seats** | the search offers only rooms that seat the party, and the save refuses more people — counting the larger of the booking's expected attendees and its event's estimate |
| **Longest booking (hours)** | the search leaves the room out for a longer window, and the save refuses it, with a sentence naming the limit |
| **Book up to (days ahead)** | the same, for a booking too far ahead |

A room with **any booking on record** cannot be deleted, only made inactive, so its history stays readable.

| Setting on an event | **What it decides** |
|---|---|
| **Organiser** | whose event it is: committed in their diary and the clash check, reminded with the guests, told in the app when an invitee answers for themselves — and refused as its approver |
| **Requires approval** | the event waits on the engine (Rule 2); its invitations, reminders and calendar audience wait with it |
| **Who it is for** — scope, unit, visibility, **Show on company calendar** | who sees it on the calendar and in the diaries, what the clash rule compares, and whom *Announce on the intranet* reaches (§ 1.1, point 3). The form shows the count before you save, and warns of a unit nobody is in |
| **Show on intranet** | marks it to announce: *Announce on the intranet* appears on its page, and HR presses it — never on save (L1-1) |
| **Requires RSVP** / **RSVP deadline** | the deadline is required, on or before the start; everybody unanswered is chased **once**, two days before it by default (Rule 6); an invitee answers until then |
| **Send reminders** / **Days before** | *Days before* is required when reminders are on; everybody not declined is reminded **once**, that many days before, and again after a move |
| **Repeats** | every date is made at once, up to 52 (Rule 4) |
| **Site** | where it is: the clash rule's "same place", the calendar file's location. Only sites where staff can be placed are offered (§ 1.7) |
| **Estimated attendees** | counted against a linked room's seats |
| **Priority** | a label for the registers, and the form says so |
| **Budget**, **budget code**, **resources**, **catering** | recorded and shown on the event page; nothing in Finance or Procurement reads them |

> **Say it like this, once, in chapter 5, and you will not have to defend it later:**
>
> *"An event here is a complete record of an occasion — what it is, who it is for, who is coming, what it costs,
> what has to be done and by whom. And it looks after itself: it is approved where it must be, the people invited
> are told and reminded, anybody who has not answered is chased before the reply-by date, and a meeting that
> repeats is every one of its dates, each with its own register."*
>
> ⚠ Keep the tense honest on a demo database: the system **sends** the emails, a mail server **delivers** them,
> and neither demo database has one (Rule 3). The bell is delivered.

### 1.5 Where the approval happens

**On the workflow engine, like every other approval in HR** (lanes 2b and 3b-1, decision D-10). Rule 2 has the
behaviour; this is the comparison a technical buyer asks for.

| | Before the final closure | Now |
|---|---|---|
| Who approves | anyone with `HR.Company.Write` | whoever the engine names — the HR desk, on both definitions |
| How they know | they did not | the workflow inbox, and a notice in the app that opens the record |
| What is kept | an approver id and a date | the engine's record — definition, step, approver, decision, time and comment; an event shows it on its *Workflow* tab |
| Self-approval | always allowed | refused to the organiser or booker and to whoever created it, with the reason |
| An event that needs none | Approve still worked | nothing to approve; *Confirmed* is the edit form's to set |
| A move after approval | kept its approval | asks again |

> *"An event or a room that needs a decision goes to the same approval engine as leave, travel and requisitions.
> The HR desk is asked, in its inbox and in the app; nobody can approve something they organised, booked or
> raised; and the record keeps who decided, when and why. Until it is approved, nobody is invited and nobody is
> reminded."*

Approve on the record's own page; the generic inbox opens that page but cannot apply the decision to it
(cross-module #15).

### 1.6 Who is told, and how

Every notice in this module goes **two ways**: **in the app** (the bell) to everybody with a login, and **by
email** where a mail server is set up. Neither demo database has one (Rule 3). Nobody is told of their own act —
except a reminder or a chase, which goes to the sender too. A notice never makes the act fail.

| Who | Is told when | In the app | Email |
|---|---|---|---|
| **A guest** | invited (one date, or several dates of a series at once) · reminded · chased for an answer · the event moved, postponed, cancelled, or its venue, site or joining link changed · taken off the list | ✅ | ✅ with a **calendar file** on every one but the reminder and the chase |
| **The organiser** | reminded, with the guests · the event approved, or not approved · **a guest answers for themselves** (in the app only) | ✅ | ✅ (not the answer) |
| **A task's assignee** | given the task, or passed it · the task is overdue (once) | ✅ | ✅ |
| **A booker** | approved · not approved · cancelled — by the desk, with its event, by retiring the room, or unapproved at its start · marked a no-show · several of theirs changed together (one notice listing them) | ✅ | ✅ |
| **An approver** | an event or booking waits for them | ✅ the engine's own | the engine's |
| **Everybody a closure covers** | the closure is announced — on HR's click only (L1-1) · their own leave is re-counted (Rule 1) | ✅ | the re-count, always; the announcement, where its topic is set to email |

**The calendar file** (decision D-14) puts the event in the guest's own calendar — Outlook, Google, a phone —
and keeps it there. It carries one stable identity per event and a sequence number raised with each change, so a
move **updates** the guest's entry instead of adding a second, and a cancellation, a postponement or a removal
**withdraws** it. An outside guest answers from their mail client to the organiser, and HR records the answer at
the desk.

Each email is a template the tenant can reword (*Letter & Email Templates*, § 18) — seventeen of them are this
module's.

### 1.7 Two design decisions worth a sentence each

Both answer a question a technical buyer asks.

**"Who is recorded as the organiser?"** — Whoever the form names. Every other actor in this module comes from the
signed-in person, and none is on a form: they used to arrive as query parameters, which would have let any caller
file a record under somebody else's name. The organiser is the one exception, on purpose (decision D-11). An HR
officer keys in the board meeting, and the Managing Director is its organiser. The creator is stamped beside them,
and neither may approve it.

**"What is the difference between Site and Location on a room?"** — They are two different fields, and both are
required. **Site** is the entry in the corporation's location tree — Tema Head Office, Ashaiman, Ho. **Location**
is free text saying where in that site the room is — *"East wing, past reception"*. The room's site originally
pointed at an empty table nothing could fill, which made the form unusable; it was repointed to the live location
tree the rest of HR uses.

> **The site pickers list sites only** (lane 5a, C-16). Every site picker in the module — on rooms, events and
> closures — offers the active locations at a level where staff can be placed. On TDC's tree that is the eight
> sites and offices, not *Ghana* or *Greater Accra*. It matters more than tidiness: a closure "at" a region would
> reach nobody, because staff are placed at a site, not at a region. A record saved on another level before the
> change still shows it when edited.

---

## 2. Before the room fills — the prep

**Time needed: 20 minutes the evening before.** Two things decide how this module demonstrates: **which database
you are on** (§ 2.1), and whether you have **a second HR window** for the approval beat (§ 2.5). Every seeded date is
relative to the day the database was built, and the hourly sweep has been working on it since (Rule 6).

### 2.1 Which database are you on?

The walks fork on it. Find out first: the events register tells you in a second, because on UAT its first pages
carry the *R4D* rows below.

| | **UAT** (`ErpSystemDB_UAT`, read on 2026-10-06) | **Rebuilt** — a database the demo pack builds today |
|---|---|---|
| **Staff** | **TDC's staff list** — 4,949 people numbered `TDC/…` beside the personas | the demo pack's workforce and personas only |
| **A closure saved live** | re-counts real people's leave and tells them — make it *Reduced operations* (Rule 1) | safe to show in full |
| **The two *Announce* buttons** | 🚫 reach real staff | reach the demo's people |
| **Approval definitions** | live, asking `hr.head` and `hr.officer` | seeded, asking the same two |
| **Mail server** | none | none |
| **Test leftovers** | **54 *R4D …* events** a month ahead (*Board meeting* and *Declined meeting*, two per run of the recruitment suite, whose interview panels point at them), and **10 cancelled *R4NB2 …*** events from the templates suite | none, until a suite is run against it |
| **The anniversary** | *TDC 74th Anniversary* dated 18 October **2026**, so it reads as its first year — no "74th" | dated from the founding, 1952: *"74th anniversary"* |
| **The second booking** | never existed (R4-2.3) | `head.dev`'s, made from the portal |
| **The seeded booking** | 1 October — past, and *Completed* by the sweep | two days after the build (moved to a weekday) |

> ⚠ **On UAT the 54 *R4D* events are on the HR desk's register, landing and calendar.** They belong to the
> recruitment suite, which still owes its own clean-up; lane 3a's ruling left them, and this module may not
> delete them. Narrow the register to the next fortnight, or say what they are: *"those are the test suite's — a
> real register would not carry them."* Re-dating UAT's anniversary to 1952 is a one-row data edit nobody has
> ruled on yet.

### 2.2 What a rebuilt database holds

`scenarios/110-company-schedule.mjs` builds it through the real API, as the real personas; the drill's event comes
from `170-she.mjs`, and the two approval definitions from the database seeder.

| Table | What is there |
|---|---|
| `MeetingRooms` | **3** — *Boardroom* (BRD, 18 seats, floor 4, video and audio), *Conference Room A* (CONF-A, 30 seats, floor 2), *Huddle Room 1* (HUD-1, 6 seats, floor 3). All at Tema Head Office, all with projector, whiteboard and A/C. **None needs approval, and none sets a longest booking or a days-ahead limit** |
| `RoomBookings` | **2** — *Management Committee — September*, the Boardroom, 09:00–12:00 two days after the build, video link for Ho, booked at the desk by `hr.head`; and *Community 25 design review with the consultants*, Conference Room A, 14:00–16:00 four days after the build, 20 people, booked **from the portal by `head.dev`** (fixed in lane 6 — it never existed before). Both *Confirmed*, and *Completed* by the sweep once they end |
| `CompanyEvents` | **5** — scenario 110's four below, and *Emergency drill: Fire Emergency Response Plan*, all-day 75 days after the build, made by Safety's own drill record (chapter 6 shows where it came from). **All have reminders on; none needs approval** |
| `EventParticipants` | **17** across scenario 110's four, including one **outside** guest — *Nana Kwame Baffoe, Board Secretariat*, a Facilitator. `hr.head` is the organiser of all four and a guest of each |
| `EventAttendances` | **5** on the retreat — four present, one absent with a reason |
| `EventTasks` | **8** — four on the durbar (one completed), two on the board meeting, two on the fire drill |
| `EventAttachments` | **4** real PDF files — the board's agenda and its Q2 minutes, the durbar's programme, the retreat's communiqué — each downloadable |
| `BusinessClosures` | **1** — *Year-end stocktake*, 29–30 December, whole company, staff paid, once |
| `CompanyMilestones` | **1** — *TDC 74th Anniversary*, 18 October, founded 1952, yearly, on the calendar |
| `CompanyProfiles` | **1** — the full legal identity: CS-1952-000118, TIN, VAT, SSNIT employer number, TDC House Community 1, the signatory and the letter footer |
| `CompanySealAssets` | **2** — a seal and a specimen signature, uploaded as **admin**. No logo: letters carry the tenant's own logo, if it has one (chapter 17) |
| Workflow definitions | *Company Event Approval* and *Room Booking Approval*, each asking the HR desk |

**The four events of scenario 110, and why each is there:**

| Event | Shape | What it demonstrates |
|---|---|---|
| **Board of Directors — Q3 meeting** *(about twelve days out)* | Board Meeting · Management Only · Management visibility · replies asked, **reply by three days before, 17:00** · Boardroom · 14 expected | six guests including the outside one, **two answers already in**, two board papers, two tasks — and an audience of **management**, so it is on a unit head's calendar and not on `staff`'s (Rule 5) |
| **Annual Staff Durbar** *(45 days out)* | Company Event · All Staff · **all-day** · GHS 48,000, code HRA/EVT/2026/03 · 120 expected | the **budget and logistics** half — canopies, PA, staging, catering for 140 — a four-task plan across all three stages, and **on everybody's calendar** |
| **Fire drill — Head Office** *(about nine days out)* | Training · 10:00–11:00 · Medium priority | the drill as HR keyed it, with its marshals' tasks; Safety's own drill made the all-day *Emergency drill* event beside it |
| **Management retreat — 2026 budget preparation** *(38 days ago)* | Meeting · Management Only · Volta Serene Hotel, Ho · GHS 62,000 | the **only completed event**: an attendance register, an actual attendance of 4, and a real outcome summary |

> **The retreat is the most valuable record on either database** and the easiest to miss, because the register
> lists the latest dates first and the retreat is past. It is the only place the attendance register and the
> outcome summary have anything on them. Chapter 6 opens it deliberately.

**What the sweep will have done by demo day** (Rule 6). Each reminder reaches every guest with a login through the
bell; the outside guest is never reached (Rule 3).

| Event | What goes, and when | Its *Invitations and reminders* card then reads |
|---|---|---|
| Fire drill | the reminder, 3 days before — about **6 days after the build** | sent, with the date, and how many it reached |
| Board meeting | the **chase** of the unanswered, 2 days before the reply-by date — about **7 days after the build**; the reminder, 3 days before — about **9 days after** | each, sent and reached |
| Durbar | the reminder, 7 days before — about **38 days after the build** | sent and reached |
| Retreat | nothing — it was over before the build | that no reminder will go, because it is over |

The board pack's task, due five days after the build, is **chased once** when it is overdue, to `md.tdc`.

**UAT on 2026-10-06**, for comparison: the four events were built on about 2026-09-29. The fire drill is 8 October, the
board meeting 12 October, the durbar 13 November, the drill's own event 13 December, and the retreat was 22–23 August.
The board pack's task was due on 4 October and has been chased.

### 2.3 Write down the numbers you will quote

They are the only figures you will say out loud.

| Screen | What to write down |
|---|---|
| `/hr/company-schedule` | Next 30 days: ____ events · Bookings awaiting approval: ____ |
| `/hr/company-schedule/events` | events in the next fortnight: ____ *(on UAT, narrow the dates, or the R4D rows count too)* |
| `/administration/hr/company-schedule/rooms` | bookable rooms: ____ |
| `/administration/hr/company-schedule/fiscal-calendar` | the current year: ____ · the next one Finance must open: ____ |

⚠ **"Bookings awaiting approval" reads 0 on both databases**, and that is right: no seeded room needs approval,
so every booking was confirmed as it was made. § 2.5 makes one wait, live — a much better demonstration than a
queue prepared in advance.

### 2.4 Pick your clash

Chapter 9's best moment is a booking the server refuses because the room is taken. You need a room and a window
that is **already booked**.

- **Rebuilt:** the Boardroom, 09:00–12:00 two days after the build, and Conference Room A, 14:00–16:00 two days
  later — each in the past by a demo a week after the build.
- **UAT:** the only booking was on 1 October.

So, on either, **make your own tonight**: book the Boardroom as `hr.head` for the morning after the demo, 09:00–12:00,
through chapter 9's own form. It is a live write; cancel it afterwards (§ 19).

> Boardroom busy: ______________  ☐ seeded  ☐ my own, booked tonight

### 2.5 Decide about the two approval beats

Both need **two HR windows**, because nobody approves their own (Rule 2). They are the strongest five minutes in
the module, and each has a choice to make tonight.

**Beat A — an event that waits for approval** *(chapters 5 and 6)*. `hr.head` creates an event with *Requires
approval* on and invites `staff`. It waits; nothing reaches `staff` (the bell is quiet, the portal calendar does
not show it). `hr.officer` approves it on its page, and in that moment the invitation goes out: `staff`'s bell has
it, and the portal calendar draws it, dashed, waiting for an answer. No preparation beyond the windows.

**Beat B — a booking that waits** *(chapters 12, 9 and 10)*.

1. turn *Bookings need approval* on for the **Boardroom** *(chapter 12, a live write)*;
2. book it live — at the desk, where the toast says *"It needs approval before it is confirmed."* *(chapter 9)*, or
   as `staff` from the portal, where it says *"It waits for approval; you will be told either way."* *(chapter 10C)*;
3. `hr.officer` approves it on the booking's page, and the booker's bell says so *(chapter 10)*.

That needs chapter 12 **before** chapter 9. To run the book in order instead, set the switch now, tonight; chapter 12
then shows it already on. Either way, **approve or cancel the booking before its start**, or the sweep cancels it
for you (Rule 6).

> Tonight I set *Bookings need approval* on: ☐ Boardroom  ☐ nothing — running in book order

**An optional third beat: a room that refuses on its own terms.** Set **Longest booking (hours)** to **2** on the
Huddle Room in chapter 12, then in chapter 9 search a three-hour window for four people. **The Huddle Room is not
offered**: since lane 3a the search applies every rule the room has, not only its seats. Clear the field
afterwards (§ 19).

### 2.6 Windows and personas

| Window | Persona | For |
|---|---|---|
| **A** | `hr.head` — the HR desk, a unit head, an organiser | every desk chapter |
| **B** | `hr.officer` — the second HR desk login | approving what `hr.head` raised (§ 2.5), and a second bell |
| **C** | `staff` — an ordinary employee in the Development Department | the portal: the calendar (Rule 5), the staff event page and an answer, booking a room; a guest's bell |
| **D** *(only for chapter 17)* | `admin` | replacing the logo, the seal or the signature (`HR.Company.Admin`; `admin` has no employee record) |
| *(optional)* | `head.dev` — head of the Development Department, `staff`'s unit | Team Schedule as a unit head who is not on the HR desk (chapter 10B) |

The same personas exist on both databases. On UAT `head.dev` heads the Development Department and `hr.head` heads HR
/ Administration. A rebuild's seeder appoints a head for each unit, so before relying on `head.dev` there, check that
Team Schedule opens on their unit (chapter 10B).

### 2.7 Pre-open every screen

| Window | Tab | Route | Used in |
|---|---|---|---|
| A | 1 | `/hr/company-schedule` | ch. 3 |
| A | 2 | `/hr/company-schedule/calendar` | ch. 3a |
| A | 3 | `/hr/company-schedule/events` | ch. 4–7 |
| A | 4 | `/hr/company-schedule/bookings` | ch. 8–10 |
| A | 5 | `/hr/company-schedule/my-schedule` | ch. 10A–10B |
| A | 6 | `/administration/hr/company-schedule` | ch. 11–15 |
| A | 7 | `/administration/hr/settings/company-profile` | ch. 17 |
| B | 1 | `/hr/company-schedule/events` | the approvals |
| C | 1 | `/me/calendar` | ch. 3a, 6a |
| C | 2 | `/me/room-bookings` | ch. 10C |
| D | 1 | `/administration/hr/settings/company-profile` | ch. 17 — **only if** replacing an image |

### 2.8 Prep checklist

- [ ] You know which database you are on, and which column of § 2.1 applies
- [ ] **UAT:** you will make the live closure *Reduced operations*, and press neither *Announce* button (Rule 1)
- [ ] The numbers from § 2.3 are written down
- [ ] A Boardroom booking of your own for the morning after the demo, written down *(§ 2.4)*
- [ ] Windows A, B and C signed in *(§ 2.6)*; D only for chapter 17
- [ ] You have decided about Beat B *(§ 2.5)*, and set the switch if running in book order
- [ ] You have read **Rules 1–7**. You know that **no email leaves either database** and the bell is the
  demonstration (Rule 3), and that **approving takes the second window** (Rule 2)

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

**For the HR desk** (`HR.Company.Read`):

**Header:** *Company schedule* — *"Events, room bookings, closures and the milestones on the company calendar."* No
back-link; this is a group root.

**Six navigation cards**, in a grid:

| Card | Goes to |
|---|---|
| **Company calendar** — *Events, closures, holidays, milestones and bookings, month by month.* | `/hr/company-schedule/calendar` (chapter 3a) |
| **Events** — *Meetings, training days, conferences and company occasions.* | `/hr/company-schedule/events` |
| **Room bookings** — *Who has which room, and what is waiting on approval.* | `/hr/company-schedule/bookings` |
| **Meeting rooms** — *The rooms people can book and the rules for booking them.* | `/administration/hr/company-schedule/rooms` |
| **Business closures** — *Days the organisation is shut, company-wide or per site.* | `/administration/hr/company-schedule/closures` |
| **Milestones** — *Anniversaries, achievements and dates worth marking.* | `/administration/hr/company-schedule/milestones` |

> Three of the six jump into Administration. That is deliberate — an HR officer thinks *"where are the rooms"*, not
> *"is that setup or operations"*.

**Four summary cards**, two by two, each with **See the calendar** in its corner:

| Card | Shows | Empty state |
|---|---|---|
| **Next 30 days** | the first six events not cancelled whose days fall in the next thirty, earliest first: name over `date · category · organiser`, with a status badge | *"Nothing scheduled in the next month."* |
| **Bookings awaiting approval (N)** | up to six *Tentative* bookings: room over `date and time · booked by`. The count is in the title | *"Nothing is waiting on a decision."* |
| **Closures ahead** *(next 60 days)* | up to six, a yearly one at its coming date: title over the date or range · who it covers (*Whole company*, a site, a unit), with a **Paid** / **Unpaid** badge | *"No closures in the next two months."* |
| **Milestones ahead** *(next 90 days)* | up to six, a yearly one at its coming anniversary: title over `date · category · 74th anniversary` | *"Nothing coming up in the next quarter."* |

If the summary cannot be read, one red line says so — *"that is not an empty schedule"* — and no card is drawn.

**For anybody else** — every employee sees this group in the sidebar, because the calendar and the two diaries are
theirs — the page is one card: *"This summary of the company's events, room bookings, closures and milestones is for
the HR desk."* Under it, *What is yours to see*: **Company Calendar**, **My Schedule**, and — for the head of a unit —
**Team Schedule**. Nothing reads as empty because it was refused (R4-3.1, fixed in lane 5a).

### ▶ Walk it

**1 — Open `/hr/company-schedule`.**

> *"Every organisation has a calendar; most of them have it in somebody's Outlook and a printed sheet on a notice
> board. This is the corporation's own, and it carries what a company calendar actually needs to carry — not just
> when, but who it is for, who is coming, what it costs and what has to be done before it."*

**2 — Read *Next 30 days* aloud.**

- **Rebuilt:** the fire drill and the board meeting, perhaps the durbar — name them:

  > *"The board's Q3 meeting. A fire drill at head office. Different occasions, completely different shapes, and the
  > same record behind both."*

- **UAT:** the fire drill (8 October) and the board meeting (12 October), then four *R4D Board meeting …* rows from
  30 October. Name the first two, and say what the rest are (§ 2.1) before anybody asks.

**3 — Point at *Milestones ahead*.**

- **Rebuilt:** *TDC 74th Anniversary · 18 October · Company Anniversary · 74th anniversary.*

  > *"Founded in 1952 to develop the Tema township, and on the calendar every year — counted, because a corporation
  > that forgets its own anniversary has forgotten something."*

- **UAT:** the same row without *"74th anniversary"*, because UAT's milestone is dated 2026 (§ 2.1). Say the line
  without the number.

**4 — Point at *Closures ahead*.** The year-end stocktake (29–30 December) is listed only within sixty days of it —
from 30 October. Earlier than that the card reads *"No closures in the next two months"*; say so, and show the
stocktake on the calendar in chapter 3a instead:

> *"Closures are a different kind of entry: not an occasion, a day the organisation is shut — and since this year,
> a day nobody's leave is charged for."*

**5 — *Bookings awaiting approval* reads 0** on both databases (§ 2.3). Say why rather than skipping it:

> *"Nothing waiting on a decision, because none of our rooms is set to need approval — a choice made per room. I
> will turn it on for the boardroom later, and you will see what it changes."*

**6 — Click *Company calendar*** and continue into chapter 3a.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The four cards | `GET api/CompanySchedule/dashboard` — one read since lane 2g-1: events overlapping the next 30 days, *Tentative* bookings, closure occurrences in the next 60, milestone occurrences in the next 90 | `HR.Company.Read` |
| *Team Schedule* link, for a refused caller | `GET …/team-schedule/units` — offered only when the caller heads a unit | any internal login |

The page asks for the summary only when the signed-in person holds `HR.Company.Read`; anybody else is never refused,
because nothing is asked.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**R4-3.1** · the landing answered a refusal with "Nothing scheduled"~~ | **Fixed in lane 5a** — the card for the HR desk, above |
| ✅ ~~**C-9** · no calendar view anywhere~~ | **Fixed in lane 7** — chapter 3a |
| ✅ ~~**C-10** · four requests~~ | **Fixed in lane 2g-1** — one read |
| **F-64** · an event awaiting approval reads *Scheduled* here and in the register | Only its own page, the calendar and the CSV export say *awaiting approval*. Found while rewriting (lane 6) |

---

## 3a. `/hr/company-schedule/calendar` and `/me/calendar` — the company calendar

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Company Calendar** · `/hr/company-schedule/calendar` — and in the portal,
**Company → Calendar** · `/me/calendar` · window A as **hr.head**, window C as **staff** · **5 minutes**. No
permission is needed for either: the server decides what each person sees (Rule 5).

### 📖 What it is

> *"One calendar for the whole organisation — and everybody sees their own version of it. HR sees everything. A member
> of staff sees what they are invited to, what is for them, the days the office is shut, and their own leave and
> travel. The same page, the same screen, a different answer for each person."*

### 👁 On the page

The two doors draw **the same component**. The HR page's header reads *Company calendar — "Events, closures, public
holidays, milestones and room bookings — and your own leave, travel, panels and training."*; the portal's reads
*Calendar — "What is on — for the company and for you. Click anything to see it, or to answer an invitation."*

**The toolbar:**

| Control | Does |
|---|---|
| **‹** · **Today** · **›** | a month or a week back, this month or week, a month or a week on; the title reads *October 2026*, or *5 Oct – 11 Oct 2026* |
| **Month** / **Week** | the two views |
| **Room picker** | *Every room* for the HR desk (*My bookings only* for anybody else), then each room by name. Choosing one is **the room view** (below) |
| **Book this room** | appears once a room is chosen: opens the booking form with the room and the day filled in — the desk's form for the HR desk, the portal's for anybody else (chapters 9 and 10C) |
| **The kind chips** | *Events · Closures · Public holidays · Milestones · Room bookings · My leave, travel, panels and training* — each is both the **legend** (its colour) and a **filter** (press to hide or show). Beside them, a dashed swatch: *waiting for your answer* |

**The month view** — six weeks from the Monday before the first, today shaded, other months' days greyed. **An entry
over several days is one band across them**, broken at the end of each week with **◂** and **▸** to say it carries on;
a timed entry starts with its time. Four bands fit a week row; more are counted per day as **+n more**, which opens that
week. A day's number opens its week too.

**The week view** — the all-day and several-day entries as bands across the top; the timed ones listed under their
day, in time order, as *09:00–16:00* over the name.

**The colours:** events blue — **dashed** while the invitation waits for your answer, struck through once you decline;
closures red (orange for reduced operations, labelled *"(reduced operations — a working day)"*); public holidays green
(*"Day off in lieu of Boxing Day"* for a substitute day); milestones purple, labelled with their years (*"TDC 74th
Anniversary (74 years)"*); room bookings grey (darker for your own); your own leave amber, travel cyan, interview panels
indigo, training teal. An event awaiting approval is in italics.

**Clicking an entry** opens its card: the name, when, a badge for its kind, its number, and *Awaiting approval* or
*You organise it* where they apply. For an **invitation of your own**, the card shows where it stands (*"You have not
answered yet"*) and, while you may still answer, **Accept · May attend · Decline** with *A note for the organiser
(optional)* — and on a series, **Which dates**: *This date only · This and the following dates · Every date still to
come*. Where you may not answer, it says why (*"The reply-by date … has passed. Ask the organiser to record your
answer."*). For an event that is yours to see but not to answer: *"You are not on its guest list; it is on your
calendar because it is for you."* **Open** goes to its page: the HR event page for the desk, the staff event page
(chapter 6a) for anybody else; a closure or a milestone opens its register for the desk; a booking its page.

**The room view** — with a room chosen, a line says *"Showing when **Boardroom** is booked"*, and for anybody but the
desk *"— other people's bookings show only as 'booked'"*. A booking that is not yours reads *Boardroom: booked*, with
no purpose, number or booker.

If a source of *your own* entries could not be read, a banner names it, as on the diaries (chapter 10A).

### ▶ Walk it

**1 — Window A (`hr.head`): open the Company Calendar** on the month view.

> *"Here is the whole company's month. The events in blue, the days off in green and red, the anniversary in purple,
> the rooms in grey — and, in amber and indigo, my own leave and my interview panels, because a calendar that does
> not show what I personally am doing is only half a calendar."*

- **UAT:** the fire drill (8 October), the board meeting (12 October), the anniversary (18 October), `hr.head`'s
  interview panels and leave — and, from 30 October, a crowd of *R4D* events that the week's **+n more** counts.
  Click **+n more** to show the week view, then say what they are (§ 2.1).
- **Rebuilt:** the seeded events, the anniversary and the two bookings.

**2 — Point at a dashed band.** On either database `hr.head` is a guest of the board meeting, the durbar and the fire
drill, and has not answered.

> *"Dashed means it waits for my answer. Click it —"* (the card opens) *"— and I can answer from here: accept, may
> attend, decline, with a note to the organiser."*

Close the card without answering — chapter 6a answers one live, from the portal.

**3 — Press *Events* in the chips** to hide them, then again to bring them back.

> *"The legend is the filter. If all you want is who is away and which days are shut, take the events off."*

**4 — Move on to December** (**›**, once a month). On the way, November shows the durbar on the 13th. In December the
stocktake on 29–30 December is one red band across two days; Christmas, Boxing Day and the day off in lieu of it are
green; and the Safety drill's event on the 13th is blue (chapter 6 shows where it came from).

**5 — Window C (`staff`): open the portal's *Company → Calendar*** on the same month.

> *"Same calendar, a member of staff. No board meeting — that is for management. No R4D test events, nobody's
> bookings, no interview panels. What they do see is everything that is theirs: the fire drill and the durbar, which
> are for all staff; the anniversary; the stocktake; and their own leave in October — one approved, one still
> waiting."*

*(That leave is UAT's. On a rebuilt database it is whatever leave the demo pack gave `staff`.)*

**6 — Still in window C, choose *Boardroom* in the room picker.**

> *"And this is the room view. When is the boardroom free? Other people's bookings show only as 'booked' — not what
> the meeting is, not who is in it. And from here, Book this room."* (Do not press it here — chapter 10C books from
> the portal.)

Clear the room picker afterwards.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The entries | `GET api/CompanySchedule/calendar?from&to&roomId` — at most sixty days a read (the month view asks for forty-two) | any internal login; the desk is decided by `HR.Company.Read`, checked on each request |
| The room picker | the desk: `GET …/rooms`; anybody else: `GET …/me/rooms` | Read / any internal login |
| An answer | `POST …/events/{eventId}/participants/{participantId}/reply` — **the invitee's own door** (lane 7a, D-8): their own invitation or a 404; the reply window the server holds (§ 6a) | any internal login |

`CompanyCalendarService` (`Services/HR/CompanySchedule/`) builds one list from five layers and the caller's diary: the
events the caller may see (the desk: every live event; anybody else: organised, invited once sent, or the audience of
— each audience rule resolved once), closure occurrences and whom they cover, calendar milestones on every anniversary
in range, public holidays from the default holiday calendar, bookings — and, as *Mine*, the caller's own leave, travel,
interview panels and training from the diary, never repeating what the company layers already show.

### ⚠ Known gaps

| Gap | |
|---|---|
| **F-62** · to the HR desk, an event it is not invited to says *"it is on your calendar because it is for you"* | The desk sees every event, so the sentence is true only for staff. Found while rewriting (lane 6) |
| *By design* · an outside guest has no calendar | They answer from their mail client, to the organiser's address on the calendar file, and HR records it (§ 1.6) |

---

## 4. `/hr/company-schedule/events` — the register

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Events** · `/hr/company-schedule/events` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Every occasion the organisation has scheduled, past and future, in one list — with its number, its category,
> when and where it is, who is running it and what state it is in — searched, filtered and exported on the server,
> so it works the same at four events or four thousand."*

### 👁 On the page

**Header:** *Company events* — *"Meetings, training days, conferences and company-wide occasions."*, a back-link to the
landing, and two buttons: **Export CSV** (disabled while nothing is found) and **+ New event**.

**The search and filters**, above the table — each runs on the server, and a new one starts again at page 1:

| Control | Finds |
|---|---|
| **Search** — *"Name, number, venue or organiser…"* | any of the four, as you type |
| **Status** | *All statuses*, or one of the seven |
| **Category** | *All categories*, or one of the eight (older rows keep *Holiday* and *Milestone*) |
| **From** / **To** | events whose days **overlap** the dates — a two-day event that starts the day before *From* is found |

**Seven columns:** **Number** (`EVT-2026-00001`) · **Event** — the name, and on a series *"3 of 10"* · **Category** ·
**When** — the start date, then *· all day* or *· 09:00* · **Where** — the venue, else the site, else the location type
· **Organiser** · **Status**. **The latest start date is first.** Each row opens the event.

**Under the table:** *"69 events · page 1 of 3"*, and **Previous** / **Next** — 25 to a page.

**One series** — the event page's *Open in the register* (chapter 6) opens this page on one series: the title reads
*One series*, the dates in order, with **Show all events** beside it.

**Empty states:** *"No events yet — Schedule the first company event."* with the button; or *"No matching events —
Try a different search, filter or dates."*

**Export CSV** downloads the events the filters find — up to ten thousand — in the order shown: *Number · Event ·
Category · Type · Start date · Start time · End date · End time · All day · Site · Venue · Unit · Audience · Organiser
· Status · Approval* (*Not needed*, *Approved 2026-10-07*, or *Awaiting*) *· Occurrence* (*3 of 10*). It opens in
Excel with its names intact, and a cell a spreadsheet would run as a formula is written as text.

### ▶ Walk it

**1 — Open the register.**

- **Rebuilt:** five rows — scenario 110's four and the Safety drill's event.
- **UAT:** *69 events*, the 54 *R4D* and 10 cancelled *R4NB2* test rows among them (§ 2.1). Set **From** to today and
  **To** a fortnight on, before you say a word — the story below works on what is left.

**2 — Read the rows across.** The point is the *variety*, not any one row:

> *"Look how different they are. A board meeting for fourteen people in the boardroom. An all-day durbar for a hundred
> and twenty on the forecourt. A one-hour fire drill. And a drill Safety scheduled itself — chapter 6 shows how."*

**3 — Point at the *Number* column.**

> *"Every event is numbered — EVT, the year, a sequence that never repeats, even after a delete. Small thing, and it is
> the difference between 'the durbar' and a record somebody can reference in a memo."*

**4 — Clear the dates and type `retreat` in the search.** The *Management retreat — 2026 budget preparation* row,
*Completed*.

> *"And it searches the name, the number, the venue and the organiser, on the server — this is not filtering a page,
> it is asking the whole register."*

**5 — Press *Export CSV*.** A file downloads; open it if a spreadsheet is to hand.

> *"The same search, as a spreadsheet: who organised it, who it was for, whether it needed approval and got it, which
> date of a series it was."*

**6 — Click the retreat row** and continue into chapter 6.

⚠ **Open the retreat before the board meeting.** It is the only event with attendance and an outcome; leading with it
makes chapter 6 twice as good, and the board meeting comes next for its guest list.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The table | `GET api/CompanySchedule/events/search?text&status&category&from&to&seriesId&sort&page&pageSize` — sorted and paged on the server (lane 2g-1) | `HR.Company.Read` |
| **Export CSV** | `GET …/events/export`, with the same filters, `text/csv` | `HR.Company.Read` — the user's ruling: whoever may read the register may export it |

`SearchAsync` sorts and pages narrow rows first, then reads the page's events with their organiser, site and unit:
sorting whole rows, each carrying an `Employee`, once asked UAT's server for 387 MB.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-10, C-11, C-12, C-13** · four requests; everything filtered in the browser; no date filter; no export~~ | **Fixed in lane 2g-1** |
| **F-64** · an event awaiting approval reads *Scheduled* in the Status column | The CSV's *Approval* column says *Awaiting*; the event page and the calendar say it. Found while rewriting (lane 6) |

---

## 5. `/hr/company-schedule/events/new` — scheduling an occasion

### 📍 Where you are

**From:** the register → **+ New event** · `/hr/company-schedule/events/new` · as **hr.head** · **6 minutes**. Also
opened by Team Schedule's *Schedule for this unit* (chapter 10B), with the audience, the unit and the day filled in
(`?scope=Department&unit=…&date=…`).

### 📖 What it is

> *"Everything you need to say about an occasion before it happens — who it is for, who runs it, where, what it
> costs — and the form tells you, before you save, how many people that reaches and what it would clash with."*

### 👁 On the page

**Header:** *New event* — *"You are recorded as the organiser."* (⚠ only the default: the *Organiser* field below can
name somebody else, and then you are recorded as its creator — **F-63**), back-link.

**Five cards.** The edit page (chapter 7) uses the same form; the differences are marked.

**Card 1 — Basics**
- **Event name** *(required, at most 100)* · **Description**
- **Category** *(required — "Public holidays and company milestones have their own registers."; a new event cannot be
  either)* · **Type** *(required)*
- **Priority** *(required — "A label for HR's own sorting; it changes nothing about the event.")* · **Status** — *edit
  only*
- **Organiser** — an employee search, *"You — or search for someone else"*. Left empty, it is you (decision D-11)

**Card 2 — When**
- **All-day event** · **Start date** and **End date** *(required)* · **Start time** and **End time** *(hidden when
  all-day; both or neither)*
- **Repeats** — *create only*. On, it says what saving will do: *"Saving makes every occurrence now, each a full event
  with its own number, guest list, replies and register. Give how many times it happens, or the date it runs until —
  one, not both; at most 52, and the series can be extended later. A monthly series on the 31st falls on the last day
  of shorter months. An occurrence on a public holiday or a company-wide closure is made and flagged, not skipped."*
  Then **Repeats** *(Every day · Every weekday (Mon–Fri) · Every week · Every two weeks · Every month · Every quarter ·
  Every year)*, **How many times (2–52)**, **…or until**, **Recurrence notes**

**Card 3 — Where**
- **Location type** *(required — On Site · Off Site · Virtual · Hybrid)*
- *Not purely virtual:* **Site** — the sites only, each labelled with its level (*"Tema Head Office · Site / Office"*),
  or *"Not tied to a site"* · **Venue** · **Venue address**
- *Virtual or hybrid:* **Meeting link** · **Meeting password** — the password reaches the event's guests and organiser,
  never an email, a calendar file or an announcement

**Card 4 — Who**
- **Audience** *(required — All Staff · Department · Selected · Management Only · External Only; new events start on
  Selected: the guest list only)*, with its own sentence: *"Who the event is for: all staff; a unit and the units
  beneath it (Department); management — unit heads and line managers; or only the people you invite (Selected, External
  only)."*
- **Organisation unit** — required for *Department* (*"The unit the event is for, with every unit beneath it."*),
  otherwise optional (*"the unit hosting the event"*)
- **Estimated attendees** · **Visibility** — *"Public follows the audience. Department narrows it to the unit,
  Management to management. Private and Confidential: the guests and the organiser only."*
- **The audience line**, as the server counts it before you save: *"For: Management — unit heads and line managers —
  493 active staff"* (UAT), *"For: Everyone — 5001 active staff"*, or *"For: Its guests and organiser"*; with a ⚠
  warning when the choice reaches nobody (an empty unit, management with no heads named)
- **The clash line** — the events these dates, this audience and this site would meet: **✕ in red**, refused on save
  (two whole-company events, or two for the same unit, at the same time in the same place — the same site, or either
  with no site); **⚠ in amber**, any other overlap with an event for more than its guests, which HR decides. A series
  adds *"This checks the first date; every date of the series is checked when you save."*
- **Requires RSVP** → **RSVP deadline** *(required, on or before the start)*
- **Show on company calendar** *(on by default — "On the company calendar, and in the diary of everyone it is for — who
  may then look busy to an interview panel.")* · **Show on intranet** *("Lets HR announce it to everyone it is for, from
  the event page, once it is approved. Nothing is sent on save.")*

**Card 5 — Approval, budget and logistics**
- **Requires approval before it is confirmed** — *create only*: once an event exists, approval is an action
- **Has a budget** → **Budget amount** and **Budget code** (on the edit page, **Actual cost** too)
- **Required resources** · **Catering** · **Technical**
- **Send reminders** — *"Everybody who has not declined is emailed once, automatically, the days before the event set
  below — and again if the date moves."* → **Days before** *(required when on)*
- **Notes**

**Footer:** **Cancel** · **Schedule event**. The form checks what the server checks before it sends — an end before
the start, one time without the other, a pattern without a count or a date (or both), an RSVP without a deadline or
with one after the start, reminders without days, a unit audience without a unit — so a refusal is seen in place.

### ▶ Walk it

This chapter makes the event that Beat A (§ 2.5) approves in chapter 6.

**1 — Press *+ New event*.** Point at the **Organiser** field before touching anything else.

> *"Whose event it is, is a choice. It defaults to me, but an HR officer keying in the board meeting would put the
> Managing Director here — and the system still records who keyed it in. Neither of them may approve it."*

**2 — Basics.** *"Heads of Unit — Q4 planning"*, Category **Meeting**, Type **Internal**, Priority **High**.

**3 — When.** A weekday next week, 09:00 to 12:00.

**4 — Show *Repeats*, and turn it off again.** Turn it on, choose **Every week**, and read the grey sentence aloud.

> *"Saving this makes every date now — each a full event, with its own guest list, answers and register. A weekly
> meeting for a quarter is thirteen events, and each one keeps its own record."*

Turn it **off**: the approval beat wants one date (Rule 4).

**5 — Where.** **On Site**, Site **Tema Head Office · Site / Office**, Venue *"Boardroom"*.

> *"The site is the corporation's own list of sites — only sites, because staff are placed at a site. The venue is
> what you would write on the invitation."*

**6 — Who.** Choose **Management Only** and stop on the audience line.

> *"Before I save, it tells me who that is: management means unit heads and anybody named as somebody's line manager —
> four hundred and ninety-three people on this database."* (**UAT**; a rebuilt one counts its own.)

Then set the audience back to **Selected** — *"For: Its guests and organiser"* — so that this event reaches only the
people chapter 6 invites. Estimated attendees **12**. Leave *Show on company calendar* on.

**7 — Approval, budget, reminders.** Turn on **Requires approval before it is confirmed**. **Has a budget**: **8,500**,
code *"HRA/EVT/2026/04"*. Catering *"Tea, coffee and a working lunch for twelve."* **Send reminders**, **Days before
2**.

> *"This is where an event stops being a diary entry: a budget with a code Finance will recognise, the catering
> somebody has to order — and because it needs approval, nobody is invited or reminded until it gets it."*

**8 — 🔴 LIVE WRITE 1 — press *Schedule event*.** The toast names the number; you land on the event, its header
showing **Approve** and **Reject** only to the person the engine names — not to you (Rule 2).

> *"EVT-2026-… — scheduled, and waiting for approval."*

*Undo:* **Cancel** on its page, with a reason (chapter 19). `hr.head` cannot delete it.

**Optional — a short series** *(🔴 LIVE WRITE 1b)*: a second event, *Repeats* on, **Every week**, **How many times 3**.
The toast reads *"Series scheduled — 3 occurrences"*. Undo it with **Cancel → Every date in the series** (chapter 6).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| **Schedule event** | `POST api/CompanySchedule/events` — a series makes every occurrence in one call | `HR.Company.Write` |
| The audience line | `GET …/events/audience-preview?scope&visibility&organizationUnitId` | `HR.Company.Read` |
| The clash line | `GET …/events/clashes?…` — the same rule the save applies | `HR.Company.Read` |
| Site picker | `GET api/Location`, `GET api/LocationLevel` | location read |
| Organiser, unit pickers | the employee search; the organisation-unit tree | lookups |

On save `CompanyEventService` checks the window, the references (the site, the unit, the organiser — an active
employee), the clash rule (every date of a series) and the category; stamps the creator; numbers each event from the
atomic sequence; and, where approval is required, starts the engine's request at create (lane 2b). An occurrence on a
public holiday or a whole-company closure is saved and returned as a warning, which the toast shows with ⚠.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-14** · recurrence generates nothing~~ | **Fixed in lane 2f** — every date, at once |
| ✅ ~~**C-15** · no clash check on an event~~ | **Fixed in lane 2g-2** — refused or warned, before and on save |
| ✅ ~~**C-16** · the site picker lists the whole tree~~ | **Fixed in lane 5a** — sites only |
| ✅ ~~**R4-5.1** · reminders with no days send nothing~~ | **Fixed in lane 2a** — *Days before* is required |
| ✅ ~~**F-44** · "Holiday" and "Milestone" categories suggest effects they do not have~~ | **Fixed in lane 2a** — refused for a new event |
| **F-63** · the header says *"You are recorded as the organiser"* | True only while the *Organiser* field is left empty; the creator is recorded either way. Found while rewriting (lane 6) |

---

## 6. `/hr/company-schedule/events/[id]` — the event, and everything that hangs off it

### 📍 Where you are

**From:** any row of the register, the calendar's **Open** for the HR desk, or a notice · `/hr/company-schedule/events/[id]`
· window A as **hr.head**, window B as **hr.officer** for the approval, window C as **staff** to watch it arrive ·
**12 minutes** — the longest chapter in the book, and the one worth the time

### 📖 What it is

> *"One occasion, in full: what it is and who it is for, who was asked and what they said, who came, what has to be
> done and by whom, its rooms, its papers — and who approved it. Plus what you can do to it: approve it, move it,
> close it off, or call it off."*

### 👁 On the page

**Header:** the event's name; *"EVT-2026-00001 · occurrence 3 of 10 · organised by …"* (the occurrence only on a series);
a back-link; and the actions:

| Button | Appears when | Does |
|---|---|---|
| **Approve** · **Reject** — the workflow's buttons | the event needs approval and waits for it — **for whoever the engine is asking**, never its creator (Rule 2) | a dialog with a comment. Approving confirms it and sends the invitations it was holding; rejecting cancels it with its rooms. The organiser is told either way |
| **Reschedule** | the event is open — not cancelled, not completed | a dialog: **New start date** and **New end date** *(required)*, **New start time** and **New end time** (*"Leave the times empty to keep its hours"*), **New RSVP deadline** *(only for an event asking for replies; "Needed only if the current one would fall after the new start. Empty keeps it.")*, **Reason** *(required)*, and on a series **Which dates** |
| **Complete** | open, **and it has started** | a dialog — *"Close off this event"*: **Actual attendance**, **Outcome** |
| **Cancel** | open | a dialog: **Reason** *(required)*; on a series **Which dates** — *"This and following ends the series at this date; their rooms are cancelled with them."* |
| **Announce on the intranet** | open, and *Show on intranet* is on | a dialog: who it reaches (*"It goes to N active staff"*), exactly what they will read, and **Announce to N staff** — or, instead, why it cannot go yet (still awaiting approval, already begun, reaching nobody). 🚫 on UAT (Rule 1) |
| **Edit** | open | chapter 7 |
| **Delete** | **`HR.Company.Admin` only** | a confirmation: *"The event and everything recorded against it are removed. Cancelling instead keeps the history."* Its live rooms are cancelled first |

**Two notes may sit under the header:**

- **A day off** — on a date of a series that falls on a public holiday or a whole-company closure: what it falls on,
  then *"It is kept as scheduled; reschedule it if it should not go ahead that day."*
- **Where it came from** — on an event Safety made: *"From **Emergency drill DRILL-2026-001 — …**"*, linked to the plan,
  then *"Its date follows the drill's next date in Safety: a change there moves or cancels it here."* (lane 2h, C-51)

**Overview card** — a grid of up to twenty-five fields: Status · Category · Type · Priority · Starts · Ends ·
**Repeats** (*One-off*, *Every week — 3 of 10*, or for a row saved as repeating before series existed *"(no occurrences
made)"*) · Audience · Location type · Site · Venue · Organisation unit · **For** (who it is for, in words — *"Everyone"*,
*"Management — unit heads and line managers"*, *"Its guests and organiser"*) · *Meeting link* (**Join**, when not on
site) · Expected · Attended · Visibility · *Budget · Actual cost · Budget code* (with a budget) · *Approved by* and the
date · *Cancelled* — the date and the reason · and, once moved, **Originally** — the window before its first move —
and **Moved on** — the day it was moved, and why.

**Series card** *(on a date of a series)* — *"Series — occurrence 3 of 10"*, **Open in the register** (chapter 4's one
series) and, while there is room under fifty-two, **Extend the series**: a dialog of **How many more** or **…or until**
(*"The latest date's guests are invited to the new dates, once each, and its rooms are booked for them in your name
where they are free."*). Then every date in order: its number, its `EVT-…` (linked), the day and time, its status,
and ⚠ a day-off note where there is one.

**Rooms card** — every booking made for the event, at any status: room, `BK-…`, when, a status badge, each opening its
booking. **Book a room** opens the booking form with the event chosen; on a series, **Book for this and following
dates** too (chapter 9). Empty: *"No room is booked for this event."*

**Invitations and reminders card** — three lines:

| Line | Reads |
|---|---|
| **Invitations** | *"5 of 6 delivered — 1 not delivered"* · *"2 wait for the approval"* · *"Nobody invited yet"* |
| **Event reminder** | *"Off — turn on Send reminders in Edit"* · *"Sent"* and when · *"Goes on Fri 9 Oct 2026, 3 days before the event, automatically"* · *"Due since … — it has reached nobody yet. The hourly sweep tries again."* · *"Not sent"* when the sweep would not send it (over, postponed, begun, or awaiting approval) |
| **RSVP chase** | *"No RSVP deadline"* · *"Chased"* and when · *"Goes on …, automatically, to everybody who has not answered"* · *"Due since …"* · *"Not sent"* |

Then, as they apply: the amber line *"No mail server is set up, so no email goes. Employees with a login are told in the
app; guests from outside, and staff without a login, are not reached."*; *"Nobody is invited while the event awaits
approval: its invitations go out when it is approved."*; and the buttons **Send the undelivered invitations (N)**,
**Send reminder now** and **Chase unanswered now** — each shown only when the sweep itself would send it (lane 2e-1,
F-33). The footnote: an invitation, reminder or chase counts as sent once it reaches somebody; each is sent once, and
sending it here counts as that send; a move lets them go again; every invitation carries a calendar entry, and a move,
a new venue or link, a postponement, a cancellation or being taken off the list sends guests the update.

**Notes card** — the description, the **Outcome**, and the notes, when there are any.

**Tabs, each with its count:** **Participants** · **Attendance** · **Tasks** · **Attachments** · and **Workflow** on an
event that needs approval.

**Participants** — *Participant* · *Kind* (*Employee*, or the outside guest's organisation) · *Role* · *Required* ·
*Invitation* (*Sent*, *Accepted*, *Declined*, *Tentative*, *Waits for approval*, or *Not delivered*) · *Responded*.
- **Add participant**: an **Employee**, *or* someone from outside — **External name**, **External email** *(required:
  "the invitation goes there")*, **External organisation** — then **Role**, **Attendance required**, **Special
  requirements**, and on a series **Which dates**. Refused: nobody twice, a leaver, an outside guest without an
  address, a cancelled or completed event.
- Each row: **Edit** (an outside guest's name, address and organisation, the role, required, needs — an employee
  stays who they are: *"To invite someone else instead, remove this guest and invite them."*; a changed address is
  sent the invitation), **Remove** (they are told), **Record accepted / declined / tentative** (the desk records an
  answer that came back another way — no notice), and on a series **Answer for several dates…** and **Take off
  several dates…**.
- A cancelled or completed event's list is its record: nothing can be added, changed, removed or answered, and its rows
  have no ⋯ (fixed 2026-10-06 — each opened an empty menu, **F-67**).

**Attendance** — the register, taken **once the event has started** and never on a cancelled one (before then:
*"Attendance is marked once the event has started."*). *Employee · Attended (Present / Absent) · Checked in · Checked
out · Reason · Marked by*. **Add**: **Employee**, **Attended**, **Check-in time** (*"On one of the event's days and not
still to come. Blank keeps the time already recorded, or now."*) or **Reason for absence**, **Notes**. Each row:
**Check out** (once, after the check-in), **Edit** (marks again — the check-in is kept unless a new one is given), and
**Remove** — a wrong row is corrected on the desk's tier (C-21).

**Tasks** — *Task · Stage · Assigned to · Due · Priority · Status*, with an **Overdue** badge worked out from the due
date and *"assignee chased 06/10/2026"* once the sweep has chased it. **Add task**: **Task**, **Stage**, **Priority**,
**Assigned to**, **Due date**; the edit adds **Status** (Overdue is not one you can set). Each row: **Mark complete**,
**Edit**, and **Remove** for `HR.Company.Admin` only. Whoever a task is given to, or passed to, is told.

**Attachments** — for the desk, on an event not cancelled, an **Add a file** box: **What it is** (Agenda · Minutes ·
Presentation · Handout · Resource), **About it (optional)**, and **File** (*"Agendas, minutes, presentations and
handouts. Each file is scanned before it is stored."*, up to 25 MB). Below, *File · Type · About it · Size · Added*,
each row with **Download** and **Remove**. A row from before lane 2h that only named a path reads *"Reference only — no
file stored"*, with no download.

**Workflow** — the engine's record of the approval: the route, who it is with, and who decided what and when.

### ▶ Walk it

Twelve minutes, in four parts: the finished retreat, the approval beat (§ 2.5, Beat A) on the event chapter 5 made,
the board meeting's guests and tasks, and Safety's drill.

**Part 1 — a finished event, read only**

**1 — Open the *Management retreat — 2026 budget preparation*.**

> *"The management retreat — two days at the Volta Serene in Ho, to settle the 2026 operating budget and the
> manpower plan. It is the only event here that has already happened, which makes it the only one with a full
> record."*

**2 — Read the Overview across.**

> *"Completed. A meeting for management — 'For: Management, unit heads and line managers.' Sixty-two thousand cedis
> against MD/EVT/2026/01. Eighteen expected, four attended — and that gap is the reason both numbers are kept. In a
> paper system you get the invitation list or the register, never both."*

**3 — Point at the *Invitations and reminders* card.** On a finished event its reminder line reads *Not sent*, and
no button is offered — the card follows the sweep's own rule, and the sweep does not remind about the past.

**4 — Read the *Outcome* aloud** from the Notes card. It is the single best line on either database:

> *"'Operating budget agreed at GHS 214 million with a 6% contingency; manpower plan referred back to HR for costing;
> Community 25 phasing endorsed.' That is what closing an event off means here. Not archiving it — writing down what
> it decided."*

**5 — Open *Attendance*.**

> *"Four present, with the times they signed in. One absent, with a reason: 'On annual leave; represented by the
> Deputy Internal Auditor.' That is what a minute secretary writes — on the record rather than in somebody's
> notebook."*

**6 — Open *Attachments*, and press *Download* on the communiqué.** A PDF opens.

> *"And the papers are the papers — uploaded, scanned for viruses, filed in the document store, and downloaded by
> anybody who may read the event."*

**Part 2 — the approval beat (Beat A, § 2.5)**

**7 — Window A: open the event chapter 5 made.** Point at the header: no **Approve** for `hr.head`.

> *"It needs approval, and I made it — so I am the one person who cannot approve it. The engine has asked the HR
> desk, and it is waiting."*

**8 — 🔴 LIVE WRITE 2 — *Participants* → *Add participant*:** the `staff` persona (UAT: Efua Seidu, `TDC/00017`),
Role **Attendee**. The toast: *"Added. The invitation goes when the event is approved."* Their *Invitation* reads
**Waits for approval**, and the *Invitations and reminders* card *"1 waits for the approval"*.

**9 — Window C (`staff`): the bell is quiet, and the portal calendar does not show the event.**

> *"Nothing has reached them, and nothing should: this meeting may not happen."*

**10 — 🔴 LIVE WRITE 3 — Window B (`hr.officer`): open the same event and press *Approve*,** with a comment. The
status becomes *Confirmed*, *Approved by* appears on the Overview, and the Workflow tab records the decision.

> *"Approved — by somebody who did not create it, on the same engine that approves leave, travel and requisitions,
> with who decided and when on the record. And in the same moment, the invitation it was holding has gone."*

**11 — Window C: the bell has the invitation, and the portal calendar draws the event dashed** — waiting for an
answer. Window A: `hr.head`'s bell says it was approved. Back on the event, the guest reads **Sent** and the card
*"1 of 1 delivered"*. Chapter 6a answers it.

**12 — Show *Reschedule* without pressing it.** Open the dialog, read its sentence aloud, then **Cancel**.

> *"Move it, with a reason, and the record keeps where it was — 'Originally' — when it moved and why. Everybody
> invited is told, accepted answers are asked again, its rooms move with it, and because it was approved, it goes
> back for approval."*

**Part 3 — the board meeting**

**13 — Open the *Board of Directors — Q3 meeting* → *Participants*.** Five employees and **one from outside**:
*Nana Kwame Baffoe, Board Secretariat*, a Facilitator — the *Kind* column shows the organisation.

> *"An event's guest list is not the staff list. Somebody from outside is invited at their own address, gets the
> calendar entry, and answers from their own mail — to the organiser."*

**14 — 🔴 LIVE WRITE 4 — on the outside guest's row, ⋯ → *Record accepted*.**

> *"Which is how their answer gets here: HR records it. It says 'record', not 'respond', because the person
> answering is not the person pressing the button."*

**15 — Open *Tasks*.** Two rows: the board pack, **Critical**, assigned to the Managing Director — on UAT already
**Overdue**, with *"assignee chased"* and the date (5 October) — and the minutes, assigned to the Head of HR.

> *"Overdue is not a box somebody ticks; it is the due date, read every time. And the hourly sweep chased the
> Managing Director once, the day after it fell due."* (**Rebuilt:** the board pack falls due five days after the
> build, and is chased the day after that.)

**16 — 🔴 LIVE WRITE 5 — *Add task*:** *"Book the video link to the Ho office"*, Stage **Preparation**, Priority
**High**, assigned to `hr.officer`, due next week. Window B's bell: *"A task for Board of Directors — Q3 meeting"*.
**🔴 LIVE WRITE 6 — ⋯ → *Mark complete*.**

**17 — *(optional)* 🔴 LIVE WRITE 7 — *Attachments* → *Add a file*:** What it is **Presentation**, a small PDF.
⚠ **CAREFUL:** the upload gate scans every file and refuses one it cannot scan — the scanner
(`scripts/Start-DemoVirusScanner.ps1`) must be running on the demo machine. Without it, show *Download* instead.

**18 — *(optional)* 🔴 LIVE WRITE 8 — *Send reminder now*** (the board meeting has a guest list). The toast counts
whom it was for and whom it reached — *"N of M reached — 0 by email, N in the app. K not reached: no mail server is
set up, and they have no login to be told in the app."* Every guest with a login is reached through the bell; the
outside guest, and any guest without a login, is not. ⚠ The cost: it counts as the automatic reminder, which will not
go again for this date.

**Part 4 — an event nobody in HR keyed in**

**19 — Open *Emergency drill: Fire Emergency Response Plan*** (13 December on UAT). The note under the header: *From
Emergency drill DRILL-2026-001*, linked.

> *"Safety recorded this drill's next date, and the company calendar has it, for everybody, without anybody in HR
> typing it twice. Move the date in Safety and it moves here."*

⚠ **Do not press *Announce on the intranet*** on UAT (Rule 1). On the seeded events it is not offered anyway: none has
*Show on intranet* on. **Delete** is not offered to `hr.head`; **Cancel** is the exit for anything made live.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/events/{id}/details` | `HR.Company.Read` |
| **Approve** / **Reject** | `POST …/events/{id}/approve` · `…/reject` — the engine names the approver; the service also refuses the organiser | `HR.Company.Write` (and the engine's say) |
| **Reschedule** · **Cancel** · **Complete** | `POST …/events/{id}/reschedule` · `…/cancel` · `…/complete` — a series by its scope | `HR.Company.Write` |
| **Delete** | `DELETE …/events/{id}` — its live bookings cancelled first | **`HR.Company.Admin`** |
| **Announce on the intranet** | `GET …/events/{id}/announcement` (the preview) · `POST …/events/{id}/announce` | `HR.Company.Write` |
| **Series** | `POST …/events/{eventId}/series/extend` | `HR.Company.Write` |
| **Rooms card** | `GET …/events/{eventId}/bookings` | `HR.Company.Read` |
| **Send reminder now** · **Chase unanswered now** · **Send the undelivered invitations** | `POST …/events/{eventId}/reminders` · `…/rsvp-reminders` · `…/invitations/send` — each answers whom it was for and whom it reached | `HR.Company.Write` |
| Participants | `GET`/`POST …/events/{eventId}/participants` · `PUT`/`DELETE …/participants/{id}` (a series by its scope) · `POST …/events/{eventId}/participants/respond` | Read / Write |
| Attendance | `GET`/`POST …/events/{eventId}/attendance` · `POST …/attendance/{id}/checkout` · `DELETE …/events/{eventId}/attendance/{id}` | Read / Write |
| Tasks | `GET`/`POST …/events/{eventId}/tasks` · `PUT …/tasks/{id}` · `POST …/tasks/{id}/complete` · `DELETE …/tasks/{id}` | Read / Write / **Admin** to delete |
| Attachments | `GET …/events/{eventId}/attachments` · `POST` multipart through the upload gate · `GET …/attachments/{id}/download` · `DELETE …/attachments/{id}` | Read / Write |

Tables: `CompanyEvents`, `EventParticipants`, `EventAttendances`, `EventTasks`, `EventAttachments`, and the engine's
instance for the approval.

**The guards are the server's** (lane 2a). An approval needs an event that requires one and is waiting; cancelling
or moving needs an open one; completing needs one that has started; a move asks for a reason, resets accepted and
tentative answers, moves the linked rooms (refusing, with the room named, if one is taken), and sends an approved
event back for approval. The screen hides what the server would refuse, so a refusal is rare — and when it comes, it
is a sentence.

**Who is told** — § 1.6. Each act names the event's own tenant, because the hourly sweep sends with nobody signed in;
no notice ever makes the act fail.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-1, R4-6.6** · removes offered to people the server refuses~~ | **Fixed** — Delete in round 4, the rest in lane 5a |
| ✅ ~~**C-2, R4-6.1** · the original dates kept but not shown~~ | **Fixed in lane 5a** — *Originally* and *Moved on* |
| ✅ ~~**C-3, C-20** · approval a flag, on any event~~ | **Fixed in lane 2b** — the engine, on events that need it |
| ✅ ~~**C-18** · attachments are references~~ | **Fixed in lane 2h** — files through the upload gate |
| ✅ ~~**C-19, R4-6.5** · no lifecycle guard~~ | **Fixed in lane 2a** |
| ✅ ~~**C-21, C-22** · attendance not removable, a guest not editable~~ | **Fixed in lane 2d** |
| ✅ ~~**C-23** · Overdue never set~~ | **Fixed in lane 2d** — worked out on read; chased once (lane 2e-3) |
| ✅ ~~**R4-6.2, R4-6.3** · the card promised sends, and counted attempts~~ | **Fixed in lane 2e** — the sweep's own conditions, and *reached* |
| ✅ ~~**R4-6.4** · no way for a guest to answer~~ | **Fixed in lane 7** — chapter 6a, and the calendar file |
| ✅ ~~**R4-6.7** · the organiser invisible to the diaries~~ | **Fixed in lane 2a** |
| **F-65** · an organiser on the HR desk who did not create the event is offered **Approve** by the engine | The engine knows the creator, not the organiser; the server refuses the organiser with the reason. Found while rewriting (lane 6) |

---

## 6a. `/me/calendar/events/[id]` — the event as staff see it

### 📍 Where you are

**From:** a guest's notice (*"You are invited …"* and every notice after it), or **Open** on an event in the portal's
calendar — or the HR menu's calendar, for anybody not on the HR desk · `/me/calendar/events/[id]` · window C as
**staff** · **3 minutes**

### 📖 What it is

> *"The event as an invitee sees it: what, when, where, how to join, who organises it, who it is for — and their own
> invitation, which they answer here. Nothing of the budget, and nothing of anybody else's answer."*

### 👁 On the page

**Header:** the event's name; *"EVT-2026-… · Meeting"*; a back-link to the calendar; and, for the HR desk only, **Open
the HR page**.

**Badges:** the status (or a red *Cancelled*), *Awaiting approval*, *You organise it*, *Date 3 of 10* on a series. A
cancelled event shows its reason.

**The event** card — *When* (*"12 October 2026, 09:00–16:00"*, or *"all day 13 November 2026"*) · *Where* (the site, the
venue, the address) · *Rooms* (the rooms booked for it) · *Joining* — on an online or hybrid event, **Join online**, or
*"The link is not set yet"*, and the meeting password **for a guest or the organiser only** · *Organiser* · *For* (who it
is for, in words) · *Reply by* (when replies are asked, with a deadline) · *About it*.

**Your invitation** card — where it stands (*"You have not answered yet"*, *"You accepted"*, *"You said you may
attend"*, *"You declined"*, *"Your invitation has not been sent yet"*), then, while you may answer, the same **Accept ·
May attend · Decline**, the note, and on a series **Which dates** as on the calendar's card (chapter 3a). Where you may
not, the reason. Your earlier note is shown under it. For an event that is for you but not an invitation: *"You are not
on its guest list — it is on your calendar because it is for you, and there is nothing to answer."*; for its organiser,
*"You organise this event."*

**Not yours to see** — an event you are neither invited to nor the audience of, or a private one: *"This event was not
found — or it is not one you are invited to or that is for you."* Exactly what a missing event says.

**When an invitee may answer** (lane 7, the user's ruling): once the invitation has gone (never while the event awaits
approval), until the RSVP deadline — or, with none, until the event begins — and never on a cancelled or completed
event. Each refusal is a sentence: *"The reply-by date for … has passed. Ask the organiser to record your answer."*,
*"… has already begun. Ask the organiser to record your answer."*, *"… is still awaiting approval, so its invitations
have not gone out yet."*

### ▶ Walk it

**1 — Window C (`staff`): open the bell's *"You are invited"* notice** from chapter 6, step 11 — or click the dashed
band on the portal calendar and press **Open**.

> *"This is what Efua sees — not HR's page. When, where, who organises it, who it is for. No budget, no catering
> order, no list of who else said what."* (UAT's `staff`; say the name your database shows.)

**2 — 🔴 LIVE WRITE 9 — under *Your invitation*, type a note** — *"I will bring the Q3 project figures."* — **and
press *Accept*.** The toast: *"Accepted — The organiser is told."* The card now reads *"You accepted"*, with the note.

**3 — Window A (`hr.head`): the bell** — *"… accepted: Heads of Unit — Q4 planning"*. On the event's page the guest
reads *Accepted* with today's date.

> *"The invitee answered for themselves, on their own page, and the organiser knew in the same second — in the app.
> Nobody in HR keyed it in."*

**4 — *(optional)* Window C: open the board meeting** by its address from window A. *"This event was not found …"*

> *"And a member of staff cannot read the board meeting at all — not because it is hidden on a menu, but because the
> server will not hand it over. It is for management."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/calendar/events/{eventId}` — for its organiser, a guest, its audience or the HR desk; anybody else a 404 | any internal login |
| An answer | `POST …/events/{eventId}/participants/{participantId}/reply` — the caller's own invitation or a 404; the window above; on a series every date the scope reaches that may still be answered; the organiser told in the app, never of their own answer | any internal login |

`CompanyCalendarService.GetEventAsync` builds the staff view; the HR desk's own answer door,
`participants/respond`, is unchanged and is not held to the reply window.

### ⚠ Known gaps

| Gap | |
|---|---|
| *By design* · the organiser is told of an answer **in the app only** | The user's ruling (lane 7): no email, no template |
| *By design* · an answer recorded at the desk (chapter 6, *Record accepted*) tells nobody | It is HR writing down an answer that came another way |

---

## 7. `/hr/company-schedule/events/[id]/edit` — amending an occasion

### 📍 Where you are

**From:** the event → **Edit** (offered while the event is open) · `/hr/company-schedule/events/[id]/edit` · as
**hr.head** · **2 minutes**

### 📖 What it is

> *"The same authoring form, with two halves swapped — what can only be set when an event is created is gone, and what
> only makes sense afterwards has appeared — and one thing it will not let you do quietly: move the event."*

### 👁 On the page

**Header:** *Edit \<event name\>* — *"Recurrence is set when the event is created and cannot be changed here."*,
back-link to the event.

The form is chapter 5's, with these differences:

| Field | Create | Edit |
|---|---|---|
| **Status** | absent — a new event is *Scheduled* | **present**: *Scheduled*, *In Progress*, *Postponed*; *Confirmed* only where no approval is needed or it has been given; and whatever it is now. *"Cancel, complete and reschedule have their own buttons on the event."* |
| **Repeats** and its fields | present | **absent** — a series is lengthened from the event page, and each date edited on its own |
| **Requires approval** | present | **absent** — once an event exists, approval is an action |
| **Actual cost** | absent | **present**, beside the budget |
| **Organiser** | *"You — or search for someone else"* | the organiser's name, to change |
| **Category** | the six for a new event | the same, plus *Holiday* or *Milestone* if an old event already is one |

**Moving it here is a reschedule.** Change a date, a time or the all-day switch and an amber box appears: *"Changing the
dates or times moves the event. Everybody invited is told why, accepted and tentative replies go back to awaiting an
answer, its room bookings move with it, the original dates are kept, and an approved event waits for approval
again."* — with **Reason for the change**, which the save requires.

**On a date of a series**, a card under the form: **Which dates** (*This date only · This and following dates · Every
date in the series*) — *"The other dates take only what you change here; new dates or times move each by the same
amount. A date that has started, been completed or been cancelled is left as it is, and each guest is told once."*

**Footer:** **Cancel** · **Save changes**. The toast says what the save did: *"Event updated"*, or *"Event moved —
Accepted replies are asked again, and its room bookings moved with it."*, with whom it reached (a postponement, or a new
venue, site or joining link, tells the guests too); on a series *"Dates moved"* or *"Dates updated"* with the dates it
reached.

### ▶ Walk it

**1 — From the board meeting, press *Edit*.**

**2 — Open the *Status* list.**

> *"Only what an edit may honestly set: scheduled, in progress, postponed — and confirmed only where no approval is
> involved. Cancelling, completing and moving have their own buttons, because each of them tells people things."*

**3 — Change the *Start time* by an hour** — and stop at the amber box.

> *"And if I move it here, it is a move, not a quiet edit: it asks for the reason, and everything the Reschedule button
> does happens — the guests told, the answers asked again, the room moved with it."*

Put the time back; the box goes.

**4 — Point at *Actual cost*** under the budget (turn *Has a budget* on if it is off).

> *"A budget is what you asked for; the actual cost is what it came to. You cannot know the second when you create
> the event, so the form does not ask."*

**5 — Press *Cancel*.** Nothing is written here.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Load | `GET api/CompanySchedule/events/{id}` | `HR.Company.Read` |
| Save | `PUT api/CompanySchedule/events/{id}` — on a series, with its scope | `HR.Company.Write` |

The update refuses a cancelled or completed event and a status it may not set. **A change of dates, times or the
all-day switch takes the reschedule path** (lane 2a, F-37, R4-7.1) — the reason is required, and the move does what a
reschedule does. A change of venue, site or joining link, or *Postponed*, tells the guests (lane 2e-1); the answer is a
re-read of what was saved (F-46).

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**R4-7.1, F-37** · Edit moved an event without anything a reschedule does~~ | **Fixed in lane 2a** — the edit takes the reschedule's path |
| ✅ ~~**C-24, F-9** · the status list accepted any transition~~ | **Fixed in lane 2a** — only what an edit may set |

---

## 8. `/hr/company-schedule/bookings` — who has which room

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Room Bookings** · `/hr/company-schedule/bookings` · as **hr.head** · **4
minutes**

### 📖 What it is

> *"Every room held, by whom, when and what for — whether the HR desk booked it or a member of staff did from the
> portal — and the approvals still outstanding. The screen a receptionist lives on."*

### 👁 On the page

**Header:** *Room bookings* — *"Who has which room, when, and which bookings are still waiting on approval."*, a
back-link, and two buttons: **Export CSV** and **Book a room**.

**The filters**, each on the server: **Status** (*All statuses* and the five), **From** / **To** (bookings that
overlap the dates), and a search, *"Number, room, purpose or booker…"*.

**Nine columns:** **Number** (`BK-2026-00001`) · **Room** · **From** · **To** (date and time, in the browser's own
time) · **Purpose** · **Seats** · **Booked by** · **Status** · ⋯. 25 to a page, *"N bookings · page 1 of 2"* under
the table. Each row opens the booking.

**The ⋯ menu** — the booking page's own rules (chapter 10). A row with nothing on offer has no ⋯ at all: for
`hr.head`, every *Completed*, *Cancelled* and *No show* booking (fixed 2026-10-06 — it used to open an empty menu).
*Not approve* and *Mark no-show* are on the booking page, which the row opens.

| Item | Shown when | Does |
|---|---|---|
| **Approve** | *Tentative*, and **not to its own booker** | as the booking page's Approve (chapter 10). The booker sees no Approve, as on the booking page (**F-66**, fixed 2026-10-06) |
| **Cancel** | the booking holds its room — *Tentative* or *Confirmed* | a dialog — *"\<room\> — the slot is released for someone else."* — with a required **Reason**; the booker is told |
| **Delete** | **`HR.Company.Admin` only** | *"Cancelling keeps the record and the reason. Deleting removes it entirely."* |

**Export CSV** — the bookings the filters find: *Number · Room · Purpose · Event · Booked by · Starts · Ends ·
Attendees · Status · Approved · Cancelled because*.

### ▶ Walk it

**1 — Open the register.**

- **Rebuilt:** two rows — the Management Committee's Boardroom morning, booked by `hr.head` at the desk, and the
  Community 25 design review in Conference Room A, booked by `head.dev` **from the portal** — plus your own clash
  booking from § 2.4.
- **UAT:** one row, the Management Committee's, *Completed* — plus your own from § 2.4.

> *"Every room held, and by whom. That one was booked by the Head of Development, from the staff portal — no HR
> officer involved — under exactly the same rules."* (Rebuilt; on UAT, say it of your own booking in chapter 10C.)

**2 — Point at the *Status* column.**

> *"Confirmed, because none of these rooms needs approval. Completed, once its time is over — the hourly pass closes
> it, and a facilities manager can mark one that was held and never used a no-show. Every status on this list is
> reached by something now."*

**3 — Narrow it:** **From** today, **To** a week on.

> *"Searched, filtered and paged on the server — and the same filters export to a spreadsheet."*

**4 — Press *Book a room*** and continue into chapter 9.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The table | `GET api/CompanySchedule/bookings/search?text&status&from&to&page&pageSize` | `HR.Company.Read` |
| **Export CSV** | `GET …/bookings/export` with the same filters | `HR.Company.Read` |
| **Approve** | `POST …/bookings/{id}/approve` — the engine names the approver; the booker is refused | `HR.Company.Write` |
| **Cancel** | `POST …/bookings/{id}/cancel` — with a reason; the booker told | `HR.Company.Write` |
| **Delete** | `DELETE …/bookings/{id}` | **`HR.Company.Admin`** |

Table: `RoomBookings`. Every booking read is scoped to the tenant inside its query (F-30), and a booking's times come
back marked as UTC, so a browser outside Ghana shows them right (F-50).

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-8** · *Completed* and *No show* unreachable~~ | **Fixed in lane 3b-2** — the sweep completes; the desk marks a no-show (chapter 10) |
| ✅ ~~**C-25** · unpaged, filtered in the browser~~ | **Fixed in lane 2g-1** |
| ✅ ~~**C-26** · no room view~~ | **Fixed in lane 7** — the calendar's room picker (chapter 3a), and the portal's day board (chapter 10C) |
| ✅ ~~**R4-2.3** · the second seeded booking never existed~~ | **Fixed in lane 6a** — `head.dev`'s, from the portal |
| ✅ ~~**F-66** · the row menu's **Approve** is offered to the booker~~ | **Fixed 2026-10-06**, with **F-67** |
| ✅ ~~**F-67** · the row menu opens empty on a completed, cancelled or no-show booking, and offers Cancel on a no-show~~ | **Fixed 2026-10-06** (the user's report) — no ⋯ where nothing applies; Cancel only while the booking holds its room |

---

## 9. `/hr/company-schedule/bookings/new` — the availability search

### 📍 Where you are

**From:** the register → **Book a room**; an event's Rooms card → **Book a room** (the event chosen, its hours and
name filled in); the calendar's room view → **Book this room** (the room and the day, 09:00–10:00) ·
`/hr/company-schedule/bookings/new` · as **hr.head** · **6 minutes** — **the best chapter in the module**

### 📖 What it is

> *"Booking a room the right way round: say when you need it and how many of you there are, and the system tells you
> what is free — free, big enough, and within the room's own rules. Not a list of every room and a disappointment at
> the end."*

### 👁 On the page

**Header:** *Book a room* — *"You are recorded as the person booking."*, back-link.

**A result card** appears at the top after booking a series (below).

**Card 1 — When and how many:** **From** and **To** *(date and time, required; the end after the start)* ·
**Expected attendees** *(required, at least one)* · **Find free rooms** — disabled until there is a window. Arriving
from the calendar, the search has already run; from an event, the window is filled in and waits for **Find free
rooms**.

**Card 2 — Which room:**

| State | Shows |
|---|---|
| before the search | *"Set the window first — Rooms are offered once there is a start and an end to check them against."* |
| searching | *"Checking what is free…"* |
| nothing free | *"Nothing free in that window — Try a different time, or a smaller number of attendees."* |
| rooms free | a **Room** list — *"Boardroom · Tema Head Office · seats 18"* — and the same rooms as badges with their seats |

**Only a room this booking may have is offered** (lane 3a, R4-9.1): free for the whole window, in use, open for
booking, seating the party, and within its own longest booking and days-ahead limit.

**Card 3 — What for:** **Purpose** *(required)* · **Linked event** — the events of the next 120 days, *"EVT-… — name"*,
or *"Not linked to an event"* · on a date of a series, **Which dates** (*"Book the room for its other dates too, each at
the same time relative to its own start; the dates the room cannot take are listed and the rest booked."*) ·
**Special requirements** · **Catering** · **Notes**.

**Footer:** **Cancel** · **Book room** (**Book the dates** for a series).
- One booking: the toast *"Room booked — BK-2026-… — Boardroom."*, adding *"It needs approval before it is
  confirmed."* where the room needs approval, and you land on the booking.
- A series: the result card — *"Booked for N dates"*, each `BK-…` with its window and status (*awaiting approval* where
  the room needs it, with *"… asks for all of them, and its decision covers the rest"*), then *Not booked* — each date
  the room could not take, with why — and the dates already begun, completed or cancelled, left alone.
- A refusal shows the server's sentence — *"Heads of Unit — Q4 planning runs …; a booking for it must fall on its
  days."*, *"Boardroom seats 18; this booking expects 40."*, *"Huddle Room 1 may be booked for at most 2 hour(s) at a
  time; this booking is 3. …"* — or, with none, *"The room may have been taken while you were filling this in."*

### ▶ Walk it

**1 — Press *Book a room*** from the register. Point at Card 2's empty state before touching anything.

> *"Notice what it will not do. There is no room list yet, because 'which room' has no answer until you have said
> when. Offering every room and letting the booking fail at the end is the same screen built backwards."*

**2 — Ask for the window you wrote down in § 2.4** — the Boardroom's busy morning — for **12** people, and press
**Find free rooms**.

> *"Conference Room A is offered; the Boardroom is not, because it is held then. And the Huddle Room is not, because it
> seats six. The system is not warning me — it is not offering them."*

**3 — Book the room for the event chapter 5 made.** Go back to that event (chapter 6), and on its **Rooms** card press
**Book a room**. The form arrives with the event chosen, its hours filled in and its name as the purpose. Set
**Expected attendees** to **12** and press **Find free rooms**.

> *"And this is where the two halves of the module meet: the room is held for the event, and the booking knows what it
> is for."*

**4 — Choose the Boardroom** and read its option aloud — *"Boardroom · Tema Head Office · seats 18"*.

**5 — 🔴 LIVE WRITE 10 — press *Book room*.**

- *With Beat B's switch on (§ 2.5):* *"Room booked — BK-2026-… — Boardroom. It needs approval before it is
  confirmed."*

  > *"It needs approval because the room does — the rule is on the room, not on the person. The engine has asked the
  > HR desk, and I booked it, so I cannot be the one to approve it."*

- *Without:* *"Room booked — BK-2026-… — Boardroom."* — confirmed at once.

You land on the booking (chapter 10). Back on the event, its **Rooms** card lists it. *Undo:* **Cancel**, with a
reason (chapter 19).

**6 — *(optional)* The room's own rule.** With the Huddle Room's *Longest booking* at 2 (§ 2.5), search a three-hour
window for **four** people. The Huddle Room is not offered.

> *"The search applies the room's own rules — not only whether it is free and big enough. You cannot pick a room the
> booking would be refused."*

**7 — *(optional, 🔴 LIVE WRITE 10b)* A room for every date of a series** — only if you made chapter 5's three-date
series. From its first date's Rooms card, press **Book for this and following dates**, choose a room, **Book the dates**.
The result card lists a booking per date, and any date the room could not take.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| **Find free rooms** | `GET api/CompanySchedule/rooms/available?startDateTime&endDateTime&minCapacity` — every room rule applied | `HR.Company.Read` |
| Linked-event list | `GET …/events/upcoming?daysAhead=120` (and the chosen event on its own) | `HR.Company.Read` |
| **Book room** | `POST …/bookings` | `HR.Company.Write` |
| **Book the dates** | `POST …/bookings/series` | `HR.Company.Write` |

**What a booking checks** (lane 3a), in one transaction under an application lock per room, so two people pressing
*Book* at the same moment cannot both have it (F-47): the room is this tenant's, in use and open for booking (*"… is
not in use, so it cannot be booked."*); the window has an end after its start; the room's own rules — longest booking,
days ahead, seats, counting the larger of the booking's attendees and its event's estimate; a linked event is this
tenant's, still to happen, and holds the booking on its days; and **no live booking overlaps** it:

```
    same room
      AND not cancelled, and Tentative or Confirmed
      AND existing.Start <  requested.End
      AND existing.End   >  requested.Start
```

— a booking ending at 12:00 and one starting at 12:00 do not clash. The refusal names the other booking: *"Boardroom is
already booked then: BK-2026-…, Wednesday 7 October 2026, 09:00–12:00."* A room needing approval makes the booking
*Tentative* and asks the engine; otherwise it is *Confirmed*.

**A series** (lane 3d-1, D-12): each date still to come is booked at the same distance from its own start, through the
same checks; on a room needing approval the first date carries the request and its decision covers the rest; the
booker is told once.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**R4-9.1, F-15** · the search offered rooms the booking would refuse~~ | **Fixed in lane 3a** |
| ✅ ~~**C-28** · the availability read not scoped to the tenant~~ | **Fixed in lane 3a** |
| ✅ ~~**C-29, F-38, F-39** · a linked event was a label~~ | **Fixed in lane 2a** — its rooms move, cancel and delete with it |
| ✅ ~~**F-7, F-47** · an inactive room bookable; two bookings at one instant~~ | **Fixed in lane 3a** |

---

## 10. `/hr/company-schedule/bookings/[id]` — one booking

### 📍 Where you are

**From:** any row of the bookings register, the event's Rooms card, the calendar's **Open**, or a notice ·
`/hr/company-schedule/bookings/[id]` · window A as **hr.head**, window B as **hr.officer** · **4 minutes**

### 📖 What it is

> *"One held room — approved or not, used or not — with everything about it editable in place while it holds the room,
> except the room itself."*

### 👁 On the page

**Header:** the **room** as the title, *"BK-2026-… · booked by …"*, back-link, and the actions:

| Button | Appears when | Does |
|---|---|---|
| **Approve** · **Not approve** | *Tentative* and open — **never to its booker**, who reads instead *"You booked this, so somebody else must approve it."* | Approve: *"Booking approved — The booker is told."* (or *"Approval recorded — Another approval stage is still to come."*). Not approve: a dialog — *"It is cancelled and the room released. \<booker\> is told, with your reason."* — with a required **Reason** |
| **Cancel** | it holds its room — *Tentative* or *Confirmed* | a dialog — *"The slot is released for someone else."* — with a required **Reason**; the booker is told, unless it is their own act |
| **Mark no-show** | *Confirmed* or *Completed*, and its start has passed | a confirmation — *"The room was held and not used. This cannot be undone, and \<booker\> is told."* |
| **Delete** | **`HR.Company.Admin` only** | *"BK-… will be removed from the register. Cancel it instead to keep it on record."* |

**Booking card:** Status · Booked on · From · To · Linked event · Approved by (name and when) · and, when cancelled,
*Cancelled* with the date and the reason (*"Not approved before it started."* for one the sweep lapsed).

**Details card** — **a form while the booking holds its room**, read-only once it is cancelled, completed or a no-show:
*"To move this to a different room, cancel it and book again — that is the only way the availability check runs against
the new room."* · **From** · **To** · **Purpose** · **Expected attendees** · **Special requirements** · **Catering** ·
**Notes** · **Save changes**. A save runs every booking check again (chapter 9), with this booking left out of the
clash; a new time on a room needing approval sends an approved booking back for approval.

### ▶ Walk it

**1 — You are on the booking chapter 9 made.** Read the Booking card.

> *"The number, when it was booked, the window, and the event it is held for."*

**2 — With Beat B's switch on, it is *Tentative*,** and the header says *"You booked this, so somebody else must
approve it."*

**3 — 🔴 LIVE WRITE 11 — Window B (`hr.officer`): open the same booking** (from the engine's notice in the bell, or the
register) **and press *Approve*.** *"Booking approved — The booker is told."*; *Approved by* fills. Window A's
bell: approved.

> *"Approved by somebody else — the engine asked the HR desk, and the person who booked it could not decide it. The
> booker is told, in the app and by email where there is a mail server."*

**4 — The clash, for real.** Open the Boardroom booking you made for § 2.4 (the morning after the demo). In *Details*,
set **From** and **To** to the event's day and hours — the window the booking you just approved holds — and press
**Save changes**. It is refused:

> *"Boardroom is already booked then: BK-2026-…, … 09:00–12:00."*

> *"Not a warning — a refusal, naming the booking in the way. It runs on every save and every booking, under a lock,
> so not even two people pressing Book at the same instant can both have the room."*

Nothing is written. Put the times back, or leave the page.

**5 — Read the grey note on *Details* aloud.**

> *"To move this to a different room, cancel it and book again. Which is a decision, not a limitation: there is exactly
> one way into a room, and it is the one with the availability search on it."*

**6 — *(UAT)* Open the Management Committee booking** (*BK-2026-00001*, *Completed*). **Mark no-show** is offered —
open it, read its sentence, and press **Cancel** in the dialog.

> *"Completed by the hourly pass once its time was over. And if a room was held and nobody came, the desk can say so —
> once, for good, and the booker is told."*

🚫 **Do not confirm it**: it cannot be undone.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The page | `GET api/CompanySchedule/bookings/{id}` | `HR.Company.Read` |
| **Save changes** | `PUT …/bookings/{id}` — every booking check again, under the room's lock | `HR.Company.Write` |
| **Approve** · **Not approve** | `POST …/bookings/{id}/approve` · `…/reject` — the engine names the approver; the booker is refused | `HR.Company.Write` |
| **Cancel** | `POST …/bookings/{id}/cancel` | `HR.Company.Write` |
| **Mark no-show** | `POST …/bookings/{id}/no-show` | `HR.Company.Write` |
| **Delete** | `DELETE …/bookings/{id}` | **`HR.Company.Admin`** |

The state guards are the server's (lane 3a, F-8): approving needs a *Tentative* booking; changing or cancelling needs
one that holds its room; a no-show needs a confirmed or completed one whose start has passed. Each refusal is a
sentence — *"BK-… is cancelled, so it can no longer be changed."*

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-30, C-31** · approve and cancel had no guard~~ | **Fixed in lane 3a** |
| ✅ ~~**C-32** · no Delete here~~ | **Fixed in lane 3a** — Admin only |
| ✅ ~~**C-8, F-48** · nothing completed, lapsed or marked a no-show~~ | **Fixed in lane 3b-2** |
| ✅ ~~**F-34** · the booker told nothing~~ | **Fixed in lane 3b-1** — every outcome, in the app and by email |

---

## 10A. `/hr/company-schedule/my-schedule` — one person's diary

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Company Schedule → **My Schedule** · `/hr/company-schedule/my-schedule` ·
as **hr.head** — or as anybody: it needs no HR permission · **3 minutes**

### 📖 What it is

> *"Everything the organisation has me down for, in one place: the meetings I am invited to or that are for me, the
> rooms I have booked, the interview panels I sit on, my training, my leave and my travel, and the days the office is
> shut. Before this, that was five screens in five modules, and none of them knew about the others."*

### 👁 On the page

**Header:** *My schedule* — *"Everything you are down for — meetings, rooms you have booked, interview panels you sit
on, training, leave and travel."*, back-link to the landing.

**The range card:** **From** and **To** *(today to thirteen days on, by default)*, **Next fortnight** and **This week**.
The page reads as soon as both dates are set; more than sixty days, or a range that runs backwards, is refused with
the server's own sentence.

**If a source could not be read**, a red banner says so: *"This schedule is incomplete — The leave could not be read
just now, so none of it is shown. A gap here is not free time — reload the page to try again."* (lane 5b, R4-10A.3)

**One card per day**, earliest first. **An entry over several days sits under every day it covers**, each saying the
run — *"· 14 Oct to 18 Oct"* — and one that began before the range starts on its first day (lane 5b, F-23). Each line:
an icon, the label, the time — or *"All day"* for anything recorded by the day — the reference, and a badge for its
kind.

| Kind | The label reads, for example | Comes from |
|---|---|---|
| Event | *Board of Directors — Q3 meeting (Scheduled, invitation Sent)* · `EVT-…`; or *Fire drill — Head Office (Scheduled, for all staff)* | events you are invited to (not declined), you **organise**, or are the **audience** of — an event shown on the company calendar, read through its scope and visibility (lane 2c); a several-day timed event on each of its days |
| Room booking | *Booked Boardroom (Confirmed)* · `BK-…` | rooms you booked |
| Interview | *Interview panel for Senior Procurement Officer* · `INT-…` | interview panels you sit on |
| Training | the course and the nomination's status | an approved nomination: every timed session of the course in the range, or the course's days |
| Leave | *Annual Leave (Approved)* · `LV…` | leave approved, pending or under way |
| Travel | *Travel (Approved)* · `TR-…` | travel approved, submitted or under way |
| Closure · Holiday | *Business closure: Year-end stocktake*, *Partial closure (a working day): …* · *Public holiday: Christmas Day*, *Day off in lieu of Boxing Day* | the closures that **cover you** (your site, your unit — lane 1a, R4-13.1), and the default holiday calendar's active days |

**Empty:** *"Nothing in this range — No meetings, bookings, panels, training, leave or travel between those dates."*

### ▶ Walk it

**1 — Open My Schedule** as `hr.head`, the next fortnight.

- **UAT (from 6 October):** the fire drill on 8 October and the board meeting on 12 October — each *invitation Sent* —
  and any interview panels or leave that fall in the fortnight.
- **Rebuilt:** the seeded events, the persona's interview panels and leave, moved with the build.

> *"This is my next fortnight, and I did not put any of it here. The interview panels come from recruitment, the leave
> from the leave module, the meetings from the company schedule. Several modules, one list — and nobody copied
> anything into a calendar."*

**2 — Point at an event's times and a day's *All day*.**

> *"And it knows the difference between 'away that day' and 'in a room from nine to eleven'. Leave is recorded by the
> day, so it says all day rather than inventing hours — and a week's leave sits under every day of the week."*

**3 — Window C (`staff`): the same page.** On UAT: the fire drill — *"(Scheduled, for all staff)"* — and their approved
leave on 14 October.

> *"Nobody gave this person a permission for it. It takes who they are from their sign-in. The fire drill is there not
> because they were invited, but because it is for all staff — so to an interview scheduler they look committed that
> morning. And there is no way to ask from here for somebody else's diary; that is the next screen."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The diary | `GET api/CompanySchedule/my-schedule?from&to` | **no HR permission** — any internal login linked to an employee; the employee is taken **from the token**, never the address |

`PersonalScheduleService` asks the seven `IPanelistCommitmentSource` implementations — the same classes the interview
clash check uses — about one employee over the range: interviews, leave, travel, meetings and events, room bookings,
training, and closures with public holidays. Each answer carries its kind, a hardness, the label, start and end,
whether it is recorded by the day, and the reference.

*Hard* means confirmed and exact to the minute — another interview, a confirmed booking, a settled meeting the person
accepted. *Soft* is worth knowing — leave, travel, a course, a closure, an event they are only the audience of. In a
diary, hardness refuses nothing; the team view shades by it. A source that fails is named in `incompleteSources`, and a
cancelled request is never counted as one.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**R4-10A.1, F-23** · a several-day entry on its first day only~~ | **Fixed in lane 5b** |
| ✅ ~~**R4-10A.2** · a timed several-day event on its first day only~~ | **Fixed in lane 2a** — here and in the clash check |
| ✅ ~~**R4-10A.3** · a failed source left the diary silently short~~ | **Fixed in lane 5b** — the banner |
| ✅ ~~**R4-10A.4, R4-13.1** · every closure and holiday was everybody's~~ | **Fixed in lane 1a** |
| ✅ ~~**R4-6.7** · the organiser missing~~ | **Fixed in lane 2a** |
| **R4-10A.5** · not walked in a browser | The plan's lane 5 walk, items 6–8 |

---

## 10B. `/hr/company-schedule/team` — a unit's diary, side by side

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Team Schedule** · `/hr/company-schedule/team` · as **hr.head**; optionally a
window as **head.dev** · **4 minutes**. The menu entry is open to everybody; the server decides which units each
person may read (lane 5b, the user's ruling).

### 📖 What it is

> *"What a unit and everybody under it are already committed to, side by side — so that when you pick a time for
> something, you can see who you are about to double-book. For HR, any unit. For a head of unit, their own."*

### 👁 On the page

**Header:** *Team schedule* — *"What a unit and everyone under it are already committed to — before you pick a time."*,
back-link, and — for the HR desk — **Schedule for this unit**, which opens the new-event form with *Department*, the
unit and the first day filled in (chapter 5).

**The picker card:** **Organisation unit** — the units **you may read**, each by its path (*"Board of Directors ›
Managing Director's Office › … › Development Department"*), with a line under it: *"Every unit — you are on the HR
desk."* or *"The units you head, and every unit beneath them."* A head opens on their own unit. **From** and **To**
*(today to six days on)*.

**Somebody who heads nothing, and is not on the HR desk,** gets one card instead: *"A team schedule shows other people's
leave and travel, so it is for the HR desk and for the head of a unit (who sees their unit and every unit beneath it).
You are not recorded as heading a unit."*, with a link to **My Schedule**.

**The grid**, in a card:
- **Sub-unit** — *"All of \<unit\> and beneath"*, or one sub-unit — and **Direct members only**; the title counts
  *"12 people"*, or *"4 of 12 people"*, with the key *"red = confirmed · amber = worth knowing"*.
- One row per **active** person in the unit and every unit beneath it, their own unit under their name when it is not
  the one chosen.
- One column per day; for the HR desk each day's heading opens the new-event form for that day.
- Each cell shows up to two entries, then *"+N more"*; hovering lists them all. A cell with anything hard is red,
  otherwise amber; an empty day is a dashed box. **An entry over several days is drawn on every day it covers.**

The incomplete-sources banner (chapter 10A) appears here too.

### ▶ Walk it

**1 — As `hr.head`, choose *HR / Administration Department*.** The list offers every unit — 451 on UAT.

- **UAT:** *12 people* across five units; every row has the fire drill on 8 October — the all-staff audience (§ 1.1,
  point 3) — and the managers the board meeting on the 12th.

> *"A head about to call a unit meeting. Everybody in the department and everything under it — and before I pick a
> time, I can see who is on leave, who is on a course, who is on an interview panel, and which mornings are already
> spoken for."*

**2 — Choose one sub-unit**, then switch **Direct members only** on: the title reads *"n of 12 people"*.

**3 — Point at a red cell and an amber one.**

> *"Red is fixed — an interview, a confirmed meeting they accepted. Amber is worth knowing — leave, a course, a closure,
> a company event they are the audience of. Leave is amber because it is recorded by the day: somebody on leave may
> come in for an hour, and the system does not overrule them."*

**4 — Press *Schedule for this unit*** — the new-event form opens with *Department*, the unit and the day in it. Press
**Cancel** there.

> *"And when the time is chosen, the event is made for this unit from here, with the clash check on the form."*

**5 — *(optional)* A window as `head.dev`.** The page opens on the *Development Department* — 18 people across six
units on UAT — and offers only that and the units beneath it; no *Schedule for this unit*.

> *"A head of unit sees their own, without being on the HR desk. Headship is data — who the organisation records as
> heading the unit — not a role somebody hands out."*

**6 — Window C (`staff`): open Team Schedule.** The card says who the page is for, and links My Schedule.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The unit list | `GET api/CompanySchedule/team-schedule/units` — every active unit for the HR desk; otherwise the units the caller heads and every unit beneath them | any internal login |
| The grid | `GET …/team-schedule/{organizationUnitId}?from&to` — the desk (`HR.Company.Write`, checked per request) any unit; the head of the unit, or of a unit above it, theirs; anybody else refused with the reason | any internal login |

`PersonalScheduleService` reads the unit tree once a request (451 units on UAT), takes the active employees in the
subtree, and asks the same seven sources about all of them at once; each member carries their own unit. The same
sixty-day limit applies. Who heads a unit is `OrganizationUnit.HeadEmployeeId` — on UAT 39 of 451 active units have
one recorded, so a unit with none is readable by the desk only.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**R4-10B.1** · it read, and did not schedule~~ | **Fixed in lane 5b** — *Schedule for this unit*, and each day's heading |
| ✅ ~~**R4-10B.2, F-23** · each entry on its start day only~~ | **Fixed in lane 5b** |
| ✅ ~~**R4-10B.3** · any Write holder any unit; a head not their own~~ | **Fixed in lane 5b** — the desk and the unit's head |
| ✅ ~~**R4-10B.4** · a directorate is hundreds of rows~~ | **Fixed in lane 5b** — the sub-unit filter and *Direct members only* |

---

## 10C. `/me/room-bookings` — staff book a room themselves

### 📍 Where you are

**Portal:** **Company → Room Bookings** · `/me/room-bookings` (+ `new`, `[id]`) · window C as **staff** · **4 minutes**.
Any member of staff with a login linked to their employee record (lane 3c, decision D-13).

### 📖 What it is

> *"A member of staff books a meeting room for themselves — the same rooms, the same rules, the same approval where a
> room needs one — and sees when the rooms are held without seeing anybody else's business."*

### 👁 On the page

**The list** (`/me/room-bookings`) — *Room bookings — "Book a meeting room, and keep track of your bookings."*, a
back-link to the portal, and **Book a room**.
- **When the rooms are held** — the **day board**: one row per bookable room, its held times as blocks across 07:00 to
  19:00 (wider when something falls outside). **Your own bookings are coloured and open their page; anybody else's is a
  grey block — no purpose, no booker, no number.** **‹**, a date, **›** and **Today**.
- **Your bookings** — **Upcoming (N)** and **Past and cancelled (N)**: *Number* (opens it) · *Room* · *When* · *Purpose*
  · *Status*.

**Book a room** (`/me/room-bookings/new`) — *"You are recorded as the person booking. A room that needs approval waits
until the HR desk approves it."*
- **When and how many** — **From**, **To**, **Expected attendees**, **Find free rooms**.
- **When the rooms are held — \<day\>** — the day board again, with the window you are asking for drawn over each room:
  green where the room is free then, red where it is held.
- **Which room** — the rooms this booking may have (the desk's search, every rule applied): *"Boardroom · Tema Head
  Office · seats 18 · needs approval"*. Choosing one shows its card — where it is, the seats, its facilities, *Needs
  approval*, and *"At most 2 hour(s) at a time, up to 30 day(s) ahead."* where it has limits. Nothing free: *"Nothing
  free then — Try another time or fewer people — or a shorter booking, or one nearer today, as some rooms limit both."*
- **What for** — **Purpose**, **Special requirements**, **Catering**, **Notes**. **No linked event**: a room for a
  company event is the HR desk's (the user's ruling).
- **Book room**: *"Room booked — BK-… — \<room\>."*, adding *"It waits for approval; you will be told either way."* on a
  room needing approval.

**One booking** (`/me/room-bookings/[id]`) — the room as the title, *"BK-… · \<purpose\>"*, and **Cancel booking**
while it holds its room. A waiting booking says *"This booking waits for the HR desk to approve it. You will be told
either way; one still waiting when its time comes is cancelled."* The *Booking* card: Status · From · To · Booked on ·
Approved by · For the event (one the desk linked) · Cancelled. **Change it** — the times, purpose, attendees and the
rest (*"To use another room, cancel this booking and book again."*, and on a room needing approval *"a new time waits
for approval again"*), with **The room that day** board. **Cancel booking** asks *"Why"*; on a waiting booking, *"Its
approval is withdrawn."* Somebody else's booking: *"That booking could not be found among yours."*

**Two rules are the portal's alone, fixed in code:** a booking from here is never linked to an event, and its start may
not be more than fifteen minutes past — *"… has passed — a booking made here starts from now on. The HR desk can record
one after the fact."* An unchanged start on a booking already under way is allowed, so it can be extended.

### ▶ Walk it

**1 — Window C (`staff`): *Company → Room Bookings*.** Move the day board to tomorrow.

> *"When are the rooms held? That grey block is somebody's booking. Not whose, not what for — just that the room is
> taken then. Mine would be in colour."*

On UAT the board is empty until a booking exists; use tomorrow, after § 2.4's booking, so the Boardroom shows grey.

**2 — 🔴 LIVE WRITE 12 — *Book a room*:** tomorrow, 14:00 to 15:00, **4** people. **Find free rooms**; the board draws
the window green over each free room. Choose the **Huddle Room 1**, purpose *"Project team catch-up"*, **Book room**.

- A room with no approval: *"Room booked — BK-2026-… — Huddle Room 1."* — confirmed.
- *With Beat B's switch on, choose the Boardroom instead:* *"It waits for approval; you will be told either way."* Then
  **🔴 LIVE WRITE 12b — window B (`hr.officer`)** approves it on the desk's booking page (chapter 10), and window C's
  bell says so.

> *"A member of staff booked a room — no HR officer involved — under exactly the same rules: the room's seats, its
> limits, the clash check, and an approval where the room needs one."*

**3 — Window A (`hr.head`): the bookings register** lists it, booked by `staff`, in full.

**4 — 🔴 LIVE WRITE 13 — window C: open it, *Cancel booking*, with a reason.** *"Booking cancelled — The room is free
for someone else."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The rooms | `GET api/CompanySchedule/me/rooms` — the rooms in use and open for booking, with their rules | any internal login linked to an employee |
| The day board | `GET …/me/rooms/busy?from&to` — every booking's room and time, and whether it is yours; at most 31 days a read | the same |
| **Find free rooms** | `GET …/me/rooms/available?startDateTime&endDateTime&minCapacity` | the same |
| Your bookings | `GET …/me/room-bookings` · `GET …/me/room-bookings/{id}` — your own, or a 404 | the same |
| **Book room** · **Change it** · **Cancel booking** | `POST …/me/room-bookings` · `PUT …/me/room-bookings/{id}` · `POST …/me/room-bookings/{id}/cancel` | the same |

`CompanyScheduleMeController` takes the booker from the token and has none of the desk's acts — no approve, no
no-show, no delete, no event link — by construction. The bookings go through the desk's own service: the same rules,
the same lock, the same approval on the engine, and the same notices to the booker (§ 1.6).

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**D-13** · staff could not book a room~~ | **Built in lane 3c** |
| *By design* · staff never link a booking to an event | The user's ruling: a room for a company event is the HR desk's |

---

## 11. `/administration/hr/company-schedule` — the setup hub

### 📍 Where you are

**Sidebar:** Administration → HR → **Company Schedule** ·
`/administration/hr/company-schedule` · as **hr.head** · **1 minute**

### 📖 What it is

> *"What the corporation decides once and then lives with: which rooms exist, which days it is shut, which dates it
> marks — and, read from Finance, what its financial year is."*

### 👁 On the page

A header (*Company Schedule* — *"Meeting rooms, closures, milestones and fiscal years."*, back-link to the HR
Administration hub) above **four navigation cards**:

| Card | Route | Chapter |
|---|---|---|
| **Meeting Rooms** — *Rooms, their facilities, and the rules for booking them.* | `…/rooms` | 12 |
| **Business Closures** — *Days the organisation is shut, company-wide or per site.* | `…/closures` | 13 |
| **Milestones** — *Anniversaries, achievements and other calendar dates.* | `…/milestones` | 14 |
| **Fiscal Calendar** — *Finance's fiscal years and periods, read-only.* | `…/fiscal-calendar` | 15 |

### ▶ Walk it

**1 — Open the hub.** Read the four titles.

> *"An HR officer touches an event every week and a room's rules once a year. Same permission, very different rhythm
> — so the daily work is under Human Resources and the standing decisions are under Administration. And the fourth
> card is not HR's at all: it is Finance's calendar, which HR reads rather than keeping a copy."*

**2 — Click *Meeting Rooms*.**

### ⚙ Behind the page

No API call — the cards are written into the page, mirroring the group in `frontend/src/config/hr-setup-nav.ts`. Each
target enforces its own permission.

---

## 12. `/administration/hr/company-schedule/rooms` — the rooms

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Meeting Rooms** ·
`…/rooms` (+ `/new`, `/[id]/edit`) · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Every room somebody can book, with what is in it and the rules for booking it. This is where
> the availability search gets its answers from."*

### 👁 Screen 1 — the register

**Header:** *Meeting rooms* — *"The rooms people can book, and the rules for booking them."*, back-link, **+ New room**,
and a **Search rooms…** box (room name, code, site and placement). Sorted by name.

**Ten columns:** **Code** (`BRD`) · **Room** · **Site** · **Where** (*"Main · 4 · …"* — building, floor, placement) ·
**Seats** · **Type** · **Facilities** (badges: *Projector · Whiteboard · VC · Audio · A/C*) · **Bookable** (**Yes** ·
**With approval** · **No**) · **Status** (Active / Inactive) · ⋯ — **Edit**, **Deactivate** (an active room), and
**Delete** for `HR.Company.Admin` only.

**Retiring a room** (lane 3a, decision D-18) opens one dialog, which first reads the room's bookings:
- **Deactivate** — *"Nobody will be able to book it."* With bookings still to come it lists them (number, when, booker,
  purpose) and says they *"will be cancelled — the reason, that the room was taken out of use — and their bookers
  told"*; the button reads **Cancel N bookings and deactivate**, beside **Keep it in use**. Switching **Active** off on
  the edit page goes through the same dialog.
- **Delete** — only for a room with **no booking on record**: *"It has no bookings on record, so nothing is lost by
  deleting it."* A room with history: *"\<room\> cannot be deleted — It has N bookings on record, and the bookings
  register would lose them. Deactivate it instead: nobody can book it, and its history stays."* with **Deactivate
  instead**.
- A refusal for want of the permission says so: *"You do not have permission to delete rooms — it needs Company
  Admin."*

**New / edit** (`…/rooms/new` — *"Rooms belong to a site, and only bookable rooms appear when someone books."* — and
`…/rooms/[id]/edit`), three cards:
- **The room** — **Room name** *(required)* · **Room code** (*"Generated if left blank"*, as `RM-0001`) · **Site**
  *(required — the sites only, each with its level: "Tema Head Office · Site / Office")* · **Room type** *(required)* ·
  **Where in the site** *(required — "e.g. East wing, past reception")* · **Building** · **Floor** · **Seats**
  *(required)* · **Description**.
- **Facilities** — **Projector** · **Whiteboard** · **Video conferencing** · **Audio system** · **Air conditioning** ·
  **Anything else**.
- **Booking rules** — **Active** · **Can be booked**; when bookable, **Bookings need approval** (*"A booking stays
  Tentative until somebody approves it."*), **Longest booking (hours)** and **Book up to (days ahead)**, both *"No
  limit"* when blank.

A code — typed or generated — is checked against every room the tenant has had, deleted ones included, and a typed
code that is taken is refused in words (R4-12.1). The edit checks the site and the code as the create does (F-6), and
answers with what it saved (F-46).

### ▶ Walk it

**1 — Open the register.** Three rows on both databases.

> *"Three rooms, all at head office. The Boardroom on the fourth floor, eighteen seats, with video conferencing and
> audio. Conference Room A on the second, thirty seats. Huddle Room 1 on the third, six seats — for the meetings that
> do not need a table."*

**2 — ⋯ → *Edit* on the Boardroom.** Stop at **Site** and **Where in the site** — § 1.7's second sentence:

> *"Two fields that sound the same and are not. The site is the corporation's own list of places — only places where
> staff can be posted, so nobody files a boardroom under 'Ghana'. 'Where in the site' is what you would say to a
> visitor: east wing, past reception."*

**3 — 🔴 LIVE WRITE 14 — *Booking rules*: turn *Bookings need approval* on** and **Save changes** — Beat B (§ 2.5), if
you did not set it last night. The register's **Bookable** now reads **With approval**.

> *"Nothing else changed — same room, same seats. But the next booking of it waits, and the HR desk is asked on the
> approval engine. The rule lives on the room, not on the person — because 'the boardroom needs approval and the huddle
> room does not' is how a real organisation works."*

*Undo:* chapter 19 — turn it off again. **Optional:** set the Huddle Room's **Longest booking (hours)** to **2** for
chapter 9's room-rule beat; the search then leaves it out of any longer window.

**4 — ⋯ → *Deactivate* on the Boardroom, and read the dialog.** It lists the Boardroom's bookings still to come — your
§ 2.4 booking and chapter 9's.

> *"Taking a room out of use does not strand the people who booked it. It shows them to me, cancels them, and tells
> each booker why. And a room with any history cannot be deleted at all — only deactivated — so the bookings register
> never loses a booking because its room went."*

Press **Keep it in use**.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register · one room | `GET api/CompanySchedule/rooms` · `GET …/rooms/{id}` | `HR.Company.Read` |
| The retirement dialog | `GET …/rooms/{id}/retirement` — bookings still to come, bookings on record, whether it can be deleted | `HR.Company.Read` |
| Create · Update | `POST …/rooms` · `PUT …/rooms/{id}` (with `cancelFutureBookings` when retiring) | `HR.Company.Write` |
| Delete | `DELETE …/rooms/{id}` — refused once the room has any booking on record | **`HR.Company.Admin`** |
| Site picker | `GET api/Location`, `GET api/LocationLevel` | location read |

Table: `MeetingRooms`. Bookings cancelled by a retirement are cancelled one by one through the booking service, each
booker told once (several of theirs together in one notice, § 1.6).

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-33** · the delete toast blamed bookings for a 403~~ | **Fixed in lane 3a** |
| ✅ ~~**C-36, F-18, F-49** · a room retired with live bookings; its history hidden by a delete~~ | **Fixed in lane 3a** (D-18) |
| ✅ ~~**R4-12.1, F-6** · a typed code refused with a 500; the edit unchecked~~ | **Fixed in lane 3a** |
| ✅ ~~**C-35** · no view of a room's bookings~~ | **Fixed in lane 7** — the calendar's room view (chapter 3a) |
| ✅ ~~**C-16** · the site list was the whole tree~~ | **Fixed in lane 5a** |
| **F-27** · the room edit page opened cold may show no site | The guard is in HEAD; the browser re-test is the plan's lane 5 walk, item 1 |

---

## 13. `/administration/hr/company-schedule/closures` — the days the office is shut

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Business Closures** · `…/closures` · as **hr.head** · **6
minutes**

### 📖 What it is

> *"A holiday calendar says which days the country is off. This says which days **TDC** is off — the year-end stocktake,
> a site closed for rewiring, a unit away at a retreat — or running a reduced service. And it is not a note: a day off
> here is a day nobody's leave is charged for."*

### 👁 On the page

**Header:** *Business closures* — *"Days the company, a site or an organisation unit is closed, or runs reduced
operations."*, back-link.

**After a save or a removal, a result card** sits above the list until dismissed (lane 1d):
- *"“\<title\>” was added."* (or *updated*, or *"The closure was removed."*);
- the save's **warnings**, in amber — a public holiday on the same day, a scope that covers nobody;
- **the leave it re-counted** (Rule 1): *"Leave recounted (N) — balances and attendance updated, and each employee
  told:"*, a line each — *"LV… · name · Annual Leave, 14 Oct 2026 → 18 Oct 2026: 5 → 4 days"*; *"Left as charged (N) —
  in a finished leave year, whose unused days may already have been carried over. Adjust these by hand:"*; anything that
  could not be recounted, in red; or *"No approved leave changed with these days."*;
- for a closure that is a day off and not over, **Announce to the N staff it covers** — *"Nothing is sent until you
  confirm. Leave it until the dates are final."* — or *"No active staff are covered by this closure, so there is
  nobody to tell."*

**+ Add closure** above the table. Dialog hint: *"You are recorded as the person who entered it. Telling staff is a
separate step: announce it once it is final."*

**Seven columns:** **Title** · **Kind** (*Whole company* · *One site* · *One organisation unit* · *Reduced operations*)
· **When** (one day, or *29 Dec 2026 → 30 Dec 2026*, and *Every year* under a yearly one) · **Covers** (*Whole company*,
the site, or the unit and everything beneath it) · **Working day** (*No — a day off* / *Yes — reduced operations*) ·
**Paid** · **Recorded by**. Row ⋯: **Edit**, **Announce to staff…** (until it is over), and **Remove** for
`HR.Company.Admin` only.

**The dialog — the kind decides the scope** (lane 1, decision D-1):

| *What closes* | Its sentence | Then asks for |
|---|---|---|
| **Whole company** | *"Everybody is off. Not a working day, so leave over it is not charged."* | nothing more |
| **One site** | *"The staff based at the site are off. Not a working day for them."* | **Site** — *"Covers the staff based at exactly this site. Only the places staff are assigned to are offered."* |
| **One organisation unit** | *"The unit and every unit beneath it are off, wherever their staff sit. Not a working day for them."* | **Organisation unit** — *"Covers the unit and every unit beneath it, wherever their staff sit."* |
| **Reduced operations** | *"Open with reduced service, for the whole company, one site or one unit. Still a working day."* | **Reduced operations for** — *The whole company · One site · One organisation unit*, one only |

Then **First day** and **Last day** · **Recurs every year** (*"The same dates every year from the first — the year-end
stocktake is typed once."*; shorter than a year) · **Staff are paid** (*"Recorded for payroll, which decides what an
unpaid day is worth."*) · **A working day**, shown **locked** — it follows the kind: *"A closure is a day off for the
staff it covers: leave over it is not charged."* or *"Reduced operations are still a working day: leave over them is
charged."* · **Reason** · **Notes on how staff were told**.

**The server refuses**, each with a sentence (422): a scope the kind does not take, a missing site or unit, a last day
before the first, a site or unit not this tenant's, and **a second closure of the same scope on the same days**, naming
the one already there (C-39). A public holiday underneath, or a scope nobody is in, is a warning, not a refusal.

### ▶ Walk it

**1 — Open the screen.** One row: *Year-end stocktake* · Whole company · 29 Dec 2026 → 30 Dec 2026 · Whole company · No
— a day off · **Paid** — on both databases (on a rebuilt one, of the build's year).

> *"The year-end stocktake. Two days, the whole company, staff paid — and not a working day, which is the field that
> matters: it is the difference between 'you were off and it cost you nothing' and 'you were off and it came out of
> your leave'. Since this year that is not a promise on a form. Leave over those days is not charged, the five-day
> appeal window in a disciplinary case steps over them, and a trip on those days is not posted as a day on duty."*

**2 — Press *+ Add closure* and walk the *What closes* list**, reading each kind's sentence.

> *"The kind decides who is off. A site means the people posted there; a unit means the unit and everything under it,
> wherever they sit; reduced operations means open, but thinly — and still a working day."*

**3 — 🔴 LIVE WRITE 15 — save one.**

- **UAT — make it *Reduced operations*** (Rule 1): *"Head office — reduced counter service"*, **Reduced operations
  for** *One site* → **Tema Head Office**, one weekday next month, **Staff are paid** on. The result card: *"was added"*
  — no leave re-counted, because reduced operations are a working day, and no announce offer.
- **Rebuilt — make it a day off:** *"Ashaiman office — electrical rewiring"*, **One site** → **Ashaiman Market**, two
  days next month, *Reason* *"Main distribution board replacement; the office cannot be occupied."* The result card
  lists any approved leave of the Ashaiman staff on those days, re-counted — and offers **Announce to the N staff it
  covers**.

> *"And look what it did: the leave already approved over those days has been re-counted — the balances put right,
> attendance re-posted, and each person told. Nobody had to remember to do it."* (Rebuilt; on UAT, say it of the
> stocktake: *"had anybody's leave sat on those two days, it would have been given back the moment this was saved."*)

**4 — The announcement.** On a rebuilt database, **🔴 LIVE WRITE 15b — *Announce to the N staff it covers*:** the dialog
shows the words — *"Closure: …"*, who, when, whether paid — and **Announce to N staff** publishes it to the bell of
everyone it covers.

🚫 **On UAT, do not.** It cannot be unsent, and it reaches real staff (Rule 1). Open the dialog to read the words if
you like, and press **Not now**.

**5 — *Remove*** is not offered to `hr.head` — an administrator's. Removing a day off re-counts the leave again, the
other way.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/closures` | `HR.Company.Read` |
| Create · Update | `POST …/closures` · `PUT …/closures/{id}` — answering with the warnings and the leave re-count | `HR.Company.Write` |
| Remove | `DELETE …/closures/{id}` — answering with the re-count | **`HR.Company.Admin`** |
| **Announce** | `GET …/closures/{id}/announcement` (the preview) · `POST …/closures/{id}/announce` | `HR.Company.Write` |
| *Is this a closure date?* | `GET …/closures/is-closure-date?date&locationId&organizationUnitId` — no scope answers company-wide closures only | `HR.Company.Read` |
| Payroll's read | `GET …/closures/employee-days?employeeIds&from&to` — each employee's closure days, paid or not (D-15c) | `HR.Company.Read` |
| The one-time re-count | `POST …/closures/recharge-leave` — for closures that existed before lane 1 | **`HR.Company.Admin`** |

Table: `BusinessClosures`. A closure's scope is an audience rule — everybody, a location, or a unit with its subtree —
resolved by `IHrAudienceResolver` and read through `IHrClosureCalendar`, the same answer for leave, the diaries, the
calendar, the announcement and payroll's read. A whole-company day off joins the working-day calculator's days off
(leave, the discipline clocks, travel's on-duty posting); a site or unit day off is each covered person's own (leave).

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-5** · closures reached nothing~~ | **Fixed in lane 1** — leave, the discipline clocks, travel, the diaries, the calendar |
| ✅ ~~**D-1, F-24** · the kind decided nothing~~ | **Fixed in lane 1a** |
| ✅ ~~**R4-13.1, C-37, F-2** · every closure everybody's; `is-closure-date` over-reported~~ | **Fixed in lane 1a** |
| ✅ ~~**C-38** · no yearly closure~~ | **Fixed in lane 1a** |
| ✅ ~~**C-39** · overlapping closures accepted~~ | **Fixed in lane 1a** — refused; a holiday underneath warned |
| ✅ ~~**F-52** · leave approved before a closure kept its charge~~ | **Fixed in lane 1c** — and the same for public holidays (D-15b) |
| **A-92** · attendance has no working-day builder, so it reads no closure | HR's own open item, in the attendance guide's ledger |
| **Payroll** · an unpaid closure day produces no deduction | Payroll's, in the payroll hand-off (§ 3 item 4); HR's read is ready |

---

## 14. `/administration/hr/company-schedule/milestones` — the dates worth marking

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Milestones** · `…/milestones` · as **hr.head** · **4 minutes**

### 📖 What it is

> *"The corporation's own history, on the calendar — anniversaries, achievements, launches, targets met, certifications
> won — with the papers that prove them. The kind of thing an organisation regrets losing track of."*

### 👁 On the page

**Header:** *Company milestones* — *"Anniversaries, achievements and other dates worth marking on the company
calendar."*, back-link.

**+ Add milestone** above a table. Dialog hint: *"A date the organisation wants remembered."*

**Seven columns:** **Title** · **Category** · **Date** — its own, the founding for an anniversary · **Next** — the
coming occurrence, with the anniversary for a yearly one (*"2026-10-18 · 74th"*), or *Past* for a one-off already gone
· **Repeats** (*Every year* / *Once*) · **On calendar** (*Yes* / *Hidden*) · **Files** (a count). Each row
has **Files**, **Edit**, and **Remove** for `HR.Company.Admin` only.

**Dialog:** **Title** *(required)* · **Category** *(required)* · **Date** *(required)* · **Description** · **Why it
matters** · **References** (free text — the files themselves are the row's *Files*) · **Repeats every year** · **Show
on the calendar**.

**Files** — *"Files — \<title\>"*, *"What evidences the milestone: a certificate, a licence, a photograph. Each file is
scanned before it is stored."* For the HR desk: **About it (optional)** and **File** (up to 25 MB). Each file: its
name, then its note, size, date and who added it, **Download**, and **Remove** (the HR desk's tier). *"No files yet."*

**A yearly milestone falls on the same month and day every year from its date** — 29 February on the 28th in other
years (lane 4a, C-40). The landing's *Milestones ahead* and the calendar show its coming anniversary, counted.

### ▶ Walk it

**1 — Open the screen.** One row: *TDC 74th Anniversary* · Company Anniversary · Every year · Yes.

- **Rebuilt:** **Date** *1952-10-18*, **Next** *2026-10-18 · 74th* (in 2026).
- **UAT:** **Date** *2026-10-18*, **Next** *2026-10-18* with no count, because UAT's row is dated 2026 (§ 2.1).

> *"Founded in 1952 to develop the Tema township. Typed once, on the founding date, and the system counts the years —
> the seventy-fourth this October, the seventy-fifth next. And it is on the company calendar, for everybody."*
> (UAT: say it without the number.)

**2 — Press *Files*** on the row.

> *"And a milestone has its evidence: the founding instrument, a certificate, the photograph — real files, scanned and
> kept, downloadable by anybody who may read the calendar's setup."*

**3 — *(optional)* 🔴 LIVE WRITE 16 — *Add milestone*:** *"ISO 9001:2015 certification"*, **Certification**, a date this
year, **Repeats every year** off, **Show on the calendar** on — then, in its **Files**, upload the certificate (a PDF).
⚠ **CAREFUL:** the upload needs the scanner running (chapter 6, step 17).

> *"A certification happens once; an anniversary every year. Both on the calendar, each with its papers."*

**4 — *Remove*** is not offered to `hr.head` — deleting a milestone, and its files with it, is an administrator's.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/CompanySchedule/milestones` — each with its next occurrence, years since and file count | `HR.Company.Read` |
| Create · Update | `POST …/milestones` · `PUT …/milestones/{id}` — a date required; the answer re-read | `HR.Company.Write` |
| Delete | `DELETE …/milestones/{id}` — its files go with it | **`HR.Company.Admin`** |
| Files | `GET …/milestones/{id}/documents` · `POST …/milestones/{id}/documents` (multipart, through the upload gate; the milestone checked before a byte is stored) · `GET …/milestones/documents/{id}/download` · `DELETE …/milestones/documents/{id}` | Read / **Write** to add and remove |
| Upcoming · range | `GET …/milestones/upcoming?daysAhead` · `…/range` — one row per occurrence, at most five years | `HR.Company.Read` |

Tables: `CompanyMilestones`, `CompanyMilestoneDocuments`. The yearly rule is `CompanyMilestoneRules`, the closures'
own.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-40, F-14** · a yearly milestone never came round again~~ | **Fixed in lane 4a** |
| ✅ ~~**C-9** · *Show on the calendar* had no calendar~~ | **Fixed in lane 7a** — and it decides now |
| ✅ ~~**F-25** · "Related documents" a text box~~ | **Fixed in lane 4a** — real files (D-3) |
| *Decided, not built* · **C-41** · a link to an employee's award or certificate | Decision D-17: a milestone is a company fact; its evidence is its files |
| *UAT data* · the anniversary is dated 2026, so it reads as its first year | Re-dating it to 1952 is a one-row edit that awaits a ruling (§ 2.1) |

---

## 15. `/administration/hr/company-schedule/fiscal-calendar` — Finance's fiscal calendar

### 📍 Where you are

**Sidebar:** Administration → HR → Company Schedule → **Fiscal Calendar** · `…/fiscal-calendar` · as **hr.head** · **2
minutes**

### 📖 What it is

> *"The financial years the corporation plans against — Finance's, read here. HR keeps no year of its own: a
> requisition's budget year and the accounts' year are the same year, because there is only one."*

### 👁 On the page

**Header:** *Fiscal calendar* — *"The company's fiscal years and periods, as Finance keeps them. Read-only here."*,
back-link, and **Set up and closed in Finance**, which opens Finance's own screen (`/finance/fiscal-years`) — for
whoever holds Finance's permission; HR's desk may be refused there.

**A line:** *"Requisitions and manpower budgets take their fiscal year from these years. A date in a year Finance has
not opened yet continues the sequence, as Finance will have to open it — next, **FY2027**, 1 Jan 2027 – 31 Dec 2027."*

**A card per year, newest first:** its name, its dates and how many periods; its own status (*Open*, *Closed*), *Locked*
where Finance locked it, and a badge for **each accounting book whose year-end is closed** or closing (*"\<book\>:
year-end closed"*) — Finance closes a year book by book and never marks the year itself closed (lane 4b, the user's
ruling). Under it, the periods: name, dates, status.

**With no Finance year at all:** *"Finance has defined no fiscal years yet, so requisitions and manpower budgets use the
year starting in **January** (HR policy settings) until it does."*

### ▶ Walk it

**1 — Open the page.** UAT: **Fiscal Year 2026**, *Open*, with its twelve months (*October 2026* … reading *Open* or
*Future*); **Fiscal Year 2025**, *Closed*; and the line naming **FY2027**.

> *"HR does not keep a fiscal year. It reads Finance's — one calendar for the corporation. A requisition raised today is
> checked against FY2026's budget because that is the year Finance has open; one dated next March is labelled FY2027,
> because that is the year Finance must open next."*

**2 — Point at *Set up and closed in Finance*.**

> *"And opening or closing a year is Finance's — so the button sends you there, rather than letting HR keep a second
> copy that could disagree."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The calendar | `GET api/CompanySchedule/fiscal-calendar` — Finance's years and periods, each year's status and lock, and each book's latest year-end close | `HR.Company.Read` |
| *The year for a date* | `GET …/fiscal-calendar/year?date=` — Finance's year, else Finance's sequence continued, else the policy month | `HR.Company.Read` |
| *The dates of a year* | `GET …/fiscal-calendar/period?year=` | `HR.Company.Read` |

`IHrFiscalCalendar` reads Finance's `FiscalYears`, `FiscalPeriods` and `YearEndBookCloseCycles` **in the server**, on
HR's own permission — Finance's routes need Finance's, which the HR desk does not hold. A date Finance has not covered
continues Finance's own rule for its next year: one higher, from the day after the last ends, twelve months each, and
the same backwards before the first. The answer says which it is (*Finance*, *Projected* or *Fallback*). The HR policy
setting *Fiscal year starts* answers only while Finance has no year at all, and the policy screen shows it read-only
meanwhile.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-42…C-48, F-3, F-13, R4-2.2** · HR's own fiscal years: read by nothing, overlapping, unclosed, unvalidated~~ | **Closed by retirement in lane 4b** (decision D-6) |
| ✅ ~~**F-56** · the manpower budget form counted its own year~~ | **Fixed in lane 4b** — Finance's year first |
| *Later* · HR's `FiscalYear` and `FiscalPeriod` tables | Retired and read by nothing; a later migration drops them |

---

## 16. *(retired)* HR's own fiscal year page

The pages that kept HR's own fiscal years and their periods — `…/company-schedule/fiscal-years` and
`…/fiscal-years/[id]` — were **removed in lane 4b** (decision D-6), with their sixteen API routes. HR reads Finance's
calendar instead (chapter 15). The number is kept so that references to "chapter 16" elsewhere still resolve.

---

## 17. `/administration/hr/settings/company-profile` — the letterhead and the seal

### 📍 Where you are

**Sidebar:** Administration → HR → **Settings** → **Company Profile** ·
`/administration/hr/settings/company-profile` · as **hr.head**, with **admin** in window D (§ 2.6) · **5 minutes**

### 📖 What it is

> *"A different menu, the same permission — and the reason this module's Admin tier exists. This is the corporation's
> legal identity: the statutory numbers, the registered address, the signatory — and the logo, seal and signature that
> every letter this system generates carries."*

> **Why it is in this book.** It is gated on `HR.Company.Read` / `Write` / `Admin`, it is built by the same demo
> scenario, and besides the HR policy settings it is the **one screen in the permission family where the Admin tier
> does something other than delete**. If you are demonstrating what an administrator is for, this is the screen.

### 👁 On the page

**Header:** *Company Profile* — *"The legal-employer details that appear on offer letters, confirmation letters and
outgoing email."*

**Four form cards, then the images card.**

- **Legal identity** — Legal name *(required)* · Trading name · Legal form · Registration number · Date of incorporation
  · Country of incorporation.
- **Statutory & tax** — Tax identification number (TIN) · VAT number · SSNIT employer number · Postal code · Other
  statutory registrations.
- **Registered address & contact** — Registered address · City · Region · Country · Digital address (GhanaPost GPS) ·
  Primary phone · Website · HR email · General email.
- **Documents & signature** — Default signatory name · Default signatory title · Offer acceptance instructions ·
  Document footer text. Then **Save changes** — *"Letters and emails will use these details."* (The old free-text
  *Logo URL* is gone, lane 4c, F-55.)

**Logo, seal & signature** — *"The images embedded in generated letters: PNG or JPEG, at most 2 MB each. Replacing one
retires the image it supersedes rather than overwriting it, so it stays possible to say which letters carry which
seal."* — and, for anybody without the permission, *"Replacing or withdrawing one needs the company administration
permission."*

Three rows — **Company logo**, **Company seal**, **Authorised signature** — each with:
- **In force** or **None**; the image's name, *"in force since"* and who uploaded it; with none, for the logo *"No logo
  is uploaded. Letters use the organisation's own logo from its tenant settings, if it has one, and otherwise render
  without a logo."*;
- for `HR.Company.Admin` **only** (lane 4c, C-49): **Upload** / **Replace**, which accepts a PNG or JPEG of at most 2 MB
  and says so before sending anything else (*"It must be a PNG or JPEG image."*, *"It must be at most 2 MB — every letter
  carries it."*), and **Withdraw**;
- **Previously used** underneath — each superseded image with its dates and the reason.

### ▶ Walk it

**1 — Open the page as `hr.head`.** Scroll the four cards without editing.

> *"This is TDC as a legal entity rather than as an employer. CS-1952-000118. The TIN, the VAT number, the SSNIT
> employer number. TDC House, Community 1, Tema — with the GhanaPost digital address, because that is how somebody
> actually finds it."*

**2 — Read the footer text aloud.**

> *"And this is not reference data in a drawer. Every letter this system generates — an offer, a confirmation at the
> end of probation, a service letter — is rendered from this record: the signatory, the instructions, and that footer,
> on every one."*

**3 — Scroll to *Logo, seal & signature*.** Both databases: the seal and the signature *In force*, the logo *None*.

> *"Three images every letter carries — the logo, the seal, the signature — and each one versioned: replacing one
> retires the old one, with its dates. Three years from now somebody disputes a letter, and you can say which seal was
> in force on that day and who put it there."*

**4 — Point at where the buttons are not.**

> *"And here is the one place in this area where an administrator is genuinely a different person from an HR officer. I
> can change the address, the signatory, the footer. I cannot change the seal — the screen does not even offer it.
> That is the administrator's, beside the policy thresholds and the deletes. Which is right: the seal is what makes a
> letter binding."*

**5 — *(optional)* 🔴 LIVE WRITE 17 — window D (`admin`):** press **Upload** on **Company logo** and choose a small PNG.
The row reads *In force*; the next letter carries it. Then **Withdraw** it if you like — the letters fall back to the
tenant's own logo — and it moves to *Previously used*.

> *"Same screen, a different person, and now it works. And nothing was overwritten: the history says what letters
> carried and when."*

*Undo:* nothing to undo — **Withdraw** leaves the history, which is the point.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The profile | `GET api/hr/company-profile` | `HR.Company.Read` |
| **Save changes** | `PUT api/hr/company-profile` — a `logoUrl` sent is ignored | `HR.Company.Write` |
| The images | `GET api/hr/company-profile/seal-assets` | `HR.Company.Read` |
| **Upload / Replace** | `POST api/hr/company-profile/seal-assets/{Logo\|Seal\|Signature}` (multipart) — PNG or JPEG by name, type and first bytes, at most 2 MB, refused before a byte is stored | **`HR.Company.Admin`** |
| **Withdraw** | `POST api/hr/company-profile/seal-assets/{kind}/retire` | **`HR.Company.Admin`** |

Tables: `CompanyProfiles`, `CompanySealAssets`. The images go through the controlled upload gate. Six kinds of letter
embed them as images inside the letter — offers, probation letters, interview papers, test papers, asset terms and HR
letter requests — the logo first from the uploaded one, then the tenant's own `LogoUrl`, else none.

### ⚠ Known gaps

| Gap | |
|---|---|
| ✅ ~~**C-49** · the seal buttons shown to people the server refuses~~ | **Fixed in lane 4c** |
| ✅ ~~**C-50, F-55** · the logo a free-text URL anybody with Write could set~~ | **Fixed in lane 4c** — an uploaded, versioned image |
| ✅ ~~*(new in 4c)* · any file accepted as a seal~~ | **Fixed in lane 4c** — PNG or JPEG, 2 MB |
| *Possible* · the asset-terms email may not show an embedded logo in some web mail | Recorded at lane 4c's ruling |

---

## 18. Where the company schedule shows up outside its own menu

Fourteen places. Four are worth a minute of the demo — the letters, the interview clash check, Safety's drill and the
email wording — and the rest answer questions. The arrows say which way the data goes.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Every generated HR letter** ← | Six kinds of letter carry chapter 17's **logo, seal and signature** as images: offers, probation letters, interview papers, test papers, asset terms and HR letter requests. The logo is the uploaded one, else the tenant's own, else none. The letter's text — the legal name, the address, the signatory, the footer — comes from the same company profile | **Yes, by reference** — say it on chapter 17; do not navigate |
| **Recruitment → Interviews** ← | The interview **clash check** reads each panelist's commitments from the same seven sources as the diaries and the calendar (§ 1.1, point 6). An event they are invited to **or organise**: a refusal when they have accepted — the organiser always has — a timed event that is firm (approved, or needing no approval), otherwise a warning. A room they booked: a refusal when it is confirmed. Leave, travel and training nominations: warnings. A closure that **covers them** — by its kind, site or unit — and the public holidays of the one calendar leave uses: warnings. An interview can also **hold a room booking**, one interview per booking. The recruitment guide's § 9.4 has the whole of it | **Yes, if recruitment came first** — *"the panel scheduler knows about the board meeting"* |
| **SHE → emergency drills** → | Safety's drill record **makes its own event here** (lane 2h, C-51): *Emergency drill: …*, all day on the drill's next date, for everybody. Moving or cancelling the drill in Safety moves or cancels the event, and the event page says where it came from | **Yes** — chapter 6, step 19 |
| **Administration → HR Settings → Letter & Email Templates** | `/administration/hr/settings/letter-templates` — the module's **seventeen** emails, under *Company Schedule*: **Invitation** (*Event Invitation*, *Event Series Invitation*, *RSVP Reminder*, *Event Guest Removed*), **Reminder** (*Event Reminder*), **Change** (*Event Rescheduled*, *Event Postponed*, *Event Details Changed*, *Event Cancelled*, *Event Series Changed*), **Approval** (*Event Approved*), **Task** (*Event Task Assigned*, *Event Task Overdue*) and **Booking** (*Room Booking Approved*, *Room Booking Cancelled*, *Room Booking No-Show*, *Room Bookings Changed*). Search *event* or *booking*, or open one directly with `?t=CompanySchedule/EventInvitation`. Reworded per tenant; **Reset** brings back the shipped wording | **Yes, on either demo database** — it is the only way to show an email that cannot be delivered there (Rule 3) |
| **Administration → HR Settings → HR policy** | `/administration/hr/settings/policy`. The *Company schedule reminders* card: *"Chase unanswered invitations this many days before the RSVP deadline"*, 2 by default, with a note that only live events are reminded and that the event page's two buttons count as the send. Under *Organisation-wide defaults*, *Fiscal year starts (when Finance has no calendar)* is greyed out while Finance has fiscal years, and names Finance's next one (Rule 7). **Saving the page is `HR.Company.Admin`**: `hr.head` reads it and cannot save it. The configuration register's § 2.8 has the chase lead proved in both positions | Only if asked |
| **The API's hourly host** | `CompanyScheduleReminderBackgroundService`: the first pass 23 minutes after the API starts, then hourly, for every tenant, under a 20-minute lease so that two running APIs never both send. It does Rule 6's five things. `POST api/CompanySchedule/reminders/run` runs the same pass now for the caller's tenant (`HR.Company.Write`); ⚠ no screen has a button for it (**F-59**) — the event page's two buttons do the same for one event | No — but know that an API left running sends what falls due |
| **Leave** → | A closure that is a day off is not charged as leave, for the people it covers. Saving, moving or removing one re-counts the leave already granted over it, re-posts attendance, and tells each person whose count changed (Rule 1) | **Yes, on a rebuilt database** — chapter 13 |
| **Discipline** → | A whole-company closure is not a working day for the statutory clocks: a five-working-day appeal window steps over it (FR-HR-180), and the window closes sooner again once the closure is removed. The review suite drives this on a case of its own (lane 6a) | Only if asked — say it from the stocktake |
| **Travel** → | A trip day on a whole-company closure is not posted to attendance as a day on duty | Only if asked |
| **Attendance** ✗ | ⚠ **Does not read closures**, nor public holidays: attendance has no working-day builder at all (attendance guide **A-92**, HR's own open item) | Say it honestly if asked |
| **Payroll** → | `GET api/CompanySchedule/closures/employee-days` (`HR.Company.Read`) gives each person's closure days with *Staff are paid*. Payroll does not yet act on an unpaid one (the payroll hand-off, § 3 item 4) | Only if asked |
| **Requisitions and manpower budgets** ← | Take their fiscal year from **Finance's** calendar, the one chapter 15 shows; a year Finance has not opened continues its sequence (Rule 7) | Only if asked |
| **HR → Announcements** | `/hr/announcements` — an announced closure or event is an announcement here, and HR can archive it. The notices it sent stay sent | Only after an *Announce* — so on a rebuilt database |
| **The workflow inbox and the bell** | `/workflow/inbox` lists an event or a booking waiting for approval and opens its page — **decide on the page**: a decision taken in the inbox does not reach the record, as for every HR approval (cross-module **#15**). The bell carries every notice in § 1.6, to everybody with a login | **Yes** — the bell is the demonstration on both databases (Rule 3) |

The employee portal is not on this list because it is this module's own: *Company → Calendar* (`/me/calendar`,
chapters 3a and 6a) and *Company → Room Bookings* (`/me/room-bookings`, chapter 10C).

---

## 19. Reset — putting the database back

Do this after the room empties, in this order. **One row needs SQL, and only sometimes**, because the module's own
*Cancel* is the reversible half of every destructive pair — the design point you spent the demo making. Cancel first:
row 1 takes most of the others with it.

**What cannot be put back:** a notice in somebody's bell, and an announcement's notices. They record what happened —
which is why Rule 1 keeps the *Announce* buttons for a rebuilt database.

| Live write | What you changed | Undo |
|---|---|---|
| **1** | *Heads of Unit — Q4 planning*, the event needing approval *(ch. 5)* | Its page → **Cancel**, reason `Demonstration`. It cancels the event's rooms (live writes 10 and 11) and tells its guests and the booker, in the bell. `hr.head` cannot delete it, and should not: a cancelled event keeps its record and its reason |
| **1b** | the three-date series *(ch. 5, optional)* | Its first date → **Cancel** → **Every date in the series**; its rooms (10b) go with it |
| **2**, **3** | `staff` invited; `hr.officer`'s approval *(ch. 6)* | Covered by row 1. There is no un-approve, and none is needed |
| **4** | the outside guest's answer, recorded on the board meeting *(ch. 6)* | Leave it. The desk records an answer and cannot clear one; the next rehearsal records a different one — *Record declined* — and makes the same point |
| **5**, **6** | the task given to `hr.officer`, then completed, on the board meeting *(ch. 6)* | Window D (`admin`): *Tasks* → **Remove** — removing a task is an administrator's. Or leave it: a completed task does no harm |
| **7** | the file on the board meeting *(ch. 6, optional)* | *Attachments* → **Remove** — the HR desk's since lane 5a |
| **8** | the board meeting's reminder, sent now *(ch. 6, optional)* | The notices stay. Its stamp stops the automatic reminder for that date, so if the next rehearsal comes before that reminder would have gone, clear the stamp: `UPDATE CompanyEvents SET ReminderSentDate = NULL WHERE EventName = N'Board of Directors — Q3 meeting' AND IsDeleted = 0` |
| **9** | `staff`'s answer and note *(ch. 6a)* | Covered by row 1 |
| **10**, **10b**, **11** | the Boardroom for chapter 5's event, the rooms for the series, and the booking's approval *(ch. 9–10)* | Covered by rows 1 and 1b. A booking can also be cancelled on its own page |
| **12**, **12b**, **13** | `staff`'s booking, and its approval *(ch. 10C)* | Live write 13 is the undo. If you skipped it, `staff` cancels it from *Company → Room Bookings* |
| **14** | the Boardroom set to need approval *(ch. 12, or the night before)* | ⋯ → **Edit** the Boardroom → turn *Bookings need approval* **off** → **Save changes**. ⚠ **Do this**, or the landing's *Bookings awaiting approval* is not 0 next time and § 2.3 is wrong. If you set the Huddle Room's **Longest booking (hours)** for the optional beat, clear it the same way, or every longer booking of it is refused |
| **15** | the closure *(ch. 13)* | Window D (`admin`): **Remove** — an administrator's. **UAT's** *Reduced operations* closure re-counts nothing either way. **A rebuilt database's** day off gives the leave back as it is removed — re-counted, and each person told again |
| **15b** | the closure's announcement *(ch. 13, rebuilt only)* | *HR → Announcements*: archive it. Its notices stay sent |
| **16** | the milestone and its certificate *(ch. 14, optional)* | Window D (`admin`): **Remove** — its files go with it |
| **17** | the logo *(ch. 17, optional)* | **Withdraw** it in window D, or leave it. The history keeps it either way, which is the point |
| — | the Boardroom booking for the morning after the demo *(§ 2.4)* | Its page → **Cancel**, reason `Demonstration` |

**Numbers never repeat.** `EVT-…`, `BK-…` and generated room codes come from one sequence that counts deleted rows,
unique per tenant, so a removed record keeps its number and the next one takes a new number. **Prefer cancelling to
deleting** for the record's sake: a cancelled record keeps its reason.

**The clean option — and a warning.** `scripts/New-UatDatabase.ps1` builds a demo database from nothing: the
migration chain, the seeders, then every scenario through the API, checked at the end. **It drops the database it is
pointed at, and it points at `ErpSystemDB_UAT` unless told otherwise.** UAT holds TDC's own staff list, which a
rebuild replaces with the demo pack's workforce. So **never rebuild UAT to reset this module** — every live write
above undoes in the UI. For a clean demonstration database, name a new one, on a port of its own:

```
powershell -File .\scripts\New-UatDatabase.ps1 -Database ErpSystemDB_DEMO -ApiPort 5010
```

It refuses the development database, starts and stops its own API on that port, and takes about an hour. Then point
the API you demonstrate with at the new database. Every seeded date moves with the build, so redo chapter 2's prep.

---

## 20. The short path — 20 minutes

When the slot shrinks. Seven screens in two windows — A (`hr.head`) and C (`staff`) — and the story still lands. Read
§ 2.1 first: it decides what each screen shows, and on UAT the closure stays read-only.

| # | Window · screen | Min | The one thing |
|---|---|---|---|
| 1 | A · `/hr/company-schedule` | 2 | the four cards — **"not a diary: the corporation's own calendar"** |
| 2 | A · `/hr/company-schedule/calendar`, beside C · `/me/calendar` | 3 | the same month to the HR desk and to a member of staff. **"Everybody sees their own calendar, and the server decides what that is"** (Rule 5) |
| 3 | A · the **Management retreat** *(ch. 6, Part 1)* | 4 | **expected 18, attended 4**; the outcome read aloud; the *Attendance* tab. **"An event is a small project, and closing it off means writing down what it decided"** |
| 4 | A · `/hr/company-schedule/bookings/new` *(ch. 9)* | 4 | § 2.4's busy window, **Find free rooms** — the Boardroom is not offered. **"A room in this system cannot be double-booked"** |
| 5 | C · `/me/room-bookings` *(ch. 10C, live writes 12–13)* | 3 | `staff` books a room themselves under the same rules, then cancels it. **"No HR officer involved"** |
| 6 | A · `/administration/hr/company-schedule/closures` *(ch. 13)* | 2 | the stocktake, read: **"a day off now — leave over it is not charged, and saving one gives back leave already granted"**. Save nothing on UAT (Rule 1) |
| 7 | A · `/administration/hr/settings/company-profile` *(ch. 17)* | 2 | the seal and its history — **"I cannot change the seal, and that is the point"** |

**With five minutes more, add the approval** — the strongest beat in the module, and the one that needs window B. Run
§ 2.5's Beat A after screen 3: chapter 5's event, `hr.officer` approves it in chapter 6, and the invitation reaches
`staff`'s bell in the same moment.

Cut, in this order if you must: the company profile *(say the sentence about letters instead)*, then the closure, then
staff booking, then the second window on screen 2.

**Do not put in the short path:** milestones, the fiscal calendar, the team schedule or a series — each needs more
explaining than the time it gets.

---

## 21. What is still open

> **The live state of every finding is kept in `HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md`.** Its § 5 gives each
> finding of this guide's earlier editions its lane, each lane's *State* says what was built and how it was proved,
> and its § 9 is the log. This chapter keeps no ledger of its own. It says where the findings went, and what is still
> open.

**Where the findings went.** The first edition (2026-09-17) found 51 (C-1…C-51), and round 4 (2026-09-24) found 26
more (R4-2.1…R4-13.1). The final closure's read and its same-day review added 58 (F-1…F-58). The user ruled that
nothing is deferred (decision D-9), and lanes 0–7 built them between 4 and 6 October 2026. Each was proved on UAT by
the review suite, in both positions: 1,152 assertions, two clean passes (lane 6a). Three groups were closed by a
decision instead of code:
- **C-41**, a milestone linked to an employee's award — by D-17: a company milestone is not one person's award, and
  its certificate is a milestone file (chapter 14);
- **C-42…C-48**, HR's own fiscal years — by D-6: retired for Finance's calendar (chapter 15);
- **D-02** in the HR finish plan, the invitee's own answer — by D-8: the reply door (chapter 6a).

One round 4 finding is about the data, not the code, and still holds: **R4-2.4**, no mail server on either demo
database (Rule 3).

### Open — the browser walks *(the user's)*

Every server rule is proved. The screens lanes 5 and 7 changed have not yet been walked in a browser; the plan's lane 5
*State* lists items 1–8, and lane 7's items 9–12:

| # | Walk | Chapter |
|---|---|---|
| 1 | a room's edit page opened cold shows its site, and Save keeps it; an event *"Not tied to a site"* survives a cold load and a Save (**F-27**, the shared select — HR-wide) | 12, 7 |
| 2 | the removes: `hr.head` gets none on tasks or closures, and does on guests, the register and files; `admin` gets all | 6, 13 |
| 3 | a moved event shows *"Originally …"* and *"Moved on … — reason"* | 6 |
| 4 | the landing as a plain employee: one card, linking My Schedule | 3 |
| 5 | the site pickers on the closure, event and room forms list the eight sites only | 5, 12, 13 |
| 6 | My Schedule draws a week's leave or a multi-day closure under every day it covers | 10A |
| 7 | Team Schedule as `hr.head`: every unit, the filters, *Schedule for this unit* | 10B |
| 8 | Team Schedule as a unit head not on the HR desk, and as a plain employee | 10B |
| 9 | the portal calendar as `staff`: an invitation answered; a long band broken at the week's end | 3a, 6a |
| 10 | the bell's invitation opens the staff event page; an event not for them is *"not found"* | 6a |
| 11 | the room view: others' bookings read *booked*; *Book this room* | 3a, 10C |
| 12 | the HR menu's calendar as `hr.head`: every event, both kinds of closure | 3a |

### Open — found while writing this guide *(lane 6; recorded, not fixed)*

Each is small, none stops a demonstration, and each is said where it bites:

| | Finding | Where |
|---|---|---|
| **F-59** | The reminder sweep's run-now, `POST reminders/run`, has no control on any screen — only the event page's per-event buttons | § 18 |
| **F-60** | `HR.Company.Approve`'s description names events, not bookings, though it is the bookings' fallback tier too | Conventions |
| **F-61** | The recruitment suite (`hr-recruitment/run-round4-d.mjs`) leaves two *R4D* events on UAT each run — 54 now, on the desk's register, landing and calendar. Its clean-up is the recruitment suite's to add | § 2.1 |
| **F-62** | To the HR desk, a calendar entry it is not invited to says *"it is on your calendar because it is for you"* | 3a |
| **F-63** | The new-event header says *"You are recorded as the organiser"*, though the *Organiser* field may name somebody else | 5 |
| **F-64** | An event awaiting approval reads *Scheduled* on the landing and in the register's Status column | 3, 4 |
| **F-65** | The engine offers **Approve** to an organiser on the HR desk who did not create the event; the server then refuses them | 6 |
| ✅ ~~**F-66**~~ | ~~The bookings register's row menu offers **Approve** to the booking's own booker~~ — **fixed 2026-10-06**, with F-67 | 8 |
| ✅ ~~**F-67**~~ | ~~A row's ⋯ opens an empty menu when nothing applies to it — the bookings register on a completed, cancelled or no-show booking (which was also offered Cancel), and every HR collection tab whose items are hidden for a row, such as a closed event's guest list~~ — **fixed 2026-10-06** (found by the user's browser walk): such a row has no ⋯. The shared tab is HR-wide | 6, 8 |

### Open — other modules' work

| | What | Whose | Where it bites here |
|---|---|---|---|
| **A-92** | Attendance has no working-day builder: it reads neither closures nor public holidays | HR — attendance (the attendance guide's ledger) | Rule 1, § 18 |
| **Payroll hand-off, § 3 item 4** | An unpaid closure day is readable (`closures/employee-days`) and not yet acted on | payroll (`HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md`) | Rule 1, § 18 |
| **#15** | A decision taken in the generic workflow inbox does not reach the record | Platform (the workflow engine) | Rule 2, § 18 |
| **#33** | `Notifications` has no index for its poll — 1.1 GB and 418k rows on UAT, so every read scans the table | Platform | the review suite reads it `WITH (NOLOCK)` (lane 6a) |
| **#40** | An email sent with nobody signed in finds no mail server, so the hourly sweep's emails fail even where one is set up | Platform | Rules 3 and 6 |
| **#23** | Payroll's profile foreign key fails at each new hire on UAT | payroll | only the review suite's API log, at its fixture hires |

The `#` numbers are entries in `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.

### Open — the demo data

- **UAT's anniversary** is dated 18 October **2026**, so it reads as its first year, with no "74th" (§ 2.1, chapter
  14). Re-dating it to 1952 is a one-row data edit nobody has ruled on.
- **UAT's board meeting** predates the reply-by rule: it asks for replies and has no deadline, so it is never chased
  (chapter 6).
- **UAT's seeded dates do not move.** They were set on about 2026-09-29: the fire drill (8 October) and the board
  meeting (12 October) pass this month, after which chapter 6's Part 3 and the diaries' fortnight need another event.
  A rebuilt database's dates move with its build (§ 2.2).

### What is genuinely strong here

Worth being as precise about as the gaps, because a module that was dormant a year ago now has a long list:

- **A room cannot be double-booked, and the rule lives in the right place.** The clash is checked on create **and** on
  edit, under one lock, so two people pressing *Book* at the same instant cannot both have the room. The booking form
  asks the server which rooms are free and offers only rooms whose own rules — seats, longest booking, days ahead — the
  booking meets, instead of offering everything and failing at the end.
- **Approval is the corporation's approval, not a flag.** Events and bookings that need it go through the workflow
  engine like leave and travel. Nobody approves what they organised, booked or created, and until it is approved
  nobody is invited or reminded (Rule 2).
- **One definition of "who is it for".** An event's scope, unit, visibility and *Show on company calendar* make one
  audience, and that audience decides the calendar, the diaries, the clash rule and the announcement. The form counts
  it before you save.
- **The diaries, the calendar and the interview clash check ask one question in one place.** "What is this person
  committed to?" is answered from the same seven sources over an hour, a fortnight or a month, so the next kind of
  commitment the organisation tracks reaches all of them at once.
- **A closure is a real day off.** Leave over it is not charged, the statutory clocks step over it, and saving one
  gives back leave already granted — re-counted, and each person told. It used to be a note on a calendar.
- **It counts truthfully.** Every send says whom it was for and whom it reached; *Sent* and the send-once stamps are
  written only for what reached somebody, so a pass that fails halfway costs only what it had not sent, and nobody is
  told twice.
- **The calendar file updates rather than duplicates.** One identity per event and a sequence raised with each change
  move a guest's own calendar entry when the event moves, and withdraw it when it is cancelled.
- **A series is every date, and every action asks how far.** A recurring meeting is real events with their own
  registers, and nothing changes a date that has passed.
- **The actor is the token.** The booker, the marker, the uploader, the announcer, the creator and the approver all
  come from the signed-in person; the organiser is the one chosen field, on purpose, with the creator stamped beside
  them.
- **The Admin tier means something.** Not "senior Write" — three things: the settings that move a trust boundary, the
  corporation's images, and destruction. The logo, the seal and the signature are **versioned rather than overwritten**,
  so which letters carried which seal stays answerable.
- **Every remove is shown only to whoever may use it**, and a refusal that remains is a sentence saying why.
- **A staff member sees what is theirs, and only that.** The staff event page has no budget and no other guest's answer,
  the meeting password goes only to guests and the organiser, and somebody else's room booking reads *booked*.
- **History is kept.** A room with bookings on record cannot be deleted, only retired; retiring one lists its bookings
  still to come and offers to cancel them, telling each booker. A cancelled event or booking keeps its reason.

---

## Appendix A — every route, in demo order

Twenty-four pages, every one in this module. A route in brackets is a parameter.

| # | Route | Chapter | Window · persona |
|---|---|---|---|
| 1 | `/hr/company-schedule` | 3 | A · `hr.head` — anybody, who sees one card |
| 2 | `/hr/company-schedule/calendar` | 3a | A · `hr.head` |
| 3 | `/me/calendar` | 3a | C · `staff` |
| 4 | `/hr/company-schedule/events` | 4 | A · `hr.head` |
| 5 | `/hr/company-schedule/events/new` | 5 | A · `hr.head` |
| 6 | `/hr/company-schedule/events/[id]` | 6 | A · `hr.head`; B · `hr.officer` approves |
| 7 | `/me/calendar/events/[id]` | 6a | C · `staff` |
| 8 | `/hr/company-schedule/events/[id]/edit` | 7 | A · `hr.head` |
| 9 | `/hr/company-schedule/bookings` | 8 | A · `hr.head` |
| 10 | `/hr/company-schedule/bookings/new` | 9 | A · `hr.head` |
| 11 | `/hr/company-schedule/bookings/[id]` | 10 | A · `hr.head`; B · `hr.officer` approves |
| 12 | `/hr/company-schedule/my-schedule` | 10A | anybody — A, then C |
| 13 | `/hr/company-schedule/team` | 10B | A · `hr.head`; optionally `head.dev` |
| 14 | `/me/room-bookings` | 10C | C · `staff` |
| 15 | `/me/room-bookings/new` | 10C | C · `staff` |
| 16 | `/me/room-bookings/[id]` | 10C | C · `staff` |
| 17 | `/administration/hr/company-schedule` | 11 | A · `hr.head` |
| 18 | `/administration/hr/company-schedule/rooms` | 12 | A · `hr.head` |
| 19 | `/administration/hr/company-schedule/rooms/new` | 12 | A · `hr.head` |
| 20 | `/administration/hr/company-schedule/rooms/[id]/edit` | 12 | A · `hr.head` |
| 21 | `/administration/hr/company-schedule/closures` | 13 | A · `hr.head`; D · `admin` to remove |
| 22 | `/administration/hr/company-schedule/milestones` | 14 | A · `hr.head`; D · `admin` to remove |
| 23 | `/administration/hr/company-schedule/fiscal-calendar` | 15 | A · `hr.head` |
| 24 | `/administration/hr/settings/company-profile` | 17 | A · `hr.head`; D · `admin` for the images |

**Visited from § 18, outside the module:** `/administration/hr/settings/letter-templates`,
`/administration/hr/settings/policy`, `/hr/announcements` and `/workflow/inbox`. **Retired in lane 4b:** the old
`/administration/hr/company-schedule/fiscal-years` and `fiscal-years/[id]` (chapter 16).

---

## Appendix B — the permission map, route by route

`hr.head` and `hr.officer` hold **Read**, **Write** and **Approve**, and **not Admin**. Every route is under
`api/CompanySchedule` unless the row says otherwise; the Conventions' ladder says the same thing by permission.

| Area | `HR.Company.Read` | `HR.Company.Write` | `HR.Company.Admin` |
|---|---|---|---|
| **Events** | list, paged, `search`, `export` (CSV), detail and `details`, by range / organiser / unit / status / category, upcoming; the landing's `dashboard`; `audience-preview`; `clashes`; the event's bookings | create, update; `approve`, `reject`; `cancel`, `reschedule`, `complete`; `series/extend`; the announcement read and `announce` | **delete** |
| **Guests** | the list | add; edit (`PUT participants/{id}`); **remove**; record an answer (`participants/respond`); send held invitations (`invitations/send`); a reminder now (`reminders`); a chase now (`rsvp-reminders`) | — |
| **Attendance** | the register | mark; check out; **remove a row** | — |
| **Tasks** | the list | add, update, complete | **remove** |
| **Files** | the list; download | upload; **remove** | — |
| **The sweep** | — | `reminders/run` — no screen (**F-59**) | — |
| **Rooms** | list, paged, detail, by location, active, `available`; `retirement` — what retiring it would cancel | create, update | **delete** — only a room with no booking history |
| **Bookings** | list, paged, `search`, `export` (CSV), detail, by room / booker / range / status, pending approvals | create; `series` — a room for every date; update; `approve`, `reject` (*Not approve*); `cancel`; `no-show` | **delete** |
| **Milestones** | list, paged, detail, by category, range, upcoming; their files, and downloads | create, update; upload and **remove** a file | **delete** — its files with it |
| **Closures** | list, paged, detail, range, by type / location, upcoming, `is-closure-date`; `employee-days` — payroll's read | create, update; the announcement read and `announce` | **delete**; `recharge-leave` — the one-time re-count for closures older than lane 1 |
| **Fiscal calendar** | `fiscal-calendar`, `fiscal-calendar/year`, `fiscal-calendar/period` — Finance's, read | — | — |
| **Company profile** (`api/hr/company-profile`) | the profile; `seal-assets` and their history | update the profile | upload, and `retire`, a logo, seal or signature |
| **HR policy** (`api/hr/policy-settings`) | read | — | **save** |

**No permission at all — any internal login, linked to an employee record.** The server decides per person:

| Route | Who gets what |
|---|---|
| `my-schedule` | the caller's own diary; there is no parameter for anybody else's |
| `team-schedule/units`, `team-schedule/{unit}` | the HR desk (`HR.Company.Write`) any unit; a unit's head their own unit and everything beneath it; anybody else, nothing |
| `calendar`, `calendar/events/{id}` | what Rule 5 says the person may see; an event that is not for them is *"not found"* |
| `events/{id}/participants/{pid}/reply` | the invitee's own answer, until the RSVP deadline; refused to anybody else |
| `me/rooms`, `me/rooms/available`, `me/rooms/busy` | the rooms, the free ones, and when each is held — somebody else's booking only as *booked* |
| `me/room-bookings` — list, detail, create, update, `cancel` | the caller's own bookings, under every rule the desk's bookings meet |

**`HR.Company.Approve` is on no route.** It decides an event or a booking that needs approval only where no workflow
definition is published — the fallback tier (Rule 2) — and does the same for team objectives and terms of reference.
Its description names events and not bookings (**F-60**).

---

## Appendix C — related documents

| Document | What it adds |
|---|---|
| `docs/HR/areas/company-schedule/HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md` | **The live state.** Every finding's lane (§ 5), every decision (§ 1), what each lane built and how it was proved (each lane's *State*), the browser walks still owed, and the log (§ 9). Where it and this guide disagree, the plan is right |
| `dev-harness/hr-company-schedule/README.md` | The proof routine, and `run-final-review.mjs`: 1,152 assertions, a block per slice, each rule in both positions, on fixtures of its own that it removes |
| `dev-harness/hr-demo-smoke/scenarios/110-company-schedule.mjs` · `170-she.mjs` | Exactly what a rebuilt database holds (§ 2.2) — 110 the rooms, the bookings and the four events; 170 Safety's drill and its event |
| `docs/HR/areas/leave/HR-LEAVE-SYSTEM-GUIDE.md` | The holiday calendar a closure sits beside, and the leave a closure re-counts (Rule 1) |
| `docs/HR/areas/attendance/HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md` | The other half of "is this a working day?" — its finding **A-92**: attendance reads neither closures nor holidays |
| `docs/HR/areas/travel/HR-STAFF-TRAVEL-SYSTEM-GUIDE.md` | A trip day on a whole-company closure is not posted as a day on duty |
| `docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md` § 9.4 | The interview clash check that reads this module, and the interview that holds a room booking. The series' first guide, and the format's origin |
| `docs/HR/areas/employees/HR-EMPLOYEES-SYSTEM-GUIDE.md` | The employee picker every guest, task owner and register row uses |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | The engine events and bookings are approved on since lanes 2b and 3b-1 (Rule 2) |
| `docs/HR/integration/HR-MODULE-INTEGRATION-MAP.md` | Where the company schedule sits against leave, attendance, SHE and recruitment |
| `docs/HR/integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` | § 2.1 and § 3 item 4: closures beside holidays for payroll, and the unpaid day payroll does not yet act on |
| `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` | The other modules' entries § 21 names: #15, #23, #33, #40 |
| `docs/HR/programme/HR-CONFIGURATION-REGISTER.md` | § 2.7, the wording of every email; § 2.8, the reminder sweep and the RSVP-chase lead, each proved in both positions |
| `docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-4-PLAN.md` | Round 4: the build and decisions behind the guide's second edition |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 7, the three-step version of this module for the standard demo pack |

---

*End of the HR Company Schedule System Guide.*
