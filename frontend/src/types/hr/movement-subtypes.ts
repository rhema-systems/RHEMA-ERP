/**
 * Subtype detail for a staff movement, and the standalone acting appointment.
 *
 * A movement carries at most ONE subtype row, and it must match the movement's own type — the
 * server refuses a promotion detail on a demotion, and refuses a second detail row on a movement
 * that already has one.
 */
import type { StaffMovementType } from './movements';

export type StaffPromotionType =
  | 'MeritBased'
  | 'SeniorityBased'
  | 'Competitive'
  | 'Acting'
  | 'Automatic';

export type StaffTransferType =
  | 'Interdepartmental'
  | 'InterStation'
  | 'CrossFunctional'
  | 'Temporary';

export type StaffTransferReasonCategory =
  | 'CareerDevelopment'
  | 'OperationalNeeds'
  | 'EmployeeRequest'
  | 'SkillsGapFilling'
  | 'OrganizationalRestructure'
  | 'PersonalReasons';

export type StaffDemotionReason =
  | 'PerformanceIssues'
  | 'DisciplinaryAction'
  | 'PositionElimination'
  | 'VoluntaryDemotion'
  | 'FailedProbation'
  | 'OrganizationalRestructure';

export type StaffSecondmentType = 'Internal' | 'External' | 'Government' | 'International';

export type StaffActingReason =
  | 'IncumbentOnLeave'
  | 'Vacancy'
  | 'TrialPeriod'
  | 'SpecialProject'
  | 'DevelopmentOpportunity';

/** How an acting allowance is worked out. Written from the enum. */
export type HRAllowanceCalculationMethod =
  | 'FixedAmount'
  | 'PercentageOfNewSalary'
  | 'DifferenceBetweenSalaries'
  | 'PercentageOfCurrentSalary';

export type StaffActingStatus =
  | 'Active'
  | 'Completed'
  | 'Extended'
  | 'TerminatedEarly'
  | 'ConvertedToPermanent';

/** Which subtype detail a movement of each type carries; the rest have none. */
export const SUBTYPE_FOR_MOVEMENT: Partial<Record<StaffMovementType, 'promotion' | 'transfer' | 'demotion' | 'secondment'>> = {
  Promotion: 'promotion',
  Transfer: 'transfer',
  Demotion: 'demotion',
  Secondment: 'secondment',
};

export interface StaffPromotionDetail {
  id: string;
  movementId: string;
  movementNumber: string;
  type: StaffPromotionType;
  typeName: string;
  /** Server-computed from the movement's salary grades — not editable. */
  gradeLevelIncrease: number;
  isActingPromotion: boolean;
  actingPeriodEndDate?: string | null;
  actingConditions?: string | null;
  additionalResponsibilities?: string | null;
  requiresTraining: boolean;
  requiredTraining?: string | null;
}

export interface StaffTransferDetail {
  id: string;
  movementId: string;
  movementNumber: string;
  type: StaffTransferType;
  typeName: string;
  reasonCategory: StaffTransferReasonCategory;
  reasonCategoryName: string;
  requiresRelocation: boolean;
  relocationAssistanceProvided: boolean;
  relocationAllowance?: number | null;
  relocationDetails?: string | null;
  housingAssistanceProvided: boolean;
  housingDetails?: string | null;
  transitionPeriodDays: number;
  transitionStartDate?: string | null;
  transitionEndDate?: string | null;
  replacementEmployeeId?: string | null;
  replacementEmployeeName?: string | null;
  isInterCompany: boolean;
  destinationCompanyId?: string | null;
  employmentContinues: boolean;
  interCompanyTransferDetails?: string | null;
}

export interface StaffDemotionDetail {
  id: string;
  movementId: string;
  movementNumber: string;
  reason: StaffDemotionReason;
  reasonName: string;
  /** Server-computed from the movement's salary grades — not editable. */
  gradeLevelDecrease: number;
  isDisciplinaryAction: boolean;
  disciplinaryActionId?: string | null;
  isPerformanceRelated: boolean;
  performanceImprovementPlanId?: string | null;
  employeeNotified: boolean;
  notificationDate?: string | null;
  rightToAppeal: boolean;
  appealDeadline?: string | null;
  employeeResponse?: string | null;
  employeeResponseDate?: string | null;
}

