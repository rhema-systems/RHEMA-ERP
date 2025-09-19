using ErpSystem.Core.Entities;

namespace ErpSystem.Web.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(ApplicationUser user, string resetToken, string resetUrl);
    Task<bool> SendWelcomeEmailAsync(ApplicationUser user, string temporaryPassword);
    Task<bool> SendAccountLockedEmailAsync(ApplicationUser user);
}