'use client';

import { useQuery } from '@tanstack/react-query';
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { hrCurrencyService, type HrCurrencyOption } from '@/services/hr/hr-currency.service';

/**
 * One currency dropdown for every HR screen (round 3, lane C2; register row R-3b).
 *
 * Replaces five ad-hoc selects that each fetched the list and rendered `CODE — Name` by hand — one
 * of which read `c.code` off Finance's `currencyCode` shape and rendered "undefined — undefined"
 * for a month. The list is Finance's: `api/hr/currencies` for a signed-in HR user, or the
 * anonymous `api/public/catalogue/currencies` (with the tenant header) for the careers form —
 * pass `options` to feed it from anywhere else.
 *
 * ⚠ The value is the ISO code (`GHS`), never the id — every consumer column is a code column.
 */
const NONE = '__none__';

export interface CurrencyPickerProps {
  value: string | null | undefined;
  onChange: (code: string) => void;
  /** Pre-fetched list; when omitted the HR-gated route is read. */
  options?: HrCurrencyOption[] | null;
  /** Read the anonymous public catalogue for this tenant instead of the HR route. */
  publicTenantId?: string | null;
  id?: string;
  disabled?: boolean;
  /** Offer a blank choice, labelled `emptyLabel`, that maps to ''. */
  allowEmpty?: boolean;
  emptyLabel?: string;
  placeholder?: string;
  className?: string;
}

const API_BASE = process.env.NEXT_PUBLIC_API_URL || '/api';

export function useCurrencyOptions(
  options?: HrCurrencyOption[] | null,
  publicTenantId?: string | null,
) {
  const fetched = useQuery({
    queryKey: ['hr', 'currencies', publicTenantId ? `public:${publicTenantId}` : 'hr'],
    queryFn: async (): Promise<HrCurrencyOption[]> => {
      if (publicTenantId) {
        const res = await fetch(`${API_BASE}/public/catalogue/currencies`, {
          headers: { 'X-Tenant-Id': publicTenantId },
        });
        if (!res.ok) throw new Error(`Could not read the currency list (${res.status})`);
        return (await res.json()) as HrCurrencyOption[];
      }
      return hrCurrencyService.getActive();
    },
    enabled: !options,
    staleTime: 5 * 60 * 1000,
  });
  return options ?? fetched.data ?? [];
}

export function currencyLabel(c: HrCurrencyOption) {
  return c.name && c.name !== c.code ? `${c.code} — ${c.name}` : c.code;
}

export function CurrencyPicker({
  value,
  onChange,
  options,
  publicTenantId,
  id = 'currency',
  disabled,
  allowEmpty = false,
  emptyLabel = 'None',
  placeholder = 'Choose a currency…',
  className,
}: CurrencyPickerProps) {
  const list = useCurrencyOptions(options, publicTenantId);
  // A stored code the list no longer offers (a retired currency) must still show, not vanish.
  const known = list.some((c) => c.code === value);
  return (
    <Select
      value={value ? value : allowEmpty ? NONE : ''}
      onValueChange={(next) => onChange(next === NONE ? '' : next)}
      disabled={disabled}
    >
      <SelectTrigger id={id} className={className}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {allowEmpty && <SelectItem value={NONE}>{emptyLabel}</SelectItem>}
        {value && !known && <SelectItem value={value}>{value}</SelectItem>}
        {list.map((c) => (
          <SelectItem key={c.code} value={c.code}>
            {currencyLabel(c)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

/** The react-hook-form wrapper, shaped like the `SelectField` helper the employee tabs use. */
export function CurrencyField<T extends FieldValues>({
  form,
  name,
  label = 'Currency',
  required,
  options,
  allowEmpty,
  emptyLabel,
  disabled,
  hint,
}: {
  form: UseFormReturn<T>;
  name: Path<T>;
  label?: string;
  required?: boolean;
  options?: HrCurrencyOption[] | null;
  allowEmpty?: boolean;
  emptyLabel?: string;
  disabled?: boolean;
  hint?: string;
}) {
  const raw = form.watch(name) as unknown as string | undefined;
  const message = (form.formState.errors as any)?.[name]?.message as string | undefined;
  return (
    <div className="space-y-2">
      <Label htmlFor={name}>
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </Label>
      <CurrencyPicker
        id={name}
        value={raw ?? ''}
        onChange={(code) => form.setValue(name, code as any, { shouldValidate: true, shouldDirty: true })}
        options={options}
        allowEmpty={allowEmpty}
        emptyLabel={emptyLabel}
        disabled={disabled}
      />
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      {message && <p className="text-sm text-red-500">{message}</p>}
    </div>
  );
}
