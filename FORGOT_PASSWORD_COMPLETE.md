# Forgot Password Implementation - Complete Summary

## ✅ COMPLETED STEPS

### Step 1: DbSet Added ✅
**File:** `ApplicationDbContext.cs` (line 83)
```csharp
public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
```

### Step 2: DI Registration Added ✅
**File:** `ServiceCollectionExtensions.cs` (line 303)
```csharp
services.AddScoped<IPasswordResetService, PasswordResetService>();
```

### Step 3-7: AuthController Updated ✅
**File:** `AuthController.cs`
- Added using statements: `ErpSystem.Core.DTOs.Auth` and `ErpSystem.Core.Interfaces.Common`
- Added fields: `IPasswordResetService` and `IEmailService`
- Updated constructor to include both parameters
- Added three endpoints:
  - `POST /api/auth/forgot-password` - Request password reset
  - `POST /api/auth/reset-password` - Reset password with token
  - `POST /api/auth/validate-reset-token` - Validate token
- Added `GeneratePasswordResetEmailBody()` helper method

### Files Created ✅
1. **PasswordResetToken.cs** - Entity with all required fields
2. **PasswordResetDtos.cs** - All request/response DTOs

## ⏳ REMAINING TASKS

### Create Interface File
**Path:** `src/ErpSystem.Core/Interfaces/IPasswordResetService.cs`

```csharp
using ErpSystem.Core.DTOs.Auth;

namespace ErpSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for managing password reset tokens and operations
    /// </summary>
    public interface IPasswordResetService
    {
        /// <summary>
        /// Generates a password reset token for a user
        /// </summary>
        Task<string> GeneratePasswordResetTokenAsync(Guid userId, string ipAddress, string userAgent, int tokenExpiryMinutes = 15);

        /// <summary>
        /// Validates a password reset token
        /// </summary>
        Task<bool> ValidateResetTokenAsync(Guid userId, string token);

        /// <summary>
        /// Resets a user's password using a valid reset token
        /// </summary>
        Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword);

        /// <summary>
        /// Gets user ID by email address
        /// </summary>
        Task<Guid?> GetUserIdByEmailAsync(string email);

        /// <summary>
        /// Invalidates/revokes all reset tokens for a user
        /// </summary>
        Task<int> InvalidateAllTokensForUserAsync(Guid userId);

        /// <summary>
        /// Cleans up expired reset tokens from the database
        /// </summary>
        Task<int> CleanupExpiredTokensAsync();
    }
}
```

### Create Service Implementation File
**Path:** `src/ErpSystem.Core/Services/PasswordResetService.cs`

