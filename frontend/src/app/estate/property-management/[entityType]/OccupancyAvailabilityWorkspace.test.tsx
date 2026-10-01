import React from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OccupancyAvailabilityWorkspace } from './OccupancyAvailabilityWorkspace';
import {
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

const asset: EstateManagedAsset = {
  id: 'asset-1',
  assetCode: 'DEMO-PROP-001',
  name: 'Demo Office Suite',
  assetType: EstateManagedAssetType.Property,
  status: EstateManagedAssetStatus.Available,
  sourceType: EstateManagedAssetSourceType.Imported,
  location: 'Accra',
  currency: 'GHS',
  externalListingCurrency: 'GHS',
  externalListingType: 'None',
  externalListingStatus: 'Draft',
  isPublishedToExternalPortal: false,
  isAvailableForLease: false,
  isAvailableForSale: false,
  isPublishedFromProject: false,
  gisProvider: 'GeoServer',
  gisSyncStatus: 'NotLinked',
  boundaryVerified: false,
  demarcationCount: 0,
  verifiedDemarcationCount: 0,
  ownershipHistory: [],
  isReadyForProjectManagement: false,
  autoGenerateRentInvoices: false,
  rentGracePeriodDays: 0,
  rentPenaltyMethod: 'None',
  rentPenaltyValue: 0,
};

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('./use-managed-assets-page', () => ({
  useManagedAssetsPage: () => ({
    assets: [asset], setAssets: vi.fn(), page: 1, setPage: vi.fn(),
    isLoading: false, loadError: null, loadAssets: vi.fn(),
    pageSize: 10, totalPages: 1, totalItems: 1,
  }),
}));

describe('Occupancy and availability row actions', () => {
  afterEach(cleanup);

  it('groups update, handover, and billing in one menu', () => {
    render(<OccupancyAvailabilityWorkspace />);

    expect(screen.queryByRole('button', { name: 'Update' })).not.toBeInTheDocument();
    const actions = screen.getByRole('button', { name: 'Actions for Demo Office Suite' });
    fireEvent.keyDown(actions, { key: 'Enter' });

    expect(screen.getByRole('menuitem', { name: 'Update status' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Handover' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Billing' })).toBeInTheDocument();
  });
});
