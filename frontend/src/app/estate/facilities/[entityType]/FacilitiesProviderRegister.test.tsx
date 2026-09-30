import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';

import { estateFacilitiesService, type FacilitiesProviderOption } from '@/services/estate-facilities.service';
import { FacilitiesProviderRegister, expiringProviderContracts } from './FacilitiesProviderRegister';

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyPermission: () => true, hasAnyRole: () => true }) }));
vi.mock('@/services/estate-facilities.service', () => ({
  estateFacilitiesService: {
    getApprovedProviders: vi.fn(), getProviderInvoices: vi.fn(), getProviderRates: vi.fn(),
    createProviderRate: vi.fn(), updateProviderRate: vi.fn(),
  },
}));

const provider = (id: string, partnerType: string, partnerName: string): FacilitiesProviderOption => ({
  id,
  partnerCode: `BP-${id}`,
  partnerType,
  partnerName,
  categories: ['Cleaning'],
  contracts: [],
});

function openActions(partnerName: string) {
  fireEvent.keyDown(screen.getByRole('button', { name: `Actions for ${partnerName}` }), { key: 'Enter', code: 'Enter' });
}

it('flags only active contracts ending within 30 days', () => {
  const item = provider('1', 'Supplier', 'Clean Co');
  item.contracts = [
    { id: 'soon', businessPartnerId: '1', contractNumber: 'A', contractTitle: 'A', contractValue: 100, currency: 'GHS', endDate: '2026-10-15' },
    { id: 'later', businessPartnerId: '1', contractNumber: 'B', contractTitle: 'B', contractValue: 200, currency: 'GHS', endDate: '2026-12-01' },
  ];
  expect(expiringProviderContracts(item, new Date('2026-09-25T00:00:00Z')).map((contract) => contract.id)).toEqual(['soon']);
});

it('shows approved supplier types and filters by provider or category', async () => {
  vi.mocked(estateFacilitiesService.getApprovedProviders).mockResolvedValue([
    provider('1', 'Supplier', 'Clean Co'),
    { ...provider('2', 'Contractor', 'Repair Co'), contracts: [{
      id: 'contract-2', businessPartnerId: '2', contractNumber: 'CT-002',
      contractTitle: 'Repairs', contractValue: 500, currency: 'GHS',
    }] },
  ]);

  render(<FacilitiesProviderRegister />);
  await waitFor(() => expect(screen.getByText('Clean Co')).toBeInTheDocument());
  expect(screen.getByText('Repair Co')).toBeInTheDocument();
  expect(screen.getByText('CT-002')).toBeInTheDocument();

  fireEvent.change(screen.getByRole('textbox', { name: 'Search providers' }), {
    target: { value: 'Repair' },
  });
  expect(screen.queryByText('Clean Co')).not.toBeInTheDocument();
  openActions('Repair Co');
  expect(await screen.findByRole('menuitem', { name: 'Details' })).toHaveAttribute(
    'href', '/procurement/business-partners/2'
  );
});

it('shows direct supplier invoices in the provider history dialog', async () => {
  vi.mocked(estateFacilitiesService.getApprovedProviders).mockResolvedValue([
    provider('1', 'Supplier', 'Clean Co'),
  ]);
  vi.mocked(estateFacilitiesService.getProviderInvoices).mockResolvedValue([{
    id: 'invoice-1', invoiceNumber: 'AP-001', invoiceDate: '2026-09-25T00:00:00Z',
    purchaseOrderId: null, totalAmount: 150, paidAmount: 50, currencyCode: 'GHS', status: 4,
  }]);

  render(<FacilitiesProviderRegister />);
  await screen.findByRole('button', { name: 'Actions for Clean Co' });
  openActions('Clean Co');
  fireEvent.click(await screen.findByRole('menuitem', { name: 'View supplier invoices' }));
  expect(await screen.findByText('AP-001')).toBeInTheDocument();
  expect(screen.getByText('Direct invoice')).toBeInTheDocument();
  expect(screen.getByText('GHS 100.00')).toBeInTheDocument();
});

it('shows service rate history and saves a new rate for an approved provider', async () => {
  vi.mocked(estateFacilitiesService.getApprovedProviders).mockResolvedValue([provider('1', 'Supplier', 'Clean Co')]);
  vi.mocked(estateFacilitiesService.getProviderRates)
    .mockResolvedValueOnce([])
    .mockResolvedValueOnce([{
      id: 'rate-1', businessPartnerId: '1', serviceName: 'Cleaning', unitOfMeasure: 'visit',
      rate: 125, currency: 'GHS', effectiveFrom: '2026-10-01', isActive: true,
    }]);
  vi.mocked(estateFacilitiesService.createProviderRate).mockResolvedValue({
    id: 'rate-1', businessPartnerId: '1', serviceName: 'Cleaning', unitOfMeasure: 'visit',
    rate: 125, currency: 'GHS', effectiveFrom: '2026-10-01', isActive: true,
  });

  render(<FacilitiesProviderRegister />);
  await screen.findByRole('button', { name: 'Actions for Clean Co' });
  openActions('Clean Co');
  fireEvent.click(await screen.findByRole('menuitem', { name: 'Rates' }));
  expect(await screen.findByText('No service rates recorded.')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Add rate' }));
  fireEvent.change(screen.getByRole('textbox', { name: 'Service' }), { target: { value: 'Cleaning' } });
  fireEvent.change(screen.getByRole('textbox', { name: 'Unit' }), { target: { value: 'visit' } });
  fireEvent.change(screen.getByRole('spinbutton', { name: 'Rate' }), { target: { value: '125' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save rate' }));

  await waitFor(() => expect(estateFacilitiesService.createProviderRate).toHaveBeenCalledWith('1', expect.objectContaining({
    serviceName: 'Cleaning', unitOfMeasure: 'visit', rate: 125,
  })));
  expect(await screen.findByText('GHS 125.00')).toBeInTheDocument();
});
