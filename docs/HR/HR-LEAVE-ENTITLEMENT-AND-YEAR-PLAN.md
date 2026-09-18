# HR Leave — Entitlement, Accrual and the Leave Year

**Status:** 2026-09-18 — **WAVES 1 AND 2 COMPLETE.** Migration
`20260918032031_AddLeaveYearEndBasisAndFirstYearProration` applied. `dev-harness/hr-leave` slices
10, 11 and 12 — **27 + 27 + 20 assertions, each green twice**; the whole suite green twice
afterwards, **501 across twelve slices**.

**⚠ All eight decisions are taken, all as recommended.** D-5's answer changed the work rather than
authorising it — see § 2.2 B4.

**ALL THREE WAVES ARE COMPLETE.** `dev-harness/hr-leave` — **515 assertions across thirteen slices,
green twice.** What remains of this plan is the deliberately-unbuilt: the anniversary leave year
(D-7) and half-day leave (D-8), both recorded as not-yet-asked-for rather than forgotten.

**Read [`HR-LEAVE-RESIDUE-CLOSURE-PLAN.md`](HR-LEAVE-RESIDUE-CLOSURE-PLAN.md) § 0 first** — it
describes the code as it stands after G1–G5. This plan does not revisit anything that one closed.

---

## 0. Progress

### Wave 1 — BUILT AND VERIFIED 2026-09-18

**D-3 taken as recommended:** a stored entitlement may be re-derived, but only by an explicit pass,
never as a side effect of the ordinary recalculation.

No migration — every change is behaviour on columns that already exist.

| Slice | What shipped |
|---|---|
| **W1a** | `CreateStandaloneAdjustmentAsync` resolves the entitlement engine instead of taking `DefaultDaysPerYear`. **`RepairEntitlementsAsync`** re-derives stored entitlements with a dry run, `POST /api/Leaves/balances/repair-entitlements`, Admin tier, and a **preview-then-apply** control on the balances screen |
| **W1b** | `LeaveTypeService` refuses a second active accrual policy on create **and** on an edit that would move one; `LeaveEntitlementService` orders its policy selection deterministically for rows that predate the guard |
| **W1c** | The register gained the **Unreachable** status, § 3.2b surveying leave's four child tables (38 settings), and a second methodological trap |

**⚠ The preview is the decision, and that shaped the UI.** The repair dialog lists every row and both
figures rather than asking somebody to authorise *"this will correct some entitlements"*. A
confirmation for a change you cannot see is not a confirmation.

**⚠ W1a did not need the seed reorder after all.** § 2.1 proposed swapping scenarios `020` and `021`
so the allocations exist before the balances are opened. The repair pass makes that unnecessary and
is better: the seeder can open balances in any order and put them right afterwards, which is also
what a real tenant does on go-live day. **The demo database still needs one repair run** — see below.

### What the harness found, and what it did not

**Slice 10's three defects were all in the slice, not the product** — the kind only a machine
driving the API finds:

| | What was wrong |
|---|---|
| the fixture query | the table is **`EmployeePositions`**, not `Positions`. A table is named after its `DbSet<>` property, and `Position`'s is not its own name |
| the actor | ⚠ **`admin` cannot post a leave adjustment.** `PerformedBy` is a required Employee foreign key stamped from the token and the admin account is not linked to an employee record, so it is refused by design. The HR desk is the right actor and is who loads opening balances in practice |
| ⚠ the refusal's status | **A2's guard returned 500, not 400.** Neither accrual-policy endpoint caught `InvalidOperationException`, so the refusal — whose whole value is the sentence naming what to do instead — arrived as *"Something went wrong"*. Found by reading the controller **before** the run rather than after, and fixed in the same build |

**⚠ The last one is the lesson.** A guard in a service is not a refusal until the controller has an
arm for it. The service was right, the harness would have failed, and the product would have shipped
a 500 where it meant to explain itself.

### ⚠ What Wave 1 does NOT do, stated so nobody assumes it

- **The demonstration database is still wrong, and it is now measured.** See below.
- **A carry-over already run for a year is not revisited.** The pass counts those rows separately
  and says so; re-running carry-over afterwards is a decision of its own.

### ⚠ The demonstration database — 53 of 97 annual leave balances are wrong

Verified 2026-09-18 by dry run and by direct query. ⚠ **The demo database is `ErpSystemDB_UAT`**, not
the `ErpSystemDB` the harness drives — which is why the first dry run reported *0 of 142 to change*
and briefly looked like good news. That database holds no demo leave data at all; every balance on it
is a harness fixture.

On the UAT database **every one of the 97 annual leave balances stores 21**, the leave type's
default, because scenario `020` opened them by adjustment before scenario `021` created the
allocations:

| Staff level | Employees | Stored | The rulebook says | |
|---|---|---|---|---|
| Junior | **39** | 21 | **15** | ⚠ overstated by 6 days each |
| Senior | 44 | 21 | 21 | correct by coincidence |
| Management | **14** | 21 | **30** | ⚠ understated by 9 days each |

