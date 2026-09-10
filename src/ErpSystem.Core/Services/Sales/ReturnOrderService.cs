using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Services.Sales;

public class ReturnOrderService : IReturnOrderService
{
    private const string CreditNoteWorkflowEntityType = "CreditNote";
    private const string RefundWorkflowEntityType = "Refund";

    private readonly IGenericRepository<ReturnOrder> _returnRepo;
    private readonly IGenericRepository<ReturnOrderLine> _returnLineRepo;
    private readonly IGenericRepository<CreditNote> _creditNoteRepo;
    private readonly IGenericRepository<CreditNoteLine> _creditLineRepo;
    private readonly IGenericRepository<Refund> _refundRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<ReturnOrderService> _logger;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IFinanceProducerIntentService? _financeProducerIntents;
    private readonly IFinanceProducerApprovedExecutionService? _financeProducerExecution;
    private readonly IFinanceProducerReversalPreparationService? _financeProducerReversals;
    private readonly IFinanceProducerReplayVerificationService? _financeProducerReplayVerifier;

    public ReturnOrderService(
        IGenericRepository<ReturnOrder> returnRepo,
        IGenericRepository<ReturnOrderLine> returnLineRepo,
        IGenericRepository<CreditNote> creditNoteRepo,
        IGenericRepository<CreditNoteLine> creditLineRepo,
        IGenericRepository<Refund> refundRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IDocumentNumberingService documentNumberingService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<ReturnOrderService> logger,
        IFinanceAuditService? financeAuditService = null,
        IFinanceProducerIntentService? financeProducerIntents = null,
        IFinanceProducerApprovedExecutionService? financeProducerExecution = null,
        IFinanceProducerReversalPreparationService? financeProducerReversals = null,
        IFinanceProducerReplayVerificationService? financeProducerReplayVerifier = null)
    {
        _returnRepo = returnRepo;
        _returnLineRepo = returnLineRepo;
        _creditNoteRepo = creditNoteRepo;
        _creditLineRepo = creditLineRepo;
        _refundRepo = refundRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
        _documentNumberingService = documentNumberingService;
        _financeAuditService = financeAuditService;
        _financeProducerIntents = financeProducerIntents;
        _financeProducerExecution = financeProducerExecution;
        _financeProducerReversals = financeProducerReversals;
        _financeProducerReplayVerifier = financeProducerReplayVerifier;
    }

    // ═════════════════════════════════════
    //  RETURN ORDERS
    // ═════════════════════════════════════

