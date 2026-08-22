import type {
  CreateProcurementPlanDto,
  ProcurementBudgetDto,
} from '@/services/procurementPlanningService';

export const applyProcurementPlanBudgetSelection = (
  current: CreateProcurementPlanDto,
  budgets: ProcurementBudgetDto[],
  budgetId: string,
): CreateProcurementPlanDto => {
  const selectedBudget = budgets.find((budget) => budget.id === budgetId);
  return {
    ...current,
    budgetId: selectedBudget?.id,
    currency: selectedBudget?.currency ?? current.currency,
  };
};
