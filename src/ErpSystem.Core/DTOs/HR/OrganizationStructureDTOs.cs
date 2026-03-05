using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

#region Organization Structure DTOs

/// <summary>
/// Base DTO for reading organization structure data
/// </summary>
public class OrganizationStructureDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new organization structure
/// </summary>
public class CreateOrganizationStructureDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; } = false;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing organization structure
/// </summary>
public class UpdateOrganizationStructureDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Summary DTO for organization structure lookup
/// </summary>
public class OrganizationStructureSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for organization structure with level count
/// </summary>
public class OrganizationStructureDetailDto : OrganizationStructureDto
{
    public int LevelCount { get; set; }
    public int UnitCount { get; set; }
    public List<OrganizationLevelSummaryDto> Levels { get; set; } = new();
}

#endregion

#region Organization Level DTOs

/// <summary>
/// Base DTO for reading organization level data
/// </summary>
public class OrganizationLevelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LevelNumber { get; set; }
    public bool IsRootLevel { get; set; }
    public bool RequiresHead { get; set; }
    public bool AllowsDirectEmployees { get; set; }
    public bool IsLocked { get; set; }
    public bool IsActive { get; set; }
    public Guid StructureId { get; set; }
    public string? StructureName { get; set; }
}

/// <summary>
/// DTO for creating a new organization level
/// </summary>
public class CreateOrganizationLevelDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 100)]
    public int LevelNumber { get; set; }

    public bool RequiresHead { get; set; } = true;

    public bool AllowsDirectEmployees { get; set; } = true;

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid StructureId { get; set; }
}

/// <summary>
/// DTO for updating an existing organization level
/// </summary>
public class UpdateOrganizationLevelDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 100)]
    public int LevelNumber { get; set; }

    public bool RequiresHead { get; set; }

    public bool AllowsDirectEmployees { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public Guid StructureId { get; set; }
}

