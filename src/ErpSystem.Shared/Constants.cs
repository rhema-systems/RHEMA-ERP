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