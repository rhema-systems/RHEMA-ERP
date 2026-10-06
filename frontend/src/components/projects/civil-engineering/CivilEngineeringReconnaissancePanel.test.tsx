import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  hasRole: vi.fn(),
  toast: vi.fn(),
  listReconnaissance: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    hasPermission: mocks.hasPermission,
    hasRole: mocks.hasRole,
  }),
}));
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));
vi.mock('@/services/civil-engineering-design.service', () => ({
  civilEngineeringDesignService: {
    listReconnaissance: mocks.listReconnaissance,
  },
}));
vi.mock('@/services/document-management.service', () => ({
  documentManagementService: { getRecord: vi.fn() },
}));

import { CivilEngineeringReconnaissancePanel } from './CivilEngineeringReconnaissancePanel';

const designCase = {
  id: 'case-1',
  projectId: 'project-1',
  referenceNumber: 'CIV-DES-001',
  title: 'Block design',
  directive: 'Complete the structural design package.',
  stage: 'SceInformationGathering' as const,
  status: 'InProgress',
  approvalStatus: 'Draft',
  requirePlanningGisValidation: false,
  hodUserId: 'hod-1',
  supervisingCivilEngineerUserId: 'sce-1',
  rowVersion: 'version',
  evidence: [],
};
const lookups = {
  supervisingCivilEngineers: [],
  civilEngineers: [],
  draftsmen: [],
  informationSourceSections: [
    {
      sectionId: 'section-1',
      departmentId: 'department-1',
      label: 'Development / Geodetic Engineering',
    },
  ],
  workClassifications: [],
  engineeringCategories: [],
  estateManagedAssets: [],
  approvedCapitalProjects: [],
  maintenanceEscalations: [],
  propertyDevelopmentNeeds: [],
  planningConditions: [],
  managementDirectives: [],
  defectMonitoringCases: [],
  infrastructureImprovementRequests: [],
};

describe('CivilEngineeringReconnaissancePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.listReconnaissance.mockResolvedValue([]);
    mocks.hasPermission.mockReturnValue(true);
    mocks.hasRole.mockReturnValue(false);
  });

  it('keeps editing hidden from a user who is not the SCE role', async () => {
    render(
      <CivilEngineeringReconnaissancePanel
        designCase={designCase}
        lookups={lookups}
        documents={[]}
        onWorkflowChanged={vi.fn()}
      />
    );

    await waitFor(() =>
      expect(mocks.listReconnaissance).toHaveBeenCalledWith('case-1')
    );
    expect(screen.queryByText('Save draft')).toBeNull();
  });

  it('shows controlled reconnaissance inputs to the assigned role surface', async () => {
    mocks.hasRole.mockImplementation(
      (role: string) => role === 'TDC_SUPERVISING_CIVIL_ENGINEER'
    );

    render(
      <CivilEngineeringReconnaissancePanel
        designCase={designCase}
        lookups={lookups}
        documents={[]}
        onWorkflowChanged={vi.fn()}
      />
    );

    await waitFor(() => screen.getByText('Save draft'));
    expect(screen.getByText('Constraint')).toBeTruthy();
    expect(screen.getByText('Section input')).toBeTruthy();
    expect(screen.getByText('Photo')).toBeTruthy();
    expect(screen.queryByText(/section id/i)).toBeNull();
  });
});
