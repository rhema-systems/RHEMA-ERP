using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// API controller for departments (used for dropdown selections)
/// </summary>
[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(
        IDepartmentService departmentService,
        ILogger<DepartmentsController> logger)
    {
        _departmentService = departmentService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all departments
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
    {
        try
        {
            var departments = await _departmentService.GetAllAsync();
            return Ok(departments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500, "An error occurred while retrieving departments");
        }
    }

    /// <summary>
    /// Gets active departments for dropdowns
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetActive()
    {
        try
        {
            var departments = await _departmentService.GetActiveDepartmentsAsync();
            return Ok(departments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active departments");
            return StatusCode(500, "An error occurred while retrieving active departments");
        }
    }

    /// <summary>
    /// Gets a department by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetById(Guid id)
    {
        try
        {
            var department = await _departmentService.GetByIdAsync(id);
            if (department == null)
                return NotFound($"Department with ID {id} not found");

            return Ok(department);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving department {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the department");
        }
    }
}

