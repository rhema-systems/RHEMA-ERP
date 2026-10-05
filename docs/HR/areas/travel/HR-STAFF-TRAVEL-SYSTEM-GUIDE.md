# HR Staff Travel — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source; **rewritten whole in the travel final closure's lane 10
(2026-10-04)** against what lanes 0–9 built — every chapter re-read in the code and against UAT's own rows, with the
walks forking where UAT (not re-run, D-61) differs from a database the demo pack builds today. Where this guide and
`HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` ever disagree, **the plan is right** — its § 3d gives every T-finding's state,
and each lane's *As built* what changed. It describes what the code does; where a screen offers something the server
refuses, the step says so.

**Scope:** the whole **Staff Travel** group of the HR sidebar (*Human Resources → Time & Leave → Staff
Travel*) — **ten menu items** — plus the screens that hang off them without a menu entry, the **two setup
areas** under Administration → HR → Travel, and the **six self-service screens** in the employee portal.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| 1 | Register | `/hr/travel` | 3 |
| 2 | Approvals | `/hr/travel/approvals` — every employee sees it; it lists only their own decisions | 3a |
| — | *(no menu entry)* Raise a request | `/hr/travel/new` | 4 |
| — | *(no menu entry)* The request, and its eight tabs | `/hr/travel/[id]` | 5 |
| — | *(no menu entry)* Amend a request | `/hr/travel/[id]/edit` | 6 |
| 3 | Group Travel | `/hr/travel/groups` (+ `[id]`) | 7 |
| 4 | Expense Claims | `/hr/travel/claims` | 8 |
| 5 | Advances | `/hr/travel/advances` | 8a |
| — | *(no menu entry)* The claim | `/hr/travel/claims/[id]` | 9 |
| — | *(no menu entry)* New claim | `/hr/travel/claims/new` | 9 |
| 6 | Policy Breaches | `/hr/travel/breaches` | 13a |
| 7 | Visa Requirements | `/hr/travel/visa-requirements` | 10 |
| 8 | Travel Documents | `/hr/travel/documents` | 10a |
| 9 | Destination Alerts | `/hr/travel/alerts` | 11 |
| 10 | Dashboard | `/hr/travel/dashboard` | 12 |
| — | *(Administration)* Travel Policies | `/administration/hr/travel/policies` (+ `new`, `[id]`) | 13 |
| — | *(Administration)* Travel Reminders | `/administration/hr/travel/reminders` | 14 |
| — | *(portal)* My Travel | `/me/travel` (+ `new`, `[id]`, `[id]/edit`, `claims/[id]`, `documents`) | 15 |

**Twenty-six pages** over **thirty-two tables**, with eight tabs inside a single travel request (four of
them — itinerary, bookings, finance, compliance — drawn for the desk only; an approver sees overview,
comments, attachments and workflow). This is the deepest module in
HR by nesting: a trip carries an itinerary carrying legs carrying activities, four kinds of booking, a
budget, advances, claims carrying lines, and eight kinds of compliance record.

> **One thing to know before you say a word about scope.** The signed SRS contains **no
> staff-travel requirement** — its single "travel" hit is a National Service allowance. This module
> was built as a full ported suite on a deliberate decision, with the gap on the table. So
> *"is it in the SRS?"* is not a usable question in this area, in either direction. If a stakeholder
> asks why travel is here, the honest answer is *"because a corporation that sends people to Lagos
> and London needs it, and it was built to the same standard as the rest even though the
> specification did not ask for it."*

---

## This document is two things at once

Like the recruitment, employees, leave, attendance and company-schedule guides, this is a
**reference** and a **script you can perform**. Every chapter has the same five parts, and you can
read only the ones you need:

| Part | Marked | Use it for |
|---|---|---|
| **Where you are** | 📍 | the sidebar path, the URL, which persona, how long |
| **What it is** | 📖 | one paragraph you could say to a non-technical room |
| **On the page** | 👁 | every control on the screen, exhaustively — nothing omitted |
| **Walk it** | ▶ | numbered steps: click this, expect that, say this |
| **Behind the page** | ⚙ | endpoint → service → table, and the permission that gates it |

Three more markers appear inside the walks:

- **🔴 LIVE WRITE** — this step changes real data. Every one is numbered
  (`LIVE WRITE 1` … `13`), and chapter 17 tells you how to undo each.
- **⚠ CAREFUL** — a way this step goes wrong in front of people, and what to do instead.
- **🚫 DO NOT PRESS** — a control that renders for your persona and returns 403, or one that
  writes something you cannot undo inside a demo.

**Say-lines are in quotation marks and indented.** They are written to be read aloud more or less
as they stand. Change the names, keep the order of the ideas — the order is doing the work.

---

## Before anything else: the seven rules that decide whether this demo works

Travel is the most *impressive* module in HR. It has genuine depth, real enforcement and a money chain that ends
in a bank transfer, and it has seven behaviours that will catch you out live. Read these twice. They replace
the six of the first edition (2026-09-17): its Rules 3 and 5 are gone (fixed), and Rules 1, 2 and 6 changed.

### Rule 1 — Find out whether the travel policy is in force before you promise a cap

The policy is the module's best story, and **whether it binds depends on the database you are on.**

A policy is a draft until a travel administrator approves it, and **a draft binds nothing**. The guard reads
only an approved policy. Look before you speak: *Administration → HR → Travel → Policies*, the **State** column:

| State | What it means for the demo |
|---|---|
| **Draft — not enforcing** | No cap binds. A First-class fare and a GHS 9,000-a-night hotel are accepted without a murmur. **This is UAT today** — its *TDC Staff Travel Policy 2026* was drafted and never approved, so no trip was checked against it (Lagos and London carry it only because they were linked by hand before lane 1) |
| **In force** | The caps bind (§ 1.5). **This is a demo database built by the pack since 2026-10-04** (closure lane 10, D-60) — `hr.officer` approved `hr.head`'s draft before the trips were submitted |
| **Approved — in force from …** | approved to start on a later date; the version before it governs until the day before |
| **Not in force** / **Expired** | superseded by a later version, or past its end date |

**When it is in force, it binds at five moments** (closure lane 1 and lane 4, D-1):

- **at submission** — the trip's estimated cost against the single-trip limit, and the trip **records the
  policy it was checked against**;
