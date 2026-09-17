# HR ↔ Finance Integration Backlog

**Opened 2026-08-17.** The worklist for the comprehensive Finance-integration sweep that runs
**after the whole HR module is complete**.

---

## Why this file exists

The Finance integration is deliberately split in two (Area 12 plan, decision D-4):

| | when | what |
|---|---|---|
| **Master data** | **now, per area** | Read Finance's `Currency` and `ExchangeRate`. Never keep a parallel HR copy. |
| **Accounting** | **once, after the module** | GL posting, AP artifacts, receivables, budget consumption, payroll routing. |

The reasoning: **GL posting is one accounting design, not twenty-seven.** If each HR area invents
its own treatment while the module is mid-build, the sweep becomes a reconciliation of
twenty-seven inconsistent decisions rather than one design applied consistently. Master data is
the opposite case — it diverges by sitting still, so it is fixed as we go.

**The deferral is only safe if the sweep starts with a complete worklist.** That is this file.

## Rules while the deferral is in force

1. **Record every money-touching point here as its area is built.** An amount that is disbursed,
   paid, accrued, claimed, budgeted or recovered belongs in the register below.
2. **Do not invent an HR-side posting mechanism** — no parallel ledger, no private
   payment-status machine, no HR "journal" to fill the gap. Anything built to paper over the
   missing GL link is work the sweep must unpick.
3. **Leave the plain records alone.** Fields like `PaymentMethod`, `PaymentReference`, `PaidAt`
   stay as the simple records they are; the sweep decides what they become.
4. **Master data is not deferred.** If an area needs a currency, a rate, a supplier or a payment
   term, it reads the canonical one. That work happens in the area, not here.

---

## Finance owner's governance message (2026-08-31) — read before implementing anything

Posted by the Finance module owner once PRs **#70** (`codex/finance-fixed-asset-disposal-settlement`)
and **#72** (`codex/finance-integration-contract-master-promotion`) merged to master — the same
merge already recorded below as "the posting entry point now EXISTS." Recorded here verbatim
because it upgrades two things in this backlog from *recommendation* to *stated policy*:

> "Finance integration foundation is now available on master through PRs #70 and #72. Module
> owners integrating with Finance should begin with: `finance-integration-contract-catalogue.md`,
> `finance-integration-adapter-checklist.md`, `finance-integration-consumer-test-template.md`.
> Each interface has a stable FIN-INT-### identifier and is classified as Available, Planned,
> Decision Required, or Requirements Clarification. **Key rule: operational modules retain
> ownership of their source transactions and approvals. Finance owns account resolution,
> fiscal-period controls, currency, tax and subledger effects, journal creation, audit, reversal
> and idempotency. Other modules should not create Finance journals or posting records
> directly.** Please coordinate with me before implementing anything marked Planned or Decision
> Required, and include the reusable consumer-contract tests with every integration."

**What this changes for HR, concretely:**

1. **Payroll's current GL posting is now explicitly out of policy, not merely legacy.**
   `PostPayrollJournalAsync` (`PayrollService.cs`) calls `IJournalEntryService.CreateJournalEntryAsync()`
   and `.PostJournalEntryAsync()` directly — Payroll creates and posts a Finance journal itself.
   The rule above says exactly the opposite: *"other modules should not create Finance journals or
   posting records directly."* This was already this backlog's top recommendation (see "What this
   changes in the checklist below" further down); it is now a stated rule from the module that owns
   the boundary, not just an HR-side judgement call. **Treat the payroll migration to
   `IFinancePostingEngine` as a compliance fix, not an optional improvement.**
2. **A coordination gate now applies to anything not already "Available."** Before building
   against a contract marked Planned, Decision Required, or Requirements Clarification, HR must
   coordinate with the Finance owner first — not design and build, then ask. The one contract in
   the catalogue that is explicitly HR/payroll-shaped, **FIN-INT-011 ("SH Fund, PF, ESB and fuel
   allocation")**, is Requirements Clarification with **no owner assigned**. HR is the natural
   party to bring a concrete requirement to the Finance owner for this one, rather than wait for
   it to be defined elsewhere.
3. **No contract exists yet for HR's own budget surfaces** (`ManpowerBudget`, `TrainingBudget`,
   `AwardBudget`, `StaffTravelBudget`). FIN-INT-015 (Procurement demand → Finance budget
   commitment) is Available but is a Procurement-specific contract — reusing its *shape* for an
   HR budget-commitment need is a **new, Planned-status conversation with the Finance owner**, not
   something HR can wire up unilaterally by calling `IFinanceBudgetCommitmentService` on its own
   initiative.
