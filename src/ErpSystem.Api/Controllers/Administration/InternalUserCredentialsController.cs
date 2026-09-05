using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Administration;

[ApiController]
[Route("api/user")]
[Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
public sealed class InternalUserCredentialsController : ControllerBase
{
    private readonly IInternalUserTemporaryPasswordResetService _passwordReset;
    private readonly ICurrentUserService _currentUser;

    public InternalUserCredentialsController(
        IInternalUserTemporaryPasswordResetService passwordReset,
        ICurrentUserService currentUser)
    {
        _passwordReset = passwordReset;
        _currentUser = currentUser;
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid userId,
        [FromBody] ResetInternalUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated ||
            !_currentUser.TenantId.HasValue ||
            !Guid.TryParse(_currentUser.UserId, out var actorUserId) ||
            string.IsNullOrWhiteSpace(_currentUser.UserName))
        {
            return Forbid();
        }

        var result = await _passwordReset.ResetAsync(
            new InternalUserTemporaryPasswordResetCommand(
                userId,
                _currentUser.TenantId.Value,
                actorUserId,
                _currentUser.UserName,
                request.NewPassword ?? string.Empty,
                request.Reason ?? string.Empty,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                HttpContext.Request.Headers["User-Agent"].ToString()),
            cancellationToken);

        if (!result.Succeeded)
            return Failure(result);

        return Ok(new
        {
            userId,
            mustChangePassword = true,
            temporaryPasswordExpiresAtUtc = result.TemporaryPasswordExpiresAtUtc,
            sessionsInvalidated = true
        });
    }

    private ObjectResult Failure(InternalUserTemporaryPasswordResetResult result)
    {
        var status = result.Code switch
        {
            "INTERNAL_USER_NOT_FOUND" => StatusCodes.Status404NotFound,
            "INTERNAL_USER_INACTIVE" => StatusCodes.Status409Conflict,
            "INTERNAL_USER_LOCAL_CREDENTIAL_REQUIRED" => StatusCodes.Status409Conflict,
            "INTERNAL_USER_EXTERNAL_RESET_PROHIBITED" => StatusCodes.Status409Conflict,
            "INTERNAL_USER_SELF_RESET_PROHIBITED" => StatusCodes.Status409Conflict,
            "TEMPORARY_PASSWORD_RESET_FAILED" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = "Internal user temporary-password reset failed",
            Detail = result.Detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = result.Code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        if (result.Errors.Count > 0)
            problem.Extensions["errors"] = result.Errors;
        return StatusCode(status, problem);
    }
}

public sealed class ResetInternalUserPasswordRequest
{
    public string? NewPassword { get; init; }
    public string? Reason { get; init; }
}
