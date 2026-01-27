using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public class TenantContext : ITenantContext
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ApplicationDbContext _context;
    private Guid? _currentTenantId;

    public TenantContext(
        ICurrentUserProvider currentUserProvider,
        ApplicationDbContext context)
    {
        _currentUserProvider = currentUserProvider;
        _context = context;
    }

    public Guid GetCurrentTenantId()
    {
        if (_currentTenantId.HasValue)
        {
            return _currentTenantId.Value;
        }

        // Get tenant ID from current user provider
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId != Guid.Empty)
        {
            _currentTenantId = tenantId;
            return tenantId;
        }

        return Guid.Empty;
    }

    public async Task<Tenant?> GetCurrentTenantAsync()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted);
    }

    public void SetCurrentTenant(Guid tenantId)
    {
        _currentTenantId = tenantId;
    }

    public bool HasCurrentTenant()
    {
        return GetCurrentTenantId() != Guid.Empty;
    }
}

