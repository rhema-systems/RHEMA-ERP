using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Sales;

internal static class SalesOrderInvoiceGuard
{
    internal static readonly FinancePostingProducerContext Producer = new(FinanceDimensionRouteId.SalesOrderCustomerInvoice);

    internal static async Task RequireNotGeneratedAsync(IUnitOfWork unitOfWork, Guid tenantId, Guid invoiceId, CancellationToken ct)
    {
        if (await unitOfWork.Repository<SalesOrder>().GetQueryable(x => x.TenantId == tenantId && x.InvoiceId == invoiceId).AnyAsync(ct))
            throw new InvalidOperationException("This invoice retains approved Sales order lines. Its source cannot be independently edited, deleted or voided.");
    }

    internal static async Task ValidateAsync(IUnitOfWork unitOfWork, Guid tenantId, Invoice invoice,
        FinancePostingProducerContext? producer, CancellationToken ct)
    {
        var source = await unitOfWork.Repository<SalesOrder>().GetQueryable(x => x.TenantId == tenantId && x.InvoiceId == invoice.Id)
            .Include(x => x.Lines).SingleOrDefaultAsync(ct);
        if (source is null)
        {
            if (producer?.RouteId == FinanceDimensionRouteId.SalesOrderCustomerInvoice)
                throw new InvalidOperationException("This Sales invoice has no retained order source.");
            return;
        }
        if (producer?.RouteId != FinanceDimensionRouteId.SalesOrderCustomerInvoice || source.IsDeleted || invoice.IsDeleted ||
            invoice.TenantId != tenantId || source.InvoiceEconomicsJson != Snapshot(invoice) || source.InvoiceSourceJson != SourceSnapshot(source))
            throw new InvalidOperationException("The Sales invoice no longer matches its retained source economics. Historical invoices without verified lineage require reconciliation.");
        RequireEligible(source, allowLinked: true);
    }

    internal static async Task ValidateCreationAsync(IUnitOfWork unitOfWork, Guid tenantId, InvoiceCreateDto dto,
        FinancePostingProducerContext? producer, CancellationToken ct)
    {
        if (producer?.RouteId != FinanceDimensionRouteId.SalesOrderCustomerInvoice)
        {
            if (dto.LineItems.Any(x => x.LineItemType == "Inventory" || x.InventoryItemId.HasValue || x.WarehouseId.HasValue ||
                x.LocationId.HasValue || x.LotNumber != null || x.SerialNumber != null || x.ExpirationDate.HasValue))
                throw new InvalidOperationException("Inventory identity is accepted only through the verified Sales order invoice route.");
            return;
        }
        var ids = dto.LineItems.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToArray();
        var candidates = await unitOfWork.Repository<SalesOrder>().GetQueryable(x => x.TenantId == tenantId &&
            x.Lines.Any(l => ids.Contains(l.Id))).Include(x => x.Lines).ToListAsync(ct);
        if (candidates.Count != 1) throw new InvalidOperationException("A Sales invoice must identify exactly one current-tenant source order.");
        var order = candidates[0];
        RequireEligible(order, allowLinked: false);
        var lines = order.Lines.Where(x => !x.IsDeleted).ToList();
        if (dto.BusinessPartnerId != order.BusinessPartnerId || dto.Reference != order.DocumentNumber ||
            dto.CurrencyCode != order.Currency || dto.DiscountAmount != order.DiscountAmount || dto.IsOpeningBalance ||
            dto.LineItems.Count != lines.Count + (order.ShippingAmount > 0 ? 1 : 0))
            throw new InvalidOperationException("The invoice header does not match the approved Sales order.");
        foreach (var source in lines)
        {
            var line = dto.LineItems.SingleOrDefault(x => x.Id == source.Id)
                ?? throw new InvalidOperationException("Every Sales order line must be invoiced exactly once.");
            if (line.Quantity != source.Quantity || line.UnitPrice != source.UnitPrice || line.Description != source.Description ||
                line.ProductId != source.ProductId || line.DiscountPercentage != source.DiscountPercentage || line.Unit != source.Unit ||
                line.InventoryItemId != source.InventoryItemId || line.WarehouseId != (source.InventoryItemId.HasValue ? source.WarehouseId ?? order.WarehouseId : null) ||
                line.LocationId != source.LocationId || line.LotNumber != source.LotNumber || line.SerialNumber != source.SerialNumber ||
                line.ExpirationDate != source.ExpirationDate || (source.GLAccountId.HasValue && line.GLAccountId != source.GLAccountId) ||
                (source.TaxGroupId.HasValue && line.TaxGroupId != source.TaxGroupId))
                throw new InvalidOperationException("An invoice line differs from its retained Sales quantity, price, account or tracking identity.");
            await RequirePostingLineAsync(unitOfWork, tenantId, line, ct);
        }
        if (order.ShippingAmount > 0)
        {
            var freight = dto.LineItems.SingleOrDefault(x => x.Id == FreightLineId(order.Id));
            if (freight is null || freight.Quantity != 1 || freight.UnitPrice != order.ShippingAmount || freight.LineItemType != "GLAccount" ||
                freight.InventoryItemId.HasValue || freight.ProductId.HasValue || freight.DiscountPercentage != 0)
                throw new InvalidOperationException("The freight line must retain the order's complete shipping amount.");
            await RequirePostingLineAsync(unitOfWork, tenantId, freight, ct);
        }
    }

