using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
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
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffMovementService> _logger;

    /// <summary>
    /// The workflow entity type. Approval authority comes from the published definition, not from a
    /// role attribute — which is the whole reason a movement is on the engine: an approver here is
    /// usually a line manager or a head of department, not HR.
    /// </summary>
    private const string EntityType = "StaffMovement";

    public StaffMovementService(
        IStaffMovementRepository movementRepo,
        IStaffMovementApprovalLevelRepository approvalRepo,
        IStaffMovementStatusHistoryRepository historyRepo,
        IStaffMovementAttachmentRepository attachmentRepo,
        IStaffMovementChecklistItemRepository checklistRepo,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffMovementService> logger)
    {
        _movementRepo   = movementRepo;
        _approvalRepo   = approvalRepo;
        _historyRepo    = historyRepo;
        _attachmentRepo = attachmentRepo;
        _checklistRepo  = checklistRepo;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserProvider = currentUserProvider;
        _unitOfWork     = unitOfWork;
        _logger         = logger;
    }

    /// <summary>
    /// The engine identifies approvers by ApplicationUser id, not Employee id — see the actor split
    /// in the attendance services. The entity's own AuthorizedById / RejectedById are Employee FKs
    /// and are set from the caller's employee separately.
    /// </summary>
    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("No user is associated with the current request.");
        return userId;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    // UnauthorizedAccessException, not InvalidOperationException: this is a refusal, not a broken rule.
    // Under MovementBusinessRulesAttribute the latter would read as 422 "unprocessable" on every endpoint
    // for a tenant-less token; both the filter and GlobalExceptionHandlingMiddleware map this to 403 with
    // its own message, so the behaviour no longer depends on the filter being present.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
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

    /// <summary>
    /// Validates a body-supplied Employee id and returns the row.
    ///
    /// Two jobs in one call, as in the SHE services. It stops an unknown or another tenant's id
    /// reaching the database as an FK violation (SQL 547, surfacing as an unexplained 500 rather than
    /// "that employee was not found"), and because the row ends up tracked, EF fixes up the navigation
    /// on the entity being written — so the write response carries the approver's or responsible
    /// person's name instead of an empty string, with no second read.
    /// </summary>
    private async Task<Employee> GetOwnedEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.IsDeleted || employee.TenantId != GetTenantId())
            throw new ArgumentException($"The {role} employee with ID '{employeeId}' was not found.");
        return employee;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<StaffMovementDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _movementRepo.GetWithFullDetailsAsync(tenantId, id);

        if (entity == null)
            throw new ArgumentException($"Staff movement with ID '{id}' was not found.");

        return entity.ToDetailDto();
    }

    public async Task<StaffMovementDto?> GetByMovementNumberAsync(string movementNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _movementRepo.GetByMovementNumberAsync(tenantId, movementNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Not the generic GetAllAsync: it reads every tenant's rows for this one to discard, and its
        // include list is missing Promotion and Transfer, which the summary row renders.
        var entities = await _movementRepo.GetByEffectiveDateRangeAsync(tenantId, DateTime.MinValue, DateTime.MaxValue);
        return entities.ToSummaryDtoList();
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
        var entities = await _movementRepo.GetSummariesByIdsAsync(tenantId, idList);
        return entities.ToSummaryDtoList();
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
        var entities = await _movementRepo.GetByEmployeeAsync(tenantId, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByStatusAsync(StaffMovementStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByStatusAsync(tenantId, status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAsync(
        StaffMovementType type, DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByTypeAsync(tenantId, type, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAndStatusAsync(
        StaffMovementType type, StaffMovementStatus status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByTypeAndStatusAsync(tenantId, type, status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByCurrentOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByCurrentOrganizationUnitAsync(tenantId, organizationUnitId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByNewOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByNewOrganizationUnitAsync(tenantId, organizationUnitId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingApprovalAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingEmployeeAcceptanceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingEmployeeAcceptanceAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetPendingHandoverAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetPendingHandoverAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetActiveTemporaryAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetActiveTemporaryAssignmentsAsync(tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetExpiringTemporaryAssignmentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetExpiringTemporaryAssignmentsAsync(tenantId, daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByEffectiveDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByEffectiveDateRangeAsync(tenantId, from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetByRequestedByAsync(Guid requestedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetByRequestedByAsync(tenantId, requestedByEmployeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetLatestMovementsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetLatestMovementsForEmployeeAsync(tenantId, employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffMovementSummaryDto>> GetBySuccessionPlanAsync(Guid successionPlanId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _movementRepo.GetBySuccessionPlanAsync(tenantId, successionPlanId);
        return entities.ToSummaryDtoList();
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
        var loaded = await _movementRepo.GetWithFullDetailsAsync(tenantId, entity.Id);
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

        // Re-read through the include path: the edited entity was loaded by id with no navigations, so
        // mapping it directly returns the new position and unit IDS with their NAMES blank — and the
        // screen renders the response, so the row it just saved appears to have lost its labels.
        var loaded = await _movementRepo.GetWithFullDetailsAsync(entity.TenantId, entity.Id);
        return (loaded ?? entity).ToDetailDto();
    }

    /// <summary>
    /// Soft-deletes a movement nobody has been asked to act on yet.
    ///
    /// Once it has gone out for approval it can only be cancelled or recalled: deleting it would
    /// leave the workflow instance running and a task sitting in an approver's queue pointing at a
    /// record that no longer exists.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(id);

        if (entity.Status == StaffMovementStatus.Approved || entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("An authorised or completed movement cannot be deleted.");

        if (entity.Status == StaffMovementStatus.Submitted)
            throw new InvalidOperationException(
                "A movement that is out for approval cannot be deleted. Recall it first, or cancel it.");

        await _movementRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement deleted: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    // ── Approval workflow ────────────────────────────────────────────────────
    // Submit / approve / reject / recall all run through the generic workflow engine; this service
    // never sets an approval status itself. StaffMovementWorkflowStatusAdapter maps the engine's
    // outcome onto the entity.
    //
    // ⚠ Like every other entity on the engine, this is inoperable until a StaffMovement workflow
    // definition has been published for the tenant — the authority to approve comes from the
    // definition, not from a role attribute. A single-step definition approves on submission.

    public async Task<bool> SubmitAsync(SubmitStaffMovementDto dto, Guid submittedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status != StaffMovementStatus.Draft)
            throw new InvalidOperationException("Only Draft movements can be submitted for approval.");

        // A movement with no destination is not something an approver can weigh. The create DTO
        // requires both, but an edit could have cleared the salary, and approving a promotion to
        // nowhere is worse than refusing to send it.
        if (entity.NewPositionId == Guid.Empty || entity.NewOrganizationUnitId == Guid.Empty)
            throw new InvalidOperationException(
                "Set the position and organisation unit the employee is moving into before submitting this movement.");

        var previousStatus = entity.Status;
        entity.RequestSubmissionDate = DateTime.UtcNow;

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the movement approval workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, workflowResult.Outcome, _currentUserProvider.UserId);

        // A definition with one step approves on submission, so the authoriser has to be stamped
        // here too — the adapter only knows the ApplicationUser id, and this column is an Employee FK.
        if (entity.Status == StaffMovementStatus.Approved)
            entity.AuthorizedById = submittedByEmployeeId;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.SubmissionNotes, submittedByEmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement submitted: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    /// <summary>
    /// The movements the caller is currently able to approve.
    ///
    /// Without this an approver has nowhere to find their work: the register is HR-only, and the
    /// approver of a movement is normally a line manager or head of department. It is the same
    /// token-derived shape as the checklist and movement `/mine` reads — no id is accepted, so it
    /// cannot become "read anyone's queue by passing their id".
    ///
    /// The engine answers per movement rather than per user, so this asks it about each submitted
    /// movement in turn. That set is small by nature — anything sitting in it is, by definition,
    /// work nobody has done yet.
    /// </summary>
    public async Task<IEnumerable<StaffMovementSummaryDto>> GetAwaitingMyApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var userId = RequireUserId();

        var submitted = await _movementRepo.GetPendingApprovalAsync(tenantId);

        var mine = new List<StaffMovement>();
        foreach (var movement in submitted)
        {
            if (await _workflowIntegrationService.CanUserApproveAsync(EntityType, movement.Id, userId))
                mine.Add(movement);
        }

        return mine.ToSummaryDtoList();
    }

    public async Task<bool> ApproveAsync(Guid movementId, Guid approvingEmployeeId, string? comments = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(movementId);
        var userId = RequireUserId();

        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, movementId, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var previousStatus = entity.Status;

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, movementId, userId, "Approve", comments);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId);

        if (entity.Status == StaffMovementStatus.Approved)
            entity.AuthorizedById = approvingEmployeeId;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status,
            comments ?? "Approval step processed", approvingEmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement approval step processed: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> RecallAsync(Guid movementId, Guid recallingEmployeeId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(movementId);
        var userId = RequireUserId();

        if (entity.Status != StaffMovementStatus.Submitted)
            throw new InvalidOperationException("Only a movement still awaiting approval can be recalled.");

        var previousStatus = entity.Status;

        var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, movementId, userId, reason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to recall the movement.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId, reason);

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status,
            reason ?? "Recalled by the requester", recallingEmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement recalled: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> RecordEmployeeResponseAsync(RespondToStaffMovementDto dto, Guid respondingEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        // The endpoint took no actor at all before this: any authenticated caller could accept or
        // decline a move on someone else's behalf. Acceptance is the employee's own testimony, so the
        // subject test is absolute — HR cannot answer for them either.
        if (entity.EmployeeId != respondingEmployeeId)
            throw new UnauthorizedAccessException("Only the employee being moved can respond to this movement.");

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

    /// <summary>
    /// Refuses the movement, as the approver the engine currently has it with.
    ///
    /// Rejection is only meaningful once somebody has been ASKED to approve it: a draft nobody has
    /// seen is deleted or cancelled, not refused. Requiring Submitted also means every rejection
    /// goes through the engine, so the instance is closed and the approval history records who
    /// refused it — a direct status write would leave a live workflow instance, and a task in
    /// somebody's queue, pointing at a rejected movement.
    /// </summary>
    public async Task<bool> RejectAsync(RejectStaffMovementDto dto, Guid rejectedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status != StaffMovementStatus.Submitted)
            throw new InvalidOperationException(
                $"Only a movement awaiting approval can be rejected. This one is {entity.Status} — " +
                "a draft is deleted or cancelled instead.");

        var previousStatus = entity.Status;
        var userId = RequireUserId();

        if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, dto.MovementId, userId))
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType, dto.MovementId, userId, "Reject", dto.RejectionReason);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, dto.RejectionReason);

        entity.RejectedById    = rejectedByEmployeeId;
        entity.RejectionReason = dto.RejectionReason;

        await _movementRepo.UpdateAsync(entity);
        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, dto.RejectionReason, rejectedByEmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff movement rejected: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffMovementDto dto, Guid cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(dto.MovementId);

        if (entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("A completed movement cannot be cancelled.");

        // Cancelling a movement that is out for approval must take its workflow instance with it,
        // or the approvers keep a live task pointing at a cancelled record.
        if (entity.Status == StaffMovementStatus.Submitted)
            await _workflowIntegrationService.CancelWorkflowAsync(
                EntityType, entity.Id, dto.CancellationReason ?? "Movement cancelled");

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

    /// <summary>
    /// Marks the movement as carried out.
    ///
    /// The three gates below were all modelled and none was enforced: RequiresEmployeeAcceptance,
    /// RequiresHandover and the required checklist items existed as flags and a helper that nothing
    /// called. A movement could be implemented over an employee who had declined it, with the
    /// outgoing duties un-handed-over and the access-revocation tasks still open.
    /// </summary>
    public async Task<bool> ImplementAsync(Guid movementId, Guid implementedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(movementId);

        if (entity.Status != StaffMovementStatus.Approved)
            throw new InvalidOperationException($"Only approved movements can be implemented. Current status: {entity.Status}.");

        if (entity.RequiresEmployeeAcceptance && entity.EmployeeAccepted != true)
            throw new InvalidOperationException(
                entity.EmployeeAccepted == false
                    ? "The employee declined this movement, so it cannot be implemented."
                    : "This movement is waiting for the employee to accept it.");

        if (entity.RequiresHandover && entity.HandoverCompletionDate == null)
            throw new InvalidOperationException("Record the handover before implementing this movement.");

        if (!await _checklistRepo.AllRequiredItemsCompletedAsync(movementId))
            throw new InvalidOperationException(
                "Every required checklist task must be complete before this movement can be implemented.");

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
        await GetOwnedEmployeeAsync(createdByUserId, "uploading");

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

        var entities = await _checklistRepo.GetOverdueItemsAsync(tenantId, movementId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffMovementChecklistItemDto>> GetChecklistItemsByResponsiblePersonAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _checklistRepo.GetByResponsiblePersonAsync(tenantId, employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<StaffMovementChecklistItemDto> AddChecklistItemAsync(CreateStaffMovementChecklistItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedMovementAsync(createDto.MovementId);

        if (createDto.ResponsiblePersonId.HasValue)
            await GetOwnedEmployeeAsync(createDto.ResponsiblePersonId.Value, "responsible");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _checklistRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteChecklistItemAsync(CompleteChecklistItemDto dto, Guid completedByUserId, bool actorIsHr, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistItemAsync(dto.ItemId);

        // A checklist item names who owes it (IT access revoked, assets returned, payroll updated).
        // That was recorded and never enforced, so anyone could sign off anyone's task. HR keeps an
        // override because items outlive their owners.
        if (entity.ResponsiblePersonId.HasValue &&
            entity.ResponsiblePersonId.Value != completedByUserId &&
            !actorIsHr)
            throw new UnauthorizedAccessException("This checklist item is assigned to someone else.");

        if (entity.IsCompleted)
            throw new InvalidOperationException("This checklist item is already complete.");

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

        var pendingApprovals    = (await _movementRepo.GetPendingApprovalAsync(tenantId)).ToList();
        var pendingHandover     = (await _movementRepo.GetPendingHandoverAsync(tenantId)).ToList();
        var expiringAssignments = (await _movementRepo.GetExpiringTemporaryAssignmentsAsync(tenantId, 60)).ToList();

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

    /// <remarks>
    /// Numbers come from the highest suffix already issued, over rows INCLUDING soft-deleted ones —
    /// not from a count of live rows. Counting re-issues a number the moment anything is deleted: the
    /// count drops back, and the next movement collides with a number still held by the deleted row's
    /// unique index. Soft-deleted rows keep their numbers, so they have to keep their place in the
    /// sequence too. The same shape was fixed across the SHE generators.
    /// </remarks>
    private async Task<string> GenerateMovementNumberAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var prefix   = $"MOV-{DateTime.UtcNow:yyyyMMdd}-";

        var issued = await _movementRepo
            .GetQueryableIncludingDeleted(m => m.TenantId == tenantId && m.MovementNumber.StartsWith(prefix))
            .Select(m => m.MovementNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D4}";
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
