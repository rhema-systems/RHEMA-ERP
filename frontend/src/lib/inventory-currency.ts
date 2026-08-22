import { formatCurrencyAmount, normalizeCurrencyCode } from '@/lib/currency';

export const DEFAULT_INVENTORY_CURRENCY = 'GHS';

export function normalizeInventoryCurrency(
  currencyCode?: string | null
): string {
  return (
    normalizeCurrencyCode(currencyCode, DEFAULT_INVENTORY_CURRENCY) ??
    DEFAULT_INVENTORY_CURRENCY
  );
}

export function formatInventoryMoney(
  value: number,
  currencyCode?: string | null
): string {
  return formatCurrencyAmount(
    value,
    normalizeInventoryCurrency(currencyCode),
    2
  );
}
