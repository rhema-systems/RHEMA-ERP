# HR — Configuration Register

**Started 2026-09-17** (leave residue plan, slices G2/G3), extended 2026-09-18 (G6), extended again
2026-09-18 (entitlement plan W1c), and 2026-09-23 (round 4 lane K-a — § 2.5; lane K-b1 — § 2.6;
lane N — § 2.7, the 44 letter and email templates; lane N-b2 — § 2.8, company-schedule reminders;
lane O — § 2.9, the technician-role flag and the person's exception).

**Surveyed: all of `CompanyHrPolicySettings` (46 + 1 added 2026-09-18 + 4 added 2026-09-23, all four enforced — § 2.5), all of `LeaveType` (26 + 2, and `AllowOffsetAgainstAnnual` added 2026-09-26), and
all four of leave's CHILD tables (38)** — plus, not as a full survey, the three `OrientationProgram` notice and
certificate switches lane K-b1 made real (§ 2.6: two were ghosts). Three ghosts found, all in the first — **and one setting that is none of the
four statuses**, which is why there are now five. The per-module settings for attendance, travel,
appraisal, company schedule and the rest are still to do — see § 4, which now says what each one is
expected to cost.

### ⚠ Corrected 2026-09-25 — every leave-type setting audited (round 5)

The 2026-09-18 surveys counted **references**. This audit read what each consumer actually **does**,
and it found settings this register had marked Enforced or clean that are not. **§ 3.2's "zero
ghosts" is wrong.** Until the round 5 lanes land, this table overrides the rows below it. Evidence:
the round 5 plan's *What exploration found* (repo copy to come:
`docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-5-LEAVE-PLAN.md`); plain terms:
`docs/HR/areas/leave/HR-LEAVE-ROUND-5-WHAT-CHANGES.md` § 15.

| Setting | This register said | Actually | Round 5 fix |
|---|---|---|---|
| `LeaveType.IsPaid` | clean (§ 3.2) | **Ghost** in HR — display only; what unpaid leave deducts is payroll's (L-D6) | N1: **done** — relabelled as a label for readers; payroll decides the deduction |
| `EncashmentWorkingDaysPerMonth` (tenant) | Enforced (§ 1) | **Unreachable** — the leave type's own divisor always wins, and the form's minimum is 1 (default 22). Slice 6 proved it only by POSTing a type divisor of 0, which no form can send | N1: **done** — off the policy page; the column stays |
| `LeaveAccrualPolicy.IsActive` | enforced (§ 3.2b) | **Unreachable** — no DTO field, mapping or update writes it; always true | N1: **done** — the *In force* switch; the one-in-force rule follows it |
| `LeaveAccrualPolicy.ProRateOnJoin` | Unreachable (§ 0, § 3.2b) | **Enforced** — fixed by entitlement plan B1; both positions work | — |
| `MaxDaysPerYear` | clean | **Misleading** — only ever lowers the entitlement. Default 0 with Max 90 means the type can never be booked (UNPAID and INJ on the demo) | N2, N4: **done** — binds on annual leave only, as the highest allocation allowed; UNPAID 90 and INJ 180 |
| `LeaveAccrualPolicy.Frequency` | enforced | **Misleading** — every frequency falls one period short within the year (monthly 11/12); incremental `Annual` accrues 0 all year; `PerPayPeriod` behaves as Monthly | N2: **done** for incremental *Annual* (refused anew; *PerPayPeriod* already was). C1: **done 2026-09-25** — a period counts on its last day, so monthly, quarterly and half-yearly reach the whole entitlement on the year's last day; asserted at exact figures on both days of every boundary (`run-round5-c.mjs` [1]–[3]) |
| `CarryOverExpiryMonths` | clean | **Misleading** — also wipes carried days already used; sweep 5's skip rule assumes it does not | G: **done 2026-09-25** — the expiry keeps the carried days taken before the deadline and removes the rest; sweep 5 warns about exactly those; carry-over leaves lapsed days behind. Executed for real in both positions of the deadline (`run-round5-g.mjs` [4], [5]) |
| `MaxCarryOverDays`, `YearEndBasis` | Enforced | Proved by previews only (slice 12) | G: **executed for real** 2026-09-25 — the cap raised and lowered with the carried figure following it, and both bases carrying different days (`run-round5-g.mjs` [2], [3]) |
| `ForfeitUnusedAfterMonths` | clean | **Misleading** — a closed year cannot be booked, so it affects no leave-taking; it only closes a window for cashing that year's leftover in | N2: **done** — help text says what it does; off for TDC (B7), UAT and seeder |
| `MandatoryAnnualLeave` | clean | Advisory (compliance list + sweep 4). Its doc comment's "used by the forfeiture routine" is false | A: **done 2026-09-25** — retired into the Annual kind (`Category`, § 3) and the column dropped |
| `LeaveTypeEligibility` rules, `Gender` | enforced; "`Gender` ANDs onto an org-scoped rule" | Rules are **OR'd**, under a tab that says "restrict"; the gender qualifier on an organisation rule **cannot be set from the tab** | N2: **done** — the tab says rules are OR'd, and a unit, level or position rule takes a gender |
| `HasSubTypes` | clean | Only blocks creating a sub-type; hides nothing | N2: **done** — derived from active sub-types; the checkbox is gone |
| `LeaveSubType.MaxDaysAllowed` | enforced | Skipped when a draft is edited; **also replaces the type's whole entitlement** for a request carrying the sub-type | N2, N3: **done** — limits the sub-type inside its type's pot; a draft edit checks it |
| `LeaveSubType.IsActive` | "refused by the service" | Create ignores it; editing a draft validates no sub-type | N3: **done** — create honours it; a draft edit checks it |
| `LeaveCategoryAllocation.LeaveSubTypeId` | enforced | Almost never applies — balances resolve at type level | N2: **done** — always the whole type; the picker is gone |
| `LeaveAccrualPolicy.ProRateOnExit` | enforced | Binds in the engine only; no payout reads it | L2 |
| `MinDaysNotice`, `RequiresReliever` | clean | **Bypassable** — skipped for drafts and never re-checked at submit | N3: **done** — submit re-runs both |
| `MedicalBoardThresholdDays` | Enforced (§ 3) | **Bypassable** — Pending requests are not counted; reschedule, suggested dates and the counter-proposal skip the gate | N3: **done** — Pending counts; a lengthening move re-runs the gate |
| `ProRateFirstYearEntitlement` | Enforced (§ 3) | Correct only for a January leave year (counts calendar months to December) | C4: **done 2026-09-25** — months of the leave year; an April-start joiner hired in February gets 2/12, both start months asserted (`run-round5-c.mjs` [9]) |
| `LeaveYearStartMonth` | Enforced (§ 1.5) | Two readers ignore it: first-year pro-rating and sweep 4's month | C4: **done 2026-09-25** — both follow it, and eleven controller defaults (a call naming no year) now give the current leave year, not the calendar year; `GET api/Leaves/leave-year` lets screens open on it |
| The five reminder windows | Enforced (§ 2) | The windows bind, but every reminder goes to the **HR role only**, in-app — the entity comments naming the employee, manager or approver are false; sweep 4 compares the calendar month | C4: **done** for sweep 4's month — `MandatoryLeaveChaseFromMonth` is a month **of the leave year**, asserted in both positions under a July start. I: **done 2026-09-26** — each reminder reaches the people who can act on it, in the app and by email, and HR when nobody else can be told; the entity comments now say who (§ 2, `run-round5-i.mjs`) |
| `AllowCashConversion` and the four encashment-rate settings | Enforced | Enforced **in service only**. The exit settlement ignores all of them — it sums every type and year (`SeparationService.cs:2234-2249`) — so with `AllowInServiceEncashment` off (the default) none can fire | L2 |
| The exit settlement's **56-day cap** on leave paid out | not listed | **A constant in code** (`SeparationService.cs:2232`, FR-HR-152): enforced, but visible on no screen and changeable only by a release | L2b (decided 2026-09-25): a company setting, default 56, empty = no cap, asserted in both positions |

