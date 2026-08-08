import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { StatutoryReportCataloguePage } from './StatutoryReportCataloguePage';

vi.mock('@tanstack/react-query', () => ({
  useQuery: ({ queryKey }: { queryKey: string[] }) => ({
    data: queryKey[0] === 'reports' ? [
      {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'APP vs Actual',
        description: 'Published report',
        type: 'procurement',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-04T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['app-vs-actual'],
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        name: 'Balance Register',
        description: 'Published report',
        type: 'inventory',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-04T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['balance-register'],
      },
      {
        id: '33333333-3333-3333-3333-333333333333',
        name: 'Opening Register',
        description: 'Published report',
        type: 'compliance',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-04T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['opening-register'],
      },
    ] : [],
    isLoading: false,
    isError: false,
    isFetching: false,
    refetch: vi.fn(),
  }),
  useMutation: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    hasAnyRole: () => true,
    hasPermission: () => true,
  }),
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

vi.mock('@/services/reports', () => ({
  reportsService: {
    getReports: vi.fn(),
    executeReport: vi.fn(),
    exportReport: vi.fn(),
  },
}));

vi.mock('@/services/businessPartnerService', () => ({
  businessPartnerService: { getAllPartnersForDropdown: vi.fn().mockResolvedValue([]) },
}));

vi.mock('@/services/inventoryManagementService', () => ({
  inventoryManagementService: {
    getWarehouses: vi.fn().mockResolvedValue([]),
    getInventoryCategories: vi.fn().mockResolvedValue([]),
    getStockMovementTypes: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: { getFiscalPeriods: vi.fn().mockResolvedValue([]) },
}));

function renderCatalogue(reportCode?: string, mode: 'procurement' | 'inventory' | 'compliance' = 'procurement') {
  return render(<StatutoryReportCataloguePage mode={mode} reportCode={reportCode} />);
}

describe('StatutoryReportCataloguePage navigation', () => {
  it('shows report categories on a clean module landing page', async () => {
    renderCatalogue();

    expect(await screen.findByRole('heading', { name: 'Procurement reports' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Planning and performance' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /APP vs Actual/ })).toHaveAttribute(
      'href',
      '/reports/purchasing/app-vs-actual',
    );
    expect(screen.queryByText(/TDC-\d+/)).not.toBeInTheDocument();
    expect(screen.queryByText('Report criteria')).not.toBeInTheDocument();
    expect(screen.queryByText('Choose a report category to open its criteria and generation page.')).not.toBeInTheDocument();
    expect(screen.queryByText('Select a report to configure its filters and generate results.')).not.toBeInTheDocument();
  });

  it('shows only relevant compact filters on the dedicated report route', async () => {
    renderCatalogue('app-vs-actual');

    expect(await screen.findByRole('heading', { name: 'APP vs Actual' })).toBeInTheDocument();
    expect(screen.getByLabelText('Start date')).toBeInTheDocument();
    expect(screen.getByLabelText('End date')).toBeInTheDocument();
    expect(screen.getByLabelText('Fiscal year')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Export/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Run report' })).toBeInTheDocument();
    expect(screen.queryByText('Supplier')).not.toBeInTheDocument();
    expect(screen.queryByText('Published report')).not.toBeInTheDocument();
    expect(screen.queryByText('Set the required filters, then run or export this report.')).not.toBeInTheDocument();
    expect(screen.queryByText(/Filters and generation metadata/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open procurement report navigator' }));
    const navigator = await screen.findByRole('complementary', { name: 'Procurement report navigator' });
    expect(within(navigator).getByRole('heading', { name: 'Reports' })).toBeInTheDocument();
    expect(within(navigator).getByRole('button', { name: 'Close report navigator' })).toHaveClass('text-red-600');
    expect(within(navigator).getByRole('link', { name: 'All procurement reports' })).toHaveAttribute('href', '/reports/purchasing');
    expect(within(navigator).getByRole('link', { name: /APP vs Actual/ })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByText(/TDC-\d+/)).not.toBeInTheDocument();
  });

  it('does not render irrelevant inventory filters', async () => {
    renderCatalogue('balance-register', 'inventory');

    expect(await screen.findByRole('heading', { name: 'Balance Register' })).toBeInTheDocument();
    expect(screen.getByText('Warehouse')).toBeInTheDocument();
    expect(screen.getByText('Category')).toBeInTheDocument();
    expect(screen.getByLabelText('Start date')).toBeInTheDocument();
    expect(screen.getByLabelText('End date')).toBeInTheDocument();
    expect(screen.queryByLabelText('Status / classification')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Slow-moving days')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Expiry warning days')).not.toBeInTheDocument();
  });

  it('exposes the dedicated audit and compliance catalogue through the shared report shell', async () => {
    renderCatalogue(undefined, 'compliance');

    expect(await screen.findByRole('heading', { name: 'Audit & Compliance reports' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sourcing integrity' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Opening Register/ })).toHaveAttribute(
      'href',
      '/reports/audit-compliance/opening-register',
    );
  });
});
