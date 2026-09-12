import * as React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Tabs, TabsList } from '@/components/ui/tabs';
import { WorkflowStepType, type WorkflowEntitySummaryDto } from '@/types/workflow';
import { WorkflowApprovalActions } from './WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from './WorkflowRecordTab';
import { workflowApiService } from '@/services/workflow-api.service';

vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: {
  getWorkflowEntitySummary: vi.fn(), uploadStepAttachment: vi.fn(),
} }));
vi.mock('./WorkflowApprovalHistoryPanel', () => ({ WorkflowApprovalHistoryPanel: () => <div>Retained audit entries</div> }));

const direct: WorkflowEntitySummaryDto = {
  entityType: 'PurchaseOrder', entityId: 'po-1', approvalRequired: false,
  hasActiveInstance: false, hasWorkflowHistory: false, canCurrentUserApprove: false, pendingApprovers: [],
};
const base = {
  entityType: 'PurchaseOrder', entityId: 'po-1', entityLabel: 'Purchase Order',
  status: 'Draft', canSubmit: true, onSubmit: vi.fn(async () => undefined),
  onApprove: vi.fn(async () => undefined), onReject: vi.fn(async () => undefined),
  loadWorkflowSummary: false,
};
const tabPolicy = { entityType: base.entityType, entityId: base.entityId, loadWorkflowSummary: false };

describe('optional approval shared UI', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue(direct);
  });
  afterEach(cleanup);

  it('retains direct completion without any approval action or misleading approval confirmation', async () => {
    render(<WorkflowApprovalActions {...base} workflowSummary={direct} canApproveReject submitCopyMode="approval" />);
    expect(screen.getByRole('button', { name: 'Finalize' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /approve|reject|submit for approval/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Finalize' }));
    expect(screen.getByText('Finalize Purchase Order?')).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: 'Finalize' }).at(-1)!);
    await waitFor(() => expect(base.onSubmit).toHaveBeenCalledOnce());
    expect(base.onApprove).not.toHaveBeenCalled();
  });

  it('retains active task uploads and completes using the saved attachment', async () => {
    const attachment = {
      id: 'attachment-1', requirementKey: 'document-1', fileName: 'signed.pdf',
      filePath: 'protected-reference', contentType: 'application/pdf', fileSizeBytes: 3,
      uploadedAt: '2026-09-12T05:00:00Z', uploadedById: 'actor-1',
    };
    vi.mocked(workflowApiService.uploadStepAttachment).mockResolvedValue(attachment);
    const complete = vi.fn(async () => ({ success: true }));
    render(<WorkflowApprovalActions {...base} status="Submitted" onCompleteTask={complete} workflowSummary={{
      ...direct, approvalRequired: true, hasActiveInstance: true, currentStepInstanceId: 'step-1',
      currentStepType: WorkflowStepType.Manual, canCurrentUserComplete: true,
      currentStepTaskConfig: { taskActionType: 'document', requiresDocument: true, documentName: 'Signed copy' },
    }} />);
    fireEvent.click(screen.getByRole('button', { name: 'Attach Documents' }));
    const file = new File(['pdf'], 'signed.pdf', { type: 'application/pdf' });
    fireEvent.change(screen.getByLabelText('Upload Signed copy'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach & Complete' }));
    await waitFor(() => expect(complete).toHaveBeenCalledWith({
      stepInstanceId: 'step-1', comments: undefined, attachments: [attachment],
    }));
    expect(workflowApiService.uploadStepAttachment).toHaveBeenCalledOnce();
  });

  it('preserves approval controls for an in-flight instance despite newly disabled configuration', () => {
    render(<WorkflowApprovalActions {...base} status="Submitted" canApproveReject workflowSummary={{
      ...direct, hasActiveInstance: true, canCurrentUserApprove: true, currentStepType: WorkflowStepType.Approval,
    }} />);
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Finalize' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit for Approval' })).not.toBeInTheDocument();
  });

  it('retains the same in-flight guard when the action component fetches its own summary', async () => {
    vi.mocked(workflowApiService.getWorkflowEntitySummary).mockResolvedValue({
      ...direct, hasActiveInstance: true, canCurrentUserApprove: true, currentStepType: WorkflowStepType.Approval,
    });
    render(<WorkflowApprovalActions {...base} loadWorkflowSummary status="Submitted" canApproveReject />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
    expect(screen.queryByRole('button', { name: 'Finalize' })).not.toBeInTheDocument();
  });

  it.each([
    { workflowSummaryLoading: true },
    { workflowSummaryError: 'Forbidden' },
    { workflowSummary: undefined },
  ])('shows unknown status and does not expose a completion bypass: %j', (state) => {
    render(<WorkflowApprovalActions {...base} workflowSummary={direct} {...state} />);
    expect(screen.getByRole('status')).toHaveTextContent(/Checking workflow|Workflow status unavailable/);
    expect(screen.queryByRole('button', { name: /Finalize|Submit for Approval|Approve|Reject/ })).not.toBeInTheDocument();
  });

  it('hides both the empty Workflow trigger and content', () => {
    render(<Tabs defaultValue="workflow"><TabsList>
      <WorkflowTabTrigger {...tabPolicy} workflowSummary={direct} />
    </TabsList><WorkflowTabContent {...base} workflowSummary={direct} /></Tabs>);
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    expect(screen.queryByText('Retained audit entries')).not.toBeInTheDocument();
  });

  it('keeps retained approval history reachable under History', () => {
    const summary = { ...direct, hasWorkflowHistory: true };
    render(<Tabs defaultValue="workflow"><TabsList>
      <WorkflowTabTrigger {...tabPolicy} workflowSummary={summary} />
    </TabsList><WorkflowTabContent {...base} workflowSummary={summary} /></Tabs>);
    expect(screen.getByRole('tab', { name: 'History' })).toBeEnabled();
    expect(screen.getByText('Retained audit entries')).toBeInTheDocument();
  });

  it('preserves an operational history tab independently of approval history', () => {
    render(<Tabs defaultValue="workflow"><TabsList>
      <WorkflowTabTrigger {...tabPolicy} workflowSummary={direct} hasHistoryContent />
    </TabsList><WorkflowTabContent {...base} workflowSummary={direct}><p>Maintenance lifecycle</p></WorkflowTabContent></Tabs>);
    expect(screen.getByRole('tab', { name: 'History' })).toBeInTheDocument();
    expect(screen.getByText('Maintenance lifecycle')).toBeInTheDocument();
  });
});
