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
