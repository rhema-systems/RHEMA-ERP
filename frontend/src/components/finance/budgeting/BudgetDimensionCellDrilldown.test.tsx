import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { BudgetDimensionCellDrilldown } from './BudgetDimensionCellDrilldown';

describe('BudgetDimensionCellDrilldown', () => {
    it('shows the exact dimensional budget position without mixing account totals', () => {
        render(
            <BudgetDimensionCellDrilldown
                currencyCode="GHS"
                cells={[{
                    periodCode: '2026-09',
                    financeDimensionSetId: 'set-fin-p100',
                    dimensionDisplayValue: 'DEPT: FIN · PROJECT: P100',
                    dimensionCombinationHash: 'hash-fin-p100',
                    dimensionAssignments: [
                        {
                            financeDimensionDefinitionId: 'definition-dept',
                            financeDimensionValueId: 'value-fin',
                            dimensionCode: 'DEPT',
                            dimensionName: 'Department',
                            valueCode: 'FIN',
                            valueName: 'Finance',
                        },
                        {
                            financeDimensionDefinitionId: 'definition-project',
                            financeDimensionValueId: 'value-p100',
                            dimensionCode: 'PROJECT',
                            dimensionName: 'Project',
                            valueCode: 'P100',
                            valueName: 'Project 100',
                        },
                    ],
                    budgetAmount: 10_000,
                    actualAmount: 500,
                    reservedAmount: 0,
                    availableAmount: 9_500,
                }]}
            />
        );

        expect(screen.getByText('2026-09')).toBeInTheDocument();
        expect(screen.getByText('DEPT: FIN — Finance')).toBeInTheDocument();
        expect(screen.getByText('PROJECT: P100 — Project 100')).toBeInTheDocument();
        expect(screen.getByText('GH₵10,000.00')).toBeInTheDocument();
        expect(screen.getByText('GH₵500.00')).toBeInTheDocument();
        expect(screen.getByText('GH₵0.00')).toBeInTheDocument();
        expect(screen.getByText('GH₵9,500.00')).toBeInTheDocument();
    });
});
