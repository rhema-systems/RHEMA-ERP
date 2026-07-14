using ErpSystem.Shared;

namespace ErpSystem.Api.Authorization;

public static class FinancePermissionPolicyMap
{
    private static readonly HashSet<string> ReadOnlyActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetAll",
        "Get",
        "GetActive",
        "GetById",
        "GetByCode",
        "GetByNumber",
        "GetByAccountNumber",
        "GetAccountById",
        "GetAccountBalance",
        "GetBalance",
        "GetBooks",
        "GetClaims",
        "GetCurrentRate",
        "GetDefault",
        "GetExchangeRateById",
        "GetFiscalYearById",
        "GetFiscalPeriodById",
        "GetJournalEntryById",
        "GetJournalEntries",
        "GetMatchingResult",
        "GetNextEntryNumber",
        "GetNextJournalNumber",
        "GetPending",
        "GetPendingApprovals",
        "GetPosition",
        "GetSummary",
        "GetTaxById",
        "GetTaxGroupById",
        "GetTaxGroupByCode",
        "GetTaxRule",
        "GetUnitAccountById",
        "GetUnitTypeById",
        "GetUnitTypeByCode",
        "GetValuationsByAsset",
        "CheckCodeUnique",
        "CheckCredit",
        "CheckDuplicate",
        "CheckThreshold",
        "Convert",
        "Validate",
        "ValidateAccountNumber",
        "CalculateDiscount",
        "CalculateRatio",
        "CalculateRatioForRange",
        "CalculateRatio",
        "CalculateTaxes",
        "GenerateAssetCode",
        "DownloadImportTemplate"
    };

    private static readonly HashSet<string> ReportActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetAgingReport",
        "GetDetailedAgingReport",
        "GetCashForecast",
        "GetSupplierStatement",
        "GetWithholdingTaxSummary",
        "GetApSummary",
        "GetCustomerStatement",
        "GetCollectionsDashboard",
        "GetArSummary",
        "GetBalanceSheet",
        "GetIncomeStatement",
        "GetTrialBalance",
        "GetDetailedLedger",
        "GetCashFlowStatement",
        "GetLedger",
        "GetMultiCurrencyDetailReport",
        "GetAssetRegister",
        "GetDashboard"
    };

    private static readonly HashSet<string> ExportActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Export",
        "Print",
        "ExportToExcel",
        "ExportToPdf"
    };

    public static IReadOnlyList<string> GetRequiredPolicies(
        string controllerName,
        string actionName,
        IEnumerable<string>? httpMethods = null,
        IEnumerable<string?>? routeTemplates = null)
    {
        var controller = NormalizeControllerName(controllerName);
        var action = actionName ?? string.Empty;
        var methods = (httpMethods ?? Array.Empty<string>())
            .Where(method => !string.IsNullOrWhiteSpace(method))
            .Select(method => method.ToUpperInvariant())
            .ToArray();
        var templates = (routeTemplates ?? Array.Empty<string?>())
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .Select(template => template!)
            .ToArray();

        var policies = controller switch
        {
            "Account" => AccountPolicy(action, methods),
            "AccountingBooks" => One(FinancePermissions.ViewFinance),
            "Allocation" => AllocationPolicy(action),
            "VendorInvoice" => VendorInvoicePolicy(action),
            "VendorPayment" => VendorPaymentPolicy(action),
            "PaymentBatch" => PaymentBatchPolicy(action),
            "ApReports" => One(FinancePermissions.RunFinanceReports),
            "Invoice" => ArInvoicePolicy(action),
            "Payment" => ArPaymentPolicy(action),
            "ArReports" => ReportPolicy(action),
            "BankAccount" => ReadOrManage(action, methods, FinancePermissions.ManageBankAccounts),
            "BankReconciliation" => BankReconciliationPolicy(action),
            "Budget" => BudgetPolicy(action),
            "CapitalProjects" => CapitalProjectsPolicy(action),
            "CashReports" => One(FinancePermissions.RunFinanceReports),
            "CashTransaction" => CashTransactionPolicy(action),
            "Currencies" => CurrencyPolicy(action, methods),
            "Customer" => CustomerPolicy(action, methods),
            "ExchangeRate" => ExchangeRatePolicy(action, methods),
            "FinanceApprovals" => FinanceApprovalPolicy(action),
            "Finance" => FinanceControllerPolicy(action),
            "FinanceReportExports" => One(FinancePermissions.ExportFinanceReports),
            "FinancePurchaseOrder" => FinancePurchaseOrderPolicy(action),
            "FinancePurchaseOrderReceipt" => FinancePurchaseOrderReceiptPolicy(action),
            "FinanceSettings" => FinanceSettingsPolicy(action, methods),
            "FiscalPeriod" => FiscalPeriodPolicy(action),
            "FixedAssetCategories" => ReadOrManage(action, methods, FinancePermissions.ManageFixedAssets),
            "FixedAssets" => FixedAssetsPolicy(action, templates),
            "GLIntegrationTest" => One(FinancePermissions.AdministerFinance),
            "JournalEntry" => JournalEntryPolicy(action),
            "LeaseAccounting" => LeaseAccountingPolicy(action, methods),
            "MigrationSignOff" => MigrationSignOffPolicy(action),
            "OpeningBalances" => OpeningBalancePolicy(action),
            "PaymentTerms" => ReadOrManage(action, methods, FinancePermissions.AdministerFinance),
            "RatioDefinition" => RatioDefinitionPolicy(action, methods),
            "Seed" => One(FinancePermissions.RunMigrationAdjustments),
            "SegmentStructure" => SegmentStructurePolicy(action, methods),
            "SubledgerAdjustmentJournal" => SubledgerAdjustmentPolicy(action),
            "SupplierReturns" => SupplierReturnsPolicy(action),
            "TaxConfiguration" => TaxConfigurationPolicy(action, methods),
            "TaxReports" => One(FinancePermissions.RunFinanceReports),
            "TaxRule" => TaxConfigurationPolicy(action, methods),
            "UnitAccount" => UnitAccountPolicy(action, methods),
            "UnitBudget" => UnitBudgetPolicy(action, methods),
            "UnitJournalEntry" => UnitJournalEntryPolicy(action),
            "UnitType" => UnitSetupPolicy(action, methods),
            _ => FallbackPolicy(action, methods)
        };

        return policies
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> AccountPolicy(string action, IReadOnlyCollection<string> methods)
    {
        if (IsRead(action, methods) || IsValidationAction(action))
        {
            return One(FinancePermissions.ViewFinance);
        }

        return One(FinancePermissions.ManageChartOfAccounts);
    }

    private static IReadOnlyList<string> AllocationPolicy(string action)
        => string.Equals(action, "RunAllocation", StringComparison.OrdinalIgnoreCase)
            ? One(FinancePermissions.PostJournalEntries)
            : ReadOrSpecific(action, FinancePermissions.AdministerFinance);

    private static IReadOnlyList<string> VendorInvoicePolicy(string action)
        => action switch
        {
            "Create" => One(FinancePermissions.CreateApInvoices),
            "Update" => One(FinancePermissions.EditApInvoices),
            "Delete" => One(FinancePermissions.DeleteApInvoices),
            "SubmitForApproval" => One(FinancePermissions.SubmitApInvoices),
            "Approve" => One(FinancePermissions.ApproveApInvoices),
            "Reject" => One(FinancePermissions.ApproveApInvoices),
            "Post" => One(FinancePermissions.PostApInvoices),
            "Void" => One(FinancePermissions.VoidApInvoices),
            "TwoWayMatch" or "ThreeWayMatch" => One(FinancePermissions.ManageApInvoices),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageApInvoices)
        };

    private static IReadOnlyList<string> VendorPaymentPolicy(string action)
        => action switch
        {
            "Create" or "Update" or "Allocate" or "Post" or "ReverseAllocation" or "ClearPayment" or "VoidPayment" => One(FinancePermissions.ProcessApPayments),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ProcessApPayments)
        };

    private static IReadOnlyList<string> PaymentBatchPolicy(string action)
        => action switch
        {
            "Approve" => One(FinancePermissions.ApproveApPayments),
            "Process" => One(FinancePermissions.ProcessApPayments),
            "Create" => One(FinancePermissions.ProcessApPayments),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ProcessApPayments)
        };

    private static IReadOnlyList<string> ArInvoicePolicy(string action)
        => action switch
        {
            "Create" => One(FinancePermissions.CreateArInvoices),
            "Update" => One(FinancePermissions.EditArInvoices),
            "Delete" => One(FinancePermissions.DeleteArInvoices),
            "Send" or "Post" => One(FinancePermissions.ApprovePostArInvoices),
            "Void" => One(FinancePermissions.VoidArInvoices),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageArInvoices)
        };

    private static IReadOnlyList<string> ArPaymentPolicy(string action)
        => action switch
        {
            "Create" or "Update" or "Allocate" or "Post" or "ClearPayment" or "BouncedPayment" or "CreateCreditNote" => One(FinancePermissions.ReceiveCustomerPayments),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ReceiveCustomerPayments)
        };

    private static IReadOnlyList<string> BankReconciliationPolicy(string action)
        => action switch
        {
            "Approve" => One(FinancePermissions.ApproveBankReconciliation),
            "StartReconciliation" or "CreateManualMatch" or "RemoveMatch" or "CreateAndPostAdjustment" or "Finalize" or "Cancel" => One(FinancePermissions.PerformBankReconciliation),
            _ when action.Contains("Match", StringComparison.OrdinalIgnoreCase) => One(FinancePermissions.PerformBankReconciliation),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.PerformBankReconciliation)
        };

    private static IReadOnlyList<string> BudgetPolicy(string action)
        => action switch
        {
            "CreateScenario" or "UpdateScenario" or "DeleteScenario" or "CreateReturn" or "BulkSaveEntries" => One(FinancePermissions.MaintainBudgets),
            "SubmitReturn" => One(FinancePermissions.SubmitBudgetReturns),
            "ApproveReturn" or "RejectReturn" => One(FinancePermissions.ApproveBudgetReturns),
            "LockScenario" => One(FinancePermissions.LockBudgets),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewBudgets) : One(FinancePermissions.MaintainBudgets)
        };

    private static IReadOnlyList<string> CapitalProjectsPolicy(string action)
        => action switch
        {
            "PostCost" or "Capitalize" => One(FinancePermissions.PostJournalEntries),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageFixedAssets)
        };

    private static IReadOnlyList<string> CashTransactionPolicy(string action)
        => action switch
        {
            "CreateReceipt" or "CreatePayment" or "CreateTransfer" or "Delete" => One(FinancePermissions.RecordCashBankTransactions),
            "Submit" => One(FinancePermissions.WorkflowSubmit),
            "Approve" => One(FinancePermissions.WorkflowApprove),
            "Reject" => One(FinancePermissions.WorkflowReject),
            "Return" => One(FinancePermissions.WorkflowRequestChanges),
            "Cancel" => One(FinancePermissions.WorkflowCancel),
            "Post" => One(FinancePermissions.WorkflowPostAfterApproval),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> CurrencyPolicy(string action, IReadOnlyCollection<string> methods)
    {
        if (IsRead(action, methods) || IsValidationAction(action))
        {
            return One(FinancePermissions.ViewFinance);
        }

        return One(FinancePermissions.ManageFxRates);
    }

    private static IReadOnlyList<string> CustomerPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) || IsValidationAction(action)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageArInvoices);

    private static IReadOnlyList<string> ExchangeRatePolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageFxRates);

    private static IReadOnlyList<string> FinanceApprovalPolicy(string action)
        => action switch
        {
            "Approve" => One(FinancePermissions.WorkflowApprove),
            "Reject" => One(FinancePermissions.WorkflowReject),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> FinanceControllerPolicy(string action)
        => action switch
        {
            "PostJournalEntry" => One(FinancePermissions.PostJournalEntries),
            "RunRevaluation" => One(FinancePermissions.RunFxRevaluation),
            _ when ReportActions.Contains(action) => One(FinancePermissions.RunFinanceReports),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> FinancePurchaseOrderPolicy(string action)
        => action switch
        {
            "SubmitForApproval" => One(FinancePermissions.SubmitApInvoices),
            "Approve" => One(FinancePermissions.ApproveApInvoices),
            "Reject" => One(FinancePermissions.ApproveApInvoices),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageApInvoices)
        };

    private static IReadOnlyList<string> FinancePurchaseOrderReceiptPolicy(string action)
        => action switch
        {
            "ConvertToVendorInvoice" => One(FinancePermissions.CreateApInvoices),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageApInvoices)
        };

    private static IReadOnlyList<string> FinanceSettingsPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.AdministerFinance);

    private static IReadOnlyList<string> FiscalPeriodPolicy(string action)
        => action switch
        {
            "ClosePeriod" or "CloseFiscalYear" or "LockPeriodForModule" => One(FinancePermissions.CloseAccountingPeriods),
            "ReopenPeriod" or "ReopenFiscalYear" or "UnlockPeriod" or "UnlockPeriodForModule" => One(FinancePermissions.ReopenAccountingPeriods),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.AdministerFinance)
        };

    private static IReadOnlyList<string> FixedAssetsPolicy(string action, IReadOnlyCollection<string> templates)
    {
        if (ExportActions.Contains(action) || templates.Any(template => template.Contains("export", StringComparison.OrdinalIgnoreCase)))
        {
            return One(FinancePermissions.ExportFinanceReports);
        }

        if (ReportActions.Contains(action) || templates.Any(template => template.Contains("reports", StringComparison.OrdinalIgnoreCase)))
        {
            return One(FinancePermissions.RunFinanceReports);
        }

        if (action.Contains("Depreciation", StringComparison.OrdinalIgnoreCase))
        {
            return IsRead(action, Array.Empty<string>())
                ? One(FinancePermissions.ViewFinance)
                : One(FinancePermissions.RunDepreciation);
        }

        if (action.Contains("Disposal", StringComparison.OrdinalIgnoreCase))
        {
            return IsRead(action, Array.Empty<string>())
                ? One(FinancePermissions.ViewFinance)
                : One(FinancePermissions.DisposeFixedAssets);
        }

        return IsRead(action, Array.Empty<string>()) || IsValidationAction(action)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageFixedAssets);
    }

    private static IReadOnlyList<string> JournalEntryPolicy(string action)
        => action switch
        {
            "CreateJournalEntry" => One(FinancePermissions.CreateJournalEntries),
            "UpdateJournalEntry" or "LinkAttachment" or "UnlinkAttachment" => One(FinancePermissions.EditJournalEntries),
            "DeleteJournalEntry" => One(FinancePermissions.DeleteJournalEntries),
            "PostJournalEntry" => One(FinancePermissions.PostJournalEntries),
            "ReverseJournalEntry" => One(FinancePermissions.ReverseJournalEntries),
            "RequestApproval" => One(FinancePermissions.SubmitJournalEntries),
            "WithdrawApproval" => One(FinancePermissions.WorkflowCancel),
            "ApproveJournalEntry" => One(FinancePermissions.ApproveJournalEntries),
            "RejectJournalEntry" => One(FinancePermissions.ApproveJournalEntries),
            "GetPendingApprovals" => One(FinancePermissions.ApproveJournalEntries),
            "GetJournalEntryAuditTrail" => One(FinancePermissions.ViewFinance),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> LeaseAccountingPolicy(string action, IReadOnlyCollection<string> methods)
        => action switch
        {
            "PostPeriodJournal" => One(FinancePermissions.PostJournalEntries),
            _ => IsRead(action, methods) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageFixedAssets)
        };

    private static IReadOnlyList<string> MigrationSignOffPolicy(string action)
        => action switch
        {
            "RepairPostingBackReferences" or "RebuildBankSnapshots" or "ReviewFinalSignOff" => One(FinancePermissions.RunMigrationAdjustments),
            _ => One(FinancePermissions.RunMigrationDiagnostics)
        };

    private static IReadOnlyList<string> OpeningBalancePolicy(string action)
        => action switch
        {
            "Get" or "Diagnostics" or "Validate" => One(FinancePermissions.RunMigrationDiagnostics),
            _ => One(FinancePermissions.RunMigrationAdjustments)
        };

    private static IReadOnlyList<string> RatioDefinitionPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) || action.Contains("Calculate", StringComparison.OrdinalIgnoreCase)
            ? One(FinancePermissions.RunFinanceReports)
            : One(FinancePermissions.AdministerFinance);

    private static IReadOnlyList<string> SegmentStructurePolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) || IsValidationAction(action)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageChartOfAccounts);

    private static IReadOnlyList<string> SubledgerAdjustmentPolicy(string action)
        => action switch
        {
            "CreateAndPost" => One(FinancePermissions.PostJournalEntries),
            "Reverse" => One(FinancePermissions.ReverseJournalEntries),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> SupplierReturnsPolicy(string action)
        => action switch
        {
            "Approve" => One(FinancePermissions.ApproveApInvoices),
            _ => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.ManageApInvoices)
        };

    private static IReadOnlyList<string> TaxConfigurationPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) || string.Equals(action, "CalculateTaxes", StringComparison.OrdinalIgnoreCase)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageTaxConfiguration);

    private static IReadOnlyList<string> UnitAccountPolicy(string action, IReadOnlyCollection<string> methods)
    {
        if (string.Equals(action, "RecalculateBalances", StringComparison.OrdinalIgnoreCase))
        {
            return One(FinancePermissions.ManageChartOfAccounts);
        }

        return IsRead(action, methods)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.ManageChartOfAccounts);
    }

    private static IReadOnlyList<string> UnitBudgetPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods)
            ? One(FinancePermissions.ViewBudgets)
            : One(FinancePermissions.MaintainBudgets);

    private static IReadOnlyList<string> UnitJournalEntryPolicy(string action)
        => action switch
        {
            "CreateUnitJournalEntry" => One(FinancePermissions.CreateJournalEntries),
            "UpdateUnitJournalEntry" => One(FinancePermissions.EditJournalEntries),
            "DeleteUnitJournalEntry" => One(FinancePermissions.DeleteJournalEntries),
            "SubmitForApproval" => One(FinancePermissions.SubmitJournalEntries),
            "Approve" or "Reject" => One(FinancePermissions.ApproveJournalEntries),
            "Post" => One(FinancePermissions.PostJournalEntries),
            "Reverse" => One(FinancePermissions.ReverseJournalEntries),
            _ => One(FinancePermissions.ViewFinance)
        };

    private static IReadOnlyList<string> UnitSetupPolicy(string action, IReadOnlyCollection<string> methods)
        => IsRead(action, methods) ? One(FinancePermissions.ViewFinance) : One(FinancePermissions.AdministerFinance);

    private static IReadOnlyList<string> ReadOrManage(string action, IReadOnlyCollection<string> methods, string managePermission)
        => IsRead(action, methods) ? One(FinancePermissions.ViewFinance) : One(managePermission);

    private static IReadOnlyList<string> ReadOrSpecific(string action, string writePermission)
        => IsRead(action, Array.Empty<string>()) ? One(FinancePermissions.ViewFinance) : One(writePermission);

    private static IReadOnlyList<string> ReportPolicy(string action)
        => ExportActions.Contains(action)
            ? One(FinancePermissions.ExportFinanceReports)
            : One(FinancePermissions.RunFinanceReports);

    private static IReadOnlyList<string> FallbackPolicy(string action, IReadOnlyCollection<string> methods)
    {
        if (ReportActions.Contains(action))
        {
            return One(FinancePermissions.RunFinanceReports);
        }

        if (ExportActions.Contains(action))
        {
            return One(FinancePermissions.ExportFinanceReports);
        }

        return IsRead(action, methods)
            ? One(FinancePermissions.ViewFinance)
            : One(FinancePermissions.MaintainFinance);
    }

    private static bool IsRead(string action, IReadOnlyCollection<string> methods)
    {
        if (ReadOnlyActions.Contains(action) || ReportActions.Contains(action) || action.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return methods.Count > 0 && methods.All(method => method == "GET");
    }

    private static bool IsValidationAction(string action)
        => action.StartsWith("Check", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("Validate", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("Calculate", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("Preview", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("Construct", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("Parse", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeControllerName(string controllerName)
        => controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? controllerName[..^"Controller".Length]
            : controllerName;

    private static string[] One(string permission) => new[] { permission };
}
