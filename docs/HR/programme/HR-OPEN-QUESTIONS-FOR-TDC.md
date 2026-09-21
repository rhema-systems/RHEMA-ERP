# HR Module — Open Questions for TDC

**Prepared 2026-08-17.** Raised as HR areas 1–11, 15 and 24 were completed (14 of the module's
27 areas).

Each item below is something the implementation has had to **assume** because the requirement
document does not settle it, or something the system needs **data** for that TDC has not yet
supplied. Where we assumed, the assumed value is stated and is easy to change — but it is running in
code today and should be confirmed rather than discovered later.

**Items 1–4 and 7 need an answer.** Items 5, 6 and 8 are decisions we are recording rather than
questions we are blocked on.

---

## 1. How long does an employee have to answer a written query?

**Requirement:** FR-HR-177 says a written query shall be issued within **48 hours** of an alleged
offence. It does not say how long the employee then has to respond.

**What we assumed:** **72 hours** (`QueryResponseWindowHours`). After that window closes, the case
may proceed to a disciplinary decision whether or not the employee has answered — silence is not a
veto.

**Why it matters:** this window is load-bearing. A disciplinary decision is **refused** by the system
until the employee has been issued a written query and either acknowledged it or let this window
close. Set it too short and the process becomes unfair; too long and every case stalls.

**The sub-question:** should this be counted in **calendar hours** or **working days**? FR-HR-180's
appeal windows are counted in *working days*, and the system already has a working-day calculator for
them. If TDC wants working days here too, both should be converted together so that all disciplinary
clocks count the same way.

---

## 2. How long may a grievance sit at one rung before it is chased?

**Requirement:** FR-HR-181 names the escalation route — Employee → Supervisor → Head of Department →
HR → GM Finance & Administration → Managing Director → Board — but sets no time limit at any rung.

**What we assumed:** **5 days** (`GrievanceRungChaseDays`). After 5 days with no response at the
current rung, the system sends a reminder to whoever HR assigned to answer it.

**Why it matters:** this is a reminder threshold only — it does not auto-escalate, and the employee
keeps the sole right to decide whether to escalate. But an unanswered grievance is the most common
way a grievance procedure fails, and 5 days is our number, not TDC's.

---

## 3. TDC has no public holiday calendar loaded

**Measured on the live database, 2026-08-17: 0 holiday calendars, 0 public holidays.**

**What depends on it:** FR-HR-180 gives an employee **5 working days** to file a disciplinary appeal
and the employer **10 working days** to decide it. "Working days" means Monday–Friday less public
holidays.

**What happens today:** with no calendar configured, the calculation falls back to Monday–Friday
only. This errs in the **safe direction** — a deadline lands no later than it should, so nobody's
appeal is refused because of a holiday the system did not know about — but public holidays will not
extend appeal windows until a calendar exists.

**What we need:** confirmation of who maintains the calendar, and the Ghana public holiday list for
the current and next year so it can be loaded. Note this also affects **leave** and **attendance**,
which read the same calendar.

---

## 4. Will reporting lines and departmental heads be maintained? *(the most consequential item)*

**Re-measured 2026-08-31:**

| | |
|---|---|
| Organisation units with a named head | **0 of 41** |
| Employees with a named line manager | **3 of 562** (0.5%) |
| Employees placed in an organisation unit | **538 of 562** (96%) — this one *is* usable |

⚠ **Read the ratios, not the totals.** The employee population has been 5,579, then 1,650, and is
562 today, because the database is rebuilt from the EF model rather than migrated. What has not
moved in three measurements across two weeks is the shape: **no unit has ever had a head**, and the
share of employees with a line manager has gone 14% → 10.6% → 0.5%. Whatever the seed does, nobody
is maintaining these two columns.

*(Earlier readings, for the record: 0 of 41 heads and 175 of 1,650 managers on 2026-08-17; 175 of
1,486 on 2026-08-16.)*

