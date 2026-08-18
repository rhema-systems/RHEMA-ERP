using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Shared.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ErpSystem.Data.Interceptors;

/// <summary>
/// EF Core interceptor that automatically creates audit log entries for all entity changes
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditInterceptor> _logger;
    private readonly AuditConfiguration _auditConfiguration;

    public AuditInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditInterceptor> logger,
        AuditConfiguration auditConfiguration)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _auditConfiguration = auditConfiguration;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CreateAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await CreateAuditEntriesAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CreateAuditEntries(DbContext? context)
    {
        if (context is not ApplicationDbContext dbContext)
        {
            return;
        }

        try
        {
            var auditEntries = GetAuditEntries(dbContext);

            if (auditEntries.Any())
            {
                // Add audit entries directly to the context
                foreach (var auditEntry in auditEntries)
                {
                    dbContext.AuditLogs.Add(auditEntry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit entries during SaveChanges");
            // Don't rethrow as audit failures shouldn't break the main operation
        }
    }

    private async Task CreateAuditEntriesAsync(DbContext? context, CancellationToken cancellationToken = default)
    {
        if (context is not ApplicationDbContext dbContext)
        {
            return;
        }

        try
        {
            var auditEntries = await GetAuditEntriesAsync(dbContext, cancellationToken);

            if (auditEntries.Any())
            {
                // Add audit entries directly to the context
                foreach (var auditEntry in auditEntries)
                {
                    dbContext.AuditLogs.Add(auditEntry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit entries during SaveChangesAsync");
            // Don't rethrow as audit failures shouldn't break the main operation
        }
    }

    private List<AuditLog> GetAuditEntries(ApplicationDbContext context)
    {
        var auditEntries = new List<AuditLog>();
        var userContext = GetUserContext();
        if (!userContext.HasValue)
        {
            _logger.LogDebug("Skipping audit entry creation - no authenticated request context is available");
            return auditEntries;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (ShouldAuditEntity(entry.Entity))
            {
                var auditEntry = CreateAuditEntry(entry, userContext.Value);
                if (auditEntry != null)
                {
                    auditEntries.Add(auditEntry);
                }
            }
        }

        return auditEntries;
    }

    private async Task<List<AuditLog>> GetAuditEntriesAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var auditEntries = new List<AuditLog>();
        var userContext = GetUserContext();
        if (!userContext.HasValue)
        {
            _logger.LogDebug("Skipping audit entry creation - no authenticated request context is available");
            return auditEntries;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (ShouldAuditEntity(entry.Entity))
            {
                var auditEntry = await CreateAuditEntryAsync(entry, userContext.Value, cancellationToken);
                if (auditEntry != null)
                {
                    auditEntries.Add(auditEntry);
                }
            }
        }

        return auditEntries;
    }

    private bool ShouldAuditEntity(object entity)
    {
        var entityType = entity.GetType();

        // Don't audit the audit logs themselves
        if (entity is AuditLog or SecurityLog)
        {
            return false;
        }

        // Don't audit certain system entities if configured
        if (_auditConfiguration.ExcludedEntityTypes.Contains(entityType))
        {
            return false;
        }

        // Only audit entities that implement IAuditable or are explicitly included
        if (_auditConfiguration.OnlyAuditableEntities)
        {
            return entity is IAuditable || _auditConfiguration.IncludedEntityTypes.Contains(entityType);
        }

        return true;
    }

    private AuditLog? CreateAuditEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        (Guid userId, string username, Guid tenantId, string ipAddress, string userAgent) userContext)
    {
        var (userId, username, tenantId, ipAddress, userAgent) = userContext;

        var entityName = entry.Entity.GetType().Name;
        var action = GetAuditAction(entry.State);
        var resourceId = GetEntityId(entry.Entity);

        var auditEntry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            Action = action,
            Resource = entityName,
            ResourceId = resourceId?.ToString(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };

        // Capture old and new values
        switch (entry.State)
        {
            case EntityState.Added:
                auditEntry.NewValues = SerializeEntityValues(entry, "CurrentValues");
                break;
            case EntityState.Modified:
                auditEntry.OldValues = SerializeEntityValues(entry, "OriginalValues");
                auditEntry.NewValues = SerializeEntityValues(entry, "CurrentValues");
                break;
            case EntityState.Deleted:
                auditEntry.OldValues = SerializeEntityValues(entry, "OriginalValues");
                break;
        }

        return auditEntry;
    }

    private Task<AuditLog?> CreateAuditEntryAsync(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        (Guid userId, string username, Guid tenantId, string ipAddress, string userAgent) userContext,
        CancellationToken cancellationToken = default)
    {
        // For now, the async version is the same as sync since we don't have async user context retrieval
        // This can be extended if needed for async user context operations
        var auditEntry = CreateAuditEntry(entry, userContext);
        return Task.FromResult(auditEntry);
    }

    private (Guid userId, string username, Guid tenantId, string ipAddress, string userAgent)? GetUserContext()
    {
        try
        {
            // Audit logging executes inside DbContext.SaveChanges. Resolving ICurrentUserService here
            // creates UserManager/ApplicationDbContext again and recursively re-enters this interceptor.
            // Claims are already the authoritative request context, so read them without opening a DI
            // scope or resolving any service that can depend on the audited DbContext.
            var httpContext = _httpContextAccessor.HttpContext;
            var principal = httpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var userId = Guid.TryParse(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                out var parsedUserId)
                ? parsedUserId
                : (Guid?)null;
            var username = principal.FindFirstValue(ClaimTypes.Name);
            var tenantId = Guid.TryParse(principal.FindFirstValue("tenant_id"), out var parsedTenantId)
                ? parsedTenantId
                : DefaultTenantId;
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request.Headers.UserAgent.FirstOrDefault();

            // All required fields must be available
            if (!userId.HasValue || string.IsNullOrEmpty(username))
            {
                return null;
            }

            return (userId.Value, username, tenantId, ipAddress ?? "Unknown", userAgent ?? "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve user context for audit logging");
            return null;
        }
    }

    private static string GetAuditAction(EntityState state)
    {
        return state switch
        {
            EntityState.Added => "Create",
            EntityState.Modified => "Update",
            EntityState.Deleted => "Delete",
            _ => "Unknown"
        };
    }

    private static object? GetEntityId(object entity)
    {
        var entityType = entity.GetType();

        // Try to get Id property
        var idProperty = entityType.GetProperty("Id");
        if (idProperty != null)
        {
            return idProperty.GetValue(entity);
        }

        // Try to get properties that might be primary keys
        var keyProperties = new[] { "Id", "Key", "Code", "Name" };
        foreach (var keyProp in keyProperties)
        {
            var prop = entityType.GetProperty(keyProp);
            if (prop != null)
            {
                return prop.GetValue(entity);
            }
        }

        return null;
    }

    private string? SerializeEntityValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string valuesType)
    {
        try
        {
            var values = new Dictionary<string, object?>();
            var properties = valuesType switch
            {
                "OriginalValues" => entry.OriginalValues,
                "CurrentValues" => entry.CurrentValues,
                _ => throw new ArgumentException("Invalid values type", nameof(valuesType))
            };

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                // Skip sensitive properties
                if (_auditConfiguration.ExcludedProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var value = valuesType == "OriginalValues" ? property.OriginalValue : property.CurrentValue;

                    // Only include properties that have changed for updates
                    if (entry.State == EntityState.Modified && valuesType == "CurrentValues")
                    {
                        if (!property.IsModified)
                        {
                            continue;
                        }
                    }

                    values[propertyName] = value;
                }
                catch (InvalidOperationException)
                {
                    // Some properties might not be available in original values (e.g., navigation properties)
                    continue;
                }
            }

            return values.Any() ? JsonSerializer.Serialize(values, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize entity values for audit logging");
            return null;
        }
    }
}

/// <summary>
/// Configuration options for audit logging
/// </summary>
public class AuditConfiguration
{
    /// <summary>
    /// Only audit entities that implement IAuditable interface
    /// </summary>
    public bool OnlyAuditableEntities { get; set; } = false;

    /// <summary>
    /// Entity types to exclude from auditing
    /// </summary>
    public HashSet<Type> ExcludedEntityTypes { get; set; } = new();

    /// <summary>
    /// Entity types to explicitly include in auditing (when OnlyAuditableEntities is false)
    /// </summary>
    public HashSet<Type> IncludedEntityTypes { get; set; } = new();

    /// <summary>
    /// Property names to exclude from audit value serialization (case-insensitive)
    /// </summary>
    public HashSet<string> ExcludedProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
        "AuthenticatorKey", "RecoveryCodes", "TwoFactorSecret",
        "Salt", "Hash", "Token", "RefreshToken", "AccessToken"
    };

    /// <summary>
    /// Create default audit configuration
    /// </summary>
    public static AuditConfiguration CreateDefault()
    {
        var config = new AuditConfiguration();

        // Exclude common system entities that don't need auditing
        config.ExcludedEntityTypes.Add(typeof(AuditLog));
        config.ExcludedEntityTypes.Add(typeof(SecurityLog));
        config.ExcludedEntityTypes.Add(typeof(RefreshToken));
        config.ExcludedEntityTypes.Add(typeof(BlacklistedToken));

        return config;
    }
}