---

## 0. Why this exists, and why it is not the open-questions document

`HR-OPEN-QUESTIONS-FOR-TDC.md` asks **one client** to settle things. That framing is wrong for what
is being built. TDC is the first client of this application, not its only one — so a value waiting on
one client's ruling is not *blocked*, it is **unconfigured**, and the answer is a default a client
can change rather than an email.

This register is the other half of that turn. It records, for every value HR assumes:

- **where it lives**, so nobody re-hardcodes it;
- **its default**, and whether that default is a real choice or a placeholder;
- **whether it is enforced**, which is the only column that matters.

### ⚠ The rule this register exists to police

> **A setting that saves and is read by nothing is worse than a hardcoded constant.**

A constant is honest — nobody believes it is configurable. A dead setting invites somebody to set it,
believe it binds, and discover months later that it never did. This is not hypothetical:

| Evidence | Where |
|---|---|
| **14 of 50 appraisal settings do not enforce what they claim** — 9 advisory, 2 client-side only, 3 read by nothing at all | `HR-APPRAISAL-SETTINGS-AUDIT.md` |
| **11 ghost leave settings** — configurable, saved, read by nothing | leave guide § 4.4 |
| Travel's policy rule register **ships deliberately read-only**, with the argument recorded on the component: an editable control that does nothing creates false assurance | `PolicyRulesPanel` |
| A room's `MaxBookingDurationHours` and `AdvanceBookingDays` are stored and read by nothing | company schedule C-4 |

**So the Status column is the point of the table, not decoration.**

### Status vocabulary

| Status | Means |
|---|---|
| **Enforced** | Changing it changes behaviour, and a harness asserts that in **both** positions |
| **Advisory** | Read and displayed, but nothing refuses on it |
| **Client-side** | The UI honours it; the API does not, so any other caller ignores it |
| **Ghost** | Saved and read by nothing. ⚠ Either wire it or delete it — leaving it is the worst option |
| **Unreachable** | **Read, in a branch that cannot be entered.** ⚠ Added 2026-09-18 — see below |

⚠ **"Enforced" requires the two-position test.** Asserting a setting at its default proves nothing:
a hardcoded value passes that test perfectly. The assertion must change the value, observe the
behaviour change, and change it back.

⚠ **And the two positions must produce different OBSERVABLE OUTCOMES, with both figures stated.**
Not *"the number changed"* — `14.00` and then `15.75`. A test that sets a value both ways and checks
only that the code read it is green in both positions and proves nothing. That is precisely how an
**Unreachable** setting passes for Enforced.

### ⚠ Why **Unreachable** had to be added, and why a tool will never find one

`LeaveAccrualPolicy.ProRateOnJoin` is read by `LeaveEntitlementService` on every accrual
calculation. The survey tool reports it as **referenced**, with the same count and the same
consuming file as `ProRateOnExit` beside it — which genuinely works. They are indistinguishable to
anything mechanical.

The guard it sits in cannot be satisfied:

```
eligibilityDate = hireDate + (MinServiceMonths ?? 0)     // ≥ hireDate, always
accrualStart    = Max(yearStart, eligibilityDate)        // ≥ eligibilityDate, always
if (ProRateOnJoin && hireDate > accrualStart && ...)     // ⚠ hireDate > something ≥ hireDate
```

So the switch changes nothing **in either direction**. The pro-rating it claims to control happens
anyway, because the eligibility date already anchors the accrual window to the hire date — which
means an organisation that wants it OFF cannot have it off.

**A Ghost is honest by comparison:** nothing reads it, and a tool says so. An Unreachable setting
reads, surveys clean, and lies. **Only a human read finds one**, which is the argument for reading
the survivors rather than counting them.

---

## 1. Leave — encashment `CompanyHrPolicySettings`

| Setting | Default | Status | Enforced where | Proof |
|---|---|---|---|---|
| `AllowInServiceEncashment` | **`false`** | **Enforced** | `LeaveEncashmentService.RequestEncashmentAsync` — asked **before** the leave type, so the refusal names the real reason | slice 6 [1] — refused off, accepted on, same request |
| `EncashmentWorkingDaysPerMonth` | `22` | **Unreachable** *(corrected 2026-09-25 — see the top)* | `EmolumentService.GetEncashmentDailyRateAsync` — only when the leave type's own divisor is 0, which the form cannot send | slice 6 [2] — 6,600 ÷ 22 vs ÷ 30, exact figures, **via an API-only type divisor of 0** |

**⚠ `AllowInServiceEncashment` settles a requirements conflict, not a preference.** FR-HR-046 says
leave is encashed *"only on exit, no other route"*, and the module ships an in-service path with
annual leave flagged convertible in the seed. **Both readings were live in the product at once.** The
default is the FRD's reading, so a new tenant starts compliant; a client whose policy differs turns
it on deliberately rather than inheriting it by accident.

**⚠ The seeded demo tenant has it `true`**, deliberately unlike the code default — the encashment
screen has already been demonstrated to stakeholders, and defaulting it off without that seed line
would make a shown feature vanish.

### The two daily-rate bases — and why they are NOT merged

| | Divisor setting | Formula |
|---|---|---|
| Leave encashment | `EncashmentWorkingDaysPerMonth` = 22 | `(basic + linked allowances) ÷ 22` |
| Final settlement | `SettlementDaysPerYear` = 365 | `monthly × 12 ÷ 365` |

At the defaults these are **~38% apart on the same salary**. That was logged as open question L-D7
and recorded as *"two hardcoded formulas awaiting TDC"*. **⚠ That record was wrong** — both sides
were already configurable. What was actually defective was narrower:

1. they sat at **different scopes on different screens**, so no client could see they disagreed;
2. leave **did not stamp its basis** on the payout, though the settlement had since FR-HR-184;
3. the fallback divisor was a `private const` in `EmolumentService`.

All three are fixed. They remain separate because **encashing unused days while employed is not the
same money event as a final settlement on exit**, and clients will legitimately want different bases.
Forcing one would be wrong for a product. Instead both appear on one settings screen with a live
worked example naming the spread, and every payout records the basis that produced it.

**Precedence:** `LeaveType.EncashmentWorkingDaysPerMonth` (if > 0) → tenant setting → `22`. The
stored `LeaveEncashment.RateBasis` names *which* of the two set it. Proved in slice 6 [3].

⚠ **The allowance links that feed the derived rate are money too, and they were losable.** A
leave-type PUT that omitted `allowanceComponentIds` deleted every link (L-13), and removal soft
deleted against a unique index that is not filtered on `IsDeleted`, so a removed allowance could
never be restored. Both fixed in G5a and asserted in slice 9. Recorded here because the register
is about what decides money, and these rows do — they are just not spelled as a setting.

---

## 1.5 Leave — the leave year `CompanyHrPolicySettings`

**Added 2026-09-18** (entitlement plan C1). The last assumption in the module to become a setting.

| Setting | Default | Status | Proof — slice 13, both positions |
|---|---|---|---|
| `LeaveYearStartMonth` | **1** (January) | **Enforced** | a plan dated **10 February** files under leave year **2025** with an April start and **2026** with January. The same date, two answers |

