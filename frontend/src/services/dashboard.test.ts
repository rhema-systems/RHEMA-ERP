import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import {
  dashboardService,
  getUnavailableDashboardModules,
  resolveDashboardReportingCurrency,
  type EnterpriseDashboardData,
} from './dashboard';

describe('dashboard availability presentation', () => {
  it('does not describe access-restricted analytics as a service outage', () => {
    const restricted = {
      module: 'Procurement and Inventory Management',
      available: false,
      accessRestricted: true,
      error: 'No assigned warehouse or location granting inventory read access.',
    };
    expect(getUnavailableDashboardModules([restricted])).toEqual([]);
    expect(restricted.available).toBe(false);
  });

  it('retains genuine and legacy service failures alongside restricted analytics', () => {
    const outage = { module: 'Projects', available: false, accessRestricted: false, error: 'Database unavailable' };
    const legacyOutage = { module: 'Maintenance', available: false, error: 'Request failed' };
    expect(getUnavailableDashboardModules([
      { module: 'Inventory', available: false, accessRestricted: true },
      outage,
      legacyOutage,
    ])).toEqual([outage, legacyOutage]);
  });

  it('does not warn about successful modules', () => {
    expect(getUnavailableDashboardModules([{ module: 'Purchase Orders', available: true }])).toEqual([]);
  });
});

describe('dashboard reporting currency', () => {
  it('uses tenant configuration ahead of the finance response', () => {
    expect(resolveDashboardReportingCurrency({
      reportingCurrency: { currencyCode: 'GHS', currencyName: 'Ghana Cedi', currencySymbol: 'GH₵', decimalPlaces: 2 },
      financeOverview: { currencyCode: 'USD', currencyDecimalPlaces: 2 } as unknown as EnterpriseDashboardData['financeOverview'],
    })).toEqual({ currencyCode: 'GHS', decimalPlaces: 2 });
  });

  it('uses the authoritative finance currency when the settings widget is unavailable', () => {
    expect(resolveDashboardReportingCurrency({
      reportingCurrency: { currencyCode: '', currencyName: '', currencySymbol: '', decimalPlaces: 0 },
      financeOverview: { currencyCode: 'EUR', currencyDecimalPlaces: 2 } as unknown as EnterpriseDashboardData['financeOverview'],
    })).toEqual({ currencyCode: 'EUR', decimalPlaces: 2 });
  });

  it('does not invent a currency fallback', () => {
    expect(resolveDashboardReportingCurrency({
      reportingCurrency: { currencyCode: '', currencyName: '', currencySymbol: '', decimalPlaces: 0 },
      financeOverview: null,
    })).toEqual({ currencyCode: '', decimalPlaces: 0 });
  });

  it('rejects an invalid configured currency instead of falling back silently', () => {
    expect(resolveDashboardReportingCurrency({
      reportingCurrency: { currencyCode: 'N/A', currencyName: '', currencySymbol: '', decimalPlaces: 0 },
      financeOverview: null,
    })).toEqual({ currencyCode: '', decimalPlaces: 0 });
  });
});

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
