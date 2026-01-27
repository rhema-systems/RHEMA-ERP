using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.HR;

#region Organizational Structure

/// <summary>
/// Defines the organizational structure template with configurable levels
/// </summary>
public class OrganizationStructure : TenantEntity
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

    public virtual ICollection<OrganizationLevel> Levels { get; set; } = new List<OrganizationLevel>();
}

/// <summary>
/// Defines a single level in the organization hierarchy
/// </summary>
public class OrganizationLevel : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Division", "Department"

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty; // e.g., "DIV", "DEPT"

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Hierarchical level number (1 = highest, e.g., Board of Directors)
    /// </summary>
    [Required]
    [Range(1, 100)]
    public int LevelNumber { get; set; }

    /// <summary>
    /// Indicates if this is the root level (no parent possible)
    /// Computed dynamically from the minimum LevelNumber among active levels in the same structure
    /// </summary>
    [NotMapped]
    public bool IsRootLevel
    {
        get
        {
            if (OrganizationStructure?.Levels == null || !OrganizationStructure.Levels.Any())
                return false;
            
            var minLevelNumber = OrganizationStructure.Levels
                .Where(l => l.IsActive && !l.IsDeleted)
                .Min(l => (int?)l.LevelNumber);
            
            return minLevelNumber.HasValue && LevelNumber == minLevelNumber.Value;
        }
    }

    /// <summary>
    /// Whether units at this level require a head/leader
    /// </summary>
    public bool RequiresHead { get; set; } = true;

    /// <summary>
    /// Whether units at this level can have direct employee assignments
    /// </summary>
    public bool AllowsDirectEmployees { get; set; } = true;

    public bool IsLocked { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid StructureId { get; set; }

    [ForeignKey(nameof(StructureId))]
    public virtual OrganizationStructure OrganizationStructure { get; set; } = null!;

    public virtual ICollection<OrganizationUnit> OrganizationUnits { get; set; } = new List<OrganizationUnit>();
}

/// <summary>
/// Represents actual organizational units (instances of OrganizationLevel)
/// </summary>
public class OrganizationUnit : TenantEntity
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

    /// <summary>
    /// References the OrganizationLevel (e.g., Division, Department)
    /// </summary>
    [Required]
    public Guid OrganizationLevelId { get; set; }

    /// <summary>
    /// Parent organizational unit (null for root level units)
    /// </summary>
    public Guid? ParentUnitId { get; set; }

    /// <summary>
    /// Head of this organizational unit
    /// </summary>
    public Guid? HeadEmployeeId { get; set; }

    /// <summary>
    /// Sequence within parent (for ordering)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Sequence { get; set; } = 1;

    public string Path { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [NotMapped]
    public string HierarchyPath { get; set; } = string.Empty;

    [NotMapped]
    public int ChildCount { get; set; }

    [NotMapped]
    public int EmployeeCount { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel OrganizationLevel { get; set; } = null!;

    [ForeignKey(nameof(ParentUnitId))]
    public virtual OrganizationUnit? ParentUnit { get; set; }

    [ForeignKey(nameof(HeadEmployeeId))]
    public virtual Employee? HeadEmployee { get; set; }

    // Self-referencing for hierarchy
    public virtual ICollection<OrganizationUnit> ChildUnits { get; set; } = new List<OrganizationUnit>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<EmployeePosition> Positions { get; set; } = new List<EmployeePosition>();
}

/// <summary>
/// Tracks changes to organizational structure over time
/// </summary>
public class OrganizationUnitHistory : TenantEntity
{
    public Guid OrganizationUnitId { get; set; }
    
    public Guid? PreviousParentId { get; set; }
    
    public Guid? NewParentId { get; set; }
    
    public Guid? PreviousHeadEmployeeId { get; set; }
    
    public Guid? NewHeadEmployeeId { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    
    public DateOnly? EffectiveTo { get; set; }
    
    [MaxLength(500)]
    public string? ChangeReason { get; set; }
    
    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit OrganizationUnit { get; set; } = null!;
}

#endregion

#region Location Structure

/// <summary>
/// Defines the geographical hierarchy template for workstations
/// </summary>
public class LocationStructure : TenantEntity
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
    
    public virtual ICollection<LocationLevel> LocationLevels { get; set; } = new List<LocationLevel>();
    
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}

/// <summary>
/// Defines the configurable layers for location hierarchy
/// </summary>
public class LocationLevel : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Country", "Region", "Office"
    
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Hierarchical level (1 = highest, e.g., Continent)
    /// </summary>
    [Required]
    public int LevelNumber { get; set; }

    /// <summary>
    /// Indicates whether locations at this level should capture address information
    /// </summary>
    public bool RequiresAddress { get; set; } = false;

    /// <summary>
    /// Indicates whether locations at this level should capture contact information
    /// (phone, email, website, contacts)
    /// </summary>
    public bool RequiresContactInfo  { get; set; } = false;

    /// <summary>
    /// Whether locations at this level can be assigned to employees
    /// </summary>
    public bool AllowsEmployeeAssignment { get; set; } = false;

    /// <summary>
    /// Indicates if this is the root level (no parent possible)
    /// Computed dynamically from the minimum LevelNumber among active levels in the same structure
    /// </summary>
    [NotMapped]
    public bool IsRootLevel
    {
        get
        {
            if (Structure?.LocationLevels == null || !Structure.LocationLevels.Any())
                return false;
            
            var minLevelNumber = Structure.LocationLevels
                .Where(l => l.IsActive && !l.IsDeleted)
                .Min(l => (int?)l.LevelNumber);
            
            return minLevelNumber.HasValue && LevelNumber == minLevelNumber.Value;
        }
    }

    public bool IsLocked { get; set; }
    
    public bool IsActive { get; set; } = true;

    [Required]
    public Guid StructureId { get; set; }

    [ForeignKey(nameof(StructureId))]
    public virtual LocationStructure Structure { get; set; } = null!;

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}

