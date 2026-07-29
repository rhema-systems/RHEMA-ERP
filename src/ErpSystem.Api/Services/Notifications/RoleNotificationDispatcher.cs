using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Notifications;

internal static class RoleNotificationDispatcher
{
    public static async Task<int> NotifyRolesAsync(
        ApplicationDbContext db,
        INotificationService notificationService,
        Guid tenantId,
        Guid? actorId,
        IEnumerable<string?> roleNames,
        string title,
        string message,
        string type,
        string entityType,
        Guid entityId,
        string? actionUrl,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return 0;
        }

        var targetRoles = roleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!.Trim())
            .Concat(new[] { Constants.Roles.TenantAdmin, Constants.Roles.SuperAdmin })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetRoles.Length == 0)
        {
            return 0;
        }

        var recipients = await db.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .Include(user => user.UserTenants)
            .Where(user => user.IsActive
                && (user.TenantId == tenantId
                    || user.UserTenants.Any(userTenant =>
                        userTenant.TenantId == tenantId
                        && !userTenant.IsDeleted
                        && userTenant.Status == UserTenantStatus.Active
                        && (userTenant.ExpiresAt == null || userTenant.ExpiresAt > DateTime.UtcNow)))
                && user.UserRoles.Any(userRole =>
                    userRole.Role.Name != null && targetRoles.Contains(userRole.Role.Name)))
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var recipientId in recipients)
        {
            await notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = recipientId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Priority = "Normal",
                    EntityType = entityType,
                    EntityId = entityId,
                    ActionUrl = actionUrl,
                    Metadata = metadata
                },
                actorId ?? recipientId,
                tenantId);
        }

        return recipients.Count;
    }
}
