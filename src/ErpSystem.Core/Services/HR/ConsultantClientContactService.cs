using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// HR-side administration of consultant-client contacts on the main JWT scheme. Replaced
/// <c>ConsultantClientPortalAuthService</c> 2026-08-31 when the PortalBearer portal was
/// retired: Identity owns credentials, lockout and confirmation; the setup token is an
/// Identity password-reset token consumed by <c>api/auth/complete-client-setup</c> (which is
/// also the mailbox proof — the token only ever travels by email); and the
/// <c>ConsultantClientContact</c> row is the authorisation anchor the portal reads from.
/// </summary>
public sealed class ConsultantClientContactService : IConsultantClientContactService
{
    private readonly IGenericRepository<ConsultantClientContact> _contactRepo;
    private readonly IConsultantClientRepository _clientRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantService _userTenants;
    private readonly IPasswordResetService _passwordReset;
    private readonly IEmailService _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ConsultantClientContactService> _logger;
    private readonly string _portalUrl;

    private const int SetupTokenExpiryHours = 72;

    /// <summary>
    /// Minimum gap between invite emails to one contact. Without it, a resend endpoint is a
    /// mailbox-bombing tool aimed at a third party.
    /// </summary>
    private static readonly TimeSpan InviteResendCooldown = TimeSpan.FromMinutes(2);

    public ConsultantClientContactService(
        IGenericRepository<ConsultantClientContact> contactRepo,
        IConsultantClientRepository clientRepo,
        UserManager<ApplicationUser> userManager,
        IUserTenantService userTenants,
        IPasswordResetService passwordReset,
        IEmailService email,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ConsultantClientContactService> logger,
        IOptions<CandidatePortalOptions> portalOptions)
    {
        _contactRepo = contactRepo;
        _clientRepo = clientRepo;
        _userManager = userManager;
        _userTenants = userTenants;
        _passwordReset = passwordReset;
        _email = email;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _portalUrl = (portalOptions.Value.PortalUrl ?? string.Empty).TrimEnd('/');
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

    public async Task<ConsultantClientContactSummaryDto> InviteContactAsync(
        Guid consultantClientId,
        ConsultantClientPortalInviteDto dto,
        Guid tenantId,
        Guid invitedByEmployeeId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var email = dto.Email.Trim().ToLowerInvariant();

        var client = await _clientRepo.FirstOrDefaultAsync(
            c => c.Id == consultantClientId && c.TenantId == tenantId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        if (!client.IsActive)
            throw new InvalidOperationException("Client organisation is not available for portal invites.");

        var user = await _userManager.FindByEmailAsync(email);
        var isNewUser = user == null;

        if (user != null)
        {
            // Adopt ONLY a pure consultant-client account (the same contact serving another
            // client). Any other existing account — staff, business partner, candidate — must
            // never be quietly widened onto a client's timesheets by an HR invite.
            await EnsureAdoptableAsync(user, tenantId);
        }
        else
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = dto.ContactName?.Trim() ?? email,
                LastName = string.Empty,
                TenantId = tenantId,
                // Setup completion (api/auth/complete-client-setup) activates the account and
                // confirms the email in one step — the setup token only ever travels by email,
                // so consuming it IS the mailbox proof.
                IsActive = false,
                EmailConfirmed = false,
                AuthenticationProvider = ErpSystem.Shared.AuthenticationProvider.Local,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "ClientPortal-Invite",
            };

            // The contact sets their own password via the setup link; this throwaway exists only
            // because Identity requires one at creation and is never disclosed to anyone.
            var created = await _userManager.CreateAsync(user, GenerateThrowawayPassword());
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    "Could not create the portal account: " +
                    string.Join(" ", created.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, ErpSystem.Shared.Constants.Roles.ConsultantClient);
            await _userTenants.GrantUserAccessToTenantAsync(
                user.Id, tenantId, UserTenantAccessLevel.Standard, "ClientPortal-Invite");
        }

        var existing = await _contactRepo.FirstOrDefaultAsync(
            c => c.TenantId == tenantId
                 && c.ConsultantClientId == consultantClientId
                 && c.UserId == user!.Id);

        if (existing != null && existing.IsActive)
            throw new InvalidOperationException(
                "This email is already a contact for this client. Use resend invite instead.");

        var now = DateTime.UtcNow;
        ConsultantClientContact contact;
        if (existing != null)
        {
            // A deactivated contact being re-invited: reactivate the row rather than minting a
            // twin (the unique index would refuse the twin anyway).
            existing.IsActive = true;
            existing.ContactName = dto.ContactName?.Trim() ?? existing.ContactName;
            existing.ContactRole = dto.ContactRole?.Trim() ?? existing.ContactRole;
            existing.InvitedById = invitedByEmployeeId;
            existing.InvitedAtUtc = now;
            existing.UpdatedAt = now;
            existing.UpdatedBy = invitedByEmployeeId.ToString();
            await _contactRepo.UpdateAsync(existing);
            contact = existing;
        }
        else
        {
            contact = new ConsultantClientContact
            {
                TenantId = tenantId,
                ConsultantClientId = client.Id,
                UserId = user!.Id,
                ContactName = dto.ContactName?.Trim(),
                ContactRole = dto.ContactRole?.Trim(),
                IsActive = true,
                InvitedById = invitedByEmployeeId,
                InvitedAtUtc = now,
                CreatedBy = invitedByEmployeeId.ToString(),
            };
            await _contactRepo.AddAsync(contact);
        }

        var needsSetup = !user!.EmailConfirmed || !user.IsActive;
        contact.LastInviteSentAtUtc = now;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "HR invited client contact {Email} for client {ClientId} by employee {EmployeeId} (new user: {IsNew})",
            email, consultantClientId, invitedByEmployeeId, isNewUser);

