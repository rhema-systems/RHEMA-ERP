import React from 'react';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PropertyEnquiriesPage from './page';

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  prospectStatus: 'New',
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
  useToast: () => ({ toast: vi.fn() }),
}));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: () => false }),
}));

describe('property enquiry contact linkage', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    mocks.prospectStatus = 'New';
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
        if (endpoint.includes('?page=')) {
          return { success: true, data: [], totalCount: 0 };
        }
        if (endpoint.endsWith('/estate-handoff')) {
          return { success: true, data: { canHandoff: false } };
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
              clearedDeposit: 0,
              depositThresholdMet: false,
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
      screen.getByText('Inherited from the qualified prospect or property listing.')
    ).toBeInTheDocument();
    client.clear();
  });
});
