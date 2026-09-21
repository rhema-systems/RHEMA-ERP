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
    [HttpGet("policies")]
    [Authorize(Policy = FinancePermissions.ViewAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> GetPolicies(CancellationToken ct) => Ok(await service.GetPoliciesAsync(ct));

    [HttpGet("eligible-books")]
    [Authorize(Policy = FinancePermissions.ViewAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> GetEligibleBooks(CancellationToken ct) => Ok(await service.GetEligibleBooksAsync(ct));

    [HttpGet("posting-identities")]
    [Authorize(Policy = FinancePermissions.ViewAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> GetPostingIdentities(CancellationToken ct) => Ok(await service.GetPostingIdentitiesAsync(ct));

    [HttpPost("policies")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> CreateDraft([FromBody] SaveAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.CreateDraftAsync(request, ct));

    [HttpPut("policies/{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] SaveAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.UpdateDraftAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/submit")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.SubmitAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.ApproveAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.RejectAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/retire")]
    [Authorize(Policy = FinancePermissions.ManageAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> Retire(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.RetireAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/retire/approve")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> ApproveRetirement(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.ApproveRetirementAsync(id, request, ct));

    [HttpPost("policies/{id:guid}/retire/reject")]
    [Authorize(Policy = FinancePermissions.ApproveAccountingBookApplicabilityPolicy)]
    public async Task<IActionResult> RejectRetirement(Guid id, [FromBody] DecideAccountingBookApplicabilityPolicyDto request, CancellationToken ct) => Ok(await service.RejectRetirementAsync(id, request, ct));

    [HttpPost("resolve")]
    [Authorize(Policy = FinancePermissions.ResolveAccountingBookApplicability)]
    public async Task<IActionResult> Resolve([FromBody] ResolveAccountingBookApplicabilityDto request, CancellationToken ct) => Ok(await service.ResolveAsync(request, ct));

    [HttpPost("selections/freeze")]
    [Authorize(Policy = FinancePermissions.ResolveAccountingBookApplicability)]
    public async Task<IActionResult> Freeze([FromBody] FreezeAccountingBookSelectionDto request, CancellationToken ct) => Ok(await service.FreezeAsync(request, ct));
}
