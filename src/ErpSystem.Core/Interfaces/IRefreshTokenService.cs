using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces;

public interface IRefreshTokenService
{
    /// <summary>
    /// Creates and stores a new refresh token for the user
    /// </summary>
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, Guid? tenantId, string ipAddress = "", string userAgent = "", string deviceId = "");

    /// <summary>
    /// Validates and retrieves a refresh token by its value
    /// </summary>
    Task<RefreshToken?> GetValidRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Marks a refresh token as used and optionally updates usage count
    /// </summary>
    Task<RefreshToken> MarkRefreshTokenAsUsedAsync(RefreshToken token);

    /// <summary>
    /// Revokes a specific refresh token
    /// </summary>
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, Guid? revokedBy = null, string reason = "");

    /// <summary>
    /// Revokes all active refresh tokens for a user
    /// </summary>
    Task<int> RevokeAllUserRefreshTokensAsync(Guid userId, Guid? revokedBy = null, string reason = "");

    /// <summary>
    /// Revokes all active refresh tokens for a user except the current one
    /// </summary>
    Task<int> RevokeAllUserRefreshTokensExceptCurrentAsync(Guid userId, string currentRefreshToken, Guid? revokedBy = null, string reason = "");

    /// <summary>
    /// Cleans up expired refresh tokens
    /// </summary>
    Task<int> CleanupExpiredTokensAsync();

    /// <summary>
    /// Gets active session count for a user
    /// </summary>
    Task<int> GetActiveSessionCountAsync(Guid userId);
}
