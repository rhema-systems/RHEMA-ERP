import type { ExchangeRate, FinanceSettings } from '@/types/finance';
import { resolvePostingExchangeRate } from '@/services/finance.service';

type CashRateSettings = Pick<
  FinanceSettings,
  'directionalExchangeRatePolicyEnabled' | 'defaultTransactionQuoteSide'
>;

export async function loadApprovedCashRate(
  params: {
    transactionCurrency: string;
    functionalCurrency: string;
    transactionDate: Date;
    settings: CashRateSettings;
  },
  fetchRate: (
    currencyCode: string,
    query: {
      baseCurrencyCode: string;
      effectiveDate: string;
      rateType: 'Daily';
      quoteSide: 'Mid' | 'Buying' | 'Selling';
    }
  ) => Promise<ExchangeRate>
) {
  const transactionCurrency = params.transactionCurrency.trim().toUpperCase();
  const functionalCurrency = params.functionalCurrency.trim().toUpperCase();
  const effectiveDate = localIsoDate(params.transactionDate);
  if (transactionCurrency === functionalCurrency) {
    return { exchangeRateId: undefined, rate: 1, source: 'Functional currency', quoteSide: 'Mid' as const };
  }

  const quoteSide = params.settings.directionalExchangeRatePolicyEnabled
    ? params.settings.defaultTransactionQuoteSide || 'Mid'
    : 'Mid';
  try {
    const rate = await fetchRate(transactionCurrency, {
      baseCurrencyCode: functionalCurrency,
      effectiveDate,
      rateType: 'Daily',
      quoteSide,
    });
    return {
      exchangeRateId: rate.id,
      rate: resolvePostingExchangeRate(rate),
      source: rate.rateSource || `Approved ${quoteSide} Daily rate`,
      quoteSide,
    };
  } catch {
    throw new Error(
      `No active approved ${quoteSide} Daily exchange rate is available for ` +
      `${transactionCurrency} to ${functionalCurrency} on ${effectiveDate}.`
    );
  }
}

function localIsoDate(value: Date): string {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    throw new Error('Transaction date is required to resolve an approved exchange rate.');
  }
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}
