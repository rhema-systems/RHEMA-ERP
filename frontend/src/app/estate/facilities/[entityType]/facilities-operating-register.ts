import {
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { leaseExpiryDate } from '../../property-management/[entityType]/property-workspace-utils';

export interface FacilitiesOperatingRow {
  asset: EstateManagedAsset;
  site: string;
  unit: string;
  termEnd: string | null;
  nextBilling: string | null;
  attention: string[];
}

function daysFromToday(value: string, today: Date): number | null {
  const date = new Date(`${value.slice(0, 10)}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return null;
  const current = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
  return Math.round((date.getTime() - current) / 86_400_000);
}

export function facilitiesOperatingRows(
  assets: EstateManagedAsset[],
  mode: 'site' | 'lease',
  today = new Date()
): FacilitiesOperatingRow[] {
  return assets
    .filter((asset) => asset.assetType !== EstateManagedAssetType.Land)
    .filter((asset) => mode === 'site' ||
      asset.status === EstateManagedAssetStatus.Leased ||
      asset.status === EstateManagedAssetStatus.Occupied)
    .map((asset) => {
      const active = asset.status === EstateManagedAssetStatus.Leased ||
        asset.status === EstateManagedAssetStatus.Occupied;
      const termEnd = active ? leaseExpiryDate(asset)?.toISOString().slice(0, 10) ?? null : null;
      const nextBilling = active && asset.autoGenerateRentInvoices
        ? asset.nextRentBillingDate?.slice(0, 10) ?? null
        : null;
      const attention: string[] = [];
      const isLease = asset.externalListingType?.toLowerCase().includes('lease');
      const isRent = asset.externalListingType?.toLowerCase().includes('rent');
      const termDays = termEnd ? daysFromToday(termEnd, today) : null;
      const billingDays = nextBilling ? daysFromToday(nextBilling, today) : null;
      if (active && isLease && !termEnd) attention.push('Lease term missing');
      if (termDays !== null && termDays < 0) attention.push('Term expired');
      else if (termDays !== null && termDays <= 90) attention.push('Term ends within 90 days');
      if (active && isRent && !asset.autoGenerateRentInvoices)
        attention.push('Recurring rent not enabled');
      if (active && asset.autoGenerateRentInvoices && !nextBilling)
        attention.push('Billing date missing');
      else if (billingDays !== null && billingDays < 0)
        attention.push('Billing overdue');
      if (active && !asset.customerBusinessPartnerId && !asset.lesseeName)
        attention.push('Occupier missing');

      return {
        asset,
        site: asset.projectTitle || asset.town || asset.location || 'Not recorded',
        unit: asset.projectUnitCode || asset.assetCode,
        termEnd,
        nextBilling,
        attention,
      };
    });
}
