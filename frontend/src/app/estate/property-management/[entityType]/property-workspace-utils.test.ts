import { describe, expect, it } from 'vitest';

import type { EstateManagedAsset } from '@/services/estate-land-management.service';
import {
  assetMatchesWorkspacePrefill,
  buildPropertyWorkspaceHref,
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
