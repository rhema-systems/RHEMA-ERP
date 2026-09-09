'use client';

import { useEffect, useMemo, useState } from 'react';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

/** Radix Select refuses an empty-string item value, so "none" needs a sentinel. */
export const PICKER_NONE = '__none__';

/** A tier of a hierarchy: an organization level or a location level. */
export interface PickerLevel {
  id: string;
  name: string;
  levelNumber: number;
  structureId: string;
  isActive: boolean;
}

/** A node at a tier: an organization unit or a location. */
export interface PickerItem {
  id: string;
  name: string;
  code?: string | null;
  levelId: string;
  structureId: string;
  parentId?: string | null;
  /** `/ancestor/…/self`, for excluding a subtree. */
  path?: string | null;
  isActive: boolean;
}

/**
 * Which tiers the picker may offer. Every bound is optional; with none set it is simply
 * level → item over the whole tenant.
 */
export interface CascadingPickerBounds {
  /** Only tiers of this structure. */
  structureId?: string;
  /** Tiers numbered at or below this — the "parent of a unit at N" case passes N − 1. */
  maxLevelNumber?: number;
  /** Tiers numbered at or above this — the "units at or under this tier" case. */
  minLevelNumber?: number;
  /** Exactly this tier — locations, whose parent must be exactly one level up. */
  exactLevelNumber?: number;
}

export interface CascadingPickerProps extends CascadingPickerBounds {
  levels: PickerLevel[];
  items: PickerItem[];
  loading?: boolean;
  /** The chosen item's id, or '' for none. */
  value: string;
  onChange: (id: string, item: PickerItem | null) => void;
  /** Fired when the user changes the tier — for a form that stores the level as well as the item. */
  onLevelChange?: (levelId: string, level: PickerLevel | null) => void;
  /** Offer a "none" row with this label. Without it the picker is a required choice. */
  allowNone?: string;
  /** Never offer these ids (the current parent in a move dialog, say). */
  excludeIds?: string[];
  /** Never offer this id or anything beneath it — a unit being re-parented. */
  excludeSubtreeOf?: string;
  /** Offer inactive items too. The current value is always offered, active or not. */
  includeInactive?: boolean;
  disabled?: boolean;
  levelLabel: string;
  itemLabel: string;
  levelPlaceholder?: string;
  itemPlaceholder?: string;
  /** Shown under the level select when the bounds leave nothing to offer. */
  noLevelsMessage?: string;
  /** Shown inside the item list when the chosen tier has nothing to offer. */
  noItemsMessage?: string;
  hint?: string;
  error?: string;
  /** Prefix for the two element ids, so a page with two pickers keeps its labels attached. */
  idPrefix?: string;
  /** Show the item's code beside its name. */
  showCode?: boolean;
  className?: string;
}

/**
 * The level → item cascade that every unit and location picker in HR is built on.
 *
 * One implementation, deliberately. Demo feedback round 2 (O-1, O-5, O-6) asked for the parent
 * of a unit to be chosen by "pick the level, then the unit at that level", and the survey found
 * about forty screens each building their own flat unit dropdown. `OrganizationScopeFields` had
 * the cascade already, but only as a scope FILTER; this is the same mechanics generalised into a
 * required-choice picker with bounds, and `OrganizationUnitPicker` / `LocationPicker` are the two
 * thin wrappers that feed it.
 *
 * The rules it restates so the user cannot choose what the server will refuse:
 * - the level list honours the bounds (structure, max / min / exact tier), sorted by tier;
 * - the item list is the chosen level's members, minus the exclusions;
 * - changing the level clears an item from another level;
 * - on edit the level is derived from the current value, so the user sees where it sits.
 *
 * ⚠ The current value is ALWAYS offered, even when the bounds or an inactive flag would hide it.
 * An edit form must show what is stored; hiding it would make the select read as blank and the
 * next save would silently change the record.
 *
 * ⚠ `excludeSubtreeOf` walks `parentId`, not `path`. Measured on DEFAULT (lane B1): every one of
 * the 41 seeded units and 11 seeded locations has an EMPTY path — the seeders never wrote one —
 * so a path-based test would offer a unit its own descendants. `path` is consulted as well, for
 * rows that carry one, but the parent walk is what makes the exclusion true on live data.
 */
