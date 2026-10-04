using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class DeliveryService : IDeliveryService
{
    private readonly IGenericRepository<DeliveryNote> _deliveryRepo;
    private readonly IGenericRepository<DeliveryNoteLine> _lineRepo;
    private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
    private readonly IGenericRepository<SalesOrderLine> _soLineRepo;
    private readonly IGenericRepository<SalesOrderStatusHistory> _historyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<DeliveryService> _logger;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ICommercialQuantityPolicyValidator? _commercialQuantityValidator;

    public DeliveryService(
        IGenericRepository<DeliveryNote> deliveryRepo,
        IGenericRepository<DeliveryNoteLine> lineRepo,
        IGenericRepository<SalesOrder> salesOrderRepo,
        IGenericRepository<SalesOrderLine> soLineRepo,
        IGenericRepository<SalesOrderStatusHistory> historyRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<DeliveryService> logger,
        IDocumentNumberingService documentNumberingService,
        ICommercialQuantityPolicyValidator? commercialQuantityValidator = null)
    {
        _deliveryRepo = deliveryRepo;
        _lineRepo = lineRepo;
        _salesOrderRepo = salesOrderRepo;
        _soLineRepo = soLineRepo;
        _historyRepo = historyRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _documentNumberingService = documentNumberingService;
        _commercialQuantityValidator = commercialQuantityValidator;
    }

    #region CRUD

    public async Task<DeliveryNoteDetailDto> CreateDeliveryNoteAsync(CreateDeliveryNoteDto dto)
    {
        try
        {
            var so = await _salesOrderRepo.GetByIdAsync(dto.SalesOrderId, s => s.Lines, s => s.BusinessPartner)
                ?? throw new InvalidOperationException($"Sales Order {dto.SalesOrderId} not found");
            if (so.TenantId != _currentUserProvider.TenantId || so.IsDeleted)
                throw new UnauthorizedAccessException("The Sales order is outside the current tenant.");

            if (so.OrderStatus != SalesOrderStatus.Confirmed &&
                so.OrderStatus != SalesOrderStatus.PartiallyDelivered)
                throw new InvalidOperationException($"Cannot create Delivery Note for Sales Order in {so.OrderStatus} status");

            var deliveryNote = new DeliveryNote
            {
                DocumentNumber = await GenerateDeliveryNumberAsync(),
                DocumentDate = DateTime.UtcNow,
                SalesOrderId = dto.SalesOrderId,
                BusinessPartnerId = so.BusinessPartnerId,
                CustomerName = so.CustomerName,
                DeliveryStatus = DeliveryNoteStatus.Draft,
                ShipmentMethod = dto.ShipmentMethod,
                ShippingAddress = dto.ShippingAddress ?? so.ShippingAddress,
                CarrierName = dto.CarrierName,
                TrackingNumber = dto.TrackingNumber,
                WarehouseId = dto.WarehouseId ?? so.WarehouseId,
                DeliveryNotes = dto.DeliveryNotes,
                InternalNotes = dto.InternalNotes,
                ExternalNotes = dto.ExternalNotes,
                Currency = so.Currency,
                ExchangeRate = so.ExchangeRate,
                TaxGroupId = so.TaxGroupId,
                TenantId = so.TenantId,
                ReferenceNumber = so.DocumentNumber // Reference back to SO
            };

            await _deliveryRepo.AddAsync(deliveryNote);

            int lineNumber = 1;
            foreach (var lineDto in dto.Lines)
            {
                var soLine = so.Lines.FirstOrDefault(l => l.Id == lineDto.SalesOrderLineId)
                    ?? throw new InvalidOperationException($"Sales Order Line {lineDto.SalesOrderLineId} not found");
                if (so.InvoiceId.HasValue && ((lineDto.InventoryItemId.HasValue && lineDto.InventoryItemId != soLine.InventoryItemId) ||
                    (lineDto.WarehouseId.HasValue && lineDto.WarehouseId != (soLine.WarehouseId ?? so.WarehouseId)) ||
                    (lineDto.LocationId.HasValue && lineDto.LocationId != soLine.LocationId) ||
                    (lineDto.SerialNumber != null && lineDto.SerialNumber != soLine.SerialNumber) ||
                    (lineDto.LotNumber != null && lineDto.LotNumber != soLine.LotNumber)))
                    throw new InvalidOperationException("Delivery tracking must match the stock source retained by the Sales invoice.");

                if (lineDto.DispatchedQuantity > soLine.RemainingQuantity)
                    throw new InvalidOperationException(
                        $"Dispatched quantity ({lineDto.DispatchedQuantity}) exceeds remaining quantity ({soLine.RemainingQuantity}) for line {soLine.Description}");

                var line = new DeliveryNoteLine
                {
                    DeliveryNoteId = deliveryNote.Id,
                    SalesOrderLineId = lineDto.SalesOrderLineId,
                    LineNumber = lineNumber++,
                    InventoryItemId = lineDto.InventoryItemId ?? soLine.InventoryItemId,
                    Description = soLine.Description,
                    ProductCode = soLine.ProductCode,
                    DispatchedQuantity = lineDto.DispatchedQuantity,
                    Unit = soLine.Unit,
                    UnitOfMeasureId = soLine.UnitOfMeasureId,
                    UnitOfMeasureCodeSnapshot = soLine.UnitOfMeasureCodeSnapshot,
                    UnitOfMeasureDecimalPlacesSnapshot = soLine.UnitOfMeasureDecimalPlacesSnapshot,
                    UnitOfMeasureRoundingIncrementSnapshot = soLine.UnitOfMeasureRoundingIncrementSnapshot,
                    WarehouseId = lineDto.WarehouseId ?? soLine.WarehouseId,
                    LocationId = lineDto.LocationId ?? soLine.LocationId,
                    SerialNumber = lineDto.SerialNumber ?? soLine.SerialNumber,
                    LotNumber = lineDto.LotNumber ?? soLine.LotNumber,
                    Notes = lineDto.Notes,
                    TenantId = so.TenantId,
                    UnitPrice = soLine.UnitPrice,
                    DiscountPercentage = soLine.DiscountPercentage,
                    DiscountAmount = soLine.DiscountAmount,
                    TaxRate = soLine.TaxRate,
                    TaxAmount = soLine.TaxAmount,
                    TaxGroupId = soLine.TaxGroupId
                };

                await ValidateLineQuantityAsync(line, line.DispatchedQuantity, "Delivery create");

                await _lineRepo.AddAsync(line);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created Delivery Note {DeliveryNumber} for Sales Order {OrderNumber}",
                deliveryNote.DocumentNumber, so.DocumentNumber);

            return await GetDeliveryNoteByIdAsync(deliveryNote.Id)
                ?? throw new InvalidOperationException("Failed to retrieve created Delivery Note");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Delivery Note for Sales Order {SalesOrderId}", dto.SalesOrderId);
            throw;
        }
    }

    public async Task<DeliveryNoteDetailDto?> GetDeliveryNoteByIdAsync(Guid id)
    {
        try
        {
            var dn = await _deliveryRepo.GetByIdAsync(id,
                d => d.SalesOrder,
                d => d.BusinessPartner,
                d => d.Lines);

            if (dn == null) return null;
            return MapToDetailDto(dn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Delivery Note {DeliveryNoteId}", id);
            throw;
        }
    }

    public async Task<PagedResult<DeliveryNoteSummaryDto>> GetDeliveryNotesAsync(
        int page, int pageSize,
        string? search = null,
        DeliveryNoteStatus? status = null,
        Guid? salesOrderId = null,
        Guid? customerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = _deliveryRepo.GetQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(d => d.DocumentNumber.Contains(search) || d.CustomerName.Contains(search));
            if (status.HasValue)
                query = query.Where(d => d.DeliveryStatus == status.Value);
            if (salesOrderId.HasValue)
                query = query.Where(d => d.SalesOrderId == salesOrderId.Value);
            if (customerId.HasValue)
                query = query.Where(d => d.BusinessPartnerId == customerId.Value);
            if (startDate.HasValue)
                query = query.Where(d => d.DocumentDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(d => d.DocumentDate <= endDate.Value);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(d => d.SalesOrder)
                .Include(d => d.Lines)
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<DeliveryNoteSummaryDto>
            {
                Items = items.Select(MapToSummaryDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Delivery Notes");
            throw;
        }
    }

    #endregion

    #region Lifecycle Actions

    public async Task<DeliveryNoteDetailDto> MarkAsPackedAsync(Guid id)
    {
        try
        {
            var dn = await _deliveryRepo.GetByIdAsync(id, delivery => delivery.Lines)
                ?? throw new InvalidOperationException($"Delivery Note {id} not found");

            if (dn.DeliveryStatus != DeliveryNoteStatus.Draft)
                throw new InvalidOperationException($"Cannot pack Delivery Note in {dn.DeliveryStatus} status");

            dn.DeliveryStatus = DeliveryNoteStatus.Packed;
            dn.PackedById = _currentUserProvider.UserId;

            await _deliveryRepo.UpdateAsync(dn);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Delivery Note {DeliveryNumber} marked as packed", dn.DocumentNumber);
            return await GetDeliveryNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking Delivery Note {DeliveryNoteId} as packed", id);
            throw;
        }
    }

    public async Task<DeliveryNoteDetailDto> MarkAsShippedAsync(Guid id, string? carrierName = null, string? trackingNumber = null)
    {
        try
        {
            var dn = await _deliveryRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Delivery Note {id} not found");

            if (dn.DeliveryStatus != DeliveryNoteStatus.Packed && dn.DeliveryStatus != DeliveryNoteStatus.Draft)
                throw new InvalidOperationException($"Cannot ship Delivery Note in {dn.DeliveryStatus} status");

            foreach (var line in dn.Lines.Where(line => !line.IsDeleted))
                await ValidateLineQuantityAsync(line, line.DispatchedQuantity, "Delivery ship");

            dn.DeliveryStatus = DeliveryNoteStatus.Shipped;
            dn.ShippedDate = DateTime.UtcNow;
            dn.ShippedById = _currentUserProvider.UserId;
            if (carrierName != null) dn.CarrierName = carrierName;
            if (trackingNumber != null) dn.TrackingNumber = trackingNumber;

            await _deliveryRepo.UpdateAsync(dn);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Delivery Note {DeliveryNumber} shipped via {Carrier}", dn.DocumentNumber, dn.CarrierName);
            return await GetDeliveryNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking Delivery Note {DeliveryNoteId} as shipped", id);
            throw;
        }
    }

    public async Task<DeliveryNoteDetailDto> ConfirmDeliveryAsync(Guid id, ConfirmDeliveryDto dto)
    {
        try
        {
            var dn = await _deliveryRepo.GetByIdAsync(id, d => d.Lines)
                ?? throw new InvalidOperationException($"Delivery Note {id} not found");

            if (dn.TenantId != _currentUserProvider.TenantId || dn.IsDeleted)
                throw new UnauthorizedAccessException("The delivery is outside the current tenant.");
            var invoiceOwnsStock = await _salesOrderRepo.GetQueryable(x => x.Id == dn.SalesOrderId &&
                x.TenantId == dn.TenantId && !x.IsDeleted && x.InvoiceId.HasValue && x.Invoice != null &&
                x.Invoice.TenantId == dn.TenantId && x.Invoice.JournalEntryId.HasValue && !x.Invoice.IsDeleted &&
                x.Invoice.Status != ErpSystem.Core.Entities.Finance.InvoiceStatus.Cancelled).AnyAsync();

            if (dn.DeliveryStatus != DeliveryNoteStatus.Shipped &&
                dn.DeliveryStatus != DeliveryNoteStatus.Packed &&
                dn.DeliveryStatus != DeliveryNoteStatus.Draft)
                throw new InvalidOperationException($"Cannot confirm delivery for Delivery Note in {dn.DeliveryStatus} status");

            dn.DeliveredDate = DateTime.UtcNow;
            dn.ReceivedById = _currentUserProvider.UserId;
            dn.ReceiverName = dto.ReceiverName;
            dn.ReceiverSignaturePath = dto.ReceiverSignaturePath;
            if (dto.DeliveryNotes != null) dn.DeliveryNotes = dto.DeliveryNotes;

            bool allFullyDelivered = true;

            // Process each line
            if (dto.Lines != null && dto.Lines.Any())
            {
                foreach (var lineConfirmation in dto.Lines)
                {
                    var line = dn.Lines.FirstOrDefault(l => l.Id == lineConfirmation.DeliveryNoteLineId)
                        ?? throw new InvalidOperationException($"Delivery Note Line {lineConfirmation.DeliveryNoteLineId} not found");

                    line.DeliveredQuantity = lineConfirmation.DeliveredQuantity;
                    line.DamagedQuantity = lineConfirmation.DamagedQuantity ?? 0;
                    line.DamageNotes = lineConfirmation.DamageNotes;
                    line.IsStockDeducted = invoiceOwnsStock;

                    await ValidateLineQuantityAsync(line, line.DeliveredQuantity, "Delivery confirm delivered");
                    await ValidateLineQuantityAsync(line, line.DamagedQuantity, "Delivery confirm damaged");

                    await _lineRepo.UpdateAsync(line);

                    // Update SO line delivered quantity
                    var soLine = await _soLineRepo.GetByIdAsync(line.SalesOrderLineId);
                    if (soLine != null)
                    {
                        soLine.DeliveredQuantity += lineConfirmation.DeliveredQuantity;
                        if (soLine.DeliveredQuantity < soLine.Quantity)
                            allFullyDelivered = false;
                        await _soLineRepo.UpdateAsync(soLine);
                    }
                }
            }
            else
            {
                // If no per-line confirmation, mark all lines as fully delivered
                foreach (var line in dn.Lines)
                {
                    line.DeliveredQuantity = line.DispatchedQuantity;
                    line.IsStockDeducted = invoiceOwnsStock;
                    await ValidateLineQuantityAsync(line, line.DeliveredQuantity, "Delivery confirm delivered");
                    await _lineRepo.UpdateAsync(line);

                    var soLine = await _soLineRepo.GetByIdAsync(line.SalesOrderLineId);
                    if (soLine != null)
                    {
                        soLine.DeliveredQuantity += line.DispatchedQuantity;
                        if (soLine.DeliveredQuantity < soLine.Quantity)
                            allFullyDelivered = false;
                        await _soLineRepo.UpdateAsync(soLine);
                    }
                }
            }

            dn.DeliveryStatus = DeliveryNoteStatus.Delivered;
            await _deliveryRepo.UpdateAsync(dn);

            // Update Sales Order status
            var so = await _salesOrderRepo.GetByIdAsync(dn.SalesOrderId);
            if (so != null)
            {
                var previousStatus = so.OrderStatus;
                so.OrderStatus = allFullyDelivered ? SalesOrderStatus.Delivered : SalesOrderStatus.PartiallyDelivered;
                so.ActualDeliveryDate = DateTime.UtcNow;
                await _salesOrderRepo.UpdateAsync(so);

                await _historyRepo.AddAsync(new SalesOrderStatusHistory
                {
                    SalesOrderId = so.Id,
                    FromStatus = previousStatus,
                    ToStatus = so.OrderStatus,
                    ChangedById = _currentUserProvider.UserId,
                    ChangedAt = DateTime.UtcNow,
                    Notes = $"Delivery {dn.DocumentNumber} confirmed",
                    TenantId = so.TenantId
                });
            }

            // Canonical Sales invoice posting owns the sole inventory issue. Delivery records fulfillment only.

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Delivery Note {DeliveryNumber} confirmed — receiver: {Receiver}",
                dn.DocumentNumber, dto.ReceiverName);

            return await GetDeliveryNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming delivery for Delivery Note {DeliveryNoteId}", id);
            throw;
        }
    }

    public async Task<DeliveryNoteDetailDto> CancelDeliveryNoteAsync(Guid id, string? reason = null)
    {
        try
        {
            var dn = await _deliveryRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Delivery Note {id} not found");

            if (dn.DeliveryStatus == DeliveryNoteStatus.Delivered)
                throw new InvalidOperationException("Cannot cancel a delivered Delivery Note");

            dn.DeliveryStatus = DeliveryNoteStatus.Cancelled;
            await _deliveryRepo.UpdateAsync(dn);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Delivery Note {DeliveryNumber} cancelled: {Reason}", dn.DocumentNumber, reason);
            return await GetDeliveryNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Delivery Note {DeliveryNoteId}", id);
            throw;
        }
    }

    #endregion

    #region Utilities

    public async Task<string> GenerateDeliveryNumberAsync()
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Sales,
            SalesDocumentTypes.DeliveryNote,
            _currentUserProvider.TenantId,
            DateTime.UtcNow,
            nameof(DeliveryNote));
    }

    public async Task<List<SalesOrderLineDto>> GetDeliverableLinesAsync(Guid salesOrderId)
    {
        try
        {
            var lines = await _soLineRepo.FindAsync(l => l.SalesOrderId == salesOrderId);
            return lines
                .Where(l => l.RemainingQuantity > 0)
                .Select(l => new SalesOrderLineDto
                {
                    Id = l.Id,
                    SalesOrderId = l.SalesOrderId,
                    LineNumber = l.LineNumber,
                    Description = l.Description,
                    ProductCode = l.ProductCode,
                    Quantity = l.Quantity,
                    DeliveredQuantity = l.DeliveredQuantity,
                    RemainingQuantity = l.RemainingQuantity,
                    UnitPrice = l.UnitPrice,
                    Unit = l.Unit,
                    WarehouseId = l.WarehouseId,
                    LocationId = l.LocationId
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deliverable lines for SO {SalesOrderId}", salesOrderId);
            throw;
        }
    }

    #endregion

    #region Private Helpers

    private DeliveryNoteSummaryDto MapToSummaryDto(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        DocumentNumber = dn.DocumentNumber,
        DocumentDate = dn.DocumentDate,
        DeliveryStatus = dn.DeliveryStatus,
        SalesOrderId = dn.SalesOrderId,
        SalesOrderNumber = dn.SalesOrder?.DocumentNumber ?? string.Empty,
        BusinessPartnerId = dn.BusinessPartnerId,
        CustomerName = dn.CustomerName,
        ShipmentMethod = dn.ShipmentMethod,
        ShippedDate = dn.ShippedDate,
        DeliveredDate = dn.DeliveredDate,
        CarrierName = dn.CarrierName,
        TrackingNumber = dn.TrackingNumber,
        LineCount = dn.Lines?.Count ?? 0
    };

    private DeliveryNoteDetailDto MapToDetailDto(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        DocumentNumber = dn.DocumentNumber,
        DocumentDate = dn.DocumentDate,
        DeliveryStatus = dn.DeliveryStatus,
        SalesOrderId = dn.SalesOrderId,
        SalesOrderNumber = dn.SalesOrder?.DocumentNumber ?? string.Empty,
        BusinessPartnerId = dn.BusinessPartnerId,
        CustomerName = dn.CustomerName,
        ShipmentMethod = dn.ShipmentMethod,
        ShippedDate = dn.ShippedDate,
        DeliveredDate = dn.DeliveredDate,
        CarrierName = dn.CarrierName,
        TrackingNumber = dn.TrackingNumber,
        ShippingAddress = dn.ShippingAddress,
        WarehouseId = dn.WarehouseId,
        ReceiverName = dn.ReceiverName,
        ReceiverSignaturePath = dn.ReceiverSignaturePath,
        DeliveryNotes = dn.DeliveryNotes,
        InternalNotes = dn.InternalNotes,
        ExternalNotes = dn.ExternalNotes,
        TotalAmount = dn.TotalAmount,
        Currency = dn.Currency,
        ExchangeRate = dn.ExchangeRate,
        TaxGroupId = dn.TaxGroupId,
        LineCount = dn.Lines?.Count ?? 0,
        Lines = dn.Lines?.Select(l => new DeliveryNoteLineDto
        {
            Id = l.Id,
            DeliveryNoteId = l.DeliveryNoteId,
            SalesOrderLineId = l.SalesOrderLineId,
            LineNumber = l.LineNumber,
            InventoryItemId = l.InventoryItemId,
            Description = l.Description,
            ProductCode = l.ProductCode,
            DispatchedQuantity = l.DispatchedQuantity,
            DeliveredQuantity = l.DeliveredQuantity,
            DamagedQuantity = l.DamagedQuantity,
            Unit = l.Unit,
            UnitOfMeasureId = l.UnitOfMeasureId,
            UnitOfMeasureCodeSnapshot = l.UnitOfMeasureCodeSnapshot,
            UnitOfMeasureDecimalPlacesSnapshot = l.UnitOfMeasureDecimalPlacesSnapshot,
            UnitOfMeasureRoundingIncrementSnapshot = l.UnitOfMeasureRoundingIncrementSnapshot,
            WarehouseId = l.WarehouseId,
            LocationId = l.LocationId,
            SerialNumber = l.SerialNumber,
            LotNumber = l.LotNumber,
            IsStockDeducted = l.IsStockDeducted,
            Notes = l.Notes,
            DamageNotes = l.DamageNotes,
            UnitPrice = l.UnitPrice,
            DiscountPercentage = l.DiscountPercentage,
            DiscountAmount = l.DiscountAmount,
            TaxRate = l.TaxRate,
            TaxAmount = l.TaxAmount,
            TaxGroupId = l.TaxGroupId
        }).ToList() ?? new()
    };

    private Task ValidateLineQuantityAsync(DeliveryNoteLine line, decimal quantity, string boundary)
    {
        var validator = _commercialQuantityValidator
            ?? throw new InvalidOperationException("Commercial quantity policy validation is not configured for Sales delivery.");
        return SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
            validator, line, line.Unit, quantity, $"{boundary} line {line.LineNumber}");
    }

    #endregion
}
