using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

public class FleetVehicleListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid AssetCategoryId { get; set; }
    public string? LicensePlate { get; set; }
    public string? Vin { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public double? Mileage { get; set; }
    public double? OperatingHours { get; set; }

    public Guid? CurrentDriverEmployeeId { get; set; }
    public string? CurrentDriverEmployeeName { get; set; }
}

public class CreateFleetVehicleDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid AssetCategoryId { get; set; }

    [StringLength(50)]
    public string? LicensePlate { get; set; }

    [StringLength(50)]
    public string? Vin { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? SerialNumber { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    public double? Mileage { get; set; }
    public double? OperatingHours { get; set; }
}

public class UpdateFleetVehicleDto : CreateFleetVehicleDto
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";
}

public class FleetTripDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string? VehicleAssetNumber { get; set; }
    public string? VehicleLicensePlate { get; set; }

    public Guid RequestedByUserId { get; set; }
    public Guid? DriverEmployeeId { get; set; }
    public string? DriverEmployeeName { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? Notes { get; set; }

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? DispatchedAt { get; set; }
    public Guid? DispatchedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }

    public double? StartMileage { get; set; }
    public double? EndMileage { get; set; }
    public double? StartOperatingHours { get; set; }
    public double? EndOperatingHours { get; set; }
}

public class CreateFleetTripDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? DriverEmployeeId { get; set; }

    [StringLength(200)]
    public string? Purpose { get; set; }

    [StringLength(200)]
    public string? Origin { get; set; }

    [StringLength(200)]
    public string? Destination { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
}

public class UpdateFleetTripDto : CreateFleetTripDto
{
}

public class DispatchFleetTripDto
{
    public DateTime? DispatchedAt { get; set; }
    public double? StartMileage { get; set; }
    public double? StartOperatingHours { get; set; }
}

public class CompleteFleetTripDto
{
    public DateTime? CompletedAt { get; set; }
    public double? EndMileage { get; set; }
    public double? EndOperatingHours { get; set; }
    public string? Notes { get; set; }
}

public class FleetComplianceItemDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string ComplianceType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }
    public string? DocumentLinks { get; set; }
    public DateTime? LastDueSoonReminderSentAt { get; set; }
    public DateTime? LastOverdueReminderSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFleetComplianceItemDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    [StringLength(100)]
    public string ComplianceType { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime? IssueDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    public bool IsCritical { get; set; } = true;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public string? DocumentLinks { get; set; }
}

public class UpdateFleetComplianceItemDto : CreateFleetComplianceItemDto
{
}

public class FleetFuelTransactionDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public Guid? FleetTripId { get; set; }
    public DateTime FuelledAt { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? UnitCost { get; set; }
    public decimal? TotalCost { get; set; }
    public double? MileageAtFuel { get; set; }
    public double? OperatingHoursAtFuel { get; set; }
    public string? VendorName { get; set; }
    public string? ReceiptReference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFleetFuelTransactionDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }

    [Required]
    public DateTime FuelledAt { get; set; } = DateTime.UtcNow;

    [Range(0.01, 9999999)]
    public decimal Quantity { get; set; }

    [StringLength(10)]
    public string Unit { get; set; } = "L";

    public decimal? UnitCost { get; set; }
    public double? MileageAtFuel { get; set; }
    public double? OperatingHoursAtFuel { get; set; }

    [StringLength(200)]
    public string? VendorName { get; set; }

    [StringLength(500)]
    public string? ReceiptReference { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class FleetVehicleAssignmentDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime AssignedFromUtc { get; set; }
    public DateTime? AssignedToUtc { get; set; }
    public string AssignmentType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class AssignFleetDriverDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public DateTime? AssignedFromUtc { get; set; }
    public string AssignmentType { get; set; } = "Primary";
    public string? Notes { get; set; }
}

public class EndFleetDriverAssignmentDto
{
    public DateTime? AssignedToUtc { get; set; }
    public string? Notes { get; set; }
}

public class FleetTripInspectionDto
{
    public Guid Id { get; set; }
    public Guid FleetTripId { get; set; }
    public Guid InspectionTemplateId { get; set; }
    public string InspectionTemplateName { get; set; } = string.Empty;
    public Guid? InspectorEmployeeId { get; set; }
    public string? InspectorEmployeeName { get; set; }
    public string InspectionKind { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallResult { get; set; }
    public string InspectionData { get; set; } = "{}";
    public string? Notes { get; set; }
}

public class StartFleetTripInspectionDto
{
    [Required]
    public Guid FleetTripId { get; set; }

