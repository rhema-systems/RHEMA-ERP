import { apiService } from './api.service';

export interface SecuritySettings {
  // Password Policy
  passwordMinLength: number;
  passwordRequireUppercase: boolean;
  passwordRequireLowercase: boolean;
  passwordRequireDigits: boolean;
  passwordRequireSpecialChars: boolean;
  passwordMaxAge: number | null;
  passwordPreventReuse: number | null;

  // Session & Token
  sessionTimeoutMinutes: number;
  jwtTokenLifetimeMinutes: number;
  preventConcurrentLogin: 'Disabled' | 'LogoutFromAllDevices' | 'PreventSubsequentLogins';

  // Lockout & Rate Limiting
  maxFailedLoginAttempts: number;
  accountLockoutMinutes: number;
  rateLimitLoginMaxAttempts: number;
  rateLimitLoginWindowMinutes: number;
  rateLimitLoginBlockDurationMinutes: number;

  // CAPTCHA
  captchaEnabled: boolean;
  captchaProvider: 'recaptcha' | 'hcaptcha';
  recaptchaSiteKey: string | null;
  recaptchaSecretKey: string | null;
  hCaptchaSiteKey: string | null;
  hCaptchaSecretKey: string | null;

  // Legal URLs
  termsOfServiceUrl: string | null;
  privacyPolicyUrl: string | null;
}

// Admin API DTO interface to match backend SecuritySettingsDto (PascalCase)
interface AdminSecuritySettingsApiDto {
  // Password Policy
  PasswordMinLength: number;
  PasswordRequireUppercase: boolean;
  PasswordRequireLowercase: boolean;
  PasswordRequireDigits: boolean;
  PasswordRequireSpecialChars: boolean;
  PasswordMaxAge: number | null;
  PasswordPreventReuse: number | null;

  // Session & Token
  SessionTimeoutMinutes: number;
  JwtTokenLifetimeMinutes: number;
  PreventConcurrentLogin: string; // 'Disabled' | 'LogoutFromAllDevices' | 'PreventSubsequentLogins'

  // Lockout & Rate Limiting
  MaxFailedLoginAttempts: number;
  AccountLockoutMinutes: number;
  RateLimitLoginMaxAttempts: number;
  RateLimitLoginWindowMinutes: number;
  RateLimitLoginBlockDurationMinutes: number;

  // CAPTCHA
  CaptchaEnabled: boolean;
  CaptchaProvider: string; // 'recaptcha' | 'hcaptcha'
  RecaptchaSiteKey: string | null;
  RecaptchaSecretKey: string | null;
  HCaptchaSiteKey: string | null;
  HCaptchaSecretKey: string | null;

  // Legal URLs
  TermsOfServiceUrl: string | null;
  PrivacyPolicyUrl: string | null;
}

// Public API DTO interface for auth endpoint (camelCase)
interface PublicSecuritySettingsApiDto {
  // Password Policy (limited fields for public use)
  passwordMinLength: number;
  passwordRequireUppercase: boolean;
  passwordRequireLowercase: boolean;
  passwordRequireDigits: boolean;
  passwordRequireSpecialChars: boolean;
  
  // CAPTCHA (for registration)
  captchaEnabled: boolean;
  captchaProvider: string; // 'recaptcha' | 'hcaptcha'
  recaptchaSiteKey: string | null;
  hCaptchaSiteKey: string | null;
  // Note: Secret keys are not exposed in public endpoint
  
  // Legal URLs (for registration)
  termsOfServiceUrl: string | null;
  privacyPolicyUrl: string | null;
}