**What this blocks — three separate requirements, one missing foundation:**

- **FR-HR-080** — "a Head of Department may issue verbal warnings only." The system cannot tell who a
  head of department is, so the rule is built and correct but cannot be switched on; disciplinary
  decisions remain HR-only.
- **FR-HR-181** — the grievance ladder's Supervisor and HOD rungs. Worked around: HR names a
  responder explicitly on each grievance rather than the system deriving one.
- **FR-HR-173** (staff movements) — the establishment/vacancy rule. Already downgraded from a blocking
  control to an **advisory** for this reason, because deriving it would have refused nearly every
  movement.

**The decision to take.** If reporting lines and unit heads are going to be maintained as live data,
these three requirements can be implemented as written and we would ask for a plan and a date. **If
they are not**, then inferring authority from the org chart is the wrong design however well it is
built — it would resolve to nobody for 89% of staff. The alternative we recommend is **explicit
assignment**: a small administrative screen on which HR names who may issue which sanction, and who
answers grievances at each rung, maintained directly rather than derived.

We would rather settle this now than work around it a fourth time.

---

## 5. For confirmation: a disciplinary decision is blocked until the employee has been heard

**This rule is not in the requirement document — we added it.** The system refuses to record a
disciplinary decision until the employee has been issued a written query and has either answered or
let the response window (item 1) close.

**The reasoning:** a missed deadline is a *late* act, and refusing the next step cannot undo it.
Sanctioning someone who was never asked to explain is a *void* act — one that will not survive
challenge, with the system's own record showing it permitted the shortcut. The cost of the gate is
one extra click; the cost of not having it is a dismissal that is set aside.

**There is a documented exception.** HR can waive the requirement through
`waive-query-opportunity`, which demands a written reason, stores it on the case and logs it. An
exception path that leaves a trace is standard practice; one that does not exist means people work
around the system entirely.

**No carve-out for summary dismissal or gross misconduct** — "summary" means without *notice*, not
without *process*.

We are asking TDC to confirm they accept this, because it changes what HR must do operationally.

## 6. For information: disciplinary reminders stop chasing after 90 days

The first live run of the disciplinary reminder sweep against TDC's own data queued **275 reminders,
242 of them written-query breaches on cases reported months ago** — burying the six items somebody
could have acted on that morning.

Obligations more than **90 days** past due are therefore no longer chased by daily reminders. They do
**not** disappear: they remain on every work queue and on each case's advisories, which is where a
historical breach belongs. The number passed over is logged each run, broken down by kind.

The equivalent engines for staff movements and for Safety, Health & Environment were checked on
17 August and have **no comparable backlog** (SHE's largest run queued 38, none of them more than 90
days overdue; movements, 3). No cap has been applied to either — and if one is ever needed for SHE it
should be designed separately, because an expired safety permit stays actionable indefinitely in a
way that a lapsed 48-hour procedural window does not.

## 7. How does TDC actually pay for medical treatment?

**The system supports three routes and we cannot tell which one TDC uses**, because there is no data
to infer it from — every medical table was empty when the area was built (0 healthcare facilities,
0 insurance policies, 0 claims of any kind).

The three are genuinely different processes, not variations:

1. **NHIS** — the facility bills the National Health Insurance Scheme, and TDC's involvement is
   tracking the claim and its settlement.
2. **Private insurance** — TDC holds a policy per employee, the insurer is billed, and TDC tracks
   utilisation against an annual limit.
3. **Out-of-pocket reimbursement** — the employee pays the facility, then claims the money back
   from TDC.

**Why it matters:** only the third puts employees in the system. We have built it so an employee
files their own claim and attaches their receipt, rather than handing paperwork to HR — partly
because that is what an ERP is for, and partly because HR both raising and approving a claim makes
the approver recorded against it meaningless. If TDC's reality is mostly (1) or (2), that screen
matters far less than the insurer and NHIS registers, and we would arrange the module accordingly.

