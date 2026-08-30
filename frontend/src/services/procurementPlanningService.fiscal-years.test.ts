import { beforeEach, describe, expect, it, vi } from 'vitest';

import { procurementPlanService } from './procurementPlanningService';

describe('procurement planning fiscal-year client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'procurement-user-token');
  });

  it('loads Finance-owned fiscal years through the procurement-authorized projection', async () => {
    const fiscalYears = [{
      id: 'fy-2026',
      fiscalYearName: 'Fiscal Year 2026',
      fiscalYearCode: 'FY2026',
      year: 2026,
      startDate: '2026-01-01T00:00:00Z',
      endDate: '2026-12-31T23:59:59Z',
      status: 'Open',
      isClosed: false,
      isLocked: false,
    }];
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(fiscalYears), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(procurementPlanService.getFiscalYears()).resolves.toEqual(fiscalYears);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/procurementplans/fiscal-years',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer procurement-user-token' }),
      }),
    );
  });
});
