# Leave settings that do what they say — the second audit, and pay to Finance

> **Status (2026-09-27): slice A BUILT — pay is Finance's (L-73 to L-76 closed); slices B and C
> next.** The user agreed the whole of it on 2026-09-27 ("I agree. proceed."). The live record is
> § 8 at the bottom: one entry per slice. ⚠ Two decisions the build raised are the user's (§ 8,
> slice A, *For the user*), and one UAT data step is theirs to run.

## Why this exists

On 2026-09-26, after round 5 was built, the user asked whether every leave-type and leave-related
setting is used and enforced — no dead or ghost settings. Four read-only reviewers traced about
seventy settings (the leave type's 31 fields, its five child tables, and fifteen company settings)
from the screen that sets them to every piece of code that acts on them, across every door a
request passes through. **Every finding below was verified in the code before it was written
here.** Round 5 lane N's audit (2026-09-25) was re-checked and holds.

What was sound: nothing on the forms is a ghost (every field is saved), and most settings do what
they say — day counting, notice, carry-over caps, sub-type caps, the Annual ceiling, casual beyond
its limit, the five reminder timings, the settlement cap, the medical board settings.

What was not: one set of settings that paid nobody, two dead columns, ten rules with a door left
open, and ten labels or small behaviours that said something the code did not do.

## Decisions

- **P1 — Pay is Finance's.** The stakeholders asked for HR to leave the monetary aspects and the
  calculation to Finance and submit the details, such as the days encashed. Round 5 had already
  recorded the principle (decision A3, *"HR decides the days, Finance the money"*; A7, the leave
  owed report in days only) but built it only as a label. The standard enterprise split: **HR owns
  facts and quantities** (dates, reasons, days owed, notice days); **payroll and Finance own the
  valuation** (the daily-rate formula is payroll configuration); **Finance owns posting and
  payment**; the one who counts is not the one who prices, and an independent review comes before
  money moves.
- **P2 — Finance values in its own step.** A new permission, **`HR.Pay.Value`** ("value the pay
  HR records"), held by Finance — by default **Finance Officer, Senior Accountant and Chief
  Accountant**, which TDC is asked to confirm. Finance works from a queue of what awaits valuation
  and sees only that, not HR's records at large. There is a precedent in the product: Inventory's
  physical count has a separate Finance Reviewer stage for its money side. The full standard —
  payroll pricing the days in the leaver's final pay run — depends on the payroll module (another
  developer's) and is recorded as a hand-off, not waited for.
- **P3 — Every pay line on a settlement.** Unpaid salary, notice pay in lieu, annual leave owed,
  gratuity or end-of-service, pension-related, tax: HR records the days and facts, Finance enters the
  amount and its source; HR cannot price them. Recoveries of documents Finance already holds
  (loans, salary and travel advances, asset and property recoveries, other deductions) stay as HR
  carries them. **HR keeps no pay-rate settings.**
- **P4 — Cashing in while employed** (off for TDC; other organisations may use it): HR approves the
  days; Finance marks it paid, entering the amount with the payment reference; the posting to
  Finance's books uses Finance's amount.
- **P5 — No HR "indicative" amount on a pay line.** A figure on HR's screen gets copied as *the*
  figure — which is what happened: the leave line's "indicative" amount went into Finance's books on
  release unless somebody overwrote it.
- **Design calls on the rule gaps** are stated with each item below; the user may overrule any.

## Findings — recorded in the leave guide § 23 as L-73 to L-96

| ID | Finding (verified) | Fix | Slice |
|---|---|---|---|
| **L-73** | The Annual type's encashment block (rate basis, rate per day, working days per month, allowances included) valued nothing a leaver was paid, while its hint said *"These settings value a leaver's unused days"*. With in-service encashment off they changed no payment at all. | Removed, with the rate preview and the policy page's "two bases" table (P1, P3) | A |
| **L-74** | A leaver's leave line and notice pay in lieu were HR's calculation (monthly basic × 12 ÷ 365), posted to Finance's books when Internal Audit released the statement. *"Finance confirms the amount"* was a label, not a step. | Days only; Finance values every pay line in its own step (P2, P3, P5) | A |
| **L-75** | The company's encashment working days per month: no screen could set it, and it was read only when a type's own figure was 0, which only the API could save. | Dropped | A |
| **L-76** | `LeaveCategoryAllocations.LeaveSubTypeId`: retired on purpose in lane N, still a column (0 rows use it). | Dropped | A |
| **L-77** | Medical evidence could be withdrawn after it passed: attachments deletable at any status, a board unlinkable at any status, and approval never re-checked. | Evidence locked once submitted; unlinking a board only in Draft; the final approval re-runs the evidence gate | B |
| **L-78** | The service-length gate and eligibility were checked only when a request was made: a draft saved before a rule changed could be submitted, and moving dates earlier got past the service gate. | Submit re-runs both; every move re-runs the service gate at the new start | B |
| **L-79** | A retired leave type was still reachable: submitting a pre-retirement draft, plans, in-service encashment; and an API save that left out `isActive` revived a retired type. | Each door refuses a retired type; `isActive` null = unchanged | B |
| **L-80** | Maternity leave could be recalled, although its dates never move and the Labour Act gives at least 12 weeks (Act 651 s.57). | Recall refuses maternity leave | B |
| **L-81** | Carried-over days stayed bookable after their expiry date until somebody ran the expiry job. | The booking check counts carried days only for leave taken before the lapse — the owed report's own rule | B |
| **L-82** | "Requires approval: off" was ignored when dates were moved: the moved request went back to an approver. | A moved request of such a type is approved again directly, as submit does | B |
| **L-83** | The "cash in while employed" switch was checked only at request time; an encashment could be approved and paid after it was switched off. | Approve and pay refuse when it is off | B |
| **L-84** | The board threshold was not re-checked when a request moved, unchanged in length, into another leave year. | A move that changes the leave year re-runs the evidence gate | B |
| **L-85** | Reliever availability was not re-checked when dates moved. | Re-checked on every move | B |
| **L-86** | Plans validated neither eligibility nor the sub-type (active, of this type) until the request was raised, after approval. | Checked when the plan is saved | B |
| **L-87** | "Pro-rate on exit" said *"A leaver's settlement does not read this figure yet"* — it has since round 5 lane L. | Corrected | C |
| **L-88** | Self-certification and the board threshold count the type's chargeable days; the hints said days. | Hints say chargeable days, as this type counts them | C |
| **L-89** | An allocation's "effective from" covers the whole leave year it falls in, and editing an allocation did not reach existing balances until an administrator ran *Repair entitlements*. | Saving an allocation re-resolves that type's balances for the leave year; the label says the year rule; "from" defaults to the leave year's start | C |
| **L-90** | Forfeiture could run for a year still open, though its help said a year that has ended cannot be booked. | Refused for a year that has not ended (a preview still runs), as carry-over already is | C |
| **L-91** | The job offer found annual leave by the word "Annual" in its name and ignored staff-level allocations and the ceiling. | By kind; the days the post's staff level would get | C |
| **L-92** | Accrual frequency "None" was offered and shown in force, accrued nothing, and blocked adding a real policy. | Refused and removed from the picker: no accrual is no policy | C |
| **L-93** | An organisation-unit eligibility rule matched the exact unit only; HR's own audience rule covers the units beneath. | The unit and every unit beneath it | C |
| **L-94** | Eligibility rules were not validated on the server (a Position rule with no position matched nobody). | Validated | C |
| **L-95** | Six desk leave screens opened on the calendar year; the leave-year change guard skipped a tenant with no settings row and did not count plans. | `useLeaveYear` on the six; the guard's two holes closed | C |
| **L-96** | Two code comments said the opposite of the code (sub-type allocations take precedence; "no entitlement keyed on grade"). | Corrected | C |

**Noted, not built:** the policy page's update resets any field a caller leaves out (the page always
sends every field — an API-only trap); a temporary-incapacity case's stored sentence names the
months in force when it was assessed while its date follows the setting today (medical, by
design); `LeaveYearContext` takes the tenant from the signed-in user, harmless until a scheduled job
resolves it; the attendance rate's "approved leave counts" switch has no behavioural suite.

## Slices

### Slice A — Pay is Finance's (L-73 to L-76) · migration

- **The settlement.** Notice pay in lieu and annual leave owed are recorded as **days** — a new
  `Days` on the line — with their working, **unvalued**, holding the statement open as unpaid
  salary already does. HR may add or remove pay lines as facts but cannot price them. A Finance
  holder of `HR.Pay.Value` enters each amount with its source; the line records **who valued it and
  when** (`ValuedByEmployeeId`, `ValuedOn`). Then HR finalises, Internal Audit releases, and the
  posting carries Finance's figures. The statement's daily rate is no longer worked out; released
  statements keep the one they were paid on.
- **The one open statement on UAT** with an HR-computed leave line (measured 2026-09-27: 3 open, 1
  such line) has that line turned back to unvalued by the migration, so no HR figure reaches the
  books. Released statements are history and are not touched.
- **Cashing in while employed.** The request records days, not an amount. *Mark as paid* is
  Finance's (`HR.Pay.Value`), taking the amount and the payment reference; the posting uses it.
- **Removed:** the leave type's encashment rate basis, rate per day and working days per month; the
  `LeaveTypeAllowances` table (31 links on UAT, which described HR's rate); the company's settlement
  days per year and encashment working days per month; `LeaveCategoryAllocations.LeaveSubTypeId`;
  the encashment rate endpoint and its preview; the policy page's comparison table. "Allow cash
  conversion" stays: it governs cashing in while employed, and its hint says only that.
- **Finance's screen:** *Pay to value*, in the Finance section of the sidebar for holders of
  `HR.Pay.Value`: statements and encashments awaiting a figure, each line's days and working, and
  **Value** / **Mark as paid**.
- **Suites re-based** (they assert the removed behaviour): hr-leave slice 6 [2]/[3], slice 9's
  allowance links, round5-n's divisor, round5-l's settlement line, hr-separation's daily-rate and
  notice-pay assertions, hr-finance's encashment posting; the new suite `run-audit2-a.mjs`.

### Slice B — Every door (L-77 to L-86)

The ten gaps above, each with its refusal worded and asserted in both positions, in
`run-audit2-b.mjs`.

### Slice C — Labels and small behaviours (L-87 to L-96)

As above, in `run-audit2-c.mjs`; the guide, the explainer, the configuration register and § 23
corrected; the TDC question below added.

## Question for TDC (added to `HR-OPEN-QUESTIONS-FOR-TDC.md`)

**Who in Finance values a leaver's final pay and marks leave cashed in as paid?** The system gives
the permission to Finance Officer, Senior Accountant and Chief Accountant by default. Name the roles
that should hold it — often the payroll unit within Finance. (This also answers the 2026-08-20
question *"How is a daily rate worked out for exit pay?"*: Finance works it out.)

## Verification

1. Per slice: its suite green twice; the re-based suites at their new counts, each change named;
   the hr-leave pass at baseline otherwise.
2. The user's walk: HR prepares a leaver's settlement (days, no amounts); a Finance user values the
   pay lines from *Pay to value*; HR finalises; Internal Audit releases; the posting carries
   Finance's figures.

## 8. Execution log

Newest slice last. Each entry: what was built, what changed from the plan and why, the suite and
its count, the neighbours re-run, and anything found in passing.

### A — Pay is Finance's · BUILT 2026-09-27

**Built.**

- **The permission.** `HR.Pay.Value` ("Value HR pay (Finance)", category *HR - Pay Valuation
  (Finance)*), policy `HR.Policy.PayValue`, granted by `HrPermissions.RoleGrants` to **Finance Officer,
  Senior Accountant and Chief Accountant** (`FinancePayValuerRoles`) — read by the seeder and by the
  role fallback handler, so it binds on a tenant whose seed has not run. HR does not hold it.
- **The settlement.** `PrepareSettlementAsync` works out no daily rate: unpaid salary, notice pay in
  lieu (with its **days**, the new `SeparationSettlementLine.Days`) and annual leave owed (its days,
  with the working) are written **unvalued** (`CannotCompute`), each ending *"To be valued by Finance:
  the amount and its source are entered in Finance's step (Pay to value)."* A stated zero (summary
  dismissal, no days owed) stays a fact. **HR cannot price a pay line** — `IsPayLine`: unpaid salary,
  notice, leave, gratuity/end-of-service, pension-related, tax — on the add or the update; it records
  the line's facts and days. Recoveries stay as HR carries them.
- **Finance's step.** `HrPayValuationController` (`api/hr/pay-valuation`, `InternalOnly` AND
  `HR.Pay.Value`): the queue (statements with pay lines awaiting a figure, then approved encashments),
  a statement read, and `PUT settlement-lines/{id}` — amount (rounded to 2 places, away from zero) and
  source, refused on a recovery, without a source, negative, or once the statement is with Internal
  Audit. The line records **who valued it and when** (`ValuedByEmployeeId`, `ValuedOn`; state
  `ValuedByFinance` = 4); an account with no employee record is refused rather than recorded as nobody.
  HR changing a valued line's **days** sends it back to Finance (its words do not).
