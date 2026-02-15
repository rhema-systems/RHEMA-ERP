using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

public class FleetIncident : TenantEntity
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }

    public Guid? DriverEmployeeId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(30)]
    public string IncidentType { get; set; } = "Incident"; // Accident, Incident

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(20)]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Closed

    [MaxLength(4000)]
    public string? DamageAssessment { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedRepairCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualRepairCost { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    // Insurance / claim tracking
    [MaxLength(200)]
    public string? InsuranceCompany { get; set; }

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    [MaxLength(100)]
    public string? ClaimNumber { get; set; }

    [MaxLength(30)]
    public string? ClaimStatus { get; set; } // None, Submitted, Approved, Rejected, Settled

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ClaimAmount { get; set; }

    public DateTime? ClaimSubmittedAtUtc { get; set; }
    public DateTime? ClaimSettledAtUtc { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalData { get; set; }

    [ForeignKey(nameof(VehicleAssetId))]
    public virtual MaintenanceAsset VehicleAsset { get; set; } = null!;

    [ForeignKey(nameof(FleetTripId))]
    public virtual FleetTrip? FleetTrip { get; set; }

    [ForeignKey(nameof(DriverEmployeeId))]
    public virtual Employee? DriverEmployee { get; set; }

    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder? WorkOrder { get; set; }
}

