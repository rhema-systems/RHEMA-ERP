using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the team reminder engine (round 2, lane F2).
/// </summary>
/// <remarks>
/// <para>All sweep logic lives in <see cref="ITeamReminderService"/> so the HR-gated run-now
/// endpoint exercises exactly the code path this host does — the same host/processor split the SHE,
/// probation, movement and separation engines use.</para>
///
/// <para><b>⚠ THIS REGISTRATION IS THE POINT.</b> Two HR sweeps in this codebase turned out never
/// to have run at all: the engine existed, the endpoint existed, and the
/// <c>AddHostedService</c> line did not — so the "advance alerts" a requirement depended on fired
/// only when somebody pressed a button nobody knew about. If you add a sweep, add its host in the
/// same commit, and check <c>TeamReminderRuns</c> has rows before believing it works.</para>
///
/// <para>Daily rather than hourly: the finest thing swept is a date — a task due, a meeting
/// tomorrow — and a reminder about a date does not become more useful for being repeated every
/// hour. Per-item dedupe means the cadence affects latency only, never duplicates.</para>
/// </remarks>
public sealed class TeamReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TeamReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public TeamReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<TeamReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Team Reminder Background Service is starting");

        try
        {
            // Staggered behind the other HR sweeps so a cold start does not run them all at once.
            await Task.Delay(TimeSpan.FromMinutes(11), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during team reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Team Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Team Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:team-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping team reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<ITeamReminderService>();

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await reminderService.RunSweepForTenantAsync(
                    tenantId, "Scheduled", triggeredByUserId: null, cancellationToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Team reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