**What we need:** roughly what share of medical spend goes through each route, and whether
employees are expected to claim money back at all.

**Not blocking.** All three are built and working. This changes emphasis and where staff are
pointed, not whether the module functions.

## 8. For information: two areas deliberately deferred

- **Separation / exit.** The discipline area records a termination and the exit checklist, and hands
  off. It does **not** compute what a leaver is owed, block a separation on clearance (**FR-HR-091**),
  or handle the non-disciplinary exit routes — resignation, retirement at 60 (**FR-HR-093**), contract
  expiry, death, mutual agreement. These need their own module. **FR-HR-092** (the MD signs all
  terminations except procedural ones) is already satisfied by approval routing rather than a
  signature flag.
- **Full grievance / employee relations.** The FR-HR-181 ladder and the grievance record are built.
  Case conferencing, union and representative handling, anonymous or whistleblower intake, and
  grievance analytics are not.

If either is needed sooner than the current plan plots them, that changes the sequence.

---

## Earlier open decisions still awaiting an answer

- **Public certificate verification (training).** The system can verify a training certificate by
  code without a login, and the endpoint is built and rate-limited for exactly that. No public web
  page was built, because it would be the application's first unauthenticated page and that needs its
  own security review. HR currently verifies codes from inside the system. **Question:** does TDC want
  outside parties — employers, regulators — to verify certificates directly?
- **Attendance rate denominator.** Weekends, public holidays and rostered off-days are excluded from
  the attendance-rate calculation. **Approved leave is left in the denominator**, so a month with
  leave in it reads below 100%. This is a policy choice and easy to reverse — **confirm it is the one
  TDC wants.**

---

## Operational prerequisite — nobody holds the Internal Audit role (raised 2026-08-20)

**FR-HR-185** requires Internal Audit to review a final settlement **before payment is released**,
and Chapter 12 states the same chain. Measured on 2026-08-20, the role existed —
`TDC_INTERNAL_AUDIT`, seeded by the procurement work — with **zero members**.

⚠ **Re-measured 2026-08-31, and it is worse than a missing grant: the role is not there at all.**
`SELECT Name FROM AspNetRoles` returns 47 roles and **neither `TDC_INTERNAL_AUDIT` nor
`TDC_MANAGING_DIRECTOR` is among them** — only the plain `Managing Director` spelling survives. So
the administrative grant this section asks TDC for cannot be made today: **the role has to be created
first**. The likely reason is this database being built from the EF model rather than from
migrations, which skips whatever seeded it; that makes it an environment question as much as a TDC
one, and it should be settled before anyone tries to action the answer below.

Concretely, it blocks more than a queue: `hr-separation/run-slice8` and `run-slice10` cannot execute
at all — every settlement-review call is a 403 — so the FR-HR-185 pipeline is currently unverifiable
end to end, not merely unused.

The HR separation module (area 9b) builds the review step as specified. Until somebody is granted
that role, **no final settlement can be reviewed and therefore none can be paid** — the control
will hold the payment rather than fail open, which is the correct behaviour but will look like a
stuck queue.

For contrast, the other half of the chain is fine: **6 users hold `Managing Director`**, so
FR-HR-092's signature on a termination is satisfiable today.

**Question for TDC:** who in Internal Audit should hold `TDC_INTERNAL_AUDIT`, and should the role
be granted to a named individual, a group, or both? This is an administrative grant, not a
development task — but the settlement stage cannot complete without it.

⚠ Note also that the Managing Director role exists under **two** spellings — `Managing Director`
(6 members) and `TDC_MANAGING_DIRECTOR` (0). The HR code authorizes on both, but the duplication
is worth resolving before it causes a grant to land on the empty one.

---

## How is a daily rate worked out for exit pay? (raised 2026-08-20)

The final settlement (FR-HR-184) turns a monthly salary into a daily rate to value **notice pay in
lieu** and **leave encashment**. The system currently uses **monthly salary × 12 ÷ 365** — a
calendar-day basis — and writes that basis in words onto every computed line so the figure can be
checked.

