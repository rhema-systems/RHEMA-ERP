import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { dashboardService, type EnterpriseDashboardData } from './dashboard';

describe('dashboardService', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('requests the enterprise dashboard with the selected date range', async () => {
    const response = {} as EnterpriseDashboardData;
    const getSpy = vi.spyOn(apiService, 'get').mockResolvedValue(response);

    const result = await dashboardService.getEnterpriseDashboard('2026-01-01', '2026-03-31');

    expect(getSpy).toHaveBeenCalledWith(
      '/dashboard/enterprise?startDate=2026-01-01&endDate=2026-03-31',
    );
    expect(result).toBe(response);
  });

  it('preserves the unfiltered endpoint for callers that omit the range', async () => {
    const response = {} as EnterpriseDashboardData;
    const getSpy = vi.spyOn(apiService, 'get').mockResolvedValue(response);

    await dashboardService.getEnterpriseDashboard();

    expect(getSpy).toHaveBeenCalledWith('/dashboard/enterprise');
  });

  it('adds the selected warehouse and location without changing the shared endpoint', async () => {
    const response = {} as EnterpriseDashboardData;
    const getSpy = vi.spyOn(apiService, 'get').mockResolvedValue(response);

    await dashboardService.getEnterpriseDashboard(
      '2026-01-01',
      '2026-03-31',
      '11111111-1111-1111-1111-111111111111',
      '22222222-2222-2222-2222-222222222222',
    );

    expect(getSpy).toHaveBeenCalledWith(
      '/dashboard/enterprise?startDate=2026-01-01&endDate=2026-03-31&warehouseId=11111111-1111-1111-1111-111111111111&locationId=22222222-2222-2222-2222-222222222222',
    );
  });
});
