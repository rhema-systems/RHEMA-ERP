using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Documents.Finance;

public sealed class CashPositionReportDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly ICashPositionReportService _reports;

    public CashPositionReportDocumentBuilder(
        ICashPositionReportService reports,
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceCashPositionReport;

    protected override async Task<FinancePrintableReport> BuildReportAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken)
    {
        var report = await _reports.GetCurrentPositionAsync(cancellationToken);
        return new FinancePrintableReport(
            "Cash Position",
            "Current posted cash and bank balances",
            $"As at {DateText(report.AsOfDate)} - functional currency {report.Currency}",
            $"cash-position-{report.AsOfDate:yyyy-MM-dd}",
            [
                new("Total liquidity", $"{report.Currency} {Money(report.TotalBalance)}"),
                new("Active accounts", report.AccountCount.ToString(InvariantCulture)),
                new("Native currencies", report.ByCurrency.Count.ToString(InvariantCulture)),
                new("Source", "Posted cash/bank GL")
            ],
            [
                "Every monetary total is a functional-currency GL amount. Native currency codes identify the underlying bank-account group and are not added as native units."
            ],
            [
                new FinancePrintableSection(
                    "Position by native account currency",
                    $"Balances are {report.Currency} base equivalents",
                    ["Native currency", "Accounts", $"Balance ({report.Currency} base eq.)"],
                    report.ByCurrency.Select(item => (IReadOnlyList<string>)[
                        item.Currency,
                        item.Count.ToString(InvariantCulture),
                        Money(item.Balance)
                    ]).ToArray(),
                    [1f, 0.8f, 1.4f]),
                new FinancePrintableSection(
                    "Position by account type",
                    null,
                    ["Account type", "Accounts", $"Balance ({report.Currency})"],
                    report.ByAccountType.Select(item => (IReadOnlyList<string>)[
                        item.Type.ToString(),
                        item.Count.ToString(InvariantCulture),
                        Money(item.Balance)
                    ]).ToArray(),
                    [1.5f, 0.8f, 1.4f])
            ]);
    }
}

public abstract class TaxReportDocumentBuilderBase : FinanceTabularReportDocumentBuilderBase
{
    private readonly ITenantSettingsService _tenantSettings;

    protected TaxReportDocumentBuilderBase(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ITenantSettingsService tenantSettings,
        IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _tenantSettings = tenantSettings;

    protected async Task<string> GetFunctionalCurrencyAsync() =>
        (await _tenantSettings.GetBaseCurrencyAsync()).Trim().ToUpperInvariant();

    protected static TaxReportRequestDto BuildTaxRequest(DocumentRenderRequestDto request)
    {
        var fromDate = RequiredDateOption(request, "fromDate");
        var toDate = RequiredDateOption(request, "toDate");
        if (toDate < fromDate)
            throw new ArgumentException("The tax report end date must be on or after the start date.");
        return new TaxReportRequestDto { FromDate = fromDate, ToDate = toDate };
    }

    protected static IReadOnlyList<string> DiagnosticNotices(IReadOnlyCollection<TaxReportDiagnosticDto> diagnostics) =>
        diagnostics.Count == 0
            ? []
            : [$"{diagnostics.Count} tax diagnostic(s) accompany this report; review them in the canonical Tax report before filing."];

    protected static FinancePrintableSection SnapshotSection(
        string title,
        IReadOnlyCollection<GhanaTaxSnapshotLineDto> lines) =>
        new(
            title,
            null,
            ["Date", "Document", "Counterparty", "Tax", "Base", "Taxable", "Rate", "Tax", "GL account"],
            lines.Select(line => (IReadOnlyList<string>)[
                DateText(line.SourceDocumentDate),
                $"{line.SourceDocumentType} {line.SourceDocumentNumber}".Trim(),
                line.CounterpartyName ?? "-",
                $"{line.TaxCode} - {line.TaxName}",
                Money(line.BaseAmount),
                Money(line.TaxableAmount),
                $"{line.TaxRate:0.####}%",
                Money(line.TaxAmount),
                $"{line.TaxAccountNumber} {line.TaxAccountName}".Trim()
            ]).ToArray(),
            [0.8f, 1.5f, 1.5f, 1.2f, 0.8f, 0.8f, 0.65f, 0.8f, 1.5f]);
}

public sealed class InputTaxRegisterDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly ITaxReportingService _reports;

