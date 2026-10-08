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

const mocks = vi.hoisted(() => ({ toast: vi.fn() }));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/services/external-estate-listings.service', () => ({
  externalEstateListingsService: {
    getEnquiryProfiles: vi.fn(),
    getEstateIdentificationTypes: vi.fn(),
    createEnquiry: vi.fn(),
    createPublicEnquiry: vi.fn(),
    requestPublicEnquiryContactChallenge: vi.fn(),
    verifyPublicEnquiryContact: vi.fn(),
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
  assetCode: 'LAND-002-D001',
  name: 'LAND-002-D001',
  externalListingType: 'Sale',
  externalListingCurrency: 'GHS',
  externalSalePrice: 1250000,
  location: 'Accra',
} as ExternalEstateListing;
beforeEach(() => {
  vi.mocked(
    externalEstateListingsService.getEstateIdentificationTypes
  ).mockResolvedValue([
    { id: 'ghana-card', name: 'Ghana Card', code: 'GHA' },
  ]);
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
  vi.mocked(
    externalEstateListingsService.requestPublicEnquiryContactChallenge
  ).mockReset();
  vi.mocked(
    externalEstateListingsService.verifyPublicEnquiryContact
  ).mockReset();
  mocks.toast.mockReset();
});
afterEach(cleanup);
const enterIdentification = async () => {
  await screen.findByRole('option', { name: 'Ghana Card' });
  fireEvent.change(screen.getByLabelText('Identification type'), {
    target: { value: 'ghana-card' },
  });
  fireEvent.change(screen.getByLabelText('Identification number'), {
    target: { value: 'GHA-123456789-0' },
  });
};
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
    expect(screen.getAllByText('LAND-002-D001')).toHaveLength(2);
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
    await enterIdentification();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled()
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(externalEstateListingsService.createEnquiry).toHaveBeenCalledWith(
      'listing-2',
      expect.objectContaining({
        businessPartnerId: 'supplier-1',
        identificationTypeId: 'ghana-card',
        identificationNumber: 'GHA-123456789-0',
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
    await enterIdentification();
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

  it('requires OTP verification for the selected email and submits only that contact channel', async () => {
    const onCreated = vi.fn();
    vi.mocked(
      externalEstateListingsService.requestPublicEnquiryContactChallenge
    ).mockResolvedValue({
      channel: 'Email',
      maskedContact: 'a***@example.com',
      expiresInSeconds: 600,
    });
    vi.mocked(
      externalEstateListingsService.verifyPublicEnquiryContact
    ).mockResolvedValue({
      verificationToken: 'verified-contact-token',
      expiresAtUtc: '2026-10-02T10:10:00Z',
      profile: {
        contactName: 'Ama Returning Prospect',
        contactEmail: 'ama@example.com',
        contactPhone: '+233240000000',
        requiresPortalLogin: false,
        externalPortalPath: null,
      },
    });
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

    expect(
      screen.queryByLabelText('Alternative phone')
    ).not.toBeInTheDocument();
    expect(
      screen.getByLabelText('Phone', { selector: 'input' })
    ).toBeDisabled();
    expect(screen.getByLabelText('Email', { selector: 'input' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Email', { selector: 'input' }), {
      target: { value: 'ama@example.com' },
    });
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Send verification code' })
      ).toBeEnabled()
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Send verification code' })
    );
    await screen.findByLabelText('Verification code');
    expect(
      externalEstateListingsService.requestPublicEnquiryContactChallenge
    ).toHaveBeenCalledWith({
      listingId: 'listing-2',
      channel: 'Email',
      contact: 'ama@example.com',
      captchaToken: undefined,
    });
    fireEvent.change(screen.getByLabelText('Verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Verify contact' }));
    await screen.findByText('Contact verified');
    expect(screen.getByLabelText('Name')).toHaveValue('Ama Returning Prospect');
    fireEvent.change(screen.getByLabelText('Your enquiry'), {
      target: { value: 'I would like to arrange a viewing.' },
    });
    await enterIdentification();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled()
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send enquiry' }));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(
      externalEstateListingsService.createPublicEnquiry
    ).toHaveBeenCalledWith('listing-2', {
      submissionId: expect.any(String),
      contactName: 'Ama Returning Prospect',
      contactPhone: undefined,
      contactEmail: 'ama@example.com',
      preferredContactMethod: 'Email',
      contactVerificationToken: 'verified-contact-token',
      identificationTypeId: 'ghana-card',
      identificationNumber: 'GHA-123456789-0',
      message: 'I would like to arrange a viewing.',
    });
    expect(externalEstateListingsService.createEnquiry).not.toHaveBeenCalled();
    const publicPayload = vi.mocked(
      externalEstateListingsService.createPublicEnquiry
    ).mock.calls[0][1] as unknown as Record<string, unknown>;
    expect(publicPayload).not.toHaveProperty('businessPartnerId');
    expect(publicPayload).not.toHaveProperty('assignedEmployeeId');
    expect(publicPayload).not.toHaveProperty('status');
    expect(publicPayload).not.toHaveProperty('opportunityId');
    expect(publicPayload).not.toHaveProperty('alternativePhoneNumber');
    expect(publicPayload).not.toHaveProperty('captchaToken');
  }, 10_000);

  it('preserves the entered name when a new public contact has no saved profile name', async () => {
    vi.mocked(
      externalEstateListingsService.requestPublicEnquiryContactChallenge
    ).mockResolvedValue({
      channel: 'Email',
      maskedContact: 'm***@example.com',
      expiresInSeconds: 600,
    });
    vi.mocked(
      externalEstateListingsService.verifyPublicEnquiryContact
    ).mockResolvedValue({
      verificationToken: 'new-contact-token',
      expiresAtUtc: '2026-10-02T10:10:00Z',
      profile: {
        contactName: null,
        contactEmail: 'michael@example.com',
        contactPhone: null,
        requiresPortalLogin: false,
        externalPortalPath: null,
      },
    });
    render(
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false } } })
        }
      >
        <PropertyEnquiryDialog
          listing={listing}
          onCreated={vi.fn()}
          onClose={vi.fn()}
          publicMode
        />
      </QueryClientProvider>
    );

    fireEvent.change(screen.getByLabelText('Name'), {
      target: { value: 'Michael Owusu' },
    });
    fireEvent.change(screen.getByLabelText('Email', { selector: 'input' }), {
      target: { value: 'michael@example.com' },
    });
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Send verification code' })
      ).toBeEnabled()
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Send verification code' })
    );
    await screen.findByLabelText('Verification code');
    fireEvent.change(screen.getByLabelText('Verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Verify contact' }));
    await screen.findByText('Contact verified');
    expect(screen.getByLabelText('Name')).toHaveValue('Michael Owusu');
    fireEvent.change(screen.getByLabelText('Your enquiry'), {
      target: { value: 'Please send the viewing details.' },
    });
    await enterIdentification();
    expect(screen.getByRole('button', { name: 'Send enquiry' })).toBeEnabled();
  });

  it('enables the phone input only when Phone is selected and accepts an international number', async () => {
    render(
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false } } })
        }
      >
        <PropertyEnquiryDialog
          listing={listing}
          onCreated={vi.fn()}
          onClose={vi.fn()}
          publicMode
        />
      </QueryClientProvider>
    );

    fireEvent.click(
      screen.getByLabelText('Phone', { selector: '[role="radio"]' })
    );
    expect(
      screen.getByLabelText('Email', { selector: 'input' })
    ).toBeDisabled();
    expect(screen.getByLabelText('Phone', { selector: 'input' })).toBeEnabled();
    expect(screen.getByLabelText('Phone country calling code')).toBeEnabled();
    fireEvent.change(screen.getByLabelText('Phone', { selector: 'input' }), {
      target: { value: '201234567' },
    });
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Send verification code' })
      ).toBeEnabled()
    );
  });

  it('blocks public submission and offers portal sign-in for an existing customer contact', async () => {
    vi.mocked(
      externalEstateListingsService.requestPublicEnquiryContactChallenge
    ).mockResolvedValue({
      channel: 'Email',
      maskedContact: 'c***@example.com',
      expiresInSeconds: 600,
    });
    vi.mocked(
      externalEstateListingsService.verifyPublicEnquiryContact
    ).mockResolvedValue({
      verificationToken: 'customer-token',
      expiresAtUtc: '2026-10-02T10:10:00Z',
      profile: {
        contactName: 'Existing Customer',
        contactEmail: 'customer@example.com',
        contactPhone: null,
        requiresPortalLogin: true,
        externalPortalPath:
          '/login?redirect=%2Fexternal-portal%2Fproperty-listings',
      },
    });
    render(
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false } } })
        }
      >
        <PropertyEnquiryDialog
          listing={listing}
          onCreated={vi.fn()}
          onClose={vi.fn()}
          publicMode
        />
      </QueryClientProvider>
    );

    fireEvent.change(screen.getByLabelText('Email', { selector: 'input' }), {
      target: { value: 'customer@example.com' },
    });
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Send verification code' })
      ).toBeEnabled()
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Send verification code' })
    );
    await screen.findByLabelText('Verification code');
    fireEvent.change(screen.getByLabelText('Verification code'), {
      target: { value: '654321' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Verify contact' }));

    expect(
      await screen.findByText('Continue in the customer portal')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Send enquiry', hidden: true })
    ).toBeDisabled();
    expect(
      externalEstateListingsService.createPublicEnquiry
    ).not.toHaveBeenCalled();
  });
});
