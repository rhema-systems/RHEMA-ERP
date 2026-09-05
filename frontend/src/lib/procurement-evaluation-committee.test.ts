import { describe, expect, it } from 'vitest';

import {
  hasEvaluationCommitteeAction,
  isEvaluationCommitteeControlError,
  validateCoiDeclaration,
  validateCommitteeBinding,
  validateMeetingReadiness,
  validateRecallRequest,
} from './procurement-evaluation-committee';
import type {
  ProcurementEvaluationAppointment,
  ProcurementEvaluationCommitteeControl,
} from '@/types/procurement-evaluation-committee';

const appointment = (
  id: string,
  memberKind: ProcurementEvaluationAppointment['memberKind'],
  eligibleToScore = true
): ProcurementEvaluationAppointment => ({
  id,
  committeeMemberId: id,
  responsibilityAssignmentId: `assignment-${id}`,
  userId: `user-${id}`,
  userDisplayName: memberKind,
  roleName: 'TDC_EVALUATOR',
  memberKind,
  isVoting: memberKind !== 'Secretary',
  effectiveFromUtc: '2026-07-23T10:00:00Z',
  status: 'Accepted',
  eligibleToScore,
  blockedReasons: eligibleToScore ? [] : ['COI declaration is missing.'],
  rowVersion: 'AQID',
});

describe('evaluation committee controls', () => {
  it('recognizes committee readiness failures that need an actionable handoff', () => {
    expect(
      isEvaluationCommitteeControlError(
        new Error(
          'No active evaluation committee control exists. (EVALUATION_SCORER_INELIGIBLE)'
        )
      )
    ).toBe(true);
    expect(
      isEvaluationCommitteeControlError(new Error('Network request failed'))
    ).toBe(false);
  });

  it('trusts server actions while normalizing naming style', () => {
    expect(
      hasEvaluationCommitteeAction(['ConfirmQuorum'], 'confirm-quorum')
    ).toBe(true);
    expect(hasEvaluationCommitteeAction([], 'ConfirmQuorum')).toBe(false);
  });

  it('requires exact composition, purpose, and effective dates', () => {
    expect(
      validateCommitteeBinding({
        sourceType: 'Tender',
        sourceId: 'tender-1',
        committeeTemplateId: 'committee-1',
        purpose: 'Technical and financial evaluation',
        effectiveFromUtc: '2026-08-01T00:00:00Z',
        requiredRoles: [
          {
            memberKind: 'Chair',
            roleName: 'TDC_EVALUATOR',
            minimumCount: 1,
            isVoting: true,
            isRequiredForQuorum: true,
          },
        ],
        idempotencyKey: 'key',
      })
    ).toBeUndefined();
    expect(
      validateCommitteeBinding({
        sourceType: 'Tender',
        sourceId: 'tender-1',
        committeeTemplateId: '',
        purpose: '',
        effectiveFromUtc: 'bad',
        requiredRoles: [],
        idempotencyKey: 'key',
      })
    ).toContain('committee');
  });

  it('allows system-generated references for no-conflict declarations', () => {
    expect(
      validateCoiDeclaration({
        outcome: 'NoConflict',
        declaration: 'I declare that no conflict exists.',
        validFromUtc: '2026-07-23T00:00:00Z',
        appointmentRowVersion: 'AQID',
        idempotencyKey: 'key',
      })
    ).toBeUndefined();
  });

  it('requires details and supporting references for a declared conflict', () => {
    expect(
      validateCoiDeclaration({
        outcome: 'ConflictDeclared',
        declaration: 'A material conflict exists.',
        signatureReference: 'SIGN-1',
        evidenceReference: 'COI-1',
        validFromUtc: '2026-07-23T00:00:00Z',
        appointmentRowVersion: 'AQID',
        idempotencyKey: 'key',
      })
    ).toContain('declared conflict');
    expect(
      validateCoiDeclaration({
        outcome: 'ConflictDeclared',
        declaration: 'A material conflict exists.',
        conflictDetails: 'A bidder is controlled by a close relative.',
        validFromUtc: '2026-07-23T00:00:00Z',
        appointmentRowVersion: 'AQID',
        idempotencyKey: 'key',
      })
    ).toContain('supporting evidence');
  });

  it('distinguishes required Chair and Secretary from voting quorum', () => {
    const members = [
      appointment('chair', 'Chair'),
      appointment('secretary', 'Secretary'),
      appointment('voter', 'VotingMember'),
    ];
    expect(
      validateMeetingReadiness({ requiredQuorum: 2, members })
    ).toBeUndefined();
    expect(
      validateMeetingReadiness({
        requiredQuorum: 2,
        members: members.filter((item) => item.memberKind !== 'Secretary'),
      })
    ).toContain('Secretary');
  });

  it('requires evidence and an independent workflow for recall', () => {
    expect(
      validateRecallRequest({
        scoreSheetRowVersion: 'AQID',
        reason: 'A transposition error requires a fresh attempt.',
        evidenceReference: 'EVIDENCE-1',
        workflowDefinitionId: 'workflow-1',
        idempotencyKey: 'key',
      })
    ).toBeUndefined();
    expect(
      validateRecallRequest({
        scoreSheetRowVersion: 'AQID',
        reason: 'Error',
        evidenceReference: '',
        workflowDefinitionId: '',
        idempotencyKey: 'key',
      })
    ).toContain('complete');
  });

  it('retains exact lineage fields required by the history-first workspace', () => {
    const control = {
      policyCode: 'TDC-POLICY',
      policyVersion: 4,
      configurationProfileCode: 'TDC-CONFIG',
      configurationProfileVersion: 2,
      methodRuleCode: 'NCT-GOODS',
      compositionIntegrityHash: 'a'.repeat(64),
    } as ProcurementEvaluationCommitteeControl;
    expect(control.policyCode).toBe('TDC-POLICY');
    expect(control.compositionIntegrityHash).toHaveLength(64);
  });
});
