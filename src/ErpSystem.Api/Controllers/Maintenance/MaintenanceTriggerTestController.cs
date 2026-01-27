using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

/// <summary>
/// Test controller for manually triggering maintenance schedule evaluation
/// </summary>
[ApiController]
[Route("api/maintenance/trigger-test")]
[Authorize]
public class MaintenanceTriggerTestController : ControllerBase
{
    private readonly MaintenanceTriggerEvaluationService _evaluationService;
    private readonly ILogger<MaintenanceTriggerTestController> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MaintenanceTriggerTestController(
        MaintenanceTriggerEvaluationService evaluationService,
        ILogger<MaintenanceTriggerTestController> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _evaluationService = evaluationService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    /// <summary>
    /// Manually trigger evaluation of all maintenance schedules (for testing)
    /// </summary>
    /// <param name="tenantId">Optional tenant ID. If not provided, uses current user's tenant</param>
    /// <returns>Number of work orders generated</returns>
    [HttpPost("evaluate-all")]
    public async Task<IActionResult> EvaluateAllSchedules([FromQuery] Guid? tenantId = null)
    {
        try
        {
            // Use provided tenantId or current user's tenant
            var targetTenantId = tenantId ?? _currentUserProvider.TenantId;

            _logger.LogInformation("Manual trigger evaluation requested for tenant {TenantId}", targetTenantId);
            var generatedCount = await _evaluationService.EvaluateAllSchedulesAsync(targetTenantId);

            return Ok(new
            {
                message = $"Evaluation completed for tenant {targetTenantId}. Generated {generatedCount} work orders.",
                workOrdersGenerated = generatedCount,
                tenantId = targetTenantId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual trigger evaluation");
            return StatusCode(500, new { error = "Failed to evaluate schedules", details = ex.Message });
        }
    }

    /// <summary>
    /// Get information about the background service status
    /// </summary>
    [HttpGet("service-info")]
    public IActionResult GetServiceInfo()
    {
        return Ok(new
        {
            backgroundServiceRunning = true,
            evaluationInterval = "30 minutes",
            initialDelay = "5 minutes after startup",
            message = "Background service evaluates all active schedules automatically every 30 minutes",
            manualTrigger = "Use POST /api/maintenance/trigger-test/evaluate-all to trigger immediately"
        });
    }
}
