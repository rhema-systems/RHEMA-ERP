import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ProcurementBudgetDetailPage from './page';
import { toast } from 'sonner';

const routerMock = vi.hoisted(() => ({
  back: vi.fn(),
  push: vi.fn(),
}));

const budgetServiceMock = vi.hoisted(() => ({
  getBudgetById: vi.fn(),
  submitBudget: vi.fn(),
  approveBudget: vi.fn(),
  createRevision: vi.fn(),
  approveRevision: vi.fn(),
  rejectRevision: vi.fn(),
}));

const workflowMock = vi.hoisted(() => ({
  options: undefined as any,
}));

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'budget-1' }),
  useRouter: () => routerMock,
}));

vi.mock('@/services/procurementPlanningService', () => ({
  procurementBudgetService: budgetServiceMock,
}));

vi.mock('sonner', () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

vi.mock('@/components/workflow', () => ({
  useWorkflowRecord: (options: any) => {
    workflowMock.options = options;
    return {
      loading: false,
      refresh: vi.fn(),
      actionProps: {
        entityType: options.entityType,
        entityId: options.entityId,
        entityLabel: options.entityLabel,
        entityNumber: options.entityNumber,
        status: options.status,
        canSubmit: options.canSubmit,
        canApproveReject: options.canApproveReject,
        onSubmit: options.commands.submit,
        onApprove: async (comments: string) => options.commands.approve({ comments }),
        onReject: async (comments: string) => options.commands.reject({ comments }),
      },
    };
  },
  WorkflowApprovalActions: () => <div data-testid="workflow-actions" />,
  WorkflowTabTrigger: () => <button type="button">Workflow</button>,
  WorkflowTabContent: () => <div data-testid="workflow-history" />,
}));

const submittedBudget = {
  id: 'budget-1',
  budgetCode: 'PB-2026-0002',
  title: 'Approved works budget',
  departmentId: 'department-one',
  departmentName: 'Procurement',
  fiscalYear: 2026,
  allocatedAmount: 100_000,
  utilizedAmount: 0,
  committedAmount: 0,
  remainingAmount: 100_000,
  currency: 'GHS',
  status: 'Submitted',
  controlLevel: 'Strict',
  warningThresholdPercent: 80,
  utilizationPercent: 0,
  createdAt: '2026-08-23T10:00:00Z',
  effectiveDate: '2026-01-01T00:00:00Z',
  expiryDate: '2026-12-31T00:00:00Z',
  allocations: [],
  revisions: [],
};

describe('Procurement budget workflow details', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    workflowMock.options = undefined;
    budgetServiceMock.getBudgetById.mockResolvedValue(submittedBudget);
    budgetServiceMock.approveBudget.mockResolvedValue(submittedBudget);
  });

  it('keeps workflow actions and history visible after submission', async () => {
    render(<ProcurementBudgetDetailPage />);

    expect(await screen.findByText('PB-2026-0002')).toBeInTheDocument();
    expect(screen.getByTestId('workflow-actions')).toBeInTheDocument();
    expect(screen.getByText('Workflow')).toBeInTheDocument();
    expect(screen.getByTestId('workflow-history')).toBeInTheDocument();
    expect(workflowMock.options.entityType).toBe('ProcurementBudget');
    expect(workflowMock.options.canApproveReject).toBe(true);
  });

  it('connects approve and reject commands to the budget workflow endpoint', async () => {
    render(<ProcurementBudgetDetailPage />);
    await screen.findByText('PB-2026-0002');

    await act(async () => {
      await workflowMock.options.commands.approve({ comments: 'Approved independently' });
      await workflowMock.options.commands.reject({ comments: 'Return for correction' });
    });

    expect(budgetServiceMock.approveBudget).toHaveBeenNthCalledWith(1, 'budget-1', {
      isApproved: true,
      comments: 'Approved independently',
    });
    expect(budgetServiceMock.approveBudget).toHaveBeenNthCalledWith(2, 'budget-1', {
      isApproved: false,
      comments: 'Return for correction',
    });
  });

  it('creates a governed amount revision from an approved budget', async () => {
    budgetServiceMock.getBudgetById.mockResolvedValue({
      ...submittedBudget,
      status: 'Approved',
      committedAmount: 10_000,
      allocations: [{
        id: 'allocation-1',
        procurementBudgetId: 'budget-1',
        categoryName: 'Goods',
        allocatedAmount: 40_000,
        utilizedAmount: 0,
        remainingAmount: 40_000,
        utilizationPercent: 0,
      }],
    });
    budgetServiceMock.createRevision.mockResolvedValue({
      id: 'revision-1',
      procurementBudgetId: 'budget-1',
      revisionNumber: 1,
      revisionType: 'Increase',
      previousAmount: 100_000,
      newAmount: 125_000,
      changeAmount: 25_000,
      reason: 'Additional approved scope',
      status: 'Pending',
      createdAt: '2026-08-24T10:00:00Z',
    });

    render(<ProcurementBudgetDetailPage />);
    await screen.findByText('PB-2026-0002');

    fireEvent.click(screen.getByRole('button', { name: /revise budget/i }));
    fireEvent.change(screen.getByLabelText(/new approved amount/i), { target: { value: '125000' } });
    fireEvent.change(screen.getByLabelText(/^reason$/i), { target: { value: 'Additional approved scope' } });
    fireEvent.click(screen.getByRole('button', { name: /save revision/i }));

    await waitFor(() => {
      expect(budgetServiceMock.createRevision).toHaveBeenCalledWith('budget-1', {
        revisionType: 'Increase',
        newAmount: 125_000,
        reason: 'Additional approved scope',
      });
    });
    expect(toast.success).toHaveBeenCalledWith('Budget revision submitted for independent approval');
  });

  it('reports a directly applied revision without inventing an approval step', async () => {
    budgetServiceMock.getBudgetById.mockResolvedValue({ ...submittedBudget, status: 'Approved' });
    budgetServiceMock.createRevision.mockResolvedValue({ approvalRequired: false, status: 'Approved' });

    render(<ProcurementBudgetDetailPage />);
    await screen.findByText('PB-2026-0002');
    fireEvent.click(screen.getByRole('button', { name: /revise budget/i }));
    fireEvent.change(screen.getByLabelText(/new approved amount/i), { target: { value: '125000' } });
    fireEvent.change(screen.getByLabelText(/^reason$/i), { target: { value: 'Additional scope' } });
    fireEvent.click(screen.getByRole('button', { name: /save revision/i }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Budget revision applied'));
    expect(toast.success).not.toHaveBeenCalledWith('Budget revision submitted for independent approval');
  });

  it('keeps the revision input and current amount when its active workflow is invalid', async () => {
    budgetServiceMock.getBudgetById.mockResolvedValue({ ...submittedBudget, status: 'Approved' });
    const failure = 'The active workflow has no eligible approver. The budget has not changed.';
    budgetServiceMock.createRevision.mockRejectedValue(new Error(failure));

    render(<ProcurementBudgetDetailPage />);
    await screen.findByText('PB-2026-0002');
    fireEvent.click(screen.getByRole('button', { name: /revise budget/i }));
    fireEvent.change(screen.getByLabelText(/new approved amount/i), { target: { value: '125000' } });
    fireEvent.change(screen.getByLabelText(/^reason$/i), { target: { value: 'Additional approved scope' } });
    fireEvent.click(screen.getByRole('button', { name: /save revision/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(failure));
    expect(screen.getByRole('dialog', { name: /revise approved budget/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/new approved amount/i)).toHaveValue(125000);
    expect(screen.getByLabelText(/^reason$/i)).toHaveValue('Additional approved scope');
    expect(budgetServiceMock.getBudgetById).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });
});
