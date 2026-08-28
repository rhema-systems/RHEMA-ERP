import { describe, expect, it } from 'vitest';
import {
  appendFinanceDimensionFilters,
  areFinanceDimensionFiltersEqual,
  buildFinanceDimensionFilters,
  readFinanceDimensionSelections,
  toFinanceDimensionFilterQueryParameters,
} from './report-dimension-filters';
import type { FinanceDimensionDefinition } from '@/types/finance';

const definitions: FinanceDimensionDefinition[] = [
  {
    id: 'dept-id',
    code: 'DEPT',
    name: 'Department',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 1,
    values: [
      {
        id: 'fin-id',
        financeDimensionDefinitionId: 'dept-id',
        code: 'FIN',
        name: 'Finance',
        effectiveDate: '2025-01-01',
        isActive: true,
        displayOrder: 1,
      },
    ],
  },
  {
    id: 'project-id',
    code: 'PROJECT',
    name: 'Project',
    classification: 'Analytical',
    valueSourceType: 'Lookup',
    isActive: true,
    displayOrder: 2,
    values: [
      {
        id: 'alpha-id',
        financeDimensionDefinitionId: 'project-id',
        code: 'A',
        name: 'Alpha',
        effectiveDate: '2025-01-01',
        isActive: true,
        displayOrder: 1,
      },
    ],
  },
];

describe('transaction-dimension report filters', () => {
  it('builds deterministic filters and flat PDF parameters', () => {
    const filters = buildFinanceDimensionFilters(definitions, {
      'project-id': 'A',
      'dept-id': 'FIN',
    });

    expect(filters.map((filter) => filter.dimensionCode)).toEqual([
      'DEPT',
      'PROJECT',
    ]);
    expect(toFinanceDimensionFilterQueryParameters(filters)).toEqual({
      'dimensionFilters[0].financeDimensionDefinitionId': 'dept-id',
      'dimensionFilters[0].dimensionCode': 'DEPT',
      'dimensionFilters[0].valueCodes': 'FIN',
      'dimensionFilters[1].financeDimensionDefinitionId': 'project-id',
      'dimensionFilters[1].dimensionCode': 'PROJECT',
      'dimensionFilters[1].valueCodes': 'A',
    });
  });

  it('uses repeated query values for the API and restores drill-down selections', () => {
    const filters = [
      {
        financeDimensionDefinitionId: 'dept-id',
        dimensionCode: 'DEPT',
        valueCodes: ['FIN', 'OPS'],
      },
    ];
    const params = new URLSearchParams();
    appendFinanceDimensionFilters(params, filters);

    expect(params.getAll('dimensionFilters[0].valueCodes')).toEqual([
      'FIN',
      'OPS',
    ]);
    expect(readFinanceDimensionSelections(params, definitions)).toEqual({
      'dept-id': 'FIN',
    });
  });

  it('compares filters independent of order', () => {
    expect(
      areFinanceDimensionFiltersEqual(
        [{ dimensionCode: 'DEPT', valueCodes: ['OPS', 'FIN'] }],
        [{ dimensionCode: 'DEPT', valueCodes: ['FIN', 'OPS'] }]
      )
    ).toBe(true);
  });
});
