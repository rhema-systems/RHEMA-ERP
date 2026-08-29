import { describe, expect, it } from 'vitest';
import type {
  PurchaseRequisitionBudgetControlHistoryDto,
  PurchaseRequisitionBudgetReadinessDto,
} from '@/services/purchasingService';
import {
  getBudgetControlPresentation,
  getPurchaseRequisitionBudgetControlHistory,
} from './procurement-requisition-budget';

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

  it('distinguishes an existing downstream commitment from unreserved availability', () => {
    expect(getBudgetControlPresentation(readiness({
      isCompliant: true,
      canReserve: true,
      basis: 'ExistingCommitment',
      commitmentStatus: 'Committed'
    }))).toMatchObject({
      tone: 'ready',
      title: 'Downstream budget commitment recorded',
      basisLabel: 'Committed'
    });
  });

  it('retains downstream reservation events in the PR control history', () => {
    const event = (
      action: string
    ): PurchaseRequisitionBudgetControlHistoryDto => ({
      id: action,
      action,
      result: 'Allowed',
      actorName: 'Finance Reviewer',
      occurredAtUtc: '2026-08-29T10:00:00Z',
      integrityHash: `hash-${action}`,
    });

    expect(
      getPurchaseRequisitionBudgetControlHistory([
        event('BudgetAvailabilityConfirmed'),
        event('BudgetCommitmentReserved'),
        event('BudgetReservationReused'),
      ]).map((item) => item.action)
    ).toEqual([
      'BudgetAvailabilityConfirmed',
      'BudgetCommitmentReserved',
      'BudgetReservationReused',
    ]);
  });
});
