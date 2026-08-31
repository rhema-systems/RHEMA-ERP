# HR ↔ Finance Entity Sweep — Comprehensive Reference

**Generated:** 2026-08-31
**Scope:** Every entity under `src/ErpSystem.Core/Entities/HR/**` (33 sub-areas, 180+ entity
classes) reviewed for financial implications, cross-referenced against the Finance module's
actual capabilities (`src/ErpSystem.Core/Entities/Finance/**`).
**Purpose:** A single, entity-level catalogue that can be picked up in any future chat session
without re-deriving the sweep from scratch.

---

## 0. How this document relates to what already exists

Two Finance-integration documents already exist in this repo. **This document does not replace
either — it is the entity-level map that sits underneath them.**

| Document | What it is | Relationship to this file |
|---|---|---|
| [`docs/HR-FINANCE-INTEGRATION-BACKLOG.md`](../HR-FINANCE-INTEGRATION-BACKLOG.md) | The **living decision register** opened 2026-08-17. Records money events *as each HR area is built*, tracks open TDC decisions, and states the deferral policy (HR does not post to GL per-area; one sweep does it after the whole module is built). | Authoritative for **decisions already made** and **open questions**. This document is authoritative for **entity-level coverage** — it sweeps every HR entity, not just the ones already registered, so it can be used to spot what the backlog has not yet recorded. |
| [`docs/Finance/finance-integration-contract-catalogue.md`](../Finance/finance-integration-contract-catalogue.md) | The **mechanism catalogue** — which Finance boundaries are callable (`IFinancePostingEngine`, budget commitment services, etc.), their status (Available/Planned/Decision required). | Authoritative for **how** a posting happens once decided. This document references contract IDs (FIN-INT-00x) per entity group so the two can be read together. |
| `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` | Business-policy questions for the client (TDC), some financial (final-settlement daily-rate basis, medical claim payment routing). | Cross-referenced where relevant; not duplicated. |

**Read this document when:** you need to know *which HR entity* carries money, *what kind* of
money event it is, and *what Finance surface* it should eventually reach. **Read the backlog when:**
you need to know what has already been decided/recorded and what is still open. **Read the
contract catalogue when:** you need to know what Finance API to call once the decision is made.

---

## 1. Architecture snapshot (why every row below points at the same few mechanisms)

The Finance module exposes a **small number of stable entry points**, and every HR money event in
this sweep resolves to one of them:

| Mechanism | Entry point | Used for |
|---|---|---|
| **General-ledger posting** | `IFinancePostingEngine.PostAsync(FinancePostingRequestDto)` — contract **FIN-INT-001**, status **Available** | Any approved source transaction → GL (salary journals, benefit-in-kind, awards paid, settlements paid, surcharge recoveries, etc.) |
| **Reversal** | `IFinancePostingEngine.GetReversalPlanAsync(postingEventId, reason, reversalDate)` | Correcting a posted HR-originated journal (payroll rerun, settlement correction) |
| **Budget commitment** | `IFinanceBudgetCommitmentService` (pattern established for Procurement, FIN-INT-015) | Reserve/consume against an adopted Finance budget cell — **no HR equivalent exists yet**; `ManpowerBudget`, `TrainingBudget`, `AwardBudget` are today HR's own bookkeeping, not Finance budget cells |
| **Fixed assets (read-only)** | `FixedAsset` entity, reached via `CompanyAsset.FixedAssetId` | HR reads Finance's capitalization/depreciation/NBV; HR never writes it (decision D1) |
| **Accounts Payable** | `VendorInvoice` / `Supplier` (Procurement/Finance) | Payments to external parties HR deals with — training vendors, insurers, healthcare facilities, unions — **no HR entity is wired to this today** |
| **Currency / exchange rate (read-only, not deferred)** | `ICurrencyService`, Finance `Currency`/`ExchangeRate` | Master data. HR must read these, never keep a parallel copy — already done in a few places (Succession, Asset surcharges), missing in most others (Travel, ManpowerBudget) |

`FinancePostingRequestDto` (the FIN-INT-001 payload) already carries everything an HR-originated
transaction needs: `OriginModuleCode`, `SourceDocumentType/Id/TenantId/Reference`, `PostingDate`,
`FiscalPeriodId`, `BookClassification`, `FunctionalCurrencyCode`, `IdempotencyKey` +
`ReturnExistingOnDuplicate`, and structured transaction **Dimensions** (cost centre / project /
department) — additive, so HR's eventual cost-centre attribution question has a carrier already
built, even though **the policy answer (which dimension HR should use) is still open**.

**The one HR consumer that already exists:** Payroll. It calls the *legacy* `IJournalEntryService`
directly (not `IFinancePostingEngine`) via `PostPayrollJournalAsync` in
[`PayrollService.cs`](../../src/ErpSystem.Api/Services/HR/PayrollService.cs), routed through
[`PayrollJournalMapping`](../../src/ErpSystem.Core/Entities/HR/Payroll/PayrollEntities.cs#L668) (a
config table of `TransactionType`/`ComponentCode` → `AccountCode`/`DebitCredit`). This is the
**one working precedent** for how an HR money event becomes a journal — and the one the sweep
should either extend to `IFinancePostingEngine` or use as the template for everything else.

