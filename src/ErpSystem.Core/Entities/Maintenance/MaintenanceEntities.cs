using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Maintenance;

#region Core Asset Management

public class MaintenanceAsset : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AssetNumber { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetCategoryId { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public int? Year { get; set; }

    public AssetOwnershipType OwnershipType { get; set; } = AssetOwnershipType.Owned;

    public Guid? EmployeeId { get; set; }

    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CurrentValue { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    // Building-specific location details
    [MaxLength(100)]
    public string? Building { get; set; }

    [MaxLength(100)]
    public string? Floor { get; set; }

    [MaxLength(100)]
    public string? Room { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Active;

    public AssetCriticality Criticality { get; set; } = AssetCriticality.Medium;

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }

    [MaxLength(200)]
    public string? WarrantyProvider { get; set; }

    // Parent-child relationship for asset hierarchy
    public Guid? ParentAssetId { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Specifications { get; set; } // JSON string

    [Column(TypeName = "nvarchar(max)")]
    public string? DocumentLinks { get; set; } // JSON array of document URLs/paths

    [Column(TypeName = "nvarchar(max)")]
    public string? Images { get; set; } // JSON array of image URLs/paths

    // Operating metrics
    public double? OperatingHours { get; set; }
    public DateTime? LastOperatingHoursUpdate { get; set; }

    // Vehicle-specific metrics
    public double? Mileage { get; set; }
    public DateTime? LastMileageUpdate { get; set; }

    [MaxLength(50)]
    public string? LicensePlate { get; set; }

    [MaxLength(50)]
    public string? VIN { get; set; } // Vehicle Identification Number

    [MaxLength(20)]
    public string? FuelType { get; set; } // Petrol, Diesel, Electric, Hybrid

    /// <summary>
    /// When true, this asset is included in Fleet Management screens/flows.
    /// Vehicle-category assets are not automatically included unless explicitly flagged.
    /// </summary>
    public bool IsFleetAsset { get; set; } = false;

    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDue { get; set; }

    // Building-specific metrics
    public double? FloorArea { get; set; } // Square feet/meters

    [MaxLength(50)]
    public string? EnergyRating { get; set; }

    // Equipment-specific metrics
    public double? Capacity { get; set; }

    [MaxLength(50)]
    public string? CapacityUnit { get; set; }

    public double? PowerRating { get; set; } // kW, HP, etc.

    [MaxLength(20)]
    public string? PowerUnit { get; set; }

    // Navigation properties
    public virtual MaintenanceAssetCategory AssetCategory { get; set; } = null!;
    public virtual Employee? Employee { get; set; } // Asset custodian/responsible employee
    public virtual MaintenanceAsset? ParentAsset { get; set; }
    public virtual ICollection<MaintenanceAsset> ChildAssets { get; set; } = new List<MaintenanceAsset>();
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public virtual ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public virtual ICollection<AssetInspection> Inspections { get; set; } = new List<AssetInspection>();
    public virtual ICollection<AssetDowntime> Downtimes { get; set; } = new List<AssetDowntime>();
}

public class MaintenanceAssetCategory : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;

    // Asset Type Classification
    [MaxLength(50)]
    public string? AssetType { get; set; } // "Equipment", "Vehicle", "Building", "Infrastructure", etc.

    // Parent-child relationship
    public Guid? ParentCategoryId { get; set; }
    public virtual MaintenanceAssetCategory? ParentCategory { get; set; }
    public virtual ICollection<MaintenanceAssetCategory> ChildCategories { get; set; } = new List<MaintenanceAssetCategory>();

    // Maintenance Schedule Configuration
    [MaxLength(20)]
    public string MaintenanceScheduleType { get; set; } = "single"; // 'single' or 'multi'

    /// <summary>
    /// Controls whether maintenance schedules should be auto-generated for
    /// newly created assets in this category based on the category template.
    /// </summary>
    public bool AutoGenerateSchedules { get; set; } = true;

    // Primary Maintenance Criteria
    [MaxLength(20)]
    public string MaintenanceType { get; set; } = "Time"; // 'Time', 'Distance', 'Usage', 'Cycles'

    [MaxLength(50)]
    public string? MaintenanceFrequency { get; set; } // For time-based: 'Monthly', 'Quarterly', etc.

    public double? MaintenanceValue { get; set; } // For non-time-based: 5000, 250, etc.

    [MaxLength(20)]
    public string? MaintenanceUnit { get; set; } // 'km', 'miles', 'hours', 'cycles', etc.

    // Secondary Maintenance Criteria (for multi-criteria schedules)
    [MaxLength(20)]
    public string? SecondaryMaintenanceType { get; set; } // 'Time', 'Distance', 'Usage', 'Cycles'

    [MaxLength(50)]
    public string? SecondaryMaintenanceFrequency { get; set; } // For time-based secondary criteria

    public double? SecondaryMaintenanceValue { get; set; } // For non-time-based secondary criteria

    [MaxLength(20)]
    public string? SecondaryMaintenanceUnit { get; set; } // Unit for secondary criteria

    // Navigation properties
    public virtual ICollection<MaintenanceAsset> Assets { get; set; } = new List<MaintenanceAsset>();
}

/// <summary>
/// Configurable asset types that define how different assets should be treated
/// </summary>
public class AssetType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Vehicle", "Building", "Equipment"

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // e.g., "VEH", "BLD", "EQP"

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;