**53 wrong, 44 right.** The leave guide instructs a demonstrator to read that figure aloud
(chapter 13, step 2).

**Fixing it is one press, on a database nothing here has touched:** point the API at
`ErpSystemDB_UAT`, then Balances → **Repair entitlements** → preview → apply, as admin, per year.
⚠ **Preview first and read the count** — 53 is the number to expect, and anything else means
something about that database differs from this reading.

### Wave 2 — W2a and W2e BUILT AND VERIFIED 2026-09-18

**D-1 taken as recommended:** `ProRateOnJoin = false` opens the accrual window with the **leave
year**, so somebody who qualifies at all accrues on the company's calendar. **ON is byte-for-byte
today's behaviour** — deliberately, because every existing row's value was arbitrary while the field
was inert, and an accrual figure must not move under a tenant that never chose anything.

**D-6 taken as recommended:** `PerPayPeriod` retired from the picker and refused by the API.

No migration. Neither slice needed one.

| Slice | What shipped |
|---|---|
| **W2a** | One condition in `LeaveEntitlementService`. ⚠ The service gate is untouched and still binds either way, so OFF cannot credit somebody for a year they never qualified in |
| **W2e** | Refused on create, and on an edit that **changes to** it. ⚠ A row already carrying it stays editable — the picker adds the retired value back for exactly that row — or retiring the option would strand it: blank select, every save refused |

**⚠ The data was checked before the meaning changed**, because this is a semantic migration in
disguise. On the UAT database `ANN` already has the switch **on** and `SICK` is a full grant, where
it never applied — so making it bind **changes nothing on the demonstration database**. Any other
tenant with an incremental policy and the switch off will see accrued figures go **up**, never down,
and no client can have been relying on the old behaviour because there was none to rely on.

**⚠ Slice 11 nearly shipped vacuously green, and that is the finding worth keeping.** Its first
draft pinned eligibility to 01 October against a 12-month gate; run in September the employee was
not yet eligible, **both positions returned 0**, and *"the two positions disagree"* would have passed
on two zeroes. The exact failure the slice exists to prevent, inside the slice. It now derives both
figures from today's date and **aborts before May** rather than report a green it cannot justify.

### W2d — BUILT AND VERIFIED 2026-09-18

**D-5 taken as rescoped.** No migration. Slice 11 gained nine assertions proving the derived rate
varies by staff level from one policy, **and** that setting an explicit rate defeats it in both
directions.

⚠ **The demo seed is corrected for FRESH databases only.** `ErpSystemDB_UAT` still carries the flat
1.75 and needs one field changed by hand — Annual Leave → Accrual → rate `0`. The guide's § 2.3b
says so and says which way the figures will move: a Junior's accrued goes **down**, a Manager's
**up**, and both are corrections.

### W2b / W2c — the model is in, awaiting one migration

**Two columns on `LeaveType`**, scaffolded together at the module owner's preference:

| Column | Default | Governs |
|---|---|---|
| `YearEndBasis` *(enum)* | **`Granted`** | what carry-over **and forfeiture** count as unused |
| `ProRateFirstYearEntitlement` *(bool)* | **`false`** | whether a joiner's first-year entitlement is scaled |

**Three decisions taken while writing the model**, recorded here rather than left implicit:

1. ⚠ **Renamed from the decision's wording.** D-2 called it *"carry-over basis"*; it is
   `YearEndBasis`, because it governs **both** year-end acts. A name that covers half of what a
   setting does is the thing this plan exists to stop.
2. ⚠ **`ProRateFirstYearEntitlement` will be REFUSED alongside an incremental accrual policy**,
   not silently ignored. Incremental accrual already limits a joiner to the part of the year they
   were present for, and because the derived per-period rate is *entitlement ÷ periods*, combining
   them would deduct for the same months **three times** — once through the accrual window, once
   through the scaled entitlement, and once through the rate that entitlement produces. So it is for
   leave types that **grant** rather than accrue: the mirror of `ProRateOnExit`, which applies to
   incremental accrual only.
3. **No third column for the pro-ration basis.** The encashment precedent stamps a basis onto a
   derived money figure; entitlement is recomputable from the hire date and the switch, the repair
   pass's notes already state both figures, and a column that exists to explain another column earns
   its place only when the inputs can move underneath it. ⚠ Recorded as a **choice**, so nobody
   later reads its absence as an oversight.

⚠ **A fourth projection nearly ate this.** `GetLeaveTypeDetailAsync` builds `LeaveTypeDetailDto`
field by field — a third place every `LeaveType` column must be listed, after the entity and the
mapper. Both new fields were added there deliberately; missing it is **exactly** harness finding 13,
where a column, entity, enum, DTO and parameter were all correct and the mapper was not, so the
value read back as its default however it was set.

### W2b and W2c — BUILT AND VERIFIED 2026-09-18

Both settings bind, both defaults preserve today's behaviour, and the refusal that keeps them apart
is enforced at both doors.

