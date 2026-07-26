import { Label } from '@/components/ui/label';
import { ReportingDimensionCombobox } from '@/components/finance/reports/ReportingDimensionCombobox';
import type { ReportSegmentSelections } from '@/lib/finance/report-segment-filters';
import type { SegmentStructure } from '@/types/finance';

interface ReportSegmentFiltersProps {
    dimensions: SegmentStructure[];
    selections: ReportSegmentSelections;
    onSelectionChange: (segmentStructureId: string, value: string) => void;
    disabled?: boolean;
}

export function ReportSegmentFilters({
    dimensions,
    selections,
    onSelectionChange,
    disabled = false,
}: ReportSegmentFiltersProps) {
    return [...dimensions]
        .sort((left, right) => left.segmentPosition - right.segmentPosition)
        .map((dimension) => {
            const inputId = `report-segment-${dimension.id}`;

            return (
                <div key={dimension.id} className="space-y-2">
                    <Label htmlFor={inputId}>{dimension.segmentName}</Label>
                    <ReportingDimensionCombobox
                        dimension={dimension}
                        value={selections[dimension.id] || ''}
                        onValueChange={(value) => onSelectionChange(dimension.id, value)}
                        disabled={disabled}
                    />
                </div>
            );
        });
}
