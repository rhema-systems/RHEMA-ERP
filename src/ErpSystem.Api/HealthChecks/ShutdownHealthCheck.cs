using ErpSystem.Web.Middleware;
using ErpSystem.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErpSystem.Web.HealthChecks
{
    public class ShutdownHealthCheck : IHealthCheck
    {
        private readonly IGracefulShutdownService _shutdownService;
        private readonly ILogger<ShutdownHealthCheck> _logger;

        public ShutdownHealthCheck(
            IGracefulShutdownService shutdownService,
            ILogger<ShutdownHealthCheck> logger)
        {
            _shutdownService = shutdownService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if shutdown is complete
                try
                {
                    var shutdownComplete = await _shutdownService.IsShutdownCompleteAsync();
                    if (shutdownComplete)
                    {
                        var data = new Dictionary<string, object>
                        {
                            ["status"] = "shutdown_complete",
                            ["message"] = "Application shutdown completed",
                            ["timestamp"] = DateTime.UtcNow
                        };

                        return HealthCheckResult.Unhealthy(
                            "Application shutdown is complete",
                            data: data);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // Shutdown service might not be initialized yet or task not completed
                    _logger.LogDebug("Unable to check shutdown completion status: {Error}", ex.Message);
                }

                // Application is running normally
                var healthyData = new Dictionary<string, object>
                {
                    ["status"] = "running",
                    ["message"] = "Application is running normally",
                    ["timestamp"] = DateTime.UtcNow
                };

                return HealthCheckResult.Healthy(
                    "Application is running normally",
                    data: healthyData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking shutdown health status");
                
                var errorData = new Dictionary<string, object>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                };

                return HealthCheckResult.Unhealthy(
                    "Error checking shutdown status",
                    ex,
                    data: errorData);
            }
        }
    }

    public static class ShutdownHealthCheckExtensions
    {
        public static IServiceCollection AddShutdownHealthCheck(this IServiceCollection services)
        {
            services.AddTransient<ShutdownHealthCheck>();
            return services;
        }

        public static IHealthChecksBuilder AddShutdownHealthCheck(this IHealthChecksBuilder builder)
        {
            return builder.AddCheck<ShutdownHealthCheck>(
                "shutdown",
                tags: new[] { "shutdown", "lifecycle", "ready", "live" });
        }
    }
}