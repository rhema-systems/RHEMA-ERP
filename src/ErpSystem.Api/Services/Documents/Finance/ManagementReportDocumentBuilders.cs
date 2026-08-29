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

    protected static string? TextOption(DocumentRenderRequestDto request, string key)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    protected static string CurrencyCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();

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

public sealed class WhtCertificateRegisterDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly IWithholdingTaxCertificateService _certificates;

    public WhtCertificateRegisterDocumentBuilder(
        IWithholdingTaxCertificateService certificates,
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ITenantSettingsService tenantSettings,
        IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _certificates = certificates;

    public override string DocumentType => DocumentTypes.FinanceTaxWhtCertificateRegister;

    protected override async Task<FinancePrintableReport> BuildReportAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken)
    {
        var fromDate = RequiredDateOption(request, "fromDate");
        var toDate = RequiredDateOption(request, "toDate");
        if (toDate < fromDate)
            throw new ArgumentException("The WHT certificate register end date must be on or after the start date.");

        var status = TextOption(request, "status");
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) status = null;
        var searchTerm = TextOption(request, "searchTerm");
        var rows = await LoadAllCertificatesAsync(fromDate, toDate, status, searchTerm, cancellationToken);
        var currencies = rows.Select(row => CurrencyCode(row.CurrencyCode)).Distinct().Order().ToArray();
        var sections = rows
            .GroupBy(row => CurrencyCode(row.CurrencyCode))
            .OrderBy(group => group.Key)
            .Select((group, index) => new FinancePrintableSection(
                $"Certificate register - {group.Key}",
                $"Amounts remain in {group.Key}; unlike currencies are never combined.",
                ["Payment", "Supplier / TIN", "Tax", "Taxable base", "WHT", "Certificate", "Status", "Remittance"],
                group.OrderBy(row => row.PaymentDate).ThenBy(row => row.PaymentNumber)
                    .Select(row => (IReadOnlyList<string>)[
                        $"{DateText(row.PaymentDate)}\n{row.PaymentNumber}",
                        $"{row.SupplierName}\n{row.SupplierTin ?? "TIN not supplied"}",
                        $"{row.TaxCode ?? "WHT"} {row.TaxRate:0.####}%",
                        Money(row.TaxableBase),
                        Money(row.WithholdingAmount),
                        $"{row.CertificateNumber ?? "-"}\nv{row.VersionNumber}",
                        row.CertificateStatus,
                        $"{row.RemittanceNumber ?? "-"}\n{row.RemittanceStatus}"
                    ]).ToArray(),
                [1.1f, 1.8f, 0.9f, 1f, 1f, 1.2f, 0.8f, 1.2f],
                PageBreakBefore: index > 0))
            .ToList();
        if (sections.Count == 0)
        {
            sections.Add(new FinancePrintableSection(
                "Certificate register", null,
                ["Payment", "Supplier", "Tax", "Taxable base", "WHT", "Certificate", "Status", "Remittance"],
                [], [1.1f, 1.8f, 0.9f, 1f, 1f, 1.2f, 0.8f, 1.2f]));
        }

        return new FinancePrintableReport(
            "WHT Statutory Certificate Register",
            "Eligible posted AP withholding payments and controlled certificate/remittance evidence",
            $"{DateText(fromDate)} to {DateText(toDate)}",
            $"wht-statutory-certificate-register-{fromDate:yyyy-MM-dd}-{toDate:yyyy-MM-dd}",
            [
                new("Eligible payments", rows.Count.ToString(InvariantCulture)),
                new("Issued", rows.Count(row => row.CertificateStatus == "Issued").ToString(InvariantCulture)),
                new("Missing", rows.Count(row => row.CertificateStatus == "Missing").ToString(InvariantCulture)),
                new("Currencies", currencies.Length.ToString(InvariantCulture))
            ],
            ["Monetary values are grouped by payment currency; no cross-currency statutory total is presented."],
            sections);
    }

    private async Task<IReadOnlyList<WhtCertificateDto>> LoadAllCertificatesAsync(
        DateTime fromDate,
        DateTime toDate,
        string? status,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var rows = new List<WhtCertificateDto>();
        for (var page = 1; ; page++)
        {
            var result = await _certificates.GetApCertificatesAsync(new WhtCertificateQueryDto
            {
                Page = page,
                PageSize = pageSize,
                FromDate = fromDate,
                ToDate = toDate,
                Status = status,
                SearchTerm = searchTerm
            }, cancellationToken);
            var pageRows = result.Items.ToList();
            rows.AddRange(pageRows);
            if (pageRows.Count == 0 || rows.Count >= result.TotalCount) break;
        }
        return rows;
    }
}

public sealed class WhtRemittanceRegisterDocumentBuilder : TaxReportDocumentBuilderBase
{
    private readonly IWithholdingTaxCertificateService _certificates;

    public WhtRemittanceRegisterDocumentBuilder(
        IWithholdingTaxCertificateService certificates,
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ITenantSettingsService tenantSettings,
        IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, tenantSettings, financeAuditService) => _certificates = certificates;

