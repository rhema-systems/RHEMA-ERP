import type {
  Account,
  AccountCurrencyLink,
  Currency,
  ExchangeRate,
  ExchangeRateQuoteSide,
  ExchangeRateType,
  FinanceSettings,
} from '@/types/finance';

// Mirrors the Finance posting engine's dated account-link/rate policy for draft UX.
// The posting engine remains authoritative; missing catalogue or effective-date evidence fails closed here.

export type ManualJournalFxStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface ManualJournalFxLineState {
  currencyCode: string;
  exchangeRateId?: string;
  exchangeRate: number | '';
  debit?: number;
  credit?: number;
  foreignDebit?: number;
  foreignCredit?: number;
  rateStatus?: ManualJournalFxStatus;
  rateError?: string;
  rateSource?: string;
  rateDate?: string;
}

export interface ManualJournalRateRequest {
  baseCurrencyCode: string;
  effectiveDate: string;
  rateType: ExchangeRateType;
  quoteSide: ExchangeRateQuoteSide;
}

const RATE_TYPES = new Set<ExchangeRateType>([
  'Daily',
  'Average',
  'MonthEnd',
  'QuarterEnd',
  'YearEnd',
  'Budget',
  'Fixed',
  'Spot',
]);

function isCurrencyLinkEffective(
  link: AccountCurrencyLink,
  selectedDate: string
): boolean {
  const startDate = link.effectiveDate?.slice(0, 10);
  const endDate = link.effectiveEndDate?.slice(0, 10);
  if (!link.isActive || !startDate) return false;
  return startDate <= selectedDate && (!endDate || endDate >= selectedDate);
}

export function normalizeCurrencyCode(value?: string | null): string {
  return value?.trim().toUpperCase() ?? '';
}

export function requireFunctionalCurrency(
  settings: Pick<FinanceSettings, 'baseCurrency'>
): string {
  const currency = normalizeCurrencyCode(settings.baseCurrency);
  if (!/^[A-Z]{3}$/.test(currency)) {
    throw new Error(
      'Finance Settings does not contain a valid functional currency.'
    );
  }

  return currency;
}

export function getAllowedJournalCurrencies(
  account: Account | undefined,
  links: AccountCurrencyLink[],
  currencies: Currency[],
  functionalCurrency: string,
  effectiveDate: string
): string[] {
  const activeCodes = new Set(
    currencies
      .filter((currency) => currency.isActive)
      .map((currency) => normalizeCurrencyCode(currency.currencyCode))
      .filter(Boolean)
  );
  const base = normalizeCurrencyCode(functionalCurrency);

  if (!account) {
    return activeCodes.has(base) ? [base] : [];
  }

  const accountCurrency = normalizeCurrencyCode(account.currencyCode) || base;
  const selectedDate = effectiveDate.slice(0, 10);
  const candidates = account.isMultiCurrency
    ? [
        accountCurrency,
        ...links
          .filter((link) => isCurrencyLinkEffective(link, selectedDate))
          .map((link) => normalizeCurrencyCode(link.linkedCurrencyCode)),
      ]
    : [accountCurrency];

  return [...new Set(candidates)]
    .filter((code) => activeCodes.has(code))
    .sort((left, right) => {
      if (left === accountCurrency) return -1;
      if (right === accountCurrency) return 1;
      return left.localeCompare(right);
    });
}

export function getManualJournalRateRequest(
  account: Account,
  transactionCurrency: string,
  links: AccountCurrencyLink[],
  settings: FinanceSettings,
  effectiveDate: string
): ManualJournalRateRequest | null {
  const functionalCurrency = requireFunctionalCurrency(settings);
  const currency = normalizeCurrencyCode(transactionCurrency);
  if (!currency || currency === functionalCurrency) return null;

  const directionalPolicyEnabled =
    settings.directionalExchangeRatePolicyEnabled === true;
  const selectedDate = effectiveDate.slice(0, 10);
  const link = account.isMultiCurrency
    ? links.find(
        (item) =>
          isCurrencyLinkEffective(item, selectedDate) &&
          normalizeCurrencyCode(item.linkedCurrencyCode) === currency
      )
    : undefined;
  const configuredRateType = link?.transactionRateType?.replace(
    /[-_ ]/g,
    ''
  ) as ExchangeRateType | undefined;
  const rateType =
    directionalPolicyEnabled &&
    configuredRateType &&
    RATE_TYPES.has(configuredRateType)
      ? configuredRateType
      : 'Daily';
  const quoteSide = directionalPolicyEnabled
    ? (link?.transactionQuoteSide ??
      settings.defaultTransactionQuoteSide ??
      'Mid')
    : 'Mid';

  return {
    baseCurrencyCode: functionalCurrency,
    effectiveDate,
    rateType,
    quoteSide,
  };
}

export function applyCanonicalJournalRate<T extends ManualJournalFxLineState>(
  line: T,
  snapshot: Pick<
    ExchangeRate,
    'id' | 'rate' | 'currentExchangeRate' | 'rateSource' | 'effectiveDate'
  >
): T {
  const exchangeRate = Number(snapshot.rate ?? snapshot.currentExchangeRate);
  if (!Number.isFinite(exchangeRate) || exchangeRate <= 0) {
    throw new Error('Finance returned an invalid exchange-rate snapshot.');
  }

  return recalculateJournalFunctionalAmounts({
    ...line,
    exchangeRateId: snapshot.id,
    exchangeRate,
    rateStatus: 'ready',
    rateError: undefined,
    rateSource: snapshot.rateSource,
    rateDate: snapshot.effectiveDate,
  });
}

export function recalculateJournalFunctionalAmounts<
  T extends ManualJournalFxLineState,
>(line: T): T {
  const rate = typeof line.exchangeRate === 'number' ? line.exchangeRate : 0;
  return {
    ...line,
    ...(line.foreignDebit && rate > 0
      ? { debit: line.foreignDebit * rate }
      : {}),
    ...(line.foreignCredit && rate > 0
      ? { credit: line.foreignCredit * rate }
      : {}),
  } as T;
}

export function getManualJournalFxBlocker(
  line: ManualJournalFxLineState,
  functionalCurrency: string
): string | null {
  const currency = normalizeCurrencyCode(line.currencyCode);
  if (!currency) return 'Select a transaction currency.';
  if (currency === normalizeCurrencyCode(functionalCurrency)) return null;
  if (line.rateStatus === 'loading')
    return `The ${currency} exchange rate is still loading.`;
  if (line.rateStatus === 'error')
    return (
      line.rateError || `No approved ${currency} exchange rate is available.`
    );
  if (
    line.rateStatus !== 'ready' ||
    !line.exchangeRateId ||
    typeof line.exchangeRate !== 'number' ||
    line.exchangeRate <= 0
  ) {
    return `No approved ${currency} exchange rate is available.`;
  }
  if ((line.debit ?? 0) > 0 && !(line.foreignDebit && line.foreignDebit > 0)) {
    return `Enter the original ${currency} debit amount.`;
  }
  if (
    (line.credit ?? 0) > 0 &&
    !(line.foreignCredit && line.foreignCredit > 0)
  ) {
    return `Enter the original ${currency} credit amount.`;
  }

  return null;
}
