using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Performance Improvement Plan

public class PerformanceImprovementPlanService : IPerformanceImprovementPlanService
{
    private readonly IGenericRepository<PerformanceImprovementPlan> _improvementPlanRepository;
    private readonly IGenericRepository<PipReviewMeeting> _reviewMeetingRepository;
    private readonly IGenericRepository<PipGoal> _pipGoalRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IAppraisalNotificationService _notifications;
    private readonly ILogger<PerformanceImprovementPlan> _logger;

    /// <summary>
    /// Entity type registered with the workflow engine. Must match the catalog entry in
    /// <c>WorkflowEntityTypeCatalogService</c> and the aliases on
    /// <c>PerformanceImprovementPlanWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "PerformanceImprovementPlan";

    public PerformanceImprovementPlanService(
        IGenericRepository<PerformanceImprovementPlan> improvementPlanRepository,
        IGenericRepository<PipReviewMeeting> reviewMeetingRepository,
        IGenericRepository<PipGoal> pipGoalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<Employee> employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IAppraisalNotificationService notifications,
        ILogger<PerformanceImprovementPlan> logger)
    {
        _improvementPlanRepository = improvementPlanRepository;
        _reviewMeetingRepository = reviewMeetingRepository;
        _pipGoalRepository = pipGoalRepository;
        _attachmentRepository = attachmentRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>
    /// Raises in-app notifications without ever failing the action that produced them. The work is
    /// already saved by the time we notify, so a bad recipient must not surface as a 500 on a PIP
    /// that was in fact created. Matches the best-effort pattern in <c>PerformanceAppraisalService</c>.
    /// </summary>
    private async Task NotifyQuietlyAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.RaiseAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise PIP notification(s); the originating action stands.");
        }
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

    // A PIP owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PerformanceImprovementPlan> GetOwnedPipAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Performance Improvement Plan with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<PerformanceImprovementPlan> TenantPipQuery()
    {
        var tenantId = GetTenantId();
        return _improvementPlanRepository.GetQueryable().Where(p => p.TenantId == tenantId);
    }

    /// <summary>
    /// Every read has to carry these four: the DTO shows the employee, the supervisor, the HR
    /// co-owner and the appraisal that triggered the plan, and a missing include renders as a
    /// blank name rather than an error.
    /// </summary>
    private IQueryable<PerformanceImprovementPlan> TenantPipQueryWithNames()
        => TenantPipQuery()
            .Include(p => p.Employee)
            .Include(p => p.Supervisor)
            .Include(p => p.HROwner)
            .Include(p => p.Appraisal);

    /// <summary>
    /// Re-reads a plan with its navigations so a write response carries real names. Writing
    /// straight back from the tracked entity returns blanks — it was never loaded with includes.
    /// </summary>
    private async Task<PerformanceImprovementPlanDto> ReloadDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await TenantPipQueryWithNames().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Performance Improvement Plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    /// <summary>
    /// Allocates the next PIP reference for the tenant and year. Nothing else assigns one on this
    /// path, so before this a plan created from the UI carried an empty reference while one raised
    /// by <c>PipRecommendationHandler</c> did not — the same record type with and without an
    /// identity. The loop covers the race between two concurrent creates; the column is indexed
    /// but not unique, so the check is ours to make.
    /// </summary>
    private async Task<string> NextPipNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PIP-{year}-";

        var used = await _improvementPlanRepository
            .GetQueryable(p => p.TenantId == tenantId && p.PipNumber.StartsWith(prefix))
            .Select(p => p.PipNumber)
            .ToListAsync(cancellationToken);

        var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
        for (var next = used.Count + 1; next <= used.Count + 1000; next++)
        {
            var candidate = $"{prefix}{next:D4}";
            if (!taken.Contains(candidate))
                return candidate;
        }

