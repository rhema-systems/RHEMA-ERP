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
  adminApiService: {
    getRoles: mocks.getRoles,
    getPermissions: mocks.getPermissions,
    createRole: mocks.createRole,
    updateRole: mocks.updateRole,
    deleteRole: mocks.deleteRole,
  },
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
        name: 'users.read',
        displayName: 'View Users',
        description: 'View user accounts and details',
        category: 'User Management',
        isSystemPermission: true,
        createdAt: new Date('2026-08-01T00:00:00Z'),
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
    expect(screen.getByText('Showing 1 of 2 permissions')).toBeInTheDocument();

    fireEvent.change(
      screen.getByRole('textbox', { name: 'Search permissions' }),
      {
        target: { value: 'missing permission' },
      }
    );

    expect(screen.getByText(/No permissions match/)).toBeInTheDocument();
  });
});
