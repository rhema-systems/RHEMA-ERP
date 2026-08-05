import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { Header } from './header';

const mocks = vi.hoisted(() => ({
  logout: vi.fn(),
  setTheme: vi.fn(),
  roles: ['SuperAdmin'],
  permissions: ['*'],
}));

vi.stubGlobal('React', React);

vi.mock('../../hooks/use-auth', () => ({
  useAuth: () => ({
    user: {
      firstName: 'Demo',
      lastName: 'User',
      username: 'demo.user',
      id: 'user-123',
      email: 'demo.user@example.com',
      isActive: true,
      roles: mocks.roles,
      permissions: mocks.permissions,
    },
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
    mocks.logout.mockReset();
    mocks.setTheme.mockReset();
    mocks.roles = ['SuperAdmin'];
    mocks.permissions = ['*'];
  });

  it('keeps search at the left and pins tenant and user content to the full-width right edge', () => {
    const { container } = render(<Header />);

    const headerRow = container.querySelector('header > div');
    expect(headerRow).toHaveClass('w-full', 'min-w-0');
    expect(headerRow).not.toHaveClass('container');

    const search = screen.getByPlaceholderText('Search modules and functions...');
    expect(search.closest('.max-w-sm')).toBeInTheDocument();

    const tenant = screen.getByText('Demo Organization');
    expect(tenant.closest('.ml-auto')).toHaveClass('shrink-0', 'items-center');
    expect(screen.getByRole('link', { name: 'Open settings' })).toHaveAttribute('href', '/settings');
    expect(screen.queryByText(/Session:/)).not.toBeInTheDocument();
  });

  it('moves account actions and theme selection into the avatar sidebar', () => {
    render(<Header />);

    expect(screen.queryByRole('button', { name: 'Theme' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));

    expect(screen.getByRole('dialog', { name: 'Demo User' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Profile Settings' })).toHaveAttribute('href', '/profile');
    expect(screen.getByRole('link', { name: 'Account Settings' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'All Settings' })).toHaveAttribute('href', '/settings');
    expect(screen.getByRole('button', { name: 'Sign Out' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('radio', { name: /Night/ }));
    expect(mocks.setTheme).toHaveBeenCalledWith('dark');

    fireEvent.click(screen.getByRole('button', { name: 'Close account sidebar' }));
    expect(screen.queryByRole('dialog', { name: 'Demo User' })).not.toBeInTheDocument();
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
