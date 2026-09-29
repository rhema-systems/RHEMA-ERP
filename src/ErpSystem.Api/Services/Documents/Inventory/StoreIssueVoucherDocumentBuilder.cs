using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Inventory;

public sealed class StoreIssueVoucherDocumentBuilder : IDocumentBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StoreIssueVoucherDocumentBuilder(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.InventoryStoreIssueVoucher;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
            throw new NotSupportedException("Store Issue Vouchers currently support PDF output only.");
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var voucher = await _context.InventoryIssueVouchers.AsNoTracking()
            .Include(value => value.InventoryRequisition)
            .Include(value => value.Warehouse)
            .Include(value => value.Location)
            .Include(value => value.RequestedBy)
            .Include(value => value.ApprovedBy)
            .Include(value => value.IssuedBy)
            .Include(value => value.ReceiverUser)
            .Include(value => value.Lines).ThenInclude(value => value.InventoryItem)
            .Include(value => value.Lines).ThenInclude(value => value.Location)
            .Include(value => value.Actions).ThenInclude(value => value.ReceiptLines)
            .SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == request.EntityId && !value.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("The Store Issue Voucher was not found in the current tenant.");
        var tenant = await _context.Tenants.AsNoTracking().SingleOrDefaultAsync(value => value.Id == tenantId, cancellationToken);
        var content = BuildPdf(voucher, tenant, request.CopyType, _currentUser.UserName);
        return new RenderedDocumentDto
        {
            Content = content,
            ContentType = "application/pdf",
            FileName = $"{Safe(voucher.VoucherNumber)}.pdf",
            DocumentType = DocumentType,
            EntityId = voucher.Id,
            Format = "pdf"
        };
    }

    private static byte[] BuildPdf(InventoryIssueVoucher voucher, Tenant? tenant, string copyType, string? generatedBy) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken4));
            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(tenant?.Name ?? "ERP System").Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                        if (!string.IsNullOrWhiteSpace(tenant?.Address)) left.Item().Text(tenant.Address).FontSize(8);
                    });
                    row.ConstantItem(250).AlignRight().Column(right =>
                    {
                        right.Item().Text("STORE ISSUE VOUCHER").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                        right.Item().Text(voucher.VoucherNumber).SemiBold().FontSize(11);
                        right.Item().Text((copyType ?? "Original").ToUpperInvariant()).FontSize(8);
                    });
                });
                column.Item().PaddingTop(8).LineHorizontal(1);
            });
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(summary =>
                {
                    summary.Item().Row(row =>
                    {
                        row.RelativeItem().Element(cell => Meta(cell, "Requisition", voucher.InventoryRequisition.RequisitionNumber));
                        row.RelativeItem().Element(cell => Meta(cell, "Issued", voucher.IssuedAtUtc.ToString("dd MMM yyyy HH:mm 'UTC'")));
                        row.RelativeItem().Element(cell => Meta(cell, "Status", voucher.Status.ToString()));
                    });
                    summary.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Element(cell => Meta(cell, "Warehouse", voucher.Warehouse.Name));
                        row.RelativeItem().Element(cell => Meta(cell, "Location", voucher.Location?.LocationCode ?? "Warehouse level"));
                        row.RelativeItem().Element(cell => Meta(cell, "Cost object", voucher.ProjectCode ?? voucher.CostCenter ?? "-"));
                    });
                });
                column.Item().PaddingTop(12).Text("Controlled handover").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                column.Item().PaddingTop(5).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                {
                    row.RelativeItem().Element(cell => Meta(cell, "Requested by", Name(voucher.RequestedBy)));
                    row.RelativeItem().Element(cell => Meta(cell, "Approved by",
                        voucher.ApprovedById.HasValue ? Name(voucher.ApprovedBy) : "Not required"));
                    row.RelativeItem().Element(cell => Meta(cell, "Issued by", Name(voucher.IssuedBy)));
                    row.RelativeItem().Element(cell => Meta(cell, "Receiver", Name(voucher.ReceiverUser)));
                });
                column.Item().PaddingTop(14).Text("Issued items").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f); columns.RelativeColumn(2.8f); columns.RelativeColumn(1f);
                        columns.RelativeColumn(1.2f); columns.RelativeColumn(1.2f); columns.RelativeColumn(1.2f);
                    });
                    foreach (var heading in new[] { "Item", "Description / tracking", "Qty", "Unit cost", "Value", "Location" })
                        table.Cell().Element(HeaderCell).Text(heading);
                    foreach (var line in voucher.Lines.OrderBy(value => value.InventoryItem.ItemCode))
                    {
                        var tracking = string.Join(" / ", new[] { line.LotNumber, line.BatchNumber, line.SerialNumber }.Where(value => !string.IsNullOrWhiteSpace(value)));
                        table.Cell().Element(BodyCell).Text(line.InventoryItem.ItemCode);
                        table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(tracking) ? line.InventoryItem.Name : $"{line.InventoryItem.Name} ({tracking})");
                        table.Cell().Element(BodyCell).AlignRight().Text($"{line.Quantity:N4} {line.UnitOfMeasure}");
                        table.Cell().Element(BodyCell).AlignRight().Text(line.UnitCost.ToString("N4"));
                        table.Cell().Element(BodyCell).AlignRight().Text(line.TotalValue.ToString("N2"));
                        table.Cell().Element(BodyCell).Text(line.Location?.LocationCode ?? "-");
                    }
                });
                column.Item().PaddingTop(8).AlignRight().Text($"Total value: {voucher.Lines.Sum(value => value.TotalValue):N2}").Bold();
                column.Item().PaddingTop(14).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(ack =>
                {
                    ack.Item().Text("Receiver acknowledgement").Bold().FontSize(10);
                    ack.Item().PaddingTop(4).Text(voucher.AcknowledgedAtUtc.HasValue
                        ? $"Acknowledged by {Name(voucher.ReceiverUser)} on {voucher.AcknowledgedAtUtc:dd MMM yyyy HH:mm} UTC."
                        : $"Awaiting acknowledgement from {Name(voucher.ReceiverUser)}.");
                    var legacyAcknowledgement = voucher.Status == InventoryIssueVoucherStatus.Acknowledged && voucher.ReceiptSequence == 0;
                    if (legacyAcknowledgement)
                        ack.Item().PaddingTop(3).Text("Historical full acknowledgement; individual receipt quantities were not recorded.");
                    ack.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); columns.RelativeColumn(); columns.RelativeColumn();
                        });
                        foreach (var heading in new[] { "Item", "Received", "Outstanding" })
                            table.Cell().Element(HeaderCell).Text(heading);
                        foreach (var line in voucher.Lines.OrderBy(value => value.InventoryItem.ItemCode))
                        {
                            var received = legacyAcknowledgement ? line.Quantity : voucher.Actions
                                .Where(action => !action.IsDeleted).SelectMany(action => action.ReceiptLines)
                                .Where(receipt => !receipt.IsDeleted && receipt.InventoryIssueVoucherLineId == line.Id)
                                .Sum(receipt => receipt.ReceivedQuantity);
                            table.Cell().Element(BodyCell).Text(line.InventoryItem.ItemCode);
                            table.Cell().Element(BodyCell).AlignRight().Text(received.ToString("N4"));
                            table.Cell().Element(BodyCell).AlignRight().Text((line.Quantity - received).ToString("N4"));
                        }
                    });
                    foreach (var action in voucher.Actions.Where(action => action.ReceiptIdempotencyKey != null).OrderBy(action => action.Sequence))
                        ack.Item().PaddingTop(3).Text($"Receipt {action.Sequence - 1}: {action.OccurredAtUtc:dd MMM yyyy HH:mm} UTC — {action.ActorName}: {action.Comment}");
                    if (!string.IsNullOrWhiteSpace(voucher.ReceiverComment)) ack.Item().PaddingTop(3).Text(voucher.ReceiverComment);
                });
            });
            page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC by {generatedBy ?? "ERP user"} | Integrity {voucher.IntegrityHash[..12]}").FontSize(7).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();

    private static void Meta(IContainer container, string label, string value) => container.Column(column =>
    {
        column.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
        column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).SemiBold();
    });
    private static IContainer HeaderCell(IContainer container) => container.Background(Colors.Blue.Darken3).Padding(5).DefaultTextStyle(style => style.FontColor(Colors.White).SemiBold().FontSize(8));
    private static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    private static string Name(ApplicationUser? user) => user == null ? "-" : string.IsNullOrWhiteSpace((user.FirstName + " " + user.LastName).Trim()) ? user.UserName ?? user.Email ?? "-" : (user.FirstName + " " + user.LastName).Trim();
    private static string Safe(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}
