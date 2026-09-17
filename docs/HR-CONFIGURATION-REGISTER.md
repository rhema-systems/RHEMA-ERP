# HR — Configuration Register

**Started 2026-09-17** (leave residue plan, slices G2 and G3). **Partial: leave only** — encashment,
the reminder cadence, and the medical-evidence rules. Everything else in HR is still to be surveyed
— see § 4.

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

⚠ **"Enforced" requires the two-position test.** Asserting a setting at its default proves nothing:
a hardcoded value passes that test perfectly. The assertion must change the value, observe the
behaviour change, and change it back.

---

## 1. Leave — encashment `CompanyHrPolicySettings`

| Setting | Default | Status | Enforced where | Proof |
|---|---|---|---|---|
| `AllowInServiceEncashment` | **`false`** | **Enforced** | `LeaveEncashmentService.RequestEncashmentAsync` — asked **before** the leave type, so the refusal names the real reason | slice 6 [1] — refused off, accepted on, same request |
| `EncashmentWorkingDaysPerMonth` | `22` | **Enforced** | `EmolumentService.GetEncashmentDailyRateAsync` | slice 6 [2] — 6,600 ÷ 22 vs ÷ 30, exact figures |

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

---

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
| `MandatoryLeaveChaseFromMonth` | a balance on a `MandatoryAnnualLeave` type with days outstanding — **planted directly**, as a year's accrual is not what the assertion is about |
| `LeaveCarryOverExpiryReminderDays` | a balance with carried days on a type with an expiry month — planted, with `CarryOverExpiryMonths = 12` so the lapse date is 31 December and the test works from any day of the year |

⚠ **One half of the mandatory assertion cannot run in December** — it needs a month the year has not
reached. The slice skips it with a printed note rather than quietly inverting; a test that changes
meaning with the calendar is worse than one that admits it cannot run today.

**Not moved, deliberately:** the engine's 90-day backlog horizon stays a constant. It stops the first
run on an established database queueing years of history at once (area 9 queued 275 items, 242 of
them history). It protects the system from itself; it is not a policy anybody should be choosing.

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

### The rest of `LeaveType` — not surveyed

⚠ The leave guide's § 4.4 traced **11 ghosts** among them; that list has not been re-checked since
the closure build, so its status is **unknown**, not clean.

---

## 4. Not yet surveyed

Everything below is known to contain assumed values and has **no entry in this register**. Each is
owed a pass, most cheaply as part of that module's own closure plan.

| Area | Known starting point |
|---|---|
| **Appraisal settings** | `HR-APPRAISAL-SETTINGS-AUDIT.md` — 50 fields already classified. ⚠ **14 do not enforce what they say.** Fold that audit in wholesale |
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
