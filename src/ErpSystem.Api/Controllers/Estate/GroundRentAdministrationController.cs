using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.DTOs.Estate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/ground-rent")]
[Authorize]
public sealed class GroundRentAdministrationController : ControllerBase
{
    private const string EstateRoles =
        "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer";

    private readonly IGroundRentAdministrationService _service;

    public GroundRentAdministrationController(IGroundRentAdministrationService service)
    {
        _service = service;
    }

    [HttpGet("options")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentOptionsDto>> GetOptions(CancellationToken cancellationToken)
        => Ok(await _service.GetOptionsAsync(cancellationToken));

    [HttpPost("assessments")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentAssetOptionDto>> AssessAsset(
        [FromBody] AssessEstateGroundRentDto request,
        CancellationToken cancellationToken)
        => Ok(await _service.AssessAssetAsync(request, cancellationToken));

    [HttpGet("accounts")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<IReadOnlyList<EstateGroundRentAccountDto>>> GetAccounts(
        CancellationToken cancellationToken)
        => Ok(await _service.GetAccountsAsync(cancellationToken));

    [HttpPost("accounts")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentAccountDto>> UpsertAccount(
        [FromBody] UpsertEstateGroundRentAccountDto request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpsertAccountAsync(request, cancellationToken));

    [HttpPost("accounts/{accountId:guid}/invoices")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentActionResultDto>> GenerateInvoice(
        Guid accountId,
        [FromBody] GenerateEstateGroundRentInvoiceDto request,
        CancellationToken cancellationToken)
        => Ok(await _service.GenerateInvoiceAsync(accountId, request, cancellationToken));

    [HttpPost("accounts/{accountId:guid}/reviews")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentActionResultDto>> ApplyReview(
        Guid accountId,
        [FromBody] ApplyEstateGroundRentReviewDto request,
        CancellationToken cancellationToken)
        => Ok(await _service.ApplyReviewAsync(accountId, request, cancellationToken));

    [HttpPost("charges/{chargeId:guid}/penalties")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentActionResultDto>> AssessPenalty(
        Guid chargeId,
        CancellationToken cancellationToken)
        => Ok(await _service.AssessPenaltyAsync(chargeId, cancellationToken));

    [HttpPost("charges/{chargeId:guid}/post")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentActionResultDto>> PostInvoice(
        Guid chargeId,
        [FromQuery] string target = "Base",
        CancellationToken cancellationToken = default)
        => Ok(await _service.PostInvoiceAsync(chargeId, target, cancellationToken));

    [HttpPost("charges/{chargeId:guid}/receipts")]
    [Authorize(Roles = EstateRoles)]
    public async Task<ActionResult<EstateGroundRentActionResultDto>> RecordReceipt(
        Guid chargeId,
        [FromBody] RecordEstateGroundRentReceiptDto request,
        CancellationToken cancellationToken)
        => Ok(await _service.RecordReceiptAsync(chargeId, request, cancellationToken));
}
