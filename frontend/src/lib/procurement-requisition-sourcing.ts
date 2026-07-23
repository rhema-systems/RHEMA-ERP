import type { PurchaseRequisitionSourcingReadinessDto } from '@/services/purchasingService';

export type SourcingReleaseTone = 'neutral' | 'ready' | 'released' | 'blocked' | 'stale';

export interface SourcingReleasePresentation {
  tone: SourcingReleaseTone;
  title: string;
  badge: string;
  canRelease: boolean;
  canEnterSourcing: boolean;
}

export function getSourcingReleasePresentation(
  readiness?: PurchaseRequisitionSourcingReadinessDto,
  loading = false
): SourcingReleasePresentation {
  if (loading || !readiness) {
    return {
      tone: 'neutral',
      title: loading ? 'Checking sourcing controls' : 'Sourcing control unavailable',
      badge: loading ? 'Checking' : 'Unavailable',
      canRelease: false,
      canEnterSourcing: false,
    };
  }

  if (readiness.isReleased) {
    return {
      tone: 'released',
      title: 'Released for sourcing',
      badge: readiness.currentRelease?.releaseReference || 'Released',
      canRelease: false,
      canEnterSourcing: true,
    };
  }

  if (readiness.hasStaleRelease) {
    return {
      tone: 'stale',
      title: 'Sourcing release is stale',
      badge: 'Revalidation required',
      canRelease: readiness.canRelease,
      canEnterSourcing: false,
    };
  }

  if (readiness.canRelease) {
    return {
      tone: 'ready',
      title: 'Ready for sourcing release',
      badge: 'Ready',
      canRelease: true,
      canEnterSourcing: false,
    };
  }

  return {
    tone: 'blocked',
    title: 'Sourcing release blocked',
    badge: readiness.decisionCode,
    canRelease: false,
    canEnterSourcing: false,
  };
}
