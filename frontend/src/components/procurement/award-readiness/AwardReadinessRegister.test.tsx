import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
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
  message: 'The current actor is distinct from the retained evaluator lineage.',
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

  it('keeps the default view focused and retains all audit records in expandable details', () => {
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
    expect(screen.getByTestId('award-readiness-outcome')).toBeVisible();
    expect(
      screen.getByText('Server-derived prerequisite register')
    ).not.toBeVisible();
    expect(screen.getByText('Financial Evaluator')).not.toBeVisible();
    expect(screen.getByText('Qualified Supplier Ltd')).not.toBeVisible();
    expect(screen.getByText('BOARD-PPA')).not.toBeVisible();
    expect(screen.getByText('Award Readiness Evaluated')).not.toBeVisible();
    screen
      .getAllByRole('table')
      .forEach((table) => expect(table).not.toBeVisible());
    expect(
      screen.getByText('Authoritative evaluator lineage')
    ).not.toBeVisible();
    fireEvent.click(screen.getByText('Evaluator and approval audit details'));
    expect(screen.getByText('Authoritative evaluator lineage')).toBeVisible();
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Review verification and due diligence',
      })
    );
    expect(
      screen.getByRole('button', {
        name: 'Review verification and due diligence',
      })
    ).toHaveAttribute('aria-expanded', 'true');
    expect(
      screen.getByText('Server-derived prerequisite register')
    ).toBeVisible();
    expect(screen.getAllByText('Award verification').length).toBeGreaterThan(0);
    expect(
      screen.getByRole('link', { name: 'Open existing control' })
    ).toHaveAttribute('href', '/procurement/tenders/tender-1?tab=verification');
    fireEvent.click(
      screen.getByText('Recommendation, evaluation and supplier records')
    );
    expect(screen.getByText('Financial Evaluator')).toBeVisible();
    expect(screen.getAllByText('Locked').length).toBeGreaterThan(0);
    expect(screen.getByText('Qualified Supplier Ltd')).toBeVisible();
    fireEvent.click(screen.getByText('Authority and evidence records'));
    expect(screen.getByText('BOARD-PPA')).toBeVisible();
    fireEvent.click(screen.getByText('Decision history and integrity'));
    expect(screen.getByText('Award Readiness Evaluated')).toBeVisible();
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
    expect(screen.getByText(/not allowed by the server/i)).toBeInTheDocument();
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
    ).toBeVisible();
    expect(screen.getByText('Effective hard stop')).not.toBeVisible();
    expect(
      screen.getByText('Authoritative evaluator lineage')
    ).not.toBeVisible();
    fireEvent.click(screen.getByText('Evaluator and approval audit details'));
    expect(screen.getByText('Effective hard stop')).toBeVisible();
    expect(screen.getByText('Authoritative evaluator lineage')).toBeVisible();
    expect(screen.getByText('Financial Evaluator')).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Evaluate award readiness' })
    ).toBeDisabled();
    expect(onEvaluate).not.toHaveBeenCalled();
  });

  it('shows a readable blocker without raw identifiers and retains the exact server reason in details', () => {
    const code = 'SUPPLIER_ef23c5222ff74f71a71848a9833725ec_ELIGIBLE';
    const message =
      'A current supplier-risk and concentration assessment is required.';
    const raw = `${code}: ${message}`;
    render(
      <AwardReadinessRegister
        sourceType="Tender"
        sourceId="tender-1"
        decision={decision({
          blockedReasons: [raw],
          prerequisiteGroups: [
            {
              group: 'SupplierEligibility',
              status: 'Failed',
              items: [
                { code, label: 'Supplier eligible', status: 'Failed', message },
              ],
            },
          ],
        })}
        history={[]}
        sodStatus={sodStatus()}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={vi.fn()}
      />
    );
    expect(screen.getByText(raw)).not.toBeVisible();
    expect(
      screen
        .getAllByText(message)
        .some((element) => element.closest('details') === null)
    ).toBe(true);
    fireEvent.click(
      screen.getByRole('button', { name: 'Review supplier eligibility' })
    );
    expect(screen.getByText(raw)).toBeVisible();
  });

  it('shows supplier warnings and keeps all additional blockers accessible', () => {
    const reasons = [
      'First blocker.',
      'Second blocker.',
      'Third blocker.',
      'Fourth blocker.',
    ];
    render(
      <AwardReadinessRegister
        sourceType="Tender"
        sourceId="tender-1"
        decision={decision({
          blockedReasons: reasons,
          suppliers: [
            {
              ...decision().suppliers[0],
              warnings: ['Risk review expires soon.'],
            },
          ],
        })}
        history={[]}
        sodStatus={sodStatus()}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={vi.fn()}
      />
    );
    expect(screen.getByText('Supplier warnings (1)')).toBeVisible();
    expect(
      screen.getByText('Qualified Supplier Ltd: Risk review expires soon.')
    ).toBeVisible();
    expect(
      screen
        .getAllByText('Fourth blocker.')
        .every((element) => element.closest('details'))
    ).toBe(true);
    fireEvent.click(screen.getByText('More blockers (1)'));
    const more = screen.getByText('More blockers (1)').closest('details')!;
    expect(within(more).getByText('Fourth blocker.')).toBeVisible();
  });

  it('keeps a historical Ready decision visibly stale and refresh accessible in history details', () => {
    const onRefresh = vi.fn();
    render(
      <AwardReadinessRegister
        sourceType="Tender"
        sourceId="tender-1"
        decision={decision({
          status: 'Ready',
          isReady: true,
          isCurrent: false,
          blockedReasons: [],
        })}
        history={[]}
        sodStatus={sodStatus()}
        isSodStatusLoading={false}
        canEvaluate
        isEvaluating={false}
        onEvaluate={vi.fn()}
        onRefresh={onRefresh}
      />
    );
    expect(screen.getByText('Readiness must be rechecked')).toBeVisible();
    expect(
      screen.queryByText('Ready for award review')
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Refresh history' })
    ).not.toBeVisible();
    fireEvent.click(screen.getByText('Decision history and integrity'));
    fireEvent.click(screen.getByRole('button', { name: 'Refresh history' }));
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it.each([false, true])(
    'retains visible recovery from a failed SOD preflight with a retained decision: %s',
    (retained) => {
      const onRefresh = vi.fn();
      const onEvaluate = vi.fn();
      render(
        <AwardReadinessRegister
          sourceType="Tender"
          sourceId="tender-1"
          decision={
            retained
              ? decision({
                  status: 'Ready',
                  isReady: true,
                  blockedReasons: [],
                  prerequisiteGroups: [],
                })
              : undefined
          }
          history={[]}
          isSodStatusLoading={false}
          sodStatusError="SOD check timed out."
          canEvaluate
          isEvaluating={false}
          onEvaluate={onEvaluate}
          onRefresh={onRefresh}
        />
      );
      expect(screen.getByText('SOD preflight is unavailable')).toBeVisible();
      expect(
        screen.getByRole('button', {
          name: retained
            ? 'Re-evaluate current readiness'
            : 'Evaluate award readiness',
        })
      ).toBeDisabled();
      fireEvent.click(screen.getByRole('button', { name: 'Refresh checks' }));
      expect(onRefresh).toHaveBeenCalledTimes(1);
      expect(onEvaluate).not.toHaveBeenCalled();
    }
  );
});
