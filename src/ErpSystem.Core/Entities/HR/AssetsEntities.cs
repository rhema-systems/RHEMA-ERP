using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Assets;

/// <summary>
/// Definition for different types of assets
/// </summary>
public class AssetType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool HasExtraAttributes { get; set; }

    public virtual ICollection<AssetTypeAttribute> AssetTypeAttributes { get; set; } = new List<AssetTypeAttribute>();
}

/// <summary>
/// Represents extra details specific to an asset type
/// </summary>
public class AssetTypeAttribute : TenantEntity
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

    [ForeignKey("AssetTypeId")]
    public virtual AssetType AssetType { get; set; } = null!;
}

/// <summary>
/// Company asset definition
/// </summary>
public class CompanyAsset : TenantEntity
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

    public Guid AssetTypeId { get; set; }

    // Identification
    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(70)]
    public string? ModelNumber { get; set; }

    [MaxLength(70)]
    public string? SerialNumber { get; set; }

    // Purchase Information
    public DateOnly? PurchaseDate { get; set; }

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

    // Organization Unit
    public Guid? UnitId { get; set; }

    // Assignment
    public bool IsAssignable { get; set; }

    public bool IsCurrentlyAssigned { get; set; }

    public Guid? CurrentAssignedToId { get; set; }

    // Maintenance
    public bool RequiresRegularMaintenance { get; set; }

    public int? MaintenanceIntervalDays { get; set; }

    public DateOnly? LastMaintenanceDate { get; set; }

    public DateOnly? NextMaintenanceDate { get; set; }

    // Insurance
    public bool IsInsured { get; set; }

    [MaxLength(70)]
    public string? InsurancePolicyNumber { get; set; }

    public decimal? InsuredValue { get; set; }

    // Disposal
    public DateOnly? DisposalDate { get; set; }

    public DisposalMethod? DisposalMethod { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }

    [ForeignKey(nameof(AssetTypeId))]
    public virtual AssetType AssetType { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [ForeignKey(nameof(UnitId))]
    public virtual OrganizationUnit? Unit { get; set; }

    [ForeignKey(nameof(CurrentAssignedToId))]
    public virtual Employee? CurrentAssignedTo { get; set; }

    public virtual ICollection<AssetAssignment> AssignmentHistory { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetMaintenance> MaintenanceRecords { get; set; } = new List<AssetMaintenance>();

    public virtual ICollection<AssetAttributeValue> AssetAttributeValues { get; set; } = new List<AssetAttributeValue>();

    public virtual ICollection<AssetImage> Images { get; set; } = new List<AssetImage>();

    public virtual ICollection<AssetAttachment> Attachments { get; set; } = new List<AssetAttachment>();
}

public class AssetAttributeValue : TenantEntity
{
    public Guid AssetId { get; set; }

    public Guid AssetTypeAttributeId { get; set; }

    [MaxLength(700)]
    public string Value { get; set; } = string.Empty;

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(AssetTypeAttributeId))]
    public virtual AssetTypeAttribute AssetTypeAttribute { get; set; } = null!;
}

/// <summary>
/// Asset images for visual identification and documentation
/// </summary>
public class AssetImage : TenantEntity
{
    public Guid AssetId { get; set; }
    
