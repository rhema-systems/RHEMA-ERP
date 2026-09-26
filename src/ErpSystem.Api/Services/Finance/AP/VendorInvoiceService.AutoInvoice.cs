using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private sealed record AvailableReceiptLine(ProcurementAcceptedReceiptLineDto Source, decimal Invoiced, decimal Available);
    private static readonly string[] ReceiptInvoiceOrderStatuses =
        ["Approved", "Sent", "Acknowledged", "PartiallyReceived", "Partially Received", "Received", "Completed", "Closed"];

    public async Task<IReadOnlyList<ProcurementInvoiceReceiptDto>> GetAutoInvoiceReceiptsAsync(Guid businessPartnerId, CancellationToken cancellationToken = default)
    {
        await RequireAutoInvoicePartnerAsync(businessPartnerId, cancellationToken);
        var orders = await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(p => p.TenantId == TenantId && !p.IsDeleted &&
            p.BusinessPartnerId == businessPartnerId && p.ProcurementCategory == ProcurementCategoryClass.Goods &&
            !p.CancelledAtUtc.HasValue && ReceiptInvoiceOrderStatuses.Contains(p.Status))
            .Include(p => p.Items).AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<ProcurementInvoiceReceiptDto>();
        foreach (var order in orders.OrderBy(p => p.OrderDate).ThenBy(p => p.Id))
        {
            IReadOnlyList<AvailableReceiptLine> lines;
            try { lines = await AvailableReceiptLinesAsync(order.Id, null, cancellationToken); }
            catch (ProcurementAcceptedSupplyValidationException e) when (e.Code == "ACCEPTED_GOODS_RECEIPT_MISSING") { continue; }
            var receiptIds = lines.Where(l => l.Available > 0 && l.Source.GoodsReceiptNoteId.HasValue)
                .Select(l => l.Source.GoodsReceiptNoteId!.Value).Distinct().ToArray();
            var receipts = await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(g =>
                g.TenantId == TenantId && !g.IsDeleted && receiptIds.Contains(g.Id) && g.StockUpdated && g.Status != GRNStatus.Cancelled)
                .AsNoTracking().ToListAsync(cancellationToken);
            foreach (var receipt in receipts)
            {
                if (!await CanReadReceiptAsync(receipt, cancellationToken)) continue;
                var selected = lines.Where(l => l.Available > 0 && l.Source.GoodsReceiptNoteId == receipt.Id && l.Source.GoodsReceiptNoteItemId.HasValue).ToList();
                result.Add(new ProcurementInvoiceReceiptDto
                {
                    GoodsReceiptNoteId = receipt.Id, ReceiptNumber = receipt.GRNNumber, ReceiptDate = receipt.ReceiptDate,
                    PurchaseOrderId = order.Id, OrderNumber = order.OrderNumber, CurrencyCode = order.Currency, WarehouseId = receipt.WarehouseId,
                    Lines = selected.Select(l => new ProcurementInvoiceReceiptLineDto
                    {
                        GoodsReceiptNoteItemId = l.Source.GoodsReceiptNoteItemId!.Value, PurchaseOrderItemId = l.Source.PurchaseOrderItemId,
                        Description = order.Items.Single(p => p.Id == l.Source.PurchaseOrderItemId).ItemDescription ?? "Goods",
                        Unit = order.Items.Single(p => p.Id == l.Source.PurchaseOrderItemId).UnitOfMeasure,
                        AcceptedQuantity = l.Source.AcceptedQuantity, ReturnedQuantity = l.Source.ReturnedQuantity,
                        InvoicedQuantity = l.Invoiced, AvailableQuantity = l.Available, UnitPrice = l.Source.UnitPrice
                    }).ToList()
                });
            }
        }
        return result;
    }

    private async Task<BusinessPartner> RequireAutoInvoicePartnerAsync(Guid id, CancellationToken token)
    {
        var partner = await _unitOfWork.Repository<BusinessPartner>().GetQueryable(p =>
            p.TenantId == TenantId && p.Id == id && !p.IsDeleted).AsNoTracking().SingleOrDefaultAsync(token)
            ?? throw new InvalidOperationException("Select a supplier Business Partner in this company.");
        if (!BusinessPartnerLifecyclePolicy.IsOperationallyApproved(partner) || !BusinessPartnerRoles.HasSupplier(partner.PartnerType))
            throw new InvalidOperationException("Auto Invoice requires an approved, active supplier Business Partner.");
        return partner;
    }

    private async Task<bool> CanReadReceiptAsync(GoodsReceiptNote receipt, CancellationToken token)
    {
        if (_receiptAccess == null) throw new InvalidOperationException("Procurement receipt authorization is not configured.");
        return (await _receiptAccess.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = "procurement.inventory.read", WarehouseId = receipt.WarehouseId,
            SourceType = "GoodsReceiptNote", SourceReference = receipt.GRNNumber
        }, "auto-invoice:receipts", token)).Allowed;
    }

    private async Task<IReadOnlyList<AvailableReceiptLine>> AvailableReceiptLinesAsync(Guid orderId, Guid? excludeInvoiceId, CancellationToken token)
    {
        var sources = await RequireAcceptedSupplyServiceAsync().GetGoodsReceiptLinesAsync(orderId, token);
        var allocations = await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().GetQueryable(a =>
            a.TenantId == TenantId && a.PurchaseOrderId == orderId && !a.IsDeleted && !a.VendorInvoice.IsDeleted &&
            a.VendorInvoice.TenantId == TenantId && (!excludeInvoiceId.HasValue || a.VendorInvoiceId != excludeInvoiceId.Value) &&
            a.VendorInvoice.Status != VendorInvoiceStatus.Voided && !a.VendorInvoiceLineItem.IsDeleted)
            .AsNoTracking().ToListAsync(token);
        var totalByLine = await PriorInvoicedQuantitiesAsync(orderId, excludeInvoiceId, token);
        // Historical AP invoices have PO-line authority but no GRN allocation. Consume them
        // deterministically from oldest accepted receipts without rewriting their history.
        var legacy = totalByLine.ToDictionary(p => p.Key, p => Math.Max(0m, p.Value -
            allocations.Where(a => a.PurchaseOrderItemId == p.Key).Sum(a => a.Quantity)));
        var result = new List<AvailableReceiptLine>();
        foreach (var source in sources.OrderBy(s => s.ReceiptDate).ThenBy(s => s.PurchaseOrderReceiptId).ThenBy(s => s.PurchaseOrderReceiptItemId))
        {
            var assigned = allocations.Where(a => a.PurchaseOrderReceiptItemId == source.PurchaseOrderReceiptItemId).Sum(a => a.Quantity);
            var available = Math.Max(0m, source.NetAcceptedQuantity - assigned);
            var old = Math.Min(available, legacy.GetValueOrDefault(source.PurchaseOrderItemId));
            legacy[source.PurchaseOrderItemId] = legacy.GetValueOrDefault(source.PurchaseOrderItemId) - old;
            result.Add(new(source, assigned + old, decimal.Floor(Math.Max(0m, available - old) * 10000m) / 10000m));
        }
        return result;
    }

    public Task<VendorInvoiceDto> CreateAutoInvoiceAsync(ProcurementAutoInvoiceRequestDto request,
        FinancePostingProducerContext producer, CancellationToken cancellationToken = default)
    {
        EnsureVendorInvoiceRoute(producer);
        if (request == null || request.RequestId == Guid.Empty || request.InvoiceDate == default || request.BusinessPartnerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber) || request.SupplierInvoiceNumber.Trim().Length > 100 || request.Lines == null ||
            request.Lines.Count is < 1 or > 500 || request.Lines.Any(l => l == null || l.GoodsReceiptNoteItemId == Guid.Empty || l.Quantity <= 0 || decimal.Round(l.Quantity, 4) != l.Quantity) ||
            request.Lines.Select(l => l.GoodsReceiptNoteItemId).Distinct().Count() != request.Lines.Count)
            throw new ArgumentException("Provide a request identity, supplier invoice reference/date and distinct receipt lines with positive quantities (up to four decimal places).");
        if (_unitOfWork.HasActiveTransaction) throw new InvalidOperationException("Start Auto Invoice outside another transaction.");
        var hash = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(new
        {
            request.BusinessPartnerId, Reference = request.SupplierInvoiceNumber.Trim(), Date = request.InvoiceDate.Date,
            request.ExchangeRateId, request.ExchangeRate,
            Lines = request.Lines.OrderBy(l => l.GoodsReceiptNoteItemId).Select(l => new { l.GoodsReceiptNoteItemId, l.Quantity })
        });
        return _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            _unitOfWork.ClearTrackedChanges();
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"auto-invoice:{TenantId:N}:{request.RequestId:N}", cancellationToken);
                var existing = await _unitOfWork.Repository<VendorInvoice>().GetQueryableIncludingDeleted(i =>
                    i.TenantId == TenantId && i.AutoInvoiceRequestId == request.RequestId).SingleOrDefaultAsync(cancellationToken);
                if (existing != null)
                {
                    if (existing.AutoInvoiceRequestHash != hash || existing.IsDeleted || existing.Status == VendorInvoiceStatus.Voided)
                        throw new InvalidOperationException("This Auto Invoice request was already used for a different or cancelled selection. Refresh and start a new request.");
                    var replay = await GetByIdAsync(existing.Id, producer, cancellationToken) ?? throw new InvalidOperationException("The generated invoice is unavailable.");
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return replay;
                }
                await RequireAutoInvoicePartnerAsync(request.BusinessPartnerId, cancellationToken);
                var itemIds = request.Lines.Select(l => l.GoodsReceiptNoteItemId).ToArray();
                var grnLines = await _unitOfWork.Repository<GoodsReceiptNoteItem>().GetQueryable(l =>
                    l.TenantId == TenantId && !l.IsDeleted && itemIds.Contains(l.Id))
                    .Include(l => l.GoodsReceiptNote).AsNoTracking().ToListAsync(cancellationToken);
                if (grnLines.Count != itemIds.Length || grnLines.Any(l => l.GoodsReceiptNote == null || l.GoodsReceiptNote.IsDeleted ||
                    l.GoodsReceiptNote.TenantId != TenantId || l.GoodsReceiptNote.SupplierId != request.BusinessPartnerId ||
                    !l.GoodsReceiptNote.PurchaseOrderId.HasValue || !l.GoodsReceiptNote.StockUpdated || l.GoodsReceiptNote.Status == GRNStatus.Cancelled))
                    throw new InvalidOperationException("All selected GRNs must be posted receipts for the selected supplier in this company.");
                var orderIds = grnLines.Select(l => l.GoodsReceiptNote.PurchaseOrderId!.Value).Distinct().OrderBy(id => id).ToArray();
                foreach (var id in orderIds) await _unitOfWork.AcquireTransactionLockAsync($"tdc-ap-match:{TenantId:N}:{id:N}", cancellationToken);
                foreach (var receipt in grnLines.Select(l => l.GoodsReceiptNote).DistinctBy(g => g.Id).OrderBy(g => g.Id))
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"supplier-return-source:{TenantId:N}:{receipt.Id:N}", cancellationToken);
                    if (!await CanReadReceiptAsync(receipt, cancellationToken)) throw new UnauthorizedAccessException("You do not have access to a selected receipt warehouse.");
                }
                var orders = await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(p => p.TenantId == TenantId && !p.IsDeleted && orderIds.Contains(p.Id))
                    .Include(p => p.Items).AsNoTracking().ToListAsync(cancellationToken);
                if (orders.Count != orderIds.Length || orders.Any(p => p.BusinessPartnerId != request.BusinessPartnerId ||
                    p.ProcurementCategory != ProcurementCategoryClass.Goods || p.CancelledAtUtc.HasValue || !ReceiptInvoiceOrderStatuses.Contains(p.Status)))
                    throw new InvalidOperationException("Selected receipts require current approved Goods purchase orders for one supplier.");
                if (orders.Select(p => p.Currency.ToUpperInvariant()).Distinct().Count() != 1)
                    throw new InvalidOperationException("Select GRNs in one currency per consolidated invoice.");
                var currency = orders[0].Currency;
                var exchange = await ResolveOpeningInvoiceExchangeRateAsync(currency, request.InvoiceDate, request.ExchangeRateId, request.ExchangeRate, cancellationToken);
                var available = new List<AvailableReceiptLine>();
                foreach (var order in orders) available.AddRange(await AvailableReceiptLinesAsync(order.Id, null, cancellationToken));
                var chosen = request.Lines.Select(input =>
                {
                    var source = available.SingleOrDefault(l => l.Source.GoodsReceiptNoteItemId == input.GoodsReceiptNoteItemId);
                    if (source == null || input.Quantity > source.Available)
                        throw new InvalidOperationException("A selected receipt quantity is no longer available. Refresh the eligible GRNs before generating the invoice.");
                    return (Input: input, Source: source.Source, LineId: Guid.NewGuid());
                }).ToList();
                var supplier = await ResolveSupplierIdentityForInvoiceAsync(request.BusinessPartnerId, false, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var invoice = await CreateCoreAsync(new VendorInvoiceCreateDto
                {
                    AutoInvoiceRequestId = request.RequestId, AutoInvoiceRequestHash = hash, SupplierId = supplier.Id,
                    SupplierInvoiceNumber = request.SupplierInvoiceNumber.Trim(), InvoiceDate = request.InvoiceDate.Date,
                    CurrencyCode = currency, ExchangeRate = exchange.Rate, ExchangeRateId = exchange.ExchangeRateId,
                    MatchingType = InvoiceMatchingType.ThreeWay,
                    LineItems = chosen.Select(row => new VendorInvoiceLineItemCreateDto
                    {
                        Id = row.LineId, PurchaseOrderItemId = row.Source.PurchaseOrderItemId,
                        Description = orders.SelectMany(p => p.Items).Single(l => l.Id == row.Source.PurchaseOrderItemId).ItemDescription ?? "Accepted goods",
                        Unit = orders.SelectMany(p => p.Items).Single(l => l.Id == row.Source.PurchaseOrderItemId).UnitOfMeasure,
                        Quantity = row.Input.Quantity, UnitPrice = row.Source.UnitPrice, LineItemType = "Product", TaxTreatment = TaxTreatment.PendingReview
                    }).ToList()
                }, producer, cancellationToken, deferSupplierWithholdingDecision: true);
                foreach (var row in chosen)
                    await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().AddAsync(new VendorInvoiceReceiptAllocation
                    {
                        TenantId = TenantId, VendorInvoiceId = invoice.Id, VendorInvoiceLineItemId = row.LineId,
                        PurchaseOrderId = row.Source.PurchaseOrderId, PurchaseOrderItemId = row.Source.PurchaseOrderItemId,
                        PurchaseOrderReceiptId = row.Source.PurchaseOrderReceiptId, PurchaseOrderReceiptItemId = row.Source.PurchaseOrderReceiptItemId,
                        InspectionCaseId = row.Source.InspectionCaseId, GoodsReceiptNoteId = row.Source.GoodsReceiptNoteId!.Value,
                        GoodsReceiptNoteItemId = row.Source.GoodsReceiptNoteItemId!.Value, Quantity = row.Input.Quantity,
                        CreatedById = CurrentUserId, CreatedBy = UserName
                    });
                await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
                {
                    TenantId = TenantId, UserId = CurrentUserId, Username = UserName, Action = "PROCUREMENT_AUTO_INVOICE_CREATED",
                    Resource = nameof(VendorInvoice), ResourceId = invoice.Id.ToString(), Timestamp = DateTime.UtcNow, IpAddress = "Unknown",
                    NewValues = JsonSerializer.Serialize(new { request.RequestId, request.BusinessPartnerId, invoice.Id, hash, request.Lines }),
                    CreatedById = CurrentUserId, CreatedBy = UserName
                });
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return invoice;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<VendorInvoiceReceiptLinkDto>> GetReceiptLinksAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await GetEntityOrThrowAsync(invoiceId, cancellationToken);
        return await _unitOfWork.Repository<VendorInvoiceReceiptAllocation>().GetQueryable(a => a.TenantId == TenantId &&
            a.VendorInvoiceId == invoiceId && !a.IsDeleted).OrderBy(a => a.GoodsReceiptNote.ReceiptDate).ThenBy(a => a.Id)
            .Select(a => new VendorInvoiceReceiptLinkDto
            {
                InvoiceLineId = a.VendorInvoiceLineItemId, GoodsReceiptNoteId = a.GoodsReceiptNoteId, ReceiptNumber = a.GoodsReceiptNote.GRNNumber,
                PurchaseOrderId = a.PurchaseOrderId, OrderNumber = a.PurchaseOrder.OrderNumber, InspectionCaseId = a.InspectionCaseId, Quantity = a.Quantity
            }).ToListAsync(cancellationToken);
    }
}
