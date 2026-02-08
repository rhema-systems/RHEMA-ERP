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
