using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF REQUISITION SERVICE
// ============================================================================

#region Staff Requisition Service

public class StaffRequisitionService : IStaffRequisitionService
{
    private readonly IStaffRequisitionRepository _requisitionRepository;
    private readonly IStaffRequisitionCostRepository _costRepository;
    private readonly IStaffRequisitionAttachmentRepository _attachmentRepository;
    private readonly IStaffRequisitionCommentRepository _commentRepository;
    private readonly IStaffRequisitionHistoryRepository _historyRepository;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffRequisitionService> _logger;

    public StaffRequisitionService(
        IStaffRequisitionRepository requisitionRepository,
        IStaffRequisitionCostRepository costRepository,
        IStaffRequisitionAttachmentRepository attachmentRepository,
        IStaffRequisitionCommentRepository commentRepository,
        IStaffRequisitionHistoryRepository historyRepository,
        ICompanyHrPolicyProvider policyProvider,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffRequisitionService> logger)
    {
        _requisitionRepository = requisitionRepository;
        _costRepository = costRepository;
        _attachmentRepository = attachmentRepository;
        _commentRepository = commentRepository;
        _historyRepository = historyRepository;
        _policyProvider = policyProvider;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A requisition owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffRequisition> GetOwnedAsync(Guid id)
    {
        var entity = await _requisitionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff requisition with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionCost> GetOwnedCostAsync(Guid id)
    {
        var entity = await _costRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition cost with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition attachment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffRequisitionComment> GetOwnedCommentAsync(Guid id)
    {
        var entity = await _commentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Requisition comment with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffRequisitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requisitionRepository.GetWithSummaryNavAsync(id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff requisition with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<StaffRequisitionDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requisitionRepository.GetWithFullDetailsAsync(id);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDetailDto();
    }

    public async Task<StaffRequisitionStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _requisitionRepository.GetSummaryQueryable().Where(r => r.TenantId == tenantId);
        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var total = await query.CountAsync(cancellationToken);

        return new StaffRequisitionStatusSummaryDto
        {
            TotalRequisitions    = total,
            Draft                = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Draft)?.Count ?? 0,
            Submitted            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Submitted)?.Count ?? 0,
            UnderReview          = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.UnderReview)?.Count ?? 0,
            Approved             = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Approved)?.Count ?? 0,
            Rejected             = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Rejected)?.Count ?? 0,
            OnHold               = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.OnHold)?.Count ?? 0,
            Cancelled            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Cancelled)?.Count ?? 0,
            PartiallyFulfilled   = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.PartiallyFulfilled)?.Count ?? 0,
            Fulfilled            = counts.FirstOrDefault(c => c.Status == StaffRequisitionStatus.Fulfilled)?.Count ?? 0,
        };
    }

    public async Task<StaffRequisitionDto?> GetByRequisitionNumberAsync(string requisitionNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requisitionRepository.GetByRequisitionNumberAsync(requisitionNumber);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetSummaryQueryable()
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffRequisitionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = PagingGuard.Clamp(pageNumber, pageSize);

        var tenantId = GetTenantId();
        var query = _requisitionRepository.GetSummaryQueryable().Where(r => r.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.RequestDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffRequisitionSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByOrganizationUnitAsync(organizationUnitId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByLocationAsync(locationId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByStatusAsync(StaffRequisitionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByTypeAsync(StaffRequisitionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByTypeAsync(type);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByRequestedByAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByRequestedByAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetPendingReviewAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetOpenRequisitionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetOpenRequisitionsAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetOverdueAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetByJobVacancyAsync(Guid jobVacancyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetByJobVacancyAsync(jobVacancyId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffRequisitionSummaryDto>> GetUpcomingStartDateAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requisitionRepository.GetUpcomingStartDateAsync(daysAhead);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<StaffRequisitionDto> CreateAsync(CreateStaffRequisitionDto createDto, Guid tenantId, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (!createDto.AllowInternalCandidates && !createDto.AllowExternalCandidates)
            throw new ArgumentException("At least one of Allow Internal Candidates or Allow External Candidates must be selected.");

        var entity = createDto.ToEntity(current, requestedByUserId);
        entity.RequisitionNumber = await GenerateRequisitionNumberAsync(current, cancellationToken);

        await _requisitionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition created: {RequisitionNumber}", entity.RequisitionNumber);

        return entity.ToDto();
    }

    public async Task<StaffRequisitionDto> UpdateAsync(UpdateStaffRequisitionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        if (!updateDto.AllowInternalCandidates && !updateDto.AllowExternalCandidates)
            throw new ArgumentException("At least one of Allow Internal Candidates or Allow External Candidates must be selected.");

        var entity = await GetOwnedAsync(updateDto.Id);

        if (entity.Status != StaffRequisitionStatus.Draft && entity.Status != StaffRequisitionStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected requisitions can be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _requisitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition updated: {RequisitionNumber}", entity.RequisitionNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != StaffRequisitionStatus.Draft)
            throw new InvalidOperationException("Only Draft requisitions can be deleted.");

        await _requisitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition deleted: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> SubmitAsync(SubmitStaffRequisitionDto submitDto, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(submitDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Draft && entity.Status != StaffRequisitionStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected requisitions can be submitted.");

        await EnforceBudgetAsync(entity, "submitted", cancellationToken);

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.Submitted;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = submittedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.Submitted, submittedByUserId, submitDto.Notes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition submitted: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveStaffRequisitionDto approveDto, Guid approvedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(approveDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Submitted && entity.Status != StaffRequisitionStatus.UnderReview)
            throw new InvalidOperationException("Only Submitted or UnderReview requisitions can be approved.");

        // Segregation of duties: a requester must not approve their own headcount request.
        // NOTE: despite its name, approvedByUserId carries the caller's EMPLOYEE id (the controller
        // passes _currentUser.EmployeeId), which is what RequestedById holds — so this compares like
        // with like.
        if (entity.RequestedById == approvedByUserId)
            throw new InvalidOperationException("You cannot approve a requisition that you raised yourself.");

        await EnforceBudgetAsync(entity, "approved", cancellationToken);

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.Approved;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.Approved, approvedByUserId, approveDto.Comments, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition approved: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> RejectAsync(RejectStaffRequisitionDto rejectDto, Guid rejectedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(rejectDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Submitted && entity.Status != StaffRequisitionStatus.UnderReview)
            throw new InvalidOperationException("Only Submitted or UnderReview requisitions can be rejected.");

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.Rejected;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = rejectedByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.Rejected, rejectedByUserId, rejectDto.Comments, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition rejected: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> PutOnHoldAsync(HoldStaffRequisitionDto holdDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(holdDto.RequisitionId);

        var nonHoldableStatuses = new[]
        {
            StaffRequisitionStatus.Cancelled,
            StaffRequisitionStatus.Fulfilled,
            StaffRequisitionStatus.OnHold,
        };

        if (nonHoldableStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"A requisition in '{entity.Status}' status cannot be put on hold.");

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.OnHold;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.OnHold, userId, holdDto.Reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition put on hold: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffRequisitionDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(cancelDto.RequisitionId);

        if (entity.Status == StaffRequisitionStatus.Cancelled || entity.Status == StaffRequisitionStatus.Fulfilled)
            throw new InvalidOperationException($"A requisition in '{entity.Status}' status cannot be cancelled.");

        var fromStatus = entity.Status;
        entity.Status = StaffRequisitionStatus.Cancelled;
        entity.CancelledById = cancelledByUserId;
        entity.CancelledDate = DateTime.UtcNow;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = cancelledByUserId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, StaffRequisitionStatus.Cancelled, cancelledByUserId, cancelDto.CancellationReason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition cancelled: {RequisitionNumber}", entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> FulfillAsync(FulfillStaffRequisitionDto fulfillDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(fulfillDto.RequisitionId);

        if (entity.Status != StaffRequisitionStatus.Approved && entity.Status != StaffRequisitionStatus.PartiallyFulfilled)
            throw new InvalidOperationException("Only Approved or PartiallyFulfilled requisitions can be fulfilled.");

        if (fulfillDto.PositionsFilled < 1 || fulfillDto.PositionsFilled > entity.NumberOfPositions)
            throw new InvalidOperationException($"Positions filled must be between 1 and {entity.NumberOfPositions}.");

        var fromStatus = entity.Status;
        entity.PositionsFilled = fulfillDto.PositionsFilled;

        var fullyFilled = entity.PositionsFilled >= entity.NumberOfPositions;
        entity.Status = fullyFilled ? StaffRequisitionStatus.Fulfilled : StaffRequisitionStatus.PartiallyFulfilled;
        entity.IsFulfilled = fullyFilled;
        entity.FulfilledDate = fullyFilled ? (fulfillDto.FulfilledDate ?? DateTime.UtcNow) : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await RecordHistoryAsync(entity, fromStatus, entity.Status, userId,
            $"Positions filled: {entity.PositionsFilled}/{entity.NumberOfPositions}", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition {Status}: {RequisitionNumber}", entity.Status, entity.RequisitionNumber);

        return true;
    }

    public async Task<bool> LinkToVacancyAsync(LinkStaffRequisitionToVacancyDto linkDto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(linkDto.RequisitionId);

        entity.JobVacancyId = linkDto.JobVacancyId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff requisition {RequisitionNumber} linked to vacancy {VacancyId}",
            entity.RequisitionNumber, linkDto.JobVacancyId);

        return true;
    }

    // ── Cost operations ───────────────────────────────────────────────────────

    public async Task<StaffRequisitionCostDto> AddCostAsync(CreateStaffRequisitionCostDto createDto, Guid tenantId, Guid recordedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RequisitionId);

        var entity = createDto.ToEntity(current, recordedByUserId);
        await _costRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionCostDto>> GetCostsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<decimal> GetTotalCostAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Sum(e => e.Amount * e.ExchangeRate);
    }

    public async Task<IEnumerable<StaffRequisitionCostDto>> GetCostsByCategoryAsync(Guid requisitionId, StaffRequisitionCostCategory category, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _costRepository.GetByRequisitionAndCategoryAsync(requisitionId, category);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionCostDto> UpdateCostAsync(UpdateStaffRequisitionCostDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCostAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _costRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCostAsync(Guid costId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCostAsync(costId);

        await _costRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Attachment operations ─────────────────────────────────────────────────

    public async Task<StaffRequisitionAttachmentDto> AddAttachmentAsync(CreateStaffRequisitionAttachmentDto createDto, Guid tenantId, Guid uploadedByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RequisitionId);

        var entity = createDto.ToEntity(current, uploadedByUserId);
        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsByUploaderAsync(Guid uploadedByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetByUploadedByAsync(uploadedByUserId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttachmentAsync(attachmentId);

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Comment operations ────────────────────────────────────────────────────

    public async Task<StaffRequisitionCommentDto> AddCommentAsync(CreateStaffRequisitionCommentDto createDto, Guid tenantId, Guid authorId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAsync(createDto.RequisitionId);

        var entity = createDto.ToEntity(current, authorId);
        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetThreadedByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetAllCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetAllByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetByAuthorAsync(authorId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _commentRepository.GetRepliesAsync(parentCommentId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionCommentDto> UpdateCommentAsync(UpdateStaffRequisitionCommentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _commentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCommentAsync(commentId);

        await _commentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── History (read-only) ───────────────────────────────────────────────────

    public async Task<IEnumerable<StaffRequisitionHistoryDto>> GetHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entities = await _historyRepository.GetByRequisitionIdAsync(requisitionId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffRequisitionHistoryDto?> GetLatestHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(requisitionId);
        var tenantId = GetTenantId();
        var entity = await _historyRepository.GetLatestAsync(requisitionId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    // ── Budget-aware requisitions ───────────────────────────────────────────────

    public async Task<RequisitionBudgetCheckDto> CheckBudgetAsync(Guid requisitionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(requisitionId);

        return await BuildBudgetCheckAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Builds the budget-check result for a requisition. Resolves the fiscal year from the
    /// requisition's desired start date, finds the matching <see cref="ManpowerBudgetLine"/> for the
    /// position, and compares the projected headcount against the approved planned count.
    /// </summary>
    private async Task<RequisitionBudgetCheckDto> BuildBudgetCheckAsync(StaffRequisition entity, CancellationToken cancellationToken)
    {
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var mode = settings.BudgetEnforcementMode;

        var referenceDate = entity.DesiredStartDate != default ? entity.DesiredStartDate : entity.RequestDate;
        var fiscalYear = referenceDate.Year;

        var result = new RequisitionBudgetCheckDto
        {
            Mode               = mode,
            FiscalYear         = fiscalYear,
            RequestedPositions = entity.NumberOfPositions,
        };

        // Match the position's budget line for the fiscal year. Prefer an Active/Approved budget.
        var line = await _unitOfWork.Repository<ManpowerBudgetLine>().GetQueryable()
            .Include(l => l.ManpowerBudget)
            .Where(l => l.TenantId == entity.TenantId
                     && l.PositionId == entity.PositionId
                     && !l.IsDeleted
                     && l.ManpowerBudget != null
                     && !l.ManpowerBudget.IsDeleted
                     && l.ManpowerBudget.FiscalYear == fiscalYear)
            .OrderByDescending(l => l.ManpowerBudget.Status == ManpowerBudgetStatus.Active)
            .ThenByDescending(l => l.ManpowerBudget.Status == ManpowerBudgetStatus.Approved)
            .FirstOrDefaultAsync(cancellationToken);

        if (line is null)
        {
            // No budgeted headcount for this position — enforcement never applies.
            result.HasBudgetLine = false;
            result.ProjectedHeadcount = entity.NumberOfPositions;
            result.Message = mode == BudgetEnforcementMode.Off
                ? "Budget enforcement is turned off."
                : $"No manpower budget line exists for this position in {fiscalYear}; the requisition is not budget-constrained.";
            return result;
        }

        result.HasBudgetLine = true;
        result.PlannedCount = line.PlannedCount;
        result.CurrentFilled = line.CurrentFilled;
        result.ProjectedHeadcount = line.CurrentFilled + entity.NumberOfPositions;
        result.IsOverBudget = mode != BudgetEnforcementMode.Off && result.ProjectedHeadcount > line.PlannedCount;
        result.WouldBlock = mode == BudgetEnforcementMode.Block && result.IsOverBudget;

        if (mode == BudgetEnforcementMode.Off)
        {
            result.Message = "Budget enforcement is turned off.";
        }
        else if (result.IsOverBudget)
        {
            result.Message =
                $"This requisition would take the position to {result.ProjectedHeadcount} filled " +
                $"({line.CurrentFilled} filled + {entity.NumberOfPositions} requested) against an approved " +
                $"budget of {line.PlannedCount} for {fiscalYear}." +
                (result.WouldBlock
                    ? " Budget enforcement is set to Block, so it cannot be submitted or approved until the budget is revised."
                    : " Budget enforcement is set to Warn — proceed with caution.");
        }
        else
        {
            result.Message =
                $"Within budget: {result.ProjectedHeadcount} of {line.PlannedCount} budgeted for {fiscalYear}.";
        }

        return result;
    }

    /// <summary>
    /// Applies budget enforcement on a workflow transition. Throws when the mode is Block and the
    /// requisition is over budget; otherwise logs a warning (Warn) or does nothing (Off / within budget).
    /// </summary>
    private async Task EnforceBudgetAsync(StaffRequisition entity, string action, CancellationToken cancellationToken)
    {
        var check = await BuildBudgetCheckAsync(entity, cancellationToken);

        if (check.WouldBlock)
            throw new InvalidOperationException(check.Message);

        if (check.IsOverBudget)
            _logger.LogWarning(
                "Requisition {RequisitionNumber} {Action} over budget: {Message}",
                entity.RequisitionNumber, action, check.Message);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateRequisitionNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var sequence = await _requisitionRepository.GetNextSequenceNumberAsync(tenantId);
        return $"REQ-{DateTime.UtcNow.Year}-{sequence:D5}";
    }

    private async Task RecordHistoryAsync(
        StaffRequisition entity,
        StaffRequisitionStatus fromStatus,
        StaffRequisitionStatus toStatus,
        Guid changedById,
        string? comments,
        CancellationToken cancellationToken)
    {
        var history = StaffRequisitionMappingExtensions.ToEntity(
            entity.TenantId,
            entity.Id,
            fromStatus,
            toStatus,
            changedById,
            comments);

        await _historyRepository.AddAsync(history);
    }
}

#endregion