    // Asset Type Configuration
    public bool RequiresLocation { get; set; } = true;
    public bool RequiresOperatingHours { get; set; } = false;
    public bool RequiresMileageTracking { get; set; } = false;
    public bool RequiresLicensing { get; set; } = false;
    public bool RequiresInspections { get; set; } = false;
    public bool SupportsHierarchy { get; set; } = false; // Can have child assets
    public bool RequiresSpecializedFields { get; set; } = false;

    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; } = 90;
    public bool RequiresPreventiveMaintenance { get; set; } = true;
    public bool RequiresConditionMonitoring { get; set; } = false;

    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; } = false;
    public bool RequiresLockoutTagout { get; set; } = false;
    public bool RequiresPermits { get; set; } = false;

    // Workflow Configuration
    public int DefaultWorkOrderPriority { get; set; } = 3; // 1=Critical, 5=Low
    public double DefaultEstimatedHours { get; set; } = 2.0;

    [MaxLength(2000)]
    public string? DefaultWorkInstructions { get; set; }

    // Custom Fields Configuration (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string? CustomFieldsConfig { get; set; }

    // Navigation properties
    public virtual ICollection<AssetTypeField> CustomFields { get; set; } = new List<AssetTypeField>();
}

/// <summary>
/// Custom fields configuration for asset types
/// </summary>
public class AssetTypeField : TenantEntity
{
    [Required]
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string FieldType { get; set; } = "Text"; // Text, Number, Date, Boolean, Dropdown, etc.

