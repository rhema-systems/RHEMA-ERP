using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The printed recruitment test — the question paper and the marking key. Round 4, lane E6.
/// </summary>
/// <remarks>
/// Returns HTML the client prints; there is deliberately no PDF. A paper is written on and a key is
/// marked from, so the browser's own print is the target — the same decision lane F's interview paper
/// made, for the same reason.
/// </remarks>
public interface IRecruitmentTestPaperService
{
    /// <summary>
    /// Builds a printed paper.
    /// </summary>
    /// <param name="assignmentId">
    /// Question paper only: print one NAMED paper per candidate this assignment reaches. Null prints a
    /// single blank paper with lines for the name. Ignored for the marking key, which has one form.
    /// </param>
    /// <param name="applicationIds">
    /// Narrows the named papers to these candidates — a reprint for the one who spilt coffee on theirs.
    /// ⚠ Each must be one the assignment reaches; an unknown id is refused rather than quietly
    /// skipped, because a test centre handed one paper fewer than it asked for finds out at the desk.
    /// </param>
    Task<RecruitmentTestPaperDto> GenerateAsync(
        Guid testId,
        RecruitmentTestPaperVariant variant,
        Guid? assignmentId,
        IReadOnlyList<Guid>? applicationIds,
        CancellationToken cancellationToken = default);
}
