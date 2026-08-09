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
    public const string ManageFinanceAccessScopes = "Finance.AccessScopes.Manage";

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

    public const string ViewJournalBatches = "Finance.JournalBatches.View";
    public const string CreateJournalBatches = "Finance.JournalBatches.Create";
    public const string EditJournalBatches = "Finance.JournalBatches.Edit";
    public const string DeleteJournalBatches = "Finance.JournalBatches.Delete";
    public const string SubmitJournalBatches = "Finance.JournalBatches.SubmitForApproval";
    public const string ApproveJournalBatches = "Finance.JournalBatches.Approve";
    public const string PostJournalBatches = "Finance.JournalBatches.Post";
    public const string ReverseJournalBatches = "Finance.JournalBatches.Reverse";
    public const string ImportJournalBatches = "Finance.JournalBatches.Import";
    public const string ExportJournalBatches = "Finance.JournalBatches.Export";
    public const string CopyJournalBatches = "Finance.JournalBatches.Copy";

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
    public const string ReverseApPayments = "Finance.AP.Payments.Reverse";

    public const string ManageArInvoices = "Finance.AR.Invoices.Manage";
    public const string CreateArInvoices = "Finance.AR.Invoices.Create";
    public const string EditArInvoices = "Finance.AR.Invoices.Edit";
    public const string DeleteArInvoices = "Finance.AR.Invoices.Delete";
    public const string MaintainArInvoices = "Finance.AR.Invoices.Write";
    public const string ApprovePostArInvoices = "Finance.AR.Invoices.ApprovePost";
    public const string SendArInvoices = "Finance.AR.Invoices.Send";
    public const string VoidArInvoices = "Finance.AR.Invoices.Void";
    public const string ReceiveCustomerPayments = "Finance.AR.Payments.Receive";
    public const string ReverseArPayments = "Finance.AR.Payments.Reverse";
    public const string ViewArCollections = "Finance.AR.Collections.View";
    public const string ManageArCollections = "Finance.AR.Collections.Manage";
    public const string RecordArCollectionReminders = "Finance.AR.Collections.Reminders.Record";

    public const string ManageBankAccounts = "Finance.BankAccounts.Manage";
    public const string RecordCashBankTransactions = "Finance.CashBank.Transactions.Record";
    public const string ReverseCashBankTransactions = "Finance.CashBank.Transactions.Reverse";
    public const string PerformBankReconciliation = "Finance.BankReconciliation.Perform";
    public const string ApproveBankReconciliation = "Finance.BankReconciliation.Approve";
    public const string ManageLiquidityAccounts = "Finance.Banking.LiquidityAccounts.Manage";
    public const string CreateBankDeposits = "Finance.Banking.Deposits.Create";
    public const string SubmitBankDeposits = "Finance.Banking.Deposits.Submit";
    public const string ApproveBankDeposits = "Finance.Banking.Deposits.Approve";
    public const string ConfirmBankDeposits = "Finance.Banking.Deposits.Confirm";
    public const string ManageBankingSettings = "Finance.Banking.Settings.Manage";
    public const string ManageReturnedCheques = "Finance.Banking.ReturnedCheques.Manage";
    public const string OperateCashTills = "Finance.CashTills.Operate";
    public const string ReviewCashTillClosures = "Finance.CashTills.Closures.Review";
    public const string ReopenCashTillSessions = "Finance.CashTills.Sessions.Reopen";
    public const string IssueCashBankDocuments = "Finance.CashBank.Documents.Issue";
    public const string ReprintCashBankDocuments = "Finance.CashBank.Documents.Reprint";

    public const string ManageTaxConfiguration = "Finance.Tax.Configuration.Manage";

    public const string ManageFxRates = "Finance.FX.Rates.Manage";
    public const string RunFxRevaluation = "Finance.FX.Revaluation.Run";

    public const string ManageFixedAssets = "Finance.FixedAssets.Manage";
    public const string RunDepreciation = "Finance.FixedAssets.Depreciation.Run";
    public const string DisposeFixedAssets = "Finance.FixedAssets.Disposal.Run";
    public const string ReverseFixedAssetCapitalization = "Finance.FixedAssets.Capitalization.Reverse";
    public const string ApproveFixedAssetCapitalizationReversal = "Finance.FixedAssets.Capitalization.Reversal.Approve";

    public const string RunFinanceReports = "Finance.Reports.Run";
    public const string ExportFinanceReports = "Finance.Reports.Export";
    public const string ManageFinancialStatementLayouts = "Finance.Reports.Layouts.Manage";
    public const string PublishFinancialStatementLayouts = "Finance.Reports.Layouts.Publish";
    public const string ViewReportSchedules = "Finance.Reports.Schedules.View";
    public const string ManageReportSchedules = "Finance.Reports.Schedules.Manage";
    public const string RunReportSchedules = "Finance.Reports.Schedules.Run";

    public const string OpenAccountingPeriods = "Finance.PeriodOpen";
    public const string CloseAccountingPeriods = "Finance.PeriodClose";
    public const string ReopenAccountingPeriods = "Finance.PeriodReopen";
    public const string ApproveAccountingPeriodReopens = "Finance.PeriodReopen.Approve";
    public const string MaintainCloseWorkspace = "Finance.PeriodClose.Workspace.Maintain";
    public const string RequestCloseExceptionWaivers = "Finance.PeriodClose.Waivers.Request";
    public const string ApproveCloseExceptionWaivers = "Finance.PeriodClose.Waivers.Approve";

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
    public const string EditBudgetReturns = "Finance.BudgetReturns.Edit";
    public const string SubmitBudgetReturns = "Finance.BudgetReturns.Submit";
    public const string ApproveBudgetReturns = "Finance.BudgetReturns.Approve";
    public const string LockBudgets = "Finance.Budgeting.Lock";
    public const string ViewBudgetRevisions = "Finance.BudgetRevisions.Read";
    public const string MaintainBudgetRevisions = "Finance.BudgetRevisions.Write";
    public const string SubmitBudgetRevisions = "Finance.BudgetRevisions.Submit";
    public const string ApplyBudgetRevisions = "Finance.BudgetRevisions.Apply";

    public static readonly FinancePermissionDefinition[] All =
    {
        new(ViewFinance, "View Finance", "View finance module records, setup, and reports.", CategoryCore),
        new(MaintainFinance, "Maintain Finance", "Create and update operational finance records.", CategoryCore),
        new(AdministerFinance, "Administer Finance", "Manage finance setup, periods, segments, and control settings.", CategoryCore),
        new(ManageFinanceAccessScopes, "Manage Finance Access Scopes", "Assign effective-dated tenant and Finance-resource data scopes to users.", CategoryCore),

        new(ManageChartOfAccounts, "Manage Chart of Accounts", "Create, update, delete, and configure chart of accounts structures and account combinations.", CategoryGeneralLedger),
        new(CreateJournalEntries, "Create Journal Entries", "Create draft manual journal entries.", CategoryGeneralLedger),
        new(EditJournalEntries, "Edit Journal Entries", "Edit draft journal entries.", CategoryGeneralLedger),
        new(DeleteJournalEntries, "Delete Journal Entries", "Delete draft journal entries.", CategoryGeneralLedger),
        new(MaintainJournalEntries, "Maintain Journal Entries", "Create and update journal entries.", CategoryGeneralLedger),
        new(SubmitJournalEntries, "Submit Journal Entries", "Submit journal entries for approval.", CategoryGeneralLedger),
        new(ApproveJournalEntries, "Approve Journal Entries", "Approve or reject submitted journal entries.", CategoryGeneralLedger),
        new(PostJournalEntries, "Post Journal Entries", "Post approved journal entries to the ledger.", CategoryGeneralLedger),
        new(ReverseJournalEntries, "Reverse Journal Entries", "Reverse posted journal entries using controlled reversal actions.", CategoryGeneralLedger),
        new(ViewJournalBatches, "View Journal Batches", "View controlled journal batches, entries, review outcomes, and posting runs.", CategoryGeneralLedger),
        new(CreateJournalBatches, "Create Journal Batches", "Create journal-batch control headers and member journals.", CategoryGeneralLedger),
        new(EditJournalBatches, "Edit Journal Batches", "Edit Draft journal batches and their member journals.", CategoryGeneralLedger),
        new(DeleteJournalBatches, "Delete Journal Batches", "Delete empty Draft journal batches.", CategoryGeneralLedger),
        new(SubmitJournalBatches, "Submit Journal Batches", "Validate and submit journal batches for approval.", CategoryGeneralLedger),
        new(ApproveJournalBatches, "Approve Journal Batches", "Record per-entry review decisions on assigned journal batches.", CategoryGeneralLedger),
        new(PostJournalBatches, "Post Journal Batches", "Post selected approved journal entries through atomic posting runs.", CategoryGeneralLedger),
        new(ReverseJournalBatches, "Reverse Journal Batches", "Create and process full linked journal-batch reversals.", CategoryGeneralLedger),
        new(ImportJournalBatches, "Import Journal Batches", "Preview and commit versioned journal-batch spreadsheets.", CategoryGeneralLedger),
        new(ExportJournalBatches, "Export Journal Batches", "Export journal-batch templates, details, and error workbooks.", CategoryGeneralLedger),
        new(CopyJournalBatches, "Copy Journal Batches", "Copy entire journal batches or rejected entries into new Draft batches.", CategoryGeneralLedger),

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
        new(ReverseApPayments, "Reverse AP Payments", "Reverse posted supplier payments through controlled compensating journals.", CategoryAccountsPayable),

        new(ManageArInvoices, "Manage AR Invoices", "Manage customer invoice lifecycle.", CategoryAccountsReceivable),
        new(CreateArInvoices, "Create AR Invoices", "Create customer invoices.", CategoryAccountsReceivable),
        new(EditArInvoices, "Edit AR Invoices", "Edit draft customer invoices.", CategoryAccountsReceivable),
        new(DeleteArInvoices, "Delete AR Invoices", "Delete draft customer invoices.", CategoryAccountsReceivable),
        new(MaintainArInvoices, "Maintain AR Invoices", "Create and update customer invoices.", CategoryAccountsReceivable),
        new(ApprovePostArInvoices, "Approve/Post AR Invoices", "Approve or post customer invoices to the ledger.", CategoryAccountsReceivable),
        new(SendArInvoices, "Send AR Invoices", "Finalize and send customer invoices.", CategoryAccountsReceivable),
        new(VoidArInvoices, "Void AR Invoices", "Void customer invoices with reversal controls.", CategoryAccountsReceivable),
        new(ReceiveCustomerPayments, "Receive Customer Payments", "Record, allocate, clear, bounce, and credit customer payments.", CategoryAccountsReceivable),
        new(ReverseArPayments, "Reverse AR Payments", "Reverse posted customer receipts through controlled compensating journals.", CategoryAccountsReceivable),
        new(ViewArCollections, "View AR Collections", "View overdue AR exposures, collection work queues, promises, and follow-up history.", CategoryAccountsReceivable),
        new(ManageArCollections, "Manage AR Collections", "Generate, assign, prioritize, and resolve controlled AR collection tasks.", CategoryAccountsReceivable),
        new(RecordArCollectionReminders, "Record AR Collection Reminders", "Prepare and record customer reminders and collection-contact evidence.", CategoryAccountsReceivable),

        new(ManageBankAccounts, "Manage Bank Accounts", "Create, update, delete, and configure bank accounts.", CategoryCashBank),
        new(RecordCashBankTransactions, "Record Cash/Bank Transactions", "Record cash receipts, payments, and transfers.", CategoryCashBank),
        new(ReverseCashBankTransactions, "Reverse Cash/Bank Transactions", "Reverse posted Finance-owned cash receipts, payments, and bank transfers through controlled compensating entries.", CategoryCashBank),
        new(PerformBankReconciliation, "Perform Bank Reconciliation", "Start, match, adjust, and maintain bank reconciliations.", CategoryCashBank),
        new(ApproveBankReconciliation, "Approve Bank Reconciliation", "Approve completed bank reconciliations.", CategoryCashBank),
        new(ManageLiquidityAccounts, "Manage Liquidity Accounts", "Create and maintain bank, till, and settlement holding-account mappings.", CategoryCashBank),
        new(CreateBankDeposits, "Create Bank Deposits", "Create and edit bank deposit batches from eligible receipts and payments.", CategoryCashBank),
        new(SubmitBankDeposits, "Submit Bank Deposits", "Submit or cancel bank deposit batches through workflow.", CategoryCashBank),
        new(ApproveBankDeposits, "Approve Bank Deposits", "Approve, reject, or return bank deposit batches.", CategoryCashBank),
        new(ConfirmBankDeposits, "Confirm Bank Deposits", "Record the bank acknowledgement reference and evidence for a posted deposit.", CategoryCashBank),
        new(ManageBankingSettings, "Manage Banking Settings", "Configure tenant deposit policy and settlement-account provisioning.", CategoryCashBank),
        new(ManageReturnedCheques, "Manage Returned Cheques", "Capture, submit, and approve returned customer cheque cases.", CategoryCashBank),
        new(OperateCashTills, "Operate Cash Tills", "Open assigned physical tills and submit denomination counts for independent closure.", CategoryCashBank),
        new(ReviewCashTillClosures, "Review Cash Till Closures", "Approve or return submitted till counts using variance and custody evidence.", CategoryCashBank),
        new(ReopenCashTillSessions, "Reopen Cash Till Sessions", "Create an immutable correction session for the most recent closed till custody record.", CategoryCashBank),
        new(IssueCashBankDocuments, "Issue Cash/Bank Documents", "Issue the single controlled original payment slip or customer receipt from a posted Finance transaction.", CategoryCashBank),
        new(ReprintCashBankDocuments, "Reprint Cash/Bank Documents", "Issue a reason-backed, watermarked replacement payment slip or customer receipt.", CategoryCashBank),

        new(ManageTaxConfiguration, "Manage Tax Configuration", "Maintain effective-dated tenant tax configuration and statutory tax setup.", CategoryTax),

        new(ManageFxRates, "Manage FX Rates", "Maintain tenant exchange rates and currency setup.", CategoryFx),
        new(RunFxRevaluation, "Run FX Revaluation", "Run foreign currency revaluation and related journals.", CategoryFx),

        new(ManageFixedAssets, "Manage Fixed Assets", "Create, update, transfer, verify, value, import, and administer fixed assets.", CategoryFixedAssets),
        new(RunDepreciation, "Run Depreciation", "Run fixed asset depreciation.", CategoryFixedAssets),
        new(DisposeFixedAssets, "Dispose Fixed Assets", "Request, approve, complete, and process fixed asset disposals.", CategoryFixedAssets),
        new(ReverseFixedAssetCapitalization, "Reverse Fixed Asset Capitalization", "Request and post linked fixed asset capitalization correction journals.", CategoryFixedAssets),
        new(ApproveFixedAssetCapitalizationReversal, "Approve Fixed Asset Capitalization Reversal", "Independently approve or reject fixed asset capitalization reversal requests.", CategoryFixedAssets),

        new(RunFinanceReports, "Run Finance Reports", "Run finance statements, aging, cash, bank, tax, FX, and fixed asset reports.", CategoryReporting),
        new(ExportFinanceReports, "Export/Print Finance Reports", "Export or print finance reports and finance-controlled documents.", CategoryReporting),
        new(ManageFinancialStatementLayouts, "Manage Financial Statement Layouts", "Create and maintain draft financial statement layouts and mappings.", CategoryReporting),
        new(PublishFinancialStatementLayouts, "Publish Financial Statement Layouts", "Validate and publish versioned financial statement layouts for production reporting.", CategoryReporting),
        new(ViewReportSchedules, "View Finance Report Schedules", "View Finance report schedules, delivery history, and retained artifacts.", CategoryReporting),
        new(ManageReportSchedules, "Manage Finance Report Schedules", "Create, update, pause, and resume controlled Finance report schedules.", CategoryReporting),
        new(RunReportSchedules, "Run Finance Report Schedules", "Run due or on-demand Finance report schedule occurrences.", CategoryReporting),

        new(OpenAccountingPeriods, "Open Accounting Periods", "Open future fiscal periods for controlled transaction posting.", CategoryPeriodClose),
        new(CloseAccountingPeriods, "Close Accounting Periods", "Close fiscal periods after month-end checks.", CategoryPeriodClose),
        new(ReopenAccountingPeriods, "Reopen Accounting Periods", "Reopen previously closed fiscal periods.", CategoryPeriodClose),
        new(ApproveAccountingPeriodReopens, "Approve Accounting Period Reopens", "Independently approve or reject controlled accounting-period reopen requests.", CategoryPeriodClose),
        new(MaintainCloseWorkspace, "Maintain Close Workspace", "Evaluate close controls, maintain task evidence, and certify preparation without granting final closure authority.", CategoryPeriodClose),
        new(RequestCloseExceptionWaivers, "Request Close Exception Waivers", "Request controlled acceptance of eligible, evidenced period-close exceptions.", CategoryPeriodClose),
        new(ApproveCloseExceptionWaivers, "Approve Close Exception Waivers", "Independently approve or reject eligible period-close exception waivers.", CategoryPeriodClose),

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
        new(EditBudgetReturns, "Edit Assigned Budget Returns", "Edit assigned budget worksheets before submission.", CategoryBudgeting),
        new(SubmitBudgetReturns, "Submit Budget Returns", "Submit assigned budget worksheets.", CategoryBudgeting),
        new(ApproveBudgetReturns, "Approve Budget Returns", "Approve or reject submitted budget worksheets.", CategoryBudgeting),
        new(LockBudgets, "Lock Budgets", "Lock approved budget scenarios.", CategoryBudgeting),
        new(ViewBudgetRevisions, "View Budget Revisions", "View virements, supplementary budgets, Board evidence, and resulting official versions.", CategoryBudgeting),
        new(MaintainBudgetRevisions, "Maintain Budget Revisions", "Prepare controlled virement and supplementary-budget requests.", CategoryBudgeting),
        new(SubmitBudgetRevisions, "Submit Budget Revisions", "Submit validated budget revisions to the configured Board approval workflow.", CategoryBudgeting),
        new(ApplyBudgetRevisions, "Apply Budget Revisions", "Apply an approved revision by creating and adopting an immutable successor budget.", CategoryBudgeting)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
