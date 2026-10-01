using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Sales;

internal sealed class SalesOrderInvoiceService(ApplicationDbContext db, ICurrentUserProvider actor,
    IInvoiceService invoices, IInventoryTrackingControlService? tracking = null,
    IPropertyEnquiryDepositApplicationService? prospectDeposits = null) : ISalesOrderInvoiceService
{
    public async Task<SalesOrderInvoiceDetailDto> GenerateAsync(Guid orderId, GenerateSalesOrderInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await RequireAsync(cancellationToken, FinancePermissions.CreateArInvoices, FinancePermissions.MaintainArInvoices);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100 || request.InvoiceDate == default)
            throw new InvalidOperationException("Invoice date and a valid generation request key are required.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            if (db.Database.IsSqlServer())
            {
                var resource = $"RHEMA:SALES:INVOICE:{actor.TenantId:N}:{orderId:N}";
                await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @r int; EXEC @r=sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
IF @r<0 THROW 51000, 'The Sales order is busy. Retry the same invoice request.', 1;", cancellationToken);
            }
            var order = await LoadAsync(orderId, cancellationToken);
            if (order.InvoiceId.HasValue)
            {
                if (order.InvoiceGenerationKey != request.IdempotencyKey || order.InvoiceGenerationHash != hash || order.InvoiceGeneratedById != actor.UserId)
                    throw new InvalidOperationException("This order already has an invoice. Open its existing invoice; do not generate a second one.");
                await transaction.CommitAsync(cancellationToken);
                return await DetailAsync(order, cancellationToken);
            }
            if (Convert.ToBase64String(order.RowVersion) != request.RowVersion)
                throw new InvalidOperationException("The Sales order changed. Reload it before generating the invoice.");
            SalesOrderInvoiceGuard.RequireEligible(order, allowLinked: false);
            var sourceLines = order.Lines.Where(x => !x.IsDeleted).OrderBy(x => x.LineNumber).ToList();
            if (request.Lines.Count != sourceLines.Count || request.Lines.Select(x => x.SalesOrderLineId).Distinct().Count() != sourceLines.Count)
                throw new InvalidOperationException("Provide the accounting and tax selection for every source order line exactly once.");
            var lines = new List<InvoiceLineItemCreateDto>();
            foreach (var line in sourceLines)
            {
                var input = request.Lines.SingleOrDefault(x => x.SalesOrderLineId == line.Id)
                    ?? throw new InvalidOperationException("An invoice selection does not belong to this order.");
                var item = line.InventoryItemId.HasValue ? await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == line.InventoryItemId && x.TenantId == actor.TenantId && !x.IsDeleted, cancellationToken) : null;
                var percentageDiscount = decimal.Round(line.Quantity * line.UnitPrice * line.DiscountPercentage / 100m, 2, MidpointRounding.AwayFromZero);
                if (percentageDiscount != line.DiscountAmount)
                    throw new InvalidOperationException($"Line {line.LineNumber} has inconsistent retained trade discounts. Correct and reapprove its source order first.");
                lines.Add(new InvoiceLineItemCreateDto
                {
                    Id = line.Id, LineItemType = line.InventoryItemId.HasValue ? "Inventory" : line.ProductId.HasValue ? "Product" : "GLAccount",
                    InventoryItemId = line.InventoryItemId, ProductId = line.ProductId,
                    GLAccountId = line.GLAccountId ?? input.GLAccountId ?? item?.SalesAccountId,
                    Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice,
                    DiscountPercentage = line.DiscountPercentage, Unit = line.Unit, TaxCode = line.TaxCode,
                    TaxGroupId = line.TaxGroupId ?? order.TaxGroupId ?? input.TaxGroupId,
                    TaxTreatment = input.TaxTreatment, WarehouseId = line.InventoryItemId.HasValue ? line.WarehouseId ?? order.WarehouseId : null,
                    LocationId = line.LocationId, LotNumber = line.LotNumber, SerialNumber = line.SerialNumber, ExpirationDate = line.ExpirationDate
                });
            }
            if (order.ShippingAmount < 0) throw new InvalidOperationException("The source shipping amount cannot be negative.");
            foreach (var stock in lines.Where(x => x.InventoryItemId.HasValue))
            {
                if (tracking is null) throw new InvalidOperationException("Inventory tracking controls are not configured for Sales invoicing.");
                var requirements = await tracking.GetRequirementsAsync(stock.InventoryItemId!.Value, cancellationToken);
                if (requirements.RequiresBatch || requirements.RequiresManufactureDate)
                    throw new InvalidOperationException("This item's category requires batch or manufacture-date evidence that Sales order lines do not yet retain. Use a supported source before invoicing.");
                await tracking.ValidateAsync(new InventoryTrackingMutationRequest
                {
                    InventoryItemId = stock.InventoryItemId.Value, WarehouseId = stock.WarehouseId ?? Guid.Empty,
                    LocationId = stock.LocationId, Direction = InventoryTrackingDirection.Issue, Quantity = stock.Quantity,
                    ReferenceType = "SalesOrder", ReferenceId = order.Id, ReferenceLineId = stock.Id,
                    ReferenceNumber = order.DocumentNumber, EventKey = $"SALES:INVOICE:PREFLIGHT:{order.Id:N}:{stock.Id:N}",
                    LotNumber = stock.LotNumber, SerialNumber = stock.SerialNumber, ExpiryDate = stock.ExpirationDate
                }, cancellationToken);
            }
            if (order.ShippingAmount > 0)
            {
                if (!request.FreightAccountId.HasValue || !request.FreightTaxTreatment.HasValue)
                    throw new InvalidOperationException("Select the freight revenue account and applicable tax treatment.");
                lines.Add(new InvoiceLineItemCreateDto
                {
                    Id = SalesOrderInvoiceGuard.FreightLineId(order.Id), LineItemType = "GLAccount", Description = "Freight / Shipping",
                    Quantity = 1, UnitPrice = order.ShippingAmount, GLAccountId = request.FreightAccountId,
                    TaxGroupId = request.FreightTaxGroupId, TaxTreatment = request.FreightTaxTreatment.Value
                });
            }
            // The order's quotation rate remains immutable source evidence. Finance validates
            // the separate approved invoice-date rate; a later invoice need not reuse the quote.
            var invoiceRate = request.ExchangeRateId.HasValue
                ? await db.Set<ExchangeRate>().AsNoTracking().Where(value => value.TenantId == actor.TenantId &&
                    value.Id == request.ExchangeRateId.Value && !value.IsDeleted)
                    .Select(value => (decimal?)value.InverseRate).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The selected invoice exchange rate was not found in the current tenant.")
                : 1m;
            var invoice = await invoices.CreateAsync(new InvoiceCreateDto
            {
                BusinessPartnerId = order.BusinessPartnerId, BusinessPartnerRoleId = request.BusinessPartnerRoleId,
                InvoiceDate = request.InvoiceDate.Date, DueDate = request.DueDate?.Date, Reference = order.DocumentNumber,
                CurrencyCode = order.Currency, ExchangeRate = invoiceRate, ExchangeRateId = request.ExchangeRateId,
                PaymentTermId = order.PaymentTermId, DiscountAmount = order.DiscountAmount, TaxGroupId = order.TaxGroupId,
                Notes = $"Sales order {order.DocumentNumber}", LineItems = lines, FinanceDimensions = request.FinanceDimensions
            }, SalesOrderInvoiceGuard.Producer, cancellationToken);
            if (invoice.TotalAmount != order.TotalAmount || invoice.TaxAmount != order.TaxAmount ||
                invoice.SubTotal != order.SubTotal + order.ShippingAmount)
                throw new InvalidOperationException("The canonical invoice tax/discount calculation differs from the approved Sales order totals. Correct and reapprove the source tax or discount facts before invoicing.");
            var persisted = await db.Invoices.Include(x => x.LineItems).SingleAsync(x => x.Id == invoice.Id && x.TenantId == actor.TenantId, cancellationToken);
            order.InvoiceId = invoice.Id; order.InvoiceGeneratedById = actor.UserId;
            order.InvoiceGenerationKey = request.IdempotencyKey; order.InvoiceGenerationHash = hash;
            order.InvoiceSourceJson = SalesOrderInvoiceGuard.SourceSnapshot(order);
            order.InvoiceEconomicsJson = SalesOrderInvoiceGuard.Snapshot(persisted);
            order.UpdatedAt = DateTime.UtcNow;
            db.SalesOrderStatusHistories.Add(new SalesOrderStatusHistory
            {
                SalesOrderId = order.Id, TenantId = order.TenantId, FromStatus = order.OrderStatus, ToStatus = order.OrderStatus,
                ChangedById = actor.UserId, ChangedAt = DateTime.UtcNow, Notes = $"Generated customer invoice {invoice.InvoiceNumber}"
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await DetailAsync(order, cancellationToken);
        });
    }

    public async Task<SalesOrderInvoiceDetailDto> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await RequireAsync(cancellationToken, FinancePermissions.ViewFinance, FinancePermissions.ManageArInvoices,
            FinancePermissions.CreateArInvoices, FinancePermissions.MaintainArInvoices, FinancePermissions.SendArInvoices, FinancePermissions.ApprovePostArInvoices);
        return await DetailAsync(await LoadAsync(orderId, cancellationToken), cancellationToken);
    }

    public async Task<SalesOrderInvoiceDetailDto> SubmitAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await RequireAsync(cancellationToken, FinancePermissions.SendArInvoices);
        var order = await LoadAsync(orderId, cancellationToken);
        await invoices.SubmitAsync(RequireInvoice(order), SalesOrderInvoiceGuard.Producer, cancellationToken);
        return await DetailAsync(order, cancellationToken);
    }

    public async Task<SalesOrderInvoiceDetailDto> PostAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await RequireAsync(cancellationToken, FinancePermissions.ApprovePostArInvoices);
        var order = await LoadAsync(orderId, cancellationToken);
        var invoiceId = RequireInvoice(order);
        await invoices.PostAsync(invoiceId, SalesOrderInvoiceGuard.Producer, cancellationToken);
        if (prospectDeposits is not null)
            await prospectDeposits.ApplyToPostedSalesInvoiceAsync(order.Id, invoiceId, cancellationToken);
        return await DetailAsync(order, cancellationToken);
    }

    public async Task<InvoiceDistributionDto> GetDistributionAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await GetAsync(orderId, cancellationToken);
        var order = await LoadAsync(orderId, cancellationToken);
        return await invoices.GetDistributionPreviewAsync(RequireInvoice(order), SalesOrderInvoiceGuard.Producer, cancellationToken);
    }

    private async Task<SalesOrder> LoadAsync(Guid id, CancellationToken ct) => await db.SalesOrders.Include(x => x.Lines)
        .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == actor.TenantId && !x.IsDeleted, ct)
        ?? throw new KeyNotFoundException("Sales order not found in the current tenant.");
    private static Guid RequireInvoice(SalesOrder order) => order.InvoiceId ?? throw new InvalidOperationException("Generate this order's invoice first.");
    private async Task<SalesOrderInvoiceDetailDto> DetailAsync(SalesOrder order, CancellationToken ct)
    {
        var invoice = await invoices.GetByIdAsync(RequireInvoice(order), SalesOrderInvoiceGuard.Producer, ct)
            ?? throw new KeyNotFoundException("The retained invoice was not found.");
        return new SalesOrderInvoiceDetailDto
        {
            SalesOrderId = order.Id, SalesOrderNumber = order.DocumentNumber, Invoice = invoice,
            CanSubmit = invoice.Status is "Draft" or "Rejected" && await HasPermissionAsync(ct, FinancePermissions.SendArInvoices),
            CanPost = invoice.Status is "Approved" or "ReadyToPost" && await HasPermissionAsync(ct, FinancePermissions.ApprovePostArInvoices)
        };
    }
    private async Task RequireAsync(CancellationToken ct, params string[] permissions)
    {
        if (!await HasPermissionAsync(ct, permissions)) throw new UnauthorizedAccessException("Your current tenant permissions do not allow this Sales invoice action.");
    }
    private async Task<bool> HasPermissionAsync(CancellationToken ct, params string[] permissions)
    {
        var now = DateTime.UtcNow;
        if (!actor.IsAuthenticated || actor.IsExternalUser || actor.UserId == Guid.Empty || actor.TenantId == Guid.Empty ||
            !await db.UserTenants.AnyAsync(x => x.TenantId == actor.TenantId && x.UserId == actor.UserId && !x.IsDeleted &&
                x.Status == UserTenantStatus.Active && (!x.ExpiresAt.HasValue || x.ExpiresAt > now) && x.User.IsActive, ct)) return false;
        if (actor.HasRole("SuperAdmin") || actor.HasRole("TenantAdmin")) return true;
        return await db.UserRoles.Where(x => x.UserId == actor.UserId).SelectMany(x => x.Role.RolePermissions)
            .AnyAsync(x => permissions.Contains(x.Permission.Name), ct);
    }
}
