import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { hydrateRoot } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { Header } from './header';
import { FontSizeProvider } from '../../contexts/FontSizeContext';
import { FontSizeToggle } from './FontSizeToggle';

const mocks = vi.hoisted(() => ({
  logout: vi.fn(),
  setTheme: vi.fn(),
  roles: ['SuperAdmin'],
  permissions: ['*'],
  userAvailable: true,
}));

vi.stubGlobal('React', React);

vi.mock('../../hooks/use-auth', () => ({
  useAuth: () => ({
    user: mocks.userAvailable ? {
      firstName: 'Demo',
      lastName: 'User',
      username: 'demo.user',
      id: 'user-123',
      email: 'demo.user@example.com',
      isActive: true,
      roles: mocks.roles,
      permissions: mocks.permissions,
    } : undefined,
    logout: mocks.logout,
    isLoggingOut: false,
    hasAnyRole: (roles: string[]) => roles.some(role =>
      mocks.roles.some(assigned => assigned.toLowerCase() === role.toLowerCase())),
    hasAnyPermission: (permissions: string[]) =>
      mocks.roles.some(role => ['superadmin', 'tenantadmin'].includes(role.toLowerCase())) ||
      mocks.permissions.includes('*') ||
      permissions.some(permission =>
        mocks.permissions.some(assigned => assigned.toLowerCase() === permission.toLowerCase())),
  }),
}));

vi.mock('../../contexts/TenantContext', () => ({
  useTenant: () => ({
    currentTenant: { name: 'Demo Organization', code: 'DEMO' },
    currentTenantCode: 'DEMO',
    setCurrentTenantCode: vi.fn(),
  }),
}));

vi.mock('../../contexts/session-timeout-context', () => ({
  useOptionalSessionTimeoutContext: () => null,
}));

vi.mock('../../contexts/ThemeContext', () => ({
  useTheme: () => ({
    theme: 'light',
    actualTheme: 'light',
    systemTheme: 'light',
    setTheme: mocks.setTheme,
    toggleTheme: vi.fn(),
  }),
}));

