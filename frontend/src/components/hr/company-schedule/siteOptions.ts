import { useQuery } from '@tanstack/react-query';
import { locationService } from '@/services/hr/location.service';
import { locationLevelService } from '@/services/hr/location-level.service';
import type { Location, LocationLevel } from '@/types/hr/location';

/**
 * Turns the location tree into options for a "Site" picker.
 *
 * ⚠ **`locationService.getAll()` returns the whole tree, not just sites.** On the TDC seed that is
 * Ghana (Country) and Greater Accra / Volta (Region) sitting alongside Tema Head Office, Ashaiman
 * Market, Ho and the rest.
 *
 * **Company-schedule lane 5a (C-16, the user's ruling): only the places staff can be placed.** A closure's or an event's
 * site matches employees assigned to that EXACT location (`HrAudienceResolver`, `HrClosureCalendar`), so a region chosen
 * as the "site" reaches nobody. The picker offers active locations whose level allows employee assignment
 * (`LocationLevel.AllowsEmployeeAssignment` — the per-tenant answer to "which level is a site"; on the TDC seed, Site /
 * Office). Two exceptions keep it honest:
 * - `keepId`, the record's current location, is always offered — an edit must show what is saved, even a region;
 * - a tenant with NO level marked assignable (the flag defaults to off) gets the whole tree, rather than nothing.
 * The level stays in every label.
 */
export function siteOptions(
  locations: Location[] | undefined,
  levels?: LocationLevel[] | undefined,
  keepId?: string | null,
) {
  const assignable = new Set((levels ?? []).filter((l) => l.allowsEmployeeAssignment).map((l) => l.id));
  const filtering = assignable.size > 0;
  return [...(locations ?? [])]
    .filter((l) => l.id === keepId || !filtering || (l.isActive && assignable.has(l.locationLevelId)))
    .sort((a, b) => (a.levelName ?? '').localeCompare(b.levelName ?? '') || a.name.localeCompare(b.name))
    .map((l) => ({
      value: l.id,
      label: l.levelName ? `${l.name} · ${l.levelName}` : l.name,
    }));
}

/**
 * The site picker's options and whether they are still loading — the locations and their levels, read once and shared
 * by the closure, event and room forms (lane 5a, C-16). `optionsFor(keepId)` builds the list for a form whose current
 * value must stay offered. A failed level read filters nothing.
 */
export function useSiteOptions() {
  const locations = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });
  const levels = useQuery({
    queryKey: ['hr', 'location-levels', 'all'],
    queryFn: () => locationLevelService.getAll(),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  return {
    optionsFor: (keepId?: string | null) => siteOptions(locations.data, levels.data, keepId),
    isLoading: locations.isLoading || (levels.isLoading && !levels.isError),
  };
}
