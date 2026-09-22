// Types for HR Onboarding (area 15) — plan templates, plans, tasks, assets and task comments.
// Mirrors the Onboarding section of ErpSystem.Core.DTOs.HR.RecruitmentDTOs.
//
// Backend routes: api/onboarding-plan-templates, api/onboarding-plans.

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums ─────────────────────────────────────────────────────────────────────

export type OnboardingStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Overdue' | 'Cancelled';

export const ONBOARDING_STATUS_OPTIONS = opts<OnboardingStatus>([
  ['NotStarted', 'Not Started'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Overdue', 'Overdue'],
  ['Cancelled', 'Cancelled'],
]);

export type OnboardingTaskStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Overdue'
  | 'Waived'
  | 'Blocked'
  | 'PendingVerification';

export const ONBOARDING_TASK_STATUS_OPTIONS = opts<OnboardingTaskStatus>([
  ['Pending', 'Pending'],
  ['InProgress', 'In Progress'],
  ['PendingVerification', 'Awaiting Verification'],
  ['Completed', 'Completed'],
  ['Overdue', 'Overdue'],
  ['Blocked', 'Blocked'],
  ['Waived', 'Waived'],
]);

export type OnboardingTaskCategory =
  | 'Documentation'
  | 'SystemAccess'
  | 'Orientation'
  | 'Training'
  | 'EquipmentSetup'
  | 'PayrollSetup'
  | 'PolicyAcknowledgement'
  | 'MeetAndGreet'
  | 'HealthAndSafety'
  | 'Compliance'
  | 'Other';

export const ONBOARDING_TASK_CATEGORY_OPTIONS = opts<OnboardingTaskCategory>([
  ['Documentation', 'Documentation'],
  ['SystemAccess', 'System Access'],
  ['Orientation', 'Orientation'],
  ['Training', 'Training'],
  ['EquipmentSetup', 'Equipment Setup'],
  ['PayrollSetup', 'Payroll Setup'],
  ['PolicyAcknowledgement', 'Policy Acknowledgement'],
  ['MeetAndGreet', 'Meet & Greet'],
  ['HealthAndSafety', 'Health & Safety'],
  ['Compliance', 'Compliance'],
  ['Other', 'Other'],
]);

export type OnboardingAssetType =
  | 'Laptop'
  | 'Desktop'
  | 'MobilePhone'
  | 'AccessCard'
  | 'ParkingPass'
  | 'Uniform'
  | 'SystemAccount'
  | 'EmailAccount'
  | 'SoftwareLicence'
  | 'Keys'
  | 'Other';

export const ONBOARDING_ASSET_TYPE_OPTIONS = opts<OnboardingAssetType>([
  ['Laptop', 'Laptop'],
  ['Desktop', 'Desktop'],
  ['MobilePhone', 'Mobile Phone'],
  ['AccessCard', 'Access Card'],
  ['ParkingPass', 'Parking Pass'],
  ['Uniform', 'Uniform'],
  ['SystemAccount', 'System Account'],
  ['EmailAccount', 'Email Account'],
  ['SoftwareLicence', 'Software Licence'],
  ['Keys', 'Keys'],
  ['Other', 'Other'],
]);

export type OnboardingAssetProvisionStatus =
  | 'Pending'
  | 'Ordered'
  | 'Ready'
  | 'Issued'
  | 'Acknowledged'
  | 'NotRequired';

export const ONBOARDING_ASSET_STATUS_OPTIONS = opts<OnboardingAssetProvisionStatus>([
  ['Pending', 'Pending'],
  ['Ordered', 'Ordered'],
  ['Ready', 'Ready'],
  ['Issued', 'Issued'],
  ['Acknowledged', 'Acknowledged'],
  ['NotRequired', 'Not Required'],
]);

// ── Plan templates ────────────────────────────────────────────────────────────

export interface OnboardingTaskTemplate extends AuditFields {
  tenantId: string;
  planTemplateId: string;
  planTemplateName: string;
  taskName: string;
  description?: string | null;
  category: OnboardingTaskCategory;
  categoryName?: string;
  /** Offset used to derive each instantiated task's due date from the plan's start date. */
  dueDaysFromStartDate: number;
  isMandatory: boolean;
  displayOrder: number;
  instructionsUrl?: string | null;
  ownerPositionId?: string | null;
  ownerPositionTitle?: string | null;
}

export interface OnboardingTaskTemplateCreateRequest {
  planTemplateId: string;
  taskName: string;
  description?: string | null;
  category: OnboardingTaskCategory;
  dueDaysFromStartDate: number;
  isMandatory: boolean;
  displayOrder: number;
  instructionsUrl?: string | null;
  ownerPositionId?: string | null;
}

export interface OnboardingTaskTemplateUpdateRequest
  extends Omit<OnboardingTaskTemplateCreateRequest, 'planTemplateId'> {
  id: string;
}

export interface OnboardingPlanTemplateSummary {
  id: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  taskTemplateCount: number;
}

export interface OnboardingPlanTemplate extends AuditFields {
  tenantId: string;
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
  taskTemplateCount: number;
}

export interface OnboardingPlanTemplateDetail extends OnboardingPlanTemplate {
  taskTemplates: OnboardingTaskTemplate[];
}

export interface OnboardingPlanTemplateCreateRequest {
  name: string;
  description?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface OnboardingPlanTemplateUpdateRequest extends OnboardingPlanTemplateCreateRequest {
  id: string;
}

// ── Task comments ─────────────────────────────────────────────────────────────

export interface OnboardingTaskComment extends AuditFields {
  tenantId: string;
  taskId: string;
  comment: string;
  authorId: string;
  authorName: string;
  commentDate: string;
}

export interface OnboardingTaskCommentCreateRequest {
  taskId: string;
  comment: string;
}

// ── Tasks ─────────────────────────────────────────────────────────────────────

export interface OnboardingTask extends AuditFields {
  tenantId: string;
  onboardingPlanId: string;
  taskTemplateId?: string | null;
  taskName: string;
  description?: string | null;
  category: OnboardingTaskCategory;
  categoryName?: string;
  status: OnboardingTaskStatus;
  statusName?: string;
  /** ISO date (DateOnly on the server) — no time component. */
  dueDate: string;
  completedDate?: string | null;
  isMandatory: boolean;
  isOverdue: boolean;
  assignedToId?: string | null;
  assignedToName?: string | null;
  assignedOrganizationUnitId?: string | null;
  assignedOrganizationUnitName?: string | null;
  ownerPositionId?: string | null;
  ownerPositionTitle?: string | null;
  completedById?: string | null;
  completedByName?: string | null;
  completionNotes?: string | null;
  evidenceFilePath?: string | null;
  requiresVerification: boolean;
  /** True while the task is done but still awaiting its second-party sign-off. */
  awaitingVerification: boolean;
  verifiedById?: string | null;
  verifiedByName?: string | null;
  verifiedDate?: string | null;
  displayOrder: number;
}

export interface OnboardingTaskDetail extends OnboardingTask {
  comments: OnboardingTaskComment[];
}

export interface OnboardingTaskCreateRequest {
  onboardingPlanId: string;
  taskTemplateId?: string | null;
  taskName: string;
  description?: string | null;
  category: OnboardingTaskCategory;
  dueDate: string;
  isMandatory: boolean;
  assignedToId?: string | null;
  assignedOrganizationUnitId?: string | null;
  requiresVerification: boolean;
  displayOrder: number;
}

export interface OnboardingTaskUpdateRequest {
  id: string;
  taskName: string;
  description?: string | null;
  dueDate: string;
  assignedToId?: string | null;
  assignedOrganizationUnitId?: string | null;
  requiresVerification: boolean;
  displayOrder: number;
}

export interface CompleteOnboardingTaskRequest {
  taskId: string;
  completionNotes?: string | null;
  evidenceFilePath?: string | null;
}

export interface VerifyOnboardingTaskRequest {
  taskId: string;
}

// ── Assets ────────────────────────────────────────────────────────────────────

export interface OnboardingAsset extends AuditFields {
  tenantId: string;
  onboardingPlanId: string;
  assetType: OnboardingAssetType;
  assetName: string;
  description?: string | null;
  assetTag?: string | null;
  serialNumber?: string | null;
  status: OnboardingAssetProvisionStatus;
  requiredByDate?: string | null;
  provisionedDate?: string | null;
  provisionedById?: string | null;
  provisionedByName?: string | null;
  issuedToEmployeeDate?: string | null;
  acknowledgedByEmployee: boolean;
  acknowledgementDate?: string | null;
  acknowledgementDocumentPath?: string | null;
  notes?: string | null;
}

export interface OnboardingAssetCreateRequest {
  onboardingPlanId: string;
  assetType: OnboardingAssetType;
  assetName: string;
  description?: string | null;
  assetTag?: string | null;
  serialNumber?: string | null;
  requiredByDate?: string | null;
  notes?: string | null;
}

export interface OnboardingAssetUpdateRequest {
  id: string;
  status: OnboardingAssetProvisionStatus;
  assetTag?: string | null;
  serialNumber?: string | null;
  requiredByDate?: string | null;
  provisionedDate?: string | null;
  provisionedById?: string | null;
  issuedToEmployeeDate?: string | null;
  acknowledgedByEmployee: boolean;
  acknowledgementDate?: string | null;
  acknowledgementDocumentPath?: string | null;
  notes?: string | null;
}

// ── Plans ─────────────────────────────────────────────────────────────────────

export interface OnboardingPlanSummary {
  id: string;
  employeeName: string;
  employeeNumber: string;
  status: OnboardingStatus;
  statusName?: string;
  startDate: string;
  targetCompletionDate?: string | null;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
}

export interface OnboardingPlan extends AuditFields {
  tenantId: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  templatePlanId?: string | null;
  templatePlanName?: string | null;
  status: OnboardingStatus;
  statusName?: string;
  startDate: string;
  targetCompletionDate?: string | null;
  actualCompletionDate?: string | null;
  assignedBuddyId?: string | null;
  assignedBuddyName?: string | null;
  onboardingCoordinatorId?: string | null;
  onboardingCoordinatorName?: string | null;
  notes?: string | null;
  /** Set when the system created the plan on hire confirmation: which template, and why (round 4, lane I4). */
  templateSelectionReason?: string | null;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
}

export interface OnboardingPlanDetail extends OnboardingPlan {
  tasks: OnboardingTask[];
  assets: OnboardingAsset[];
}

export interface OnboardingPlanCreateRequest {
  employeeId: string;
  /**
   * When set, the server instantiates the template's task templates onto the new plan, deriving each
   * due date from startDate + dueDaysFromStartDate. The tasks are copied, so later edits to the
   * template do not rewrite plans already in flight.
   */
  templatePlanId?: string | null;
  startDate: string;
  targetCompletionDate?: string | null;
  assignedBuddyId?: string | null;
  onboardingCoordinatorId?: string | null;
  notes?: string | null;
}

export interface OnboardingPlanUpdateRequest {
  id: string;
  status: OnboardingStatus;
  targetCompletionDate?: string | null;
  actualCompletionDate?: string | null;
  assignedBuddyId?: string | null;
  onboardingCoordinatorId?: string | null;
  notes?: string | null;
}
