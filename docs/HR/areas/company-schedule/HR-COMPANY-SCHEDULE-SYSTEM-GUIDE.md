# HR Company Schedule — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source; updated 2026-09-24 for round 4 of the demo feedback;
**rewritten whole in the company-schedule final closure's lane 6 (from 2026-10-06)** against what lanes 0–7
built. Every chapter is re-read in the code and against UAT's own rows, and the walks fork where UAT differs from
a database the demo pack builds today (§ 2.1). Where this guide and
`HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md` disagree, **the plan is right**: its § 5 gives every finding's owner,
and each lane's *State* says what was built and how it was proved. The guide describes what the code does; where a
screen offers something the server refuses, the step says so.

> **⚠ Rewrite in progress (lane 6).** The front matter, the seven rules, the conventions and chapters 1–2 are
> new (slice 6b, 2026-10-06), and so are chapters 3–7 with the new 3a and 6a (slice 6c). Chapters 8–21 and the
> appendices still describe round 4 until slices 6d–6e replace them; their live-write numbers will follow on from
> chapter 6a's 9. Where an old chapter disagrees with the rules or chapter 1, the rules and chapter 1 are right.
>
> ⚠ **Not yet walked in a browser:** the screens lanes 1–7 changed. The plan lists the walks (lane 5 items
> 1–8, lane 7 items 9–12); every server rule behind them is proved by the review suite on UAT (1152 assertions,
> two clean passes).

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
> on both demo databases, so this is load-bearing rather than theoretical: chapter 17 is the only place `admin` is
> used, for the logo, the seal and the signature.

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
- A cancelled or completed event's list is its record: nothing can be added, changed, removed or answered.

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
| **Delete** | **only for `HR.Company.Admin`**, in red. ✅ **Hidden from `hr.head` since round 4** |

Cancel's dialog: *"\<room\> — the slot is released for someone else."*
Delete's confirmation, for an administrator: *"Cancelling keeps the record and the reason. Deleting
removes it entirely."*

### ▶ Walk it

**1 — Open the register.** On a clean rebuild, **one** seeded row — plus your own, if you booked a
clash window in § 2.4.

> *"A room held. The Management Committee has the boardroom for three hours in the morning — with
> a video link to the Ho office in the requirements."*

⚠ *(Corrected 2026-09-24.)* This step used to read two rows, the second a *Community 25 design
review* booked by the Head of Development. **That booking has never existed on a built database.**
Scenario 110 books it as `head.dev`, who holds no company-schedule permission, and swallows the
refusal (**R4-2.3**).

**2 — Point at *Booked by*.**

> *"And who booked it is recorded from the account that did it — from the token, not from a field
> somebody typed. There is no 'booked by' on the form."*

Booking is `HR.Company.Write`, so on this database it is an HR desk action. If you are asked
whether a head of department books their own rooms, the honest answer is *"not with the roles as
seeded — that is a permission TDC decides in role setup."*

**3 — Point at the *Status* column.** It reads **Confirmed**.

> *"Confirmed, because that room is not set to need approval. Which is a per-room decision, and I
> will show you what changes when you turn it on."*

**4 — Open the *Status* filter** and read the five values.

> *"Tentative, Confirmed, Completed, Cancelled, No show. The last two matter to a facilities
> manager more than anyone else — a room held and not used is a room somebody else could have
> had."*

⚠ Do not promise *Completed* or *No show* will ever appear — nothing sets them (**C-8**).

**5 — Open the ⋯ menu.** Since round 4 it offers **Approve** and **Cancel** and nothing else to
`hr.head`: Delete is an administrator's, and it is no longer shown to anybody who is not one.

> *"Cancel, and no delete. Cancelling keeps the record and the reason — which is an HR officer's
> action. Deleting removes it entirely, and that is an administrator's."*

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

> **Round 4:** the Delete is hidden from `hr.head` (C-1). No new finding on this screen; the seeded
> booking that never existed is **R4-2.3**, in § 21.

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

The failure toast carries the **server's own sentence**: the clash, or since round 4 the room's own
rule — *"Boardroom seats 18; this booking expects 40."* Only when the server gives no sentence does it
fall back to the line written for the race: *"The room may have been taken while you were filling
this in."*

### ▶ Walk it

This is the chapter to slow down on. Do the refusal first — it is more convincing than the success.

**1 — Press *Book a room*.** Point at Card 2's empty state before touching anything.

> *"Notice what it will not do. There is no room dropdown yet, because the question 'which room'
> has no answer until you have said when. Offering every room and letting the booking fail at the
> end is the same screen built backwards."*

**2 — Now demonstrate the clash. Set From and To to the window you wrote down in § 2.4** — the
Boardroom's busy morning. Expected attendees **12**.

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

> *"'BK-2026-00002 — Boardroom. It needs approval before it is confirmed.' Because that room is set
> to need approval, and the booking came out Tentative rather than Confirmed. A room that does not
> need approval books straight through. Same form, different room, different outcome — and the
> rule is on the room, not on the person."*

*If you did not:*

> *"'BK-2026-00002 — Boardroom.' Confirmed immediately, because that room does not require
> approval. Turn that setting on for a room and the same booking comes out Tentative and waits for
> somebody — I will show you the switch in a moment."*

*(The number is `BK-2026-00002` on a clean rebuild, which seeds one booking — or `00003` if you
booked a clash window yourself in § 2.4.)*

*Undo:* chapter 19 — **Cancel** it, with a reason.

**9 — *(optional, round 4)* The room's own rule.** Only if you set the Huddle Room's longest booking
in § 2.3. Search a three-hour window for **four** people. The Huddle Room **is** offered — say so
before you pick it:

> *"The search asks whether the room is free and big enough. It does not yet ask the room's own
> limits — watch."*

