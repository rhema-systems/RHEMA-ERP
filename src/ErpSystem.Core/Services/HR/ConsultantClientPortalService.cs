using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Identity;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The consultant-client contact's portal surface, on the main JWT scheme since 2026-08-31.
/// The actor is the token's Identity user; authorisation is by the caller's active
/// <c>ConsultantClientContact</c> rows — a guessed timesheet id outside the caller's clients
/// is a refusal, never a disclosure. Account state (active, email confirmed) is read from the
/// Identity STORE, not token claims: a stale claim minted before deactivation must not keep
/// the door open.
/// </summary>
public sealed class ConsultantClientPortalService : IConsultantClientPortalService
{
    private readonly IGenericRepository<ConsultantClientContact> _contactRepo;
    private readonly IConsultantTimesheetRepository _timesheetRepo;
    private readonly IConsultantTimesheetService _timesheetService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserProvider _currentUserProvider;

    public ConsultantClientPortalService(
        IGenericRepository<ConsultantClientContact> contactRepo,
        IConsultantTimesheetRepository timesheetRepo,
        IConsultantTimesheetService timesheetService,
        UserManager<ApplicationUser> userManager,
        ICurrentUserProvider currentUserProvider)
    {
        _contactRepo = contactRepo;
        _timesheetRepo = timesheetRepo;
        _timesheetService = timesheetService;
        _userManager = userManager;
        _currentUserProvider = currentUserProvider;
    }

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
    /// The caller's live account plus every active contact row (client) it holds. Refuses a
    /// deactivated or unconfirmed account outright — timesheet data names consultants and
    /// their hours, so it is not reachable on an unproven mailbox.
    /// </summary>
    private async Task<(ApplicationUser User, List<ConsultantClientContact> Contacts)> GetPortalContextAsync(
        Guid userId,
        Guid tenantId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("Account not found.");

        if (!user.IsActive || !user.EmailConfirmed)
            throw new UnauthorizedAccessException(
                "Please complete your account setup before using the client portal.");

        var contacts = (await _contactRepo.FindAsync(
                c => c.TenantId == tenantId && c.UserId == userId && c.IsActive,
                c => c.ConsultantClient))
            .Where(c => c.ConsultantClient != null && c.ConsultantClient.IsActive)
            .ToList();

        if (contacts.Count == 0)
            throw new UnauthorizedAccessException("This account has no client portal access.");

        return (user, contacts);
    }

    public async Task<ConsultantClientPortalDashboardDto> GetDashboardAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var (user, contacts) = await GetPortalContextAsync(userId, tenantId);

        var sections = new List<ConsultantClientPortalClientSectionDto>();
        foreach (var contact in contacts.OrderBy(c => c.ConsultantClient.ClientName))
        {
            var pending = (await _timesheetRepo.GetByClientIdAsync(contact.ConsultantClientId))
                .Where(t => t.TenantId == tenantId && t.Status == TimesheetStatus.SentToClient)
                .OrderByDescending(t => t.PeriodEndDate)
                .ToList();

            sections.Add(new ConsultantClientPortalClientSectionDto
            {
                ConsultantClientId = contact.ConsultantClientId,
                ClientName = contact.ConsultantClient.ClientName,
                ClientCode = contact.ConsultantClient.ClientCode,
                ContactRole = contact.ContactRole,
                PendingConfirmationCount = pending.Count,
                PendingTimesheets = pending.ToSummaryDtoList().ToList(),
            });
        }

        return new ConsultantClientPortalDashboardDto
        {
            Email = user.Email ?? string.Empty,
            ContactName = contacts.Select(c => c.ContactName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? user.FullName,
            PendingConfirmationCount = sections.Sum(s => s.PendingConfirmationCount),
            Clients = sections,
        };
    }

    public async Task<ClientTimesheetConfirmationPublicDto> GetTimesheetAsync(
        Guid userId,
        Guid timesheetId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var (_, contacts) = await GetPortalContextAsync(userId, tenantId);
        var timesheet = await _timesheetRepo.GetWithFullDetailsAsync(timesheetId);

        if (timesheet == null || timesheet.TenantId != tenantId)
            throw new ArgumentException("Timesheet not found.");

        if (!contacts.Any(c => c.ConsultantClientId == timesheet.ClientId))
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
        Guid userId,
        Guid timesheetId,
        ConsultantClientPortalConfirmTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default)
        => ConfirmOrRejectAsync(userId, timesheetId, tenantId, confirm: true, dto.ClientNotes, ct);

    public Task<ClientTimesheetConfirmationDto> RejectTimesheetAsync(
        Guid userId,
        Guid timesheetId,
        ConsultantClientPortalRejectTimesheetDto dto,
        Guid tenantId,
        CancellationToken ct = default)
        => ConfirmOrRejectAsync(userId, timesheetId, tenantId, confirm: false, dto.ClientNotes, ct);

    private async Task<ClientTimesheetConfirmationDto> ConfirmOrRejectAsync(
        Guid userId,
        Guid timesheetId,
        Guid tenantId,
        bool confirm,
        string? clientNotes,
        CancellationToken ct)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var (_, contacts) = await GetPortalContextAsync(userId, tenantId);

        var timesheet = await _timesheetRepo.GetByIdAsync(timesheetId);
        if (timesheet == null || timesheet.TenantId != tenantId)
            throw new ArgumentException("Timesheet not found.");

        var contact = contacts.FirstOrDefault(c => c.ConsultantClientId == timesheet.ClientId)
            ?? throw new UnauthorizedAccessException("You do not have access to this timesheet.");

        if (confirm)
        {
            return await _timesheetService.ConfirmByPortalClientAsync(
                timesheetId,
                contact.ConsultantClientId,
                clientNotes,
                ct);
        }

        if (string.IsNullOrWhiteSpace(clientNotes))
            throw new InvalidOperationException("A reason is required when rejecting a timesheet.");

        return await _timesheetService.RejectByPortalClientAsync(
            timesheetId,
            contact.ConsultantClientId,
            clientNotes.Trim(),
            ct);
    }
}
