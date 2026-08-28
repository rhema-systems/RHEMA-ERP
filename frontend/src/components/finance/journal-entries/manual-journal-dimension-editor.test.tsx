import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ManualJournalDimensionCell } from './manual-journal-dimension-editor';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';

Object.assign(globalThis, { React });

const definitions: FinanceDimensionDefinition[] = [
  {
    id: 'department',
    code: 'DEPT',
    name: 'Department',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 1,
    values: [
      {
        id: 'ops',
        financeDimensionDefinitionId: 'department',
        code: 'OPS',
        name: 'Operations',
        effectiveDate: '2025-01-01',
        isActive: true,
        displayOrder: 1,
      },
    ],
  },
  {
    id: 'project',
    code: 'PROJECT',
    name: 'Project',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 2,
    values: [
      {
        id: 'p200',
        financeDimensionDefinitionId: 'project',
        code: 'P200',
        name: 'Project 200',
        effectiveDate: '2025-01-01',
        isActive: true,
        displayOrder: 1,
      },
    ],
  },
];

const rules: FinanceDimensionAccountRule[] = [
  {
    id: 'required-dept',
    accountId: 'expense',
    accountNumber: '6000',
    accountName: 'Expense',
    financeDimensionDefinitionId: 'department',
    dimensionCode: 'DEPT',
    dimensionName: 'Department',
    ruleType: 'Required',
    effectiveDate: '2025-01-01',
    isActive: true,
  },
];

describe('ManualJournalDimensionCell', () => {
  it('keeps the table compact and opens a focused governed editor', () => {
    render(
      <ManualJournalDimensionCell
        definitions={definitions}
        rules={rules}
        effectiveDate="2026-08-28"
        lineNumber={1}
        accountId="expense"
        accountLabel="6000 - Expense"
        values={{ DEPT: 'OPS', PROJECT: 'P200' }}
        defaults={{ DEPT: 'OPS' }}
        onChange={vi.fn()}
        onApplyToAll={vi.fn()}
      />
    );

    const trigger = screen.getByRole('button', { name: /OPS · P200/i });
    expect(trigger).toBeInTheDocument();
    expect(screen.queryByText('Required and fixed')).not.toBeInTheDocument();

    fireEvent.click(trigger);

    expect(screen.getByText('Line 1 coding dimensions')).toBeInTheDocument();
    expect(screen.getByText('Required and fixed')).toBeInTheDocument();
    expect(screen.getByText('Optional dimensions (1)')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /apply this coding to all lines/i })
    ).toBeInTheDocument();
  });
});
