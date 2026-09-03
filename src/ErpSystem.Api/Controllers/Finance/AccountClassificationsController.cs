using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/account-classifications")]
public sealed class AccountClassificationsController : ControllerBase
{
    private readonly IAccountClassificationService _service;
    public AccountClassificationsController(IAccountClassificationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? accountingBookId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetAsync(accountingBookId, includeInactive, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveAccountClassificationDto request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveAccountClassificationDto request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/retire")]
    public async Task<IActionResult> Retire(Guid id, [FromBody] RetireAccountClassificationDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RetireAsync(id, request, cancellationToken));
}
