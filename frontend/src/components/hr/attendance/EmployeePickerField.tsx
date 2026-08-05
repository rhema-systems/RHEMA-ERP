'use client';

import { useState } from 'react';
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { Label } from '@/components/ui/label';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';

/**
 * `EmployeePicker` wired into react-hook-form, matching the other helpers in
 * `components/hr/employee/tabs/fields.tsx`.
 *
 * The picker is uncontrolled with respect to its label — it only receives one back from its
 * own search results — so the label is held here and seeded with `initialLabel` when editing
 * an existing row. Without that, reopening a dialog would show the search box rather than
 * the employee already chosen.
 */
export function EmployeePickerField<T extends FieldValues>({
  form,
  name,
  label,
  required,
  initialLabel,
  placeholder,
  disabled,
}: {
  form: UseFormReturn<T>;
  name: Path<T>;
  label: string;
  required?: boolean;
  initialLabel?: string | null;
  placeholder?: string;
  disabled?: boolean;
}) {
  const value = (form.watch(name) as unknown as string) || null;
  const [selectedLabel, setSelectedLabel] = useState<string | null>(initialLabel ?? null);
  const message = (form.formState.errors as any)?.[name]?.message as string | undefined;

  return (
    <div className="space-y-2">
      <Label htmlFor={name}>
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </Label>
      <EmployeePicker
        value={value}
        initialLabel={selectedLabel}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(employeeId, employeeLabel) => {
          setSelectedLabel(employeeLabel);
          form.setValue(name, (employeeId ?? '') as any, { shouldValidate: true });
        }}
      />
      {message && <p className="text-sm text-red-500">{message}</p>}
    </div>
  );
}
