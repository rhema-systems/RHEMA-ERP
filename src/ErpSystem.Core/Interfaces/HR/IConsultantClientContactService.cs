using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// HR-side administration of consultant-client contacts: main-scheme Identity accounts
/// (ConsultantClient role) linked to a client organisation by a <c>ConsultantClientContact</c>
/// row. Replaced <c>IConsultantClientPortalAuthService</c> 2026-08-31 when the bespoke
/// PortalBearer portal was retired — Identity owns every credential concern now, and this
/// service owns only the invite lifecycle and the contact link.
/// </summary>
public interface IConsultantClientContactService
{
    /// <summary>
    /// Invites a contact onto the client's portal surface: creates (or adopts) the Identity
    /// account, links it to the client, and emails a setup link. Adoption is refused for any
    /// existing account that is not purely a consultant-client one — an internal or
    /// business-partner account must never be quietly widened onto a client's timesheets.
    /// </summary>
    Task<ConsultantClientContactSummaryDto> InviteContactAsync(
        Guid consultantClientId,
        ConsultantClientPortalInviteDto dto,
        Guid tenantId,
        Guid invitedByEmployeeId,
        CancellationToken ct = default);

    /// <summary>Re-sends the invite/setup email with a fresh token, behind a per-contact cooldown.</summary>
    Task ResendInviteAsync(
        Guid consultantClientId,
        string email,
        Guid tenantId,
        CancellationToken ct = default);

    Task<IEnumerable<ConsultantClientContactSummaryDto>> GetContactsForClientAsync(
        Guid consultantClientId,
        Guid tenantId,
        CancellationToken ct = default);
}