export interface StaffSecondmentDetail {
  id: string;
  movementId: string;
  movementNumber: string;
  type: StaffSecondmentType;
  typeName: string;
  startDate: string;
  endDate: string;
  durationMonths: number;
  isExternal: boolean;
  hostOrganization?: string | null;
  hostOrganizationContact?: string | null;
  termsAndConditions: string;
  salaryPaidByHomeOrganization: boolean;
  allowancesPaidByHostOrganization: boolean;
  secondmentAllowance?: number | null;
  objectives: string;
  expectedOutcomes?: string | null;
  returnGuaranteed: boolean;
  returnArrangements?: string | null;
  extensionAllowed: boolean;
  maxExtensionMonths?: number | null;
}

export interface StaffActingAppointment {
  id: string;
  appointmentNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  actingPositionId: string;
  actingPositionTitle: string;
  startDate: string;
  endDate?: string | null;
  reason: StaffActingReason;
  reasonName: string;
  actingForEmployeeId?: string | null;
  actingForEmployeeName?: string | null;
  receivesActingAllowance: boolean;
  actingAllowance?: number | null;
  /**
   * ⚠ The id was missing here while only its resolved NAME was present, so an edit form had
   * nothing to seed the control from — which is why `AllowanceCalculation` is one of the fields
   * the closure ledger's section E lists as settable by no form.
   */
  allowanceCalculation?: HRAllowanceCalculationMethod | null;
  allowanceCalculationName?: string | null;
  movementId?: string | null;
  movementNumber?: string | null;
  status: StaffActingStatus;
  statusName?: string;
  completionDate?: string | null;
  convertedToPermanent: boolean;
  conversionDate?: string | null;
  conversionMovementId?: string | null;
  notes?: string | null;
}

export interface CreateStaffActingAppointmentRequest {
  employeeId: string;
  actingPositionId: string;
  startDate: string;
  endDate?: string | null;
  reason: StaffActingReason;
  actingForEmployeeId?: string | null;
  receivesActingAllowance: boolean;
  actingAllowance?: number | null;
  movementId?: string | null;
  notes?: string | null;
}

/**
 * ⚠ Narrower than the create, and deliberately: the employee, the acting position and the start
 * date are what the appointment IS, and changing them makes it a different appointment. What an
 * edit corrects is how long it runs, what it pays and where it has got to.
 */
export interface UpdateStaffActingAppointmentRequest {
  endDate?: string | null;
  receivesActingAllowance: boolean;
  actingAllowance?: number | null;
  allowanceCalculation?: HRAllowanceCalculationMethod | null;
  status: StaffActingStatus;
  notes?: string | null;
}

export const ALLOWANCE_CALCULATIONS: HRAllowanceCalculationMethod[] = [
  'FixedAmount',
  'PercentageOfNewSalary',
  'DifferenceBetweenSalaries',
  'PercentageOfCurrentSalary',
];

export const ACTING_REASONS: StaffActingReason[] = [
  'IncumbentOnLeave',
  'Vacancy',
  'TrialPeriod',
  'SpecialProject',
  'DevelopmentOpportunity',
];

export const PROMOTION_TYPES: StaffPromotionType[] = [
  'MeritBased',
  'SeniorityBased',
  'Competitive',
  'Acting',
  'Automatic',
];

export const TRANSFER_TYPES: StaffTransferType[] = [
  'Interdepartmental',
  'InterStation',
  'CrossFunctional',
  'Temporary',
];

export const TRANSFER_REASONS: StaffTransferReasonCategory[] = [
  'CareerDevelopment',
  'OperationalNeeds',
  'EmployeeRequest',
  'SkillsGapFilling',
  'OrganizationalRestructure',
  'PersonalReasons',
];

export const DEMOTION_REASONS: StaffDemotionReason[] = [
  'PerformanceIssues',
  'DisciplinaryAction',
  'PositionElimination',
  'VoluntaryDemotion',
  'FailedProbation',
  'OrganizationalRestructure',
];

export const SECONDMENT_TYPES: StaffSecondmentType[] = [
  'Internal',
  'External',
  'Government',
  'International',
];
