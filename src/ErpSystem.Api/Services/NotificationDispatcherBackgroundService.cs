using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services;

/// <summary>
/// Background service that periodically sends pending notifications.
/// Implements enterprise-grade queued notification delivery with retries and dead-letter handling.
/// </summary>
public class NotificationDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDispatcherBackgroundService> _logger;

    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _dispatchInterval;
    private readonly TimeSpan _initialBackoffDelay;
    private readonly double _backoffMultiplier;

    public NotificationDispatcherBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<NotificationDispatcherBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;

        // Load configuration with sensible defaults
        _maxRetryAttempts = int.TryParse(_configuration["Notifications:MaxRetryAttempts"], out var max) ? max : 5;
        if (int.TryParse(_configuration["Notifications:DispatchIntervalSeconds"], out var intervalSeconds) && intervalSeconds > 0)
        {
            _dispatchInterval = TimeSpan.FromSeconds(intervalSeconds);
        }
        else if (int.TryParse(_configuration["Notifications:DispatchIntervalMinutes"], out var intervalMinutes) && intervalMinutes > 0)
        {
            _dispatchInterval = TimeSpan.FromMinutes(intervalMinutes);
        }
        else
        {
            // Default to a short interval so in-app/email queue processing feels near real-time.
            _dispatchInterval = TimeSpan.FromSeconds(30);
        }
        _initialBackoffDelay = int.TryParse(_configuration["Notifications:InitialBackoffSeconds"], out var backoff)
            ? TimeSpan.FromSeconds(backoff)
            : TimeSpan.FromSeconds(30);
        _backoffMultiplier = double.TryParse(_configuration["Notifications:BackoffMultiplier"], out var mult) ? mult : 1.5;
    }

    public NotificationDispatcherBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationDispatcherBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = new ConfigurationBuilder().Build();
        _logger = logger;

        // Initialize with default values since configuration is not provided
        _maxRetryAttempts = 5;
        _dispatchInterval = TimeSpan.FromSeconds(30);
        _initialBackoffDelay = TimeSpan.FromSeconds(30);
        _backoffMultiplier = 1.5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Notification Dispatcher Background Service is starting with MaxRetries={MaxRetries}, DispatchInterval={DispatchIntervalSeconds}s",
            _maxRetryAttempts, _dispatchInterval.TotalSeconds);

        try
        {
            // Wait a bit before starting the first dispatch to allow app to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchPendingNotificationsAsync();
                    await ArchiveExpiredNotificationsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during notification dispatch");
                }

                // Wait for the next dispatch cycle
                await Task.Delay(_dispatchInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
            _logger.LogInformation("Notification Dispatcher Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Notification Dispatcher Background Service");
            throw;
        }

        _logger.LogInformation("Notification Dispatcher Background Service has stopped");
    }

    private async Task DispatchPendingNotificationsAsync()
    {
        _logger.LogInformation("Starting pending notification dispatch");

        using var scope = _serviceProvider.CreateScope();

        try
        {
            var notificationService = scope.ServiceProvider
                .GetRequiredService<IMaintenanceNotificationService>();

            var sentCount = await notificationService.SendPendingNotificationsAsync();

            if (sentCount > 0)
            {
                _logger.LogInformation("Notification dispatch completed. Sent {Count} pending notifications", sentCount);
            }
            else
            {
                _logger.LogDebug("Notification dispatch completed. No pending notifications to send");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pending notification dispatch");
        }

        try
        {
            var unifiedNotificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await unifiedNotificationService.ProcessPendingNotificationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unified pending notification dispatch");
        }
    }

    private async Task ArchiveExpiredNotificationsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<ErpSystem.Core.Interfaces.IUnitOfWork>();

            var repo = unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.MaintenanceNotification>();

            // Find notifications that have exceeded max retries
            var expiredNotifications = await repo.FindAsync(n =>
                n.Status == "Pending" && n.AttemptCount >= _maxRetryAttempts);

            var archivedCount = 0;
            foreach (var notification in expiredNotifications)
            {
                notification.Status = "DeadLetter";
                await repo.UpdateAsync(notification);
                archivedCount++;
            }

            if (archivedCount > 0)
            {
                await unitOfWork.SaveChangesAsync();
                _logger.LogWarning("Archived {Count} notifications to dead-letter queue after exceeding max retries", archivedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dead-letter archival");
        }

        try
        {
            using var scope2 = _serviceProvider.CreateScope();
            var unifiedNotificationService = scope2.ServiceProvider.GetRequiredService<INotificationService>();
            await unifiedNotificationService.CleanupExpiredNotificationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unified notification cleanup");
        }
    }
}