⚠ **The two-position test is the ONLY thing that could have proved this one.** Thirty-eight sites
were routed through a `LeaveYear` helper while it still answered "January", and the whole suite
staying green proved the refactor changed nothing — it could not prove the sites would follow a
different answer. Slice 13 moves the boundary and asks. A survey tool would have called the setting
*referenced* from the moment the helper read it, which is exactly the **Unreachable** trap in § 0
wearing different clothes.

⚠ **Change-once-at-setup, and the refusal is enforced** (decision D-9): once the tenant holds any
leave request or balance, the save is refused and names what exists. Of the figures a moved boundary
disturbs, only `CarriedOverDays` cannot be re-derived — it was computed by a year-end run against
boundaries that would no longer exist.

⚠ **It is NOT `FiscalYearStartMonth`.** Separate settings on purpose: plenty of organisations run
the finance year and the leave year apart, and the coupling would be invisible until one of them
did.

## 2. Leave — reminder cadence `CompanyHrPolicySettings`

All five were `private const` in `LeaveReminderService`, documented there as *"the working
assumption, not TDC's number"* — honest, and still wrong: the cadence at which a system nags people
is exactly what one client wants weekly and another fortnightly, and changing it cost a release.

| Setting | Default | Status | Proof — all in slice 6, both positions |
|---|---|---|---|
| `LeaveStartingReminderDays` | `7` | **Enforced** | [4] leave 10 days out: invisible at a 3-day window, visible at 20 |
| `LeaveUndecidedChaseDays` | `5` | **Enforced** | [4] a request filed today: unchased at 90 days, chased at 0 |
| `LeaveClosureGraceDays` | `2` | **Enforced** | [5] leave ending today: unchased at 30 days' grace, chased at 0 |
| `MandatoryLeaveChaseFromMonth` | `9` | **Enforced** | [6] an outstanding mandatory balance: raised chasing from month 1, silent chasing from next month |
| `LeaveCarryOverExpiryReminderDays` | `30` | **Enforced** | [7] 5 carried days lapsing 31 Dec: silent at a window short of it, warned at one that reaches it |

**Who each one reaches** *(round 5, lane I, 2026-09-26)* — the entity's comments say the same, and
the settings card lists it. Every reminder goes in the app **and by email**, through its own
notification topic per audience (`LeaveReminder.{Kind}.{Audience}`), and anybody who cannot be told
directly is passed to HR with the reason:

| Setting | Chases | Told |
|---|---|---|
| `LeaveStartingReminderDays` | approved leave about to start, until somebody says it is still going | the employee |
| `LeaveClosureGraceDays` | leave ended and not closed | the line manager once the return is reported (supervisor, else the nearest head of unit); HR before |
| `LeaveUndecidedChaseDays` | a request waiting | whoever its current approval step is asking, never the requester; the employee when sent back with other dates |
| `MandatoryLeaveChaseFromMonth` | annual leave not yet planned or taken — everybody serving past the qualifying period, with or without a record, once a leave year | the employee; the supervisor, one message naming their people; HR, one summary per run |
| `LeaveCarryOverExpiryReminderDays` | carried days about to lapse | the employee |

Proved in both positions by `dev-harness/hr-leave/run-round5-i.mjs`. The chase's kind is now
`AnnualLeaveOutstanding` (was `MandatoryLeaveOutstanding`); slice 6 and lane C's suite look for the
new name — with the old one, slice 6's *"silent from next month"* half would pass for ever.

**All five now carry the two-position test.** For a time the last three were marked *Enforced
(wiring)* — read from the same `policy` object in the same method as the two that were proved, but
with no fixture of their own, so the sweep produced nothing for them and flipping the setting could
not be observed either way. That is a weaker claim and the register said so. It has since been
closed, because *"reads the setting"* is not *"uses it correctly"*: each of those three had a way to
be wrong that reads perfectly fine — a sign (`today.AddDays(-grace)`), an off-by-one (`>=` vs `>`),
and an **inverted skip condition** (`continue` when *outside* the window, the opposite shape to the
other four).

The fixtures they needed:

| Setting | What the sweep required |
|---|---|
| `LeaveClosureGraceDays` | leave whose end date has passed and is still open — made by **recalling** leave that started today, since creating a backdated request is refused |
| `MandatoryLeaveChaseFromMonth` | a balance on the tenant's **Annual** type with days outstanding — **planted directly**, as a year's accrual is not what the assertion is about. Since round 5 lane A it is planted on the tenant's own Annual type for the one fixture employee and removed by id; before, it was a `MandatoryAnnualLeave` type of the slice's own. Since lane I the chase reads everybody past the qualifying period, record or none, so the planted row fixes the figure rather than making the employee a candidate |
| `LeaveCarryOverExpiryReminderDays` | a balance with carried days on a type with an expiry month — planted, with `CarryOverExpiryMonths = 12` so the lapse date is 31 December and the test works from any day of the year |

⚠ **One half of the mandatory assertion cannot run in December** — it needs a month the year has not
reached. The slice skips it with a printed note rather than quietly inverting; a test that changes
meaning with the calendar is worse than one that admits it cannot run today.

**Not moved, deliberately:** the engine's 90-day backlog horizon stays a constant. It stops the first
run on an established database queueing years of history at once (area 9 queued 275 items, 242 of
them history). It protects the system from itself; it is not a policy anybody should be choosing.

---

## 2.5 Orientation & onboarding — reminder windows `CompanyHrPolicySettings` — added 2026-09-23

Round 4, lane K-a. Read by `OnboardingOrientationReminderService` — **the first HR sweep that
delivers** (one in-app notification per person per run, and the same by email) rather than only
logging. Born as settings, not constants, and each proved in both positions before being marked
enforced. Migration `AddOnboardingOrientationReminders` adds them `NOT NULL` with their real
`DEFAULT`s, so every tenant's row — not only the seeded one — starts at the value below.

| Setting | Default | Status | Proof — `hr-orientation/run-round4-k.mjs` block B, both positions |
|---|---|---|---|
| `OnboardingTaskDueLeadDays` | `3` | **Enforced** | [B1] a task due in 5 days: silent at 3, reminded at 10 |
| `OrientationDueLeadDays` | `7` | **Enforced** | [B2] an orientation due in 3 days: silent at 2, reminded at 7 |
| `OrientationCertificateExpiryLeadDays` | `30` | **Enforced** | [B3] a certificate expiring in 10 days: silent at 5, warned at 30 |
| `OrientationChaseAfterDays` | `3` | **Enforced** | [B4] an assessment unattempted, and a task awaiting sign-off, both waiting 5 days: neither chased at 10, both chased at 3 |

**Not a setting, deliberately:** the 90-day backlog horizon for overdue items, for the reason given
under § 2 — measured here as well: the demo database held 6,583 open onboarding tasks more than 90
days overdue on the day the engine was built.

---

## 2.6 Orientation — per programme: notices and certificates `OrientationProgram` — added 2026-09-23

Round 4, lane K-b1. Two switches on the programme form that said what they did and did nothing —
**both ghosts until this lane**, found by its survey before anything was built. The first is also a
defect in lane K-a: the daily sweep shipped reminding people on programmes where "Send reminders"
was off.

