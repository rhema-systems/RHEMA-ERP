using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services;

public interface IEmailTemplateService
{
    Task<List<EmailTemplate>> GetAllTemplatesAsync();
    Task<EmailTemplate?> GetTemplateByIdAsync(Guid id);
    Task<EmailTemplate?> GetTemplateByNameAsync(string name);
    Task<List<EmailTemplate>> GetTemplatesByModuleAsync(string module);
    Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template);
    Task<EmailTemplate> UpdateTemplateAsync(EmailTemplate template);
    Task DeleteTemplateAsync(Guid id);
    Task<string> ProcessTemplateAsync(Guid templateId, Dictionary<string, object> data);
    Task<EmailTemplate> DuplicateTemplateAsync(Guid templateId, string newName);
    Task<List<string>> GetTemplateVariablesAsync(Guid templateId);
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<EmailTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<List<EmailTemplate>> GetAllTemplatesAsync()
    {
        try
        {
            var templates = await _unitOfWork.Repository<EmailTemplate>().GetAllAsync();
            return templates.Where(t => t.IsActive).OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email templates");
            throw;
        }
    }

    public async Task<EmailTemplate?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            return await _unitOfWork.Repository<EmailTemplate>().GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email template {TemplateId}", id);
            throw;
        }
    }

    public async Task<EmailTemplate?> GetTemplateByNameAsync(string name)
    {
        try
        {
            var templates = await _unitOfWork.Repository<EmailTemplate>()
                .FindAsync(t => t.Name == name && t.IsActive);
            return templates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email template by name {TemplateName}", name);
            throw;
        }
    }

    public async Task<List<EmailTemplate>> GetTemplatesByModuleAsync(string module)
    {
        try
        {
            var templates = await _unitOfWork.Repository<EmailTemplate>()
                .FindAsync(t => t.Module == module && t.IsActive);
            return templates.OrderBy(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email templates for module {Module}", module);
            throw;
        }
    }

    public async Task<EmailTemplate> CreateTemplateAsync(EmailTemplate template)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant context is required");
            }

            // Check for duplicate names within the tenant
            var existing = await _unitOfWork.Repository<EmailTemplate>()
                .FindAsync(t => t.Name == template.Name && t.TenantId == tenantId.Value);
            
            if (existing.Any())
            {
                throw new InvalidOperationException($"A template with the name '{template.Name}' already exists");
            }

            template.Id = Guid.NewGuid();
            template.TenantId = tenantId.Value;
            template.CreatedAt = DateTime.UtcNow;
            template.CreatedBy = _currentUserService.UserName;

            // Extract and store template variables from HTML body
            template.TemplateVariables = ExtractTemplateVariables(template.HtmlBody);

            var createdTemplate = await _unitOfWork.Repository<EmailTemplate>().AddAsync(template);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created email template {TemplateName} for tenant {TenantId}", 
                template.Name, tenantId);

            return createdTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email template {TemplateName}", template.Name);
            throw;
        }
    }

    public async Task<EmailTemplate> UpdateTemplateAsync(EmailTemplate template)
    {
        try
        {
            var existingTemplate = await GetTemplateByIdAsync(template.Id);
            if (existingTemplate == null)
            {
                throw new InvalidOperationException($"Template with ID {template.Id} not found");
            }

            // Update properties
            existingTemplate.Name = template.Name;
            existingTemplate.Module = template.Module;
            existingTemplate.TableName = template.TableName;
            existingTemplate.Subject = template.Subject;
            existingTemplate.HtmlBody = template.HtmlBody;
            existingTemplate.PlainTextBody = template.PlainTextBody;
            existingTemplate.SelectedFields = template.SelectedFields;
            existingTemplate.Description = template.Description;
            existingTemplate.Category = template.Category;
            existingTemplate.UpdatedAt = DateTime.UtcNow;
            existingTemplate.UpdatedBy = _currentUserService.UserName;

            // Update template variables
            existingTemplate.TemplateVariables = ExtractTemplateVariables(template.HtmlBody);

            await _unitOfWork.Repository<EmailTemplate>().UpdateAsync(existingTemplate);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated email template {TemplateName} ({TemplateId})", 
                template.Name, template.Id);

            return existingTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email template {TemplateId}", template.Id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            var template = await GetTemplateByIdAsync(id);
            if (template == null)
            {
                throw new InvalidOperationException($"Template with ID {id} not found");
            }

            // Soft delete
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _currentUserService.UserName;

            await _unitOfWork.Repository<EmailTemplate>().UpdateAsync(template);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted email template {TemplateName} ({TemplateId})", 
                template.Name, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email template {TemplateId}", id);
            throw;
        }
    }

    public async Task<string> ProcessTemplateAsync(Guid templateId, Dictionary<string, object> data)
    {
        try
        {
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                throw new InvalidOperationException($"Template with ID {templateId} not found");
            }

            var processedContent = template.HtmlBody;

            // Replace placeholders with actual data
            foreach (var kvp in data)
            {
                var placeholder = $"{{{{{kvp.Key}}}}}";
                var value = kvp.Value?.ToString() ?? "";
                processedContent = processedContent.Replace(placeholder, value);
            }

            return processedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<EmailTemplate> DuplicateTemplateAsync(Guid templateId, string newName)
    {
        try
        {
            var originalTemplate = await GetTemplateByIdAsync(templateId);
            if (originalTemplate == null)
            {
                throw new InvalidOperationException($"Template with ID {templateId} not found");
            }

            var duplicateTemplate = new EmailTemplate
            {
                Name = newName,
                Module = originalTemplate.Module,
                TableName = originalTemplate.TableName,
                Subject = originalTemplate.Subject,
                HtmlBody = originalTemplate.HtmlBody,
                PlainTextBody = originalTemplate.PlainTextBody,
                SelectedFields = originalTemplate.SelectedFields,
                Description = $"Copy of {originalTemplate.Description}",
                Category = originalTemplate.Category
            };

            return await CreateTemplateAsync(duplicateTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating template {TemplateId} as {NewName}", templateId, newName);
            throw;
        }
    }

    public async Task<List<string>> GetTemplateVariablesAsync(Guid templateId)
    {
        try
        {
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                throw new InvalidOperationException($"Template with ID {templateId} not found");
            }

            if (string.IsNullOrEmpty(template.TemplateVariables))
            {
                return new List<string>();
            }

            var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(template.TemplateVariables);
            return variables?.Keys.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template variables for {TemplateId}", templateId);
            throw;
        }
    }

    private string ExtractTemplateVariables(string htmlContent)
    {
        try
        {
            // Extract placeholders in the format {{Variable.Name}}
            var regex = new Regex(@"\{\{([^}]+)\}\}", RegexOptions.IgnoreCase);
            var matches = regex.Matches(htmlContent);

            var variables = new Dictionary<string, string>();
            foreach (Match match in matches)
            {
                var placeholder = match.Value;
                var variableName = match.Groups[1].Value;
                
                if (!variables.ContainsKey(placeholder))
                {
                    // Generate a friendly description from the variable name
                    var description = GenerateVariableDescription(variableName);
                    variables[placeholder] = description;
                }
            }

            return JsonSerializer.Serialize(variables);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting template variables from HTML content");
            return "{}";
        }
    }

    private string GenerateVariableDescription(string variableName)
    {
        // Convert "Table.Field" or "Field" to a friendly description
        var parts = variableName.Split('.');
        if (parts.Length == 2)
        {
            var table = SplitCamelCase(parts[0]);
            var field = SplitCamelCase(parts[1]);
            return $"{table} {field}";
        }
        
        return SplitCamelCase(variableName);
    }

    private string SplitCamelCase(string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
    }
}