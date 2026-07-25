# Finance Go-Live Implementation Sequencing Plan

Date prepared: 2026-07-03

This document turns `docs/finance-go-live-remediation-backlog.md` into PR-sized implementation batches. It is an execution plan, not an application-code change.

Execution constraints:

- Do not implement a parallel approval system. Finance approvals must use the existing workflow engine: `IWorkflowEngine`, `SimpleWorkflowService`, `FinanceApprovalsController`, workflow definitions, workflow steps, workflow instances, and workflow approvals.
- Do not create new Finance posting paths. Implement one controlled Finance posting engine, then migrate workflows onto it.
- The posted General Ledger is the accounting source of truth. Stored balances are rebuildable read models or controlled snapshots only.
- Tenant isolation is by `TenantId` only and must apply to every query, command, workflow, posting, report, tax rule, FX rate, asset transaction, and migration diagnostic.
- Each tenant may configure its functional currency. Functional currency is locked after accounting activity unless a controlled migration workflow changes it.
- Multi-currency must support IFRS-style transaction currency, functional currency, exchange-rate snapshots, realized FX, unrealized FX revaluation, AP/AR revaluation, bank FX treatment, and revaluation journals.
- Ghana statutory handling must reflect current GRA rules: no COVID-19 Health Recovery Levy, VAT 15%, NHIL 2.5%, GETFund 2.5%, VAT withholding where applicable, WHT, effective dating, tenant-specific tax configuration, and auditability.
- Existing accounting data must be migrated, cleaned, reconciled, and signed off.
- GL reporting must support tenant-level and configured segment-based reporting.
- Every implementation batch includes automated tests.

Reference sources:

- IFRS IAS 1: https://www.ifrs.org/issued-standards/list-of-standards/ias-1-presentation-of-financial-statements/
- IFRS IAS 21: https://www.ifrs.org/issued-standards/list-of-standards/ias-21-the-effects-of-changes-in-foreign-exchange-rates/
- GRA VAT: https://gra.gov.gh/domestic-tax/tax-types/vat/
- GRA VAT Withholding: https://gra.gov.gh/domestic-tax/tax-types/vat-withholding/
- GRA WHT: https://gra.gov.gh/domestic-tax/tax-types/withholding-tax/

## Per-Batch Execution Controls

Every implementation PR must include a short Definition of Done checklist with:

- Backend build status.
- Frontend build/type-check impact, if applicable.
- Tests added or updated.
- Migrations added, if applicable.
- Rollback considerations.
- Tenant-isolation verification.
- Accounting impact note.

Every Finance batch must also maintain `docs/finance-go-live-limitations-register.md`:

- Add any new limitation, deferred behavior, workaround, or diagnostic follow-up with a stable `FIN-LIM-0000` style ID.
- Update existing limitation statuses when a batch resolves, narrows, or accepts a limitation.
- Reference limitation IDs in the PR summary.
- State whether each remaining limitation blocks final go-live, the next batch, both, or neither.
- Mark `Blocks next batch = Yes` only when the limitation weakens a foundation the next batch depends on, such as posting engine correctness, tenant isolation, audit integrity, functional currency readiness, period enforcement, or posted-GL reporting integrity.

Batch 1 must explicitly separate:

- Tests that must pass now.
- Diagnostics that are expected to fail or remain skipped because they expose known gaps.
- Follow-up tickets created from those diagnostics.

## Audit Trail Foundation Ownership

The repo already has central audit infrastructure:

- `src/ErpSystem.Data/Interceptors/AuditInterceptor.cs` records EF Core create/update/delete changes and captures user, tenant, IP address, user agent, old values, and new values when user context is available.
- `src/ErpSystem.Core/Entities/LogEntities.cs` defines `AuditLog` with `TenantId`, user, action, resource, resource ID, old/new values, IP address, user agent, and timestamp.
- `src/ErpSystem.Core/Services/LogService.cs` provides `IAuditLogService` and `AuditLogService`.
- `src/ErpSystem.Api/Controllers/AuditLogController.cs` and `src/ErpSystem.Api/Controllers/Optimized/OptimizedAuditLogController.cs` expose audit log query APIs.
- `src/ErpSystem.Core/Services/Workflow/WorkflowActivityService.cs` separately records workflow activity history.

This is not enough by itself for Finance go-live. Finance needs a dedicated audit foundation that defines event semantics for posting, reversal, workflow outcomes, exports, migration adjustments, and accounting sign-off. That ownership is assigned to **Batch 7A - Finance Audit Trail Foundation**. Later batches may add module-specific audit events, but they should use the Batch 7A conventions.

## Recommended First PR

Start with **Batch 1: test baseline, authorization, and tenant-isolation diagnostics**.

Reason:

- It does not alter accounting behavior yet.
- It creates the safety net needed for the posting engine work.
- It blocks anonymous and unauthorized Finance access early.
- It exposes tenant-isolation violations before larger posting refactors depend on those services.

Do not start with Ghana tax, fixed assets, or frontend cleanup. Those depend on the posting engine, tenant isolation, workflow integration, and ledger-as-source-of-truth reporting foundations.

## Phase Map

| Phase | Goal | Batches |
|---|---|---|
| Phase 0 | Baseline and security foundation | Batch 1, Batch 2, Batch 3 |
| Phase 1 | Ledger source-of-truth and posting foundation | Batch 4, Batch 5, Batch 6 |
| Phase 2 | Workflow and audit foundations | Batch 7, Batch 7A |
| Phase 3 | Core subledger workflows | Batch 8, Batch 9, Batch 10 |
| Phase 4 | Period close and reporting | Batch 11, Batch 12, Batch 13, Batch 14 |
| Phase 5 | Ghana statutory tax | Batch 15, Batch 16 |
| Phase 6 | Multi-currency and FX | Batch 17, Batch 18 |
| Phase 7 | Fixed assets | Batch 19, Batch 20, Batch 21 |
| Phase 8 | Frontend, outputs, migration, and sign-off | Batch 22, Batch 23, Batch 24, Batch 25 |

