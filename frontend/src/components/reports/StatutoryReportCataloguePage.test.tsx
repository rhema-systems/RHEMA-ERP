import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  civilEngineeringCatalogue,
  StatutoryReportCataloguePage,
} from './StatutoryReportCataloguePage';

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
        id: '11111111-1111-1111-1111-111111111112',
        name: 'Purchase Requisition Status Register',
        description: 'Published report',
        type: 'procurement',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-12T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['requisition-status'],
      },
      {
        id: '11111111-1111-1111-1111-111111111113',
        name: 'Purchase Order Register',
        description: 'Published report',
        type: 'procurement',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-12T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['purchase-order-register'],
      },
      {
        id: '11111111-1111-1111-1111-111111111114',
        name: 'Procurement Commitment Register',
        description: 'Published report',
        type: 'procurement',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-12T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['commitment-register'],
      },
      {
        id: '11111111-1111-1111-1111-111111111115',
        name: 'Procurement Certificate Tracking Register',
        description: 'Published report',
        type: 'procurement',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-12T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['certificate-tracking'],
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
      {
        id: '44444444-4444-4444-4444-444444444444',
        name: 'BoQ Summary',
        description: 'Published report',
        type: 'quantity-survey',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-11T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['boq-summary'],
      },
      {
        id: '44444444-4444-4444-4444-444444444445',
        name: 'Retention Register',
        description: 'Published report',
        type: 'quantity-survey',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-29T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['retention-register'],
      },
      {
        id: '44444444-4444-4444-4444-444444444446',
        name: 'Cost-to-Complete Report',
        description: 'Published report',
        type: 'quantity-survey',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-29T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['cost-to-complete'],
      },
      {
        id: '44444444-4444-4444-4444-444444444447',
        name: 'Contract Balance Report',
        description: 'Published report',
        type: 'quantity-survey',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-29T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['contract-balance'],
      },
      {
        id: '44444444-4444-4444-4444-444444444448',
        name: 'Quantity Survey Audit Trail',
        description: 'Published report',
        type: 'quantity-survey',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-29T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['audit-trail'],
      },
      ...[
        ['55555555-5555-5555-5555-555555555551', 'Design Backlog', 'design-backlog'],
        ['55555555-5555-5555-5555-555555555552', 'Field Task Register', 'field-task-register'],
        ['55555555-5555-5555-5555-555555555553', 'Supervision Controls', 'supervision-controls'],
        ['55555555-5555-5555-5555-555555555554', 'Maintenance and Complaints', 'maintenance-complaints'],
        ['55555555-5555-5555-5555-555555555555', 'Permitting Watch', 'permitting-watch'],
        ['55555555-5555-5555-5555-555555555556', 'Completion and Handover Register', 'completion-handover'],
        ['55555555-5555-5555-5555-555555555561', 'Engineering Work Register', 'engineering-work-register'],
        ['55555555-5555-5555-5555-555555555562', 'Inspection Report', 'inspection-report'],
        ['55555555-5555-5555-5555-555555555563', 'Site Instruction Log', 'site-instruction-log'],
        ['55555555-5555-5555-5555-555555555564', 'Progress Report', 'progress-report'],
        ['55555555-5555-5555-5555-555555555565', 'Defect Report', 'defect-report'],
        ['55555555-5555-5555-5555-555555555566', 'Completion Certificate Report', 'completion-certificate-report'],
        ['55555555-5555-5555-5555-555555555567', 'Project Dashboard', 'project-dashboard'],
        ['55555555-5555-5555-5555-555555555568', 'Engineering Audit Trail', 'engineering-audit-trail'],
      ].map(([id, name, code]) => ({
        id,
        name,
        description: 'Published Civil Engineering report',
        type: 'civil-engineering',
        status: 'published',
        createdBy: 'system',
        createdAt: '2026-08-29T00:00:00Z',
        isScheduled: false,
        isFavorite: false,
        tags: ['architecture-16.5', code],
      })),
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

vi.mock('@/services/projectService', () => ({
  projectService: {
    getProjects: vi.fn().mockResolvedValue({
      items: [{ id: '55555555-5555-5555-5555-555555555555', projectCode: 'PRJ-001', title: 'Works project' }],
      totalCount: 1,
      page: 1,
      pageSize: 500,
      totalPages: 1,
    }),
  },
}));

function renderCatalogue(reportCode?: string, mode: 'procurement' | 'inventory' | 'compliance' | 'quantity-survey' | 'civil-engineering' = 'procurement') {
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
    expect(screen.getByRole('link', { name: /Requisition Status/ })).toHaveAttribute(
      'href',
      '/reports/purchasing/requisition-status',
    );
    expect(screen.getByRole('link', { name: /Purchase Order Register/ })).toHaveAttribute(
      'href',
      '/reports/purchasing/purchase-order-register',
    );
    expect(screen.getByRole('link', { name: /Commitment Register/ })).toHaveAttribute(
      'href',
      '/reports/purchasing/commitment-register',
    );
    expect(screen.getByRole('link', { name: /Certificate Tracking/ })).toHaveAttribute(
      'href',
      '/reports/purchasing/certificate-tracking',
    );
    expect(screen.queryByText(/TDC-\d+/)).not.toBeInTheDocument();
    expect(screen.queryByText('Report criteria')).not.toBeInTheDocument();
    expect(screen.queryByText('Choose a report category to open its criteria and generation page.')).not.toBeInTheDocument();
    expect(screen.queryByText('Select a report to configure its filters and generate results.')).not.toBeInTheDocument();
  });

  it('uses controlled fiscal-year and supplier selectors for the new operational reports', async () => {
    const requisition = renderCatalogue('requisition-status');
    expect(await screen.findByRole('heading', { name: 'Requisition Status' })).toBeInTheDocument();
    expect(screen.getByLabelText('Fiscal year')).toBeInTheDocument();
    expect(screen.queryByText('Supplier')).not.toBeInTheDocument();
    requisition.unmount();

    renderCatalogue('purchase-order-register');
    expect(await screen.findByRole('heading', { name: 'Purchase Order Register' })).toBeInTheDocument();
    expect(screen.getByText('Supplier')).toBeInTheDocument();
    expect(screen.queryByLabelText('Fiscal year')).not.toBeInTheDocument();
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

  it('uses the shared report shell and a controlled project selector for Quantity Survey reports', async () => {
    renderCatalogue('boq-summary', 'quantity-survey');

    expect(await screen.findByRole('heading', { name: 'BoQ Summary' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Project' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Run report' })).toBeDisabled();
    expect(screen.getByLabelText('Start date')).toBeInTheDocument();
    expect(screen.getByLabelText('End date')).toBeInTheDocument();
    expect(screen.queryByLabelText('Status / classification')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open quantity survey report navigator' }));
    const navigator = await screen.findByRole('complementary', { name: 'Quantity Survey report navigator' });
    expect(within(navigator).getByRole('link', { name: 'All quantity survey reports' })).toHaveAttribute(
      'href',
      '/reports/quantity-survey',
    );
    expect(within(navigator).getByRole('link', { name: /Retention Register/ })).toHaveAttribute(
      'href',
      '/reports/quantity-survey/retention-register',
    );
    expect(within(navigator).getByRole('link', { name: /Cost-to-Complete Report/ })).toBeInTheDocument();
    expect(within(navigator).getByRole('link', { name: /Contract Balance Report/ })).toBeInTheDocument();
    expect(within(navigator).getByRole('link', { name: /Quantity Survey Audit Trail/ })).toBeInTheDocument();
  });

  it('exposes Civil Engineering reports through the same scoped report shell', async () => {
    renderCatalogue('design-backlog', 'civil-engineering');

    expect(await screen.findByRole('heading', { name: 'Design Backlog' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Project' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Run report' })).toBeDisabled();
    expect(screen.getByLabelText('Start date')).toBeInTheDocument();
    expect(screen.getByLabelText('End date')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open civil engineering report navigator' }));
    const navigator = await screen.findByRole('complementary', { name: 'Civil Engineering report navigator' });
    expect(within(navigator).getByRole('link', { name: 'All civil engineering reports' })).toHaveAttribute(
      'href',
      '/reports/civil-engineering',
    );
  });

  it('exposes the exact architecture-named Civil report routes in the shared report shell', async () => {
    renderCatalogue(undefined, 'civil-engineering');

    expect(await screen.findByRole('heading', { name: 'Civil Engineering reports' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Architecture-required outputs' })).toBeInTheDocument();
    expect(civilEngineeringCatalogue).toHaveLength(14);
    expect(civilEngineeringCatalogue.map(item => item.code)).toEqual(
      expect.arrayContaining([
        'engineering-work-register',
        'inspection-report',
        'site-instruction-log',
        'progress-report',
        'defect-report',
        'completion-certificate-report',
        'project-dashboard',
        'engineering-audit-trail',
      ]),
    );
    expect(screen.getByRole('link', { name: /Completion Certificate Report/ })).toHaveAttribute(
      'href',
      '/reports/civil-engineering/completion-certificate-report',
    );
    expect(screen.getByRole('link', { name: /Project Dashboard/ })).toHaveAttribute(
      'href',
      '/reports/civil-engineering/project-dashboard',
    );
    expect(screen.getByRole('link', { name: /Engineering Audit Trail/ })).toHaveAttribute(
      'href',
      '/reports/civil-engineering/engineering-audit-trail',
    );
  });
});
