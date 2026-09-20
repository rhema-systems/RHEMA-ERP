import * as React from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PurchaseOrderPage from './purchase-orders/[id]/page';
import ReceiptPage from './receipts/[id]/page';
import { financePurchaseOrderService } from '@/services/financePurchaseOrderService';
import { workflowApiService } from '@/services/workflow-api.service';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasAnyRole: () => true }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: vi.fn() } }));
vi.mock('@/services/financePurchaseOrderService', () => ({ financePurchaseOrderService: {
  getPurchaseOrderById: vi.fn(), getReceiptById: vi.fn(), submitPurchaseOrderForApproval: vi.fn(),
  submitReceiptForApproval: vi.fn(), approvePurchaseOrder: vi.fn(), rejectPurchaseOrder: vi.fn(), convertToVendorInvoice: vi.fn(),
} }));

const order = { id: 'record', orderNumber: 'FPO-TEST', vendorId: 'supplier', vendorName: 'Supplier',
  orderDate: '2026-09-12', status: 1, approvalRequired: true, currencyCode: 'GHS', exchangeRate: 1,
  totalAmount: 100, items: [] };
const receipt = { id: 'record', financePurchaseOrderId: 'po', receiptNumber: 'FGRV-TEST',
  receiptDate: '2026-09-12', status: 1, statusName: 'Draft', approvalRequired: true, items: [] };
const pages = [
  { type: 'FinancePurchaseOrder', Page: PurchaseOrderPage, label: 'Finalize', submit: financePurchaseOrderService.submitPurchaseOrderForApproval,
    get: financePurchaseOrderService.getPurchaseOrderById, record: order, completed: 2 },
  { type: 'FinancePurchaseOrderReceipt', Page: ReceiptPage, label: 'Complete', submit: financePurchaseOrderService.submitReceiptForApproval,
    get: financePurchaseOrderService.getReceiptById, record: receipt, completed: 3 },
];
describe.each(pages)('$type optional approval actions', ({ type, Page, label, submit, get, record, completed }) => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    vi.clearAllMocks();
    vi.mocked(financePurchaseOrderService.getPurchaseOrderById).mockResolvedValue(order);
    vi.mocked(financePurchaseOrderService.getReceiptById).mockResolvedValue(receipt);
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: type, entityId: 'record', approvalRequired: false, hasActiveInstance: false,
      canCurrentUserApprove: false, pendingApprovers: [],
    });
    vi.mocked(submit).mockResolvedValue({ ...record, status: completed, approvalRequired: false } as never);
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
  const open = async () => {
    await act(async () => { render(<React.Suspense fallback="Loading"><Page params={Promise.resolve({ id: 'record' })} /></React.Suspense>); });
  };

  it('offers normal completion without approval actions when the server confirms no active process', async () => {
    await open();
    const action = await screen.findByRole('button', { name: label });
    expect(action).toBeEnabled();
    expect(screen.queryByRole('button', { name: /Submit For Approval|Approve PO|^Reject$/i })).not.toBeInTheDocument();
    fireEvent.click(action);
    await waitFor(() => expect(submit).toHaveBeenCalledExactlyOnceWith('record'));
  });
  it('keeps the approval route when an active definition is configured', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: type, entityId: 'record', approvalRequired: true, hasActiveInstance: false,
      canCurrentUserApprove: false, pendingApprovers: [],
    });
    await open();
    expect(await screen.findByRole('button', { name: /submit for approval/i })).toBeEnabled();
    expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
  });
  it('does not bypass a failed status lookup', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockRejectedValue(new Error('Approval status unavailable'));
    await open();
    expect(await screen.findByRole('alert')).toHaveTextContent('Approval status unavailable');
    expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Checking approval status/ })).toBeDisabled();
    expect(submit).not.toHaveBeenCalled();
  });
  it('labels a saved direct completion without claiming that a person approved it', async () => {
    vi.mocked(get).mockResolvedValue({ ...record, status: completed, statusName: 'Approved', approvalRequired: false } as never);
    await open();
    expect((await screen.findAllByText(type === 'FinancePurchaseOrder' ? 'Ready for receiving' : 'Ready for invoice')).length).toBeGreaterThan(0);
    expect(screen.queryByText('Approved By')).not.toBeInTheDocument();
  });
});