    public bool IsRequired { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    [MaxLength(1000)]
    public string? ValidationRules { get; set; } // JSON for validation rules

    [MaxLength(2000)]
    public string? Options { get; set; } // JSON for dropdown options

    [MaxLength(500)]
    public string? DefaultValue { get; set; }

    [MaxLength(1000)]
    public string? HelpText { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual AssetType AssetType { get; set; } = null!;
}

#endregion

#region Asset Admission and Discharge

/// <summary>
/// Tracks when an asset is admitted for maintenance
/// </summary>
public class AssetAdmission : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string AdmissionNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime AdmissionDate { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid AdmittedById { get; set; }

    [MaxLength(20)]
    public string AdmissionType { get; set; } = "Scheduled"; // Scheduled, Emergency, Breakdown

    [MaxLength(20)]
    public string AssetConditionOnAdmission { get; set; } = "Unknown"; // Excellent, Good, Fair, Poor, Critical

    [MaxLength(2000)]
    public string? AdmissionNotes { get; set; }

    [MaxLength(2000)]
    public string? ObservedProblems { get; set; }

    // Asset readings at admission
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }

    // Admission checklist (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string? AdmissionChecklist { get; set; }

    // Photos and documents at admission
    [Column(TypeName = "nvarchar(max)")]
    public string? PhotoPaths { get; set; } // JSON array of photo file paths

    [Column(TypeName = "nvarchar(max)")]
    public string? DocumentPaths { get; set; } // JSON array of document file paths

    // Location tracking
    [MaxLength(100)]
    public string? AdmissionLocation { get; set; }

    [MaxLength(100)]
    public string? BayOrStation { get; set; }

    // Expected completion
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime? EstimatedDischargeDate { get; set; }

    // Status
    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Completed, Cancelled

    // Link to discharge
    public Guid? DischargeId { get; set; }

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual JobCard? JobCard { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual ApplicationUser AdmittedBy { get; set; } = null!;
    public virtual AssetDischarge? Discharge { get; set; }
}

/// <summary>
/// Tracks when an asset is discharged after maintenance completion
/// </summary>
public class AssetDischarge : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string DischargeNumber { get; set; } = string.Empty;

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime DischargeDate { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid DischargedById { get; set; }

    [MaxLength(20)]
    public string AssetConditionOnDischarge { get; set; } = "Good"; // Excellent, Good, Fair, Poor, Critical

    [MaxLength(2000)]
    public string? DischargeNotes { get; set; }

    [MaxLength(2000)]
    public string? WorkCompleted { get; set; }

    [MaxLength(2000)]
    public string? RemainingIssues { get; set; }

    // Asset readings at discharge
    public decimal? MileageReading { get; set; }
    public decimal? HoursReading { get; set; }
    public decimal? FuelLevel { get; set; }

    // Quality control
    public bool QualityCheckPassed { get; set; } = false;
    public Guid? QualityCheckedById { get; set; }
    public DateTime? QualityCheckDate { get; set; }

    [MaxLength(1000)]
    public string? QualityCheckNotes { get; set; }

    // Discharge checklist (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string? DischargeChecklist { get; set; }

    // Final photos and documents
    [Column(TypeName = "nvarchar(max)")]
    public string? PhotoPaths { get; set; } // JSON array of photo file paths

    [Column(TypeName = "nvarchar(max)")]
    public string? DocumentPaths { get; set; } // JSON array of document file paths

    // Completion certificate
    public bool CertificateGenerated { get; set; } = false;
    public DateTime? CertificateGeneratedDate { get; set; }

    [MaxLength(500)]
    public string? CertificatePath { get; set; }

    // Customer/user acceptance
    public bool CustomerAcceptance { get; set; } = false;
    public Guid? AcceptedById { get; set; }
    public DateTime? AcceptedDate { get; set; }

    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }

    // Follow-up requirements
    public bool RequiresFollowUp { get; set; } = false;
    public DateTime? FollowUpDate { get; set; }

    [MaxLength(1000)]
    public string? FollowUpInstructions { get; set; }

    // Warranty information
    public int WarrantyDays { get; set; } = 0;
    public DateTime? WarrantyExpiration { get; set; }

    [MaxLength(1000)]
    public string? WarrantyTerms { get; set; }

    // Navigation properties
    public virtual AssetAdmission Admission { get; set; } = null!;
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual JobCard? JobCard { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual ApplicationUser DischargedBy { get; set; } = null!;
    public virtual ApplicationUser? QualityCheckedBy { get; set; }
    public virtual ApplicationUser? AcceptedBy { get; set; }
    public virtual ICollection<MaintenanceCertificate> Certificates { get; set; } = new List<MaintenanceCertificate>();
}

/// <summary>
/// Maintenance completion certificates
/// </summary>
public class MaintenanceCertificate : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string CertificateNumber { get; set; } = string.Empty;

    [Required]
    public Guid DischargeId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CertificateType { get; set; } = "Maintenance Completion"; // Maintenance Completion, Safety Inspection, Quality Assurance

    [Required]
    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ValidUntil { get; set; }

    [Required]
    public Guid IssuedById { get; set; }

    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(100)]
    public string FileFormat { get; set; } = "PDF";

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Certificate content (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string CertificateData { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual AssetDischarge Discharge { get; set; } = null!;
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual ApplicationUser IssuedBy { get; set; } = null!;
}

/// <summary>
/// Calculated view for asset downtime tracking
/// </summary>
public class AssetMaintenanceDowntime : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    public Guid? DischargeId { get; set; }

    public Guid? JobCardId { get; set; }
    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime DowntimeStart { get; set; }

    public DateTime? DowntimeEnd { get; set; }

    // Calculated downtime in minutes
    public int DowntimeMinutes { get; set; } = 0;

    // Downtime categorization
    [MaxLength(50)]
    public string DowntimeType { get; set; } = "Maintenance"; // Maintenance, Repair, Inspection, Breakdown

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    // Cost impact
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCostImpact { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCostImpact { get; set; } = 0;

    // Status
    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Completed, Cancelled

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual AssetAdmission Admission { get; set; } = null!;
    public virtual AssetDischarge? Discharge { get; set; }
    public virtual JobCard? JobCard { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
}

#endregion

#region Work Order Details

public class WorkOrder : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Optional link to originating job card
    public Guid? JobCardId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Assigned, InProgress, OnHold, Completed, Cancelled, Closed

    // Maintenance location classification
    [MaxLength(20)]
    public string MaintenanceLocation { get; set; } = "Internal"; // Internal, External, Onsite, Offsite

    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician for this work order
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }

    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCost { get; set; } = 0;

    /// <summary>
    /// Billing type for the work order: "Maintenance" uses fixed amount, "Repairs" uses itemized costs
    /// </summary>
    [MaxLength(20)]
    public string BillingType { get; set; } = "Repairs"; // Maintenance, Repairs

    /// <summary>
    /// Fixed billing amount for Maintenance billing type (copied from MaintenanceType.FixedAmount)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal FixedAmount { get; set; } = 0;

    public double EstimatedHours { get; set; } = 0;
    public double ActualHours { get; set; } = 0;

    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? SupervisorId { get; set; }
    public Guid? CompletedById { get; set; }
    public Guid? QualityCheckedById { get; set; }
    public Guid? ContractorId { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }

    [MaxLength(50)]
    public string? FailureCode { get; set; }

    [MaxLength(50)]
    public string? CauseCode { get; set; }

    [MaxLength(50)]
    public string? ActionCode { get; set; }

    // Safety and compliance
    [MaxLength(500)]
    public string? SafetyRequirements { get; set; }

    public bool RequiresPermit { get; set; } = false;
    public bool RequiresLockout { get; set; } = false;
    public bool RequiresConfinedSpaceEntry { get; set; } = false;

    // Related work orders
    public Guid? ParentWorkOrderId { get; set; }

    // Recurring work order tracking
    public Guid? MaintenanceScheduleId { get; set; }
    public bool IsRecurring { get; set; } = false;

    [Column(TypeName = "nvarchar(max)")]
    public string? CustomFields { get; set; } // JSON string for additional fields

    // Dynamic custom field values based on AssetType configuration
    [Column(TypeName = "nvarchar(max)")]
    public string? CustomFieldValues { get; set; } // JSON object with field values

    // Navigation properties
    public virtual JobCard? JobCard { get; set; }
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual WorkOrderType WorkOrderType { get; set; } = null!;
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;
    public virtual PriorityLevel PriorityLevel { get; set; } = null!;
    public virtual Employee AssignedTechnician { get; set; } = null!;
    public virtual TechnicianTeam? AssignedTeam { get; set; }
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? Supervisor { get; set; }
    public virtual ApplicationUser? CompletedBy { get; set; }
    public virtual ApplicationUser? QualityCheckedBy { get; set; }
    public virtual WorkOrder? ParentWorkOrder { get; set; }
    public virtual MaintenanceSchedule? MaintenanceSchedule { get; set; }
    public virtual MaintenanceContractor? Contractor { get; set; }
    public virtual ContractorWorkOrder? ContractorWorkOrder { get; set; }
    public virtual ICollection<WorkOrder> ChildWorkOrders { get; set; } = new List<WorkOrder>();
    public virtual ICollection<WorkOrderTask> Tasks { get; set; } = new List<WorkOrderTask>();
    public virtual ICollection<WorkOrderPart> Parts { get; set; } = new List<WorkOrderPart>();
    public virtual ICollection<WorkOrderLabor> Labor { get; set; } = new List<WorkOrderLabor>();
    public virtual ICollection<WorkOrderTool> Tools { get; set; } = new List<WorkOrderTool>();
    public virtual ICollection<WorkOrderDocument> Documents { get; set; } = new List<WorkOrderDocument>();
    public virtual ICollection<WorkOrderComment> Comments { get; set; } = new List<WorkOrderComment>();
    public virtual ICollection<WorkOrderQualityCheck> QualityChecks { get; set; } = new List<WorkOrderQualityCheck>();
}

public class WorkOrderType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; } = false;
    public int DefaultPriority { get; set; } = 3; // 1=Critical, 2=High, 3=Medium, 4=Low

    // Navigation properties
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class MaintenanceType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    // Enhanced categorization
    [MaxLength(30)]
    public string Category { get; set; } = "Scheduled"; // Scheduled, Emergency, Preventive, Corrective, Routine, Inspection, Overhaul

    [MaxLength(30)]
    public string MaintenanceClass { get; set; } = "Routine"; // Routine, Emergency, Preventive, Corrective, Predictive

    // Internal vs External maintenance classification
    [MaxLength(20)]
    public string Location { get; set; } = "Internal"; // Internal, External, Onsite, Offsite

    // Condition-based triggers
    public bool IsConditionBased { get; set; } = false;
    public bool IsUsageBased { get; set; } = false;
    public bool IsTimeBased { get; set; } = true;

    // Usage-based criteria
    public decimal? MileageTrigger { get; set; } // Trigger maintenance at X miles/km
    public decimal? HoursTrigger { get; set; } // Trigger maintenance at X operating hours
    public decimal? CycleTrigger { get; set; } // Trigger maintenance at X cycles

    // Condition-based criteria (JSON for complex conditions)
    [Column(TypeName = "nvarchar(max)")]
    public string? ConditionCriteria { get; set; } // JSON: temperature, vibration, oil analysis, etc.

    // Time-based criteria
    public int? FrequencyDays { get; set; } // Frequency in days
    public int? FrequencyWeeks { get; set; } // Alternative: frequency in weeks
    public int? FrequencyMonths { get; set; } // Alternative: frequency in months

    // Asset type associations
    [Column(TypeName = "nvarchar(max)")]
    public string? ApplicableAssetTypes { get; set; } // JSON array of asset type IDs

    // Skill and resource requirements
    [Column(TypeName = "nvarchar(max)")]
    public string? RequiredSkills { get; set; } // JSON array of required skills

    [Column(TypeName = "nvarchar(max)")]
    public string? RequiredTools { get; set; } // JSON array of required tools

    // Safety and compliance
    public bool RequiresSafetyPermit { get; set; } = false;
    public bool RequiresShutdown { get; set; } = false;
    public bool RequiresSpecialTraining { get; set; } = false;

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    // Approval requirements
    public bool RequiresApproval { get; set; } = false;
    public int ApprovalLevels { get; set; } = 1;

    // Cost and time estimates
    public double EstimatedHours { get; set; } = 0;
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; } = 0;

    /// <summary>
    /// Fixed billing amount for maintenance-type work orders.
    /// When work order billing type is "Maintenance", this amount is used instead of itemized costs.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal FixedAmount { get; set; } = 0;

    // Priority and criticality
    public int DefaultPriority { get; set; } = 3; // 1=Critical, 2=High, 3=Medium, 4=Low, 5=Deferred

    [MaxLength(20)]
    public string Criticality { get; set; } = "Medium"; // Critical, High, Medium, Low

    // Planning parameters
    public int LeadTimeDays { get; set; } = 0; // Days needed to plan/prepare
    public int DowntimeMinutes { get; set; } = 0; // Expected downtime

    // Quality control
    public bool RequiresQualityCheck { get; set; } = false;
    public bool RequiresDocumentation { get; set; } = true;
    public bool RequiresCertification { get; set; } = false;

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Template and checklist
    [Column(TypeName = "nvarchar(max)")]
    public string? TaskTemplate { get; set; } // JSON: default tasks for this maintenance type

    [Column(TypeName = "nvarchar(max)")]
    public string? ChecklistTemplate { get; set; } // JSON: default checklist items

    [Column(TypeName = "nvarchar(max)")]
    public string? PartsTemplate { get; set; } // JSON: commonly used parts

    // Maintenance intervals and scheduling
    [Column(TypeName = "nvarchar(max)")]
    public string? SchedulingRules { get; set; } // JSON: complex scheduling rules

    // Performance tracking
    public double AverageCompletionHours { get; set; } = 0;
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageCost { get; set; } = 0;
    public DateTime? LastPerformanceUpdate { get; set; }

    // Navigation properties
    public virtual ICollection<JobCard> JobCards { get; set; } = new List<JobCard>();
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public virtual ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
}

