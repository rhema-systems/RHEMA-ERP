import { z } from 'zod';

/**
 * What every job-description child panel is handed.
 *
 * The two capability flags are separate because **the API separates them**: POST and PUT on all
 * twelve collections sit on `HR.Policy.JobArchitectureWrite`, which the HR role holds, while every
 * one of the twelve DELETEs sits on `HR.Policy.JobArchitectureAdmin`, which it does not. An HR
 * author can add a duty and correct it, and cannot remove it. Passing one flag for both would
 * either hide editing from the people who do it or offer a delete that 403s.
 */
export interface ChildPanelProps {
  jobDescriptionId: string;
  /**
   * False once the job description is approved or superseded. **This is a screen rule, not an API
   * rule** — the API would let the edit through. See `AUTHORABLE_JOB_DESCRIPTION_STATUSES`.
   */
  canAuthor: boolean;
  /** Admin-tier. False for an HR author. */
  canDelete: boolean;
  /** Detail/summary keys to refresh so the tab counts follow the table. */
  invalidateKeys: unknown[][];
}

/** The query key every panel derives its own from, so one invalidate can clear the family. */
export const childKey = (jobDescriptionId: string, collection: string) => [
  'hr',
  'job-analysis',
  jobDescriptionId,
  collection,
];

/**
 * An optional number field.
 *
 * ⚠ `z.coerce.number().optional()` is the wrong tool and looks right. An untouched numeric input
 * submits `''`, and `Number('')` is **0** — so a blank "% of time" would silently save as zero,
 * which on a responsibility means "this takes none of the holder's week" rather than "not stated".
 * Preprocessing to null first is what keeps blank meaning blank.
 */
export const optionalNumber = (min?: number, max?: number) =>
  z.preprocess(
    (v) => (v === '' || v === null || v === undefined ? null : Number(v)),
    z
      .number({ error: 'Enter a number' })
      .refine((n) => (min === undefined ? true : n >= min), { message: `Must be ${min} or more` })
      .refine((n) => (max === undefined ? true : n <= max), { message: `Must be ${max} or less` })
      .nullable(),
  );

/** A required whole number, coerced from the string a number input actually submits. */
export const requiredInt = (min: number, max?: number) => {
  const base = z.coerce.number().int().min(min);
  return max === undefined ? base : base.max(max);
};

/** Trim, and turn an empty optional text field into null rather than an empty string. */
export const nullIfBlank = (v?: string | null) => {
  const trimmed = (v ?? '').trim();
  return trimmed.length > 0 ? trimmed : null;
};

/**
 * `SelectField` yields `''` for its "None" choice; the API wants `null` for an absent FK.
 *
 * Sending `''` where a `Guid?` is expected is a 400 from the model binder, not a null — the two are
 * not interchangeable on the wire even though both read as "nothing chosen" in the form.
 */
export const idOrNull = (v?: string | null) => (v && v.length > 0 ? v : null);

/** 'CamelCaseName' → 'Camel Case Name', for showing an enum the form did not supply a label for. */
export const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/** Look a label up from one of the option lists, falling back to the spaced member name. */
export const labelFor = <T extends string>(
  options: { value: T; label: string }[],
  value?: T | null,
) => options.find((o) => o.value === value)?.label ?? spaced(value);
