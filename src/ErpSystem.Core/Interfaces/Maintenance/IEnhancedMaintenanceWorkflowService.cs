using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Workflow;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for enhanced maintenance workflow service that integrates with the new workflow engine foundation.
/// Provides configurable and extensible workflow execution for maintenance operations.
/// </summary>
public interface IEnhancedMaintenanceWorkflowService
{
    /// <summary>
    /// Creates a work order with configurable workflow based on maintenance type and conditions
    /// </summary>
    /// <param name="createDto">Work order creation data</param>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>Enhanced work order creation result with workflow information</returns>
    Task<EnhancedWorkOrderCreationResult> CreateWorkOrderWithWorkflowAsync(
        CreateWorkOrderDto createDto, string tenantId);

    /// <summary>
    /// Advances work order through workflow steps with enhanced validation and automation
    /// </summary>
    /// <param name="workOrderId">Work order identifier</param>
    /// <param name="stepAction">Action to perform (approve, reject, start, complete, escalate)</param>
    /// <param name="stepData">Optional step execution data</param>
    /// <returns>Enhanced workflow step result</returns>
    Task<EnhancedWorkflowStepResult> AdvanceWorkOrderWorkflowAsync(
        Guid workOrderId, string stepAction, Dictionary<string, object>? stepData = null);

    /// <summary>
    /// Gets enhanced workflow status for a work order including maintenance-specific context
    /// </summary>
    /// <param name="workOrderId">Work order identifier</param>
    /// <returns>Enhanced maintenance workflow status</returns>
    Task<EnhancedMaintenanceWorkflowStatusDto> GetWorkOrderWorkflowStatusAsync(Guid workOrderId);

    /// <summary>
    /// Configures workflow templates for different maintenance scenarios
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>Workflow template configuration result</returns>
    Task<WorkflowTemplateConfigurationResult> ConfigureMaintenanceWorkflowTemplatesAsync(string tenantId);
}
