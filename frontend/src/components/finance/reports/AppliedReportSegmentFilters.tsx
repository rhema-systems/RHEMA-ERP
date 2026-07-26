import { areFinanceSegmentFiltersEqual } from '@/lib/finance/report-segment-filters';
import type { FinanceSegmentFilterDto, SegmentStructure } from '@/types/finance';

interface AppliedReportSegmentFiltersProps {
    dimensions: SegmentStructure[];
    appliedFilters: FinanceSegmentFilterDto[];
    pendingFilters: FinanceSegmentFilterDto[];
}

export function AppliedReportSegmentFilters({
    dimensions,
    appliedFilters,
    pendingFilters,
}: AppliedReportSegmentFiltersProps) {
    const hasPendingChanges = !areFinanceSegmentFiltersEqual(appliedFilters, pendingFilters);
    if (appliedFilters.length === 0 && !hasPendingChanges) return null;

    const dimensionById = new Map(dimensions.map((dimension) => [dimension.id, dimension]));

    return (
        <div className="mt-4 space-y-2 border-t pt-4 text-sm">
            {appliedFilters.length > 0 && (
                <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium text-muted-foreground">Applied report filters:</span>
                    {appliedFilters.map((filter) => {
                        const dimension = filter.segmentStructureId
                            ? dimensionById.get(filter.segmentStructureId)
                            : undefined;
                        return (
                            <span
                                key={`${filter.segmentStructureId ?? filter.segmentCode}-${filter.segmentValue}`}
                                className="rounded-full bg-muted px-2.5 py-1 text-xs"
                            >
                                {dimension?.segmentName ?? filter.segmentCode ?? `Segment ${filter.segmentPosition}`}: {filter.segmentValue}
                            </span>
                        );
                    })}
                </div>
            )}
            {hasPendingChanges && (
                <p className="text-amber-700" role="status">
                    Segment selections have changed. Run the report to apply them; print and export still use the last successfully generated segment criteria.
                </p>
            )}
        </div>
    );
}
