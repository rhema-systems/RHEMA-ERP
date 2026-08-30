import { describe, expect, it, vi } from 'vitest';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/services/api.service', () => ({ apiService: { get, post } }));

describe('civilEngineeringMaintenanceAssessmentService', () => {
  it('uses the governed maintenance-assessment resource for lookups, direction and transitions', async () => {
    const { civilEngineeringMaintenanceAssessmentService } = await import('./civil-engineering-maintenance-assessment.service');
    const request = { clientRequestId: 'r', supervisingCivilEngineerUserId: 's', direction: 'Assess defect', dueAt: '2026-08-22T08:00:00Z' };
    civilEngineeringMaintenanceAssessmentService.lookups();
    civilEngineeringMaintenanceAssessmentService.start('intake-1', request);
    civilEngineeringMaintenanceAssessmentService.transition('assessment-1', { clientRequestId: 'r2', action: 'Approve', rowVersion: 'rv' });
    expect(get).toHaveBeenCalledWith('/projects/civil-engineering/maintenance-assessments/lookups');
    expect(post).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-assessments/intakes/intake-1', request);
    expect(post).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-assessments/assessment-1/transition', { clientRequestId: 'r2', action: 'Approve', rowVersion: 'rv' });
  });
});
