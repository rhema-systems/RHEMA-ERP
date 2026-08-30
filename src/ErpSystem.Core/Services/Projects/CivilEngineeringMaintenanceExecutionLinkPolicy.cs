using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

/// <summary>Pure lifecycle rules for the Civil-to-Maintenance reference envelope.</summary>
public static class CivilEngineeringMaintenanceExecutionLinkPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringMaintenanceExecutionLinkRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (request.HandoffId == Guid.Empty) errors.Add("Select an awarded Civil costing handoff.");

        var isCreate = string.Equals(request.LinkMode, CivilEngineeringMaintenanceExecutionLinkModes.CreateJobCard, StringComparison.Ordinal);
        var isLink = string.Equals(request.LinkMode, CivilEngineeringMaintenanceExecutionLinkModes.LinkExisting, StringComparison.Ordinal);
        if (!isCreate && !isLink) errors.Add("Select whether to create a Maintenance job card or link an existing Maintenance record.");

        if (isCreate)
        {
            if (request.MaintenanceTypeId is not { } maintenanceTypeId || maintenanceTypeId == Guid.Empty) errors.Add("Select an active Maintenance type for the new job card.");
            if (request.PriorityLevelId is not { } priorityLevelId || priorityLevelId == Guid.Empty) errors.Add("Select an active Maintenance priority for the new job card.");
            if (request.JobCardId.HasValue || request.WorkOrderId.HasValue) errors.Add("Creating a job card cannot also link an existing job card or work order.");
        }

        if (isLink)
        {
            if (!request.JobCardId.HasValue && !request.WorkOrderId.HasValue) errors.Add("Select an existing Maintenance job card or work order to link.");
            if (request.MaintenanceTypeId.HasValue || request.PriorityLevelId.HasValue) errors.Add("Maintenance type and priority are selected by the existing Maintenance record.");
        }

        return errors;
    }

    public static (string Stage, string Status, string Summary) DeriveOwnerState(string? jobCardStatus, string? jobCardApprovalStatus, Guid? generatedWorkOrderId, string? workOrderStatus)
    {
        if (IsTerminalBlocked(jobCardStatus) || IsTerminalBlocked(jobCardApprovalStatus) || IsTerminalBlocked(workOrderStatus))
            return (CivilEngineeringMaintenanceExecutionLinkStages.Blocked, CivilEngineeringMaintenanceExecutionLinkStatuses.Blocked, "The linked Maintenance lifecycle is rejected, cancelled or otherwise blocked.");
        if (!string.IsNullOrWhiteSpace(workOrderStatus))
        {
            if (IsCompleted(workOrderStatus))
                return (CivilEngineeringMaintenanceExecutionLinkStages.Completed, CivilEngineeringMaintenanceExecutionLinkStatuses.Completed, "The linked Maintenance work order is completed or closed.");
            return (CivilEngineeringMaintenanceExecutionLinkStages.WorkInProgress, CivilEngineeringMaintenanceExecutionLinkStatuses.Active, $"The linked Maintenance work order is {workOrderStatus}.");
        }
        if (generatedWorkOrderId.HasValue)
            return (CivilEngineeringMaintenanceExecutionLinkStages.WorkInProgress, CivilEngineeringMaintenanceExecutionLinkStatuses.Active, "The linked approved job card generated a work order that is pending synchronization.");
        if (string.Equals(jobCardApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(jobCardStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            return (CivilEngineeringMaintenanceExecutionLinkStages.AwaitingWorkOrder, CivilEngineeringMaintenanceExecutionLinkStatuses.Active, "The linked Maintenance job card is approved and awaiting authoritative work-order creation.");
        return (CivilEngineeringMaintenanceExecutionLinkStages.AwaitingJobCardApproval, CivilEngineeringMaintenanceExecutionLinkStatuses.Pending, "The linked Maintenance job card remains in its authoritative approval lifecycle.");
    }

    private static bool IsCompleted(string? value)
        => string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "Closed", StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminalBlocked(string? value)
        => string.Equals(value, "Rejected", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "OnHold", StringComparison.OrdinalIgnoreCase);
}
