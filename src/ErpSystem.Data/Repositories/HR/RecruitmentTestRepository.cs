using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <inheritdoc cref="IRecruitmentTestRepository"/>
public class RecruitmentTestRepository : GenericRepository<RecruitmentTest>, IRecruitmentTestRepository
{
    private readonly INumberSequenceService _sequences;

    public RecruitmentTestRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    public async Task<string> GetNextTestCodeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Not year-scoped: a paper outlives the year it was written in, and a code that carries one
        // reads as though it expired.
        return await _sequences.NextUnusedAsync(
            "RTEST",
            year: null,
            format: value => $"TEST-{value:D4}",
            isTaken: code => _dbSet.IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && t.TestCode == code, cancellationToken),
            highestIssued: async () => NumberSequenceExtensions.HighestIssued(
                await _dbSet.IgnoreQueryFilters()
                    .Where(t => t.TenantId == tenantId)
                    .Select(t => t.TestCode)
                    .ToListAsync(cancellationToken)),
            cancellationToken: cancellationToken);
    }

    public async Task<RecruitmentTest?> GetWithFullPaperAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.Sections)
            .Include(t => t.Questions).ThenInclude(q => q.Options)
            .Include(t => t.Questions).ThenInclude(q => q.Section)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);
    }
}
