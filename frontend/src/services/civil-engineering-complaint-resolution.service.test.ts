import { describe, expect, it, vi } from 'vitest';

const get = vi.fn();
vi.mock('@/services/api.service', () => ({ apiService: { get } }));

describe('civilEngineeringComplaintResolutionService', () => {
  it('uses the read-only Civil complaint-resolution resource', async () => {
    const { civilEngineeringComplaintResolutionService } = await import('./civil-engineering-complaint-resolution.service');
    civilEngineeringComplaintResolutionService.list();
    civilEngineeringComplaintResolutionService.timeline('helpdesk-ticket');
    expect(get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/complaint-resolutions');
    expect(get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/complaint-resolutions/helpdesk-ticket/timeline');
  });
});