/// <summary>
/// Summary DTO for organization level lookup
/// </summary>
public class OrganizationLevelSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int LevelNumber { get; set; }
    public bool IsRootLevel { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for organization level with unit count
/// </summary>
public class OrganizationLevelDetailDto : OrganizationLevelDto
{
    public int UnitCount { get; set; }
}

#endregion

#region Organization Unit DTOs

/// <summary>
/// Base DTO for reading organization unit data
/// </summary>
public class OrganizationUnitDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public string? Description { get; set; }
    public Guid OrganizationLevelId { get; set; }
    public string? LevelName { get; set; }
    public Guid? ParentUnitId { get; set; }
    public string? ParentUnitName { get; set; }
    public Guid? HeadEmployeeId { get; set; }
    public string? HeadEmployeeName { get; set; }
    public int Sequence { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new organization unit
/// </summary>
public class CreateOrganizationUnitDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AccountCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    public Guid? ParentUnitId { get; set; }

    public Guid? HeadEmployeeId { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing organization unit
/// </summary>
public class UpdateOrganizationUnitDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AccountCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    public Guid? ParentUnitId { get; set; }

    public Guid? HeadEmployeeId { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Summary DTO for organization unit lookup
/// </summary>
public class OrganizationUnitSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid OrganizationLevelId { get; set; }
    public string? LevelName { get; set; }
    public Guid? ParentUnitId { get; set; }
    public string? ParentUnitName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for organization unit with hierarchy information
/// </summary>
public class OrganizationUnitDetailDto : OrganizationUnitDto
{
    public string HierarchyPath { get; set; } = string.Empty;
    public int ChildCount { get; set; }
    public int EmployeeCount { get; set; }
    public int Depth { get; set; }
}

/// <summary>
/// Tree-friendly DTO for organization unit hierarchy visualization
/// </summary>
public class OrganizationUnitTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid OrganizationLevelId { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public Guid? ParentUnitId { get; set; }
    public Guid? HeadEmployeeId { get; set; }
    public string? HeadEmployeeName { get; set; }
    public int Sequence { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasChildren { get; set; }
    public int Depth { get; set; }
    public List<OrganizationUnitTreeDto> Children { get; set; } = new();
}

/// <summary>
/// Hierarchy DTO for organization unit with full path and metadata
/// </summary>
public class OrganizationUnitHierarchyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid OrganizationLevelId { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public int LevelNumber { get; set; }
    public Guid? ParentUnitId { get; set; }
    public string? ParentUnitName { get; set; }
    public Guid? HeadEmployeeId { get; set; }
    public string? HeadEmployeeName { get; set; }
    public string Path { get; set; } = string.Empty;
    public string HierarchyPath { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int Depth { get; set; }
    public bool HasChildren { get; set; }
    public int ChildCount { get; set; }
    public int EmployeeCount { get; set; }
    public bool IsActive { get; set; }
    public List<OrganizationUnitHierarchyDto> Children { get; set; } = new();
}

#endregion

#region Organization Unit History DTOs

/// <summary>
/// Base DTO for reading organization unit history (read-only)
/// </summary>
public class OrganizationUnitHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PreviousParentId { get; set; }
    public string? PreviousParentName { get; set; }
    public Guid? NewParentId { get; set; }
    public string? NewParentName { get; set; }
    public Guid? PreviousHeadEmployeeId { get; set; }
    public string? PreviousHeadEmployeeName { get; set; }
    public Guid? NewHeadEmployeeId { get; set; }
    public string? NewHeadEmployeeName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? ChangeReason { get; set; }
}

/// <summary>
/// Summary DTO for organization unit history
/// </summary>
public class OrganizationUnitHistorySummaryDto
{
    public Guid Id { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? ChangeReason { get; set; }
}

/// <summary>
/// Detailed DTO for organization unit history with full change tracking
/// </summary>
public class OrganizationUnitHistoryDetailDto : OrganizationUnitHistoryDto
{
    public string ChangeType { get; set; } = string.Empty; // "Restructure", "Leadership Change", etc.
    public string? ChangedBy { get; set; }
    public DateTime? ChangedAt { get; set; }
}

#endregion

#region Location Structure DTOs

/// <summary>
/// Base DTO for reading location structure data
/// </summary>
public class LocationStructureDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new location structure
/// </summary>
public class CreateLocationStructureDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; } = false;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing location structure
/// </summary>
public class UpdateLocationStructureDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Summary DTO for location structure lookup
/// </summary>
public class LocationStructureSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for location structure with level count
/// </summary>
public class LocationStructureDetailDto : LocationStructureDto
{
    public int LevelCount { get; set; }
    public int LocationCount { get; set; }
    public List<LocationLevelSummaryDto> Levels { get; set; } = new();
}

#endregion

#region Location Level DTOs

/// <summary>
/// Base DTO for reading location level data
/// </summary>
public class LocationLevelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LevelNumber { get; set; }
    public bool RequiresAddress { get; set; }
    public bool RequiresContactInfo { get; set; }
    public bool AllowsEmployeeAssignment { get; set; }
    public bool IsRootLevel { get; set; }
    public bool IsLocked { get; set; }
    public bool IsActive { get; set; }
    public Guid StructureId { get; set; }
    public string? StructureName { get; set; }
}

/// <summary>
/// DTO for creating a new location level
/// </summary>
public class CreateLocationLevelDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int LevelNumber { get; set; }

    public bool RequiresAddress { get; set; } = false;

    public bool RequiresContactInfo { get; set; } = false;

    public bool AllowsEmployeeAssignment { get; set; } = false;

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid StructureId { get; set; }
}

/// <summary>
/// DTO for updating an existing location level
/// </summary>
public class UpdateLocationLevelDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public int LevelNumber { get; set; }

    public bool RequiresAddress { get; set; }

    public bool RequiresContactInfo { get; set; }

    public bool AllowsEmployeeAssignment { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public Guid StructureId { get; set; }
}

/// <summary>
/// Summary DTO for location level lookup
/// </summary>
public class LocationLevelSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int LevelNumber { get; set; }
    public bool IsRootLevel { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for location level with location count
/// </summary>
public class LocationLevelDetailDto : LocationLevelDto
{
    public int LocationCount { get; set; }
}

#endregion

#region Location DTOs

/// <summary>
/// Base DTO for reading location data
/// </summary>
public class LocationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid StructureId { get; set; }
    public string? StructureName { get; set; }
    public Guid LocationLevelId { get; set; }
    public string? LevelName { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string? ParentLocationName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? FaxNumber { get; set; }
    public int Sequence { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new location
/// </summary>
public class CreateLocationDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid StructureId { get; set; }

    [Required]
    public Guid LocationLevelId { get; set; }

    public Guid? ParentLocationId { get; set; }

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(50)]
    public string? FaxNumber { get; set; }

    public int Sequence { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing location
/// </summary>
public class UpdateLocationDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid StructureId { get; set; }

    [Required]
    public Guid LocationLevelId { get; set; }

    public Guid? ParentLocationId { get; set; }

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(50)]
    public string? FaxNumber { get; set; }

    public int Sequence { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Summary DTO for location lookup
/// </summary>
public class LocationSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? LevelName { get; set; }
    public string? City { get; set; }
    public string? CountryName { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed DTO for location with full address and contact information
/// </summary>
public class LocationDetailDto : LocationDto
{
    public int ChildLocationCount { get; set; }
    public int EmployeeCount { get; set; }
    public int ContactCount { get; set; }
    public List<LocationContactSummaryDto> Contacts { get; set; } = new();
}

/// <summary>
/// Tree-friendly DTO for location hierarchy visualization
/// </summary>
public class LocationTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid LocationLevelId { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public Guid? ParentLocationId { get; set; }
    public string? City { get; set; }
    public string? CountryName { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public bool IsActive { get; set; }
    public bool IsLeaf { get; set; }
    public int Depth { get; set; }
    public List<LocationTreeDto> Children { get; set; } = new();
}

/// <summary>
/// Hierarchy DTO for location with full path and metadata
/// </summary>
public class LocationHierarchyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid LocationLevelId { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public int LevelNumber { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string? ParentLocationName { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int Depth { get; set; }
    public bool IsLeaf { get; set; }
    public int ChildLocationCount { get; set; }
    public int EmployeeCount { get; set; }
    public bool IsActive { get; set; }
    public List<LocationHierarchyDto> Children { get; set; } = new();
}

#endregion

#region Location Contact DTOs

/// <summary>
/// Base DTO for reading location contact data
/// </summary>
public class LocationContactDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// DTO for creating a new location contact
/// </summary>
public class CreateLocationContactDto : CreateDtoBase
{
    [Required]
    public Guid LocationId { get; set; }

    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsPrimary { get; set; } = false;
}

/// <summary>
/// DTO for updating an existing location contact
/// </summary>
public class UpdateLocationContactDto : UpdateDtoBase
{
    [Required]
    public Guid LocationId { get; set; }

    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsPrimary { get; set; }
}

/// <summary>
/// Summary DTO for location contact lookup
/// </summary>
public class LocationContactSummaryDto
{
    public Guid Id { get; set; }
    public string? ContactName { get; set; }
    public string? EmployeeName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Detailed DTO for location contact with full information
/// </summary>
public class LocationContactDetailDto : LocationContactDto
{
    public string? EmployeeCode { get; set; }
    public string? EmployeeDepartment { get; set; }
}

#endregion
