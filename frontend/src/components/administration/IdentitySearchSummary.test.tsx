import React from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IdentitySearchSummary } from './IdentitySearchSummary';

const mocks = vi.hoisted(() => ({ get: vi.fn(), tenant: 'ONE', allowed: true }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'identity-1' }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: 'admin-1' }, hasAnyRole: () => mocks.allowed }) }));
vi.mock('@/contexts/TenantContext', () => ({ useTenant: () => ({ currentTenantCode: mocks.tenant }) }));
vi.mock('@/services/api.service', () => ({ apiService: { get: mocks.get } }));

describe('Read-only identity search destinations', () => {
  beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.tenant = 'ONE'; mocks.allowed = true; });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it.each([
    ['users', '/administration/user-tenant-mappings/current/users/identity-1'],
    ['roles', '/Role/summaries/identity-1'],
  ] as const)('reads %s only through the minimal authorized summary endpoint', async (kind, endpoint) => {
    mocks.get.mockResolvedValue({ id: 'identity-1', name: 'Finance', status: 'Active' });
    render(<IdentitySearchSummary kind={kind} />);
    expect(await screen.findByText('Finance')).toBeInTheDocument();
    expect(mocks.get).toHaveBeenCalledWith(endpoint);
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('does not request identity data without the owner read role', () => {
    mocks.allowed = false;
    render(<IdentitySearchSummary kind="users" />);
    expect(screen.getByRole('alert')).toHaveTextContent('permission');
    expect(mocks.get).not.toHaveBeenCalled();
  });

  it('shows owner denial without displaying identity data', async () => {
    mocks.get.mockRejectedValue(new Error('Not found in current tenant.'));
    render(<IdentitySearchSummary kind="users" />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Not found in current tenant.');
  });

  it('discards a previous tenant response after the active tenant changes', async () => {
    let finish!: (value: { id: string; name: string; status: string }) => void;
    mocks.get.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }))
      .mockResolvedValue({ id: 'identity-1', name: 'Second tenant user', status: 'Active' });
    const view = render(<IdentitySearchSummary kind="users" />);
    await waitFor(() => expect(mocks.get).toHaveBeenCalledTimes(1));
    mocks.tenant = 'TWO';
    view.rerender(<IdentitySearchSummary kind="users" />);
    expect(await screen.findByText('Second tenant user')).toBeInTheDocument();
    await act(async () => finish({ id: 'identity-1', name: 'First tenant user', status: 'Active' }));
    expect(screen.queryByText('First tenant user')).not.toBeInTheDocument();
  });
});
