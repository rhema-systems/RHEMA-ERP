using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class TenderAssignmentRepository : GenericRepository<TenderAssignment>, ITenderAssignmentRepository
{
    public TenderAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<TenderAssignment?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(ta => ta.Tender)
            .Include(ta => ta.BusinessPartner)
            .Include(ta => ta.AssignedToUser)
            .Include(ta => ta.AssignedBy)
            .Where(ta => ta.Id == id && !ta.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderAssignment>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Include(ta => ta.BusinessPartner)
            .Include(ta => ta.AssignedToUser)
            .Include(ta => ta.AssignedBy)
            .Where(ta => ta.TenderId == tenderId && !ta.IsDeleted)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderAssignment>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Include(ta => ta.Tender)
            .Include(ta => ta.BusinessPartner)
            .Where(ta => (ta.AssignedToUserId == userId || ta.AssignmentType == "AllUsers") && !ta.IsDeleted)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderAssignment>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Include(ta => ta.Tender)
            .Include(ta => ta.AssignedToUser)
            .Where(ta => ta.BusinessPartnerId == businessPartnerId && !ta.IsDeleted)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderAssignment>> GetByTenderAndBusinessPartnerAsync(Guid tenderId, Guid businessPartnerId)
    {
        return await _dbSet
            .Include(ta => ta.AssignedToUser)
            .Where(ta => ta.TenderId == tenderId && ta.BusinessPartnerId == businessPartnerId && !ta.IsDeleted)
            .ToListAsync();
    }

    public async Task<TenderAssignment> CreateAsync(TenderAssignment entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
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

    public async Task<bool> ExistsAsync(Guid tenderId, Guid businessPartnerId, Guid? userId = null)
    {
        var query = _dbSet.Where(ta => ta.TenderId == tenderId && ta.BusinessPartnerId == businessPartnerId && !ta.IsDeleted);
        
        if (userId.HasValue)
        {
            query = query.Where(ta => ta.AssignedToUserId == userId.Value);
        }

        return await query.AnyAsync();
    }
}