**⚠ One thing the harness changed about the implementation.** Writing slice 12 meant re-reading
`UnusedDaysAsync` against its own comment, which said *"a run for the CURRENT year must not count
months that have not happened"* — **and the code did exactly that.** The engine clamps a supplied
as-of date down to the year end but never to today, so asking for 31 December of the current year
projected the whole year's accrual. Under `Earned` that would carry days nobody had earned, which is
the one thing the setting exists to prevent. It now asks as at the **earlier of the year end and
today**.

**⚠ And one thing the harness changed about itself, which is the more useful lesson.** Slice 12
first asserted the carry-over preview's **total**. That endpoint is scoped by *employee*, not by
leave type, so the total sums every carry-over-eligible balance the fixture employee has
accumulated. It passed on the first run and reported **50 and 40** on the second.

> ⚠ **Green then red on unchanged code is the tell.** The product was right both times.

The slice now asserts the **difference** between the two positions, which isolates the variable under
test — and that single number proves the carry-over cap as well, because an unbound cap would make
the difference 14 rather than 10. Recorded as README finding 22, the **third** instance of one
shape: *never assert an exact figure read through an aggregate over a scope other fixtures can grow
into.*

### Wave 3, half one — BUILT AND VERIFIED 2026-09-18

**A pure refactor. No migration, no setting, no behaviour change** — and that was the point: the risk
in C1 is the call sites, not the setting, so the sites moved first while the right answer was still
known. `dev-harness/hr-leave` **501 assertions, green twice, unchanged**. Had anything gone red,
half one was wrong; there was no new behaviour to hide behind.

**`LeaveYear`** (beside `HrFiscalYear`) answers three questions in one place: which leave year a date
falls in, and where a leave year starts and ends. Every caller passes `LeaveYear.CalendarStartMonth`,
for which all three reduce exactly to the arithmetic they replaced.

⚠ **Five EF queries had to be HOISTED, and one of them would not have warned anybody.**
`r.StartDate.Year == request.StartDate.Year` — the right-hand side is a captured local, so it reads
as a perfectly translatable member access. A `LeaveYear` call there would have thrown at runtime, not
at compile time. Those now resolve the year in C# and ask the database for a **range**, which is also
the only form an index can use: `YEAR(column) = x` cannot.

### Wave 3, half two — BUILT AND VERIFIED 2026-09-18

**D-9 taken: change-once-at-setup.** Migration `20260918042423_AddLeaveYearStartMonth`.

| | |
|---|---|
| `LeaveYearStartMonth` on `CompanyHrPolicySettings` | default **1**, so nothing moved for anybody |
| **`ILeaveYearContext`** | one **scoped** owner of the value, read once per request. ⚠ The provider does not cache, so reading it per site would have put a query inside loops walking hundreds of balances — and caching it in five services is where the fifth copy drifts |
| The D-9 guard | refuses the change once the tenant holds leave data, **naming what exists** |
| The screen | its own card above encashment and reminders, because it is the setting those are measured against |

⚠ **The background sweeps deliberately do NOT use the context.** They loop every tenant inside one
scope, so a per-scope cache would hand the second tenant the first tenant's leave year.
`LeaveReminderService` reads `LeaveYearStartMonth` from the per-tenant settings it already loads.

**⚠ Why the refusal is a refusal and not a warning**, recorded because it is the whole of D-9: of
the figures a moved boundary disturbs, **only `CarriedOverDays` cannot be re-derived.** Used,
pending, adjustment and encashed days recompute; entitlement became repairable in W1a. Carry-over
was computed by a year-end run against boundaries that would no longer exist. A change that
half-corrects the ledger is worse than one that is refused.

### ⚠ Slice 13 is the only thing that could prove Wave 3

The other twelve slices run on a January tenant — which is every tenant that exists. A green suite
proved half one **changed nothing**, and structurally could not prove the sites would follow a
*different* answer. Slice 13 moves the boundary and asks:

- a plan dated **10 February** files under leave year **2025** with an April start and **2026** with
  January — the same date, two answers;
- an allocation dated **April–March** is matched by the entitlement engine, which January–December
  bounds would miss, falling through to the leave type's default.

⚠ **It sets the month by SQL, because the API refuses it** — that refusal is the feature, and is
asserted in the same slice. It restores the setting in a `finally` and deletes the plans it raises,
because every slice here is run twice.

### Still to build — after Wave 3

⚠ **Each needs a scaffolded migration**, which is why they are the two left. **D-2 and D-4 are
taken** — both per-leave-type settings whose defaults preserve today's behaviour exactly.

⚠ **W2c must not be built before W2b.** Pro-rating the grant is the other way to fix the carry-over
basis, and a client turning both on must not have the reduction applied twice — the guard for that
belongs in W2c, where both settings exist.

---

## 0.1 Where this came from, and what it is not

It came from one question, asked of the finished module: **"how does leave work for someone who just
joined the company?"**

