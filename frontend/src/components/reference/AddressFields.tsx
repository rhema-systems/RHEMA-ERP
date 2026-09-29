'use client';

/**
 * The shared administrative-address control: a country, then one cascading dropdown per tier of
 * whatever scheme that country uses.
 *
 * ⚠ **There is no per-country code in here, and there must never be.** The labels come from
 * `GeoLevel.name` on the server, so a Ghanaian employee's form reads Region / District / Town /
 * Community and a Nigerian one reads State / LGA / Ward, from this one component. If you find
 * yourself adding `if (country === 'GH')`, the scheme is wrong, not the component.
 *
 * ⚠ **It emits ONE id — the deepest tier the user actually picked.** Not one per tier. The
 * ancestors are recoverable from the tree, which is what lets a scheme gain a fifth tier without
 * touching this file or any table that stores the answer.
 *
 * ⚠ **A country with no scheme is a normal state, not an error.** The endpoint answers 204 and this
 * falls back to the free-text fields the caller supplies as `fallback`. Most of the world has no
 * scheme loaded, and an address form that refused to render for them would be worse than useless.
 *
 * See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
 */

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { geographyReader } from '@/services/reference/geography.service';
import { countryService } from '@/services/hr/country.service';
import type { GeoAreaOption } from '@/types/reference/geography';

/** Radix Select cannot hold an empty string as a value, so "not chosen" needs a sentinel. */
const NONE = '__none__';

export interface AddressFieldsProps {
  /** Selected country, or '' for none. */
  countryId: string;
  onCountryChange: (countryId: string) => void;

  /** The deepest area chosen, or '' for none. */
  geoAreaId: string;
  onGeoAreaChange: (geoAreaId: string) => void;

  /**
   * Rendered under the cascade — the free-text address lines the tree does not replace (street,
   * postal code, digital address), and the City/Region inputs when no scheme exists.
   */
  fallback?: (schemeLoaded: boolean) => React.ReactNode;

  disabled?: boolean;
  className?: string;

  /**
   * Read the anonymous careers catalogue for this tenant instead of the internal route.
   *
   * ⚠ Set this on candidate-facing pages and nowhere else. `api/reference/geo` is InternalOnly,
   * which is a blocklist excluding the Candidate role — so without it the cascade is refused for
   * every candidate and, because a failed query renders as an empty list, silently shows "this
   * country has no scheme" rather than an error. Round 4, lane A.
   */
  publicTenantId?: string | null;

  /** Pre-fetched country list, for a page that already has one (the careers portal does). */
  countryOptions?: { id: string; name: string }[] | null;
}

