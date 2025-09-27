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
}

// API DTO interface to match backend SecuritySettingsDto
interface SecuritySettingsApiDto {
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
}

class SettingsService {
  async getSecuritySettings(): Promise<SecuritySettings> {
    try {
      // Use the public endpoint for security settings (no authentication required)
      const response = await apiService.publicRequest<SecuritySettingsApiDto>('/auth/security-settings', {
        method: 'GET',
      });

      // Convert API response to frontend interface
      return {
        passwordMinLength: response.passwordMinLength,
        passwordRequireUppercase: response.passwordRequireUppercase,
        passwordRequireLowercase: response.passwordRequireLowercase,
        passwordRequireDigits: response.passwordRequireDigits,
        passwordRequireSpecialChars: response.passwordRequireSpecialChars,
        passwordMaxAge: response.passwordMaxAge,
        passwordPreventReuse: response.passwordPreventReuse,
        sessionTimeoutMinutes: response.sessionTimeoutMinutes,
        jwtTokenLifetimeMinutes: response.jwtTokenLifetimeMinutes,
        preventConcurrentLogin: response.preventConcurrentLogin,
        maxFailedLoginAttempts: response.maxFailedLoginAttempts,
        accountLockoutMinutes: response.accountLockoutMinutes,
        rateLimitLoginMaxAttempts: response.rateLimitLoginMaxAttempts,
        rateLimitLoginWindowMinutes: response.rateLimitLoginWindowMinutes,
        rateLimitLoginBlockDurationMinutes: response.rateLimitLoginBlockDurationMinutes,
        captchaEnabled: response.captchaEnabled,
        captchaProvider: response.captchaProvider,
        recaptchaSiteKey: response.recaptchaSiteKey,
        recaptchaSecretKey: response.recaptchaSecretKey,
        hCaptchaSiteKey: response.hCaptchaSiteKey,
        hCaptchaSecretKey: response.hCaptchaSecretKey,
      };
    } catch (error) {
      console.warn('Failed to fetch security settings, using defaults:', error);
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
      };
    }
  }

  async updateSecuritySettings(settings: SecuritySettings): Promise<SecuritySettings> {
    // Convert frontend interface to API DTO
    const requestDto: SecuritySettingsApiDto = {
      passwordMinLength: settings.passwordMinLength,
      passwordRequireUppercase: settings.passwordRequireUppercase,
      passwordRequireLowercase: settings.passwordRequireLowercase,
      passwordRequireDigits: settings.passwordRequireDigits,
      passwordRequireSpecialChars: settings.passwordRequireSpecialChars,
      passwordMaxAge: settings.passwordMaxAge,
      passwordPreventReuse: settings.passwordPreventReuse,
      sessionTimeoutMinutes: settings.sessionTimeoutMinutes,
      jwtTokenLifetimeMinutes: settings.jwtTokenLifetimeMinutes,
      preventConcurrentLogin: settings.preventConcurrentLogin,
      maxFailedLoginAttempts: settings.maxFailedLoginAttempts,
      accountLockoutMinutes: settings.accountLockoutMinutes,
      rateLimitLoginMaxAttempts: settings.rateLimitLoginMaxAttempts,
      rateLimitLoginWindowMinutes: settings.rateLimitLoginWindowMinutes,
      rateLimitLoginBlockDurationMinutes: settings.rateLimitLoginBlockDurationMinutes,
      captchaEnabled: settings.captchaEnabled,
      captchaProvider: settings.captchaProvider,
      recaptchaSiteKey: settings.recaptchaSiteKey,
      recaptchaSecretKey: settings.recaptchaSecretKey,
      hCaptchaSiteKey: settings.hCaptchaSiteKey,
      hCaptchaSecretKey: settings.hCaptchaSecretKey,
    };

    const response = await apiService.request<SecuritySettingsApiDto>('/settings/security', {
      method: 'PUT',
      body: JSON.stringify(requestDto),
    });

    // Convert API response back to frontend interface
    return {
      passwordMinLength: response.passwordMinLength,
      passwordRequireUppercase: response.passwordRequireUppercase,
      passwordRequireLowercase: response.passwordRequireLowercase,
      passwordRequireDigits: response.passwordRequireDigits,
      passwordRequireSpecialChars: response.passwordRequireSpecialChars,
      passwordMaxAge: response.passwordMaxAge,
      passwordPreventReuse: response.passwordPreventReuse,
      sessionTimeoutMinutes: response.sessionTimeoutMinutes,
      jwtTokenLifetimeMinutes: response.jwtTokenLifetimeMinutes,
      preventConcurrentLogin: response.preventConcurrentLogin,
      maxFailedLoginAttempts: response.maxFailedLoginAttempts,
      accountLockoutMinutes: response.accountLockoutMinutes,
      rateLimitLoginMaxAttempts: response.rateLimitLoginMaxAttempts,
      rateLimitLoginWindowMinutes: response.rateLimitLoginWindowMinutes,
      rateLimitLoginBlockDurationMinutes: response.rateLimitLoginBlockDurationMinutes,
      captchaEnabled: response.captchaEnabled,
      captchaProvider: response.captchaProvider,
      recaptchaSiteKey: response.recaptchaSiteKey,
      recaptchaSecretKey: response.recaptchaSecretKey,
      hCaptchaSiteKey: response.hCaptchaSiteKey,
      hCaptchaSecretKey: response.hCaptchaSecretKey,
    };
  }
}

export const settingsService = new SettingsService();
export default settingsService;