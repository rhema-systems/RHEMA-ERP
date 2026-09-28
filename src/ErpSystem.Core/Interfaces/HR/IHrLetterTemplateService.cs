using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// HR's letter and email templates (round 4, lane N): every email the HR modules send, listed from
/// their catalogues, with the tenant's own wording where HR has chosen one.
/// </summary>
/// <remarks>
/// Scope: the <c>EmailTemplate</c> + <c>IEmailEventCatalog</c> system only. In-app notifications,
/// maintenance notification templates and the platform's database-field email designer are separate
/// systems, untouched here.
/// </remarks>
public interface IHrLetterTemplateService
{
    /// <summary>Every event of every HR catalogue, with whether the tenant has its own wording.</summary>
    Task<IReadOnlyList<HrLetterTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One event: what goes out now, the shipped default, and the tokens it may use.</summary>
    Task<HrLetterTemplateDto> GetAsync(string module, string eventKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the tenant's own wording. Refused, with every reason, for a subject or body whose
    /// structure is broken, that names a token the event does not supply, or that places text a
    /// person typed (a name, a note) without escaping it.
    /// </summary>
    Task<HrLetterTemplateDto> SaveAsync(string module, string eventKey, SaveHrLetterTemplateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Renders unsaved (or current) wording with the catalogue's sample values. Changes nothing.</summary>
    Task<HrLetterTemplatePreviewDto> PreviewAsync(string module, string eventKey, PreviewHrLetterTemplateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Emails a sample to the signed-in officer themselves — nobody else — and says what happened.</summary>
    Task<HrLetterTemplateTestSendResultDto> TestSendAsync(string module, string eventKey, PreviewHrLetterTemplateDto dto, CancellationToken cancellationToken = default);

    /// <summary>Drops the tenant's wording; the shipped default goes out again.</summary>
    Task<HrLetterTemplateDto> ResetAsync(string module, string eventKey, CancellationToken cancellationToken = default);
}
