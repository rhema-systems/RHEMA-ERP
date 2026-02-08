using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
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

            if (topic.IsRequired && !dto.IsActive)
                return BadRequest("This topic is required and cannot be deactivated.");

            if (topic.IsRequired && !dto.EnableInApp && !dto.EnableEmail)
                return BadRequest("This topic is required and must have at least one channel enabled (In-app or Email).");

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

            // Replace non-system recipients only (system recipients are protected).
            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            var existingRecipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                .Where(r => !r.IsDeleted && !r.IsSystem)
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
                    IsSystem = false,
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

            if (topic.IsSystem)
                return BadRequest("This is a system topic and cannot be deleted.");

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
            IsSystem = t.IsSystem,
            IsRequired = t.IsRequired,
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
                    IsSystem = r.IsSystem,
                    SendInApp = r.SendInApp,
                    SendEmail = r.SendEmail
                })
                .ToList()
        };
    }

    /// <summary>
    /// Seeds system topics (including protected workflow topics) for the current tenant.
    /// Idempotent and safe to run multiple times.
    /// </summary>
    [HttpPost("seed-system")]
    public async Task<ActionResult> SeedSystemTopics()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue) return BadRequest("TenantId not found in token");

            var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? (Guid?)uid : null;

            // Seed workflow topics for each active workflow entity type so critical approval notifications cannot be misconfigured.
            var entityTypeRepo = _unitOfWork.Repository<WorkflowEntityType>();
            var workflowEntityTypes = await entityTypeRepo.FindAsync(et =>
                et.TenantId == tenantId.Value && !et.IsDeleted && et.IsActive);

            var normalizedEntityTypes = workflowEntityTypes
                .Select(et => NormalizeSegment(et.Name))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var workflowActivities = new[]
            {
                "WorkflowSubmitted",
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest",
                "WorkflowStepEscalated",
                "WorkflowCompleted",
                "WorkflowRejected",
                "WorkflowStepOverdue"
            };

            var requiredActivities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest"
            };

            // Default email on for key workflow activities so approvals/submissions can reach users outside the app.
            // Admins can still toggle per topic in the UI after seeding.
            var defaultEmailActivities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WorkflowSubmitted",
                "WorkflowStepAssignment",
                "WorkflowApprovalRequest"
            };

            var topicRepo = _unitOfWork.Repository<NotificationTopic>();
            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();

            var createdTopics = 0;
            var updatedTopics = 0;
            var createdRecipients = 0;

            foreach (var entityType in normalizedEntityTypes)
            {
                foreach (var activity in workflowActivities)
                {
                    var key = GenerateKey(entityType, NormalizeSegment(activity), "Internal");
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    var topic = await topicRepo.FirstOrDefaultAsync(t =>
                        t.TenantId == tenantId.Value &&
                        t.Key == key &&
                        !t.IsDeleted,
                        t => t.Recipients);

                    var isRequired = requiredActivities.Contains(activity);

                    if (topic == null)
                    {
                        topic = new NotificationTopic
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId.Value,
                            Key = key,
                            Name = $"{entityType}: {activity}",
                            Description = "System-seeded workflow notification topic.",
                            EntityType = entityType,
                            IsSystem = true,
                            IsRequired = isRequired,
                            IsActive = true,
                            EnableInApp = true,
                            EnableEmail = defaultEmailActivities.Contains(activity),
                            InAppTitleTemplate = "{{Title}}",
                            InAppBodyTemplate = "{{Message}}",
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = userId
                        };

                        await topicRepo.AddAsync(topic);
                        createdTopics++;
                    }
                    else
                    {
                        var changed = false;
                        if (!topic.IsSystem) { topic.IsSystem = true; changed = true; }
                        if (isRequired && !topic.IsRequired) { topic.IsRequired = true; changed = true; }
                        if (string.IsNullOrWhiteSpace(topic.EntityType)) { topic.EntityType = entityType; changed = true; }
                        if (!topic.IsActive) { topic.IsActive = true; changed = true; }
                        if (defaultEmailActivities.Contains(activity) && !topic.EnableEmail)
                        {
                            topic.EnableEmail = true;
                            changed = true;
                        }
                        if (isRequired && !topic.EnableInApp && !topic.EnableEmail)
                        {
                            // Ensure required topics always have at least one channel.
                            topic.EnableInApp = true;
                            changed = true;
                        }

                        if (changed)
                        {
                            topic.UpdatedAt = DateTime.UtcNow;
                            topic.LastModifiedById = userId;
                            await topicRepo.UpdateAsync(topic);
                            updatedTopics++;
                        }
                    }

                    // Ensure system recipient rules exist (protected from removal by Update).
                    var recipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
                        .Where(r => !r.IsDeleted)
                        .ToList();

                    var systemRules = GetWorkflowSystemRecipients(activity);
                    foreach (var rule in systemRules)
                    {
                        var exists = recipients.Any(r =>
                            r.IsSystem &&
                            string.Equals(r.RecipientKind, rule.Kind, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(r.RecipientValue, rule.Value, StringComparison.OrdinalIgnoreCase));
                        if (exists) continue;

                        await recipientRepo.AddAsync(new NotificationTopicRecipient
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId.Value,
                            TopicId = topic.Id,
                            RecipientKind = rule.Kind,
                            RecipientValue = rule.Value,
                            IsSystem = true,
                            SendInApp = true,
                            SendEmail = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = userId
                        });
                        createdRecipients++;
                    }
                }
            }

            // Seed non-workflow system topics used by background jobs / global event publishing.
            // These are system-provided defaults but remain configurable by admins (recipients/templates/channels).
            var additionalTopics = new[]
            {
                new
                {
                    EntityType = "FleetCompliance",
                    Activity = "ComplianceDueSoon",
                    Audience = "Internal",
                    Name = "Fleet Compliance: Due Soon",
                    Description = "System-seeded fleet compliance reminder (due soon).",
                    EnableEmail = true,
                    InAppTitleTemplate = "Fleet compliance due soon: {{VehicleName}}",
                    InAppBodyTemplate = "{{ComplianceType}} expires on {{ExpiryDate}} ({{DaysToExpiry}} days).",
                    ActionUrlTemplate = "/maintenance/fleet/compliance?vehicleAssetId={{VehicleAssetId}}"
                },
                new
                {
                    EntityType = "FleetCompliance",
                    Activity = "ComplianceOverdue",
                    Audience = "Internal",
                    Name = "Fleet Compliance: Overdue",
                    Description = "System-seeded fleet compliance reminder (overdue).",
                    EnableEmail = true,
                    InAppTitleTemplate = "Fleet compliance overdue: {{VehicleName}}",
                    InAppBodyTemplate = "{{ComplianceType}} expired on {{ExpiryDate}} ({{DaysOverdue}} days overdue).",
                    ActionUrlTemplate = "/maintenance/fleet/compliance?vehicleAssetId={{VehicleAssetId}}"
                }
            };

            foreach (var t in additionalTopics)
            {
                var key = GenerateKey(NormalizeSegment(t.EntityType), NormalizeSegment(t.Activity), NormalizeSegment(t.Audience));
                if (string.IsNullOrWhiteSpace(key)) continue;

                var topic = await topicRepo.FirstOrDefaultAsync(x =>
                    x.TenantId == tenantId.Value &&
                    x.Key == key &&
                    !x.IsDeleted,
                    x => x.Recipients);

                if (topic == null)
                {
                    topic = new NotificationTopic
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId.Value,
                        Key = key,
                        Name = t.Name,
                        Description = t.Description,
                        EntityType = NormalizeSegment(t.EntityType),
                        IsSystem = true,
                        IsRequired = false,
                        IsActive = true,
                        EnableInApp = true,
                        EnableEmail = t.EnableEmail,
                        InAppTitleTemplate = t.InAppTitleTemplate,
                        InAppBodyTemplate = t.InAppBodyTemplate,
                        ActionUrlTemplate = t.ActionUrlTemplate,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = userId
                    };

                    await topicRepo.AddAsync(topic);
                    createdTopics++;
                }
                else
                {
                    var changed = false;

                    if (!topic.IsSystem) { topic.IsSystem = true; changed = true; }
                    if (string.IsNullOrWhiteSpace(topic.EntityType)) { topic.EntityType = NormalizeSegment(t.EntityType); changed = true; }
                    if (!topic.IsActive) { topic.IsActive = true; changed = true; }
                    if (string.IsNullOrWhiteSpace(topic.Name)) { topic.Name = t.Name; changed = true; }
                    if (string.IsNullOrWhiteSpace(topic.Description)) { topic.Description = t.Description; changed = true; }

                    if (string.IsNullOrWhiteSpace(topic.InAppTitleTemplate)) { topic.InAppTitleTemplate = t.InAppTitleTemplate; changed = true; }
                    if (string.IsNullOrWhiteSpace(topic.InAppBodyTemplate)) { topic.InAppBodyTemplate = t.InAppBodyTemplate; changed = true; }
                    if (string.IsNullOrWhiteSpace(topic.ActionUrlTemplate)) { topic.ActionUrlTemplate = t.ActionUrlTemplate; changed = true; }

                    if (changed)
                    {
                        topic.UpdatedAt = DateTime.UtcNow;
                        topic.LastModifiedById = userId;
                        await topicRepo.UpdateAsync(topic);
                        updatedTopics++;
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                createdTopics,
                updatedTopics,
                createdRecipients,
                entityTypes = normalizedEntityTypes.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding system notification topics");
            return StatusCode(500, "An error occurred while seeding system notification topics");
        }
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

    private static List<(string Kind, string Value)> GetWorkflowSystemRecipients(string activity)
    {
        if (string.Equals(activity, "WorkflowApprovalRequest", StringComparison.OrdinalIgnoreCase))
        {
            return new List<(string, string)>
            {
                ("UserFromData", "TargetUserId"),
                ("RoleFromData", "TargetRole")
            };
        }

        if (string.Equals(activity, "WorkflowStepEscalated", StringComparison.OrdinalIgnoreCase))
        {
            return new List<(string, string)>
            {
                ("UsersFromData", "TargetUserIds")
            };
        }

        return new List<(string, string)>
        {
            ("UserFromData", "TargetUserId")
        };
    }
}