class SettingsService {
  async getSecuritySettings(): Promise<SecuritySettings> {
    try {
      // First try authenticated admin endpoint for full settings (admin pages)
      console.log('🔄 Fetching admin security settings from /settings/security...');
      const response = await apiService.request<AdminSecuritySettingsApiDto>('/settings/security', {
        method: 'GET',
      });
      
      console.log('✅ Admin endpoint response:', response);

      // Convert admin API response to frontend interface
      // Handle both PascalCase and camelCase responses from backend
      const data = response as any; // Type as any to handle both cases
      const result = {
        passwordMinLength: data.PasswordMinLength ?? data.passwordMinLength ?? 8,
        passwordRequireUppercase: Boolean(data.PasswordRequireUppercase ?? data.passwordRequireUppercase ?? true),
        passwordRequireLowercase: Boolean(data.PasswordRequireLowercase ?? data.passwordRequireLowercase ?? true),
        passwordRequireDigits: Boolean(data.PasswordRequireDigits ?? data.passwordRequireDigits ?? true),
        passwordRequireSpecialChars: Boolean(data.PasswordRequireSpecialChars ?? data.passwordRequireSpecialChars ?? true),
        passwordMaxAge: data.PasswordMaxAge ?? data.passwordMaxAge,
        passwordPreventReuse: data.PasswordPreventReuse ?? data.passwordPreventReuse,
        sessionTimeoutMinutes: data.SessionTimeoutMinutes ?? data.sessionTimeoutMinutes ?? 30,
        jwtTokenLifetimeMinutes: data.JwtTokenLifetimeMinutes ?? data.jwtTokenLifetimeMinutes ?? 60,
        preventConcurrentLogin: (data.PreventConcurrentLogin ?? data.preventConcurrentLogin ?? 'Disabled') as 'Disabled' | 'LogoutFromAllDevices' | 'PreventSubsequentLogins',
        maxFailedLoginAttempts: data.MaxFailedLoginAttempts ?? data.maxFailedLoginAttempts ?? 5,
        accountLockoutMinutes: data.AccountLockoutMinutes ?? data.accountLockoutMinutes ?? 30,
        rateLimitLoginMaxAttempts: data.RateLimitLoginMaxAttempts ?? data.rateLimitLoginMaxAttempts ?? 5,
        rateLimitLoginWindowMinutes: data.RateLimitLoginWindowMinutes ?? data.rateLimitLoginWindowMinutes ?? 15,
        rateLimitLoginBlockDurationMinutes: data.RateLimitLoginBlockDurationMinutes ?? data.rateLimitLoginBlockDurationMinutes ?? 30,
        captchaEnabled: Boolean(data.CaptchaEnabled ?? data.captchaEnabled ?? false),
        captchaProvider: (data.CaptchaProvider ?? data.captchaProvider ?? 'recaptcha') as 'recaptcha' | 'hcaptcha',
        recaptchaSiteKey: data.RecaptchaSiteKey ?? data.recaptchaSiteKey ?? null,
        recaptchaSecretKey: data.RecaptchaSecretKey ?? data.recaptchaSecretKey ?? null,
        hCaptchaSiteKey: data.HCaptchaSiteKey ?? data.hCaptchaSiteKey ?? null,
        hCaptchaSecretKey: data.HCaptchaSecretKey ?? data.hCaptchaSecretKey ?? null,
        termsOfServiceUrl: data.TermsOfServiceUrl ?? data.termsOfServiceUrl ?? null,
        privacyPolicyUrl: data.PrivacyPolicyUrl ?? data.privacyPolicyUrl ?? null,
      };
      
      console.log('\u2705 Converted settings for frontend:', result);
      return result;
    } catch (adminError) {
      console.warn('Failed to fetch admin security settings, trying public endpoint:', adminError);
      
      try {
        // Fallback to public endpoint (for registration page)
        const publicResponse = await apiService.publicRequest<PublicSecuritySettingsApiDto>('/auth/security-settings', {
          method: 'GET',
        });

        // Convert public API response to frontend interface (with defaults for missing fields)
        return {
          passwordMinLength: publicResponse.passwordMinLength,
          passwordRequireUppercase: publicResponse.passwordRequireUppercase,
          passwordRequireLowercase: publicResponse.passwordRequireLowercase,
          passwordRequireDigits: publicResponse.passwordRequireDigits,
          passwordRequireSpecialChars: publicResponse.passwordRequireSpecialChars,
          passwordMaxAge: null, // Not available in public endpoint
          passwordPreventReuse: null, // Not available in public endpoint
          sessionTimeoutMinutes: 30, // Default values for admin-only fields
          jwtTokenLifetimeMinutes: 60,
          preventConcurrentLogin: 'Disabled',
          maxFailedLoginAttempts: 5,
          accountLockoutMinutes: 30,
          rateLimitLoginMaxAttempts: 5,
          rateLimitLoginWindowMinutes: 15,
          rateLimitLoginBlockDurationMinutes: 30,
          captchaEnabled: publicResponse.captchaEnabled,
          captchaProvider: publicResponse.captchaProvider as 'recaptcha' | 'hcaptcha',
          recaptchaSiteKey: publicResponse.recaptchaSiteKey,
          recaptchaSecretKey: null, // Not exposed in public endpoint
          hCaptchaSiteKey: publicResponse.hCaptchaSiteKey,
          hCaptchaSecretKey: null, // Not exposed in public endpoint
          termsOfServiceUrl: publicResponse.termsOfServiceUrl,
          privacyPolicyUrl: publicResponse.privacyPolicyUrl,
        };
      } catch (publicError) {
        console.warn('Failed to fetch public security settings, using defaults:', publicError);
        // Return default security settings for public access (registration page)
        return {
          passwordMinLength: 8,
          passwordRequireUppercase: true,
          passwordRequireLowercase: true,
          passwordRequireDigits: true,
          passwordRequireSpecialChars: true,
          passwordMaxAge: 90,
          passwordPreventReuse: 5,
          sessionTimeoutMinutes: 30,
          jwtTokenLifetimeMinutes: 60,
          preventConcurrentLogin: 'Disabled',
          maxFailedLoginAttempts: 5,
          accountLockoutMinutes: 30,
          rateLimitLoginMaxAttempts: 5,
          rateLimitLoginWindowMinutes: 15,
          rateLimitLoginBlockDurationMinutes: 30,
          captchaEnabled: false,
          captchaProvider: 'recaptcha',
          recaptchaSiteKey: null,
          recaptchaSecretKey: null,
          hCaptchaSiteKey: null,
          hCaptchaSecretKey: null,
          termsOfServiceUrl: null,
          privacyPolicyUrl: null,
        };
      }
    }
  }

