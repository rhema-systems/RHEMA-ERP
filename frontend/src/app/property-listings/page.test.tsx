import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import PublicPropertyListingsPage from './page';
import { externalEstateListingsService, type ExternalEstateListing } from '@/services/external-estate-listings.service';

vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/external-estate-listings.service', () => ({
  externalEstateListingsService: {
    getPublicListingsPage: vi.fn(),
    getListingImage: vi.fn(),
  },
}));
vi.mock('@/components/estate/PropertyEnquiryDialog', () => ({
  PropertyEnquiryDialog: ({ listing }: { listing: ExternalEstateListing }) => (
    <div data-testid="enquiry-dialog">{listing.name}</div>
  ),
}));

const listings = [
  {
    id: 'public-sale',
    assetCode: 'SALE-001',
    name: 'East Legon Parcel',
    assetType: 'Land',
    status: 'Available',
    externalListingType: 'Sale',
    externalListingCurrency: 'GHS',
    sourceLabel: 'Estate',
  },
  {
    id: 'public-rent',
    assetCode: 'RENT-001',
    name: 'Airport Apartment',
    assetType: 'Property',
    status: 'Available',
    externalListingType: 'Rent',
    externalListingCurrency: 'GHS',
    sourceLabel: 'Estate',
  },
] as ExternalEstateListing[];

beforeEach(() => {
  vi.mocked(externalEstateListingsService.getPublicListingsPage).mockResolvedValue({
    items: listings,
    page: 1,
    pageSize: 10,
    totalCount: listings.length,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  });
  vi.mocked(externalEstateListingsService.getListingImage).mockResolvedValue(null);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('Public property listings', () => {
  it('opens the listing image preview and starts enquiry from the preview', async () => {
    render(<PublicPropertyListingsPage />);

    const imageButton = await screen.findByRole('button', {
      name: 'Preview image for Airport Apartment',
    });
    expect(externalEstateListingsService.getListingImage).toHaveBeenCalledWith(listings[1]);

    fireEvent.click(imageButton);
    const preview = await screen.findByRole('dialog');
    expect(within(preview).getByRole('img', { name: 'Airport Apartment' })).toBeInTheDocument();

    fireEvent.click(within(preview).getByRole('button', { name: 'Enquiry' }));
    expect(screen.getByTestId('enquiry-dialog')).toHaveTextContent('Airport Apartment');
  });
});