Other bases are common and give different money on identical facts:

| Basis | GHS 6,000/month, 16 days' notice |
|---|---|
| × 12 ÷ 365 (calendar, current) | GHS 3,156.16 |
| ÷ 30 (30-day month) | GHS 3,200.00 |
| ÷ 22 (working days) | GHS 4,363.64 |

**Question for TDC:** which basis does TDC use for notice pay and for leave encashment, and are they
the same for both? It is a one-line change once known, but every settlement computed before the
answer arrives uses the calendar basis.

⚠ Related and more urgent: **202 of 3,883 employees have a salary on record**, and there are **no
leave balances at all**. Until that data exists the settlement cannot value these lines for almost
anybody — it will say so rather than show zero, and require the amount to be entered by hand with
its source named. That is a data-migration dependency, not a development one.

---

## What is the basis for a long-service award? (raised 2026-08-21)

**Requirement:** FR-HR-113 requires the system to report long-service-award eligibility, and TDC's
own *Staff Awards Changes* note asks us to "define the basis for the long service awards". It does
not say what that basis is.

**What was built** (delivered 2026-08-21, area 14 slice 9): the ladder is **data, not code**. Each
award type carries its own list of milestones, one row per number of years, and each row holds what
that rung is worth — a monetary amount, a leave-days bonus and a free-text benefits line. HR
maintains them on the admin screen, so TDC's answer is a data change and not a development request.

A "seed the default ladder" action fills in **10, 15, 20, 25 and 30 years**. ⚠ It seeds **only the
years**. The money, leave days and benefits are left **empty on purpose**: TDC has not said what a
twenty-year award is worth, and a seeded figure would look like an approved one. Until question 2
below is answered, the ladder grants recognition with no value attached to it.

(The tenant-wide `LongServiceMilestoneYears` setting — currently 5/10/15/20/25 — is offered on that
screen as a one-click alternative, but it is not applied automatically: it has a coded default that
cannot be told apart from a value someone actually chose.)

**What we need from TDC:**

1. **Which milestones?** 10/15/20/25/30, or a different ladder (some organisations start at 5, some
   stop at 25, some add 35 and 40).
2. **What does each milestone carry** — an amount, extra leave days, a certificate, a trophy, or a
   combination? The amounts are the part we cannot guess.
3. **What counts as service?** The system measures from `DateEmployed`. If a break in service, a
   secondment, or prior service elsewhere in the group should count differently, say so.

**The same note also states a disqualification rule:** *"any negative records such as disciplinary
action, then you are exempted"*. Two things TDC needs to settle for that to be enforceable:

**What was built.** The rule is a **switch on each award type, off by default.** TDC stated it
under their Long Service heading, so applying it to an Employee of the Month award would be
extending a policy they did not write. HR turns it on for the awards it should govern.

A **negative record** is currently read as *a disciplinary action that reached a decision and was
not dismissed*. A case still in **draft** is one nobody has been formally accused in, and a
**dismissed** case is an exoneration — treating either as a black mark would deny an award over an
allegation that went nowhere. A case **under appeal** does count, because the decision stands until
it is overturned.

**What we still need from TDC:**

- **How far back does a disciplinary record count?** The system defaults to **forever**, because
  that is the note read literally — *"any negative records"*, with no horizon — and a window is a
  softening nobody has asked for. A horizon can be set per award and **relaxes** the rule.
  ⚠ This is the setting most likely to be wrong as written: a warning from two decades ago
  currently disqualifies a thirty-year award. If that is not the intent, name the window.
- **Which outcomes disqualify?** Every case that reached a decision, or only those above a
  threshold — a written warning and a final written warning are not the same thing.
  ⚠ **This one cannot be built until it is answered.** `StaffDisciplinaryActions` has **no severity
  column** — only the authority level required to decide the case — so there is nothing to compare a
  threshold against. If TDC wants severity to matter, the discipline record needs a severity field
  first, and that is a schema change rather than a setting.

