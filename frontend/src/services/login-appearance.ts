import { apiService } from './api.service';

export type LoginPageStyle = 'LightCorporate' | 'DarkPremium';

export interface PublicLoginAppearance {
  loginPageStyle: LoginPageStyle;
  lightBackgroundUrl: string | null;
  darkBackgroundUrl: string | null;
}

export const DEFAULT_LOGIN_PAGE_STYLE: LoginPageStyle = 'LightCorporate';

export const normalizeLoginPageStyle = (value: unknown): LoginPageStyle =>
  value === 'DarkPremium' ? 'DarkPremium' : DEFAULT_LOGIN_PAGE_STYLE;

class LoginAppearanceService {
  async getPublicLoginAppearance(): Promise<PublicLoginAppearance> {
    try {
      const response = await apiService.publicRequest<{
        loginPageStyle?: unknown;
        lightBackgroundUrl?: unknown;
        darkBackgroundUrl?: unknown;
      }>(
        '/public/config/login',
        {
          method: 'GET',
          cache: 'no-store',
        }
      );

      return {
        loginPageStyle: normalizeLoginPageStyle(response?.loginPageStyle),
        lightBackgroundUrl:
          typeof response?.lightBackgroundUrl === 'string' ? response.lightBackgroundUrl : null,
        darkBackgroundUrl:
          typeof response?.darkBackgroundUrl === 'string' ? response.darkBackgroundUrl : null,
      };
    } catch {
      return {
        loginPageStyle: DEFAULT_LOGIN_PAGE_STYLE,
        lightBackgroundUrl: null,
        darkBackgroundUrl: null,
      };
    }
  }
}

export const loginAppearanceService = new LoginAppearanceService();
