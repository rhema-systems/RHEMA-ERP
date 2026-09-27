import type { EstateManagedAsset } from '@/services/estate-land-management.service';

type ListingPriceSource = Pick<
  EstateManagedAsset,
  'listingScope' | 'targetSalePrice' | 'externalListingType' |
  'externalListingPrice' | 'externalSalePrice' | 'externalMonthlyRent'
>;

export function getListingPriceDefaults(asset: ListingPriceSource) {
  const landBankPrice = asset.listingScope === 'demarcation' &&
    asset.targetSalePrice != null && asset.targetSalePrice > 0
    ? asset.targetSalePrice
    : null;
  const legacyRecurringLeasePrice = asset.listingScope === 'demarcation' &&
    asset.externalListingType === 'Lease' &&
    asset.externalMonthlyRent != null &&
    asset.externalListingPrice === asset.externalMonthlyRent &&
    landBankPrice != null;
  const salePrice = asset.externalSalePrice ??
    (asset.externalListingType === 'Sale' ? asset.externalListingPrice : null) ??
    landBankPrice ?? asset.targetSalePrice ?? null;
  const leasePrice = (asset.externalListingType === 'Lease' && !legacyRecurringLeasePrice
    ? asset.externalListingPrice
    : null) ?? landBankPrice ?? asset.targetSalePrice ?? null;

  return { landBankPrice, salePrice, leasePrice, legacyRecurringLeasePrice };
}
