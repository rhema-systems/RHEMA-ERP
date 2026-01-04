using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Template for asset condition checklists mapped to specific asset categories.
/// These checklists document the state of an asset during admission and discharge.
/// Used to record items present/absent, their condition, and readings.
/// </summary>
public class PreInspectionChecklistTemplate : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Vehicle, Equipment, Building, etc.

    /// <summary>
    /// The specific asset category this checklist applies to
    /// </summary>
    [Required]
    public Guid AssetCategoryId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    // Version control
    public int Version { get; set; } = 1;

    [MaxLength(1000)]
    public string? VersionNotes { get; set; }

    // Navigation properties
    [ForeignKey("AssetCategoryId")]
    public virtual MaintenanceAssetCategory AssetCategory { get; set; } = null!;

    public virtual ICollection<PreInspectionChecklistItem> ChecklistItems { get; set; } = new List<PreInspectionChecklistItem>();
    public virtual ICollection<AssetConditionRecord> AssetConditionRecords { get; set; } = new List<AssetConditionRecord>();
}

/// <summary>
/// Individual items in an asset condition checklist template.
/// Examples: Fire Extinguisher, Spare Tire, Fuel Gauge, Windscreen, Jack, Tool Kit, etc.
/// </summary>
public class PreInspectionChecklistItem : TenantEntity
{
    [Required]
    public Guid TemplateId { get; set; }

    /// <summary>
    /// The name/label of the item to check (e.g., "Fire Extinguisher", "Spare Tire", "Fuel Level")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping items (e.g., Safety Equipment, Interior, Exterior, Engine Bay, etc.)
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Type of input for this item:
    /// - Boolean: Present/Absent checkbox
    /// - Text: Free text description (e.g., windscreen condition description)
    /// - Numeric: Number input (e.g., fuel gauge %, mileage reading)
    /// - Choice: Dropdown selection from predefined options (e.g., Good/Fair/Poor)
    /// </summary>
    [MaxLength(20)]
    public string ItemType { get; set; } = "Boolean";

    public bool IsRequired { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// For Choice type: JSON array of options e.g., ["Good", "Fair", "Poor", "Damaged"]
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ChoiceOptions { get; set; }

    /// <summary>
    /// For Numeric type: Unit of measurement (e.g., "%", "km", "liters")
    /// </summary>
    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>
    /// For Numeric type: Minimum expected value
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// For Numeric type: Maximum expected value
    /// </summary>
    public decimal? MaxValue { get; set; }

    [MaxLength(500)]
    public string? DefaultValue { get; set; }

    [MaxLength(1000)]
    public string? HelpText { get; set; }

    public bool RequiresPhoto { get; set; } = false;

    // Navigation properties
    [ForeignKey("TemplateId")]
    public virtual PreInspectionChecklistTemplate Template { get; set; } = null!;
}

/// <summary>
/// A completed asset condition inspection record.
/// Records the state of items on the asset during admission or discharge.
/// </summary>
public class AssetConditionRecord : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string InspectionNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid TemplateId { get; set; }

    /// <summary>
    /// The user who performed the inspection (not a foreign key to Employees)
    /// </summary>
    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of inspection: Admission (when customer brings asset) or Discharge (when returning to customer)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string InspectionType { get; set; } = "Admission"; // Admission, Discharge

    [MaxLength(20)]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Cancelled

    [MaxLength(2000)]
    public string? GeneralNotes { get; set; }

    /// <summary>
    /// Link to the admission record
    /// </summary>
    public Guid? AdmissionId { get; set; }

    /// <summary>
    /// Link to the discharge record (for discharge inspections)
    /// </summary>
    public Guid? DischargeId { get; set; }

    // Photos/Documents of overall asset condition
    [Column(TypeName = "nvarchar(max)")]
    public string? PhotoPaths { get; set; } // JSON array of photo paths

    // Navigation properties
    [ForeignKey("AssetId")]
    public virtual MaintenanceAsset Asset { get; set; } = null!;

    [ForeignKey("TemplateId")]
    public virtual PreInspectionChecklistTemplate Template { get; set; } = null!;

    // Note: InspectorId is NOT a foreign key - it stores the user ID directly
    // The inspector name is looked up from the Users table when needed

    [ForeignKey("AdmissionId")]
    public virtual AssetAdmission? Admission { get; set; }

    [ForeignKey("DischargeId")]
    public virtual AssetDischarge? Discharge { get; set; }

    public virtual ICollection<AssetConditionItemResult> ItemResults { get; set; } = new List<AssetConditionItemResult>();
}

/// <summary>
/// Individual item result in an asset condition record.
/// Captures the value (present/absent, text, numeric) and comment for each checklist item.
/// </summary>
public class AssetConditionItemResult : TenantEntity
{
    [Required]
    public Guid ConditionRecordId { get; set; }

    [Required]
    public Guid ChecklistItemId { get; set; }

    /// <summary>
    /// For Boolean items: true = Present, false = Absent
    /// </summary>
    public bool? IsPresent { get; set; }

    /// <summary>
    /// For Text items: Free text description of condition
    /// </summary>
    [MaxLength(1000)]
    public string? TextValue { get; set; }

    /// <summary>
    /// For Numeric items: The numeric value entered (e.g., fuel level %, mileage)
    /// </summary>
    public decimal? NumericValue { get; set; }

    /// <summary>
    /// For Choice items: The selected option (e.g., "Good", "Fair", "Poor")
    /// </summary>
    [MaxLength(100)]
    public string? SelectedOption { get; set; }

    /// <summary>
    /// Comment/remarks for this item - REQUIRED for all items
    /// </summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Photo paths for this specific item (JSON array)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? PhotoPaths { get; set; }

    public DateTime? InspectedAt { get; set; }

    // Navigation properties
    [ForeignKey("ConditionRecordId")]
    public virtual AssetConditionRecord ConditionRecord { get; set; } = null!;

    [ForeignKey("ChecklistItemId")]
    public virtual PreInspectionChecklistItem ChecklistItem { get; set; } = null!;
}
