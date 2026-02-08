using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Checklist Template DTOs

/// <summary>
/// DTO for asset condition checklist template
/// </summary>
public class AssetConditionChecklistTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid AssetCategoryId { get; set; }
    public string AssetCategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public int Version { get; set; }
    public string? VersionNotes { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AssetConditionChecklistItemDto> ChecklistItems { get; set; } = new();
}

/// <summary>
/// DTO for individual checklist item (e.g., Fire Extinguisher, Spare Tire, Fuel Gauge)
/// </summary>
public class AssetConditionChecklistItemDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    /// <summary>
    /// Boolean = Present/Absent, Text = Description, Numeric = Number, Choice = Dropdown
    /// </summary>
    public string ItemType { get; set; } = "Boolean";
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public List<string>? ChoiceOptions { get; set; }
    public string? Unit { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    public bool RequiresPhoto { get; set; }

    /// <summary>
    /// Whether this item can have a repair or replacement action during admission
    /// </summary>
    public bool AllowRepairReplacement { get; set; }

    /// <summary>
    /// Default action when repair/replacement is selected: "None", "Repair", or "Replace"
    /// </summary>
    public string? DefaultRepairReplacementAction { get; set; }

    /// <summary>
    /// Estimated hours for repair action
    /// </summary>
    public double EstimatedRepairHours { get; set; }

    /// <summary>
    /// Estimated hours for replacement action
    /// </summary>
    public double EstimatedReplacementHours { get; set; }
}

public class CreateAssetConditionTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    public Guid AssetCategoryId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    public List<CreateAssetConditionItemDto> ChecklistItems { get; set; } = new();
}

public class UpdateAssetConditionTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    public Guid AssetCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public string? VersionNotes { get; set; }

    public List<UpdateAssetConditionItemDto> ChecklistItems { get; set; } = new();
}

/// <summary>
/// DTO for creating a new checklist item
/// </summary>
public class CreateAssetConditionItemDto
{
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Boolean, Text, Numeric, or Choice
    /// </summary>
    [MaxLength(20)]
    public string ItemType { get; set; } = "Boolean";

    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// For Choice type: list of options (e.g., ["Good", "Fair", "Poor", "Damaged"])
    /// </summary>
    public List<string>? ChoiceOptions { get; set; }

    /// <summary>
    /// For Numeric type: unit of measurement (e.g., "%", "km", "liters")
    /// </summary>
    public string? Unit { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    public bool RequiresPhoto { get; set; } = false;

    /// <summary>
    /// Whether this item can have a repair or replacement action during admission
    /// </summary>
    public bool AllowRepairReplacement { get; set; } = false;

    /// <summary>
    /// Default action when repair/replacement is selected: "None", "Repair", or "Replace"
    /// </summary>
    public string? DefaultRepairReplacementAction { get; set; }

    /// <summary>
    /// Estimated hours for repair action
    /// </summary>
    public double EstimatedRepairHours { get; set; } = 1.0;

    /// <summary>
    /// Estimated hours for replacement action
    /// </summary>
    public double EstimatedReplacementHours { get; set; } = 1.0;
}

public class UpdateAssetConditionItemDto : CreateAssetConditionItemDto
{
    public Guid? Id { get; set; } // null for new items
}

#endregion

#region Asset Condition Record DTOs

/// <summary>
/// DTO for asset condition record (inspection at admission or discharge)
/// </summary>
public class AssetConditionRecordDto
{
    public Guid Id { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Guid InspectorId { get; set; }
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    /// <summary>
    /// Admission or Discharge
    /// </summary>
    public string InspectionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? GeneralNotes { get; set; }
    public Guid? AdmissionId { get; set; }
    public Guid? DischargeId { get; set; }
    public List<string>? PhotoPaths { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AssetConditionItemResultDto> ItemResults { get; set; } = new();
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
}

/// <summary>
/// DTO for individual item result in an asset condition record
/// </summary>
public class AssetConditionItemResultDto
{
    public Guid Id { get; set; }
    public Guid ConditionRecordId { get; set; }
    public Guid ChecklistItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }

    /// <summary>
    /// For Boolean items: Present/Absent
    /// </summary>
    public bool? IsPresent { get; set; }

    /// <summary>
    /// For Text items: condition description
    /// </summary>
    public string? TextValue { get; set; }

    /// <summary>
    /// For Numeric items: the number value
    /// </summary>
    public decimal? NumericValue { get; set; }

    /// <summary>
    /// For Choice items: selected option
    /// </summary>
    public string? SelectedOption { get; set; }

    /// <summary>
    /// Comment for this item
    /// </summary>
    public string? Comment { get; set; }

    public List<string>? PhotoPaths { get; set; }
    public DateTime? InspectedAt { get; set; }

    /// <summary>
    /// Selected repair/replacement action: "None", "Repair", or "Replace"
    /// </summary>
    public string? RepairReplacementAction { get; set; }

    /// <summary>
    /// Whether the repair/replacement task has been created in the work order
    /// </summary>
    public bool TaskCreated { get; set; }

    /// <summary>
    /// Reference to the created work order task (if any)
    /// </summary>
    public Guid? CreatedTaskId { get; set; }

    /// <summary>
    /// Whether this item allows repair/replacement (from checklist item)
    /// </summary>
    public bool AllowRepairReplacement { get; set; }
}

/// <summary>
/// DTO for starting a new asset condition inspection
/// </summary>
public class CreateAssetConditionRecordDto
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Admission or Discharge
    /// </summary>
    [Required]
    public string InspectionType { get; set; } = "Admission";

    /// <summary>
    /// Link to existing admission (required for Discharge type)
    /// </summary>
    public Guid? AdmissionId { get; set; }

    /// <summary>
    /// The ID of the employee who performed the inspection.
    /// If not provided, defaults to the current user.
    /// </summary>
    public Guid? InspectorId { get; set; }

    public string? GeneralNotes { get; set; }
}

/// <summary>
/// DTO for submitting an individual item result
/// </summary>
public class SubmitAssetConditionItemDto
{
    [Required]
    public Guid ChecklistItemId { get; set; }

    /// <summary>
    /// For Boolean items: true = Present, false = Absent
    /// </summary>
    public bool? IsPresent { get; set; }

    /// <summary>
    /// For Text items: condition description
    /// </summary>
    [MaxLength(1000)]
    public string? TextValue { get; set; }

    /// <summary>
    /// For Numeric items: the number value
    /// </summary>
    public decimal? NumericValue { get; set; }

    /// <summary>
    /// For Choice items: selected option
    /// </summary>
    [MaxLength(100)]
    public string? SelectedOption { get; set; }

    /// <summary>
    /// Comment for this item (can be required based on setup)
    /// </summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Selected repair/replacement action: "None", "Repair", or "Replace"
    /// </summary>
    [MaxLength(20)]
    public string? RepairReplacementAction { get; set; }

    /// <summary>
    /// Photo paths for this item
    /// </summary>
    public List<string>? PhotoPaths { get; set; }
}

/// <summary>
/// DTO for completing an asset condition inspection
/// </summary>
public class CompleteAssetConditionRecordDto
{
    public string? GeneralNotes { get; set; }
}

/// <summary>
/// Summary DTO for listing asset condition records
/// </summary>
public class AssetConditionRecordSummaryDto
{
    public Guid Id { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public bool HasAdmission { get; set; }
    public bool HasDischarge { get; set; }
}

#endregion
