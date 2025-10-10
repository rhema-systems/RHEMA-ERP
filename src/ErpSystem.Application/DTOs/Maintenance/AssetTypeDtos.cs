using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Application.DTOs.Maintenance;

public class AssetTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ColorCode { get; set; }
    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileage { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspection { get; set; }
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }
    public int DefaultMaintenanceIntervalDays { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public string? UpdatedByName { get; set; }
    public List<AssetTypeFieldDto> Fields { get; set; } = new();
}

public class CreateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Icon { get; set; }

    [StringLength(7)]
    public string? ColorCode { get; set; }

    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileage { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspection { get; set; }
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }

    [Range(1, 365)]
    public int DefaultMaintenanceIntervalDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public List<CreateAssetTypeFieldDto>? Fields { get; set; }
}

public class UpdateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Icon { get; set; }

    [StringLength(7)]
    public string? ColorCode { get; set; }

    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileage { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspection { get; set; }
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }

    [Range(1, 365)]
    public int DefaultMaintenanceIntervalDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public List<UpdateAssetTypeFieldDto>? Fields { get; set; }
}

public class AssetTypeFieldDto
{
    public Guid Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public Dictionary<string, object>? ValidationRules { get; set; }
    public List<string>? Options { get; set; }
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAssetTypeFieldDto
{
    [Required]
    [StringLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FieldType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? DefaultValue { get; set; }

    public Dictionary<string, object>? ValidationRules { get; set; }

    public List<string>? Options { get; set; }

    [StringLength(500)]
    public string? HelpText { get; set; }

    public bool IsRequired { get; set; }

    [Range(0, 1000)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateAssetTypeFieldDto
{
    public Guid? Id { get; set; } // Null for new fields

    [Required]
    [StringLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FieldType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? DefaultValue { get; set; }

    public Dictionary<string, object>? ValidationRules { get; set; }

    public List<string>? Options { get; set; }

    [StringLength(500)]
    public string? HelpText { get; set; }

    public bool IsRequired { get; set; }

    [Range(0, 1000)]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}