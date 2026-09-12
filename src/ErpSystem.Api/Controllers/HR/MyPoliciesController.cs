using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The policy library, as the employee sees it — and where they sign (area 25 slice 12d, D7).
/// </summary>
/// <remarks>
/// A policy that does not apply to the caller is a lookup miss, not a refusal: a 403 would tell
/// them a policy exists which they were not given. The signature's IP is taken from the request
/// by the server and never from the payload.
/// </remarks>
[ApiController]
[Route("api/employee-portal/policies")]
[Authorize(Policy = "InternalOnly")]
public class MyPoliciesController : ControllerBase
{
    private readonly IHrPolicyService _service;
    private readonly ICurrentUserService _currentUser;

    public MyPoliciesController(IHrPolicyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>Server-observed, never from the client — a self-declared IP proves nothing.</summary>
    private string? CallerIp => _currentUser.IpAddress
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Every live policy that applies to the caller, with their own answer on each.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMineAsync(empId, ct));
    }

    /// <summary>Only the ones still waiting on them.</summary>
    [HttpGet("outstanding")]
    public async Task<IActionResult> GetOutstanding(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMyOutstandingAsync(empId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var policy = await _service.GetMineByIdAsync(id, empId, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    /// <summary>Signs it. The submitted declaration must match what the policy currently says.</summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(
        Guid id, [FromBody] AcknowledgePolicyDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            return Ok(await _service.AcknowledgeAsync(id, empId, dto ?? new(), CallerIp, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Refuses it, with a reason. A recorded outcome, not a failure.</summary>
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(
        Guid id, [FromBody] DeclinePolicyDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            return Ok(await _service.DeclineAsync(id, empId, dto ?? new(), CallerIp, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Downloads the policy document itself.</summary>
    [HttpGet("{id:guid}/document")]
    public async Task<IActionResult> DownloadDocument(
        [FromServices] ErpSystem.Data.ApplicationDbContext db,
        [FromServices] ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService fileStorage,
        Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (_currentUser.TenantId is not Guid tenantId) return NoEmployee();

        // Entitlement through the same audience check as the read: a policy document must not be
        // reachable by anybody the policy itself does not apply to.
        var policy = await _service.GetMineByIdAsync(id, empId, ct);
        if (policy is null || !policy.HasDocument) return NotFound();

        var row = await db.HrPolicyDocuments.FindAsync([id], ct);
        if (row is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, fileStorage, db, tenantId,
            row.DocumentRecordId, row.DocumentVersionId,
            row.FileUploadRecordId, row.FilePath,
            row.FileName ?? "policy", row.ContentType,
            inline: false, cancellationToken: ct);
    }
}
