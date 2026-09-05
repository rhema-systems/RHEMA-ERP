using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Letters an employee asks HR for, and the letters HR has issued them
/// (area 25 slice 12b, decision D7).
/// </summary>
/// <remarks>
/// The scoping law is the module's: the employee comes from the token, no route or query
/// parameter here carries an employee id, and a request belonging to somebody else is a
/// lookup miss rather than a refusal.
/// </remarks>
[ApiController]
[Route("api/employee-portal/letters")]
[Authorize(Policy = "InternalOnly")]
public class MyLettersController : ControllerBase
{
    private readonly IHrLetterRequestService _service;
    private readonly ICurrentUserService _currentUser;

    public MyLettersController(IHrLetterRequestService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>The caller's own letter requests, newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMineAsync(empId, ct));
    }

    /// <summary>Asks HR for a letter.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHrLetterRequestDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            var created = await _service.CreateAsync(empId, dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    /// <summary>One of the caller's own requests. Somebody else's id is a 404, never a 403.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var request = await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Withdraws a request HR has not answered yet.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            return Ok(await _service.CancelAsync(id, empId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// The issued letter, as it was frozen at issue — for reading and printing. 404 while the
    /// request is unanswered, and for a letter HR fulfilled by uploading a signed scan instead
    /// (that one is served as a file, below).
    /// </summary>
    [HttpGet("{id:guid}/document")]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var document = await _service.GetIssuedDocumentAsync(id, empId, isHrDesk: false, ct);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>Downloads the signed letter, when HR fulfilled the request by uploading one.</summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(
        [FromServices] ErpSystem.Data.ApplicationDbContext db,
        [FromServices] ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService fileStorage,
        Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (_currentUser.TenantId is not Guid tenantId) return NoEmployee();

        // Ownership first: a request that is not the caller's does not resolve at all.
        var request = await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
        if (request is null || request.Fulfilment != "Uploaded") return NotFound();

        var row = await db.HrLetterRequests.FindAsync([id], ct);
        if (row is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, fileStorage, db, tenantId,
            row.DocumentRecordId, row.DocumentVersionId,
            row.FileUploadRecordId, row.FilePath,
            row.FileName ?? "letter", row.ContentType,
            inline: false, cancellationToken: ct);
    }
}
