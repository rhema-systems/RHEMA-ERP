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

    /// <summary>
    /// This asset's counterpart in the Maintenance module's register, where it has one. Slice 9.
    /// </summary>
    public Guid? MaintenanceAssetId { get; set; }

    /// <summary>
    /// True where this asset is known to the Maintenance module, so a screen can offer "send for
    /// maintenance" rather than a dead button. It says nothing about who schedules the servicing:
    /// HR watches its own assets whether or not they are linked.
    /// </summary>
    public bool IsKnownToMaintenance => MaintenanceAssetId.HasValue;
    
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

    /// <summary>AST-9 — whether an employee can be charged for holding this.</summary>
    public bool IsRentable { get; set; }
    public decimal? StandardRentalAmount { get; set; }
    public string? RentalCurrencyCode { get; set; }
    
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

    /// <summary>
    /// What the asset cost to acquire. <b>Not</b> a current or book value — defect D-jj.
    /// </summary>
    /// <remarks>
    /// This field was called <c>CurrentValue</c> and was mapped from <c>entity.PurchaseCost</c>,
    /// so every register list answered the acquisition cost under a name that promised a
    /// depreciated one. Nobody reading a column headed "current value" expects the price paid four
    /// years ago, and totalling that column gave a figure that was wrong in a direction nobody
    /// could see. Renamed rather than made true: HR has no valuation to give. Depreciation, net
    /// book value and disposal accounting are Finance's under decision <b>D1</b>, reachable on a
    /// linked asset through <c>CompanyAssetDetailDto.FixedAsset</c> and simply absent on an
    /// HR-created one.
    /// </remarks>
    public decimal? PurchaseCost { get; set; }

    /// <summary>AST-9 — so the register can answer "what do we let to staff" from a list.</summary>
    public bool IsRentable { get; set; }

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

    /// <summary>
    /// Slice 9 — this asset's counterpart in the Maintenance module's register, so work can be sent
    /// there. HR keeps its own maintenance schedule either way.
    /// </summary>
    public Guid? MaintenanceAssetId { get; set; }

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

    // Rental — AST-9. ⚠ A field on the payload AND in the mapping AND read back: this area has
    // produced a permanently-null column six times by getting one of the three, so all three or
    // none. D-i(b) is the same shape one slice earlier.
    /// <summary>Whether an employee can be charged for holding this — staff housing, a car.</summary>
    public bool IsRentable { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? StandardRentalAmount { get; set; }

    [MaxLength(3)]
    public string? RentalCurrencyCode { get; set; }

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

    /// <summary>
    /// Slice 9 — this asset's counterpart in the Maintenance module's register, so work can be sent
    /// there. HR keeps its own maintenance schedule either way.
    /// </summary>
    public Guid? MaintenanceAssetId { get; set; }

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

    // Rental — AST-9. ⚠ A field on the payload AND in the mapping AND read back: this area has
    // produced a permanently-null column six times by getting one of the three, so all three or
    // none. D-i(b) is the same shape one slice earlier.
    /// <summary>Whether an employee can be charged for holding this — staff housing, a car.</summary>
    public bool IsRentable { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? StandardRentalAmount { get; set; }

    [MaxLength(3)]
    public string? RentalCurrencyCode { get; set; }

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

    /// <summary>
    /// What kind of thing it is — defect <b>D-ll</b>, found by the slice-12 content audit.
    /// </summary>
    /// <remarks>
    /// The SUMMARY has carried this since slice 6 and the full record did not, so the detail screen
    /// could say less about an asset than the list row that opened it. The <c>.Include</c> that
    /// feeds it was already there (<c>Asset.AssetType</c> on <c>GetWithDetailsAsync</c>) — only the
    /// property and its mapping were missing, which is the same half-a-change shape as D-w, and the
    /// seventh time in this area that a field existed on one side of a pair and not the other.
    /// </remarks>
    public string AssetTypeName { get; set; } = string.Empty;

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

    /// <summary>
    /// Days until the asset is due back; negative once it is late. Null where no return was
    /// expected, or where it has already come back — area 16 slice 11.
    /// </summary>
    /// <remarks>
    /// <para><b>Derived, not mapped</b>, and deliberately so: this area has produced a blank field
    /// six separate times from a DTO property that a mapper forgot. A property computed from data
    /// already on the row cannot be forgotten by anything.</para>
    ///
    /// <para>Signed for the reason the maintenance watchlists give: an absolute number plus a flag
    /// lets a caller sort an exception list and put the worst row at the bottom.</para>
    /// </remarks>
    public int? DaysUntilReturnDue => ExpectedReturnDate is not { } due || ReturnDate is not null
        ? null
        : due.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
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

    // Rental — AST-10, decision D2
    public decimal? RentalAmount { get; set; }
    public string? RentalCurrencyCode { get; set; }
    public RentalDeductionFrequency? RentalFrequency { get; set; }
    public string? RentalFrequencyName => RentalFrequency?.ToString();
    public DateOnly? RentalEffectiveFrom { get; set; }
    public DateOnly? RentalEffectiveTo { get; set; }
    public bool IsBenefitInKind { get; set; }
    public decimal? BenefitInKindValue { get; set; }

    /// <summary>True where rental terms have been declared at all.</summary>
    public bool HasRentalTerms => RentalFrequency.HasValue;

    /// <summary>
    /// The rent is stated, the period has started, and nothing has closed it.
    /// </summary>
    /// <remarks>
    /// Answers the question the payroll projection asks, so a screen and the projection cannot
    /// disagree about whether an arrangement is running.
    /// </remarks>
    public bool IsRentalRunning =>
        HasRentalTerms
        && (RentalEffectiveFrom is null || RentalEffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow))
        && (RentalEffectiveTo is null || RentalEffectiveTo >= DateOnly.FromDateTime(DateTime.UtcNow));
}

