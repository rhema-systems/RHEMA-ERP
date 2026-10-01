import { normalizeCurrencyCode } from '@/lib/currency';
import {
  type CurrencyListDto,
  estateCurrencyService,
} from '@/services/financeCommonService';

type CurrencyLike = Partial<Pick<CurrencyListDto, 'code' | 'name' | 'symbol' | 'decimalPlaces' | 'isActive' | 'isBaseCurrency'>>;

export type EstateCurrencyReference = {
  code: string;
  name: string;
  symbol: string;
  decimalPlaces: number;
};

export const DEFAULT_ESTATE_CURRENCY: EstateCurrencyReference = {
  code: 'GHS',
  name: 'Ghanaian Cedi',
  symbol: 'GHS',
  decimalPlaces: 2,
};

export function getEstateCurrencyCode(currency?: CurrencyLike | null) {
  return typeof currency?.code === 'string'
    ? normalizeCurrencyCode(currency.code, DEFAULT_ESTATE_CURRENCY.code)
    : '';
}

export function resolveEstateBaseCurrency(
  baseCurrency?: CurrencyLike | null,
  currencies: CurrencyLike[] = []
): EstateCurrencyReference {
  const activeCurrencies = currencies.filter((currency) => currency.isActive !== false);
  const candidate =
    baseCurrency ??
    activeCurrencies.find((currency) => currency.isBaseCurrency) ??
    activeCurrencies[0] ??
    DEFAULT_ESTATE_CURRENCY;
  const code = getEstateCurrencyCode(candidate) || DEFAULT_ESTATE_CURRENCY.code;

  return {
    code,
    name:
      typeof candidate?.name === 'string' && candidate.name.trim()
        ? candidate.name.trim()
        : DEFAULT_ESTATE_CURRENCY.name,
    symbol:
      typeof candidate?.symbol === 'string' && candidate.symbol.trim()
        ? candidate.symbol.trim()
        : DEFAULT_ESTATE_CURRENCY.symbol,
    decimalPlaces:
      typeof candidate?.decimalPlaces === 'number'
        ? candidate.decimalPlaces
        : DEFAULT_ESTATE_CURRENCY.decimalPlaces,
  };
}

export function buildEstateCurrencyOptions(
  currencies: CurrencyLike[],
  baseCurrency: EstateCurrencyReference,
  currentValue?: string | null
): string[] {
  const values = [
    baseCurrency.code,
    ...currencies
      .filter((currency) => currency.isActive !== false)
      .map((currency) => getEstateCurrencyCode(currency))
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

export function findEstateCurrency(
  currencies: CurrencyLike[],
  code?: string | null,
  baseCurrency?: EstateCurrencyReference
) {
  const normalizedCode = code?.trim()
    ? normalizeCurrencyCode(code, baseCurrency?.code ?? DEFAULT_ESTATE_CURRENCY.code)
    : '';

  return normalizedCode
    ? currencies.find((currency) => getEstateCurrencyCode(currency) === normalizedCode)
    : undefined;
}

export function formatEstateCurrencyOption(
  currencies: CurrencyLike[],
  code: string,
  baseCurrency: EstateCurrencyReference
) {
  const currency = findEstateCurrency(currencies, code, baseCurrency);
  const normalizedCode = normalizeCurrencyCode(code, baseCurrency.code);
  const name = typeof currency?.name === 'string' ? currency.name.trim() : '';
  const symbol = typeof currency?.symbol === 'string' ? currency.symbol.trim() : '';

  return [normalizedCode, name, symbol ? `(${symbol})` : ''].filter(Boolean).join(' ');
}

export async function loadEstateCurrencyContext(): Promise<{
  activeCurrencies: CurrencyListDto[];
  baseCurrency: EstateCurrencyReference;
  rawBaseCurrency: CurrencyListDto | null;
}> {
  const activeCurrencies = await estateCurrencyService.getActive().catch(() => []);
  const rawBaseCurrency = activeCurrencies.find((currency) => currency.isBaseCurrency) ?? null;

  return {
    activeCurrencies,
    baseCurrency: resolveEstateBaseCurrency(rawBaseCurrency, activeCurrencies),
    rawBaseCurrency,
  };
}
