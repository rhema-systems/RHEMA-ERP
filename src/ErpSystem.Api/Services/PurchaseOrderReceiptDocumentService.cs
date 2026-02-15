using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

public class PurchaseOrderReceiptDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PurchaseOrderReceiptDocumentService> _logger;

    public PurchaseOrderReceiptDocumentService(ApplicationDbContext context, ILogger<PurchaseOrderReceiptDocumentService> logger)
    {
        _context = context;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateGrnAsync(Guid receiptId)
    {
        var receipt = await _context.PurchaseOrderReceipts
            .Include(r => r.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(r => r.ReceivedBy)
            .Include(r => r.InspectedBy)
            .Include(r => r.Items)
                .ThenInclude(i => i.PurchaseOrderItem)
                    .ThenInclude(poi => poi.InventoryItem)
            .Include(r => r.Items)
                .ThenInclude(i => i.PurchaseOrderItem)
                    .ThenInclude(poi => poi.Warehouse)
            .FirstOrDefaultAsync(r => r.Id == receiptId);

        if (receipt == null)
            throw new ArgumentException($"Receipt {receiptId} not found");

        // Resolve put-away locations for display.
        var locationIds = (receipt.Items ?? [])
            .Where(i => i.LocationId.HasValue && i.LocationId.Value != Guid.Empty)
            .Select(i => i.LocationId!.Value)
            .Distinct()
            .ToList();

        var locationLookup = new Dictionary<Guid, Core.Entities.Inventory.WarehouseLocation>();
        if (locationIds.Count > 0)
        {
            // We intentionally do NOT rely on PO-line warehouse here; receiving can override the put-away location.
            // Also, for older/migrated receipts, LocationId might not map cleanly; we'll fallback below.
            var locations = await _context.WarehouseLocations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(l => l.Warehouse)
                .Where(l => locationIds.Contains(l.Id))
                .ToListAsync();

            locationLookup = locations.ToDictionary(l => l.Id, l => l);
        }

        // Defensive fallback: if some LocationId values don't match WarehouseLocations (historical data),
        // try interpreting them as Warehouse IDs so the GRN still prints something usable.
        var unresolvedLocationIds = locationIds.Where(id => !locationLookup.ContainsKey(id)).ToList();
        var warehouseLookup = new Dictionary<Guid, Core.Entities.Inventory.Warehouse>();
        if (unresolvedLocationIds.Count > 0)
        {
            var warehouses = await _context.Warehouses
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(w => unresolvedLocationIds.Contains(w.Id))
                .ToListAsync();

            warehouseLookup = warehouses.ToDictionary(w => w.Id, w => w);
        }

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, receipt));
                page.Content().Element(c => ComposeContent(c, receipt, locationLookup, warehouseLookup));
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

    private static void ComposeHeader(IContainer container, Core.Entities.Procurement.PurchaseOrderReceipt receipt)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("GOODS RECEIVED NOTE (GRN)").Bold().FontSize(18);
                    col.Item().Text($"GRN #: {receipt.ReceiptNumber}").FontSize(12);
                    col.Item().Text($"PO #: {receipt.PurchaseOrder?.OrderNumber ?? "N/A"}").FontSize(11);
                });
                row.ConstantItem(180).AlignRight().Column(col =>
                {
                    col.Item().Text($"Receipt Date: {receipt.ReceiptDate:dd-MMM-yyyy}");
                    col.Item().Text($"Status: {receipt.Status}");
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private static void ComposeContent(
        IContainer container,
        Core.Entities.Procurement.PurchaseOrderReceipt receipt,
        IReadOnlyDictionary<Guid, Core.Entities.Inventory.WarehouseLocation> locationLookup,
        IReadOnlyDictionary<Guid, Core.Entities.Inventory.Warehouse> warehouseLookup)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("SUPPLIER").Bold();
                    col.Item().Text(receipt.PurchaseOrder?.BusinessPartner?.PartnerName ?? "N/A");
                    if (!string.IsNullOrWhiteSpace(receipt.CarrierName))
                        col.Item().Text($"Carrier: {receipt.CarrierName}");
                    if (!string.IsNullOrWhiteSpace(receipt.TrackingNumber))
                        col.Item().Text($"Tracking #: {receipt.TrackingNumber}");
                    if (!string.IsNullOrWhiteSpace(receipt.DeliveryNote))
                        col.Item().Text($"Delivery Note: {receipt.DeliveryNote}");
                });

                row.ConstantItem(20);

                row.RelativeItem().Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("RECEIPT DETAILS").Bold();
                    if (receipt.ReceivedBy != null)
                        col.Item().Text($"Received By: {receipt.ReceivedBy.FirstName} {receipt.ReceivedBy.LastName}".Trim());
                    if (receipt.RequiresInspection)
                        col.Item().Text("Quality Inspection: Required");
                    if (receipt.InspectedBy != null)
                        col.Item().Text($"Inspector: {receipt.InspectedBy.FirstName} {receipt.InspectedBy.LastName}".Trim());
                });
            });

            if (!string.IsNullOrWhiteSpace(receipt.Notes))
            {
                column.Item().PaddingTop(10).Border(1).Padding(10).Column(col =>
                {
                    col.Item().Text("NOTES").Bold();
                    col.Item().Text(receipt.Notes);
                });
            }

            column.Item().PaddingTop(15).Text("ITEMS RECEIVED").Bold().FontSize(12);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Item Code").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("UOM").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Received").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Location").Bold();
                });

                var idx = 1;
                foreach (var item in (receipt.Items ?? []).OrderBy(i => i.PurchaseOrderItem?.InventoryItem?.ItemCode))
                {
                    var itemCode = item.PurchaseOrderItem?.InventoryItem?.ItemCode ?? "";
                    var itemName = item.PurchaseOrderItem?.InventoryItem?.Name ?? item.PurchaseOrderItem?.ItemDescription ?? "";
                    var uom = item.UnitOfMeasure ?? item.PurchaseOrderItem?.UnitOfMeasure ?? item.PurchaseOrderItem?.InventoryItem?.UnitOfMeasure ?? "";

                    // Prefer receipt put-away location -> fallback to warehouse (for older/bad data) -> fallback to PO-line warehouse.
                    string locationDisplay;
                    if (item.LocationId.HasValue && item.LocationId.Value != Guid.Empty)
                    {
                        if (locationLookup.TryGetValue(item.LocationId.Value, out var wl))
                        {
                            var whCode = wl.Warehouse?.Code ?? string.Empty;
                            var wlCode = wl.LocationCode ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(whCode) && !string.IsNullOrWhiteSpace(wlCode))
                                locationDisplay = $"{whCode} / {wlCode}";
                            else if (!string.IsNullOrWhiteSpace(wlCode))
                                locationDisplay = wlCode;
                            else if (!string.IsNullOrWhiteSpace(whCode))
                                locationDisplay = whCode;
                            else
                                locationDisplay = "-";
                        }
                        else if (warehouseLookup.TryGetValue(item.LocationId.Value, out var wh))
                        {
                            // LocationId stored as WarehouseId (historical data). Show warehouse code/name.
                            locationDisplay = !string.IsNullOrWhiteSpace(wh.Code)
                                ? wh.Code
                                : (!string.IsNullOrWhiteSpace(wh.Name) ? wh.Name : "-");
                        }
                        else
                        {
                            // Unknown ID: show PO-line warehouse if we have it; otherwise show a short hint for auditing.
                            var poWarehouse = item.PurchaseOrderItem?.Warehouse;
                            locationDisplay = poWarehouse != null
                                ? (!string.IsNullOrWhiteSpace(poWarehouse.Code) ? poWarehouse.Code : (poWarehouse.Name ?? "-"))
                                : $"(Unknown: {item.LocationId.Value.ToString()[..8]})";
                        }
                    }
                    else
                    {
                        var poWarehouse = item.PurchaseOrderItem?.Warehouse;
                        locationDisplay = poWarehouse != null
                            ? (!string.IsNullOrWhiteSpace(poWarehouse.Code) ? poWarehouse.Code : (poWarehouse.Name ?? "-"))
                            : "-";
                    }

                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(idx.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(itemCode);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(itemName);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(uom);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.ReceivedQuantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(locationDisplay);
                    idx++;
                }
            });

            column.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Received By:").Bold();
                    col.Item().PaddingTop(20).LineHorizontal(1);
                    col.Item().Text("(Name & Signature)");
                });

                row.ConstantItem(50);

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Authorized By:").Bold();
                    col.Item().PaddingTop(20).LineHorizontal(1);
                    col.Item().Text("(Name & Signature)");
                });
            });
        });
    }
}
