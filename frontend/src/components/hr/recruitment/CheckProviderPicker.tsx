'use client';

import { useQuery } from '@tanstack/react-query';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SupplierPicker } from '@/components/hr/common/SupplierPicker';
import { preEmploymentCheckService } from '@/services/hr/offers.service';
import type { PreEmploymentCheckType } from '@/types/hr/offers';

const NONE = '__none__';
const OTHER = '__other__';
const TYPED = '__typed__';

/**
 * The provider of one pre-employment check (round 3, lane G; register row R-7; decision D-14).
 *
 * The check type narrows the list to the suppliers set up as providing THAT check (the
 * `providers` table on the check service). "Another supplier" opens the whole supplier register
 * through HR's read door; "Not a supplier on file" keeps a typed name. Whatever is picked, the
 * server mirrors the supplier's name into the snapshot column — so the name shown on a completed
 * check is the name the provider had when the check was done.
 */
export function CheckProviderPicker({
  checkType,
  supplierId,
  name,
  onChange,
  idPrefix = 'provider',
}: {
  checkType: PreEmploymentCheckType;
  supplierId: string | null;
  name: string;
  onChange: (next: { supplierId: string | null; name: string }) => void;
  idPrefix?: string;
}) {
  const providers = useQuery({
    queryKey: ['hr', 'pre-employment-providers', checkType],
    queryFn: () => preEmploymentCheckService.getProviders(checkType),
  });
  const rows = providers.data ?? [];
  const known = supplierId ? rows.find((p) => p.supplierId === supplierId) : undefined;
  // What the dropdown shows: a provider for this check, another supplier, or a typed name.
  const mode = supplierId ? (known ? supplierId : OTHER) : name ? TYPED : NONE;

  return (
    <div className="space-y-2">
      <Label htmlFor={`${idPrefix}-choice`}>Service provider</Label>
      <Select
        value={mode}
        onValueChange={(v) => {
          if (v === NONE) onChange({ supplierId: null, name: '' });
          else if (v === TYPED) onChange({ supplierId: null, name });
          else if (v === OTHER) onChange({ supplierId: null, name: '' });
          else {
            const row = rows.find((p) => p.supplierId === v);
            onChange({ supplierId: v, name: row?.supplierName ?? '' });
          }
          if (v === OTHER) onChange({ supplierId: '', name: '' });
        }}
      >
        <SelectTrigger id={`${idPrefix}-choice`}>
          <SelectValue placeholder="Choose the provider" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NONE}>None yet</SelectItem>
          {rows.map((p) => (
            <SelectItem key={p.id} value={p.supplierId}>
              {p.supplierName}
              {p.notes ? ` — ${p.notes}` : ''}
            </SelectItem>
          ))}
          <SelectItem value={OTHER}>Another supplier from the register…</SelectItem>
          <SelectItem value={TYPED}>Not a supplier on file (type the name)</SelectItem>
        </SelectContent>
      </Select>
      {rows.length === 0 && !providers.isLoading && (
        <p className="text-xs text-muted-foreground">
          No supplier is set up as providing this check yet — set them up on the check templates page.
        </p>
      )}
      {mode === OTHER && (
        <SupplierPicker
          value={supplierId || null}
          onChange={(id, supplier) => onChange({ supplierId: id, name: supplier?.name ?? '' })}
        />
      )}
      {mode === TYPED && (
        <Input
          id={`${idPrefix}-name`}
          value={name}
          placeholder="Provider's name"
          onChange={(e) => onChange({ supplierId: null, name: e.target.value })}
        />
      )}
      {supplierId && known && (
        <p className="text-xs text-muted-foreground">Stored as {known.supplierName} ({known.supplierCode}).</p>
      )}
    </div>
  );
}