vi.mock('../ClientOnly', () => ({
  ClientOnly: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('../notifications/HeaderNotificationBell', () => ({
  default: () => <button type="button">Notifications</button>,
}));

describe('Header layout', () => {
  beforeEach(() => {
    localStorage.removeItem('erp-font-size');
    delete document.documentElement.dataset.fontSize;
    mocks.logout.mockReset();
    mocks.setTheme.mockReset();
    mocks.roles = ['SuperAdmin'];
    mocks.permissions = ['*'];
    mocks.userAvailable = true;
  });

  it('changes font size and restores the choice when the app reopens', () => {
    const first = render(<FontSizeProvider><Header /></FontSizeProvider>);
    expect(screen.getByRole('button', { name: 'Medium font size' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Large font size' }));
    expect(document.documentElement).toHaveAttribute('data-font-size', 'large');
    expect(localStorage.getItem('erp-font-size')).toBe('large');
    first.unmount();

    render(<FontSizeProvider><Header /></FontSizeProvider>);
    expect(screen.getByRole('button', { name: 'Large font size' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Small font size' }));
    expect(document.documentElement).toHaveAttribute('data-font-size', 'small');
    expect(screen.getByRole('button', { name: 'Large font size' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('keeps size controls working when browser storage is unavailable', () => {
    const read = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    const write = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    try {
      // Exercise the font control without the header's unrelated tenant storage lookup.
      render(<FontSizeProvider><FontSizeToggle /></FontSizeProvider>);
      fireEvent.click(screen.getByRole('button', { name: 'Large font size' }));
      expect(document.documentElement).toHaveAttribute('data-font-size', 'large');
    } finally {
      read.mockRestore();
      write.mockRestore();
    }
  });

  it('hydrates the signed-out server header before revealing stored-user settings permissions', async () => {
    mocks.roles = [];
    mocks.permissions = [];
    mocks.userAvailable = false;
    const container = document.createElement('div');
    container.innerHTML = renderToString(<Header />);
    document.body.appendChild(container);
    expect(container.querySelector('[aria-label="Open settings"]')).toBeNull();

    // The first browser render can read permissions from localStorage before
    // the current-user query finishes; the server cannot read that storage.
    mocks.roles = ['ProcurementOfficer'];
    mocks.permissions = ['procurement.supplier.manage'];
    const onRecoverableError = vi.fn();
    const root = hydrateRoot(container, <Header />, { onRecoverableError });
    try {
      await waitFor(() => expect(container.querySelector('[aria-label="Open settings"]')).not.toBeNull());
      expect(onRecoverableError).not.toHaveBeenCalled();
    } finally {
      await act(async () => root.unmount());
      container.remove();
    }
  });

  it('hydrates placeholder account text before showing a cached client user', async () => {
    mocks.roles = [];
    mocks.permissions = [];
    mocks.userAvailable = false;
    const container = document.createElement('div');
    container.innerHTML = renderToString(<Header />);
    document.body.appendChild(container);
    mocks.userAvailable = true;
    const onRecoverableError = vi.fn();
    const root = hydrateRoot(container, <Header />, { onRecoverableError });
    try {
      await waitFor(() => expect(container.querySelector('[aria-label="Open account sidebar"]')).toHaveTextContent('Demo User'));
      expect(onRecoverableError).not.toHaveBeenCalled();
    } finally {
      await act(async () => root.unmount());
      container.remove();
    }
  });

  it('keeps search at the left and pins tenant and user content to the full-width right edge', () => {
    const { container } = render(<Header />);

    const headerRow = container.querySelector('header > div');
    expect(headerRow).toHaveClass('w-full', 'min-w-0');
    expect(headerRow).not.toHaveClass('container');

    const search = screen.getByRole('combobox', { name: 'Search across the app' });
    expect(search.closest('.max-w-2xl')).toBeInTheDocument();

    const tenant = screen.getByText('Demo Organization');
    expect(tenant.closest('.ml-auto')).toHaveClass('shrink-0', 'items-center');
    expect(screen.getByRole('link', { name: 'Open settings' })).toHaveAttribute('href', '/settings');
    expect(screen.queryByText(/Session:/)).not.toBeInTheDocument();
  });

  it('moves account actions and theme selection into the avatar sidebar', () => {
    render(<Header />);

    expect(screen.queryByRole('button', { name: 'Theme' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));

    expect(screen.getByRole('complementary', { name: 'Demo User' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Profile Settings' })).toHaveAttribute('href', '/profile');
    expect(screen.getByRole('link', { name: 'Account Settings' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'All Settings' })).toHaveAttribute('href', '/settings');
    expect(screen.getByRole('button', { name: 'Sign Out' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('radio', { name: /Night/ }));
    expect(mocks.setTheme).toHaveBeenCalledWith('dark');

    fireEvent.click(screen.getByRole('button', { name: 'Close account sidebar' }));
    expect(screen.queryByRole('complementary', { name: 'Demo User' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open account sidebar' })).toHaveFocus();
  });

  it('opens a non-modal account panel without a backdrop or body scroll lock and restores focus on Escape', () => {
    render(<Header />);
    const trigger = screen.getByRole('button', { name: 'Open account sidebar' });
    const previousOverflow = document.body.style.overflow;
    trigger.focus();
    fireEvent.click(trigger);

    const panel = screen.getByRole('complementary', { name: 'Demo User' });
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(trigger).toHaveAttribute('aria-controls', panel.id);
    expect(panel).not.toHaveAttribute('aria-modal');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Dismiss account sidebar' })).not.toBeInTheDocument();
    expect(document.body.style.overflow).toBe(previousOverflow);
    expect(screen.getByRole('button', { name: 'Close account sidebar' })).toHaveFocus();

    const notifications = screen.getByRole('button', { name: 'Notifications' });
    notifications.focus();
    expect(notifications).toHaveFocus();
    fireEvent.keyDown(notifications, { key: 'Escape' });

    expect(panel).not.toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveFocus();
    expect(document.body.style.overflow).toBe(previousOverflow);
  });

  it('keeps focus in the selected control when the header rerenders', () => {
    const { rerender } = render(<Header />);
    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));
    const nightMode = screen.getByRole('radio', { name: /Night/ });
    nightMode.focus();

    rerender(<Header className="test-update" />);

    expect(nightMode).toHaveFocus();
    const profileLink = screen.getByRole('link', { name: 'Profile Settings' });
    profileLink.addEventListener('click', event => event.preventDefault(), { once: true });
    fireEvent.click(profileLink);
    expect(screen.queryByRole('complementary', { name: 'Demo User' })).not.toBeInTheDocument();
  });

  it('preserves sign out from the account panel', () => {
    render(<Header />);
    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sign Out' }));

    expect(mocks.logout).toHaveBeenCalledOnce();
    expect(screen.queryByRole('complementary', { name: 'Demo User' })).not.toBeInTheDocument();
  });

  it('hides Settings entry points when the user has no authorized settings item', () => {
    mocks.roles = ['User'];
    mocks.permissions = [];
    render(<Header />);

    expect(screen.queryByRole('link', { name: 'Open settings' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));
    expect(screen.getByRole('link', { name: 'Profile Settings' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Account Settings' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'All Settings' })).not.toBeInTheDocument();
  });
});
