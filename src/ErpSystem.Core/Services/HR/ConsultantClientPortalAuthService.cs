using ErpSystem.Core.DTOs.HR;
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

public sealed class ConsultantClientPortalAuthService : IConsultantClientPortalAuthService
{
    private readonly IGenericRepository<ConsultantClientPortalAccount> _accountRepo;
    private readonly IConsultantClientRepository _clientRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;
    private readonly IConsultantClientPortalJwtService _jwtService;
    private readonly IPasswordHasher<ConsultantClientPortalAccount> _hasher;
    private readonly ILogger<ConsultantClientPortalAuthService> _logger;
    private readonly string _portalUrl;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int VerificationTokenExpiryHours = 24;
    private const int ResetTokenExpiryHours = 1;
    private const int SetupTokenExpiryHours = 72;

    public ConsultantClientPortalAuthService(
        IGenericRepository<ConsultantClientPortalAccount> accountRepo,
        IConsultantClientRepository clientRepo,
        IUnitOfWork unitOfWork,
        IEmailService email,
        IConsultantClientPortalJwtService jwtService,
        IPasswordHasher<ConsultantClientPortalAccount> hasher,
        ILogger<ConsultantClientPortalAuthService> logger,
        IOptions<CandidatePortalOptions> portalOptions)
    {
        _accountRepo = accountRepo;
        _clientRepo = clientRepo;
        _unitOfWork = unitOfWork;
        _email = email;
        _jwtService = jwtService;
        _hasher = hasher;
        _logger = logger;
        _portalUrl = (portalOptions.Value.PortalUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(_portalUrl))
            _portalUrl = "http://localhost:5085";
    }

    public async Task<ConsultantClientPortalAuthResultDto> RegisterAsync(
        ConsultantClientPortalRegisterDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var clientCode = dto.ClientCode.Trim();

        var client = await _clientRepo.GetByClientCodeAsync(clientCode);
        if (client == null || client.TenantId != tenantId || !client.IsActive)
            throw new InvalidOperationException("Invalid client code. Please check the code provided by your HR contact.");

        var existing = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == email);
        if (existing != null)
            throw new InvalidOperationException(
                "An account already exists for this email address. Please sign in.");

