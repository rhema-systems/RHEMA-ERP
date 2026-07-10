using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class SalesAllocationService : ISalesAllocationService
{
    private const string WorkflowEntityType = "SalesAllocation";

    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Reserved",
        "Allocated",
        "Sold",
        "Leased",
        "PendingApproval",
        "Approved"
    };

    private static readonly HashSet<string> ReleasedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Released",
        "Cancelled",
        "Expired",
        "Rejected"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ILogger<SalesAllocationService> _logger;

    public SalesAllocationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ILogger<SalesAllocationService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SalesAllocationDto>> GetAllocationsAsync(
        Guid? saleableSourceId = null,
        string? sourceItemId = null,
        string? status = null,
        Guid? businessPartnerId = null,
        Guid? salesOrderId = null,
        Guid? salesAgreementId = null,
        bool activeOnly = false)
    {
        var tenantId = _currentUserProvider.TenantId;
        var query = _unitOfWork.Repository<SalesAllocation>()
            .GetQueryable()
            .Include(x => x.SaleableSource)
            .Include(x => x.BusinessPartner)
            .Include(x => x.SalesOrder)
            .Include(x => x.SalesAgreement)
            .Include(x => x.History)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (saleableSourceId.HasValue)
        {
            query = query.Where(x => x.SaleableSourceId == saleableSourceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sourceItemId))
        {
            var normalizedItemId = NormalizeRequired(sourceItemId, "Source item ID");
            query = query.Where(x => x.SourceItemId == normalizedItemId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (businessPartnerId.HasValue)
        {
            query = query.Where(x => x.BusinessPartnerId == businessPartnerId.Value);
        }

        if (salesOrderId.HasValue)
        {
            query = query.Where(x => x.SalesOrderId == salesOrderId.Value);
        }

        if (salesAgreementId.HasValue)
        {
            query = query.Where(x => x.SalesAgreementId == salesAgreementId.Value);
        }

        if (activeOnly)
        {
            query = query.Where(x => ActiveStatuses.Contains(x.Status));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<SalesAllocationDto?> GetAllocationByIdAsync(Guid id)
    {
        var tenantId = _currentUserProvider.TenantId;
        var allocation = await _unitOfWork.Repository<SalesAllocation>()
            .GetQueryable()
            .Include(x => x.SaleableSource)
            .Include(x => x.BusinessPartner)
            .Include(x => x.SalesOrder)
            .Include(x => x.SalesAgreement)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted);

        return allocation == null ? null : MapToDto(allocation);
    }

    public async Task<SalesAllocationDto> CreateAllocationAsync(CreateSalesAllocationDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var source = await _unitOfWork.Repository<SalesSaleableSource>().FirstOrDefaultAsync(x =>
                x.Id == dto.SaleableSourceId
                && x.TenantId == tenantId
                && !x.IsDeleted)
            ?? throw new InvalidOperationException("The selected saleable source was not found.");

        if (!source.IsActive)
        {
            throw new InvalidOperationException("The selected saleable source is inactive.");
        }

        if (!source.AllowReservations)
        {
            throw new InvalidOperationException("The selected saleable source does not allow reservations or allocations.");
        }

        var normalizedSourceItemId = NormalizeRequired(dto.SourceItemId, "Source item ID");
        var status = NormalizeStatus(dto.Status);
        if (ActiveStatuses.Contains(status)
            && await HasActiveAllocationAsync(source.Id, normalizedSourceItemId))
        {
            throw new InvalidOperationException("This saleable item already has an active reservation or allocation.");
        }

        var allocation = new SalesAllocation
        {
            TenantId = tenantId,
            SaleableSourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = source.AdapterKey,
            SourceItemId = normalizedSourceItemId,
            SourceItemCode = NormalizeOptional(dto.SourceItemCode),
            SourceItemName = NormalizeRequired(dto.SourceItemName, "Source item name"),
            SourceItemType = NormalizeOptional(dto.SourceItemType),
            BusinessPartnerId = dto.BusinessPartnerId,
            CustomerName = await ResolveCustomerNameAsync(dto.BusinessPartnerId, dto.CustomerName),
            LeadId = dto.LeadId,
            OpportunityId = dto.OpportunityId,
            SalesOrderId = dto.SalesOrderId,
            SalesAgreementId = dto.SalesAgreementId,
            AllocationType = NormalizeAllocationType(dto.AllocationType),
            Status = status,
            ReservedUntil = dto.ReservedUntil,
            EffectiveDate = dto.EffectiveDate ?? DateTime.UtcNow,
            EstimatedValue = dto.EstimatedValue,
            AgreedValue = dto.AgreedValue,
            Currency = NormalizeCurrency(dto.Currency ?? source.DefaultCurrency),
            Notes = NormalizeOptional(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<SalesAllocation>().AddAsync(allocation);
        await AddHistoryAsync(allocation, "Created", null, status, dto.Notes);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Created Sales allocation {AllocationId} for source {SourceCode} item {SourceItemId}",
            allocation.Id,
            allocation.SourceCode,
            allocation.SourceItemId);

        return await GetAllocationByIdAsync(allocation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve created allocation.");
    }

    public async Task<SalesAllocationDto> UpdateAllocationStatusAsync(Guid id, UpdateSalesAllocationStatusDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var allocation = await _unitOfWork.Repository<SalesAllocation>().FirstOrDefaultAsync(x =>
                x.Id == id
                && x.TenantId == tenantId
                && !x.IsDeleted)
            ?? throw new InvalidOperationException("The selected sales allocation was not found.");

        var nextStatus = NormalizeStatus(dto.Status);
        if (ActiveStatuses.Contains(nextStatus)
            && await HasActiveAllocationAsync(allocation.SaleableSourceId, allocation.SourceItemId, allocation.Id))
        {
            throw new InvalidOperationException("This saleable item already has another active reservation or allocation.");
        }

        var previousStatus = allocation.Status;
        allocation.Status = nextStatus;
        allocation.SalesOrderId = dto.SalesOrderId ?? allocation.SalesOrderId;
        allocation.SalesAgreementId = dto.SalesAgreementId ?? allocation.SalesAgreementId;
        allocation.AgreedValue = dto.AgreedValue ?? allocation.AgreedValue;
        allocation.ReservedUntil = dto.ReservedUntil ?? allocation.ReservedUntil;
        allocation.UpdatedBy = _currentUserProvider.Username;
        allocation.LastModifiedById = _currentUserProvider.UserId;
        allocation.UpdatedAt = DateTime.UtcNow;

        if (ReleasedStatuses.Contains(nextStatus))
        {
            allocation.ReleasedDate = DateTime.UtcNow;
            allocation.ReleaseReason = NormalizeOptional(dto.ReleaseReason ?? dto.Notes);
        }
        else
        {
            allocation.ReleasedDate = null;
            allocation.ReleaseReason = null;
        }

        await _unitOfWork.Repository<SalesAllocation>().UpdateAsync(allocation);
        await AddHistoryAsync(allocation, "StatusChanged", previousStatus, nextStatus, dto.Notes ?? dto.ReleaseReason);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Updated Sales allocation {AllocationId} from {PreviousStatus} to {NextStatus}",
            allocation.Id,
            previousStatus,
            nextStatus);

        return await GetAllocationByIdAsync(allocation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve updated allocation.");
    }

    public async Task<SalesAllocationDto> SubmitForApprovalAsync(Guid id)
    {
        var tenantId = _currentUserProvider.TenantId;
        var allocation = await _unitOfWork.Repository<SalesAllocation>().FirstOrDefaultAsync(x =>
                x.Id == id
                && x.TenantId == tenantId
                && !x.IsDeleted)
            ?? throw new InvalidOperationException("The selected sales allocation was not found.");

        if (!string.Equals(allocation.Status, "Reserved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only reserved allocations can be submitted for approval.");
        }

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var previousStatus = allocation.Status;
        var workflowResult = await _workflowIntegrationService.SubmitAsync(WorkflowEntityType, id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start allocation workflow.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        adapter.ApplySubmitOutcome(allocation, workflowResult.Outcome, userId);

        allocation.UpdatedBy = _currentUserProvider.Username;
        allocation.LastModifiedById = userId;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<SalesAllocation>().UpdateAsync(allocation);
        await AddHistoryAsync(allocation, "Submitted", previousStatus, allocation.Status, "Submitted for approval");
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Submitted Sales allocation {AllocationId} for approval", allocation.Id);

        return await GetAllocationByIdAsync(allocation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve submitted allocation.");
    }

    public async Task<SalesAllocationDto> ProcessApprovalAsync(Guid id, SalesAllocationApprovalDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var allocation = await _unitOfWork.Repository<SalesAllocation>().FirstOrDefaultAsync(x =>
                x.Id == id
                && x.TenantId == tenantId
                && !x.IsDeleted)
            ?? throw new InvalidOperationException("The selected sales allocation was not found.");

        if (!string.Equals(allocation.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending approval allocations can be approved or rejected.");
        }

        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(WorkflowEntityType, id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned to approve the current workflow step.");
        }

        var previousStatus = allocation.Status;
        var comments = dto.IsApproved
            ? dto.Comments
            : dto.RejectionReason ?? dto.Comments;

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            WorkflowEntityType,
            id,
            userId,
            dto.IsApproved ? "Approve" : "Reject",
            comments);

        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process allocation workflow approval.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(WorkflowEntityType);
        adapter.ApplyApprovalOutcome(allocation, workflowResult.Outcome, userId, comments);

        allocation.UpdatedBy = _currentUserProvider.Username;
        allocation.LastModifiedById = userId;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<SalesAllocation>().UpdateAsync(allocation);
        await AddHistoryAsync(
            allocation,
            dto.IsApproved ? "Approved" : "Rejected",
            previousStatus,
            allocation.Status,
            comments);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Processed Sales allocation {AllocationId} approval. Approved: {Approved}",
            allocation.Id,
            dto.IsApproved);

        return await GetAllocationByIdAsync(allocation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve approved allocation.");
    }

    public async Task<SalesAllocationDto> TransferAllocationAsync(Guid id, TransferSalesAllocationDto dto)
    {
        var tenantId = _currentUserProvider.TenantId;
        var allocation = await _unitOfWork.Repository<SalesAllocation>()
            .GetQueryable()
            .Include(x => x.BusinessPartner)
            .FirstOrDefaultAsync(x =>
                x.Id == id
                && x.TenantId == tenantId
                && !x.IsDeleted)
            ?? throw new InvalidOperationException("The selected sales allocation was not found.");

        if (ReleasedStatuses.Contains(allocation.Status))
        {
            throw new InvalidOperationException("Released, cancelled, expired, or rejected allocations cannot be transferred.");
        }

        if (string.Equals(allocation.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Recall or complete the allocation workflow before transferring the allocation.");
        }

        var nextCustomerName = await ResolveCustomerNameAsync(dto.BusinessPartnerId, dto.CustomerName);
        if (!dto.BusinessPartnerId.HasValue && string.IsNullOrWhiteSpace(nextCustomerName))
        {
            throw new InvalidOperationException("Select a customer or enter a transfer recipient name.");
        }

        var previousCustomerName = allocation.CustomerName ?? allocation.BusinessPartner?.PartnerName ?? "Unassigned";
        var previousBusinessPartnerId = allocation.BusinessPartnerId;
        var previousSalesOrderId = allocation.SalesOrderId;
        var previousSalesAgreementId = allocation.SalesAgreementId;

        allocation.BusinessPartnerId = dto.BusinessPartnerId;
        allocation.CustomerName = nextCustomerName;
        allocation.LeadId = dto.LeadId;
        allocation.OpportunityId = dto.OpportunityId;

        if (dto.ClearLinkedDocuments)
        {
            allocation.SalesOrderId = null;
            allocation.SalesAgreementId = null;
        }

        allocation.UpdatedBy = _currentUserProvider.Username;
        allocation.LastModifiedById = _currentUserProvider.UserId;
        allocation.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<SalesAllocation>().UpdateAsync(allocation);

        var transferNotes = BuildTransferNotes(
            previousCustomerName,
            nextCustomerName,
            previousBusinessPartnerId,
            dto.BusinessPartnerId,
            previousSalesOrderId,
            previousSalesAgreementId,
            dto.ClearLinkedDocuments,
            dto.Notes);

        await AddHistoryAsync(allocation, "Transferred", allocation.Status, allocation.Status, transferNotes);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Transferred Sales allocation {AllocationId} from {PreviousCustomer} to {NextCustomer}",
            allocation.Id,
            previousCustomerName,
            nextCustomerName);

        return await GetAllocationByIdAsync(allocation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve transferred allocation.");
    }

    public async Task<bool> HasActiveAllocationAsync(Guid saleableSourceId, string sourceItemId, Guid? excludeAllocationId = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var normalizedSourceItemId = NormalizeRequired(sourceItemId, "Source item ID");

        var query = _unitOfWork.Repository<SalesAllocation>().GetQueryable()
            .Where(x =>
                x.TenantId == tenantId
                && x.SaleableSourceId == saleableSourceId
                && x.SourceItemId == normalizedSourceItemId
                && !x.IsDeleted
                && ActiveStatuses.Contains(x.Status));

        if (excludeAllocationId.HasValue)
        {
            query = query.Where(x => x.Id != excludeAllocationId.Value);
        }

        return await query.AnyAsync();
    }

    private async Task<string?> ResolveCustomerNameAsync(Guid? businessPartnerId, string? suppliedCustomerName)
    {
        var normalized = NormalizeOptional(suppliedCustomerName);
        if (!businessPartnerId.HasValue)
        {
            return normalized;
        }

        var partner = await _unitOfWork.Repository<BusinessPartner>().FirstOrDefaultAsync(x =>
            x.Id == businessPartnerId.Value
            && x.TenantId == _currentUserProvider.TenantId
            && !x.IsDeleted);

        return partner?.PartnerName ?? normalized;
    }

    private async Task AddHistoryAsync(
        SalesAllocation allocation,
        string action,
        string? fromStatus,
        string toStatus,
        string? notes)
    {
        var history = new SalesAllocationHistory
        {
            TenantId = allocation.TenantId,
            SalesAllocationId = allocation.Id,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            PerformedById = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
            PerformedByName = NormalizeOptional(_currentUserProvider.FullName) ?? _currentUserProvider.Username,
            PerformedAt = DateTime.UtcNow,
            Notes = NormalizeOptional(notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<SalesAllocationHistory>().AddAsync(history);
    }

    private static SalesAllocationDto MapToDto(SalesAllocation allocation) => new()
    {
        Id = allocation.Id,
        TenantId = allocation.TenantId,
        SaleableSourceId = allocation.SaleableSourceId,
        SourceCode = allocation.SourceCode,
        SourceType = allocation.SourceType,
        AdapterKey = allocation.AdapterKey,
        SourceItemId = allocation.SourceItemId,
        SourceItemCode = allocation.SourceItemCode,
        SourceItemName = allocation.SourceItemName,
        SourceItemType = allocation.SourceItemType,
        BusinessPartnerId = allocation.BusinessPartnerId,
        CustomerName = allocation.CustomerName ?? allocation.BusinessPartner?.PartnerName,
        LeadId = allocation.LeadId,
        OpportunityId = allocation.OpportunityId,
        SalesOrderId = allocation.SalesOrderId,
        SalesOrderNumber = allocation.SalesOrder?.DocumentNumber,
        SalesAgreementId = allocation.SalesAgreementId,
        SalesAgreementTitle = allocation.SalesAgreement?.AgreementTitle,
        AllocationType = allocation.AllocationType,
        Status = allocation.Status,
        ReservedUntil = allocation.ReservedUntil,
        EffectiveDate = allocation.EffectiveDate,
        ReleasedDate = allocation.ReleasedDate,
        EstimatedValue = allocation.EstimatedValue,
        AgreedValue = allocation.AgreedValue,
        Currency = allocation.Currency,
        Notes = allocation.Notes,
        ReleaseReason = allocation.ReleaseReason,
        CreatedAt = allocation.CreatedAt,
        UpdatedAt = allocation.UpdatedAt,
        History = allocation.History
            .OrderByDescending(history => history.PerformedAt)
            .Select(history => new SalesAllocationHistoryDto
            {
                Id = history.Id,
                SalesAllocationId = history.SalesAllocationId,
                Action = history.Action,
                FromStatus = history.FromStatus,
                ToStatus = history.ToStatus,
                PerformedById = history.PerformedById,
                PerformedByName = history.PerformedByName,
                PerformedAt = history.PerformedAt,
                Notes = history.Notes
            })
            .ToList()
    };

    private static string NormalizeRequired(string value, string label)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Reserved" : value.Trim();

    private static string NormalizeAllocationType(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Reservation" : value.Trim();

    private static string NormalizeCurrency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
        return normalized.Length > 10 ? normalized[..10] : normalized;
    }

    private static string BuildTransferNotes(
        string previousCustomerName,
        string? nextCustomerName,
        Guid? previousBusinessPartnerId,
        Guid? nextBusinessPartnerId,
        Guid? previousSalesOrderId,
        Guid? previousSalesAgreementId,
        bool clearedLinkedDocuments,
        string? notes)
    {
        var parts = new List<string>
        {
            $"Transferred from {previousCustomerName} to {nextCustomerName ?? "Unassigned"}."
        };

        if (previousBusinessPartnerId.HasValue || nextBusinessPartnerId.HasValue)
        {
            parts.Add($"Business partner changed from {previousBusinessPartnerId?.ToString() ?? "none"} to {nextBusinessPartnerId?.ToString() ?? "none"}.");
        }

        if (clearedLinkedDocuments && (previousSalesOrderId.HasValue || previousSalesAgreementId.HasValue))
        {
            parts.Add("Linked Sales Order/Agreement references were cleared because the allocation holder changed.");
        }

        parts.Add("External ownership register sync is pending the Land Management/property register integration.");

        var normalizedNotes = NormalizeOptional(notes);
        if (!string.IsNullOrWhiteSpace(normalizedNotes))
        {
            parts.Add(normalizedNotes);
        }

        return string.Join(" ", parts);
    }
}
