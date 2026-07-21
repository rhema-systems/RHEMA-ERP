using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/permits")]
[Authorize]
public class ShePermitToWorkController : SheApiControllerBase
{
    private readonly IShePermitToWorkService _service;

    public ShePermitToWorkController(IShePermitToWorkService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShePermitToWorkDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{permitNumber}")]
    public async Task<ActionResult<ShePermitToWorkDto?>> GetByNumber(string permitNumber)
        => Ok(await _service.GetByNumberAsync(permitNumber));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetByStatus(ShePermitStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetByType(ShePermitType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("contractor/{contractorId:guid}")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetByContractor(Guid contractorId)
        => Ok(await _service.GetByContractorAsync(contractorId));

    [HttpGet("requestor/{requestedById:guid}")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetByRequestor(Guid requestedById)
        => Ok(await _service.GetByRequestorAsync(requestedById));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetExpiring([FromQuery] int daysAhead = 1)
        => Ok(await _service.GetExpiringAsync(daysAhead));

    [HttpGet("suspended")]
    public async Task<ActionResult<IEnumerable<ShePermitToWorkSummaryDto>>> GetSuspended()
        => Ok(await _service.GetSuspendedAsync());

    [HttpPost]
    public async Task<ActionResult<ShePermitToWorkDto>> Create([FromBody] CreateShePermitToWorkDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShePermitToWorkDto>> Update(Guid id, [FromBody] UpdateShePermitToWorkDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Workflow ──
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveShePermitToWorkDto dto)
    {
        dto.PermitId = id;
        await _service.ApproveAsync(dto, UserId);
        return Ok(new { message = "Permit approved." });
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendShePermitToWorkDto dto)
    {
        dto.PermitId = id;
        await _service.SuspendAsync(dto, UserId);
        return Ok(new { message = "Permit suspended." });
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        await _service.ResumeAsync(id, UserId);
        return Ok(new { message = "Permit resumed." });
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseShePermitToWorkDto dto)
    {
        dto.PermitId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Permit closed." });
    }

    // ── Workers ──
    [HttpPost("{id:guid}/workers")]
    public async Task<ActionResult<ShePermitToWorkWorkerDto>> AddWorker(Guid id, [FromBody] CreateShePermitToWorkWorkerDto dto)
    {
        dto.PermitToWorkId = id;
        return Ok(await _service.AddWorkerAsync(dto, TenantId, UserId));
    }

    [HttpPut("workers/{workerId:guid}")]
    public async Task<ActionResult<ShePermitToWorkWorkerDto>> UpdateWorker(Guid workerId, [FromBody] UpdateShePermitToWorkWorkerDto dto)
    {
        if (workerId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWorkerAsync(dto, UserId));
    }

    [HttpDelete("workers/{workerId:guid}")]
    public async Task<IActionResult> DeleteWorker(Guid workerId)
    {
        await _service.DeleteWorkerAsync(workerId);
        return NoContent();
    }

    // ── Extensions ──
    [HttpPost("{id:guid}/extensions")]
    public async Task<ActionResult<ShePermitToWorkExtensionDto>> AddExtension(Guid id, [FromBody] CreateShePermitToWorkExtensionDto dto)
    {
        dto.PermitToWorkId = id;
        return Ok(await _service.AddExtensionAsync(dto, TenantId, UserId));
    }

    // ── Documents ──
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<ShePermitToWorkDocumentDto>> AddDocument(Guid id, [FromBody] CreateShePermitToWorkDocumentDto dto)
    {
        dto.PermitToWorkId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
