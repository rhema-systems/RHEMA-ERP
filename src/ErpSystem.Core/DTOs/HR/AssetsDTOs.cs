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

    /// <summary>AST-7 — "Additional Remarks".</summary>
    public string? AdditionalRemarks { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;

    /// <summary>AST-11 — HR-created, or picked from the Finance fixed-asset register.</summary>
    public AssetSource Source { get; set; } = AssetSource.HrCreated;
    public string SourceName => Source.ToString();

    /// <summary>The Finance fixed asset this stands for, where it stands for one.</summary>
    public Guid? FixedAssetId { get; set; }

    /// <summary>
    /// True where Finance owns this asset's money and its disposal, so a screen can grey those
    /// fields rather than offering an edit the API will refuse.
    /// </summary>
    public bool IsFinanceOwned => Source == AssetSource.FixedAssetsModule && FixedAssetId.HasValue;
    
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

    /// <summary>
    /// The organisation unit the asset sits in. ⚠ Present on the LIST dto since the port and
    /// permanently null until this slice, because nothing could ever set it — defect D-i(b).
    /// </summary>
    public Guid? UnitId { get; set; }
    public string? UnitName { get; set; }
    
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

    /// <summary>AST-4 — when the cover lapses.</summary>
    public DateOnly? InsuranceExpiryDate { get; set; }

    /// <summary>
    /// Cover that has already lapsed on an asset still flagged as insured. False — not null — when
    /// no expiry is recorded: an unknown date is not an expired one, and a screen that treated it
    /// as one would cry wolf across the whole register.
    /// </summary>
    public bool IsInsuranceExpired =>
        IsInsured && InsuranceExpiryDate.HasValue
        && InsuranceExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
    
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

    /// <summary>AST-11 — so a register list shows at a glance where each row came from.</summary>
    public AssetSource Source { get; set; } = AssetSource.HrCreated;
    public string SourceName => Source.ToString();
}

/// <summary>
/// Detailed DTO for company asset with related data
/// </summary>
public class CompanyAssetDetailDto : CompanyAssetDto
{
    /// <summary>
    /// AST-11 — what the Finance fixed-asset register says about this asset, read live.
    /// </summary>
    /// <remarks>
    /// Null on an HR-created asset. <b>Read through on demand, never copied onto the HR row</b>: a
    /// net book value changes at every depreciation run, and a stored copy would be wrong within
    /// the month. Only the detail read pays for the extra call — the list reads must not, which is
    /// why this lives here and not on <see cref="CompanyAssetDto"/>.
    /// </remarks>
    public FixedAssetLinkDto? FixedAsset { get; set; }

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

    /// <summary>AST-7 — "Additional Remarks".</summary>
    [MaxLength(1000)]
    public string? AdditionalRemarks { get; set; }

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

    /// <summary>
    /// The organisation unit the asset sits in. ⚠ Absent from both payloads until this slice, which
    /// is why the column — and the list column that reads it — were always null. Defect D-i(b).
    /// </summary>
    public Guid? UnitId { get; set; }

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

    /// <summary>AST-4 — when the cover lapses.</summary>
    public DateOnly? InsuranceExpiryDate { get; set; }

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

    /// <summary>AST-7 — "Additional Remarks".</summary>
    [MaxLength(1000)]
    public string? AdditionalRemarks { get; set; }

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

    /// <summary>
    /// The organisation unit the asset sits in. ⚠ Absent from both payloads until this slice, which
    /// is why the column — and the list column that reads it — were always null. Defect D-i(b).
    /// </summary>
    public Guid? UnitId { get; set; }

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

    /// <summary>AST-4 — when the cover lapses.</summary>
    public DateOnly? InsuranceExpiryDate { get; set; }

    // Disposal
    public DateOnly? DisposalDate { get; set; }
    public DisposalMethod? DisposalMethod { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }

    public List<CreateAssetAttributeValueDto>? AttributeValues { get; set; }
}