Pick it and press **Book room**. It is refused with *"Huddle Room 1 may be booked for at most 2
hour(s) at a time; this booking is 3. Shorten it, or book a room without that limit."* Nothing is
written. ⚠ Not browser-walked.

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
⚠ It does **not** apply the room's longest booking or days-ahead limit (**R4-9.1**).

**What create does:** loads the room and refuses it if it is not bookable; **since round 4, applies
the room's own rules** — the end must be after the start, and then longest booking, days ahead and
seats, each skipped when the room leaves it blank; runs the same overlap test again; generates the
booking number from the shared sequence; and sets the status from the **room's** `RequiresApproval`
— *Tentative* if it needs one, *Confirmed* if it does not. Then re-reads before mapping, for the
same navigation reason as events. **Edit** applies the same rules and the same overlap test, with
the booking's own id excluded.

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

> **Round 4, 2026-09-24: one new finding.**
>
> | | Now |
> |---|---|
> | **R4-9.1** | **The search offers rooms the booking will refuse.** Round 4 put the room's longest booking and days-ahead limit on create and edit, and not on `rooms/available`. A room limited to two hours is offered for a three-hour window, and the refusal arrives at **Book room**. The screen was built on the opposite principle — ask what is free, then offer only that — so the search should apply the same three rules. Seats are consistent: the search already filtered on them. |

| Gap | |
|---|---|
| ✅ ~~**C-4 · The room's max duration and advance-booking window are not enforced**~~ **Fixed in round 4**, on create and edit (Rule 4) — but see R4-9.1 | |
| ✅ ~~**C-27 · Capacity is a filter, not a rule.**~~ **Fixed in round 4** — create and edit refuse a booking that expects more people than the room seats | |
| **C-28 · The availability query is not tenant-filtered.** The repository collects booked room ids across every tenant before the service filters the *rooms* by tenant. Harmless — room ids do not collide across tenants — but it is the only read in the module that does not scope explicitly | |
| ✅ ~~**C-6 · The booking number counts live rows**~~ **Fixed in round 4** (Rule 6) | |
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
> extend into a taken one. **Since round 4 every save also re-applies the room's own rules** — its
> seats, its longest booking and how far ahead — so a booking that breaks a rule set after it was
> made cannot be saved again, even for a change to its notes, until it complies.

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

> *"And changing the window re-runs the clash check — and the room's own limits. Those are not
> create-time rules, they are the rules."*

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
excluded**, so saving an unchanged window does not clash with itself. Since round 4 it first runs
`EnforceRoomRules` against the booking's room, the same method create uses.

### ⚠ Known gaps

| Gap | |
|---|---|
| **C-30 · Approve has no guard.** The screen shows it only on a *Tentative* booking; the service will confirm anything, including an already-cancelled booking | |
| **C-31 · Cancel does not check the status either.** A cancelled booking can be cancelled again, overwriting the first reason and date | |
| **C-32 · There is no delete on the detail page**, only on the register — inconsistent, though in practice a helpful accident | |

---

## 10A. `/hr/company-schedule/my-schedule` — one person's diary *(round 4)*

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Company Schedule → **My Schedule** ·
`/hr/company-schedule/my-schedule` · as **hr.head** — or as anybody: it needs no HR permission ·
**3 minutes**

### 📖 What it is

> *"Everything the organisation has me down for, in one place: the meetings I am invited to, the
> rooms I have booked, the interview panels I sit on, my training, my leave and my travel. Before
> this, that was five screens in five modules, and none of them knew about the others."*

### 👁 On the page

**Header:** *My schedule* — *"Everything you are down for — meetings, rooms you have booked,
interview panels you sit on, training, leave and travel."*, back-link to the landing.

**The range card:** **From** and **To** *(dates — today to thirteen days on, by default)*, and two
buttons, **Next fortnight** and **This week**. The page reads as soon as both dates are set.

**One card per day**, earliest first, headed with the date. Each entry is one line: an icon for its
kind, the label, the time — or *"All day"* for anything recorded by the day — its reference, and a
badge naming the kind.

| Kind | The label reads, for example | Comes from |
|---|---|---|
| Interview | *Interview panel for Senior Procurement Officer* · INT-000025 | interviews you sit on — scheduled, rescheduled or in progress |
| Event | *Board of Directors — Q3 meeting (Scheduled, invitation Sent)* · EVT-2026-00001 | events you are **invited to**, unless you declined or it was cancelled — not events you only organise (**R4-6.7**) |
| Room booking | *Booked Boardroom (Confirmed)* · BK-2026-00001 | rooms **you booked** |
| Training | the programme's name and the nomination's status · its number | nominations approved, confirmed or waitlisted, with the session's hours where the day has one |
| Leave | *Casual Leave (Pending)* · LV2026000011 | leave approved, pending or in progress |
| Travel | *Travel (Approved)* · the request number | travel approved, submitted or in progress |
| Closure · Holiday | *Business closure: Year-end stocktake* · *Public holiday: …* | every closure and public holiday in the range (**R4-10A.4**) |

**Empty:** *"Nothing in this range — No meetings, bookings, panels, training, leave or travel
between those dates."*

**A range over sixty days, or one that runs backwards,** is refused, and the page shows the
server's own sentence: *"Sixty days is the most that can be read at once — narrow the range."*

### ▶ Walk it

**1 — Open My Schedule.** As `hr.head`, the next fortnight. Measured on UAT on 2026-09-24:

- three interview panels, with their times;
- a day of casual leave, still pending, marked *All day*;
- the fire drill;
- the board meeting.

On a rebuild the dates move with the build, but the kinds are the same.

> *"This is my next fortnight, and I did not put any of it here. The interview panels come from
> recruitment, the leave from the leave module, the drill and the board meeting from the company
> schedule. Five modules, one list — and nobody had to copy anything into a calendar."*

**2 — Point at the leave day's *All day* and an interview's times.**

