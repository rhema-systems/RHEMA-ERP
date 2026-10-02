# HR Staff Travel — System Guide and Demonstration Workbook

**Status:** written 2026-09-17 from the source — page components, forms, the eight controllers, the
services, the policy guard, the EF model, the workflow definitions, the seeded permission map and
the two demo scenarios. It describes what the code is built to do. Where a screen offers something
the server will refuse, the step says so rather than smoothing it over. **The open work on every
finding in § 19 is tracked in `HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` (2026-10-01).**

**Scope:** the whole **Staff Travel** group of the HR sidebar — all six menu items — plus the six
screens that hang off them without a menu entry, the **two setup areas** under Administration →
HR → Travel, and the **four self-service screens** in the employee portal.

| # | Menu item | Route | Chapter |
|---|---|---|---|
| 1 | Register | `/hr/travel` | 3 |
| — | *(no menu entry)* Raise a request | `/hr/travel/new` | 4 |
| — | *(no menu entry)* The request, and its seven tabs | `/hr/travel/[id]` | 5 |
| — | *(no menu entry)* Amend a request | `/hr/travel/[id]/edit` | 6 |
| 2 | Group Travel | `/hr/travel/groups` (+ `[id]`) | 7 |
| 3 | Expense Claims | `/hr/travel/claims` | 8 |
| — | *(no menu entry)* The claim | `/hr/travel/claims/[id]` | 9 |
| — | *(no menu entry)* New claim | `/hr/travel/claims/new` | 9 |
| 4 | Visa Requirements | `/hr/travel/visa-requirements` | 10 |
| 5 | Destination Alerts | `/hr/travel/alerts` | 11 |
| 6 | Dashboard | `/hr/travel/dashboard` | 12 |
| — | *(Administration)* Travel Policies | `/administration/hr/travel/policies` (+ `new`, `[id]`) | 13 |
| — | *(Administration)* Travel Reminders | `/administration/hr/travel/reminders` | 14 |
| — | *(portal)* My Travel | `/me/travel` (+ `new`, `[id]`, `[id]/edit`) | 15 |

**Twenty screens** over **thirty-one tables**, with seven tabs inside a single travel request. This
is the deepest module in HR by nesting: a trip carries an itinerary carrying legs carrying
activities, four kinds of booking, a budget, advances, claims carrying lines, and six kinds of
compliance record.

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

## Before anything else: the six rules that decide whether this demo works

Travel is the most *impressive* module in HR — it has genuine depth, a real enforcement mechanism
and a money chain that ends in a bank transfer — and it has six behaviours that will catch you out
live. Read these twice.

### Rule 1 — The travel policy on this database is a **draft**, so no cap binds

This is the module's best story and its biggest trap in the same sentence.

The travel policy **genuinely refuses bookings** — a First-class ticket above the cap, a hotel over
the nightly ceiling — and only a travel **administrator** may authorise the breach. That is a real
financial control and it is properly built.

But a policy is a **draft until it is approved**, and the guard checks for that:

```
    var policy = candidates.FirstOrDefault(
        p => p.TenantId == request.TenantId && p.ApprovedById is not null);
    if (policy is null) return TravelPolicyCaps.None;      // ← no policy, no cap
```

The demo's *TDC Staff Travel Policy 2026* was created and **never approved**. Approving is
`HR.Travel.Admin` **and** the endpoint demands the caller be linked to an employee record. Until
closure lane 4 no demo login held both. **Since lane 4 (slice 4c, D-3) the HR role holds Admin**, so
**`hr.officer`** can sign it. `hr.head` drafted it, and whoever drafted or last changed a policy does
not approve it. The demo pack still leaves it a draft: lane 10 decides what the demo's trips should
meet first.

**What this means for you:** on this database the Bookings tab will accept a First-class fare and a
GHS 9,000-a-night hotel without a murmur. **Do not offer to demonstrate the cap refusing
something** unless you have done § 2.4's prep. The policy screen says *Draft* in its State column,
which is the honest hook — chapter 13 turns it into the strongest thirty seconds in the module.
This is finding **T-1**.

### Rule 2 — HR administers travel, but nobody authorises what they did themselves

> **Changed by closure lane 4 (slice 4c, 2026-10-02, D-3).** Until then `HR` held `HR.Travel.Read`,
> `Write` and `Approve`, but **not** `HR.Travel.Admin`, so `hr.head` could not delete anything,
> approve a policy or authorise a breach (**T-2**). The HR desk now holds Admin. The separation that
> used to come from the role now comes from **the act**: each authority is refused to whoever did
> the thing it checks.

In this module Admin is **not just deletion**:

| Act | Tier | Refused to |
|---|---|---|
| Delete a request, attachment, itinerary, leg, activity, any booking, any segment, a claim, a claim line, an advance, a per-diem rate, a policy *(draft)*, a rule, a document, a visa requirement, a risk assessment, an alert, an insurance record, a health requirement, a group | **Admin** | — *(a booking with a policy exception is cancelled, not deleted — lane 4, D-20)* |
| Delete a comment | its author, or **Admin** | everyone else. **Editing** a comment is its author's alone (lane 4, D-21) |
| **Approve a travel policy** — which is what makes its caps bind at all | **Admin** | whoever drafted or last changed it |
| **Withdraw a travel policy** | **Admin** | — |
| **Authorise or refuse a booking's policy exception** — *Staff Travel → Policy Breaches* (lane 4, D-8) | **Admin** | whoever booked it or asked for the exception |
| **Decide a policy exception** | **Admin** | whoever raised it |
| **Write off an advance** | **Admin** | the advance's own traveller |
| **Approve a trip's budget** | **Admin** | the traveller, and whoever set or last changed the budget (lane 4, D-19) |
| **Void a claim's payment** | **Admin** | the claimant and whoever paid it |
| **The entire Reminders screen, reads included** | **Admin** | — *(the desk that renews a passport sees the queue, T-52)* |

> **`HR.Travel.Admin` is a financial authority here, not a housekeeping one.** It decides what the
> organisation may spend on travel and who may exceed it. Holding it, an HR officer still cannot
> sign a policy they wrote, wave through their own booking's breach or approve a budget they set.
> **A second HR officer does** — on the demo, `hr.officer` for what `hr.head` did. Each refusal
> names the reason, so pressing the button is safe.

### Rule 3 — Two dropdown values on the request form will 400

`TravelRequestForm` offers a **Purpose** list and a **Risk level** list written from examples rather
than from the enums:

| Dropdown | Offers | The C# enum actually has |
|---|---|---|
| **Purpose** | BusinessDevelopment · ClientMeeting · Conference · Training · SiteVisit · Audit · **Negotiation** · Other | …plus Inspection, ProjectWork, GovernmentEngagement, PersonalCombined, Emergency — and **no Negotiation** |
| **Risk level** | Low · Medium · High · **Extreme** | Low · Medium · High · **Critical** · **Prohibited** — and **no Extreme** |

Enums bind by name (`JsonStringEnumConverter`), so choosing **Negotiation** or **Extreme** produces
a **400** the form cannot attribute to a field — it surfaces as a red *"Could not create the
request"* toast with a JSON binder message.

**The defaults are safe** (*Client meeting* and *Low*). **Do not choose those two values.** Five
real purposes and two real risk levels are unreachable, which is the other half of the same bug.
This is finding **T-3**.

### Rule 4 — The policy's rule register is read-only, and that is a decision, not an omission

Open a policy and you will find a **Rules** table you cannot edit. The API carries create, update
and delete for rules, all harness-covered. They are deliberately not wired to a screen, because:

> **`StaffTravelPolicyRule` is enforced by nothing.** The guard refuses bookings on the policy's own
> scalar caps (`MaxFlightClass*`, `MaxHotelRate*`). No code path anywhere reads a *rule*, so
> `ruleType`, `limitValue` and `violationAction` describe a mechanism that does not run.

The reasoning, which is worth saying out loud because it is a good answer: *an editable control
that does nothing creates false assurance, and that is worse than no control.* A rule set to
**Block** is a promise to whoever configured it; a warning banner is a weak defence to an auditor
looking at a screenshot. So authoring ships **on the day evaluation lands**, not before.

Chapter 13 has the say-line. This is finding **T-4** — recorded as a decision, not a defect.

### Rule 5 — Do not ask for a converted total. Anywhere.

Finance's currency conversion is **inverted** — `1 USD → GHS` returns `0.08` — and travel
**delegates to it deliberately** rather than doing its own arithmetic, because two answers about
one trip is worse than one wrong one shared with the rest of the product.

Everything downstream was built around that. The **dashboard splits its totals per currency and
refuses to add them up**, and says so on the screen:

> *"Travel is costed in 2 currencies. These are not added together — doing so would need an
> exchange rate, and travel takes rates from Finance rather than inventing one."*

On the demo database **everything is costed in GHS**, so you will see the single-currency layout
and the question never arises. If someone asks for "the total travel spend in cedis", the answer
is the sentence above. This is finding **T-5**, and it is owned by Finance, not by travel.

### Rule 6 — The advance is recovered when the claim is **paid**, not when it is approved

Travel's worst historical defect was that `AdvanceDeducted` and `SettledAmount` had **no writer
anywhere**, so an employee who took a GHS 2,500 advance and then claimed GHS 2,500 of expenses was
**paid twice**. It is fixed, and the fix has a shape you need to know before you open the pay
dialog:

- recovery happens **inside the pay call**, not on review or approval;
- so until you press **Record payment**, `netPayable` still reads as the **full approved amount**;
- the dialog therefore **deliberately does not show `netPayable`** — it shows *Approved*, *Less
  advance*, *To pay*, computed with the server's own `min(outstanding, approved)` rule and labelled
  **anticipated**.

If you quote the claim's *Payable* figure and then pay it, the number on screen will change and you
will look wrong. Read the dialog, not the table. This is finding **T-6** — a defect that was fixed
properly, and the only thing left is that the two screens disagree until the moment of payment.

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
| `HR.Travel.Write` | raise and amend travel on behalf of staff; make and change every kind of booking; build itineraries; set budgets; request, approve and disburse advances; create, submit, review and **pay** expense claims; record documents, visas, insurance, risk assessments and alerts; author policies and their rules | **yes** |
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
 │  StaffTravelPolicy   ⚠ a DRAFT until approved — see Rule 1                 │
 │    max cabin class domestic / international                                │
 │    max hotel rate  domestic / international                                │
 │    advance-booking days · cheapest fare · preferred vendor                 │
 │    max single trip · max annual · receipt required above · submission days │
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
 │  ⚙ Submit / Approve / Reject run on the WORKFLOW ENGINE — see § 1.6        │
 └──┬──────┬──────┬──────┬──────┬──────┬────────────────────────────────────┬─┘
    │      │      │      │      │      │                                    │
    ▼      ▼      ▼      ▼      ▼      ▼                                    ▼
 ITINERARY  BOOKINGS   FINANCE   COMPLIANCE   COMMENTS   ATTACHMENTS    GROUP
    │          │          │           │                                    │
    │          │          │           ├── StaffTravelDocument (passport…)   │
    │          │          │           ├── StaffTravelVisaRequirement        │
    │          │          │           ├── StaffTravelVisaApplication        │
    │          │          │           ├── StaffTravelHealthRequirement      │
    │          │          │           ├── StaffTravelInsurancePolicy        │
    │          │          │           ├── StaffTravelRiskAssessment         │
    │          │          │           └── StaffTravelAlert → Notification   │
    │          │          │
    │          │          ├── StaffTravelBudget   (committed / actual /
    │          │          │      variance — DERIVED, recomputed on read)
    │          │          ├── StaffTravelAdvance  Requested → Approved →
    │          │          │      Disbursed → PartiallySettled → FullySettled
    │          │          └── StaffTravelExpenseClaim
    │          │                 └── StaffTravelExpenseClaimLine
    │          │                 Draft → Submitted → UnderReview →
    │          │                 Approved / PartiallyApproved → Paid
    │          │                              ⛔ the advance is recovered HERE
    │          │
    │          ├── StaffTravelFlightBooking → StaffTravelFlightSegment
    │          ├── StaffTravelHotelBooking       ⛔ policy caps refuse here
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

**3. The policy is a control, not a document.** It genuinely refuses a booking. Whether it is *in
force* is a separate question from whether it *exists* — which is Rule 1 and chapter 13.

**4. Compliance is per destination, not per trip.** Visa requirements, health requirements and
destination alerts are recorded against a **country**, and the trip's Compliance tab resolves them
for this traveller's passport and this destination. Record them once; every future trip there
inherits them.

### 1.2 The tables

Thirty-one, in the order the module uses them.

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
| 18 | `StaffTravelPolicies` | The caps. **Draft until approved** |
| 19 | `StaffTravelPolicyRules` | The rule register — **read-only, enforced by nothing** |
| 20 | `StaffTravelPolicyExceptions` | A recorded breach and its decision |
| 21 | `StaffTravelDocuments` | Passports, driving licences, permits — with expiry |
| 22 | `StaffTravelVisaRequirements` | Passport country → destination country: what is needed |
| 23 | `StaffTravelVisaApplications` | One application, for one trip |
| 24 | `StaffTravelRiskAssessments` | The destination's risk, its mitigation and the traveller's acknowledgement |
| 25 | `StaffTravelAlerts` | A time-boxed advisory against a country and city |
| 26 | `StaffTravelAlertNotifications` | That alert, sent to one traveller, and their acknowledgement |
| 27 | `StaffTravelInsurancePolicies` | Cover for one trip |
| 28 | `StaffTravelHealthRequirements` | Vaccinations and clearances by destination |
| 29 | `StaffTravelReminderRuns` | One sweep of the reminder engine |
| 30 | `StaffTravelReminderDispatchLogs` | What that sweep sent, to whom |
| 31 | *(shared)* `NumberSequences` | Where `TR-2026-00002` comes from |

### 1.3 The vocabularies

Nine enum families. Three matter; the rest are look-ups.

**Request status** — nine values, and the lifecycle is the module's spine:

| Value | Means | Set by |
|---|---|---|
| `Draft` | being written | create; **Recall** returns a submitted request here |
| `Submitted` | out for approval | **Submit**, via the workflow engine |
| `ReturnedForRevision` | sent back to the requester | **Return for revision** (the approver) or **Request change** (an approved trip) — since closure lane 1, 2026-10-02; nothing wrote it before |
| `Approved` | authorised to travel | the engine |
| `Rejected` | refused | the engine |
| `InProgress` | the trip is happening | *(nothing sets it yet — T-7; the closure's lane 8 sweep will)* |
| `Completed` | the trip has happened | **Mark completed** (not before the trip starts, since lane 1) |
| `Closed` | finalised | **Close trip** — once every claim is paid or rejected and every advance settled (since lane 1; the lane 8 sweep will close too) |
| `Cancelled` | called off, with a reason | **Cancel** (not once under way, since lane 1) |

**Claim status** — eight: Draft · Submitted · UnderReview · Approved · **PartiallyApproved** ·
Rejected · **Paid** · Returned. *PartiallyApproved* is the one people ask about: it means some lines
were approved and some rejected, and the claim is still payable.

**Advance status** — seven: Requested → Approved → Disbursed → PartiallySettled → FullySettled,
plus Overdue and WrittenOff.

The rest, for reference:

| Family | Values |
|---|---|
| **Travel type** | Domestic · International · CrossBorder · Regional · OverseasAssignment · FieldVisit · Training · Conference · ClientVisit · GovernmentDuty · Emergency *(the form offers ten of the eleven — Emergency is missing)* |
| **Purpose** | BusinessDevelopment · ClientMeeting · Conference · Training · Audit · Inspection · ProjectWork · SiteVisit · GovernmentEngagement · PersonalCombined · Emergency · Other — ⚠ **see Rule 3** |
| **Priority** | Routine · Urgent · Emergency |
| **Risk level** | Low · Medium · High · Critical · Prohibited — ⚠ **see Rule 3** |
| **Initiator role** | Employee · Manager · HrAdmin · TravelDesk · System |
| **Booking status** | Pending · Confirmed · Ticketed · Cancelled · Refunded · NoShow · Completed · OnHold |
| **Cabin class** | Economy(1) · PremiumEconomy(2) · Business(3) · First(4) — ⚠ **the numbering is load-bearing**; the cap comparison is numeric |
| **Alert severity** | Info · Warning · Critical · Emergency |
| **Group status** | Planning · Open · Closed · InProgress · Completed · Cancelled |

### 1.4 The money — the arithmetic

Four figures get quoted and they are computed differently. Get these right and nothing can trip you.

**① The budget's committed, actual and variance.**

```
    Committed = Σ confirmed bookings (flights + hotels + ground + car rentals)
    Actual    = Σ PAID expense claims
    Variance  = ApprovedTotal − Actual
```

All three are **derived and recomputed on read**, onto the DTO rather than the tracked entity —
because a write-only rollup is stale the moment the next booking is made, and a read that writes is
a different kind of problem. Nothing types these numbers.

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

And the advance moves `Disbursed → PartiallySettled` or `→ FullySettled` accordingly.

**④ The dashboard's cost totals — deliberately not one number.** Grouped by currency, never
converted. See Rule 5.

### 1.5 What the travel policy decides, and what it does not

This is the honest slide, and it is a *strong* one here because more is enforced than in most of HR.

| Setting on a policy | Stored | Shown | **Enforced when a booking is written** |
|---|---|---|---|
| **Max flight class, domestic** | ✅ | ✅ | ✅ — refuses above the cap, 422, unless a **travel administrator** authorises it |
| **Max flight class, international** | ✅ | ✅ | ✅ — same |
| **Max hotel rate, domestic** | ✅ | ✅ | ✅ — same |
| **Max hotel rate, international** | ✅ | ✅ | ✅ — same |
| *(which of the two applies)* | — | — | ✅ — from the **request's** `IsInternational`, not the booking's |
| *(is the policy in force)* | — | ✅ | ✅ — **an unapproved policy caps nothing** (Rule 1) |
| **Advance booking days, flight / hotel** | ✅ | ✅ | ❌ — read by nothing |
| **Requires cheapest fare** | ✅ | ✅ | ❌ |
| **Preferred vendor mandatory** | ✅ | ✅ | ❌ — and there is no vendor field on any booking form (T-8) |
| **Max single trip budget** | ✅ | ✅ | ❌ |
| **Max annual travel budget** | ✅ | ✅ | ❌ |
| **Receipt required above** | ✅ | ✅ | ❌ — a claim line above it is accepted with no receipt |
| **Expense submission days** | ✅ | ✅ | ❌ |
| **Every rule in the rule register** | ✅ | ✅ *(read-only)* | ❌ — Rule 4 |

Two footnotes that matter to an auditor:

> **The hotel cap has no currency.** `MaxHotelRateDomestic/International` are bare decimals with no
> currency alongside them, so the comparison is *numbers against numbers*. It is sound only while
> policy caps and bookings are expressed in the same currency — which on this database they are
> (everything is GHS). Recorded rather than silently assumed; giving the policy a currency is a
> schema change. **T-9.**

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

**The line manager's way in.** *Human Resources → Time & Leave → Staff Travel → Approvals* lists what waits for the
signed-in person — every employee sees the entry, and it lists only their own work. A line manager
with no travel permission opens the request from there or from their inbox and sees the trip, its
comments and attachments and their decision; the desk's tabs (itinerary, bookings, finance,
compliance) and its Edit, Cancel and Complete buttons are not drawn for them.

