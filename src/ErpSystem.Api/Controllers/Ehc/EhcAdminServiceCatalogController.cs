using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/service-catalog")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminServiceCatalogController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminServiceCatalogController> _logger;

    public EhcAdminServiceCatalogController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminServiceCatalogController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("request-types")]
    public async Task<ActionResult> ListRequestTypes(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcServiceRequestTypeDto>() });
        }

        var items = await _db.EhcServiceRequestTypes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var data = items.Select(t => new EhcServiceRequestTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Description = t.Description,
            IsActive = t.IsActive,
            WorkflowName = t.WorkflowName,
            FormDefinitionJson = t.FormDefinitionJson
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost("request-types")]
    public async Task<ActionResult> CreateRequestType([FromBody] UpsertEhcServiceRequestTypeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, message = "Code is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Name is required." });
            }

            if (!TryValidateAndNormalizeFormDefinition(request.FormDefinitionJson, out var normalizedFormJson, out var formError))
            {
                return BadRequest(new { success = false, message = formError });
            }

            var code = request.Code.Trim();
            var exists = await _db.EhcServiceRequestTypes
                .AsNoTracking()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Code == code, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "A request type with this code already exists." });
            }

            var now = DateTime.UtcNow;
            var item = new EhcServiceRequestType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive,
                WorkflowName = string.IsNullOrWhiteSpace(request.WorkflowName) ? null : request.WorkflowName.Trim(),
                FormDefinitionJson = normalizedFormJson,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcServiceRequestTypes.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcServiceRequestTypeDto
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    IsActive = item.IsActive,
                    WorkflowName = item.WorkflowName,
                    FormDefinitionJson = item.FormDefinitionJson
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC service request type");
            return StatusCode(500, new { success = false, message = "Failed to create service request type" });
        }
    }

    [HttpPut("request-types/{id:guid}")]
    public async Task<ActionResult> UpdateRequestType(Guid id, [FromBody] UpsertEhcServiceRequestTypeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var item = await _db.EhcServiceRequestTypes.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
            if (item == null)
            {
                return NotFound(new { success = false, message = "Request type not found." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, message = "Code is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Name is required." });
            }

            if (!TryValidateAndNormalizeFormDefinition(request.FormDefinitionJson, out var normalizedFormJson, out var formError))
            {
                return BadRequest(new { success = false, message = formError });
            }

            var code = request.Code.Trim();
            var exists = await _db.EhcServiceRequestTypes
                .AsNoTracking()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id != id && t.Code == code, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "A request type with this code already exists." });
            }

            item.Code = code;
            item.Name = request.Name.Trim();
            item.Description = request.Description?.Trim();
            item.IsActive = request.IsActive;
            item.WorkflowName = string.IsNullOrWhiteSpace(request.WorkflowName) ? null : request.WorkflowName.Trim();
            item.FormDefinitionJson = normalizedFormJson;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC service request type {RequestTypeId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update service request type" });
        }
    }

    [HttpDelete("request-types/{id:guid}")]
    public async Task<ActionResult> DeleteRequestType(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var item = await _db.EhcServiceRequestTypes.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
            if (item == null)
            {
                return NotFound(new { success = false, message = "Request type not found." });
            }

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            item.DeletedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC service request type {RequestTypeId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete service request type" });
        }
    }

    private static bool TryValidateAndNormalizeFormDefinition(string json, out string normalizedJson, out string error)
    {
        normalizedJson = "{}";
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "FormDefinitionJson is required.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "FormDefinitionJson must be a JSON object.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Array)
            {
                error = "FormDefinitionJson must include a 'fields' array.";
                return false;
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fieldsEl.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object)
                {
                    error = "Each field must be a JSON object.";
                    return false;
                }

                if (!field.TryGetProperty("key", out var keyEl) || keyEl.ValueKind != JsonValueKind.String)
                {
                    error = "Each field must have a string 'key'.";
                    return false;
                }

                var key = keyEl.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Field 'key' cannot be empty.";
                    return false;
                }

                if (!keys.Add(key))
                {
                    error = $"Duplicate field key '{key}'.";
                    return false;
                }

                if (!field.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                {
                    error = $"Field '{key}' must have a string 'type'.";
                    return false;
                }

                var type = (typeEl.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                var allowed = type is "text" or "textarea" or "number" or "select" or "checkbox" or "date";
                if (!allowed)
                {
                    error = $"Field '{key}' has unsupported type '{type}'.";
                    return false;
                }

                if (type == "select")
                {
                    if (!field.TryGetProperty("options", out var optEl) || optEl.ValueKind != JsonValueKind.Array)
                    {
                        error = $"Field '{key}' of type 'select' must include an 'options' array.";
                        return false;
                    }
                }
            }

            normalizedJson = JsonSerializer.Serialize(doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            error = "FormDefinitionJson must be valid JSON.";
            return false;
        }
        catch
        {
            error = "Invalid form definition.";
            return false;
        }
    }
}

