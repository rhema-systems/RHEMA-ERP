import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AwardReadinessRegister } from './AwardReadinessRegister';
import type {
  ProcurementAwardReadinessDecision,
  ProcurementEvaluatorAwardApproverSodStatus,
} from '@/types/procurement-award-readiness';

const decision = (
  overrides: Partial<ProcurementAwardReadinessDecision> = {}
): ProcurementAwardReadinessDecision => ({
  id: 'decision-1',
  decisionSequence: 3,
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  method: 'NationalCompetitiveTendering',
  status: 'Blocked',
  isReady: false,
  isCurrent: true,
  sourceIntegrityHash: 'a'.repeat(64),
  integrityHash: 'b'.repeat(64),
  idempotencyKey: 'tdc0209-evaluate-1',
  correlationId: 'correlation-1',
  evaluatedAtUtc: '2026-07-24T10:00:00Z',
  evaluatedByUserId: 'approver-1',
  evaluatedByName: 'Procurement Approver',
  recommendation: {
    subjectType: 'TenderBid',
    subjectIds: ['bid-1'],
    businessPartnerIds: ['supplier-1'],
    reason: 'Highest evaluated responsive bid.',
    evidenceReference: 'RECOMMENDATION-1',
    recommendedAtUtc: '2026-07-24T09:00:00Z',
    recommendedByUserId: 'evaluator-1',
  },
  evaluations: [
    {
      evaluationType: 'TenderFinancialEvaluation',
      evaluationId: 'evaluation-1',
      phase: 'Financial',
      status: 'Completed',
      completedAtUtc: '2026-07-24T08:00:00Z',
      evidenceReference: 'EVALUATION-1',
      integrityHash: 'c'.repeat(64),
      scoreAttempts: [
        {
          scoreSheetId: 'score-1',
          committeeControlId: 'committee-1',
          meetingId: 'meeting-1',
          appointmentId: 'appointment-1',
          phase: 'Financial',
          scoreSubjectType: 'TenderBid',
          scoreSubjectId: 'bid-1',
          attempt: 1,
          status: 'Locked',
          submittedAtUtc: '2026-07-24T07:30:00Z',
          submittedByUserId: 'evaluator-1',
          submittedByName: 'Financial Evaluator',
          evidenceReference: 'SCORE-1',
          integrityHash: 'd'.repeat(64),
          recallIds: [],
        },
      ],
    },
  ],
  suppliers: [
    {
      businessPartnerId: 'supplier-1',
      partnerCode: 'SUP-001',
      partnerName: 'Qualified Supplier Ltd',
      isEligible: true,
      validationCode: 'Eligible',
      errors: [],
      warnings: [],
      prequalification: [
        {
          entryId: 'entry-1',
          exerciseId: 'exercise-1',
          applicationId: 'application-1',
          categoryId: 'category-1',
          status: 'Active',
          validFromUtc: '2026-01-01T00:00:00Z',
          expiresAtUtc: '2027-01-01T00:00:00Z',
          approvalReference: 'APPROVAL-1',
          approvalEvidenceReference: 'PREQUAL-1',
          integrityHash: 'e'.repeat(64),
        },
      ],
    },
  ],
  verifications: [],
  authority: {
    methodRuleId: 'method-rule-1',
    methodRuleCode: 'NCT-GOODS',
    authorityRouteId: 'authority-1',
    authorityRouteReference: 'BOARD-PPA',
    workflowDefinitionId: 'workflow-1',
    workflowInstanceId: 'workflow-instance-1',
    workflowStatus: 'Completed',
    approvalReference: 'BOARD-001',
    approvedAtUtc: '2026-07-24T09:30:00Z',
    approvedByUserId: 'board-1',
    approvalActorUserIds: ['board-1', 'ppa-1'],
  },
  evidence: [
    {
      requirementKey: 'AwardVerification',
      label: 'Award verification',
      isAvailable: false,
    },
  ],
  prerequisiteGroups: [
    {
      group: 'Evaluation',
      status: 'Passed',
      items: [
        {
          code: 'EVALUATION_COMPLETE',
          label: 'Evaluation complete',
          status: 'Passed',
          message: 'The current financial evaluation is complete.',
        },
      ],
    },
    {
      group: 'VerificationAndDueDiligence',
      status: 'Failed',
      items: [
        {
          code: 'VERIFICATION_REQUIRED',
          label: 'Award verification',
          status: 'Failed',
          message: 'Verification is incomplete.',
          remediation: 'Complete the existing bidder verification checklist.',
        },
      ],
    },
  ],
  timeline: [
    {
      eventType: 'AWARD_READINESS_EVALUATED',
      occurredAtUtc: '2026-07-24T10:00:00Z',
      actorUserId: 'approver-1',
      reference: 'TDR-001',
      integrityHash: 'b'.repeat(64),
    },
  ],
  blockedReasons: ['Award verification is incomplete.'],
  allowedActions: ['EvaluateReadiness'],
  ...overrides,
});

