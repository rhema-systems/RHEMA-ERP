using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Asset Type DTOs

/// <summary>
/// DTO for asset type read operations
/// </summary>
public class AssetTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasExtraAttributes { get; set; }
    public int AttributeCount { get; set; }
}

/// <summary>
/// Summary DTO for asset type list views
/// </summary>
public class AssetTypeSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasExtraAttributes { get; set; }
    public int AssetCount { get; set; }
    public int AttributeCount { get; set; }
}

/// <summary>
/// Detailed DTO for asset type with attributes
/// </summary>
public class AssetTypeDetailDto : AssetTypeDto
{
    public List<AssetTypeAttributeDto> Attributes { get; set; } = new();
}

/// <summary>
/// DTO for creating an asset type
/// </summary>
public class CreateAssetTypeDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool HasExtraAttributes { get; set; }

    public List<CreateAssetTypeAttributeDto>? Attributes { get; set; }
}

/// <summary>
/// DTO for updating an asset type
/// </summary>
public class UpdateAssetTypeDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool HasExtraAttributes { get; set; }
}

#endregion

#region Asset Type Attribute DTOs

/// <summary>
/// DTO for asset type attribute read operations
/// </summary>
public class AssetTypeAttributeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public AssetAttributeDataType DataType { get; set; }
    public string DataTypeName => DataType.ToString();
    public bool IsRequired { get; set; }
    public bool IsExpiryDate { get; set; }
    public string AttributeOptions { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating an asset type attribute
/// </summary>
public class CreateAssetTypeAttributeDto : CreateDtoBase
{
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(70)]
    public string AttributeName { get; set; } = string.Empty;

    public AssetAttributeDataType DataType { get; set; } = AssetAttributeDataType.Text;

    public bool IsRequired { get; set; }

    public bool IsExpiryDate { get; set; }

    [MaxLength(2000)]
    public string AttributeOptions { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating an asset type attribute
/// </summary>
public class UpdateAssetTypeAttributeDto : UpdateDtoBase
{
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(70)]
    public string AttributeName { get; set; } = string.Empty;

    public AssetAttributeDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    public bool IsExpiryDate { get; set; }

    [MaxLength(2000)]
    public string AttributeOptions { get; set; } = string.Empty;
}

#endregion

#region Company Asset DTOs

/// <summary>
/// DTO for company asset read operations
/// </summary>
public class CompanyAssetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;
    
    // Identification
    public string? Manufacturer { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    
    // Purchase
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? Supplier { get; set; }
    public string? InvoiceNumber { get; set; }
    
    // Warranty
    public bool HasWarranty { get; set; }
    public DateOnly? WarrantyStartDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public string? WarrantyProvider { get; set; }
    public bool IsWarrantyActive => HasWarranty && WarrantyEndDate.HasValue && WarrantyEndDate.Value >= DateOnly.FromDateTime(DateTime.UtcNow);
    
    // Physical Details
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Specifications { get; set; }
    
    // Status
    public CompanyAssetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public HRAssetCondition Condition { get; set; }
    public string ConditionName => Condition.ToString();
    
    // Location
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationDetails { get; set; }
    
    // Assignment
    public bool IsAssignable { get; set; }
    public bool IsCurrentlyAssigned { get; set; }
    public Guid? CurrentAssignedToId { get; set; }
    public string? CurrentAssignedToName { get; set; }
    
    // Maintenance
    public bool RequiresRegularMaintenance { get; set; }
    public int? MaintenanceIntervalDays { get; set; }
    public DateOnly? LastMaintenanceDate { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }
    
    // Insurance
    public bool IsInsured { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public decimal? InsuredValue { get; set; }
    
    // Disposal
    public DateOnly? DisposalDate { get; set; }
    public DisposalMethod? DisposalMethod { get; set; }
    public string? DisposalNotes { get; set; }
}

/// <summary>
/// Summary DTO for company asset list views
/// </summary>
public class CompanyAssetSummaryDto
{
    public Guid Id { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetTypeName { get; set; } = string.Empty;
    public CompanyAssetStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public HRAssetCondition Condition { get; set; }
    public string ConditionName => Condition.ToString();
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? UnitId { get; set; }
    public string? UnitName { get; set; }
    public bool IsCurrentlyAssigned { get; set; }
    public string? CurrentAssignedToName { get; set; }
    public decimal? CurrentValue { get; set; }
}

/// <summary>
/// Detailed DTO for company asset with related data
/// </summary>
public class CompanyAssetDetailDto : CompanyAssetDto
{
    public List<AssetAttributeValueDto> AttributeValues { get; set; } = new();
    public List<AssetAssignmentSummaryDto> RecentAssignments { get; set; } = new();
    public List<AssetMaintenanceSummaryDto> RecentMaintenance { get; set; } = new();
    public List<AssetAttachmentDto> Attachments { get; set; } = new();
}

/// <summary>
/// DTO for creating a company asset
/// </summary>
public class CreateCompanyAssetDto : CreateDtoBase
{
    [MaxLength(70)]
    public string AssetNumber { get; set; } = string.Empty;

    [MaxLength(70)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AssetName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetTypeId { get; set; }

    // Identification
    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(70)]
    public string? ModelNumber { get; set; }

    [MaxLength(70)]
    public string? SerialNumber { get; set; }

    // Purchase
    public DateOnly? PurchaseDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PurchaseCost { get; set; }

    [MaxLength(100)]
    public string? Supplier { get; set; }

    [MaxLength(70)]
    public string? InvoiceNumber { get; set; }

    // Warranty
    public bool HasWarranty { get; set; }
    public DateOnly? WarrantyStartDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }

    [MaxLength(100)]
    public string? WarrantyProvider { get; set; }

    // Physical Details
    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(30)]
    public string? Size { get; set; }

    [MaxLength(300)]
    public string? Specifications { get; set; }

    // Status
    public CompanyAssetStatus Status { get; set; } = CompanyAssetStatus.Available;
    public HRAssetCondition Condition { get; set; } = HRAssetCondition.Good;

    // Location
    public Guid? LocationId { get; set; }

    [MaxLength(500)]
    public string? LocationDetails { get; set; }

    // Assignment
    public bool IsAssignable { get; set; }

    // Maintenance
    public bool RequiresRegularMaintenance { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaintenanceIntervalDays { get; set; }

    // Insurance
    public bool IsInsured { get; set; }

    [MaxLength(70)]
    public string? InsurancePolicyNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? InsuredValue { get; set; }

    public List<CreateAssetAttributeValueDto>? AttributeValues { get; set; }
}

/// <summary>
/// DTO for updating a company asset
/// </summary>
public class UpdateCompanyAssetDto : UpdateDtoBase
{
    [MaxLength(70)]
    public string AssetNumber { get; set; } = string.Empty;

    [MaxLength(70)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AssetName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetTypeId { get; set; }

    // Identification
    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(70)]
    public string? ModelNumber { get; set; }

    [MaxLength(70)]
    public string? SerialNumber { get; set; }

    // Purchase
    public DateOnly? PurchaseDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PurchaseCost { get; set; }

    [MaxLength(100)]
    public string? Supplier { get; set; }

    [MaxLength(70)]
    public string? InvoiceNumber { get; set; }

    // Warranty
    public bool HasWarranty { get; set; }
    public DateOnly? WarrantyStartDate { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }

    [MaxLength(100)]
    public string? WarrantyProvider { get; set; }

    // Physical Details
    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(30)]
    public string? Size { get; set; }

    [MaxLength(300)]
    public string? Specifications { get; set; }

    // Status
    public CompanyAssetStatus Status { get; set; }
    public HRAssetCondition Condition { get; set; }

    // Location
    public Guid? LocationId { get; set; }

    [MaxLength(500)]
    public string? LocationDetails { get; set; }

    // Assignment
    public bool IsAssignable { get; set; }

    // Maintenance
    public bool RequiresRegularMaintenance { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaintenanceIntervalDays { get; set; }

    public DateOnly? LastMaintenanceDate { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }

    // Insurance
    public bool IsInsured { get; set; }

    [MaxLength(70)]
    public string? InsurancePolicyNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? InsuredValue { get; set; }

    // Disposal
    public DateOnly? DisposalDate { get; set; }
    public DisposalMethod? DisposalMethod { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }

    public List<CreateAssetAttributeValueDto>? AttributeValues { get; set; }
}

/// <summary>
/// DTO for disposing a company asset
/// </summary>
public class DisposeAssetDto
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public DateOnly DisposalDate { get; set; }

    [Required]
    public DisposalMethod DisposalMethod { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }
}

#endregion

#region Asset Attribute Value DTOs

/// <summary>
/// DTO for asset attribute value read operations
/// </summary>
public class AssetAttributeValueDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public Guid AssetTypeAttributeId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public AssetAttributeDataType DataType { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating an asset attribute value
/// </summary>
public class CreateAssetAttributeValueDto : CreateDtoBase
{
    [Required]
    public Guid AssetTypeAttributeId { get; set; }

    [MaxLength(700)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating an asset attribute value
/// </summary>
public class UpdateAssetAttributeValueDto : UpdateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid AssetTypeAttributeId { get; set; }

    [MaxLength(700)]
    public string Value { get; set; } = string.Empty;
}

#endregion

#region Asset Assignment DTOs

/// <summary>
/// DTO for asset assignment read operations
/// </summary>
public class AssetAssignmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    
    // Assignment Details
    public DateOnly AssignmentDate { get; set; }
    public DateOnly? ExpectedReturnDate { get; set; }
    public AssignmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public AssignmentPurpose Purpose { get; set; }
    public string PurposeName => Purpose.ToString();
    public string? AssignmentNotes { get; set; }
    public bool IsPrimaryUser { get; set; }
    
    // Condition
    public HRAssetCondition ConditionAtAssignment { get; set; }
    public string ConditionAtAssignmentName => ConditionAtAssignment.ToString();
    public string? ConditionNotes { get; set; }
    
    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    
    // Acknowledgement
    public bool EmployeeAcknowledged { get; set; }
    public DateTime? AcknowledgementDate { get; set; }
    
    // Terms
    public bool ResponsibleForLoss { get; set; }
    public bool ResponsibleForDamage { get; set; }
    public string? TermsAndConditions { get; set; }
    
    // Return
    public AssignmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ReturnDate { get; set; }
    public HRAssetCondition? ConditionAtReturn { get; set; }
    public string? ConditionAtReturnName => ConditionAtReturn?.ToString();
    public string? ReturnNotes { get; set; }
    public bool ReturnedInGoodCondition { get; set; }
    public Guid? ReturnedToId { get; set; }
    public string? ReturnedToName { get; set; }
    
    // Damages
    public bool DamageReported { get; set; }
    public string? DamageDescription { get; set; }
    public bool EmployeeLiable { get; set; }
    public decimal? RepairCost { get; set; }
    public decimal? ReplacementCost { get; set; }
}

/// <summary>
/// Summary DTO for asset assignment list views
/// </summary>
public class AssetAssignmentSummaryDto
{
    public Guid Id { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly AssignmentDate { get; set; }
    public DateOnly? ExpectedReturnDate { get; set; }
    public AssignmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public AssignmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ReturnDate { get; set; }
}

/// <summary>
/// DTO for creating an asset assignment
/// </summary>
public class CreateAssetAssignmentDto : CreateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public DateOnly AssignmentDate { get; set; }

    public DateOnly? ExpectedReturnDate { get; set; }

    [Required]
    public AssignmentType Type { get; set; }

    [Required]
    public AssignmentPurpose Purpose { get; set; }

    [MaxLength(1000)]
    public string? AssignmentNotes { get; set; }

    public bool IsPrimaryUser { get; set; }

    [Required]
    public HRAssetCondition ConditionAtAssignment { get; set; }

    [MaxLength(1000)]
    public string? ConditionNotes { get; set; }

    public Guid? ApprovedById { get; set; }

    public bool ResponsibleForLoss { get; set; }
    public bool ResponsibleForDamage { get; set; }

    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }
}

/// <summary>
/// DTO for updating an asset assignment
/// </summary>
public class UpdateAssetAssignmentDto : UpdateDtoBase
{
    public DateOnly? ExpectedReturnDate { get; set; }

    [MaxLength(1000)]
    public string? AssignmentNotes { get; set; }

    public bool IsPrimaryUser { get; set; }

    public bool ResponsibleForLoss { get; set; }
    public bool ResponsibleForDamage { get; set; }

    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }
}

