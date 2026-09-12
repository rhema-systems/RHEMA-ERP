import * as React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SalesOrderPage from './orders/[id]/page';
import SalesAgreementPage from './agreements/[id]/page';
import SalesAllocationPage from './allocations/[id]/page';
import { WorkflowStepType } from '@/types/workflow';
import { workflowApiService } from '@/services/workflow-api.service';
import { salesOrderService } from '@/services/salesOrderService';
import { salesAgreementService } from '@/services/salesAgreementService';
import { salesAllocationService } from '@/services/salesAllocationService';

vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'sales-record' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: { getWorkflowEntitySummary: vi.fn() } }));
vi.mock('@/services/salesOrderService', () => ({ salesOrderService: {
  getSalesOrderById: vi.fn(), submitForApproval: vi.fn(), processApproval: vi.fn(), confirmSalesOrder: vi.fn(),
} }));
vi.mock('@/services/salesAgreementService', () => ({ salesAgreementService: {
  getAgreementById: vi.fn(), submitForApproval: vi.fn(), processApproval: vi.fn(), activateAgreement: vi.fn(),
} }));
vi.mock('@/services/salesAllocationService', () => ({ salesAllocationService: {
  getAllocationById: vi.fn(), submitForApproval: vi.fn(), processApproval: vi.fn(), updateAllocationStatus: vi.fn(),
} }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: {} }));
vi.mock('@/components/workflow', async () => ({
  ...(await import('@/hooks/useWorkflowRecord')),
  ...(await import('@/components/workflow/WorkflowApprovalActions')),
  WorkflowApprovalHistoryPanel: () => <div data-testid="retained-workflow-history">Retained approval history</div>,
}));

const order = { id: 'sales-record', orderNumber: 'SO-OPTIONAL', status: 'Draft', customerName: 'Customer',
  currency: 'GHS', lines: [], statusHistory: [], totalAmount: 100, subtotalAmount: 100, taxAmount: 0,
  discountAmount: 0, deliveryProgress: 0 };
const agreement = { id: 'sales-record', documentNumber: 'SA-OPTIONAL', agreementStatus: 'Draft', agreementType: 'General', customerName: 'Customer',
  currency: 'GHS', lines: [], milestones: [], renewals: [], agreedValue: 100, utilizedValue: 0,
  remainingValue: 100, minimumCommitment: 0, maximumCommitment: 0, discountPercentage: 0 };
const allocation = { id: 'sales-record', sourceItemName: 'Saleable item', sourceCode: 'PROJECT', status: 'Reserved', history: [] };
const pages = [
  { type: 'SalesOrder', Page: SalesOrderPage, record: order, get: salesOrderService.getSalesOrderById,
    submit: salesOrderService.submitForApproval, status: 'status', completed: 'Confirmed' },
  { type: 'SalesAgreement', Page: SalesAgreementPage, record: agreement, get: salesAgreementService.getAgreementById,
    submit: salesAgreementService.submitForApproval, status: 'agreementStatus', completed: 'Active' },
  { type: 'SalesAllocation', Page: SalesAllocationPage, record: allocation, get: salesAllocationService.getAllocationById,
    submit: salesAllocationService.submitForApproval, status: 'status', completed: 'Allocated' },
];

describe.each(pages)('$type optional workflow detail page', ({ type, Page, record, get, submit, status, completed }) => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    vi.clearAllMocks();
    vi.mocked(get).mockResolvedValue(record as never);
    vi.mocked(submit).mockResolvedValue({ ...record, [status]: completed } as never);
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: type, entityId: 'sales-record', approvalRequired: false, hasActiveInstance: false,
      hasWorkflowHistory: false, canCurrentUserApprove: false, pendingApprovers: [],
    });
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('uses central submission for direct completion without empty approval cards or duplicate completion routes', async () => {
    render(<Page />);
    await screen.findByRole('button', { name: 'Finalize' });
    expect(screen.queryByRole('button', { name: /Submit for Approval|^Approve$|^Reject$|Mark Allocated|Confirm Order/ })).not.toBeInTheDocument();
    expect(screen.queryByTestId('retained-workflow-history')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Finalize' }));
    fireEvent.click(screen.getAllByRole('button', { name: 'Finalize' }).at(-1)!);
    await waitFor(() => expect(submit).toHaveBeenCalledExactlyOnceWith('sales-record'));
    expect(salesOrderService.confirmSalesOrder).not.toHaveBeenCalled();
    expect(salesAllocationService.updateAllocationStatus).not.toHaveBeenCalled();
  });

  it('retains history when an old workflow exists, without showing current approval actions', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: type, entityId: 'sales-record', approvalRequired: false, hasActiveInstance: false,
      hasWorkflowHistory: true, canCurrentUserApprove: false, pendingApprovers: [],
    });
    render(<Page />);
    await screen.findByRole('button', { name: 'Finalize' });
    expect(screen.getByTestId('retained-workflow-history')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Approve$|^Reject$/ })).not.toBeInTheDocument();
  });

  it('keeps independent approval controls for the existing instance even if configuration was disabled later', async () => {
    vi.mocked(get).mockResolvedValue({ ...record, [status]: 'PendingApproval' } as never);
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      entityType: type, entityId: 'sales-record', approvalRequired: false, hasActiveInstance: true,
      hasWorkflowHistory: true, canCurrentUserApprove: true, pendingApprovers: [], currentStepType: WorkflowStepType.Approval,
    });
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /Finalize|Mark Allocated|Confirm Order/ })).not.toBeInTheDocument();
    expect(screen.getByTestId('retained-workflow-history')).toBeInTheDocument();
  });

  it('fails closed when policy lookup fails, rather than treating it as no approval', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockRejectedValue(new Error('Access denied'));
    render(<Page />);
    await screen.findByTitle('Access denied');
    expect(screen.queryByRole('button', { name: /Finalize|Submit for Approval|Mark Allocated|Confirm Order/ })).not.toBeInTheDocument();
    expect(submit).not.toHaveBeenCalled();
  });
});
