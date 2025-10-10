using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Procurement;

#region Supplier Management Repository Implementations

public class SupplierRepository : GenericRepository<Supplier>, ISupplierRepository
{
    public SupplierRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Supplier>> GetActiveSuppliers()
    {
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Supplier?> GetBySupplierCodeAsync(string supplierCode)
    {
        if (string.IsNullOrWhiteSpace(supplierCode))
            return null;
            
        return await _dbSet
            .Where(s => s.SupplierCode == supplierCode && !s.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsSupplierCodeUniqueAsync(string supplierCode, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(supplierCode))
            return false;
            
        var query = _dbSet.Where(s => s.SupplierCode == supplierCode && !s.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<Supplier>> GetPreferredSuppliersAsync()
    {
        return await _dbSet
            .Where(s => s.IsPreferred && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByTypeAsync(string supplierType)
    {
        if (string.IsNullOrWhiteSpace(supplierType))
            return new List<Supplier>();
            
        return await _dbSet
            .Where(s => s.SupplierType == supplierType && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> SearchSuppliersAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<Supplier>();
            
        var lowerSearchTerm = searchTerm.ToLower();
        return await _dbSet
            .Where(s => !s.IsDeleted &&
                       (s.SupplierCode.ToLower().Contains(lowerSearchTerm) ||
                        s.Name.ToLower().Contains(lowerSearchTerm) ||
                        (s.Description != null && s.Description.ToLower().Contains(lowerSearchTerm)) ||
                        (s.Email != null && s.Email.ToLower().Contains(lowerSearchTerm))))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByRatingAsync(int minRating)
    {
        return await _dbSet
            .Where(s => s.Rating >= minRating && s.IsActive && !s.IsDeleted)
            .OrderByDescending(s => s.Rating)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Supplier?> GetSupplierWithContactsAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(s => s.Id == supplierId && !s.IsDeleted)
            .Include(s => s.Contacts.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersForInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<Supplier>();
            
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .Where(s => s.ItemCatalogs.Any(ic => ic.InventoryItemId == inventoryItemId && 
                                               ic.IsActive && !ic.IsDeleted))
            .Include(s => s.ItemCatalogs.Where(ic => ic.InventoryItemId == inventoryItemId))
            .OrderBy(s => s.ItemCatalogs.First(ic => ic.InventoryItemId == inventoryItemId).UnitPrice)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets suppliers with contract expiring within specified days
    /// </summary>
    public async Task<IEnumerable<Supplier>> GetSuppliersWithExpiringContractsAsync(int daysAhead = 30)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(daysAhead);
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted &&
                       s.ContractEndDate <= targetDate &&
                       s.ContractEndDate >= DateTime.UtcNow.Date)
            .OrderBy(s => s.ContractEndDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets supplier performance metrics
    /// </summary>
    public async Task<(int TotalOrders, decimal TotalValue, double AverageLeadTime)> GetSupplierPerformanceAsync(Guid supplierId, DateTime startDate, DateTime endDate)
    {
        var orders = await _context.Set<PurchaseOrder>()
            .Where(po => po.SupplierId == supplierId && 
                        !po.IsDeleted &&
                        po.OrderDate >= startDate && 
                        po.OrderDate <= endDate)
            .ToListAsync();
            
        var totalOrders = orders.Count;
        var totalValue = orders.Sum(o => o.TotalAmount);
        var averageLeadTime = orders
            .Where(o => o.PromisedDate.HasValue && o.ReceivedDate.HasValue)
            .Select(o => (o.ReceivedDate!.Value - o.PromisedDate!.Value).TotalDays)
            .DefaultIfEmpty(0)
            .Average();
            
        return (totalOrders, totalValue, averageLeadTime);
    }
    
    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<Supplier>> GetSuppliersAsync(int page, int pageSize, string? search = null, string? status = null, string? supplierType = null, bool? isActive = null, bool? isPreferred = null)
    {
        var query = _dbSet.Where(s => !s.IsDeleted).AsQueryable();
        
        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(s => s.SupplierCode.ToLower().Contains(search) ||
                                   s.Name.ToLower().Contains(search) ||
                                   (s.Description != null && s.Description.ToLower().Contains(search)));
        }
        
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }
        
        if (!string.IsNullOrWhiteSpace(supplierType))
        {
            query = query.Where(s => s.SupplierType == supplierType);
        }
        
        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }
        
        if (isPreferred.HasValue)
        {
            query = query.Where(s => s.IsPreferred == isPreferred.Value);
        }
        
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        return new ErpSystem.Core.DTOs.Common.PagedResult<Supplier>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
    
    public async Task<Supplier?> GetSupplierByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(s => s.Id == id && !s.IsDeleted)
            .FirstOrDefaultAsync();
    }
    
    public async Task<Supplier?> GetSupplierByCodeAsync(string supplierCode)
    {
        return await GetBySupplierCodeAsync(supplierCode);
    }
    
    public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
    {
        return await AddAsync(supplier);
    }
    
    public async Task<Supplier> UpdateSupplierAsync(Supplier supplier)
    {
        await UpdateAsync(supplier);
        return supplier;
    }
    
    public async Task DeleteSupplierAsync(Guid id)
    {
        await DeleteAsync(id);
    }
    
    public async Task<bool> HasActivePurchaseOrdersAsync(Guid supplierId)
    {
        return await _context.Set<PurchaseOrder>()
            .AnyAsync(po => po.SupplierId == supplierId && 
                           !po.IsDeleted &&
                           (po.Status == "Sent" || po.Status == "Acknowledged" || po.Status == "Partial"));
    }
    
    public async Task UpdateSupplierStatusAsync(Guid supplierId, string status)
    {
        var supplier = await GetByIdAsync(supplierId);
        if (supplier != null)
        {
            supplier.Status = status;
            await UpdateAsync(supplier);
        }
    }
}

public class SupplierContactRepository : GenericRepository<SupplierContact>, ISupplierContactRepository
{
    public SupplierContactRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SupplierContact>> GetContactsBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return new List<SupplierContact>();
            
        return await _dbSet
            .Where(sc => sc.SupplierId == supplierId && !sc.IsDeleted)
            .Include(sc => sc.Supplier)
            .OrderByDescending(sc => sc.IsPrimary)
            .ThenBy(sc => sc.Name)
            .ToListAsync();
    }

    public async Task<SupplierContact?> GetPrimaryContactAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(sc => sc.SupplierId == supplierId && sc.IsPrimary && !sc.IsDeleted)
            .Include(sc => sc.Supplier)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SupplierContact>> GetContactsByTypeAsync(string contactType)
    {
        if (string.IsNullOrWhiteSpace(contactType))
            return new List<SupplierContact>();
            
        return await _dbSet
            .Where(sc => sc.ContactType == contactType && !sc.IsDeleted)
            .Include(sc => sc.Supplier)
            .OrderBy(sc => sc.Supplier.Name)
            .ThenBy(sc => sc.Name)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<SupplierContact>> GetContactsBySupplierId(Guid supplierId)
    {
        return await GetContactsBySupplierAsync(supplierId);
    }
    
    public async Task<SupplierContact> CreateContactAsync(SupplierContact contact)
    {
        return await AddAsync(contact);
    }
}

public class SupplierItemCatalogRepository : GenericRepository<SupplierItemCatalog>, ISupplierItemCatalogRepository
{
    public SupplierItemCatalogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SupplierItemCatalog>> GetCatalogBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return new List<SupplierItemCatalog>();
            
        return await _dbSet
            .Where(sic => sic.SupplierId == supplierId && !sic.IsDeleted)
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.SupplierItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierItemCatalog>> GetCatalogByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<SupplierItemCatalog>();
            
        return await _dbSet
            .Where(sic => sic.InventoryItemId == inventoryItemId && !sic.IsDeleted)
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.UnitPrice)
            .ToListAsync();
    }

    public async Task<SupplierItemCatalog?> GetCatalogItemAsync(Guid supplierId, Guid inventoryItemId)
    {
        if (supplierId == Guid.Empty || inventoryItemId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(sic => sic.SupplierId == supplierId && 
                         sic.InventoryItemId == inventoryItemId && 
                         !sic.IsDeleted)
            .Include(sic => sic.Supplier)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SupplierItemCatalog>> GetActiveItemsAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return new List<SupplierItemCatalog>();
            
        return await _dbSet
            .Where(sic => sic.SupplierId == supplierId && 
                         sic.IsActive && 
                         !sic.IsDeleted &&
                         (sic.ExpiryDate == null || sic.ExpiryDate > DateTime.UtcNow))
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.SupplierItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierItemCatalog>> GetPreferredSuppliersForItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<SupplierItemCatalog>();
            
        return await _dbSet
            .Where(sic => sic.InventoryItemId == inventoryItemId && 
                         sic.IsPreferred && 
                         sic.IsActive && 
                         !sic.IsDeleted)
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.UnitPrice)
            .ToListAsync();
    }

    public async Task<SupplierItemCatalog?> GetBestPriceForItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(sic => sic.InventoryItemId == inventoryItemId && 
                         sic.IsActive && 
                         !sic.IsDeleted &&
                         (sic.ExpiryDate == null || sic.ExpiryDate > DateTime.UtcNow))
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.UnitPrice)
            .FirstOrDefaultAsync();
    }
    
    /// <summary>
    /// Gets catalog items that are expiring within specified days
    /// </summary>
    public async Task<IEnumerable<SupplierItemCatalog>> GetExpiringCatalogItemsAsync(int daysAhead = 30)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(daysAhead);
        return await _dbSet
            .Where(sic => sic.IsActive && !sic.IsDeleted &&
                         sic.ExpiryDate.HasValue &&
                         sic.ExpiryDate <= targetDate &&
                         sic.ExpiryDate >= DateTime.UtcNow.Date)
            .Include(sic => sic.Supplier)
            .OrderBy(sic => sic.ExpiryDate)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<SupplierItemCatalog>> GetCatalogBySupplierId(Guid supplierId)
    {
        return await GetCatalogBySupplierAsync(supplierId);
    }
}

#endregion