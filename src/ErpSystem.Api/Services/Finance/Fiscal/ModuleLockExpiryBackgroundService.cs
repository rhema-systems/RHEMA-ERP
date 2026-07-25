using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Fiscal;

/// <summary>
/// Reconciles temporary fiscal-period module reopenings. Posting enforcement also
/// checks ReopenExpiresAtUtc directly, so this worker is for persisted state, audit,
/// notifications, and restoring a suspended global lock - not enforcement correctness.
/// </summary>
public sealed class ModuleLockExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan WarningWindow = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleLockExpiryBackgroundService> _logger;

    public ModuleLockExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModuleLockExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SafeRunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await SafeRunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private async Task SafeRunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = scope.ServiceProvider.GetService<INotificationService>();
            var now = DateTime.UtcNow;
            var candidates = await db.PeriodModuleLocks
                .IgnoreQueryFilters()
                .Include(item => item.FiscalPeriod)
                .Include(item => item.ModuleDefinition)
                .Where(item =>
                    !item.IsDeleted
                    && !item.IsLocked
                    && item.ReopenExpiresAtUtc.HasValue
                    && item.ReopenExpiresAtUtc <= now.Add(WarningWindow))
                .OrderBy(item => item.ReopenExpiresAtUtc)
                .Take(250)
                .ToListAsync(cancellationToken);

            var expiredPeriodIds = new HashSet<Guid>();
            foreach (var moduleLock in candidates)
            {
                if (moduleLock.ReopenExpiresAtUtc > now)
                {
                    if (moduleLock.ExpiryWarningSentAtUtc.HasValue)
                        continue;

                    moduleLock.ExpiryWarningSentAtUtc = now;
                    moduleLock.UpdatedAt = now;
                    moduleLock.UpdatedBy = "System";
                    await TryNotifyAsync(
                        notifications,
                        moduleLock,
                        "Module reopening expires soon",
                        $"{moduleLock.ModuleDefinition.ModuleName} will automatically relock for " +
                        $"{moduleLock.FiscalPeriod.PeriodName} at {moduleLock.ReopenExpiresAtUtc:u}.",
                        "FinanceModuleLockExpiryWarning",
                        cancellationToken);
                    continue;
                }

                var before = new
                {
                    moduleLock.IsLocked,
                    moduleLock.ReopenExpiresAtUtc,
                    moduleLock.UnlockReason
                };
                moduleLock.IsLocked = true;
                moduleLock.AutoRelockedDate = now;
                moduleLock.LockedDate = now;
                moduleLock.LockedByUserId = null;
                moduleLock.LockReason = $"Automatically relocked when temporary reopening expired at {moduleLock.ReopenExpiresAtUtc:u}.";
                moduleLock.UpdatedAt = now;
                moduleLock.UpdatedBy = "System";
                expiredPeriodIds.Add(moduleLock.FiscalPeriodId);

                await AddAutomaticRelockAuditAsync(db, moduleLock, before, now, cancellationToken);
                await TryNotifyAsync(
                    notifications,
                    moduleLock,
                    "Module automatically relocked",
                    $"{moduleLock.ModuleDefinition.ModuleName} has automatically relocked for " +
                    $"{moduleLock.FiscalPeriod.PeriodName}.",
                    "FinanceModuleAutoRelocked",
                    cancellationToken);
            }

            if (candidates.Count > 0)
                await db.SaveChangesAsync(cancellationToken);

            if (expiredPeriodIds.Count == 0)
                return;

            var periods = await db.FiscalPeriods
                .IgnoreQueryFilters()
                .Include(period => period.PeriodModuleLocks)
                .Where(period =>
                    !period.IsDeleted
                    && expiredPeriodIds.Contains(period.Id)
                    && period.IsGlobalLockSuspended)
                .ToListAsync(cancellationToken);

            foreach (var period in periods)
            {
                var hasActiveReopening = period.PeriodModuleLocks.Any(moduleLock =>
                    !moduleLock.IsDeleted
                    && !moduleLock.IsLocked
                    && moduleLock.ReopenExpiresAtUtc.HasValue
                    && moduleLock.ReopenExpiresAtUtc > now);
                if (hasActiveReopening)
                    continue;

                period.IsGlobalLockSuspended = false;
                period.IsLocked = true;
                period.IsOpen = false;
                period.IsClosed = true;
                period.PeriodStatus = "Locked";
                period.UpdatedAt = now;
                period.UpdatedBy = "System";
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Fiscal-period module lock expiry reconciliation failed");
        }
    }

    private static async Task AddAutomaticRelockAuditAsync(
        ApplicationDbContext db,
        ErpSystem.Core.Entities.Finance.PeriodModuleLock moduleLock,
        object before,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!moduleLock.UnlockedByUserId.HasValue)
            return;

        var authorizingUserId = moduleLock.UnlockedByUserId.Value;
        var userExists = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Id == authorizingUserId, cancellationToken);
        if (!userExists)
            return;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = moduleLock.TenantId,
            UserId = authorizingUserId,
            Username = "System (automatic expiry)",
            Action = FinanceAuditEvents.AccountingPeriodModuleAutoRelocked,
            Resource = "Finance.FiscalPeriod",
            ResourceId = moduleLock.FiscalPeriodId.ToString(),
            OldValues = JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new
            {
                moduleLock.Id,
                moduleLock.ModuleDefinitionId,
                moduleLock.IsLocked,
                moduleLock.AutoRelockedDate,
                moduleLock.LockReason,
                authorizedByUserId = authorizingUserId
            }, JsonOptions),
            IpAddress = "System",
            UserAgent = nameof(ModuleLockExpiryBackgroundService),
            Timestamp = now,
            CreatedAt = now,
            CreatedBy = "System",
            CreatedById = authorizingUserId
        });
    }

    private async Task TryNotifyAsync(
        INotificationService? notifications,
        ErpSystem.Core.Entities.Finance.PeriodModuleLock moduleLock,
        string title,
        string message,
        string notificationType,
        CancellationToken cancellationToken)
    {
        if (notifications == null || !moduleLock.UnlockedByUserId.HasValue)
            return;

        try
        {
            await notifications.CreateInAppNotificationAsync(
                moduleLock.UnlockedByUserId.Value,
                title,
                message,
                notificationType,
                new Dictionary<string, object>
                {
                    ["fiscalPeriodId"] = moduleLock.FiscalPeriodId,
                    ["moduleCode"] = moduleLock.ModuleDefinition.ModuleCode,
                    ["reopenExpiresAtUtc"] = moduleLock.ReopenExpiresAtUtc?.ToString("O") ?? string.Empty
                },
                moduleLock.TenantId);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Unable to send module lock expiry notification for {PeriodId}/{ModuleCode}",
                moduleLock.FiscalPeriodId,
                moduleLock.ModuleDefinition.ModuleCode);
        }
    }
}
