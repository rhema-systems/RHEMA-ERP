# Hand-off to the Payroll module owner — an approved claim "paid through payroll" reaches nobody

**Raised by:** HR module work, 2026-10-04 (staff travel final closure, lane 9, slice 9d; decision D-10).
**Severity:** no data loss today, but a real gap: payroll has no way to receive a one-off amount HR owes an employee.
Travel has stopped offering the option; medical still offers its equivalent.
**Status:** open. Nothing in payroll is asked to change until you decide the first question below.

This is a self-contained report — nothing in it requires reading HR's plans or code.

HR is following the rule recorded in `docs/HR/README.md`: **payroll is another module's; HR reads it and never writes
it.** Two HR screens let an officer settle an employee's approved claim "through payroll". Neither sends anything to
payroll, so the claim read *Paid* while nobody paid the employee. HR has switched the travel one off and is asking you
whether payroll wants to receive these at all.

---

## 1. What happened, and what HR has done

**Staff travel.** A travel expense claim (`StaffTravelExpenseClaim`) is paid by an HR officer, who chooses a payment
method. One of them was **Payroll offset** (`TravelPaymentMethod.PayrollOffset = 2`). Choosing it marked the claim
*Paid* and stopped there: no reader of that value exists outside HR's Finance posting, so the employee was never paid
(HR finding O-6).

Since 2026-10-02 (travel closure lane 3, decision D-10):
- the pay dialog no longer offers *Payroll offset*;
- the API refuses it: *"Payroll cannot receive travel claims yet, so a claim paid by payroll offset would reach nobody.
  Pay it by bank transfer, cash, cheque or corporate card."* (`StaffTravelFinanceService.PayClaimAsync`);
- a claim already paid that way keeps its value. **On UAT none has been** — the only paid claims are bank transfers.

**Medical — the same gap, still open.** A medical expense claim (`MedicalExpenseClaim`) can be paid by **Salary
deduction** (`PaymentMethod.SalaryDeduction = 6`), and the medical claim page's pay dialog still offers it. Despite its name,
it means the same thing: the approved amount is meant to reach the employee through payroll. Nothing sends it there.
On UAT no claim has used it yet (one paid medical claim, by bank transfer). HR will decide separately whether to switch
it off the way travel did. One payroll intake would serve both.

**The accounting is already designed.** For either method, HR's Finance posting is written so that payroll's journal
does the paying: HR records the expense and the amount owed to the employee (*staff claims payable*), and when the
claim is "paid through payroll" HR posts nothing more — except, for travel, the part of the claim set off against an
advance the employee still held. Payroll's journal is expected to clear the same *staff claims payable* account through
its `PayrollJournalMapping`. What is missing is the step before that: **the amount reaching a payslip.**

---

## 2. What payroll has today (read 2026-10-04)

No intake for a one-off amount owed to an employee. The nearest shapes, none of which fits:
- **Bonus, back-pay and promotion arrears** (`PayrollBonus*`, `PayrollBackpay*`, `PayrollPromotionArrearsEntry`) — each
  its own policy and rules, for its own purpose.
- **`PayrollEmployeeComponent`** — a standing per-employee setting for a component (amount, rate, tax overrides, effective
  dates), not one item per claim.
- **`PayrollImportBatch` / `PayrollImportRow`** — reconciles legacy staff numbers to employees; carries no amounts.

HR will not write into any of these. The rule above forbids it, and each one means something else.

---

## 3. What HR would send, if payroll wants it

One item per claim, when HR's officer chooses "through payroll" on an approved claim:

| What | Where HR keeps it | Note |
|---|---|---|
| Reference | `ClaimNumber` (unique per tenant) | e.g. `EXP-2026-00005`; a medical claim has its own number |
| Employee | `EmployeeId` (HR's `Employees.Id`) | payroll finds its own profile from it |
| Amount to pay | `NetPayable` — the approved total less any advance already set off (`TotalApproved`, `AdvanceDeducted`) | medical: `AmountApproved` |
| Currency | `CurrencyCode` | a trip abroad can be claimed in another currency — see question 3 |
| What it was for | the trip's number and destination, or the medical claim's number and treatment | for the payslip line |
| When | the date HR's officer released it | HR suggests the next open pay period takes it |

And what HR would need back:
- **the pay period (or run) that paid it**, so the claim reads *Paid* only once it has been — not when it was sent;
- **a way to withdraw an item a run has not yet paid**, because travel lets an officer void a payment made in error.

HR would call payroll's own service for both, never its tables.

---

## 4. What HR is asking for

1. **Decide whether claims should be paid through payroll at all.** If TDC pays travel and medical claims by bank
   transfer, the answer may simply be *no*: travel's refusal stays, medical's option is switched off the same way, and
   this hand-off closes with no work on your side. Please say which.
2. **If yes, name the intake.** A shape for "a one-off amount to pay this employee in the next period", with a reference
   back and a way to withdraw it; HR will build its side against whatever you publish.
3. **Currency and tax are yours to rule.** A claim may be in a foreign currency — say whether payroll converts it (at
   which date's rate) or wants HR to send the base-currency amount at Finance's rate. Whether a reimbursed business
   expense is taxable pay is for you and TDC; HR has no position to impose.

Not asked: recovering an overdue travel advance by salary deduction. Travel recovers advances through expense claims,
cash returned, a write-off, or — since 2026-10-04 — the leaver's final settlement; it does not ask payroll for a
deduction.

Contact: the HR module owner. Related: `docs/HR/areas/travel/HR-STAFF-TRAVEL-FINAL-CLOSURE-PLAN.md` (finding O-6,
decision D-10, lane 9); `docs/HR/integration/handoffs/HANDOFF-FINANCE-HR-POSTING-ROUTES.md` § 3, *"Payroll clears what HR
skips"*, which said this would be raised with you — this is that note;
`docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` #37.
