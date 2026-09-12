using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingNominationService : ITrainingNominationService
{
    /// <summary>Entity-type key used to look up a configurable approval workflow for nominations.</summary>
    private const string WorkflowEntityType = "TrainingNomination";

    private readonly ITrainingNominationRepository _nominationRepository;
    private readonly ITrainingAttendanceRepository _attendanceRepository;
    private readonly ITrainingFeedbackRepository _feedbackRepository;
    private readonly ITrainingFollowUpAssessmentRepository _followUpRepository;
    private readonly ITrainingScheduleRepository _scheduleRepository;
    private readonly IGenericRepository<TrainerProfile> _trainerProfileRepository;
    private readonly ITrainingServiceBondService _bondService;
    private readonly IWorkflowIntegrationService _workflowIntegration;
    private readonly IWorkflowStatusAdapterRegistry _adapterRegistry;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INumberSequenceService _numberSequence;
    private readonly ILogger<TrainingNominationService> _logger;

    public TrainingNominationService(
        ITrainingNominationRepository nominationRepository,
        ITrainingAttendanceRepository attendanceRepository,
        ITrainingFeedbackRepository feedbackRepository,
        ITrainingFollowUpAssessmentRepository followUpRepository,
        ITrainingScheduleRepository scheduleRepository,
        IGenericRepository<TrainerProfile> trainerProfileRepository,
        ITrainingServiceBondService bondService,
        IWorkflowIntegrationService workflowIntegration,
        IWorkflowStatusAdapterRegistry adapterRegistry,
        ICurrentUserService currentUser,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ILogger<TrainingNominationService> logger)
    {
        _nominationRepository = nominationRepository;
        _attendanceRepository = attendanceRepository;
        _feedbackRepository = feedbackRepository;
        _followUpRepository = followUpRepository;
        _scheduleRepository = scheduleRepository;
        _trainerProfileRepository = trainerProfileRepository;
        _bondService = bondService;
        _workflowIntegration = workflowIntegration;
        _adapterRegistry = adapterRegistry;
        _currentUser = currentUser;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
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

    // A nomination owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<TrainingNomination> GetOwnedNominationAsync(Guid id)
    {
        var entity = await _nominationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training nomination with ID '{id}' not found.");
        return entity;
    }

    // Same "missing, not forbidden" rule as above, for the follow-up assessments this service mutates.
    private async Task<TrainingFollowUpAssessment> GetOwnedFollowUpAsync(Guid id)
    {
        var entity = await _followUpRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Follow-up assessment with ID '{id}' not found.");
        return entity;
    }

    // ── Nomination queries ────────────────────────────────────────────────────

    public async Task<TrainingNominationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _nominationRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training nomination with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<TrainingNominationDto?> GetByNominationNumberAsync(string nominationNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _nominationRepository.GetByNominationNumberAsync(nominationNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PagedResult<TrainingNominationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Scoping the query before the count matters: an unscoped CountAsync reports other tenants' rows
        // in TotalCount and breaks the pager.
        var query = _nominationRepository.GetQueryable().Where(n => n.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        // Eager-load the navigations the summary DTO reads, otherwise the list shows blank
        // ProgramName/EmployeeName (or degrades into N+1 queries under lazy loading).
        var items = await query
            .Include(n => n.Schedule).ThenInclude(s => s.Program)
            .Include(n => n.Employee)
            .OrderByDescending(n => n.NominationDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TrainingNominationSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingNominationSummaryDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _nominationRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNominationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _nominationRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNominationSummaryDto>> GetByStatusAsync(NominationStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _nominationRepository.GetByStatusAsync(status);
        return entities.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNominationSummaryDto>> GetPendingSupervisorApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _nominationRepository.GetPendingSupervisorApprovalAsync();
        return entities.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNominationSummaryDto>> GetPendingHrApprovalAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _nominationRepository.GetPendingHrApprovalAsync();
        return entities.Where(n => n.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Nomination CRUD ───────────────────────────────────────────────────────

    public async Task<TrainingNominationDto> CreateAsync(CreateTrainingNominationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Block duplicate nominations of the same employee to the same schedule while an earlier
        // nomination is still live (rejected/withdrawn ones don't count, so re-nomination stays possible).
        // Scoped to the tenant: unscoped, another tenant's row would block a legitimate nomination.
        var alreadyNominated = await _nominationRepository.GetQueryable()
            .AnyAsync(n => n.TenantId == current
                && n.ScheduleId == createDto.ScheduleId
                && n.EmployeeId == createDto.EmployeeId
                && n.Status != NominationStatus.Rejected
                && n.Status != NominationStatus.Withdrawn,
                cancellationToken);

        if (alreadyNominated)
            throw new InvalidOperationException("This employee already has an active nomination for the selected schedule.");

        var entity = createDto.ToEntity(current, createdByUserId);
        entity.NominationNumber = await GenerateNominationNumberAsync(cancellationToken);
        entity.NominationDate = DateTime.UtcNow;
        entity.Status = createDto.SaveAsDraft ? NominationStatus.Draft : NominationStatus.Submitted;

        await _nominationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination created: {NominationNumber}", entity.NominationNumber);

        // A freshly written entity has no Schedule/Employee loaded, so mapping it directly returns a
        // blank programme and nominee on the create response. Re-read through the includes chain.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<BulkNominationResultDto> BulkCreateAsync(BulkCreateTrainingNominationDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var result = new BulkNominationResultDto { RequestedCount = dto.EmployeeIds.Count };

        var schedule = await _scheduleRepository.GetByIdAsync(dto.ScheduleId);
        if (schedule == null || schedule.TenantId != current)
            throw new ArgumentException($"Training schedule with ID '{dto.ScheduleId}' not found.");

        // Existing live nominations for this schedule (for both dup checks and capacity).
        var existing = await _nominationRepository.GetQueryable()
            .Where(n => n.TenantId == current
                && n.ScheduleId == dto.ScheduleId
                && n.Status != NominationStatus.Rejected
                && n.Status != NominationStatus.Withdrawn)
            .Select(n => new { n.EmployeeId, n.Status })
            .ToListAsync(cancellationToken);

        var alreadyNominated = existing.Select(e => e.EmployeeId).ToHashSet();
        var status = dto.SaveAsDraft ? NominationStatus.Draft : NominationStatus.Submitted;

        foreach (var employeeId in dto.EmployeeIds.Distinct())
        {
            if (alreadyNominated.Contains(employeeId))
            {
                result.Skipped.Add(new BulkNominationSkipDto { EmployeeId = employeeId, Reason = "Already has an active nomination for this schedule." });
                continue;
            }

            var entity = new CreateTrainingNominationDto
            {
                ScheduleId = dto.ScheduleId,
                EmployeeId = employeeId,
                Type = dto.Type,
                Justification = dto.Justification,
                SaveAsDraft = dto.SaveAsDraft
            }.ToEntity(current, createdByUserId);

            entity.NominationNumber = await GenerateNominationNumberAsync(cancellationToken);
            entity.NominationDate = DateTime.UtcNow;
            entity.Status = status;

            await _nominationRepository.AddAsync(entity);
            alreadyNominated.Add(employeeId);
            result.CreatedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk nomination for schedule {ScheduleId}: {Created} created, {Skipped} skipped",
            dto.ScheduleId, result.CreatedCount, result.Skipped.Count);

        return result;
    }

    public async Task<TrainingNominationDto> UpdateAsync(Guid id, UpdateTrainingNominationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(id);

        if (entity.Status == NominationStatus.Approved ||
            entity.Status == NominationStatus.Confirmed ||
            entity.Status == NominationStatus.Rejected ||
            entity.Status == NominationStatus.Withdrawn)
            throw new InvalidOperationException($"Nomination cannot be edited because it is already {entity.Status}.");

        entity.Type = dto.Type;
        entity.Justification = dto.Justification;
        entity.TrainingNeedsAssessmentId = dto.TrainingNeedsAssessmentId;
        entity.ActualCost = dto.ActualCost;
        entity.EmployeeContributed = dto.EmployeeContributed;
        entity.EmployeeContribution = dto.EmployeeContributed ? dto.EmployeeContribution : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination updated: {NominationNumber}", entity.NominationNumber);

        // A changed ScheduleId does not refresh the loaded Schedule navigation — re-read.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingNominationDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(id);

        if (entity.Status != NominationStatus.Draft)
            throw new InvalidOperationException($"Only Draft nominations can be submitted. Current status is '{entity.Status}'.");

        // Configurable workflow: if the tenant has an active approval workflow for TrainingNomination,
        // start it and let it drive the status. Otherwise fall back to the legacy Supervisor→HR chain.
        var workflowResult = await _workflowIntegration.SubmitAsync(WorkflowEntityType, id);
        if (workflowResult.ExecutionResult.Success)
        {
            var adapter = _adapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplySubmitOutcome(entity, workflowResult, _currentUser.EmployeeId);
            entity.UpdatedAt = DateTime.UtcNow;

            await _nominationRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // A single-step / auto-approving workflow can complete on submission.
            if (entity.Status == NominationStatus.Approved && _currentUser.EmployeeId.HasValue)
                await _bondService.EnsureBondForNominationAsync(entity.Id, entity.TenantId, _currentUser.EmployeeId.Value, cancellationToken);

            _logger.LogInformation("Training nomination {NominationNumber} submitted via configurable workflow", entity.NominationNumber);
            return await GetByIdAsync(entity.Id, cancellationToken);
        }

        // Legacy fallback — no active workflow configured for this entity type.
        entity.Status = NominationStatus.Submitted;
        entity.UpdatedAt = DateTime.UtcNow;

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination submitted (legacy chain): {NominationNumber}", entity.NominationNumber);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(id);

        if (entity.Status != NominationStatus.Draft && entity.Status != NominationStatus.Submitted)
            throw new InvalidOperationException("Only draft or submitted (not yet approved) nominations can be deleted.");

        await _nominationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination deleted: {NominationNumber}", entity.NominationNumber);

        return true;
    }

    // ── Workflow ──────────────────────────────────────────────────────────────

    public async Task<bool> ApproveAsync(ApproveNominationDto dto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(dto.NominationId);

        // ── Configurable-workflow path ────────────────────────────────────────
        if (entity.WorkflowInstanceId.HasValue)
        {
            // The workflow engine identifies approvers by USER id; the nomination's approver
            // fields (HrApprovedById) are EMPLOYEE ids — keep the two distinct.
            var approverUserId = GetCurrentUserId();
            var canApprove = await _workflowIntegration.CanUserApproveAsync(WorkflowEntityType, entity.Id, approverUserId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            // A full schedule cannot take another seat — advise waitlisting rather than approving.
            await EnforceScheduleCapacityAsync(entity, cancellationToken);

            var workflowResult = await _workflowIntegration.ProcessApprovalAsync(WorkflowEntityType, entity.Id, approverUserId, "Approve", dto.Comments);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval.");

            var adapter = _adapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, approvedById);
            entity.HrComments = dto.Comments;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = approvedById.ToString();

            await _nominationRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Training nomination {NominationNumber} approval processed via workflow → {Status}", entity.NominationNumber, entity.Status);

            if (entity.Status == NominationStatus.Approved)
                await _bondService.EnsureBondForNominationAsync(entity.Id, entity.TenantId, approvedById, cancellationToken);

            return true;
        }

        // ── Legacy Supervisor→HR path ─────────────────────────────────────────
        EnsureLegacyDecisionAllowed();

        if (dto.ApproverRole.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            if (entity.Status != NominationStatus.Submitted && entity.Status != NominationStatus.SupervisorReview)
                throw new InvalidOperationException("Nomination is not in a state that allows supervisor approval.");

            entity.SupervisorApprovedById = approvedById;
            entity.SupervisorApprovalDate = DateTime.UtcNow;
            entity.SupervisorComments = dto.Comments;
            entity.Status = NominationStatus.HrReview;
        }
        else if (dto.ApproverRole.Equals("HR", StringComparison.OrdinalIgnoreCase))
        {
            if (entity.Status != NominationStatus.HrReview)
                throw new InvalidOperationException("Nomination must be in HR Review status to be approved by HR.");

            // Capacity enforcement: final HR approval takes a seat, so block it once the schedule is full.
            await EnforceScheduleCapacityAsync(entity, cancellationToken);

            entity.HrApprovedById = approvedById;
            entity.HrApprovalDate = DateTime.UtcNow;
            entity.HrComments = dto.Comments;
            entity.Status = NominationStatus.Approved;
        }
        else
        {
            throw new ArgumentException($"ApproverRole must be 'Supervisor' or 'HR'. Received: '{dto.ApproverRole}'.");
        }

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = approvedById.ToString();

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination {NominationNumber} approved by {Role}", entity.NominationNumber, dto.ApproverRole);

        // On final HR approval, auto-create a pending service bond if the program requires one (idempotent).
        if (entity.Status == NominationStatus.Approved)
            await _bondService.EnsureBondForNominationAsync(entity.Id, entity.TenantId, approvedById, cancellationToken);

        return true;
    }

    /// <summary>Current authenticated user's id (ApplicationUser id) for workflow-engine approver checks.</summary>
    private Guid GetCurrentUserId()
        => Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty;

    /// <summary>
    /// W3 slice 8: the legacy no-workflow approve/reject path previously accepted ANY authenticated
    /// caller — the approver role came from the request body, so any employee could post
    /// <c>{"approverRole":"HR"}</c> and approve a nomination outright. The live path is
    /// workflow-validated per request (<see cref="IWorkflowIntegrationService.CanUserApproveAsync"/>);
    /// the legacy path survives only for nominations created before the workflow wiring, so it is
    /// held to the HR-shaped roles (the MentoringService IsHr shape). A legacy line-supervisor
    /// approval is deliberately not supported: the org holds no reporting lines to validate a
    /// supervisor claim against, and new nominations all take the workflow path.
    /// </summary>
    private void EnsureLegacyDecisionAllowed()
    {
        var allowed = _currentUserProvider.HasRole(Constants.Roles.Hr)
            || _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
            || _currentUserProvider.HasRole(Constants.Roles.TenantAdmin);
        if (!allowed)
            throw new UnauthorizedAccessException(
                "This nomination has no approval workflow; only HR or an administrator may decide it.");
    }

    /// <summary>Blocks approval when the schedule has no remaining seats (confirmed/approved ≥ capacity).</summary>
    private async Task EnforceScheduleCapacityAsync(TrainingNomination entity, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(entity.ScheduleId);
        if (schedule == null || schedule.TenantId != entity.TenantId || schedule.MaxParticipants <= 0) return;

        // Seats must be counted within the nomination's tenant: an unscoped count sums other tenants'
        // nominations and reports the schedule as full when it is not.
        var seatsTaken = await _nominationRepository.GetQueryable()
            .CountAsync(n => n.TenantId == entity.TenantId
                && n.ScheduleId == entity.ScheduleId
                && n.Id != entity.Id
                && (n.Status == NominationStatus.Approved || n.Status == NominationStatus.Confirmed),
                cancellationToken);

        if (seatsTaken >= schedule.MaxParticipants)
            throw new InvalidOperationException(
                $"Cannot approve: this schedule is full ({schedule.MaxParticipants} seats taken). Add the employee to the waitlist instead.");
    }

    public async Task<bool> RejectAsync(RejectNominationDto dto, Guid rejectedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(dto.NominationId);

        if (entity.Status == NominationStatus.Rejected || entity.Status == NominationStatus.Withdrawn)
            throw new InvalidOperationException($"Nomination is already {entity.Status}.");

        // ── Configurable-workflow path ────────────────────────────────────────
        if (entity.WorkflowInstanceId.HasValue)
        {
            var approverUserId = GetCurrentUserId();
            var canApprove = await _workflowIntegration.CanUserApproveAsync(WorkflowEntityType, entity.Id, approverUserId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var reason = !string.IsNullOrWhiteSpace(dto.RejectionReason) ? dto.RejectionReason : "Rejected";
            var workflowResult = await _workflowIntegration.ProcessApprovalAsync(WorkflowEntityType, entity.Id, approverUserId, "Reject", reason);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection.");

            var adapter = _adapterRegistry.GetAdapter(WorkflowEntityType);
            adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, rejectedById, reason);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = rejectedById.ToString();

            await _nominationRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Training nomination {NominationNumber} rejection processed via workflow", entity.NominationNumber);

            await _bondService.CancelForNominationAsync(entity.Id, rejectedById, cancellationToken);
            return true;
        }

        // ── Legacy path ───────────────────────────────────────────────────────
        EnsureLegacyDecisionAllowed();

        entity.Status = NominationStatus.Rejected;
        entity.RejectionReason = dto.RejectionReason;
        entity.RejectedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = rejectedById.ToString();

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination rejected: {NominationNumber}", entity.NominationNumber);

        // A rejected nomination cancels any still-pending service bond.
        await _bondService.CancelForNominationAsync(entity.Id, rejectedById, cancellationToken);

        return true;
    }

    public async Task<bool> WithdrawAsync(WithdrawNominationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNominationAsync(dto.NominationId);

        if (entity.Status == NominationStatus.Withdrawn || entity.Status == NominationStatus.Rejected)
            throw new InvalidOperationException($"Nomination cannot be withdrawn because it is already {entity.Status}.");

        entity.Status = NominationStatus.Withdrawn;
        entity.UpdatedAt = DateTime.UtcNow;

        await _nominationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training nomination withdrawn: {NominationNumber}", entity.NominationNumber);

        // A withdrawn nomination cancels any still-pending service bond.
        await _bondService.CancelForNominationAsync(entity.Id, entity.EmployeeId, cancellationToken);

        return true;
    }

    // ── Attendance sub-operations ─────────────────────────────────────────────

    public async Task<TrainingAttendanceDto> MarkAttendanceAsync(MarkAttendanceDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _attendanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The written entity has no Schedule/Employee/MarkedBy loaded, so mapping it directly returns
        // a row with a blank programme, nominee and marker.
        var saved = await _attendanceRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<IEnumerable<TrainingAttendanceDto>> BulkMarkAttendanceAsync(BulkMarkAttendanceDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var records = new List<TrainingAttendance>();

        foreach (var entry in dto.Entries)
        {
            var markDto = new MarkAttendanceDto
            {
                ScheduleId = dto.ScheduleId,
                EmployeeId = entry.EmployeeId,
                AttendanceDate = dto.AttendanceDate,
                IsPresent = entry.IsPresent,
                CheckInTime = entry.CheckInTime,
                CheckOutTime = entry.CheckOutTime,
                AbsenceReason = entry.AbsenceReason
            };

            var entity = markDto.ToEntity(current, createdByUserId);
            records.Add(entity);
        }

        foreach (var record in records)
            await _attendanceRepository.AddAsync(record);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk attendance marked for schedule {ScheduleId} on {Date}: {Count} records", dto.ScheduleId, dto.AttendanceDate.Date, records.Count);

        return records.Select(r => r.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingAttendanceDto>> GetAttendanceForScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _attendanceRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingAttendanceDto>> GetAttendanceByDateAsync(Guid scheduleId, DateTime date, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _attendanceRepository.GetByScheduleAndDateAsync(scheduleId, date);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    // ── Feedback sub-operations ───────────────────────────────────────────────

    public async Task<TrainingFeedbackDto> SubmitFeedbackAsync(SubmitTrainingFeedbackDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // One feedback per person per schedule. Without this a trainee could file twice, and
        // because CreditTrainerRatingAsync runs on EVERY submission each one was counted into the
        // trainer's average — so one attendee could move a trainer's score simply by pressing the
        // button again. That is the half of this that matters: a missing dedupe on an opinion is
        // untidy, a repeatable vote is a broken statistic.
        var already = await _feedbackRepository.GetQueryable()
            .AnyAsync(f => f.TenantId == current
                        && f.ScheduleId == dto.ScheduleId
                        && f.EmployeeId == dto.EmployeeId
                        && !f.IsDeleted,
                      cancellationToken);
        if (already)
            throw new InvalidOperationException(
                "You have already given feedback for this course. It cannot be submitted twice.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _feedbackRepository.AddAsync(entity);
        await CreditTrainerRatingAsync(entity, cancellationToken);

        // One SaveChanges so the rating credit is atomic with the feedback it came from — a partial
        // write would leave the trainer's average counting a review that does not exist.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training feedback submitted for schedule {ScheduleId} by employee {EmployeeId}", dto.ScheduleId, dto.EmployeeId);

        var saved = await _feedbackRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    /// <summary>
    /// The feedback this employee has given, newest first.
    /// </summary>
    /// <remarks>
    /// The only read before this was <c>GetFeedbackForScheduleAsync</c>, which is HR's aggregate
    /// over everybody on a course. A trainee could therefore file feedback and never see it again
    /// — and, more to the point, a form had no way to know whether they had already answered.
    /// That is why the portal feedback form was parked from slice 6 until now: the write existed,
    /// the read did not, and a form built on the write alone would invite the double submission
    /// this slice also closed.
    /// </remarks>
    public async Task<IEnumerable<TrainingFeedbackDto>> GetMyFeedbackAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var list = (await _feedbackRepository.GetQueryable()
                .Where(f => f.TenantId == tenantId && f.EmployeeId == employeeId && !f.IsDeleted)
                // ⚠ `Schedule` alone is not enough. ToDto() reads ProgramName through
                // Schedule.Program and EmployeeName through Employee, so a single Include leaves
                // both as the empty string — present but blank, which the probe caught before
                // this reached a screen. A row that names neither the course nor the person is
                // useless to the form that reads it back.
                .Include(f => f.Schedule).ThenInclude(sch => sch.Program)
                .Include(f => f.Employee)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync(cancellationToken))
            .Select(f => f.ToDto())
            .ToList();
        return list;
    }

    public async Task<IEnumerable<TrainingFeedbackDto>> GetFeedbackForScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _feedbackRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    // ── Follow-up assessment sub-operations ───────────────────────────────────

    public async Task<TrainingFollowUpAssessmentDto> SubmitFollowUpAssessmentAsync(SubmitFollowUpAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _followUpRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Follow-up assessment submitted for schedule {ScheduleId} by employee {EmployeeId}", dto.ScheduleId, dto.EmployeeId);

        var saved = await _followUpRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<TrainingFollowUpAssessmentDto> SubmitManagerObservationAsync(SubmitManagerObservationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFollowUpAsync(dto.AssessmentId);

        entity.ManagerId = updatedByUserId;
        entity.ManagerObservationNotes = dto.ManagerObservationNotes;
        entity.ManagerSubmittedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _followUpRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manager observation submitted for follow-up assessment {AssessmentId}", dto.AssessmentId);

        // ManagerId was just set, so the tracked instance's Manager navigation is still null/stale —
        // an untracked re-read is the only way to get ManagerName onto this response.
        var saved = await _followUpRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (saved ?? entity).ToDto();
    }

    public async Task<IEnumerable<TrainingFollowUpAssessmentDto>> GetFollowUpAssessmentsAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _followUpRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<string> GenerateNominationNumberAsync(CancellationToken ct)
        => _numberSequence.GenerateAsync("NOM", ct);

    /// <summary>
    /// Folds a feedback form's trainer rating into the delivering trainer's running average.
    /// <c>TrainerProfile.AverageRating</c> and <c>TotalRatingsCount</c> are read by the trainer list
    /// and the dashboard leaderboard but nothing has ever written them.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="TrainingFeedback.TrainerKnowledgeRating"/> — the only rating on the form that
    /// is about the trainer. Overall satisfaction covers venue, materials and content too, so folding
    /// it into a *trainer's* score would blame them for a bad room. Ratings are kept as a running mean
    /// (count + average) rather than recomputed, so this stays O(1) as feedback accumulates.
    /// </remarks>
    private async Task CreditTrainerRatingAsync(TrainingFeedback feedback, CancellationToken ct)
    {
        if (!feedback.TrainerKnowledgeRating.HasValue)
            return;

        var schedule = await _scheduleRepository.GetByIdAsync(feedback.ScheduleId);
        if (schedule?.TrainerProfileId == null)
            return;

        var trainer = await _trainerProfileRepository.GetByIdAsync(schedule.TrainerProfileId.Value);
        if (trainer == null || trainer.TenantId != feedback.TenantId)
            return;

        var previousTotal = (trainer.AverageRating ?? 0m) * trainer.TotalRatingsCount;
        trainer.TotalRatingsCount += 1;
        trainer.AverageRating = Math.Round(
            (previousTotal + feedback.TrainerKnowledgeRating.Value) / trainer.TotalRatingsCount, 2);
        trainer.UpdatedAt = DateTime.UtcNow;

        await _trainerProfileRepository.UpdateAsync(trainer);
    }

}