export function AddressFields({
  countryId,
  onCountryChange,
  geoAreaId,
  onGeoAreaChange,
  fallback,
  disabled = false,
  className,
  publicTenantId,
  countryOptions,
}: AddressFieldsProps) {
  // One reader for the whole component and its tiers — internal by default, the anonymous careers
  // catalogue when a public tenant is named. See geographyReader for why this split exists.
  const geo = useMemo(() => geographyReader(publicTenantId), [publicTenantId]);
  /**
   * One entry per tier: the area chosen at that depth, or '' if the user stopped above it.
   * Kept as its own state rather than derived, because the user's *intent* — "Greater Accra, and I
   * have not said which district" — is not recoverable from a single id.
   */
  const [chain, setChain] = useState<string[]>([]);

  const { data: fetchedCountries = [] } = useQuery({
    queryKey: ['reference', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
    // The careers pages already hold a public country list and cannot read the internal one.
    enabled: !countryOptions,
  });
  const countries = countryOptions ?? fetchedCountries;

  const { data: scheme, isLoading: schemeLoading } = useQuery({
    queryKey: ['reference', 'geo', 'scheme-for-country', countryId, publicTenantId ?? 'internal'],
    queryFn: () => geo.getSchemeForCountry(countryId),
    enabled: !!countryId,
  });

  const levels = useMemo(() => scheme?.levels ?? [], [scheme]);

  /**
   * Re-open an existing record: one id comes back from the API and the cascade has to be rebuilt
   * from it. The ancestors endpoint returns the whole chain broadest-first, which is exactly the
   * shape `chain` holds — this is why that endpoint exists.
   */
  useEffect(() => {
    let cancelled = false;

    if (!geoAreaId) {
      // Only clear when the caller cleared it; typing in the cascade sets both together.
      setChain((current) => (current.some(Boolean) ? [] : current));
      return;
    }

    // Already reflected — do not re-fetch on every keystroke elsewhere in the form.
    if (chain.filter(Boolean).at(-1) === geoAreaId) return;

    geo
      .getAncestors(geoAreaId)
      .then((ancestors) => {
        if (!cancelled) setChain(ancestors.map((a) => a.id));
      })
      .catch(() => {
        // A stale or cross-tenant id: leave the cascade empty rather than wedging the form. The
        // free-text snapshot still shows what the record says.
        if (!cancelled) setChain([]);
      });

    return () => {
      cancelled = true;
    };
    // `chain` is deliberately absent: this effect reconciles TO the incoming id, and including it
    // would re-run the effect from its own result.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [geoAreaId]);

  // Changing country invalidates the whole chain — a Ghanaian district is not a Nigerian one.
  function handleCountryChange(next: string) {
    onCountryChange(next);
    setChain([]);
    onGeoAreaChange('');
  }

  function handleTierChange(tierIndex: number, value: string) {
    const picked = value === NONE ? '' : value;

    // Everything below the changed tier is now meaningless — picking a different district cannot
    // leave the old district's town standing underneath it.
    const next = [...chain.slice(0, tierIndex), picked];
    setChain(next);

    // The emitted id is the DEEPEST tier actually chosen, which may be above the one just touched
    // if the user cleared it.
    const deepest = [...next].reverse().find(Boolean) ?? '';
    onGeoAreaChange(deepest);
  }

  const schemeLoaded = !!scheme && levels.length > 0;

  return (
    <div className={className}>
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="space-y-2">
          <Label htmlFor="countryId">Country</Label>
          <Select
            value={countryId || NONE}
            onValueChange={handleCountryChange}
            disabled={disabled}
          >
            <SelectTrigger id="countryId">
              <SelectValue placeholder="Select a country" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Not stated</SelectItem>
              {countries.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {schemeLoaded &&
          levels.map((level, index) => (
            <TierSelect
              key={level.id}
              levelId={level.id}
              // The tier's own name is the label. This is the mechanism, not a convenience.
              label={level.name}
              required={level.isRequiredInAddress}
              parentId={index === 0 ? undefined : chain[index - 1]}
              // A tier is unreachable until the one above it has been answered.
              disabled={disabled || (index > 0 && !chain[index - 1])}
              value={chain[index] ?? ''}
              onChange={(value) => handleTierChange(index, value)}
              geo={geo}
              readerKey={publicTenantId ?? 'internal'}
            />
          ))}
      </div>

      {countryId && !schemeLoading && !schemeLoaded && (
        <p className="text-muted-foreground mt-2 text-xs">
          No administrative divisions are loaded for this country, so the address below is recorded
          as text. An administrator can add them under Administration → Reference Data → Geography.
        </p>
      )}

      {fallback?.(schemeLoaded)}
    </div>
  );
}

function TierSelect({
  levelId,
  label,
  required,
  parentId,
  disabled,
  value,
  onChange,
  geo,
  readerKey,
}: {
  levelId: string;
  label: string;
  required: boolean;
  parentId?: string;
  disabled: boolean;
  value: string;
  onChange: (value: string) => void;
  /** The parent's reader, so every tier speaks to the same route. */
  geo: ReturnType<typeof geographyReader>;
  /** Distinguishes the internal and public caches — the same tier, read two ways, is two answers. */
  readerKey: string;
}) {
  const { data: options = [], isLoading } = useQuery({
    queryKey: ['reference', 'geo', 'options', levelId, parentId ?? 'root', readerKey],
    queryFn: () => geo.getAreaOptions(levelId, parentId),
    // The root tier needs no parent; every tier below one does, and asking without it would
    // return the whole tier flattened.
    enabled: !disabled,
  });

  return (
    <div className="space-y-2">
      <Label htmlFor={`geo-${levelId}`}>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </Label>
      <Select
        value={value || NONE}
        onValueChange={onChange}
        disabled={disabled || isLoading}
      >
        <SelectTrigger id={`geo-${levelId}`}>
          <SelectValue placeholder={disabled ? '—' : `Select ${label.toLowerCase()}`} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NONE}>Not stated</SelectItem>
          {options.map((o: GeoAreaOption) => (
            <SelectItem key={o.id} value={o.id}>
              {o.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
