using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services;

public class JwtBlacklistService : IJwtBlacklistService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JwtBlacklistService> _logger;

    public JwtBlacklistService(
        IUnitOfWork unitOfWork,
        ILogger<JwtBlacklistService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task BlacklistTokenAsync(string jti, Guid userId, DateTime expiresAt, string reason = "")
    {
        try
        {
            if (string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("Attempted to blacklist token with empty JTI");
                return;
            }

            // Check if token is already blacklisted
            var existingBlacklistedToken = await _unitOfWork.Repository<BlacklistedToken>()
                .FirstOrDefaultAsync(bt => bt.Jti == jti && !bt.IsDeleted);

            if (existingBlacklistedToken != null)
            {
                _logger.LogInformation("Token with JTI {Jti} is already blacklisted", jti);
                return;
            }

            var blacklistedToken = new BlacklistedToken
            {
                Jti = jti,
                UserId = userId,
                ExpiresAt = expiresAt,
                BlacklistedAt = DateTime.UtcNow,
                Reason = string.IsNullOrEmpty(reason) ? "Token revoked" : reason
            };

            await _unitOfWork.Repository<BlacklistedToken>().AddAsync(blacklistedToken);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Blacklisted JWT token with JTI {Jti} for user {UserId} (reason: {Reason})", 
                jti, userId, blacklistedToken.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blacklisting JWT token with JTI {Jti}", jti);
            throw;
        }
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        try
        {
            if (string.IsNullOrEmpty(jti))
                return false;

            var blacklistedToken = await _unitOfWork.Repository<BlacklistedToken>()
                .FirstOrDefaultAsync(bt => bt.Jti == jti && !bt.IsDeleted);

            return blacklistedToken != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if JWT token with JTI {Jti} is blacklisted", jti);
            // Fail safe - assume not blacklisted on error
            return false;
        }
    }

    public async Task<int> BlacklistAllUserTokensAsync(Guid userId, string reason = "")
    {
        try
        {
            // This is a complex operation because we need to find all currently valid JWT tokens
            // for a user. Since we don't store JWTs in the database, we can't directly blacklist them.
            // However, we can revoke all refresh tokens and rely on JWT expiration.
            
            // For now, we'll log this operation but not implement it fully
            // A proper implementation would require either:
            // 1. Storing JWT IDs when tokens are issued
            // 2. Using a different approach like changing the user's security stamp
            
            _logger.LogInformation("BlacklistAllUserTokensAsync called for user {UserId} (reason: {Reason}). " +
                                 "This operation is not fully implemented - refresh tokens should be revoked instead.", 
                                 userId, reason);
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blacklisting all user tokens for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> CleanupExpiredBlacklistedTokensAsync()
    {
        try
        {
            var expiredTokens = await _unitOfWork.Repository<BlacklistedToken>()
                .FindAsync(bt => bt.ExpiresAt <= DateTime.UtcNow && !bt.IsDeleted);

            var tokens = expiredTokens.ToList();
            var cleanedCount = 0;

            foreach (var token in tokens)
            {
                // Soft delete expired blacklisted tokens
                token.IsDeleted = true;
                token.DeletedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<BlacklistedToken>().UpdateAsync(token);
                cleanedCount++;
            }

            if (cleanedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {CleanedCount} expired blacklisted JWT tokens", cleanedCount);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired blacklisted tokens");
            return 0;
        }
    }
}