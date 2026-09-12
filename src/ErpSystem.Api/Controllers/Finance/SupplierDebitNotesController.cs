using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/ap/supplier-debit-notes")]
public sealed class SupplierDebitNotesController : ControllerBase
{
    private readonly ISupplierDebitNoteService _service;
    private static readonly FinancePostingProducerContext DimensionProducer =
        new(FinanceDimensionRouteId.FinanceApSupplierDebitNote);

    public SupplierDebitNotesController(ISupplierDebitNoteService service) => _service = service;

    [HttpGet("inventory-returns/{returnId:guid}/source-invoices")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<InventoryReturnCreditSourceDto>>> ReturnSources(Guid returnId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetInventoryReturnCreditSourcesAsync(returnId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("inventory-returns/{returnId:guid}/credit")]
    [Authorize(Policy = FinancePermissions.ManageApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> CreateReturnCredit(Guid returnId,
        [FromBody] CreateInventoryReturnCreditDto dto, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateInventoryReturnCreditAsync(returnId, dto, DimensionProducer, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<SupplierDebitNoteDto>>> GetAll(
        [FromQuery] SupplierDebitNoteQueryDto query,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(query, cancellationToken));

    [HttpPut("{id:guid}/inventory-return-header")]
    [Authorize(Policy = FinancePermissions.ManageApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> UpdateReturnCreditHeader(Guid id,
        [FromBody] UpdateInventoryReturnCreditHeaderDto dto, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpdateInventoryReturnCreditHeaderAsync(id, dto, DimensionProducer, cancellationToken)); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Supplier credit changed. Refresh the record before saving your header correction." }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<SupplierDebitNoteDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, DimensionProducer, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Create(
        [FromBody] CreateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(dto, DimensionProducer, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = FinancePermissions.ManageApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Update(
        Guid id,
        [FromBody] UpdateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpdateDraftAsync(id, dto, DimensionProducer, cancellationToken)); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Supplier debit note was changed by another user. Refresh and try again." }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = FinancePermissions.SubmitApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Submit(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.SubmitAsync(id, DimensionProducer, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/approval")]
    [Authorize(Policy = FinancePermissions.ApproveApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> ProcessApproval(
        Guid id,
        [FromBody] SupplierDebitNoteApprovalDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.ProcessApprovalAsync(id, dto, DimensionProducer, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = FinancePermissions.PostApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.PostAsync(id, DimensionProducer, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = FinancePermissions.ManageApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Cancel(
        Guid id,
        [FromBody] ReverseSupplierDebitNoteDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CancelAsync(id, dto.Reason, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = FinancePermissions.ReverseApSupplierDebitNotes)]
    public async Task<ActionResult<SupplierDebitNoteDto>> Reverse(
        Guid id,
        [FromBody] ReverseSupplierDebitNoteDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.ReverseAsync(id, dto, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { error = exception.Message }); }
    }
}
