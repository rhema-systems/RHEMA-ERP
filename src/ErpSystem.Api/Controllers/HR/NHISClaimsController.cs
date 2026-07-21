using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/nhis-claims")]
[Authorize]
public class NHISClaimsController : MedicalControllerBase
{
    private readonly INHISService _service;

    public NHISClaimsController(INHISService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // CLAIMS
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllClaimsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NHISClaimDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<NHISClaimDetailDto>> GetWithDocuments(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimWithDocumentsAsync(id, ct));

    [HttpGet("number/{claimNumber}")]
    public async Task<ActionResult<NHISClaimDto?>> GetByNumber(string claimNumber, CancellationToken ct)
        => Ok(await _service.GetClaimByNumberAsync(claimNumber, ct));

    [HttpGet("employees/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetClaimsByEmployeeAsync(employeeId, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingClaimsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<NHISClaimDto>> Create(
        [FromBody] CreateNHISClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateClaimAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NHISClaimDto>> Update(
        Guid id,
        [FromBody] UpdateNHISClaimDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateClaimAsync(dto, userId, ct));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateNHISClaimStatusDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.UpdateClaimStatusAsync(dto, ct);
        return Ok(new { message = "Claim status updated." });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitNHISClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.SubmitClaimAsync(dto, ct);
        return Ok(new { message = "Claim submitted." });
    }

    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordNHISClaimPaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.RecordClaimPaymentAsync(dto, ct);
        return Ok(new { message = "Payment recorded." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{nhisClaimId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<NHISClaimDocumentDto>>> GetDocuments(
        Guid nhisClaimId,
        CancellationToken ct)
        => Ok(await _service.GetClaimDocumentsAsync(nhisClaimId, ct));

    [HttpPost("documents")]
    public async Task<ActionResult<NHISClaimDocumentDto>> AddDocument(
        [FromBody] CreateNHISClaimDocumentDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddClaimDocumentAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimDocumentAsync(id, ct);
        return NoContent();
    }
}
