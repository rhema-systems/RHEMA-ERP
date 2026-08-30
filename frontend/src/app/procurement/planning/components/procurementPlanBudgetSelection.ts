import type {
  CreateProcurementPlanDto,
  ProcurementBudgetDto,
} from '@/services/procurementPlanningService';

export const applyProcurementPlanBudgetSelection = <T extends CreateProcurementPlanDto>(
  current: T,
  budgets: ProcurementBudgetDto[],
  budgetId: string,
): T => {
  const selectedBudget = budgets.find((budget) => budget.id === budgetId);
  return {
    ...current,
    budgetId: selectedBudget?.id,
    currency: selectedBudget?.currency ?? current.currency,
  } as T;
};
