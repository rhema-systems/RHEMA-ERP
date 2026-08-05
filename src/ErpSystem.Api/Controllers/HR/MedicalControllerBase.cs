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
    protected ActionResult? TryGetEmployeeWriteContext(out Guid tenantId, out Guid userId, out Guid employeeId)
    {
        employeeId = default;

        var error = TryGetWriteContext(out tenantId, out userId);
        if (error != null)
            return error;

        if (CurrentUser.EmployeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        employeeId = CurrentUser.EmployeeId.Value;
        return null;
    }
}