/// <summary>
/// Summary DTO for asset assignment list views
/// </summary>
public class AssetAssignmentSummaryDto
{
    public Guid Id { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// The asset itself, not only its name — slice 6.
    /// </summary>
    /// <remarks>
    /// A list that names an asset without identifying it forces every screen showing it to fetch
    /// each row again to be able to link anywhere, and the employee's own list is exactly that
    /// screen. The number is here for the same reason the name is: it is what a person reads off
    /// the label stuck to the thing in their hands.
    /// </remarks>
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetTypeName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly AssignmentDate { get; set; }
    public DateOnly? ExpectedReturnDate { get; set; }
    public AssignmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public AssignmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ReturnDate { get; set; }

    /// <summary>See <see cref="AssetAssignmentDto.DaysUntilReturnDue"/>.</summary>
    public int? DaysUntilReturnDue => ExpectedReturnDate is not { } due || ReturnDate is not null
        ? null
        : due.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;

    /// <summary>Whether the holder has signed for it, and when — AST-8.</summary>
    /// <remarks>
    /// On the summary because the whole point of the employee's list is to show what still needs
    /// acknowledging; deriving that would mean reading every row in full to find the few that do.
    /// </remarks>
    public bool EmployeeAcknowledged { get; set; }
    public DateTime? AcknowledgementDate { get; set; }

    /// <summary>
    /// What the holder is charged for this, per period — AST-10.
    /// </summary>
    /// <remarks>
    /// On the summary because the employee's own list is where somebody finds out they are paying
    /// rent for a company flat, and making them open every row to discover it is how a deduction
    /// becomes a surprise on a payslip. Columns on the assignment itself, so no read pays for them.
    /// </remarks>
    public decimal? RentalAmount { get; set; }
    public string? RentalCurrencyCode { get; set; }
    public string? RentalFrequencyName { get; set; }
    public bool IsBenefitInKind { get; set; }
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

    /// <summary>Slice 9b — the workshop admission this record was sent out on, where there is one.</summary>
    public Guid? MaintenanceAdmissionId { get; set; }
    public string? MaintenanceAdmissionNumber { get; set; }
    public Guid? MaintenanceDischargeId { get; set; }

    /// <summary>
    /// True while the asset is physically at the workshop — sent, and not yet discharged. The one
    /// question a maintenance record could not answer before slice 9b.
    /// </summary>
    public bool IsAtWorkshop => MaintenanceAdmissionId.HasValue && !MaintenanceDischargeId.HasValue;
}

/// <summary>
/// Summary DTO for asset maintenance list views
/// </summary>
public class AssetMaintenanceSummaryDto
{
    public Guid Id { get; set; }
    public string MaintenanceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Which asset this row is about — <b>defect D-ee</b>, area 16 slice 9.
    /// </summary>
    /// <remarks>
    /// The summary carried a name and no identity, so nothing reading a maintenance list could open
    /// the asset behind a row, and two assets sharing a name were indistinguishable. This is the
    /// same shape as D-y on the assignment summary: a list DTO that names a thing it cannot point
    /// at. The name alone also made the list read disagree with the by-id read, which carries both.
    /// </remarks>
    public Guid AssetId { get; set; }

    public string AssetNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public DateTime MaintenanceDate { get; set; }
    public AssetMaintenanceType Type { get; set; }
    public string TypeName => Type.ToString();
    public MaintenanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal? Cost { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }

