using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing inspection templates
/// </summary>
public class InspectionTemplateService : IInspectionTemplateService
{
    private readonly IInspectionTemplateRepository _templateRepository;
    private readonly ILogger<InspectionTemplateService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public InspectionTemplateService(
        IInspectionTemplateRepository templateRepository,
        ILogger<InspectionTemplateService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _templateRepository = templateRepository;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region CRUD Operations

    public async Task<InspectionTemplateDto> CreateTemplateAsync(CreateInspectionTemplateDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating inspection template: {TemplateName}", createDto.Name);

            var template = new InspectionTemplate
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Code = createDto.Code,
                Description = createDto.Description,
                Category = createDto.Category,
                Version = createDto.Version ?? "1.0",
                InspectionType = createDto.InspectionType,
                EstimatedDurationMinutes = createDto.EstimatedDurationMinutes,
                RequiredSkills = JsonSerializer.Serialize(createDto.RequiredSkills ?? new List<string>()),
                RequiredTools = JsonSerializer.Serialize(createDto.RequiredTools ?? new List<string>()),
                SafetyRequirements = createDto.SafetyRequirements,
                IsRegulatory = createDto.IsRegulatory,
                RegulatoryStandard = createDto.RegulatoryStandard,
                ComplianceLevel = createDto.ComplianceLevel,
                IsActive = createDto.IsActive,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            await _templateRepository.AddAsync(template);
            
            _logger.LogInformation("Created inspection template {TemplateId} successfully", template.Id);
            
            return await MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inspection template: {TemplateName}", createDto.Name);
            throw;
        }
    }

