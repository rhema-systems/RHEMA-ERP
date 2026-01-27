# Email Service Configuration & Debugging Guide

## Email Service Flow

The forgot password email goes through this flow:

```
AuthController.ForgotPassword()
    ↓
CoreEmailServiceAdapter.SendEmailAsync(EmailDto)
    ↓
ProductionEmailService.SendEmailAsync(to, subject, body, isHtml)
    ↓
SettingsService.GetEmailSettingsAsync()
    ↓
Database: EmailSettings table (decrypts SMTP password via ICryptoService)
    ↓
SmtpClient → smtp.office365.com:587 (with TLS)
    ↓
User's email
```

---

## Current EmailSettings Configuration

Your database has SMTP configured:

| Field | Value |
|-------|-------|
| **SmtpHost** | smtp.office365.com |
| **SmtpPort** | 587 |
| **SmtpUsername** | michael@rhema-systems.com.gh |
| **SmtpPassword** | *(encrypted)* |
| **UseTLS** | ✅ Enabled (1) |
| **FromAddress** | michael@rhema-systems.com.gh |
| **FromName** | ERP Team |
| **TenantId** | 00000000-0000-0000-0000-000000000001 |

---

## Email Sending Process

### Step 1: Generate Reset Token
```csharp
// In AuthController.ForgotPassword()
var resetToken = await _passwordResetService.GeneratePasswordResetTokenAsync(
    user.Id, ipAddress, userAgent);
```

### Step 2: Prepare Email
```csharp
var emailDto = new EmailDto
{
    To = user.Email,
    Subject = "Password Reset Request",
    Body = GeneratePasswordResetEmailBody(user.FirstName, resetUrl),
    IsHtml = true
};
```

### Step 3: Send via Adapter
```csharp
// CoreEmailServiceAdapter acts as a bridge
await _emailService.SendEmailAsync(emailDto);
```

### Step 4: Actual Email Sending
```csharp
// ProductionEmailService.SendEmailAsync()
var emailSettings = await _settingsService.GetEmailSettingsAsync();

if (emailSettings == null)
{
    _logger.LogWarning("No email settings configured");
    return false; // In production
}

// Decrypt password
var decryptedPassword = _cryptoService.Decrypt(emailSettings.SmtpPassword);

// Configure SMTP
var smtpClient = new SmtpClient(emailSettings.SmtpHost, emailSettings.SmtpPort);
smtpClient.Credentials = new NetworkCredential(emailSettings.SmtpUsername, decryptedPassword);
smtpClient.EnableSsl = emailSettings.UseTLS; // true for Office365

// Send
await smtpClient.SendMailAsync(mailMessage);
```

---

## Debugging Checklist

### ✅ Check 1: Application Logs
The ForgotPassword endpoint wraps email sending in try-catch that **logs all errors** but returns success:

```csharp
try
{
    await _emailService.SendEmailAsync(emailDto);
    _logger.LogInformation("Password reset email sent to {Email}", user.Email);
}
catch (Exception emailEx)
{
    _logger.LogError(emailEx, "Failed to send password reset email to {Email}", user.Email);
    // Don't fail the request - token was generated, email might be resent
}
```

**To debug:**
1. Check application logs for error messages
2. Look for warnings from `SettingsService` about null settings
3. Look for SMTP-specific errors in logs

### ✅ Check 2: EmailSettings Loaded Correctly
In `SettingsService.GetEmailSettingsAsync()`:
- Retrieves first record from EmailSettings table ✅
- Decrypts the password using `ICryptoService.Decrypt()`
- Returns decrypted settings

**Potential issues:**
- If decryption fails, falls back to plain text (backwards compatibility)
- If settings table is empty → returns null → email NOT sent (logged as warning)

### ✅ Check 3: Encryption Key
The password decryption uses:
```csharp
private string _encryptionKey = configuration["Security:EncryptionKey"];
// From user-secrets: J0R5Oy/zfiSSITSkaJmpj4DZxCCKHySLUJrZ89Dh4Eg=
```

**If encryption key changed:**
- Old encrypted passwords can't be decrypted
- Falls back to treating as plain text
- Email sending may fail

### ✅ Check 4: Office365 SMTP Requirements
Office365 requires:
- Host: `smtp.office365.com` ✅
- Port: `587` ✅
- TLS: `Enabled` ✅
- Username: Email address ✅
- Password: **May need to be an App Password** ⚠️

