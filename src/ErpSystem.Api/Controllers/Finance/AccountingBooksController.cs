using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounting-books")]
public sealed class AccountingBooksController : ControllerBase
{
    private readonly IAccountingBookService _service;
    public AccountingBooksController(IAccountingBookService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> GetBooks([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetBooksAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> GetBook(Guid id, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetBookAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageAccountingBooks)]
    public async Task<IActionResult> Create([FromBody] CreateAccountingBookDto request, CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBooks)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountingBookDto request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/transitions")]
    [Authorize(Policy = FinancePermissions.RequestAccountingBookTransitions)]
    public async Task<IActionResult> RequestTransition(Guid id, [FromBody] RequestAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RequestTransitionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/transitions/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> ApproveTransition(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApproveTransitionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/transitions/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> RejectTransition(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectTransitionAsync(id, request, cancellationToken));
}