| Setting (the form's label) | Default | Status | Proof — `hr-orientation/run-round4-kb.mjs`, both positions |
|---|---|---|---|
| `EnableReminders` ("Send reminders") | on for a new programme | **Enforced** — was a ghost | [A13/A15] the same audience rule on two programmes enrols both pairs, tells them only where it is on; [F1] an HR enrolment: told on one, not the other; [F2] the daily sweep: due-soon reminded on one, silent on the other. Governs what its description says — enrolment notices and the sweep's reminders; session changes, completions and certificates are sent either way |
| `IsCertificateIssued` ("Issues a certificate") | off | **Enforced** — was a ghost | [C1–C3] a certificated programme issues its certificate at the moment of completion; [C9] an uncertificated one issues none; [D3] and HR's Issue certificate is refused for it. Before this nothing issued one, and HR's endpoint (which no screen called) never asked |
| `CertificateValidityMonths` | none — never expires | **Enforced** | [C3] 12 months, and [D8] 24 months, become the certificate's expiry. HR's issue path already read it; the completion path does too |

---

## 2.7 HR letters and emails — the wording of every template `EmailTemplate` — added 2026-09-23

Round 4, lane N. Every email and printed document the HR modules produce is rendered from a
template whose shipped wording is declared in one of **eight catalogues** (`IEmailEventCatalog`) —
**44 templates**. Since this lane a tenant can reword any of them at **Administration → HR Settings →
Letter & Email Templates** (`api/hr/letter-templates`: reading on HR Company Read, saving, resetting
and a test send on Write, which the HR role holds). The shipped wording stays in code; a tenant's own
is a row in `EmailTemplates`, written only when HR saves one and set aside by **Reset**.

⚠ **This table is checked against the screen in both directions** by
`hr-templates/run-lane-n.mjs` [A6–A8]. A template added to a catalogue and not entered here fails that
suite, and so does a row here for a template that no longer exists.

**Not on the screen, and not configurable:** the in-app notifications — the orientation notices'
headline and message, every `NotificationTemplate` — are written in code. The screen says so.

### Whose wording goes out — one resolver, three roads

Every production render goes through `TemplatedEmailService`. The survey on 2026-09-23 found 21 call
sites, and none bypasses it. The service uses **the sending tenant's own row** if that tenant has
chosen one, and the shipped wording otherwise. The roads differ only in how the sender's tenant is
known.

| Road | The tenant comes from | Templates | Proof in both positions (`run-lane-n.mjs`) |
|---|---|---|---|
| **Signed in**: HR, an employee or a candidate, inside a request | the caller's own token | 18 emails and 12 documents | [E1–E4] a letter HR previews: shipped wording, then HR's, then shipped again after a reset. [K2–K3] a candidate's application email arrives in HR's wording |
| **Background**: a job with nobody signed in | the sender names it (`SendForTenantAsync`) | the 13 orientation and onboarding emails, sent through the outbox and the daily digest | [F4] shipped. [H5–H7] HR's. [I3] shipped after a reset. [I6] HR's again after a revive |
| **Anonymous**: a careers registration, or a candidate answering an offer by its emailed link | the sender names it: the registration's tenant, or the offer's | `CandidateAccountActivation`; `OfferAccepted` when the acceptance comes by the link (lane N-b1) | [J2–J3b] shipped. [J6–J7] HR's. [J8] shipped after a reset. `run-lane-nb1.mjs` [F2–F3]: an acceptance by the link arrives in the tenant's own wording |

⚠ **"Enforced" below is proved per road, not per template.** The resolver is one method, every call
site reaches it, and the harness changes one template on each road and sees the other wording
arrive. A caller that stopped using `ITemplatedEmailService` would silently stop being Enforced, so
the 21-call-site count is the thing to re-survey.

⚠ **Raw placement is a catalogue fact, not a save-time guess.** `{{{Token}}}` is not escaped, so a
save accepts it only for tokens marked `IsHtml`: the ready-made HTML the system builds (tables and
lists). Twelve tokens across six templates carry the mark; the table below names them. The first
cut of the rule allowed raw placement only where the shipped default already had it. That refused
HR the offer letter's `ConditionsList` and the score sheet's `PanelTable`, both supplied as HTML on
every render, and the escaped form would have printed their markup as text.

| Template | Name | Kind | Goes out, or is rendered, when | Road | Status |
|---|---|---|---|---|---|
| `Recruitment/CandidateAccountActivation` | Careers Account Activation | email | a candidate registers on the careers site, or asks for the link again | anonymous | **Enforced**. It now carries the tenant's legal name: the controller had overridden the name with the tenant record's label, so the email read "Activate your Default Tenant careers account" |
| `Recruitment/ApplicationReceived` | Application Received | email | a candidate submits an application on the careers portal | signed in | **Enforced**. ⚠ Until lane N it greeted the candidate by their email address, because the portal passed the address as `CandidateName` [K3]. The older external-apply path that also sends it has no caller |
| `Recruitment/ApplicationWithdrawn` | Application Withdrawn | email | an application is withdrawn: by HR on the candidate's behalf, by an employee applicant through their own door, or by the candidate on the careers portal | signed in | **Enforced**. It was **Unreachable** until lane N-b1: its only sender was a token-link withdrawal nothing called, and the three live withdrawals sent nothing (`run-lane-nb1.mjs` [D1–D6]) |
| `Recruitment/ApplicationUnderReview` | Application Under Review | email | **once per application**: the first time a person moves it into a review stage (application review, screening, hiring-manager review), through either stage-move door. A submission placed automatically in the pipeline's first stage does not count | signed in | **Enforced**. Until lane N-b1 the older door sent it after **every** move, into an interview or beside Assessment Pending, and the board's door never sent it (`run-lane-nb1.mjs` [B1–C3]) |
| `Recruitment/ApplicationShortlisted` | Application Shortlisted | email | HR sends the shortlist notifications | signed in | **Enforced** |
| `Recruitment/ApplicationRejected` | Application Unsuccessful | email | HR sends the rejection notifications | signed in | **Enforced** |
| `Recruitment/AssessmentPending` | Assessment Invitation | email | an application moves into an assessment stage | signed in | **Enforced** |
| `Recruitment/InterviewInvitation` | Interview Invitation | email + calendar file | HR sends the interview invitations | signed in | **Enforced** |
| `Recruitment/InterviewRescheduled` | Interview Rescheduled | email + calendar file | HR moves an interview | signed in | **Enforced** |
| `Recruitment/InterviewPanelistAssignment` | Interview Panel Assignment | email + calendar file | HR notifies the panel | signed in | **Enforced** |
| `Recruitment/OfferIssued` | Offer Issued | email, with the offer letter attached as a PDF | HR issues an offer | signed in | **Enforced**. Since lane N-b1 it carries the offer letter as a PDF, and `{{#if LetterAttached}}` says so only when the attachment really went. A letter that cannot be converted costs the attachment, never the email (`run-lane-nb1.mjs` [E1–E8]) |
| `Recruitment/OfferAccepted` | Offer Accepted | email | a candidate's acceptance, however it arrives: HR recording it, the candidate's emailed link, or the careers portal. A decline sends nothing | signed in, or anonymous by the link | **Enforced**. Until lane N-b1 only HR's recording sent it. The link is anonymous, so the send names the offer's tenant (`run-lane-nb1.mjs` [F1–F7]) |
| `Recruitment/OfferLetter` | Offer Letter (document) | document, also attached to Offer Issued | HR previews the offer, or the candidate opens it on the portal; and, since lane N-b1, rendered to PDF for the Offer Issued email | signed in | **Enforced**. Raw HTML: `BenefitsList`, `ConditionsList`, `DutiesList`, `PreEmploymentChecklist`, `SalaryBreakdownTable`. ⚠ The PDF engine (Syncfusion's HTML import) needs XHTML, and it drew `rem`-styled tables as empty boxes. `HtmlToPdfRenderer` normalises both, and the suite checks that the terms and salary tables' words are in the PDF [E4b–E4c] |
| `Recruitment/TalentPoolInvitation` | Talent Pool Invitation | email | HR invites a talent-pool candidate to apply | signed in | **Enforced** |
| `Recruitment/TestInvitation` | Test Invitation | email | HR invites candidates to a recruitment test | signed in | **Enforced** |
| `Probation/ProbationConfirmationLetter` | Probation Confirmation Letter | document | HR generates a confirmation letter | signed in | **Enforced** |
| `Assets/AssetResponsibilityTerms` | Asset Responsibility and Terms | document, also emailed | HR or the holder opens the terms, or HR emails them. The emailed copy is rendered first, then queued | signed in | **Enforced** |
| `OnboardingOrientation/OrientationReminderDigest` | Orientation & Onboarding Reminder | email | the daily reminder sweep (lane K-a), one digest per person | background | **Enforced** |
| `OnboardingOrientation/OrientationEnrolled` | Orientation Enrolment | email | an enrolment made by HR, in bulk, by a rule or by a renewal, on a programme that sends reminders | background | **Enforced**: [F4], [H5–H7], [I3], [I6] |
| `OnboardingOrientation/OrientationSessionScheduled` | Orientation Session Scheduled | email | a person is placed on a session, or a facilitator is on a session going live or added to a live one | background | **Enforced** |
| `OnboardingOrientation/OrientationSessionRescheduled` | Orientation Session Moved | email | a live session moves | background | **Enforced** |
| `OnboardingOrientation/OrientationSessionPostponed` | Orientation Session Postponed | email | a live session is postponed | background | **Enforced** |
| `OnboardingOrientation/OrientationSessionCancelled` | Orientation Session Cancelled | email | a live session is cancelled | background | **Enforced** |
| `OnboardingOrientation/OrientationCompleted` | Orientation Completed | email | a person first completes an orientation | background | **Enforced** |
| `OnboardingOrientation/OrientationCertificateIssued` | Orientation Certificate Issued | email | a certificate is issued or reissued | background | **Enforced** |
| `OnboardingOrientation/OnboardingWelcome` | Onboarding Welcome | email | an onboarding plan is made for a new hire | background | **Enforced** |
| `OnboardingOrientation/OnboardingCoordinatorAssigned` | Onboarding Coordinator Assigned | email | a plan is made, or passes to a new coordinator | background | **Enforced** |
| `OnboardingOrientation/OnboardingBuddyAssigned` | Onboarding Buddy Assigned | email | a plan is made, or passes to a new buddy | background | **Enforced** |
| `OnboardingOrientation/OnboardingTaskAssigned` | Onboarding Task Assigned | email | a task is added for somebody, or passed to somebody new | background | **Enforced** |
| `OnboardingOrientation/OnboardingTaskDone` | Onboarding Task Waiting for Sign-off | email | a task needing sign-off is marked done | background | **Enforced** |
| `CompanySchedule/EventInvitation` | Event Invitation | email | HR adds a participant to an event | signed in | **Enforced** |
| `CompanySchedule/EventRsvpReminder` | RSVP Reminder | email | the hourly sweep chases unanswered invitations `CompanyEventRsvpChaseLeadDays` before the RSVP deadline, **once**; or HR presses **Chase unanswered now**, which counts as the chase. Until lane N-b2 the chase was an API endpoint that no screen called | signed in, or background (sweep) | **Enforced** (§ 2.8) |
| `CompanySchedule/EventReminder` | Event Reminder | email | the hourly sweep sends it `ReminderDaysBefore` before a live event whose **Send reminders** is on, **once**, and again if the date moves; or HR presses **Send reminder now**, which counts as the send. Until lane N-b2 the form's switch and its days were read by nothing | signed in, or background (sweep) | **Enforced** (§ 2.8) |
| `CompanySchedule/EventRescheduled` | Event Rescheduled | email | an event is moved | signed in | **Enforced** |
| `CompanySchedule/EventCancelled` | Event Cancelled | email | an event is cancelled | signed in | **Enforced** |
| `HrLetters/HrLetterEmploymentConfirmation` | Letter — employment confirmation | document | HR previews or issues an employee's letter request | signed in | **Enforced**: [E1–E4] |
| `HrLetters/HrLetterIntroduction` | Letter — introduction | document | as above | signed in | **Enforced** |
| `HrLetters/HrLetterServiceCertificate` | Letter — certificate of service | document | as above | signed in | **Enforced** |
| `HrLetters/HrLetterSalaryConfirmation` | Letter — employment and salary confirmation | document | as above | signed in | **Enforced** |
| `Interviews/InterviewScoreSheet` | Interview Scoring Sheet (printed) | document | HR prints an interview's paper | signed in | **Enforced**. Raw HTML: `CommentLines`, `PanelTable`, `QuestionTable`, `RecommendationBoxes` |
| `Interviews/InterviewQuestionList` | Interview Question List (printed) | document | as above | signed in | **Enforced**. Raw HTML: `PanelTable`, `QuestionTable` |
| `Interviews/InterviewPackCover` | Interview Pack Cover (printed) | document | as above | signed in | **Enforced**. Raw HTML: `PanelTable`, `TimetableTable` |
| `RecruitmentTests/TestQuestionPaper` | Recruitment Test Paper (printed) | document | HR prints a test paper, blank or one per candidate | signed in | **Enforced**. Raw HTML: `QuestionBlock` |
| `RecruitmentTests/TestMarkingKey` | Recruitment Test Marking Key (printed) | document | HR prints the marking key | signed in | **Enforced**. Raw HTML: `KeyBlock` |

**Not a setting, deliberately:** the catalogue seeder (`EmailTemplateCatalogSeeder`). Lane N's plan
proposed seeding a row per template on every tenant at startup, so that a fresh tenant would have
rows to edit. The screen lists from the catalogues instead. A seeded row would freeze its day's
wording, because the resolver prefers a stored row, and lane K-b alone rewrote twelve defaults in one
day. The seeder stays deferred. The resolver also ignores an untouched seeded copy: `IsSystemDefault`
with no `UpdatedBy` [G1–G3].

---

## 2.8 Company schedule — reminders that send themselves — added 2026-09-23

Round 4, lane N-b2. Read by the company-schedule reminder sweep
(`ICompanyEventService.SendDueRemindersAsync`). The sweep runs **hourly** (in
`CompanyScheduleReminderBackgroundService`), and HR can run the same code now with
`POST api/CompanySchedule/reminders/run`.

It sweeps only **live** events: scheduled, confirmed or rescheduled, not cancelled, and approved where
approval is required. Each send is stamped on the event (`ReminderSentDate`, `RsvpReminderSentDate`),
so it goes **once**. A reschedule or an edit that moves a date clears the matching stamp. HR's
**Send reminder now** and **Chase unanswered now**, on the event page's Reminders card, stamp the same
dates. **Two of the three settings were ghosts**: the event form offered them, the database saved them,
and nothing read them.

| Setting (the form's label) | Where | Default | Status | Proof — `hr-templates/run-lane-nb2.mjs`, both positions |
|---|---|---|---|---|
| `SendReminders` ("Send reminders") | per event | off | **Enforced**. It was a ghost | [A2] on: an event two days away, 3 days before, reminded everybody who had not declined. [A4] off, the same date: nobody |
| `ReminderDaysBefore` ("Days before") | per event | none | **Enforced**. It was a ghost | [A5] 3 days before an event in 5: not yet. [A10] the same event at 7: reminded |
| `CompanyEventRsvpChaseLeadDays` ("Chase unanswered invitations this many days before the RSVP deadline") | `CompanyHrPolicySettings`, the policy page's **Company schedule reminders** card | **2**; migration `AddCompanyScheduleReminderSweep` adds it `NOT NULL DEFAULT (2)` | **Enforced**. New in N-b2 | [B3] a deadline four days away, at 2: not chased. [B6] the same deadline at 5: chased. [B7] restored |

**Also proved:**
- once [A8–A9];
- never for a cancelled event, nor one still awaiting approval, and at once when approved [C1–C3];
- HR's send counts as the send [D1–D4];
- a moved date is reminded again, for both a reschedule and an edit of the RSVP deadline [E1–E4];
- the tenant's own wording, under its legal name [F1–F2].

**Not a setting, deliberately:** the hourly cadence. A reminder is day-granular, and the stamp makes
cadence a matter of latency, never of duplicates.

## 2.9 Maintenance technicians — the technician-role flag and the person's exception `EmployeePosition` · `Employee` — added 2026-09-23

Round 4, lane O. **One answer** to "may Maintenance assign this person work?": the stored
`Employee.CanBeAssignedToMaintenance`. HR's technician door (`api/hr/employees/technicians*`) reads
it. So do Maintenance's work-order, labour, staff-schedule and QC gates. It follows the position's
flag unless HR set it by hand, and `ApplicationDbContext.HrTechnicianRole.cs` re-establishes that on
every save touching an employee or a position. ⚠ Before this lane the column had **no writer at
all**: 0 of UAT's 2,089 employees held it, the door listed nobody, and every assignment would have
been refused.

| Setting (the form's label) | Where | Default | Status | Proof — `hr-jobarch/run-round4-o.mjs`, both positions |
|---|---|---|---|---|
| `IsTechnicianRole` ("Technician role") | per position, the position form | off; migration `AddTechnicianRoleFlag` adds `NOT NULL DEFAULT (0)`. TDC's `DV-BMS`, `DV-ART`, `MS-CT` are flagged by `TdcOrganogramSeeder` and demo scenario 007 | **Enforced**. New | [B2–B9] on: a holder is available and in the door, never ticked. [C1–C3] off: out, and the stored column cleared, in the same save. [C5] on again: back. [A6] an update that omits it leaves it |
| `MaintenanceAssignment` ("Available to Maintenance": Follow the position / Include / Exclude) | per employee, the employee form's **Maintenance** section; stored as `MaintenanceAssignmentSetByHand` + the column | Follow the position | **Enforced**. New | [E1–E5] Include in a plain post: in, and it survives moving through a technician post. [E7–E10] Exclude in a technician post: out, and it survives the post switched off and on. [E11–E12] Follow the position hands it back |
| `ExperienceLevel` ("Experience level") | per employee | none | **Enforced vocabulary** — Junior, Intermediate, Senior, Expert, the words Maintenance filters on | [G3] "senior" stored as "Senior". [G4] "Snr" refused, saying why |

**Not settings, and recorded because they look like them:**

| Column | Status | Why |
|---|---|---|
| `Employee.CurrentWorkload` | **Ghost, and worse** | Written by NOTHING, so it is always 0. Yet Maintenance's `TechnicianService` reads it for `IsAvailable = CurrentWorkload < MaxWorkload` and its utilisation figures, so that screen reports every technician free. HR neither surfaces nor reads it. HR's door returns work orders and workload as **null — not supplied** [G30–G31, G35], where it hardcoded 0. Real utilisation is Maintenance's (`TechnicianSchedulingService`). Cross-module defect #29 |
| `Employee.MaxWorkload` | **Maintenance's** | Written only by `TechnicianService.Create/UpdateTechnicianAsync`, which no controller calls. Not surfaced by HR. Defect #29 |
| `Employee.Specialization`, `.CertificationLevel` | Data, HR-written since lane O | The employee form writes them; HR's door and Maintenance's technician list read them [G1–G2, G25–G26] |

**The "available" rule — a rule, not a setting, proved both ways:**
- **In:** a technician on probation is available [B8, G29, G35b]. Every hire starts on probation,
  and it was Active only, so a new artisan read "unavailable: Probation" in the pool that listed
  them. TDC's call, 2026-09-23.
- **Out:** a technician who is not at work is not available, and says so [M1–M2].

Suspended, inactive and terminated staff stay out.

## 2.10 The qualification ladder, as shortlisting reads it `QualificationLevel` · `Qualification` · `JobCandidateQualification` — added 2026-09-24

Round 4, lane Q (the proper fix for recruitment guide R4-5.2). The ladder (*HR Setup → People
Reference Data → Qualification Levels*) existed before this lane, with 11 rungs on UAT from scenario
005, but **nothing read it**. Since lane Q, an *Education level* shortlisting criterion compares the
RANK of a candidate's qualifications against its minimum rung. So the ladder, and where each
qualification sits on it, are now settings with a consequence: they decide scores.

| Setting (the form's label) | Where | Default | Status | Proof — `hr-recruitment/run-round4-q.mjs`, both positions |
|---|---|---|---|---|
| `QualificationLevel.Rank` ("Rank") | per rung, the Qualification Levels screen | scenario 005's ladder: BECE 10 … Doctorate 90, with HND and Bachelor's both at 50 | **Enforced**. Newly read | Q2: the required rung passes (100); a tie passes (HND meets "at least Bachelor's"); below fails, and disqualifies when mandatory (0); a higher rung beside a lower one is judged by the higher |
| `QualificationLevel.IsActive` | per rung | active | **Enforced** | Q1: a retired rung cannot be newly chosen on a qualification, nor as a criterion's minimum (422). The server also lets a rung retired after use stay on its row; that direction is not exercised |
| `Qualification.QualificationLevelId` ("Level" on the catalogue form) | per catalogue entry | none. Scenario 008 places the demo catalogue's Education entries, 59 of 64, and leaves five for HR | **Enforced**. Newly read | Q1: a catalogue pick with no level of its own inherits the entry's rung. Q2: it passes on that rung (100). Q5: the careers catalogue carries it (`levelId`), so the portal can pre-fill |
| `JobCandidateQualification.QualificationLevelId` ("Level") | per candidate qualification, HR's Qualifications tab and the careers profile | none. The migration's one-off backfill filled 152 typed rows from their names | **Enforced**. New | Q1 / Q6: an Education row without one is refused at both doors (422 / 400), a licence needs none, and a stranger id is refused. Q2: none at all is a miss, **60 not 100** when non-mandatory. Q3: frozen into a careers application's snapshot, and back-filled only when the snapshot predates levels |

**The rule's second position, recorded because it is easy to miss:** a tenant with **no active
rung** cannot be asked for a level. Both doors then waive the Education requirement, and the forms
hide the field. The waiver is in the code, and no suite proves it: the demo tenant has a ladder.

**Not settings:** `Qualification.Type` (Education, Certification…) is a category and ranks nothing.
It still decides where a Level is *required*: for Education only.

---

## 3. Leave — per leave type

### Medical evidence (R-15a) — surveyed

These three are on the **leave type**, not the tenant, because they are rules about a KIND of leave:
sick leave needs a certificate, annual leave does not, and every client has both.

| Setting | Default | Status | Proof — slice 7, both positions |
|---|---|---|---|
| `RequiresMedicalCertificate` | `false` | **Enforced** | [1] a 21-day absence submits freely with the flag off; [2] a 7-day one is refused with it on |
| `SelfCertificationDays` | `3` | **Enforced** | [2] 3 days submits, 7 days refuses, and the message names both figures |
| `MedicalBoardThresholdDays` | `90` (null on pre-existing rows) | **Enforced** | [3] two 6-day absences against a 10-day threshold: each passes alone, together they refuse stating 12 |

⚠ **The board threshold is counted across the YEAR.** Asserted with two absences that each pass the
per-request test — a per-request rule would let both through, which is what splitting an absence
looks like.

⚠ **Typing the evidence is what makes these enforceable**, and slice 7 asserts the negative case: an
`Other` attachment does **not** satisfy an excuse-duty requirement, and excuse duty does **not**
substitute for a board recommendation. Without those two, the gate would be satisfied by any file
and the rules would be decorative.

⚠ **The gate is asserted on the auto-approving path too** (`RequiresApproval = false`), which is the
branch where a miss approves sick leave with nobody asked.

⚠ **On a maternity type, leave the board threshold blank** (round 5, lane A; guide § 23, L-58).
Switching the certificate on arms the board rule too, and its default is 90 days a year: the
statutory extension on top of 84 days makes 98, which would send a new mother to a medical board.
TDC's *Maternity Leave* is certificate on, 0 self-certification days, no board — set through the API
on UAT on 2026-09-25, and by the seeder for a fresh build. `run-round5-a.mjs` [2b] proves both
positions on its own maternity type.

### The year-end basis and first-year pro-rating (entitlement plan B2/B3) — added 2026-09-18

Both on the leave type, and both introduced **because the product was already answering their
question silently**. Neither is a correction: both readings are ordinary employer policy.

| Setting | Default | Status | Proof — slice 12, both positions |
|---|---|---|---|
| `YearEndBasis` | **`Granted`** | **Enforced** | carry-over preview, same fixture, two positions: switching to `Earned` carries **exactly 10 fewer days** — `min(entitled 24, cap 20)` against `min(accrued 10, cap 20)` |
| `ProRateFirstYearEntitlement` | **`false`** | **Enforced** | an April joiner entitled to 24 is re-derived to **18** (9/12) by the repair pass, which names both figures |

⚠ **`YearEndBasis` governs BOTH year-end acts**, carry-over and forfeiture, which is why it is not
called a carry-over basis. A name covering half of what a setting does is the kind of thing this
register exists to catch.

⚠ **`ProRateFirstYearEntitlement` is REFUSED alongside an incremental accrual policy**, at both
doors, rather than silently ignored — the distinction § 0's rule turns on. Accrual already limits a
joiner to the months they were present; scaling the entitlement too would deduct for them twice, and
a third time through the derived per-period rate. Slice 12 asserts the refusal from each side **and**
asserts that a full-grant policy is still allowed, so the rule is about the accrual *mode* rather
than about accrual existing.

⚠ **Both defaults are the behaviour that predates them, deliberately.** The year-end runs have no
undo, so a default that silently moved people's carried days on the next run would be worse than the
inconsistency it corrects — the same reasoning that gave `AllowInServiceEncashment` its conservative
default.

### The kind — `Category` (round 5, lane A) — added 2026-09-25

Replaces `MandatoryAnnualLeave`, which only ever meant "this is the annual leave". Migration
`AddLeaveTypeCategory` chose one Annual type per tenant (`ANN` first) and made `MAT` Maternity, then
dropped the flag.

| Setting | Default | Status | Proof — `run-round5-a.mjs`, both positions |
|---|---|---|---|
| `Category` | `Other`. On save, null means Other on create and **unchanged** on update | **Enforced** | [1] a second ACTIVE Annual is refused at both doors (create; switching an Annual type on), a switched-off type may be Annual, and the Annual type's own save is allowed with its row unchanged; [2] Maternity takes leave at 5 days' notice where Other with the same 30 days refuses, and refuses send-back and move where Other accepts both; [3] plans refuse Other and Maternity and accept Annual; [4] in-service encashment refuses Other and Maternity, and Annual passes to the next check; [5] the compliance register holds Annual rows only |

⚠ **Readers still to come**, in their lanes: the balances view (J), the reminders (I) and the
leaver's settlement (L2). The casual-leave set-off (H) landed 2026-09-26: only an **Other** kind may
charge its extra days to annual leave, and the charge always lands on the tenant's one active Annual
type.

### Beyond the limit — `AllowOffsetAgainstAnnual` (round 5, lane H) — added 2026-09-26

Decision A5: days asked for beyond a leave type's limit may be charged to annual leave, the employee
asking and HR deciding at the final approval, which splits the request (`LeaveRequest.SplitFromRequestId`
on the annual part). Migration `AddLeaveRequestSplit` added it off for every existing type; UAT's
`CAS` was switched on through the API, and the demo seeder sets it on `CAS`.

| Setting | Default | Status | Proof — `run-round5-h.mjs`, both positions |
|---|---|---|---|
| `AllowOffsetAgainstAnnual` | off. On save, null means off on create and **unchanged** on update | **Enforced** | [1] off and on read back through both mappers; a save without it leaves it on; refused on an Annual or Maternity type and on a type that does not require approval, at both doors; [2] with it on, a request beyond the limit that asks is accepted and one that does not is told it could; with it off the plain refusal, and asking is refused; [3] the final approval splits 5 days into 3 + 2 on annual leave, the ledger exact on both types; [9] the split is re-checked at approval, refused when annual leave can no longer take the days; [10] annual leave's service gate applies to the extra days |

### The rest of `LeaveType`

Surveyed 2026-09-18 and clean — see § 3.2, which also explains why the leave guide's claim of
**11 ghosts** no longer holds.

---

## 3.1 ⚠ The rest of `CompanyHrPolicySettings` — SURVEYED

**46 properties. Three are ghosts.** Verified mechanically, not asserted: run

```
python scripts/hr-coverage/05_settings_consumption.py \
    src/ErpSystem.Core/Entities/HR/CompanyHrPolicySettings.cs CompanyHrPolicySettings \
    "CompanyHrPolicySettingsDTOs.cs,CompanyHrPolicyMappingExtensions.cs,\
     CompanyHrPolicySettingsService.cs,CompanyHrPolicySettingsController.cs,\
     ApplicationDbContext.HR.cs,policy-settings.ts,settings/policy"
```

| Ghost | What somebody would reasonably believe it does | What it does |
|---|---|---|
| `MinimumWorkingAge` | refuses a hire below the age | **nothing.** No service reads it. It reads like a safeguard and is not one |
| `VoluntaryRetirementAge` | drives a voluntary-retirement date the way `CompulsoryRetirementAge` drives the compulsory one | **nothing** beyond a form rule that it must not exceed the compulsory age. `HrPolicyCalculations` reads the compulsory age and its gender-specific overrides; the voluntary age is read by no code at all |
| `ReviewDueLeadDays` | how far ahead a review is announced | **nothing.** No sweep reads it. The other eleven lead-time settings beside it all have a reminder service that does |

The other 43 are referenced by at least one service. ⚠ **That is not the same as enforced** — see
the note on the tool below.

### Why these three are worth more than a row each

`MinimumWorkingAge` is the one to fix first. The other two are conveniences nobody is relying on;
this one is a **compliance control that does not control anything**, and its presence on a settings
page is an active claim that it does. Either wire it into the hire and candidate paths or take it
off the page — leaving it is the worst of the three options, which is the rule this register exists
to police.

`VoluntaryRetirementAge` is the most misleading. It sits directly beside a setting that **is**
enforced, is validated against it, and does nothing — so the pair reads as one working feature.

## 3.2 `LeaveType` — SURVEYED, and the guide's claim is now stale

⚠ **Superseded 2026-09-25** — see the correction at the top: one ghost (`IsPaid`) and about ten
settings that do something other than their label says. The paragraph below is the 2026-09-18 reading.

**26 settings, zero ghosts.** Every one has a real consumer. ⚠ **That covers the leave type itself
and not its child tables** — those are § 3.2b, added later, and one of them holds this register's
first **Unreachable** entry.

⚠ The leave guide's § 4.4 recorded **11 ghost settings** on leave types, and this register
carried that forward as *"status unknown, not clean"*. **It is now clean.** The closure build's
waves A–E wired them — L-31/L-32 alone took the holiday settings from decorative to load-bearing —
and G2 and G3 added five more that were enforced on the day they landed.

**The guide was right when written and is wrong now.** That is the ordinary fate of a findings
document whose findings get fixed, and it is why this register exists separately: a guide records
what was true on a date, a register records what is true and says how it was checked.

## 3.2b `LeaveType`'s four child tables — SURVEYED 2026-09-18

**⚠ These had no entry at all, while § 3.2 above read as though leave were finished.** They are
where the rulebook's per-type rules actually live — the sub-type caps, the staff-level allocations,
the eligibility rules and the accrual policy — and they hold **38 settings** between them.

```
for c in LeaveAccrualPolicy LeaveSubType LeaveCategoryAllocation LeaveTypeEligibility; do
  python scripts/hr-coverage/05_settings_consumption.py \
      src/ErpSystem.Core/Entities/HR/LeaveEntities.cs $c \
      "LeaveDTOs.cs,LeaveMappingExtensions.cs,LeaveTypesController.cs,ApplicationDbContext.HR.cs,\
       types/hr/leave.ts,types/hr/leave-request.ts,leave-type.service.ts,\
       administration/hr/leave-types,LeaveAccrualPoliciesTab.tsx,LeaveSubTypesTab.tsx,\
       LeaveAllocationsTab.tsx,LeaveEligibilityTab.tsx,LeaveTypeForm.tsx"
done
```

⚠ **`LeaveTypeService.cs` is deliberately NOT in that plumbing list.** See the trap below.

| Table | Settings | Result |
|---|---|---|
| `LeaveAccrualPolicy` | 9 | **8 enforced, 1 Unreachable** — `ProRateOnJoin`. `Frequency`, `Mode`, `AccrualRate`, `MinServiceMonths`, `ProRateOnExit` and `IsActive` all bind in `LeaveEntitlementService`. ⚠ **2026-09-25:** `ProRateOnJoin` has since been fixed; **`IsActive` is the Unreachable one** (nothing can set it false); incremental `Annual` frequency accrues 0; `ProRateOnExit` feeds no payout — see the top |
| `LeaveSubType` | 10 | **enforced.** `MaxDaysAllowed` is the annual cap (`LeaveService`), `IsActive` filters the pickers **and** is refused by the service |
| `LeaveCategoryAllocation` | 9 | **enforced.** `AllocationDays` with `EffectiveFrom`/`EffectiveTo` is step 2 of the entitlement engine's precedence |
| `LeaveTypeEligibility` | 10 | **enforced.** `EligibilityType` drives the four-arm switch in `LeaveTypeService.MatchesRule`, and `Gender` ANDs onto an org-scoped rule |

**One real ghost, and it is harmless:** `LeaveSubType.LeavePlans`, a navigation collection nothing
reads. A dead navigation is not a dead setting — nobody can set it and nobody believes it does
anything. Recorded so the next survey does not re-derive it.

### ⚠ A second methodological trap, found here and not previously written down

The tool's own documentation warns that **passing the setting's edit screen as plumbing is not
optional**, or everything looks consumed. This survey found the mirror of that, and it fails in the
more dangerous direction:

> ⚠ **Passing a SERVICE as plumbing hides any enforcement that lives in that same service.**

The first run of this survey passed `LeaveTypeService.cs` as plumbing, because for
`CompanyHrPolicySettings` the equivalent service genuinely is plumbing — it reads and writes the
record and nothing else. `LeaveTypeService` is not: it carries the CRUD **and** contains
`MatchesRule`, the eligibility evaluator. So `EligibilityType` was reported as a **GHOST**, and it is
one of the best-enforced settings in the module.

| Trap | Direction | Consequence |
|---|---|---|
| Forgetting the **edit screen** | everything looks consumed | ghosts hide. **False negatives** |
| Excluding a **service that also enforces** | a live setting looks dead | **False positives** — you wire something already wired, or worse, delete it |

**The rule:** a file goes in the plumbing list only if it *carries* the value and never *acts* on
it. When in doubt, leave it out and read the extra hits — an inflated count costs a minute; a false
ghost costs a change.

## 3.3 What the tool proves, and what it does not

`scripts/hr-coverage/05_settings_consumption.py` reads every `.cs`/`.ts`/`.tsx` under `src/` and
`frontend/src` once and reports, per property, which files mention it — discarding the files that
merely CARRY the value: the entity, its DTOs, its mapper, its service, its controller, the
DbContext, the TS type, **and the setting's own edit screen**.

| Result | Means |
|---|---|
| **Zero references** | a **ghost**, definitively. Nothing can be consuming it |
| **One or more services** | it is **referenced**. ⚠ NOT proof that it is enforced |

⚠ **Telling Enforced from Advisory from Client-side still needs a human read**, which is what the
appraisal audit did by hand for its 50 fields. Use the tool to find the dead ones cheaply; classify
the survivors by reading them. A count is evidence of life, not of correctness.

⚠ **Passing the edit screen as plumbing is not optional.** Without it every setting looks
consumed, because that screen mentions all of them — and mentioning is not consuming. The first run
of this survey reported **zero** ghosts for exactly that reason.

## 4. Not yet surveyed

Everything below is known to contain assumed values and has **no entry in this register**. Each is
owed a pass, most cheaply as part of that module's own closure plan.

| Area | Known starting point |
|---|---|
| **Appraisal settings** | `HR-APPRAISAL-SETTINGS-AUDIT.md` — 50 fields already classified **by hand**, which is the harder half the tool cannot do. ⚠ **14 do not enforce what they say.** Fold that audit in wholesale rather than re-deriving it |
| Attendance & time | 35 settings noted in the attendance guide; `LateGracePeriodMinutes` is read by **nothing in the solution** (A-1) |
| Travel | the policy rule register is read-only by decision (T-4); caps bind only when a policy is approved (T-1) |
| Company schedule | `MaxBookingDurationHours`, `AdvanceBookingDays` — both ghosts (C-4) |
| Recruitment, performance, medical, separation, discipline | not looked at |
| The rest of `CompanyHrPolicySettings` | 35 fields predating this register — retirement ages, notice periods, the FR-HR-092 threshold, the alert lead times, the grievance clocks |

⚠ **`SettlementDaysPerYear` is in that last group and it moves money.** It is enforced
(`SeparationService.DailyRateAsync`) and it records its basis in words on every settlement line —
but TDC has never chosen the basis, and the entity's own remark says not to run real final
settlements until they do. That warning stands; making the value configurable did not answer the
question, it only made the question answerable by each client rather than by a deploy.

---

## 5. Related documents

- `HR/HR-LEAVE-RESIDUE-CLOSURE-PLAN.md` — § 1 is the rule this register polices; G2 is the work above
- `HR/HR-APPRAISAL-SETTINGS-AUDIT.md` — the enforced / advisory / client-side / ghost classification
- `HR-OPEN-QUESTIONS-FOR-TDC.md` — ⚠ superseded in framing for anything appearing here: these are
  configuration with defaults, not questions blocking a build
- `HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` — the four concepts modelled in both HR and payroll.
  **Not closed by configuration**, because "is anything consuming `PayrollLeaveSetup`?" is a question
  of fact about another module, not a policy anybody can set