/// <summary>
/// What Finance says about a fixed asset HR has linked to — AST-11. Read-only in HR, all of it.
/// </summary>
public class FixedAssetLinkDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal AcquisitionCost { get; set; }

    /// <summary>Depreciated value as Finance holds it today. Never stored on the HR row.</summary>
    public decimal NetBookValue { get; set; }

    public string StatusName { get; set; } = string.Empty;
    public string? CurrentCustodianName { get; set; }
    public string? SerialNumber { get; set; }
}

/// <summary>
/// One row of the "pick an asset from Fixed Assets" list — AST-11.
/// </summary>
/// <remarks>
/// Deliberately thin. The picker needs enough to identify the thing and no more; the full financial
/// picture belongs on the Finance screens, and re-serving it here would be the duplication the
/// change document explicitly asks us to avoid.
/// </remarks>
public class FixedAssetPickDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public decimal NetBookValue { get; set; }
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// True where HR has already registered this fixed asset. Linked assets are RETURNED rather
    /// than filtered out, so a user who cannot find one is told why instead of being handed a list
    /// that silently omits it.
    /// </summary>
    public bool AlreadyLinked { get; set; }

    /// <summary>The HR asset holding the link, where there is one — so a screen can navigate to it.</summary>
    public Guid? LinkedCompanyAssetId { get; set; }
}

/// <summary>
/// Registers an asset in HR from an existing Finance fixed asset — AST-11.
/// </summary>
/// <remarks>
/// Identity fields are COPIED from Finance at the moment of linking (code, name, serial number,
/// purchase date, acquisition cost) so the HR register reads sensibly on its own and in a list. The
/// money is a display snapshot only — <see cref="CompanyAssetDetailDto.FixedAsset"/> is the live
/// truth, and HR refuses to edit the copied figures afterwards.
/// </remarks>
public class CreateAssetFromFixedAssetDto : CreateDtoBase
{
    [Required]
    public Guid FixedAssetId { get; set; }

    /// <summary>
    /// HR's own classification. Asked for rather than derived: Finance's categories are accounting
    /// classes and do not map onto the things HR issues to people.
    /// </summary>
    [Required]
    public Guid AssetTypeId { get; set; }

    [MaxLength(70)]
    public string AssetTag { get; set; } = string.Empty;

    public HRAssetCondition Condition { get; set; } = HRAssetCondition.Good;

    public Guid? LocationId { get; set; }

    public Guid? UnitId { get; set; }

    public bool IsAssignable { get; set; } = true;

    [MaxLength(1000)]
    public string? AdditionalRemarks { get; set; }
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

    /// <summary>The requisition that produced this assignment, where it came from one — D-e.</summary>
    public Guid? RequisitionId { get; set; }
    public string? RequisitionNumber { get; set; }

    /// <summary>The transfer that produced this assignment, where it came from one — slice 4.</summary>
    public Guid? TransferId { get; set; }
    public string? TransferNumber { get; set; }

    /// <summary>Whether the responsibility document has been served, and how — AST-5b.</summary>
    public DateTime? TermsDocumentSentAt { get; set; }
    public string? TermsDocumentSentTo { get; set; }
    public string? TermsDocumentSentByName { get; set; }
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

    /// <summary>AST-6b — who the asset is for, when that is not the person who asked.</summary>
    public Guid? BeneficiaryEmployeeId { get; set; }
    public string? BeneficiaryEmployeeName { get; set; }

    /// <summary>
    /// The employee the asset will actually be issued to: the beneficiary where one is named, the
    /// requester otherwise.
    /// </summary>
    /// <remarks>
    /// Computed here so that no screen has to re-derive it and get it subtly wrong, and so the rule
    /// is stated once. Fulfilment applies exactly the same one.
    /// </remarks>
    public Guid ForEmployeeId => BeneficiaryEmployeeId ?? RequestedById;
    public string ForEmployeeName => BeneficiaryEmployeeName ?? RequestedByName;

    /// <summary>True where somebody raised this on another employee's behalf.</summary>
    public bool IsOnBehalf => BeneficiaryEmployeeId.HasValue && BeneficiaryEmployeeId != RequestedById;
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

