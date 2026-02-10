using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Maintenance;

public class FleetVehicleAssignment : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public DateTime AssignedFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedToUtc { get; set; }

    [MaxLength(20)]
    public string AssignmentType { get; set; } = "Primary"; // Primary, Temporary

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

public class FleetTripInspection : TenantEntity
{
    [Required]
    public Guid FleetTripId { get; set; }

    [Required]
    public Guid InspectionTemplateId { get; set; }

    public Guid? InspectorEmployeeId { get; set; }

    [MaxLength(20)]
    public string InspectionKind { get; set; } = "PreTrip"; // PreTrip, PostTrip

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Cancelled

    [MaxLength(20)]
    public string? OverallResult { get; set; } // Pass, Fail, ConditionalPass

    [Column(TypeName = "nvarchar(max)")]
    public string InspectionData { get; set; } = "{}";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FleetTripId))]
    public virtual FleetTrip FleetTrip { get; set; } = null!;

    [ForeignKey(nameof(InspectionTemplateId))]
    public virtual InspectionTemplate InspectionTemplate { get; set; } = null!;

    [ForeignKey(nameof(InspectorEmployeeId))]
    public virtual Employee? InspectorEmployee { get; set; }
}

public class FleetDefect : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? FleetTripInspectionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed

    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ReportedByEmployeeId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalData { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;

    [ForeignKey(nameof(FleetTripId))]
    public virtual FleetTrip? FleetTrip { get; set; }

    [ForeignKey(nameof(FleetTripInspectionId))]
    public virtual FleetTripInspection? FleetTripInspection { get; set; }

    [ForeignKey(nameof(ReportedByEmployeeId))]
    public virtual Employee? ReportedByEmployee { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }
}

public class FleetTyre : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(50)]
    public string? Size { get; set; }

    [MaxLength(30)]
    public string? Position { get; set; } // FrontLeft, FrontRight, RearLeft, RearRight, Spare, etc.

    [Column(TypeName = "decimal(18,4)")]
    public decimal? TreadDepthMm { get; set; }

    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAtUtc { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Installed"; // Installed, InStock, Removed, Disposed

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;
}

public class FleetTyreEvent : TenantEntity
{
    [Required]
    public Guid FleetTyreId { get; set; }

    [Required]
    public Guid VehicleAssetId { get; set; }

    public DateTime EventAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(30)]
    public string EventType { get; set; } = "Updated"; // Installed, Removed, Rotated, StatusChanged, Updated

    [MaxLength(30)]
    public string? FromPosition { get; set; }

    [MaxLength(30)]
    public string? ToPosition { get; set; }

    [MaxLength(20)]
    public string? FromStatus { get; set; }

    [MaxLength(20)]
    public string? ToStatus { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? TreadDepthMm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CostAmount { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FleetTyreId))]
    public virtual FleetTyre FleetTyre { get; set; } = null!;

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;
}

public class FleetBattery : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Brand { get; set; }

    [MaxLength(50)]
    public string? Spec { get; set; } // e.g. 12V 100Ah

    [MaxLength(30)]
    public string? Position { get; set; }

    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAtUtc { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Installed"; // Installed, InStock, Removed, Disposed

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;
}

public class FleetBatteryEvent : TenantEntity
{
    [Required]
    public Guid FleetBatteryId { get; set; }

    [Required]
    public Guid VehicleAssetId { get; set; }

    public DateTime EventAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(30)]
    public string EventType { get; set; } = "Updated"; // Installed, Removed, Moved, StatusChanged, Updated

    [MaxLength(30)]
    public string? FromPosition { get; set; }

    [MaxLength(30)]
    public string? ToPosition { get; set; }

    [MaxLength(20)]
    public string? FromStatus { get; set; }

    [MaxLength(20)]
    public string? ToStatus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CostAmount { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FleetBatteryId))]
    public virtual FleetBattery FleetBattery { get; set; } = null!;

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;
}

public class FleetExternalRepair : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? VendorBusinessPartnerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Requested"; // Requested, Quoted, Approved, InProgress, Completed, Invoiced, Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualCost { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? InvoicedAtUtc { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalData { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;

    [ForeignKey(nameof(VendorBusinessPartnerId))]
    public virtual BusinessPartner? VendorBusinessPartner { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }
}

public class FleetCostEntry : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? FleetExternalRepairId { get; set; }
    public Guid? FleetFuelTransactionId { get; set; }
    public Guid? FleetTyreEventId { get; set; }
    public Guid? FleetBatteryEventId { get; set; }

    public DateTime CostDateUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string CostType { get; set; } = "Fuel"; // Fuel, ExternalRepair, InternalMaintenance, Tyre, Battery, Other

    /// <summary>
    /// Origin of this cost entry. Used for clarity in reporting/UX.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = "Manual"; // Manual, WorkOrderCompletion, FuelTransaction, ExternalRepair, TyreEvent, BatteryEvent

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;

    [ForeignKey(nameof(FleetTripId))]
    public virtual FleetTrip? FleetTrip { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }

    [ForeignKey(nameof(FleetExternalRepairId))]
    public virtual FleetExternalRepair? FleetExternalRepair { get; set; }

    [ForeignKey(nameof(FleetFuelTransactionId))]
    public virtual FleetFuelTransaction? FleetFuelTransaction { get; set; }

    [ForeignKey(nameof(FleetTyreEventId))]
    public virtual FleetTyreEvent? FleetTyreEvent { get; set; }

    [ForeignKey(nameof(FleetBatteryEventId))]
    public virtual FleetBatteryEvent? FleetBatteryEvent { get; set; }
}

