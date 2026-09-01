import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringIpcEndorsementService } from './civil-engineering-ipc-endorsement.service';

describe('civilEngineeringIpcEndorsementService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the project-scoped Civil IPC review API and never creates a second certificate endpoint', async () => {
    api.get.mockResolvedValue({});
    api.post.mockResolvedValue({});
    await civilEngineeringIpcEndorsementService.list('project-1');
    await civilEngineeringIpcEndorsementService.lookups('project-1');
    await civilEngineeringIpcEndorsementService.submit('certificate-1', {} as never);
    await civilEngineeringIpcEndorsementService.review('review-1', {} as never);
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/ipc-endorsements', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/ipc-endorsements/lookups', { projectId: 'project-1' });
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/ipc-endorsements/payment-certificates/certificate-1/submit', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/ipc-endorsements/review-1/review', expect.any(Object));
  });
});
