using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing maintenance notification templates with variable substitution,
/// lead time scheduling, and role-based delivery configuration.
/// </summary>
public interface IMaintenanceNotificationTemplateService
{
    Task<MaintenanceNotificationTemplateDto?> GetTemplateByIdAsync(Guid id);
    Task<MaintenanceNotificationTemplateDto?> GetTemplateByNameAsync(string name);
    Task<List<MaintenanceNotificationTemplateDto>> GetTemplatesByNotificationTypeAsync(string notificationType);
    Task<MaintenanceNotificationTemplateDto> CreateTemplateAsync(MaintenanceNotificationTemplateDto dto);
    Task<MaintenanceNotificationTemplateDto> UpdateTemplateAsync(Guid id, MaintenanceNotificationTemplateDto dto);
    Task DeleteTemplateAsync(Guid id);
    Task<List<MaintenanceNotificationTemplateDto>> GetAllTemplatesAsync();
    
    // Template processing
    Task<string> ProcessTemplateTitleAsync(Guid templateId, Dictionary<string, object> variables);
    Task<string> ProcessTemplateMessageAsync(Guid templateId, Dictionary<string, object> variables);
    Task<List<string>> ExtractTemplateVariablesAsync(Guid templateId);
}

public class MaintenanceNotificationTemplateService : IMaintenanceNotificationTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MaintenanceNotificationTemplateService> _logger;

    public MaintenanceNotificationTemplateService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<MaintenanceNotificationTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<MaintenanceNotificationTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            var template = await _unitOfWork.Repository<MaintenanceNotificationTemplate>().GetByIdAsync(id);
            if (template == null) return null;

            return MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification template {TemplateId}", id);
            throw;
        }
    }

    public async Task<MaintenanceNotificationTemplateDto?> GetTemplateByNameAsync(string name)
    {
        try
        {
            var templates = await _unitOfWork.Repository<MaintenanceNotificationTemplate>()
                .FindAsync(t => t.Name == name && t.IsActive);
            
            var template = templates.FirstOrDefault();
            return template == null ? null : MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification template by name {TemplateName}", name);
            throw;
        }
    }

    public async Task<List<MaintenanceNotificationTemplateDto>> GetTemplatesByNotificationTypeAsync(string notificationType)
    {
        try
        {
            var templates = await _unitOfWork.Repository<MaintenanceNotificationTemplate>()
                .FindAsync(t => t.NotificationType == notificationType && t.IsActive);

            return templates.Select(MapToDto).OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates for notification type {NotificationType}", notificationType);
            throw;
        }
    }

    public async Task<MaintenanceNotificationTemplateDto> CreateTemplateAsync(MaintenanceNotificationTemplateDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;

            var entity = new MaintenanceNotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                NotificationType = dto.NotificationType,
                Description = dto.Description,
                TitleTemplate = dto.TitleTemplate,
                MessageTemplate = dto.MessageTemplate,
                DefaultPriority = dto.DefaultPriority,
                LeadTimeMinutes = dto.LeadTimeMinutes,
                IsActive = true,
                DefaultRoles = dto.DefaultRoles,
                DeliveryMethods = dto.DeliveryMethods,
                TenantId = tenantId
            };

            var createdEntity = await _unitOfWork.Repository<MaintenanceNotificationTemplate>().AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created notification template {TemplateName} ({TemplateId})", entity.Name, entity.Id);

            return MapToDto(createdEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template {TemplateName}", dto.Name);
            throw;
        }
    }

    public async Task<MaintenanceNotificationTemplateDto> UpdateTemplateAsync(Guid id, MaintenanceNotificationTemplateDto dto)
    {
        try
        {
            var entity = await _unitOfWork.Repository<MaintenanceNotificationTemplate>().GetByIdAsync(id);
            if (entity == null)
                throw new ArgumentException($"Template with ID {id} not found");

            entity.Name = dto.Name;
            entity.NotificationType = dto.NotificationType;
            entity.Description = dto.Description;
            entity.TitleTemplate = dto.TitleTemplate;
            entity.MessageTemplate = dto.MessageTemplate;
            entity.DefaultPriority = dto.DefaultPriority;
            entity.LeadTimeMinutes = dto.LeadTimeMinutes;
            entity.DefaultRoles = dto.DefaultRoles;
            entity.DeliveryMethods = dto.DeliveryMethods;
            entity.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<MaintenanceNotificationTemplate>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated notification template {TemplateName} ({TemplateId})", entity.Name, id);

            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification template {TemplateId}", id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            var entity = await _unitOfWork.Repository<MaintenanceNotificationTemplate>().GetByIdAsync(id);
            if (entity == null)
                throw new ArgumentException($"Template with ID {id} not found");

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<MaintenanceNotificationTemplate>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted notification template {TemplateName} ({TemplateId})", entity.Name, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification template {TemplateId}", id);
            throw;
        }
    }

    public async Task<List<MaintenanceNotificationTemplateDto>> GetAllTemplatesAsync()
    {
        try
        {
            var templates = await _unitOfWork.Repository<MaintenanceNotificationTemplate>()
                .FindAsync(t => t.IsActive);

            return templates.Select(MapToDto).OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all notification templates");
            throw;
        }
    }

    public async Task<string> ProcessTemplateTitleAsync(Guid templateId, Dictionary<string, object> variables)
    {
        try
        {
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException($"Template with ID {templateId} not found");

            return SubstituteVariables(template.TitleTemplate, variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template title for template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<string> ProcessTemplateMessageAsync(Guid templateId, Dictionary<string, object> variables)
    {
        try
        {
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException($"Template with ID {templateId} not found");

            return SubstituteVariables(template.MessageTemplate, variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template message for template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<List<string>> ExtractTemplateVariablesAsync(Guid templateId)
    {
        try
        {
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException($"Template with ID {templateId} not found");

            var titleVars = ExtractVariablesFromTemplate(template.TitleTemplate);
            var messageVars = ExtractVariablesFromTemplate(template.MessageTemplate);

            return titleVars.Union(messageVars).OrderBy(v => v).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting variables from template {TemplateId}", templateId);
            throw;
        }
    }

    private string SubstituteVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;

        foreach (var kvp in variables)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}";
            result = result.Replace(placeholder, kvp.Value?.ToString() ?? "N/A");
        }

        return result;
    }

    private List<string> ExtractVariablesFromTemplate(string template)
    {
        var regex = new Regex(@"\{\{(\w+(?:\.\w+)*)\}\}");
        var matches = regex.Matches(template);

        return matches.Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    private MaintenanceNotificationTemplateDto MapToDto(MaintenanceNotificationTemplate entity)
    {
        return new MaintenanceNotificationTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            NotificationType = entity.NotificationType,
            Description = entity.Description,
            TitleTemplate = entity.TitleTemplate,
            MessageTemplate = entity.MessageTemplate,
            DefaultPriority = entity.DefaultPriority,
            LeadTimeMinutes = entity.LeadTimeMinutes,
            IsActive = entity.IsActive,
            DefaultRoles = entity.DefaultRoles,
            DeliveryMethods = entity.DeliveryMethods,
            CreatedDate = entity.CreatedAt,
            LastModifiedDate = entity.UpdatedAt,
            CreatedByName = entity.CreatedById.ToString(),
            LastModifiedByName = entity.LastModifiedById?.ToString()
        };
    }
}