⚠ **Two data facts that bear on this now.** Measured on the live database, 2026-08-21:

| | |
|---|---|
| Employees with a `DateEmployed` on record | **2,103 of 5,579 (38%)** |
| Employees with 10+ years' service | **1** |
| Employees with 15, 20 or 25 years | **0** |

The first is the blocker: a long-service report cannot see 62% of staff, because they have no start
date to measure from. ✅ **The report built for FR-HR-113 does not hide this** — employees with no
employment date appear on it with a standing of *Service unknown* and a plain reason, and the totals
report how many there are. That makes the data gap visible to whoever can fix it, but it does not
close it: those employees cannot be assessed for an award until somebody supplies their start date. The second means the feature is being built and tested against fixtures — it
has almost no live subjects to act on, which is expected for a young employee record set but should
not be mistaken for the engine failing. Both are data-migration dependencies, not development ones.

---

## Which awards should accept a nomination from the nominee themselves? (raised 2026-08-21)

**Requirement:** TDC's *Staff Awards Changes* note describes employees nominating each other —
*"employees nominating through staff portal"*, *"if the employees themselves must choose or nominate
people for the best employee award"*, and explicitly *"nominate managers for awards"*. It does not
say whether somebody may put their **own** name forward.

**What the system does:** self-nomination is a **setting on each award type**, and it is **off by
default**. Nobody can nominate themselves unless HR has enabled it for that particular award.

**Why a setting rather than one rule.** In practice this is not a single policy question — it splits
by what the award is measuring:

| Normally **not** self-nominated | Normally **open** to self-nomination |
|---|---|
| Employee of the Month / Year | Innovation awards |
| Values and behaviour awards | Suggestion and improvement schemes |
| Peer recognition | Project or technical achievement awards |
| Leadership awards | Cost-saving submissions |
| Customer service awards | Patents, publications, certifications |
| **Anything decided by a staff vote** | |

The distinction is whether the achievement is something the nominee can evidence themselves — an
innovation, a measurable saving — or a judgement about how they are seen by others, which is not a
claim one can sensibly make about oneself. Long service is a third case: it is computed from service
records with no nomination step at all.

**Why the default is "off".** The awards TDC's note actually describes are the first kind. Defaulting
to "on" would have let every unconfigured award quietly accept self-nominations, including ones
going to a staff vote.

⚠ Worth knowing: the usual objection to open self-nomination is about *participation* rather than
fairness of judgement — it over-represents employees comfortable promoting themselves, which varies
by personality, seniority and culture in ways unrelated to the work. The counter-argument is equally
real: a nomination-only scheme leaves recognition dependent on having an attentive manager, so good
work goes unrecognised when a supervisor simply never submits anything.

**Question for TDC:** which awards in the catalogue should have self-nomination enabled? Our
expectation is the Innovation award and any suggestion scheme, and nothing else — but it is a
setting per award, so any answer is a configuration change rather than a development one.

---

## Who may read HR's interpretation and the investigation report on a grievance? *(raised 2026-08-28)*

**Requirement:** FR-HR-181 requires a grievance to retain, among other things, the **HR
interpretation** and the **investigation report**, and to carry them up the escalation route so that
each level above can read what the levels below concluded.

**What we assumed:** both are visible to **everyone who may read the case at all** — that is, the
employee who raised it, HR, and whoever has been named to answer at a rung. Nobody else can open a
grievance: a manager cannot, and *the person complained about cannot* unless they have been named to
answer it.

**Why we assumed it.** The people named on rungs *are* the escalation route, so withholding HR's
reading of the case from the very person being asked to answer would defeat the requirement. And an
employee reading the findings of an investigation into their own grievance, and HR's position on it,
is ordinary natural justice rather than a disclosure.

