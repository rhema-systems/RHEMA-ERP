using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Provides access to the current tenant context
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Get the current tenant ID from the request context
    /// </summary>
    Guid GetCurrentTenantId();
    
    /// <summary>
    /// Get the current tenant from the request context
    /// </summary>
    Task<Tenant?> GetCurrentTenantAsync();
    
    /// <summary>
    /// Set the current tenant for the request context
    /// </summary>
    void SetCurrentTenant(Guid tenantId);
    
    /// <summary>
    /// Check if a tenant is set in the current context
    /// </summary>
    bool HasCurrentTenant();
}