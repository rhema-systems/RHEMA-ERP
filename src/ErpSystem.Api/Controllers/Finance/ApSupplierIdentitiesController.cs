using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Read-only Finance settlement identity lookup. This endpoint never writes Finance's bridge and
/// never mutates Procurement's Business Partner or Supplier masters. Purpose-authorized AP
/// commands persist an unambiguous bridge as part of their own controlled transaction.
/// </summary>
[ApiController]
[Route("api/ap/supplier-identities")]
[Authorize]
public sealed class ApSupplierIdentitiesController : ControllerBase
{
    private readonly IApSupplierIdentityService _identity;

    public ApSupplierIdentitiesController(IApSupplierIdentityService identity) => _identity = identity;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    [ProducesResponseType(typeof(ApSupplierIdentityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApSupplierIdentityDto>> Lookup(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _identity.LookupAsync(id, cancellationToken));
}
