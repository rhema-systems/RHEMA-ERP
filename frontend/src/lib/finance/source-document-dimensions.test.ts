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
  getSourceLineDimensionAccounts,
  getSourceLineDimensionRuleResolution,
  resolveSourceDimensionValues,
  toFinanceDimensionValueRecord,
  toFinancePostingDimensionValues,
  toFinanceSourceDimensionFormState,
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
  it('combines additional accounts after applying each account route specificity', () => {
    const rules = [
      rule({ ruleType: 'Optional' }),
      rule({
        accountId: 'variance',
        accountNumber: '6200',
        ruleType: 'Optional',
        sourceRoute: undefined,
      }),
      rule({
        accountId: 'variance',
        accountNumber: '6200',
        ruleType: 'Required',
      }),
      rule({ accountId: 'unrelated', ruleType: 'Prohibited' }),
    ];
    const result = getSourceLineDimensionRuleResolution(
      rules,
      'expense',
      '2026-08-30',
      context,
      ['variance', 'variance', 'expense']
    );
    expect(result.conflicts).toEqual([]);
    expect(result.rules).toHaveLength(1);
    expect(result.rules[0].ruleType).toBe('Required');
    expect(
      getMissingRequiredDimensions(
        rules,
        'expense',
        '2026-08-30',
        context,
        {},
        ['variance']
      )
    ).toHaveLength(1);
    expect(
      getMissingRequiredDimensions(
        rules,
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'OPS' },
        ['variance']
      )
    ).toEqual([]);
  });

  it('uses a secondary Fixed value instead of a primary Required default', () => {
    const rules = [
      rule({ defaultValueCode: 'FIN' }),
      rule({
        accountId: 'variance',
        ruleType: 'Fixed',
        defaultValueCode: 'OPS',
      }),
    ];
    const result = getSourceLineDimensionRuleResolution(
      rules,
      'expense',
      '2026-08-30',
      context,
      ['variance']
    );
    expect(result.conflicts).toEqual([]);
    expect(result.rules[0]).toMatchObject({
      ruleType: 'Fixed',
      defaultValueCode: 'OPS',
    });
    expect(
      resolveSourceDimensionValues(
        [definition('department', 'DEPT', ['FIN', 'OPS'])],
        rules,
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'FIN' },
        ['variance']
      )
    ).toEqual({ DEPT: 'OPS' });
  });

  it('lets Prohibited defeat Optional but reports Required or Fixed conflicts', () => {
    const prohibited = rule({
      accountId: 'variance',
      accountNumber: '6200',
      ruleType: 'Prohibited',
    });
    const optional = [
      rule({ ruleType: 'Optional', defaultValueCode: 'FIN' }),
      prohibited,
    ];
    expect(
      getSourceLineDimensionRuleResolution(
        optional,
        'expense',
        '2026-08-30',
        context,
        ['variance']
      ).rules[0].ruleType
    ).toBe('Prohibited');
    expect(
      resolveSourceDimensionValues(
        [definition('department', 'DEPT', ['FIN'])],
        optional,
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'FIN' },
        ['variance']
      )
    ).toEqual({});
    for (const ruleType of ['Required', 'Fixed'] as const) {
      const result = getSourceLineDimensionRuleResolution(
        [rule({ ruleType, defaultValueCode: 'FIN' }), prohibited],
        'expense',
        '2026-08-30',
        context,
        ['variance']
      );
      expect(result.conflicts[0].message).toMatch(
        /both require and prohibit.*Ask Finance/
      );
      expect(result.rules[0].defaultValueCode).toBeUndefined();
    }
  });

  it('never chooses an arbitrary conflicting Fixed value', () => {
    const rules = [
      rule({ ruleType: 'Fixed', defaultValueCode: 'FIN' }),
      rule({
        accountId: 'variance',
        ruleType: 'Fixed',
        defaultValueCode: 'OPS',
      }),
    ];
    const result = getSourceLineDimensionRuleResolution(
      rules,
      'expense',
      '2026-08-30',
      context,
      ['variance']
    );
    expect(result.conflicts).toHaveLength(1);
    expect(result.conflicts[0].message).toMatch(/different Fixed values/);
    expect(result.rules[0].defaultValueCode).toBeUndefined();
    expect(
      resolveSourceDimensionValues(
        [definition('department', 'DEPT', ['FIN', 'OPS'])],
        rules,
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'FIN' },
        ['variance']
      )
    ).toEqual({});
  });

  it('leaves conflicting nonfixed defaults unset until a value is explicitly chosen', () => {
    const rules = [
      rule({ defaultValueCode: 'FIN' }),
      rule({
        accountId: 'variance',
        ruleType: 'Optional',
        defaultValueCode: 'OPS',
      }),
    ];
    const definitions = [definition('department', 'DEPT', ['FIN', 'OPS'])];
    expect(
      getSourceLineDimensionRuleResolution(
        rules,
        'expense',
        '2026-08-30',
        context,
        ['variance']
      ).conflicts
    ).toEqual([]);
    expect(
      resolveSourceDimensionValues(
        definitions,
        rules,
        'expense',
        '2026-08-30',
        context,
        {},
        ['variance']
      )
    ).toEqual({});
    expect(
      resolveSourceDimensionValues(
        definitions,
        rules,
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'OPS' },
        ['variance']
      )
    ).toEqual({ DEPT: 'OPS' });
    expect(
      getMissingRequiredDimensions(
        rules,
        'expense',
        '2026-08-30',
        context,
        {},
        ['variance']
      )
    ).toHaveLength(1);
  });

  it('ignores inactive, expired and other-route rules on secondary accounts', () => {
    const primary = rule({ ruleType: 'Optional' });
    const result = getSourceLineDimensionRuleResolution(
      [
        primary,
        rule({
          accountId: 'variance',
          ruleType: 'Prohibited',
          isActive: false,
        }),
        rule({
          accountId: 'variance',
          ruleType: 'Fixed',
          defaultValueCode: 'FIN',
          expiryDate: '2026-08-01',
        }),
        rule({
          accountId: 'variance',
          ruleType: 'Required',
          sourceRoute: 'another-route',
        }),
      ],
      'expense',
      '2026-08-30',
      context,
      ['variance']
    );
    expect(result.rules[0].ruleType).toBe('Optional');
    expect(result.conflicts).toEqual([]);
  });

  it('keeps single-account rule selection and defaults unchanged', () => {
    const primary = rule({ defaultValueCode: 'FIN' });
    const result = getSourceLineDimensionRuleResolution(
      [primary],
      'expense',
      '2026-08-30',
      context,
      ['expense']
    );
    expect(result.rules).toEqual(
      getApplicableDimensionRules([primary], 'expense', '2026-08-30', context)
    );
    expect(result.rules[0]).toBe(primary);
    expect(
      resolveSourceDimensionValues(
        [definition('department', 'DEPT', ['FIN', 'OPS'])],
        [primary],
        'expense',
        '2026-08-30',
        context,
        { DEPT: 'OPS' }
      )
    ).toEqual({ DEPT: 'FIN' });
  });

  it('gets additional accounts from the same persisted source line without trusting other lines', () => {
    const evidence = {
      lines: [
        {
          sourceLineId: 'line-1',
          accountId: 'accrued',
          additionalAccountIds: ['variance', 'accrued', 'variance'],
          requiredDimensionCodes: ['DEPT'],
        },
        {
          sourceLineId: 'line-2',
          accountId: 'other',
          additionalAccountIds: ['unrelated'],
        },
      ],
    } as Parameters<typeof getSourceLineDimensionAccounts>[0];
    expect(getSourceLineDimensionAccounts(evidence, 'line-1')).toEqual({
      accountId: 'accrued',
      additionalAccountIds: ['variance'],
      requiredDimensionCodes: ['DEPT'],
    });
    expect(
      getSourceLineDimensionAccounts(evidence, 'line-1', 'edited')
    ).toMatchObject({ accountId: 'edited' });
    expect(getSourceLineDimensionAccounts(evidence, 'new-line')).toEqual({
      accountId: undefined,
      additionalAccountIds: [],
      requiredDimensionCodes: [],
    });
  });
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

  it('serializes only canonical code pairs and restores persisted evidence', () => {
    expect(
      toFinancePostingDimensionValues({
        PROJECT: 'P100',
        DEPT: 'OPS',
        FUND: '',
      })
    ).toEqual([
      { dimensionCode: 'DEPT', valueCode: 'OPS' },
      { dimensionCode: 'PROJECT', valueCode: 'P100' },
    ]);
    expect(
      toFinanceDimensionValueRecord([
        {
          dimensionCode: 'DEPT',
          dimensionName: 'Department',
          valueCode: 'OPS',
          valueName: 'Operations',
          isReadOnly: true,
          ruleType: 'Fixed',
        },
      ])
    ).toEqual({ DEPT: 'OPS' });
  });

  it('hydrates document and line values under their persisted source-line identities', () => {
    expect(
      toFinanceSourceDimensionFormState({
        routeId: 'FinanceApVendorInvoice',
        certificationState: 'CaptureOptional',
        sourceDocumentId: 'invoice-1',
        defaultValues: [
          {
            dimensionCode: 'FUND',
            dimensionName: 'Fund',
            valueCode: 'GENERAL',
            valueName: 'General',
            isReadOnly: false,
          },
        ],
        lines: [
          {
            sourceLineId: 'persisted-line-1',
            accountId: 'expense',
            isFrozen: false,
            values: [
              {
                dimensionCode: 'DEPT',
                dimensionName: 'Department',
                valueCode: 'OPS',
                valueName: 'Operations',
                isReadOnly: false,
              },
            ],
            readinessWarnings: [],
          },
        ],
        readinessWarnings: [],
        budgetEvidenceStatus: 'NotEvaluated',
      })
    ).toEqual({
      defaultValues: { FUND: 'GENERAL' },
      lineValues: { 'persisted-line-1': { DEPT: 'OPS' } },
    });
  });
});
