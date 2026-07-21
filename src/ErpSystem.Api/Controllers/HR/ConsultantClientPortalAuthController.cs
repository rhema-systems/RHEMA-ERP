using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/client-portal/auth")]
[AllowAnonymous]
[EnableRateLimiting("PublicPortalPolicy")]
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

    [HttpPost("complete-setup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteSetup(
        [FromBody] ConsultantClientPortalCompleteSetupDto dto,
        CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "X-Tenant-Id header is required." });

        try
        {
            await _authService.CompleteAccountSetupAsync(dto, tenantId, ct);
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