    /// <summary>
    /// Slice 9b. On the summary as well as the detail, because "which of these is not in the
    /// building" is a list question, and answering it a click at a time is how an asset stays lost.
    /// </summary>
    public string? MaintenanceAdmissionNumber { get; set; }
    public bool IsAtWorkshop { get; set; }
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
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>AST-6b — so a list can show who it is for, not only who asked.</summary>
    public Guid? BeneficiaryEmployeeId { get; set; }
    public string? BeneficiaryEmployeeName { get; set; }

    /// <summary>
    /// The employee the asset will be issued to: the beneficiary where one is named, the requester
    /// otherwise — the same rule the full DTO and fulfilment both apply, stated once.
    /// </summary>
    public string ForEmployeeName => BeneficiaryEmployeeName ?? RequestedByName;

    /// <summary>True where somebody raised this on another employee's behalf.</summary>
    public bool IsOnBehalf => BeneficiaryEmployeeId.HasValue && BeneficiaryEmployeeId != RequestedById;
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

#region Asset Incident and Surcharge DTOs — area 16 slice 7 (AST-3, D-d, decision D9)

/// <summary>
/// Reports that a company asset was lost or damaged beyond return, closing the custody.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="ReturnAssetDto"/> for the assets that never come back.
/// <c>AssignmentStatus.Lost</c> and <c>.Damaged</c> existed from the port with no writer anywhere,
/// so an asset that was never returned could only be recorded by pretending it had been.
/// </remarks>
public class ReportAssetIncidentDto
{
    [Required]
    public Guid AssignmentId { get; set; }