  async updateSecuritySettings(settings: SecuritySettings): Promise<SecuritySettings> {
    console.log('💾 Updating security settings - input data:', settings);
    
    // Convert frontend interface to admin API DTO (PascalCase)
    // Handle empty strings vs null values properly
    const requestDto: AdminSecuritySettingsApiDto = {
      PasswordMinLength: settings.passwordMinLength,
      PasswordRequireUppercase: settings.passwordRequireUppercase,
      PasswordRequireLowercase: settings.passwordRequireLowercase,
      PasswordRequireDigits: settings.passwordRequireDigits,
      PasswordRequireSpecialChars: settings.passwordRequireSpecialChars,
      PasswordMaxAge: settings.passwordMaxAge,
      PasswordPreventReuse: settings.passwordPreventReuse,
      SessionTimeoutMinutes: settings.sessionTimeoutMinutes,
      JwtTokenLifetimeMinutes: settings.jwtTokenLifetimeMinutes,
      PreventConcurrentLogin: settings.preventConcurrentLogin,
      MaxFailedLoginAttempts: settings.maxFailedLoginAttempts,
      AccountLockoutMinutes: settings.accountLockoutMinutes,
      RateLimitLoginMaxAttempts: settings.rateLimitLoginMaxAttempts,
      RateLimitLoginWindowMinutes: settings.rateLimitLoginWindowMinutes,
      RateLimitLoginBlockDurationMinutes: settings.rateLimitLoginBlockDurationMinutes,
      CaptchaEnabled: settings.captchaEnabled,
      CaptchaProvider: settings.captchaProvider,
      RecaptchaSiteKey: settings.recaptchaSiteKey || null,
      RecaptchaSecretKey: settings.recaptchaSecretKey || null,
      HCaptchaSiteKey: settings.hCaptchaSiteKey || null,
      HCaptchaSecretKey: settings.hCaptchaSecretKey || null,
      TermsOfServiceUrl: settings.termsOfServiceUrl || null,
      PrivacyPolicyUrl: settings.privacyPolicyUrl || null,
    };
    
    console.log('🔄 Sending to API (PascalCase):', requestDto);

    const response = await apiService.request<AdminSecuritySettingsApiDto>('/settings/security', {
      method: 'PUT',
      body: JSON.stringify(requestDto),
    });
    
    console.log('✅ API response from PUT:', response);

    // Convert admin API response back to frontend interface with proper null handling
    const result = {
      passwordMinLength: response.PasswordMinLength ?? 8,
      passwordRequireUppercase: Boolean(response.PasswordRequireUppercase ?? true),
      passwordRequireLowercase: Boolean(response.PasswordRequireLowercase ?? true),
      passwordRequireDigits: Boolean(response.PasswordRequireDigits ?? true),
      passwordRequireSpecialChars: Boolean(response.PasswordRequireSpecialChars ?? true),
      passwordMaxAge: response.PasswordMaxAge,
      passwordPreventReuse: response.PasswordPreventReuse,
      sessionTimeoutMinutes: response.SessionTimeoutMinutes ?? 30,
      jwtTokenLifetimeMinutes: response.JwtTokenLifetimeMinutes ?? 60,
      preventConcurrentLogin: (response.PreventConcurrentLogin ?? 'Disabled') as 'Disabled' | 'LogoutFromAllDevices' | 'PreventSubsequentLogins',
      maxFailedLoginAttempts: response.MaxFailedLoginAttempts ?? 5,
      accountLockoutMinutes: response.AccountLockoutMinutes ?? 30,
      rateLimitLoginMaxAttempts: response.RateLimitLoginMaxAttempts ?? 5,
      rateLimitLoginWindowMinutes: response.RateLimitLoginWindowMinutes ?? 15,
      rateLimitLoginBlockDurationMinutes: response.RateLimitLoginBlockDurationMinutes ?? 30,
      captchaEnabled: Boolean(response.CaptchaEnabled ?? false),
      captchaProvider: (response.CaptchaProvider ?? 'recaptcha') as 'recaptcha' | 'hcaptcha',
      recaptchaSiteKey: response.RecaptchaSiteKey ?? null,
      recaptchaSecretKey: response.RecaptchaSecretKey ?? null,
      hCaptchaSiteKey: response.HCaptchaSiteKey ?? null,
      hCaptchaSecretKey: response.HCaptchaSecretKey ?? null,
      termsOfServiceUrl: response.TermsOfServiceUrl ?? null,
      privacyPolicyUrl: response.PrivacyPolicyUrl ?? null,
    };
    
    console.log('✅ Converted response for frontend:', result);
    return result;
  }

