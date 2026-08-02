using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/client-portal/auth")]
[AllowAnonymous]
// AuthPolicy (10/min in production), not PublicPortalPolicy (60/min). The looser policy is a
// vacancy-browsing budget and was never an appropriate allowance for credential endpoints —
// the candidate portal's auth controller has always used AuthPolicy.
[EnableRateLimiting("AuthPolicy")]
public class ConsultantClientPortalAuthController : ControllerBase
{
    private readonly IConsultantClientPortalAuthService _authService;

    public ConsultantClientPortalAuthController(IConsultantClientPortalAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ConsultantClientPortalAuthResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] ConsultantClientPortalRegisterDto dto,
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(ConsultantClientPortalAuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] ConsultantClientPortalLoginDto dto,
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

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] ConsultantClientPortalVerifyEmailDto dto,
        CancellationToken ct)
    {
        // No X-Tenant-Id required — tenant resolved from the globally-unique token (emailed link).
        try
        {
            await _authService.VerifyEmailAsync(dto.Token, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Re-sends the account verification email. Always returns 204 to avoid email enumeration.
    /// </summary>
    /// <remarks>
    /// Registration issues no session token and login refuses unverified accounts, so without a
    /// resend path a lost verification email leaves the account permanently unusable. Cooled down
    /// per-address in the service as well as rate-limited here, because this endpoint sends mail
    /// to a third party on demand.
    /// </remarks>
    [HttpPost("resend-verification")]
    [EnableRateLimiting("SensitivePolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ConsultantClientPortalForgotPasswordDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        await _authService.ResendVerificationEmailAsync(dto.Email, tenantId, ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ConsultantClientPortalForgotPasswordDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        await _authService.RequestPasswordResetAsync(dto.Email, tenantId, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ConsultantClientPortalResetPasswordDto dto,
        CancellationToken ct)
    {
        // No X-Tenant-Id required — tenant resolved from the globally-unique reset token.
        try
        {
            await _authService.ResetPasswordAsync(dto, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("complete-setup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteSetup(
        [FromBody] ConsultantClientPortalCompleteSetupDto dto,
        CancellationToken ct)
    {
        // No X-Tenant-Id required — tenant resolved from the globally-unique setup token.
        try
        {
            await _authService.CompleteAccountSetupAsync(dto, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return Request.Headers.TryGetValue("X-Tenant-Id", out var v)
            && Guid.TryParse(v, out tenantId);
    }
}
