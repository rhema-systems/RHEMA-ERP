using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Web.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ErpSystem.Web.HealthChecks
{
    public class ApplicationHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApplicationHealthCheck> _logger;
        private readonly ApplicationOptions _applicationOptions;

        public ApplicationHealthCheck(
            IServiceProvider serviceProvider,
            ILogger<ApplicationHealthCheck> logger,
            IOptions<ApplicationOptions> applicationOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _applicationOptions = applicationOptions.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var healthCheckData = new Dictionary<string, object>
            {
                ["application"] = _applicationOptions.ApplicationName,
                ["version"] = _applicationOptions.Version,
                ["environment"] = _applicationOptions.Environment,
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            try
            {
                // Check critical services
                await CheckCriticalServicesAsync(healthCheckData, cancellationToken);

                // Check module status
                await CheckModuleStatusAsync(healthCheckData, cancellationToken);

                // Check configuration validity
                await CheckConfigurationAsync(healthCheckData, cancellationToken);

                return HealthCheckResult.Healthy(
                    $"{_applicationOptions.ApplicationName} v{_applicationOptions.Version} is healthy",
                    healthCheckData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application health check failed");

                healthCheckData["error"] = ex.Message;
                return HealthCheckResult.Unhealthy(
                    $"{_applicationOptions.ApplicationName} health check failed: {ex.Message}",
                    ex,
                    healthCheckData);
            }
        }

        private async Task CheckCriticalServicesAsync(Dictionary<string, object> data, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var serviceStatus = new Dictionary<string, string>();

            // Check TenantService
            try
            {
                var tenantService = scope.ServiceProvider.GetService<ITenantService>();
                if (tenantService != null)
                {
                    var tenants = await tenantService.GetAllTenantsAsync();
                    serviceStatus["TenantService"] = $"Healthy ({tenants.Count()} tenants)";
                }
                else
                {
                    serviceStatus["TenantService"] = "Not registered";
                }
            }
            catch (Exception ex)
            {
                serviceStatus["TenantService"] = $"Error: {ex.Message}";
                throw new InvalidOperationException($"TenantService check failed: {ex.Message}", ex);
            }

            // Check SearchService
            try
            {
                var searchService = scope.ServiceProvider.GetService<Core.Interfaces.ISearchService>();
                if (searchService != null)
                {
                    await searchService.IndexExistsAsync("health-check");
                    serviceStatus["SearchService"] = "Healthy";
                }
                else
                {
                    serviceStatus["SearchService"] = "Not registered";
                }
            }
            catch (Exception ex)
            {
                serviceStatus["SearchService"] = $"Warning: {ex.Message}";
                // Don't fail for search service issues
            }

            // Check DatabaseContext
            try
            {
                var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                if (dbContext != null)
                {
                    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
                    serviceStatus["Database"] = canConnect ? "Healthy" : "Cannot connect";

                    if (!canConnect)
                    {
                        throw new InvalidOperationException("Database connection failed");
                    }
                }
                else
                {
                    serviceStatus["Database"] = "Not registered";
                    throw new InvalidOperationException("Database context not registered");
                }
            }
            catch (Exception ex)
            {
                serviceStatus["Database"] = $"Error: {ex.Message}";
                throw;
            }

            data["services"] = serviceStatus;
        }

        private async Task CheckModuleStatusAsync(Dictionary<string, object> data, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                var moduleOptions = scope.ServiceProvider.GetService<IOptions<ModuleOptions>>()?.Value;
                if (moduleOptions != null)
                {
                    var moduleStatus = new Dictionary<string, object>
                    {
                        ["Finance"] = new { Enabled = moduleOptions.Finance.Enabled, Name = moduleOptions.Finance.DisplayName },
                        ["HR"] = new { Enabled = moduleOptions.HR.Enabled, Name = moduleOptions.HR.DisplayName },
                        ["Sales"] = new { Enabled = moduleOptions.Sales.Enabled, Name = moduleOptions.Sales.DisplayName },
                        ["Procurement"] = new { Enabled = moduleOptions.Procurement.Enabled, Name = moduleOptions.Procurement.DisplayName },
                        ["Inventory"] = new { Enabled = moduleOptions.Inventory.Enabled, Name = moduleOptions.Inventory.DisplayName },
                        ["Marketing"] = new { Enabled = moduleOptions.Marketing.Enabled, Name = moduleOptions.Marketing.DisplayName },
                        ["WorkflowEngine"] = new { Enabled = moduleOptions.WorkflowEngine.Enabled, Name = moduleOptions.WorkflowEngine.DisplayName }
                    };

                    var enabledCount = new[]
                    {
                        moduleOptions.Finance.Enabled,
                        moduleOptions.HR.Enabled,
                        moduleOptions.Sales.Enabled,
                        moduleOptions.Procurement.Enabled,
                        moduleOptions.Inventory.Enabled,
                        moduleOptions.Marketing.Enabled,
                        moduleOptions.WorkflowEngine.Enabled
                    }.Count(x => x);

                    data["modules"] = moduleStatus;
                    data["modules_enabled_count"] = enabledCount;
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                data["modules_error"] = ex.Message;
                throw new InvalidOperationException($"Module status check failed: {ex.Message}", ex);
            }
        }

        private async Task CheckConfigurationAsync(Dictionary<string, object> data, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                var validationService = scope.ServiceProvider.GetService<Configuration.IConfigurationValidationService>();
                if (validationService != null)
                {
                    var validationResult = await validationService.ValidateConfigurationAsync();

                    data["configuration_valid"] = validationResult.IsValid;
                    data["configuration_message"] = validationResult.Message;

                    if (!validationResult.IsValid)
                    {
                        data["configuration_errors"] = validationResult.Errors;
                        throw new InvalidOperationException($"Configuration validation failed: {validationResult.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                data["configuration_error"] = ex.Message;
                // Don't fail the health check for configuration validation issues in production
                if (_applicationOptions.Environment == "Production")
                {
                    _logger.LogWarning(ex, "Configuration validation failed but continuing in production");
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
