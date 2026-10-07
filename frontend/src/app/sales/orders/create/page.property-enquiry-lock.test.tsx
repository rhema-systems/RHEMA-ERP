import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import CreateSalesOrderPage from './page';

const services = vi.hoisted(() => ({
  getSaleableSources: vi.fn().mockResolvedValue([]),
  searchSaleableItems: vi.fn().mockResolvedValue([]),
  getPaymentTerms: vi.fn().mockResolvedValue([]),
  getTaxGroups: vi.fn().mockResolvedValue([]),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));
vi.mock('@/services/salesSetupService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/salesSetupService')>();
  return {
    ...actual,
    salesSetupService: {
      ...actual.salesSetupService,
      getSaleableSources: services.getSaleableSources,
      searchSaleableItems: services.searchSaleableItems,
    },
  };
});
vi.mock('@/services/financeCommonService', () => ({
  paymentTermService: { getByApplicableTo: services.getPaymentTerms },
}));
vi.mock('@/services/finance/tax-data.service', () => ({
  taxDataService: {
    getActiveTaxGroups: services.getTaxGroups,
    calculateTax: vi.fn(),
  },
}));
vi.mock('@/services/salesOrderService', () => ({
  salesOrderService: { createSalesOrder: vi.fn() },
}));
vi.mock('@/services/salesAllocationService', () => ({
  salesAllocationService: {
    hasActiveAllocation: vi.fn(),
    createAllocation: vi.fn(),
  },
}));
vi.mock('@/services/projectService', () => ({
  projectService: { linkSalesOrderToProjectUnit: vi.fn() },
}));
vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn() },
}));

describe('Sales Order property enquiry source lock', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    services.getSaleableSources.mockClear();
    services.searchSaleableItems.mockClear();
    services.getPaymentTerms.mockClear();
    services.getTaxGroups.mockClear();
  });

  afterEach(() => {
    cleanup();
    window.history.replaceState({}, '', '/');
    vi.unstubAllGlobals();
  });

  it('locks the exact enquiry property into the first order line', async () => {
    const params = new URLSearchParams({
      customerId: 'customer-1',
      customerName: 'Public Enquirer',
      opportunityId: 'opportunity-1',
      sourceId: 'property-source-1',
      sourceCode: 'PROPERTY_REGISTER',
      sourceDisplayName: 'Properties',
      sourceType: 'PropertyRegister',
      adapterKey: 'property-register',
      sourceItemId: 'property-1',
      itemCode: 'PROP-001',
      itemName: 'Published Property',
      itemType: 'Property',
      unitOfMeasure: 'Plot',
      propertyReference: 'PROP-001',
      estimatedValue: '250000',
      currency: 'GHS',
      shouldCreateSalesAllocation: 'true',
      propertyEnquiryId: 'enquiry-1',
      propertyEnquiryAssetType: 'Property',
      saleableSourceLocked: 'true',
    });
    window.history.replaceState({}, '', `/sales/orders/create?${params.toString()}`);

    render(<CreateSalesOrderPage />);

    expect(await screen.findByText(/exact Property listing is locked to this order/i)).toBeInTheDocument();
    const linesTab = screen.getByRole('tab', { name: 'Order Lines' });
    fireEvent.mouseDown(linesTab);
    fireEvent.click(linesTab);
    await waitFor(() => expect(linesTab).toHaveAttribute('aria-selected', 'true'));

    const itemName = await screen.findByDisplayValue('Published Property');
    const itemCode = screen.getByDisplayValue('PROP-001');
    const unitOfMeasure = screen.getByDisplayValue('Each');
    expect(itemName).toBeDisabled();
    expect(itemCode).toBeDisabled();
    expect(unitOfMeasure).toBeDisabled();
    expect(screen.queryByDisplayValue('Plot')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add Line' })).not.toBeInTheDocument();
    expect(services.searchSaleableItems).not.toHaveBeenCalled();
  });

  it('keeps direct Sales Order creation flexible', async () => {
    window.history.replaceState({}, '', '/sales/orders/create');

    render(<CreateSalesOrderPage />);
    const linesTab = screen.getByRole('tab', { name: 'Order Lines' });
    fireEvent.mouseDown(linesTab);
    fireEvent.click(linesTab);
    await waitFor(() => expect(linesTab).toHaveAttribute('aria-selected', 'true'));

    expect(await screen.findByRole('button', { name: 'Add Line' })).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Item name')).toBeEnabled();
    expect(screen.queryByText(/listing is locked to this order/i)).not.toBeInTheDocument();
  });
});
