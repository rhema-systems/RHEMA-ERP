using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Identity;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Identity;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Identity;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Identity;

public sealed class HrIdentityReconciliationService : IHrIdentityReconciliationService
{
    private const string SystemReason = "HR/Identity reconciliation";
    private static readonly StaffStatus[] EligibleStatuses =
        [StaffStatus.Active, StaffStatus.Probation, StaffStatus.OnLeave];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ApplicationDbContext _db;
    private readonly ILogger<HrIdentityReconciliationService> _logger;

    public HrIdentityReconciliationService(
        ApplicationDbContext db,
        ILogger<HrIdentityReconciliationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HrIdentityReconciliationRunDto> RunAsync(
        Guid tenantId,
        Guid? requestedById,
        HrIdentityReconciliationTrigger trigger,
        string? idempotencyKey = null,
        IReadOnlyCollection<Guid>? employeeIds = null,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        var key = NormalizeIdempotencyKey(idempotencyKey, trigger);
        var existing = await _db.HrIdentityReconciliationRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                !item.IsDeleted && item.TenantId == tenantId && item.IdempotencyKey == key,
                cancellationToken);
        if (existing != null)
        {
            return MapRun(existing);
        }

        var run = new HrIdentityReconciliationRun
        {
            TenantId = tenantId,
            IdempotencyKey = key,
            Trigger = trigger,
            Status = HrIdentityReconciliationRunStatus.Running,
            RequestedById = requestedById,
            StartedAtUtc = DateTime.UtcNow,
            CreatedById = requestedById,
            CreatedBy = requestedById?.ToString() ?? "System"
        };
        _db.HrIdentityReconciliationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var employeeFilter = employeeIds?.Where(id => id != Guid.Empty).Distinct().ToArray();
            var candidatesQuery =
                from user in _db.Users.IgnoreQueryFilters().AsNoTracking()
                join employee in _db.Employees.IgnoreQueryFilters().AsNoTracking()
                    on user.EmployeeId equals employee.Id
                where user.TenantId == tenantId &&
                      employee.TenantId == tenantId &&
                      !employee.IsDeleted
                select new { UserId = user.Id, EmployeeId = employee.Id };
            if (employeeFilter is { Length: > 0 })
            {
                candidatesQuery = candidatesQuery.Where(item => employeeFilter.Contains(item.EmployeeId));
            }

            var candidates = await candidatesQuery
                .OrderBy(item => item.EmployeeId)
                .Select(item => new Candidate(item.UserId, item.EmployeeId))
                .ToListAsync(cancellationToken);
            run.CandidateCount = candidates.Count;
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                try
                {
                    var result = await ReconcileCandidateAsync(run, candidate, requestedById, cancellationToken);
                    switch (result.Status)
                    {
                        case HrIdentityReconciliationItemStatus.Reconciled:
                        case HrIdentityReconciliationItemStatus.NoChange:
                            run.ReconciledCount++;
                            break;
                        case HrIdentityReconciliationItemStatus.ReviewRequired:
                            run.ReviewRequiredCount++;
                            break;
                        case HrIdentityReconciliationItemStatus.Failed:
                            run.FailedCount++;
                            break;
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "HR/Identity reconciliation failed for tenant {TenantId}, user {UserId}, employee {EmployeeId}",
                        tenantId,
                        candidate.UserId,
                        candidate.EmployeeId);
                    _db.ChangeTracker.Clear();
                    run = await _db.HrIdentityReconciliationRuns
                        .IgnoreQueryFilters()
                        .SingleAsync(item => item.Id == run.Id, cancellationToken);
                    var previousAttempts = await _db.HrIdentityReconciliationItems
                        .IgnoreQueryFilters()
                        .CountAsync(item => item.RunId == run.Id && item.UserId == candidate.UserId, cancellationToken);
                    _db.HrIdentityReconciliationItems.Add(new HrIdentityReconciliationItem
                    {
                        TenantId = tenantId,
                        RunId = run.Id,
                        UserId = candidate.UserId,
                        EmployeeId = candidate.EmployeeId,
                        AttemptNumber = previousAttempts + 1,
                        Status = HrIdentityReconciliationItemStatus.Failed,
                        Summary = "Reconciliation failed and is eligible for retry.",
                        Error = Truncate(exception.Message, 2000),
                        ProcessedAtUtc = DateTime.UtcNow,
                        CreatedById = requestedById,
                        CreatedBy = requestedById?.ToString() ?? "System"
                    });
                    run.FailedCount++;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            run.Status = run.FailedCount > 0
                ? HrIdentityReconciliationRunStatus.CompletedWithErrors
                : HrIdentityReconciliationRunStatus.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return MapRun(run);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "HR/Identity reconciliation run {RunId} failed before it could reach a terminal state.",
                run.Id);

            // Do not leave an idempotency key permanently stuck in Running if
            // candidate discovery or final persistence fails. Use an
            // independent token so cancellation of the work does not prevent
            // recording the terminal failure that operators must be able to
            // inspect and retry.
            _db.ChangeTracker.Clear();
            var failedRun = await _db.HrIdentityReconciliationRuns
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            if (failedRun.Status == HrIdentityReconciliationRunStatus.Running)
            {
                failedRun.Status = HrIdentityReconciliationRunStatus.Failed;
                failedRun.CompletedAtUtc = DateTime.UtcNow;
                failedRun.Error = Truncate(exception.Message, 2000);
                await _db.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<HrIdentityReconciliationDashboardDto> GetDashboardAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        var states = await _db.HrIdentityReconciliationStates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.ReactivationReviewRequired)
            .ThenBy(item => item.UserId)
            .ToListAsync(cancellationToken);

        var issues = await _db.HrIdentityWorkflowIssues.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.DetectedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);
        var issueUserIds = issues.SelectMany(item => new Guid?[]
            {
                item.UserId,
                item.StaleAssigneeId,
                item.SuggestedReplacementUserId,
                item.ReplacementUserId
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value);
        var userIds = states.Select(item => item.UserId)
            .Concat(issueUserIds)
            .Distinct()
            .ToArray();
        var users = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var employeeIds = states.Select(item => item.EmployeeId)
            .Concat(states.Where(item => item.ManagerEmployeeId.HasValue).Select(item => item.ManagerEmployeeId!.Value))
            .Concat(issues.Select(item => item.EmployeeId))
            .Distinct()
            .ToArray();
        var employees = await HrEmployees()
            .Where(item => employeeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var departmentIds = states.Where(item => item.DepartmentId.HasValue)
            .Select(item => item.DepartmentId!.Value)
            .Distinct()
            .ToArray();
        var departments = await _db.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => departmentIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var stateDtos = states.Select(state => MapState(state, users, employees, departments)).ToList();
        var issueDtos = issues.Select(item => MapIssue(item, users, employees)).ToList();
        var runEntities = await _db.HrIdentityReconciliationRuns.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.StartedAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);
        var runs = runEntities.Select(MapRun).ToList();

        return new HrIdentityReconciliationDashboardDto(
            stateDtos.Count,
            stateDtos.Count(item => !item.HrAccessEligible),
            stateDtos.Count(item => item.AccessSuspendedByReconciliation),
            stateDtos.Count(item => item.ReactivationReviewRequired),
            issueDtos.Count(item => item.Status == HrIdentityWorkflowIssueStatus.Open.ToString()),
            stateDtos,
            issueDtos,
            runs);
    }

