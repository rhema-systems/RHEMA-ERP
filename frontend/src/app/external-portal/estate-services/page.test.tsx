import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ExternalEstateServicesPage from './page';

const mocks = vi.hoisted(() => ({
  getRequestTypes: vi.fn(),
  getMyRequestsPage: vi.fn(),
  getMyProperties: vi.fn(),
  getCustomerProfiles: vi.fn(),
  createRequest: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock('@/services/external-estate-services.service', () => ({
  externalEstateServicesService: {
    getRequestTypes: mocks.getRequestTypes,
    getMyRequestsPage: mocks.getMyRequestsPage,
    getMyProperties: mocks.getMyProperties,
    createRequest: mocks.createRequest,
  },
}));
vi.mock('@/services/external-estate-listings.service', () => ({
  externalEstateListingsService: { getCustomerProfiles: mocks.getCustomerProfiles },
}));
vi.mock('sonner', () => ({ toast: { error: mocks.toastError, success: vi.fn() } }));
vi.mock('@/components/ui/select', () => ({
  Select: ({ value, onValueChange, disabled, children }: React.PropsWithChildren<{ value?: string; onValueChange: (value: string) => void; disabled?: boolean }>) => {
    const trigger = React.Children.toArray(children)[0] as React.ReactElement<{ id?: string }>;
    return <select id={trigger.props.id} value={value || ''} disabled={disabled} onChange={event => onValueChange(event.target.value)}>
      <option value="">Select</option>
      {children}
    </select>;
  },
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: React.PropsWithChildren) => <>{children}</>,
  SelectItem: ({ value, children }: React.PropsWithChildren<{ value: string }>) => <option value={value}>{children}</option>,
}));

beforeEach(() => {
  mocks.getRequestTypes.mockResolvedValue([{ code: 'maintenance', title: 'Maintenance', module: 'Facilities', entityType: 'EstateFacilityMaintenance', category: 'Facilities' }]);
  mocks.getMyRequestsPage.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 1, hasPreviousPage: false, hasNextPage: false });
  mocks.getMyProperties.mockResolvedValue({ properties: [
    { id: 'asset-1', assetCode: 'ASR-001', name: 'First Property', customerBusinessPartnerId: 'customer-1', location: 'Accra' },
    { id: 'asset-2', assetCode: 'ASR-002', name: 'Second Property', customerBusinessPartnerId: 'customer-1', location: 'Tema' },
  ], customers: [] });
  mocks.getCustomerProfiles.mockResolvedValue([{ id: 'customer-1', partnerName: 'Estate Customer', primaryEmail: 'customer@example.test' }]);
  mocks.createRequest.mockResolvedValue({ id: 'request-1' });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('Estate service property selection', () => {
  it('submits a customer-owned ASR selected from the portfolio', async () => {
    render(<ExternalEstateServicesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add New' }));
    const dialog = screen.getByRole('dialog');
    const property = within(dialog).getByRole('combobox', { name: 'Property / unit / plot' });
    expect(within(dialog).queryByRole('textbox', { name: 'Property / unit / plot' })).not.toBeInTheDocument();
    expect(within(property).getByRole('option', { name: 'ASR-001 · First Property' })).toBeInTheDocument();
    expect(within(property).getByRole('option', { name: 'ASR-002 · Second Property' })).toBeInTheDocument();

    fireEvent.change(property, { target: { value: 'ASR-002' } });
    expect(within(dialog).queryByText('Urgency')).not.toBeInTheDocument();
    fireEvent.change(within(dialog).getByRole('textbox', { name: 'Problem description' }), { target: { value: 'Please inspect the property.' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Submit request' }));
    await waitFor(() => expect(mocks.createRequest).toHaveBeenCalledWith(expect.objectContaining({ propertyReference: 'ASR-002', location: 'Tema', priority: '' })));
  });

  it('does not offer free-text property entry without linked properties', async () => {
    mocks.getMyProperties.mockResolvedValue({ properties: [], customers: [] });
    render(<ExternalEstateServicesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add New' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('combobox', { name: 'Property / unit / plot' })).toBeDisabled();
    expect(within(dialog).queryByRole('textbox', { name: 'Property / unit / plot' })).not.toBeInTheDocument();
  });
});