/// <summary>
/// DTO for acknowledging an asset assignment
/// </summary>
public class AcknowledgeAssignmentDto
{
    [Required]
    public Guid AssignmentId { get; set; }
}

/// <summary>
/// DTO for returning an assigned asset
/// </summary>
public class ReturnAssetDto
{
    [Required]
    public Guid AssignmentId { get; set; }

    [Required]
    public HRAssetCondition ConditionAtReturn { get; set; }

    [MaxLength(1000)]
    public string? ReturnNotes { get; set; }

    public bool ReturnedInGoodCondition { get; set; }

    [Required]
    public Guid ReturnedToId { get; set; }

    public bool DamageReported { get; set; }

    [MaxLength(1000)]
    public string? DamageDescription { get; set; }

    public bool EmployeeLiable { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RepairCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ReplacementCost { get; set; }
}

#endregion

#region Asset Maintenance DTOs

/// <summary>
/// DTO for asset maintenance read operations
/// </summary>
public class AssetMaintenanceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string MaintenanceNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public DateTime MaintenanceDate { get; set; }
    public AssetMaintenanceType Type { get; set; }
    public string TypeName => Type.ToString();
    public string Description { get; set; } = string.Empty;
    public string? WorkPerformed { get; set; }
    public string? PartsReplaced { get; set; }
    public bool IsInternalMaintenance { get; set; }
    public Guid? PerformedById { get; set; }
    public string? PerformedByName { get; set; }
    public string? ExternalServiceProvider { get; set; }
    public string? ServiceTicketNumber { get; set; }
    public decimal? Cost { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public MaintenanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
}

/// <summary>
/// Summary DTO for asset maintenance list views
/// </summary>
public class AssetMaintenanceSummaryDto
{
    public Guid Id { get; set; }
    public string MaintenanceNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public DateTime MaintenanceDate { get; set; }
    public AssetMaintenanceType Type { get; set; }
    public string TypeName => Type.ToString();
    public MaintenanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal? Cost { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
}

/// <summary>
/// DTO for creating an asset maintenance record
/// </summary>
public class CreateAssetMaintenanceDto : CreateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public DateTime MaintenanceDate { get; set; }

