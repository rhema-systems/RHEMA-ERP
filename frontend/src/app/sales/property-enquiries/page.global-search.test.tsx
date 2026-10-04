import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PropertyEnquiriesPage from './page';

const mocks = vi.hoisted(() => ({ id: 'one', request: vi.fn() }));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(`id=${mocks.id}`) }));
vi.mock('@/services/api.service', () => ({ apiService: { request: mocks.request } }));
vi.mock('@/services/salesReferenceService', () => ({
  salesReferenceService: {
    getActiveCurrencies: async () => [
      { code: 'GHS', name: 'Ghana Cedi', isBaseCurrency: true },
      { code: 'USD', name: 'US Dollar', isBaseCurrency: false },
    ],
  },
}));
vi.mock('@/components/estate/PropertyEnquiryDetails', () => ({ PropertyEnquiryDetails: () => null }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => false }) }));

describe('property enquiry search link', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    mocks.id = 'one';
    mocks.request.mockReset();
    mocks.request.mockImplementation(async (endpoint: string) => {
      if (endpoint.includes('?page=')) return { success: true, data: [], totalCount: 0 };
      if (endpoint.endsWith('/estate-handoff')) return { success: true, data: { canHandoff: false } };
      const id = endpoint.split('/').at(-1);
      return { success: true, data: { id, ticketNumber: `PE-${id}`, subject: `Enquiry ${id}`, status: 'New', messages: [] } };
    });
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('loads a new query-selected record even when already on the page and absent from its register page', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    const view = render(<QueryClientProvider client={client}><PropertyEnquiriesPage /></QueryClientProvider>);
    await waitFor(() => expect(mocks.request).toHaveBeenCalledWith('/ehc/internal/property-enquiries/one', { method: 'GET' }));
    await screen.findByRole('heading', { name: 'PE-one' });
    mocks.id = 'two';
    view.rerender(<QueryClientProvider client={client}><PropertyEnquiriesPage /></QueryClientProvider>);
    await waitFor(() => expect(mocks.request).toHaveBeenCalledWith('/ehc/internal/property-enquiries/two', { method: 'GET' }));
    await screen.findByRole('heading', { name: 'PE-two' });
    expect(screen.queryByRole('heading', { name: 'PE-one' })).not.toBeInTheDocument();
    expect(mocks.request.mock.calls.every(call => call[1]?.method === 'GET')).toBe(true);
    client.clear();
  });
});
