import { describe, expect, it } from 'vitest';

import { buildWorkflowRoutingDescription } from './WorkflowApprovalActions';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

const summary = (pendingApprovers: WorkflowEntitySummaryDto['pendingApprovers']): WorkflowEntitySummaryDto => ({
  entityType: 'EstateLandAcquisition',
  entityId: '00000000-0000-0000-0000-000000000001',
  hasActiveInstance: true,
  approvalRequired: true,
  pendingApprovers,
  canCurrentUserApprove: false,
  canCurrentUserRecall: false,
  canCurrentUserComplete: false,
  canCurrentUserResubmit: false,
  currentStepChecklist: [],
  currentStepTaskAttachments: [],
});

describe('buildWorkflowRoutingDescription', () => {
  it('indicates the next approver role after a workflow action routes forward', () => {
    expect(buildWorkflowRoutingDescription(summary([
      { approverRole: 'Estate Manager' },
    ]))).toBe('Routed to Estate Manager.');
  });

  it('prefers approver names and compacts long approver lists', () => {
    expect(buildWorkflowRoutingDescription(summary([
      { approverName: 'Ama Mensah', approverRole: 'Estate Manager' },
      { approverName: 'Kojo Owusu', approverRole: 'Finance Manager' },
      { approverRole: 'Managing Director' },
    ]))).toBe('Routed to Ama Mensah, Kojo Owusu +1.');
  });

  it('omits routing text when there is no active pending approver', () => {
    expect(buildWorkflowRoutingDescription(summary([]))).toBeUndefined();
    expect(buildWorkflowRoutingDescription({ ...summary([]), hasActiveInstance: false })).toBeUndefined();
  });
});
