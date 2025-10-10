using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ErpSystem.Data.Interceptors;

namespace ErpSystem.Data.Extensions;

/// <summary>
/// Extension methods for configuring audit logging
/// </summary>
public static class AuditExtensions
{
    /// <summary>
    /// Add audit logging interceptor to the DbContext
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure audit options from configuration
        services.Configure<AuditOptions>(configuration.GetSection("Audit"));

        // Register audit configuration
        services.AddSingleton<AuditConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AuditOptions>>().Value;
            return CreateAuditConfiguration(options);
        });

        // Register the audit interceptor
        services.AddScoped<AuditInterceptor>();

        return services;
    }

    /// <summary>
    /// Add audit logging interceptor to the DbContext with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, Action<AuditOptions> configureOptions)
    {
        // Configure audit options
        services.Configure(configureOptions);

        // Register audit configuration
        services.AddSingleton<AuditConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AuditOptions>>().Value;
            return CreateAuditConfiguration(options);
        });

        // Register the audit interceptor
        services.AddScoped<AuditInterceptor>();

        return services;
    }

    /// <summary>
    /// Add the audit interceptor to DbContext options
    /// </summary>
    /// <param name="optionsBuilder">DbContext options builder</param>
    /// <param name="serviceProvider">Service provider to resolve the interceptor</param>
    /// <returns>Options builder for chaining</returns>
    public static DbContextOptionsBuilder AddAuditInterceptor(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
    {
        var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
        return optionsBuilder.AddInterceptors(auditInterceptor);
    }

    private static AuditConfiguration CreateAuditConfiguration(AuditOptions options)
    {
        var config = new AuditConfiguration
        {
            OnlyAuditableEntities = options.OnlyAuditableEntities
        };

        // Add excluded entity types
        foreach (var typeName in options.ExcludedEntityTypes)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                config.ExcludedEntityTypes.Add(type);
            }
        }

        // Add included entity types
        foreach (var typeName in options.IncludedEntityTypes)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                config.IncludedEntityTypes.Add(type);
            }
        }

        // Add excluded properties
        foreach (var propertyName in options.ExcludedProperties)
        {
            config.ExcludedProperties.Add(propertyName);
        }

        return config;
    }
}

/// <summary>
/// Configuration options for audit logging
/// </summary>
public class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>
    /// Enable or disable audit logging globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Only audit entities that implement IAuditable interface
    /// </summary>
    public bool OnlyAuditableEntities { get; set; } = false;

    /// <summary>
    /// List of entity type names to exclude from auditing
    /// </summary>
    public List<string> ExcludedEntityTypes { get; set; } = new()
    {
        "ErpSystem.Core.Entities.AuditLog",
        "ErpSystem.Core.Entities.SecurityLog",
        "ErpSystem.Core.Entities.RefreshToken",
        "ErpSystem.Core.Entities.BlacklistedToken"
    };

    /// <summary>
    /// List of entity type names to explicitly include in auditing
    /// </summary>
    public List<string> IncludedEntityTypes { get; set; } = new();

    /// <summary>
    /// List of property names to exclude from audit value serialization
    /// </summary>
    public List<string> ExcludedProperties { get; set; } = new()
    {
        "Password", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", 
        "AuthenticatorKey", "RecoveryCodes", "TwoFactorSecret",
        "Salt", "Hash", "Token", "RefreshToken", "AccessToken",
        "NormalizedUserName", "NormalizedEmail", "EmailConfirmationToken",
        "PasswordResetToken", "TwoFactorCode", "BackupCodes"
    };

    /// <summary>
    /// Maximum length for serialized audit values (to prevent database issues)
    /// </summary>
    public int MaxAuditValueLength { get; set; } = 4000;

    /// <summary>
    /// Include system-generated changes (like timestamps) in audit logs
    /// </summary>
    public bool IncludeSystemChanges { get; set; } = false;
}