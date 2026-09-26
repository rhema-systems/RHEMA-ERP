import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { Role, User } from '../../../../services/admin-api.service';

const mocks = vi.hoisted(() => ({
  getUsers: vi.fn(),
  getRoles: vi.fn(),
  getTenants: vi.fn(),
  createUser: vi.fn(),
  updateUser: vi.fn(),
  deleteUser: vi.fn(),
  resetUserPassword: vi.fn(),
  toast: vi.fn(),
}));

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

vi.stubGlobal('ResizeObserver', ResizeObserverMock);

vi.mock('../../../../contexts/TenantContext', () => ({
  useTenant: () => ({ currentTenant: { id: 'tenant-qs-test' } }),
}));

vi.mock('../../../../services/admin-api.service', () => ({
  adminApiService: {
    getUsers: mocks.getUsers,
    getRoles: mocks.getRoles,
    getTenants: mocks.getTenants,
    createUser: mocks.createUser,
    updateUser: mocks.updateUser,
    deleteUser: mocks.deleteUser,
    resetUserPassword: mocks.resetUserPassword,
  },
}));

vi.mock('../../../../hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('../../../../components/admin/data-table', () => ({
  DataTable: ({ data, onAdd, onEdit }: {
    data: User[];
    onAdd: () => void;
    onEdit: (user: User) => void;
  }) => (
    <div>
      <button type="button" onClick={onAdd}>Add user</button>
      {data.map((user) => (
        <button key={user.id} type="button" onClick={() => onEdit(user)}>
          Edit {user.username}
        </button>
      ))}
    </div>
  ),
}));

vi.mock('../../../../components/admin/BulkUserActions', () => ({
  BulkUserActions: () => null,
}));

vi.mock('../../../../components/admin/UserProfile', () => ({
  UserProfile: () => null,
}));

vi.mock('../../../../components/ui/client-only', () => ({
  ClientOnly: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('../../../../components/ui/phone-input', () => ({
  default: ({ value, onChange }: { value: string; onChange: (value: string) => void }) => (
    <input
      aria-label="Phone Number"
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  ),
}));

import UsersPage from './page';

const user: User = {
  id: 'user-1',
  username: 'rolekeeper',
  email: 'rolekeeper@example.test',
  firstName: 'Role',
  lastName: 'Keeper',
  phoneNumber: '+233241234567',
  roles: ['Procurement User', 'TDC_HEAD_OF_PROCUREMENT'],
  isActive: true,
  createdAt: new Date('2026-09-01T00:00:00Z'),
};

const role = (id: string, name: string): Role => ({
  id,
  name,
  description: `${name} description`,
  permissions: [],
  isSystemRole: false,
  createdAt: new Date('2026-09-01T00:00:00Z'),
  updatedAt: new Date('2026-09-01T00:00:00Z'),
});

const availableRoles = [
  role('role-1', 'Procurement User'),
  role('role-2', 'TDC_HEAD_OF_PROCUREMENT'),
  role('role-3', 'Approver'),
];

const renderPage = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <UsersPage />
    </QueryClientProvider>,
  );
};

describe('Security user role editing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getUsers.mockResolvedValue([user]);
    mocks.getRoles.mockResolvedValue(availableRoles);
    mocks.getTenants.mockResolvedValue([]);
    mocks.updateUser.mockResolvedValue(user);
  });

  it('requires an entered password when creating a user and sends the selected roles', async () => {
    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Add user' }));
    fireEvent.change(screen.getByLabelText('Username *'), { target: { value: 'qs-preparer-test' } });
    fireEvent.change(screen.getByLabelText('Email *'), { target: { value: 'qs-preparer@example.test' } });
    fireEvent.change(screen.getByLabelText('Phone Number'), { target: { value: '' } });
    fireEvent.change(screen.getByLabelText('Password *'), { target: { value: 'short' } });
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Assign Procurement User' }));
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }));
    expect(await screen.findByText('Password must be at least 8 characters')).toBeInTheDocument();
    expect(mocks.createUser).not.toHaveBeenCalled();

    const testPassword = 'Only-a-test-value-93!';
    expect(screen.getByLabelText('Password *')).toHaveAttribute('type', 'password');
    fireEvent.change(screen.getByLabelText('Password *'), { target: { value: testPassword } });
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }));
    await waitFor(() => expect(mocks.createUser).toHaveBeenCalledWith(expect.objectContaining({
      username: 'qs-preparer-test', password: testPassword, roles: ['Procurement User'], tenantId: 'tenant-qs-test',
    })));
  });

  it('preserves every existing role when another role is added', async () => {
    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: 'Edit rolekeeper' }));

    expect(await screen.findByRole('checkbox', { name: 'Assign Procurement User' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Assign TDC_HEAD_OF_PROCUREMENT' })).toBeChecked();
    expect(screen.getByText('2 selected')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('checkbox', { name: 'Assign Approver' }));
    expect(screen.getByText('3 selected')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Update User' }));

    await waitFor(() => expect(mocks.updateUser).toHaveBeenCalledWith(
      user.id,
      expect.objectContaining({
        roles: ['Procurement User', 'TDC_HEAD_OF_PROCUREMENT', 'Approver'],
      }),
    ));
  });

  it('supports removing one role while retaining and adding other roles', async () => {
    mocks.getRoles.mockResolvedValue([
      role('role-1', 'Procurement User'),
      role('role-3', 'Approver'),
    ]);

    renderPage();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit rolekeeper' }));

    const retainedRole = await screen.findByRole('checkbox', { name: 'Assign TDC_HEAD_OF_PROCUREMENT' });
    expect(retainedRole).toBeChecked();
    expect(screen.getByText('Currently assigned role')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('checkbox', { name: 'Assign Procurement User' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Assign Approver' }));
    fireEvent.click(screen.getByRole('button', { name: 'Update User' }));

    await waitFor(() => expect(mocks.updateUser).toHaveBeenCalledWith(
      user.id,
      expect.objectContaining({
        roles: ['TDC_HEAD_OF_PROCUREMENT', 'Approver'],
      }),
    ));
  });
});
