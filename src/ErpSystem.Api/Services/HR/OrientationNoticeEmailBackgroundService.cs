using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Every minute, sends the emails queued on orientation &amp; onboarding notices (round 4, lane K-b).
///
/// All the logic lives in <see cref="IOrientationNoticeEmailDispatcher"/>, which HR's "Send queued
/// emails now" (POST api/orientation-notifications/send-queued) calls too — and which holds the
/// dispatch lock itself, so this host and the button can never send the same email twice.
///
/// A minute, not a day like the reminder sweeps: these are news — you have been enrolled, your
/// session has moved — and an email a day late is one the in-app notice already beat. A pass with
/// nothing queued is a single indexed query.
///
/// ⚠ Registered with <c>AddHostedService</c> in the same change as this class, as K5 taught.
/// </summary>
public sealed class OrientationNoticeEmailBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrientationNoticeEmailBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public OrientationNoticeEmailBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OrientationNoticeEmailBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orientation Notice Email Background Service is starting");

        try
        {
            // Out of the way of startup and of the minute-zero sweeps.
            await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error occurred while sending orientation notice emails");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Orientation Notice Email Background Service is stopping due to cancellation");
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOrientationNoticeEmailDispatcher>();

        foreach (var tenantId in await dispatcher.TenantsWithQueuedAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await dispatcher.DispatchAsync(tenantId, cancellationToken: cancellationToken);
                if (result.Busy)
                    _logger.LogDebug("Orientation notice emails for tenant {TenantId} skipped this pass (another pass holds the lock)", tenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Orientation notice emails failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
