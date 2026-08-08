using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The entitlement test for a performance improvement plan, shared by every controller that
/// touches one.
///
/// <para>A PIP names an employee who has been told their performance is not good enough. It is
/// readable by HR, by that employee, by the named supervisor and by the named HR owner — and by
/// nobody else in the tenant. It is writable by everyone on that list except the employee, whose
/// only write is their own comment on a review meeting.</para>
///
/// <para>Kept here rather than duplicated per controller so the three PIP controllers cannot drift
/// apart on who may see what.</para>
/// </summary>
internal static class PipAccess
{
    internal static bool IsHr(ControllerBase controller) =>
        controller.User.IsInRole(Constants.Roles.SuperAdmin) ||
        controller.User.IsInRole(Constants.Roles.Hr);

    /// <summary>HR, the subject employee, the supervisor or the HR owner.</summary>
    internal static Task<bool> CanAccessAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        CancellationToken ct = default)
        => MatchesAsync(controller, db, currentUser, pipId, includeSubject: true, ct);

    /// <summary>As <see cref="CanAccessAsync"/>, minus the subject employee — they read, they do not edit.</summary>
    internal static Task<bool> CanManageAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        CancellationToken ct = default)
        => MatchesAsync(controller, db, currentUser, pipId, includeSubject: false, ct);

    private static async Task<bool> MatchesAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        bool includeSubject,
        CancellationToken ct)
    {
        if (IsHr(controller)) return true;
        if (currentUser.EmployeeId is not Guid me) return false;
        if (currentUser.TenantId is not Guid tenantId) return false;

        return await db.Set<PerformanceImprovementPlan>()
            .AsNoTracking()
            .AnyAsync(p => p.Id == pipId
                        && p.TenantId == tenantId
                        && (p.SupervisorId == me
                            || p.HROwnerId == me
                            || (includeSubject && p.EmployeeId == me)), ct);
    }
}
