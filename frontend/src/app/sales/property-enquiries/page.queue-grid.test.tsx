import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PropertyEnquiriesPage from './page';

const mocks = vi.hoisted(() => ({ request: vi.fn() }));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('@/services/api.service', () => ({
  apiService: { request: mocks.request },
}));
vi.mock('@/services/salesReferenceService', () => ({
  salesReferenceService: { getActiveCurrencies: async () => [] },
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

const row = {
  id: 'enquiry-1',
  ticketNumber: 'PE-001',
  subject: 'Apartment viewing',
  requesterName: 'Ama Mensah',
  status: 'Acknowledged',
  createdAt: '2026-10-01T12:00:00Z',
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  render(
    <QueryClientProvider client={client}>
      <PropertyEnquiriesPage />
    </QueryClientProvider>
  );
  return client;
}

describe('property enquiry register grid', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    mocks.request.mockReset();
    mocks.request.mockImplementation(async (endpoint: string) => {
      if (endpoint.includes('?page=')) {
        const params = new URLSearchParams(endpoint.split('?')[1]);
        if (params.get('search') === 'missing') {
          return { success: true, data: [], totalCount: 0, pageSize: 25 };
        }
        return { success: true, data: [row], totalCount: 30, pageSize: 25 };
      }
      if (endpoint.endsWith('/estate-handoff'))
        return { success: true, data: { canHandoff: false } };
      if (endpoint.endsWith('/prospect'))
        return { success: true, data: null };
      return {
        success: true,
        data: { ...row, description: 'Please call me', messages: [] },
      };
    });
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('sends search and filters to the paged API and keeps the selected detail', async () => {
    const client = renderPage();
    const table = screen.getByRole('table', { name: 'Property enquiries' });
    await within(table).findByRole('button', { name: /PE-001/ });
    fireEvent.click(within(table).getByRole('button', { name: /PE-001/ }));
    await screen.findByRole('heading', { name: 'PE-001' });

    fireEvent.change(screen.getByRole('combobox', { name: 'Filter by status' }), {
      target: { value: 'Acknowledged' },
    });
    fireEvent.change(screen.getByRole('combobox', { name: 'Filter by CRM linkage' }), {
      target: { value: 'linked' },
    });
    fireEvent.change(screen.getByRole('combobox', { name: 'Rows per page' }), {
      target: { value: '10' },
    });
    fireEvent.change(screen.getByRole('searchbox', { name: 'Search enquiries' }), {
      target: { value: 'missing' },
    });

    await waitFor(() => {
      expect(mocks.request.mock.calls.some(([endpoint]) => {
        if (!endpoint.includes('?page=')) return false;
        const params = new URLSearchParams(endpoint.split('?')[1]);
        return params.get('page') === '1'
          && params.get('pageSize') === '10'
          && params.get('search') === 'missing'
          && params.get('status') === 'Acknowledged'
          && params.get('crmLinked') === 'true';
      })).toBe(true);
    });
    expect(screen.getByText('No enquiries match these filters.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'PE-001' })).toBeInTheDocument();
    client.clear();
  });

  it('requests the next server page without loading every enquiry', async () => {
    const client = renderPage();
    await screen.findByText('Showing 1–25 of 30');
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(mocks.request).toHaveBeenCalledWith(
      '/ehc/internal/property-enquiries?page=2&pageSize=25',
      { method: 'GET' }
    ));
    await screen.findByText('Page 2 of 2');
    client.clear();
  });
});
