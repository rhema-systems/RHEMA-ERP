using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF MOVEMENT SERVICE
// ============================================================================

#region Staff Movement Service

public class StaffMovementService : IStaffMovementService
{
    private readonly IStaffMovementRepository _movementRepo;
    private readonly IStaffMovementApprovalLevelRepository _approvalRepo;
    private readonly IStaffMovementStatusHistoryRepository _historyRepo;
    private readonly IStaffMovementAttachmentRepository _attachmentRepo;
    private readonly IStaffMovementChecklistItemRepository _checklistRepo;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffMovementService> _logger;

    public StaffMovementService(
        IStaffMovementRepository movementRepo,
        IStaffMovementApprovalLevelRepository approvalRepo,
        IStaffMovementStatusHistoryRepository historyRepo,
        IStaffMovementAttachmentRepository attachmentRepo,
        IStaffMovementChecklistItemRepository checklistRepo,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffMovementService> logger)
    {
        _movementRepo   = movementRepo;
        _approvalRepo   = approvalRepo;
        _historyRepo    = historyRepo;
        _attachmentRepo = attachmentRepo;
        _checklistRepo  = checklistRepo;
        _currentUserProvider = currentUserProvider;
        _unitOfWork     = unitOfWork;
        _logger         = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A movement owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffMovement> GetOwnedMovementAsync(Guid id)
    {
        var entity = await _movementRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Staff movement with ID '{id}' was not found.");
        return entity;
    }

    private async Task<StaffMovementApprovalLevel> GetOwnedApprovalLevelAsync(Guid id)
    {
        var entity = await _approvalRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Approval level with ID '{id}' was not found.");
        return entity;
    }

    private async Task<StaffMovementAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attachment with ID '{id}' was not found.");
        return entity;
    }

    private async Task<StaffMovementChecklistItem> GetOwnedChecklistItemAsync(Guid id)
    {
        var entity = await _checklistRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Checklist item with ID '{id}' was not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffMovementDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _movementRepo.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Staff movement with ID '{id}' was not found.");

        return entity.ToDetailDto();
    }

