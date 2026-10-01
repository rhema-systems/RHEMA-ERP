import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ProcedureCaseWorkspace } from './ProcedureCaseWorkspace';

const mocks = vi.hoisted(() => ({
  listCasesPage: vi.fn(),
  getLevels: vi.fn(),
  getUnits: vi.fn(),
  getManagedAssets: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/estate/facilities/EstateFacilityComplaint',
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/procedure-case.service', () => ({
  procedureCaseService: { listCasesPage: mocks.listCasesPage },
}));
vi.mock('@/services/hr/organization-level.service', () => ({
  organizationLevelService: { getAll: mocks.getLevels },
}));
vi.mock('@/services/hr/organization-unit.service', () => ({
  organizationUnitService: { getSummary: mocks.getUnits },
}));
vi.mock('@/services/estate-land-management.service', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/services/estate-land-management.service')>(),
  estateLandManagementService: { getManagedAssets: mocks.getManagedAssets },
}));

describe('Facilities complaint register', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.listCasesPage.mockResolvedValue({ items: [], totalCount: 0, totalPages: 1 });
    mocks.getLevels.mockResolvedValue([{ id: 'level-1', code: 'DEPT', name: 'Department', isActive: true }]);
    mocks.getUnits.mockResolvedValue([{ id: 'unit-1', name: 'Facilities', code: 'FAC', organizationLevelId: 'level-1', isActive: true }]);
    mocks.getManagedAssets.mockResolvedValue([]);
  });
  afterEach(cleanup);

  it('keeps the table compact and opens complaint intake in a dialog', async () => {
    render(<ProcedureCaseWorkspace module="Facilities" entityType="EstateFacilityComplaint"
      defaultTitle="Complaint Management" workspaceType="Case Workflow" intakeFields={[
        { key: 'complaintCategory', label: 'Complaint category', type: 'text' },
        { key: 'priority', label: 'Priority', type: 'select', options: ['Low', 'Normal', 'High'] },
        { key: 'complaintDescription', label: 'Complaint description', type: 'textarea' },
      ]} />);

    await waitFor(() => expect(mocks.listCasesPage).toHaveBeenCalled());
    expect(screen.getByText('Cases')).toBeInTheDocument();
    expect(screen.queryByLabelText('Complaint category')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Create case' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByLabelText('Complaint category')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Complaint description')).toBeInTheDocument();
    expect(within(dialog).getByRole('combobox', { name: 'Property' })).toBeInTheDocument();
    expect(screen.getAllByLabelText('Complaint category')).toHaveLength(1);
  });
});
