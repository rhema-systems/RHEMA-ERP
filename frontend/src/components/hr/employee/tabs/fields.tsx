'use client';

import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

/**
 * Thin form-field wrappers used by the employee sub-resource tabs. Each one renders a
 * label, the control and its validation message, so a tab config can describe a whole
 * dialog in a few lines instead of repeating the same markup 13 times.
 */

interface BaseProps<T extends FieldValues> {
  form: UseFormReturn<T>;
  name: Path<T>;
  label: string;
  placeholder?: string;
  required?: boolean;
}

function FieldError<T extends FieldValues>({ form, name }: { form: UseFormReturn<T>; name: Path<T> }) {
  const message = (form.formState.errors as any)?.[name]?.message as string | undefined;
  if (!message) return null;
  return <p className="text-sm text-red-500">{message}</p>;
}

function FieldLabel({ htmlFor, label, required }: { htmlFor: string; label: string; required?: boolean }) {
  return (
    <Label htmlFor={htmlFor}>
      {label}
      {required && <span className="ml-0.5 text-red-500">*</span>}
    </Label>
  );
}

export function TextField<T extends FieldValues>({
  form,
  name,
  label,
  placeholder,
  required,
  type = 'text',
}: BaseProps<T> & { type?: 'text' | 'email' | 'tel' }) {
  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Input id={name} type={type} placeholder={placeholder} {...form.register(name)} />
      <FieldError form={form} name={name} />
    </div>
  );
}

/** A swatch preset needs to read as a colour, not a hex string, so the label is only a tooltip. */
const COLOR_PRESETS = [
  '#4F46E5', '#2563EB', '#0891B2', '#059669', '#65A30D',
  '#CA8A04', '#EA580C', '#DC2626', '#DB2777', '#7C3AED',
  '#475569', '#171717',
];

/**
 * A CSS hex colour: #RGB, #RRGGBB or #RRGGBBAA. Mirrors `Constants.Colors.HexPattern` server-side —
 * keep the two in step, since the API rejects a mismatch with a 400 the form cannot attribute to a
 * field.
 */
export const HEX_COLOR_PATTERN = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;

/**
 * Zod schema for an optional hex colour field. Empty is allowed — these are all nullable — but a
 * non-empty value must be a real hex code, so a typo is caught on submit instead of coming back as
 * a server-side 400.
 */
export const hexColorSchema = z
  .string()
  .trim()
  .refine((v) => v === '' || HEX_COLOR_PATTERN.test(v), {
    message: 'Use a hex colour such as #4F46E5 (#RGB, #RRGGBB or #RRGGBBAA).',
  })
  .optional()
  .or(z.literal(''));

const HEX_PATTERN = HEX_COLOR_PATTERN;

/**
 * Colour input: a native picker, a set of presets, and the hex text kept visible and editable.
 *
 * The text box is retained deliberately rather than replaced — a brand palette arrives as hex codes
 * that someone needs to paste, and these values are stored as strings the API round-trips. The picker
 * is what most people will use; the text box is the escape hatch and the audit of what was chosen.
 *
 * ⚠ `<input type="color">` cannot represent "no colour" — it reports #000000 when empty. So the
 * control never writes to the field on mount, only on an actual user action, and offers an explicit
 * Clear. Otherwise simply opening a form would silently set every blank colour to black.
 */
export function ColorField<T extends FieldValues>({
  form,
  name,
  label,
  required,
}: BaseProps<T>) {
  const raw = form.watch(name) as unknown;
  const value = typeof raw === 'string' ? raw : '';
  const isValid = HEX_PATTERN.test(value);
  // The native picker only accepts 6-digit hex; fall back to a neutral so it does not error, while
  // the field itself stays empty.
  const pickerValue = isValid && value.length === 7 ? value : '#4F46E5';

  const setValue = (next: string) =>
    form.setValue(name, next as any, { shouldDirty: true, shouldValidate: true });

  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <div className="flex items-center gap-2">
        <input
          type="color"
          aria-label={`${label} picker`}
          value={pickerValue}
          onChange={(e) => setValue(e.target.value.toUpperCase())}
          className="h-9 w-12 shrink-0 cursor-pointer rounded-md border bg-background p-1"
        />
        <Input
          id={name}
          placeholder="#4F46E5"
          className="font-mono uppercase"
          {...form.register(name)}
        />
        {value !== '' && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="shrink-0 text-muted-foreground"
            onClick={() => setValue('')}
          >
            Clear
          </Button>
        )}
      </div>
      <div className="flex flex-wrap gap-1.5">
        {COLOR_PRESETS.map((preset) => (
          <button
            key={preset}
            type="button"
            title={preset}
            aria-label={preset}
            onClick={() => setValue(preset)}
            className={`h-6 w-6 rounded-full border transition-transform hover:scale-110 ${
              value.toUpperCase() === preset ? 'ring-2 ring-offset-2 ring-ring' : ''
            }`}
            style={{ backgroundColor: preset }}
          />
        ))}
      </div>
      {value !== '' && !isValid && (
        <p className="text-sm text-amber-600">
          Not a valid hex colour — use #RGB, #RRGGBB or #RRGGBBAA.
        </p>
      )}
      <FieldError form={form} name={name} />
    </div>
  );
}

