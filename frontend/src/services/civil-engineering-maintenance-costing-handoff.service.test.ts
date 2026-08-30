import { describe, expect, it, vi } from 'vitest';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/services/api.service', () => ({ apiService: { get, post } }));

describe('civilEngineeringMaintenanceCostingHandoffService', () => {
  it('uses the governed cross-owner handoff resource', async () => {
    const { civilEngineeringMaintenanceCostingHandoffService } = await import('./civil-engineering-maintenance-costing-handoff.service');
    const create = { clientRequestId: 'create', assessmentId: 'assessment', projectId: 'project', quantitySurveyEstimateVersionId: 'estimate' };
    const action = { clientRequestId: 'action', action: 'SubmitCosting' as const, rowVersion: 'rv' };
    civilEngineeringMaintenanceCostingHandoffService.lookups();
    civilEngineeringMaintenanceCostingHandoffService.create(create);
    civilEngineeringMaintenanceCostingHandoffService.act('handoff', action);
    expect(get).toHaveBeenCalledWith('/projects/civil-engineering/maintenance-costing-handoffs/lookups');
    expect(post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-costing-handoffs', create);
    expect(post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-costing-handoffs/handoff/actions', action);
  });
});
