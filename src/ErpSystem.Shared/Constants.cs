namespace ErpSystem.Shared;

public static class Constants
{
    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string TenantAdmin = "TenantAdmin";
        public const string Manager = "Manager";
        public const string Employee = "Employee";
        public const string ReadOnly = "ReadOnly";

        /// <summary>
        /// The HR module role.
        ///
        /// ⚠ This was seeded as "HR User" while every <c>[Authorize(Roles = "HR")]</c> attribute
        /// across the HR controllers named a bare "HR" — a role no user could hold, so those
        /// endpoints were reachable only by SuperAdmin/Admin while the same users passed the
        /// permission-based medical endpoints. The seeded role is renamed to "HR" on startup
        /// (see <c>DatabaseSeedingService.MigrateLegacyHrRoleNameAsync</c>); membership is by
        /// role id, so nobody loses access in the rename. Use this constant rather than a
        /// literal so the two cannot drift apart again.
        /// </summary>
        public const string Hr = "HR";

        /// <summary>The pre-rename name, kept only so the migration and fallbacks can find it.</summary>
        public const string LegacyHrUser = "HR User";

        /// <summary>
        /// The Managing Director, who signs every non-procedural termination (FR-HR-092) and sits
        /// at the top of the grievance ladder (FR-HR-181).
        ///
        /// ⚠ This role exists under <b>two</b> names in the reference database (checked
        /// 2026-08-20): "Managing Director", seeded with the general roles, and
        /// "TDC_MANAGING_DIRECTOR", seeded by the procurement work. That is the
        /// <c>HR</c> / <c>HR User</c> trap again — an attribute naming only one of them silently
        /// excludes half the people entitled to act. Authorize on <see cref="ManagingDirectorAny"/>
        /// rather than either literal until the two are reconciled.
        /// </summary>
        public const string ManagingDirector = "Managing Director";

        /// <summary>The procurement-seeded spelling of <see cref="ManagingDirector"/>.</summary>
        public const string TdcManagingDirector = "TDC_MANAGING_DIRECTOR";

        /// <summary>
        /// Both spellings of the Managing Director role, ready for
        /// <c>[Authorize(Roles = Constants.Roles.ManagingDirectorAny)]</c>. Roles in an
        /// <c>[Authorize]</c> list are ORed, so this admits a holder of either.
        /// </summary>
        public const string ManagingDirectorAny = ManagingDirector + "," + TdcManagingDirector;

        /// <summary>
        /// Internal Audit, which reviews the final settlement before payment is released
        /// (FR-HR-185) and is the mandatory review point before a payroll release. Only the
        /// TDC_ spelling exists (checked 2026-08-20).
        /// </summary>
        public const string InternalAudit = "TDC_INTERNAL_AUDIT";

        // External users (customer/vendor/partner/citizen portal accounts)
        public const string ExternalUser = "ExternalUser";

        // Helpdesk / Enquiry / Complaints roles
        public const string HelpdeskAgent = "HelpdeskAgent";
        public const string HelpdeskSupervisor = "HelpdeskSupervisor";
        public const string HelpdeskManager = "HelpdeskManager";

        public static bool IsProtectedSystemRole(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var normalizedRoleName = roleName.Trim();
            return string.Equals(normalizedRoleName, SuperAdmin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, TenantAdmin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, Manager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, Employee, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, ReadOnly, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, ExternalUser, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, HelpdeskAgent, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, HelpdeskSupervisor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedRoleName, HelpdeskManager, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class Modules
    {
        public const string Finance = "Finance";
        public const string HR = "HR";
        public const string Sales = "Sales";
        public const string Procurement = "Procurement";
        public const string Inventory = "Inventory";
        public const string Marketing = "Marketing";
        public const string WorkflowEngine = "WorkflowEngine";
    }

    public static class Claims
    {
        public const string TenantId = "tenant_id";
        public const string TenantName = "tenant_name";
        public const string Module = "module";
        public const string Permission = "permission";
    }

    public static class AuthenticationSchemes
    {
        public const string LDAP = "LDAP";
        public const string Local = "Local";
    }

    public static class Tenants
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public const string DefaultTenantCode = "DEFAULT";
    }

    /// <summary>
    /// Validation for user-chosen display colours stored as hex strings.
    /// </summary>
    public static class Colors
    {
        /// <summary>
        /// A CSS hex colour: <c>#RGB</c>, <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.
        /// </summary>
        /// <remarks>
        /// Anchored, so <c>[RegularExpression]</c> cannot match a substring. These fields are all
        /// nullable and optional — <c>[RegularExpression]</c> ignores null and empty, so an unset
        /// colour still validates and only a non-empty malformed value is rejected.
        /// ⚠ Applies only to fields that hold a *hex code*. Several HR entities have a
        /// <c>Color</c> that is a plain description (an asset's physical colour, e.g. "Silver") —
        /// those must not use this.
        /// </remarks>
        public const string HexPattern = @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$";

        public const string HexMessage = "Use a hex colour such as #4F46E5 (#RGB, #RRGGBB or #RRGGBBAA).";
    }
}

public enum AuthenticationProvider
{
    Local = 1,
    LDAP = 2
}

public enum TenantStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}

public enum ModuleStatus
{
    Enabled = 1,
    Disabled = 2,
    Maintenance = 3
}