        // Unreachable in practice; a distinct fallback beats handing back a duplicate.
        return $"{prefix}{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
    }

    /// <summary>The states in which a plan is live against the employee.</summary>
    private static bool IsLive(PipStatus status)
        => status is PipStatus.Active or PipStatus.InProgress;

    public async Task<PipReviewMeetingDto> AddReviewMeetingAsync(Guid pipId, CreatePipReviewMeetingDto createDto, CancellationToken cancellationToken = default)
    {
        var pip = await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var conductorExists = await _employeeRepository.ExistsAsync(e => e.TenantId == tenantId && e.Id == createDto.ConductedById);
        if (!conductorExists)
            throw new ArgumentException("Meeting conductor not found");

        if (pip.Status is PipStatus.Draft or PipStatus.PendingApproval)
            throw new InvalidOperationException(
                "Review meetings can only be held once the plan is in force.");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.PipId = pipId;

        await _reviewMeetingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _reviewMeetingRepository.GetQueryable()
                                            .Include(m => m.ConductedBy)
                                            .FirstOrDefaultAsync(m => m.Id == entity.Id && m.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Review meeting added successfully: {Id}", entity!.Id);

        // Only a meeting still ahead of us is news; logging one that has already been held is a
        // record, not an invitation.
        if (entity.MeetingDate > DateTime.UtcNow)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    pip.EmployeeId,
                    AppraisalNotificationType.PipMeetingScheduled,
                    "Improvement plan review scheduled",
                    $"A review of {pip.PipNumber} is set for {entity.MeetingDate:d MMM yyyy}.",
                    NavigationUrl: $"/hr/performance/pip/{pipId}",
                    AppraisalId: pip.AppraisalId)
            }, cancellationToken);
        }

        return entity!.ToDto();
    }

    /// <summary>
    /// Closes a plan with its outcome — or, for <see cref="PipOutcome.Extended"/>, pushes the end
    /// date out and leaves it running. Extending is not a completion: the old code set every
    /// outcome other than "improved" to Unsuccessful, so extending a plan marked the employee as
    /// having failed it and closed the record they were still working through.
    /// </summary>
    public async Task<bool> CompletePipAsync(CompletePipDto completeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(completeDto.PipId, cancellationToken);

        if (!IsLive(entity.Status))
            throw new InvalidOperationException(
                entity.Status is PipStatus.Draft or PipStatus.PendingApproval
                    ? "An outcome can only be recorded once the plan is in force. Get it approved first."
                    : $"This plan is already closed ({entity.Status}).");

        if (completeDto.Outcome == PipOutcome.Extended)
        {
            if (completeDto.NewEndDate is not DateTime newEnd)
                throw new InvalidOperationException("Extending a plan needs a new end date.");
            if (newEnd.Date <= entity.EndDate.Date)
                throw new InvalidOperationException("The new end date has to be later than the current one.");

            entity.EndDate = newEnd;
            entity.Outcome = PipOutcome.Extended;
            entity.OutcomeNotes = completeDto.OutcomeNotes;
            // Status, CompletionDate untouched on purpose — the plan is still running.
        }
        else
        {
            entity.Status = completeDto.Outcome == PipOutcome.PerformanceImproved ? PipStatus.Completed : PipStatus.Unsuccessful;
            entity.CompletionDate = DateTime.UtcNow;
            entity.Outcome = completeDto.Outcome;
            entity.OutcomeNotes = completeDto.OutcomeNotes;
        }

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan {Id} outcome recorded: {Outcome}", completeDto.PipId, completeDto.Outcome);

        var subject = await ReloadDtoAsync(entity.Id, cancellationToken);
        var recipients = new List<Guid> { entity.EmployeeId, entity.SupervisorId };
        if (entity.HROwnerId is Guid hrOwner) recipients.Add(hrOwner);

        await NotifyQuietlyAsync(recipients.Distinct().Select(r => new AppraisalNotificationRequest(
            r,
            AppraisalNotificationType.PipOutcomeRecorded,
            completeDto.Outcome == PipOutcome.Extended ? "Improvement plan extended" : "Improvement plan closed",
            completeDto.Outcome == PipOutcome.Extended
                ? $"{subject.PipNumber} now runs to {entity.EndDate:d MMM yyyy}."
                : $"{subject.PipNumber} was closed with outcome: {completeDto.Outcome}.",
            NavigationUrl: $"/hr/performance/pip/{entity.Id}",
            AppraisalId: entity.AppraisalId,
            SubjectEmployeeName: subject.EmployeeName,
            Urgency: completeDto.Outcome is PipOutcome.Termination or PipOutcome.Demotion
                ? NotificationUrgency.Urgent
                : NotificationUrgency.Normal)), cancellationToken);

        return true;
    }

    /// <summary>
    /// Creates a plan in <see cref="PipStatus.Draft"/>. A PIP is an employment record served on a
    /// named employee, so it goes out for approval before it is in force — see
    /// <c>PerformanceImprovementPlanWorkflowStatusAdapter</c>.
    /// </summary>
    public async Task<PerformanceImprovementPlanDto> CreateAsync(CreatePerformanceImprovementPlanDto createDto, CancellationToken cancellationToken = default)
    {
        // ── Data integrity: only one live or in-flight PIP per employee at a time ──────────
        var tenantId = GetTenantId();
        var blocking = await _improvementPlanRepository
            .GetQueryable(p => p.TenantId == tenantId && p.EmployeeId == createDto.EmployeeId)
            .Where(p => p.Status == PipStatus.Active
                     || p.Status == PipStatus.InProgress
                     || p.Status == PipStatus.PendingApproval)
            .Select(p => new { p.PipNumber, p.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (blocking != null)
            throw new InvalidOperationException(
                blocking.Status == PipStatus.PendingApproval
                    ? $"Improvement plan {blocking.PipNumber} for this employee is already out for approval."
                    : $"This employee is already on improvement plan {blocking.PipNumber}. " +
                      "Complete or cancel it before creating another.");

        if (createDto.EndDate.Date <= createDto.StartDate.Date)
            throw new InvalidOperationException("The plan's end date has to be after its start date.");

        var supervisorExists = await _employeeRepository.ExistsAsync(
            e => e.TenantId == tenantId && e.Id == createDto.SupervisorId);
        if (!supervisorExists)
            throw new ArgumentException("Supervisor not found.");

        var performanceImprovementPlan = createDto.ToEntity();
        performanceImprovementPlan.TenantId = tenantId;
        performanceImprovementPlan.Status = PipStatus.Draft;
        performanceImprovementPlan.PipNumber = await NextPipNumberAsync(tenantId, cancellationToken);

        await _improvementPlanRepository.AddAsync(performanceImprovementPlan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan created: {PipNumber} ({pipId})",
            performanceImprovementPlan.PipNumber, performanceImprovementPlan.Id);

        return await ReloadDtoAsync(performanceImprovementPlan.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(id, cancellationToken);

        // A plan that has been in force is part of the employment record. Cancelling it says it
        // was stopped; deleting it says it never happened.
        if (entity.Status != PipStatus.Draft)
            throw new InvalidOperationException(
                entity.Status == PipStatus.PendingApproval
                    ? "Recall the plan from approval before deleting it."
                    : $"A {entity.Status} plan is part of the employment record and cannot be deleted. Cancel it instead.");

        await _improvementPlanRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan deleted: {id}", id);

        return true;
    }

    public async Task<bool> DeleteReviewMeetingAsync(Guid pipId, Guid meetingId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _reviewMeetingRepository.FirstOrDefaultAsync(m => m.Id == meetingId && m.PipId == pipId && m.TenantId == tenantId);

        if (entity == null)
            throw new ArgumentException("Review meeting not found");

        await _reviewMeetingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review meeting deleted successfully");

        return true;
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetActivePipsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await TenantPipQueryWithNames()
                                                    .Where(p => p.Status == PipStatus.Active || p.Status == PipStatus.InProgress)
                                                    .OrderByDescending(p => p.StartDate)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await TenantPipQueryWithNames()
                                                    .OrderByDescending(p => p.StartDate)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantPipQueryWithNames()
                                    .Where(p => p.EmployeeId == employeeId)
                                    .OrderByDescending(p => p.StartDate)
                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    /// <summary>
    /// Plans a manager owns — as the named supervisor or the named HR owner. Their worklist,
    /// without giving them the org-wide list.
    /// </summary>
    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetBySupervisorAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantPipQueryWithNames()
                                    .Where(p => p.SupervisorId == employeeId || p.HROwnerId == employeeId)
                                    .OrderByDescending(p => p.StartDate)
                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PerformanceImprovementPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await TenantPipQueryWithNames()
                                                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetByStatusAsync(PipStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await TenantPipQueryWithNames()
                                                    .Where(p => p.Status == status)
                                                    .OrderByDescending(p => p.StartDate)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PipReviewMeetingDto> GetLatestReviewMeetingAsync(Guid pipId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _reviewMeetingRepository.GetQueryable(m => m.PipId == pipId && m.TenantId == tenantId)
                                                .Include(m => m.ConductedBy)
                                                .OrderByDescending(m => m.MeetingDate)
                                                .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            throw new ArgumentException("No review meetings found for this Performance Improvement Plan");

        return entity.ToDto();
    }

    public async Task<PagedResult<PerformanceImprovementPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = TenantPipQueryWithNames();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(p => p.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        var improvementPlans = items.ToDtoList();

        return new PagedResult<PerformanceImprovementPlanDto>
        {
            Items = improvementPlans,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<PipReviewMeetingDto>> GetReviewMeetingsAsync(Guid pipId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entities = await _reviewMeetingRepository.GetQueryable(m => m.PipId == pipId && m.TenantId == tenantId)
                                                    .Include(m => m.ConductedBy)
                                                    .OrderByDescending(m => m.MeetingDate)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PipReviewMeetingDto?> GetReviewMeetingByIdAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _reviewMeetingRepository.GetQueryable(m => m.Id == meetingId && m.TenantId == tenantId)
                                                   .Include(m => m.ConductedBy)
                                                   .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDto();
    }

    public async Task<PerformanceImprovementPlanDto> UpdateAsync(UpdatePerformanceImprovementPlanDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(updateDto.Id, cancellationToken);

        if (entity.Status == PipStatus.PendingApproval)
            throw new InvalidOperationException(
                "This plan is out for approval. Recall it before making changes.");
        if (entity.Status is PipStatus.Completed or PipStatus.Unsuccessful or PipStatus.Cancelled)
            throw new InvalidOperationException($"A {entity.Status} plan can no longer be edited.");

        if (updateDto.EndDate.Date <= updateDto.StartDate.Date)
            throw new InvalidOperationException("The plan's end date has to be after its start date.");

        // The status is the workflow's and the outcome path's to set. Letting the edit form carry
        // them would hand any editor a way round both the approval and the closure rules.
        var status = entity.Status;
        var outcome = entity.Outcome;
        var outcomeNotes = entity.OutcomeNotes;
        var completionDate = entity.CompletionDate;

        updateDto.UpdateEntity(entity);

        entity.Status = status;
        entity.Outcome = outcome;
        entity.OutcomeNotes = outcomeNotes;
        entity.CompletionDate = completionDate;

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan updated: {Id}", updateDto.Id);

        return await ReloadDtoAsync(entity.Id, cancellationToken);
    }

    // ── Approval workflow ──────────────────────────────────────────────────
    // Draft → PendingApproval → Active runs on the generic workflow engine; this service never
    // sets those three itself. PerformanceImprovementPlanWorkflowStatusAdapter maps the engine's
    // outcome onto the entity.
    //
    // ⚠ This used to say "Inoperable until a PerformanceImprovementPlan workflow definition has
    // been published". It was NOT inoperable — it auto-approved (corrected 2026-09-15). With no
    // definition, WorkflowIntegrationService.SubmitAsync returns WorkflowOutcome.Approved and the
    // adapter maps it to Active, so submitting a plan put it straight into force against the
    // employee, unreviewed — and AfterApprovalStepAsync then notified the employee, the supervisor
    // and the HR owner that it was binding. All four methods below now take a no-workflow branch;
    // see HrWorkflowFallbackAuthority for the mechanism and for why submit could not be fixed on
    // its own.
    //
    // The second half of the old sentence still holds and is the point of the engine: when a
    // definition IS published, the authority to approve comes from it and not from a role.

    public async Task<PerformanceImprovementPlanDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(id, cancellationToken);

        if (entity.Status == PipStatus.PendingApproval)
            throw new InvalidOperationException("This plan is already awaiting approval.");
        if (entity.Status != PipStatus.Draft)
            throw new InvalidOperationException($"A {entity.Status} plan cannot be submitted for approval.");

        // An approver is being asked to sign off what will be served on the employee. A plan with
        // no measurable goals is not something anyone can weigh.
        var goalCount = await _pipGoalRepository.CountAsync(g => g.PipId == id && g.TenantId == entity.TenantId);
        if (goalCount == 0)
            throw new InvalidOperationException(
                "Add at least one improvement goal before submitting the plan for approval.");

        // The one-plan-per-employee rule has to hold here as well as at create. Two people can
        // each write a draft for the same employee — that is reasonable — but only one of them
        // can go into force, and a draft written weeks ago must not sail past a plan that has
        // become live since.
        var competing = await _improvementPlanRepository
            .GetQueryable(p => p.TenantId == entity.TenantId
                            && p.EmployeeId == entity.EmployeeId
                            && p.Id != entity.Id)
            .Where(p => p.Status == PipStatus.Active
                     || p.Status == PipStatus.InProgress
                     || p.Status == PipStatus.PendingApproval)
            .Select(p => new { p.PipNumber, p.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (competing != null)
            throw new InvalidOperationException(
                competing.Status == PipStatus.PendingApproval
                    ? $"Improvement plan {competing.PipNumber} for this employee is already out for approval."
                    : $"This employee is already on improvement plan {competing.PipNumber}. " +
                      "Complete or cancel it before putting another into force.");

        var hasWorkflow = await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType);

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the improvement plan approval workflow.");

        var submitOutcome = hasWorkflow ? workflowResult.Outcome : WorkflowOutcome.Pending;

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan {Id} submitted for approval", id);
        return await AfterApprovalStepAsync(entity, cancellationToken);
    }

    public async Task<PerformanceImprovementPlanDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(id, cancellationToken);
        var userId = RequireUserId();

        WorkflowOutcome approvalOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Approve");
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the approval.");

            approvalOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "approve an improvement plan", HrPermissions.ApprovePerformance);
            approvalOutcome = WorkflowOutcome.Approved;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan {Id} approval step processed", id);
        return await AfterApprovalStepAsync(entity, cancellationToken);
    }

    public async Task<PerformanceImprovementPlanDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(id, cancellationToken);
        var userId = RequireUserId();

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();

        WorkflowOutcome rejectionOutcome;
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            if (!await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId))
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(EntityType, id, userId, "Reject", rejectionText);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process the rejection.");

            rejectionOutcome = workflowResult.Outcome;
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserProvider, "reject an improvement plan", HrPermissions.ApprovePerformance);
            rejectionOutcome = WorkflowOutcome.Rejected;
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan {Id} rejected", id);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    public async Task<PerformanceImprovementPlanDto> RecallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(id, cancellationToken);
        var userId = RequireUserId();

        if (entity.Status != PipStatus.PendingApproval)
            throw new InvalidOperationException("Only a plan still awaiting approval can be recalled.");

        // Skipped when nothing is published: RecallWorkflowAsync answers "No active workflow found"
        // without an instance, so the recall would fail on exactly the tenants where submitting now
        // leaves a plan at PendingApproval.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var workflowResult = await _workflowIntegrationService.RecallAsync(EntityType, id, userId);
            if (!workflowResult.ExecutionResult.Success)
                throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to recall the plan.");
        }

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan {Id} recalled", id);
        return await ReloadDtoAsync(id, cancellationToken);
    }

    /// <summary>
    /// Reloads after a workflow step and, when that step put the plan in force, tells the people
    /// it now binds. A single-step definition approves on submission, so this is checked after
    /// both submit and approve rather than assumed from which endpoint was called.
    /// </summary>
    private async Task<PerformanceImprovementPlanDto> AfterApprovalStepAsync(
        PerformanceImprovementPlan entity, CancellationToken cancellationToken)
    {
        var dto = await ReloadDtoAsync(entity.Id, cancellationToken);
        if (entity.Status != PipStatus.Active)
            return dto;

        var recipients = new List<Guid> { entity.EmployeeId, entity.SupervisorId };
        if (entity.HROwnerId is Guid hrOwner) recipients.Add(hrOwner);

        await NotifyQuietlyAsync(recipients.Distinct().Select(r => new AppraisalNotificationRequest(
            r,
            AppraisalNotificationType.PipOpened,
            "Improvement plan in force",
            $"{dto.PipNumber} runs from {entity.StartDate:d MMM yyyy} to {entity.EndDate:d MMM yyyy}.",
            NavigationUrl: $"/hr/performance/pip/{entity.Id}",
            AppraisalId: entity.AppraisalId,
            SubjectEmployeeName: dto.EmployeeName,
            Urgency: NotificationUrgency.Urgent)), cancellationToken);

        return dto;
    }

    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new InvalidOperationException("No signed-in user could be resolved for this workflow action.");
        return userId;
    }

    public async Task<PipReviewMeetingDto> UpdateReviewMeetingAsync(Guid pipId, UpdatePipReviewMeetingDto updateDto, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _reviewMeetingRepository.GetQueryable()
            .Include(m => m.ConductedBy)
            .FirstOrDefaultAsync(m => m.Id == updateDto.Id && m.PipId == pipId && m.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Review meeting not found");

        updateDto.UpdateEntity(entity);
        // The mapper copies PipId from the body; the meeting belongs to the plan it was found
        // under, and an edit is not a way to move it to a different one.
        entity.PipId = pipId;
        await _reviewMeetingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review meeting updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    /// <summary>
    /// Moves a plan that is already in force between its running states. Draft, PendingApproval
    /// and Active belong to the workflow engine, and Completed/Unsuccessful belong to the outcome
    /// path — routing them through here would be a way round both.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(UpdatePipStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPipAsync(statusDto.PipId, cancellationToken);

        if (statusDto.Status is PipStatus.Draft or PipStatus.PendingApproval or PipStatus.Active)
            throw new InvalidOperationException(
                $"{statusDto.Status} is set by the approval workflow, not directly.");
        if (statusDto.Status is PipStatus.Completed or PipStatus.Unsuccessful)
            throw new InvalidOperationException(
                "Close the plan by recording its outcome, which captures why it ended.");
        if (!IsLive(entity.Status))
            throw new InvalidOperationException(
                $"A {entity.Status} plan cannot be moved to {statusDto.Status}.");

        entity.Status = statusDto.Status;

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan status updated: {Id}", statusDto.PipId);

        return true;
    }

    // ─── PIP Goals ──────────────────────────────────────────────────────────────

    public async Task<PipGoalDto> AddPipGoalAsync(
        Guid pipId, CreatePipGoalDto dto, CancellationToken cancellationToken = default)
    {
        var pip = await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.TenantId = tenantId;
        entity.PipId = pipId;
        entity.Status = GoalProgressStatus.NotStarted;

        await _pipGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal added to PIP {PipId}: {GoalId}", pipId, entity.Id);
        return (await _pipGoalRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<PipGoalDto>> GetPipGoalsAsync(
        Guid pipId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entities = await _pipGoalRepository
            .GetQueryable(g => g.PipId == pipId && g.TenantId == tenantId)
            .OrderBy(g => g.DueDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PipGoalDto> UpdatePipGoalAsync(
        Guid pipId, UpdatePipGoalDto dto, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == dto.Id && g.PipId == pipId && g.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        dto.UpdateEntity(entity);
        await _pipGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal updated: {GoalId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeletePipGoalAsync(
        Guid pipId, Guid goalId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == goalId && g.PipId == pipId && g.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        await _pipGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PipGoalDto> UpdatePipGoalProgressAsync(
        Guid pipId, Guid goalId, decimal progressPercent, string? notes,
        GoalProgressStatus status, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == goalId && g.PipId == pipId && g.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        var clamped = Math.Clamp((int)progressPercent, 0, 100);
        entity.ProgressPercent = clamped;

        if (!string.IsNullOrWhiteSpace(notes))
            entity.ProgressNotes = notes;

        entity.Status = status;

        await _pipGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal {GoalId} progress updated to {Progress}%", goalId, clamped);
        return entity.ToDto();
    }

    public async Task<PipGoalDto?> GetGoalByIdAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _pipGoalRepository.GetByIdAsync(goalId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    // ─── PIP Attachments ────────────────────────────────────────────────────────

    public async Task<IEnumerable<PipAttachmentDto>> GetPipAttachmentsAsync(
        Guid pipId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entities = await _attachmentRepository
            .GetQueryable(a => a.PipId == pipId && a.TenantId == tenantId && a.EntityType == AppraisalAttachmentEntityType.PipPlan)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToPipAttachmentDtoList();
    }

    public async Task<PipAttachmentDto?> GetAttachmentByIdAsync(
        Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository
            .GetQueryable(a => a.Id == attachmentId && a.TenantId == tenantId)
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToPipAttachmentDto();
    }

    public async Task<PipAttachmentDto> CreatePipAttachmentAsync(
        Guid pipId, Guid uploadedById, string fileName, string filePath,
        string? publicUrl, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null)
    {
        await GetOwnedPipAsync(pipId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = new AppraisalAttachment
        {
            TenantId = tenantId,
            PipId = pipId,
            EntityType = AppraisalAttachmentEntityType.PipPlan,
            FileName = fileName,
            FilePath = filePath,
            FileSizeBytes = fileSizeBytes,
            Description = description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _attachmentRepository
            .GetQueryable(a => a.Id == entity.Id)
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogInformation("PIP attachment created: {AttachmentId} for PIP {PipId}", entity!.Id, pipId);

        var dto = entity!.ToPipAttachmentDto();
        dto.PublicUrl = publicUrl;
        return dto;
    }

    public async Task<bool> DeletePipAttachmentAsync(
        Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository
            .GetQueryable(a => a.Id == attachmentId && a.TenantId == tenantId && a.EntityType == AppraisalAttachmentEntityType.PipPlan)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            throw new ArgumentException($"PIP attachment with ID '{attachmentId}' not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP attachment deleted: {AttachmentId}", attachmentId);
        return true;
    }
}

#endregion Performance Improvement Plan

