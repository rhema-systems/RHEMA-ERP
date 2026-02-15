using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Maintenance;

/// <summary>
/// Background service that periodically evaluates maintenance schedule triggers
/// </summary>
public class MaintenanceTriggerEvaluationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MaintenanceTriggerEvaluationBackgroundService> _logger;
    private readonly TimeSpan _evaluationInterval = TimeSpan.FromMinutes(30); // Run every 30 minutes

    public MaintenanceTriggerEvaluationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MaintenanceTriggerEvaluationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Maintenance Trigger Evaluation Background Service is starting");

        try
        {
            // Wait a bit before starting the first evaluation
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EvaluateTriggersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during trigger evaluation");
                }

                // Wait for the next evaluation cycle
                await Task.Delay(_evaluationInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // This is expected when the application is shutting down
            _logger.LogInformation("Maintenance Trigger Evaluation Background Service is being cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Maintenance Trigger Evaluation Background Service");
            throw;
        }

        _logger.LogInformation("Maintenance Trigger Evaluation Background Service is stopping");
    }

    private async Task EvaluateTriggersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled trigger evaluation for default tenant");

        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:maintenance-trigger-evaluation",
            leaseDuration: TimeSpan.FromMinutes(25),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping maintenance trigger evaluation run (lock not acquired)");
            return;
        }

        var evaluationService = scope.ServiceProvider.GetRequiredService<MaintenanceTriggerEvaluationService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            // Query the default tenant from database
            var defaultTenant = await unitOfWork.Repository<Tenant>()
                .FirstOrDefaultAsync(t => t.Code == "DEFAULT");

            if (defaultTenant == null)
            {
                _logger.LogWarning("Default tenant not found in database");
                return;
            }

            var generatedCount = await evaluationService.EvaluateAllSchedulesAsync(tenantId: defaultTenant.Id);
            _logger.LogInformation("Trigger evaluation completed for default tenant {TenantId}. Generated {Count} work orders", defaultTenant.Id, generatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during trigger evaluation");
        }
    }
}