Answering it properly meant reading `LeaveEntitlementService` rather than the guide, and that read
turned up nine things. **Six of the nine are not missing features.** They are places where the
product already answers a policy question — silently, in code, the same way the eight numbers in the
residue plan's § 4.4.6 were answered before they became settings. The other three are ordinary
defects.

⚠ **So this is the same turn the residue plan made, applied one level down.** That plan moved
tenant-wide constants onto a settings screen. This one is about the *entitlement engine*, where the
constants are not numbers but **assumptions**: that a leave year is a calendar year, that a leave day
is a whole day, that what you carry over is what you were granted rather than what you earned.

**What it is not:** a request for features nobody asked for. Two items (§ 2.3) exist only because a
client who is not TDC will ask, and they are sequenced last and marked as such.

---

## 1. The two rules this plan is built on

### Rule 1 — a value lives at the level at which it is a rule

The leave guide's § 1.11 already states the three levels: the **tenant**, the **leave type**, and the
**sub-type or allocation**. This plan adds the test that decides which one:

> **Could two leave types legitimately want different answers? Then it does not go on the tenant
> settings screen.**

Applied in § 4, which is the section to read if you only read one.

⚠ **And a warning that is the whole reason § 4 exists.** `CompanyHrPolicySettings` now carries
**46 fields across six unrelated areas**. It is the obvious home for anything that does not
obviously belong elsewhere, and that is exactly how it becomes unauditable. The cautionary tale is
in this repository: `HR-APPRAISAL-SETTINGS-AUDIT.md` found **14 of 50 appraisal settings do not
enforce what they say**. Of the eight values this plan introduces, **two** belong on that screen.

### Rule 2 — the two-position test, sharpened

The residue plan's § 1 said every setting ships with an assertion proving it binds **in both
positions**. That rule is right and it is **not strong enough**, because item B1 below would pass it.
`ProRateOnJoin` is read, on every call, in a branch that can never execute. A test that sets it true,
sets it false, and checks that the code read it would be green in both positions and prove nothing.

> **The sharpened rule: the two positions must produce DIFFERENT OBSERVABLE OUTCOMES on the same
> fixture, and the assertion must state both numbers.**

Not *"the accrued figure changed"*. **`14.00` and then `15.75`.** This is the same lesson slice 6
learned when its first green read `22 → 0, 30 → 0` on an employee with no salary — a direction is not
a figure, and a read is not an effect.

---

## 2. The register

### 2.1 Group A — defects. No decision needed, no setting introduced

#### A1 — a balance created by an adjustment ignores the entitlement engine

[`LeaveService.CreateStandaloneAdjustmentAsync`](../../src/ErpSystem.Core/Services/HR/LeaveService.cs)
creates a missing balance row with `EntitledDays = leaveType.DefaultDaysPerYear`. The other two
creation sites — `LeaveBalanceRecalculationService` and `LeaveYearEndService` — both call
`ResolveAnnualEntitlementAsync`, which resolves **sub-type cap → effective-dated allocation for the
employee's staff level → the type default**.

So a row first created by posting an adjustment permanently records the *default* instead of the
employee's *allocation*. And because `EntitledDays` is never refreshed (A3), it stays wrong.

⚠ **This is live on the demonstration database, and it is worse than one wrong column.**

- the demo seeds **every** opening balance by posting an adjustment
  (`scenarios/020-leave-balances.mjs` → `POST /Leaves/adjustments`), so **every balance row in the
  tenant was created through this path**;
- scenario `020` runs **before** `021`, which is what creates the staff-level allocations
  (Junior 15 · Senior 21 · Management 30). So even with A1 fixed, the engine would resolve to the
  default at the moment `020` runs, because the allocations do not exist yet.

**Two things therefore have to change together**, or the fix is invisible: the creation site, **and
the seed order**. The leave guide instructs a demonstrator to read that Entitled figure aloud
(chapter 13 step 2), so it is a number stakeholders have been shown.

#### A2 — two active accrual policies on one leave type are read arbitrarily

`GetAccruedAsOfAsync` selects the policy with
`.Where(p => p.IsActive && p.Frequency != None).FirstOrDefaultAsync(ct)` — **no ordering, and no
scoping to the employee.** Two active policies means whichever row the database returns first wins,
and it may differ between calls.

⚠ **This is the failure mode behind B4.** The natural thing for an administrator to want is a
different accrual rate per staff level, and the natural thing to try is a second policy. Today that
silently produces a non-deterministic answer rather than an error.

#### A3 — `EntitledDays` is frozen at creation, and nothing can repair it

`LeaveBalanceRecalculationService` states it in its own summary: *"Never touches EntitledDays or
CarriedOverDays."* That is deliberate for `CarriedOverDays` — the year-end run owns it. For
`EntitledDays` it means:

- an allocation corrected mid-year does not reach balances that already exist;
- **the tenant-wide recalculation added by L-19 cannot repair A1**, because it derives the counters
  and deliberately leaves entitlement alone;
- there is **no route in the product** to bring a stored entitlement back into line with the rulebook.

⚠ **A1 without A3 is a fix that helps nobody who is already wrong.** They are one slice.

