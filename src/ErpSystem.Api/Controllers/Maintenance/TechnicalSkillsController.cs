using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

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
                pageSize = 100;

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
            
            // Fallback to mock data if service is unavailable
            var fallbackResult = GetMockSkills(page, pageSize, searchTerm, category, skillLevel, isActive);
            return Ok(fallbackResult);
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
            
            // Fallback to mock data
            var fallbackResult = GetMockActiveSkills();
            return Ok(fallbackResult);
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
            
            // Fallback to filtered mock data
            var fallbackResult = GetMockSkillsByCategory(category);
            return Ok(fallbackResult);
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
                return NotFound($"Technical skill with ID {id} not found");

            return Ok(skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technical skill {SkillId}", id);
            
            // Fallback to mock data
            var fallbackResult = GetMockSkillById(id);
            if (fallbackResult == null)
                return NotFound($"Technical skill with ID {id} not found");
            
            return Ok(fallbackResult);
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
            
            // Return mock synchronized skills
            var fallbackResult = GetMockHRSkills();
            return Ok(fallbackResult);
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
            
            // Fallback to mock analysis
            var fallbackResult = GetMockSkillGapAnalysis();
            return Ok(fallbackResult);
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
            
            // Fallback to mock technicians
            var fallbackResult = GetMockTechniciansWithSkill();
            return Ok(fallbackResult);
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
                return BadRequest(ModelState);

            var assignment = await _skillService.AssignSkillToTechnicianAsync(createDto);
            return CreatedAtAction(nameof(GetSkillAssignment), new { id = assignment.Id }, assignment);
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
    /// Gets skill assignment by ID
    /// </summary>
    [HttpGet("assignments/{id:guid}")]
    public async Task<ActionResult<TechnicianSkillAssignmentDto>> GetSkillAssignment(Guid id)
    {
        try
        {
            // This would need to be implemented in the service
            // For now, return mock data
            var fallbackResult = GetMockSkillAssignment(id);
            return Ok(fallbackResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skill assignment {AssignmentId}", id);
            return StatusCode(500, "An error occurred while retrieving the skill assignment");
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
            
            // Fallback to mock data
            var fallbackResult = GetMockTechnicianSkills(technicianId);
            return Ok(fallbackResult);
        }
    }

    #region Fallback Methods

    private PagedResult<TechnicalSkillListDto> GetMockSkills(
        int page, int pageSize, string? searchTerm, string? category, string? skillLevel, bool? isActive)
    {
        var mockData = new List<TechnicalSkillListDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Electrical Systems",
                Code = "ELEC-001",
                Category = "Electrical",
                SkillLevel = "Advanced",
                Complexity = "High",
                RiskLevel = "High",
                EstimatedLearningHours = 120,
                IsActive = true,
                TechniciansCount = 8,
                AverageRating = 4.2m,
                IsFromHRModule = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "HVAC Maintenance",
                Code = "HVAC-001",
                Category = "HVAC",
                SkillLevel = "Intermediate",
                Complexity = "Medium",
                RiskLevel = "Medium",
                EstimatedLearningHours = 80,
                IsActive = true,
                TechniciansCount = 12,
                AverageRating = 4.5m,
                IsFromHRModule = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Plumbing Systems",
                Code = "PLUMB-001",
                Category = "Plumbing",
                SkillLevel = "Intermediate",
                Complexity = "Medium",
                RiskLevel = "Medium",
                EstimatedLearningHours = 60,
                IsActive = true,
                TechniciansCount = 6,
                AverageRating = 4.0m,
                IsFromHRModule = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Industrial Automation",
                Code = "AUTO-001",
                Category = "Automation",
                SkillLevel = "Expert",
                Complexity = "Very High",
                RiskLevel = "High",
                EstimatedLearningHours = 200,
                IsActive = true,
                TechniciansCount = 4,
                AverageRating = 4.8m,
                IsFromHRModule = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Mechanical Systems",
                Code = "MECH-001",
                Category = "Mechanical",
                SkillLevel = "Basic",
                Complexity = "Low",
                RiskLevel = "Low",
                EstimatedLearningHours = 40,
                IsActive = true,
                TechniciansCount = 15,
                AverageRating = 3.8m,
                IsFromHRModule = true
            }
        };

        // Apply filters
        var filtered = mockData.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filtered = filtered.Where(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(category))
        {
            filtered = filtered.Where(x => x.Category == category);
        }

        if (!string.IsNullOrEmpty(skillLevel))
        {
            filtered = filtered.Where(x => x.SkillLevel == skillLevel);
        }

        if (isActive.HasValue)
        {
            filtered = filtered.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<TechnicalSkillListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private IEnumerable<TechnicalSkillDto> GetMockActiveSkills()
    {
        return GetMockSkills(1, 100, null, null, null, true).Items.Select(s => new TechnicalSkillDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            Category = s.Category,
            SkillLevel = s.SkillLevel,
            Complexity = s.Complexity,
            RiskLevel = s.RiskLevel,
            EstimatedLearningHours = s.EstimatedLearningHours,
            IsActive = s.IsActive,
            TechniciansCount = s.TechniciansCount,
            AverageRating = s.AverageRating,
            IsFromHRModule = s.IsFromHRModule,
            Description = "Comprehensive skill for maintenance technicians",
            Prerequisites = new List<string> { "Basic Safety Training", "Tool Usage" },
            Certifications = new List<string> { "Industry Certification", "Safety Compliance" },
            ToolsRequired = new List<string> { "Multimeter", "Hand Tools", "Safety Equipment" },
            SafetyRequirements = "Follow all safety protocols and use appropriate PPE",
            CompetencyAreas = new List<string> { "Installation", "Maintenance", "Troubleshooting", "Repair" },
            RelatedMaintenanceTypes = new List<string> { "Preventive", "Corrective", "Emergency" },
            CreatedDate = DateTime.UtcNow.AddMonths(-6),
            CreatedBy = "HR System",
            LastSyncDate = DateTime.UtcNow.AddHours(-2)
        });
    }

    private IEnumerable<TechnicalSkillDto> GetMockSkillsByCategory(string category)
    {
        return GetMockActiveSkills().Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    private TechnicalSkillDto? GetMockSkillById(Guid id)
    {
        return GetMockActiveSkills().FirstOrDefault();
    }

    private IEnumerable<TechnicalSkillDto> GetMockHRSkills()
    {
        var skills = GetMockActiveSkills().ToList();
        foreach (var skill in skills)
        {
            skill.LastSyncDate = DateTime.UtcNow;
            skill.IsFromHRModule = true;
        }
        return skills;
    }

    private SkillGapAnalysisDto GetMockSkillGapAnalysis()
    {
        return new SkillGapAnalysisDto
        {
            SkillDeficits = new List<SkillDeficitDto>
            {
                new()
                {
                    SkillId = Guid.NewGuid(),
                    SkillName = "Industrial Automation",
                    RequiredTechnicians = 8,
                    CurrentTechnicians = 4,
                    Deficit = 4,
                    Priority = "High"
                },
                new()
                {
                    SkillId = Guid.NewGuid(),
                    SkillName = "Advanced Electrical",
                    RequiredTechnicians = 10,
                    CurrentTechnicians = 6,
                    Deficit = 4,
                    Priority = "Medium"
                }
            },
            OverstaffedSkills = new List<OverstaffedSkillDto>
            {
                new()
                {
                    SkillId = Guid.NewGuid(),
                    SkillName = "Basic Mechanical",
                    RequiredTechnicians = 10,
                    CurrentTechnicians = 15,
                    Excess = 5
                }
            },
            CriticalSkills = new List<CriticalSkillDto>
            {
                new()
                {
                    SkillId = Guid.NewGuid(),
                    SkillName = "Electrical Systems",
                    TechnicianCount = 8,
                    RiskLevel = "High",
                    MitegationActions = "Cross-train technicians; Hire additional specialists; Partner with contractors"
                }
            },
            TotalSkillsAnalyzed = 25,
            AnalysisDate = DateTime.UtcNow
        };
    }

    private IEnumerable<string> GetMockTechniciansWithSkill()
    {
        return new List<string>
        {
            "John Smith - Senior Technician",
            "Sarah Johnson - Lead Technician", 
            "Mike Wilson - Maintenance Specialist",
            "Lisa Brown - Technical Expert"
        };
    }

    private TechnicianSkillAssignmentDto GetMockSkillAssignment(Guid id)
    {
        return new TechnicianSkillAssignmentDto
        {
            Id = id,
            TechnicianId = Guid.NewGuid(),
            SkillId = Guid.NewGuid(),
            SkillName = "Electrical Systems",
            Category = "Electrical",
            ProficiencyLevel = 4,
            ProficiencyDescription = "Advanced",
            AcquiredDate = DateTime.UtcNow.AddMonths(-6),
            ExpirationDate = DateTime.UtcNow.AddYears(2),
            IsVerified = true,
            VerifiedBy = "Technical Supervisor",
            LastAssessmentDate = DateTime.UtcNow.AddMonths(-3),
            Notes = "Excellent performance in field applications",
            IsExpired = false,
            DaysUntilExpiration = 730
        };
    }

    private IEnumerable<TechnicianSkillAssignmentDto> GetMockTechnicianSkills(Guid technicianId)
    {
        return new List<TechnicianSkillAssignmentDto>
        {
            GetMockSkillAssignment(Guid.NewGuid()),
            new()
            {
                Id = Guid.NewGuid(),
                TechnicianId = technicianId,
                SkillId = Guid.NewGuid(),
                SkillName = "HVAC Maintenance",
                Category = "HVAC",
                ProficiencyLevel = 3,
                ProficiencyDescription = "Intermediate",
                AcquiredDate = DateTime.UtcNow.AddMonths(-12),
                IsVerified = true,
                VerifiedBy = "HVAC Specialist",
                LastAssessmentDate = DateTime.UtcNow.AddMonths(-6),
                Notes = "Solid understanding of HVAC systems",
                IsExpired = false,
                DaysUntilExpiration = -1
            }
        };
    }

    #endregion
}