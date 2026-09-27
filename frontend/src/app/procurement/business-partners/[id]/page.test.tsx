import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import BusinessPartnerDetailPage from './page';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn(), back: vi.fn() }), useParams: () => ({ id: 'partner-role-test' }) }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getById: vi.fn(), getPostingOptions: vi.fn() } }));
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
});
