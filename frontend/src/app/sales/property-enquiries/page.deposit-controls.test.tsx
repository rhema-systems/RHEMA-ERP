import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PropertyEnquiriesPage from './page';
import {
  RECEIVE_PROSPECT_DEPOSIT_PERMISSION,
  REVERSE_PROSPECT_DEPOSIT_PERMISSION,
} from '@/lib/sales/property-enquiry-deposit-access';

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  toast: vi.fn(),
  permissions: new Set<string>(),
  prospectStatus: 'Opportunity',
  businessPartnerId: null as string | null,
  depositThresholdMet: false,
  depositError: null as string | null,
}));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('id=enquiry-1'),
  useRouter: () => ({ push: vi.fn() }),
}));
vi.mock('@/services/api.service', () => ({
  apiService: { request: mocks.request },
}));
vi.mock('@/services/salesReferenceService', () => ({
  salesReferenceService: {
    getActiveCurrencies: async () => [
      { code: 'GHS', name: 'Ghana Cedi', isBaseCurrency: true },
      { code: 'USD', name: 'US Dollar', isBaseCurrency: false },
    ],
  },
}));
vi.mock('@/components/estate/PropertyEnquiryDetails', () => ({
  PropertyEnquiryDetails: () => null,
}));
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => mocks.permissions.has(permission),
  }),
}));

const renderPage = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  render(
    <QueryClientProvider client={client}>
      <PropertyEnquiriesPage />
    </QueryClientProvider>
  );
  return client;
};

