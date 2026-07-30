import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementEvaluationCommitteeService as service } from './procurement-evaluation-committee.service';

describe('procurement evaluation-committee API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses one tenant-safe source query for readiness, options, and history', async () => {
    await service.readiness('Tender', 'tender-1');
    await service.options('Tender', 'tender-1');
    await service.get('Tender', 'tender-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/evaluation-committees/readiness',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/evaluation-committees/options',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      3,
      '/procurement/evaluation-committees',
      { sourceType: 'Tender', sourceId: 'tender-1' }
    );
  });

  it('uses authoritative actor-specific scorer eligibility', async () => {
    await service.scorerEligibility('RequestForQuotation', 'rfq-1', 'Combined');

    expect(api.get).toHaveBeenCalledWith(
      '/procurement/evaluation-committees/scorer-eligibility',
      {
        sourceType: 'RequestForQuotation',
        sourceId: 'rfq-1',
        phase: 'Combined',
      }
    );
  });

  it('routes committee, meeting, and attendance lifecycle actions by exact id', async () => {
    await service.activate('control-1', {
      rowVersion: 'AQID',
      evidenceReference: 'ACTIVATE-1',
      idempotencyKey: 'activate-key',
    });
    await service.createMeeting('control-1', {
      phase: 'Technical',
      meetingMode: 'Hybrid',
      meetingChannel: 'Boardroom / TEAMS-1',
      scheduledAtUtc: '2026-08-01T10:00:00Z',
      evidenceReference: 'AGENDA-1',
      remoteMeetingEvidenceReference: 'TEAMS-RECORDING-1',
      committeeRowVersion: 'AQID',
      idempotencyKey: 'meeting-key',
    });
    await service.signAttendance('meeting-1', {
      isPresent: true,
      signatureReference: 'SIGN-1',
      evidenceReference: 'ATTENDANCE-1',
      meetingRowVersion: 'AQID',
      appointmentRowVersion: 'BAQF',
      idempotencyKey: 'attendance-key',
    });

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/evaluation-committees/control-1/activate',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/evaluation-committees/control-1/meetings',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenNthCalledWith(
      3,
      '/procurement/evaluation-committees/meetings/meeting-1/attendance',
      expect.any(Object)
    );
  });

  it('uses only controlled recall routes for immutable score history', async () => {
    await service.requestRecall('sheet-1', {
      scoreSheetRowVersion: 'AQID',
      reason: 'Transposition error requires a fresh attempt.',
      evidenceReference: 'RECALL-1',
      workflowDefinitionId: 'workflow-1',
      idempotencyKey: 'recall-key',
    });
    await service.decideRecall('recall-1', {
      approve: true,
      rowVersion: 'AQID',
      decisionReference: 'WORKFLOW-1',
      evidenceReference: 'DECISION-1',
      idempotencyKey: 'decision-key',
    });

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/evaluation-committees/score-sheets/sheet-1/recalls',
      expect.any(Object)
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/evaluation-committees/score-recalls/recall-1/decision',
      expect.objectContaining({ approve: true })
    );
  });
});
