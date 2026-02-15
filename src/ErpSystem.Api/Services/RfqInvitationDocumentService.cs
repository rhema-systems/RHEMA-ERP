using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

/// <summary>
/// Generates a concise RFQ invitation PDF (QuestPDF) used as an email attachment.
/// </summary>
public class RfqInvitationDocumentService : IRfqInvitationDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RfqInvitationDocumentService> _logger;

    public RfqInvitationDocumentService(ApplicationDbContext context, ILogger<RfqInvitationDocumentService> logger)
    {
        _context = context;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<(byte[] Content, string FileName)> GenerateRfqInvitationPdfAsync(Guid rfqId, CancellationToken cancellationToken = default)
    {
        var rfq = await _context.RequestForQuotations
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);

        if (rfq == null)
            throw new ArgumentException($"RFQ {rfqId} not found");

        var fileName = $"RFQ-{rfq.RfqNumber}.pdf";

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, rfq));
                page.Content().Element(c => ComposeContent(c, rfq));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Generated RFQ invitation PDF for {RfqNumber} ({RfqId})", rfq.RfqNumber, rfqId);

        return (pdfBytes, fileName);
    }

    private static void ComposeHeader(IContainer container, Core.Entities.Procurement.RequestForQuotation rfq)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("REQUEST FOR QUOTATION (RFQ)").Bold().FontSize(18);
                    col.Item().Text(rfq.RfqNumber).FontSize(12).Bold();
                    col.Item().Text(rfq.Title).FontSize(11);
                });

                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().Text($"Status: {rfq.Status}");
                    col.Item().Text($"Sent: {(rfq.SentAt.HasValue ? rfq.SentAt.Value.ToString("dd-MMM-yyyy HH:mm") : "N/A")}");
                    col.Item().Text($"Deadline: {(rfq.SubmissionDeadline.HasValue ? rfq.SubmissionDeadline.Value.ToString("dd-MMM-yyyy HH:mm") : "N/A")}");
                    col.Item().Text($"Currency: {rfq.Currency}");
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private static void ComposeContent(IContainer container, Core.Entities.Procurement.RequestForQuotation rfq)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(rfq.Description))
            {
                column.Item().PaddingTop(10).Text(rfq.Description).FontSize(10);
            }

            column.Item().PaddingTop(12).Text("Items").Bold().FontSize(12);

            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);  // #
                    columns.ConstantColumn(70);  // Item Code
                    columns.RelativeColumn(5);   // Description
                    columns.ConstantColumn(60);  // Qty
                    columns.ConstantColumn(45);  // UOM
                    columns.ConstantColumn(90);  // Req date
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Code").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Qty").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("UOM").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Req. Date").Bold();
                });

                var items = (rfq.Items ?? [])
                    .Where(i => !i.IsDeleted)
                    .OrderBy(i => i.LineNumber)
                    .ToList();

                foreach (var item in items)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.LineNumber.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ItemCode ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.Description);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.Quantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.UnitOfMeasure ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(item.RequiredDeliveryDate.HasValue ? item.RequiredDeliveryDate.Value.ToString("dd-MMM-yyyy") : "");
                }
            });

            column.Item().PaddingTop(14).Text("Notes").Bold().FontSize(12);
            column.Item().PaddingTop(6).Text("Please review this RFQ and respond with your quotation. If you have questions, contact the procurement team.").FontSize(10);
        });
    }
}
