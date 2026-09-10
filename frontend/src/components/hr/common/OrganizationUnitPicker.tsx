'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationLevel, OrganizationUnitSummary } from '@/types/hr/organization';
import {
  CascadingPicker,
  type CascadingPickerBounds,
  type PickerItem,
  type PickerLevel,
} from './CascadingPicker';

export interface OrganizationUnitPickerProps extends CascadingPickerBounds {
  /** The chosen unit's id, or '' for none. */
  value: string;
  onChange: (unitId: string, unit: OrganizationUnitSummary | null) => void;
  /** Fired when the user changes the level — for a form that stores the level too (positions). */
  onLevelChange?: (levelId: string, level: OrganizationLevel | null) => void;
  /** Offer a "none" row with this label; without it the unit is a required choice. */
  allowNone?: string;
  /** Offer a "no level" row with this label — for a scope filter where "any level" is a state. */
  allowNoLevel?: string;
  /** The level to show when no unit is chosen (a level-only scope on edit). */
  initialLevelId?: string;
  excludeIds?: string[];
  /** The unit being re-parented: it and its whole subtree are never offered. */
  excludeSubtreeOf?: string;
  includeInactive?: boolean;
  disabled?: boolean;
  levelLabel?: string;
  unitLabel?: string;
  levelPlaceholder?: string;
  unitPlaceholder?: string;
  noLevelsMessage?: string;
  noUnitsMessage?: string;
  hint?: string;
  error?: string;
  idPrefix?: string;
  showCode?: boolean;
  className?: string;
}

/**
 * Level → unit, the way the organisation is actually navigated.
 *
 * Demo feedback round 2, O-1: "parent selection as a cascading dropdown — pick the parent's level
 * number, then the unit at that level; a child at level 4 may have its parent at level 1, 2 or 3."
 * The server has always enforced exactly that (a parent must sit at a strictly higher tier,
 * skipping allowed); the forms listed every unit flat and let the user find out on save.
 *
 * The lists are fetched here, under the same query keys the organisation pages use, so a unit
 * created on one screen appears in the picker on the next without a reload. Callers therefore
 * pass bounds and a value, not data.
 *
 * Common shapes:
 * - parent of a unit at tier N: `structureId` + `maxLevelNumber={N - 1}` + `excludeSubtreeOf`;
 * - any unit (owning unit of a team, scope of a record): no bounds, usually `allowNone`;
 * - a position's unit: no bounds, `onLevelChange` so the form stores the level id as well.
 */
export function OrganizationUnitPicker({
  value,
  onChange,
  onLevelChange,
  allowNone,
  allowNoLevel,
  initialLevelId,
  excludeIds,
  excludeSubtreeOf,
  includeInactive,
  disabled,
  structureId,
  maxLevelNumber,
  minLevelNumber,
  exactLevelNumber,
  levelLabel = 'Level',
  unitLabel = 'Organization unit',
  levelPlaceholder,
  unitPlaceholder,
  noLevelsMessage,
  noUnitsMessage = 'No units at this level.',
  hint,
  error,
  idPrefix = 'org-unit',
  showCode,
  className,
}: OrganizationUnitPickerProps) {
  const { data: levels = [], isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: units = [], isLoading: unitsLoading } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
    staleTime: 5 * 60 * 1000,
  });

  const levelRows = useMemo<PickerLevel[]>(
    () =>
      levels.map((l) => ({
        id: l.id,
        name: l.name,
        levelNumber: l.levelNumber,
        structureId: l.structureId,
        isActive: l.isActive,
      })),
    [levels],
  );

  // `levelNumber`, `structureId` and `path` reached the summary read in round 2 (X-10); before that
  // the cascade could not rank units without a second fetch of the levels.
  const unitRows = useMemo<PickerItem[]>(
    () =>
      units.map((u) => ({
        id: u.id,
        name: u.name,
        code: u.code,
        levelId: u.organizationLevelId,
        structureId: u.structureId,
        parentId: u.parentUnitId,
        path: u.path,
        isActive: u.isActive,
      })),
    [units],
  );

  return (
    <CascadingPicker
      levels={levelRows}
      items={unitRows}
      loading={levelsLoading || unitsLoading}
      value={value}
      onChange={(id) => onChange(id, units.find((u) => u.id === id) ?? null)}
      onLevelChange={
        onLevelChange
          ? (id) => onLevelChange(id, levels.find((l) => l.id === id) ?? null)
          : undefined
      }
      allowNone={allowNone}
      allowNoLevel={allowNoLevel}
      initialLevelId={initialLevelId}
      excludeIds={excludeIds}
      excludeSubtreeOf={excludeSubtreeOf}
      includeInactive={includeInactive}
      disabled={disabled}
      structureId={structureId}
      maxLevelNumber={maxLevelNumber}
      minLevelNumber={minLevelNumber}
      exactLevelNumber={exactLevelNumber}
      levelLabel={levelLabel}
      itemLabel={unitLabel}
      levelPlaceholder={levelPlaceholder}
      itemPlaceholder={unitPlaceholder}
      noLevelsMessage={noLevelsMessage}
      noItemsMessage={noUnitsMessage}
      hint={hint}
      error={error}
      idPrefix={idPrefix}
      showCode={showCode}
      className={className}
    />
  );
}