**Why it matters, and what we would need to know.** HR's interpretation is a legal-ish position —
what the Conditions of Service and the collective agreement say about the case — written before the
outcome is known. Some organisations treat that as an internal working note and share only the
decision. If TDC's grievance procedure says either of these:

- HR's interpretation is an internal note and is **not** shown to the employee, or
- the investigation report is released only in **summary** to the employee,

then this is a one-line change in each of two places and should be made before go-live. It is a
policy question, not a technical one, and it is the only place in the employee-relations module
where we chose transparency on TDC's behalf.

**Not affected either way:** meeting notes are already restricted to HR and the meeting's chair, and
cross-references to disciplinary cases, safety incidents and improvement plans are already visible to
HR alone.

## How should staff numbers be issued? *(raised 2026-09-01)*

**Where this came from.** Lane 3b set out to make staff numbering configurable. It had been
behaviour rather than configuration: the format `{year}{sequence:D4}` was compiled into
`EmployeeRepository`, and whether HR could type their own number was decided by whether the field
happened to be left blank. Neither was a decision anybody had made.

**What we now know, and it is the reason these questions exist.** TDC's real employee list uses
**two different formats** — permanent staff are numbered as bare digits, contract staff carry a
prefix (of the shape `ABC123`). That is not visible in this database: **1,401 of its 1,507 employee
rows are accumulated test fixtures**, and the 106 that remain are system-generated. So the questions
below cannot be answered by looking at the system, and the answers are being asked for rather than
inferred.

**What has been built to the answer's shape, not to a guess.** `StaffNumberFormat` is a table with
one row per register per tenant — name, prefix, separator, whether the year appears, sequence width,
suffix — so any of these answers is configuration rather than a code change. What it cannot decide
for itself is below.

### 1. What exactly are the two formats?

We have the shape but not the specifics, and the difference matters because it is what keeps the two
registers from ever colliding:

- **Permanent** — how many digits? Is it zero-padded? Does the sequence ever restart, or does it
  climb for ever?
- **Contract** — is the prefix always the same three letters, or does it vary by project, site or
  contracting company? **If it varies, that is a different design**: the prefix stops being a
  property of the register and becomes a property of the engagement, and the rule needs to be
  selected by something other than employment type.

### 2. Does a contract employee who becomes permanent get a new number?

⚠ **This is the load-bearing question.** Both answers are defensible and they build differently:

- **They keep it.** Then a staff number records *which register somebody entered by*, not what they
  are today — and no report may infer employment type from the shape of the number. This is what is
  built today, because renumbering would orphan the ~750 references to `EmployeeNumber` across HR,
  Payroll, Maintenance and Appraisal.
- **They are renumbered.** Then the system needs a "previously known as" trail, and every module
  holding a staff number needs to follow the change. That is a materially larger feature and it is
  not built.

### 3. Are there more than two registers?

Casual or site labour, expatriates, seconded staff, apprentices — do any of these carry their own
numbering? The table supports as many as TDC has; we only need to know which exist.

### 4. When a manual number is typed, should the system check its shape?

> **Partly settled 2026-09-01.** The *import* half of this question is answered by design rather
> than by a setting: a data load goes through its own path that accepts numbers exactly as given
> **and advances the register's counter past the highest it loaded**. There is no mode to toggle
> before an import and none to remember to toggle back — a mode that must be restored is a mode that
> gets left wrong. What remains open is only the question below, about numbers typed by hand during
> ordinary day-to-day entry.

If contract numbers are `ABC123` and HR types `XYZ999`, should that be refused?

- **Refuse** — the format is a rule, and a number outside it is an error.
- **Accept** — manual entry exists precisely because some numbers come from elsewhere (a payroll
  bureau, a legacy register) and will not match anything we describe.

Accept is the safer default and is what we would build absent an answer, but it means the recorded
format is documentation rather than enforcement.

### 5. ⚠ What should the counter start from? *(a migration question, not a preference)*