public class PriorityLevel : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Level { get; set; } // 1=Highest priority, higher numbers = lower priority

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; } = "#6B7280"; // Default gray

    public int ResponseTimeHours { get; set; } = 24; // Expected response time in hours

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

#endregion

#region Job Cards and Work Orders

/// <summary>
/// Represents a maintenance request/job card that goes through approval workflow
/// before becoming a work order
/// Note: This inherits from TenantEntity instead of ApprovableEntity to get TenantId
/// and manually includes approval workflow properties
/// </summary>
public class JobCard : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string JobCardNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? ProblemDescription { get; set; }

    // Maintenance location classification
    [MaxLength(20)]
    public string MaintenanceLocation { get; set; } = "Internal"; // Internal, External, Onsite, Offsite

    // Request details
    public Guid RequestedById { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredCompletionDate { get; set; }

    // Estimated resources
    public double EstimatedHours { get; set; } = 0;
    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; } = 0;

    // Assignment (can be pre-assigned or assigned during approval)
    /// <summary>
    /// ID of the user (ApplicationUser) preferred as technician
    /// </summary>
    public Guid? PreferredTechnicianId { get; set; }
    public Guid? PreferredTeamId { get; set; }
    public Guid? ContractorId { get; set; } // For external maintenance

    // Job card specific flags
    public bool RequiresSpecialTools { get; set; } = false;
    public bool RequiresShutdown { get; set; } = false;
    public bool RequiresSafetyPermit { get; set; } = false;

    [MaxLength(1000)]
    public string? SpecialInstructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    // Approval workflow tracking
    [MaxLength(20)]
    public string JobCardStatus { get; set; } = "Draft"; // Draft, Submitted, UnderReview, Approved, Rejected, Cancelled

    /// <summary>
    /// Current approval status
    /// </summary>
    [MaxLength(50)]
    public string ApprovalStatus { get; set; } = "Draft";

    /// <summary>
    /// User who submitted for approval
    /// </summary>
    public Guid? SubmittedById { get; set; }

    /// <summary>
    /// Date submitted for approval
    /// </summary>
    public DateTime? SubmittedDate { get; set; }

    /// <summary>
    /// User who approved
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Date approved
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Approval comments
    /// </summary>
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    /// <summary>
    /// Workflow instance ID if using workflow engine
    /// </summary>
    public Guid? WorkflowInstanceId { get; set; }

    // Planning details (filled during approval)
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician during approval
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    // Work order generation
    public Guid? GeneratedWorkOrderId { get; set; }
    public DateTime? WorkOrderGeneratedAt { get; set; }

    // Asset Admission (replaces AssetAdmission table)
    [MaxLength(20)]
    public string? AssetConditionOnAdmission { get; set; } // Excellent, Good, Fair, Poor, Critical

    public decimal? MileageReadingOnAdmission { get; set; }
    public decimal? HoursReadingOnAdmission { get; set; }
    public decimal? FuelLevelOnAdmission { get; set; }

    [MaxLength(2000)]
    public string? AdmissionNotes { get; set; }

    [MaxLength(100)]
    public string? BayOrStation { get; set; }

    // Job Card Completion (replaces AssetDischarge table)
    public DateTime? CompletedDate { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    [MaxLength(20)]
    public string? AssetConditionOnCompletion { get; set; } // Excellent, Good, Fair, Poor, Critical

    public decimal? MileageReadingOnCompletion { get; set; }
    public decimal? HoursReadingOnCompletion { get; set; }
    public decimal? FuelLevelOnCompletion { get; set; }

    [MaxLength(2000)]
    public string? WorkCompletedSummary { get; set; }

    [MaxLength(2000)]
    public string? RemainingIssues { get; set; }

    // Quality Control
    public bool QualityCheckPassed { get; set; } = false;
    public Guid? QualityCheckedById { get; set; }
    public DateTime? QualityCheckDate { get; set; }

    [MaxLength(2000)]
    public string? QualityCheckNotes { get; set; }

    // Certificate Generation
    public bool CertificateGenerated { get; set; } = false;
    public DateTime? CertificateGeneratedDate { get; set; }

    // Customer/User Acceptance
    public bool CustomerAcceptance { get; set; } = false;
    public Guid? AcceptedById { get; set; }
    public DateTime? AcceptedDate { get; set; }

    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }

    // Follow-up
    public bool RequiresFollowUp { get; set; } = false;
    public DateTime? FollowUpDate { get; set; }

    [MaxLength(1000)]
    public string? FollowUpInstructions { get; set; }

    // Warranty
    public int WarrantyDays { get; set; } = 0;
    public DateTime? WarrantyExpiration { get; set; }

    [MaxLength(1000)]
    public string? WarrantyTerms { get; set; }

    // Document attachments
    [Column(TypeName = "nvarchar(max)")]
    public string? AttachmentPaths { get; set; } // JSON array of file paths

    // Custom fields for different asset types
    [Column(TypeName = "nvarchar(max)")]
    public string? CustomFieldValues { get; set; } // JSON object with field values

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;
    public virtual PriorityLevel PriorityLevel { get; set; } = null!;
    public virtual Employee RequestedBy { get; set; } = null!;
    public virtual Employee? PreferredTechnician { get; set; }
    public virtual TechnicianTeam? PreferredTeam { get; set; }
    public virtual Employee? AssignedTechnician { get; set; }
    public virtual TechnicianTeam? AssignedTeam { get; set; }
    public virtual MaintenanceContractor? Contractor { get; set; }
    public virtual Employee? QualityCheckedBy { get; set; }
    public virtual Employee? AcceptedBy { get; set; }
    public virtual WorkOrder? GeneratedWorkOrder { get; set; }
    public virtual ICollection<JobCardComment> Comments { get; set; } = new List<JobCardComment>();
    public virtual ICollection<JobCardDocument> Documents { get; set; } = new List<JobCardDocument>();
    public virtual ICollection<JobCardApprovalStep> ApprovalSteps { get; set; } = new List<JobCardApprovalStep>();
    public virtual ICollection<JobCardCertificate> Certificates { get; set; } = new List<JobCardCertificate>();
}

