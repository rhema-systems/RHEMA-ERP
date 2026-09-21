import type { SkillCertification, SkillCertificationInput } from './certification';

// Mirrors SkillDto (ErpSystem.Core.DTOs.HR).
export interface Skill {
  id: string;
  name: string;
  description?: string | null;
  category?: string | null;
  requiresCertification: boolean;
  isActive: boolean;
  /** The credentials that evidence it (round 2, lane C2). Filled on the single read, empty on lists. */
  certifications: SkillCertification[];
}

// Mirrors CreateSkillDto — used for both create and update (the PUT reuses it).
export interface SkillRequest {
  name: string;
  description?: string | null;
  category?: string | null;
  requiresCertification: boolean;
  isActive: boolean;
  /**
   * The accepted credentials, as the whole set. Omitted = leave as stored; empty = none. A skill
   * that requires certification must name at least one, or the server refuses the save.
   */
  certifications?: SkillCertificationInput[] | null;
}
