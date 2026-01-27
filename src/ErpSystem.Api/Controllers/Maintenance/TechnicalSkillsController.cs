using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/technical-skills")]
[Authorize]
public class TechnicalSkillsController : ControllerBase
{
    private readonly ITechnicalSkillService _skillService;
    private readonly ILogger<TechnicalSkillsController> _logger;

    public TechnicalSkillsController(
        ITechnicalSkillService skillService,
        ILogger<TechnicalSkillsController> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of technical skills with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TechnicalSkillListDto>>> GetSkills(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? skillLevel = null,
        [FromQuery] string? complexity = null,
        [FromQuery] string? riskLevel = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isFromHRModule = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var filter = new TechnicalSkillFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Category = category,
                SkillLevel = skillLevel,
                Complexity = complexity,
                RiskLevel = riskLevel,
                IsActive = isActive,
                IsFromHRModule = isFromHRModule
            };

            var result = await _skillService.GetSkillsPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technical skills");
            return StatusCode(500, "An error occurred while retrieving technical skills");
        }
    }

    /// <summary>
    /// Gets all active technical skills
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<TechnicalSkillDto>>> GetActiveSkills()
    {
        try
        {
            var result = await _skillService.GetActiveSkillsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active technical skills");
            return StatusCode(500, "An error occurred while retrieving active technical skills");
        }
    }

    /// <summary>
    /// Gets skills by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<TechnicalSkillDto>>> GetSkillsByCategory(string category)
    {
        try
        {
            var result = await _skillService.GetSkillsByCategoryAsync(category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skills for category {Category}", category);
            return StatusCode(500, $"An error occurred while retrieving skills for category {category}");
        }
    }

    /// <summary>
    /// Gets a specific technical skill by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TechnicalSkillDto>> GetSkill(Guid id)
    {
        try
        {
            var skill = await _skillService.GetSkillByIdAsync(id);
            if (skill == null)
            {
                return NotFound($"Technical skill with ID {id} not found");
            }

            return Ok(skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technical skill {SkillId}", id);
            return StatusCode(500, $"An error occurred while retrieving technical skill {id}");
        }
    }

    /// <summary>
    /// Synchronizes skills from HR module
    /// </summary>
    [HttpPost("sync-from-hr")]
    public async Task<ActionResult<IEnumerable<TechnicalSkillDto>>> SyncFromHR()
    {
        try
        {
            var result = await _skillService.SyncSkillsFromHRAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing skills from HR module");
            return StatusCode(500, "An error occurred while synchronizing skills from HR module");
        }
    }

    /// <summary>
    /// Gets skill gap analysis
    /// </summary>
    [HttpGet("gap-analysis")]
    public async Task<ActionResult<SkillGapAnalysisDto>> GetSkillGapAnalysis()
    {
        try
        {
            var result = await _skillService.GetSkillGapAnalysisAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skill gap analysis");
            return StatusCode(500, "An error occurred while retrieving skill gap analysis");
        }
    }

    /// <summary>
    /// Gets technicians with specific skill
    /// </summary>
    [HttpGet("{id:guid}/technicians")]
    public async Task<ActionResult<IEnumerable<string>>> GetTechniciansWithSkill(Guid id, [FromQuery] int? minProficiencyLevel = null)
    {
        try
        {
            var result = await _skillService.GetTechniciansWithSkillAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians with skill {SkillId}", id);
            return StatusCode(500, $"An error occurred while retrieving technicians with skill {id}");
        }
    }

    /// <summary>
    /// Assigns skill to technician (for local skills only)
    /// </summary>
    [HttpPost("assignments")]
    public async Task<ActionResult<TechnicianSkillAssignmentDto>> AssignSkillToTechnician([FromBody] CreateTechnicianSkillAssignmentDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var assignment = await _skillService.AssignSkillToTechnicianAsync(createDto);
            return CreatedAtAction(nameof(GetTechnicianSkills), new { technicianId = createDto.TechnicianId }, assignment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning skill to technician");
            return StatusCode(500, "An error occurred while assigning the skill");
        }
    }


    /// <summary>
    /// Gets technician skill assignments
    /// </summary>
    [HttpGet("technicians/{technicianId:guid}/skills")]
    public async Task<ActionResult<IEnumerable<TechnicianSkillAssignmentDto>>> GetTechnicianSkills(Guid technicianId)
    {
        try
        {
            var result = await _skillService.GetTechnicianSkillsAsync(technicianId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skills for technician {TechnicianId}", technicianId);
            return StatusCode(500, $"An error occurred while retrieving skills for technician {technicianId}");
        }
    }

}
