using ErpSystem.Core.Entities;

namespace ErpSystem.Web.Services;

public class SimpleEmailService : IEmailService
{
    private readonly ILogger<SimpleEmailService> _logger;
    private readonly IWebHostEnvironment _environment;

    public SimpleEmailService(ILogger<SimpleEmailService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task<bool> SendPasswordResetEmailAsync(ApplicationUser user, string resetToken, string resetUrl)
    {
        try
        {
            // In development, just log the reset information
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== PASSWORD RESET EMAIL ===");
                _logger.LogInformation("To: {Email} ({FirstName} {LastName})", user.Email, user.FirstName, user.LastName);
                _logger.LogInformation("Subject: Password Reset Request - ERP System");
                _logger.LogInformation("Reset URL: {ResetUrl}", resetUrl);
                _logger.LogInformation("Reset Token: {ResetToken}", resetToken);
                _logger.LogInformation("Token expires in 1 hour");
                _logger.LogInformation("============================");

                // Simulate email sending delay
                await Task.Delay(500);
                return true;
            }

            // In production, implement actual email sending logic here
            // Examples: SendGrid, SMTP, Amazon SES, etc.
            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(ApplicationUser user, string temporaryPassword)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== WELCOME EMAIL ===");
                _logger.LogInformation("To: {Email} ({FirstName} {LastName})", user.Email, user.FirstName, user.LastName);
                _logger.LogInformation("Subject: Welcome to ERP System");
                _logger.LogInformation("Temporary Password: {TempPassword}", temporaryPassword);
                _logger.LogInformation("Please change your password after first login");
                _logger.LogInformation("=====================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
            return false;
        }
    }

    public async Task<bool> SendAccountLockedEmailAsync(ApplicationUser user)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== ACCOUNT LOCKED EMAIL ===");
                _logger.LogInformation("To: {Email} ({FirstName} {LastName})", user.Email, user.FirstName, user.LastName);
                _logger.LogInformation("Subject: Account Locked - ERP System");
                _logger.LogInformation("Your account has been temporarily locked due to multiple failed login attempts");
                _logger.LogInformation("Contact your administrator to unlock your account");
                _logger.LogInformation("============================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account locked email to {Email}", user.Email);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== GENERIC EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Subject: {Subject}", subject);
                _logger.LogInformation("Is HTML: {IsHtml}", isHtml);
                _logger.LogInformation("Body: {Body}", body);
                _logger.LogInformation("=====================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendEmailWithAttachmentsAsync(string to, string subject, string body, List<EmailAttachment> attachments, bool isHtml = false)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== EMAIL WITH ATTACHMENTS ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Subject: {Subject}", subject);
                _logger.LogInformation("Is HTML: {IsHtml}", isHtml);
                _logger.LogInformation("Attachments: {AttachmentCount}", attachments?.Count ?? 0);
                
                if (attachments != null && attachments.Count > 0)
                {
                    foreach (var attachment in attachments)
                    {
                        _logger.LogInformation("  - {FileName} ({ContentType}, {Size} bytes)",
                            attachment.FileName,
                            attachment.ContentType,
                            attachment.Content?.Length ?? 0);
                    }
                }
                
                _logger.LogInformation("Body Preview: {BodyPreview}", body?.Length > 200 ? body.Substring(0, 200) + "..." : body);
                _logger.LogInformation("==============================");

                await Task.Delay(300);
                return true;
            }

            // In production, implement actual email sending with attachments
            // Examples: SendGrid, SMTP with System.Net.Mail, Amazon SES, etc.
            _logger.LogWarning("Email service with attachments not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email with attachments to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendRegistrationSubmittedEmailAsync(string to, string companyName, string registrationId)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== REGISTRATION SUBMITTED EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Company: {CompanyName}", companyName);
                _logger.LogInformation("Registration ID: {RegistrationId}", registrationId);
                _logger.LogInformation("Subject: Registration Submitted - ERP System");
                _logger.LogInformation("=====================================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration submitted email to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendRegistrationApprovedEmailAsync(string to, string companyName, string registrationId)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== REGISTRATION APPROVED EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Company: {CompanyName}", companyName);
                _logger.LogInformation("Registration ID: {RegistrationId}", registrationId);
                _logger.LogInformation("Subject: Registration Approved - ERP System");
                _logger.LogInformation("===================================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration approved email to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendRegistrationRejectedEmailAsync(string to, string companyName, string registrationId)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== REGISTRATION REJECTED EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Company: {CompanyName}", companyName);
                _logger.LogInformation("Registration ID: {RegistrationId}", registrationId);
                _logger.LogInformation("Subject: Registration Rejected - ERP System");
                _logger.LogInformation("====================================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration rejected email to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendDocumentVerificationRequestEmailAsync(string to, string companyName, string documentType)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== DOCUMENT VERIFICATION REQUEST EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Company: {CompanyName}", companyName);
                _logger.LogInformation("Document Type: {DocumentType}", documentType);
                _logger.LogInformation("Subject: Document Verification Required - ERP System");
                _logger.LogInformation("================================================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document verification request email to {Email}", to);
            return false;
        }
    }

    public async Task<bool> SendLicenseExpiryReminderEmailAsync(string to, string companyName, string licenseType, DateTime expiryDate, int daysUntilExpiry)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("=== LICENSE EXPIRY REMINDER EMAIL ===");
                _logger.LogInformation("To: {Email}", to);
                _logger.LogInformation("Company: {CompanyName}", companyName);
                _logger.LogInformation("License Type: {LicenseType}", licenseType);
                _logger.LogInformation("Expiry Date: {ExpiryDate}", expiryDate);
                _logger.LogInformation("Days Until Expiry: {DaysUntilExpiry}", daysUntilExpiry);
                _logger.LogInformation("Subject: License Expiry Reminder - ERP System");
                _logger.LogInformation("==========================================");

                await Task.Delay(300);
                return true;
            }

            _logger.LogWarning("Email service not configured for production environment");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send license expiry reminder email to {Email}", to);
            return false;
        }
    }
}
