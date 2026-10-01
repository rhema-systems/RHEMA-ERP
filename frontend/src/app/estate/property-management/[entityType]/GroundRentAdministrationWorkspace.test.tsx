import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GroundRentAdministrationWorkspace } from './GroundRentAdministrationWorkspace';
import {
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

const mocks = vi.hoisted(() => ({
  getAccounts: vi.fn(),
  getOptions: vi.fn(),
  assessAsset: vi.fn(),
  getManagedAssets: vi.fn(),
  getManagedAsset: vi.fn(),
  getPortalListingDemarcations: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('assetId=apartment-1'),
}));
vi.mock('@/services/estate-ground-rent.service', () => ({
  estateGroundRentService: mocks,
}));
vi.mock('@/services/estate-land-management.service', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/services/estate-land-management.service')>(),
  estateLandManagementService: mocks,
}));
vi.mock('sonner', () => ({ toast: { success: mocks.success, error: mocks.error } }));

const apartment: EstateManagedAsset = {
  id: 'apartment-1',
  assetCode: 'APT-001',
  name: 'Apartment 1',
  assetType: EstateManagedAssetType.Property,
  status: EstateManagedAssetStatus.Available,
  sourceType: EstateManagedAssetSourceType.Imported,
  currency: 'GHS',
  externalListingCurrency: 'GHS',
  externalListingType: 'Rent',
  externalListingStatus: 'Published',
  isPublishedToExternalPortal: true,
  isAvailableForLease: true,
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
  groundRentPayable: 1200,
};

describe('Ground Rent Administration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAccounts.mockResolvedValue([]);
    mocks.getOptions.mockResolvedValue({ assets: [], incomeAccounts: [] });
    mocks.getManagedAssets.mockResolvedValue([]);
    mocks.getManagedAsset.mockResolvedValue(apartment);
    mocks.getPortalListingDemarcations.mockResolvedValue([]);
    mocks.assessAsset.mockResolvedValue({ id: apartment.id, assetType: 'Property', approvedAnnualGroundRent: 1440 });
  });
  afterEach(cleanup);

  it('edits the fixed annual ground rent of a published apartment', async () => {
    render(<GroundRentAdministrationWorkspace />);

    expect(await screen.findByText('APT-001 - Apartment 1')).toBeInTheDocument();
    expect(screen.getByText('Approved annual ground rent')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '1440' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Assessment' }));

    await waitFor(() => expect(mocks.assessAsset).toHaveBeenCalledWith({
      estateManagedAssetId: apartment.id,
      ratePerAcre: 0,
      annualAmount: 1440,
      currencyCode: 'GHS',
    }));
  });
});
