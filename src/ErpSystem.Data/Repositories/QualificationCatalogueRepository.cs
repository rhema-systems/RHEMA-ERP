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

    /// <summary>
    /// Every read loads the ladder rung, because the DTO reports its name and rank.
    /// </summary>
    /// <remarks>
    /// ⚠ A resolved name on a DTO that no read populates is the most repeated defect in this
    /// module — it compiles, returns 200, and is silently null for ever. The Include is not an
    /// optimisation, it is what makes `QualificationLevelName` true.
    /// </remarks>
    private IQueryable<Qualification> WithLevel()
        => _context.Set<Qualification>().Include(q => q.QualificationLevel);

    public override async Task<Qualification?> GetByIdAsync(Guid id)
        => await WithLevel().FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

    public override async Task<IEnumerable<Qualification>> GetAllAsync()
        => await WithLevel().Where(q => !q.IsDeleted).OrderBy(q => q.Name).ToListAsync();

    public async Task<IEnumerable<Qualification>> GetActiveAsync()
        => await WithLevel()
            .Where(q => !q.IsDeleted && q.IsActive)
            .OrderBy(q => q.Name)
            .ToListAsync();

    public async Task<IEnumerable<Qualification>> GetByTypeAsync(QualificationType type)
        => await WithLevel()
            .Where(q => !q.IsDeleted && q.Type == type)
            .OrderBy(q => q.Name)
            .ToListAsync();

    public async Task<Qualification?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return await WithLevel()
            .FirstOrDefaultAsync(q => !q.IsDeleted && q.Name == name);
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return await _context.Set<Qualification>()
            .AnyAsync(q => !q.IsDeleted && q.Name == name);
    }
}
