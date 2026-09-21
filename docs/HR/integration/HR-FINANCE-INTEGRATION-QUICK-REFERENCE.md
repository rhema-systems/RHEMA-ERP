# HR ↔ Finance Integration — Quick Reference

**Purpose:** A short briefing note for any AI agent (or person) picking up HR-Finance
integration work in this repo, without needing to re-read the full sweep/backlog first.
This is the workspace-file counterpart of the agent's internal repo-memory note, so it can be
referenced by other tools/agents that don't have access to that memory store.

**Vetted 2026-09-02** against the code together with every other document in this folder; the
key-facts list below was corrected inline (manpower actuals, exchange rates, the missed revenue
flow, SHE's backlog placeholder, payroll ownership). The entity sweep's own vetting block holds
the full audit trail.

---

## Read these four docs, in this order

1. **[`docs/HR/integration/HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md)** — comprehensive
   entity-level sweep (created 2026-08-31). Master table of every HR entity with financial
   implications, its direction (`EXP`/`PAY`/`REC`/`BUD`/`REF`), proposed Finance target, and
   status. **Use this to find "which entity carries money."**
2. **[`docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`](HR-FINANCE-INTEGRATION-BACKLOG.md)** — the
   living decision register (opened 2026-08-17). Authoritative for decisions already made and
   open TDC questions. Policy: HR does **not** post to GL per-area; one comprehensive sweep
   happens after the whole HR module is built. Master data (`Currency`/`ExchangeRate`) is **not**
   deferred — read Finance's copy now, never keep a parallel HR copy.
3. **[`docs/Finance/finance-integration-contract-catalogue.md`](../../Finance/finance-integration-contract-catalogue.md)**
   — the mechanism catalogue (contracts FIN-INT-001 through FIN-INT-016). FIN-INT-001
   (`IFinancePostingEngine.PostAsync`) is the one **Available**, general-purpose GL posting entry
   point. Status meanings: Available / Planned / Decision required / Requirements clarification.
4. **[`docs/HR/integration/HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md)** —
   comprehensive sweep (created 2026-08-31) of Finance/Procurement/Inventory/Projects for
   engineering and business patterns HR hasn't adopted yet (reminder notifications, optimistic
   concurrency, budget commitment lifecycle, asset lifecycle maturity, etc.). **Use this to find
   "what has another module already solved that HR is re-solving badly, or not solving at all."**
5. **[`docs/HR/catalogues/HR-REPORTS-CATALOGUE.md`](../catalogues/HR-REPORTS-CATALOGUE.md)** and
   **[`docs/HR/catalogues/HR-BULK-OPERATIONS-CATALOGUE.md`](../catalogues/HR-BULK-OPERATIONS-CATALOGUE.md)** — not
   Finance-integration docs, but companions in the same folder: the reports HR needs to build,
   and the single-item actions that need a bulk/multi-select equivalent.
6. **[`docs/HR/catalogues/HR-DOCUMENTATION-CATALOGUE.md`](../catalogues/HR-DOCUMENTATION-CATALOGUE.md)** — what
   documentation/outputs HR should produce for developers, end users and stakeholders, and where
   each should be exposed (this folder is one part of that picture, not all of it).
