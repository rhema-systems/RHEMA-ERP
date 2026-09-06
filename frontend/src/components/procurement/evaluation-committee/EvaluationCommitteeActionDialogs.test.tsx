import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ decideRecall: vi.fn(), requestRecall: vi.fn() }));
vi.mock('@/services/procurement-evaluation-committee.service', () => ({
  procurementEvaluationCommitteeService: api,
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { EvaluationCommitteeActionDialogs } from './EvaluationCommitteeActionDialogs';
import type {
  ProcurementEvaluationAppointment,
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
  ProcurementEvaluationMeeting,
  ProcurementEvaluationScoreRecall,
  ProcurementEvaluationScoreSheet,
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

const recall = {
  id: 'recall-1',
  rowVersion: 'AQID',
  status: 'PendingApproval',
  reason: 'Supplier evidence changed after the original score was submitted.',
} as ProcurementEvaluationScoreRecall;

const fillRecallDecision = () => {
  fireEvent.change(screen.getAllByRole('textbox')[0], {
    target: { value: 'WORKFLOW-DECISION-1' },
  });
  fireEvent.change(
    screen.getByPlaceholderText(
      'Workflow evidence, signed file, minute, or controlled reference'
    ),
    { target: { value: 'RECALL-EVIDENCE-1' } }
  );
};

beforeEach(() => {
  vi.clearAllMocks();
  api.decideRecall.mockReset();
  api.requestRecall.mockReset();
});

describe('persistent recall failure feedback', () => {
  it.each([true, false])(
    'retains decision inputs and actionable ProblemDetails after approve=%s fails',
    async (approve) => {
      api.decideRecall.mockRejectedValue({
        response: {
          data: {
            detail: 'The recall workflow must be completed by an independent reviewer.',
            extensions: { code: 'RECALL_INDEPENDENT_REVIEW_REQUIRED' },
          },
        },
      });
      render(
        <EvaluationCommitteeActionDialogs
          {...commonProps}
          action={{ type: 'recall-decision', recall, approve }}
        />
      );
      fillRecallDecision();
      const submit = screen.getByRole('button', {
        name: approve ? 'Approve recall' : 'Reject recall',
      });
      fireEvent.click(submit);

      expect(
        await screen.findByText(
          'The recall workflow must be completed by an independent reviewer. (RECALL_INDEPENDENT_REVIEW_REQUIRED)'
        )
      ).toBeVisible();
      expect(screen.getByText('Recall action could not be confirmed').closest('[role="alert"]')).toBeVisible();
      expect(screen.getByDisplayValue('WORKFLOW-DECISION-1')).toBeVisible();
      expect(screen.getByDisplayValue('RECALL-EVIDENCE-1')).toBeVisible();
      expect(screen.getByRole('dialog')).toBeVisible();
      expect(submit).toBeEnabled();
      expect(commonProps.onClose).not.toHaveBeenCalled();
      expect(commonProps.onCompleted).not.toHaveBeenCalled();
    }
  );

  it('allows an explicit retry with the retained decision and clears the previous error', async () => {
    api.decideRecall
      .mockRejectedValueOnce({ detail: 'The recall decision could not be saved.', code: 'RECALL_SAVE_FAILED' })
      .mockResolvedValueOnce(undefined);
    render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'recall-decision', recall, approve: true }}
      />
    );
    fillRecallDecision();
    const submit = screen.getByRole('button', { name: 'Approve recall' });
    fireEvent.click(submit);
    expect(await screen.findByText('The recall decision could not be saved. (RECALL_SAVE_FAILED)')).toBeVisible();
    fireEvent.click(submit);

    await waitFor(() => expect(commonProps.onCompleted).toHaveBeenCalledOnce());
    expect(commonProps.onClose).toHaveBeenCalledOnce();
    expect(api.decideRecall).toHaveBeenCalledTimes(2);
    expect(api.decideRecall).toHaveBeenLastCalledWith('recall-1', expect.objectContaining({
      approve: true,
      rowVersion: 'AQID',
      decisionReference: 'WORKFLOW-DECISION-1',
      evidenceReference: 'RECALL-EVIDENCE-1',
    }));
    expect(screen.queryByText('Recall action could not be confirmed')).not.toBeInTheDocument();
  });

  it('shows a persistent fallback without server detail and clears it for a fresh dialog', async () => {
    api.decideRecall.mockRejectedValue({ response: { status: 500 } });
    const { rerender } = render(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'recall-decision', recall, approve: true }}
      />
    );
    fillRecallDecision();
    fireEvent.click(screen.getByRole('button', { name: 'Approve recall' }));
    expect(await screen.findByText('The controlled committee action failed.')).toBeVisible();

    rerender(<EvaluationCommitteeActionDialogs {...commonProps} action={undefined} />);
    rerender(
      <EvaluationCommitteeActionDialogs
        {...commonProps}
        action={{ type: 'recall-decision', recall, approve: true }}
      />
    );
    expect(screen.queryByText('Recall action could not be confirmed')).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue('WORKFLOW-DECISION-1')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Approve recall' })).toBeDisabled();
  });
});

describe('evaluation committee action evidence fields', () => {
  it('requires an explicit recall workflow selection instead of choosing the first unrelated workflow', async () => {
    render(<EvaluationCommitteeActionDialogs {...commonProps}
      action={{ type: 'recall', scoreSheet: { id: 'sheet-1', phase: 'Combined', attempt: 1, scoreSubjectType: 'TenderEvaluation' } as ProcurementEvaluationScoreSheet }}
      options={{ committees: [], users: [], workflows: [{ id: 'unrelated-1', name: 'Accounts Payable Invoice Approval', version: 1 }] }}
    />);
    expect(screen.getByRole('combobox')).toHaveTextContent('Select workflow');
    expect(screen.getByRole('button', { name: 'Submit recall request' })).toBeDisabled();
    expect(screen.queryByText('Accounts Payable Invoice Approval · v1')).not.toBeInTheDocument();
  });

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
