import type { PurchaseRequisitionAuthorityReadinessDto } from '@/services/purchasingService';

export type AuthorityControlTone = 'neutral' | 'ready' | 'blocked';

export interface AuthorityControlPresentation {
  tone: AuthorityControlTone;
  title: string;
  basisLabel: string;
}

export function getAuthorityControlPresentation(
  readiness?: PurchaseRequisitionAuthorityReadinessDto,
  loading = false
): AuthorityControlPresentation {
  if (loading || !readiness) {
    return {
      tone: 'neutral',
      title: loading
        ? 'Resolving approval authority'
        : 'Authority control unavailable',
      basisLabel: 'Not evaluated',
    };
  }

  if (readiness.authorityRouteId) {
    return {
      tone: 'ready',
      title: readiness.currentWorkflowStage
        ? `In ${readiness.currentWorkflowStage}`
        : 'Immutable route captured',
      basisLabel: `Attempt ${readiness.attemptNumber ?? 1}`,
    };
  }

  if (readiness.canSubmit) {
    return {
      tone: 'ready',
      title: `${readiness.steps.length} authority stage${readiness.steps.length === 1 ? '' : 's'} resolved`,
      basisLabel: 'Route ready',
    };
  }

  return {
    tone: 'neutral',
    title: 'Optional policy guidance not configured',
    basisLabel: 'Advisory',
  };
}
