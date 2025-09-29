using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, Guid? tenantId, string ipAddress = "", string userAgent = "", string deviceId = "")
    {
        try
        {
            var tokenValue = GenerateRefreshTokenValue();
            var tokenHash = HashToken(tokenValue);

            var refreshToken = new RefreshToken
            {
                TokenHash = tokenHash,
                UserId = userId,
                TenantId = tenantId,
                ExpiresAt = DateTime.UtcNow.AddDays(30), // 30 days default
                IsRevoked = false,
                UsageCount = 0,
                MaxUsageCount = 0, // Unlimited
                IpAddress = ipAddress.Length > 45 ? ipAddress[..45] : ipAddress,
                UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
                DeviceId = deviceId.Length > 100 ? deviceId[..100] : deviceId
            };

            await _unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created new refresh token for user {UserId}, expires at {ExpiresAt}", 
                userId, refreshToken.ExpiresAt);

            // Return a copy with the original token value for the client
            // We don't store the actual token value, only the hash
            refreshToken.TokenHash = tokenValue; // Temporarily set for return
            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<RefreshToken?> GetValidRefreshTokenAsync(string refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var tokenHash = HashToken(refreshToken);
            var token = await _unitOfWork.Repository<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && !rt.IsDeleted);

            if (token == null)
            {
                _logger.LogWarning("Refresh token not found with hash: {HashPrefix}...", 
                    tokenHash.Length > 10 ? tokenHash[..10] : tokenHash);
                return null;
            }

            // Check if token is valid
            if (token.IsRevoked)
            {
                _logger.LogWarning("Refresh token {TokenId} is revoked (reason: {Reason})", 
                    token.Id, token.RevocationReason);
                return null;
            }

            if (token.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token {TokenId} has expired at {ExpiresAt}", 
                    token.Id, token.ExpiresAt);
                return null;
            }

            if (token.MaxUsageCount > 0 && token.UsageCount >= token.MaxUsageCount)
            {
                _logger.LogWarning("Refresh token {TokenId} has exceeded maximum usage count {MaxUsageCount}", 
                    token.Id, token.MaxUsageCount);
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token");
            return null;
        }
    }

    public async Task<RefreshToken> MarkRefreshTokenAsUsedAsync(RefreshToken token)
    {
        try
        {
            token.UsageCount++;
            token.LastUsedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Marked refresh token {TokenId} as used (usage count: {UsageCount})", 
                token.Id, token.UsageCount);

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking refresh token {TokenId} as used", token.Id);
            throw;
        }
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, Guid? revokedBy = null, string reason = "")
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            var tokenHash = HashToken(refreshToken);
            var token = await _unitOfWork.Repository<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && !rt.IsRevoked && !rt.IsDeleted);

            if (token == null)
                return false;

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedBy = revokedBy;
            token.RevocationReason = string.IsNullOrEmpty(reason) ? "Manual revocation" : reason;

            await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Revoked refresh token {TokenId} for user {UserId} (reason: {Reason})", 
                token.Id, token.UserId, token.RevocationReason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token");
            return false;
        }
    }

    public async Task<int> RevokeAllUserRefreshTokensAsync(Guid userId, Guid? revokedBy = null, string reason = "")
    {
        try
        {
            var activeTokens = await _unitOfWork.Repository<RefreshToken>()
                .FindAsync(rt => rt.UserId == userId && 
                              !rt.IsRevoked && 
                              rt.ExpiresAt > DateTime.UtcNow &&
                              !rt.IsDeleted);

            var tokens = activeTokens.ToList();
            var revokedCount = 0;

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedBy = revokedBy;
                token.RevocationReason = string.IsNullOrEmpty(reason) ? "Bulk revocation" : reason;

                await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
                revokedCount++;
            }

            if (revokedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Revoked {RevokedCount} refresh tokens for user {UserId} (reason: {Reason})", 
                    revokedCount, userId, reason);
            }

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all refresh tokens for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> RevokeAllUserRefreshTokensExceptCurrentAsync(Guid userId, string currentRefreshToken, Guid? revokedBy = null, string reason = "")
    {
        try
        {
            var currentTokenHash = HashToken(currentRefreshToken);
            var activeTokens = await _unitOfWork.Repository<RefreshToken>()
                .FindAsync(rt => rt.UserId == userId && 
                              !rt.IsRevoked && 
                              rt.ExpiresAt > DateTime.UtcNow &&
                              !rt.IsDeleted);

            var tokens = activeTokens.Where(rt => rt.TokenHash != currentTokenHash).ToList();
            var revokedCount = 0;

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedBy = revokedBy;
                token.RevocationReason = string.IsNullOrEmpty(reason) ? "Concurrent session revocation" : reason;

                await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
                revokedCount++;
            }

            if (revokedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Revoked {RevokedCount} refresh tokens for user {UserId} except current (reason: {Reason})", 
                    revokedCount, userId, reason);
            }

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh tokens except current for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> CleanupExpiredTokensAsync()
    {
        try
        {
            var expiredTokens = await _unitOfWork.Repository<RefreshToken>()
                .FindAsync(rt => rt.ExpiresAt <= DateTime.UtcNow && !rt.IsDeleted);

            var tokens = expiredTokens.ToList();
            var cleanedCount = 0;

            foreach (var token in tokens)
            {
                // Soft delete expired tokens
                token.IsDeleted = true;
                token.DeletedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
                cleanedCount++;
            }

            if (cleanedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {CleanedCount} expired refresh tokens", cleanedCount);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired refresh tokens");
            return 0;
        }
    }

    public async Task<int> GetActiveSessionCountAsync(Guid userId)
    {
        try
        {
            var activeCount = await _unitOfWork.Repository<RefreshToken>()
                .CountAsync(rt => rt.UserId == userId && 
                                !rt.IsRevoked && 
                                rt.ExpiresAt > DateTime.UtcNow &&
                                !rt.IsDeleted);

            return activeCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active session count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token value
    /// </summary>
    private string GenerateRefreshTokenValue()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// Hashes a token using SHA256
    /// </summary>
    private string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }
}