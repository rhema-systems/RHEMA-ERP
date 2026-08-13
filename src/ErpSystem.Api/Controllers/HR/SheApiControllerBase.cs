using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Shared base for the SHE (Safety, Health &amp; Environment) controllers. Exposes the
/// current tenant and acting-employee ids; both throw an <see cref="UnauthorizedAccessException"/>
/// (mapped to HTTP 403 with the exception's own message, identically by the global exception
/// middleware and by <c>SafetyBusinessRulesAttribute</c>) when the context cannot be resolved —
/// an unresolvable tenant or an employee-unlinked login is an entitlement failure, not a
/// business-rule violation, and the mapping must not depend on whether the controller carries
/// the filter.
/// </summary>
public abstract class SheApiControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected SheApiControllerBase(ICurrentUserService currentUser) => CurrentUser = currentUser;

    protected Guid TenantId => CurrentUser.TenantId
        ?? throw new UnauthorizedAccessException("Tenant context could not be resolved.");

    protected Guid UserId => CurrentUser.EmployeeId
        ?? throw new UnauthorizedAccessException("Your user account is not linked to an employee record. Please contact your administrator.");
}
