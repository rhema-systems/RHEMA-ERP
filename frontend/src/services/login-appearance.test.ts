import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  publicRequest: vi.fn(),
}));

vi.mock('./api.service', () => ({
  apiService: {
    publicRequest: mocks.publicRequest,
  },
}));

import {
  DEFAULT_LOGIN_PAGE_STYLE,
  loginAppearanceService,
  normalizeLoginPageStyle,
} from './login-appearance';

describe('loginAppearanceService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('reads the selected design from the anonymous login configuration endpoint', async () => {
    mocks.publicRequest.mockResolvedValue({ loginPageStyle: 'DarkPremium' });

    await expect(loginAppearanceService.getPublicLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'DarkPremium',
    });
    expect(mocks.publicRequest).toHaveBeenCalledWith('/public/config/login', {
      method: 'GET',
      cache: 'no-store',
    });
  });

  it.each([undefined, null, '', 'Unknown', 'darkpremium'])(
    'uses the safe corporate fallback for unsupported value %s',
    (value) => {
      expect(normalizeLoginPageStyle(value)).toBe(DEFAULT_LOGIN_PAGE_STYLE);
    }
  );

  it('keeps the login usable when public configuration is unavailable', async () => {
    mocks.publicRequest.mockRejectedValue(new Error('Configuration unavailable'));

    await expect(loginAppearanceService.getPublicLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'LightCorporate',
    });
  });
});
