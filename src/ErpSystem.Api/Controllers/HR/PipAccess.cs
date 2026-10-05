using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
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
    /// <summary>
    /// W3: whether the caller holds the given performance policy. The HR and SuperAdmin roles
    /// keep passing through the role-fallback handler; a seeded permission holder counts too.
    /// </summary>
    internal static async Task<bool> HoldsPolicyAsync(ControllerBase controller, string policy)
    {
        var authorization = controller.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(controller.User, policy)).Succeeded;
    }

    /// <summary>The subject employee, the supervisor, the HR owner, or a performance-Read holder.</summary>
    internal static Task<bool> CanAccessAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        CancellationToken ct = default)
        => MatchesAsync(controller, db, currentUser, pipId, includeSubject: true,
            HrPermissions.PerformanceReadPolicy, ct);

    /// <summary>As <see cref="CanAccessAsync"/>, minus the subject employee — they read, they do
    /// not edit — and at the Write tier for the desk.</summary>
    internal static Task<bool> CanManageAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        CancellationToken ct = default)
        => MatchesAsync(controller, db, currentUser, pipId, includeSubject: false,
            HrPermissions.PerformanceWritePolicy, ct);

    /// <remarks>
    /// Performance closure P4, the subject first: the employee a plan is about is its subject
    /// whatever else they hold — an HR officer included (the two-actor rule). They read it once it
    /// is in force (a draft, or one out for approval, is not yet theirs to see —
    /// <see cref="PipStatus.Draft"/>'s own contract) and never manage it. The policy test used to
    /// come first, so an HR officer on a plan could edit, submit and close their own.
    /// </remarks>
    private static async Task<bool> MatchesAsync(
        ControllerBase controller,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        Guid pipId,
        bool includeSubject,
        string policy,
        CancellationToken ct)
    {
        if (currentUser.TenantId is not Guid tenantId) return false;
        var me = currentUser.EmployeeId is Guid id && id != Guid.Empty ? id : (Guid?)null;

        var plan = await db.Set<PerformanceImprovementPlan>()
            .AsNoTracking()
            .Where(p => p.Id == pipId && p.TenantId == tenantId)
            .Select(p => new { p.EmployeeId, p.SupervisorId, p.HROwnerId, p.Status })
            .FirstOrDefaultAsync(ct);

        // An unknown id falls to the desk, so the action reports it missing rather than forbidden.
        if (plan is null) return await HoldsPolicyAsync(controller, policy);

        if (me is Guid subject && plan.EmployeeId == subject)
            return includeSubject && IsInForceForSubject(plan.Status);

        if (await HoldsPolicyAsync(controller, policy)) return true;
        return me is Guid party && (plan.SupervisorId == party || plan.HROwnerId == party);
    }

    /// <summary>
    /// Decision D-75: whether the caller is the plan's subject. The subject — an HR officer included —
    /// does not decide, close or delete their own plan: approve, reject, the outcome and delete reach
    /// the service past <see cref="CanManageAsync"/> (their authority is the workflow's or the desk
    /// policy's), so the desk test let an HR officer on a plan record their own outcome.
    /// </summary>
    internal static async Task<bool> IsSubjectAsync(
        ApplicationDbContext db, ICurrentUserService currentUser, Guid pipId, CancellationToken ct = default)
    {
        if (currentUser.TenantId is not Guid tenantId) return false;
        if (currentUser.EmployeeId is not Guid me || me == Guid.Empty) return false;

        return await db.Set<PerformanceImprovementPlan>()
            .AsNoTracking()
            .AnyAsync(p => p.Id == pipId && p.TenantId == tenantId && p.EmployeeId == me, ct);
    }

    /// <summary>
    /// Whether the plan's subject may see it: once it has left Draft and approval. Shared by the
    /// single-plan gate and the subject's own lists.
    /// </summary>
    internal static bool IsInForceForSubject(PipStatus status) =>
        status is not (PipStatus.Draft or PipStatus.PendingApproval);

    /// <summary>
    /// Whether an employee's own login holds the performance desk at the Write tier (performance
    /// closure P4): an active account in the tenant whose roles grant Maintain or Administer
    /// Performance, by the role fallback or a seeded role permission. A plan's HR owner reads and
    /// manages it as a party, so naming anyone as HR owner used to hand them the plan; now it can
    /// only name someone who could already reach it.
    /// </summary>
    internal static async Task<bool> EmployeeHoldsDeskAsync(
        ApplicationDbContext db, Guid tenantId, Guid employeeId, CancellationToken ct = default)
    {
        var desk = new[] { HrPermissions.MaintainPerformance, HrPermissions.AdministerPerformance };
        var deskUpper = desk.Select(p => p.ToUpperInvariant()).ToArray();

        var userIds = await db.Users
            .AsNoTracking()
            .Where(u => u.EmployeeId == employeeId && u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (userIds.Count == 0) return false;

        // The same walk PermissionAuthorizationHandler makes for a signed-in user.
        var roles = await db.UserRoles
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Select(ur => new
            {
                ur.Role.Name,
                Seeded = ur.Role.RolePermissions.Any(rp => deskUpper.Contains(rp.Permission.Name.ToUpper())),
            })
            .ToListAsync(ct);

        if (roles.Any(r => r.Seeded)) return true;
        var names = roles.Select(r => r.Name).OfType<string>().ToList();
        return names.Contains(Constants.Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            || HrPermissions.RolesGrantAny(names, desk);
    }
}