describe('property enquiry deposit controls', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    mocks.permissions.clear();
    mocks.toast.mockReset();
    mocks.prospectStatus = 'Opportunity';
    mocks.businessPartnerId = null;
    mocks.depositThresholdMet = false;
    mocks.depositError = null;
    mocks.request.mockReset();
    mocks.request.mockImplementation(async (endpoint: string, options?: RequestInit) => {
      if (endpoint.endsWith('/prospect/qualify')) {
        throw new Error('Record Sales contact before qualifying this prospect');
      }
      if (endpoint.includes('?page=')) {
        return { success: true, data: [], totalCount: 0 };
      }
      if (endpoint.endsWith('/estate-handoff')) {
        return {
          success: true,
          data: {
            canHandoff: false,
            opportunity: {
              id: 'opportunity-1',
              referenceNumber: 'OPP-001',
              stage: 'Qualified',
              amount: 10000,
              currency: 'GHS',
            },
          },
        };
      }
      if (endpoint.endsWith('/prospect/deposits') && options?.method === 'POST') {
        if (mocks.depositError) throw new Error(mocks.depositError);
        return {
          success: true,
          data: {
            id: 'new-deposit-1',
            receiptNumber: 'PDR-003',
            amount: 500,
            currency: 'GHS',
            paymentMethod: 'BankTransfer',
            status: 'Pending',
            receivedAt: '2026-10-01T10:00:00.000Z',
          },
        };
      }
      if (endpoint.endsWith('/prospect/deposits')) {
        return {
          success: true,
          data: [
            {
              id: 'pending-1',
              receiptNumber: 'PDR-001',
              amount: 1000,
              currency: 'GHS',
              paymentMethod: 'BankTransfer',
              status: 'Pending',
              receivedAt: '2026-10-01T08:00:00.000Z',
            },
            {
              id: 'cleared-1',
              receiptNumber: 'PDR-002',
              amount: 2000,
              currency: 'GHS',
              paymentMethod: 'BankTransfer',
              status: 'Cleared',
              receivedAt: '2026-09-30T08:00:00.000Z',
              clearedAt: '2026-10-01T09:00:00.000Z',
            },
          ],
        };
      }
      if (endpoint.endsWith('/prospect')) {
        return {
          success: true,
          data: {
            ticketId: 'enquiry-1',
            leadId: 'lead-1',
            opportunityId: 'opportunity-1',
            businessPartnerId: mocks.businessPartnerId,
            businessPartnerCode: mocks.businessPartnerId ? 'CUS-001' : null,
            businessPartnerName: mocks.businessPartnerId
              ? 'Public Enquirer'
              : null,
            status: mocks.prospectStatus,
            agreedAmount: 10000,
            currency: 'GHS',
            depositRequirementType: 'Percentage',
            requiredDeposit: 3000,
            clearedDeposit: 2000,
            depositThresholdMet: mocks.depositThresholdMet,
          },
        };
      }
      return {
        success: true,
        data: {
          id: 'enquiry-1',
          ticketNumber: 'PE-001',
          subject: 'Property enquiry',
          status: 'Acknowledged',
          messages: [],
          propertyListing: {
            listingReference: 'PROP-001',
            listingType: 'Sale',
            contactName: 'Public Enquirer',
          },
        },
      };
    });
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('shows the receipt date input but hides Finance actions without permission', async () => {
    const client = renderPage();

    expect(await screen.findByLabelText('Receipt date and time')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/PDR-001/)).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Clear' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reverse' })).not.toBeInTheDocument();
    client.clear();
  });

  it('uses the linked prospect currency and prevents deposit currency editing', async () => {
    const client = renderPage();

    const currency = await screen.findByLabelText('Deposit currency');
    expect(currency).toHaveValue('GHS');
    expect(currency).toBeDisabled();
    client.clear();
  });

  it('shows deposit recording failures in a toast instead of the page alert', async () => {
    mocks.depositError = 'An active deposit policy is required.';
    const client = renderPage();

    fireEvent.change(await screen.findByLabelText('Deposit amount'), {
      target: { value: '500' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Record deposit' }));

    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith({
      title: 'Deposit could not be recorded',
      description: 'An active deposit policy is required.',
      variant: 'destructive',
    }));
    expect(screen.queryByText('An active deposit policy is required.')).not.toBeInTheDocument();
    client.clear();
  });

  it('shows only the controlled actions granted by Finance permissions', async () => {
    mocks.permissions.add(RECEIVE_PROSPECT_DEPOSIT_PERMISSION);
    mocks.permissions.add(REVERSE_PROSPECT_DEPOSIT_PERMISSION);
    const client = renderPage();

    expect(await screen.findByRole('button', { name: 'Clear' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reverse' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
    expect(screen.getByLabelText('Clearance date and time')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    fireEvent.click(screen.getByRole('button', { name: 'Reverse' }));
    expect(screen.getByLabelText('Reversal date and time')).toBeInTheDocument();
    client.clear();
  });

  it('shows the server qualification rule as a friendly UI error', async () => {
    mocks.prospectStatus = 'Contacted';
    const client = renderPage();

    fireEvent.change(await screen.findByLabelText('Agreed property amount'), {
      target: { value: '10000' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Mark qualified' }));

    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith({
      title: 'Prospect could not be qualified',
      description: 'Record Sales contact before qualifying this prospect',
      variant: 'destructive',
    }));
    client.clear();
  });

  it('requires final customer conversion before exposing Sales Order creation', async () => {
    mocks.businessPartnerId = 'customer-1';
    mocks.prospectStatus = 'Opportunity';
    const client = renderPage();

    const finalize = await screen.findByRole('button', {
      name: 'Finalize customer conversion',
    });
    expect(finalize).toBeDisabled();
    expect(
      screen.queryByRole('link', { name: 'Sales Order' })
    ).not.toBeInTheDocument();
    client.clear();
  });

  it('carries the converted prospect and property lineage into Sales Order creation', async () => {
    mocks.businessPartnerId = 'customer-1';
    mocks.depositThresholdMet = true;
    mocks.prospectStatus = 'Converted';
    const client = renderPage();

    const link = await screen.findByRole('link', { name: 'Sales Order' });
    const href = link.getAttribute('href') || '';
    expect(href).toContain('/sales/orders/create?');
    expect(href).toContain('customerId=customer-1');
    expect(href).toContain('opportunityId=opportunity-1');
    expect(href).toContain('propertyReference=PROP-001');
    expect(href).toContain('propertyType=Sale');
    client.clear();
  });
});
