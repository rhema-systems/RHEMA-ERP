import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { SecuritySettings } from '@/services/settings';

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}

vi.stubGlobal('ResizeObserver', ResizeObserverStub);

const mocks = vi.hoisted(() => ({
  getSecuritySettings: vi.fn(),
  updateSecuritySettings: vi.fn(),
  getLoginAppearance: vi.fn(),
  updateLoginAppearance: vi.fn(),
  getSecurityMetrics: vi.fn(),
  getSecurityAlerts: vi.fn(),
  getSecurityHealthScore: vi.fn(),
  getThreatDetections: vi.fn(),
  getTwoFactorSettings: vi.fn(),
  onSecurityAlert: vi.fn(),
  onSecurityMetrics: vi.fn(),
  hasAnyRole: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('../../../../hooks/use-auth', () => ({
  useAuth: () => ({ hasAnyRole: mocks.hasAnyRole }),
}));

vi.mock('../../../../hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('../../../../services/auth', () => ({
  authService: { getStoredUser: () => ({ email: 'admin@example.test' }) },
}));

vi.mock('../../../../services/settings', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/services/settings')>();
  return {
    ...original,
    settingsService: {
      getSecuritySettings: mocks.getSecuritySettings,
      updateSecuritySettings: mocks.updateSecuritySettings,
      getLoginAppearance: mocks.getLoginAppearance,
      updateLoginAppearance: mocks.updateLoginAppearance,
    },
  };
});

vi.mock('../../../../services/security', () => ({
  securityService: {
    getSecurityMetrics: mocks.getSecurityMetrics,
    getSecurityAlerts: mocks.getSecurityAlerts,
    getSecurityHealthScore: mocks.getSecurityHealthScore,
    getThreatDetections: mocks.getThreatDetections,
    getTwoFactorSettings: mocks.getTwoFactorSettings,
    onSecurityAlert: mocks.onSecurityAlert,
    onSecurityMetrics: mocks.onSecurityMetrics,
    dismissAlert: vi.fn(),
  },
}));

vi.mock('../../../../components/security/TwoFactorAuth', () => ({ TwoFactorAuth: () => null }));
vi.mock('../../../../components/security/AuditLog', () => ({ AuditLog: () => null }));
vi.mock('../../../../components/security/DeviceManagement', () => ({ DeviceManagement: () => null }));
vi.mock('../../../../components/security/SecurityPolicies', () => ({ SecurityPolicies: () => null }));
vi.mock('@/components/admin/SessionManagementTab', () => ({ SessionManagementTab: () => null }));
vi.mock('../../../../components/ui/client-only', () => ({
  ClientOnly: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

import SecurityDashboardPage from './page';

const securitySettings: SecuritySettings = {
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

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <SecurityDashboardPage />
    </QueryClientProvider>
  );
}

function clickTab(name: RegExp) {
  const tab = screen.getByRole('tab', { name });
  fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
  fireEvent.click(tab);
  return tab;
}

describe('Security Management consolidated login appearance', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, '', '/administration/security/dashboard');
    mocks.hasAnyRole.mockReturnValue(true);
    mocks.getSecuritySettings.mockResolvedValue(securitySettings);
    mocks.updateSecuritySettings.mockResolvedValue(securitySettings);
    mocks.getLoginAppearance.mockResolvedValue({ loginPageStyle: 'LightCorporate' });
    mocks.updateLoginAppearance.mockImplementation(async (settings) => settings);
    mocks.getSecurityMetrics.mockResolvedValue({
      twoFactorAdoptionRate: 0,
      failedLoginAttempts: 0,
      activeSessions: 1,
      securityIncidents: 0,
      passwordCompliance: 100,
      auditEventsToday: 0,
      trends: {
        twoFactorAdoptionRate: 0,
        failedLoginAttempts: 0,
        activeSessions: 0,
        securityIncidents: 0,
        passwordCompliance: 0,
        auditEventsToday: 0,
      },
    });
    mocks.getSecurityAlerts.mockResolvedValue([]);
    mocks.getSecurityHealthScore.mockResolvedValue({
      overall: 100,
      categories: { authentication: 100 },
      recommendations: [],
    });
    mocks.getThreatDetections.mockResolvedValue([]);
    mocks.getTwoFactorSettings.mockResolvedValue({ isEnabled: false });
    mocks.onSecurityAlert.mockReturnValue(() => undefined);
    mocks.onSecurityMetrics.mockReturnValue(() => undefined);
  });

  it('manages login appearance from the existing Settings tab and Save Changes action', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: 'Security Management' })).toBeInTheDocument();
    clickTab(/^settings$/i);
    const appearanceTab = clickTab(/login appearance/i);
    await waitFor(() => expect(appearanceTab).toHaveAttribute('aria-selected', 'true'));

    fireEvent.click(await screen.findByRole('radio', { name: 'Dark Premium' }));
    fireEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(mocks.updateLoginAppearance).toHaveBeenCalledWith({
        loginPageStyle: 'DarkPremium',
      });
    });
    expect(mocks.updateSecuritySettings).not.toHaveBeenCalled();
  });

  it('opens the consolidated appearance section from the legacy redirect target', async () => {
    window.history.replaceState(
      {},
      '',
      '/administration/security/dashboard?tab=settings&section=appearance'
    );

    renderPage();

    const settingsTab = await screen.findByRole('tab', { name: /^settings$/i });
    await waitFor(() => expect(settingsTab).toHaveAttribute('aria-selected', 'true'));
    expect(screen.getByRole('tab', { name: /login appearance/i })).toHaveAttribute(
      'aria-selected',
      'true'
    );
    expect(await screen.findByRole('radio', { name: 'Light Corporate' })).toBeVisible();
  });
});
