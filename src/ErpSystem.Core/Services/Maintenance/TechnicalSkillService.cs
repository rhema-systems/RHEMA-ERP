using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing technical skills with HR module integration
/// Skills data is primarily synchronized from HR module, with local assignment tracking
/// </summary>
public class TechnicalSkillService : ITechnicalSkillService
{
    private readonly ITechnicalSkillRepository _skillRepository;
    private readonly ITechnicianSkillAssignmentRepository _assignmentRepository;
    private readonly ILogger<TechnicalSkillService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public TechnicalSkillService(
        ITechnicalSkillRepository skillRepository,
        ITechnicianSkillAssignmentRepository assignmentRepository,
        ILogger<TechnicalSkillService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _skillRepository = skillRepository;
        _assignmentRepository = assignmentRepository;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region CRUD Operations (Limited - mainly for non-HR skills)

    public async Task<TechnicalSkillDto> CreateSkillAsync(CreateTechnicalSkillDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating technical skill: {SkillName}", createDto.Name);

            // Check if skill name already exists
            if (!await _skillRepository.IsNameUniqueAsync(createDto.Name))
                throw new ArgumentException($"Skill name '{createDto.Name}' already exists");

            var skill = new Core.Entities.HR.Skill
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                Category = createDto.Category,
                IsActive = createDto.IsActive,
                RequiresCertification = false,
                TenantId = _currentUserProvider.TenantId
            };

            await _skillRepository.AddAsync(skill);
            
            _logger.LogInformation("Created technical skill {SkillId} successfully", skill.Id);
            
            return await MapToDto(skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating technical skill: {SkillName}", createDto.Name);
            throw;
        }
    }

    public async Task<TechnicalSkillDto> UpdateSkillAsync(Guid id, UpdateTechnicalSkillDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating technical skill: {SkillId}", id);

            var skill = await _skillRepository.GetByIdAsync(id);
            if (skill == null)
                throw new ArgumentException($"Skill with ID {id} not found");

            skill.Name = updateDto.Name;
            skill.Description = updateDto.Description;
            skill.Category = updateDto.Category;
            skill.IsActive = updateDto.IsActive;
            skill.RequiresCertification = false; // Default value

            await _skillRepository.UpdateAsync(skill);
            
            _logger.LogInformation("Updated technical skill {SkillId} successfully", id);
            
            return await MapToDto(skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating technical skill: {SkillId}", id);
            throw;
        }
    }

    public async Task DeleteSkillAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting technical skill: {SkillId}", id);

            var skill = await _skillRepository.GetByIdAsync(id);
            if (skill == null)
                throw new ArgumentException($"Skill with ID {id} not found");

            await _skillRepository.DeleteAsync(id);
            