7. **[`docs/HR/programme/HR-UAT-DEMO-PRESENTATION-PLAN.md`](../programme/HR-UAT-DEMO-PRESENTATION-PLAN.md)** — the
   2-day UAT/acceptance demo plan for the whole HR module: agenda, a verified requirements
   traceability/sign-off matrix (real FR-HR-### IDs pulled from the build plans), and the
   documents to prepare beforehand.
8. **[`docs/HR/catalogues/HR-IMPORT-EXPORT-CATALOGUE.md`](../catalogues/HR-IMPORT-EXPORT-CATALOGUE.md)** — which HR/SHE
   entities need a bulk import or export capability, what already exists, and the one pattern
   (Finance's `BulkImportAssets`: Excel + dry-run + row-level errors) to standardize new ones on.
9. **[`docs/HR/integration/HR-MODULE-INTEGRATION-MAP.md`](HR-MODULE-INTEGRATION-MAP.md)** — every OTHER
   module (not Finance) HR/SHE needs a real integration with — Maintenance/Fleet, Procurement,
   Inventory, Projects, Estate, Identity, EHC, Sales, Pricing, Quantity Survey, plus Workflow,
   Identity reconciliation, Payroll and DMS (added 2026-09-02) — with a verdict per link.
10. **[`docs/HR/integration/HR-PAYROLL-BOUNDARY.md`](HR-PAYROLL-BOUNDARY.md)** — payroll is another
    developer's module; what HR reads, what it never writes, the three bridges (salary structure,
    pay components, payroll membership), and which Finance-sweep items are the payroll owner's.
11. **[`docs/HR/integration/HR-WORKFLOW-ENGINE-INTEGRATION.md`](HR-WORKFLOW-ENGINE-INTEGRATION.md)** — the
    four-step plug-in recipe, the traps, and the engine defects every approval-bearing money event
    inherits (#3 conditional routing, #15 generic approvals strand the record).
12. **[`docs/HR/README.md`](../README.md)** — the index of everything HR, with the governing rules.

Also read `docs/HR/areas/she/HR-SHE-INTEGRATION-AND-BOUNDARIES.md` if the task touches Safety money (rows
60–63) and `docs/HR/operations/HR-VERIFICATION-HARNESS-GUIDE.md` before running any harness.

## Governance rule (confirmed in writing, 2026-08-31)

The Finance module owner announced the posting-engine foundation (PRs #70/#72) with an explicit
rule: *"operational modules retain ownership of their source transactions and approvals. Finance
owns account resolution, fiscal-period controls, currency, tax and subledger effects, journal
creation, audit, reversal and idempotency. Other modules should not create Finance journals or
posting records directly."* Two procedural requirements come with it:

- **Coordinate with the Finance owner before implementing anything marked Planned or Decision
  Required** in the contract catalogue — do not design and build first. FIN-INT-011 ("SH Fund, PF,
  ESB and fuel allocation") is HR/payroll-shaped and has no assigned owner; HR should raise it.
- **Every integration ships with the reusable consumer-contract tests** — see
  `docs/Finance/finance-integration-consumer-test-template.md` (assertion helper at
  `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConsumerContractAssertions.cs`) and follow
  `docs/Finance/finance-integration-adapter-checklist.md` at design time.

Full quote and what it changes for HR: `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md` → "Finance owner's
governance message."

## Key facts to carry into any HR-Finance task

- **2026-09-20 — the sweep has started; read [`HR-FINANCE-POSTING-DESIGN.md`](HR-FINANCE-POSTING-DESIGN.md)
  before the rest of this list.** Medical claims and staff travel post through
  `IHrFinancePostingAdapter` (HR's side of FIN-INT-001). Payroll's legacy `IJournalEntryService`
  call, described below as the top gap, was already migrated by the payroll owner. Several bullets
  below are now history rather than open gaps; the design's sections 6 and 7 say what is still open.

- **Payroll is the only HR area that posts to GL today**, and it uses the **legacy**
  `IJournalEntryService` directly — **not** the newer `IFinancePostingEngine` / FIN-INT-001. This
  is now confirmed **out of policy** per the governance rule above, not just technical debt.
  Migrating that call is flagged as the highest-leverage next step; it upgrades every other money
  event that already routes through payroll (loans, advances, bonuses, backpay, leave encashment,
  benefit contributions) at once. ⚠ **Payroll is another developer's module** — HR raises this
  with the payroll owner (the `docs/HR/integration/handoffs/HANDOFF-PAYROLL-*.md` shape), it does not edit
  `PayrollService.cs`. See `HR-PAYROLL-BOUNDARY.md`.
- **HR's only revenue flow is not in the backlog at all** *(found 2026-09-02)*:
  `TimesheetInvoice` bills external consultant clients (hours × rate, tax, receipt) with no Finance
  AR leg and no customer link. Entity-sweep row 64 / decision #11. Register it before the sweep.
- **`PayrollJournalMapping`** entity
  (`src/ErpSystem.Core/Entities/HR/Payroll/PayrollEntities.cs`) is the config bridge:
  `TransactionType`/`ComponentCode` → `AccountCode`/`DebitCredit`.
- **Biggest open decision, blocking the most rows:** *"payroll vs. direct payment"* for
  reimbursements — governs Medical claims, Travel claims, Separation settlement, Asset
  surcharge/rental recovery, and Awards paid. One TDC answer closes six+ rows in the sweep.
- **`ManpowerBudget.ActualSpent` is caller-supplied on every budget update** (`UpdateManpowerBudgetDto`),
  and `Variance` is derived from it — a self-declared figure that no screen sends, so it is 0 in
  practice. It can only be *correctly* populated from Finance's GL actuals, and the caller path
  should go once that feed exists. *(Corrected 2026-09-02; the earlier "no writer anywhere" was
  wrong.)*
- **Master-data rule status:** Travel, Assets, Letters, Separation, Succession **and Recruitment
  costs (since 2026-09-10, round 2b R7)** all read Finance's rates through `HrCurrencyBridge`.
  **No HR violation is left.** Payroll keeps its own `PayrollExchangeRate` table — the payroll
  owner's to fix. Suppliers are read through `api/hr/suppliers` (Procurement's own list answers
  400 to everyone — cross-module defect #26).
- **Three-way training-budget double-count risk, unresolved:** `ManpowerBudget.TrainingBudget`
  vs. area-7's own `TrainingBudget` vs. `SuccessionDevelopmentActivity` cost. Settle ownership
  before wiring any of the three to Finance.
- **`CompanyAsset.FixedAssetId` → Finance `FixedAsset`** is the one clean, working integration
  pattern in the whole module: HR **reads** Finance's live net-book-value/depreciation state and
  **never writes** capitalization, depreciation or disposal (decision D1). Use this as the
  template for how other shared entities should split ownership.
- **Newly surfaced gap (not yet in the backlog as of the entity sweep):**
  `StaffDisciplineFine` / `StaffDisciplinaryActionType.DefaultFineAmount` has no Finance
  treatment and is not yet registered in `HR-FINANCE-INTEGRATION-BACKLOG.md`. Add it before the
  sweep starts — same shape as an Asset surcharge (an employee receivable/deduction).
- **Second newly surfaced gap (Safety/SHE, found 2026-08-31):** `SafetyIncident.ClaimAmount`/
  `.AmountPaid`/`.InsuranceClaimFiled` is a real insurance-claim payment flow, the same shape as
  Medical's already-flagged priority back-fill — and `SafetyIncidentInvolvedPerson` links directly
  to Medical's own `MedicalExpenseClaim`, so the two should be solved together, not separately.
  The backlog has only a placeholder row for SHE ("any compensation or remediation spend, to
  record"); the entity-level detail is still owed there.
- **Medical is three third-party flows, not one:** the premium payable
  (`MedicalInsurancePremiumRecord`), the recovery from the insurer (`MedicalInsuranceClaim`) and
  the NHIS recovery (`NHISClaim`) all exist beside the employee reimbursement. Rows 70–72.
- **The payee question already has an answer in one HR area:** the six travel booking entities
  carry `VendorId → Procurement.Supplier`. Reuse that FK for healthcare facilities, insurers and
  training vendors rather than inventing an HR payee.

## Rule of thumb while working in HR

Do **not** invent an HR-side posting mechanism (a parallel ledger, a private payment-status
machine, an HR "journal") to paper over a missing Finance link. Record the money event, note the
gap, and defer the actual GL/AP/AR treatment to the comprehensive sweep — per the policy in
`HR-FINANCE-INTEGRATION-BACKLOG.md`.