> *"And it knows the difference between 'she is away that day' and 'she is in a room from nine to
> eleven'. Leave is recorded by the day, so it says all day rather than inventing hours."*

**3 — Press *This week*.**

> *"And it is the same question the interview scheduler asks — 'what is this person committed
> to?' — over a fortnight instead of an hour. It is literally the same code, so the day somebody
> teaches the system about another kind of commitment, both learn it at once."*

**4 — *(optional)* In a window as `staff`, open the same page.** A different person, their own
diary, and no HR permission needed.

> *"Nobody gave this person a permission for it. The system takes who you are from your sign-in,
> and there is no way to ask it for somebody else's diary from here — that is the next screen, and
> it is HR's."*

⚠ In that window, **stay on this page**. The *Company Schedule* group header above it opens a
landing that tells `staff` there is nothing scheduled (**R4-3.1**).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The diary | `GET api/CompanySchedule/my-schedule?from&to` | **no HR permission** — any signed-in internal user linked to an employee record. The employee is taken **from the token**; there is no id in the URL, and there must not be |

`PersonalScheduleService.GetForEmployeeAsync` asks the seven registered
`IPanelistCommitmentSource` implementations — the same classes the interview clash check uses —
about one employee over the range. Each answer carries its kind, a hardness, the label, start and
end, whether it is recorded by the day, and the reference.

*Hard* means confirmed and exact to the minute: another interview, a confirmed booking, a confirmed
meeting the person accepted. In a diary, hardness refuses nothing; the team view shades by it.

A user with no employee link gets a 400, *"Your user account is not linked to an employee
record."* A source that throws is logged at Error and left out (**R4-10A.3**).

### ⚠ Known gaps

> **Round 4, 2026-09-24: a new screen, and five findings.**
>
> | | Now |
> |---|---|
> | **R4-10A.1** | **A multi-day entry is filed under its first day only.** A week's leave appears on Monday's card and nowhere else. Leave that began before the range is filed under its own start, a card dated before the range. Each source reports a commitment once, at its start; a course with several sessions in the range shows its first. |
> | **R4-10A.2** | **A timed event that spans days counts on its first day only — here and in the interview clash check.** The event source places a timed event on its start date, so a panelist on day two of a two-day timed meeting reads as free. All-day events are found on every day they cover, but reported with the first day's date. |
> | **R4-10A.3** | **A source that fails leaves the diary silently short.** It is logged at Error, and the page cannot say so. The clash check returns the list of sources that answered (`sourcesConsulted`); the diary does not. |
> | **R4-10A.4** | **Closures and public holidays are everybody's.** Every closure is attributed to every person, whatever its site or department (**R4-13.1**). Public holidays are every calendar the tenant keeps, and inactive ones too: the source filters on neither `HolidayCalendarId` nor `IsActive`. |
> | **R4-10A.5** | **Not browser-walked.** Proved by harness (lane D-2's suite asserts an ordinary employee reads their own diary and is refused the team's) and by reading the page. |

---

## 10B. `/hr/company-schedule/team` — a unit's diary, side by side *(round 4)*

### 📍 Where you are

**Sidebar:** … → Company Schedule → **Team Schedule** · `/hr/company-schedule/team` · as
**hr.head** — it needs `HR.Company.Write` · **3 minutes**

### 📖 What it is

> *"What a unit and everybody under it are already committed to, side by side — so that when you
> pick a time for something, you can see who you are about to double-book."*

### 👁 On the page

**Header:** *Team schedule* — *"What a unit and everyone under it are already committed to —
before you pick a time."*, back-link to the landing.

**The picker card:** **Organisation unit** *(every unit)* · **From** · **To** *(today to six days
on, by default)*.

**Until a unit is chosen:** *"Choose a unit — Pick an organisation unit to see what it and
everything under it are committed to."*

**The grid:** a card headed *"N people"* with the key *"red = confirmed · amber = worth
knowing"*.
- One row per person: every **active** employee in the unit **and every unit beneath it**,
  alphabetically.
- One column per day.
- Each cell shows up to two entries, then *"+N more"*, and hovering lists them all.
- A cell with anything hard is shaded red, otherwise amber. An empty day is a dashed box.

**Nobody there:** *"Nobody in this unit — The unit and its subtree have no active employees."*

### ▶ Walk it

**1 — Choose a small unit — a section, not a directorate.** ⚠ The subtree is the point, and also the
trap. On UAT, *HR / Administration Department* is **363 people**, 360 of them with an empty
fortnight (**R4-10B.4**).

> *"A head about to call a unit meeting. Everybody in the section and everything under it — and
> before you pick a time, you can see who is on leave, who is on a course and who is on an
> interview panel."*

**2 — Point at a red cell and an amber one.**

> *"Red is fixed — an interview, a confirmed meeting they have accepted. Amber is worth knowing —
> leave, travel, a course, a closure. Leave is amber because it is recorded by the day: somebody on
> leave may well come in for an hour, and the system does not overrule them."*

⚠ **Do not read an empty cell as "free" out loud.** A week's leave is drawn on its first day only,
and leave that began before the range is not drawn at all (**R4-10B.2**).

**3 — Say what the screen does not do.**

> *"This shows you the clashes. It does not yet book the meeting for you — you still create the
> event and invite people one at a time, and the invitation form does not flag a clash. That is
> the next step, and the data it needs is all here."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Unit picker | `GET` the organisation units | lookup |
| The grid | `GET api/CompanySchedule/team-schedule/{organizationUnitId}?from&to` | **`HR.Company.Write`** — Write, not Read, because it shows other people's leave and travel |

`PersonalScheduleService.GetForUnitAsync` walks the unit's subtree (`UnitSubtreeAsync`, which
returns **unit** ids), takes the active employees in those units, and asks the same seven sources
about all of them at once. The same sixty-day limit applies.

### ⚠ Known gaps

