using System.Net;
using System.Net.Mail;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;

namespace ErpSystem.Web.Services;

public class ProductionEmailService : IEmailService
{
    private readonly ILogger<ProductionEmailService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IWebHostEnvironment _environment;

    public ProductionEmailService(
        ILogger<ProductionEmailService> logger,
        ISettingsService settingsService,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _settingsService = settingsService;
        _environment = environment;
    }

    public async Task<bool> SendPasswordResetEmailAsync(ApplicationUser user, string resetToken, string resetUrl)
    {
        try
        {
            var subject = "Password Reset Request - ERP System";
            var body = GeneratePasswordResetEmailBody(user, resetToken, resetUrl);

            return await SendEmailAsync(user.Email!, subject, body, true);
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
            var subject = "Welcome to ERP System";
            var body = GenerateWelcomeEmailBody(user, temporaryPassword);

            return await SendEmailAsync(user.Email!, subject, body, true);
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
            var subject = "Account Locked - ERP System";
            var body = GenerateAccountLockedEmailBody(user);

            return await SendEmailAsync(user.Email!, subject, body, true);
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
            // Get email settings from database
            var emailSettings = await _settingsService.GetEmailSettingsAsync();

            if (emailSettings == null)
            {
                _logger.LogWarning("No email settings configured in database. Please configure SMTP settings first.");

                // Fallback to development logging in development environment
                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("=== EMAIL (No SMTP Config) ===");
                    _logger.LogInformation("To: {Email}", to);
                    _logger.LogInformation("Subject: {Subject}", subject);
                    _logger.LogInformation("Is HTML: {IsHtml}", isHtml);
                    _logger.LogInformation("Body: {Body}", body);
                    _logger.LogInformation("===============================");
                    return true;
                }

                return false;
            }

            // Validate required settings
            if (string.IsNullOrEmpty(emailSettings.SmtpHost) ||
                string.IsNullOrEmpty(emailSettings.FromAddress))
            {
                _logger.LogWarning("Incomplete email settings configuration. SMTP host and from address are required.");
                return false;
            }

            // Create and configure SMTP client
            using var smtpClient = new SmtpClient(emailSettings.SmtpHost, emailSettings.SmtpPort);

            if (!string.IsNullOrEmpty(emailSettings.SmtpUsername))
            {
                smtpClient.Credentials = new NetworkCredential(emailSettings.SmtpUsername, emailSettings.SmtpPassword);
            }

            smtpClient.EnableSsl = emailSettings.UseTLS;
            smtpClient.Timeout = 30000; // 30 seconds timeout

            // Create email message
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(emailSettings.FromAddress, emailSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            mailMessage.To.Add(new MailAddress(to));

            // Send email
            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation("Successfully sent email to {Email} with subject '{Subject}'", to, subject);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "SMTP error occurred while sending email to {Email}: {SmtpError}", to, smtpEx.Message);
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
            // Get email settings from database
            var emailSettings = await _settingsService.GetEmailSettingsAsync();

            if (emailSettings == null)
            {
                _logger.LogWarning("No email settings configured in database. Please configure SMTP settings first.");

                // Fallback to development logging in development environment
                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("=== EMAIL WITH ATTACHMENTS (No SMTP Config) ===");
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
                    _logger.LogInformation("================================================");
                    return true;
                }

                return false;
            }

            // Validate required settings
            if (string.IsNullOrEmpty(emailSettings.SmtpHost) ||
                string.IsNullOrEmpty(emailSettings.FromAddress))
            {
                _logger.LogWarning("Incomplete email settings configuration. SMTP host and from address are required.");
                return false;
            }

            // Create and configure SMTP client
            using var smtpClient = new SmtpClient(emailSettings.SmtpHost, emailSettings.SmtpPort);

            if (!string.IsNullOrEmpty(emailSettings.SmtpUsername))
            {
                smtpClient.Credentials = new NetworkCredential(emailSettings.SmtpUsername, emailSettings.SmtpPassword);
            }

            smtpClient.EnableSsl = emailSettings.UseTLS;
            smtpClient.Timeout = 30000; // 30 seconds timeout

