using System.Security.Claims;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmailTemplateController : ControllerBase
{
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IDatabaseMetadataService _metadataService;
    private readonly ILogger<EmailTemplateController> _logger;

    public EmailTemplateController(
        IEmailTemplateService emailTemplateService,
        IDatabaseMetadataService metadataService,
        ILogger<EmailTemplateController> logger)
    {
        _emailTemplateService = emailTemplateService;
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <summary>
    /// Get all email templates
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<IEnumerable<EmailTemplateDto>>> GetAllTemplates()
    {
        try
        {
            var templates = await _emailTemplateService.GetAllTemplatesAsync();
            var templateDtos = templates.Select(t => new EmailTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Module = t.Module,
                TableName = t.TableName,
                Subject = t.Subject,
                HtmlBody = t.HtmlBody,
                PlainTextBody = t.PlainTextBody,
                SelectedFields = t.SelectedFields,
                TemplateVariables = t.TemplateVariables,
                Description = t.Description,
                Category = t.Category,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                CreatedBy = t.CreatedBy,
                UpdatedAt = t.UpdatedAt,
                UpdatedBy = t.UpdatedBy
            }).ToList();

            return Ok(templateDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email templates");
            return StatusCode(500, "An error occurred while retrieving email templates");
        }
    }

    /// <summary>
    /// Get email template by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<EmailTemplateDto>> GetTemplateById(Guid id)
    {
        try
        {
            var template = await _emailTemplateService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }

            var templateDto = new EmailTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Module = template.Module,
                TableName = template.TableName,
                Subject = template.Subject,
                HtmlBody = template.HtmlBody,
                PlainTextBody = template.PlainTextBody,
                SelectedFields = template.SelectedFields,
                TemplateVariables = template.TemplateVariables,
                Description = template.Description,
                Category = template.Category,
                IsActive = template.IsActive,
                CreatedAt = template.CreatedAt,
                CreatedBy = template.CreatedBy,
                UpdatedAt = template.UpdatedAt,
                UpdatedBy = template.UpdatedBy
            };

            return Ok(templateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email template {TemplateId}", id);
            return StatusCode(500, "An error occurred while retrieving the email template");
        }
    }

    /// <summary>
    /// Get templates by module
    /// </summary>
    [HttpGet("by-module/{module}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<IEnumerable<EmailTemplateDto>>> GetTemplatesByModule(string module)
    {
        try
        {
            var templates = await _emailTemplateService.GetTemplatesByModuleAsync(module);
            var templateDtos = templates.Select(t => new EmailTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Module = t.Module,
                TableName = t.TableName,
                Subject = t.Subject,
                Description = t.Description,
                Category = t.Category
            }).ToList();

            return Ok(templateDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email templates for module {Module}", module);
            return StatusCode(500, "An error occurred while retrieving email templates");
        }
    }

    /// <summary>
    /// Create new email template
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailTemplateDto>> CreateTemplate([FromBody] CreateEmailTemplateDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = new EmailTemplate
            {
                Name = request.Name,
                Module = request.Module,
                TableName = request.TableName,
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                PlainTextBody = request.PlainTextBody,
                SelectedFields = request.SelectedFields,
                Description = request.Description,
                Category = request.Category
            };

            var createdTemplate = await _emailTemplateService.CreateTemplateAsync(template);

            var responseDto = new EmailTemplateDto
            {
                Id = createdTemplate.Id,
                Name = createdTemplate.Name,
                Module = createdTemplate.Module,
                TableName = createdTemplate.TableName,
                Subject = createdTemplate.Subject,
                HtmlBody = createdTemplate.HtmlBody,
                PlainTextBody = createdTemplate.PlainTextBody,
                SelectedFields = createdTemplate.SelectedFields,
                TemplateVariables = createdTemplate.TemplateVariables,
                Description = createdTemplate.Description,
                Category = createdTemplate.Category,
                IsActive = createdTemplate.IsActive,
                CreatedAt = createdTemplate.CreatedAt,
                CreatedBy = createdTemplate.CreatedBy
            };

            return CreatedAtAction(nameof(GetTemplateById), new { id = createdTemplate.Id }, responseDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email template {TemplateName}", request.Name);
            return StatusCode(500, "An error occurred while creating the email template");
        }
    }

    /// <summary>
    /// Update email template
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateEmailTemplateDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = new EmailTemplate
            {
                Id = id,
                Name = request.Name,
                Module = request.Module,
                TableName = request.TableName,
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                PlainTextBody = request.PlainTextBody,
                SelectedFields = request.SelectedFields,
                Description = request.Description,
                Category = request.Category
            };

            var updatedTemplate = await _emailTemplateService.UpdateTemplateAsync(template);

            var responseDto = new EmailTemplateDto
            {
                Id = updatedTemplate.Id,
                Name = updatedTemplate.Name,
                Module = updatedTemplate.Module,
                TableName = updatedTemplate.TableName,
                Subject = updatedTemplate.Subject,
                HtmlBody = updatedTemplate.HtmlBody,
                PlainTextBody = updatedTemplate.PlainTextBody,
                SelectedFields = updatedTemplate.SelectedFields,
                TemplateVariables = updatedTemplate.TemplateVariables,
                Description = updatedTemplate.Description,
                Category = updatedTemplate.Category,
                IsActive = updatedTemplate.IsActive,
                CreatedAt = updatedTemplate.CreatedAt,
                CreatedBy = updatedTemplate.CreatedBy,
                UpdatedAt = updatedTemplate.UpdatedAt,
                UpdatedBy = updatedTemplate.UpdatedBy
            };

            return Ok(responseDto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email template {TemplateId}", id);
            return StatusCode(500, "An error occurred while updating the email template");
        }
    }

    /// <summary>
    /// Delete email template
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            await _emailTemplateService.DeleteTemplateAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email template {TemplateId}", id);
            return StatusCode(500, "An error occurred while deleting the email template");
        }
    }

    /// <summary>
    /// Duplicate email template
    /// </summary>
    [HttpPost("{id}/duplicate")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailTemplateDto>> DuplicateTemplate(Guid id, [FromBody] DuplicateTemplateDto request)
    {
        try
        {
            var duplicatedTemplate = await _emailTemplateService.DuplicateTemplateAsync(id, request.NewName);

            var responseDto = new EmailTemplateDto
            {
                Id = duplicatedTemplate.Id,
                Name = duplicatedTemplate.Name,
                Module = duplicatedTemplate.Module,
                TableName = duplicatedTemplate.TableName,
                Subject = duplicatedTemplate.Subject,
                HtmlBody = duplicatedTemplate.HtmlBody,
                PlainTextBody = duplicatedTemplate.PlainTextBody,
                SelectedFields = duplicatedTemplate.SelectedFields,
                TemplateVariables = duplicatedTemplate.TemplateVariables,
                Description = duplicatedTemplate.Description,
                Category = duplicatedTemplate.Category,
                IsActive = duplicatedTemplate.IsActive,
                CreatedAt = duplicatedTemplate.CreatedAt,
                CreatedBy = duplicatedTemplate.CreatedBy
            };

            return CreatedAtAction(nameof(GetTemplateById), new { id = duplicatedTemplate.Id }, responseDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating email template {TemplateId}", id);
            return StatusCode(500, "An error occurred while duplicating the email template");
        }
    }

    /// <summary>
    /// Get available modules
    /// </summary>
    [HttpGet("modules")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableModules()
    {
        try
        {
            var modules = await _metadataService.GetAvailableModulesAsync();
            return Ok(modules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available modules");
            return StatusCode(500, "An error occurred while retrieving available modules");
        }
    }

    /// <summary>
    /// Get database tables
    /// </summary>
    [HttpGet("database/tables")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<DatabaseTableDto>>> GetDatabaseTables()
    {
        try
        {
            var tables = await _metadataService.GetTablesAsync();
            var tableDtos = tables.Select(t => new DatabaseTableDto
            {
                Name = t.Name,
                Schema = t.Schema,
                Description = t.Description
            }).ToList();

            return Ok(tableDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database tables");
            return StatusCode(500, "An error occurred while retrieving database tables");
        }
    }

    /// <summary>
    /// Get table columns
    /// </summary>
    [HttpGet("database/tables/{tableName}/columns")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<DatabaseColumnDto>>> GetTableColumns(string tableName)
    {
        try
        {
            var columns = await _metadataService.GetTableColumnsAsync(tableName);
            var columnDtos = columns.Select(c => new DatabaseColumnDto
            {
                Name = c.Name,
                DataType = c.DataType,
                IsNullable = c.IsNullable,
                IsPrimaryKey = c.IsPrimaryKey,
                Description = c.Description,
                DefaultValue = c.DefaultValue
            }).ToList();

            return Ok(columnDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving columns for table {TableName}", tableName);
            return StatusCode(500, "An error occurred while retrieving table columns");
        }
    }

    /// <summary>
    /// Export email template as JSON
    /// </summary>
    [HttpGet("{id}/export")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult> ExportTemplate(Guid id)
    {
        try
        {
            var template = await _emailTemplateService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }

            var exportData = new TemplateExportDto
            {
                Name = template.Name,
                Module = template.Module,
                TableName = template.TableName,
                Subject = template.Subject,
                HtmlBody = template.HtmlBody,
                PlainTextBody = template.PlainTextBody,
                SelectedFields = template.SelectedFields,
                Description = template.Description,
                Category = template.Category,
                ExportedAt = DateTime.UtcNow,
                ExportedBy = User.Identity?.Name ?? "Unknown"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"{template.Name.Replace(" ", "_")}_template.json";
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting template {TemplateId}", id);
            return StatusCode(500, "An error occurred while exporting the template");
        }
    }

    /// <summary>
    /// Import email template from JSON
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailTemplateDto>> ImportTemplate([FromBody] TemplateImportDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = new EmailTemplate
            {
                Name = request.Name,
                Module = request.Module,
                TableName = request.TableName,
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                PlainTextBody = request.PlainTextBody,
                SelectedFields = request.SelectedFields,
                Description = request.Description,
                Category = request.Category
            };

            var createdTemplate = await _emailTemplateService.CreateTemplateAsync(template);

            var responseDto = new EmailTemplateDto
            {
                Id = createdTemplate.Id,
                Name = createdTemplate.Name,
                Module = createdTemplate.Module,
                TableName = createdTemplate.TableName,
                Subject = createdTemplate.Subject,
                HtmlBody = createdTemplate.HtmlBody,
                PlainTextBody = createdTemplate.PlainTextBody,
                SelectedFields = createdTemplate.SelectedFields,
                TemplateVariables = createdTemplate.TemplateVariables,
                Description = createdTemplate.Description,
                Category = createdTemplate.Category,
                IsActive = createdTemplate.IsActive,
                CreatedAt = createdTemplate.CreatedAt,
                CreatedBy = createdTemplate.CreatedBy
            };

            return CreatedAtAction(nameof(GetTemplateById), new { id = createdTemplate.Id }, responseDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing email template {TemplateName}", request.Name);
            return StatusCode(500, "An error occurred while importing the email template");
        }
    }
}

// DTOs
public class EmailTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? SelectedFields { get; set; }
    public string? TemplateVariables { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateEmailTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? SelectedFields { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class UpdateEmailTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? SelectedFields { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class DuplicateTemplateDto
{
    public string NewName { get; set; } = string.Empty;
}

public class DatabaseTableDto
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DatabaseColumnDto
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
}

public class TemplateExportDto
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? SelectedFields { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime ExportedAt { get; set; }
    public string ExportedBy { get; set; } = string.Empty;
}

public class TemplateImportDto
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? SelectedFields { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}
