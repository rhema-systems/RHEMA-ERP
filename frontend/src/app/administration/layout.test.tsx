import { render, screen, cleanup } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import React, { type ReactNode } from 'react';

vi.stubGlobal('React', React);

const state = vi.hoisted(() => ({ path: '', permissions: [] as string[] }));
vi.mock('next/navigation', () => ({ usePathname: () => state.path }));
vi.mock('../../components/layout/dashboard-layout', () => ({ DashboardLayout: ({ children }: { children: ReactNode }) => children }));
vi.mock('../../components/auth/auth-guard', () => ({ AuthGuard: ({ children, requiredPermissions }: { children: ReactNode; requiredPermissions?: string[] }) =>
  !requiredPermissions?.length || requiredPermissions.some(p => state.permissions.includes(p)) ? children : <div>Denied</div> }));
import AdministrationLayout from './layout';

describe('QS administration route grants', () => {
  afterEach(cleanup);
  it.each([
    ['/administration/project-management/quantity-survey-catalogues', 'quantity-survey.configuration.read'],
    ['/administration/project-management/quantity-survey-config/test-profile', 'quantity-survey.configuration.read'],
    ['/administration/project-management/quantity-survey-rate-library', 'quantity-survey.workspace.read'],
  ])('admits the module reader at %s', (path, permission) => {
    state.path = path;
    state.permissions = [permission];
    render(<AdministrationLayout>QS workspace</AdministrationLayout>);
    expect(screen.getByText('QS workspace')).toBeTruthy();
  });
  it.each(['/administration/project-management/settings', '/administration/finance', '/administration/project-management/quantity-survey-config-extra'])(
    'does not expand the QS grant into %s', path => {
      state.path = path;
      state.permissions = ['quantity-survey.configuration.read', 'quantity-survey.workspace.read'];
      render(<AdministrationLayout>QS workspace</AdministrationLayout>);
      expect(screen.getByText('Denied')).toBeTruthy();
    });
});
