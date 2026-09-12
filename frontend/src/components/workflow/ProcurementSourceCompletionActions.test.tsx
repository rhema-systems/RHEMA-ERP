import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ExceptionalPage from '@/app/procurement/tenders/[id]/exception-controls/page';
import PrequalificationPage from '@/app/procurement/prequalification/[id]/page';

const mocks = vi.hoisted(() => ({
  policy: { visibility: { known: true, direct: true, showApprovalControls: false }, error: undefined as string | undefined, refresh: vi.fn() },
  exceptional: {} as any,
  prequalification: {} as any,
  submitSourcing: vi.fn(),
  submitQualification: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'source-1' }) }));
vi.mock('next/link', () => ({ default: ({ children, ...props }: any) => <a {...props}>{children}</a> }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/hooks/useWorkflowSummary', () => ({ useWorkflowSummary: () => mocks.policy }));
vi.mock('@/components/procurement/awards/NegotiationInviteDialog', () => ({ default: () => null }));
vi.mock('@/services/procurement-award-readiness.service', () => ({ procurementAwardReadinessService: { latest: async () => null } }));
vi.mock('@/services/procurement-exceptional-sourcing-control.service', () => ({ procurementExceptionalSourcingControlService: {
  get: async () => mocks.exceptional,
  readiness: async () => ({ method: 4, supplierOptions: [], evidenceRequirements: [] }),
  submitApproval: (...args: any[]) => mocks.submitSourcing(...args),
} }));
vi.mock('@/services/procurement-prequalification.service', () => ({ procurementPrequalificationService: {
  get: async () => mocks.prequalification,
  readiness: async () => ({ categories: [], policies: [], workflows: [], suppliers: [], approvalRequired: false }),
  submitDecision: (...args: any[]) => mocks.submitQualification(...args),
} }));

beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  mocks.policy = { visibility: { known: true, direct: true, showApprovalControls: false }, error: undefined, refresh: vi.fn() };
  mocks.exceptional = {
    tenderId: 'source-1', tenderNumber: 'SS-001', tenderTitle: 'Sourcing', method: 4,
    status: 0, approvalRequired: true, boardApprovalRequired: true, managingDirectorApprovalRequired: false,
    ppaApprovalRequired: true, rowVersion: 'AQ==', integrityHash: 'hash', suppliers: [], evidenceChecklist: [],
    bids: [], milestones: [],
  };
  mocks.prequalification = {
    id: 'source-1', reference: 'PQ-001', title: 'Qualification', description: 'Qualification scope',
    status: 3, approvalRequired: true, rowVersion: 'AQ==', integrityHash: 'hash', categories: [], criteria: [],
    qualifiedEntries: [], milestones: [], applicationCount: 1, qualifiedCount: 0,
    opensAtUtc: '2026-07-01T00:00:00Z', closesAtUtc: '2026-07-10T00:00:00Z',
    applications: [{ id: 'app-1', applicationNumber: 'APP-001', supplierName: 'Supplier', status: 1,
      submittedAtUtc: '2026-07-02T00:00:00Z', passed: true, totalScore: 80, categoryIds: [], evidence: [], scores: [] }],
  };
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('Exceptional sourcing and qualification direct completion', () => {
  it('requires real Board and PPA references before completing exceptional sourcing', async () => {
    render(<ExceptionalPage />);
    const button = await screen.findByRole('button', { name: 'Complete sourcing' });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText('Board approval reference'), { target: { value: 'BOARD-1' } });
    fireEvent.change(screen.getByLabelText('PPA approval reference'), { target: { value: 'PPA-1' } });
    await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
    fireEvent.click(button);
    await waitFor(() => expect(mocks.submitSourcing).toHaveBeenCalledWith('source-1', 'AQ==', {
      boardApprovalReference: 'BOARD-1', managingDirectorApprovalReference: undefined, ppaApprovalReference: 'PPA-1',
    }));
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).toBeNull();
  });

  it('does not enable exceptional submission when server policy is unknown', async () => {
    mocks.policy.visibility = { known: false, direct: false, showApprovalControls: false };
    mocks.policy.error = 'Workflow unavailable';
    render(<ExceptionalPage />);
    expect((await screen.findByRole('button', { name: 'Submit for approval' }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole('alert').textContent).toContain('Workflow unavailable');
  });

  it('requires a signed business decision before creating the qualified list', async () => {
    render(<PrequalificationPage />);
    const button = await screen.findByRole('button', { name: 'Complete qualification' });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText('Decision reference'), { target: { value: 'DECISION-1' } });
    fireEvent.change(screen.getByLabelText('Signed decision evidence'), { target: { value: 'DMS-SIGNED' } });
    fireEvent.change(screen.getByLabelText('Decision reason'), { target: { value: 'Scorecards reviewed' } });
    await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
    fireEvent.click(button);
    await waitFor(() => expect(mocks.submitQualification).toHaveBeenCalledWith('source-1', 'AQ==', {
      decisionReference: 'DECISION-1', decisionEvidenceReference: 'DMS-SIGNED', reason: 'Scorecards reviewed',
    }));
    expect(screen.queryByRole('button', { name: 'Approve workflow step' })).toBeNull();
  });

  it('retains qualification approval controls for an in-flight workflow', async () => {
    mocks.policy.visibility = { known: true, direct: false, showApprovalControls: true };
    mocks.prequalification.status = 4;
    render(<PrequalificationPage />);
    expect(await screen.findByRole('button', { name: 'Approve workflow step' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Complete qualification' })).toBeNull();
  });
});
