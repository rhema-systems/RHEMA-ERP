using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Core.Services;

/// <summary>
/// Service for managing concurrent login prevention
/// </summary>
public class ConcurrentLoginService : IConcurrentLoginService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ConcurrentLoginService> _logger;

    public ConcurrentLoginService(
        IUnitOfWork unitOfWork,
        ISettingsService settingsService,
        ILogger<ConcurrentLoginService> logger)
    {
        _unitOfWork = unitOfWork;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<bool> ShouldAllowLoginAsync(Guid userId, PreventConcurrentLogin preventConcurrentLogin)
    {
        try
        {
            if (preventConcurrentLogin == PreventConcurrentLogin.Disabled)
            {
                return true;
            }

            if (preventConcurrentLogin == PreventConcurrentLogin.PreventSubsequentLogins)
            {
                var activeSessionCount = await GetActiveSessionCountAsync(userId);
                if (activeSessionCount > 0)
                {
                    _logger.LogInformation("Blocking login for user {UserId} - {ActiveSessions} active session(s) found", 
                        userId, activeSessionCount);
                    return false;
                }
            }

            // For LogoutFromAllDevices, we allow login but will invalidate other sessions
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if login should be allowed for user {UserId}", userId);
            // Fail safe - allow login on error
            return true;
        }
    }

    public async Task<int> EnforceConcurrentLoginPolicyAsync(Guid userId, PreventConcurrentLogin preventConcurrentLogin, string? currentRefreshToken = null)
    {
        try
        {
            _logger.LogInformation("EnforceConcurrentLoginPolicyAsync called for user {UserId} with policy {Policy}, currentToken provided: {HasToken}", 
                userId, preventConcurrentLogin, !string.IsNullOrEmpty(currentRefreshToken));

            if (preventConcurrentLogin != PreventConcurrentLogin.LogoutFromAllDevices)
            {
                _logger.LogInformation("Concurrent login policy is {Policy}, not LogoutFromAllDevices - skipping enforcement", preventConcurrentLogin);
                return 0;
            }

            // Get all active refresh tokens for the user
            var refreshTokens = await _unitOfWork.Repository<RefreshToken>()
                .FindAsync(rt => rt.UserId == userId && 
                              !rt.IsRevoked && 
                              rt.ExpiresAt > DateTime.UtcNow &&
                              !rt.IsDeleted);

            var activeTokens = refreshTokens.ToList();
            _logger.LogInformation("Found {ActiveTokenCount} active tokens for user {UserId}", activeTokens.Count, userId);

            var tokensToRevoke = activeTokens.ToList();
            
            // Exclude the current token if provided (the one we just created for this login)
            if (!string.IsNullOrEmpty(currentRefreshToken))
            {
                var currentTokenHash = HashToken(currentRefreshToken);
                _logger.LogInformation("Current token hash calculated. Looking for tokens to exclude with hash: {HashPrefix}...", 
                    currentTokenHash.Length > 10 ? currentTokenHash[..10] : currentTokenHash);
                
                var originalCount = tokensToRevoke.Count;
                tokensToRevoke = tokensToRevoke.Where(rt => rt.TokenHash != currentTokenHash).ToList();
                
                _logger.LogInformation("Excluded current token. Tokens to revoke: {ToRevokeCount} (was {OriginalCount})", 
                    tokensToRevoke.Count, originalCount);
            }

            // Revoke all other active tokens
            var revokedCount = 0;
            foreach (var token in tokensToRevoke)
            {
                _logger.LogInformation("Revoking token {TokenId} (Hash: {HashPrefix}...) for user {UserId}", 
                    token.Id, token.TokenHash.Length > 10 ? token.TokenHash[..10] : token.TokenHash, userId);
                
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedBy = null; // System revocation
                token.RevocationReason = "Concurrent login policy - LogoutFromAllDevices";

                await _unitOfWork.Repository<RefreshToken>().UpdateAsync(token);
                revokedCount++;
            }

            if (revokedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Successfully revoked {RevokedCount} active sessions for user {UserId} due to concurrent login policy", 
                    revokedCount, userId);
            }
            else
            {
                _logger.LogInformation("No sessions to revoke for user {UserId}", userId);
            }

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enforcing concurrent login policy for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> GetActiveSessionCountAsync(Guid userId)
    {
        try
        {
            var activeTokenCount = await _unitOfWork.Repository<RefreshToken>()
                .CountAsync(rt => rt.UserId == userId && 
                                !rt.IsRevoked && 
                                rt.ExpiresAt > DateTime.UtcNow &&
                                !rt.IsDeleted);

            return activeTokenCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active session count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<PreventConcurrentLogin> GetConcurrentLoginPolicyAsync()
    {
        try
        {
            var securitySettings = await _settingsService.GetSecuritySettingsAsync();
            var policy = securitySettings?.PreventConcurrentLogin ?? PreventConcurrentLogin.Disabled;
            _logger.LogInformation("Retrieved concurrent login policy from settings: {Policy}", policy);
            return policy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting concurrent login policy from security settings");
            // Fail safe - return disabled on error
            return PreventConcurrentLogin.Disabled;
        }
    }

    /// <summary>
    /// Hash a token using SHA256 (same method as RefreshTokenService)
    /// </summary>
    private string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }
}