    public async Task<ReturnOrderDetailDto> CreateReturnOrderAsync(CreateReturnOrderDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var salesOrder = await _unitOfWork.Repository<SalesOrder>()
            .GetQueryable(s => s.TenantId == tenantId && s.Id == dto.SalesOrderId && !s.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Return order source sales order was not found for this tenant.");

        if (dto.DeliveryNoteId.HasValue)
        {
            var deliveryNote = await _unitOfWork.Repository<DeliveryNote>()
                .GetQueryable(d => d.TenantId == tenantId && d.Id == dto.DeliveryNoteId.Value && !d.IsDeleted)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Return order delivery note was not found for this tenant.");

            if (deliveryNote.BusinessPartnerId != salesOrder.BusinessPartnerId)
                throw new InvalidOperationException("Return order delivery note must belong to the source sales order business partner.");

            if (deliveryNote.SalesOrderId != salesOrder.Id)
                throw new InvalidOperationException("Return order delivery note must belong to the selected source sales order.");
        }

        var docNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Sales,
            SalesDocumentTypes.ReturnOrder,
            tenantId,
            DateTime.UtcNow,
            nameof(ReturnOrder));

        var ro = new ReturnOrder
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            SalesOrderId = dto.SalesOrderId,
            DeliveryNoteId = dto.DeliveryNoteId,
            // Sales orders already use BusinessPartner. Deriving the return counterparty prevents a
            // caller from creating a return against a different tenant/customer identity.
            BusinessPartnerId = salesOrder.BusinessPartnerId,
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

        var workflowResult = await _workflowIntegrationService.SubmitAsync("ReturnOrder", ro.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            ro.ReturnStatus = ReturnOrderStatus.Cancelled;
            ro.InspectionNotes = workflowResult.ExecutionResult.Message ?? "Unable to start customer return approval workflow.";
            await _returnRepo.UpdateAsync(ro);
            await _unitOfWork.SaveChangesAsync();
            throw new InvalidOperationException(ro.InspectionNotes);
        }

        _logger.LogInformation("Created Return Order {DocNumber} for {Amount}", docNumber, total);
        return await GetReturnOrderByIdAsync(ro.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ReturnOrderDetailDto?> GetReturnOrderByIdAsync(Guid id)
    {
        var ro = await _returnRepo.GetByIdAsync(id,
            r => r.SalesOrder,
            r => r.BusinessPartner,
            r => r.CreditNote!,
            r => r.Lines);
        return ro == null ? null : MapReturnOrderDetailDto(ro);
    }

    public async Task<PagedResult<ReturnOrderSummaryDto>> GetReturnOrdersAsync(
        int page = 1, int pageSize = 20,
        string? search = null, ReturnOrderStatus? status = null,
        Guid? businessPartnerId = null, Guid? salesOrderId = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _returnRepo.GetQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(r => r.ReturnStatus == status.Value);
        if (businessPartnerId.HasValue)
            query = query.Where(r => r.BusinessPartnerId == businessPartnerId.Value);
        if (salesOrderId.HasValue)
            query = query.Where(r => r.SalesOrderId == salesOrderId.Value);
        if (startDate.HasValue)
            query = query.Where(r => r.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(r => r.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(r => r.SalesOrder)
            .Include(r => r.BusinessPartner)
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

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("ReturnOrder", id, _currentUserProvider.UserId, "Approve");
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to process customer return approval workflow.");

        if (workflowResult.Outcome == WorkflowOutcome.Pending)
            return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

        if (workflowResult.Outcome == WorkflowOutcome.Rejected)
        {
            ro.ReturnStatus = ReturnOrderStatus.Rejected;
            await _returnRepo.UpdateAsync(ro);
            await _unitOfWork.SaveChangesAsync();
            return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }

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
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("ReturnOrder", id, _currentUserProvider.UserId, "Reject", reason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to process customer return rejection workflow.");

        if (workflowResult.Outcome == WorkflowOutcome.Pending)
            return await GetReturnOrderByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

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
        var tenantId = _currentUserProvider.TenantId;
        await ValidateCreditNoteBusinessPartnerAsync(dto, tenantId);
        var docNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Sales,
            SalesDocumentTypes.CreditNote,
            tenantId,
            DateTime.UtcNow,
            nameof(CreditNote));

        var cn = new CreditNote
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            BusinessPartnerId = dto.BusinessPartnerId,
            ReturnOrderId = dto.ReturnOrderId,
            OriginalInvoiceId = dto.OriginalInvoiceId,
            CreditNoteStatus = CreditNoteStatus.PendingApproval,
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

        var workflowResult = await _workflowIntegrationService.SubmitAsync("CreditNote", cn.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            cn.CreditNoteStatus = CreditNoteStatus.Draft;
            await _creditNoteRepo.UpdateAsync(cn);
            await _unitOfWork.SaveChangesAsync();
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to start credit note approval workflow.");
        }

        await RecordArCreditNoteAuditAsync(
            FinanceAuditEvents.ArCreditNoteSubmitted,
            cn,
            afterValues: new
            {
                cn.DocumentNumber,
                cn.CreditNoteStatus,
                cn.BusinessPartnerId,
                cn.OriginalInvoiceId,
                cn.TotalAmount,
                cn.TaxAmount
            },
            comment: "AR credit note submitted through the configured workflow engine.");

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
            BusinessPartnerId = ro.BusinessPartnerId,
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
        var tenantId = _currentUserProvider.TenantId;
        var cn = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id)
            .Include(c => c.BusinessPartner)
            .Include(c => c.ReturnOrder!)
            .Include(c => c.OriginalInvoice)
            .Include(c => c.Lines)
            .FirstOrDefaultAsync();
        if (cn is null) return null;
        var dto = MapCreditNoteDetailDto(cn);
        var events = await _unitOfWork.Repository<AccountingEvent>().GetQueryable(item =>
            item.TenantId == tenantId && item.SourceDocumentId == cn.Id &&
            (item.IdempotencyKey == SalesCreditNotePostingKey(cn) || item.IdempotencyKey == SalesCreditNoteReversalKey(cn)) &&
            !item.IsDeleted).ToListAsync();
        var original = events.SingleOrDefault(item => item.IdempotencyKey == SalesCreditNotePostingKey(cn));
        var reversal = events.SingleOrDefault(item => item.IdempotencyKey == SalesCreditNoteReversalKey(cn));
        if (original is not null)
        {
            dto.AccountingEventId = original.Id; dto.AccountingEventRequestFingerprint = original.RequestFingerprint;
            dto.AccountingEventStatus = original.Status; dto.AccountingEventDecisionStatus = original.ProducerDecisionStatus;
        }
        if (reversal is not null)
        {
            dto.ReversalAccountingEventId = reversal.Id; dto.ReversalAccountingEventRequestFingerprint = reversal.RequestFingerprint;
            dto.ReversalAccountingEventStatus = reversal.Status; dto.ReversalAccountingEventDecisionStatus = reversal.ProducerDecisionStatus;
        }
        return dto;
    }

    public async Task<PagedResult<CreditNoteSummaryDto>> GetCreditNotesAsync(
        int page = 1, int pageSize = 20,
        string? search = null, CreditNoteStatus? status = null,
        Guid? businessPartnerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var query = _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(c => c.CreditNoteStatus == status.Value);
        if (businessPartnerId.HasValue)
            query = query.Where(c => c.BusinessPartnerId == businessPartnerId.Value);
        if (startDate.HasValue)
            query = query.Where(c => c.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(c => c.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(c => c.BusinessPartner)
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

    public async Task<CreditNoteDetailDto> SubmitCreditNoteForApprovalAsync(Guid id)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Credit Note {id} not found");

        if (cn.CreditNoteStatus != CreditNoteStatus.Draft)
            throw new InvalidOperationException("Only draft credit notes can be submitted for approval");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User is not authenticated");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(CreditNoteWorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start credit note workflow");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(CreditNoteWorkflowEntityType);
        adapter.ApplySubmitOutcome(cn, workflowResult.Outcome, userId);

        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Credit Note {DocNumber} submitted for approval", cn.DocumentNumber);
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> ProcessCreditNoteApprovalAsync(Guid id, CreditNoteApprovalDto dto)
    {
        var cn = await _creditNoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Credit Note {id} not found");

        if (cn.CreditNoteStatus != CreditNoteStatus.PendingApproval)
            throw new InvalidOperationException("Only pending approval credit notes can be approved or rejected");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User is not authenticated");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(CreditNoteWorkflowEntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned to approve the current workflow step");

        var comments = dto.IsApproved
            ? dto.Comments
            : dto.RejectionReason ?? dto.Comments;

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            CreditNoteWorkflowEntityType,
            id,
            userId,
            dto.IsApproved ? "Approve" : "Reject",
            comments);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process credit note workflow approval");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(CreditNoteWorkflowEntityType);
        adapter.ApplyApprovalOutcome(cn, workflowResult.Outcome, userId, comments);

        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Credit Note {DocNumber} workflow approval processed. Approved: {Approved}",
            cn.DocumentNumber,
            dto.IsApproved);
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> ApproveCreditNoteAsync(Guid id)
    {
        var tenantId = _currentUserProvider.TenantId;
        var cn = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Credit Note {id} not found");
        if (cn.CreditNoteStatus != CreditNoteStatus.Draft && cn.CreditNoteStatus != CreditNoteStatus.PendingApproval)
            throw new InvalidOperationException("Only draft/pending credit notes can be approved");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("CreditNote", id, _currentUserProvider.UserId, "Approve");
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to process credit note approval workflow.");

        if (workflowResult.Outcome == WorkflowOutcome.Pending)
            return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

        if (workflowResult.Outcome == WorkflowOutcome.Rejected)
        {
            cn.CreditNoteStatus = CreditNoteStatus.Voided;
            cn.Reason = $"{cn.Reason}\n[Rejected] Workflow rejected".Trim();
            await _creditNoteRepo.UpdateAsync(cn);
            await _unitOfWork.SaveChangesAsync();
            await RecordArCreditNoteAuditAsync(
                FinanceAuditEvents.ArCreditNoteRejected,
                cn,
                afterValues: new { cn.CreditNoteStatus, cn.Reason },
                reason: "Workflow rejected",
                comment: "AR credit note rejected through the configured workflow engine.");
            return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }

        cn.CreditNoteStatus = CreditNoteStatus.Approved;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        await RecordArCreditNoteAuditAsync(
            FinanceAuditEvents.ArCreditNoteApproved,
            cn,
            afterValues: new { cn.CreditNoteStatus },
            comment: "AR credit note approved through the configured workflow engine.");

        return await PostCreditNoteAsync(id);
    }

    public async Task<CreditNoteDetailDto> PostCreditNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var producer = RequireProducerIntents();
        var execution = RequireProducerExecution();
        CreditNote? cn = null;
        ProducerAccountingIntentDto? intent = null;
        AccountingEventDto? prepared = null;
        var transactionStarted = false;
        var transactionCommitted = false;
        var executionAttempted = false;

        try
        {
            cn = await LoadCreditNoteForPostingAsync(id, cancellationToken);
            if (cn.JournalEntryId.HasValue)
            {
                await RequireExactPostedReplayAsync(cn, cancellationToken);
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
            }

            // Preparation deliberately happens before any Sales mutation. C11 later accepts an ID-only
            // maker/checker decision; this method never approves a producer intent.
            intent = await BuildSalesCreditNoteProducerIntentAsync(cn, cancellationToken);
            prepared = await producer.PrepareAsync(intent, cancellationToken);
            intent.AccountingEventId = prepared.Id;
            if (prepared.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved)
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            transactionStarted = true;
            await _unitOfWork.AcquireTransactionLockAsync(CreditNoteExecutionLock(cn), cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            cn = await LoadCreditNoteForPostingAsync(id, cancellationToken);
            if (cn.JournalEntryId.HasValue)
            {
                await RequireExactPostedReplayAsync(cn, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
            }

            // Preparation is intentionally outside this transaction. Rebuild the producer intent after
            // the Serializable reload so source status, invoice availability and prior-credit limits are
            // rechecked under the owner transaction; C12 will reject any snapshot/fingerprint conflict.
            intent = await BuildSalesCreditNoteProducerIntentAsync(cn, cancellationToken);
            intent.AccountingEventId = prepared.Id;
            var decision = await producer.GetAsync(prepared.Id, cancellationToken);
            RequireApprovedPreparedAuthority(prepared, decision);
            // This tracked, rollback-safe Sales mutation is the deterministic owner effect acknowledged by C12.
            cn.UpdatedAt = DateTime.UtcNow;
            cn.UpdatedBy = _currentUserProvider.Username;
            await _creditNoteRepo.UpdateAsync(cn);
            executionAttempted = true;
            var result = await execution.ExecuteInAmbientTransactionAsync(prepared.Id, intent,
                ReceiptFor(cn, intent.ExpectedOwnerEffect), cancellationToken);
            ValidateCompatibility(prepared, result);
            cn.JournalEntryId = result.JournalEntryId;
            cn.CreditNoteStatus = CreditNoteStatus.Approved;
            await _creditNoteRepo.UpdateAsync(cn);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            transactionStarted = false;
            transactionCommitted = true;

            await RecordArCreditNoteAuditAsync(FinanceAuditEvents.ArCreditNotePosted, cn,
                postingEventId: result.FinancePostingEventId, journalEntryId: result.JournalEntryId,
                afterValues: new { result.AccountingEventId, result.FinancePostingEventId, result.JournalEntryId },
                comment: "AR credit note posted through the governed Finance producer boundary.", cancellationToken: cancellationToken);
            return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            if (transactionStarted)
                await _unitOfWork.RollbackAsync(cancellationToken);
            // A caller-owned ambient transaction remains open after UnitOfWork relinquishes its join.
            // Finance failure evidence is valid only after the actual owner has ended that transaction.
            if (executionAttempted && !transactionCommitted && !_unitOfWork.HasActiveTransaction && prepared is not null && intent is not null)
                await execution.RecordFailureAfterRollbackAsync(prepared.Id, intent,
                    ReceiptFor(cn ?? throw new InvalidOperationException("Credit note was unavailable after rollback."), intent.ExpectedOwnerEffect), ex, cancellationToken);
            if (cn != null && prepared is null)
            {
                await RecordArCreditNoteAuditAsync(
                    FinanceAuditEvents.ArCreditNotePostingFailed,
                    cn,
                    afterValues: new { cn.JournalEntryId, error = ex.Message },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);
            }

            _logger.LogError(ex, "Failed to prepare or execute governed AR credit note {DocumentNumber}", cn?.DocumentNumber ?? id.ToString());
            throw;
        }
    }

    public async Task<CreditNoteDetailDto> ApplyCreditNoteAsync(Guid id, Guid? invoiceId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var cn = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Credit Note {id} not found");

        var applicationInvoiceId = invoiceId ?? cn.OriginalInvoiceId;
        if (cn.CreditNoteStatus == CreditNoteStatus.Reversed)
        {
            throw new InvalidOperationException(
                "Reversed AR credit notes cannot be applied. Create a new approved credit note if a correction is required.");
        }

        if (cn.CreditNoteStatus == CreditNoteStatus.Applied)
        {
            if (cn.AppliedToInvoiceId == applicationInvoiceId)
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

            throw new InvalidOperationException("Applied AR credit notes cannot be moved to another invoice. Use a reversal or adjustment workflow.");
        }

        if (cn.CreditNoteStatus != CreditNoteStatus.Approved)
            throw new InvalidOperationException("Only approved credit notes can be applied");

        var postedEventExists = cn.JournalEntryId.HasValue && await _unitOfWork.Repository<FinancePostingEvent>()
            .GetQueryable(e =>
                e.TenantId == tenantId &&
                e.SourceDocumentType == "SalesCreditNote" &&
                e.SourceDocumentId == cn.Id &&
                e.PostingAction == "Post" &&
                e.PostingStatus == "Posted" &&
                e.JournalEntryId == cn.JournalEntryId &&
                !e.IsDeleted)
            .AnyAsync();

        if (!postedEventExists)
            throw new InvalidOperationException("AR credit note must be posted through the central finance posting engine before it can be applied.");

        if (cn.OriginalInvoiceId.HasValue && applicationInvoiceId != cn.OriginalInvoiceId)
            throw new InvalidOperationException("AR credit notes can only be applied to their original invoice. Use a reversal or adjustment workflow to correct the target.");

        if (!applicationInvoiceId.HasValue)
            throw new InvalidOperationException("Standalone AR credit notes require an open Finance invoice for the same business partner before application.");

        if (applicationInvoiceId.HasValue)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == tenantId && i.Id == applicationInvoiceId.Value && !i.IsDeleted)
                .FirstOrDefaultAsync();
            if (invoice == null)
                throw new InvalidOperationException("AR credit note application invoice was not found for this tenant.");
            if (invoice.BusinessPartnerId != cn.BusinessPartnerId)
                throw new InvalidOperationException("AR credit notes can only be applied to invoices for the same business partner.");
            if (!invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException($"AR credit note cannot be applied to unposted invoice '{invoice.InvoiceNumber}'.");

            var postedReceiptSettlement = await GetPostedCustomerReceiptSettlementForInvoiceAsync(
                invoice.Id,
                CancellationToken.None);
            var postedSalesCredits = await GetPostedSalesCreditTotalForInvoiceAsync(
                invoice.Id,
                excludedCreditNoteId: null,
                cancellationToken: CancellationToken.None);
            if (RoundMoney(postedReceiptSettlement + postedSalesCredits) > RoundMoney(invoice.TotalAmount))
            {
                throw new InvalidOperationException(
                    $"AR credit note would over-settle invoice '{invoice.InvoiceNumber}' when combined with posted receipts and credit notes.");
            }
        }
        cn.CreditNoteStatus = CreditNoteStatus.Applied;
        cn.AppliedDate = DateTime.UtcNow;
        cn.AppliedToInvoiceId = applicationInvoiceId;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Credit Note {DocNumber} applied", cn.DocumentNumber);
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CreditNoteDetailDto> ReverseCreditNoteAsync(
        Guid id,
        ReverseCreditNoteDto dto,
        CancellationToken cancellationToken = default)
    {
        var producer = RequireProducerIntents();
        var execution = RequireProducerExecution();
        var reversals = RequireProducerReversals();
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A credit note reversal reason is required.");

        CreditNote? creditNote = null;
        ProducerAccountingReversalPreparationResultDto? prepared = null;
        ProducerOwnerEffectIdentityDto? ownerEffect = null;
        var transactionStarted = false;
        var transactionCommitted = false;
        var executionAttempted = false;
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            creditNote = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Credit Note {id} not found.");

            if (creditNote.CreditNoteStatus == CreditNoteStatus.Reversed)
            {
                if (!creditNote.ReversalJournalEntryId.HasValue || !creditNote.ReversalPostingEventId.HasValue)
                    throw new InvalidOperationException("Reversed AR credit note is missing its immutable reversal references.");

                var requestedDate = dto.ReversalDate?.Date;
                if (!string.Equals(creditNote.ReversalReason, dto.Reason.Trim(), StringComparison.Ordinal)
                    || (requestedDate.HasValue && creditNote.ReversedAt?.Date != requestedDate))
                {
                    throw new InvalidOperationException(
                        "AR credit note reversal retry conflicts with the immutable reversal evidence.");
                }
                await RequireExactReversalReplayAsync(creditNote, cancellationToken);
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
            }

            if (!creditNote.JournalEntryId.HasValue)
                throw new InvalidOperationException("Only a posted AR credit note can be reversed.");
            if (creditNote.CreditNoteStatus != CreditNoteStatus.Approved && creditNote.CreditNoteStatus != CreditNoteStatus.Applied)
                throw new InvalidOperationException("Only approved or applied AR credit notes can be reversed.");

            var original = await _unitOfWork.Repository<AccountingEvent>()
                .GetQueryable(e =>
                    e.TenantId == tenantId &&
                    e.OriginatingModuleCode == "SALES" &&
                    e.SourceDocumentType == "SalesCreditNote" &&
                    e.SourceDocumentId == creditNote.Id &&
                    e.PostingAction == "Post" && e.IdempotencyKey == SalesCreditNotePostingKey(creditNote) &&
                    e.Status == AccountingEventStatuses.Posted &&
                    !e.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The original governed AR credit note event was not found for this tenant.");
            // A retry/restart must retain the first durable C13 event date rather than sampling a
            // new UTC day after midnight under the same deterministic reversal key.
            var existingReversal = await _unitOfWork.Repository<AccountingEvent>().GetQueryable(e =>
                e.TenantId == tenantId && e.IdempotencyKey == SalesCreditNoteReversalKey(creditNote) && !e.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingReversal is not null && dto.ReversalDate.HasValue &&
                existingReversal.EventDate.Date != dto.ReversalDate.Value.Date)
                throw new InvalidOperationException("AR credit note reversal retry conflicts with the immutable reversal date.");
            var reversalDate = existingReversal?.EventDate.Date ?? (dto.ReversalDate ?? DateTime.UtcNow).Date;
            ownerEffect = OwnerEffectFor(creditNote, "REVERSE", $"{original.Id:N}:{reversalDate:O}:{dto.Reason.Trim()}");
            prepared = await reversals.PrepareReversalAsync(new PrepareProducerAccountingReversalDto
            {
                OriginalAccountingEventId = original.Id,
                ReversalAccountingEventId = DeterministicGuid(creditNote.Id, "AR-CREDIT-NOTE-REVERSAL"),
                IdempotencyKey = SalesCreditNoteReversalKey(creditNote),
                ReversalDate = reversalDate,
                Reason = dto.Reason.Trim(), ParticipantIdentity = ownerEffect.ParticipantCode,
                ExpectedOwnerEffect = ownerEffect
            }, cancellationToken);
            if (prepared.DecisionStatus != ProducerIntentDecisionStatuses.Approved)
                return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            transactionStarted = true;
            await _unitOfWork.AcquireTransactionLockAsync(CreditNoteExecutionLock(creditNote), cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            creditNote = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted)
                .FirstAsync(cancellationToken);
            var lockedOriginal = await _unitOfWork.Repository<AccountingEvent>().GetQueryable(e =>
                e.TenantId == tenantId && e.Id == original.Id && e.OriginatingModuleCode == "SALES" &&
                e.SourceDocumentType == "SalesCreditNote" && e.SourceDocumentId == creditNote.Id &&
                e.PostingAction == "Post" && e.IdempotencyKey == SalesCreditNotePostingKey(creditNote) &&
                e.Status == AccountingEventStatuses.Posted && !e.IsDeleted).SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("AR credit note reversal source authority changed before execution.");
            var lockedEffect = OwnerEffectFor(creditNote, "REVERSE", $"{lockedOriginal.Id:N}:{reversalDate:O}:{dto.Reason.Trim()}");
            if (!OwnerEffectsMatch(ownerEffect, lockedEffect))
                throw new InvalidOperationException("AR credit note reversal owner authority changed before execution.");
            var decision = await producer.GetAsync(prepared.AccountingEventId, cancellationToken);
            if (decision.Id != prepared.AccountingEventId || decision.RequestFingerprint != prepared.AccountingEventRequestFingerprint
                || decision.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved)
                throw new InvalidOperationException("AR credit note reversal approval authority changed before execution.");
            creditNote.UpdatedAt = DateTime.UtcNow;
            creditNote.UpdatedBy = _currentUserProvider.Username;
            await _creditNoteRepo.UpdateAsync(creditNote);
            executionAttempted = true;
            var result = await execution.ExecuteInAmbientTransactionAsync(prepared.AccountingEventId,
                ReceiptFor(creditNote, ownerEffect), cancellationToken);
            if (result.AccountingEventId != prepared.AccountingEventId || result.AccountingEventRequestFingerprint != prepared.AccountingEventRequestFingerprint
                || result.Status != AccountingEventStatuses.Posted || result.FinancePostingEventId == Guid.Empty || result.JournalEntryId == Guid.Empty)
                throw new InvalidOperationException("AR credit note reversal compatibility evidence does not bind to the prepared event.");

            creditNote.CreditNoteStatus = CreditNoteStatus.Reversed;
            creditNote.ReversalJournalEntryId = result.JournalEntryId;
            creditNote.ReversalPostingEventId = result.FinancePostingEventId;
            creditNote.ReversedAt = reversalDate;
            creditNote.ReversalReason = dto.Reason.Trim();
            await _creditNoteRepo.UpdateAsync(creditNote);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            transactionStarted = false;
            transactionCommitted = true;

            await RecordArCreditNoteAuditAsync(
                FinanceAuditEvents.ArCreditNoteReversed,
                creditNote,
                postingEventId: result.FinancePostingEventId,
                journalEntryId: result.JournalEntryId,
                afterValues: new
                {
                    creditNote.CreditNoteStatus,
                    creditNote.ReversalPostingEventId,
                    creditNote.ReversalJournalEntryId,
                    creditNote.ReversedAt
                },
                reason: creditNote.ReversalReason,
                comment: "AR credit note reversed through the governed Finance producer boundary.",
                cancellationToken: cancellationToken);

            return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }
        catch (Exception ex)
        {
            if (transactionStarted)
                await _unitOfWork.RollbackAsync(cancellationToken);

            if (executionAttempted && !transactionCommitted && !_unitOfWork.HasActiveTransaction && prepared is not null && ownerEffect is not null)
                await execution.RecordFailureAfterRollbackAsync(prepared.AccountingEventId,
                    ReceiptFor(creditNote ?? throw new InvalidOperationException("Credit note was unavailable after rollback."), ownerEffect), ex, cancellationToken);

            if (creditNote != null && prepared is null)
            {
                await RecordArCreditNoteAuditAsync(
                    FinanceAuditEvents.ArCreditNoteReversalFailed,
                    creditNote,
                    afterValues: new { error = ex.Message },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);
            }

            _logger.LogError(ex, "Failed to reverse AR credit note {DocumentNumber}", creditNote?.DocumentNumber ?? id.ToString());
            throw;
        }
    }

    public async Task<CreditNoteDetailDto> VoidCreditNoteAsync(Guid id, string? reason = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var cn = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Credit Note {id} not found");
        if (cn.JournalEntryId.HasValue)
            throw new InvalidOperationException("Posted AR credit notes cannot be voided by mutation. Use a reversal or adjustment workflow.");
        cn.CreditNoteStatus = CreditNoteStatus.Voided;
        await _creditNoteRepo.UpdateAsync(cn);
        await _unitOfWork.SaveChangesAsync();
        return await GetCreditNoteByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    // ═════════════════════════════════════
    //  REFUNDS
    // ═════════════════════════════════════

    private async Task ValidateCreditNoteBusinessPartnerAsync(CreateCreditNoteDto dto, Guid tenantId)
    {
        if (dto.BusinessPartnerId == Guid.Empty)
            throw new InvalidOperationException("AR credit note business partner is required.");

        var businessPartner = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(p => p.TenantId == tenantId && p.Id == dto.BusinessPartnerId && p.IsActive && !p.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("AR credit note business partner was not found or is inactive for this tenant.");

        if (dto.OriginalInvoiceId.HasValue)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == tenantId && i.Id == dto.OriginalInvoiceId.Value && !i.IsDeleted)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("AR credit note original invoice was not found for this tenant.");

            if (invoice.BusinessPartnerId != businessPartner.Id)
                throw new InvalidOperationException("AR credit note business partner must match the original invoice.");
        }

        if (dto.ReturnOrderId.HasValue)
        {
            var returnOrder = await _returnRepo.GetQueryable(r => r.TenantId == tenantId && r.Id == dto.ReturnOrderId.Value && !r.IsDeleted)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("AR credit note return order was not found for this tenant.");

            if (returnOrder.BusinessPartnerId != businessPartner.Id)
                throw new InvalidOperationException("AR credit note business partner must match the return order.");
        }
    }

    private async Task ValidateRefundBusinessPartnerAsync(CreateRefundDto dto, Guid tenantId)
    {
        if (dto.BusinessPartnerId == Guid.Empty)
            throw new InvalidOperationException("Refund business partner is required.");

        var businessPartnerExists = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(p => p.TenantId == tenantId && p.Id == dto.BusinessPartnerId && p.IsActive && !p.IsDeleted)
            .AnyAsync();
        if (!businessPartnerExists)
            throw new InvalidOperationException("Refund business partner was not found or is inactive for this tenant.");

        if (dto.CreditNoteId.HasValue)
        {
            var creditNote = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == dto.CreditNoteId.Value && !c.IsDeleted)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Refund credit note was not found for this tenant.");

            if (creditNote.BusinessPartnerId != dto.BusinessPartnerId)
                throw new InvalidOperationException("Refund business partner must match the credit note.");
        }

        if (dto.ReturnOrderId.HasValue)
        {
            var returnOrder = await _returnRepo.GetQueryable(r => r.TenantId == tenantId && r.Id == dto.ReturnOrderId.Value && !r.IsDeleted)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Refund return order was not found for this tenant.");

            if (returnOrder.BusinessPartnerId != dto.BusinessPartnerId)
                throw new InvalidOperationException("Refund business partner must match the return order.");
        }
    }

    private async Task<CreditNote> LoadCreditNoteForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserProvider.TenantId;
        var creditNote = await _creditNoteRepo.GetQueryable(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.OriginalInvoice)
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(cancellationToken);

        if (creditNote == null)
            throw new KeyNotFoundException($"Credit Note {id} not found.");

        return creditNote;
    }

    private async Task<ProducerAccountingIntentDto> BuildSalesCreditNoteProducerIntentAsync(
        CreditNote creditNote,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserProvider.TenantId;
        if (creditNote.TenantId != tenantId)
            throw new InvalidOperationException("AR credit note belongs to another tenant.");

        if (creditNote.CreditNoteStatus == CreditNoteStatus.Reversed)
            throw new InvalidOperationException("Reversed AR credit notes cannot be posted again. Create a new approved credit note if a correction is required.");

        var existingPostedEvent = creditNote.JournalEntryId.HasValue ||
            await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == tenantId &&
                    e.SourceDocumentType == "SalesCreditNote" &&
                    e.SourceDocumentId == creditNote.Id &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted" &&
                    e.JournalEntryId.HasValue &&
                    !e.IsDeleted)
                .AnyAsync(cancellationToken);

        if (!existingPostedEvent && creditNote.CreditNoteStatus != CreditNoteStatus.Approved)
            throw new InvalidOperationException("AR credit note workflow approval is not complete.");

        if (creditNote.TotalAmount <= 0m)
            throw new InvalidOperationException("AR credit note amount must be positive.");

        if (creditNote.BusinessPartner == null || creditNote.BusinessPartner.TenantId != tenantId)
            throw new InvalidOperationException("AR credit note customer was not found for this tenant.");

        if (!creditNote.BusinessPartner.IsActive)
            throw new InvalidOperationException($"Customer '{creditNote.BusinessPartner.PartnerName}' is not active for AR credit note posting.");

        var activeLines = creditNote.Lines?
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .ToList() ?? new List<CreditNoteLine>();

        if (activeLines.Count == 0)
            throw new InvalidOperationException("AR credit note must have at least one line before posting.");

        foreach (var line in activeLines)
        {
            if (line.TenantId != tenantId || line.CreditNoteId != creditNote.Id)
                throw new InvalidOperationException("AR credit note line belongs to another tenant or document.");
            if (line.Quantity <= 0m || line.UnitPrice < 0m || line.TaxAmount < 0m)
                throw new InvalidOperationException("AR credit note line quantities, unit prices, and tax amounts must be valid non-negative values.");
        }

        if (creditNote.OriginalInvoiceId.HasValue)
        {
            if (creditNote.AppliedToInvoiceId.HasValue && creditNote.AppliedToInvoiceId != creditNote.OriginalInvoiceId)
                throw new InvalidOperationException("AR credit note has conflicting original and application invoice references.");

            var originalInvoice = creditNote.OriginalInvoice;
            if (originalInvoice == null || originalInvoice.TenantId != tenantId)
                throw new InvalidOperationException("AR credit note original invoice was not found for this tenant.");
            if (originalInvoice.BusinessPartnerId != creditNote.BusinessPartnerId)
                throw new InvalidOperationException("AR credit note and original invoice must use the same business partner.");

            if (!originalInvoice.JournalEntryId.HasValue)
                throw new InvalidOperationException($"AR credit note cannot post against unposted invoice '{originalInvoice.InvoiceNumber}'.");

            var invoicePostingExists = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == tenantId &&
                    e.SourceDocumentType == "CustomerInvoice" &&
                    e.SourceDocumentId == originalInvoice.Id &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted" &&
                    e.JournalEntryId.HasValue &&
                    !e.IsDeleted)
                .AnyAsync(cancellationToken);

            if (!invoicePostingExists)
                throw new InvalidOperationException($"AR credit note cannot post against invoice '{originalInvoice.InvoiceNumber}' because its central posting event was not found.");

            var postedCreditTotal = await GetPostedSalesCreditTotalForInvoiceAsync(
                originalInvoice.Id,
                creditNote.Id,
                cancellationToken);
            var eligibleCreditAmount = RoundMoney(originalInvoice.TotalAmount - postedCreditTotal);
            if (RoundMoney(creditNote.TotalAmount) > eligibleCreditAmount)
                throw new InvalidOperationException($"AR credit note would exceed eligible credit amount for invoice '{originalInvoice.InvoiceNumber}'.");
        }

        var settings = await GetFinanceSettingsAsync(cancellationToken);
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var creditNoteCurrency = NormalizeCurrency(creditNote.Currency, functionalCurrency);
        var exchangeRate = NormalizeExchangeRate(creditNote.ExchangeRate);
        var accountCache = new Dictionary<Guid, Account>();

        var arAccountId = creditNote.BusinessPartner.DefaultArAccountId
            ?? settings.ControlAccountArId
            ?? throw new InvalidOperationException("AR control account is not configured for this tenant.");
        await ResolveCreditNotePostingAccountAsync(arAccountId, "AR control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

        var salesReturnsAccountId = settings.DiscountAllowedAccountId
            ?? throw new InvalidOperationException("Sales returns/allowance account is not configured for this tenant.");
        await ResolveCreditNotePostingAccountAsync(salesReturnsAccountId, "sales returns/allowance account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

        var lineSubtotal = RoundMoney(activeLines.Sum(l => l.Quantity * l.UnitPrice));
        var taxAmount = RoundMoney(creditNote.TaxAmount > 0m
            ? creditNote.TaxAmount
            : activeLines.Sum(l => l.TaxAmount));
        var totalAmount = RoundMoney(creditNote.TotalAmount > 0m ? creditNote.TotalAmount : lineSubtotal + taxAmount);

        if (totalAmount != RoundMoney(lineSubtotal + taxAmount))
            throw new InvalidOperationException("AR credit note amount does not match posting line totals.");

        var postingLines = new List<FinancePostingLineDto>();
        var lineNumber = 1;

        postingLines.Add(BuildCreditNotePostingLine(
            salesReturnsAccountId,
            $"Sales credit note {creditNote.DocumentNumber}",
            debitTransactionAmount: lineSubtotal,
            creditTransactionAmount: 0m,
            creditNoteCurrency,
            functionalCurrency,
            exchangeRate,
            creditNote.DocumentDate,
            creditNote.DocumentNumber,
            lineNumber++,
            "AR-CreditNote-SalesReturn"));

        if (taxAmount > 0m)
        {
            var taxAccountId = settings.ControlAccountTaxId
                ?? throw new InvalidOperationException("Tax control account is not configured for AR credit note tax reversal.");
            await ResolveCreditNotePostingAccountAsync(taxAccountId, "output tax reversal account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            postingLines.Add(BuildCreditNotePostingLine(
                taxAccountId,
                $"Reverse output tax - {creditNote.DocumentNumber}",
                debitTransactionAmount: taxAmount,
                creditTransactionAmount: 0m,
                creditNoteCurrency,
                functionalCurrency,
                exchangeRate,
                creditNote.DocumentDate,
                creditNote.DocumentNumber,
                lineNumber++,
                "AR-CreditNote-Tax"));
        }

        postingLines.Add(BuildCreditNotePostingLine(
            arAccountId,
            $"Sales credit note {creditNote.DocumentNumber}",
            debitTransactionAmount: 0m,
            creditTransactionAmount: totalAmount,
            creditNoteCurrency,
            functionalCurrency,
            exchangeRate,
            creditNote.DocumentDate,
            creditNote.DocumentNumber,
            lineNumber++,
            "AR-Control"));

        if (RoundMoney(postingLines.Sum(l => l.DebitAmount)) != RoundMoney(postingLines.Sum(l => l.CreditAmount)))
            throw new InvalidOperationException("AR credit note posting is not balanced.");

        var owner = OwnerEffectFor(creditNote, "POST", "POST");
        return new ProducerAccountingIntentDto
        {
            AccountingEventId = DeterministicGuid(creditNote.Id, "AR-CREDIT-NOTE-POST"),
            EventKind = AccountingEventKinds.Original,
            IdempotencyKey = SalesCreditNotePostingKey(creditNote),
            ParticipantIdentity = owner.ParticipantCode,
            ExpectedOwnerEffect = owner,
            PostingRequest = new ProducerFinancePostingRequestDto
            {
                SourceModule = "AR", OriginModuleCode = "SALES", SourceDocumentType = "SalesCreditNote",
                SourceDocumentId = creditNote.Id, SourceDocumentTenantId = creditNote.TenantId, PostingAction = "Post",
                SourceDocumentReference = creditNote.DocumentNumber,
                Description = $"Sales credit note {creditNote.DocumentNumber} - {creditNote.BusinessPartner.PartnerName}",
                PostingDate = creditNote.DocumentDate, JournalType = "AR Credit Note", FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = SalesCreditNotePostingKey(creditNote), ReturnExistingOnDuplicate = true, Lines = postingLines
            }
        };
    }

    private IFinanceProducerIntentService RequireProducerIntents() => _financeProducerIntents
        ?? throw new InvalidOperationException("Governed Finance producer intents are not configured for AR credit note posting.");
    private IFinanceProducerApprovedExecutionService RequireProducerExecution() => _financeProducerExecution
        ?? throw new InvalidOperationException("Governed Finance producer execution is not configured for AR credit note posting.");
    private IFinanceProducerReversalPreparationService RequireProducerReversals() => _financeProducerReversals
        ?? throw new InvalidOperationException("Governed Finance producer reversal preparation is not configured for AR credit note reversal.");
    private IFinanceProducerReplayVerificationService RequireReplayVerifier() => _financeProducerReplayVerifier
        ?? throw new InvalidOperationException("Governed Finance producer replay verification is not configured for AR credit note posting.");

    private static string SalesCreditNotePostingKey(CreditNote creditNote) =>
        $"AR:SalesCreditNote:{creditNote.TenantId:N}:{creditNote.Id:N}:Post";
    private static string SalesCreditNoteReversalKey(CreditNote creditNote) =>
        $"AR:SalesCreditNote:{creditNote.TenantId:N}:{creditNote.Id:N}:Reverse";
    private static string CreditNoteExecutionLock(CreditNote creditNote) =>
        creditNote.OriginalInvoiceId.HasValue
            ? $"SALES:CREDIT_NOTE:INVOICE:{creditNote.TenantId:N}:{creditNote.OriginalInvoiceId.Value:N}"
            : $"SALES:CREDIT_NOTE:STANDALONE:{creditNote.TenantId:N}:{creditNote.Id:N}";
    private static Guid DeterministicGuid(Guid source, string purpose) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{source:N}:{purpose}"))[..16]);
    private static ProducerOwnerEffectIdentityDto OwnerEffectFor(CreditNote creditNote, string action, string salt)
    {
        const string participant = "SALES.CREDIT_NOTE.V1";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{participant}:{creditNote.TenantId:N}:{creditNote.Id:N}:{action}:{salt}")));
        return new ProducerOwnerEffectIdentityDto { ParticipantCode = participant, OwnerEntityType = "SALES_CREDIT_NOTE",
            OwnerEntityId = creditNote.Id, OwnerAction = action, EffectFingerprint = fingerprint };
    }
    private static ProducerOwnerEffectReceiptDto ReceiptFor(CreditNote creditNote, ProducerOwnerEffectIdentityDto effect) => new()
    {
        TenantId = creditNote.TenantId, ParticipantCode = effect.ParticipantCode, OwnerEntityType = effect.OwnerEntityType,
        OwnerEntityId = effect.OwnerEntityId, OwnerAction = effect.OwnerAction, EffectFingerprint = effect.EffectFingerprint
    };
    private static bool OwnerEffectsMatch(ProducerOwnerEffectIdentityDto expected, ProducerOwnerEffectIdentityDto actual) =>
        expected.ParticipantCode == actual.ParticipantCode && expected.OwnerEntityType == actual.OwnerEntityType &&
        expected.OwnerEntityId == actual.OwnerEntityId && expected.OwnerAction == actual.OwnerAction &&
        expected.EffectFingerprint == actual.EffectFingerprint;
    private static FinanceProducerReplayVerificationRequestDto ReplayRequest(CreditNote creditNote, string action, string salt,
        string requestFingerprint, Guid postingEventId, Guid journalEntryId)
    {
        var owner = OwnerEffectFor(creditNote, action, salt);
        return new FinanceProducerReplayVerificationRequestDto
        {
            AccountingEventRequestFingerprint = requestFingerprint, OriginatingModuleCode = "SALES",
            SourceDocumentType = "SalesCreditNote", SourceDocumentId = creditNote.Id, PostingAction = "Post",
            ParticipantIdentity = owner.ParticipantCode, OwnerEffectReceipt = ReceiptFor(creditNote, owner),
            FinancePostingEventId = postingEventId, JournalEntryId = journalEntryId
        };
    }
    private static void RequireApprovedPreparedAuthority(AccountingEventDto prepared, AccountingEventDto decision)
    {
        if (decision.Id != prepared.Id || decision.RequestFingerprint != prepared.RequestFingerprint
            || decision.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved)
            throw new InvalidOperationException("AR credit note requires an unchanged independent Finance approval before posting.");
    }
    private static void ValidateCompatibility(AccountingEventDto prepared, FinanceProducerApprovedExecutionResultDto result)
    {
        if (result.AccountingEventId != prepared.Id || result.AccountingEventRequestFingerprint != prepared.RequestFingerprint
            || result.Status != AccountingEventStatuses.Posted || result.FinancePostingEventId == Guid.Empty || result.JournalEntryId == Guid.Empty)
            throw new InvalidOperationException("AR credit note compatibility evidence does not bind to the prepared Finance event.");
    }

    private async Task RequireExactPostedReplayAsync(CreditNote creditNote, CancellationToken cancellationToken)
    {
        if (!creditNote.JournalEntryId.HasValue)
            throw new InvalidOperationException("AR credit note replay requires a linked journal identity.");
        var tenantId = _currentUserProvider.TenantId;
        var eventId = DeterministicGuid(creditNote.Id, "AR-CREDIT-NOTE-POST");
        var prepared = await RequireProducerIntents().GetAsync(eventId, cancellationToken);
        var finance = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(posting =>
                posting.TenantId == tenantId &&
                posting.SourceModule == "AR" && posting.SourceDocumentType == "SalesCreditNote" &&
                posting.SourceDocumentId == creditNote.Id && posting.PostingAction == "Post" &&
                posting.JournalEntryId == creditNote.JournalEntryId && posting.PostingStatus == "Posted" && !posting.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);
        if (finance is null)
            throw new InvalidOperationException("AR credit note replay compatibility posting link is missing or conflicts with the governed event.");
        await RequireReplayVerifier().VerifyPostedAsync(eventId, ReplayRequest(creditNote, "POST", "POST", prepared.RequestFingerprint,
            finance.Id, creditNote.JournalEntryId.Value), cancellationToken);
    }

    private async Task RequireExactReversalReplayAsync(CreditNote creditNote, CancellationToken cancellationToken)
    {
        if (!creditNote.ReversalJournalEntryId.HasValue || !creditNote.ReversalPostingEventId.HasValue)
            throw new InvalidOperationException("AR credit note reversal replay requires immutable compatibility identities.");
        var tenantId = _currentUserProvider.TenantId;
        var eventId = DeterministicGuid(creditNote.Id, "AR-CREDIT-NOTE-REVERSAL");
        var prepared = await RequireProducerIntents().GetAsync(eventId, cancellationToken);
        var originalId = DeterministicGuid(creditNote.Id, "AR-CREDIT-NOTE-POST");
        var owner = OwnerEffectFor(creditNote, "REVERSE", $"{originalId:N}:{creditNote.ReversedAt!.Value.Date:O}:{creditNote.ReversalReason}");
        await RequireReplayVerifier().VerifyPostedAsync(eventId, new FinanceProducerReplayVerificationRequestDto
        {
            AccountingEventRequestFingerprint = prepared.RequestFingerprint, OriginatingModuleCode = "SALES",
            SourceDocumentType = "SalesCreditNote", SourceDocumentId = creditNote.Id, PostingAction = "Post",
            ParticipantIdentity = owner.ParticipantCode, OwnerEffectReceipt = ReceiptFor(creditNote, owner),
            FinancePostingEventId = creditNote.ReversalPostingEventId.Value, JournalEntryId = creditNote.ReversalJournalEntryId.Value
        }, cancellationToken);
    }

    private async Task<decimal> GetPostedSalesCreditTotalForInvoiceAsync(
        Guid invoiceId,
        Guid? excludedCreditNoteId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserProvider.TenantId;
        var postedCreditNoteIds = await _unitOfWork.Repository<FinancePostingEvent>()
            .GetQueryable(e =>
                e.TenantId == tenantId &&
                e.SourceModule == "AR" &&
                e.SourceDocumentType == "SalesCreditNote" &&
                e.PostingAction == "Post" &&
                e.PostingStatus == "Posted" &&
                e.JournalEntryId.HasValue &&
                !e.IsDeleted)
            .Select(e => e.SourceDocumentId)
            .ToListAsync(cancellationToken);

        if (postedCreditNoteIds.Count == 0)
            return 0m;

        // Posted events, rather than CreditNote.CreditedAmount-style snapshots, are the
        // authoritative prior-credit facts used to prevent concurrent over-crediting.
        return RoundMoney(await _creditNoteRepo
            .GetQueryable(c =>
            c.TenantId == tenantId &&
            (!excludedCreditNoteId.HasValue || c.Id != excludedCreditNoteId.Value) &&
            c.OriginalInvoiceId == invoiceId &&
            c.CreditNoteStatus != CreditNoteStatus.Reversed &&
            postedCreditNoteIds.Contains(c.Id) &&
                !c.IsDeleted)
            .SumAsync(c => c.TotalAmount, cancellationToken));
    }

    private async Task<decimal> GetPostedCustomerReceiptSettlementForInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserProvider.TenantId;
        var postedReceiptIds = await _unitOfWork.Repository<FinancePostingEvent>()
            .GetQueryable(e =>
                e.TenantId == tenantId &&
                e.SourceModule == "AR" &&
                e.SourceDocumentType == "CustomerPayment" &&
                e.PostingAction == "Post" &&
                e.PostingStatus == "Posted" &&
                e.JournalEntryId.HasValue &&
                !e.IsDeleted)
            .Select(e => e.SourceDocumentId)
            .ToListAsync(cancellationToken);

        if (postedReceiptIds.Count == 0)
            return 0m;

        return RoundMoney(await _unitOfWork.Repository<PaymentAllocation>()
            .GetQueryable(a =>
                a.TenantId == tenantId &&
                a.InvoiceId == invoiceId &&
                !a.IsDeleted &&
                !a.IsReversal &&
                postedReceiptIds.Contains(a.CustomerPaymentId) &&
                a.CustomerPayment != null &&
                !a.CustomerPayment.IsCreditNote)
            .SumAsync(a => a.AllocatedAmount + a.DiscountAmount, cancellationToken));
    }

