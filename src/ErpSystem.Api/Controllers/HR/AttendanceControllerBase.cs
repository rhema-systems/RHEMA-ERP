using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

public abstract class AttendanceControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected AttendanceControllerBase(ICurrentUserService currentUser)
    {
        CurrentUser = currentUser;
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
