import type { PurchaseRequisitionSubmissionReadinessDto } from '@/services/purchasingService';

export type SubmissionControlTone = 'neutral' | 'ready' | 'blocked';

export interface SubmissionControlPresentation {
  tone: SubmissionControlTone;
  title: string;
  basisLabel: string;
}

export function getSubmissionControlPresentation(
  readiness?: PurchaseRequisitionSubmissionReadinessDto,
  loading = false
): SubmissionControlPresentation {
  if (loading || !readiness) {
    return {
      tone: 'neutral',
      title: loading ? 'Checking submission control' : 'Submission control unavailable',
      basisLabel: 'Not evaluated'
    };
  }

  if (readiness.canSubmit) {
    return {
      tone: 'ready',
      title: 'Ready for controlled submission',
      basisLabel: readiness.basis === 'ApprovedException' ? 'Approved exception' : 'Acknowledged APP'
    };
  }

  return {
    tone: 'blocked',
    title: readiness.isCompliant ? 'Submission no longer available' : 'Submission control blocked',
    basisLabel: readiness.basis === 'ApprovedException'
      ? 'Approved exception'
      : readiness.basis === 'AcknowledgedAPP'
        ? 'Acknowledged APP'
        : 'No eligible basis'
  };
}
