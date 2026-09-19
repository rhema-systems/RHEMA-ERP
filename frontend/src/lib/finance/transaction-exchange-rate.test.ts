import { describe, expect, it, vi } from 'vitest';
import type { ExchangeRate } from '@/types/finance';
import { loadApprovedSettlementRate } from './settlement-exchange-rate';
import { loadApprovedCashRate } from './cash-exchange-rate';

const approvedRate = (quoteSide: 'Mid' | 'Buying' | 'Selling'): ExchangeRate => ({
  id: `rate-${quoteSide}`,
  baseCurrencyCode: 'GHS',
  targetCurrencyCode: 'USD',
  rate: quoteSide === 'Buying' ? 14.8 : 15.2,
  effectiveDate: '2026-08-31',
  rateType: 'Daily',
  quoteSide,
  rateSource: 'Bank of Ghana',
  isActive: true,
  createdAt: '2026-08-31T00:00:00Z',
  updatedAt: '2026-08-31T00:00:00Z',
});

describe('approved transaction exchange-rate controls', () => {
  it('requests Buying for AR settlement when directional policy is enabled', async () => {
    const fetchRate = vi.fn().mockResolvedValue(approvedRate('Buying'));
    const snapshot = await loadApprovedSettlementRate({
      module: 'AR',
      transactionCurrency: 'USD',
      functionalCurrency: 'GHS',
      settlementDate: new Date(2026, 7, 31),
      settings: {
        directionalExchangeRatePolicyEnabled: true,
        arSettlementQuoteSide: 'Buying',
      },
    }, fetchRate);

    expect(snapshot.exchangeRateId).toBe('rate-Buying');
    expect(fetchRate).toHaveBeenCalledWith('USD', expect.objectContaining({ quoteSide: 'Buying' }));
  });

  it('requests Selling for AP settlement when directional policy is enabled', async () => {
    const fetchRate = vi.fn().mockResolvedValue(approvedRate('Selling'));
    await loadApprovedSettlementRate({
      module: 'AP',
      transactionCurrency: 'USD',
      functionalCurrency: 'GHS',
      settlementDate: new Date(2026, 7, 31),
      settings: {
        directionalExchangeRatePolicyEnabled: true,
        apSettlementQuoteSide: 'Selling',
      },
    }, fetchRate);

    expect(fetchRate).toHaveBeenCalledWith('USD', expect.objectContaining({ quoteSide: 'Selling' }));
  });

  it('does not replace a missing foreign settlement rate with 1', async () => {
    await expect(loadApprovedSettlementRate({
      module: 'AP',
      transactionCurrency: 'USD',
      functionalCurrency: 'GHS',
      settlementDate: new Date(2026, 7, 31),
      settings: {},
    }, vi.fn().mockRejectedValue(new Error('404')))).rejects.toThrow(
      'No active approved Mid Daily exchange rate'
    );
  });

  it('uses the default transaction quote side for direct cash', async () => {
    const fetchRate = vi.fn().mockResolvedValue(approvedRate('Selling'));
    const snapshot = await loadApprovedCashRate({
      transactionCurrency: 'USD',
      functionalCurrency: 'GHS',
      transactionDate: new Date(2026, 7, 31),
      settings: {
        directionalExchangeRatePolicyEnabled: true,
        defaultTransactionQuoteSide: 'Selling',
      },
    }, fetchRate);

    expect(snapshot.exchangeRateId).toBe('rate-Selling');
    expect(fetchRate).toHaveBeenCalledWith('USD', expect.objectContaining({ quoteSide: 'Selling' }));
  });
});
