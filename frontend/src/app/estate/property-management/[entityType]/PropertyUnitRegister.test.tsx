import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PropertyUnitRegister } from './PropertyUnitRegister';
import {
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

const mocks = vi.hoisted(() => ({
  loadAssets: vi.fn(),
  updateExternalListing: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
  info: vi.fn(),
  assets: [] as EstateManagedAsset[],
}));

const asset: EstateManagedAsset = {
  id: 'asset-1',
  assetCode: 'DEMO-PROP-001',
  name: 'Demo Office Suite',
  assetType: EstateManagedAssetType.Property,
  status: EstateManagedAssetStatus.Available,
  sourceType: EstateManagedAssetSourceType.Imported,
  location: 'Accra',
  purpose: 'Office',
  areaValue: 90,
  areaUnit: 'sqm',
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

vi.mock('./use-managed-assets-page', () => ({
  useManagedAssetsPage: () => ({
    assets: mocks.assets, page: 1, setPage: vi.fn(), isLoading: false,
    loadError: null, loadAssets: mocks.loadAssets, pageSize: 10,
    totalPages: 1, totalItems: 1,
  }),
}));
vi.mock('./EstateAssetImportDialog', () => ({ EstateAssetImportDialog: () => null }));
vi.mock('@/services/estate-land-management.service', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/services/estate-land-management.service')>(),
  estateLandManagementService: { updateExternalListing: mocks.updateExternalListing },
}));
vi.mock('sonner', () => ({ toast: { success: mocks.success, error: mocks.error, info: mocks.info } }));

describe('Property and Unit Register', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.assets = [asset];
    mocks.loadAssets.mockResolvedValue(undefined);
    mocks.updateExternalListing.mockResolvedValue({ ...asset, externalListingType: 'Rent' });
  });
  afterEach(cleanup);

  it('keeps the register open and refreshes after sending an asset to Portal Listings', async () => {
    render(<PropertyUnitRegister />);

    fireEvent.keyDown(screen.getByRole('button', { name: 'Actions for Demo Office Suite' }), { key: 'Enter' });
    fireEvent.click(screen.getByRole('menuitem', { name: 'Send to portal' }));

    await waitFor(() => expect(mocks.updateExternalListing).toHaveBeenCalledWith(
      asset.id,
      expect.objectContaining({ externalListingType: 'Rent', externalListingStatus: 'Draft' })
    ));
    await waitFor(() => expect(mocks.loadAssets).toHaveBeenCalled());
    expect(screen.getByRole('heading', { name: 'Estate Property Master Register' })).toBeInTheDocument();
  });

  it('shows secondary fields in details instead of wide table columns', async () => {
    render(<PropertyUnitRegister />);
    expect(screen.queryByRole('columnheader', { name: 'Lease record' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'File reference' })).not.toBeInTheDocument();

    fireEvent.keyDown(screen.getByRole('button', { name: 'Actions for Demo Office Suite' }), { key: 'Enter' });
    fireEvent.click(screen.getByRole('menuitem', { name: 'View details' }));

    expect(await screen.findByRole('dialog')).toHaveTextContent('90 sqm');
    expect(screen.getByRole('dialog')).toHaveTextContent('File reference');
  });

  it('shows why a non-available property cannot be sent to the portal', () => {
    mocks.assets = [{ ...asset, status: EstateManagedAssetStatus.UnderMaintenance }];
    render(<PropertyUnitRegister />);

    fireEvent.keyDown(screen.getByRole('button', { name: 'Actions for Demo Office Suite' }), { key: 'Enter' });

    expect(screen.getByRole('menuitem', { name: 'Send to portal' })).toHaveAttribute('data-disabled');
    expect(screen.getByText('Set the property status to Available first.')).toBeInTheDocument();
  });

  it('allows an available manually registered property to be sent to the portal', () => {
    mocks.assets = [{ ...asset, sourceType: EstateManagedAssetSourceType.Manual }];
    render(<PropertyUnitRegister />);

    fireEvent.keyDown(screen.getByRole('button', { name: 'Actions for Demo Office Suite' }), { key: 'Enter' });

    expect(screen.getByRole('menuitem', { name: 'Send to portal' })).not.toHaveAttribute('data-disabled');
  });
});
