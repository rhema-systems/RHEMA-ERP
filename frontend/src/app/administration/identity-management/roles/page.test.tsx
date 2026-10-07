import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getRoles: vi.fn(),
  getPermissions: vi.fn(),
  createRole: vi.fn(),
  updateRole: vi.fn(),
  deleteRole: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('../../../../services/admin-api.service', () => ({
  getAdminProblemMessage: (_error: unknown, fallback: string) => fallback,
  adminApiService: {
    getRoles: mocks.getRoles,
    getPermissions: mocks.getPermissions,
    createRole: mocks.createRole,
    updateRole: mocks.updateRole,
    deleteRole: mocks.deleteRole,
  },
}));

vi.mock('../../../../hooks/use-auth', () => ({
  useAuth: () => ({ hasRole: () => true }),
}));

vi.mock('../../../../hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

import RolesPage from './page';

const renderPage = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <RolesPage />
    </QueryClientProvider>
  );
};

describe('role permission search', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      }
    );
    mocks.getRoles.mockResolvedValue([
      {
        id: 'role-1',
        name: 'TDC_PROCUREMENT_OFFICER',
        description: 'Procurement maker',
        permissions: ['procurement.sourcing.manage'],
        isSystemRole: true,
        createdAt: new Date('2026-08-01T00:00:00Z'),
        updatedAt: new Date('2026-08-01T00:00:00Z'),
      },
    ]);
    mocks.getPermissions.mockResolvedValue([
      {
        id: 'permission-1',
        name: 'procurement.sourcing.manage',
        displayName: 'Manage Procurement Sourcing',
        description: 'Create and administer sourcing events',
        category: 'Procurement',
        isSystemPermission: true,
        createdAt: new Date('2026-08-01T00:00:00Z'),
      },
      {
        id: 'permission-2',
        name: 'procurement.contracts.approve',
        displayName: 'Approve Procurement Contracts',
        description: 'Approve procurement contracts',
        category: 'Procurement',
        isSystemPermission: true,
        createdAt: new Date('2026-08-01T00:00:00Z'),
      },
      {
        id: 'permission-3',
        name: 'users.read',
        displayName: 'View Users',
        description: 'View user accounts and details',
        category: 'User Management',
        isSystemPermission: true,
        createdAt: new Date('2026-08-01T00:00:00Z'),
      },
      {
        id: 'permission-4',
        name: 'crm.read',
        displayName: 'View CRM',
        description: 'View CRM accounts, leads, opportunities, activities, and opportunity stages.',
        category: 'CRM',
        isSystemPermission: true,
        createdAt: new Date('2026-10-07T00:00:00Z'),
      },
    ]);
  });

  it('filters permission checkboxes by friendly name, code, description or category', async () => {
    renderPage();

    fireEvent.click(await screen.findByTitle('Edit role permissions'));

    expect(
      await screen.findByText('Manage Procurement Sourcing')
    ).toBeInTheDocument();
    expect(screen.getByText('View Users')).toBeInTheDocument();

    fireEvent.change(
      screen.getByRole('textbox', { name: 'Search permissions' }),
      {
        target: { value: 'sourcing' },
      }
    );

    expect(screen.getByText('Manage Procurement Sourcing')).toBeInTheDocument();
    expect(screen.queryByText('View Users')).not.toBeInTheDocument();
    expect(screen.getByText('Showing 1 of 4 permissions')).toBeInTheDocument();

    fireEvent.change(
      screen.getByRole('textbox', { name: 'Search permissions' }),
      {
        target: { value: 'missing permission' },
      }
    );

    expect(screen.getByText(/No permissions match/)).toBeInTheDocument();
  });

  it('surfaces server-registered CRM permissions for assignment to any role', async () => {
    renderPage();

    fireEvent.click(await screen.findByTitle('Edit role permissions'));

    const crmTrigger = await screen.findByRole('button', {
      name: /CRM 0 of 1 selected/,
    });
    expect(crmTrigger).toHaveAttribute('aria-expanded', 'true');

    const viewCrm = screen.getByRole('checkbox', { name: 'View CRM' });
    expect(viewCrm).not.toBeChecked();

    fireEvent.click(viewCrm);
    expect(viewCrm).toBeChecked();
  });

  it('selects a whole module and lets the module group collapse', async () => {
    renderPage();

    fireEvent.click(await screen.findByTitle('Edit role permissions'));

    const moduleTrigger = await screen.findByRole('button', {
      name: /Procurement 1 of 2 selected/,
    });
    expect(moduleTrigger).toHaveAttribute('aria-expanded', 'true');

    fireEvent.click(screen.getByRole('button', { name: 'Collapse all permission modules' }));
    expect(moduleTrigger).toHaveAttribute('aria-expanded', 'false');

    fireEvent.click(screen.getByRole('button', { name: 'Expand all permission modules' }));
    expect(moduleTrigger).toHaveAttribute('aria-expanded', 'true');

    const selectAll = screen.getByRole('checkbox', {
      name: 'Select all Procurement permissions',
    });
    expect(selectAll).toBePartiallyChecked();

    fireEvent.click(selectAll);

    expect(screen.getByRole('checkbox', { name: 'Manage Procurement Sourcing' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Approve Procurement Contracts' })).toBeChecked();
    expect(selectAll).toBeChecked();

    fireEvent.click(moduleTrigger);
    expect(moduleTrigger).toHaveAttribute('aria-expanded', 'false');

    fireEvent.click(moduleTrigger);
    expect(moduleTrigger).toHaveAttribute('aria-expanded', 'true');
  });

  it('shows the complete server-supplied Sales permission catalogue', async () => {
    mocks.getPermissions.mockResolvedValue([
      ['sales.access', 'Access Sales'],
      ['sales.read', 'View Sales'],
      ['sales.manage', 'Manage Sales'],
      ['sales.approve', 'Approve Sales'],
      ['sales.configure', 'Configure Sales'],
      ['sales.reports.read', 'View Sales Reports'],
    ].map(([name, displayName], index) => ({
      id: `sales-permission-${index}`,
      name,
      displayName,
      description: `${displayName} permission`,
      category: 'Sales',
      isSystemPermission: true,
      createdAt: new Date('2026-10-07T00:00:00Z'),
    })));

    renderPage();
    fireEvent.click(await screen.findByTitle('Edit role permissions'));

    expect(await screen.findByRole('button', { name: /Sales 0 of 6 selected/ }))
      .toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Access Sales' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'View Sales' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Manage Sales' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Approve Sales' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Configure Sales' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'View Sales Reports' })).toBeInTheDocument();
  });
});
