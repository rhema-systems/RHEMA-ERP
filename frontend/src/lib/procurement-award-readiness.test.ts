import { afterEach, describe, expect, it, vi } from 'vitest';

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
  afterEach(() => vi.restoreAllMocks());

  it('normalizes server action names without manufacturing client readiness', () => {
    expect(hasAwardReadinessAction(['EvaluateReadiness'], 'EvaluateReadiness')).toBe(
      true
    );
    expect(hasAwardReadinessAction(['RecordAward'], 'RecordAward')).toBe(true);
    expect(hasAwardReadinessAction(['ViewHistory'], 'RecordAward')).toBe(false);
  });

  it('keeps the optimistic source hash for a current retained decision', () => {
    vi.spyOn(crypto, 'randomUUID').mockReturnValue(
      '00000000-0000-4000-8000-000000000209'
    );
    const latest = {
      isCurrent: true,
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

  it('re-evaluates a historical decision without binding its obsolete source hash', () => {
    const latest = {
      isCurrent: false,
      sourceIntegrityHash: 'obsolete-source-hash',
      recommendation: {
        subjectIds: ['bid-1'],
        businessPartnerIds: ['supplier-1'],
      },
    } as ProcurementAwardReadinessDecision;

    const request = createAwardReadinessEvaluationRequest(latest);

    expect(request.expectedSourceIntegrityHash).toBeUndefined();
    expect(JSON.parse(JSON.stringify(request))).not.toHaveProperty(
      'expectedSourceIntegrityHash'
    );
    expect(request.expectedRecommendedSubjectIds).toEqual(['bid-1']);
    expect(request.expectedBusinessPartnerIds).toEqual(['supplier-1']);
    expect(latest.sourceIntegrityHash).toBe('obsolete-source-hash');
  });

  it('conservatively retains a supplied hash when currentness is unavailable', () => {
    const latest = {
      sourceIntegrityHash: 'unverified-source-hash',
      recommendation: {
        subjectIds: ['bid-1'],
        businessPartnerIds: ['supplier-1'],
      },
    } as ProcurementAwardReadinessDecision;

    expect(
      createAwardReadinessEvaluationRequest(latest).expectedSourceIntegrityHash
    ).toBe('unverified-source-hash');
  });

  it('does not manufacture lineage when no retained decision is available', () => {
    const request = createAwardReadinessEvaluationRequest();

    expect(request.expectedSourceIntegrityHash).toBeUndefined();
    expect(request.expectedRecommendedSubjectIds).toEqual([]);
    expect(request.expectedBusinessPartnerIds).toEqual([]);
  });

  it('uses a fresh idempotency key for each new evaluation attempt', () => {
    vi.spyOn(crypto, 'randomUUID')
      .mockReturnValueOnce('00000000-0000-4000-8000-000000000001')
      .mockReturnValueOnce('00000000-0000-4000-8000-000000000002');

    const first = createAwardReadinessEvaluationRequest();
    const second = createAwardReadinessEvaluationRequest();

    expect(first.idempotencyKey).not.toBe(second.idempotencyKey);
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
