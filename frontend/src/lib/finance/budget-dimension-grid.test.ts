import { describe, expect, it } from 'vitest';
import type { BudgetControlDimension, BudgetEntry } from '@/types/budget';
import type { FinanceDimensionDefinition, FiscalPeriod } from '@/types/finance';
import {
  budgetDimensionCombinationKey,
  buildBudgetDimensionCombinations,
  isBudgetCombinationValidForPeriod,
  isCompleteBudgetDimensionCombination,
  LEGACY_BUDGET_COMBINATION_KEY,
} from './budget-dimension-grid';

const controls: BudgetControlDimension[] = [
  {
    financeDimensionDefinitionId: 'department',
    dimensionCode: 'DEPT',
    dimensionName: 'Department',
    displayOrder: 0,
  },
];

const definitions: FinanceDimensionDefinition[] = [
  {
    id: 'department',
    code: 'DEPT',
    name: 'Department',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 0,
    values: [
      {
        id: 'finance',
        financeDimensionDefinitionId: 'department',
        code: 'FIN',
        name: 'Finance',
        effectiveDate: '2025-01-01',
        expiryDate: '2025-12-31',
        isActive: true,
        displayOrder: 0,
      },
    ],
  },
];

it('keeps legacy scenarios on the original account-period grid', () => {
  expect(buildBudgetDimensionCombinations([], [], [])).toEqual([
    {
      key: LEGACY_BUDGET_COMBINATION_KEY,
      label: 'Account and period (legacy)',
      assignments: [],
    },
  ]);
});

it('uses a stable key regardless of assignment order', () => {
  const left = [
    { financeDimensionDefinitionId: 'project', financeDimensionValueId: 'p1' },
    {
      financeDimensionDefinitionId: 'department',
      financeDimensionValueId: 'finance',
    },
  ];
  expect(budgetDimensionCombinationKey(left)).toBe(
    budgetDimensionCombinationKey([...left].reverse())
  );
});

it('requires exactly one value for every scenario control', () => {
  expect(isCompleteBudgetDimensionCombination(controls, [])).toBe(false);
  expect(
    isCompleteBudgetDimensionCombination(controls, [
      {
        financeDimensionDefinitionId: 'department',
        financeDimensionValueId: 'finance',
      },
    ])
  ).toBe(true);
});

it('reconstructs persisted combinations with readable labels', () => {
  const entry = {
    dimensionAssignments: [
      {
        financeDimensionDefinitionId: 'department',
        financeDimensionValueId: 'finance',
        dimensionCode: 'DEPT',
        dimensionName: 'Department',
        valueCode: 'FIN',
        valueName: 'Finance',
      },
    ],
  } as BudgetEntry;
  expect(
    buildBudgetDimensionCombinations([entry], controls, definitions)[0].label
  ).toBe('DEPT: FIN — Finance');
});

describe('effective dating', () => {
  const assignment = [
    {
      financeDimensionDefinitionId: 'department',
      financeDimensionValueId: 'finance',
    },
  ];
  const period = (startDate: string, endDate: string) =>
    ({ startDate, endDate }) as FiscalPeriod;

  it('allows a value effective for the full fiscal period', () => {
    expect(
      isBudgetCombinationValidForPeriod(
        assignment,
        definitions,
        period('2025-01-01', '2025-01-31')
      )
    ).toBe(true);
  });

  it('blocks a cell outside the value effective range', () => {
    expect(
      isBudgetCombinationValidForPeriod(
        assignment,
        definitions,
        period('2026-01-01', '2026-01-31')
      )
    ).toBe(false);
  });
});
