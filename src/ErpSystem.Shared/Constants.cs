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
