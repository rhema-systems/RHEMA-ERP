# HR — Configuration Register

**Started 2026-09-17** (leave residue plan, slices G2/G3), extended 2026-09-18 (G6), extended again
2026-09-18 (entitlement plan W1c), and 2026-09-23 (round 4 lane K-a — § 2.5; lane K-b1 — § 2.6).

**Surveyed: all of `CompanyHrPolicySettings` (46 + 1 added 2026-09-18 + 4 added 2026-09-23, all four enforced — § 2.5), all of `LeaveType` (26 + 2), and
all four of leave's CHILD tables (38)** — plus, not as a full survey, the three `OrientationProgram` notice and
certificate switches lane K-b1 made real (§ 2.6: two were ghosts). Three ghosts found, all in the first — **and one setting that is none of the
four statuses**, which is why there are now five. The per-module settings for attendance, travel,
appraisal, company schedule and the rest are still to do — see § 4, which now says what each one is
expected to cost.

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
| `LeaveAccrualPolicy` | 9 | **8 enforced, 1 Unreachable** — `ProRateOnJoin`. `Frequency`, `Mode`, `AccrualRate`, `MinServiceMonths`, `ProRateOnExit` and `IsActive` all bind in `LeaveEntitlementService` |
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
