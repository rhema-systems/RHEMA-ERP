import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  setCurrentTenantCode: vi.fn(),
  getCurrentUser: vi.fn(),
  publicRequest: vi.fn(),
  selectTenant: vi.fn(),
  getStoredUser: vi.fn(),
  logout: vi.fn(),
  completeTransition: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock('../../contexts/TenantContext', () => ({
  useTenant: () => ({ setCurrentTenantCode: mocks.setCurrentTenantCode }),
}));

vi.mock('../../services/api.service', () => ({
  apiService: {
    getCurrentUser: mocks.getCurrentUser,
    publicRequest: mocks.publicRequest,
  },
}));

vi.mock('../../services/tenant', () => ({
  tenantService: { selectTenant: mocks.selectTenant },
}));

vi.mock('../../services/auth', () => ({
  authService: {
    getStoredUser: mocks.getStoredUser,
    logout: mocks.logout,
  },
}));

vi.mock('../../lib/tenant-selection-transition', () => ({
  completeTenantSelectionTransition: mocks.completeTransition,
}));

vi.mock('../../components/ui/confirmation-dialog', () => ({
  ConfirmationDialog: () => null,
}));

import TenantSelectPage from './page';

const accessibleTenants = [
  {
    tenantId: '11111111-1111-1111-1111-111111111111',
    tenantCode: 'TDC',
    tenantName: 'TDC Development Company',
    isDefault: true,
    accessLevel: 'Standard',
  },
  {
    tenantId: '22222222-2222-2222-2222-222222222222',
    tenantCode: 'SECOND',
    tenantName: 'Second Organization',
    isDefault: false,
    accessLevel: 'Standard',
  },
];

const currentUser = {
  id: '33333333-3333-3333-3333-333333333333',
  username: 'tenant.admin',
  email: 'redacted@example.test',
  accessibleTenants,
  isActive: true,
  roles: ['TenantAdmin'],
  permissions: [],
};

const renderPage = (initialUser = currentUser) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, structuralSharing: false },
      mutations: { retry: false },
    },
  });
  queryClient.setQueryData(['currentUser'], initialUser);

  const view = render(
    <QueryClientProvider client={queryClient}>
      <React.StrictMode><TenantSelectPage /></React.StrictMode>
    </QueryClientProvider>
  );
  return { ...view, queryClient };
};

describe('tenant selection transition', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    window.history.replaceState({}, '', '/tenant-select');
    mocks.getCurrentUser.mockResolvedValue(currentUser);
    mocks.publicRequest.mockResolvedValue({});
    mocks.selectTenant.mockResolvedValue(undefined);
    mocks.getStoredUser.mockReturnValue(currentUser);
    mocks.logout.mockResolvedValue(undefined);
    mocks.completeTransition.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('hands a successful selection to the cache-safe hard-navigation boundary immediately', async () => {
    renderPage();

    fireEvent.click(
      await screen.findByRole('button', { name: /TDC Development Company/i })
    );

    await waitFor(() => {
      expect(mocks.selectTenant).toHaveBeenCalledWith('TDC', false);
    });
    await waitFor(() => {
      expect(mocks.completeTransition).toHaveBeenCalledWith(
        expect.objectContaining({
          tenantCode: 'TDC',
          target: '/dashboard',
          setCurrentTenantCode: mocks.setCurrentTenantCode,
          cancelQueries: expect.any(Function),
          removeQueries: expect.any(Function),
        })
      );
    });
  });

  it('restores the chooser with an inline error when selection fails', async () => {
    mocks.selectTenant.mockRejectedValue(
      new Error('The organization could not be selected.')
    );
    renderPage();

    fireEvent.click(
      await screen.findByRole('button', { name: /TDC Development Company/i })
    );

    expect(
      await screen.findByRole('alert')
    ).toHaveTextContent('The organization could not be selected.');
    expect(
      screen.getByRole('button', { name: /TDC Development Company/i })
    ).toBeEnabled();
    expect(mocks.completeTransition).not.toHaveBeenCalled();
  });

  it('selects a single tenant only once across repeated effects and refreshed tenant arrays', async () => {
    const singleTenantUser = { ...currentUser, accessibleTenants: accessibleTenants.slice(0, 1) };
    mocks.getCurrentUser.mockResolvedValue(singleTenantUser);
    let finishSelection!: () => void;
    mocks.selectTenant.mockReturnValue(new Promise<void>((resolve) => { finishSelection = resolve; }));
    const { queryClient } = renderPage(singleTenantUser);

    await waitFor(() => expect(mocks.selectTenant).toHaveBeenCalledTimes(1));
    await act(async () => {
      queryClient.setQueryData(['currentUser'], {
        ...singleTenantUser,
        accessibleTenants: singleTenantUser.accessibleTenants.map((tenant) => ({ ...tenant })),
      });
    });
    expect(mocks.selectTenant).toHaveBeenCalledTimes(1);

    await act(async () => { finishSelection(); });
    await waitFor(() => expect(mocks.completeTransition).toHaveBeenCalledTimes(1));
    await act(async () => { queryClient.removeQueries(); });
    expect(screen.getByText('Preparing your workspace')).toBeInTheDocument();
    expect(mocks.selectTenant).toHaveBeenCalledTimes(1);
  });

  it('does not automatically retry a failed single-tenant selection, but allows manual retry', async () => {
    const singleTenantUser = { ...currentUser, accessibleTenants: accessibleTenants.slice(0, 1) };
    mocks.getCurrentUser.mockResolvedValue(singleTenantUser);
    mocks.selectTenant.mockRejectedValueOnce(new Error('Please try selecting again.'));
    const { queryClient } = renderPage(singleTenantUser);

    expect(await screen.findByRole('alert')).toHaveTextContent('Please try selecting again.');
    await act(async () => {
      queryClient.setQueryData(['currentUser'], { ...singleTenantUser });
    });
    expect(mocks.selectTenant).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: /TDC Development Company/i }));
    await waitFor(() => expect(mocks.selectTenant).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(mocks.completeTransition).toHaveBeenCalledTimes(1));
  });

  it('requires a choice when several organizations are available on a normal host', async () => {
    renderPage();
    expect(await screen.findByRole('button', { name: /Second Organization/i })).toBeEnabled();
    expect(mocks.selectTenant).not.toHaveBeenCalled();
  });
});
