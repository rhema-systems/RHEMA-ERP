using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The printed interview paper — round 4, lane F.
/// </summary>
/// <remarks>
/// Ported from the solution HR came from, where a scoring sheet existed at
/// <c>Pages\Recruitment\InterviewScorecard.razor</c> and was never brought across. Returns HTML the
/// client prints; there is deliberately no PDF, because a scoring sheet is written on and signed and
/// the browser's own print is the target.
/// </remarks>
public interface IInterviewPaperService
{
    /// <summary>
    /// Builds the paper for one interview.
    /// </summary>
    /// <param name="variant">Which paper — the sheets, the questions alone, or the whole pack.</param>
    /// <param name="panelistId">
    /// Print only this panelist's sheets. Null prints one per panelist per candidate, which is what
    /// a pack is; the ported original could only ever print the single current panelist.
    /// </param>
    /// <param name="intervieweeIds">
    /// Print only these candidates. Null prints everyone booked in, ordered by slot so the sheets
    /// come off the printer in the order the panel will see people.
    /// </param>
    Task<InterviewPaperDto> GenerateAsync(
        Guid interviewId,
        InterviewPaperVariant variant,
        Guid? panelistId,
        IReadOnlyList<Guid>? intervieweeIds,
        CancellationToken cancellationToken = default);
}
