using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Reference;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Administrative geography — shared reference data DTOs.
//  See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
//
//  ⚠ Deliberately not deriving from ErpSystem.Core.DTOs.HR.BaseDto. This module is shared; taking
//  a base type from HR would make every other consumer depend on HR's DTO namespace to read a
//  region. The four audit fields are repeated instead — a small cost for a boundary that holds.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Audit fields every geography DTO carries.</summary>
public abstract class ReferenceDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

#region GeoScheme

/// <summary>A country's way of dividing itself up.</summary>
public class GeoSchemeDto : ReferenceDto
{
    public Guid CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }

    /// <summary>How many tiers this scheme defines — the depth of its address form.</summary>
    public int LevelCount { get; set; }

    /// <summary>How many areas have been loaded against it. Zero means the seed never ran.</summary>
    public int AreaCount { get; set; }
}

/// <summary>A scheme together with its tiers — what an address form needs to render itself.</summary>
public class GeoSchemeDetailDto : GeoSchemeDto
{
    public List<GeoLevelDto> Levels { get; set; } = new();
}

public class CreateGeoSchemeDto
{
    [Required]
    public Guid CountryId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Setting this unsets whichever scheme currently holds the default for the same country,
    /// rather than being refused — a country has exactly one default and the caller is telling us
    /// which it is.
    /// </summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateGeoSchemeDto : CreateGeoSchemeDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region GeoLevel

/// <summary>One tier of a scheme. <see cref="Name"/> is the label the address form prints.</summary>
public class GeoLevelDto : ReferenceDto
{
    public Guid SchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LevelNumber { get; set; }
    public bool IsRequiredInAddress { get; set; }
    public bool AllowsAddressAssignment { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Areas loaded at this tier — so a screen can warn before retiring it.</summary>
    public int AreaCount { get; set; }
}

public class CreateGeoLevelDto
{
    [Required]
    public Guid SchemeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1, 20)]
    public int LevelNumber { get; set; }

    public bool IsRequiredInAddress { get; set; }

    public bool AllowsAddressAssignment { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

public class UpdateGeoLevelDto : CreateGeoLevelDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region GeoArea

/// <summary>An administrative area — a region, a district, a town.</summary>
public class GeoAreaDto : ReferenceDto
{
    public Guid SchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;

    public Guid GeoLevelId { get; set; }
    public string GeoLevelName { get; set; } = string.Empty;
    public int LevelNumber { get; set; }

    public Guid? ParentAreaId { get; set; }
    public string? ParentAreaName { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PolygonCoordinatesJson { get; set; }

    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public Guid? SupersededByGeoAreaId { get; set; }
    public string? SupersededByAreaName { get; set; }

    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// True when the area has been end-dated. A historical area still resolves for records that
    /// reference it but is not offered for new ones.
    /// </summary>
    public bool IsHistorical { get; set; }

    /// <summary>Direct children — so a screen can warn before retiring a branch.</summary>
    public int ChildCount { get; set; }

    public List<GeoAreaAliasDto> Aliases { get; set; } = new();
}

/// <summary>
/// A node in the area tree, with its descendants inline. Returned by the tree endpoint so a screen
/// can render a whole scheme without one request per level.
/// </summary>
public class GeoAreaTreeNodeDto
{
    public Guid Id { get; set; }
    public Guid GeoLevelId { get; set; }
    public string GeoLevelName { get; set; } = string.Empty;
    public int LevelNumber { get; set; }
    public Guid? ParentAreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsHistorical { get; set; }
    public List<GeoAreaTreeNodeDto> Children { get; set; } = new();
}

/// <summary>
/// The lean shape a picker needs. Used by the cascading address dropdowns, where sending the full
/// area — polygon and all — for 261 districts would be absurd.
/// </summary>
public class GeoAreaOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid GeoLevelId { get; set; }
    public Guid? ParentAreaId { get; set; }
}

/// <summary>
/// An area resolved from a name, with the ancestors that place it. What the employee form shows
/// once a town is picked, and what the import resolver returns per row.
/// </summary>
public class GeoAreaResolutionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid GeoLevelId { get; set; }
    public string GeoLevelName { get; set; } = string.Empty;

    /// <summary>Broadest first — [Greater Accra, Tema Metropolitan, Community 5].</summary>
    public List<GeoAreaOptionDto> Ancestors { get; set; } = new();

    /// <summary>The name that matched, when the match came through an alias rather than the name.</summary>
    public string? MatchedAlias { get; set; }

    /// <summary>
    /// True when the matched area has been end-dated. The caller decides what to do — an import
    /// should usually follow <see cref="SupersededByGeoAreaId"/>, a form should usually refuse.
    /// </summary>
    public bool IsHistorical { get; set; }

    public Guid? SupersededByGeoAreaId { get; set; }
}

public class CreateGeoAreaDto
{
    [Required]
    public Guid SchemeId { get; set; }

    [Required]
    public Guid GeoLevelId { get; set; }

    public Guid? ParentAreaId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The official statutory code where one exists. The seeder's idempotency key.</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [MaxLength(8000)]
    public string? PolygonCoordinatesJson { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public Guid? SupersededByGeoAreaId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateGeoAreaDto : CreateGeoAreaDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region GeoAreaAlias

public class GeoAreaAliasDto : ReferenceDto
{
    public Guid GeoAreaId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public GeoAreaAliasKind Kind { get; set; }
    public string KindLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CreateGeoAreaAliasDto
{
    [Required]
    public Guid GeoAreaId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Alias { get; set; } = string.Empty;

    public GeoAreaAliasKind Kind { get; set; } = GeoAreaAliasKind.FormerName;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateGeoAreaAliasDto : CreateGeoAreaAliasDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion
