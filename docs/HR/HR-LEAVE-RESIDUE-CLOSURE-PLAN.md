# HR Leave — Residue Closure Plan

**Status:** 2026-09-17 — **G1 BUILT and verified. G2–G7 planned, not built.**

**Read `HR-LEAVE-CLOSURE-PLAN.md` § 0 first** — it describes the code as it stands after the six-wave
closure build of 2026-09-17. This plan picks up what that one deliberately left, plus two things it
could not have known it was leaving.

---

## 0. Progress

### G1 — recall from leave: BUILT 2026-09-17

Migration `20260917192244_AddLeaveRecall` (six nullable columns, guarded SQL, registered in
`FastBuildMigrationMetadata`). `PUT /api/Leaves/{id}/recall`,
`POST api/hr/leave/reminders/advance-in-progress`, the `InProgress` sweep wired **first** in the
nightly host, a recall dialog and a recall panel on both the HR and the employee's own screen.

**Verified by `dev-harness/hr-leave` slice 5 — 51 assertions, green twice. Suite total 243, all five
slices green together.**

⚠ **The recall endpoint was the small part.** `LeaveStatus.InProgress` had been read or filtered in
**eight** places since the port and assigned by **nothing** — so building the sweep that sets it
turned three latent omissions into live ones, and one of them was already causing harm:

| | What was wrong | Why nothing had noticed |
|---|---|---|
| ⚠ **`UsedDays`** counted only `Approved`, while `Close` sets `Completed` and does not recalculate | **closed leave returned its days at the NEXT recalculation** — whichever unrelated event fired it — attributed to nothing | predates the closure build; **no harness slice had ever closed a request** |
| **`Close`** accepted only `Approved`, while reminder sweep 2 chases `Approved ǀ InProgress` | a deadlock: the reminder would chase for ever and the action it names would refuse | could not occur until something set `InProgress` |
| **The overlap guard** and the **reliever-conflict guard** ignored `InProgress` | an employee could book leave on top of leave they were currently on | same |

`UsedDays` now counts exactly `CountsAsTaken` — `Approved ǀ InProgress ǀ Completed` — so the balance
and the attendance register cannot disagree about what has been taken. `InProgress` left
`PendingDays` so nothing is charged twice.

**The attendance invariant changed shape.** It was convergent on *status*; it is now convergent on
the *day set*. `PostAsync` prunes rows outside the request's current range instead of only adding,
because recall is the first operation that shortens a request **while it still counts as taken** —
cancel and reschedule both pass through a not-taken status, so the reverse ran and cleaned up. A
request truncated to nothing routes to the full reverse rather than returning early.

**Deviation from § 4's sketch:** the plan proposed fixing `ReverseAsync` to take a range. It is
`PostAsync` that prunes instead — the rule then needs no caller to remember it, and no future
operation that shortens a range can reintroduce the bug.

### G2–G7 — not started

Unchanged from § 4 below.

---

## 0. What this closes, and why it is not only leave

The leave closure plan finished with seven open items. It filed four of them as *"blocked on TDC"*.

**That framing was wrong, and correcting it is the point of this plan.** TDC is the first client of
this application, not its only one. An item that waits for one client's policy answer is not blocked
— it is **unconfigured**. The fix is a default that a client can change, not an email.

Applying that turn:

| Was filed as | Really is | Closes how |
|---|---|---|
| R-14 recall from leave | a feature, fully specified | build it |
| R-15 excuse duty / medical board | blocked on TDC's thresholds | **defaults + settings**, and the board is a Medical-module record |
| L-D7 two daily-rate bases | blocked on TDC | **already configurable on both sides** — see § 2.2 |
| L-D8 in-service encashment | a requirements conflict | **a tenant switch, defaulting to the conservative reading** |
| L-D9 five reminder windows | ours, not TDC's | move the constants to settings |
| L-D6 what unpaid leave does to pay | needs the payroll owner | **still does** — it is a contract, not a policy |
| `PayrollLeaveSetup` second rulebook | needs the payroll owner | **still does** — it is a fact, not a policy |

**After this plan, nothing in leave waits on TDC.** Two items wait on the payroll developer, and
neither is a question about policy.

⚠ **This plan reaches outside leave.** R-15's board is a Medical-module record (§ 2.1), its outcome
is a Separation reason that already exists, and § 4's G6 registers assumed values across **all** of
HR, not just this module. That is deliberate and it is named here so nobody widens it silently
later.

