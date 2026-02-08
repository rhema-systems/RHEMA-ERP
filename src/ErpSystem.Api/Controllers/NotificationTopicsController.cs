using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/notification-topics")]
[Authorize(Roles = "SuperAdmin,TenantAdmin")]
public class NotificationTopicsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationTopicsController> _logger;

    public NotificationTopicsController(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<NotificationTopicsController> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationTopicDto>>> GetAll()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var repo = _unitOfWork.Repository<NotificationTopic>();
            var topics = await repo.FindAsync(t => t.TenantId == tenantId.Value && !t.IsDeleted, t => t.Recipients);

            var list = topics
                .OrderBy(t => t.EntityType)
                .ThenBy(t => t.Name)
                .Select(Map)
                .ToList();

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification topics");
            return StatusCode(500, "An error occurred while retrieving notification topics");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationTopicDto>> GetById(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var repo = _unitOfWork.Repository<NotificationTopic>();
            var topic = await repo.FirstOrDefaultAsync(
                t => t.Id == id && t.TenantId == tenantId.Value && !t.IsDeleted,
                t => t.Recipients);

            if (topic == null) return NotFound();
            return Ok(Map(topic));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification topic {TopicId}", id);
            return StatusCode(500, "An error occurred while retrieving the notification topic");
        }
    }

    [HttpPost]
    public async Task<ActionResult<NotificationTopicDto>> Create([FromBody] CreateNotificationTopicDto dto)
    {
        try
        {
            dto ??= new CreateNotificationTopicDto();

            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? (Guid?)uid : null;

            var normalizedEntityType = NormalizeSegment(dto.EntityType);
            var activity = NormalizeSegment(dto.Activity);
            var audience = NormalizeSegment(dto.Audience);
            var key = GenerateKey(normalizedEntityType, activity, audience);

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEntityType)) return BadRequest("EntityType is required");
            if (string.IsNullOrWhiteSpace(activity)) return BadRequest("Activity is required");
            if (string.IsNullOrWhiteSpace(audience)) return BadRequest("Audience is required");
            if (activity.Length > 80) return BadRequest("Activity is too long.");
            if (audience.Length > 80) return BadRequest("Audience is too long.");
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required");

            var repo = _unitOfWork.Repository<NotificationTopic>();
            var exists = await repo.ExistsAsync(t => t.TenantId == tenantId.Value && t.Key == key && !t.IsDeleted);
            if (exists) return Conflict($"A notification topic with key '{key}' already exists.");

            var topic = new NotificationTopic
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                Key = key,
                Name = name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                EntityType = normalizedEntityType,
                IsActive = dto.IsActive,
                EnableInApp = dto.EnableInApp,
                EnableEmail = dto.EnableEmail,
                InAppTitleTemplate = string.IsNullOrWhiteSpace(dto.InAppTitleTemplate) ? null : dto.InAppTitleTemplate.Trim(),
                InAppBodyTemplate = string.IsNullOrWhiteSpace(dto.InAppBodyTemplate) ? null : dto.InAppBodyTemplate.Trim(),
                EmailTemplateId = dto.EmailTemplateId,
                ActionUrlTemplate = string.IsNullOrWhiteSpace(dto.ActionUrlTemplate) ? null : dto.ActionUrlTemplate.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };

            await repo.AddAsync(topic);

            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            foreach (var r in (dto.Recipients ?? new List<CreateNotificationTopicRecipientDto>()))
            {
                var kind = (r.RecipientKind ?? string.Empty).Trim();
                var value = (r.RecipientValue ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value)) continue;

                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    TopicId = topic.Id,
                    RecipientKind = kind,
                    RecipientValue = value,
                    SendInApp = r.SendInApp,
                    SendEmail = r.SendEmail,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            var created = await repo.FirstOrDefaultAsync(t => t.Id == topic.Id, t => t.Recipients);
            return CreatedAtAction(nameof(GetById), new { id = topic.Id }, created == null ? Map(topic) : Map(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification topic");
            return StatusCode(500, "An error occurred while creating the notification topic");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationTopicDto>> Update(Guid id, [FromBody] UpdateNotificationTopicDto dto)
    {
        try
        {
            dto ??= new UpdateNotificationTopicDto();

            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? (Guid?)uid : null;

            var repo = _unitOfWork.Repository<NotificationTopic>();
            var topic = await repo.FirstOrDefaultAsync(
                t => t.Id == id && t.TenantId == tenantId.Value && !t.IsDeleted,
                t => t.Recipients);

            if (topic == null) return NotFound();

            // Key is system-controlled and cannot be changed once created.
            var normalizedEntityType = string.IsNullOrWhiteSpace(dto.EntityType) ? topic.EntityType : NormalizeSegment(dto.EntityType);
            var activity = NormalizeSegment(dto.Activity);
            var audience = NormalizeSegment(dto.Audience);
            var key = !string.IsNullOrWhiteSpace(normalizedEntityType) &&
                      !string.IsNullOrWhiteSpace(activity) &&
                      !string.IsNullOrWhiteSpace(audience)
                ? GenerateKey(normalizedEntityType, activity, audience)
                : (dto.Key ?? string.Empty).Trim();

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required");

            if (!string.Equals(topic.Key, key, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Key cannot be changed once created.");

            if (dto.EntityType != null && !string.Equals(topic.EntityType, normalizedEntityType, StringComparison.OrdinalIgnoreCase))
                return BadRequest("EntityType cannot be changed once created.");

            topic.Name = name;
            topic.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            topic.IsActive = dto.IsActive;
            topic.EnableInApp = dto.EnableInApp;
            topic.EnableEmail = dto.EnableEmail;
            topic.InAppTitleTemplate = string.IsNullOrWhiteSpace(dto.InAppTitleTemplate) ? null : dto.InAppTitleTemplate.Trim();
            topic.InAppBodyTemplate = string.IsNullOrWhiteSpace(dto.InAppBodyTemplate) ? null : dto.InAppBodyTemplate.Trim();
            topic.EmailTemplateId = dto.EmailTemplateId;
            topic.ActionUrlTemplate = string.IsNullOrWhiteSpace(dto.ActionUrlTemplate) ? null : dto.ActionUrlTemplate.Trim();
            topic.UpdatedAt = DateTime.UtcNow;
            topic.LastModifiedById = userId;

            await repo.UpdateAsync(topic);

            // Replace recipients (soft delete existing and insert new).
            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            var existingRecipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                .Where(r => !r.IsDeleted)
                .ToList();
            foreach (var r in existingRecipients)
            {
                r.IsDeleted = true;
                r.DeletedAt = DateTime.UtcNow;
                r.LastModifiedById = userId;
                await recipientRepo.UpdateAsync(r);
            }

            foreach (var r in (dto.Recipients ?? new List<CreateNotificationTopicRecipientDto>()))
            {
                var kind = (r.RecipientKind ?? string.Empty).Trim();
                var value = (r.RecipientValue ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value)) continue;

                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    TopicId = topic.Id,
                    RecipientKind = kind,
                    RecipientValue = value,
                    SendInApp = r.SendInApp,
                    SendEmail = r.SendEmail,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            var updated = await repo.FirstOrDefaultAsync(t => t.Id == topic.Id, t => t.Recipients);
            return Ok(updated == null ? Map(topic) : Map(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification topic {TopicId}", id);
            return StatusCode(500, "An error occurred while updating the notification topic");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? (Guid?)uid : null;

            var repo = _unitOfWork.Repository<NotificationTopic>();
            var topic = await repo.FirstOrDefaultAsync(
                t => t.Id == id && t.TenantId == tenantId.Value && !t.IsDeleted,
                t => t.Recipients);

            if (topic == null) return NotFound();

            topic.IsDeleted = true;
            topic.DeletedAt = DateTime.UtcNow;
            topic.LastModifiedById = userId;
            await repo.UpdateAsync(topic);

            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            foreach (var r in (topic.Recipients ?? new List<NotificationTopicRecipient>()).Where(r => !r.IsDeleted))
            {
                r.IsDeleted = true;
                r.DeletedAt = DateTime.UtcNow;
                r.LastModifiedById = userId;
                await recipientRepo.UpdateAsync(r);
            }

            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification topic {TopicId}", id);
            return StatusCode(500, "An error occurred while deleting the notification topic");
        }
    }

    private static NotificationTopicDto Map(NotificationTopic t)
    {
        var (entityTypeFromKey, activity, audience) = SplitKey(t.Key);
        return new NotificationTopicDto
        {
            Id = t.Id,
            Key = t.Key,
            Activity = activity,
            Audience = audience,
            Name = t.Name,
            Description = t.Description,
            EntityType = string.IsNullOrWhiteSpace(t.EntityType) ? entityTypeFromKey : t.EntityType,
            IsActive = t.IsActive,
            EnableInApp = t.EnableInApp,
            EnableEmail = t.EnableEmail,
            InAppTitleTemplate = t.InAppTitleTemplate,
            InAppBodyTemplate = t.InAppBodyTemplate,
            EmailTemplateId = t.EmailTemplateId,
            ActionUrlTemplate = t.ActionUrlTemplate,
            Recipients = (t.Recipients ?? new List<NotificationTopicRecipient>())
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.RecipientKind)
                .ThenBy(r => r.RecipientValue)
                .Select(r => new NotificationTopicRecipientDto
                {
                    Id = r.Id,
                    RecipientKind = r.RecipientKind,
                    RecipientValue = r.RecipientValue,
                    SendInApp = r.SendInApp,
                    SendEmail = r.SendEmail
                })
                .ToList()
        };
    }

    private static (string? entityType, string? activity, string? audience) SplitKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return (null, null, null);
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return (parts.FirstOrDefault(), null, null);
        return (parts[0], parts[1], parts[2]);
    }

    private static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        var chars = trimmed.Where(ch => char.IsLetterOrDigit(ch)).ToArray();
        return chars.Length == 0 ? string.Empty : new string(chars);
    }

    private static string GenerateKey(string entityType, string activity, string audience)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return string.Empty;
        if (string.IsNullOrWhiteSpace(activity)) return string.Empty;
        if (string.IsNullOrWhiteSpace(audience)) return string.Empty;
        return $"{entityType}.{activity}.{audience}";
    }
}