4. **Every future HR-Finance adapter must ship with the reusable consumer-contract tests.** The
   shared assertion lives at
   `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConsumerContractAssertions.cs`
   (`ShouldSatisfyPostingContract(...)`); see `docs/Finance/finance-integration-consumer-test-template.md`
   for the required test set (happy path, retry/idempotency, Finance-failure-does-not-mark-posted,
   cross-tenant, closed/locked-period, AR/AP visibility) and
   `docs/Finance/finance-integration-adapter-checklist.md` for the design-time checklist (agree
   ownership and the FIN-INT-### ID before coding; resolve accounts from Finance configuration,
   never hard-code them; generate a deterministic idempotency key; persist `PostingEventId`/
   `JournalEntryId` back onto the HR source record).

This does not change the deferral policy itself (the numbered rules above) — GL posting is still
one sweep, not twenty-seven. It changes what "correct" looks like once that sweep starts, and it
opens one thing (FIN-INT-011) that HR could reasonably raise with the Finance owner ahead of the
sweep, since nobody else owns it.

---

## Register

Status key: 🔲 to record · ✅ recorded, awaiting the sweep · ⏳ area not yet built

### Area 12 — Staff Travel ✅ *(surveyed 2026-08-17; area not yet built)*

Source: `plans/HR-Area-12-Travel-Build-Plan.md` §7.4. Measured fact: **zero** references to
`GLAccount`, cost centre, `ProjectId`, `Payroll`, `BudgetEntry` or `SupplierId` across all 34
travel entities.

| # | money event | entity | what is missing |
|---|---|---|---|
| 12.1 | Expense claim paid | `StaffTravelExpenseClaim` | No GL posting, no AP document. `PaymentMethod` is a travel-private enum, `PaymentReference` free text, `PaidAt` an HR timestamp. `FinanceReviewedById` is an **`Employee`** FK, so the finance review is an HR fact invisible to Finance. |
| 12.2 | Cash advance disbursed | `StaffTravelAdvance` | No GL entry. An outstanding advance is an **employee receivable**: `UnsettledAmount` is a balance-sheet figure living only in HR, appearing in no trial balance and no ageing. |
| 12.3 | Advance settled against a claim | `StaffTravelExpenseClaim.AdvanceDeducted` | The contra-entry that clears the receivable has no accounting counterpart. **⚠ Note: the travel-side arithmetic now EXISTS as of slice 4** — paying a claim deducts and settles the linked advance, capped at the outstanding balance. Before that, nothing wrote `AdvanceDeducted` or `SettledAmount` at all, so employees were paid in full despite holding an advance and the advance stayed outstanding for ever. The sweep therefore inherits correct travel-side numbers to post from, not a blank field. |
| 12.4 | Trip budget committed / consumed | `StaffTravelBudget` | Per-trip envelope (flight / accommodation / per-diem / transport / misc) with no link to `BudgetEntry`, `UnitBudget`, a GL account or a cost centre. The breakdown is legitimately travel-owned; the missing part is that it must **consume from** the department's finance budget. |
| 12.5 | Booking cost committed | `StaffTravel{Flight,Hotel,GroundTransport,CarRental}Booking` | `EstimatedCost` / `ActualCost` per booking, no commitment accounting. |

⚠ **What slice 4 already fixed, so the sweep does not re-litigate it.** The travel-side money
arithmetic is now correct and tested against known quantities: advance settlement exists and is
capped, `NetPayable` has one formula rather than two, and `AmountBaseCurrency` is derived from
`AmountOriginal × ExchangeRate` rather than declared by the caller. **What remains for the sweep is
the accounting, not the arithmetic** — the GL entries, the AP artifact, the receivable, the budget
consumption. The exchange RATE is still caller-supplied; sourcing it from Finance's `ExchangeRate`
is area 12 slice 6, not the sweep.

**Open questions this area raises for the sweep** (also in the area-12 plan §9):

- **Is a travel expense reimbursed through payroll or as a direct payment?** Decides where 12.1
  posts. Payroll is another developer's module — read-only from HR — so a payroll answer makes
  this a hand-off rather than something HR builds. **Needs TDC.**