**⚠ Governance update (2026-08-31).** The Finance module owner has now stated the ownership rule
below explicitly, in writing, on merging the posting-engine foundation (PRs #70/#72): *"operational
modules retain ownership of their source transactions and approvals. Finance owns account
resolution, fiscal-period controls, currency, tax and subledger effects, journal creation, audit,
reversal and idempotency. Other modules should not create Finance journals or posting records
directly."* Payroll's legacy path above is exactly what this now forbids, so **migrating it to
`IFinancePostingEngine` (row 15 below) is a compliance fix, not merely the highest-leverage
improvement.** The Finance owner has also asked to be **coordinated with before anything marked
Planned or Decision Required is implemented** (the catalogue's FIN-INT-011 is HR/payroll-shaped
and ownerless — HR should raise it rather than wait), and for every integration to ship with the
reusable consumer-contract tests (`docs/Finance/finance-integration-consumer-test-template.md`,
assertion helper at `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConsumerContractAssertions.cs`)
and follow `docs/Finance/finance-integration-adapter-checklist.md` at design time. Full quote and
consequences: `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` → "Finance owner's governance message."

---

## 2. Master table — every HR entity with financial implications

Legend — **Direction**: `EXP`=expense/cost to the org, `PAY`=payable to a person/party,
`REC`=receivable from a person, `BUD`=budget/authorization (not a movement of cash),
`REF`=reference/pricing data (no cash effect itself, but feeds one). **Status**: ✅ posts to
Finance today · 🟡 calculated in HR, no Finance hand-off · ⚪ pure reference/config · 🔲 not yet
built.

| # | Area | Entity | Money event | Dir. | Proposed Finance target | Status |
|---|---|---|---|---|---|---|
| 1 | Org Structure | `Department.Budget`, `.AccountCode` | Departmental budget & GL cost-centre code | BUD/REF | Finance `BudgetEntry` / GL account dimension | 🟡 stored, not linked |
| 2 | Org Structure | `Division.AccountCode`, `OrganizationUnit.AccountCode` | GL cost-centre/account tagging | REF | GL account/cost-centre dimension | 🟡 stored, not linked |
| 3 | Org Structure | `Team.CostCenterCode` (if present) | Cost-centre tag for cross-functional teams | REF | GL cost-centre dimension | 🟡 stored, not linked |
| 4 | Job Architecture | `JobDescription.RoleIntrinsicValue`, `.IndustryBenchmarkSalary`, `.EstimatedSalaryLow/High`, `.FinancialAuthorityLimit` | Role pricing / spend-authority ceiling | REF | None (pricing input to grading, not a transaction) | ⚪ reference only |
| 5 | Job Architecture | `ManpowerBudget.SalaryBudget/.BenefitsBudget/.RecruitmentBudget/.TrainingBudget/.TotalBudget` | **Largest single HR money surface** — annual headcount+cost authorization per org unit | BUD | Finance Budget module (no contract exists yet — nearest analogue FIN-INT-015, built for Procurement) | 🟡 HR-only, no currency field, no Finance link |
| 6 | Job Architecture | `ManpowerBudget.ActualSpent`, `.Variance` | Budget-vs-actual variance | BUD | **Must be written from Finance GL actuals** | 🔲 **no writer anywhere — permanently zero today** |
| 7 | Job Architecture | `ManpowerBudgetLine.PlannedTotalCost/.PlannedAverageSalary/.CurrentTotalCost/.CurrentAverageSalary` | Per-position cost build-up | BUD/REF | Rolls into row 5 | 🟡 HR-only |
| 8 | Recruitment | `JobVacancy.SalaryRangeMin/Max`, `.SalaryCurrencyCode` | Advertised compensation range | REF | None (posting copy, not a transaction) | ⚪ reference only |
| 9 | Recruitment | `JobAnalysis`-level `RecruitmentBudget` (on `ManpowerBudget`) vs. per-requisition recruitment cost | Overlap risk — same spend possibly counted twice | BUD | Needs a single owner decided in the sweep | ⚠ unresolved overlap |
| 10 | Compensation | `SalaryGrade/SalaryLevel/SalaryNotch` (min/mid/max/notch amounts) | Pay-band reference data | REF | Feeds Payroll's salary journals indirectly | ⚪ reference only |
| 11 | Compensation | `PayComponent.DefaultAmount`, `PositionPayComponent.Amount`, `EmployeePayComponent.Amount` | Resolved allowance/deduction amounts feeding gross pay | EXP | Payroll salary journal via `PayrollJournalMapping` | 🟡 feeds Payroll, not itself posted |
| 12 | Payroll Core | `PayrollComponent` (Amount/Rate/TaxFreeCeiling/etc.), `PayrollTaxBand`, `PayrollTaxRelief`, `PayrollPensionScheme`, `PayrollOvertimePolicy` | Statutory & pay calculation configuration | REF | Drives the amounts posted, not posted itself | ⚪ configuration |
| 13 | Payroll Core | `PayrollBudgetAnalysisRow` | Payroll budget-vs-actual worksheet (legacy Oracle carry-over) | BUD | Same overlap concern as `ManpowerBudget` — **two payroll-adjacent budget surfaces exist** | ⚠ unresolved overlap with row 5 |
| 14 | Payroll Core | **`PayrollJournalMapping`** | Config: `TransactionType`/`ComponentCode` → GL `AccountCode`/`DebitCredit` | REF | **This *is* the HR→GL bridge** | ✅ implemented, drives row 15 |
| 15 | Payroll Core | Payroll run → `PostPayrollJournalAsync` | Salary, tax, SSF, employer-contribution journal per pay run | EXP/PAY | GL via legacy `IJournalEntryService` — **not yet migrated to `IFinancePostingEngine`** | ⚠ posts, but on a path the Finance owner has since confirmed is out of policy (2026-08-31) |
| 16 | Payroll Loans | `PayrollLoan`, `.OutstandingBalance`, `PayrollLoanSchedule` | Staff loan disbursed & repaid via payroll deduction | REC | Employee receivable — **no Finance AR record exists**, balance lives only in HR/payroll | 🟡 HR-only ledger |
| 17 | Payroll Loans | `PayrollLoanPolicy` (`MaxLoanAmount`, `InterestRatePercent`, `MaxDebitRatioPercent`) | Loan eligibility & interest rules | REF | Feeds row 16 | ⚪ configuration |
| 18 | Payroll Advances | `PayrollSalaryAdvance.AdvanceAmount` | Salary advance disbursed, recovered next pay run | REC | Employee receivable — same gap as row 16 | 🟡 HR-only ledger |
| 19 | Payroll Bonus | `PayrollBonusPolicy/.Rule/.Exception` (`Amount`, `TaxFreeCeiling`, `TaxRate`) | Bonus computation & tax treatment | EXP | Posted via `PayrollJournalMapping` (row 14) at run time | 🟡 calculated, posts via row 15 |
| 20 | Payroll Backpay | `PayrollBackpayPolicy/.Rule/.Exception` (`Amount`, `MinimumServiceValue`) | Retroactive pay adjustment | EXP | Same as row 19 | 🟡 calculated, posts via row 15 |
| 21 | Payroll Disbursement | `EmployeeBankDetail`, `BankSchedule`/`TaxSchedule`/`PensionSchedule` (report types) | Payment instruction artifacts (bank file, tax authority remittance, pension remittance) | PAY | **Report export only — no Finance cash/AP hand-off; actual disbursement is manual today** | 🟡 generated, not wired |
| 22 | Leave | `LeaveType.EncashmentRateBasis/.EncashmentRatePerDay/.EncashmentWorkingDaysPerMonth` | Cash-conversion pricing rule for unused leave | REF | Feeds row 23 | ⚪ configuration |
| 23 | Leave | `LeaveEncashment.DaysEncashed`, `.AmountPaid` | Leave cashed out mid-service | EXP/PAY | Should post like a payroll component — **currently no GL hand-off recorded** | 🟡 HR-only, back-fill owed (backlog area 2) |
| 24 | Leave | `LeaveBalance.EncashedDays` | Running total feeding a leave liability | REF | Basis for a **leave-liability accrual** Finance may want on the balance sheet — not modelled anywhere today | 🔲 not modelled |
| 25 | Benefits | `BenefitPolicy` (`EmployeeContribution`, `EmployerContribution`, `CoverageLimit`, `FlatValue`, `ValuationRate`, `ValuationCap`, `TaxExemptThreshold`) | Benefit valuation & tax-treatment configuration | REF | Feeds row 26 | ⚪ configuration |
| 26 | Benefits | `EmployeeBenefitEnrollment` (`AssessedValue`, `TaxableValue`, `EmployerContribution`, `EmployeeContribution`, `UtilizedAmount`) | Per-employee benefit valuation, incl. Ghana GRA Benefit-in-Kind | EXP | Feeds payroll gross-up (`PayComponent.AffectsGrossPay`); **no direct GL posting of employer contribution cost** | 🟡 calculated, not posted |
| 27 | Benefits | `BenefitUtilization.Amount` (claim/expense/adjustment against a benefit's coverage limit) | Benefit claim paid to/for an employee | PAY | Same gap as Medical claims (row 30) — **payroll-vs-direct-payment decision not yet made** | 🟡 HR-only |
| 28 | Benefits | `BenefitBeneficiary.Percentage` | Life/provident payout allocation | REF | Only matters on a death/maturity claim (out of HR scope) | ⚪ reference only |
| 29 | Medical | `MedicalInsurancePlan` (Annual/Lifetime/Outpatient/Inpatient/Dental/Optical/Maternity limits, `MonthlyPremium`, `AnnualPremium`, contribution %s) | Insurance plan pricing | REF | Feeds row 30; premium is a payable to the **insurer**, who is not modelled as a Finance vendor | ⚪ configuration, ⚠ payee gap |
| 30 | Medical | `MedicalExpenseClaim`, `EmployeeMedicalInsurancePolicy.UtilizedAmount` | Claim create → approve → **pay** (a *live, working* payment path) | PAY | **Priority back-fill per the backlog — closest analogue to Travel 12.1, must post the same way** | 🟡 HR-only, flagged priority |
| 31 | Medical | `HealthcareFacility`/`MedicalInsuranceProvider` (`BankId`, `BranchId`, `AccountNumber`, `AccountName`) | External parties HR pays or is paid through | PAY | These look like **AP payees/vendors** — no link to Finance `Supplier`/`BusinessPartner` exists | 🔲 not modelled |
| 32 | Awards | `AwardBudget` (`BudgetAmount`, `SpentAmount`, `ReservedAmount`) | Annual award budget & consumption (now correctly decremented as of slice 8) | BUD | Own bookkeeping — **not a Finance budget cell**; reconcile against, don't import as-is | 🟡 HR-only, internally consistent since slice 8 |
| 33 | Awards | `AwardType.MinMonetaryAmount/MaxMonetaryAmount`, `AwardLevel.MonetaryAmount` | Award value reference/tier pricing | REF | Feeds row 34 | ⚪ configuration |
| 34 | Awards | `EmployeeAward.MonetaryAmount`, `.PaymentProcessed/.PaymentDate/.PaymentReference` | Award conferred & paid to an employee | PAY | Payable — **no GL posting**; payroll-vs-direct-payment question applies here too | 🟡 HR-only |
| 35 | Awards | `LongServiceAward.MonetaryAmount`, `LongServiceMilestone.MonetaryAmount` | Long-service milestone payout (ladder is currently **unpriced** pending TDC) | PAY | Same as row 34 | 🟡 HR-only, values often null |
| 36 | Assets | `CompanyAsset.PurchaseCost`, `.InsuredValue` | Asset acquisition & insurance value (HR-created assets only) | REF | If `Source=FinanceLinked`, Finance's `FixedAsset` is authoritative (decision D1) — HR never writes capitalization | ⚪ reference, read-only where linked |
| 37 | Assets | `CompanyAsset.IsRentable`, `.StandardRentalAmount`, `AssetAssignment.RentalAmount/.RentalCurrencyCode/.RentalFrequency` | Rent charged to an employee for a subsidised/company asset (e.g. vehicle) | REC | HR declares via `GET Assets/payroll/rental-deductions`; **payroll deducts, nobody posts to GL** | 🟡 built (slice 8), no GL hand-off |
| 38 | Assets | `AssetAssignment.IsBenefitInKind`, `.BenefitInKindValue` | Taxable value of a subsidised asset | EXP/REF | HR computes the value; **payroll is responsible for taxing it** — confirm no double-valuation with Benefits (row 26) | 🟡 built (slice 8), cross-check owed |
| 39 | Assets | `AssetSurcharge.AssessedAmount`, `.BasisRepairCost/.BasisReplacementCost` | Employee charged for damaged/lost company property | REC | Employee receivable — **the one place HR is the creditor, not the payer** | 🟡 HR-only, no Finance AR record |
| 40 | Assets | `AssetSurchargeRecovery.Amount` | Collection against a surcharge (often via payroll) | REC | Clears the HR-side receivable; **no GL counterpart** | 🟡 HR-only |
| 41 | Assets | `AssetSurcharge.WaiverReason` | Surcharge forgiven | REC | A write-off — **no GL treatment, no approval-limit rule beyond workflow** | 🔲 not modelled |
| 42 | Discipline | `StaffDisciplinaryActionType.DefaultFineAmount`, `StaffDisciplineFine.FineAmount/.FinePaidAmount` | Disciplinary monetary fine | REC | Employee receivable/deduction — not registered in the Finance backlog at all today | 🔲 **not yet in the register — new finding** |
| 43 | Training | `TrainingVendor.DefaultDailyRate`, `TrainingProgram.CostPerParticipant` | External training pricing | REF | Feeds row 44; vendor payment implies an **AP** relationship — vendor not modelled as Finance `Supplier` | ⚪ configuration, ⚠ payee gap |
| 44 | Training | `TrainingBudget.BudgetAmount/.SpentAmount`, `TrainingBudgetTransaction.TransactionAmount` | Departmental training budget & consumption | BUD | Own bookkeeping — **overlaps `ManpowerBudget.TrainingBudget` and Succession's development-activity cost (three-way double-count, backlog flags this explicitly)** | 🟡 HR-only, unresolved overlap |
| 45 | Training | `TrainingNomination.ActualCost`, `.EmployeeContribution` | Cost of sending one employee on one course | EXP | Rolls into row 44 | 🟡 HR-only |
| 46 | Training | `TrainingServiceBond.BondAmount`, `.RepaymentAmount` | Recoverable bond if an employee exits before the bonded period ends | REC | Employee receivable on early exit — same shape as Assets surcharge (row 39) and Loans (row 16); **no GL hand-off** | 🟡 HR-only |
| 47 | Succession | `SuccessionDevelopmentActivity.EstimatedCost/.ActualCost` + `CurrencyCode` | Cost of developing a named successor (training/secondment/certification) | EXP | **Currency IS validated against Finance's `ICurrencyService`** (slice 5) — the one area doing the read-side integration correctly; GL posting still deferred | 🟡 read-side integrated, posting deferred |
| 48 | Travel | `StaffTravelExpenseClaim` (`TotalClaimed/.TotalApproved/.TotalRejected/.AdvanceDeducted/.NetPayable`) | Expense claim reimbursed to an employee | PAY | **Zero references to GLAccount/cost-centre/Payroll/BudgetEntry/SupplierId across all 34 travel entities** — the clearest gap in the whole sweep | 🟡 HR-only, arithmetic correct (slice 4), no Finance hand-off |
| 49 | Travel | `StaffTravelAdvance` (`RequestedAmount/.ApprovedAmount/.SettledAmount/.UnsettledAmount`) | Cash advance disbursed before travel | REC | Employee receivable — same gap as Loans (row 16) | 🟡 HR-only |
| 50 | Travel | `StaffTravelExpenseClaimLine` (`AmountOriginal/.CurrencyOriginal/.ExchangeRate/.AmountBaseCurrency`) | Per-line foreign-currency conversion | REF | **`ExchangeRate` is caller-supplied, not read from Finance** — a master-data gap the "read now" rule (§0) says should already be fixed | 🔲 master-data gap |
| 51 | Travel | `StaffTravelBudget` (per-trip envelope) | Trip budget committed/consumed | BUD | No link to `BudgetEntry`/cost-centre — legitimately travel-owned breakdown, but must **consume from** the department's Finance budget | 🟡 HR-only |
| 52 | Travel | `StaffTravel{Flight,Hotel,GroundTransport,CarRental}Booking` (`EstimatedCost`/`ActualCost`) | Booking-level cost commitment | BUD | Rolls into row 51 | 🟡 HR-only |
| 53 | Travel | `StaffTravelPerDiemRate` (`DailyAllowance/.AccommodationLimit/.MealAllowance/.IncidentalAllowance`) | Per-diem policy pricing | REF | Feeds rows 48–52 | ⚪ configuration |
| 54 | Separation | `SeparationSettlementLine.Amount` across categories (`UnpaidSalary`, `NoticePay`, `LeaveEncashment`, `GratuityOrEndOfService`, benefits/pension payables, loan/advance/travel-advance/property recoveries, tax deductions) | **The single largest money event in HR** — full exit settlement | PAY/REC | Net payable computed; **nothing posts**. Payroll-vs-direct-payment question applies here too | 🟡 HR-only, "correct arithmetic, no accounting" (same shape as Travel) |
| 55 | Separation | `SeparationSettlement.DailyRateBasis` (currently monthly × 12 ÷ 365) | Values notice pay & leave encashment | REF | Policy answer outstanding — see `HR-OPEN-QUESTIONS-FOR-TDC.md` | ⚠ open TDC question |
| 56 | Separation | `SeparationClearanceItem.OutstandingAmount` (+ `OutstandingCurrencyCode`) | Loans/advances/surcharges/unreturned property rolled into the exit settlement | REC | Feeds row 54; provenance links (`SourceAssignmentId`, `SourceSurchargeId`) already correct | 🟡 HR-only |
| 57 | Promotion/Transfer | `StaffMovement.CurrentSalary/.NewSalary/.SalaryIncreaseAmount/.SalaryIncreasePercentage` | Compensation change on a career move | EXP/REF | Authorization event, not a payment itself — feeds Payroll's next run via `EmployeePayComponent`/`Employee.Salary` | ⚪ reference/authorization |
| 58 | Company Schedule | `CompanyEvent.HasBudget/.BudgetAmount/.ActualCost/.BudgetCode` | Company event/training-day budget | BUD | `BudgetCode` suggests a GL tie that is not actually wired | 🟡 HR-only |
| 59 | Union/CBA | `CollectiveBargainingAgreement` (financial terms embedded in `Summary`/document, not structured fields) | Negotiated pay/benefit terms | REF | Feeds Payroll/Benefits configuration manually; no structured monetary fields to post | ⚪ reference only |
| 60 | Safety, Health & Environment | `SafetyIncident.ClaimAmount`, `.AmountPaid`, `.ClaimApproved`, `.InsuranceClaimFiled`, `.InsuranceProviderId` (FK to `MedicalInsuranceProvider`) | Insurance claim filed and paid against a workplace safety incident | PAY | Same claim-payment shape as Medical claims (row 30) — **a second, independent instance of the same unmodelled payment path**, and `SafetyIncidentInvolvedPerson.MedicalExpenseClaimId` cross-links straight into Medical's own claim entity, so the two should not be solved separately | 🟡 HR-only, added 2026-08-31 |
| 61 | Safety, Health & Environment | `SafetyEquipmentMaintenance.Cost`, `PpeInventory.UnitCost` | Equipment upkeep and PPE stock cost | EXP/REF | Operational cost tracking, no GL posting found | 🟡 HR-only |
| 62 | Safety, Health & Environment | `SheSustainabilityInitiative.EstimatedCostSavings`, `SheMonthlyEnvironmentalReport.SustainabilityCostSavings` | Estimated/realized cost savings from sustainability initiatives | REF | Reporting figure, not a transaction — feeds the monthly environmental report, nothing to post | ⚪ reference only |
| 63 | Safety, Health & Environment | `SheContractorNonCompliance.SanctionApplied` (enum incl. `FineIssued`) | Labelled sanction on a non-compliant contractor | REF | **Confirmed: no amount field, no payment record, no procurement-side consequence exists anywhere** — the label alone carries no money; if TDC expects a fine to actually be charged, that is unbuilt, not merely unposted | 🔲 not modelled |

---

## 3. Narrative detail by functional area

### 3.1 Payroll — the one area with a working (if legacy) bridge to Finance

Payroll is the **only** HR sub-domain that currently posts anything to the general ledger, via:

```
Payroll run → PayrollJournalMapping (TransactionType/ComponentCode → AccountCode/DebitCredit)
            → PayrollJournalLine (per employee, per component)
            → IJournalEntryService.CreateJournalEntryAsync() + PostJournalEntryAsync()
            → Finance JournalEntry (legacy path)
```

Five GL accounts are auto-seeded for this (`EnsurePayrollFinanceAccountsAsync`):

| Account | Type | Purpose |
|---|---|---|
| 1010 | Asset | Payroll clearing bank account |
| 1120 | Asset | Employee receivables (loans, advances) |
| 2120 | Liability | Accrued payroll payables & taxes |
| 4920 | Revenue | Payroll recoveries (loan interest) |
| 6020 | Expense | Salary, allowances, overtime, employer contributions |

**What's missing:** this path uses `IJournalEntryService` directly rather than the newer
`IFinancePostingEngine` (FIN-INT-001), so it has no `FinancePostingEvent` audit trail, no
idempotency key, and no structured cost-centre/project dimensions. **Migrating payroll's posting
call is the single highest-leverage piece of work in the eventual sweep** — everything else in
this document (loans, advances, bonuses, leave encashment, benefit contributions) is already
routed *through* payroll's component/journal-mapping mechanism, so fixing the one call point
upgrades all of them at once. **As of 2026-08-31 this is also a stated compliance gap, not just a
design preference** — see the governance note in §0/§1 above: the Finance owner has said in
writing that "other modules should not create Finance journals or posting records directly,"
which is precisely what this call does.

Loans, salary advances, bonuses and backpay have **no GL account fields of their own** — by
design. They are calculated on HR-side ledgers and become GL lines only when
`PayrollJournalMapping` matches their `TransactionType`. This is a clean separation (payroll logic
independent of the chart of accounts) but it also means the employee receivable balance for a loan
or advance (`PayrollLoan.OutstandingBalance`, `PayrollSalaryAdvance.AdvanceAmount`) lives **only**
in HR — Finance's AR never carries it as a named receivable, only as an aggregate GL balance on
account 1120.

### 3.2 Benefits & Medical — the priority back-fill

Medical claims (`MedicalExpenseClaim`) have a **live, working create→approve→pay workflow** —
unlike most of this document, this is not calculated-but-unposted, it is a functioning payment
process with no Finance leg at all. The backlog explicitly names this the priority back-fill
because it is the closest working analogue to Travel's claim-payment gap, and whatever accounting
treatment the sweep picks for one **must** be applied to the other, or the module will have two
different answers to "how does HR pay someone back" that happen to look similar.

`HealthcareFacility` and `MedicalInsuranceProvider` both carry `BankId`/`AccountNumber` fields —
they look like they were modelled with an eventual AP relationship in mind, but no link to
Finance's `Supplier`/`BusinessPartner` exists. The same observation applies to `TrainingVendor`
(row 43). **This is a below-the-radar decision the sweep needs to make explicitly**: are these
three (healthcare facility, insurer, training vendor) modelled as Finance suppliers so their
payments run through the existing AP module, or as a new "payee" concept specific to HR?

### 3.3 Assets — the one place HR is the creditor

Every other money event in this document is the organisation *paying* an employee or a third
party. Asset surcharges (`AssetSurcharge`) are the exception: HR assesses a charge **against** an
employee for damage or loss, and recovers it — usually through payroll, sometimes as a lump sum.
Two read-only projections already exist for payroll to pull from
(`Assets/surcharges/payroll-deductions`, `Assets/payroll/rental-deductions` for asset rental /
benefit-in-kind), and **neither writes a deduction** — HR declares the amount and method, payroll
(a separate module) is expected to consume it. The backlog is explicit that HR must not build a
deduction-run or payment-status machine to paper over this gap; that machinery belongs to whichever
side of the boundary the sweep assigns it to.

`CompanyAsset.FixedAssetId` is the one integration point that already works correctly in both
directions of the rule: HR **reads** Finance's live net-book-value/depreciation state through
`AssetsServices.cs`'s `ReadFixedAssetAsync`, and **never writes** capitalization, depreciation or
disposal — those stay in Finance (decision D1). This is the pattern the sweep should hold up as the
template for "shared entity, single owner of the money side."

### 3.4 Staff Discipline — the newly-surfaced gap

`StaffDisciplinaryActionType.DefaultFineAmount` and `StaffDisciplineFine.FineAmount` /
`.FinePaidAmount` model a monetary penalty against an employee. **This entity group does not
appear anywhere in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` today.** It is the same shape as an
Asset surcharge (row 39) — an employee receivable/deduction — and should be added to that register
before the sweep, so it is not rediscovered mid-sweep the way Succession's development-activity
cost was (the backlog's own §"Area 13" entry records exactly that kind of surprise).

### 3.5 Training, Succession & Manpower Budget — the three-way double-count

Three different HR areas each maintain a number that claims to represent "money spent developing
people":

1. `ManpowerBudget.TrainingBudget` (org-unit-level annual envelope, part of the establishment
   authorization — row 5–7)
2. `TrainingBudget`/`TrainingBudgetTransaction` (area 7's own departmental training ledger — row 44)
3. `SuccessionDevelopmentActivity.EstimatedCost/.ActualCost` (a successor's training/secondment
   cost — row 47)

The backlog already flags this explicitly and it is repeated here because it is the single
clearest **structural** (not just missing-integration) problem in the HR-Finance boundary: without
a decision, the same training course could be counted in up to three places. **This should be
settled before, not during, the mechanical GL-wiring work**, because it changes which entity is
authoritative and therefore which one gets the Finance link.

### 3.6 Travel & Separation — same shape, same open question

Travel expense claims (row 48) and the separation final settlement (row 54) are structurally
identical: an amount is correctly calculated on the HR side (both have had their arithmetic
explicitly hardened — travel in slice 4, separation from day one) and then **nothing posts**.
Both are blocked on the same TDC decision: **is the payment made through payroll (a "final run" /
"off-cycle run") or as a direct Finance/bank payment?** The backlog is explicit that this single
answer governs both surfaces plus the two Asset-recovery surfaces (rental and surcharge
deductions) — **four separate money flows are waiting on one policy decision**.

### 3.7 Awards — the only area whose own bookkeeping is already internally correct

`AwardBudget.SpentAmount`/`.ReservedAmount` were dead columns until area 14 slice 8 (they existed,
nothing incremented them). As of that slice, conferral reserves and payment spends correctly, so
`GetAvailableBudgetAsync` (`BudgetAmount - SpentAmount - ReservedAmount`) is now meaningful — **but
it is still bookkeeping, not accounting**: it is the awards desk's own view of its yearly
allowance, posts nothing to GL, and the sweep should treat it as a number to *reconcile against*
Finance's actuals rather than a ledger to import wholesale. This is offered as the closest thing in
the HR module to a "done right, but still not Finance-integrated" example — useful as a template
for what the *other* budget surfaces (`ManpowerBudget`, `TrainingBudget`, `StaffTravelBudget`)
should look like once they get the same treatment.

---

## 4. Consolidated list of open decisions (cross-referenced to the backlog)

These are the decisions that block turning "🟡 calculated, not posted" into "✅ posts to Finance"
across multiple rows at once — resolving one often closes several rows in §2:

1. **Payroll vs. direct payment.** Governs: Medical claims (30), Benefit utilizations (27), Awards
   paid (34–35), Travel claims (48), Separation settlement (54), Asset surcharge recovery (40),
   Asset rental deduction (37). *One answer, at least six rows.*
2. **Employee receivables in Finance.** Should Loans (16), Salary Advances (18), Travel Advances
   (49), Asset Surcharges (39), Training Service Bonds (46) become named Finance AR records, or
   remain HR-only balances that net into a single GL control account (as Payroll's loans do
   today via account 1120)? *One answer, five rows.*
3. **Training/Development budget ownership.** Settle whether `ManpowerBudget.TrainingBudget`,
   area 7's `TrainingBudget`, and `SuccessionDevelopmentActivity` costs are one authoritative
   ledger with the others as projections, or three genuinely separate envelopes. *One answer,
   three rows (44, 5, 47).*
4. **`ManpowerBudget.ActualSpent`/`.Variance` writer.** Must be populated from Finance GL actuals —
   no HR mechanism can supply this correctly. *Blocks row 6 specifically, but undermines the
   credibility of the entire manpower-budget surface (rows 5–7) until fixed.*
5. **Does a manpower budget carry a currency?** `ManpowerBudget` has no `CurrencyCode` today,
   unlike Succession (which does the read-side integration correctly) and Travel (which has the
   field but populates it from the caller instead of Finance). *Affects row 5.*
6. **Cost-attribution dimension.** FIN-INT-001's `Dimensions` collection is ready to carry a cost
   centre/project/department tag; **TDC has not said which dimension HR should use.** *Affects
   every EXP/PAY row that eventually posts.*
7. **External payee modelling.** `HealthcareFacility`, `MedicalInsuranceProvider`, `TrainingVendor`
   all carry bank details suggesting an eventual AP relationship, with no link to Finance's
   `Supplier`/`BusinessPartner`. *Affects rows 29–31, 43.*
8. **Separation daily-rate basis** (monthly×12÷365 vs. 30-day-month vs. working-days) — raised in
   `docs/HR-OPEN-QUESTIONS-FOR-TDC.md`, affects row 54–55.
9. **Long-service award pricing** — the milestone ladder is seeded with no monetary value; a null
   here means "unanswered", not "free." *Affects row 35.*
10. **Add Staff Discipline fines to the backlog register** — newly surfaced by this sweep (§3.4),
    not yet in `HR-FINANCE-INTEGRATION-BACKLOG.md`. *Affects row 42.*

---

## 5. Suggested sequencing for the eventual sweep

Not a commitment, a starting order — informed by which rows are (a) highest value, (b) block the
most other rows, and (c) already have the cleanest HR-side arithmetic to build on:

1. **Migrate Payroll's journal posting** from legacy `IJournalEntryService` to
   `IFinancePostingEngine` (FIN-INT-001). Unlocks the audit trail, idempotency and dimensions for
   every row that already routes through `PayrollJournalMapping` (11, 12, 15, 19, 20, 22–24).
2. **Settle decision #1 (payroll vs. direct payment)** — the highest-leverage open question;
   closes the door on six rows at once.
3. **Back-fill Medical claim payment** (row 30) as the reference implementation once #2 is
   settled, then apply the identical treatment to Travel (48) and Separation (54).
4. **Wire Asset surcharge/rental recovery** (37, 39–41) using whatever payroll-deduction mechanism
   #3 established.
5. **Settle decision #2 (employee receivables)** and, if Finance AR records are chosen, migrate
   Loans/Advances/Travel-Advances/Surcharges/Service-Bonds together (16, 18, 39, 46, 49).
6. **Settle the training/manpower/succession budget overlap** (decision #3) before doing any
   GL-wiring on those three surfaces — wiring the wrong owner first means re-wiring later.
7. **Fix `ManpowerBudget.ActualSpent`/`.Variance`** and its currency question (decisions #4–#5) —
   this is a Finance-side read, not an HR posting, and can proceed independently of the rest.
8. **Model external payees** (decision #7) for `HealthcareFacility`/`MedicalInsuranceProvider`/
   `TrainingVendor` once AP is in scope for those payments.
9. **Add Staff Discipline fines to the backlog** and treat alongside Asset surcharges (same shape).

---

## 6. Appendix — quick-reference lookups

**Finance mechanisms available today** (see `docs/Finance/finance-integration-contract-catalogue.md`
for the full catalogue):

| Contract | Status | Relevant to HR because |
|---|---|---|
| FIN-INT-001 (`IFinancePostingEngine.PostAsync`) | Available | The GL posting target for almost every row in §2 |
| FIN-INT-011 (SH Fund, PF, ESB, fuel allocation) | **Owner not defined**, requirements clarification, v0.0 | Explicitly flagged in the backlog as HR/payroll-shaped territory with no owner — the sweep is where it gets one |
| FIN-INT-015 (Procurement demand → Finance budget commitment) | Available (Procurement-specific) | The nearest existing pattern for what an HR/`ManpowerBudget` → Finance budget contract would need to look like — no HR equivalent exists |
| FIN-INT-016 (Direct AP expense invoice → budget) | Available | Template for how an external-payee invoice (training vendor, insurer) could reserve/consume budget once modelled |

**Entity groups NOT covered above because they carry no financial fields** (confirmed during the
sweep, listed so they are not re-checked next time): Organization hierarchy master data
(`OrganizationStructure`, `OrganizationLevel`, `Team`/`TeamMember` beyond cost-centre tagging),
Job Architecture reference data (`JobFamily`, `JobSubFamily`, `CareerLevel`), Job Analysis narrative
fields (`JobDutyItem`, `JobResponsibility`, `JobCompetency`, etc.), Recruitment pipeline mechanics
(`RecruitmentPipeline(Stage)`, `JobPosting`, `JobApplication`, `JobInterview`), Performance &
Appraisal (`AppraisalTemplate*`, `AppraisalCycle`, `EmployeeGoal`, `KpiDefinition`) — scoring
weights are not money, Attendance (`StaffAttendanceRecord/Log`, `StaffDailyAttendance`,
`StaffMonthlyAttendanceSummary`) — feeds payroll hours, not amounts, Orientation & Policies
(`OrientationProgram`, `HrPolicyDocument`) except where `OrientationProgram` inherits the same
service-bond shape as Training (already covered under row 46's pattern, no separate monetary field
found), Grievance, Profile Changes, HR Letters, Union/CBA structural fields (only the
financial-terms-in-free-text noted at row 59), Probation/Onboarding, Company Schedule participant
mechanics (only `CompanyEvent`'s budget fields are financial, row 58).

**Correction, 2026-08-31:** Safety/SHE was originally placed in this "no financial fields" list on
an inference, without a dedicated field-level check. A follow-up pass found it does carry real
money (rows 60–63 above) — an insurance-claim payment flow on `SafetyIncident` that is the same
shape as Medical's already-flagged priority back-fill (row 30), plus equipment/PPE cost tracking
and a sanction label with no backing payment field. Treat any "confirmed no financial fields"
claim elsewhere in this appendix as provisional unless it was checked at the same field level.

---

*This document is a point-in-time sweep (2026-08-31). As HR areas not yet built (Awards
portals, remaining areas 19–27) come online, add their money-touching entities to
`docs/HR-FINANCE-INTEGRATION-BACKLOG.md` per its own rules, and update the master table above so
it stays a complete map rather than a snapshot.*
