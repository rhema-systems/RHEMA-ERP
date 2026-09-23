using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounting-book-applicability")]
public sealed class AccountingBookApplicabilityController(IAccountingBookApplicabilityService service) : ControllerBase
{
    [HttpPost("resolve")]
    [Authorize(Policy = FinancePermissions.ResolveAccountingBookApplicability)]
    public async Task<IActionResult> Resolve([FromBody] ResolveAccountingBookApplicabilityDto request, CancellationToken ct) => Ok(await service.ResolveAsync(request, ct));

    [HttpPost("selections/freeze")]
    [Authorize(Policy = FinancePermissions.ResolveAccountingBookApplicability)]
    public async Task<IActionResult> Freeze([FromBody] FreezeAccountingBookSelectionDto request, CancellationToken ct) => Ok(await service.FreezeAsync(request, ct));
}
