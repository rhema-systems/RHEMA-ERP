using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The responsibility-and-terms document an employee signs for a company asset — AST-5 and AST-5b,
/// decision D7.
/// </summary>
/// <remarks>
/// <para>Rendered from the HR-editable <c>Assets/AssetResponsibilityTerms</c> template, the way
/// every other HR letter is produced (offer, probation confirmation): a self-contained HTML document
/// the browser can print for physical signature, and the same document by email.</para>
///
/// <para>Two routes over ONE render — a printed form and an emailed one that disagree about what
/// somebody is responsible for is worse than having neither.</para>
/// </remarks>
public interface IAssetTermsLetterService
{
    /// <summary>
    /// Renders the assignment's responsibility document for display and print.
    /// </summary>
    /// <remarks>
    /// Deliberately records nothing on the assignment. Printing a copy is not serving it on
    /// somebody, and stamping "sent" here would let an unsent form look served.
    /// </remarks>
    Task<AssetTermsLetterDto> GenerateAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the same document to the holder's recorded email address, and records the send.
    /// </summary>
    Task<AssetTermsLetterSendResultDto> EmailAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
