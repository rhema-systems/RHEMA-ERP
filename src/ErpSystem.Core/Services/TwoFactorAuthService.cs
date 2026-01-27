using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Entities;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;

namespace ErpSystem.Core.Services;

public interface ITwoFactorAuthService
{
    Task<TwoFactorSetupResult> SetupTwoFactorAsync(ApplicationUser user, string issuer = "ERP System");
    Task<bool> ValidateTotpAsync(ApplicationUser user, string totpCode);
    Task<List<string>> GenerateRecoveryCodesAsync(ApplicationUser user, int count = 8);
    Task<bool> ValidateRecoveryCodeAsync(ApplicationUser user, string recoveryCode);
    Task<bool> IsRecoveryCodeValidAsync(ApplicationUser user, string recoveryCode);
    string GenerateQrCodeDataUri(string secretKey, string userEmail, string issuer = "ERP System");
}

public class TwoFactorSetupResult
{
    public string SecretKey { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
    public List<string> RecoveryCodes { get; set; } = new();
    public string ManualEntryKey { get; set; } = string.Empty;
}

public class TwoFactorAuthService : ITwoFactorAuthService
{
    private readonly ILogger<TwoFactorAuthService> _logger;
    private readonly IUserService _userService;

    public TwoFactorAuthService(ILogger<TwoFactorAuthService> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    public async Task<TwoFactorSetupResult> SetupTwoFactorAsync(ApplicationUser user, string issuer = "ERP System")
    {
        try
        {
            // Generate a new secret key
            var secretKey = GenerateSecretKey();

            // Store the secret key for the user (in a real implementation, this would be stored securely)
            // For now, we'll use the AuthenticatorKey field if available
            // In a production system, you'd want to encrypt this
            user.AuthenticatorKey = secretKey;

            // Generate QR code
            var qrCodeDataUri = GenerateQrCodeDataUri(secretKey, user.Email ?? user.UserName ?? "user", issuer);

            // Generate recovery codes
            var recoveryCodes = await GenerateRecoveryCodesAsync(user);

            // Create manual entry key (formatted for easier entry)
            var manualEntryKey = FormatSecretKeyForManualEntry(secretKey);

            _logger.LogInformation("Two-factor authentication setup initiated for user {UserId}", user.Id);

            return new TwoFactorSetupResult
            {
                SecretKey = secretKey,
                QrCodeDataUri = qrCodeDataUri,
                RecoveryCodes = recoveryCodes,
                ManualEntryKey = manualEntryKey
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up two-factor authentication for user {UserId}", user.Id);
            throw;
        }
    }

    public Task<bool> ValidateTotpAsync(ApplicationUser user, string totpCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(user.AuthenticatorKey))
            {
                _logger.LogWarning("No authenticator key found for user {UserId}", user.Id);
                return Task.FromResult(false);
            }

            if (string.IsNullOrWhiteSpace(totpCode) || totpCode.Length != 6)
            {
                _logger.LogWarning("Invalid TOTP code format for user {UserId}: code length = {Length}, code = '{Code}'", user.Id, totpCode?.Length ?? 0, totpCode);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Validating TOTP for user {UserId}, code = '{Code}', secret key = '{SecretKey}'", user.Id, totpCode, user.AuthenticatorKey);

            var secretKeyBytes = Base32Encoding.ToBytes(user.AuthenticatorKey);
            var totp = new Totp(secretKeyBytes);

            // Get current timestamp and validate with tolerance window
            var now = DateTime.UtcNow;
            _logger.LogInformation("Current UTC time: {CurrentTime}", now.ToString("yyyy-MM-dd HH:mm:ss"));

            // Try current window and adjacent windows (±90 seconds total)
            var windows = new[] { -2, -1, 0, 1, 2 }; // Expanded window for debugging

            foreach (var windowOffset in windows)
            {
                var testTime = now.AddSeconds(windowOffset * 30);
                var expectedCode = totp.ComputeTotp(testTime);

                _logger.LogInformation("Window {WindowOffset}: Time = {TestTime}, Expected code = '{ExpectedCode}'",
                    windowOffset, testTime.ToString("yyyy-MM-dd HH:mm:ss"), expectedCode);

                if (expectedCode == totpCode)
                {
                    _logger.LogInformation("TOTP validation successful for user {UserId} with window offset {WindowOffset}", user.Id, windowOffset);
                    return Task.FromResult(true);
                }
            }

            _logger.LogWarning("TOTP validation failed for user {UserId}. Provided code: '{ProvidedCode}', Expected codes: {ExpectedCodes}",
                user.Id,
                totpCode,
                string.Join(", ", windows.Select(w => $"{w}:{totp.ComputeTotp(now.AddSeconds(w * 30))}").ToArray()));
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TOTP for user {UserId}: {ErrorMessage}", user.Id, ex.Message);
            return Task.FromResult(false);
        }
    }

    public Task<List<string>> GenerateRecoveryCodesAsync(ApplicationUser user, int count = 8)
    {
        try
        {
            var recoveryCodes = new List<string>();

            for (int i = 0; i < count; i++)
            {
                var code = GenerateRecoveryCode();
                recoveryCodes.Add(code);
            }

            // In a real implementation, you'd hash these codes and store them in the database
            // For now, we'll just return them
            _logger.LogInformation("Generated {Count} recovery codes for user {UserId}", count, user.Id);

            return Task.FromResult(recoveryCodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recovery codes for user {UserId}", user.Id);
            throw;
        }
    }

    public Task<bool> ValidateRecoveryCodeAsync(ApplicationUser user, string recoveryCode)
    {
        try
        {
            // In a real implementation, you'd check the hashed recovery codes stored in the database
            // and mark the used code as consumed
            _logger.LogInformation("Recovery code validation for user {UserId}", user.Id);

            // For now, return false as we don't have recovery code storage implemented
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating recovery code for user {UserId}", user.Id);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> IsRecoveryCodeValidAsync(ApplicationUser user, string recoveryCode)
    {
        return await ValidateRecoveryCodeAsync(user, recoveryCode);
    }

    public string GenerateQrCodeDataUri(string secretKey, string userEmail, string issuer = "ERP System")
    {
        try
        {
            // Create the TOTP URI according to Google Authenticator format
            var totpUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(userEmail)}?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";

            // Generate QR Code
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            var qrCodeBytes = qrCode.GetGraphic(20);
            var qrCodeImageAsBase64 = Convert.ToBase64String(qrCodeBytes);
            var dataUri = $"data:image/png;base64,{qrCodeImageAsBase64}";

            return dataUri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating QR code data URI");
            throw;
        }
    }

    private static string GenerateSecretKey()
    {
        // Generate a 32-byte (160-bit) secret key
        var buffer = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }

        return Base32Encoding.ToString(buffer);
    }

    private static string GenerateRecoveryCode()
    {
        // Generate an 8-character recovery code using alphanumeric characters
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var buffer = new byte[8];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }

        var result = new StringBuilder(8);
        for (int i = 0; i < 8; i++)
        {
            result.Append(chars[buffer[i] % chars.Length]);
        }

        return result.ToString();
    }

    private static string FormatSecretKeyForManualEntry(string secretKey)
    {
        // Format the secret key in groups of 4 characters for easier manual entry
        var result = new StringBuilder();
        for (int i = 0; i < secretKey.Length; i += 4)
        {
            if (i > 0)
            {
                result.Append(' ');
            }

            result.Append(secretKey.AsSpan(i, Math.Min(4, secretKey.Length - i)));
        }
        return result.ToString();
    }
}
