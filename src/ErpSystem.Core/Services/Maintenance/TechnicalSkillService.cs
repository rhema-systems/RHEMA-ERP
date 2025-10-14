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

            // Only allow creation of local skills (non-HR)
            if (await _skillRepository.IsCodeUniqueAsync(createDto.Code))
                throw new ArgumentException($"Skill code '{createDto.Code}' already exists");

            var skill = new TechnicalSkill
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Code = createDto.Code,
                Description = createDto.Description,
                Category = createDto.Category,
                SkillLevel = createDto.SkillLevel,
                Complexity = createDto.Complexity,
                RiskLevel = createDto.RiskLevel,
                Prerequisites = JsonSerializer.Serialize(createDto.Prerequisites),
                Certifications = JsonSerializer.Serialize(createDto.Certifications),
                EstimatedLearningHours = createDto.EstimatedLearningHours,
                ToolsRequired = JsonSerializer.Serialize(createDto.ToolsRequired),
                SafetyRequirements = createDto.SafetyRequirements,
                CompetencyAreas = JsonSerializer.Serialize(createDto.CompetencyAreas),
                RelatedMaintenanceTypes = JsonSerializer.Serialize(createDto.RelatedMaintenanceTypes),
                IsActive = createDto.IsActive,
                IsFromHRModule = false, // Local skill
                CreatedById = _currentUserProvider.UserId,
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

            // Only allow updates to local skills (non-HR)
            if (skill.IsFromHRModule)
                throw new InvalidOperationException("Cannot modify skills synchronized from HR module. Use HR Skills Management to make changes.");

            skill.Name = updateDto.Name;
            skill.Description = updateDto.Description;
            skill.Category = updateDto.Category;
            skill.SkillLevel = updateDto.SkillLevel;
            skill.Complexity = updateDto.Complexity;
            skill.RiskLevel = updateDto.RiskLevel;
            skill.Prerequisites = JsonSerializer.Serialize(updateDto.Prerequisites);
            skill.Certifications = JsonSerializer.Serialize(updateDto.Certifications);
            skill.EstimatedLearningHours = updateDto.EstimatedLearningHours;
            skill.ToolsRequired = JsonSerializer.Serialize(updateDto.ToolsRequired);
            skill.SafetyRequirements = updateDto.SafetyRequirements;
            skill.CompetencyAreas = JsonSerializer.Serialize(updateDto.CompetencyAreas);
            skill.RelatedMaintenanceTypes = JsonSerializer.Serialize(updateDto.RelatedMaintenanceTypes);
            skill.IsActive = updateDto.IsActive;
            skill.LastModifiedById = _currentUserProvider.UserId;

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

            // Only allow deletion of local skills (non-HR)
            if (skill.IsFromHRModule)
                throw new InvalidOperationException("Cannot delete skills synchronized from HR module. Deactivate in HR Skills Management instead.");

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
                                          s.Code.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          s.Description.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter.Category))
            filtered = filtered.Where(s => s.Category == filter.Category);

        if (!string.IsNullOrEmpty(filter.SkillLevel))
            filtered = filtered.Where(s => s.SkillLevel == filter.SkillLevel);

        if (!string.IsNullOrEmpty(filter.Complexity))
            filtered = filtered.Where(s => s.Complexity == filter.Complexity);

        if (!string.IsNullOrEmpty(filter.RiskLevel))
            filtered = filtered.Where(s => s.RiskLevel == filter.RiskLevel);

        if (filter.IsActive.HasValue)
            filtered = filtered.Where(s => s.IsActive == filter.IsActive.Value);

        if (filter.IsFromHRModule.HasValue)
            filtered = filtered.Where(s => s.IsFromHRModule == filter.IsFromHRModule.Value);

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
        if (!int.TryParse(skillLevel, out int level))
            throw new ArgumentException($"Invalid skill level: {skillLevel}", nameof(skillLevel));
            
        var skills = await _skillRepository.GetBySkillLevelAsync(level);
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByComplexityAsync(string complexity)
    {
        var skills = await _skillRepository.GetByComplexityAsync(complexity);
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<IEnumerable<TechnicalSkillDto>> GetSkillsByRiskLevelAsync(string riskLevel)
    {
        var skills = await _skillRepository.GetByRiskLevelAsync(riskLevel);
        var result = new List<TechnicalSkillDto>();
        
        foreach (var skill in skills)
        {
            result.Add(await MapToDto(skill));
        }
        
        return result;
    }

    public async Task<TechnicalSkillDto> ToggleSkillStatusAsync(Guid id)
    {
        var skill = await _skillRepository.GetByIdAsync(id);
        if (skill == null)
            throw new ArgumentException($"Skill with ID {id} not found");

        // Only allow status changes for local skills (non-HR)
        if (skill.IsFromHRModule)
            throw new InvalidOperationException("Cannot modify status of skills synchronized from HR module. Use HR Skills Management to make changes.");

        skill.IsActive = !skill.IsActive;
        skill.LastModifiedById = _currentUserProvider.UserId;
        
        await _skillRepository.UpdateAsync(skill);
        
        return await MapToDto(skill);
    }

    public async Task<bool> IsSkillCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _skillRepository.IsCodeUniqueAsync(code, excludeId);
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
        try
        {
            _logger.LogInformation("Starting synchronization of technical skills from HR module");

            // This would typically call the HR API to fetch technical skills
            // For now, we'll simulate the sync with mock data
            var hrSkills = await _skillRepository.SyncFromHRAsync();
            var result = new List<TechnicalSkillDto>();
            
            foreach (var skill in hrSkills)
            {
                skill.IsFromHRModule = true;
                skill.LastSyncDate = DateTime.UtcNow;
                await _skillRepository.UpdateAsync(skill);
                result.Add(await MapToDto(skill));
            }

            _logger.LogInformation("Synchronized {Count} technical skills from HR module", result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing skills from HR module");
            throw;
        }
    }

    public async Task<TechnicalSkillDto?> GetSkillFromHRAsync(Guid hrSkillId)
    {
        // This would typically call the HR API
        var skills = await _skillRepository.GetFromHRModuleAsync();
        var hrSkill = skills.FirstOrDefault(s => s.Id == hrSkillId);
        return hrSkill != null ? await MapToDto(hrSkill) : null;
    }

    public async Task UpdateSkillFromHRAsync(TechnicalSkillDto hrSkill)
    {
        var existingSkill = await _skillRepository.GetByCodeAsync(hrSkill.Code);
        if (existingSkill != null && existingSkill.IsFromHRModule)
        {
            // Update existing HR skill
            existingSkill.Name = hrSkill.Name;
            existingSkill.Description = hrSkill.Description;
            existingSkill.Category = hrSkill.Category;
            existingSkill.SkillLevel = hrSkill.SkillLevel;
            existingSkill.LastSyncDate = DateTime.UtcNow;
            await _skillRepository.UpdateAsync(existingSkill);
        }
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

    private async Task<TechnicalSkillDto> MapToDto(TechnicalSkill skill)
    {
        return new TechnicalSkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Code = skill.Code,
            Description = skill.Description,
            Category = skill.Category,
            SkillLevel = skill.SkillLevel,
            Complexity = skill.Complexity,
            RiskLevel = skill.RiskLevel,
            Prerequisites = JsonSerializer.Deserialize<List<string>>(skill.Prerequisites) ?? new List<string>(),
            Certifications = JsonSerializer.Deserialize<List<string>>(skill.Certifications) ?? new List<string>(),
            EstimatedLearningHours = skill.EstimatedLearningHours,
            ToolsRequired = JsonSerializer.Deserialize<List<string>>(skill.ToolsRequired) ?? new List<string>(),
            SafetyRequirements = skill.SafetyRequirements,
            CompetencyAreas = JsonSerializer.Deserialize<List<string>>(skill.CompetencyAreas) ?? new List<string>(),
            RelatedMaintenanceTypes = JsonSerializer.Deserialize<List<string>>(skill.RelatedMaintenanceTypes) ?? new List<string>(),
            IsActive = skill.IsActive,
            IsFromHRModule = skill.IsFromHRModule,
            LastSyncDate = skill.LastSyncDate,
            CreatedDate = skill.CreatedDate,
            CreatedBy = "Created By Name", // Would be resolved
            LastModifiedDate = skill.LastModifiedDate,
            LastModifiedBy = "Modified By Name", // Would be resolved
            TechniciansCount = skill.TechniciansCount,
            AverageRating = skill.AverageRating
        };
    }

    private TechnicianSkillAssignmentDto MapAssignmentToDto(TechnicianSkillAssignment assignment, TechnicalSkill skill)
    {
        return new TechnicianSkillAssignmentDto
        {
            Id = assignment.Id,
            TechnicianId = assignment.TechnicianId,
            SkillId = assignment.SkillId,
            SkillName = skill.Name,
            Category = skill.Category,
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