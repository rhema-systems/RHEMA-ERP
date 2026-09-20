import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getStoredToken: vi.fn(),
  getStoredUser: vi.fn(),
  login: vi.fn(),
  requestLoginOtp: vi.fn(),
  loginWithOtp: vi.fn(),
  getTenants: vi.fn(),
  getPublicSecuritySettings: vi.fn(),
  selectTenant: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mocks.replace }),
  useSearchParams: () => new URLSearchParams(window.location.search),
}));
vi.mock('next/link', () => ({ default: ({ children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement>) => <a {...props}>{children}</a> }));
vi.mock('react-google-recaptcha', () => ({ default: () => <div data-testid="captcha">CAPTCHA</div> }));
vi.mock('../../services/auth', () => ({ authService: mocks }));
vi.mock('../../services/api.service', () => ({ apiService: mocks }));
vi.mock('../../services/settings', () => ({ settingsService: mocks }));
vi.mock('../../services/tenant', () => ({ tenantService: mocks }));

import LoginPage from './page';

const internalUser = {
  id: 'login-user',
  roles: ['Manager'],
  accessibleTenants: [{ tenantCode: 'TDC', tenantName: 'TDC', isDefault: true }],
};

const renderPage = () => render(
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
    <React.StrictMode><LoginPage /></React.StrictMode>
  </QueryClientProvider>
);

describe('login entry points and redirects', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, '', '/login');
    mocks.getStoredToken.mockReturnValue(null);
    mocks.getStoredUser.mockReturnValue(internalUser);
    mocks.getTenants.mockResolvedValue([{ isActive: true, allowSelfRegistration: true }]);
    mocks.getPublicSecuritySettings.mockResolvedValue({ captchaEnabled: false });
    mocks.selectTenant.mockResolvedValue(undefined);
    mocks.login.mockResolvedValue({ token: 'test-session', user: internalUser });
  });

  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it('keeps supplier applications and password/OTP controls, without a registration button or helper', async () => {
    renderPage();
    expect(await screen.findByRole('link', { name: 'Apply as a supplier' })).toHaveAttribute('href', '/supplier-application');
    expect(screen.queryByText('Create Account')).not.toBeInTheDocument();
    expect(screen.queryByText(/Supplier applicants verify a contact/)).not.toBeInTheDocument();
    expect(screen.getByLabelText('USER NAME OR EMAIL ADDRESS')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('PASSWORD')).toHaveAttribute('type', 'text');
    fireEvent.click(screen.getByRole('button', { name: 'One-time code' }));
    expect(screen.getByRole('button', { name: 'Send code' })).toBeVisible();
  });

  it('continues to render required CAPTCHA verification', async () => {
    mocks.getPublicSecuritySettings.mockResolvedValue({ captchaEnabled: true, recaptchaSiteKey: 'public-test-key', captchaProvider: 'recaptcha' });
    renderPage();
    expect(await screen.findByTestId('captcha')).toBeVisible();
  });

  it('redirects a stored internal session once when effects are replayed', async () => {
    mocks.getStoredToken.mockReturnValue('test-session');
    renderPage();
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith('/tenant-select'));
    expect(mocks.replace).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('status')).toHaveTextContent('Signing you in...');
    expect(mocks.selectTenant).not.toHaveBeenCalled();
  });

  it.each([
    ['ExternalUser', '/external-portal'],
    ['Candidate', '/external-portal/careers'],
    ['ConsultantClient', '/external-portal/client-timesheets'],
  ])('selects once and preserves the %s destination', async (role, destination) => {
    mocks.getStoredToken.mockReturnValue('test-session');
    mocks.getStoredUser.mockReturnValue({ ...internalUser, roles: [role] });
    renderPage();
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith(destination));
    expect(mocks.selectTenant).toHaveBeenCalledTimes(1);
    expect(mocks.selectTenant).toHaveBeenCalledWith('TDC', false);
    expect(mocks.replace).toHaveBeenCalledTimes(1);
  });

  it('requires tenant selection after an external auto-selection failure', async () => {
    mocks.getStoredToken.mockReturnValue('test-session');
    mocks.getStoredUser.mockReturnValue({ ...internalUser, roles: ['ExternalUser'] });
    mocks.selectTenant.mockRejectedValue(new Error('Organization unavailable'));
    renderPage();
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith('/tenant-select'));
    expect(mocks.selectTenant).toHaveBeenCalledTimes(1);
  });

  it('retains required password changes ahead of tenant selection', async () => {
    mocks.getStoredToken.mockReturnValue('test-session');
    mocks.getStoredUser.mockReturnValue({ ...internalUser, mustChangePassword: true });
    renderPage();
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith('/change-temporary-password'));
    expect(mocks.selectTenant).not.toHaveBeenCalled();
  });

  it('keeps password 2FA on the verification screen until authentication completes', async () => {
    mocks.login.mockResolvedValue({ requiresTwoFactor: true, twoFactorToken: 'test-challenge' });
    renderPage();
    fireEvent.change(screen.getByLabelText('USER NAME OR EMAIL ADDRESS'), { target: { value: 'test.manager' } });
    fireEvent.change(screen.getByLabelText('PASSWORD'), { target: { value: 'Example123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign In' }));
    expect(await screen.findByLabelText('AUTHENTICATION CODE')).toBeVisible();
    expect(mocks.replace).not.toHaveBeenCalled();
    expect(mocks.selectTenant).not.toHaveBeenCalled();
  });

  it('shares one redirect between a successful login and the stored-session effect', async () => {
    mocks.login.mockImplementation(async () => {
      mocks.getStoredToken.mockReturnValue('test-session');
      return { token: 'test-session', user: internalUser };
    });
    renderPage();
    fireEvent.change(screen.getByLabelText('USER NAME OR EMAIL ADDRESS'), { target: { value: 'test.manager' } });
    fireEvent.change(screen.getByLabelText('PASSWORD'), { target: { value: 'Example123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign In' }));
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith('/tenant-select'));
    expect(mocks.replace).toHaveBeenCalledTimes(1);
    expect(mocks.login).toHaveBeenCalledTimes(1);
  });
});