const sodStatus = (
  overrides: Partial<ProcurementEvaluatorAwardApproverSodStatus> = {}
): ProcurementEvaluatorAwardApproverSodStatus => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  allowed: true,
  code: 'SOD_ALLOWED',
  message:
    'The current actor is distinct from the retained evaluator lineage.',
  currentActorUserId: 'approver-1',
  currentActorName: 'Procurement Approver',
  currentActorRoles: ['AwardApprover'],
  evaluatorUserIds: ['evaluator-1'],
  independentApprovalActorUserIds: ['approver-1'],
  evaluatorLineage: [
    {
      family: 'CommitteeScoreSheet',
      evaluationId: 'evaluation-1',
      committeeControlId: 'committee-1',
      appointmentId: 'appointment-1',
      scoreSheetId: 'score-1',
      scoreSubjectType: 'TenderBid',
      scoreSubjectId: 'bid-1',
      phase: 'Financial',
      attempt: 1,
      scoreStatus: 'Locked',
      isRetainedAttempt: true,
      isRecalledAttempt: false,
      evaluatorUserId: 'evaluator-1',
      evaluatedAtUtc: '2026-07-24T08:00:00Z',
      integrityHash: 'f'.repeat(64),
    },
  ],
  readinessDecisionId: 'decision-1',
  readinessDecisionSequence: 3,
  readinessSourceIntegrityHash: 'a'.repeat(64),
  readinessIntegrityHash: 'b'.repeat(64),
  readinessDecisionIsCurrent: true,
  sodDecisionId: 'sod-decision-1',
  sodControlCode: 'SOD-EVALUATOR-AWARD-APPROVER',
  sodPolicySetId: 'sod-policy-1',
  sodPolicyCode: 'PROC-SOD',
  sodPolicyVersion: 2,
  sodRuleId: 'sod-rule-1',
  sodRuleCode: 'EVALUATOR-AWARD-APPROVER',
  sodSourceDecisionKey: 'Tender:tender-1',
  sourceMethodRuleId: 'method-rule-1',
  sourceMethodRuleCode: 'NCT-GOODS',
  correlationId: 'correlation-sod-1',
  evaluatedAtUtc: '2026-07-24T10:05:00Z',
  ...overrides,
});

describe('award-readiness history-first register', () => {
  it('shows a controlled empty state and never exposes a readiness toggle', () => {
    const onEvaluate = vi.fn();
    render(
      <AwardReadinessRegister
        sourceType="RequestForQuotation"
        sourceId="rfq-1"
        history={[]}
        sodStatus={sodStatus({
          sourceType: 'RequestForQuotation',
          sourceId: 'rfq-1',
          sourceReference: 'RFQ-001',
          readinessDecisionId: undefined,
          readinessDecisionSequence: undefined,
          readinessSourceIntegrityHash: undefined,
          readinessIntegrityHash: undefined,
          readinessDecisionIsCurrent: undefined,
        })}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={onEvaluate}
      />
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Evaluate award readiness' })
    );
    expect(onEvaluate).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.queryByText(/mark ready/i)).not.toBeInTheDocument();
  });

  it('renders prerequisite remediation, score locks, supplier lineage, authority, and immutable history', () => {
    render(
      <AwardReadinessRegister
        sourceType="Tender"
        sourceId="tender-1"
        decision={decision()}
        history={[decision()]}
        sodStatus={sodStatus()}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={vi.fn()}
      />
    );

    expect(screen.getByText('Award remains blocked')).toBeInTheDocument();
    expect(
      screen.getByText('Server-derived prerequisite register')
    ).toBeInTheDocument();
    expect(screen.getAllByText('Award verification').length).toBeGreaterThan(0);
    expect(
      screen.getByRole('link', { name: 'Open existing control' })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1?tab=verification'
    );
    expect(screen.getByText('Financial Evaluator')).toBeInTheDocument();
    expect(screen.getAllByText('Locked').length).toBeGreaterThan(0);
    expect(screen.getByText('Qualified Supplier Ltd')).toBeInTheDocument();
    expect(screen.getByText('BOARD-PPA')).toBeInTheDocument();
    expect(screen.getByText('Award Readiness Evaluated')).toBeInTheDocument();
  });

  it('requires both client permission and the latest server action gate', () => {
    render(
      <AwardReadinessRegister
        sourceType="Tender"
        sourceId="tender-1"
        decision={decision({ allowedActions: ['ViewHistory'] })}
        history={[]}
        sodStatus={sodStatus()}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Re-evaluate current readiness' })
    ).not.toBeInTheDocument();
    expect(
      screen.getByText(/not allowed by the server/i)
    ).toBeInTheDocument();
  });

  it('shows the authoritative SOD lineage and disables evaluation for a blocked actor', () => {
    const onEvaluate = vi.fn();
    render(
      <AwardReadinessRegister
        sourceType="ExceptionalSourcing"
        sourceId="tender-2"
        history={[]}
        sodStatus={sodStatus({
          sourceType: 'ExceptionalSourcing',
          sourceId: 'tender-2',
          sourceReference: 'EXC-002',
          allowed: false,
          code: 'SOD_CONFLICT',
          message:
            'The current actor is an evaluator and no independent approval actor is retained.',
          currentActorUserId: 'evaluator-1',
          currentActorName: 'Financial Evaluator',
          currentActorRoles: ['TenderEvaluator', 'AwardApprover'],
          independentApprovalActorUserIds: [],
          readinessDecisionId: undefined,
          readinessDecisionSequence: undefined,
          readinessSourceIntegrityHash: undefined,
          readinessIntegrityHash: undefined,
          readinessDecisionIsCurrent: undefined,
        })}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={onEvaluate}
      />
    );

    expect(
      screen.getByTestId('award-readiness-sod-status')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Current actor is blocked from evaluating readiness')
    ).toBeInTheDocument();
    expect(screen.getByText('Effective hard stop')).toBeInTheDocument();
    expect(screen.getByText('Authoritative evaluator lineage')).toBeInTheDocument();
    expect(screen.getByText('Financial Evaluator')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Evaluate award readiness' })
    ).toBeDisabled();
    expect(onEvaluate).not.toHaveBeenCalled();
  });
});
