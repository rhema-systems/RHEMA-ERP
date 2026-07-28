// Enums serialize as strings (JsonStringEnumConverter is registered globally).
export type WorkMode = 'OnSite' | 'Remote' | 'Hybrid';
export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export const SKILL_LEVEL_OPTIONS: { value: SkillLevel; label: string }[] = [
  { value: 'Beginner', label: 'Beginner' },
  { value: 'Intermediate', label: 'Intermediate' },
  { value: 'Advanced', label: 'Advanced' },
  { value: 'Expert', label: 'Expert' },
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

// Mirrors CreatePositionSkillRequirementDto (write).
export interface PositionSkillRequirementInput {
  skillId: string;
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
}

// Mirrors EmployeePositionDto. Position benefits are still deferred (built with
// the Benefits area); skill requirements + staff level are managed here.
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
  requiresLicense: boolean;
  isActive: boolean;
  employeeCount: number;
  skillRequirements: PositionSkillRequirement[];
}

// Mirrors CreateEmployeePositionDto (position benefits omitted — deferred).
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
  requiresLicense: boolean;
  skillRequirements: PositionSkillRequirementInput[];
}

// Mirrors UpdateEmployeePositionDto (adds isActive).
export interface UpdateEmployeePositionRequest extends CreateEmployeePositionRequest {
  isActive: boolean;
}
