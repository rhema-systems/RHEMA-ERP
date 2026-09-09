import type {
  PositionCertificationRequirement,
  PositionCertificationRequirementInput,
} from './certification';

// Enums serialize as strings (JsonStringEnumConverter is registered globally).
export type WorkMode = 'OnSite' | 'Remote' | 'Hybrid';
// Mirrors ErpSystem.Core.Enums.SkillLevel, which has five members — 'Master' was
// missing here, so a Master-level skill coming back from the API was untyped.
export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert' | 'Master';

export const SKILL_LEVEL_OPTIONS: { value: SkillLevel; label: string }[] = [
  { value: 'Beginner', label: 'Beginner' },
  { value: 'Intermediate', label: 'Intermediate' },
  { value: 'Advanced', label: 'Advanced' },
  { value: 'Expert', label: 'Expert' },
  { value: 'Master', label: 'Master' },
];

// Mirrors PositionSkillRequirementDto (read).
export interface PositionSkillRequirement {
  id: string;
  skillId: string;
  skillName: string;
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
}

/**
 * Mirrors EmployeePositionBenefitDto (read). A position-level benefit ENTITLEMENT: everyone
 * holding the position is entitled to the policy, and `POST .../employee-benefit-enrollments/
 * reconcile` turns these into actual enrolments for each employee.
 */
export interface PositionBenefit {
  id: string;
  positionId: string;
  policyId: string;
  policyName: string;
  /** DateOnly. Past this date the entitlement confers nothing and reconcile skips it. */
  expiryDate?: string | null;
  /** Overrides the policy's own valuation for holders of this position when set. */
  positionAmount?: number | null;
  isActive: boolean;
}

/**
 * Mirrors CreateEmployeePositionBenefitDto (write).
 *
 * ⚠ Sent as the complete set on every position save. The server syncs to what it receives, so
 * an omitted or empty array removes every entitlement the position had — the edit form must
 * load the existing ones back in, not start blank.
 */
export interface PositionBenefitInput {
  policyId: string;
  expiryDate?: string | null;
  positionAmount?: number | null;
}

// Mirrors CreatePositionSkillRequirementDto (write).
export interface PositionSkillRequirementInput {
  skillId: string;
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
}

// Mirrors EmployeePositionDto. Skill requirements, staff level and benefit entitlements are
// all managed here.
export interface EmployeePosition {
  id: string;
  title: string;
  code: string;
  description?: string | null;
  organizationLevelId: string;
  organizationLevelName: string;
  organizationUnitId: string;
  organizationUnitName: string;
  staffLevelId?: string | null;
  staffLevelName?: string | null;
  reportsToPositionId?: string | null;
  reportsToPositionTitle?: string | null;
  level: number;
  minimumExperienceYears?: number | null;
  minimumAge?: number | null;
  maximumAge?: number | null;
  expectedHeadcount: number;
  salaryGradeId?: string | null;
  salaryGradeName?: string | null;
  workMode: WorkMode;
  probationPeriodMonths?: number | null;
  noticePeriodMonths?: number | null;
  requiresCertification: boolean;
  requiresGuarantor: boolean;
  /** ⚠ Null with requiresGuarantor true means "a guarantor, amount unspecified". */
  requiredGuarantorAmount?: number | null;
  requiredGuarantorCurrencyCode?: string | null;
  requiresLicense: boolean;
  isActive: boolean;
  employeeCount: number;
  skillRequirements: PositionSkillRequirement[];
  positionBenefits: PositionBenefit[];
  /** What the post must hold (round 2, lane C2). Filled on the single read and write responses. */
  certificationRequirements: PositionCertificationRequirement[];
}

// Mirrors CreateEmployeePositionDto.
export interface CreateEmployeePositionRequest {
  title: string;
  code: string;
  description?: string | null;
  organizationLevelId: string;
  organizationUnitId: string;
  staffLevelId?: string | null;
  reportsToPositionId?: string | null;
  level: number;
  minimumExperienceYears?: number | null;
  minimumAge?: number | null;
  maximumAge?: number | null;
  expectedHeadcount: number;
  salaryGradeId?: string | null;
  workMode: WorkMode;
  probationPeriodMonths?: number | null;
  noticePeriodMonths?: number | null;
  requiresCertification: boolean;
  requiresGuarantor: boolean;
  /** ⚠ Null with requiresGuarantor true means "a guarantor, amount unspecified". */
  requiredGuarantorAmount?: number | null;
  requiredGuarantorCurrencyCode?: string | null;
  requiresLicense: boolean;
  skillRequirements: PositionSkillRequirementInput[];
  positionBenefits: PositionBenefitInput[];
  /**
   * The required credentials, as the whole set (round 2, lane C2). With requiresCertification or
   * requiresLicense on, at least one — or the server refuses the save.
   */
  certificationRequirements: PositionCertificationRequirementInput[];
}

// Mirrors UpdateEmployeePositionDto (adds isActive).
export interface UpdateEmployeePositionRequest extends CreateEmployeePositionRequest {
  isActive: boolean;
}
