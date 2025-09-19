using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using System.Net.Mail;
using System.Net;

namespace ErpSystem.Core.Services;

public interface ISettingsService
{
    Task<EmailSettings?> GetEmailSettingsAsync();
    Task<EmailSettings> UpdateEmailSettingsAsync(EmailSettings settings);
    Task<bool> TestEmailSettingsAsync(EmailSettings settings, string testEmail);
    
    Task<PasswordPolicy?> GetPasswordPolicyAsync();
    Task<PasswordPolicy> UpdatePasswordPolicyAsync(PasswordPolicy policy);
    
    Task<SystemSettings?> GetSystemSettingAsync(string key);
    Task<SystemSettings> SetSystemSettingAsync(string key, string value, string? description = null);
    Task<IEnumerable<SystemSettings>> GetAllSystemSettingsAsync();
    Task DeleteSystemSettingAsync(string key);
}

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICryptoService _cryptoService;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICryptoService cryptoService,
        ILogger<SettingsService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cryptoService = cryptoService;
        _logger = logger;
    }

    #region Email Settings

    public async Task<EmailSettings?> GetEmailSettingsAsync()
    {
        try
        {
            var settings = await _unitOfWork.Repository<EmailSettings>().GetAllAsync();
            var emailSettings = settings.FirstOrDefault();
            
            if (emailSettings != null && !string.IsNullOrEmpty(emailSettings.SmtpPassword))
            {
                // Decrypt the password for use (but don't modify the entity)
                try
                {
                    var decryptedPassword = _cryptoService.Decrypt(emailSettings.SmtpPassword);
                    emailSettings.SmtpPassword = decryptedPassword;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt SMTP password, password may be stored in plain text");
                    // If decryption fails, assume it's plain text (for backwards compatibility)
                }
            }
            
            return emailSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email settings");
            throw;
        }
    }

    public async Task<EmailSettings> UpdateEmailSettingsAsync(EmailSettings settings)
    {
        try
        {
            var existingSettings = await GetEmailSettingsAsync();
            
            if (existingSettings != null)
            {
                // Update existing settings
                existingSettings.SmtpHost = settings.SmtpHost;
                existingSettings.SmtpPort = settings.SmtpPort;
                existingSettings.SmtpUsername = settings.SmtpUsername;
                existingSettings.SmtpPassword = !string.IsNullOrEmpty(settings.SmtpPassword) ? _cryptoService.Encrypt(settings.SmtpPassword) : settings.SmtpPassword;
                existingSettings.UseTLS = settings.UseTLS;
                existingSettings.FromAddress = settings.FromAddress;
                existingSettings.FromName = settings.FromName;
                existingSettings.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Repository<EmailSettings>().UpdateAsync(existingSettings);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Updated email settings");
                return existingSettings;
            }
            else
            {
                // Create new settings
                settings.Id = Guid.NewGuid();
                settings.TenantId = _currentUserService.GetTenantId() ?? throw new InvalidOperationException("Tenant ID is required");
                settings.CreatedAt = DateTime.UtcNow;
                settings.CreatedBy = _currentUserService.GetUsername();
                
                // Encrypt the password before storing
                if (!string.IsNullOrEmpty(settings.SmtpPassword))
                {
                    settings.SmtpPassword = _cryptoService.Encrypt(settings.SmtpPassword);
                }
                
                var createdSettings = await _unitOfWork.Repository<EmailSettings>().AddAsync(settings);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Created new email settings");
                return createdSettings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email settings");
            throw;
        }
    }

    public async Task<bool> TestEmailSettingsAsync(EmailSettings settings, string testEmail)
    {
        try
        {
            using var smtpClient = new SmtpClient(settings.SmtpHost, settings.SmtpPort);
            
            if (!string.IsNullOrEmpty(settings.SmtpUsername))
            {
                smtpClient.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);
            }
            
            smtpClient.EnableSsl = settings.UseTLS;
            smtpClient.Timeout = 30000; // 30 seconds

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName),
                Subject = "ERP System - SMTP Test Email",
                Body = "This is a test email to verify your SMTP configuration is working correctly.",
                IsBodyHtml = false
            };
            
            mailMessage.To.Add(new MailAddress(testEmail));

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Successfully sent test email to {TestEmail}", testEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send test email to {TestEmail}", testEmail);
            return false;
        }
    }

    #endregion

    #region Password Policy

    public async Task<PasswordPolicy?> GetPasswordPolicyAsync()
    {
        try
        {
            var policies = await _unitOfWork.Repository<PasswordPolicy>().GetAllAsync();
            return policies.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving password policy");
            throw;
        }
    }

    public async Task<PasswordPolicy> UpdatePasswordPolicyAsync(PasswordPolicy policy)
    {
        try
        {
            var existingPolicy = await GetPasswordPolicyAsync();
            
            if (existingPolicy != null)
            {
                // Update existing policy
                existingPolicy.MinLength = policy.MinLength;
                existingPolicy.RequireUppercase = policy.RequireUppercase;
                existingPolicy.RequireLowercase = policy.RequireLowercase;
                existingPolicy.RequireDigits = policy.RequireDigits;
                existingPolicy.RequireSpecialChars = policy.RequireSpecialChars;
                existingPolicy.MaxAge = policy.MaxAge;
                existingPolicy.PreventReuse = policy.PreventReuse;
                existingPolicy.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Repository<PasswordPolicy>().UpdateAsync(existingPolicy);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Updated password policy");
                return existingPolicy;
            }
            else
            {
                // Create new policy
                policy.Id = Guid.NewGuid();
                policy.TenantId = _currentUserService.GetTenantId() ?? throw new InvalidOperationException("Tenant ID is required");
                policy.CreatedAt = DateTime.UtcNow;
                policy.CreatedBy = _currentUserService.GetUsername();
                var createdPolicy = await _unitOfWork.Repository<PasswordPolicy>().AddAsync(policy);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Created new password policy");
                return createdPolicy;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password policy");
            throw;
        }
    }

    #endregion

    #region System Settings

    public async Task<SystemSettings?> GetSystemSettingAsync(string key)
    {
        try
        {
            var settings = await _unitOfWork.Repository<SystemSettings>().FindAsync(s => s.Key == key);
            return settings.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system setting {Key}", key);
            throw;
        }
    }

    public async Task<SystemSettings> SetSystemSettingAsync(string key, string value, string? description = null)
    {
        try
        {
            var existingSetting = await GetSystemSettingAsync(key);
            
            if (existingSetting != null)
            {
                existingSetting.Value = value;
                existingSetting.Description = description ?? existingSetting.Description;
                existingSetting.UpdatedAt = DateTime.UtcNow;
                
                await _unitOfWork.Repository<SystemSettings>().UpdateAsync(existingSetting);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Updated system setting {Key}", key);
                return existingSetting;
            }
            else
            {
                var newSetting = new SystemSettings
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    Description = description,
                    TenantId = _currentUserService.GetTenantId() ?? throw new InvalidOperationException("Tenant ID is required"),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUserService.GetUsername()
                };
                
                var createdSetting = await _unitOfWork.Repository<SystemSettings>().AddAsync(newSetting);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Created system setting {Key}", key);
                return createdSetting;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting system setting {Key}", key);
            throw;
        }
    }

    public async Task<IEnumerable<SystemSettings>> GetAllSystemSettingsAsync()
    {
        try
        {
            return await _unitOfWork.Repository<SystemSettings>().GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all system settings");
            throw;
        }
    }

    public async Task DeleteSystemSettingAsync(string key)
    {
        try
        {
            var setting = await GetSystemSettingAsync(key);
            if (setting != null)
            {
                await _unitOfWork.Repository<SystemSettings>().DeleteAsync(setting.Id);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Deleted system setting {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting system setting {Key}", key);
            throw;
        }
    }

    #endregion
}