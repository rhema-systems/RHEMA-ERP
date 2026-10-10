import { render, screen, cleanup } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import React, { type ReactNode } from 'react';

vi.stubGlobal('React', React);

const state = vi.hoisted(() => ({
  path: '',
  permissions: [] as string[],
  roles: [] as string[],
}));
vi.mock('next/navigation', () => ({ usePathname: () => state.path }));
vi.mock('../../components/layout/dashboard-layout', () => ({
  DashboardLayout: ({ children }: { children: ReactNode }) => children,
}));
vi.mock('../../components/auth/auth-guard', () => ({
  AuthGuard: ({
    children,
    requiredPermissions,
    requiredRoles,
    accessMode = 'all',
  }: {
    children: ReactNode;
    requiredPermissions?: string[];
    requiredRoles?: string[];
    accessMode?: 'all' | 'any';
  }) => {
    const checks = [
      ...(requiredPermissions?.length
        ? [requiredPermissions.some((p) => state.permissions.includes(p))]
        : []),
      ...(requiredRoles?.length
        ? [requiredRoles.some((role) => state.roles.includes(role))]
        : []),
    ];
    const allowed =
      checks.length === 0 ||
      (accessMode === 'any' ? checks.some(Boolean) : checks.every(Boolean));
    return allowed ? children : <div>Denied</div>;
  },
}));
import AdministrationLayout from './layout';

describe('QS administration route grants', () => {
  afterEach(() => {
    cleanup();
    state.permissions = [];
    state.roles = [];
  });
  it.each([
    [
      '/administration/project-management/quantity-survey-catalogues',
      'quantity-survey.configuration.read',
    ],
    [
      '/administration/project-management/quantity-survey-config/test-profile',
      'quantity-survey.configuration.read',
    ],
    [
      '/administration/project-management/quantity-survey-rate-library',
      'quantity-survey.workspace.read',
    ],
  ])('admits the module reader at %s', (path, permission) => {
    state.path = path;
    state.permissions = [permission];
    render(<AdministrationLayout>QS workspace</AdministrationLayout>);
    expect(screen.getByText('QS workspace')).toBeTruthy();
  });
  it.each([
    '/administration/project-management/settings',
    '/administration/finance',
    '/administration/project-management/quantity-survey-config-extra',
  ])('does not expand the QS grant into %s', (path) => {
    state.path = path;
    state.permissions = [
      'quantity-survey.configuration.read',
      'quantity-survey.workspace.read',
    ];
    render(<AdministrationLayout>QS workspace</AdministrationLayout>);
    expect(screen.getByText('Denied')).toBeTruthy();
  });

  it.each([
    ['/administration/estate', 'Admin'],
    ['/administration/legal', 'Admin'],
    ['/administration/document-management/access-retention', 'Records Officer'],
    ['/administration/workflow', 'WorkflowAdmin'],
  ])('admits the configured role at %s', (path, role) => {
    state.path = path;
    state.roles = [role];
    render(
      <AdministrationLayout>Administration workspace</AdministrationLayout>
    );
    expect(screen.getByText('Administration workspace')).toBeTruthy();
  });

  it.each([
    '/administration/estate',
    '/administration/legal',
    '/administration/document-management',
    '/administration/workflow',
  ])('denies an ordinary authenticated role at %s', (path) => {
    state.path = path;
    state.roles = ['Employee'];
    render(
      <AdministrationLayout>Administration workspace</AdministrationLayout>
    );
    expect(screen.getByText('Denied')).toBeTruthy();
  });

  it.each([
    'MobilePOS.Store.View',
    'MobilePOS.Store.Manage',
    'MobilePOS.Device.Approve',
    'MobilePOS.Till.Review',
  ])('admits Mobile POS administration with %s', permission => {
    state.path = '/administration/mobile-pos';
    state.permissions = [permission];
    render(<AdministrationLayout>Mobile POS workspace</AdministrationLayout>);
    expect(screen.getByText('Mobile POS workspace')).toBeTruthy();
  });

  it('denies Mobile POS administration without a Mobile POS permission', () => {
    state.path = '/administration/mobile-pos';
    state.permissions = ['settings.read'];
    state.roles = ['Administrator'];
    render(<AdministrationLayout>Mobile POS workspace</AdministrationLayout>);
    expect(screen.getByText('Denied')).toBeTruthy();
  });
});
