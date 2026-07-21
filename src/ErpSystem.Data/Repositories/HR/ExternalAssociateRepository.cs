using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class ExternalAssociateRepository
    : GenericRepository<ExternalAssociate>, IExternalAssociateRepository
{
    public ExternalAssociateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ExternalAssociate?> GetByAssociateNumberAsync(string associateNumber) =>
        await _dbSet
            .FirstOrDefaultAsync(a => a.AssociateNumber == associateNumber && !a.IsDeleted);

    public async Task<ExternalAssociate?> GetByEmailAsync(string email) =>
        await _dbSet
            .FirstOrDefaultAsync(a => a.Email == email.ToLowerInvariant() && !a.IsDeleted);

    public async Task<IEnumerable<ExternalAssociate>> GetActiveAsync() =>
        await _dbSet
            .Where(a => a.IsActive && !a.IsDeleted)
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .ToListAsync();

    public async Task<IEnumerable<ExternalAssociate>> SearchAsync(string q, int limit = 20)
    {
        var term = q.Trim().ToLower();
        return await _dbSet
            .Where(a => !a.IsDeleted
                     && a.IsActive
                     && (a.FirstName.ToLower().Contains(term)
                      || a.LastName.ToLower().Contains(term)
                      || (a.FirstName.ToLower() + " " + a.LastName.ToLower()).Contains(term)
                      || a.Email.ToLower().Contains(term)
                      || (a.CompanyName != null && a.CompanyName.ToLower().Contains(term))
                      || a.AssociateNumber.ToLower().Contains(term)))
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> AssociateNumberExistsAsync(string associateNumber, Guid? excludeId = null) =>
        await _dbSet.AnyAsync(a =>
            !a.IsDeleted
            && a.AssociateNumber == associateNumber
            && (excludeId == null || a.Id != excludeId.Value));

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeId = null) =>
        await _dbSet.AnyAsync(a =>
            !a.IsDeleted
            && a.Email == email.ToLowerInvariant()
            && (excludeId == null || a.Id != excludeId.Value));

    public async Task<string> GenerateAssociateNumberAsync()
    {
        var last = await _dbSet
            .Where(a => a.AssociateNumber.StartsWith("EXT-"))
            .OrderByDescending(a => a.AssociateNumber)
            .Select(a => a.AssociateNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null && int.TryParse(last.Replace("EXT-", ""), out var parsed))
            next = parsed + 1;

        return $"EXT-{next:D4}";
    }
}
