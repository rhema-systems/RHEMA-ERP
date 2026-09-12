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
  onRetireDraft: vi.fn(),
  onAppointment: vi.fn(),
  onDeclareCoi: vi.fn(),
  onCreateMeeting: vi.fn(),
  onSignAttendance: vi.fn(),
  onConfirmQuorum: vi.fn(),
  onRequestRecall: vi.fn(),
  onDecideRecall: vi.fn(),
};

const activeControl = (
  overrides: Partial<ProcurementEvaluationCommitteeControl> = {}
): ProcurementEvaluationCommitteeControl => ({
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
  quorumMet: false,
  policySetId: 'policy-1',
  policyCode: 'TDC-POLICY',
  policyVersion: 4,
  methodRuleId: 'rule-1',
  methodRuleCode: 'NCT-GOODS',
  effectiveFromUtc: '2026-07-23T00:00:00Z',
  compositionIntegrityHash: 'a'.repeat(64),
  requiredRoles: [],
  members: [],
  meetings: [],
  scoreSheets: [],
  recalls: [],
  timeline: [],
  allowedActions: [],
  blockedReasons: [],
  rowVersion: 'AQID',
  ...overrides,
});

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
    expect(
      screen.getByText('Evaluation controls are not ready')
    ).toBeInTheDocument();
  });

  it('does not offer appointment responses until the committee is active', () => {
    const control = {
      id: 'control-draft',
      sourceType: 'Tender',
      sourceId: 'tender-1',
      version: 1,
      sourceReference: 'TDR-001',
      purpose: 'Technical evaluation',
      status: 'Draft',
      committeeTemplateId: 'committee-1',
      committeeCode: 'TDC-EVAL',
      committeeName: 'Evaluation Committee',
      requiredQuorum: 2,
      compositionReady: true,
      quorumMet: false,
      policySetId: 'policy-1',
      policyCode: 'TDC-POLICY',
      policyVersion: 4,
      methodRuleId: 'rule-1',
      methodRuleCode: 'NCT-GOODS',
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      compositionIntegrityHash: 'a'.repeat(64),
      requiredRoles: [],
      members: [
        {
          id: 'appointment-1',
          committeeMemberId: 'member-1',
          responsibilityAssignmentId: 'assignment-1',
          userId: 'user-1',
          userDisplayName: 'Tender Evaluator',
          roleName: 'TDC_EVALUATOR',
          memberKind: 'Chair',
          isVoting: true,
          effectiveFromUtc: '2026-07-23T00:00:00Z',
          status: 'Pending',
          eligibleToScore: false,
          blockedReasons: ['Appointment acceptance is missing.'],
          rowVersion: 'AQID',
        },
      ],
      meetings: [],
      scoreSheets: [],
      recalls: [],
      timeline: [],
      allowedActions: ['RespondToAppointment'],
      blockedReasons: ['The committee control is not active.'],
      rowVersion: 'AQID',
    } as ProcurementEvaluationCommitteeControl;

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({
          hasControl: true,
          blockedReasons: ['The committee control is not active.'],
        })}
        control={control}
        scorerEligibility={{}}
        currentUserId="user-1"
        canAdminister={false}
        canEvaluate
        canApprove={false}
        {...callbacks}
      />
    );

    expect(
      screen.getByText('Awaiting committee activation')
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Accept' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Decline' })
    ).not.toBeInTheDocument();
  });

  it('offers governed draft retirement and replacement without hiding history', () => {
    const control = {
      id: 'control-retired',
      sourceType: 'Tender',
      sourceId: 'tender-1',
      version: 1,
      sourceReference: 'TDR-001',
      purpose: 'Incorrect committee snapshot',
      status: 'Retired',
      committeeTemplateId: 'committee-1',
      committeeCode: 'TDC-EVAL',
      committeeName: 'Evaluation Committee',
      requiredQuorum: 2,
      compositionReady: true,
      quorumMet: false,
      policySetId: 'policy-1',
      policyCode: 'TDC-POLICY',
      policyVersion: 4,
      methodRuleId: 'rule-1',
      methodRuleCode: 'NCT-GOODS',
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      retiredAtUtc: '2026-09-04T10:00:00Z',
      retirementReason: 'Supplier-linked chair was selected in error.',
      compositionIntegrityHash: 'a'.repeat(64),
      requiredRoles: [],
      members: [],
      meetings: [],
      scoreSheets: [],
      recalls: [],
      timeline: [],
      allowedActions: ['bind'],
      blockedReasons: ['The committee control is not active.'],
      rowVersion: 'AQID',
    } as ProcurementEvaluationCommitteeControl;

    const { rerender } = render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true })}
        control={{
          ...control,
          status: 'Draft',
          allowedActions: ['retireDraft'],
        }}
        scorerEligibility={{}}
        canAdminister
        canEvaluate={false}
        canApprove={false}
        {...callbacks}
      />
    );

    screen.getByRole('button', { name: 'Retire incorrect draft' }).click();
    expect(callbacks.onRetireDraft).toHaveBeenCalledOnce();

    rerender(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true })}
        control={control}
        scorerEligibility={{}}
        canAdminister
        canEvaluate={false}
        canApprove={false}
        {...callbacks}
      />
    );
    expect(
      screen.getByText('Draft retired; history preserved')
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Supplier-linked chair was selected in error/)
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Constitute replacement' })
    ).toBeInTheDocument();
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
          Financial: {
            allowed: true,
            sourceType: 'Tender',
            sourceId: 'tender-1',
            phase: 'Financial',
            actorUserId: 'user-1',
            authorizedAttempt: 1,
            blockedReasons: [],
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
      screen.getByText('Evaluation readiness and score history')
    ).toBeInTheDocument();
    expect(screen.getByText('COMMITTEE_ACTIVATED')).toBeInTheDocument();
    expect(
      screen.getByText('Actor is not an appointed scorer.')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Open Tender Bids' })
    ).toHaveAttribute('href', '/procurement/bids?tenderId=tender-1');
  });

  it('explains why attendance signing is unavailable instead of hiding the state', () => {
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
      quorumMet: false,
      policySetId: 'policy-1',
      policyCode: 'TDC-POLICY',
      policyVersion: 4,
      methodRuleId: 'rule-1',
      methodRuleCode: 'NCT-GOODS',
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      compositionIntegrityHash: 'a'.repeat(64),
      requiredRoles: [],
      members: [],
      meetings: [
        {
          id: 'meeting-1',
          sequence: 1,
          phase: 'Technical',
          status: 'Draft',
          meetingMode: 'InPerson',
          meetingChannel: 'Boardroom',
          scheduledAtUtc: '2026-09-02T14:00:00Z',
          eligibleVotingMemberCount: 2,
          signedVotingAttendanceCount: 0,
          chairPresent: false,
          secretaryPresent: false,
          quorumMet: false,
          evidenceReference: 'MEETING-1',
          quorumIntegrityHash: 'b'.repeat(64),
          attendance: [],
          rowVersion: 'AQID',
        },
      ],
      scoreSheets: [],
      recalls: [],
      timeline: [],
      allowedActions: ['ConfirmQuorum'],
      blockedReasons: [],
      rowVersion: 'AQID',
    } as ProcurementEvaluationCommitteeControl;

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true })}
        control={control}
        scorerEligibility={{}}
        currentUserId="procurement-officer"
        canAdminister
        canEvaluate
        canApprove={false}
        {...callbacks}
      />
    );

    expect(
      screen.getByText('Attendance signing unavailable')
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Only a user appointed to this source-specific evaluation committee can sign their own attendance. Sign in as an appointed member.'
      )
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Confirm Quorum' })
    ).toBeInTheDocument();
  });

  it('shows recorded eligible attendance as a pending preview before quorum confirmation', () => {
    const chair = {
      id: 'appointment-chair',
      committeeMemberId: 'member-chair',
      responsibilityAssignmentId: 'assignment-chair',
      userId: 'chair-user',
      userDisplayName: 'Committee Chair',
      roleName: 'TDC_EVALUATOR',
      memberKind: 'Chair' as const,
      isVoting: true,
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      status: 'Accepted' as const,
      eligibleToScore: true,
      blockedReasons: [],
      rowVersion: 'AQID',
    };
    const secretary = {
      ...chair,
      id: 'appointment-secretary',
      committeeMemberId: 'member-secretary',
      responsibilityAssignmentId: 'assignment-secretary',
      userId: 'secretary-user',
      userDisplayName: 'Committee Secretary',
      memberKind: 'Secretary' as const,
    };
    const control = activeControl({
      members: [chair, secretary],
      meetings: [
        {
          id: 'meeting-draft',
          sequence: 1,
          phase: 'Technical',
          status: 'Draft',
          meetingMode: 'InPerson',
          meetingChannel: 'Boardroom',
          scheduledAtUtc: '2026-09-02T14:00:00Z',
          eligibleVotingMemberCount: 0,
          signedVotingAttendanceCount: 0,
          chairPresent: false,
          secretaryPresent: false,
          quorumMet: false,
          evidenceReference: 'MEETING-1',
          quorumIntegrityHash: 'b'.repeat(64),
          attendance: [
            {
              id: 'attendance-chair',
              appointmentId: chair.id,
              userId: chair.userId,
              userDisplayName: chair.userDisplayName,
              memberKind: 'Chair',
              isVoting: true,
              isPresent: true,
              signedAtUtc: '2026-09-02T14:01:00Z',
              signatureReference: 'signature://chair',
              evidenceReference: 'evidence://chair',
              wasEligibleAtSignature: true,
              integrityHash: 'c'.repeat(64),
            },
          ],
          rowVersion: 'AQID',
        },
      ],
      allowedActions: ['confirmQuorum'],
    });

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true })}
        control={control}
        scorerEligibility={{}}
        currentUserId={chair.userId}
        canAdminister
        canEvaluate
        canApprove={false}
        {...callbacks}
      />
    );

    expect(
      screen.getByText('Recorded attendance — pending quorum confirmation')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Recorded eligible voting attendance').parentElement
        ?.parentElement
    ).toHaveTextContent('1/2');
    expect(
      screen.getByText('Recorded Chair').parentElement?.parentElement
    ).toHaveTextContent('Present');
    expect(
      screen.getByText('Recorded Secretary').parentElement?.parentElement
    ).toHaveTextContent('Missing');
  });

  it('allows attendance signing and quorum re-confirmation after quorum failed', () => {
    const chair = {
      id: 'appointment-chair',
      committeeMemberId: 'member-chair',
      responsibilityAssignmentId: 'assignment-chair',
      userId: 'chair-user',
      userDisplayName: 'Committee Chair',
      roleName: 'TDC_EVALUATOR',
      memberKind: 'Chair' as const,
      isVoting: true,
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      status: 'Accepted' as const,
      eligibleToScore: true,
      blockedReasons: [],
      rowVersion: 'AQID',
    };
    const secretary = {
      ...chair,
      id: 'appointment-secretary',
      committeeMemberId: 'member-secretary',
      responsibilityAssignmentId: 'assignment-secretary',
      userId: 'secretary-user',
      userDisplayName: 'Committee Secretary',
      memberKind: 'Secretary' as const,
    };
    const meeting = {
      id: 'meeting-failed',
      sequence: 1,
      phase: 'Technical' as const,
      status: 'QuorumFailed' as const,
      meetingMode: 'InPerson',
      meetingChannel: 'Boardroom',
      scheduledAtUtc: '2026-09-02T14:00:00Z',
      eligibleVotingMemberCount: 2,
      signedVotingAttendanceCount: 1,
      chairPresent: true,
      secretaryPresent: false,
      quorumMet: false,
      evidenceReference: 'MEETING-1',
      quorumIntegrityHash: 'b'.repeat(64),
      attendance: [
        {
          id: 'attendance-chair',
          appointmentId: chair.id,
          userId: chair.userId,
          userDisplayName: chair.userDisplayName,
          memberKind: 'Chair' as const,
          isVoting: true,
          isPresent: true,
          signedAtUtc: '2026-09-02T14:01:00Z',
          signatureReference: 'signature://chair',
          evidenceReference: 'evidence://chair',
          wasEligibleAtSignature: true,
          integrityHash: 'c'.repeat(64),
        },
      ],
      rowVersion: 'AQID',
    };
    const control = activeControl({
      members: [chair, secretary],
      meetings: [meeting],
      allowedActions: ['signAttendance', 'confirmQuorum'],
    });
    const onSignAttendance = vi.fn();
    const onConfirmQuorum = vi.fn();

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true })}
        control={control}
        scorerEligibility={{}}
        currentUserId={secretary.userId}
        canAdminister
        canEvaluate
        canApprove={false}
        {...callbacks}
        onSignAttendance={onSignAttendance}
        onConfirmQuorum={onConfirmQuorum}
      />
    );

    screen.getByRole('button', { name: 'Sign attendance' }).click();
    screen.getByRole('button', { name: 'Confirm Quorum' }).click();

    expect(onSignAttendance).toHaveBeenCalledWith(meeting, secretary);
    expect(onConfirmQuorum).toHaveBeenCalledWith(meeting);
    expect(
      screen.getByText('Recorded eligible voting attendance').parentElement
        ?.parentElement
    ).toHaveTextContent('1/2');
  });

  it('uses the immutable persisted quorum snapshot after confirmation', () => {
    const chair = {
      id: 'appointment-chair',
      committeeMemberId: 'member-chair',
      responsibilityAssignmentId: 'assignment-chair',
      userId: 'chair-user',
      userDisplayName: 'Committee Chair',
      roleName: 'TDC_EVALUATOR',
      memberKind: 'Chair' as const,
      isVoting: true,
      effectiveFromUtc: '2026-07-23T00:00:00Z',
      status: 'Accepted' as const,
      eligibleToScore: true,
      blockedReasons: [],
      rowVersion: 'AQID',
    };
    const control = activeControl({
      quorumMet: true,
      members: [chair],
      meetings: [
        {
          id: 'meeting-confirmed',
          sequence: 1,
          phase: 'Technical',
          status: 'QuorumConfirmed',
          meetingMode: 'InPerson',
          meetingChannel: 'Boardroom',
          scheduledAtUtc: '2026-09-02T14:00:00Z',
          eligibleVotingMemberCount: 2,
          signedVotingAttendanceCount: 2,
          chairPresent: true,
          secretaryPresent: true,
          quorumMet: true,
          evidenceReference: 'MEETING-1',
          quorumIntegrityHash: 'b'.repeat(64),
          attendance: [
            {
              id: 'attendance-chair',
              appointmentId: chair.id,
              userId: chair.userId,
              userDisplayName: chair.userDisplayName,
              memberKind: 'Chair',
              isVoting: true,
              isPresent: true,
              signedAtUtc: '2026-09-02T14:01:00Z',
              signatureReference: 'signature://chair',
              evidenceReference: 'evidence://chair',
              wasEligibleAtSignature: true,
              integrityHash: 'c'.repeat(64),
            },
          ],
          rowVersion: 'AQID',
        },
      ],
    });

    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true, quorumMet: true })}
        control={control}
        scorerEligibility={{}}
        currentUserId={chair.userId}
        canAdminister
        canEvaluate
        canApprove={false}
        {...callbacks}
      />
    );

    expect(
      screen.queryByText('Recorded attendance — pending quorum confirmation')
    ).not.toBeInTheDocument();
    expect(
      screen.getByText('Eligible voting attendance').parentElement
        ?.parentElement
    ).toHaveTextContent('2/2');
    expect(
      screen.getByText('Required Chair').parentElement?.parentElement
    ).toHaveTextContent('Present');
    expect(
      screen.getByText('Required Secretary').parentElement?.parentElement
    ).toHaveTextContent('Present');
  });

  it('explains that scorer eligibility is not queried for a non-evaluator account', () => {
    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true, quorumMet: true })}
        control={activeControl({ quorumMet: true })}
        scorerEligibility={{}}
        currentUserId="procurement-officer"
        canAdminister
        canEvaluate={false}
        canApprove={false}
        {...callbacks}
      />
    );

    expect(screen.getAllByText('Not checked')).toHaveLength(3);
    expect(
      screen.getAllByText(
        'Scorer eligibility is actor-specific. Sign in as an appointed evaluator with the tender-evaluation permission to check it.'
      )
    ).toHaveLength(3);
    expect(
      screen.queryByText('Authoritative scorer eligibility is loading.')
    ).not.toBeInTheDocument();
  });

  it('shows a failed eligibility request as unavailable instead of loading forever', () => {
    render(
      <EvaluationCommitteeRegister
        readiness={readiness({ hasControl: true, quorumMet: true })}
        control={activeControl({ quorumMet: true })}
        scorerEligibility={{}}
        scorerEligibilityErrors={{
          Technical: new Error('Eligibility service unavailable.'),
          Financial: new Error('Eligibility service unavailable.'),
          Combined: new Error('Eligibility service unavailable.'),
        }}
        currentUserId="evaluator"
        canAdminister={false}
        canEvaluate
        canApprove={false}
        {...callbacks}
      />
    );

    expect(screen.getAllByText('Unavailable')).toHaveLength(3);
    expect(
      screen.getAllByText('Eligibility service unavailable.')
    ).toHaveLength(3);
    expect(
      screen.queryByText('Authoritative scorer eligibility is loading.')
    ).not.toBeInTheDocument();
  });
});
