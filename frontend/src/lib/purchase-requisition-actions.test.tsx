import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import type { User } from '@/types';
import {
  WorkflowStepType,
  type WorkflowEntitySummaryDto,
} from '@/types/workflow';
import {
  canRenderPurchaseRequisitionApprovalActions,
  PURCHASE_REQUISITION_APPROVE_PERMISSION,
} from './purchase-requisition-actions';

const userWith = (roles: string[], permissions: string[] = []): User =>
  ({
    id: 'user-1',
    username: 'pr-user',
    email: 'pr-user@example.com',
    isActive: true,
    roles,
    permissions,
  }) satisfies User;

const assignedApproval: WorkflowEntitySummaryDto = {
  entityType: 'PurchaseRequisition',
  entityId: 'pr-1',
  hasActiveInstance: true,
  approvalRequired: true,
  currentStepName: 'Department approval',
  currentStepInstanceId: 'step-1',
  currentStepType: WorkflowStepType.Approval,
  canCurrentUserApprove: true,
  currentUserApprovalId: 'approval-1',
  pendingApprovers: [{ approverName: 'Assigned User' }],
};

function renderAssignedActions(
  user: User,
  workflowCanCurrentUserApprove = true
) {
  const workflowSummary = {
    ...assignedApproval,
    canCurrentUserApprove: workflowCanCurrentUserApprove,
  };
  render(
    <WorkflowApprovalActions
      entityType="PurchaseRequisition"
      entityId="pr-1"
      entityLabel="Purchase Requisition"
      status="Pending Approval"
      workflowSummary={workflowSummary}
      loadWorkflowSummary={false}
      canApproveReject={canRenderPurchaseRequisitionApprovalActions(
        'Pending Approval',
        user,
        workflowSummary.canCurrentUserApprove
      )}
      onApprove={vi.fn().mockResolvedValue(undefined)}
      onReject={vi.fn().mockResolvedValue(undefined)}
    />
  );
}

function expectApprovalActionsHidden() {
  expect(
    screen.queryByRole('button', { name: 'Approve' })
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Reject' })
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Delegate' })
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Send back' })
  ).not.toBeInTheDocument();
}

describe('purchase-requisition approval action access', () => {
  it.each(['Employee', 'TDC_QUANTITY_SURVEYOR'])(
    'does not render approval governance actions for an assigned %s without the procurement approval capability',
    (role) => {
      renderAssignedActions(userWith([role], ['procurement.records.read']));

      expectApprovalActionsHidden();
    }
  );

  it('renders approval governance actions for an assigned explicit procurement approver', () => {
    renderAssignedActions(
      userWith(
        ['TDC_FINANCE_REVIEWER'],
        ['procurement.records.read', PURCHASE_REQUISITION_APPROVE_PERMISSION]
      )
    );

    expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Delegate' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Send back' })
    ).toBeInTheDocument();
  });

  it('keeps the SuperAdmin bypass but does not grant TenantAdmin a PR approval bypass', () => {
    expect(
      canRenderPurchaseRequisitionApprovalActions(
        'Submitted',
        userWith(['SuperAdmin']),
        true
      )
    ).toBe(true);
    expect(
      canRenderPurchaseRequisitionApprovalActions(
        'Submitted',
        userWith(['TenantAdmin']),
        true
      )
    ).toBe(false);
  });

  it('does not treat a wildcard as a PR approval bypass and never shows actions outside an approval status', () => {
    const wildcardUser = userWith(['Employee'], ['*']);

    expect(
      canRenderPurchaseRequisitionApprovalActions(
        'Pending Approval',
        wildcardUser,
        true
      )
    ).toBe(false);
    expect(
      canRenderPurchaseRequisitionApprovalActions(
        'Approved',
        userWith(['SuperAdmin']),
        true
      )
    ).toBe(false);
  });

  it('does not render actions for a capable user who is not assigned to the workflow step', () => {
    renderAssignedActions(
      userWith(
        ['TDC_FINANCE_REVIEWER'],
        [PURCHASE_REQUISITION_APPROVE_PERMISSION]
      ),
      false
    );

    expectApprovalActionsHidden();
  });
});
