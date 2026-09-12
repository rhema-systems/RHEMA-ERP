/**
 * Staff movements — promotion, transfer, demotion, secondment, acting appointment, lateral move
 * and redesignation. Backend route: api/staff-movements (+ the per-subtype controllers).
 *
 * A movement is the paperwork for a change to someone's position, unit, reporting line and pay.
 * Slice 1 covers the core record, its status history, attachments and checklist; the approval route
 * and the act of applying the movement to the employee arrive in later slices.
 */

export type StaffMovementType =
  | 'Promotion'
  | 'Transfer'
  | 'Demotion'
  | 'LateralMove'
  | 'Secondment'
  | 'ActingAppointment'
  | 'Redesignation';

export type StaffMovementCategory =
  | 'Voluntary'
  | 'Involuntary'
  | 'OrganizationalRestructure'
  | 'CareerDevelopment'
  | 'Administrative';

/**
 * The six approval-route members between Submitted and Approved name TDC's chain: the employee's
 * current supervisor, the receiving supervisor, both heads of department, HR, then management.
 */
export type StaffMovementStatus =
  | 'Draft'
  | 'Submitted'
  | 'CurrentSupervisorApproval'
  | 'NewSupervisorApproval'
  | 'CurrentHodApproval'
  | 'NewHodApproval'
  | 'HrReview'
  | 'ManagementApproval'
  | 'Approved'
  | 'Rejected'
  | 'EmployeeAcceptancePending'
  | 'Implemented'
  | 'Cancelled';

export type StaffMovementAttachmentType =
  | 'ApprovalLetter'
  | 'PerformanceReview'
  | 'Justification'
  | 'JobDescription'
  | 'Other';

export type StaffMovementChecklistCategory =
  | 'HRTasks'
  | 'ITTasks'
  | 'FinanceTasks'
  | 'DepartmentHandover'
  | 'AccessSecurity';

export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected';

export const MOVEMENT_TYPES: StaffMovementType[] = [
  'Promotion',
  'Transfer',
  'Demotion',
  'LateralMove',
  'Secondment',
  'ActingAppointment',
  'Redesignation',
];

export const MOVEMENT_CATEGORIES: StaffMovementCategory[] = [
  'Voluntary',
  'Involuntary',
  'OrganizationalRestructure',
  'CareerDevelopment',
  'Administrative',
];

export const ATTACHMENT_TYPES: StaffMovementAttachmentType[] = [
  'ApprovalLetter',
  'PerformanceReview',
  'Justification',
  'JobDescription',
  'Other',
];

export const CHECKLIST_CATEGORIES: StaffMovementChecklistCategory[] = [
  'HRTasks',
  'ITTasks',
  'FinanceTasks',
  'DepartmentHandover',
  'AccessSecurity',
];

/** Types that are temporary by nature — the form defaults `isTemporary` from this. */
export const TEMPORARY_BY_NATURE: StaffMovementType[] = ['Secondment', 'ActingAppointment'];

/** Statuses in which the movement is somewhere inside the approval route. */
export const IN_APPROVAL_STATUSES: StaffMovementStatus[] = [
  'Submitted',
  'CurrentSupervisorApproval',
  'NewSupervisorApproval',
  'CurrentHodApproval',
  'NewHodApproval',
  'HrReview',
  'ManagementApproval',
];

/** Statuses after which nothing further happens to the record. */
export const TERMINAL_STATUSES: StaffMovementStatus[] = ['Rejected', 'Implemented', 'Cancelled'];

export interface StaffMovementSummary {
  id: string;
  movementNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber?: string | null;
  movementType: StaffMovementType;
  movementTypeName: string;
  category: StaffMovementCategory;
  categoryName: string;
  currentPositionTitle: string;
  currentOrganizationUnitName: string;
  newPositionTitle: string;
  newOrganizationUnitName: string;
  requestDate: string;
  effectiveDate: string;
  status: StaffMovementStatus;
  isTemporary: boolean;
  temporaryEndDate?: string | null;
  returnProcessed: boolean;
  promotionType?: string | null;
  gradeLevelIncrease?: number | null;
  isActingPromotion: boolean;
  transferType?: string | null;
  requiresRelocation: boolean;
  isInterCompany: boolean;
  isInTransition: boolean;
}

