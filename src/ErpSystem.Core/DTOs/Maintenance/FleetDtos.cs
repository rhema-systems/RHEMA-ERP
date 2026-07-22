using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

public class FleetVehicleListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid AssetCategoryId { get; set; }
    public string? AssetType { get; set; }
    public string? LicensePlate { get; set; }
    public string? Vin { get; set; }
    public string? FuelType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public double? Mileage { get; set; }
    public double? OperatingHours { get; set; }
    public string? Location { get; set; }
    public Guid? CurrentProjectId { get; set; }
    public string? CurrentProjectName { get; set; }
    public Guid? CurrentSiteLocationId { get; set; }
    public string? CurrentSiteLocationName { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDue { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceScheduleDueAt { get; set; }

    public Guid? CurrentDriverEmployeeId { get; set; }
    public string? CurrentDriverEmployeeName { get; set; }
}

public class FleetDriverDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string StaffStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public Guid? DriverLicenseId { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public DateOnly? LicenseIssueDate { get; set; }
    public DateOnly? LicenseExpiryDate { get; set; }
    public string? LicenseIssuingAuthority { get; set; }
    public bool IsLicenseVerified { get; set; }
    public DateTime? LicenseVerifiedDate { get; set; }
    public string LicenseStatus { get; set; } = "Missing";
    public int? DaysUntilLicenseExpiry { get; set; }

    public Guid? CurrentAssignmentId { get; set; }
    public Guid? CurrentVehicleAssetId { get; set; }
    public string? CurrentVehicleName { get; set; }
    public string? CurrentVehicleAssetNumber { get; set; }
    public DateTime? AssignedFromUtc { get; set; }
    public bool IsAssigned { get; set; }

    public int TotalTripCount { get; set; }
    public int ActiveTripCount { get; set; }
    public string AvailabilityStatus { get; set; } = "Available";
    public Guid? ActiveTripId { get; set; }
    public string? ActiveTripVehicleName { get; set; }
    public string? ActiveTripVehicleAssetNumber { get; set; }
    public DateTime? ActiveTripStartedAtUtc { get; set; }
    public DateTime? LastTripAtUtc { get; set; }
}

public class FleetDriverSummaryDto
{
    public int TotalDrivers { get; set; }
    public int ValidLicenses { get; set; }
    public int ExpiringLicenses { get; set; }
    public int ExpiredLicenses { get; set; }
    public int MissingLicenses { get; set; }
    public int UnverifiedLicenses { get; set; }
    public int AssignedDrivers { get; set; }
    public int EngagedDrivers { get; set; }
}

public class FleetDriverDirectoryDto : PagedResult<FleetDriverDto>
{
    public FleetDriverSummaryDto Summary { get; set; } = new();
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

    [StringLength(20)]
    public string? FuelType { get; set; }

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
    public string? VehicleFuelType { get; set; }

    public Guid RequestedByUserId { get; set; }
    public Guid? DriverEmployeeId { get; set; }
    public string? DriverEmployeeName { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public Guid? FleetTripDestinationId { get; set; }
    public string? FleetTripDestinationName { get; set; }
    public double? ExpectedHours { get; set; }
    public double? ExpectedMileage { get; set; }
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

    public Guid? FleetTripDestinationId { get; set; }

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
    public Guid? FleetTripDestinationId { get; set; }
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
    public DateTime? ExpiryDate { get; set; }
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

public class FleetComplianceTemplateItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string ComplianceType { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public int SortOrder { get; set; }
}

public class FleetComplianceTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<FleetComplianceTemplateItemDto> Items { get; set; } = new();
}

public class CreateFleetComplianceTemplateItemDto
{
    [Required]
    [StringLength(100)]
    public string ComplianceType { get; set; } = string.Empty;

    public bool IsCritical { get; set; } = true;

    public int SortOrder { get; set; } = 0;
}

public class CreateFleetComplianceTemplateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CreateFleetComplianceTemplateItemDto> Items { get; set; } = new();
}

public class UpdateFleetComplianceTemplateDto : CreateFleetComplianceTemplateDto
{
}

public class FleetVehicleComplianceTemplateDto
{
    public Guid VehicleAssetId { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
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

public class FleetTripDestinationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public double? ExpectedHours { get; set; }
    public double? ExpectedMileage { get; set; }
    public bool IsActive { get; set; }
}

public class CreateFleetTripDestinationDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Origin { get; set; }

    [StringLength(200)]
    public string? Destination { get; set; }

    public double? ExpectedHours { get; set; }
    public double? ExpectedMileage { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateFleetTripDestinationDto : CreateFleetTripDestinationDto
{
}

public class UpdateFleetFuelTransactionDto : CreateFleetFuelTransactionDto
{
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
    public Guid? FleetTripId { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleAssetName { get; set; } = string.Empty;
    public string VehicleAssetNumber { get; set; } = string.Empty;
    public Guid InspectionTemplateId { get; set; }
    public string InspectionTemplateName { get; set; } = string.Empty;
    public string SheetType { get; set; } = "InspectionSheet";
    public Guid? InspectorEmployeeId { get; set; }
    public string? InspectorEmployeeName { get; set; }
    public string InspectionKind { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallResult { get; set; }
    public string InspectionData { get; set; } = "{}";
    public string? Notes { get; set; }
    public string? ClientSubmissionId { get; set; }
    public DateTime? CapturedOfflineAtUtc { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
    public Guid? DefectId { get; set; }
    public Guid? WorkOrderId { get; set; }
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

public class SubmitFleetAssetInspectionDto
{
    [Required]
    public Guid InspectionTemplateId { get; set; }

    public Guid? InspectorEmployeeId { get; set; }

    [Required, StringLength(100)]
    public string ClientSubmissionId { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CapturedOfflineAtUtc { get; set; }

    [StringLength(30)]
    public string InspectionKind { get; set; } = "PreTrip";

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

    /// <summary>
    /// Work order billing type: "Maintenance" (fixed amount) or "Repairs" (itemized costs)
    /// </summary>
    [StringLength(20)]
    public string BillingType { get; set; } = "Repairs";

    public string? TitleOverride { get; set; }
    public string? DescriptionOverride { get; set; }
}

public class FleetIncidentDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;

    public Guid? FleetTripId { get; set; }
    public Guid? DriverEmployeeId { get; set; }
    public string? DriverEmployeeName { get; set; }

    public DateTime OccurredAtUtc { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DamageAssessment { get; set; }

    public decimal? EstimatedRepairCost { get; set; }
    public decimal? ActualRepairCost { get; set; }
    public string? CurrencyCode { get; set; }

    public string? InsuranceCompany { get; set; }
    public string? PolicyNumber { get; set; }
    public string? ClaimNumber { get; set; }
    public string? ClaimStatus { get; set; }
    public decimal? ClaimAmount { get; set; }
    public DateTime? ClaimSubmittedAtUtc { get; set; }
    public DateTime? ClaimSettledAtUtc { get; set; }

    public Guid? WorkOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFleetIncidentDto
{
    [Required]
    public Guid VehicleAssetId { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? DriverEmployeeId { get; set; }

    public DateTime? OccurredAtUtc { get; set; }

    [Required, StringLength(30)]
    public string IncidentType { get; set; } = "Incident";

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [StringLength(20)]
    public string Severity { get; set; } = "Medium";

    [StringLength(20)]
    public string Status { get; set; } = "Open";

    [StringLength(4000)]
    public string? DamageAssessment { get; set; }

    public decimal? EstimatedRepairCost { get; set; }
    public decimal? ActualRepairCost { get; set; }

    [StringLength(10)]
    public string? CurrencyCode { get; set; }

    [StringLength(200)]
    public string? InsuranceCompany { get; set; }

    [StringLength(100)]
    public string? PolicyNumber { get; set; }

    [StringLength(100)]
    public string? ClaimNumber { get; set; }

    [StringLength(30)]
    public string? ClaimStatus { get; set; }

    public decimal? ClaimAmount { get; set; }
    public DateTime? ClaimSubmittedAtUtc { get; set; }
    public DateTime? ClaimSettledAtUtc { get; set; }
}

public class UpdateFleetIncidentDto : CreateFleetIncidentDto
{
}

public class CreateWorkOrderFromFleetIncidentDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    [StringLength(20)]
    public string BillingType { get; set; } = "Repairs";

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
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
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
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
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
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
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
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
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
    public decimal? ActualCost { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? InvoicedAtUtc { get; set; }
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

    public decimal? ActualCost { get; set; }
    public string? CurrencyCode { get; set; }
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
    public decimal InternalMaintenanceCostThisMonth { get; set; }
    public decimal TotalCostThisMonth { get; set; }
    public decimal? AverageFuelCostPerKm { get; set; }
    public decimal? AverageKmPerLiter { get; set; }
}

public class FleetInspectionOperationsDashboardDto
{
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public int TotalInspections { get; set; }
    public int SyncedInspections { get; set; }
    public int OfflineCapturedInspections { get; set; }
    public int PassedInspections { get; set; }
    public int FailedInspections { get; set; }
    public int FlaggedInspections { get; set; }
    public int DefectsCreated { get; set; }
    public int WorkOrdersCreated { get; set; }
    public int OpenFollowUpWorkOrders { get; set; }
    public int QrEnabledTemplates { get; set; }
    public int StaleQrTemplates { get; set; }
    public decimal SyncRate { get; set; }
    public decimal FailureRate { get; set; }
    public decimal WorkOrderFollowUpRate { get; set; }
    public List<FleetInspectionSheetBreakdownDto> SheetBreakdown { get; set; } = new();
    public List<FleetInspectionRecentIssueDto> RecentIssues { get; set; } = new();
}

public class FleetInspectionSheetBreakdownDto
{
    public string SheetType { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Failed { get; set; }
    public int Flagged { get; set; }
    public int OfflineCaptured { get; set; }
}

public class FleetInspectionRecentIssueDto
{
    public Guid InspectionId { get; set; }
    public Guid? DefectId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string SheetType { get; set; } = string.Empty;
    public string OverallResult { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string DefectStatus { get; set; } = string.Empty;
    public string? WorkOrderStatus { get; set; }
    public DateTime ReportedAtUtc { get; set; }
}

public class FleetCostEntryDto
{
    public Guid Id { get; set; }
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public DateTime CostDateUtc { get; set; }
    public string CostType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }

    public Guid? FleetTripId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? FleetExternalRepairId { get; set; }
    public Guid? FleetFuelTransactionId { get; set; }
    public Guid? FleetIncidentId { get; set; }
}

public class CreateFleetCostEntryDto
{
    /// <summary>
    /// Preferred linkage: costs should be attributed to a specific trip.
    /// When provided, VehicleAssetId will be derived from the trip.
    /// </summary>
    public Guid? FleetTripId { get; set; }

    /// <summary>
    /// Legacy/optional linkage. If FleetTripId is not provided, VehicleAssetId must be provided.
    /// </summary>
    public Guid? VehicleAssetId { get; set; }

    public DateTime? CostDateUtc { get; set; }

    [Required, StringLength(50)]
    public string CostType { get; set; } = "Other";

    /// <summary>
    /// Optional origin label (e.g. TripExpense). Defaults to Manual.
    /// </summary>
    [StringLength(50)]
    public string? Source { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string? CurrencyCode { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
