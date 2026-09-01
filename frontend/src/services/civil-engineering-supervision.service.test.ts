import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringSupervisionService } from './civil-engineering-supervision.service';

describe('civilEngineeringSupervisionService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses only tenant-authenticated, project-scoped assignment and lookup routes', async () => {
    api.get.mockResolvedValue([]);

    await civilEngineeringSupervisionService.listProjectEngineerAssignments('project-1');
    await civilEngineeringSupervisionService.projectEngineerAssignmentLookups('project-1');
    await civilEngineeringSupervisionService.projectEngineerAssignmentHistory('assignment-1');

    expect(api.get).toHaveBeenNthCalledWith(1,
      '/projects/civil-engineering/supervision/project-engineer-assignments', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(2,
      '/projects/civil-engineering/supervision/project-engineer-assignments/lookups', { projectId: 'project-1' });
    expect(api.get).toHaveBeenNthCalledWith(3,
      '/projects/civil-engineering/supervision/project-engineer-assignments/assignment-1/history');
  });

  it('keeps appointment creation and ending separate, versioned commands', async () => {
    api.post.mockResolvedValue({});
    const assignment = {
      clientRequestId: 'request-1', assignedUserId: 'user-1',
      authority: 'FullProjectEngineer' as const, effectiveFrom: '2026-08-20T00:00:00.000Z',
      reason: 'Approved replacement for the active site phase.',
    };
    const end = {
      clientRequestId: 'request-2', rowVersion: 'row-version', effectiveTo: '2026-08-22T00:00:00.000Z',
      reason: 'Construction supervision is complete.',
    };

    await civilEngineeringSupervisionService.assignProjectEngineer('project-1', assignment);
    await civilEngineeringSupervisionService.endProjectEngineerAssignment('assignment-1', end);

    expect(api.post).toHaveBeenNthCalledWith(1,
      '/projects/civil-engineering/supervision/projects/project-1/project-engineer-assignments', assignment);
    expect(api.post).toHaveBeenNthCalledWith(2,
      '/projects/civil-engineering/supervision/project-engineer-assignments/assignment-1/end', end);
  });
});
