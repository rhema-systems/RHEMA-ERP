using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/opening-balances")]
public sealed class OpeningBalancesController : ControllerBase
{
    private readonly IOpeningBalanceService _openingBalanceService;

    public OpeningBalancesController(IOpeningBalanceService openingBalanceService)
    {
        _openingBalanceService = openingBalanceService;
    }

    [HttpPost]
    public async Task<ActionResult<OpeningBalanceBatchDto>> Create(
        [FromBody] CreateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateBatchAsync(dto, cancellationToken));

    [HttpPost("fixed-assets")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateFixedAssetBatch(
        [FromBody] CreateFixedAssetOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateFixedAssetBatchAsync(dto, cancellationToken));

    [HttpPost("bank-accounts")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateBankAccountOpening(
        [FromBody] CreateBankAccountOpeningBalanceDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateBankAccountOpeningBatchAsync(dto, cancellationToken));

    [HttpPost("residual-gl-equity")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateResidualGlEquityOpening(
        [FromBody] CreateResidualGlEquityOpeningBalanceDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateResidualGlEquityOpeningBatchAsync(dto, cancellationToken));

    [HttpPost("supplier-advances")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateSupplierAdvance([FromBody] CreateSupplierAdvanceOpeningBalanceDto dto, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateSupplierAdvanceBatchAsync(dto, cancellationToken));

    [HttpPost("customer-advances")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateCustomerAdvance([FromBody] CreateCustomerAdvanceOpeningBalanceDto dto, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateCustomerAdvanceBatchAsync(dto, cancellationToken));

    [HttpPost("ap-withholding")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateApWithholding([FromBody] CreateApWithholdingOpeningBalanceDto dto, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateApWithholdingBatchAsync(dto, cancellationToken));

    [HttpPost("ar-withholding")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> CreateArWithholding([FromBody] CreateArWithholdingOpeningBalanceDto dto, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.CreateArWithholdingBatchAsync(dto, cancellationToken));

    [HttpGet("specialized-options")]
    public async Task<ActionResult<SpecializedOpeningBalanceOptionsDto>> GetSpecializedOptions(CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetSpecializedOptionsAsync(cancellationToken));

    [HttpGet("governed-options")]
    public async Task<ActionResult<GovernedOpeningBalanceOptionsDto>> GetGovernedOptions(
        [FromQuery] GovernedOpeningBalanceOptionsRequestDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetGovernedOptionsAsync(dto, cancellationToken));

    [HttpGet("subledger-readiness")]
    public async Task<ActionResult<SubledgerOpeningBalanceReadinessDto>> GetSubledgerReadiness(
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetSubledgerReadinessAsync(cancellationToken));

    [HttpGet("{batchId:guid}")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> Get(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var batch = await _openingBalanceService.GetBatchAsync(batchId, cancellationToken);
        return batch == null ? NotFound() : Ok(batch);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceBatchDto>>> List(CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetBatchesAsync(cancellationToken));

    [HttpPut("{batchId:guid}")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> Update(
        Guid batchId,
        [FromBody] UpdateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.UpdateBatchAsync(batchId, dto, cancellationToken));

    [HttpPost("{batchId:guid}/validate")]
    public async Task<ActionResult<OpeningBalanceValidationResultDto>> Validate(
        Guid batchId,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.ValidateBatchAsync(batchId, cancellationToken));

    [HttpPost("{batchId:guid}/submit")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> Submit(
        Guid batchId,
        [FromBody] SubmitOpeningBalanceBatchDto? dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.SubmitForApprovalAsync(batchId, dto?.Comment, cancellationToken));

    [HttpPost("{batchId:guid}/post")]
    public async Task<ActionResult<OpeningBalanceBatchDto>> Post(
        Guid batchId,
        [FromBody] PostOpeningBalanceBatchDto? dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.PostAsync(batchId, dto?.Comment, cancellationToken));

    [HttpGet("{batchId:guid}/reversals")]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceBatchReversalDto>>> GetReversals(Guid batchId, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetReversalsAsync(batchId, cancellationToken));

    [HttpPost("{batchId:guid}/reversals")]
    public async Task<ActionResult<OpeningBalanceBatchReversalDto>> RequestReversal(
        Guid batchId,
        [FromBody] RequestOpeningBalanceBatchReversalDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.RequestReversalAsync(batchId, dto, cancellationToken));

    [HttpPost("{batchId:guid}/reversals/{requestId:guid}/review")]
    [Authorize(Policy = FinancePermissions.ApproveOpeningBalanceReversal)]
    public async Task<ActionResult<OpeningBalanceBatchReversalDto>> ReviewReversal(
        Guid batchId,
        Guid requestId,
        [FromBody] ReviewOpeningBalanceBatchReversalDto dto,
        CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.ReviewReversalAsync(batchId, requestId, dto, cancellationToken));

    [HttpPost("{batchId:guid}/reversals/{requestId:guid}/post")]
    public async Task<ActionResult<OpeningBalanceBatchReversalDto>> PostReversal(Guid batchId, Guid requestId, CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.PostReversalAsync(batchId, requestId, cancellationToken));

    [HttpGet("diagnostics")]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceDiagnosticDto>>> Diagnostics(CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetDiagnosticsAsync(cancellationToken));
}
