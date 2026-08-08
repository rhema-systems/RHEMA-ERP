using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// RECRUITMENT ANALYTICS SERVICE
// ============================================================================

public interface IRecruitmentAnalyticsService
{
    /// <summary>
    /// Aggregates recruitment analytics for a year: time-to-fill/hire, cost-per-hire,
    /// source effectiveness, funnel conversion, offer accept/decline, vacancy ageing
    /// and recruiter load. Ageing and recruiter load are point-in-time (current open
    /// work), everything else is filtered to the requested year.
    /// </summary>
    Task<RecruitmentAnalyticsDto> GetAnalyticsAsync(int? year = null, CancellationToken cancellationToken = default);
}
