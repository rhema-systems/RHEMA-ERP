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
  recaptchaSecretConfigured?: boolean;
  hCaptchaSiteKey: string | null;
  hCaptchaSecretKey: string | null;
  hCaptchaSecretConfigured?: boolean;

  // Legal URLs
  termsOfServiceUrl: string | null;
  privacyPolicyUrl: string | null;
}

export interface SessionRuntimeSettings {
  sessionTimeoutMinutes: number;
  jwtTokenLifetimeMinutes: number;
}

export type LoginPageStyle = 'LightCorporate' | 'DarkPremium';

export interface LoginAppearanceSettings {
  loginPageStyle: LoginPageStyle;
  lightBackgroundUrl: string | null;
  darkBackgroundUrl: string | null;
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
  RecaptchaSecretConfigured?: boolean;
  HCaptchaSiteKey: string | null;
  HCaptchaSecretKey: string | null;
  HCaptchaSecretConfigured?: boolean;

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

  // Tenant (optional; resolved from host for public portals like support.company.com)
  tenantCode?: string | null;
  tenantName?: string | null;
  
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

interface SessionRuntimeSettingsApiDto {
  SessionTimeoutMinutes?: number;
  sessionTimeoutMinutes?: number;
  JwtTokenLifetimeMinutes?: number;
  jwtTokenLifetimeMinutes?: number;
}

const mapAdminSecuritySettings = (response: AdminSecuritySettingsApiDto | Record<string, unknown>): SecuritySettings => {
  const data = response as Record<string, unknown>;

  return {
    passwordMinLength: Number(data.PasswordMinLength ?? data.passwordMinLength ?? 8),
    passwordRequireUppercase: Boolean(data.PasswordRequireUppercase ?? data.passwordRequireUppercase ?? true),
    passwordRequireLowercase: Boolean(data.PasswordRequireLowercase ?? data.passwordRequireLowercase ?? true),
    passwordRequireDigits: Boolean(data.PasswordRequireDigits ?? data.passwordRequireDigits ?? true),
    passwordRequireSpecialChars: Boolean(data.PasswordRequireSpecialChars ?? data.passwordRequireSpecialChars ?? true),
    passwordMaxAge: (data.PasswordMaxAge ?? data.passwordMaxAge ?? null) as number | null,
    passwordPreventReuse: (data.PasswordPreventReuse ?? data.passwordPreventReuse ?? null) as number | null,
    sessionTimeoutMinutes: Number(data.SessionTimeoutMinutes ?? data.sessionTimeoutMinutes ?? 30),
    jwtTokenLifetimeMinutes: Number(data.JwtTokenLifetimeMinutes ?? data.jwtTokenLifetimeMinutes ?? 60),
    preventConcurrentLogin: (data.PreventConcurrentLogin ?? data.preventConcurrentLogin ?? 'Disabled') as 'Disabled' | 'LogoutFromAllDevices' | 'PreventSubsequentLogins',
    maxFailedLoginAttempts: Number(data.MaxFailedLoginAttempts ?? data.maxFailedLoginAttempts ?? 5),
    accountLockoutMinutes: Number(data.AccountLockoutMinutes ?? data.accountLockoutMinutes ?? 30),
    rateLimitLoginMaxAttempts: Number(data.RateLimitLoginMaxAttempts ?? data.rateLimitLoginMaxAttempts ?? 5),
    rateLimitLoginWindowMinutes: Number(data.RateLimitLoginWindowMinutes ?? data.rateLimitLoginWindowMinutes ?? 15),
    rateLimitLoginBlockDurationMinutes: Number(data.RateLimitLoginBlockDurationMinutes ?? data.rateLimitLoginBlockDurationMinutes ?? 30),
    captchaEnabled: Boolean(data.CaptchaEnabled ?? data.captchaEnabled ?? false),
    captchaProvider: (data.CaptchaProvider ?? data.captchaProvider ?? 'recaptcha') as 'recaptcha' | 'hcaptcha',
    recaptchaSiteKey: (data.RecaptchaSiteKey ?? data.recaptchaSiteKey ?? null) as string | null,
    recaptchaSecretKey: (data.RecaptchaSecretKey ?? data.recaptchaSecretKey ?? null) as string | null,
    recaptchaSecretConfigured: Boolean(data.RecaptchaSecretConfigured ?? data.recaptchaSecretConfigured ?? false),
    hCaptchaSiteKey: (data.HCaptchaSiteKey ?? data.hCaptchaSiteKey ?? null) as string | null,
    hCaptchaSecretKey: (data.HCaptchaSecretKey ?? data.hCaptchaSecretKey ?? null) as string | null,
    hCaptchaSecretConfigured: Boolean(data.HCaptchaSecretConfigured ?? data.hCaptchaSecretConfigured ?? false),
    termsOfServiceUrl: (data.TermsOfServiceUrl ?? data.termsOfServiceUrl ?? null) as string | null,
    privacyPolicyUrl: (data.PrivacyPolicyUrl ?? data.privacyPolicyUrl ?? null) as string | null,
  };
};

const mapLoginAppearanceSettings = (
  response: LoginAppearanceSettings | Record<string, unknown>
): LoginAppearanceSettings => {
  const data = response as Record<string, unknown>;
  const configuredStyle = data.loginPageStyle ?? data.LoginPageStyle;

  return {
    loginPageStyle:
      configuredStyle === 'DarkPremium' ? 'DarkPremium' : 'LightCorporate',
    lightBackgroundUrl: (data.lightBackgroundUrl ?? data.LightBackgroundUrl ?? null) as string | null,
    darkBackgroundUrl: (data.darkBackgroundUrl ?? data.DarkBackgroundUrl ?? null) as string | null,
  };
};

class SettingsService {
  async getSessionSettings(): Promise<SessionRuntimeSettings> {
    const response = await apiService.request<SessionRuntimeSettingsApiDto>('/auth/session-settings', {
      method: 'GET',
    });

    const sessionTimeoutMinutes = Number(response.SessionTimeoutMinutes ?? response.sessionTimeoutMinutes);
    const jwtTokenLifetimeMinutes = Number(response.JwtTokenLifetimeMinutes ?? response.jwtTokenLifetimeMinutes);

    if (!Number.isFinite(sessionTimeoutMinutes) || sessionTimeoutMinutes <= 0) {
      throw new Error('Session timeout is missing from authenticated session settings.');
    }

    if (!Number.isFinite(jwtTokenLifetimeMinutes) || jwtTokenLifetimeMinutes <= 0) {
      throw new Error('JWT token lifetime is missing from authenticated session settings.');
    }

    return {
      sessionTimeoutMinutes,
      jwtTokenLifetimeMinutes,
    };
  }