  async getPublicSecuritySettings(): Promise<SecuritySettings> {
    try {
      // Use public endpoint directly for login/registration pages
      console.log('🔄 Fetching public security settings from /auth/security-settings...');
      const response = await apiService.publicRequest<PublicSecuritySettingsApiDto>('/auth/security-settings', {
        method: 'GET',
      });
      
      console.log('✅ Public endpoint response:', response);

      // Convert public API response to frontend interface
      return {
        passwordMinLength: response.passwordMinLength,
        passwordRequireUppercase: response.passwordRequireUppercase,
        passwordRequireLowercase: response.passwordRequireLowercase,
        passwordRequireDigits: response.passwordRequireDigits,
        passwordRequireSpecialChars: response.passwordRequireSpecialChars,
        passwordMaxAge: null, // Not available in public endpoint
        passwordPreventReuse: null, // Not available in public endpoint
        sessionTimeoutMinutes: 30, // Default values for admin-only fields
        jwtTokenLifetimeMinutes: 60,
        preventConcurrentLogin: 'Disabled',
        maxFailedLoginAttempts: 5,
        accountLockoutMinutes: 30,
        rateLimitLoginMaxAttempts: 5,
        rateLimitLoginWindowMinutes: 15,
        rateLimitLoginBlockDurationMinutes: 30,
        captchaEnabled: response.captchaEnabled,
        captchaProvider: response.captchaProvider as 'recaptcha' | 'hcaptcha',
        recaptchaSiteKey: response.recaptchaSiteKey,
        recaptchaSecretKey: null, // Not exposed in public endpoint
        hCaptchaSiteKey: response.hCaptchaSiteKey,
        hCaptchaSecretKey: null, // Not exposed in public endpoint
        termsOfServiceUrl: response.termsOfServiceUrl,
        privacyPolicyUrl: response.privacyPolicyUrl,
      };
    } catch (error) {
      console.warn('Failed to fetch public security settings, using defaults:', error);
      // Return default security settings
      return {
        passwordMinLength: 8,
        passwordRequireUppercase: true,
        passwordRequireLowercase: true,
        passwordRequireDigits: true,
        passwordRequireSpecialChars: true,
        passwordMaxAge: null,
        passwordPreventReuse: null,
        sessionTimeoutMinutes: 30,
        jwtTokenLifetimeMinutes: 60,
        preventConcurrentLogin: 'Disabled',
        maxFailedLoginAttempts: 5,
        accountLockoutMinutes: 30,
        rateLimitLoginMaxAttempts: 5,
        rateLimitLoginWindowMinutes: 15,
        rateLimitLoginBlockDurationMinutes: 30,
        captchaEnabled: false,
        captchaProvider: 'recaptcha',
        recaptchaSiteKey: null,
        recaptchaSecretKey: null,
        hCaptchaSiteKey: null,
        hCaptchaSecretKey: null,
        termsOfServiceUrl: null,
        privacyPolicyUrl: null,
      };
    }
  }
}

export const settingsService = new SettingsService();
export default settingsService;