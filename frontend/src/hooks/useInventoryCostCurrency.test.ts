import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useInventoryCostCurrency } from './useInventoryCostCurrency';

const getActive = vi.hoisted(() => vi.fn());
vi.mock('@/services/financeCommonService', () => ({ procurementCurrencyService: { getActive } }));

describe('inventory cost currency reference', () => {
  it('uses the configured base currency through the procurement reference', async () => {
    getActive.mockResolvedValue([{ code: 'USD', isBaseCurrency: false }, { code: 'GHS', isBaseCurrency: true }]);
    const { result } = renderHook(useInventoryCostCurrency);
    await waitFor(() => expect(result.current).toBe('GHS'));
  });
  it('does not invent a currency when reference lookup fails', async () => {
    getActive.mockRejectedValue(new Error('unavailable'));
    const { result } = renderHook(useInventoryCostCurrency);
    await waitFor(() => expect(getActive).toHaveBeenCalled());
    expect(result.current).toBeUndefined();
  });
});
