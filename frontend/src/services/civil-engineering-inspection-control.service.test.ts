import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringInspectionControlService } from './civil-engineering-inspection-control.service';

describe('civilEngineeringInspectionControlService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses project-scoped controlled endpoints and distinct lifecycle commands', async () => {
    api.get.mockResolvedValue({});
    api.post.mockResolvedValue({});
    await civilEngineeringInspectionControlService.list('project-1');
    await civilEngineeringInspectionControlService.lookups('project-1');
    await civilEngineeringInspectionControlService.create('project-1', {} as never);
    await civilEngineeringInspectionControlService.process('inspection-1', {} as never);
    await civilEngineeringInspectionControlService.history('inspection-1');
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/inspection-controls', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/inspection-controls/lookups', { projectId: 'project-1' });
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/inspection-controls/projects/project-1', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/inspection-controls/inspection-1/process', expect.any(Object));
    expect(api.get).toHaveBeenNthCalledWith(3, '/projects/civil-engineering/inspection-controls/inspection-1/history');
  });
});
