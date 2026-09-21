using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the recruitment lifecycle sweep — the module's first scheduled job.
///
/// <para>All sweep logic lives in <see cref="IRecruitmentLifecycleSweepService"/> so the HR-gated
/// run-now endpoint (<c>POST api/recruitment-dashboard/sweep</c>) exercises exactly the code path
/// this host does — the same host/processor split the probation, discipline, separation, asset and
/// travel engines use.</para>
///
/// <para>Daily rather than hourly: everything swept is a <b>date</b> — an offer's expiry, an
/// advert's closing date, the day a notified departure takes effect. A record that lapsed at
/// 09:00 reading as live until the next morning is not a defect worth a job running twenty-four
/// times to prevent; a record that lapsed in March and still reads as live in September is, and
/// that is what this fixes. Every write is idempotent — the queries only match records not yet
/// advanced — so cadence affects latency only, never correctness.</para>
///
/// <para>⚠ <b>Registration is the part that gets forgotten.</b> Two HR sweeps in this repo were
/// written, tested, and registered nowhere; they ran only when somebody pressed the button, and
/// nobody noticed for months. The line that matters is in
/// <c>ServiceCollectionExtensions.AddHostedService&lt;RecruitmentLifecycleSweepBackgroundService&gt;()</c>.
/// If the counters this fixes start drifting again, check that line before anything else.</para>
/// </summary>
public sealed class RecruitmentLifecycleSweepBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecruitmentLifecycleSweepBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public RecruitmentLifecycleSweepBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RecruitmentLifecycleSweepBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recruitment Lifecycle Sweep Background Service is starting");

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
                    _logger.LogError(ex, "Error occurred during the recruitment lifecycle sweep");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Recruitment Lifecycle Sweep Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Recruitment Lifecycle Sweep Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:recruitment-lifecycle-sweep",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping recruitment lifecycle sweep (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sweepService = scope.ServiceProvider.GetRequiredService<IRecruitmentLifecycleSweepService>();

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await sweepService.RunSweepForTenantAsync(
                    tenantId, "Scheduled", triggeredByUserId: null, cancellationToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Recruitment lifecycle sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
