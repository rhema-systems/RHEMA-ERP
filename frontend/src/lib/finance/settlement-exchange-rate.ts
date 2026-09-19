import type {
  ExchangeRate,
  ExchangeRateQuoteSide,
  FinanceSettings,
} from '@/types/finance';
import { resolvePostingExchangeRate } from '@/services/finance.service';

export type SettlementRateModule = 'AP' | 'AR';

type SettlementRateSettings = Pick<
  FinanceSettings,
  | 'directionalExchangeRatePolicyEnabled'
  | 'apSettlementQuoteSide'
  | 'arSettlementQuoteSide'
>;

export interface ApprovedSettlementRateSnapshot {
  exchangeRateId?: string;
  rate: number;
  quoteSide: ExchangeRateQuoteSide;
  source: string;
  effectiveDate: string;
  isFunctionalCurrency: boolean;
}

/**
 * Loads the approved Daily rate required by the configured AR/AP settlement policy.
 * A missing rate is a blocking condition: a value of 1 is never substituted for FX.
 */
export async function loadApprovedSettlementRate(
  params: {
    module: SettlementRateModule;
    transactionCurrency: string;
    functionalCurrency: string;
    settlementDate: Date;
    settings: SettlementRateSettings;
  },
  fetchRate: (
    currencyCode: string,
    query: {
      baseCurrencyCode: string;
      effectiveDate: string;
      rateType: 'Daily';
      quoteSide: ExchangeRateQuoteSide;
    }
  ) => Promise<ExchangeRate>
): Promise<ApprovedSettlementRateSnapshot> {
  const transactionCurrency = normalizeCurrency(params.transactionCurrency);
  const functionalCurrency = normalizeCurrency(params.functionalCurrency);
  const effectiveDate = toLocalIsoDate(params.settlementDate);

  if (transactionCurrency === functionalCurrency) {
    return {
      exchangeRateId: undefined,
      rate: 1,
      quoteSide: 'Mid',
      source: 'Functional currency',
      effectiveDate,
      isFunctionalCurrency: true,
    };
  }

  const quoteSide = !params.settings.directionalExchangeRatePolicyEnabled
    ? 'Mid'
    : params.module === 'AP'
      ? params.settings.apSettlementQuoteSide || 'Selling'
      : params.settings.arSettlementQuoteSide || 'Buying';

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
      quoteSide,
      source: rate.rateSource || `Approved ${quoteSide} Daily rate`,
      effectiveDate,
      isFunctionalCurrency: false,
    };
  } catch {
    throw new Error(
      `No active approved ${quoteSide} Daily exchange rate is available for ` +
        `${transactionCurrency} to ${functionalCurrency} on ${effectiveDate}. ` +
        'Load and approve the rate in Multi-Currency before recording this settlement.'
    );
  }
}

function normalizeCurrency(value: string): string {
  const normalized = value.trim().toUpperCase();
  if (normalized.length !== 3) {
    throw new Error('Currency codes must use three-character ISO values.');
  }
  return normalized;
}

function toLocalIsoDate(value: Date): string {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    throw new Error('Settlement date is required to resolve an approved exchange rate.');
  }
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
