using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Objectively queries current employees as potential talent-pool members or succession
/// candidates, computing age, service-years-left, latest appraisal, competency/skill gaps
/// against a target position, and a composite fit score.
/// </summary>
public interface ISuccessionCandidateSearchService
{
    Task<IReadOnlyList<SuccessionCandidateSearchResultDto>> SearchAsync(
        SuccessionCandidateSearchDto criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores a plan's existing candidates against the plan's position and proposes a fit-based
    /// ranking (best fit → rank 1). Used to surface fit next to the current rank and to power a
    /// "re-rank by fit" suggestion.
    /// </summary>
    Task<IReadOnlyList<CandidateFitScoreDto>> ScorePlanCandidatesAsync(
        Guid planId, CancellationToken cancellationToken = default);
}
