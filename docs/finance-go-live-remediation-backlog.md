# Finance Go-Live Remediation Backlog

Date prepared: 2026-07-03

This backlog converts the Finance audit findings into a production-grade stabilization program. It is repo-specific and cites the current implementation surfaces that need remediation. It assumes:

- Multi-tenant production is required, with tenant isolation by `TenantId`.
- Multi-currency is required at go-live.
- Fixed assets are required at go-live.
- Reporting must support tenant-level financial statements and configured GL segments such as branch, cost center, department, project, fund, and business unit.
- Accounting basis is IFRS plus Ghana statutory reporting.
- No fixed date pressure. Quality and accounting correctness are more important than speed.
- The posted General Ledger is the accounting source of truth. Stored balances may exist only as derived read models that can be rebuilt from immutable posted ledger entries.

Reference sources used for statutory and accounting context:

- IFRS IAS 1, Presentation of Financial Statements: https://www.ifrs.org/issued-standards/list-of-standards/ias-1-presentation-of-financial-statements/
- IFRS IAS 21, The Effects of Changes in Foreign Exchange Rates: https://www.ifrs.org/issued-standards/list-of-standards/ias-21-the-effects-of-changes-in-foreign-exchange-rates/
- Ghana Revenue Authority VAT page: https://gra.gov.gh/domestic-tax/tax-types/vat/
- Ghana Revenue Authority VAT Withholding page: https://gra.gov.gh/domestic-tax/tax-types/vat-withholding/
- Ghana Revenue Authority WHT page: https://gra.gov.gh/domestic-tax/tax-types/withholding-tax/

## Architecture Direction

### GL As The Source Of Truth

All financial reports, reconciliations, and statutory balances must be derived from immutable posted ledger entries or controlled ledger balance snapshots generated from posted entries. Mutable fields such as `Account.CurrentBalance`, `BankAccount.CurrentBalance`, or operational document balances may remain as cached read models only if they can be rebuilt and reconciled to posted GL.

Primary affected areas:

- `src/ErpSystem.Core/Entities/Finance/JournalEntry.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountTransaction.cs`
- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/BankAccountService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`

Rules:

- Posted journal entries are immutable.
- Corrections use reversal or adjustment journals.
- Account and bank balances are derived views.
- Financial statements must filter by `TenantId`, book, period/date, account, segment, currency, posting status, and deletion state.
- All posting must be idempotent by source document and posting event.

### Posting Engine Design

Create a single Finance posting boundary, preferably `IFinancePostingEngine`, and migrate all Finance workflows onto it. This engine should wrap the existing `JournalEntryService` rather than allowing parallel GL writers.

Recommended responsibilities:

- Validate tenant ownership for every referenced entity.
- Validate period state and module lock at posting time.
- Validate account status, posting permissions, control-account rules, currency, and segments.
- Resolve posting templates by source module and event.
- Create journal entries and account transactions.
- Store source document references and posting event type.
- Post, reverse, and adjust ledger entries idempotently.
- Emit audit events.
- Update or rebuild cached balances only after ledger posting.

Current implementation surfaces to consolidate:

- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `JournalEntryService.ReverseJournalEntryAsync`
- `GeneralLedgerService.PostJournalEntryAsync`
- `GeneralLedgerService.RunCurrencyRevaluationCoreAsync`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostApPaymentAsync`
- `SubledgerPostingService.PostArInvoiceAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `CashTransactionService.CreateReceiptAsync`
- `CashTransactionService.CreatePaymentAsync`
- `AssetDisposalService.CompleteDisposalAsync`
- `AssetValuationService.PostValuationToGLAsync`

### Workflow Engine Integration

The repo already contains a configurable workflow engine. Finance must extend this engine rather than create hardcoded approval logic.

Canonical workflow components found:

- `src/ErpSystem.Core/Interfaces/Workflow/IWorkflowServices.cs`
- `src/ErpSystem.Core/Services/Workflow/WorkflowEngine.cs`
- `src/ErpSystem.Core/Interfaces/IWorkflowService.cs`
- `src/ErpSystem.Api/Services/SimpleWorkflowService.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Core/Entities/Workflow/WorkflowEntityType.cs`
- `src/ErpSystem.Core/Entities/Workflow/WorkflowDefinition.cs`
- `src/ErpSystem.Core/Entities/Workflow/WorkflowApproval.cs`
- DI registration in `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`

Current Finance workflow integrations already exist for:

- `JournalEntry`
- `VendorInvoice`
- `PaymentBatch`
- `BudgetReturn`
- `UnitJournalEntry`
- `BankReconciliation`
- `AssetTransfer`
- `AssetDisposal`

Required direction:

- Add or complete workflow entity context in `SimpleWorkflowService.BuildEntityContextAsync`.
- Add approved and rejected outcomes in `FinanceApprovalsController.ApplyApprovedOutcomeAsync` and `ApplyRejectedOutcomeAsync`.
- Seed tenant-specific workflow definitions for every Finance approval action.
- Keep thresholds, approver roles, and routing rules configurable in workflow definitions.
- Do not hardcode approval thresholds where workflow configuration can express them.

### Tenant Isolation Model

Tenant isolation is by `TenantId` only. Every Finance query, command, posting lookup, workflow lookup, report, export, cache rebuild, and migration script must scope by current tenant.

Rules:

- No `FindAsync(id)` for tenant-owned Finance entities unless immediately followed by a tenant guard before use.
- No cross-tenant references in DTOs. Validate all referenced IDs belong to the current tenant.
- Unique constraints must include `TenantId` where appropriate.
- Tests must prove cross-tenant read, write, post, reverse, approve, close, and report access is denied.

### Functional Currency And Multi-Currency Architecture

Each tenant can choose a functional currency. Functional currency must be locked after accounting transactions exist unless a formal migration/change process is implemented.

Required multi-currency support:

- Transaction currency.
- Tenant functional currency.
- Exchange-rate snapshot on every foreign currency transaction.
- Foreign amount, functional amount, rate, rate type, rate source, and rate date on ledger lines.
- Realized FX gains/losses on settlement.
- Unrealized FX revaluation for foreign AP, AR, bank, and other monetary items.
- Bank FX accounts.
- Revaluation and translation journals through the posting engine.
- IFRS-style auditability: historical rates remain reproducible after rate table changes.

### Segment-Based Reporting

The GL already supports segmentation through account segment structures and segment values. Reports must support filters and grouping by configured segments, not only fixed branch/cost-center fields.

Affected areas:

- `src/ErpSystem.Api/Services/Finance/Segments/*`
- `src/ErpSystem.Core/Entities/Finance/AccountSegmentStructure.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountSegmentValue.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- Finance report DTOs under `src/ErpSystem.Core/DTOs/Finance`

### Ghana Statutory Tax Handling

Current GRA guidance reflected in this backlog:

- COVID-19 Health Recovery Levy is abolished.
- VAT is 15%.
- NHIL is 2.5%.
- GETFund Levy is 2.5%.
- VAT, NHIL, and GETFund are calculated on the same taxable base.
- VAT invoices must show separate lines for NHIL, GETFund, and VAT.
- VAT-registered taxpayers may claim NHIL and GETFund as input tax under current GRA guidance.
- VAT withholding agents withhold 7% from payments for standard-rated supplies as described by GRA.
- WHT must be configurable by transaction type, residence status, supplier/customer status, exemption, thresholds, effective dates, and statutory return requirements.

Implementation must avoid hardcoded rates except in seed data. Rates must be effective-dated, tenant-scoped, versioned, and reproducible for historic transactions.

### Data Migration And Cleanup Strategy

Existing accounting data must be migrated, cleaned, reconciled, and signed off before go-live.

Migration phases:

1. Freeze legacy posting behavior.
2. Snapshot all source documents and ledger records by tenant.
3. Classify records by status: draft, submitted, approved, posted, reversed, voided, deleted-like, orphaned.
4. Rebuild ledger balances from posted entries.
5. Reconcile GL to AP, AR, bank, tax, fixed assets, and multi-currency subledgers.
6. Produce proposed adjustment journals.
7. Obtain accountant sign-off.
8. Lock opening periods.
9. Load or confirm opening balances.
10. Run go-live cutover smoke tests.

### Audit Trail And Immutability Rules

Required rules:

- Posted journals and posted source documents cannot be edited or deleted.
- Voids create reversing entries.
- Operational status changes that affect accounting must be linked to journal entries.
- Tax config, exchange rates, COA structure, periods, bank accounts, workflow definitions, and approval decisions require audit logs.
- Audit logs must include tenant, user, timestamp, action, old values, new values, reason, source IP/session where available, and source document.

## Suggested Implementation Order

1. Stabilize test/build baseline.
2. Authorization and tenant isolation.
3. Posting engine and GL immutability.
4. Workflow integration for all Finance approvals.
5. Cash/bank/reconciliation rebuild.
6. AP and AR lifecycle corrections.
7. Period close and cut-off controls.
8. Multi-currency architecture.
9. Ghana tax handling.
10. Fixed assets lifecycle.
11. Reporting and segment filters.
12. COA controls.
13. Frontend completion.
14. Data migration and reconciliation.
15. Go-live accounting sign-off.

## Backlog Tickets

### FIN-001 - Repair Finance Test And Validation Baseline

Suggested implementation order: 1

Affected files/classes/methods:

- `tests/ErpSystem.Tests/Services/Finance/*`
- `tests/ErpSystem.Api.Tests/Services/Finance/*`
- `frontend/package.json`
- Finance frontend pages under `frontend/src/app/finance`

Problem statement:

The API project builds and a small targeted API Finance test subset passes, but the broader Finance test project does not compile and frontend `tsc --noEmit` fails in Finance pages. This prevents reliable regression testing of accounting fixes.

Accounting/compliance impact:

High-risk posting, reversal, tax, FX, and period-close behavior can regress without detection.

Proposed implementation approach:

- Update stale Finance tests to current service names and interfaces.
- Add test categories for `TenantIsolation`, `Authorization`, `Posting`, `Reversal`, `PeriodClose`, `Tax`, `FX`, `FixedAssets`, `Reporting`, and `Migration`.
- Fix Finance TypeScript errors before any release candidate.
- Add CI gates for backend build, Finance test suites, frontend type-check, and lint.

Dependencies:

- None.

Acceptance criteria:

- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore` passes.
- Finance tests in both test projects compile and run.
- `npm.cmd run type-check` passes.
- `npm.cmd run lint:check` passes.
- New failing tests exist for current blocker behaviors before remediation work closes.

Test cases:

- Compile all Finance test projects.
- Run all tests tagged or namespaced Finance.
- Run frontend type-check and lint.
- Add regression tests that currently fail for unauthenticated cash endpoints, cross-tenant journal lookup, and posting into a closed period.

Priority: Critical

Go-live blocker: Yes

### FIN-002 - Enforce Finance Authorization And Permission Policies

Suggested implementation order: 2

Affected files/classes/methods:

- `src/ErpSystem.Api/Controllers/Finance/BankAccountController.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashTransactionController.cs`
- `src/ErpSystem.Api/Controllers/Finance/BankReconciliationController.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashReportsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs`
- `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`
- `src/ErpSystem.Api/Controllers/Finance/ArControllersConsolidated.cs`
- `src/ErpSystem.Api/Controllers/Finance/FiscalPeriodController.cs`
- `src/ErpSystem.Api/Controllers/Finance/ExchangeRateController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/AccountController.cs`
- `frontend/src/components/layout/sidebar.tsx`
- `frontend/src/lib/permissions.ts`

Problem statement:

Several Finance controllers are unauthenticated, and many authenticated controllers lack action-level permission checks. Sidebar role checks are only UI hints and cannot protect backend financial actions.

Accounting/compliance impact:

Unauthorized users can create, edit, delete, approve, post, reconcile, or inspect sensitive financial records. This breaks segregation of duties and auditability.

Proposed implementation approach:

- Define a Finance permission matrix covering read, create, edit, delete, submit, approve, reject, post, reverse, void, reconcile, close, reopen, export, tax-config, FX-rate, COA-admin, fixed-asset-admin, and migration actions.
- Add class-level `[Authorize]` to unauthenticated controllers.
- Add action-level policies or explicit permission checks to every mutation and sensitive read.
- Keep frontend permission checks for UX only; backend remains authoritative.
- Add a permission seed/update migration for all Finance permissions and roles.

Dependencies:

- Tenant isolation.
- Workflow engine for approval actions.

Acceptance criteria:

- No Finance controller mutation endpoint is callable anonymously.
- No sensitive Finance action is callable without the configured permission.
- Approval endpoints require both workflow authorization and Finance permission.
- Permission names are consistent and centrally documented.

Test cases:

- Anonymous calls to bank, cash, reconciliation, and cash report endpoints return 401.
- Authenticated user without Finance permissions receives 403 for create/post/reverse/close/reconcile.
- Approver without assigned workflow approval cannot approve.
- User with read-only Finance role cannot mutate records.

Priority: Critical

Go-live blocker: Yes

### FIN-003 - Enforce Tenant Isolation Across Finance

Suggested implementation order: 3

Affected files/classes/methods:

- `JournalEntryService.GetJournalEntryByIdAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `JournalEntryService.ReverseJournalEntryAsync`
- `GeneralLedgerService.GetAccountByIdAsync`
- `GeneralLedgerService.GetAccountByCodeAsync`
- `GeneralLedgerService.GetAllAccountsAsync`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostApPaymentAsync`
- `SubledgerPostingService.PostArInvoiceAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `CashTransactionService.GetByIdAsync`
- `CashTransactionService.DeleteAsync`
- `BankAccountService.GetByIdAsync`
- `BankAccountService.UpdateAsync`
- `BankAccountService.DeleteAsync`
- `BankReconciliationService.StartReconciliationAsync`
- `TaxRuleController.GetTaxRules`
- `TaxRuleController.UpdateTaxRule`
- `TaxRuleController.DeleteTaxRule`

Problem statement:

Multiple services load tenant-owned records by ID without tenant predicates, often through `FindAsync(id)`.

Accounting/compliance impact:

Cross-tenant data leakage or cross-tenant posting can compromise production ledgers.

Proposed implementation approach:

- Introduce a reusable tenant guard helper such as `ITenantEntityGuard`.
- Replace `FindAsync(id)` patterns with tenant-scoped queries for tenant-owned Finance entities.
- Validate every referenced ID in DTOs belongs to the current tenant.
- Add tenant-scoped unique indexes where missing.
- Add integration tests with two tenants and identical IDs where possible.

Dependencies:

- Tenant isolation.

Acceptance criteria:

- All Finance reads, writes, deletes, approvals, postings, reversals, reports, exports, and workflows are tenant-scoped.
- Cross-tenant IDs in request DTOs fail with 404 or 403 before business logic executes.
- Workflow instances and approvals are tenant-scoped.

Test cases:

- Tenant A cannot read Tenant B journal, bank account, invoice, payment, asset, tax rule, exchange rate, fiscal period, or workflow approval.
- Tenant A cannot post or reverse a Tenant B document.
- Tenant A cannot use Tenant B account ID on a journal line.
- Tenant A cannot reconcile Tenant B bank account.

Priority: Critical

Go-live blocker: Yes

### FIN-004 - Create A Single Finance Posting Engine

Suggested implementation order: 4

Affected files/classes/methods:

- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IJournalEntryService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
- Finance services that currently create journal entries directly.

Problem statement:

The codebase has multiple posting paths. Some paths create or post journal entries directly, bypassing consistent validations, tenant checks, control-account rules, period checks, balance updates, and audit logging.

Accounting/compliance impact:

Different workflows can post inconsistent GL entries. Reports and balances may not reconcile.

Proposed implementation approach:

- Add `IFinancePostingEngine` and implementation in `src/ErpSystem.Api/Services/Finance/Posting`.
- Route all posting events through this engine.
- Decommission or wrap `GeneralLedgerService.PostJournalEntryAsync` so it cannot bypass `JournalEntryService`.
- Require source document type, source document ID, posting event, tenant, book, period, currency, and idempotency key.
- Store posting result and journal links on source documents.
- Centralize account, tax, FX, period, segment, control-account, and workflow validations.

Status update 2026-07-09:

- Normal runtime posting paths are now locked down under `docs/finance-legacy-posting-path-lockdown.md`.
- `ISubledgerPostingService` is obsolete and not registered in normal DI.
- `GeneralLedgerService.PostJournalEntryAsync` and the generic direct GL posting endpoint are disabled for normal runtime posting.
- Remaining opening-balance and migration posting work is tracked separately under `FIN-LIM-0006`.

Dependencies:

- Tenant isolation.
- Workflow engine for approval-gated posting.
- FX.
- Tax.

Acceptance criteria:

- There is one approved path for creating/posting/reversing accounting entries.
- All Finance workflows use the posting engine.
- Duplicate posting attempts for the same source event are idempotent.
- Direct account balance mutation is removed from posting flows.

Test cases:

- AP invoice post creates exactly one journal for one posting event.
- Duplicate AP invoice post returns existing posting result.
- Cash receipt post cannot bypass period lock.
- Direct control-account manual posting is rejected unless source event is system-owned.
- Reversal creates linked reversing journal and does not mutate posted journal lines.

Priority: Critical

Go-live blocker: Yes

### FIN-005 - Make Stored Account And Bank Balances Rebuildable Read Models

Suggested implementation order: 5

Affected files/classes/methods:

- `Account.CurrentBalance` and related balance fields in `src/ErpSystem.Core/Entities/Finance/Account.cs`
- `BankAccount.CurrentBalance` and `BankAccount.AvailableBalance` in `src/ErpSystem.Core/Entities/Finance/BankAccount.cs`
- `BankAccountService.UpdateBalanceAsync`
- `BankAccountService.GetBalanceAsync`
- `CashReportsController.GetPosition`
- `GeneralLedgerService.CalculateAccountBalanceAsOf`
- `src/ErpSystem.Core/Entities/Finance/AccountBalance.cs`

Problem statement:

Stored balances are mutated by operational workflows and are used as if they were accounting source-of-truth balances.

Accounting/compliance impact:

Balances can diverge from posted GL, creating false reconciliations and incorrect statements.

Proposed implementation approach:

- Treat account and bank balances as read models only.
- Add a controlled balance snapshot/rebuild service sourced from posted ledger entries.
- Remove direct bank balance mutation from cash transaction flows.
- Add reconciliation reports comparing cached balances to posted GL balances.
- Restrict any balance correction to a rebuild process, not manual edits.

Dependencies:

- Posting engine.
- Reporting.
- Migration.

Acceptance criteria:

- Financial reports do not use mutable account or bank balance fields.
- Cached balances can be rebuilt per tenant, book, account, segment, currency, and period.
- Rebuild output exactly matches posted ledger totals.
- Bank balances displayed in cash management identify whether they are ledger-derived or bank-statement-derived.

Test cases:

- Rebuild account balance from posted ledger entries and compare to cached balance.
- Cash receipt changes ledger balance only after posting.
- Deleting or voiding an unposted cash transaction does not alter GL.
- Cached balance tampering is detected by rebuild comparison.

Priority: Critical

Go-live blocker: Yes

### FIN-006 - Harden Journal Entry Lifecycle

Suggested implementation order: 6

Affected files/classes/methods:

- `JournalEntryController.CreateJournalEntry`
- `JournalEntryController.RequestApproval`
- `JournalEntryController.ApproveJournalEntry`
- `JournalEntryController.PostJournalEntry`
- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `JournalEntryService.ReverseJournalEntryAsync`
- `JournalEntryService.ValidateControlAccountPosting`
- `JournalEntryService.UpdateApprovalStatusAsync`

Problem statement:

Manual journal creation lacks complete permission checks; posting does not revalidate period state; control-account protection trusts `SourceModule`; and approval logic must consistently use the workflow engine.

Accounting/compliance impact:

Users can post unauthorized or backdated entries, potentially into control accounts or closed periods.

Proposed implementation approach:

- Add create, update, delete, submit, approve, reject, post, and reverse permissions.
- Remove client trust over `SourceModule` for control-account validation.
- Revalidate tenant, period, book, account, currency, segments, balance, and workflow approval at posting time.
- Require workflow completion before posting where configured.
- Keep posted journal immutable; only reversal is allowed.
- Add journal attachments to audit evidence but ensure attachment access is permissioned and tenant-scoped.

Dependencies:

- Authorization.
- Tenant isolation.
- Posting engine.
- Workflow engine.
- Period close.

Acceptance criteria:

- Draft journals can be edited only before submission/posting.
- Approved journals can be posted only if workflow is complete and period is open.
- Posted journals cannot be edited or deleted.
- Manual journals cannot post to control accounts unless explicitly allowed through a controlled system event.

Test cases:

- Create without `Finance.JournalEntries.Create` returns 403.
- Post into closed period fails.
- Change period from open to closed after draft; posting fails.
- Manual journal with client-supplied `SourceModule = AP` cannot bypass control-account rules.
- Reverse posted journal creates equal and opposite posted journal.

Priority: Critical

Go-live blocker: Yes

### FIN-007 - Rebuild Cash, Bank, And Reconciliation Around GL

Suggested implementation order: 7

Affected files/classes/methods:

- `CashTransactionService.CreateReceiptAsync`
- `CashTransactionService.CreatePaymentAsync`
- `CashTransactionService.CreateTransferAsync`
- `CashTransactionService.DeleteAsync`
- `BankAccountService.UpdateBalanceAsync`
- `BankReconciliationService.StartReconciliationAsync`
- `BankReconciliationService.ApproveReconciliationAsync`
- `BankReconciliationController`
- `CashReportsController`

Problem statement:

Direct cash receipts and payments update bank balances without posting GL journals. Reconciliation uses mutable bank balance instead of posted GL book balance. Cash transactions can be physically deleted when unposted/unreconciled.

Accounting/compliance impact:

Cash/bank records can agree operationally while disagreeing with GL. Bank reconciliation is not audit-grade.

Proposed implementation approach:

- Require every bank account to link to a GL cash/bank account.
- Post receipts, payments, fees, interest, transfers, and reconciliation adjustments through the posting engine.
- Replace cash transaction deletion with cancel/void state and audit log.
- Derive book balance from posted GL transactions for the bank account.
- Use workflow approval for bank reconciliation completion.
- Require unresolved differences to be posted as approved adjustment journals or explicitly carried forward.

Dependencies:

- Authorization.
- Tenant isolation.
- Posting engine.
- Workflow engine.
- Reporting.

Acceptance criteria:

- Cash receipt/payment creates posted or approval-pending journal based on workflow config.
- Reconciliation book balance comes from posted GL, not `BankAccount.CurrentBalance`.
- Approved reconciliations are immutable.
- Reconciliation reports tie to GL cash/bank account.

Test cases:

- Post cash receipt and verify Dr Bank / Cr source account.
- Post cash payment and verify Dr expense/AP/clearing / Cr Bank.
- Bank reconciliation cannot approve if GL book balance and statement reconciliation are inconsistent.
- Reconciliation approval uses workflow assigned approver.
- Voiding a cash transaction creates reversal when already posted.

Priority: Critical

Go-live blocker: Yes

### FIN-008 - Correct AP Invoice, Payment, Allocation, And Void Accounting

Suggested implementation order: 8

Affected files/classes/methods:

- `VendorInvoiceService.SubmitForApprovalAsync`
- `VendorInvoiceService.ApproveAsync`
- `VendorInvoiceService.VoidAsync`
- `VendorPaymentService.CreateAsync`
- `VendorPaymentService.CreatePaymentBatchAsync`
- `VendorPaymentService.ApprovePaymentBatchAsync`
- `VendorPaymentService.ProcessPaymentBatchAsync`
- `VendorPaymentService.ReverseAllocationAsync`
- `VendorPaymentService.VoidPaymentAsync`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostApPaymentAsync`
- `ApControllersConsolidated.cs`

Problem statement:

AP invoices and batches partially use workflow, but direct vendor payments can post immediately. Voids and allocation reversals update operational records without guaranteed GL reversals.

Accounting/compliance impact:

AP control account, cash, discounts, WHT, VAT input, and supplier balances can be misstated.

Proposed implementation approach:

- Route all AP posting through the posting engine.
- Enforce workflow for AP invoice approval, vendor payment authorization, payment batch approval, and voids above configured thresholds.
- Ensure AP invoice posting captures VAT/NHIL/GETFund/WHT/VAT withholding as applicable.
- Make payment allocation accounting explicit for discounts, write-offs, exchange differences, and WHT.
- Void posted invoices/payments only through reversal journals.
- Add AP control account reconciliation report.

Dependencies:

- Posting engine.
- Workflow engine.
- Tax.
- FX.
- Reporting.

Acceptance criteria:

- AP invoice approval posts only after workflow completion.
- Direct vendor payment cannot bypass required workflow.
- Voiding a posted vendor invoice reverses its original GL impact.
- Voiding a posted vendor payment reverses cash/AP GL impact.
- AP aging reconciles to AP control account by tenant, currency, and segment.

Test cases:

- Create AP invoice with Ghana VAT components and verify posted tax lines.
- Approve AP invoice through workflow and verify journal.
- Pay foreign currency AP invoice and verify realized FX.
- Void posted invoice and verify linked reversal.
- AP aging total equals AP control account balance.

Priority: Critical

Go-live blocker: Yes

### FIN-009 - Correct AR Invoice, Receipt, Credit Note, And Bounce Accounting

Suggested implementation order: 9

Affected files/classes/methods:

- `InvoiceService.SendInvoiceAsync`
- `InvoiceService.VoidInvoiceAsync`
- `InvoiceService.GenerateInvoicePrintAsync`
- `PaymentService.CreateAsync`
- `PaymentService.ReverseAllocationAsync`
- `PaymentService.BouncedPaymentAsync`
- `PaymentService.GeneratePaymentReceiptAsync`
- `SubledgerPostingService.PostArInvoiceAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `ArControllersConsolidated.cs`

Problem statement:

AR invoices and payments can update operational balances without consistent reversal accounting. Invoice print and receipt generation are stubs.

Accounting/compliance impact:

AR, revenue, output tax, cash, discounts, credit notes, and bad-debt balances can be misstated. Ghana VAT invoice requirements may not be met.

Proposed implementation approach:

- Route AR invoice, credit note, customer payment, refund, write-off, and bounced payment posting through posting engine.
- Enforce workflow where configured.
- Post Ghana VAT/NHIL/GETFund lines separately.
- Store tax invoice snapshot, rate snapshot, and QR/e-invoice metadata where required.
- Void/bounce posted transactions via reversal journals.
- Complete invoice print and payment receipt generation.
- Add AR control account reconciliation report.

Dependencies:

- Posting engine.
- Workflow engine.
- Tax.
- FX.
- Reporting.

Acceptance criteria:

- AR invoice posting creates Dr AR / Cr revenue / Cr output tax lines.
- Customer receipt creates Dr Bank / Cr AR and realized FX if foreign.
- Credit notes post controlled AR adjustments.
- Bounced payment reverses receipt GL.
- AR aging reconciles to AR control account.

Test cases:

- Create Ghana VAT sales invoice and verify separate VAT, NHIL, GETFund lines.
- Send/post invoice and verify journal.
- Receive payment in foreign currency and verify realized FX.
- Bounce posted payment and verify reversal.
- Print invoice and receipt without `NotImplementedException`.

Priority: Critical

Go-live blocker: Yes

### FIN-010 - Rebuild Period Close, Lock, And Cut-Off Controls

Suggested implementation order: 10

Affected files/classes/methods:

- `FiscalPeriodService.ClosePeriodAsync`
- `FiscalPeriodService.ReopenPeriodAsync`
- `FiscalPeriodService.LockPeriodAsync`
- `FiscalPeriodService.UnlockPeriodAsync`
- `FiscalPeriodService.ValidatePeriodCloseAsync`
- `FiscalPeriodController.ClosePeriod`
- `FiscalPeriodController.ReopenPeriod`
- `FiscalPeriodController.UnlockPeriod`
- `GeneralLedgerService.CloseFiscalPeriodAsync`
- `GeneralLedgerService.ValidatePeriodCloseAsync`

Problem statement:

Period close state is inconsistent; validation can be skipped; route ID is ignored in close; unlock can reopen a closed period; checklist fields are not enforced; posting does not revalidate close state.

Accounting/compliance impact:

Closed books can be changed, cut-off can fail, and statutory reporting can become unreliable.

Proposed implementation approach:

- Normalize period states: `Future`, `Open`, `SoftClosed`, `Closed`, `Locked`.
- Use one period service, deprecating duplicate close logic in GL service if needed.
- Require workflow approval for close, reopen, and unlock.
- Require reason and audit log for reopen/unlock.
- Enforce close checklist: unposted journals, bank reconciliations, AP/AR review, tax review, FX revaluation, depreciation, inventory valuation, trial balance balance.
- Revalidate period and module lock in posting engine at posting time.

Dependencies:

- Workflow engine.
- Posting engine.
- Reporting.
- FX.
- Fixed assets.
- Tax.

Acceptance criteria:

- Close sets `IsClosed` and `PeriodStatus` consistently.
- Unlock does not reopen closed periods unless formal reopen workflow completes.
- Route period ID and request period ID must match.
- Posting into closed/locked period fails.
- Period close validation uses posted ledger only.

Test cases:

- Close period with unposted journal fails.
- Close period with unreconciled bank account fails.
- Close period with missing FX revaluation fails when foreign monetary balances exist.
- Close period with missing depreciation fails when depreciable assets exist.
- Reopen requires workflow approval and reason.

Priority: Critical

Go-live blocker: Yes

### FIN-011 - Rebuild Financial Statements From Posted Ledger

Suggested implementation order: 11

Affected files/classes/methods:

- `GeneralLedgerService.GenerateTrialBalanceAsync`
- `GeneralLedgerService.GenerateBalanceSheetAsync`
- `GeneralLedgerService.GenerateIncomeStatementAsync`
- `GeneralLedgerService.GenerateCashFlowStatementAsync`
- `GeneralLedgerService.CalculateAccountBalanceAsOf`
- `GeneralLedgerService.CalculateAccountActivityForPeriod`
- `GeneralLedgerService.CalculatePostedAccountNetBalanceAsOf`
- Finance report DTOs under `src/ErpSystem.Core/DTOs/Finance`
- frontend report pages under `frontend/src/app/finance/reports`

Problem statement:

Balance sheet, income statement, and cash flow use calculations that do not consistently filter by posted ledger status, tenant, book, deletion state, or book classification.

Accounting/compliance impact:

Financial statements may include drafts, reversed lines, wrong-book transactions, or cross-tenant data.

Proposed implementation approach:

- Create a single ledger query service for posted balances and activity.
- Standardize filters: tenant, book, period/date, account, segment, currency, posting status, deleted state, reversal treatment.
- Implement balance snapshots generated from posted entries for performance.
- Use natural balance signs by account type.
- Add statement drilldown to journal lines.
- Add statement-to-trial-balance reconciliation checks.

Dependencies:

- Posting engine.
- Tenant isolation.
- Segment reporting.
- FX.
- Migration.

Acceptance criteria:

- Trial balance, balance sheet, income statement, and cash flow are generated from posted ledger or controlled snapshots.
- Balance sheet balances.
- Income statement ties to retained earnings/year-close process.
- Cash flow ties to cash/bank GL accounts.
- Reports support tenant, date, period, book, account, segment, branch/cost-center/project/fund/business-unit, customer/supplier where applicable, and currency filters.

Test cases:

- Draft journal does not affect statements.
- Reversed journal is treated according to posted reversal logic and does not double-count.
- Balance sheet equals trial balance control totals.
- Segment-filtered income statement equals sum of segment detail.
- Foreign currency report shows transaction and functional currency balances.

Priority: Critical

Go-live blocker: Yes

### FIN-012 - Implement Segment-Based Reporting And Validation

Suggested implementation order: 12

Affected files/classes/methods:

- `SegmentStructureService`
- `AccountCombinationService`
- `AccountService.CreateAsync`
- `AccountService.UpdateAsync`
- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `GeneralLedgerService` report methods
- `frontend/src/components/finance/generator/SegmentValueSelector.tsx`
- `frontend/src/app/finance/settings/segments/page.tsx`

Problem statement:

The application supports configurable account segments, but reporting and posting validation do not yet consistently treat configured segments as first-class dimensions.

Accounting/compliance impact:

Management and statutory reporting by branch, cost center, department, project, fund, or business unit may be incomplete or unreliable.

Proposed implementation approach:

- Ensure every journal line can carry the required segment values or derive them from account combinations.
- Validate required segments at posting time.
- Add report filters and groupings by configured segment definitions.
- Add balance snapshots by segment combination.
- Prevent posting to invalid segment/account combinations.

Dependencies:

- Posting engine.
- Reporting.
- COA controls.

Acceptance criteria:

- Tenant-configured segment dimensions appear in report filters.
- Posting fails when required segments are missing.
- Segment totals reconcile to unsegmented totals.
- Segment filters work across GL, AP, AR, cash, tax, and fixed asset reports where relevant.

Test cases:

- Create journal with missing required cost center and verify rejection.
- Trial balance by branch totals to full trial balance.
- Income statement by project excludes unrelated project lines.
- AP aging by business unit ties to AP control by business unit.

Priority: High

Go-live blocker: Yes

### FIN-013 - Lock Chart Of Accounts Structural Fields After Use

Suggested implementation order: 13

Affected files/classes/methods:

- `AccountService.UpdateAsync`
- `AccountService.DeleteAsync`
- `AccountService.RemoveCurrencyLinkAsync`
- `AccountService.InactivateCurrencyLinkAsync`
- `AccountController`
- `AccountSegmentValueService`
- `AccountCurrencyLink`

Problem statement:

Accounts with transactions can still change code, account number, segments, control-account flags, direct-posting flags, and currency links.

Accounting/compliance impact:

Historical reports can change meaning after posting, undermining audit trail and comparability.

Proposed implementation approach:

- Lock account code, number, type, category, segments, currency, control-account flag, and direct-posting flag after first posted or draft transaction.
- Allow only safe metadata changes such as display name through audited workflow, if approved.
- Replace force removal of currency links with inactivation and effective dates.
- Add account structural change workflow where exceptional migration is required.

Dependencies:

- Tenant isolation.
- Workflow engine.
- Posting engine.
- Migration.

Acceptance criteria:

- Structural COA fields cannot change after transactions exist.
- Account deletion is soft-delete only for unused accounts; used accounts can only be deactivated.
- Currency links with activity cannot be removed.
- All COA structural changes are audited.

Test cases:

- Attempt to change account code after posted transaction fails.
- Attempt to toggle control-account flag after activity fails.
- Attempt to force-remove currency link with transactions fails.
- Deactivated account cannot receive new postings.

Priority: High

Go-live blocker: Yes

### FIN-014 - Implement Ghana VAT, NHIL, GETFund, VAT Withholding, And WHT

Suggested implementation order: 14

Affected files/classes/methods:

- `TaxCalculationEngine.CalculateTaxesAsync`
- `TaxCalculationEngine.CalculateTaxableAmount`
- `TaxConfigurationService`
- `TaxRuleController`
- `TaxConfigurationController`
- `src/ErpSystem.Core/Entities/Finance/TaxEntities.cs`
- `src/ErpSystem.Core/Entities/Finance/TaxRule.cs`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostArInvoiceAsync`
- frontend tax pages under `frontend/src/app/finance/tax`

Problem statement:

The tax engine exists, but Ghana statutory handling is not complete and AP/AR tax paths are not consistently integrated. Tax rules have tenant and audit weaknesses.

Accounting/compliance impact:

VAT invoices, input tax, output tax, VAT withholding, WHT, and statutory returns can be wrong.

Proposed implementation approach:

- Seed Ghana tax components as effective-dated tenant tax templates: VAT 15%, NHIL 2.5%, GETFund 2.5%, no COVID levy.
- Calculate VAT, NHIL, and GETFund on the same taxable base.
- Store tax line snapshots on posted documents and ledger lines.
- Add VAT withholding support for appointed withholding agents, including 7% withholding per current GRA guidance.
- Add WHT rule engine for resident/non-resident, goods/services/works/rent/dividends/interest/royalties, thresholds, exemptions, certificates, and returns.
- Generate output VAT, input VAT, VAT reconciliation, VAT withholding, and WHT reports from posted ledger/tax lines.
- Ensure invoices display separate VAT, NHIL, and GETFund lines.

Dependencies:

- Posting engine.
- Reporting.
- Tenant isolation.
- Workflow engine for tax configuration approvals.

Acceptance criteria:

- Ghana standard-rated sale posts separate output VAT, NHIL, and GETFund lines.
- Ghana standard-rated purchase posts separate recoverable input VAT/NHIL/GETFund lines when eligible.
- COVID-19 levy is not charged for current effective dates.
- VAT withholding is captured at payment time for applicable agents.
- WHT is calculated and posted according to effective-dated rules.
- Tax reports reconcile to tax control accounts.

Test cases:

- GHS 1,000 taxable sale calculates GHS 150 VAT, GHS 25 NHIL, GHS 25 GETFund.
- VAT exempt and zero-rated supplies do not charge VAT/NHIL/GETFund.
- VAT withholding agent payment records withholding certificate and liability.
- WHT for resident services above threshold calculates configured rate.
- Tax rule change does not alter historical posted invoice tax.

Priority: Critical

Go-live blocker: Yes

### FIN-015 - Make Tax Configuration Tenant-Scoped, Versioned, And Audited

Suggested implementation order: 15

Affected files/classes/methods:

- `TaxRuleController.GetTaxRules`
- `TaxRuleController.GetTaxRule`
- `TaxRuleController.CreateTaxRule`
- `TaxRuleController.UpdateTaxRule`
- `TaxRuleController.DeleteTaxRule`
- `TaxConfigurationService`
- `TaxRule`
- `TaxEntities`

Problem statement:

Tax rule endpoints contain hardcoded tenant behavior, tenantless reads, placeholder product category names, and physical delete.

Accounting/compliance impact:

Tax configuration can be incorrect, cross-tenant, or unauditable.

Proposed implementation approach:

- Remove hardcoded tenant IDs.
- Scope all tax config reads and writes by current tenant.
- Replace delete with deactivate/version.
- Require workflow approval for rate/rule changes.
- Add effective dates and prohibit changing used tax rules.
- Join product/category fields correctly or remove placeholder output.

Dependencies:

- Tenant isolation.
- Workflow engine.
- Tax.
- Audit.

Acceptance criteria:

- Tax rules are tenant-scoped.
- Used tax rules cannot be edited destructively.
- Tax config changes are approved and audited.
- API never returns placeholder category names as final data.

Test cases:

- Tenant A cannot see Tenant B tax rule.
- Used tax rule update creates new version.
- Delete of used tax rule deactivates future use only.
- Tax rule change requires configured approval.

Priority: High

Go-live blocker: Yes

### FIN-016 - Implement Tenant Functional Currency Controls

Suggested implementation order: 16

Affected files/classes/methods:

- `CurrencyService.SetBaseCurrencyAsync`
- `CurrencyService.CreateCurrencyAsync`
- `CurrencyService.UpdateExchangeRateAsync`
- `CurrencyService.ConvertAsync`
- `CurrenciesController`
- `FinanceSettingsService`
- `FinanceSettings`
- `Currency`

Problem statement:

Base currency can be changed after transactions, conversion can silently return the original amount when no rate exists, and one placeholder method returns success without implementation.

Accounting/compliance impact:

Functional currency statements and foreign currency transactions can be materially wrong.

Proposed implementation approach:

- Store tenant functional currency in finance settings or tenant finance profile.
- Lock functional currency after any accounting transaction exists.
- Add formal migration workflow for exceptional functional currency change.
- Make missing exchange rates fail loudly for financial posting/reporting.
- Remove placeholder conversion/update methods.

Dependencies:

- Tenant isolation.
- FX.
- Workflow engine.
- Migration.

Acceptance criteria:

- Each tenant can set functional currency before transactions exist.
- Functional currency cannot change after ledger activity without migration workflow.
- Missing rate causes validation error, not silent pass-through.
- All posted foreign transactions store transaction and functional currency values.

Test cases:

- New tenant sets GHS functional currency successfully.
- Tenant with posted journal cannot change functional currency.
- Foreign AP invoice without rate fails validation.
- `ConvertAsync` throws or returns structured failure when rate is missing.

Priority: Critical

Go-live blocker: Yes

### FIN-017 - Implement Exchange Rate Governance And Snapshots

Suggested implementation order: 17

Affected files/classes/methods:

- `ExchangeRateService.CreateExchangeRateAsync`
- `ExchangeRateService.UpdateExchangeRateAsync`
- `ExchangeRateService.DeleteExchangeRateAsync`
- `ExchangeRateService.HasExistingTransactionsUsingRate`
- `ExchangeRate`
- `ExchangeRateController`
- `AccountTransaction`
- `JournalEntry`

Problem statement:

Exchange rates allow weak validation, updates/deletes can affect used rates, and transaction matching to used rates is not reliable.

Accounting/compliance impact:

Historical FX amounts may become unreproducible, violating audit expectations and IAS 21-style handling.

Proposed implementation approach:

- Require positive rates and inverse rates.
- Add rate type, source, effective date/time, tenant, approval status, and versioning.
- Store exchange-rate snapshot fields on every posted foreign ledger line.
- Prevent editing/deleting rates used by transactions; create new versions instead.
- Add workflow approval for exchange rate imports and manual overrides.

Dependencies:

- FX.
- Workflow engine.
- Posting engine.
- Audit.

Acceptance criteria:

- Used rates cannot be edited or deleted.
- Posted ledger lines retain exact rate snapshot.
- Approved rate source is visible in audit trail.
- Rate import rejects duplicates/conflicts according to tenant policy.

Test cases:

- Create rate with zero or negative value fails.
- Edit used rate creates new version or fails.
- Posted foreign invoice remains reproducible after new rate version.
- Manual rate override requires workflow approval.

Priority: Critical

Go-live blocker: Yes

### FIN-018 - Implement Realized And Unrealized FX Accounting

Suggested implementation order: 18

Affected files/classes/methods:

- `GeneralLedgerService.RunCurrencyRevaluationCoreAsync`
- `CurrencyRevaluationService`
- `ICurrencyRevaluationService`
- `SubledgerPostingService.PostApPaymentAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `PaymentService.CreateAsync`
- `VendorPaymentService.CreateAsync`
- `AccountTransaction`
- `RevaluationDtos`

Problem statement:

Currency revaluation reads transactions without sufficient filters, skips missing rates, and creates posted journals outside the standard posting path. Realized FX on settlements is not consistently handled.

Accounting/compliance impact:

Foreign monetary items, AP/AR settlements, bank FX accounts, and FX gains/losses can be misstated.

Proposed implementation approach:

- Move FX revaluation into posting engine.
- Revalue foreign monetary balances by tenant, book, account, segment, currency, and period.
- Fail when required closing rates are missing.
- Generate unrealized FX journals with reversal option in next period.
- Calculate realized FX at settlement by comparing invoice/bill recognition rate and payment rate.
- Support bank FX accounts and functional currency reporting.

Dependencies:

- Posting engine.
- Exchange rates.
- Reporting.
- Period close.

Acceptance criteria:

- Foreign AP/AR balances revalue using approved closing rates.
- Missing closing rate blocks period close and revaluation.
- Revaluation journals are posted through standard workflow/posting rules.
- Realized FX gain/loss posts on settlement.
- Revaluation history is auditable.

Test cases:

- Foreign AR invoice revalues at period end and reverses next period if configured.
- Foreign AP payment posts realized FX difference.
- Missing USD/GHS closing rate blocks revaluation.
- Revaluation excludes draft, deleted, reversed, and wrong-book transactions.

Priority: Critical

Go-live blocker: Yes

### FIN-019 - Complete Fixed Asset Acquisition And Capitalization

Suggested implementation order: 19

Affected files/classes/methods:

- `FixedAssetService.CreateAsync`
- `FixedAssetService.ActivateAsync`
- `FixedAssetService.DeleteAsync`
- `FinancePurchaseOrderService`
- `VendorInvoiceService`
- `SubledgerPostingService.PostApInvoiceAsync`
- `FixedAsset`
- `FixedAssetBookValue`
- `FixedAssetCategory`

Problem statement:

Fixed asset records can be created and activated without complete GL capitalization. Deletion can physically remove assets.

Accounting/compliance impact:

Fixed asset register may not reconcile to GL asset control accounts.

Proposed implementation approach:

- Support acquisition from AP invoice, cash purchase, opening balance, and capital project.
- Post Dr fixed asset / Cr AP, cash, clearing, or CIP through posting engine.
- Prevent activation unless capitalization is posted or opening migration is approved.
- Replace delete with deactivate/cancel for unused drafts; prohibit delete after accounting activity.
- Store book value by tenant, book, currency, and asset.

Dependencies:

- Posting engine.
- AP.
- Workflow engine.
- Migration.

Acceptance criteria:

- Asset acquisition creates or links to capitalization journal.
- Active asset has reconciled book value.
- Asset delete is prohibited after any transaction.
- Asset register ties to fixed asset control accounts.

Test cases:

- Capitalize AP invoice line into fixed asset.
- Activate asset only after posted acquisition.
- Delete draft unused asset succeeds or cancels according to policy.
- Delete active asset fails.
- Fixed asset register total equals GL asset account balance.

Priority: Critical

Go-live blocker: Yes

### FIN-020 - Complete Fixed Asset Depreciation

Suggested implementation order: 20

Affected files/classes/methods:

- `FixedAssetDepreciationService`
- `FixedAssetDepreciationService.PostDepreciationToGlAsync`
- `AssetDepreciationSchedule`
- `FixedAssetBookValue`
- `FiscalPeriod`
- `FixedAssetReportsService`

Problem statement:

Depreciation posting exists but must be integrated with period close, workflow, segment reporting, and GL reconciliation.

Accounting/compliance impact:

Depreciation expense and accumulated depreciation can be missing, duplicated, or posted into the wrong period.

Proposed implementation approach:

- Support required depreciation methods: straight-line, reducing balance, units of production, and component-level where configured.
- Generate depreciation schedules by book and asset component.
- Post depreciation through posting engine.
- Mark period depreciation complete only from posted journals.
- Block period close when depreciation is incomplete for active depreciable assets.

Dependencies:

- Posting engine.
- Fixed assets.
- Period close.
- Reporting.

Acceptance criteria:

- Depreciation is calculated by configured method and book.
- Posted depreciation creates Dr depreciation expense / Cr accumulated depreciation.
- Depreciation cannot post into closed period.
- Depreciation report reconciles to GL.

Test cases:

- Straight-line asset depreciates correct monthly amount.
- Reducing-balance asset depreciates correctly.
- Units-of-production method uses usage quantity.
- Period close fails when depreciation not posted.
- Depreciation reversal/adjustment uses journal reversal.

Priority: Critical

Go-live blocker: Yes

### FIN-021 - Complete Fixed Asset Revaluation, Impairment, Transfers, And Disposals

Suggested implementation order: 21

Affected files/classes/methods:

- `AssetValuationService.CreateValuationAsync`
- `AssetValuationService.PostValuationToGLAsync`
- `AssetTransferService`
- `AssetDisposalService.RequestDisposalAsync`
- `AssetDisposalService.ApproveDisposalAsync`
- `AssetDisposalService.CompleteDisposalAsync`
- `AssetVerificationService`
- `FixedAssetReportsService`

Problem statement:

Valuation updates asset NBV before GL posting; disposal uses an asset account placeholder for sale proceeds; transfers need segment and location accounting; impairment must be explicit.

Accounting/compliance impact:

Asset values, accumulated depreciation, revaluation surplus, impairment loss, cash/receivable proceeds, and gain/loss on disposal can be misstated.

Proposed implementation approach:

- Make valuation/revaluation workflow approval-driven before GL impact.
- Do not update NBV until posting succeeds.
- Add impairment workflow and GL posting.
- For disposal, post asset cost, accumulated depreciation, proceeds to cash/AR, and gain/loss correctly.
- For transfers, update custody/location/segment and post segment reclassification if required.
- Add fixed asset GL reconciliation report.

Dependencies:

- Posting engine.
- Workflow engine.
- Fixed assets.
- Reporting.

Acceptance criteria:

- Revaluation, impairment, transfer, and disposal require configured workflows.
- Disposal no longer uses asset account as proceeds placeholder.
- Asset register remains reconciled to GL after every lifecycle event.
- Asset transaction history is immutable.

Test cases:

- Revalue asset upward and post revaluation surplus.
- Impair asset and post impairment loss.
- Transfer asset between cost centers and verify segment reporting.
- Dispose asset with gain and verify cash/AR and gain/loss.
- Dispose asset with loss and verify correct expense.

Priority: Critical

Go-live blocker: Yes

### FIN-022 - Complete Finance Workflow Entity Coverage And Seeded Definitions

Suggested implementation order: 22

Affected files/classes/methods:

- `SimpleWorkflowService.BuildEntityContextAsync`
- `SimpleWorkflowService.AttachWorkflowInstanceAsync`
- `FinanceApprovalsController.FinanceWorkflowEntityKeys`
- `FinanceApprovalsController.ResolveFactsAsync`
- `FinanceApprovalsController.ApplyApprovedOutcomeAsync`
- `FinanceApprovalsController.ApplyRejectedOutcomeAsync`
- `WorkflowEngine.StartWorkflowAsync`
- `WorkflowDefinition`
- `WorkflowStep`
- `WorkflowApproval`
- workflow seeders and admin UI under `frontend/src/app/administration/workflow`

Problem statement:

Finance workflow coverage is partial. Several Finance approval actions either bypass workflow or have incomplete outcome handling.

Accounting/compliance impact:

Approval controls are inconsistent, and maker-checker requirements cannot be relied on.

Proposed implementation approach:

- Define Finance workflow entity types for all approval-required transactions.
- Add entity context variables needed for workflow routing: amount, currency, tenant, source module, account type, supplier/customer, bank account, period, segment, risk flags, tax amount, FX exposure, fixed asset category.
- Seed baseline workflows per tenant for journal, AP invoice, vendor payment, payment batch, AR credit note/refund, cash transaction, bank reconciliation, period close/reopen, tax config, exchange rate, asset acquisition, asset transfer, asset valuation, impairment, disposal, depreciation run, and migration adjustment.
- Use configurable workflow thresholds and roles.
- Centralize Finance approval queue and outcome application.

Dependencies:

- Workflow engine.
- Authorization.
- Posting engine.
- Tenant isolation.

Acceptance criteria:

- Every approval-required Finance action maps to a workflow entity type.
- Workflow definitions are tenant-scoped and configurable.
- Approved workflow outcome calls the correct domain service/posting action.
- Rejected workflow outcome returns source document to correct state with reason.

Test cases:

- Submit each Finance workflow entity and verify workflow instance and approval rows.
- Approval by non-assigned user fails.
- Approval completion applies domain outcome exactly once.
- Rejection records reason and does not post.
- Threshold routing sends high-value payment to configured senior approver.

Priority: Critical

Go-live blocker: Yes

### FIN-023 - Implement Finance Audit Log And Immutability Enforcement

Suggested implementation order: 23

Affected files/classes/methods:

- `JournalEntryService`
- `AccountService`
- `TaxRuleController`
- `ExchangeRateService`
- `FiscalPeriodService`
- `BankReconciliationService`
- `VendorInvoiceService`
- `VendorPaymentService`
- `InvoiceService`
- `PaymentService`
- `FixedAssetService`
- `ApplicationDbContext`

Problem statement:

Audit metadata exists in places, but there is no consistent immutable audit log for sensitive Finance actions. Some records can be physically removed or materially changed.

Accounting/compliance impact:

Auditors cannot reliably trace who changed financial configuration or posted financial events.

Proposed implementation approach:

- Add `FinanceAuditEvent` or extend an existing audit/event system.
- Log before/after changes for sensitive configuration.
- Log posting, reversal, void, approval, rejection, period close/reopen, reconciliation, tax config, FX rate, COA, and migration adjustment events.
- Enforce immutability for posted entries and posted source documents.
- Require reason fields for reversals, voids, reopen/unlock, force changes, and migration adjustments.

Dependencies:

- Posting engine.
- Workflow engine.
- Authorization.
- Migration.

Acceptance criteria:

- Every sensitive Finance action produces an audit event.
- Posted journal line edits/deletes are blocked.
- Sensitive config changes are versioned or audited.
- Audit events are tenant-scoped and reportable.

Test cases:

- Post journal and verify audit event.
- Attempt to edit posted journal line fails and logs denied attempt where appropriate.
- Tax rate change logs old/new values and reason.
- Period reopen requires reason and logs workflow approval.

Priority: Critical

Go-live blocker: Yes

### FIN-024 - Data Migration, Cleanup, And Accounting Sign-Off

Suggested implementation order: 24

Affected files/classes/methods:

- `src/ErpSystem.Data/Migrations/*`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- Finance entities under `src/ErpSystem.Core/Entities/Finance`
- posting and reporting services after remediation
- scripts to be added under `tools` or `scripts` if repo convention exists

Problem statement:

Existing accounting data must be cleaned and reconciled before go-live.

Accounting/compliance impact:

Even correct code will produce incorrect books if legacy data contains orphaned, duplicated, cross-tenant, unposted, or unreconciled transactions.

Proposed implementation approach:

- Build migration diagnostics by tenant.
- Identify orphaned journals, documents without journals, journals without source docs, unbalanced entries, draft entries in closed periods, duplicated postings, invalid tenant references, invalid currency/rate snapshots, invalid tax lines, invalid asset book values.
- Rebuild balances from posted GL.
- Reconcile AP, AR, bank, tax, fixed assets, and FX subledgers to GL.
- Generate adjustment journals through posting engine.
- Produce accountant sign-off pack.

Dependencies:

- Posting engine.
- Reporting.
- Tax.
- FX.
- Fixed assets.
- Period close.

Acceptance criteria:

- Migration diagnostics run per tenant.
- Every exception is classified, fixed, or formally accepted.
- Opening trial balance is signed off.
- Subledger control accounts reconcile to GL.
- Opening periods are locked after sign-off.

Test cases:

- Run diagnostics against seeded dirty dataset and verify findings.
- Rebuild balances and verify totals.
- Generate and post approved migration adjustment.
- Validate no orphaned posted source documents remain.
- Validate locked opening period blocks normal posting.

Priority: Critical

Go-live blocker: Yes

### FIN-025 - Complete Frontend Finance Readiness

Suggested implementation order: 25

Affected files/classes/methods:

- `frontend/src/app/finance/ap/receipts/[id]/page.tsx`
- `frontend/src/app/finance/ap/reports/page.tsx`
- `frontend/src/app/finance/ar/customers/[id]/page.tsx`
- `frontend/src/app/finance/ar/reports/page.tsx`
- `frontend/src/app/finance/unit-types/[id]/page.tsx`
- `frontend/src/app/finance/unit-types/new/page.tsx`
- `frontend/src/app/finance/allocations/[id]/page.tsx`
- `frontend/src/app/finance/ratio-definitions/calculator/page.tsx`
- `frontend/src/app/finance/tax/configuration/taxes/[id]/page.tsx`
- `frontend/src/services/finance/*`

Problem statement:

Frontend type-check fails and some Finance pages contain TODOs, mock data, or incomplete API integration.

Accounting/compliance impact:

Users may rely on stale/mock information or be unable to perform controlled Finance workflows.

Proposed implementation approach:

- Fix TypeScript errors.
- Replace mock data with API calls or hide unfinished routes.
- Add permission-aware actions based on backend permissions.
- Add workflow status panels for approval-required actions.
- Surface accounting validation errors clearly.
- Add report filters for tenant, period, book, account, segment, currency, customer, supplier, and branch-like dimensions.

Dependencies:

- Authorization.
- Workflow engine.
- Reporting.
- Tax.
- FX.
- Fixed assets.

Acceptance criteria:

- `npm.cmd run type-check` passes.
- `npm.cmd run lint:check` passes.
- No go-live Finance page uses mock data.
- UI actions match backend workflow states.
- Posting/approval/period-lock errors are displayed accurately.

Test cases:

- Type-check and lint.
- User without permission does not see mutation buttons and backend still denies direct call.
- Approval queue displays journal, AP, AR, bank reconciliation, FX, tax config, and fixed asset approvals.
- Report filters produce expected API requests.

Priority: High

Go-live blocker: Yes

### FIN-026 - Complete Print, Export, And Statutory Output Artifacts

Suggested implementation order: 26

Affected files/classes/methods:

- `InvoiceService.GenerateInvoicePrintAsync`
- `PaymentService.GeneratePaymentReceiptAsync`
- `ApReportsService` export methods
- `ArReportsService` export methods
- `src/ErpSystem.Api/Services/Documents/Finance/*`
- Finance report frontend pages

Problem statement:

Some Finance print/export methods are stubs or incomplete.

Accounting/compliance impact:

Users cannot reliably produce invoices, receipts, reports, or audit evidence required for operations and statutory compliance.

Proposed implementation approach:

- Implement invoice PDF/print with Ghana VAT/NHIL/GETFund lines.
- Implement payment receipt output.
- Implement AP/AR aging and statement exports.
- Implement tax reports and reconciliation exports.
- Add export audit logs.

Dependencies:

- Tax.
- Reporting.
- Audit.

Acceptance criteria:

- Invoice print no longer throws `NotImplementedException`.
- Receipt print no longer throws `NotImplementedException`.
- AP/AR reports export consistently with on-screen results.
- Tax report exports reconcile to ledger.

Test cases:

- Generate Ghana VAT invoice PDF with separate tax lines.
- Generate customer receipt.
- Export AP aging and compare totals to API response.
- Export VAT reconciliation and compare to posted tax ledger.

Priority: Medium

Go-live blocker: Yes for statutory invoice/receipt/report outputs; otherwise High

### FIN-027 - Final Go-Live Accounting Scenario Pack

Suggested implementation order: 27

Affected files/classes/methods:

- End-to-end tests under `e2e-tests/tests/finance.spec.ts`
- Backend integration tests under `tests/ErpSystem.Api.Tests/Services/Finance`
- Frontend Finance pages under `frontend/src/app/finance`
- All remediated Finance services

Problem statement:

The Finance module needs accountant-approved scenario testing after remediation.

Accounting/compliance impact:

Go-live without scenario testing can miss cross-module errors not visible in unit tests.

Proposed implementation approach:

- Build a scenario pack with known expected journals and reports.
- Include Ghana tax, FX, fixed assets, workflow approval, period close, and migration cases.
- Obtain sign-off from accounting owner.

Dependencies:

- All critical remediation tickets.

Acceptance criteria:

- Scenario pack passes for at least one clean tenant and one migrated tenant.
- Accountant signs off trial balance, balance sheet, income statement, AP aging, AR aging, bank reconciliation, VAT/WHT reports, FX revaluation, and fixed asset register.
- Go-live readiness checklist is complete.

Test cases:

- Quote/order/invoice/cash receipt lifecycle.
- Purchase/AP invoice/vendor payment lifecycle.
- Foreign currency AP/AR and FX revaluation.
- Bank statement import and reconciliation.
- Asset acquisition, depreciation, revaluation, impairment, transfer, disposal.
- VAT/NHIL/GETFund, VAT withholding, and WHT reporting.
- Month close, reopen, adjustment, re-close.
- Migration adjustment and opening balance lock.

Priority: Critical

Go-live blocker: Yes

### FIN-028 - Remove COA Read-Path Backfill And Bound Account Queries

Status: To be worked on

Date observed: 2026-09-01

Suggested implementation order: 3

Affected files/classes/methods:

- `src/ErpSystem.Api/Services/Finance/GL/AccountService.cs` (`GetAllAsync`)
- `src/ErpSystem.Api/Services/Finance/Settings/AccountingBookService.cs` (`EnsureTenantDefaultsAsync`, `BackfillAccountMappingsAsync`)
- `src/ErpSystem.Api/Controllers/Finance/AccountController.cs` (`GetAllAccounts`)
- `frontend/src/app/finance/accounts/page.tsx`
- `frontend/src/services/api.service.ts`

Problem statement:

Loading the Chart of Accounts can exceed the frontend's 20-second request deadline. The GET path currently calls accounting-book default creation and mapping backfill before reading accounts. That backfill loads all tenant accounts and mappings, scans mappings per account, unconditionally updates mapping audit fields, and saves changes. The subsequent account read is unbounded and includes multiple child collections in one tracked EF query, increasing row multiplication, memory use, database work, and lock contention. Client cancellation is then surfaced as HTTP 500 rather than a cancellation response.

Observed evidence:

- `GET /api/finance/accounts?coaType=Segmented` timed out in the browser after 20 seconds.
- The server recorded cancellation in `AccountService.GetAllAsync` after approximately 26.3 seconds.
- A separate already-cancelled request stopped in HR identity access and was also logged as HTTP 500.

Proposed implementation approach:

- Remove tenant-default creation and historical mapping backfill from the COA GET request.
- Run backfill through migration/startup repair or an explicit idempotent administrative operation.
- Change mapping reconciliation to indexed lookups and update only records whose business values changed.
- Make COA reads no-tracking and use a bounded projection or split query that avoids collection Cartesian expansion.
- Add server-side paging, search, type/status filters, and a safe maximum page size; stop loading and filtering the full COA in the browser.
- Treat request-aborted `OperationCanceledException` as client cancellation instead of logging it as an application HTTP 500.
- Do not increase the frontend timeout as the primary remedy.

Acceptance criteria:

- A COA GET performs no inserts or updates.
- The first and subsequent segmented-COA page loads complete within the agreed performance budget under representative tenant volume.
- SQL/query telemetry proves bounded rows and no collection Cartesian explosion.
- Concurrent COA requests do not block each other on accounting-book mappings.
- Browser cancellation is not reported as an application HTTP 500.
- Pagination, filtering, and account ordering remain tenant-scoped and deterministic.

Test cases:

- Load the first COA page for a tenant with representative account, segment, and accounting-book volumes.
- Execute concurrent identical COA reads and verify no database writes or lock waits are introduced.
- Verify paging and filters cannot expose another tenant's accounts.
- Cancel a request and verify cancellation handling and logging.
- Run a separate accounting-book repair and prove it is idempotent.

Priority: High

Go-live blocker: Yes for representative-volume tenants

## Dependency Matrix

| Dependency | Tickets |
|---|---|
| Tenant isolation | FIN-002, FIN-003, FIN-004, FIN-007, FIN-011, FIN-015, FIN-016, FIN-024 |
| Posting engine | FIN-004 through FIN-021, FIN-024, FIN-027 |
| Workflow engine | FIN-006, FIN-008, FIN-010, FIN-013, FIN-015, FIN-017, FIN-021, FIN-022, FIN-023 |
| Reporting | FIN-005, FIN-007, FIN-008, FIN-009, FIN-011, FIN-012, FIN-014, FIN-018, FIN-019 through FIN-021, FIN-024 |
| Tax | FIN-008, FIN-009, FIN-014, FIN-015, FIN-026, FIN-027 |
| FX | FIN-008, FIN-009, FIN-016, FIN-017, FIN-018, FIN-024, FIN-027 |
| Migration | FIN-005, FIN-013, FIN-016, FIN-019, FIN-024, FIN-027 |
| COA performance | FIN-003, FIN-013, FIN-025, FIN-028 |

## Go-Live Readiness Gate

Finance should not go live until all Critical tickets and all tickets marked as go-live blockers are complete, tested, and signed off. Because multi-currency, fixed assets, Ghana statutory tax, migration, and multi-tenant production are all in scope, partial go-live is not recommended unless the relevant modules are explicitly disabled and contractually out of scope for the release.