**What A3 needs is a rule, and it is not obvious**, which is the only reason this is in Group A
rather than Group B: re-deriving entitlement on every recalculation would silently restate history
the moment somebody edits an allocation with a retrospective effective date. The recommendation is
in § 3 (D-3).

### 2.2 Group B — a switch that cannot bind, or a policy the product answers silently

#### B1 — ⚠ `ProRateOnJoin` can never change anything, in either direction

In `LeaveEntitlementService.GetAccruedAsOfAsync`:

```
eligibilityDate = hireDate + (MinServiceMonths ?? 0)
accrualStart    = Max(yearStart, eligibilityDate)
if (ProRateOnJoin && hireDate > accrualStart && hireDate <= yearEnd)
    accrualStart = hireDate
```

`MinServiceMonths` is non-negative, so `eligibilityDate ≥ hireDate`, so
`accrualStart ≥ eligibilityDate ≥ hireDate`. **`hireDate > accrualStart` is unsatisfiable.** The
branch cannot execute for any employee, any policy, any date.

**The pro-rating still happens** — the eligibility date already anchors the accrual window to the
hire date. What is missing is the ability to turn it **off**. An organisation whose rule is *"joiners
accrue on the company's calendar like everybody else"* flips the switch and gets no change.

⚠ **The `ProRateOnExit` fix (L-29) was written believing its twin worked**; the comment in the
service still says so — *"the mirror of ProRateOnJoin, which moves the START of the accrual window to
the hire date"*. It does not move it. The eligibility date got there first.

#### B2 — carry-over and forfeiture compute on the granted figure, not the earned one

`ProcessCarryOverAsync` uses `balance.AvailableDays`, and `LeaveBalance.AvailableDays` is
`Entitled + Carried + Adjustments − Used − Pending − Encashed`. **`EntitledDays` is the whole year's
grant**, whatever has actually accrued. `ProcessForfeitureAsync` forfeits the same figure.

So an employee who accrued 3.5 days, took none, and works for an employer with a five-day carry-over
cap carries **five days they never earned**.

⚠ **This is defensible, and that is precisely why it needs a setting rather than a fix.** Under the
module's own framing — *Available is what the year owes you; Can take now is what you may book
today* — carrying what you are owed is consistent. Under the other reading, carry-over is a reward
for leave you earned and did not take. **Both are ordinary employer policies.** The product currently
picks one, silently, and no screen says which.

#### B3 — entitlement is never pro-rated for a mid-year joiner

A December joiner's `EntitledDays` reads the full annual figure. Accrual throttles what they can
*book*, which imitates pro-rating from the employee's side — but the **ledger** says the full year,
and B2 means the ledger is what year-end reads.

An employer whose rule is *"first-year entitlement is days × remaining months ÷ 12"* cannot state
that anywhere. They can approximate the *effect* with accrual and still have the wrong number in the
Entitled column, on the balances screen, in the CSV export and in the carry-over run.

#### B4 — ⚠ the accrual rate CAN vary by staff level, and nothing said so

**This entry was wrong when written, and the correction is the most useful thing in this plan.**

The original reading was that a flat `AccrualRate` made one rate serve every grade. It does — *when
it is set*. The line that matters is the fallback:

```
ratePerPeriod = AccrualRate > 0 ? AccrualRate : annual / periodsPerYear
```

`annual` is the entitlement engine's answer **for that employee**, which is their staff-level
allocation. So **a rate of zero derives the rate per person**: a junior on 15 days accrues 1.25 a
month and a manager on 30 accrues 2.5, from one policy.

**Proved 2026-09-18** against two employees on different levels, one policy, rate 0:

| Staff level | Entitled | Accrued to date |
|---|---|---|
| lower | 12 | **8** — eight months at 12 ÷ 12 |
| higher | 36 | **24** — eight months at 36 ÷ 12 |

⚠ **So the defect was never the model. It was that nothing told anybody**, and the trigger is the
digit `0`, which reads as *"accrues nothing"* — the opposite of what it does. Nobody discovers that
by looking at the form.

⚠ **And the demo seed had it wrong**, which is the proof that it is undiscoverable: `accrualRate:
1.75`, flat, set before the staff-level allocations existed and never revisited. A junior entitled
to 15 reached their cap in September; a manager entitled to 30 could never pass 21, because
1.75 × 12 is 21. **Wrong in both directions at once, and nothing errored.**

#### B5 — `AccrualFrequency.PerPayPeriod` is treated as monthly

`PeriodsPerYear` maps `PerPayPeriod → 12` and `CompletedPeriods` counts elapsed months for it. On a
fortnightly or weekly payroll that is simply wrong, and the option's name is an active claim that it
is not.

⚠ **The pay cycle is payroll's fact, not HR's.** Modelling it here would create a second rulebook for
something another module owns — the shape [`HR-PAYROLL-BOUNDARY.md`](HR-PAYROLL-BOUNDARY.md) exists
to prevent. **The recommendation is to remove the option, not to implement it.**