## Batch 1 - Test Baseline, Authorization, And Tenant-Isolation Diagnostics

Tickets included:

- FIN-001 partial
- FIN-002 diagnostic coverage
- FIN-003 diagnostic coverage

Files/classes/methods to be changed:

- `tests/ErpSystem.Tests/Services/Finance/*`
- `tests/ErpSystem.Api.Tests/Services/Finance/*`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/*` if not present, add
- `src/ErpSystem.Api/Controllers/Finance/BankAccountController.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashTransactionController.cs`
- `src/ErpSystem.Api/Controllers/Finance/BankReconciliationController.cs`
- `src/ErpSystem.Api/Controllers/Finance/CashReportsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs`
- `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`
- `src/ErpSystem.Api/Controllers/Finance/ArControllersConsolidated.cs`

Database/entity/model changes:

- None expected.
- Add only test fixtures/build helpers.

Migration impact:

- None.

API impact:

- Add or validate `[Authorize]` on exposed Finance controllers.
- Add temporary or permanent diagnostics tests identifying endpoints missing permission enforcement.
- Do not redesign APIs in this batch.

Frontend impact:

- None, except documenting that frontend permissions remain non-authoritative.

Test coverage:

- Restore Finance test project compilation.
- Add anonymous access tests for cash, bank, reconciliation, cash reports.
- Add unauthorized access tests for journal create/post/reverse, AP payment create/process, AR payment create, period close.
- Add cross-tenant diagnostic tests for journal, bank account, cash transaction, invoice, payment, tax rule, exchange rate, and asset lookups.

Dependencies:

- None.

Risks:

- Existing tests may reveal unrelated compile failures outside Finance.
- Some tests may need test infrastructure updates before they can express authorization scenarios cleanly.

Expected acceptance criteria:

- Backend API build passes.
- Finance tests compile.
- New security/tenant diagnostics fail where current behavior is unsafe.
- No accounting behavior is refactored yet.

## Batch 2 - Finance Permission Matrix And Controller Enforcement

Tickets included:

- FIN-002

Files/classes/methods to be changed:

- `src/ErpSystem.Api/Controllers/Finance/*`
- `src/ErpSystem.Data/Services/PermissionService.cs`
- `src/ErpSystem.Data/Services/RolePermissionService.cs`
- `src/ErpSystem.Core/Entities/Permission.cs`
- `frontend/src/lib/permissions.ts`
- `frontend/src/components/layout/sidebar.tsx`

Database/entity/model changes:

- Seed or migrate Finance permissions for read, create, edit, delete, submit, approve, reject, post, reverse, void, reconcile, close, reopen, export, tax config, FX rate, COA admin, fixed asset admin, and migration.

Migration impact:

- Permission seed migration or idempotent seeder update.

API impact:

- Add action-level permission enforcement to Finance controllers.
- Apply consistent 401/403 behavior.
- Approval endpoints must require both workflow authorization and Finance action permission.

Frontend impact:

- Update permission constants.
- Disable or hide Finance actions based on permission, while preserving backend enforcement.

Test coverage:

- Role-based API tests per major Finance action.
- Ensure read-only user cannot mutate Finance data.
- Ensure assigned workflow approver without required Finance permission cannot approve.

Dependencies:

- Batch 1.

Risks:

- Permission names currently vary across controllers.
- Some existing users/roles may lose access until seeded roles are updated.

Expected acceptance criteria:

- No Finance mutation endpoint is accessible without explicit permission.
- Finance permission matrix is documented and seeded.
- Frontend permission names match backend permission names.

## Batch 3 - Tenant Guard And High-Risk Tenant Isolation Fixes

Tickets included:

- FIN-003

Files/classes/methods to be changed:

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
- `TaxRuleController`
- `ExchangeRateService`
- fixed asset services under `src/ErpSystem.Api/Services/Finance/FixedAssets`

Database/entity/model changes:

- Add tenant-scoped indexes where lookup patterns need them.
- No schema change unless missing tenant fields are discovered.

Migration impact:

- Index migration likely.

API impact:

- Cross-tenant IDs should return 404 or 403 consistently.
- DTO references must be tenant-validated before posting or updates.

Frontend impact:

- None expected.

Test coverage:

- Two-tenant integration tests for every high-risk Finance service.
- Cross-tenant posting, approval, report, and reconciliation attempts fail.

Dependencies:

- Batch 1.
- Batch 2 recommended.

Risks:

- Existing data may contain invalid cross-tenant references; migration diagnostics will need to flag them later.
- Generic repositories may hide tenant scoping.

Expected acceptance criteria:

- `TenantId` is enforced across high-risk Finance reads and mutations.
- No Finance posting can reference another tenant's account, period, vendor, customer, bank, tax rule, exchange rate, or asset.

## Batch 4 - Finance Posting Engine Contract And Ledger Validation Boundary

Tickets included:

- FIN-004 foundation

Files/classes/methods to be changed:

- Add `src/ErpSystem.Core/Interfaces/Finance/IFinancePostingEngine.cs`
- Add `src/ErpSystem.Api/Services/Finance/Posting/FinancePostingEngine.cs`
- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `JournalEntryService.ReverseJournalEntryAsync`
- `GeneralLedgerService.PostJournalEntryAsync`
- `SubledgerPostingService`
- `FinanceServiceCollectionExtensions.cs`

Database/entity/model changes:

- Add posting event/idempotency model, for example `FinancePostingEvent`.
- Add source document type, source document ID, event type, journal entry ID, reversal journal entry ID, status, tenant ID, book ID, posting date, and idempotency key.
- The posting event model must cover journal, AP invoice, AP payment, AR invoice, AR receipt, bank transaction, reconciliation adjustment, tax posting, FX revaluation, depreciation, disposal, and migration adjustment events.

Migration impact:

- New posting event table and unique index on tenant/source/event/idempotency key.

API impact:

- No broad API change yet.
- Services begin consuming posting engine internally.

Frontend impact:

- None expected.

Test coverage:

- Posting engine unit tests.
- Idempotency tests.
- Validation tests for tenant, period, account, currency, balance, and control-account rules.

Dependencies:

- Batch 3.

Risks:

- Posting engine may expose hidden inconsistencies in existing journal DTOs.
- The legacy `GeneralLedgerService.PostJournalEntryAsync` path must not remain a second accounting path.

Status update 2026-07-09:

- Normal runtime posting-engine bypass lockdown is documented in `docs/finance-legacy-posting-path-lockdown.md`.
- `GeneralLedgerService.PostJournalEntryAsync` is disabled as a normal direct posting path, and `FIN-LIM-0018` is resolved for normal runtime Finance workflows.
- Opening-balance and migration posting remains tracked separately under `FIN-LIM-0006`.

Expected acceptance criteria:

- One controlled internal API exists for Finance posting.
- Posting events are idempotent.
- Posting engine delegates final journal persistence/posting to the approved journal service.

## Batch 5 - Rebuildable Balance Read Models And Snapshot Strategy

Tickets included:

- FIN-005

Files/classes/methods to be changed:

- `src/ErpSystem.Core/Entities/Finance/AccountBalance.cs`
- `src/ErpSystem.Core/Entities/Finance/Account.cs`
- `src/ErpSystem.Core/Entities/Finance/BankAccount.cs`
- `BankAccountService.UpdateBalanceAsync`
- `BankAccountService.GetBalanceAsync`
- `GeneralLedgerService.CalculateAccountBalanceAsOf`
- `CashReportsController`
- Add ledger balance rebuild service under `src/ErpSystem.Api/Services/Finance/GL`

Database/entity/model changes:

- Extend or replace balance snapshot model to include tenant, book, account, segment combination, currency, period, balance type, source hash, and rebuild timestamp.
- Mark mutable balance fields as cached/read-model fields in code comments and service design.
- Add a rebuild/repair command, job, or script that can regenerate snapshots from posted ledger entries.

Migration impact:

- Balance snapshot schema/index migration likely.

API impact:

- Cash/bank/account balance endpoints should identify ledger-derived balances.
- No reports should consume mutable operational balances.

Frontend impact:

- Balance display labels may need to distinguish ledger balance, available balance, and statement balance.

Test coverage:

- Rebuild snapshots from posted ledger.
- Compare cached balance to ledger-derived balance.
- Ensure draft/unposted/reversed/deleted entries are treated correctly.

Dependencies:

- Batch 4.

Risks:

- Existing UI may expect immediate mutable balances.
- Performance tuning may be needed for large tenants.

Expected acceptance criteria:

- Stored balances are not accounting source of truth.
- Rebuild process can reproduce posted ledger balances by tenant, book, currency, account, period, and segment.

## Batch 6 - Journal Lifecycle, Approval Gate, And Immutability

Tickets included:

- FIN-006
- FIN-023 partial

Files/classes/methods to be changed:

- `JournalEntryController.CreateJournalEntry`
- `JournalEntryController.RequestApproval`
- `JournalEntryController.ApproveJournalEntry`
- `JournalEntryController.PostJournalEntry`
- `JournalEntryController.ReverseJournalEntry`
- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `JournalEntryService.ReverseJournalEntryAsync`
- `JournalEntryService.ValidateControlAccountPosting`
- `JournalEntryService.UpdateApprovalStatusAsync`
- `JournalEntry.cs`
- `AccountTransaction.cs`

Database/entity/model changes:

- Add immutable posting/audit fields only if missing.
- Add journal posting event link if not fully covered by Batch 4.

Migration impact:

- Possible new indexes for journal status, posting status, source document, period, and tenant.

API impact:

- Manual journal source module is server-controlled.
- Posting requires workflow completion where configured.
- Posted journals cannot be edited or deleted.

Frontend impact:

- Journal UI must respect immutable posted state.
- Show approval/workflow state before post.

Test coverage:

- Create/update/delete/post/reverse permission tests.
- Closed period posting tests.
- Control-account spoofing tests.
- Immutability tests for posted journals and lines.

Dependencies:

- Batch 2.
- Batch 3.
- Batch 4.

Risks:

- Existing draft or approved journals may not match stricter lifecycle states.

Expected acceptance criteria:

- Journal lifecycle is controlled from draft through approval, posting, and reversal.
- Posted journals are immutable.
- Manual journal control-account bypass is closed.

## Batch 7 - Finance Workflow Engine Coverage

Tickets included:

- FIN-022

Files/classes/methods to be changed:

- `SimpleWorkflowService.BuildEntityContextAsync`
- `SimpleWorkflowService.AttachWorkflowInstanceAsync`
- `FinanceApprovalsController.FinanceWorkflowEntityKeys`
- `FinanceApprovalsController.ResolveFactsAsync`
- `FinanceApprovalsController.ApplyApprovedOutcomeAsync`
- `FinanceApprovalsController.ApplyRejectedOutcomeAsync`
- `WorkflowDefinition`
- `WorkflowStep`
- `WorkflowApproval`
- workflow seeders/admin UI

Database/entity/model changes:

- Seed tenant-scoped workflow entity types and workflow definitions for Finance approval actions.
- Add workflow instance links to Finance entities where needed.

Migration impact:

- Workflow seed migration or tenant-aware seeder update.

API impact:

- Finance approval queue becomes the common approval surface.
- Domain services call `IWorkflowService` facade rather than custom approval logic.

Frontend impact:

- Finance approval queue must show all required Finance entity types.
- Entity detail pages show workflow state and history.

Test coverage:

- Workflow start/approve/reject tests for each Finance entity type.
- Approval outcome idempotency tests.
- Threshold/routing tests using workflow data context.

Dependencies:

- Batch 2.
- Batch 3.
- Batch 6.

Risks:

- Workflow data context currently covers many non-Finance entities more deeply than Finance entities.
- Existing hardcoded approval states may conflict with workflow state.

Expected acceptance criteria:

- All Finance approvals use the workflow engine.
- Approved/rejected outcomes are centralized and tested.
- No parallel approval system is introduced.
- Workflow definitions support tenant-specific thresholds, approver roles, routing, escalation, and delegation where the existing workflow engine supports them.

## Batch 7A - Finance Audit Trail Foundation

Tickets included:

- FIN-023 foundation

Files/classes/methods to be changed:

- `src/ErpSystem.Data/Interceptors/AuditInterceptor.cs`
- `src/ErpSystem.Core/Entities/LogEntities.cs`
- `src/ErpSystem.Core/Services/LogService.cs`
- `src/ErpSystem.Api/Controllers/AuditLogController.cs`
- `src/ErpSystem.Core/Services/Workflow/WorkflowActivityService.cs`
- Finance posting and workflow services once Batch 4 and Batch 7 are in place

Database/entity/model changes:

- Prefer extending existing `AuditLog` only if it can meet Finance requirements without breaking global audit behavior.
- If needed, add a Finance-specific audit event table linked to `TenantId`, source document, posting event, workflow instance, journal entry, and migration batch.

Migration impact:

- Possible new audit-event table or indexes.
- No historical audit backfill in this batch unless data is available and reliable.

API impact:

- Define audit event conventions for Finance actions.
- Do not expose broad new audit APIs unless required; existing audit APIs may remain the read surface.

Frontend impact:

- None required in this batch, though later detail pages may display Finance audit history.

Test coverage:

- Audit event contains tenant, user, timestamp, action, resource/source document, and before/after values where applicable.
- Workflow approval history remains linked to Finance audit context.
- Posting/reversal events have durable source document and journal references.
- Export/print and migration adjustment audit event contracts are defined for later batches.

Dependencies:

- Batch 4.
- Batch 7.

Risks:

- EF interceptor audit logs are entity-change oriented and may not express domain accounting events clearly enough without Finance event conventions.
- Avoid double-counting audit events where interceptor and explicit Finance audit event both fire.

Expected acceptance criteria:

- Finance has one documented audit event taxonomy.
- Existing central audit infrastructure is either confirmed sufficient for each event type or extended through a Finance-specific event model.
- Later batches know exactly which audit API/event method to call for posting, reversal, workflow, export, print, and migration events.

## Batch 8 - Cash, Bank, And Reconciliation GL Migration

Tickets included:

- FIN-007

Files/classes/methods to be changed:

- `CashTransactionService.CreateReceiptAsync`
- `CashTransactionService.CreatePaymentAsync`
- `CashTransactionService.CreateTransferAsync`
- `CashTransactionService.DeleteAsync`
- `BankAccountService.UpdateBalanceAsync`
- `BankAccountService.GetBalanceAsync`
- `BankReconciliationService.StartReconciliationAsync`
- `BankReconciliationService.ApproveReconciliationAsync`
- `BankReconciliationController`
- `CashReportsController`

Database/entity/model changes:

- Ensure bank account has required GL account link.
- Add journal/posting event links to cash transactions and reconciliation adjustments if missing.

Migration impact:

- Backfill bank account GL links.
- Flag existing cash transactions without journals for migration diagnostics.

API impact:

- Cash receipt/payment posts through posting engine or enters approval workflow.
- Delete becomes cancel/void where accounting impact exists.
- Reconciliation book balance is GL-derived.

Frontend impact:

- Cash/bank screens show posting and reconciliation status.
- Reconciliation UI displays GL book balance, statement balance, adjustments, and unresolved difference.

Test coverage:

- Receipt/payment/transfer journal tests.
- Reconciliation approval workflow tests.
- Void/reversal tests for posted cash transactions.
- GL bank account tie-out tests.

Dependencies:

- Batch 4.
- Batch 5.
- Batch 7.

Risks:

- Existing bank balances may not reconcile to posted ledger until migration cleanup.

Expected acceptance criteria:

- Bank/cash subledger cannot diverge from GL through normal workflows.
- Reconciliation approval is workflow-controlled and ledger-based.

## Batch 9 - AP Lifecycle And Control Account Reconciliation

Tickets included:

- FIN-008

Files/classes/methods to be changed:

- `VendorInvoiceService`
- `VendorPaymentService`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostApPaymentAsync`
- `ApControllersConsolidated.cs`
- `ApReportsService`
- AP DTOs under `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`

Database/entity/model changes:

- Add posting event links for AP invoice, payment, allocation, void, discount, WHT, and VAT withholding if missing.
- Add tax/FX snapshots needed for AP.

Migration impact:

- Backfill or flag AP documents without valid journals.

API impact:

- Direct vendor payment cannot bypass workflow if configured.
- AP voids and allocation reversals produce reversing/adjustment journals.

Frontend impact:

- AP invoice/payment pages show posting, workflow, reversal, WHT, and VAT withholding status.

Test coverage:

- AP invoice approval and posting.
- Vendor payment approval and posting.
- AP void/reversal.
- AP aging to GL AP control reconciliation.
- Foreign AP realized FX settlement.

Dependencies:

- Batch 4.
- Batch 7.
- Batch 8 for bank posting.
- Tax and FX batches for full statutory/foreign cases, but base AP lifecycle can start first.

Risks:

- Partial implementation before tax/FX can only cover local-currency non-tax scenarios.

Expected acceptance criteria:

- AP subledger movements reconcile to AP control accounts.
- Posted AP documents reverse through journals, not direct balance mutation.

## Batch 10 - AR Lifecycle And Control Account Reconciliation

Tickets included:

- FIN-009

Files/classes/methods to be changed:

- `InvoiceService`
- `PaymentService`
- `SubledgerPostingService.PostArInvoiceAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `ArControllersConsolidated.cs`
- `ArReportsService`
- AR DTOs under `src/ErpSystem.Core/DTOs/Finance`

Database/entity/model changes:

- Add posting event links for AR invoice, customer payment, allocation, credit note, refund, bounced payment, and write-off if missing.
- Add tax/FX snapshots needed for AR.

Migration impact:

- Backfill or flag AR documents without valid journals.

API impact:

- AR voids, bounced payments, credit notes, and refunds produce reversing/adjustment journals.

Frontend impact:

- AR pages show posting, workflow, reversal, tax, and FX status.

Test coverage:

- AR invoice posting.
- Customer receipt posting.
- Credit note posting.
- Bounced payment reversal.
- AR aging to GL AR control reconciliation.
- Foreign AR realized FX settlement.

Dependencies:

- Batch 4.
- Batch 7.
- Batch 8 for bank posting.
- Tax and FX batches for full statutory/foreign cases.

Risks:

- Existing AR payment status strings may not map cleanly to controlled lifecycle states.

Expected acceptance criteria:

- AR subledger movements reconcile to AR control accounts.
- Posted AR documents reverse through journals, not direct balance mutation.

## Batch 11 - Period Close, Locks, And Cut-Off

Tickets included:

- FIN-010

Files/classes/methods to be changed:

- `FiscalPeriodService.ClosePeriodAsync`
- `FiscalPeriodService.ReopenPeriodAsync`
- `FiscalPeriodService.LockPeriodAsync`
- `FiscalPeriodService.UnlockPeriodAsync`
- `FiscalPeriodService.ValidatePeriodCloseAsync`
- `FiscalPeriodController`
- `GeneralLedgerService.CloseFiscalPeriodAsync`
- `GeneralLedgerService.ValidatePeriodCloseAsync`
- `FiscalPeriod.cs`
- `PeriodCloseDtos.cs`

Database/entity/model changes:

- Normalize period state fields if needed.
- Add period close workflow/audit links if missing.
- Add close checklist status fields only if existing fields are insufficient.

Migration impact:

- Data cleanup for inconsistent `IsClosed`, `IsOpen`, and `PeriodStatus` values.

API impact:

- Close/reopen/unlock requires workflow and reason.
- Posting engine blocks closed/locked periods.

Frontend impact:

- Period page shows checklist, workflow status, close blockers, reopen reason.

Test coverage:

- Period close checklist tests.
- Posting into closed/locked period tests.
- Reopen/unlock workflow tests.
- Route ID/request ID mismatch tests.

Dependencies:

- Batch 4.
- Batch 7.
- Batch 8, 9, 10 for core subledger close checks.

Risks:

- Close checks may initially fail for existing dirty data, which is expected until migration diagnostics.

Expected acceptance criteria:

- Period close is consistent, workflow-controlled, auditable, and enforced at posting time.

## Batch 12 - Posted-Ledger Reporting Core

Tickets included:

- FIN-011

Files/classes/methods to be changed:

- `GeneralLedgerService.GenerateTrialBalanceAsync`
- `GeneralLedgerService.GenerateBalanceSheetAsync`
- `GeneralLedgerService.GenerateIncomeStatementAsync`
- `GeneralLedgerService.GenerateCashFlowStatementAsync`
- `GeneralLedgerService.CalculateAccountBalanceAsOf`
- `GeneralLedgerService.CalculateAccountActivityForPeriod`
- `GeneralLedgerService.CalculatePostedAccountNetBalanceAsOf`
- report DTOs under `src/ErpSystem.Core/DTOs/Finance`

Database/entity/model changes:

- Optional controlled ledger snapshot tables from Batch 5.
- Add indexes for posted ledger report queries.

Migration impact:

- None beyond indexes/snapshots.

API impact:

- Reports use one posted ledger query service.
- Reports expose drilldown to source journal lines.

Frontend impact:

- Report pages may need updated filters and drilldown links.

Test coverage:

- Drafts excluded.
- Reversals treated correctly.
- Tenant/book/period/status filters enforced.
- Balance sheet ties to trial balance.
- Cash flow ties to GL cash accounts.

Dependencies:

- Batch 4.
- Batch 5.
- Batch 11.

Risks:

- Report figures will change where old reports included draft/unposted data.

Expected acceptance criteria:

- Core financial statements are generated only from posted ledger entries or controlled snapshots.

## Batch 13 - Segment-Based Posting And Reporting

Tickets included:

- FIN-012

Files/classes/methods to be changed:

- `SegmentStructureService`
- `AccountCombinationService`
- `AccountService.CreateAsync`
- `AccountService.UpdateAsync`
- `JournalEntryService.CreateJournalEntryAsync`
- `JournalEntryService.PostJournalEntryAsync`
- `GeneralLedgerService` report methods
- segment frontend components under `frontend/src/components/finance/generator`

Database/entity/model changes:

- Add or normalize journal-line segment/dimension storage if account-level segments are insufficient.
- Add segment combination indexes for reporting.

Migration impact:

- Backfill segment values onto existing ledger lines where derivable.
- Flag unresolvable segment history for accountant review.

API impact:

- Posting requires configured mandatory segments.
- Reports accept configured segment filters.

Frontend impact:

- Dynamic segment filters in GL reports.
- Posting forms validate required segments.

Test coverage:

- Required segment missing fails.
- Segment trial balance ties to full trial balance.
- Segment income statement ties to full income statement.

Dependencies:

- Batch 4.
- Batch 12.

Risks:

- Historical data may lack segment values.

Expected acceptance criteria:

- GL reporting supports tenant and configured segment dimensions.

## Batch 14 - Chart Of Accounts Structural Controls

Tickets included:

- FIN-013

Files/classes/methods to be changed:

- `AccountService.UpdateAsync`
- `AccountService.DeleteAsync`
- `AccountService.RemoveCurrencyLinkAsync`
- `AccountService.InactivateCurrencyLinkAsync`
- `AccountController`
- `AccountSegmentValueService`
- `AccountCurrencyLink`

Database/entity/model changes:

- Add effective-dated account/currency link metadata if needed.
- Add structural lock indicators only if not derivable from transaction existence.

Migration impact:

- Flag active accounts with transactions and risky mutable fields.

API impact:

- Structural account fields locked after accounting activity.
- Used accounts deactivate rather than delete.

Frontend impact:

- Account edit page disables locked fields and explains status via validation response.

Test coverage:

- Account code/number/segments/currency/control flags cannot change after transactions.
- Used account cannot be deleted.
- Used currency link cannot be removed.

Dependencies:

- Batch 3.
- Batch 4.

Risks:

- Current workflows may rely on editing fields that must become locked.

Expected acceptance criteria:

- COA history remains stable after posting.

## Batch 15 - Tax Configuration Governance

Tickets included:

- FIN-015

Files/classes/methods to be changed:

- `TaxRuleController`
- `TaxConfigurationController`
- `TaxConfigurationService`
- `TaxRule`
- `TaxEntities`
- tax frontend pages under `frontend/src/app/finance/tax/configuration`

Database/entity/model changes:

- Add tax rule versioning/effective dates if incomplete.
- Add deactivation fields and approval/audit links.

Migration impact:

- Migrate existing tax rules to tenant-scoped versioned records.
- Remove hardcoded/mock tenant references.

API impact:

- Tax config endpoints become tenant-scoped and workflow-protected.
- Used tax rules cannot be destructively edited.

Frontend impact:

- Tax config UI supports versions, effective dates, approval status, and deactivation.

Test coverage:

- Cross-tenant tax rules blocked.
- Used tax rule update creates version or fails.
- Tax config change requires workflow approval.

Dependencies:

- Batch 3.
- Batch 7.
- Batch 7A audit foundations may be completed here or before.

Risks:

- Existing tax setup may contain placeholder category data.

Expected acceptance criteria:

- Tax configuration is tenant-scoped, effective-dated, versioned, workflow-controlled, and auditable.

## Batch 16 - Ghana VAT, NHIL, GETFund, VAT Withholding, And WHT

Tickets included:

- FIN-014

Files/classes/methods to be changed:

- `TaxCalculationEngine.CalculateTaxesAsync`
- `TaxCalculationEngine.CalculateTaxableAmount`
- `SubledgerPostingService.PostApInvoiceAsync`
- `SubledgerPostingService.PostArInvoiceAsync`
- `ApReportsService`
- `ArReportsService`
- tax report pages under `frontend/src/app/finance/tax/reports`

Database/entity/model changes:

- Add posted tax line snapshots if missing.
- Add VAT withholding and WHT transaction/certificate entities if missing.
- Add tax control account mappings.

Migration impact:

- Existing posted invoices need tax snapshot diagnostics.
- Tax balances must reconcile to GL tax control accounts.

API impact:

- AP/AR posting includes Ghana tax lines where applicable.
- Tax reports derive from posted tax lines and GL.
- Posting must resolve effective-dated tenant tax configuration; current Ghana rates must not be hardcoded directly into posting logic.

Frontend impact:

- Invoice and tax screens show VAT, NHIL, GETFund, VAT withholding, and WHT separately.

Test coverage:

- GHS 1,000 standard-rated invoice produces VAT 150, NHIL 25, GETFund 25.
- COVID levy is not charged.
- VAT withholding agent treatment.
- WHT rule scenarios.
- Tax report to GL reconciliation.

Dependencies:

- Batch 4.
- Batch 9.
- Batch 10.
- Batch 15.

Risks:

- Tax treatment varies by taxpayer status, exemption, and transaction type; accountant review is required.

Expected acceptance criteria:

- Ghana statutory tax handling is effective-dated, tenant-specific, posted, reportable, and auditable.

## Batch 17 - Functional Currency And Exchange Rate Governance

Tickets included:

- FIN-016
- FIN-017

Files/classes/methods to be changed:

- `CurrencyService.SetBaseCurrencyAsync`
- `CurrencyService.CreateCurrencyAsync`
- `CurrencyService.ConvertAsync`
- `ExchangeRateService.CreateExchangeRateAsync`
- `ExchangeRateService.UpdateExchangeRateAsync`
- `ExchangeRateService.DeleteExchangeRateAsync`
- `ExchangeRateService.HasExistingTransactionsUsingRate`
- `Currency`
- `ExchangeRate`
- `FinanceSettings`
- `CurrenciesController`
- `ExchangeRateController`

Database/entity/model changes:

- Add tenant functional currency setting if not already canonical.
- Add exchange rate versioning, rate source, rate type, approval status, and usage lock fields.
- Add rate snapshot fields to ledger/source documents if not already present.

Migration impact:

- Determine functional currency per tenant.
- Lock functional currency for tenants with activity.
- Diagnose used rates that lack snapshot data.

API impact:

- Missing rates fail financial posting/reporting.
- Used rates cannot be edited or deleted.

Frontend impact:

- Currency and exchange rate admin pages show functional currency lock and rate approval status.

Test coverage:

- Tenant sets functional currency before activity.
- Functional currency change after activity fails.
- Zero/negative exchange rate fails.
- Used rate cannot be edited/deleted.
- Missing rate blocks posting.

Dependencies:

- Batch 3.
- Batch 4.
- Batch 7.

Risks:

- Existing data may not have complete rate snapshots.

Expected acceptance criteria:

- Functional currency and exchange rates are governed, tenant-scoped, versioned, and auditable.

## Batch 18 - Realized And Unrealized FX

Tickets included:

- FIN-018

Files/classes/methods to be changed:

- `GeneralLedgerService.RunCurrencyRevaluationCoreAsync`
- `CurrencyRevaluationService`
- `ICurrencyRevaluationService`
- `SubledgerPostingService.PostApPaymentAsync`
- `SubledgerPostingService.PostArPaymentAsync`
- `PaymentService.CreateAsync`
- `VendorPaymentService.CreateAsync`
- `AccountTransaction`
- `RevaluationDtos`
- `frontend/src/app/finance/revaluation/page.tsx`

Database/entity/model changes:

- Add revaluation batch/header/line entities if needed.
- Add realized/unrealized FX journal links.

Migration impact:

- Diagnose foreign monetary balances and missing historic rates.

API impact:

- Revaluation runs through posting engine and workflow where configured.
- AP/AR settlement posts realized FX.

Frontend impact:

- Revaluation UI shows preview, missing rates, approval, posting, and reversal status.

Test coverage:

- Foreign AP/AR revaluation.
- Bank FX account revaluation.
- Missing closing rate blocks revaluation and period close.
- Realized FX on settlement.
- Revaluation reversal in next period if configured.

Dependencies:

- Batch 17.
- Batch 9.
- Batch 10.
- Batch 11.

Risks:

- IFRS treatment requires clear distinction between monetary revaluation and translation scenarios.

Expected acceptance criteria:

- FX gains/losses are posted, auditable, reproducible, and period-close aware.

## Batch 19 - Fixed Asset Acquisition And Capitalization

Tickets included:

- FIN-019

Files/classes/methods to be changed:

- `FixedAssetService.CreateAsync`
- `FixedAssetService.ActivateAsync`
- `FixedAssetService.DeleteAsync`
- `VendorInvoiceService`
- `SubledgerPostingService.PostApInvoiceAsync`
- `FixedAsset`
- `FixedAssetBookValue`
- `FixedAssetCategory`
- fixed asset frontend register pages

Database/entity/model changes:

- Add acquisition source links and posting event references if missing.
- Ensure asset book values are tenant/book/currency scoped.

Migration impact:

- Diagnose assets without capitalization journals.
- Backfill opening asset book values for migration.

API impact:

- Asset activation requires posted capitalization or approved opening migration.
- Asset delete prohibited after activity.

Frontend impact:

- Asset register shows acquisition source and capitalization status.

Test coverage:

- Capitalize AP invoice line.
- Activate only capitalized asset.
- Asset register to GL reconciliation.

Dependencies:

- Batch 4.
- Batch 9.
- Batch 17 if foreign assets exist.

Risks:

- Existing fixed asset records may lack acquisition source detail.

Expected acceptance criteria:

- Fixed asset register acquisition values reconcile to GL.

## Batch 20 - Fixed Asset Depreciation

Tickets included:

- FIN-020

Files/classes/methods to be changed:

- `FixedAssetDepreciationService`
- `FixedAssetDepreciationService.PostDepreciationToGlAsync`
- `AssetDepreciationSchedule`
- `FixedAssetBookValue`
- `FixedAssetReportsService`
- depreciation frontend pages

Database/entity/model changes:

- Add component/depreciation method fields if required methods are not fully modeled.
- Add depreciation run/posting links.

Migration impact:

- Recalculate or validate existing depreciation schedules.

API impact:

- Depreciation runs post through posting engine.
- Period close validates depreciation completion.

Frontend impact:

- Depreciation UI shows schedule, run status, posting status, and exceptions.

Test coverage:

- Straight-line, reducing balance, units-of-production.
- Depreciation posting.
- Period close block when depreciation incomplete.

Dependencies:

- Batch 11.
- Batch 19.

Risks:

- Component accounting may require data model extension.

Expected acceptance criteria:

- Depreciation expense and accumulated depreciation reconcile to GL.

## Batch 21 - Fixed Asset Revaluation, Impairment, Transfers, And Disposals

Tickets included:

- FIN-021

Files/classes/methods to be changed:

- `AssetValuationService.CreateValuationAsync`
- `AssetValuationService.PostValuationToGLAsync`
- `AssetTransferService`
- `AssetDisposalService.RequestDisposalAsync`
- `AssetDisposalService.ApproveDisposalAsync`
- `AssetDisposalService.CompleteDisposalAsync`
- `AssetVerificationService`
- `FixedAssetReportsService`

Database/entity/model changes:

- Add impairment entities/fields if missing.
- Add disposal proceeds account/cash/AR reference.
- Add transfer segment/location history.

Migration impact:

- Diagnose prior valuations/disposals without correct journals.

API impact:

- Revaluation, impairment, transfer, and disposal use workflow and posting engine.
- Disposal posts cash/AR proceeds and gain/loss correctly.

Frontend impact:

- Fixed asset lifecycle pages show workflow, posting, and reconciliation state.

Test coverage:

- Revaluation surplus.
- Impairment loss.
- Cost-center transfer.
- Disposal with gain.
- Disposal with loss.

Dependencies:

- Batch 7.
- Batch 19.
- Batch 20.

Risks:

- Disposal correction may require changing current placeholder account behavior.

Expected acceptance criteria:

- Full fixed asset lifecycle reconciles to GL and is workflow-controlled.

## Batch 22 - Frontend Finance Readiness

Tickets included:

- FIN-025

Files/classes/methods to be changed:

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

Database/entity/model changes:

- None expected.

Migration impact:

- None.

API impact:

- May require minor DTO adjustments uncovered by frontend integration.

Frontend impact:

- Fix TypeScript errors.
- Replace mock data.
- Add workflow/permission-aware UX.
- Add report filters for configured segments.

Test coverage:

- `npm.cmd run type-check`.
- `npm.cmd run lint:check`.
- Component or integration tests for key Finance workflows if existing test stack supports them.

Dependencies:

- Backend APIs from prior batches.

Risks:

- Some pages depend on APIs not yet complete; sequence this after backend contracts stabilize.

Expected acceptance criteria:

- No go-live Finance page uses mock data or broken types.

## Batch 23 - Print, Export, And Statutory Outputs

Tickets included:

- FIN-026

Files/classes/methods to be changed:

- `InvoiceService.GenerateInvoicePrintAsync`
- `PaymentService.GeneratePaymentReceiptAsync`
- `ApReportsService` export methods
- `ArReportsService` export methods
- `src/ErpSystem.Api/Services/Documents/Finance/*`
- Finance report frontend pages

Database/entity/model changes:

- Audit export events if not covered by audit infrastructure.

Migration impact:

- None.

API impact:

- Implement invoice, receipt, AP/AR exports, tax exports.

Frontend impact:

- Enable print/export actions only when backend output exists.

Test coverage:

- PDF/export generation tests.
- Totals match API/report data.
- Ghana invoice shows VAT, NHIL, GETFund separately.

Dependencies:

- Batch 12.
- Batch 16.
- Batch 22.

Risks:

- Document layout testing can be time-consuming.

Expected acceptance criteria:

- Required statutory and operational documents can be produced and reconcile to posted data.

## Batch 24 - Data Migration, Diagnostics, Cleanup, And Sign-Off Pack

Tickets included:

- FIN-024

Files/classes/methods to be changed:

- migration scripts under repo convention, likely `scripts`, `tools`, or `src/ErpSystem.Data`
- `ApplicationDbContext`
- Finance entities under `src/ErpSystem.Core/Entities/Finance`
- posting/reporting services for diagnostic reuse

Database/entity/model changes:

- Optional diagnostic run tables.
- Optional migration adjustment batch table.

Migration impact:

- This is the migration/cutover batch.
- Snapshot existing data.
- Classify and correct or formally accept exceptions.
- Generate accountant-readable reconciliation reports, not only technical diagnostics.

API impact:

- Admin-only migration diagnostics endpoints only if required; prefer offline tooling for destructive/cutover actions.

Frontend impact:

- Optional admin dashboard for diagnostics.

Test coverage:

- Dirty-data fixture diagnostics.
- Balance rebuild.
- Subledger to GL tie-outs.
- Migration adjustment posting.
- Opening period lock.

Dependencies:

- All accounting behavior batches.

Risks:

- Existing accounting data may expose material differences requiring accountant decisions.

Expected acceptance criteria:

- Trial balance, AP, AR, bank, tax, FX, and fixed asset balances reconcile.
- Accountant sign-off pack is generated.
- Opening/legacy periods are locked.

## Batch 25 - Final Go-Live Scenario Pack

Tickets included:

- FIN-027

Files/classes/methods to be changed:

- `e2e-tests/tests/finance.spec.ts`
- `tests/ErpSystem.Api.Tests/Services/Finance/*`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/*`
- seed data/test fixtures

Database/entity/model changes:

- Test seed data only.

Migration impact:

- None, but validates migration output.

API impact:

- None expected.

Frontend impact:

- End-to-end workflows across Finance UI.

Test coverage:

- Quote/order/invoice/cash receipt.
- AP invoice/vendor payment.
- Foreign currency AP/AR and revaluation.
- Bank reconciliation.
- Fixed asset lifecycle.
- Ghana tax reports.
- Month close/reopen/re-close.
- Migration adjustment and opening balance lock.

Dependencies:

- All blocker batches.

Risks:

- End-to-end tests may reveal integration issues requiring small follow-up PRs.

Expected acceptance criteria:

- Clean tenant and migrated tenant both pass the scenario pack.
- Accountant signs off final Finance readiness.

## Dependency Batches

| Batch | Can Start After | Blocks |
|---|---|---|
| 1 | None | 2, 3 |
| 2 | 1 | 6, 7, all secured APIs |
| 3 | 1 | 4 and every cross-tenant-safe workflow |
| 4 | 3 | 5, 6, 8, 9, 10, 16, 18, 19, 20, 21 |
| 5 | 4 | 8, 11, 12, 24 |
| 6 | 2, 3, 4 | 7, 11 |
| 7 | 2, 3, 6 | 7A, 8, 9, 10, 11, 15, 17, 21 |
| 7A | 4, 7 | 8, 9, 10, 15, 16, 18, 19, 20, 21, 23, 24 |
| 8 | 4, 5, 7, 7A | 11, 24 |
| 9 | 4, 7, 7A, 8 | 16, 18, 24 |
| 10 | 4, 7, 7A, 8 | 16, 18, 24 |
| 11 | 4, 7, 8, 9, 10 | 12, 18, 20, 24 |
| 12 | 4, 5, 11 | 13, 16, 18, 23, 24 |
| 13 | 4, 12 | 24 |
| 14 | 3, 4 | 24 |
| 15 | 3, 7 | 16 |
| 16 | 4, 9, 10, 15 | 23, 24 |
| 17 | 3, 4, 7 | 18 |
| 18 | 9, 10, 11, 17 | 24 |
| 19 | 4, 9, 17 | 20, 21 |
| 20 | 11, 19 | 21, 24 |
| 21 | 7, 19, 20 | 24 |
| 22 | Backend contracts stable | 23, 25 |
| 23 | 12, 16, 22 | 25 |
| 24 | All accounting batches | 25 |
| 25 | All blockers | Go-live |

## First Implementation Batch Recommendation

Implement **Batch 1** first as a PR-sized task.

Scope for the first PR:

- Restore Finance test compilation enough to run targeted Finance security tests.
- Add or update test infrastructure for authenticated, unauthorized, and cross-tenant API/service scenarios.
- Add `[Authorize]` to unauthenticated Finance controllers if this can be done without broad permission-matrix changes.
- Add diagnostic tests documenting missing action-level permissions and tenant isolation gaps.
- Do not refactor posting, tax, FX, fixed assets, or reporting in this PR.

Expected PR acceptance:

- API build passes.
- Finance test projects compile or have a documented minimal subset restored with follow-up tasks.
- New tests prove anonymous cash/bank/reconciliation access is blocked.
- New tests expose at least the current known tenant-isolation defects.
- No accounting behavior changes beyond access gating.
