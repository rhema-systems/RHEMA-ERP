using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for maintenance inventory service - handles parts allocation, consumption, and returns for work orders
/// </summary>
public interface IMaintenanceInventoryService
{
    #region Parts Allocation

    /// <summary>
    /// Allocates parts for all required items in a work order
    /// </summary>
    Task<MaintenancePartsAllocationResult> AllocateWorkOrderPartsAsync(Guid workOrderId, Guid userId);

    #endregion

    #region Parts Consumption

    /// <summary>
    /// Consumes parts when work order tasks are completed
    /// </summary>
    Task<MaintenancePartsConsumptionResult> ConsumeWorkOrderPartsAsync(
        Guid workOrderId, List<PartConsumptionDto> consumptions, Guid userId);

    #endregion

    #region Parts Returns

    /// <summary>
    /// Returns unused parts from a work order
    /// </summary>
    Task<MaintenancePartsReturnResult> ReturnWorkOrderPartsAsync(
        Guid workOrderId, List<PartReturnDto> returns, Guid userId);

    #endregion

    #region Work Order Integration

    /// <summary>
    /// Gets current parts status for a work order
    /// </summary>
    Task<WorkOrderPartsStatusDto> GetWorkOrderPartsStatusAsync(Guid workOrderId);

    /// <summary>
    /// Checks if all required parts are available for allocation
    /// </summary>
    Task<PartsAvailabilityCheckResult> CheckPartsAvailabilityAsync(Guid workOrderId);

    #endregion
}