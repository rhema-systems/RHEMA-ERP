using System.Globalization;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services.Inventory;

internal static class InventoryDisposalWaybillDocument
{
    internal static byte[] Generate(InventoryDisposalDto disposal)
    {
        if (disposal.Method == InventoryDisposalMethod.Sale || disposal.Status is not (InventoryDisposalStatus.Approved or InventoryDisposalStatus.ReadyForExecution or InventoryDisposalStatus.AdjustmentPending or InventoryDisposalStatus.Completed))
            throw new InventoryDisposalException("INV_DISPOSAL_WAYBILL_STATE",
                "A waybill is available only for an authorized non-sales disposal ready for execution or completed.");

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(header =>
            {
                header.Item().Text("DISPOSAL WAYBILL").Bold().FontSize(20);
                header.Item().Text(disposal.DisposalNumber).FontSize(12);
                header.Item().PaddingTop(8).LineHorizontal(1);
            });
            page.Content().PaddingVertical(12).Column(content =>
            {
                content.Spacing(6);
                content.Item().Text($"Method: {disposal.Method}   |   Status: {disposal.Status}");
                content.Item().Text($"Warehouse: {disposal.WarehouseCode} - {disposal.WarehouseName}");
                content.Item().Text($"Date: {(disposal.CompletedAtUtc ?? disposal.ApprovedAtUtc ?? disposal.RequestedAtUtc):dd MMM yyyy}");
                content.Item().Text($"Recipient / destination: {disposal.BuyerOrRecipient ?? "Not recorded"}");
                content.Item().Text($"Reference: {disposal.ExecutionReference ?? disposal.DisposalNumber}");
                content.Item().Text($"Reason: {disposal.Reason}");
                content.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(24);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var heading in new[] { "#", "Item / tracking", "Bin", "Quantity", "Unit" })
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(heading).Bold();
                    });
                    var number = 0;
                    foreach (var line in disposal.Lines)
                    {
                        table.Cell().Padding(5).Text((++number).ToString(CultureInfo.InvariantCulture));
                        table.Cell().Padding(5).Column(item =>
                        {
                            item.Item().Text($"{line.ItemCode} - {line.ItemName}");
                            if (!string.IsNullOrWhiteSpace(line.LotNumber)) item.Item().Text($"Lot: {line.LotNumber}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(line.BatchNumber)) item.Item().Text($"Batch: {line.BatchNumber}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(line.SerialNumber)) item.Item().Text($"Serial: {line.SerialNumber}").FontSize(8);
                        });
                        table.Cell().Padding(5).Text(line.LocationCode);
                        table.Cell().Padding(5).AlignRight().Text(line.Quantity.ToString("0.####", CultureInfo.InvariantCulture));
                        table.Cell().Padding(5).Text(line.UnitOfMeasure);
                    }
                });
                content.Item().PaddingTop(12).Text($"Prepared by: {disposal.RequestedByName}");
                content.Item().Text(disposal.ApprovalRequired
                    ? $"Approval: {disposal.ApprovedAtUtc:dd MMM yyyy HH:mm} UTC | {disposal.AuthorityRoute}"
                    : "Approval: not required by the configured workflow");
                content.Item().PaddingTop(12).Text("Carrier / vehicle: _____________________________________");
                content.Item().Text("Released by / date: ____________________________________");
                content.Item().Text("Received by / date: ____________________________________");
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"{disposal.DisposalNumber} | Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }
}
