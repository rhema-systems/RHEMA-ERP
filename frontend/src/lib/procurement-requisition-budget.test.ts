import { describe, expect, it } from 'vitest';
import type { PurchaseRequisitionBudgetReadinessDto } from '@/services/purchasingService';
import { getBudgetControlPresentation } from './procurement-requisition-budget';

const readiness = (
  overrides: Partial<PurchaseRequisitionBudgetReadinessDto> = {}
): PurchaseRequisitionBudgetReadinessDto => ({
  requisitionId: 'pr-105',
  requisitionNumber: 'PR-2026-105',
  status: 'Draft',
  isCompliant: false,
  canReserve: false,
  decisionCode: 'PR_BUDGET_REQUIRED',
  message: 'An approved budget is required.',
  requestedAmount: 100,
  allocatedAmount: 0,
  utilizedAmount: 0,
  committedAmount: 0,
  availableAmount: 0,
  shortfallAmount: 100,
  isOverride: false,
  requiredActions: [],
  ...overrides
});

describe('getBudgetControlPresentation', () => {
  it('shows the Finance evaluation state while loading', () => {
    expect(getBudgetControlPresentation(undefined, true)).toEqual({
      tone: 'neutral',
      title: 'Checking Finance budget',
      basisLabel: 'Not evaluated'
    });
  });

  it('labels sufficient approved budget as available without reserving it', () => {
    expect(getBudgetControlPresentation(readiness({
      isCompliant: true,
      canReserve: true,
      basis: 'ApprovedBudget'
    }))).toMatchObject({
      tone: 'ready',
      title: 'Approved budget is available',
      basisLabel: 'Availability confirmed'
    });
  });

  it('distinguishes a traceable authorized override', () => {
    expect(getBudgetControlPresentation(readiness({
      isCompliant: true,
      canReserve: true,
      basis: 'AuthorizedOverride',
      isOverride: true
    }))).toMatchObject({ tone: 'ready', basisLabel: 'Authorized override' });
  });

  it('shows insufficient budget as a hard stop', () => {
    expect(getBudgetControlPresentation(readiness())).toMatchObject({
      tone: 'blocked',
      title: 'Finance budget blocked',
      basisLabel: 'Hard stop'
    });
  });

  it('shows an idempotent existing reservation as protected', () => {
    expect(getBudgetControlPresentation(readiness({
      isCompliant: true,
      canReserve: true,
      basis: 'ExistingCommitment',
      commitmentStatus: 'Reserved'
    }))).toMatchObject({
      tone: 'ready',
      title: 'Existing commitment retained',
      basisLabel: 'Active commitment'
    });
  });
});
