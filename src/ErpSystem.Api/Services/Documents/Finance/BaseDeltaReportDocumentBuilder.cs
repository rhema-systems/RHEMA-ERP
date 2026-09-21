using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Renders the calculated base-plus-Delta reporting view. This is deliberately a parameterized
/// report, not a voucher or a synthetic accounting book: the base and Delta ledgers remain
/// independent financial authority and are combined only for presentation.
/// </summary>
public sealed class BaseDeltaReportDocumentBuilder : IDocumentBuilder
{
    private readonly IAccountingBookService _accountingBooks;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly ICurrentUserService _currentUser;

    public BaseDeltaReportDocumentBuilder(
        IAccountingBookService accountingBooks,
        ITenantSettingsService tenantSettings,
        ICurrentUserService currentUser)
    {
        _accountingBooks = accountingBooks;
        _tenantSettings = tenantSettings;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceBaseDeltaReport;
    public bool RequiresEntityId => false;

    public bool SupportsFormat(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException("Base + Delta report currently supports PDF output only.");

        var deltaBookId = Guid.TryParse(Option(request, "deltaAccountingBookId"), out var parsedBookId)
            && parsedBookId != Guid.Empty
                ? parsedBookId
                : throw new InvalidOperationException("A Delta accounting-book ID is required.");
        var asOfDate = DateTime.TryParse(Option(request, "asOfDate"), out var parsedDate)
            ? parsedDate.Date
            : DateTime.UtcNow.Date;
        var report = await _accountingBooks.GetDeltaCombinedReportAsync(deltaBookId, asOfDate, cancellationToken);
        var companyName = await _tenantSettings.GetCompanyNameAsync();
        var content = BuildPdf(report, companyName, _currentUser.UserName);

        return new RenderedDocumentDto
        {
            Content = content,
            ContentType = "application/pdf",
            FileName = $"base-delta-{Safe(report.BaseAccountingBookCode)}-{Safe(report.DeltaAccountingBookCode)}-{report.AsOfDate:yyyyMMdd}.pdf",
            DocumentType = DocumentType,
            EntityId = Guid.Empty,
            Format = "pdf"
        };
    }

    private static string? Option(DocumentRenderRequestDto request, string key)
        => request.Options != null && request.Options.TryGetValue(key, out var value)
            ? value?.Trim()
            : null;

    private static byte[] BuildPdf(DeltaBookCombinedReportDto report, string companyName, string? generatedBy)
        => Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Colors.Grey.Darken4));
                page.Header().Column(header =>
                {
                    header.Item().Text(string.IsNullOrWhiteSpace(companyName) ? "ERP System" : companyName).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    header.Item().Text("Base + Delta report").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                    header.Item().Text($"As at {report.AsOfDate:dd MMM yyyy} | Base: {report.BaseAccountingBookCode} | Delta: {report.DeltaAccountingBookCode} | Positive values are net debits; negative values are net credits.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(8).LineHorizontal(1);
                });
                page.Content().PaddingTop(12).Column(content =>
                {
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(2.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(1.35f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Account");
                            header.Cell().Element(HeaderCell).Text("Account name");
                            header.Cell().Element(HeaderCell).Text("Type");
                            header.Cell().Element(HeaderCell).AlignRight().Text($"Base ({report.BaseAccountingBookCode})");
                            header.Cell().Element(HeaderCell).AlignRight().Text($"Delta ({report.DeltaAccountingBookCode})");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Combined");
                        });
                        foreach (var line in report.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(line.AccountNumber);
                            table.Cell().Element(BodyCell).Text(line.AccountName);
                            table.Cell().Element(BodyCell).Text(line.AccountType);
                            table.Cell().Element(BodyCell).AlignRight().Text(Signed(line.BaseSignedBalance, report.FunctionalCurrencyCode));
                            table.Cell().Element(BodyCell).AlignRight().Text(Signed(line.DeltaSignedBalance, report.FunctionalCurrencyCode));
                            table.Cell().Element(BodyCell).AlignRight().Text(Signed(line.CombinedSignedBalance, report.FunctionalCurrencyCode)).SemiBold();
                        }
                        if (report.Lines.Count == 0)
                            table.Cell().ColumnSpan(6).Element(BodyCell).AlignCenter().Text("No mapped accounts or posted balances were found.");
                    });
                    content.Item().PaddingTop(14).AlignRight().Width(360).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(summary =>
                    {
                        Summary(summary, "Base net control", report.BaseTotal, report.FunctionalCurrencyCode);
                        Summary(summary, "Delta net control", report.DeltaTotal, report.FunctionalCurrencyCode);
                        Summary(summary, "Combined net control", report.CombinedTotal, report.FunctionalCurrencyCode);
                    });
                    content.Item().PaddingTop(6).Text("Net controls should normally be zero for balanced posted journals; they are reconciliation checks, not statement totals.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    if (!string.IsNullOrWhiteSpace(generatedBy)) text.Span($" by {generatedBy}");
                    text.Span("  |  Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
                });
            });
        }).GeneratePdf();

    private static void Summary(ColumnDescriptor column, string label, decimal value, string currency)
        => column.Item().PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Text(label).SemiBold();
            row.ConstantItem(170).AlignRight().Text(Signed(value, currency)).SemiBold();
        });

    private static string Signed(decimal value, string currency)
        => value > 0m ? $"{currency} {value:N2} Dr"
            : value < 0m ? $"{currency} {Math.Abs(value):N2} Cr"
            : $"{currency} 0.00";

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(style => style.SemiBold().FontSize(8))
            .Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);

    private static string Safe(string value)
        => string.Concat((value ?? string.Empty).Trim().ToLowerInvariant().Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) || char.IsWhiteSpace(character) ? '-' : character));
}
