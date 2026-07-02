using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

public abstract class FinanceReportDocumentBuilderBase : IDocumentBuilder
{
    protected readonly IGeneralLedgerService LedgerService;
    private readonly ICurrentUserService _currentUser;

    protected FinanceReportDocumentBuilderBase(IGeneralLedgerService ledgerService, ICurrentUserService currentUser)
    {
        LedgerService = ledgerService;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public abstract string DocumentType { get; }
    protected abstract string ReportTitle { get; }
    public bool RequiresEntityId => false;

    public bool SupportsFormat(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
        {
            throw new NotSupportedException($"{ReportTitle} currently supports PDF output only.");
        }

        var report = await BuildReportAsync(request, cancellationToken);
        var pdfBytes = BuildPdf(report);

        return new RenderedDocumentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"{SafeFileName(ReportTitle)}-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
            DocumentType = DocumentType,
            EntityId = Guid.Empty,
            Format = "pdf"
        };
    }

    protected abstract Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken);

    protected string? Option(DocumentRenderRequestDto request, string key)
        => request.Options != null && request.Options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    protected DateTime DateOption(DocumentRenderRequestDto request, string key, DateTime fallback)
        => DateTime.TryParse(Option(request, key), out var value) ? value.Date : fallback.Date;

    protected bool BoolOption(DocumentRenderRequestDto request, string key, bool fallback)
        => bool.TryParse(Option(request, key), out var value) ? value : fallback;

    protected static string Money(decimal amount, string currency)
        => $"{currency} {amount:N2}";

    protected static FinanceReportLine Line(string label, decimal amount, int level = 0, bool isTotal = false)
        => new(label, null, amount, level, isTotal);

    protected static FinanceReportLine Line(string label, string? detail, decimal amount, int level = 0, bool isTotal = false)
        => new(label, detail, amount, level, isTotal);

    protected FinanceReportDocumentModel Model(string companyName, string title, string subtitle, string currency, IReadOnlyList<FinanceReportSection> sections, IReadOnlyList<FinanceReportSummary> summaries)
        => new(
            string.IsNullOrWhiteSpace(companyName) ? "ERP System" : companyName,
            title,
            subtitle,
            currency,
            sections,
            summaries,
            _currentUser.UserName);

    private static byte[] BuildPdf(FinanceReportDocumentModel report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header => ComposeHeader(header, report));
                page.Content().Element(content => ComposeContent(content, report));
                page.Footer().Element(footer => ComposeFooter(footer, report.GeneratedBy));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, FinanceReportDocumentModel report)
    {
        container.Column(column =>
        {
            column.Item().Text(report.CompanyName).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
            column.Item().Text(report.Title).Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
            column.Item().Text(report.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(8).LineHorizontal(1);
        });
    }

    private static void ComposeContent(IContainer container, FinanceReportDocumentModel report)
    {
        container.PaddingTop(12).Column(column =>
        {
            foreach (var section in report.Sections)
            {
                column.Item().PaddingTop(8).Text(section.Title).Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                column.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.8f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.1f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Description");
                        header.Cell().Element(HeaderCell).Text("Detail");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                    });

                    foreach (var line in section.Lines)
                    {
                        table.Cell().Element(cell => BodyCell(cell, line.IsTotal)).PaddingLeft(line.Level * 10).Text(text =>
                        {
                            var span = text.Span(line.Label);
                            if (line.IsTotal)
                            {
                                span.SemiBold();
                            }
                        });
                        table.Cell().Element(cell => BodyCell(cell, line.IsTotal)).Text(line.Detail ?? "-");
                        table.Cell().Element(cell => BodyCell(cell, line.IsTotal)).AlignRight().Text(text =>
                        {
                            var span = text.Span(Money(line.Amount, report.Currency));
                            if (line.IsTotal)
                            {
                                span.SemiBold();
                            }
                        });
                    }

                    if (section.Lines.Count == 0)
                    {
                        table.Cell().ColumnSpan(3).Element(BodyCell).AlignCenter().Text("No report lines found.");
                    }
                });
            }

            if (report.Summaries.Count > 0)
            {
                column.Item().PaddingTop(16).AlignRight().Width(320).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(summary =>
                {
                    foreach (var item in report.Summaries)
                    {
                        summary.Item().PaddingBottom(4).Row(row =>
                        {
                            row.RelativeItem().Text(item.Label).SemiBold();
                            row.ConstantItem(150).AlignRight().Text(Money(item.Amount, report.Currency)).SemiBold();
                        });
                    }
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container, string? generatedBy)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            if (!string.IsNullOrWhiteSpace(generatedBy))
            {
                text.Span($" by {generatedBy}");
            }

            text.Span("  |  Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(x => x.SemiBold().FontSize(8))
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5);

    private static IContainer BodyCell(IContainer container)
        => BodyCell(container, false);

    private static IContainer BodyCell(IContainer container, bool isTotal)
        => container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Background(isTotal ? Colors.Grey.Lighten4 : Colors.White)
            .Padding(5);

    private static string SafeFileName(string value)
    {
        var fileName = string.IsNullOrWhiteSpace(value) ? "finance-report" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '-');
        }

        return fileName;
    }
}

