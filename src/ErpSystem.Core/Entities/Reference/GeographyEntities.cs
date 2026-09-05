using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Reference;

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Administrative geography — shared reference data.
//
//  Region / State / Province / Municipal / Metropolitan / District / Town / Locality / Ward: one
//  model for all of them, because they are the same idea at different depths in different
//  countries. See docs/GEOGRAPHY-REFERENCE-DESIGN.md for the decisions behind this file.
//
//  This is NOT the Location tree. `Location` (OrganizationStructureEntities.cs) answers "which
//  of OUR sites?" and is the target of ~30 foreign keys — incident sites, asset custody,
//  attendance devices, company schedules, geofence zones. `GeoArea` answers "where is this on the
//  map of the country?", which is true whether or not the company operates there. Seeding regions
//  as Location rows would put "Greater Accra Region" in the incident-site picker. The two trees
//  stay separate; `Location` gains a `GeoAreaId` so a site can say which district it sits in.
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One country's way of dividing itself up. Ghana is Region → Metropolitan/Municipal/District →
/// Town; Nigeria is State → LGA → Ward; the UK is Country → County → District → Parish.
/// </summary>
/// <remarks>
/// <para>Making the scheme <b>data</b> rather than code is what removes the schema change when a
/// second country arrives (decision D-2). A tenant operating in three countries holds three
/// schemes, and every address form adapts itself from whichever one the selected country points
/// at.</para>
/// </remarks>
public class GeoScheme : TenantEntity
{
    /// <summary>The country this scheme divides. A scheme without a country cannot be selected.</summary>
    [Required]
    public Guid CountryId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. <c>GH-ADMIN</c>. Unique per tenant.</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The scheme an address form reaches for when the country is chosen. At most one per country
    /// per tenant; the service enforces that, unsetting the previous default rather than refusing.
    /// </summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;

    public virtual ICollection<GeoLevel> Levels { get; set; } = new List<GeoLevel>();

    public virtual ICollection<GeoArea> Areas { get; set; } = new List<GeoArea>();
}

/// <summary>
/// One tier within a scheme — "Region", "District", "Town".
/// </summary>
/// <remarks>
/// <para>⚠ <see cref="Name"/> is <b>the field label the address form prints</b>. That is the whole
/// mechanism by which one React component serves every country: a Ghanaian employee's form reads
/// Region / District / Town and a Nigerian one reads State / LGA / Ward, with no difference in
/// frontend code. Rename a level and every form that uses its scheme re-labels itself.</para>
/// </remarks>
public class GeoLevel : TenantEntity
{
    [Required]
    public Guid SchemeId { get; set; }

    /// <summary>The tier's name, and the label an address form prints above its dropdown.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. <c>REGION</c>, <c>MMDA</c>, <c>TOWN</c>. Unique within the scheme.</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 1 = broadest. Mirrors <c>LocationLevel.LevelNumber</c> and <c>OrganizationLevel.LevelNumber</c>
    /// so the three hierarchies in this codebase are read the same way. Unique within the scheme.
    /// </summary>
    [Required]
    public int LevelNumber { get; set; }

    /// <summary>
    /// Whether an address using this scheme must name an area at this tier. Drives validation on
    /// the address widget; the region is usually required and the town usually is not.
    /// </summary>
    public bool IsRequiredInAddress { get; set; }

    /// <summary>
    /// Whether an address may <b>stop</b> here — i.e. whether a record's <c>GeoAreaId</c> is allowed
    /// to point at an area of this tier. False for a purely administrative tier that exists to
    /// group but is never itself an answer to "where do you live?".
    /// </summary>
    public bool AllowsAddressAssignment { get; set; } = true;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(SchemeId))]
    public virtual GeoScheme Scheme { get; set; } = null!;

    public virtual ICollection<GeoArea> Areas { get; set; } = new List<GeoArea>();
}

