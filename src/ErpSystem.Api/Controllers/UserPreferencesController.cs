using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/user/preferences")]
    [Authorize]
    public class UserPreferencesController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserPreferencesController> _logger;

        public UserPreferencesController(
            ICurrentUserService currentUserService,
            ILogger<UserPreferencesController> logger)
        {
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get user's theme preferences
        /// </summary>
        [HttpGet("theme")]
        public async Task<ActionResult> GetThemePreferences()
        {
            try
            {
                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                _logger.LogInformation("Getting theme preferences for user {UserId}", userId.Value);

                // For now, return default theme preferences since we don't have user preferences storage yet
                var defaultPreferences = new
                {
                    themePreferences = new
                    {
                        theme = "system",
                        colorScheme = "blue",
                        fontSize = "md",
                        compactMode = false,
                        reducedMotion = false,
                        highContrast = false,
                        lastUpdated = DateTime.UtcNow.ToString("O")
                    }
                };

                return Ok(defaultPreferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting theme preferences");
                return StatusCode(500, new { message = "An error occurred while getting theme preferences" });
            }
        }

        /// <summary>
        /// Update user's theme preferences
        /// </summary>
        [HttpPut("theme")]
        public async Task<ActionResult> UpdateThemePreferences([FromBody] object themePreferencesRequest)
        {
            try
            {
                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                _logger.LogInformation("Updating theme preferences for user {UserId}", userId.Value);

                // For now, just return the same preferences with updated timestamp
                // In the future, this should save to database
                var updatedPreferences = new
                {
                    themePreferences = new
                    {
                        theme = "system",
                        colorScheme = "blue", 
                        fontSize = "md",
                        compactMode = false,
                        reducedMotion = false,
                        highContrast = false,
                        lastUpdated = DateTime.UtcNow.ToString("O")
                    }
                };

                return Ok(updatedPreferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating theme preferences");
                return StatusCode(500, new { message = "An error occurred while updating theme preferences" });
            }
        }

        /// <summary>
        /// Reset user's theme preferences to defaults
        /// </summary>
        [HttpPost("theme/reset")]
        public async Task<ActionResult> ResetThemePreferences()
        {
            try
            {
                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                _logger.LogInformation("Resetting theme preferences for user {UserId}", userId.Value);

                var defaultPreferences = new
                {
                    themePreferences = new
                    {
                        theme = "system",
                        colorScheme = "blue",
                        fontSize = "md", 
                        compactMode = false,
                        reducedMotion = false,
                        highContrast = false,
                        lastUpdated = DateTime.UtcNow.ToString("O")
                    }
                };

                return Ok(defaultPreferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting theme preferences");
                return StatusCode(500, new { message = "An error occurred while resetting theme preferences" });
            }
        }
    }
}