using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Maintenance module settings for tenant-specific configuration.
/// </summary>
public class MaintenanceSettings : TenantEntity
{
    /// <summary>
    /// Number of days before a compliance expiry date that a compliance item is considered "due soon".
    /// </summary>
    [Range(0, 3650)]
    public int FleetComplianceDueSoonDays { get; set; } = 7;

    /// <summary>
    /// When enabled, block fleet trip dispatch if any critical compliance item is due within FleetComplianceDueSoonDays.
    /// </summary>
    public bool BlockFleetDispatchWhenComplianceDueSoon { get; set; } = true;
}

