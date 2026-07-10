using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/allocations")]
public class SalesAllocationsController : ControllerBase
{
    private readonly ISalesAllocationService _salesAllocationService;

    public SalesAllocationsController(ISalesAllocationService salesAllocationService)
    {
        _salesAllocationService = salesAllocationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SalesAllocationDto>>> GetAllocations(
        [FromQuery] Guid? saleableSourceId = null,
        [FromQuery] string? sourceItemId = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] Guid? salesOrderId = null,
        [FromQuery] Guid? salesAgreementId = null,
        [FromQuery] bool activeOnly = false)
        => Ok(await _salesAllocationService.GetAllocationsAsync(
            saleableSourceId,
            sourceItemId,
            status,
            businessPartnerId,
            salesOrderId,
            salesAgreementId,
            activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesAllocationDto>> GetAllocation(Guid id)
    {
        var allocation = await _salesAllocationService.GetAllocationByIdAsync(id);
        return allocation == null ? NotFound() : Ok(allocation);
    }

    [HttpGet("active-check")]
    public async Task<ActionResult<object>> HasActiveAllocation(
        [FromQuery] Guid saleableSourceId,
        [FromQuery] string sourceItemId)
    {
        var hasActiveAllocation = await _salesAllocationService.HasActiveAllocationAsync(saleableSourceId, sourceItemId);
        return Ok(new { hasActiveAllocation });
    }

    [HttpPost]
    public async Task<ActionResult<SalesAllocationDto>> CreateAllocation([FromBody] CreateSalesAllocationDto dto)
    {
        try
        {
            var allocation = await _salesAllocationService.CreateAllocationAsync(dto);
            return CreatedAtAction(nameof(GetAllocation), new { id = allocation.Id }, allocation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<SalesAllocationDto>> SubmitForApproval(Guid id)
    {
        try
        {
            return Ok(await _salesAllocationService.SubmitForApprovalAsync(id));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<SalesAllocationDto>> ProcessApproval(Guid id, [FromBody] SalesAllocationApprovalDto dto)
    {
        try
        {
            return Ok(await _salesAllocationService.ProcessApprovalAsync(id, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/transfer")]
    public async Task<ActionResult<SalesAllocationDto>> TransferAllocation(Guid id, [FromBody] TransferSalesAllocationDto dto)
    {
        try
        {
            return Ok(await _salesAllocationService.TransferAllocationAsync(id, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<SalesAllocationDto>> UpdateAllocationStatus(Guid id, [FromBody] UpdateSalesAllocationStatusDto dto)
    {
        try
        {
            return Ok(await _salesAllocationService.UpdateAllocationStatusAsync(id, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