export function CascadingPicker({
  levels,
  items,
  loading,
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
  levelLabel,
  itemLabel,
  levelPlaceholder = 'Select a level',
  itemPlaceholder,
  noLevelsMessage,
  noItemsMessage = 'Nothing at this level.',
  hint,
  error,
  idPrefix = 'picker',
  showCode,
  className,
}: CascadingPickerProps) {
  const current = useMemo(() => items.find((i) => i.id === value) ?? null, [items, value]);
  const [levelId, setLevelId] = useState<string>(current?.levelId ?? '');

  // On edit (or after the lists load) the tier is where the current value sits.
  useEffect(() => {
    if (current && current.levelId !== levelId) setLevelId(current.levelId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [current?.id, current?.levelId]);

  const levelOptions = useMemo(
    () =>
      levels
        .filter((l) => {
          if (l.id === current?.levelId) return true;
          if (structureId && l.structureId !== structureId) return false;
          if (exactLevelNumber !== undefined && l.levelNumber !== exactLevelNumber) return false;
          if (maxLevelNumber !== undefined && l.levelNumber > maxLevelNumber) return false;
          if (minLevelNumber !== undefined && l.levelNumber < minLevelNumber) return false;
          return l.isActive;
        })
        .sort((a, b) => a.levelNumber - b.levelNumber || a.name.localeCompare(b.name)),
    [levels, current?.levelId, structureId, exactLevelNumber, maxLevelNumber, minLevelNumber],
  );

  // One possible tier is not a choice; pick it so the user only has the item to choose. The level
  // select stays on screen so a location form and a unit form look and behave alike.
  useEffect(() => {
    if (!levelId && levelOptions.length === 1) setLevelId(levelOptions[0].id);
  }, [levelId, levelOptions]);

  // A tier the bounds no longer allow (the caller narrowed them) is dropped, and the value with it.
  useEffect(() => {
    if (levelId && !loading && levels.length && !levelOptions.some((l) => l.id === levelId)) {
      setLevelId('');
      if (value) onChange('', null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [levelOptions, levelId, loading, levels.length]);

  // The subtree under `excludeSubtreeOf`, by walking parent ids (see the note above).
  const excludedSubtree = useMemo(() => {
    const out = new Set<string>();
    if (!excludeSubtreeOf) return out;
    out.add(excludeSubtreeOf);
    const byParent = new Map<string, string[]>();
    for (const i of items) {
      if (!i.parentId) continue;
      const list = byParent.get(i.parentId) ?? [];
      list.push(i.id);
      byParent.set(i.parentId, list);
    }
    const queue = [excludeSubtreeOf];
    while (queue.length) {
      const id = queue.shift()!;
      for (const child of byParent.get(id) ?? []) {
        if (!out.has(child)) {
          out.add(child);
          queue.push(child);
        }
      }
    }
    for (const i of items) {
      if ((i.path ?? '').split('/').includes(excludeSubtreeOf)) out.add(i.id);
    }
    return out;
  }, [items, excludeSubtreeOf]);

  const itemOptions = useMemo(
    () =>
      items
        .filter((i) => {
          if (i.id === value) return true;
          if (!levelId || i.levelId !== levelId) return false;
          if (!includeInactive && !i.isActive) return false;
          if (structureId && i.structureId !== structureId) return false;
          if (excludeIds?.includes(i.id)) return false;
          if (excludedSubtree.has(i.id)) return false;
          return true;
        })
        .sort((a, b) => a.name.localeCompare(b.name)),
    [items, value, levelId, includeInactive, structureId, excludeIds, excludedSubtree],
  );

  const handleLevel = (next: string) => {
    setLevelId(next);
    onLevelChange?.(next, levels.find((l) => l.id === next) ?? null);
    // An item from the old tier would contradict the new one.
    if (value && current?.levelId !== next) onChange('', null);
  };

  const handleItem = (next: string) => {
    const id = next === PICKER_NONE ? '' : next;
    onChange(id, items.find((i) => i.id === id) ?? null);
  };

  const levelDisabled = disabled || loading || levelOptions.length === 0;
  const itemDisabled = disabled || loading || (!levelId && !allowNone);

  return (
    <div className={className ?? 'grid gap-4 sm:grid-cols-2'}>
      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-level`}>{levelLabel}</Label>
        <Select value={levelId || undefined} onValueChange={handleLevel} disabled={levelDisabled}>
          <SelectTrigger id={`${idPrefix}-level`}>
            <SelectValue placeholder={loading ? 'Loading…' : levelPlaceholder} />
          </SelectTrigger>
          <SelectContent>
            {levelOptions.map((l) => (
              <SelectItem key={l.id} value={l.id}>
                {l.name} (L{l.levelNumber})
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {!loading && levelOptions.length === 0 && noLevelsMessage && (
          <p className="text-xs text-muted-foreground">{noLevelsMessage}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-item`}>{itemLabel}</Label>
        <Select
          value={value || (allowNone ? PICKER_NONE : undefined)}
          onValueChange={handleItem}
          disabled={itemDisabled}
        >
          <SelectTrigger id={`${idPrefix}-item`}>
            <SelectValue
              placeholder={
                loading
                  ? 'Loading…'
                  : (itemPlaceholder ?? (levelId ? `Select ${itemLabel.toLowerCase()}` : 'Select a level first'))
              }
            />
          </SelectTrigger>
          <SelectContent>
            {allowNone && <SelectItem value={PICKER_NONE}>{allowNone}</SelectItem>}
            {!levelId ? (
              <div className="px-2 py-1.5 text-sm text-muted-foreground">Select a level first.</div>
            ) : itemOptions.length === 0 ? (
              <div className="px-2 py-1.5 text-sm text-muted-foreground">{noItemsMessage}</div>
            ) : (
              itemOptions.map((i) => (
                <SelectItem key={i.id} value={i.id}>
                  {i.name}
                  {showCode && i.code ? ` · ${i.code}` : ''}
                  {i.isActive ? '' : ' (inactive)'}
                </SelectItem>
              ))
            )}
          </SelectContent>
        </Select>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>

      {hint && <p className="text-xs text-muted-foreground sm:col-span-2">{hint}</p>}
    </div>
  );
}
