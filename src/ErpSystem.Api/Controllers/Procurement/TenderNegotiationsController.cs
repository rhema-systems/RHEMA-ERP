using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/negotiations")]
[Authorize]
public class TenderNegotiationsController : ControllerBase
{
    private readonly ITenderNegotiationService _service;
    private readonly ILogger<TenderNegotiationsController> _logger;

    public TenderNegotiationsController(
        ITenderNegotiationService service,
        ILogger<TenderNegotiationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get negotiation by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<TenderNegotiationDto>> GetById(Guid id)
    {
        var negotiation = await _service.GetByIdAsync(id);
        if (negotiation == null) return NotFound();
        return Ok(negotiation);
    }

    /// <summary>
    /// Get negotiation by tender and bid
    /// </summary>
    [HttpGet("by-tender-bid")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<TenderNegotiationDto>> GetByTenderAndBid([FromQuery] Guid tenderId, [FromQuery] Guid bidId)
    {
        var negotiation = await _service.GetByTenderAndBidAsync(tenderId, bidId);
        if (negotiation == null) return NotFound();
        return Ok(negotiation);
    }

    /// <summary>
    /// Get all negotiations for a tender
    /// </summary>
    [HttpGet("by-tender/{tenderId}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderNegotiationDto>>> GetByTenderId(Guid tenderId)
    {
        var negotiations = await _service.GetByTenderIdAsync(tenderId);
        return Ok(negotiations);
    }

    /// <summary>
    /// Get all negotiations for a bid
    /// </summary>
    [HttpGet("by-bid/{bidId}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderNegotiationDto>>> GetByBidId(Guid bidId)
    {
        var negotiations = await _service.GetByBidIdAsync(bidId);
        return Ok(negotiations);
    }

    /// <summary>
    /// Create a new negotiation (invite bidder for negotiation)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderNegotiationDto>> Create([FromBody] CreateNegotiationDto dto)
    {
        try
        {
            var negotiation = await _service.CreateNegotiationAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = negotiation.Id }, negotiation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update a negotiation item's negotiated price
    /// </summary>
    [HttpPut("{id}/items")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderNegotiationDto>> UpdateItem(Guid id, [FromBody] UpdateNegotiationItemDto dto)
    {
        try
        {
            var negotiation = await _service.UpdateNegotiationItemAsync(id, dto);
            return Ok(negotiation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Save negotiation as draft (save progress without completing)
    /// </summary>
    [HttpPost("{id}/save-draft")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderNegotiationDto>> SaveDraft(Guid id, [FromBody] CompleteNegotiationDto dto)
    {
        try
        {
            var negotiation = await _service.SaveDraftAsync(id, dto);
            return Ok(negotiation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Complete a negotiation with final negotiated prices
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderNegotiationDto>> Complete(Guid id, [FromBody] CompleteNegotiationDto dto)
    {
        try
        {
            var negotiation = await _service.CompleteNegotiationAsync(id, dto);
            return Ok(negotiation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a negotiation
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult> Cancel(Guid id)
    {
        try
        {
            await _service.CancelNegotiationAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

