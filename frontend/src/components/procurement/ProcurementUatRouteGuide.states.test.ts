import { describe, expect, it } from 'vitest';
import type { TenderDetailDto } from '@/services/tenderService';
import type { ProcurementTenderDocumentReadiness } from '@/types/procurement-tender-document';
import type {
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
} from '@/types/procurement-evaluation-committee';
import type { PurchaseRequisitionSubmissionReadinessDto } from '@/services/purchasingService';
import {
  tenderStageStates,
  upstreamStageStates,
} from './ProcurementUatRouteGuide';

const tender = (status: string, extra: Partial<TenderDetailDto> = {}) =>
  ({
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status,
    bidCount: 1,
    sourcePurchaseRequisitionId: 'pr-1',
    sourcingCaseId: 'case-1',
    sourcingReleaseId: 'release-1',
    ...extra,
  }) as TenderDetailDto;
const expired = {
  ready: false,
  blockedReasons: ['The submission deadline has passed.'],
} as ProcurementTenderDocumentReadiness;

describe('record-backed procurement progress', () => {
  it.each(['Published', 'Closed', 'UnderEvaluation', 'Evaluated', 'Awarded'])(
    'retains publication and preparation milestones for %s despite expired publish readiness',
    (status) => {
      const states = tenderStageStates(tender(status), expired);
      expect(states['tender-rfq-preparation']?.status).toBe('complete');
      expect(states['tender-documents-publication']?.status).toBe('complete');
      expect(states['tender-documents-publication']?.blockers).toBeUndefined();
    }
  );

  it('retains publication evidence when a published source is subsequently cancelled', () => {
    const states = tenderStageStates(
      tender('Cancelled', { publishDate: '2026-09-05T18:00:00Z' }),
      expired
    );
    expect(states['tender-rfq-preparation']?.status).toBe('failed');
    expect(states['tender-documents-publication']?.status).toBe('complete');
    expect(states['bid-opening-evaluation']?.status).not.toBe('complete');
  });

  it('still blocks an unpublished approved tender with expired readiness', () => {
    expect(
      tenderStageStates(tender('Approved'), expired)[
        'tender-documents-publication'
      ]
    ).toMatchObject({
      status: 'blocked',
      blockers: expired.blockedReasons,
    });
  });

  it('uses the server publication flag as retained source evidence', () => {
    expect(
      tenderStageStates(tender('Approved'), {
        ...expired,
        isSourcePublished: true,
      })['tender-documents-publication']?.status
    ).toBe('complete');
  });

  it('does not equate one evaluated bid or submitted score sheet to a fully evaluated tender', () => {
    const states = tenderStageStates(
      tender('Closed', {
        bids: [{ status: 'Evaluated' }] as TenderDetailDto['bids'],
      })
    );
    expect(states['supplier-bidding']?.status).toBe('complete');
    expect(states['bid-opening-evaluation']?.status).toBe('in-progress');
    expect(states.award?.status).toBe('not-started');
  });

  it('keeps committee unverified when completed tender evidence does not include committee data', () => {
    const states = tenderStageStates(tender('Evaluated'));
    expect(states['evaluation-committee']?.status).toBe('unknown');
    expect(states['bid-opening-evaluation']?.status).toBe('complete');
    expect(states.award?.status).toBe('in-progress');
    expect(states['supplier-bidding']?.href).toBe(
      '/procurement/tenders/tender-1?tab=bids'
    );
  });

  it('requires all live committee readiness conditions or retained confirmed quorum evidence', () => {
    const readiness = {
      hasControl: true,
      compositionReady: true,
      appointmentsReady: true,
      declarationsReady: false,
      quorumMet: true,
    } as ProcurementEvaluationCommitteeReadiness;
    expect(
      tenderStageStates(tender('Evaluated'), undefined, readiness)[
        'evaluation-committee'
      ]?.status
    ).toBe('in-progress');
    const control = {
      meetings: [
        {
          status: 'Closed',
          quorumMet: true,
          quorumIntegrityHash: 'retained-hash',
        },
      ],
    } as ProcurementEvaluationCommitteeControl;
    expect(
      tenderStageStates(tender('Evaluated'), undefined, readiness, control)[
        'evaluation-committee'
      ]?.status
    ).toBe('complete');
    const unconfirmed = {
      meetings: [{ status: 'Draft', quorumMet: true, quorumIntegrityHash: '' }],
    } as ProcurementEvaluationCommitteeControl;
    expect(
      tenderStageStates(tender('Evaluated'), undefined, readiness, unconfirmed)[
        'evaluation-committee'
      ]?.status
    ).toBe('in-progress');
  });

  it('does not mark sourcing complete on a case id without its immutable release', () => {
    expect(
      tenderStageStates(tender('Evaluated', { sourcingReleaseId: undefined }))[
        'sourcing-release-case'
      ]?.status
    ).toBe('blocked');
  });

  it('never marks approval complete merely because the source is evaluated', () => {
    expect(tenderStageStates(tender('Evaluated')).award?.status).not.toBe(
      'complete'
    );
    expect(tenderStageStates(tender('Awarded')).award?.status).toBe('complete');
  });

  it('does not invent upstream completion when linked evidence is unavailable', () => {
    expect(upstreamStageStates()).toEqual({});
  });

  it('requires recorded APP acknowledgement or an evidenced approved exception', () => {
    const submission = {
      appSubmissionStatus: 'Acknowledged',
      appSubmissionId: 'app-1',
    } as PurchaseRequisitionSubmissionReadinessDto;
    expect(
      upstreamStageStates(undefined, submission)['ghaneps-exchange']?.status
    ).toBe('in-progress');
    expect(
      upstreamStageStates(undefined, {
        ...submission,
        appAcknowledgementReference: 'ack-1',
        appAcknowledgedAtUtc: '2026-09-05T18:00:00Z',
      })['ghaneps-exchange']?.status
    ).toBe('complete');
    expect(
      upstreamStageStates(undefined, {
        basis: 'ApprovedException',
        exceptionApprovalReference: 'exception-1',
        exceptionApprovedAtUtc: '2026-09-05T18:00:00Z',
      } as PurchaseRequisitionSubmissionReadinessDto)['ghaneps-exchange']
        ?.status
    ).toBe('skipped');
  });
});