/// <summary>
/// Comments/notes on job cards during approval process
/// </summary>
public class JobCardComment : TenantEntity
{
    [Required]
    public Guid JobCardId { get; set; }

    [Required]
    public Guid CommentById { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CommentType { get; set; } = "General"; // General, Question, Concern, Approval, Rejection

    public bool IsInternal { get; set; } = true; // Internal vs visible to requestor

    public DateTime CommentDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual JobCard JobCard { get; set; } = null!;
    public virtual Employee CommentBy { get; set; } = null!;
}

/// <summary>
/// Document attachments for job cards
/// </summary>
public class JobCardDocument : TenantEntity
{
    [Required]
    public Guid JobCardId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    [MaxLength(50)]
    public string DocumentType { get; set; } = "General"; // General, Photo, Drawing, Manual, Report

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid UploadedById { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual JobCard JobCard { get; set; } = null!;
    public virtual Employee UploadedBy { get; set; } = null!;
}

/// <summary>
/// Multi-step approval workflow for job cards
/// </summary>
public class JobCardApprovalStep : TenantEntity
{
    [Required]
    public Guid JobCardId { get; set; }

    [Required]
    public int StepOrder { get; set; }

    [Required]
    [MaxLength(100)]
    public string StepName { get; set; } = string.Empty;

