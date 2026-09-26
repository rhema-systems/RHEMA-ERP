using System.Globalization;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Shared presentation boundary for parameterized Finance reports. Concrete builders must load
/// an existing canonical report DTO and only map it to printable sections; they must not rebuild
/// balances from transaction tables independently of the on-screen/CSV report.
/// </summary>
public abstract class FinanceTabularReportDocumentBuilderBase : IDocumentBuilder
{
    protected static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    protected FinanceTabularReportDocumentBuilderBase(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public abstract string DocumentType { get; }
    public bool RequiresEntityId => false;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException($"{DocumentType} supports PDF output only.");

        var tenantId = _currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var tenant = await _context.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The current tenant was not found.");
        var generatedAtUtc = DateTime.UtcNow;
        try
        {
            var report = await BuildReportAsync(request, cancellationToken);
            var content = BuildPdf(tenant, report, generatedAtUtc, _currentUser.UserName);
            await RecordAuditAsync(
                FinanceAuditEvents.ReportExported,
                tenantId,
                request,
                report.PeriodLabel,
                content.Length,
                null,
                generatedAtUtc,
                cancellationToken);

            return new RenderedDocumentDto
            {
                Content = content,
                ContentType = "application/pdf",
                FileName = $"{SanitizeFileName(report.FileStem)}.pdf",
                DocumentType = DocumentType,
                EntityId = Guid.Empty,
                Format = "pdf"
            };
        }
        catch (Exception exception)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.ReportExportFailed,
                tenantId,
                request,
                null,
                null,
                exception.Message,
                generatedAtUtc,
                cancellationToken);
            throw;
        }
    }

    protected abstract Task<FinancePrintableReport> BuildReportAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken);

    protected static DateTime RequiredDateOption(DocumentRenderRequestDto request, string key)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue)
            || !DateTime.TryParse(rawValue, InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            throw new ArgumentException($"A valid {key} report date is required.");
        }

        return parsed.Date;
    }

    protected static DateTime OptionalDateOption(
        DocumentRenderRequestDto request,
        string key,
        DateTime fallback)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback.Date;
        }

        if (!DateTime.TryParse(rawValue, InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            throw new ArgumentException($"'{rawValue}' is not a valid {key} report date.");

        return parsed.Date;
    }

    protected static bool BoolOption(DocumentRenderRequestDto request, string key, bool fallback = false)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (!bool.TryParse(rawValue, out var parsed))
            throw new ArgumentException($"'{rawValue}' is not a valid {key} value.");

        return parsed;
    }

    protected static IReadOnlyCollection<Guid> GuidListOption(DocumentRenderRequestDto request, string key)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<Guid>();
        }

        var result = new HashSet<Guid>();
        foreach (var token in rawValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(token, out var id) || id == Guid.Empty)
                throw new ArgumentException($"'{token}' is not a valid {key} identifier.");
            result.Add(id);
        }

        return result.ToArray();
    }

    protected static Guid? GuidOption(DocumentRenderRequestDto request, string key)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (!Guid.TryParse(rawValue, out var id) || id == Guid.Empty)
            throw new ArgumentException($"'{rawValue}' is not a valid {key} identifier.");
        return id;
    }

    protected static TEnum? EnumOption<TEnum>(DocumentRenderRequestDto request, string key)
        where TEnum : struct, Enum
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(rawValue, true, out var value) || !Enum.IsDefined(value))
            throw new ArgumentException($"'{rawValue}' is not a valid {key} value.");
        return value;
    }

    protected static string Money(decimal value) => value.ToString("#,##0.00;(#,##0.00);-", InvariantCulture);
    protected static string DateText(DateTime value) => value.ToString("dd MMM yyyy", InvariantCulture);

    private static byte[] BuildPdf(
        Tenant tenant,
        FinancePrintableReport report,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken4));
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(tenant.Name).FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                            left.Item().Text(report.Title).FontSize(17).Bold();
                            if (!string.IsNullOrWhiteSpace(report.Subtitle))
                                left.Item().Text(report.Subtitle).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(210).AlignRight().Column(right =>
                        {
                            right.Item().Text(report.PeriodLabel).Bold();
                            right.Item().Text($"Generated {generatedAtUtc:dd MMM yyyy HH:mm} UTC");
                            right.Item().Text($"By {TextOrDash(generatedBy)}");
                        });
                    });
                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Blue.Medium);
                });

                page.Content().PaddingTop(10).Column(column =>
                {
                    ComposeSummary(column, report.Summary);
                    foreach (var notice in report.Notices)
                    {
                        column.Item().PaddingBottom(4).Background(Colors.Orange.Lighten5)
                            .Border(0.5f).BorderColor(Colors.Orange.Lighten2).Padding(5).Text(notice);
                    }

                    for (var index = 0; index < report.Sections.Count; index++)
                    {
                        var section = report.Sections[index];
                        if (index > 0 && section.PageBreakBefore)
                            column.Item().PageBreak();
                        ComposeSection(column, section);
                    }
                });

                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("Finance report - generated from the canonical application read model")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                    row.ConstantItem(110).AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeSummary(ColumnDescriptor column, IReadOnlyList<FinancePrintableMetric> metrics)
    {
        foreach (var group in metrics.Chunk(4))
        {
            column.Item().PaddingBottom(7).Row(row =>
            {
                foreach (var metric in group)
                {
                    row.RelativeItem().PaddingRight(6).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Background(Colors.Grey.Lighten4).Padding(6).Column(box =>
                        {
                            box.Item().Text(metric.Label).FontSize(7).FontColor(Colors.Grey.Darken1);
                            box.Item().Text(metric.Value).FontSize(11).Bold();
                        });
                }
            });
        }
    }

    private static void ComposeSection(ColumnDescriptor column, FinancePrintableSection section)
    {
        column.Item().PaddingTop(4).PaddingBottom(4).Column(title =>
        {
            title.Item().Text(section.Title).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
            if (!string.IsNullOrWhiteSpace(section.Subtitle))
                title.Item().Text(section.Subtitle).FontSize(7).FontColor(Colors.Grey.Darken1);
        });

        if (section.Metrics.Count > 0)
            ComposeSummary(column, section.Metrics);

        if (section.Rows.Count == 0)
        {
            column.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10)
                .AlignCenter().Text(section.EmptyMessage ?? "No report rows match the selected parameters.")
                .FontColor(Colors.Grey.Medium);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                var widths = section.RelativeWidths.Count == section.Headers.Count
                    ? section.RelativeWidths
                    : Enumerable.Repeat(1f, section.Headers.Count).ToArray();
                foreach (var width in widths)
                    columns.RelativeColumn(width);
            });
            table.Header(header =>
            {
                foreach (var heading in section.Headers)
                    header.Cell().Background(Colors.Blue.Darken2).Padding(4).Text(heading).FontColor(Colors.White).Bold();
            });

            for (var rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++)
            {
                var background = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                foreach (var value in section.Rows[rowIndex])
                    table.Cell().Background(background).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(TextOrDash(value));
            }
        });
    }

    private static string TextOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string SanitizeFileName(string value)
    {
        var safe = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "finance-report" : safe;
    }

    private async Task RecordAuditAsync(
        string eventType,
        Guid tenantId,
        DocumentRenderRequestDto request,
        string? periodLabel,
        int? contentLength,
        string? error,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = tenantId,
            SourceModule = "Finance",
            SourceDocumentType = DocumentType,
            Resource = DocumentType,
            ResourceId = DocumentType,
            AfterValues = new
            {
                Format = "PDF",
                request.Options,
                Period = periodLabel,
                ContentLength = contentLength,
                Error = error,
                GeneratedAtUtc = generatedAtUtc
            },
            Comment = error == null
                ? "Controlled Finance report PDF generated from the canonical report read model."
                : "Controlled Finance report PDF generation failed."
        }, cancellationToken);
    }
}