- **Cost attribution.** Travel has no `ProjectId` and no cost centre. If TDC charges travel to
  projects or departments, the dimension has to come from somewhere.

### Areas already closed — back-fill needed 🔲

These were built before this register existed. Each needs a pass **before the sweep begins**, or
the sweep is a re-survey after all.

| area | likely money events | status |
|---|---|---|
| 2 — Leave | leave encashment — in service and on separation | ✅ **recorded 2026-09-17**, see below |
| 4 — Compensation | pay components, allowances, the payroll boundary | 🔲 to record |
| 7 — Training | training budget, costs, vendor payments, `costPerCompletion` | 🔲 to record |
| 11 — Medical | claim create → approve → **pay**; insurance utilisation; NHIS | 🔲 to record |
| 10 — SHE | any compensation or remediation spend | 🔲 to record |

⚠ **Area 11 is the priority back-fill** — it has a live, working claim→approve→**pay** path, so it
is the closest analogue to travel 12.1 and the two must post the same way.

### Area 2 — Leave ✅ *(recorded 2026-09-17, closure plan wave D slice D3)*

Leave has **one** money event, and unlike travel it is already arithmetically complete: the payout
is derived server-side from the employee's emoluments and the leave type's rate policy, the
lifecycle is create → approve → **processed**, and `Processed` is the only status that moves a
balance. **What is missing is the accounting, not the arithmetic.**

| # | money event | entity | what is missing |
|---|---|---|---|
| 2.1 | Leave encashment paid | `LeaveEncashment` | No GL posting and no AP document. `PaymentReference` is free text and `ProcessedDate` an HR timestamp; `ProcessedByEmployeeId` is an **`Employee`** FK, so who authorised the payment is an HR fact invisible to Finance. The payout (`AmountPaid`) is a real cash movement that appears in no trial balance. |
| 2.2 | Leave liability carried | `LeaveBalance` | Untaken leave is an **accrued liability** — days owed that the company will either pay out or absorb. Six balance components are derived and correct, and none of them reaches a balance sheet. Nothing in HR values them, because the day-rate question below is unanswered. |

**⚠ Two things the sweep must NOT assume.**

1. **There are two daily-rate formulas live in the product.** Leave encashment uses
   `(basic + linked allowances) ÷ 22`; the separation settlement uses `monthly × 12 ÷ 365`. On
   GHS 6,000/month those differ by about **38%**. This is logged for TDC as **L-D7** in
   `HR-OPEN-QUESTIONS-FOR-TDC.md` and is deliberately **not** reconciled in code — picking one
   without an answer would just make it a third. The sweep inherits the question, not a decision.

2. **Whether in-service encashment should exist at all is an open requirements conflict.**
   FR-HR-046 says leave is encashed *"only on exit, no other route"*, and the module ships an
   in-service path with annual leave flagged `AllowCashConversion` in the seed. Logged as **L-D8**.
   If TDC upholds FR-HR-046, event 2.1 collapses into the separation settlement (row 9b) and stops
   being a leave-module posting at all. **Do not build 2.1 before L-D8 is answered.**

**What the sweep inherits that is already right:** four create checks on an encashment, a
server-derived payout the caller cannot override, one-encashment-per-request, create-and-submit in
one transaction, and a balance that only moves on `Processed`. As of 2026-09-17 the processor is
also stamped from the caller's own employee id rather than taken from the request body, so the
"who authorised this" field is at least truthful — it just is not a Finance identity.

### Area 13 — Succession & Talent (recorded 2026-08-18, slice 5)

⚠ **The build plan's first pass said this area had no money in it. That was wrong**, and it is worth
recording why: the money is not on the plan or the candidate, where anyone would look — it is two
fields down on a *development activity*, the training or secondment a successor is put through.
Cost lives where the work happens, not where the record is filed.

| Money event | Where | Notes |
|---|---|---|
| Development activity **estimated cost** | `SuccessionDevelopmentActivity.EstimatedCost` + `CurrencyCode` | Budgeted spend on preparing a named successor — training, secondment, certification |
| Development activity **actual cost** | `SuccessionDevelopmentActivity.ActualCost` | Recorded on the update path once the activity runs |

