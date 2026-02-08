using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class ProcurementSettingsRepository : GenericRepository<ProcurementSettings>, IProcurementSettingsRepository
{
    public ProcurementSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ProcurementSettings?> GetByTenantIdAsync(Guid tenantId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted);
    }

    public async Task<ProcurementSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId)
    {
        var settings = await GetByTenantIdAsync(tenantId);
        
        if (settings == null)
        {
            // Create default settings for this tenant
            settings = new ProcurementSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AutoCreateInventoryItems = false,
                AutoCreateSupplierItems = false,
                AllowNonInventoryItems = true,
                DefaultValuationMethod = "FIFO",
                RequireApprovalForPO = true,
                AllowBackorders = true,
                RequireDeliveryDate = true,
                EnforceSupplierCatalog = false,
                AllowMultipleSuppliersPerItem = true,
                ValidateBudgetBeforePO = false,
                RequireContractForPO = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };

            await _dbSet.AddAsync(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }
}