    /// <summary>
    /// The assets this requisition produced — D-e.
    /// </summary>
    /// <remarks>
    /// Replaces a single nullable asset column, which could only ever remember one of them however
    /// many were issued against a quantity. Populated on the by-id read from the assignments that
    /// cite this requisition, which is now the only record of the fact.
    /// </remarks>
    public List<RequisitionFulfilmentDto> FulfilledWith { get; set; } = new();
    public DateTime? FulfilledDate { get; set; }
    public Guid? FulfilledById { get; set; }
    public string? FulfilledByName { get; set; }
}

/// <summary>
/// Summary DTO for asset requisition list views
/// </summary>
public class AssetRequisitionSummaryDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>AST-6b — so a list can show who it is for, not only who asked.</summary>
    public string? BeneficiaryEmployeeName { get; set; }
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

    /// <summary>
    /// AST-6b — the employee this is for, when raising it on someone else's behalf.
    /// </summary>
    /// <remarks>
    /// Leave null to request for yourself. Naming somebody else requires the HR role or being that
    /// employee's recorded line manager. The requester is always taken from the token regardless, so
    /// this field can widen who benefits but never who is recorded as having asked.
    /// </remarks>
    public Guid? BeneficiaryEmployeeId { get; set; }

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

    // ⚠ There is deliberately no `Status` here any more (area 16, slice 3b). It used to be written
    // straight onto the record, so a caller could POST `{"status": 3}` and create a requisition
    // that was already Approved — no approver, no approval date, no workflow instance — and HR's
    // fulfilment gate ("only an approved requisition can be fulfilled") was satisfied by it. A new
    // requisition is a Draft; it reaches Submitted through `POST requisitions/{id}/submit` and
    // every state after that belongs to the approval workflow.
}

/// <summary>
/// DTO for updating an asset requisition
/// </summary>
public class UpdateAssetRequisitionDto : UpdateDtoBase
{
    [Required]
    public Guid AssetTypeId { get; set; }

    /// <summary>
    /// AST-6b — the employee this is for, when raising it on someone else's behalf.
    /// </summary>
    /// <remarks>
    /// Leave null to request for yourself. Naming somebody else requires the HR role or being that
    /// employee's recorded line manager. The requester is always taken from the token regardless, so
    /// this field can widen who benefits but never who is recorded as having asked.
    /// </remarks>
    public Guid? BeneficiaryEmployeeId { get; set; }

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

    // ⚠ No `Status` here either, and for a worse reason than on the create DTO: this one applied to
    // an EXISTING record, so a requester editing their own draft could set it to Approved or
    // straight to Fulfilled. Slice 3b: the workflow owns the status, the form owns the request.
}

/// <summary>
/// Why a request is being pulled back out of an approval queue — requisitions and transfers alike.
/// </summary>
/// <remarks>
/// The body is optional on both recall routes: a recall with no reason is still a recall, and
/// refusing one over a missing sentence would only teach people to type a full stop.
/// </remarks>
public class RecallAssetRequestDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>The rendered responsibility-and-terms document — AST-5.</summary>
public class AssetTermsLetterDto
{
    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;

    /// <summary>Rendered subject line, used when the document is emailed.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Self-contained HTML document body, suitable for display and print-to-PDF.</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Whether this document has already been served, and to which address.</summary>
    public DateTime? TermsDocumentSentAt { get; set; }
    public string? TermsDocumentSentTo { get; set; }
}

/// <summary>What happened when the document was emailed to the holder — AST-5b.</summary>
public class AssetTermsLetterSendResultDto
{
    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;

    /// <summary>The address it actually went to, which is what the record keeps.</summary>
    public string SentTo { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string SentByName { get; set; } = string.Empty;
}

/// <summary>One asset issued against a requisition, and the assignment that issued it — D-e.</summary>
public class RequisitionFulfilmentDto
{
    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly AssignmentDate { get; set; }
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

