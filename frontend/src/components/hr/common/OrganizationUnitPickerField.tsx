'use client';

import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { OrganizationUnitPicker } from './OrganizationUnitPicker';

/**
 * `OrganizationUnitPicker` as a react-hook-form field — the drop-in for the `SelectField
 * options={units.map(...)}` idiom that about fifteen HR forms used (demo feedback round 2, lane
 * B3: "the cascading level → unit dropdown everywhere a unit is chosen").
 *
 * Same contract as the `SelectField` it replaces: `form.watch(name)` is the value, an empty
 * string is "none", `form.setValue(..., { shouldValidate: true })` on change, the zod message
 * under the control. Two additions:
 * - `levelName`: a form that stores the level as well (a position history row, a goal template's
 *   scope) names that field and the picker writes it on every level change — one control for
 *   the pair instead of a level select beside a unit select that could disagree.
 * - `allowAnyLevel`: for a scope where "any level, any unit" is a real state (a template that
 *   applies to everyone), the level select gets a none row too.
 */
export function OrganizationUnitPickerField<T extends FieldValues>({
  form,
  name,
  levelName,
  label = 'Organization unit',
  levelLabel = 'Level',
  required,
  allowEmpty = false,
  emptyLabel = 'None',
  allowAnyLevel,
  hint,
  disabled,
  idPrefix,
}: {
  form: UseFormReturn<T>;
  name: Path<T>;
  /** The form field that stores the level id, when the form keeps one. */
  levelName?: Path<T>;
  label?: string;
  levelLabel?: string;
  required?: boolean;
  /** Adds a "None" choice that maps to an empty string. */
  allowEmpty?: boolean;
  emptyLabel?: string;
  /** Adds a "no level" choice to the level select, labelled so. */
  allowAnyLevel?: string;
  hint?: string;
  disabled?: boolean;
  idPrefix?: string;
}) {
  const raw = form.watch(name) as unknown as string | null | undefined;
  const value = raw ? String(raw) : '';
  const levelRaw = levelName ? (form.watch(levelName) as unknown as string | null | undefined) : undefined;
  const errors = form.formState.errors as Record<string, { message?: string } | undefined>;
  const error = (errors[name as string]?.message ?? (levelName ? errors[levelName as string]?.message : undefined)) as string | undefined;

  return (
    <OrganizationUnitPicker
      value={value}
      onChange={(id) => form.setValue(name, id as never, { shouldValidate: true, shouldDirty: true })}
      onLevelChange={
        levelName
          ? (levelId) => form.setValue(levelName, levelId as never, { shouldValidate: true, shouldDirty: true })
          : undefined
      }
      initialLevelId={levelName ? (levelRaw ? String(levelRaw) : '') : undefined}
      allowNone={allowEmpty ? emptyLabel : undefined}
      allowNoLevel={allowAnyLevel}
      unitLabel={required ? `${label} *` : label}
      levelLabel={levelLabel}
      hint={hint}
      error={error}
      disabled={disabled}
      idPrefix={idPrefix ?? String(name)}
    />
  );
}
