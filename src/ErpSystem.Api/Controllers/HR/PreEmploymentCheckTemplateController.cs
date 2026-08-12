using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Reusable pre-employment check templates — "which checks a hire of this kind needs".
///
/// <para>⚠ Setup data, and previously the worst-scoped surface in recruitment: the service behind
/// this controller had no tenancy at all, so the list returned every tenant's templates and every
/// by-id operation acted on whichever tenant owned that id. Scoping is now explicit in the service
/// and its repository.</para>
/// </summary>
[ApiController]
[Route("api/pre-employment-check-templates")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
[RecruitmentBusinessRules]
public class PreEmploymentCheckTemplateController : ControllerBase
{
    private readonly IPreEmploymentCheckTemplateService _service;
    private readonly ICurrentUserService _currentUser;

    public PreEmploymentCheckTemplateController(
        IPreEmploymentCheckTemplateService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckTemplateDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PreEmploymentCheckTemplateDetailDto>> GetWithItems(Guid id)
        => Ok(await _service.GetWithItemsAsync(id));

    // =========================================================================
    // CRUD

    [HttpPost]
    public async Task<ActionResult<PreEmploymentCheckTemplateDetailDto>> Create(
        [FromBody] CreatePreEmploymentCheckTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetWithItems), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PreEmploymentCheckTemplateDto>> Update(
        Guid id,
        [FromBody] UpdatePreEmploymentCheckTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.Id) return BadRequest("Route ID does not match payload ID.");

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.UpdateAsync(dto, employeeId.Value);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // ITEMS

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<PreEmploymentCheckTemplateItemDto>> AddItem(
        Guid id,
        [FromBody] CreatePreEmploymentCheckTemplateItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.TemplateId = id;

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.AddItemAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<PreEmploymentCheckTemplateItemDto>> UpdateItem(
        Guid id,
        Guid itemId,
        [FromBody] UpdatePreEmploymentCheckTemplateItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (itemId != dto.Id) return BadRequest("Route item ID does not match payload ID.");

        dto.TemplateId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.UpdateItemAsync(dto, employeeId.Value);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId)
    {
        // The route always carried the owning template id; the service simply ignored it and found
        // the item by scanning every tenant's templates. It is passed through now.
        await _service.DeleteItemAsync(id, itemId);
        return NoContent();
    }
}
