using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// CRUD for tenant-configurable talent pool types (replaces the former TalentPoolType enum).
/// </summary>
[ApiController]
[Route("api/talent-pool-types")]
[Authorize]
public class TalentPoolTypesController : ControllerBase
{
    private readonly ITalentPoolTypeDefinitionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TalentPoolTypesController> _logger;

    public TalentPoolTypesController(
        ITalentPoolTypeDefinitionService service,
        ICurrentUserService currentUser,
        ILogger<TalentPoolTypesController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TalentPoolTypeDefinitionDto>>> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TalentPoolTypeDefinitionDto>> GetById(Guid id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TalentPoolTypeDefinitionDto>> Create([FromBody] CreateTalentPoolTypeDefinitionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TalentPoolTypeDefinitionDto>> Update(Guid id, [FromBody] UpdateTalentPoolTypeDefinitionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            return Ok(await _service.UpdateAsync(dto, employeeId.Value));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
