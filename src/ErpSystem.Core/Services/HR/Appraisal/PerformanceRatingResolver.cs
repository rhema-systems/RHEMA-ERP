using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The one place an overall score becomes a grade and a <see cref="PerformanceRating"/>: the tenant's
/// configurable <see cref="AppraisalGradeDefinition"/> overall bands, falling back to the fixed
/// <see cref="AppraisalScoring.MapScoreToRating"/> bands when none are configured.
/// </summary>
/// <remarks>
/// ⚠ There used to be three resolvers — this one, and one each in the appraisal and calibration
/// services — and they disagreed: this one skipped a band with no mapped rating and matched the
/// highest band first, the other two matched whichever band the database returned first and
/// ignored the rating. The same score could be graded one way at finalisation and rated another
/// way in the talent pools (performance closure A2).
///
/// A band's minimum is its threshold. Bands are matched highest minimum first, and a score falls
/// in the first band whose minimum it reaches — so a score between two bands' published ranges
/// (90.5 between 76–90 and 91–100) takes the lower band, because it has not reached the higher
/// one's threshold, and a score above the top band's maximum takes the top band. The save path
/// refuses overlapping bands, so the order never decides between two bands that both claim a
/// score.
/// </remarks>
public class PerformanceRatingResolver : IPerformanceRatingResolver
{
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private List<Band>? _bands;

    private sealed record Band(Guid Id, string Name, decimal Min, decimal Max, PerformanceRating? Rating);

    public PerformanceRatingResolver(
        IGenericRepository<AppraisalGradeDefinition> gradeRepository,
        ICurrentUserProvider currentUserProvider)
    {
        _gradeRepository = gradeRepository;
        _currentUserProvider = currentUserProvider;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
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

            // No band covers the score (none configured, or the score is below the lowest
            // threshold), or the band carries no rating — the fixed bands answer instead of null.
            return Match(bands, score.Value)?.Rating ?? AppraisalScoring.MapScoreToRating(score);
        };
    }

    public async Task<Guid?> ResolveGradeDefinitionIdAsync(decimal? score, CancellationToken cancellationToken = default)
    {
        if (!score.HasValue) return null;

        var bands = await GetBandsAsync(cancellationToken);

        // Null when no band covers the score: grades are optional configuration, and an ungraded
        // appraisal is better than a mislabelled one.
        return Match(bands, score.Value)?.Id;
    }

    private static Band? Match(List<Band> bands, decimal score)
        => bands.FirstOrDefault(b => score >= b.Min);

    private async Task<List<Band>> GetBandsAsync(CancellationToken cancellationToken)
    {
        if (_bands != null) return _bands;

        var tenantId = GetTenantId();
        var defs = await _gradeRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId
                        && g.IsActive
                        && g.OverallMinScore != null
                        && g.OverallMaxScore != null)
            .Select(g => new { g.Id, g.GradeName, g.OverallMinScore, g.OverallMaxScore, g.MappedRating })
            .ToListAsync(cancellationToken);

        _bands = defs
            .Select(g => new Band(g.Id, g.GradeName, g.OverallMinScore!.Value, g.OverallMaxScore!.Value, g.MappedRating))
            .OrderByDescending(b => b.Min)
            .ThenBy(b => b.Id)
            .ToList();

        return _bands;
    }
}
