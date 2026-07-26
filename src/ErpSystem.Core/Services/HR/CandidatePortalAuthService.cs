using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Authentication service for the external candidate portal.
/// Manages <see cref="CandidatePortalAccount"/> records — register, login,
/// email verification, and password reset/change.
/// </summary>
public sealed class CandidatePortalAuthService : ICandidatePortalAuthService
{
    private readonly IGenericRepository<CandidatePortalAccount> _accountRepo;
    private readonly IJobCandidateRepository _candidateRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;
    private readonly ICandidateJwtService _jwtService;
    private readonly IPasswordHasher<CandidatePortalAccount> _hasher;
    private readonly ILogger<CandidatePortalAuthService> _logger;
    private readonly string _portalUrl;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int VerificationTokenExpiryHours = 24;
    private const int ResetTokenExpiryHours = 1;

    public CandidatePortalAuthService(
        IGenericRepository<CandidatePortalAccount> accountRepo,
        IJobCandidateRepository candidateRepo,
        IUnitOfWork unitOfWork,
        IEmailService email,
        ICandidateJwtService jwtService,
        IPasswordHasher<CandidatePortalAccount> hasher,
        ILogger<CandidatePortalAuthService> logger,
        IOptions<CandidatePortalOptions> portalOptions)
    {
        _accountRepo   = accountRepo;
        _candidateRepo = candidateRepo;
        _unitOfWork    = unitOfWork;
        _email         = email;
        _jwtService    = jwtService;
        _hasher        = hasher;
        _logger        = logger;
        _portalUrl     = portalOptions.Value.PortalUrl.TrimEnd('/');
    }

    // ── Register ──────────────────────────────────────────────────────────────
    public async Task<CandidatePortalAuthResultDto> RegisterAsync(
        CandidatePortalRegisterDto dto,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var existing = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == email);
        if (existing != null)
            throw new InvalidOperationException(
                "An account already exists for this email address. Please sign in.");

        var account = new CandidatePortalAccount
        {
            TenantId  = tenantId,
            Email     = email,
            IsActive  = true,
        };
        account.PasswordHash           = _hasher.HashPassword(account, dto.Password);
        account.EmailVerificationToken = GenerateSecureToken();
        account.EmailVerificationExpiry = DateTime.UtcNow.AddHours(VerificationTokenExpiryHours);

        // NOTE: we deliberately do NOT link an existing JobCandidate here, and do NOT issue a session
        // token. Registration only proves someone typed an email address — not that they own it.
        // Linking (or authenticating) at this point would let anyone register with a victim's email and
        // immediately read that candidate's CV, date of birth, phone, work history, referees and
        // applications. Both happen in VerifyEmailAsync, once ownership of the mailbox is proven.

        await _accountRepo.AddAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("New candidate portal account registered: {Email} (tenant {TenantId})", email, tenantId);

        await SendVerificationEmailAsync(account, ct);

        return BuildUnverifiedResult(account);
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    public async Task<CandidatePortalAuthResultDto> LoginAsync(
        CandidatePortalLoginDto dto,
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

        // Rehash if needed (e.g. password hasher version upgrade)
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = _hasher.HashPassword(account, dto.Password);
        }

        // Password is correct — but an unverified account must not receive a session token, otherwise
        // registering with someone else's email address is enough to reach their candidate profile.
        if (!account.IsEmailVerified)
        {
            account.FailedLoginAttempts = 0;
            account.LockedOutUntil      = null;
            await _accountRepo.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(ct);

            throw new UnauthorizedAccessException(
                "Please verify your email address before signing in. Check your inbox for the verification link.");
        }

