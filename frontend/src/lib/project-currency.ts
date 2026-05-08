import { formatCurrencyAmount, normalizeCurrencyCode } from '@/lib/currency';
import {
  currencyService,
  type CurrencyDetailDto,
  type CurrencyListDto,
} from '@/services/financeCommonService';

type CurrencyLike = Partial<Pick<CurrencyListDto, 'code' | 'name' | 'symbol' | 'decimalPlaces' | 'isActive' | 'isBaseCurrency'>>;

export type ProjectCurrencyReference = {
  code: string;
  name: string;
  symbol: string;
  decimalPlaces: number;
};

export const DEFAULT_PROJECT_CURRENCY: ProjectCurrencyReference = {
  code: 'USD',
  name: 'US Dollar',
  symbol: '$',
  decimalPlaces: 2,
};

export function getProjectCurrencyCode(currency?: CurrencyLike | null) {
  return typeof currency?.code === 'string'
    ? normalizeCurrencyCode(currency.code, DEFAULT_PROJECT_CURRENCY.code)
    : '';
}

export function formatProjectCurrencyLabel(currency?: CurrencyLike | null, fallbackCode?: string) {
  const code = getProjectCurrencyCode(currency) || normalizeCurrencyCode(fallbackCode, DEFAULT_PROJECT_CURRENCY.code);
  const name = typeof currency?.name === 'string' ? currency.name.trim() : '';
  const symbol = typeof currency?.symbol === 'string' ? currency.symbol.trim() : '';

  return [code, name, symbol ? `(${symbol})` : '']
    .filter(Boolean)
    .join(' ');
}

export function resolveProjectBaseCurrency(
  baseCurrency?: CurrencyLike | null,
  currencies: CurrencyLike[] = [],
): ProjectCurrencyReference {
  const activeCurrencies = currencies.filter((currency) => currency.isActive !== false);
  const candidate = baseCurrency
    ?? activeCurrencies.find((currency) => currency.isBaseCurrency)
    ?? activeCurrencies[0]
    ?? DEFAULT_PROJECT_CURRENCY;
  const code = getProjectCurrencyCode(candidate) || DEFAULT_PROJECT_CURRENCY.code;
  const name = typeof candidate?.name === 'string' && candidate.name.trim()
    ? candidate.name.trim()
    : DEFAULT_PROJECT_CURRENCY.name;
  const symbol = typeof candidate?.symbol === 'string' && candidate.symbol.trim()
    ? candidate.symbol.trim()
    : DEFAULT_PROJECT_CURRENCY.symbol;
  const decimalPlaces = typeof candidate?.decimalPlaces === 'number'
    ? candidate.decimalPlaces
    : DEFAULT_PROJECT_CURRENCY.decimalPlaces;

  return {
    code,
    name,
    symbol,
    decimalPlaces,
  };
}

export function buildProjectCurrencyOptions(
  currencies: CurrencyLike[],
  baseCurrency: ProjectCurrencyReference,
  currentValue?: string | null,
): string[] {
  const values = [
    baseCurrency.code,
    ...currencies
      .filter((currency) => currency.isActive !== false)
      .map((currency) => getProjectCurrencyCode(currency))
      .filter((value): value is string => Boolean(value && value.trim())),
  ];
  const uniqueValues = Array.from(new Set(values));
  const normalizedCurrentValue = currentValue?.trim()
    ? normalizeCurrencyCode(currentValue, baseCurrency.code)
    : '';

  return normalizedCurrentValue && !uniqueValues.includes(normalizedCurrentValue)
    ? [normalizedCurrentValue, ...uniqueValues]
    : uniqueValues;
}

export function findProjectCurrency(
  currencies: CurrencyLike[],
  code?: string | null,
  baseCurrency?: ProjectCurrencyReference,
) {
  const normalizedCode = code?.trim()
    ? normalizeCurrencyCode(code, baseCurrency?.code ?? DEFAULT_PROJECT_CURRENCY.code)
    : '';

  if (!normalizedCode) {
    return undefined;
  }

  return currencies.find((currency) => getProjectCurrencyCode(currency) === normalizedCode);
}

export function formatProjectMoney(
  value: number,
  currency?: string | null,
  fallbackCurrency?: string,
  maximumFractionDigits: number = 0,
) {
  return formatCurrencyAmount(
    value,
    currency || fallbackCurrency || DEFAULT_PROJECT_CURRENCY.code,
    maximumFractionDigits,
  );
}

export async function loadProjectCurrencyContext(): Promise<{
  activeCurrencies: CurrencyListDto[];
  baseCurrency: ProjectCurrencyReference;
  rawBaseCurrency: CurrencyDetailDto | null;
}> {
  const [activeCurrencies, rawBaseCurrency] = await Promise.all([
    currencyService.getActive().catch(() => []),
    currencyService.getBaseCurrency().catch(() => null),
  ]);

  return {
    activeCurrencies,
    baseCurrency: resolveProjectBaseCurrency(rawBaseCurrency, activeCurrencies),
    rawBaseCurrency,
  };
}
