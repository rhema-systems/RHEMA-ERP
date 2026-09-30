import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import ExternalPropertyListingsPage from './page';
import { externalEstateListingsService, type ExternalEstateListing } from '@/services/external-estate-listings.service';

vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/external-estate-listings.service', () => ({
  externalEstateListingsService: {
    getListingsPage: vi.fn(),
    getListingImage: vi.fn(),
  },
}));
vi.mock('@/components/estate/PropertyEnquiryDialog', () => ({
  PropertyEnquiryDialog: ({ listing }: { listing: ExternalEstateListing }) => (
    <div data-testid="enquiry-dialog">{listing.name}</div>
  ),
}));

const listings = [
  { id: 'sale', assetCode: 'SALE-001', name: 'East Legon Parcel', assetType: 'Land', status: 'Available', externalListingType: 'Sale', externalListingCurrency: 'GHS', sourceLabel: 'Estate' },
  { id: 'rent', assetCode: 'RENT-001', name: 'Airport Apartment', assetType: 'Property', status: 'Available', externalListingType: 'Rent', externalListingCurrency: 'GHS', sourceLabel: 'Estate' },
  { id: 'lease', assetCode: 'LEASE-001', name: 'Tema Office', assetType: 'Property', status: 'Available', externalListingType: 'Lease', externalListingCurrency: 'GHS', sourceLabel: 'Estate' },
] as ExternalEstateListing[];

beforeEach(() => {
  vi.mocked(externalEstateListingsService.getListingsPage).mockResolvedValue({
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

describe('External property listings', () => {
  it('keeps enquiry available on a selected card and opens it from the image preview', async () => {
    render(<ExternalPropertyListingsPage />);
    const imageButton = await screen.findByRole('button', { name: 'Preview image for Airport Apartment' });
    const card = imageButton.parentElement;
    expect(card).not.toBeNull();

    fireEvent.click(within(card as HTMLElement).getByText('Airport Apartment').closest('button') as HTMLButtonElement);
    await waitFor(() => expect(within(card as HTMLElement).getByRole('button', { name: 'Enquiry' })).toBeInTheDocument());
    expect(screen.getByRole('heading', { name: 'Airport Apartment' })).toBeInTheDocument();

    fireEvent.click(imageButton);
    const preview = await screen.findByRole('dialog');
    expect(within(preview).getByRole('img', { name: 'Airport Apartment' })).toBeInTheDocument();
    fireEvent.click(within(preview).getByRole('button', { name: 'Enquiry' }));
    expect(screen.getByTestId('enquiry-dialog')).toHaveTextContent('Airport Apartment');
  });

  it('distinguishes sale, rent, and lease badges', async () => {
    render(<ExternalPropertyListingsPage />);
    await screen.findByRole('button', { name: 'Preview image for Tema Office' });
    const cardFor = (name: string) => screen.getByRole('button', { name: `Preview image for ${name}` }).parentElement as HTMLElement;
    expect(within(cardFor('East Legon Parcel')).getByText('For sale')).toHaveClass('bg-emerald-100');
    expect(within(cardFor('Airport Apartment')).getByText('For rent')).toHaveClass('bg-blue-100');
    expect(within(cardFor('Tema Office')).getByText('For lease')).toHaveClass('bg-amber-100');
  });
});
