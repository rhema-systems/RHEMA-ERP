using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Shared base for the SHE (Safety, Health &amp; Environment) controllers. Exposes the
/// current tenant and acting-employee ids; both throw an <see cref="InvalidOperationException"/>
/// (mapped to HTTP 400 by the global exception middleware) when the context cannot be resolved.
/// </summary>
public abstract class SheApiControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected SheApiControllerBase(ICurrentUserService currentUser) => CurrentUser = currentUser;

    protected Guid TenantId => CurrentUser.TenantId
        ?? throw new InvalidOperationException("Tenant context could not be resolved.");

    protected Guid UserId => CurrentUser.EmployeeId
        ?? throw new InvalidOperationException("Your user account is not linked to an employee record. Please contact your administrator.");
}
