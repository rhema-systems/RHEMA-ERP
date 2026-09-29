import { describe, expect, it } from 'vitest';
import {
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { facilitiesOperatingRows } from './facilities-operating-register';

const asset = (overrides: Partial<EstateManagedAsset> = {}) => ({
  id: 'asset-1', assetCode: 'A-1', name: 'Apartment 1',
  assetType: EstateManagedAssetType.Property,
  status: EstateManagedAssetStatus.Occupied,
  dateOfTenancy: '2025-01-31', leaseTermYears: 1,
  autoGenerateRentInvoices: true, nextRentBillingDate: '2026-01-01',
  customerBusinessPartnerId: 'customer-1',
  ...overrides,
} as EstateManagedAsset);

describe('facilities operating register', () => {
  it('flags expired terms and overdue recurring billing without treating billing as a lease price', () => {
    const rows = facilitiesOperatingRows([asset()], 'lease', new Date('2026-02-15T00:00:00Z'));
    expect(rows).toHaveLength(1);
    expect(rows[0].termEnd).toBe('2026-01-31');
    expect(rows[0].attention).toEqual(['Term expired', 'Billing overdue']);
  });

  it('shows sites without a lease but limits lease monitoring to active occupancy', () => {
    const available = asset({ id: 'asset-2', status: EstateManagedAssetStatus.Available });
    const land = asset({ id: 'asset-3', assetType: EstateManagedAssetType.Land });
    expect(facilitiesOperatingRows([available, land], 'site')).toHaveLength(1);
    expect(facilitiesOperatingRows([available, land], 'lease')).toHaveLength(0);
  });

  it('flags missing billing date and occupier on an active unit', () => {
    const rows = facilitiesOperatingRows([asset({ nextRentBillingDate: undefined,
      customerBusinessPartnerId: undefined, lesseeName: undefined })],
    'lease', new Date('2025-06-01T00:00:00Z'));
    expect(rows[0].attention).toEqual(['Billing date missing', 'Occupier missing']);
  });

  it('flags incomplete lease terms and monthly rent setup separately', () => {
    const rows = facilitiesOperatingRows([
      asset({ id: 'lease', externalListingType: 'Lease', dateOfTenancy: undefined,
        leaseTermYears: undefined, autoGenerateRentInvoices: false }),
      asset({ id: 'rent', externalListingType: 'Rent', autoGenerateRentInvoices: false }),
    ], 'lease', new Date('2025-06-01T00:00:00Z'));
    expect(rows[0].attention).toEqual(['Lease term missing']);
    expect(rows[1].attention).toEqual(['Recurring rent not enabled']);
  });
});
