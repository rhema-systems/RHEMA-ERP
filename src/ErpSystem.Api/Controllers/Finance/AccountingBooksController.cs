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

    [HttpGet("{id:guid}/delta-combined-report")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> GetDeltaCombinedReport(Guid id, [FromQuery] DateTime asOfDate, CancellationToken cancellationToken = default) =>
        Ok(await _service.GetDeltaCombinedReportAsync(id, asOfDate, cancellationToken));

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

    [HttpPost("{id:guid}/primary-replacement")]
    [Authorize(Policy = FinancePermissions.RequestAccountingBookTransitions)]
    public async Task<IActionResult> RequestPrimaryReplacement(Guid id, [FromBody] RequestPrimaryAccountingBookReplacementDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RequestPrimaryReplacementAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/primary-replacement/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> ApprovePrimaryReplacement(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApprovePrimaryReplacementAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/primary-replacement/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> RejectPrimaryReplacement(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectPrimaryReplacementAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/primary-replacement/reversal")]
    [Authorize(Policy = FinancePermissions.RequestAccountingBookTransitions)]
    public async Task<IActionResult> RequestPrimaryReplacementReversal(Guid id, [FromBody] RequestPrimaryAccountingBookReversalDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RequestPrimaryReplacementReversalAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/primary-replacement/reversal/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> ApprovePrimaryReplacementReversal(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApprovePrimaryReplacementReversalAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/primary-replacement/reversal/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookTransitions)]
    public async Task<IActionResult> RejectPrimaryReplacementReversal(Guid id, [FromBody] DecideAccountingBookTransitionDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectPrimaryReplacementReversalAsync(id, request, cancellationToken));
}
