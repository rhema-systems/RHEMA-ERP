using System.Security.Claims;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/tools")]
[Authorize]
public class ToolCheckoutController : ControllerBase
{
    private readonly IToolCheckoutService _toolCheckoutService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ToolCheckoutController> _logger;

    public ToolCheckoutController(
        IToolCheckoutService toolCheckoutService,
        ICurrentUserService currentUserService,
        ILogger<ToolCheckoutController> logger)
    {
        _toolCheckoutService = toolCheckoutService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Tool Management

    /// <summary>
    /// Get all available tools
    /// </summary>
    [HttpGet("available")]
    public async Task<ActionResult<List<MaintenanceToolDto>>> GetAvailableTools()
    {
        try
        {
            var tools = await _toolCheckoutService.GetAvailableToolsAsync();
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tools");
            return StatusCode(500, "An error occurred while retrieving available tools");
        }
    }

    /// <summary>
    /// Get all tools
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<List<MaintenanceToolDto>>> GetAllTools()
    {
        try
        {
            var tools = await _toolCheckoutService.GetAllToolsAsync();
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tools");
            return StatusCode(500, "An error occurred while retrieving tools");
        }
    }

    /// <summary>
    /// Get tool by ID
    /// </summary>
    [HttpGet("{toolId}")]
    public async Task<ActionResult<MaintenanceToolDto>> GetToolById(Guid toolId)
    {
        try
        {
            var tool = await _toolCheckoutService.GetToolByIdAsync(toolId);
            if (tool == null)
            {
                return NotFound($"Tool {toolId} not found");
            }

            return Ok(tool);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool {ToolId}", toolId);
            return StatusCode(500, "An error occurred while retrieving the tool");
        }
    }

    /// <summary>
    /// Check tool availability
    /// </summary>
    [HttpGet("{toolId}/availability")]
    public async Task<ActionResult<ToolAvailabilityDto>> CheckToolAvailability(
        Guid toolId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var availability = await _toolCheckoutService.CheckToolAvailabilityAsync(toolId, startDate, endDate);
            return Ok(availability);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tool {ToolId} not found", toolId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability for tool {ToolId}", toolId);
            return StatusCode(500, "An error occurred while checking tool availability");
        }
    }

    /// <summary>
    /// Get tool checkout history
    /// </summary>
    [HttpGet("{toolId}/history")]
    public async Task<ActionResult<ToolCheckoutHistoryDto>> GetToolHistory(
        Guid toolId,
        [FromQuery] int limit = 50)
    {
        try
        {
            var history = await _toolCheckoutService.GetToolCheckoutHistoryAsync(toolId, limit);
            return Ok(history);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tool {ToolId} not found", toolId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for tool {ToolId}", toolId);
            return StatusCode(500, "An error occurred while retrieving tool history");
        }
    }

    #endregion

    #region Checkout Operations

    /// <summary>
    /// Checkout a tool to an employee
    /// </summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<ToolCheckoutResult>> CheckoutTool(
        [FromQuery] Guid toolId,
        [FromQuery] Guid employeeId,
        [FromBody] CheckoutToolDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _toolCheckoutService.CheckoutToolAsync(toolId, employeeId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid checkout request for tool {ToolId}", toolId);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot checkout tool {ToolId}", toolId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out tool {ToolId}", toolId);
            return StatusCode(500, "An error occurred while checking out the tool");
        }
    }

    /// <summary>
    /// Return a checked-out tool
    /// </summary>
    [HttpPost("return/{checkoutId}")]
    public async Task<ActionResult<ToolReturnResult>> ReturnTool(
        Guid checkoutId,
        [FromBody] ReturnToolDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Use current application user (Users table) as the person performing the return
            if (string.IsNullOrEmpty(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
            {
                return BadRequest("Current user is not linked to an application user");
            }

            var result = await _toolCheckoutService.ReturnToolAsync(checkoutId, userId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Checkout {CheckoutId} not found", checkoutId);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot return tool for checkout {CheckoutId}", checkoutId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning tool for checkout {CheckoutId}", checkoutId);
            return StatusCode(500, "An error occurred while returning the tool");
        }
    }

    /// <summary>
    /// Report damage for a checked-out tool
    /// </summary>
    [HttpPost("checkouts/{checkoutId}/damage")]
    public async Task<ActionResult> ReportToolDamage(
        Guid checkoutId,
        [FromBody] ToolDamageDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _toolCheckoutService.ReportToolDamageAsync(checkoutId, dto);
            return Ok(new { message = "Damage reported successfully" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Checkout {CheckoutId} not found", checkoutId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting damage for checkout {CheckoutId}", checkoutId);
            return StatusCode(500, "An error occurred while reporting damage");
        }
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Get active checkouts (optionally filtered by employee)
    /// </summary>
    [HttpGet("checkouts/active")]
    public async Task<ActionResult<List<ToolCheckoutDto>>> GetActiveCheckouts(
        [FromQuery] Guid? employeeId = null)
    {
        try
        {
            var checkouts = await _toolCheckoutService.GetActiveCheckoutsAsync(employeeId);
            return Ok(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active checkouts");
            return StatusCode(500, "An error occurred while retrieving active checkouts");
        }
    }

    /// <summary>
    /// Get overdue checkouts
    /// </summary>
    [HttpGet("checkouts/overdue")]
    public async Task<ActionResult<List<ToolCheckoutDto>>> GetOverdueCheckouts()
    {
        try
        {
            var checkouts = await _toolCheckoutService.GetOverdueCheckoutsAsync();
            return Ok(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue checkouts");
            return StatusCode(500, "An error occurred while retrieving overdue checkouts");
        }
    }

    /// <summary>
    /// Get employee checkout history
    /// </summary>
    [HttpGet("employees/{employeeId}/checkouts")]
    public async Task<ActionResult<List<ToolCheckoutDto>>> GetEmployeeCheckoutHistory(
        Guid employeeId,
        [FromQuery] int limit = 50)
    {
        try
        {
            var checkouts = await _toolCheckoutService.GetEmployeeCheckoutHistoryAsync(employeeId, limit);
            return Ok(checkouts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkout history for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving employee checkout history");
        }
    }

    #endregion
}
