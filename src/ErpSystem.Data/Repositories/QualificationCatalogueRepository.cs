using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for the Qualification master catalogue.
/// </summary>
public sealed class QualificationCatalogueRepository : GenericRepository<Qualification>, IQualificationCatalogueRepository
{
    public QualificationCatalogueRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Qualification>> GetActiveAsync()
        => await _context.Set<Qualification>()
            .Where(q => !q.IsDeleted && q.IsActive)
            .OrderBy(q => q.Name)
            .ToListAsync();

    public async Task<IEnumerable<Qualification>> GetByTypeAsync(QualificationType type)
        => await _context.Set<Qualification>()
            .Where(q => !q.IsDeleted && q.Type == type)
            .OrderBy(q => q.Name)
            .ToListAsync();

    public async Task<Qualification?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return await _context.Set<Qualification>()
            .FirstOrDefaultAsync(q => !q.IsDeleted && q.Name == name);
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return await _context.Set<Qualification>()
            .AnyAsync(q => !q.IsDeleted && q.Name == name);
    }
}
