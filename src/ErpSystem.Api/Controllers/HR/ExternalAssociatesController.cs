using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The register of external associates — interview panellists, technical assessors and advisers who
/// act for the organisation without holding an ERP login.
/// </summary>
/// <remarks>
/// <para><b>The whole surface is gated — reads included</b> (W3 slice 14: <c>HR.Company.Read</c>
/// on reads and search, Write on create/amend/activate/deactivate, Admin on delete — replacing
/// the SA/TenantAdmin/HR role gate with the same reach for HR). That is a tighter gate than the
/// union register next door, and deliberately so: a union is a noticeboard, while this is a
/// directory of named third parties' personal email addresses and phone numbers. The gate costs
/// nothing in reach, because the one endpoint anything consumes — <c>search</c>, behind
/// <c>PanelMemberPicker</c> — is only ever rendered inside an action that
/// <c>JobInterviewService</c> already restricts to HR (<c>EnsureHr("change an interview panel")</c>).
/// Checked before gating rather than assumed.</para>
///
/// <para>⚠ Until areas 19-23 slice 8 this controller carried a bare <c>[Authorize]</c>, and slice 8's
/// probe measured what that meant: a plain <c>Employee</c> could list every associate with their
/// email and phone number, and create one.</para>
///
/// <para>⚠ It also had no error contract worth the name. <c>Delete</c> caught only
/// <c>ArgumentException</c>, so the panel-membership refusal added in this slice would have arrived
/// as a bare 500; the four list reads caught nothing at all. One <c>RunAsync</c> now carries every
/// action, as on <see cref="UnionController"/>.</para>
/// </remarks>
[ApiController]
[Route("api/external-associates")]
[Authorize(Policy = "InternalOnly")]
public class ExternalAssociatesController : ControllerBase
{
    private readonly IExternalAssociateService _service;
    private readonly ICurrentUserService       _currentUser;
    private readonly ILogger<ExternalAssociatesController> _logger;

    public ExternalAssociatesController(
        IExternalAssociateService service,
        ICurrentUserService currentUser,
        ILogger<ExternalAssociatesController> logger)
    {
        _service     = service;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <summary>
    /// One error contract for every action here, so a rule can explain itself.
    /// </summary>
    /// <remarks>
    /// <c>ArgumentException</c> is the service's "not found", <c>InvalidOperationException</c> its
    /// "you may not do that", and <c>UnauthorizedAccessException</c> its cross-tenant refusal. Each
    /// carries the sentence the service wrote; the catch-all keeps its detail out of the response
    /// and puts it in the log.
    /// </remarks>
    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action, string what)
    {
        try
        {
            return Ok(await action());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External associates: {What} failed", what);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = $"Could not {what}." });
        }
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public Task<IActionResult> GetAll(CancellationToken ct) =>
        RunAsync(() => _service.GetAllAsync(ct), "list the external associates");

    /// <param name="isActive">
    /// ⚠ New in slice 8. The register has an activate/deactivate pair and had no way to page the
    /// inactive half; the parameter did not exist, so the screen could only ever show everyone.
    /// </param>
    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public Task<IActionResult> GetPaged(
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 20,
        [FromQuery] string? searchTerm  = null,
        [FromQuery] bool?   isActive    = null,
        CancellationToken ct = default) =>
        RunAsync(() => _service.GetPagedAsync(pageNumber, pageSize, searchTerm, isActive, ct),
            "page the external associates");

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public Task<IActionResult> GetActive(CancellationToken ct) =>
        RunAsync(() => _service.GetActiveAsync(ct), "list the active external associates");

    /// <summary>
    /// Typeahead search — used by the interview panel picker.
    /// Requires at least 2 characters; returns up to <paramref name="limit"/> results.
    /// </summary>
    [HttpGet("search")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<ExternalAssociateSearchResultDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<ExternalAssociateSearchResultDto>());

        return Ok(await _service.SearchAsync(q, limit, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        RunAsync(() => _service.GetByIdAsync(id, ct), "read the external associate");

    /// <remarks>
    /// ⚠ Answers <b>404</b> when the number names nobody. It used to answer 200 with a null body —
    /// a "not found" the caller had to detect by inspecting the payload.
    /// </remarks>
    [HttpGet("number/{associateNumber}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public Task<IActionResult> GetByNumber(string associateNumber, CancellationToken ct) =>
        RunAsync(() => _service.GetByAssociateNumberAsync(associateNumber, ct),
            "read the external associate by number");

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExternalAssociateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest(new { message = "Tenant context could not be resolved." });
        if (employeeId == null) return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            var result = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)            { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex)    { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex)  { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateExternalAssociateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.Id)       return BadRequest(new { message = "ID in URL does not match body." });

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
        }
        catch (ArgumentException ex)            { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex)    { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex)  { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    /// <remarks>
    /// ⚠ Refused with a <b>409</b> naming the count when the associate sits on an interview panel.
    /// The delete is a soft delete, so the <c>OnDelete.Restrict</c> on the panel's foreign key never
    /// fires; before this slice the associate simply vanished from every panel that carried them.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (ArgumentException ex)         { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var empId = _currentUser.EmployeeId;
        if (empId == null) return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            return Ok(await _service.ActivateAsync(id, empId.Value, ct));
        }
        catch (ArgumentException ex)         { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var empId = _currentUser.EmployeeId;
        if (empId == null) return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            return Ok(await _service.DeactivateAsync(id, empId.Value, ct));
        }
        catch (ArgumentException ex)         { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
