// Mirrors SkillDto (ErpSystem.Core.DTOs.HR).
export interface Skill {
  id: string;
  name: string;
  description?: string | null;
  category?: string | null;
  requiresCertification: boolean;
  isActive: boolean;
}

// Mirrors CreateSkillDto — used for both create and update (the PUT reuses it).
export interface SkillRequest {
  name: string;
  description?: string | null;
  category?: string | null;
  requiresCertification: boolean;
  isActive: boolean;
}
