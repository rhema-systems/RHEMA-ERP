# Finance Permission Matrix

Date: 2026-07-03

## Authorization Architecture

Batch 2 uses the existing ERP authorization model instead of creating a parallel Finance security system.

- Permission source of truth: `Permission` and `RolePermission` entities.
- Finance permission definitions: `ErpSystem.Shared.FinancePermissions`.
- Policy registration: `AddErpSystemAuthorization`.
- Controller/action enforcement: `FinancePermissionAuthorizationConvention` and `FinancePermissionPolicyMap`.
- Runtime permission checks: `PermissionAuthorizationHandler`.
- Tenant scope: every permission decision requires a valid `TenantId` claim and confirms the authenticated user has access to that tenant before role permissions are evaluated.

Super administrators are not treated as cross-tenant bypass users. They still need valid tenant access for the selected tenant before the permission handler succeeds. Tenant administrators receive the full Finance permission set through seeded role permissions, but are still constrained by tenant access.

## Permission Matrix

| Finance capability | Permission |
| --- | --- |
| View Finance data | `Finance.Read` |
| General Finance write fallback | `Finance.Write` |
| Finance administration fallback | `Finance.Admin` |
| Manage chart of accounts | `Finance.ChartOfAccounts.Manage` |
| Create draft journal entries | `Finance.JournalEntries.Create` |
| Edit draft journal entries | `Finance.JournalEntries.Edit` |
| Delete draft journal entries | `Finance.JournalEntries.Delete` |
| Submit journal entries for approval | `Finance.JournalEntries.SubmitForApproval` |
| Approve or reject journal entries | `Finance.JournalEntries.Approve` |
| Post journal entries | `Finance.JournalEntries.Post` |
| Reverse posted journal entries | `Finance.JournalEntries.Reverse` |
| Manage AP invoices | `Finance.AP.Invoices.Manage` |
| Create AP invoices | `Finance.AP.Invoices.Create` |
| Edit AP invoices | `Finance.AP.Invoices.Edit` |
| Delete AP invoices | `Finance.AP.Invoices.Delete` |
| Submit AP invoices for approval | `Finance.AP.Invoices.SubmitForApproval` |
| Approve AP invoices | `Finance.AP.Invoices.Approve` |
| Post AP invoices | `Finance.AP.Invoices.Post` |
| Void AP invoices | `Finance.AP.Invoices.Void` |
| Process AP payments | `Finance.AP.Payments.Process` |
| Approve AP payments | `Finance.AP.Payments.Approve` |
| Manage AR invoices | `Finance.AR.Invoices.Manage` |
| Create AR invoices | `Finance.AR.Invoices.Create` |
| Edit AR invoices | `Finance.AR.Invoices.Edit` |
| Delete AR invoices | `Finance.AR.Invoices.Delete` |
| Approve or post AR invoices | `Finance.AR.Invoices.ApprovePost` |
| Send AR invoices | `Finance.AR.Invoices.Send` |
| Void AR invoices | `Finance.AR.Invoices.Void` |
| Receive customer payments | `Finance.AR.Payments.Receive` |
| Manage bank accounts | `Finance.BankAccounts.Manage` |
| Record cash or bank transactions | `Finance.CashBank.Transactions.Record` |
| Perform bank reconciliation | `Finance.BankReconciliation.Perform` |
| Approve bank reconciliation | `Finance.BankReconciliation.Approve` |
| Manage tax configuration | `Finance.Tax.Configuration.Manage` |
| Manage FX rates | `Finance.FX.Rates.Manage` |
| Run FX revaluation | `Finance.FX.Revaluation.Run` |
| Manage fixed assets | `Finance.FixedAssets.Manage` |
| Run fixed asset depreciation | `Finance.FixedAssets.Depreciation.Run` |
| Dispose fixed assets | `Finance.FixedAssets.Disposal.Run` |
| Run Finance reports | `Finance.Reports.Run` |
| Export or print Finance reports | `Finance.Reports.Export` |
| Close accounting periods | `Finance.PeriodClose` |
| Reopen accounting periods | `Finance.PeriodReopen` |
| Submit Finance workflow item | `Finance.Workflow.Submit` |
| Approve Finance workflow item | `Finance.Workflow.Approve` |
| Reject Finance workflow item | `Finance.Workflow.Reject` |
| Return or request changes | `Finance.Workflow.RequestChanges` |
| Cancel Finance workflow item | `Finance.Workflow.Cancel` |
| Post after approval | `Finance.Workflow.PostAfterApproval` |
| Run migration diagnostics | `Finance.Migration.Diagnostics.Run` |
| Run migration adjustments | `Finance.Migration.Adjustments.Run` |
| View budgets | `Finance.Budgeting.View` |
| Manage budgets | `Finance.Budgeting.Manage` |
| Approve budgets | `Finance.Budgeting.Approve` |
| Run budget reports | `Finance.Budgeting.Reports` |

