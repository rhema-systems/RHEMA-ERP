import { describe, expect, it } from 'vitest';

import {
  getProcurementProblemMessage,
  getTenderHeaderActions,
} from './procurement-tender-header-actions';

const input = {
  tenderId: 'tender-1',
  tenderType: 'ITB',
  sourcingCaseId: 'case-1',
  sourcingMethod: 'NationalCompetitiveTendering' as const,
  canReadProcurementRecords: true,
};

describe('tender header control actions', () => {
  it('exposes all controls for a readable case-backed formal tender', () => {
    expect(getTenderHeaderActions(input)).toEqual({
      showCommitteeControls: true,
      showAwardReadiness: true,
      showGhanepsExchange: true,
      committeeControlsHref: '/procurement/tenders/tender-1/committee-controls',
      awardReadinessHref: '/procurement/tenders/tender-1/award-readiness',
      ghanepsExchangeHref: '/procurement/tenders/tender-1/ghaneps-exchange',
    });
  });

  it('keeps supported release-only controls but hides case-only committee controls', () => {
    const actions = getTenderHeaderActions({
      ...input,
      sourcingCaseId: undefined,
    });

    expect(actions.showCommitteeControls).toBe(false);
    expect(actions.showAwardReadiness).toBe(true);
    expect(actions.showGhanepsExchange).toBe(true);
  });

  it.each(['RestrictedTendering', 'SingleSource', 'PettyPurchase'] as const)(
    'routes %s through exceptional-sourcing workspaces',
    (sourcingMethod) => {
      const actions = getTenderHeaderActions({ ...input, sourcingMethod });

      expect(
        actions.awardReadinessHref.endsWith(
          '/award-readiness?sourceType=ExceptionalSourcing'
        )
      ).toBe(true);
      expect(
        actions.ghanepsExchangeHref.endsWith(
          '/ghaneps-exchange?sourceType=ExceptionalSourcing'
        )
      ).toBe(true);
    }
  );

  it('supports legacy numeric exceptional methods', () => {
    const actions = getTenderHeaderActions({ ...input, sourcingMethod: 4 });
    expect(actions.awardReadinessHref).toContain(
      'sourceType=ExceptionalSourcing'
    );
  });

  it('hides all three actions for RFQs and unauthorized users', () => {
    for (const changed of [
      { ...input, tenderType: 'RFQ' },
      { ...input, canReadProcurementRecords: false },
    ]) {
      const actions = getTenderHeaderActions(changed);
      expect(actions.showCommitteeControls).toBe(false);
      expect(actions.showAwardReadiness).toBe(false);
      expect(actions.showGhanepsExchange).toBe(false);
    }
  });
});

describe('procurement ProblemDetails presentation', () => {
  it('shows the server detail and structured code', () => {
    expect(
      getProcurementProblemMessage({
        response: {
          data: {
            detail: 'The tender source type does not match.',
            extensions: { code: 'AWARD_READINESS_SOURCE_TYPE_MISMATCH' },
          },
        },
      })
    ).toBe(
      'The tender source type does not match. (AWARD_READINESS_SOURCE_TYPE_MISMATCH)'
    );
  });

  it('does not duplicate a code already included in the detail', () => {
    expect(
      getProcurementProblemMessage({
        detail: 'GHANEPS_SOURCE_TYPE_MISMATCH: Select the matching source.',
        code: 'GHANEPS_SOURCE_TYPE_MISMATCH',
      })
    ).toBe('GHANEPS_SOURCE_TYPE_MISMATCH: Select the matching source.');
  });
});
