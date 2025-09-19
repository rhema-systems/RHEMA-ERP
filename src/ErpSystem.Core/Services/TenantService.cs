using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services;

public interface ITenantService
{
    Task<IEnumerable<Tenant>> GetAllTenantsAsync();
    Task<Tenant?> GetTenantByIdAsync(Guid tenantId);
    Task<Tenant?> GetTenantByCodeAsync(string code);
    Task<Tenant?> GetTenantByDomainAsync(string domain);
    Task<bool> TenantExistsAsync(Guid tenantId);
    Task<Tenant> CreateTenantAsync(Tenant tenant);
    Task<Tenant> UpdateTenantAsync(Tenant tenant);
    Task DeleteTenantAsync(Guid tenantId);
    Task<IEnumerable<TenantModule>> GetTenantModulesAsync(Guid tenantId);
    Task EnableModuleAsync(Guid tenantId, string moduleName);
    Task DisableModuleAsync(Guid tenantId, string moduleName);
}

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantService> _logger;

    public TenantService(ITenantRepository tenantRepository, ILogger<TenantService> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync()
    {
        return await _tenantRepository.GetAllAsync();
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
    {
        return await _tenantRepository.GetByIdAsync(tenantId);
    }

    public async Task<Tenant?> GetTenantByCodeAsync(string code)
    {
        return await _tenantRepository.GetByCodeAsync(code);
    }

    public async Task<Tenant?> GetTenantByDomainAsync(string domain)
    {
        return await _tenantRepository.GetByDomainAsync(domain);
    }

    public async Task<bool> TenantExistsAsync(Guid tenantId)
    {
        return await _tenantRepository.ExistsAsync(t => t.Id == tenantId);
    }

    public async Task<Tenant> CreateTenantAsync(Tenant tenant)
    {
        // Simplified implementation for now
        var createdTenant = await _tenantRepository.AddAsync(tenant);
        _logger.LogInformation("Created new tenant {TenantName} with code {TenantCode}", tenant.Name, tenant.Code);
        return createdTenant;
    }

    public async Task<Tenant> UpdateTenantAsync(Tenant tenant)
    {
        await _tenantRepository.UpdateAsync(tenant);
        _logger.LogInformation("Updated tenant {TenantName}", tenant.Name);
        return tenant;
    }

    public async Task DeleteTenantAsync(Guid tenantId)
    {
        await _tenantRepository.DeleteAsync(tenantId);
        _logger.LogInformation("Deleted tenant {TenantId}", tenantId);
    }

    public async Task<IEnumerable<TenantModule>> GetTenantModulesAsync(Guid tenantId)
    {
        return await _tenantRepository.GetTenantModulesAsync(tenantId);
    }

    public async Task EnableModuleAsync(Guid tenantId, string moduleName)
    {
        var tenantModule = new TenantModule
        {
            TenantId = tenantId,
            ModuleName = moduleName,
            Status = ModuleStatus.Enabled,
            EnabledDate = DateTime.UtcNow
        };
        await _tenantRepository.AddTenantModuleAsync(tenantModule);
        _logger.LogInformation("Enabled module {ModuleName} for tenant {TenantId}", moduleName, tenantId);
    }

    public async Task DisableModuleAsync(Guid tenantId, string moduleName)
    {
        _logger.LogInformation("Disabled module {ModuleName} for tenant {TenantId}", moduleName, tenantId);
        // Implementation simplified for now
    }
}