    public InputTaxRegisterDocumentBuilder(ITaxReportingService reports, ApplicationDbContext context, ICurrentUserService currentUser, ITenantSettingsService tenantSettings, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceTaxInputRegister;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var filter = BuildTaxRequest(request);
        var report = await _reports.GetInputTaxReportAsync(filter, cancellationToken);
        var currency = await GetFunctionalCurrencyAsync();
        return TaxSnapshotReport("Input Tax Register", "Posted recoverable and non-recoverable input tax snapshots", report, currency, "input-tax-register");
    }

    private static FinancePrintableReport TaxSnapshotReport(string title, string subtitle, GhanaTaxSnapshotReportDto report, string currency, string fileStem) =>
        new(
            title,
            subtitle,
            $"{DateText(report.FromDate)} to {DateText(report.ToDate)} - {currency}",
            $"{fileStem}-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}",
            [
                new("Snapshot lines", report.Lines.Count.ToString(InvariantCulture)),
                new("Taxable amount", $"{currency} {Money(report.Totals.TaxableAmount)}"),
                new("Total tax", $"{currency} {Money(report.Totals.TotalTaxAmount)}"),
                new("GL variance", $"{currency} {Money(report.Totals.Variance)}")
            ],
            DiagnosticNotices(report.Diagnostics),
            [SnapshotSection("Posted input-tax snapshots", report.Lines)]);
}

public sealed class OutputTaxRegisterDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly ITaxReportingService _reports;

    public OutputTaxRegisterDocumentBuilder(ITaxReportingService reports, ApplicationDbContext context, ICurrentUserService currentUser, ITenantSettingsService tenantSettings, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceTaxOutputRegister;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var filter = BuildTaxRequest(request);
        var report = await _reports.GetOutputTaxReportAsync(filter, cancellationToken);
        var currency = await GetFunctionalCurrencyAsync();
        return new FinancePrintableReport(
            "Output Tax Register",
            "Posted output-tax snapshots",
            $"{DateText(report.FromDate)} to {DateText(report.ToDate)} - {currency}",
            $"output-tax-register-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}",
            [
                new("Snapshot lines", report.Lines.Count.ToString(InvariantCulture)),
                new("Taxable amount", $"{currency} {Money(report.Totals.TaxableAmount)}"),
                new("Total tax", $"{currency} {Money(report.Totals.TotalTaxAmount)}"),
                new("GL variance", $"{currency} {Money(report.Totals.Variance)}")
            ],
            DiagnosticNotices(report.Diagnostics),
            [SnapshotSection("Posted output-tax snapshots", report.Lines)]);
    }
}

public sealed class VatReconciliationDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly ITaxReportingService _reports;

    public VatReconciliationDocumentBuilder(ITaxReportingService reports, ApplicationDbContext context, ICurrentUserService currentUser, ITenantSettingsService tenantSettings, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceTaxVatReconciliation;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var filter = BuildTaxRequest(request);
        var report = await _reports.GetNetVatSummaryAsync(filter, cancellationToken);
        var currency = await GetFunctionalCurrencyAsync();
        return new FinancePrintableReport(
            "VAT Reconciliation",
            report.SourceOfTruthMode,
            $"{DateText(report.FromDate)} to {DateText(report.ToDate)} - {currency}",
            $"vat-reconciliation-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}",
            [
                new("Taxable base", $"{currency} {Money(report.Totals.TaxableBase)}"),
                new("VAT", $"{currency} {Money(report.Totals.VatAmount)}"),
                new("Total tax", $"{currency} {Money(report.Totals.TotalTaxAmount)}"),
                new("GL variance", $"{currency} {Money(report.Totals.Variance)}")
            ],
            DiagnosticNotices(report.Diagnostics),
            [SnapshotSection("Posted VAT snapshot evidence", report.Lines)]);
    }
}

