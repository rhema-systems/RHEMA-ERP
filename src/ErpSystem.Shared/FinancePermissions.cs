namespace ErpSystem.Shared;

public sealed record FinancePermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

public static class FinancePermissions
{
    public const string CategoryCore = "Finance";
    public const string CategoryGeneralLedger = "Finance - General Ledger";
    public const string CategoryAccountsPayable = "Finance - Accounts Payable";
    public const string CategoryAccountsReceivable = "Finance - Accounts Receivable";
    public const string CategoryCashBank = "Finance - Cash and Bank";
    public const string CategoryTax = "Finance - Tax";
    public const string CategoryFx = "Finance - Multi-Currency";
    public const string CategoryFixedAssets = "Finance - Fixed Assets";
    public const string CategoryReporting = "Finance - Reporting";
    public const string CategoryPeriodClose = "Finance - Period Close";
    public const string CategoryWorkflow = "Finance - Workflow";
    public const string CategoryMigration = "Finance - Migration";
    public const string CategoryBudgeting = "Finance - Budgeting";

    public const string ViewFinance = "Finance.Read";
    public const string MaintainFinance = "Finance.Write";
    public const string AdministerFinance = "Finance.Admin";

    public const string ManageChartOfAccounts = "Finance.ChartOfAccounts.Manage";
    public const string ConfigureChartOfAccountsPolicy = "Finance.Policy.ConfigureChartOfAccounts";
    public const string ConfigureTaxPolicy = "Finance.Policy.ConfigureTax";
    public const string ConfigureFixedAssetCategoriesPolicy = "Finance.Policy.ConfigureFixedAssetCategories";

    public const string CreateJournalEntries = "Finance.JournalEntries.Create";
    public const string EditJournalEntries = "Finance.JournalEntries.Edit";
    public const string DeleteJournalEntries = "Finance.JournalEntries.Delete";
    public const string MaintainJournalEntries = "Finance.JournalEntries.Write";
    public const string SubmitJournalEntries = "Finance.JournalEntries.SubmitForApproval";
    public const string ApproveJournalEntries = "Finance.JournalEntries.Approve";
    public const string PostJournalEntries = "Finance.JournalEntries.Post";
    public const string ReverseJournalEntries = "Finance.JournalEntries.Reverse";

    public const string ManageApInvoices = "Finance.AP.Invoices.Manage";
    public const string CreateApInvoices = "Finance.AP.Invoices.Create";
    public const string EditApInvoices = "Finance.AP.Invoices.Edit";
    public const string DeleteApInvoices = "Finance.AP.Invoices.Delete";
    public const string MaintainApInvoices = "Finance.AP.Invoices.Write";
    public const string SubmitApInvoices = "Finance.AP.Invoices.SubmitForApproval";
    public const string ApproveApInvoices = "Finance.AP.Invoices.Approve";
    public const string PostApInvoices = "Finance.AP.Invoices.Post";
    public const string VoidApInvoices = "Finance.AP.Invoices.Void";
    public const string ProcessApPayments = "Finance.AP.Payments.Process";
    public const string ApproveApPayments = "Finance.AP.Payments.Approve";

    public const string ManageArInvoices = "Finance.AR.Invoices.Manage";
    public const string CreateArInvoices = "Finance.AR.Invoices.Create";
    public const string EditArInvoices = "Finance.AR.Invoices.Edit";
    public const string DeleteArInvoices = "Finance.AR.Invoices.Delete";
    public const string MaintainArInvoices = "Finance.AR.Invoices.Write";
    public const string ApprovePostArInvoices = "Finance.AR.Invoices.ApprovePost";
    public const string SendArInvoices = "Finance.AR.Invoices.Send";
    public const string VoidArInvoices = "Finance.AR.Invoices.Void";
    public const string ReceiveCustomerPayments = "Finance.AR.Payments.Receive";

    public const string ManageBankAccounts = "Finance.BankAccounts.Manage";
    public const string RecordCashBankTransactions = "Finance.CashBank.Transactions.Record";
    public const string PerformBankReconciliation = "Finance.BankReconciliation.Perform";
    public const string ApproveBankReconciliation = "Finance.BankReconciliation.Approve";

    public const string ManageTaxConfiguration = "Finance.Tax.Configuration.Manage";

    public const string ManageFxRates = "Finance.FX.Rates.Manage";
    public const string RunFxRevaluation = "Finance.FX.Revaluation.Run";

    public const string ManageFixedAssets = "Finance.FixedAssets.Manage";
    public const string RunDepreciation = "Finance.FixedAssets.Depreciation.Run";
    public const string DisposeFixedAssets = "Finance.FixedAssets.Disposal.Run";

    public const string RunFinanceReports = "Finance.Reports.Run";
    public const string ExportFinanceReports = "Finance.Reports.Export";

