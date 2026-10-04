import { apiService } from './api.service';

export type LoginPageStyle = 'LightCorporate' | 'DarkPremium';

export interface PublicLoginAppearance {
  loginPageStyle: LoginPageStyle;
}

export const DEFAULT_LOGIN_PAGE_STYLE: LoginPageStyle = 'LightCorporate';

export const normalizeLoginPageStyle = (value: unknown): LoginPageStyle =>
  value === 'DarkPremium' ? 'DarkPremium' : DEFAULT_LOGIN_PAGE_STYLE;

class LoginAppearanceService {
  async getPublicLoginAppearance(): Promise<PublicLoginAppearance> {
    try {
      const response = await apiService.publicRequest<{ loginPageStyle?: unknown }>(
        '/public/config/login',
        {
          method: 'GET',
          cache: 'no-store',
        }
      );

      return {
        loginPageStyle: normalizeLoginPageStyle(response?.loginPageStyle),
      };
    } catch {
      return { loginPageStyle: DEFAULT_LOGIN_PAGE_STYLE };
    }
  }
}

export const loginAppearanceService = new LoginAppearanceService();
