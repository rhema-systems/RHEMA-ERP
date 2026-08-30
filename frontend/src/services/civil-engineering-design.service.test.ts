import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringDesignService } from './civil-engineering-design.service';

describe('civilEngineeringDesignService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant-authenticated project-scoped list and lookup routes', async () => {
    api.get.mockResolvedValue([]);

    await civilEngineeringDesignService.list('project-1');
    await civilEngineeringDesignService.lookups('project-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/design-cases',
      { projectId: 'project-1' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/design-cases/lookups',
      { projectId: 'project-1' }
    );
  });

  it('keeps operational transitions separate from final decisions', async () => {
    const request = {
      clientRequestId: 'request-1',
      action: 'Approve' as const,
      rowVersion: 'row-version',
      evidence: [],
    };
    api.post.mockResolvedValue({});

    await civilEngineeringDesignService.transition('case-1', request);
    await civilEngineeringDesignService.decide('case-1', request);

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/design-cases/case-1/transition',
      request
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/design-cases/case-1/decision',
      request
    );
  });

  it('keeps reconnaissance under the selected design case', async () => {
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({});

    await civilEngineeringDesignService.listReconnaissance('case-1');
    await civilEngineeringDesignService.completeReconnaissance(
      'case-1',
      'report-1',
      {
        clientRequestId: 'request-1',
        rowVersion: 'row-version',
        reason: 'Ready for design',
      }
    );

    expect(api.get).toHaveBeenCalledWith(
      '/projects/civil-engineering/design-cases/case-1/reconnaissance'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/projects/civil-engineering/design-cases/case-1/reconnaissance/report-1/complete',
      {
        clientRequestId: 'request-1',
        rowVersion: 'row-version',
        reason: 'Ready for design',
      }
    );
  });

  it('separates project review requests from the current-section response queue', async () => {
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({});
    const response = {
      clientRequestId: 'request-2',
      rowVersion: 'row-version',
      responseText: 'The verified planning input is attached.',
      centralDocumentRecordId: 'record-1',
      centralDocumentVersionId: 'version-1',
    };

    await civilEngineeringDesignService.listInformationRequests('case-1');
    await civilEngineeringDesignService.listAssignedInformationRequests();
    await civilEngineeringDesignService.submitInformationResponse(
      'input-1',
      response
    );

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/design-cases/case-1/information-requests'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/design-cases/information-requests/assigned'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/projects/civil-engineering/design-cases/information-requests/input-1/response',
      response
    );
  });

  it('keeps governed engineering files under the selected design case and separates maker-checker actions', async () => {
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({});
    const create = {
      clientRequestId: 'request-3',
      centralDocumentRecordId: 'record-1',
      centralDocumentVersionId: 'version-1',
      discipline: 'Structural' as const,
      sequenceNumber: 1,
      revisionNumber: 0,
      ownerUserId: 'owner-1',
      reviewerUserId: 'reviewer-1',
    };

    await civilEngineeringDesignService.documentLookups('case-1');
    await civilEngineeringDesignService.listDocuments('case-1');
    await civilEngineeringDesignService.createDocument('case-1', create);
    await civilEngineeringDesignService.submitDocument('document-1', {
      clientRequestId: 'request-4',
      rowVersion: 'row-version',
    });
    await civilEngineeringDesignService.reviewDocument('document-1', {
      clientRequestId: 'request-5',
      rowVersion: 'row-version-2',
      approve: true,
      reason: 'Independent technical review passed.',
    });

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/design-cases/case-1/documents/lookups'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/design-cases/case-1/documents'
    );
    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/projects/civil-engineering/design-cases/case-1/documents',
      create
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/projects/civil-engineering/design-cases/documents/document-1/submit',
      { clientRequestId: 'request-4', rowVersion: 'row-version' }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      3,
      '/projects/civil-engineering/design-cases/documents/document-1/decision',
      {
        clientRequestId: 'request-5',
        rowVersion: 'row-version-2',
        approve: true,
        reason: 'Independent technical review passed.',
      }
    );
  });
});
