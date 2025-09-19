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
}