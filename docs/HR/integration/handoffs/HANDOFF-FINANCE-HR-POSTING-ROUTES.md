# Hand-off to the Finance module owner — HR now posts through FIN-INT-001; four things only Finance can settle

**Raised by:** HR module work, 2026-09-20 (HR finish plan lane 8, slice 1).
**Severity:** not a defect — a coordination note under your 2026-08-31 rule ("coordinate before
implementing anything Planned or Decision Required"). Everything HR built uses the **Available**
boundary only; the questions below are the parts that are Finance's to answer.
**Status:** open. HR's side is built and contract-tested; nothing below blocks it, but each answer
either promotes it or tells HR to change it.

Self-contained; the design is `docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md`.

---

## 1. What HR built, in one paragraph

Five HR money events (medical claim approved / paid, travel claim approved / paid, travel advance
disbursed) post through `IFinancePostingEngine.PostAsync(FinancePostingRequestV2Dto)` with
`SourceModule = OriginModuleCode = "HR"`, one concrete book from your
`FinanceAccountingBookCodeResolver`, deterministic idempotency keys, distinct posting actions per
event, and accounts resolved from an HR-owned role mapping chosen through HR's chart read door —
never hard-coded. Failure rolls the HR action back; reversal is your `ReverseAsync`. Twenty-six
consumer-contract tests (`HrFinancePostingAdapterTests`, `HrFinancePostingCatalogueTests`) use
`FinanceConsumerContractAssertions.ShouldSatisfyPostingContract` and are registered in
`finance-integration-gate.yml` (consumer gate count 35 → 61). HR writes no journal, no posting
record and no Finance table.

## 2. What HR needs from Finance

1. **Producer routes.** `FinanceDimensionRouteCatalog` has one HR route (`hr.payroll.journals`).
   HR posts through the route-less overload today, as Procurement's tender fee does. Please add —
   or tell HR the shape you want —
   `hr.medical.claims` (`MedicalExpenseClaim`), `hr.travel.claims` (`StaffTravelExpenseClaim`),
   `hr.travel.advances` (`StaffTravelAdvance`), all `HR → GL`, with matching
   `FinanceExternalProducerContractId` values. HR then passes the producer context (one line per
   event) and dimension rules become enforceable on HR postings.
2. **The clearing account.** HR's "paid" credits a *Staff payments clearing* role, on the payroll
   model (`1010 Cash and Bank - Payroll Clearing`), and expects Finance's Cash module to clear it
   against the bank. Confirm that is the boundary you want, and whether it should be one clearing
   account or one per payment method (bank transfer / cheque / mobile money / cash). HR can add
   roles; it should not decide this.
3. **FX evidence.** A foreign-currency advance (USD per diem) is valued through
   `ICurrencyService.ConvertAsync` — the same valuation every HR money field uses — and posted as
   functional lines, with the original currency and amount on HR's register row and in the line
   narration. HR did **not** send `ExchangeRateId` because the engine's rate-policy resolution is
   account-scoped and would refuse a rate whose type or quote side differs from policy. If you
   want the FX leg on the journal, say which rate type and quote side HR should request from
   `IExchangeRateService` and HR will pass the record id.
4. **A catalogue row.** Proposed `FIN-INT-017 "HR employee claims and advances → GL"`, producer
   owner HR, Finance owner GL, source types as in (1), status Available once you have reviewed
   the accounts, dates and reversal — the checklist's § 6 review.

## 3. Things to know, not to answer

- **Chart of accounts.** The UAT chart has no staff-claims payable, medical or travel expense
  account; HR's walk-through maps to `2120`, `1120`, `1010`, `6020`, `6000`. Whether to add
  dedicated accounts is yours.
- **Payroll clears what HR skips.** A travel claim paid by payroll offset (or a medical claim by
  salary deduction) is logged *Skipped* by HR; payroll's journal must credit the same staff-claims
  payable account through its `PayrollJournalMapping`. Raised with the payroll owner separately.
- **FIN-INT-011** ("SH Fund, PF, ESB and fuel allocation") is still unowned in your catalogue and
  is payroll-shaped, not HR-shaped: every one of those is a payroll component. HR suggests the
  payroll owner brings the requirement; HR will not design against it.
- **Budget commitment.** HR's four budget surfaces stay HR-side reads. Reusing FIN-INT-015's shape
  for HR is the Planned conversation your rule requires; HR has not called
  `IFinanceBudgetCommitmentService`.
- **A rebuilt UAT database cannot post until Finance's authority is set.** `New-UatDatabase.ps1`
  leaves every account-book mapping disabled, every book non-postable (lifecycle Configuring), no
  book-period rows and the current period closed. HR's harness set those directly to verify
  (design § 5.1); the seed should do it through your governed path, or the demo box needs a
  Finance step before any producer — payroll included — can post.
- **Baseline dependency.** The engine requires `AccountingBookPeriods`; a database migrated up the
  legacy chain lacks it and every producer fails. HR's harness README says so; you may want the
  cutover note to say it for payroll too.

## 4. Evidence

Branch `hrdev`, 2026-09-20: `src/ErpSystem.Core/Services/HR/Finance/*`,
`src/ErpSystem.Api/Controllers/HR/HrFinancePostingController.cs`,
`src/ErpSystem.Data/ApplicationDbContext.HrFinancePosting.cs`,
`tests/ErpSystem.Api.Tests/Services/HR/HrFinancePosting*.cs`, the two wired services
(`MedicalServices.cs` `MedicalExpenseClaimService`, `StaffTravelFinanceService.cs`).
