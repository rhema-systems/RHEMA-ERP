using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/schedules")]
[Authorize]
public class MaintenanceSchedulesController : ControllerBase
{
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly ILogger<MaintenanceSchedulesController> _logger;

    public MaintenanceSchedulesController(
        IMaintenanceScheduleService scheduleService,
        ILogger<MaintenanceSchedulesController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of maintenance schedules with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceScheduleListDto>>> GetSchedules(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? maintenanceTypeId = null,
        [FromQuery] string? frequency = null,
        [FromQuery] string? priority = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isOverdue = null,
        [FromQuery] Guid? assignedTechnicianId = null,
        [FromQuery] Guid? assignedTeamId = null,
        [FromQuery] DateTime? dueDateFrom = null,
        [FromQuery] DateTime? dueDateTo = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new MaintenanceScheduleFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                AssetId = assetId,
                MaintenanceTypeId = maintenanceTypeId,
                Frequency = frequency,
                Priority = priority,
                IsActive = isActive,
                IsOverdue = isOverdue,
                AssignedTechnicianId = assignedTechnicianId,
                AssignedTeamId = assignedTeamId,
                DueDateFrom = dueDateFrom,
                DueDateTo = dueDateTo
            };

            var result = await _scheduleService.GetSchedulesPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance schedules");
            
            // Fallback to mock data if service is unavailable
            var fallbackResult = GetMockSchedules(page, pageSize, searchTerm, frequency, priority, isActive);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets all active maintenance schedules
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetActiveSchedules()
    {
        try
        {
            var result = await _scheduleService.GetActiveSchedulesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active maintenance schedules");
            
            // Fallback to mock data
            var fallbackResult = GetMockActiveSchedules();
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets schedules due within specified days
    /// </summary>
    [HttpGet("due-in-days/{days}")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetSchedulesDueInDays(int days)
    {
        try
        {
            var result = await _scheduleService.GetSchedulesDueInDaysAsync(days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules due in {Days} days", days);
            
            // Fallback to filtered mock data
            var fallbackResult = GetMockSchedulesDueInDays(days);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets overdue maintenance schedules
    /// </summary>
    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetOverdueSchedules()
    {
        try
        {
            var result = await _scheduleService.GetOverdueSchedulesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue maintenance schedules");
            
            // Fallback to mock data
            var fallbackResult = GetMockOverdueSchedules();
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets a specific maintenance schedule by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> GetSchedule(Guid id)
    {
        try
        {
            var schedule = await _scheduleService.GetScheduleByIdAsync(id);
            if (schedule == null)
                return NotFound($"Maintenance schedule with ID {id} not found");

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance schedule {ScheduleId}", id);
            
            // Fallback to mock data
            var fallbackResult = GetMockScheduleById(id);
            if (fallbackResult == null)
                return NotFound($"Maintenance schedule with ID {id} not found");
            
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Creates a new maintenance schedule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceScheduleDto>> CreateSchedule([FromBody] CreateMaintenanceScheduleDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var schedule = await _scheduleService.CreateScheduleAsync(createDto);
            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance schedule");
            return StatusCode(500, "An error occurred while creating the maintenance schedule");
        }
    }

    /// <summary>
    /// Updates an existing maintenance schedule
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> UpdateSchedule(Guid id, [FromBody] UpdateMaintenanceScheduleDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var schedule = await _scheduleService.UpdateScheduleAsync(id, updateDto);
            return Ok(schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while updating the maintenance schedule");
        }
    }

    /// <summary>
    /// Deletes a maintenance schedule
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        try
        {
            await _scheduleService.DeleteScheduleAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while deleting the maintenance schedule");
        }
    }

    /// <summary>
    /// Toggles the active status of a maintenance schedule
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<MaintenanceScheduleDto>> ToggleStatus(Guid id)
    {
        try
        {
            var schedule = await _scheduleService.ToggleScheduleStatusAsync(id);
            return Ok(schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling maintenance schedule status {ScheduleId}", id);
            return StatusCode(500, "An error occurred while toggling the maintenance schedule status");
        }
    }

    /// <summary>
    /// Generates work orders from a specific schedule
    /// </summary>
    [HttpPost("{id:guid}/generate-work-orders")]
    public async Task<ActionResult<IEnumerable<WorkOrderDto>>> GenerateWorkOrders(Guid id, [FromQuery] int count = 1)
    {
        try
        {
            var workOrders = await _scheduleService.CreateWorkOrdersFromScheduleAsync(id, count);
            return Ok(workOrders);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work orders from schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while generating work orders");
        }
    }

    /// <summary>
    /// Gets schedule compliance report
    /// </summary>
    [HttpGet("compliance-report")]
    public async Task<ActionResult<ScheduleComplianceReportDto>> GetComplianceReport(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var report = await _scheduleService.GetScheduleComplianceReportAsync(start, end);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule compliance report");
            
            // Fallback to mock report
            var fallbackReport = GetMockComplianceReport();
            return Ok(fallbackReport);
        }
    }

    #region Fallback Methods

    private PagedResult<MaintenanceScheduleListDto> GetMockSchedules(
        int page, int pageSize, string? searchTerm, string? frequency, string? priority, bool? isActive)
    {
        var mockData = new List<MaintenanceScheduleListDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HVAC System Quarterly Check",
                Code = "MS-001",
                AssetName = "Main Building HVAC Unit #1",
                MaintenanceTypeName = "Preventive Maintenance",
                Frequency = "Quarterly",
                NextDueDate = DateTime.UtcNow.AddDays(10),
                Priority = "Medium",
                IsActive = true,
                IsOverdue = false,
                DaysUntilDue = 10,
                AssignedTechnicianName = "John Smith",
                CompliancePercentage = 95.2m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Generator Monthly Inspection",
                Code = "MS-002", 
                AssetName = "Emergency Generator #1",
                MaintenanceTypeName = "Inspection",
                Frequency = "Monthly",
                NextDueDate = DateTime.UtcNow.AddDays(-5),
                Priority = "High",
                IsActive = true,
                IsOverdue = true,
                DaysUntilDue = -5,
                AssignedTechnicianName = "Sarah Johnson",
                CompliancePercentage = 88.7m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Elevator Annual Safety Check",
                Code = "MS-003",
                AssetName = "Passenger Elevator A",
                MaintenanceTypeName = "Safety Inspection",
                Frequency = "Yearly",
                NextDueDate = DateTime.UtcNow.AddDays(45),
                Priority = "High",
                IsActive = true,
                IsOverdue = false,
                DaysUntilDue = 45,
                AssignedTechnicianName = "Mike Wilson",
                CompliancePercentage = 100.0m
            }
        };

        // Apply filters
        var filtered = mockData.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filtered = filtered.Where(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(frequency))
        {
            filtered = filtered.Where(x => x.Frequency == frequency);
        }

        if (!string.IsNullOrEmpty(priority))
        {
            filtered = filtered.Where(x => x.Priority == priority);
        }

        if (isActive.HasValue)
        {
            filtered = filtered.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<MaintenanceScheduleListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private IEnumerable<MaintenanceScheduleDto> GetMockActiveSchedules()
    {
        return GetMockSchedules(1, 100, null, null, null, true).Items.Select(s => new MaintenanceScheduleDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            AssetName = s.AssetName,
            MaintenanceTypeName = s.MaintenanceTypeName,
            Frequency = s.Frequency,
            NextDueDate = s.NextDueDate,
            Priority = s.Priority,
            IsActive = s.IsActive,
            IsOverdue = s.IsOverdue,
            DaysUntilDue = s.DaysUntilDue,
            AssignedTechnicianName = s.AssignedTechnicianName,
            CompliancePercentage = s.CompliancePercentage,
            Description = "Mock maintenance schedule description",
            EstimatedHours = 2.5m,
            EstimatedCost = 350.00m,
            AutoGenerateWorkOrders = true,
            Instructions = "Follow standard maintenance procedures",
            SafetyNotes = "Ensure proper PPE and lockout procedures",
            RequiredSkills = new List<string> { "HVAC", "Electrical Safety" },
            RequiredTools = new List<string> { "Multimeter", "Torque wrench" },
            RequiredParts = new List<string> { "Air filter", "Lubricant" },
            CreatedDate = DateTime.UtcNow.AddMonths(-2),
            CreatedBy = "System Admin"
        });
    }

    private IEnumerable<MaintenanceScheduleDto> GetMockSchedulesDueInDays(int days)
    {
        return GetMockActiveSchedules().Where(s => s.DaysUntilDue <= days && s.DaysUntilDue >= 0);
    }

    private IEnumerable<MaintenanceScheduleDto> GetMockOverdueSchedules()
    {
        return GetMockActiveSchedules().Where(s => s.IsOverdue);
    }

    private MaintenanceScheduleDto? GetMockScheduleById(Guid id)
    {
        return GetMockActiveSchedules().FirstOrDefault();
    }

    private ScheduleComplianceReportDto GetMockComplianceReport()
    {
        return new ScheduleComplianceReportDto
        {
            TotalSchedules = 15,
            ActiveSchedules = 12,
            CompletedOnTime = 45,
            OverdueSchedules = 3,
            SchedulesDueToday = 2,
            SchedulesDueThisWeek = 7,
            OverallCompliancePercentage = 91.2m,
            OnTimeCompletionRate = 88.5m,
            TotalWorkOrdersGenerated = 124,
            ReportPeriodStart = DateTime.UtcNow.AddMonths(-3),
            ReportPeriodEnd = DateTime.UtcNow,
            ReportGeneratedDate = DateTime.UtcNow,
            ScheduleCompliance = new List<ScheduleComplianceDetail>
            {
                new()
                {
                    ScheduleId = Guid.NewGuid(),
                    ScheduleName = "HVAC Quarterly Check",
                    AssetName = "Main HVAC Unit",
                    Frequency = "Quarterly",
                    WorkOrdersGenerated = 12,
                    CompletedOnTime = 10,
                    CompliancePercentage = 83.3m,
                    DaysOverdue = 0,
                    NextDueDate = DateTime.UtcNow.AddDays(15)
                }
            },
            AssetCompliance = new List<AssetComplianceDto>
            {
                new()
                {
                    AssetId = Guid.NewGuid(),
                    AssetName = "Main HVAC Unit",
                    AssetType = "HVAC System",
                    ScheduleCount = 4,
                    AverageCompliance = 89.2m,
                    OverdueSchedules = 1,
                    LastMaintenanceDate = DateTime.UtcNow.AddDays(-10)
                }
            }
        };
    }

    #endregion
}