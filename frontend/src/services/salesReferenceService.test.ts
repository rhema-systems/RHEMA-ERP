import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { salesReferenceService } from './salesReferenceService';

vi.mock('./api.service', () => ({
  apiService: { get: vi.fn() },
}));

describe('salesReferenceService', () => {
  beforeEach(() => vi.mocked(apiService.get).mockReset());

  it('loads currencies from the narrow Sales reference route', async () => {
    vi.mocked(apiService.get).mockResolvedValue([
      {
        code: 'GHS',
        name: 'Ghana Cedi',
        symbol: 'GH₵',
        decimalPlaces: 2,
        isBaseCurrency: true,
      },
    ]);

    const currencies = await salesReferenceService.getActiveCurrencies();

    expect(apiService.get).toHaveBeenCalledWith('/sales/reference/currencies');
    expect(currencies).toEqual([
      expect.objectContaining({ code: 'GHS', isBaseCurrency: true }),
    ]);
  });
});
