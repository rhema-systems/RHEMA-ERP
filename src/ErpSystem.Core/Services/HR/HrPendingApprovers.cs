using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who an entity's open approval is asking right now — the logins its workflow's current step is waiting on: the named
/// approvers of its lowest open approval group, and every active holder of a role that group names. Tenant-explicit, so
/// a nightly sweep with nobody signed in can ask.
/// </summary>
/// <remarks>
/// <para>Leave's reminder sweep resolves exactly this (<c>LeaveReminderService.ApproversAskedAsync</c>, round 5 lane I);
/// the travel closure's lane 8 (slice 8b) needs it for "approval waiting", so it is shared here, and leave's own copy is
/// left as it is — the shape lane 2 used for <see cref="HrLineAuthority"/>.</para>
///
/// <para>The step the engine calls current is the one the instance points at, else the newest open one
/// (<c>WorkflowStepInstanceRepository.GetCurrentStepAsync</c>); then its lowest open group. A role is resolved here, not
/// left to the publisher, because a sweep's message must not reach the person the request is about and the publisher
/// cannot exclude anyone.</para>
/// </remarks>
public static class HrPendingApprovers
{
    /// <summary>For each entity with an open approval, the users its current step is asking (user id and their employee).</summary>
    public static async Task<Dictionary<Guid, List<(Guid UserId, Guid? EmployeeId)>>> ForEntitiesAsync(
        IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, Guid tenantId,
        IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, List<(Guid UserId, Guid? EmployeeId)>>();
        if (entityIds.Count == 0) return result;
        var ids = entityIds.ToList();

        var rows = await unitOfWork.Repository<WorkflowApproval>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.Status == WorkflowApprovalStatus.Pending
                            && !a.StepInstance.IsDeleted
                            && (a.StepInstance.Status == WorkflowStepInstanceStatus.Pending
                                || a.StepInstance.Status == WorkflowStepInstanceStatus.InProgress)
                            && !a.StepInstance.WorkflowInstance.IsDeleted
                            && (a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Created
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Waiting
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Suspended)
                            && ids.Contains(a.StepInstance.WorkflowInstance.EntityId))
            .Select(a => new
            {
                EntityId = a.StepInstance.WorkflowInstance.EntityId,
                a.StepInstanceId,
                OnCurrentStep = a.StepInstance.WorkflowInstance.CurrentStepId == a.StepInstance.WorkflowStepId,
                StepCreatedAt = a.StepInstance.CreatedAt,
                a.ApproverId, a.ApproverRole, a.ApprovalGroup,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return result;

        var current = rows
            .GroupBy(r => r.EntityId)
            .Select(g =>
            {
                var step = g.OrderByDescending(r => r.OnCurrentStep).ThenByDescending(r => r.StepCreatedAt).First().StepInstanceId;
                var onStep = g.Where(r => r.StepInstanceId == step).ToList();
                var group = onStep.Min(r => r.ApprovalGroup);
                return (EntityId: g.Key, Rows: onStep.Where(r => r.ApprovalGroup == group).ToList());
            })
            .ToList();

        var roleHolders = new Dictionary<string, List<(Guid UserId, Guid? EmployeeId)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in current.SelectMany(c => c.Rows).Select(r => r.ApproverRole)
                     .Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            roleHolders[role] = (await userManager.GetUsersInRoleAsync(role))
                .Where(u => u.IsActive && u.TenantId == tenantId)
                .Select(u => (u.Id, u.EmployeeId))
                .ToList();
        }

        var namedIds = current.SelectMany(c => c.Rows)
            .Where(r => r.ApproverId is Guid id && id != Guid.Empty)
            .Select(r => r.ApproverId!.Value)
            .Distinct()
            .ToList();
        var named = namedIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : (await userManager.Users
                    .Where(u => namedIds.Contains(u.Id) && u.IsActive && u.TenantId == tenantId)
                    .Select(u => new { u.Id, u.EmployeeId })
                    .ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id, u => u.EmployeeId);

        foreach (var (entityId, stepRows) in current)
        {
            var users = new List<(Guid UserId, Guid? EmployeeId)>();
            foreach (var row in stepRows)
            {
                if (row.ApproverId is Guid userId && named.TryGetValue(userId, out var employeeId))
                    users.Add((userId, employeeId));
                if (!string.IsNullOrWhiteSpace(row.ApproverRole) && roleHolders.TryGetValue(row.ApproverRole.Trim(), out var holders))
                    users.AddRange(holders);
            }
            result[entityId] = users.DistinctBy(u => u.UserId).ToList();
        }

        return result;
    }
}