Both write paths (`SuccessionDevelopmentActivityService` and the candidate service's
`AddDevelopmentActivity` / `UpdateDevelopmentActivity`) now validate `CurrencyCode` against
Finance's `ICurrencyService` — slice 5, after `"ZZZ"` was measured being accepted and stored. That
is the **read-side** integration this module is allowed to do now. No GL posting: it waits for the
one comprehensive sweep, like everything else in this register.

⚠ **Overlap to settle during the sweep:** a development activity of type `Training` describes the
same spend area 7 already budgets through `TrainingBudget`. If a successor's course is recorded in
both places it will be counted twice. Decide once whether succession development costs are their
own budget line or a projection of the training budget.

## Area 17/18 — Job architecture, competency & manpower budget

The **manpower budget is the largest single money surface in the HR module**, and unlike most rows
in this register it is not a payment — it is an *authorisation*. Approving one (FR-HR-135:
Department Head → HR → Managing Director) authorises both headcount and the cost of it, and from
slice 8 it also sets the establishment that gates whether a vacancy may be opened at all. That
makes it the natural place for HR planning and the Finance budget to meet, and the place where a
double-count would be least visible.

| Money event | Where it lives | What it is |
|---|---|---|
| **Salary budget** | `ManpowerBudget.SalaryBudget` | Planned salary cost for the fiscal year, per organisation unit |
| **Benefits budget** | `ManpowerBudget.BenefitsBudget` | Planned benefits cost |
| **Recruitment budget** | `ManpowerBudget.RecruitmentBudget` | Planned cost of filling the posts the budget authorises |
| **Training budget** | `ManpowerBudget.TrainingBudget` | ⚠ see the overlap below |
| **Total budget** | `ManpowerBudget.TotalBudget` | Computed from the four above, not supplied |
| **Actual spent / variance** | `ManpowerBudget.ActualSpent`, `.Variance` | ⚠ **no writer anywhere** — see below |
| **Per-position planned cost** | `ManpowerBudgetLine.PlannedTotalCost`, `.PlannedAverageSalary` | The line-level build-up of the salary budget |
| **Per-position current cost** | `ManpowerBudgetLine.CurrentTotalCost`, `.CurrentAverageSalary` | The baseline it is measured against |
| **Job valuation** | `JobDescription.IndustryBenchmarkSalary`, `.EstimatedSalaryLow/High`, `.RoleIntrinsicValue` | Not a spend — a *pricing* input that suggests a salary grade |

⚠ **No currency anywhere.** Unlike travel and succession, `ManpowerBudget` carries no
`CurrencyCode`: every figure is an unqualified decimal. That was left as-is deliberately rather than
guessed at — adding a currency to a budget is a Finance-shaped decision about what the reporting
currency of an HR plan even is, and this register is the right place to raise it rather than a
slice. **Settle it during the sweep**, and note that it interacts with the double-count below.

⚠ **`ActualSpent` and `Variance` have no writer.** Both columns exist, both are surfaced on the
DTO, and nothing in the codebase ever sets them — a budget's variance is permanently zero. They are
the two fields that can only be filled from actuals, which means from Finance. **This is the single
clearest case in the whole register for the sweep to own**, rather than HR inventing a number.

⚠ **Two overlaps to settle, not one:**
- `ManpowerBudget.TrainingBudget` and area 7's own training budget describe the same spend, and now
  so does succession development (recorded under area 13). That is a **three-way** double-count.
- `ManpowerBudget.RecruitmentBudget` overlaps whatever recruitment cost area 6 tracks per
  requisition. ~~Decide once whether the manpower budget is the parent envelope that the others draw
  down from, or a parallel plan.~~ **DECIDED 2026-09-10 (round 2b, D-5): the manpower budget is the
  parent envelope; approved `StaffRequisitionCost` rows on requisitions linked to a budget draw it
  down (lane R6). `ActualSpent` stays unwritten — the drawdown is a read.**

### Area 6 — Recruitment costs (added 2026-09-10, round 2b R7; the R8 ask is `docs/HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md`)

| Money event | Where it lives | What it is |
|---|---|---|
| **Recruitment cost** | `StaffRequisitionCost` (`Amount`, `Currency`, `ExchangeRate` from Finance, `AmountBaseCurrency`, `CostDate`, `SupplierId`/`PayeeName`, `Status`, `ApprovedById/On`) | Spend on filling a requisition, paid to a Procurement supplier or a named person, approved by HR (`Recorded → Approved | Rejected`) |
| **Payment voucher** | `StaffRequisitionCost.PaymentVoucherNumber` | ⚠ Still a **typed record**. The AP hand-off (HR-approved cost → `IVendorInvoiceService` vendor invoice → Finance approves and pays → HR reads the voucher back) is designed as lane R8 and **waits on the Finance owner's answers** (a producer route, a catalogue row, the authorising event, the non-supplier payee, the expense account). |