  async getSecuritySettings(): Promise<SecuritySettings> {
    const response = await apiService.request<AdminSecuritySettingsApiDto>('/settings/security', {
      method: 'GET',
    });

    return mapAdminSecuritySettings(response);
  }

  async updateSecuritySettings(settings: SecuritySettings): Promise<SecuritySettings> {
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
      RecaptchaSecretConfigured: settings.recaptchaSecretConfigured,
      HCaptchaSiteKey: settings.hCaptchaSiteKey || null,
      HCaptchaSecretKey: settings.hCaptchaSecretKey || null,
      HCaptchaSecretConfigured: settings.hCaptchaSecretConfigured,
      TermsOfServiceUrl: settings.termsOfServiceUrl || null,
      PrivacyPolicyUrl: settings.privacyPolicyUrl || null,
    };

    const response = await apiService.request<AdminSecuritySettingsApiDto>('/settings/security', {
      method: 'PUT',
      body: JSON.stringify(requestDto),
    });

    return mapAdminSecuritySettings(response);
  }

  async getLoginAppearance(): Promise<LoginAppearanceSettings> {
    const response = await apiService.request<LoginAppearanceSettings>(
      '/settings/login-appearance',
      { method: 'GET' }
    );

    return mapLoginAppearanceSettings(response);
  }

  async updateLoginAppearance(
    settings: Pick<LoginAppearanceSettings, 'loginPageStyle'>
  ): Promise<LoginAppearanceSettings> {
    const response = await apiService.request<LoginAppearanceSettings>(
      '/settings/login-appearance',
      {
        method: 'PUT',
        body: JSON.stringify(settings),
      }
    );

    return mapLoginAppearanceSettings(response);
  }

  async uploadLoginBackground(
    style: LoginPageStyle,
    file: File
  ): Promise<LoginAppearanceSettings> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiService.request<LoginAppearanceSettings>(
      `/settings/login-appearance/background/${style}`,
      {
        method: 'POST',
        body: formData,
      }
    );

    return mapLoginAppearanceSettings(response);
  }

  async resetLoginBackground(style: LoginPageStyle): Promise<LoginAppearanceSettings> {
    const response = await apiService.request<LoginAppearanceSettings>(
      `/settings/login-appearance/background/${style}`,
      { method: 'DELETE' }
    );

    return mapLoginAppearanceSettings(response);
  }

  async getPublicSecuritySettings(): Promise<SecuritySettings> {
    try {
      const response = await apiService.publicRequest<PublicSecuritySettingsApiDto>('/auth/security-settings', {
        method: 'GET',
      });

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
