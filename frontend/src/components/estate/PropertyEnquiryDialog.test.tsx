import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PropertyEnquiryDialog } from './PropertyEnquiryDialog';
import {
  externalEstateListingsService,
  type ExternalEstateListing,
} from '@/services/external-estate-listings.service';

vi.mock('@/services/external-estate-listings.service', () => ({
  externalEstateListingsService: {
    getEnquiryProfiles: vi.fn(),
    createEnquiry: vi.fn(),
    createPublicEnquiry: vi.fn(),
  },
}));
vi.mock('@/services/settings', () => ({
  settingsService: {
    getPublicSecuritySettings: vi
      .fn()
      .mockResolvedValue({ captchaEnabled: false }),
  },
}));
const listing = {
  id: 'listing-2',
  assetCode: 'LAND-002-PORTION-001',
  name: 'East Legon Parcel',
  externalListingType: 'Sale',
  externalListingCurrency: 'GHS',
  externalSalePrice: 1250000,
  location: 'Accra',
} as ExternalEstateListing;
beforeEach(() => {
  vi.mocked(externalEstateListingsService.getEnquiryProfiles).mockResolvedValue(
    [
      {
        id: 'supplier-1',
        partnerName: 'Supplier Only Ltd',
        partnerType: 'Supplier',
      },
    ]
  );
  vi.mocked(externalEstateListingsService.createEnquiry).mockReset();
  vi.mocked(externalEstateListingsService.createPublicEnquiry).mockReset();
});
afterEach(cleanup);
const mount = () => {
  const onCreated = vi.fn();
  const onClose = vi.fn();
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <PropertyEnquiryDialog
        listing={listing}
        onCreated={onCreated}
        onClose={onClose}
      />
    </QueryClientProvider>
  );
  return { onCreated, onClose };
};
describe('Property enquiry dialog', () => {
  it('shows the selected property and supplier without submitting until a message is sent', async () => {
    const { onCreated } = mount();
    expect(screen.getByText('East Legon Parcel')).toBeVisible();
    expect(screen.getByText('LAND-002-PORTION-001')).toBeVisible();
    await screen.findByRole('option', { name: 'Supplier Only Ltd' });
    expect(externalEstateListingsService.createEnquiry).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeDisabled();
    vi.mocked(externalEstateListingsService.createEnquiry).mockResolvedValue({
      id: 'ticket-1',
      ticketNumber: 'ENQ-001',
      status: 'New',
    });
    fireEvent.change(screen.getByLabelText('Your enquiry'), {
      target: { value: 'Is a site visit available?' },
    });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled()
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(externalEstateListingsService.createEnquiry).toHaveBeenCalledWith(
      'listing-2',
      expect.objectContaining({
        businessPartnerId: 'supplier-1',
        message: 'Is a site visit available?',
        submissionId: expect.any(String),
      })
    );
  });
  it('retains the message and submission identifier after a failed response', async () => {
    mount();
    await screen.findByRole('option', { name: 'Supplier Only Ltd' });
    vi.mocked(externalEstateListingsService.createEnquiry)
      .mockRejectedValueOnce(new Error('Connection interrupted'))
      .mockResolvedValueOnce({
        id: 'ticket-2',
        ticketNumber: 'ENQ-002',
        status: 'New',
      });
    fireEvent.change(screen.getByLabelText('Your enquiry'), {
      target: { value: 'Please explain the payment terms.' },
    });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled()
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));
    await screen.findByText('Connection interrupted');
    expect(screen.getByLabelText('Your enquiry')).toHaveValue(
      'Please explain the payment terms.'
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));
    await waitFor(() =>
      expect(externalEstateListingsService.createEnquiry).toHaveBeenCalledTimes(
        2
      )
    );
    const calls = vi.mocked(externalEstateListingsService.createEnquiry).mock
      .calls;
    expect(calls[0][1].submissionId).toBe(calls[1][1].submissionId);
  });

  it('submits an anonymous prospect with contact details and no internal or business-partner fields', async () => {
    const onCreated = vi.fn();
    vi.mocked(
      externalEstateListingsService.createPublicEnquiry
    ).mockResolvedValue({
      id: 'ticket-public',
      ticketNumber: 'ENQ-PUBLIC-1',
      status: 'New',
    });
    render(
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false } } })
        }
      >
        <PropertyEnquiryDialog
          listing={listing}
          onCreated={onCreated}
          onClose={vi.fn()}
          publicMode
        />
      </QueryClientProvider>
    );

    fireEvent.change(screen.getByLabelText('Name'), {
      target: { value: 'Ama Prospect' },
    });
    fireEvent.change(screen.getByLabelText('Phone'), {
      target: { value: '+233 20 000 0000' },
    });
    fireEvent.change(screen.getByLabelText('Alternative phone'), {
      target: { value: '+233 24 000 0000' },
    });
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'ama@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Preferred contact method'), {
      target: { value: 'Either' },
    });
    fireEvent.change(screen.getByLabelText('Your enquiry'), {
      target: { value: 'I would like to arrange a viewing.' },
    });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled()
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(
      externalEstateListingsService.createPublicEnquiry
    ).toHaveBeenCalledWith('listing-2', {
      submissionId: expect.any(String),
      contactName: 'Ama Prospect',
      contactPhone: '+233 20 000 0000',
      alternativePhoneNumber: '+233 24 000 0000',
      contactEmail: 'ama@example.com',
      preferredContactMethod: 'Either',
      message: 'I would like to arrange a viewing.',
      captchaToken: undefined,
    });
    expect(externalEstateListingsService.createEnquiry).not.toHaveBeenCalled();
    const publicPayload = vi.mocked(
      externalEstateListingsService.createPublicEnquiry
    ).mock.calls[0][1] as Record<string, unknown>;
    expect(publicPayload).not.toHaveProperty('businessPartnerId');
    expect(publicPayload).not.toHaveProperty('assignedEmployeeId');
    expect(publicPayload).not.toHaveProperty('status');
    expect(publicPayload).not.toHaveProperty('opportunityId');
  });
});
