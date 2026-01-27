using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class BusinessPartnerUserRepository : GenericRepository<BusinessPartnerUser>, IBusinessPartnerUserRepository
{
    public BusinessPartnerUserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<BusinessPartnerUser?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(bpu => bpu.BusinessPartner)
            .Include(bpu => bpu.User)
            .Include(bpu => bpu.GrantedBy)
            .Where(bpu => bpu.Id == id && !bpu.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerUser?> GetByUserIdAsync(Guid userId)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering for external users
        // External users need to access their business partner user link regardless of tenant context
        return await _dbSet
            .IgnoreQueryFilters()
            .Include(bpu => bpu.BusinessPartner)
            .Include(bpu => bpu.User)
            .Where(bpu => bpu.UserId == userId && !bpu.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerUser?> GetByBusinessPartnerAndUserAsync(Guid businessPartnerId, Guid userId)
    {
        return await _dbSet
            .Include(bpu => bpu.BusinessPartner)
            .Include(bpu => bpu.User)
            .Where(bpu => bpu.BusinessPartnerId == businessPartnerId && bpu.UserId == userId && !bpu.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BusinessPartnerUser>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
            return await _dbSet
            .Include(bpu => bpu.User)
            .Include(bpu => bpu.GrantedBy)
            .Where(bpu => bpu.BusinessPartnerId == businessPartnerId && !bpu.IsDeleted)
            .OrderByDescending(bpu => bpu.GrantedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerUser>> GetActiveByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Include(bpu => bpu.User)
            .Where(bpu => bpu.BusinessPartnerId == businessPartnerId && bpu.IsActive && !bpu.IsDeleted)
            .OrderBy(bpu => bpu.User!.FullName)
            .ToListAsync();
    }

    public async Task<BusinessPartnerUser> CreateAsync(BusinessPartnerUser entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task<BusinessPartnerUser> UpdateAsync(BusinessPartnerUser entity)
    {
        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            entity.IsDeleted = true;
            _dbSet.Update(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid businessPartnerId, Guid userId)
    {
        return await _dbSet
            .AnyAsync(bpu => bpu.BusinessPartnerId == businessPartnerId && bpu.UserId == userId && !bpu.IsDeleted);
    }
}

