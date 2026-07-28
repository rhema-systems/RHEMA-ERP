using ErpSystem.Api.Controllers;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services;

public sealed class FileStorageCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly ILogger<FileStorageCleanupBackgroundService> _logger;

    public FileStorageCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileUploadOptions> options,
        ILogger<FileStorageCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromSeconds(
            Math.Clamp(options.Value.StorageCleanupIntervalSeconds, 5, 3600));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<FileStorageCleanupProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The controlled file-storage cleanup cycle failed.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
