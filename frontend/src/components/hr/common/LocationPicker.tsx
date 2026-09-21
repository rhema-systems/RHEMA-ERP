'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { locationLevelService } from '@/services/hr/location-level.service';
import { locationService } from '@/services/hr/location.service';
import type { LocationLevel, LocationSummary } from '@/types/hr/location';
import {
  CascadingPicker,
  type CascadingPickerBounds,
  type PickerItem,
  type PickerLevel,
} from './CascadingPicker';

export interface LocationPickerProps extends CascadingPickerBounds {
  /** The chosen location's id, or '' for none. */
  value: string;
  onChange: (locationId: string, location: LocationSummary | null) => void;
  onLevelChange?: (levelId: string, level: LocationLevel | null) => void;
  allowNone?: string;
  excludeIds?: string[];
  /** The location being re-parented: it and its whole subtree are never offered. */
  excludeSubtreeOf?: string;
  includeInactive?: boolean;
  disabled?: boolean;
  levelLabel?: string;
  locationLabel?: string;
  levelPlaceholder?: string;
  locationPlaceholder?: string;
  noLevelsMessage?: string;
  noLocationsMessage?: string;
  hint?: string;
  error?: string;
  idPrefix?: string;
  showCode?: boolean;
  className?: string;
}

/**
 * Level → location, the location twin of `OrganizationUnitPicker` over the same
 * `CascadingPicker`.
 *
 * Demo feedback round 2, O-5. The one difference from units is the server rule (X-11): a
 * location's parent must sit EXACTLY one level up, so the location form passes
 * `exactLevelNumber` and the level list holds a single entry. It is still rendered — the picker
 * selects it for the user — so the two placement screens look and behave alike.
 */
export function LocationPicker({
  value,
  onChange,
  onLevelChange,
  allowNone,
  excludeIds,
  excludeSubtreeOf,
  includeInactive,
  disabled,
  structureId,
  maxLevelNumber,
  minLevelNumber,
  exactLevelNumber,
  levelLabel = 'Level',
  locationLabel = 'Location',
  levelPlaceholder,
  locationPlaceholder,
  noLevelsMessage,
  noLocationsMessage = 'No locations at this level.',
  hint,
  error,
  idPrefix = 'location',
  showCode,
  className,
}: LocationPickerProps) {
  const { data: levels = [], isLoading: levelsLoading } = useQuery({
    queryKey: ['hr', 'location-levels'],
    queryFn: () => locationLevelService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: locations = [], isLoading: locationsLoading } = useQuery({
    queryKey: ['hr', 'locations', 'summary'],
    queryFn: () => locationService.getSummary(),
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

  const locationRows = useMemo<PickerItem[]>(
    () =>
      locations.map((l) => ({
        id: l.id,
        name: l.name,
        code: l.code,
        levelId: l.locationLevelId,
        structureId: l.structureId,
        parentId: l.parentLocationId,
        path: l.path,
        isActive: l.isActive,
      })),
    [locations],
  );

  return (
    <CascadingPicker
      levels={levelRows}
      items={locationRows}
      loading={levelsLoading || locationsLoading}
      value={value}
      onChange={(id) => onChange(id, locations.find((l) => l.id === id) ?? null)}
      onLevelChange={
        onLevelChange
          ? (id) => onLevelChange(id, levels.find((l) => l.id === id) ?? null)
          : undefined
      }
      allowNone={allowNone}
      excludeIds={excludeIds}
      excludeSubtreeOf={excludeSubtreeOf}
      includeInactive={includeInactive}
      disabled={disabled}
      structureId={structureId}
      maxLevelNumber={maxLevelNumber}
      minLevelNumber={minLevelNumber}
      exactLevelNumber={exactLevelNumber}
      levelLabel={levelLabel}
      itemLabel={locationLabel}
      levelPlaceholder={levelPlaceholder}
      itemPlaceholder={locationPlaceholder}
      noLevelsMessage={noLevelsMessage}
      noItemsMessage={noLocationsMessage}
      hint={hint}
      error={error}
      idPrefix={idPrefix}
      showCode={showCode}
      className={className}
    />
  );
}
