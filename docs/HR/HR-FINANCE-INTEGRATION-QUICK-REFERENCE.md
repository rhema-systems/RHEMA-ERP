# HR ↔ Finance Integration — Quick Reference

**Purpose:** A short briefing note for any AI agent (or person) picking up HR-Finance
integration work in this repo, without needing to re-read the full sweep/backlog first.
This is the workspace-file counterpart of the agent's internal repo-memory note, so it can be
referenced by other tools/agents that don't have access to that memory store.

---

## Read these four docs, in this order

1. **[`docs/HR/HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md)** — comprehensive
   entity-level sweep (created 2026-08-31). Master table of every HR entity with financial
   implications, its direction (`EXP`/`PAY`/`REC`/`BUD`/`REF`), proposed Finance target, and
   status. **Use this to find "which entity carries money."**
2. **[`docs/HR-FINANCE-INTEGRATION-BACKLOG.md`](../HR-FINANCE-INTEGRATION-BACKLOG.md)** — the
   living decision register (opened 2026-08-17). Authoritative for decisions already made and
   open TDC questions. Policy: HR does **not** post to GL per-area; one comprehensive sweep
   happens after the whole HR module is built. Master data (`Currency`/`ExchangeRate`) is **not**
   deferred — read Finance's copy now, never keep a parallel HR copy.
3. **[`docs/Finance/finance-integration-contract-catalogue.md`](../Finance/finance-integration-contract-catalogue.md)**
   — the mechanism catalogue (contracts FIN-INT-001 through FIN-INT-016). FIN-INT-001
   (`IFinancePostingEngine.PostAsync`) is the one **Available**, general-purpose GL posting entry
   point. Status meanings: Available / Planned / Decision required / Requirements clarification.
4. **[`docs/HR/HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md)** —
   comprehensive sweep (created 2026-08-31) of Finance/Procurement/Inventory/Projects for
   engineering and business patterns HR hasn't adopted yet (reminder notifications, optimistic
   concurrency, budget commitment lifecycle, asset lifecycle maturity, etc.). **Use this to find
   "what has another module already solved that HR is re-solving badly, or not solving at all."**
5. **[`docs/HR/HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md)** and
   **[`docs/HR/HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md)** — not
   Finance-integration docs, but companions in the same folder: the reports HR needs to build,
   and the single-item actions that need a bulk/multi-select equivalent.
6. **[`docs/HR/HR-DOCUMENTATION-CATALOGUE.md`](HR-DOCUMENTATION-CATALOGUE.md)** — what
   documentation/outputs HR should produce for developers, end users and stakeholders, and where
   each should be exposed (this folder is one part of that picture, not all of it).
7. **[`docs/HR/HR-UAT-DEMO-PRESENTATION-PLAN.md`](HR-UAT-DEMO-PRESENTATION-PLAN.md)** — the
   2-day UAT/acceptance demo plan for the whole HR module: agenda, a verified requirements
   traceability/sign-off matrix (real FR-HR-### IDs pulled from the build plans), and the
   documents to prepare beforehand.
8. **[`docs/HR/HR-IMPORT-EXPORT-CATALOGUE.md`](HR-IMPORT-EXPORT-CATALOGUE.md)** — which HR/SHE
   entities need a bulk import or export capability, what already exists, and the one pattern
   (Finance's `BulkImportAssets`: Excel + dry-run + row-level errors) to standardize new ones on.

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

Full quote and what it changes for HR: `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` → "Finance owner's
governance message."

## Key facts to carry into any HR-Finance task

- **Payroll is the only HR area that posts to GL today**, and it uses the **legacy**
  `IJournalEntryService` directly — **not** the newer `IFinancePostingEngine` / FIN-INT-001. This
  is now confirmed **out of policy** per the governance rule above, not just technical debt.
  Migrating that call is flagged as the highest-leverage next step; it upgrades every other money
  event that already routes through payroll (loans, advances, bonuses, backpay, leave encashment,
  benefit contributions) at once.
- **`PayrollJournalMapping`** entity
  (`src/ErpSystem.Core/Entities/HR/Payroll/PayrollEntities.cs`) is the config bridge:
  `TransactionType`/`ComponentCode` → `AccountCode`/`DebitCredit`.
- **Biggest open decision, blocking the most rows:** *"payroll vs. direct payment"* for
  reimbursements — governs Medical claims, Travel claims, Separation settlement, Asset
  surcharge/rental recovery, and Awards paid. One TDC answer closes six+ rows in the sweep.
- **`ManpowerBudget.ActualSpent` / `.Variance` have no writer anywhere** — permanently zero today.
  They can only be correctly populated from Finance's GL actuals.
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
  Also not yet registered in the backlog.

## Rule of thumb while working in HR

Do **not** invent an HR-side posting mechanism (a parallel ledger, a private payment-status
machine, an HR "journal") to paper over a missing Finance link. Record the money event, note the
gap, and defer the actual GL/AP/AR treatment to the comprehensive sweep — per the policy in
`HR-FINANCE-INTEGRATION-BACKLOG.md`.