    [Required]
    public AssetIncidentOutcome Outcome { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateOnly? OccurredOn { get; set; }

    /// <summary>Whether the holder is answerable for it. The surcharge decision is separate and later.</summary>
    public bool EmployeeLiable { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RepairCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ReplacementCost { get; set; }
}

public class AssetSurchargeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SurchargeNumber { get; set; } = string.Empty;

    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public AssetSurchargeReason Reason { get; set; }
    public string ReasonName => Reason.ToString();
    public string Description { get; set; } = string.Empty;

    // ── the money ────────────────────────────────────────────────────────────
    public decimal AssessedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>What the assignment said the damage cost when this was raised — the basis, kept.</summary>
    public decimal? BasisRepairCost { get; set; }
    public decimal? BasisReplacementCost { get; set; }

    public decimal AmountRecovered { get; set; }

    /// <summary>
    /// What is still owed. Computed here so no screen re-derives it and gets it subtly wrong, and
    /// so the exit settlement (FR-HR-184) has one number to read.
    /// </summary>
    public decimal AmountOutstanding => AssessedAmount - AmountRecovered;

    /// <summary>True where the charge was set below what the damage actually cost.</summary>
    public bool IsBelowAssessedCost =>
        BasisReplacementCost is { } r ? AssessedAmount < r
        : BasisRepairCost is { } p && AssessedAmount < p;

    // ── state ────────────────────────────────────────────────────────────────
    public AssetSurchargeStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? RaisedById { get; set; }
    public string? RaisedByName { get; set; }
    public DateTime RaisedAt { get; set; }

    // ── the employee's side ──────────────────────────────────────────────────
    public DateTime? NotifiedAt { get; set; }
    public AssetSurchargeEmployeeResponse EmployeeResponse { get; set; }
    public string EmployeeResponseName => EmployeeResponse.ToString();
    public DateTime? EmployeeRespondedAt { get; set; }
    public string? EmployeeResponseComments { get; set; }
    public string? ProceededWithoutResponseReason { get; set; }

    /// <summary>The employee has been told and has not answered — the gate that holds a submit.</summary>
    public bool IsAwaitingEmployee =>
        Status == AssetSurchargeStatus.WithEmployee
        && EmployeeResponse == AssetSurchargeEmployeeResponse.NotYetGiven;

    public bool IsDisputed => EmployeeResponse == AssetSurchargeEmployeeResponse.Disputed;

    // ── the decision ─────────────────────────────────────────────────────────
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ApprovalComments { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // ── recovery, declared ───────────────────────────────────────────────────
    public AssetSurchargeRecoveryMethod? RecoveryMethod { get; set; }
    public string? RecoveryMethodName => RecoveryMethod?.ToString();
    public int? InstalmentCount { get; set; }
    public DateOnly? RecoveryStartDate { get; set; }

    /// <summary>What one instalment comes to, where a plan says how many. Payroll deducts, not this.</summary>
    public decimal? InstalmentAmount =>
        InstalmentCount is > 0 ? decimal.Round(AssessedAmount / InstalmentCount.Value, 2) : null;

    public IEnumerable<AssetSurchargeRecoveryDto> Recoveries { get; set; } = [];

    // ── other endings ────────────────────────────────────────────────────────
    public Guid? WaivedById { get; set; }
    public string? WaivedByName { get; set; }
    public DateTime? WaivedAt { get; set; }
    public string? WaiverReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class AssetSurchargeSummaryDto
{
    public Guid Id { get; set; }
    public string SurchargeNumber { get; set; } = string.Empty;
    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public AssetSurchargeReason Reason { get; set; }
    public string ReasonName => Reason.ToString();
    public decimal AssessedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal AmountRecovered { get; set; }
    public decimal AmountOutstanding => AssessedAmount - AmountRecovered;
    public AssetSurchargeStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public AssetSurchargeEmployeeResponse EmployeeResponse { get; set; }
    public string EmployeeResponseName => EmployeeResponse.ToString();
    public bool IsDisputed => EmployeeResponse == AssetSurchargeEmployeeResponse.Disputed;
    public DateTime RaisedAt { get; set; }
    public DateOnly? RecoveryStartDate { get; set; }
}

public class AssetSurchargeRecoveryDto : BaseDto
{
    public Guid SurchargeId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly RecoveredOn { get; set; }
    public AssetSurchargeRecoveryMethod Method { get; set; }
    public string MethodName => Method.ToString();
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedById { get; set; }
    public string? RecordedByName { get; set; }
}

public class CreateAssetSurchargeDto : CreateDtoBase
{
    [Required]
    public Guid AssignmentId { get; set; }

    [Required]
    public AssetSurchargeReason Reason { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// What the employee is asked to pay.
    /// </summary>
    /// <remarks>
    /// Leave null to take the assignment's replacement cost, or its repair cost where there is no
    /// replacement cost — a default, never a rule. An employer routinely charges less than the
    /// damage cost, and the basis is kept on the record either way so the two can be compared.
    /// </remarks>
    [Range(0, double.MaxValue)]
    public decimal? AssessedAmount { get; set; }

    /// <summary>Defaults to the tenant's base currency when omitted. Validated against Finance.</summary>
    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
}

public class UpdateAssetSurchargeDto : UpdateDtoBase
{
    [Required]
    public AssetSurchargeReason Reason { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, double.MaxValue)]
    public decimal AssessedAmount { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
}

/// <summary>The employee's answer to a charge put to them — decision D9.</summary>
public class RespondToAssetSurchargeDto
{
    /// <summary>True to accept the charge, false to dispute it. Both send it on for approval.</summary>
    [Required]
    public bool Accepted { get; set; }


    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class ApproveAssetSurchargeDto
{
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    /// <summary>
    /// The amount finally charged, where the approver settles on a different figure.
    /// </summary>
    /// <remarks>
    /// Reducing a charge on approval is the ordinary outcome of a dispute the employee won in part.
    /// Omit to approve the amount as assessed; it may be lowered, never raised — a figure the
    /// employee was never given a chance to answer is not one they can be charged.
    /// </remarks>
    [Range(0, double.MaxValue)]
    public decimal? ApprovedAmount { get; set; }
}

public class RejectAssetSurchargeDto
{
    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Sends a charge for approval.
/// </summary>
public class SubmitAssetSurchargeDto
{
    /// <summary>
    /// Required only where the employee has been asked and has not answered.
    /// </summary>
    /// <remarks>
    /// The right of reply is a right to be asked, not a veto exercised by silence — but proceeding
    /// past it is a decision somebody makes, so it is stated and recorded rather than defaulted.
    /// </remarks>
    [MaxLength(1000)]
    public string? ProceedWithoutResponseReason { get; set; }
}

/// <summary>How the approved amount is to be recovered — a declaration to payroll, not a deduction.</summary>
public class SetAssetSurchargeRecoveryPlanDto
{
    [Required]
    public AssetSurchargeRecoveryMethod RecoveryMethod { get; set; }

    [Range(1, 120)]
    public int? InstalmentCount { get; set; }

    public DateOnly? RecoveryStartDate { get; set; }
}

public class RecordAssetSurchargeRecoveryDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateOnly RecoveredOn { get; set; }

    [Required]
    public AssetSurchargeRecoveryMethod Method { get; set; }

    [MaxLength(200)]
    public string? Reference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class WaiveAssetSurchargeDto
{
    [Required]
    [MaxLength(1000)]
    public string WaiverReason { get; set; } = string.Empty;
}

public class CancelAssetSurchargeDto
{
    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

/// <summary>
/// The read-only projection payroll consumes — one line per employee with something to deduct.
/// </summary>
/// <remarks>
/// HR states what is owed and how it was declared to be recovered. It computes no payslip, writes
/// nothing to payroll, and holds no deduction of its own — the same boundary decision D2 draws for
/// rental. Only <b>approved</b> charges with an outstanding balance and a payroll-deduction plan
/// appear; a disputed-but-unapproved charge is not yet a debt.
/// </remarks>
public class AssetSurchargePayrollLineDto
{
    public Guid SurchargeId { get; set; }
    public string SurchargeNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal AssessedAmount { get; set; }
    public decimal AmountRecovered { get; set; }
    public decimal AmountOutstanding { get; set; }
    public int? InstalmentCount { get; set; }
    public decimal? InstalmentAmount { get; set; }
    public DateOnly? RecoveryStartDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

#endregion

#region Rental and the payroll seam — area 16 slice 8 (AST-9, AST-10, decision D2)

/// <summary>
/// Declares what an employee is charged for holding a rentable asset — AST-10.
/// </summary>
/// <remarks>
/// ⚠ A <b>declaration</b>. Nothing here deducts anything: payroll reads
/// <c>GET Assets/payroll/rental-deductions</c> and runs its own deduction. HR does not know
/// payroll's periods, its proration or its net-pay floor, and inventing them here is exactly the
/// parallel mechanism the ownership boundary exists to prevent (decision D2).
/// </remarks>
public class SetAssetRentalTermsDto
{
    /// <summary>
    /// What the employee pays per period. <b>Zero is not null.</b>
    /// </summary>
    /// <remarks>
    /// Zero means the asset is provided free — a stated arrangement, and usually a taxable one.
    /// Null means nobody has said. Leave it out to take the asset's standard rate.
    /// </remarks>
    [Range(0, double.MaxValue)]
    public decimal? RentalAmount { get; set; }

    /// <summary>Defaults to the asset's rental currency, then to Finance's base currency.</summary>
    [MaxLength(3)]
    public string? RentalCurrencyCode { get; set; }

    [Required]
    public RentalDeductionFrequency RentalFrequency { get; set; }

    /// <summary>Defaults to the assignment date — rent runs from when they got the keys.</summary>
    public DateOnly? RentalEffectiveFrom { get; set; }

    /// <summary>Open-ended when omitted. Closing the custody closes it either way.</summary>
    public DateOnly? RentalEffectiveTo { get; set; }

    public bool IsBenefitInKind { get; set; }

    /// <summary>
    /// The taxable value per period, where it is not simply the rent charged.
    /// </summary>
    /// <remarks>
    /// Left null on a benefit-in-kind arrangement it is computed as the asset's standard rate less
    /// what the employee pays — the value of the subsidy, which is a fact HR holds. Assessing tax
    /// on it is payroll's.
    /// </remarks>
    [Range(0, double.MaxValue)]
    public decimal? BenefitInKindValue { get; set; }
}

/// <summary>
/// One employee, one rentable asset, one period — the read-only line payroll pulls.
/// </summary>
/// <remarks>
/// <para><b>HR declares; payroll deducts.</b> This carries no payroll period id, no deduction code
/// and no net-pay arithmetic, because those belong to a module this one integrates with read-only.
/// What it does carry is everything payroll needs to build its own row and everything an auditor
/// needs to see where the figure came from: the asset, the custody, the rate, the currency, the
/// window, and whether the arrangement is a charge, a taxable benefit, or both.</para>
///
/// <para>⚠ <c>AmountForPeriod</c> is <b>not</b> prorated. A tenancy that starts mid-month is
/// reported with its window and its full periodic rate; how much of it falls in a given pay run is
/// payroll's calculation, made with payroll's calendar. Prorating here would be HR guessing at
/// somebody else's period boundaries and being quietly wrong.</para>
/// </remarks>
public class AssetRentalPayrollLineDto
{
    public Guid AssignmentId { get; set; }
    public string AssignmentNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetTypeName { get; set; } = string.Empty;

    /// <summary>What the employee pays per period. Zero where the asset is provided free.</summary>
    public decimal RentalAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public RentalDeductionFrequency Frequency { get; set; }
    public string FrequencyName => Frequency.ToString();

    /// <summary>Whether there is anything to deduct at all.</summary>
    public bool IsDeductible => RentalAmount > 0;

    public bool IsBenefitInKind { get; set; }

    /// <summary>The taxable value per period, where the arrangement is a benefit.</summary>
    public decimal? BenefitInKindValue { get; set; }

    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>The period this line was requested for, echoed back.</summary>
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// True where the arrangement covers only part of the requested period — it started or ended
    /// inside it. Payroll prorates; this only flags that there is something to prorate.
    /// </summary>
    public bool IsPartialPeriod { get; set; }
}

#endregion

#region Maintenance monitoring DTOs — area 16 slice 9, AST-1

/// <summary>
/// An asset whose maintenance is due, overdue, or has never been scheduled at all.
/// </summary>
/// <remarks>
/// <para><b>Why this type exists — defect D-ff.</b> The one monitoring read this module had,
/// <c>GET api/Assets/due-maintenance</c>, answered with <see cref="CompanyAssetSummaryDto"/>, which
/// carries <b>no maintenance date of any kind</b>. It could therefore say <i>that</i> a set of
/// assets needed attention and never <i>when</i>, <i>how late</i>, or <i>in what order</i> — which
/// is the entire content of AST-1's "there must be a way to monitor it". A list of thirty assets
/// with no dates is not a schedule; it is a hint that somebody should go and look.</para>
///
/// <para><b><see cref="DaysRemaining"/> is signed on purpose.</b> Negative is overdue. Collapsing it
/// to an absolute number plus a flag would let a caller sort a list and put the most urgent row at
/// the bottom, which is the failure mode a monitoring screen cannot survive.</para>
///
/// <para><see cref="LastMaintenanceDate"/> and <see cref="MaintenanceIntervalDays"/> travel with the
/// row because the first question anyone asks about an overdue asset is whether the schedule is
/// real — a 30-day interval on something last serviced two years ago is a data problem, not a
/// maintenance problem, and the two need telling apart from the list.</para>
/// </remarks>
public class AssetMaintenanceDueDto
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

    /// <summary>
    /// Who is holding it, when somebody is. Maintenance on an issued asset has to be arranged with
    /// the holder, so a schedule that cannot name them sends the planner back to the register.
    /// </summary>
    public bool IsCurrentlyAssigned { get; set; }
    public Guid? CurrentAssignedToId { get; set; }
    public string? CurrentAssignedToName { get; set; }

    public bool RequiresRegularMaintenance { get; set; }
    public int? MaintenanceIntervalDays { get; set; }
    public DateOnly? LastMaintenanceDate { get; set; }

    /// <summary>Null only on the unscheduled read — see <see cref="IsScheduled"/>.</summary>
    public DateOnly? NextMaintenanceDate { get; set; }

    /// <summary>False when the asset requires maintenance and no next date has ever been set.</summary>
    public bool IsScheduled { get; set; }

    /// <summary>Signed: negative means overdue by that many days. Zero on the unscheduled read.</summary>
    public int DaysRemaining { get; set; }

    public bool IsOverdue { get; set; }

    /// <summary>The date the read was taken as at, echoed back so a stale screen is detectable.</summary>
    public DateOnly AsOf { get; set; }
}

#endregion

#region Insurance, returns and the register report — area 16 slice 11

/// <summary>
/// An asset whose insurance is expiring, has lapsed, or is claimed without a date.
/// </summary>
/// <remarks>
/// <para><b>Its own type, for the reason <see cref="AssetMaintenanceDueDto"/> exists.</b>
/// <c>CompanyAssetSummaryDto</c> carries no insurance field at all, so a watchlist answered with it
/// could say <i>that</i> cover needed attention and never <i>whose policy</i>, <i>for how much</i>,
/// or <i>by when</i> — defect D-ff one surface across. Slice 2b put
/// <c>InsuranceExpiryDate</c> on the register end to end and nothing has ever read it back;
/// this is the read.</para>
///
/// <para><b><see cref="DaysRemaining"/> is signed</b>, negative meaning already lapsed, so an
/// exception list can be sorted worst-first. Same rule as the maintenance watchlists, stated once
/// in each so the two cannot drift.</para>
/// </remarks>
public class AssetInsuranceWatchItemDto
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

    /// <summary>
    /// Who is holding it, when somebody is.
    /// </summary>
    /// <remarks>
    /// Renewing cover on an issued asset needs the holder as much as booking a service does — an
    /// insurer asks where the thing is kept, and the register cannot answer that without naming
    /// the person who has it.
    /// </remarks>
    public bool IsCurrentlyAssigned { get; set; }
    public Guid? CurrentAssignedToId { get; set; }
    public string? CurrentAssignedToName { get; set; }

    public bool IsInsured { get; set; }
    public string? InsurancePolicyNumber { get; set; }

    /// <summary>
    /// What the asset is insured for.
    /// </summary>
    /// <remarks>
    /// ⚠ Carries <b>no currency</b>, because <c>CompanyAsset.InsuredValue</c> does not — unlike the
    /// surcharge and rental amounts, which do. Nothing in the register can express another
    /// currency, so nothing here is losing one; but a figure with no currency beside one that has
    /// is worth knowing about before somebody totals a column of them. Recorded for the
    /// HR↔Finance sweep.
    /// </remarks>
    public decimal? InsuredValue { get; set; }

    /// <summary>Null only on the undated read — see <see cref="IsDated"/>.</summary>
    public DateOnly? InsuranceExpiryDate { get; set; }

    /// <summary>False when the asset is marked insured and no expiry date has ever been set.</summary>
    public bool IsDated { get; set; }

    /// <summary>Signed: negative means cover lapsed that many days ago. Zero on the undated read.</summary>
    public int DaysRemaining { get; set; }

    public bool IsExpired { get; set; }

    /// <summary>The date the read was taken as at, echoed back so a stale screen is detectable.</summary>
    public DateOnly AsOf { get; set; }
}

/// <summary>
/// One grouped line of the register report — a count and its money, under some heading.
/// </summary>
public class AssetRegisterGroupDto
{
    /// <summary>Null on a grouping that has no record behind it, such as "no unit set".</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int AssetCount { get; set; }

    public decimal TotalPurchaseCost { get; set; }

    /// <summary>How many of this group are in somebody's hands right now.</summary>
    public int AssignedCount { get; set; }
}

/// <summary>
/// The asset register, counted and totalled — area 16 slice 11.
/// </summary>
/// <remarks>
/// <para><b>A summary, not a listing.</b> The rows are already available through
/// <c>GET api/Assets/paged</c> with the same filters; repeating them here would produce a
/// document that is unbounded in exactly the situation it is most wanted — a large register.
/// What a register report adds is the arithmetic nobody can do from a paged screen.</para>
///
/// <para><b>The watchlist counts are here on purpose.</b> A register report that says how many
/// assets exist and not how many of them are uninsured, unscheduled or overdue is a stocktake,
/// and this area already has the three maintenance reads and the three insurance reads that
/// answer those. Counting them beside the totals is what turns the page into something worth
/// signing.</para>
///
/// <para>⚠ <b>The money is purchase cost and carries no currency</b> — see
/// <see cref="TotalPurchaseCost"/>. Nothing here is a book value: depreciation and valuation are
/// Finance's under decision D1, and for an HR-created asset there is no valuation at all.</para>
/// </remarks>
public class AssetRegisterReportDto
{
    public DateTime GeneratedAt { get; set; }

    public DateOnly AsOf { get; set; }

    // ── what was asked for, echoed back so a printed page says what it covers ──
    public Guid? AssetTypeId { get; set; }
    public string? AssetTypeName { get; set; }
    public Guid? UnitId { get; set; }
    public string? UnitName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public CompanyAssetStatus? Status { get; set; }
    public string? StatusName { get; set; }

    // ── the totals ────────────────────────────────────────────────────────────
    public int AssetCount { get; set; }
    public int AssignedCount { get; set; }
    public int UnassignedCount { get; set; }
    public int AssignableCount { get; set; }
    public int RentableCount { get; set; }
    public int InsuredCount { get; set; }
    public int UninsuredCount { get; set; }
    public int FromFixedAssetsCount { get; set; }
    public int HrCreatedCount { get; set; }
    public int LinkedToMaintenanceCount { get; set; }

    /// <summary>
    /// The sum of every asset's purchase cost, in whatever the organisation's money is.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>CompanyAsset.PurchaseCost</c> carries no currency code, so this total is only safe
    /// because nothing in the register can express a second currency. It is not a book value and
    /// must not be presented as one.
    /// </remarks>
    public decimal TotalPurchaseCost { get; set; }

    public decimal TotalInsuredValue { get; set; }

    /// <summary>How many rows carry no purchase cost at all, so a nil total is readable as one.</summary>
    /// <remarks>
    /// Without this the report cannot distinguish "the estate cost nothing" from "nobody entered
    /// what it cost" — and on a register that has just been migrated, the second is the answer.
    /// </remarks>
    public int AssetsWithoutPurchaseCost { get; set; }

    // ── the breakdowns ────────────────────────────────────────────────────────
    public List<AssetRegisterGroupDto> ByStatus { get; set; } = new();
    public List<AssetRegisterGroupDto> ByAssetType { get; set; } = new();
    public List<AssetRegisterGroupDto> ByCondition { get; set; } = new();
    public List<AssetRegisterGroupDto> ByUnit { get; set; } = new();
    public List<AssetRegisterGroupDto> ByLocation { get; set; } = new();

    // ── what needs attention ──────────────────────────────────────────────────
    //
    // ⚠ THESE COUNTS ARE DISJOINT AND THE WATCHLIST READS ARE NOT, which is why they are named
    // "…DueSoon" and "…ExpiringSoon" rather than "…Due" and "…Expiring".
    //
    // A LIST is opened by somebody asking "what is due?", and it would be a poor answer to hide the
    // rows that are already late — so `GET due-maintenance?daysAhead=30` means "due on or before
    // then", overdue rows included, and slice 9 asserts exactly that. A COUNT on a summary page is
    // read differently: three numbers in a row get added up, and a reader who sees "due 12,
    // overdue 5" and is given 12 that already contains the 5 has been misled by arithmetic they
    // were entitled to do.
    //
    // So the report counts what the SWEEP counts — `MaintenanceDueSoon` and `MaintenanceOverdue`
    // are already separate rungs there — and the identity that ties the two vocabularies together
    // is worth stating, because it is what the harness asserts:
    //
    //     due-soon + overdue  ==  what `due-maintenance` returns for the same window
    //     expiring-soon + expired  ==  what `insurance/expiring` returns
    //
    // Renaming rather than changing either behaviour: the lists are right for lists, the counts are
    // right for counts, and the only thing that was wrong was one word.

    /// <summary>Scheduled inside the window and <b>not yet late</b>. Excludes overdue.</summary>
    public int MaintenanceDueSoonCount { get; set; }

    public int MaintenanceOverdueCount { get; set; }
    public int MaintenanceUnscheduledCount { get; set; }

    /// <summary>Cover lapsing inside the window and <b>not yet lapsed</b>. Excludes expired.</summary>
    public int InsuranceExpiringSoonCount { get; set; }

    public int InsuranceExpiredCount { get; set; }
    public int InsuranceUndatedCount { get; set; }
    public int ReturnsOverdueCount { get; set; }
    public int OutstandingSurchargeCount { get; set; }
    public decimal OutstandingSurchargeAmount { get; set; }
}

#endregion

#region Asset reminder engine DTOs — area 16 slice 9

public class AssetReminderRunResultDto
{
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }

    /// <summary>How many candidates were found but already claimed by an earlier sweep.</summary>
    public int AlreadySent { get; set; }
}

/// <summary>
/// One thing a sweep would fire. Carries a reference and a date and nothing sensitive — see the
/// remarks on <c>AssetReminderDispatchLog</c>.
/// </summary>
public class AssetReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid AssetId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>True when a previous sweep already claimed this key, so a real run would skip it.</summary>
    public bool AlreadySent { get; set; }
}

public class AssetReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public Guid? TriggeredByUserId { get; set; }
    public int RemindersQueued { get; set; }
}

public class AssetReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid AssetId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Maintenance-module push DTOs — area 16 slice 9b, decision D10

/// <summary>
/// A row in the Maintenance module's asset register, offered to HR so an HR asset can name its
/// counterpart there. Slice 9b.
/// </summary>
/// <remarks>
/// Mirrors <see cref="FixedAssetPickDto"/> from slice 2b, including the part that matters:
/// already-linked rows are <b>returned and flagged</b>, never filtered out. A picker that silently
/// omits them leaves the user hunting for an asset that is on screen nowhere, with no way to learn
/// that somebody else has already claimed it.
/// </remarks>
public class MaintenanceAssetPickDto
{
    public Guid Id { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public bool AlreadyLinked { get; set; }
    public Guid? LinkedCompanyAssetId { get; set; }
}

/// <summary>Sends an asset to the workshop — slice 9b. AST-1's other half.</summary>
public class SendAssetForMaintenanceDto
{
    /// <summary>What is wrong with it. Required: an admission nobody can read is a lost asset.</summary>
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public AssetMaintenanceType Type { get; set; } = AssetMaintenanceType.Corrective;

    /// <summary>"Scheduled", "Emergency" or "Breakdown" — the Maintenance module's own vocabulary.</summary>
    /// <remarks>
    /// Passed through rather than translated. The other module starts a downtime record for
    /// Emergency and Breakdown and not for Scheduled, so mapping HR's words onto theirs would decide
    /// something about their data that is not HR's to decide.
    /// </remarks>
    [MaxLength(20)]
    public string AdmissionType { get; set; } = "Scheduled";

    [MaxLength(2000)]
    public string? ObservedProblems { get; set; }

    [MaxLength(100)]
    public string? AdmissionLocation { get; set; }

    public DateTime? EstimatedCompletionDate { get; set; }

    /// <summary>What HR expects it to cost, where HR has a figure.</summary>
    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }
}

/// <summary>Links an HR asset to its counterpart in the Maintenance register — slice 9b.</summary>
public class LinkMaintenanceAssetDto
{
    [Required]
    public Guid MaintenanceAssetId { get; set; }
}

#endregion
