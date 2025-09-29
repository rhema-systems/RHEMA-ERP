-- Test script to insert security settings for debugging
-- This will help us verify if the issue is with missing data or frontend handling

USE RhemaERPTDC;

-- Check if any security settings exist
SELECT COUNT(*) as SecuritySettingsCount FROM Securities;

-- If no records exist, insert a test record with CAPTCHA enabled
IF NOT EXISTS (SELECT 1 FROM Securities WHERE TenantId = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO Securities (
        Id,
        TenantId,
        -- Password Policy
        PasswordMinLength,
        PasswordRequireUppercase,
        PasswordRequireLowercase,
        PasswordRequireDigits,
        PasswordRequireSpecialChars,
        PasswordMaxAge,
        PasswordPreventReuse,
        -- CAPTCHA Settings (enabled for testing)
        CaptchaEnabled,
        CaptchaProvider,
        RecaptchaSiteKey,
        RecaptchaSecretKey,
        HCaptchaSiteKey,
        HCaptchaSecretKey,
        -- Session & Token Settings
        SessionTimeoutMinutes,
        JwtTokenLifetimeMinutes,
        PreventConcurrentLogin,
        -- Lockout Settings
        MaxFailedLoginAttempts,
        AccountLockoutMinutes,
        -- Rate Limiting Settings
        RateLimitLoginMaxAttempts,
        RateLimitLoginWindowMinutes,
        RateLimitLoginBlockDurationMinutes,
        RateLimitRegisterMaxAttempts,
        RateLimitRegisterWindowMinutes,
        RateLimitRegisterBlockDurationMinutes,
        RateLimitForgotPasswordMaxAttempts,
        RateLimitForgotPasswordWindowMinutes,
        RateLimitForgotPasswordBlockDurationMinutes,
        -- Audit fields
        CreatedAt,
        CreatedBy
    )
    VALUES (
        NEWID(),
        '00000000-0000-0000-0000-000000000001', -- Default tenant ID
        -- Password Policy
        8,      -- PasswordMinLength
        1,      -- PasswordRequireUppercase
        1,      -- PasswordRequireLowercase
        1,      -- PasswordRequireDigits
        1,      -- PasswordRequireSpecialChars
        90,     -- PasswordMaxAge
        5,      -- PasswordPreventReuse
        -- CAPTCHA Settings (enabled for testing)
        1,      -- CaptchaEnabled = true
        'recaptcha', -- CaptchaProvider
        'test-site-key-6Lc123456789', -- RecaptchaSiteKey
        'test-secret-key-6Lc987654321', -- RecaptchaSecretKey
        'test-hcaptcha-site-key-10000000-ffff', -- HCaptchaSiteKey
        'test-hcaptcha-secret-key-0x0000000', -- HCaptchaSecretKey
        -- Session & Token Settings
        30,     -- SessionTimeoutMinutes
        60,     -- JwtTokenLifetimeMinutes
        0,      -- PreventConcurrentLogin (0 = Disabled)
        -- Lockout Settings
        5,      -- MaxFailedLoginAttempts
        30,     -- AccountLockoutMinutes
        -- Rate Limiting Settings
        5,      -- RateLimitLoginMaxAttempts
        15,     -- RateLimitLoginWindowMinutes
        30,     -- RateLimitLoginBlockDurationMinutes
        3,      -- RateLimitRegisterMaxAttempts
        60,     -- RateLimitRegisterWindowMinutes
        60,     -- RateLimitRegisterBlockDurationMinutes
        3,      -- RateLimitForgotPasswordMaxAttempts
        60,     -- RateLimitForgotPasswordWindowMinutes
        120,    -- RateLimitForgotPasswordBlockDurationMinutes
        -- Audit fields
        GETUTCDATE(),
        'test-script'
    );
    
    PRINT 'Test security settings record created with CAPTCHA enabled.';
END
ELSE
BEGIN
    PRINT 'Security settings record already exists.';
    -- Show current CAPTCHA settings
    SELECT 
        CaptchaEnabled,
        CaptchaProvider,
        RecaptchaSiteKey,
        RecaptchaSecretKey,
        HCaptchaSiteKey,
        HCaptchaSecretKey
    FROM Securities 
    WHERE TenantId = '00000000-0000-0000-0000-000000000001';
END

-- Display all security settings for verification
SELECT * FROM Securities WHERE TenantId = '00000000-0000-0000-0000-000000000001';