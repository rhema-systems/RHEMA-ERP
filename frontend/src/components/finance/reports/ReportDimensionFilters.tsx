import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { ReportDimensionSelections } from '@/lib/finance/report-dimension-filters';
import type { FinanceDimensionDefinition } from '@/types/finance';

interface ReportDimensionFiltersProps {
  definitions: FinanceDimensionDefinition[];
  selections: ReportDimensionSelections;
  onSelectionChange: (definitionId: string, valueCode: string) => void;
  disabled?: boolean;
}

export function ReportDimensionFilters({
  definitions,
  selections,
  onSelectionChange,
  disabled = false,
}: ReportDimensionFiltersProps) {
  return [...definitions]
    .filter((definition) => definition.values.length > 0)
    .sort(
      (left, right) =>
        left.displayOrder - right.displayOrder ||
        left.code.localeCompare(right.code)
    )
    .map((definition) => (
      <div key={definition.id} className="space-y-2">
        <Label>{definition.name}</Label>
        <Select
          value={selections[definition.id] || '__all'}
          onValueChange={(value) =>
            onSelectionChange(definition.id, value === '__all' ? '' : value)
          }
          disabled={disabled}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all">All {definition.name}</SelectItem>
            {[...definition.values]
              .sort(
                (left, right) =>
                  left.displayOrder - right.displayOrder ||
                  left.code.localeCompare(right.code)
              )
              .map((value) => (
                <SelectItem key={value.id} value={value.code}>
                  {value.code} — {value.name}
                  {value.isActive ? '' : ' (inactive)'}
                </SelectItem>
              ))}
          </SelectContent>
        </Select>
      </div>
    ));
}
