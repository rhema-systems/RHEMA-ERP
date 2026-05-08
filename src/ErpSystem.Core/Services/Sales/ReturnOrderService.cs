using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class ReturnOrderService : IReturnOrderService
{
    private readonly IGenericRepository<ReturnOrder> _returnRepo;
    private readonly IGenericRepository<ReturnOrderLine> _returnLineRepo;
    private readonly IGenericRepository<CreditNote> _creditNoteRepo;
    private readonly IGenericRepository<CreditNoteLine> _creditLineRepo;
    private readonly IGenericRepository<Refund> _refundRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ReturnOrderService> _logger;

    public ReturnOrderService(
        IGenericRepository<ReturnOrder> returnRepo,
        IGenericRepository<ReturnOrderLine> returnLineRepo,
        IGenericRepository<CreditNote> creditNoteRepo,
        IGenericRepository<CreditNoteLine> creditLineRepo,
        IGenericRepository<Refund> refundRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ReturnOrderService> logger)
    {
        _returnRepo = returnRepo;
        _returnLineRepo = returnLineRepo;
        _creditNoteRepo = creditNoteRepo;
        _creditLineRepo = creditLineRepo;
        _refundRepo = refundRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // ═════════════════════════════════════
    //  RETURN ORDERS
    // ═════════════════════════════════════

    public async Task<ReturnOrderDetailDto> CreateReturnOrderAsync(CreateReturnOrderDto dto)
    {
        var docNumber = $"RO-{await _returnRepo.CountAsync() + 1:D6}";
        var tenantId = _currentUserProvider.TenantId;

        var ro = new ReturnOrder
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            SalesOrderId = dto.SalesOrderId,
            DeliveryNoteId = dto.DeliveryNoteId,
            CustomerId = dto.CustomerId,
            ReturnStatus = ReturnOrderStatus.Requested,
            ReasonCode = dto.ReasonCode,
            ReasonDescription = dto.ReasonDescription,
            TenantId = tenantId
        };

        await _returnRepo.AddAsync(ro);

        decimal total = 0;
        foreach (var lineDto in dto.Lines)
        {
            var line = new ReturnOrderLine
            {
                ReturnOrderId = ro.Id,
                SalesOrderLineId = lineDto.SalesOrderLineId,
                Description = lineDto.Description,
                ProductCode = lineDto.ProductCode,
                QuantityReturned = lineDto.QuantityReturned,
                UnitPrice = lineDto.UnitPrice,
                ReasonCode = lineDto.ReasonCode,
                Condition = lineDto.Condition,
                IsRestockable = lineDto.IsRestockable,
                TenantId = tenantId
            };
            total += lineDto.QuantityReturned * lineDto.UnitPrice;
            await _returnLineRepo.AddAsync(line);
        }

        ro.TotalAmount = total;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Return Order {DocNumber} for {Amount}", docNumber, total);
        return await GetReturnOrderByIdAsync(ro.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto?> GetReturnOrderByIdAsync(Guid id)
    {
        var ro = await _returnRepo.GetByIdAsync(id,
            r => r.SalesOrder,
            r => r.Customer,
            r => r.CreditNote!,
            r => r.Lines);
        return ro == null ? null : MapReturnOrderDetailDto(ro);
    }

    public async Task<PagedResult<ReturnOrderSummaryDto>> GetReturnOrdersAsync(
        int page = 1, int pageSize = 20,
        string? search = null, ReturnOrderStatus? status = null,
        Guid? customerId = null, Guid? salesOrderId = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _returnRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(r => r.ReturnStatus == status.Value);
        if (customerId.HasValue)
            query = query.Where(r => r.CustomerId == customerId.Value);
        if (salesOrderId.HasValue)
            query = query.Where(r => r.SalesOrderId == salesOrderId.Value);
        if (startDate.HasValue)
            query = query.Where(r => r.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(r => r.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(r => r.SalesOrder)
            .Include(r => r.Customer)
            .Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<ReturnOrderSummaryDto>
        {
            Items = items.Select(MapReturnOrderSummaryDto).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    public async Task<ReturnOrderDetailDto> ApproveReturnOrderAsync(Guid id)
    {
        var ro = await _returnRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Return Order {id} not found");
        if (ro.ReturnStatus != ReturnOrderStatus.Requested)
            throw new InvalidOperationException("Only requested return orders can be approved");
        ro.ReturnStatus = ReturnOrderStatus.Approved;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();
        return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto> ReceiveReturnOrderAsync(Guid id)
    {
        var ro = await _returnRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Return Order {id} not found");
        if (ro.ReturnStatus != ReturnOrderStatus.Approved)
            throw new InvalidOperationException("Only approved return orders can be received");
        ro.ReturnStatus = ReturnOrderStatus.Received;
        ro.ReceivedDate = DateTime.UtcNow;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();
        return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto> InspectReturnOrderAsync(Guid id, string? notes = null)
    {
        var ro = await _returnRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Return Order {id} not found");
        if (ro.ReturnStatus != ReturnOrderStatus.Received)
            throw new InvalidOperationException("Only received return orders can be inspected");
        ro.ReturnStatus = ReturnOrderStatus.Inspected;
        ro.InspectedDate = DateTime.UtcNow;
        ro.InspectedById = _currentUserProvider.UserId;
        ro.InspectionNotes = notes;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Return Order {DocNumber} inspected", ro.DocumentNumber);
        return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto> RejectReturnOrderAsync(Guid id, string? reason = null)
    {
        var ro = await _returnRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Return Order {id} not found");
        ro.ReturnStatus = ReturnOrderStatus.Rejected;
        ro.InspectionNotes = reason;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();
        return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto> CancelReturnOrderAsync(Guid id, string? reason = null)
    {
        var ro = await _returnRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Return Order {id} not found");
        ro.ReturnStatus = ReturnOrderStatus.Cancelled;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();
        return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    // ═════════════════════════════════════
    //  CREDIT NOTES
    // ═════════════════════════════════════

    public async Task<CreditNoteDetailDto> CreateCreditNoteAsync(CreateCreditNoteDto dto)
    {
        var docNumber = $"CN-{await _creditNoteRepo.CountAsync() + 1:D6}";
        var tenantId = _currentUserProvider.TenantId;

        var cn = new CreditNote
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            CustomerId = dto.CustomerId,
            ReturnOrderId = dto.ReturnOrderId,
            OriginalInvoiceId = dto.OriginalInvoiceId,
            CreditNoteStatus = CreditNoteStatus.Draft,
            Reason = dto.Reason,
            TenantId = tenantId
        };

        await _creditNoteRepo.AddAsync(cn);

        decimal total = 0, tax = 0;
        foreach (var lineDto in dto.Lines)
        {
            var line = new CreditNoteLine
            {
                CreditNoteId = cn.Id,
                Description = lineDto.Description,
                Quantity = lineDto.Quantity,
                UnitPrice = lineDto.UnitPrice,
                TaxAmount = lineDto.TaxAmount,
                TaxCode = lineDto.TaxCode,
                TenantId = tenantId
            };
            total += lineDto.Quantity * lineDto.UnitPrice;
            tax += lineDto.TaxAmount;
            await _creditLineRepo.AddAsync(line);
        }

        cn.TotalAmount = total + tax;
        cn.TaxAmount = tax;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Credit Note {DocNumber} for {Amount}", docNumber, cn.TotalAmount);
        return await GetCreditNoteByIdAsync(cn.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> CreateCreditNoteFromReturnAsync(Guid returnOrderId)
    {
        var ro = await _returnRepo.GetByIdAsync(returnOrderId, r => r.Lines)
            ?? throw new InvalidOperationException($"Return Order {returnOrderId} not found");

        if (ro.ReturnStatus != ReturnOrderStatus.Inspected)
            throw new InvalidOperationException("Can only create credit notes from inspected return orders");

        var createDto = new CreateCreditNoteDto
        {
            CustomerId = ro.CustomerId,
            ReturnOrderId = returnOrderId,
            Reason = $"Credit for Return Order {ro.DocumentNumber}",
            Lines = ro.Lines.Select(l => new CreateCreditNoteLineDto
            {
                Description = l.Description,
                Quantity = l.QuantityReturned,
                UnitPrice = l.UnitPrice
            }).ToList()
        };

        var cn = await CreateCreditNoteAsync(createDto);

        // Update return order with credit note reference
        ro.ReturnStatus = ReturnOrderStatus.CreditIssued;
        ro.CreditNoteId = cn.Id;
        await _returnRepo.UpdateAsync(ro);
        await _unitOfWork.SaveChangesAsync();

        return cn;
    }

    public async Task<CreditNoteDetailDto?> GetCreditNoteByIdAsync(Guid id)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id,
            c => c.Customer,
            c => c.ReturnOrder!,
            c => c.Lines);
        return cn == null ? null : MapCreditNoteDetailDto(cn);
    }

    public async Task<PagedResult<CreditNoteSummaryDto>> GetCreditNotesAsync(
        int page = 1, int pageSize = 20,
        string? search = null, CreditNoteStatus? status = null,
        Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _creditNoteRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(c => c.CreditNoteStatus == status.Value);
        if (customerId.HasValue)
            query = query.Where(c => c.CustomerId == customerId.Value);
        if (startDate.HasValue)
            query = query.Where(c => c.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(c => c.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(c => c.Customer)
            .Include(c => c.Lines)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<CreditNoteSummaryDto>
        {
            Items = items.Select(MapCreditNoteSummaryDto).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    public async Task<CreditNoteDetailDto> ApproveCreditNoteAsync(Guid id)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Credit Note {id} not found");
        if (cn.CreditNoteStatus != CreditNoteStatus.Draft && cn.CreditNoteStatus != CreditNoteStatus.PendingApproval)
            throw new InvalidOperationException("Only draft/pending credit notes can be approved");
        cn.CreditNoteStatus = CreditNoteStatus.Approved;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> ApplyCreditNoteAsync(Guid id, Guid? invoiceId = null)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Credit Note {id} not found");
        if (cn.CreditNoteStatus != CreditNoteStatus.Approved)
            throw new InvalidOperationException("Only approved credit notes can be applied");
        cn.CreditNoteStatus = CreditNoteStatus.Applied;
        cn.AppliedDate = DateTime.UtcNow;
        cn.AppliedToInvoiceId = invoiceId;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Credit Note {DocNumber} applied", cn.DocumentNumber);
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> VoidCreditNoteAsync(Guid id, string? reason = null)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Credit Note {id} not found");
        cn.CreditNoteStatus = CreditNoteStatus.Voided;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    // ═════════════════════════════════════
    //  REFUNDS
    // ═════════════════════════════════════

    public async Task<RefundDetailDto> CreateRefundAsync(CreateRefundDto dto)
    {
        var docNumber = $"RF-{await _refundRepo.CountAsync() + 1:D6}";
        var refund = new Refund
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            CustomerId = dto.CustomerId,
            CreditNoteId = dto.CreditNoteId,
            ReturnOrderId = dto.ReturnOrderId,
            RefundStatus = RefundStatus.Draft,
            RefundAmount = dto.RefundAmount,
            TotalAmount = dto.RefundAmount,
            RefundMethod = dto.RefundMethod,
            Reason = dto.Reason,
            TenantId = _currentUserProvider.TenantId
        };

        await _refundRepo.AddAsync(refund);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Refund {DocNumber} for {Amount}", docNumber, dto.RefundAmount);
        return await GetRefundByIdAsync(refund.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto?> GetRefundByIdAsync(Guid id)
    {
        var refund = await _refundRepo.GetByIdAsync(id,
            r => r.Customer,
            r => r.CreditNote!,
            r => r.ProcessedBy!);
        return refund == null ? null : MapRefundDetailDto(refund);
    }

    public async Task<PagedResult<RefundSummaryDto>> GetRefundsAsync(
        int page = 1, int pageSize = 20,
        string? search = null, RefundStatus? status = null,
        Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _refundRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(r => r.RefundStatus == status.Value);
        if (customerId.HasValue)
            query = query.Where(r => r.CustomerId == customerId.Value);
        if (startDate.HasValue)
            query = query.Where(r => r.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(r => r.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(r => r.Customer)
            .Include(r => r.CreditNote)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<RefundSummaryDto>
        {
            Items = items.Select(MapRefundSummaryDto).ToList(),
            TotalCount = totalCount, Page = page, PageSize = pageSize
        };
    }

    public async Task<RefundDetailDto> ApproveRefundAsync(Guid id)
    {
        var refund = await _refundRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Refund {id} not found");
        if (refund.RefundStatus != RefundStatus.Draft && refund.RefundStatus != RefundStatus.PendingApproval)
            throw new InvalidOperationException("Only draft/pending refunds can be approved");
        refund.RefundStatus = RefundStatus.Approved;
        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto> ProcessRefundAsync(Guid id, string? paymentReference = null)
    {
        var refund = await _refundRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Refund {id} not found");
        if (refund.RefundStatus != RefundStatus.Approved)
            throw new InvalidOperationException("Only approved refunds can be processed");
        refund.RefundStatus = RefundStatus.Completed;
        refund.ProcessedDate = DateTime.UtcNow;
        refund.ProcessedById = _currentUserProvider.UserId;
        refund.PaymentReference = paymentReference;
        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Refund {DocNumber} processed — {Amount} via {Method}", refund.DocumentNumber, refund.RefundAmount, refund.RefundMethod);
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto> RejectRefundAsync(Guid id, string? reason = null)
    {
        var refund = await _refundRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Refund {id} not found");
        refund.RefundStatus = RefundStatus.Rejected;
        refund.Reason = $"{refund.Reason}\n[Rejected] {reason}".Trim();
        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    // ═════════════════════════════════════
    //  MAPPING
    // ═════════════════════════════════════

    private static ReturnOrderSummaryDto MapReturnOrderSummaryDto(ReturnOrder r) => new()
    {
        Id = r.Id,
        DocumentNumber = r.DocumentNumber,
        ReturnStatus = r.ReturnStatus,
        ReasonCode = r.ReasonCode,
        CustomerName = r.Customer?.CustomerName,
        SalesOrderNumber = r.SalesOrder?.DocumentNumber,
        TotalAmount = r.TotalAmount,
        LineCount = r.Lines?.Count ?? 0,
        ReceivedDate = r.ReceivedDate,
        CreatedAt = r.CreatedAt
    };

    private static ReturnOrderDetailDto MapReturnOrderDetailDto(ReturnOrder r) => new()
    {
        Id = r.Id,
        DocumentNumber = r.DocumentNumber,
        ReturnStatus = r.ReturnStatus,
        ReasonCode = r.ReasonCode,
        CustomerName = r.Customer?.CustomerName,
        SalesOrderNumber = r.SalesOrder?.DocumentNumber,
        TotalAmount = r.TotalAmount,
        LineCount = r.Lines?.Count ?? 0,
        ReceivedDate = r.ReceivedDate,
        CreatedAt = r.CreatedAt,
        SalesOrderId = r.SalesOrderId,
        DeliveryNoteId = r.DeliveryNoteId,
        CustomerId = r.CustomerId,
        ReasonDescription = r.ReasonDescription,
        InspectedDate = r.InspectedDate,
        InspectedByName = r.InspectedBy?.UserName,
        InspectionNotes = r.InspectionNotes,
        CreditNoteId = r.CreditNoteId,
        CreditNoteNumber = r.CreditNote?.DocumentNumber,
        RefundId = r.RefundId,
        Lines = r.Lines?.Select(l => new ReturnOrderLineDto
        {
            Id = l.Id,
            Description = l.Description,
            ProductCode = l.ProductCode,
            QuantityReturned = l.QuantityReturned,
            UnitPrice = l.UnitPrice,
            LineTotal = l.QuantityReturned * l.UnitPrice,
            ReasonCode = l.ReasonCode,
            Condition = l.Condition,
            IsRestockable = l.IsRestockable
        }).ToList() ?? new()
    };

    private static CreditNoteSummaryDto MapCreditNoteSummaryDto(CreditNote c) => new()
    {
        Id = c.Id,
        DocumentNumber = c.DocumentNumber,
        CreditNoteStatus = c.CreditNoteStatus,
        CustomerName = c.Customer?.CustomerName,
        TotalAmount = c.TotalAmount,
        Reason = c.Reason,
        AppliedDate = c.AppliedDate,
        LineCount = c.Lines?.Count ?? 0,
        CreatedAt = c.CreatedAt
    };

    private static CreditNoteDetailDto MapCreditNoteDetailDto(CreditNote c) => new()
    {
        Id = c.Id,
        DocumentNumber = c.DocumentNumber,
        CreditNoteStatus = c.CreditNoteStatus,
        CustomerName = c.Customer?.CustomerName,
        TotalAmount = c.TotalAmount,
        Reason = c.Reason,
        AppliedDate = c.AppliedDate,
        LineCount = c.Lines?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        CustomerId = c.CustomerId,
        ReturnOrderId = c.ReturnOrderId,
        ReturnOrderNumber = c.ReturnOrder?.DocumentNumber,
        OriginalInvoiceId = c.OriginalInvoiceId,
        AppliedToInvoiceId = c.AppliedToInvoiceId,
        TaxAmount = c.TaxAmount,
        Lines = c.Lines?.Select(l => new CreditNoteLineDto
        {
            Id = l.Id,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.Quantity * l.UnitPrice,
            TaxAmount = l.TaxAmount,
            TaxCode = l.TaxCode
        }).ToList() ?? new()
    };

    private static RefundSummaryDto MapRefundSummaryDto(Refund r) => new()
    {
        Id = r.Id,
        DocumentNumber = r.DocumentNumber,
        RefundStatus = r.RefundStatus,
        CustomerName = r.Customer?.CustomerName,
        RefundAmount = r.RefundAmount,
        RefundMethod = r.RefundMethod,
        ProcessedDate = r.ProcessedDate,
        CreatedAt = r.CreatedAt
    };

    private static RefundDetailDto MapRefundDetailDto(Refund r) => new()
    {
        Id = r.Id,
        DocumentNumber = r.DocumentNumber,
        RefundStatus = r.RefundStatus,
        CustomerName = r.Customer?.CustomerName,
        RefundAmount = r.RefundAmount,
        RefundMethod = r.RefundMethod,
        ProcessedDate = r.ProcessedDate,
        CreatedAt = r.CreatedAt,
        CustomerId = r.CustomerId,
        CreditNoteId = r.CreditNoteId,
        CreditNoteNumber = r.CreditNote?.DocumentNumber,
        ReturnOrderId = r.ReturnOrderId,
        Reason = r.Reason,
        ProcessedByName = r.ProcessedBy?.UserName,
        PaymentReference = r.PaymentReference
    };
}
