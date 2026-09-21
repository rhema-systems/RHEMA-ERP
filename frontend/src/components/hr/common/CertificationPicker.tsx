'use client';

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
import { referenceDimensionService } from '@/services/hr/lookup.service';
import { certificationService } from '@/services/hr/certification.service';
import type { Certification } from '@/types/hr/certification';

const NONE = '__none__';

export interface CertificationPickerProps {
  /** The chosen certification's id, or '' for none. */
  value: string;
  onChange: (certificationId: string, certification: Certification | null) => void;
  /** Offer a "none" row with this label; without it the certification is a required choice. */
  allowNone?: string;
  /** Never offer these ids — the rows already on the form. */
  excludeIds?: string[];
  disabled?: boolean;
  bodyLabel?: string;
  certificationLabel?: string;
  idPrefix?: string;
  error?: string;
  hint?: string;
  className?: string;
}

/**
 * Certifying body → certification, the cascade the demo feedback asked for twice (skills setup
 * bullets S-1 and S-2). The first select is the body catalogue; the second is what that body
 * issues, read from `certifying-bodies/{id}/certifications`.
 *
 * On edit the body is derived from the current value, so the user sees who issued it. The
 * current value is always offered, active or not — an edit form must show what is stored.
 */
export function CertificationPicker({
  value,
  onChange,
  allowNone,
  excludeIds,
  disabled,
  bodyLabel = 'Certifying body',
  certificationLabel = 'Certification',
  idPrefix = 'certification',
  error,
  hint,
  className,
}: CertificationPickerProps) {
  const { data: bodies = [], isLoading: bodiesLoading } = useQuery({
    queryKey: ['hr', 'certifying-bodies', 'active'],
    queryFn: () => referenceDimensionService.getCertifyingBodies(true),
    staleTime: 5 * 60 * 1000,
  });

  // The whole catalogue once, so the body of a stored value can be derived without a round trip;
  // the second select still narrows to the chosen body.
  const { data: catalogue = [], isLoading: catalogueLoading } = useQuery({
    queryKey: ['hr', 'certifications', 'all'],
    queryFn: () => certificationService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const current = useMemo(() => catalogue.find((c) => c.id === value) ?? null, [catalogue, value]);
  const [bodyId, setBodyId] = useState<string>(current?.certifyingBodyId ?? '');

  useEffect(() => {
    if (current && current.certifyingBodyId !== bodyId) setBodyId(current.certifyingBodyId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [current?.id, current?.certifyingBodyId]);

  const bodyOptions = useMemo(
    () =>
      bodies
        .filter((b) => b.isActive || b.id === current?.certifyingBodyId)
        .sort((a, b) => a.name.localeCompare(b.name)),
    [bodies, current?.certifyingBodyId],
  );

  const certificationOptions = useMemo(
    () =>
      catalogue
        .filter((c) => {
          if (c.id === value) return true;
          if (!bodyId || c.certifyingBodyId !== bodyId) return false;
          if (!c.isActive) return false;
          if (excludeIds?.includes(c.id)) return false;
          return true;
        })
        .sort((a, b) => a.name.localeCompare(b.name)),
    [catalogue, value, bodyId, excludeIds],
  );

  const loading = bodiesLoading || catalogueLoading;

  return (
    <div className={className ?? 'grid gap-4 sm:grid-cols-2'}>
      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-body`}>{bodyLabel}</Label>
        <Select
          value={bodyId || undefined}
          disabled={disabled || loading}
          onValueChange={(next) => {
            setBodyId(next);
            if (value && current?.certifyingBodyId !== next) onChange('', null);
          }}
        >
          <SelectTrigger id={`${idPrefix}-body`}>
            <SelectValue placeholder={loading ? 'Loading…' : 'Choose the body'} />
          </SelectTrigger>
          <SelectContent>
            {bodyOptions.map((b) => (
              <SelectItem key={b.id} value={b.id}>
                {b.abbreviation ? `${b.name} (${b.abbreviation})` : b.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-certification`}>{certificationLabel}</Label>
        <Select
          value={value || (allowNone ? NONE : undefined)}
          disabled={disabled || loading || (!bodyId && !allowNone)}
          onValueChange={(next) => {
            const id = next === NONE ? '' : next;
            onChange(id, catalogue.find((c) => c.id === id) ?? null);
          }}
        >
          <SelectTrigger id={`${idPrefix}-certification`}>
            <SelectValue placeholder={bodyId ? 'Choose the certification' : 'Choose a body first'} />
          </SelectTrigger>
          <SelectContent>
            {allowNone && <SelectItem value={NONE}>{allowNone}</SelectItem>}
            {!bodyId ? (
              <div className="px-2 py-1.5 text-sm text-muted-foreground">Choose a body first.</div>
            ) : certificationOptions.length === 0 ? (
              <div className="px-2 py-1.5 text-sm text-muted-foreground">
                This body has no active certifications in the catalogue.
              </div>
            ) : (
              certificationOptions.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                  {c.code ? ` · ${c.code}` : ''}
                  {c.kind !== 'Certification' ? ` · ${c.kind}` : ''}
                  {c.isActive ? '' : ' (inactive)'}
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