    private async Task<FinanceSettings> GetFinanceSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserProvider.TenantId;
        var settings = await _unitOfWork.Repository<FinanceSettings>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
    }

    private async Task<Account> ResolveCreditNotePostingAccountAsync(
        Guid accountId,
        string role,
        Dictionary<Guid, Account> accountCache,
        bool allowControlAccount,
        bool requireDirectPosting,
        CancellationToken cancellationToken)
    {
        if (accountCache.TryGetValue(accountId, out var cached))
        {
            return cached;
        }

        var tenantId = _currentUserProvider.TenantId;
        var account = await _unitOfWork.Repository<Account>()
            .GetQueryable(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (account == null)
            throw new InvalidOperationException($"AR credit note posting {role} was not found for this tenant.");

        if (account.Status != AccountStatus.Active)
            throw new InvalidOperationException($"AR credit note posting {role} account '{account.AccountNumber}' is not active.");

        if (account.IsControlAccount && !allowControlAccount)
            throw new InvalidOperationException($"AR credit note posting {role} account '{account.AccountNumber}' is a control account and cannot be used for this line.");

        if (requireDirectPosting && !account.AllowDirectPosting)
            throw new InvalidOperationException($"AR credit note posting {role} account '{account.AccountNumber}' does not allow direct posting.");

        accountCache[accountId] = account;
        return account;
    }

    private static FinancePostingLineDto BuildCreditNotePostingLine(
        Guid accountId,
        string description,
        decimal debitTransactionAmount,
        decimal creditTransactionAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate,
        DateTime exchangeRateDate,
        string reference,
        int lineNumber,
        string transactionTag)
    {
        var isForeign = !string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        var debitAmount = ToFunctionalAmount(debitTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);
        var creditAmount = ToFunctionalAmount(creditTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);

        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            TransactionCurrency = transactionCurrency,
            ForeignCurrencyAmount = isForeign
                ? debitTransactionAmount > 0m ? debitTransactionAmount : creditTransactionAmount
                : null,
            ExchangeRate = isForeign ? exchangeRate : null,
            ExchangeRateSource = isForeign ? "AR credit note exchange-rate snapshot" : null,
            ExchangeRateDate = isForeign ? exchangeRateDate.Date : null,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            TransactionTag = transactionTag
        };
    }

    private async Task RecordArCreditNoteAuditAsync(
        string eventType,
        CreditNote creditNote,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = creditNote.TenantId,
            SourceModule = "AR",
            SourceDocumentType = "SalesCreditNote",
            SourceDocumentId = creditNote.Id,
            JournalEntryId = journalEntryId ?? creditNote.JournalEntryId,
            PostingEventId = postingEventId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = "Finance.ARCreditNote",
            ResourceId = creditNote.Id.ToString()
        }, cancellationToken);
    }

    private static decimal ToFunctionalAmount(
        decimal transactionAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate)
    {
        if (transactionAmount == 0m)
        {
            return 0m;
        }

        var normalizedRate = NormalizeExchangeRate(exchangeRate);
        return string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            ? RoundMoney(transactionAmount)
            : RoundMoney(transactionAmount * normalizedRate);
    }

    private static decimal NormalizeExchangeRate(decimal exchangeRate)
        => exchangeRate <= 0m ? 1m : exchangeRate;

    private static string NormalizeCurrency(string? currencyCode, string defaultValue)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? defaultValue.Trim().ToUpperInvariant()
            : currencyCode.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    public async Task<RefundDetailDto> CreateRefundAsync(CreateRefundDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        await ValidateRefundBusinessPartnerAsync(dto, tenantId);
        var docNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Sales,
            SalesDocumentTypes.Refund,
            tenantId,
            DateTime.UtcNow,
            nameof(Refund));
        var refund = new Refund
        {
            DocumentNumber = docNumber,
            DocumentDate = DateTime.UtcNow,
            BusinessPartnerId = dto.BusinessPartnerId,
            CreditNoteId = dto.CreditNoteId,
            ReturnOrderId = dto.ReturnOrderId,
            RefundStatus = RefundStatus.PendingApproval,
            RefundAmount = dto.RefundAmount,
            TotalAmount = dto.RefundAmount,
            RefundMethod = dto.RefundMethod,
            Reason = dto.Reason,
            TenantId = tenantId
        };

        await _refundRepo.AddAsync(refund);
        await _unitOfWork.SaveChangesAsync();

        var workflowResult = await _workflowIntegrationService.SubmitAsync("Refund", refund.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            refund.RefundStatus = RefundStatus.Draft;
            await _refundRepo.UpdateAsync(refund);
            await _unitOfWork.SaveChangesAsync();
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to start refund approval workflow.");
        }

        _logger.LogInformation("Created Refund {DocNumber} for {Amount}", docNumber, dto.RefundAmount);
        return await GetRefundByIdAsync(refund.Id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto?> GetRefundByIdAsync(Guid id)
    {
        var tenantId = _currentUserProvider.TenantId;
        var refund = await _refundRepo.GetQueryable(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted)
            .Include(r => r.BusinessPartner)
            .Include(r => r.CreditNote)
            .Include(r => r.ProcessedBy)
            .FirstOrDefaultAsync();
        return refund == null ? null : MapRefundDetailDto(refund);
    }

    public async Task<PagedResult<RefundSummaryDto>> GetRefundsAsync(
        int page = 1, int pageSize = 20,
        string? search = null, RefundStatus? status = null,
        Guid? businessPartnerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var query = _refundRepo.GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => r.DocumentNumber.Contains(search));
        if (status.HasValue)
            query = query.Where(r => r.RefundStatus == status.Value);
        if (businessPartnerId.HasValue)
            query = query.Where(r => r.BusinessPartnerId == businessPartnerId.Value);
        if (startDate.HasValue)
            query = query.Where(r => r.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(r => r.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(r => r.BusinessPartner)
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

    public async Task<RefundDetailDto> SubmitRefundForApprovalAsync(Guid id)
    {
        var refund = await _refundRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Refund {id} not found");

        if (refund.RefundStatus != RefundStatus.Draft)
            throw new InvalidOperationException("Only draft refunds can be submitted for approval");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User is not authenticated");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(RefundWorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start refund workflow");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(RefundWorkflowEntityType);
        adapter.ApplySubmitOutcome(refund, workflowResult.Outcome, userId);

        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Refund {DocNumber} submitted for approval", refund.DocumentNumber);
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto> ProcessRefundApprovalAsync(Guid id, RefundApprovalDto dto)
    {
        var refund = await _refundRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Refund {id} not found");

        if (refund.RefundStatus != RefundStatus.PendingApproval)
            throw new InvalidOperationException("Only pending approval refunds can be approved or rejected");

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User is not authenticated");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(RefundWorkflowEntityType, id, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned to approve the current workflow step");

        var comments = dto.IsApproved
            ? dto.Comments
            : dto.RejectionReason ?? dto.Comments;

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            RefundWorkflowEntityType,
            id,
            userId,
            dto.IsApproved ? "Approve" : "Reject",
            comments);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process refund workflow approval");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(RefundWorkflowEntityType);
        adapter.ApplyApprovalOutcome(refund, workflowResult.Outcome, userId, comments);

        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Refund {DocNumber} workflow approval processed. Approved: {Approved}",
            refund.DocumentNumber,
            dto.IsApproved);
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto> ApproveRefundAsync(Guid id)
    {
        var refund = await _refundRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Refund {id} not found");
        if (refund.RefundStatus != RefundStatus.Draft && refund.RefundStatus != RefundStatus.PendingApproval)
            throw new InvalidOperationException("Only draft/pending refunds can be approved");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("Refund", id, _currentUserProvider.UserId, "Approve");
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to process refund approval workflow.");

        if (workflowResult.Outcome == WorkflowOutcome.Pending)
            return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

        if (workflowResult.Outcome == WorkflowOutcome.Rejected)
        {
            refund.RefundStatus = RefundStatus.Rejected;
            await _refundRepo.UpdateAsync(refund);
            await _unitOfWork.SaveChangesAsync();
            return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
        }

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
        await ReleaseSourceAllocationsForRefundAsync(refund, paymentReference);
        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Refund {DocNumber} processed — {Amount} via {Method}", refund.DocumentNumber, refund.RefundAmount, refund.RefundMethod);
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<RefundDetailDto> RejectRefundAsync(Guid id, string? reason = null)
    {
        var refund = await _refundRepo.GetByIdAsync(id) ?? throw new InvalidOperationException($"Refund {id} not found");
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("Refund", id, _currentUserProvider.UserId, "Reject", reason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Unable to process refund rejection workflow.");

        if (workflowResult.Outcome == WorkflowOutcome.Pending)
            return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");

        refund.RefundStatus = RefundStatus.Rejected;
        refund.Reason = $"{refund.Reason}\n[Rejected] {reason}".Trim();
        await _refundRepo.UpdateAsync(refund);
        await _unitOfWork.SaveChangesAsync();
        return await GetRefundByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    private async Task ReleaseSourceAllocationsForRefundAsync(Refund refund, string? paymentReference)
    {
        var salesOrderId = await ResolveRefundSalesOrderIdAsync(refund);
        if (!salesOrderId.HasValue)
        {
            return;
        }

        var activeStatuses = new[] { "Reserved", "PendingApproval", "Approved", "Allocated", "Sold", "Leased" };
        var allocationRepo = _unitOfWork.Repository<SalesAllocation>();
        var historyRepo = _unitOfWork.Repository<SalesAllocationHistory>();
        var allocations = await allocationRepo.GetQueryable()
            .Where(a =>
                a.SalesOrderId == salesOrderId.Value
                && activeStatuses.Contains(a.Status)
                && !a.IsDeleted)
            .ToListAsync();

        if (allocations.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var reason = $"Released after refund {refund.DocumentNumber} was processed";
        if (!string.IsNullOrWhiteSpace(paymentReference))
        {
            reason = $"{reason}. Payment reference: {paymentReference}";
        }

        foreach (var allocation in allocations)
        {
            var previousStatus = allocation.Status;
            allocation.Status = "Released";
            allocation.ReleasedDate = now;
            allocation.ReleaseReason = reason;
            allocation.UpdatedAt = now;
            allocation.UpdatedBy = _currentUserProvider.Username;
            allocation.LastModifiedById = _currentUserProvider.UserId;

            await allocationRepo.UpdateAsync(allocation);
            await historyRepo.AddAsync(new SalesAllocationHistory
            {
                SalesAllocationId = allocation.Id,
                Action = "RefundRelease",
                FromStatus = previousStatus,
                ToStatus = allocation.Status,
                Notes = reason,
                TenantId = allocation.TenantId,
                PerformedById = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
                PerformedByName = _currentUserProvider.Username,
                PerformedAt = now
            });
        }
    }

    private async Task<Guid?> ResolveRefundSalesOrderIdAsync(Refund refund)
    {
        if (refund.ReturnOrderId.HasValue)
        {
            var returnOrder = await _returnRepo.GetByIdAsync(refund.ReturnOrderId.Value);
            return returnOrder?.SalesOrderId;
        }

        if (refund.CreditNoteId.HasValue)
        {
            var creditNote = await _creditNoteRepo.GetByIdAsync(refund.CreditNoteId.Value);
            if (creditNote?.ReturnOrderId.HasValue == true)
            {
                var returnOrder = await _returnRepo.GetByIdAsync(creditNote.ReturnOrderId.Value);
                return returnOrder?.SalesOrderId;
            }
        }

        return null;
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
        CustomerName = r.BusinessPartner?.PartnerName,
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
        CustomerName = r.BusinessPartner?.PartnerName,
        SalesOrderNumber = r.SalesOrder?.DocumentNumber,
        TotalAmount = r.TotalAmount,
        LineCount = r.Lines?.Count ?? 0,
        ReceivedDate = r.ReceivedDate,
        CreatedAt = r.CreatedAt,
        SalesOrderId = r.SalesOrderId,
        DeliveryNoteId = r.DeliveryNoteId,
        BusinessPartnerId = r.BusinessPartnerId,
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
        CustomerName = c.BusinessPartner?.PartnerName,
        TotalAmount = c.TotalAmount,
        Reason = c.Reason,
        AppliedDate = c.AppliedDate,
        JournalEntryId = c.JournalEntryId,
        LineCount = c.Lines?.Count ?? 0,
        CreatedAt = c.CreatedAt
    };

    private static CreditNoteDetailDto MapCreditNoteDetailDto(CreditNote c) => new()
    {
        Id = c.Id,
        DocumentNumber = c.DocumentNumber,
        CreditNoteStatus = c.CreditNoteStatus,
        CustomerName = c.BusinessPartner?.PartnerName,
        TotalAmount = c.TotalAmount,
        Reason = c.Reason,
        AppliedDate = c.AppliedDate,
        JournalEntryId = c.JournalEntryId,
        LineCount = c.Lines?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        BusinessPartnerId = c.BusinessPartnerId,
        ReturnOrderId = c.ReturnOrderId,
        ReturnOrderNumber = c.ReturnOrder?.DocumentNumber,
        OriginalInvoiceId = c.OriginalInvoiceId,
        AppliedToInvoiceId = c.AppliedToInvoiceId,
        TaxAmount = c.TaxAmount,
        ReversalJournalEntryId = c.ReversalJournalEntryId,
        ReversalPostingEventId = c.ReversalPostingEventId,
        ReversedAt = c.ReversedAt,
        ReversalReason = c.ReversalReason,
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
        CustomerName = r.BusinessPartner?.PartnerName,
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
        CustomerName = r.BusinessPartner?.PartnerName,
        RefundAmount = r.RefundAmount,
        RefundMethod = r.RefundMethod,
        ProcessedDate = r.ProcessedDate,
        CreatedAt = r.CreatedAt,
        BusinessPartnerId = r.BusinessPartnerId,
        CreditNoteId = r.CreditNoteId,
        CreditNoteNumber = r.CreditNote?.DocumentNumber,
        ReturnOrderId = r.ReturnOrderId,
        Reason = r.Reason,
        ProcessedByName = r.ProcessedBy?.UserName,
        PaymentReference = r.PaymentReference
    };
}
