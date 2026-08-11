using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
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

    [HttpGet("diagnostics")]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceDiagnosticDto>>> Diagnostics(CancellationToken cancellationToken)
        => Ok(await _openingBalanceService.GetDiagnosticsAsync(cancellationToken));
}
