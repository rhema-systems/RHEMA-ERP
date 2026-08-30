import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringWeeklySupervisionService } from './civil-engineering-weekly-supervision.service';

describe('civilEngineeringWeeklySupervisionService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses project-scoped lookups and separate create, review and escalation commands', async () => {
    api.get.mockResolvedValue({});
    api.post.mockResolvedValue({});
    await civilEngineeringWeeklySupervisionService.list('project-1');
    await civilEngineeringWeeklySupervisionService.lookups('project-1');
    await civilEngineeringWeeklySupervisionService.create('project-1', {} as never);
    await civilEngineeringWeeklySupervisionService.review('report-1', {} as never);
    await civilEngineeringWeeklySupervisionService.escalateOverdue('project-1', 'request-1');
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/weekly-supervision-reports', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/weekly-supervision-reports/lookups', { projectId: 'project-1' });
    expect(api.post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/weekly-supervision-reports/projects/project-1', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/weekly-supervision-reports/report-1/review', expect.any(Object));
    expect(api.post).toHaveBeenNthCalledWith(3, '/projects/civil-engineering/weekly-supervision-reports/projects/project-1/escalate-overdue?clientRequestId=request-1');
  });
});
