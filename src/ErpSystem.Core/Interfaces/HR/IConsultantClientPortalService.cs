using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The consultant-client contact's own portal surface (main JWT scheme, ConsultantClient role
/// since 2026-08-31). The actor is the Identity user id from the token; authorisation inside is
/// by the caller's active <c>ConsultantClientContact</c> rows — no contact row, no client data.
/// </summary>
public interface IConsultantClientPortalService
{
    Task<ConsultantClientPortalDashboardDto> GetDashboardAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationPublicDto> GetTimesheetAsync(
        Guid userId,
        Guid timesheetId,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationDto> ConfirmTimesheetAsync(
        Guid userId,
        Guid timesheetId,
        ConsultantClientPortalConfirmTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationDto> RejectTimesheetAsync(
        Guid userId,
        Guid timesheetId,
        ConsultantClientPortalRejectTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default);
}