public sealed class ArAgingReportDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IArReportsService _reports;

    public ArAgingReportDocumentBuilder(IArReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceArAgingReport;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var asOfDate = OptionalDateOption(request, "asOfDate", DateTime.UtcNow.Date);
        var report = await _reports.GetAgingReportAsync(asOfDate, cancellationToken: cancellationToken);
        return new FinancePrintableReport(
            "AR Aging Analysis",
            "Outstanding customer balances grouped by aging bucket",
            $"As at {DateText(report.AsOfDate)} - {report.CurrencyCode}",
            $"ar-aging-{report.AsOfDate:yyyy-MM-dd}",
            [
                new("Customers", report.Summary.TotalCustomers.ToString(InvariantCulture)),
                new("Overdue customers", report.Summary.OverdueCustomers.ToString(InvariantCulture)),
                new("Total outstanding", $"{report.CurrencyCode} {Money(report.Summary.GrandTotal)}"),
                new("Source", report.UsesSettlementReadModel ? "Settlement read model" : "AR report service")
            ],
            report.Diagnostics.Count == 0 ? [] : [$"{report.Diagnostics.Count} settlement diagnostic(s) accompany this report."],
            [new FinancePrintableSection(
                "Customer aging",
                null,
                ["Customer", "Current", "1-30", "31-60", "61-90", "90+", "Outstanding"],
                report.Customers.Select(customer => (IReadOnlyList<string>)[
                    $"{customer.CustomerCode} - {customer.CustomerName}",
                    Money(customer.Current),
                    Money(customer.Days1To30),
                    Money(customer.Days31To60),
                    Money(customer.Days61To90),
                    Money(customer.Days90Plus),
                    Money(customer.TotalOutstanding)
                ]).ToArray(),
                [2.4f, 1f, 1f, 1f, 1f, 1f, 1.2f])]);
    }
}

