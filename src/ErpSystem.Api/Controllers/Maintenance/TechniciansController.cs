using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/technicians")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianService _technicianService;
    private readonly ILogger<TechniciansController> _logger;

    public TechniciansController(
        ITechnicianService technicianService,
        ILogger<TechniciansController> logger)
    {
        _technicianService = technicianService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of technicians with optional filtering (read-only from HR module)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TechnicianListDto>>> GetTechnicians(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? department = null,
        [FromQuery] string? jobTitle = null,
        [FromQuery] string? employmentStatus = null,
        [FromQuery] string? experienceLevel = null,
        [FromQuery] string? shiftSchedule = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] Guid? skillId = null,
        [FromQuery] string? specialization = null,
        [FromQuery] string? location = null,
        [FromQuery] bool? hasExpiredCertifications = null,
        [FromQuery] bool? hasExpiringCertifications = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new TechnicianFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Department = department,
                JobTitle = jobTitle,
                EmploymentStatus = employmentStatus,
                ExperienceLevel = experienceLevel,
                ShiftSchedule = shiftSchedule,
                IsAvailable = isAvailable,
                SkillId = skillId,
                Specialization = specialization,
                Location = location,
                HasExpiredCertifications = hasExpiredCertifications,
                HasExpiringCertifications = hasExpiringCertifications
            };

            var result = await _technicianService.GetTechniciansPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians from HR module");
            return StatusCode(500, "An error occurred while retrieving technicians");
        }
    }

    /// <summary>
    /// Gets all available technicians
    /// </summary>
    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetAvailableTechnicians(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        try
        {
            var start = startTime ?? DateTime.UtcNow;
            var end = endTime ?? DateTime.UtcNow.AddDays(30);
            var result = await _technicianService.GetAvailableTechniciansAsync(start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available technicians");
            return StatusCode(500, "An error occurred while retrieving available technicians");
        }
    }

    /// <summary>
    /// Gets technicians by department (using location-based filtering)
    /// </summary>
    [HttpGet("department/{department}")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansByDepartment(string department)
    {
        try
        {
            // Use paged filtering to get technicians by department
            var filter = new TechnicianFilterDto
            {
                Page = 1,
                PageSize = 1000, // Get all for department
                Department = department
            };
            var result = await _technicianService.GetTechniciansPagedAsync(filter);
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for department {Department}", department);
            return StatusCode(500, $"An error occurred while retrieving technicians for department {department}");
        }
    }

    /// <summary>
    /// Gets technicians by location
    /// </summary>
    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansByLocation(Guid locationId)
    {
        try
        {
            var result = await _technicianService.GetTechniciansByLocationAsync(locationId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for location {LocationId}", locationId);
            return StatusCode(500, $"An error occurred while retrieving technicians for location {locationId}");
        }
    }

    /// <summary>
    /// Gets a specific technician by ID (read-only)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TechnicianDto>> GetTechnician(Guid id)
    {
        try
        {
            var technician = await _technicianService.GetTechnicianByIdAsync(id);
            if (technician == null)
                return NotFound($"Technician with ID {id} not found in HR module");

            return Ok(technician);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician {TechnicianId} from HR module", id);
            return StatusCode(500, $"An error occurred while retrieving technician {id}");
        }
    }

    /// <summary>
    /// Gets all active technicians (HR sync is handled internally)
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetActiveTechnicians()
    {
        try
        {
            var result = await _technicianService.GetActiveTechniciansAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active technicians");
            return StatusCode(500, "An error occurred while retrieving active technicians");
        }
    }

    /// <summary>
    /// Gets technician workload information
    /// </summary>
    [HttpGet("{id:guid}/workload")]
    public async Task<ActionResult<TechnicianWorkloadDto>> GetTechnicianWorkload(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;
            var result = await _technicianService.GetTechnicianWorkloadAsync(id, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workload for technician {TechnicianId}", id);
            return StatusCode(500, $"An error occurred while retrieving workload for technician {id}");
        }
    }

    /// <summary>
    /// Gets technician analytics (performance and other metrics)
    /// </summary>
    [HttpGet("{id:guid}/analytics")]
    public async Task<ActionResult<TechnicianAnalyticsDto>> GetTechnicianAnalytics(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var result = await _technicianService.GetTechnicianAnalyticsAsync(id, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics for technician {TechnicianId}", id);
            return StatusCode(500, $"An error occurred while retrieving analytics for technician {id}");
        }
    }

    /// <summary>
    /// Gets technician availability status
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<TechnicianAvailabilityDto>> GetTechnicianAvailability(
        Guid id,
        [FromQuery] DateTime? date = null)
    {
        try
        {
            var checkDate = date ?? DateTime.UtcNow.Date;
            var result = await _technicianService.GetTechnicianAvailabilityAsync(id, checkDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for technician {TechnicianId}", id);
            return StatusCode(500, $"An error occurred while retrieving availability for technician {id}");
        }
    }

    /// <summary>
    /// Gets technicians with specific skills
    /// </summary>
    [HttpGet("with-skill/{skillId:guid}")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansWithSkill(Guid skillId)
    {
        try
        {
            var result = await _technicianService.GetTechniciansBySkillAsync(skillId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians with skill {SkillId}", skillId);
            return StatusCode(500, $"An error occurred while retrieving technicians with skill {skillId}");
        }
    }

    /// <summary>
    /// Gets technician with skills information
    /// </summary>
    [HttpGet("{id:guid}/with-skills")]
    public async Task<ActionResult<TechnicianDto>> GetTechnicianWithSkills(Guid id)
    {
        try
        {
            var result = await _technicianService.GetTechnicianWithSkillsAsync(id);
            if (result == null)
                return NotFound($"Technician with ID {id} not found");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician with skills {TechnicianId}", id);
            return StatusCode(500, $"An error occurred while retrieving technician with skills {id}");
        }
    }
}