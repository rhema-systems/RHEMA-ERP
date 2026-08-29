import { describe, expect, it } from 'vitest';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';
import {
  getCommonManualDimensionValues,
  getManualDimensionSummary,
  getApplicableManualDimensionRules,
  getDefaultManualDimensionValues,
  getMissingRequiredManualDimension,
  resolveManualDimensionValues,
} from './manual-journal-dimensions';

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
  effectiveDate: '2025-01-01',
  isActive: true,
  ...overrides,
});

const dimension = (
  id: string,
  code: string,
  valueCodes: string[],
  displayOrder = 0
): FinanceDimensionDefinition => ({
  id,
  code,
  name: code,
  classification: 'Analytical',
  valueSourceType: 'Lookup',
  isActive: true,
  displayOrder,
  values: valueCodes.map((valueCode, index) => ({
    id: `${id}-${valueCode}`,
    financeDimensionDefinitionId: id,
    code: valueCode,
    name: valueCode,
    effectiveDate: '2025-01-01',
    isActive: true,
    displayOrder: index,
  })),
});

describe('manual journal dimension rules', () => {
  it('selects the most specific effective manual-journal rule', () => {
    const selected = getApplicableManualDimensionRules(
      [
        rule({ id: 'global', ruleType: 'Optional' }),
        rule({
          id: 'manual',
          sourceModule: 'GL',
          sourceDocumentType: 'ManualJournalEntry',
          postingAction: 'Post',
        }),
        rule({ id: 'future', effectiveDate: '2027-01-01' }),
      ],
      'expense',
      '2026-01-15'
    );
    expect(selected).toHaveLength(1);
    expect(selected[0].id).toBe('manual');
  });

  it('applies defaults and identifies an unresolved required value', () => {
    expect(
      getDefaultManualDimensionValues(
        [rule({ defaultValueCode: 'FIN' })],
        'expense',
        '2026-01-15'
      )
    ).toEqual({ DEPT: 'FIN' });

    expect(
      getMissingRequiredManualDimension([rule()], 'expense', '2026-01-15', {})
    ).toMatchObject({ dimensionCode: 'DEPT' });
  });

  it('does not apply a rule scoped to an uncertified producer', () => {
    expect(
      getApplicableManualDimensionRules(
        [
          rule({
            sourceModule: 'AP',
            sourceDocumentType: 'VendorInvoice',
            postingAction: 'Post',
          }),
        ],
        'expense',
        '2026-01-15'
      )
    ).toEqual([]);
  });

  it('inherits operator defaults without bypassing fixed or prohibited account rules', () => {
    const definitions = [
      dimension('department', 'DEPT', ['FIN', 'OPS']),
      dimension('project', 'PROJECT', ['P100', 'P200'], 1),
      dimension('fund', 'FUND', ['GENERAL'], 2),
    ];
    const resolved = resolveManualDimensionValues(
      definitions,
      [
        rule({
          financeDimensionDefinitionId: 'department',
          dimensionCode: 'DEPT',
          ruleType: 'Fixed',
          defaultValueCode: 'FIN',
        }),
        rule({
          financeDimensionDefinitionId: 'project',
          dimensionCode: 'PROJECT',
          ruleType: 'Prohibited',
        }),
      ],
      'expense',
      '2026-01-15',
      { DEPT: 'OPS', PROJECT: 'P200', FUND: 'GENERAL' }
    );

    expect(resolved).toEqual({ DEPT: 'FIN', FUND: 'GENERAL' });
  });

  it('derives shared defaults and a compact non-mutating summary', () => {
    expect(
      getCommonManualDimensionValues([
        { DEPT: 'OPS', PROJECT: 'P200' },
        { DEPT: 'OPS', PROJECT: 'P100' },
      ])
    ).toEqual({ DEPT: 'OPS' });

    const definitions = [
      dimension('project', 'PROJECT', ['P200'], 2),
      dimension('department', 'DEPT', ['OPS'], 1),
    ];
    expect(
      getManualDimensionSummary(
        definitions,
        { DEPT: 'OPS', PROJECT: 'P200' },
        1
      )
    ).toBe('OPS · +1');
    expect(definitions.map((item) => item.code)).toEqual(['PROJECT', 'DEPT']);
  });
});
