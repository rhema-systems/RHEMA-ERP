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
    private readonly ICurrentUserProvider _currentUserProvider;

    public ConsultantClientPortalService(
        IGenericRepository<ConsultantClientPortalAccount> accountRepo,
        IConsultantTimesheetRepository timesheetRepo,
        IConsultantClientRepository clientRepo,
        IConsultantTimesheetService timesheetService,
        ICurrentUserProvider currentUserProvider)
    {
        _accountRepo = accountRepo;
        _timesheetRepo = timesheetRepo;
        _clientRepo = clientRepo;
        _timesheetService = timesheetService;
        _currentUserProvider = currentUserProvider;
    }

    // Portal JWT supplies tenantId. If CurrentUser also has a tenant (e.g. staff impersonation /
    // dual context), it must match; empty CurrentUser TenantId is allowed for portal-only sessions.
    private Guid RequireCurrentTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));

        var current = _currentUserProvider.TenantId;
        if (current != Guid.Empty && current != tenantId)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        return tenantId;
    }

    /// <summary>
    /// Refuses portal data to an account that has not proven ownership of its email address.
    /// </summary>
    /// <remarks>
    /// Login no longer issues tokens to unverified accounts, so this is defence in depth: the
    /// database stays authoritative even if a token asserts otherwise. Timesheet data names
    /// consultants and their hours, so it should not be reachable on an unproven mailbox.
    /// </remarks>
    private static void RequireVerifiedEmail(ConsultantClientPortalAccount account)
    {
        if (!account.IsEmailVerified)
            throw new UnauthorizedAccessException(
                "Please verify your email address before using the client portal.");
    }

    public async Task<ConsultantClientPortalDashboardDto> GetDashboardAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetAccountAsync(accountId, tenantId);
        RequireVerifiedEmail(account);
        var client = await _clientRepo.FirstOrDefaultAsync(
            c => c.Id == account.ConsultantClientId && c.TenantId == tenantId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        var pending = (await _timesheetRepo.GetByClientIdAsync(client.Id))
            .Where(t => t.TenantId == tenantId && t.Status == TimesheetStatus.SentToClient)
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
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetAccountAsync(accountId, tenantId);
        RequireVerifiedEmail(account);
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
        tenantId = RequireCurrentTenant(tenantId);
        var account = await GetAccountAsync(accountId, tenantId);

        if (!account.IsEmailVerified)
            throw new InvalidOperationException(
                "Please verify your email address before confirming or rejecting timesheets.");

        var timesheet = await _timesheetRepo.GetByIdAsync(timesheetId);
        if (timesheet == null || timesheet.TenantId != tenantId)
            throw new ArgumentException("Timesheet not found.");

        if (timesheet.ClientId != account.ConsultantClientId)
            throw new UnauthorizedAccessException("You do not have access to this timesheet.");

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
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.Id == accountId && a.TenantId == tenantId)
            ?? throw new UnauthorizedAccessException("Account not found.");

        if (!account.IsActive)
            throw new UnauthorizedAccessException();

        return account;
    }
}
