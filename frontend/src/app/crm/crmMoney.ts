import type { CrmCurrencyAmountDto } from '@/services/crmService';

const formatNumber = (value: number) =>
  new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(value);

export function formatCrmAmount(value: number, currency?: string | null): string {
  const code = currency?.trim().toUpperCase();
  if (!code || !/^[A-Z]{3}$/.test(code)) {
    return `${formatNumber(value)} (currency not recorded)`;
  }

  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: code,
      currencyDisplay: 'code',
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${code} ${formatNumber(value)}`;
  }
}

export function formatCrmCurrencyTotals(totals?: CrmCurrencyAmountDto[] | null): string {
  return totals?.length
    ? totals.map(({ amount, currency }) => formatCrmAmount(amount, currency)).join(' · ')
    : '—';
}

export function formatCrmMissingCurrencyCount(count?: number): string | null {
  return count
    ? `${count} ${count === 1 ? 'record' : 'records'} without currency omitted`
    : null;
}
