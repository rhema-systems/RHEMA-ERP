namespace ErpSystem.Shared;

public sealed record HrPermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);

/// <summary>
/// Named HR permissions and the policies built from them, mirroring
/// <see cref="FinancePermissions"/>.
/// </summary>
/// <remarks>
/// <para>Only the occupational-health slice is populated for now. It exists because the medical
/// controllers previously carried a bare <c>[Authorize]</c>, which meant any authenticated user
/// — every employee with an ERP login — could list health profiles, read conditions, allergies
/// and exam documents, and delete them. Role strings would have closed that, but medical data
/// warrants the same explicit, greppable permission model Finance already uses.</para>
///
/// <para>This class is the home for future HR permissions; the rest of the HR module still uses
/// role strings and is not in scope here.</para>
/// </remarks>
public static class HrPermissions
{
    public const string CategoryMedical = "HR - Occupational Health";
    public const string CategoryTravel = "HR - Staff Travel";

    /// <summary>Prefix identifying HR permissions, used by the role-fallback handler.</summary>
    public const string Prefix = "HR.";

    public const string ViewMedicalRecords = "HR.Medical.Read";
    public const string MaintainMedicalRecords = "HR.Medical.Write";
    public const string AdministerMedical = "HR.Medical.Admin";

    public const string MedicalReadPolicy = "HR.Policy.MedicalRead";
    public const string MedicalWritePolicy = "HR.Policy.MedicalWrite";
    public const string MedicalAdminPolicy = "HR.Policy.MedicalAdmin";

    public const string ViewTravel = "HR.Travel.Read";
    public const string MaintainTravel = "HR.Travel.Write";
    public const string AdministerTravel = "HR.Travel.Admin";

    public const string TravelReadPolicy = "HR.Policy.TravelRead";
    public const string TravelWritePolicy = "HR.Policy.TravelWrite";
    public const string TravelAdminPolicy = "HR.Policy.TravelAdmin";

    public static readonly HrPermissionDefinition[] All =
    {
        new(ViewMedicalRecords, "View Medical Records",
            "View employee health profiles, conditions, allergies, exams, claims, and medical documents.",
            CategoryMedical),
        new(MaintainMedicalRecords, "Maintain Medical Records",
            "Create and update employee health records, exams, and medical claims.",
            CategoryMedical),
        new(AdministerMedical, "Administer Medical Records",
            "Delete medical records and administer occupational-health configuration.",
            CategoryMedical),

        new(ViewTravel, "View Staff Travel",
            "View travel requests, itineraries, bookings, advances, expense claims, travel documents and policies.",
            CategoryTravel),
        new(MaintainTravel, "Maintain Staff Travel",
            "Raise and amend travel requests on behalf of staff, make bookings, and process advances and expense claims.",
            CategoryTravel),
        new(AdministerTravel, "Administer Staff Travel",
            "Delete travel records and administer travel policies, per-diem rates, vendors and approval templates.",
            CategoryTravel)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();

    /// <summary>
    /// What HR staff hold: they maintain records but do not administer them, so deleting a
    /// medical record or a paid travel claim stays with tenant administrators.
    /// </summary>
    /// <remarks>
    /// Travel follows medical deliberately. HR raises travel on behalf of staff and processes
    /// advances and claims (Write), but deleting travel records and setting the policies,
    /// per-diem rates and approval templates that govern their own spending authority is
    /// administration (Admin) — the same separation that stopped an HR-role user deleting a paid
    /// medical claim.
    /// </remarks>
    private static readonly string[] HrStaffGrants =
    {
        ViewMedicalRecords, MaintainMedicalRecords,
        ViewTravel, MaintainTravel
    };

    /// <summary>
    /// Per-role HR permission grants. This is the single source for both the database seed
    /// (<c>DatabaseSeedingService</c>) and the role fallback
    /// (<c>HrPermissionRoleFallbackAuthorizationHandler</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>The fallback exists to stand in for the seed, so it must grant exactly what the
    /// seed grants.</b> Both read this map for that reason. The previous implementation matched
    /// on the <c>HR.</c> prefix alone and never inspected the verb, so an HR-role user — seeded
    /// with Read and Write only — satisfied <c>MedicalAdminPolicy</c> as well and could delete
    /// medical records, including paid expense claims. Verb-blind fallback is a privilege
    /// escalation; keep the two sides reading one map so they cannot drift apart again.</para>
    ///
    /// <para>Permissions resolve from the database, so without a fallback every HR endpoint would
    /// 403 for everyone but SuperAdmin on a tenant provisioned before the seeder ran.</para>
    ///
    /// <para>"HR User" was the seeded name before the rename to "HR"
    /// (<c>DatabaseSeedingService.MigrateLegacyHrRoleNameAsync</c>). It is listed so a tenant that
    /// has not yet run the renaming startup keeps access; the seeder does not seed it, which is
    /// precisely what the fallback is for. Drop it once every environment is known to be migrated.</para>
    ///
    /// <para>⚠ "Admin" is a bare literal with no <c>Constants.Roles</c> member, and no such role
    /// exists in the reference database (checked 2026-08-17). It is retained because removing it
    /// would silently revoke medical access in any environment that does have one. Confirm whether
    /// any environment uses it, then either promote it to a constant or delete it.</para>
    ///
    /// <para>When extending this to other HR areas (the W3 permission sweep), add the area's
    /// permissions here per role rather than widening the match — the whole point of this map is
    /// that a role's grants are stated, not inferred from a name.</para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string[]> RoleGrants =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Constants.Roles.SuperAdmin] = AllNames,
            [Constants.Roles.TenantAdmin] = AllNames,
            ["Admin"] = AllNames,
            [Constants.Roles.Hr] = HrStaffGrants,
            [Constants.Roles.LegacyHrUser] = HrStaffGrants
        };

    /// <summary>
    /// The HR permissions granted to <paramref name="roleName"/>, or an empty array when the role
    /// receives none. Callers can concatenate this with their own module's grants.
    /// </summary>
    public static string[] GrantsFor(string roleName)
        => RoleGrants.TryGetValue(roleName, out var permissions)
            ? permissions
            : Array.Empty<string>();
}
