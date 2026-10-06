import React from 'react';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ProcedureCaseWorkspace } from './ProcedureCaseWorkspace';

const mocks = vi.hoisted(() => ({
  listCasesPage: vi.fn(),
  getCase: vi.fn(),
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
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));
vi.mock('@/services/procedure-case.service', () => ({
  procedureCaseService: {
    listCasesPage: mocks.listCasesPage,
    getCase: mocks.getCase,
  },
}));
vi.mock('@/components/workflow/WorkflowApprovalHistoryPanel', () => ({
  WorkflowApprovalHistoryPanel: ({
    entityType,
    entityId,
  }: {
    entityType: string;
    entityId: string;
  }) => (
    <div>
      Workflow trail for {entityType} {entityId}
    </div>
  ),
}));
vi.mock('@/services/hr/organization-level.service', () => ({
  organizationLevelService: { getAll: mocks.getLevels },
}));
vi.mock('@/services/hr/organization-unit.service', () => ({
  organizationUnitService: { getSummary: mocks.getUnits },
}));
vi.mock(
  '@/services/estate-land-management.service',
  async (importOriginal) => ({
    ...(await importOriginal<
      typeof import('@/services/estate-land-management.service')
    >()),
    estateLandManagementService: { getManagedAssets: mocks.getManagedAssets },
  })
);

describe('Facilities complaint register', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.listCasesPage.mockResolvedValue({
      items: [],
      totalCount: 0,
      totalPages: 1,
    });
    mocks.getCase.mockResolvedValue(null);
    mocks.getLevels.mockResolvedValue([
      { id: 'level-1', code: 'DEPT', name: 'Department', isActive: true },
    ]);
    mocks.getUnits.mockResolvedValue([
      {
        id: 'unit-1',
        name: 'Facilities',
        code: 'FAC',
        organizationLevelId: 'level-1',
        isActive: true,
      },
    ]);
    mocks.getManagedAssets.mockResolvedValue([]);
  });
  afterEach(cleanup);

  it('keeps the table compact and opens complaint intake in a dialog', async () => {
    render(
      <ProcedureCaseWorkspace
        module="Facilities"
        entityType="EstateFacilityComplaint"
        defaultTitle="Complaint Management"
        workspaceType="Case Workflow"
        intakeFields={[
          {
            key: 'complaintCategory',
            label: 'Complaint category',
            type: 'text',
          },
          {
            key: 'priority',
            label: 'Priority',
            type: 'select',
            options: ['Low', 'Normal', 'High'],
          },
          {
            key: 'complaintDescription',
            label: 'Complaint description',
            type: 'textarea',
          },
        ]}
      />
    );

    await waitFor(() => expect(mocks.listCasesPage).toHaveBeenCalled());
    expect(screen.getByText('Cases')).toBeInTheDocument();
    expect(
      screen.queryByLabelText('Complaint category')
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Create case' }));
    const dialog = screen.getByRole('dialog');
    expect(
      within(dialog).getByLabelText('Complaint category')
    ).toBeInTheDocument();
    expect(
      within(dialog).getByLabelText('Complaint description')
    ).toBeInTheDocument();
    expect(
      within(dialog).getByRole('combobox', { name: 'Property' })
    ).toBeInTheDocument();
    expect(screen.getAllByLabelText('Complaint category')).toHaveLength(1);
  });

  it('shows the shared workflow history for a governed procedure case', async () => {
    mocks.getCase.mockResolvedValue({
      id: 'case-1',
      module: 'Facilities',
      entityType: 'EstateFacilityComplaint',
      title: 'Water service complaint',
      referenceNumber: 'FAC-001',
      status: 'Open',
      currentStageIndex: 1,
      currentStageName: 'Management Approval',
      currentAssignedRole: 'Facilities Manager',
      usesConfiguredWorkflow: true,
      workflowInstanceId: 'workflow-1',
      createdAt: '2026-10-06T08:00:00Z',
      canEditCurrentStage: false,
      currentStageFieldKeys: [],
      fields: [],
      checklistItems: [],
      documents: [],
      activities: [],
    });

    render(
      <ProcedureCaseWorkspace
        module="Facilities"
        entityType="EstateFacilityComplaint"
        defaultTitle="Complaint Management"
        workspaceType="Case Workflow"
        caseId="case-1"
        detailOnly
      />
    );

    const historyTab = await screen.findByRole('tab', {
      name: 'Workflow history',
    });
    fireEvent.mouseDown(historyTab, { button: 0, ctrlKey: false });

    expect(
      await screen.findByText(
        'Workflow trail for EstateFacilityComplaint case-1'
      )
    ).toBeInTheDocument();
  });
});
