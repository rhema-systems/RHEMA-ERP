import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringMaintenanceIntakeService } from './civil-engineering-maintenance-intake.service';

describe('civilEngineeringMaintenanceIntakeService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the tenant-safe maintenance-intake resource and separate controlled lookups', async () => {
    api.get.mockResolvedValue({});
    api.post.mockResolvedValue({});

    await civilEngineeringMaintenanceIntakeService.list();
    await civilEngineeringMaintenanceIntakeService.lookups();
    await civilEngineeringMaintenanceIntakeService.create({} as never);

    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/maintenance-intakes');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/maintenance-intakes/lookups');
    expect(api.post).toHaveBeenCalledWith('/projects/civil-engineering/maintenance-intakes', expect.any(Object));
  });
});
