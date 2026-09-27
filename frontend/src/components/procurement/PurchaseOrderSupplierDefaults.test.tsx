import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PurchaseOrderSupplierDefaults } from './PurchaseOrderSupplierDefaults';
import { businessPartnerService } from '@/services/businessPartnerService';
import { BankAccountType } from '@/types/cash-management';
import { TaxApplicability } from '@/types/tax';
import type { PurchaseOrderSupplierDefaultsDto } from '@/services/purchasingService';

Object.assign(globalThis, { React });
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPostingOptions: vi.fn() } }));
const snapshot: PurchaseOrderSupplierDefaultsDto = {
  businessPartnerId: 'supplier', paymentTerms: 'Net 30', tin: 'TIN-SAVED', creditLimit: 3000, currency: 'GHS',
  postingDefaults: { subjectToWithholdingDeduction: true, withholdingTaxRate: 7.5, cashAccountSource: 'Chequebook', defaultBankAccountId: 'bank', defaultTaxGroupId: 'tax' },
};
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({
    accounts: [],
    bankAccounts: [{ id: 'bank', accountNumber: 'GCB-01', accountName: 'GCB Cedi', bankName: 'GCB', currency: 'GHS', accountType: BankAccountType.Checking, currentBalance: 0, availableBalance: 0, isActive: true, createdAt: '2026-01-01' }],
    taxGroups: [{ id: 'tax', tenantId: 'tenant', code: 'STD', name: 'Standard Purchase Tax', applicability: TaxApplicability.Purchases, isDefault: false, isActive: true, components: [], createdAt: '2026-01-01' }],
  });
});

describe('saved purchase order supplier defaults', () => {
  it('displays stored TIN, WHT, credit limit and saved catalogue selections without recalculating PO totals', async () => {
    render(<PurchaseOrderSupplierDefaults defaults={snapshot} paymentTerms="Custom 45 days" />);
    expect(screen.getByText('TIN-SAVED')).toBeInTheDocument();
    expect(screen.getByText('7.5%')).toBeInTheDocument();
    expect(screen.getByText('Net 30')).toBeInTheDocument();
    expect(await screen.findByText('GCB-01 - GCB Cedi')).toBeInTheDocument();
    expect(screen.getByText('STD - Standard Purchase Tax')).toBeInTheDocument();
    expect(screen.getByText(/3,000.00/)).toHaveTextContent('GHS');
  });

  it('does not duplicate matching PO payment terms', async () => {
    render(<PurchaseOrderSupplierDefaults defaults={snapshot} paymentTerms="Net 30" />);
    await screen.findByText('GCB-01 - GCB Cedi');
    expect(screen.queryByText('Default Payment Terms')).not.toBeInTheDocument();
  });

  it('does not infer current master defaults for a historical PO with no snapshot', () => {
    const { container } = render(<PurchaseOrderSupplierDefaults defaults={null} />);
    expect(container).toBeEmptyDOMElement();
    expect(businessPartnerService.getPostingOptions).not.toHaveBeenCalled();
  });
});
