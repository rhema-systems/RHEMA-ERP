namespace ErpSystem.Core.Interfaces;

public interface IJwtBlacklistService
{
    /// <summary>
    /// Adds a JWT token to the blacklist
    /// </summary>
    /// <param name="jti">JWT ID (jti claim)</param>
    /// <param name="userId">User ID from token</param>
    /// <param name="expiresAt">Token expiration date</param>
    /// <param name="reason">Reason for blacklisting</param>
    Task BlacklistTokenAsync(string jti, Guid userId, DateTime expiresAt, string reason = "");

    /// <summary>
    /// Checks if a JWT token is blacklisted
    /// </summary>
    /// <param name="jti">JWT ID (jti claim)</param>
    Task<bool> IsTokenBlacklistedAsync(string jti);

    /// <summary>
    /// Blacklists all active JWT tokens for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="reason">Reason for blacklisting</param>
    Task<int> BlacklistAllUserTokensAsync(Guid userId, string reason = "");

    /// <summary>
    /// Cleans up expired blacklisted tokens
    /// </summary>
    Task<int> CleanupExpiredBlacklistedTokensAsync();
}