    public async Task<IReadOnlyList<HrIdentityUserOptionDto>> GetEligibleReplacementUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        var now = DateTime.UtcNow;
        var rows = await (
            from user in _db.Users.IgnoreQueryFilters().AsNoTracking()
            join employee in HrEmployees()
                on user.EmployeeId equals employee.Id
            join department in _db.Departments.IgnoreQueryFilters().AsNoTracking()
                on employee.DepartmentId equals department.Id into departments
            from department in departments.DefaultIfEmpty()
            where user.TenantId == tenantId &&
                  user.IsActive &&
                  employee.TenantId == tenantId &&
                  !employee.IsDeleted &&
                  employee.IsActive &&
                  EligibleStatuses.Contains(employee.StaffStatus) &&
                  (!employee.TerminationDate.HasValue || employee.TerminationDate.Value > now)
            orderby user.FirstName, user.LastName, user.UserName
            select new HrIdentityUserOptionDto(
                user.Id,
                employee.Id,
                (user.FirstName + " " + user.LastName).Trim(),
                user.UserName,
                employee.EmployeeNumber,
                employee.DepartmentId,
                department != null ? department.Name : null,
                _db.Employees.IgnoreQueryFilters().Any(candidate =>
                    !candidate.IsDeleted && candidate.TenantId == tenantId && candidate.ManagerId == employee.Id))
        ).ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<HrIdentityWorkflowIssueDto> ResolveWorkflowIssueAsync(
        Guid tenantId,
        Guid issueId,
        Guid replacementUserId,
        Guid actorUserId,
        string resolutionNote,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireActor(actorUserId);
        RequireReviewNote(resolutionNote, nameof(resolutionNote));
        var issue = await _db.HrIdentityWorkflowIssues
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == issueId, cancellationToken)
            ?? throw new KeyNotFoundException("The HR/Identity workflow issue was not found.");
        if (issue.Status != HrIdentityWorkflowIssueStatus.Open)
        {
            throw new InvalidOperationException("Only an open HR/Identity workflow issue can be resolved.");
        }

