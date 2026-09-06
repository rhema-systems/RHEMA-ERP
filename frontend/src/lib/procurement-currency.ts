export interface ProcurementCurrencyReference {
  code: string;
  isBaseCurrency: boolean;
  isActive: boolean;
}

export const normalizeProcurementCurrency = (
  currency?: string | null,
  fallback = 'GHS'
) => {
  const normalized = currency?.trim().toUpperCase();
  return normalized && /^[A-Z]{3}$/.test(normalized)
    ? normalized
    : fallback.trim().toUpperCase();
};

export const getProcurementBaseCurrency = (
  currencies: ProcurementCurrencyReference[],
  fallback = 'GHS'
) => normalizeProcurementCurrency(
  currencies.find((currency) => currency.isActive && currency.isBaseCurrency)?.code,
  fallback
);

// Recommendation DTOs contain amounts but no currency. Resolve against the
// already-loaded, tenant-scoped tender bids; never infer dollars from an amount.
export const getTenderBidCurrency = (
  bidId: string | undefined,
  bids: ReadonlyArray<{ id: string; currency?: string | null }> | undefined,
  tenderCurrency?: string | null
) => normalizeProcurementCurrency(
  bids?.find((bid) => bid.id === bidId)?.currency,
  normalizeProcurementCurrency(tenderCurrency)
);

export const formatProcurementMoney = (
  amount: number,
  currency?: string | null,
  fractionDigits = 2
) => `${normalizeProcurementCurrency(currency)} ${Number(amount || 0).toLocaleString('en-US', {
  minimumFractionDigits: fractionDigits,
  maximumFractionDigits: fractionDigits,
})}`;
