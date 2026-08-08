using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IConsultantClientPortalService
{
    Task<ConsultantClientPortalDashboardDto> GetDashboardAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationPublicDto> GetTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationDto> ConfirmTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        ConsultantClientPortalConfirmTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ClientTimesheetConfirmationDto> RejectTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        ConsultantClientPortalRejectTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default);
}