    [Required]
    public Guid InspectionTemplateId { get; set; }

    public Guid? InspectorEmployeeId { get; set; }

    [Required]
    public string InspectionKind { get; set; } = "PreTrip";
}

public class CompleteFleetTripInspectionDto
{
    public DateTime? CompletedAtUtc { get; set; }

    [Required]
    public string OverallResult { get; set; } = "Pass";

    public string InspectionData { get; set; } = "{}";
    public string? Notes { get; set; }
}

public class FleetDefectDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public Guid? FleetTripId { get; set; }
    public Guid? FleetTripInspectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReportedAtUtc { get; set; }
    public Guid? ReportedByEmployeeId { get; set; }
    public string? ReportedByEmployeeName { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public class CreateFleetDefectDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? FleetTripInspectionId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string Severity { get; set; } = "Medium";
}

public class UpdateFleetDefectStatusDto
{
    [Required, StringLength(20)]
    public string Status { get; set; } = "Open";

    public string? Notes { get; set; }
}

public class CreateWorkOrderFromFleetDefectDto
{
    [Required]
    public Guid DefectId { get; set; }

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    public string? TitleOverride { get; set; }
    public string? DescriptionOverride { get; set; }
}

public class FleetTyreDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Size { get; set; }
    public string? Position { get; set; }
    public decimal? TreadDepthMm { get; set; }
    public DateTime InstalledAtUtc { get; set; }
    public DateTime? RemovedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class FleetTyreEventDto
{
    public Guid Id { get; set; }
    public Guid FleetTyreId { get; set; }
    public Guid VehicleAssetId { get; set; }
    public DateTime EventAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FromPosition { get; set; }
    public string? ToPosition { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public decimal? TreadDepthMm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public class CreateFleetTyreDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    public string? Brand { get; set; }
    public string? Size { get; set; }
    public string? Position { get; set; }
    public decimal? TreadDepthMm { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    public string Status { get; set; } = "Installed";
    public string? Notes { get; set; }
}

public class FleetBatteryDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Spec { get; set; }
    public string? Position { get; set; }
    public DateTime InstalledAtUtc { get; set; }
    public DateTime? RemovedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class FleetBatteryEventDto
{
    public Guid Id { get; set; }
    public Guid FleetBatteryId { get; set; }
    public Guid VehicleAssetId { get; set; }
    public DateTime EventAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FromPosition { get; set; }
    public string? ToPosition { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public class CreateFleetBatteryDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    public string? Brand { get; set; }
    public string? Spec { get; set; }
    public string? Position { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    public string Status { get; set; } = "Installed";
    public string? Notes { get; set; }
}

public class FleetBatteryKpisDto
{
    public Guid VehicleAssetId { get; set; }
    public int Total { get; set; }
    public int Installed { get; set; }
    public int InStock { get; set; }
    public int Removed { get; set; }
    public int Disposed { get; set; }
    public double? AverageInstalledAgeDays { get; set; }
    public DateTime? LatestInstalledAtUtc { get; set; }
}

public class FleetExternalRepairDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public Guid? VendorBusinessPartnerId { get; set; }
    public string? VendorBusinessPartnerName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public class CreateFleetExternalRepairDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? VendorBusinessPartnerId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public decimal? EstimatedCost { get; set; }
    public string? CurrencyCode { get; set; }
}

public class UpdateFleetExternalRepairStatusDto
{
    [Required, StringLength(20)]
    public string Status { get; set; } = "Requested";
}

public class FleetDashboardSummaryDto
{
    public int ActiveVehicles { get; set; }
    public int TripsThisMonth { get; set; }
    public int OpenDefects { get; set; }
    public int ComplianceDueSoon { get; set; }
    public int ComplianceOverdue { get; set; }
    public decimal FuelCostThisMonth { get; set; }
    public decimal ExternalRepairCostThisMonth { get; set; }
    public decimal? AverageFuelCostPerKm { get; set; }
    public decimal? AverageKmPerLiter { get; set; }
}

public class FleetCostEntryDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public DateTime CostDateUtc { get; set; }
    public string CostType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? FleetExternalRepairId { get; set; }
    public Guid? FleetFuelTransactionId { get; set; }
}

public class CreateFleetCostEntryDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public DateTime? CostDateUtc { get; set; }

    [Required, StringLength(50)]
    public string CostType { get; set; } = "Other";

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string? CurrencyCode { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
