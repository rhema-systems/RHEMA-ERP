import { areFinanceDimensionFiltersEqual } from '@/lib/finance/report-dimension-filters';
import type {
  FinanceDimensionDefinition,
  FinanceDimensionFilterDto,
} from '@/types/finance';

interface AppliedReportDimensionFiltersProps {
  definitions: FinanceDimensionDefinition[];
  appliedFilters: FinanceDimensionFilterDto[];
  pendingFilters: FinanceDimensionFilterDto[];
}

export function AppliedReportDimensionFilters({
  definitions,
  appliedFilters,
  pendingFilters,
}: AppliedReportDimensionFiltersProps) {
  const hasPendingChanges = !areFinanceDimensionFiltersEqual(
    appliedFilters,
    pendingFilters
  );
  if (appliedFilters.length === 0 && !hasPendingChanges) return null;

  const definitionById = new Map(
    definitions.map((definition) => [definition.id, definition])
  );
  return (
    <div className="mt-4 space-y-2 border-t pt-4 text-sm">
      {appliedFilters.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium text-muted-foreground">
            Applied transaction dimensions:
          </span>
          {appliedFilters.map((filter) => {
            const definition = filter.financeDimensionDefinitionId
              ? definitionById.get(filter.financeDimensionDefinitionId)
              : undefined;
            return (
              <span
                key={`${filter.financeDimensionDefinitionId ?? filter.dimensionCode}-${filter.valueCodes.join('-')}`}
                className="rounded-full bg-blue-50 px-2.5 py-1 text-xs text-blue-800"
              >
                {definition?.name ?? filter.dimensionCode ?? 'Dimension'}:{' '}
                {filter.valueCodes.join(', ')}
              </span>
            );
          })}
        </div>
      )}
      {hasPendingChanges && (
        <p className="text-amber-700" role="status">
          Dimension selections have changed. Run the report to apply them; print
          and export still use the last successfully generated criteria.
        </p>
      )}
    </div>
  );
}
