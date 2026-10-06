import React, { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DashboardLayout } from './dashboard-layout';

vi.stubGlobal('React', React);

vi.mock('./sidebar', () => ({
  Sidebar: () => <nav aria-label="Main navigation" />,
  navigationItems: [],
  settingsNavigationItems: [],
  filterNavigationByAccess: (items: unknown[]) => items,
}));

vi.mock('../../hooks/use-auth', () => ({
  useAuth: () => ({
    user: { firstName: 'Demo', lastName: 'User', username: 'demo.user', roles: ['User'] },
    logout: vi.fn(),
    hasAnyRole: () => false,
    hasAnyPermission: () => false,
  }),
}));

vi.mock('../../services/auth', () => ({
  authService: { isAuthenticated: () => true },
}));

vi.mock('../../contexts/TenantContext', () => ({
  useTenant: () => ({ currentTenant: null, setCurrentTenantCode: vi.fn() }),
}));

vi.mock('../../contexts/session-timeout-context', () => ({
  SessionTimeoutProvider: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('../../contexts/ThemeContext', () => ({
  useTheme: () => ({ theme: 'light', actualTheme: 'light', setTheme: vi.fn() }),
}));

vi.mock('../ClientOnly', () => ({
  ClientOnly: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('../notifications/HeaderNotificationBell', () => ({
  default: () => <button type="button">Notifications</button>,
}));

function PageContent() {
  const [count, setCount] = useState(0);
  return <button onClick={() => setCount(value => value + 1)}>Page action {count}</button>;
}

describe('Dashboard account panel integration', () => {
  it('docks the panel beside a shrinkable main region and keeps page content interactive and mounted', () => {
    render(<DashboardLayout><PageContent /></DashboardLayout>);
    expect(screen.getByTestId('environment-banner')).toHaveTextContent('Unknown Environment');
    fireEvent.click(screen.getByRole('button', { name: 'Page action 0' }));
    fireEvent.click(screen.getByRole('button', { name: 'Open account sidebar' }));

    const main = screen.getByRole('main');
    const panel = screen.getByRole('complementary', { name: 'Demo User' });
    expect(panel).toHaveTextContent('System context');
    expect(panel).toHaveTextContent('Unknown Environment');
    expect(panel.parentElement?.parentElement).toBe(main.parentElement);
    expect(panel.parentElement).toHaveClass('contents');
    expect(main).toHaveClass('min-w-0', 'min-h-0', 'flex-1', 'overflow-auto');
    expect(main.parentElement).toHaveClass('flex-col', 'lg:flex-row');
    expect(panel).toHaveClass('lg:w-80', 'shrink-0', 'order-first', 'lg:order-last');
    expect(panel).not.toHaveClass('fixed', 'absolute');

    fireEvent.click(screen.getByRole('button', { name: 'Page action 1' }));
    expect(panel).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Close account sidebar' }));

    expect(panel).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Page action 2' })).toBeInTheDocument();
  });
});
