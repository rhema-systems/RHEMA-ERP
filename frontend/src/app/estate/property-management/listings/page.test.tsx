import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import EstatePropertyListingsPage from './page';
import {
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

const mocks = vi.hoisted(() => ({
  getManagedAssets: vi.fn(),
  getManagedAsset: vi.fn(),
  getPortalListingDemarcations: vi.fn(),
  getDocuments: vi.fn(),
  error: vi.fn(),
  params: 'assetId=target',
}));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(mocks.params),
}));
vi.mock('@/services/estate-land-management.service', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/services/estate-land-management.service')>(),
  estateLandManagementService: mocks,
}));
vi.mock('sonner', () => ({ toast: { error: mocks.error } }));

const listing = (id: string): EstateManagedAsset => ({
  id,
  assetCode: `PROP-${id}`,
  name: `Property ${id}`,
  assetType: EstateManagedAssetType.Property,
  status: EstateManagedAssetStatus.Available,
  sourceType: EstateManagedAssetSourceType.Imported,
  location: 'Accra',
  currency: 'GHS',
  externalListingCurrency: 'GHS',
  externalListingType: 'Rent',
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
});

describe('Portal Listings deep links', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.params = 'assetId=target';
    mocks.getManagedAssets.mockResolvedValue(Array.from({ length: 11 }, (_, index) => listing(String(index))));
    mocks.getPortalListingDemarcations.mockResolvedValue([]);
    mocks.getManagedAsset.mockResolvedValue(listing('target'));
    mocks.getDocuments.mockResolvedValue([]);
  });
  afterEach(cleanup);

  it('loads the requested listing directly when it is outside the first page', async () => {
    render(<EstatePropertyListingsPage />);

    await waitFor(() => expect(mocks.getManagedAsset).toHaveBeenCalledWith('target'));
    expect(await screen.findByRole('dialog')).toHaveTextContent('Property target');
    expect(screen.queryByText('The requested managed asset could not be loaded.')).not.toBeInTheDocument();
  });

  it('does not open an asset that has not been sent to Portal Listings', async () => {
    mocks.getManagedAsset.mockResolvedValue({ ...listing('target'), externalListingType: 'None' });
    render(<EstatePropertyListingsPage />);

    expect(await screen.findByRole('dialog')).toHaveTextContent('The requested managed asset could not be loaded.');
    expect(screen.queryByText('Property target')).not.toBeInTheDocument();
  });

  it('uses the stored asset type names in the inventory', async () => {
    mocks.params = '';
    mocks.getManagedAssets.mockResolvedValue([listing('property'), {
      ...listing('facility'), assetType: EstateManagedAssetType.Facility,
    }]);
    mocks.getPortalListingDemarcations.mockResolvedValue([{
      ...listing('land'), assetType: EstateManagedAssetType.Land,
      listingScope: 'demarcation', parentAssetId: 'parent',
    }]);
    render(<EstatePropertyListingsPage />);

    await waitFor(() => expect(screen.getByRole('cell', { name: 'Facility' })).toBeInTheDocument());
    expect(screen.getByRole('cell', { name: 'Property' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Land' })).toBeInTheDocument();
  });

  it('sends the selected listing status to both paged sources', async () => {
    mocks.params = '';
    render(<EstatePropertyListingsPage />);
    await waitFor(() => expect(mocks.getManagedAssets).toHaveBeenCalled());

    fireEvent.click(screen.getByRole('combobox', { name: 'Filter by listing status' }));
    fireEvent.click(screen.getByRole('option', { name: 'Published' }));

    await waitFor(() => expect(mocks.getManagedAssets).toHaveBeenLastCalledWith(
      expect.objectContaining({ externalListingStatus: 'Published', skip: 0, take: 500 })
    ));
    expect(mocks.getPortalListingDemarcations).toHaveBeenLastCalledWith(
      '', 0, 500, 'Published'
    );
  });

  it('filters Property and Facility through the managed-asset query', async () => {
    mocks.params = '';
    render(<EstatePropertyListingsPage />);
    await waitFor(() => expect(screen.queryByText('Loading assets')).not.toBeInTheDocument());
    mocks.getPortalListingDemarcations.mockClear();

    fireEvent.click(screen.getByRole('combobox', { name: 'Filter by asset type' }));
    fireEvent.click(screen.getByRole('option', { name: 'Facility' }));

    await waitFor(() => expect(mocks.getManagedAssets).toHaveBeenLastCalledWith(
      expect.objectContaining({ assetType: EstateManagedAssetType.Facility, skip: 0, take: 500 })
    ));
    expect(mocks.getPortalListingDemarcations).not.toHaveBeenCalled();
  });

  it('loads only demarcations when filtering for Land', async () => {
    mocks.params = '';
    render(<EstatePropertyListingsPage />);
    await waitFor(() => expect(screen.queryByText('Loading assets')).not.toBeInTheDocument());
    mocks.getManagedAssets.mockClear();

    fireEvent.click(screen.getByRole('combobox', { name: 'Filter by asset type' }));
    fireEvent.click(screen.getByRole('option', { name: 'Land' }));

    await waitFor(() => expect(mocks.getPortalListingDemarcations).toHaveBeenLastCalledWith(
      '', 0, 500, undefined
    ));
    expect(mocks.getManagedAssets).not.toHaveBeenCalled();
  });

  it('loads the next API batch before paginating a large inventory', async () => {
    mocks.params = '';
    mocks.getManagedAssets.mockImplementation(({ skip }: { skip: number }) =>
      Promise.resolve(skip === 0
        ? Array.from({ length: 500 }, (_, index) => listing(String(index)))
        : [listing('500')])
    );
    render(<EstatePropertyListingsPage />);

    await waitFor(() => expect(mocks.getManagedAssets).toHaveBeenCalledWith(
      expect.objectContaining({ skip: 500, take: 500 })
    ));
    expect(await screen.findByText('Property 0')).toBeInTheDocument();
  });
});
