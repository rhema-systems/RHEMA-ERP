using System.Globalization;
using System.Text;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.Reporting;

public sealed class FinanceReportExportService : IFinanceReportExportService
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private readonly IGeneralLedgerService _generalLedgerService;
    private readonly IApReportsService _apReportsService;
    private readonly IArReportsService _arReportsService;
    private readonly IFixedAssetReportsService _fixedAssetReportsService;
    private readonly ITaxReportingService? _taxReportingService;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FinanceReportExportService> _logger;

    public FinanceReportExportService(
        IGeneralLedgerService generalLedgerService,
        IApReportsService apReportsService,
        IArReportsService arReportsService,
        IFixedAssetReportsService fixedAssetReportsService,
        ICurrentUserService currentUserService,
        ILogger<FinanceReportExportService> logger,
        IFinanceAuditService? financeAuditService = null,
        ITaxReportingService? taxReportingService = null)
    {
        _generalLedgerService = generalLedgerService;
        _apReportsService = apReportsService;
        _arReportsService = arReportsService;
        _fixedAssetReportsService = fixedAssetReportsService;
        _taxReportingService = taxReportingService;
        _currentUserService = currentUserService;
        _logger = logger;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public Task<FinanceReportExportResultDto> ExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken = default)
        => GenerateAsync(request, isPrint: false, cancellationToken);

    public Task<FinanceReportExportResultDto> PrintAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken = default)
        => GenerateAsync(request, isPrint: true, cancellationToken);

    private async Task<FinanceReportExportResultDto> GenerateAsync(
        FinanceReportExportRequestDto request,
        bool isPrint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reportType = NormalizeReportType(request.ReportType);
        var format = NormalizeFormat(request.Format);

        try
        {
            if (format != FinanceReportExportFormats.Csv)
            {
                throw new NotSupportedException($"Finance report export format '{request.Format}' is not supported. Supported format: Csv.");
            }

            var result = reportType switch
            {
                FinanceReportExportTypes.TrialBalance => await BuildTrialBalanceExportAsync(request, cancellationToken),
                FinanceReportExportTypes.BalanceSheet => await BuildBalanceSheetExportAsync(request, cancellationToken),
                FinanceReportExportTypes.IncomeStatement => await BuildIncomeStatementExportAsync(request, cancellationToken),
                FinanceReportExportTypes.DetailedLedger => await BuildDetailedLedgerExportAsync(request, cancellationToken),
                FinanceReportExportTypes.CashBankLedger => await BuildCashBankLedgerExportAsync(request, cancellationToken),
                FinanceReportExportTypes.ApAging => await BuildApAgingExportAsync(request, cancellationToken),
                FinanceReportExportTypes.ArAging => await BuildArAgingExportAsync(request, cancellationToken),
                FinanceReportExportTypes.CustomerStatement => await BuildCustomerStatementExportAsync(request, cancellationToken),
                FinanceReportExportTypes.SupplierStatement => await BuildSupplierStatementExportAsync(request, cancellationToken),
                FinanceReportExportTypes.ApControlReconciliation => await BuildApControlReconciliationExportAsync(request, cancellationToken),
                FinanceReportExportTypes.ArControlReconciliation => await BuildArControlReconciliationExportAsync(request, cancellationToken),
                FinanceReportExportTypes.FixedAssetRegister => await BuildFixedAssetRegisterExportAsync(request, cancellationToken),
                FinanceReportExportTypes.FixedAssetRollForward => await BuildFixedAssetRollForwardExportAsync(request, cancellationToken),
                FinanceReportExportTypes.FixedAssetGlReconciliation => await BuildFixedAssetGlReconciliationExportAsync(request, cancellationToken),
                FinanceReportExportTypes.TaxOutput => await BuildTaxSnapshotExportAsync(request, FinanceReportExportTypes.TaxOutput, cancellationToken),
                FinanceReportExportTypes.TaxInput => await BuildTaxSnapshotExportAsync(request, FinanceReportExportTypes.TaxInput, cancellationToken),
                FinanceReportExportTypes.TaxNetSummary => await BuildTaxSnapshotExportAsync(request, FinanceReportExportTypes.TaxNetSummary, cancellationToken),
                FinanceReportExportTypes.TaxExemptZeroOutOfScope => await BuildTaxTreatmentExportAsync(request, cancellationToken),
                FinanceReportExportTypes.VatWithholding => await BuildTaxWithholdingExportAsync(request, FinanceReportExportTypes.VatWithholding, cancellationToken),
                FinanceReportExportTypes.WhtPayable => await BuildTaxWithholdingExportAsync(request, FinanceReportExportTypes.WhtPayable, cancellationToken),
                FinanceReportExportTypes.WhtReceivable => await BuildTaxWithholdingExportAsync(request, FinanceReportExportTypes.WhtReceivable, cancellationToken),
                FinanceReportExportTypes.TaxAccountReconciliation => await BuildTaxAccountReconciliationExportAsync(request, cancellationToken),
                FinanceReportExportTypes.TaxConfigurationHistory => await BuildTaxConfigurationHistoryExportAsync(request, cancellationToken),
                FinanceReportExportTypes.TaxCovidDiagnostic => await BuildTaxCovidDiagnosticExportAsync(request, cancellationToken),
                _ => throw new NotSupportedException($"Finance report type '{request.ReportType}' is not supported for backend export.")
            };

            result.Format = format;
            result.FileName = BuildFileName(reportType, isPrint);
            result.ContentType = "text/csv";
            result.GeneratedAt = DateTime.UtcNow;
            await RecordSuccessAuditAsync(reportType, result, request, isPrint, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Finance report {ReportType} {Action} failed", reportType, isPrint ? "print" : "export");
            await RecordFailureAuditAsync(reportType, request, ex, isPrint, cancellationToken);
            throw;
        }
    }

    private async Task<FinanceReportExportResultDto> BuildTrialBalanceExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await _generalLedgerService.GenerateTrialBalanceAsync(new TrialBalanceRequestDto
        {
            AsAtDate = request.AsOfDate ?? DateTime.UtcNow.Date,
            PeriodStart = request.PeriodStart,
            BookClassification = request.BookClassification,
            IncludeZeroBalances = request.IncludeZeroBalances,
            AccountIds = request.AccountIds,
            SegmentFilters = request.SegmentFilters
        });

        var rows = new List<string[]>
        {
            new[] { "AccountNumber", "AccountName", "AccountType", "OpeningDebit", "OpeningCredit", "PeriodDebits", "PeriodCredits", "DebitBalance", "CreditBalance" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.AccountNumber,
            line.AccountName,
            line.AccountType,
            Money(line.OpeningDebitBalance),
            Money(line.OpeningCreditBalance),
            Money(line.PeriodDebits),
            Money(line.PeriodCredits),
            Money(line.DebitBalance),
            Money(line.CreditBalance)
        }));
        rows.Add(new[] { "TOTAL", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Money(report.TotalDebits), Money(report.TotalCredits) });

        return BuildCsvResult(
            FinanceReportExportTypes.TrialBalance,
            "Posted GL",
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>
            {
                ["TotalDebits"] = report.TotalDebits,
                ["TotalCredits"] = report.TotalCredits,
                ["Difference"] = report.Difference
            });
    }

    private async Task<FinanceReportExportResultDto> BuildBalanceSheetExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await _generalLedgerService.GenerateBalanceSheetAsync(new BalanceSheetRequestDto
        {
            AsAtDate = request.AsOfDate ?? DateTime.UtcNow.Date,
            BookClassification = request.BookClassification,
            IncludeAccountDetails = request.IncludeAccountDetails,
            AccountIds = request.AccountIds,
            SegmentFilters = request.SegmentFilters,
            LayoutId = request.LayoutId,
            UseDefaultLayout = request.UseDefaultLayout
        });

        List<string[]> rows;
        int rowCount;
        string sourceOfTruth;
        var warnings = new List<string>(report.PresentationWarnings);
        if (report.LayoutExecution != null)
        {
            var layout = report.LayoutExecution;
            rows = new List<string[]>
            {
                new[] { "RowCode", "ParentRowCode", "RowType", "Sequence", "Label", "Amount", "AccountNumbers" }
            };
            foreach (var row in layout.Rows.OrderBy(row => row.Sequence))
            {
                rows.Add(new[]
                {
                    row.RowCode,
                    row.ParentRowCode ?? string.Empty,
                    row.RowType.ToString(),
                    row.Sequence.ToString(InvariantCulture),
                    row.Label,
                    Money(row.Amount),
                    string.Join(";", row.Accounts.Select(account => account.AccountNumber))
                });
            }

            rowCount = layout.Rows.Count;
            sourceOfTruth =
                $"Posted GL | Layout {layout.LayoutCode} v{layout.VersionNumber}";
            warnings.AddRange(layout.Warnings.Select(warning => warning.Message));
            if (layout.Reconciliation.UnmappedNonZeroAccountCount > 0)
            {
                warnings.Add(
                    $"{layout.Reconciliation.UnmappedNonZeroAccountCount} non-zero eligible GL account(s) are not mapped to this layout.");
            }
        }
        else
        {
            rows = new List<string[]>
            {
                new[] { "Section", "Category", "LineItem", "Amount", "AccountNumbers" }
            };
            foreach (var section in report.Sections.OrderBy(s => s.SectionOrder))
            {
                foreach (var category in section.Categories.OrderBy(c => c.CategoryOrder))
                {
                    foreach (var line in category.LineItems.OrderBy(l => l.LineOrder))
                    {
                        rows.Add(new[]
                        {
                            section.SectionName,
                            category.CategoryName,
                            line.LineItemName,
                            Money(line.Amount),
                            line.AccountNumbers == null ? string.Empty : string.Join(";", line.AccountNumbers)
                        });
                    }
                }
            }
            rows.Add(new[] { "TOTAL", "Assets", string.Empty, Money(report.TotalAssets), string.Empty });
            rows.Add(new[] { "TOTAL", "Liabilities", string.Empty, Money(report.TotalLiabilities), string.Empty });
            rows.Add(new[] { "TOTAL", "Equity", string.Empty, Money(report.TotalEquity), string.Empty });
            rowCount = rows.Count - 4;
            sourceOfTruth = "Posted GL | Legacy account classification";
        }

        return BuildCsvResult(
            FinanceReportExportTypes.BalanceSheet,
            sourceOfTruth,
            rows,
            rowCount,
            new Dictionary<string, decimal>
            {
                ["TotalAssets"] = report.TotalAssets,
                ["TotalLiabilities"] = report.TotalLiabilities,
                ["TotalEquity"] = report.TotalEquity,
                ["BalanceCheckDifference"] = report.TotalAssets - (report.TotalLiabilities + report.TotalEquity)
            },
            warnings);
    }

    private async Task<FinanceReportExportResultDto> BuildIncomeStatementExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await _generalLedgerService.GenerateIncomeStatementAsync(new IncomeStatementRequestDto
        {
            PeriodStart = request.PeriodStart ?? DateTime.UtcNow.Date,
            PeriodEnd = request.PeriodEnd ?? request.AsOfDate ?? DateTime.UtcNow.Date,
            BookClassification = request.BookClassification,
            IncludeAccountDetails = request.IncludeAccountDetails,
            AccountIds = request.AccountIds,
            SegmentFilters = request.SegmentFilters,
            LayoutId = request.LayoutId,
            UseDefaultLayout = request.UseDefaultLayout
        });

        List<string[]> rows;
        int rowCount;
        string sourceOfTruth;
        var warnings = new List<string>(report.PresentationWarnings);
        if (report.LayoutExecution != null)
        {
            var layout = report.LayoutExecution;
            rows = new List<string[]>
            {
                new[] { "RowCode", "ParentRowCode", "RowType", "Sequence", "Label", "Amount", "AccountNumbers" }
            };
            foreach (var row in layout.Rows.OrderBy(row => row.Sequence))
            {
                rows.Add(new[]
                {
                    row.RowCode,
                    row.ParentRowCode ?? string.Empty,
                    row.RowType.ToString(),
                    row.Sequence.ToString(InvariantCulture),
                    row.Label,
                    Money(row.Amount),
                    string.Join(";", row.Accounts.Select(account => account.AccountNumber))
                });
            }

            rowCount = layout.Rows.Count;
            sourceOfTruth =
                $"Posted GL | Layout {layout.LayoutCode} v{layout.VersionNumber}";
            warnings.AddRange(layout.Warnings.Select(warning => warning.Message));
            if (layout.Reconciliation.UnmappedNonZeroAccountCount > 0)
            {
                warnings.Add(
                    $"{layout.Reconciliation.UnmappedNonZeroAccountCount} non-zero eligible GL account(s) are not mapped to this layout.");
            }
        }
        else
        {
            rows = new List<string[]>
            {
                new[] { "Section", "LineItem", "Amount", "AccountNumbers" }
            };
            foreach (var section in report.Sections.OrderBy(s => s.SectionOrder))
            {
                foreach (var line in section.LineItems.OrderBy(l => l.LineOrder))
                {
                    rows.Add(new[]
                    {
                        section.SectionName,
                        line.LineItemName,
                        Money(line.Amount),
                        line.AccountNumbers == null ? string.Empty : string.Join(";", line.AccountNumbers)
                    });
                }
            }
            rows.Add(new[] { "TOTAL", "GrossProfit", Money(report.GrossProfit), string.Empty });
            rows.Add(new[] { "TOTAL", "OperatingProfit", Money(report.OperatingProfit), string.Empty });
            rows.Add(new[] { "TOTAL", "ProfitBeforeTax", Money(report.ProfitBeforeTax), string.Empty });
            rows.Add(new[] { "TOTAL", "NetProfit", Money(report.NetProfit), string.Empty });
            rowCount = report.Sections.Sum(section => section.LineItems.Count);
            sourceOfTruth =
                "Posted GL with non-operating disposal gain presentation mapping | Legacy account classification";
        }

        return BuildCsvResult(
            FinanceReportExportTypes.IncomeStatement,
            sourceOfTruth,
            rows,
            rowCount,
            new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = report.TotalRevenue,
                ["TotalOperatingExpenses"] = report.TotalOperatingExpenses,
                ["TotalOtherIncome"] = report.TotalOtherIncome,
                ["TotalOtherExpenses"] = report.TotalOtherExpenses,
                ["NetProfit"] = report.NetProfit
            },
            warnings);
    }

    private async Task<FinanceReportExportResultDto> BuildDetailedLedgerExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await _generalLedgerService.GenerateDetailedLedgerAsync(new DetailedLedgerRequestDto
        {
            StartDate = request.PeriodStart ?? DateTime.UtcNow.Date,
            EndDate = request.PeriodEnd ?? request.AsOfDate ?? DateTime.UtcNow.Date,
            AccountIds = request.AccountIds,
            BookClassification = request.BookClassification,
            IncludeReversed = request.IncludeReversed,
            IncludeOpeningBalances = request.IncludeOpeningBalances,
            SegmentFilters = request.SegmentFilters
        });

        var rows = new List<string[]>
        {
            new[] { "AccountNumber", "AccountName", "JournalEntryNumber", "TransactionDate", "LineNumber", "Description", "Reference", "SourceModule", "Debit", "Credit", "RunningBalance", "RunningBalanceType", "Segment" }
        };
        foreach (var account in report.Accounts)
        {
            rows.AddRange(account.Lines.Select(line => new[]
            {
                account.AccountNumber,
                account.AccountName,
                line.JournalEntryNumber,
                Date(line.TransactionDate),
                line.LineNumber.ToString(InvariantCulture),
                line.Description,
                line.Reference,
                line.SourceModule,
                Money(line.DebitAmount),
                Money(line.CreditAmount),
                Money(line.RunningBalance),
                line.RunningBalanceType,
                line.SegmentString ?? string.Empty
            }));
        }

        return BuildCsvResult(
            FinanceReportExportTypes.DetailedLedger,
            "Posted GL",
            rows,
            report.Accounts.Sum(account => account.Lines.Count),
            new Dictionary<string, decimal>
            {
                ["TotalDebits"] = report.TotalDebits,
                ["TotalCredits"] = report.TotalCredits
            });
    }

    private async Task<FinanceReportExportResultDto> BuildCashBankLedgerExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await _generalLedgerService.GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
        {
            StartDate = request.PeriodStart ?? DateTime.UtcNow.Date,
            EndDate = request.PeriodEnd ?? request.AsOfDate ?? DateTime.UtcNow.Date,
            BankAccountIds = request.BankAccountIds,
            GlAccountIds = request.GlAccountIds,
            BookClassification = request.BookClassification,
            IncludeOpeningBalances = request.IncludeOpeningBalances,
            SegmentFilters = request.SegmentFilters
        });

        var rows = new List<string[]>
        {
            new[] { "BankAccountNumber", "BankAccountName", "BankCurrency", "GlAccountNumber", "JournalEntryNumber", "TransactionDate", "Description", "Reference", "SourceDocumentType", "Debit", "Credit", "RunningBalance", "StoredSnapshotBalance", "SnapshotVariance" }
        };
        foreach (var account in report.Accounts)
        {
            rows.AddRange(account.Lines.Select(line => new[]
            {
                account.BankAccountNumber,
                account.BankAccountName,
                account.BankCurrencyCode,
                account.GlAccountNumber,
                line.JournalEntryNumber,
                Date(line.TransactionDate),
                line.Description,
                line.Reference,
                line.SourceDocumentType,
                Money(line.DebitAmount),
                Money(line.CreditAmount),
                Money(line.RunningBalance),
                Money(account.StoredSnapshotBalance),
                Money(account.SnapshotVariance)
            }));
        }

        return BuildCsvResult(
            FinanceReportExportTypes.CashBankLedger,
            "Posted GL with stored bank snapshot variance only",
            rows,
            report.Accounts.Sum(account => account.Lines.Count),
            new Dictionary<string, decimal>
            {
                ["TotalOpeningBalance"] = report.TotalOpeningBalance,
                ["TotalReceipts"] = report.TotalReceipts,
                ["TotalPayments"] = report.TotalPayments,
                ["TotalClosingBalance"] = report.TotalClosingBalance
            });
    }

    private async Task<FinanceReportExportResultDto> BuildApAgingExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var report = await _apReportsService.GetDetailedAgingReportAsync(request.AsOfDate, request.SupplierId, cancellationToken);
        if (!report.UsesSettlementReadModel)
        {
            throw new InvalidOperationException("AP aging export requires the AP settlement read model. Legacy operational-field aging is not allowed for production export.");
        }

        var rows = new List<string[]>
        {
            new[] { "SupplierName", "InvoiceNumber", "InvoiceDate", "DueDate", "FunctionalCurrency", "FunctionalTotalAmount", "FunctionalSettledAmount", "FunctionalCreditedAmount", "FunctionalWithheldAmount", "FunctionalOutstandingAmount", "DocumentCurrency", "DocumentTotalAmount", "DocumentSettledAmount", "DocumentCreditedAmount", "DocumentWithheldAmount", "DocumentOutstandingAmount", "AgingBucket", "SettlementStatus", "SourcePostingEventId", "SourceJournalEntryId", "Diagnostics" }
        };
        foreach (var supplier in report.SupplierDetails)
        {
            rows.AddRange(supplier.Invoices.Select(invoice => new[]
            {
                supplier.SupplierName,
                invoice.InvoiceNumber,
                Date(invoice.InvoiceDate),
                Date(invoice.DueDate),
                invoice.CurrencyCode,
                Money(invoice.TotalAmount),
                Money(invoice.SettledAmount),
                Money(invoice.CreditedAmount),
                Money(invoice.WithheldAmount),
                Money(invoice.BalanceAmount),
                invoice.DocumentCurrencyCode,
                Money(invoice.DocumentTotalAmount),
                Money(invoice.DocumentSettledAmount),
                Money(invoice.DocumentCreditedAmount),
                Money(invoice.DocumentWithheldAmount),
                Money(invoice.DocumentBalanceAmount),
                invoice.AgingBucket,
                invoice.SettlementStatus ?? string.Empty,
                invoice.SourcePostingEventId?.ToString() ?? string.Empty,
                invoice.SourceJournalEntryId?.ToString() ?? string.Empty,
                invoice.DiagnosticFlags ?? string.Empty
            }));
        }

        return BuildCsvResult(
            FinanceReportExportTypes.ApAging,
            "AP settlement read model rebuilt from posted AP source documents and posting events",
            rows,
            report.SupplierDetails.Sum(supplier => supplier.Invoices.Count),
            new Dictionary<string, decimal>
            {
                ["TotalOutstanding"] = report.TotalOutstanding,
                ["Current"] = report.Current,
                ["ThirtyDays"] = report.ThirtyDays,
                ["SixtyDays"] = report.SixtyDays,
                ["NinetyPlusDays"] = report.NinetyPlusDays
            },
            report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"),
            usesSettlementReadModel: true);
    }

    private async Task<FinanceReportExportResultDto> BuildArAgingExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var report = await _arReportsService.GetDetailedAgingReportAsync(request.AsOfDate, request.CustomerId, cancellationToken);
        if (!report.UsesSettlementReadModel)
        {
            throw new InvalidOperationException("AR aging export requires the AR settlement read model. Legacy operational-field aging is not allowed for production export.");
        }

        var rows = new List<string[]>
        {
            new[] { "CustomerName", "InvoiceNumber", "InvoiceDate", "DueDate", "FunctionalCurrency", "FunctionalTotalAmount", "FunctionalPaidAmount", "FunctionalCreditedAmount", "FunctionalWithheldAmount", "FunctionalOutstandingAmount", "DocumentCurrency", "DocumentTotalAmount", "DocumentPaidAmount", "DocumentCreditedAmount", "DocumentWithheldAmount", "DocumentOutstandingAmount", "AgingBucket", "SettlementStatus", "SourcePostingEventId", "SourceJournalEntryId", "Diagnostics" }
        };
        foreach (var customer in report.Customers)
        {
            rows.AddRange(customer.Invoices.Select(invoice => new[]
            {
                customer.CustomerName,
                invoice.InvoiceNumber,
                Date(invoice.InvoiceDate),
                Date(invoice.DueDate),
                invoice.CurrencyCode,
                Money(invoice.TotalAmount),
                Money(invoice.PaidAmount),
                Money(invoice.CreditedAmount),
                Money(invoice.WithheldAmount),
                Money(invoice.BalanceAmount),
                invoice.DocumentCurrencyCode,
                Money(invoice.DocumentTotalAmount),
                Money(invoice.DocumentPaidAmount),
                Money(invoice.DocumentCreditedAmount),
                Money(invoice.DocumentWithheldAmount),
                Money(invoice.DocumentBalanceAmount),
                invoice.AgingBucket,
                invoice.SettlementStatus ?? string.Empty,
                invoice.SourcePostingEventId?.ToString() ?? string.Empty,
                invoice.SourceJournalEntryId?.ToString() ?? string.Empty,
                invoice.DiagnosticFlags ?? string.Empty
            }));
        }

        return BuildCsvResult(
            FinanceReportExportTypes.ArAging,
            "AR settlement read model rebuilt from posted AR source documents and posting events",
            rows,
            report.Customers.Sum(customer => customer.Invoices.Count),
            new Dictionary<string, decimal>
            {
                ["TotalOutstanding"] = report.Summary.GrandTotal,
                ["Current"] = report.Summary.TotalCurrent,
                ["Days1To30"] = report.Summary.TotalDays1To30,
                ["Days31To60"] = report.Summary.TotalDays31To60,
                ["Days61To90"] = report.Summary.TotalDays61To90,
                ["Days90Plus"] = report.Summary.TotalDays90Plus
            },
            report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"),
            usesSettlementReadModel: true);
    }

    private async Task<FinanceReportExportResultDto> BuildCustomerStatementExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var startDate = request.PeriodStart ?? DateTime.UtcNow.Date;
        var endDate = request.PeriodEnd ?? request.AsOfDate ?? DateTime.UtcNow.Date;
        var customerIds = ResolveReportIds(request.CustomerIds, request.CustomerId);
        var report = await _arReportsService.GetCustomerDetailedLedgerAsync(
            startDate,
            endDate,
            customerIds,
            request.ShowCustomerCurrency,
            cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "CustomerCode", "CustomerName", "StatementFrom", "StatementTo", "StatementCurrency", "LineDate", "LineType", "DocumentNumber", "Reference", "Description", "TransactionCurrency", "ExchangeRate", "Debit", "Credit", "RunningBalance" }
        };

        foreach (var customer in report.Customers)
        {
            rows.Add(new[]
            {
                customer.CustomerCode,
                customer.CustomerName,
                Date(report.FromDate),
                Date(report.ToDate),
                customer.CurrencyCode,
                Date(report.FromDate),
                "Opening Balance",
                string.Empty,
                string.Empty,
                "Opening balance brought forward",
                customer.CurrencyCode,
                string.Empty,
                string.Empty,
                string.Empty,
                Money(customer.OpeningBalance)
            });

            rows.AddRange(customer.Lines.Select(line => new[]
            {
                customer.CustomerCode,
                customer.CustomerName,
                Date(report.FromDate),
                Date(report.ToDate),
                customer.CurrencyCode,
                Date(line.TransactionDate),
                line.TransactionType,
                line.DocumentNumber,
                line.Reference ?? string.Empty,
                line.Description,
                line.TransactionCurrencyCode,
                line.ExchangeRate.ToString("0.######", InvariantCulture),
                Money(line.Debit),
                Money(line.Credit),
                Money(line.RunningBalance)
            }));

            rows.Add(new[]
            {
                customer.CustomerCode,
                customer.CustomerName,
                Date(report.FromDate),
                Date(report.ToDate),
                customer.CurrencyCode,
                Date(report.ToDate),
                "Closing Balance",
                string.Empty,
                string.Empty,
                "Closing balance carried forward",
                customer.CurrencyCode,
                string.Empty,
                Money(customer.TotalDebits),
                Money(customer.TotalCredits),
                Money(customer.ClosingBalance)
            });
        }

        var totals = report.CurrencyTotals.Count > 1
            ? report.CurrencyTotals.SelectMany(total => new Dictionary<string, decimal>
            {
                [$"{total.CurrencyCode}.TotalOpeningBalance"] = total.OpeningBalance,
                [$"{total.CurrencyCode}.TotalDebits"] = total.TotalDebits,
                [$"{total.CurrencyCode}.TotalCredits"] = total.TotalCredits,
                [$"{total.CurrencyCode}.TotalClosingBalance"] = total.ClosingBalance
            }).ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, decimal>
            {
                ["TotalOpeningBalance"] = report.TotalOpeningBalance,
                ["TotalDebits"] = report.TotalDebits,
                ["TotalCredits"] = report.TotalCredits,
                ["TotalClosingBalance"] = report.TotalClosingBalance
            };

        return BuildCsvResult(
            FinanceReportExportTypes.CustomerStatement,
            "AR subledger statement of account with opening balance, period movements, and closing balance",
            rows,
            report.Customers.Sum(customer => customer.Lines.Count + 2),
            totals,
            report.Warnings);
    }

    private async Task<FinanceReportExportResultDto> BuildSupplierStatementExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var startDate = request.PeriodStart ?? DateTime.UtcNow.Date;
        var endDate = request.PeriodEnd ?? request.AsOfDate ?? DateTime.UtcNow.Date;
        var supplierIds = ResolveReportIds(request.SupplierIds, request.SupplierId);
        var report = await _apReportsService.GetSupplierDetailedLedgerAsync(
            startDate,
            endDate,
            supplierIds,
            request.ShowSupplierCurrency,
            cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "SupplierCode", "SupplierName", "StatementFrom", "StatementTo", "StatementCurrency", "LineDate", "LineType", "DocumentNumber", "Reference", "Description", "TransactionCurrency", "ExchangeRate", "Debit", "Credit", "RunningBalance" }
        };

        foreach (var supplier in report.Suppliers)
        {
            rows.Add(new[]
            {
                supplier.SupplierCode,
                supplier.SupplierName,
                Date(report.FromDate),
                Date(report.ToDate),
                supplier.CurrencyCode,
                Date(report.FromDate),
                "Opening Balance",
                string.Empty,
                string.Empty,
                "Opening balance brought forward",
                supplier.CurrencyCode,
                string.Empty,
                string.Empty,
                string.Empty,
                Money(supplier.OpeningBalance)
            });

            rows.AddRange(supplier.Lines.Select(line => new[]
            {
                supplier.SupplierCode,
                supplier.SupplierName,
                Date(report.FromDate),
                Date(report.ToDate),
                supplier.CurrencyCode,
                Date(line.TransactionDate),
                line.TransactionType,
                line.DocumentNumber,
                line.Reference ?? string.Empty,
                line.Description,
                line.TransactionCurrencyCode,
                line.ExchangeRate.ToString("0.######", InvariantCulture),
                Money(line.Debit),
                Money(line.Credit),
                Money(line.RunningBalance)
            }));

            rows.Add(new[]
            {
                supplier.SupplierCode,
                supplier.SupplierName,
                Date(report.FromDate),
                Date(report.ToDate),
                supplier.CurrencyCode,
                Date(report.ToDate),
                "Closing Balance",
                string.Empty,
                string.Empty,
                "Closing balance carried forward",
                supplier.CurrencyCode,
                string.Empty,
                Money(supplier.TotalDebits),
                Money(supplier.TotalCredits),
                Money(supplier.ClosingBalance)
            });
        }

        var totals = report.CurrencyTotals.Count > 1
            ? report.CurrencyTotals.SelectMany(total => new Dictionary<string, decimal>
            {
                [$"{total.CurrencyCode}.TotalOpeningBalance"] = total.OpeningBalance,
                [$"{total.CurrencyCode}.TotalDebits"] = total.TotalDebits,
                [$"{total.CurrencyCode}.TotalCredits"] = total.TotalCredits,
                [$"{total.CurrencyCode}.TotalClosingBalance"] = total.ClosingBalance
            }).ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, decimal>
            {
                ["TotalOpeningBalance"] = report.TotalOpeningBalance,
                ["TotalDebits"] = report.TotalDebits,
                ["TotalCredits"] = report.TotalCredits,
                ["TotalClosingBalance"] = report.TotalClosingBalance
            };

        return BuildCsvResult(
            FinanceReportExportTypes.SupplierStatement,
            "AP subledger statement of account with opening balance, period movements, and closing balance",
            rows,
            report.Suppliers.Sum(supplier => supplier.Lines.Count + 2),
            totals,
            report.Warnings);
    }

    private async Task<FinanceReportExportResultDto> BuildApControlReconciliationExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var report = await _apReportsService.GetControlReconciliationAsync(request.AsOfDate, cancellationToken);
        return BuildControlReconciliationResult(FinanceReportExportTypes.ApControlReconciliation, report);
    }

    private async Task<FinanceReportExportResultDto> BuildArControlReconciliationExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var report = await _arReportsService.GetControlReconciliationAsync(request.AsOfDate, cancellationToken);
        return BuildControlReconciliationResult(FinanceReportExportTypes.ArControlReconciliation, report);
    }

    private static FinanceReportExportResultDto BuildControlReconciliationResult(
        string reportType,
        SubledgerControlReconciliationDto report)
    {
        var rows = new List<string[]>
        {
            new[] { "SourceModule", "AsOfDate", "ControlAccountNumber", "ControlAccountName", "ReadModelOutstanding", "PostedGlControlBalance", "Variance", "DocumentCount", "DiagnosticCount" },
            new[]
            {
                report.SourceModule,
                Date(report.AsOfDate),
                report.ControlAccountNumber ?? string.Empty,
                report.ControlAccountName ?? string.Empty,
                Money(report.ReadModelOutstanding),
                Money(report.PostedGlControlBalance),
                Money(report.Variance),
                report.DocumentCount.ToString(InvariantCulture),
                report.DiagnosticCount.ToString(InvariantCulture)
            }
        };

        foreach (var diagnostic in report.Diagnostics)
        {
            rows.Add(new[]
            {
                "Diagnostic",
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourceDocumentId?.ToString() ?? string.Empty,
                diagnostic.PostingEventId?.ToString() ?? string.Empty,
                Money(diagnostic.VarianceAmount),
                string.Empty,
                string.Empty,
                string.Empty
            });
        }

        return BuildCsvResult(
            reportType,
            "Settlement read model reconciled to posted GL control account",
            rows,
            1 + report.Diagnostics.Count,
            new Dictionary<string, decimal>
            {
                ["ReadModelOutstanding"] = report.ReadModelOutstanding,
                ["PostedGlControlBalance"] = report.PostedGlControlBalance,
                ["Variance"] = report.Variance
            },
            report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"),
            usesSettlementReadModel: true);
    }

    private async Task<FinanceReportExportResultDto> BuildFixedAssetRegisterExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var query = BuildFixedAssetQuery(request);
        var report = await _fixedAssetReportsService.GetAssetRegisterAsync(query);
        var rows = new List<string[]>
        {
            new[] { "AssetCode", "Name", "Category", "Status", "AcquisitionDate", "CapitalizationDate", "Cost", "AccumulatedDepreciation", "NetBookValue", "PostedGlCostMovement", "PostedGlAccumulatedDepreciationMovement", "Variance", "SourceDocumentType", "JournalEntryId", "PostingEventId" }
        };
        rows.AddRange(report.Items.Select(item => new[]
        {
            item.AssetCode,
            item.Name,
            item.CategoryName,
            item.Status.ToString(),
            Date(item.AcquisitionDate),
            Date(item.CapitalizationDate),
            Money(item.Cost),
            Money(item.AccumulatedDepreciation),
            Money(item.NetBookValue),
            Money(item.PostedGlCostMovement),
            Money(item.PostedGlAccumulatedDepreciationMovement),
            Money(item.ReconciliationVariance),
            item.SourceDocumentType ?? string.Empty,
            item.JournalEntryId?.ToString() ?? string.Empty,
            item.PostingEventId?.ToString() ?? string.Empty
        }));

        return BuildCsvResult(
            FinanceReportExportTypes.FixedAssetRegister,
            "Fixed asset subledger snapshots reconciled to posted GL references",
            rows,
            report.Items.Count,
            new Dictionary<string, decimal>
            {
                ["TotalCost"] = report.TotalCost,
                ["TotalAccumulatedDepreciation"] = report.TotalAccumulatedDepreciation,
                ["TotalNetBookValue"] = report.TotalNetBookValue
            });
    }

    private async Task<FinanceReportExportResultDto> BuildFixedAssetRollForwardExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var query = BuildFixedAssetQuery(request);
        var report = await _fixedAssetReportsService.GetRollForwardReportAsync(query);
        var rows = new List<string[]>
        {
            new[] { "Category", "Book", "AssetCount", "OpeningCost", "Additions", "RevaluationIncrease", "RevaluationDecrease", "ImpairmentAdditions", "DepreciationCharge", "AccumulatedDepreciationMovement", "Disposals", "AccumulatedDepreciationCleared", "AccumulatedImpairmentCleared", "ClosingCost", "ClosingAccumulatedDepreciation", "ClosingAccumulatedImpairment", "ClosingNBV" }
        };
        rows.AddRange(report.Rows.Select(row => new[]
        {
            row.CategoryName,
            row.BookClassification,
            row.AssetCount.ToString(InvariantCulture),
            Money(row.OpeningCost),
            Money(row.Additions),
            Money(row.RevaluationIncrease),
            Money(row.RevaluationDecrease),
            Money(row.ImpairmentAdditions),
            Money(row.DepreciationCharge),
            Money(row.AccumulatedDepreciationMovement),
            Money(row.Disposals),
            Money(row.AccumulatedDepreciationCleared),
            Money(row.AccumulatedImpairmentCleared),
            Money(row.ClosingCost),
            Money(row.ClosingAccumulatedDepreciation),
            Money(row.ClosingAccumulatedImpairment),
            Money(row.ClosingNetBookValue)
        }));
        var total = report.Totals;
        rows.Add(new[]
        {
            "TOTAL",
            total.BookClassification,
            total.AssetCount.ToString(InvariantCulture),
            Money(total.OpeningCost),
            Money(total.Additions),
            Money(total.RevaluationIncrease),
            Money(total.RevaluationDecrease),
            Money(total.ImpairmentAdditions),
            Money(total.DepreciationCharge),
            Money(total.AccumulatedDepreciationMovement),
            Money(total.Disposals),
            Money(total.AccumulatedDepreciationCleared),
            Money(total.AccumulatedImpairmentCleared),
            Money(total.ClosingCost),
            Money(total.ClosingAccumulatedDepreciation),
            Money(total.ClosingAccumulatedImpairment),
            Money(total.ClosingNetBookValue)
        });

        return BuildCsvResult(
            FinanceReportExportTypes.FixedAssetRollForward,
            "Fixed asset roll-forward from subledger snapshots tied to posted GL movements",
            rows,
            report.Rows.Count,
            new Dictionary<string, decimal>
            {
                ["OpeningCost"] = total.OpeningCost,
                ["Additions"] = total.Additions,
                ["DepreciationCharge"] = total.DepreciationCharge,
                ["Disposals"] = total.Disposals,
                ["ClosingNetBookValue"] = total.ClosingNetBookValue
            });
    }

    private async Task<FinanceReportExportResultDto> BuildFixedAssetGlReconciliationExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var query = BuildFixedAssetQuery(request);
        var report = await _fixedAssetReportsService.GetGlReconciliationReportAsync(query);
        var rows = new List<string[]>
        {
            new[] { "Area", "AccountNumber", "AccountName", "AccountType", "GlBalance", "SubledgerBalance", "Variance", "SourceDocumentCount", "MissingPostingReferenceCount", "GlWithoutSourceReferenceCount", "SubledgerWithoutGlCount", "PresentationWarning" }
        };
        rows.AddRange(report.Rows.Select(row => new[]
        {
            row.Area,
            row.AccountNumber ?? string.Empty,
            row.AccountName ?? string.Empty,
            row.AccountType?.ToString() ?? string.Empty,
            Money(row.GlBalance),
            Money(row.SubledgerBalance),
            Money(row.Variance),
            row.SourceDocumentCount.ToString(InvariantCulture),
            row.MissingPostingReferenceCount.ToString(InvariantCulture),
            row.GlWithoutSourceReferenceCount.ToString(InvariantCulture),
            row.SubledgerWithoutGlCount.ToString(InvariantCulture),
            row.PresentationWarning ?? string.Empty
        }));

        var warnings = report.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PresentationWarning))
            .Select(row => row.PresentationWarning!)
            .Concat(report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

        return BuildCsvResult(
            FinanceReportExportTypes.FixedAssetGlReconciliation,
            "Posted GL reconciled to fixed asset subledger snapshots",
            rows,
            report.Rows.Count,
            new Dictionary<string, decimal>
            {
                ["TotalGlBalance"] = report.TotalGlBalance,
                ["TotalSubledgerBalance"] = report.TotalSubledgerBalance,
                ["TotalVariance"] = report.TotalVariance
            },
            warnings);
    }

    private async Task<FinanceReportExportResultDto> BuildTaxSnapshotExportAsync(
        FinanceReportExportRequestDto request,
        string reportType,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var taxService = RequireTaxReportingService();
        var query = BuildTaxReportQuery(request);
        var report = reportType switch
        {
            FinanceReportExportTypes.TaxOutput => await taxService.GetOutputTaxReportAsync(query, cancellationToken),
            FinanceReportExportTypes.TaxInput => await taxService.GetInputTaxReportAsync(query, cancellationToken),
            FinanceReportExportTypes.TaxNetSummary => await taxService.GetNetVatSummaryAsync(query, cancellationToken),
            _ => throw new NotSupportedException($"Tax snapshot report '{reportType}' is not supported.")
        };

        var rows = new List<string[]>
        {
            new[] { "DocumentType", "DocumentNumber", "DocumentDate", "Counterparty", "TaxCode", "TaxName", "TaxCategory", "TaxGroup", "BaseAmount", "TaxableAmount", "TaxRate", "TaxAmount", "RecoverableInput", "TaxAccountNumber", "TaxAccountName", "JournalEntryId", "PostingEventId", "FilingPeriod" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.SourceDocumentType,
            line.SourceDocumentNumber,
            Date(line.SourceDocumentDate),
            line.CounterpartyName ?? string.Empty,
            line.TaxCode,
            line.TaxName,
            line.TaxCategory.ToString(),
            line.TaxGroupCode ?? string.Empty,
            Money(line.BaseAmount),
            Money(line.TaxableAmount),
            line.TaxRate.ToString("0.####", InvariantCulture),
            Money(line.TaxAmount),
            line.IsRecoverableInputTax.ToString(InvariantCulture),
            line.TaxAccountNumber ?? string.Empty,
            line.TaxAccountName ?? string.Empty,
            line.JournalEntryId?.ToString() ?? string.Empty,
            line.PostingEventId?.ToString() ?? string.Empty,
            line.FilingPeriod
        }));

        return BuildCsvResult(
            reportType,
            report.SourceOfTruthMode,
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>
            {
                ["TaxableBase"] = report.Totals.TaxableBase,
                ["VatAmount"] = report.Totals.VatAmount,
                ["NhilAmount"] = report.Totals.NhilAmount,
                ["GetFundAmount"] = report.Totals.GetFundAmount,
                ["TotalTaxAmount"] = report.Totals.TotalTaxAmount,
                ["PostedGlAmount"] = report.Totals.PostedGlAmount,
                ["Variance"] = report.Totals.Variance
            },
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private async Task<FinanceReportExportResultDto> BuildTaxTreatmentExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await RequireTaxReportingService()
            .GetExemptZeroRatedOutOfScopeReportAsync(BuildTaxReportQuery(request), cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "Module", "DocumentType", "DocumentNumber", "DocumentDate", "LineDescription", "TaxTreatment", "LineAmount", "TaxAmount", "JournalEntryId", "PostingEventId" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.SourceModule,
            line.SourceDocumentType,
            line.SourceDocumentNumber,
            Date(line.SourceDocumentDate),
            line.Description,
            line.TaxTreatment.ToString(),
            Money(line.LineAmount),
            Money(line.TaxAmount),
            line.JournalEntryId?.ToString() ?? string.Empty,
            line.PostingEventId?.ToString() ?? string.Empty
        }));

        return BuildCsvResult(
            FinanceReportExportTypes.TaxExemptZeroOutOfScope,
            report.SourceOfTruthMode,
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>
            {
                ["TaxableBase"] = report.Totals.TaxableBase,
                ["TotalTaxAmount"] = report.Totals.TotalTaxAmount
            },
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private async Task<FinanceReportExportResultDto> BuildTaxWithholdingExportAsync(
        FinanceReportExportRequestDto request,
        string reportType,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var taxService = RequireTaxReportingService();
        var query = BuildTaxReportQuery(request);
        var report = reportType switch
        {
            FinanceReportExportTypes.VatWithholding => await taxService.GetVatWithholdingReportAsync(query, cancellationToken),
            FinanceReportExportTypes.WhtPayable => await taxService.GetWhtPayableReportAsync(query, cancellationToken),
            FinanceReportExportTypes.WhtReceivable => await taxService.GetWhtReceivableReportAsync(query, cancellationToken),
            _ => throw new NotSupportedException($"Tax withholding report '{reportType}' is not supported.")
        };

        var rows = new List<string[]>
        {
            new[] { "WithholdingType", "Module", "DocumentType", "DocumentNumber", "DocumentDate", "Counterparty", "TaxCode", "TaxName", "TaxRate", "TaxableBase", "WithholdingAmount", "TaxAccountNumber", "TaxAccountName", "CertificateNumber", "CertificateDate", "CertificateStatus", "JournalEntryId", "PostingEventId" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.WithholdingType,
            line.SourceModule,
            line.SourceDocumentType,
            line.SourceDocumentNumber,
            Date(line.SourceDocumentDate),
            line.CounterpartyName ?? string.Empty,
            line.TaxCode ?? string.Empty,
            line.TaxName ?? string.Empty,
            line.TaxRate.ToString("0.####", InvariantCulture),
            Money(line.TaxableBase),
            Money(line.WithholdingAmount),
            line.TaxAccountNumber ?? string.Empty,
            line.TaxAccountName ?? string.Empty,
            line.CertificateNumber ?? string.Empty,
            Date(line.CertificateDate),
            line.CertificateStatus,
            line.JournalEntryId?.ToString() ?? string.Empty,
            line.PostingEventId?.ToString() ?? string.Empty
        }));

        return BuildCsvResult(
            reportType,
            report.SourceOfTruthMode,
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>
            {
                ["TaxableBase"] = report.Totals.TaxableBase,
                ["TotalWithholdingAmount"] = report.Totals.TotalWithholdingAmount
            },
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private async Task<FinanceReportExportResultDto> BuildTaxAccountReconciliationExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await RequireTaxReportingService()
            .GetTaxAccountReconciliationReportAsync(BuildTaxReportQuery(request), cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "Area", "TaxAccountNumber", "TaxAccountName", "SnapshotAmount", "PostedGlAmount", "Variance", "SnapshotLineCount", "PostedGlLineCount", "MissingPostingReferenceCount" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.Area,
            line.TaxAccountNumber ?? string.Empty,
            line.TaxAccountName ?? string.Empty,
            Money(line.SnapshotAmount),
            Money(line.PostedGlAmount),
            Money(line.Variance),
            line.SnapshotLineCount.ToString(InvariantCulture),
            line.PostedGlLineCount.ToString(InvariantCulture),
            line.MissingPostingReferenceCount.ToString(InvariantCulture)
        }));

        return BuildCsvResult(
            FinanceReportExportTypes.TaxAccountReconciliation,
            report.SourceOfTruthMode,
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>
            {
                ["SnapshotAmount"] = report.Totals.TotalTaxAmount,
                ["PostedGlAmount"] = report.Totals.PostedGlAmount,
                ["Variance"] = report.Totals.Variance
            },
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private async Task<FinanceReportExportResultDto> BuildTaxConfigurationHistoryExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await RequireTaxReportingService()
            .GetTaxConfigurationHistoryReportAsync(BuildTaxReportQuery(request), cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "TaxCode", "TaxName", "TaxCategory", "Applicability", "Rate", "EffectiveFrom", "EffectiveTo", "IsActive", "IsCurrentActiveCovidLevy", "HasOverlappingRateHistory", "TaxPayableAccountId", "TaxReceivableAccountId" }
        };
        rows.AddRange(report.Lines.Select(line => new[]
        {
            line.TaxCode,
            line.TaxName,
            line.TaxCategory.ToString(),
            line.Applicability.ToString(),
            line.Rate.ToString("0.####", InvariantCulture),
            Date(line.EffectiveFrom),
            Date(line.EffectiveTo),
            line.IsActive.ToString(InvariantCulture),
            line.IsCurrentActiveCovidLevy.ToString(InvariantCulture),
            line.HasOverlappingRateHistory.ToString(InvariantCulture),
            line.TaxPayableAccountId?.ToString() ?? string.Empty,
            line.TaxReceivableAccountId?.ToString() ?? string.Empty
        }));

        return BuildCsvResult(
            FinanceReportExportTypes.TaxConfigurationHistory,
            report.SourceOfTruthMode,
            rows,
            report.Lines.Count,
            new Dictionary<string, decimal>(),
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private async Task<FinanceReportExportResultDto> BuildTaxCovidDiagnosticExportAsync(
        FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        var report = await RequireTaxReportingService()
            .GetCurrentActiveCovidLevyDiagnosticReportAsync(BuildTaxReportQuery(request), cancellationToken);

        var rows = new List<string[]>
        {
            new[] { "Code", "Severity", "Message", "AsOfDate" }
        };
        rows.AddRange(report.Diagnostics.Select(d => new[]
        {
            d.Code,
            d.Severity,
            d.Message,
            Date(report.AsOfDate)
        }));

        return BuildCsvResult(
            FinanceReportExportTypes.TaxCovidDiagnostic,
            report.SourceOfTruthMode,
            rows,
            report.Diagnostics.Count,
            new Dictionary<string, decimal>(),
            report.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
    }

    private FixedAssetReportQueryDto BuildFixedAssetQuery(FinanceReportExportRequestDto request)
    {
        return request.FixedAssetQuery ?? new FixedAssetReportQueryDto
        {
            FromDate = request.PeriodStart,
            ToDate = request.PeriodEnd ?? request.AsOfDate,
            AccountId = request.AccountIds.Count == 1 ? request.AccountIds[0] : (Guid?)null,
            BookClassification = request.BookClassification
        };
    }

    private TaxReportRequestDto BuildTaxReportQuery(FinanceReportExportRequestDto request)
    {
        return request.TaxReportQuery ?? new TaxReportRequestDto
        {
            FromDate = request.PeriodStart,
            ToDate = request.PeriodEnd ?? request.AsOfDate,
            TaxAccountId = request.AccountIds.Count == 1 ? request.AccountIds[0] : (Guid?)null,
            CustomerId = request.CustomerId,
            SupplierId = request.SupplierId
        };
    }

    private ITaxReportingService RequireTaxReportingService()
    {
        return _taxReportingService
            ?? throw new InvalidOperationException("Tax reporting service is not configured for finance report exports.");
    }

    private static FinanceReportExportResultDto BuildCsvResult(
        string reportType,
        string sourceOfTruth,
        List<string[]> rows,
        int rowCount,
        Dictionary<string, decimal> totals,
        IEnumerable<string>? warnings = null,
        bool usesSettlementReadModel = false)
    {
        var warningList = warnings?
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var csvRows = new List<string[]>
        {
            new[] { "ReportType", reportType },
            new[] { "SourceOfTruth", sourceOfTruth },
            new[] { "UsesSettlementReadModel", usesSettlementReadModel.ToString(InvariantCulture) },
            new[] { "GeneratedAtUtc", DateTime.UtcNow.ToString("O", InvariantCulture) }
        };

        foreach (var warning in warningList)
        {
            csvRows.Add(new[] { "Warning", warning });
        }

        csvRows.Add(Array.Empty<string>());
        csvRows.AddRange(rows);

        return new FinanceReportExportResultDto
        {
            ReportType = reportType,
            SourceOfTruthMode = sourceOfTruth,
            RowCount = rowCount,
            UsesSettlementReadModel = usesSettlementReadModel,
            Warnings = warningList,
            Totals = totals,
            Content = Encoding.UTF8.GetBytes(ToCsv(csvRows))
        };
    }

    private async Task RecordSuccessAuditAsync(
        string reportType,
        FinanceReportExportResultDto result,
        FinanceReportExportRequestDto request,
        bool isPrint,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        var eventTypes = GetSuccessAuditEvents(reportType, isPrint);
        foreach (var eventType in eventTypes)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = TenantId,
                SourceModule = "Finance.Reporting",
                SourceDocumentType = "FinanceReport",
                AfterValues = new
                {
                    request.ReportType,
                    result.Format,
                    result.FileName,
                    result.RowCount,
                    result.SourceOfTruthMode,
                    result.UsesSettlementReadModel,
                    totals = result.Totals,
                    warnings = result.Warnings
                },
                Context = new
                {
                    request.AsOfDate,
                    request.PeriodStart,
                    request.PeriodEnd,
                    request.BookClassification,
                    accountFilterCount = request.AccountIds.Count,
                    segmentFilterCount = request.SegmentFilters.Count,
                    outputFormat = result.Format,
                    generatedFile = result.FileName
                },
                Resource = "Finance.ReportExport",
                ResourceId = $"{reportType}:{result.GeneratedAt:O}"
            }, cancellationToken);
        }
    }

    private async Task RecordFailureAuditAsync(
        string reportType,
        FinanceReportExportRequestDto request,
        Exception exception,
        bool isPrint,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = isPrint ? FinanceAuditEvents.ReportPrintFailed : FinanceAuditEvents.ReportExportFailed,
            TenantId = TenantId,
            SourceModule = "Finance.Reporting",
            SourceDocumentType = "FinanceReport",
            AfterValues = new
            {
                request.ReportType,
                request.Format,
                error = exception.Message
            },
            Context = new
            {
                request.AsOfDate,
                request.PeriodStart,
                request.PeriodEnd,
                request.BookClassification
            },
            Resource = "Finance.ReportExport",
            ResourceId = reportType
        }, cancellationToken);

        if (!isPrint && IsTaxReport(reportType))
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.TaxReportExportFailed,
                TenantId = TenantId,
                SourceModule = "Finance.Reporting",
                SourceDocumentType = "FinanceReport",
                AfterValues = new
                {
                    request.ReportType,
                    request.Format,
                    error = exception.Message
                },
                Resource = "Finance.ReportExport",
                ResourceId = reportType
            }, cancellationToken);
        }
    }

    private static IEnumerable<string> GetSuccessAuditEvents(string reportType, bool isPrint)
    {
        if (isPrint)
        {
            yield return FinanceAuditEvents.ReportPrinted;
            yield break;
        }

        yield return FinanceAuditEvents.ReportExported;

        switch (reportType)
        {
            case FinanceReportExportTypes.TrialBalance:
                yield return FinanceAuditEvents.TrialBalanceExported;
                break;
            case FinanceReportExportTypes.BalanceSheet:
                yield return FinanceAuditEvents.BalanceSheetExported;
                break;
            case FinanceReportExportTypes.IncomeStatement:
                yield return FinanceAuditEvents.IncomeStatementExported;
                break;
            case FinanceReportExportTypes.DetailedLedger:
                yield return FinanceAuditEvents.DetailedLedgerExported;
                break;
            case FinanceReportExportTypes.CashBankLedger:
                yield return FinanceAuditEvents.CashBankLedgerExported;
                break;
            case FinanceReportExportTypes.ApAging:
                yield return FinanceAuditEvents.ApAgingExported;
                break;
            case FinanceReportExportTypes.ArAging:
                yield return FinanceAuditEvents.ArAgingExported;
                break;
            case FinanceReportExportTypes.CustomerStatement:
                yield return FinanceAuditEvents.CustomerStatementExported;
                break;
            case FinanceReportExportTypes.SupplierStatement:
                yield return FinanceAuditEvents.SupplierStatementExported;
                break;
            case FinanceReportExportTypes.ApControlReconciliation:
                yield return FinanceAuditEvents.ApControlReconciliationExported;
                break;
            case FinanceReportExportTypes.ArControlReconciliation:
                yield return FinanceAuditEvents.ArControlReconciliationExported;
                break;
            case FinanceReportExportTypes.FixedAssetRegister:
            case FinanceReportExportTypes.FixedAssetRollForward:
                yield return FinanceAuditEvents.FixedAssetReportExported;
                break;
            case FinanceReportExportTypes.FixedAssetGlReconciliation:
                yield return FinanceAuditEvents.FixedAssetReportExported;
                yield return FinanceAuditEvents.FixedAssetGlReconciliationExported;
                break;
            case FinanceReportExportTypes.TaxOutput:
                yield return FinanceAuditEvents.OutputTaxReportExported;
                break;
            case FinanceReportExportTypes.TaxInput:
                yield return FinanceAuditEvents.InputTaxReportExported;
                break;
            case FinanceReportExportTypes.TaxNetSummary:
                yield return FinanceAuditEvents.VatReportExported;
                break;
            case FinanceReportExportTypes.VatWithholding:
                yield return FinanceAuditEvents.VatWithholdingReportExported;
                break;
            case FinanceReportExportTypes.WhtPayable:
            case FinanceReportExportTypes.WhtReceivable:
                yield return FinanceAuditEvents.WhtReportExported;
                break;
            case FinanceReportExportTypes.TaxAccountReconciliation:
                yield return FinanceAuditEvents.TaxAccountReconciliationExported;
                break;
            case FinanceReportExportTypes.TaxExemptZeroOutOfScope:
            case FinanceReportExportTypes.TaxConfigurationHistory:
            case FinanceReportExportTypes.TaxCovidDiagnostic:
                yield return FinanceAuditEvents.VatReportExported;
                break;
        }
    }

    private static string NormalizeReportType(string reportType)
    {
        var normalized = (reportType ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        return normalized.ToUpperInvariant() switch
        {
            "TRIALBALANCE" => FinanceReportExportTypes.TrialBalance,
            "BALANCESHEET" => FinanceReportExportTypes.BalanceSheet,
            "INCOMESTATEMENT" or "PROFITANDLOSS" or "PL" => FinanceReportExportTypes.IncomeStatement,
            "DETAILEDLEDGER" => FinanceReportExportTypes.DetailedLedger,
            "CASHBANKLEDGER" or "CASHBOOK" => FinanceReportExportTypes.CashBankLedger,
            "APAGING" or "ACCOUNTPAYABLEAGING" or "ACCOUNTSPAYABLEAGING" => FinanceReportExportTypes.ApAging,
            "ARAGING" or "ACCOUNTRECEIVABLEAGING" or "ACCOUNTSRECEIVABLEAGING" => FinanceReportExportTypes.ArAging,
            "CUSTOMERSTATEMENT" or "ARCUSTOMERSTATEMENT" or "ARSTATEMENT" or "ACCOUNTSRECEIVABLESTATEMENT" => FinanceReportExportTypes.CustomerStatement,
            "SUPPLIERSTATEMENT" or "APSUPPLIERSTATEMENT" or "APSTATEMENT" or "ACCOUNTSPAYABLESTATEMENT" => FinanceReportExportTypes.SupplierStatement,
            "APCONTROLRECONCILIATION" => FinanceReportExportTypes.ApControlReconciliation,
            "ARCONTROLRECONCILIATION" => FinanceReportExportTypes.ArControlReconciliation,
            "FIXEDASSETREGISTER" or "ASSETREGISTER" => FinanceReportExportTypes.FixedAssetRegister,
            "FIXEDASSETROLLFORWARD" or "ASSETROLLFORWARD" => FinanceReportExportTypes.FixedAssetRollForward,
            "FIXEDASSETGLRECONCILIATION" or "ASSETGLRECONCILIATION" => FinanceReportExportTypes.FixedAssetGlReconciliation,
            "TAXOUTPUT" or "OUTPUTTAX" or "VATOUTPUT" or "OUTPUTTAXREPORT" => FinanceReportExportTypes.TaxOutput,
            "TAXINPUT" or "INPUTTAX" or "VATINPUT" or "INPUTTAXREPORT" => FinanceReportExportTypes.TaxInput,
            "TAXNETSUMMARY" or "NETVATSUMMARY" or "VATNETSUMMARY" => FinanceReportExportTypes.TaxNetSummary,
            "TAXEXEMPTZEROOUTOFSCOPE" or "NONTAXABLESUPPLIES" or "EXEMPTZERORATEDOUTOFSCOPE" => FinanceReportExportTypes.TaxExemptZeroOutOfScope,
            "VATWITHHOLDING" or "VATWITHHOLDINGREPORT" => FinanceReportExportTypes.VatWithholding,
            "WHTPAYABLE" or "WITHHOLDINGTAXPAYABLE" => FinanceReportExportTypes.WhtPayable,
            "WHTRECEIVABLE" or "WITHHOLDINGTAXRECEIVABLE" => FinanceReportExportTypes.WhtReceivable,
            "TAXACCOUNTRECONCILIATION" or "TAXRECONCILIATION" => FinanceReportExportTypes.TaxAccountReconciliation,
            "TAXCONFIGURATIONHISTORY" or "TAXRATEHISTORY" => FinanceReportExportTypes.TaxConfigurationHistory,
            "TAXCOVIDDIAGNOSTIC" or "COVIDLEVYDIAGNOSTIC" => FinanceReportExportTypes.TaxCovidDiagnostic,
            _ => normalized
        };
    }

    private static bool IsTaxReport(string reportType)
        => reportType is FinanceReportExportTypes.TaxOutput
            or FinanceReportExportTypes.TaxInput
            or FinanceReportExportTypes.TaxNetSummary
            or FinanceReportExportTypes.TaxExemptZeroOutOfScope
            or FinanceReportExportTypes.VatWithholding
            or FinanceReportExportTypes.WhtPayable
            or FinanceReportExportTypes.WhtReceivable
            or FinanceReportExportTypes.TaxAccountReconciliation
            or FinanceReportExportTypes.TaxConfigurationHistory
            or FinanceReportExportTypes.TaxCovidDiagnostic;

    private static string NormalizeFormat(string? format)
    {
        return string.IsNullOrWhiteSpace(format)
            ? FinanceReportExportFormats.Csv
            : format.Trim().Equals("Csv", StringComparison.OrdinalIgnoreCase)
                ? FinanceReportExportFormats.Csv
                : format.Trim();
    }

    private static IReadOnlyCollection<Guid> ResolveReportIds(IReadOnlyCollection<Guid> ids, Guid? singleId)
    {
        var resolved = ids
            .Where(id => id != Guid.Empty)
            .ToList();

        if (singleId.HasValue && singleId.Value != Guid.Empty && !resolved.Contains(singleId.Value))
        {
            resolved.Add(singleId.Value);
        }

        return resolved;
    }

    private static string BuildFileName(string reportType, bool isPrint)
    {
        var suffix = isPrint ? "print" : "export";
        return $"{reportType}-{DateTime.UtcNow:yyyyMMddHHmmss}-{suffix}.csv";
    }

    private static string ToCsv(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"', StringComparison.Ordinal))
        {
            value = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        }

        return value.Contains(',', StringComparison.Ordinal) ||
               value.Contains('\r', StringComparison.Ordinal) ||
               value.Contains('\n', StringComparison.Ordinal) ||
               value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
    }

    private static string Money(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", InvariantCulture) : string.Empty;

    private static string Money(decimal value)
        => value.ToString("0.00", InvariantCulture);

    private static string Date(DateTime? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd", InvariantCulture) : string.Empty;

    private static string Date(DateTime value)
        => value.ToString("yyyy-MM-dd", InvariantCulture);
}
