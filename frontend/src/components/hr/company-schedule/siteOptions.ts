import type { Location } from '@/types/hr/location';

/**
 * Turns the location tree into options for a "Site" picker.
 *
 * ⚠ **`locationService.getAll()` returns the whole tree, not just sites.** On the TDC seed that is
 * Ghana (Country) and Greater Accra / Volta (Region) sitting alongside Tema Head Office, Ashaiman
 * Market, Ho and the rest — so an unlabelled list invites someone to file a meeting room under
 * "Ghana". The level is appended to every option rather than filtering the list, because which
 * level counts as a "site" is a per-tenant question: the seeder marks it with `RequiresAddress`,
 * but nothing stops a tenant defining four levels or naming them differently. Showing the level is
 * true for any scheme; guessing which one is bookable is not.
 *
 * Deepest levels are listed last so the specific places sit at the bottom of the list, nearest the
 * cursor, rather than being buried under the countries.
 */
export function siteOptions(locations: Location[] | undefined) {
  return [...(locations ?? [])]
    .sort((a, b) => (a.levelName ?? '').localeCompare(b.levelName ?? '') || a.name.localeCompare(b.name))
    .map((l) => ({
      value: l.id,
      label: l.levelName ? `${l.name} · ${l.levelName}` : l.name,
    }));
}
