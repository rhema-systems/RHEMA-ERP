import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  redirect: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  redirect: mocks.redirect,
}));

import LegacySecuritySettingsPage from './page';

describe('LegacySecuritySettingsPage', () => {
  it('redirects to the consolidated Security Management login appearance section', () => {
    LegacySecuritySettingsPage();

    expect(mocks.redirect).toHaveBeenCalledWith(
      '/administration/security/dashboard?tab=settings&section=appearance'
    );
  });
});
