import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { VendorInvoiceFormPage } from '@/components/finance/ap/VendorInvoiceFormPage';
import { accountsPayableService } from '@/services/accountsPayableService';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';

const { queryData, toast, push, dimensionPanel } = vi.hoisted(() => ({ queryData: {} as Record<string, unknown>, toast: vi.fn(), push: vi.fn(), dimensionPanel: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push, back: vi.fn() }), useSearchParams: () => new URLSearchParams() }));
vi.mock('@/contexts/TenantContext', () => ({ useTenant: () => ({ currentTenantCode: 'DEFAULT' }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast }) }));
vi.mock('@tanstack/react-query', () => ({ useQuery: ({ queryKey, enabled }: { queryKey: string[]; enabled?: boolean }) => ({ data: enabled === false ? undefined : queryData[queryKey[0]], isLoading: false, isFetching: false, error: null }) }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({ SourceDocumentDimensionPanel: (props: unknown) => { dimensionPanel(props); return <div />; } }));
vi.mock('@/services/financeCommonService', () => ({ paymentTermService: { getByApplicableTo: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: { createInvoice: vi.fn(), updateInvoice: vi.fn(), getInvoiceBudgetCells: vi.fn().mockResolvedValue([]) } }));
Object.assign(globalThis, { React, ResizeObserver: class { observe() {} unobserve() {} disconnect() {} } });
Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'hasPointerCapture', { configurable: true, value: () => false });
Object.defineProperty(Element.prototype, 'setPointerCapture', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'releasePointerCapture', { configurable: true, value: vi.fn() });

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(accountsPayableService.getInvoiceBudgetCells).mockResolvedValue([]);
  delete queryData['vendor-invoice'];
  const accounts = [
    { id: 'ap', accountCode: '2100', accountNumber: '2100', accountName: 'Trade Payables', accountType: 'Liability', isControlAccount: true, allowDirectPosting: false },
    { id: 'expense', accountCode: '6100', accountNumber: '6100', accountName: 'Freight Purchases', accountType: 'Expense', isControlAccount: false, allowDirectPosting: true },
  ];
  Object.assign(queryData, {
    'ap-invoice-entry-suppliers': { items: [{ id: 'supplier-role', businessPartnerId: 'supplier', businessPartnerRoleId: 'supplier-role', roleType: 'Supplier', name: 'Freight Vendor', code: 'SUP-001', currency: 'GHS', isTransactionReady: true }] },
    'gl-accounts-active': { items: accounts }, 'inventory-items-active': [], warehouses: [], 'ap-purchase-orders': { items: [] },
    'finance-settings': { baseCurrency: 'GHS' }, taxes: [{ id: 'wht', code: 'WHT7', category: 'Withholding', name: 'Supplier WHT', rate: 7.5, isActive: true, applicability: 'Purchases', taxPayableAccountId: 'wht-account' }],
    'tax-groups-active': [{ id: 'vat', name: 'VAT Five', code: 'VAT5', components: [{ taxId: 'tax5', taxCode: 'VAT5', taxName: 'VAT Five', taxRate: 5, taxCategory: 'VAT', calculationOrder: 1, compoundBasis: 'Base' }] }],
    'ap-invoice-supplier-defaults': { businessPartnerId: 'supplier', withholdingDefault: { required: true, taxId: 'wht', rate: 7.5, taxPayableAccountId: 'wht-account' }, postingDefaults: { subjectToWithholdingDeduction: true, withholdingTaxRate: 7.5, defaultWithholdingTaxId: 'wht', defaultApAccountId: 'ap', defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'vat', cashAccountSource: 'Chequebook' } },
  });
  vi.mocked(accountsPayableService.createInvoice).mockResolvedValue({} as never);
  vi.mocked(accountsPayableService.updateInvoice).mockResolvedValue({} as never);
  vi.mocked(paymentTermService.getByApplicableTo).mockResolvedValue([]);
});