public sealed class TrialBalanceDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public TrialBalanceDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceTrialBalance;
    protected override string ReportTitle => "Trial Balance";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var report = await LedgerService.GenerateTrialBalanceAsync(new TrialBalanceRequestDto
        {
            AsAtDate = DateOption(request, "asAtDate", DateTime.UtcNow),
            BookClassification = Option(request, "bookClassification") ?? "IFRS",
            IncludeZeroBalances = BoolOption(request, "includeZeroBalances", false)
        });

        var lines = report.Lines.Select(line => Line(line.AccountName, line.AccountNumber ?? line.AccountCode, line.DebitBalance - line.CreditBalance)).ToList();
        var sections = new[] { new FinanceReportSection("Accounts", lines) };
        var summaries = new[]
        {
            new FinanceReportSummary("Total Debits", report.TotalDebits),
            new FinanceReportSummary("Total Credits", report.TotalCredits),
            new FinanceReportSummary("Difference", report.Difference)
        };

        return Model(report.CompanyName, ReportTitle, $"As at {report.AsAtDate:dd MMM yyyy} | Book: {report.BookClassification}", report.CurrencyCode, sections, summaries);
    }
}

public sealed class IncomeStatementDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public IncomeStatementDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceIncomeStatement;
    protected override string ReportTitle => "Income Statement";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var report = await LedgerService.GenerateIncomeStatementAsync(new IncomeStatementRequestDto
        {
            PeriodStart = DateOption(request, "periodStart", new DateTime(DateTime.UtcNow.Year, 1, 1)),
            PeriodEnd = DateOption(request, "periodEnd", DateTime.UtcNow),
            BookClassification = Option(request, "bookClassification") ?? "IFRS",
            IncludeAccountDetails = BoolOption(request, "includeAccountDetails", true)
        });

        var sections = report.Sections
            .OrderBy(section => section.SectionOrder)
            .Select(section => new FinanceReportSection(section.SectionName, section.LineItems
                .OrderBy(line => line.LineOrder)
                .Select(line => Line(line.LineItemName, line.AccountNumbers == null ? null : string.Join(", ", line.AccountNumbers), line.Amount, 1))
                .Append(Line($"Total {section.SectionName}", section.SectionTotal, 0, true))
                .ToList()))
            .ToList();

        var summaries = new[]
        {
            new FinanceReportSummary("Gross Profit", report.GrossProfit),
            new FinanceReportSummary("Operating Profit", report.OperatingProfit),
            new FinanceReportSummary("Net Profit", report.NetProfit)
        };

        return Model(report.CompanyName, ReportTitle, $"{report.PeriodStart:dd MMM yyyy} to {report.PeriodEnd:dd MMM yyyy} | Book: {report.BookClassification}", report.CurrencyCode, sections, summaries);
    }
}

public sealed class BalanceSheetDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public BalanceSheetDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceBalanceSheet;
    protected override string ReportTitle => "Balance Sheet";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var report = await LedgerService.GenerateBalanceSheetAsync(new BalanceSheetRequestDto
        {
            AsAtDate = DateOption(request, "asAtDate", DateTime.UtcNow),
            BookClassification = Option(request, "bookClassification") ?? "IFRS",
            IncludeAccountDetails = BoolOption(request, "includeAccountDetails", true)
        });

        var sections = report.Sections
            .OrderBy(section => section.SectionOrder)
            .Select(section => new FinanceReportSection(section.SectionName, section.Categories
                .OrderBy(category => category.CategoryOrder)
                .SelectMany(category => new[] { Line(category.CategoryName, category.CategoryTotal, 0, true) }
                    .Concat(category.LineItems.OrderBy(line => line.LineOrder)
                        .Select(line => Line(line.LineItemName, line.AccountNumbers == null ? null : string.Join(", ", line.AccountNumbers), line.Amount, 1))))
                .Append(Line($"Total {section.SectionName}", section.SectionTotal, 0, true))
                .ToList()))
            .ToList();

        var summaries = new[]
        {
            new FinanceReportSummary("Total Assets", report.TotalAssets),
            new FinanceReportSummary("Total Liabilities", report.TotalLiabilities),
            new FinanceReportSummary("Total Equity", report.TotalEquity)
        };

        return Model(report.CompanyName, ReportTitle, $"As at {report.AsAtDate:dd MMM yyyy} | Book: {report.BookClassification}", report.CurrencyCode, sections, summaries);
    }
}