    public const string CloseAccountingPeriods = "Finance.PeriodClose";
    public const string ReopenAccountingPeriods = "Finance.PeriodReopen";

    public const string WorkflowSubmit = "Finance.Workflow.Submit";
    public const string WorkflowApprove = "Finance.Workflow.Approve";
    public const string WorkflowReject = "Finance.Workflow.Reject";
    public const string WorkflowRequestChanges = "Finance.Workflow.RequestChanges";
    public const string WorkflowCancel = "Finance.Workflow.Cancel";
    public const string WorkflowPostAfterApproval = "Finance.Workflow.PostAfterApproval";

    public const string RunMigrationDiagnostics = "Finance.Migration.Diagnostics.Run";
    public const string RunMigrationAdjustments = "Finance.Migration.Adjustments.Run";
    public const string PrepareOpeningBalances = "Finance.Migration.OpeningBalances.Prepare";

    public const string ViewBudgets = "Finance.Budgeting.Read";
    public const string MaintainBudgets = "Finance.Budgeting.Write";
    public const string AssignBudgetReturns = "Finance.BudgetReturns.Assign";
    public const string SubmitBudgetReturns = "Finance.BudgetReturns.Submit";
    public const string ApproveBudgetReturns = "Finance.BudgetReturns.Approve";
    public const string LockBudgets = "Finance.Budgeting.Lock";