async function prepareInvoice(choice: 'Yes' | 'No' | 'Dismiss' = 'No') {
  const result = render(<VendorInvoiceFormPage />);
  fireEvent.click(screen.getByRole('combobox', { name: 'Supplier' }));
  fireEvent.click(await screen.findByText(/Freight Vendor/));
  const confirmation = await screen.findByRole('dialog', { name: 'Classify this invoice for WHT at payment?' });
  fireEvent.click(within(confirmation).getByRole('button', { name: choice === 'Dismiss' ? 'Close' : choice }));
  if (choice === 'Yes') {
    fireEvent.change(await screen.findByLabelText('WHT Contract / Reference'), { target: { value: 'CONTRACT-001' } });
    const categoryArea = screen.getByText('WHT Supply Category').parentElement;
    if (!categoryArea) throw new Error('WHT Supply Category field was not rendered.');
    fireEvent.click(within(categoryArea).getByRole('combobox'));
    fireEvent.click(await screen.findByRole('option', { name: 'Services' }));
  }
  const checkbox = await screen.findByRole('checkbox', { name: 'Use approved supplier invoice defaults' });
  expect(checkbox).toBeChecked();
  fireEvent.change(screen.getByPlaceholderText('Notes'), { target: { value: 'Freight service' } });
  const priceInput = result.container.querySelector('input[name="lineItems.0.unitPrice"]');
  if (!priceInput) throw new Error('Invoice price input was not rendered.');
  fireEvent.change(priceInput, { target: { value: '100' } });
  return result;
}

