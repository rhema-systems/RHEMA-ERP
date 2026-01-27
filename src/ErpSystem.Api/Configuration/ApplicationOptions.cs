using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Web.Configuration
{
    public class ApplicationOptions
    {
        public const string SectionName = "ApplicationSettings";

        [Required]
        [MinLength(1)]
        public string ApplicationName { get; set; } = "ErpSystem";

        [Required]
        [RegularExpression(@"^\d+\.\d+\.\d+(-\w+)?$")]
        public string Version { get; set; } = "1.0.0";

        [Required]
        [AllowedValues("Development", "Staging", "Production")]
        public string Environment { get; set; } = "Development";

        [Required]
        [MinLength(1)]
        public string CompanyName { get; set; } = "Your Company";

        [Required]
        [EmailAddress]
        public string SupportEmail { get; set; } = "support@yourcompany.com";
    }

    public class PerformanceOptions
    {
        public const string SectionName = "Performance";

        public CachingOptions Caching { get; set; } = new();
        public SearchOptions Search { get; set; } = new();
    }

    public class CachingOptions
    {
        [Range(1, 1440)] // 1 minute to 24 hours
        public int DefaultCacheDurationMinutes { get; set; } = 30;

        public bool ResponseCacheEnabled { get; set; } = true;
        public bool DistributedCacheEnabled { get; set; } = true;
    }

    public class SearchOptions
    {
        [Url]
        public string? ElasticsearchUrl { get; set; }

        [Required]
        [MinLength(1)]
        public string IndexPrefix { get; set; } = "erpsystem";

        [Range(5, 50)]
        public int DefaultPageSize { get; set; } = 20;

        [Range(10, 1000)]
        public int MaxPageSize { get; set; } = 100;
    }

    public class AuthenticationOptions
    {
        public const string SectionName = "Authentication";

        [Required]
        [AllowedValues("Local", "LDAP", "Azure")]
        public string DefaultAuthenticationProvider { get; set; } = "Local";

        public bool FallbackToLocal { get; set; } = true;

        public LdapOptions LDAP { get; set; } = new();
        public SecurityOptions Security { get; set; } = new();
    }

    public class LdapOptions
    {
        public bool Enabled { get; set; } = false;

        public string? DefaultServer { get; set; }

        [Range(1, 65535)]
        public int DefaultPort { get; set; } = 389;

        public string? DefaultBaseDn { get; set; }
        public string? DefaultBindDn { get; set; }
        public string? DefaultBindPassword { get; set; }
    }

    public class SecurityOptions
    {
        public bool RequireHttps { get; set; } = true;
        public bool RequireConfirmedAccount { get; set; } = false;

        [Range(1, 10)]
        public int MaxFailedAccessAttempts { get; set; } = 5;

        public TimeSpan LockoutTimeSpan { get; set; } = TimeSpan.FromMinutes(30);

        public bool PasswordRequireDigit { get; set; } = true;

        [Range(6, 128)]
        public int PasswordRequiredLength { get; set; } = 8;

        public bool PasswordRequireNonAlphanumeric { get; set; } = true;
        public bool PasswordRequireUppercase { get; set; } = true;
        public bool PasswordRequireLowercase { get; set; } = true;

        [Range(1, 10)]
        public int PasswordRequiredUniqueChars { get; set; } = 1;
    }

    public class DataProtectionOptions
    {
        public const string SectionName = "DataProtection";

        public string? KeysPath { get; set; }

        [Required]
        [MinLength(1)]
        public string ApplicationName { get; set; } = "ErpSystem";
    }

    public class SecurityHeaderOptions
    {
        public const string SectionName = "Security";

        public string AllowedHosts { get; set; } = "*";
        public bool ForceHttps { get; set; } = true;

        [Range(1, int.MaxValue)]
        public int HstsMaxAge { get; set; } = 31536000; // 1 year

        [Required]
        public string ContentSecurityPolicy { get; set; } = "default-src 'self';";

        [Required]
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    }

    public class MonitoringOptions
    {
        public const string SectionName = "Monitoring";

        public ApplicationInsightsOptions ApplicationInsights { get; set; } = new();
        public HealthCheckOptions HealthChecks { get; set; } = new();
    }

    public class ApplicationInsightsOptions
    {
        public string? InstrumentationKey { get; set; }
        public string? ConnectionString { get; set; }
    }

    public class HealthCheckOptions
    {
        public bool Enabled { get; set; } = true;
        public bool DetailedErrors { get; set; } = false;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
    }

    public class ModuleOptions
    {
        public const string SectionName = "Modules";

        public ModuleSettings Finance { get; set; } = new() { DisplayName = "Finance" };
        public ModuleSettings HR { get; set; } = new() { DisplayName = "Human Resources" };
        public ModuleSettings Sales { get; set; } = new() { DisplayName = "Sales" };
        public ModuleSettings Procurement { get; set; } = new() { DisplayName = "Procurement" };
        public ModuleSettings Inventory { get; set; } = new() { DisplayName = "Inventory" };
        public ModuleSettings Marketing { get; set; } = new() { DisplayName = "Marketing" };
        public ModuleSettings WorkflowEngine { get; set; } = new() { DisplayName = "Workflow Engine" };
    }

    public class ModuleSettings
    {
        public bool Enabled { get; set; } = true;

        [Required]
        [MinLength(1)]
        public string DisplayName { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
