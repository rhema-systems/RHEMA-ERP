import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { SettingsModulePage } from './SettingsModulePage';
import { hrSetupGroups } from '../../config/hr-setup-nav';

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

describe('SettingsModulePage', () => {
  beforeEach(() => {
    mocks.back.mockReset();
    mocks.roles = ['SuperAdmin'];
    mocks.permissions = ['*'];
  });

  it('shows only the selected module children', () => {
    render(<SettingsModulePage sectionKey="modules" moduleKey="inventory" />);

    expect(screen.getByRole('heading', { name: 'Inventory Settings' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Units of Measure' }))
      .toHaveAttribute('href', '/administration/inventory/units-of-measure');
    expect(screen.getByRole('link', { name: 'Units of Measure' }))
      .toHaveClass('min-h-16', 'px-3', 'py-2');
    expect(screen.getByRole('link', { name: 'Warehouses & Locations' }))
      .toHaveAttribute('href', '/administration/inventory/warehouses');
    expect(screen.queryByRole('link', { name: 'Policy Profiles' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Back to previous screen' }));
    expect(mocks.back).toHaveBeenCalledOnce();
  });

  it('searches only within the selected module', () => {
    render(<SettingsModulePage sectionKey="modules" moduleKey="inventory" />);

    fireEvent.change(screen.getByRole('textbox', { name: 'Search Inventory settings' }), {
      target: { value: 'custody' },
    });

    expect(screen.getByRole('link', { name: 'Issue Accounting & Asset Custody' }))
      .toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Units of Measure' })).not.toBeInTheDocument();
  });

  it('bands the Human Resources settings and captions every tile', () => {
    // ⚠ The reported bug: this page captions a tile with its ancestor path, so the twenty-two
    // HR settings that hung directly off the HR node rendered with no subtext at all — and the
    // fifty that did render arrived as one undifferentiated grid with no headings.
    render(<SettingsModulePage sectionKey="modules" moduleKey="human-resources" />);

    // One DOM pass, not one per link: there are eighty-odd tiles and a getByRole apiece rescans
    // the whole tree, which takes the test past its timeout.
    const nav = screen.getByRole('navigation', { name: 'Human Resources settings' });
    const rendered = new Map(
      within(nav)
        .getAllByRole('link')
        .map(link => [
          link.getAttribute('href'),
          link.textContent?.replace(/\s+/g, ' ').trim() ?? '',
        ]),
    );
    const headings = within(nav)
      .getAllByRole('heading')
      .map(heading => heading.textContent);

    for (const group of hrSetupGroups) {
      expect(headings, `${group.title} band`).toContain(group.title);

      for (const link of group.links) {
        expect(rendered.get(link.href), link.title).toBe(
          `${link.title}${link.description}`,
        );
      }
    }
  });

  it('fails closed when the module has no authorized settings', () => {
    mocks.roles = [];
    mocks.permissions = [];

    render(<SettingsModulePage sectionKey="modules" moduleKey="inventory" />);

    expect(screen.getByRole('heading', { name: 'Settings module unavailable' }))
      .toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));
    expect(mocks.back).toHaveBeenCalledOnce();
  });
});