> **Round 4, 2026-09-24: a new screen, and four findings.**
>
> | | Now |
> |---|---|
> | **R4-10B.1** | **It reads; it does not schedule.** The round 4 plan's D5 asked for a head to schedule an event *for* the unit: participants pre-filled from the subtree, and a clash flag on each person **at the point of selection**. Only the read was built. An event still gets its participants one at a time, with no availability shown (C-15 stands). |
> | **R4-10B.2** | **The grid draws each entry on its start day only.** A week's leave marks Monday and leaves Tuesday to Friday as dashed, empty boxes. Leave that began before the range is keyed to a day that is not a column, so it is **not drawn at all**. For a screen whose purpose is "who is free", that is the wrong way to fail. |
> | **R4-10B.3** | **Any Company Write holder can read any unit, and a head without it cannot read their own.** The gate is the HR desk's permission, not "this is my unit". As seeded, only HR sees this screen. That is defensible, but it is not the plan's picture of a head scheduling for their team. |
> | **R4-10B.4** | **A directorate is hundreds of rows.** The whole active subtree is drawn, so *HR / Administration Department* is 363 rows, and seven reads are made across all 363 people. There is no paging and no "only people with something on". |

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

> **Room code is generated when blank** as `RM-0001`. *(Round 4.)* It comes from the shared number
> sequence, which skips any code a room has ever held, deleted rooms included, and room codes are
> unique per tenant in the database. So a generated code can no longer collide (**C-34**, fixed). A
> code you **type** is still checked against live rooms only, and reusing a deleted room's code gets a
> bare *"Something went wrong"* from the database (**R4-12.1**).

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

**8 — Point at the two numeric fields underneath it.** *(Round 4: both are enforced now.)*

> *"Two more rules sitting beside it — the longest a single booking may be, and how far ahead
> people may book. Leave them blank and there is no limit. Set them, and a booking that breaks one
> is refused with a sentence naming the rule — and so is a booking for more people than the room
> seats. On a new booking and on every change to one."*

If you are doing § 2.3's optional second beat, this is where you set the Huddle Room's longest
booking to **2**. Be ready for the honest follow-up: the availability search does not yet apply
these two limits, so the refusal comes when the booking is made, not in the picker (**R4-9.1**).

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

> **Round 4, 2026-09-24: one new finding, and this screen's Delete is one of the eight still offered.**
>
> | | Now |
> |---|---|
> | **R4-12.1** | **A typed room code once used by a deleted room is refused with a 500.** The duplicate check reads live rooms only, while round 4's unique index covers deleted rows too. The check passes, the database refuses, and nothing in the company-schedule error filter translates it: *"Something went wrong."* Check the code with deleted rows included, as the generator does. Only reachable after an Admin delete. |
> | *(R4-6.6)* | ⋯ → **Delete** here is still offered to `hr.head`, and still 403s. |

| Gap | |
|---|---|
| ✅ ~~**C-4 · Max duration and advance-booking days are stored and never read**~~ **Fixed in round 4** — enforced on create and edit of a booking (Rule 4, and R4-9.1 for the search) | |
| **C-33 · The delete failure toast guesses the wrong reason** — it blames bookings when the real answer is a 403 | |
| ✅ ~~**C-34 · A generated room code can collide.**~~ **Fixed in round 4** — issued from the shared sequence; see R4-12.1 for the typed code | |
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
> its scope, its dates, whether it is paid, whether it counts, and who announced it. Two things
> read it already: a recruiter scheduling an interview on that day is warned, and it appears in
> everybody's diary. What has not been wired is the two places that matter most: the leave
> day-counter and the attendance day-builder. Both already take the statutory holiday calendar out
> of their arithmetic, so this is the same join, one level up."*

⚠ If you then open a diary to show the closure, note that it shows for **everybody**, including
people at other sites, whatever the closure's scope (**R4-13.1**). The Ashaiman closure you just
created is the example that exposes it.

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

> **Round 4, 2026-09-24: one new finding.**
>
> | | Now |
> |---|---|
> | **R4-13.1** | **Every closure is attributed to everybody.** The commitment source behind the interview clash check and the two diaries reads all the tenant's closures in the window and ignores `AffectsAllStations`, `LocationId` and `DepartmentId`. A two-day closure of the Ashaiman office warns every interview panel at Tema and appears in every diary. That is the same over-reporting as C-37, from a new reader. The scope rule `is-closure-date` already applies is the one to reuse. |

| Gap | |
|---|---|
| ◐ **C-5 · Closures reach nothing.** No leave and no attendance code path reads `BusinessClosures`, and no screen calls `is-closure-date`. The screen's own switch description and the demo runbook's aside both claim otherwise — Rule 5. **Since round 4 the interview clash check and the two diaries read closures**, as a soft warning, but without their scope (**R4-13.1**) | |
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

**1 — Open the screen.** One row: **2026**, *FY 2026*, 1 Jan – 31 Dec, **Current**, Active. ⚠ No
*Current* badge means a company-schedule harness suite has run since the build (Rule 8). Put it back
with ⋯ → **Set as current** before the room fills, not during.

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

Tables: `FiscalYear`, `FiscalPeriod` — singular; the plural pair is Finance's. Also available and unused: paged, by year number, current,
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

Tables: `FiscalYear`, `FiscalPeriod` — singular; the plural pair is Finance's.

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

