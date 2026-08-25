import { describe, expect, it } from 'vitest';
import type { PurchaseRequisitionSubmissionReadinessDto } from '@/services/purchasingService';
import { getSubmissionControlPresentation } from './procurement-requisition-submission';

const readiness = (
  overrides: Partial<PurchaseRequisitionSubmissionReadinessDto> = {}
): PurchaseRequisitionSubmissionReadinessDto => ({
  requisitionId: 'pr-104',
  requisitionNumber: 'PR-2026-104',
  status: 'Draft',
  isCompliant: false,
  canSubmit: false,
  decisionCode: 'PR_REQUIRED_DETAILS_INCOMPLETE',
  message: 'Required requisition details are incomplete.',
  requiredActions: [],
  ...overrides
});

describe('getSubmissionControlPresentation', () => {
  it('shows a neutral evaluation state while readiness is loading', () => {
    expect(getSubmissionControlPresentation(undefined, true)).toEqual({
      tone: 'neutral',
      title: 'Checking submission control',
      basisLabel: 'Not evaluated'
    });
  });

  it('uses required business details as the submission basis', () => {
    expect(getSubmissionControlPresentation(readiness({
      isCompliant: true,
      canSubmit: true,
      basis: 'BusinessRequirements'
    }))).toMatchObject({ tone: 'ready', basisLabel: 'Required details complete' });
  });

  it('does not turn optional APP metadata into the submission basis', () => {
    expect(getSubmissionControlPresentation(readiness({
      isCompliant: true,
      canSubmit: true,
      basis: 'AcknowledgedAPP'
    }))).toMatchObject({ tone: 'ready', basisLabel: 'Required details complete' });
  });

  it('keeps a compliant non-Draft requisition non-submittable', () => {
    expect(getSubmissionControlPresentation(readiness({
      status: 'Submitted',
      isCompliant: true,
      canSubmit: false,
      basis: 'BusinessRequirements'
    }))).toMatchObject({ tone: 'blocked', title: 'Submission no longer available' });
  });
});