public sealed class WhtPayableReportDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly ITaxReportingService _reports;

    public WhtPayableReportDocumentBuilder(ITaxReportingService reports, ApplicationDbContext context, ICurrentUserService currentUser, ITenantSettingsService tenantSettings, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceTaxWhtPayable;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var filter = BuildTaxRequest(request);
        var report = await _reports.GetWhtPayableReportAsync(filter, cancellationToken);
        var currency = await GetFunctionalCurrencyAsync();
        return new FinancePrintableReport(
            "Withholding Tax Payable",
            report.SourceOfTruthMode,
            $"{DateText(report.FromDate)} to {DateText(report.ToDate)} - {currency}",
            $"wht-payable-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}",
            [
                new("Withholding lines", report.Lines.Count.ToString(InvariantCulture)),
                new("Taxable base", $"{currency} {Money(report.Totals.TaxableBase)}"),
                new("Withheld", $"{currency} {Money(report.Totals.TotalWithholdingAmount)}"),
                new("GL variance", $"{currency} {Money(report.Totals.Variance)}")
            ],
            DiagnosticNotices(report.Diagnostics),
            [new FinancePrintableSection(
                "Posted withholding records",
                null,
                ["Date", "Document", "Counterparty", "Tax", "Rate", "Taxable base", "Withheld", "Certificate", "GL account"],
                report.Lines.Select(line => (IReadOnlyList<string>)[
                    DateText(line.SourceDocumentDate),
                    $"{line.SourceDocumentType} {line.SourceDocumentNumber}".Trim(),
                    line.CounterpartyName ?? "-",
                    $"{line.TaxCode} {line.TaxName}".Trim(),
                    $"{line.TaxRate:0.####}%",
                    Money(line.TaxableBase),
                    Money(line.WithholdingAmount),
                    $"{line.CertificateStatus} {line.CertificateNumber}".Trim(),
                    $"{line.TaxAccountNumber} {line.TaxAccountName}".Trim()
                ]).ToArray(),
                [0.8f, 1.4f, 1.4f, 1.1f, 0.6f, 0.9f, 0.9f, 1.1f, 1.5f])]);
    }
}

