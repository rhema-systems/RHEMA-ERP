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

    private string GeneratePasswordResetEmailBody(ApplicationUser user, string resetToken, string resetUrl)
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

    private string GenerateWelcomeEmailBody(ApplicationUser user, string temporaryPassword)
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

    private string GenerateAccountLockedEmailBody(ApplicationUser user)
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
}