- **Leave cashed in while employed.** The request records days only (amount 0, no basis). *Mark as
  paid* is gated on `HR.Pay.Value` and takes `{ amount, paymentReference, basis? }`; the payout, its
  basis (*"Valued by Finance when paid: …"*) and the payer are Finance's, and the posting carries
  Finance's amount.
- **Removed.** The leave type's `EncashmentRateBasis`, `EncashmentRatePerDay`,
  `EncashmentWorkingDaysPerMonth` and the `LeaveTypeAllowances` table; the company's
  `SettlementDaysPerYear` and `EncashmentWorkingDaysPerMonth`; `LeaveCategoryAllocations.LeaveSubTypeId`
  with its key and indexes; `EmolumentService.GetEncashmentDailyRateAsync`, its record and
  `GET api/hr/emoluments/encashment-rate`; the policy page's worked example and the leave type's rate
  block and preview. *Allow cash conversion* stays; its hint says only what it governs.
- **Frontend.** `/hr/pay-valuation` (*Pay to value*, under Finance in the sidebar); the separation page
  shows each line's days, *awaiting Finance* and who valued it, and lets HR edit a pay line's days but
  never its amount; the encashment pages ask Finance for the amount; the leave type form, detail and
  policy page lose the rate settings.
- **Migration `20260927010643_PayValuedByFinance`**, guarded SQL throughout: the three line columns,
  index and Restrict key; the drops (a default constraint looked up, not guessed); the allocation index
  rebuilt without the sub-type. **Two data steps:** (1) on every statement still with HR (separation
  status 6 — a *returned* statement keeps its `FinalisedOn`, so that is not the test), a pay line with
  a non-zero HR figure (Computed or ManuallyEntered) turned back to unvalued, HR's source kept in the
  basis, days read off the description — UAT: **SEP-2026-00002's three lines** (leave 18,539.84 → 56
  days; unpaid salary 9,840; gratuity 148,500); (2) older unvalued notice and leave lines given their
  days from the description — UAT: three lines on SEP-2026-00013 and -00014. The scaffold's
  `UpdateData` in the Down became column defaults (22, 365, 0). Proven on a scratch database (Up, Up
  again, Down, Down again, Up again; **29 checks**). UAT applied it at startup, checked in SQL.

