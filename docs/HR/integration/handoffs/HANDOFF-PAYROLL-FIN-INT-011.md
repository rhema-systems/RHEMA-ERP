# Hand-off to the payroll and Finance owners — FIN-INT-011 ("SH Fund, PF, ESB and fuel allocation")

**Raised by:** HR module work, 2026-09-21 (HR finish plan lane 8, slice 6).
**Severity:** not a defect — an ownership note. The Finance integration catalogue lists FIN-INT-011
with no assigned producer owner; HR is recording that it is not HR's, and why.

## 1. What FIN-INT-011 is, as the catalogue words it

The statutory and provident funds (SSNIT / SH Fund, PF), the end-of-service benefit (ESB) and the
fuel allocation are **payroll-computed amounts**: they arise inside a payroll run, per employee, per
period, and are settled by payroll's own journal (route `HrPayrollJournal`, already on
`IFinancePostingEngine`) and payroll's own remittances.

## 2. Why HR does not design against it

The HR ↔ Finance sweep (2026-09-20 → 21, `docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md`) walked
every HR money surface. None of these four amounts lives in an HR field:

| Amount | Where it is computed | HR's relationship |
|---|---|---|
| SH Fund / SSNIT | payroll run (statutory deductions) | HR supplies the employee master and the pay-component *assignments*; the amount is payroll's |
| Provident fund | payroll run | same — HR's `EmployeePayComponent` is the authorisation, not the figure |
| ESB (end of service) | payroll's final run; HR's separation settlement carries a **gratuity** line that posts to Separation expense (slice 3) | the settlement's gratuity is HR's; an ESB scheme accrual is payroll's / Finance's |
| Fuel allocation | a pay component / allowance in payroll | HR assigns it; payroll pays and journals it |

Posting any of them from HR would journal payroll's numbers a second time — the same reasoning
that kept `TrainingBudgetTransaction` and `SuccessionDevelopmentActivity.ActualCost` out of the HR
register (design § 3.1e / § 3.1f).

## 3. What HR asks

1. **Assign FIN-INT-011's producer owner to payroll** in the catalogue, with Finance as consumer.
2. If Finance wants an *accrual* for ESB separately from payroll's run (a balance-sheet liability
   built up over service), say so: the only HR-side input would be service dates and grade, which
   HR already exposes read-only to payroll (`docs/HR/integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md`).
3. Nothing else. HR holds no field it would post under this contract.

## 4. Evidence

- `FinanceIntegrationContractCatalog.cs` (FIN-INT-011 row, no producer owner).
- `docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md` § 3 (the 26 HR events; none carries these amounts).
- `docs/HR/integration/HR-FINANCE-ENTITY-SWEEP.md` rows 10, 11, 57 (compensation is authorisation, not payment).
- Payroll's route `HrPayrollJournal` in `FinanceDimensionRouteCatalog.cs` (payroll already posts through the engine).