**This one will bite on the day TDC's real staff are loaded.** The system's counter starts at 1
unless told otherwise, so the first new permanent hire after go-live would be issued **00001** while
TDC's existing staff are already numbered in the ten-thousands. Every register's counter must be
**seeded from the highest number already in use** as part of the data load.

So: for each register, what is the highest number currently issued? And should the counter continue
from there, or start at a deliberate round number so that system-issued numbers are visibly distinct
from historical ones?

---

## The leave module's eight open decisions *(raised 2026-09-17)*

These arise from closing out leave management. The full reasoning behind each is in
`docs/HR/areas/leave/HR-LEAVE-CLOSURE-PLAN.md` § 4; this is the list TDC has to answer.

**Six of them are already built to a recommendation**, because leaving the module half-finished
while waiting is worse than building the defensible reading and changing it if TDC disagrees. Each
row says what is running today. **Two — L-D7 and L-D8 — are not built and should not be until TDC
answers, because both readings are live in the product at once.**

### L-D1 · Should a leave REQUEST support send-back-with-suggested-dates?

TDC described *"sending back for correction with suggested dates"*. That conversation exists today
on a leave **plan** (the annual intention) but not on a **request** (the actual application):
`LeaveRequest` has no suggested-dates fields and no `respond-suggestion` endpoint.

**Built to:** yes, on the request, mirroring the plan exactly. The plan is annual and optional; the
request is where real dates are argued about. If TDC only ever meant the plan, the request-side
fields become unused rather than wrong.

### L-D2 · Should a leave balance be held per sub-type?

Today all Sick sub-types share one pot, because `LeaveBalance` is keyed on (employee, leave type,
year) with no sub-type. The sub-type's `MaxDaysAllowed` cap therefore never reaches a balance.

**Built to:** one pot per leave type, with the sub-type cap enforced **per request** instead.
Per-sub-type balances would multiply every row on the Balances screen and every year-end run, for an
outcome the per-request cap already delivers. **This is a policy call and TDC may prefer the other.**

### L-D3 · Is HR a second approval step after the supervisor?

TDC described *"supervisor reviewing for approval … hr getting the final leave dates"*. The seeded
workflow is **one** step where Manager **or** HR may approve, and one approval is enough. So
"supervisor approves, then HR confirms" is not what is configured.

**Built to:** a two-stage definition — stage 1 line manager, stage 2 HR, with initiator-approval
prevented. This is configuration rather than code and is easy to reverse.

### L-D4 · What does "confirming the upcoming leave will be observed" mean?

An approved request goes quiet between approval and its start date. Nobody is asked whether the
person is still going, and nothing records the answer.

**Built to:** both a reminder N days out **and** an explicit confirm/defer action on the request, so
the answer is recorded rather than assumed. If TDC only wants the reminder, the action is harmless.

### L-D5 · Who may shift an approved leave date, and does it need re-approval?

TDC asked for *"possible shifting of the leave to a different day even after the planning is done"*.
Today the only route is cancel-and-re-key, which loses the number, the approval and the history.

**Built to:** HR may amend; the employee may request an amendment; **any change to the dates
re-opens the approval.** Anything else would let an approval mean something it did not.

### L-D6 · What should an unpaid leave type actually do? *(needs the payroll owner, not TDC)*

`LeaveType.IsPaid` is display-only: unpaid leave produces no deduction anywhere. HR records the
leave and the days; what that is worth is payroll's. Raised separately in
`docs/HR/integration/handoffs/HANDOFF-PAYROLL-LEAVE.md`. **Not built, and should not be built inside HR.**

### L-D7 · ⚠ One daily-rate basis, or two? *(NOT built — needs an answer)*

Two formulas are live in one product. **Leave encashment** uses `(basic + linked allowances) ÷ 22`;
the **separation settlement** uses `monthly × 12 ÷ 365`. On GHS 6,000/month those differ by about
**38%**. This is the same question already raised above under *"How is a daily rate worked out for
exit pay?"* — it is repeated here because leave is the second module now depending on the answer.