---

## 2. Before the room fills — the prep

**Time needed: 20 minutes the evening before, plus 5 on the morning.** Most of this module needs no
preparation at all — the two demo scenarios build a genuinely complete travel office. What needs
deciding is Rule 1: whether you want the policy cap to actually refuse something live.

### 2.1 What the demo database already holds

Two scenario modules build it: `080-travel.mjs` (the request and the money) and
`081-travel-logistics.mjs` (the trip itself). Between them:

**Four travel requests, and exactly four** — the runbook reads the register out loud, so nothing
may mint a fifth:

| # | Trip | Who | State | What it is for |
|---|---|---|---|---|
| 1 | **Kumasi** — Ghana Institution of Engineers conference, presenting the Community 25 drainage design | `head.dev` | **Approved** | the **advance** story: GHS 2,500 requested → approved → **disbursed** |
| 2 | **Lagos** — Free Zone housing scheme study tour | `gm.ops` | **Submitted** *(pending)* | the **depth** story: group travel, a 3-leg itinerary with activities, insurance, a medium-risk assessment, a live alert. *(Its flight and hotel left the pack in closure lane 5 — a submitted trip takes no booking, D-23)* |
| 3 | **London** — CIPD Africa HR Summit | `hr.head` | **Approved** *(since closure lane 5, D-26; `hr.officer` gives HR's approval — hr.head travels)* | the **compliance** story: a visa application, a policy exception, an over-cap hotel, a car rental, a flight **refused its ticket until the visa is approved** (T-24), priority Emergency |
| 4 | **Sebrepor** — site handover with the contractor | `staff` | **Approved** | the **claim** story: an expense claim with 3 lines and a real receipt, submitted and awaiting review |

**And around them:**

| Table | What is there |
|---|---|
| `StaffTravelPolicies` | **1** — *TDC Staff Travel Policy 2026*, effective 1 Jan. Caps: Economy domestic, **Premium economy international**, GHS 900/night domestic, **GHS 2,400/night international**, 21 days ahead for flights, receipts above GHS 100, max single trip GHS 75,000. ⚠ **Draft — not approved** (Rule 1) |
| `StaffTravelPolicyRules` | **5** — hotel ceilings domestic and international, the international cabin-class ceiling, 21-day advance booking, receipt above GHS 100 |
| `StaffTravelPolicyExceptions` | **1**, **Approved** — the London summit hotel at GHS 3,200 against the GHS 2,400 ceiling |
| `StaffTravelPerDiemRates` | **3** — Ghana (GHS 450/day), Lagos (1,500), London (2,600), each split into breakfast / lunch / dinner |
| `StaffTravelBudgets` | **4**, one per request, split across flight / hotel / per-diem / transport / misc |
| `StaffTravelAdvances` | **1** — Kumasi, GHS 2,500, **Disbursed**, settlement deadline set |
| `StaffTravelExpenseClaims` | **1** — Sebrepor, **Submitted**, 3 lines: per-diem GHS 250, transport GHS 60, fuel GHS 180 with a **receipt attached** |
| `StaffTravelRequestComments` | **3** — two on Lagos (one **internal**, not visible to the traveller), one query on London |
| `StaffTravelRequestAttachments` | **3** — the GhIE invitation, the CIPD programme, the Sebrepor receipts |
| `StaffGroupTravels` | **1** — *Lagos Free Zone study tour*, max 5, the Lagos request linked to it |
| `StaffTravelDocuments` | **4** — three passports (two **verified**), one driving licence. ⚠ `head.dev`'s passport is **deliberately short-dated** so the expiring-documents screen has something on it |
| `StaffTravelVisaRequirements` | **2** — Ghana→Nigeria (**ECOWAS free movement**, no visa, 90 days) and Ghana→UK (Standard Visitor, 15 working days, VFS Accra) |
| `StaffTravelVisaApplications` | **1** — the London Standard Visitor, GHS 1,450 |
| `StaffTravelHealthRequirements` | **3** — Nigeria yellow fever (**mandatory**), Nigeria health declaration, UK fitness-to-travel over 60 |
| `StaffTravelInsurancePolicies` | **2** — Lagos GHS 250,000 cover, London GHS 500,000 |
| `StaffTravelRiskAssessments` | **2** — Lagos **Medium** with real mitigation notes, London **Low**. Only the London one is acknowledged |
| `StaffTravelAlerts` | **3** — Lagos road disruption (**Warning**), London Piccadilly line works (Info), Ashanti heavy rains (Info) |
| `StaffTravelFlightBookings` | **1** since closure lane 5 — British Airways ACC↔LHR (**Premium economy, Confirmed** — its ticket is refused until the visa is approved, T-24) with 2 segments, terminals, seats and baggage. *(Before lane 5 also Air Peace ACC↔LOS for Lagos, and the BA flight Ticketed — what a UAT seeded earlier still shows)* |
| `StaffTravelHotelBookings` | **1** since closure lane 5 — Hilton London Metropole at **GHS 3,200/night**, Confirmed. *(Before lane 5 also the Radisson Blu Anchorage, Lagos, at GHS 2,200/night)* |
| `StaffTravelGroundTransports` | **3** — the VIP coach to Kumasi, a Kumasi taxi, private car hire to Sebrepor |
| `StaffTravelCarRentalBookings` | **1** — Avis at Heathrow T5, VW Golf automatic |
| `StaffTravelItineraries` | **1** — the Lagos programme, version 1, 3 legs, **3 activities** with named contacts at LFZDC |
| `StaffTravelReminderRuns` | **0** — nothing has swept yet |

> **The Lagos trip is the one to open.** It is the only request that has every tab populated —
> itinerary, bookings, finance, compliance, comments, group. Chapter 5 spends most of its time
> there. *(Since closure lane 5 the pack books only approved trips, so on a freshly built database
> Lagos has no bookings and the **London** trip carries them — open London for the Bookings tab.)*

### 2.2 Check the register reads four

Open `/hr/travel` and count. If it is not four, something re-ran. Both scenarios are "ensure" steps
and safe to re-run:

```powershell
cd "D:\Rhema\TDC ERPS\dev-harness\hr-demo-smoke"
node scenarios.mjs --only 080
node scenarios.mjs --only 081
```

⚠ **Never create a travel request through `groups/{id}/participants`** — that door mints one new
**Draft** request per employee and the register stops reading four.

### 2.3 Write down the figures you will quote

| Screen | What to write down |
|---|---|
| `/hr/travel` — *All requests* | ____ requests |
| `/hr/travel` — *Pending approval* | ____ |
| `/hr/travel` — *Departing soon* | ____ |
| `/hr/travel/dashboard` | high-risk ____ · international ____ · estimated total GHS ____________ |
| `/hr/travel/claims` — *Awaiting payment* | ____ claims, GHS ____________ |

⚠ **"Awaiting payment" will read 0** on a fresh database: the Sebrepor claim is *Submitted*, not yet
*Approved*. Chapter 9 reviews and approves it live, and *then* the queue has something in it — which
is a much better demonstration than a pre-baked number.

### 2.4 Decide about Rule 1 — do you want the cap to bite?

This is the only real decision in the prep, and it changes the best five minutes of the demo.

**Option A — leave the policy as a draft (no prep).** Chapter 13 shows the policy, its caps, its
**Draft** state and the greyed **Approve** button, and you say the sentence about why an unapproved
policy binds nothing and who is allowed to sign it. That is a *good* governance story and it costs
nothing. The Bookings tab will accept anything, and you simply do not offer to show a refusal.

**Option B — approve the policy first, so the cap refuses live.** Then chapter 5.3 can try to book
Business class on the London trip and be refused by name, which is the most convincing thirty
seconds in the module.

To do Option B you need a caller holding `HR.Travel.Admin` **and** linked to an employee. **Since
closure lane 4 (D-3) that is any HR officer except the policy's author:** sign in as **`hr.officer`**,
open Administration → HR → Travel → **Travel Policies** and press **Approve** on the 2026 policy.
`hr.head` drafted it and is refused by name. That is the route. It needs no grant and no SQL, and the
server checks the version's dates and steps down the version it replaces (lane 4, O-4).

> *Retired routes, kept for a database from before lane 4:* granting `HR.Travel.Admin` to the HR role
> by hand (since lane 4 the role map carries it — `seed-db` grants it, and UAT was granted it on 2026-10-02), or the
> SQL below. ⚠ The SQL skips every check the
> approval makes, including the author rule, the dates and the sibling version. Do not use it on a
> database built since lane 4.

```sql
-- DEMO DATABASE ONLY. Puts the 2026 travel policy in force so its caps bind.
UPDATE p
   SET p.ApprovedById = e.Id,
       p.ApprovedAt   = SYSUTCDATETIME()
  FROM StaffTravelPolicies p
  CROSS APPLY (SELECT TOP 1 Id FROM Employees
                WHERE TenantId = p.TenantId AND IsDeleted = 0
                ORDER BY EmployeeNumber) e
 WHERE p.IsDeleted = 0
   AND p.PolicyName = N'TDC Staff Travel Policy 2026';
```

> Tonight I chose: ☐ **A** — policy stays a draft  ☐ **B** — policy approved *(method: ______)*

⚠ **If you choose B, re-read § 2.1's hotel line.** The London Hilton was booked at GHS 3,200
against a GHS 2,400 cap *while the policy was inert*. With the policy in force that row still
exists — the guard runs on write, not on read — so nothing breaks. It just means the over-cap
booking on screen was not itself authorised by the mechanism you are about to demonstrate. Say
*"and here is one that went over"* rather than *"and here is one the system let through"*.

### 2.5 Rehearse the two dropdowns you must not touch

Open `/hr/travel/new` once, tonight. Look at **Purpose** and **Risk level** and fix in your mind
that **Negotiation** and **Extreme** are the two that 400 (Rule 3). They are not last in their
lists, which is exactly why they catch people.

### 2.6 One window, two personas

Most of this book is **`hr.head`** in one window. Two exceptions:

- **Chapter 15 (the portal)** needs a second window as **`staff`** — the Sebrepor traveller, whose
  claim you will have just reviewed.
- **Chapter 13's approve step** needs Option B from § 2.4, or it is a read-only chapter.

### 2.7 Pre-open every screen

Eight tabs, left to right:

| Tab | Route | Used in |
|---|---|---|
| 1 | `/hr/travel` | ch. 3 |
| 2 | `/hr/travel/[Lagos id]` | ch. 5 — **the deep one** |
| 3 | `/hr/travel/claims` | ch. 8–9 |
| 4 | `/hr/travel/visa-requirements` | ch. 10 |
| 5 | `/hr/travel/alerts` | ch. 11 |
| 6 | `/hr/travel/dashboard` | ch. 12 |
| 7 | `/administration/hr/travel/policies` | ch. 13 |
| 8 | `/me/travel` *(second window, as `staff`)* | ch. 15 |

### 2.8 Prep checklist

- [ ] `/hr/travel` reads **four** requests *(§ 2.2)*
- [ ] The five figures from § 2.3 are written down
- [ ] You have chosen **A** or **B** for the policy *(§ 2.4)* and done the prep if B
- [ ] You know that **Negotiation** and **Extreme** 400 *(§ 2.5)*
- [ ] Window B open as `staff` *(§ 2.6)*
- [ ] Eight tabs open in the order above *(§ 2.7)*
- [ ] You have read **Rules 1–6**

---

## 3. `/hr/travel` — the register

### 📍 Where you are

**Sidebar:** Human Resources → Time & Leave → Staff Travel → **Register** · `/hr/travel` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Every trip the corporation has been asked to pay for, and where each one has got to. Three
> views, because a travel desk has three questions: what is there, what is waiting on me, and what
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

**A filter card** — the active view's hint on the left, and a **type** dropdown on the right:
*All types* · Domestic · International · CrossBorder · Regional. It filters in the browser.

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
> currencies by nature, and the request carries the one it was costed in. That is a small thing you
> can point at, and it is the visible end of Rule 5.

### ▶ Walk it

**1 — Open the register on *All requests*.** Four rows.

> *"Four trips. A conference in Kumasi, a study tour in Lagos, a summit in London, and a site
> handover at Sebrepor — which between them are about as different as corporate travel gets."*

**2 — Read the *Route* column across and point at the globe icons.**

> *"Two of them cross a border, and the system knows that without being told: international is
> derived from the two countries rather than being a box somebody ticks. It used to be a free
> boolean on the record, which let a request claim a domestic trip between two countries."*

**3 — Point at the *Estimated* column.**

> *"Three hundred and fifty cedis for a day at Sebrepor; sixty-two thousand for a week in London.
> And the currency is on every row, because a travel register that assumes one currency is a
> register that will be wrong the first time somebody goes abroad."*

**4 — Switch to *Pending approval*.** Two rows: Lagos and London.

> *"Two waiting on somebody. And this is a purpose-built queue, not a status filter — 'what is
> waiting on an approver' is the question the desk is actually measured on."*

**5 — Switch to *Departing soon*.**

> *"Approved and leaving within thirty days. Which is the other question a travel desk lives on,
> because that is the list where a passport that expires next month becomes a problem."*

**6 — Switch back to *All requests* and click `TR-2026-00002`, the Lagos trip.**

⚠ **Open Lagos, not London.** Lagos is the only request with all seven tabs populated. London is
chapter 5.5's example for compliance and you will come back to it.

### ⚙ Behind the page

| View | Endpoint |
|---|---|
| All requests | `GET api/staff-travel/requests/all` |
| Pending approval | `GET api/staff-travel/requests/pending-approval` |
| Departing soon | `GET api/staff-travel/requests/upcoming?daysAhead=30` |

All gated on `HR.Travel.Read` — the **whole controller** carries it at class level, with Write and
Admin layered onto individual actions.

Table: `StaffTravelRequests`. The API also offers paged, by employee, by status, by date range, by
organisation unit and by parent — six reads this screen does not use.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-11 · Unpaged.** `requests/all` returns every request the tenant has ever had; `GET requests` is the paged one and the screen uses the unpaged variant | |
| **T-12 · The type filter offers four of eleven travel types** — Domestic, International, CrossBorder and Regional. A `FieldVisit` or a `Conference` cannot be filtered for, though the create form can set them | |
| **T-13 · No search, no date filter, no traveller filter.** The API has all three | |
| **T-14 · No export** | |

---

## 4. `/hr/travel/new` — raising a request

### 📍 Where you are

**From:** the register → **Raise request** · `/hr/travel/new` · as **hr.head** · **6 minutes**

### 📖 What it is

> *"Everything that has to be settled before anybody asks for the money: who is going, where, when,
> why, what it will cost, how risky it is and what they will need to be allowed in."*

### 🚫 **READ RULE 3 BEFORE THIS CHAPTER.** Do not choose *Negotiation* or *Extreme*.

### 👁 On the page

**Header:** *Raise a travel request* — **"The request is created as a draft — submit it separately
once the details are settled."**, back-link.

**Card 1 — Who is travelling** *(create-only, and desk-only)*
- **Traveller** *(searchable employee picker, required)*
- **Raised as** — Employee · Manager · HrAdmin · TravelDesk

> ⚠ **There is no "raised by" field, only "raised as".** Who raised it is stamped from the token;
> the **role** it was raised under is the only real input. And **traveller is create-only** — the
> update payload carries no employee id, so a trip cannot be reassigned. Cancel and raise a new one.

**Card 2 — The trip**
- **Travel type** *(required — ten of the eleven; Emergency is missing)* · **Purpose** *(required
  — ⚠ **Negotiation 400s**)*
- **Justification** *(textarea — "Why this trip is necessary, and what it should achieve.")*
- **From (country)** *(required)* · **From (city)** *(required)*
- **To (country)** *(required)* · **To (city)** *(required)*
- A dashed callout appears the moment the two countries differ:
  *"🌐 This is an international trip — it crosses a border, so compliance documents and visa checks
  apply."*
- **Departure** *(required)* · **Return** *(required — refused if before departure)*

**Card 3 — Cost, risk and requirements**
- **Estimated cost** *(required, ≥ 0)* · **Currency** *(required — **Finance's currency list**, not
  travel's own)*
- **Priority** *(Routine · Urgent · Emergency)* · **Risk level** *(⚠ **Extreme 400s**)*
- **Organisation unit** *(picker, desk only, clearable)*
- **Requires a visa** switch — *"Turns on the visa tracking for this trip."*
- **Requires health clearance** switch — *"Vaccination or fitness-to-travel evidence must be
  recorded before departure."*
- **Reason for the change** *(edit only)*

**Footer:** **Cancel** · **Create request**.

**Two behaviours worth knowing:**

- **`isInternational` is derived, never asked.** The two country pickers decide it.
- **Choosing two different countries turns *Requires a visa* on, once.** It is then left alone —
  re-forcing it on every keystroke would fight the user.
- **The currency list comes from Finance.** Travel keeps no currency table, and the server refuses a
  code Finance does not hold — so a free-text box would produce a 422 the form could not attribute
  to a field.

### ▶ Walk it

**1 — Press *Raise request*.** Read the subtitle aloud:

> *"'Created as a draft — submit it separately.' Which matters: raising a request and asking for it
> to be approved are two acts, and the gap between them is where the travel desk gets the details
> right."*

**2 — Card 1 — pick a traveller, and leave *Raised as* on TravelDesk.**

> *"And note what is not on this form: who raised it. That is taken from whoever is signed in. What
> you can say is the capacity you are raising it in — the desk, a manager, HR, or the employee
> themselves. Those five actor fields used to be settable by the caller, which would have let
> anybody file a trip under somebody else's name."*

**3 — Card 2 — Travel type **Conference**, Purpose **Conference**.**

⚠ **Do not open the Purpose list and scroll to Negotiation.** If you must show the list, say
*"eight kinds of trip"* and close it.

**4 — Set the route: from Ghana / Tema, to South Africa / Johannesburg.** Watch the callout appear.

> *"And the moment the countries differ, the system says so and turns visa tracking on. It is not a
> box somebody remembers to tick — it is derived from the two fields that already answer the
> question."*

**5 — Dates next quarter; a real justification.**

**6 — Card 3 — cost 45,000, currency **GHS**, priority Routine, risk **Medium**.**

⚠ **Leave Risk level on Low or set it to Medium or High. Not Extreme.**

> *"And the currency comes from Finance's list, not from a box in travel. Travel does not keep its
> own currencies and does not invent its own exchange rates — which is the discipline that stops
> two parts of one system disagreeing about what a trip cost."*

**7 — 🔴 LIVE WRITE 1 — press *Create request*.** You land on the new request, at **Draft**, with
its number.

> *"TR-2026-00005. Draft. Nothing has been asked of anybody yet."*

*Undo:* chapter 17 — **Cancel** it with a reason; `hr.head` cannot delete it.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Create *(desk)* | `POST api/staff-travel/requests` | `HR.Travel.Write` |
| Create *(portal)* | `POST api/staff-travel/me/requests` | signed-in — no employee id anywhere |
| Countries | `GET api/Country/active` | reference data |
| Currencies | `GET api/finance/…/currencies?isActive=true` | Finance |

Table: `StaffTravelRequests`. The number comes from the shared `NumberSequences` table as
`TR-{year}-{00000}`. `InitiatedById` is the token's employee id; `Status` is set to `Draft`;
`EstimatedDurationDays` is derived from the two dates.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-3 · Two dropdown values 400** — *Negotiation* and *Extreme* are not enum members, and five purposes plus two risk levels are unreachable. Rule 3 | |
| **T-15 · `Emergency` is missing from the travel-type list** — ten of eleven | |
| **T-16 · The policy is not chosen or shown on the form.** `PolicyId` is nullable and nothing on the create form sets it; the demo links it by a later amendment. So a request is raised with no policy attached and the caps are resolved at *booking* time from whatever policy covers the traveller — which is the right mechanism, but the form gives no hint that a policy exists | |
| **T-17 · Nothing checks the estimate against `MaxSingleTripBudget`.** A GHS 500,000 trip is accepted under a policy that caps a single trip at 75,000 | |

---

## 5. `/hr/travel/[id]` — the request, and its seven tabs

### 📍 Where you are

**From:** any row of the register · `/hr/travel/[id]` · as **hr.head** · **18 minutes** — by far
the longest chapter, and the module

### 📖 What it is

> *"One trip, completely. What was asked for, what was approved, where they are actually going day
> by day, what has been booked, what it costs and who has been paid, what they need to be allowed
> in, what has been said about it, and the papers."*

### 👁 The header

Title `TR-2026-00002`, subtitle `Kwabena Osei · Tema → Lagos, 18 Oct 2026`, back-link, and:

| Control | Appears when |
|---|---|
| A **status badge** | always |
| **Edit** | status is `Draft` or `ReturnedForRevision` |
| **Workflow approval actions** *(Submit / Approve / Reject / Recall)* | driven by the engine — Submit on Draft or ReturnedForRevision, Approve/Reject on Submitted |
| **Mark completed** | status is `Approved` or `InProgress` |
| **Cancel** | status is **not** Cancelled, Rejected, Completed or Closed |

**Cancel's dialog:** *"A cancelled request cannot be revived. The reason is kept on the record."* —
a **reason is required**.

**Eight tabs:** Overview · Itinerary · Bookings · Finance · Compliance · Comments · Attachments ·
**Workflow**.

---

### 5.1 👁 Overview

**Card 1 — The trip** — ten fields in a three-column grid: Traveller · **Raised by** *(name and the
role in brackets)* · Type · Purpose · Priority · **Route** *(with the globe icon)* · Departs ·
Returns · Duration · Organisation unit.

**Card 2 — Cost and risk** — eight: Estimated · **Approved budget** · **Risk level** *(with a red
shield icon at High or above)* · Visa required · Health clearance · Submitted · Approved ·
Completed.

**Card 3 — Justification** — rendered only when there is one.

**Card 4 — Why it was cancelled / rejected** — rendered only when there is a cancellation reason,
with the canceller's name and the timestamp. The title flips between *"Why it was rejected"* and
*"Why it was cancelled"* on the status.

### ▶ Walk the Overview

**1 — Open the Lagos trip and read Card 1.**

> *"The study tour of the Lagos Free Zone housing scheme. Raised by the travel desk on behalf of
> the General Manager for Operations — and the record keeps both: who actually raised it, and the
> capacity they raised it in."*

**2 — Card 2, and point at *Approved budget*.**

> *"Thirty-eight thousand estimated. The approved budget is what the approver authorises, which is
> not necessarily what was asked for — and that is the number the trip is then measured against."*

⚠ On this database **Approved budget reads an em dash** even on the approved trips, because the
page does not send one and the workflow definition does not prompt for one (**T-10**). Say *"which
the approver sets when they sign it off"* rather than pointing at a number.

**3 — Point at *Risk level* — Medium, and the shield that is not there.**

> *"Medium risk, so no red flag. The London one is Low; if either were High or above there would be
> a shield here and the compliance tab would be insisting on a briefing."*

---

### 5.2 👁 Itinerary tab

> ⚠ **Changed by closure lane 5, slice 5b (2026-10-02) — the walk below predates it; lane 10 rewrites it.**
> - **The status is the server's.** A version starts a **Draft**; **Finalise** (the version in force, once it has a
>   leg) marks it **Finalised** and stamps it — then it, its legs and its activities are the record, not changed;
>   making another version current marks the old one **Superseded**; cancelling the trip marks the plan
>   **Cancelled**. The version in force is never deleted.
> - **The days are the trip's** — travel, working and weekend days are worked out from its dates, read-only.
> - **Each leg can link a booking of this trip** (*Linked booking*), shows it with its dates, and is **flagged when
>   the leg's date disagrees with the booking's** — a flight flying another day, a night outside the hotel stay.
> - Legs and activities have **Edit** and **Remove**; a plan is made while the trip is open, from its draft until it
>   is under way. On the demo, Lagos's programme is a Draft: **Finalise** is a live, safe write to show.

**What it is:** the programme — where they are, day by day, and what happens.

**Versioned.** An itinerary carries a version number and one is current. **New version** creates the
next one; the old versions stay.

**Empty state:** *"No itinerary yet"* with a **Build the itinerary** button.

**When there is one:** a card headed with the itinerary's title and version, a summary strip
(total travel days / working days / weekend days), and then the **legs in sequence**. Each leg
shows: order · **leg type** (Travel · Stay · Transit · Work · Leisure · Return) · date · origin →
destination · transport mode · departure and arrival times · notes — and, nested underneath, its
**activities**: type, title, where, the address, start and end, a named contact with email and
phone, and whether it is **mandatory**.

Three dialogs: **New itinerary** *(title, total days, summary notes — version number is shown, not
asked)*, **Add a leg**, **Add an activity**.

### ▶ Walk the Itinerary

**1 — Open the tab.** *Lagos Free Zone study tour — programme*, version 1, three legs.

**2 — Read the three legs.**

> *"Leg one: the eighteenth, Tema to Lagos, Air Peace out of Kotoka Terminal 3 at 08:40, met on
> arrival by the host organisation. Leg two: three programme days on Victoria Island and at
> Ibeju-Lekki. Leg three: the twenty-third, back into Accra and a road transfer to Tema."*

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

**5 — 🔴 LIVE WRITE 2 *(optional)* — Add an activity** to leg two. Title *"Courtesy call on the
Ghana High Commission, Lagos"*, type Meeting, a time, not mandatory.

*Undo:* chapter 17 — removing it is Admin, so prefer to leave it or plan the SQL.

### ⚙ Behind the Itinerary

| Element | Endpoint | Permission |
|---|---|---|
| The itineraries | `GET api/staff-travel/itineraries/request/{requestId}` | Read |
| The current one | `GET …/itineraries/request/{requestId}/current` | Read |
| Create / update | `POST` / `PUT …/itineraries[/{id}]` | Write |
| **Set current version** | `POST …/itineraries/{id}/set-current` | Write |
| Legs | `GET`/`POST …/{itineraryId}/legs` · `PUT …/legs/{legId}` | Read / Write |
| Activities | `GET`/`POST …/legs/{legId}/activities` · `PUT …/activities/{id}` | Read / Write |
| Every delete | `DELETE …` | **Admin** |

Tables: `StaffTravelItineraries`, `StaffTravelItineraryLegs`, `StaffTravelItineraryActivities`.

> ⚠ **Times are sent as the local wall-clock string the traveller typed**, not converted to UTC. A
> meeting at 09:30 in Lagos is 09:30 there whatever the browser's timezone is, and converting would
> shift it by the offset between the traveller and whoever is doing the booking.

---

### 5.3 👁 Bookings tab

> ⚠ **Changed by closure lanes 4 and 5 (2026-10-02) — read this before the walk below, which lane 10
> rewrites.**
> - **Bookings live on an approved trip (lane 5, D-23).** The Add buttons show only while the trip is
>   Approved or under way; on any other trip a note says why. On a database built by the demo pack since
>   lane 5, **London is approved and Lagos carries no flight or hotel** (D-26). UAT built before that still
>   shows the older shape until the pack runs again.
> - **The status is no longer on the dialogs.** A booking is saved **Pending**, and the row's **⋯ menu** moves
>   it: *Put on hold*, *Confirm*, *Ticket…* (flights, with the ticket number), *Mark completed* and *Record a
>   no-show* (once the trip has started), *Cancel booking…* (a reason, kept as an internal note, and on a
>   flight or hotel the supplier's fee), *Delete* (a pending booking only; administrators), and *Edit*.
> - **A flight on a trip that needs a visa is not ticketed** until a visa application on it is approved or
>   recorded as not required (T-24). On the demo, London's BA flight is Confirmed and *Ticket…* is refused,
>   naming the visa: say it out loud.
> - **The exception switches ask, they do not grant** (lane 4, D-8): a breaching booking waits on *Staff
>   Travel → Policy Breaches* for another travel administrator and cannot be confirmed until then.
> - A flight's **Segments** dialog lists, adds and removes its segments; a hotel has a star rating and ground
>   transport an actual cost. Every booking falls inside the trip's dates, a day either side.

**What it is:** what has actually been reserved. **Four sections, each with its own Add button:**
**Flights** · **Hotels** · **Ground transport** · **Car rentals**.

**Flights** — Airline · Reference *(PNR)* · Class · Fare *(right-aligned)* · Segments *(a count)* ·
Status. Expanding a flight shows its **segments**: order, flight number, origin → destination
airport, departure and arrival with **terminals**, duration, aircraft, seat, baggage allowance.

Dialog: **Booking reference** · **Airline code** *(2 chars)* · **Airline name** · **Booking class**
*(Economy · Premium economy · Business · First)* · **Class exception approved** switch +
**reason** · **Booked by** *(Self service · Travel desk · Travel agency · Direct airline · Direct
hotel · Online portal)* · **Total fare** · **Taxes and fees** · **Currency** · **Ticket number** ·
**Status**.

**Hotels** — Hotel · City · Dates · Nights · Total · Status. Dialog adds hotel chain, address, star
rating, room type, **rate per night**, **rate exception approved** + reason, and the cancellation
policy.

**Ground transport** — Type · Route · Cost · Status. Ten types: Taxi · Rideshare · Bus · Train ·
Metro · CompanyVehicle · PrivateCarHire · Shuttle · Motorcycle · Ferry.

**Car rentals** — Category · Route · Dates · Total · Status. Eight vehicle categories, plus daily
rate, insurance included, fuel policy and whether a licence is required.

> ⛔ **This is where the policy bites.** A cabin class or a nightly rate above the cap, a booking made
> with less notice than the policy asks, or (under a preferred-vendors policy) one with no supplier
> is **refused**, unless the exception is asked for with a reason. Since closure lane 4 (slice 4b,
> D-8) ticking the exception **asks**; it no longer grants. The booking is saved *Pending*, never
> *Confirmed*, until a travel administrator who neither booked it nor asked authorises it on
> **Staff Travel → Policy Breaches**. The refusal explains itself —
> the toast is titled *"The booking was refused"* and carries the server's own sentence, because the
> cap is not the form's to predict.

### ▶ Walk the Bookings

**1 — Open the tab.** Flights: Air Peace, `QK7T2M`, Economy, GHS 14,200, **2 segments**, Confirmed.

**2 — Expand the segments.**

> *"P4 106 out of Accra Terminal 3 at 08:40, into Lagos Terminal I at 10:05 — eighty-five minutes,
> a 737-500, seat 14A, thirty kilos of baggage. And the return. That is what a travel desk hands to
> a traveller, and it is on the trip rather than in an email."*

**3 — Hotels: Radisson Blu Anchorage, five nights at GHS 2,200 a night.**

> *"Two thousand two hundred a night on Victoria Island — and the international ceiling in the
> policy is two thousand four hundred. So this one is inside the rule."*

**4 — Now the story. Open the London trip in a second tab and look at its hotel.** Hilton London
Metropole, **GHS 3,200 a night**, with a **rate exception approved** and the reason recorded.

> *"And here is one that is not. Three thousand two hundred a night at the summit venue, against a
> ceiling of two thousand four hundred — with the exception recorded, and the reason: it is the
> only hotel within walking distance and that is the delegate rate."*

**5 — Now say what happens when you try that without authority.**

*If you chose Option A in § 2.4 (policy is a draft):*

> *"And what the system does about it depends on one thing I will show you in a moment: whether the
> policy has been signed. The mechanism is real — a cabin class or a nightly rate over the cap is
> refused outright, and only a travel **administrator** can authorise the breach, so a travel clerk
> cannot approve their own. On this database the policy is still a draft, which is exactly the
> state a corporation is in before somebody signs it, and I will show you that screen shortly."*

*If you chose Option B (policy approved) — and this is the best thirty seconds in the module:* open **Add flight** on the London trip, set **Booking class** to **Business**, leave
the exception switch **off**, and save.

> *"Refused — and it tells me why: Business exceeds the TDC Staff Travel Policy 2026 cap of Premium
> economy for this trip, and an approved policy exception is required."*

Now tick **Class exception approved** and save again.

> *"And refused a second time, differently: approving a booking above the cap requires travel
> administrator rights. Which is the control that matters. A clerk cannot tick their own exception
> — the exception is not a field on the form, it is an act of authority, and the server decides who
> holds it rather than trusting the box."*

*Undo:* nothing was written on either attempt.

**6 — Point at *Booked by* on a row.**

> *"And every booking records the channel — desk, agency, direct with the airline, self-service.
> Which is how a travel manager answers 'are we still getting value out of the agency'."*

### ⚙ Behind the Bookings

| Element | Endpoint | Permission |
|---|---|---|
| Per request | `GET api/staff-travel/bookings/{kind}/request/{requestId}` | Read |
| Create / update | `POST` / `PUT …/bookings/{kind}[/{id}]` | Write **+ the policy guard** |
| Segments | `GET`/`POST …/flights/{id}/segments` · `PUT …/segments/{id}` | Read / Write |
| Every delete | `DELETE …` | **Admin** |

Tables: `StaffTravelFlightBookings`, `StaffTravelFlightSegments`, `StaffTravelHotelBookings`,
`StaffTravelGroundTransports`, `StaffTravelCarRentalBookings`.

**What the guard does**, in order: resolves the traveller's **staff level from their position** and
their organisation unit; asks for the policies applicable on the **departure date**; keeps only
those in **this tenant** and **approved**; picks the most specific; then takes the domestic or
international cap **from the request**, not the booking. Cabin class is a numeric comparison on the
enum's ordering — `Economy(1) → PremiumEconomy(2) → Business(3) → First(4)` — **which is
load-bearing and must not be renumbered**.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-1 · An unapproved policy caps nothing** — Rule 1 | |
| **T-8 · There is no vendor field on any booking form.** `VendorId` exists on the entities and Procurement's `SuppliersController` answers 400, so there are no selectable vendors. *Preferred vendor mandatory* on the policy therefore has nothing to check | |
| **T-9 · The hotel cap has no currency** — see § 1.5 | |
| **T-18 · Only flights and hotels are capped.** Ground transport and car rentals have no policy check at all, though a GHS 620-a-day car hire for five days is real money | |
| **T-19 · Nothing reconciles a booking against the itinerary.** A flight arriving the day after the itinerary says the traveller is in a meeting is accepted | *Fixed in closure lane 5: a leg links only this trip's bookings and is flagged when its date is not the booking's (a warning, not a refusal)* |

---

### 5.4 👁 Finance tab

**Three sections: Budget · Advances · Expense claims.**

**Budget** — one card. Set once per request. Shows **Approved total** and the split across
**flight / accommodation / per-diem / transport / miscellaneous**, then the three derived figures:

| Figure | Derived from |
|---|---|
| **Committed** | the sum of confirmed bookings |
| **Actual** | the sum of **paid** expense claims |
| **Variance** | approved total − actual |

Empty state offers **Set a budget**; when there is one, **Edit the budget**.

**Advances** — Number · Type · Requested · Approved · **Outstanding** · Settle by · Status.
Two dialogs: **Request a travel advance** *(amount, currency, type, settlement deadline)* and
**Approve this advance** *(the approved amount — which may differ from the requested one)*. A third
action, **Disburse**, appears on an approved advance.

**Expense claims** — Number · Type · Claimed · **Payable** · Submitted · Status, each a link into
the claim.

### ▶ Walk the Finance

**1 — Open the tab on the Lagos trip.** The budget: GHS 38,000, split 16,000 flight / 12,000 hotel /
7,500 per-diem / 1,800 transport / 700 misc.

> *"The approved envelope, and how it is meant to be spent. Which matters because 'thirty-eight
> thousand for Lagos' is not a number anybody can check — the split is."*

**2 — Point at *Committed* and *Actual*.**

> *"Committed is what has actually been booked: the flight and the hotel. Actual is what has been
> paid out in claims — nothing yet, because the trip has not happened. And the variance between the
> envelope and the spend is computed every time this page is read, not stored. A stored rollup is
> out of date the moment the next booking is made."*

**3 — Now switch to the *Kumasi* trip's Finance tab** — that is where the advance is.

> *"Two and a half thousand cedis, requested, approved and disbursed. Cash in his hand before he
> gets on the coach, because a man presenting a paper in Kumasi should not be funding the
> corporation's travel out of his own pocket for three weeks."*

**4 — Point at *Outstanding* and *Settle by*.**

> *"And the outstanding balance, with a settlement deadline. Which is the half of an advance that
> organisations lose — and I will show you exactly where it gets recovered in a moment."*

**5 — 🔴 LIVE WRITE 3 *(optional)* — Set a budget** on the request you created in chapter 4.

*Undo:* chapter 17 — or leave it; a budget on a draft trip is harmless.

### ⚙ Behind the Finance

| Element | Endpoint | Permission |
|---|---|---|
| Budget | `GET api/staff-travel/finance/budgets/request/{id}` · `POST`/`PUT …/budgets` | Read / Write |
| Advances | `GET …/advances/request/{id}` · `POST`/`PUT …/advances` | Read / Write |
| **Approve an advance** | `POST …/advances/{id}/approve` | Write |
| **Disburse** | `POST …/advances/{id}/disburse` | Write |
| Claims | `GET …/claims/request/{id}` | Read |
| Deletes | `DELETE …` | **Admin** |

Tables: `StaffTravelBudgets`, `StaffTravelAdvances`, `StaffTravelExpenseClaims`.

> ⚠ **`approveAdvance` is the only way to set `approvedAmount`** — the plain update cannot, because
> approving is an act with an author and updating is not. Same shape as the claim review.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-20 · The budget lines are not enforced.** A GHS 20,000 flight against a 16,000 flight budget is accepted; only the derived Committed figure moves | |
| **T-21 · `Overdue` and `WrittenOff` advance statuses are set by nothing** — though `advances/overdue-settlements` exists as a read and the reminder sweep chases them | |
| **T-22 · A budget has no currency of its own** — it inherits the request's, which is right, but the DTO carries `currencyCode` and the form does not offer it | |

---

### 5.5 👁 Compliance tab

**The richest tab in HR, and the one that justifies the module.** Six sections:

**① What this traveller's passport needs** — resolved live from the **visa requirement** for their
passport country into this destination. Shows the requirement type, the category, the maximum stay
and the **processing days** — which is the number that decides whether the trip is even possible.

**② Destination alerts in force** — any active alert for the destination country, with its
severity, its title and **its body**. A trip to a country with no alert shows nothing.

**③ Travel documents** — the traveller's passports, licences and permits with expiry dates and a
**verified** flag, plus a **Verify** action.

**④ Visa applications** — Type · Number · Submitted · Expires · Fee · Status, with a **Record a
visa application** dialog.

**⑤ Risk assessment** — the destination's risk level and category, the assessment source, the
summary, whether mitigation is required and the **mitigation notes**, plus whether a duty-of-care
briefing was sent and whether the traveller has **acknowledged** it. Dialog: **Assess the
destination**.

**⑥ Insurance** — Policy · Type · Cover · Period · Sum insured · Premium. Dialog: **Record travel
insurance**.

### ▶ Walk the Compliance

**Do this one on the London trip** — it is the one with a visa application.

**1 — Open the London request's Compliance tab.**

**2 — Read section ① aloud.**

> *"A Ghanaian passport into the United Kingdom: a Standard Visitor visa, up to a hundred and
> eighty days, **fifteen working days to process**. That last number is the one that decides
> whether a summit in three weeks is possible at all — and it is on the screen before anybody
> books a flight."*

**3 — Now open the Lagos trip's Compliance tab and read the same section.**

> *"And into Nigeria: no visa. ECOWAS free movement, ninety days, zero processing days. Same screen,
> completely different answer — because the answer is a property of the passport and the
> destination, recorded once and reused by every trip."*

**4 — Section ② on the Lagos trip — the live alert.**

> *"And what he is being told before he goes: fuel-supply protests causing road closures between
> Mile 2 and Badagry, use the Lekki–Epe corridor, allow two extra hours for airport transfers.
> Warning severity, from the Ghana Mission in Lagos, in force until after he gets back."*

**5 — Section ⑤ — the risk assessment.** This is the duty-of-care story.

> *"Medium risk. And the mitigation is not a tick-box: airport pickup by the host organisation
> only, no road movement after seven in the evening, the party travels together, hotel on Victoria
> Island, emergency numbers circulated before departure. A duty-of-care briefing was sent. That is
> what an employer owes somebody it sends to a medium-risk destination, and it is on the record
> with a date."*

**6 — Point at the acknowledgement, and be honest about it.**

> *"And whether the traveller has confirmed they read it. That is deliberately the traveller's own
> act — nobody can acknowledge a security briefing on your behalf, because an acknowledgement
> anyone can record for you records nothing."*

⚠ On this database **the Lagos assessment is not acknowledged and cannot be**: the desk route sits
on `HR.Travel.Write`, which the `Employee` role never holds, while the service refuses anyone but
the addressee. So **only a trip belonging to somebody who also holds Write** can be acknowledged at
all — which on the demo data is the London trip, `hr.head`'s own. This is finding **T-23**, and the
destination-alert half of it was fixed with token-scoped `/me` routes (chapter 15) while the risk
assessment's was not.

**7 — Section ③, and the passport that is about to expire.** Switch to the Kumasi trip or look at
the documents list.

> *"And what they actually hold. Three passports, two verified against the physical document. One
> of them expires in ten weeks — which is the sort of thing that is discovered at the airport
> unless something is watching it. Something is: the reminder sweep, and I will show you that at
> the end."*

**8 — 🔴 LIVE WRITE 4 *(optional)* — *Record travel insurance*** on the request you created in
chapter 4.

### ⚙ Behind the Compliance

| Element | Endpoint | Permission |
|---|---|---|
| Visa requirement resolution | `GET api/staff-travel/compliance/visa-requirements/destination/{countryId}` | Read |
| Active alerts for a country | `GET …/alerts/country/{countryId}/current` | Read |
| Documents | `GET …/documents/employee/{employeeId}` · `POST`/`PUT …/documents` | Read / Write |
| **Verify a document** | `POST …/documents/{id}/verify` | Write |
| Visa applications | `GET`/`POST …/visa-applications…` | Read / Write |
| Risk assessments | `GET`/`POST`/`PUT …/risk-assessments…` | Read / Write |
| **Acknowledge a risk assessment** | `POST …/risk-assessments/{id}/acknowledge` | Write ⚠ see T-23 |
| Insurance | `GET`/`POST`/`PUT …/insurance…` | Read / Write |
| Health requirements | `GET …/health-requirements/country/{id}` | Read |
| Every delete | `DELETE …` | **Admin** |

Tables: `StaffTravelDocuments`, `StaffTravelVisaRequirements`, `StaffTravelVisaApplications`,
`StaffTravelRiskAssessments`, `StaffTravelAlerts`, `StaffTravelInsurancePolicies`,
`StaffTravelHealthRequirements`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-23 · The risk-assessment acknowledgement is unreachable by the people it is for** — the gate and the service check do not overlap. The alert acknowledgement had the same shape and was fixed with `/me` routes; this one was not | |
| **T-24 · `RequiresVisa` and `RequiresHealthClearance` gate nothing.** A trip flagged as needing a visa can be approved, booked and completed with no visa application on it | *Ticketing half fixed in closure lane 5: a flight is not ticketed until a visa application is approved or not required. Deriving the flag from the requirements table, and health clearance, are lane 7's* |
| **T-25 · Health requirements are shown per country but never checked against the traveller.** Nigeria's mandatory yellow-fever certificate is displayed; nothing verifies the traveller holds one | |
| **T-26 · An expiring passport does not block anything** — the reminder sweep chases it and no other path reads the expiry | |

---

### 5.6 👁 Comments tab

A textarea, a **note that the comment is posted in your name and visible to the traveller**, and an
**Add comment** button. Below it, the thread: author, timestamp, body, and *"Internal — not shown to
the traveller"* on the ones marked private.

> ⚠ **The composer always posts `isVisibleToTraveller: true`.** There is no control to post an
> internal note from this screen, even though the model supports it and the demo has one. **T-27.**

### ▶ Walk the Comments

**1 — Open the tab on the Lagos trip.** Two comments.

> *"'Estates has confirmed four places on the study tour. Passport copies are with the travel desk.'
> Visible to the traveller. And underneath — 'Internal: the ECOWAS travel certificate is enough for
> Nigeria, no visa fee to budget for.' Marked internal, and the traveller does not see it."*

**2 — Say why that distinction matters.**

> *"Which is the difference between a comment thread and a conversation you can actually have. The
> desk needs somewhere to say 'we do not need to budget for this' without it reading as a promise
> to the traveller."*

**3 — 🔴 LIVE WRITE 5 — type a comment and press *Add comment*.**

> *"And posted in my name — there is no author field, because there should not be."*

---

### 5.7 👁 Attachments tab

A table of what is attached: type, description, file name, size, who uploaded it and when, with a
**Download** link and a **Remove** action.

**Upload** takes an **attachment type** (Invitation letter · Conference brochure · Receipt ·
Passport copy · Visa · Ticket · Itinerary · Other), a description and a file.

> This goes through the product's **controlled upload gate** — virus scanning, type and size
> checks, and a DMS record — unlike the company schedule's event attachments, which are references.

**Remove is `HR.Travel.Admin`**, which the HR role holds since closure lane 4 (D-3), so `hr.head`
can remove an attachment. 🔴 It is a live write, so do not press it on the demo's two documents.

### ▶ Walk the Attachments

**1 — Open the tab on the Lagos trip, then the London one.**

> *"The invitation to present at the Ghana Institution of Engineers on one; the CIPD summit
> programme on another; and on the Sebrepor trip, the actual fuel and taxi receipts — which are
> what the expense claim points at."*

**2 — 🚫 Do not press Remove.**

---

### 5.8 👁 Workflow tab

The shared workflow tab: the definition, the steps, who is assigned to each, the decision history
and any comments. **This is authoritative** — the request's `status` says only which phase it is in.

### ▶ Walk it, and then move the request

**1 — Open the tab on the Lagos trip.**

> *"And this is not a status field somebody typed. Travel had its own approval chain and it was
> retired — this runs on the corporation's engine, the same one that approves leave, a
> requisition or a purchase order. The definition, the step, who it is assigned to, and every
> decision with a timestamp."*

**2 — 🔴 LIVE WRITE 6 — press *Approve*** in the header, add a comment, confirm. The status moves
to **Approved** and *Approved* fills on the Overview.

> *"Approved. And note that nothing on this page wrote that status — the page relayed the decision
> and refetched. Which is why a two-step definition would leave it Submitted until the second
> signature, without a line of travel code changing."*

⚠ **The definition does not prevent initiator approval** — `hr.head` can approve what `hr.head`
raised. Say it rather than hoping nobody notices.

**3 — 🔴 LIVE WRITE 7 *(optional)* — press *Mark completed*** on the Kumasi or Sebrepor trip.

> *"And when the trip has happened, it is closed off — which is what opens the door to the expense
> claim being the last word on it."*

### ⚙ Behind the request

| Element | Endpoint | Permission |
|---|---|---|
| The request | `GET api/staff-travel/requests/{id}` | Read |
| **Submit** | `POST …/{id}/submit` | Write, via the engine |
| **Approve** | `POST …/{id}/approve` | Write, via the engine |
| **Reject** | `POST …/{id}/reject?reason=` | Write, via the engine |
| **Cancel** | `POST …/{id}/cancel` | Write |
| **Mark completed** | `POST …/{id}/complete` | Write |
| Comments | `GET`/`POST …/{id}/comments` · `PUT …/comments/{id}` | Read / Write |
| Attachments | `GET`/`POST …/{id}/attachments` · `GET …/attachments/{id}/download` | Read / Write |
| Delete anything | `DELETE …` | **Admin** |

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-7 · `InProgress` and `Closed` are unreachable.** Nothing moves a trip into progress when it departs, and nothing closes it after the claim is paid — so the lifecycle's last two states never occur | |
| **T-10 · `approvedBudget` is never sent on approval** — see § 1.6 | |
| **T-27 · An internal comment cannot be posted from the screen** | |
| **T-28 · Cancelling has no status guard on the server** — a completed trip can be cancelled, and the screen's own `isLive` check is the only thing stopping it | |

---

## 6. `/hr/travel/[id]/edit` — amending a request

### 📍 Where you are

**From:** the request → **Edit** · `/hr/travel/[id]/edit` · as **hr.head** · **2 minutes**

### 📖 What it is

> *"The same form, with two differences: you cannot change who is travelling, and you have to say
> why you changed anything."*

### 👁 On the page

Chapter 4's form, minus **Card 1** *(traveller and raised-as are create-only)* and plus one field
at the bottom of Card 3:

- **Reason for the change** — *"Kept on the record — say what changed and why."*

**The Edit button only appears on `Draft` and `ReturnedForRevision`.** Everything else is read-only,
which is correct: a trip somebody has approved is not a trip you retype.

### ▶ Walk it

**1 — From your chapter-4 draft, press *Edit*.**

**2 — Point at what has gone.**

> *"The traveller is not here. A request is raised for one person and stays with them — if the wrong
> name went on it, you cancel and raise a new one rather than quietly moving an approved trip onto
> somebody else."*

**3 — Point at *Reason for the change*.**

> *"And an amendment needs a reason, kept on the record. Which is the difference between a system
> you can audit and a system where the numbers move."*

**4 — Press *Cancel*.** The live writes all live on the detail page.

⚠ **The demo scenario's own amendments used a full-replace payload.** `UpdateStaffTravelRequestDto`
is a **replace, not a patch** — every field omitted is written back as its default. The form always
sends the complete shape, so this only matters if somebody is driving the API. **T-29.**

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Update *(desk)* | `PUT api/staff-travel/requests/{id}` | `HR.Travel.Write` |
| Update *(portal)* | `PUT api/staff-travel/me/requests/{id}` | signed-in, own request only |

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
and a **New group trip** button.

**Six columns:** **Group** · **Lead** · **Destination** · **Dates** · **Travellers** *(a count,
right-aligned)* · **Status**. Rows link into the group.

**Empty state** says the design out loud: *"A group trip raises one travel request per traveller, so
everyone's is tracked individually while the trip is organised as one."*

**New-group dialog:** group name, **lead traveller**, event name, destination country and city,
travel dates, max participants.

### 👁 Screen 2 — the group (`…/groups/[id]`)

The group's details, its status, and the **participants** — each a travel request, with its own
number, traveller and status, linking into the request.

An **Add participants** action takes a list of employees.

> 🚫 ⚠ **Do not press *Add participants* on the demo database.** That endpoint **creates one new
> Draft travel request per employee**, and the register then stops reading four — which the runbook
> reads out loud. The demo's own Lagos request was linked to the group by an *amendment* to the
> existing request, deliberately, for exactly this reason.

### ▶ Walk it

**1 — Open the register.** One row: *Lagos Free Zone study tour*, lead `gm.ops`, Lagos, max 5.

**2 — Read the empty-state sentence even though the list is not empty** — it is the best
explanation of the feature:

> *"A group trip raises one travel request per traveller, so everyone's is tracked individually
> while the trip is organised as one. Which is exactly right: the coach is booked once, but the
> approval, the advance and the expense claim belong to each person."*

**3 — Open the group** and show the participant linking through to `TR-2026-00002`.

> *"One participant so far, and clicking through lands on his own travel request — with his own
> approval, his own budget and his own claim."*

**4 — 🚫 Do not press *Add participants*.** If asked:

> *"Adding somebody to the group raises their travel request for them, pre-filled from the group's
> destination and dates — which is the point, and it is also why I am not going to do it on a
> database whose register I have just read out to you."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-travel/requests/groups` | Read |
| By status | `GET …/groups/status/{status}` | Read |
| The group | `GET …/groups/{id}` | Read |
| Create / update | `POST` / `PUT …/groups[/{id}]` | Write |
| **Add participants** | `POST …/groups/{id}/participants` | Write ⚠ creates a request each |
| Remove a participant | `DELETE …/groups/{groupId}/participants/{requestId}` | **Admin** |
| Delete the group | `DELETE …/groups/{id}` | **Admin** |

Table: `StaffGroupTravels`, with `StaffTravelRequests.GroupTravelId` pointing at it.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-30 · There is no way to link an *existing* request to a group from any screen.** The only door creates new ones. The demo had to do it by amendment | |
| **T-31 · `maxParticipants` is not enforced** — a sixth participant is accepted on a group of five | |
| **T-32 · The group's dates and destination are not pushed onto its participants**, and nothing checks that they agree | |

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

**Two view buttons:** **All claims** and **Awaiting payment**.

When *Awaiting payment* has rows and they share one currency, a summary card appears:
*"**2** claims approved and unpaid, totalling **GHS 4,120.00**."*

> ⚠ **The total is only shown when every row is in the same currency.** Mixed currencies say
> nothing rather than adding up numbers that do not add up — the same discipline as the dashboard
> (Rule 5).

**Seven columns:** **Number** *(a link)* · **Traveller** · **Type** · **Claimed** · **Payable** ·
**Submitted** · **Status**.

### ▶ Walk it

**1 — Open on *All claims*.** One row: the Sebrepor claim, **Submitted**.

**2 — Switch to *Awaiting payment*.** Empty.

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

## 9. `/hr/travel/claims/[id]` — the claim, and the money leaving

### 📍 Where you are

**From:** the claims register, or a trip's Finance tab · `/hr/travel/claims/[id]` ·
as **hr.head** · **8 minutes** — the second-best chapter in the module

### 📖 What it is

> *"What somebody spent, line by line, what the organisation agreed to, and what actually went into
> their account — with the advance they were already given taken off automatically."*

### 🚫 **READ RULE 6 BEFORE THIS CHAPTER.**

### 👁 On the page

**Header:** the claim number, the traveller and the trip, a status badge, and up to four actions:

| Button | Appears when |
|---|---|
| **Add an expense** | status is `Draft` |
| **Submit** | status is `Draft` |
| **Review** | status is `Submitted` or `UnderReview` |
| **Record payment** | status is `Approved` or `PartiallyApproved` |

**The claim card** — the trip it belongs to, the claim type, the currency, total claimed, total
approved, **net payable**, the linked **advance number** and its outstanding balance, submitted /
reviewed / paid dates and who did each.

**The expenses table** — seven columns: **Date** · **Category** · **Description** · **Spent**
*(the original amount and currency)* · **In {claim currency}** · **Approved** · **Status**. Each
pending line carries two buttons: **✓ Approve** and **✗ Reject**.

**Three dialogs:**

**① Add an expense** — category *(17 of them: Airfare · Accommodation · Meals · Local transport ·
Taxi/rideshare · Car rental · Fuel · Visa fees · Insurance · Communication · Conference fees ·
Gifts/entertainment · Tips · Laundry · Medical · Baggage · Miscellaneous)*, date, description,
merchant, amount and currency, **policy limit**, **is per-diem** + the per-diem rate, and a
**receipt** picked from the trip's attachments.

**② Review this claim** — *"You are recorded as the reviewer. Reviewing the individual expenses does
not by itself settle the claim — this does."* Three radio outcomes — **Approved** ·
**Partially approved** · **Rejected** — each with a one-line hint, plus **Notes**.

**③ Record payment** — *"To {traveller}. The payment date is taken from the clock."* Shows:

```
    Approved                      GHS 490.00
    Less advance TA-2026-00001   − GHS 490.00
    ─────────────────────────────────────────
    To pay                          GHS 0.00
    The advance is recovered as part of this payment;
    the figures are confirmed once it is recorded.
```

then **Method** *(bank transfer, cheque, cash, mobile money, payroll)* and **Reference**.

> ⚠ **The dialog deliberately does not show `netPayable`.** The advance is recovered *inside* the
> pay call, so until it completes `netPayable` still reads as the full approved amount — showing it
> would promise the payer a figure the act itself is about to change. The split above is computed
> with the server's own `min(outstanding, approved)` rule and **labelled anticipated**. Rule 6.

### ▶ Walk it

**1 — Open the Sebrepor claim.** Three lines: per-diem GHS 250, transport GHS 60, fuel GHS 180.

**2 — Read the lines.**

> *"A day at Sebrepor. Two hundred and fifty cedis of subsistence at the Ghana domestic rate — and
> note it is marked as per-diem and linked to the rate it came from, so it is not a number he
> invented. Sixty cedis of trotro and taxi. And a hundred and eighty of fuel for the pool vehicle,
> with the receipt attached."*

**3 — Point at the fuel line's receipt.**

> *"And the receipt is not an attachment floating next to the claim — the line points at it. Which
> is what an auditor asks for: not 'were there receipts', but 'which receipt is this line'."*

**4 — Say the policy sentence, honestly.**

> *"The policy says a receipt is required above a hundred cedis. The fuel line has one and the
> per-diem does not need one. What the system does not yet do is refuse a line over the limit with
> no receipt — that rule is captured on the policy and not yet enforced at this door."*

**5 — 🔴 LIVE WRITE 8 — approve the lines.** Press **✓** on each of the three.

> *"Line by line, because a claim is rarely all-or-nothing. In practice this is where a finance
> officer queries the one that looks wrong and lets the rest through."*

**6 — 🔴 LIVE WRITE 9 — press *Review*.** Read the dialog description aloud, choose
**Approved**, add a note, record it.

> *"'Reviewing the individual expenses does not by itself settle the claim — this does.' Which is
> the distinction people get wrong: ticking the lines is arithmetic, and the review is the
> decision."*

The status moves to **Approved** and **Record payment** appears.

**7 — Go back to `/hr/travel/claims` and switch to *Awaiting payment*.** It now has a row and a
total.

> *"And there is the finance desk's queue, with a real number in it."*

**8 — Come back, and press *Record payment*.** **Stop and read the dialog before pressing
anything** — this is the module's punchline.

⚠ **On the Sebrepor claim there is no advance**, so the dialog will show a plain *To pay*. To show
the recovery, you need a claim against the **Kumasi** trip, which has the GHS 2,500 disbursed
advance. If you want that beat, create one: from the Kumasi trip's Finance tab → the claims
section → a claim with one line, then review and approve it. That is four extra minutes and it is
the single most valuable thing in this module.

> *"Four hundred and ninety approved. Less the advance he was already given. To pay: nothing —
> because he took two and a half thousand cedis in cash before he got on the coach, and this claim
> is how the corporation gets it back. Before this was wired, both halves were paid: the advance
> went out, the claim was reimbursed in full, and the two records never met. The fields existed;
> nothing wrote them."*

**9 — 🔴 LIVE WRITE 10 — choose a method, add a reference, press *Record payment*.**

> *"Paid. And the advance moves from Disbursed to Partially settled or Fully settled depending on
> what was left — which is the moment the money chain actually closes."*

*Undo:* chapter 17 — this one is SQL, and it is the hardest to reverse in the module. Consider
doing steps 8–9 only if you are rebuilding afterwards, or on a claim you created yourself.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| The claim | `GET api/staff-travel/finance/claims/{id}` | Read |
| Lines | `GET`/`POST …/claims/{id}/lines` · `PUT …/lines/{id}` | Read / Write |
| **Review a line** | `POST …/lines/{lineId}/review` | Write |
| **Submit** | `POST …/claims/{id}/submit` | Write |
| **Review the claim** | `POST …/claims/{id}/review` | Write |
| **Pay** | `POST …/claims/{id}/pay` | Write |
| Delete a claim or line | `DELETE …` | **Admin** |

Tables: `StaffTravelExpenseClaims`, `StaffTravelExpenseClaimLines`, `StaffTravelAdvances`.

> ⚠ **Reviewing is not updating.** `reviewClaim`, `reviewClaimLine` and `approveAdvance` each stamp
> **who decided and when** from the token; the plain update endpoints cannot set those fields. The
> review DTO takes a `NewStatus`, not a boolean — a client typed as `approve: boolean` would leave
> every claim stuck in *UnderReview* and unpayable, which is precisely the mistake a type written
> from an endpoint's *name* rather than its DTO produces.

---

### 9b. `/hr/travel/claims/new` — filing a claim

**From:** a trip's Finance tab · `/hr/travel/claims/new?requestId=…` · **1 minute**

A small form, and its **empty state is the interesting part**. Opened without a trip it says:

> *"No trip chosen — open a travel request and file the claim from its Finance tab."*

Which is the design: **a claim belongs to a trip and cannot exist without one**, so the screen
refuses to start rather than offering a trip picker that would let somebody file against the
wrong one.

With a trip, one card — **The claim**:

- **Type** *(Reimbursement · Advance settlement · Mixed)*
- **Claim currency** *(Finance's list again)*
- **Settle against an advance** — a picker of the trip's outstanding advances

Then **Cancel** and **File the claim**, landing on the new claim at `Draft` with no lines.

> ⚠ **Choosing the advance here is what links the recovery.** A claim filed without it is paid
> in full and the advance stays outstanding — the recovery is not inferred from the trip. **T-57.**

---

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-35 · `ReceiptRequiredAbove` is not enforced** — a GHS 5,000 line with no receipt is accepted | |
| **T-36 · `ExpenseSubmissionDays` is not enforced** — a claim submitted a year after the trip is accepted | |
| **T-37 · The claim currency and the line currency can differ and the conversion is Finance's**, which is inverted (Rule 5). On this database everything is GHS so it does not bite | |
| **T-38 · `Returned` claim status is set by nothing** — a claim cannot be sent back to the traveller for more information, only approved, partially approved or rejected | |
| **T-39 · There is no payment reversal.** A claim paid in error needs SQL | |

---

## 10. `/hr/travel/visa-requirements` — what a passport needs

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Visa Requirements** · `/hr/travel/visa-requirements` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"The travel desk's reference book: what each passport needs to enter each destination. Recorded
> once, read by every trip — which is why the page says an entry that is wrong is worse than one
> that is missing."*

### 👁 On the page

**Header:** *Visa requirements* — **"What each passport needs to enter a destination. The travel
desk answers from this, so an entry that is wrong is worse than one that is missing."**

**A destination card** — one country picker. **Nothing else renders until you choose one**:
*"Choose a destination — pick the country being travelled to, and every passport recorded against
it appears here."*

**The requirements table** — six columns: **Passport** *(the country the traveller's passport is
from)* · **Requirement** *(Visa-free · Visa on arrival · eVisa · Visa required · Transit visa)* ·
**Category** *(free text — "ECOWAS free movement", "Standard Visitor")* · **Max stay** ·
**Processing** *(days)* · **Last checked**.

Empty state for a chosen country: *"Nothing recorded for this destination — no passport has a
requirement here yet. Until one is added, the travel desk has no answer to give."*

**Dialog:** passport country · destination country · requirement type · category · max stay days ·
processing days · **official source URL** · **last verified** · notes.

### ▶ Walk it

**1 — Open the screen** and read the subtitle aloud. It is the best sentence on any travel screen:

> *"'The travel desk answers from this, so an entry that is wrong is worse than one that is
> missing.' Which is why every row carries the official source it came from and the date somebody
> last checked it."*

**2 — Choose **Nigeria**.**

> *"A Ghanaian passport into Nigeria: visa-free, ECOWAS free movement, ninety days, zero processing
> days — sourced from the ECOWAS protocol, checked a month ago."*

**3 — Choose **United Kingdom**.**

> *"And into the United Kingdom: a Standard Visitor visa, a hundred and eighty days, **fifteen
> working days to process**, applied for at VFS Global in Accra, biometrics in person, allow three
> working weeks in peak season. That is the answer a travel desk gives on the phone, and it is the
> number that decides whether a summit in a fortnight is possible."*

**4 — Tie it back to the trip.**

> *"And this is what the compliance tab on the London trip was showing you — it does not store the
> answer on the trip, it resolves it from here. Record it once; every future trip to the UK
> inherits it, and when the rules change you fix one row."*

**5 — 🔴 LIVE WRITE 11 *(optional)* — add a requirement.** Ghana → South Africa, *Visa required*,
category *"Visitor's visa (Port of entry)"*, 90 days max stay, 10 processing days, with a source URL
and today's verification date.

*Undo:* chapter 17 — removal is Admin.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| For a destination | `GET api/staff-travel/compliance/visa-requirements/destination/{countryId}` | Read |
| All | `GET …/visa-requirements` | Read |
| Create / update | `POST` / `PUT …/visa-requirements[/{id}]` | Write |
| Delete | `DELETE …/visa-requirements/{id}` | **Admin** |

Table: `StaffTravelVisaRequirements`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-40 · Nothing expires a requirement.** `lastVerifiedAt` is recorded and no screen or sweep flags a row nobody has checked for a year — on a page whose own subtitle says a wrong entry is worse than a missing one | |
| **T-41 · There is no reverse view.** You cannot ask "which destinations does a Ghanaian passport enter freely" — only "what does this destination require" | |
| **T-42 · The requirement is not checked against the trip.** A trip flagged `RequiresVisa = false` into a visa-required country is accepted (and see T-24) | |

---

## 11. `/hr/travel/alerts` — what travellers are told

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Destination Alerts** · `/hr/travel/alerts` ·
as **hr.head** · **4 minutes**

### 📖 What it is

> *"Security, health and disruption advisories against a destination, time-boxed so they expire on
> their own. A trip to a country with a live alert shows it on the compliance tab; a trip to one
> without shows nothing."*

### 👁 On the page

**Header:** *Destination alerts* — *"What travellers are told about security, health and disruption
where they are going."*, with a **Raise an alert** button.

**Five columns:** **Alert** *(the title)* · **Where** *(country and city)* · **Severity** *(Info ·
Warning · Critical · Emergency)* · **In force from** · **Actions** *(edit and remove)*.

Empty state: *"No active alerts — nothing is in force for any destination. A trip to a country with
no alert shows nothing on its compliance tab."*

**Dialog:** **alert type** *(Security · Health outbreak · Weather · Political unrest · Transport
disruption · Natural disaster)* · **severity** · **country** · **city** · **title** · **body** ·
**source** · **in force from** and **to** · **active** switch.

### ▶ Walk it

**1 — Open the screen.** Three alerts.

**2 — Read the Lagos one in full.** The body is the point:

> *"Lagos — road disruption and protests on the Badagry expressway. Warning. 'Fuel-supply protests
> are causing long queues and intermittent road closures between Mile 2 and Badagry. Travellers
> should use the Lekki–Epe corridor and allow two extra hours for airport transfers.' Source: the
> Ghana Mission in Lagos."*

**3 — Say why the body matters.**

> *"And that paragraph is the whole feature. An alert that says 'Civil unrest · High' and nothing
> else tells a traveller nothing — it has to say what, where, and what to do instead. That is what
> lands on his compliance tab and what goes out to him."*

⚠ That is not rhetorical. **A shipped screen showed alerts with no text for months**, because the
"current alert for a country" read returned a summary DTO with no `body` and the compliance strip
renders the body conditionally — so a traveller saw the headline and never the advice. Fixed, and
worth knowing the shape of. **T-43**, closed.

**4 — Point at the two dates.**

> *"In force from and to. An advisory that never expires is an advisory nobody reads, so these are
> time-boxed and drop off on their own."*

**5 — 🔴 LIVE WRITE 12 *(optional)* — raise one.** Type **Health outbreak**, severity **Warning**,
a country, a title and a real body, a source, and a two-month window.

**6 — Then say where it goes.**

> *"And an alert becomes a notification to a named traveller — which they, and only they, can
> acknowledge. Nobody can confirm on your behalf that you read a security briefing about where you
> are going. I will show you that side of it in the portal."*

*Undo:* chapter 17 — removal is Admin; **deactivating** it from the edit dialog works for `hr.head`.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Active alerts | `GET api/staff-travel/compliance/alerts/active` | Read |
| For a country | `GET …/alerts/country/{id}` · `…/country/{id}/current` | Read |
| Create / update | `POST` / `PUT …/alerts[/{id}]` | Write |
| Delete | `DELETE …/alerts/{id}` | **Admin** |
| Notify a traveller | `POST …/alert-notifications` | Write |
| *(the traveller's own)* | `GET`/`POST api/staff-travel/me/alert-notifications…` | signed-in |

Tables: `StaffTravelAlerts`, `StaffTravelAlertNotifications`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-44 · Nothing sends an alert to anybody automatically.** Raising an alert for Nigeria does not notify the people with approved trips to Nigeria — a notification is created one at a time through a separate endpoint that no screen calls | |
| **T-45 · An alert does not block or flag a booking** to the destination it warns about, at any severity — including `Emergency` | *Flagged since closure lane 5: a Critical or Emergency alert in force over the trip shows as a warning on the request page — for the approver and the desk. Blocking is TDC's question* |
| **T-46 · `TravelRiskLevel.Prohibited` exists and prohibits nothing** | |

---

## 12. `/hr/travel/dashboard` — where the organisation's travel stands

### 📍 Where you are

**Sidebar:** … → Staff Travel → **Dashboard** · `/hr/travel/dashboard` · as **hr.head** ·
**4 minutes**

### 📖 What it is

> *"The shape of the corporation's travel: how much is in flight, what it is costing, who is going
> where and what is waiting on somebody."*

### 👁 On the page

**A row of stat tiles:** total requests · pending approval · approved · **high risk** *(with
"N international" as a hint)*.

**Estimated cost card** — and this is Rule 5 made visible:

- **When every request shares one currency** *(which it does on this database)*: three big figures
  — **Estimated**, **Approved budget**, and **Across N requests**.
- **When they do not**: a table with a row per currency — Currency · Estimated · Approved budget ·
  Requests — followed by the sentence:
  > *"Travel is costed in N currencies. These are not added together — doing so would need an
  > exchange rate, and travel takes rates from Finance rather than inventing one. Cancelled and
  > rejected requests are excluded throughout."*

**By status** — a breakdown.
**Top destinations** — where people are going.
**Requests raised, last six months** — a trend.
**Three lists:** **Awaiting approval** · **Departing soon** · **Recently raised**, each five columns
— Number · Traveller · Route · Departs · Status.

### ▶ Walk it

**1 — Open the dashboard.** Read the tiles.

**2 — Point at *High risk* and its hint.**

> *"High risk, and how many are international — which are not the same question. A field visit to a
> site with no road access is domestic and high risk; a conference in Copenhagen is international
> and low."*

**3 — Read the *Estimated cost* card.**

> *"A hundred and four thousand five hundred and fifty cedis in flight across four requests, against
> the approved budgets."*

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
| **T-48 · Actual spend is not on it.** Estimated and approved budget are; the sum of paid claims — the number a finance director actually wants — is not, though it is derived per request on the budget | |

---

## 13. `/administration/hr/travel/policies` — the caps, and who signs them

### 📍 Where you are

**Sidebar:** Administration → HR → **Travel** → **Travel Policies** ·
`/administration/hr/travel/policies` (+ `new`, `[id]`) · as **hr.head** · **6 minutes**

### 📖 What it is

> *"What staff may spend on travel — and, more importantly, whether anybody has signed it."*

### 🚫 **READ RULE 1 AND RULE 4 BEFORE THIS CHAPTER.** This is where they both land.

### 👁 Screen 1 — the register

**Header:** *Travel policies* — **"What staff may spend on travel, and what the caps refuse."**,
with a **New policy** button.

**Six columns:** **Policy** · **Version** · **Effective** · **Rules** *(a count)* · **State**
*(**Draft** / **In force**)* · **Approved by**.

Row actions: **Approve** *(on a draft)* and **Withdraw** *(on one in force)*, both
`HR.Travel.Admin`. The HR role holds it since closure lane 4 (D-3). But `hr.head` drafted the demo's
policy, so **Approve refuses `hr.head` by name** (*"…drafted or last changed…"*); `hr.officer` signs it.

Empty state: *"No travel policies — without one, travel bookings are not capped at all."*

### 👁 Screen 2 — the policy (`…/policies/[id]`)

**Header:** the policy name and version, its state, and — on a **draft only** — **Edit** and
**Delete**. The edit page's own subtitle says why: *"Only a draft can be corrected — an approved
policy needs a new version."*

**Card 1 — who it applies to** — staff level from / to, organisation unit, effective from / to.

**Card 2 — "The caps that refuse a booking"** — the four that are enforced, in bold, then the eight
that are recorded:

| Enforced | Recorded only |
|---|---|
| Max flight class, domestic | Advance booking days, flight and hotel |
| Max flight class, international | Requires cheapest fare |
| Max hotel rate, domestic | Preferred vendor mandatory |
| Max hotel rate, international | Max single trip budget |
| | Max annual travel budget |
| | Receipt required above |
| | Expense submission days |

**Card 3 — the rule register** — **read-only**, with an explanatory note. Six columns: rule code ·
rule name · **type** *(Hard limit · Soft limit · Warning · Mandatory · Preferred · Prohibited)* ·
expense category · **limit** and unit · **violation action**.

### ▶ Walk it

This is the governance chapter. Take the six minutes.

**1 — Open the register.** One row: *TDC Staff Travel Policy 2026*, version 1, 5 rules, **Draft**,
Approved by — an em dash.

**2 — Say the headline before opening it.**

> *"One policy, and look at the State column: **Draft**. Which is the most important word on this
> screen, and I want to explain why rather than skip past it."*

**3 — Open the policy and read Card 2's four enforced caps.**

> *"Economy on domestic flights. Premium economy on international. Nine hundred cedis a night
> domestically, two thousand four hundred internationally. Those four are not descriptions — they
> are enforced. A booking above any of them is refused outright at the point somebody tries to save
> it, and only a travel **administrator** can authorise the breach, so a travel clerk cannot tick
> their own exception."*

**4 — Now the Draft sentence. This is the thirty seconds.**

> *"And none of that is in force yet, because nobody has signed this policy. That is deliberate, and
> it is the part I would want to hear if I were sitting where you are. Before this rule existed,
> anybody who could edit a travel policy could write the rule constraining everybody's travel
> spending and have it bind immediately — which means the person spending the money could set their
> own limit. So a policy is a **draft** until an administrator approves it, and until then it caps
> nothing. What you are looking at is the state every corporation is in on the day before somebody
> signs."*

**5 — Point at the *Approve* button.** *(Since closure lane 4 it is live for HR. As `hr.head`,
pressing it is refused by name, which is safe to show.)*

> *"And I cannot sign it — I wrote it. Approving a travel policy is a travel administrator's act,
> the same tier that authorises a breach of it, and it is never the author's. A second officer
> signs. Writing the rules and putting them in force are two different acts, and the system holds
> them apart by who did what."*

**6 — 🔴 LIVE WRITE 13 *(only if you chose § 2.4 Option B and kept it for the room)*:** in a second
window signed in as **`hr.officer`**, **approve it**. The State flips to **In force**, with that
officer in *Approved by*. Then, if you have not already, go back to chapter 5.3's refusal.

**7 — Scroll to Card 3, the rule register**, and read the five rules.

> *"And underneath, the rule register: the hotel ceilings, the cabin-class ceiling, twenty-one days'
> advance booking, a receipt above a hundred cedis — each with a violation action saying whether it
> warns, requires approval, or blocks."*

**8 — Now say the Rule 4 sentence, which is the second-best governance moment in the module.**

> *"And this table is read-only — deliberately. These rules are not yet evaluated by anything: the
> caps that refuse a booking are the four scalar ones you just saw, and this register describes a
> mechanism that is built but not yet running. So rather than give you an editor for rules that do
> nothing, we left it read-only. An editable control that does nothing is worse than no control — a
> rule set to 'Block' is a promise to whoever configured it, and a warning banner is a weak defence
> to an auditor looking at a screenshot. The write endpoints exist and are tested; the affordances
> come back on the day evaluation lands, not before."*

That answer is better than the feature would have been.

**9 — 🚫 Do not press *Delete* on the policy** *(Admin)* — and note the screen only offers it on a
draft anyway.

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| Register | `GET api/staff-travel/policies` | Read |
| Current / applicable | `GET …/policies/current` · `…/applicable` | Read |
| Create / update | `POST` / `PUT …/policies[/{id}]` | Write — **update is refused once approved** |
| **Approve** | `POST …/policies/{id}/approve` | **Admin** + employee-linked |
| **Withdraw** | `POST …/policies/{id}/withdraw` | **Admin** |
| Delete | `DELETE …/policies/{id}` | **Admin** |
| Rules | `GET …/policies/{id}/rules` · `…/rules/active` | Read |
| Rule writes | `POST`/`PUT …/rules…` | Write — **no screen calls them** |
| Exceptions | `GET …/exceptions/request/{id}` · `…/exceptions/pending` · `POST …/exceptions` · `…/{id}/decide` | Read / Write — **no screen calls the write half** |

Tables: `StaffTravelPolicies`, `StaffTravelPolicyRules`, `StaffTravelPolicyExceptions`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-1 · Every policy in every tenant is currently unapproved**, so travel caps do not bind anywhere until each is signed. **This is a real behaviour change on the day somebody signs one** — Rule 1 | |
| **T-4 · The rule register is enforced by nothing and ships read-only** — a decision, not a defect, and the reasoning is in the say-line | |
| **T-49 · The policy-exception flow has no screen at all.** Create and decide are implemented and harness-covered; the demo's one exception was made through the API. Withheld for the same reason as the rules — nothing raises an exception, because its producer (rule evaluation) was never built | |
| **T-50 · Version uniqueness is caller-declared.** `versionNumber` and `isCurrentVersion` come from the client; nothing stops two version 1s or two current versions | |
| **T-51 · `AppliesToLevelFromId` targets `StaffLevel`, not a salary level** — an FK's name is not its target, and a salary-level id would fail on a constraint naming neither | |

---

## 14. `/administration/hr/travel/reminders` — the sweep

### 📍 Where you are

**Sidebar:** Administration → HR → Travel → **Travel Reminders** ·
`/administration/hr/travel/reminders` · **needs `HR.Travel.Admin`, which the HR role holds since
closure lane 4 (D-3, T-52)** · **3 minutes**

### 📖 What it is

> *"The thing that watches the dates nobody else is watching: a passport expiring, a visa about to
> lapse, an advance past its settlement deadline, a departure coming up."*

### **This whole screen is Admin-gated, reads included.** Before closure lane 4, `hr.head` got a 403
on the page itself. Since D-3 the HR desk opens it, because the desk that renews an expiring
passport is the one that needs the queue (T-52). ⚠ **Run the sweep now** posts real in-app
reminders, each of which is sent only once, so treat it as a live write.

### 👁 On the page

**Header:** *Travel reminders* — *"Expiring passports and visas, overdue advances, and departures
coming up."*, with a **Run the sweep now** button.

**Card 1 — What the sweep chases** — the four kinds and their thresholds.

**Card 2 — Due now** — a preview, with an **as-of date** override and a **clear** button. Five
columns: **Kind** · **Record** · **Due** · **Days** · **Tier** *(how urgent)*. Empty state:
*"Nothing due."*

**Card 3 — Sweeps** — the run history: Started · Finished · Trigger · Queued. Empty state:
*"No sweeps yet — nothing has run for this tenant."*

**Card 4 — Sent in the last 14 days** — the dispatch log.

### ▶ Walk it *(read-only, from an admin window, or skip)*

**1 — If you have an admin window, open it.** All three lists are empty except *Due now*.

> *"Nothing has ever run on this database, which is honest. What the sweep does is chase four kinds
> of date: a passport or visa about to expire, an advance past its settlement deadline, and a
> departure coming up. And the preview tells you what it *would* send before you send it — with an
> as-of date, so you can ask 'what will this look like on the first of next month'."*

**2 — Point at *Due now* and find the short-dated passport.**

> *"And there is the one we planted: a passport expiring in ten weeks, on a traveller who is
> booked on a trip. That is the failure this module exists to prevent, and it is found by a job
> rather than by somebody remembering."*

**3 — 🚫 Do not press *Run the sweep now* during a demo.** It dispatches.

**4 — If you have no admin window, say it from chapter 5.5 instead:**

> *"And the passports are watched by a reminder sweep — expiring documents, lapsing visas, overdue
> advances and upcoming departures, on a schedule, with a preview of what it will send before it
> sends it. It sits under Administration because it dispatches to people."*

### ⚙ Behind the page

| Element | Endpoint | Permission |
|---|---|---|
| **Run** | `POST api/staff-travel/reminders/run` | **Admin** |
| Preview | `GET …/reminders/preview?asOf=` | **Admin** |
| Run history | `GET …/reminders/runs` | **Admin** |
| Dispatch log | `GET …/reminders/log` | **Admin** |

Tables: `StaffTravelReminderRuns`, `StaffTravelReminderDispatchLogs`.

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-52 · The reads are Admin-gated along with the writes.** The travel desk — the people who would act on an expiring passport — cannot see the queue at all. An open TDC question: should the desk see the reminder log? | |
| **T-53 · Nothing runs the sweep on a schedule.** There is no hosted service; the only trigger is the button on this screen | |

---

## 15. `/me/travel` — the portal

### 📍 Where you are

**Portal:** Time, Leave & Pay → **My Travel** · `/me/travel` (+ `new`, `[id]`, `[id]/edit`) ·
**as the `staff` persona**, in window B · **5 minutes**

### 📖 What it is

> *"The traveller's own side. Four screens, no permissions, and one thing only they can do."*

### 👁 The four screens

**`/me/travel` — My travel.** *"Trips you have requested, and where each one has got to."* Five
columns: Number · Route · Dates · Estimated · Status. Plus the **destination-alerts panel** (below).

**`/me/travel/new` — Request travel.** Chapter 4's form on the **`self` surface**: the traveller and
the initiator fields are **absent, not disabled** — because the self-service endpoint overwrites
them server-side, and a disabled control would imply the value was sent. Subtitle: *"Saved as a
draft. Submit it when you are ready for approval."*

**`/me/travel/[id]` — one trip.** The overview, the itinerary, the bookings and the compliance
summary, read-only, plus **Submit** on a draft and **Cancel** while it is live.

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

**1 — Switch to window B, signed in as `staff`**, and open **Time, Leave & Pay → My Travel**.

> *"And this is the other side. One trip — the Sebrepor site handover, approved, three hundred and
> fifty cedis."*

**2 — Open it.**

> *"His own trip, read-only: where he went, what was booked, what he needs. And the claim we just
> paid, which is the part he actually cares about."*

**3 — Press *Request travel*** and point at what is missing.

> *"No traveller field. This posts to a different endpoint entirely — one that takes no employee id
> anywhere and stamps him from the token. The field is not disabled, it is absent, because a
> disabled control implies the value was sent."*

Press **Cancel** rather than creating one.

**4 — Scroll to the destination alerts** *(if the `staff` persona has any — the demo sends none, so
this may be empty)*.

*If empty:*
> *"And where his destination alerts arrive. Nothing for a day trip to Sebrepor — but for the Lagos
> or London travellers, the advisory we were looking at earlier lands here, with the full text, and
> only they can acknowledge it. Nobody can confirm on your behalf that you read a security briefing
> about where you are going — an acknowledgement anyone can record for you records nothing."*

**5 — Close the loop back to the desk.**

> *"Punch line for the whole module: everything the travel desk did — the request, the approval, the
> flight, the hotel, the advance, the claim, the payment — and the traveller's own view of it is
> four screens with no permissions on them at all."*

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

### ⚠ Known gaps

| Gap | |
|---|---|
| **T-54 · The traveller cannot raise an expense claim.** Claims are desk-only — the one part of the money chain an employee would most expect to start themselves | |
| **T-55 · The traveller cannot see or acknowledge their risk assessment** — T-23's other half | |
| **T-56 · The traveller cannot upload a document or a receipt.** Both are desk acts | |

---

## 16. Where staff travel shows up outside its own menu

Six places. Two are worth a minute of the demo; the rest are for the questions.

| Where | What it shows | Worth showing? |
|---|---|---|
| **Finance — currencies and conversion** | Every currency picker in this module reads Finance's active-currency list, and every conversion delegates to Finance's `ConvertAsync`. Travel keeps no currency table and invents no rate | **Say it**, in chapter 4 and again on the dashboard. It is the discipline behind Rule 5 |
| **Procurement — Suppliers** | `VendorId` on every booking points at a Procurement `Supplier`. `api/Suppliers` answers **400**, so there are no selectable vendors and no booking form offers the field (**T-8**) | Only if asked why *"preferred vendor mandatory"* checks nothing |
| **Fleet — vehicles** | `GroundTransportType.CompanyVehicle` reserves a **Fleet** vehicle asset, which is a different register from HR's own `CompanyAssets`. The demo's Sebrepor trip uses *PrivateCarHire* for exactly that reason | Mention it in chapter 5.3 if somebody asks about the pool vehicle |
| **The employee's position → staff level** | The policy guard resolves the applicable policy from the traveller's **staff level, which lives on their position**, not on the employee. A traveller with no position falls back to the organisation-wide policy | Worth one sentence in chapter 13 |
| **Workflow inbox** (`/workflow/inbox`) | A submitted travel request appears in the assignee's inbox alongside every other approval in the ERP | **Yes** — 30 seconds, and it is the same point as every other module: a manager lives in one inbox |
| **General Ledger** | ⚠ **Nothing.** No travel transaction posts to GL. Advances, claims and payments are recorded in travel's own tables and the Finance hand-off is an open backlog item (D-4) | Say it plainly if a finance director asks — see **T-58** |

---

## 17. Reset — putting the database back

Do this after the room empties. Travel is the **hardest module in HR to reset**, because `hr.head`
cannot delete anything and three of the live writes are money moving. Read this before you decide
how much of chapter 9 to perform.

| # | What you changed | Undo |
|---|---|---|
| 1 | **Travel request created** *(ch. 4, LW 1)* | Open it → **Cancel**, reason `Demonstration`. Cancelling is Write and works; deleting is Admin. A cancelled request with a reason is a clean end state |
| 2 | **Itinerary activity added** *(ch. 5.2, LW 2)* | Removal is Admin. Either leave it — an extra courtesy call on a study tour is harmless — or `UPDATE StaffTravelItineraryActivities SET IsDeleted = 1 WHERE Title = '…'` |
| 3 | **Budget set** *(ch. 5.4, LW 3)* | Leave it; it hangs off the request you cancelled in row 1 |
| 4 | **Insurance recorded** *(ch. 5.5, LW 4)* | Same — it belongs to the cancelled request |
| 5 | **Comment added** *(ch. 5.6, LW 5)* | Deleting a comment is Admin. Leave it; a comment is a comment |
| 6 | **Request approved** *(ch. 5.8, LW 6)* | No un-approve, and the **workflow instance** would disagree with a status edit. Leave it approved — that is the honest state — or rebuild |
| 7 | **Trip marked completed** *(ch. 5.8, LW 7)* | `UPDATE StaffTravelRequests SET Status = 3, CompletedAt = NULL WHERE RequestNumber = '…'` *(3 = Approved)* |
| 8 | **Claim lines approved** *(ch. 9, LW 8)* | Covered by row 9 if you reset the claim |
| 9 | **Claim reviewed** *(ch. 9, LW 9)* | `UPDATE StaffTravelExpenseClaims SET Status = 2, FinanceReviewedById = NULL, ReviewedAt = NULL, ReviewNotes = NULL WHERE ClaimNumber = '…'` *(2 = Submitted)*, and set every line back to `Status = 1, AmountApproved = NULL` |
| 10 | **Claim paid** *(ch. 9, LW 10)* | ⚠ **The hardest one.** Payment recovered the advance, so **two** rows moved. Reset the claim as in row 9, *and* `UPDATE StaffTravelAdvances SET Status = 3, SettledAmount = 0 WHERE AdvanceNumber = '…'` *(3 = Disbursed)*, *and* clear `AdvanceDeducted` on the claim. **If you are not comfortable with that, do not perform LW 10** — read the pay dialog aloud and press Cancel instead |
| 11 | **Visa requirement added** *(ch. 10, LW 11)* | Removal is Admin. `UPDATE StaffTravelVisaRequirements SET IsDeleted = 1 WHERE …` |
| 12 | **Destination alert raised** *(ch. 11, LW 12)* | ⋯ → **Edit** → turn **Active** off. That works for `hr.head` and is the better undo anyway — an expired advisory is a real thing |
| 13 | **Travel policy approved** *(ch. 13, LW 13)* | **Withdraw** (Admin — any HR officer since closure lane 4) stands it down and keeps the record that it was approved. SQL, to make it a draft again: `UPDATE StaffTravelPolicies SET ApprovedById = NULL, ApprovedAt = NULL, IsCurrentVersion = 0 WHERE PolicyName = 'TDC Staff Travel Policy 2026'` |
| — | **§ 2.4 Option B's permission grant** | **Retired by closure lane 4.** The HR role holds `HR.Travel.Admin` by design (D-3): the role map carries it, `seed-db` grants it, and UAT holds it since 2026-10-02. ⚠ **Do not remove it** — the HR desk's budget approvals, breach decisions and the reminders screen depend on it |

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

⚠ A rebuild renumbers every travel request in this book and costs you chapter 2's prep — including
§ 2.4's policy decision, which you will have to make again.

---

## 18. The short path — 25 minutes

When the slot shrinks. Seven screens, in this order. This module has more good material than any
other in HR, so the cutting is genuinely painful — cut in the order given at the bottom.

| # | Screen | Min | The one thing |
|---|---|---|---|
| 1 | `/hr/travel` | 2 | four trips, four shapes; the globe icons; **money in each row's own currency** |
| 2 | `/hr/travel/[Lagos]` → **Itinerary** | 4 | the three legs, then **expand leg two's activities** — named contacts at LFZDC, mandatory flags. *"That is not a diary"* |
| 3 | The same trip → **Bookings** | 3 | the flight's **two segments** with terminals and seats; then the Lagos hotel inside the cap |
| 4 | `/hr/travel/[London]` → **Compliance** | 5 | *"fifteen working days to process"*; then Lagos's *"no visa, ECOWAS, zero days"*; then the **risk mitigation notes**. This is the duty-of-care beat |
| 5 | `/administration/hr/travel/policies` → the policy | 4 | the four enforced caps, then **Draft**, then *"I cannot sign it, and that is the point"* |
| 6 | `/hr/travel/claims/[Sebrepor]` | 5 | the three lines and the **receipt the line points at**; **review ≠ approving the lines**; then open the pay dialog and **read the advance recovery without pressing** |
| 7 | `/hr/travel/dashboard` | 2 | the tiles, then the **currency sentence** |

Cut, in this order if you must: the dashboard, then group travel *(never in the short path anyway)*,
then the bookings tab, then the itinerary — **never chapters 4, 5 or 6 of this table**, which are
the module's argument.

**Do not put in the short path:** raising a request *(Rule 3's two dropdowns)*, paying a claim
*(Rule 6 and the reset cost)*, the reminders screen *(403)*, or adding group participants.

---

## 19. What this walk found

> ⚠ **The live state of every finding below is now kept in
> `HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` (2026-10-01)** — its § 3d places each T-finding in a lane, as
> deferred, or as already fixed. Three statements here were already wrong when that review re-read the
> code: **T-5** (Finance's conversion was fixed on 2026-09-10), **T-53** (the reminder sweep has been
> hosted daily since 2026-08-17) and **T-28** (the server does refuse to cancel a Cancelled, Completed
> or Closed request). This section is the record of the walk, not a to-do list.
>
> **Closure lane 0 (2026-10-01, committed `0b8cdf124`)** changed what several steps here show: **T-3,
> T-12, T-15 and T-27 are fixed** (every dropdown is written from its enum; the composer has an
> *Internal note* toggle), the request form now sends back the fields the **T-29** replace would erase,
> the **T-6** copy says recovery happens on payment, and the attachment upload accepts all seven types.
> It also found that **no travel currency dropdown could be filled by an HR officer or a traveller** —
> they read a Finance list that answers 403 (closure finding O-19, fixed in the same lane). Where a step
> below warns about one of these, the warning describes the code before lane 0.
>
> **Closure lane 5, bookings and itinerary (2026-10-02) — lane 5 complete** (5a committed `4b7d12302`, 5b staged).
> **The itinerary (5b):** planned while the trip is open; its status the server's — Draft, **Finalised**, Superseded,
> Cancelled with the trip (D-25); a finalised or superseded version not changed; the days the trip's; the version in
> force not deleted (O-15); a leg links only this trip's bookings and is **flagged when its date disagrees with the
> booking's (T-19)**; legs and activities edited and removed. **A Critical or Emergency destination alert in force over
> the trip shows as a warning on the request page (T-45).** **Bookings (5a):** **Bookings live on an approved trip** (D-23): made, changed, held, confirmed and ticketed while
> the trip is Approved or under way, cancelled on any trip not closed, completed or a no-show once it has started.
> Every booking falls inside the trip's dates, a day either side. **The status moves only by the row's verbs** —
> an edit never changes it, a new booking is Pending, and the stamps, the fee, nights and totals are the server's
> (D1, D5). Cancelling records a reason as an internal note and, on a flight or hotel, the supplier's fee. **A flight
> on a trip needing a visa is ticketed once a visa application is approved or not required (T-24's ticketing half).**
> Only a pending booking is deleted. **A trip with a confirmed or ticketed booking is not cancelled** until those are;
> its holds are cancelled with it (D-24). Flight segments have a screen at last; hotels a star rating, ground transport
> an actual cost. The demo pack approves London and no longer books Lagos (D-26) — § 2.1 and the head of § 5.3 say
> what that changes on screen.
>
> **Closure lane 4, policy and authority (2026-10-02) — lane 4 complete** (4a committed `19f20f2f2`, 4b
> `3a6792a30`, 4c staged). **The policy (4a):** real cabin classes; an end no earlier than its start; a
> staff-level band in rank order; its money limits in a stated currency (base by default; the hotel cap had
> none, **T-9 fixed**). The server numbers versions, and whoever drafted or last changed a policy does not
> approve it (**T-50 fixed**). A directorate's policy covers its units, and the nearest unit's wins. A
> version approved to start later takes over on its own date instead of at once. A USD hotel rate is
> converted at Finance's rate before it meets the cap. **Bookings (4b):** the policy's advance notice and
> preferred suppliers bind (D-1). Ticking an exception now **asks** for it with a reason. The booking stays
> *Pending*, never *Confirmed*, until a travel administrator who neither booked it nor asked authorises it,
> or refuses it with a reason, on the new **Staff Travel → Policy Breaches** screen (D-8). An authorised
> exception stands until the booking changes. A booking with an exception is cancelled, not deleted (D-20).
> A policy exception is decided at the server's time by an administrator who did not raise it. A trip
> rated, or assessed, *Prohibited* is not submitted or approved. **Authority (4c):** the **HR role holds
> `HR.Travel.Admin`** (D-3; **T-2 fixed**), and each Admin act is refused to whoever did the thing it
> checks: a budget to whoever set or last changed it (D-19), a comment edited only by its author (D-21).
> The reminders screen opens to the HR desk (**T-52 fixed**). On the demo, `hr.officer` decides what
> `hr.head` did. Rules 1 and 2, § 2.4 Option B and chapters 13–14 were updated in place; lane 10 rewrites
> the six rules.
>
> **Closure lane 3, the money chain (2026-10-02) — lane 3 complete** (3a committed `e2785ce7b`, 3b
> `f49e5eb9c`, 3c `b08bd498d`). **Advances (3a):** raised only on an approved trip or one under way, for the
> trip's traveller; approved above zero, no more than asked and within the trip's approved budget; nobody
> approves, pays out or writes off their own, and the approver does not pay it out; **Reject, Cancel, Cash
> back and Write off** (Admin) buttons; nothing is owed until the cash goes out; the sweep marks an advance
> **Overdue** and a write-off marks it **Written off** (**T-21 fixed**); **Staff Travel → Advances** lists
> overdue settlements; a deleted claim's or advance's number is never issued again. **Claims (3b):** filed
> for the trip's traveller, in the base currency, only on a trip approved, under way or completed; each
> expense valued at Finance's rate on its own date (**T-37 fixed**); under an approved policy, receipts
> above its threshold (**T-35 fixed**, a per diem excepted) and its claim window (**T-36 fixed**) — both
> bind only once a policy is approved, which Rule 1 still blocks on the demo; an expense approved in whole
> or part with a reason for any cut; a claim **returned** to the claimant with a reason (**T-38 fixed**);
> nobody reviews or pays their own claim and no reviewer pays it; no payroll offset; paying in full past
> advance cash the claim does not name needs a recorded reason (**T-57** — naming the advance is still what
> recovers it, Rule 6). **The budget and the void (3c):** a budget is set once the trip is approved, in the
> trip's currency (**T-22 fixed**), within the trip's approved budget, its parts empty or adding up to the
> total; a travel administrator approves it, and a change withdraws the approval; *Actual* now counts
> advance cash paid out as well as claims paid, and an overrun is **flagged, not refused** (**T-20** stays
> open — whether it should refuse is TDC's question); a travel administrator who neither claimed nor paid
> it can **Void payment** on a paid claim with a reason — the journal is reversed, the advance's recovery
> undone and the claim goes back to approved (**T-39 fixed**). The three Admin acts are in Rule 2's table.
> *(Written before lane 4: "no demo persona holds Admin, so on the demo the budgets stay unapproved". Since
> lane 4 the pack approves Kumasi's budget as `hr.officer` and leaves Sebrepor's awaiting approval.)*
>
> **Closure lane 2, slice 2b (2026-10-02, committed `519418f00`) — lane 2 complete.** The screens: a **Travel
> Approvals** queue under Staff Travel; the request page's own **Approve, Reject and Return** buttons,
> drawn only for whoever may decide at the stage, with a banner saying whose decision it is; the approve
> dialog asks for the **approved budget** at the last stage, prefilled with the estimate (**T-10
> fixed**); an approver without travel permission sees the trip, not the desk's tabs. The workflow
> designer now keeps the travel route's named approvers when the route is edited (cross-module defect
> #34).
>
> **Closure lane 2, slice 2a (2026-10-02, committed `4e4d85b2f`)** put travel approval in **two stages** — the
> traveller's line manager, then HR (§ 1.6) — and gave the line manager a way in: the traveller's
> supervisor or head of unit can now open the request, its comments and attachments, find it in a queue
> of what waits for them, and approve, reject or return it, with no travel permission and no Manager
> role; a Manager outside the traveller's line can do none of it and is no longer told about it. HR sets
> the **approved budget** at its stage (**T-10** is fixed at the API; the dialog that asks for it is the
> next slice). The screens — the approvals queue, the approver's view of the request — are slice 2b.
>
> **Closure lane 1, slice 1c (2026-10-02, committed `895996b6f`) — lane 1 complete.** Groups now move by their own
> buttons (**Open to travellers, Close, Reopen, Cancel group**) instead of an edit's status dropdown; the
> **traveller limit binds** (**T-31 fixed**; a cancelled or rejected trip no longer holds a place); an
> **existing draft request can be linked** to a group (**T-30 fixed**) and takes the group's destination
> and dates; a new destination or new dates **reach every draft or returned trip** on the group
> (**T-32 fixed** — submitted and approved trips keep their own and are marked "differs from the group");
> a group cannot be cancelled while its travellers' trips are going ahead; deleting one takes its
> travellers off it. The add-traveller dialog asks for purpose, risk, visa and health clearance. A
> comment is edited or deleted by its author or a travel administrator only, and the traveller's portal
> receives no internal note and no policy exception from the server.
>
> **Closure lane 1, slice 1b (2026-10-02, committed `8fadfd31e`)** gave the request its other verbs: an approver can
> **return a request for revision** with a reason, an approved trip can be sent back with **Request
> change** and approved again (its bookings, advances and claims stay), the requester can **recall** a
> submission, and the desk can **close** a completed trip once nothing is left to settle — after which
> nothing more can be booked, advanced or claimed on it. **Cancel** now withdraws the approval in
> progress, and is refused for a trip under way and for an approved trip whose advance cash is still out;
> **Mark completed** waits for the trip to start; the request records **who approved** it. **T-7** is half
> fixed (Closed has a writer; InProgress waits for lane 8) and **T-28** stays corrected.
>
> **Closure lane 1, slice 1a (2026-10-02, committed `e1d050da2`)** changed the request form and submission: the form no
> longer offers an organisation unit — a request carries the **traveller's own unit**, and whether it is
> international follows from its two countries; a **Policy and limits** card shows the approved policy
> that will apply and its limits before saving (**T-16 fixed**); submission refuses a trip estimated above
> that policy's single-trip limit (**T-17 fixed** — it binds only once a policy is approved, which **T-1**
> still blocks on the demo), a trip costed at nothing, a trip over the days of another of the traveller's
> trips, and — from the portal — a trip whose departure has passed (the desk submits that one with a
> reason, kept as an internal note). A request can be edited only while it is a Draft or returned for
> revision, and travel cannot be raised for someone who has left. Approved leave over the trip's days is
> shown as a warning on submission.

**Fifty-eight findings.** This is the most complete module in HR by some distance — it was built as
twelve slices with 506 harness assertions and two closure slices on top, and the density of
*correct* decisions in it is higher than anywhere else in the product. Six behaviours will catch
you out in a demonstration; **five** are the ones to fix before it is called finished.

### The six that affect a demonstration

| # | Where | Finding |
|---|---|---|
| **T-1** | policies | **Every policy in every tenant is unapproved, so travel caps bind nowhere.** Approval is `HR.Travel.Admin` *and* needs an employee-linked caller, and no demo login is both. **This is a real behaviour change on the day somebody signs one** *(lane 4, D-3: `hr.officer` can sign now; the demo pack leaves the policy a draft until lane 10)* |
| **T-2** | everywhere | **`hr.head` cannot delete anything, approve a policy, or authorise a breach.** Mostly correct design — Admin here is a *financial* authority — but every button still renders *(**fixed** by lane 4, D-3: HR holds Admin, each act refused to whoever did it — Rule 2)* |
| **T-3** | request form | **Two dropdown values 400** — Purpose → *Negotiation* and Risk level → *Extreme* are not enum members. Five purposes and two risk levels are unreachable |
| **T-4** | policies | **The rule register is enforced by nothing and ships read-only** — a recorded decision, not an omission |
| **T-5** | everywhere | **Finance's currency conversion is inverted** and travel inherits it deliberately. The dashboard and the claims queue both refuse to add mixed currencies and say why |
| **T-6** | claims | **The advance is recovered on payment, not approval** — so `netPayable` and the pay dialog disagree until the moment you press the button |

### The five worth fixing first

| # | Where | Finding | Why it is first |
|---|---|---|---|
| **T-3** | request form | **Write the two unions from the enums.** A dropdown value that 400s is the only thing in this module that fails in front of a user for no reason | Twenty minutes, and it is the one a user will hit |
| **T-1** | policies | **Seed an approved policy, or decide that travel administrators are employee-linked accounts.** A control nobody can switch on is not a control | It is a seeding decision, not code — and until it is made, the module's best feature is inert |
| **T-23 / T-55** | compliance | **Give the risk-assessment acknowledgement a `/me` route**, exactly as the destination alert already has. Today the gate and the service check do not overlap, so **nobody** can acknowledge a security briefing | It is the duty-of-care record, and it is unreachable |
| **T-24 / T-42** | compliance | **Make `RequiresVisa` gate something.** A trip flagged as needing a visa can be approved, booked and completed with no visa application, into a country the requirements table says needs one *(lane 5: the ticket now waits for the visa; the derived flag is lane 7's)* | It is the failure the module exists to prevent |
| **T-58** | finance | **The GL hand-off.** No travel transaction posts to the general ledger; advances, claims and payments live only in travel's tables | It is the open item a finance director will find first |

### Everything else, by area

| # | Area | Finding |
|---|---|---|
| T-7 | requests | `InProgress` and `Closed` are unreachable — nothing moves a trip into progress on departure or closes it after payment |
| T-8 | bookings | **No vendor field on any booking form**; Procurement's `SuppliersController` answers 400, so *preferred vendor mandatory* checks nothing |
| T-9 | policies | **The hotel cap has no currency** — a bare decimal compared against a booking's rate. Sound only while both are in the same currency |
| T-10 | requests | `approvedBudget` is never sent on approval, so the Overview's *Approved budget* reads an em dash on approved trips |
| T-11 | register | Unpaged — `requests/all`, not `requests` |
| T-12 | register | The type filter offers four of eleven travel types |
| T-13 | register | No search, no date filter, no traveller filter — all three exist on the API |
| T-14 | register | No export |
| T-15 | request form | `Emergency` is missing from the travel-type list |
| T-16 | request form | The policy is neither chosen nor shown when a request is raised |
| T-17 | request form | `MaxSingleTripBudget` is not checked against the estimate |
| T-18 | bookings | **Only flights and hotels are capped** — ground transport and car rentals have no policy check at all |
| T-19 | bookings | Nothing reconciles a booking against the itinerary |
| T-20 | finance | The budget's per-line splits are not enforced — only the derived *Committed* figure moves |
| T-21 | finance | `Overdue` and `WrittenOff` advance statuses are set by nothing |
| T-22 | finance | A budget carries a currency on its DTO that no form offers |
| T-25 | compliance | Health requirements are shown per country and never checked against the traveller |
| T-26 | compliance | An expiring passport blocks nothing; only the reminder sweep reads the expiry |
| T-27 | comments | **An internal comment cannot be posted from the screen** — the composer always sends `isVisibleToTraveller: true` |
| T-28 | requests | Cancelling has no status guard on the server; the screen's own check is the only thing stopping a completed trip being cancelled |
| T-29 | requests | The update DTO is a **replace, not a patch** — every omitted field is written back as its default |
| T-30 | groups | **No way to link an existing request to a group from any screen** — the only door creates new ones |
| T-31 | groups | `maxParticipants` is not enforced |
| T-32 | groups | The group's dates and destination are neither pushed to nor checked against its participants |
| T-33 | claims | Unpaged and unfiltered, on the screen finance lives on |
| T-34 | claims | No export |
| T-35 | claims | **`ReceiptRequiredAbove` is not enforced** — a line of any size is accepted with no receipt |
| T-36 | claims | `ExpenseSubmissionDays` is not enforced |
| T-37 | claims | Claim and line currencies may differ, and the conversion is Finance's inverted one |
| T-38 | claims | `Returned` is set by nothing — a claim cannot be sent back for more information |
| T-39 | claims | **There is no payment reversal** |
| T-40 | visa requirements | Nothing expires a requirement, on a page whose own subtitle says a wrong entry is worse than a missing one |
| T-41 | visa requirements | No reverse view — you cannot ask where a given passport travels freely |
| T-43 | alerts | *(closed)* The compliance strip showed alerts with no body for months, because the per-country read returned a summary DTO and the client typed it as the full record |
| T-44 | alerts | **Raising an alert notifies nobody automatically** — notifications are created one at a time by an endpoint no screen calls |
| T-45 | alerts | An alert does not flag or block a booking to the destination it warns about, at any severity |
| T-46 | requests | `TravelRiskLevel.Prohibited` prohibits nothing |
| T-47 | dashboard | No date range — it is always "now" |
| T-48 | dashboard | Actual spend is not on it, though it is derived per request |
| T-49 | policies | **The policy-exception flow has no screen at all** — withheld deliberately, because nothing raises an exception until rule evaluation exists |
| T-50 | policies | Version number and *is current version* are caller-declared; nothing stops two current versions |
| T-51 | policies | `AppliesToLevelFromId` targets `StaffLevel`, not a salary level — an FK's name is not its target |
| T-52 | reminders | **The reads are Admin-gated with the writes**, so the travel desk cannot see the queue it would act on |
| T-53 | reminders | Nothing runs the sweep on a schedule; the only trigger is the button |
| T-54 | portal | **The traveller cannot raise an expense claim** — the one part of the money chain they would expect to start |
| T-56 | portal | The traveller cannot upload a document or a receipt |
| T-57 | claims | Choosing the advance when filing is what links the recovery — it is **not** inferred from the trip, so a claim filed without it is paid in full |

### What is genuinely strong here

This list is longer than the demo-affecting one, and that is the honest summary of the module:

- **The policy guard is a real financial control**, correctly placed: it refuses at the point of
  writing a booking, resolves the applicable policy from the traveller's staff level and unit rather
  than trusting the caller, takes domestic-or-international **from the request** so a booking cannot
  get a second opinion, and **returns the exception flag to store rather than accepting the caller's
  claim of it**. Before it existed, a caller could book First class, declare that the policy allowed
  First, tick their own exception, and nothing refused it.
- **Who may approve a breach is separated from who may book.** A travel clerk with Write cannot
  authorise their own exception; it needs Admin. And the service takes that decision as an explicit
  parameter rather than inspecting claims itself — *because a service that quietly inspects the
  caller's identity is the thing that made these fields spoofable in the first place.*
- **A policy is a draft until signed**, so the person spending the money cannot set their own limit.
- **The advance–claim link closes the money loop**, at payment, with `min(outstanding, approved)` —
  and the pay dialog is deliberately built not to quote a figure the act is about to change.
- **Around twenty actor holes were closed.** Every *"who did this"* used to be read from the request
  body; all five actor ids are now the token, and none is a form field.
- **Reviewing is not updating.** Three separate endpoints stamp a decider, and the plain update
  cannot touch those fields.
- **Derived money is recomputed on read, onto the DTO rather than the entity** — because a
  write-only rollup is stale immediately, and a read that writes is a different problem.
- **Mixed currencies are never silently added**, on either the dashboard or the claims queue, and
  both say why on the screen.
- **`isInternational` is derived from the two countries** rather than being a boolean a request could
  lie about.
- **The self-service surface takes no employee id anywhere**, and its 404 deliberately does not
  distinguish "not yours" from "deleted".
- **Two features were deliberately *not* shipped** — the rule editor and the exception flow — on the
  argument that an editable control that does nothing creates false assurance. The write paths stay
  implemented and harness-covered so enforcement is a screen change. That is a better answer than
  the feature would have been.

---

## Appendix A — every route, in demo order

| # | Route | Chapter | Persona |
|---|---|---|---|
| 1 | `/hr/travel` | 3 | hr.head |
| 2 | `/hr/travel/new` | 4 | hr.head |
| 3 | `/hr/travel/[id]` — Overview | 5.1 | hr.head |
| 4 | `/hr/travel/[id]` — Itinerary | 5.2 | hr.head |
| 5 | `/hr/travel/[id]` — Bookings | 5.3 | hr.head |
| 6 | `/hr/travel/[id]` — Finance | 5.4 | hr.head |
| 7 | `/hr/travel/[id]` — Compliance | 5.5 | hr.head |
| 8 | `/hr/travel/[id]` — Comments | 5.6 | hr.head |
| 9 | `/hr/travel/[id]` — Attachments | 5.7 | hr.head |
| 10 | `/hr/travel/[id]` — Workflow | 5.8 | hr.head |
| 11 | `/hr/travel/[id]/edit` | 6 | hr.head |
| 12 | `/hr/travel/groups` | 7 | hr.head |
| 13 | `/hr/travel/groups/[id]` | 7 | hr.head |
| 14 | `/hr/travel/claims` | 8 | hr.head |
| 15 | `/hr/travel/claims/[id]` | 9 | hr.head |
| 16 | `/hr/travel/claims/new` | 9b | hr.head |
| 17 | `/hr/travel/visa-requirements` | 10 | hr.head |
| 18 | `/hr/travel/alerts` | 11 | hr.head |
| 19 | `/hr/travel/dashboard` | 12 | hr.head |
| 20 | `/administration/hr/travel/policies` (+ `new`, `[id]`) | 13 | hr.head *(+ admin)* |
| 21 | `/administration/hr/travel/reminders` | 14 | **admin only** |
| 22 | `/me/travel` (+ `new`, `[id]`, `[id]/edit`) | 15 | **staff** |
| — | `/workflow/inbox` | 16 | the assignee |

---

## Appendix B — the permission map, in one table

`hr.head` holds **Read**, **Write** and **Approve**, and, since closure lane 4 (D-3), **Admin**. Before
lane 4 it did not. The rows below describe the tiers; Rule 2 lists who each Admin act is refused to.

| Tier | Actions |
|---|---|
| **No permission — signed in and internal** | the whole **`api/staff-travel/me`** surface: list, read, create, update, submit and cancel **your own** travel request; read and **acknowledge your own** destination-alert notifications. It takes no employee id anywhere |
| **`HR.Travel.Read`** | the **entire controller set** carries it at class level: every read of requests, groups, comments, attachments, itineraries, legs, activities, all four booking kinds and their segments, budgets, advances, claims and lines, per-diem rates, policies, rules, exceptions, documents, visa requirements, visa applications, risk assessments, alerts, notifications, insurance and health requirements — plus the **dashboard** |
| **`HR.Travel.Write`** | raise, amend, **submit**, **approve**, **reject**, **cancel** and **complete** a travel request; add and amend comments and attachments; create and version **itineraries**, legs and activities and **set the current version**; create and amend **every booking kind** *(subject to the policy guard)*; set a **budget**; request, **approve** and **disburse** an advance; create, add lines to, **submit**, **review a line**, **review** and **pay** an expense claim; maintain **per-diem rates**; author **policies** *(drafts only)* and their **rules**; create and **decide policy exceptions**; record and **verify travel documents**; maintain **visa requirements**, **visa applications**, **risk assessments**, **alerts**, **notifications**, **insurance** and **health requirements**; create and amend **groups** and **add participants** |
| **`HR.Travel.Admin`** — ***held by HR since lane 4*** | **every delete in the module** — and, more importantly: **approve a travel policy** *(which is what makes its caps bind at all)*, **withdraw a policy**, **authorise or refuse a booking's policy exception**, **decide a policy exception**, **write off an advance**, **approve a budget**, **void a payment**, and **the entire Reminders screen including its reads** |
| **`HR.Travel.Approve`** | interim authority to approve a travel request **where no workflow definition is published**. A `STAFF_TRAVEL_REQUEST` definition **is** seeded and published, so it does nothing on this database |

> **Two things worth naming.** First, **`HR.Travel.Admin` is a financial authority here**, not a
> housekeeping one: it decides what the organisation may spend on travel and who may exceed it. The
> permission map says so on purpose. Second, **a permission an unlinked account holds is a
> permission it cannot exercise** — three Admin writes stamp an `Employee` foreign key, and `admin`
> is SuperAdmin but not employee-linked. That is Rule 1's root cause, and it is a seeding question:
> are TDC's travel administrators real employees, or service accounts?

---

## Appendix C — related documents

| Document | What it adds |
|---|---|
| `plans/HR-Area-12-Travel-Build-Plan.md` | The canonical build plan — twelve slices, the decisions D-1…D-6, and the open TDC questions in § 9 |
| `dev-harness/hr-travel/README.md` | 506 assertions across 16 files, plus the defect table each slice found |
| `dev-harness/hr-demo-smoke/scenarios/080-travel.mjs`, `081-travel-logistics.mjs` | Exactly what the demo database holds, and how to rebuild it. **`080` first** — `081` reads the four requests it builds |
| `dev-harness/hr-demo-smoke/runbook/book-2-operations-hr.html` | § 3 is the short version of this guide, for the standard demo pack |
| `docs/HR/integration/HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md` | The currency and exchange-rate boundary, and why travel inherits Finance's conversion rather than disagreeing with it |
| `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` | The GL posting sweep (D-4) — 18 entries, of which travel's advances, claims and payments are several |
| `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` | **#1** Procurement's dead `SuppliersController` *(T-8)* and **#2** Finance's inverted conversion *(T-5)* — both owned by other teams |
| `docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md` | How `StaffTravelRequest` reaches the engine, after its bespoke chain was retired |
| `docs/HR/areas/company-schedule/HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md` | The neighbouring module, and the counter-example on approvals: company schedule deliberately does **not** use the engine |
| `docs/HR/areas/attendance/HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md` | Where a traveller's days show up as attendance, and the geofence that does not apply to them |

---

*End of the HR Staff Travel System Guide.*