    public async Task<StaffMovementDto?> GetByMovementNumberAsync(string movementNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _movementRepo.GetByMovementNumberAsync(movementNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetAllAsync(
            m => m.Employee,
            m => m.CurrentPosition,
            m => m.CurrentOrganizationUnit,
            m => m.NewPosition,
            m => m.NewOrganizationUnit);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public const int MaxSummariesByIdsBatchSize = 500;

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetSummariesByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        if (idList.Count > MaxSummariesByIdsBatchSize)
            throw new ArgumentException(
                $"At most {MaxSummariesByIdsBatchSize} movement IDs may be requested per call.",
                nameof(ids));

        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetSummariesByIdsAsync(idList);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    private static readonly StaffMovementStatus[] _pendingApprovalStatuses =
    {
        StaffMovementStatus.Submitted,
        StaffMovementStatus.CurrentSupervisorApproval,
        StaffMovementStatus.NewSupervisorApproval,
        StaffMovementStatus.CurrentHodApproval,
        StaffMovementStatus.NewHodApproval,
        StaffMovementStatus.HrReview,
        StaffMovementStatus.ManagementApproval
    };

    public async Task<PagedResult<StaffMovementSummaryDto>> GetPagedAsync(
        int pageNumber, int pageSize,
        StaffMovementStatus? status = null,
        StaffMovementType? type = null,
        bool isPendingApproval = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        IQueryable<StaffMovement> query = _movementRepo.GetQueryable()
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Include(m => m.Promotion)
            .Include(m => m.Transfer)
            .Where(m => m.TenantId == tenantId && !m.IsDeleted);

        if (isPendingApproval)
            query = query.Where(m => _pendingApprovalStatuses.Contains(m.Status));
        else if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (type.HasValue) query = query.Where(m => m.MovementType == type.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.EffectiveDate)
            .ThenByDescending(m => m.RequestSubmissionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffMovementSummaryDto>
        {
            Items      = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page       = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByEmployeeAsync(employeeId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByStatusAsync(StaffMovementStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByStatusAsync(status);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAsync(
        StaffMovementType type, DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByTypeAsync(type, from, to);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAndStatusAsync(
        StaffMovementType type, StaffMovementStatus status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByTypeAndStatusAsync(type, status);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByCurrentOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByCurrentOrganizationUnitAsync(organizationUnitId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByNewOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByNewOrganizationUnitAsync(organizationUnitId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingApprovalAsync();
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingEmployeeAcceptanceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingEmployeeAcceptanceAsync();
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingHandoverAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingHandoverAsync();
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetActiveTemporaryAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetActiveTemporaryAssignmentsAsync();
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetExpiringTemporaryAssignmentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetExpiringTemporaryAssignmentsAsync(daysAhead);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByEffectiveDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByEffectiveDateRangeAsync(from, to);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByRequestedByAsync(Guid requestedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByRequestedByAsync(requestedByEmployeeId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetLatestMovementsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetLatestMovementsForEmployeeAsync(employeeId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetBySuccessionPlanAsync(Guid successionPlanId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetBySuccessionPlanAsync(successionPlanId);
        return entities.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<StaffMovementDto> CreateAsync(CreateStaffMovementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.MovementNumber          = await GenerateMovementNumberAsync(cancellationToken);
        entity.Status                  = StaffMovementStatus.Draft;
        entity.RequestSubmissionDate   = DateTime.UtcNow;

        await _movementRepo.AddAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, StaffMovementStatus.Draft, StaffMovementStatus.Draft, "Movement created", createdByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement created: {MovementNumber}", entity.MovementNumber);

        // Reload with all nav-prop includes so the returned DTO has display names populated
        var loaded = await _movementRepo.GetWithFullDetailsAsync(entity.Id);
        if (loaded != null && loaded.TenantId != tenantId)
            loaded = null;
        return (loaded ?? entity).ToDetailDto();
    }

    public async Task<StaffMovementDto> UpdateAsync(UpdateStaffMovementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(updateDto.Id);

        if (entity.Status == StaffMovementStatus.Approved || entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("An authorised or completed movement cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _movementRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement updated: {MovementNumber}", entity.MovementNumber);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(id);

        if (entity.Status == StaffMovementStatus.Approved || entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("An authorised or completed movement cannot be deleted.");

        await _movementRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement deleted: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> SubmitAsync(SubmitStaffMovementDto dto, Guid submittedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status != StaffMovementStatus.Draft)
            throw new InvalidOperationException("Only Draft movements can be submitted for approval.");

        var previousStatus = entity.Status;
        entity.Status               = StaffMovementStatus.Submitted;
        entity.RequestSubmissionDate = DateTime.UtcNow;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.SubmissionNotes, submittedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement submitted: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> AuthorizeAsync(AuthorizeStaffMovementDto dto, Guid authorizedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        var approvalStatuses = new[]
        {
            StaffMovementStatus.Submitted,
            StaffMovementStatus.CurrentSupervisorApproval,
            StaffMovementStatus.NewSupervisorApproval,
            StaffMovementStatus.CurrentHodApproval,
            StaffMovementStatus.NewHodApproval,
            StaffMovementStatus.HrReview,
            StaffMovementStatus.ManagementApproval,
        };

        if (!approvalStatuses.Contains(entity.Status))
            throw new InvalidOperationException($"Movement cannot be authorized in its current status: {entity.Status}.");

        var allLevelsApproved = await _approvalRepo.AllLevelsApprovedAsync(dto.MovementId);
        if (!allLevelsApproved)
            throw new InvalidOperationException("All approval levels must be approved before the movement can be authorized.");

        var previousStatus        = entity.Status;
        entity.Status             = StaffMovementStatus.Approved;
        entity.AuthorizedById     = authorizedByUserId;
        entity.AuthorizationDate  = DateTime.UtcNow;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.Comments, authorizedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement authorised: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> RecordEmployeeResponseAsync(RespondToStaffMovementDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (!entity.RequiresEmployeeAcceptance)
            throw new InvalidOperationException("This movement does not require employee acceptance.");

        var previousStatus = entity.Status;

        // Ensure we are in the acceptance-pending stage
        if (entity.Status == StaffMovementStatus.Approved)
            entity.Status = StaffMovementStatus.EmployeeAcceptancePending;

        entity.EmployeeAccepted     = dto.Accepted;
        entity.EmployeeResponseDate = DateTime.UtcNow;
        entity.EmployeeComments     = dto.Comments;

        // On acceptance progress back to Approved (ready for implementation)
        if (dto.Accepted)
            entity.Status = StaffMovementStatus.Approved;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status,
            dto.Accepted ? "Employee accepted the movement" : "Employee did not accept the movement",
            entity.EmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee response recorded for movement {MovementNumber}: {Accepted}",
            entity.MovementNumber, dto.Accepted);

        return true;
    }

    public async Task<bool> CompleteHandoverAsync(CompleteHandoverDto dto, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (!entity.RequiresHandover)
            throw new InvalidOperationException("This movement does not require a handover.");

        if (entity.Status != StaffMovementStatus.Approved)
            throw new InvalidOperationException($"Handover can only be completed for approved movements. Current status: {entity.Status}.");

        entity.HandoverCompletionDate = DateTime.UtcNow;
        entity.HandoverNotes          = dto.HandoverNotes;

        await _movementRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Handover completed for movement: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> RejectAsync(RejectStaffMovementDto dto, Guid rejectedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status == StaffMovementStatus.Approved || entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("An authorised or completed movement cannot be rejected.");

        var previousStatus = entity.Status;
        entity.Status          = StaffMovementStatus.Rejected;
        entity.RejectedById    = rejectedByUserId;
        entity.RejectionDate   = DateTime.UtcNow;
        entity.RejectionReason = dto.RejectionReason;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.RejectionReason, rejectedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement rejected: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffMovementDto dto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("A completed movement cannot be cancelled.");

        var previousStatus = entity.Status;
        entity.Status              = StaffMovementStatus.Cancelled;
        entity.CancelledById       = cancelledByUserId;
        entity.CancellationDate    = DateTime.UtcNow;
        entity.CancellationReason  = dto.CancellationReason;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.CancellationReason, cancelledByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement cancelled: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> ProcessReturnFromTemporaryAsync(ProcessReturnFromTemporaryDto dto, Guid processedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (!entity.IsTemporary)
            throw new InvalidOperationException("This movement is not a temporary assignment.");

        if (entity.ReturnProcessed)
            throw new InvalidOperationException("Return has already been processed for this movement.");

        var previousStatus = entity.Status;
        entity.ReturnProcessed  = true;
        entity.ActualReturnDate = dto.ActualReturnDate;
        entity.ReturnMovementId = dto.ReturnMovementId;
        entity.Status           = StaffMovementStatus.Implemented;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.Notes ?? "Return from temporary assignment processed", processedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Return processed for temporary movement: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> ImplementAsync(Guid movementId, Guid implementedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(movementId);

        if (entity.Status != StaffMovementStatus.Approved)
            throw new InvalidOperationException($"Only approved movements can be implemented. Current status: {entity.Status}.");

        var previousStatus = entity.Status;
        entity.Status = StaffMovementStatus.Implemented;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, "Movement implemented", implementedByUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement implemented: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    // ── Approval Level Operations ─────────────────────────────────────────────

    public async Task<IEnumerable<StaffMovementApprovalLevelDto>> GetApprovalLevelsAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _approvalRepo.GetByMovementIdAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMovementApprovalLevelDto?> GetCurrentPendingApprovalLevelAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entity = await _approvalRepo.GetCurrentPendingLevelAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<StaffMovementApprovalLevelDto> AddApprovalLevelAsync(CreateStaffMovementApprovalLevelDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedMovementAsync(createDto.MovementId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = ApprovalStatus.Pending;

        await _approvalRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ActionApprovalLevelAsync(ActionApprovalLevelDto dto, Guid actionedByUserId, CancellationToken cancellationToken = default)
    {
        var level = await GetOwnedApprovalLevelAsync(dto.ApprovalLevelId);

        if (level.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("This approval level has already been actioned.");

        level.Status     = dto.Decision;
        level.ActionDate = DateTime.UtcNow;
        level.Comments   = dto.Comments;

        await _approvalRepo.UpdateAsync(level);

        // Auto-advance to Authorized when all levels are approved.
        if (dto.Decision == ApprovalStatus.Approved)
        {
            var allApproved = await _approvalRepo.AllLevelsApprovedAsync(level.MovementId);
            if (allApproved)
            {
                var movement = await GetOwnedMovementAsync(level.MovementId);
                var previousStatus      = movement.Status;
                movement.Status         = StaffMovementStatus.Approved;
                movement.AuthorizedById = actionedByUserId;
                movement.AuthorizationDate = DateTime.UtcNow;

                await _movementRepo.UpdateAsync(movement);
                await RecordStatusHistoryAsync(movement.TenantId, movement.Id, previousStatus, movement.Status,
                    "All approval levels cleared — movement authorised", actionedByUserId, cancellationToken);
            }
        }
        else if (dto.Decision == ApprovalStatus.Rejected)
        {
            var movement = await GetOwnedMovementAsync(level.MovementId);
            var previousStatus     = movement.Status;
            movement.Status        = StaffMovementStatus.Rejected;
            movement.RejectedById  = actionedByUserId;
            movement.RejectionDate = DateTime.UtcNow;
            movement.RejectionReason = dto.Comments;

            await _movementRepo.UpdateAsync(movement);
            await RecordStatusHistoryAsync(movement.TenantId, movement.Id, previousStatus, movement.Status,
                dto.Comments ?? "Rejected at approval level", actionedByUserId, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DelegateApprovalLevelAsync(DelegateApprovalLevelDto dto, CancellationToken cancellationToken = default)
    {
        var level = await GetOwnedApprovalLevelAsync(dto.ApprovalLevelId);

        if (level.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Only pending approval levels can be delegated.");

        level.DelegatedToId    = dto.DelegatedToId;
        level.DelegationDate   = DateTime.UtcNow;
        level.DelegationReason = dto.DelegationReason;

        await _approvalRepo.UpdateAsync(level);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> AllLevelsApprovedAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        return await _approvalRepo.AllLevelsApprovedAsync(movementId);
    }

    // ── Status History Operations ─────────────────────────────────────────────

    public async Task<IEnumerable<StaffMovementStatusHistoryDto>> GetStatusHistoryAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _historyRepo.GetByMovementIdAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMovementStatusHistoryDto?> GetLatestStatusAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entity = await _historyRepo.GetLatestAsync(movementId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    // ── Attachment Operations ─────────────────────────────────────────────────

    public async Task<StaffMovementAttachmentDto> AddAttachmentAsync(CreateStaffMovementAttachmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedMovementAsync(createDto.MovementId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.UploadDate     = DateTime.UtcNow;
        entity.UploadedById   = createdByUserId;

        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffMovementAttachmentDto>> GetAttachmentsAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepo.GetByMovementIdAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMovementAttachmentDto>> GetAttachmentsByTypeAsync(Guid movementId, StaffMovementAttachmentType type, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepo.GetByTypeAsync(movementId, type);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAttachmentAsync(attachmentId);

        await _attachmentRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Checklist Operations ──────────────────────────────────────────────────

    public async Task<IEnumerable<StaffMovementChecklistItemDto>> GetChecklistItemsAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _checklistRepo.GetByMovementIdAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMovementChecklistItemDto>> GetPendingChecklistItemsAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        var tenantId = GetTenantId();
        var entities = await _checklistRepo.GetPendingItemsAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMovementChecklistItemDto>> GetOverdueChecklistItemsAsync(Guid? movementId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (movementId.HasValue)
            await GetOwnedMovementAsync(movementId.Value);

        var entities = await _checklistRepo.GetOverdueItemsAsync(movementId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMovementChecklistItemDto>> GetChecklistItemsByResponsiblePersonAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _checklistRepo.GetByResponsiblePersonAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMovementChecklistItemDto> AddChecklistItemAsync(CreateStaffMovementChecklistItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedMovementAsync(createDto.MovementId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _checklistRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteChecklistItemAsync(CompleteChecklistItemDto dto, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistItemAsync(dto.ItemId);

        entity.IsCompleted      = true;
        entity.CompletionDate   = DateTime.UtcNow;
        entity.CompletionNotes  = dto.CompletionNotes;

        await _checklistRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> AllRequiredItemsCompletedAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        await GetOwnedMovementAsync(movementId);
        return await _checklistRepo.AllRequiredItemsCompletedAsync(movementId);
    }

    public async Task<bool> DeleteChecklistItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistItemAsync(itemId);

        await _checklistRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<StaffMovementDashboardDto> GetDashboardAsync(
        int?      filterYear = null,
        DateTime? fromDate   = null,
        DateTime? toDate     = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var yearStart = filterYear.HasValue
            ? new DateTime(filterYear.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1).AddTicks(-1);

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd   = monthStart.AddMonths(1).AddTicks(-1);

        var query     = _movementRepo.GetQueryable().Where(m => m.TenantId == tenantId && !m.IsDeleted);
        var ytdQuery  = query.Where(m => m.RequestDate >= yearStart && m.RequestDate <= yearEnd);
        var dateQuery = (fromDate.HasValue || toDate.HasValue)
            ? query.Where(m =>
                  (!fromDate.HasValue || m.RequestDate >= fromDate.Value) &&
                  (!toDate.HasValue   || m.RequestDate <= toDate.Value))
            : ytdQuery;

        var pendingStatuses = new[]
        {
            StaffMovementStatus.Submitted,
            StaffMovementStatus.CurrentSupervisorApproval,
            StaffMovementStatus.NewSupervisorApproval,
            StaffMovementStatus.CurrentHodApproval,
            StaffMovementStatus.NewHodApproval,
            StaffMovementStatus.HrReview,
            StaffMovementStatus.ManagementApproval
        };

        var terminalStatuses = new[]
        {
            StaffMovementStatus.Rejected,
            StaffMovementStatus.Implemented,
            StaffMovementStatus.Cancelled
        };

        var pendingApprovals    = (await _movementRepo.GetPendingApprovalAsync()).Where(m => m.TenantId == tenantId).ToList();
        var pendingHandover     = (await _movementRepo.GetPendingHandoverAsync()).Where(m => m.TenantId == tenantId).ToList();
        var expiringAssignments = (await _movementRepo.GetExpiringTemporaryAssignmentsAsync(60)).Where(m => m.TenantId == tenantId).ToList();

        // ── Scalar KPIs ───────────────────────────────────────────────────────

        var totalActive            = await query.CountAsync(m => !terminalStatuses.Contains(m.Status), cancellationToken);
        var pendingApprovalCount   = await query.CountAsync(m => pendingStatuses.Contains(m.Status), cancellationToken);
        var awaitingEmployee       = await query.CountAsync(m => m.Status == StaffMovementStatus.EmployeeAcceptancePending, cancellationToken);
        var awaitingHandover       = pendingHandover.Count;
        var awaitingImplementation = await query.CountAsync(m => m.Status == StaffMovementStatus.Approved, cancellationToken);
        var implementedYtd         = await ytdQuery.CountAsync(m => m.Status == StaffMovementStatus.Implemented, cancellationToken);
        var approvedThisMonth      = await query.CountAsync(m =>
            (m.Status == StaffMovementStatus.Approved || m.Status == StaffMovementStatus.Implemented)
            && m.RequestDate >= monthStart && m.RequestDate <= monthEnd, cancellationToken);
        var rejectedThisMonth      = await query.CountAsync(m =>
            m.Status == StaffMovementStatus.Rejected
            && m.RequestDate >= monthStart && m.RequestDate <= monthEnd, cancellationToken);
        var activeActing           = await query.CountAsync(m =>
            m.MovementType == StaffMovementType.ActingAppointment
            && !terminalStatuses.Contains(m.Status), cancellationToken);
        var expiringSecondments    = expiringAssignments.Count;

        // Overdue = pending approval for more than 5 days
        var overdueList = pendingApprovals
            .Where(m => (now - m.RequestSubmissionDate).TotalDays > 5)
            .ToList();

        // ── ByType breakdown ──────────────────────────────────────────────────

        var allActiveMovements = await query
            .Where(m => !terminalStatuses.Contains(m.Status))
            .GroupBy(m => m.MovementType)
            .Select(g => new { MovementType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pendingByType = pendingApprovals
            .GroupBy(m => m.MovementType)
            .ToDictionary(g => g.Key, g => g.Count());

        var implementedThisMonth = await query
            .Where(m => m.Status == StaffMovementStatus.Implemented
                        && m.RequestDate >= monthStart && m.RequestDate <= monthEnd)
            .GroupBy(m => m.MovementType)
            .Select(g => new { MovementType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var implementedYtdByType = await ytdQuery
            .Where(m => m.Status == StaffMovementStatus.Implemented)
            .GroupBy(m => m.MovementType)
            .Select(g => new { MovementType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byType = allActiveMovements.Select(t => new MovementTypeSummaryDto
        {
            MovementType       = t.MovementType,
            TotalActive        = t.Count,
            PendingApproval    = pendingByType.TryGetValue(t.MovementType, out var pb) ? pb : 0,
            CompletedThisMonth = implementedThisMonth.FirstOrDefault(x => x.MovementType == t.MovementType)?.Count ?? 0,
            ImplementedYtd     = implementedYtdByType.FirstOrDefault(x => x.MovementType == t.MovementType)?.Count ?? 0,
            SharePercent       = totalActive > 0 ? Math.Round(t.Count * 100.0 / totalActive, 1) : 0
        }).ToList();

        // ── Overdue approvals (rich DTO) ──────────────────────────────────────

        var overdueApprovals = overdueList.Select(m =>
        {
            var days     = (int)(now - m.RequestSubmissionDate).TotalDays;
            var approver = m.ApprovalLevels?
                .FirstOrDefault(al => al.Status == ApprovalStatus.Pending)
                ?.Approver?.FullName ?? string.Empty;

            return new OverdueApprovalSummaryDto
            {
                MovementId          = m.Id,
                MovementNumber      = m.MovementNumber,
                EmployeeName        = m.Employee?.FullName ?? string.Empty,
                EmployeeNumber      = m.Employee?.EmployeeNumber,
                MovementType        = m.MovementType,
                Status              = m.Status,
                PendingApproverName = approver,
                PendingStage        = m.Status.ToString(),
                OverdueDays         = days,
                EffectiveDate       = m.EffectiveDate,
                Priority            = days > 10 ? 1 : days >= 5 ? 2 : 3
            };
        }).OrderByDescending(x => x.OverdueDays).ToList();

        // ── Expiring assignments (rich DTO) ───────────────────────────────────

        var expiringRich = expiringAssignments.Select(m => new ExpiringAssignmentSummaryDto
        {
            MovementId       = m.Id,
            MovementNumber   = m.MovementNumber,
            EmployeeName     = m.Employee?.FullName ?? string.Empty,
            EmployeeNumber   = m.Employee?.EmployeeNumber,
            AssignmentType   = m.MovementType,
            EndDate          = m.TemporaryEndDate!.Value,
            DaysRemaining    = (int)(m.TemporaryEndDate!.Value - now).TotalDays,
            ReturnProcessed  = m.ReturnProcessed,
            CurrentPosition  = m.CurrentPosition?.Title,
            NewPosition      = m.NewPosition?.Title,
            HostOrganization = m.NewOrganizationUnit?.Name
        }).ToList();

        // ── Bottlenecks by org unit (top 5) ───────────────────────────────────

        var bottlenecks = pendingApprovals
            .GroupBy(m => m.CurrentOrganizationUnitId)
            .Select(g =>
            {
                var unitName = g.FirstOrDefault()?.CurrentOrganizationUnit?.Name
                               ?? g.Key.ToString();
                var avgWait  = g.Any()
                    ? (int)g.Average(m => (now - m.RequestSubmissionDate).TotalDays)
                    : 0;
                var mostDelayedStage = g
                    .GroupBy(m => m.Status.ToString())
                    .OrderByDescending(s => s.Count())
                    .First().Key;

                return new ApprovalBottleneckDto
                {
                    OrganizationUnitId   = g.Key,
                    OrganizationUnitName = unitName,
                    PendingCount         = g.Count(),
                    AverageWaitDays      = avgWait,
                    MostDelayedStage     = mostDelayedStage,
                    NearingEffectiveDate = g.Count(m => m.EffectiveDate <= now.AddDays(14))
                };
            })
            .OrderByDescending(b => b.PendingCount)
            .Take(5)
            .ToList();

        // ── Recent activity ───────────────────────────────────────────────────

        var recentEntities = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        // ── Assemble ──────────────────────────────────────────────────────────

        return new StaffMovementDashboardDto
        {
            // KPIs
            TotalActive             = totalActive,
            PendingApproval         = pendingApprovalCount,
            ApprovedThisMonth       = approvedThisMonth,
            RejectedThisMonth       = rejectedThisMonth,
            AwaitingEmployeeResponse = awaitingEmployee,
            AwaitingHandover         = awaitingHandover,
            ExpiringSecondments     = expiringSecondments,
            ActiveActingAppointments = activeActing,
            AwaitingImplementation  = awaitingImplementation,
            OverdueApprovalActions  = overdueList.Count,
            ImplementedYtd          = implementedYtd,

            // Legacy scalars
            TotalMovementsYtd       = await ytdQuery.CountAsync(cancellationToken),
            FilterYear              = filterYear ?? now.Year,
            AverageSalaryIncreasePercentage = await ytdQuery
                .Where(m => m.SalaryIncreasePercentage != null)
                .AverageAsync(m => (decimal?)m.SalaryIncreasePercentage, cancellationToken) ?? 0m,
            MovementsWithSalaryIncrease = await ytdQuery.CountAsync(m => m.SalaryIncreaseAmount > 0, cancellationToken),

            // Lists
            ByType              = byType,
            Recent              = recentEntities.ToSummaryDtoList().ToList(),
            OverdueApprovals    = overdueApprovals,
            ExpiringAssignments = expiringRich,
            BottlenecksByUnit   = bottlenecks,

            ComputedAt = now
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateMovementNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var prefix  = $"MOV-{DateTime.UtcNow:yyyyMMdd}";
        var countToday = await _movementRepo.GetQueryable()
            .Where(m => m.TenantId == tenantId && m.MovementNumber.StartsWith(prefix))
            .CountAsync(cancellationToken);

        return $"{prefix}-{(countToday + 1):D4}";
    }

    private async Task RecordStatusHistoryAsync(
        Guid tenantId,
        Guid movementId,
        StaffMovementStatus fromStatus,
        StaffMovementStatus toStatus,
        string? reason,
        Guid changedById,
        CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var history = new StaffMovementStatusHistory
        {
            TenantId    = tenantId,
            MovementId  = movementId,
            FromStatus  = fromStatus,
            ToStatus    = toStatus,
            ChangedDate = DateTime.UtcNow,
            ChangedById = changedById,
            Reason      = reason
        };

        await _historyRepo.AddAsync(history);
    }
}

#endregion
