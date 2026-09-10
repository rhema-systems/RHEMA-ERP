/**
 * How one person is tied to another — the catalogue the referee, guarantor, next-of-kin and
 * candidate-referee screens pick from.
 *
 * ⚠ **Dependants are not a consumer.** They keep the `DependentRelationship` enum, because benefit
 * eligibility branches on its members (`maxChildAge` applies to Son and Daughter and to nothing
 * else). `mapsToDependentRelationship` is a read-only bridge between the two, not a write path.
 *
 * ⚠ **The category is the point, not a tag.** Each screen accepts a specific set, and the server
 * refuses anything outside it. Filtering the dropdown is a courtesy; the rule lives on the write.
 *
 * Round 2, lane D2 (register rows E-11a, E-11b).
 */

export type RelationshipCategory = 'Familial' | 'Professional' | 'Other';

export const RELATIONSHIP_CATEGORY_OPTIONS: { value: RelationshipCategory; label: string }[] = [
  { value: 'Familial', label: 'Familial' },
  { value: 'Professional', label: 'Professional' },
  { value: 'Other', label: 'Other' },
];

/**
 * What each screen accepts, mirroring the server's sets exactly.
 *
 * ⚠ Kept in one place rather than at each call site, because the four free-text columns this
 * catalogue replaces drifted apart precisely by being maintained in four places.
 */
export const RELATIONSHIP_SCOPES = {
  /** A next of kin is not a former manager. */
  nextOfKin: ['Familial', 'Other'] as RelationshipCategory[],
  /** A guarantor may be anyone — an employer, a brother, a landlord. */
  guarantor: ['Familial', 'Professional', 'Other'] as RelationshipCategory[],
  /** A professional or academic referee. */
  professionalReferee: ['Professional', 'Other'] as RelationshipCategory[],
  /** A personal referee may be a relative or a family friend. */
  personalReferee: ['Familial', 'Other'] as RelationshipCategory[],
} as const;

export interface RelationshipType {
  id: string;
  name: string;
  code?: string | null;
  category: RelationshipCategory;
  description?: string | null;
  /** Which dependant relationship this means, where it means one. Familial rows only. */
  mapsToDependentRelationship?: string | null;
  sortOrder: number;
  isActive: boolean;
  /**
   * How many records across all four consumers name this row.
   *
   * ⚠ Not decoration. Zero means the row can be deleted; anything else means it can only be
   * retired, and the delete endpoint refuses on the same number.
   */
  usageCount: number;
}

export interface CreateRelationshipType {
  name: string;
  code?: string | null;
  category: RelationshipCategory;
  description?: string | null;
  mapsToDependentRelationship?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export type UpdateRelationshipType = CreateRelationshipType;

/** The fourteen dependant ties a familial row may map to. Matches `DependentRelationship`. */
export const DEPENDENT_RELATIONSHIP_OPTIONS: { value: string; label: string }[] = [
  { value: 'Spouse', label: 'Spouse' },
  { value: 'Son', label: 'Son' },
  { value: 'Daughter', label: 'Daughter' },
  { value: 'Mother', label: 'Mother' },
  { value: 'Father', label: 'Father' },
  { value: 'Brother', label: 'Brother' },
  { value: 'Sister', label: 'Sister' },
  { value: 'Uncle', label: 'Uncle' },
  { value: 'Aunt', label: 'Aunt' },
  { value: 'Nephew', label: 'Nephew' },
  { value: 'Niece', label: 'Niece' },
  { value: 'Grandfather', label: 'Grandfather' },
  { value: 'Grandmother', label: 'Grandmother' },
  { value: 'Other', label: 'Other' },
];
