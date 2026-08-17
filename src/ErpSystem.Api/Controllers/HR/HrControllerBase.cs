using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Shared actor resolution for HR controllers.
/// </summary>
/// <remarks>
/// <para>Extracted from <see cref="MedicalControllerBase"/> when area 12 needed the same helpers.
/// Copying them would have been the third place in HR to re-derive the same distinction, and the
/// distinction is precisely the thing that keeps getting got wrong.</para>
///
/// <para><b>The distinction:</b> a platform <c>User</c> id belongs in audit fields
/// (<c>CreatedBy</c>, <c>UpdatedBy</c>); an <c>Employee</c> id belongs in domain actor fields
/// (approver, author, assessor). They are different identifiers for different things, and an
/// administrative account often has the first and not the second — 18 of 1,609 users on the
/// reference tenant are unlinked, <c>admin</c> among them.</para>
///
/// <para>Requiring an employee link for an audit field is therefore a refusal with no cause: it
/// turns away a caller because a field they were never going to populate is unavailable. Area 12
/// had 33 such refusals across its eight controllers, every one of them feeding a
/// <c>createdByUserId</c> or <c>updatedByUserId</c> parameter, and all 33 also wrote an Employee
/// id into a column meant for a User id.</para>
/// </remarks>
public abstract class HrControllerBase : ControllerBase
{
    protected readonly ICurrentUserService CurrentUser;

    protected HrControllerBase(ICurrentUserService currentUser)
    {
        CurrentUser = currentUser;
    }

    /// <summary>
    /// Resolves tenant and the platform user id. Use this for audit fields — it deliberately does
    /// not require an employee link.
    /// </summary>
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
    /// Naming the purpose matters because administrative accounts are frequently unlinked, and
    /// "your account is not linked" on its own does not tell the caller which part of what they just
    /// did needed it.
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
