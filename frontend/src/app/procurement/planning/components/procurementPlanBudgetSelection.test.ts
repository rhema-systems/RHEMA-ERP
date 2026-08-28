import { describe, expect, it } from 'vitest';
import type {
  CreateProcurementPlanDto,
  ProcurementBudgetDto,
} from '@/services/procurementPlanningService';
import { applyProcurementPlanBudgetSelection } from './procurementPlanBudgetSelection';

const plan: CreateProcurementPlanDto = {
  title: 'Annual plan',
  departmentId: 'department-one',
  fiscalYear: 2026,
  planStartDate: '2026-01-01',
  planEndDate: '2026-12-31',
  currency: 'USD',
};

const budget: ProcurementBudgetDto = {
  id: 'budget-one',
  budgetCode: 'PB-2026-0002',
  title: 'Approved works budget',
  departmentId: 'department-one',
  fiscalYear: 2026,
  allocatedAmount: 100000,
  utilizedAmount: 0,
  committedAmount: 0,
  remainingAmount: 100000,
  currency: 'GHS',
  status: 'Approved',
  controlLevel: 'Strict',
  warningThresholdPercent: 80,
  utilizationPercent: 0,
  createdAt: '2026-01-01T00:00:00Z',
};

describe('applyProcurementPlanBudgetSelection', () => {
  it('inherits currency from the selected approved budget', () => {
    const result = applyProcurementPlanBudgetSelection(plan, [budget], budget.id);

    expect(result.budgetId).toBe(budget.id);
    expect(result.currency).toBe('GHS');
  });

  it('clears the link without inventing a different currency', () => {
    const result = applyProcurementPlanBudgetSelection(
      { ...plan, budgetId: budget.id, currency: budget.currency },
      [budget],
      '__none__',
    );

    expect(result.budgetId).toBeUndefined();
    expect(result.currency).toBe('GHS');
  });
});
