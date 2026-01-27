using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Tracks asset usage metrics for usage-based maintenance scheduling
/// </summary>
public class AssetUsageTracking : TenantEntity
{
    /// <summary>
    /// Asset being tracked
    /// </summary>
    [Required]
    public Guid AssetId { get; set; }

    /// <summary>
    /// Date and time when usage was recorded
    /// </summary>
    [Required]
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Current odometer/mileage reading
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Mileage { get; set; }

    /// <summary>
    /// Unit for mileage (km, miles, etc.)
    /// </summary>
    [MaxLength(10)]
    public string? MileageUnit { get; set; }

    /// <summary>
    /// Total operating hours
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OperatingHours { get; set; }

    /// <summary>
    /// Number of operational cycles completed
    /// </summary>
    public int? Cycles { get; set; }

    /// <summary>
    /// Fuel consumed (liters or gallons)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FuelConsumed { get; set; }

    /// <summary>
    /// Unit for fuel (liters, gallons, etc.)
    /// </summary>
    [MaxLength(10)]
    public string? FuelUnit { get; set; }

    /// <summary>
    /// Source of the usage data (Manual, Telematics, IoT, API, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DataSource { get; set; } = "Manual";

    /// <summary>
    /// External system reference ID if imported from another system
    /// </summary>
    [MaxLength(100)]
    public string? ExternalReferenceId { get; set; }

    /// <summary>
    /// Additional usage metrics stored as JSON
    /// Example: {"temperature": 75, "pressure": 30, "rpm": 2500}
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalMetrics { get; set; }

    /// <summary>
    /// Notes about this usage reading
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Employee who recorded the usage (if manual entry)
    /// </summary>
    public Guid? RecordedById { get; set; }

    /// <summary>
    /// Flag indicating if this reading triggered a maintenance schedule
    /// </summary>
    public bool TriggeredMaintenance { get; set; }

    /// <summary>
    /// Flag indicating if this is a validated/verified reading
    /// </summary>
    public bool IsValidated { get; set; } = true;

    // Navigation properties
    [ForeignKey(nameof(AssetId))]
    public virtual MaintenanceAsset? Asset { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual ErpSystem.Core.Entities.HR.Employee? RecordedBy { get; set; }
}
