import React from 'react';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PropertyEnquiriesPage from './page';

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  toast: vi.fn(),
  prospectStatus: 'New',
  depositThresholdMet: false,
  handoffReady: false,
  queueError: '',
  partnerMatches: [] as Array<{
    id: string;
    partnerCode: string;
    partnerName: string;
    email: string;
    phone: string;
    matchedOn: string[];
    approvalStatus: string;
  }>,
}));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('id=enquiry-42'),
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
  useAuth: () => ({ hasPermission: () => false }),
}));

describe('property enquiry contact linkage', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    mocks.prospectStatus = 'New';
    mocks.depositThresholdMet = false;
    mocks.handoffReady = false;
    mocks.queueError = '';
    mocks.partnerMatches = [];
    mocks.toast.mockReset();
    mocks.request.mockReset();
    mocks.request.mockImplementation(
      async (endpoint: string, options?: { method?: string }) => {
        if (endpoint.endsWith('/prospect/contacted')) {
          mocks.prospectStatus = 'Contacted';
          return {
            success: true,
            data: {
              ticketId: 'enquiry-42',
              leadId: 'lead-42',
              status: 'Contacted',
            },
          };
        }
        if (endpoint.endsWith('/prospect/opportunity') && options?.method === 'POST') {
          throw new Error('This property already has an active reservation.');
        }
        if (endpoint.endsWith('/prospect/business-partner-matches')) {
          return { success: true, data: mocks.partnerMatches };
        }
        if (endpoint.includes('?page=')) {
          if (mocks.queueError) throw new Error(mocks.queueError);
          return { success: true, data: [], totalCount: 0 };
        }
        if (endpoint.endsWith('/estate-handoff') && options?.method === 'POST') {
          return {
            success: true,
            data: {
              procedureCaseId: 'estate-case-42',
              referenceNumber: 'EST-042',
              alreadyExists: false,
            },
          };
        }
        if (endpoint.endsWith('/estate-handoff')) {
          return {
            success: true,
            data: mocks.handoffReady
              ? {
                  canHandoff: true,
                  opportunity: {
                    id: 'opportunity-42',
                    stage: 'Won',
                    isWon: true,
                    amount: 10000,
                    currency: 'GHS',
                    actualCloseDate: '2026-10-07T10:00:00Z',
                  },
                  salesOrder: {
                    id: 'sales-order-42',
                    reference: 'SO-000042',
                    status: 'Completed',
                    agreedAmount: 10000,
                    amountPaid: 10000,
                    currency: 'GHS',
                    completedAt: '2026-10-07T10:00:00Z',
                    paymentReference: 'PAY-042',
                  },
                }
              : { canHandoff: false },
          };
        }
        if (endpoint.endsWith('/prospect')) {
          return {
            success: true,
            data: {
              ticketId: 'enquiry-42',
              leadId: 'lead-42',
              status: mocks.prospectStatus,
              agreedAmount: 10000,
              currency: 'GHS',
              depositRequirementType: 'Full',
              requiredDeposit: 10000,
              clearedDeposit: mocks.depositThresholdMet ? 10000 : 0,
              depositThresholdMet: mocks.depositThresholdMet,
              businessPartnerId: mocks.handoffReady ? 'customer-42' : null,
            },
          };
        }
        if (endpoint.endsWith('/enquiry-42')) {
          return {
            success: true,
            data: {
              id: 'enquiry-42',
              ticketNumber: 'PE-042',
              subject: 'Facility enquiry',
              status: 'Acknowledged',
              messages: [],
              propertyListing: {
                price: 10000,
                currency: 'GHS',
                contactName: 'Ama Mensah',
                contactEmail: 'ama@example.test',
              },
            },
          };
        }
        throw new Error(
          `Unexpected ${options?.method ?? 'GET'} request to ${endpoint}`
        );
      }
    );
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('records Contact made against the selected enquiry before enabling qualification', async () => {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });
    render(
      <QueryClientProvider client={client}>
        <PropertyEnquiriesPage />
      </QueryClientProvider>
    );

    fireEvent.change(await screen.findByLabelText('Activity notes'), {
      target: { value: 'Spoke with the enquirer and confirmed interest.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Record contact' }));

    await waitFor(() =>
      expect(mocks.request).toHaveBeenCalledWith(
        '/ehc/internal/property-enquiries/enquiry-42/prospect/contacted',
        {
          method: 'POST',
          body: JSON.stringify({
            notes: 'Spoke with the enquirer and confirmed interest.',
          }),
        }
      )
    );
    await waitFor(() =>
      expect(
        screen.queryByText(/Before qualifying, go to Internal activity below/)
      ).not.toBeInTheDocument()
    );
    expect(screen.getByRole('button', { name: 'Mark qualified' })).toBeEnabled();
    client.clear();
  });

  it('shows the inherited opportunity currency as read only', async () => {
    mocks.prospectStatus = 'Qualified';
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });
    render(
      <QueryClientProvider client={client}>
        <PropertyEnquiriesPage />
      </QueryClientProvider>
    );

    const currency = await screen.findByRole('combobox', {
      name: 'Opportunity Currency',
    });
    expect(currency).toHaveTextContent('GHS');
    expect(currency).toBeDisabled();
    expect(
      screen.getByText('Inherited from the listed property.')
    ).toBeInTheDocument();
    client.clear();
  });

  it('keeps qualification currency read only from the listed property', async () => {
    mocks.prospectStatus = 'Contacted';
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><PropertyEnquiriesPage /></QueryClientProvider>);

    const currency = await screen.findByLabelText('Currency');
    expect(currency).toHaveValue('GHS');
    expect(currency).toHaveAttribute('readonly');
    client.clear();
  });

  it('shows opportunity creation errors as a toast near the user action', async () => {
    mocks.prospectStatus = 'Qualified';
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><PropertyEnquiriesPage /></QueryClientProvider>);

    fireEvent.change(await screen.findByLabelText('Expected close date'), {
      target: { value: '2026-12-31' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Create Opportunity' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Create Opportunity' }));

    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith({
      title: 'Opportunity could not be created',
      description: 'This property already has an active reservation.',
      variant: 'destructive',
    }));
    expect(screen.queryByText('This property already has an active reservation.')).not.toBeInTheDocument();
    client.clear();
  });

  it('checks for an existing customer and opens creation when the deposit threshold is met', async () => {
    mocks.prospectStatus = 'Qualified';
    mocks.depositThresholdMet = true;
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });
    render(
      <QueryClientProvider client={client}>
        <PropertyEnquiriesPage />
      </QueryClientProvider>
    );

    const createCustomer = await screen.findByRole('button', {
      name: 'Create customer',
    });
    expect(createCustomer).toBeEnabled();
    fireEvent.click(createCustomer);

    await waitFor(() =>
      expect(mocks.request).toHaveBeenCalledWith(
        '/ehc/internal/property-enquiries/enquiry-42/prospect/business-partner-matches',
        { method: 'GET' }
      )
    );
    expect(
      await screen.findByRole('heading', {
        name: 'Create customer Business Partner',
      })
    ).toBeInTheDocument();
    client.clear();
  });

  it('disables Estate handoff immediately after a successful handoff', async () => {
    mocks.prospectStatus = 'Converted';
    mocks.depositThresholdMet = true;
    mocks.handoffReady = true;
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });
    render(
      <QueryClientProvider client={client}>
        <PropertyEnquiriesPage />
      </QueryClientProvider>
    );

    const handoffButton = await screen.findByRole('button', {
      name: 'Hand off to Estate',
    });
    await waitFor(() => expect(handoffButton).toBeEnabled());
    fireEvent.click(handoffButton);
    const confirmation = await screen.findByRole('dialog');
    fireEvent.click(
      within(confirmation).getByRole('button', { name: 'Hand off to Estate' })
    );

    const completedButton = await screen.findByRole('button', {
      name: 'Handed to Estate',
    });
    expect(completedButton).toBeDisabled();
    expect(mocks.request).toHaveBeenCalledWith(
      '/ehc/internal/property-enquiries/enquiry-42/estate-handoff',
      expect.objectContaining({ method: 'POST' })
    );
    client.clear();
  });

  it('reports query failures through a toast instead of a page-top error', async () => {
    mocks.queueError = 'The enquiry register is temporarily unavailable.';
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });
    render(
      <QueryClientProvider client={client}>
        <PropertyEnquiriesPage />
      </QueryClientProvider>
    );

    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith({
        title: 'Property enquiry request failed',
        description: 'The enquiry register is temporarily unavailable.',
        variant: 'destructive',
      })
    );
    expect(
      screen.queryByText('The enquiry register is temporarily unavailable.')
    ).not.toBeInTheDocument();
    client.clear();
  });
});
