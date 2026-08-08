using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/orientation-categories")]
[Authorize]
public class OrientationCategoriesController : ControllerBase
{
    private readonly IOrientationCategoryService _service;
    private readonly ICurrentUserService _currentUser;

    public OrientationCategoriesController(IOrientationCategoryService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrientationCategoryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<OrientationCategoryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("root")]
    public async Task<ActionResult<IEnumerable<OrientationCategoryDto>>> GetRoot()
        => Ok(await _service.GetRootCategoriesAsync());

    [HttpGet("lookup")]
    public async Task<ActionResult<IEnumerable<OrientationCategoryLookupDto>>> GetLookup()
        => Ok(await _service.GetLookupAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrientationCategoryDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{parentId:guid}/children")]
    public async Task<ActionResult<IEnumerable<OrientationCategoryDto>>> GetChildren(Guid parentId)
        => Ok(await _service.GetByParentAsync(parentId));

    [HttpPost]
    public async Task<ActionResult<OrientationCategoryDto>> Create([FromBody] CreateOrientationCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrientationCategoryDto>> Update(Guid id, [FromBody] UpdateOrientationCategoryDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
