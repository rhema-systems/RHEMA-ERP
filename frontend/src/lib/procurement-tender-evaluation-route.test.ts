import { describe, expect, it } from 'vitest';

import {
  getTenderEvaluationRoute,
  resolveTenderEvaluationHref,
} from './procurement-tender-evaluation-route';

describe('tender evaluation routing', () => {
  it('uses the standard case route after loading the authoritative lifecycle mode', async () => {
    const href = await resolveTenderEvaluationHref(
      'tender-1',
      'bid-1',
      {
        id: 'tender-1',
        sourcingCaseId: 'case-1',
        sourcingMethod: 'NationalCompetitiveTendering',
      },
      async () => ({
        id: 'tender-1',
        sourcingCaseId: 'case-1',
        sourcingMethod: 'NationalCompetitiveTendering',
        usesControlledTenderLifecycle: false,
      })
    );
    expect(href).toBe('/procurement/evaluations/create?bidId=bid-1');
  });

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
        '/procurement/tenders/tender-1/controls?bidId=bid-1#evaluation-workspace'
      );
      expect(route.evaluationHref).not.toContain('/evaluations/create');
      expect(route.evaluationLabel).toBe('Evaluate this bid');
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

  it('opens the tender-wide scoring workspace when no bid is selected', () => {
    const route = getTenderEvaluationRoute({
      id: 'tender-1',
      sourcingCaseId: 'case-1',
      sourcingMethod: 'NationalCompetitiveTendering',
    });

    expect(route.evaluationHref).toBe(
      '/procurement/tenders/tender-1/controls#evaluation-workspace'
    );
    expect(route.evaluationLabel).toBe('Open tender evaluation');
  });

  it('fails closed to the legacy release route when no locked sourcing case exists', () => {
    const route = getTenderEvaluationRoute(
      { id: 'tender-3', sourcingMethod: 'NationalCompetitiveTendering' },
      'bid-3'
    );

    expect(route.mode).toBe('legacy');
  });

  it('reloads incomplete list data before selecting the evaluation route', async () => {
    const href = await resolveTenderEvaluationHref(
      'tender-1',
      'bid-1',
      { id: 'tender-1', sourcingCaseId: 'case-1' },
      async () => ({
        id: 'tender-1',
        sourcingCaseId: 'case-1',
        sourcingMethod: 'NationalCompetitiveTendering',
      })
    );

    expect(href).toBe(
      '/procurement/tenders/tender-1/controls?bidId=bid-1#evaluation-workspace'
    );
  });
});
