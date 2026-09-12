import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import RfqControls from '@/app/procurement/rfqs/[id]/controls/page';
import TenderControls from '@/app/procurement/tenders/[id]/controls/page';

const mocks = vi.hoisted(() => ({
  policy: { visibility: { known: true, direct: true, showApprovalControls: false }, error: undefined as string | undefined },
  rfq: {} as any,
  tender: {} as any,
  submitRfq: vi.fn(),
  submitTender: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'record-1' }) }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: any) => <a {...props}>{children}</a> }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: 'actor', name: 'Actor' }, hasPermission: () => true }) }));
vi.mock('@/hooks/useWorkflowSummary', () => ({ useWorkflowSummary: () => mocks.policy }));
vi.mock('@/services/procurement-award-readiness.service', () => ({ procurementAwardReadinessService: { latest: async () => null } }));
vi.mock('@/services/rfqService', () => ({ rfqService: { getControls: async () => mocks.rfq, submitEvaluation: (...args: any[]) => mocks.submitRfq(...args) } }));
vi.mock('@/services/procurement-tender-control.service', () => ({ procurementTenderControlService: { get: async () => mocks.tender, submitApproval: (...args: any[]) => mocks.submitTender(...args) } }));

beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  mocks.policy = { visibility: { known: true, direct: true, showApprovalControls: false }, error: undefined };
  mocks.rfq = {
    rfqId: 'record-1', rfqNumber: 'RFQ-001', rfqStatus: 'Sent', methodRuleId: 'rule', methodRuleCode: 'RFQ-GOODS',
    minimumQuotationCount: 2, qualifiedInvitationCount: 2, onTimeReceiptCount: 2, lateReceiptCount: 0,
    submissionDeadlinePassed: true, quotesRemainSealed: false, minimumCompetitionMet: true,
    receipts: [], evaluationOptions: [], openingRegister: { entries: [], participants: [], integrityHash: 'hash' },
    evaluation: { id: 'evaluation', status: 'Draft', approvalRequired: true, awardMode: 'WinnerTakesAll',
      recommendationReason: 'Best price', evidenceReference: 'DMS-EVALUATION', rowVersion: 'AQ==', lines: [] },
  };
  mocks.tender = {
    tenderId: 'record-1', tenderNumber: 'TND-001', tenderTitle: 'Goods', method: 'NationalCompetitiveTendering',
    methodRuleCode: 'NCT-GOODS', authorityRouteReference: 'ARR-1', ppaApprovalRequired: true,
    status: 'FinancialEvaluated', advertisementReference: 'ADV', publicationChannel: 'News',
    tenderDocumentReference: 'STD', tenderDocumentVersion: '1', documentFee: 0,
    advertisedAtUtc: '2026-07-01T00:00:00Z', submissionDeadlineUtc: '2026-07-20T00:00:00Z',
    openingScheduledAtUtc: '2026-07-20T01:00:00Z', integrityHash: 'hash',
    minimumTechnicalScore: 80, technicalWeight: 60, financialWeight: 40, rowVersion: 'AQ==',
    documentIssues: [], milestones: [], technicalResults: [], submissionReceipts: [],
  };
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('Controlled recommendation optional workflow actions', () => {
  it('offers RFQ completion instead of approval after a positive direct-policy response', async () => {
    render(<RfqControls />);
    const button = await screen.findByRole('button', { name: 'Complete evaluation' });
    expect((button as HTMLButtonElement).disabled).toBe(false);
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).toBeNull();
  });

  it('does not enable RFQ completion when workflow policy is unknown', async () => {
    mocks.policy.visibility = { known: false, direct: false, showApprovalControls: false };
    mocks.policy.error = 'Workflow status unavailable';
    render(<RfqControls />);
    const button = await screen.findByRole('button', { name: 'Submit for approval' });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole('alert').textContent).toContain('Workflow status unavailable');
  });

  it('keeps RFQ approval controls for a retained running workflow', async () => {
    mocks.policy.visibility = { known: true, direct: false, showApprovalControls: true };
    mocks.rfq.evaluation.status = 'Submitted';
    render(<RfqControls />);
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Complete evaluation' })).toBeNull();
  });

  it('requires the genuine statutory PPA reference before direct tender completion', async () => {
    render(<TenderControls />);
    const button = await screen.findByRole('button', { name: 'Complete recommendation' });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    const input = screen.getByLabelText('PPA/central reference (required)');
    fireEvent.change(input, { target: { value: 'PPA-TEST-001' } });
    await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
    fireEvent.click(button);
    await waitFor(() => expect(mocks.submitTender).toHaveBeenCalledWith('record-1', 'AQ==', 'PPA-TEST-001'));
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('keeps tender submission disabled while policy is unknown', async () => {
    mocks.policy.visibility = { known: false, direct: false, showApprovalControls: false };
    render(<TenderControls />);
    const button = await screen.findByRole('button', { name: 'Submit for approval' });
    expect((button as HTMLButtonElement).disabled).toBe(true);
  });
});
