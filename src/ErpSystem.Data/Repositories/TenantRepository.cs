using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories
{
    public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
    {
        public TenantRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Tenant?> GetByCodeAsync(string code)
        {
            return await _dbSet
                .Include(t => t.TenantModules)
                .FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);
        }

        public async Task<Tenant?> GetByDomainAsync(string domain)
        {
            return await _dbSet
                .Include(t => t.TenantModules)
                .FirstOrDefaultAsync(t => t.Domain == domain && !t.IsDeleted);
        }

        public async Task<IEnumerable<TenantModule>> GetTenantModulesAsync(Guid tenantId)
        {
            return await _context.TenantModules
                .Where(tm => tm.TenantId == tenantId && !tm.IsDeleted)
                .OrderBy(tm => tm.ModuleName)
                .ToListAsync();
        }

        public async Task<TenantModule?> GetTenantModuleAsync(Guid tenantId, string moduleName)
        {
            return await _context.TenantModules
                .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleName == moduleName && !tm.IsDeleted);
        }

        public async Task AddTenantModuleAsync(TenantModule tenantModule)
        {
            tenantModule.Id = Guid.NewGuid();
            tenantModule.CreatedAt = DateTime.UtcNow;
            await _context.TenantModules.AddAsync(tenantModule);
        }

        public Task UpdateTenantModuleAsync(TenantModule tenantModule)
        {
            tenantModule.UpdatedAt = DateTime.UtcNow;
            _context.TenantModules.Update(tenantModule);
            return Task.CompletedTask;
        }

        public override async Task<IEnumerable<Tenant>> GetAllAsync()
        {
            return await _dbSet
                .Include(t => t.TenantModules)
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public override async Task<Tenant> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(t => t.TenantModules)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        }
    }
}