    public override string DocumentType => DocumentTypes.FinanceTaxWhtRemittanceRegister;

    protected override async Task<FinancePrintableReport> BuildReportAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken)
    {
        var fromDate = RequiredDateOption(request, "fromDate");
        var toDate = RequiredDateOption(request, "toDate");
        if (toDate < fromDate)
            throw new ArgumentException("The WHT remittance register end date must be on or after the start date.");

        var rows = await LoadAllRemittancesAsync(fromDate, toDate, cancellationToken);
        var currencies = rows.Select(row => CurrencyCode(row.CurrencyCode)).Distinct().Order().ToArray();
        var sections = new List<FinancePrintableSection>();
        foreach (var group in rows.GroupBy(row => CurrencyCode(row.CurrencyCode)).OrderBy(group => group.Key))
        {
            sections.Add(new FinancePrintableSection(
                $"Remittance batches - {group.Key}",
                $"Amounts remain in {group.Key}; unlike currencies are never combined.",
                ["Remittance", "Period / due", "Status", "Items", "WHT", "Submission", "Payment / GRA receipt"],
                group.OrderBy(row => row.PeriodFrom).ThenBy(row => row.RemittanceNumber)
                    .Select(row => (IReadOnlyList<string>)[
                        row.RemittanceNumber,
                        $"{DateText(row.PeriodFrom)} to {DateText(row.PeriodTo)}\nDue {DateText(row.DueDate)}",
                        row.Status,
                        row.LineCount.ToString(InvariantCulture),
                        Money(row.TotalWithholdingAmount),
                        $"{row.SubmissionReference ?? "-"}\n{(row.SubmittedAtUtc.HasValue ? DateText(row.SubmittedAtUtc.Value) : "-")}",
                        $"{row.PaymentReference ?? "-"}\n{row.AuthorityReceiptReference ?? "-"}"
                    ]).ToArray(),
                [1.2f, 1.6f, 0.8f, 0.5f, 0.9f, 1.4f, 1.4f],
                PageBreakBefore: sections.Count > 0));

            var liabilityRows = group
                .SelectMany(remittance => remittance.Lines.Select(line => new { Remittance = remittance, Line = line }))
                .OrderBy(item => item.Line.PaymentDate)
                .ThenBy(item => item.Line.PaymentNumber)
                .Select(item => (IReadOnlyList<string>)[
                    item.Remittance.RemittanceNumber,
                    $"{DateText(item.Line.PaymentDate)}\n{item.Line.PaymentNumber}",
                    $"{item.Line.SupplierName}\n{item.Line.SupplierTin ?? "TIN not supplied"}",
                    item.Line.TaxCode ?? "WHT",
                    Money(item.Line.TaxableBase),
                    Money(item.Line.WithholdingAmount)
                ]).ToArray();
            sections.Add(new FinancePrintableSection(
                $"Underlying liabilities - {group.Key}",
                "Immutable payment evidence included in the remittance batches above.",
                ["Remittance", "Payment", "Supplier / TIN", "Tax", "Taxable base", "WHT"],
                liabilityRows,
                [1.2f, 1.2f, 2f, 0.8f, 1f, 1f],
                PageBreakBefore: true));
        }
        if (sections.Count == 0)
        {
            sections.Add(new FinancePrintableSection(
                "Remittance batches", null,
                ["Remittance", "Period / due", "Status", "Items", "WHT", "Submission", "Payment / GRA receipt"],
                [], [1.2f, 1.6f, 0.8f, 0.5f, 0.9f, 1.4f, 1.4f]));
        }

        return new FinancePrintableReport(
            "WHT Remittance Register",
            "Submission, settlement, cancellation, and underlying AP liability evidence",
            $"{DateText(fromDate)} to {DateText(toDate)}",
            $"wht-remittance-register-{fromDate:yyyy-MM-dd}-{toDate:yyyy-MM-dd}",
            [
                new("Batches", rows.Count.ToString(InvariantCulture)),
                new("Draft", rows.Count(row => row.Status == "Draft").ToString(InvariantCulture)),
                new("Submitted", rows.Count(row => row.Status == "Submitted").ToString(InvariantCulture)),
                new("Paid", rows.Count(row => row.Status == "Paid").ToString(InvariantCulture)),
                new("Cancelled", rows.Count(row => row.Status == "Cancelled").ToString(InvariantCulture)),
                new("Currencies", currencies.Length.ToString(InvariantCulture))
            ],
            ["Monetary values are grouped by remittance currency; no cross-currency statutory total is presented."],
            sections);
    }

    private async Task<IReadOnlyList<WhtRemittanceDto>> LoadAllRemittancesAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var rows = new List<WhtRemittanceDto>();
        for (var page = 1; ; page++)
        {
            var result = await _certificates.GetRemittancesAsync(new WhtRemittanceQueryDto
            {
                Page = page,
                PageSize = pageSize,
                FromDate = fromDate,
                ToDate = toDate
            }, cancellationToken);
            var pageRows = result.Items.ToList();
            rows.AddRange(pageRows);
            if (pageRows.Count == 0 || rows.Count >= result.TotalCount) break;
        }
        return rows;
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
