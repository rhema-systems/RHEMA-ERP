'use client';

import { useEffect, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarRange } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { appraisalCycleLookupService } from '@/services/hr/goals.service';
import type { AppraisalCycleOption } from '@/types/hr/goals';

/**
 * Every goal screen is scoped to one appraisal cycle, so they all open with this.
 *
 * Cycles are their own slice of area 5 and are not editable here — this only reads them.
 * The list is every cycle, not just the open ones, because last year's goals still need to
 * be readable; the open ones are marked so the current one is obvious.
 */
export function useAppraisalCycles() {
  return useQuery({
    queryKey: ['hr', 'appraisal-cycles'],
    queryFn: () => appraisalCycleLookupService.getAll(),
    staleTime: 5 * 60 * 1000,
  });
}

/** `"FY2026 · Annual 2026"` — enough to tell two cycles of the same year apart. */
export function cycleLabel(cycle: AppraisalCycleOption): string {
  return `${cycle.cycleCode} · ${cycle.cycleName}`;
}

// A running cycle is Open; InProgress, which only the demo seeder wrote, is gone (D-14).
const OPEN_STATUSES = new Set(['Open']);

/** Sentinel for the "every cycle" option — Radix Select refuses an empty-string item value. */
const ALL = '__all__';

interface CycleSelectProps {
  value: string | null;
  onChange: (cycleId: string) => void;
  /** Wraps the control in a card with a label; the bare select is for use inside a form. */
  standalone?: boolean;
  label?: string;
  className?: string;
  /**
   * Adds an "All cycles" option and stops the auto-default, for screens whose reads are
   * cycle-optional — the HR review queue is cross-cycle by nature.
   */
  allowAll?: boolean;
}

export function CycleSelect({
  value,
  onChange,
  standalone = true,
  label = 'Appraisal cycle',
  className,
  allowAll = false,
}: CycleSelectProps) {
  const { data, isLoading } = useAppraisalCycles();

  const cycles = useMemo(() => {
    // Newest first, and an open cycle ahead of a closed one from the same year — the
    // cycle someone wants is almost always the one currently running.
    return [...(data ?? [])].sort((a, b) => {
      const openDelta = Number(OPEN_STATUSES.has(b.status)) - Number(OPEN_STATUSES.has(a.status));
      if (openDelta !== 0) return openDelta;
      if (b.year !== a.year) return b.year - a.year;
      return a.cycleCode.localeCompare(b.cycleCode);
    });
  }, [data]);

  // Default to the first cycle so the screen has something to show without a click. Skipped
  // when "All cycles" is available, since empty already means something there.
  useEffect(() => {
    if (!allowAll && !value && cycles.length > 0) onChange(cycles[0].id);
  }, [allowAll, value, cycles, onChange]);

  const placeholder = isLoading
    ? 'Loading cycles…'
    : cycles.length === 0
      ? 'No appraisal cycles yet'
      : 'Select a cycle…';

  const control = (
    <Select
      value={value || (allowAll ? ALL : '')}
      onValueChange={(next) => onChange(next === ALL ? '' : next)}
      disabled={cycles.length === 0}
    >
      <SelectTrigger id="cycleId" className={className}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {allowAll && <SelectItem value={ALL}>All cycles</SelectItem>}
        {cycles.map((c) => (
          <SelectItem key={c.id} value={c.id}>
            {cycleLabel(c)}
            {OPEN_STATUSES.has(c.status) ? ' — open' : ''}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );

  if (!standalone) return control;

  return (
    <Card>
      <CardContent className="flex flex-wrap items-end gap-4 p-4">
        <div className="min-w-[280px] flex-1 space-y-2">
          <Label htmlFor="cycleId" className="flex items-center gap-2">
            <CalendarRange className="h-4 w-4 text-muted-foreground" />
            {label}
          </Label>
          {control}
        </div>
      </CardContent>
    </Card>
  );
}
