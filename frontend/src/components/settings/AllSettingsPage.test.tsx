import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AllSettingsPage, buildSettingsSections, filterSettingsByAccess } from './AllSettingsPage';
import { settingsNavigationItems } from '../layout/sidebar';

const mocks = vi.hoisted(() => ({
  back: vi.fn(),
  roles: ['SuperAdmin'],
  permissions: ['*'],
}));

vi.stubGlobal('React', React);

vi.mock('next/navigation', () => ({
  useRouter: () => ({ back: mocks.back }),
}));

vi.mock('../../contexts/TenantContext', () => ({
  useTenant: () => ({ currentTenant: { name: 'Demo Organization' } }),
}));

vi.mock('../../hooks/use-auth', () => ({
  useAuth: () => ({
    hasAnyRole: (roles: string[]) => roles.some(role =>
      mocks.roles.some(assigned => assigned.toLowerCase() === role.toLowerCase())),
    hasAnyPermission: (permissions: string[]) =>
      mocks.roles.some(role => ['superadmin', 'tenantadmin'].includes(role.toLowerCase())) ||
      mocks.permissions.includes('*') ||
      permissions.some(permission =>
        mocks.permissions.some(assigned => assigned.toLowerCase() === permission.toLowerCase())),
  }),
}));

describe('AllSettingsPage', () => {
  beforeEach(() => {
    mocks.back.mockReset();
    mocks.roles = ['SuperAdmin'];
    mocks.permissions = ['*'];
  });

  it('builds module and administration categories from shared navigation metadata', () => {
    const sections = buildSettingsSections(settingsNavigationItems);
    const modules = sections.find(section => section.title === 'Module Settings');
    const administration = sections.find(section => section.title === 'Administration & Governance');

    expect(modules?.cards.map(card => card.title)).toContain('Procurement');
    expect(modules?.cards.map(card => card.title)).toContain('Inventory');
    const finance = modules?.cards.find(card => card.title === 'Finance');
    expect(finance?.links.filter(link => link.href === '/finance/accounts')).toHaveLength(1);
    expect(finance?.links.map(link => link.href)).not.toContain('/administration/finance/accounts');
    expect(finance?.links.filter(link => link.group === 'Unit Accounting').map(link => link.title)).toEqual([
      'Unit Accounts',
      'Unit Types',
      'Ratio Definitions',
    ]);
    expect(finance?.links.filter(link => link.group === 'Fiscal & Close').map(link => link.title)).toEqual([
      'Fiscal Calendar Setup',
      'Fiscal Years',
      'Fiscal Periods',
      'Close Templates',
    ]);
    expect(modules?.cards.find(card => card.title === 'Inventory')?.links.map(link => link.title)).toEqual([
      'Units of Measure',
      'UoM Schedules',
      'Warehouses & Locations',
      'Issue Accounting & Asset Custody',
    ]);
    expect(modules?.cards.find(card => card.title === 'Inventory')?.links.map(link => link.title))
      .not.toContain('Inventory Items');

    expect(administration?.cards.map(card => card.title)).toEqual(expect.arrayContaining([
      'Workflow & Automation',
      'Security & Identity',
      'Tenant & Organization',
      'Audit & Monitoring',
      'Notifications & Communications',
    ]));
    expect(administration?.cards.find(card => card.title === 'General Administration')?.links)
      .toEqual(expect.arrayContaining([
        expect.objectContaining({
          title: 'Document Templates',
          href: '/administration/document-management/document-templates',
        }),
      ]));
  });

  it('searches setting children and closes back to the previous workspace', () => {
    render(<AllSettingsPage />);

    expect(screen.getByRole('heading', { name: 'All Settings' })).toBeInTheDocument();
    expect(screen.getByText('Demo Organization')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Notification Center' })).toHaveAttribute('href', '/notifications#center');

    fireEvent.change(screen.getByRole('textbox', { name: 'Search settings' }), {
      target: { value: 'audit logs' },
    });

    expect(screen.getByRole('link', { name: 'Audit Logs' })).toHaveAttribute('href', '/administration/audit-logs');
    expect(screen.queryByRole('link', { name: 'Finance Settings' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Close Settings/ }));
    expect(mocks.back).toHaveBeenCalledOnce();
  });

  it('loads every category expanded and toggles each card from its header', () => {
    render(<AllSettingsPage />);

    expect(screen.getByTestId('modules-settings-masonry')).toHaveClass(
      'columns-1',
      'md:columns-2',
      'xl:columns-4',
      '2xl:columns-5',
    );

    const collapseInventory = screen.getByRole('button', { name: 'Collapse Inventory settings' });
    expect(collapseInventory.closest('article')).toHaveClass('break-inside-avoid');
    expect(collapseInventory).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'Units of Measure' })).toBeInTheDocument();

    fireEvent.click(collapseInventory);
    const expandInventory = screen.getByRole('button', { name: 'Expand Inventory settings' });
    expect(expandInventory).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'Units of Measure' })).not.toBeInTheDocument();

    fireEvent.click(expandInventory);
    expect(screen.getByRole('button', { name: 'Collapse Inventory settings' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'Units of Measure' })).toBeInTheDocument();
  });

  it('derives module visibility from authorized children and removes inaccessible siblings', () => {
    const allowed = filterSettingsByAccess(
      settingsNavigationItems,
      roles => roles.some(role => role === 'TDC_STORES_MANAGER'),
      permissions => permissions.includes('procurement.inventory.master-data.manage'),
    );
    const sections = buildSettingsSections(allowed);
    const modules = sections.find(section => section.key === 'modules');

    expect(modules?.cards.map(card => card.title)).toEqual(['Inventory']);
    expect(modules?.cards[0].links.map(link => link.title)).toEqual([
      'Units of Measure',
      'UoM Schedules',
      'Warehouses & Locations',
      'Issue Accounting & Asset Custody',
    ]);
    expect(sections.flatMap(section => section.cards).flatMap(card => card.links).map(link => link.title))
      .not.toContain('Policy Profiles');
  });

  it('filters the catalogue before search so inaccessible settings never appear in results', () => {
    mocks.roles = [];
    mocks.permissions = ['procurement.access.manage'];
    render(<AllSettingsPage />);

    expect(screen.getByRole('link', { name: 'Scopes & Committees' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Policy Profiles' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Inventory Items' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Notification Center' })).not.toBeInTheDocument();

    fireEvent.change(screen.getByRole('textbox', { name: 'Search settings' }), {
      target: { value: 'policy profiles' },
    });

    expect(screen.queryByRole('link', { name: 'Policy Profiles' })).not.toBeInTheDocument();
    expect(screen.getByText('No settings match “policy profiles”.')).toBeInTheDocument();

    fireEvent.change(screen.getByRole('textbox', { name: 'Search settings' }), {
      target: { value: 'committees' },
    });

    expect(screen.getByRole('link', { name: 'Scopes & Committees' })).toHaveAttribute(
      'href',
      '/administration/procurement/access-controls',
    );
  });

  it('exposes only the Civil Engineering policy to a configuration reader', () => {
    const allowed = filterSettingsByAccess(
      settingsNavigationItems,
      () => false,
      permissions => permissions.includes('civil-engineering.configuration.read'),
    );
    const links = buildSettingsSections(allowed)
      .flatMap(section => section.cards)
      .flatMap(card => card.links);

    expect(links).toEqual([
      expect.objectContaining({
        title: 'Civil Engineering Policy',
        href: '/administration/project-management/civil-engineering-config',
      }),
    ]);
  });

  it('fails closed for users with no settings role or permission', () => {
    mocks.roles = [];
    mocks.permissions = [];
    render(<AllSettingsPage />);

    expect(screen.queryByTestId('modules-settings-masonry')).not.toBeInTheDocument();
    expect(screen.queryByTestId('administration-settings-masonry')).not.toBeInTheDocument();
    expect(screen.getByText('No settings are available for your current roles and permissions.')).toBeInTheDocument();
  });
});
