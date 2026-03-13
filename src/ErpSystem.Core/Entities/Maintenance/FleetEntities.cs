using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Fleet trip request + dispatch lifecycle for vehicle assets.
/// Vehicle records are represented by MaintenanceAsset (AssetCategory.AssetType == "Vehicle").
/// </summary>
public class FleetTrip : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    /// <summary>
    /// User who requested the trip (ApplicationUser.Id).
    /// </summary>
    [Required]
    public Guid RequestedByUserId { get; set; }

    /// <summary>
    /// Optional assigned driver (Employee.Id).
    /// </summary>
    public Guid? DriverEmployeeId { get; set; }

    [MaxLength(200)]
    public string? Purpose { get; set; }

    [MaxLength(200)]
    public string? Origin { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    /// <summary>
    /// Optional predefined destination/template for this trip (route).
    /// </summary>
    public Guid? FleetTripDestinationId { get; set; }

    /// <summary>
    /// Expected trip duration in hours (from predefined destination/template).
    /// </summary>
    public double? ExpectedHours { get; set; }

    /// <summary>
    /// Expected trip mileage in km (from predefined destination/template).
    /// </summary>
    public double? ExpectedMileage { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }

    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    /// <summary>
    /// Fleet lifecycle status. Uses same high-level statuses as other modules:
    /// Draft -> Submitted -> Approved/Rejected -> Dispatched -> Completed/Cancelled.
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FleetTripStatuses.Draft;

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime? DispatchedAt { get; set; }
    public Guid? DispatchedByUserId { get; set; }

    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }

    // Meter readings (odometer and/or hour-meter)
    public double? StartMileage { get; set; }
    public double? EndMileage { get; set; }
    public double? StartOperatingHours { get; set; }
    public double? EndOperatingHours { get; set; }

    // Navigation
    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset? VehicleAsset { get; set; }

    [ForeignKey(nameof(DriverEmployeeId))]
    public virtual Employee? DriverEmployee { get; set; }

    [ForeignKey(nameof(FleetTripDestinationId))]
    public virtual FleetTripDestination? FleetTripDestination { get; set; }
}

public static class FleetTripStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Dispatched = "Dispatched";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public class FleetComplianceItem : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    /// <summary>
    /// Optional link to the template checklist item that created this record.
    /// Helps avoid duplicates when applying templates to a vehicle.
    /// </summary>
    public Guid? TemplateItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ComplianceType { get; set; } = string.Empty; // Registration, Insurance, Permit, Roadworthy, etc.

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Expiry date can be null for template-populated items before the user fills details.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// When true, expiry/near-expiry blocks dispatch (configurable due-soon policy applies).
    /// </summary>
    public bool IsCritical { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? DocumentLinks { get; set; } // JSON array of document URLs/paths

    /// <summary>
    /// Last time a due-soon reminder was sent (UTC).
    /// </summary>
    public DateTime? LastDueSoonReminderSentAt { get; set; }

    /// <summary>
    /// Last time an overdue reminder was sent (UTC).
    /// </summary>
    public DateTime? LastOverdueReminderSentAt { get; set; }

    // Navigation
    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset? VehicleAsset { get; set; }

    [ForeignKey(nameof(TemplateItemId))]
    public virtual FleetComplianceTemplateItem? TemplateItem { get; set; }
}

public class FleetComplianceTemplate : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<FleetComplianceTemplateItem> Items { get; set; } = new List<FleetComplianceTemplateItem>();
}

public class FleetComplianceTemplateItem : TenantEntity
{
    [Required]
    public Guid TemplateId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ComplianceType { get; set; } = string.Empty;

    public bool IsCritical { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    [ForeignKey(nameof(TemplateId))]
    public virtual FleetComplianceTemplate Template { get; set; } = null!;
}

public class FleetVehicleComplianceTemplate : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    public Guid TemplateId { get; set; }

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public Guid? AppliedByUserId { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset? VehicleAsset { get; set; }

    [ForeignKey(nameof(TemplateId))]
    public virtual FleetComplianceTemplate? Template { get; set; }
}

/// <summary>
/// Predefined fleet trip destination/template (route) with expected duration and mileage.
/// </summary>
public class FleetTripDestination : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Origin { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    public double? ExpectedHours { get; set; }
    public double? ExpectedMileage { get; set; }

    public bool IsActive { get; set; } = true;
}

public class FleetFuelTransaction : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }

    [Required]
    public DateTime FuelledAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; }

    [MaxLength(10)]
    public string Unit { get; set; } = "L"; // L, Gal, etc.

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalCost { get; set; }

    public double? MileageAtFuel { get; set; }
    public double? OperatingHoursAtFuel { get; set; }

    [MaxLength(200)]
    public string? VendorName { get; set; }

    [MaxLength(500)]
    public string? ReceiptReference { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset? VehicleAsset { get; set; }

    [ForeignKey(nameof(FleetTripId))]
    public virtual FleetTrip? FleetTrip { get; set; }
}

