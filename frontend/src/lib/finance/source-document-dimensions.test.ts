import { describe, expect, it } from 'vitest';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';
import {
  getApplicableDimensionRules,
  getCommonDimensionValues,
  getDimensionSummary,
  getMissingRequiredDimensions,
  resolveSourceDimensionValues,
  type FinanceDimensionRuleContext,
} from './source-document-dimensions';

const context: FinanceDimensionRuleContext = {
  sourceModule: 'AP',
  sourceDocumentType: 'VendorInvoice',
  postingAction: 'Post',
  sourceRoute: 'finance.ap.vendor-invoices.manual',
  contractVersion: '1.0',
};

const rule = (
  overrides: Partial<FinanceDimensionAccountRule> = {}
): FinanceDimensionAccountRule => ({
  id: crypto.randomUUID(),
  accountId: 'expense',
  accountNumber: '6000',
  accountName: 'Expense',
  financeDimensionDefinitionId: 'department',
  dimensionCode: 'DEPT',
  dimensionName: 'Department',
  ruleType: 'Required',
  sourceModule: 'AP',
  sourceDocumentType: 'VendorInvoice',
  postingAction: 'Post',
  sourceRoute: context.sourceRoute,
  contractVersion: context.contractVersion,
  effectiveDate: '2026-01-01',
  isActive: true,
  ...overrides,
});

const definition = (
  id: string,
  code: string,
  values: string[],
  displayOrder = 0
): FinanceDimensionDefinition => ({
  id,
  code,
  name: code,
  classification: 'Analytical',
  valueSourceType: 'Lookup',
  isActive: true,
  displayOrder,
  values: values.map((valueCode, index) => ({
    id: `${id}-${valueCode}`,
    financeDimensionDefinitionId: id,
    code: valueCode,
    name: valueCode,
    effectiveDate: '2026-01-01',
    isActive: true,
    displayOrder: index,
  })),
});

describe('source document dimensions', () => {
  it('matches the complete trusted route identity and selects the most specific rule', () => {
    const selected = getApplicableDimensionRules(
      [
        rule({
          id: 'global',
          sourceRoute: undefined,
          contractVersion: undefined,
        }),
        rule({ id: 'route' }),
        rule({ id: 'other-version', contractVersion: '2.0' }),
      ],
      'expense',
      '2026-08-30',
      context
    );

    expect(selected).toHaveLength(1);
    expect(selected[0].id).toBe('route');
  });

  it('applies document defaults without overriding fixed or prohibited rules', () => {
    const definitions = [
      definition('department', 'DEPT', ['FIN', 'OPS']),
      definition('project', 'PROJECT', ['P100']),
      definition('fund', 'FUND', ['GENERAL']),
    ];
    const resolved = resolveSourceDimensionValues(
      definitions,
      [
        rule({ ruleType: 'Fixed', defaultValueCode: 'FIN' }),
        rule({
          financeDimensionDefinitionId: 'project',
          dimensionCode: 'PROJECT',
          ruleType: 'Prohibited',
        }),
      ],
      'expense',
      '2026-08-30',
      context,
      { DEPT: 'OPS', PROJECT: 'P100', FUND: 'GENERAL' }
    );

    expect(resolved).toEqual({ DEPT: 'FIN', FUND: 'GENERAL' });
  });

  it('reports missing required values while preserving reusable default helpers', () => {
    expect(
      getMissingRequiredDimensions(
        [rule()],
        'expense',
        '2026-08-30',
        context,
        {}
      )
    ).toHaveLength(1);
    expect(
      getMissingRequiredDimensions([rule()], 'expense', '2026-08-30', context, {
        DEPT: 'OPS',
      })
    ).toEqual([]);
    expect(
      getCommonDimensionValues([
        { DEPT: 'OPS', PROJECT: 'P100' },
        { DEPT: 'OPS', PROJECT: 'P200' },
      ])
    ).toEqual({ DEPT: 'OPS' });
    expect(
      getDimensionSummary(
        [
          definition('department', 'DEPT', ['OPS']),
          definition('project', 'PROJECT', ['P100'], 1),
        ],
        { DEPT: 'OPS', PROJECT: 'P100' },
        1
      )
    ).toBe('OPS · +1');
  });
});
