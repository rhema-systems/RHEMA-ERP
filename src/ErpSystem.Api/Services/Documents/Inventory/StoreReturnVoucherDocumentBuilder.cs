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

public sealed class StoreReturnVoucherDocumentBuilder : IDocumentBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StoreReturnVoucherDocumentBuilder(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.InventoryStoreReturnVoucher;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format)) throw new NotSupportedException("Store Return Vouchers currently support PDF output only.");
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var voucher = await _context.InventoryReturnVouchers.AsNoTracking()
            .Include(value => value.InventoryRequisition)
            .Include(value => value.Warehouse)
            .Include(value => value.Lines).ThenInclude(value => value.InventoryItem)
            .Include(value => value.Lines).ThenInclude(value => value.Location)
            .Include(value => value.Actions).ThenInclude(value => value.ActorUser)
            .SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == request.EntityId && !value.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The Store Return Voucher was not found in the current tenant.");
        var tenant = await _context.Tenants.AsNoTracking().SingleOrDefaultAsync(value => value.Id == tenantId, cancellationToken);
        return new RenderedDocumentDto
        {
            Content = BuildPdf(voucher, tenant, request.CopyType, _currentUser.UserName),
            ContentType = "application/pdf",
            FileName = $"{Safe(voucher.VoucherNumber)}.pdf",
            DocumentType = DocumentType,
            EntityId = voucher.Id,
            Format = "pdf"
        };
    }

    private static byte[] BuildPdf(InventoryReturnVoucher voucher, Tenant? tenant, string copyType, string? generatedBy) =>
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
                        left.Item().Text(tenant?.Name ?? "ERP System").Bold().FontSize(13).FontColor(Colors.Green.Darken3);
                        if (!string.IsNullOrWhiteSpace(tenant?.Address)) left.Item().Text(tenant.Address).FontSize(8);
                    });
                    row.ConstantItem(250).AlignRight().Column(right =>
                    {
                        right.Item().Text("STORE RETURN VOUCHER").Bold().FontSize(18).FontColor(Colors.Green.Darken3);
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
                        row.RelativeItem().Element(cell => Meta(cell, "Status", voucher.Status.ToString()));
                        row.RelativeItem().Element(cell => Meta(cell, "Warehouse", voucher.Warehouse.Name));
                    });
                    summary.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Element(cell => Meta(cell, "Reason code", voucher.ReasonCode));
                        row.RelativeItem(2).Element(cell => Meta(cell, "Reason", voucher.Reason));
                    });
                });
                column.Item().PaddingTop(12).Text("Returned items").Bold().FontSize(11).FontColor(Colors.Green.Darken3);
                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.6f); columns.RelativeColumn(2.8f); columns.RelativeColumn(1f);
                        columns.RelativeColumn(1.2f); columns.RelativeColumn(1.2f); columns.RelativeColumn(1.4f);
                    });
                    foreach (var heading in new[] { "Item", "Description / tracking", "Qty", "Unit cost", "Value", "Location" })
                        table.Cell().Element(HeaderCell).Text(heading);
                    foreach (var line in voucher.Lines.OrderBy(value => value.InventoryItem.ItemCode))
                    {
                        var tracking = string.Join(" / ", new[] { line.LotNumber, line.BatchNumber, line.SerialNumber }.Where(value => !string.IsNullOrWhiteSpace(value)));
                        table.Cell().Element(BodyCell).Text(line.InventoryItem.ItemCode);
                        table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(tracking) ? line.InventoryItem.Name : $"{line.InventoryItem.Name} ({tracking})");
                        table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("N4"));
                        table.Cell().Element(BodyCell).AlignRight().Text(line.UnitCost.ToString("N4"));
                        table.Cell().Element(BodyCell).AlignRight().Text(line.TotalValue.ToString("N2"));
                        table.Cell().Element(BodyCell).Text(line.Location?.LocationCode ?? "Warehouse level");
                    }
                });
                column.Item().PaddingTop(8).AlignRight().Text($"Total value: {voucher.TotalValue:N2}").Bold();
                column.Item().PaddingTop(14).Text("Control lifecycle").Bold().FontSize(11).FontColor(Colors.Green.Darken3);
                foreach (var action in voucher.Actions.OrderBy(value => value.Sequence))
                    column.Item().PaddingTop(3).Text($"{action.Sequence}. {action.ActionType} — {Name(action.ActorUser)} — {action.OccurredAtUtc:dd MMM yyyy HH:mm} UTC{(string.IsNullOrWhiteSpace(action.Comment) ? string.Empty : $" — {action.Comment}")}").FontSize(8);
            });
            page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC by {generatedBy ?? "ERP user"} | Integrity {voucher.IntegrityHash[..12]}").FontSize(7).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();

    private static void Meta(IContainer container, string label, string value) => container.Column(column =>
    {
        column.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
        column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).SemiBold();
    });
    private static IContainer HeaderCell(IContainer container) => container.Background(Colors.Green.Darken3).Padding(5).DefaultTextStyle(style => style.FontColor(Colors.White).SemiBold().FontSize(8));
    private static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    private static string Name(ApplicationUser user) => string.IsNullOrWhiteSpace((user.FirstName + " " + user.LastName).Trim()) ? user.UserName ?? user.Email ?? "-" : (user.FirstName + " " + user.LastName).Trim();
    private static string Safe(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}
