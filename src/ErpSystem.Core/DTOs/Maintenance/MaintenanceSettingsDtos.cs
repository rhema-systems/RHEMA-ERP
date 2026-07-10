namespace ErpSystem.Core.DTOs.Maintenance;

public class MaintenanceSettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public int FleetComplianceDueSoonDays { get; set; }
    public bool BlockFleetDispatchWhenComplianceDueSoon { get; set; }
    public bool RequirePredefinedFleetTripDestinationOnDispatch { get; set; }
    public Guid? DefaultFleetDefectWorkOrderTypeId { get; set; }
    public Guid? DefaultFleetDefectMaintenanceTypeId { get; set; }
    public Guid? DefaultFleetDefectPriorityLevelId { get; set; }
    public string DefaultFleetDefectBillingType { get; set; } = "Repairs";

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateMaintenanceSettingsDto
{
    public int FleetComplianceDueSoonDays { get; set; }
    public bool BlockFleetDispatchWhenComplianceDueSoon { get; set; }
    public bool RequirePredefinedFleetTripDestinationOnDispatch { get; set; }
    public Guid? DefaultFleetDefectWorkOrderTypeId { get; set; }
    public Guid? DefaultFleetDefectMaintenanceTypeId { get; set; }
    public Guid? DefaultFleetDefectPriorityLevelId { get; set; }
    public string DefaultFleetDefectBillingType { get; set; } = "Repairs";
}
