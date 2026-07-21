using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Public (anonymous) authentication endpoints for the external candidate portal.
/// Rate-limited: the per-account lockout does not protect against password spraying across many
/// accounts, nor against mass registration or forgot-password mail-bombing.
/// </summary>
[ApiController]
[Route("api/portal/auth")]
[AllowAnonymous]
[EnableRateLimiting("AuthPolicy")]
public class CandidatePortalAuthController : ControllerBase
{
    private readonly ICandidatePortalAuthService _authService;

    public CandidatePortalAuthController(ICandidatePortalAuthService authService)
    {
        _authService = authService;
    }

    // ── Register ──────────────────────────────────────────────────────────────
    /// <summary>Creates a new candidate portal account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(CandidatePortalAuthResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] CandidatePortalRegisterDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        try
        {
            var result = await _authService.RegisterAsync(dto, tenantId, ct);
            return CreatedAtAction(nameof(Register), result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    /// <summary>Authenticates a candidate and returns a JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(CandidatePortalAuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] CandidatePortalLoginDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        try
        {
            var result = await _authService.LoginAsync(dto, tenantId, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // ── Verify Email ───────────────────────────────────────────────────────────
    /// <summary>Confirms the candidate's email address using the token sent by email.</summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] CandidatePortalVerifyEmailDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        try
        {
            await _authService.VerifyEmailAsync(dto.Token, tenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Forgot Password ────────────────────────────────────────────────────────
    /// <summary>
    /// Sends a password reset email. Always returns 204 to avoid email enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] CandidatePortalForgotPasswordDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        await _authService.RequestPasswordResetAsync(dto.Email, tenantId, ct);
        return NoContent();
    }

    // ── Reset Password ─────────────────────────────────────────────────────────
    /// <summary>Resets the password using the token from the reset email.</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] CandidatePortalResetPasswordDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        try
        {
            await _authService.ResetPasswordAsync(dto, tenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return Request.Headers.TryGetValue("X-Tenant-Id", out var v)
            && Guid.TryParse(v, out tenantId);
    }
}