---

## 1. The rule this plan is built on

> **A setting that saves and is read by nothing is worse than a hardcoded constant.**

A constant is honest: it is not configurable and nobody thinks it is. A dead setting invites someone
to set it, believe it binds, and discover months later that it never did.

This is not hypothetical in this codebase:

- the **appraisal settings audit** found **14 of 50 fields do not enforce what they say** — 9
  advisory, 2 client-side only, 3 read by nothing at all;
- **travel's `PolicyRulesPanel` ships read-only on purpose**, with the argument recorded on the
  component: an editable control that does nothing creates false assurance;
- the leave guide's § 4.4 traced **11 ghost settings** in this module alone before the closure build.

**So every setting introduced by this plan ships with its enforcement in the same slice, and with a
harness assertion that proves it binds in both positions.** A slice that adds a field without a
failing-then-passing assertion is not done. This is the acceptance rule for G2, G3 and G6.

---

## 2. The register

### 2.1 Buildable, fully specified

#### R-14 — recall from leave (curtailment)

Specified in `HR-LEAVE-CLOSURE-PLAN.md` § 3.3b. Unchanged by this plan. Two things make it first:

**⚠ `LeaveStatus.InProgress` is a dead status.** Verified 2026-09-17: **eight** sites read or filter
on it and **nothing anywhere assigns it**. The product has no notion of leave that is *currently
happening*, which is the precondition for recalling somebody from it.

**⚠ It exposes a live defect in what wave D shipped.** `LeaveAttendancePostingService.ReverseAsync`
selects rows by `LeaveRequestId` alone with no date range — it is all-or-nothing. The attendance
invariant built in wave D is convergent on **status** but not on **range**. Truncating `EndDate`
while the status stays `Approved` means the reconciler takes its *post* arm, `PostAsync` only adds,
and the now-out-of-range `OnLeave` days are left behind. Reschedule does not hit this because it
drops to `Pending` first, so the reverse runs. **Curtailment is the first operation that changes a
range while the leave still counts as taken.**

Two facts that make the `InProgress` introduction safer than it looks, both verified:

- `LeaveService.CountsAsTaken` **already** returns true for `InProgress`, so attendance posting is
  correct the moment the status starts occurring;
- reminder sweep 2 (*leave that ended and was never closed*) **already** filters
  `Approved || InProgress`. Sweep 1 chases before the start date, so it is unaffected.

What still needs auditing is the other six read sites, the balance derivation, and the close and
cancel paths.

#### R-15b — the medical board

**Decision taken 2026-09-17: the full board apparatus, in the Medical module.** The SHE↔Medical
ownership boundary already settles this — Medical owns clinical records (`EmployeeHealthProfile`,
`EmployeeMedicalExam`, facilities, physicians) and other modules **bridge by reference**. A board
that rules on fitness is a clinical record. It is gated on `HR.Medical.*`, not the HR role.

**It is much less new code than it sounds, because the vocabulary already exists:**

| Needed | Already there |
|---|---|
| the outcome vocabulary | **`MedicalExamResult`** — Fit · FitWithRestrictions · TemporarilyUnfit · Unfit · RequiresFurtherInvestigation. Exactly a board's findings |
| findings / recommendations / restrictions / review date | fields on `EmployeeMedicalExam`, to mirror |
| the retirement outcome | **`SeparationReason.MedicalRetirement = 11`** already exists |
| facilities and physicians | `HealthcareFacility`, `Physician` |
| a panel of members with a sitting | the shape of performance's calibration panel, to copy |

So the new records are: a **board**, its **sitting**, its **members**, and its **recommendation** —
the recommendation reusing `MedicalExamResult` and optionally referencing the `EmployeeMedicalExam`
it was based on.

⚠ **The board does not decide leave.** It records a clinical recommendation. Leave reads it;
separation reads it. Neither writes to it. Keeping that one-way is what stops this becoming three
modules with a shared mutable record.

### 2.2 Closed by configuration

#### L-D7 — the two daily-rate bases

**⚠ The closure plan recorded this as two hardcoded formulas awaiting TDC. It is not.** Verified
2026-09-17 — **both sides are already configurable**:

| | Where it is configured | Default | Formula |
|---|---|---|---|
| Separation settlement | `CompanyHrPolicySettings.SettlementDaysPerYear` | 365 | `monthly × 12 ÷ days` |
| Leave encashment | `LeaveType.EncashmentRateBasis`, `EncashmentWorkingDaysPerMonth`, `EncashmentRatePerDay` | 22 | `(basic + linked allowances) ÷ divisor` |

