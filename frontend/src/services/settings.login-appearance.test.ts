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
    mocks.request.mockResolvedValue({
      loginPageStyle: 'DarkPremium',
      lightBackgroundUrl: null,
      darkBackgroundUrl: 'https://erp.example/dark',
    });

    await expect(settingsService.getLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'DarkPremium',
      lightBackgroundUrl: null,
      darkBackgroundUrl: 'https://erp.example/dark',
    });
    expect(mocks.request).toHaveBeenCalledWith('/settings/login-appearance', {
      method: 'GET',
    });
  });

  it('persists the selected login appearance through the authenticated endpoint', async () => {
    mocks.request.mockResolvedValue({ loginPageStyle: 'LightCorporate' });

    await expect(
      settingsService.updateLoginAppearance({ loginPageStyle: 'LightCorporate' })
    ).resolves.toEqual({
      loginPageStyle: 'LightCorporate',
      lightBackgroundUrl: null,
      darkBackgroundUrl: null,
    });
    expect(mocks.request).toHaveBeenCalledWith('/settings/login-appearance', {
      method: 'PUT',
      body: JSON.stringify({ loginPageStyle: 'LightCorporate' }),
    });
  });

  it('uses the safe light default if an invalid value reaches the client', async () => {
    mocks.request.mockResolvedValue({ loginPageStyle: 'UnsupportedStyle' });

    await expect(settingsService.getLoginAppearance()).resolves.toEqual({
      loginPageStyle: 'LightCorporate',
      lightBackgroundUrl: null,
      darkBackgroundUrl: null,
    });
  });

  it('uploads a background through the authenticated multipart endpoint', async () => {
    const file = new File(['image'], 'campus.webp', { type: 'image/webp' });
    mocks.request.mockResolvedValue({
      loginPageStyle: 'LightCorporate',
      lightBackgroundUrl: 'https://erp.example/light',
      darkBackgroundUrl: null,
    });

    await settingsService.uploadLoginBackground('LightCorporate', file);

    const [, request] = mocks.request.mock.calls[0];
    expect(mocks.request.mock.calls[0][0]).toBe(
      '/settings/login-appearance/background/LightCorporate'
    );
    expect(request.method).toBe('POST');
    expect(request.body).toBeInstanceOf(FormData);
    expect(request.body.get('file')).toBe(file);
  });
});