```csharp
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ErpSystem.Core.Services
{
    /// <summary>
    /// Service for managing password reset tokens and operations
    /// </summary>
    public class PasswordResetService : IPasswordResetService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly ISecurityLogService _securityLogService;

        public PasswordResetService(
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork,
            ILogger<PasswordResetService> logger,
            ISecurityLogService securityLogService)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _securityLogService = securityLogService;
        }

        public async Task<string> GeneratePasswordResetTokenAsync(Guid userId, string ipAddress, string userAgent, int tokenExpiryMinutes = 15)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    _logger.LogWarning("Attempt to generate password reset token for non-existent user {UserId}", userId);
                    throw new InvalidOperationException("User not found");
                }

                var tokenBytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                var token = Convert.ToBase64String(tokenBytes);

                var resetToken = new PasswordResetToken
                {
                    UserId = userId,
                    Token = token,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes),
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false,
                    RequestedFromIpAddress = ipAddress,
                    RequestedFromUserAgent = userAgent
                };

                await _unitOfWork.Repository<PasswordResetToken>().AddAsync(resetToken);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Password reset token generated for user {UserId} from IP {IpAddress}", userId, ipAddress);

                var securityLog = new SecurityLog
                {
                    Action = "PasswordResetTokenGenerated",
                    Success = true,
                    IpAddress = ipAddress,
                    Username = user.UserName,
                    UserId = userId,
                    Details = $"Password reset token generated. Valid for {tokenExpiryMinutes} minutes.",
                    UserAgent = userAgent,
                    TenantId = user.TenantId
                };
                await _securityLogService.CreateSecurityLogAsync(securityLog);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating password reset token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateResetTokenAsync(Guid userId, string token)
        {
            try
            {
                var repo = _unitOfWork.Repository<PasswordResetToken>();
                var resetToken = await repo.FirstOrDefaultAsync(rt =>
                    rt.UserId == userId &&
                    rt.Token == token &&
                    !rt.IsUsed &&
                    rt.ExpiryTime > DateTime.UtcNow);

                return resetToken != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password reset token for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword)
        {
            try
            {
                if (!await ValidateResetTokenAsync(userId, token))
                {
                    _logger.LogWarning("Invalid or expired password reset token for user {UserId}", userId);
                    return false;
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    _logger.LogWarning("User not found when attempting to reset password {UserId}", userId);
                    return false;
                }

                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                {
                    _logger.LogError("Failed to remove old password for user {UserId}", userId);
                    return false;
                }

                var addPasswordResult = await _userManager.AddPasswordAsync(user, newPassword);
                if (!addPasswordResult.Succeeded)
                {
                    _logger.LogError("Failed to set new password for user {UserId}. Errors: {Errors}",
                        userId, string.Join(", ", addPasswordResult.Errors.Select(e => e.Description)));
                    return false;
                }

                var repo = _unitOfWork.Repository<PasswordResetToken>();
                var resetTokenEntity = await repo.FirstOrDefaultAsync(rt =>
                    rt.UserId == userId &&
                    rt.Token == token);

                if (resetTokenEntity != null)
                {
                    resetTokenEntity.IsUsed = true;
                    resetTokenEntity.UsedAt = DateTime.UtcNow;
                    await repo.UpdateAsync(resetTokenEntity);
                    await _unitOfWork.SaveChangesAsync();
                }

                _logger.LogInformation("Password successfully reset for user {UserId}", userId);

                var securityLog = new SecurityLog
                {
                    Action = "PasswordReset",
                    Success = true,
                    IpAddress = "System",
                    Username = user.UserName,
                    UserId = userId,
                    Details = "User password reset via forgot password flow",
                    TenantId = user.TenantId
                };
                await _securityLogService.CreateSecurityLogAsync(securityLog);

                await InvalidateAllTokensForUserAsync(userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
                return false;
            }
        }

        public async Task<Guid?> GetUserIdByEmailAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                return user?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding user by email");
                return null;
            }
        }

        public async Task<int> InvalidateAllTokensForUserAsync(Guid userId)
        {
            try
            {
                var repo = _unitOfWork.Repository<PasswordResetToken>();
                var tokens = await repo.FindAsync(rt => rt.UserId == userId && !rt.IsUsed);

                var invalidatedCount = 0;
                foreach (var token in tokens)
                {
                    token.IsUsed = true;
                    token.UsedAt = DateTime.UtcNow;
                    await repo.UpdateAsync(token);
                    invalidatedCount++;
                }

                if (invalidatedCount > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Invalidated {Count} password reset tokens for user {UserId}", invalidatedCount, userId);
                }

                return invalidatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating password reset tokens for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> CleanupExpiredTokensAsync()
        {
            try
            {
                var repo = _unitOfWork.Repository<PasswordResetToken>();
                var expiredTokens = await repo.FindAsync(rt => rt.ExpiryTime < DateTime.UtcNow);

                var deletedCount = 0;
                foreach (var token in expiredTokens)
                {
                    await repo.DeleteAsync(token.Id);
                    deletedCount++;
                }

                if (deletedCount > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} expired password reset tokens", deletedCount);
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired password reset tokens");
                return 0;
            }
        }
    }
}
```

## ⏳ FINAL STEP: Database Migration

Run these commands in the terminal from the `ErpSystem.Api` directory:

```bash
dotnet ef migrations add AddPasswordResetTokens
dotnet ef database update
```

## Testing the Flow

1. **Request password reset:**
```
POST /api/auth/forgot-password
Content-Type: application/json

{
  "email": "user@example.com"
}
```

2. **Reset password:**
```
POST /api/auth/reset-password
Content-Type: application/json

{
  "email": "user@example.com",
  "resetToken": "[token from email]",
  "newPassword": "NewSecurePass123!",
  "confirmPassword": "NewSecurePass123!"
}
```

3. **Login with new password:**
```
POST /api/auth/login
Content-Type: application/json

{
  "username": "user@example.com",
  "password": "NewSecurePass123!"
}
```

## Summary of Changes

✅ Steps 1-7 completed
- DbSet added
- DI registration added  
- AuthController updated with endpoints
- Entity and DTOs created

⏳ Final steps (copy/paste the code above):
- Create `IPasswordResetService.cs`
- Create `PasswordResetService.cs`
- Run migrations

**Security features implemented:**
- Cryptographically secure token generation
- Token expiry (15 minutes default)
- Single-use tokens
- Auto-invalidation of all tokens after reset
- Email privacy (no confirmation if account exists)
- Full audit logging
- IP/User-Agent tracking
