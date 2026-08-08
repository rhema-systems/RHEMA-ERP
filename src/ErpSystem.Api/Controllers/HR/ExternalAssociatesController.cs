using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/external-associates")]
[Authorize]
public class ExternalAssociatesController : ControllerBase
{
    private readonly IExternalAssociateService _service;
    private readonly ICurrentUserService       _currentUser;

    public ExternalAssociatesController(
        IExternalAssociateService service,
        ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExternalAssociateSummaryDto>>> GetAll(CancellationToken ct) =>
        Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ExternalAssociateSummaryDto>>> GetPaged(
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 20,
        [FromQuery] string? searchTerm  = null,
        CancellationToken ct = default) =>
        Ok(await _service.GetPagedAsync(pageNumber, pageSize, searchTerm, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ExternalAssociateSummaryDto>>> GetActive(CancellationToken ct) =>
        Ok(await _service.GetActiveAsync(ct));

    /// <summary>
    /// Typeahead search — used by the interview panel picker.
    /// Requires at least 2 characters; returns up to <paramref name="limit"/> results.
    /// </summary>
    [HttpGet("search")]
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
    public async Task<ActionResult<ExternalAssociateDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id, ct));
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet("number/{associateNumber}")]
    public async Task<ActionResult<ExternalAssociateDto?>> GetByNumber(
        string associateNumber, CancellationToken ct) =>
        Ok(await _service.GetByAssociateNumberAsync(associateNumber, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<ExternalAssociateDto>> Create(
        [FromBody] CreateExternalAssociateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            var result = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExternalAssociateDto>> Update(
        Guid id, [FromBody] UpdateExternalAssociateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.Id)       return BadRequest(new { message = "ID in URL does not match body." });

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
        }
        catch (ArgumentException)            { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (ArgumentException) { return NotFound(); }
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ExternalAssociateDto>> Activate(Guid id, CancellationToken ct)
    {
        var empId = _currentUser.EmployeeId;
        if (empId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            return Ok(await _service.ActivateAsync(id, empId.Value, ct));
        }
        catch (ArgumentException)            { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ExternalAssociateDto>> Deactivate(Guid id, CancellationToken ct)
    {
        var empId = _currentUser.EmployeeId;
        if (empId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            return Ok(await _service.DeactivateAsync(id, empId.Value, ct));
        }
        catch (ArgumentException)            { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

