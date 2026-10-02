import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import BusinessPartnerDetailPage from './page';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import { businessPartnerFinanceProfileService } from '@/services/businessPartnerFinanceProfileService';

const workflowMocks = vi.hoisted(() => ({
  refresh: vi.fn(),
  useWorkflowSummary: vi.fn(),
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn(), back: vi.fn() }), useParams: () => ({ id: 'partner-role-test' }) }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: {
  getById: vi.fn(), getPostingOptions: vi.fn(), submitPartnerForApproval: vi.fn(),
  approvePartner: vi.fn(), rejectPartner: vi.fn(),
} }));
vi.mock('@/services/businessPartnerFinanceProfileService', () => ({ businessPartnerFinanceProfileService: { get: vi.fn() } }));
vi.mock('@/hooks/useWorkflowSummary', () => ({ useWorkflowSummary: workflowMocks.useWorkflowSummary }));
vi.mock('@/services/partnerConfigService', () => ({ licenseTypeService: { getActive: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/performanceTrackingService', () => ({ performanceTrackingService: {
  getMetricsByBusinessPartner: vi.fn().mockResolvedValue([]), getIncidentsByBusinessPartner: vi.fn().mockResolvedValue([]),
  getReviewsByBusinessPartner: vi.fn().mockResolvedValue([]),
} }));
vi.mock('@/services/purchasingService', () => ({ purchasingService: { getPurchaseOrdersBySupplier: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/components/procurement/BusinessPartnerAccessSetup', () => ({ BusinessPartnerAccessSetup: () => null }));
vi.mock('@/components/procurement/BusinessPartnerCurrentAccountsPanel', () => ({ BusinessPartnerCurrentAccountsPanel: ({ ledger }: { ledger: string }) => <div>Current {ledger} account sources</div> }));
vi.mock('@/components/procurement/PerformanceReviewDialog', () => ({ PerformanceReviewDialog: () => null }));
vi.mock('@/components/procurement/PerformanceReviewDetailDialog', () => ({ PerformanceReviewDetailDialog: () => null }));
vi.mock('@/components/procurement/PerformanceTrendsChart', () => ({ PerformanceTrendsChart: () => null }));
Object.assign(globalThis, { React, ResizeObserver: class { observe() {} unobserve() {} disconnect() {} } });

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({ accounts: [], bankAccounts: [], taxGroups: [] });
  vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({
    businessPartnerId: 'partner-role-test', partnerCode: 'ROLE-001', partnerName: 'Role test partner', roles: [],
  });
  workflowMocks.refresh.mockResolvedValue(undefined);
  workflowMocks.useWorkflowSummary.mockReturnValue({
    summary: {
      entityType: 'BusinessPartner', entityId: 'partner-role-test', approvalRequired: true,
      hasActiveInstance: false, hasWorkflowHistory: false, canCurrentUserApprove: false, pendingApprovers: [],
    },
    loading: false, error: undefined, refresh: workflowMocks.refresh,
    visibility: { known: true, active: false, direct: false, approvalRequired: true, showTab: true, tabLabel: 'Workflow', showApprovalControls: true },
  });
});

describe('business partner detail account roles', () => {
  it.each([
    ['Contractor', true, false], ['Supplier', true, false], ['Customer', false, true], ['Both', true, true],
  ] as const)('restores the applicable current account tabs for %s', async (partnerType, payables, receivables) => {
    vi.mocked(businessPartnerService.getById).mockResolvedValue({
      id: 'partner-role-test', partnerCode: 'ROLE-001', partnerName: 'Role test partner', partnerType,
      status: 'Active', approvalStatus: 'Approved', documents: [], licenses: [],
      isPreferred: false, isBlacklisted: false, createdAt: '2026-09-27T00:00:00Z',
    } satisfies BusinessPartnerDetailDto);
    render(<BusinessPartnerDetailPage />);
    await screen.findByRole('heading', { name: 'Role test partner' });
    expect(!!screen.queryByRole('tab', { name: 'Accounts Payable' })).toBe(payables);
    expect(!!screen.queryByRole('tab', { name: 'Accounts Receivable' })).toBe(receivables);
    const name = payables ? 'Accounts Payable' : 'Accounts Receivable';
    fireEvent.mouseDown(screen.getByRole('tab', { name }), { button: 0, ctrlKey: false });
    expect(await screen.findByText(`Current ${payables ? 'payables' : 'receivables'} account sources`)).toBeInTheDocument();
  });

  it('shows the current approved AR profile values instead of the legacy partner defaults', async () => {
    vi.mocked(businessPartnerService.getById).mockResolvedValue({
      id: 'partner-role-test', partnerCode: 'ROLE-001', partnerName: 'Role test partner', partnerType: 'Customer',
      status: 'Active', approvalStatus: 'Approved', documents: [], licenses: [], currency: 'GHS', creditLimit: 25,
      isPreferred: false, isBlacklisted: false, createdAt: '2026-09-27T00:00:00Z',
    } satisfies BusinessPartnerDetailDto);
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({
      businessPartnerId: 'partner-role-test', partnerCode: 'ROLE-001', partnerName: 'Role test partner', roles: [{
        id: 'customer-role', roleType: 'Customer', status: 'Active', activeFromUtc: '2026-01-01T00:00:00Z', apProfiles: [],
        arProfiles: [{
          id: 'ar-approved', businessPartnerRoleId: 'customer-role', versionNumber: 2, status: 'Approved',
          effectiveFrom: '2026-01-01T00:00:00Z', creditLimit: 12500, isWithholdingAgent: false,
        }],
      }],
    });

    render(<BusinessPartnerDetailPage />);
    await screen.findByRole('heading', { name: 'Role test partner' });
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Financial Info' }), { button: 0, ctrlKey: false });

    expect(await screen.findByText('GHS 12,500.00')).toBeInTheDocument();
    expect(screen.queryByText('GHS 25.00')).not.toBeInTheDocument();
  });
});

describe('business partner identity approval workflow', () => {
  const pendingPartner = {
    id: 'partner-role-test', partnerCode: 'SUP260001', partnerName: 'Akwaaba Technical Services Ltd', partnerType: 'Supplier',
    status: 'PendingApproval', approvalStatus: 'Pending', documents: [], licenses: [],
    isPreferred: false, isBlacklisted: false, createdAt: '2026-09-27T00:00:00Z',
  } satisfies BusinessPartnerDetailDto;

  it('lets the maker submit an identity that has no active workflow', async () => {
    vi.mocked(businessPartnerService.getById).mockResolvedValue(pendingPartner);
    vi.mocked(businessPartnerService.submitPartnerForApproval).mockResolvedValue(undefined);

    render(<BusinessPartnerDetailPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Submit for approval' }));

    await waitFor(() => expect(businessPartnerService.submitPartnerForApproval).toHaveBeenCalledWith('partner-role-test'));
    expect(workflowMocks.refresh).toHaveBeenCalled();
  });

  it('shows decisions only to the checker assigned by workflow', async () => {
    workflowMocks.useWorkflowSummary.mockReturnValue({
      summary: {
        entityType: 'BusinessPartner', entityId: 'partner-role-test', approvalRequired: true,
        hasActiveInstance: true, hasWorkflowHistory: true, canCurrentUserApprove: true, pendingApprovers: [],
      },
      loading: false, error: undefined, refresh: workflowMocks.refresh,
      visibility: { known: true, active: true, direct: false, approvalRequired: true, showTab: true, tabLabel: 'Workflow', showApprovalControls: true },
    });
    vi.mocked(businessPartnerService.getById).mockResolvedValue(pendingPartner);
    vi.mocked(businessPartnerService.approvePartner).mockResolvedValue(undefined);

    render(<BusinessPartnerDetailPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(businessPartnerService.approvePartner).toHaveBeenCalledWith('partner-role-test'));
  });

  it('does not expose decision controls to the maker or another unassigned user', async () => {
    workflowMocks.useWorkflowSummary.mockReturnValue({
      summary: {
        entityType: 'BusinessPartner', entityId: 'partner-role-test', approvalRequired: true,
        hasActiveInstance: true, hasWorkflowHistory: true, canCurrentUserApprove: false, pendingApprovers: [],
      },
      loading: false, error: undefined, refresh: workflowMocks.refresh,
      visibility: { known: true, active: true, direct: false, approvalRequired: true, showTab: true, tabLabel: 'Workflow', showApprovalControls: true },
    });
    vi.mocked(businessPartnerService.getById).mockResolvedValue(pendingPartner);

    render(<BusinessPartnerDetailPage />);
    await screen.findByText(/awaiting an independent approval/i);

    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit for approval' })).not.toBeInTheDocument();
  });
});
