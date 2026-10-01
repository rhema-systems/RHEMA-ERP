import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { SaleableSourceQuickStart } from './SaleableSourceQuickStart';
import type { SalesSaleableItemDto, SalesSaleableSourceDto } from '@/services/salesSetupService';

const service = vi.hoisted(() => ({
  getSaleableSources: vi.fn(),
  searchSaleableItems: vi.fn(),
}));
const hooks = vi.hoisted(() => ({ toast: vi.fn() }));

vi.mock('@/services/salesSetupService', () => ({ salesSetupService: service }));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => hooks }));

const source = (id: string, displayName: string): SalesSaleableSourceDto => ({
  id,
  tenantId: 'tenant-1',
  code: id.toUpperCase(),
  displayName,
  sourceType: `${displayName}Register`,
  adapterKey: 'property-register',
  isActive: true,
  icon: 'Building2',
  colorCode: '#000000',
  sortOrder: id === 'property' ? 1 : 2,
  supportedTransactionTypes: 'SalesOrder',
  defaultCurrency: 'GHS',
  allowSalesOrders: true,
  allowSalesAgreements: true,
  allowReservations: true,
  requiresExternalModule: false,
  isSystemSource: true,
  createdAt: '2026-10-01T00:00:00Z',
});

const item = (overrides: Partial<SalesSaleableItemDto> = {}): SalesSaleableItemDto => ({
  sourceId: 'facility',
  sourceCode: 'FACILITY',
  sourceType: 'FacilityRegister',
  adapterKey: 'property-register',
  sourceItemId: 'facility-1',
  itemName: 'Conference Facility',
  canCreateSalesOrder: true,
  canCreateSalesAgreement: true,
  canCreateLeaseAgreement: true,
  shouldCreateSalesAllocation: true,
  hasActiveAllocation: false,
  ...overrides,
});

describe('SaleableSourceQuickStart', () => {
  beforeEach(() => {
    Object.defineProperty(Element.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn(),
    });
    service.getSaleableSources.mockReset();
    service.searchSaleableItems.mockReset();
    service.getSaleableSources.mockResolvedValue([
      source('property', 'Property'),
      source('facility', 'Facility'),
    ]);
  });

  afterEach(cleanup);

  it('clears a stale search and loads records when the source changes', async () => {
    service.searchSaleableItems.mockResolvedValue([item()]);
    render(<SaleableSourceQuickStart mode="order" linkedContext={null} />);

    const search = await screen.findByPlaceholderText('Search saleable items');
    fireEvent.change(search, { target: { value: 'old property search' } });
    const sourcePicker = screen.getByRole('combobox', { name: 'Saleable source' });
    await waitFor(() => expect(sourcePicker).toBeEnabled());
    fireEvent.click(sourcePicker);
    fireEvent.click(await screen.findByRole('option', { name: 'Facility' }));

    await waitFor(() =>
      expect(service.searchSaleableItems).toHaveBeenCalledWith('facility', undefined, 50),
    );
    expect(search).toHaveValue('');
    expect((await screen.findAllByText('Conference Facility')).length).toBeGreaterThan(0);
  });

  it('shows the API reason when a published item is not sale eligible', async () => {
    service.searchSaleableItems.mockResolvedValue([
      item({
        sourceId: 'property',
        sourceItemId: 'property-1',
        itemName: 'Published Property',
        canCreateSalesOrder: false,
        salesOrderIneligibilityReason: 'Sales orders are disabled for this saleable source.',
      }),
    ]);
    render(<SaleableSourceQuickStart mode="order" linkedContext={null} />);

    await screen.findByPlaceholderText('Search saleable items');
    fireEvent.click(screen.getByRole('button', { name: 'Search saleable items' }));

    expect(await screen.findByText(
      'Sales orders are disabled for this saleable source.',
    )).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Use For Sales Order' })).toBeDisabled();
  });
});
