import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  listInformationRequests: vi.fn(),
  listAssignedInformationRequests: vi.fn(),
  getRecord: vi.fn(),
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));
vi.mock('@/services/civil-engineering-design.service', () => ({
  civilEngineeringDesignService: {
    listInformationRequests: mocks.listInformationRequests,
    listAssignedInformationRequests: mocks.listAssignedInformationRequests,
  },
}));
vi.mock('@/services/document-management.service', () => ({
  documentManagementService: { getRecord: mocks.getRecord },
}));

import { CivilEngineeringInformationRequestsPanel } from './CivilEngineeringInformationRequestsPanel';

const designCase = {
  id: 'case-1',
  projectId: 'project-1',
  referenceNumber: 'CIV-DES-001',
  title: 'Block design',
  directive: 'Complete the coordinated structural design package.',
  stage: 'SceInformationGathering' as const,
  status: 'InProgress',
  approvalStatus: 'Draft',
  requirePlanningGisValidation: false,
  hodUserId: 'hod-1',
  supervisingCivilEngineerUserId: 'sce-1',
  rowVersion: 'AQID',
  evidence: [],
};

const request = {
  id: 'request-1',
  designCaseId: 'case-1',
  projectId: 'project-1',
  referenceNumber: 'CIV-INP-001',
  subject: 'Planning setback confirmation',
  question: 'Confirm the approved building setback and planning constraints.',
  priority: 'High' as const,
  status: 'Submitted' as const,
  raisedDate: '2026-08-15T08:00:00Z',
  responseDueDate: '2026-08-20T23:59:59Z',
  requestedSectionId: 'section-1',
  requestedSectionLabel: 'Development / Town Planning',
  requestedByUserId: 'sce-1',
  blocksDesignReadiness: true,
  isDesignReady: false,
  rowVersion: 'AQID',
  responses: [],
};

describe('CivilEngineeringInformationRequestsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.listInformationRequests.mockResolvedValue([]);
    mocks.listAssignedInformationRequests.mockResolvedValue([]);
  });

  it('uses controlled section, priority, due-date and readiness inputs', async () => {
    render(
      <CivilEngineeringInformationRequestsPanel
        designCase={designCase}
        lookups={{
          supervisingCivilEngineers: [],
          civilEngineers: [],
          draftsmen: [],
          informationSourceSections: [
            {
              sectionId: 'section-1',
              departmentId: 'department-1',
              label: 'Development / Town Planning',
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
        }}
        documents={[]}
        canCreate
      />
    );

    await waitFor(() =>
      expect(mocks.listInformationRequests).toHaveBeenCalledWith('case-1')
    );
    expect(screen.getByText('Requested section')).toBeTruthy();
    expect(screen.getByText('Priority')).toBeTruthy();
    expect(screen.getByText('Response due date')).toBeTruthy();
    expect(screen.getByText('Required before design assignment')).toBeTruthy();
    expect(screen.queryByText(/section id/i)).toBeNull();
  });

  it('loads the current employee section queue and exposes a governed response', async () => {
    mocks.listAssignedInformationRequests.mockResolvedValue([request]);

    render(
      <CivilEngineeringInformationRequestsPanel
        assignedOnly
        documents={[]}
        canRespond
      />
    );

    expect(
      await screen.findByText(/Planning setback confirmation/)
    ).toBeTruthy();
    expect(mocks.listAssignedInformationRequests).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Respond' }));
    expect(screen.getByPlaceholderText('Section response')).toBeTruthy();
    expect(screen.getByText('Select evidence')).toBeTruthy();
  });
});