export interface StaffMovement extends StaffMovementSummary {
  tenantId: string;
  createdAt: string;
  updatedAt?: string | null;

  currentPositionId: string;
  currentOrganizationUnitId: string;
  currentOrganizationLevelId?: string | null;
  currentOrganizationLevelName?: string | null;
  currentLocationId?: string | null;
  currentLocationName?: string | null;
  currentLocationLevelId?: string | null;
  currentLocationLevelName?: string | null;
  currentSupervisorId?: string | null;
  currentSupervisorName?: string | null;
  currentSalary: number;
  currentSalaryGradeId?: string | null;
  currentSalaryGradeCode?: string | null;
  currentSalaryGradeName?: string | null;
  currentSalaryLevelId?: string | null;
  currentSalaryLevelName?: string | null;
  currentSalaryNotchId?: string | null;
  currentSalaryNotchNumber?: number | null;

  newPositionId: string;
  newOrganizationUnitId: string;
  newOrganizationLevelId?: string | null;
  newOrganizationLevelName?: string | null;
  newLocationId?: string | null;
  newLocationName?: string | null;
  newLocationLevelId?: string | null;
  newLocationLevelName?: string | null;
  newSupervisorId?: string | null;
  newSupervisorName?: string | null;
  newSalary: number;
  newSalaryGradeId?: string | null;
  newSalaryGradeCode?: string | null;
  newSalaryGradeName?: string | null;
  newSalaryLevelId?: string | null;
  newSalaryLevelName?: string | null;
  newSalaryNotchId?: string | null;
  newSalaryNotchNumber?: number | null;

  /** Server-computed from the salary delta — not editable. */
  salaryIncreaseAmount?: number | null;
  salaryIncreasePercentage?: number | null;

  reason: string;
  justification?: string | null;
  isReorganization: boolean;
  isSuccessionPlan: boolean;
  successionPlanId?: string | null;
  successionPlanNumber?: string | null;

  requestSubmissionDate: string;
  requestedById: string;
  requestedByName?: string | null;

  temporaryArrangementDetails?: string | null;
  actualReturnDate?: string | null;
  returnMovementId?: string | null;

  authorizedById?: string | null;
  authorizedByName?: string | null;
  authorizationDate?: string | null;

  rejectionReason?: string | null;
  rejectedById?: string | null;
  rejectedByName?: string | null;
  rejectionDate?: string | null;

  cancellationReason?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancellationDate?: string | null;

  requiresEmployeeAcceptance: boolean;
  employeeAccepted?: boolean | null;
  employeeResponseDate?: string | null;
  employeeComments?: string | null;

  requiresHandover: boolean;
  handoverCompletionDate?: string | null;
  handoverNotes?: string | null;

  basedOnAppraisalId?: string | null;
  additionalNotes?: string | null;

  approvalLevels?: StaffMovementApprovalLevel[];
  statusHistory?: StaffMovementStatusHistory[];
  attachments?: StaffMovementAttachment[];
  checklistItems?: StaffMovementChecklistItem[];
}

export interface StaffMovementApprovalLevel {
  id: string;
  movementId: string;
  movementNumber: string;
  level: number;
  roleName: string;
  approverId: string;
  approverName: string;
  status: ApprovalStatus;
  actionDate?: string | null;
  comments?: string | null;
  delegatedToId?: string | null;
  delegatedToName?: string | null;
  delegationDate?: string | null;
  delegationReason?: string | null;
}

export interface StaffMovementStatusHistory {
  id: string;
  movementId: string;
  movementNumber: string;
  fromStatus: StaffMovementStatus;
  toStatus: StaffMovementStatus;
  changedDate: string;
  changedById: string;
  changedByName: string;
  reason?: string | null;
}

export interface StaffMovementAttachment {
  id: string;
  movementId: string;
  movementNumber: string;
  fileName: string;
  filePath: string;
  type: StaffMovementAttachmentType;
  description?: string | null;
  uploadDate: string;
  uploadedById: string;
  uploadedByName: string;
}

export interface StaffMovementChecklistItem {
  id: string;
  movementId: string;
  taskDescription: string;
  category: StaffMovementChecklistCategory;
  isRequired: boolean;
  responsiblePersonId?: string | null;
  responsiblePersonName?: string | null;
  dueDate?: string | null;
  isCompleted: boolean;
  completionDate?: string | null;
  completionNotes?: string | null;
  displayOrder: number;
}