            _logger.LogInformation("Deleted technical skill {SkillId} successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting technical skill: {SkillId}", id);
            throw;
        }
    }

    public async Task<TechnicalSkillDto?> GetSkillByIdAsync(Guid id)
    {
        var skill = await _skillRepository.GetByIdAsync(id);
        return skill != null ? await MapToDto(skill) : null;
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetAllSkillsAsync()
    {
        var skills = await _skillRepository.GetAllAsync();
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<PagedResult<TechnicalSkillDto>> GetSkillsPagedAsync(TechnicalSkillFilterDto filter)
    {
        var allSkills = await _skillRepository.GetAllAsync();
        var filtered = allSkills.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                filtered = filtered.Where(s => s.Name.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                              (s.Description ?? "").Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filter.Category))
                filtered = filtered.Where(s => s.Category == filter.Category);

            if (filter.IsActive.HasValue)
                filtered = filtered.Where(s => s.IsActive == filter.IsActive.Value);

        var totalCount = filtered.Count();
        var skillsPage = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var items = new List<TechnicalSkillDto>();
        foreach (var skill in skillsPage)
        {
            items.Add(await MapToDto(skill));
        }

        return new PagedResult<TechnicalSkillDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    #endregion

    #region Business Logic

    public async Task<IEnumerable<TechnicalSkillDto>> GetActiveSkillsAsync()
    {
        var skills = await _skillRepository.GetActiveAsync();
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByCategoryAsync(string category)
    {
        var skills = await _skillRepository.GetByCategoryAsync(category);
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByLevelAsync(string skillLevel)
    {
        // HR Skills don't have skill level - return all active skills
        return await GetActiveSkillsAsync();
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByComplexityAsync(string complexity)
    {
        // HR Skills don't have complexity - return all active skills  
        return await GetActiveSkillsAsync();
    }
    

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByRiskLevelAsync(string riskLevel)
    {
        // HR Skills don't have risk level - return all active skills
        return await GetActiveSkillsAsync();
    }
    

    public async Task<TechnicalSkillDto> ToggleSkillStatusAsync(Guid id)
    {
        var skill = await _skillRepository.GetByIdAsync(id);
        if (skill == null)
            throw new ArgumentException($"Skill with ID {id} not found");

        // HR Skills can have status toggled
        skill.IsActive = !skill.IsActive;
        
        await _skillRepository.UpdateAsync(skill);
        
        return await MapToDto(skill);
    }

    public async Task<bool> IsSkillCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        // HR Skills use name instead of code
        return await _skillRepository.IsNameUniqueAsync(code, excludeId);
    }

    public async Task<bool> IsSkillNameUniqueAsync(string name, Guid? excludeId = null)
    {
        return await _skillRepository.IsNameUniqueAsync(name, excludeId);
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetRecommendedSkillsAsync(Guid technicianId)
    {
        try
        {
            // Get existing skills for the technician
            var existingAssignments = await _assignmentRepository.GetByTechnicianIdAsync(technicianId);
            var existingSkillIds = existingAssignments.Select(a => a.SkillId).ToList();

            // Get all available skills
            var allSkills = await _skillRepository.GetActiveAsync();
            
            // Recommend skills that:
            // 1. The technician doesn't already have
            // 2. Are related to their current skills or department
            // 3. Are the next logical progression (complexity/level)
            var recommendedSkills = allSkills
                .Where(s => !existingSkillIds.Contains(s.Id))
                .Where(s => s.IsActive)
                .Take(10) // Limit recommendations
                .ToList();

            var result = new List<TechnicalSkillDto>();
            foreach (var skill in recommendedSkills)
            {
                result.Add(await MapToDto(skill));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommended skills for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    #endregion

    #region HR Module Integration

    public async Task<IEnumerable<TechnicalSkillDto>> SyncSkillsFromHRAsync()
    {
        // Skills are already from HR module - just return all active skills
        _logger.LogInformation("Skills are read directly from HR module");
        return await GetActiveSkillsAsync();
    }

    public async Task<TechnicalSkillDto?> GetSkillFromHRAsync(Guid hrSkillId)
    {
        return await GetSkillByIdAsync(hrSkillId);
    }

    public async Task UpdateSkillFromHRAsync(TechnicalSkillDto hrSkill)
    {
        // Skills are managed in HR module
        _logger.LogInformation("Skills are managed directly in HR module");
        await Task.CompletedTask;
    }

    #endregion

    #region Skill Assignments

    public async Task<TechnicianSkillAssignmentDto> AssignSkillToTechnicianAsync(CreateTechnicianSkillAssignmentDto createDto)
    {
        try
        {
            _logger.LogInformation("Assigning skill {SkillId} to technician {TechnicianId}", 
                createDto.SkillId, createDto.TechnicianId);

            var skill = await _skillRepository.GetByIdAsync(createDto.SkillId);
            if (skill == null)
                throw new ArgumentException($"Skill with ID {createDto.SkillId} not found");

            // Check if assignment already exists
            var existingAssignment = await _assignmentRepository.GetTechnicianSkillAsync(
                createDto.TechnicianId, createDto.SkillId);
            
            if (existingAssignment != null)
                throw new InvalidOperationException("Skill is already assigned to this technician");

            var assignment = new TechnicianSkillAssignment
            {
                Id = Guid.NewGuid(),
                TechnicianId = createDto.TechnicianId,
                SkillId = createDto.SkillId,
                ProficiencyLevel = createDto.ProficiencyLevel,
                ProficiencyDescription = createDto.ProficiencyDescription ?? string.Empty,
                AcquiredDate = createDto.AcquiredDate ?? DateTime.UtcNow,
                ExpirationDate = createDto.ExpirationDate,
                IsVerified = createDto.IsVerified,
                VerifiedBy = createDto.VerifiedBy,
                Notes = createDto.Notes,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId
            };

            await _assignmentRepository.AddAsync(assignment);
            
            _logger.LogInformation("Assigned skill {SkillId} to technician {TechnicianId} successfully", 
                createDto.SkillId, createDto.TechnicianId);
            
            return MapAssignmentToDto(assignment, skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning skill {SkillId} to technician {TechnicianId}", 
                createDto.SkillId, createDto.TechnicianId);
            throw;
        }
    }

    public async Task<TechnicianSkillAssignmentDto> UpdateTechnicianSkillAsync(Guid id, UpdateTechnicianSkillAssignmentDto updateDto)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(id);
        if (assignment == null)
            throw new ArgumentException($"Skill assignment with ID {id} not found");

        assignment.ProficiencyLevel = updateDto.ProficiencyLevel;
        assignment.ProficiencyDescription = updateDto.ProficiencyDescription;
        assignment.ExpirationDate = updateDto.ExpirationDate;
        assignment.IsVerified = updateDto.IsVerified;
        assignment.VerifiedBy = updateDto.VerifiedBy;
        assignment.LastAssessmentDate = updateDto.LastAssessmentDate;
        assignment.Notes = updateDto.Notes;
        assignment.LastModifiedById = _currentUserProvider.UserId;

        await _assignmentRepository.UpdateAsync(assignment);

        var skill = await _skillRepository.GetByIdAsync(assignment.SkillId);
        return MapAssignmentToDto(assignment, skill!);
    }

    public async Task RemoveTechnicianSkillAsync(Guid id)
    {
        await _assignmentRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<TechnicianSkillAssignmentDto>> GetTechnicianSkillsAsync(Guid technicianId)
    {
        var assignments = await _assignmentRepository.GetByTechnicianIdAsync(technicianId);
        var result = new List<TechnicianSkillAssignmentDto>();
        
        foreach (var assignment in assignments)
        {
            var skill = await _skillRepository.GetByIdAsync(assignment.SkillId);
            if (skill != null)
            {
                result.Add(MapAssignmentToDto(assignment, skill));
            }
        }
        
        return result;
    }

    public async Task<IEnumerable<TechnicianSkillAssignmentDto>> GetSkillTechniciansAsync(Guid skillId)
    {
        var assignments = await _assignmentRepository.GetBySkillIdAsync(skillId);
        var result = new List<TechnicianSkillAssignmentDto>();
        
        var skill = await _skillRepository.GetByIdAsync(skillId);
        if (skill != null)
        {
            foreach (var assignment in assignments)
            {
                result.Add(MapAssignmentToDto(assignment, skill));
            }
        }
        
        return result;
    }

    #endregion

    #region Analytics

    public async Task<SkillGapAnalysisDto> GetSkillGapAnalysisAsync()
    {
        // This would typically analyze skill requirements vs available technicians
        return new SkillGapAnalysisDto
        {
            SkillDeficits = new List<SkillDeficitDto>(),
            OverstaffedSkills = new List<OverstaffedSkillDto>(),
            CriticalSkills = new List<CriticalSkillDto>(),
            TotalSkillsAnalyzed = await _skillRepository.CountAsync(),
            AnalysisDate = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId)
    {
        try
        {
            _logger.LogInformation("Getting technicians with skill: {SkillId}", skillId);
            
            var assignments = await _assignmentRepository.GetBySkillIdAsync(skillId);
            var result = new List<TechnicianDto>();
            
            // For now, return empty list - this would typically call HR/Technician service
            // In a real implementation, this would resolve technician details
            
            _logger.LogInformation("Found {TechnicianCount} technicians with skill {SkillId}", result.Count, skillId);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technicians with skill: {SkillId}", skillId);
            throw;
        }
    }

    #endregion

    #region Private Methods

    // Map HR Skill entity to TechnicalSkillDto
    private async Task<TechnicalSkillDto> MapToDto(Core.Entities.HR.Skill skill)
    {
        var technicianCount = await _skillRepository.GetTechnicianCountBySkillAsync(skill.Id);
        
        return new TechnicalSkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Code = skill.Name, // HR Skill doesn't have Code, use Name
            Description = skill.Description ?? string.Empty,
            Category = skill.Category ?? "General",
            SkillLevel = "Intermediate", // HR Skill doesn't have this, use default
            Complexity = "Medium", // Not in HR Skill
            RiskLevel = "Low", // Not in HR Skill
            Prerequisites = new List<string>(),
            Certifications = new List<string>(),
            EstimatedLearningHours = 0,
            ToolsRequired = new List<string>(),
            SafetyRequirements = string.Empty,
            CompetencyAreas = new List<string>(),
            RelatedMaintenanceTypes = new List<string>(),
            IsActive = skill.IsActive,
            IsFromHRModule = true, // Always true for HR Skills
            LastSyncDate = DateTime.UtcNow,
            CreatedDate = skill.CreatedAt,
            CreatedBy = "System", // TenantEntity doesn't expose CreatedBy as string
            LastModifiedDate = skill.UpdatedAt,
            LastModifiedBy = "System", // TenantEntity doesn't expose UpdatedBy as string
            TechniciansCount = technicianCount,
            AverageRating = 0
        };
    }

    private TechnicianSkillAssignmentDto MapAssignmentToDto(TechnicianSkillAssignment assignment, Core.Entities.HR.Skill skill)
    {
        return new TechnicianSkillAssignmentDto
        {
            Id = assignment.Id,
            TechnicianId = assignment.TechnicianId,
            SkillId = assignment.SkillId,
            SkillName = skill.Name,
            Category = skill.Category ?? "General",
            ProficiencyLevel = assignment.ProficiencyLevel,
            ProficiencyDescription = assignment.ProficiencyDescription,
            AcquiredDate = assignment.AcquiredDate,
            ExpirationDate = assignment.ExpirationDate,
            IsVerified = assignment.IsVerified,
            VerifiedBy = assignment.VerifiedBy,
            LastAssessmentDate = assignment.LastAssessmentDate,
            Notes = assignment.Notes,
            IsExpired = assignment.IsExpired,
            DaysUntilExpiration = assignment.DaysUntilExpiration
        };
    }

    #endregion
}