**Issue:** If using Office365, the user account needs either:
1. Basic Auth enabled on the mailbox, OR
2. An **App Password** generated in Office365

---

## Testing Steps

### Test 1: Verify EmailSettings in Database
```sql
SELECT SmtpHost, SmtpPort, SmtpUsername, UseTLS, FromAddress, FromName 
FROM EmailSettings 
WHERE TenantId = '00000000-0000-0000-0000-000000000001';
```

✅ **Result:** Should show your Office365 SMTP config

### Test 2: Verify Encryption Key
```
User-Secrets: Security:EncryptionKey = J0R5Oy/zfiSSITSkaJmpj4DZxCCKHySLUJrZ89Dh4Eg=
```

This key must be present in production/development user-secrets.

### Test 3: Check Application Logs
When you call forgot-password endpoint:

**Success scenario logs:**
```
[INF] Password reset requested for email: user@example.com
[INF] Password reset token generated for user {UserId}...
[INF] Successfully sent email to user@example.com...
```

**Failure scenario logs:**
```
[WRN] No email settings configured in database...
[ERR] SMTP error occurred while sending email: {SmtpException}
[ERR] Failed to decrypt data: {CryptoException}
```

### Test 4: Settings Controller Email Test
There's likely a Settings API endpoint to test SMTP:

```bash
POST /api/settings/email/test
{
  "testEmail": "your-test-email@example.com"
}
```

This endpoint should test the SMTP connection.

---

## Common Issues & Solutions

### Issue 1: "No email settings configured"
**Cause:** EmailSettings table is empty or null

**Solution:**
1. Go to Settings page in admin panel
2. Configure SMTP settings
3. Save

### Issue 2: SMTP Authentication Failed
**Cause:** Wrong password or Office365 requires App Password

**Solution (Office365):**
1. Go to https://account.microsoft.com/security
2. Generate an **App Password** (not your regular password)
3. Use that App Password in EmailSettings
4. Restart application to reload encryption key

### Issue 3: Connection Timeout
**Cause:** Firewall blocking SMTP port 587

**Solution:**
1. Check firewall rules allow outbound 587
2. Try port 25 or 465 if 587 blocked (with appropriate TLS settings)
3. Check network connectivity to smtp.office365.com

### Issue 4: Encryption/Decryption Error
**Cause:** Encryption key changed or corrupted data

**Solution:**
1. Verify `Security:EncryptionKey` in user-secrets matches
2. Re-enter SMTP password in settings (forces re-encryption)
3. Check database for encrypted password format (should be base64)

---

## Manual Email Sending Test

To verify the service outside of forgot-password:

```csharp
// In any controller/service with IEmailService injection
var emailDto = new EmailDto
{
    To = "your-email@example.com",
    Subject = "Test Email",
    Body = "This is a test email from ERP System",
    IsHtml = false
};

var result = await _emailService.SendEmailAsync(emailDto);

if (result)
    _logger.LogInformation("Test email sent successfully");
else
    _logger.LogError("Test email failed");
```

---

## Development Environment Notes

In **Development** mode, if EmailSettings is null:
- Email is logged to output instead of actually sent
- You'll see: `=== EMAIL (No SMTP Config) ===`
- Then email details logged to console

In **Production** mode, if EmailSettings is null:
- Email sending simply fails and returns false
- Error is logged but request continues
- User sees success message (security best practice)

---

## Files Involved

| File | Purpose |
|------|---------|
| `AuthController.cs` | ForgotPassword endpoint, handles email sending try-catch |
| `PasswordResetService.cs` | Generates reset tokens |
| `CoreEmailServiceAdapter.cs` | Bridges Core IEmailService to ProductionEmailService |
| `ProductionEmailService.cs` | Actual SMTP email sender |
| `SettingsService.cs` | Retrieves & decrypts EmailSettings from database |
| `CryptoService.cs` | Encrypts/decrypts SMTP password |
| Database: `EmailSettings` | Stores SMTP configuration |

---

## Next Steps

1. **Check application logs** for email sending errors
2. **Test with Settings endpoint** if available
3. **Verify Office365 account** has proper authentication setup
4. **Check firewall** allows outbound SMTP traffic
5. **Re-enter SMTP password** in settings to force re-encryption

---

**Status:** SMTP Configuration Present ✅  
**Last Verified:** 2025-11-08  
**SMTP Host:** smtp.office365.com:587 with TLS
