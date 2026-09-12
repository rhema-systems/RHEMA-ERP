using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

public abstract class AttendanceControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected AttendanceControllerBase(ICurrentUserService currentUser)
    {
        CurrentUser = currentUser;
    }

    /// <summary>
    /// The caller satisfies the given policy — evaluated through the policy pipeline, so
    /// database grants and the HR role-fallback both count. Resolved from the request rather
    /// than the constructor so the two dozen derived controllers keep their signatures (W3).
    /// </summary>
    protected async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// Self-or-permission (W3): the caller is the employee the record is about, or holds the
    /// given policy. Deliberately not self-or-manager — a supervisor's part in attendance is
    /// approval, which reaches them through the workflow engine's own assignee check.
    /// </summary>
    protected async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (CurrentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return await HoldsPolicyAsync(policy);
    }

    protected ActionResult? TryGetTenantAndEmployee(out Guid tenantId, out Guid employeeId)
    {
        tenantId = default;
        employeeId = default;

        if (CurrentUser.TenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        if (CurrentUser.EmployeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        tenantId = CurrentUser.TenantId.Value;
        employeeId = CurrentUser.EmployeeId.Value;
        return null;
    }

    protected ActionResult? TryGetEmployee(out Guid employeeId)
    {
        employeeId = default;

        if (CurrentUser.EmployeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        employeeId = CurrentUser.EmployeeId.Value;
        return null;
    }

    protected ActionResult? TryGetTenant(out Guid tenantId)
    {
        tenantId = default;

        if (CurrentUser.TenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        tenantId = CurrentUser.TenantId.Value;
        return null;
    }
}
