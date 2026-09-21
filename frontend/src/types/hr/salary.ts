/**
 * Salary structure.
 *
 * Who maintains it is a policy setting (lane G, `salaryStructureSource`). With Payroll as the
 * source — the default, and TDC's case — grades, levels and notches are defined in Payroll
 * (Administration → HR → Payroll → Grades Setup), mirrored into HR by a backend projection, and
 * every HR write answers 409. With HR as the source the projection is off and the write types
 * below are live, through Administration → HR → Pay & Benefits → Salary Structure.
 *
 * `salaryStructureTiers` says whether the level is a real tier or the one implicit level a
 * two-tier grade carries; pickers hide it in the latter case and the server resolves it.
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

// ── Writes — live only while the structure source is HR (409 otherwise) ──────────

export interface CreateSalaryGradeRequest {
  code: string;
  name: string;
  description?: string;
  minSalary: number;
  maxSalary: number;
  isActive: boolean;
  /** DateTime */
  effectiveDate: string;
  endDate?: string | null;
}

export type UpdateSalaryGradeRequest = CreateSalaryGradeRequest & { id: string };

export interface CreateSalaryLevelRequest {
  salaryGradeId: string;
  code: string;
  name: string;
  minSalary: number;
  midSalary: number;
  maxSalary: number;
  sequence: number;
  isActive: boolean;
}

export type UpdateSalaryLevelRequest = CreateSalaryLevelRequest & { id: string };

export interface CreateSalaryNotchRequest {
  salaryLevelId: string;
  notchNumber: number;
  salaryAmount: number;
  isActive: boolean;
}

export type UpdateSalaryNotchRequest = CreateSalaryNotchRequest & { id: string };

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
