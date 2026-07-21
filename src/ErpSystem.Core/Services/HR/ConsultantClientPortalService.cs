using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

public sealed class ConsultantClientPortalService : IConsultantClientPortalService
{
    private readonly IGenericRepository<ConsultantClientPortalAccount> _accountRepo;
    private readonly IConsultantTimesheetRepository _timesheetRepo;
    private readonly IConsultantClientRepository _clientRepo;
    private readonly IConsultantTimesheetService _timesheetService;

    public ConsultantClientPortalService(
        IGenericRepository<ConsultantClientPortalAccount> accountRepo,
        IConsultantTimesheetRepository timesheetRepo,
        IConsultantClientRepository clientRepo,
        IConsultantTimesheetService timesheetService)
    {
        _accountRepo = accountRepo;
        _timesheetRepo = timesheetRepo;
        _clientRepo = clientRepo;
        _timesheetService = timesheetService;
    }

    public async Task<ConsultantClientPortalDashboardDto> GetDashboardAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountId, tenantId);
        var client = await _clientRepo.GetByIdAsync(account.ConsultantClientId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        var pending = (await _timesheetRepo.GetByClientIdAsync(client.Id))
            .Where(t => t.Status == TimesheetStatus.SentToClient)
            .OrderByDescending(t => t.PeriodEndDate)
            .ToList();

        return new ConsultantClientPortalDashboardDto
        {
            ConsultantClientId = client.Id,
            ClientName = client.ClientName,
            ClientCode = client.ClientCode,
            Email = account.Email,
            ContactName = account.ContactName,
            PendingConfirmationCount = pending.Count,
            PendingTimesheets = pending.ToSummaryDtoList().ToList(),
        };
    }

    public async Task<ClientTimesheetConfirmationPublicDto> GetTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var account = await GetAccountAsync(accountId, tenantId);
        var timesheet = await _timesheetRepo.GetWithFullDetailsAsync(timesheetId);

        if (timesheet == null || timesheet.TenantId != tenantId)
            throw new ArgumentException("Timesheet not found.");

        if (timesheet.ClientId != account.ConsultantClientId)
            throw new UnauthorizedAccessException("You do not have access to this timesheet.");

        var confirmation = timesheet.Confirmations
            .OrderByDescending(c => c.SentDate)
            .FirstOrDefault();

        var isExpired = confirmation == null
            || confirmation.TokenExpiryDate < DateTime.UtcNow
            || confirmation.Status == TimesheetConfirmationStatus.Expired;

        var canRespond = timesheet.Status == TimesheetStatus.SentToClient
            && confirmation != null
            && confirmation.Status is TimesheetConfirmationStatus.Sent
                or TimesheetConfirmationStatus.Resent
                or TimesheetConfirmationStatus.Viewed
            && !isExpired;

        return new ClientTimesheetConfirmationPublicDto
        {
            TimesheetNumber = timesheet.TimesheetNumber,
            ConsultantName = $"{timesheet.Consultant.FirstName} {timesheet.Consultant.LastName}".Trim(),
            ClientName = timesheet.Client.ClientName,
            PeriodStartDate = timesheet.PeriodStartDate,
            PeriodEndDate = timesheet.PeriodEndDate,
            TotalHours = timesheet.TotalHours,
            Status = confirmation?.Status ?? TimesheetConfirmationStatus.Sent,
            IsExpired = isExpired,
            CanRespond = canRespond,
            TokenExpiryDate = confirmation?.TokenExpiryDate ?? DateTime.UtcNow,
            Entries = timesheet.Entries.OrderBy(e => e.WorkDate).Select(e => e.ToDto()).ToList(),
        };
    }

    public Task<ClientTimesheetConfirmationDto> ConfirmTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        ConsultantClientPortalConfirmTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default)
        => ConfirmOrRejectAsync(accountId, timesheetId, tenantId, confirm: true, dto.ClientNotes, ct);

    public Task<ClientTimesheetConfirmationDto> RejectTimesheetAsync(
        Guid accountId,
        Guid timesheetId,
        ConsultantClientPortalRejectTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default)
        => ConfirmOrRejectAsync(accountId, timesheetId, tenantId, confirm: false, dto.ClientNotes, ct);

    private async Task<ClientTimesheetConfirmationDto> ConfirmOrRejectAsync(
        Guid accountId,
        Guid timesheetId,
        Guid tenantId,
        bool confirm,
        string? clientNotes,
        CancellationToken ct)
    {
        var account = await GetAccountAsync(accountId, tenantId);

        if (!account.IsEmailVerified)
            throw new InvalidOperationException(
                "Please verify your email address before confirming or rejecting timesheets.");

        if (confirm)
        {
            return await _timesheetService.ConfirmByPortalClientAsync(
                timesheetId,
                account.ConsultantClientId,
                clientNotes,
                ct);
        }

        if (string.IsNullOrWhiteSpace(clientNotes))
            throw new InvalidOperationException("A reason is required when rejecting a timesheet.");

        return await _timesheetService.RejectByPortalClientAsync(
            timesheetId,
            account.ConsultantClientId,
            clientNotes.Trim(),
            ct);
    }

    private async Task<ConsultantClientPortalAccount> GetAccountAsync(Guid accountId, Guid tenantId)
    {
        var account = await _accountRepo.GetByIdAsync(accountId)
            ?? throw new UnauthorizedAccessException("Account not found.");

        if (account.TenantId != tenantId || !account.IsActive)
            throw new UnauthorizedAccessException();

        return account;
    }
}