public sealed class ArCustomerStatementDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IArReportsService _reports;

    public ArCustomerStatementDocumentBuilder(IArReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceArCustomerStatement;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var fromDate = RequiredDateOption(request, "fromDate");
        var toDate = RequiredDateOption(request, "toDate");
        if (toDate < fromDate)
            throw new ArgumentException("The statement end date must be on or after the start date.");

        var businessPartnerIds = GuidListOption(request, "businessPartnerIds");
        var showCustomerCurrency = BoolOption(request, "showCustomerCurrency");
        var report = await _reports.GetCustomerDetailedLedgerAsync(fromDate, toDate, businessPartnerIds, showCustomerCurrency, cancellationToken);
        var sections = report.Customers.Select((customer, index) => new FinancePrintableSection(
            $"{customer.CustomerName} ({customer.CustomerCode})",
            $"Statement currency: {customer.CurrencyCode}",
            ["Date", "Type", "Document", "Reference", "Description", "Curr.", "Debit", "Credit", "Balance"],
            customer.Lines.Select(line => (IReadOnlyList<string>)[
                DateText(line.TransactionDate),
                line.TransactionType,
                line.DocumentNumber,
                line.Reference ?? "-",
                line.Description,
                line.TransactionCurrencyCode,
                Money(line.Debit),
                Money(line.Credit),
                Money(line.RunningBalance)
            ]).ToArray(),
            [0.9f, 1.1f, 1.15f, 1.1f, 2.2f, 0.65f, 0.9f, 0.9f, 1f],
            MetricsValue:
            [
                new("Opening", $"{customer.CurrencyCode} {Money(customer.OpeningBalance)}"),
                new("Debits", $"{customer.CurrencyCode} {Money(customer.TotalDebits)}"),
                new("Credits", $"{customer.CurrencyCode} {Money(customer.TotalCredits)}"),
                new("Closing", $"{customer.CurrencyCode} {Money(customer.ClosingBalance)}")
            ],
            PageBreakBefore: index > 0)).ToArray();
        var fileStem = report.Customers.Count == 1
            ? $"customer-statement-{report.Customers[0].CustomerCode}-{fromDate:yyyy-MM-dd}-{toDate:yyyy-MM-dd}"
            : $"customer-statements-{fromDate:yyyy-MM-dd}-{toDate:yyyy-MM-dd}";

        return new FinancePrintableReport(
            report.Customers.Count == 1 ? "Customer Statement" : "Customer Statements",
            "Opening balance, period movements, and closing balance from the AR detailed ledger",
            $"{DateText(fromDate)} to {DateText(toDate)}",
            fileStem,
            [],
            report.Warnings,
            sections);
    }
}

