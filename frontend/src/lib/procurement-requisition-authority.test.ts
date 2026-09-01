import { describe, expect, it } from 'vitest';
import type { PurchaseRequisitionAuthorityReadinessDto } from '@/services/purchasingService';
import { getAuthorityControlPresentation } from './procurement-requisition-authority';

const readiness = (
  overrides: Partial<PurchaseRequisitionAuthorityReadinessDto> = {}
): PurchaseRequisitionAuthorityReadinessDto => ({
  requisitionId: 'pr-106',
  requisitionNumber: 'PR-2026-106',
  status: 'Draft',
  isCompliant: false,
  canSubmit: false,
  decisionCode: 'PR_AUTHORITY_POLICY_NOT_EFFECTIVE',
  message: 'No effective policy.',
  category: 'Goods',
  amount: 100,
  currencyCode: 'GHS',
  steps: [],
  findings: [],
  requiredActions: [],
  ...overrides,
});

describe('getAuthorityControlPresentation', () => {
  it('shows deterministic resolution while loading', () => {
    expect(getAuthorityControlPresentation(undefined, true)).toEqual({
      tone: 'neutral',
      title: 'Resolving approval authority',
      basisLabel: 'Not evaluated',
    });
  });

  it('shows a ready configured route before submission', () => {
    expect(
      getAuthorityControlPresentation(
        readiness({
          isCompliant: true,
          canSubmit: true,
          steps: [
            {
              sequence: 1,
            } as PurchaseRequisitionAuthorityReadinessDto['steps'][number],
          ],
        })
      )
    ).toMatchObject({
      tone: 'ready',
      title: '1 authority stage resolved',
      basisLabel: 'Route ready',
    });
  });

  it('shows missing policy authority coverage as optional guidance', () => {
    expect(getAuthorityControlPresentation(readiness())).toMatchObject({
      tone: 'neutral',
      title: 'Optional policy guidance not configured',
      basisLabel: 'Advisory',
    });
  });

  it('shows immutable route and current workflow stage after submission', () => {
    expect(
      getAuthorityControlPresentation(
        readiness({
          status: 'Pending Approval',
          isCompliant: true,
          authorityRouteId: 'route-1',
          attemptNumber: 2,
          currentWorkflowStage: 'Finance review',
        })
      )
    ).toEqual({
      tone: 'ready',
      title: 'In Finance review',
      basisLabel: 'Attempt 2',
    });
  });
});