    [Required]
    public AssetMaintenanceType Type { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? WorkPerformed { get; set; }

    [MaxLength(2000)]
    public string? PartsReplaced { get; set; }

    public bool IsInternalMaintenance { get; set; }

    public Guid? PerformedById { get; set; }

    [MaxLength(1000)]
    public string? ExternalServiceProvider { get; set; }

    [MaxLength(70)]
    public string? ServiceTicketNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    public DateTime? NextMaintenanceDate { get; set; }

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating an asset maintenance record
/// </summary>
public class UpdateAssetMaintenanceDto : UpdateDtoBase
{
    [Required]
    public DateTime MaintenanceDate { get; set; }

    [Required]
    public AssetMaintenanceType Type { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? WorkPerformed { get; set; }

    [MaxLength(2000)]
    public string? PartsReplaced { get; set; }

    public bool IsInternalMaintenance { get; set; }

    public Guid? PerformedById { get; set; }

    [MaxLength(1000)]
    public string? ExternalServiceProvider { get; set; }

    [MaxLength(70)]
    public string? ServiceTicketNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    public DateTime? NextMaintenanceDate { get; set; }

    public MaintenanceStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Asset Image DTOs

/// <summary>
/// DTO for asset image read operations
/// </summary>
public class AssetImageDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public string? UploadedBy { get; set; }
}

/// <summary>
/// DTO for creating an asset image record
/// </summary>
public class CreateAssetImageDto : CreateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
}

#endregion

#region Asset Attachment DTOs

/// <summary>
/// DTO for asset attachment read operations
/// </summary>
public class AssetAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// DTO for creating an asset attachment
/// </summary>
public class CreateAssetAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// DTO for updating an asset attachment
/// </summary>
public class UpdateAssetAttachmentDto : UpdateDtoBase
{
    [Required]
    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

#endregion

#region Asset Requisition DTOs

/// <summary>
/// DTO for asset requisition read operations
/// </summary>
public class AssetRequisitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public HRAssetRequisitionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public string Justification { get; set; } = string.Empty;
    public DateTime? RequiredByDate { get; set; }
    public AssetRequisitionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    // Approval
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }
    
    // Rejection
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }
    
