using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Banking workbench APIs. These endpoints deliberately separate operational settlement
/// from customer receipting and expose only approved bank-facing movements to reconciliation.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/banking")]
public sealed class BankingSettlementController : ControllerBase
{
    private readonly IBankingSettlementService _service;

    public BankingSettlementController(IBankingSettlementService service)
    {
        _service = service;
    }

    [HttpGet("setup")]
    public async Task<ActionResult<BankingSetupStatusDto>> GetSetup(CancellationToken cancellationToken)
        => Ok(await _service.GetSetupStatusAsync(cancellationToken));

    [HttpPost("setup")]
    public async Task<ActionResult<BankingSetupStatusDto>> CompleteSetup(
        [FromBody] CompleteBankingSetupDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.CompleteSetupAsync(dto, cancellationToken));

    [HttpGet("liquidity-accounts")]
    public async Task<ActionResult<IReadOnlyList<LiquidityAccountDto>>> GetLiquidityAccounts(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetLiquidityAccountsAsync(activeOnly, cancellationToken));

    [HttpGet("liquidity-accounts/{id:guid}")]
    public async Task<ActionResult<LiquidityAccountDto>> GetLiquidityAccount(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetLiquidityAccountAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("liquidity-accounts")]
    public async Task<ActionResult<LiquidityAccountDto>> CreateLiquidityAccount(
        [FromBody] CreateLiquidityAccountDto dto,
        CancellationToken cancellationToken)
    {
        var item = await _service.CreateLiquidityAccountAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetLiquidityAccount), new { id = item.Id }, item);
    }

    [HttpPut("liquidity-accounts/{id:guid}")]
    public async Task<ActionResult<LiquidityAccountDto>> UpdateLiquidityAccount(
        Guid id,
        [FromBody] UpdateLiquidityAccountDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateLiquidityAccountAsync(id, dto, cancellationToken));

    [HttpGet("eligible-entries")]
    public async Task<ActionResult<IReadOnlyList<LiquidityAccountEntryDto>>> GetEligibleEntries(
        [FromQuery] Guid? liquidityAccountId = null,
        [FromQuery] string? currency = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetEligibleEntriesAsync(liquidityAccountId, currency, cancellationToken));

    [HttpGet("posted-payment-candidates")]
    public async Task<ActionResult<IReadOnlyList<PostedLiquidityPaymentCandidateDto>>> GetPostedPaymentCandidates(
        [FromQuery] Guid? liquidityAccountId = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetPostedPaymentCandidatesAsync(liquidityAccountId, cancellationToken));

    [HttpPost("posted-payment-entries")]
    public async Task<ActionResult<LiquidityAccountEntryDto>> RegisterPostedPayment(
        [FromBody] RegisterPostedLiquidityPaymentDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RegisterPostedPaymentAsync(dto, cancellationToken));

    [HttpGet("deposits")]
    public async Task<ActionResult<IReadOnlyList<BankDepositDto>>> GetDeposits(
        [FromQuery] BankDepositStatus? status = null,
        [FromQuery] bool includeAllocations = false,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetDepositsAsync(status, includeAllocations, limit, cancellationToken));

    [HttpGet("deposits/{id:guid}")]
    public async Task<ActionResult<BankDepositDto>> GetDeposit(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDepositAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("deposits")]
    public async Task<ActionResult<BankDepositDto>> CreateDeposit(
        [FromBody] CreateBankDepositDto dto,
        CancellationToken cancellationToken)
    {
        var item = await _service.CreateDepositAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetDeposit), new { id = item.Id }, item);
    }

    [HttpPut("deposits/{id:guid}")]
    public async Task<ActionResult<BankDepositDto>> UpdateDeposit(
        Guid id,
        [FromBody] UpdateBankDepositDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateDepositAsync(id, dto, cancellationToken));

    [HttpPut("deposits/{id:guid}/dimensions")]
    public async Task<ActionResult<BankDepositDto>> UpdateDepositDimensions(
        Guid id,
        [FromBody] FinanceSourceDocumentDimensionInputDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateDepositDimensionsAsync(id, dto, cancellationToken));

    [HttpPost("deposits/{id:guid}/attachments")]
    public async Task<ActionResult<BankDepositDto>> LinkDepositAttachment(
        Guid id,
        [FromBody] LinkBankingAttachmentDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.LinkDepositAttachmentAsync(id, dto, cancellationToken));

    [HttpDelete("deposits/{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<ActionResult<BankDepositDto>> UnlinkDepositAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
        => Ok(await _service.UnlinkDepositAttachmentAsync(id, attachmentId, cancellationToken));

    [HttpPost("deposits/{id:guid}/submit")]
    public async Task<ActionResult<BankDepositDto>> SubmitDeposit(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.SubmitDepositAsync(id, cancellationToken));

    [HttpPost("deposits/{id:guid}/approve")]
    public async Task<ActionResult<BankDepositDto>> ApproveDeposit(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ApproveDepositAsync(id, dto.Comments, cancellationToken));

    [HttpPost("deposits/{id:guid}/reject")]
    public async Task<ActionResult<BankDepositDto>> RejectDeposit(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RejectDepositAsync(id, dto.Reason ?? dto.Comments, cancellationToken));

    [HttpPost("deposits/{id:guid}/return")]
    public async Task<ActionResult<BankDepositDto>> ReturnDeposit(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ReturnDepositAsync(id, dto.Comments ?? dto.Reason, cancellationToken));

    [HttpPost("deposits/{id:guid}/cancel")]
    public async Task<ActionResult<BankDepositDto>> CancelDeposit(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.CancelDepositAsync(id, dto.Reason ?? "Cancelled by user.", cancellationToken));

    [HttpPost("deposits/{id:guid}/post")]
    public async Task<ActionResult<BankDepositDto>> PostDeposit(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.PostDepositAsync(id, cancellationToken));

    [HttpPost("deposits/{id:guid}/confirm")]
    public async Task<ActionResult<BankDepositDto>> ConfirmDeposit(
        Guid id,
        [FromBody] ConfirmBankDepositDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ConfirmDepositAsync(id, dto, cancellationToken));

    [HttpGet("returned-cheques")]
    public async Task<ActionResult<IReadOnlyList<ReturnedChequeCaseDto>>> GetReturnedCheques(
        [FromQuery] ReturnedChequeCaseStatus? status = null,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetReturnedChequesAsync(status, limit, cancellationToken));

    [HttpGet("returned-cheques/{id:guid}")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> GetReturnedCheque(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetReturnedChequeAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("returned-cheques")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> CreateReturnedCheque(
        [FromBody] CreateReturnedChequeCaseDto dto,
        CancellationToken cancellationToken)
    {
        var item = await _service.CreateReturnedChequeAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetReturnedCheque), new { id = item.Id }, item);
    }

    [HttpPut("returned-cheques/{id:guid}/dimensions")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> UpdateReturnedChequeDimensions(
        Guid id,
        [FromBody] FinanceSourceDocumentDimensionInputDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateReturnedChequeDimensionsAsync(id, dto, cancellationToken));

    [HttpPost("returned-cheques/{id:guid}/attachments")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> LinkReturnedChequeAttachment(
        Guid id,
        [FromBody] LinkBankingAttachmentDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.LinkReturnedChequeAttachmentAsync(id, dto, cancellationToken));

    [HttpPost("returned-cheques/{id:guid}/submit")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> SubmitReturnedCheque(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _service.SubmitReturnedChequeAsync(id, cancellationToken));

    [HttpPost("returned-cheques/{id:guid}/approve")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> ApproveReturnedCheque(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ApproveReturnedChequeAsync(id, dto.Comments, cancellationToken));

    [HttpPost("returned-cheques/{id:guid}/reject")]
    public async Task<ActionResult<ReturnedChequeCaseDto>> RejectReturnedCheque(
        Guid id,
        [FromBody] BankingWorkflowActionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.RejectReturnedChequeAsync(id, dto.Reason ?? dto.Comments, cancellationToken));
}
