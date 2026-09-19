using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/accounts/{accountId:guid}/revaluation-policies")]
public sealed class AccountBookCurrencyPoliciesController : ControllerBase
{
    private readonly IAccountBookCurrencyPolicyService _service;
    public AccountBookCurrencyPoliciesController(IAccountBookCurrencyPolicyService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> Get(Guid accountId, CancellationToken cancellationToken) =>
        Ok(await _service.GetForAccountAsync(accountId, cancellationToken));

    [HttpPut("currency-links/{accountCurrencyLinkId:guid}/books/{accountingBookId:guid}")]
    [Authorize(Policy = FinancePermissions.OverrideFxRevaluationPolicy)]
    public async Task<IActionResult> Save(Guid accountId, Guid accountCurrencyLinkId, Guid accountingBookId,
        [FromBody] SaveAccountBookCurrencyPolicyDto request, CancellationToken cancellationToken) =>
        Ok(await _service.SaveAsync(accountId, accountCurrencyLinkId, accountingBookId, request, cancellationToken));

    [HttpPost("{policyId:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveFxRevaluationPolicy)]
    public async Task<IActionResult> Approve(Guid accountId, Guid policyId,
        [FromBody] DecideAccountBookCurrencyPolicyDto request, CancellationToken cancellationToken) =>
        Ok(await _service.ApproveAsync(accountId, policyId, request, cancellationToken));

    [HttpPost("{policyId:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveFxRevaluationPolicy)]
    public async Task<IActionResult> Reject(Guid accountId, Guid policyId,
        [FromBody] DecideAccountBookCurrencyPolicyDto request, CancellationToken cancellationToken) =>
        Ok(await _service.RejectAsync(accountId, policyId, request, cancellationToken));
}
