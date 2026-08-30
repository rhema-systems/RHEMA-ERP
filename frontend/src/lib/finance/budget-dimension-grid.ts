import type {
  BudgetControlDimension,
  BudgetDimensionAssignment,
  BudgetDimensionAssignmentInput,
  BudgetEntry,
} from '@/types/budget';
import type { FinanceDimensionDefinition, FiscalPeriod } from '@/types/finance';

export const LEGACY_BUDGET_COMBINATION_KEY = 'LEGACY';

export interface BudgetDimensionCombination {
  key: string;
  label: string;
  assignments: BudgetDimensionAssignmentInput[];
}

const normalizedAssignments = (assignments: BudgetDimensionAssignmentInput[]) =>
  [...assignments].sort((left, right) =>
    left.financeDimensionDefinitionId.localeCompare(
      right.financeDimensionDefinitionId
    )
  );

export const budgetDimensionCombinationKey = (
  assignments: BudgetDimensionAssignmentInput[]
) =>
  assignments.length === 0
    ? LEGACY_BUDGET_COMBINATION_KEY
    : normalizedAssignments(assignments)
        .map(
          (assignment) =>
            `${assignment.financeDimensionDefinitionId}:${assignment.financeDimensionValueId}`
        )
        .join('|');

export const isCompleteBudgetDimensionCombination = (
  controls: BudgetControlDimension[],
  assignments: BudgetDimensionAssignmentInput[]
) => {
  if (controls.length === 0) return assignments.length === 0;
  const assignmentsByDefinition = new Map(
    assignments.map((assignment) => [
      assignment.financeDimensionDefinitionId,
      assignment.financeDimensionValueId,
    ])
  );
  return (
    assignmentsByDefinition.size === controls.length &&
    controls.every((control) =>
      assignmentsByDefinition.has(control.financeDimensionDefinitionId)
    )
  );
};

export const budgetDimensionCombinationLabel = (
  assignments: Array<
    BudgetDimensionAssignment | BudgetDimensionAssignmentInput
  >,
  controls: BudgetControlDimension[],
  definitions: FinanceDimensionDefinition[]
) => {
  if (assignments.length === 0) return 'Account and period (legacy)';
  const assignmentByDefinition = new Map(
    assignments.map((assignment) => [
      assignment.financeDimensionDefinitionId,
      assignment,
    ])
  );
  return [...controls]
    .sort((left, right) => left.displayOrder - right.displayOrder)
    .map((control) => {
      const assignment = assignmentByDefinition.get(
        control.financeDimensionDefinitionId
      );
      if (!assignment) return `${control.dimensionCode}: Missing`;
      if ('valueCode' in assignment) {
        return `${control.dimensionCode}: ${assignment.valueCode} — ${assignment.valueName}`;
      }
      const definition = definitions.find(
        (item) => item.id === control.financeDimensionDefinitionId
      );
      const value = definition?.values.find(
        (item) => item.id === assignment.financeDimensionValueId
      );
      return `${control.dimensionCode}: ${value ? `${value.code} — ${value.name}` : 'Unavailable value'}`;
    })
    .join(' · ');
};

export const buildBudgetDimensionCombinations = (
  entries: BudgetEntry[],
  controls: BudgetControlDimension[],
  definitions: FinanceDimensionDefinition[]
): BudgetDimensionCombination[] => {
  if (controls.length === 0) {
    return [
      {
        key: LEGACY_BUDGET_COMBINATION_KEY,
        label: 'Account and period (legacy)',
        assignments: [],
      },
    ];
  }

  const combinations = new Map<string, BudgetDimensionCombination>();
  for (const entry of entries) {
    const assignments = entry.dimensionAssignments.map((assignment) => ({
      financeDimensionDefinitionId: assignment.financeDimensionDefinitionId,
      financeDimensionValueId: assignment.financeDimensionValueId,
    }));
    if (!isCompleteBudgetDimensionCombination(controls, assignments)) continue;
    const key = budgetDimensionCombinationKey(assignments);
    combinations.set(key, {
      key,
      assignments: normalizedAssignments(assignments),
      label: budgetDimensionCombinationLabel(
        entry.dimensionAssignments,
        controls,
        definitions
      ),
    });
  }
  return [...combinations.values()].sort((left, right) =>
    left.label.localeCompare(right.label)
  );
};

export const isBudgetCombinationValidForPeriod = (
  assignments: BudgetDimensionAssignmentInput[],
  definitions: FinanceDimensionDefinition[],
  period: Pick<FiscalPeriod, 'startDate' | 'endDate'>
) => {
  const start = new Date(period.startDate).getTime();
  const end = new Date(period.endDate).getTime();
  return assignments.every((assignment) => {
    const definition = definitions.find(
      (item) => item.id === assignment.financeDimensionDefinitionId
    );
    const value = definition?.values.find(
      (item) => item.id === assignment.financeDimensionValueId
    );
    if (!value || !value.isActive) return false;
    const effective = new Date(value.effectiveDate).getTime();
    const expiry = value.expiryDate
      ? new Date(value.expiryDate).getTime()
      : Number.POSITIVE_INFINITY;
    return effective <= start && expiry >= end;
  });
};
