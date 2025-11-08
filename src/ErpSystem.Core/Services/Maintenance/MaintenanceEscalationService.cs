using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for evaluating and executing maintenance notification escalation rules.
/// Handles automatic priority escalation, notification retargeting, and escalation notifications.
/// </summary>
public interface IMaintenanceEscalationService
{
    Task<MaintenanceEscalationRuleDto?> GetEscalationRuleByIdAsync(Guid id);
    Task<List<MaintenanceEscalationRuleDto>> GetRulesByEntityTypeAsync(string entityType);
    Task<MaintenanceEscalationRuleDto> CreateEscalationRuleAsync(MaintenanceEscalationRuleDto dto);
    Task<MaintenanceEscalationRuleDto> UpdateEscalationRuleAsync(Guid id, MaintenanceEscalationRuleDto dto);
    Task DeleteEscalationRuleAsync(Guid id);
    Task<List<MaintenanceEscalationRuleDto>> GetAllRulesAsync();
    
    // Escalation evaluation
    Task EvaluateAndExecuteEscalationsAsync();
    Task<int> EscalateOverdueNotificationsAsync(string entityType, int overdueHours);
}

public class MaintenanceEscalationService : IMaintenanceEscalationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMaintenanceNotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MaintenanceEscalationService> _logger;

    public MaintenanceEscalationService(
        IUnitOfWork unitOfWork,
        IMaintenanceNotificationService notificationService,
        ICurrentUserService currentUserService,
        ILogger<MaintenanceEscalationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<MaintenanceEscalationRuleDto?> GetEscalationRuleByIdAsync(Guid id)
    {
        try
        {
            var rule = await _unitOfWork.Repository<MaintenanceEscalationRule>().GetByIdAsync(id);
            return rule == null ? null : MapToDto(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting escalation rule {RuleId}", id);
            throw;
        }
    }

    public async Task<List<MaintenanceEscalationRuleDto>> GetRulesByEntityTypeAsync(string entityType)
    {
        try
        {
            var rules = await _unitOfWork.Repository<MaintenanceEscalationRule>()
                .FindAsync(r => r.EntityType == entityType);

            return rules.Select(MapToDto).OrderBy(r => r.HoursOverdue).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting escalation rules for entity type {EntityType}", entityType);
            throw;
        }
    }

    public async Task<MaintenanceEscalationRuleDto> CreateEscalationRuleAsync(MaintenanceEscalationRuleDto dto)
    {
        try
        {
            var entity = new MaintenanceEscalationRule
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                EntityType = dto.EntityType,
                TriggerCondition = dto.TriggerCondition,
                HoursOverdue = dto.HoursOverdue,
                TriggerPriority = dto.TriggerPriority
            };

            var createdEntity = await _unitOfWork.Repository<MaintenanceEscalationRule>().AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created escalation rule {RuleName} for entity type {EntityType}", entity.Name, entity.EntityType);

            return MapToDto(createdEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating escalation rule {RuleName}", dto.Name);
            throw;
        }
    }

    public async Task<MaintenanceEscalationRuleDto> UpdateEscalationRuleAsync(Guid id, MaintenanceEscalationRuleDto dto)
    {
        try
        {
            var entity = await _unitOfWork.Repository<MaintenanceEscalationRule>().GetByIdAsync(id);
            if (entity == null)
                throw new ArgumentException($"Escalation rule with ID {id} not found");

            entity.Name = dto.Name;
            entity.EntityType = dto.EntityType;
            entity.TriggerCondition = dto.TriggerCondition;
            entity.HoursOverdue = dto.HoursOverdue;
            entity.TriggerPriority = dto.TriggerPriority;

            await _unitOfWork.Repository<MaintenanceEscalationRule>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated escalation rule {RuleName} ({RuleId})", entity.Name, id);

            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating escalation rule {RuleId}", id);
            throw;
        }
    }

    public async Task DeleteEscalationRuleAsync(Guid id)
    {
        try
        {
            var entity = await _unitOfWork.Repository<MaintenanceEscalationRule>().GetByIdAsync(id);
            if (entity == null)
                throw new ArgumentException($"Escalation rule with ID {id} not found");

            await _unitOfWork.Repository<MaintenanceEscalationRule>().DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted escalation rule {RuleName} ({RuleId})", entity.Name, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting escalation rule {RuleId}", id);
            throw;
        }
    }

    public async Task<List<MaintenanceEscalationRuleDto>> GetAllRulesAsync()
    {
        try
        {
            var rules = await _unitOfWork.Repository<MaintenanceEscalationRule>().GetAllAsync();
            return rules.Select(MapToDto).OrderBy(r => r.EntityType).ThenBy(r => r.HoursOverdue).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all escalation rules");
            throw;
        }
    }

    public async Task EvaluateAndExecuteEscalationsAsync()
    {
        try
        {
            _logger.LogInformation("Starting escalation rule evaluation");

            var rules = await GetAllRulesAsync();
            var escalatedCount = 0;

            foreach (var rule in rules)
            {
                try
                {
                    var count = await EscalateOverdueNotificationsAsync(rule.EntityType, rule.HoursOverdue);
                    escalatedCount += count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating escalation rule {RuleName}", rule.Name);
                }
            }

            _logger.LogInformation("Escalation evaluation completed. Escalated {Count} notifications", escalatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during escalation evaluation");
            throw;
        }
    }

    public async Task<int> EscalateOverdueNotificationsAsync(string entityType, int overdueHours)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-overdueHours);
            var repo = _unitOfWork.Repository<MaintenanceNotification>();

            var overdueNotifications = await repo.FindAsync(n =>
                n.EntityType == entityType &&
                n.Status == "Pending" &&
                n.ScheduledFor <= cutoffTime);

            var escalatedCount = 0;

            foreach (var notification in overdueNotifications)
            {
                try
                {
                    // Bump priority to Critical if not already
                    var newPriority = GetEscalatedPriority(notification.Priority);
                    notification.Priority = newPriority;
                    notification.Message = $"[ESCALATED] {notification.Message}";

                    await repo.UpdateAsync(notification);

                    // Send escalation notification immediately
                    var escalationNotification = new CreateMaintenanceNotificationDto
                    {
                        NotificationType = "Escalation",
                        EntityType = notification.EntityType,
                        EntityId = notification.EntityId,
                        RecipientId = notification.RecipientId,
                        RecipientRole = notification.RecipientRole,
                        Title = $"[ESCALATED] {notification.Title}",
                        Message = $"This notification has been escalated due to being overdue for {overdueHours} hours.",
                        Priority = newPriority,
                        ScheduledFor = DateTime.UtcNow,
                        ActionUrl = notification.ActionUrl
                    };

                    await _notificationService.CreateNotificationAsync(escalationNotification);

                    escalatedCount++;

                    _logger.LogWarning(
                        "Escalated notification {NotificationId} for {EntityType} {EntityId} from {OldPriority} to {NewPriority}",
                        notification.Id, entityType, notification.EntityId, notification.Priority, newPriority);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error escalating notification {NotificationId}", notification.Id);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return escalatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error escalating overdue notifications for entity type {EntityType}", entityType);
            throw;
        }
    }

    private string GetEscalatedPriority(string currentPriority)
    {
        return currentPriority.ToLower() switch
        {
            "low" => "Normal",
            "normal" => "High",
            "high" => "Critical",
            "critical" => "Critical",
            _ => "Critical"
        };
    }

    private MaintenanceEscalationRuleDto MapToDto(MaintenanceEscalationRule entity)
    {
        return new MaintenanceEscalationRuleDto
        {
            Id = entity.Id,
            Name = entity.Name,
            EntityType = entity.EntityType,
            TriggerCondition = entity.TriggerCondition,
            HoursOverdue = entity.HoursOverdue,
            TriggerPriority = entity.TriggerPriority
        };
    }
}
