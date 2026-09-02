import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  hasRole: vi.fn(),
  toast: vi.fn(),
  list: vi.fn(),
  lookups: vi.fn(),
  history: vi.fn(),
  listReconnaissance: vi.fn(),
  listInformationRequests: vi.fn(),
  documentLookups: vi.fn(),
  listDocuments: vi.fn(),
  planningGisLookups: vi.fn(),
  listPlanningGis: vi.fn(),
  getRecords: vi.fn(),
  getRecord: vi.fn(),
  getWorkflowEntitySummary: vi.fn(),
  saveStepChecklistResponses: vi.fn(),
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
    list: mocks.list,
    lookups: mocks.lookups,
    history: mocks.history,
    listReconnaissance: mocks.listReconnaissance,
    listInformationRequests: mocks.listInformationRequests,
    documentLookups: mocks.documentLookups,
    listDocuments: mocks.listDocuments,
    planningGisLookups: mocks.planningGisLookups,
    listPlanningGis: mocks.listPlanningGis,
  },
}));

vi.mock('@/services/document-management.service', () => ({
  documentManagementService: {
    getRecords: mocks.getRecords,
    getRecord: mocks.getRecord,
  },
}));

vi.mock('@/services/workflow-api.service', () => ({
  workflowApiService: {
    getWorkflowEntitySummary: mocks.getWorkflowEntitySummary,
    saveStepChecklistResponses: mocks.saveStepChecklistResponses,
  },
}));

import { CivilEngineeringDesignWorkflowPanel } from './CivilEngineeringDesignWorkflowPanel';

describe('CivilEngineeringDesignWorkflowPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(false);
    mocks.hasRole.mockReturnValue(false);
    mocks.list.mockResolvedValue([]);
    mocks.lookups.mockResolvedValue({
      supervisingCivilEngineers: [],
      civilEngineers: [],
      draftsmen: [],
      informationSourceSections: [],
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
    });
    mocks.history.mockResolvedValue([]);
    mocks.listReconnaissance.mockResolvedValue([]);
    mocks.listInformationRequests.mockResolvedValue([]);
    mocks.documentLookups.mockResolvedValue({
      namingPolicy: 'ProjectDisciplineSequenceRevision',
      allowedFileExtensions: ['.dwg', '.pro', '.rvt', '.std', '.pdf', '.docx'],
      maximumFileSizeMb: 100,
      metadataTemplateCode: 'TDC-CIV-ENGINEERING-FILE',
      allowAuthorizedPreview: true,
      allowAuthorizedDownload: false,
      owners: [],
      reviewers: [],
      workPackages: [],
      documents: [],
    });
    mocks.listDocuments.mockResolvedValue([]);
    mocks.planningGisLookups.mockResolvedValue({
      estateManagedAssetId: 'asset-1',
      estateManagedAssetLabel: 'SITE-1 - Block A',
      developmentApprovalFiles: [],
      planningConditions: [],
      developmentConstraints: [],
      landUseImpacts: [],
      evidenceDocuments: [],
      layoutConformities: [],
    });
    mocks.listPlanningGis.mockResolvedValue([]);
    mocks.getRecords.mockResolvedValue([]);
    mocks.getWorkflowEntitySummary.mockResolvedValue({
      entityType: 'PROJECT_DESIGN_REVIEW',
      entityId: 'case-1',
      hasActiveInstance: true,
      currentStepName: 'SCE design review',
      currentStepInstanceId: 'step-1',
      currentStepType: 'Approval',
      canCurrentUserApprove: true,
      pendingApprovers: [],
      currentStepChecklist: [
        { id: 'civil-design', name: 'Design reviewed', isRequired: true },
        { id: 'civil-drawings', name: 'Drawings reviewed', isRequired: true },
        {
          id: 'civil-calculations',
          name: 'Calculations reviewed',
          isRequired: true,
        },
        {
          id: 'civil-specifications',
          name: 'Specifications reviewed',
          isRequired: true,
        },
        {
          id: 'civil-dependencies',
          name: 'Dependencies resolved',
          isRequired: true,
        },
      ],
    });
  });

  it('does not load Civil records without workspace permission', () => {
    render(<CivilEngineeringDesignWorkflowPanel projectId="project-1" />);

    expect(screen.getByText('Civil design access required')).toBeTruthy();
    expect(mocks.list).not.toHaveBeenCalled();
  });

  it('loads project-scoped controlled member lookups for an authorized HOD', async () => {
    mocks.hasPermission.mockImplementation(
      (permission: string) =>
        permission === 'civil-engineering.workspace.read' ||
        permission === 'civil-engineering.design.manage'
    );
    mocks.hasRole.mockImplementation(
      (role: string) => role === 'TDC_HEAD_OF_CIVIL_ENGINEERING'
    );

    render(<CivilEngineeringDesignWorkflowPanel projectId="project-1" />);

    await waitFor(() =>
      expect(mocks.lookups).toHaveBeenCalledWith('project-1')
    );
    expect(screen.getByText('New Engineering Case')).toBeTruthy();
    expect(screen.getByText('Authoritative source')).toBeTruthy();
    expect(screen.getByText('Property / site')).toBeTruthy();
    expect(screen.getByText('Engineering category')).toBeTruthy();
    expect(screen.getByText('Supervising Civil Engineer')).toBeTruthy();
    expect(screen.queryByText(/user id/i)).toBeNull();
  });

  it('uses the shared workflow checklist gate for SCE package submission', async () => {
    mocks.hasPermission.mockImplementation(
      (permission: string) =>
        permission === 'civil-engineering.workspace.read' ||
        permission === 'civil-engineering.design.manage'
    );
    mocks.hasRole.mockImplementation(
      (role: string) => role === 'TDC_SUPERVISING_CIVIL_ENGINEER'
    );
    mocks.list.mockResolvedValue([
      {
        id: 'case-1',
        projectId: 'project-1',
        referenceNumber: 'CIV-DES-001',
        title: 'Block A structural design',
        directive: 'Prepare the coordinated structural design package.',
        stage: 'SceDrawingReview',
        status: 'PendingApproval',
        approvalStatus: 'Pending',
        requirePlanningGisValidation: true,
        hodUserId: 'hod-1',
        supervisingCivilEngineerUserId: 'sce-1',
        currentAssigneeUserId: 'sce-1',
        workflowInstanceId: 'workflow-1',
        rowVersion: 'AQID',
        evidence: [],
      },
    ]);

    render(<CivilEngineeringDesignWorkflowPanel projectId="project-1" />);

    expect(await screen.findByText('SCE design review')).toBeTruthy();
    expect(screen.getByText('Submission package')).toBeTruthy();
    expect(screen.getByText('HOD review due date')).toBeTruthy();
    expect(await screen.findByText('Submit package to HOD')).toBeTruthy();
    expect(screen.getByText('Return drawings')).toBeTruthy();
    expect(screen.getByText('Engineering document register')).toBeTruthy();
    expect(mocks.documentLookups).toHaveBeenCalledWith('case-1');
    expect(mocks.listDocuments).toHaveBeenCalledWith('case-1');
    expect(mocks.getWorkflowEntitySummary).toHaveBeenCalledWith(
      'PROJECT_DESIGN_REVIEW',
      'case-1'
    );
  });
});
