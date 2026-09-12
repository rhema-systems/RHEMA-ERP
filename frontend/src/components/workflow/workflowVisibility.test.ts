import { describe, expect, it } from 'vitest';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';
import { getWorkflowVisibility } from './workflowVisibility';

const direct: WorkflowEntitySummaryDto = {
  entityType: 'PurchaseOrder', entityId: 'po-1', approvalRequired: false,
  hasActiveInstance: false, hasWorkflowHistory: false, canCurrentUserApprove: false, pendingApprovers: [],
};

describe('optional approval visibility', () => {
  it('hides approval controls and empty Workflow tab only after an explicit no-approval policy', () => {
    expect(getWorkflowVisibility({ summary: direct })).toMatchObject({
      known: true, direct: true, showTab: false, showApprovalControls: false,
    });
  });

  it('keeps retained audit accessible as History without implying current approval', () => {
    expect(getWorkflowVisibility({ summary: { ...direct, hasWorkflowHistory: true } })).toMatchObject({
      direct: true, showTab: true, tabLabel: 'History', showApprovalControls: false,
    });
  });

  it('preserves an existing workflow even if the configuration says approval is no longer required', () => {
    expect(getWorkflowVisibility({ summary: { ...direct, hasActiveInstance: true } })).toMatchObject({
      direct: false, approvalRequired: true, showTab: true, showApprovalControls: true, tabLabel: 'Workflow',
    });
  });

  it.each([
    {},
    { summary: direct, loading: true },
    { summary: direct, error: '403' },
    { summary: { ...direct, approvalRequired: undefined } as unknown as WorkflowEntitySummaryDto },
    { summary: { ...direct, hasActiveInstance: undefined } as unknown as WorkflowEntitySummaryDto },
  ])('fails closed for incomplete or unavailable status: %j', (input) => {
    expect(getWorkflowVisibility(input)).toMatchObject({
      known: false, direct: false, showTab: true, showApprovalControls: false,
    });
  });

  it('does not infer retained history from a domain status or old instance identifier', () => {
    expect(getWorkflowVisibility({ summary: { ...direct, workflowInstanceId: 'old' } }).showTab).toBe(false);
  });

  it('retains the Workflow tab for a new approval-required record', () => {
    expect(getWorkflowVisibility({ summary: { ...direct, approvalRequired: true } })).toMatchObject({
      direct: false, showTab: true, tabLabel: 'Workflow',
    });
  });
});
