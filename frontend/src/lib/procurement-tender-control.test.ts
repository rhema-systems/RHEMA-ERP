import { describe, expect, it, vi } from 'vitest';
import {
  buildFinancialScores,
  buildTechnicalScores,
  getTenderControlReadiness,
  tenderControlStatusLabel,
} from './procurement-tender-control';
import {
  ProcurementTenderControlStatus as Status,
  ProcurementTenderSubmissionDisposition as Disposition,
  type ProcurementTenderControl,
} from '@/types/procurement-tender-control';

const control = (status: Status): ProcurementTenderControl => ({
  tenderId: 't1', tenderNumber: 'NCT-1', tenderTitle: 'Goods', method: 1,
  methodRuleCode: 'NCT-GOODS', authorityRouteReference: 'ARR-1',
  ppaApprovalRequired: false, status, advertisementReference: 'ADV-1',
  publicationChannel: 'Daily Graphic', tenderDocumentReference: 'STD-1',
  tenderDocumentVersion: '1', documentFee: 0, advertisementEvidenceReference: 'ev-1',
  advertisedAtUtc: '2026-07-01T00:00:00Z', submissionDeadlineUtc: '2026-07-20T00:00:00Z',
  openingScheduledAtUtc: '2026-07-20T01:00:00Z', integrityHash: 'a'.repeat(64),
  minimumTechnicalScore: 80, technicalWeight: 60, financialWeight: 40,
  rowVersion: 'AAAAAA==', documentIssues: [], milestones: [],
  technicalResults: [{ bidId: 'b1', score: 90, qualified: true }],
  submissionReceipts: [
    { id: 'r1', tenderBidId: 'b1', businessPartnerId: 'p1', businessPartnerName: 'One',
      receiptNumber: 'R1', receivedAtUtc: '2026-07-19T00:00:00Z',
      disposition: Disposition.OnTimeAccepted, bidAmount: 100, currency: 'GHS', integrityHash: 'b'.repeat(64) },
    { id: 'r2', tenderBidId: 'b2', businessPartnerId: 'p2', businessPartnerName: 'Two',
      receiptNumber: 'R2', receivedAtUtc: '2026-07-21T00:00:00Z',
      disposition: Disposition.LateRejected, bidAmount: 200, currency: 'GHS', integrityHash: 'c'.repeat(64) },
  ],
});

describe('NCT/ICT tender control helpers', () => {
  it('keeps advertised submissions sealed and separates late receipts', () => {
    vi.setSystemTime(new Date('2026-07-21T00:00:00Z'));
    expect(getTenderControlReadiness(control(Status.Advertised))).toMatchObject({
      onTime: 1, late: 1, sealed: true, canOpen: true,
    });
    vi.useRealTimers();
  });
  it('enables only the next technical step after opening', () => {
    expect(getTenderControlReadiness(control(Status.Opened))).toMatchObject({
      canTechnical: true, canFinancial: false,
    });
  });
  it('builds evaluation rows only from on-time submissions', () => {
    expect(buildTechnicalScores(control(Status.Opened)).map((item) => item.bidId)).toEqual(['b1']);
    expect(buildFinancialScores(control(Status.TechnicalEvaluated)).map((item) => item.bidId)).toEqual(['b1']);
  });
  it('limits QBS financial review to the highest qualified technical bid', () => {
    const qbs = control(Status.TechnicalEvaluated);
    qbs.method = 7;
    qbs.submissionReceipts.push({
      id: 'r3', tenderBidId: 'b3', businessPartnerId: 'p3', businessPartnerName: 'Three',
      receiptNumber: 'R3', receivedAtUtc: '2026-07-19T01:00:00Z',
      disposition: Disposition.OnTimeAccepted, bidAmount: 90, currency: 'GHS', integrityHash: 'd'.repeat(64),
    });
    qbs.technicalResults.push({ bidId: 'b3', score: 85, qualified: true });
    expect(buildFinancialScores(qbs).map((item) => item.bidId)).toEqual(['b1']);
  });
  it('exposes approval-to-acceptance readiness in sequence', () => {
    expect(getTenderControlReadiness(control(Status.Approved)).canAward).toBe(true);
    expect(getTenderControlReadiness(control(Status.Awarded)).canContract).toBe(true);
    expect(getTenderControlReadiness(control(Status.Contracted)).canAccept).toBe(true);
  });
  it('provides an explicit status label', () => {
    expect(tenderControlStatusLabel[Status.PendingApproval]).toBe('Authority approval pending');
  });
});