Eight places, three of them new in round 4. Two are worth a minute of the demo; the rest are for
the questions.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Every generated HR letter** | The offer of appointment, the confirmation letter, the service letter — all rendered from the **Company Profile**: the legal name, the registered address, the signatory, the acceptance instructions, the footer line and the **seal**. Chapter 17 is where that record lives | **Yes, by reference** — say it while you are on chapter 17; do not navigate |
| **Recruitment → Interviews** *(deeper since round 4)* | The interview **clash check** reads this module: meetings a panelist is **invited to** (a refusal when the meeting is confirmed and they accepted, otherwise a warning), rooms they **booked** (a refusal when confirmed), and closures and public holidays (a warning). And an interview can **hold a room booking** rather than name a room in free text — one interview per booking. The recruitment guide's § 9.4 has the whole of it | **Yes, if recruitment came first** — *"the panel scheduler knows about the board meeting"* is the cross-module line |
| **HR Settings → Letter & Email Templates** *(round 4)* | `/administration/hr/settings/letter-templates` — the module's five emails: *Event Invitation*, *RSVP Reminder*, *Event Reminder*, *Event Rescheduled*, *Event Cancelled*. Filed under the categories *Invitation*, *Reminder* and *Change*; search *event*, or open one directly with `?t=CompanySchedule/EventInvitation`. Reworded per tenant; **Reset** brings the shipped wording back | **Yes, on the demo database** — it is the only way to show an email that cannot be delivered there (Rule 7) |
| **HR Settings → HR policy** *(round 4)* | `/administration/hr/settings/policy` — the *Company schedule reminders* card: *"Chase unanswered invitations this many days before the RSVP deadline"*, 2 by default. **Changing it is `HR.Company.Admin`**; `hr.head` can read it and not save it. The configuration register's § 2.8 records both positions of the setting | Only if asked |
| **The API's hourly reminder host** *(round 4)* | `CompanyScheduleReminderBackgroundService` — its first pass runs 23 minutes after the API starts, then hourly, for every tenant, under a 20-minute lease. `POST api/CompanySchedule/reminders/run` runs the same pass now for the caller's tenant (`HR.Company.Write`) | No — but know that a long-running API sends what falls due |
| **SHE → Emergency plans** | The *Fire drill — Head Office* event in this module **is** the SHE emergency plan's next scheduled drill. Two records, one occasion, and nothing joins them (**C-51**) | Mention it in chapter 4; it is a good illustration of a real integration that is not yet made |
| **Leave, travel, training and attendance** | ⚠ **Closures still reach none of them** — Rule 5 and **C-5**. **The other direction is new in round 4:** the two diaries (§ 10A, § 10B) read leave, travel and training nominations, as the interview clash check does | Say the closures point honestly in chapter 13; show the diary in § 10A |
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
| 5 | **Event approved** *(ch. 6, LW 6 — the event you created in ch. 5)* | No un-approve, and none needed: row 1's **Cancel** closes that event, approval and all |
| 6 | **Room booked** *(ch. 9, LW 7)* | Open it → **Cancel**, reason `Demonstration`. **The slot is released**, which is what matters for the next rehearsal |
| 7 | **Booking approved / extended** *(ch. 10, LW 8)* | Covered by row 6 — cancel the booking and both go with it |
| 8 | **"Bookings need approval" turned on** *(ch. 12, LW 9)* | ⋯ → **Edit** the Boardroom → turn it **off** → **Save changes**. ⚠ **Do this**, or the next rehearsal's § 2.2 count will not be zero and chapter 8's say-line stops being true |
| 9 | **Closure added** *(ch. 13, LW 10)* | ⋯ → **Remove closure** is Admin. From the `admin` window, or SQL: `UPDATE BusinessClosures SET IsDeleted = 1 WHERE Title = 'Ashaiman office — electrical rewiring'` |
| 10 | **Milestone added** *(ch. 14, LW 11)* | Same shape as row 9, on `CompanyMilestones` |
| 11 | **Fiscal period added and closed** *(ch. 16, LW 12)* | No re-open. SQL: `UPDATE FiscalPeriod SET IsClosed = 0, ClosedDate = NULL WHERE Id = '<id>'`, or `IsDeleted = 1` to drop the period entirely. ⚠ **`FiscalPeriod`, singular** — *corrected 2026-09-24; this row used to name `FiscalPeriods`, which is Finance's table.* **Or leave it** — a closed Q1 in September is correct |
| 12 | **Seal replaced** *(ch. 17, LW 13)* | **Nothing.** The whole point is that nothing was overwritten; the previous image is retired, not lost. Leave it |
| 13 | **Reminder sent now** *(ch. 6, LW 14, optional)* | Nothing was delivered (Rule 7). The stamp stops the board meeting's own reminder from going, so clear it if the next rehearsal should see *"Goes 3 days before…"*: `UPDATE CompanyEvents SET ReminderSentDate = NULL WHERE EventNumber = 'EVT-2026-00001'`. Rescheduling the event would also clear it, and would email everybody |
| 14 | **Huddle Room's longest booking** *(§ 2.3's optional beat, ch. 12)* | ⋯ → **Edit** the Huddle Room → clear **Longest booking (hours)** → **Save changes**. ⚠ Do this, or every longer booking of it is refused |
| 15 | **A clash window booked in prep** *(§ 2.4, only if the seeded one had passed)* | Open it → **Cancel**, reason `Demonstration` |

**The numbers are safe since round 4.** `EVT-…`, `BK-…` and generated room codes never repeat: a
deleted record keeps its number, and the next one takes a new number (Rule 6). **Prefer cancelling to
deleting** for the audit reason alone — a cancelled record keeps its reason.

**The clean option.** The whole database rebuilds in 45–60 minutes, and a rebuild is also the only
way to clear the harness's fixtures (Rule 8):