### 2.3 Group C — structural. Sequenced last, and neither is a bigger version of Group B

#### C1 — the leave year is the calendar year, and cannot be anything else

Hardcoded throughout: `new DateOnly(year, 1, 1)` … `(year, 12, 31)` in the entitlement engine, and
`StartDate.Year` as the year a request belongs to. **Carry-over expiry and the forfeiture cut-off are
both measured from 1 January**, so *"carry-over expires after 3 months"* means 31 March for everybody.

`CompanyHrPolicySettings.FiscalYearStartMonth` **exists and leave ignores it completely** — only
recruitment's budget check reads it (`HrFiscalYear`). So a July–June leave year is not available
either.

**The size, measured rather than guessed:**

| Surface | Count |
|---|---|
| Year-keyed sites in the seven leave services | ⚠ **49 by raw grep, 30 in reality** — corrected 2026-09-18, see below |
| HR screens carrying a year selector | **7** |
| Portal screens carrying a year | **3** |
| Harness slices that would need re-running | **9** (427 assertions) |
| Unique index that encodes the assumption | `(EmployeeId, LeaveTypeId, LeaveSubTypeId, Year)` on `LeaveBalances` |

### ⚠ The 49 was wrong, and the correction is the useful part

A raw grep for year-keyed expressions counts **two different things**, and only one of them is work:

| Shape | Count | Is it affected by where the year starts? |
|---|---|---|
| `lb.Year == year` on a balance, adjustment, encashment or plan | ~17 | **No.** That is a stored `int` **LABEL** on a row, not a date |
| `new DateOnly(year, 1, 1)` / `(year, 12, 31)` boundaries | 7 | Yes |
| a **date** mapped to a year — `StartDate.Year` | 25 | Yes |

⚠ **That distinction is why `LeaveBalance.Year` can stay an `int` through this whole wave**, and
treating all 49 alike would have broken it. A leave year is labelled by the calendar year it starts
in, so the label survives; what changes is which dates map to which label.

