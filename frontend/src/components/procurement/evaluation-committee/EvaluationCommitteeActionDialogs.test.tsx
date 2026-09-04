import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EvaluationCommitteeActionDialogs } from './EvaluationCommitteeActionDialogs';
import type {
  ProcurementEvaluationAppointment,
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
  ProcurementEvaluationMeeting,
} from '@/types/procurement-evaluation-committee';

const readiness: ProcurementEvaluationCommitteeReadiness = {
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceExists: true,
  hasControl: true,
  committeeControlId: 'control-1',
  status: 'Active',
  compositionReady: true,
  appointmentsReady: true,
  declarationsReady: true,
  quorumMet: false,
  requiredQuorum: 2,
  eligibleVotingMemberCount: 2,
  signedVotingAttendanceCount: 0,
  blockedReasons: [],
  allowedActions: [],
};

const member = {
  id: 'appointment-1',
  committeeMemberId: 'member-1',
  responsibilityAssignmentId: 'assignment-1',
  userId: 'user-1',
  userDisplayName: 'Tender Evaluator',
  roleName: 'TDC_EVALUATOR',
  memberKind: 'Chair',
  isVoting: true,
  effectiveFromUtc: '2026-09-02T10:00:00Z',
  status: 'Accepted',
  eligibleToScore: true,
  blockedReasons: [],
  rowVersion: 'AQID',
} as ProcurementEvaluationAppointment;

const meeting = {
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
  quorumIntegrityHash: 'a'.repeat(64),
  attendance: [],
  rowVersion: 'AQID',
} as ProcurementEvaluationMeeting;

const control = {
  id: 'control-1',
  rowVersion: 'AQID',
} as ProcurementEvaluationCommitteeControl;

const commonProps = {
  onClose: vi.fn(),
  onCompleted: vi.fn(async () => undefined),
  readiness,
  control,
};

describe('evaluation committee action evidence fields', () => {
  it('allows activation without a manually entered evidence reference', () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'activate' }}
      />
    );

    expect(
      screen.getByText('Supporting evidence reference (optional)')
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/system creates the activation audit reference/i)
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Activate committee' })
    ).toBeEnabled();
  });

  it('allows appointment acceptance without manual signature or evidence references', () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'appointment', member, accept: true }}
      />
    );

    expect(
      screen.getByText('External signature reference (optional)')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Accept appointment' })
    ).toBeEnabled();
  });

  it('allows a no-conflict declaration without manual references', async () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'coi', member }}
      />
    );

    expect(
      screen.getByText('External signature reference (optional)')
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Submit signed declaration' })
      ).toBeEnabled()
    );
  });

  it('allows self-attendance without manual signature or evidence references', () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'attendance', meeting, member }}
      />
    );

    expect(
      screen.getByText('External attendance signature reference (optional)')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Sign attendance' })
    ).toBeEnabled();
  });

  it('allows an in-person meeting notice without manual evidence', () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'meeting' }}
      />
    );

    fireEvent.change(
      screen.getByPlaceholderText(
        'Boardroom or controlled Teams/Zoom reference'
      ),
      { target: { value: 'Boardroom' } }
    );

    expect(
      screen.getByRole('button', { name: 'Create meeting' })
    ).toBeEnabled();
  });

  it('allows server quorum confirmation without manual evidence', () => {
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'quorum', meeting }}
      />
    );

    expect(
      screen.getByRole('button', { name: 'Confirm Quorum' })
    ).toBeEnabled();
    expect(screen.queryByText('Server-derived quorum')).not.toBeInTheDocument();
    expect(
      screen.queryByText(/the server derives quorum/i)
    ).not.toBeInTheDocument();
  });
});
