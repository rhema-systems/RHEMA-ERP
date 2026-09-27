import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SalesOrderInvoicePanel } from './SalesOrderInvoicePanel';
import type { SalesOrderDetailDto } from '@/services/salesOrderService';
import { salesOrderInvoiceService as service } from '@/services/salesOrderInvoiceService';
import type { SourceDocumentDimensionPanelProps } from '@/components/finance/dimensions/source-document-dimension-panel';

vi.mock('@/services/salesOrderInvoiceService', () => ({ salesOrderInvoiceService: { get: vi.fn(), generate: vi.fn(), submit: vi.fn(), post: vi.fn(), distribution: vi.fn() } }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: { getAccounts: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/finance/tax-data.service', () => ({ taxDataService: { getTaxGroups: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/finance.service', () => ({ financeService: { getSettings: vi.fn().mockResolvedValue({ baseCurrency: 'GHS' }), getCurrentExchangeRate: vi.fn() } }));
vi.mock('@/lib/finance/invoice-exchange-rate', () => ({ loadApprovedInvoiceRate: vi.fn().mockResolvedValue({ rate: 1 }) }));
vi.mock('@/components/finance/PostingAccountPicker', () => ({ PostingAccountPicker: () => <span>Configured revenue account</span> }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({
  SourceDocumentDimensionPanel: (props: SourceDocumentDimensionPanelProps) => <div>
    <span>{props.context.sourceRoute}</span>
    <button onClick={() => props.onDefaultValuesChange({ DEPARTMENT: 'ESTATE' })}>Set dimension default</button>
    <button onClick={() => props.onLineValuesChange({ line: { DEPARTMENT: 'SALES' } })}>Set line dimensions</button>
    <button onClick={() => props.onLineValuesChange({ line: {} })}>Clear line dimensions</button>
  </div>,
}));
const order = { id: 'order', rowVersion: 'AQID', orderNumber: 'SO-1', orderType: 'Standard', status: 'Confirmed', currency: 'GHS',
  lines: [{ id: 'line', itemName: 'Stock item', quantity: 1, unitPrice: 100 }], totalAmount: 100 } as SalesOrderDetailDto;
const detail = { salesOrderId: 'order', salesOrderNumber: 'SO-1', canSubmit: false, canPost: true,
  invoice: { id: 'invoice', invoiceNumber: 'SI-1', status: 'ReadyToPost', currencyCode: 'GHS', totalAmount: 100, invoiceDate: '2026-09-27', lineItems: [] } };
describe('Sales order customer invoice', () => {
  beforeEach(() => vi.clearAllMocks());
  it('requires explicit applicable tax treatment before draft generation', async () => {
    render(<SalesOrderInvoicePanel order={order} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByText('Generate invoice'));
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    fireEvent.click(screen.getByText('Create draft invoice'));
    expect(screen.getAllByRole('alert')[0]).toHaveTextContent('applicable tax treatment');
    expect(service.generate).not.toHaveBeenCalled();
  });
  it('retains the generation key and selections after an unchanged failed request', async () => {
    vi.mocked(service.generate).mockRejectedValue({ detail: 'Source tax total does not match.' });
    render(<SalesOrderInvoicePanel order={order} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByText('Generate invoice'));
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    fireEvent.change(screen.getByLabelText('Tax treatment 1'), { target: { value: 'Exempt' } });
    fireEvent.click(screen.getByText('Create draft invoice'));
    await waitFor(() => expect(service.generate).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    fireEvent.click(screen.getByText('Create draft invoice'));
    await waitFor(() => expect(service.generate).toHaveBeenCalledTimes(2));
    expect(vi.mocked(service.generate).mock.calls[0][1]).toEqual(vi.mocked(service.generate).mock.calls[1][1]);
    expect(vi.mocked(service.generate).mock.calls[0][1]).toMatchObject({ rowVersion: 'AQID', lines: [{ salesOrderLineId: 'line', taxTreatment: 'Exempt' }] });
  });
  it('loads a balanced preview without posting and requires the explicit post action', async () => {
    vi.mocked(service.get).mockResolvedValue(detail);
    vi.mocked(service.distribution).mockResolvedValue({ invoiceId: 'invoice', currencyCode: 'GHS', isEstimated: true,
      totalDebit: 100, totalCredit: 100, isBalanced: true, lines: [{ accountId: 'ar', accountCode: '1200', accountName: 'Receivable', description: 'Invoice', debit: 100, credit: 0 }] });
    vi.mocked(service.post).mockResolvedValue({ ...detail, canPost: false });
    render(<SalesOrderInvoicePanel order={{ ...order, invoiceId: 'invoice' }} onChanged={vi.fn()} />);
    fireEvent.click(await screen.findByText('Review and post'));
    expect(await screen.findByText('Balanced')).toBeInTheDocument();
    expect(service.post).not.toHaveBeenCalled();
    fireEvent.click(screen.getByText('Post invoice'));
    await waitFor(() => expect(service.post).toHaveBeenCalledWith('order'));
  });
  it('does not expose financial actions on an unapproved order', () => {
    render(<SalesOrderInvoicePanel order={{ ...order, status: 'Draft' }} onChanged={vi.fn()} />);
    expect(screen.queryByText('Generate invoice')).not.toBeInTheDocument();
  });
  it('captures Finance defaults and exact source-line overrides, preserving explicit clears on retry', async () => {
    vi.mocked(service.generate).mockRejectedValue({ detail: 'Correct the required dimensions.' });
    render(<SalesOrderInvoicePanel order={{ ...order, lines: [{ ...order.lines[0], glAccountId: 'revenue' }] }} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByText('Generate invoice'));
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    expect(screen.getByText('sales.orders.customer-invoices')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Tax treatment 1'), { target: { value: 'Exempt' } });
    fireEvent.click(screen.getByText('Set dimension default'));
    fireEvent.click(screen.getByText('Set line dimensions'));
    fireEvent.click(screen.getByText('Create draft invoice'));
    await waitFor(() => expect(service.generate).toHaveBeenCalledTimes(1));
    expect(vi.mocked(service.generate).mock.calls[0][1].financeDimensions).toEqual({
      defaultDimensions: [{ dimensionCode: 'DEPARTMENT', valueCode: 'ESTATE' }],
      lines: [{ sourceLineId: 'line', accountId: 'revenue', dimensions: [{ dimensionCode: 'DEPARTMENT', valueCode: 'SALES' }] }],
      applyDefaultToEligibleLines: true,
    });
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    fireEvent.click(screen.getByText('Clear line dimensions'));
    fireEvent.click(screen.getByText('Create draft invoice'));
    await waitFor(() => expect(service.generate).toHaveBeenCalledTimes(2));
    expect(vi.mocked(service.generate).mock.calls[1][1].financeDimensions?.lines).toEqual([{ sourceLineId: 'line', accountId: 'revenue', dimensions: [] }]);
    expect(vi.mocked(service.generate).mock.calls[1][1].idempotencyKey).not.toEqual(vi.mocked(service.generate).mock.calls[0][1].idempotencyKey);
  });
  it.each([
    "Sales stock quantities must use the item's base unit of measure. Convert and reapprove the source quantity before invoicing.",
    'Your current warehouse scope does not allow inventory issue from this bin.',
  ])('keeps source setup failures visible and retains the draft selections: %s', async detail => {
    vi.mocked(service.generate).mockRejectedValue({ response: { data: { detail } } });
    render(<SalesOrderInvoicePanel order={order} onChanged={vi.fn()} />);
    fireEvent.click(screen.getByText('Generate invoice'));
    await waitFor(() => expect(screen.getByText('Create draft invoice')).not.toBeDisabled());
    fireEvent.change(screen.getByLabelText('Tax treatment 1'), { target: { value: 'Exempt' } });
    fireEvent.click(screen.getByText('Create draft invoice'));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(detail));
    expect(screen.getByLabelText('Tax treatment 1')).toHaveValue('Exempt');
    expect(screen.getByText('Create draft invoice')).not.toBeDisabled();
  });
});
