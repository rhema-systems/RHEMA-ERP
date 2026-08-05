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

    /// <summary>Prefix identifying HR permissions, used by the role-fallback handler.</summary>
    public const string Prefix = "HR.";

    public const string ViewMedicalRecords = "HR.Medical.Read";
    public const string MaintainMedicalRecords = "HR.Medical.Write";
    public const string AdministerMedical = "HR.Medical.Admin";

    public const string MedicalReadPolicy = "HR.Policy.MedicalRead";
    public const string MedicalWritePolicy = "HR.Policy.MedicalWrite";
    public const string MedicalAdminPolicy = "HR.Policy.MedicalAdmin";

    /// <summary>
    /// Roles that retain medical access when a tenant predates the permission seed.
    /// </summary>
    /// <remarks>
    /// Permissions resolve from the database, so without this every medical endpoint would 403
    /// for everyone but SuperAdmin on any tenant provisioned before the seeder ran. See
    /// <c>HrPermissionRoleFallbackAuthorizationHandler</c>.
    /// </remarks>
    /// <remarks>
    /// Both "HR" and "HR User" are listed deliberately: the seeder creates "HR User", while the
    /// existing <c>[Authorize(Roles = "HR")]</c> attributes across the HR controllers reference a
    /// bare "HR". Whichever a tenant actually assigned, medical access survives this change.
    /// </remarks>
    public static readonly string[] MedicalFallbackRoles =
    {
        "SuperAdmin",
        "TenantAdmin",
        "Admin",
        "HR",
        "HR User"
    };

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
            CategoryMedical)
    };

    public static readonly string[] AllNames = All.Select(permission => permission.Name).ToArray();
}
