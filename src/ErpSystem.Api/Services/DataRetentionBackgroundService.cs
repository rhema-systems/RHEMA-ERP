using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Tenant-scoped data retention purge for audit/security logs, notifications, and EHC audit events.
/// Runs periodically with a distributed lock to avoid multiple instances purging simultaneously.
/// </summary>
public sealed class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public DataRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data retention background service started");

        // First run shortly after startup.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        await SafeRunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await SafeRunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Data retention background service stopped");
    }

    private async Task SafeRunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

            await using var leader = await lockService.TryAcquireAsync(
                lockName: "bg:data-retention",
                leaseDuration: TimeSpan.FromMinutes(55),
                cancellationToken: cancellationToken);

            if (leader == null)
            {
                _logger.LogDebug("Skipping data retention run (lock not acquired)");
                return;
            }

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            var tenantIds = await db.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Status == Shared.TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            foreach (var tenantId in tenantIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunForTenantAsync(db, tenantId, now, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data retention run failed");
        }
    }

    private static async Task RunForTenantAsync(ApplicationDbContext db, Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var policy = await db.DataRetentionPolicies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        if (policy == null)
        {
            policy = new DataRetentionPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Enabled = true,
                AuditLogRetentionDays = 365,
                SecurityLogRetentionDays = 365,
                NotificationRetentionDays = 180,
                EhcAuditEventRetentionDays = 365,
                CreatedAt = now,
                CreatedBy = "System"
            };
            db.DataRetentionPolicies.Add(policy);
            await db.SaveChangesAsync(cancellationToken);
        }

        var run = new DataRetentionJobRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobName = "data-retention",
            StartedAtUtc = now,
            Success = true,
            CreatedAt = now,
            CreatedBy = "System"
        };

        db.DataRetentionJobRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var counts = new Dictionary<string, int>();

        try
        {
            if (!policy.Enabled)
            {
                run.CountsJson = JsonSerializer.Serialize(new { disabled = true });
                run.CompletedAtUtc = DateTime.UtcNow;
                run.Success = true;
                run.UpdatedAt = DateTime.UtcNow;
                run.UpdatedBy = "System";
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            // Audit logs
            var auditCutoff = now.AddDays(-Math.Clamp(policy.AuditLogRetentionDays, 1, 3650));
            counts["AuditLogs"] = await db.AuditLogs
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && a.Timestamp < auditCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            // Security logs
            var securityCutoff = now.AddDays(-Math.Clamp(policy.SecurityLogRetentionDays, 1, 3650));
            counts["SecurityLogs"] = await db.SecurityLogs
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.Timestamp < securityCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            // Notifications (only very old, regardless of read status)
            var notificationCutoff = now.AddDays(-Math.Clamp(policy.NotificationRetentionDays, 1, 3650));
            counts["Notifications"] = await db.Notifications
                .IgnoreQueryFilters()
                .Where(n => n.TenantId == tenantId && n.CreatedAt < notificationCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            // EHC audit events
            var ehcCutoff = now.AddDays(-Math.Clamp(policy.EhcAuditEventRetentionDays, 1, 3650));
            counts["EhcTicketAuditEvents"] = await db.EhcTicketAuditEvents
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && e.CreatedAt < ehcCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            run.CountsJson = JsonSerializer.Serialize(counts);
            run.CompletedAtUtc = DateTime.UtcNow;
            run.Success = true;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = "System";
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            run.CountsJson = JsonSerializer.Serialize(counts);
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            run.UpdatedBy = "System";
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

