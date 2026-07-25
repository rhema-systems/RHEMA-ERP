import { describe, expect, it } from 'vitest';
import type { SegmentStructure } from '@/types/finance';
import {
    appendFinanceSegmentFilters,
    areFinanceSegmentFiltersEqual,
    buildFinanceSegmentFilters,
    toFinanceSegmentFilterQueryParameters,
} from './report-segment-filters';

const dimensions: SegmentStructure[] = [
    {
        id: 'department-id',
        segmentName: 'Department',
        segmentCode: 'DEPT',
        segmentPosition: 2,
        segmentLength: 3,
        dataType: 'Alphanumeric',
        lookupTableRequired: true,
        isMandatory: true,
        isReportingDimension: true,
        isNaturalAccount: false,
        isActive: true,
        createdAt: '2026-01-01',
        updatedAt: '2026-01-01',
    },
    {
        id: 'fund-id',
        segmentName: 'Fund',
        segmentCode: 'FUND',
        segmentPosition: 1,
        segmentLength: 3,
        dataType: 'Alphanumeric',
        lookupTableRequired: true,
        isMandatory: true,
        isReportingDimension: true,
        isNaturalAccount: false,
        isActive: true,
        createdAt: '2026-01-01',
        updatedAt: '2026-01-01',
    },
];

describe('financial report segment filters', () => {
    it('builds ordered structured filters and ignores unselected dimensions', () => {
        const filters = buildFinanceSegmentFilters(dimensions, {
            'department-id': ' 500 ',
            'fund-id': '',
        });

        expect(filters).toEqual([
            {
                segmentStructureId: 'department-id',
                segmentCode: 'DEPT',
                segmentPosition: 2,
                segmentValue: '500',
            },
        ]);
    });

    it('compares applied and pending filters independently of their array order', () => {
        const filters = buildFinanceSegmentFilters(dimensions, {
            'department-id': 'FIN',
            'fund-id': '001',
        });

        expect(areFinanceSegmentFiltersEqual(filters, [...filters].reverse())).toBe(true);
        expect(areFinanceSegmentFiltersEqual(filters, filters.map((filter, index) =>
            index === 0 ? { ...filter, segmentValue: 'OPS' } : filter
        ))).toBe(false);
    });

    it('serializes filters using ASP.NET indexed complex-object query keys', () => {
        const filters = buildFinanceSegmentFilters(dimensions, {
            'fund-id': '001',
            'department-id': '500',
        });

        expect(toFinanceSegmentFilterQueryParameters(filters)).toEqual({
            'segmentFilters[0].segmentStructureId': 'fund-id',
            'segmentFilters[0].segmentCode': 'FUND',
            'segmentFilters[0].segmentPosition': 1,
            'segmentFilters[0].segmentValue': '001',
            'segmentFilters[1].segmentStructureId': 'department-id',
            'segmentFilters[1].segmentCode': 'DEPT',
            'segmentFilters[1].segmentPosition': 2,
            'segmentFilters[1].segmentValue': '500',
        });

        const query = new URLSearchParams();
        appendFinanceSegmentFilters(query, filters);
        expect(query.get('segmentFilters[0].segmentValue')).toBe('001');
        expect(query.get('segmentFilters[1].segmentValue')).toBe('500');
    });
});
