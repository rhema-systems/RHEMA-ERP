using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class CompanyAssetListDto
{
    public Guid Id { get; set; }
    public string AssetNumber { get; set; }
    public string AssetTag { get; set; }
    public string AssetName { get; set; }
    public AssetCategory Category { get; set; }
    public string CategoryName { get; set; }
    public CompanyAssetType AssetType { get; set; }
    public string AssetTypeName { get; set; }
    public AssetStatus Status { get; set; }
    public string StatusName { get; set; }
    public bool IsCurrentlyAssigned { get; set; }
    public string CurrentAssignedToName { get; set; }
    public string Location { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public bool IsWarrantyExpired { get; set; }
}

// Detail DTO
public class CompanyAssetDetailDto
{
    public Guid Id { get; set; }
    public string AssetNumber { get; set; }
    public string AssetTag { get; set; }

    // Basic Info
    public string AssetName { get; set; }
    public string Description { get; set; }
    public AssetCategory Category { get; set; }
    public string CategoryName { get; set; }
    public CompanyAssetType AssetType { get; set; }
    public string AssetTypeName { get; set; }

    // Manufacturer Details
    public string Manufacturer { get; set; }
    public string ModelNumber { get; set; }
    public string SerialNumber { get; set; }
    public string Imei { get; set; }

    // Purchase Details
    public DateTime? PurchaseDate { get; set; }
    public string Supplier { get; set; }
    public string InvoiceNumber { get; set; }
    public decimal? PurchaseCost { get; set; }

    // Warranty
    public bool HasWarranty { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public string WarrantyProvider { get; set; }

    // Physical Characteristics
    public string Color { get; set; }
    public string Size { get; set; }
    public AssetCondition Condition { get; set; }
    public string ConditionName { get; set; }
    public string Specifications { get; set; }

    // Location
    public Guid? StationId { get; set; }
    public string StationName { get; set; }
    public string LocationDetails { get; set; }

    // Assignment
    public AssetStatus Status { get; set; }
    public string StatusName { get; set; }
    public bool IsCurrentlyAssigned { get; set; }
    public Guid? CurrentAssignedToId { get; set; }
    public string CurrentAssignedToName { get; set; }
    public DateTime? CurrentAssignmentDate { get; set; }

    // Financial
    public decimal? CurrentValue { get; set; }
    public decimal? SalvageValue { get; set; }
    public int? UsefulLifeYears { get; set; }

    // Insurance
    public bool IsInsured { get; set; }
    public string InsurancePolicyNumber { get; set; }
    public decimal? InsuredValue { get; set; }

    // Disposal
    public bool IsDisposed { get; set; }
    public DateTime? DisposalDate { get; set; }
    public DisposalMethod? DisposalMethod { get; set; }
    public string DisposalMethodName { get; set; }
    public string DisposalNotes { get; set; }

    // Collections
    public List<AssetAssignmentListDto> AssignmentHistory { get; set; }
    public List<AssetMaintenanceDto> MaintenanceRecords { get; set; }
    public List<AssetAttachmentDto> Attachments { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateCompanyAssetDto
{
    public string AssetTag { get; set; }
    public string AssetName { get; set; }
    public string Description { get; set; }
    public AssetCategory Category { get; set; }
    public CompanyAssetType AssetType { get; set; }
    public string Manufacturer { get; set; }
    public string ModelNumber { get; set; }
    public string SerialNumber { get; set; }
    public string Imei { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string Supplier { get; set; }
    public string InvoiceNumber { get; set; }
    public decimal? PurchaseCost { get; set; }
    public bool HasWarranty { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public string WarrantyProvider { get; set; }
    public string Color { get; set; }
    public string Size { get; set; }
    public AssetCondition Condition { get; set; }
    public string Specifications { get; set; }
    public Guid? StationId { get; set; }
    public string LocationDetails { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? SalvageValue { get; set; }
    public int? UsefulLifeYears { get; set; }
    public bool IsInsured { get; set; }
    public string InsurancePolicyNumber { get; set; }
    public decimal? InsuredValue { get; set; }
}

// Update DTO
public class UpdateCompanyAssetDto
{
    public Guid Id { get; set; }
    public string AssetName { get; set; }
    public string Description { get; set; }
    public AssetCondition Condition { get; set; }
    public string Specifications { get; set; }
    public string LocationDetails { get; set; }
    public decimal? CurrentValue { get; set; }
    public bool IsInsured { get; set; }
    public string InsurancePolicyNumber { get; set; }
    public decimal? InsuredValue { get; set; }
}

// Asset Assignment DTOs
public class AssetAssignmentListDto
{
    public Guid Id { get; set; }
    public string AssignmentNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public string AssignmentTypeName { get; set; }
    public DateTime AssignmentDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public AssignmentStatus Status { get; set; }
    public string StatusName { get; set; }
}

public class AssetAssignmentDetailDto
{
    public Guid Id { get; set; }
    public string AssignmentNumber { get; set; }

    // Asset Info
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; }
    public string AssetName { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }

    // Assignment Details
    public AssignmentType AssignmentType { get; set; }
    public string AssignmentTypeName { get; set; }
    public AssignmentPurpose Purpose { get; set; }
    public string PurposeName { get; set; }
    public DateTime AssignmentDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string AssignmentNotes { get; set; }

    // Condition at Assignment
    public AssetCondition ConditionAtAssignment { get; set; }
    public string ConditionAtAssignmentName { get; set; }
    public string ConditionNotes { get; set; }

    // Terms & Conditions
    public string TermsAndConditions { get; set; }
    public bool EmployeeAcceptedTerms { get; set; }
    public DateTime? EmployeeAcceptanceDate { get; set; }
    public string EmployeeSignature { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Return Details
    public DateTime? ActualReturnDate { get; set; }
    public Guid? ReturnedToId { get; set; }
    public string ReturnedToName { get; set; }
    public AssetCondition? ConditionAtReturn { get; set; }
    public string ConditionAtReturnName { get; set; }
    public string ReturnNotes { get; set; }
    public bool IsDamaged { get; set; }
    public string DamageDescription { get; set; }
    public decimal? RepairCost { get; set; }
    public bool IsLost { get; set; }
    public decimal? ReplacementCost { get; set; }

    // Status
    public AssignmentStatus Status { get; set; }
    public string StatusName { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AssignAssetDto
{
    public Guid AssetId { get; set; }
    public Guid EmployeeId { get; set; }
    public AssignmentType AssignmentType { get; set; }
    public AssignmentPurpose Purpose { get; set; }
    public DateTime AssignmentDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string AssignmentNotes { get; set; }
    public string TermsAndConditions { get; set; }
}

public class ReturnAssetDto
{
    public Guid Id { get; set; }
    public DateTime ActualReturnDate { get; set; }
    public AssetCondition ConditionAtReturn { get; set; }
    public string ReturnNotes { get; set; }
    public bool IsDamaged { get; set; }
    public string DamageDescription { get; set; }
    public decimal? RepairCost { get; set; }
}

// Maintenance DTOs
public class AssetMaintenanceDto
{
    public Guid Id { get; set; }
    public string MaintenanceNumber { get; set; }
    public AssetMaintenanceType MaintenanceType { get; set; }
    public string MaintenanceTypeName { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string Description { get; set; }
    public string WorkPerformed { get; set; }
    public decimal? Cost { get; set; }
    public MaintenanceStatus Status { get; set; }
    public string StatusName { get; set; }
    public string PerformedByName { get; set; }
}

public class CreateAssetMaintenanceDto
{
    public Guid AssetId { get; set; }
    public AssetMaintenanceType MaintenanceType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string Description { get; set; }
    public Guid? PerformedById { get; set; }
    public string ExternalServiceProvider { get; set; }
}

// Attachment DTO
public class AssetAttachmentDto
{
    public Guid Id { get; set; }
    public AssetAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Dashboard DTO
public class AssetDashboardDto
{
    public int TotalAssets { get; set; }
    public int AssignedAssets { get; set; }
    public int AvailableAssets { get; set; }
    public int InMaintenanceAssets { get; set; }
    public int DamagedAssets { get; set; }
    public decimal TotalAssetValue { get; set; }
    public int WarrantiesExpiringThisMonth { get; set; }
    public int OverdueReturns { get; set; }
    public Dictionary<AssetCategory, int> AssetsByCategory { get; set; }
    public Dictionary<AssetStatus, int> AssetsByStatus { get; set; }
    public List<CompanyAssetListDto> RecentlyAssigned { get; set; }
    public List<AssetMaintenanceDto> UpcomingMaintenance { get; set; }
}