// ── Requests ─────────────────────────────────────────────────────────────────

export interface CreateStaffMovementRequest {
  employeeId: string;
  movementType: StaffMovementType;
  category: StaffMovementCategory;

  currentPositionId: string;
  currentOrganizationUnitId: string;
  currentOrganizationLevelId?: string | null;
  currentLocationId?: string | null;
  currentLocationLevelId?: string | null;
  currentSupervisorId?: string | null;
  currentSalary: number;
  currentSalaryGradeId?: string | null;
  currentSalaryLevelId?: string | null;
  currentSalaryNotchId?: string | null;

  newPositionId: string;
  newOrganizationUnitId: string;
  newOrganizationLevelId?: string | null;
  newLocationId?: string | null;
  newLocationLevelId?: string | null;
  newSupervisorId?: string | null;
  newSalary: number;
  newSalaryGradeId?: string | null;
  newSalaryLevelId?: string | null;
  newSalaryNotchId?: string | null;

  reason: string;
  justification?: string | null;
  isReorganization?: boolean;
  isSuccessionPlan?: boolean;
  successionPlanId?: string | null;
  effectiveDate: string;
  isTemporary?: boolean;
  temporaryEndDate?: string | null;
  temporaryArrangementDetails?: string | null;
  requiresEmployeeAcceptance?: boolean;
  requiresHandover?: boolean;
  basedOnAppraisalId?: string | null;
  additionalNotes?: string | null;
}

export interface UpdateStaffMovementRequest {
  id: string;
  newPositionId: string;
  newOrganizationUnitId: string;
  newOrganizationLevelId?: string | null;
  newLocationId?: string | null;
  newLocationLevelId?: string | null;
  newSupervisorId?: string | null;
  newSalary: number;
  newSalaryGradeId?: string | null;
  newSalaryLevelId?: string | null;
  newSalaryNotchId?: string | null;
  reason: string;
  justification?: string | null;
  effectiveDate: string;
  isTemporary?: boolean;
  temporaryEndDate?: string | null;
  temporaryArrangementDetails?: string | null;
  requiresEmployeeAcceptance?: boolean;
  requiresHandover?: boolean;
  additionalNotes?: string | null;
}

export interface StaffMovementDashboard {
  totalActive: number;
  pendingApproval: number;
  approvedThisMonth: number;
  rejectedThisMonth: number;
  awaitingEmployeeResponse: number;
  awaitingHandover: number;
  expiringSecondments: number;
  activeActingAppointments: number;
  awaitingImplementation: number;
  overdueApprovalActions: number;
  implementedYtd: number;
  totalMovementsYtd: number;
  filterYear: number;
  averageSalaryIncreasePercentage: number;
  movementsWithSalaryIncrease: number;
  byType: MovementTypeSummary[];
  recent: StaffMovementSummary[];
  overdueApprovals: OverdueApprovalSummary[];
  expiringAssignments: ExpiringAssignmentSummary[];
  bottlenecksByUnit: ApprovalBottleneck[];
  computedAt: string;
}

export interface MovementTypeSummary {
  movementType: StaffMovementType;
  totalActive: number;
  pendingApproval: number;
  completedThisMonth: number;
  implementedYtd: number;
  sharePercent: number;
}

export interface OverdueApprovalSummary {
  movementId: string;
  movementNumber: string;
  employeeName: string;
  employeeNumber?: string | null;
  movementType: StaffMovementType;
  status: StaffMovementStatus;
  pendingApproverName: string;
  pendingStage: string;
  overdueDays: number;
  effectiveDate: string;
  priority: number;
}

export interface ExpiringAssignmentSummary {
  movementId: string;
  movementNumber: string;
  employeeName: string;
  employeeNumber?: string | null;
  assignmentType: StaffMovementType;
  endDate: string;
  daysRemaining: number;
  returnProcessed: boolean;
  currentPosition?: string | null;
  newPosition?: string | null;
  hostOrganization?: string | null;
}

export interface ApprovalBottleneck {
  organizationUnitId: string;
  organizationUnitName: string;
  pendingCount: number;
  averageWaitDays: number;
  mostDelayedStage: string;
  nearingEffectiveDate: number;
}
