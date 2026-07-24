import { describe, expect, it, vi } from 'vitest';

import {
  awardReadinessRemediationHref,
  awardReadinessSodPresentation,
  createAwardReadinessEvaluationRequest,
  hasAwardReadinessAction,
  isAwardReadinessSodPolicyEffective,
  isCurrentActorAwardEvaluator,
} from './procurement-award-readiness';
import type {
  ProcurementAwardReadinessDecision,
  ProcurementEvaluatorAwardApproverSodStatus,
} from '@/types/procurement-award-readiness';

describe('procurement award readiness helpers', () => {
  it('normalizes server action names without manufacturing client readiness', () => {
    expect(hasAwardReadinessAction(['EvaluateReadiness'], 'EvaluateReadiness')).toBe(
      true
    );
    expect(hasAwardReadinessAction(['RecordAward'], 'RecordAward')).toBe(true);
    expect(hasAwardReadinessAction(['ViewHistory'], 'RecordAward')).toBe(false);
  });

  it('builds a stale-safe server evaluation request from retained lineage only', () => {
    vi.spyOn(crypto, 'randomUUID').mockReturnValue(
      '00000000-0000-4000-8000-000000000209'
    );
    const latest = {
      sourceIntegrityHash: 'source-hash',
      recommendation: {
        subjectIds: ['bid-1'],
        businessPartnerIds: ['supplier-1'],
      },
    } as ProcurementAwardReadinessDecision;

    expect(createAwardReadinessEvaluationRequest(latest)).toEqual({
      idempotencyKey:
        'tdc0209-evaluate-00000000-0000-4000-8000-000000000209',
      expectedRecommendedSubjectIds: ['bid-1'],
      expectedBusinessPartnerIds: ['supplier-1'],
      expectedSourceIntegrityHash: 'source-hash',
    });
    expect(
      createAwardReadinessEvaluationRequest(latest)
    ).not.toHaveProperty('isReady');
  });

  it('routes remediation to existing shared controls for Tender and RFQ', () => {
    expect(
      awardReadinessRemediationHref('Tender', 'tender-1', 'ScoreIntegrity')
    ).toBe('/procurement/tenders/tender-1/committee-controls');
    expect(
      awardReadinessRemediationHref(
        'RequestForQuotation',
        'rfq-1',
        'AuthorityAndWorkflow'
      )
    ).toBe('/procurement/rfqs/rfq-1/controls');
    expect(
      awardReadinessRemediationHref(
        'ExceptionalSourcing',
        'tender-2',
        'Recommendation'
      )
    ).toBe('/procurement/tenders/tender-2/exception-controls');
  });

  it('derives current-actor and effective hard-stop presentation from server status', () => {
    const status = {
      allowed: false,
      code: 'SOD_CONFLICT',
      currentActorUserId: 'actor-1',
      evaluatorUserIds: ['actor-1', 'actor-2'],
    } as ProcurementEvaluatorAwardApproverSodStatus;

    expect(awardReadinessSodPresentation(status)).toBe('Blocked');
    expect(isAwardReadinessSodPolicyEffective(status)).toBe(true);
    expect(isCurrentActorAwardEvaluator(status)).toBe(true);

    expect(
      awardReadinessSodPresentation({
        ...status,
        allowed: true,
        code: 'SOD_NOT_APPLICABLE',
      })
    ).toBe('NotApplicable');
    expect(
      isAwardReadinessSodPolicyEffective({
        ...status,
        code: 'SOD_POLICY_INCOMPLETE',
      })
    ).toBe(false);
  });
});
