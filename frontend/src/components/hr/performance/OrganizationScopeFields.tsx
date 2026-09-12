'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';

/** Radix Select refuses an empty-string item value, so "no narrowing" needs a sentinel. */
const NONE = '__none__';

interface OrganizationScopeFieldsProps {
  levelId: string;
  unitId: string;
  onLevelChange: (value: string) => void;
  onUnitChange: (value: string) => void;
  /** Copy under the pair, when the caller wants to explain what the scope does. */
  hint?: string;
  disabled?: boolean;
}

/**
 * The level → unit narrowing pair used wherever an HR record is scoped to part of the org.
 *
 * The two are not independent. A unit belongs to a level, so choosing a level filters the units
 * on offer and changing it clears a now-inconsistent unit; choosing a unit is the *narrower*
 * statement and is what the server acts on when both are set.
 *
 * Both blank means "no narrowing" — for a calibration session that is the whole cycle, which is
 * a real choice for a small organisation and a mistake for a large one, so callers should say so
 * in `hint` rather than leaving it implied.
 */
export function OrganizationScopeFields({
  levelId,
  unitId,
  onLevelChange,
  onUnitChange,
  hint,
  disabled,
}: OrganizationScopeFieldsProps) {
  const { data: levels, isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'organization-levels'],
    queryFn: () => organizationLevelService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: units, isLoading: unitsLoading } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const unitOptions = useMemo(() => {
    const all = units ?? [];
    if (!levelId) return all;
    return all.filter((u) => u.organizationLevelId === levelId);
  }, [units, levelId]);

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="organizationLevelId">Organization level</Label>
          <Select
            value={levelId || NONE}
            onValueChange={(next) => {
              const value = next === NONE ? '' : next;
              onLevelChange(value);
              // A unit from the old level would contradict the new one.
              if (unitId) onUnitChange('');
            }}
            disabled={disabled || levelsLoading}
          >
            <SelectTrigger id="organizationLevelId">
              <SelectValue placeholder={levelsLoading ? 'Loading…' : 'Any level'} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Any level</SelectItem>
              {(levels ?? []).map((level) => (
                <SelectItem key={level.id} value={level.id}>
                  {level.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="organizationUnitId">Organization unit</Label>
          <Select
            value={unitId || NONE}
            onValueChange={(next) => onUnitChange(next === NONE ? '' : next)}
            disabled={disabled || unitsLoading}
          >
            <SelectTrigger id="organizationUnitId">
              <SelectValue placeholder={unitsLoading ? 'Loading…' : 'Any unit'} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Any unit</SelectItem>
              {unitOptions.map((unit) => (
                <SelectItem key={unit.id} value={unit.id}>
                  {unit.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}
