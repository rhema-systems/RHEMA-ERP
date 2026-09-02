import { describe, expect, it } from 'vitest';

import { getTenderEvaluationRoute } from './procurement-tender-evaluation-route';

describe('tender evaluation routing', () => {
  it.each([
    'NationalCompetitiveTendering',
    'InternationalCompetitiveTendering',
    'QualityBasedSelection',
    'QualityAndCostBasedSelection',
  ] as const)(
    'routes %s through the signed controlled tender lifecycle',
    (sourcingMethod) => {
      const route = getTenderEvaluationRoute(
        {
          id: 'tender-1',
          sourcingCaseId: 'case-1',
          sourcingMethod,
        },
        'bid-1'
      );

      expect(route.mode).toBe('controlled');
      expect(route.evaluationHref).toBe(
        '/procurement/tenders/tender-1/controls'
      );
      expect(route.evaluationHref).not.toContain('/evaluations/create');
      expect(route.committeeHref).toBe(
        '/procurement/tenders/tender-1/committee-controls'
      );
    }
  );

  it('retains the legacy per-bid route for a non-controlled tender', () => {
    const route = getTenderEvaluationRoute(
      { id: 'tender-2', sourcingMethod: 'RequestForQuotation' },
      'bid-2'
    );

    expect(route.mode).toBe('legacy');
    expect(route.evaluationHref).toBe(
      '/procurement/evaluations/create?bidId=bid-2'
    );
  });

  it('fails closed to the legacy release route when no locked sourcing case exists', () => {
    const route = getTenderEvaluationRoute(
      { id: 'tender-3', sourcingMethod: 'NationalCompetitiveTendering' },
      'bid-3'
    );

    expect(route.mode).toBe('legacy');
  });
});