/// <summary>
/// Represents actual locations (instances of LocationLevel)
/// </summary>
public class Location : TenantEntity
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

    /// <summary>
    /// References the LocationLevel (e.g., Country, Region, Office)
    /// </summary>
    [Required]
    public Guid LocationLevelId { get; set; }
    
    /// <summary>
    /// Parent location (null for root level like Continent)
    /// </summary>
    public Guid? ParentLocationId { get; set; }
    
    // Address Information
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
    
    // Contact Information
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(50)]
    public string? FaxNumber { get; set; }

    public int Sequence { get; set; } = 1;

    public string Path { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    [ForeignKey(nameof(StructureId))]
    public virtual LocationStructure Structure { get; set; } = null!;

    [ForeignKey(nameof(LocationLevelId))]
    public virtual LocationLevel LocationLevel { get; set; } = null!;
    
    [ForeignKey(nameof(ParentLocationId))]
    public virtual Location? ParentLocation { get; set; }
    
    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }
    
    public virtual ICollection<Location> ChildLocations { get; set; } = new List<Location>();
    public virtual ICollection<LocationContact> LocationContacts { get; set; } = new List<LocationContact>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

/// <summary>
/// Manages multiple contacts for a location
/// </summary>
public class LocationContact : TenantEntity
{
    public Guid LocationId { get; set; }
    
    public Guid? EmployeeId { get; set; }
    
    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(100)]
    public string? JobTitle { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    public bool IsPrimary { get; set; } = false;
    
    [ForeignKey(nameof(LocationId))]
    public virtual Location Location { get; set; } = null!;
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }
}

#endregion

#region Organization Chart

/// <summary>
/// Organizational chart node
/// </summary>
public class OrganizationChartNode : TenantEntity
{
    public Guid? PositionId { get; set; }
    public EmployeePosition? Position { get; set; }

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid? ParentNodeId { get; set; }
    public OrganizationChartNode? ParentNode { get; set; }

    public int Level { get; set; }
    public string? CustomLabel { get; set; }
    public bool IsVacant { get; set; }

    public ICollection<OrganizationChartNode> ChildNodes { get; set; } = new List<OrganizationChartNode>();
}

#endregion
