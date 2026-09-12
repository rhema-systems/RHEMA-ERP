using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — PERMIT-TO-WORK, PPE & SAFETY EQUIPMENT DTOs
// Domains: E. Permit-to-Work   F. PPE Management   G. Safety Equipment
// ============================================================================

// ============================================================================
// E. PERMIT-TO-WORK
// ============================================================================

#region Permit-to-Work

public class ShePermitToWorkDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PermitNumber { get; set; } = string.Empty;
    public ShePermitType PermitType { get; set; }
    public string PermitTypeName => PermitType.ToString();
    public string WorkDescription { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }

    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public DateTime RequestedDate { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public TimeSpan PlannedStartTime { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public TimeSpan PlannedEndTime { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public ShePermitStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? IssuedById { get; set; }
    public string? IssuedByName { get; set; }
    public DateTime? IssuedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public string? HazardsIdentified { get; set; }
    public string? ControlMeasures { get; set; }
    public string? PpeRequired { get; set; }
    public string? GasTestResults { get; set; }
    public string? IsolationDetails { get; set; }
    public Guid? RiskAssessmentId { get; set; }
    public string? RiskAssessmentNumber { get; set; }

    public bool IsSuspended { get; set; }
    public DateTime? SuspendedDate { get; set; }
    public string? SuspensionReason { get; set; }
    public Guid? SuspendedById { get; set; }
    public string? SuspendedByName { get; set; }

    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosureNotes { get; set; }
    public bool WorkCompletedSatisfactorily { get; set; }
    public bool AreaLeftSafe { get; set; }
    public string? ReinstatementNotes { get; set; }

    public List<ShePermitToWorkWorkerDto> AuthorisedWorkers { get; set; } = new();
    public List<ShePermitToWorkExtensionDto> Extensions { get; set; } = new();
    public List<ShePermitToWorkDocumentDto> Documents { get; set; } = new();
}

public class ShePermitToWorkSummaryDto
{
    public Guid Id { get; set; }
    public string PermitNumber { get; set; } = string.Empty;
    public ShePermitType PermitType { get; set; }
    public string PermitTypeName => PermitType.ToString();
    public string WorkDescription { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string? ContractorName { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public ShePermitStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsSuspended { get; set; }
}

public class CreateShePermitToWorkDto : CreateDtoBase
{
    [Required]
    public ShePermitType PermitType { get; set; }

    [Required, MaxLength(300)]
    public string WorkDescription { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required]
    public Guid RequestedById { get; set; }

    public Guid? ContractorId { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime PlannedStartDate { get; set; }
    public TimeSpan PlannedStartTime { get; set; }

    [Required]
    public DateTime PlannedEndDate { get; set; }
    public TimeSpan PlannedEndTime { get; set; }

    [MaxLength(2000)]
    public string? HazardsIdentified { get; set; }

    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    [MaxLength(1000)]
    public string? PpeRequired { get; set; }

    [MaxLength(500)]
    public string? GasTestResults { get; set; }

    [MaxLength(500)]
    public string? IsolationDetails { get; set; }

    public Guid? RiskAssessmentId { get; set; }
}

public class UpdateShePermitToWorkDto : UpdateDtoBase
{
    [Required]
    public ShePermitType PermitType { get; set; }

    [Required, MaxLength(300)]
    public string WorkDescription { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? ContractorId { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }
    public TimeSpan PlannedStartTime { get; set; }

    [Required]
    public DateTime PlannedEndDate { get; set; }
    public TimeSpan PlannedEndTime { get; set; }

    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [MaxLength(2000)]
    public string? HazardsIdentified { get; set; }

    [MaxLength(2000)]
    public string? ControlMeasures { get; set; }

    [MaxLength(1000)]
    public string? PpeRequired { get; set; }

    [MaxLength(500)]
    public string? GasTestResults { get; set; }

    [MaxLength(500)]
    public string? IsolationDetails { get; set; }

    public Guid? RiskAssessmentId { get; set; }
}

/// <summary>Issues/approves a permit (PendingApproval → Approved/Active).</summary>
public class ApproveShePermitToWorkDto
{
    [Required]
    public Guid PermitId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovedDate { get; set; } = DateTime.UtcNow;
    public Guid? IssuedById { get; set; }
    public DateTime? IssuedDate { get; set; }
}

/// <summary>Suspends an active permit.</summary>
public class SuspendShePermitToWorkDto
{
    [Required]
    public Guid PermitId { get; set; }

    [Required]
    public Guid SuspendedById { get; set; }

    public DateTime SuspendedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(500)]
    public string SuspensionReason { get; set; } = string.Empty;
}

/// <summary>Closes / reinstates the work area after a permit.</summary>
public class CloseShePermitToWorkDto
{
    [Required]
    public Guid PermitId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;
    public bool WorkCompletedSatisfactorily { get; set; }
    public bool AreaLeftSafe { get; set; }

    [MaxLength(500)]
    public string? ReinstatementNotes { get; set; }

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

public class ShePermitToWorkWorkerDto : BaseDto
{
    public Guid PermitToWorkId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? TradeOrRole { get; set; }
    public bool Briefed { get; set; }
    public DateTime? BriefedDate { get; set; }
    public bool SignedOff { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class CreateShePermitToWorkWorkerDto : CreateDtoBase
{
    [Required]
    public Guid PermitToWorkId { get; set; }

    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? TradeOrRole { get; set; }

    public bool Briefed { get; set; }
    public DateTime? BriefedDate { get; set; }
    public bool SignedOff { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class UpdateShePermitToWorkWorkerDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string WorkerName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(100)]
    public string? TradeOrRole { get; set; }

    public bool Briefed { get; set; }
    public DateTime? BriefedDate { get; set; }
    public bool SignedOff { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class ShePermitToWorkExtensionDto : BaseDto
{
    public Guid PermitToWorkId { get; set; }
    public int ExtensionNumber { get; set; }
    public DateTime NewEndDate { get; set; }
    public TimeSpan NewEndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ApprovedById { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime ApprovedDate { get; set; }
}

public class CreateShePermitToWorkExtensionDto : CreateDtoBase
{
    [Required]
    public Guid PermitToWorkId { get; set; }

    public int ExtensionNumber { get; set; }

    [Required]
    public DateTime NewEndDate { get; set; }
    public TimeSpan NewEndTime { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovedDate { get; set; } = DateTime.UtcNow;
}

public class ShePermitToWorkDocumentDto : BaseDto
{
    public Guid PermitToWorkId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateShePermitToWorkDocumentDto : CreateDtoBase
{
    [Required]
    public Guid PermitToWorkId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    [Required]
    public Guid UploadedById { get; set; }
}

#endregion

// ============================================================================
// F. PPE MANAGEMENT
// ============================================================================

#region PPE Type

public class PpeTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ShePpeCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? Standard { get; set; }
    public int? LifespanMonths { get; set; }
    public bool RequiresSerialNumber { get; set; }
    public bool HasExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePpeTypeDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public ShePpeCategory Category { get; set; }

    [MaxLength(100)]
    public string? Standard { get; set; }

    public int? LifespanMonths { get; set; }
    public bool RequiresSerialNumber { get; set; }
    public bool HasExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdatePpeTypeDto : UpdateDtoBase
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public ShePpeCategory Category { get; set; }

    [MaxLength(100)]
    public string? Standard { get; set; }

    public int? LifespanMonths { get; set; }
    public bool RequiresSerialNumber { get; set; }
    public bool HasExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region PPE Inventory

public class PpeInventoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PpeTypeId { get; set; }
    public string PpeTypeName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Size { get; set; }
    public int QuantityInStock { get; set; }
    public int MinimumStockLevel { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsBelowReorderLevel => QuantityInStock <= ReorderLevel;
    public string? StorageLocation { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Supplier { get; set; }
    public DateTime? LastRestockDate { get; set; }
    public Guid? LastRestockedById { get; set; }
    public string? LastRestockedByName { get; set; }
}

public class CreatePpeInventoryDto : CreateDtoBase
{
    [Required]
    public Guid PpeTypeId { get; set; }

    [Required, MaxLength(50)]
    public string ItemCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Size { get; set; }

    public int QuantityInStock { get; set; }
    public int MinimumStockLevel { get; set; }
    public int ReorderLevel { get; set; }

    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    public decimal? UnitCost { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }
}

public class UpdatePpeInventoryDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Size { get; set; }

    public int QuantityInStock { get; set; }
    public int MinimumStockLevel { get; set; }
    public int ReorderLevel { get; set; }

    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    public decimal? UnitCost { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }
}

/// <summary>Records a restock of an inventory item.</summary>
public class RestockPpeInventoryDto
{
    [Required]
    public Guid PpeInventoryId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public Guid RestockedById { get; set; }

    public DateTime RestockDate { get; set; } = DateTime.UtcNow;
}

#endregion

#region PPE Issuance

public class PpeIssuanceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid PpeTypeId { get; set; }
    public string PpeTypeName { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public ShePpeCondition? ConditionWhenIssued { get; set; }
    public string? ConditionWhenIssuedName => ConditionWhenIssued?.ToString();
    public ShePpeCondition? ConditionWhenReturned { get; set; }
    public string? ConditionWhenReturnedName => ConditionWhenReturned?.ToString();
    public Guid IssuedById { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public bool IsReturned { get; set; }
    public Guid? ReturnedToId { get; set; }
    public string? ReturnedToName { get; set; }
    public string? Notes { get; set; }
}

public class CreatePpeIssuanceDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid PpeTypeId { get; set; }

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [MaxLength(20)]
    public string? Size { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public ShePpeCondition? ConditionWhenIssued { get; set; }

    [Required]
    public Guid IssuedById { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>Records the return of issued PPE.</summary>
public class ReturnPpeIssuanceDto
{
    [Required]
    public Guid IssuanceId { get; set; }

    public DateTime ActualReturnDate { get; set; } = DateTime.UtcNow;
    public ShePpeCondition? ConditionWhenReturned { get; set; }

    [Required]
    public Guid ReturnedToId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Job Role PPE Requirement

public class JobRolePpeRequirementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string JobRoleCode { get; set; } = string.Empty;
    public string JobRoleName { get; set; } = string.Empty;
    public Guid PpeTypeId { get; set; }
    public string PpeTypeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReplacementFrequencyMonths { get; set; }
    public bool IsMandatory { get; set; }
}

public class CreateJobRolePpeRequirementDto : CreateDtoBase
{
    [Required, MaxLength(50)]
    public string JobRoleCode { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string JobRoleName { get; set; } = string.Empty;

    [Required]
    public Guid PpeTypeId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public int ReplacementFrequencyMonths { get; set; }
    public bool IsMandatory { get; set; } = true;
}

public class UpdateJobRolePpeRequirementDto : UpdateDtoBase
{
    [Required, MaxLength(150)]
    public string JobRoleName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    public int ReplacementFrequencyMonths { get; set; }
    public bool IsMandatory { get; set; } = true;
}

#endregion

// ============================================================================
// G. SAFETY EQUIPMENT
// ============================================================================

#region Safety Equipment

public class SafetyEquipmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string EquipmentNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheSafetyEquipmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? SpecificArea { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstallationDate { get; set; }
    public bool RequiresRegularInspection { get; set; }
    public int InspectionFrequencyDays { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
    public bool RequiresCertification { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }
    public string? CertificationDocumentPath { get; set; }
    public SheSafetyEquipmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? OutOfServiceDate { get; set; }
    public string? OutOfServiceReason { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDueDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public string? Notes { get; set; }

    public List<SafetyEquipmentInspectionDto> Inspections { get; set; } = new();
    public List<SafetyEquipmentMaintenanceDto> MaintenanceRecords { get; set; } = new();
}

public class SafetyEquipmentSummaryDto
{
    public Guid Id { get; set; }
    public string EquipmentNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SheSafetyEquipmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string LocationName { get; set; } = string.Empty;
    public SheSafetyEquipmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? NextInspectionDueDate { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }
}

public class CreateSafetyEquipmentDto : CreateDtoBase
{
    /// <summary>Ignored — the equipment number is assigned server-side (SEQ-YYYY-NNNN).</summary>
    [MaxLength(30)]
    public string EquipmentNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheSafetyEquipmentType Type { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstallationDate { get; set; }
    public bool RequiresRegularInspection { get; set; }
    public int InspectionFrequencyDays { get; set; }
    public bool RequiresCertification { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateSafetyEquipmentDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheSafetyEquipmentType Type { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstallationDate { get; set; }
    public bool RequiresRegularInspection { get; set; }
    public int InspectionFrequencyDays { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDueDate { get; set; }
    public bool RequiresCertification { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }

    [MaxLength(500)]
    public string? CertificationDocumentPath { get; set; }

    [Required]
    public SheSafetyEquipmentStatus Status { get; set; }

    public DateTime? OutOfServiceDate { get; set; }

    [MaxLength(500)]
    public string? OutOfServiceReason { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDueDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class SafetyEquipmentInspectionDto : BaseDto
{
    public Guid EquipmentId { get; set; }
    public DateTime InspectionDate { get; set; }
    public SheInspectionType InspectionType { get; set; }
    public string InspectionTypeName => InspectionType.ToString();
    public Guid InspectedById { get; set; }
    public string InspectedByName { get; set; } = string.Empty;
    public SheInspectionResult Result { get; set; }
    public string ResultName => Result.ToString();
    public string? Findings { get; set; }
    public string? DeficienciesNoted { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public List<SafetyEquipmentInspectionActionDto> InspectionActions { get; set; } = new();
}

public class CreateSafetyEquipmentInspectionDto : CreateDtoBase
{
    [Required]
    public Guid EquipmentId { get; set; }

    [Required]
    public DateTime InspectionDate { get; set; }

    [Required]
    public SheInspectionType InspectionType { get; set; }

    [Required]
    public Guid InspectedById { get; set; }

    [Required]
    public SheInspectionResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? DeficienciesNoted { get; set; }

    public DateTime? NextInspectionDate { get; set; }
}

public class UpdateSafetyEquipmentInspectionDto : UpdateDtoBase
{
    [Required]
    public DateTime InspectionDate { get; set; }

    [Required]
    public SheInspectionType InspectionType { get; set; }

    [Required]
    public SheInspectionResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(2000)]
    public string? DeficienciesNoted { get; set; }

    public DateTime? NextInspectionDate { get; set; }
}

public class SafetyEquipmentInspectionActionDto : BaseDto
{
    public Guid SafetyEquipmentInspectionId { get; set; }
    public Guid CorrectiveActionTemplateId { get; set; }
    public string CorrectiveActionTemplateTitle { get; set; } = string.Empty;
    public SheCorrectiveActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
}

public class CreateSafetyEquipmentInspectionActionDto : CreateDtoBase
{
    [Required]
    public Guid SafetyEquipmentInspectionId { get; set; }

    [Required]
    public Guid CorrectiveActionTemplateId { get; set; }

    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    public DateTime? DueDate { get; set; }
    public Guid? AssignedToId { get; set; }
}

public class UpdateSafetyEquipmentInspectionActionDto : UpdateDtoBase
{
    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    public Guid? AssignedToId { get; set; }
}

public class SafetyEquipmentMaintenanceDto : BaseDto
{
    public Guid EquipmentId { get; set; }
    public Guid? MaintenanceRecordId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public string? MaintenanceType { get; set; }
    public string? Description { get; set; }
    public bool EquipmentTakenOutOfService { get; set; }
    public DateTime? OutOfServiceStart { get; set; }
    public DateTime? OutOfServiceEnd { get; set; }
    public decimal? Cost { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
}

public class CreateSafetyEquipmentMaintenanceDto : CreateDtoBase
{
    [Required]
    public Guid EquipmentId { get; set; }

    public Guid? MaintenanceRecordId { get; set; }

    [Required]
    public DateTime MaintenanceDate { get; set; }

    [MaxLength(100)]
    public string? MaintenanceType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool EquipmentTakenOutOfService { get; set; }
    public DateTime? OutOfServiceStart { get; set; }
    public DateTime? OutOfServiceEnd { get; set; }
    public decimal? Cost { get; set; }

    [MaxLength(200)]
    public string? PerformedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateSafetyEquipmentMaintenanceDto : UpdateDtoBase
{
    [Required]
    public DateTime MaintenanceDate { get; set; }

    [MaxLength(100)]
    public string? MaintenanceType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool EquipmentTakenOutOfService { get; set; }
    public DateTime? OutOfServiceStart { get; set; }
    public DateTime? OutOfServiceEnd { get; set; }
    public decimal? Cost { get; set; }

    [MaxLength(200)]
    public string? PerformedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion
