import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringSiteInstructionService } from './civil-engineering-site-instruction.service';

describe('civilEngineeringSiteInstructionService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant-authenticated project-scoped list and controlled lookup routes', async () => {
    api.get.mockResolvedValue([]);

    await civilEngineeringSiteInstructionService.list('project-1');
    await civilEngineeringSiteInstructionService.lookups('project-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/site-instructions',
      { projectId: 'project-1' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/site-instructions/lookups',
      { projectId: 'project-1' }
    );
  });

  it('keeps PE issue, PM workflow action, contractor response, engineering review and closure as separate commands', async () => {
    api.post.mockResolvedValue({});
    const issue = {
      clientRequestId: 'request-1',
      projectEngineerAssignmentId: 'assignment-1',
      referenceNumber: 'SI-001',
      title: 'Revise drainage detail',
      description: 'Use the approved drainage detail at the affected chainage.',
      effectiveDate: '2026-08-20T00:00:00.000Z',
      evidence: [
        {
          centralDocumentRecordId: 'record-1',
          centralDocumentVersionId: 'version-1',
        },
      ],
    };
    const decision = {
      clientRequestId: 'request-2',
      rowVersion: 'row-version',
      approve: true,
      reason: 'Reviewed and approved for contractor issue.',
    };
    const response = {
      clientRequestId: 'request-3',
      rowVersion: 'row-version',
      action: 'ContractorResponded' as const,
      message: 'The revised execution plan is attached.',
      centralDocumentRecordId: 'record-2',
      centralDocumentVersionId: 'version-2',
    };
    const review = {
      clientRequestId: 'request-4',
      rowVersion: 'row-version',
      approve: true,
      reason: 'Engineering review accepts the submitted controlled response.',
      centralDocumentRecordId: 'record-3',
      centralDocumentVersionId: 'version-3',
    };
    const closure = {
      clientRequestId: 'request-5',
      rowVersion: 'row-version',
      action: 'InstructionClosed' as const,
      reason: 'Controlled closure evidence confirms the follow-up is complete.',
      centralDocumentRecordId: 'record-4',
      centralDocumentVersionId: 'version-4',
    };

    await civilEngineeringSiteInstructionService.issue('project-1', issue);
    await civilEngineeringSiteInstructionService.processProjectManagerRouting(
      'routing-1',
      decision
    );
    await civilEngineeringSiteInstructionService.recordContractorResponse(
      'routing-1',
      response
    );
    await civilEngineeringSiteInstructionService.reviewContractorResponse(
      'routing-1',
      review
    );
    await civilEngineeringSiteInstructionService.recordEngineeringFollowUp(
      'routing-1',
      closure
    );

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/site-instructions/projects/project-1',
      issue
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/site-instructions/routing-1/project-manager-routing',
      decision
    );
    expect(api.post).toHaveBeenNthCalledWith(
      3,
      '/projects/civil-engineering/site-instructions/routing-1/contractor-response',
      response
    );
    expect(api.post).toHaveBeenNthCalledWith(
      4,
      '/projects/civil-engineering/site-instructions/routing-1/engineering-review',
      review
    );
    expect(api.post).toHaveBeenNthCalledWith(
      5,
      '/projects/civil-engineering/site-instructions/routing-1/engineering-follow-up',
      closure
    );
  });
});
