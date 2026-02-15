using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

public class TransferDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransferDocumentService> _logger;

    public TransferDocumentService(ApplicationDbContext context, ILogger<TransferDocumentService> logger)
    {
        _context = context;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateShipmentNoteAsync(Guid transferId)
    {
        var transfer = await _context.InventoryTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(t => t.ShippedBy)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new ArgumentException($"Transfer {transferId} not found");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeShipmentHeader(h, transfer));
                page.Content().Element(c => ComposeShipmentContent(c, transfer));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    public async Task<byte[]> GenerateGoodsReceivedNoteAsync(Guid transferId)
    {
        var transfer = await _context.InventoryTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(t => t.ReceivedBy)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new ArgumentException($"Transfer {transferId} not found");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeGrnHeader(h, transfer));
                page.Content().Element(c => ComposeGrnContent(c, transfer));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return pdfBytes;
    }

    private void ComposeShipmentHeader(IContainer container, ErpSystem.Core.Entities.Inventory.InventoryTransfer transfer)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SHIPMENT NOTE").Bold().FontSize(20);
                    col.Item().Text($"Transfer #: {transfer.TransferNumber}").FontSize(12);
                });
                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Text($"Date: {transfer.ShippedDate?.ToString("dd-MMM-yyyy") ?? DateTime.Now.ToString("dd-MMM-yyyy")}");
                    col.Item().Text($"Status: {transfer.Status}");
                });
            });
            column.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private void ComposeShipmentContent(IContainer container, ErpSystem.Core.Entities.Inventory.InventoryTransfer transfer)
    {
        container.PaddingVertical(10).Column(column =>
        {
            // Source and Destination
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("FROM (Source Warehouse)").Bold();
                    col.Item().Text(transfer.SourceWarehouse?.Name ?? "N/A");
                    col.Item().Text(transfer.SourceWarehouse?.Address ?? "");
                });
                row.ConstantItem(20);
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("TO (Destination Warehouse)").Bold();
                    col.Item().Text(transfer.DestinationWarehouse?.Name ?? "N/A");
                    col.Item().Text(transfer.DestinationWarehouse?.Address ?? "");
                });
            });

            // Tracking Info
            if (!string.IsNullOrEmpty(transfer.TrackingNumber))
            {
                column.Item().PaddingTop(10).Text($"Tracking Number: {transfer.TrackingNumber}").Bold();
            }

            // Items Table
            column.Item().PaddingTop(15).Text("ITEMS SHIPPED").Bold().FontSize(12);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Item Code").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("UoM").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Requested").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Shipped").Bold();
                });

                var idx = 1;
                foreach (var item in transfer.Items.Where(i => i.ShippedQuantity > 0))
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(idx.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.ItemCode ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.Name ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.UnitOfMeasure ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.RequestedQuantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.ShippedQuantity.ToString("N2"));
                    idx++;
                }
            });

            // Signatures
            column.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Prepared By:").Bold();
                    col.Item().PaddingTop(30).LineHorizontal(1);
                    col.Item().Text(transfer.ShippedBy != null ? $"{transfer.ShippedBy.FirstName} {transfer.ShippedBy.LastName}" : "");
                });
                row.ConstantItem(50);
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Received By:").Bold();
                    col.Item().PaddingTop(30).LineHorizontal(1);
                    col.Item().Text("(Name & Signature)");
                });
            });

            // Notes
            if (!string.IsNullOrEmpty(transfer.Notes))
            {
                column.Item().PaddingTop(20).Text("Notes:").Bold();
                column.Item().Text(transfer.Notes);
            }
        });
    }

    private void ComposeGrnHeader(IContainer container, ErpSystem.Core.Entities.Inventory.InventoryTransfer transfer)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("GOODS RECEIVED NOTE (GRN)").Bold().FontSize(20);
                    col.Item().Text($"Transfer #: {transfer.TransferNumber}").FontSize(12);
                });
                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Text($"Date: {transfer.ReceivedDate?.ToString("dd-MMM-yyyy") ?? DateTime.Now.ToString("dd-MMM-yyyy")}");
                    col.Item().Text($"Status: {transfer.Status}");
                });
            });
            column.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private void ComposeGrnContent(IContainer container, ErpSystem.Core.Entities.Inventory.InventoryTransfer transfer)
    {
        container.PaddingVertical(10).Column(column =>
        {
            // Source and Destination
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("RECEIVED FROM").Bold();
                    col.Item().Text(transfer.SourceWarehouse?.Name ?? "N/A");
                    col.Item().Text(transfer.SourceWarehouse?.Address ?? "");
                });
                row.ConstantItem(20);
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("RECEIVED AT").Bold();
                    col.Item().Text(transfer.DestinationWarehouse?.Name ?? "N/A");
                    col.Item().Text(transfer.DestinationWarehouse?.Address ?? "");
                });
            });

            // Items Table
            column.Item().PaddingTop(15).Text("ITEMS RECEIVED").Bold().FontSize(12);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Item Code").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("UoM").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Shipped").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Received").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Variance").Bold();
                });

                var idx = 1;
                foreach (var item in transfer.Items.Where(i => i.ShippedQuantity > 0 || i.ReceivedQuantity > 0))
                {
                    var variance = item.ReceivedQuantity - item.ShippedQuantity;
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(idx.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.ItemCode ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.Name ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.InventoryItem?.UnitOfMeasure ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.ShippedQuantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.ReceivedQuantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight()
                        .Text(variance != 0 ? variance.ToString("N2") : "-")
                        .FontColor(variance < 0 ? Colors.Red.Medium : variance > 0 ? Colors.Green.Medium : Colors.Black);
                    idx++;
                }
            });

            // Signatures
            column.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Received By:").Bold();
                    col.Item().PaddingTop(30).LineHorizontal(1);
                    col.Item().Text(transfer.ReceivedBy != null ? $"{transfer.ReceivedBy.FirstName} {transfer.ReceivedBy.LastName}" : "");
                });
                row.ConstantItem(50);
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Inspected By:").Bold();
                    col.Item().PaddingTop(30).LineHorizontal(1);
                    col.Item().Text("(Name & Signature)");
                });
            });

            // Notes
            if (!string.IsNullOrEmpty(transfer.Notes))
            {
                column.Item().PaddingTop(20).Text("Notes:").Bold();
                column.Item().Text(transfer.Notes);
            }
        });
    }
}