    public async Task<InspectionTemplateDto> UpdateTemplateAsync(Guid id, UpdateInspectionTemplateDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating inspection template: {TemplateId}", id);

            var template = await _templateRepository.GetByIdAsync(id);
            if (template == null)
                throw new ArgumentException($"Template with ID {id} not found");

            // Update properties
            template.Name = updateDto.Name;
            template.Description = updateDto.Description;
            template.Category = updateDto.Category;
            template.Version = updateDto.Version;
            template.InspectionType = updateDto.InspectionType;
            template.EstimatedDurationMinutes = updateDto.EstimatedDurationMinutes;
            template.RequiredSkills = JsonSerializer.Serialize(updateDto.RequiredSkills ?? new List<string>());
            template.RequiredTools = JsonSerializer.Serialize(updateDto.RequiredTools ?? new List<string>());
            template.SafetyRequirements = updateDto.SafetyRequirements;
            template.IsRegulatory = updateDto.IsRegulatory;
            template.RegulatoryStandard = updateDto.RegulatoryStandard;
            template.ComplianceLevel = updateDto.ComplianceLevel;
            template.IsActive = updateDto.IsActive;
            template.LastModifiedById = _currentUserProvider.UserId;
            template.LastModified = DateTime.UtcNow;

            await _templateRepository.UpdateAsync(template);
            
            _logger.LogInformation("Updated inspection template {TemplateId} successfully", id);
            
            return await MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting inspection template: {TemplateId}", id);

            var template = await _templateRepository.GetByIdAsync(id);
            if (template == null)
                throw new ArgumentException($"Template with ID {id} not found");

            await _templateRepository.DeleteAsync(id);
            
            _logger.LogInformation("Deleted inspection template {TemplateId} successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection template: {TemplateId}", id);
            throw;
        }
    }

    public async Task<InspectionTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        return template != null ? await MapToDto(template) : null;
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetAllTemplatesAsync()
    {
        var templates = await _templateRepository.GetAllAsync();
        var result = new List<InspectionTemplateDto>();
        
        foreach (var template in templates)
        {
            result.Add(await MapToDto(template));
        }
        
        return result;
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetTemplatesByCategoryAsync(string category)
    {
        var templates = await _templateRepository.GetByCategoryAsync(category);
        var result = new List<InspectionTemplateDto>();
        
        foreach (var template in templates)
        {
            result.Add(await MapToDto(template));
        }
        
        return result;
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetActiveTemplatesAsync()
    {
        var templates = await _templateRepository.GetActiveTemplatesAsync();
        var result = new List<InspectionTemplateDto>();
        
        foreach (var template in templates)
        {
            result.Add(await MapToDto(template));
        }
        
        return result;
    }

    #endregion

    #region Business Logic Methods

    public async Task<bool> IsTemplateCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _templateRepository.IsCodeUniqueAsync(code, excludeId);
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetRegulatoryTemplatesAsync()
    {
        var templates = await _templateRepository.GetRegulatoryTemplatesAsync();
        var result = new List<InspectionTemplateDto>();
        
        foreach (var template in templates)
        {
            result.Add(await MapToDto(template));
        }
        
        return result;
    }

    public async Task<IEnumerable<InspectionTemplateDto>> GetTemplatesByInspectionTypeAsync(string inspectionType)
    {
        var templates = await _templateRepository.GetByInspectionTypeAsync(inspectionType);
        var result = new List<InspectionTemplateDto>();
        
        foreach (var template in templates)
        {
            result.Add(await MapToDto(template));
        }
        
        return result;
    }

    public async Task<InspectionTemplateDto> CloneTemplateAsync(Guid templateId, string newName, string newCode)
    {
        try
        {
            _logger.LogInformation("Cloning inspection template: {TemplateId}", templateId);

            var originalTemplate = await _templateRepository.GetByIdAsync(templateId);
            if (originalTemplate == null)
                throw new ArgumentException($"Template with ID {templateId} not found");

            var clonedTemplate = new InspectionTemplate
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Code = newCode,
                Description = $"Cloned from: {originalTemplate.Name}",
                Category = originalTemplate.Category,
                Version = "1.0",
                InspectionType = originalTemplate.InspectionType,
                EstimatedDurationMinutes = originalTemplate.EstimatedDurationMinutes,
                RequiredSkills = originalTemplate.RequiredSkills,
                RequiredTools = originalTemplate.RequiredTools,
                SafetyRequirements = originalTemplate.SafetyRequirements,
                IsRegulatory = originalTemplate.IsRegulatory,
                RegulatoryStandard = originalTemplate.RegulatoryStandard,
                ComplianceLevel = originalTemplate.ComplianceLevel,
                IsActive = true,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            await _templateRepository.AddAsync(clonedTemplate);
            
            _logger.LogInformation("Cloned inspection template {TemplateId} successfully", clonedTemplate.Id);
            
            return await MapToDto(clonedTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning inspection template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<InspectionTemplateDto> UpdateTemplateVersionAsync(Guid templateId, string newVersion)
    {
        try
        {
            _logger.LogInformation("Updating template version: {TemplateId} to {Version}", templateId, newVersion);

            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException($"Template with ID {templateId} not found");

            template.Version = newVersion;
            template.LastModifiedById = _currentUserProvider.UserId;
            template.LastModified = DateTime.UtcNow;

            await _templateRepository.UpdateAsync(template);
            
            _logger.LogInformation("Updated template version {TemplateId} successfully", templateId);
            
            return await MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template version: {TemplateId}", templateId);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<InspectionTemplateDto> MapToDto(InspectionTemplate template)
    {
        var requiredSkills = new List<string>();
        var requiredTools = new List<string>();

        try
        {
            if (!string.IsNullOrEmpty(template.RequiredSkills))
                requiredSkills = JsonSerializer.Deserialize<List<string>>(template.RequiredSkills) ?? new List<string>();
            
            if (!string.IsNullOrEmpty(template.RequiredTools))
                requiredTools = JsonSerializer.Deserialize<List<string>>(template.RequiredTools) ?? new List<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error deserializing template data for template {TemplateId}", template.Id);
        }

        return new InspectionTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Code = template.Code,
            Description = template.Description,
            Category = template.Category,
            Version = template.Version,
            InspectionType = template.InspectionType,
            EstimatedDurationMinutes = template.EstimatedDurationMinutes,
            RequiredSkills = requiredSkills,
            RequiredTools = requiredTools,
            SafetyRequirements = template.SafetyRequirements,
            IsRegulatory = template.IsRegulatory,
            RegulatoryStandard = template.RegulatoryStandard,
            ComplianceLevel = template.ComplianceLevel,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            LastModified = template.LastModified
        };
    }

    #endregion
}