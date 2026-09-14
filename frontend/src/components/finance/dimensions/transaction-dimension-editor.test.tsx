import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TransactionDimensionLineEditor } from './transaction-dimension-editor';
import { SourceDocumentDimensionPanel } from './source-document-dimension-panel';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';

Object.assign(globalThis, {
  React,
  ResizeObserver: class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});
Object.defineProperty(Element.prototype, 'scrollIntoView', {
  configurable: true,
  value: vi.fn(),
});
const { catalogue } = vi.hoisted(() => ({
  catalogue: { definitions: [] as unknown[], rules: [] as unknown[] },
}));
vi.mock('@tanstack/react-query', () => ({
  useQuery: ({ queryKey }: { queryKey: string[] }) => ({
    data:
      queryKey[0] === 'finance-dimensions'
        ? catalogue.definitions
        : catalogue.rules,
    isLoading: false,
  }),
}));

const context = {
  sourceModule: 'AP',
  sourceDocumentType: 'VendorInvoice',
  postingAction: 'Post',
  sourceRoute: 'finance.ap.vendor-invoices.manual',
  contractVersion: '1.0',
};
const definitions: FinanceDimensionDefinition[] = [
  {
    id: 'department',
    code: 'DEPT',
    name: 'Department',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 0,
    values: ['FIN', 'OPS'].map((code) => ({
      id: code,
      financeDimensionDefinitionId: 'department',
      code,
      name: code,
      isActive: true,
      effectiveDate: '2026-01-01',
      displayOrder: 0,
    })),
  },
];
const makeRule = (
  overrides: Partial<FinanceDimensionAccountRule>
): FinanceDimensionAccountRule => ({
  id: crypto.randomUUID(),
  accountId: 'primary',
  accountNumber: '1300',
  accountName: 'Accrued',
  financeDimensionDefinitionId: 'department',
  dimensionCode: 'DEPT',
  dimensionName: 'Department',
  ruleType: 'Optional',
  effectiveDate: '2026-01-01',
  isActive: true,
  ...overrides,
});
const base = {
  definitions,
  context,
  effectiveDate: '2026-09-13',
  lineNumber: 1,
  accountId: 'primary',
  additionalAccountIds: ['variance'],
  values: {},
  defaults: { DEPT: 'FIN' },
  onChange: vi.fn(),
};
beforeEach(() => {
  vi.clearAllMocks();
  catalogue.definitions = definitions;
  catalogue.rules = [];
});

describe('multi-account source-line dimension editor', () => {
  it('hides a secondary Prohibited dimension and clears it only on explicit default application', () => {
    render(
      <TransactionDimensionLineEditor
        {...base}
        values={{ DEPT: 'FIN' }}
        rules={[
          makeRule({}),
          makeRule({ accountId: 'variance', ruleType: 'Prohibited' }),
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'FIN' }));
    expect(
      screen.queryByRole('combobox', { name: 'Department' })
    ).not.toBeInTheDocument();
    expect(base.onChange).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole('button', { name: 'Use document defaults' })
    );
    expect(base.onChange).toHaveBeenCalledWith({});
  });

  it('shows and locks a Fixed value belonging to the secondary account', () => {
    render(
      <TransactionDimensionLineEditor
        {...base}
        rules={[
          makeRule({}),
          makeRule({
            accountId: 'variance',
            ruleType: 'Fixed',
            defaultValueCode: 'OPS',
          }),
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'OPS' }));
    expect(screen.getByRole('combobox', { name: 'Department' })).toBeDisabled();
    expect(screen.getByText('Fixed')).toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', { name: 'Use document defaults' })
    );
    expect(base.onChange).toHaveBeenCalledWith({ DEPT: 'OPS' });
  });

  it('marks secondary Required dimensions and allows the user to supply the value', () => {
    render(
      <TransactionDimensionLineEditor
        {...base}
        certificationState="Enforced"
        rules={[
          makeRule({}),
          makeRule({ accountId: 'variance', ruleType: 'Required' }),
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /1 required/ }));
    expect(screen.getByText('Department *')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Department' })).toBeEnabled();
    expect(screen.getByText(/Missing Department/)).toBeInTheDocument();
  });

  it('shows an actionable Fixed conflict instead of selecting the first account value', () => {
    render(
      <TransactionDimensionLineEditor
        {...base}
        rules={[
          makeRule({ ruleType: 'Fixed', defaultValueCode: 'FIN' }),
          makeRule({
            accountId: 'variance',
            accountNumber: '6200',
            ruleType: 'Fixed',
            defaultValueCode: 'OPS',
          }),
        ]}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /1 conflict/ }));
    expect(screen.getByRole('alert')).toHaveTextContent(
      'require different Fixed values'
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Ask Finance');
    expect(
      screen.getByRole('combobox', { name: 'Department' })
    ).toHaveTextContent('Not assigned');
    expect(base.onChange).not.toHaveBeenCalled();
  });

  it('uses server required codes when current rule metadata is not returned', () => {
    render(
      <TransactionDimensionLineEditor
        {...base}
        rules={[]}
        requiredDimensionCodes={['DEPT']}
        certificationState="Enforced"
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /1 required/ }));
    expect(screen.getByText('Department *')).toBeInTheDocument();
  });

  it('applies document defaults using all account rules in the generic panel', () => {
    catalogue.rules = [
      makeRule({}),
      makeRule({
        accountId: 'variance',
        ruleType: 'Fixed',
        defaultValueCode: 'OPS',
      }),
    ];
    const onLineValuesChange = vi.fn();
    render(
      <SourceDocumentDimensionPanel
        context={context}
        effectiveDate="2026-09-13"
        lines={[
          {
            id: 'same-source-line',
            accountId: 'primary',
            additionalAccountIds: ['variance'],
          },
        ]}
        defaultValues={{ DEPT: 'FIN' }}
        lineValues={{ other: { DEPT: 'FIN' } }}
        onDefaultValuesChange={vi.fn()}
        onLineValuesChange={onLineValuesChange}
      />
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Apply to all eligible lines' })
    );
    expect(onLineValuesChange).toHaveBeenCalledWith({
      other: { DEPT: 'FIN' },
      'same-source-line': { DEPT: 'OPS' },
    });
  });
});