    [Required]
    public Guid ApproverId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Skipped

    public DateTime? ActionDate { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    public bool IsRequired { get; set; } = true;

    // Navigation properties
    public virtual JobCard JobCard { get; set; } = null!;
    public virtual Employee Approver { get; set; } = null!;
}

/// <summary>
/// Certificates generated for completed job cards
/// </summary>
public class JobCardCertificate : TenantEntity
{
    [Required]
    public Guid JobCardId { get; set; }

    [Required]
    [MaxLength(50)]
    public string CertificateNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CertificateType { get; set; } = "Maintenance Completion"; // Maintenance Completion, Safety Inspection, Quality Assurance

    [Required]
    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ValidUntil { get; set; }

    [Required]
    public Guid IssuedById { get; set; }

    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(100)]
    public string FileFormat { get; set; } = "PDF";

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Certificate content (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string CertificateData { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual JobCard JobCard { get; set; } = null!;
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual Employee IssuedBy { get; set; } = null!;
}

public class WorkOrderTask : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int Sequence { get; set; } = 1;

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Skipped

    public double EstimatedHours { get; set; } = 0;
    public double ActualHours { get; set; } = 0;

    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician for this task
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }

    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Path to the photo uploaded for this task (e.g., before/after photos)
    /// </summary>
    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual Employee? AssignedTechnician { get; set; }
}

public class WorkOrderPart : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    // Reference to Inventory Module
    [Required]
    public Guid InventoryItemId { get; set; }

    // Cached values from inventory item (for performance and history)
    [MaxLength(100)]
    public string ItemCode { get; set; } = string.Empty; // From InventoryItem.ItemCode

    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty; // From InventoryItem.Name

    [MaxLength(1000)]
    public string? Description { get; set; } // From InventoryItem.Description

    // Quantities
    [Required]
    public decimal QuantityRequired { get; set; } = 1;

    public decimal QuantityAllocated { get; set; } = 0; // From inventory allocation
    public decimal QuantityUsed { get; set; } = 0;
    public decimal QuantityReturned { get; set; } = 0;

    // Costing (captured at time of work order)
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalCost { get; set; } = 0;

    // Location and tracking
    public Guid? WarehouseLocationId { get; set; } // Where the part should be picked from

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    // Status tracking
    [MaxLength(20)]
    public string Status { get; set; } = "Required"; // Required, Allocated, Picked, Used, Returned

    // Allocation tracking
    public Guid? AllocationId { get; set; } // Link to InventoryAllocation

    public DateTime? AllocatedAt { get; set; }
    public DateTime? PickedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;

    // References to Inventory Module
    // Note: Navigation properties are defined here but DbContext configuration
    // will handle the relationships to avoid circular dependencies
    [ForeignKey("InventoryItemId")]
    public virtual InventoryItem? InventoryItem { get; set; }

