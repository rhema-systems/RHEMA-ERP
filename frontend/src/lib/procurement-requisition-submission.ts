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
      title: 'Ready for approval workflow',
      basisLabel: 'Required details complete'
    };
  }

  return {
    tone: 'blocked',
    title: readiness.isCompliant ? 'Submission no longer available' : 'Required details incomplete',
    basisLabel: 'Action required'
  };
}