```
# stop every API first — a stray one ruins the rebuild. Win32_Process names carry ".exe", and an
# API started with `dotnet ErpSystem.Api.dll` is named dotnet.exe, so match both.
Get-CimInstance Win32_Process |
  Where-Object { $_.Name -eq 'ErpSystem.Api.exe' -or
                 ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*ErpSystem.Api.dll*') } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

*(Corrected 2026-09-24: the command used to filter on `Name='ErpSystem.Api'`, which matches
nothing.)*

⚠ A rebuild moves every seeded date with the build, and costs you chapter 2's prep as well. For
this module it is rarely worth it for a reset — almost every live write undoes in the UI. It **is**
worth it before a demo if the harness has run (§ 2.2).

---

## 20. The short path — 20 minutes

When the slot shrinks. Seven screens, and the story still lands — twenty-two minutes with the diary,
twenty without it.

| # | Screen | Min | The one thing |
|---|---|---|---|
| 1 | `/hr/company-schedule` | 2 | read the four cards; **"not a diary — the corporation's own calendar"** |
| 2 | `/hr/company-schedule/events/[id]` — **the Management retreat** | 5 | the Overview, then **expected 18 vs attended 4**, then read the **outcome summary** aloud. Then the Attendance tab. ⚠ Scroll past its Reminders card (**R4-6.2**) |
| 3 | The same event's **Tasks** tab, or the board meeting's | 2 | before / during / after, with owners and due dates. **"An event is a small project"** |
| 4 | `/hr/company-schedule/bookings/new` | 5 | **set a window that is already taken and press *Find free rooms*** — the boardroom is not offered. *"A room in this system cannot be double-booked."* Then book a free one |
| 5 | `/hr/company-schedule/my-schedule` *(round 4)* | 2 | `hr.head`'s fortnight: interview panels, a leave day, the drill, the board meeting. **"Five modules, one list — and nobody typed any of it in"** |
| 6 | `/administration/hr/company-schedule/rooms` — edit the Boardroom | 3 | **Site vs Where in the site**; then *Bookings need approval*, and what it changes |
| 7 | `/administration/hr/settings/company-profile` — the **Seal** card | 3 | *"replacing one retires the image it supersedes"*; then **"I cannot change the seal, and that is the point"** |

Cut, in this order if you must: the company profile *(say the sentence about letters instead)*,
then the rooms screen, then the tasks tab, then the diary.

**Do not put in the short path:** closures *(the honest caveat needs more time than the screen
does)*, milestones, or fiscal years.

---

## 21. What this walk found

> ⚠ **The live state of every finding below is now kept in
> `HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md` (2026-10-01)** — its § 5 owns each C- and R4-
> finding by lane and its § 8 holds their status. This section is the record of the walk, not a
> to-do list.

**Fifty-one findings.** This module is in better shape than its history suggests — it was dormant
for a year and was built, wired and harnessed in one pass, and it shows. Six behaviours will catch
you out in a demonstration; **five** are the ones to fix before it is called finished.

### Round 4 — what it closed, and what this rewrite found (2026-09-24)

**Closed or narrowed by lanes D-2 and N-b2** (2026-09-22 → 23; 39 + 36 assertions, each green twice):

| | Status | How |
|---|---|---|
| **C-1** | ◐ partly | the event page's and the bookings register's Delete are hidden without Admin; eight others are not (**R4-6.6**) |
| **C-2** | ◐ partly | the original window is kept on the first move and emailed; not shown on the page (**R4-6.1**), and Edit bypasses it (**R4-7.1**) |
| **C-4** · **C-27** | ✅ | longest booking, days ahead and seats refuse on create and edit, each with a sentence; the search does not apply the first two (**R4-9.1**) |
| **C-5** | ◐ partly | the interview clash check and the diaries read closures, without their scope (**R4-13.1**); leave and attendance still do not |
| **C-6** · **C-34** | ✅ | the shared sequence, deleted rows counted, unique per tenant; a typed room code has an edge (**R4-12.1**) |
| **C-17** | ✅ | invitations are emailed on add; reschedule and cancel notify everybody; the reminder and the RSVP chase are sent hourly, once each |

**Not touched, and still true:** C-3, C-7, C-8, C-9 to C-16, C-18 to C-26, C-28 to C-33, C-35 to C-51.
Two of them have a new consequence: **C-19** now sends email (**R4-6.5**), and **C-24**'s status
dropdown now cancels silently.

**Round 4's own findings — 26, recorded where they bite, and none fixed.**

Four are **in the demo data, not the code**, and they are what bites a demonstration. § 2 works
round each of them:

| | Finding |
|---|---|
| **R4-2.1** | **The lane D-1 and D-2 suites leave their fixtures live on the demo database.** Measured on UAT 2026-09-24: 60 *R4D …* events still ahead (43 within 30 days), 15 *R4D …* rooms, 32 bookings besides the seeded one. They crowd the register, the landing, the linked-event dropdown, the rooms register and the free-room search, and the hourly sweep processes them. `hr.head` cannot delete them. **Rebuild before a demo**, and give the two suites a tidy step as P2a did for the check templates. |
| **R4-2.2** | **The company-schedule slice suites leave the tenant with no current fiscal year.** They create a fixture year as *current*, which takes the flag off FY 2026, and then delete the fixture. The product half: deleting the current year leaves none, and nothing restores the one before. |
| **R4-2.3** | **The seeded *Community 25* booking has never existed.** Scenario 110 books it as `head.dev`, who holds no company-schedule permission, and `.catch(() => {})` swallows the 403. So a rebuild seeds **one** booking, not two, and chapter 8 was describing a row nobody could see. It also means that, as seeded, only the HR desk can book a room. |
| **R4-2.4** | **The demo database has no mail server** (`EmailSettings` is empty), so none of round 4's emails is delivered while every screen reads as sent (Rule 7, and **R4-6.3** for the product side). |

The other twenty-two are in the code, by chapter:

| | Chapter | Finding |
|---|---|---|
| **R4-3.1** | landing | My schedule opened the group to everybody; this page answers them *"Nothing scheduled"* on a 403 |
| **R4-5.1** | new event | *Send reminders* with *Days before* blank never sends, and says it will |
| **R4-6.1** | the event | the kept original is shown nowhere on the page |
| **R4-6.2** | the event | the Reminders card promises sends the sweep will not make |
| **R4-6.3** | the event | *Sent*, *reminded* and *chased* mean attempted |
| **R4-6.4** | the event | the emails ask for an answer and give no way to give one; a reschedule does not reset the answers |
| **R4-6.5** | the event | C-19 now emails — an API cancel of a completed event tells everybody |
| **R4-6.6** | every screen | eight Admin-only removes still offered to `hr.head` |
| **R4-6.7** | the event | an organiser who is not on the guest list is invisible to the diaries and the clash check |
| **R4-7.1** | edit | Edit moves an event with no original kept, no reason and no email |
| **R4-9.1** | book a room | the search offers rooms the booking will refuse |
| **R4-10A.1** | my schedule | a multi-day entry is filed under its first day only |
| **R4-10A.2** | my schedule, interviews | a timed event spanning days counts on its first day only — in the clash check too |
| **R4-10A.3** | my schedule | a failing source leaves the diary silently short |
| **R4-10A.4** | my schedule | closures and every calendar's holidays, active or not, are everybody's |
| **R4-10A.5** | my schedule | not browser-walked |
| **R4-10B.1** | team schedule | it reads; the plan's "schedule for the unit" half was not built |
| **R4-10B.2** | team schedule | each entry drawn on its start day; leave begun before the range not drawn at all |
| **R4-10B.3** | team schedule | any Write holder reads any unit; a head without Write cannot read their own |
| **R4-10B.4** | team schedule | a directorate is hundreds of rows |
| **R4-12.1** | rooms | a typed code once used by a deleted room is refused with a 500 |
| **R4-13.1** | closures | every closure is attributed to everybody, whatever its scope |

R4-10A.4 and R4-13.1 overlap, because their closure half is one defect seen from two screens. And
R4-10A.5 is a walk not yet done rather than a defect.

**Corrected in this rewrite** — five statements the 2026-09-17 guide made that the code or the
database contradicts, and none of them a round 4 change:
- **Chapter 6 approved the board meeting**, which has no Approve button: no seeded event requires
  approval. It now approves chapter 5's event.
- **Chapter 8 read two seeded bookings.** There is one (**R4-2.3**).
- **The fiscal tables** were said to live on the main context, and chapters 15 and 16 named
  `FiscalYears` / `FiscalPeriods`. Those are **Finance's**; HR's are the singular `FiscalYear` /
  `FiscalPeriod`.
- **§ 19's period-reopen SQL** therefore targeted Finance's table, where an HR period id matches
  nothing.
- **§ 19's stop-the-API command** filtered on a process name that never matches.

### The six that affect a demonstration

*As round 4 left them: see the rules above chapter 1, now eight.*

| # | Where | Finding | After round 4 |
|---|---|---|---|
| **C-1** | every screen | **`hr.head` cannot delete anything** — every delete is `HR.Company.Admin`, and the **event page carries a red Delete button in its header** | ◐ the header button and the bookings Delete hidden; eight others still 403 |
| **C-2** | event → Reschedule | **The dialog says the original dates are kept. They are overwritten** and nothing stores them | ◐ kept and emailed; not shown; Edit bypasses |
| **C-3** | events, bookings | **Approve is a flag, not the workflow engine** — no instance, no inbox entry, no assignee check, self-approval always allowed | unchanged |
| **C-4** | rooms, bookings | **Max booking duration and advance-booking days are recorded and never enforced.** Only *bookable* and *the time clash* are | ✅ enforced; the search lags |
| **C-5** | closures | **Business closures reach nothing** — no leave and no attendance code path reads them, despite the screen's own wording | ◐ the clash check and diaries read them; leave and attendance do not |
| **C-6** | events, bookings | **Numbers can repeat.** Both generators count live rows and neither index is unique, so a delete makes the next record reuse the number | ✅ fixed |

### The five worth fixing first

*Re-ranked after round 4. C-2 and C-6 were two of the original five; C-6 is done, and C-2 is two
small pieces from done.*

| # | Where | Finding | Why it is first |
|---|---|---|---|
| **C-5** | closures | **Wire closures into the leave day-counter and the attendance day-builder**, with the closure's scope. Both already exclude statutory holidays; this is the same join, and `is-closure-date` is already built for it — reuse its scope rule for **R4-13.1** at the same time | The feature is complete except for the two callers, and the screen still says it is wired |
| **C-19** | events | **No lifecycle guard on the server.** A completed event can be cancelled, a cancelled one completed, and either rescheduled. Every other HR module refuses these | Since round 4 each of those also **emails everybody invited** (**R4-6.5**) |
| **R4-10B.2** · **R4-10A.1** | the diaries | **Draw a commitment on every day it covers**, and keep entries that began before the range | A diary that shows a week's leave as one day, or not at all, answers "who is free?" wrongly |
| **R4-6.2** · **R4-6.3** | the event | **Make the Reminders card state the sweep's own conditions, and say when nothing could be delivered** | The card reads out promises and successes that did not happen, on the screen the demo spends longest on |
| **C-26 / C-35** | bookings, rooms | **Build the room view** — `bookings/room/{roomId}` already exists and there is nowhere in the product to see one room's week | The most-requested view of a booking register, one screen away |

Behind those, the cheap ones: **R4-6.6** (pass `allowRemove` eight times), **R4-6.1** (render four
fields the page already has), **R4-5.1** (make *Days before* required), **R4-9.1** (apply the same
rules in the search).

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
| C-17 | participants | ✅ ~~**No invitation is ever sent**~~ — **fixed in round 4**: the invitation is emailed on add (best effort; *Sent* either way, **R4-6.3**) |
| C-18 | attachments | **References, not files** — no upload, no download, no link to the document register |
| C-20 | events | Approve ignores `RequiresApproval` and confirms any open event |
| C-21 | attendance | An attendance record **cannot be removed** — no delete endpoint; a wrong row is corrected by marking again, leaving both |
| C-22 | participants | A participant **cannot be edited** — and removal is Admin, so `hr.head` cannot fix a typo in an external guest's email at all |
| C-23 | tasks | `EventTaskStatus.Overdue` is never set; a task past its due date stays *Not Started* |
| C-24 | events | The edit form's status dropdown accepts any transition, and setting *Cancelled* there does **not** set `IsCancelled`, the date or a reason. Since round 4 it also emails nobody, where the Cancel button does |
| C-25 | bookings | Unpaged and filtered client-side, with six unused server reads |
| C-27 | bookings | ✅ ~~**Capacity is a filter, not a rule**~~ — **fixed in round 4**: create and edit refuse it |
| C-28 | bookings | The availability repository query is **not tenant-filtered** before the service narrows the rooms. Harmless, and the only unscoped read in the module |
| C-29 | bookings | A linked event is a label — cancelling either does not touch the other |
| C-30 | bookings | Approve has no status guard on the server |
| C-31 | bookings | Cancel has none either; a cancelled booking can be cancelled again, overwriting the first reason |
| C-32 | bookings | Delete is on the register's row menu and not on the detail page — inconsistent, if convenient |
| C-33 | rooms | The delete failure toast blames bookings when the real answer is a 403 |
| C-34 | rooms | ✅ ~~A **generated** room code skips the duplicate check a typed one gets, and is count-based~~ — **fixed in round 4**: the shared sequence (**R4-12.1** for a typed code) |
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

*Added by round 4:*

- **The diary is the clash check's own code, not a copy of it.** "What is this person committed
  to?" is one question asked over an hour or a fortnight, so both read the same seven registered
  sources. The next kind of commitment the organisation tracks reaches both at once.
- **Send-once is a stamp on the record, not a hope about timing.** The hourly sweep and HR's buttons
  write the same date, a moved date clears it, and each event is saved as it is sent. So a pass
  that fails halfway costs only what it had not sent, and nobody is told twice.
- **Background email names the tenant it is for.** The sweep runs with nobody signed in, so every
  send carries the event's tenant, and the email is in that tenant's wording under its legal name.
- **The Delete was hidden without loosening the permission.** Whether HR may delete a company event
  is left to role setup, and the harness asserts the 403 is still there.

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
| 8A | `/hr/company-schedule/my-schedule` *(round 4)* | 10A | hr.head — or anybody; optionally a window as **staff** |
| 8B | `/hr/company-schedule/team` *(round 4)* | 10B | hr.head |
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
| **`HR.Company.Write`** | create and update an **event** · **approve** it · **cancel** it · **reschedule** it · **complete** it · add a **participant** · record an invitation **response** · mark **attendance** · **check out** · add / update / **complete** a **task** · add an **attachment** · create and update a **room** · create and update a **booking** · **approve** a booking · **cancel** a booking · create and update a **milestone**, a **closure**, a **fiscal year** and a **period** · **set the current fiscal year** · **close a period** · update the **company profile** · *round 4:* **send an event's reminder now** · **chase unanswered invitations now** · **run the reminder sweep now** (`reminders/run`) · read a unit's **team schedule** — Write, not Read, because it shows other people's leave and travel |
| *(no HR permission)* *— round 4* | **My schedule** (`my-schedule`): any signed-in internal user linked to an employee record reads **their own** diary. The employee comes from the token; there is no parameter for anybody else's |
| **`HR.Company.Admin`** — ***`hr.head` is refused*** | **every delete in the module**: events, participants, attachments, tasks, rooms, bookings, milestones, closures, fiscal years, fiscal periods. **Plus** — and this is the part that is not about destruction — **replacing or withdrawing the company seal**, and **changing the HR policy settings** (the procedural-absence threshold, FR-HR-092; the budget and establishment enforcement modes, FR-HR-136) |
| **`HR.Company.Approve`** | **nothing in this module.** Despite the name it is the interim authority over **team objectives and terms of reference** where no workflow definition is published. Company-schedule approvals are plain `Write` |

> **Two absences worth naming.** There is **no self-service write**. Round 4 added the module's first
> self-service screen, *My schedule*, and it only reads. An invitee still cannot answer their own
> invitation, because `participants/respond` takes a participant id and sits on the desk's Write
> policy, so it means *"HR records the response"*. Since round 4 that absence is visible to the
> invitee: the invitation email asks them to confirm and gives them no way to (**R4-6.4**). A genuine
> reply screen needs a self-or-permission check against the participant's own employee id first;
> that is the module's one recorded open decision (D-02). And there is **no approver tier** — see
> Rule 3.

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
| `docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-4-PLAN.md` | *(round 4)* Lanes D-1, D-2 and N-b2: the build, the decisions and the execution logs behind every round 4 change here — and P2b, this rewrite |
| `docs/HR/programme/HR-CONFIGURATION-REGISTER.md` | *(round 4)* § 2.7, the five emails' wording; § 2.8, the reminder sweep and the RSVP-chase lead, each proved in both positions |
| `docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md` § 9.4 | *(round 4)* The interview clash check that reads this module, and the interview that holds a room booking |
| `dev-harness/hr-company-schedule/run-round4-d.mjs` · `dev-harness/hr-templates/run-lane-nb2.mjs` | *(round 4)* 39 and 36 assertions: the numbers, the original window, the room rules, the hidden Delete, the diaries, the notices; the sweep, send-once, and the chase lead in both positions. ⚠ The first leaves its fixtures on the database (**R4-2.1**) |
| `dev-harness/hr-demo-smoke/scenarios/110-company-schedule.mjs` | Exactly what the demo database holds, and how to rebuild it. ⚠ Its second booking has never been created (**R4-2.3**) |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 7 is the three-step version of this guide, for the standard demo pack. ⚠ Its aside still says closures feed leave and attendance (Rule 5) |

---

*End of the HR Company Schedule System Guide.*