**Two more sites lived outside the services and this inventory missed them**:
`LeaveMappingExtensions` (a plan's year follows its dates) and
`LeaveRepository.GetEmployeeLeaveHistoryAsync`. **32 in total.**

⚠ **Two modes, and they are not the same job.**

- **Fiscal leave year** (a start month other than January) is contained: `Year` keeps meaning *a
  year*, labelled by the calendar year it starts in, exactly as `HrFiscalYear` already labels the
  fiscal year. One helper, one setting, and the 49 sites resolved through it.
- **Anniversary leave year** (each employee's own year, running from their hire date) is a different
  data model. `LeaveBalance.Year` stops being a year and becomes *which of your personal leave
  years*; the balances screen's year picker, the year-end runs, the compliance register and every
  "this year" read stop meaning what they say. **It is not a larger version of the first. Do not
  scope them together.**

#### C2 — a leave day is a whole day

`CalculateLeaveDaysAsync` is `(await GetChargeableDaysAsync(...)).Count` — the **count of chargeable
dates**. `TotalDays` is `decimal(5,2)` and adjustments already step by 0.5, so the ledger can hold a
half day and no request can produce one.

⚠ **This is not a setting you switch on.** It changes what a leave day is, and it reaches the
chargeable-day list, the balance arithmetic, the attendance posting (one row per day — which day is
the half?), and the payroll export that reads `DaysOnLeave`. The good news is that attendance
**already has a `HalfDay` status**, so the destination exists.

### 2.4 ⚠ A fourth setting status this codebase needs a word for

`HR-CONFIGURATION-REGISTER.md` classifies every value as **Enforced · Advisory · Client-side ·
Ghost**, and its own tool would report `ProRateOnJoin` as **referenced** — because it genuinely is,
by the entitlement service, on every call.

B1 is none of the four. It is **read, in a branch that cannot execute.** A mechanical survey can
never find it, and it is the most dangerous kind precisely because it *survives* one: the tool
reports it consumed and everybody moves on.

**Proposed addition to the register's vocabulary:**

| Status | Means |
|---|---|
| **Unreachable** | The value is read, and the code path that acts on it cannot be entered. Only a human read finds these. ⚠ A mechanical survey reports them as consumed |

### 2.5 The register's own gap, found on the way

`HR-CONFIGURATION-REGISTER.md` § 3.2 records **`LeaveType` — 26 settings, zero ghosts**, and § 1–3
present leave as complete. **The leave child tables were never surveyed:** `LeaveAccrualPolicy`,
`LeaveSubType`, `LeaveCategoryAllocation`, `LeaveTypeEligibility` appear nowhere in it.

`ProRateOnJoin` lives on one of them. So *"leave is complete in the register"* is a stronger claim
than the survey supports, and closing that gap is part of slice **W1c**.

---

## 3. Decisions

⚠ **None of these is taken.** Each is a recommendation with its reasoning, for the module owner to
accept, reject or replace. **No slice in § 5 should start before the decision it depends on.**

| # | Question | Recommendation | Why |
|---|---|---|---|
| **D-1** | What should `ProRateOnJoin = false` mean? | **Accrue from the start of the leave year regardless of hire date** — the accrual window starts at the later of the year start and the eligibility date, and the hire date is used only when the switch is on | It is what a reader of the field name expects, it is the only reading under which the switch does anything, and it makes the pair with `ProRateOnExit` symmetrical |
| **D-2** | Carry-over and forfeiture: granted or earned? | **A per-leave-type setting, defaulting to GRANTED** | Granted is today's behaviour. A default that silently reduces people's carried days on the next year-end run is worse than an explicit setting — the same reasoning that gave `AllowInServiceEncashment` the conservative default and the demo tenant an explicit seed line |
| **D-3** | May a stored `EntitledDays` be re-derived? | **Yes, but only on an explicit repair pass** — a new admin action, never as a side effect of the ordinary recalculation | Re-deriving on every recalculation would restate history the moment somebody edits an allocation with a retrospective effective date. An explicit pass is auditable and can be previewed |
| **D-4** | First-year entitlement pro-rating | **A per-leave-type setting, defaulting to OFF** (no pro-rating), with the pro-rated figure stored on the balance and the basis stated on screen | Off is today's behaviour. ⚠ It interacts with D-2: pro-rating the grant is the other way to fix B2, and a client that turns both on must not have the reduction applied twice |
| **D-5** | Accrual by staff level | ⚠ **Rescoped by the finding above, then taken: surface the existing behaviour and fix the demo seed.** No column, no migration | A second mechanism for a rule the product already expresses is how two mechanisms drift. What was missing was that anybody could tell |
| **D-6** | `PerPayPeriod` | **Remove it from the picker and refuse it at the API**, leaving the enum value in place for stored rows | The pay cycle is payroll's fact. Implementing it here creates a second rulebook; leaving it visible is a claim the product cannot honour |
| **D-7** | Leave year | **Build the fiscal mode. Do not build the anniversary mode until a client asks**, and record that in the register rather than leaving it unwritten | § 2.3 — they are different data models. Building the second speculatively would change what `Year` means for every existing tenant |
| **D-8** | Half-day leave | **Defer.** Record it as a known limit in the guide and the register | It is a data-model change, not a setting, and no client has asked. ⚠ It should not be smuggled in as part of any slice below |

---

## 4. Placement — where each value lives

**This is the section to read if you read one.** Rule 1 applied, item by item.

| Value | Home | Why there and not elsewhere |
|---|---|---|
| **Leave year start month** | **`CompanyHrPolicySettings`** | A company has one leave year. Two leave types on different years would make a single balances screen incoherent |
| **Leave year mode** *(calendar / fiscal)* | **`CompanyHrPolicySettings`** | It changes what `Year` means on every balance row; it cannot vary per type |
| **Carry-over basis** *(granted / earned)* | **`LeaveType`** | It belongs beside `AllowCarryOver`, `MaxCarryOverDays` and `CarryOverExpiryMonths`, which are already per type. Sick and annual leave may legitimately differ |
| **First-year pro-rating** | **`LeaveType`** | A rule about a kind of leave. Maternity is not pro-rated; annual might be |
| **`ProRateOnJoin` semantics** | **stays on `LeaveAccrualPolicy`** | No move. Make the existing field bind |
| **Accrual rate by staff level** | **`LeaveAccrualPolicy` + `StaffLevelId`** | Mirrors `LeaveCategoryAllocation`. ⚠ **Not** the tenant screen — it varies by type *and* by level, which is two reasons it fails the test |
| **Half-day / minimum increment** *(deferred)* | **`LeaveType`** | Half-day annual leave, whole-day maternity |
| **Pay-period length** | **payroll's**, or nowhere | § 2.2 B5 |

### ⚠ What does NOT go on the HR Policy Settings screen

Everything above except the first two. The test from Rule 1 answers every one of them, and the cost
of getting it wrong is not a misplaced field — it is a settings page large enough that nobody audits
it, which is how 14 of 50 appraisal settings came to claim things they do not do.

**The two that do belong there both pass the same test for the same reason:** they do not configure a
*kind of leave*, they configure *what a year is*, and every kind of leave in the tenant has to agree
about that.

---

## 5. The slice plan

Ordered so each ships alone. **Wave 1 carries the live defect**, as G1 did.

### Wave 1 — the defects. No decisions except D-3

| Slice | What | Size | Notes |
|---|---|---|---|
| **W1a** | **A1 + A3 together.** Creation site calls the entitlement engine; a new **admin repair pass** re-derives `EntitledDays` for a year, with a dry run. ⚠ **Includes swapping the demo seed order** so allocations exist before balances are opened | **M** | needs D-3. No migration |
| **W1b** | **A2.** Refuse a second active accrual policy per leave type at write; order deterministically at read so existing data cannot flip | **S** | no migration |
| **W1c** | **The register gap** — survey `LeaveAccrualPolicy`, `LeaveSubType`, `LeaveCategoryAllocation`, `LeaveTypeEligibility`; add the **Unreachable** status and B1 as its first entry | **S** | documentation. ⚠ Do it **before** W2a, so the register records the defect before it records the fix |

⚠ **W1a is the one with a live consequence.** Until it runs, every Entitled figure on the
demonstration database is the leave type's default rather than the employee's allocation, and a
demonstrator is instructed to read that number aloud.

### Wave 2 — the settings. Each needs its decision first

| Slice | What | Size | Notes |
|---|---|---|---|
| **W2a** | **B1 — make `ProRateOnJoin` bind.** ⚠ The acceptance test is Rule 2's sharpened form: the same fixture, two positions, **two stated accrual figures** | **S** | needs D-1. No migration |
| **W2b** | **B2 — the carry-over basis**, per leave type, defaulting to granted. Both year-end jobs read it; the dry run states which basis it used | **M** | needs D-2. Migration |
| **W2c** | **B3 — first-year entitlement pro-rating**, per leave type. ⚠ Must not double-count with W2b | **M** | needs D-4 **and** D-2. Migration |
| **W2d** | **B4 — surface the derived rate.** ⚠ **Rescoped from "add a column" to "say what the field does"**: the table reads *derived from entitlement* instead of `0`, the form explains the consequence in both positions, and the demo seed's flat 1.75 becomes 0. **DONE 2026-09-18** | **S** | no migration |
| **W2e** | **B5 — retire `PerPayPeriod`** from the picker and the API | **S** | needs D-6. No migration |

### Wave 3 — the leave year

| Slice | What | Size | Notes |
|---|---|---|---|
| **W3a** | **C1, fiscal mode only.** A `LeaveYear` helper beside `HrFiscalYear`; the setting; all **49** call sites resolved through it; the 7 HR and 3 portal year selectors labelled with the real period | **L** | needs D-7. Migration. ⚠ **Re-run all 9 harness slices** — the year is in most fixtures |
| **W3b** | **Anniversary mode** | **XL** | ⚠ **Not scheduled.** § 2.3 — it is a different data model. Recorded so nobody scopes it into W3a |

---

## 6. Out of scope

| Not doing | Why |
|---|---|
| **Half-day leave** | D-8. A data-model change, unasked for, and it must not be smuggled into another slice |
| **Anniversary leave year** | D-7 / W3b |
| **Per-sub-type balances** | Decision D-2 of the original closure plan stands: one pot per leave type, the cap enforced as a query |
| **Reusing `FiscalYearStartMonth` for the leave year** | ⚠ Tempting and wrong. The finance year and the leave year are different facts and many organisations run them apart. Coupling them is invisible until a client wants a July leave year on a January fiscal year, and by then two modules read the field |
| **Anything payroll owns** | The pay cycle (B5), and what unpaid leave is worth (L-30) |
| **Re-opening the residue plan's items** | G1–G5 are built and verified; nothing here touches them |

---

## 7. Verification

**The acceptance rule is § 1's Rule 2**, and it is stricter than the residue plan's:

- every setting introduced ships with assertions that state **both figures**, not both directions;
- **W2a's assertion is the one that matters most** — it is the test that would have caught B1, and a
  naive version of it would not;
- W1a needs an assertion that a balance opened **by an adjustment** carries the employee's
  allocation, not the type default — the case nothing has ever asserted;
- W3a re-runs all nine slices, because the year is in most fixtures rather than beside them.

⚠ **A harness proves the server and says nothing about whether a person can get there.** The lesson
of commit `66543029` — five endpoints that were green throughout and reachable by nobody — applies to
every slice here that introduces a setting. **A setting with no control on a screen is a constant
with extra steps.**

---

## 8. Related documents

- [`HR-LEAVE-RESIDUE-CLOSURE-PLAN.md`](HR-LEAVE-RESIDUE-CLOSURE-PLAN.md) — § 0 is the current state
  of the module, and § 1 is the rule this plan sharpens
- [`HR-LEAVE-CLOSURE-PLAN.md`](HR-LEAVE-CLOSURE-PLAN.md) — the first build, and decision D-2 on
  sub-type balances
- [`HR-LEAVE-SYSTEM-GUIDE.md`](HR-LEAVE-SYSTEM-GUIDE.md) — § 1.2 and § 1.3 describe the engine this
  plan changes; § 1.11 states the three levels Rule 1 extends. ⚠ **Neither answers the new-joiner
  question**, which is what started this
- [`../HR-CONFIGURATION-REGISTER.md`](../HR-CONFIGURATION-REGISTER.md) — § 2.4's fourth status and
  § 2.5's gap both belong in it
- [`HR-APPRAISAL-SETTINGS-AUDIT.md`](HR-APPRAISAL-SETTINGS-AUDIT.md) — 14 of 50, and the reason § 4
  resists the tenant settings screen
- [`HR-PAYROLL-BOUNDARY.md`](HR-PAYROLL-BOUNDARY.md) — why B5 is removed rather than built
