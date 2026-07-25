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
  decisionCode: 'PR_APP_OR_EXCEPTION_REQUIRED',
  message: 'Acknowledged APP linkage or approved exception required.',
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

  it('labels acknowledged APP readiness as controlled and ready', () => {
    expect(getSubmissionControlPresentation(readiness({
      isCompliant: true,
      canSubmit: true,
      basis: 'AcknowledgedAPP'
    }))).toMatchObject({ tone: 'ready', basisLabel: 'Acknowledged APP' });
  });

  it('labels approved exception readiness without presenting it as an APP acknowledgement', () => {
    expect(getSubmissionControlPresentation(readiness({
      isCompliant: true,
      canSubmit: true,
      basis: 'ApprovedException'
    }))).toMatchObject({ tone: 'ready', basisLabel: 'Approved exception' });
  });

  it('keeps a compliant non-Draft requisition non-submittable', () => {
    expect(getSubmissionControlPresentation(readiness({
      status: 'Submitted',
      isCompliant: true,
      canSubmit: false,
      basis: 'AcknowledgedAPP'
    }))).toMatchObject({ tone: 'blocked', title: 'Submission no longer available' });
  });
});
