import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
}));

vi.mock('./api.service', () => ({
  apiService: {
    request: mocks.request,
  },
}));

import { settingsService } from './settings';

describe('settingsService login appearance', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads the authenticated login appearance setting', async () => {
    mocks.request.mockResolvedValue({ loginPageStyle: 'DarkPremium' });

    await expect(settingsService.getLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'DarkPremium',
    });
    expect(mocks.request).toHaveBeenCalledWith('/settings/login-appearance', {
      method: 'GET',
    });
  });

  it('persists the selected login appearance through the authenticated endpoint', async () => {
    mocks.request.mockResolvedValue({ loginPageStyle: 'LightCorporate' });

    await expect(
      settingsService.updateLoginAppearance({ loginPageStyle: 'LightCorporate' })
    ).resolves.toEqual({ loginPageStyle: 'LightCorporate' });
    expect(mocks.request).toHaveBeenCalledWith('/settings/login-appearance', {
      method: 'PUT',
      body: JSON.stringify({ loginPageStyle: 'LightCorporate' }),
    });
  });

  it('uses the safe light default if an invalid value reaches the client', async () => {
    mocks.request.mockResolvedValue({ loginPageStyle: 'UnsupportedStyle' });

    await expect(settingsService.getLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'LightCorporate',
    });
  });
});
