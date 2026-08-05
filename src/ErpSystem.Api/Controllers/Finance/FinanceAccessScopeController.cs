using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Administers effective-dated Finance data scopes for the current tenant.
///
/// The permission policy on this controller controls who may administer grants. Runtime Finance
/// services separately enforce the resulting grants against operational records. This separation
/// is deliberate: an administrator should not gain operational access merely because they can
/// maintain access-control configuration.
/// </summary>
[ApiController]
[Authorize(Policy = FinancePermissions.ManageFinanceAccessScopes)]
[Route("api/finance/access-scopes")]
public sealed class FinanceAccessScopeController : ControllerBase
{
    private readonly IFinanceAccessScopeService _scopeService;

    public FinanceAccessScopeController(IFinanceAccessScopeService scopeService)
    {
        _scopeService = scopeService;
    }

    /// <summary>Lists Finance scope grants, optionally for one tenant user.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FinanceAccessScopeGrantDto>>> GetGrants(
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
        => Ok(await _scopeService.GetGrantsAsync(userId, cancellationToken));

    /// <summary>Returns active tenant users available for a Finance scope assignment.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<FinanceAccessUserOptionDto>>> GetUsers(
        CancellationToken cancellationToken)
        => Ok(await _scopeService.GetUsersAsync(cancellationToken));

    /// <summary>Returns tenant bank accounts available for bank-account restricted grants.</summary>
    [HttpGet("bank-accounts")]
    public async Task<ActionResult<IReadOnlyList<FinanceAccessBankAccountOptionDto>>> GetBankAccounts(
        CancellationToken cancellationToken)
        => Ok(await _scopeService.GetBankAccountsAsync(cancellationToken));

    /// <summary>Creates a new effective-dated Finance data-scope grant.</summary>
    [HttpPost]
    public async Task<ActionResult<FinanceAccessScopeGrantDto>> CreateGrant(
        [FromBody] SaveFinanceAccessScopeGrantDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _scopeService.SaveGrantAsync(null, dto, cancellationToken);
            return CreatedAtAction(nameof(GetGrants), new { userId = created.UserId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Updates a grant using optimistic concurrency.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FinanceAccessScopeGrantDto>> UpdateGrant(
        Guid id,
        [FromBody] SaveFinanceAccessScopeGrantDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scopeService.SaveGrantAsync(id, dto, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The Finance scope grant changed after it was loaded. Refresh and retry." });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Ends a grant without deleting its audit history. Scope records are never hard-deleted because
    /// historic access decisions must remain explainable during Finance and Internal Audit review.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateGrant(
        Guid id,
        [FromBody] DeactivateFinanceAccessScopeGrantDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            await _scopeService.DeactivateGrantAsync(id, dto.Reason, dto.RowVersion, cancellationToken);
            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The Finance scope grant changed after it was loaded. Refresh and retry." });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