export function NumberField<T extends FieldValues>({
  form,
  name,
  label,
  placeholder,
  required,
  step,
}: BaseProps<T> & { step?: string }) {
  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Input id={name} type="number" step={step} placeholder={placeholder} {...form.register(name)} />
      <FieldError form={form} name={name} />
    </div>
  );
}

/** Native date input; value is the 'YYYY-MM-DD' string the API expects for DateOnly. */
export function DateField<T extends FieldValues>({ form, name, label, required }: BaseProps<T>) {
  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Input id={name} type="date" {...form.register(name)} />
      <FieldError form={form} name={name} />
    </div>
  );
}

/**
 * Native datetime-local input for the API's `DateTime` fields.
 *
 * ⚠ The form holds the browser's `YYYY-MM-DDTHH:mm` local string, NOT an ISO instant — the control
 * cannot represent a zone, and writing an ISO string straight into it silently drops the time. Use
 * {@link toIsoInstant} on submit and {@link fromIsoInstant} when seeding, so the conversion happens
 * once at the edge rather than being half-applied across a form.
 */
export function DateTimeField<T extends FieldValues>({ form, name, label, required }: BaseProps<T>) {
  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Input id={name} type="datetime-local" {...form.register(name)} />
      <FieldError form={form} name={name} />
    </div>
  );
}

/** ISO instant → the `YYYY-MM-DDTHH:mm` local string `DateTimeField` expects. Empty for null. */
export function fromIsoInstant(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** `DateTimeField`'s local string → an ISO instant. Null for empty, so an optional field stays unset. */
export function toIsoInstant(local?: string | null): string | null {
  if (!local) return null;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

/**
 * Native time input for the API's `TimeSpan` / `TimeOnly` fields.
 *
 * The wire format is `HH:mm:ss` but `<input type="time">` reads and writes `HH:mm`, so the
 * seconds are trimmed on the way in and appended on the way out. Attendance times are never
 * sub-minute, so nothing is lost.
 */
export function TimeField<T extends FieldValues>({ form, name, label, required }: BaseProps<T>) {
  const raw = form.watch(name) as unknown as string | undefined;
  const value = raw ? String(raw).slice(0, 5) : '';

  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Input
        id={name}
        type="time"
        value={value}
        onChange={(e) =>
          form.setValue(name, (e.target.value ? `${e.target.value}:00` : '') as any, {
            shouldValidate: true,
          })
        }
      />
      <FieldError form={form} name={name} />
    </div>
  );
}

export function TextareaField<T extends FieldValues>({
  form,
  name,
  label,
  placeholder,
  rows = 3,
}: BaseProps<T> & { rows?: number }) {
  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} />
      <Textarea id={name} rows={rows} placeholder={placeholder} {...form.register(name)} />
      <FieldError form={form} name={name} />
    </div>
  );
}

export const NONE_VALUE = '__none__';

export function SelectField<T extends FieldValues>({
  form,
  name,
  label,
  required,
  options,
  placeholder = 'Select…',
  /** Adds a "None" choice that maps to an empty string. */
  allowEmpty = false,
  emptyLabel = 'None',
}: BaseProps<T> & {
  options: { value: string; label: string }[];
  allowEmpty?: boolean;
  emptyLabel?: string;
}) {
  const raw = form.watch(name) as unknown as string | undefined;
  const value = raw ? String(raw) : allowEmpty ? NONE_VALUE : '';

  return (
    <div className="space-y-2">
      <FieldLabel htmlFor={name} label={label} required={required} />
      <Select
        value={value}
        onValueChange={(next) =>
          form.setValue(name, (next === NONE_VALUE ? '' : next) as any, { shouldValidate: true })
        }
      >
        <SelectTrigger id={name}>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {allowEmpty && <SelectItem value={NONE_VALUE}>{emptyLabel}</SelectItem>}
          {options.map((o) => (
            <SelectItem key={o.value} value={o.value}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <FieldError form={form} name={name} />
    </div>
  );
}

export function SwitchField<T extends FieldValues>({
  form,
  name,
  label,
  description,
}: Omit<BaseProps<T>, 'placeholder' | 'required'> & { description?: string }) {
  const checked = !!form.watch(name);
  return (
    <div className="flex items-center justify-between rounded-md border p-3">
      <div className="space-y-0.5">
        <Label htmlFor={name}>{label}</Label>
        {description && <p className="text-xs text-muted-foreground">{description}</p>}
      </div>
      <Switch
        id={name}
        checked={checked}
        onCheckedChange={(next) => form.setValue(name, next as any, { shouldValidate: true })}
      />
    </div>
  );
}

/** Two fields side by side. */
export function FieldRow({ children }: { children: React.ReactNode }) {
  return <div className="grid grid-cols-2 gap-4">{children}</div>;
}
