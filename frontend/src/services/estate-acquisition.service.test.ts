import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  createWorkflowDefinition: vi.fn(),
}));

vi.mock('./compatibleApiService', () => ({
  compatibleApiService: {
    get: mocks.get,
    post: mocks.post,
  },
}));

vi.mock('./api.service', () => ({
  apiService: {
    request: vi.fn(),
    downloadBlob: vi.fn(),
  },
}));

vi.mock('./workflow-api.service', () => ({
  workflowApiService: {
    createWorkflowDefinition: mocks.createWorkflowDefinition,
  },
}));

import {
  ACQUISITION_STAGES,
  estateAcquisitionService,
  STAGE_WORKFLOW_REQUIREMENTS,
} from './estate-acquisition.service';

describe('EstateAcquisitionService workflow requirements', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('keeps physical and planning capture fields in Parcel Identification', () => {
    const parcelFields = STAGE_WORKFLOW_REQUIREMENTS[0].fields.map(
      (field) => field.name
    );
    const approvalFields = STAGE_WORKFLOW_REQUIREMENTS[1].fields.map(
      (field) => field.name
    );

    expect(parcelFields).toEqual(
      expect.arrayContaining([
        'soilType',
        'topography',
        'hasAccessRoad',
        'hasUtilities',
        'siteAccessRoute',
        'drainageCondition',
        'planningSchemeReference',
        'encumbranceObserved',
      ])
    );
    expect(approvalFields).toEqual([
      'assessmentRecommendation',
      'approvalNotes',
    ]);
    expect(approvalFields).not.toContain('soilType');
  });

  it('defines named required documents for every acquisition stage', () => {
    const stages = Object.values(STAGE_WORKFLOW_REQUIREMENTS);

    expect(stages).toHaveLength(17);
    expect(stages.every((stage) => stage.documents.length > 0)).toBe(true);
    expect(stages.flatMap((stage) => stage.documents)).toHaveLength(47);
  });

  it('keeps survey verification approval-only and clears overlap during ownership verification', () => {
    expect(STAGE_WORKFLOW_REQUIREMENTS[3].fields).toEqual([]);
    expect(STAGE_WORKFLOW_REQUIREMENTS[3].checklist).toEqual([]);
    expect(
      STAGE_WORKFLOW_REQUIREMENTS[3].documents.map(
        (document) => document.name
      )
    ).toEqual(['Survey Verification Report']);

    expect(
      STAGE_WORKFLOW_REQUIREMENTS[5].fields.find(
        (field) => field.name === 'overlapCleared'
      )?.label
    ).toBe('Ownership / Title Overlap Cleared');
    expect(
      STAGE_WORKFLOW_REQUIREMENTS[5].documents.map(
        (document) => document.name
      )
    ).toContain('Overlap Clearance Evidence');
  });

  it('keeps stage documents out of approval checklists', async () => {
    mocks.createWorkflowDefinition.mockResolvedValue({});

    await estateAcquisitionService.createWorkflowTemplate();

    expect(mocks.createWorkflowDefinition).toHaveBeenCalledTimes(1);
    const definition = mocks.createWorkflowDefinition.mock.calls[0][0];
    const steps = definition.steps || [];
    const documentRequirements = steps.flatMap(
      (step: any) => step.configuration?.taskConfig?.documentRequirements || []
    );
    const approvalChecks = steps.flatMap(
      (step: any) => step.configuration?.qualityConfig?.qualityChecks || []
    );

    expect(documentRequirements).toHaveLength(47);
    expect(approvalChecks.length).toBeGreaterThan(0);
    expect(approvalChecks.every((check: any) => check.requiresDocument === false)).toBe(true);
    expect(approvalChecks.some((check: any) => check.name.startsWith('Attach '))).toBe(false);
  });

  it('routes submission to the Estate Manager before cadastral survey while retaining procedure IDs', async () => {
    mocks.createWorkflowDefinition.mockResolvedValue({});

    await estateAcquisitionService.createWorkflowTemplate();

    const definition = mocks.createWorkflowDefinition.mock.calls[0][0];
    const steps = definition.steps;
    expect(steps.map((step: any) => step.order)).toEqual(
      Array.from({ length: 17 }, (_, index) => index + 1)
    );
    expect(steps.find((step: any) => step.order === 2)).toMatchObject({
      name: 'Suitability Approval',
      requiredRole: 'Estate Manager',
    });
    expect(steps.find((step: any) => step.order === 3)).toMatchObject({
      name: 'Cadastral Survey',
      requiredRole: 'Survey Officer',
    });
    expect(definition.transitions).toHaveLength(16);
    definition.transitions.forEach((transition: any, index: number) => {
      expect(transition).toMatchObject({
        fromStepId: steps[index].id,
        toStepId: steps[index + 1].id,
        isDefault: true,
      });
    });
    const procedureIds = Array.from({ length: 17 }, (_, index) => index);
    expect(ACQUISITION_STAGES.map((stage) => stage.id)).toEqual(procedureIds);
    expect(ACQUISITION_STAGES.map((stage) => stage.order)).toEqual(procedureIds);
    expect(JSON.parse(definition.configuration).stages).toEqual(ACQUISITION_STAGES);
  });

  it('loads the active document catalog from the operational acquisition endpoint', async () => {
    const requirements = {
      0: [
        {
          id: 'planning-evidence',
          requirementKey: 'planning-evidence',
          documentName: 'Planning Evidence',
          documentType: 'Planning',
          isRequired: true,
        },
      ],
    };
    mocks.get.mockResolvedValue({ data: requirements });

    await expect(
      estateAcquisitionService.getActiveWorkflowDocumentRequirements()
    ).resolves.toEqual(requirements);
    expect(mocks.get).toHaveBeenCalledWith(
      '/estate/land-acquisitions/active-document-requirements'
    );
  });

  it('surfaces a document-catalog request failure', async () => {
    mocks.get.mockRejectedValue(new Error('workflow unavailable'));

    await expect(
      estateAcquisitionService.getActiveWorkflowDocumentRequirements()
    ).rejects.toThrow('workflow unavailable');
  });
});