Separation already writes its basis onto every settlement **in words**, so a payout always says
which basis produced it. `SeparationService` even carries the comment naming the 38% spread.

**They are also not the same formula with a different constant.** One is working-days-per-month on
basic-plus-allowances; the other is calendar-days-per-year on monthly salary. They are different
money events — encashing five unused days while employed is not a final settlement on exit — and
plenty of clients will legitimately want different bases for each. **Forcing one would be wrong for
a product.**

So what is actually defective is narrower, and none of it needs TDC:

| # | Defect | Fix |
|---|---|---|
| 1 | `EmolumentService.DefaultWorkingDaysPerMonth = 22` is a **private const** — the last genuinely hardcoded piece, used whenever a leave type leaves its divisor at 0 | move it to `CompanyHrPolicySettings` |
| 2 | **Leave encashment does not stamp its basis** on the payout. Settlement does. So an encashment record cannot say what produced its figure | stamp it, in words, exactly as settlement does |
| 3 | The two are configured at **different scopes and on different screens**, so no client can see that their defaults disagree by 38% | **one settings screen showing both side by side, with a worked example on the same salary** |

Defect 3 is the one that matters. A client should meet that 38% on a settings screen before they
meet it in a payout.

#### L-D8 — is in-service encashment permitted at all?

FR-HR-046 says leave is encashed *"only on exit, no other route"*. The module ships an in-service
encashment path, and the seed flags annual leave `AllowCashConversion`. **Both readings are live in
the product at once.**

