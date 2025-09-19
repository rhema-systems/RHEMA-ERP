using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DebugController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DebugController> _logger;

    public DebugController(ICurrentUserService currentUserService, ILogger<DebugController> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user context info for debugging
    /// </summary>
    [HttpGet("current-user")]
    public ActionResult<object> GetCurrentUserInfo()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var tenantId = _currentUserService.GetTenantId();
            var username = _currentUserService.GetUsername();
            var isAuthenticated = _currentUserService.IsAuthenticated();
            
            // Get all claims for debugging
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();

            var result = new
            {
                IsAuthenticated = isAuthenticated,
                UserId = userId,
                TenantId = tenantId,
                Username = username,
                Claims = claims
            };

            _logger.LogInformation("Debug user info: {@UserInfo}", result);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user info");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }
}