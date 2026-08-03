/**
 * Salary structure — READ ONLY in HR.
 *
 * Grades, levels and notches are defined in Payroll (Administration → HR → Payroll →
 * Grades Setup) and mirrored into the HR tables by a backend projection. HR screens
 * consume them as picker data; there are deliberately no create/update/delete request
 * types here, and the corresponding API endpoints return 409.
 */

// Mirrors SalaryGradeDto.
export interface SalaryGrade {
  id: string;
  code: string;
  name: string;
  description: string;
  minSalary: number;
  maxSalary: number;
  isActive: boolean;
  effectiveDate: string;
  endDate?: string | null;
}

// Mirrors SalaryLevelDto. HR is 3-tier; payroll is 2-tier, so a single level is
// synthesized per grade by the projection.
export interface SalaryLevel {
  id: string;
  salaryGradeId: string;
  code: string;
  name: string;
  minSalary: number;
  midSalary: number;
  maxSalary: number;
  sequence: number;
  isActive: boolean;
}

// Mirrors SalaryNotchDto. The notch amount is what HR reads for basic pay.
export interface SalaryNotch {
  id: string;
  salaryLevelId: string;
  notchNumber: number;
  salaryAmount: number;
  isActive: boolean;
}

// Mirrors SalaryGradeDetailDto.
export interface SalaryGradeDetail extends SalaryGrade {
  levels: SalaryLevel[];
}

// Mirrors SalaryStructureProjectionResult, returned by the explicit sync endpoint.
export interface SalaryStructureProjectionResult {
  skippedAsUnchanged: boolean;
  gradesCreated: number;
  gradesUpdated: number;
  gradesDeactivated: number;
  levelsCreated: number;
  levelsUpdated: number;
  notchesCreated: number;
  notchesUpdated: number;
  notchesDeactivated: number;
  warnings: string[];
}
