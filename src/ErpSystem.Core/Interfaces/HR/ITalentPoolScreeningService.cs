using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Round 4, lane B — screening the talent pool by criteria that mean something, then acting on
/// the result.
/// </summary>
/// <remarks>
/// <para>Before this, the pool could only be matched by a blind 40/30/20 rubric over experience,
/// work mode and availability, and there was <b>no path at all</b> from a pool member to an
/// application or an interview: <c>JobInterviewee</c> requires a <c>JobApplicationId</c>, and
/// nothing created one. So a recruiter could find the right person in the pool and then had to
/// start again by hand.</para>
///
/// <para>Screening runs the vacancy's own live <c>JobShortlistingCriteria</c> through the same
/// <c>ShortlistingEvaluator</c> an application is scored by — not a second rubric that resembles
/// it. The ad-hoc door answers "who do we have?" when no vacancy is open yet.</para>
/// </remarks>
public interface ITalentPoolScreeningService
{
    /// <summary>Screens the pool against one vacancy's live shortlisting criteria.</summary>
    Task<TalentPoolScreenResultDto> ScreenAgainstVacancyAsync(
        Guid vacancyId, TalentPoolScreenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Screens the pool against criteria supplied in the request and stored nowhere — "who do we
    /// have who could do this?", asked before a requisition exists.
    /// </summary>
    Task<TalentPoolScreenResultDto> ScreenAdHocAsync(
        TalentPoolScreenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an application for each named pool member against a vacancy, logs the engagement and
    /// invites them by email. Partial: every row reports its own outcome.
    /// </summary>
    Task<RecruitmentBulkOperationResultDto> InviteToApplyAsync(
        TalentPoolInviteToApplyDto dto, Guid actingEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books each named pool member into an existing interview session, through the application
    /// they already hold against that interview's vacancy. Partial, with per-row reasons.
    /// </summary>
    Task<RecruitmentBulkOperationResultDto> BookForInterviewAsync(
        TalentPoolBookInterviewDto dto, Guid actingEmployeeId, CancellationToken cancellationToken = default);
}