/// <summary>
/// An actual administrative area — Greater Accra, Tema Metropolitan, Community 5.
/// </summary>
/// <remarks>
/// <para><b>The one-FK rule.</b> Consumers carry a single nullable <c>GeoAreaId</c> pointing at the
/// <i>lowest</i> tier they know — never <c>RegionId</c> + <c>DistrictId</c> + <c>TownId</c>.
/// Roll-up to any tier comes from <see cref="Path"/>. That is what lets a fourth tier be added
/// later without migrating a single consumer table.</para>
///
/// <para><b>⚠ Never rename an area to reflect a boundary change.</b> Ghana went from 10 regions to
/// 16 in 2019 and districts split most election cycles. End-date the old row, create the new one,
/// and point <see cref="SupersededByGeoAreaId"/> at the successor. A record created in 2018 then
/// still resolves to the region that existed in 2018, and "headcount by region <i>as at</i> a
/// date" stays answerable. Renaming in place silently rewrites history, which is the specific
/// failure this model exists to avoid.</para>
/// </remarks>
public class GeoArea : TenantEntity
{
    [Required]
    public Guid SchemeId { get; set; }

    [Required]
    public Guid GeoLevelId { get; set; }

    /// <summary>Null at the broadest tier; otherwise the area one tier up.</summary>
    public Guid? ParentAreaId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <b>official statutory code</b> where one exists — a Ghana Statistical Service district
    /// code, an ISO 3166-2 subdivision code — not an invented one.
    /// </summary>
    /// <remarks>
    /// ⚠ This is what makes re-running the seeder a no-op instead of a duplicate. Invent a code and
    /// the next seed of the same data creates a second Tema Metropolitan.
    /// </remarks>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Materialised ancestor path, matching the <c>Location.Path</c> convention. Maintained by the
    /// service on create and on any reparent, including for every descendant.
    /// </summary>
    [MaxLength(1000)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Centre latitude, for map display and for anchoring a search.</summary>
    public double? Latitude { get; set; }

    /// <summary>Centre longitude.</summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// JSON array of {lat, lng} pairs describing the area's boundary — the same storage shape as
    /// <c>GeofenceZone.PolygonCoordinatesJson</c>, so the Leaflet picker built for geofencing reads
    /// it without translation. Expected to stay null until someone has shapefiles.
    /// </summary>
    [MaxLength(8000)]
    public string? PolygonCoordinatesJson { get; set; }

    /// <summary>
    /// When this area came into existence. Null means "as long as anyone cares" — the common case
    /// for areas that predate the records in the system.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// When this area ceased to exist. Non-null means the area is historical: it must still resolve
    /// for records that reference it, but it is not offered for new ones.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// The area that replaced this one when it was split, merged or renamed. Lets a report follow a
    /// 2018 record forward to today's boundaries without destroying what the record actually said.
    /// </summary>
    public Guid? SupersededByGeoAreaId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(SchemeId))]
    public virtual GeoScheme Scheme { get; set; } = null!;

    [ForeignKey(nameof(GeoLevelId))]
    public virtual GeoLevel GeoLevel { get; set; } = null!;

    [ForeignKey(nameof(ParentAreaId))]
    public virtual GeoArea? ParentArea { get; set; }

    [ForeignKey(nameof(SupersededByGeoAreaId))]
    public virtual GeoArea? SupersededByArea { get; set; }

    public virtual ICollection<GeoArea> ChildAreas { get; set; } = new List<GeoArea>();

    public virtual ICollection<GeoAreaAlias> Aliases { get; set; } = new List<GeoAreaAlias>();
}

/// <summary>
/// An alternate name an area is known by.
/// </summary>
/// <remarks>
/// Feeds type-ahead, but its real job is the import resolver: a spreadsheet that still says
/// "Brong Ahafo" resolves to the right successor instead of failing the row. Without this, every
/// boundary change turns into a wave of unresolvable import rows.
/// </remarks>
public class GeoAreaAlias : TenantEntity
{
    [Required]
    public Guid GeoAreaId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Alias { get; set; } = string.Empty;

    public GeoAreaAliasKind Kind { get; set; } = GeoAreaAliasKind.FormerName;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual GeoArea GeoArea { get; set; } = null!;
}