public sealed class ApAgingReportDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IApReportsService _reports;

    public ApAgingReportDocumentBuilder(IApReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceApAgingReport;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var asOfDate = OptionalDateOption(request, "asOfDate", DateTime.UtcNow.Date);
        var report = await _reports.GetAgingReportAsync(asOfDate, cancellationToken: cancellationToken);
        return new FinancePrintableReport(
            "AP Aging Analysis",
            "Outstanding supplier balances grouped by aging bucket",
            $"As at {DateText(report.AsOfDate)} - {report.CurrencyCode}",
            $"ap-aging-{report.AsOfDate:yyyy-MM-dd}",
            [
                new("Suppliers", report.TotalSuppliers.ToString(InvariantCulture)),
                new("Invoices", report.TotalInvoices.ToString(InvariantCulture)),
                new("Total outstanding", $"{report.CurrencyCode} {Money(report.TotalOutstanding)}"),
                new("Source", report.UsesSettlementReadModel ? "Settlement read model" : "AP report service")
            ],
            report.Diagnostics.Count == 0 ? [] : [$"{report.Diagnostics.Count} settlement diagnostic(s) accompany this report."],
            [new FinancePrintableSection(
                "Supplier aging",
                null,
                ["Supplier", "Current", "31-60", "61-90", "90+", "Outstanding", "Invoices"],
                report.SupplierDetails.Select(supplier => (IReadOnlyList<string>)[
                    $"{supplier.SupplierCode} - {supplier.SupplierName}",
                    Money(supplier.Current),
                    Money(supplier.ThirtyDays),
                    Money(supplier.SixtyDays),
                    Money(supplier.NinetyPlusDays),
                    Money(supplier.TotalOutstanding),
                    supplier.InvoiceCount.ToString(InvariantCulture)
                ]).ToArray(),
                [2.5f, 1f, 1f, 1f, 1f, 1.2f, 0.7f])]);
    }
}

public sealed class ApCashRequirementsDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IApReportsService _reports;

    public ApCashRequirementsDocumentBuilder(IApReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceApCashRequirements;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var asOfDate = OptionalDateOption(request, "asOfDate", DateTime.UtcNow.Date);
        var report = await _reports.GetCashRequirementForecastAsync(asOfDate, cancellationToken);
        return new FinancePrintableReport(
            "AP Cash Requirements Forecast",
            "Expected supplier cash requirements and available settlement discounts",
            $"As at {DateText(report.AsOfDate)} - {report.CurrencyCode}",
            $"ap-cash-requirements-{report.AsOfDate:yyyy-MM-dd}",
            [
                new("Total payable", $"{report.CurrencyCode} {Money(report.TotalPayable)}"),
                new("Overdue", $"{report.CurrencyCode} {Money(report.OverdueAmount)}"),
                new("Forecast periods", report.Periods.Count.ToString(InvariantCulture))
            ],
            [],
            [new FinancePrintableSection(
                "Forecast by due period",
                null,
                ["Period", "From", "To", "Invoices", "Amount due", "Discount available"],
                report.Periods.Select(period => (IReadOnlyList<string>)[
                    period.Period,
                    DateText(period.PeriodStart),
                    DateText(period.PeriodEnd),
                    period.InvoiceCount.ToString(InvariantCulture),
                    Money(period.AmountDue),
                    Money(period.DiscountAvailable)
                ]).ToArray(),
                [1.5f, 1f, 1f, 0.7f, 1.1f, 1.1f])]);
    }
}

public sealed class ApMatchExceptionReportDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IApReportsService _reports;

    public ApMatchExceptionReportDocumentBuilder(IApReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceApMatchExceptionReport;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var fromDate = RequiredDateOption(request, "fromDate");
        var toDate = RequiredDateOption(request, "toDate");
        if (toDate < fromDate)
            throw new ArgumentException("The report end date must be on or after the start date.");
        var status = EnumOption<VendorInvoiceMatchExceptionStatus>(request, "status");
        var supplierId = GuidOption(request, "supplierId");
        var report = await _reports.GetThreeWayMatchExceptionsAsync(fromDate, toDate, status, supplierId, cancellationToken);

        return new FinancePrintableReport(
            "AP Three-way-match Exception Register",
            "Approval, expiry, evidence lineage, and corrective-action follow-up",
            $"{DateText(fromDate)} to {DateText(toDate)}",
            $"ap-match-exceptions-{fromDate:yyyy-MM-dd}-{toDate:yyyy-MM-dd}",
            [
                new("Exceptions", report.TotalCount.ToString(InvariantCulture)),
                new("Approved", report.ApprovedCount.ToString(InvariantCulture)),
                new("Expired", report.ExpiredCount.ToString(InvariantCulture)),
                new("Open corrective actions", report.OpenCorrectiveActionCount.ToString(InvariantCulture))
            ],
            [],
            [new FinancePrintableSection(
                "Exception register",
                status.HasValue ? $"Status filter: {status}" : "All statuses",
                ["Invoice / supplier", "PO", "Status", "Variance", "Root cause", "Corrective action", "Due", "Evidence"],
                report.Rows.Select(row => (IReadOnlyList<string>)[
                    $"{row.InvoiceNumber} - {row.SupplierName}",
                    row.PurchaseOrderNumber,
                    row.Status.ToString(),
                    $"{row.VarianceType} ({row.MaximumVariancePercentage:0.##}% max)",
                    $"{row.RootCauseCategory}: {row.RootCauseDescription}",
                    $"{row.CorrectiveActionOwnerName} - {row.CorrectiveActionStatus}",
                    DateText(row.CorrectiveActionDueAtUtc),
                    row.EvidenceCount.ToString(InvariantCulture)
                ]).ToArray(),
                [1.7f, 1f, 0.8f, 1.2f, 2f, 1.6f, 0.9f, 0.55f])]);
    }
}