        if (needsSetup)
        {
            var setupToken = await _passwordReset.GeneratePasswordResetTokenAsync(
                user.Id, "hr-invite", "hr-invite", SetupTokenExpiryHours * 60);
            await SendInviteEmailAsync(user, contact, client, setupToken, ct);
        }
        else
        {
            // An already-active contact gaining a second client needs no setup — just the news.
            await SendClientAddedEmailAsync(user, contact, client, ct);
        }

        return ToSummary(contact, user);
    }

    public async Task ResendInviteAsync(
        Guid consultantClientId,
        string email,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var normalised = email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(normalised)
            ?? throw new InvalidOperationException("No portal contact found for this email and client.");

        var contact = await _contactRepo.FirstOrDefaultAsync(
            c => c.TenantId == tenantId
                 && c.ConsultantClientId == consultantClientId
                 && c.UserId == user.Id)
            ?? throw new InvalidOperationException("No portal contact found for this email and client.");

        if (!contact.IsActive)
            throw new InvalidOperationException("This contact has been deactivated.");

        if (user.EmailConfirmed && user.IsActive)
            throw new InvalidOperationException("This portal account is already active.");

        if (contact.LastInviteSentAtUtc is { } last && DateTime.UtcNow - last < InviteResendCooldown)
            throw new InvalidOperationException(
                "An invite email was just sent. Please wait a couple of minutes before resending.");

        var client = await _clientRepo.FirstOrDefaultAsync(
            c => c.Id == consultantClientId && c.TenantId == tenantId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        contact.LastInviteSentAtUtc = DateTime.UtcNow;
        contact.UpdatedAt = DateTime.UtcNow;
        await _contactRepo.UpdateAsync(contact);
        await _unitOfWork.SaveChangesAsync(ct);

        var setupToken = await _passwordReset.GeneratePasswordResetTokenAsync(
            user.Id, "hr-invite-resend", "hr-invite-resend", SetupTokenExpiryHours * 60);
        await SendInviteEmailAsync(user, contact, client, setupToken, ct);
    }

    public async Task<IEnumerable<ConsultantClientContactSummaryDto>> GetContactsForClientAsync(
        Guid consultantClientId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        _ = await _clientRepo.FirstOrDefaultAsync(
            c => c.Id == consultantClientId && c.TenantId == tenantId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        var contacts = await _contactRepo.FindAsync(
            c => c.TenantId == tenantId && c.ConsultantClientId == consultantClientId,
            c => c.User);

        return contacts
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToSummary(c, c.User))
            .ToList();
    }

    private async Task EnsureAdoptableAsync(ApplicationUser user, Guid tenantId)
    {
        if (user.TenantId != tenantId)
            throw new InvalidOperationException(
                "This email belongs to an account registered under another organisation.");

        if (user.EmployeeId != null)
            throw new InvalidOperationException(
                "This email belongs to a staff account and cannot be invited as a client contact.");

        var roles = await _userManager.GetRolesAsync(user);
        var isPureClientContact = roles.Count == 1
            && string.Equals(roles[0], ErpSystem.Shared.Constants.Roles.ConsultantClient, StringComparison.OrdinalIgnoreCase);
        if (!isPureClientContact)
            throw new InvalidOperationException(
                "This email belongs to an existing system account and cannot be invited as a client contact.");
    }

    private static string GenerateThrowawayPassword()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1!";

    private static ConsultantClientContactSummaryDto ToSummary(
        ConsultantClientContact contact,
        ApplicationUser user)
        => new()
        {
            Id = contact.Id,
            Email = user.Email ?? string.Empty,
            ContactName = contact.ContactName,
            ContactRole = contact.ContactRole,
            IsEmailVerified = user.EmailConfirmed,
            IsActive = contact.IsActive && user.IsActive,
            IsSetupPending = !user.EmailConfirmed || !user.IsActive,
            LastLoginAt = user.LastLoginDate,
            CreatedAt = contact.CreatedAt,
        };

    private async Task SendInviteEmailAsync(
        ApplicationUser user,
        ConsultantClientContact contact,
        ConsultantClient client,
        string setupToken,
        CancellationToken ct)
    {
        var setupLink =
            $"{_portalUrl}/client-portal/setup?token={Uri.EscapeDataString(setupToken)}&email={Uri.EscapeDataString(user.Email ?? string.Empty)}";
        var contactName = WebUtility.HtmlEncode(contact.ContactName ?? user.Email ?? "there");
        var clientName = WebUtility.HtmlEncode(client.ClientName);
        var subject = "You're invited to the Client Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Client Portal Invitation</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{contactName}</strong>,</p>
  <p>Your consultant firm has invited you to access the client portal for <strong>{clientName}</strong>.</p>
  <p>Click below to set your password and activate your account. You can then sign in and review the timesheets awaiting your confirmation.</p>
  <p style='margin-top:1.5rem'>
    <a href='{setupLink}'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Complete Account Setup
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>This link expires in {SetupTokenExpiryHours} hours.</p>
  <p style='color:#6b7280;font-size:0.85rem'>If the button does not work, copy and paste this URL into your browser:<br/>
  <a href='{setupLink}' style='color:#0f766e;word-break:break-all'>{setupLink}</a></p>
</div>
</body></html>";

        // Best-effort: the invite is already committed. A mail failure must not fail the request —
        // the invite can be resent, but the caller should not see a 500 for a saved contact.
        try
        {
            await _email.SendEmailAsync(new EmailDto
            {
                To = user.Email ?? string.Empty,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send portal invite email to {Email}", user.Email);
        }
    }

    private async Task SendClientAddedEmailAsync(
        ApplicationUser user,
        ConsultantClientContact contact,
        ConsultantClient client,
        CancellationToken ct)
    {
        var portalLink = $"{_portalUrl}/external-portal";
        var contactName = WebUtility.HtmlEncode(contact.ContactName ?? user.Email ?? "there");
        var clientName = WebUtility.HtmlEncode(client.ClientName);
        var subject = $"You now have client portal access for {client.ClientName}";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Client Portal Access</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{contactName}</strong>,</p>
  <p>Your existing client portal account now also covers <strong>{clientName}</strong>. Sign in as usual to review that client's timesheets alongside the rest.</p>
  <p style='margin-top:1.5rem'>
    <a href='{portalLink}'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Sign in to the portal
    </a>
  </p>
</div>
</body></html>";

        try
        {
            await _email.SendEmailAsync(new EmailDto
            {
                To = user.Email ?? string.Empty,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send client-added email to {Email}", user.Email);
        }
    }
}
