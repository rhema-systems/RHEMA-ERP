import React, { type ReactNode } from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.stubGlobal('React', React);

vi.mock('@/components/auth/tenant-guard', () => ({
  TenantGuard: ({ children }: { children: ReactNode }) => (
    <div data-testid="tenant-guard">{children}</div>
  ),
}));

vi.mock('@/components/layout/dashboard-layout', () => ({
  DashboardLayout: ({ children, defaultSidebarCollapsed = false }: { children: ReactNode; defaultSidebarCollapsed?: boolean }) => (
    <div data-testid="dashboard-layout" data-sidebar-collapsed={defaultSidebarCollapsed}>{children}</div>
  ),
}));

vi.mock('@/components/reports/StatutoryReportCataloguePage', () => ({
  StatutoryReportCataloguePage: ({ mode, reportCode }: { mode: string; reportCode?: string }) => (
    <div data-testid={`statutory-report-${mode}`} data-report-code={reportCode} />
  ),
}));

import InventoryReportsPage from './inventory/page';
import InventoryReportPage from './inventory/[reportCode]/page';
import PurchasingReportsPage from './purchasing/page';
import ProcurementReportPage from './purchasing/[reportCode]/page';

describe('statutory report route layout', () => {
  it.each([
    ['inventory', InventoryReportsPage],
    ['procurement', PurchasingReportsPage],
  ])('renders the %s catalogue inside the standard tenant dashboard shell', (mode, Page) => {
    render(<Page />);

    const guard = screen.getByTestId('tenant-guard');
    const layout = screen.getByTestId('dashboard-layout');
    const catalogue = screen.getByTestId(`statutory-report-${mode}`);

    expect(guard).toContainElement(layout);
    expect(layout).toContainElement(catalogue);
    expect(layout).toHaveAttribute('data-sidebar-collapsed', 'false');
  });

  it.each([
    ['inventory', InventoryReportPage, 'balance-register'],
    ['procurement', ProcurementReportPage, 'app-vs-actual'],
  ])('renders the %s report detail in the same tenant dashboard shell', async (mode, Page, reportCode) => {
    const page = await Page({ params: Promise.resolve({ reportCode }) });
    render(page);

    const guard = screen.getByTestId('tenant-guard');
    const layout = screen.getByTestId('dashboard-layout');
    const report = screen.getByTestId(`statutory-report-${mode}`);

    expect(guard).toContainElement(layout);
    expect(layout).toContainElement(report);
    expect(layout).toHaveAttribute('data-sidebar-collapsed', 'true');
    expect(report).toHaveAttribute('data-report-code', reportCode);
  });
});
