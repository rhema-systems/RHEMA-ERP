using ErpSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces
{
    public interface ITenantRepository : IGenericRepository<Tenant>
    {
        Task<Tenant?> GetByCodeAsync(string code);
        Task<Tenant?> GetByDomainAsync(string domain);
        Task<IEnumerable<TenantModule>> GetTenantModulesAsync(Guid tenantId);
        Task<TenantModule?> GetTenantModuleAsync(Guid tenantId, string moduleName);
        Task AddTenantModuleAsync(TenantModule tenantModule);
        Task UpdateTenantModuleAsync(TenantModule tenantModule);
    }
}