        account.FailedLoginAttempts = 0;
        account.LockedOutUntil      = null;
        account.LastLoginAt         = DateTime.UtcNow;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Candidate portal login: {Email} (tenant {TenantId})", email, tenantId);
        return BuildAuthResult(account, tenantId);
    }

    // ── Email verification ─────────────────────────────────────────────────────
    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        // Token-only lookup: the verification token is 48-byte crypto-random and globally unique, so
        // the tenant is derived from the account row — no X-Tenant-Id header is needed for this
        // emailed-link flow (a recipient's clean browser has no tenant context).
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.EmailVerificationToken == token);

        if (account == null)
            throw new InvalidOperationException("Verification link is invalid or has already been used.");

        if (account.EmailVerificationExpiry < DateTime.UtcNow)
            throw new InvalidOperationException(
                "Verification link has expired. Please request a new verification email.");

        account.IsEmailVerified         = true;
        account.EmailVerificationToken  = null;
        account.EmailVerificationExpiry = null;

        // Ownership of the mailbox is now proven, so it is safe to adopt any JobCandidate profile that
        // already exists for this address (e.g. from a prior anonymous application). Deliberately done
        // here rather than at registration — see the note in RegisterAsync.
        if (account.JobCandidateId == null)
        {
            var candidate = await _candidateRepo.GetByEmailAsync(account.Email, account.TenantId);
            if (candidate != null)
                account.JobCandidateId = candidate.Id;
        }

        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Email verified for candidate account {AccountId}", account.Id);

        await SendAccountActivatedEmailAsync(account, ct);
    }

    // ── Forgot password ────────────────────────────────────────────────────────
    public async Task RequestPasswordResetAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Email == normalised);

        // Always return success to avoid email enumeration
        if (account == null || !account.IsActive)
            return;

        account.PasswordResetToken  = GenerateSecureToken();
        account.PasswordResetExpiry = DateTime.UtcNow.AddHours(ResetTokenExpiryHours);
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        await SendPasswordResetEmailAsync(account, ct);
    }

    // ── Reset password ────────────────────────────────────────────────────────
    public async Task ResetPasswordAsync(
        CandidatePortalResetPasswordDto dto,
        CancellationToken ct = default)
    {
        // Token-only lookup: reset token is 48-byte crypto-random and globally unique; tenant is
        // derived from the account, so no X-Tenant-Id header is required for this emailed-link flow.
        var account = await _accountRepo.FirstOrDefaultAsync(
            a => a.PasswordResetToken == dto.Token);

        if (account == null)
            throw new InvalidOperationException("Reset link is invalid or has already been used.");

        if (account.PasswordResetExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Reset link has expired. Please request a new one.");

        account.PasswordHash        = _hasher.HashPassword(account, dto.NewPassword);
        account.PasswordResetToken  = null;
        account.PasswordResetExpiry = null;
        account.FailedLoginAttempts = 0;
        account.LockedOutUntil      = null;
        await _accountRepo.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset completed for candidate account {AccountId}", account.Id);
    }

    // ── Change password ────────────────────────────────────────────────────────
    public async Task ChangePasswordAsync(
        Guid accountId,
        CandidatePortalChangePasswordDto dto,
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

        await SendPasswordChangedEmailAsync(account, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Result for an account that exists but has not proven ownership of its email address.
    /// Carries NO session token — the caller must verify their email, then sign in.
    /// </summary>
    private static CandidatePortalAuthResultDto BuildUnverifiedResult(CandidatePortalAccount account) => new()
    {
        Token           = string.Empty,
        AccountId       = account.Id,
        Email           = account.Email,
        IsEmailVerified = false,
        HasProfile      = false,
    };

    private CandidatePortalAuthResultDto BuildAuthResult(CandidatePortalAccount account, Guid tenantId)
    {
        var token = _jwtService.GenerateToken(account, tenantId);
        return new CandidatePortalAuthResultDto
        {
            Token           = token,
            ExpiresAt       = DateTime.UtcNow.AddDays(7),
            AccountId       = account.Id,
            Email           = account.Email,
            FirstName       = account.JobCandidate?.FirstName ?? string.Empty,
            LastName        = account.JobCandidate?.LastName,
            IsEmailVerified = account.IsEmailVerified,
            HasProfile      = account.JobCandidateId.HasValue,
            CandidateId     = account.JobCandidateId,
        };
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private async Task SendVerificationEmailAsync(CandidatePortalAccount account, CancellationToken ct)
    {
        var verifyLink = $"{_portalUrl}/careers/portal/verify-email?token={Uri.EscapeDataString(account.EmailVerificationToken!)}";
        var subject = "Verify your email — Career Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#1e3a8a,#1a56db);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Welcome to our Career Portal</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
<p>Please verify your email address to complete your registration by clicking the button below.</p>
<p style='margin-top:1.5rem'>
  <a href='{verifyLink}'
     style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
    Verify Email Address
  </a>
</p>
<p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>This link expires in {VerificationTokenExpiryHours} hours.</p>
<p style='color:#6b7280;font-size:0.85rem'>If the button does not work, copy and paste this URL into your browser:<br/>
<a href='{verifyLink}' style='color:#1a56db;word-break:break-all'>{verifyLink}</a></p>
</div>
</body></html>";

        await _email.SendEmailAsync(new()
        {
            To      = account.Email,
            Subject = subject,
            Body    = body,
            IsHtml  = true,
        });
    }

    private async Task SendPasswordResetEmailAsync(CandidatePortalAccount account, CancellationToken ct)
    {
        var resetLink = $"{_portalUrl}/careers/portal/reset-password?token={Uri.EscapeDataString(account.PasswordResetToken!)}";
        var subject = "Reset your password — Career Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#1e3a8a,#1a56db);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Password Reset Request</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
<p>We received a request to reset the password for <strong>{account.Email}</strong>.</p>
<p style='margin-top:1.5rem'>
  <a href='{resetLink}'
     style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
    Reset Password
  </a>
</p>
<p style='color:#6b7280;font-size:0.85rem;margin-top:1.5rem'>This link expires in {ResetTokenExpiryHours} hour. If you did not request this, you can safely ignore this email.</p>
<p style='color:#6b7280;font-size:0.85rem'>If the button does not work, copy and paste this URL into your browser:<br/>
<a href='{resetLink}' style='color:#1a56db;word-break:break-all'>{resetLink}</a></p>
</div>
</body></html>";

        await _email.SendEmailAsync(new()
        {
            To      = account.Email,
            Subject = subject,
            Body    = body,
            IsHtml  = true,
        });
    }

    private async Task SendAccountActivatedEmailAsync(CandidatePortalAccount account, CancellationToken ct)
    {
        var subject = "Your account is active — Career Portal";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#1e3a8a,#1a56db);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Your account is now active!</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{account.Email}</strong>,</p>
  <p>Your email has been verified and your Career Portal account is ready to use.</p>
  <p>You can now:</p>
  <ul style='color:#374151'>
    <li>Browse and apply for open positions</li>
    <li>Track the status of your applications</li>
    <li>Manage your candidate profile</li>
  </ul>
  <p style='margin-top:1.5rem'>
    <a href='{_portalUrl}/careers/portal/login'
       style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Sign in to your account
    </a>
  </p>
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>If you did not create this account, please ignore this email.</p>
</div>
</body></html>";

        try
        {
            await _email.SendEmailAsync(new()
            {
                To      = account.Email,
                Subject = subject,
                Body    = body,
                IsHtml  = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send account activated email to {Email}", account.Email);
        }
    }

    private async Task SendPasswordChangedEmailAsync(CandidatePortalAccount account, CancellationToken ct)
    {
        var subject = "Your password has been changed — Career Portal";
        var changedAt = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + " UTC";
        var body = $@"
<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>
<div style='background:linear-gradient(135deg,#1e3a8a,#1a56db);padding:2rem;border-radius:8px 8px 0 0'>
  <h1 style='color:#fff;margin:0;font-size:1.5rem'>Password changed</h1>
</div>
<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>
  <p>Hi <strong>{account.Email}</strong>,</p>
  <p>Your Career Portal password was successfully changed on <strong>{changedAt}</strong>.</p>
  <p>If you made this change, no further action is needed.</p>
  <p style='background:#fef2f2;border:1px solid #fecaca;border-radius:6px;padding:0.75rem;color:#b91c1c'>
    <strong>Did not make this change?</strong> Please reset your password immediately and contact support.
  </p>
  <p style='margin-top:1.5rem'>
    <a href='{_portalUrl}/careers/portal/forgot-password'
       style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Reset my password
    </a>
  </p>
</div>
</body></html>";

        try
        {
            await _email.SendEmailAsync(new()
            {
                To      = account.Email,
                Subject = subject,
                Body    = body,
                IsHtml  = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password changed email to {Email}", account.Email);
        }
    }
}
