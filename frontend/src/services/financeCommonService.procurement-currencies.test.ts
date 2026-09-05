import { beforeEach, describe, expect, it, vi } from 'vitest';

import { apiService } from './api.service';
import { procurementCurrencyService } from './financeCommonService';

describe('procurement currency reference-data client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'procurement-user-token');
  });

  it('loads Finance-owned currencies through the procurement-authorized projection', async () => {
    const currencies = [
      {
        id: 'ghs-id',
        currencyCode: 'GHS',
        currencyName: 'Ghana Cedi',
        currencySymbol: 'GH₵',
        isBaseCurrency: true,
        isActive: true,
      },
      {
        id: 'usd-id',
        currencyCode: 'USD',
        currencyName: 'US Dollar',
        currencySymbol: '$',
        isBaseCurrency: false,
        isActive: true,
      },
    ];
    const getMock = vi.spyOn(apiService, 'get').mockResolvedValue(currencies);

    await expect(procurementCurrencyService.getActive()).resolves.toEqual([
      expect.objectContaining({ id: 'ghs-id', code: 'GHS', name: 'Ghana Cedi', isBaseCurrency: true }),
      expect.objectContaining({ id: 'usd-id', code: 'USD', name: 'US Dollar', isBaseCurrency: false }),
    ]);
    expect(getMock).toHaveBeenCalledWith('/procurement/reference-data/currencies');
  });
});
