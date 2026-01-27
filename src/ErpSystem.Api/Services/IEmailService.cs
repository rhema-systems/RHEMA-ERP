using ErpSystem.Core.Entities;

namespace ErpSystem.Web.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(ApplicationUser user, string resetToken, string resetUrl);
    Task<bool> SendWelcomeEmailAsync(ApplicationUser user, string temporaryPassword);
    Task<bool> SendAccountLockedEmailAsync(ApplicationUser user);
    Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);

    // Business Partner Registration Emails
    Task<bool> SendRegistrationSubmittedEmailAsync(string email, string companyName, string applicationNumber);
    Task<bool> SendRegistrationApprovedEmailAsync(string email, string companyName, string partnerNumber);
    Task<bool> SendRegistrationRejectedEmailAsync(string email, string companyName, string reason);
    Task<bool> SendDocumentVerificationRequestEmailAsync(string email, string companyName, string documentType);
    Task<bool> SendLicenseExpiryReminderEmailAsync(string email, string companyName, string licenseType, DateTime expiryDate, int daysUntilExpiry);
}