    // Fulfillment
    public bool IsFulfilled { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public Guid? FulfilledById { get; set; }
    public string? FulfilledByName { get; set; }
    public Guid? AssignedAssetId { get; set; }
    public string? AssignedAssetName { get; set; }
}

/// <summary>
/// Summary DTO for asset requisition list views
/// </summary>
public class AssetRequisitionSummaryDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public HRAssetRequisitionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public AssetRequisitionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? RequiredByDate { get; set; }
}

/// <summary>
/// DTO for creating an asset requisition
/// </summary>
public class CreateAssetRequisitionDto : CreateDtoBase
{
    [Required]
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public HRAssetRequisitionPriority Priority { get; set; } = HRAssetRequisitionPriority.Medium;

    [Required]
    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;

    public DateTime? RequiredByDate { get; set; }

    public AssetRequisitionStatus Status { get; set; } = AssetRequisitionStatus.Submitted;
}

/// <summary>
/// DTO for updating an asset requisition
/// </summary>
public class UpdateAssetRequisitionDto : UpdateDtoBase
{
    [Required]
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public HRAssetRequisitionPriority Priority { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;

    public DateTime? RequiredByDate { get; set; }

    public AssetRequisitionStatus? Status { get; set; }
}

/// <summary>
/// DTO for approving an asset requisition
/// </summary>
public class ApproveAssetRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }
}