Master data is read canonically (rule 4): currency and rate through `HrCurrencyBridge`, the
supplier through `api/hr/suppliers`. **No HR-side payment status exists** (rule 2): `Status` is
HR's own approval of the spend, and whether Finance has paid it is Finance's to say.

**Budget control (round 2b R6, 2026-09-10).** The recruitment envelope is the linked manpower
budget's `RecruitmentBudget`, drawn down by the **approved** base-currency costs of every
requisition on that budget; `GET api/JobAnalysis/budgets/{id}/recruitment-spend` is the read.
Approval is refused under Block / warned under Warn when it would pass the envelope; recording
never refuses. An envelope of 0 is "not set". `ManpowerBudget.ActualSpent` is still **not
written** by any of this — it stays the Finance actuals column (areas 17/18) and the spend read
is what the screens show instead. When the AP hand-off (R8) lands, Finance's budget-control
check on the vendor invoice is a second, independent gate; the two are not reconciled.

**Vacancy costs (the PDF's "vacancy costs within requisition costs") — nothing to validate.**
`JobVacancy` carries no cost field of any kind (only the advertised `SalaryRangeMin/Max`), so the
second half of the bullet cannot be built until a vacancy has a cost estimate to compare. If it
is wanted, the shape is a `JobVacancy.EstimatedCost` (or a per-vacancy cost line) validated at
vacancy creation against the requisition's approved costs. Not scheduled.

### Cleanup owed at finalization (area 17/18, decision D-6)

Harness residue that will appear in any register or report until it is removed, both created by
**area 6**, not by area 17:

- **43 job descriptions** titled `E2E RecB Engineer …` / `E2E RecD Engineer …`, all Draft, all
  childless.
- **Ten salary grades** named `E2E RecD Band 141309` and similar, sitting alongside the two real TDC
  grades (`M1 General Managers`, `M2 Heads of Department`). ⚠ These are the more damaging of the
  two: a salary grade appears in pay-related pickers.

### Area 16 — Staff / Company Assets (recorded 2026-08-24, slice 7)

The money in this area is **recovery from an employee**, not spend on one — the only place in the
HR module where the employer is the creditor rather than the payer, which is why it needs stating
plainly rather than filing under "HR costs".

| # | money event | entity | what is missing |
|---|---|---|---|
| 16.1 | Surcharge assessed | `AssetSurcharge.AssessedAmount` + `CurrencyCode` | An approved surcharge is an **employee receivable**: a balance-sheet figure living only in HR, in no trial balance and no ageing. `CurrencyCode` **is** validated against Finance's `ICurrencyService` — that is the read-side integration this register permits now. |
| 16.2 | Surcharge recovered | `AssetSurchargeRecovery.Amount` | Each collection clears part of that receivable and has no accounting counterpart. `Reference` holds the payroll period or receipt number as free text. |
| 16.3 | Recovery declared to payroll | `AssetSurcharge.RecoveryMethod` / `InstalmentCount` / `RecoveryStartDate`, read through `GET Assets/surcharges/payroll-deductions` | HR **declares**; payroll deducts. The projection exists and nothing consumes it yet — wiring it into a payroll run is the sweep's, not HR's, and payroll is another dev's module. |
| 16.4 | Surcharge waived | `AssetSurcharge.WaiverReason` | Forgiving a balance is a write-off. No GL treatment, no approval-limit rule beyond the workflow definition. |
| 16.5 | Outstanding balance deducted at exit | `AssetSurcharge` → `SeparationClearanceItem.OutstandingAmount` (slice 10) | The link is HR-internal; the settlement that pays it out is unavoidably an accounting event. |
| 16.6 | Rental charged to an employee | `AssetAssignment.RentalAmount` + `RentalCurrencyCode` + `RentalFrequency` + the effective window, read through `GET Assets/payroll/rental-deductions` | ✅ **built, slice 8.** A second employee deduction with no accounting counterpart. HR declares; payroll deducts. The projection deliberately does **not** prorate — it carries the full periodic rate, the window and an `IsPartialPeriod` flag, because HR does not know payroll's period boundaries. |
| 16.6b | Benefit in kind on a subsidised asset | `AssetAssignment.IsBenefitInKind` + `BenefitInKindValue` | ✅ **built, slice 8.** The taxable value of a subsidy — the asset's standard rate less what the employee pays. HR computes the *value*; **assessing tax on it is payroll's** and nothing in HR does it. The sweep should confirm nobody else is already valuing the same benefit. |
| 16.7 | Asset sourced from Finance's fixed-asset register | `CompanyAsset.FixedAssetId` + `AssetSource` | Custody is HR's; depreciation, valuation and disposal accounting stay Finance's (decision D1). The sweep should confirm that a surcharge for a *destroyed* fixed asset does not double-count against Finance's own write-off. |

⚠ **The one question this area raises that no other does:** a surcharge is money owed *by* an
employee, so the sweep must decide whether it is an employee receivable, a payroll deduction, or
both in sequence — and TDC's answer to the payroll-vs-direct-payment question below now has a
second surface depending on it.

⚠ **Two projections, one boundary.** Slices 7 and 8 each expose a read-only line payroll pulls
(`surcharges/payroll-deductions`, `payroll/rental-deductions`). Neither writes a deduction, and the
sweep should decide whether payroll consumes two HR endpoints or one — but *not* by having HR write
into payroll, which is the alternative D2 explicitly rejected.

⚠ **What slice 7 deliberately did NOT build**, so the sweep does not have to unpick it: no
instalment schedule, no deduction run, no payment status machine of its own. HR records the amount,
how it was said to be recovered, and what somebody else actually collected.

### Areas not yet built ⏳

14 awards · 19–23 · 25–27 portals · plus the deferred separation/exit
module, whose **final settlement** is unavoidably an accounting event.

---

## ✅ The posting entry point now EXISTS (recorded 2026-08-28, merge #8)

**This closes the single biggest unknown in the checklist below.** Master `f71d6917` shipped a
formal Finance integration contract, so the sweep no longer has to negotiate a mechanism first.

| | |
|---|---|
| Contract | **FIN-INT-001** — “Approved source transaction → GL”, status **Available**, version 1.1 |
| Entry point | `IFinancePostingEngine.PostAsync(FinancePostingRequestDto, ct)` |
| Also on the interface | `GetReversalPlanAsync(postingEventId, reason, reversalDate, ct)` |
| Registered | `ServiceCollectionExtensions:834` → `FinancePostingEngine`. Callable today, not a plan |
| Catalogue | `docs/Finance/finance-integration-contract-catalogue.md` |
| How to build an adapter | `docs/Finance/finance-integration-adapter-checklist.md` |
| Consumer test template | `docs/Finance/finance-integration-consumer-test-template.md` |
| CI gate | `.github/workflows/finance-integration-gate.yml` — a focused test must be added and its TRX count updated |

`FinancePostingRequestDto` already carries what an HR money event needs: `OriginModuleCode`,
`SourceDocumentType`/`SourceDocumentId`/`SourceDocumentTenantId`/`SourceDocumentReference`,
`PostingDate`, `FiscalPeriodId`, `BookClassification`, `FunctionalCurrencyCode`,
`ExchangeRateTypeOverride`, and — importantly for a reminder-driven module that may retry —
`IdempotencyKey` with `ReturnExistingOnDuplicate`. Reversal is a first-class operation rather
than something HR would have had to model itself.

There is **no module allow-list to be added to**: `OriginModuleCode` is a free string the engine
derives from `SourceModule` when omitted. So nothing blocks HR from being a consumer.

**What this changes in the checklist below:**

- “Confirm the Finance module's posting entry point and who owns it” — **answered**. It is
  `IFinancePostingEngine`, Finance-owned, and the adapter belongs on the HR side of the boundary.
  The rule in §Rules “do not invent an HR-side posting mechanism” is now not merely a caution:
  there is a sanctioned one, so inventing another would be plainly wrong.
- “Cost attribution (project / cost centre dimensions)” — the **mechanism** now exists (master
  shipped Finance coding dimensions and dimension budgets, PRs #103/#112–#115/#120/#121, and
  FIN-INT-001 notes that structured transaction dimensions are additive). **TDC's policy answer
  is still outstanding** — what changed is that we no longer have to design the carrier.

**What has NOT changed:** the deferral itself. The sweep still runs once, after the module, over
the whole register — the argument for that was never the absence of an entry point, it was that
GL posting is one accounting design rather than twenty-seven. Do not start posting per area now
that a callable engine exists.

One caveat worth carrying into the sweep: FIN-INT-011 in the catalogue — *“SH Fund, PF, ESB and
fuel allocation”* — is listed as **owner not defined**, requirements-clarification, version 0.0.
That is HR/payroll-shaped territory with no owner, and the sweep is the moment it gets one.

⚠ **2026-08-31 update:** the Finance owner has since confirmed this in writing — see "Finance
owner's governance message" above — and added that anything Planned/Decision-Required/
Requirements-Clarification (FIN-INT-011 included) needs coordination with them **before** design
work starts, not after.

---

## Before the sweep starts

- [ ] **Migrate Payroll's GL posting off the legacy `IJournalEntryService` calls onto
      `IFinancePostingEngine`.** No longer just this backlog's top recommendation — the Finance
      owner's 2026-08-31 message states plainly that "other modules should not create Finance
      journals or posting records directly," which is exactly what Payroll's current path does.
- [ ] **Raise FIN-INT-011 ("SH Fund, PF, ESB and fuel allocation") with the Finance owner.** It is
      the one catalogue entry that is HR/payroll-shaped and has no assigned owner — coordination is
      required before anyone designs against it.
- [ ] Back-fill the five closed areas above.
- [ ] Settle the **three-way** training double-count: area 7's training budget, succession
      development activities, and `ManpowerBudget.TrainingBudget`.
- [ ] Decide who writes `ManpowerBudget.ActualSpent` and `.Variance` — nothing does today.
- [ ] Decide whether a manpower budget carries a currency at all, and if so which.
- [ ] Get TDC's answer on payroll-vs-direct-payment reimbursement — it now governs **two**
      surfaces: travel claims paid *to* an employee and asset surcharges recovered *from* one.
- [ ] Decide whether an approved asset surcharge is an employee receivable, a payroll deduction, or
      both in sequence (area 16.1–16.3).
- [ ] Get TDC's answer on cost attribution (project / cost centre dimensions).
- [ ] Confirm the Finance module's posting entry point and who owns it — Finance is not this
      module's to modify without agreement, the same rule that governs payroll.
- [ ] Decide the treatment **once**, then apply it across every row in the register.

---

## Area 9b — Separation & final settlement (registered 2026-08-20)

**The largest single money event in HR**, and the one the note above anticipated when it said the
deferred separation module's final settlement "is unavoidably an accounting event". It is now
built: `SeparationSettlements` and `SeparationSettlementLines`.

| Event | Where it is recorded | Direction |
|---|---|---|
| Unpaid salary to the last working day | settlement line, `UnpaidSalary` | payable |
| Notice pay in lieu | settlement line, `NoticePay` | payable |
| Leave encashment on exit (capped 56 days, FR-HR-152) | settlement line, `LeaveEncashment` | payable |
| Gratuity / end-of-service | settlement line, `GratuityOrEndOfService` | payable |
| Benefit and pension-related payments | settlement lines | payable |
| Outstanding loans, salary advances, payroll recoveries | settlement lines, from the clearance form | recoverable |
| **Outstanding travel advances** | settlement line, `TravelAdvanceRecovery`, auto-populated from `StaffTravelAdvances` | recoverable |
| Unreturned property and equipment | settlement line, `PropertyRecovery`, from the clearance form | recoverable |
| Tax and other deductions | settlement lines | recoverable |

**Nothing posts.** The statement records what is payable and what is recoverable, and stops. Net
payable is derived from the lines, in the settlement's own currency.

### What the sweep needs to decide here

- [ ] **Who pays it.** Through payroll as a final run, or as a direct payment from Finance? The
      answer decides whether the settlement posts to payroll control or straight to the bank, and it
      is the same open question as the reimbursement one above — settle both together.
- [ ] **Recoveries that exceed earnings.** A leaver can owe more than they are due; `NetPayable`
      goes negative and the organisation is a creditor. There is no debtor record for an
      ex-employee, and nothing today converts one into a receivable.
- [ ] **Currency.** The settlement is stated in HR's configured default validated against Finance,
      falling back to Finance's base. A travel advance in another currency is deliberately left
      **uncomputed** rather than converted, because Finance owns conversion — and its rates are
      known to be inverted (see the note on `StaffTravelCurrencyBridge`). Fix that before the sweep,
      or every cross-currency recovery will be wrong.
- [ ] **The daily-rate basis.** Monthly × 12 ÷ 365 today, stated on every computed line. If TDC
      computes on a 30-day month or working days, the money differs. Raised in
      `HR-OPEN-QUESTIONS-FOR-TDC.md`.
- [ ] **Timing.** The settlement is finalised, then reviewed by Internal Audit (FR-HR-185), and only
      then paid. The accounting event is the release, not the finalisation — do not post on finalise.

---

## Area 14 — Staff Awards & Recognition (registered 2026-08-21)

Status: ✅ recorded, awaiting the sweep.

An award is money leaving the organisation for a named employee, so it belongs in this register. As
everywhere else in HR, **nothing here posts to the general ledger** — the awards module records what
was promised and what was paid, and the sweep decides what those become.

| Money event | Where it is recorded | Nature |
|---|---|---|
| Award value at conferral | `EmployeeAwards.MonetaryAmount` | payable, committed |
| Award payment | `EmployeeAwards.PaymentProcessed`, `PaymentDate`, `PaymentReference` | payable, settled |
| Amount actually paid | `ProcessAwardPaymentDto.AmountPaid`, when it differs from the award value | payable, settled |
| Long-service award value | `LongServiceAwards.MonetaryAmount`, `PaymentDate`, `PaymentReference` | payable |
| Long-service milestone value | `LongServiceMilestones.MonetaryAmount` — what a rung is worth | reference |
| Award level value | `AwardLevels.MonetaryAmount` — the tier's standard amount | reference |
| Annual award budget | `AwardBudgets.BudgetAmount` | budget |
| Committed but unpaid | `AwardBudgets.ReservedAmount` | budget consumption |
| Paid | `AwardBudgets.SpentAmount` | budget consumption |

### Two things the sweep needs to know about the budget figures

**They are maintained by the awards module for itself, and they are new.** Before area 14 slice 8,
`SpentAmount` and `ReservedAmount` existed and **nothing ever incremented either** — so
`GetAvailableBudgetAsync`, which computes `BudgetAmount - SpentAmount - ReservedAmount`, always
returned the whole budget however many awards had been paid. Slice 8 made conferral reserve and
payment spend. A budget that never depletes is worse than no budget: it looks like a control and is
not one.

**This is bookkeeping, not accounting.** The figures are the awards desk's own view of what it has
committed against a yearly allowance. They are deliberately *not* a parallel ledger, they post
nothing, and the sweep should treat them as a source to reconcile against rather than as a set of
entries to import.

⚠ **An award whose year has no budget row is not refused.** TDC has not said budgets are mandatory,
and refusing to record an award somebody has already been told they won would be inventing a
control. Such an award simply draws against nothing — which means budget consumption is complete
only for award types that have a budget for the year.

### The long-service ladder (added by slice 9)

`LongServiceMilestones` is the ladder HR maintains — one row per number of years per award, holding
what that rung carries. The sweep **copies** the amount onto the award it grants rather than
referencing the rung, so that repricing a rung later cannot restate the value of an award already
conferred. For the sweep, that means: **`LongServiceAwards.MonetaryAmount` is the figure that
matters**, and the milestone row is reference data explaining where it came from.

⚠ **The seeded ladder is unpriced.** A fresh ladder carries years and nothing else, because TDC
has not said what a twenty-year award is worth — see the long-service question in
`docs/HR-OPEN-QUESTIONS-FOR-TDC.md`. Until that is answered, long-service awards are granted with a
**null** amount, which is an unanswered question rather than a value of zero. The sweep should not
read a null here as "no money owed".

### Leave as a reward

`AwardType.LeaveDaysBonus`, `AwardLevel.LeaveDaysBonus` and `LongServiceAward.LeaveDaysBonus` grant
**leave days** rather than money. `LongServiceAward` carries `LeaveProcessed`, `LeaveProcessedDate`
and a `LeaveId` pointing into the leave module. Not a Finance item, but recorded here because it is
the other thing an award can cost, and because `LeaveId` **has no writer** — the hook exists and
nothing fills it. Flagged for area 14's own remaining slices, not for the sweep.