## Controller And Action Coverage

Finance controller policy coverage is centralized in `FinancePermissionPolicyMap`. The MVC convention applies policies to Finance actions during application startup, including the root `FinanceController` whose namespace is outside `ErpSystem.Api.Controllers.Finance`.

Covered areas include:

- Chart of accounts and general Finance setup.
- Journal entry creation, submission, approval, posting, reversal, and cancellation.
- Accounts payable invoices, approvals, posting, payment batches, and vendor payments.
- Accounts receivable invoices, approvals, posting, payment receipt, and voiding.
- Bank accounts, cash/bank transactions, cash flow, cash forecasting, and bank reconciliation.
- Finance reports, exports, trial balance, balance sheet, income statement, cash flow, budget variance, AR/AP aging, and audit reports.
- Fiscal periods, period close, and reopen actions.
- Finance approval queue actions exposed through `FinanceApprovalsController`.
- Tax settings and tax reports.
- FX rates, conversions, revaluation, and FX reports.
- Fixed assets, depreciation, revaluation, impairment, transfer, disposal, and export actions.
- Migration diagnostics and migration adjustment endpoints.
- Budget workflows and budget reporting.

Actions not explicitly mapped fall back to `Finance.Read` for safe HTTP reads and `Finance.Write` for mutating Finance endpoints. Tests verify that every HTTP action in Finance controllers resolves to a Finance permission policy.

## Workflow Authorization

Finance workflow-facing actions now map to Finance workflow permissions for submit, approve, reject, request changes, cancel, and post-after-approval flows. This does not replace the existing workflow engine. It adds controller-level authorization before workflow operations reach `IWorkflowEngine`, `SimpleWorkflowService`, workflow definitions, workflow steps, or workflow approvals.

Cash/bank workflow hardening adds these explicit controller mappings:

| Controller action | Permission |
| --- | --- |
| `CashTransactionController.CreateReceipt`, `CreatePayment`, `CreateTransfer`, `Delete` | `Finance.CashBank.Transactions.Record` |
| `CashTransactionController.Submit` | `Finance.Workflow.Submit` |
| `CashTransactionController.Approve` | `Finance.Workflow.Approve` |
| `CashTransactionController.Reject` | `Finance.Workflow.Reject` |
| `CashTransactionController.Return` | `Finance.Workflow.RequestChanges` |
| `CashTransactionController.Cancel` | `Finance.Workflow.Cancel` |
| `CashTransactionController.Post` | `Finance.Workflow.PostAfterApproval` |

The generic `WorkflowController` was not globally converted into a Finance-specific controller because it is shared platform workflow infrastructure. Finance-specific workflow entry points and Finance approval actions are covered. Deeper workflow-engine routing, threshold, escalation, delegation, and tenant-specific workflow definition validation remain owned by the later workflow integration batch.

## Known Limitations

- Batch 2 enforces controller/action-level Finance permissions. Service/repository-level tenant filtering diagnostics and fixes remain in Batch 3.
- Identity role assignments are still global user role assignments. The permission handler verifies tenant access before granting permissions, but role permissions themselves are not yet tenant-specific.
- Existing ad hoc permission checks inside some Finance controllers remain in place where they are more restrictive than the new policy layer. They can be simplified after the centralized matrix is stable.
- Frontend route and button visibility was not changed in Batch 2. Backend authorization is authoritative; frontend permission-aware UX can be aligned in a later UI batch.
- Audit trail semantics are not added here. Batch 7A remains the explicit Finance audit trail foundation owner.
