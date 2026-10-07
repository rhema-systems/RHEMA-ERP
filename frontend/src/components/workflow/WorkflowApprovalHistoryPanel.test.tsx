import * as React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkflowInstanceStatus, type WorkflowEntityAuditDto, type WorkflowEntitySummaryDto } from '@/types/workflow';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowApprovalHistoryPanel } from './WorkflowApprovalHistoryPanel';

vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: {
  getWorkflowEntityAudit: vi.fn(),
  getWorkflowEntitySummary: vi.fn(),
  downloadStepAttachment: vi.fn(),
} }));
vi.mock('@/components/procedures/ProcedurePdfViewer', () => ({ default: () => null }));

const audit: WorkflowEntityAuditDto = {
  entityType: 'SalesOrder',
  entityId: 'sales-order-9',
  workflowInstanceId: 'workflow-1',
  workflowName: 'Sales order approval',
  status: WorkflowInstanceStatus.Completed,
  startedDate: new Date('2026-10-07T11:40:00Z'),
  completedDate: new Date('2026-10-07T11:45:00Z'),
  steps: [],
};
const summary: WorkflowEntitySummaryDto = {
  entityType: 'SalesOrder',
  entityId: 'sales-order-9',
  hasActiveInstance: false,
  hasWorkflowHistory: true,
  approvalRequired: false,
  canCurrentUserApprove: false,
  pendingApprovers: [],
};

describe('WorkflowApprovalHistoryPanel summary ownership', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(workflowApiService.getWorkflowEntityAudit).mockResolvedValue(audit);
  });

  afterEach(cleanup);

  it('does not duplicate the page-owned entity-summary request while the shared summary is loading', async () => {
    const { rerender } = render(
      <WorkflowApprovalHistoryPanel
        entityType="SalesOrder"
        entityId="sales-order-9"
        workflowSummaryLoading
        loadWorkflowSummary={false}
        showActions={false}
      />
    );

    await waitFor(() => expect(workflowApiService.getWorkflowEntityAudit).toHaveBeenCalledOnce());
    expect(workflowApiService.getWorkflowEntitySummary).not.toHaveBeenCalled();

    rerender(
      <WorkflowApprovalHistoryPanel
        entityType="SalesOrder"
        entityId="sales-order-9"
        workflowSummary={summary}
        workflowSummaryLoading={false}
        loadWorkflowSummary={false}
        showActions={false}
      />
    );

    await screen.findByText(/Sales order approval/);
    expect(workflowApiService.getWorkflowEntityAudit).toHaveBeenCalledOnce();
    expect(workflowApiService.getWorkflowEntitySummary).not.toHaveBeenCalled();
  });

  it('still loads its own summary when no page-level owner is supplied', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue(summary);

    render(
      <WorkflowApprovalHistoryPanel
        entityType="SalesOrder"
        entityId="sales-order-9"
        showActions={false}
      />
    );

    await screen.findByText(/Sales order approval/);
    expect(workflowApiService.getWorkflowEntitySummary).toHaveBeenCalledExactlyOnceWith('SalesOrder', 'sales-order-9');
  });
});
