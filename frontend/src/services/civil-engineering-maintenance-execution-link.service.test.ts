import { describe, expect, it, vi } from 'vitest';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/services/api.service', () => ({ apiService: { get, post } }));

describe('civilEngineeringMaintenanceExecutionLinkService', () => {
  it('uses the governed Civil-to-Maintenance execution resource', async () => {
    const { civilEngineeringMaintenanceExecutionLinkService } = await import('./civil-engineering-maintenance-execution-link.service');
    const create = { clientRequestId: 'create', handoffId: 'handoff', linkMode: 'CreateJobCard' as const, maintenanceTypeId: 'type', priorityLevelId: 'priority' };
    const refresh = { clientRequestId: 'refresh', rowVersion: 'rv' };
    civilEngineeringMaintenanceExecutionLinkService.lookups();
    civilEngineeringMaintenanceExecutionLinkService.create(create);
    civilEngineeringMaintenanceExecutionLinkService.refresh('execution-link', refresh);
    expect(get).toHaveBeenCalledWith('/projects/civil-engineering/maintenance-execution-links/lookups');
    expect(post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-execution-links', create);
    expect(post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-execution-links/execution-link/refresh', refresh);
  });
});
