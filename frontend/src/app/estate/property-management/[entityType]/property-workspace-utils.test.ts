import { describe, expect, it } from 'vitest';

import { EstateManagedAssetStatus, type EstateManagedAsset } from '@/services/estate-land-management.service';
import {
  assetMatchesWorkspacePrefill,
  buildPropertyWorkspaceHref,
  leaseExpiryAlert,
  leaseExpiryDate,
} from './property-workspace-utils';

const asset = {
  id: 'asset-123',
  assetCode: 'LAND-009',
  projectUnitCode: 'UNIT-A1',
  propertyFileReference: 'LEASE/2026/001',
  name: 'Unit A1',
  lesseeName: 'Ada Customer',
  customerBusinessPartnerId: 'customer-1',
} as EstateManagedAsset;

describe('property workspace prefill links', () => {
  it('carries the exact asset id and property context to target workspaces', () => {
    const href = buildPropertyWorkspaceHref(
      '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
      asset,
      'Billing start',
      { billingReference: 'BILL-UNIT-A1' }
    );
    const url = new URL(href, 'http://localhost');

    expect(url.searchParams.get('assetId')).toBe('asset-123');
    expect(url.searchParams.get('field_propertyUnit')).toBe('UNIT-A1');
    expect(url.searchParams.get('field_billingReference')).toBe('BILL-UNIT-A1');
  });

  it('matches incoming workspace context by id, unit, asset, or file reference', () => {
    expect(assetMatchesWorkspacePrefill(asset, 'asset-123', null)).toBe(true);
    expect(assetMatchesWorkspacePrefill(asset, null, 'UNIT-A1')).toBe(true);
    expect(assetMatchesWorkspacePrefill(asset, null, 'LAND-009')).toBe(true);
    expect(assetMatchesWorkspacePrefill(asset, null, 'LEASE/2026/001')).toBe(true);
    expect(assetMatchesWorkspacePrefill(asset, 'other-asset', 'OTHER')).toBe(false);
  });
});

describe('lease expiry', () => {
  it('clamps month-end terms and flags active leases near expiry', () => {
    const rental = {
      ...asset,
      status: EstateManagedAssetStatus.Leased,
      dateOfTenancy: '2026-01-31T00:00:00Z',
      externalLeaseTermMonths: 1,
    } as EstateManagedAsset;

    expect(leaseExpiryDate(rental)?.toISOString().slice(0, 10)).toBe('2026-02-28');
    expect(leaseExpiryAlert(rental, new Date('2026-02-01T00:00:00Z')))
      .toBe('Expires in 27 days');
    expect(leaseExpiryAlert(rental, new Date('2026-03-01T00:00:00Z')))
      .toBe('Expired 1 day ago');
  });

  it('does not warn on a former lease', () => {
    const former = {
      ...asset,
      status: EstateManagedAssetStatus.Available,
      dateOfTenancy: '2026-01-01T00:00:00Z',
      leaseTermYears: 1,
    } as EstateManagedAsset;

    expect(leaseExpiryAlert(former, new Date('2027-01-01T00:00:00Z'))).toBeNull();
  });
});