    internal static void RequireEligible(SalesOrder order, bool allowLinked)
    {
        if (order.IsDeleted || order.OrderType != SalesOrderType.Standard || order.OrderStatus is not
            (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced or SalesOrderStatus.Closed))
            throw new InvalidOperationException("Only a confirmed standard Sales order can generate or post an invoice.");
        if (!allowLinked && (order.InvoiceId.HasValue || order.Lines.Any(x => !x.IsDeleted && x.InvoicedQuantity != 0)))
            throw new InvalidOperationException("This order already has invoice history. Reconcile its existing invoice instead of creating another.");
        if (order.Lines.All(x => x.IsDeleted) || order.Lines.Any(x => !x.IsDeleted && (x.TenantId != order.TenantId || x.Quantity <= 0 || x.UnitPrice < 0)))
            throw new InvalidOperationException("The source order has missing or invalid lines.");
    }

    internal static async Task RequirePostingLineAsync(IUnitOfWork uow, Guid tenant, InvoiceLineItemCreateDto line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 200)
            throw new InvalidOperationException("Sales invoice descriptions must contain between 1 and 200 characters. Shorten and reapprove the source order description before invoicing; no text has been truncated.");
        if (line.TaxTreatment == TaxTreatment.PendingReview || !Enum.IsDefined(line.TaxTreatment) ||
            (line.TaxTreatment == TaxTreatment.Standard && !line.TaxGroupId.HasValue))
            throw new InvalidOperationException("Select the applicable tax treatment and tax group before generating the Sales invoice.");
        var account = await uow.Repository<Account>().GetQueryable(x => x.Id == line.GLAccountId && x.TenantId == tenant && !x.IsDeleted).SingleOrDefaultAsync(ct);
        if (account is null || account.Status != AccountStatus.Active || !account.AllowDirectPosting || account.IsControlAccount || account.AccountType != AccountType.Revenue)
            throw new InvalidOperationException("Select an active, directly postable revenue account for each Sales invoice line.");
        if (!line.InventoryItemId.HasValue)
        {
            if (line.LineItemType == "Inventory" || line.WarehouseId.HasValue || line.LocationId.HasValue ||
                line.LotNumber != null || line.SerialNumber != null || line.ExpirationDate.HasValue)
                throw new InvalidOperationException("An inventory line must include its complete verified item and bin identity.");
            return;
        }
        var item = await uow.Repository<InventoryItem>().GetQueryable(x => x.Id == line.InventoryItemId && x.TenantId == tenant && !x.IsDeleted).SingleOrDefaultAsync(ct);
        var bin = await uow.Repository<WarehouseLocation>().GetQueryable(x => x.Id == line.LocationId && x.TenantId == tenant && !x.IsDeleted)
            .Include(x => x.Warehouse).SingleOrDefaultAsync(ct);
        if (item is null || item.Status != ItemStatus.Active || item.ItemType != ItemType.StockItem || line.LineItemType != "Inventory" ||
            bin is null || !bin.IsActive || bin.InventoryWarehouseId != line.WarehouseId || !bin.Warehouse.IsActive ||
            bin.Warehouse.IsDeleted || bin.Warehouse.TenantId != tenant || bin.IsInTransitLocation || bin.IsQuarantineLocation || bin.IsInspectionLocation || bin.IsDamageLocation)
            throw new InvalidOperationException("The Sales stock line requires an active item and a permitted exact warehouse bin.");
        if ((item.IsLotTracked && string.IsNullOrWhiteSpace(line.LotNumber)) ||
            (item.IsSerialTracked && (string.IsNullOrWhiteSpace(line.SerialNumber) || line.Quantity != 1)))
            throw new InvalidOperationException("The Sales order must retain the exact lot or single serial number before invoicing.");
        if (!string.Equals(line.Unit?.Trim(), item.UnitOfMeasure?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sales stock quantities must use the item's base unit of measure. Convert and reapprove the source quantity before invoicing.");
    }

    internal static Guid FreightLineId(Guid orderId) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"SALES:INVOICE:FREIGHT:1:{orderId:N}")).AsSpan(0, 16));
    internal static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    internal static string SourceSnapshot(SalesOrder order) => JsonSerializer.Serialize(new
    {
        order.Id, order.TenantId, order.BusinessPartnerId, order.DocumentNumber, order.OrderType, order.Currency,
        ExchangeRate = Number(order.ExchangeRate), DiscountPercentage = Number(order.DiscountPercentage),
        SubTotal = Number(order.SubTotal), Discount = Number(order.DiscountAmount), Shipping = Number(order.ShippingAmount),
        Tax = Number(order.TaxAmount), Total = Number(order.TotalAmount), order.PaymentTermId, order.TaxGroupId, order.WarehouseId,
        Lines = order.Lines.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.TenantId, x.InventoryItemId, x.ProductId, x.Description, Quantity = Number(x.Quantity), Price = Number(x.UnitPrice),
            Discount = Number(x.DiscountAmount), DiscountPercentage = Number(x.DiscountPercentage), Tax = Number(x.TaxAmount),
            x.GLAccountId, x.TaxGroupId, x.TaxCode, x.Unit, x.WarehouseId, x.LocationId, x.LotNumber, x.SerialNumber, x.ExpirationDate
        })
    });
    internal static string Snapshot(Invoice invoice) => JsonSerializer.Serialize(new
    {
        Economics = Inventory.InventoryDisposalAuctionInvoiceGuard.Snapshot(invoice),
        Stock = invoice.LineItems.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new
        { x.Id, x.InventoryItemId, x.WarehouseId, x.LocationId, x.LotNumber, x.SerialNumber, x.ExpirationDate, x.Unit, x.TaxCode })
    });
}