    public static readonly FinancePermissionDefinition[] All =
    {
        new(ViewFinance, "View Finance", "View finance module records, setup, and reports.", CategoryCore),
        new(MaintainFinance, "Maintain Finance", "Create and update operational finance records.", CategoryCore),
        new(AdministerFinance, "Administer Finance", "Manage finance setup, periods, segments, and control settings.", CategoryCore),

        new(ManageChartOfAccounts, "Manage Chart of Accounts", "Create, update, delete, and configure chart of accounts structures and account combinations.", CategoryGeneralLedger),
        new(CreateJournalEntries, "Create Journal Entries", "Create draft manual journal entries.", CategoryGeneralLedger),
        new(EditJournalEntries, "Edit Journal Entries", "Edit draft journal entries.", CategoryGeneralLedger),
        new(DeleteJournalEntries, "Delete Journal Entries", "Delete draft journal entries.", CategoryGeneralLedger),
        new(MaintainJournalEntries, "Maintain Journal Entries", "Create and update journal entries.", CategoryGeneralLedger),
        new(SubmitJournalEntries, "Submit Journal Entries", "Submit journal entries for approval.", CategoryGeneralLedger),
        new(ApproveJournalEntries, "Approve Journal Entries", "Approve or reject submitted journal entries.", CategoryGeneralLedger),
        new(PostJournalEntries, "Post Journal Entries", "Post approved journal entries to the ledger.", CategoryGeneralLedger),
        new(ReverseJournalEntries, "Reverse Journal Entries", "Reverse posted journal entries using controlled reversal actions.", CategoryGeneralLedger),

        new(ManageApInvoices, "Manage AP Invoices", "Manage supplier invoice lifecycle and matching.", CategoryAccountsPayable),
        new(CreateApInvoices, "Create AP Invoices", "Capture supplier invoices.", CategoryAccountsPayable),
        new(EditApInvoices, "Edit AP Invoices", "Edit draft supplier invoices.", CategoryAccountsPayable),
        new(DeleteApInvoices, "Delete AP Invoices", "Delete draft supplier invoices.", CategoryAccountsPayable),
        new(MaintainApInvoices, "Maintain AP Invoices", "Create and update supplier invoices.", CategoryAccountsPayable),
        new(SubmitApInvoices, "Submit AP Invoices", "Submit supplier invoices for approval.", CategoryAccountsPayable),
        new(ApproveApInvoices, "Approve AP Invoices", "Approve or reject supplier invoices.", CategoryAccountsPayable),
        new(PostApInvoices, "Post AP Invoices", "Post approved supplier invoices to the ledger.", CategoryAccountsPayable),
        new(VoidApInvoices, "Void AP Invoices", "Void supplier invoices with reversal controls.", CategoryAccountsPayable),
        new(ProcessApPayments, "Process AP Payments", "Create, allocate, clear, void, and process supplier payments and payment batches.", CategoryAccountsPayable),
        new(ApproveApPayments, "Approve AP Payments", "Approve supplier payments and payment batches.", CategoryAccountsPayable),

        new(ManageArInvoices, "Manage AR Invoices", "Manage customer invoice lifecycle.", CategoryAccountsReceivable),
        new(CreateArInvoices, "Create AR Invoices", "Create customer invoices.", CategoryAccountsReceivable),
        new(EditArInvoices, "Edit AR Invoices", "Edit draft customer invoices.", CategoryAccountsReceivable),
        new(DeleteArInvoices, "Delete AR Invoices", "Delete draft customer invoices.", CategoryAccountsReceivable),
        new(MaintainArInvoices, "Maintain AR Invoices", "Create and update customer invoices.", CategoryAccountsReceivable),
        new(ApprovePostArInvoices, "Approve/Post AR Invoices", "Approve or post customer invoices to the ledger.", CategoryAccountsReceivable),
        new(SendArInvoices, "Send AR Invoices", "Finalize and send customer invoices.", CategoryAccountsReceivable),
        new(VoidArInvoices, "Void AR Invoices", "Void customer invoices with reversal controls.", CategoryAccountsReceivable),
        new(ReceiveCustomerPayments, "Receive Customer Payments", "Record, allocate, clear, bounce, and credit customer payments.", CategoryAccountsReceivable),

        new(ManageBankAccounts, "Manage Bank Accounts", "Create, update, delete, and configure bank accounts.", CategoryCashBank),
        new(RecordCashBankTransactions, "Record Cash/Bank Transactions", "Record cash receipts, payments, transfers, and reversals.", CategoryCashBank),
        new(PerformBankReconciliation, "Perform Bank Reconciliation", "Start, match, adjust, and maintain bank reconciliations.", CategoryCashBank),
        new(ApproveBankReconciliation, "Approve Bank Reconciliation", "Approve completed bank reconciliations.", CategoryCashBank),

        new(ManageTaxConfiguration, "Manage Tax Configuration", "Maintain effective-dated tenant tax configuration and statutory tax setup.", CategoryTax),

        new(ManageFxRates, "Manage FX Rates", "Maintain tenant exchange rates and currency setup.", CategoryFx),
        new(RunFxRevaluation, "Run FX Revaluation", "Run foreign currency revaluation and related journals.", CategoryFx),

        new(ManageFixedAssets, "Manage Fixed Assets", "Create, update, transfer, verify, value, import, and administer fixed assets.", CategoryFixedAssets),
        new(RunDepreciation, "Run Depreciation", "Run fixed asset depreciation.", CategoryFixedAssets),
        new(DisposeFixedAssets, "Dispose Fixed Assets", "Request, approve, complete, and process fixed asset disposals.", CategoryFixedAssets),

        new(RunFinanceReports, "Run Finance Reports", "Run finance statements, aging, cash, bank, tax, FX, and fixed asset reports.", CategoryReporting),
        new(ExportFinanceReports, "Export/Print Finance Reports", "Export or print finance reports and finance-controlled documents.", CategoryReporting),

        new(CloseAccountingPeriods, "Close Accounting Periods", "Close fiscal periods after month-end checks.", CategoryPeriodClose),
        new(ReopenAccountingPeriods, "Reopen Accounting Periods", "Reopen previously closed fiscal periods.", CategoryPeriodClose),

        new(WorkflowSubmit, "Submit Finance Workflow", "Submit finance transactions into configured workflows.", CategoryWorkflow),
        new(WorkflowApprove, "Approve Finance Workflow", "Approve finance workflow tasks assigned by the workflow engine.", CategoryWorkflow),
        new(WorkflowReject, "Reject Finance Workflow", "Reject finance workflow tasks assigned by the workflow engine.", CategoryWorkflow),
        new(WorkflowRequestChanges, "Request Finance Workflow Changes", "Return finance workflow items for correction or requested changes.", CategoryWorkflow),
        new(WorkflowCancel, "Cancel Finance Workflow", "Cancel or withdraw finance workflow submissions.", CategoryWorkflow),
        new(WorkflowPostAfterApproval, "Post After Finance Approval", "Post finance transactions after workflow approval.", CategoryWorkflow),

        new(RunMigrationDiagnostics, "Run Finance Migration Diagnostics", "Run finance migration diagnostics and reconciliation checks.", CategoryMigration),
        new(RunMigrationAdjustments, "Run Finance Migration Adjustments", "Run approved finance migration adjustment actions.", CategoryMigration),
        new(PrepareOpeningBalances, "Prepare Opening Balances", "Create, validate, and submit controlled opening-balance batches for approval.", CategoryMigration),

        new(ViewBudgets, "View Budgets", "View budget scenarios, returns, and worksheets.", CategoryBudgeting),
        new(MaintainBudgets, "Maintain Budgets", "Create budget scenarios, returns, and entries.", CategoryBudgeting),
        new(AssignBudgetReturns, "Assign Budget Returns", "Assign budget worksheets to preparers.", CategoryBudgeting),
        new(SubmitBudgetReturns, "Submit Budget Returns", "Submit assigned budget worksheets.", CategoryBudgeting),
        new(ApproveBudgetReturns, "Approve Budget Returns", "Approve or reject submitted budget worksheets.", CategoryBudgeting),
        new(LockBudgets, "Lock Budgets", "Lock approved budget scenarios.", CategoryBudgeting)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
