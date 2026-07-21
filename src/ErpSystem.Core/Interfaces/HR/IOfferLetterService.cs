using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Generates the formal offer-of-employment letter for a <c>JobOffer</c>. Assembles the enriched
/// token set (position/grade/staff-level, reporting line, itemised salary breakdown, position
/// benefits, job summary + key duties, bargaining-unit status, pre-employment conditions, letterhead
/// and signature blocks) and renders it through the HR-editable "OfferLetter" email template via
/// <c>ITemplatedEmailService</c>. The result is used for the portal view, print-to-PDF, and the
/// offer email body.
/// </summary>
public interface IOfferLetterService
{
    /// <summary>Renders the offer letter for the given offer id. Throws when the offer is not found.</summary>
    Task<OfferLetterDto> GenerateAsync(Guid offerId, CancellationToken cancellationToken = default);
}
