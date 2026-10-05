'use client';

import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { SERIES_SCOPE_LABELS, SERIES_SCOPES } from '@/types/hr/company-schedule';
import type { SeriesScope } from '@/types/hr/company-schedule';

/**
 * "Which dates" for an action on one date of a recurring event (lane 2f-2b, D-12): this date only, this and following
 * dates, or every date. The server leaves a date that has started, been completed or been cancelled as it is.
 */
export function SeriesScopeField({
  value,
  onChange,
  hint,
}: {
  value: SeriesScope;
  onChange: (scope: SeriesScope) => void;
  /** What happens to the other dates, in the action's own words. */
  hint: string;
}) {
  return (
    <div className="space-y-2">
      <Label>Which dates</Label>
      <Select value={value} onValueChange={(v) => v && onChange(v as SeriesScope)}>
        <SelectTrigger><SelectValue /></SelectTrigger>
        <SelectContent>
          {SERIES_SCOPES.map((s) => (
            <SelectItem key={s} value={s}>{SERIES_SCOPE_LABELS[s]}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">
        {hint} A date that has started, been completed or been cancelled is left as it is; each guest is told once.
      </p>
    </div>
  );
}
