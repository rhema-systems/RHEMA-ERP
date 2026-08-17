using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

public abstract class MedicalControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected MedicalControllerBase(ICurrentUserService currentUser)
    {
        CurrentUser = currentUser;
    }

    protected ActionResult? TryGetWriteContext(out Guid tenantId, out Guid userId)
    {
        tenantId = default;
        userId = default;

        if (CurrentUser.TenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        if (string.IsNullOrWhiteSpace(CurrentUser.UserId) || !Guid.TryParse(CurrentUser.UserId, out userId))
            return BadRequest("User context could not be resolved.");

        tenantId = CurrentUser.TenantId.Value;
        return null;
    }

    /// <summary>
    /// Resolves tenant, user, and the authenticated employee record. <paramref name="userId"/> remains the
    /// platform user id (for audit fields); <paramref name="employeeId"/> is the linked employee id (for
    /// domain actor fields such as claim author/approver). The two are intentionally kept distinct.
    /// </summary>
    /// <param name="purpose">
    /// What the employee link is needed FOR, e.g. "Recording a claim approval". Only use this helper
    /// where a domain field genuinely stores an <c>Employee</c> id — an audit field takes the user id
    /// and should use <see cref="TryGetWriteContext"/> instead, which does not require the link at all.
    /// Naming the purpose matters because administrative accounts are frequently unlinked (18 of 1,609
    /// on the reference tenant, <c>admin</c> among them), and "your account is not linked" on its own
    /// does not tell the caller which part of what they just did needed it.
    /// </param>
    protected ActionResult? TryGetEmployeeWriteContext(
        out Guid tenantId, out Guid userId, out Guid employeeId, string? purpose = null)
    {
        employeeId = default;

        var error = TryGetWriteContext(out tenantId, out userId);
        if (error != null)
            return error;

        if (CurrentUser.EmployeeId == null)
            return BadRequest(purpose is null
                ? "Your user account is not linked to an employee record. Please contact your administrator."
                : $"{purpose} requires your user account to be linked to an employee record. Please contact your administrator.");

        employeeId = CurrentUser.EmployeeId.Value;
        return null;
    }
}
