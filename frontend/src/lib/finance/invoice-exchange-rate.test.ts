import { describe, expect, it, vi } from 'vitest';
import type { ExchangeRate } from '@/types/finance';
import {
  loadApprovedInvoiceRate,
  resolveInvoiceQuoteSide,
} from './invoice-exchange-rate';

describe('invoice exchange-rate control', () => {
  it('uses 1 only for the functional currency without calling the rate API', async () => {
    const fetchRate = vi.fn();

    const snapshot = await loadApprovedInvoiceRate(
      {
        module: 'AP',
        transactionCurrency: 'ghs',
        functionalCurrency: 'GHS',
        invoiceDate: new Date(2025, 1, 3),
        settings: {},
      },
      fetchRate
    );

    expect(snapshot.rate).toBe(1);
    expect(snapshot.exchangeRateId).toBeUndefined();
    expect(snapshot.isFunctionalCurrency).toBe(true);
    expect(fetchRate).not.toHaveBeenCalled();
  });

  it('uses Mid for invoices while the directional policy is disabled', () => {
    expect(
      resolveInvoiceQuoteSide('AP', {
        directionalExchangeRatePolicyEnabled: false,
        apInvoiceQuoteSide: 'Selling',
      })
    ).toBe('Mid');
  });

  it('requests the configured module side and invoice date when directional policy is enabled', async () => {
    const approvedRate: ExchangeRate = {
      id: 'rate-1',
      baseCurrencyCode: 'GHS',
      targetCurrencyCode: 'USD',
      rate: 15.4,
      effectiveDate: '2025-02-03',
      rateType: 'Daily',
      quoteSide: 'Buying',
      rateSource: 'Bank of Ghana',
      isActive: true,
      createdAt: '2025-02-03T00:00:00Z',
      updatedAt: '2025-02-03T00:00:00Z',
    };
    const fetchRate = vi.fn().mockResolvedValue(approvedRate);

    const snapshot = await loadApprovedInvoiceRate(
      {
        module: 'AR',
        transactionCurrency: 'USD',
        functionalCurrency: 'GHS',
        invoiceDate: new Date(2025, 1, 3),
        settings: {
          directionalExchangeRatePolicyEnabled: true,
          arInvoiceQuoteSide: 'Buying',
        },
      },
      fetchRate
    );

    expect(snapshot.rate).toBe(15.4);
    expect(snapshot.exchangeRateId).toBe('rate-1');
    expect(fetchRate).toHaveBeenCalledWith('USD', {
      baseCurrencyCode: 'GHS',
      effectiveDate: '2025-02-03',
      rateType: 'Daily',
      quoteSide: 'Buying',
    });
  });

  it('blocks a foreign invoice when no approved rate can be loaded', async () => {
    const action = loadApprovedInvoiceRate(
      {
        module: 'AP',
        transactionCurrency: 'USD',
        functionalCurrency: 'GHS',
        invoiceDate: new Date(2025, 1, 3),
        settings: {},
      },
      vi.fn().mockRejectedValue(new Error('404'))
    );

    await expect(action).rejects.toThrow(
      'No active approved Mid Daily exchange rate is available for USD to GHS on 2025-02-03'
    );
  });
});