public sealed class ConsolidatedBudgetDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IBudgetService _budgets;

    public ConsolidatedBudgetDocumentBuilder(IBudgetService budgets, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _budgets = budgets;

    public override string DocumentType => DocumentTypes.FinanceBudgetConsolidated;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var scenarioId = GuidOption(request, "scenarioId")
            ?? throw new ArgumentException("A scenarioId is required for the consolidated budget report.");
        var approvedOnly = BoolOption(request, "approvedOnly", true);
        var report = await _budgets.GetConsolidatedViewAsync(scenarioId, approvedOnly);
        var notices = report.ValidationIssues
            .Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")
            .ToArray();

        return new FinancePrintableReport(
            "Consolidated Budget",
            $"{report.ScenarioName} - {report.ScenarioStatus}{(report.IsOfficial ? " - official baseline" : string.Empty)}",
            $"{report.FiscalYearName} - {report.BookClassification} - {report.CurrencyCode}",
            $"consolidated-budget-{report.ScenarioName}-{(approvedOnly ? "approved" : "working")}",
            [
                new("Revenue budget", $"{report.CurrencyCode} {Money(report.TotalRevenueBudget)}"),
                new("Expense budget", $"{report.CurrencyCode} {Money(report.TotalExpenseBudget)}"),
                new("Net budget", $"{report.CurrencyCode} {Money(report.NetBudget)}"),
                new("Net actual", $"{report.CurrencyCode} {Money(report.NetActual)}"),
                new("Returns included", $"{report.IncludedReturnCount}/{report.TotalReturnCount}"),
                new("Ready", report.ReadyForSubmission ? "Yes" : "No")
            ],
            notices,
            [
                new FinancePrintableSection(
                    "Budget versus actual by account and period",
                    approvedOnly ? "Approved returns only" : "Working view",
                    ["Account", "Type", "Period", "Budget", "Actual", "Variance", "Variance %", "Result"],
                    report.Lines.Select(line => (IReadOnlyList<string>)[
                        $"{line.AccountCode} - {line.AccountName}",
                        line.AccountType,
                        line.PeriodCode,
                        Money(line.BudgetAmount),
                        Money(line.ActualAmount),
                        Money(line.VarianceAmount),
                        line.VariancePercent.HasValue ? $"{line.VariancePercent:0.##}%" : "-",
                        line.Favorability
                    ]).ToArray(),
                    [2.2f, 0.8f, 0.8f, 1f, 1f, 1f, 0.8f, 0.9f]),
                new FinancePrintableSection(
                    "Budget returns by department / unit",
                    null,
                    ["Unit", "Status", "Assigned to", "Budget"],
                    report.Units.Select(unit => (IReadOnlyList<string>)[
                        $"{unit.SegmentCode} - {unit.SegmentName}".Trim(' ', '-'),
                        unit.ReturnStatus,
                        unit.AssignedToUserName,
                        Money(unit.BudgetAmount)
                    ]).ToArray(),
                    [2f, 0.8f, 1.5f, 1f],
                    PageBreakBefore: true)
            ]);
    }
}

public sealed class BudgetScenarioComparisonDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IBudgetService _budgets;

    public BudgetScenarioComparisonDocumentBuilder(IBudgetService budgets, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _budgets = budgets;

    public override string DocumentType => DocumentTypes.FinanceBudgetScenarioComparison;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var baseScenarioId = GuidOption(request, "baseScenarioId")
            ?? throw new ArgumentException("A baseScenarioId is required for the budget scenario comparison report.");
        var comparisonScenarioId = GuidOption(request, "comparisonScenarioId")
            ?? throw new ArgumentException("A comparisonScenarioId is required for the budget scenario comparison report.");
        var report = await _budgets.CompareScenariosAsync(baseScenarioId, comparisonScenarioId);

        return new FinancePrintableReport(
            "Budget Scenario Comparison",
            $"{report.BaseScenarioName} versus {report.ComparisonScenarioName}",
            $"Approved returns - {report.CurrencyCode}",
            $"budget-scenario-comparison-{report.BaseScenarioName}-versus-{report.ComparisonScenarioName}",
            [
                new(report.BaseScenarioName, $"{report.CurrencyCode} {Money(report.BaseTotal)}"),
                new(report.ComparisonScenarioName, $"{report.CurrencyCode} {Money(report.ComparisonTotal)}"),
                new("Difference", $"{report.CurrencyCode} {Money(report.DifferenceTotal)}")
            ],
            ["Both scenarios use approved budget returns from the same fiscal year."],
            [
                new FinancePrintableSection(
                    "Comparison by account and period",
                    null,
                    ["Account", "Type", "Period", "Base", "Compared", "Difference", "Difference %"],
                    report.Lines.Select(line => (IReadOnlyList<string>)[
                        $"{line.AccountCode} - {line.AccountName}",
                        line.AccountType,
                        line.PeriodCode,
                        Money(line.BaseAmount),
                        Money(line.ComparisonAmount),
                        Money(line.DifferenceAmount),
                        line.DifferencePercent.HasValue ? $"{line.DifferencePercent:0.##}%" : "-"
                    ]).ToArray(),
                    [2.4f, 0.8f, 0.8f, 1f, 1f, 1f, 0.8f])
            ]);
    }
}