**Changed from the plan, and why.**

1. **Internal Audit cannot release HR's figures, and a return clears them** — not in the plan. UAT had
   two statements **finalised before the change** (SEP-2026-00005, -00012) carrying HR's typed figures,
   with Internal Audit; approving them would have posted HR's figures, the thing P5 exists to stop.
   `ApproveSettlementReviewAsync` refuses a statement with an HR-priced pay line, saying to return it;
   `ReturnSettlementAsync` clears those lines exactly as the migration does, so Finance values them.
   Without the clearing, a returned legacy statement would deadlock: HR could neither price nor
   release it.
2. **A defect found on that path, fixed:** a statement Internal Audit **returned** read *"finalised and
   with Internal Audit"* and `CanFinalise` false — both keyed on `FinalisedOn`, which a return keeps as
   history — so the screen hid editing and *Finalise* and it could never be sent back. Keyed on the
   separation's status now (the rule `RequireEditable` already stated); the page likewise.
3. **The finalise refusal names whose lines wait** — it told HR to "enter each amount" on lines that are
   now Finance's.
4. **The data step 2 did not run on UAT.** It was added after UAT had applied the migration, and my
   UPDATE was refused by the permission classifier (shared database). The rendered batch is
   `dev-harness/hr-leave/audit2a-uat-days-step.sql` — **the user's to run** (three lines; idempotent).
   Until then those three lines show no days in Finance's queue; their descriptions carry them.