        var account = new ConsultantClientPortalAccount
        {
            TenantId = tenantId,
            Email = email,
            ConsultantClientId = client.Id,
            ContactName = dto.ContactName?.Trim(),
            ContactRole = dto.ContactRole?.Trim(),
            IsActive = true,
        };
        account.PasswordHash = _hasher.HashPassword(account, dto.Password);
        account.EmailVerificationToken = GenerateSecureToken();
        account.EmailVerificationExpiry = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);
        account.ConsultantClient = client;

        await _accountRepo.AddAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "New consultant client portal account registered: {Email} for client {ClientCode}",
            email,
            clientCode);

        await SendVerificationEmailAsync(account, client, ct);

        return BuildAuthResult(account, client, tenantId);
    }

    public async Task<ConsultantClientPortalAuthResultDto> LoginAsync(
        ConsultantClientPortalLoginDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!account.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated. Please contact support.");

        if (account.LockedOutUntil.HasValue && account.LockedOutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((account.LockedOutUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException(
                $"Account is temporarily locked. Please try again in {remaining} minute(s).");
        }

        var result = _hasher.VerifyHashedPassword(account, account.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
                account.LockedOutUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            await _accountRepo.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = _hasher.HashPassword(account, dto.Password);

        account.FailedLoginAttempts = 0;
        account.LockedOutUntil = null;
        account.LastLoginAt = DateTime.UtcNow;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        var client = await _clientRepo.GetByIdAsync(account.ConsultantClientId)
            ?? throw new InvalidOperationException("Linked client organisation not found.");

        _logger.LogInformation("Consultant client portal login: {Email} (tenant {TenantId})", email, tenantId);
        return BuildAuthResult(account, client, tenantId);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        // Token-only lookup: the token is 48-byte crypto-random and globally unique, so tenant is
        // derived from the account — no X-Tenant-Id header needed for this emailed-link flow.
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.EmailVerificationToken == token);

        if (account == null)
            throw new InvalidOperationException("Verification link is invalid or has already been used.");

        if (account.EmailVerificationExpiry < DateTime.UtcNow)
            throw new InvalidOperationException(
                "Verification link has expired. Please request a new verification email.");

        account.IsEmailVerified = true;
        account.EmailVerificationToken = null;
        account.EmailVerificationExpiry = null;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Email verified for client portal account {AccountId}", account.Id);

        var client = await _clientRepo.GetByIdAsync(account.ConsultantClientId);
        if (client != null)
            await SendAccountActivatedEmailAsync(account, client, ct);
    }

    public async Task RequestPasswordResetAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == normalised);

        if (account == null || !account.IsActive)
            return;

        account.PasswordResetToken = GenerateSecureToken();
        account.PasswordResetExpiry = DateTime.UtcNow.AddHours(ResetTokenExpiryHours);
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        await SendPasswordResetEmailAsync(account, ct);
    }

    public async Task ResetPasswordAsync(
        ConsultantClientPortalResetPasswordDto dto,
        CancellationToken ct = default)
    {
        // Token-only lookup: reset token is globally unique; tenant derived from the account.
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.PasswordResetToken == dto.Token);

        if (account == null)
            throw new InvalidOperationException("Reset link is invalid or has already been used.");

        if (account.PasswordResetExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Reset link has expired. Please request a new one.");

        account.PasswordHash = _hasher.HashPassword(account, dto.NewPassword);
        account.PasswordResetToken = null;
        account.PasswordResetExpiry = null;
        account.FailedLoginAttempts = 0;
        account.LockedOutUntil = null;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset completed for client portal account {AccountId}", account.Id);
    }

    public async Task CompleteAccountSetupAsync(
        ConsultantClientPortalCompleteSetupDto dto,
        CancellationToken ct = default)
    {
        // Token-only lookup: setup token is globally unique; tenant derived from the account.
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.AccountSetupToken == dto.Token);

        if (account == null)
            throw new InvalidOperationException("Setup link is invalid or has already been used.");

        if (account.AccountSetupExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Setup link has expired. Please ask HR to resend the invite.");

        account.PasswordHash = _hasher.HashPassword(account, dto.NewPassword);
        account.AccountSetupToken = null;
        account.AccountSetupExpiry = null;
        account.FailedLoginAttempts = 0;
        account.LockedOutUntil = null;
        account.EmailVerificationToken = GenerateSecureToken();
        account.EmailVerificationExpiry = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);
        account.IsEmailVerified = false;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Account setup completed for client portal account {AccountId}", account.Id);

        var client = await _clientRepo.GetByIdAsync(account.ConsultantClientId)
            ?? throw new InvalidOperationException("Linked client organisation not found.");

        await SendVerificationEmailAsync(account, client, ct);
    }

    public async Task<ConsultantClientPortalAccountSummaryDto> InvitePortalAccountAsync(
        Guid consultantClientId,
        ConsultantClientPortalInviteDto dto,
        Guid tenantId,
        Guid invitedByEmployeeId,
        CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var client = await _clientRepo.GetByIdAsync(consultantClientId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        if (client.TenantId != tenantId || !client.IsActive)
            throw new InvalidOperationException("Client organisation is not available for portal invites.");

        var existing = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == email);

        if (existing != null)
        {
            if (existing.ConsultantClientId != consultantClientId)
                throw new InvalidOperationException("This email is already registered for another client organisation.");

            if (existing.IsEmailVerified)
                throw new InvalidOperationException("An active portal account already exists for this email.");

            if (existing.AccountSetupToken != null)
                throw new InvalidOperationException(
                    "A portal invite is already pending for this email. Use resend invite instead.");

            throw new InvalidOperationException(
                "An account exists for this email but email is not verified. Use resend invite to send a new verification email.");
        }

        var account = new ConsultantClientPortalAccount
        {
            TenantId = tenantId,
            Email = email,
            ConsultantClientId = client.Id,
            ContactName = dto.ContactName?.Trim(),
            ContactRole = dto.ContactRole?.Trim(),
            IsActive = true,
            PasswordHash = _hasher.HashPassword(
                new ConsultantClientPortalAccount(),
                GenerateSecureToken()),
            AccountSetupToken = GenerateSecureToken(),
            AccountSetupExpiry = DateTime.UtcNow.AddHours(SetupTokenExpiryHours),
        };
        account.ConsultantClient = client;

        await _accountRepo.AddAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "HR invited client portal account {Email} for client {ClientId} by employee {EmployeeId}",
            email,
            consultantClientId,
            invitedByEmployeeId);

        await SendPortalInviteEmailAsync(account, client, ct);

        return ToAccountSummary(account);
    }

    public async Task ResendPortalInviteAsync(
        Guid consultantClientId,
        string email,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var normalised = email.Trim().ToLowerInvariant();

        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId
                 && a.Email == normalised
                 && a.ConsultantClientId == consultantClientId)
            ?? throw new InvalidOperationException("No portal account found for this email and client.");

        if (!account.IsActive)
            throw new InvalidOperationException("This portal account has been deactivated.");

        var client = await _clientRepo.GetByIdAsync(consultantClientId)
            ?? throw new InvalidOperationException("Client organisation not found.");

        if (account.AccountSetupToken != null || !account.IsEmailVerified && account.LastLoginAt == null)
        {
            account.AccountSetupToken = GenerateSecureToken();
            account.AccountSetupExpiry = DateTime.UtcNow.AddHours(SetupTokenExpiryHours);
            await _accountRepo.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(ct);
            await SendPortalInviteEmailAsync(account, client, ct);
            return;
        }

        if (!account.IsEmailVerified)
        {
            account.EmailVerificationToken = GenerateSecureToken();
            account.EmailVerificationExpiry = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);
            await _accountRepo.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(ct);
            await SendVerificationEmailAsync(account, client, ct);
            return;
        }

        throw new InvalidOperationException("This portal account is already active.");
    }

    public async Task<IEnumerable<ConsultantClientPortalAccountSummaryDto>> GetPortalAccountsForClientAsync(
        Guid consultantClientId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var accounts = await _accountRepo.FindAsync(
            a => a.TenantId == tenantId && a.ConsultantClientId == consultantClientId);

        return accounts
            .OrderByDescending(a => a.CreatedAt)
            .Select(ToAccountSummary)
            .ToList();
    }

    public async Task ChangePasswordAsync(
        Guid accountId,
        ConsultantClientPortalChangePasswordDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var account = await _accountRepo.GetByIdAsync(accountId)
            ?? throw new InvalidOperationException("Account not found.");

        if (account.TenantId != tenantId)
            throw new UnauthorizedAccessException();

        var verify = _hasher.VerifyHashedPassword(account, account.PasswordHash, dto.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Current password is incorrect.");

        account.PasswordHash = _hasher.HashPassword(account, dto.NewPassword);
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private ConsultantClientPortalAuthResultDto BuildAuthResult(
        ConsultantClientPortalAccount account,
        ConsultantClient client,
        Guid tenantId)
    {
        account.ConsultantClient ??= client;
        var token = _jwtService.GenerateToken(account, tenantId);
        return new ConsultantClientPortalAuthResultDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            AccountId = account.Id,
            Email = account.Email,
            ContactName = account.ContactName,
            ConsultantClientId = client.Id,
            ClientName = client.ClientName,
            ClientCode = client.ClientCode,
            IsEmailVerified = account.IsEmailVerified,
        };
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private async Task SendVerificationEmailAsync(
        ConsultantClientPortalAccount account,
        ConsultantClient client,
        CancellationToken ct)
    {
        var verifyLink =
            $"{_portalUrl}/client-portal/verify-email?token={Uri.EscapeDataString(account.EmailVerificationToken!)}";
        var contact = WebUtility.HtmlEncode(account.ContactName ?? account.Email);
        var clientName = WebUtility.HtmlEncode(client.ClientName);
        var subject = "Verify your email — Client Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Welcome to the Client Portal</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{contact}</strong>,</p>
  <p>Please verify your email to activate your portal account for <strong>{clientName}</strong>.</p>
  <p style='margin-top:1.5rem'>
    <a href='{verifyLink}'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Verify Email Address
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>This link expires in {VerificationTokenExpiryHours} hours.</p>
  <p style='color:#6b7280;font-size:0.85rem'>If the button does not work, copy and paste this URL into your browser:<br/>
  <a href='{verifyLink}' style='color:#0f766e;word-break:break-all'>{verifyLink}</a></p>
</div>
</body></html>";

        // Best-effort: the account row is already committed by the time this runs, so a mail failure
        // must not fail the request and leave a client unable to verify or re-register.
        try
        {
            await _email.SendEmailAsync(new()
            {
                To = account.Email,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email to {Email}", account.Email);
        }
    }

    private async Task SendPasswordResetEmailAsync(ConsultantClientPortalAccount account, CancellationToken ct)
    {
        var resetLink =
            $"{_portalUrl}/client-portal/reset-password?token={Uri.EscapeDataString(account.PasswordResetToken!)}";
        var subject = "Reset your password — Client Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Password Reset Request</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>We received a request to reset the password for <strong>{WebUtility.HtmlEncode(account.Email)}</strong>.</p>
  <p style='margin-top:1.5rem'>
    <a href='{resetLink}'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Reset Password
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>This link expires in {ResetTokenExpiryHours} hour.</p>
</div>
</body></html>";

        // Best-effort: the reset token is already committed, so a mail failure must not surface as an
        // error that tells an anonymous caller whether the address exists.
        try
        {
            await _email.SendEmailAsync(new()
            {
                To = account.Email,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}", account.Email);
        }
    }

    private async Task SendAccountActivatedEmailAsync(
        ConsultantClientPortalAccount account,
        ConsultantClient client,
        CancellationToken ct)
    {
        var subject = "Your account is active — Client Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Your account is now active!</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{WebUtility.HtmlEncode(account.ContactName ?? account.Email)}</strong>,</p>
  <p>Your email has been verified. You can now review and confirm consultant timesheets for <strong>{WebUtility.HtmlEncode(client.ClientName)}</strong>.</p>
  <p style='margin-top:1.5rem'>
    <a href='{_portalUrl}/client-portal/login'
       style='background:#0f766e;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Sign in to your account
    </a>
  </p>
</div>
</body></html>";

        try
        {
            await _email.SendEmailAsync(new()
            {
                To = account.Email,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send account activated email to {Email}", account.Email);
        }
    }

    private async Task SendPortalInviteEmailAsync(
        ConsultantClientPortalAccount account,
        ConsultantClient client,
        CancellationToken ct)
    {
        var setupLink =
            $"{_portalUrl}/client-portal/complete-setup?token={Uri.EscapeDataString(account.AccountSetupToken!)}";
        var contact = WebUtility.HtmlEncode(account.ContactName ?? account.Email);
        var clientName = WebUtility.HtmlEncode(client.ClientName);
        var subject = "You're invited to the Client Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#0f766e,#14b8a6);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Client Portal Invitation</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{contact}</strong>,</p>
  <p>Your consultant firm has invited you to access the client portal for <strong>{clientName}</strong>.</p>
  <p>Click below to set your password and complete account setup. You will then receive a separate email to verify your address.</p>
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

        // Best-effort: the invite/setup token is already committed. A mail failure must not fail the
        // request — the invite can be resent, but the caller should not see a 500 for a saved account.
        try
        {
            await _email.SendEmailAsync(new()
            {
                To = account.Email,
                Subject = subject,
                Body = body,
                IsHtml = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send portal invite email to {Email}", account.Email);
        }
    }

    private static ConsultantClientPortalAccountSummaryDto ToAccountSummary(ConsultantClientPortalAccount account)
        => new()
        {
            Id = account.Id,
            Email = account.Email,
            ContactName = account.ContactName,
            ContactRole = account.ContactRole,
            IsEmailVerified = account.IsEmailVerified,
            IsActive = account.IsActive,
            IsSetupPending = account.AccountSetupToken != null,
            LastLoginAt = account.LastLoginAt,
            CreatedAt = account.CreatedAt,
        };
}
