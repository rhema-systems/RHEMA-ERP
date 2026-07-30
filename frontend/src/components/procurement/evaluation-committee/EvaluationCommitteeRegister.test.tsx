import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EvaluationCommitteeRegister } from './EvaluationCommitteeRegister';
import type {
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
} from '@/types/procurement-evaluation-committee';

const readiness = (
  overrides: Partial<ProcurementEvaluationCommitteeReadiness> = {}
): ProcurementEvaluationCommitteeReadiness => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceExists: true,
  hasControl: false,
  compositionReady: false,
  appointmentsReady: false,
  declarationsReady: false,
  quorumMet: false,
  requiredQuorum: 3,
  eligibleVotingMemberCount: 0,
  signedVotingAttendanceCount: 0,
  blockedReasons: ['Committee control is not constituted.'],
  allowedActions: ['BindCommittee'],
  ...overrides,
});

const callbacks = {
  onBind: vi.fn(),
  onActivate: vi.fn(),
  onAppointment: vi.fn(),
  onDeclareCoi: vi.fn(),
  onCreateMeeting: vi.fn(),
  onSignAttendance: vi.fn(),
  onConfirmQuorum: vi.fn(),
  onRequestRecall: vi.fn(),
  onDecideRecall: vi.fn(),
};

describe('evaluation committee history-first register', () => {
  it('shows a controlled empty state and authorized bind action', () => {
    render(
      <EvaluationCommitteeRegister
        readiness={readiness()}
        scorerEligibility={{}}
        canAdminister
        canEvaluate
        canApprove
        {...callbacks}
      />
    );

    expect(
      screen.getByText('No source-specific committee has been constituted')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', {
        name: 'Constitute evaluation committee',
      })
    ).toBeInTheDocument();
    expect(screen.getByText('Evaluation controls are not ready')).toBeInTheDocument();
  });

  it('renders composition, quorum, score-lock, and immutable timeline evidence', () => {
    const control = {
      id: 'control-1',
      sourceType: 'Tender',
      sourceId: 'tender-1',
      version: 1,
      sourceReference: 'TDR-001',
      purpose: 'Technical evaluation',
      status: 'Active',
      committeeTemplateId: 'committee-1',
      committeeCode: 'TDC-EVAL',
      committeeName: 'Evaluation Committee',
      requiredQuorum: 2,
      compositionReady: true,
      quorumMet: true,
      policySetId: 'policy-1',
      policyCode: 'TDC-POLICY',
      policyVersion: 4,
      methodRuleId: 'rule-1',
      methodRuleCode: 'NCT-GOODS',
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      compositionIntegrityHash: 'a'.repeat(64),
      requiredRoles: [
        {
          id: 'role-1',
          memberKind: 'Chair',
          roleName: 'TDC_EVALUATOR',
          minimumCount: 1,
          isVoting: true,
          isRequiredForQuorum: true,
          matchedCount: 1,
          isMet: true,
        },
      ],
      members: [],
      meetings: [],
      scoreSheets: [],
      recalls: [],
      timeline: [
        {
          occurredAtUtc: '2026-07-23T10:00:00Z',
          action: 'COMMITTEE_ACTIVATED',
          outcome: 'Allowed',
          actorUserId: 'admin',
          actorName: 'Administrator',
          reference: 'MINUTE-001',
        },
      ],
      allowedActions: ['CreateMeeting'],
      blockedReasons: [],
      rowVersion: 'AQID',
    } as ProcurementEvaluationCommitteeControl;

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({
          hasControl: true,
          compositionReady: true,
          appointmentsReady: true,
          declarationsReady: true,
          quorumMet: true,
          eligibleVotingMemberCount: 2,
          signedVotingAttendanceCount: 2,
          blockedReasons: [],
        })}
        control={control}
        scorerEligibility={{
          Technical: {
            allowed: false,
            sourceType: 'Tender',
            sourceId: 'tender-1',
            phase: 'Technical',
            actorUserId: 'user-1',
            authorizedAttempt: 1,
            blockedReasons: ['Actor is not an appointed scorer.'],
          },
        }}
        canAdminister
        canEvaluate
        canApprove
        {...callbacks}
      />
    );

    expect(screen.getByText('Evaluation Committee')).toBeInTheDocument();
    expect(
      screen.getByText('Composition, acceptance, and COI history')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Signed attendance and quorum history')
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Scorer eligibility, immutable score sheets, and controlled recall'
      )
    ).toBeInTheDocument();
    expect(screen.getByText('COMMITTEE_ACTIVATED')).toBeInTheDocument();
    expect(
      screen.getByText('Actor is not an appointed scorer.')
    ).toBeInTheDocument();
  });
});
