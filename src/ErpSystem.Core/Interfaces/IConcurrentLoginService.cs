using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service for managing concurrent login prevention
/// </summary>
public interface IConcurrentLoginService
{
    /// <summary>
    /// Checks if a user login should be allowed based on concurrent login settings
    /// </summary>
    /// <param name="userId">The user attempting to login</param>
    /// <param name="preventConcurrentLogin">The concurrent login prevention setting</param>
    /// <returns>True if login should be allowed, false if it should be blocked</returns>
    Task<bool> ShouldAllowLoginAsync(Guid userId, PreventConcurrentLogin preventConcurrentLogin);
    
    /// <summary>
    /// Handles concurrent login enforcement by invalidating existing sessions if needed
    /// </summary>
    /// <param name="userId">The user logging in</param>
    /// <param name="preventConcurrentLogin">The concurrent login prevention setting</param>
    /// <param name="currentRefreshToken">The new refresh token being created (to exclude from invalidation)</param>
    /// <returns>Number of sessions invalidated</returns>
    Task<int> EnforceConcurrentLoginPolicyAsync(Guid userId, PreventConcurrentLogin preventConcurrentLogin, string? currentRefreshToken = null);
    
    /// <summary>
    /// Gets the count of active sessions for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Number of active refresh tokens/sessions</returns>
    Task<int> GetActiveSessionCountAsync(Guid userId);
    
    /// <summary>
    /// Gets the current security settings to determine concurrent login policy
    /// </summary>
    /// <returns>Current security settings or null if none exist</returns>
    Task<PreventConcurrentLogin> GetConcurrentLoginPolicyAsync();
}