describe('new AP invoice visible supplier defaults', () => {
  it('derives Migration Clearing and omits manual posting accounts for an opening balance', async () => {
    const lineId = '2d7b93c1-8f53-4d9c-b594-d76c43e2f0c8';
    queryData['vendor-invoice'] = {
      id: 'opening-invoice', invoiceNumber: 'LEG-AP-001', businessPartnerId: 'supplier',
      status: 'Draft', invoiceDate: '2026-10-01', dueDate: '2026-10-31', currencyCode: 'GHS', exchangeRate: 1,
      isOpeningBalance: true, paymentTermsDays: 30, applySupplierWithholdingDefaults: false,
      expenseAccountId: 'expense',
      lineItems: [{ id: lineId, lineItemType: 'Expense', glAccountId: 'expense', budgetEntryId: 'budget-cell', description: 'Legacy supplier balance', quantity: 1, unitPrice: 12000, unit: 'EA' }],
    };

    render(<VendorInvoiceFormPage editInvoiceId="opening-invoice" />);

    expect(await screen.findByText(/Migration Clearing is derived from Finance Settings/i)).toBeInTheDocument();
    expect(screen.queryByText('GL Account')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(accountsPayableService.updateInvoice).toHaveBeenCalled());
    const request = vi.mocked(accountsPayableService.updateInvoice).mock.calls[0][1];
    expect(request.expenseAccountId).toBeUndefined();
    expect(request.lineItems[0].glAccountId).toBeUndefined();
    expect(request.lineItems[0].budgetEntryId).toBeUndefined();
    expect(request.financeDimensions?.lines).toEqual([]);
  });

  it('preserves a QS service certificate source and line identity while exposing expense and budget coding', async () => {
    const lineId = '2d7b93c1-8f53-4d9c-b594-d76c43e2f0c8';
    queryData['vendor-invoice'] = {
      id: 'invoice', invoiceNumber: 'VI-QS-001', businessPartnerId: 'supplier',
      status: 'Draft', invoiceDate: '2026-09-20', dueDate: '2026-10-20', currencyCode: 'GHS', exchangeRate: 1,
      isOpeningBalance: false, paymentTermsDays: 30, applySupplierWithholdingDefaults: false,
      acceptedSupplyKind: 'WorksPaymentCertificate', acceptedSupplySourceId: 'certificate',
      lineItems: [{ id: lineId, lineItemType: 'Service', glAccountId: 'expense', budgetEntryId: 'budget-cell', description: 'Approved QS certificate', quantity: 1, unitPrice: 100, unit: 'Certificate' }],
    };
    vi.mocked(accountsPayableService.getInvoiceBudgetCells).mockResolvedValue([{ budgetEntryId: 'budget-cell', dimensionAssignments: [], fiscalPeriodCode: '2026', functionalCurrencyCode: 'GHS', approvedAmount: 1000, postedActualAmount: 0, reservedAmount: 0, availableAmount: 1000 }] as never);
    render(<VendorInvoiceFormPage editInvoiceId="invoice" />);
    expect((await screen.findAllByText('Service / Works certificate')).length).toBeGreaterThan(0);
    await waitFor(() => expect(accountsPayableService.getInvoiceBudgetCells).toHaveBeenCalled());
    const glAccountField = screen.getByText('GL Account').parentElement;
    if (!glAccountField) throw new Error('GL Account field was not rendered.');
    await waitFor(() => expect(within(glAccountField).getAllByRole('combobox')).toHaveLength(1));
    expect(within(glAccountField).getByRole('combobox')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(accountsPayableService.updateInvoice).toHaveBeenCalled());
    expect(vi.mocked(accountsPayableService.updateInvoice).mock.calls[0][1]).toMatchObject({
      acceptedSupplyKind: 'WorksPaymentCertificate', acceptedSupplySourceId: 'certificate',
      lineItems: [expect.objectContaining({ id: lineId, lineItemType: 'Service', glAccountId: 'expense', budgetEntryId: 'budget-cell' })],
      financeDimensions: { lines: [expect.objectContaining({ sourceLineId: lineId, accountId: 'expense' })] },
    });
    expect(accountsPayableService.createInvoice).not.toHaveBeenCalled();
  });
  it('edits and submits server-resolved PO line dimensions with the same line identity', async () => {
    const lineId = '2d7b93c1-8f53-4d9c-b594-d76c43e2f0c8';
    queryData['vendor-invoice'] = {
      id: 'invoice', invoiceNumber: 'AP-001', businessPartnerId: 'supplier', purchaseOrderId: 'po',
      status: 'Draft', invoiceDate: '2026-09-01', dueDate: '2026-10-01', currencyCode: 'GHS', exchangeRate: 1,
      isOpeningBalance: false, paymentTermsDays: 30, matchingType: 'ThreeWay',
      applySupplierWithholdingDefaults: false,
      lineItems: [{ id: lineId, lineItemType: 'Expense', description: 'Received service', quantity: 1, unitPrice: 100, unit: 'EA' }],
      financeDimensions: {
        routeId: 'FinanceApVendorInvoice', certificationState: 'Enforced', sourceDocumentId: 'invoice', defaultValues: [], readinessWarnings: [], budgetEvidenceStatus: 'NotApplicable',
        lines: [{ sourceLineId: lineId, accountId: 'accrued', additionalAccountIds: ['variance'], requiredDimensionCodes: ['DEPT'], isFrozen: false, readinessWarnings: [], values: [{ dimensionCode: 'DEPT', dimensionName: 'Department', valueCode: 'OPS', valueName: 'Operations', isReadOnly: false }] }],
      },
    };
    render(<VendorInvoiceFormPage editInvoiceId="invoice" />);
    const save = await screen.findByRole('button', { name: 'Save Changes' });
    expect(dimensionPanel).toHaveBeenLastCalledWith(expect.objectContaining({
      certificationState: 'Enforced',
      lines: [expect.objectContaining({ id: lineId, accountId: 'accrued', additionalAccountIds: ['variance'], requiredDimensionCodes: ['DEPT'] })],
    }));
    fireEvent.click(save);
    await waitFor(() => expect(accountsPayableService.updateInvoice).toHaveBeenCalled());
    const request = vi.mocked(accountsPayableService.updateInvoice).mock.calls[0][1];
    expect(request.financeDimensions?.lines).toEqual([{ sourceLineId: lineId, accountId: 'accrued', dimensions: [{ dimensionCode: 'DEPT', valueCode: 'OPS' }] }]);
    expect(accountsPayableService.createInvoice).not.toHaveBeenCalled();
  });
  it('applies other supplier defaults but retains an explicit No withholding decision', async () => {
    await prepareInvoice();
    expect(screen.getByRole('switch', { name: 'Classify for WHT at payment' })).not.toBeChecked();
    expect(screen.getAllByText('VAT Five').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    await waitFor(() => expect(accountsPayableService.createInvoice).toHaveBeenCalled());
    const request = vi.mocked(accountsPayableService.createInvoice).mock.calls[0][0];
    expect(request.applyBusinessPartnerDefaults).toBe(true);
    expect(request.expenseAccountId).toBe('expense');
    expect(request.lineItems[0].taxGroupId).toBe('vat');
    expect(request.lineItems[0].taxRate).toBe(5);
    expect(request.withholdingTaxId).toBeNull();
    expect(request.withholdingTaxRate).toBe(0);
    expect(request.applySupplierWithholdingDefaults).toBe(false);
    expect(request.withholdingTaxRateOverride).toBeNull();
  });

  it('retains explicitly selected line No Tax and disables hidden server tax defaulting', async () => {
    await prepareInvoice();
    const lineTaxArea = screen.getByText('Tax Group').parentElement;
    if (!lineTaxArea) throw new Error('Tax Group field was not rendered.');
    const picker = within(lineTaxArea).getByRole('combobox');
    fireEvent.keyDown(picker, { key: 'Enter' });
    fireEvent.click(await screen.findByRole('option', { name: 'Zero-rated / Exempt' }));
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    await waitFor(() => expect(accountsPayableService.createInvoice).toHaveBeenCalled());
    const request = vi.mocked(accountsPayableService.createInvoice).mock.calls[0][0];
    expect(request.applyBusinessPartnerDefaults).toBe(false);
    expect(request.lineItems[0].taxGroupId).toBeNull();
    expect(request.lineItems[0].taxRate).toBe(0);
  });

  it('uses saved supplier/PO timing even when the selected term catalogue now has different days', async () => {
    const supplierData = queryData['ap-invoice-entry-suppliers'] as { items: Array<{ paymentTermId?: string }> };
    supplierData.items[0].paymentTermId = 'net';
    Object.assign(queryData['ap-invoice-supplier-defaults'] as object, { paymentTermId: 'net', paymentTermsDays: 60 });
    vi.mocked(paymentTermService.getByApplicableTo).mockResolvedValue([{ id: 'net', code: 'NET30', name: 'Net 30', dueDays: 30 }] as PaymentTermListDto[]);
    await prepareInvoice();
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    await waitFor(() => expect(accountsPayableService.createInvoice).toHaveBeenCalled());
    const request = vi.mocked(accountsPayableService.createInvoice).mock.calls[0][0];
    expect(request.paymentTermId).toBe('net');
    expect(request.paymentTermsDays).toBe(60);
    expect(new Date(request.dueDate || '').getTime() - new Date(request.invoiceDate).getTime()).toBe(60 * 24 * 60 * 60 * 1000);
  });

  it('applies supplier WHT only after Yes, shows the deduction once, and saves an editable rate override', async () => {
    await prepareInvoice('Yes');
    expect(screen.getByRole('switch', { name: 'Classify for WHT at payment' })).toBeChecked();
    expect(screen.getByLabelText('Expected WHT Rate (%)')).toHaveValue(7.5);
    expect(screen.getByText('Estimated WHT deducted at payment (7.5%)')).toBeInTheDocument();
    expect(screen.getByText(/-.*7\.50/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Expected WHT Rate (%)'), { target: { value: '5' } });
    expect(screen.getByText('Estimated WHT deducted at payment (5%)')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    await waitFor(() => expect(accountsPayableService.createInvoice).toHaveBeenCalled());
    expect(vi.mocked(accountsPayableService.createInvoice).mock.calls[0][0]).toMatchObject({ applySupplierWithholdingDefaults: true, withholdingTaxId: 'wht', withholdingTaxRate: 5, withholdingTaxRateOverride: 5, withholdingTaxAccountId: 'wht-account' });
  });

  it('retains an explicit zero rate and does not substitute the supplier rate', async () => {
    await prepareInvoice('Yes');
    fireEvent.change(screen.getByLabelText('Expected WHT Rate (%)'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    await waitFor(() => expect(accountsPayableService.createInvoice).toHaveBeenCalled());
    expect(vi.mocked(accountsPayableService.createInvoice).mock.calls[0][0]).toMatchObject({ applySupplierWithholdingDefaults: true, withholdingTaxRate: 0, withholdingTaxRateOverride: 0 });
  });

  it('does not record a decline or apply WHT when the prompt is dismissed', async () => {
    await prepareInvoice('Dismiss');
    expect(screen.getByRole('switch', { name: 'Classify for WHT at payment' })).not.toBeChecked();
    fireEvent.click(screen.getByRole('button', { name: 'Record Invoice' }));
    expect(await screen.findByRole('dialog', { name: 'Classify this invoice for WHT at payment?' })).toBeInTheDocument();
    expect(accountsPayableService.createInvoice).not.toHaveBeenCalled();
  });

  it('asks for a fresh decision after changing supplier', async () => {
    (queryData['ap-invoice-entry-suppliers'] as { items: unknown[] }).items.push({ id: 'supplier-two-role', businessPartnerId: 'supplier-two', businessPartnerRoleId: 'supplier-two-role', roleType: 'Supplier', name: 'Second Vendor', code: 'SUP-002', currency: 'GHS', isTransactionReady: true });
    await prepareInvoice('Yes');
    fireEvent.click(screen.getByRole('combobox', { name: 'Supplier' }));
    fireEvent.click(await screen.findByText(/Second Vendor/));
    const prompt = await screen.findByRole('dialog', { name: 'Classify this invoice for WHT at payment?' });
    fireEvent.click(within(prompt).getByRole('button', { name: 'No' }));
    expect(screen.getByRole('switch', { name: 'Classify for WHT at payment' })).not.toBeChecked();
    expect(screen.getByLabelText('Expected WHT Rate (%)')).toHaveValue(0);
  });

  it('preserves an existing draft choice, rate override and account despite catalogue changes', async () => {
    queryData['vendor-invoice'] = {
      id: 'invoice', invoiceNumber: 'AP-001', businessPartnerId: 'supplier', status: 'Draft', invoiceDate: '2026-09-01', dueDate: '2026-10-01', currencyCode: 'GHS', exchangeRate: 1,
      isOpeningBalance: false, paymentTermsDays: 30, matchingType: 'None', apAccountId: 'ap', expenseAccountId: 'expense',
      applySupplierWithholdingDefaults: true, withholdingTaxId: 'wht', withholdingTaxRate: 4.25, withholdingTaxRateOverride: 4.25, withholdingTaxAccountId: 'stored-wht-account',
      withholdingContractReference: 'CONTRACT-001', withholdingSupplyCategory: 'Services',
      lineItems: [{ id: '2d7b93c1-8f53-4d9c-b594-d76c43e2f0c8', lineItemType: 'Expense', glAccountId: 'expense', description: 'Saved service', quantity: 1, unitPrice: 100, unit: 'EA' }],
    };
    render(<VendorInvoiceFormPage editInvoiceId="invoice" />);
    await screen.findByRole('button', { name: 'Save Changes' });
    expect(screen.queryByRole('dialog', { name: 'Classify this invoice for WHT at payment?' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Expected WHT Rate (%)')).toHaveValue(4.25);
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(accountsPayableService.updateInvoice).toHaveBeenCalled());
    expect(vi.mocked(accountsPayableService.updateInvoice).mock.calls[0][1]).toMatchObject({ applySupplierWithholdingDefaults: true, withholdingTaxRate: 4.25, withholdingTaxRateOverride: 4.25, withholdingTaxAccountId: 'stored-wht-account' });
  });

  it('prompts when opening an unresolved auto-generated draft and saves No explicitly', async () => {
    queryData['vendor-invoice'] = {
      id: 'invoice', invoiceNumber: 'LC-DRAFT', businessPartnerId: 'supplier', status: 'Draft', invoiceDate: '2026-09-01', dueDate: '2026-10-01', currencyCode: 'GHS', exchangeRate: 1,
      isOpeningBalance: false, paymentTermsDays: 30, matchingType: 'None', apAccountId: 'ap', expenseAccountId: 'expense',
      applySupplierWithholdingDefaults: null, withholdingDecisionPending: true, withholdingTaxRate: 0,
      lineItems: [{ id: '2d7b93c1-8f53-4d9c-b594-d76c43e2f0c8', lineItemType: 'Expense', glAccountId: 'expense', description: 'Landed cost', quantity: 1, unitPrice: 100, unit: 'EA' }],
    };
    render(<VendorInvoiceFormPage editInvoiceId="invoice" />);
    const prompt = await screen.findByRole('dialog', { name: 'Classify this invoice for WHT at payment?' });
    fireEvent.click(within(prompt).getByRole('button', { name: 'No' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(accountsPayableService.updateInvoice).toHaveBeenCalled());
    expect(vi.mocked(accountsPayableService.updateInvoice).mock.calls[0][1]).toMatchObject({ applySupplierWithholdingDefaults: false, withholdingTaxId: null, withholdingTaxRate: 0 });
  });
});