    [ForeignKey("WarehouseLocationId")]
    public virtual WarehouseLocation? WarehouseLocation { get; set; }

    [ForeignKey("AllocationId")]
    public virtual InventoryAllocation? Allocation { get; set; }
}

public class WorkOrderLabor : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    /// <summary>
    /// ID of the employee (HR) who performed the labor
    /// </summary>
    [Required]
    public Guid TechnicianId { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public double Hours { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; } = 0;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(50)]
    public string? LaborType { get; set; } = "Regular"; // Regular, Overtime, Emergency

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;

    // NOTE: Technicians are now Employee-driven. We intentionally do not maintain an EF FK relationship here
    // to allow a clean transition from previously user-based TechnicianId values.
}

public class WorkOrderDocument : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FileType { get; set; }

    public long FileSize { get; set; } = 0;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string DocumentType { get; set; } = "General"; // General, Photo, Manual, Drawing, Report

    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual Employee UploadedBy { get; set; } = null!;
}

public class WorkOrderComment : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [MaxLength(20)]
    public string CommentType { get; set; } = "General"; // General, StatusUpdate, Issue, Resolution

    public bool IsInternal { get; set; } = true;

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual Employee User { get; set; } = null!;
}

/// <summary>
/// Predefined task templates for specific assets and maintenance types
/// These templates are used to automatically generate work order tasks
/// </summary>
public class AssetTaskTemplate : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int Sequence { get; set; } = 1;

    public double EstimatedHours { get; set; } = 0;

    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician template default
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    [MaxLength(1000)]
    public string? RequiredTools { get; set; }

    [MaxLength(1000)]
    public string? RequiredParts { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;
    public virtual Employee? AssignedTechnician { get; set; }
}

/// <summary>
/// Generic task templates for asset types and maintenance types
/// Used as fallback when asset-specific templates don't exist
/// </summary>
public class AssetTypeTaskTemplate : TenantEntity
{
    [Required]
    public Guid AssetTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int Sequence { get; set; } = 1;

    public double EstimatedHours { get; set; } = 0;

    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician template default
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    [MaxLength(1000)]
    public string? RequiredTools { get; set; }

    [MaxLength(1000)]
    public string? RequiredParts { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual AssetType AssetType { get; set; } = null!;
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;
    public virtual Employee? AssignedTechnician { get; set; }
}

/// <summary>
/// Default task templates for maintenance types (system-wide fallback)
/// </summary>
public class MaintenanceTaskTemplate : TenantEntity
{
    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int Sequence { get; set; } = 1;

    public double EstimatedHours { get; set; } = 0;

    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// ID of the user (ApplicationUser) assigned as technician template default
    /// </summary>
    public Guid? AssignedTechnicianId { get; set; }

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    [MaxLength(1000)]
    public string? SafetyRequirements { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual MaintenanceType MaintenanceType { get; set; } = null!;
    public virtual Employee? AssignedTechnician { get; set; }
}

#endregion

#region Maintenance Scheduling

// MaintenanceSchedule class is defined in separate MaintenanceSchedule.cs file

#endregion

#region Inspection Management

public class InspectionTemplate : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [Required]
    [MaxLength(50)]
    public string Frequency { get; set; } = "AdHoc"; // Daily, Weekly, Monthly, Quarterly, Yearly, AdHoc

    [MaxLength(50)]
    public string InspectionType { get; set; } = "General"; // Safety, Quality, Regulatory, Maintenance, Fleet

    public int EstimatedDuration { get; set; } = 60; // minutes

    public bool RequiresSignature { get; set; } = false;
    public bool AllowPhotos { get; set; } = false;

    [MaxLength(10)]
    public string Version { get; set; } = "1.0";

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    [Column(TypeName = "nvarchar(max)")]
    public string AssetTypes { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string InspectorRoles { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "nvarchar(max)")]
    public string ChecklistItems { get; set; } = "[]"; // JSON array of inspection items

    // Navigation properties
    public virtual ICollection<AssetInspection> Inspections { get; set; } = new List<AssetInspection>();
}

public class AssetInspection : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid InspectionTemplateId { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Status { get; set; } = "InProgress"; // Scheduled, InProgress, Completed, Failed

    [MaxLength(20)]
    public string? OverallResult { get; set; } // Pass, Fail, ConditionalPass

    [Column(TypeName = "nvarchar(max)")]
    public string InspectionData { get; set; } = "{}"; // JSON object with inspection results

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDue { get; set; }

    // Compliance tracking
    public bool IsRegulatoryRequired { get; set; } = false;
    [MaxLength(100)]
    public string? RegulatoryStandard { get; set; }

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual InspectionTemplate InspectionTemplate { get; set; } = null!;
    public virtual Employee Inspector { get; set; } = null!;
    public virtual ICollection<InspectionDocument> Documents { get; set; } = new List<InspectionDocument>();
}

public class InspectionDocument : TenantEntity
{
    [Required]
    public Guid InspectionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FileType { get; set; }

    public long FileSize { get; set; } = 0;

    [MaxLength(50)]
    public string DocumentType { get; set; } = "Photo"; // Photo, Document, Report, Certificate

    [MaxLength(1000)]
    public string? Description { get; set; }

