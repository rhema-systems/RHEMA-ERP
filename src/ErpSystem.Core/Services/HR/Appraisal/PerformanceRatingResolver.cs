using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Resolves an overall score to a <see cref="PerformanceRating"/> using the tenant's configurable
/// <see cref="AppraisalGradeDefinition"/> overall bands, falling back to the fixed
/// <see cref="AppraisalScoring.MapScoreToRating"/> bands when no bands are configured.
/// </summary>
public class PerformanceRatingResolver : IPerformanceRatingResolver
{
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeRepository;
    private List<(decimal Min, decimal Max, PerformanceRating Rating)>? _bands;

    public PerformanceRatingResolver(IGenericRepository<AppraisalGradeDefinition> gradeRepository)
    {
        _gradeRepository = gradeRepository;
    }

    public async Task<PerformanceRating?> ResolveAsync(decimal? score, CancellationToken cancellationToken = default)
    {
        var mapper = await GetMapperAsync(cancellationToken);
        return mapper(score);
    }

    public async Task<Func<decimal?, PerformanceRating?>> GetMapperAsync(CancellationToken cancellationToken = default)
    {
        var bands = await GetBandsAsync(cancellationToken);

        return score =>
        {
            if (!score.HasValue) return null;
            if (bands.Count == 0)
                return AppraisalScoring.MapScoreToRating(score); // no configured bands → fixed fallback

            var s = score.Value;
            foreach (var b in bands)
            {
                if (s >= b.Min && s <= b.Max)
                    return b.Rating;
            }
            // Score falls outside every configured band — fall back rather than returning null.
            return AppraisalScoring.MapScoreToRating(score);
        };
    }

    private async Task<List<(decimal Min, decimal Max, PerformanceRating Rating)>> GetBandsAsync(CancellationToken cancellationToken)
    {
        if (_bands != null) return _bands;

        var defs = await _gradeRepository.GetQueryable()
            .Where(g => g.IsActive
                        && g.MappedRating != null
                        && g.OverallMinScore != null
                        && g.OverallMaxScore != null)
            .ToListAsync(cancellationToken);

        _bands = defs
            .Select(g => (g.OverallMinScore!.Value, g.OverallMaxScore!.Value, g.MappedRating!.Value))
            // Highest band first so the strongest matching rating wins on any overlap.
            .OrderByDescending(b => b.Item1)
            .ToList();

        return _bands;
    }
}
