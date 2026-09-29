using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
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

    // Terms of employment are the employee service's to write — one door, one supersede rule.
    private readonly IEmployeeService _employees;

    // Round 4, lane I3: an implemented transfer or promotion is an orientation trigger.
    private readonly IOrientationEnrollmentTriggerService _orientationTriggers;

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
        IEmployeeService employees,
        IOrientationEnrollmentTriggerService orientationTriggers,
        ILogger<StaffMovementService> logger)
    {
        _orientationTriggers = orientationTriggers;
        _movementRepo   = movementRepo;
        _approvalRepo   = approvalRepo;
        _historyRepo    = historyRepo;
        _attachmentRepo = attachmentRepo;
        _checklistRepo  = checklistRepo;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserProvider = currentUserProvider;
        _unitOfWork     = unitOfWork;
        _employees      = employees;
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
        await RequirePayrollForSalaryChangeAsync(entity, cancellationToken);
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
        await RequirePayrollForSalaryChangeAsync(entity, cancellationToken);

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

        // Establishment pressure is reported, not enforced — see the note on the method. It goes
        // into the movement's own history so the approver sees it on the record they are approving,
        // rather than only in a screen they may never open.
        var establishmentWarning = await DescribeEstablishmentPressureAsync(entity, cancellationToken);

        var previousStatus = entity.Status;
        entity.RequestSubmissionDate = DateTime.UtcNow;

        // ── Submitting must never approve (2026-09-15) ─────────────────────────────────────────
        // WorkflowIntegrationService.SubmitAsync returns WorkflowOutcome.Approved whenever no
        // active definition exists for the entity type, and StaffMovementWorkflowStatusAdapter
        // maps Approved to Approved (or EmployeeAcceptancePending) and stamps AuthorizationDate.
        // ⚠ Corrected 2026-09-16: this said "No StaffMovement definition is seeded anywhere in the
        // solution". It IS seeded, published and active by EnsureHrWorkflowsSeededAsync, so on a
        // seeded tenant this path never ran. Kept as defence in depth for an unseeded tenant — see
        // HrWorkflowFallbackAuthority. Where it DID run, pressing Submit took
        // a promotion or transfer Draft → Approved in one step with nobody having reviewed it —
        // and the block below then wrote AuthorizedById = the submitter, recording the person who
        // asked for the move as the person who authorised it. That is a false audit fact, not just
        // a missing gate.
        //
        // Found through recruitment's G-4.1/G-10.1; see HrWorkflowFallbackAuthority for the full
        // mechanism and why approve, reject and recall below needed changing at the same time.
        var hasWorkflow = await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(
                workflowResult.ExecutionResult.Message ?? "Failed to start the movement approval workflow.");

        var submitOutcome = hasWorkflow ? workflowResult.Outcome : WorkflowOutcome.Pending;

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        // A definition with one step approves on submission, so the authoriser has to be stamped
        // here too — the adapter only knows the ApplicationUser id, and this column is an Employee FK.
        // Reachable only on the configured path now: with no definition the movement lands at
        // Submitted, so there is no authoriser yet to stamp.
        if (entity.Status == StaffMovementStatus.Approved)
            entity.AuthorizedById = submittedByEmployeeId;

        await _movementRepo.UpdateAsync(entity);

        var submissionNote = string.IsNullOrWhiteSpace(establishmentWarning)
            ? dto.SubmissionNotes
            : string.IsNullOrWhiteSpace(dto.SubmissionNotes)
                ? $"Establishment note: {establishmentWarning}"
                : $"{dto.SubmissionNotes} — establishment note: {establishmentWarning}";

        await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status, submissionNote, submittedByEmployeeId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (establishmentWarning != null)
            _logger.LogInformation("Staff movement {MovementNumber} submitted over establishment: {Warning}",
                entity.MovementNumber, establishmentWarning);

        _logger.LogInformation("Staff movement submitted: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    public async Task<string?> GetEstablishmentAdvisoryAsync(Guid movementId, CancellationToken cancellationToken = default)
    {
        var movement = await GetOwnedMovementAsync(movementId);
        return await DescribeEstablishmentPressureAsync(movement, cancellationToken);
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

        // With no definition published there is no instance, so CanUserApproveAsync answers false
        // about every movement and this inbox would come back EMPTY for everyone - while movements
        // sit in it, because that is where submitting now leaves them. Whoever holds the fallback
        // authority sees them all; they are the people who can actually decide one.
        if (!await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            return HrWorkflowFallbackAuthority.CanRuleWithoutWorkflow(
                       _currentUserProvider, HrPermissions.ApproveMovements)
                ? submitted.ToSummaryDtoList()
                : Enumerable.Empty<StaffMovementSummaryDto>();
        }

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

        // Segregation of duties. RequestedById and approvingEmployeeId are both Employee ids, so
        // this compares like with like. It runs on both paths: a published definition's
        // preventInitiatorApproval covers the same ground by ApplicationUser, and this one still
        // catches a requester approving through a second login.
        if (entity.RequestedById == approvingEmployeeId)
            throw new InvalidOperationException("You cannot approve a movement that you requested yourself.");

        var previousStatus = entity.Status;

        // Two paths — see HrWorkflowFallbackAuthority. With no definition published the engine has
        // no instance to answer about: CanUserApproveAsync returns false and ProcessApprovalAsync
        // has nothing to process, so a movement submitted on an unconfigured tenant would be
        // unapprovable. Authority then falls to the movements administer tier.
        WorkflowOutcome approvalOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, movementId, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, movementId, userId, "Approve", comments);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

            approvalOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "approve a staff movement", HrPermissions.ApproveMovements);
            approvalOutcome = WorkflowOutcome.Approved;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        // ⚠ EmployeeAcceptancePending counts as cleared, and leaving it out meant a movement that
        // requires employee acceptance NEVER recorded who authorised it. This condition read
        // `== Approved` only; an acceptance-requiring movement goes to EmployeeAcceptancePending
        // at final approval instead, and `RespondAsync` — which moves it on to Approved — stamps
        // the flags and the status and not this. So AuthorizedById stayed null for the whole class,
        // permanently, on a field the DTO exposes and screens render.
        //
        // The authoriser is the approver who cleared the last step, not the employee who then
        // accepted: accepting is not authorising, and stamping the subject here would say the
        // person moved approved their own move.
        if (entity.Status is StaffMovementStatus.Approved
                          or StaffMovementStatus.EmployeeAcceptancePending)
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

        // Skipped when nothing is published: RecallWorkflowAsync answers "No active workflow found"
        // without an instance. On the configured path the engine enforces requester-only; on the
        // unconfigured one that rule has to be made here, against the Employee id the caller passed.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, movementId, userId, reason);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to recall the movement.");
        }
        else if (entity.RequestedById != recallingEmployeeId)
        {
            throw new InvalidOperationException("Only the person who requested a movement can recall it.");
        }

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

        entity.EmployeeAccepted     = dto.Accepted;
        entity.EmployeeResponseDate = DateTime.UtcNow;
        entity.EmployeeComments     = dto.Comments;

        // Either answer leaves the acceptance-pending stage (the workflow adapter is what puts a
        // movement there now — area 25 slice 7). Approved is the entity's historical post-response
        // status either way: the answer itself lives on the flags, and the implementation gate
        // already refuses an unaccepted movement.
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

        // Same two paths as ApproveAsync. Note that the docstring above — "every rejection goes
        // through the engine, so the instance is closed" — describes the configured path; with no
        // definition there is no instance to close, and without this branch a submitted movement
        // could be neither approved nor rejected nor recalled.
        WorkflowOutcome rejectionOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, dto.MovementId, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                EntityType, dto.MovementId, userId, "Reject", dto.RejectionReason);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

            rejectionOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "reject a staff movement", HrPermissions.ApproveMovements);
            rejectionOutcome = WorkflowOutcome.Rejected;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, dto.RejectionReason);

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
        //
        // No HasActiveApprovalWorkflowAsync guard here, unlike recall: with nothing published there
        // is no instance, and SimpleWorkflowService.CancelWorkflowAsync answers Success = false /
        // "No active workflow found" rather than throwing. The result is deliberately discarded, so
        // the cancellation goes through either way. (It would throw only if the WorkflowEntityType
        // row itself were missing, which is a misconfiguration, not the unseeded-definition case.)
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

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var owned = _unitOfWork.HasActiveTransaction;
            if (!owned) await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var previousStatus = entity.Status;
                entity.ReturnProcessed  = true;
                entity.ActualReturnDate = dto.ActualReturnDate;
                entity.ReturnMovementId = dto.ReturnMovementId;
                entity.Status           = StaffMovementStatus.Implemented;

                // Coming back is a move too. If the secondment was applied, the employee is sitting
                // in the host post; flagging the return without putting them back would leave them
                // there for good — the assignment would be temporary only on paper.
                if (entity.Status == StaffMovementStatus.Implemented && entity.CurrentPositionId != Guid.Empty)
                    await ReverseTemporaryAssignmentAsync(entity, processedByUserId, cancellationToken);

                await _movementRepo.UpdateAsync(entity);
                await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status,
                    dto.Notes ?? "Return from temporary assignment processed", processedByUserId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (!owned) await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (!owned) await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);

        _logger.LogInformation("Return processed for temporary movement: {MovementNumber}", entity.MovementNumber);

        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // APPLYING THE MOVEMENT
    //
    // Until this slice, implementing a movement flipped a status and stopped. Nothing wrote the
    // employee's position, unit, reporting line or pay; EmployeeCareerPath had no automatic writer
    // anywhere in the solution; Employee.LastPromotionDate was never assigned. A movement was
    // paperwork that described a change nobody had made.
    //
    // FR-HR-173: "validate vacancy availability before promotion approval, and update grade, salary
    // scale, position and reporting line on approval."
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Puts an employee back where they were before a temporary assignment.
    ///
    /// The movement holds both sides, so the return is the same write with the halves swapped: the
    /// CURRENT snapshot taken when the secondment was raised is where they belong afterwards.
    /// </summary>
    private async Task ReverseTemporaryAssignmentAsync(
        StaffMovement movement, Guid actorEmployeeId, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(movement.EmployeeId);
        if (employee == null) return;

        // Only put them back if they are actually where the movement left them; if something else
        // has moved them since, that later placement is the truth and this must not overwrite it.
        if (employee.PositionId != movement.NewPositionId) return;

        var returnDate = (movement.ActualReturnDate ?? DateTime.UtcNow).Date;

        employee.PositionId = movement.CurrentPositionId;
        employee.OrganizationUnitId = movement.CurrentOrganizationUnitId;
        if (movement.CurrentOrganizationLevelId.HasValue) employee.OrganizationLevelId = movement.CurrentOrganizationLevelId;
        if (movement.CurrentLocationId.HasValue) employee.LocationId = movement.CurrentLocationId;
        if (movement.CurrentLocationLevelId.HasValue) employee.LocationLevelId = movement.CurrentLocationLevelId;
        if (movement.CurrentSupervisorId.HasValue) employee.ManagerId = movement.CurrentSupervisorId;
        // Restoring the pre-movement salary to somebody since taken off payroll would put a pay
        // figure back on a record the run does not pay; leave it as the flip left it.
        if (movement.CurrentSalary > 0 && employee.IsOnPayroll) employee.Salary = movement.CurrentSalary;

        employee.UpdatedAt = DateTime.UtcNow;
        employee.UpdatedBy = actorEmployeeId.ToString();
        await _unitOfWork.Repository<Employee>().UpdateAsync(employee);

        // Coming back is a change of terms as much as going was: an acting appointment that paid an
        // allowance ends, and the contract has to say so from the day they return. Same helper, so
        // the return cannot leave the pair of rows in a state the outward move could not.
        if (movement.CurrentSalary > 0 && employee.IsOnPayroll)
            await _employees.SupersedeCurrentContractAsync(
                movement.EmployeeId,
                DateOnly.FromDateTime(returnDate),
                movement.CurrentSalary,
                newEmploymentType: null,
                $"{movement.MovementNumber}: returned from temporary assignment",
                cancellationToken);

        // The placement goes back too (round 3, lane H): the acting grade ends on the return date
        // and the grade they held before — snapshotted at implementation — resumes the day after.
        // Someone since taken off payroll or moved to a negotiated amount is left as that change
        // left them, for the same reason the salary above is.
        if (movement.NewSalaryGradeId.HasValue && employee.IsOnPayroll && employee.PayBasis == PayBasis.SalaryScale)
        {
            if (movement.CurrentSalaryGradeId is { } backGradeId)
                await PlaceOnScaleAsync(movement, employee, backGradeId, movement.CurrentSalaryLevelId, movement.CurrentSalaryNotchId,
                    returnDate.AddDays(1), $"{movement.MovementNumber}: returned from temporary assignment", cancellationToken);
            else
                await EndMovementPlacementAsync(movement, returnDate, cancellationToken);
        }

        var careerRepo = _unitOfWork.Repository<EmployeeCareerPath>();
        var openStep = await careerRepo
            .GetQueryable(c => c.TenantId == movement.TenantId
                            && c.EmployeeId == movement.EmployeeId
                            && c.IsCurrent)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (openStep != null)
        {
            openStep.IsCurrent = false;
            openStep.EndDate = returnDate;
            await careerRepo.UpdateAsync(openStep);
        }

        await careerRepo.AddAsync(new EmployeeCareerPath
        {
            TenantId = movement.TenantId,
            EmployeeId = movement.EmployeeId,
            PositionId = movement.CurrentPositionId,
            OrganizationUnitId = movement.CurrentOrganizationUnitId,
            OrganizationLevelId = movement.CurrentOrganizationLevelId,
            LocationId = movement.CurrentLocationId,
            LocationLevelId = movement.CurrentLocationLevelId,
            StartDate = returnDate.AddDays(1),
            IsCurrent = true,
            MovementId = movement.Id,
            Salary = movement.CurrentSalary,
            SalaryGradeId = movement.CurrentSalaryGradeId,
            SalaryLevelId = movement.CurrentSalaryLevelId,
            SalaryNotchId = movement.CurrentSalaryNotchId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorEmployeeId.ToString(),
        });

        var historyRepo = _unitOfWork.Repository<EmployeePositionHistory>();
        var openHistory = await historyRepo
            .GetQueryable(h => h.TenantId == movement.TenantId
                            && h.EmployeeId == movement.EmployeeId
                            && h.EndDate == null)
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (openHistory != null)
        {
            openHistory.EndDate = returnDate;
            await historyRepo.UpdateAsync(openHistory);
        }

        await historyRepo.AddAsync(new EmployeePositionHistory
        {
            TenantId = movement.TenantId,
            EmployeeId = movement.EmployeeId,
            PositionId = movement.CurrentPositionId,
            OrganizationUnitId = movement.CurrentOrganizationUnitId,
            OrganizationLevelId = movement.CurrentOrganizationLevelId ?? Guid.Empty,
            LocationId = movement.CurrentLocationId,
            LocationLevelId = movement.CurrentLocationLevelId,
            StartDate = returnDate.AddDays(1),
            // Coming back from cover is not itself a secondment — the employee is resuming the post
            // they never gave up, which is closest to their original assignment.
            ChangeReason = PositionChangeReason.InitialAssignment,
            Notes = $"{movement.MovementNumber}: returned from temporary assignment",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorEmployeeId.ToString(),
        });
    }

    /// <summary>
    /// Reports how the destination position stands against its establishment (FR-HR-173).
    ///
    /// <para><b>⚠ Advisory only where the establishment was never authorised — area 17 slice 8.</b>
    /// A post whose <c>EstablishmentApprovedOn</c> is set has had its headcount approved by
    /// Department Head, HR and the Managing Director (FR-HR-135), and moving someone into it beyond
    /// that number is now <b>refused</b> rather than noted. The paragraph below records why it was
    /// advisory in the first place, and it still governs every position nobody has established.</para>
    ///
    /// <para><b>Advisory, not a block — a decision taken with the user.</b> The rule as specified
    /// refuses a move into a full post, and that is the right rule the day the establishment is
    /// maintained. It is not maintained: in the live tenant 132 of 146 positions still carry
    /// <c>ExpectedHeadcount = 1</c>, the entity default, while one of them holds over a thousand
    /// people. Enforced as a block it would refuse very nearly every movement, and a rule that
    /// refuses everything is one people route around rather than obey. Reported instead, with the
    /// actual figures, it is the thing most likely to get the establishment filled in.</para>
    ///
    /// <para>Temporary assignments are exempt: a secondment or an acting appointment is cover, and
    /// the substantive holder still occupies the post. A move that does not change position is
    /// exempt for the obvious reason.</para>
    ///
    /// <para>Returns null when there is nothing to say.</para>
    /// </summary>
    private async Task<string?> DescribeEstablishmentPressureAsync(
        StaffMovement movement, CancellationToken cancellationToken)
    {
        if (movement.IsTemporary) return null;

        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(movement.EmployeeId);
        if (employee != null && employee.PositionId == movement.NewPositionId) return null;

        var position = await _unitOfWork.Repository<EmployeePosition>().GetByIdAsync(movement.NewPositionId);
        if (position == null || position.IsDeleted || position.TenantId != movement.TenantId)
            throw new ArgumentException($"The position with ID '{movement.NewPositionId}' was not found.");

        var occupied = await _unitOfWork.Repository<Employee>()
            .GetQueryable(e => e.TenantId == movement.TenantId
                            && e.PositionId == movement.NewPositionId
                            && e.IsActive)
            .CountAsync(cancellationToken);

        if (occupied < position.ExpectedHeadcount) return null;

        var note = $"{position.Title} is established for {position.ExpectedHeadcount} and already has {occupied} " +
                   $"in post. This movement takes it to {occupied + 1}.";

        // ⚠ PROMOTED FROM ADVISORY, but only where the establishment was actually authorised.
        //
        // Area 8 downgraded FR-HR-173 to a note because ExpectedHeadcount could not be trusted: 132
        // of 146 positions carried the column default of 1, so refusing a move into a "full" post
        // would have refused very nearly every movement. Area 17 slice 8 gives the column the thing
        // it was missing — EstablishmentApprovedOn, stamped when a manpower budget completes
        // FR-HR-135's chain — so an authorised establishment is now distinguishable from an
        // untouched default.
        //
        // Where it IS authorised, three people have said how many posts exist and the rule as
        // specified applies. Where it is not, the note stays a note, exactly as before. The rule did
        // not change; the data caught up with it.
        if (position.EstablishmentApprovedOn != null)
            throw new InvalidOperationException(
                $"This movement cannot be submitted: {note} That establishment was approved on " +
                $"{position.EstablishmentApprovedOn:dd MMM yyyy}. Revise the manpower budget for the " +
                "position, or move the employee into a post with capacity.");

        return note;
    }

    /// <summary>
    /// Applies the movement to the employee and writes both history stores.
    ///
    /// <para>Both, deliberately. EmployeeCareerPath is the movement-linked record with the salary
    /// grade snapshot; EmployeePositionHistory is the timeline the employee-detail screen has
    /// rendered since area 1. They are two stores of the same fact, and a movement that updated only
    /// one would leave the other quietly lying.</para>
    ///
    /// <para>Payroll is NOT touched. Employee.Salary is the HR-side figure and ours to write; the
    /// pay run belongs to the payroll module, and this records that the movement happened rather
    /// than instructing anyone to pay differently.</para>
    /// </summary>
    /// <summary>
    /// A movement may carry a new salary or a new grade/level/notch. Both are pay-structure facts,
    /// and a pay-structure fact about somebody the payroll run does not pay is a contradiction —
    /// so it is refused when the movement is raised, not discovered at implementation after three
    /// approvals. A movement with no salary and no grade (a plain transfer, a secondment) is
    /// unaffected.
    /// </summary>
    private async Task RequirePayrollForSalaryChangeAsync(StaffMovement movement, CancellationToken cancellationToken)
    {
        var carriesPay = movement.NewSalary > 0
                      || movement.NewSalaryGradeId.HasValue
                      || movement.NewSalaryLevelId.HasValue
                      || movement.NewSalaryNotchId.HasValue;
        if (!carriesPay) return;

        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(movement.EmployeeId);
        if (employee != null && !employee.IsOnPayroll)
            throw new InvalidOperationException(
                $"{employee.EmployeeNumber} is not on payroll, so a movement cannot set a salary or a salary grade for them. Put them on payroll first, or raise the movement without pay details.");

        // Round 3, lane H. A movement that names a grade is a placement on the scale, and the two
        // rules every placement obeys apply at raise time: not for somebody paid a negotiated
        // amount, and the notch/level must belong to the grade. The resolver is the one the Salary
        // tab's own placement door uses, so a movement cannot say something the tab would refuse.
        if (movement.NewSalaryGradeId is { } gradeId)
        {
            if (employee != null && employee.PayBasis == PayBasis.Negotiated)
                throw new InvalidOperationException(NegotiatedSentence(employee));
            await _employees.ResolvePlacementLevelAsync(gradeId, movement.NewSalaryLevelId, movement.NewSalaryNotchId, cancellationToken);
        }
        else if (movement.NewSalaryLevelId.HasValue || movement.NewSalaryNotchId.HasValue)
        {
            throw new InvalidOperationException("A salary level or notch on a movement needs its grade. Choose the grade as well.");
        }
    }

    private static string NegotiatedSentence(Employee employee) =>
        $"{employee.EmployeeNumber} is paid a negotiated amount, so a movement cannot place them on the salary scale. Change their pay basis to the scale first, or raise the movement with a salary figure only.";

    /// <summary>
    /// The movement's CURRENT grade/level/notch snapshot is what a temporary assignment returns to.
    /// The form rarely fills it (it reads the employee header, which has no placement), so it is
    /// taken here, at implementation, from the placement in force on the day — before the new one
    /// is written over it.
    /// </summary>
    private async Task SnapshotCurrentPlacementAsync(StaffMovement movement, CancellationToken cancellationToken)
    {
        if (movement.CurrentSalaryGradeId.HasValue) return;

        var today = DateTime.UtcNow.Date;
        var inForce = await _unitOfWork.Repository<EmployeeSalaryAssignment>().GetQueryable()
            .Where(a => a.EmployeeId == movement.EmployeeId && !a.IsDeleted && a.WithdrawnAt == null
                     && a.EffectiveDate <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (inForce == null) return;

        movement.CurrentSalaryGradeId = inForce.GradeId;
        movement.CurrentSalaryLevelId = inForce.LevelId;
        movement.CurrentSalaryNotchId = inForce.NotchId;
    }

    /// <summary>
    /// Writes the placement a movement names, through the same door as the Salary tab — so it
    /// inherits the level resolver, the withdrawal rule for a placement that has not started, and
    /// the payroll gate. The reason carries the movement number so the return path can find it.
    /// </summary>
    private async Task PlaceOnScaleAsync(
        StaffMovement movement, Employee employee, Guid gradeId, Guid? levelId, Guid? notchId,
        DateTime effective, string reason, CancellationToken cancellationToken)
    {
        if (employee.PayBasis == PayBasis.Negotiated)
            throw new InvalidOperationException(NegotiatedSentence(employee));

        await _employees.AssignSalaryAsync(new CreateEmployeeSalaryAssignmentDto
        {
            EmployeeId = movement.EmployeeId,
            GradeId = gradeId,
            LevelId = levelId,
            NotchId = notchId,
            EffectiveDate = effective,
            AssignmentReason = reason,
        }, cancellationToken, SalaryChangeAuthority.Approved);
    }

    /// <summary>
    /// A temporary assignment with no outgoing placement to return to (nobody was placed before it)
    /// simply ends the placement the movement opened, on the return date.
    /// </summary>
    private async Task EndMovementPlacementAsync(StaffMovement movement, DateTime returnDate, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var prefix = movement.MovementNumber + ":";
        var open = await repo.GetQueryable()
            .Where(a => a.EmployeeId == movement.EmployeeId && !a.IsDeleted && a.WithdrawnAt == null
                     && a.EffectiveTo == null && a.AssignmentReason.StartsWith(prefix))
            .ToListAsync(cancellationToken);
        foreach (var a in open)
        {
            a.EffectiveTo = returnDate;
            await repo.UpdateAsync(a);
        }
    }

    private async Task ApplyMovementToEmployeeAsync(
        StaffMovement movement, Guid actorEmployeeId, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(movement.EmployeeId)
            ?? throw new ArgumentException($"The employee for movement {movement.MovementNumber} was not found.");

        var effective = movement.EffectiveDate.Date;
        var vacatedPositionId = employee.PositionId;

        // ── The live employee record ──────────────────────────────────────────
        employee.PositionId = movement.NewPositionId;
        employee.OrganizationUnitId = movement.NewOrganizationUnitId;
        if (movement.NewOrganizationLevelId.HasValue) employee.OrganizationLevelId = movement.NewOrganizationLevelId;
        if (movement.NewLocationId.HasValue) employee.LocationId = movement.NewLocationId;
        if (movement.NewLocationLevelId.HasValue) employee.LocationLevelId = movement.NewLocationLevelId;
        if (movement.NewSupervisorId.HasValue) employee.ManagerId = movement.NewSupervisorId;
        // The gate ran at create/update; this is the same rule at the moment of writing, in case
        // the person was taken off payroll while the movement sat in approval.
        if (movement.NewSalary > 0 && !employee.IsOnPayroll)
            throw new InvalidOperationException(
                $"{employee.EmployeeNumber} is no longer on payroll, so the new salary on movement {movement.MovementNumber} cannot be applied. Put them on payroll first, or remove the salary from the movement.");
        if (movement.NewSalary > 0) employee.Salary = movement.NewSalary;

        if (movement.MovementType == StaffMovementType.Promotion)
            employee.LastPromotionDate = effective;

        employee.UpdatedAt = DateTime.UtcNow;
        employee.UpdatedBy = actorEmployeeId.ToString();
        await _unitOfWork.Repository<Employee>().UpdateAsync(employee);

        // G-3.2: the post they came FROM is now a seat short. This is the promotion/transfer half
        // of the guarantee PositionVacancy's documentation made and nothing kept — and it is the
        // half most likely to be missed, because unlike a termination nobody has left the company,
        // so no exit process runs. Skipped when the movement does not actually change post (a
        // salary-only or reporting-line movement), which would otherwise log a phantom vacancy on
        // a seat still occupied by the same person.
        if (vacatedPositionId != Guid.Empty && vacatedPositionId != movement.NewPositionId)
        {
            await PositionVacancyLog.LogDepartureAsync(
                _unitOfWork,
                movement.TenantId,
                employee.Id,
                vacatedPositionId,
                movement.MovementType == StaffMovementType.Promotion
                    ? VacancyReason.Promotion
                    : VacancyReason.Transfer,
                effective,
                _currentUserProvider.UserId,
                note: $"Vacated by movement {movement.MovementNumber}.",
                cancellationToken: cancellationToken);
        }

        // ── The terms of employment ───────────────────────────────────────────
        //
        // E-7d. New pay is new terms. Until lane D1 a promotion updated Employee.Salary, the career
        // path and the position history, and left the CONTRACT still quoting the old figure — the
        // one document the organisation would produce if asked what it pays this person.
        //
        // ⚠ A conversion between employment types (contract → permanent, the case the feedback
        // named) cannot be driven from here: StaffMovement carries no NewEmploymentType, only a new
        // position, unit, location, supervisor and pay. Converting somebody today means changing the
        // employment type on the employee header, which writes through to the current contract. A
        // column on the movement is a schema change, and is recorded as owed rather than smuggled in.
        await _employees.SupersedeCurrentContractAsync(
            movement.EmployeeId,
            DateOnly.FromDateTime(effective),
            movement.NewSalary > 0 ? movement.NewSalary : null,
            newEmploymentType: null,
            $"{movement.MovementNumber}: {movement.Reason}",
            cancellationToken);

        // ── The grade placement (round 3, lane H — owed since round 2) ────────
        //
        // Until now a promotion moved the position, the unit, the manager, Employee.Salary and the
        // contract, and left EmployeeSalaryAssignment saying the OLD grade — whose notch HrBasicPay
        // quotes first, so the person read as promoted everywhere except in what they were paid.
        // The movement's grade/level/notch landed only in the career-path row, which no pay
        // resolver reads. The movement is approved by the time it is implemented, so it writes the
        // placement itself rather than raising a salary change request (round-3 D-1).
        if (movement.NewSalaryGradeId is { } newGradeId)
        {
            await SnapshotCurrentPlacementAsync(movement, cancellationToken);
            await PlaceOnScaleAsync(movement, employee, newGradeId, movement.NewSalaryLevelId, movement.NewSalaryNotchId,
                effective, $"{movement.MovementNumber}: {movement.MovementType}", cancellationToken);
        }

        // ── Career path: close the open step, open the new one ────────────────
        var careerRepo = _unitOfWork.Repository<EmployeeCareerPath>();

        var openStep = await careerRepo
            .GetQueryable(c => c.TenantId == movement.TenantId
                            && c.EmployeeId == movement.EmployeeId
                            && c.IsCurrent)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (openStep != null)
        {
            openStep.IsCurrent = false;
            // The day before the new step starts, so the two do not both claim the same day.
            openStep.EndDate = effective.AddDays(-1);
            openStep.UpdatedAt = DateTime.UtcNow;
            openStep.UpdatedBy = actorEmployeeId.ToString();
            await careerRepo.UpdateAsync(openStep);
        }

        await careerRepo.AddAsync(new EmployeeCareerPath
        {
            TenantId = movement.TenantId,
            EmployeeId = movement.EmployeeId,
            PositionId = movement.NewPositionId,
            OrganizationUnitId = movement.NewOrganizationUnitId,
            OrganizationLevelId = movement.NewOrganizationLevelId,
            LocationId = movement.NewLocationId,
            LocationLevelId = movement.NewLocationLevelId,
            StartDate = effective,
            IsCurrent = true,
            MovementId = movement.Id,
            Salary = movement.NewSalary,
            SalaryGradeId = movement.NewSalaryGradeId,
            SalaryLevelId = movement.NewSalaryLevelId,
            SalaryNotchId = movement.NewSalaryNotchId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorEmployeeId.ToString(),
        });

        // ── Position history: the timeline area 1 already renders ─────────────
        var historyRepo = _unitOfWork.Repository<EmployeePositionHistory>();

        var openHistory = await historyRepo
            .GetQueryable(h => h.TenantId == movement.TenantId
                            && h.EmployeeId == movement.EmployeeId
                            && h.EndDate == null)
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (openHistory != null)
        {
            openHistory.EndDate = effective.AddDays(-1);
            openHistory.UpdatedAt = DateTime.UtcNow;
            openHistory.UpdatedBy = actorEmployeeId.ToString();
            await historyRepo.UpdateAsync(openHistory);
        }

        await historyRepo.AddAsync(new EmployeePositionHistory
        {
            TenantId = movement.TenantId,
            EmployeeId = movement.EmployeeId,
            PositionId = movement.NewPositionId,
            OrganizationUnitId = movement.NewOrganizationUnitId,
            OrganizationLevelId = movement.NewOrganizationLevelId ?? Guid.Empty,
            LocationId = movement.NewLocationId,
            LocationLevelId = movement.NewLocationLevelId,
            StartDate = effective,
            ChangeReason = MapChangeReason(movement.MovementType),
            Notes = $"{movement.MovementNumber}: {movement.Reason}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorEmployeeId.ToString(),
        });
    }

    /// <summary>
    /// Every movement type has a reason of its own.
    ///
    /// A lateral move is recorded as a Transfer because that is what it is — an equivalent post
    /// elsewhere — but a secondment, an acting appointment and a redesignation each got their own
    /// member on <see cref="PositionChangeReason"/> rather than sharing Other, which would have left
    /// the timeline unable to say whether someone had ever really left their post.
    /// </summary>
    private static PositionChangeReason MapChangeReason(StaffMovementType type) => type switch
    {
        StaffMovementType.Promotion => PositionChangeReason.Promotion,
        StaffMovementType.Demotion => PositionChangeReason.Demotion,
        StaffMovementType.Transfer => PositionChangeReason.Transfer,
        StaffMovementType.LateralMove => PositionChangeReason.Transfer,
        StaffMovementType.Secondment => PositionChangeReason.Secondment,
        StaffMovementType.ActingAppointment => PositionChangeReason.ActingAppointment,
        StaffMovementType.Redesignation => PositionChangeReason.Redesignation,
        _ => PositionChangeReason.Other,
    };

    /// <summary>
    /// Marks the movement as carried out AND carries it out.
    ///
    /// The three gates below were all modelled and none was enforced: RequiresEmployeeAcceptance,
    /// RequiresHandover and the required checklist items existed as flags and a helper that nothing
    /// called. A movement could be implemented over an employee who had declined it, with the
    /// outgoing duties un-handed-over and the access-revocation tasks still open.
    ///
    /// The whole thing runs in one transaction: a half-applied movement — employee moved, history
    /// not written, or the reverse — is worse than one that failed cleanly and can be retried.
    /// </summary>
    public async Task<bool> ImplementAsync(Guid movementId, Guid implementedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMovementAsync(movementId);

        if (entity.Status == StaffMovementStatus.Implemented)
            throw new InvalidOperationException("This movement has already been implemented.");

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


        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var owned = _unitOfWork.HasActiveTransaction;
            if (!owned) await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var previousStatus = entity.Status;
                entity.Status = StaffMovementStatus.Implemented;

                await ApplyMovementToEmployeeAsync(entity, implementedByUserId, cancellationToken);

                await _movementRepo.UpdateAsync(entity);
                await RecordStatusHistoryAsync(entity.TenantId, entity.Id, previousStatus, entity.Status,
                    "Movement implemented and applied to the employee record", implementedByUserId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (!owned) await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (!owned) await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);

        _logger.LogInformation("Staff movement implemented and applied: {MovementNumber}", entity.MovementNumber);

        // Round 4, lane I3 — outside the movement's transaction, after its commit, best-effort: the
        // movement stands whatever orientation makes of it. A movement implemented ahead of its
        // effective date fires nothing now; the nightly sweep fires it when the date comes.
        await _orientationTriggers.OnMovementImplementedAsync(entity.Id, cancellationToken);

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