Whichever basis TDC names, both should use it.

### L-D8 · ⚠ Is in-service leave encashment permitted at all? *(NOT built — a requirements conflict)*

**FR-HR-046 says leave is encashed *"only on exit, no other route"*.** The leave module nevertheless
ships an in-service encashment path, with annual leave flagged `AllowCashConversion` in the seed —
so the product currently implements both readings at once.

- **If FR-HR-046 stands:** `AllowCashConversion` should be **off on every seeded leave type** and the
  employee-facing encashment screen gated behind separation.
- **If in-service encashment is intended:** FR-HR-046's wording needs amending.

**Either is a small change. Shipping both is not an option, and this must be resolved before
sign-off.**

### L-D9 · The five leave reminder windows are our numbers, not TDC's

The leave reminder engine (built 2026-09-17) sweeps nightly and chases five things. Each threshold
is a working assumption, running in code today, and each is a one-line change:

| What it chases | Window we chose | Why that number |
|---|---|---|
| Approved leave about to start, with nobody confirming it is still going | **7 days** | long enough to brief a reliever; short enough that the reminder is about *this* leave |
| Leave that ended and was never closed | **2 days'** grace | a day either side of a weekend should not raise a chase |
| A request nobody has decided | **5 days** from the request date | chased on the REQUEST date, not the start date, so a request filed months ahead and ignored is still caught |
| Mandatory leave still outstanding | from **month 9** | lands with a quarter of the year left to actually take it |
| Carry-over about to lapse | **30 days** | one pay cycle's notice |

There is also a **90-day backlog floor**: the first sweep on an established database ignores
anything older, so go-live does not queue years of history. (Area 9's first live run queued 275
reminders, 242 of which were history.)

**None of these is load-bearing** — nothing breaks if TDC prefers different numbers, and nothing
needs re-testing beyond the harness. They are listed so they are confirmed rather than discovered.

### L-D10 · Excuse duty and the medical board — the rules, before anything is built

Stakeholders raised **excuse duty** and a **medical board recommendation** as requirements for sick
leave. **Nothing of the kind exists in the system today** — sick leave is an ordinary leave type
with optional, untyped attachments and no certification rule at all.

Before it can be built, TDC has to answer four things. The parenthesised values are common
Ghanaian practice and are **our guesses, not a proposal**:

1. **How many days may an employee self-certify** before a certificate is required? *(commonly
   2–3)*
2. **What counts as valid excuse duty** — any registered facility, or a named panel of providers?
   Does the certificate have to name a diagnosis, or only a period? *(The second is usually
   preferable: HR needs to know she is excused and for how long, not what is wrong with her.)*
3. **At what cumulative sick leave does a Medical Board become required**, and is that counted per
   year or per episode? Is the Board a standing committee, an ad-hoc panel, or an external referral?
4. **What outcomes may the Board recommend**, and what does each one do to the employment record?
   The usual set is *fit to resume · fit with restrictions · extend sick leave · retire on medical
   grounds* — and the last of those is a **separation**, which makes this a cross-module rule rather
   than a leave setting.

**Why this needs answering rather than assuming.** A certification threshold set too low buries HR
in paperwork for one-day absences; set too high it stops being a control. And a medical board is a
statutory process in parts of Ghanaian employment — if TDC is bound by a specific instrument, the
thresholds are not ours to choose at all.

⚠ **One design point worth confirming with the answer.** Our recommendation is that the evidence is
a **typed** attachment (*excuse duty* / *board recommendation*) rather than a general file, so the
system can refuse a submission that is missing the *right* document rather than merely missing *a*
document. That is the difference between a control and a filing cabinet.

Recorded in `docs/HR/areas/leave/HR-LEAVE-CLOSURE-PLAN.md` § 3.3b as **R-15**.

---
