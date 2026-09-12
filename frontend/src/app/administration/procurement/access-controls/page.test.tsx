import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { ProcurementAccessReadiness } from '@/types/procurement-access-control';

const mocks = vi.hoisted(() => ({
  readiness: vi.fn(),
  roles: vi.fn(),
  permissions: vi.fn(),
  users: vi.fn(),
  warehouses: vi.fn(),
  locations: vi.fn(),
  assignments: vi.fn(),
  committees: vi.fn(),
  workflows: vi.fn(),
  audit: vi.fn(),
  saveAssignment: vi.fn(),
  updateCommittee: vi.fn(),
  addCommitteeMember: vi.fn(),
  removeCommitteeMember: vi.fn(),
  checkCapability: vi.fn(),
  enforceCapability: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/services/procurement-access-control.service', () => ({
  procurementAccessControlService: {
    readiness: mocks.readiness,
    roles: mocks.roles,
    permissions: mocks.permissions,
    users: mocks.users,
    warehouses: mocks.warehouses,
    locations: mocks.locations,
    assignments: mocks.assignments,
    committees: mocks.committees,
    workflows: mocks.workflows,
    audit: mocks.audit,
    saveAssignment: mocks.saveAssignment,
    updateCommittee: mocks.updateCommittee,
    addCommitteeMember: mocks.addCommitteeMember,
    removeCommitteeMember: mocks.removeCommitteeMember,
    checkCapability: mocks.checkCapability,
    enforceCapability: mocks.enforceCapability,
  },
}));

import ProcurementAccessControlsPage from './page';

const readiness: ProcurementAccessReadiness = {
  requiredRoleCount: 19,
  configuredRoleCount: 17,
  requiredPermissionCount: 39,
  configuredPermissionCount: 38,
  requiredCommitteeCount: 4,
  configuredCommitteeCount: 4,
  readyCommitteeCount: 1,
  requiredWorkflowCount: 4,
  configuredWorkflowCount: 4,
  publishedWorkflowCount: 2,
  activeAssignmentCount: 3,
  internalAuditIsReadOnly: true,
  isReadyForUat: false,
  issues: ['Two required roles are not configured.'],
};

const renderPage = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ProcurementAccessControlsPage />
    </QueryClientProvider>,
  );
};

describe('procurement access administration loading boundary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    for (const query of [
      mocks.roles, mocks.permissions, mocks.users, mocks.warehouses,
      mocks.locations, mocks.assignments, mocks.committees, mocks.workflows,
      mocks.audit,
    ]) query.mockResolvedValue([]);
  });

  it('does not show false zero counts or request protected collections after a 403', async () => {
    mocks.readiness.mockRejectedValue(
      Object.assign(new Error('The procurement access management permission is required.'), { status: 403 }),
    );

    renderPage();

    expect(await screen.findByText('Procurement access administration is not assigned')).toBeInTheDocument();
    expect(screen.queryByText('0/19')).not.toBeInTheDocument();
    expect(screen.queryByText('Shared-control workspace')).not.toBeInTheDocument();
    for (const query of [
      mocks.roles, mocks.permissions, mocks.users, mocks.warehouses,
      mocks.locations, mocks.assignments, mocks.committees, mocks.workflows,
      mocks.audit,
    ]) expect(query).not.toHaveBeenCalled();
  });

  it('loads protected collections and renders server counts after readiness succeeds', async () => {
    mocks.readiness.mockResolvedValue(readiness);

    renderPage();

    expect(await screen.findByText('17/19')).toBeInTheDocument();
    expect(screen.getByText('38/39')).toBeInTheDocument();
    await waitFor(() => expect(mocks.committees).toHaveBeenCalledTimes(1));
    expect(screen.getByText('Shared-control workspace')).toBeInTheDocument();
  });
});
