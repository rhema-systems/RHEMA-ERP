using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed class FacilitiesLeaseReminderService(
    ApplicationDbContext db,
    IDistributedLockService lockService)
{
    public static DateTime? LeaseEndDate(EstateManagedAsset asset)
    {
        var start = asset.DateOfTenancy ?? asset.RightOfEntryDate;
        var months = asset.ExternalLeaseTermMonths is > 0
            ? asset.ExternalLeaseTermMonths.Value
            : asset.LeaseTermYears is > 0 ? asset.LeaseTermYears.Value * 12 : 0;
        return start.HasValue && months > 0 ? start.Value.Date.AddMonths(months) : null;
    }

    public static string? ReminderBand(DateTime expiry, DateTime today)
    {
        var days = (expiry.Date - today.Date).Days;
        if (days < 0) return "expired";
        if (days <= 7) return "7-day";
        if (days <= 30) return "30-day";
        if (days <= 90) return "90-day";
        return null;
    }

    public async Task<int> RunForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var lease = await lockService.TryAcquireAsync(
            $"facilities:lease-reminders:{tenantId}", TimeSpan.FromMinutes(15), cancellationToken);
        if (lease is null) return 0;

        var recipients = await db.Users.AsNoTracking()
            .Where(user => user.IsActive
                && (user.TenantId == tenantId || user.UserTenants.Any(link =>
                    link.TenantId == tenantId && !link.IsDeleted && link.Status == UserTenantStatus.Active))
                && user.UserRoles.Any(link => link.Role.Name == "Facilities Manager"
                    || link.Role.Name == "Facilities Supervisor"
                    || link.Role.Name == "Facilities Officer"))
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (recipients.Count == 0) return 0;

        var assets = await db.EstateManagedAssets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId && !asset.IsDeleted
                && (asset.Status == EstateManagedAssetStatus.Leased || asset.Status == EstateManagedAssetStatus.Occupied)
                && asset.AssetType != EstateManagedAssetType.Land
                && (asset.DateOfTenancy.HasValue || asset.RightOfEntryDate.HasValue)
                && (asset.ExternalLeaseTermMonths > 0 || asset.LeaseTermYears > 0))
            .ToListAsync(cancellationToken);
        var existing = await db.Notifications.AsNoTracking()
            .Where(notification => notification.TenantId == tenantId && !notification.IsDeleted
                && notification.EntityType == "EstateManagedAsset"
                && notification.NotificationType.StartsWith("facilities.lease.expiry."))
            .Select(notification => new { notification.EntityId, notification.RecipientId, notification.NotificationType })
            .ToListAsync(cancellationToken);
        var sent = existing.Select(item => (item.EntityId, item.RecipientId, item.NotificationType)).ToHashSet();
        var today = DateTime.UtcNow.Date;
        var created = 0;

        foreach (var asset in assets)
        {
            var expiry = LeaseEndDate(asset);
            if (!expiry.HasValue) continue;
            var band = ReminderBand(expiry.Value, today);
            if (band is null) continue;
            var type = $"facilities.lease.expiry.{band}.{expiry.Value:yyyyMMdd}";
            var title = band == "expired" ? "Property term expired" : "Property term nearing expiry";
            foreach (var recipientId in recipients)
            {
                if (!sent.Add((asset.Id, recipientId, type))) continue;
                db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    NotificationType = type,
                    Title = title,
                    Message = $"{asset.AssetCode} ({asset.Name}) term ends on {expiry.Value:dd MMM yyyy}. Review renewal or termination in Estate.",
                    Priority = band is "expired" or "7-day" ? "High" : "Normal",
                    RecipientId = recipientId,
                    EntityType = "EstateManagedAsset",
                    EntityId = asset.Id,
                    ActionUrl = "/estate/facilities/EstateFacilityLease",
                    DeliveryMethods = "InApp"
                });
                created++;
            }
        }

        if (created > 0) await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
