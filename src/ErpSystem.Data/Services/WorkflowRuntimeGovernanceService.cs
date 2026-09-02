using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed class WorkflowRuntimeGovernanceService : IWorkflowRuntimeGovernanceService
{
    private readonly ApplicationDbContext _db;

    public WorkflowRuntimeGovernanceService(ApplicationDbContext db) => _db = db;

    public async Task EnsureMandatoryApprovalActorsAvailableAsync(
        Guid tenantId,
        Guid initiatedById,
        string workflowName,
        IReadOnlyCollection<WorkflowStep> steps,
        CancellationToken cancellationToken = default)
    {
        foreach (var step in steps
                     .Where(item => item.StepType == WorkflowStepType.Approval && item.IsRequired)
                     .OrderBy(item => item.Order))
        {
            var config = ReadStepConfiguration(step, workflowName);

            // A conditional skip/auto-approval or a runtime-derived assignee cannot be evaluated
            // truthfully until step entry. Do not turn this start-time safety check into a guess.
            if (config?.SkipCondition != null || config?.ApprovalConfig?.AutoApprovalCondition != null)
                continue;

            var approval = config?.ApprovalConfig;
            var rules = approval?.ApproverRules
                .Where(rule => rule.Condition == null)
                .ToList() ?? [];

            if (rules.Any(rule => rule.AssignmentType is
                    WorkflowAssignmentType.Dynamic or
                    WorkflowAssignmentType.RequestorManager or
                    WorkflowAssignmentType.PreviousStepUser))
            {
                continue;
            }

            if (rules.Count == 0 && !string.IsNullOrWhiteSpace(step.RequiredRole))
            {
                rules.Add(new WorkflowAssignmentRuleDto
                {
                    ApprovalGroup = 1,
                    AssignmentType = WorkflowAssignmentType.Role,
                    Role = step.RequiredRole
                });
            }

            // A step made entirely from conditional rules has no deterministic start-time actor
            // set. Step-entry resolution remains authoritative for that route.
            if (rules.Count == 0)
                continue;

            foreach (var group in rules.GroupBy(rule => Math.Max(rule.ApprovalGroup, 1)))
            {
                var eligibleUserIds = new HashSet<Guid>();
                var actorLabels = new List<string>();

                foreach (var rule in group)
                {
                    switch (rule.AssignmentType)
                    {
                        case WorkflowAssignmentType.User when rule.UserId.HasValue:
                            actorLabels.Add($"user {rule.UserId.Value}");
                            if (await IsActiveTenantUserAsync(tenantId, rule.UserId.Value, cancellationToken))
                                eligibleUserIds.Add(rule.UserId.Value);
                            break;

                        case WorkflowAssignmentType.Role when !string.IsNullOrWhiteSpace(rule.Role):
                            actorLabels.Add($"role '{rule.Role.Trim()}'");
                            eligibleUserIds.UnionWith(await GetActiveTenantRoleUsersAsync(
                                tenantId,
                                rule.Role,
                                cancellationToken));
                            break;
                    }
                }

                if (approval?.PreventInitiatorApproval == true)
                    eligibleUserIds.Remove(initiatedById);

                var requiredActors = approval?.RequireDistinctApprovers == true
                    ? Math.Max(approval.MinApprovalsRequired, 1)
                    : 1;

                if (eligibleUserIds.Count >= requiredActors)
                    continue;

                var actorDescription = actorLabels.Count == 0
                    ? "its configured assignment"
                    : string.Join(" or ", actorLabels.Distinct(StringComparer.OrdinalIgnoreCase));
                var independence = approval?.PreventInitiatorApproval == true
                    ? " independent"
                    : string.Empty;

                throw new InvalidOperationException(
                    $"Workflow '{workflowName}' cannot start because approval step '{step.Name}' " +
                    $"has no{independence} active user in this tenant eligible for {actorDescription}. " +
                    "Assign an eligible approver or submit with a different initiator.");
            }
        }
    }

    public async Task<WorkflowDelegationResolution?> ResolveDelegateAsync(Guid tenantId, Guid principalUserId,
        string? module, string? entityType, Guid? workflowDefinitionId, Guid? workflowStepId,
        decimal? amount, string? currencyCode, DateTime effectiveAt,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _db.WorkflowDelegations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.PrincipalUserId == principalUserId && item.EffectiveFrom <= effectiveAt &&
                item.EffectiveTo >= effectiveAt)
            .ToListAsync(cancellationToken);

        var match = WorkflowDelegationPolicy.SelectEffective(candidates, module, entityType,
            workflowDefinitionId, workflowStepId, amount, currencyCode);

        return match == null ? null : new WorkflowDelegationResolution(
            match.Id, match.PrincipalUserId, match.DelegateUserId, match.Reason, match.AllowRedelegation);
    }

    public async Task ValidateOneOffDelegationAsync(Guid tenantId, Guid principalUserId, Guid delegateUserId,
        bool allowRedelegation, CancellationToken cancellationToken = default)
    {
        if (principalUserId == delegateUserId)
            throw new InvalidOperationException("An approval cannot be delegated to the same user.");

        var delegateExists = await _db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == delegateUserId && user.TenantId == tenantId && user.IsActive, cancellationToken);
        if (!delegateExists)
            throw new InvalidOperationException("The delegate must be an active user in the current tenant.");

        var createsCycle = await _db.WorkflowDelegations.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
            item.PrincipalUserId == delegateUserId && item.DelegateUserId == principalUserId &&
            item.EffectiveFrom <= DateTime.UtcNow && item.EffectiveTo >= DateTime.UtcNow, cancellationToken);
        if (createsCycle)
            throw new InvalidOperationException("This delegation would create an active delegation cycle.");

        if (!allowRedelegation)
        {
            var principalIsDelegate = await _db.WorkflowDelegations.AsNoTracking().AnyAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.IsActive &&
                item.DelegateUserId == principalUserId && !item.AllowRedelegation &&
                item.EffectiveFrom <= DateTime.UtcNow && item.EffectiveTo >= DateTime.UtcNow, cancellationToken);
            if (principalIsDelegate)
                throw new InvalidOperationException("The delegated authority does not permit re-delegation.");
        }
    }

    public async Task<DateTime?> CalculateDueDateAsync(Guid tenantId, DateTime startedAtUtc,
        double? workingHours, CancellationToken cancellationToken = default)
    {
        if (!workingHours.HasValue || workingHours <= 0) return null;

        var calendar = await _db.WorkflowWorkingCalendars.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (calendar == null) return startedAtUtc.AddHours(workingHours.Value);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        var holidays = ParseHolidays(calendar.HolidaysJson);
        return WorkflowDelegationPolicy.CalculateDueDate(startedAtUtc, workingHours.Value, zone,
            calendar.WorkingDaysMask, calendar.WorkDayStart, calendar.WorkDayEnd, holidays);
    }

    private static HashSet<DateOnly> ParseHolidays(string json)
    {
        try { return (JsonSerializer.Deserialize<List<DateOnly>>(json) ?? []).ToHashSet(); }
        catch (JsonException) { return []; }
    }

    private static WorkflowStepConfigurationDto? ReadStepConfiguration(
        WorkflowStep step,
        string workflowName)
    {
        if (string.IsNullOrWhiteSpace(step.Configuration))
            return null;

        try
        {
            // Workflow definitions are authored with the same string-enum JSON contract used by
            // WorkflowEngine and the seeders. Keep the start-time governance reader on that
            // canonical contract so it cannot reject a route that the engine itself can execute.
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(
                step.Configuration,
                options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Workflow '{workflowName}' approval step '{step.Name}' has invalid configuration.",
                exception);
        }
    }

    private Task<bool> IsActiveTenantUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return _db.Users.AsNoTracking().AnyAsync(user =>
                user.Id == userId &&
                user.IsActive &&
                (user.TenantId == tenantId || user.UserTenants.Any(link =>
                    link.TenantId == tenantId &&
                    !link.IsDeleted &&
                    link.Status == UserTenantStatus.Active &&
                    (!link.ExpiresAt.HasValue || link.ExpiresAt > now))),
            cancellationToken);
    }

    private async Task<List<Guid>> GetActiveTenantRoleUsersAsync(
        Guid tenantId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var normalizedRoleName = roleName.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        return await _db.UserRoles.AsNoTracking()
            .Where(link => _db.Roles.Any(role =>
                role.Id == link.RoleId &&
                (role.NormalizedName == normalizedRoleName || role.Name!.ToUpper() == normalizedRoleName)))
            .Where(link => _db.Users.Any(user =>
                user.Id == link.UserId &&
                user.IsActive &&
                (user.TenantId == tenantId || user.UserTenants.Any(membership =>
                    membership.TenantId == tenantId &&
                    !membership.IsDeleted &&
                    membership.Status == UserTenantStatus.Active &&
                    (!membership.ExpiresAt.HasValue || membership.ExpiresAt > now)))))
            .Select(link => link.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

}
