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
| 2 — Leave | leave encashment on separation (FR-HR-152 caps it) | 🔲 to record |
| 4 — Compensation | pay components, allowances, the payroll boundary | 🔲 to record |
| 7 — Training | training budget, costs, vendor payments, `costPerCompletion` | 🔲 to record |
| 11 — Medical | claim create → approve → **pay**; insurance utilisation; NHIS | 🔲 to record |
| 10 — SHE | any compensation or remediation spend | 🔲 to record |

⚠ **Area 11 is the priority back-fill** — it has a live, working claim→approve→**pay** path, so it
is the closest analogue to travel 12.1 and the two must post the same way.

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
  requisition. Decide once whether the manpower budget is the parent envelope that the others draw
  down from, or a parallel plan.

### Cleanup owed at finalization (area 17/18, decision D-6)

Harness residue that will appear in any register or report until it is removed, both created by
**area 6**, not by area 17:

- **43 job descriptions** titled `E2E RecB Engineer …` / `E2E RecD Engineer …`, all Draft, all
  childless.
- **Ten salary grades** named `E2E RecD Band 141309` and similar, sitting alongside the two real TDC
  grades (`M1 General Managers`, `M2 Heads of Department`). ⚠ These are the more damaging of the
  two: a salary grade appears in pay-related pickers.

### Areas not yet built ⏳

14 awards · 16 assets · 19–23 · 25–27 portals · plus the deferred separation/exit
module, whose **final settlement** is unavoidably an accounting event.

---

## Before the sweep starts

- [ ] Back-fill the five closed areas above.
- [ ] Settle the **three-way** training double-count: area 7's training budget, succession
      development activities, and `ManpowerBudget.TrainingBudget`.
- [ ] Decide who writes `ManpowerBudget.ActualSpent` and `.Variance` — nothing does today.
- [ ] Decide whether a manpower budget carries a currency at all, and if so which.
- [ ] Get TDC's answer on payroll-vs-direct-payment reimbursement.
- [ ] Get TDC's answer on cost attribution (project / cost centre dimensions).
- [ ] Confirm the Finance module's posting entry point and who owns it — Finance is not this
      module's to modify without agreement, the same rule that governs payroll.
- [ ] Decide the treatment **once**, then apply it across every row in the register.
