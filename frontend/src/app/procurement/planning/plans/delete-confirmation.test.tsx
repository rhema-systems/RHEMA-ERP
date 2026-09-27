import React from 'react';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import { commonService, marketAnalysisService, procurementBudgetService } from '@/services/procurementPlanningService';
import { businessPartnerService } from '@/services/businessPartnerService';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { organizationUnitService } from '@/services/hr/organization-unit.service';

import ProcurementPlansPage from './page';
import EditProcurementPlanPage from './[id]/edit/page';

const router = vi.hoisted(() => ({ push: vi.fn(), back: vi.fn() }));
const service = vi.hoisted(() => ({
  getPlans: vi.fn(), getPlanById: vi.fn(), deletePlan: vi.fn(), removeItem: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'plan-1' }), useRouter: () => router }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('xlsx', () => ({}));
vi.mock('file-saver', () => ({ saveAs: vi.fn() }));
vi.mock('@/services/procurementPlanningService', () => ({
  procurementPlanService: service,
  procurementBudgetService: {
    getAvailableBudgetsForLinking: vi.fn().mockResolvedValue([]),
    getBudgetById: vi.fn().mockResolvedValue({ allocations: [] }),
  },
  commonService: { getDepartments: vi.fn().mockResolvedValue([]), getInventoryItems: vi.fn().mockResolvedValue([]) },
  marketAnalysisService: { getAnalyses: vi.fn().mockResolvedValue({ items: [] }) },
}));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { getUnitsOfMeasure: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/hr/organization-unit.service', () => ({ organizationUnitService: { getSummary: vi.fn().mockResolvedValue([]) } }));
vi.mock('../components/FiscalYearSelect', () => ({ FiscalYearSelect: () => <div>FY2026</div> }));

const item = {
  id: 'item-1', itemDescription: 'Disposable test item', estimatedQuantity: 1,
  estimatedUnitPrice: 10, estimatedTotalCost: 10, unitOfMeasure: 'EA', currency: 'GHS',
  priority: 'Medium', status: 'Planned', itemSuppliers: [],
};
const plan = {
  id: 'plan-1', planNumber: 'PP-2026-TEST', title: 'Test operations plan',
  organizationUnitId: 'ops', organizationUnitName: 'Operations', departmentName: 'Operations', fiscalYear: 2026,
  planningCycle: 'Annual', planStartDate: '2026-09-05', planEndDate: '2026-12-31',
  planDurationYears: 1, totalEstimatedBudget: 100, approvedBudget: 0,
  status: 'Draft', currency: 'GHS', createdAt: '2026-09-05', itemCount: 1, items: [item],
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(organizationUnitService.getSummary).mockResolvedValue([]);
  vi.mocked(commonService.getDepartments).mockResolvedValue([]);
  vi.mocked(commonService.getInventoryItems).mockResolvedValue([]);
  vi.mocked(procurementBudgetService.getAvailableBudgetsForLinking).mockResolvedValue([]);
  vi.mocked(procurementBudgetService.getBudgetById).mockResolvedValue({
    id: 'budget-1', budgetCode: 'BUD-2026-001', title: 'Operations budget', fiscalYear: 2026,
    allocatedAmount: 100, utilizedAmount: 0, committedAmount: 0, remainingAmount: 100,
    currency: 'GHS', status: 'Approved', controlLevel: 'Strict', warningThresholdPercent: 80,
    utilizationPercent: 0, createdAt: '2026-09-01', allocations: [], revisions: [],
  });
  vi.mocked(marketAnalysisService.getAnalyses).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });
  vi.mocked(businessPartnerService.getAllPartnersForDropdown).mockResolvedValue([]);
  vi.mocked(inventoryManagementService.getUnitsOfMeasure).mockResolvedValue([]);
  service.getPlans.mockResolvedValue({ items: [plan], totalPages: 1 });
  service.getPlanById.mockResolvedValue(plan);
  service.deletePlan.mockResolvedValue(undefined);
  service.removeItem.mockResolvedValue(undefined);
  vi.spyOn(window, 'confirm').mockReturnValue(false);
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const surfaces = [
  { name: 'plan register', Component: ProcurementPlansPage, title: 'Delete procurement plan?', confirm: 'Delete plan', target: 'PP-2026-TEST', api: service.deletePlan, args: ['plan-1'] },
  { name: 'plan item editor', Component: EditProcurementPlanPage, title: 'Delete procurement plan item?', confirm: 'Delete item', target: 'Disposable test item', api: service.removeItem, args: ['plan-1', 'item-1'] },
];

async function openDelete(surface: typeof surfaces[number]) {
  render(<surface.Component />);
  if (surface.name === 'plan item editor') {
    const tab = await screen.findByRole('tab', { name: /Plan Items/ });
    fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
  }
  const button = await screen.findByRole('button', { name: surface.name === 'plan register' ? 'Delete' : 'Delete item' });
  fireEvent.click(button);
  const dialog = await screen.findByRole('dialog', { name: surface.title });
  expect(within(dialog).getByText(new RegExp(surface.target))).toBeInTheDocument();
  expect(window.confirm).not.toHaveBeenCalled();
  return dialog;
}

