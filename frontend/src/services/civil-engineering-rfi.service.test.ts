import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringRfiService } from './civil-engineering-rfi.service';

describe('civilEngineeringRfiService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses project-scoped tenant-authenticated reads and separate PE/PM commands', async () => {
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({});
    await civilEngineeringRfiService.list('project-1');
    await civilEngineeringRfiService.lookups('project-1');
    await civilEngineeringRfiService.submitProjectEngineerResponse('routing-1', {
      clientRequestId: 'request-1', rowVersion: 'row-version', responseText: 'Controlled engineer response text.',
      centralDocumentRecordId: 'record-1', centralDocumentVersionId: 'version-1',
    });
    await civilEngineeringRfiService.processProjectManagerResponse('routing-1', {
      clientRequestId: 'request-2', rowVersion: 'row-version', approve: true, reason: 'Approved for contractor response.',
    });
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/rfis', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/rfis/lookups', { projectId: 'project-1' });
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/rfis/routing-1/project-engineer-response', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/rfis/routing-1/project-manager-response', expect.any(Object));
  });
});
