using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

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

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
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

#region Teams

/// <summary>
/// Operational working group within or across the organization.
/// Distinct from <see cref="OrganizationUnit"/>, which represents the formal hierarchy.
/// </summary>
public class Team : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public TeamType TeamType { get; set; } = TeamType.Permanent;

    public TeamStatus Status { get; set; } = TeamStatus.Active;

    /// <summary>
    /// Primary sponsoring / owning org unit. Nullable for cross-functional teams.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>
    /// Team lead / manager (not necessarily the org-unit head).
    /// </summary>
    public Guid? TeamLeadId { get; set; }

    /// <summary>
    /// Optional parent for sub-teams (e.g. "Engineering" → "Platform Squad").
    /// </summary>
    public Guid? ParentTeamId { get; set; }

    /// <summary>
    /// Primary work location for the team, when relevant.
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Default shift for shift-based teams.
    /// </summary>
    public Guid? ShiftId { get; set; }

    [MaxLength(100)]
    public string? CostCenterCode { get; set; }

    /// <summary>
    /// External reference for project teams (e.g. project code).
    /// </summary>
    [MaxLength(50)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? TeamEmail { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Null = open-ended (typical for permanent teams).
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Optional soft cap on membership.
    /// </summary>
    [Range(1, 10000)]
    public int? MaxMembers { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [NotMapped]
    public int MemberCount { get; set; }

    // Navigation
    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(TeamLeadId))]
    public virtual Employee? TeamLead { get; set; }

    [ForeignKey(nameof(ParentTeamId))]
    public virtual Team? ParentTeam { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [ForeignKey(nameof(ShiftId))]
    public virtual ShiftDefinition? Shift { get; set; }

    public virtual ICollection<Team> ChildTeams { get; set; } = new List<Team>();
    public virtual ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}

/// <summary>
/// Links an employee to a team with role, allocation, and effective dates.
/// </summary>
public class TeamMember : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

    /// <summary>
    /// For matrix organizations: % of time allocated to this team (0–100).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal AllocationPercent { get; set; } = 100m;

    /// <summary>
    /// Marks the employee's primary team when they belong to several.
    /// </summary>
    public bool IsPrimary { get; set; }

    public DateOnly JoinDate { get; set; }

    /// <summary>
    /// Null = still an active member.
    /// </summary>
    public DateOnly? LeaveDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Audit trail for team membership changes.
/// </summary>
public class TeamMemberHistory : TenantEntity
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public TeamMemberRole PreviousRole { get; set; }

    public TeamMemberRole NewRole { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [MaxLength(500)]
    public string? ChangeReason { get; set; }

    [ForeignKey(nameof(TeamId))]
    public virtual Team Team { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
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

	/// <summary>Building or campus name.</summary>
	[MaxLength(200)]
	public string? Building { get; set; }

	/// <summary>Floor or level within the building.</summary>
	[MaxLength(50)]
	public string? Floor { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
    public Guid? CountryId { get; set; }
    
    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

	/// <summary>Centre latitude of this station (for map display / geofence anchoring).</summary>
	public double? Latitude { get; set; }

	/// <summary>Centre longitude of this station.</summary>
	public double? Longitude { get; set; }

	/// <summary>FK to the geofence zone that governs valid clock-in range for this station.</summary>
	public Guid? GeofenceZoneId { get; set; }
    
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

    [MaxLength(1000)]
    public string Path { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
	public string? Notes { get; set; }
    
    // Navigation Properties
    [ForeignKey(nameof(StructureId))]
    public virtual LocationStructure Structure { get; set; } = null!;

    [ForeignKey(nameof(LocationLevelId))]
    public virtual LocationLevel LocationLevel { get; set; } = null!;
    
    [ForeignKey(nameof(ParentLocationId))]
    public virtual Location? ParentLocation { get; set; }
    
    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [ForeignKey(nameof(GeofenceZoneId))]
    public virtual GeofenceZone? GeofenceZone { get; set; }
    
    public virtual ICollection<Location> ChildLocations { get; set; } = new List<Location>();
    public virtual ICollection<LocationContact> LocationContacts { get; set; } = new List<LocationContact>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
	public virtual ICollection<StaffAttendanceDevice> Devices { get; set; } = new List<StaffAttendanceDevice>();
	public virtual ICollection<StaffDailyAttendance> AttendanceDays { get; set; } = new List<StaffDailyAttendance>();
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