public sealed class ApProcurementReconciliationDocumentBuilder : FinanceTabularReportDocumentBuilderBase
{
    private readonly IApReportsService _reports;

    public ApProcurementReconciliationDocumentBuilder(IApReportsService reports, ApplicationDbContext context, ICurrentUserService currentUser, IFinanceAuditService? financeAuditService = null)
        : base(context, currentUser, financeAuditService) => _reports = reports;

    public override string DocumentType => DocumentTypes.FinanceApProcurementReconciliation;

    protected override async Task<FinancePrintableReport> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var asOfDate = OptionalDateOption(request, "asOfDate", DateTime.UtcNow.Date);
        var purchaseOrderId = GuidOption(request, "purchaseOrderId");
        var report = await _reports.GetProcurementFinanceReconciliationAsync(asOfDate, purchaseOrderId, cancellationToken);
        var notices = new List<string>
        {
            "Amounts remain in their recorded currencies and are never added across currencies."
        };
        if (report.DecisionKeys.Count > 0)
            notices.Add($"Decision lineage: {string.Join(", ", report.DecisionKeys)}.");

        return new FinancePrintableReport(
            "Procurement and Finance Reconciliation",
            "Commitments, receipts, AP settlements, central postings, reversals, retention, and milestones",
            $"As at {DateText(report.AsOfDate)} - {report.RuleCode} / {report.TaskCode}",
            $"procurement-finance-reconciliation-{report.AsOfDate:yyyy-MM-dd}",
            [
                new("Purchase orders", report.PurchaseOrderCount.ToString(InvariantCulture)),
                new("Issues", report.IssueCount.ToString(InvariantCulture)),
                new("Unbalanced postings", report.UnbalancedPostingCount.ToString(InvariantCulture)),
                new("Status", report.IsReconciled ? "Reconciled" : "Exceptions found")
            ],
            notices,
            [new FinancePrintableSection(
                "Purchase-order reconciliation register",
                null,
                ["PO / status", "Curr.", "Order", "Commitment", "Receipts", "Invoices", "Settled", "Invoice GL", "Payment GL", "Result / issues"],
                report.Rows.Select(row => (IReadOnlyList<string>)[
                    $"{row.PurchaseOrderNumber} - {row.PurchaseOrderStatus}",
                    row.CurrencyCode,
                    Money(row.PurchaseOrderAmount),
                    Money(row.CommitmentAmount),
                    Money(row.AcceptedReceiptAmount),
                    Money(row.InvoiceAmount),
                    Money(row.SettledAmount),
                    Money(row.InvoicePostedAmount),
                    Money(row.PaymentPostedAmount),
                    row.IsReconciled ? "Reconciled" : string.Join("; ", row.Issues.Select(issue => $"{issue.Code}: {issue.Message}"))
                ]).ToArray(),
                [1.5f, 0.55f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 2.2f])]);
    }
}

public sealed record FinancePrintableReport(
    string Title,
    string Subtitle,
    string PeriodLabel,
    string FileStem,
    IReadOnlyList<FinancePrintableMetric> Summary,
    IReadOnlyList<string> Notices,
    IReadOnlyList<FinancePrintableSection> Sections);

public sealed record FinancePrintableMetric(string Label, string Value);

public sealed record FinancePrintableSection(
    string Title,
    string? Subtitle,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<float> RelativeWidths,
    IReadOnlyList<FinancePrintableMetric>? MetricsValue = null,
    bool PageBreakBefore = false,
    string? EmptyMessage = null)
{
    public IReadOnlyList<FinancePrintableMetric> Metrics => MetricsValue ?? [];
}