public sealed class CashFlowStatementDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public CashFlowStatementDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceCashFlowStatement;
    protected override string ReportTitle => "Cash Flow Statement";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var report = await LedgerService.GenerateCashFlowStatementAsync(new CashFlowStatementRequestDto
        {
            PeriodStart = DateOption(request, "periodStart", new DateTime(DateTime.UtcNow.Year, 1, 1)),
            PeriodEnd = DateOption(request, "periodEnd", DateTime.UtcNow),
            BookClassification = Option(request, "bookClassification") ?? "IFRS",
            IncludeAccountDetails = BoolOption(request, "includeAccountDetails", true),
            Method = Option(request, "method") ?? "Indirect"
        });

        var sourceSections = new[] { report.OperatingActivities, report.InvestingActivities, report.FinancingActivities };
        var sections = sourceSections.Select(section => new FinanceReportSection(section.SectionName, section.LineItems
                .OrderBy(line => line.LineOrder)
                .Select(line => Line(line.LineItemName, line.AccountNumbers == null ? null : string.Join(", ", line.AccountNumbers), line.Amount, 1))
                .Append(Line($"Net {section.SectionName}", section.SectionTotal, 0, true))
                .ToList()))
            .ToList();

        var summaries = new[]
        {
            new FinanceReportSummary("Net Increase in Cash", report.NetIncreaseInCash),
            new FinanceReportSummary("Cash at Beginning", report.CashAtBeginning),
            new FinanceReportSummary("Cash at End", report.CashAtEnd)
        };

        return Model(report.CompanyName, ReportTitle, $"{report.PeriodStart:dd MMM yyyy} to {report.PeriodEnd:dd MMM yyyy} | Book: {report.BookClassification}", report.CurrencyCode, sections, summaries);
    }
}

public sealed class MultiCurrencyDetailDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public MultiCurrencyDetailDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceMultiCurrencyDetail;
    protected override string ReportTitle => "Multi-Currency Detail";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var report = await LedgerService.GenerateMultiCurrencyDetailReportAsync(new MultiCurrencyDetailRequestDto
        {
            StartDate = DateOption(request, "startDate", new DateTime(DateTime.UtcNow.Year, 1, 1)),
            EndDate = DateOption(request, "endDate", DateTime.UtcNow),
            CurrencyCode = Option(request, "currencyCode"),
            IncludeRevaluation = BoolOption(request, "includeRevaluation", true)
        });

        var sections = report.Accounts.Select(account => new FinanceReportSection($"{account.AccountNumber} - {account.AccountName} ({account.CurrencyCode})",
                account.Transactions.Select(line => Line($"{line.TransactionDate:dd MMM yyyy} {line.Description}", line.Reference, line.BaseAmount, 1))
                    .Append(Line("Closing Base Balance", account.ClosingBalanceBase, 0, true))
                    .ToList()))
            .ToList();

        return Model(report.CompanyName, ReportTitle, $"{report.PeriodStart:dd MMM yyyy} to {report.PeriodEnd:dd MMM yyyy}", "GHS", sections, Array.Empty<FinanceReportSummary>());
    }
}

public sealed class DetailedLedgerDocumentBuilder : FinanceReportDocumentBuilderBase
{
    public DetailedLedgerDocumentBuilder(IGeneralLedgerService ledgerService, ICurrentUserService currentUser) : base(ledgerService, currentUser) { }
    public override string DocumentType => DocumentTypes.FinanceDetailedLedger;
    protected override string ReportTitle => "Detailed Ledger";

    protected override async Task<FinanceReportDocumentModel> BuildReportAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken)
    {
        var accountIds = (Option(request, "accountIds") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();

        var report = await LedgerService.GenerateDetailedLedgerAsync(new DetailedLedgerRequestDto
        {
            StartDate = DateOption(request, "startDate", new DateTime(DateTime.UtcNow.Year, 1, 1)),
            EndDate = DateOption(request, "endDate", DateTime.UtcNow),
            AccountIds = accountIds,
            BookClassification = Option(request, "bookClassification") ?? "IFRS",
            IncludeReversed = BoolOption(request, "includeReversed", true),
            IncludeOpeningBalances = BoolOption(request, "includeOpeningBalances", true)
        });

        var sections = report.Accounts.Select(account => new FinanceReportSection($"{account.AccountNumber} - {account.AccountName}",
                new[] { Line($"Opening Balance ({account.OpeningBalanceType})", account.OpeningBalance, 0, true) }
                    .Concat(account.Lines.Select(line => Line($"{line.TransactionDate:dd MMM yyyy} {line.Description}", line.JournalEntryNumber, line.DebitAmount - line.CreditAmount, 1)))
                    .Append(Line($"Closing Balance ({account.ClosingBalanceType})", account.ClosingBalance, 0, true))
                    .ToList()))
            .ToList();

        var summaries = new[]
        {
            new FinanceReportSummary("Total Debits", report.TotalDebits),
            new FinanceReportSummary("Total Credits", report.TotalCredits)
        };

        return Model(report.CompanyName, ReportTitle, $"{report.StartDate:dd MMM yyyy} to {report.EndDate:dd MMM yyyy} | Book: {report.BookClassification}", report.CurrencyCode, sections, summaries);
    }
}

public sealed record FinanceReportDocumentModel(
    string CompanyName,
    string Title,
    string Subtitle,
    string Currency,
    IReadOnlyList<FinanceReportSection> Sections,
    IReadOnlyList<FinanceReportSummary> Summaries,
    string? GeneratedBy);

public sealed record FinanceReportSection(string Title, IReadOnlyList<FinanceReportLine> Lines);

public sealed record FinanceReportLine(string Label, string? Detail, decimal Amount, int Level, bool IsTotal);

public sealed record FinanceReportSummary(string Label, decimal Amount);
