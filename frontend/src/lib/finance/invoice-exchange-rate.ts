import type {
  ExchangeRate,
  ExchangeRateQuoteSide,
  FinanceSettings,
} from '@/types/finance';
import { resolvePostingExchangeRate } from '@/services/finance.service';

export type InvoiceRateModule = 'AP' | 'AR';

export interface InvoiceRateQuery {
  baseCurrencyCode: string;
  effectiveDate: string;
  rateType: 'Daily';
  quoteSide: ExchangeRateQuoteSide;
}

export interface ApprovedInvoiceRateSnapshot {
  rate: number;
  quoteSide: ExchangeRateQuoteSide;
  source: string;
  effectiveDate: string;
  isFunctionalCurrency: boolean;
}

type InvoiceRateSettings = Pick<
  FinanceSettings,
  | 'directionalExchangeRatePolicyEnabled'
  | 'apInvoiceQuoteSide'
  | 'arInvoiceQuoteSide'
>;

/**
 * Loads the same approved Daily/quote-side rate that the Finance posting engine requires.
 * A fetch failure is deliberately returned as a blocking error; callers must never substitute 1.
 */
export async function loadApprovedInvoiceRate(
  params: {
    module: InvoiceRateModule;
    transactionCurrency: string;
    functionalCurrency: string;
    invoiceDate: Date;
    settings: InvoiceRateSettings;
  },
  fetchRate: (
    currencyCode: string,
    query: InvoiceRateQuery
  ) => Promise<ExchangeRate>
): Promise<ApprovedInvoiceRateSnapshot> {
  const transactionCurrency = normalizeCurrency(
    params.transactionCurrency,
    'Invoice currency'
  );
  const functionalCurrency = normalizeCurrency(
    params.functionalCurrency,
    'Functional currency'
  );
  const effectiveDate = toLocalIsoDate(params.invoiceDate);

  if (transactionCurrency === functionalCurrency) {
    return {
      rate: 1,
      quoteSide: 'Mid',
      source: 'Functional currency',
      effectiveDate,
      isFunctionalCurrency: true,
    };
  }

  const quoteSide = resolveInvoiceQuoteSide(params.module, params.settings);
  try {
    const rate = await fetchRate(transactionCurrency, {
      baseCurrencyCode: functionalCurrency,
      effectiveDate,
      rateType: 'Daily',
      quoteSide,
    });

    return {
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
        'Load and approve the rate in Multi-Currency before saving this invoice.'
    );
  }
}

export function resolveInvoiceQuoteSide(
  module: InvoiceRateModule,
  settings: InvoiceRateSettings
): ExchangeRateQuoteSide {
  if (!settings.directionalExchangeRatePolicyEnabled) return 'Mid';
  return module === 'AP'
    ? settings.apInvoiceQuoteSide || 'Mid'
    : settings.arInvoiceQuoteSide || 'Mid';
}

function normalizeCurrency(value: string, label: string): string {
  const normalized = value.trim().toUpperCase();
  if (normalized.length !== 3)
    throw new Error(`${label} must be a three-character ISO code.`);
  return normalized;
}

function toLocalIsoDate(value: Date): string {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    throw new Error(
      'Invoice date is required to resolve an approved exchange rate.'
    );
  }
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