        var approval = issue.WorkflowApprovalId.HasValue
            ? await _db.WorkflowApprovals
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowStep)
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowInstance)
                .SingleOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.Id == issue.WorkflowApprovalId.Value,
                    cancellationToken)
            : null;
        if (approval == null || approval.Status != WorkflowApprovalStatus.Pending)
        {
            throw new InvalidOperationException("The linked approval is no longer pending; refresh the reconciliation workspace.");
        }

        var eligibility = await ValidateReplacementAsync(tenantId, approval, replacementUserId, cancellationToken);
        if (!eligibility.IsAllowed)
        {
            throw new InvalidOperationException(eligibility.Reason);
        }

        ReassignApproval(approval, replacementUserId, actorUserId, resolutionNote, DateTime.UtcNow);
        issue.Status = HrIdentityWorkflowIssueStatus.Resolved;
        issue.ReplacementUserId = replacementUserId;
        issue.ResolvedById = actorUserId;
        issue.ResolvedAtUtc = DateTime.UtcNow;
        issue.ResolutionNote = resolutionNote.Trim();
        issue.UpdatedAt = DateTime.UtcNow;
        issue.LastModifiedById = actorUserId;
        AddWorkflowActivity(approval, actorUserId, "HR/Identity workflow issue resolved", resolutionNote);
        AddAuditLog(tenantId, actorUserId, "ResolveHrIdentityWorkflowIssue", "HrIdentityWorkflowIssue",
            issue.Id, null, new { replacementUserId, resolutionNote = resolutionNote.Trim() });
        await _db.SaveChangesAsync(cancellationToken);

        var users = await LoadUsersAsync(
            [issue.UserId, issue.StaleAssigneeId, issue.SuggestedReplacementUserId, replacementUserId],
            cancellationToken);
        var employees = await HrEmployees()
            .Where(item => item.Id == issue.EmployeeId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return MapIssue(issue, users, employees);
    }

    public async Task<HrIdentityReconciliationStateDto> ApproveReactivationAsync(
        Guid tenantId,
        Guid userId,
        Guid actorUserId,
        string reviewNote,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireActor(actorUserId);
        RequireReviewNote(reviewNote, nameof(reviewNote));
        var state = await _db.HrIdentityReconciliationStates
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("The HR/Identity reconciliation state was not found.");
        if (!state.AccessSuspendedByReconciliation || !state.ReactivationReviewRequired)
        {
            throw new InvalidOperationException("This identity does not have a pending reconciliation reactivation review.");
        }

        var employee = await HrEmployees()
            .SingleOrDefaultAsync(item =>
                !item.IsDeleted && item.TenantId == tenantId && item.Id == state.EmployeeId,
                cancellationToken)
            ?? throw new InvalidOperationException("The linked HR employment record is unavailable.");
        if (!IsHrAccessEligible(employee, DateTime.UtcNow))
        {
            throw new InvalidOperationException("HR still marks this employee as ineligible for ERP access.");
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("The identity was not found.");
        var suspendedIds = DeserializeIds(state.SuspendedUserTenantIdsJson);
        var suspendedRelationships = await _db.UserTenants.IgnoreQueryFilters()
            .Where(item =>
                suspendedIds.Contains(item.Id) &&
                item.TenantId == tenantId &&
                item.UserId == userId &&
                !item.IsDeleted &&
                item.Status == UserTenantStatus.Suspended)
            .ToListAsync(cancellationToken);

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = actorUserId.ToString();
        foreach (var relationship in suspendedRelationships)
        {
            relationship.Status = UserTenantStatus.Active;
            relationship.ReactivatedAt = DateTime.UtcNow;
            relationship.StatusChangedBy = actorUserId.ToString();
            relationship.Notes = AppendNote(relationship.Notes, $"Reactivated after HR/Identity review: {reviewNote.Trim()}");
            relationship.UpdatedAt = DateTime.UtcNow;
            relationship.LastModifiedById = actorUserId;
        }

        state.AccessSuspendedByReconciliation = false;
        state.ReactivationReviewRequired = false;
        state.SuspendedUserTenantIdsJson = "[]";
        state.ReviewReason = null;
        state.LastReconciledAtUtc = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;
        state.LastModifiedById = actorUserId;
        AddAuditLog(tenantId, actorUserId, "ApproveHrIdentityReactivation", "ApplicationUser", userId,
            new { isActive = false },
            new { isActive = true, restoredTenantRelationships = suspendedRelationships.Count, reviewNote = reviewNote.Trim() });
        await _db.SaveChangesAsync(cancellationToken);

        var users = new Dictionary<Guid, ApplicationUser> { [user.Id] = user };
        var employees = new Dictionary<Guid, Employee> { [employee.Id] = employee };
        var departments = employee.DepartmentId.HasValue
            ? await _db.Departments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == employee.DepartmentId.Value)
                .Select(item => new { item.Id, item.Name })
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            : new Dictionary<Guid, string>();
        return MapState(state, users, employees, departments);
    }

    public async Task<HrIdentityReconciliationRunDto> RetryFailedRunAsync(
        Guid tenantId,
        Guid runId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireActor(actorUserId);
        var runExists = await _db.HrIdentityReconciliationRuns.AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == runId, cancellationToken);
        if (!runExists)
        {
            throw new KeyNotFoundException("The reconciliation run was not found.");
        }

        var failedEmployeeIds = await _db.HrIdentityReconciliationItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RunId == runId &&
                           item.Status == HrIdentityReconciliationItemStatus.Failed)
            .Select(item => item.EmployeeId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (failedEmployeeIds.Length == 0)
        {
            throw new InvalidOperationException("The selected reconciliation run has no failed items to retry.");
        }

        return await RunAsync(
            tenantId,
            actorUserId,
            HrIdentityReconciliationTrigger.Retry,
            $"retry:{runId:N}:{Guid.NewGuid():N}",
            failedEmployeeIds,
            cancellationToken);
    }

    private async Task<HrIdentityReconciliationItem> ReconcileCandidateAsync(
        HrIdentityReconciliationRun run,
        Candidate candidate,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = await _db.Users.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == candidate.UserId && item.TenantId == run.TenantId, cancellationToken);
        var employee = await HrEmployees()
            .SingleAsync(item =>
                !item.IsDeleted && item.Id == candidate.EmployeeId && item.TenantId == run.TenantId,
                cancellationToken);
        var roles = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            orderby role.Name
            select role.Name ?? string.Empty)
            .Where(name => name != string.Empty)
            .ToArrayAsync(cancellationToken);
        var roleJson = JsonSerializer.Serialize(roles, JsonOptions);
        var managerUserId = employee.ManagerId.HasValue
            ? await ResolveEligibleUserForEmployeeAsync(run.TenantId, employee.ManagerId.Value, cancellationToken)
            : null;
        var eligible = IsHrAccessEligible(employee, now);
        var fingerprint = Fingerprint(employee, eligible, managerUserId, roles);
        var state = await _db.HrIdentityReconciliationStates
            .SingleOrDefaultAsync(item => item.TenantId == run.TenantId && item.UserId == user.Id, cancellationToken);

        if (state == null)
        {
            state = await _db.HrIdentityReconciliationStates
                .SingleOrDefaultAsync(item => item.TenantId == run.TenantId && item.EmployeeId == employee.Id, cancellationToken);

            if (state != null && state.UserId != user.Id)
            {
                state.UserId = user.Id;
            }
        }

        var previousDepartmentId = state?.DepartmentId;
        var previousManagerEmployeeId = state?.ManagerEmployeeId;
        var accessChanged = state == null || state.HrAccessEligible != eligible;
        var departmentChanged = state != null && state.DepartmentId != employee.DepartmentId;
        var managerChanged = state != null && state.ManagerEmployeeId != employee.ManagerId;
        var roleChanged = state != null && !string.Equals(state.RoleNamesJson, roleJson, StringComparison.Ordinal);
        var sessionsRevoked = 0;
        var refreshTokensRevoked = 0;
        var responsibilitiesSuspended = 0;
        var workflowsReassigned = 0;
        var issuesCreated = 0;

        if (state == null)
        {
            state = new HrIdentityReconciliationState
            {
                TenantId = run.TenantId,
                UserId = user.Id,
                EmployeeId = employee.Id,
                CreatedById = actorUserId,
                CreatedBy = actorUserId?.ToString() ?? "System"
            };
            _db.HrIdentityReconciliationStates.Add(state);
        }

        if (!eligible)
        {
            var wasActiveAtDetection = user.IsActive;
            var suspendedRelationshipIds = DeserializeIds(state.SuspendedUserTenantIdsJson);
            if (user.IsActive)
            {
                user.IsActive = false;
                user.UpdatedAt = now;
                user.UpdatedBy = actorUserId?.ToString() ?? "System";
            }

            var activeRelationships = await _db.UserTenants.IgnoreQueryFilters()
                .Where(item =>
                    !item.IsDeleted && item.TenantId == run.TenantId && item.UserId == user.Id &&
                    item.Status == UserTenantStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var relationship in activeRelationships)
            {
                relationship.Status = UserTenantStatus.Suspended;
                relationship.SuspendedAt = now;
                relationship.StatusChangedBy = actorUserId?.ToString() ?? "System";
                relationship.Notes = AppendNote(relationship.Notes, SystemReason);
                relationship.UpdatedAt = now;
                relationship.LastModifiedById = actorUserId;
                suspendedRelationshipIds.Add(relationship.Id);
            }

            var sessions = await _db.UserSessions
                .Where(item => item.TenantId == run.TenantId && item.UserId == user.Id && item.IsActive)
                .ToListAsync(cancellationToken);
            var sessionJtis = sessions.Where(item => !string.IsNullOrWhiteSpace(item.JwtTokenId))
                .Select(item => item.JwtTokenId!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var existingJtis = sessionJtis.Length == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await _db.BlacklistedTokens.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => !item.IsDeleted && sessionJtis.Contains(item.Jti))
                    .Select(item => item.Jti)
                    .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            foreach (var session in sessions)
            {
                session.MarkAsLoggedOut(SystemReason);
                sessionsRevoked++;
                if (!string.IsNullOrWhiteSpace(session.JwtTokenId) && existingJtis.Add(session.JwtTokenId))
                {
                    _db.BlacklistedTokens.Add(new BlacklistedToken
                    {
                        Jti = session.JwtTokenId,
                        UserId = user.Id,
                        ExpiresAt = now.AddHours(24),
                        BlacklistedAt = now,
                        Reason = SystemReason,
                        CreatedById = actorUserId,
                        CreatedBy = actorUserId?.ToString() ?? "System"
                    });
                }
            }

            var refreshTokens = await _db.RefreshTokens.IgnoreQueryFilters()
                .Where(item => !item.IsDeleted && item.UserId == user.Id && !item.IsRevoked && item.ExpiresAt > now)
                .ToListAsync(cancellationToken);
            foreach (var token in refreshTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
                token.RevokedBy = actorUserId;
                token.RevocationReason = SystemReason;
                refreshTokensRevoked++;
            }

            var responsibilities = await _db.ProcurementResponsibilityAssignments.IgnoreQueryFilters()
                .Where(item => !item.IsDeleted && item.TenantId == run.TenantId && item.UserId == user.Id &&
                               item.IsActive && item.EffectiveFrom <= now &&
                               (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
                .ToListAsync(cancellationToken);
            foreach (var responsibility in responsibilities)
            {
                responsibility.IsActive = false;
                responsibility.EffectiveTo = now;
                responsibility.Reason = AppendNote(responsibility.Reason, SystemReason);
                responsibility.UpdatedAt = now;
                responsibility.LastModifiedById = actorUserId;
                responsibilitiesSuspended++;
            }

            var assignedApprovals = await _db.WorkflowApprovals
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowStep)
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowInstance)
                .Where(item => item.TenantId == run.TenantId && item.ApproverId == user.Id &&
                               item.Status == WorkflowApprovalStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var approval in assignedApprovals)
            {
                var outcome = await TryAutomaticallyReassignAsync(
                    run.TenantId,
                    employee,
                    approval,
                    managerUserId,
                    HrIdentityWorkflowIssueType.InactiveApprover,
                    actorUserId,
                    "Approver became HR-ineligible.",
                    cancellationToken);
                workflowsReassigned += outcome.Reassigned ? 1 : 0;
                issuesCreated += outcome.IssueCreated ? 1 : 0;
            }

            state.AccessSuspendedByReconciliation = state.AccessSuspendedByReconciliation || wasActiveAtDetection ||
                                                     activeRelationships.Count > 0;
            state.ReactivationReviewRequired = false;
            state.SuspendedUserTenantIdsJson = JsonSerializer.Serialize(
                suspendedRelationshipIds.OrderBy(id => id), JsonOptions);
            state.ReviewReason = "HR employment is inactive, suspended, terminated, retired, or end-dated.";
        }
        else if (state.AccessSuspendedByReconciliation)
        {
            state.ReactivationReviewRequired = true;
            state.ReviewReason = "HR employment is eligible again; Identity approval is required before access is restored.";
        }

        if (managerChanged && previousManagerEmployeeId.HasValue)
        {
            var previousManagerUserId = await ResolveAnyUserForEmployeeAsync(
                run.TenantId, previousManagerEmployeeId.Value, cancellationToken);
            if (previousManagerUserId.HasValue)
            {
                var routedApprovals = await _db.WorkflowApprovals
                    .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowStep)
                    .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowInstance)
                    .Where(item => item.TenantId == run.TenantId &&
                                   item.ApproverId == previousManagerUserId.Value &&
                                   item.Status == WorkflowApprovalStatus.Pending &&
                                   item.StepInstance.WorkflowInstance.InitiatedById == user.Id)
                    .ToListAsync(cancellationToken);
                foreach (var approval in routedApprovals)
                {
                    var outcome = await TryAutomaticallyReassignAsync(
                        run.TenantId,
                        employee,
                        approval,
                        managerUserId,
                        HrIdentityWorkflowIssueType.ManagerReplacementUnavailable,
                        actorUserId,
                        "The initiator's HR manager changed.",
                        cancellationToken);
                    workflowsReassigned += outcome.Reassigned ? 1 : 0;
                    issuesCreated += outcome.IssueCreated ? 1 : 0;
                }
            }
        }

        if (departmentChanged)
        {
            var pendingApprovals = await _db.WorkflowApprovals
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowStep)
                .Include(item => item.StepInstance).ThenInclude(item => item.WorkflowInstance)
                .Where(item => item.TenantId == run.TenantId &&
                               item.Status == WorkflowApprovalStatus.Pending &&
                               item.StepInstance.WorkflowInstance.InitiatedById == user.Id)
                .ToListAsync(cancellationToken);
            foreach (var approval in pendingApprovals)
            {
                if (await CreateIssueIfMissingAsync(
                        run.TenantId,
                        employee,
                        approval,
                        HrIdentityWorkflowIssueType.DepartmentOwnershipChanged,
                        approval.ApproverId,
                        managerUserId,
                        $"Department changed from {previousDepartmentId} to {employee.DepartmentId}; workflow ownership requires review.",
                        actorUserId,
                        cancellationToken))
                {
                    issuesCreated++;
                }
            }
        }

        state.EmployeeId = employee.Id;
        state.HrAccessEligible = eligible;
        state.DepartmentId = employee.DepartmentId;
        state.ManagerEmployeeId = employee.ManagerId;
        state.ManagerUserId = managerUserId;
        state.RoleNamesJson = roleJson;
        state.SourceFingerprint = fingerprint;
        state.LastObservedAtUtc = now;
        state.LastReconciledAtUtc = now;
        state.LastRunId = run.Id;
        state.UpdatedAt = now;
        state.LastModifiedById = actorUserId;

        var hasReview = state.ReactivationReviewRequired || issuesCreated > 0;
        var changed = accessChanged || departmentChanged || managerChanged || roleChanged ||
                      sessionsRevoked > 0 || refreshTokensRevoked > 0 || responsibilitiesSuspended > 0 ||
                      workflowsReassigned > 0 || issuesCreated > 0;
        var item = new HrIdentityReconciliationItem
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            UserId = user.Id,
            EmployeeId = employee.Id,
            AttemptNumber = await _db.HrIdentityReconciliationItems.IgnoreQueryFilters()
                .CountAsync(existing => existing.RunId == run.Id && existing.UserId == user.Id, cancellationToken) + 1,
            Status = hasReview
                ? HrIdentityReconciliationItemStatus.ReviewRequired
                : changed
                    ? HrIdentityReconciliationItemStatus.Reconciled
                    : HrIdentityReconciliationItemStatus.NoChange,
            AccessStateChanged = accessChanged,
            DepartmentChanged = departmentChanged,
            ManagerChanged = managerChanged,
            RoleSnapshotChanged = roleChanged,
            PreviousDepartmentId = previousDepartmentId,
            CurrentDepartmentId = employee.DepartmentId,
            PreviousManagerEmployeeId = previousManagerEmployeeId,
            CurrentManagerEmployeeId = employee.ManagerId,
            SessionsRevoked = sessionsRevoked,
            RefreshTokensRevoked = refreshTokensRevoked,
            ResponsibilityAssignmentsSuspended = responsibilitiesSuspended,
            WorkflowAssignmentsReassigned = workflowsReassigned,
            WorkflowIssuesCreated = issuesCreated,
            Summary = BuildSummary(eligible, accessChanged, departmentChanged, managerChanged, roleChanged,
                sessionsRevoked, refreshTokensRevoked, responsibilitiesSuspended, workflowsReassigned, issuesCreated,
                state.ReactivationReviewRequired),
            ProcessedAtUtc = now,
            CreatedById = actorUserId,
            CreatedBy = actorUserId?.ToString() ?? "System"
        };
        _db.HrIdentityReconciliationItems.Add(item);

        if (actorUserId.HasValue)
        {
            AddAuditLog(run.TenantId, actorUserId.Value, "ReconcileHrIdentity", "ApplicationUser", user.Id,
                new { previousDepartmentId, previousManagerEmployeeId },
                new
                {
                    eligible,
                    employee.DepartmentId,
                    employee.ManagerId,
                    roles,
                    sessionsRevoked,
                    refreshTokensRevoked,
                    responsibilitiesSuspended,
                    workflowsReassigned,
                    issuesCreated
                });
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateHrIdentityStateException(exception))
        {
            _logger.LogWarning(
                exception,
                "Recovered from duplicate HR identity state insert for tenant {TenantId}, user {UserId}, employee {EmployeeId}",
                run.TenantId,
                user.Id,
                employee.Id);

            var stateEntry = _db.Entry(state);
            if (stateEntry.State == EntityState.Added)
            {
                stateEntry.State = EntityState.Detached;
            }

            state = await _db.HrIdentityReconciliationStates
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == run.TenantId && item.UserId == user.Id, cancellationToken)
                ?? await _db.HrIdentityReconciliationStates
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(item => item.TenantId == run.TenantId && item.EmployeeId == employee.Id, cancellationToken);

            if (state == null)
            {
                throw;
            }

            if (state.UserId != user.Id)
            {
                state.UserId = user.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return item;
    }

    private static bool IsDuplicateHrIdentityStateException(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is Microsoft.Data.SqlClient.SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ReassignmentOutcome> TryAutomaticallyReassignAsync(
        Guid tenantId,
        Employee employee,
        WorkflowApproval approval,
        Guid? replacementUserId,
        HrIdentityWorkflowIssueType unavailableIssueType,
        Guid? actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!replacementUserId.HasValue)
        {
            var created = await CreateIssueIfMissingAsync(
                tenantId, employee, approval, unavailableIssueType, approval.ApproverId, null,
                $"{reason} No active tenant-safe identity is linked to the current HR manager.", actorUserId,
                cancellationToken);
            return new(false, created);
        }

        var eligibility = await ValidateReplacementAsync(tenantId, approval, replacementUserId.Value, cancellationToken);
        if (!eligibility.IsAllowed)
        {
            var created = await CreateIssueIfMissingAsync(
                tenantId, employee, approval, HrIdentityWorkflowIssueType.SegregationOfDutiesConflict,
                approval.ApproverId, replacementUserId, $"{reason} {eligibility.Reason}", actorUserId,
                cancellationToken);
            return new(false, created);
        }

        ReassignApproval(approval, replacementUserId.Value, actorUserId, reason, DateTime.UtcNow);
        AddWorkflowActivity(approval, actorUserId, "Approval reassigned from HR hierarchy", reason);
        return new(true, false);
    }

    private async Task<ReplacementDecision> ValidateReplacementAsync(
        Guid tenantId,
        WorkflowApproval approval,
        Guid replacementUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await (
            from user in _db.Users.IgnoreQueryFilters()
            join employee in HrEmployees() on user.EmployeeId equals employee.Id
            where user.Id == replacementUserId &&
                  user.TenantId == tenantId &&
                  user.IsActive &&
                  employee.TenantId == tenantId &&
                  !employee.IsDeleted
            select new { User = user, Employee = employee })
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate == null || !IsHrAccessEligible(candidate.Employee, DateTime.UtcNow))
        {
            return new(false, "The selected replacement is not an active HR-linked user in this tenant.");
        }

        if (approval.StepInstance.WorkflowInstance.InitiatedById == replacementUserId)
        {
            return new(false, "The workflow initiator cannot become their own approver.");
        }

        if (!string.IsNullOrWhiteSpace(approval.ApproverRole))
        {
            var hasRequiredRole = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == replacementUserId && role.Name == approval.ApproverRole
                select userRole.UserId)
                .AnyAsync(cancellationToken);
            if (!hasRequiredRole)
            {
                return new(false, $"The selected replacement does not hold the required role '{approval.ApproverRole}'.");
            }
        }

        var stepInstances = await _db.WorkflowStepInstances
            .AsNoTracking()
            .Include(item => item.WorkflowStep)
            .Where(item => item.WorkflowInstanceId == approval.StepInstance.WorkflowInstanceId)
            .ToListAsync(cancellationToken);
        var stepIds = stepInstances.Select(item => item.Id).ToArray();
        var workflowApprovals = await _db.WorkflowApprovals.AsNoTracking()
            .Where(item => stepIds.Contains(item.StepInstanceId))
            .ToListAsync(cancellationToken);
        var approvalsByStep = workflowApprovals
            .GroupBy(item => item.StepInstanceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<WorkflowApproval>)group.ToList());
        var previousSteps = stepInstances
            .Where(item => item.WorkflowStep.Order < approval.StepInstance.WorkflowStep.Order)
            .ToList();
        var config = DeserializeApprovalConfiguration(approval.StepInstance.WorkflowStep.Configuration);
        var context = DeserializeWorkflowContext(approval.StepInstance.WorkflowInstance.DataContext);
        var conflicts = WorkflowCrossStepSodEvaluator.Validate(
            config, previousSteps, approvalsByStep, context, replacementUserId);
        return conflicts.Count > 0
            ? new(false, string.Join(" ", conflicts))
            : new(true, string.Empty);
    }

    private async Task<bool> CreateIssueIfMissingAsync(
        Guid tenantId,
        Employee employee,
        WorkflowApproval approval,
        HrIdentityWorkflowIssueType issueType,
        Guid? staleAssigneeId,
        Guid? suggestedReplacementUserId,
        string reason,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.HrIdentityWorkflowIssues.IgnoreQueryFilters()
            .AnyAsync(item =>
                !item.IsDeleted && item.TenantId == tenantId &&
                item.WorkflowApprovalId == approval.Id && item.IssueType == issueType &&
                item.Status == HrIdentityWorkflowIssueStatus.Open,
                cancellationToken);
        if (exists)
        {
            return false;
        }

        _db.HrIdentityWorkflowIssues.Add(new HrIdentityWorkflowIssue
        {
            TenantId = tenantId,
            UserId = await ResolveAnyUserForEmployeeAsync(tenantId, employee.Id, cancellationToken)
                     ?? approval.StepInstance.WorkflowInstance.InitiatedById,
            EmployeeId = employee.Id,
            WorkflowInstanceId = approval.StepInstance.WorkflowInstanceId,
            WorkflowStepInstanceId = approval.StepInstanceId,
            WorkflowApprovalId = approval.Id,
            IssueType = issueType,
            Status = HrIdentityWorkflowIssueStatus.Open,
            StaleAssigneeId = staleAssigneeId,
            SuggestedReplacementUserId = suggestedReplacementUserId,
            Reason = Truncate(reason, 2000),
            DetectedAtUtc = DateTime.UtcNow,
            CreatedById = actorUserId,
            CreatedBy = actorUserId?.ToString() ?? "System"
        });
        return true;
    }

    private static void ReassignApproval(
        WorkflowApproval approval,
        Guid replacementUserId,
        Guid? actorUserId,
        string reason,
        DateTime now)
    {
        var staleAssignee = approval.ApproverId;
        approval.OriginalApproverId ??= staleAssignee;
        approval.ApproverId = replacementUserId;
        approval.DelegatedById = actorUserId;
        approval.DelegatedAt = now;
        approval.DelegationReason = Truncate(reason, 1000);
        approval.UpdatedAt = now;
        approval.LastModifiedById = actorUserId;
        if (approval.StepInstance.AssignedToId == staleAssignee)
        {
            approval.StepInstance.AssignedToId = replacementUserId;
            approval.StepInstance.UpdatedAt = now;
            approval.StepInstance.LastModifiedById = actorUserId;
        }
    }

    private void AddWorkflowActivity(
        WorkflowApproval approval,
        Guid? actorUserId,
        string title,
        string description)
    {
        _db.WorkflowActivityLogs.Add(new WorkflowActivityLog
        {
            TenantId = approval.TenantId,
            WorkflowInstanceId = approval.StepInstance.WorkflowInstanceId,
            StepInstanceId = approval.StepInstanceId,
            PerformedById = actorUserId,
            ActivityType = WorkflowActivityType.StepReassigned,
            Title = title,
            Description = Truncate(description, 2000),
            Data = JsonSerializer.Serialize(new
            {
                approvalId = approval.Id,
                originalApproverId = approval.OriginalApproverId,
                replacementUserId = approval.ApproverId,
                source = "HR_IDENTITY_RECONCILIATION"
            }, JsonOptions),
            ActivityDate = DateTime.UtcNow,
            CreatedById = actorUserId,
            CreatedBy = actorUserId?.ToString() ?? "System"
        });
    }

    private void AddAuditLog(
        Guid tenantId,
        Guid actorUserId,
        string action,
        string resource,
        Guid resourceId,
        object? oldValues,
        object? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = actorUserId,
            Username = actorUserId.ToString(),
            Action = action,
            Resource = resource,
            ResourceId = resourceId.ToString(),
            OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            IpAddress = "System",
            UserAgent = "HR/Identity reconciliation",
            Timestamp = DateTime.UtcNow,
            CreatedById = actorUserId,
            CreatedBy = actorUserId.ToString()
        });
    }

    private async Task<Guid?> ResolveEligibleUserForEmployeeAsync(
        Guid tenantId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from user in _db.Users.IgnoreQueryFilters().AsNoTracking()
            join employee in HrEmployees()
                on user.EmployeeId equals employee.Id
            where user.TenantId == tenantId && user.EmployeeId == employeeId && user.IsActive &&
                  employee.TenantId == tenantId && !employee.IsDeleted
            select new { user.Id, Employee = employee })
            .SingleOrDefaultAsync(cancellationToken);
        return row != null && IsHrAccessEligible(row.Employee, DateTime.UtcNow) ? row.Id : null;
    }

    private Task<Guid?> ResolveAnyUserForEmployeeAsync(
        Guid tenantId,
        Guid employeeId,
        CancellationToken cancellationToken)
        => _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.EmployeeId == employeeId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Dictionary<Guid, ApplicationUser>> LoadUsersAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancellationToken)
    {
        var values = ids.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        return await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(item => values.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private static bool IsHrAccessEligible(Employee employee, DateTime now)
    {
        return employee.IsActive &&
               EligibleStatuses.Contains(employee.StaffStatus) &&
               (!employee.TerminationDate.HasValue || employee.TerminationDate.Value > now);
    }

    private static string Fingerprint(
        Employee employee,
        bool eligible,
        Guid? managerUserId,
        IReadOnlyCollection<string> roles)
    {
        var source = string.Join("|",
            employee.Id,
            employee.TenantId,
            employee.IsActive,
            (int)employee.StaffStatus,
            employee.TerminationDate?.ToUniversalTime().ToString("O") ?? string.Empty,
            employee.DepartmentId,
            employee.ManagerId,
            managerUserId,
            eligible,
            string.Join(",", roles.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private IQueryable<Employee> HrEmployees()
        => _db.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(item => new Employee
            {
                Id = item.Id,
                TenantId = item.TenantId,
                IsDeleted = item.IsDeleted,
                IsActive = item.IsActive,
                StaffStatus = item.StaffStatus,
                TerminationDate = item.TerminationDate,
                DepartmentId = item.DepartmentId,
                ManagerId = item.ManagerId,
                EmployeeNumber = item.EmployeeNumber,
                FirstName = item.FirstName,
                LastName = item.LastName
            });

    private static string NormalizeIdempotencyKey(string? value, HrIdentityReconciliationTrigger trigger)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? $"{trigger.ToString().ToLowerInvariant()}:{Guid.NewGuid():N}"
            : value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(value));
        }
        return normalized;
    }

    private static HashSet<Guid> DeserializeIds(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<Guid[]>(json, JsonOptions) ?? []).ToHashSet();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static WorkflowApprovalConfigDto? DeserializeApprovalConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try { return JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyDictionary<string, object> DeserializeWorkflowContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions)
                   ?? new Dictionary<string, object>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>();
        }
    }

    private static string AppendNote(string? existing, string value)
        => string.IsNullOrWhiteSpace(existing) ? value : $"{existing.Trim()} | {value}";

    private static string BuildSummary(
        bool eligible,
        bool accessChanged,
        bool departmentChanged,
        bool managerChanged,
        bool roleChanged,
        int sessions,
        int refreshTokens,
        int responsibilities,
        int reassignments,
        int issues,
        bool reactivationReview)
    {
        var changes = new List<string>
        {
            eligible ? "HR access eligible" : "HR access ineligible"
        };
        if (accessChanged)
        {
            changes.Add("access state changed");
        }

        if (departmentChanged)
        {
            changes.Add("department changed");
        }

        if (managerChanged)
        {
            changes.Add("manager changed");
        }

        if (roleChanged)
        {
            changes.Add("Identity role snapshot changed (roles were not mutated)");
        }

        if (sessions > 0)
        {
            changes.Add($"{sessions} session(s) revoked");
        }

        if (refreshTokens > 0)
        {
            changes.Add($"{refreshTokens} refresh token(s) revoked");
        }

        if (responsibilities > 0)
        {
            changes.Add($"{responsibilities} procurement responsibility assignment(s) suspended");
        }

        if (reassignments > 0)
        {
            changes.Add($"{reassignments} workflow approval(s) reassigned");
        }

        if (issues > 0)
        {
            changes.Add($"{issues} workflow review issue(s) opened");
        }

        if (reactivationReview)
        {
            changes.Add("manual Identity reactivation review required");
        }

        return Truncate(string.Join("; ", changes) + ".", 2000);
    }

    private static HrIdentityReconciliationRunDto MapRun(HrIdentityReconciliationRun run)
        => new(run.Id, run.IdempotencyKey, run.Trigger.ToString(), run.Status.ToString(), run.RequestedById,
            run.StartedAtUtc, run.CompletedAtUtc, run.CandidateCount, run.ReconciledCount,
            run.ReviewRequiredCount, run.FailedCount, run.Error);

    private static HrIdentityReconciliationStateDto MapState(
        HrIdentityReconciliationState state,
        IReadOnlyDictionary<Guid, ApplicationUser> users,
        IReadOnlyDictionary<Guid, Employee> employees,
        IReadOnlyDictionary<Guid, string> departments)
    {
        users.TryGetValue(state.UserId, out var user);
        employees.TryGetValue(state.EmployeeId, out var employee);
        Employee? manager = null;
        if (state.ManagerEmployeeId.HasValue)
        {
            employees.TryGetValue(state.ManagerEmployeeId.Value, out manager);
        }

        var roles = DeserializeStrings(state.RoleNamesJson);
        return new HrIdentityReconciliationStateDto(
            state.UserId,
            DisplayName(user),
            user?.UserName,
            state.EmployeeId,
            employee?.EmployeeNumber ?? string.Empty,
            user?.IsActive ?? false,
            state.HrAccessEligible,
            state.AccessSuspendedByReconciliation,
            state.ReactivationReviewRequired,
            employee?.StaffStatus.ToString() ?? "Unavailable",
            state.DepartmentId,
            state.DepartmentId.HasValue && departments.TryGetValue(state.DepartmentId.Value, out var departmentName)
                ? departmentName : null,
            state.ManagerEmployeeId,
            manager == null ? null : $"{manager.FirstName} {manager.LastName}".Trim(),
            state.ManagerUserId,
            roles,
            state.LastObservedAtUtc,
            state.LastReconciledAtUtc,
            state.ReviewReason);
    }

    private static HrIdentityWorkflowIssueDto MapIssue(
        HrIdentityWorkflowIssue issue,
        IReadOnlyDictionary<Guid, ApplicationUser> users,
        IReadOnlyDictionary<Guid, Employee> employees)
    {
        users.TryGetValue(issue.UserId, out var user);
        users.TryGetValue(issue.StaleAssigneeId ?? Guid.Empty, out var stale);
        users.TryGetValue(issue.SuggestedReplacementUserId ?? Guid.Empty, out var suggested);
        users.TryGetValue(issue.ReplacementUserId ?? Guid.Empty, out var replacement);
        employees.TryGetValue(issue.EmployeeId, out var employee);
        return new HrIdentityWorkflowIssueDto(
            issue.Id, issue.UserId, DisplayName(user), issue.EmployeeId, employee?.EmployeeNumber ?? string.Empty,
            issue.WorkflowInstanceId, issue.WorkflowStepInstanceId, issue.WorkflowApprovalId,
            issue.IssueType.ToString(), issue.Status.ToString(), issue.StaleAssigneeId, DisplayNameOrNull(stale),
            issue.SuggestedReplacementUserId, DisplayNameOrNull(suggested), issue.Reason, issue.DetectedAtUtc,
            issue.ReplacementUserId, DisplayNameOrNull(replacement), issue.ResolvedAtUtc, issue.ResolutionNote);
    }

    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string DisplayName(ApplicationUser? user)
        => user == null ? "Unavailable identity" :
            string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim())
                ? user.UserName ?? user.Id.ToString()
                : $"{user.FirstName} {user.LastName}".Trim();

    private static string? DisplayNameOrNull(ApplicationUser? user) => user == null ? null : DisplayName(user);

    private static void RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("A tenant context is required.");
        }
    }

    private static void RequireActor(Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated actor is required.");
        }
    }

    private static void RequireReviewNote(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 10 || value.Trim().Length > 2000)
        {
            throw new ArgumentException("A review note between 10 and 2,000 characters is required.", parameterName);
        }
    }

    private static string Truncate(string? value, int maximumLength)
        => string.IsNullOrEmpty(value) || value.Length <= maximumLength ? value ?? string.Empty : value[..maximumLength];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record Candidate(Guid UserId, Guid EmployeeId);
    private sealed record ReassignmentOutcome(bool Reassigned, bool IssueCreated);
    private sealed record ReplacementDecision(bool IsAllowed, string Reason);
}