5. **The screens accept Finance's roles beside the permission.** Login lists only *seeded* permissions,
   and the seeder does not run in Staging, so on UAT a Finance Officer's token carries no
   `HR.Pay.Value` while the API grants it by role. `PAY_VALUER_ROLES` (PermissionGate, mirroring
   `FinancePayValuerRoles`) gates the sidebar item (`accessMode: 'any'`) and both pages.

**For the user.**

- **Benefit payment and *other earning* are not pay lines** — they were not on the agreed list, so HR
  still types their amounts. Should they be Finance's too?
- **The demo cast has no Finance persona**, so nobody can present *Pay to value*; scenario 160 now leaves
  the retirement statement's pay lines waiting there, and runbook book 3 § 9's settlement steps say HR
  enters the unpaid salary. Both owed with the persona.
- The user's walk (verification 2) is owed.

**Suite.** `dev-harness/hr-leave/run-audit2-a.mjs` — **145 assertions, green twice** (a run before the
build: 142 and the three fixes' 3 red). Fixtures by surname, no logins on the leavers; actors
`leave.fin` (Finance Officer) and `leave.audit` (TDC_INTERNAL_AUDIT) minted once. ⚠ Each run posts two
settlement releases and one encashment payment on UAT, as the lane 8 suites do.

**Neighbours re-based and run** (each change is named in the suite's own comments):

| Suite | Count | Change |
|---|---|---|
| hr-leave slice 6 | **24** (was 32) | [2]/[3] proved the two divisors; now one section: the settings sent are not kept, the endpoint is gone |
| hr-leave slice 9 | **32** (was 37) | [1] guarded the allowance links (L-13); now proves they are gone |
| hr-leave round 5 A | **88** (was 89) | an allowance comparison that would pass on two absent fields, removed |
| hr-leave round 5 C | **112** (=) | its own fixture SQL filtered on the dropped sub-type column (first run 82+1: section [8] died) |
| hr-leave round 5 L | **52** (was 51) | the leave line: unvalued, the days, Finance's sentence — not HR's amount, not "indicative" |
| hr-leave round 5 N | **92** (=) | the allocation assertion now asserts the field's absence, not a null |
| hr-separation slice 5 | **85** | HR refused on pay; Finance values unpaid salary and the gratuity; notice and leave lines carry days |
| hr-separation slice 6 | **39** | Finance restates the returned pay line; *canFinalise* after a return asserted; ⚠ stale since lane 8 (2026-09-20): a released line is refused as *posted to Finance* before the status guard |
| hr-separation lane 2a | **25** | § 5 now proves no HR rate with a salary on record; ⚠ its fixture contract was dated before the actor's own terms (stale) |
| hr-separation run-audit | **134 + 1 red** | tax is a pay line: added as a fact, valued by Finance. ⚠ The red is unrelated and stale: a property clearance line now carries an amount (asset surcharges, lane 8 slice 4) |
| hr-separation slices 10, 12 | **37**, **62** | the shared fixture (`valueUncomputed` in `setup.mjs`: pay lines by Finance, the rest by HR) |
| hr-separation slice 9 | **20 then stops** | its settlement fixture ran; it stops on two unrelated stale points — the direct terminate needs `HR.Employee.Admin` (W3), and a new hire reads *Probation* |
| hr-finance slice 2 | **62** | Finance marks encashments paid with the amount; the request records days; HR refused |
| hr-finance slices 3, 4 | **23**, **86** | pay lines valued by Finance in the fixture; ⚠ slice 4's catalogue count re-baselined 20 → 26 (stale since lane 8 slice 5) |

**Re-based, not run:** `hr-w3-permissions/run-slice7-compensation.mjs` (the removed rate endpoint's
probes) — it takes the first two employees, real TDC staff on UAT; `hr-demo-smoke` scenarios 021 (it
read the dropped table — a crash, not a skip) and 160, and `demo-coverage-manifest.csv` — the demo
dataset builder, not a suite.

**Docs.** The leave guide (ch. 4, 4b, 15 rewritten for Finance's step, 22's short path, § 23 given
L-73 to L-96, Appendix B's `HR.Pay.Value`); the configuration register; `HR-OPEN-QUESTIONS-FOR-TDC.md`
(the question below added; the 2026-08-20 daily-rate question, L-D7 and R5-Q2 marked answered);
`HR-FINANCE-ENTITY-SWEEP.md`, `HR-PAYROLL-BOUNDARY.md`, the payroll settings hand-off, and round 5's
*what changes* note.

**Housekeeping.** The four leave types today's runs created (G2C/G2O/G2Y694007, G5L716689) switched
off through the product's save.