            // Create email message
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(emailSettings.FromAddress, emailSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            mailMessage.To.Add(new MailAddress(to));

            // Add attachments
            if (attachments != null && attachments.Count > 0)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment.Content != null && attachment.Content.Length > 0)
                    {
                        var memoryStream = new MemoryStream(attachment.Content);
                        var mailAttachment = new Attachment(memoryStream, attachment.FileName, attachment.ContentType);
                        mailMessage.Attachments.Add(mailAttachment);
                    }
                }
            }

            // Send email
            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation("Successfully sent email with {AttachmentCount} attachment(s) to {Email} with subject '{Subject}'",
                attachments?.Count ?? 0, to, subject);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "SMTP error occurred while sending email with attachments to {Email}: {SmtpError}", to, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email with attachments to {Email}", to);
            return false;
        }
    }

    private static string GeneratePasswordResetEmailBody(ApplicationUser user, string resetToken, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Password Reset Request</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .button {{ display: inline-block; background-color: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Password Reset Request</h1>
        </div>
        <div class=""content"">
            <h2>Hello {user.FirstName} {user.LastName},</h2>
            <p>We received a request to reset the password for your ERP System account.</p>
            <p>Click the button below to reset your password:</p>
            <a href=""{resetUrl}"" class=""button"">Reset Password</a>
            <p>If the button doesn't work, you can copy and paste the following URL into your browser:</p>
            <p><a href=""{resetUrl}"">{resetUrl}</a></p>
            <div class=""warning"">
                <strong>Important:</strong>
                <ul>
                    <li>This link will expire in 1 hour</li>
                    <li>If you didn't request this password reset, please ignore this email</li>
                    <li>Never share this link with anyone</li>
                </ul>
            </div>
        </div>
        <div class=""footer"">
            <p>This email was sent by ERP System. If you have any questions, please contact your system administrator.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateWelcomeEmailBody(ApplicationUser user, string temporaryPassword)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Welcome to ERP System</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .credentials {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
        .warning {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Welcome to ERP System</h1>
        </div>
        <div class=""content"">
            <h2>Hello {user.FirstName} {user.LastName},</h2>
            <p>Your account has been created successfully! Here are your login credentials:</p>
            <div class=""credentials"">
                <p><strong>Email:</strong> {user.Email}</p>
                <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
            </div>
            <div class=""warning"">
                <strong>Important Security Notice:</strong>
                <ul>
                    <li>Please log in and change your password immediately</li>
                    <li>This temporary password should only be used for your first login</li>
                    <li>Choose a strong password with at least 8 characters</li>
                    <li>Never share your login credentials with anyone</li>
                </ul>
            </div>
            <p>You can access the system by visiting the login page and using the credentials provided above.</p>
        </div>
        <div class=""footer"">
            <p>This email was sent by ERP System. If you have any questions, please contact your system administrator.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateAccountLockedEmailBody(ApplicationUser user)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Account Locked - ERP System</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
        .alert {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Account Locked</h1>
        </div>
        <div class=""content"">
            <h2>Hello {user.FirstName} {user.LastName},</h2>
            <div class=""alert"">
                <p><strong>Your account has been temporarily locked</strong></p>
                <p>This happened due to multiple unsuccessful login attempts.</p>
            </div>
            <p>For security reasons, your account access has been temporarily restricted. This helps protect your account from unauthorized access attempts.</p>
            <p><strong>What to do next:</strong></p>
            <ul>
                <li>Wait for the automatic unlock period to expire, or</li>
                <li>Contact your system administrator to unlock your account immediately</li>
            </ul>
            <p>If you believe this lockout was triggered in error or if you suspect unauthorized access attempts, please contact your system administrator immediately.</p>
        </div>
        <div class=""footer"">
            <p>This email was sent by ERP System. If you have any questions, please contact your system administrator.</p>
        </div>
    </div>
</body>
</html>";
    }

    #region Business Partner Registration Emails

    public async Task<bool> SendRegistrationSubmittedEmailAsync(string email, string companyName, string applicationNumber)
    {
        try
        {
            var subject = "Business Partner Registration Submitted - ERP System";
            var body = GenerateRegistrationSubmittedEmailBody(companyName, applicationNumber);

            return await SendEmailAsync(email, subject, body, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration submitted email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendRegistrationApprovedEmailAsync(string email, string companyName, string partnerNumber)
    {
        try
        {
            var subject = "Business Partner Registration Approved - ERP System";
            var body = GenerateRegistrationApprovedEmailBody(companyName, partnerNumber);

            return await SendEmailAsync(email, subject, body, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration approved email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendRegistrationRejectedEmailAsync(string email, string companyName, string reason)
    {
        try
        {
            var subject = "Business Partner Registration Update - ERP System";
            var body = GenerateRegistrationRejectedEmailBody(companyName, reason);

            return await SendEmailAsync(email, subject, body, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send registration rejected email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendDocumentVerificationRequestEmailAsync(string email, string companyName, string documentType)
    {
        try
        {
            var subject = "Document Verification Required - ERP System";
            var body = GenerateDocumentVerificationRequestEmailBody(companyName, documentType);

            return await SendEmailAsync(email, subject, body, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document verification request email to {Email}", email);
            return false;
        }
    }

    public async Task<bool> SendLicenseExpiryReminderEmailAsync(string email, string companyName, string licenseType, DateTime expiryDate, int daysUntilExpiry)
    {
        try
        {
            var subject = $"License Expiry Reminder - {daysUntilExpiry} Days Remaining";
            var body = GenerateLicenseExpiryReminderEmailBody(companyName, licenseType, expiryDate, daysUntilExpiry);

            return await SendEmailAsync(email, subject, body, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send license expiry reminder email to {Email}", email);
            return false;
        }
    }

    private static string GenerateRegistrationSubmittedEmailBody(string companyName, string applicationNumber)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Registration Submitted</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .info-box {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>✓ Registration Submitted Successfully</h1>
        </div>
        <div class=""content"">
            <h2>Dear {companyName},</h2>
            <p>Thank you for submitting your business partner registration application with our ERP System.</p>
            <div class=""info-box"">
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Submitted Date:</strong> {DateTime.UtcNow:MMMM dd, yyyy}</p>
            </div>
            <p><strong>What happens next?</strong></p>
            <ul>
                <li>Our team will review your application and supporting documents</li>
                <li>We may contact you if additional information is required</li>
                <li>You will receive an email notification once the review is complete</li>
                <li>You can track your application status in the external portal</li>
            </ul>
            <p>The review process typically takes 3-5 business days.</p>
            <p>If you have any questions, please contact our procurement team.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from ERP System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateRegistrationApprovedEmailBody(string companyName, string partnerNumber)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Registration Approved</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .success-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 Registration Approved!</h1>
        </div>
        <div class=""content"">
            <h2>Congratulations {companyName}!</h2>
            <p>We are pleased to inform you that your business partner registration has been approved.</p>
            <div class=""success-box"">
                <p><strong>Partner Number:</strong> {partnerNumber}</p>
                <p><strong>Status:</strong> Active</p>
                <p><strong>Approval Date:</strong> {DateTime.UtcNow:MMMM dd, yyyy}</p>
            </div>
            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>You can now access all business partner features in the external portal</li>
                <li>Your partner number will be used for all future transactions</li>
                <li>Please keep your company information and documents up to date</li>
                <li>Review and comply with our business partner terms and conditions</li>
            </ul>
            <p>We look forward to a successful partnership!</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from ERP System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateRegistrationRejectedEmailBody(string companyName, string reason)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Registration Update</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ffc107; color: #333; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .warning-box {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Registration Update</h1>
        </div>
        <div class=""content"">
            <h2>Dear {companyName},</h2>
            <p>Thank you for your interest in becoming a business partner. After careful review, we regret to inform you that we are unable to approve your registration at this time.</p>
            <div class=""warning-box"">
                <p><strong>Reason:</strong></p>
                <p>{reason}</p>
            </div>
            <p><strong>What you can do:</strong></p>
            <ul>
                <li>Review the reason provided above</li>
                <li>Address any issues or concerns mentioned</li>
                <li>You may submit a new application once the issues are resolved</li>
                <li>Contact our procurement team if you need clarification</li>
            </ul>
            <p>We appreciate your understanding and hope to work with you in the future.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from ERP System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateDocumentVerificationRequestEmailBody(string companyName, string documentType)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Document Verification Required</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #17a2b8; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .info-box {{ background-color: #d1ecf1; border: 1px solid #bee5eb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>📄 Document Verification Required</h1>
        </div>
        <div class=""content"">
            <h2>Dear {companyName},</h2>
            <p>As part of our business partner registration review process, we need additional verification for one of your submitted documents.</p>
            <div class=""info-box"">
                <p><strong>Document Type:</strong> {documentType}</p>
                <p><strong>Action Required:</strong> Please review and update this document</p>
            </div>
            <p><strong>What you need to do:</strong></p>
            <ul>
                <li>Log in to the external portal</li>
                <li>Navigate to your registration application</li>
                <li>Review the document mentioned above</li>
                <li>Upload a clearer copy or provide additional information if needed</li>
            </ul>
            <p>Please complete this verification within 5 business days to avoid delays in processing your application.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from ERP System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateLicenseExpiryReminderEmailBody(string companyName, string licenseType, DateTime expiryDate, int daysUntilExpiry)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>License Expiry Reminder</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ff6b6b; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .alert-box {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>⚠️ License Expiry Reminder</h1>
        </div>
        <div class=""content"">
            <h2>Dear {companyName},</h2>
            <p>This is a reminder that one of your business licenses is approaching its expiry date.</p>
            <div class=""alert-box"">
                <p><strong>License Type:</strong> {licenseType}</p>
                <p><strong>Expiry Date:</strong> {expiryDate:MMMM dd, yyyy}</p>
                <p><strong>Days Remaining:</strong> {daysUntilExpiry} days</p>
            </div>
            <p><strong>Action Required:</strong></p>
            <ul>
                <li>Renew your {licenseType} before {expiryDate:MMMM dd, yyyy}</li>
                <li>Upload the renewed license document to the external portal</li>
                <li>Ensure all license information is up to date</li>
            </ul>
            <p><strong>Important:</strong> Failure to renew your license may result in suspension of your business partner status and affect ongoing transactions.</p>
            <p>Please take action as soon as possible to avoid any disruption to our partnership.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from ERP System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    #endregion
}
