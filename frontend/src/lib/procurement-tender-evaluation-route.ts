import { getTenderPublicationPresentation } from '@/lib/procurement-tender-publication';
import type { ProcurementMethodType } from '@/types/procurement-policy';

export type TenderEvaluationSource = {
  id: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
  usesControlledTenderLifecycle?: boolean;
};

export type TenderEvaluationRoute = {
  mode: 'controlled' | 'legacy';
  committeeHref: string;
  evaluationHref: string;
  evaluationLabel: string;
};

export function getTenderEvaluationRoute(
  tender: TenderEvaluationSource,
  bidId?: string
): TenderEvaluationRoute {
  const presentation = getTenderPublicationPresentation(tender);
  const committeeHref = `/procurement/tenders/${tender.id}/committee-controls`;

  if (presentation.requiresControlledPublication) {
    return {
      mode: 'controlled',
      committeeHref,
      evaluationHref: bidId
        ? `/procurement/tenders/${tender.id}/controls?bidId=${encodeURIComponent(bidId)}#evaluation-workspace`
        : `/procurement/tenders/${tender.id}/controls#evaluation-workspace`,
      evaluationLabel: bidId ? 'Evaluate this bid' : 'Open tender evaluation',
    };
  }

  return {
    mode: 'legacy',
    committeeHref,
    evaluationHref: bidId
      ? `/procurement/evaluations/create?bidId=${bidId}`
      : `/procurement/tenders/${tender.id}`,
    evaluationLabel: 'Create evaluation',
  };
}

export async function resolveTenderEvaluationHref(
  tenderId: string,
  bidId: string,
  cachedTender: TenderEvaluationSource | undefined,
  loadTender: (id: string) => Promise<TenderEvaluationSource>
): Promise<string> {
  const tender =
    cachedTender?.sourcingMethod != null &&
    (!cachedTender.sourcingCaseId ||
      cachedTender.usesControlledTenderLifecycle !== undefined)
      ? cachedTender
      : await loadTender(tenderId);

  return getTenderEvaluationRoute(tender, bidId).evaluationHref;
}