- **at booking** — a cabin class and a hotel rate against their caps, and the advance-booking notice (21 days
  for a flight, 14 for a hotel on the demo's policy);
- **at booking, when the policy makes preferred vendors mandatory** — a booking names a supplier;
- **at claim submission** — a receipt for every non-per-diem line above the threshold (GHS 100 on the demo's
  policy), and the claim window (14 days after the trip ends). These two follow the policy **the trip recorded
  at submission**: a trip submitted under no policy is held to none, whatever is approved later;
- **a breach is not refused outright** if the booker asks for an exception with a reason. The booking is
  saved **Pending**, and nobody can confirm or ticket it until a **different** travel administrator
  authorises it on *Staff Travel → Policy Breaches* (Rule 2, D-8).

So on UAT, do not offer to show a cap refusing something unless § 2.4's preparation has been done; the
*Draft — not enforcing* badge is the honest hook (chapter 13). On a freshly built demo database, the London
trip's Hilton (3,200 a night against a 2,400 ceiling) **is** the cap at work: booked Pending, authorised by
`hr.officer`, then confirmed. Finding **T-1** — open on UAT by choice (D-61), closed on a rebuilt demo.

### Rule 2 — HR administers travel, but nobody authorises what they did themselves

Since closure lane 4 (slice 4c, D-3) the HR role holds **`HR.Travel.Admin`**. The separation that used to come
from the role comes from **the act**: each authority is refused to whoever did the thing it checks. Every
refusal names its reason, so pressing the button is safe.

| Act | Tier | Refused to |
|---|---|---|
| **Approve a travel policy** — which is what makes it bind at all | **Admin** | whoever drafted or last changed it |
| **Withdraw a travel policy** | **Admin** | — |
| **Authorise or refuse a booking's policy exception** (*Policy Breaches*, D-8) | **Admin** | whoever booked it, whoever asked for the exception, and the traveller |
| **Decide a policy exception** (the older register, no screen — Rule 4) | **Admin** | whoever raised it |
| **Approve a trip's budget** | **Admin** | the traveller, and whoever set or last changed the budget (D-19) |
| **Write off an advance** | **Admin** | the advance's own traveller |
| **Void a claim's payment** | **Admin** | the claimant, and whoever paid it |
| **The Reminders screen, reads included** | **Admin** | — *(the desk that renews a passport sees the queue, T-52)* |
| **Delete** | **Admin**, and only while the record is still a draft of itself | see below |
| **Edit a comment** | its **author** only (D-21) | everyone else — deleting one is its author's or an administrator's |

**What cannot be deleted at all, whoever asks** (closure lanes 1, 4, 5, 7 — a deletion would erase a record
someone relied on):

- a travel request that is not a **Draft** — cancel it instead;
- a booking that is not **Pending** — cancel it, so the record of what was booked stays; and a flight or hotel
  carrying a policy exception (pending, authorised or refused), at any status — it is the breach register;
- the itinerary version **in force** (a superseded one can be removed);
- a **verified** travel document, an **acknowledged** risk assessment, an alert that has been **sent**.

Two more rules sit on the ordinary **Write** tier, where no administrator is involved:

- **Nobody decides their own trip** — at either approval stage (D-7). `hr.head`'s London trip is approved by
  her line authority and then by `hr.officer`.
- **Money needs two people** (D-2, D-16): whoever approves an advance does not pay it out; whoever reviewed a
  claim — or any of its lines — does not pay it; nobody approves, pays or is paid their own. Rule 5.

> **`HR.Travel.Admin` is a financial authority here, not a housekeeping one.** It decides what the
> organisation may spend on travel and who may exceed it. Holding it, an HR officer still cannot
> sign a policy they wrote, wave through their own booking's breach or approve a budget they set.
> **A second HR officer does** — on the demo, `hr.officer` for what `hr.head` did.

### Rule 3 — A trip is approved in two stages, and two of UAT's trips are on the old route

Since closure lane 2 (D-7) a trip goes first to the **traveller's line authority** — addressed by name to their
supervisor and the head of their unit or a unit above it, whoever can sign in — and then to **HR**, who sets the
approved budget. Approve, Reject and Return appear only for whoever may decide at the stage the trip is on;
everyone else sees *"Waiting for …"* (§ 1.6).

On **UAT**, the two trips still awaiting approval — **Lagos (TR-2026-00002)** and **London (TR-2026-00003)** —
were submitted before the change and stay on the **one-step route** they were submitted on: HR decides them in
one step. A trip submitted **today**, or on a freshly built demo database, takes both stages. Know which you
are showing before you say how many signatures it needs.

### Rule 4 — The policy's rule register is read-only, and that is a decision, not an omission

Open a policy and you will find a **Rules** table you cannot edit. The API carries create, update and delete
for rules; they are deliberately not wired to a screen, because **`StaffTravelPolicyRule` is enforced by
nothing**. The guard binds on the policy's own fields (§ 1.5); no code path reads a *rule*, so `ruleType`,
`limitValue` and `violationAction` describe a mechanism that does not run.

The reasoning is worth saying out loud, because it is a good answer: *an editable control that does nothing
creates false assurance, and that is worse than no control.* A rule set to **Block** is a promise to whoever
configured it. Finding **T-4**, recorded as a decision (finish plan D-29), not a defect.

Keep two things apart when someone asks about "exceptions":

- a **booking exception** (D-8) — a booking above a cap, saved Pending until a second administrator authorises
  it. It has a screen, *Staff Travel → Policy Breaches*, and it binds;
- a **policy exception** (`StaffTravelPolicyException`) — an older register of a recorded breach and its
  decision, kept and decided by an administrator who did not raise it, with **no screen** (T-49).

### Rule 5 — Money takes two people, and payroll is not one of the ways to pay

Since closure lane 3 (D-2, D-16) the money chain cannot be run by one person:

- an **advance** is requested, approved by one officer and **paid out by another**; it is raised only on an
  approved or under-way trip (money after the trip is a claim);
- a **claim** is reviewed line by line, then approved, then **paid by someone who reviewed neither the claim nor
  any of its lines**; nobody reviews or pays their own;
- **Payroll offset is gone** from the pay dialog and refused by the server (D-10): payroll cannot receive a
  claim, so a claim "paid" that way reached nobody. The question is with the payroll owner
  (`docs/HR/integration/handoffs/HANDOFF-PAYROLL-TRAVEL-CLAIMS.md`).

**For the demo:** `hr.head` cannot both review and pay a claim, nor approve and pay out an advance. The
second HR officer, **`hr.officer`**, does the other half. A walk that has one persona do both will be
refused at the second step, with the reason on screen.

*The currency footnote that used to be a rule.* Finance's conversion was fixed on 2026-09-10, and travel now
converts at **Finance's rate for the day**: each expense at its own date's rate (B12), a foreign advance at the
payment day's rate (D-15), the hotel cap in the policy's own currency. Only the **dashboard** and the claims
queue still refuse to add totals in different currencies — on purpose; they show one row per currency.

### Rule 6 — The advance is recovered when the claim is **paid**, not when it is approved

An employee who took a GHS 2,500 advance and then claimed GHS 2,500 of expenses used to be **paid twice**. It
is fixed, and the fix has a shape you need to know before you open the pay dialog:

- recovery happens **inside the pay call**, not on review or approval;
- so until you press **Record payment**, the claim's payable still reads as the **full approved amount**;
- the dialog therefore **does not show the payable figure** — it shows *Approved*, *Less advance*, *To pay*,
  computed with the server's own `min(outstanding, approved)` rule and labelled **anticipated**;
- if the traveller still holds advance cash on the trip that this claim does not name, paying in full needs a
  reason in the **waiver** box (O-2);
- an advance in another currency is valued at **the payment day's rate** (D-15); whatever a rate movement
  leaves on it is refunded or written off;
- **Void payment** undoes the payment *and* the recovery (T-39) — no SQL needed to take a payment back.

If you quote the claim's *Payable* figure and then pay it, the number on screen will change and you will look
wrong. Read the dialog, not the table. Finding **T-6** — fixed properly; the two screens disagree only until
the moment of payment.

### Rule 7 — Trips move by themselves, once a night

Since closure lane 8 (slice 8c) the **nightly travel sweep** runs **11 minutes after the API starts**, then
daily, and it changes what you will find:

- an approved trip goes **under way** on its departure date (or when Fleet dispatches its company vehicle);
- a trip is **completed** the day after it ends, and its traveller is told the last day to claim;
- a completed trip is **closed** once its claim window has passed and nothing is left open;
- every approved trip's working days are kept on the traveller's **attendance** as *On duty* (lane 9a).

So a demo database started for the first time moves its past trips at once: on a freshly built one, **Sebrepor
is completed** within minutes. Rehearse on the database you will present, after it has been running for a
quarter of an hour, not straight after a rebuild.

---

## Conventions

**Routes.** `/hr/travel/[id]` is the file `frontend/src/app/hr/travel/[id]/page.tsx`. A segment in
square brackets is a parameter.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping in the solution. A table is
named after its `DbSet<>` property in `ApplicationDbContext.HR.cs` — `StaffTravelRequest` lives in
`StaffTravelRequests`, `StaffTravelFlightBooking` in `StaffTravelFlightBookings`.

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
| `HR.Travel.Read` | every read — requests, itineraries, bookings, advances, claims, travel documents, visa requirements, health requirements, alerts, insurance, risk assessments and policies | **yes** |
| `HR.Travel.Write` | raise and amend travel on behalf of staff; make and change every kind of booking; build itineraries; set budgets; request, approve and disburse advances; create, submit, review and **pay** expense claims; record documents, visas, insurance, risk assessments and alerts; author policies and their rules — **never on one's own trip, and never both halves of a payment** (D-7, D-2, D-16 — Rules 2 and 5) | **yes** |
| `HR.Travel.Admin` | **delete anything**; **approve and withdraw a travel policy**; **authorise a booking above the policy cap**; decide a policy exception; write off an advance; approve a budget; void a payment; maintain per-diem rates' deletion; **the whole Reminders screen, reads included** — each refused to whoever did the act it checks (Rule 2) | **yes**, since closure lane 4 (D-3) — **no** before |
| `HR.Travel.Approve` | interim authority to approve a travel request **where no workflow definition is published**. A `STAFF_TRAVEL_REQUEST` definition **is** seeded and published, so in practice this does nothing | **yes** |

**Self-access is a separate controller, not a permission.** `api/staff-travel/me` is gated only on
*signed-in-and-internal*. It takes **no employee id anywhere** — the traveller is the token — and a
404 from it means *"not yours"*, not *"deleted"*.

**The actor is always the token.** The initiator of a request, the approver, the canceller, the
person who verified a document, reviewed a claim, approved an advance, assessed a destination and
acknowledged an alert are all taken from `CurrentUser`. **None of those is a form field.** Around
twenty such holes were closed across this area; every *"who did this"* used to be read from the
request body.

> ⚠ **A permission an unlinked account holds is a permission it cannot exercise.** Three Admin
> writes here stamp an **Employee** foreign key. `admin` is SuperAdmin, so it holds
> `HR.Travel.Admin` — and it is not employee-linked, so it cannot approve a policy. Since closure
> lane 4 the HR desk holds Admin and is employee-linked, so this now matters only for platform
> accounts. Whether TDC's travel administrators are real employees or service accounts is still a
> **seeding** question, not a code one.

---

## 1. How staff travel hangs together

### 1.1 One request, six satellites

Recruitment is a chain, employees a hub, leave a ledger, attendance a pipeline, the company
schedule two registers. **Travel is one root aggregate with six things hanging off it**, and
everything traces back to the request.

```
 ┌──────────── THE RULEBOOK (Administration → HR → Travel) ───────────────────┐
 │  StaffTravelPolicy   binds only once APPROVED — Rule 1; dated versions,    │
 │    a unit's policy covers the units under it; limits in its own currency   │
 │    max cabin class domestic / international                                │
 │    max hotel rate  domestic / international                                │
 │    advance-booking days flight / hotel · preferred vendor mandatory        │
 │    max single trip · receipt required above · claim window (days)          │
 │      └── StaffTravelPolicyRule  (read-only; enforced by NOTHING — Rule 4)  │
 │  StaffTravelPerDiemRate — per country/city: daily, meals, incidentals      │
 └──────────────────────────────┬─────────────────────────────────────────────┘
                                │  caps and rates
                                ▼
 ┌──────────── THE REQUEST — StaffTravelRequest  TR-2026-00002 ───────────────┐
 │  traveller · initiator + the ROLE they raised it under · type · purpose    │
 │  origin & destination (country + city) · dates · duration                  │
 │  estimated cost + currency · approved budget                               │
 │  international? · visa? · health clearance? · risk level · priority        │
 │                                                                            │
 │  Draft → Submitted → Approved → InProgress → Completed → Closed            │
 │       ↘ ReturnedForRevision   ↘ Rejected   ↘ Cancelled                     │
 │                                                                            │
 │  ⚙ Submit / Approve / Reject run on the WORKFLOW ENGINE — two stages § 1.6 │
 │  ⚙ submission records the POLICY it was checked against                    │
 │  ⚙ approved: its working days on the traveller's ATTENDANCE as On duty     │
 └──┬──────┬──────┬──────┬──────┬──────┬────────────────────────────────────┬─┘
    │      │      │      │      │      │                                    │
    ▼      ▼      ▼      ▼      ▼      ▼                                    ▼
 ITINERARY  BOOKINGS   FINANCE   COMPLIANCE   COMMENTS   ATTACHMENTS    GROUP
    │          │          │           │                                    │
    │          │          │           ├── StaffTravelDocument (passport…)   │
    │          │          │           ├── StaffTravelVisaRequirement        │
    │          │          │           ├── StaffTravelVisaApplication        │
    │          │          │           ├── StaffTravelHealthRequirement      │
    │          │          │           │     └── StaffTravelHealthClearance  │
    │          │          │           │         (ticked per trip — 7b)      │
    │          │          │           ├── StaffTravelInsurancePolicy        │
    │          │          │           ├── StaffTravelRiskAssessment         │
    │          │          │           └── StaffTravelAlert → Notification   │
    │          │          │
    │          │          ├── StaffTravelBudget   (committed / actual /
    │          │          │      variance — DERIVED, recomputed on read)
    │          │          ├── StaffTravelAdvance  Requested → Approved →
    │          │          │      Disbursed → PartiallySettled → FullySettled
    │          │          │      (or Rejected · Cancelled · Overdue · WrittenOff;
    │          │          │       two people: approver ≠ payer — Rule 5)
    │          │          └── StaffTravelExpenseClaim
    │          │                 └── StaffTravelExpenseClaimLine
    │          │                 Draft → Submitted → UnderReview →
    │          │                 Approved / PartiallyApproved → Paid
    │          │                              ⛔ the advance is recovered HERE
    │          │
    │          ├── StaffTravelFlightBooking → StaffTravelFlightSegment
    │          ├── StaffTravelHotelBooking       ⛔ over a cap: Pending until a
    │          │                                    SECOND admin authorises (D-8)
    │          ├── StaffTravelGroundTransport
    │          └── StaffTravelCarRentalBooking
    │
    └── StaffTravelItinerary (versioned) → Leg → Activity
```

Four points follow, and they are the four worth landing in a room:

**1. The request is the spine and nothing escapes it.** A flight, a hotel, an advance, a visa
application, an insurance certificate and a security briefing all carry the request's id. Ask *"what
did the Lagos trip cost us, and what did we have to do to send him?"* and there is one place to
look.

**2. There are exactly two places money leaves the organisation, and they are linked.** An
**advance** is cash before the trip; a **claim** is reimbursement after it. The second recovers the
first automatically, at the moment of payment. That link is the single most valuable thing in this
module and it is the one that was broken (Rule 6).

**3. The policy is a control, not a document.** Once approved it binds at submission, at booking and
at the claim, and a breach waits for a second officer (Rule 1). Whether it is *in force* is a separate
question from whether it *exists* — which is Rule 1 and chapter 13.

**4. Compliance is per destination, then ticked per trip.** Visa requirements, health requirements and
destination alerts are recorded against a **country**, and the trip's Compliance tab resolves them
for this traveller's passport and this destination. Record them once; every future trip there
inherits them. What is per trip is the evidence: the desk ticks each health requirement as seen for this
trip (lane 7b), and the visa register sets whether this trip needs a visa (lane 7, D-39).

### 1.2 The tables

Thirty-two, in the order the module uses them. (Travel also writes into one table it does not own:
`StaffDailyAttendances`, whose `StaffTravelRequestId` marks the days an approved trip holds as *On duty* —
lane 9a.)

| # | Table | One line |
|---|---|---|
| 1 | `StaffTravelRequests` | **The root.** Everything traces back here |
| 2 | `StaffGroupTravels` | Several people travelling to one place for one thing |
| 3 | `StaffTravelRequestComments` | Threaded notes, each marked visible to the traveller or internal |
| 4 | `StaffTravelRequestAttachments` | Invitations, brochures, receipts — through the controlled upload gate |
| 5 | `StaffTravelItineraries` | A **versioned** programme; one is current |
| 6 | `StaffTravelItineraryLegs` | One movement or stay within it |
| 7 | `StaffTravelItineraryActivities` | What happens on a leg — a meeting, a site visit, with a contact |
| 8 | `StaffTravelFlightBookings` | A PNR, an airline, a cabin class, a fare |
| 9 | `StaffTravelFlightSegments` | Each flight within it, with terminals, seat and baggage |
| 10 | `StaffTravelHotelBookings` | Hotel, nights, rate per night, cancellation policy |
| 11 | `StaffTravelGroundTransports` | Taxi, bus, train, private hire, shuttle |
| 12 | `StaffTravelCarRentalBookings` | Vehicle category, daily rate, fuel policy |
| 13 | `StaffTravelBudgets` | The approved envelope, split by line. **Committed/actual/variance are derived** |
| 14 | `StaffTravelAdvances` | Cash before the trip, and its settlement |
| 15 | `StaffTravelExpenseClaims` | Reimbursement after it |
| 16 | `StaffTravelExpenseClaimLines` | One expense, with a receipt reference and a per-diem link |
| 17 | `StaffTravelPerDiemRates` | Daily allowance by country and city, split by meal |
| 18 | `StaffTravelPolicies` | The caps — **binding only once approved**; dated versions per scope, each in its own currency |
| 19 | `StaffTravelPolicyRules` | The rule register — **read-only, enforced by nothing** |
| 20 | `StaffTravelPolicyExceptions` | The older register of a recorded breach and its decision (no screen — Rule 4). A *booking's* breach lives on the booking itself: its exception state, who asked, who authorised (D-8) |
| 21 | `StaffTravelDocuments` | Passports, driving licences, permits — with expiry |
| 22 | `StaffTravelVisaRequirements` | Passport country → destination country: what is needed |
| 23 | `StaffTravelVisaApplications` | One application, for one trip |
| 24 | `StaffTravelRiskAssessments` | The destination's risk, its mitigation and the traveller's acknowledgement |
| 25 | `StaffTravelAlerts` | A time-boxed advisory against a country and city |
| 26 | `StaffTravelAlertNotifications` | That alert, sent to one traveller, and their acknowledgement |
| 27 | `StaffTravelInsurancePolicies` | Cover for one trip |
| 28 | `StaffTravelHealthRequirements` | Vaccinations and clearances by destination |
| 29 | `StaffTravelHealthClearances` | One requirement ticked as seen for one trip — who, when, a note (lane 7b, D-36) |
| 30 | `StaffTravelReminderRuns` | One sweep of the reminder engine |
| 31 | `StaffTravelReminderDispatchLogs` | What that sweep sent or moved, to whom — and, since lane 8c, each trip it moved |
| 32 | *(shared)* `NumberSequences` | Where `TR-2026-00002` comes from |

### 1.3 The vocabularies

Nine enum families. Three matter; the rest are look-ups. Since closure lane 0, every list a screen offers
is built from these enums, so no form offers a value the server refuses (the first edition's Rule 3).

**Request status** — nine values, and the lifecycle is the module's spine:

| Value | Means | Set by |
|---|---|---|
| `Draft` | being written | create; **Recall** returns a submitted request here |
| `Submitted` | out for approval | **Submit**, via the workflow engine |
| `ReturnedForRevision` | sent back to the requester | **Return for revision** (the approver) or **Request change** (an approved trip) — since closure lane 1, 2026-10-02; nothing wrote it before |
| `Approved` | authorised to travel | the engine |
| `Rejected` | refused | the engine |
| `InProgress` | under way | **the nightly sweep**, on the departure date — or before it, once Fleet dispatches one of the trip's company vehicles (since closure lane 8, slice 8c; nothing set it before — T-7) |
| `Completed` | the trip has happened | **Mark completed** (not before the trip starts, since lane 1); **the nightly sweep** the day after it ends (lane 8, D-47), telling the traveller the last day to claim |
| `Closed` | finalised | **Close trip** — once every claim is paid or rejected and every advance settled (since lane 1); **the nightly sweep** too, once the claim window has also passed (lane 8, D-51 — the policy's window, or 30 days with none) |
| `Cancelled` | called off, with a reason | **Cancel** (not once under way, since lane 1) — except the desk's **Did not travel**: an under-way trip that did not happen, until its end date and while nothing was spent on it (lane 8, D-48) |

**Claim status** — eight: Draft · Submitted · UnderReview · Approved · **PartiallyApproved** ·
Rejected · **Paid** · Returned. *PartiallyApproved* is the one people ask about: it means some lines
were approved and some rejected, and the claim is still payable.

**Advance status** — nine: Requested → Approved → Disbursed → PartiallySettled → FullySettled, plus
**Overdue** (past its settlement deadline with cash still out), **WrittenOff** (an administrator's, never the
traveller's own), **Rejected** and **Cancelled** (closure lane 3 — an advance not yet paid out is withdrawn
with its trip). An advance a leaver's final settlement recovered is fully settled when the settlement is
released (lane 9c, D-58).

The rest, for reference:

| Family | Values |
|---|---|
| **Travel type** | Domestic · International · CrossBorder · Regional · OverseasAssignment · FieldVisit · Training · Conference · ClientVisit · GovernmentDuty · Emergency *(all eleven on the form since lane 0)* |
| **Purpose** | BusinessDevelopment · ClientMeeting · Conference · Training · Audit · Inspection · ProjectWork · SiteVisit · GovernmentEngagement · PersonalCombined · Emergency · Other |
| **Priority** | Routine · Urgent · Emergency |
| **Risk level** | Low · Medium · High · Critical · Prohibited — a trip rated or assessed **Prohibited** is not submitted or approved (lane 4, C5); a **Critical** one's flight is not ticketed until it has a risk assessment the traveller has acknowledged on their portal (lane 7, D-18) |
| **Initiator role** | Employee · Manager · HrAdmin · TravelDesk · System |
| **Booking status** | Pending · Confirmed · Ticketed · Cancelled · Refunded · NoShow · Completed · OnHold |
| **Cabin class** | Economy(1) · PremiumEconomy(2) · Business(3) · First(4) — ⚠ **the numbering is load-bearing**; the cap comparison is numeric |
| **Alert severity** | Info · Warning · Critical · Emergency |
| **Group status** | Planning · Open · Closed · InProgress · Completed · Cancelled |

### 1.4 The money — the arithmetic

Four figures get quoted and they are computed differently. Get these right and nothing can trip you.

**① The budget's committed, actual and variance.**

```
    Committed = Σ the trip's bookings (flights + hotels + ground + car rentals), Pending and OnHold included,
                no-shows left out, a cancelled or refunded flight's or hotel's cancellation FEE kept,
                + the company vehicle's cost from Fleet's own cost entries (lane 6)
    Actual    = Σ PAID claims' net payable
              + advances paid out, less cash handed back      (lane 3, B10 — T-22)
    Variance  = ApprovedTotal − Actual
```

All three are **derived and recomputed on read**, in the **budget's currency** — the trip's, set by the
server — each figure converted at Finance's rate on its own date (a booking when made, a claim when paid, an
advance when paid out). A claim against an advance pays only the balance, so nothing is counted twice. An
overrun is **flagged, not refused** (whether it should refuse is TDC's question). A budget exists only once the
trip is approved, its parts add up to its total, and its total cannot exceed the trip's approved budget.
Nothing types these numbers.

**② What a claim is worth.**

```
    TotalClaimed  = Σ line.AmountOriginal (converted to the claim currency)
    TotalApproved = Σ approved line amounts
    NetPayable    = TotalApproved − AdvanceDeducted
```

⚠ `AdvanceDeducted` is **zero until the claim is paid** — Rule 6.

**③ The advance recovery, at the moment of payment.**

```
    recovered = min(advance.Outstanding, claim.TotalApproved)
    paid      = claim.TotalApproved − recovered
```

And the advance moves `Disbursed → PartiallySettled` or `→ FullySettled` accordingly. An advance in another
currency is valued at **Finance's rate on the payment day** (D-15), the deduction worked in the advance's own
currency. **Void payment** puts all of it back (T-39).

**④ The dashboard's cost totals — deliberately not one number.** Grouped by currency, never added across
currencies (Rule 5's footnote).

### 1.5 What the travel policy decides, and what it does not

This is the honest slide, and since closure lane 4 it is a *strong* one: every setting on the form binds,
and the two that could not were taken off the form (D-1). All of it applies only to an **approved** policy
(Rule 1).

| Setting on a policy | **Binds** | When, and what happens |
|---|---|---|
| **Max flight class**, domestic / international | ✅ | at booking. A cabin above the cap is refused — or, with an exception asked and a reason, saved **Pending** until a different travel administrator authorises it (D-8). Domestic or international is the **trip's** own, worked out by the server (lane 1) |
| **Max hotel rate**, domestic / international | ✅ | at booking, the same way — compared **in the policy's currency**, a rate in another currency converted at Finance's rate (lane 4; T-9 fixed) |
| **Advance booking days**, flight / hotel | ✅ | at booking — a booking made with less notice is a breach, excepted the same way (D-1, D-8) |
| **Preferred vendor mandatory** | ✅ | at booking — the booking must name a supplier from Procurement's register; every booking form has the field (D-1; T-8 fixed) |
| **Max single trip budget** | ✅ | at submission — the trip's estimated cost; and HR's approved budget cannot exceed it (D-1, lane 2) |
| **Receipt required above** | ✅ | at claim submission — every line above it, per-diem lines aside, needs a receipt (D-1) |
| **Expense submission days** | ✅ | at claim submission — the claim window after the trip ends; the nightly sweep also uses it to close a trip (D-1, D-51) |
| *(which policy applies)* | ✅ | the traveller's own unit's, else the nearest unit above it, else the organisation's (O-5); among a scope's versions, the one in force on the departure date (O-4) |
| *(is it in force)* | ✅ | only once approved — **by someone other than its author** (C3) |
| ~~Requires cheapest fare~~ · ~~Max annual travel budget~~ | — | **dropped** from the form and the API: nothing could enforce them (D-1; the columns stay) |
| **Every rule in the rule register** | ❌ | read-only, by decision — Rule 4 |

Two footnotes that matter to an auditor:

> **Claims follow the policy the trip recorded at submission**, not the one in force when the claim is filed.
> A trip submitted under no approved policy is held to no receipt rule and no claim window, whatever is
> approved later. That is why the demo pack approves its policy before it submits the trips (lane 10, D-60).

> **No policy means no cap, and that is deliberate.** A tenant that has configured no travel policy
> is not thereby forbidden from booking travel. Refusing every booking until someone writes a
> policy would make the area unusable on a fresh tenant — which is how unmaintained reference data
> has turned a rule into an obstacle elsewhere in HR.

### 1.6 Where the approval actually happens

**On the workflow engine**, unlike the company schedule and like leave, attendance and recruitment.
Travel's own bespoke approval chain was retired.

> **Closure lane 2 (2026-10-02) made this two stages** — read this table, not the one-step story the
> rest of this guide was walked against. A request submitted before the change stays on the one-step
> route it was submitted on (UAT's demo *TR-2026-00002* and *-00003*) and is decided as before.

| | |
|---|---|
| Entity type | `StaffTravelRequest` |
| Definition seeded? | ✅ *Staff Travel Approval*, version 2 — `Draft → Line manager approval → HR approval → Approved` |
| Stage 1 — *Line manager approval* | **addressed by name** to the traveller's two nearest line authorities who can sign in: their supervisor, then the head of their unit and of each unit above. Only they are told, see it in their inbox and may decide it — no Manager role or travel permission needed. With nobody in the line able to sign in, it falls to **HR**, and the request gains an internal note saying why |
| Stage 2 — *HR approval* | roles **HR** and **TenantAdmin**; the **last** stage, so HR sets the **approved budget** here (more than zero, within the policy's single-trip limit) |
| Prevents self-approval? | **Yes, in the service** — nobody decides their own trip on either stage; the engine's initiator bar stays off so the desk can decide trips it raised for others |
| Editing the route | Allowed in the workflow designer (`/administration/workflow`): it keeps stage 1's named approvers and its HR fallback (cross-module defect #34, fixed by HR in lane 2). A stage added in between — Finance, say — is a middle stage: the budget stays with whoever approves **last** |

Six things follow (since closure lane 2):

1. **`status` says only which phase the request is in.** *Submitted* means "out for approval";
   **which stage it sits on is the workflow instance's business** — the page's banner names it, and
   the **Workflow** tab is authoritative.
2. **Nothing on the page ever writes a status.** It sends the decision and refetches rather than
   assuming an outcome.
3. **No approver id is sent.** The server resolves the approver from the token.
4. **Approve, Reject and Return for revision are the server's answer, not a permission.** The page
   asks *viewer actions* who may decide at this stage and draws the three buttons only for them — the
   traveller's line manager at stage 1 (the travel desk there only when nobody in the line can sign
   in), HR at stage 2. Everyone else sees whose decision it is: *"Waiting for Kojo Fiadzo (supervisor)
   or …"*.
5. **The approve dialog asks for the approved budget at the last stage only**, prefilled with the
   estimate (**T-10 fixed**). An earlier stage approves without one and is told who sets it.
6. **Two states can be submitted from:** `Draft` and `ReturnedForRevision`.
7. **Decide on the trip page or the Approvals queue, never the generic workflow inbox** (`/workflow/inbox`).
   That inbox completes the engine's step without travel's service: the trip stays *Submitted*, skips the
   line-authority and own-trip checks, and posts no attendance days or notices. Cross-module defect **#15** —
   the platform's, not travel's.

**The line manager's way in.** *Human Resources → Time & Leave → Staff Travel → Approvals* lists what waits for the
signed-in person — every employee sees the entry, and it lists only their own work. A line manager
with no travel permission opens the request from there or from their inbox and sees the trip, its
comments and attachments and their decision; the desk's tabs (itinerary, bookings, finance,
compliance) and its Edit, Cancel and Complete buttons are not drawn for them.

---

## 2. Before the room fills — the prep

**Time needed: 20 minutes the evening before, plus 5 on the morning.** Most of this module needs no
preparation at all — the two demo scenarios build a genuinely complete travel office. What needs
deciding is which database you are on, because **UAT and a rebuilt demo database differ** (closure lane 10, D-61):
UAT was seeded before the closure and has not been re-run; a database the pack builds today has the policy in
force and the money and approvals done the new way. Every walk in this book says where the two part.

### 2.1 What the demo database already holds

Two scenario modules build it: `080-travel.mjs` (the request and the money) and
`081-travel-logistics.mjs` (the trip itself). Between them:

**Four travel requests, and exactly four** — the runbook reads the register out loud, so nothing
may mint a fifth:

| # | Trip | Traveller | On UAT | On a rebuilt demo | What it is for |
|---|---|---|---|---|---|
| 1 | **Kumasi** — Ghana Institution of Engineers conference, presenting the Community 25 drainage design *(UAT: 19–20 Oct)* | `head.dev` | **Approved**, under no policy | **Approved**, under the policy | the **advance** story: GHS 2,500 requested → approved → **paid out** by another officer |
| 2 | **Lagos** — Free Zone housing scheme study tour *(9–13 Nov)* | `gm.ops` | **Submitted**, on the one-step route (Rule 3); its flight and hotel booked before lane 5 | **Submitted**, two-stage; no bookings (D-23); yellow fever ticked | the **depth** story: group travel, a 3-leg itinerary with activities, insurance, a medium-risk assessment, a live alert |
| 3 | **London** — CIPD Africa HR Summit *(14–18 Dec)* | `hr.head` | **Submitted**, one-step; BA flight **Ticketed** (before the visa rule); Hilton with no exception state | **Approved** — `hr.officer` gives HR's approval, since `hr.head` travels; BA **Confirmed**, its ticket refused until the visa; the Hilton through D-8 | the **compliance** story: a visa application, a policy exception, an over-cap hotel, a car rental, priority Emergency |
| 4 | **Sebrepor** — site handover with the contractor *(17 Sep)* | `staff` | **Completed** by the sweep, under no policy | **Approved**, moved on by the sweep | the **claim** story: an expense claim with 3 lines and a real receipt, submitted and awaiting review |

*A rebuilt database's dates are set relative to the day it was built.*

⚠ **The nightly sweep moves these by their dates** (Rule 7). A trip whose departure date has come goes **under way**;
one that has ended is **completed** the day after, and its traveller is told the last day to claim. So **Sebrepor
reads Completed** from the first sweep after the deploy (11 minutes after the API starts), with its claim still
Submitted; it closes only once that claim is paid or rejected and its claim window has passed. Kumasi goes under way on
its departure date. Say *"the sweep moved it"*, not *"the seed is wrong"*.

**And around them:**

| Table | What is there |
|---|---|
| `StaffTravelPolicies` | **1** — *TDC Staff Travel Policy 2026*, effective 1 Jan. Caps: Economy domestic, **Premium economy international**, GHS 900/night domestic, **GHS 2,400/night international**, 21 days ahead for flights (14 for a hotel), receipts above GHS 100, claims within 14 days, max single trip GHS 75,000. ⚠ **UAT: Draft — not enforcing. Rebuilt: in force**, approved by `hr.officer` before the trips were submitted (Rule 1) |
| `StaffTravelPolicyRules` | **5** — hotel ceilings domestic and international, the international cabin-class ceiling, 21-day advance booking, receipt above GHS 100 |
| `StaffTravelPolicyExceptions` | **1**, **Approved** by `hr.officer` — the London summit hotel at GHS 3,200 against the 2,400 ceiling *(the older, trip-level register — no screen, D-29)* |
| `StaffTravelPerDiemRates` | **3** — Ghana (GHS 450/day), Lagos (1,500), London (2,600), each split into breakfast / lunch / dinner |
| `StaffTravelBudgets` | **UAT: 4**, one per trip, set before lane 3, **none approved**. **Rebuilt: 2** — only approved trips take one (D-16): Kumasi's, approved by `hr.officer`; Sebrepor's, **left awaiting approval** for chapter 5.4's live write |
| `StaffTravelAdvances` | **1** — Kumasi, GHS 2,500, **Disbursed**, settle by a date after the trip |
| `StaffTravelExpenseClaims` | **1** — Sebrepor, **Submitted**, 3 lines: per-diem GHS 250, transport GHS 60, fuel GHS 180 with a **receipt attached** |
| `StaffTravelRequestComments` | **3** — two on Lagos (one **internal**, not visible to the traveller), one query on London |
| `StaffTravelRequestAttachments` | **3** — the GhIE invitation, the CIPD programme, the Sebrepor receipts |
| `StaffGroupTravels` | **1** — *Lagos Free Zone study tour*, max 5, **Planning**, the Lagos request linked to it |
| `StaffTravelDocuments` | **4** — three passports (two **verified**), one driving licence. ⚠ `head.dev`'s passport is **deliberately short-dated** so the expiring view has something on it (chapter 10a) |
| `StaffTravelVisaRequirements` | **2** — Ghana→Nigeria (**ECOWAS free movement**, no visa, 90 days) and Ghana→UK (Standard Visitor, embassy visa, 15 days, VFS Accra) |
| `StaffTravelVisaApplications` | **1** — the London Standard Visitor, GHS 1,450 |
| `StaffTravelHealthRequirements` · `…Clearances` | **3** — Nigeria yellow fever (**mandatory**), Nigeria health declaration, UK fitness-to-travel over 60. Cleared: **UAT none; rebuilt 1** — Lagos's yellow fever |
| `StaffTravelInsurancePolicies` | **2** — Lagos GHS 250,000 cover, London GHS 500,000 |
| `StaffTravelRiskAssessments` | **2** — Lagos **Medium** with real mitigation notes, London **Low**. Only the London one is acknowledged |
| `StaffTravelAlerts` · `…AlertNotifications` | **3** — Lagos road disruption (**Warning**), London Piccadilly line works (Info), Ashanti heavy rains (Info). **Sent to nobody**: they were raised before an active alert went out on its own (chapter 11) |
| `StaffTravelFlightBookings` | **UAT: 2** — Air Peace ACC↔LOS for Lagos, and British Airways ACC↔LHR **Ticketed**. **Rebuilt: 1** — the BA flight (**Premium economy, Confirmed** — its ticket refused until the visa is approved, T-24) with 2 segments, terminals, seats and baggage |
| `StaffTravelHotelBookings` | **UAT: 2** — the Radisson Blu Anchorage, Lagos (GHS 2,200/night), and the Hilton London Metropole at **GHS 3,200/night**, both Confirmed with no exception state. **Rebuilt: 1** — the Hilton, saved Pending with its exception asked, **authorised by `hr.officer`**, then Confirmed (D-8) |
| `StaffTravelGroundTransports` | **3** — the VIP coach to Kumasi, a Kumasi taxi, private car hire to Sebrepor *(completed)* |
| `StaffTravelCarRentalBookings` | **1** — Avis at Heathrow T5, VW Golf automatic |
| `StaffTravelItineraries` | **1** — the Lagos programme, version 1, 3 legs, **3 activities** with named contacts at LFZDC |
| `StaffDailyAttendances` | the approved trips' days, **on duty** (lane 9a) — on UAT Sebrepor's day and Kumasi's two |
| `StaffTravelReminderRuns` | one per sweep — **UAT has hundreds** (most from the test harness); a rebuilt database **none until 11 minutes after the API starts** |

> **Which trip to open.** On **UAT, Lagos** has every tab populated — itinerary, bookings, finance, compliance,
> comments, group — and chapter 5 spends most of its time there. **On a rebuilt database** Lagos has no bookings
> (a submitted trip takes none, D-23) and **London** carries them — open London for the Bookings tab.

### 2.2 Check the register reads four

Open `/hr/travel` and count. If it is not four, something re-ran or something was added. The two scenarios are
"ensure" steps — each step is skipped when its row exists — and safe to re-run **on a database the pack built**:

```powershell
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
$env:DEMO_DB = '<the demo database>'; $env:DEMO_API = 'http://localhost:5000/api'
node scenarios.mjs --only 080
node scenarios.mjs --only 081
```

> ⚠ **Not on UAT without deciding to.** Re-run there, 080 now **approves the travel policy** as `hr.officer`
> (D-60) before anything else — which turns UAT from Rule 1's *Draft* into *In force*, for every trip booked after.
> That is § 2.4's Option B by another door, and the closure left it undone on UAT on purpose (D-61). `DEMO_DB`
> defaults to UAT, so set it.

⚠ **Never press *Add a traveller* on the group** (chapter 7) — it mints a new **Draft** request and the register
stops reading four.

### 2.3 Write down the figures you will quote

| Screen | What to write down |
|---|---|
| `/hr/travel` — *All requests* | ____ requests |
| `/hr/travel` — *Pending approval* | ____ |
| `/hr/travel` — *Departing soon* | ____ |
| `/hr/travel/dashboard` | high-risk ____ · international ____ · estimated total GHS ____________ |
| `/hr/travel/claims` — *Awaiting payment* | ____ claims, GHS ____________ |
| `/hr/travel/advances` — *Cash out* | ____ advances, GHS ____________ |
| `/hr/travel/breaches` — *All* | ____ |

⚠ **"Awaiting payment" will read 0**: the Sebrepor claim is *Submitted*, not yet *Approved*. Chapter 9 reviews and
approves it live, and *then* the queue has something in it — which is a much better demonstration than a pre-baked
number.

### 2.4 Decide about Rule 1 — do you want the cap to bite?

**On a rebuilt demo database there is nothing to decide:** the policy is in force, London's Hilton has already gone
through D-8, and chapter 13a shows who authorised it.

**On UAT** this is the only real decision in the prep, and it changes the best five minutes of the demo.

**Option A — leave the policy as a draft (no prep).** Chapter 13 shows the policy, its caps, its
**Draft — not enforcing** state and the **Approve** button, and you say the sentence about why an unapproved
policy binds nothing and who is allowed to sign it. That is a *good* governance story and it costs
nothing. The Bookings tab will accept anything, and you simply do not offer to show a refusal.

**Option B — approve the policy first, so the cap refuses live.** Then chapter 5.3 can add an over-cap hotel on the
**Kumasi** trip and be refused by name, ask for the exception, and have `hr.officer` authorise it on Policy
Breaches — the most convincing three minutes in the module.

To do Option B, sign in as **`hr.officer`**, open Administration → HR → Travel → **Travel Policies** and press
**Approve** on the 2026 policy. `hr.head` drafted it and is refused by name. Since closure lane 4 (D-3) that is the
route: it needs no grant and no SQL, and the server checks the version's dates and steps down the version it replaces
(O-4). *(The first edition's SQL shortcut is gone: it skipped every check the approval makes, including the author
rule — the very control the chapter demonstrates.)*

> Tonight I chose: ☐ **A** — policy stays a draft  ☐ **B** — `hr.officer` approved it

⚠ **If you choose B on UAT, two things stay as they were.** The trips already submitted were checked against no
policy — Kumasi and Sebrepor record none, so their claims are held to none (Rule 1); Lagos and London were linked to it
by hand before lane 1, so once it is approved their claims are held to it. And **London's Hilton** was
booked at GHS 3,200 against the 2,400 cap *while the policy was inert*: the row still exists — the guard runs on
write, not on read — and carries no exception. Say *"and here is one that went over before the policy was signed"*,
not *"and here is one the system let through"*.

### 2.5 Know who does the second half

Since lanes 3 and 4 nobody completes a money or authority act alone (Rules 2 and 5). The demo's second officer is
**`hr.officer`**; keep a window open as them. The acts that need them:

| Chapter | `hr.head` does | `hr.officer` does |
|---|---|---|
| 13 | drafted the policy | **approves** it *(Option B)* |
| 5 header | travels to London | gives London **HR's approval** *(done on a rebuilt demo)* |
| 5.4 | sets a budget · approves an advance | **approves the budget** · **pays the advance out** |
| 9 | reviews the claim and its expenses | **records the payment** |
| 5.3, 13a | books over the cap and asks for the exception | **authorises the breach** |
| 9 *(undo)* | **voids the payment** | — they paid it, so they may not void it |

Each refusal is on screen with its reason — press the button as `hr.head` first when you want the room to see the
control.

### 2.6 Three windows, three personas

- **Window A — `hr.head`.** Most of this book.
- **Window B — `hr.officer`.** The second half of every act in § 2.5.
- **Window C — `staff`** for chapter 15 (the portal) — the Sebrepor traveller, whose claim you will have just
  reviewed.

The line authorities who approve a trip's first stage are named on its stage banner (chapter 3a); sign in as one
only if you walk an approval.

### 2.7 Pre-open every screen

In Window A, left to right:

| Tab | Route | Used in |
|---|---|---|
| 1 | `/hr/travel` | ch. 3 |
| 2 | the deep trip — **Lagos on UAT, London on a rebuilt demo** | ch. 5 |
| 3 | `/hr/travel/claims` | ch. 8–9 |
| 4 | `/hr/travel/advances` | ch. 8a |
| 5 | `/hr/travel/visa-requirements` | ch. 10 |
| 6 | `/hr/travel/documents` | ch. 10a |
| 7 | `/hr/travel/alerts` | ch. 11 |
| 8 | `/hr/travel/dashboard` | ch. 12 |
| 9 | `/administration/hr/travel/policies` | ch. 13 |
| 10 | `/hr/travel/breaches` | ch. 13a |

Window B on the claims queue; Window C on `/me/travel`.

### 2.8 Prep checklist

- [ ] You know **which database** you are on, and so which side of each walk's fork you read *(§ 2.1)*
- [ ] `/hr/travel` reads **four** requests *(§ 2.2)*
- [ ] The figures from § 2.3 are written down
- [ ] On UAT: you have chosen **A** or **B** for the policy *(§ 2.4)* and done it if B
- [ ] Windows A, B and C are signed in *(§ 2.6)*
- [ ] Ten tabs open in the order above *(§ 2.7)*
- [ ] You have read **Rules 1–7**

---

## 3. `/hr/travel` — the register

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Staff Travel → **Register** · `/hr/travel` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Every trip the corporation has been asked to pay for, and where each one has got to. Three
> views, because a travel desk has three questions: what is there, what is waiting on an approver, and what
> is leaving soon."*

### 👁 On the page

**Header:** *Staff travel* — *"Travel requests, their approval, and the trips that follow."*,
back-link to the HR hub, and a **Raise request** button.

**Three view buttons**, the active one filled:

| Button | Shows | Reads |
|---|---|---|
| **All requests** | every request on record | *"Every travel request on record"* |
| **Pending approval** | submitted and waiting on an approver | *"Submitted and waiting on an approver"* |
| **Departing soon** | approved trips leaving within 30 days | *"Approved trips leaving within 30 days"* |

**A filter card** — the active view's hint on the left, and a **type** dropdown on the right: *All types* and
**all eleven travel types** (since closure lane 0, built from the enum; T-12 fixed). It filters in the browser.

**One employee's trips.** Opened from an employee record's Travel tab, the register reads `?employeeId=…` and says
so in a banner — *"Showing the trips of … only."* — with **Show every traveller** to clear it.

**Six columns:**

| Column | Shows |
|---|---|
| **Number** | `TR-2026-00002` — a link into the request |
| **Traveller** | the employee's name |
| **Route** | a **🌐 globe icon** when international, then `Tema → Lagos, Nigeria` |
| **Dates** | `18 Oct 2026 – 23 Oct 2026` |
| **Estimated** | right-aligned, **in the row's own currency** — `GHS 38,000.00` |
| **Status** | a status badge |

> **Money is rendered from each row's own currency code, never a hard-coded symbol.** Travel spans
> currencies by nature, and the request carries the one it was costed in — Rule 5's footnote.

### ▶ Walk it

**1 — Open the register on *All requests*.** Four rows.

> *"Four trips. A conference in Kumasi, a study tour in Lagos, a summit in London, and a site
> handover at Sebrepor — which between them are about as different as corporate travel gets."*

**2 — Read the *Route* column across and point at the globe icons.**

> *"Two of them cross a border, and the system knows that without being told: international is
> worked out by the server from the two countries rather than being a box somebody ticks."*

**3 — Point at the *Estimated* column.**

> *"Three hundred and fifty cedis for a day at Sebrepor; sixty-two thousand for a week in London.
> And the currency is on every row, because a travel register that assumes one currency is a
> register that will be wrong the first time somebody goes abroad."*

**4 — Switch to *Pending approval*.**

- **On UAT:** two rows, Lagos and London — both submitted before the two-stage route (Rule 3).
- **On a rebuilt demo database:** one row, Lagos; London is approved (D-26).

> *"Waiting on somebody. And this is a purpose-built queue, not a status filter — 'what is waiting on an
> approver' is the question the desk is actually measured on. The approvers themselves have their own queue,
> which I will show you next."*

**5 — Switch to *Departing soon*.**

> *"Approved and leaving within thirty days. Which is the other question a travel desk lives on,
> because that is the list where a passport that expires next month becomes a problem."*

**6 — Switch back to *All requests* and open a trip.**

- **On UAT**, open **`TR-2026-00002`, Lagos**: its flight and hotel were booked before closure lane 5, so all eight
  tabs are populated.
- **On a rebuilt demo database**, Lagos — still awaiting approval — carries no bookings (D-23); open **London**
  for the Bookings tab and Lagos for the itinerary.

### ⚙ Behind the page

| View | Endpoint |
|---|---|
| All requests | `GET api/staff-travel/requests/all` (or `…/employee/{id}` with the banner) |
| Pending approval | `GET api/staff-travel/requests/pending-approval` |
| Departing soon | `GET api/staff-travel/requests/upcoming?daysAhead=30` |

All gated on `HR.Travel.Read`. Table: `StaffTravelRequests`. The API also offers paged, by status, by date range,
by organisation unit and by parent reads this screen does not use; every list read names its traveller and
destination (asserted by the harness since lane 10).

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-11 · Unpaged.** `requests/all` returns every request the tenant has ever had; `GET requests` is the paged one and the screen uses the unpaged variant | |
| **T-13 · No search, no date filter.** The API has both; the traveller filter arrives from the employee record only | |
| **T-14 · No export** | |

*Fixed since the first edition:* T-12 (all eleven travel types on the filter).

---

## 3a. `/hr/travel/approvals` — what is waiting for *you*

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Staff Travel → **Approvals** · `/hr/travel/approvals` · as
**whoever a trip waits for** — a line manager, the travel desk, HR · **3 minutes**

### 📖 What it is

> *"Not the desk's queue — yours. Every trip that is waiting for your signature, and only those."*

Since closure lane 2 (D-7) a trip is decided first by the traveller's line authority and then by HR (Rule 3). A
line manager needs no travel permission for that: **every employee sees this entry**, and it lists only the trips
waiting for them.

### 👁 On the page

**Header:** *Travel Approvals* — *"Travel requests waiting for your decision — as the traveller's line manager, for
the travel desk, or as HR."*

**Columns:** **Request** *(a link)* · **Traveller** · **Trip** · **Estimated** · **Stage** *(e.g. Line manager
approval)* · **You decide as** *(the traveller's supervisor, a head of unit above them, HR …)* · **Waiting** *(Today,
1 day, n days)*.

Empty state: *"Nothing is waiting for you — When a trip needs your decision it appears here, and you are
notified."*

Opening a row takes you to the trip, where **Approve**, **Reject** and **Return for revision** are drawn for you
(chapter 5). A line manager without travel permission sees the trip's overview, comments, attachments and the
workflow — not the desk's itinerary, bookings, finance and compliance tabs, nor its Edit, Cancel or Complete.

### ▶ Walk it

**1 — Sign in as the person the trip waits for.** The trip's stage banner names them (*"Waiting for …"*).

- **On UAT:** Lagos is on the old one-step route and waits for **HR** — as `hr.head`, one row, *You decide as HR*.
  London is `hr.head`'s own trip, so it is not hers to decide (D-7): it waits for **`hr.officer`**.
- **On a rebuilt demo database:** Lagos waits at the **line manager** stage for `gm.ops`'s line authority —
  the trip's banner names who — and then for HR.

> *"This is the approver's own list, not the travel desk's. A line manager who never opens the travel module
> still has a queue — and a notification — the moment one of their people asks to travel."*

**2 — Point at *You decide as* and *Waiting*.**

> *"And it says in what capacity I am being asked, and how long the trip has been waiting on me."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The queue | `GET api/staff-travel/requests/my-approvals` — the engine's tasks, then travel's line rule, request by request | signed in, internal |
| Who may decide | `GET …/requests/{id}/viewer-actions` | the trip's read door |
| Approve / Reject / Return | `POST …/requests/{id}/approve` · `…/reject` · `…/return` (`StaffTravelApprovalsController`) | signed in, internal — **the service decides who**, stage by stage |

⚠ Decide here or on the trip page — **never in the generic workflow inbox**, which skips travel's service
entirely (§ 1.6, defect #15).

---

## 4. `/hr/travel/new` — raising a request

### 📍 Where you are

**From:** the register → **Raise request** · `/hr/travel/new` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Everything that has to be settled before anybody asks for the money: who is going, where, when,
> why, what it will cost, how risky it is and what they will need to be allowed in."*

Since closure lane 0, every list on this form is built from the server's own enums, so no choice is refused
(the first edition's Rule 3 is gone). Since lane 1 the server, not the form, decides the trip's unit, whether
it is international, and the policy it is checked against.

### 👁 On the page

**Header:** *Raise a travel request* — **"The request is created as a draft — submit it separately
once the details are settled."**, back-link.

**Card 1 — Who is travelling** *(create-only, and desk-only)*
- **Traveller** *(searchable employee picker, required)*
- **Raised as** — the capacity: Employee · Manager · HR Admin · Travel Desk

> **There is no "raised by" field, only "raised as".** Who raised it is stamped from the token;
> the **role** it was raised under is the only real input. And **the traveller is create-only** — the
> update payload carries no employee id, so a trip cannot be reassigned. Cancel and raise a new one.

**Card 2 — The trip**
- **Travel type** *(required — all eleven)* · **Purpose** *(required — all twelve)*
- **Justification** *(textarea — "Why this trip is necessary, and what it should achieve.")*
- **From (country)** · **From (city)** · **To (country)** · **To (city)** *(all required)*
- A dashed callout appears the moment the two countries differ: *"🌐 This is an international trip — it crosses a
  border, so compliance documents and visa checks apply."*
- **Departure** · **Return** *(required; the return not before the departure)*

**Card 3 — Cost, risk and requirements**
- **Estimated cost** *(required, ≥ 0)* · **Currency** *(required — Finance's currencies, read through HR's own door,
  `api/hr/currencies`: the form used to read Finance's own list, which HR and travellers are refused — O-19)*
- **Priority** *(Routine · Urgent · Emergency)* · **Risk level** *(Low · Medium · High · Critical · Prohibited)*
- **Requires a visa** — *"Set from the visa register when the traveller's passport is on file. The flight is
  ticketed once a visa application is approved or recorded as not required."*
- **If this differs from the visa register, why** — only when the answer above is deliberately not the register's
  (a residence permit, say); kept on the trip as an internal note
- **Requires health clearance** — *"The destination's health requirements are listed on the trip's Compliance tab,
  where the travel desk ticks each one off. Nothing is blocked by an unticked one."*
- **Reason for the change** *(edit only)*

**Card 4 — Policy and limits** *(since closure lane 1, T-16)* — the organisation unit **from the traveller's own
employee record** (the form no longer offers a unit), the **approved policy** that covers the trip on its departure
date, and its limits; a warning when the estimate is above the single-trip limit, which is enforced at submission.
With no approved policy: *"No approved travel policy covers this trip, so no limits apply to it."* — **what UAT
shows** (Rule 1).

**Footer:** **Cancel** · **Create request**.

**What the server decides, not the form:**

- **International** is the two countries'. **The visa flag** is the visa register's, for the traveller's primary
  passport into the destination, on save and again at submission — an E-Visa or embassy visa means yes, visa-free or
  on arrival no — unless the override reason says otherwise (lane 7, D-39). A destination the register records as
  refusing that passport cannot be submitted.
- **The unit and the policy** are the traveller's (lane 1).
- **A leaver is refused**, and so is a trip that starts after an approved separation's leaving day (lane 9c,
  D-57).

**At submission** (chapter 5) the server also refuses a departure already past (the desk submits it with a
reason — *Submit after departure*), dates **overlapping another trip** of the traveller that is submitted, approved
or under way, and an estimate **above the policy's single-trip limit**. It **warns** — the submission still goes
— when the traveller has **approved leave** over the dates, when no insurance covers an international trip yet, and
when the primary passport expires within six months of the return.

### ▶ Walk it

**1 — Press *Raise request*.** Read the subtitle aloud:

> *"'Created as a draft — submit it separately.' Which matters: raising a request and asking for it
> to be approved are two acts, and the gap between them is where the travel desk gets the details
> right."*

**2 — Card 1 — pick a traveller, and leave *Raised as* on Travel Desk.**

> *"And note what is not on this form: who raised it. That is taken from whoever is signed in. What
> you can say is the capacity you are raising it in — the desk, a manager, HR, or the employee
> themselves."*

**3 — Card 2 — Travel type **Conference**, Purpose **Conference**.**

**4 — Set the route: from Ghana / Tema, to South Africa / Johannesburg.** Watch the callout appear.

> *"And the moment the countries differ, the system says so. It is not a box somebody remembers to tick — the
> server works it out from the two fields that already answer the question, and the visa question from the
> traveller's passport and the visa register."*

**5 — Dates next quarter; a real justification.**

**6 — Card 3 — cost 45,000, currency **GHS**, priority Routine, risk **Medium**.** Then read **Card 4**.

> *"And here, before anybody submits anything, the policy this trip will be held to and its limits — or, on this
> database, the honest statement that no approved policy covers it yet."*

**7 — 🔴 LIVE WRITE 1 — press *Create request*.** You land on the new request, at **Draft**, with
its number.

> *"TR-2026-00005. Draft. Nothing has been asked of anybody yet."*

*Undo:* chapter 17 — a **Draft** is deleted (Admin, which `hr.head` holds), or cancelled with a reason.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Create *(desk)* | `POST api/staff-travel/requests` | `HR.Travel.Write` |
| Create *(portal)* | `POST api/staff-travel/me/requests` | signed-in — no employee id anywhere |
| The policy preview | `GET …/requests/policy-preview` *(desk)* · `GET …/me/policy-preview` *(portal)* | Read / signed-in |
| Countries | `GET api/Country/active` | reference data |
| Currencies | `GET api/hr/currencies` | signed in, internal |

Table: `StaffTravelRequests`. The number comes from the shared `NumberSequences` table as
`TR-{year}-{00000}`. `InitiatedById` is the token's employee id; `Status` is set to `Draft`;
`EstimatedDurationDays` is derived from the two dates.

*Fixed since the first edition:* T-3 and T-15 (every enum value on the form), T-16 (the policy shown, and recorded
at submission), T-17 (the single-trip limit enforced at submission).

---

## 5. `/hr/travel/[id]` — the request, and its eight tabs

### 📍 Where you are

**From:** any row of the register · `/hr/travel/[id]` · as **hr.head** · **18 minutes** — by far
the longest chapter, and the module

### 📖 What it is

> *"One trip, completely. What was asked for, what was approved, where they are actually going day
> by day, what has been booked, what it costs and who has been paid, what they need to be allowed
> in, what has been said about it, and the papers."*

### 👁 The header

Title `TR-2026-00002`, subtitle `Kwabena Osei · Tema → Lagos, 18 Oct 2026`, back-link, and the controls below.
**Each one is drawn only when it can work** — for this status, and for you.

| Control | Appears when |
|---|---|
| A **status badge** | always |
| **Edit** | `Draft` or `ReturnedForRevision`, for the desk |
| **Submit** · **Recall** *(the workflow's own buttons)* | Submit on `Draft` or `ReturnedForRevision`; Recall while it is out for approval — the requester's |
| **Submit after departure** | a draft whose departure date has passed — the desk only, with **the reason it is late**, kept as an internal note (lane 1) |
| **Approve** · **Reject** · **Return for revision** | `Submitted`, **and you are the person this stage waits for** — the server's answer (*viewer actions*), not a permission (§ 1.6). Reject and Return need a reason |
| **Request change** | `Approved` — sends the trip back for re-approval with what has changed; its bookings, advances and claims stay with it (D-9). Its attendance days come off until it is approved again (lane 9a) |
| **Mark completed** | `Approved` or `InProgress` — **disabled until the trip has started** (*"A trip can be marked completed once it has started."*) |
| **Close trip** | `Completed` — refused while a claim is unpaid or an advance unsettled (lane 1) |
| **Cancel** | not Cancelled, Rejected, Completed, Closed or **InProgress**. A reason is required. The server refuses it while a booking is confirmed or ticketed (the desk cancels those first, D-24), while advance cash is still out, or while a company vehicle is approved or out in Fleet (lane 6) |
| **Did not travel** *(lane 8, D-48)* | `InProgress` and the trip has not ended — see below |

**Under the header**, when they apply:

- **the stage banner**, on a submitted trip — *"Line manager approval stage — then HR approval"*, and either what
  you decide as (and, at the last stage, *"You set the approved budget."*) or *"Waiting for … or …"*;
- **destination alerts** in force for the trip — on a submitted trip framed for the approver, on an approved one for
  the booker (lane 7, E6);
- on a **driver's own trip**, a line naming the trip whose company vehicle they drive (lane 6, D-33).

**Cancel's dialog:** *"A cancelled request cannot be revived. The reason is kept on the record."* Cancelling withdraws
its approval in progress, its unconfirmed bookings (D-24), advances not yet paid out (lane 3), its plan (5b) and its
attendance days (9a), and tells the traveller.

**Did not travel** opens the same dialog as *Cancel as not travelled*, for a trip the nightly sweep moved under way on
its date that did not happen. The server refuses it once anything was spent on the trip — a claim filed, advance
cash out, a booking confirmed, ticketed or used, a company vehicle dispatched or back — and says to mark it completed
instead. The reason is kept as *"Did not travel: …"*, with an internal note. A company vehicle's driver kept away
overnight travels on a request of their own, which goes with the trip — unless it too is under way: then the cancel
is refused until the desk has dealt with the driver's request on its own page (D-52).

**Eight tabs:** Overview · Itinerary · Bookings · Finance · Compliance · Comments · Attachments · **Workflow**. A line
manager deciding the trip, without travel permission, sees Overview, Comments, Attachments and Workflow only.

---

### 5.1 👁 Overview

**Card 1 — The trip** — Traveller · **Raised by** *(name and the role in brackets)* · Type · Purpose · Priority ·
**Route** *(with the globe icon)* · Departs · Returns · Duration · **On attendance** · Organisation unit · **Travel
policy**.

- **On attendance** *(lane 9a, D-54)* — how many of the trip's working days (Monday to Friday, not a public holiday)
  are on the traveller's attendance as *On duty*. They go on when the trip is approved, come off if it is cancelled or
  sent back for a change, and stop at an early return; a day already recorded otherwise — a clock-in, leave, a clerk's
  entry — is never overwritten. The monthly attendance summary counts *On duty* as present.
- **Organisation unit** — the traveller's own, from their employee record (lane 1).
- **Travel policy** — the approved policy the trip was checked against **when it was submitted**, which is the one
  its claims follow (§ 1.5). On UAT's trips, *none*.

**Card 2 — Cost and risk** — Estimated · **Approved budget** *(set by HR at the last approval stage)* · **Risk level**
*(a red shield at High or above)* · Visa required · Health clearance · Submitted · Approved · Completed · **Closed**
*(when, and by whom)*.

**Card 3 — Justification** — rendered only when there is one.

**Card 4 — Why it was cancelled / rejected** — rendered only when there is a reason, with who and when.

### ▶ Walk the Overview

**1 — Open the Lagos trip and read Card 1.**

> *"The study tour of the Lagos Free Zone housing scheme. Raised by the travel desk on behalf of
> the General Manager for Operations — and the record keeps both: who actually raised it, and the
> capacity they raised it in."*

**2 — Card 2, and point at *Approved budget*.**

> *"Thirty-eight thousand estimated. The approved budget is what HR authorises at the last signature, which is
> not necessarily what was asked for — and that is the number the trip is then measured against."*

Lagos is still awaiting approval, so its approved budget reads an em dash; Kumasi's and, on a rebuilt demo,
London's carry one (T-10 fixed).

**3 — Point at *Risk level* — Medium, and the shield that is not there.**

> *"Medium risk, so no red flag. The London one is Low. At Critical, the flight would not be ticketed until the
> traveller had read and acknowledged the risk assessment; at Prohibited, the trip could not even be submitted."*

**4 — On the Kumasi trip, point at *On attendance*.**

> *"And it is already on the traveller's attendance: the working days of an approved trip are recorded as on duty,
> so nobody marks him absent while he is in Kumasi."*

---

### 5.2 👁 Itinerary tab

**What it is:** the programme — where they are, day by day, and what happens.

**Versioned, and its status is the server's** (closure lane 5, slice 5b, D-25):

| Status | How a version gets there |
|---|---|
| **Draft** | created; legs and activities are added and changed |
| **Finalised** | **Finalise**, on the version in force, once it has a leg — it is stamped, and then it, its legs and its activities are the record: no leg, edit or removal |
| **Superseded** | another version is made current |
| **Cancelled** | the trip is cancelled |

The version **in force** carries a ★ and is never deleted; a superseded one can be removed.

**Empty state:** *"No itinerary yet"* with **Create an itinerary**.

**When there is one:** a version picker, the version's title and status, a summary strip (travel, working and
weekend days — **worked out from the trip's dates**, read-only), and buttons as they apply: **Make this the current
version** *(on a version not in force)* · **Finalise** *(the version in force, a draft; disabled until it has a
leg)* · **Edit** · **Delete** *(Admin; never the version in force)* · **New version**.

**The legs, in sequence.** Each shows: order · **leg type** (Departure · Transit · Arrival · Stay · DayTrip · Return) ·
date · origin → destination · transport mode (Flight · Train · Bus · Car · Ferry · Helicopter · Motorcycle · Walk) ·
times · notes · **Linked booking** — one of **this trip's** bookings, shown with its dates — and a **flag when the
leg's date disagrees with the booking's** (a flight flying another day, a night outside the hotel stay; a warning,
not a refusal — T-19). Nested underneath, its **activities**: type, title, where, the address, start and end, a named
contact with email and phone, and whether it is **mandatory**. Legs and activities have **Edit** and **Remove**
(Admin) while the version is a draft.

A plan is made while the trip is open — from its draft until it is under way.

### ▶ Walk the Itinerary

**1 — Open the tab on the Lagos trip.** *Lagos Free Zone study tour — programme*, version 1, **Draft**, three legs.

**2 — Read the three legs.**

> *"Leg one: departure, Tema to Lagos, Air Peace out of Kotoka Terminal 3 at 08:40, met on arrival by the host
> organisation. Leg two: the stay — three programme days on Victoria Island and at Ibeju-Lekki. Leg three: the
> return, back into Accra and a road transfer to Tema."*

**3 — Expand leg two's activities.** This is the moment the room understands the depth.

> *"And inside the stay, what actually happens. Opening session with the Lagos Free Zone Development
> Company at half past nine — at their head office, on Free Zone Road, with Mrs Adeola Bankole
> named, her email and her Nigerian mobile. Marked mandatory. Then the site visit to the Phase 2
> workers' housing scheme with Engineer Musa Idris. Then a wrap-up back at the hotel to draft the
> report for the Managing Director — not mandatory."*

**4 — Say what that is for.**

> *"That is not a diary. That is the document you give the traveller before they get on the plane,
> and the document the Managing Director reads when the report comes back. And if it changes — and
> it always changes — you build version two and version one stays, because 'what were we supposed
> to be doing on the Tuesday' is a question somebody eventually asks."*

**5 — 🔴 LIVE WRITE 2 *(optional)* — Add an activity** to leg two. Title *"Courtesy call on the Ghana High
Commission, Lagos"*, type Meeting, a time, not mandatory.

⚠ **Do this before step 6.** Once the version is finalised, the add is refused.

**6 — 🔴 *(optional)* Finalise the version.** It becomes **Finalised**, stamped with the date.

> *"And now it is the programme, not a draft of it: nothing on it changes. A change from here is a new version —
> and this one stays as it was, superseded, with the date it was agreed."*

*Undo:* chapter 17 — an added activity is removed (Admin) while the version is a draft; a finalised version stays
finalised, so finalise only if you are content to leave it so.

### ⚙ Behind the Itinerary

| Element | Endpoint | Permission |
|---|---|---|
| The itineraries | `GET api/staff-travel/itineraries/request/{requestId}` · `…/current` | Read |
| Create / update | `POST` / `PUT …/itineraries[/{id}]` — the version number and status are the server's | Write |
| **Make current** | `POST …/itineraries/{id}/set-current` | Write |
| **Finalise** | `POST …/itineraries/{id}/finalise` | Write |
| Legs | `GET`/`POST …/{itineraryId}/legs` · `PUT …/legs/{legId}` | Read / Write |
| Activities | `GET`/`POST …/legs/{legId}/activities` · `PUT …/activities/{id}` | Read / Write |
| Every delete | `DELETE …` — never the version in force | **Admin** |

Tables: `StaffTravelItineraries`, `StaffTravelItineraryLegs`, `StaffTravelItineraryActivities`.

> ⚠ **Times are sent as the local wall-clock string the traveller typed**, not converted to UTC. A
> meeting at 09:30 in Lagos is 09:30 there whatever the browser's timezone is, and converting would
> shift it by the offset between the traveller and whoever is doing the booking.

---

### 5.3 👁 Bookings tab

**What it is:** what has actually been reserved. **Four sections, each with its own Add button:** **Flights** ·
**Hotels** · **Ground transport** · **Car rentals**.

**Bookings live on an approved trip** (closure lane 5, D-23): the Add buttons show only while the trip is Approved or
under way; on any other trip a note says why. Every booking falls inside the trip's dates, a day either side.

**The status is the verbs', not the dialog's.** A booking is saved **Pending**, and the row's **⋯ menu** moves it:
*Put on hold* · *Confirm* · *Ticket…* (a flight, with the ticket number) · *Mark completed* and *Record a no-show*
(once the trip has started) · *Cancel booking…* (a reason, kept as an internal note, and on a flight or hotel the
supplier's fee) · *Edit* · *Delete* (a **pending** booking only, administrators — never one carrying a policy
exception, D-20). A flight also has **Segments**.

**Flights** — Airline · Reference *(PNR)* · Class · Fare · Segments *(a count)* · Status. Dialog: **Airline** ·
**Airline code** · **Booking reference** · **Ticket number** *(on edit)* · **Cabin class** *(Economy · Premium economy ·
Business · First)* · **Booked through** *(the channel)* · **Fare** · **Taxes and fees** · **Currency** · **Supplier** ·
**Ask for an exception to the policy** + **Why the exception is needed**. The **Segments** dialog lists, adds and
removes them: order, carrier, flight number, from and to airports, seat, departs and arrives (local times), and
whether it is a connection — **the duration is the server's**, from the two times.

**Hotels** — Hotel · City · Dates · Nights · Total · Status. Dialog: hotel, chain, address, city, country, check in
and out, **rate per night** and currency, room type, star rating, booking reference, booked through, cancellation
policy, supplier, and the exception request. **Nights and total are the server's.**

**Ground transport** — Type · Route · Cost · Status. Ten types: Taxi · Rideshare · Bus · Train · Metro ·
**CompanyVehicle** · PrivateCarHire · Shuttle · Motorcycle · Ferry; estimated and actual cost.

**Car rentals** — Category · Route · Dates · Total · Status: category and model, pick-up and drop-off, **daily rate**
(the total is the server's), fuel policy, insurance included, licence required.

> ⛔ **This is where the policy bites** — when one is in force (Rule 1). A cabin class or a nightly rate above the cap,
> a booking made with less notice than the policy asks, or (under a preferred-vendors policy) one with no supplier is
> **refused**, unless the exception is asked for with a reason. Then the booking is saved **Pending** and **cannot be
> confirmed or ticketed** until a travel administrator **who neither booked it, asked for it, nor travels on it**
> authorises it on **Staff Travel → Policy Breaches** (chapter 13a; D-8). The refusal explains itself — the toast
> carries the server's own sentence.

**A company vehicle is reserved in Fleet** (closure lane 6). Ground transport of type *Company vehicle* offers Fleet's
vehicles — each with its plate and, when it is not free, why (another planned trip, or an insurance or other critical
item running out before the return) — and the licensed drivers, flagged when on leave or driving another trip; the
traveller's own official car comes preselected. Pick-up and drop-off times are required, and there are no cost fields:
the leg costs what Fleet books against its trip. The leg's status is Fleet's, and its ⋯ menu offers only *Cancel
booking…*. **While Fleet publishes no approval route (UAT today, D-27) the reservation stays a draft** and the leg says
the vehicle is not held. **UAT has no fleet, so none of this can be shown there.** A leg that keeps a driver other than
the traveller away overnight offers **Raise the driver's request** — a Draft trip of their own, which goes with the leg.

**A flight on a trip that needs a visa is not ticketed** until a visa application on it is approved or recorded as not
required (T-24) — and, in order after that, until insurance covers every day of an international trip, and on a
Critical trip until the traveller has acknowledged the risk assessment (lane 7).

### ▶ Walk the Bookings

**On UAT, use London for the tour and Kumasi for anything live; on a rebuilt demo database, London for both.** UAT's
London is still awaiting approval, so it shows the bookings it was given before lane 5 but takes no new one.

**1 — Open London's Bookings tab.** British Airways, `H4RB9L`, Premium economy, **2 segments** — **Ticketed** on UAT
(booked before the visa rule existed), **Confirmed** on a rebuilt demo database.

**2 — Open the segments.**

> *"BA 078 out of Accra Terminal 3 at 22:55, into Heathrow Terminal 5 at 05:40 — the duration worked out from the
> two times, the aircraft, the seat, the baggage. And the return. That is what a travel desk hands to a traveller,
> and it is on the trip rather than in an email."*

**3 — The visa rule.**

- **On a rebuilt demo database:** open the flight's ⋯ menu and press **Ticket…**, with any number.

> *"Refused — and it says why: this trip needs a visa and no visa application on it is approved yet. A ticket is
> money the airline keeps; it is not bought until the traveller can actually get in."*

  Nothing is written. *(T-24, live.)*

- **On UAT:** the flight was ticketed before the rule existed, so say it instead: *"Today this would not be
  ticketed until the visa application is approved — the ticket waits for the visa, then the insurance."*

**4 — The hotel: Hilton London Metropole, GHS 3,200 a night.**

- **On a rebuilt demo database** — the D-8 story, already on the record:

> *"Three thousand two hundred a night at the summit venue, against a ceiling of two thousand four hundred. The
> booking was not refused — it was saved pending, with the reason, and it could not be confirmed until another
> officer authorised the breach. Booked by the head of HR, authorised by a second HR officer, confirmed only then.
> The person who books over the limit is never the person who waves it through."*

  Show it on **Staff Travel → Policy Breaches** (chapter 13a).

- **On UAT** — the hotel was booked before the policy and its exception rules existed, so it carries no exception
  state. Say what would happen now, then, if you prepared § 2.4 Option B (the policy approved), show it live on
  **Kumasi**, which is approved:

**5 — 🔴 *(Option B only)* Add a hotel on Kumasi at GHS 1,500 a night** — above the 900 domestic ceiling — with the
exception **not** asked.

> *"Refused — and it tells me why: fifteen hundred exceeds the policy's domestic ceiling of nine hundred."*

(Kumasi departs on 19 October, so from 5 October the refusal also names the policy's fourteen days' notice for a
hotel — the same exception covers both.)

Ask for the exception, give the reason, save again. **It is saved — Pending.** Open its ⋯ menu: *Confirm* is refused,
naming the exception. Then, as **`hr.officer`**, authorise it on Policy Breaches, and confirm it.

> *"And that is the control. The booking was not blocked — sometimes the summit hotel really is the only hotel — but
> it waited for a second officer. A clerk cannot tick their own exception: the box asks; another person decides."*

*Undo:* chapter 17 — cancel the hotel (a booking that carried an exception is cancelled, never deleted).

**6 — Point at *Booked through* on a row.**

> *"And every booking records the channel — desk, agency, direct with the airline, self-service. Which is how a
> travel manager answers 'are we still getting value out of the agency'."*

### ⚙ Behind the Bookings

| Element | Endpoint | Permission |
|---|---|---|
| Per request | `GET api/staff-travel/bookings/{kind}/request/{requestId}` | Read |
| Create / update | `POST` / `PUT …/bookings/{kind}[/{id}]` — saved Pending; the policy decides refuse or Pending | Write |
| The verbs | `POST …/{kind}/{id}/hold` · `…/confirm` · `…/ticket` *(flights)* · `…/complete` · `…/no-show` · `…/cancel` | Write |
| **Authorise / refuse a breach** | `POST …/flights/{id}/exception/authorise` · `…/refuse` (and hotels) | **Admin** — never the booker, the asker or the traveller |
| Segments | `GET`/`POST …/flights/{id}/segments` · `DELETE …/segments/{id}` | Read / Write |
| Fleet's vehicles and drivers | `GET …/bookings/fleet/options?requestId=&from=&to=` | Read |
| The driver's request | `POST …/ground-transport/{id}/driver-request` | Write |
| Delete | `DELETE …` — a pending booking without an exception only | **Admin** |

Tables: `StaffTravelFlightBookings`, `StaffTravelFlightSegments`, `StaffTravelHotelBookings`,
`StaffTravelGroundTransports`, `StaffTravelCarRentalBookings`.

**What the guard does**, in order: resolves the trip's own unit and staff level; finds the **approved** policy for
that unit (or the nearest above it, or the organisation's) in force on the **departure date**; takes the domestic or
international cap from **the trip**, in the policy's currency; and compares. Cabin class is a numeric comparison on
the enum's ordering — `Economy(1) → PremiumEconomy(2) → Business(3) → First(4)` — **which is load-bearing and must not
be renumbered**.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-1 · UAT's policy is a draft**, so nothing there is capped — Rule 1 (by D-61) | |
| **T-18 · Only flights and hotels are capped.** Ground transport and car rentals have no policy check, though a GHS 620-a-day car hire for five days is real money | |

*Fixed since the first edition:* T-8 (a supplier on every booking form — Procurement's register works since
2026-09-22), T-9 (the cap in the policy's currency), T-19 (a leg linked to its booking, flagged when the dates
disagree), and the self-granted exception (D-8).

---

### 5.4 👁 Finance tab

**Three sections: Budget · Advances · Expense claims.** Money on this tab follows Rule 5: two people, never one's own.

**Budget** — one per trip, and only once the trip is **approved** (D-16). **In the trip's currency** (set by the
server). Its **total** defaults to the trip's approved budget and cannot exceed it; its five parts — flights,
accommodation, per diem, transport, miscellaneous — are all zero or add up to the total exactly. Then the derived
figures (§ 1.4):

| Figure | Derived from |
|---|---|
| **Committed** | the trip's bookings — Pending and OnHold included, no-shows left out, a cancellation fee kept — plus Fleet's costs for a company vehicle |
| **Actual** | paid claims' net, plus advances paid out less cash handed back |
| **Variance** | approved total − actual |

An overrun turns its figure red and says so — **flagged, not refused**. **Approve the budget** *(Admin; never the
traveller's own trip, nor whoever set or last changed it — D-19)* stamps who and when; **changing an approved budget
withdraws its approval**. Empty state: **Set a budget**; then **Edit budget**.

**Advances** — Number · Type · Requested · Approved · **Outstanding** · Settle by · Status · **Finance** *(the posting's
state in HR's Finance register — on UAT, *Unposted*: no travel rule is switched on)*. On an approved or under-way trip
only (D-16). Row actions by status:

| Status | Actions |
|---|---|
| Requested | **Approve** — *"the amount requested or less — never more, and never your own advance"*; and within the trip's approved budget · **Reject** *(a reason)* · **Cancel** |
| Approved | **Pay out** *(not by whoever approved it — D-2)* · **Cancel** |
| Cash out *(Disbursed, PartiallySettled, Overdue)* | **Cash back** — record cash the traveller handed back, once · **Write off** *(Admin; never the traveller's own)* |

A trip's cancel withdraws its advances not yet paid out; cash still out holds the cancel back. An advance a leaver's
final settlement recovered is settled when the settlement is released (9c, D-58).

**Expense claims** — Number · Type · Claimed · **Payable** · Submitted · Status, each a link into the claim; **File a
claim** opens chapter 9b for this trip.

### ▶ Walk the Finance

**1 — Open the tab on the Kumasi trip** — approved, with its budget and its advance.

> *"The approved envelope, and how it is meant to be spent — and on this trip, approved by a second officer, because
> the officer who sets a budget does not also sign it."*

*(On UAT Kumasi's budget is set but not yet approved; on a rebuilt demo `hr.officer` has approved it.)*

**2 — Point at *Committed* and *Actual*.**

> *"Committed is what has been booked. Actual is what has gone out — the claims paid and the advance in hand. And
> both are worked out every time this page is read, not stored: a stored rollup is out of date the moment the next
> booking is made."*

**3 — The advance.**

> *"Two and a half thousand cedis, requested, approved and paid out — by two different officers. Cash in hand
> before the coach leaves, because somebody presenting a paper in Kumasi should not be funding the corporation's
> travel out of their own pocket."*

**4 — Point at *Outstanding* and *Settle by*.**

> *"And the outstanding balance, with a settlement deadline. Which is the half of an advance that organisations lose —
> recovered from the traveller's claim when it is paid, handed back as cash, or written off by an administrator who
> is not the traveller."*

**5 — 🔴 LIVE WRITE 3 *(optional)* — approve a budget as the second officer.** As `hr.head`, press **Approve the
budget**: refused — *you set it*. In a second window as **`hr.officer`**, approve it: *"Approved by … on …"*.

- **On UAT:** Kumasi's (all four demo trips carry budgets set before lane 3, none approved).
- **On a rebuilt demo database:** Sebrepor's, left awaiting approval for this step (Kumasi's is already approved).

> *"The officer who draws up the envelope does not also sign it."*

⚠ A budget can no longer be **set** on a draft trip (D-16) — the first edition's *Set a budget* on the chapter-4 draft
is refused.

*Undo:* chapter 17 — editing the budget withdraws its approval.

### ⚙ Behind the Finance

| Element | Endpoint | Permission |
|---|---|---|
| Budget | `GET api/staff-travel/finance/budgets/request/{id}` · `POST`/`PUT …/budgets` | Read / Write |
| **Approve the budget** | `POST …/budgets/{id}/approve` | **Admin** + employee-linked |
| Advances | `GET …/advances/request/{id}` · `POST`/`PUT …/advances` | Read / Write |
| **Approve** · **Reject** · **Cancel** an advance | `POST …/advances/{id}/approve` · `…/reject` · `…/cancel` | Write |
| **Pay out** · **Cash back** | `POST …/advances/{id}/disburse` · `…/refund` | Write |
| **Write off** | `POST …/advances/{id}/write-off` | **Admin** |
| Claims | `GET …/claims/request/{id}` | Read |

Tables: `StaffTravelBudgets`, `StaffTravelAdvances`, `StaffTravelExpenseClaims`. Paying out, cash back and a write-off
each record a row in HR's Finance posting register (§ 16).

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-20 · The budget's parts do not bind.** A GHS 20,000 flight against a 16,000 flight line is accepted; only the derived figures move, flagged red. Whether an overrun should refuse is TDC's question | |

*Fixed since the first edition:* T-21 (Overdue set by the sweep, WrittenOff by an administrator), T-22 (the budget is
the trip's currency, set by the server).

---

### 5.5 👁 Compliance tab

**The richest tab in HR, and the one that justifies the module.** Up to six cards:

**① Company vehicle incidents (from Fleet)** — only when Fleet has recorded one against a company vehicle on this trip
(lane 6, 6c): when, the vehicle and driver, type, severity, status, where and what happened. Fleet records and closes
incidents; the nightly sweep tells the desk and the traveller's line authority (lane 8). UAT has no fleet.

**② Active alerts for the destination** — every alert in force for the country (and city), with its severity, title
and **body**. A new alert raised active goes at once to the travellers of approved trips there (lane 7, E6).

**③ Risk assessment** — the risk level and category, the source, the summary, whether mitigation is required and the
**mitigation notes**, whether a duty-of-care briefing was sent, and the traveller's **acknowledgement**. Dialog:
**Assess the destination**. Nobody on the desk can record the acknowledgement: the card says *"Not yet acknowledged.
Only … can acknowledge their own assessment, on their My travel page."* (E1). On a **Critical** trip the flight is not
ticketed until it is acknowledged.

**④ Visas** — first, **what this traveller's passport needs**: the register's answer for the country that issued their
**primary passport** (read from the *Travel Documents* register, chapter 10a — the tab no longer lists documents) into
this destination — the requirement, category, maximum stay and **processing days**, flagged when the entry has not been
checked for over a year (T-40). With no passport on file it says so and links to the register. Then the trip's **visa
applications** — type, number *(masked)*, submitted, expires, fee, status — with **Record a visa** and, per row,
**Change**.

**⑤ Health requirements for the destination** — the requirements in force over the trip, mandatory first; the desk
**Clears** each with a note of what it saw (recorded in its name, today), or takes the tick off. Nothing is blocked by an
unticked one (lane 7, D-36).

**⑥ Insurance** — policy, type, cover, period, sum insured, premium. Dialog: **Record travel insurance**. An
international trip's flight is not ticketed until cover spans every day of it.

### ▶ Walk the Compliance

**Do this one on the London trip** — the one with a visa application — then Lagos.

**1 — Open London's Compliance tab and read the Visas card.**

> *"A Ghanaian passport into the United Kingdom: a Standard Visitor visa, up to a hundred and eighty days, **fifteen
> working days to process**. That last number decides whether a summit in three weeks is possible at all — and it is on
> the screen before anybody books a flight. And the visa application lodged at VFS Global, with its fee."*

**2 — Now Lagos's Visas card.**

> *"And into Nigeria: no visa. ECOWAS free movement, ninety days, zero processing days. Same screen, a different answer
> — because the answer is a property of the passport and the destination, recorded once and reused by every trip. And
> the register sets the trip's own visa flag: nobody has to remember to tick it."*

**3 — Lagos's active alert.**

> *"And what he is being told before he goes: fuel-supply protests causing road closures between Mile 2 and Badagry,
> use the Lekki–Epe corridor, allow two extra hours for airport transfers. In force until after he gets back — and it
> reached him the moment it was raised."*

**4 — The risk assessment.** This is the duty-of-care story.

> *"Medium risk. And the mitigation is not a tick-box: airport pickup by the host organisation only, no road movement
> after seven in the evening, the party travels together, hotel on Victoria Island, emergency numbers circulated before
> departure. A duty-of-care briefing was sent."*

**5 — Point at the acknowledgement.**

> *"And whether the traveller has confirmed they read it — which only the traveller can do, on their own page. Nobody
> can acknowledge a security briefing on your behalf, because an acknowledgement anyone can record for you records
> nothing."*

The Lagos assessment is not acknowledged — deliberately, for the portal walk (chapter 15).

**6 — Lagos's health requirements.**

> *"The yellow-fever certificate is mandatory for Nigeria. The desk has seen it, and ticked it — with who, when and a
> note of what was seen. The online declaration is not ticked yet. Neither stops the trip; the record says what has been
> checked."*

*(On UAT, nothing is ticked yet — the tick came with lane 7; a rebuilt demo has the yellow fever ticked.)*

**7 — 🔴 LIVE WRITE 4 *(optional)* — *Record travel insurance*** on the request you created in chapter 4.

### ⚙ Behind the Compliance

| Element | Endpoint | Permission |
|---|---|---|
| What the passport needs | `GET api/staff-travel/compliance/visa-requirements?passportCountryId=&destinationCountryId=` | Read |
| Alerts in force | `GET …/alerts/country/{countryId}/current` · `GET …/requests/{id}/destination-alerts` | Read |
| The traveller's passport | `GET …/documents/employee/{employeeId}` *(masked)* | Read |
| Visa applications | `GET`/`POST`/`PUT …/visa-applications…` | Read / Write |
| Risk assessments | `GET`/`POST`/`PUT …/risk-assessments…` | Read / Write |
| **Acknowledge** | the traveller only: `POST …/me/risk-assessments/{id}/acknowledge` (chapter 15) | signed in, own trip |
| Health requirements | `GET …/requests/{id}/health-requirements` · `POST`/`DELETE …/requests/{id}/health-requirements/{reqId}/clear` | Read / Write |
| Insurance | `GET`/`POST`/`PUT …/insurance…` | Read / Write |
| Fleet incidents | `GET …/compliance/requests/{id}/fleet-incidents` | Read |
| Every delete | `DELETE …` — never an acknowledged assessment or a sent alert | **Admin** |

Tables: `StaffTravelVisaRequirements`, `StaffTravelVisaApplications`, `StaffTravelRiskAssessments`,
`StaffTravelAlerts`, `StaffTravelAlertNotifications`, `StaffTravelInsurancePolicies`, `StaffTravelHealthRequirements`,
`StaffTravelHealthClearances`; the traveller's documents from `StaffTravelDocuments`.

*Fixed since the first edition:* T-23 (the traveller acknowledges on the portal), T-24 (the visa flag from the register;
the ticket waits for the visa), T-25 (health requirements ticked per trip), T-26 (warned at submission when the passport
expires within six months of the return).

---

### 5.6 👁 Comments tab

A textarea, an **internal note** switch, and **Add comment**. Below it, the thread: author, timestamp, body, and
*"Internal — not shown to the traveller"* on the private ones. The traveller writes here too, from My travel (chapter
15): their questions arrive labelled **Query** and their replies **Response**, under their own name, and since lane 8a
the desk is told. A reply — the desk's or theirs — answers a comment on the same trip, or it is refused.

A comment is **edited by its author only** (D-21); deleting one is its author's or an administrator's. Neither control is
on this screen.

### ▶ Walk the Comments

**1 — Open the tab on the Lagos trip.** Two comments.

> *"'Estates has confirmed four places on the study tour. Passport copies are with the travel desk.' Visible to the
> traveller. And underneath — 'Internal: the ECOWAS travel certificate is enough for Nigeria, no visa fee to budget for.'
> Marked internal, and the traveller does not see it."*

**2 — Say why that distinction matters.**

> *"Which is the difference between a comment thread and a conversation you can actually have. The desk needs somewhere
> to say 'we do not need to budget for this' without it reading as a promise to the traveller."*

**3 — 🔴 LIVE WRITE 5 — type a comment and press *Add comment*.** It tells the traveller (it is visible to them).

> *"And posted in my name — there is no author field, because there should not be."*

---

### 5.7 👁 Attachments tab

A table of what is attached: type, description, file name, size, who uploaded it and when, with **Download** and
**Remove**.

**Upload** takes an **attachment type** — Invitation Letter · Conference Brochure · Receipt · Visa Document · Insurance
Certificate · Medical Certificate · Other — a description and a file. The traveller's own files from the portal land here
too (lane 7, 7c2).

> This goes through the product's **controlled upload gate** — virus scanning, type and size checks, and a DMS
> record — unlike the company schedule's event attachments, which are references.

**Remove is `HR.Travel.Admin`** (the traveller removes their own from the portal). 🔴 It is a live write, so do not
press it on the demo's documents.

### ▶ Walk the Attachments

**1 — Open the tab on the Kumasi trip, then London, then Sebrepor.**

> *"The invitation to present at the Ghana Institution of Engineers on one; the CIPD summit programme on another; and on
> the Sebrepor trip, the actual fuel and taxi receipts — which are what the expense claim points at."*

**2 — 🚫 Do not press Remove.**

---

### 5.8 👁 Workflow tab

The shared workflow tab: the definition, the stages, who each is addressed to, the decision history and its comments.
**This is authoritative** — the request's `status` says only which phase it is in (§ 1.6).

### ▶ Walk it, and then move the request

**1 — Open the tab on the Lagos trip.**

> *"And this is not a status field somebody typed. It runs on the corporation's approval engine — the same one that
> approves leave, a requisition or a purchase order. The definition, the stage, who it is addressed to, and every
> decision with a timestamp."*

**2 — 🔴 LIVE WRITE 6 — approve the Lagos trip.** Rule 3 decides how:

- **On UAT** (the old one-step route): as `hr.head`, press **Approve** in the header; the dialog asks for the **approved
  budget**, prefilled with the estimate. The status becomes **Approved**.
- **On a rebuilt demo database** (two stages): the banner reads *"Line manager approval stage — then HR approval"* and
  *"Waiting for …"*. Sign in as the person it names and approve; then, as `hr.head`, approve the HR stage with the
  budget.

> *"Approved. And nothing on this page wrote that status — the page relayed the decision and refetched. With two
> stages, it stays out for approval until the second signature, without a line of travel code changing."*

Approval also puts the trip's working days on the traveller's attendance as *On duty* and tells the traveller and
the desk.

> *"And nobody decides their own trip — not the traveller, at either stage. That is the service's rule, not the
> engine's, so it holds whoever the stage is addressed to."*

**3 — 🔴 LIVE WRITE 7 *(optional)* — *Mark completed*** on the Kumasi trip, once it is under way (the button is
disabled until it has started). The nightly sweep completes it on its own the day after it ends (Rule 7).

> *"And when the trip has happened, it is completed — which opens the window for the expense claim — and closed once
> every claim is paid and every advance settled."*

### ⚙ Behind the request

| Element | Endpoint | Permission |
|---|---|---|
| The request | `GET api/staff-travel/requests/{id}` | Read, or the person it waits for |
| Who may decide, now | `GET …/{id}/viewer-actions` | the same |
| **Submit** | `POST …/{id}/submit` *(a late departure needs `lateSubmissionReason`)* | Write, via the engine |
| **Recall** | `POST …/{id}/recall` | the requester |
| **Approve** · **Reject** · **Return** | `POST …/{id}/approve` · `…/reject` · `…/return` | signed in — the service decides who (Rule 3) |
| **Request change** | `POST …/{id}/request-change` | Write |
| **Cancel** / **Did not travel** | `POST …/{id}/cancel` | Write |
| **Mark completed** · **Close** | `POST …/{id}/complete` · `…/close` | Write |
| Comments | `GET`/`POST …/{id}/comments` | Read / Write |
| Attachments | `GET`/`POST …/{id}/attachments` · `GET …/attachments/{id}/download` | Read / Write |
| Delete | `DELETE …/{id}` — a **Draft** only | **Admin** |

*Fixed since the first edition:* T-7 (Close trip; the sweep moves trips — chapter 14), T-10 (the approved budget asked
at the last stage), T-27 (internal notes). **T-28 was wrong**: the server always refused to cancel a cancelled,
completed or closed trip; since lane 1 it also refuses an under-way one except as *did not travel*.

---

## 6. `/hr/travel/[id]/edit` — amending a request

### 📍 Where you are

**From:** the request → **Edit** · `/hr/travel/[id]/edit` · as **hr.head** · **2 minutes**

### 📖 What it is

> *"The same form, with two differences: you cannot change who is travelling, and you have to say why you changed
> anything."*

### 👁 On the page

Chapter 4's form, minus **Card 1** *(traveller and raised-as are create-only)* and plus one field at the bottom of
Card 3:

- **Reason for the change** — *"Kept on the record — say what changed and why."*

**Only a `Draft` or `ReturnedForRevision` trip opens the form.** Any other status opens a page that says why and what to
do instead:

- **Submitted** — *"It is out for approval. Recall it, or ask the approver to return it for revision, and then edit it."*
- **Approved** — *"An approved trip is changed by sending it back for revision; it is then approved again. Its bookings,
  advances and claims stay with it."* — with a **Request change** button (D-9).
- anything later — *"A request that is … cannot be changed."*

### ▶ Walk it

**1 — From your chapter-4 draft, press *Edit*.**

**2 — Point at what has gone.**

> *"The traveller is not here. A request is raised for one person and stays with them — if the wrong name went on it,
> you cancel and raise a new one rather than quietly moving an approved trip onto somebody else."*

**3 — Point at *Reason for the change*.**

> *"And an amendment needs a reason, kept on the record. Which is the difference between a system you can audit and a
> system where the numbers move."*

**4 — Press *Cancel*.** Then open the **Kumasi** trip's edit address (`/hr/travel/<id>/edit`) to show the approved
trip's page and its **Request change**.

> *"And an approved trip is not retyped: it goes back for approval with what changed, and keeps everything that hangs
> off it."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Update *(desk)* | `PUT api/staff-travel/requests/{id}` — a Draft or returned trip only; the unit, the international flag, the policy and the approved budget are the server's | `HR.Travel.Write` |
| Update *(portal)* | `PUT api/staff-travel/me/requests/{id}` | signed-in, own request only |
| Request change | `POST …/requests/{id}/request-change` | Write |

*Fixed since the first edition:* T-29 (the update is a full replace, and the server keeps what the form does not send —
the group link, the facts it decides).

---

## 7. `/hr/travel/groups` — several people, one trip

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Group Travel** · `/hr/travel/groups` (+ `[id]`) ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Five people going to the same place for the same thing. Organised as one trip, tracked as five
> — because the flights are booked together and the approvals are individual."*

### 👁 Screen 1 — the register

**Header:** *Group travel* — *"Several people travelling to one place for one thing."*, back-link,
and a **New group trip** button *(Write)*.

**Six columns:** **Group** *(a link, the event underneath)* · **Lead** · **Destination** · **Dates** ·
**Travellers** *(places taken, out of the limit — `1 / 5`)* · **Status**.

**Empty state** says the design out loud: *"A group trip raises one travel request per traveller, so
everyone's is tracked individually while the trip is organised as one."*

**New-group dialog:** group name, event, **lead traveller** *(an active employee)*, destination country and
city, travel dates *(in order)*, max travellers. A new group starts in **Planning**.

### 👁 Screen 2 — the group (`…/groups/[id]`)

**Details:** Status · Lead · Destination · Dates · **Places taken** — *"A cancelled or rejected trip holds no
place."*

**The group's status moves by its own buttons** *(Write)* — an edit no longer writes one:

| Status | Buttons | Then |
|---|---|---|
| **Planning** | **Open to travellers** · **Close to new travellers** · **Cancel group** | |
| **Open** | **Close to new travellers** · **Cancel group** | |
| **Closed** | **Reopen** · **Cancel group** | adding is refused: *"…closed to new travellers. Reopen it to add someone."* |
| **In Progress** · **Completed** | *(set by the nightly sweep, chapter 14)* | Completed takes no edit and no cancel |
| **Cancelled** | — | only once **none of its travellers has a trip still going ahead** — cancel those, or take them off, first |

The sweep reads the group off its travellers' trips (Rule 7): **In Progress** once any of them is under way or
back, **Completed** once all of them are back. A group only moves forward.

**Edit** *(Write; not on a cancelled or completed group)* — name, event, lead, destination, dates, max
travellers *(never below the places already taken)*. ⚠ **A new destination or new dates are given to every
traveller whose trip is still a draft or returned for revision.** A submitted or approved trip keeps its own.

**Travellers** — Traveller · Request *(a link to the trip)* · Status · **Where and when**: *As the group*, or a
**Differs from the group** badge whose tooltip gives the trip's own city and dates. A cancelled or rejected
trip is greyed. Two buttons, both off once the group is closed or full *(the tooltip says why)*:

- **Add a traveller** *(Write)* — ⚠ **raises a new Draft travel request for them**: the group's destination and
  dates, plus *travelling from*, purpose, risk, justification, estimated cost and currency, visa and health
  clearance from the dialog. Whether it is international is worked out from the two countries. Someone already
  holding a place is skipped, not added twice; past the limit is refused.
- **Link an existing request** *(Write)* — pick the traveller, then one of their **draft or returned**
  requests. It joins the group and **takes the group's destination and dates**. A submitted or approved trip
  is refused — *"recall it first"* — and so is one already on another group.

**Remove** *(Admin, on each row)* — *"Take them off this group?"* ⚠ **Their trip is NOT cancelled**: it carries on
as an ordinary trip. **Delete** *(Admin)* takes everyone off the group first, then removes it; their trips carry
on.

> 🚫 ⚠ **Do not press *Add a traveller* or *Link an existing request* on the demo database.** Adding raises a
> new Draft request, and the register (chapter 3) then stops reading four — which the runbook reads out loud.

### ▶ Walk it

**1 — Open the register.** One row: *Lagos Free Zone study tour*, lead **Kojo Fiadzo** (`gm.ops`), Lagos,
**1 / 5**, **Planning**.

**2 — Read the empty-state sentence even though the list is not empty** — it is the best
explanation of the feature:

> *"A group trip raises one travel request per traveller, so everyone's is tracked individually
> while the trip is organised as one. Which is exactly right: the coach is booked once, but the
> approval, the advance and the expense claim belong to each person."*

**3 — Open the group.** One traveller, `TR-2026-00002`, **Submitted**, *As the group*.

> *"One traveller so far, and clicking through lands on their own travel request — with its own approval, its
> own budget and its own claim. It was linked to the group while it was still a draft, so it took the group's
> dates; once submitted, a trip keeps its own, and this column would say so."*

**4 — Point at *Places taken* and the limit.**

> *"Five places. The sixth is refused, and a traveller whose trip is cancelled gives the place back."*

**5 — Point at the status buttons — do not press them.** If asked:

> *"Planning, then open to travellers, then closed. Under way and completed are not buttons — they follow the
> travellers' own trips, overnight. And a group cannot be cancelled while anybody on it is still going."*

**6 — 🚫 Do not press *Add a traveller*.** If asked:

> *"Adding somebody raises their travel request for them, with the group's destination and dates — which is
> the point, and it is also why I am not going to do it on a database whose register I have just read out to
> you. Removing somebody does not cancel their trip: no longer travelling with the group is not the same as
> not travelling."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-travel/requests/groups` | Read |
| By status | `GET …/groups/status/{status}` | Read |
| The group | `GET …/groups/{id}` | Read |
| Create · Edit | `POST …/groups` · `PUT …/groups/{id}` | Write |
| **Open** / **Reopen** · **Close** · **Cancel** | `POST …/groups/{id}/open` · `…/close` · `…/cancel` | Write |
| **Add a traveller** | `POST …/groups/{id}/participants` | Write ⚠ creates a Draft request each |
| **Link an existing request** | `POST …/groups/{groupId}/requests/{requestId}` | Write |
| **Remove** | `DELETE …/groups/{groupId}/participants/{requestId}` | **Admin** |
| **Delete** | `DELETE …/groups/{id}` | **Admin** |

Table: `StaffGroupTravels`, with `StaffTravelRequests.GroupTravelId` pointing at it.

### ⚠ Known gaps

None open. *Fixed since the first edition (lane 1):* **T-30** (an existing draft can be linked), **T-31** (the
limit binds; a cancelled or rejected trip holds no place), **T-32** (the group's destination and dates reach
its draft and returned trips; the others are marked), and the group's status — In Progress and Completed had
no writer, and an edit wrote whatever status it was given.

---

## 8. `/hr/travel/claims` — the finance desk's queue

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Expense Claims** · `/hr/travel/claims` ·
as **hr.head** · **3 minutes**

### 📖 What it is

> *"What travellers have claimed back, across every trip — and what is waiting to be paid. This is
> a queue that runs sideways across the register, which is why it has its own menu entry."*

### 👁 On the page

**Header:** *Travel expense claims* — *"What travellers have claimed back, and what is waiting to be
paid."*, back-link.

**Two view buttons:** **All claims** and **Awaiting payment** — every claim pay accepts and has not paid:
**Approved**, and **Partially approved** too *(lane 3 — it was left out while pay accepted it)*. Oldest
submitted first. A claim a traveller files from the portal (chapter 15) lands here like any other.

When *Awaiting payment* has rows and they share one currency, a summary card appears:
*"**2** claims approved and unpaid, totalling **GHS 4,120.00**."*

> ⚠ **The total is only shown when every row is in the same currency.** Mixed currencies say
> nothing rather than adding up numbers that do not add up — the same discipline as the dashboard
> (chapter 12).

**Seven columns:** **Number** *(a link)* · **Traveller** · **Type** · **Claimed** · **Payable** ·
**Submitted** · **Status**.

### ▶ Walk it

**1 — Open on *All claims*.** One row: `EXP-2026-00001`, **Efua Seidu**, *Post Travel*, GHS 490.00 claimed,
GHS 490.00 payable, **Submitted**.

**2 — Switch to *Awaiting payment*.** Empty — *"Nothing is waiting to be paid."*

> *"Nothing waiting to be paid — because the one claim we have has been submitted and not yet
> reviewed. Which is the right state: 'approved and unpaid' is the finance desk's actual worklist
> and the one it is measured on, so it is a purpose-built queue rather than a status filter."*

**3 — Switch back and open the claim.** Continue into chapter 9.

### ⚙ Behind the page

| View | Endpoint |
|---|---|
| All claims | `GET api/staff-travel/finance/claims` |
| Awaiting payment | `GET api/staff-travel/finance/claims/unpaid-approved` |

Both `HR.Travel.Read`. Table: `StaffTravelExpenseClaims`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-33 · Unpaged and unfiltered** — no traveller, date, trip or status filter, though by-employee, by-status and by-request reads all exist | |
| **T-34 · No export**, on the one screen where finance would most want one | |

---

## 8a. `/hr/travel/advances` — the cash still out

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Advances** · `/hr/travel/advances` ·
as **hr.head** · **2 minutes** *(new in lane 3, D-5)*

### 📖 What it is

> *"Cash paid out ahead of trips — who is still holding it, and whose deadline has passed. The half of an
> advance organisations lose is the settling of it, so the chase list has its own screen."*

### 👁 On the page

**Header:** *Travel advances* — *"Cash paid out ahead of trips — what travellers still hold, and what is past its
deadline."*, back-link.

**Three view buttons:**

| View | Shows |
|---|---|
| **Overdue settlements** *(opens here)* | cash still out **past its settlement deadline** — read by the server there and then, whether or not the nightly sweep has marked the advance *Overdue* yet |
| **Cash out** | every advance paid out and not yet settled — *Disbursed*, *Partially settled* or *Overdue*, with something outstanding |
| **All advances** | every advance, whatever its state |

On the first two, when every row shares a currency, a card: *"**1** advance with cash out, travellers holding
**GHS 2,500.00**."* — and, as on chapter 8, nothing at all when the currencies are mixed.

**Seven columns:** **Number** · **Traveller** · **Trip** *(a link to the trip)* · **Approved** ·
**Outstanding** · **Settle by** *(and "N days late" in red)* · **Status** *(an extra **Overdue** badge when the
deadline has passed before the sweep has run)*.

**Read-only.** The footnote says where the work is done:

> *"An advance is settled from its trip's Finance tab: a claim that names it recovers it when the claim is paid,
> cash handed back is recorded there, and a travel administrator can write off what is left. A traveller with an
> overdue advance can take no new one."*

### ▶ Walk it

**1 — Open the page.** *Overdue settlements* — empty: *"Every advance with cash out is within its settlement
deadline."*

**2 — Switch to *Cash out*.** One row: `ADV-2026-00001`, **Kwasi Danquah**, `TR-2026-00001`, GHS 2,500.00
approved, **GHS 2,500.00 outstanding**, settle by **3 Nov 2026**, **Disbursed**.

> *"Two and a half thousand cedis in one traveller's hand, and the date by which it comes back — as a receipt
> against a claim, as cash, or as a write-off signed by somebody who is not the traveller. The day after that
> date it moves to the first view on its own, and no new advance can be taken until it is settled."*

**3 — Click the trip number** — it opens the Kumasi trip; its **Finance** tab (chapter 5.4) shows the same
advance and its actions.

### ⚙ Behind the page

| View | Endpoint |
|---|---|
| Overdue settlements | `GET api/staff-travel/finance/advances/overdue-settlements` |
| Cash out · All advances | `GET …/advances` *(Cash out is filtered on the page)* |

Both `HR.Travel.Read`. Table: `StaffTravelAdvances`. The actions — approve, reject, cancel, pay out, cash back,
write off — are on the trip's Finance tab (chapter 5.4).

### ⚠ Known gaps

| Gap | |
|---|---|
| **As T-33 and T-34** — unpaged, no traveller or date filter, no export | |

---

## 9. `/hr/travel/claims/[id]` — the claim, and the money leaving

### 📍 Where you are

**From:** the claims queue, or a trip's Finance tab · `/hr/travel/claims/[id]` ·
as **hr.head**, then **hr.officer** for the payment · **8 minutes** — the second-best chapter in the module

### 📖 What it is

> *"What somebody spent, line by line, what the organisation agreed to, and what actually went into
> their account — with the advance they were already given taken off automatically."*

### 🚫 **READ RULES 5 AND 6 BEFORE THIS CHAPTER.**

Two people, never one's own (Rule 5); the advance comes back when the claim is **paid** (Rule 6).

### 👁 On the page

**Header:** the claim number, *"{traveller} · {type} · trip {number}"*, a status badge, and the actions — all
for the travel desk only (lane 3, N8):

| Button | Appears when | Who |
|---|---|---|
| **Add an expense** · **Submit** | `Draft` or `Returned` | Write |
| **Review** | `Submitted` or `UnderReview` | Write — never the claimant |
| **Record payment** | `Approved` or `PartiallyApproved` | Write — never the claimant, nor anyone who reviewed the claim or one of its expenses |
| **Void payment** | `Paid` | **Admin** — never the claimant, nor whoever paid it |

**The reviewer's words**, when there are any — a card headed *Reviewer's notes*, or *Returned to the claimant* /
*Rejected* with an amber edge, and who wrote them.

**The claim card:** **Claimed** · **Approved** · **Rejected** · **Advance recovered** · **Net payable** ·
**Against advance** *(its number, or None)* · **Submitted** · **Reviewed** *(who, when)* · then, once paid,
**Paid** *(when, by whom)* and **Payment** *(method, reference)* · **Trip** *(a link)*. Under it, one line says
what the advance did — *"…was recovered from the linked advance when this claim was paid"*, or *"…has not been
recovered yet — that happens when the claim is paid"* — and a waiver, or a voided payment, is shown with its
reason.

**Finance** *(a card, only once something has happened to post)* — what HR's posting register holds for this
claim: the approval's recognition and the payment's settlement, each **Posted** with its journal, or
**Unposted**, **Failed**, **Skipped** or **Reversed** with the reason (§ 16). On UAT no travel rule is switched
on, so the rows read *Unposted*.

**The expenses table — eight columns:** **Date** · **Category** · **Description** *(merchant, "per diem", and a
company vehicle's fuel)* · **Spent** *(the amount and currency spent in)* · **In {claim currency}** *(with the
rate, when it was converted)* · **Approved** · **Receipt** *(the attachment's name)* · **Status** *(and the
reason for anything cut)*. On a draft a pencil changes an expense; on a submitted claim each expense has a
**Review** button.

**Four dialogs:**

**① Add an expense** — category *(17: Airfare · Accommodation · Meals · Local transport · Taxi/rideshare · Car
rental · Fuel · Visa fees · Insurance · Communication · Conference fees · Gifts/entertainment · Tips/gratuity ·
Laundry · Medical · Baggage fees · Miscellaneous)*, date, description, merchant, **amount spent and the currency
spent in** — converted at **Finance's rate for that date**, and refused, with the reason, when Finance holds no
rate for it — a **receipt** picked from the trip's attachments, and **This is a per-diem claim**. Changing an
expense that was reviewed sends it back to be reviewed again.

> ⚠ **Fuel for a company vehicle** *(lane 6, D-30–D-32).* On a trip travelling by company vehicle, a Fuel expense
> names the vehicle's trip and the **litres** — required when no car was hired. When Fleet already logs fuel for that
> trip on that day, the dialog lists it and asks **why it is claimed too** (kept as an internal note on the trip).
> When the claim is paid, the fuel goes into Fleet's fuel log, and voiding the payment removes it. UAT has no fleet,
> so the demo cannot show this.

**② Review this expense** — *"You are recorded as the reviewer, so you will not be the one who pays this
claim."* **Approve** — the whole expense, or less: *"whatever is not approved is rejected, with the reason
below"* — or **Reject**, with the reason. The claimant sees it.

**③ Review this claim** — *"…Decide each expense first — approving the claim approves what its expenses' reviews
approved."* Four outcomes:

| Outcome | Then |
|---|---|
| **Approve** | **Approved** when every expense was approved in full, **Partially approved** when something was cut. Refused while any expense is undecided, or when nothing was approved |
| **Reject** | nothing payable — the reason is required, and the claimant is told |
| **Return to the claimant** | back to them for detail or receipts — the reason is required; it can be changed and **submitted again** |
| **Mark under review** | still being looked at *(offered only on a Submitted claim)* |

**④ Record payment** — *"To {traveller}. The payment date is taken from the clock, and you are recorded as the
officer who paid — it cannot be the claimant or anyone who reviewed the claim or its expenses."* Then:

```
    Approved                      GHS 490.00
    Less advance ADV-2026-00001  − GHS 490.00
    ─────────────────────────────────────────
    To pay                          GHS 0.00
    The advance is recovered as part of this payment;
    the figures are confirmed once it is recorded.
```

then **Method** *(bank transfer, cash, cheque, corporate card)* and **Reference**. *Payroll offset* is not offered
and the server refuses it (D-10): payroll cannot receive a travel claim yet, so it would reach nobody
(`docs/HR/integration/handoffs/HANDOFF-PAYROLL-TRAVEL-CLAIMS.md`).

> ⚠ **The dialog deliberately does not show `netPayable`.** The advance is recovered *inside* the pay call, so
> until it completes `netPayable` still reads as the full approved amount. The split above uses the server's own
> `min(outstanding, approved)` rule and is **labelled anticipated** (Rule 6). An advance in another currency is
> valued at Finance's rate on the day of payment (D-15), so the dialog names it rather than subtracting it.

> ⚠ **The waiver** *(O-2, T-57).* When the traveller still holds advance cash on this trip that the claim does
> **not** name, an amber box says so — *"paid as it stands, the claim pays in full and the advance stays owed"* —
> and **Record payment** stays off until a reason is written. Better: link the advance to the claim.

**Void payment** *(Admin)* — a reason of five characters or more. The payment's Finance journal is reversed (or
its unposted row marked Skipped), the amount it recovered goes back onto the advance, its fuel leaves Fleet's
log, and the claim returns to **Approved**, *"to be paid again or not"*. An internal note on the trip records it.
Not on a closed trip, and not once the advance it recovered from has been written off.

### ▶ Walk it

**1 — Open the Sebrepor claim** (`EXP-2026-00001`, **Submitted**). Three expenses: per diem GHS 250, local
transport GHS 60, fuel GHS 180.

**2 — Read the lines.**

> *"A day at Sebrepor. Two hundred and fifty cedis of subsistence at the Ghana domestic rate — and
> note it is marked as per-diem and linked to the rate it came from, so it is not a number anybody
> invented. Sixty cedis of trotro and taxi. And a hundred and eighty of fuel for the pool vehicle,
> with the receipt attached."*

**3 — Point at the fuel line's receipt.**

> *"And the receipt is not an attachment floating next to the claim — the line points at it. Which
> is what an auditor asks for: not 'were there receipts', but 'which receipt is this line'."*

**4 — Say the policy sentence — and check the database first.**

- **On a rebuilt demo database** the trip was submitted under the approved policy, so its claim was held to it:

  > *"The policy asks for a receipt above a hundred cedis, and within fourteen days of the trip ending. The fuel
  > has one; the sixty cedis of trotro is under the line; and a per diem is a flat daily allowance — there is no
  > receipt behind it to ask for. A claim missing one is refused at submission, and the refusal names the line."*

- **On UAT** the Sebrepor trip was submitted under no policy (Rule 1), so its claim is held to none:

  > *"This trip went ahead before the policy was approved, so its claim is held to no receipt rule. Under an
  > approved policy, a line above the threshold without a receipt is refused at submission — by name."*

**5 — 🔴 LIVE WRITE 8 — review the lines.** On each of the three, press **Review → Approve** *(the whole
amount)* **→ Record**.

> *"Line by line, because a claim is rarely all-or-nothing. In practice this is where a finance officer cuts the
> one that looks wrong — and has to say why, because the claimant is told."*

**6 — 🔴 LIVE WRITE 9 — press *Review*.** Read the dialog description aloud, choose **Approve**, add a note,
**Record the review**.

> *"'Decide each expense first — approving the claim approves what its expenses' reviews approved.' Ticking the
> lines is arithmetic, and the review is the decision. And it records me as the reviewer — which means I am the
> one person who may not now pay it."*

The status moves to **Approved** and **Record payment** appears.

**7 — Go back to `/hr/travel/claims` and switch to *Awaiting payment*.** One row, and the card: *"1 claim
approved and unpaid, totalling GHS 490.00."*

> *"And there is the finance desk's queue, with a real number in it."*

**8 — Come back and press *Record payment* as `hr.head`, choose a method, press it.** Refused: *"You reviewed
claim EXP-2026-00001, so another officer must pay it — the person who decides an amount does not also pay it
out."*

> *"Which is the control. The person who decided how much is not the person who sends it."*

**9 — 🔴 LIVE WRITE 10 — in a second window as `hr.officer`, open the claim and press *Record payment*.**
**Read the dialog before pressing anything.** The Sebrepor trip has no advance, so it shows a plain *Approved*.
Choose a method, add a reference, record it — **Paid**, by `hr.officer`.

**The recovery beat *(optional, four extra minutes — the single most valuable thing in this module)*.** The
Sebrepor claim has no advance; the **Kumasi** trip has the GHS 2,500 one. From the Kumasi trip's **Finance** tab
press **File a claim** — the advance is already chosen (chapter 9b) — add one expense of GHS 490 and **Submit**;
review the expense and the claim as `hr.head`; then open **Record payment** as `hr.officer`:

> *"Four hundred and ninety approved. Less the advance the traveller was already given. To pay: nothing — because
> two and a half thousand cedis went out in cash before the coach left, and this claim is how the corporation gets it
> back. Before this was wired, both halves were paid: the advance went out, the claim was reimbursed in full, and
> the two records never met."*

Record it: the advance moves from **Disbursed** to **Partially settled**, GHS 2,010 still outstanding — chapter 8a
shows it.

*Undo:* **Void payment**, as `hr.head` — an administrator who neither claimed nor paid — with a reason: the claim
returns to **Approved** and the advance gets back what the payment recovered. Putting the claim back to
*Submitted*, and removing a Kumasi claim, is chapter 17.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The claim | `GET api/staff-travel/finance/claims/{id}` | Read |
| Expenses | `GET`/`POST …/claims/{id}/lines` · `PUT …/lines/{id}` | Read / Write |
| A company vehicle's trips and fuel | `GET …/claims/{id}/fleet-fuel` | Read |
| **Review an expense** | `POST …/lines/{lineId}/review` | Write |
| **Submit** | `POST …/claims/{id}/submit` | Write |
| **Review the claim** | `POST …/claims/{id}/review` | Write |
| **Record payment** | `POST …/claims/{id}/pay` | Write |
| **Void payment** | `POST …/claims/{id}/void-payment` | **Admin** |
| Delete a draft claim, or an expense | `DELETE …/claims/{id}` · `…/lines/{id}` | **Admin** |
| Finance card | `GET api/hr/finance-posting/records/source/{id}` | `HR.Company.Read` |

Tables: `StaffTravelExpenseClaims`, `StaffTravelExpenseClaimLines`, `StaffTravelAdvances`, and
`HrFinancePostingRecords` for the Finance card.

> ⚠ **Reviewing is not updating.** `reviewClaim`, `reviewClaimLine` and `approveAdvance` each stamp **who decided
> and when** from the token; the plain update endpoints cannot set those fields. The review DTO takes a
> `NewStatus`, not a boolean — a client typed as `approve: boolean` would leave every claim stuck in
> *UnderReview* and unpayable, which is precisely the mistake a type written from an endpoint's *name* rather
> than its DTO produces.

---

### 9b. `/hr/travel/claims/new` — filing a claim

**From:** a trip's Finance tab · `/hr/travel/claims/new?requestId=…` · **1 minute**

A small form, and its **empty states are the interesting part**:

| Opened… | It says |
|---|---|
| without a trip | *"No trip chosen — Open a travel request and file the claim from its Finance tab."* |
| on a trip not yet approved | *"This trip takes no claims yet — An expense claim is filed once the trip is approved…"* — the server takes a claim only on a trip that is **approved, under way or completed** |
| without travel desk access | *"Not available — Filing an expense claim needs travel desk access."* |

Which is the design: **a claim belongs to a trip and cannot exist without one**, so the screen refuses to start
rather than offering a trip picker that would let somebody file against the wrong one.

With a trip — header *File an expense claim*, *"{traveller} · {trip} · {from} → {to}"* — one card, **The claim**:

- **Type** *(Post Travel · Advance Settlement · Partial Claim · Amendment)*
- **Settle against an advance** — the trip's advances **with cash still out**, *"ADV-… — GHS 2,500.00 still with
  the traveller"*; **the first is chosen for you** (O-2)

**No currency to choose** *(lane 3, B11)*: the claim is kept in the organisation's base currency, and each expense
is converted from the currency it was spent in. Then **Cancel** and **File the claim**, landing on the new claim at
`Draft` with no expenses.

> ⚠ **Choosing the advance here is what links the recovery.** A claim filed with *No advance* while the traveller
> holds cash on the trip can still be filed, but **paying it in full needs a recorded reason** (chapter 9's
> waiver) — the leak T-57 described is closed.

> *Since closure lane 7 (7d, D-38)* the traveller files their own claim from **My travel** (chapter 15) and
> submits it; it arrives in the queue as any other, under their name. The desk's path here is unchanged — it
> still files for a traveller who cannot.

---

### ⚠ Known gaps

None open. *Fixed since the first edition (lane 3):* **T-35** (a receipt above the policy's threshold is
required at submission, a per diem excepted), **T-36** (the claim window binds a first submission), **T-37**
(no claim currency to choose; each expense at Finance's rate for its date), **T-38** (*Return to the claimant*),
**T-39** (*Void payment*), **T-57** (the waiver). The policy rules bind only under an **approved** policy the trip
recorded at submission (Rule 1).

---

## 10. `/hr/travel/visa-requirements` — what a passport needs

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Visa Requirements** · `/hr/travel/visa-requirements` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"The travel desk's reference book: what each passport needs to enter each destination. Recorded
> once, read by every trip — which is why the page says an entry that is wrong is worse than one
> that is missing."*

Since lane 7 (7b, D-39) it is no longer only a reference: **it decides a trip's *requires a visa***. When the
traveller's primary passport is on file (chapter 10a) and the register has an entry for that passport into the
destination, the register's answer is the trip's:

| Requirement | The trip |
|---|---|
| **e-Visa** · **Embassy visa** | needs a visa — and its flight cannot be ticketed until one is approved (chapter 5.3) |
| **No visa needed** · **Visa on arrival** · **Conditional** | does not — read a *Conditional* entry's notes before saying so |
| **Travel prohibited** | the passport is refused entry: the trip **cannot be submitted** |

A different answer on the request stands only with a reason (*"If this differs from the visa register, why"*),
kept on the trip. Without a passport on file, or an entry for the pair, the requester's own answer stands.

### 👁 On the page

**Header:** *Visa requirements* — **"What each passport needs to enter a destination. The travel
desk answers from this, so an entry that is wrong is worse than one that is missing."** — and, once a
destination is chosen, **Add a passport** *(Write)*.

**A destination card** — one country picker: *"The register is read one destination at a time — the API answers
by destination, and so does the travel desk."* **Nothing else renders until you choose one**: *"Choose a
destination — Pick the country being travelled to, and every passport recorded against it appears here."*

**The requirements table** — *Travelling to {country}*, six columns: **Passport** *(the country the passport is
from, the notes underneath)* · **Requirement** *(No visa needed · Visa on arrival · e-Visa · Embassy visa ·
Travel prohibited · Conditional)* · **Category** *(free text — "ECOWAS free movement", "Standard Visitor")* ·
**Max stay** · **Processing** *(days)* · **Last checked** *(the date, and a **source** link)*.

⚠ **Stale entries are flagged** *(lane 7, T-40)*: never checked reads **Never**, and one not checked for a year
reads **"· over a year ago"**, both in amber — here and on the trip's Compliance tab.

Empty state for a chosen country: *"Nothing recorded for this destination — No passport has a requirement here
yet. Until one is added, the travel desk has no answer to give."*

**Dialog** — *Add a passport for {country}*: passport country *(one already recorded for this destination is not
offered)* · **What is required** · category · maximum stay · processing days · **official source** *("Where this
was read from. Visa rules change without notice, so the next person to check needs to know what you checked.")* ·
**last checked against that source** · notes. Editing cannot move an entry to another pair: *"The country pair
cannot be changed. To correct it, remove this entry and add the right one."* Remove is **Admin**.

### ▶ Walk it

**1 — Open the screen** and read the subtitle aloud. It is the best sentence on any travel screen:

> *"'The travel desk answers from this, so an entry that is wrong is worse than one that is
> missing.' Which is why every row carries the official source it came from and the date somebody
> last checked it — and a row nobody has checked for a year turns amber."*

**2 — Choose **Nigeria**.**

> *"A Ghanaian passport into Nigeria: no visa needed, ECOWAS free movement, ninety days, zero processing
> days — sourced from the ECOWAS protocol, checked at the end of August."*

**3 — Choose **United Kingdom**.**

> *"And into the United Kingdom: an embassy visa, Standard Visitor, a hundred and eighty days, **fifteen
> days to process**, applied for at VFS Global in Accra, biometrics in person, allow three working weeks in
> peak season. That is the answer a travel desk gives on the phone, and it is the number that decides
> whether a summit in a fortnight is possible."*

**4 — Tie it back to the trip.**

> *"And this row is not just advice. The London traveller's passport is on file, so this row is what set
> *requires a visa* on the London trip — and why its flight is held from ticketing until the visa is
> approved. Record it once; every future trip to the UK inherits it, and when the rules change you fix one
> row."*

**5 — 🔴 LIVE WRITE 11 *(optional)* — add a requirement.** Choose **South Africa**, **Add a passport**: Ghana,
*Embassy visa*, category *"Visitor's visa"*, 90 days max stay, 10 processing days, with a source URL and today's
date.

*Undo:* the bin icon on the row — Remove is **Admin**, which `hr.head` holds (Rule 2).

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| For a destination | `GET api/staff-travel/compliance/visa-requirements/destination/{countryId}` | Read |
| One pair | `GET …/visa-requirements?passportCountryId=…&destinationCountryId=…` | Read |
| Add · Edit | `POST …/visa-requirements` · `PUT …/visa-requirements/{id}` | Write |
| Remove | `DELETE …/visa-requirements/{id}` | **Admin** |

Table: `StaffTravelVisaRequirements`. The trip reads it through `StaffTravelComplianceRules` — at create, edit,
a group's *Add a traveller*, submission and ticketing.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-41 · There is no reverse view.** You cannot ask "which destinations does a Ghanaian passport enter freely" — only "what does this destination require" | |

*Fixed since the first edition (lane 7):* **T-40** (stale entries flagged), **T-42** (the register sets the trip's
visa flag; a different answer needs a reason; a passport refused entry is not submitted — D-39).

---

## 10a. `/hr/travel/documents` — the passports it is keyed on

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Travel Documents** · `/hr/travel/documents` ·
as **hr.head** · **3 minutes** *(new in lane 7, slice 7a)*

### 📖 What it is

> *"Whose passport, issued where, expiring when — and whether anyone has looked at it. The visa register is keyed
> on the passport; without this page there was nothing for it to read."*

Before lane 7 there was no screen: nothing a person typed reached the visa lookup or the passport-expiry
reminders (E2), while the Compliance tab told users to *"record their passport under travel documents first"*.

### 👁 On the page

**Header:** *Travel documents* — *"Travellers' passports and other travel documents. The visa lookup and the
passport checks read the primary passport; numbers show in full only when a document is opened."* — and **Add a
document** *(Write)*.

**Filters:** **Traveller** *(Every traveller, or one)* and **Show** *(Every document · Expiring within 90 days)*.

**Seven columns:** **Traveller** · **Document** *(its type, and a **primary** badge)* · **Number** *(masked to its
last four — O-7)* · **Issued by** · **Expires** *(an **expired** or **within 6 months** badge)* · **Verified** *(who,
or "Not yet")* · actions: **Verify** *(Write, on an unverified one)*, a pencil, and a bin *(Admin, on an unverified
one)*.

**Dialog** — *Record a travel document*: traveller, **type** *(Passport · National ID · Visa · Resident permit ·
Work permit · Frequent flyer card · Hotel loyalty card · Driving licence · Vaccine certificate)*, number, issuing
country, issued, expires *(after it was issued)*, and **The traveller's primary document of this type**. The rules
are the server's:

- **one primary document per type** — *"marking this one primary stands the traveller's other one down"*;
- **a change takes the verification off** — *"Saving a change takes the verification off — another look confirms
  it again"*; the dialog opens the document in full, the only place the whole number shows;
- **a verified document is not deleted** (O-15).

### ▶ Walk it

**1 — Open the page.** Four documents: three Ghanaian passports — **Akpene Amoah** and **Kojo Fiadzo**, both
verified; **Kwasi Danquah**, not yet — and Kwasi Danquah's driving licence.

> *"Every number cut to its last four. The whole number shows only to somebody who opens the document — which is
> the difference between a register and a list of passport numbers on a screen."*

**2 — Point at Kwasi Danquah's passport** — expires **8 Dec 2026**, *within 6 months*.

**3 — Switch *Show* to *Expiring within 90 days*.** That passport is the one row.

> *"Two months left. The Kumasi trip does not need it — it is domestic — but the next trip abroad would. The
> nightly sweep tells the traveller at ninety, thirty and seven days, and once it lapses (chapter 14). And when an
> international trip is submitted, a passport expiring less than six months after the return comes back as a
> warning."*

**4 — Tie it to chapter 10.** The London traveller's verified passport is what the visa register read to set
*requires a visa* on the London trip.

*(No live write: verifying or changing a document moves the London and Lagos trips' answers.)*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Every document · one traveller's | `GET api/staff-travel/compliance/documents` · `…/documents/employee/{employeeId}` | Read |
| Expiring | `GET …/documents/expiring?daysAhead=90` | Read |
| One, in full | `GET …/documents/{id}` | Read |
| Record · Change | `POST …/documents` · `PUT …/documents/{id}` | Write |
| **Verify** | `POST …/documents/{id}/verify` | Write — the caller is recorded |
| Remove | `DELETE …/documents/{id}` | **Admin** — never a verified one |

Table: `StaffTravelDocuments`. The traveller manages their own from **My travel → Documents** (chapter 15).

### ⚠ Known gaps

| Gap | |
|---|---|
| **Verifying one's own document is not refused** *(seen while writing lane 10)*. A travel officer can verify their own passport; the two-person rule (Rule 2) does not reach verification. TDC's call whether it should | |

---

## 11. `/hr/travel/alerts` — what travellers are told

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Destination Alerts** · `/hr/travel/alerts` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Security, health and disruption advisories against a destination, time-boxed so they expire on
> their own — and sent to the people already going."*

**Since lane 7 (7a, E6) an alert raised *active* goes out at once** to the traveller of every **approved or
under-way** trip to its country — to its city, when it names one — whose dates meet the alert's window. Each is
recorded on the trip, reaches the traveller in the app and by email (lane 8), and waits on their portal for them
to confirm they have read it; the HR desk is told in the app, but not whoever raised it. A trip approved later is
sent it from its **Compliance** tab (chapter 5.5). **Raised inactive, it goes to nobody** — and ticking *Active*
later sends nothing either: send it from each trip.

> ⚠ **On the demo database this reaches the demo's own trips.** A new active alert for **Ghana** or **Kumasi**
> dated over 19–20 October reaches the Kumasi trip and its traveller. Raise demo alerts for a city no demo trip
> visits, or raise them inactive.

### 👁 On the page

**Header:** *Destination alerts* — *"What travellers are told about security, health and disruption
where they are going."*, with a **Raise an alert** button *(Write)*.

**Five columns** — every alert marked **active**, whatever its dates: **Alert** *(the title, its type underneath)* ·
**Where** *(country · city)* · **Severity** *(Info · Warning · Critical · Emergency)* · **In force from** ·
**Actions** *(edit — Write; remove — **Admin**)*.

Empty state: *"No active alerts — Nothing is in force for any destination. A trip to a country with
no alert shows nothing on its compliance tab."*

**Dialog** — *Raise a destination alert*: **title** · **type** *(Security · Health outbreak · Weather · Political
unrest · Transport disruption · Natural disaster)* · **severity** · **country** · **city** *("the whole country if
left blank")* · **What travellers need to know** — *"This is the alert. A title and a severity with no text tells
a traveller nothing they can act on."* · **source** · **in force from** and **until** *(blank while open-ended)* ·
**Active**. Its description says where it will go.

**Remove** *(Admin)* — refused once the alert has reached a trip: *"…has reached N trip(s), so it is not deleted —
deactivate it instead (untick Active)."*

### ▶ Walk it

**1 — Open the screen.** Three alerts: **Lagos** *(Security, Warning)*, **Kumasi** *(Weather, Info)*, **London**
*(Transport disruption, Info)*.

**2 — Open the Lagos one** *(the pencil — the list carries no body)* and read it in full. The body is the point:

> *"Lagos — road disruption and protests on the Badagry expressway. Warning. 'Fuel-supply protests
> are causing long queues and intermittent road closures between Mile 2 and Badagry. Travellers
> should use the Lekki–Epe corridor and allow two extra hours for airport transfers.' Source: the
> Ghana Mission in Lagos."*

Close it with **Cancel**.

**3 — Say why the body matters.**

> *"And that paragraph is the whole feature. An alert that says 'Civil unrest · High' and nothing
> else tells a traveller nothing — it has to say what, where, and what to do instead. That is what
> lands on the traveller's own travel page, and what they confirm they have read."*

⚠ That is not rhetorical. **A shipped screen showed alerts with no text for months**, because the
"current alert for a country" read returned a summary DTO with no `body` and the compliance strip
renders the body conditionally — so a traveller saw the headline and never the advice. Fixed, and
worth knowing the shape of. **T-43**, closed.

**4 — Point at the two dates.**

> *"In force from and until. An advisory that never expires is an advisory nobody reads, so these are
> time-boxed and drop off on their own."*

**5 — 🔴 LIVE WRITE 12 *(optional)* — raise one.** Type **Health outbreak**, severity **Warning**, a country **no
demo trip visits**, a title and a real body, a source, and a two-month window.

**6 — Then say where it goes.**

> *"Raised active, it goes at once to everyone already approved to travel there in those dates — in the app and by
> email — and each of them, and only they, confirms they have read it. Nobody can confirm on your behalf that you
> read a security briefing about where you are going. I will show you that side of it in the portal."*

*Undo:* untick **Active** in its edit dialog; or remove it — **Admin**, and only while it has reached no trip.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Active alerts | `GET api/staff-travel/compliance/alerts/active` | Read |
| One alert, in full | `GET …/alerts/{id}` | Read |
| For a country | `GET …/alerts/country/{id}` · `…/country/{id}/current` | Read |
| **Raise** · Edit | `POST …/alerts` *(sends an active one to the trips it meets)* · `PUT …/alerts/{id}` | Write |
| Remove | `DELETE …/alerts/{id}` | **Admin** — never once sent |
| Send it to one trip's traveller | `POST …/alert-notifications` *(the trip's Compliance tab)* | Write |
| *(the traveller's own, and their acknowledgement)* | `GET`/`POST api/staff-travel/me/alert-notifications…` | signed-in, the traveller only |

Tables: `StaffTravelAlerts`, `StaffTravelAlertNotifications`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-45 · An alert does not block a booking** to the destination it warns about, at any severity — including `Emergency` | *Flagged since lane 5: a Critical or Emergency alert in force over the trip shows as a warning on the request page — for the approver and the desk. Blocking is TDC's question* |

*Fixed since the first edition:* **T-44** (lane 7 — an active alert goes to the trips it meets; lane 8 — in the
app as well as by email), **T-46** (lane 4 — a trip at risk level *Prohibited* is refused at submission and at
every approval stage).

---

## 12. `/hr/travel/dashboard` — where the organisation's travel stands

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Dashboard** · `/hr/travel/dashboard` · as **hr.head** ·
**4 minutes**

### 📖 What it is

> *"The shape of the corporation's travel: how much is in flight, what it is costing, who is going
> where and what is waiting on somebody."*

### 👁 On the page

**Header:** *Staff travel dashboard* — *"Where the organisation's travel stands."*

**Four tiles:** **Requests** · **Awaiting approval** *(submitted; "N still in draft" when there are drafts)* ·
**Departing in 30 days** *(approved or under way, leaving within thirty days)* · **High risk or above** *(High, Critical or
Prohibited; "N international" underneath)*.

**Estimated cost card** — cancelled and rejected requests left out:

- **When every request shares one currency** *(as on this database)*: three big figures —
  **Estimated**, **Approved budget**, and **Across N requests**.
- **When they do not**: a table with a row per currency — Currency · Estimated · Approved budget ·
  Requests — followed by the sentence:
  > *"Travel is costed in N currencies. These are not added together — doing so would need an
  > exchange rate, and travel takes rates from Finance rather than inventing one. Cancelled and
  > rejected requests are excluded throughout."*

**By status** — a breakdown. **Top destinations** — where people are going. **Requests raised, last six
months** — a trend. **Three lists:** **Awaiting approval** · **Departing soon** · **Recently raised**, each five
columns — Number *(a link)* · Traveller · Route · Departs · Status.

### ▶ Walk it

**1 — Open the dashboard.** Read the tiles. On UAT, as written *(4 October 2026)*: **4** requests, **2** awaiting
approval *(Lagos and London)*, **1** departing in 30 days *(Kumasi, 19 October)*, **0** high risk — *2
international*.

**2 — Point at *High risk or above* and its hint.**

> *"High risk, and how many are international — which are not the same question. A field visit to a
> site with no road access is domestic and high risk; a conference in Copenhagen is international
> and low."*

**3 — Read the *Estimated cost* card.**

> *"A hundred and four thousand five hundred and fifty cedis in flight across four requests — and four thousand
> five hundred and fifty of it approved: the two domestic trips. The two international ones are still waiting for
> their approvals."*

**4 — Now say the currency sentence even though you cannot see it**, because it is the best piece of
engineering judgement on the screen:

> *"Everything here is in cedis today, so you get one number. The moment a trip is costed in dollars
> the screen stops adding them up and shows you one row per currency instead, and says why — because
> converting would need an exchange rate, and travel takes its rates from Finance rather than
> inventing one. Which is a deliberate choice: a converted headline that is confidently wrong is
> worse than a split one that is visibly incomplete."*

**5 — Finish on the three lists.**

> *"And the three questions a Head of HR asks about travel: what is waiting on me, who is leaving
> soon, and what has just come in."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Everything | `GET api/staff-travel/requests/dashboard?upcomingDays=30` | `HR.Travel.Read` |

One request. `upcomingTrips` is **a list, not a count** — a client type that said otherwise would
render `undefined` on a screen that type-checks cleanly, which is exactly what happened once.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-47 · No date range.** The dashboard is always "now"; there is no way to ask about last quarter | |
| **T-48 · Actual spend is not on it.** Estimated and approved budget are; the sum of paid claims and advances — the number a finance director actually wants — is not, though each trip's budget derives it (chapter 5.4) | |

---

## 13. `/administration/hr/travel/policies` — the caps, and who signs them

### 📍 Where you are

**Sidebar:** Administration → HR → **Travel** → **Travel Policies** ·
`/administration/hr/travel/policies` (+ `new`, `[id]`) · as **hr.head**, with **hr.officer** in a second window ·
**6 minutes**

### 📖 What it is

> *"What staff may spend on travel — and, more importantly, whether anybody has signed it."*

### 🚫 **READ RULE 1 AND RULE 4 BEFORE THIS CHAPTER.** This is where they both land.

### 👁 Screen 1 — the register

**Header:** *Travel policies* — **"What staff may spend on travel, and what the caps refuse."**, with a **Draft a
policy** button.

**Columns:** **Policy** · **Version** · **Effective** · **Rules** *(a count)* · **State** · **Approved by**.
**State** is one of *Draft — not enforcing*, *Approved — in force from …* (a version approved to start
later), *In force*, *Not in force* (superseded) or *Expired* (Rule 1's table).

**Row actions** (`HR.Travel.Admin`, which the HR role holds since closure lane 4):

- **Approve**, on a draft. It is **refused to whoever drafted or last changed the policy** (C3), with the
  reason on screen: *"You drafted or last changed … so another travel administrator must approve it"*. So
  `hr.officer` signs what `hr.head` wrote.
- **Withdraw**, on an approved one — the way to stop it binding.

Approving a version makes room among its scope's versions **by date** (O-4): one approved to start later stays
*Approved — in force from …* until its day, and the version it replaces stays in force until the day before.

Empty state: *"No travel policies — without one, travel bookings are not capped at all."*

### 👁 Screen 2 — the policy (`…/policies/[id]`)

**Header:** the policy name and version, its state, and — **on a draft only** — **Edit** and **Delete**. The edit
page's subtitle says why: *"Only a draft can be corrected — an approved policy needs a new version."*

**On an approved policy**, a card above the rest: *"Approved by … on …. An approved policy cannot be edited or
deleted — raise a new version to change what it allows, or withdraw it from the register to stop it capping
bookings. A new version approved for the same scope takes over from its own start date; this one stays in
force until the day before."*

**Card 1 — Scope:** organisation unit (*The whole organisation* when none) and the staff-level band. A unit's
policy covers the units **under** it; the nearest unit's wins (O-5).

**Card 2 — "The caps that refuse a booking":** max cabin domestic / international, hotel domestic /
international per night, **the currency of the limits**, max per trip, receipt required above, days to submit
expenses, book flights ahead, book hotels ahead, preferred vendors. **All of them bind** once the policy is
approved (§ 1.5). *Requires cheapest fare* and *max annual budget* are gone from the form — nothing could
enforce them (D-1).

**Card 3 — the rule register:** **read-only**, with an explanatory note. Columns: rule code · rule name · **type**
*(Hard limit · Soft limit · Warning · Mandatory · Preferred · Prohibited)* · expense category · **limit** and unit
· **violation action**.

### ▶ Walk it

This is the governance chapter. Take the six minutes. **First look at the State column** — the walk forks on it.

**1 — Open the register.** One row: *TDC Staff Travel Policy 2026*, version 1, five rules.

- **On UAT:** *Draft — not enforcing*; Approved by an em dash.
- **On a demo database built since 2026-10-04:** *In force*, approved by `hr.officer` (D-60).

**2 — Open the policy and read Card 2.**

> *"Economy on domestic flights, premium economy on international. Nine hundred cedis a night at home, two
> thousand four hundred abroad. Flights booked three weeks ahead, hotels two. Seventy-five thousand cedis a trip at
> most. A receipt for anything over a hundred cedis, and fourteen days after the trip to claim. Every one of
> those binds: a booking, a submission or a claim that breaks one is refused — or, if the desk asks for an
> exception and gives a reason, it waits for a second officer to authorise it."*

**3a — On UAT (a draft): the Draft sentence. This is the thirty seconds.**

> *"And none of that is in force yet, because nobody has signed this policy. That is deliberate. Before this rule
> existed, anybody who could edit a travel policy could write the rule constraining everybody's travel spending
> and have it bind immediately — the person spending the money could set their own limit. So a policy is a
> **draft** until an administrator approves it, and until then it caps nothing."*

Point at **Approve** as `hr.head`. It is refused by name, which is safe to show:

> *"And I cannot sign it — I wrote it. Approving is a travel administrator's act, and never the author's. A
> second officer signs. Writing the rules and putting them in force are two different acts, and the system holds
> them apart by who did what."*

**🔴 LIVE WRITE 13** *(only if you chose § 2.4 Option B and kept it for the room)*: in the second window, as
**`hr.officer`**, **approve it**. The State becomes *In force*, with that officer in *Approved by*.

⚠ **CAREFUL:** from that moment it binds **every** trip submitted afterwards, on the whole tenant. Trips already
submitted keep the policy they recorded — on UAT, Kumasi and Sebrepor none, so their claims are held to nothing;
Lagos and London carry it from a hand link made before lane 1, so their claims are held to it from then on.

**3b — On a rebuilt demo database (in force): say who signed it, then show it working.**

> *"Signed — by the second officer, not by me: I drafted it, and the system will not let the author put their own
> rules in force. And it is working right now."*

Open **Staff Travel → Policy Breaches** (chapter 13a): London's Hilton, 3,200 a night against the 2,400 ceiling,
**booked by `hr.head`, authorised by `hr.officer`**.

> *"The booking was not refused — the summit hotel was genuinely needed — but it could not be confirmed until an
> officer who neither booked it nor travels on it authorised the breach. Here is who, and when."*

**4 — Scroll to Card 3, the rule register**, and read the five rules.

> *"And underneath, the rule register: the hotel ceilings, the cabin-class ceiling, twenty-one days' advance
> booking, a receipt above a hundred cedis — each with a violation action saying whether it warns, requires
> approval, or blocks."*

**5 — Now say the Rule 4 sentence, the second-best governance moment in the module.**

> *"And this table is read-only — deliberately. What binds is the policy's own settings you just read; this
> register describes a finer mechanism that is not evaluated by anything yet. So rather than give you an editor
> for rules that do nothing, we left it read-only. An editable control that does nothing is worse than no
> control — a rule set to 'Block' is a promise to whoever configured it. The write endpoints exist and are
> tested; the editor comes back on the day evaluation lands, not before."*

That answer is better than the feature would have been.

**6 — 🚫 Do not press *Delete* on the policy** *(Admin)* — the screen offers it on a draft only anyway.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-travel/policies` | Read |
| Current / applicable | `GET …/policies/current` · `…/applicable` | Read |
| Create / update | `POST` / `PUT …/policies[/{id}]` | Write — **update is refused once approved**; the version is the server's (T-50) |
| **Approve** | `POST …/policies/{id}/approve` | **Admin** + employee-linked; **never the author** (C3) |
| **Withdraw** | `POST …/policies/{id}/withdraw` | **Admin** |
| Delete | `DELETE …/policies/{id}` | **Admin**; a draft only |
| Rules | `GET …/policies/{id}/rules` · `…/rules/active` | Read |
| Rule writes | `POST`/`PUT`/`DELETE …/rules…` | Write / Admin — **no screen calls them** (Rule 4) |
| Policy exceptions | `GET …/exceptions/request/{id}` · `…/exceptions/pending` · `POST …/exceptions` · `…/{id}/decide` | Read / Write / Admin — **no screen**; decided by an administrator who did not raise it (C4, T-49) |
| Booking breaches | *Staff Travel → Policy Breaches* — chapter 13a | Read; authorise / refuse **Admin**, never the booker, the asker or the traveller (D-8) |

Tables: `StaffTravelPolicies`, `StaffTravelPolicyRules`, `StaffTravelPolicyExceptions`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-1 · UAT's policy is a draft**, so no cap binds there. Kept so by D-61 until the next demo is prepared; a demo database built by the pack since 2026-10-04 has it approved (D-60). **Approving it on UAT changes every later trip** — Rule 1 | |
| **T-4 · The rule register is enforced by nothing and ships read-only** — a decision, not a defect (finish plan D-29) | |
| **T-49 · The policy-exception register has no screen.** Create and decide are implemented and tested, decided by an administrator who did not raise it. A *booking's* breach — the one that matters day to day — has its screen: Policy Breaches (D-8) | |
| **T-51 · `AppliesToLevelFromId` targets `StaffLevel`**, not a salary level — informational: an FK's name is not its target | |

*Fixed since the first edition:* T-2 (HR holds Admin, narrowed by the act — D-3), T-9 (the limits have a
currency), T-50 (versions numbered by the server, dated by approval — O-4).

---

## 13a. `/hr/travel/breaches` — the bookings over the policy, and who let them through

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Policy Breaches** · `/hr/travel/breaches` ·
as **hr.head**, then **hr.officer** to decide · **2 minutes** *(new in lane 4, D-8)*

### 📖 What it is

> *"Every booking above the policy's caps, or booked later than it asks — and who let it through. The booking is
> not refused; it waits, and the person who decides it is never the person who made it."*

A flight or hotel booking that breaches its trip's policy (a cabin class or nightly rate above the cap, or less
notice than the policy asks) is refused at booking — **unless the desk asks for an exception, with a reason**
(chapter 5.3). Then it is saved **Pending** and **cannot be confirmed or ticketed** until a travel administrator
decides it here. Only an **approved** policy binds (Rule 1), so on UAT, where the policy is a draft, nothing breaches.

### 👁 On the page

**Header:** *Policy breaches* — *"Bookings above the travel policy's caps, or booked later than it asks, and who
decided them."* — and two view buttons: **Awaiting authorisation** *(opens here)* and **All**.

**Five columns:** **Trip** *(its number, a link; the traveller and departure underneath)* · **Booking** *(what it is,
and its status)* · **Policy cap** · **Why** *(the reason given, and "asked by …")* · **Exception** *(Awaiting
authorisation · Authorised · Refused, with who decided and when)* — and, for a travel administrator, on a pending
row: **Authorise** and **Refuse**.

**Refuse** asks for a reason *(five characters or more)*: *"the booking stays as it is and cannot be confirmed or
ticketed; the desk changes or cancels it. The reason is kept on the trip as an internal note."*

The footnote is the rule: *"A travel administrator decides an exception — never the one who booked it or asked for
it, and never the traveller. Until it is authorised the booking stays Pending and cannot be confirmed or
ticketed."* The buttons are shown to every administrator; the server refuses the three people it names, with the
reason.

Empty state: *"Nothing awaiting authorisation — A booking that breaches its trip's policy appears here once the
desk asks for an exception."*

### ▶ Walk it

- **On a rebuilt demo database:** switch to **All**. One row — London's trip; *Hilton London Metropole · GHS 3,200.00 a
  night*, **Confirmed**; cap *2,400.00 a night*; the reason, *asked by* the head of HR; **Authorised**, by
  `hr.officer`.

  > *"Three thousand two hundred a night at the summit venue, against a ceiling of two thousand four hundred. Asked
  > for by the person who booked it, authorised by an officer who neither booked it nor travels on it — and only
  > then confirmed. Here is who, and when."*

- **On UAT:** both views are empty — the policy is a draft, so no booking breaches it (Rule 1). If you prepared
  § 2.4 Option B, chapter 5.3's Kumasi hotel lands here; authorise it as `hr.officer`.

  > *"Empty — because the policy on this database has not been signed. Once it is, a booking over its caps waits
  > here, and the person who booked it cannot wave it through."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The register | `GET api/staff-travel/bookings/exceptions[?state=Pending]` | Read |
| **Authorise** | `POST …/flights/{id}/exception/authorise` · `…/hotels/{id}/exception/authorise` | **Admin** + employee-linked |
| **Refuse** | `POST …/flights/{id}/exception/refuse` · `…/hotels/{id}/exception/refuse` *(a reason)* | **Admin** + employee-linked |

The exception lives on the booking — `StaffTravelFlightBookings`, `StaffTravelHotelBookings`. The older
`StaffTravelPolicyExceptions` register (a trip-level exception) is a different thing and still has no screen — T-49,
kept by decision (D-29).

### ⚠ Known gaps

None open. This screen is lane 4's answer to D-8: before it, a booker holding `HR.Travel.Admin` authorised their own
breach.

---

## 14. `/administration/hr/travel/reminders` — the sweep

### 📍 Where you are

**Sidebar:** Administration → HR → Travel → **Travel Reminders** ·
`/administration/hr/travel/reminders` · **needs `HR.Travel.Admin`, which the HR role holds since
closure lane 4 (D-3, T-52)** · **3 minutes**

### 📖 What it is

> *"The thing that watches the dates nobody else is watching: a passport expiring, a visa about to
> lapse, an advance past its settlement deadline, a departure coming up — and, since closure lane 8, a
> visa still missing, a request nobody has decided, a risk briefing not acknowledged, a claim window
> about to close. Each goes to the people who act on it."*

*Closure lane 8 (slice 8b — D-49, D-50, F2).* Before it, every reminder went to the HR role in the app only, and a
document got one notice at 90 days and the next once it had lapsed. Now:

| Kind | When | To |
|---|---|---|
| Travel document expiring | 90, 30 and 7 days before; then after a week and a month lapsed | its owner (in the app and by email; by email alone without a login; the desk told to tell them with neither) |
| Visa expiring | the same rungs, on a trip still to happen or under way | the traveller |
| Advance settlement overdue | the deadline passed; after a week; after a month | the traveller and the desk |
| Trip departing | once, 14 days or less before an approved trip | the traveller |
| Visa missing | once, 14 days or less before an approved trip needing a visa with none approved (or *not required*) | the traveller and the desk |
| Approval waiting | 5 days after submission; after a week; after a month | whoever its current stage asks — named approvers and the stage's role holders, never the traveller — by email too; the desk when nobody can be asked |
| Approval escalated | once, 3 days or less before departure (or after it), still waiting | the desk (O-11) |
| Briefing unacknowledged | once per assessment, 7 days or less before departure | the traveller |
| Claim window closing | once, 7 days before a completed trip's claim window closes, no claim submitted | the traveller |
| Claim window passed | once, closed with a claim not submitted or advance cash still out | the desk |
| Fleet returned *(8c)* | once, when every company vehicle of a trip still under way is back in Fleet | the desk — to mark it completed if the traveller is back too |
| Fleet incident *(8c, D-29)* | once per incident Fleet records on the vehicle of a trip not yet closed | the desk (in the app, with Fleet's title), and the traveller's nearest line authority with a login (in the app and by email, without it) |

The windows are constants, each a question for TDC (closure plan § 6). Each reminder is sent once; a sweep that stopped
between recording and sending one leaves it for the next. The words and recipients of each are on **Administration →
Notification Topics**, under `StaffTravel.`.

*Closure lane 8 (slice 8c — D-6, D-47, D-48, D-51; T-7).* The sweep also **moves trips**, first thing in each run, so
its reminders read them as they now stand:

| Move | When | Who is told |
|---|---|---|
| Approved → **under way** | on the departure date — or before it, once Fleet dispatches one of the trip's company vehicles | nobody (the traveller was told it is departing) |
| Under way → **completed** | the day after the trip ends (the desk's *Mark completed* stays for an early return) | the traveller, with the last day to file a claim — while that day is still ahead |
| Completed → **closed** | once the claim window has passed (the approved policy's, or 30 days with none) **and** every claim is paid or rejected and every advance settled — the *Close trip* verb's own test | nobody |
| A group trip → **under way**, **completed** | once any traveller's trip is under way; once every traveller's trip is completed or closed | nobody |

An approved trip already past its end takes several steps in one run. Each move is logged beside the reminders (kinds
*Trip started*, *Trip completed*, *Trip closed*, *Group started*, *Group completed*) and listed in the preview before
it is made. A trip moved under way that did not happen is the desk's **Did not travel** (chapter 5).

*Closure lane 9 (slice 9a, D-54).* Each run also puts travellers' attendance right: every approved, under-way,
completed or closed trip that has not ended, or ended in the last 14 days, holds its working days as *On duty*; a trip
cancelled or sent back holds none. It repairs what a path missed — the days themselves go on and off as the trip is
decided (chapter 5.1). The run's toast says how many days it added and removed.

### **This whole screen is Admin-gated, reads included.** Before closure lane 4, `hr.head` got a 403
on the page itself. Since D-3 the HR desk opens it, because the desk that renews an expiring
passport is the one that needs the queue (T-52). ⚠ **Run a sweep now** sends real reminders — to
travellers and approvers by email too, since lane 8 — each only once, and moves trips, so treat it as a live write.

### 👁 On the page

**Header:** *Travel reminders* — *"Expiring passports and visas, overdue advances, departures, waiting approvals,
closing claim windows and Fleet's word — each to the people who act on it; and trips moved under way, completed and
closed."*, with a **Run a sweep now** button. Its toast says how many were sent and, since 8c, how many trips and groups
it moved.

**Card 1 — What the sweep chases** — the twelve kinds, their windows and who each reaches *(the table above)*.

**Card 2 — What the sweep moves** *(8c)* — the moves *(the second table above)*, and a line on *Did not travel*.

**Card 3 — What would fire** — a preview, with **Evaluate as if it were** *(a date)* and **Back to today** —
*"Nothing is sent by previewing. Set a future date to see what is coming."* Six
columns: **Kind** · **Record** · **Due** · **Days** · **Tier** *(how urgent)* · **Sent to** *(Traveller, Desk,
Approvers — lane 8; Line Manager — 8c; empty for a silent move)*. Its count line says how many would be sent and how many
trip or group moves would be made. Empty state: *"Nothing due."*

**Card 4 — Recent sweeps** — the run history: Started · Finished · Trigger · Queued. Empty state:
*"No sweeps yet — nothing has run for this tenant."*

**Card 5 — Sent and moved in the last 14 days** — the dispatch log, the moves among the reminders, with **Sent** — when
the sweep sent it (or made the move), or *Not yet — next sweep* for one recorded and not sent (lane 8).

### ▶ Walk it *(read-only, as `hr.head`)*

**1 — Open the page.** Read Card 1's twelve kinds and Card 2's moves aloud, briefly.

> *"The thing that watches the dates nobody else is watching. It chases twelve kinds of date — a passport, a visa,
> an advance, a departure, an approval nobody has decided, a claim window about to close — each to the person who
> acts on it. And it moves the trips themselves: under way on the day, completed the day after, closed once the
> money is settled."*

**2 — Point at *Recent sweeps*.**

- **On UAT:** hundreds of rows, one a day since the hosted sweep started — most of them the test harness's.
- **On a rebuilt demo database:** none until 11 minutes after the API starts, then one a day.

> *"It runs every night with nobody signed in. Nobody has to remember to press anything."*

**3 — The preview: set *Evaluate as if it were* to a month before `head.dev`'s passport expires** — on UAT it expires
on 8 December 2026, so 8 November *(chapter 10a shows the date on a rebuilt database)*. Find it at its **30-day**
rung.

> *"And the preview tells you what it would send before it sends it — so you can ask 'what will this look like next
> month'. There is the one we planted: a passport with a month left. Its owner is told at ninety, thirty and seven
> days, and once it lapses. It is found by a job rather than by somebody remembering."*

Press **Back to today**.

**4 — 🚫 Do not press *Run a sweep now* during a demo.** It dispatches, and it moves trips.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| **Run** | `POST api/staff-travel/reminders/run` | **Admin** |
| Preview | `GET …/reminders/preview?asOf=` | **Admin** |
| Run history | `GET …/reminders/runs` | **Admin** |
| Dispatch log | `GET …/reminders/log` | **Admin** |

Tables: `StaffTravelReminderRuns`, `StaffTravelReminderDispatchLogs` (`PublishedAt` — when the sweep sent it; batch 1's
column, written since lane 8). The reminders themselves are `Notifications` rows, one per recipient and channel, on
the `StaffTravel.*` topics.

### ⚠ Known gaps

None open. *Fixed since the first edition:* **T-52** (lane 4, D-3 — the HR role holds `HR.Travel.Admin`, so the desk
opens this screen), **T-53** (wrong when written: the sweep has run daily on a hosted service since 2026-08-17, first 11
minutes after the API starts; lane 8 proves the scheduled run on its own, `run-final-sweep-scheduled.mjs`), **F2** (lane
8, 8b — the people who act on each reminder, by email too, and the new kinds), **T-7** (lane 8, 8c — the sweep moves
trips). The windows are constants — each one TDC's to confirm (closure plan § 6).

---

## 15. `/me/travel` — the portal

### 📍 Where you are

**Portal:** Time, Leave & Pay → **My Travel** · `/me/travel` (+ `new`, `[id]`, `[id]/edit`, `documents`, `claims/[id]`) ·
**as the `staff` persona**, in window C · **5 minutes**

### 📖 What it is

> *"The traveller's own side. Six screens, no permissions, and the acts only they can do."*

### 👁 The six screens

**`/me/travel` — My travel.** *"Trips you have requested, and where each one has got to."* Five
columns: Number · Route · Dates · Estimated · Status. Plus the **destination-alerts panel** (below), and
**My travel documents** in the header *(closure lane 7, slice 7c1)*.

**`/me/travel/new` — Request travel.** Chapter 4's form on the **`self` surface**: the traveller and
the initiator fields are **absent, not disabled** — because the self-service endpoint overwrites
them server-side, and a disabled control would imply the value was sent. Subtitle: *"Saved as a
draft. Submit it when you are ready for approval."*

**`/me/travel/[id]` — one trip.** *Before closure lane 7 this was one card of the trip and the desk's
shared notes — nothing the desk arranged reached the traveller (E7).* Since closure lane 7, six tabs (four from
slice 7c1, *Messages* and *Files* from 7c2), plus **Send for approval** on a draft, **Recall**, **Request change**
and **Withdraw** as each applies:

- **The trip** — the request and its lifecycle notes.
- **Before you go** — the **risk assessment** with **I have read this** (below); every **alert in force**
  for the destination over the trip, with its text — including ones never sent to this traveller; the
  destination's **health requirements**, each *cleared by the travel desk* or not; the **visas** (the number
  to its last four); the **insurance** (policy number, cover dates, the emergency line). While a Critical trip
  waits for the acknowledgement, or an international one for cover, the card says the flight is not
  ticketed until it is.
- **Itinerary & bookings** — the itinerary **the desk finalised**, its legs and activities; while the desk is
  drafting one, *"The travel desk is planning your itinerary"* instead (D-42). Every flight with its segments
  — flight numbers, times, terminals, seats, baggage — hotels with their addresses, ground transport (a company
  car's vehicle and driver), car rentals. **Not shown:** who asked for or authorised a policy exception on a
  booking, and why — the desk's decision, kept off the traveller's read as the request's policy exceptions are.
- **Money** — the advances: asked, approved, paid, what the traveller still holds and by when it is settled;
  the claims and where each has got to, each opening on its own page. **File a claim** *(closure lane 7, slice 7d —
  D-38)* on a trip that is approved, under way or completed: the type, and the advance it settles — one still out
  with the traveller is offered first — then on to the claim.
- **Messages** — the desk's shared notes (internal notes never reach this browser), threaded, with **Reply** on
  each thread and a **Write to the travel desk** box for a question of the traveller's own (D-41). A reply is
  saved as a *Response*, a new message as a *Query*; neither can be changed once sent — *"Messages are kept with
  the trip and cannot be changed once sent."* The desk reads them on its Comments tab (5.6).
- **Files** — every file on the trip, the desk's and the traveller's (*You*), with **Download**; **Attach** (a
  type and a description; scanned before it is stored) on any trip not cancelled, rejected or closed; a remove
  button on the traveller's **own** files **only while the trip is a draft or returned to them** (D-40) — after
  that *"This trip has been sent for approval, so a file you added stays — ask the travel desk if one should go."*

**I have read this — the risk assessment.** *Only the traveller can record it, and only here* (E1). Before
slice 7c1 nobody could: the desk route needs `HR.Travel.Write`, which the `Employee` role never holds, and the
service accepts only the traveller. A Critical trip's flight is not ticketed until it is recorded (D-37). The
desk's Compliance tab now only reports it.

**What tells the traveller** *(closure lane 8, slice 8a — D-4, D-45)*. The bell and an email, each opening
the trip on the tab where they act (`/me/travel/[id]?tab=…`, or the claim's own page): submitted for them
by the desk, approved, rejected, returned for changes, sent back for a change or cancelled by someone else;
an advance approved, paid out (with the date to account for it by) or rejected; a claim returned, approved
(in full or in part), rejected or paid (saying when the advance covered it); a destination alert; a risk
briefing to acknowledge (again if its level rises); a note the desk shares. **Nothing about their own
acts**, and no reason or note text — the link opens it. A traveller with no login is emailed at the address
on their employee record; with no address either, the desk is told to tell them. *Before lane 8 the
traveller heard only an alert's email and the workflow engine's notices to whoever pressed Submit, which
opened the desk's page they cannot see.*

**`/me/travel/documents` — My travel documents.** The traveller's passport and other travel documents:
**Add a document**, change one, remove one **while it is unverified**. The travel desk verifies; a change
takes the verification off; one primary per type. The list shows numbers to the last four; changing a
document shows it in full. *Why it matters:* the primary passport decides which trips need a visa (chapter
4) and is checked for expiry at submission.

**`/me/travel/claims/[id]` — one of my claims** *(closure lane 7, slice 7d — D-38, T-54)*. The traveller's own
expense claim, from the trip's *Money* tab. The claim card: claimed, approved, the advance deducted, **payable to
you**, submitted, reviewed (by whom), paid (method and reference), and the desk's notes — a returned claim's in a
banner: *"The travel desk returned this claim to you: … Change what they asked for, then send it again."* The
expenses: date, category (*per diem* and a company vehicle's litres marked), what was spent and its value in the
organisation's currency, the receipt, and the desk's decision — *Not yet reviewed*, or approved with the amount
and why the rest was not. While the claim is a draft or returned: **Add an expense** (category, date,
description, merchant, amount and the currency it was spent in, the receipt — one of the trip's files, or
**Upload a receipt** there and then — the per-diem switch, and on a company-vehicle trip the fuel fields), change
or remove one; **Send to the travel desk**. **Delete the claim** while it is a draft. The rules are the desk's
own (chapter 9): expenses fixed once sent; a receipt above the policy's threshold (a per diem needs none, D-43);
the claim window; nobody reviews or pays their own claim — there is no review or pay button here at all.

**`/me/travel/[id]/edit`.** The self form. **Locked** once the request is Approved, Completed,
Cancelled or Closed — with an explanation rather than a disabled form.

> ⚠ **A 404 from any `/me` route means "not yours", not "deleted"** — the self-service surface does
> not distinguish, deliberately.

**The destination-alerts panel** — the alerts sent to this traveller, each with its severity, its
title, **its body** and an **Acknowledge** button.

> ⚠ **This panel is the only place a destination alert can be acknowledged, and that is the fix for
> a real hole.** The desk route sits on `HR.Travel.Write`, which HR staff hold and the `Employee`
> role never does, while the service refuses anyone but the addressee. The two rules do not
> overlap — so **no traveller could pass the gate and no HR officer could pass the service check**,
> and the acknowledgement was unreachable by anybody. The `/me` route takes the acknowledger from
> the token, so **nothing here can acknowledge on somebody's behalf by construction**.

### ▶ Walk it

**1 — Switch to window C, signed in as `staff`**, and open **Time, Leave & Pay → My Travel**.

> *"And this is the other side. One trip — the Sebrepor site handover, completed, three hundred and fifty cedis."*

**2 — Open it, and walk the tabs.** *The trip*; *Before you go*; *Itinerary & bookings* (the car hire); then **Money**.

> *"The traveller's own trip: where they went, what was booked, what they need. And the claim — which is the part
> they actually care about: submitted, reviewed, and, if we paid it in chapter 9, paid, with the reference."*

Open the claim from the Money tab: *"payable to you"*, and each expense with the desk's decision.

**3 — Back on My Travel, press *Request travel*** and point at what is missing.

> *"No traveller field. This posts to a different endpoint entirely — one that takes no employee id anywhere and
> stamps the traveller from the token. The field is not disabled, it is absent, because a disabled control implies
> the value was sent."*

Press **Cancel** rather than creating one.

**4 — Scroll to the destination alerts.** Empty — the demo's alerts were raised before an active alert went out on
its own, and none was sent to the Sebrepor traveller.

> *"And this is where destination alerts arrive. Nothing for a day trip to Sebrepor — but for the Lagos or London
> travellers, an advisory lands here with the full text, and only they can acknowledge it. Nobody can confirm on
> your behalf that you read a security briefing about where you are going — an acknowledgement anyone can record for
> you records nothing."*

**5 — Close the loop back to the desk.**

> *"Punch line for the whole module: everything the travel desk did — the request, the approval, the flight, the
> hotel, the advance, the claim, the payment — and the traveller's own view of it is six screens with no
> permissions on them at all. They can ask a question, attach a file, file their own claim and confirm a briefing;
> they cannot approve, pay or verify anything of their own."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| My requests | `GET api/staff-travel/me/requests` | **signed-in** |
| One of mine | `GET …/me/requests/{id}` | signed-in, 404 if not yours |
| Create / update mine | `POST` / `PUT …/me/requests[/{id}]` | signed-in |
| Submit mine | `POST …/me/requests/{id}/submit` | signed-in |
| Cancel mine | `POST …/me/requests/{id}/cancel` | signed-in |
| My alerts | `GET …/me/alert-notifications` · `…/unacknowledged` | signed-in |
| **Acknowledge one** | `POST …/me/alert-notifications/{id}/acknowledge` | signed-in, **addressee only** |
| The itinerary in force | `GET …/me/requests/{id}/itinerary` | signed-in, 404 if not yours |
| The bookings in full | `GET …/me/requests/{id}/bookings` | signed-in, 404 if not yours |
| Alerts in force · health requirements | `GET …/me/requests/{id}/destination-alerts` · `…/health-requirements` | signed-in, 404 if not yours |
| **Acknowledge the risk assessment** | `POST …/me/risk-assessments/{id}/acknowledge` | signed-in, **the traveller only** (404 otherwise) |
| My travel documents | `GET`/`POST …/me/travel-documents` · `GET`/`PUT`/`DELETE …/me/travel-documents/{id}` | signed-in, 404 if not yours; delete refused (422) once verified |
| Attach a file | `POST …/me/requests/{id}/attachments` (multipart) — **201** | signed-in, 404 if not yours; 422 on a cancelled, rejected or closed trip |
| Download a file | `GET …/me/attachments/{id}/download` | signed-in, 404 unless the file's trip is yours |
| Remove a file | `DELETE …/me/attachments/{id}` | signed-in; **your upload, the trip a draft or returned** — 422 otherwise (D-40) |
| Write to the desk | `POST …/me/requests/{id}/comments` — `{ body, parentCommentId? }` | signed-in, 404 if not yours or the note is not one you can see |
| My claim | `GET`/`PUT`/`DELETE …/me/claims/{id}` · `POST …/me/claims` (201) · `POST …/me/claims/{id}/submit` | signed-in, 404 if not yours; delete only a draft (D-44) |
| My claim's expenses | `POST …/me/claims/{id}/lines` · `PUT`/`DELETE …/me/claim-lines/{lineId}` · `GET …/me/claims/{id}/fleet-fuel` | signed-in, 404 if not yours; while a draft or returned; a policy limit sent is ignored |

### ⚠ Known gaps

None open. *Fixed since the first edition (lane 7):* **T-54** (7d, D-38 — *File a claim* on the Money tab; the
traveller adds expenses with their receipts and submits; the desk reviews and pays as before, and can still file for
them), **T-55** (7c1, E1 — *Before you go* shows the risk assessment and *I have read this* records it; a Critical
trip's ticket waits for it, D-37), **T-56** (7c2 — the Files tab attaches through the scan gate, downloads, and removes
the traveller's own before submission, D-40; receipts with 7d's claims).

---

## 16. Where staff travel shows up outside its own menu

Eleven places. Three are worth a minute of the demo; the rest are for the questions.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Finance — currencies and the dated rate** | Every currency picker reads the currencies through HR's own door (`api/hr/currencies` — Finance's answered 403 to the desk, O-19), and every conversion takes **Finance's rate for the day**: each expense at its own date's, a foreign advance at the payment day's (B12, D-15), the hotel cap in the policy's own currency. Travel keeps no currency table and invents no rate | **Say it**, in chapter 4 and again on the dashboard. It is the discipline behind Rule 5's footnote |
| **Procurement — Suppliers** | A booking names a Procurement `Supplier`: the booking dialogs offer them since Procurement's supplier read was fixed (2026-09-22, **T-8**), and under a policy that makes preferred vendors mandatory a booking must name one (D-1) | Only if asked about preferred vendors |
| **Fleet — vehicles** | `GroundTransportType.CompanyVehicle` reserves a **Fleet** vehicle asset, which is a different register from HR's own `CompanyAssets`. The demo's Sebrepor trip uses *PrivateCarHire* for exactly that reason. *Since closure lane 6 (slice 6a) the leg makes a real fleet trip — clashes and expiring compliance refused, its status, vehicle, driver and costs read from Fleet, cancelled with the leg or the trip; since slice 6b a paid fuel expense goes into Fleet's fuel log (chapter 9), and since 6c a driver kept away overnight travels on a request of their own and Fleet's incidents show on the Compliance tab. Since lane 8 (8c) the sweep starts a trip when Fleet dispatches its vehicle and tells the desk when every vehicle is back. UAT has no fleet, so it is not demonstrable there; what Fleet still owes is in `docs/HR/integration/handoffs/HANDOFF-FLEET-STAFF-TRAVEL.md`* | Mention it in chapter 5.3 if somebody asks about the pool vehicle |
| **The traveller's unit and position → the policy** | The policy guard reads the traveller's **own organisation unit** — off the traveller, not the request, so a requester cannot pick a laxer unit — and walks **up the unit tree, nearest first**, so a directorate's policy covers its departments (O-5, lanes 1 and 4). The **staff level** comes from the traveller's **position**; a traveller with no position resolves to the organisation-wide policy. Only an approved policy is ever chosen | Worth one sentence in chapter 13 |
| **Workflow** | A trip's first stage asks the traveller's line authority, its second the HR role (D-7, Rule 3) — on **Staff Travel → Approvals** (chapter 3a). ⚠ **Not from the generic or mobile workflow inbox**: those advance the engine's step without travel's own service, so the request's status does not follow — cross-module defect **#15** | Say it if asked "can managers approve from their inbox?" — *not yet; from Approvals* |
| **The bell, and email** *(closure lane 8, slice 8a)* | One topic per event and audience, `StaffTravel.{Event}.Traveller` (in the app and by email) and `.Desk` (in the app, to the HR role's holders but whoever did it), editable on **Administration → Notification Topics**. The desk hears what it must act on: a trip approved (book it), cancelled or sent back for a change by someone outside the desk, an advance to approve, a claim to review, a destination alert sent, and the traveller's messages and files. The approvers keep the workflow engine's own *Approval required*. The old HR-role topics and the engine's three notices to the submitter are switched off — ⚠ a workflow-topic seed switches those three back on until the next travel notice | Only if asked who is told what |
| **Attendance** *(closure lane 9, slice 9a)* | An approved trip's working days are on the traveller's daily attendance as **On duty**, never over a clock-in, leave or a clerk's entry; the monthly summary counts them as present. The trip's page says how many (chapter 5.1) | **Yes** — open the traveller's attendance for the trip's dates: the days are there, saying which trip |
| **Leave** *(closure lane 9, slice 9b)* | A leave request over a trip's days — awaiting approval, approved or under way — shows *"Staff travel over these days"* on its page, to the employee, the approver and HR, however its dates were set; on the approvals list, where requests are approved in bulk, as a badge on the row. A warning, not a rule | Only if asked how leave and travel know about each other — travel warns of approved leave at its own submission too |
| **Separation** *(closure lane 9, slice 9c)* | The clearance page has a **Staff travel** card: the leaver's trips not cancelled, rejected or closed (with live bookings), advances still open and claims not paid, each saying what happens to it — read live, a warning that holds nothing. The separation's approval **cancels the leaver's drafts, submissions and trips sent back**, each through travel's own cancel, *"Left the organisation on {day} — separation SEP-…"*; approved and under-way trips stay for the desk. From the approval on, travel refuses to raise or submit a trip that starts after the leaving day. When Internal Audit releases the final settlement, each advance it recovered is **settled in travel** with an internal note on its trip — and no travel posting, since the settlement's journal is the posting | Only if asked what happens to a leaver's travel — the cancelled trips and the settled advance show on their own pages |
| **Payroll** | ⚠ **Nothing, on purpose.** A claim cannot be paid by payroll offset (D-10): payroll cannot receive one yet, so it would reach nobody. The question is with the payroll owner — `docs/HR/integration/handoffs/HANDOFF-PAYROLL-TRAVEL-CLAIMS.md`, defect **#37** | Say it if asked "can it go through the payslip?" |
| **General Ledger** | **Claims and advances post** through HR's Finance posting register, each its own event: an advance **paid out**, a claim **approved** (recognised) and **paid** (settled, the advance it recovered as its own leg) since 2026-09-20; **cash handed back** and a **write-off** since lane 3 (2026-10-02). A voided payment reverses its journal. An advance a leaver's settlement recovered posts nothing in travel — the settlement's journal is the posting. Each claim and advance shows its rows on a **Finance** card (chapters 5.4, 9). ⚠ **No travel rule is switched on on UAT**, so its rows read *Unposted*; and on a database seeded with Finance's v2 books no HR posting lands at all — defect **#35** | Say it plainly if a finance director asks — and show a claim's Finance card |

---

## 17. Reset — putting the database back

Do this after the room empties. Travel is the **hardest module in HR to reset**: three of the live writes move money,
and a number, once drawn, is spent. Since closure lanes 3 and 4 the HR desk holds `HR.Travel.Admin`, so most undos
are buttons — but a deletion is allowed **only while the record is still a draft of itself** (Rule 2).

| # | What you changed | Undo |
|---|---|---|
| 1 | **Travel request created** *(ch. 4, LW 1)* | Open it → **Cancel**, reason `Demonstration` — a cancelled request with a reason is a clean end state. *(Deleting it is Admin, and only while it is a draft)* |
| 2 | **Itinerary activity added** *(ch. 5.2, LW 2)* | Remove it (Admin) **while the version is a draft**. A **finalised** version stays finalised — finalise only if you are content to leave it so |
| — | **Over-cap hotel on Kumasi** *(ch. 5.3, Option B only)* | Its ⋯ menu → **Cancel booking**. The exception's decision stays on Policy Breaches as a record |
| 3 | **Budget approved** *(ch. 5.4, LW 3)* | **Edit budget**, change a part and save — a change withdraws the approval — then edit it back. The approval's history is the record |
| 4 | **Insurance recorded** *(ch. 5.5, LW 4)* | Leave it; it belongs to the request you cancelled in row 1 |
| 5 | **Comment added** *(ch. 5.6, LW 5)* | Leave it — no screen deletes a comment, and it has told the traveller anyway |
| 6 | **Request approved** *(ch. 5.8, LW 6)* | No un-approve, and the **workflow instance** would disagree with a status edit. Leave it approved — that is the honest state — or rebuild. ⚠ An approved trip now holds its days on attendance (lane 9a) and receives the destination's alerts |
| 7 | **Trip marked completed** *(ch. 5.8, LW 7)* | `UPDATE StaffTravelRequests SET Status = 7, CompletedAt = NULL WHERE RequestNumber = '…'` *(7 = In progress — the button is offered only once the trip is under way)*. The sweep completes it again the day after it ends |
| 8–9 | **Claim lines reviewed, claim approved** *(ch. 9, LW 8–9)* | Leave it approved — a reviewed claim is a clean end state — or put it back to *Submitted* in SQL: the claim `Status = 2, TotalApproved = 0, TotalRejected = 0, NetPayable = TotalClaimed, FinanceReviewedById = NULL, FinanceReviewedAt = NULL, ReviewNotes = NULL`; each line `Status = 1, AmountApproved = NULL, AmountRejected = 0, RejectionReason = NULL, ReviewedById = NULL, ReviewedAt = NULL`. The Finance register's approval row stays; a later approval is recognised as the same event |
| 10 | **Claim paid** *(ch. 9, LW 10, and the Kumasi recovery beat)* | **Void payment** as `hr.head` — an administrator who neither claimed nor paid — with a reason: the claim returns to *Approved*, the advance gets back what the payment recovered, the journal is reversed or its unposted row marked *Skipped*, and the trip gets an internal note. No SQL (T-39). A **Kumasi claim** made for the beat cannot then be deleted (only a draft can): void it, and leave it approved, or soft-delete it in SQL — its number is spent either way |
| 11 | **Visa requirement added** *(ch. 10, LW 11)* | The bin icon on its row (Admin) |
| 12 | **Destination alert raised** *(ch. 11, LW 12)* | ⋯ → **Edit** → untick **Active** — the better undo anyway; an expired advisory is a real thing. Remove (Admin) only while it has reached no trip |
| 13 | **Travel policy approved** *(ch. 13, LW 13)* | **Withdraw** (Admin — any HR officer since closure lane 4) stands it down and keeps the record that it was approved. SQL, to make it a draft again: `UPDATE StaffTravelPolicies SET ApprovedById = NULL, ApprovedAt = NULL, IsCurrentVersion = 0 WHERE PolicyName = 'TDC Staff Travel Policy 2026'` |
| — | **The HR role's `HR.Travel.Admin`** | **Not a demo change.** The HR role holds it by design (D-3): the role map carries it, `seed-db` grants it, UAT since 2026-10-02. ⚠ **Do not remove it** — budget approvals, breach decisions, voids and the reminders screen depend on it |

**The one thing that cannot be put back** is the **request number** — `TR-2026-00005` is spent. It
comes from the shared `NumberSequences` table, which only moves forward, by design. The same is
true of a claim number and an advance number.

**The clean option**, and for this module it is more often worth it than elsewhere:

```
# stop every API first — a stray one ruins the rebuild
Get-CimInstance Win32_Process -Filter "Name='ErpSystem.Api'" |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

powershell -File .\scripts\New-UatDatabase.ps1
```

⚠ A rebuild starts every number again, and it **changes which side of every fork you are on**: a rebuilt database is the *rebuilt demo* of each walk, with the policy in force, London
approved, the Hilton through D-8 and two budgets instead of four. Re-read chapter 2 after it.

---

## 18. The short path — 25 minutes

When the slot shrinks. Seven screens, in this order. This module has more good material than any
other in HR, so the cutting is genuinely painful — cut in the order given at the bottom.

| # | Screen | Min | The one thing |
|---|---|---|---|
| 1 | `/hr/travel` | 2 | four trips, four shapes; the globe icons; **money in each row's own currency** |
| 2 | `/hr/travel/[Lagos]` → **Itinerary** | 4 | the three legs, then **expand leg two's activities** — named contacts at LFZDC, mandatory flags. *"That is not a diary"* |
| 3 | `/hr/travel/[London]` → **Bookings** | 3 | the flight's **two segments** with terminals and seats; then the **Hilton over the cap** — on a rebuilt demo authorised by the second officer (D-8), on UAT said rather than shown |
| 4 | The same trip → **Compliance** | 5 | *"fifteen days to process"*, and the flight waiting for the visa; then Lagos's *"no visa, ECOWAS, zero days"*; then the **risk mitigation notes**. This is the duty-of-care beat |
| 5 | `/administration/hr/travel/policies` → the policy | 4 | the caps, then the **State**: on UAT *Draft — "I cannot sign it, and that is the point"*; on a rebuilt demo *In force*, signed by the second officer — and Policy Breaches' one row |
| 6 | `/hr/travel/claims/[Sebrepor]` | 5 | the three lines and the **receipt the line points at**; **review ≠ approving the lines**; then Rule 6 in one sentence — the advance comes back when the claim is **paid**, by an officer who reviewed none of it |
| 7 | `/hr/travel/dashboard` | 2 | the tiles, then the **currency sentence** |

Cut, in this order if you must: the dashboard, then group travel *(never in the short path anyway)*,
then the bookings tab, then the itinerary — **never rows 4, 5 or 6 of this table**, which are
the module's argument.

**Do not put in the short path:** raising a request *(it mints a fifth trip, and the register reads four)*, paying a
claim *(it needs the second officer, and its undo is a void)*, **Run a sweep now** *(it dispatches and moves trips)*,
or adding a traveller to the group.

---

## 19. What this walk found

The first edition's walk (2026-09-17) found **fifty-eight findings, T-1…T-58**. The travel final closure
(2026-10-01…04) took each one into a lane, fixed it, kept it by decision or deferred it. **The live record is
`HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` § 3d**, and each lane's *As built* there says what changed; the closure's own
findings (A–O, E, K and the decisions D-1…D-62) are in the same plan. Each chapter of this guide names the findings
fixed since the first edition under its *Known gaps*.

| Where each one stands | Findings |
|---|---|
| **Fixed by the closure** | lane 0: T-3, T-12, T-15, T-27, T-29 · lane 1: T-7 (with lane 8), T-16, T-17, T-28, T-30, T-31, T-32 · lane 2: T-10 · lane 3: T-21, T-22, T-35, T-36, T-37, T-38, T-39, T-57 · lane 4: T-1 and T-2 (D-3), T-9, T-46, T-50, T-52 · lane 5: T-19, T-24/T-42 (the ticket waits for the visa) · lane 6: T-8's vehicle half · lane 7: T-23/T-55, T-24/T-42 (the register sets the flag), T-25, T-26, T-40, T-44, T-54, T-56 |
| **Fixed upstream — the first edition was wrong or overtaken** | T-5 and T-37's conversion half (Finance's conversion fixed 2026-09-10), T-8's supplier read (2026-09-22), T-43 (the alert body), T-53 (the sweep has run daily since 2026-08-17), T-58 (claims and advances post since 2026-09-20 — chapter 16), T-44's per-trip button (it existed) |
| **Kept by decision** | **T-4** — the policy's rule register stays read-only (D-29); **T-49** — the older trip-level exception register stays without a screen (D-29; a *booking's* breach has one, chapter 13a); **T-6** — the advance is recovered on payment (Rule 6); **T-51** — a policy's level band targets `StaffLevel` (informational) |
| **A warning, not a block** | **T-45** — a Critical or Emergency alert in force over a trip shows on the request page; whether it should block is TDC's question |

**Still open — deferred, each TDC's call or a later slice (plan § 6):**

| # | Finding | Chapter |
|---|---|---|
| T-11 | The register is unpaged | 3 |
| T-13 | The register has no search, date or traveller filter, though the API has all three | 3 |
| T-14 | The register has no export | 3 |
| T-18 | Only flights and hotels are capped — ground transport and car rentals have no policy check | 5.3 |
| T-20 | A budget's parts do not bind; only the derived figures move, flagged red | 5.4 |
| T-33 | The claims queue is unpaged and unfiltered | 8 |
| T-34 | The claims queue has no export | 8 |
| T-41 | The visa register has no reverse view — where does a given passport travel freely? | 10 |
| T-47 | The dashboard has no date range | 12 |
| T-48 | Actual spend is not on the dashboard, though each trip's budget derives it | 12 |

*Seen while rewriting this guide (lane 10):* a travel document's verifier may be its owner — the two-person rule does
not reach verification (chapter 10a).

### What is genuinely strong here

This list is longer than the open one, and that is the honest summary of the module:

- **The policy guard is a real financial control**, correctly placed: it refuses at the point of writing a booking,
  resolves the applicable policy from the traveller's own unit — walking up the tree — and staff level rather than
  trusting the caller, takes domestic-or-international **from the request** so a booking cannot get a second opinion,
  and **returns the exception flag to store rather than accepting the caller's claim of it**. Before it existed, a
  caller could book First class, declare that the policy allowed First, tick their own exception, and nothing
  refused it.
- **A breach is decided by somebody else.** A booking over the cap is saved Pending, and only a travel administrator
  who neither booked it, asked for it nor travels on it can let it through (D-8) — on a screen that shows who did.
- **A policy is a draft until a second person signs it**, so the person who drafts the limits cannot put them in force
  (C3), and the person spending the money cannot set their own.
- **Every money act takes two people** — a budget, an advance, a claim's payment, a payment's void — and nobody
  approves, pays or is paid their own (Rule 5).
- **The advance–claim link closes the money loop**, at payment, with `min(outstanding, approved)` — the pay dialog is
  built not to quote a figure the act is about to change, and a claim that leaves the traveller's advance out cannot
  be paid in full without a reason.
- **Trips move themselves.** The nightly sweep starts, completes and closes trips by their dates and their money, and
  chases twelve kinds of date to the people who act on them.
- **Around twenty actor holes were closed.** Every *"who did this"* used to be read from the request body; every actor
  id is now the token, and none is a form field.
- **Reviewing is not updating.** Separate endpoints stamp a decider, and the plain update cannot touch those fields.
- **Derived money is recomputed on read, onto the DTO rather than the entity** — because a write-only rollup is stale
  immediately, and a read that writes is a different problem.
- **Mixed currencies are never silently added**, on either the dashboard or the claims queue, and both say why on the
  screen.
- **`isInternational` is derived from the two countries** rather than being a boolean a request could lie about.
- **The self-service surface takes no employee id anywhere**, and its 404 deliberately does not distinguish "not
  yours" from "deleted".
- **One feature is still deliberately *not* shipped** — the rule editor — on the argument that an editable control
  that does nothing creates false assurance. That is a better answer than the feature would have been.

---

## Appendix A — every route, in demo order

| # | Route | Chapter | Persona |
|---|---|---|---|
| 1 | `/hr/travel` | 3 | hr.head |
| 2 | `/hr/travel/approvals` | 3a | whoever a trip's stage asks — a line authority, then the HR role |
| 3 | `/hr/travel/new` | 4 | hr.head |
| 4 | `/hr/travel/[id]` — Overview | 5.1 | hr.head |
| 5 | `/hr/travel/[id]` — Itinerary | 5.2 | hr.head |
| 6 | `/hr/travel/[id]` — Bookings | 5.3 | hr.head *(hr.officer authorises a breach)* |
| 7 | `/hr/travel/[id]` — Finance | 5.4 | hr.head *(hr.officer approves the budget, pays an advance out)* |
| 8 | `/hr/travel/[id]` — Compliance | 5.5 | hr.head |
| 9 | `/hr/travel/[id]` — Comments | 5.6 | hr.head |
| 10 | `/hr/travel/[id]` — Attachments | 5.7 | hr.head |
| 11 | `/hr/travel/[id]` — Workflow | 5.8 | hr.head |
| 12 | `/hr/travel/[id]/edit` | 6 | hr.head |
| 13 | `/hr/travel/groups` | 7 | hr.head |
| 14 | `/hr/travel/groups/[id]` | 7 | hr.head |
| 15 | `/hr/travel/claims` | 8 | hr.head |
| 16 | `/hr/travel/advances` | 8a | hr.head |
| 17 | `/hr/travel/claims/[id]` | 9 | hr.head reviews · **hr.officer pays** |
| 18 | `/hr/travel/claims/new` | 9b | hr.head |
| 19 | `/hr/travel/visa-requirements` | 10 | hr.head |
| 20 | `/hr/travel/documents` | 10a | hr.head |
| 21 | `/hr/travel/alerts` | 11 | hr.head |
| 22 | `/hr/travel/dashboard` | 12 | hr.head |
| 23 | `/administration/hr/travel/policies` (+ `new`, `[id]`) | 13 | hr.head drafts · **hr.officer approves** |
| 24 | `/hr/travel/breaches` | 13a | hr.head reads · **hr.officer decides** |
| 25 | `/administration/hr/travel/reminders` | 14 | hr.head *(Admin, since lane 4)* |
| 26 | `/me/travel` (+ `new`, `[id]`, `[id]/edit`, `claims/[id]`, `documents`) | 15 | **staff** |
| — | `/workflow/inbox` | 16 | ⚠ **not for travel** — it strands the request (#15); approve on route 2 |

---

## Appendix B — the permission map, in one table

`hr.head` and `hr.officer` hold **Read**, **Write**, **Approve** and, since closure lane 4 (D-3), **Admin**. The
rows below describe the tiers; Rule 2 lists who each act is refused to, whatever the tier.

| Tier | Actions |
|---|---|
| **No permission — signed in and internal: the portal** (`api/staff-travel/me`) | **your own** trips: list, read, raise, amend, submit, cancel, **recall**, **Request change**; their itinerary, bookings, health requirements and destination alerts; the policy preview for a trip you are planning; **acknowledge your own risk assessment** (E1); your files (upload, download, remove your own) and messages to the desk; **your own claims** — file, amend, add and change lines with receipts, submit, remove a draft (D-38); **your own travel documents** — list, add, amend, remove an unverified one (E2); your destination-alert notifications and their acknowledgement. It takes **no employee id anywhere** — the traveller is the token, and a 404 means *"not yours"* |
| **No permission — signed in and internal: the decision** | **approve, reject and return a trip for revision** (`StaffTravelApprovalsController`) — the **service** decides who may, stage by stage: the traveller's line authority at stage 1, HR at stage 2, never the traveller (D-7). The *Approvals* screen and the trip's read door are the same: whoever the trip waits for may open it |
| **`HR.Travel.Read`** | every read of requests, groups, comments, attachments, itineraries, legs, activities, the four booking kinds and segments, budgets, advances, claims and lines, per-diem rates, policies, rules, exceptions, documents, visa requirements and applications, risk assessments, alerts, notifications, insurance, health requirements and clearances — plus the **dashboard**, the **Advances** queue and **Policy Breaches** |
| **`HR.Travel.Write`** | raise, amend, **submit**, **cancel** (and *did not travel*), **Request change**, **complete** and **close** a trip; comments and attachments; itineraries — create, version, **finalise**, legs and activities; **every booking kind** and its verbs *(subject to the policy — a breach is saved Pending, Rule 1)*; set a **budget**; request, **approve** and **pay out** an advance, record cash handed back; create, add lines to, **submit**, **review** and **pay** a claim — **never two halves of one payment, never one's own** (D-2, D-16); maintain per-diem rates; draft **policies** and their **rules**; raise a **policy exception**; record and **verify** travel documents; visa requirements and applications, risk assessments, **alerts** (raised active, they go out at once — E6), insurance, health requirements and **tick a health requirement** for a trip; groups — create, open, close, reopen, cancel, add a traveller, link a request |
| **`HR.Travel.Admin`** — ***held by HR since lane 4*** | **every delete**, and only while the record is still a draft of itself (Rule 2); **approve** *(never the author)* and **withdraw** a policy; **authorise or refuse a booking's breach** *(never the booker, the asker or the traveller — D-8)*; **decide a policy exception** *(never whoever raised it — C4)*; **write off an advance**; **approve a budget** *(never the traveller or whoever set it — D-19)*; **void a payment**; **the Reminders screen, reads included** |
| **`HR.Travel.Approve`** | interim authority to approve a travel request **where no workflow definition is published**. A `STAFF_TRAVEL_REQUEST` definition **is** seeded and published, so it does nothing on this database |

> **Two things worth naming.** First, **`HR.Travel.Admin` is a financial authority here**, not a housekeeping one:
> it decides what the organisation may spend on travel and who may exceed it — which is why each of its acts is
> refused to whoever did the thing it checks. Second, **a permission an unlinked account holds is a permission it
> cannot exercise**: the Admin writes that stamp an `Employee` foreign key (approving a policy, authorising a
> breach) need the caller's employee link, and `admin` is SuperAdmin but not employee-linked. Until lane 4 that
> was why nobody could sign the demo's policy; since then the HR desk, which is employee-linked, can.

---

## Appendix C — related documents

| Document | What it adds |
|---|---|
| `docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` | **The live record.** The closure's lanes 0–10, every decision (D-1…D-62), each lane's *As built*, and § 3d — where each of this guide's T-findings now stands |
| `plans/HR-Area-12-Travel-Build-Plan.md` | The original build plan — twelve slices and the decisions they took |
| `docs/HR/programme/HR-OPEN-QUESTIONS-FOR-TDC.md` | The programme's questions for TDC — for travel, whether a claim may be paid through payroll. Travel's own (the reminder windows, whether an overrun or an alert should refuse, the claim window with no policy) are in the closure plan's § 6 |
| `dev-harness/hr-travel/README.md` | The closure's suites: **1,868 assertions a regression pass** — ten suites (1,646) and the reminders (222); 1,883 with the scheduled sweep, 1,953 with the posting proof on a scratch copy. The first edition's sixteen suites are retired to `retired/`, each with the reason |
| `dev-harness/hr-demo-smoke/scenarios/080-travel.mjs`, `081-travel-logistics.mjs` | Exactly what a demo database holds, and how to rebuild it. **`080` first** — `081` reads the four requests it builds. Since lane 10, 080 approves the policy as `hr.officer` before the trips are submitted (D-60) |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 3 is the short version of this guide, for the standard demo pack |
| `docs/HR/integration/HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md` | The currency and exchange-rate boundary: Finance's rate for the day, read through HR's own door |
| `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` | Area 12's rows — what travel posts (12.1–12.3 since 2026-09-20, 12.6–12.7 since lane 3), what it deliberately does not (12.8), and the budget commitment still unposted (12.4–12.5) |
| `docs/HR/integration/handoffs/HANDOFF-PAYROLL-TRAVEL-CLAIMS.md` | What payroll would need to receive a travel claim, before payroll offset can come back (D-10, #37) |
| `docs/HR/integration/handoffs/HANDOFF-FLEET-STAFF-TRAVEL.md` | What travel already reads from Fleet, and what Fleet still owes it (#38) |
| `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` | Other teams' defects travel meets: **#15** a generic-inbox approval strands the request · **#34** the workflow designer hides and deletes "person named by the record" approvers · **#35** no HR posting lands on a database seeded with Finance's v2 books · **#36** the notification dispatcher undoes a soft delete · **#37** payroll has no intake for a one-off amount owed · **#38** Fleet double-books and approves nobody. *(#1 Procurement's suppliers and #2 Finance's conversion, both met by the first edition, are resolved)* |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | How `StaffTravelRequest` reaches the engine, after its bespoke chain was retired |
| `docs/HR/areas/company-schedule/HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md` | The neighbouring module, and the counter-example on approvals: company schedule deliberately does **not** use the engine |
| `docs/HR/areas/attendance/HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md` | Where a traveller's days show up as attendance — *On duty*, since lane 9 — and the geofence that does not apply to them |

---

*End of the HR Staff Travel System Guide.*
