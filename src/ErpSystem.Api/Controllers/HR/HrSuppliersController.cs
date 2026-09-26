using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR's read-only window onto Procurement's supplier master — the payee of a recruitment cost.
/// </summary>
/// <remarks>
/// <para><b>Why this exists</b> (round 2b, lane R7). A recruitment cost — an agency fee, an
/// advert, a test venue, a medical — is paid to somebody, and until R7 that somebody was a
/// free-text <c>Purpose</c>. The payee is a Procurement <c>Supplier</c>, the same master the six
/// travel booking entities already point at (<c>VendorId</c>), and the one a Finance payable will
/// need when the AP hand-off (R8) is agreed. HR reads it; HR never creates a supplier.</para>
///
/// <para><b>Why not <c>api/Suppliers</c>.</b> Procurement's own list answers <b>400 "The operation
/// is not valid for the current state of the object"</b> to every caller, admin included
/// (measured 2026-09-10; recorded for the Procurement owner in
/// <c>docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md</c>). Even when that is fixed, this projection
/// is the same shape and reason as <c>HrCurrenciesController</c> and
/// <c>HrFinanceAccountsController</c>: another module owns what a supplier IS; HR owns which one
/// a cost is paid to, and hands its screens id, code, name and active — nothing else.</para>
/// </remarks>
[ApiController]
[Route("api/hr/suppliers")]
[Authorize(Policy = "InternalOnly")]
public class HrSuppliersController : ControllerBase
{
    private readonly ISupplierRepository _suppliers;
    private readonly ICurrentUserProvider _currentUser;

    public HrSuppliersController(ISupplierRepository suppliers, ICurrentUserProvider currentUser)
    {
        _suppliers = suppliers;
        _currentUser = currentUser;
    }

    /// <summary>Suppliers a picker can offer, optionally searched by code or name. Active only unless asked.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HrSupplierOption>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HrSupplierOption>>> Search(
        [FromQuery] string? search = null,
        [FromQuery] int take = 50,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var capped = take < 1 ? 50 : Math.Min(take, 200);
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var query = _suppliers.GetQueryable().AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted);
        if (!includeInactive) query = query.Where(s => s.IsActive);
        if (term != null) query = query.Where(s => s.Name.Contains(term) || s.SupplierCode.Contains(term));

        var rows = await query.OrderBy(s => s.Name).Take(capped)
            .Select(s => new { s.Id, s.SupplierCode, s.Name, s.IsActive })
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(s => new HrSupplierOption(s.Id, s.SupplierCode, s.Name, s.IsActive)).ToList());
    }

    /// <summary>One supplier by id — what a picker needs to name the payee a cost ALREADY has, active or not.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HrSupplierOption), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HrSupplierOption>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var s = await _suppliers.GetQueryable().AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.Id, x.SupplierCode, x.Name, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return s is null
            ? NotFound(new { message = $"No supplier was found with ID '{id}'." })
            : Ok(new HrSupplierOption(s.Id, s.SupplierCode, s.Name, s.IsActive));
    }
}

/// <param name="Id">What HR stores. The name is snapshotted onto the cost at save time.</param>
/// <param name="Code">Procurement's supplier code.</param>
/// <param name="Name">What a picker shows.</param>
/// <param name="IsActive">HR refuses to newly name an inactive supplier but shows one already named.</param>
public record HrSupplierOption(Guid Id, string Code, string Name, bool IsActive);
