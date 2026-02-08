using System.Net;
using System.Net.Mail;
using System.Reflection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public interface ISettingsService
{
    Task<EmailSettings?> GetEmailSettingsAsync();
    Task<EmailSettings> UpdateEmailSettingsAsync(EmailSettings settings);
    Task<bool> TestEmailSettingsAsync(EmailSettings settings, string testEmail);


    Task<Security?> GetSecuritySettingsAsync();
    Task<Security?> GetSecuritySettingsAsync(Guid tenantId);
    Task<Security?> GetPublicSecuritySettingsAsync();
    Task<Security> UpdateSecuritySettingsAsync(Security settings);

    Task<SystemSettings?> GetSystemSettingAsync(string key);
    Task<SystemSettings> SetSystemSettingAsync(string key, string value, string? description = null);
    Task<IEnumerable<SystemSettings>> GetAllSystemSettingsAsync();
    Task DeleteSystemSettingAsync(string key);
    
    // Field Label Configuration
    Task<Dictionary<string, string>> GetFieldLabelsAsync(string module);
    Task<Dictionary<string, string>> SetFieldLabelsAsync(string module, Dictionary<string, string> labels);
    Task<string> GetFieldLabelAsync(string module, string fieldName);
    Task SetFieldLabelAsync(string module, string fieldName, string label);
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
            // Retrieve email settings without tenant filter
            // Using a simple query that gets the first non-deleted record
            // This works even for anonymous requests where TenantId is not available
            var emailSettings = await _unitOfWork.Repository<EmailSettings>()
                .FirstOrDefaultAsync(e => true); // Simple predicate to get first record

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
                settings.TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
                settings.CreatedAt = DateTime.UtcNow;
                settings.CreatedBy = _currentUserService.UserName;

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


    #region Security Settings

    public async Task<Security?> GetSecuritySettingsAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
            var settings = await _unitOfWork.Repository<Security>().FindAsync(s => s.TenantId == tenantId);
            return settings.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security settings");
            throw;
        }
    }

    public async Task<Security?> GetSecuritySettingsAsync(Guid tenantId)
    {
        try
        {
            var settings = await _unitOfWork.Repository<Security>().FindAsync(s => s.TenantId == tenantId);
            return settings.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security settings for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Gets security settings for public access (login, registration) from default tenant
    /// </summary>
    public async Task<Security?> GetPublicSecuritySettingsAsync()
    {
        try
        {
            // Get settings from default tenant or first available tenant
            var settings = await _unitOfWork.Repository<Security>().GetAllAsync();
            var defaultSettings = settings.FirstOrDefault();

            if (defaultSettings != null)
            {
                _logger.LogInformation("Retrieved public security settings from tenant {TenantId}: CAPTCHA enabled = {CaptchaEnabled}",
                    defaultSettings.TenantId, defaultSettings.CaptchaEnabled);
            }
            else
            {
                _logger.LogWarning("No security settings found in database for public access");
            }

            return defaultSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public security settings");
            throw;
        }
    }

    public async Task<Security> UpdateSecuritySettingsAsync(Security settings)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
            var existingSettings = await GetSecuritySettingsAsync();

            if (existingSettings != null)
            {
                // Update existing settings
                existingSettings.PasswordMinLength = settings.PasswordMinLength;
                existingSettings.PasswordRequireUppercase = settings.PasswordRequireUppercase;
                existingSettings.PasswordRequireLowercase = settings.PasswordRequireLowercase;
                existingSettings.PasswordRequireDigits = settings.PasswordRequireDigits;
                existingSettings.PasswordRequireSpecialChars = settings.PasswordRequireSpecialChars;
                existingSettings.PasswordMaxAge = settings.PasswordMaxAge;
                existingSettings.PasswordPreventReuse = settings.PasswordPreventReuse;

                // CAPTCHA Settings
                existingSettings.CaptchaEnabled = settings.CaptchaEnabled;
                existingSettings.CaptchaProvider = settings.CaptchaProvider;
                existingSettings.RecaptchaSiteKey = settings.RecaptchaSiteKey;
                existingSettings.RecaptchaSecretKey = settings.RecaptchaSecretKey;
                existingSettings.HCaptchaSiteKey = settings.HCaptchaSiteKey;
                existingSettings.HCaptchaSecretKey = settings.HCaptchaSecretKey;

                // Session and Token Settings
                existingSettings.SessionTimeoutMinutes = settings.SessionTimeoutMinutes;
                existingSettings.JwtTokenLifetimeMinutes = settings.JwtTokenLifetimeMinutes;
                existingSettings.PreventConcurrentLogin = settings.PreventConcurrentLogin;

                // Lockout Settings
                existingSettings.MaxFailedLoginAttempts = settings.MaxFailedLoginAttempts;
                existingSettings.AccountLockoutMinutes = settings.AccountLockoutMinutes;

                // Rate Limiting Settings
                existingSettings.RateLimitLoginMaxAttempts = settings.RateLimitLoginMaxAttempts;
                existingSettings.RateLimitLoginWindowMinutes = settings.RateLimitLoginWindowMinutes;
                existingSettings.RateLimitLoginBlockDurationMinutes = settings.RateLimitLoginBlockDurationMinutes;

                // Legal URLs
                existingSettings.TermsOfServiceUrl = settings.TermsOfServiceUrl;
                existingSettings.PrivacyPolicyUrl = settings.PrivacyPolicyUrl;

                existingSettings.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Repository<Security>().UpdateAsync(existingSettings);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Updated security settings for tenant {TenantId}", tenantId);
                return existingSettings;
            }
            else
            {
                // Create new settings
                settings.Id = Guid.NewGuid();
                settings.TenantId = tenantId;
                settings.CreatedAt = DateTime.UtcNow;
                settings.CreatedBy = _currentUserService.UserName;

                var createdSettings = await _unitOfWork.Repository<Security>().AddAsync(settings);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Created new security settings for tenant {TenantId}", tenantId);
                return createdSettings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security settings");
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
                    TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserName
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

    #region Field Label Configuration

    private const string FieldLabelKeyPrefix = "FieldLabel";

    /// <summary>
    /// Gets all field labels for a specific module (e.g., "InventoryItem", "BusinessPartner")
    /// </summary>
    public async Task<Dictionary<string, string>> GetFieldLabelsAsync(string module)
    {
        try
        {
            var prefix = $"{FieldLabelKeyPrefix}:{module}:";
            var allSettings = await _unitOfWork.Repository<SystemSettings>().GetAllAsync();
            var fieldLabels = allSettings
                .Where(s => s.Key.StartsWith(prefix))
                .ToDictionary(
                    s => s.Key.Substring(prefix.Length),
                    s => s.Value
                );

            // Return default labels if none are configured
            if (fieldLabels.Count == 0 && module == "InventoryItem")
            {
                return GetDefaultInventoryItemLabels();
            }

            return fieldLabels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving field labels for module {Module}", module);
            throw;
        }
    }

    /// <summary>
    /// Sets multiple field labels for a module at once
    /// </summary>
    public async Task<Dictionary<string, string>> SetFieldLabelsAsync(string module, Dictionary<string, string> labels)
    {
        try
        {
            foreach (var label in labels)
            {
                await SetFieldLabelAsync(module, label.Key, label.Value);
            }

            _logger.LogInformation("Updated {Count} field labels for module {Module}", labels.Count, module);
            return await GetFieldLabelsAsync(module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting field labels for module {Module}", module);
            throw;
        }
    }

    /// <summary>
    /// Gets a single field label
    /// </summary>
    public async Task<string> GetFieldLabelAsync(string module, string fieldName)
    {
        try
        {
            var key = $"{FieldLabelKeyPrefix}:{module}:{fieldName}";
            var setting = await GetSystemSettingAsync(key);
            
            if (setting != null)
            {
                return setting.Value;
            }

            // Return default label if not configured
            return GetDefaultLabel(module, fieldName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving field label {Module}:{FieldName}", module, fieldName);
            throw;
        }
    }

    /// <summary>
    /// Sets a single field label
    /// </summary>
    public async Task SetFieldLabelAsync(string module, string fieldName, string label)
    {
        try
        {
            var key = $"{FieldLabelKeyPrefix}:{module}:{fieldName}";
            var description = $"Custom label for {module}.{fieldName} field";
            await SetSystemSettingAsync(key, label, description);
            _logger.LogInformation("Set field label {Module}:{FieldName} = {Label}", module, fieldName, label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting field label {Module}:{FieldName}", module, fieldName);
            throw;
        }
    }

    /// <summary>
    /// Gets default labels for inventory item fields
    /// </summary>
    private Dictionary<string, string> GetDefaultInventoryItemLabels()
    {
        return new Dictionary<string, string>
        {
            { "Brand", "Brand" },
            { "Manufacturer", "Manufacturer" },
            { "Style", "Style" },
            { "Feature", "Feature" }
        };
    }

    /// <summary>
    /// Gets the default label for a field if no custom label is configured
    /// </summary>
    private string GetDefaultLabel(string module, string fieldName)
    {
        if (module == "InventoryItem")
        {
            var defaults = GetDefaultInventoryItemLabels();
            return defaults.TryGetValue(fieldName, out var label) ? label : fieldName;
        }

        // Default to the field name itself
        return fieldName;
    }

    #endregion
}
