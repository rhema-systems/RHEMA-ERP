using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Route("api/finance/book-balances")]
[Authorize(Policy = FinancePermissions.ViewFinance)]
public sealed class BookBalanceController : ControllerBase
{
    private readonly IBookBalanceReadModelService _service;
    private readonly ICurrentUserService _currentUser;

    public BookBalanceController(IBookBalanceReadModelService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("accounts/{accountId:guid}")]
    public async Task<ActionResult<BookBalanceInquiryDto>> Get(Guid accountId,
        [FromQuery] string accountingBookCode, [FromQuery] Guid? fiscalPeriodId,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetAsync(_currentUser.GetRequiredFinanceTenantId(), accountId,
            accountingBookCode, fiscalPeriodId, cancellationToken));
}
