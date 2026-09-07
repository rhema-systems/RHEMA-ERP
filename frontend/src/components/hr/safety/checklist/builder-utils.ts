import { z } from 'zod';

/** The ids with `id` shifted one place up (-1) or down (+1); unchanged at the edges. */
export function moveWithin(ids: string[], id: string, delta: -1 | 1): string[] {
  const from = ids.indexOf(id);
  const to = from + delta;
  if (from < 0 || to < 0 || to >= ids.length) return ids;
  const next = [...ids];
  next.splice(from, 1);
  next.splice(to, 0, id);
  return next;
}

/** A decimal typed into a text box — kept as a string on the form, converted on submit. */
export const decimalString = (max: number) =>
  z
    .string()
    .regex(/^\d{1,3}(\.\d{1,2})?$/, 'Enter a number such as 85 or 94.99')
    .refine((v) => Number(v) <= max, `Must be ${max} or less`)
    .optional()
    .or(z.literal(''));

export const intString = (max: number) =>
  z
    .string()
    .regex(/^\d{1,4}$/, 'Enter a whole number')
    .refine((v) => Number(v) <= max, `Must be ${max} or less`)
    .optional()
    .or(z.literal(''));

export const numOrNull = (v?: string) => (v == null || v === '' ? null : Number(v));
export const numToStr = (v?: number | null) => (v == null ? '' : String(v));
export const blank = (v?: string) => (v && v.length > 0 ? v : null);
