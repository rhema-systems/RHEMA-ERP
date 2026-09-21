/** The language catalogue (round 3, lane C1) — `api/hr/languages`. */
export interface Language {
  id: string;
  name: string;
  code?: string | null;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
  /** Candidate language rows naming this language; a delete is refused while it is above zero. */
  usageCount: number;
}

export interface CreateLanguage {
  name: string;
  code?: string | null;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export type UpdateLanguage = CreateLanguage;