**Decision taken 2026-09-17:** a tenant switch, `CompanyHrPolicySettings.AllowInServiceEncashment`,
**defaulting to `false`** — the conservative reading, which is also FR-HR-046's. `LeaveType.
AllowCashConversion` only takes effect when the tenant switch is on. Another client turns it on and
gets the other reading, deliberately rather than by accident.

⚠ **The demo database needs a seed line turning it on for the demo tenant**, or the in-service
encashment screen goes dark on a database stakeholders have already been shown. That seed line is
part of G2, not an afterthought.

#### L-D9 — the five reminder windows

7 days before a start · 2 days' closure grace · 5 days undecided · month 9 for mandatory leave ·
30 days for carry-over. All ours, all documented at their constants, none configurable. Move them to
settings. Precedent and reasoning already exist on `GrievanceRungChaseDays`, which carries the
argument this whole plan rests on.

#### L-D10 / R-15a — excuse duty thresholds

**Decision taken 2026-09-17: build in full with defaults that a client can change.** The
self-certification threshold and the cumulative medical-board threshold become **leave-type**
settings — they are rules about a kind of leave, not about a tenant. Defaults stated in G3, and
stated *as* defaults.

### 2.3 Not closed by configuration

These two are owed by the **payroll developer**, not by TDC, and no amount of HR configuration
answers either.

| Item | Why configuration cannot close it |
|---|---|
| **L-D6 — what `IsPaid = false` should do** | HR can configure what it *records and exports*; whether payroll deducts is payroll's code. It is a **cross-module contract**, and the handoff doc raises it |
| **`PayrollLeaveSetup`** — leave days by service band, duplicating `LeaveType` + `LeaveCategoryAllocation` | It is a **question of fact**: is anything consuming it? If so there are two live rulebooks for "how many leave days does this person get", and the module we just closed enforces only one. The `LegacyCompanyCode` column hints at a dead import artefact, but the table and its endpoints were seen; the downstream use was not |

Both are already written up in `docs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` and
`docs/HANDOFF-PAYROLL-LEAVE.md`. **The action on both is to get an answer, not to build.**

### 2.4 The eight deferred guide findings

`L-12, L-13, L-15, L-19, L-20, L-22, L-24, L-26` — deferred before F1 as convenience gaps.

**⚠ L-13 is worse than its filing.** The leave-type PUT is a replace-set, so an update that omits
the allowances collection **silently unlinks every allowance on that type** — which, given § 2.2,
silently changes what an encashment pays. It is a data-loss bug, not a convenience gap, and it is a
few lines. This codebase has been bitten by the replace-set shape before.

---

## 3. Decisions

Both open decisions were taken by the module owner on 2026-09-17. **No decision in this plan is
outstanding.**

| # | Decision | Taken | Consequence |
|---|---|---|---|
| **E-1** | `AllowInServiceEncashment` default | **`false`** — FR-HR-046's literal reading | a fresh tenant matches the FRD. ⚠ The demo tenant needs an explicit seed line, or a screen stakeholders have seen goes dark |
| **E-2** | How far R-15's board goes | **the full apparatus, in the Medical module** | three modules touched, its own harness. Cheaper than it sounds — § 2.1 |

Recorded so a later reader does not reopen them: the three *recommend* decisions from the original
plan (D-9, D-10, D-11) stand as built, and D-1…D-5 remain overturnable by any client through
configuration rather than code.

---

## 4. The slice plan

Ordered so each ships alone. **G1 first because it carries a live defect**, not because it is the
biggest.

| Slice | What | Size | Notes |
|---|---|---|---|
| **G1** | **R-14 recall / curtailment.** `RecalledOn` / `RecalledById` / `RecallReason` / `DaysRestored` on `LeaveRequest`; `PUT /api/Leaves/{id}/recall`; `EndDate` truncated and `TotalDays` recomputed through `GetChargeableDaysAsync`. ⚠ **Includes making the reconciler range-aware** — prune rows outside the current range, not reverse-all-or-post-all. ⚠ **Includes a sweep that sets `InProgress`**, plus the audit of the six remaining read sites | **L** | needs a scaffolded migration |
| **G2** | **The configuration turn.** `AllowInServiceEncashment` (default false) + its demo seed line; `DefaultEncashmentWorkingDaysPerMonth` replacing the private const; the five reminder windows; the encashment basis stamped onto the payout in words. **One settings screen showing both daily-rate bases side by side with a worked example** | **M** | needs a scaffolded migration |
| **G3** | **R-15a — the leave-side gate.** Typed evidence (`ExcuseDuty` / `MedicalBoardRecommendation`); `RequiresMedicalCertificate` + the self-certification threshold + the board threshold on `LeaveType`; **refusal at submit**, naming what is missing, in the shape of the eight existing create checks | **M** | usable on its own, without G4 |
| **G4** | **R-15b — the board.** Board · sitting · members · recommendation in the Medical module, reusing `MedicalExamResult`, gated on `HR.Medical.*`. Referenced from leave; `MedicalRetirement` handed to separation. **One-way: the board is read by leave and separation, written by neither** | **L** | three modules |
| **G5** | **L-13 and the other seven.** L-13 first — it is a data-loss bug, not a convenience gap | **S** | |
| **G6** | **`docs/HR-CONFIGURATION-REGISTER.md`** — every assumed value across **all** HR: where it lives, its default, whether the UI can set it, and whether it is **enforced / advisory / ghost**. The appraisal settings audit is the template for that classification | **S** | documentation only |
| **G7** | **Harness.** `dev-harness/hr-leave` slices 5 and 6 — curtailment and its attendance range, and every setting G2/G3 introduced proved in **both** positions | **M** | the § 1 acceptance rule |

**G6 is not optional paperwork.** It is the answer to *"I hope we don't forget anything that needs
to be confirmed later"*. A register that records enforcement status is the only artefact that makes
§ 1's rule checkable rather than aspirational.

---

## 5. Out of scope

| Not doing | Why |
|---|---|
| Forcing one daily-rate basis across leave and separation | § 2.2 — they are different money events, and a product should let a client set each |
| Building configuration for other HR modules' assumed constants | G6 **registers** them; each module's own closure plan builds them. Widening here would never end |
| Any GL posting for encashment or leave liability | unchanged from the original plan — one Finance sweep posts everything |
| Payroll's treatment of unpaid leave | § 2.3 — raised, not built |
| Liveness detection, biometric hardware | the attendance guide's § 1.7 — a procurement spec, not code |

---

## 6. Related documents

- `HR-LEAVE-CLOSURE-PLAN.md` — § 0 is the only description of current leave code
- `HR-LEAVE-SYSTEM-GUIDE.md` — the screen-by-screen walk the findings came from
- `../HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md` · `../HANDOFF-PAYROLL-LEAVE.md` — § 2.3's two items
- `../HR-OPEN-QUESTIONS-FOR-TDC.md` — L-D1…L-D10. ⚠ **G6 supersedes its framing**: items become
  configuration with defaults, not questions blocking a build
- `HR-PERFORMANCE-SYSTEM-GUIDE.md` § 1.6 and the appraisal settings audit — the enforced / advisory
  / ghost classification G6 reuses
