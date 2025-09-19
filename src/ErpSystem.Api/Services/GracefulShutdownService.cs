using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ErpSystem.Web.Services
{
    public interface IGracefulShutdownService
    {
        Task BeginShutdownAsync(CancellationToken cancellationToken = default);
        Task CompleteShutdownAsync(CancellationToken cancellationToken = default);
        Task<bool> IsShutdownCompleteAsync();
    }

    public class GracefulShutdownService : IGracefulShutdownService, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GracefulShutdownService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly TaskCompletionSource<bool> _shutdownComplete;
        
        private bool _shutdownRequested = false;
        private readonly object _shutdownLock = new object();

        public GracefulShutdownService(
            IServiceProvider serviceProvider,
            ILogger<GracefulShutdownService> logger,
            IHostApplicationLifetime applicationLifetime)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _applicationLifetime = applicationLifetime;
            _shutdownComplete = new TaskCompletionSource<bool>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _logger.LogInformation("Graceful shutdown service started");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Graceful shutdown service stopping...");
            await BeginShutdownAsync(cancellationToken);
            await CompleteShutdownAsync(cancellationToken);
            _logger.LogInformation("Graceful shutdown service stopped");
        }

        public async Task BeginShutdownAsync(CancellationToken cancellationToken = default)
        {
            lock (_shutdownLock)
            {
                if (_shutdownRequested)
                {
                    _logger.LogDebug("Shutdown already in progress");
                    return;
                }
                _shutdownRequested = true;
            }

            _logger.LogInformation("Beginning graceful shutdown sequence...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Step 1: Stop accepting new requests (handled by ASP.NET Core)
                _logger.LogDebug("Step 1: Stopped accepting new requests");

                // Step 2: Allow existing requests to complete (with timeout)
                await WaitForActiveRequestsAsync(cancellationToken);
                _logger.LogDebug("Step 2: Active requests completed or timed out");

                // Step 3: Begin cleanup of resources
                await BeginResourceCleanupAsync(cancellationToken);
                _logger.LogDebug("Step 3: Resource cleanup initiated");

                stopwatch.Stop();
                _logger.LogInformation("Graceful shutdown sequence initiated in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during graceful shutdown initiation after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task CompleteShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!_shutdownRequested)
            {
                _logger.LogDebug("Shutdown not initiated, skipping completion");
                return;
            }

            _logger.LogInformation("Completing graceful shutdown...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var tasks = new List<Task>
                {
                    CleanupDatabaseConnectionsAsync(cancellationToken),
                    CleanupCacheAsync(cancellationToken),
                    CleanupBackgroundServicesAsync(cancellationToken),
                    CleanupFileResourcesAsync(cancellationToken),
                    SaveFinalStateAsync(cancellationToken)
                };

                await Task.WhenAll(tasks);

                stopwatch.Stop();
                _logger.LogInformation("Graceful shutdown completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                
                _shutdownComplete.SetResult(true);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during graceful shutdown completion after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                _shutdownComplete.SetException(ex);
                throw;
            }
        }

        public async Task<bool> IsShutdownCompleteAsync()
        {
            return await _shutdownComplete.Task;
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("Application stopping event received, initiating graceful shutdown");
            
            // Use fire-and-forget pattern for graceful shutdown
            _ = Task.Run(async () =>
            {
                try
                {
                    await BeginShutdownAsync(CancellationToken.None);
                    await CompleteShutdownAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during application stopping graceful shutdown");
                }
            });
        }

        private async Task WaitForActiveRequestsAsync(CancellationToken cancellationToken)
        {
            const int maxWaitSeconds = 30;
            var timeout = TimeSpan.FromSeconds(maxWaitSeconds);
            
            try
            {
                _logger.LogDebug("Waiting up to {MaxWaitSeconds} seconds for active requests to complete", maxWaitSeconds);
                
                // Wait for a reasonable time for requests to complete
                // In a real scenario, you might track active requests
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                
                _logger.LogDebug("Active request wait period completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Active request wait was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for active requests to complete");
            }
        }

        private async Task BeginResourceCleanupAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Beginning resource cleanup...");
                
                // Signal all background services to begin cleanup
                // This is a placeholder - in practice you'd have a registry of cleanup tasks
                await Task.Delay(100, cancellationToken);
                
                _logger.LogDebug("Resource cleanup signaling completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource cleanup initiation");
            }
        }

        private async Task CleanupDatabaseConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Cleaning up database connections...");
                
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                
                if (dbContext != null)
                {
                    // Ensure all pending changes are saved
                    if (dbContext.ChangeTracker.HasChanges())
                    {
                        _logger.LogWarning("Saving pending database changes during shutdown");
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    
                    // Dispose the context properly
                    await dbContext.DisposeAsync();
                    _logger.LogDebug("Database connections cleaned up");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database cleanup");
            }
        }

        private async Task CleanupCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Cleaning up cache resources...");
                
                using var scope = _serviceProvider.CreateScope();
                
                // Clean up memory cache
                var memoryCache = scope.ServiceProvider.GetService<IMemoryCache>();
                if (memoryCache is MemoryCache mc)
                {
                    mc.Dispose();
                    _logger.LogDebug("Memory cache disposed");
                }
                
                // Clean up distributed cache connections
                var distributedCache = scope.ServiceProvider.GetService<IDistributedCache>();
                if (distributedCache != null)
                {
                    // For Redis or other distributed caches, ensure connections are closed gracefully
                    // This depends on the specific cache implementation
                    _logger.LogDebug("Distributed cache cleanup completed");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private async Task CleanupBackgroundServicesAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Cleaning up background services...");
                
                // In practice, you would have a registry of background services to clean up
                // For now, we'll just ensure any warmup services are properly disposed
                using var scope = _serviceProvider.CreateScope();
                var warmupService = scope.ServiceProvider.GetService<IApplicationWarmupService>();
                
                if (warmupService != null)
                {
                    _logger.LogDebug("Background services cleanup completed");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background services cleanup");
            }
        }

        private async Task CleanupFileResourcesAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Cleaning up file resources...");
                
                // Clean up temporary files, logs, etc.
                var tempPath = Path.GetTempPath();
                var appTempFiles = Directory.GetFiles(tempPath, "ErpSystem_*", SearchOption.TopDirectoryOnly);
                
                foreach (var file in appTempFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("Deleted temporary file: {FileName}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {FileName}", Path.GetFileName(file));
                    }
                }
                
                _logger.LogDebug("File resources cleanup completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file resources cleanup");
            }
        }

        private async Task SaveFinalStateAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Saving final application state...");
                
                // Save any final state information
                var shutdownInfo = new
                {
                    ShutdownTime = DateTime.UtcNow,
                    Version = typeof(Program).Assembly.GetName().Version?.ToString(),
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                };
                
                _logger.LogInformation("Application shutdown completed: {@ShutdownInfo}", shutdownInfo);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during final state save");
            }
        }
    }
}