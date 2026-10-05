import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const mocks = vi.hoisted(() => ({ get: vi.fn(), submit: vi.fn(), post: vi.fn(), summary: vi.fn(), permission: vi.fn(), toast: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'invoice-1' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: mocks.permission }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/contexts/TenantContext', () => ({ useTenant: () => ({ currentTenant: { name: 'Test', code: 'TEST' } }) }));
vi.mock('@/services/ar-service', () => ({ arService: { getInvoice: mocks.get, submitInvoiceForApproval: mocks.submit, postInvoice: mocks.post, deleteInvoice: vi.fn() } }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: mocks.summary } }));
vi.mock('@/components/finance/ar/ArInvoicePrintDocument', () => ({ ArInvoicePrintDocument: () => null, printArInvoiceDocument: vi.fn() }));
vi.mock('@/components/finance/ar/ArInvoicePrintDocument.module.css', () => ({ default: { screenRoot: 'screenRoot' } }));
vi.mock('@/components/finance/dimensions/source-document-dimension-panel', () => ({ SourceDocumentDimensionEvidence: () => null }));
import Page from './page';

const invoice = { id: 'invoice-1', invoiceNumber: 'AR-1', businessPartnerId: 'customer-1', customerName: 'Customer',
  invoiceDate: '2026-09-12', dueDate: null, lineItems: [], totalAmount: 100, paidAmount: 0, balanceAmount: 100,
  currencyCode: 'GHS', status: 'Draft', approvalRequired: true };
const direct = { entityType: 'Invoice', entityId: 'invoice-1', approvalRequired: false, hasActiveInstance: false, hasWorkflowHistory: false };
function show() { return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><Page /></QueryClientProvider>); }
beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.get.mockResolvedValue(invoice); mocks.summary.mockResolvedValue(direct); mocks.permission.mockReturnValue(true); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('AR invoice optional approval and explicit posting', () => {
  it('prepares a direct draft without silently posting it', async () => {
    mocks.submit.mockResolvedValue({ ...invoice, status: 'ReadyToPost', approvalRequired: false });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Prepare to post' }));
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Prepare to post' }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledWith('invoice-1'));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Ready to post' })));
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it('posts only a retained direct readiness state after confirmation', async () => {
    mocks.get.mockResolvedValue({ ...invoice, status: 'ReadyToPost', approvalRequired: false });
    mocks.post.mockResolvedValue({ ...invoice, status: 'Sent', journalEntryId: 'journal-1' });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Post' }));
    expect(mocks.post).not.toHaveBeenCalled();
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.post).toHaveBeenCalledWith('invoice-1'));
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('does not let Send permission substitute for posting permission', async () => {
    mocks.get.mockResolvedValue({ ...invoice, status: 'ReadyToPost', approvalRequired: false });
    mocks.permission.mockImplementation((permission: string) => permission !== 'Finance.AR.Invoices.ApprovePost');
    show();
    await screen.findByText('Invoice AR-1');
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });

  it('offers explicit Post when the active configured process already completed', async () => {
    mocks.get.mockResolvedValue({ ...invoice, status: 'Approved', approvalRequired: true, workflowInstanceId: 'workflow-1' });
    mocks.summary.mockResolvedValue({ ...direct, approvalRequired: true, hasWorkflowHistory: true });
    show();
    expect(await screen.findByRole('button', { name: 'Post' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Submit for Approval' })).not.toBeInTheDocument();
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it('retains the configured approval action', async () => {
    mocks.summary.mockResolvedValue({ ...direct, approvalRequired: true });
    show();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Submit for Approval' })).toBeEnabled());
    expect(screen.queryByRole('button', { name: 'Prepare to post' })).not.toBeInTheDocument();
  });

  it('fails closed and offers retry when workflow lookup fails', async () => {
    mocks.summary.mockRejectedValue(new Error('Workflow unavailable'));
    show();
    expect(await screen.findByRole('alert')).toHaveTextContent('Workflow unavailable');
    expect(screen.getByRole('button', { name: 'Submit for Approval' })).toBeDisabled();
    mocks.summary.mockResolvedValue(direct);
    fireEvent.click(screen.getByRole('button', { name: 'Retry workflow check' }));
    expect(await screen.findByRole('button', { name: 'Prepare to post' })).toBeEnabled();
  });

  it('retains the confirmation and server error on failed posting', async () => {
    mocks.get.mockResolvedValue({ ...invoice, status: 'ReadyToPost', approvalRequired: false });
    mocks.post.mockRejectedValue({ response: { detail: 'Accounting period is closed.', code: 'PERIOD_CLOSED' } });
    show();
    fireEvent.click(await screen.findByRole('button', { name: 'Post' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Post' }));
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ description: 'Accounting period is closed. (PERIOD_CLOSED)' })));
    expect(dialog).toBeVisible();
  });
});
