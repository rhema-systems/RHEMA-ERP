import { getTenderPublicationPresentation } from '@/lib/procurement-tender-publication';
import type { ProcurementMethodType } from '@/types/procurement-policy';

type TenderEvaluationSource = {
  id: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
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
      evaluationHref: `/procurement/tenders/${tender.id}/controls`,
      evaluationLabel:
        presentation.advancedControlLabel ?? 'Controlled tender evaluation',
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