    public string FilePath { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    
    public DateTime UploadDate { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;
}

/// <summary>
/// Asset assignment to employee
/// </summary>
public class AssetAssignment : TenantEntity
{
    [MaxLength(70)]
    public string AssignmentNumber { get; set; } = string.Empty;

    public Guid AssetId { get; set; }

    public Guid EmployeeId { get; set; }

    // Assignment Details
    public DateOnly AssignmentDate { get; set; }
    
    public DateOnly? ExpectedReturnDate { get; set; }
    
    public AssignmentType Type { get; set; } // Permanent, Temporary, Project-based
    
    public AssignmentPurpose Purpose { get; set; }

    [MaxLength(1000)]
    public string? AssignmentNotes { get; set; }
    
    public bool IsPrimaryUser { get; set; }

    // Condition at Assignment
    public HRAssetCondition ConditionAtAssignment { get; set; }
    
    [MaxLength(1000)]
    public string? ConditionNotes { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Acknowledgement
    public bool EmployeeAcknowledged { get; set; }
    
    public DateTime? AcknowledgementDate { get; set; }

    // Terms & Responsibilities
    public bool ResponsibleForLoss { get; set; }
    
    public bool ResponsibleForDamage { get; set; }
    
    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }

    // Return
    public AssignmentStatus Status { get; set; }
    
    public DateTime? ReturnDate { get; set; }
    
    public HRAssetCondition? ConditionAtReturn { get; set; }
    
    [MaxLength(1000)]
    public string? ReturnNotes { get; set; }
    
    public bool ReturnedInGoodCondition { get; set; }

    public Guid? ReturnedToId { get; set; }

    // Damages/Loss
    public bool DamageReported { get; set; }
    
    [MaxLength(1000)]
    public string? DamageDescription { get; set; }
    
    public bool EmployeeLiable { get; set; }
    
    public decimal? RepairCost { get; set; }
    
    public decimal? ReplacementCost { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(ReturnedToId))]
    public virtual Employee? ReturnedTo { get; set; }
}

/// <summary>
/// Asset maintenance record
/// </summary>
public class AssetMaintenance : TenantEntity
{
    [MaxLength(70)]
    public string MaintenanceNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public DateTime MaintenanceDate { get; set; }
    
    public AssetMaintenanceType Type { get; set; }

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

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Cost { get; set; }
    
    public DateTime? NextMaintenanceDate { get; set; }

    public MaintenanceStatus Status { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(PerformedById))]
    public virtual Employee? PerformedBy { get; set; }
}

public class AssetAttachment : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime UploadDate { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;
}

/// <summary>
/// Asset requisition/request from employee
/// </summary>
public class AssetRequisition : TenantEntity
{
    [MaxLength(70)]
    public string RequisitionNumber { get; set; } = string.Empty;

    [Required]
    public Guid RequestedById { get; set; }

    public DateTime RequestDate { get; set; }
    
    [Required]
    public Guid AssetTypeId { get; set; }
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }
    
    public HRAssetRequisitionPriority Priority { get; set; }

    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;
    
    public DateTime? RequiredByDate { get; set; }

    public AssetRequisitionStatus Status { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }
    
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Fulfillment
    public bool IsFulfilled { get; set; }
    
    public DateTime? FulfilledDate { get; set; }
    
    public Guid? FulfilledById { get; set; }

    public Guid? AssignedAssetId { get; set; }

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee RequestedBy { get; set; } = null!;

    [ForeignKey(nameof(AssetTypeId))]
    public virtual AssetType AssetType { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(FulfilledById))]
    public virtual Employee? FulfilledBy { get; set; }

    [ForeignKey(nameof(AssignedAssetId))]
    public virtual CompanyAsset? AssignedAsset { get; set; }
}

/// <summary>
/// Asset transfer between locations/employees
/// </summary>
public class AssetTransfer : TenantEntity
{
    [MaxLength(70)]
    public string TransferNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public DateTime TransferDate { get; set; }
    
    public HRAssetTransferType Type { get; set; } // Employee-to-Employee, Location-to-Location

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

    public Guid InitiatedById { get; set; }

    public HRAssetTransferStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    public DateTime? CompletionDate { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(FromEmployeeId))]
    public virtual Employee? FromEmployee { get; set; }

    [ForeignKey(nameof(FromLocationId))]
    public virtual Location? FromLocation { get; set; }
    
    [ForeignKey(nameof(FromUnitId))]
    public virtual OrganizationUnit? FromUnit { get; set; }

    [ForeignKey(nameof(ToEmployeeId))]
    public virtual Employee? ToEmployee { get; set; }

    [ForeignKey(nameof(ToLocationId))]
    public virtual Location? ToLocation { get; set; }
    
    [ForeignKey(nameof(ToUnitId))]
    public virtual OrganizationUnit? ToUnit { get; set; }

    [ForeignKey(nameof(InitiatedById))]
    public virtual Employee InitiatedBy { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
}