    // Navigation properties
    public virtual AssetInspection Inspection { get; set; } = null!;
}

#endregion

#region Resource Management

public class TechnicianTeam : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? TeamLeaderId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Inactive

    // Navigation properties
    public virtual Employee? TeamLeader { get; set; }
    public virtual ICollection<TechnicianTeamMember> Members { get; set; } = new List<TechnicianTeamMember>();
    public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class TechnicianTeamMember : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [MaxLength(50)]
    public string Role { get; set; } = "Member"; // Leader, Member, Trainee

    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LeftDate { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual TechnicianTeam Team { get; set; } = null!;
    // TechnicianId now references ApplicationUser (Users table) instead of Employee
}

public class TechnicianSkill : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<UserTechnicianSkill> UserSkills { get; set; } = new List<UserTechnicianSkill>();
}

public class UserTechnicianSkill : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public int ProficiencyLevel { get; set; } = 1; // 1=Beginner, 2=Intermediate, 3=Advanced, 4=Expert

    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }

    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    [MaxLength(100)]
    public string? CertificationNumber { get; set; }

    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual TechnicianSkill Skill { get; set; } = null!;
}

#endregion

#region Technician Scheduling

public class TechnicianSchedule : TenantEntity
{
    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string ScheduleType { get; set; } = "WorkOrder"; // WorkOrder, Shift, Training, Leave, Meeting

    public Guid? WorkOrderId { get; set; }
    public Guid? ShiftId { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium"; // Critical, High, Medium, Low

    public bool IsAllDay { get; set; } = false;
    public bool IsRecurring { get; set; } = false;

    [MaxLength(100)]
    public string? RecurrencePattern { get; set; } // RRULE format

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Estimated vs actual time tracking
    public double EstimatedHours { get; set; } = 0;
    public double ActualHours { get; set; } = 0;

    // Navigation properties
    // TechnicianId now references ApplicationUser (Users table) instead of Employee
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual TechnicianShift? Shift { get; set; }
}

public class TechnicianAvailability : TenantEntity
{
    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string AvailabilityType { get; set; } = "Available"; // Available, Unavailable, PartiallyAvailable

    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = "WorkingHours"; // WorkingHours, Vacation, Sick, Training, Meeting, Personal

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsRecurring { get; set; } = false;

    [MaxLength(100)]
    public string? RecurrencePattern { get; set; } // For regular working hours, etc.

    // For partial availability
    public double? AvailableHours { get; set; }
    public double? CapacityPercentage { get; set; } = 100;

    // Navigation properties
    // TechnicianId now references ApplicationUser (Users table) instead of Employee
}

public class TechnicianShift : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;

    // Days of week (JSON array of day numbers: 0=Sunday, 1=Monday, etc.)
    [Column(TypeName = "nvarchar(20)")]
    public string DaysOfWeek { get; set; } = "[1,2,3,4,5]"; // Monday-Friday default

    public double ScheduledHours { get; set; } = 8.0;

    // Break times
    public double BreakMinutes { get; set; } = 60; // Total break time in minutes

    // Navigation properties
    public virtual ICollection<TechnicianSchedule> Schedules { get; set; } = new List<TechnicianSchedule>();
}

#endregion

#region Asset Downtime Tracking

public class AssetDowntime : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    public Guid? WorkOrderId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    public double? DowntimeHours { get; set; }

    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty; // Breakdown, Maintenance, Inspection, Other

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCostImpact { get; set; } = 0;

    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Resolved

    // Navigation properties
    public virtual MaintenanceAsset Asset { get; set; } = null!;
    public virtual WorkOrder? WorkOrder { get; set; }
}

#endregion

#region Safety Compliance Tracking

public class SafetyComplianceRecord : TenantEntity
{
    [Required]
    public Guid SafetyProtocolId { get; set; }

    // Alias for compatibility with services
    public Guid ProtocolId
    {
        get => SafetyProtocolId;
        set => SafetyProtocolId = value;
    }

    [Required]
    public Guid TechnicianId { get; set; }

    public Guid? WorkOrderId { get; set; }

    [Required]
    public DateTime ComplianceDate { get; set; }

    // Alias for compatibility with services
    public DateTime CheckDate
    {
        get => ComplianceDate;
        set => ComplianceDate = value;
    }

    [Required]
    [MaxLength(20)]
    public string ComplianceStatus { get; set; } = string.Empty; // Compliant, NonCompliant, PartialCompliance

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsCompliant { get; set; }

    [MaxLength(50)]
    public string? ViolationType { get; set; }

    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }

    public DateTime? CorrectiveActionDueDate { get; set; }

    // Additional properties for service compatibility
    [Column(TypeName = "nvarchar(max)")]
    public string? ChecklistItems { get; set; } // JSON serialized list of checklist items

    [Column(TypeName = "nvarchar(max)")]
    public string? Violations { get; set; } // JSON serialized list of violations

    public Guid? InspectorId { get; set; }

    [MaxLength(2000)]
    public string? InspectorNotes { get; set; }

    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedDate { get; set; }

    // Navigation properties
    public virtual SafetyProtocol SafetyProtocol { get; set; } = null!;
    public virtual Employee Technician { get; set; } = null!;
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual Employee? Inspector { get; set; }
    public virtual Employee? VerifiedBy { get; set; }
}

#endregion
