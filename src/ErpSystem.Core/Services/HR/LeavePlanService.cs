using ErpSystem.Application.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class LeavePlanService : ILeavePlanService
{
    private const string EntityType = "LeavePlan";

    private readonly ILeavePlanRepository _leavePlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeavePlanService> _logger;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILeaveRepository _leaveRequestRepository;

    public LeavePlanService(
        ILeavePlanRepository leavePlanRepository,
        IUnitOfWork unitOfWork,
        ILeaveYearContext leaveYear,
        ILogger<LeavePlanService> logger,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        ICurrentUserService currentUserService,
        ILeaveRepository leaveRequestRepository)
    {
        _leavePlanRepository = leavePlanRepository;
        _unitOfWork = unitOfWork;
        _leaveYear = leaveYear;
        _logger = logger;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _currentUserService = currentUserService;
        _leaveRequestRepository = leaveRequestRepository;
    }

    /// <summary>
    /// The employee the token belongs to, for <c>PlannedBy</c> — an Employee foreign key. Not the
    /// user id: a user id is never an employee id, and the constraint refuses it.
    /// </summary>
    /// <remarks>
    /// Finish-plan lane 4 (2026-09-01). Both the desk and the self-service planner sent the LOGIN's
    /// user id as <c>plannedBy</c>, so no plan raised from either screen had ever satisfied the
    /// constraint. Stamped here, never read from the payload.
    /// </remarks>
    private Guid RequireActingEmployeeId()
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
            return me;
        throw new InvalidOperationException(
            "Planning leave requires your user account to be linked to an employee record. Please contact your administrator.");
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // A leave plan owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<LeavePlan> GetOwnedLeavePlanAsync(Guid id)
    {
        var entity = await _leavePlanRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave plan '{id}' not found.");
        return entity;
    }

    private async Task<bool> HasConflictingPlanAsync(
        Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null)
    {
        var tenantId = GetTenantId();
        var query = _leavePlanRepository.GetQueryable().Where(p =>
            p.TenantId == tenantId &&
            p.EmployeeId == employeeId &&
            p.Status != LeavePlanStatus.Cancelled &&
            p.Status != LeavePlanStatus.Rejected &&
            p.StartDate <= endDate &&
            p.EndDate >= startDate);

        if (excludePlanId.HasValue)
            query = query.Where(p => p.Id != excludePlanId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<LeavePlanDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();
        var items = await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Where(p => p.TenantId == tenantId && p.EmployeeId == employeeId && p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        var dtos = items.ToDtoList();
        await EnrichRelieverClashesAsync(dtos);
        await EnrichRaisedRequestsAsync(dtos);
        return dtos;
    }

    public async Task<IEnumerable<LeavePlanDto>> GetByYearAsync(int year)
    {
        var tenantId = GetTenantId();
        var items = await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Include(p => p.OrganizationUnit)
            .Where(p => p.TenantId == tenantId && p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        var dtos = items.ToDtoList();
        await EnrichRelieverClashesAsync(dtos);
        await EnrichRaisedRequestsAsync(dtos);
        return dtos;
    }

    public async Task<LeavePlanDto> GetByIdAsync(Guid id)
    {
        var entity = await GetWithIncludes(id);
        if (entity == null)
            throw new ArgumentException($"Leave plan '{id}' not found.");
        var dto = entity.ToDto();
        await EnrichRelieverClashesAsync(new List<LeavePlanDto> { dto });
        await EnrichRaisedRequestsAsync(new List<LeavePlanDto> { dto });
        dto.RelieverRoster = await LoadRelieverRosterAsync(entity.EmployeeId);
        return dto;
    }

    public async Task<LeavePlanDto> CreateLeavePlanAsync(CreateLeavePlanDto dto)
    {
        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(dto.EmployeeId, dto.StartDate, dto.EndDate);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        await RequireAnnualTypeAsync(dto.LeaveTypeId);
        await ValidateRelieversAsync(dto.EmployeeId, dto.RelieverId, dto.SecondRelieverId);

        var entity = dto.ToEntity();
        entity.TenantId = GetTenantId();
        entity.PlannedBy = RequireActingEmployeeId();
        // ⚠ The mapper leaves Year at 0 on purpose — it cannot read the tenant's leave year. Set
        // here and on the update path, so both agree (entitlement plan C1).
        entity.Year = LeaveYear.For(dto.StartDate, await _leaveYear.StartMonthAsync());
        await FillRelieversFromRosterAsync(entity);
        await _leavePlanRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Leave plan created for employee {employeeId}", dto.EmployeeId);
        return await GetByIdAsync(entity.Id);
    }

    public async Task<LeavePlanDto> UpdateLeavePlanAsync(Guid id, CreateLeavePlanDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Draft)
            throw new InvalidOperationException("Only draft leave plans can be edited.");

        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, id);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        await RequireAnnualTypeAsync(dto.LeaveTypeId);
        await ValidateRelieversAsync(dto.EmployeeId, dto.RelieverId, dto.SecondRelieverId);

        entity.EmployeeId = dto.EmployeeId;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.LeaveTypeId = dto.LeaveTypeId;
        entity.LeaveSubTypeId = dto.LeaveSubTypeId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.RelieverId = dto.RelieverId;
        entity.SecondRelieverId = dto.SecondRelieverId;
        entity.Notes = dto.Notes;
        // PlannedBy is who raised the plan; an edit does not re-author it.
        // Year follows the dates — moving a plan into January moves its year with it (L-17).
        entity.Year = LeaveYear.For(dto.StartDate, await _leaveYear.StartMonthAsync());

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<LeavePlanDto> SubmitLeavePlanAsync(Guid id)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Draft)
            throw new InvalidOperationException("Only draft leave plans can be submitted.");

        // Submitting must never approve — see HrWorkflowFallbackAuthority. Defence in depth; a
        // LEAVE_PLAN definition is seeded, so this bites only on an unseeded tenant.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(entity, submitOutcome, GetCurrentUserId());

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} submitted for approval", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

        /// <summary>
    /// Refuses an approval by the very employee the record is about.
    /// </summary>
    /// <remarks>
    /// This is the segregation the two-stage leave definition relies on, and it is done HERE rather
    /// than with the engine's <c>PreventInitiatorApproval</c> on purpose. That flag guards the
    /// INITIATOR; a leave record's conflicted party is its SUBJECT, and HR raises leave on other
    /// people's behalf from the desk. On a tenant with one HR user the flag would strand every
    /// desk-raised record at the HR stage with nobody able to clear it — trap 7 of
    /// <c>HR-WORKFLOW-ENGINE-INTEGRATION.md</c>, and the area-9b mistake. Checking the subject
    /// blocks the real conflict and cannot strand somebody else's record.
    ///
    /// It sits before the authority call so it holds on the fallback path too, not just the engine.
    /// </remarks>
    private void RefuseSelfApproval(Guid subjectEmployeeId, string what)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == subjectEmployeeId)
            throw new InvalidOperationException(
                $"You cannot approve your own {what}. It has to be approved by someone else.");
    }

    /// <summary>
    /// Refuses a decision on a plan that is not waiting for one (round 5 lane E4).
    /// </summary>
    /// <remarks>
    /// Approve and reject had no status check of their own and relied on the engine to refuse. Two
    /// ways that failed: a plan sent back with suggested dates is waiting for the EMPLOYEE, and its
    /// approval was cancelled, so the screen offered Approve/Reject that came back as a bare 403;
    /// and on a tenant with no published definition the fallback path would have approved a
    /// cancelled or rejected plan outright. A plan awaiting a decision is Submitted — the two-stage
    /// definition keeps it there until the last stage rules.
    /// </remarks>
    private static void EnsureAwaitingDecision(LeavePlan plan, string verb, string pastTense)
    {
        if (plan.Status == LeavePlanStatus.Submitted) return;

        throw new InvalidOperationException(plan.Status == LeavePlanStatus.ChangesSuggested
            ? $"This plan is waiting for the employee to answer the suggested dates, so there is nothing to {verb} yet."
            : $"Only a submitted leave plan can be {pastTense}. This one is {plan.Status}.");
    }

    public async Task<LeavePlanDto> ApproveLeavePlanAsync(Guid id)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        EnsureAwaitingDecision(entity, "approve", "approved");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        RefuseSelfApproval(entity.EmployeeId, "leave plan");

        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Approve", null, "approve a leave plan", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, approvalOutcome, userId);

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} approved", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> RejectLeavePlanAsync(Guid id, string reason)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        EnsureAwaitingDecision(entity, "reject", "rejected");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        var rejectionText = !string.IsNullOrWhiteSpace(reason) ? reason : "Rejected";

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserService.Roles, EntityType, id, userId,
            "Reject", rejectionText, "reject a leave plan", HrPermissions.ApproveLeave);

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} rejected", id);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> SuggestChangesAsync(Guid id, SuggestLeavePlanChangesDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.Submitted)
            throw new InvalidOperationException("Only submitted leave plans can have changes suggested.");
        if (dto.SuggestedEndDate < dto.SuggestedStartDate)
            throw new InvalidOperationException("Suggested end date must be after or equal to start date.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");

        // Suggesting changes is a third decision verb alongside approve and reject, and it needs
        // the same two paths they have. With no definition published there is no instance, so
        // CanUserApproveAsync answers false for everybody and nothing could be sent back to the
        // employee — and CancelWorkflowAsync would have no instance to cancel either. A plan can
        // now sit at Submitted on such a tenant (that is the point of this programme), so without
        // this branch that plan would be stuck with cancellation as its only exit.
        if (await _workflowIntegrationService.HasActiveApprovalWorkflowAsync(EntityType))
        {
            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, id, userId);
            if (!canApprove)
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step.");

            // Suggesting changes sends the plan back to the employee, so the active approval
            // workflow is cancelled; a fresh one starts when the employee re-submits.
            var cancelResult = await _workflowIntegrationService.CancelWorkflowAsync(
                EntityType, id, "Manager suggested alternative dates");
            if (!cancelResult.Success)
                throw new InvalidOperationException(cancelResult.Message ?? "Failed to update the approval workflow.");
        }
        else
        {
            HrWorkflowFallbackAuthority.EnsureCanRuleWithoutWorkflow(
                _currentUserService.Roles,
                "send a leave plan back with suggested changes",
                HrPermissions.ApproveLeave);
        }

        entity.Status = LeavePlanStatus.ChangesSuggested;
        entity.SuggestedStartDate = dto.SuggestedStartDate;
        entity.SuggestedEndDate = dto.SuggestedEndDate;
        entity.ManagerSuggestionNotes = dto.Notes;
        entity.WorkflowInstanceId = null;
        entity.ApprovedById = null;
        entity.ApprovedDate = null;

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} sent back with suggested changes by {userId}", id, userId);
        return (await GetWithIncludes(id))!.ToDto();
    }

    public async Task<LeavePlanDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        if (entity.Status != LeavePlanStatus.ChangesSuggested)
            throw new InvalidOperationException("This leave plan has no suggested changes to respond to.");

        DateOnly newStart, newEnd;
        if (dto.Accept)
        {
            if (entity.SuggestedStartDate == null || entity.SuggestedEndDate == null)
                throw new InvalidOperationException("No suggested dates are available to accept.");
            newStart = entity.SuggestedStartDate.Value;
            newEnd = entity.SuggestedEndDate.Value;
        }
        else
        {
            if (dto.StartDate == null || dto.EndDate == null)
                throw new InvalidOperationException("Provide your preferred start and end dates.");
            newStart = dto.StartDate.Value;
            newEnd = dto.EndDate.Value;
        }

        if (newEnd < newStart)
            throw new InvalidOperationException("End date must be after or equal to start date.");

        var hasConflict = await HasConflictingPlanAsync(entity.EmployeeId, newStart, newEnd, id);
        if (hasConflict)
            throw new InvalidOperationException("Employee already has a leave plan for this period.");

        entity.StartDate = newStart;
        entity.EndDate = newEnd;
        entity.Year = LeaveYear.For(newStart, await _leaveYear.StartMonthAsync());
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;

        // The suggestion is now resolved — clear it before re-submitting.
        entity.SuggestedStartDate = null;
        entity.SuggestedEndDate = null;
        entity.ManagerSuggestionNotes = null;

        // The resubmission path, and it needs the same guard as the first submit above: without it
        // a plan sent back for changes would approve itself on its way back in.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start approval workflow.");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(entity, submitOutcome, GetCurrentUserId());

        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} re-submitted after suggestion ({mode})",
            id, dto.Accept ? "accepted" : "countered");
        return (await GetWithIncludes(id))!.ToDto();
    }

    /// <remarks>
    /// <para>Round 5 lane E5 — the stakeholders asked what the approver's Cancel did, and the honest
    /// answer was "anything, for anyone with the write tier, with no reason, and the approver's queue
    /// never heard". Now:</para>
    /// <list type="bullet">
    ///   <item>the employee may cancel their own plan until it is approved;</item>
    ///   <item>the desk may also cancel an APPROVED plan — the one cancel that takes back something
    ///   agreed — provided no leave request has been raised from it, and must say why;</item>
    ///   <item>a rejected or cancelled plan has nothing left to cancel.</item>
    /// </list>
    /// <para>⚠ <b>Every check runs before the approval is withdrawn.</b> A refusal after the
    /// workflow cancel would leave a plan nobody can approve — the order-of-operations defect the
    /// leave closure harness found in <c>SuggestChangesAsync</c>.</para>
    /// </remarks>
    public async Task CancelLeavePlanAsync(Guid id, string? reason, bool actingAsDesk)
    {
        var entity = await GetOwnedLeavePlanAsync(id);
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        switch (entity.Status)
        {
            case LeavePlanStatus.Cancelled:
                throw new InvalidOperationException("Leave plan is already cancelled.");

            case LeavePlanStatus.Rejected:
                throw new InvalidOperationException("This plan was rejected, so there is nothing to cancel.");

            case LeavePlanStatus.Approved:
                if (!actingAsDesk)
                    throw new InvalidOperationException(
                        "An approved plan can only be cancelled by HR. Ask HR to cancel it, or raise the leave " +
                        "request with the dates you now want.");
                if (trimmedReason == null)
                    throw new InvalidOperationException(
                        "Say why this approved plan is being cancelled — it takes back something that was agreed.");

                var tenantId = GetTenantId();
                var raised = await _leaveRequestRepository.GetQueryable()
                    .AnyAsync(r => r.TenantId == tenantId
                                && r.LeavePlanId == id
                                && r.Status != LeaveStatus.Cancelled
                                && r.Status != LeaveStatus.Rejected);
                if (raised)
                    throw new InvalidOperationException(
                        "A leave request has already been raised from this plan. Cancel or change the request instead.");
                break;
        }

        // A plan out for approval has a live workflow instance. Withdraw it, or the approver's queue
        // goes on offering a plan that no longer exists — which is what cancelling used to do.
        // A missing instance is not an error (an unseeded tenant, or a plan submitted before its
        // definition existed): there is simply nothing to withdraw, and approve/reject now refuse a
        // cancelled plan on their own (EnsureAwaitingDecision), so a stray instance cannot revive it.
        if (entity.Status == LeavePlanStatus.Submitted)
        {
            var withdrawn = await _workflowIntegrationService.CancelWorkflowAsync(
                EntityType, id, trimmedReason ?? "Leave plan cancelled");
            if (!withdrawn.Success)
                _logger.LogWarning(
                    "Leave plan {id} cancelled; its approval could not be withdrawn: {message}",
                    id, withdrawn.Message);
        }

        entity.Status = LeavePlanStatus.Cancelled;
        entity.CancellationDate = DateTime.UtcNow;
        entity.CancellationReason = trimmedReason;
        entity.WorkflowInstanceId = null;
        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id} cancelled ({by})", id, actingAsDesk ? "desk" : "owner");
    }

    public async Task<LeavePlanDto> UpdateRelieversAsync(Guid id, UpdateLeavePlanRelieversDto dto)
    {
        var entity = await GetOwnedLeavePlanAsync(id);

        if (entity.Status is not (LeavePlanStatus.Submitted
                                  or LeavePlanStatus.ChangesSuggested
                                  or LeavePlanStatus.Approved))
        {
            throw new InvalidOperationException(entity.Status == LeavePlanStatus.Draft
                ? "This plan is still a draft — edit the plan itself instead."
                : $"The relievers of a {entity.Status.ToString().ToLowerInvariant()} plan can no longer be changed.");
        }

        await ValidateRelieversAsync(entity.EmployeeId, dto.RelieverId, dto.SecondRelieverId);

        entity.RelieverId = dto.RelieverId;
        entity.SecondRelieverId = dto.SecondRelieverId;
        await _leavePlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Leave plan {id}: relievers changed", id);
        return await GetByIdAsync(id);
    }

    /// <summary>
    /// Plans are for annual leave (round 5, decision A4 / lane A2).
    /// </summary>
    /// <remarks>
    /// A plan is the year's annual leave agreed in advance: "scheduled, then applied for as of
    /// right". Sick, casual and compassionate leave are requested when they happen, and maternity
    /// follows the birth. Plans of another kind made before this are left alone; new plans and edits
    /// are refused.
    /// </remarks>
    private async Task RequireAnnualTypeAsync(Guid leaveTypeId)
    {
        var tenantId = GetTenantId();
        var type = await _unitOfWork.Repository<LeaveType>().GetQueryable()
            .Where(t => t.TenantId == tenantId && t.Id == leaveTypeId)
            .Select(t => new { t.Name, t.Category })
            .FirstOrDefaultAsync();

        if (type == null)
            throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");
        if (type.Category != LeaveTypeCategory.Annual)
            throw new InvalidOperationException(
                $"Leave plans are for annual leave, and '{type.Name}' is not. Other kinds of leave are "
                + "requested when they are needed.");
    }

    /// <summary>
    /// Refuses a reliever who is the employee themselves, is not this tenant's employee, or is not
    /// at work (round 5 lanes E2/E3) — the refusals a leave request already makes.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Availability is deliberately NOT a refusal here. A plan's reliever clash check is
    /// advisory — plans move, and the planner may know that a colleague's leave is about to change —
    /// so it warns rather than blocks. A request, which fixes real dates, keeps its hard clash
    /// check.</para>
    ///
    /// <para>"At work" is Active or on probation (<see cref="LeaveService.CanCover"/>). It was Active
    /// only, and every hire starts on probation: on UAT that refused 2,191 of TDC's 2,399 staff.</para>
    /// </remarks>
    private async Task ValidateRelieversAsync(Guid employeeId, Guid? relieverId, Guid? secondRelieverId)
    {
        if (relieverId.HasValue && relieverId == secondRelieverId)
            throw new InvalidOperationException("The same person cannot be both reliever and second reliever.");

        var tenantId = GetTenantId();
        foreach (var candidate in new[] { relieverId, secondRelieverId })
        {
            if (candidate is not Guid rid) continue;

            if (rid == employeeId)
                throw new InvalidOperationException("An employee cannot be their own reliever.");

            var reliever = await _unitOfWork.Repository<Employee>().GetQueryable()
                .Where(e => e.TenantId == tenantId && e.Id == rid && !e.IsDeleted)
                .Select(e => new { e.IsActive, e.StaffStatus })
                .FirstOrDefaultAsync();

            if (reliever == null)
                throw new InvalidOperationException("The chosen reliever is not one of this company's employees.");
            if (!LeaveService.CanCover(reliever.IsActive, reliever.StaffStatus))
                throw new InvalidOperationException(LeaveService.NotAtWorkMessage(reliever.StaffStatus));
        }
    }

    /// <summary>
    /// Fills empty reliever slots from the employee's reliever roster, by priority (round 5 lane E2).
    /// </summary>
    /// <remarks>
    /// <para>Leave REQUESTS have filled their relievers from the roster since the port
    /// (<c>LeaveService.AutoFillRelieversAsync</c>); plans never did, so every plan's relievers were
    /// typed by hand and the stakeholders asked why the profile's relievers were not offered.</para>
    ///
    /// <para>A roster entry who is not free over the dates is skipped rather than assigned, judged by
    /// the same clash check the planner is shown, so the form and the fill cannot disagree. ⚠ That is
    /// why this is not a helper shared with the request path, which judges availability by live
    /// requests only. Explicit picks are never overridden.</para>
    /// </remarks>
    private async Task FillRelieversFromRosterAsync(LeavePlan entity)
    {
        if (entity.RelieverId.HasValue && entity.SecondRelieverId.HasValue) return;

        foreach (var candidate in (await LoadRelieverRosterAsync(entity.EmployeeId)).Select(r => r.EmployeeId))
        {
            if (candidate == entity.EmployeeId
                || candidate == entity.RelieverId
                || candidate == entity.SecondRelieverId)
                continue;

            var clashes = await GetRelieverClashesAsync(candidate, entity.StartDate, entity.EndDate);
            if (clashes.Count > 0) continue;

            if (!entity.RelieverId.HasValue) entity.RelieverId = candidate;
            else entity.SecondRelieverId = candidate;

            if (entity.RelieverId.HasValue && entity.SecondRelieverId.HasValue) break;
        }
    }

    /// <summary>The employee's active reliever roster — staff at work only — by priority.</summary>
    private async Task<List<LeavePlanRosterRelieverDto>> LoadRelieverRosterAsync(Guid employeeId)
    {
        var tenantId = GetTenantId();
        var rows = await _unitOfWork.Repository<EmployeeReliever>().GetQueryable()
            .Include(r => r.RelieverEmployee).ThenInclude(e => e.Position)
            .Where(r => r.TenantId == tenantId
                     && r.EmployeeId == employeeId
                     && r.IsActive
                     && !r.IsDeleted
                     && !r.RelieverEmployee.IsDeleted
                     // LeaveService.CanCover, spelled out so EF can translate it.
                     && r.RelieverEmployee.IsActive
                     && (r.RelieverEmployee.StaffStatus == StaffStatus.Active
                         || r.RelieverEmployee.StaffStatus == StaffStatus.Probation))
            .OrderBy(r => r.Priority)
            .AsNoTracking()
            .ToListAsync();

        // FullName is computed in C#, so the names are read after the rows are loaded.
        return rows.Select(r => new LeavePlanRosterRelieverDto
        {
            EmployeeId = r.RelieverEmployeeId,
            Name = r.RelieverEmployee.FullName,
            PositionName = r.RelieverEmployee.Position?.Title,
            Priority = r.Priority,
        }).ToList();
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

    // ── Reliever clashes ──────────────────────────────────────────────────────────
    //
    // Finish-plan lane 4 (2026-09-01). TDC's demo feedback: "reliever clashes are not visible on
    // the plan". A plan named a reliever and nothing ever asked whether that person would be there.
    // Three ways they might not be: they have a leave PLAN of their own over the dates, they have a
    // leave REQUEST (pending, approved or under way) over the dates, or another plan in the window
    // already names them as reliever. Advisory, not a gate — the register and the form both show it
    // and let the planner decide; what neither may do is stay silent.

    private static readonly LeaveStatus[] LiveRequestStatuses =
        { LeaveStatus.Pending, LeaveStatus.Approved, LeaveStatus.InProgress };

    public async Task<IReadOnlyList<LeaveRelieverClashDto>> GetRelieverClashesAsync(
        Guid relieverId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null)
    {
        var probe = new LeavePlanDto
        {
            Id = excludePlanId ?? Guid.Empty,
            StartDate = startDate,
            EndDate = endDate,
            RelieverId = relieverId,
        };
        await EnrichRelieverClashesAsync(new List<LeavePlanDto> { probe });
        return probe.RelieverClashes;
    }

    /// <summary>
    /// Fills <see cref="LeavePlanDto.RelieverClashes"/> for every plan in the list with one query per
    /// source over the whole window, rather than three per row.
    /// </summary>
    /// <summary>
    /// Fills <c>RaisedLeaveRequestId</c>/<c>Number</c> — the request, if any, already raised from
    /// each plan. One query for the whole page, the same shape as its reliever-clash sibling.
    /// </summary>
    /// <remarks>
    /// Cancelled and rejected requests are excluded on purpose: a plan whose request was cancelled
    /// has not been used up, and the employee should be able to raise another from it.
    /// </remarks>
    private async Task EnrichRaisedRequestsAsync(List<LeavePlanDto> plans)
    {
        if (plans.Count == 0) return;

        var tenantId = GetTenantId();
        var planIds = plans.Select(p => p.Id).ToList();

        var raised = await _leaveRequestRepository
            .GetQueryable()
            .Where(r => r.TenantId == tenantId
                     && r.LeavePlanId != null
                     && planIds.Contains(r.LeavePlanId!.Value)
                     && r.Status != LeaveStatus.Cancelled
                     && r.Status != LeaveStatus.Rejected)
            .Select(r => new { PlanId = r.LeavePlanId!.Value, r.Id, r.RequestNumber })
            .ToListAsync();

        var byPlan = raised
            .GroupBy(r => r.PlanId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var plan in plans)
        {
            if (!byPlan.TryGetValue(plan.Id, out var request)) continue;
            plan.RaisedLeaveRequestId = request.Id;
            plan.RaisedLeaveRequestNumber = request.RequestNumber;
        }
    }

    private async Task EnrichRelieverClashesAsync(List<LeavePlanDto> plans)
    {
        var withReliever = plans.Where(p => p.RelieverId.HasValue || p.SecondRelieverId.HasValue).ToList();
        if (withReliever.Count == 0) return;

        var tenantId = GetTenantId();
        var relieverIds = withReliever
            .SelectMany(p => new[] { p.RelieverId, p.SecondRelieverId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var windowStart = withReliever.Min(p => p.StartDate);
        var windowEnd = withReliever.Max(p => p.EndDate);

        // (a) + (c): plans in the window where a reliever is the employee, or is named as reliever.
        var plansInWindow = await _leavePlanRepository.GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Where(p => p.TenantId == tenantId
                     && p.Status != LeavePlanStatus.Cancelled
                     && p.Status != LeavePlanStatus.Rejected
                     && p.StartDate <= windowEnd && p.EndDate >= windowStart
                     && (relieverIds.Contains(p.EmployeeId)
                         || (p.RelieverId.HasValue && relieverIds.Contains(p.RelieverId.Value))
                         || (p.SecondRelieverId.HasValue && relieverIds.Contains(p.SecondRelieverId.Value))))
            .AsNoTracking()
            .ToListAsync();

        // (b): the reliever's own leave requests that are live over the window.
        var requestsInWindow = await _leaveRequestRepository.GetQueryable()
            .Include(r => r.LeaveType)
            .Where(r => r.TenantId == tenantId
                     && relieverIds.Contains(r.EmployeeId)
                     && LiveRequestStatuses.Contains(r.Status)
                     && r.StartDate <= windowEnd && r.EndDate >= windowStart)
            .AsNoTracking()
            .ToListAsync();

        foreach (var plan in withReliever)
        {
            plan.RelieverClashes.Clear();
            var slots = new (Guid? Id, string? Name, int Slot)[]
            {
                (plan.RelieverId, plan.RelieverName, 1),
                (plan.SecondRelieverId, plan.SecondRelieverName, 2),
            };
            foreach (var (relieverId, relieverName, slot) in slots)
            {
                if (relieverId is not Guid rid) continue;

                foreach (var other in plansInWindow.Where(o => o.Id != plan.Id
                                                             && o.StartDate <= plan.EndDate
                                                             && o.EndDate >= plan.StartDate))
                {
                    if (other.EmployeeId == rid)
                    {
                        plan.RelieverClashes.Add(new LeaveRelieverClashDto
                        {
                            RelieverId = rid,
                            RelieverName = relieverName ?? other.Employee?.FullName ?? string.Empty,
                            Slot = slot,
                            Source = "LeavePlan",
                            Description = $"has a {other.Status.ToString().ToLowerInvariant()} {other.LeaveType?.Name ?? "leave"} plan of their own over these dates",
                            FromDate = other.StartDate,
                            ToDate = other.EndDate,
                        });
                    }
                    else if (other.RelieverId == rid || other.SecondRelieverId == rid)
                    {
                        plan.RelieverClashes.Add(new LeaveRelieverClashDto
                        {
                            RelieverId = rid,
                            RelieverName = relieverName ?? string.Empty,
                            Slot = slot,
                            Source = "RelieverOnAnotherPlan",
                            Description = $"is already named as reliever for {other.Employee?.FullName ?? "another employee"} over these dates",
                            FromDate = other.StartDate,
                            ToDate = other.EndDate,
                        });
                    }
                }

                foreach (var request in requestsInWindow.Where(r => r.EmployeeId == rid
                                                                  && r.StartDate <= plan.EndDate
                                                                  && r.EndDate >= plan.StartDate))
                {
                    plan.RelieverClashes.Add(new LeaveRelieverClashDto
                    {
                        RelieverId = rid,
                        RelieverName = relieverName ?? string.Empty,
                        Slot = slot,
                        Source = "LeaveRequest",
                        Description = $"has a {request.Status.ToString().ToLowerInvariant()} {request.LeaveType?.Name ?? "leave"} request {request.RequestNumber} over these dates",
                        FromDate = request.StartDate,
                        ToDate = request.EndDate,
                    });
                }
            }
        }
    }

    private async Task<LeavePlan?> GetWithIncludes(Guid id)
    {
        var tenantId = GetTenantId();
        return await _leavePlanRepository
            .GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.OrganizationLevel)
            .Include(p => p.OrganizationUnit)
            .Include(p => p.Position)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.SecondRelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
    }
}
