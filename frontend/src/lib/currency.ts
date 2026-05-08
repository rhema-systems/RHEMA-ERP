'use client';

const DEFAULT_CURRENCY = 'USD';
const CURRENCY_CODE_PATTERN = /^[A-Z]{3}$/;

export function normalizeCurrencyCode(currency?: string | null, fallback: string = DEFAULT_CURRENCY) {
  const normalizedFallback = fallback.trim().toUpperCase();
  const normalizedCurrency = currency?.trim().toUpperCase();
  const candidate = normalizedCurrency && CURRENCY_CODE_PATTERN.test(normalizedCurrency)
    ? normalizedCurrency
    : normalizedFallback;

  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: candidate }).resolvedOptions().currency;
  } catch {
    return DEFAULT_CURRENCY;
  }
}

export function formatCurrencyAmount(
  value: number,
  currency?: string | null,
  maximumFractionDigits: number = 0,
) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: normalizeCurrencyCode(currency),
    maximumFractionDigits,
  }).format(value);
}
