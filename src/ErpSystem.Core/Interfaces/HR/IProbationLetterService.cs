using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Produces the FR-HR-032 probation confirmation letter.
/// </summary>
public interface IProbationLetterService
{
    /// <summary>
    /// Renders the confirmation letter for a <b>confirmed</b> probation.
    /// </summary>
    /// <remarks>
    /// Refuses a probation that is still running or was terminated: the FRD chain is
    /// <i>head confirms then HR issues the letter</i>, so the letter reports a decision rather
    /// than making one.
    /// </remarks>
    Task<ProbationConfirmationLetterDto> GenerateConfirmationLetterAsync(
        Guid probationId, CancellationToken cancellationToken = default);
}