/// <summary>
/// DTO for rejecting an asset requisition
/// </summary>
public class RejectAssetRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for fulfilling an asset requisition
/// </summary>
public class FulfillAssetRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one asset must be assigned")]
    public List<Guid> AssignedAssetIds { get; set; } = new();
}

#endregion

#region Asset Transfer DTOs

/// <summary>
/// DTO for asset transfer read operations
/// </summary>
public class AssetTransferDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public HRAssetTransferType Type { get; set; }
    public string TypeName => Type.ToString();
    
    // From
    public Guid? FromEmployeeId { get; set; }
    public string? FromEmployeeName { get; set; }
    public Guid? FromLocationId { get; set; }
    public string? FromLocationName { get; set; }
    public Guid? FromUnitId { get; set; }
    public string? FromUnitName { get; set; }
    
    // To
    public Guid? ToEmployeeId { get; set; }
    public string? ToEmployeeName { get; set; }
    public Guid? ToLocationId { get; set; }
    public string? ToLocationName { get; set; }
    public Guid? ToUnitId { get; set; }
    public string? ToUnitName { get; set; }
    
    public string? TransferReason { get; set; }
    public Guid InitiatedById { get; set; }
    public string InitiatedByName { get; set; } = string.Empty;
    public HRAssetTransferStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Summary DTO for asset transfer list views
/// </summary>
public class AssetTransferSummaryDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public HRAssetTransferType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? FromName { get; set; }
    public string? ToName { get; set; }
    public HRAssetTransferStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

/// <summary>
/// DTO for creating an asset transfer
/// </summary>
public class CreateAssetTransferDto : CreateDtoBase
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public DateTime TransferDate { get; set; }

    [Required]
    public HRAssetTransferType Type { get; set; }

    // From
    public Guid? FromEmployeeId { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? FromUnitId { get; set; }

    // To
    public Guid? ToEmployeeId { get; set; }
    public Guid? ToLocationId { get; set; }
    public Guid? ToUnitId { get; set; }

    [MaxLength(1000)]
    public string? TransferReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating an asset transfer
/// </summary>
public class UpdateAssetTransferDto : UpdateDtoBase
{
    [Required]
    public DateTime TransferDate { get; set; }

    [MaxLength(1000)]
    public string? TransferReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for approving an asset transfer
/// </summary>
public class ApproveAssetTransferDto
{
    [Required]
    public Guid TransferId { get; set; }
}

/// <summary>
/// DTO for completing an asset transfer
/// </summary>
public class CompleteAssetTransferDto
{
    [Required]
    public Guid TransferId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

