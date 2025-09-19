using Microsoft.EntityFrameworkCore;
using ErpSystem.Data;
using ErpSystem.Core.Services;
using ErpSystem.Web.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;

namespace ErpSystem.Web.Services
{
    public interface IApplicationWarmupService
    {
        Task WarmupAsync(CancellationToken cancellationToken = default);
    }

    public class ApplicationWarmupService : IApplicationWarmupService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApplicationWarmupService> _logger;
        private readonly ApplicationOptions _applicationOptions;

        public ApplicationWarmupService(
            IServiceProvider serviceProvider, 
            ILogger<ApplicationWarmupService> logger,
            IOptions<ApplicationOptions> applicationOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _applicationOptions = applicationOptions.Value;
        }

        public async Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Starting application warmup for {ApplicationName} v{Version}", 
                _applicationOptions.ApplicationName, _applicationOptions.Version);

            try
            {
                var tasks = new List<Task>
                {
                    WarmupDatabaseAsync(cancellationToken),
                    WarmupCacheAsync(cancellationToken),
                    WarmupServicesAsync(cancellationToken),
                    PrecompileViewsAsync(cancellationToken),
                    InitializeModulesAsync(cancellationToken)
                };

                await Task.WhenAll(tasks);

                stopwatch.Stop();
                _logger.LogInformation("Application warmup completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Application warmup failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task WarmupDatabaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Ensure database is created and migrations are applied
                await dbContext.Database.MigrateAsync(cancellationToken);

                // Warm up connection pool with a simple query
                var connectionTest = await dbContext.Database.CanConnectAsync(cancellationToken);
                if (!connectionTest)
                {
                    throw new InvalidOperationException("Database connection test failed during warmup");
                }

                // Preload frequently accessed data
                var tenantCount = await dbContext.Tenants.CountAsync(cancellationToken);
                
                _logger.LogDebug("Database warmup completed. Found {TenantCount} tenants", tenantCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database warmup failed");
                throw;
            }
        }

        private async Task WarmupCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                // Test memory cache
                var memoryCache = scope.ServiceProvider.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                if (memoryCache != null)
                {
                    var cacheEntryOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                    };
                    memoryCache.Set("warmup-test", DateTime.UtcNow, cacheEntryOptions);
                    _logger.LogDebug("Memory cache warmed up");
                }

                // Test distributed cache if configured
                var distributedCache = scope.ServiceProvider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                if (distributedCache != null)
                {
                    var cacheOptions = new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                    };
                    await distributedCache.SetStringAsync("warmup-test", DateTime.UtcNow.ToString(), cacheOptions, cancellationToken);
                    await distributedCache.RemoveAsync("warmup-test", cancellationToken);
                    _logger.LogDebug("Distributed cache warmed up");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache warmup encountered issues but continuing");
                // Don't fail the entire warmup for cache issues
            }
        }

        private async Task WarmupServicesAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                // Initialize core services
                var tenantService = scope.ServiceProvider.GetService<ITenantService>();
                if (tenantService != null)
                {
                    var tenants = await tenantService.GetAllTenantsAsync();
                    _logger.LogDebug("Tenant service warmed up with {Count} tenants", tenants.Count());
                }

                var searchService = scope.ServiceProvider.GetService<Core.Interfaces.ISearchService>();
                if (searchService != null)
                {
                    // Initialize search service (placeholder)
                    await searchService.IndexExistsAsync("warmup-test");
                    _logger.LogDebug("Search service warmed up");
                }

                _logger.LogDebug("Services warmup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Services warmup failed");
                throw;
            }
        }

        private async Task PrecompileViewsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // For Blazor Server, we can warm up SignalR hub connections
                using var scope = _serviceProvider.CreateScope();
                
                // This is a placeholder for view/component precompilation
                // In a real scenario, you might want to trigger compilation of critical Razor components
                await Task.Delay(10, cancellationToken); // Simulate work
                
                _logger.LogDebug("Views precompilation completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Views precompilation encountered issues but continuing");
            }
        }

        private async Task InitializeModulesAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var moduleOptions = scope.ServiceProvider.GetService<IOptions<ModuleOptions>>()?.Value;

                if (moduleOptions != null)
                {
                    var enabledModules = new List<string>();
                    
                    if (moduleOptions.Finance.Enabled) enabledModules.Add("Finance");
                    if (moduleOptions.HR.Enabled) enabledModules.Add("HR");
                    if (moduleOptions.Sales.Enabled) enabledModules.Add("Sales");
                    if (moduleOptions.Procurement.Enabled) enabledModules.Add("Procurement");
                    if (moduleOptions.Inventory.Enabled) enabledModules.Add("Inventory");
                    if (moduleOptions.Marketing.Enabled) enabledModules.Add("Marketing");
                    if (moduleOptions.WorkflowEngine.Enabled) enabledModules.Add("WorkflowEngine");

                    _logger.LogInformation("Initialized modules: {Modules}", string.Join(", ", enabledModules));
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Module initialization failed");
                throw;
            }
        }
    }

    public class WarmupHostedService : IHostedService
    {
        private readonly IApplicationWarmupService _warmupService;
        private readonly ILogger<WarmupHostedService> _logger;

        public WarmupHostedService(IApplicationWarmupService warmupService, ILogger<WarmupHostedService> logger)
        {
            _warmupService = warmupService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _warmupService.WarmupAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Application warmup failed - this may impact application performance");
                // Don't prevent startup, but log the issue
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}