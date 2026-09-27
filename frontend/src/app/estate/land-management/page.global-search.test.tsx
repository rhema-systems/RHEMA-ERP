import React from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import EstateLandManagementPage from './page';
import { EstateManagedAssetSourceType, EstateManagedAssetStatus, EstateManagedAssetType, type EstateManagedAsset } from '@/services/estate-land-management.service';

const mocks = vi.hoisted(() => ({
  getLandBank: vi.fn(), getManagedAsset: vi.fn(), getLandDemarcations: vi.fn(),
  board: vi.fn(), replace: vi.fn(), error: vi.fn(),
}));
vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('recordId=target'),
  usePathname: () => '/estate/land-management',
  useRouter: () => ({ replace: mocks.replace }),
}));
vi.mock('next/dynamic', () => ({ default: () => () => null }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyRole: () => false, hasPermission: () => false }) }));
vi.mock('@/services/estate-land-management.service', async importOriginal => ({
  ...await importOriginal<typeof import('@/services/estate-land-management.service')>(),
  estateLandManagementService: {
    getLandBank: mocks.getLandBank, getManagedAsset: mocks.getManagedAsset,
    getLandDemarcations: mocks.getLandDemarcations,
  },
}));
vi.mock('@/services/estate-acquisition.service', () => ({ estateAcquisitionService: { getAcquisitionWorkflowBoard: mocks.board } }));
vi.mock('sonner', () => ({ toast: { error: mocks.error } }));
vi.mock('./DemarcateLandDialog', () => ({ default: () => null }));
vi.mock('./GisAssetLinkDialog', () => ({ default: () => null }));
vi.mock('./LandDocumentsPanel', () => ({ default: () => null }));

const asset = (id: string): EstateManagedAsset => ({
  id, assetCode: `LAND-${id}`, name: `Land ${id}`, gisProvider: 'GeoServer', gisSyncStatus: 'NotLinked',
  boundaryVerified: false, demarcationCount: 0, verifiedDemarcationCount: 0, ownershipHistory: [],
  isReadyForProjectManagement: false, assetType: EstateManagedAssetType.Land,
  status: EstateManagedAssetStatus.LandBank, sourceType: EstateManagedAssetSourceType.Manual,
  currency: 'GHS', isAvailableForLease: false, isAvailableForSale: false,
  isPublishedFromProject: false, isPublishedToExternalPortal: false,
  externalListingType: 'None', externalListingStatus: 'Draft', autoGenerateRentInvoices: false,
  rentGracePeriodDays: 0, rentPenaltyMethod: 'None', rentPenaltyValue: 0, externalListingCurrency: 'GHS',
});

describe('Land Management global search destination', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    vi.clearAllMocks();
    mocks.getLandBank.mockResolvedValue(Array.from({ length: 12 }, (_, index) => asset(String(index))));
    mocks.getManagedAsset.mockResolvedValue(asset('target'));
    mocks.getLandDemarcations.mockResolvedValue([]);
    mocks.board.mockResolvedValue({ stages: [] });
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('waits for the register load, then fetches and selects an off-page search result', async () => {
    let finish!: (records: EstateManagedAsset[]) => void;
    mocks.getLandBank.mockReturnValue(new Promise<EstateManagedAsset[]>(resolve => { finish = resolve; }));
    render(<EstateLandManagementPage />);
    expect(mocks.getManagedAsset).not.toHaveBeenCalled();
    await act(async () => finish(Array.from({ length: 12 }, (_, index) => asset(String(index)))));
    await waitFor(() => expect(mocks.getManagedAsset).toHaveBeenCalledWith('target'));
    await waitFor(() => expect(screen.getAllByText('Land target').length).toBeGreaterThan(0));
    expect(mocks.getLandDemarcations).toHaveBeenLastCalledWith('target');
    expect(mocks.replace).toHaveBeenCalledWith('/estate/land-management', { scroll: false });
    expect(mocks.getManagedAsset).toHaveBeenCalledTimes(1);
  });

  it('does not select a different asset type returned for a forged land link', async () => {
    mocks.getManagedAsset.mockResolvedValue({ ...asset('target'), assetType: EstateManagedAssetType.Property });
    render(<EstateLandManagementPage />);
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('The selected record is not a land asset.'));
    expect(screen.queryByText('Land target')).not.toBeInTheDocument();
  });
});
