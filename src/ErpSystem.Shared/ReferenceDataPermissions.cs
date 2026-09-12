namespace ErpSystem.Shared;

public sealed record ReferenceDataPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

/// <summary>
/// Permissions over shared, cross-module reference data — the sets no single module owns.
/// Administrative geography is the first resident; see docs/GEOGRAPHY-REFERENCE-DESIGN.md.
/// </summary>
/// <remarks>
/// <para>Its own family rather than a corner of <see cref="HrPermissions"/> because the data is not
/// HR's (decision D-1). HR is the first consumer, but Estate, Sales, Procurement and Inventory all
/// carry the same free-text region and district columns today, and none of them should have to hold
/// an HR permission to maintain a list of districts.</para>
///
/// <para>⚠ There is deliberately <b>no Read permission</b>. Every module's address form needs to
/// list regions and districts, and gating that would break an address dropdown for anyone outside
/// the reference-data desk. The read endpoints are <c>InternalOnly</c>: authenticated internal
/// users, no further gate. Administrative divisions are public knowledge — the sensitivity in an
/// address lives on the record that carries it, which keeps its own module's gate.</para>
/// </remarks>
public static class ReferenceDataPermissions
{
    public const string Category = "Reference Data";

    /// <summary>Prefix identifying shared reference-data permissions.</summary>
    public const string Prefix = "Reference.";

    public const string MaintainGeography = "Reference.Geography.Write";
    public const string AdministerGeography = "Reference.Geography.Admin";

    public const string GeographyWritePolicy = "Reference.Policy.GeographyWrite";
    public const string GeographyAdminPolicy = "Reference.Policy.GeographyAdmin";

    public static readonly ReferenceDataPermissionDefinition[] All =
    [
        new(MaintainGeography, "Maintain Geography",
            "Maintain the administrative geography every module's addresses resolve through: country division schemes, "
            + "their tiers (Region, District, Town and their equivalents), the areas themselves, and the alternate "
            + "names old records and imported spreadsheets still use. Includes end-dating an area whose boundaries "
            + "have changed, which is how a split or merge is recorded.",
            Category),
        new(AdministerGeography, "Administer Geography",
            "Delete geography reference data — schemes, tiers, areas and aliases. Separate from Maintain because "
            + "deleting an area is the one act that cannot be undone by a later correction: records pointing at it "
            + "lose what they said. Ending an area's effective period is the reversible alternative and sits with "
            + "Maintain.",
            Category)
    ];

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();

    /// <summary>
    /// Who maintains geography. Deliberately narrow: this is a list the whole product reads, so a
    /// district renamed carelessly is felt in every module at once.
    /// </summary>
    /// <remarks>
    /// The HR role is granted Maintain because HR is the first consumer and, at TDC, the desk that
    /// actually curates the list. Deletion stays with tenant administrators for everyone.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string[]> RoleGrants =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.Roles.SuperAdmin] = AllNames,
            [Constants.Roles.TenantAdmin] = AllNames,
            ["Admin"] = AllNames,
            [Constants.Roles.Hr] = [MaintainGeography],
            [Constants.Roles.LegacyHrUser] = [MaintainGeography],
        };

    /// <summary>
    /// The reference-data permissions granted to <paramref name="roleName"/>, or an empty array
    /// when the role receives none.
    /// </summary>
    public static string[] GrantsFor(string roleName)
        => RoleGrants.TryGetValue(roleName, out var permissions)
            ? permissions
            : Array.Empty<string>();
}
