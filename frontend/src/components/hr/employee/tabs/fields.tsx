'use client';

import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
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
