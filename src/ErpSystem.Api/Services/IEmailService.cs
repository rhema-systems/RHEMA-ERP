using ErpSystem.Core.Entities;

namespace ErpSystem.Web.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(ApplicationUser user, string resetToken, string resetUrl);
    Task<bool> SendWelcomeEmailAsync(ApplicationUser user, string temporaryPassword);
    Task<bool> SendAccountLockedEmailAsync(ApplicationUser user);
    Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);
    
    /// <summary>
    /// Sends an email with attachments
    /// </summary>
    Task<bool> SendEmailWithAttachmentsAsync(string to, string subject, string body, List<EmailAttachment> attachments, bool isHtml = false);

    // Business Partner Registration Emails
    Task<bool> SendRegistrationSubmittedEmailAsync(string email, string companyName, string applicationNumber);
    Task<bool> SendRegistrationApprovedEmailAsync(string email, string companyName, string partnerNumber);
    Task<bool> SendRegistrationRejectedEmailAsync(string email, string companyName, string reason);
    Task<bool> SendDocumentVerificationRequestEmailAsync(string email, string companyName, string documentType);
    Task<bool> SendLicenseExpiryReminderEmailAsync(string email, string companyName, string licenseType, DateTime expiryDate, int daysUntilExpiry);
}

/// <summary>
/// Email attachment model for the Web email service
/// </summary>
public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}
