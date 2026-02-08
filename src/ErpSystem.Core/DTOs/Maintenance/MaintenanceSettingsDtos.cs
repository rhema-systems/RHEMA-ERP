namespace ErpSystem.Core.DTOs.Maintenance;

public class MaintenanceSettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public int FleetComplianceDueSoonDays { get; set; }
    public bool BlockFleetDispatchWhenComplianceDueSoon { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateMaintenanceSettingsDto
{
    public int FleetComplianceDueSoonDays { get; set; }
    public bool BlockFleetDispatchWhenComplianceDueSoon { get; set; }
}

