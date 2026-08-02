using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical-expense-claims")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class MedicalExpenseClaimsController : MedicalControllerBase
{
    private readonly IMedicalExpenseClaimService _service;

    public MedicalExpenseClaimsController(IMedicalExpenseClaimService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // CLAIMS
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalExpenseClaimDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<MedicalExpenseClaimDetailDto>> GetWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimWithDetailsAsync(id, ct));

    [HttpGet("number/{claimNumber}")]
    public async Task<ActionResult<MedicalExpenseClaimDto?>> GetByNumber(string claimNumber, CancellationToken ct)
        => Ok(await _service.GetClaimByNumberAsync(claimNumber, ct));

    [HttpGet("employees/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetClaimsByEmployeeAsync(employeeId, ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<MedicalExpenseClaimSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ClaimStatus? status = null,
        CancellationToken ct = default)
        => Ok(await _service.GetClaimsPagedAsync(pageNumber, pageSize, status, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingClaimsAsync(ct));

    [HttpGet("flagged")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetFlagged(CancellationToken ct)
        => Ok(await _service.GetFlaggedClaimsAsync(ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<MedicalExpenseClaimDto>> Create(
        [FromBody] CreateMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId) is { } error) return error;

        // Default to the caller filing for themselves. HR/Admin may file on behalf of another employee.
        var targetEmployeeId = employeeId;
        if (dto.EmployeeId.HasValue && dto.EmployeeId.Value != employeeId)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                return Forbid();
            targetEmployeeId = dto.EmployeeId.Value;
        }

        var created = await _service.CreateClaimAsync(dto, targetEmployeeId, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MedicalExpenseClaimDto>> Update(
        Guid id,
        [FromBody] UpdateMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateClaimAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/approval")]
    public async Task<IActionResult> ProcessApproval(
        Guid id,
        [FromBody] ProcessMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId) is { } error) return error;

        await _service.ProcessApprovalAsync(dto, tenantId, employeeId, userId, ct);
        return Ok(new { message = "Approval recorded." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> ProcessPayment(
        Guid id,
        [FromBody] ProcessMedicalExpensePaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.ProcessPaymentAsync(dto, ct);
        return Ok(new { message = "Payment processed." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/flag")]
    public async Task<IActionResult> Flag(
        Guid id,
        [FromBody] FlagMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.FlagClaimAsync(dto, ct);
        return Ok(new { message = "Claim flagged." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/unflag")]
    public async Task<IActionResult> Unflag(
        Guid id,
        [FromBody] UnflagMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        await _service.UnflagClaimAsync(dto, ct);
        return Ok(new { message = "Claim unflagged." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // ITEMS
    // =========================================================================

    [HttpGet("{claimId:guid}/items")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseItemDto>>> GetItems(
        Guid claimId,
        CancellationToken ct)
        => Ok(await _service.GetItemsAsync(claimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/items")]
    public async Task<ActionResult<MedicalExpenseItemDto>> AddItem(
        Guid claimId,
        [FromBody] CreateMedicalExpenseItemDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = claimId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddItemAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = claimId }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<MedicalExpenseItemDto>> UpdateItem(
        Guid id,
        [FromBody] UpdateMedicalExpenseItemDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateItemAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await _service.DeleteItemAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{claimId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseDocumentDto>>> GetDocuments(
        Guid claimId,
        CancellationToken ct)
        => Ok(await _service.GetDocumentsAsync(claimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/documents")]
    public async Task<ActionResult<MedicalExpenseDocumentDto>> AddDocument(
        Guid claimId,
        [FromBody] CreateMedicalExpenseDocumentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = claimId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddDocumentAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = claimId }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteDocumentAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpGet("{claimId:guid}/notes")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimNoteDto>>> GetNotes(
        Guid claimId,
        [FromQuery] bool internalOnly = false,
        CancellationToken ct = default)
        => Ok(internalOnly
            ? await _service.GetInternalNotesAsync(claimId, ct)
            : await _service.GetNotesAsync(claimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/notes")]
    public async Task<ActionResult<MedicalExpenseClaimNoteDto>> AddNote(
        Guid claimId,
        [FromBody] AddMedicalExpenseClaimNoteDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = claimId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId) is { } error) return error;

        var created = await _service.AddNoteAsync(dto, tenantId, employeeId, userId, ct);
        return Ok(created);
    }
}
