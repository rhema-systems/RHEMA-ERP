import type { FinanceSegmentFilterDto, SegmentStructure } from '@/types/finance';

export type ReportSegmentSelections = Record<string, string>;
export type ReportSegmentQueryParameters = Record<string, string | number>;

export function buildFinanceSegmentFilters(
    dimensions: SegmentStructure[],
    selections: ReportSegmentSelections,
): FinanceSegmentFilterDto[] {
    return [...dimensions]
        .sort((left, right) => left.segmentPosition - right.segmentPosition)
        .map((dimension) => ({
            segmentStructureId: dimension.id,
            segmentCode: dimension.segmentCode,
            segmentPosition: dimension.segmentPosition,
            segmentValue: selections[dimension.id]?.trim() ?? '',
        }))
        .filter((filter) => filter.segmentValue.length > 0);
}

export function appendFinanceSegmentFilters(
    queryParams: URLSearchParams,
    filters: FinanceSegmentFilterDto[] | undefined,
): void {
    Object.entries(toFinanceSegmentFilterQueryParameters(filters)).forEach(([key, value]) => {
        queryParams.append(key, String(value));
    });
}

export function toFinanceSegmentFilterQueryParameters(
    filters: FinanceSegmentFilterDto[] | undefined,
): ReportSegmentQueryParameters {
    const parameters: ReportSegmentQueryParameters = {};

    (filters ?? [])
        .filter((filter) => filter.segmentValue.trim().length > 0)
        .forEach((filter, index) => {
            const prefix = `segmentFilters[${index}]`;
            if (filter.segmentStructureId) {
                parameters[`${prefix}.segmentStructureId`] = filter.segmentStructureId;
            }
            if (filter.segmentCode) {
                parameters[`${prefix}.segmentCode`] = filter.segmentCode;
            }
            if (filter.segmentPosition !== undefined) {
                parameters[`${prefix}.segmentPosition`] = filter.segmentPosition;
            }
            parameters[`${prefix}.segmentValue`] = filter.segmentValue.trim();
        });

    return parameters;
}

export function areFinanceSegmentFiltersEqual(
    left: FinanceSegmentFilterDto[] | undefined,
    right: FinanceSegmentFilterDto[] | undefined,
): boolean {
    const normalize = (filters: FinanceSegmentFilterDto[] | undefined) =>
        (filters ?? [])
            .map((filter) => ({
                segmentStructureId: filter.segmentStructureId ?? '',
                segmentCode: filter.segmentCode ?? '',
                segmentPosition: filter.segmentPosition ?? 0,
                segmentValue: filter.segmentValue.trim(),
            }))
            .sort((a, b) =>
                a.segmentPosition - b.segmentPosition ||
                a.segmentStructureId.localeCompare(b.segmentStructureId) ||
                a.segmentCode.localeCompare(b.segmentCode));

    return JSON.stringify(normalize(left)) === JSON.stringify(normalize(right));
}