describe.each(surfaces)('$name application confirmation', (surface) => {
  it('requires explicit confirmation and cancel does not delete', async () => {
    const dialog = await openDelete(surface);
    expect(surface.api).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: surface.title })).not.toBeInTheDocument());
    expect(surface.api).not.toHaveBeenCalled();
  });

  it('deletes only the selected record once and closes after success', async () => {
    const dialog = await openDelete(surface);
    let finish!: () => void;
    surface.api.mockImplementationOnce(() => new Promise<void>((resolvePromise) => { finish = resolvePromise; }));
    service.getPlans.mockResolvedValue({ items: [], totalPages: 1 });
    service.getPlanById.mockResolvedValue({ ...plan, items: [] });
    const confirm = within(dialog).getByRole('button', { name: surface.confirm });
    fireEvent.click(confirm);
    fireEvent.click(confirm);
    expect(surface.api).toHaveBeenCalledExactlyOnceWith(...surface.args);
    expect(within(dialog).getByRole('button', { name: 'Please wait...' })).toBeDisabled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }));
    expect(screen.getByRole('dialog', { name: surface.title })).toBeInTheDocument();
    await act(async () => finish());
    await waitFor(() => expect(screen.queryByRole('dialog', { name: surface.title })).not.toBeInTheDocument());
    expect(toast.success).toHaveBeenCalled();
  });

  it('retains the target and structured server error for retry after failure', async () => {
    const dialog = await openDelete(surface);
    const message = 'The plan is no longer a draft. (PLAN_NOT_DRAFT)';
    surface.api.mockRejectedValueOnce(new Error(message));
    fireEvent.click(within(dialog).getByRole('button', { name: surface.confirm }));
    await waitFor(() => expect(within(dialog).getByRole('alert')).toHaveTextContent(message));
    expect(screen.getByRole('dialog', { name: surface.title })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: surface.confirm })).toBeEnabled();
    expect(surface.api).toHaveBeenCalledExactlyOnceWith(...surface.args);
    expect(toast.success).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: surface.confirm }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: surface.title })).not.toBeInTheDocument());
    expect(surface.api).toHaveBeenCalledTimes(2);
    expect(toast.success).toHaveBeenCalled();
  });
});

it('does not open another item confirmation while a committed deletion is refreshing', async () => {
  service.getPlanById.mockResolvedValue({ ...plan, items: [item, { ...item, id: 'item-2', itemDescription: 'Retained item' }] });
  render(<EditProcurementPlanPage />);
  fireEvent.mouseDown(await screen.findByRole('tab', { name: /Plan Items/ }), { button: 0, ctrlKey: false });
  fireEvent.click(within(await screen.findByRole('row', { name: /Disposable test item/ })).getByRole('button', { name: 'Delete item' }));
  let finishRefresh!: (value: typeof plan) => void;
  service.getPlanById.mockImplementationOnce(() => new Promise((resolvePromise) => { finishRefresh = resolvePromise; }));
  fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete item' }));
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  fireEvent.click(within(screen.getByRole('row', { name: /Retained item/ })).getByRole('button', { name: 'Delete item' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  await act(async () => finishRefresh({ ...plan, items: [{ ...item, id: 'item-2', itemDescription: 'Retained item' }] }));
  expect(service.removeItem).toHaveBeenCalledExactlyOnceWith('plan-1', 'item-1');
});

it('does not re-offer item deletion when only the post-delete refresh fails', async () => {
  const surface = surfaces[1];
  const dialog = await openDelete(surface);
  service.getPlanById.mockRejectedValueOnce(new Error('Read unavailable'));
  fireEvent.click(within(dialog).getByRole('button', { name: surface.confirm }));
  await waitFor(() => expect(screen.queryByRole('dialog', { name: surface.title })).not.toBeInTheDocument());
  expect(service.removeItem).toHaveBeenCalledExactlyOnceWith('plan-1', 'item-1');
  expect(toast.error).toHaveBeenCalledWith('Item deleted, but the plan could not be refreshed. Reload the page before continuing.');
  expect(screen.queryByText('Disposable test item')).not.toBeInTheDocument();
});

it('keeps native JavaScript dialogs out of the procurement plan pages', () => {
  for (const path of ['page.tsx', '[id]/page.tsx', '[id]/edit/page.tsx', 'new/page.tsx']) {
    const source = readFileSync(resolve('src/app/procurement/planning/plans', path), 'utf8');
    expect(source).not.toMatch(/\b(?:window\.|globalThis\.)?(?:alert|confirm|prompt)\s*\(/);
  }
});
