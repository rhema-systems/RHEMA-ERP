using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

/// <summary>
/// Generates a concise tender/RFQ invitation PDF (QuestPDF) used as an email attachment.
/// </summary>
public class TenderInvitationDocumentService : ITenderInvitationDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TenderInvitationDocumentService> _logger;

    public TenderInvitationDocumentService(ApplicationDbContext context, ILogger<TenderInvitationDocumentService> logger)
    {
        _context = context;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<(byte[] Content, string FileName)> GenerateTenderInvitationPdfAsync(Guid tenderId, CancellationToken cancellationToken = default)
    {
        var tender = await _context.Tenders
            .AsNoTracking()
            .Include(t => t.Items)
            .Include(t => t.Lots)
            .FirstOrDefaultAsync(t => t.Id == tenderId, cancellationToken);

        if (tender == null)
            throw new ArgumentException($"Tender {tenderId} not found");

        var lotsById = (tender.Lots ?? [])
            .ToDictionary(l => l.Id, l => l);

        var fileName = $"{(string.Equals(tender.TenderType, "RFQ", StringComparison.OrdinalIgnoreCase) ? "RFQ" : "Tender")}-{tender.TenderNumber}.pdf";

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, tender));
                page.Content().Element(c => ComposeContent(c, tender, lotsById));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Generated tender invitation PDF for {TenderNumber} ({TenderId})", tender.TenderNumber, tenderId);

        return (pdfBytes, fileName);
    }

    private static void ComposeHeader(IContainer container, Core.Entities.Procurement.Tender tender)
    {
        var title = string.Equals(tender.TenderType, "RFQ", StringComparison.OrdinalIgnoreCase)
            ? "REQUEST FOR QUOTATION (RFQ)"
            : "TENDER INVITATION";

        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(title).Bold().FontSize(18);
                    col.Item().Text($"{tender.TenderNumber}").FontSize(12).Bold();
                    col.Item().Text(tender.Title).FontSize(11);
                });

                row.ConstantItem(200).AlignRight().Column(col =>
                {
                    col.Item().Text($"Status: {tender.Status}");
                    col.Item().Text($"Published: {(tender.PublishDate.HasValue ? tender.PublishDate.Value.ToString("dd-MMM-yyyy") : "N/A")}");
                    col.Item().Text($"Deadline: {(tender.SubmissionDeadline.HasValue ? tender.SubmissionDeadline.Value.ToString("dd-MMM-yyyy HH:mm") : "N/A")}");
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private static void ComposeContent(
        IContainer container,
        Core.Entities.Procurement.Tender tender,
        Dictionary<Guid, Core.Entities.Procurement.TenderLot> lotsById)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(tender.Description))
            {
                column.Item().PaddingTop(10).Text(tender.Description).FontSize(10);
            }

            column.Item().PaddingTop(12).Text("Items").Bold().FontSize(12);

            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);  // #
                    columns.ConstantColumn(60);  // Lot
                    columns.ConstantColumn(60);  // Item Code
                    columns.RelativeColumn(4);   // Description
                    columns.ConstantColumn(55);  // Qty
                    columns.ConstantColumn(45);  // UOM
                    columns.ConstantColumn(90);  // Req date
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Lot").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Code").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Qty").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("UOM").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Req. Date").Bold();
                });

                var items = (tender.Items ?? [])
                    .OrderBy(i => i.LotId.HasValue ? (lotsById.TryGetValue(i.LotId.Value, out var l) ? l.DisplayOrder : int.MaxValue) : int.MaxValue)
                    .ThenBy(i => i.LineNumber)
                    .ToList();

                var idx = 1;
                foreach (var item in items)
                {
                    var lotCode = item.LotId.HasValue && lotsById.TryGetValue(item.LotId.Value, out var lot)
                        ? lot.LotCode
                        : "-";

                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(idx.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(lotCode);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ItemCode ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.Description);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.Quantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.UnitOfMeasure ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(item.RequiredDeliveryDate.HasValue ? item.RequiredDeliveryDate.Value.ToString("dd-MMM-yyyy") : "");
                    idx++;
                }
            });

            if (!string.IsNullOrWhiteSpace(tender.TermsAndConditions))
            {
                column.Item().PaddingTop(14).Text("Terms & Conditions").Bold().FontSize(12);
                column.Item().PaddingTop(6).Text(tender.TermsAndConditions).FontSize(10);
            }

            column.Item().PaddingTop(14).Text("Notes").Bold().FontSize(12);
            column.Item().PaddingTop(6).Text(
                string.Equals(tender.TenderType, "RFQ", StringComparison.OrdinalIgnoreCase)
                    ? "Please review the attached RFQ and respond with your quotation. If you have questions, contact the procurement team."
                    : "Please review this tender invitation and respond accordingly. If you have questions, contact the procurement team.")
                .FontSize(10);
        });
    }
}

