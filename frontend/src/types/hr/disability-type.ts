/**
 * The tenant's disability catalogue (round 3, lane P2; register row E-5) — what the employee form
 * and the dependant form pick from once the disability box is ticked. Mirrors DisabilityTypeDto.
 *
 * ⚠ The free-text description on both records is NOT replaced by this: it stays as notes — the
 * person's own words, the accommodation needed, a condition the list does not name.
 */
export type DisabilityCategory =
  | 'Physical'
  | 'Visual'
  | 'Hearing'
  | 'Speech'
  | 'Intellectual'
  | 'Psychosocial'
  | 'Neurological'
  | 'ChronicHealth'
  | 'Multiple'
  | 'Other';

export const DISABILITY_CATEGORY_OPTIONS: { value: DisabilityCategory; label: string }[] = [
  { value: 'Physical', label: 'Physical / mobility' },
  { value: 'Visual', label: 'Visual' },
  { value: 'Hearing', label: 'Hearing' },
  { value: 'Speech', label: 'Speech' },
  { value: 'Intellectual', label: 'Intellectual / learning' },
  { value: 'Psychosocial', label: 'Psychosocial / mental health' },
  { value: 'Neurological', label: 'Neurological' },
  { value: 'ChronicHealth', label: 'Chronic health condition' },
  { value: 'Multiple', label: 'Multiple' },
  { value: 'Other', label: 'Other' },
];

export const DISABILITY_CATEGORY_LABEL: Record<DisabilityCategory, string> = Object.fromEntries(
  DISABILITY_CATEGORY_OPTIONS.map((o) => [o.value, o.label]),
) as Record<DisabilityCategory, string>;

export interface DisabilityType {
  id: string;
  name: string;
  code?: string | null;
  category: DisabilityCategory;
  categoryName?: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
  /** Employees plus dependants naming the row — zero is what lets it be deleted rather than retired. */
  usageCount: number;
}

export interface CreateDisabilityType {
  name: string;
  code?: string | null;
  category: DisabilityCategory;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export type UpdateDisabilityType = CreateDisabilityType;

/** The picker's options, grouped the way the catalogue is sectioned. */
export function disabilityTypeOptions(types: DisabilityType[]): { value: string; label: string }[] {
  return types.map((t) => ({ value: t.id, label: `${t.name} · ${DISABILITY_CATEGORY_LABEL[t.category] ?? t.category}` }));
}
