import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringQualityTestService } from './civil-engineering-quality-test.service';

describe('civilEngineeringQualityTestService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses project-scoped lookups and distinct create/review commands', async () => {
    api.get.mockResolvedValue({});
    api.post.mockResolvedValue({});
    await civilEngineeringQualityTestService.list('project-1');
    await civilEngineeringQualityTestService.lookups('project-1');
    await civilEngineeringQualityTestService.create('project-1', {} as never);
    await civilEngineeringQualityTestService.review('report-1', {} as never);
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/quality-tests', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/quality-tests/lookups', { projectId: 'project-1' });
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/quality-tests/projects/project-1', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/quality-tests/report-1/review', expect.any(Object));
  });
});
