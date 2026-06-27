// TypeScript interfaces matching the C# workflow DTOs

// Workflow Definition DTOs
export interface WorkflowDefinitionAdminDto {
  id: string;
  name: string;
  description?: string;
  entityType: string;
  version: number;
  isActive: boolean;
  configuration?: string;
  createdDate: Date;
  lastModifiedDate?: Date;
  createdByName: string;
  lastModifiedByName?: string;
  stepCount: number;
  activeInstancesCount: number;
  lastUsedDate?: Date;
}

export interface CreateWorkflowDefinitionAdminDto {
  name: string;
  description?: string;
  entityType: string;
  isActive: boolean;
  configuration?: string;
  steps?: CreateWorkflowStepDto[];
  transitions?: CreateWorkflowTransitionDto[];
}

export interface UpdateWorkflowDefinitionAdminDto {
  name: string;
  description?: string;
  isActive: boolean;
  configuration?: string;
  steps?: CreateWorkflowStepDto[];
  transitions?: CreateWorkflowTransitionDto[];
}

export interface WorkflowDefinitionDto {
  id: string;
  name: string;
  description?: string;
  entityType: string;
  configuration?: string;
  isActive: boolean;
  version: number;
  createdById: string;
  createdByName: string;
  createdDate: Date;
  lastModifiedById?: string;
  lastModifiedByName?: string;
  lastModifiedDate?: Date;
  steps: WorkflowStepDto[];
  transitions: WorkflowTransitionDto[];
}

export interface WorkflowStepDto {
  id: string;
  name: string;
  description?: string;
  stepType: WorkflowStepType;
  order: number;
  isRequired: boolean;
  requiredRole?: string;
  estimatedHours?: number;
  configuration?: WorkflowStepConfigurationDto;
}

export interface WorkflowTransitionDto {
  id: string;
  fromStepId: string;
  toStepId: string;
  name: string;
  description?: string;
  condition?: WorkflowConditionDto;
  isDefault: boolean;
  priority: number;
}

export interface CreateWorkflowStepDto {
  id?: string;
  name: string;
  description?: string;
  stepType: WorkflowStepType;
  order: number;
  isRequired: boolean;
  requiredRole?: string;
  estimatedHours?: number;
  configuration?: WorkflowStepConfigurationDto;
}

export interface CreateWorkflowTransitionDto {
  fromStepId: string;
  toStepId: string;
  name: string;
  description?: string;
  condition?: WorkflowConditionDto;
  isDefault: boolean;
  priority: number;
}

export interface WorkflowStepConfigurationDto {
  assignmentRules?: WorkflowAssignmentRuleDto[];
  approvalConfig?: WorkflowApprovalConfigDto;
  qualityConfig?: WorkflowQualityConfigDto;
  notificationConfig?: WorkflowNotificationConfigDto;
  taskConfig?: WorkflowTaskConfigDto;
  escalationRules?: WorkflowEscalationRuleDto[];
  formFields?: WorkflowFormFieldDto[];
  skipCondition?: WorkflowConditionDto;
}

export interface WorkflowConditionDto {
  conditionType: WorkflowConditionType;
  expression: string;
  variables?: Record<string, any>;
  logicalOperator: WorkflowLogicalOperator;
  childConditions?: WorkflowConditionDto[];
}

export interface WorkflowAssignmentRuleDto {
  condition?: WorkflowConditionDto;
  assignmentType: WorkflowAssignmentType;
  userId?: string;
  role?: string;
  dynamicExpression?: string;
  priority: number;
}

export interface WorkflowApprovalConfigDto {
  approvalType: WorkflowApprovalType;
  approverRules: WorkflowAssignmentRuleDto[];
  minApprovalsRequired: number;
  autoApprovalCondition?: WorkflowConditionDto;
  rejectionHandling: WorkflowRejectionHandling;
}

export interface WorkflowQualityConfigDto {
  qualityChecks: WorkflowQualityCheckDto[];
  inspectionOfficerRules?: WorkflowAssignmentRuleDto[];
  autoPassCondition?: WorkflowConditionDto;
}

export interface WorkflowQualityCheckDto {
  id?: string;
  name: string;
  description: string;
  isRequired: boolean;
  applicabilityCondition?: WorkflowConditionDto;
  expectedValue?: any;
  validationExpression?: string;
}

export interface WorkflowApprovalChecklistResponseDto {
  id?: string;
  name: string;
  isSatisfied: boolean;
  notes?: string;
}

export interface WorkflowTaskConfigDto {
  taskActionType: string;
  documentName?: string;
  requiresDocument: boolean;
  documentRequirementKey?: string;
  instructions?: string;
}

export interface WorkflowTaskAttachmentDto {
  id: string;
  requirementKey?: string;
  documentName?: string;
  fileName: string;
  filePath: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedById: string;
  uploadedByName?: string;
}

export interface WorkflowNotificationConfigDto {
  triggers: WorkflowNotificationTriggerDto[];
}

export interface WorkflowNotificationTriggerDto {
  event: WorkflowNotificationEvent;
  condition?: WorkflowConditionDto;
  recipients: WorkflowAssignmentRuleDto[];
  messageTemplate?: string;
  channel: WorkflowNotificationChannel;
}

export interface WorkflowEscalationRuleDto {
  triggerCondition: WorkflowConditionDto;
  delayHours: number;
  escalationTargets: WorkflowAssignmentRuleDto[];
  action: WorkflowEscalationAction;
}

export interface WorkflowFormFieldDto {
  name: string;
  label: string;
  fieldType: WorkflowFieldType;
  isRequired: boolean;
  defaultValue?: any;
  options?: string[];
  validationRule?: string;
  visibilityCondition?: WorkflowConditionDto;
}

// Workflow Instance and Status DTOs
export interface WorkflowStatusDto {
  workflowInstanceId: string;
  workflowName: string;
  entityId: string;
  entityType: string;
  status: WorkflowInstanceStatus;
  startedDate: Date;
  completedDate?: Date;
  currentStepName?: string;
  currentStepInstanceId?: string;
  progress: WorkflowProgressDto;
  steps: WorkflowStepStatusDto[];
  pendingApprovals: WorkflowApprovalStatusDto[];
}

export interface WorkflowProgressDto {
  totalSteps: number;
  completedSteps: number;
  percentComplete: number;
  estimatedTimeRemaining?: string; // ISO duration string
}

export interface WorkflowStepStatusDto {
  stepInstanceId: string;
  stepName: string;
  status: WorkflowStepInstanceStatus;
  assignedToId?: string;
  assignedToName?: string;
  startedDate?: Date;
  completedDate?: Date;
  dueDate?: Date;
  isOverdue: boolean;
}

export interface WorkflowApprovalStatusDto {
  approvalId: string;
  stepName: string;
  approverId: string;
  approverName: string;
  approverRole?: string;
  status: WorkflowApprovalStatus;
  requestedDate: Date;
  dueDate?: Date;
  isOverdue: boolean;
}

export interface WorkflowPendingApproverDto {
  approverId?: string;
  approverName?: string;
  approverRole?: string;
}

export interface WorkflowEntitySummaryDto {
  entityType: string;
  entityId: string;
  hasActiveInstance: boolean;
  workflowInstanceId?: string;
  workflowName?: string;
  status?: WorkflowInstanceStatus;
  currentStepName?: string;
  currentStepInstanceId?: string;
  currentStepType?: WorkflowStepType;
  canCurrentUserApprove: boolean;
  canCurrentUserRecall?: boolean;
  canCurrentUserComplete?: boolean;
  pendingApprovers: WorkflowPendingApproverDto[];
  currentStepChecklist?: WorkflowQualityCheckDto[];
  currentStepTaskConfig?: WorkflowTaskConfigDto;
  currentStepTaskAttachments?: WorkflowTaskAttachmentDto[];
}

export interface WorkflowApprovalAuditDto {
  approvalId: string;
  approverId?: string;
  approverName?: string;
  approverRole?: string;
  status: WorkflowApprovalStatus;
  requestedDate: Date;
  processedDate?: Date;
  comments?: string;
  processedById?: string;
  processedByName?: string;
}

export interface WorkflowStepAuditDto {
  stepInstanceId: string;
  stepName: string;
  stepType: WorkflowStepType;
  status: WorkflowStepInstanceStatus;
  startedDate?: Date;
  completedDate?: Date;
  assignedToId?: string;
  assignedToName?: string;
  comments?: string;
  checklist?: WorkflowQualityCheckDto[];
  taskConfig?: WorkflowTaskConfigDto;
  taskAttachments?: WorkflowTaskAttachmentDto[];
  approvals: WorkflowApprovalAuditDto[];
}

export interface WorkflowEntityAuditDto {
  entityType: string;
  entityId: string;
  workflowInstanceId: string;
  workflowName: string;
  status: WorkflowInstanceStatus;
  startedDate: Date;
  completedDate?: Date;
  steps: WorkflowStepAuditDto[];
}

export interface WorkflowExecutionResult {
  success: boolean;
  message?: string;
  status: WorkflowInstanceStatus;
  workflowInstanceId?: string;
  currentStepId?: string;
  errors?: WorkflowExecutionError[];
  resultData?: any;
}

export interface WorkflowExecutionError {
  code: string;
  message: string;
  details?: string;
  stepId?: string;
}

export interface WorkflowValidationResult {
  isValid: boolean;
  errors: WorkflowValidationError[];
  warnings: string[];
}

export interface WorkflowValidationError {
  code: string;
  message: string;
  stepId?: string;
  transitionId?: string;
}

export interface WorkflowVariableInfo {
  name: string;
  displayName: string;
  dataType: string;
  description?: string;
  possibleValues?: string[];
}

export interface WorkflowEntityTypeInfo {
  id: string;
  code: string;
  name: string;
  description?: string;
  displayOrder: number;
  icon?: string;
  colorCode?: string;
  isActive: boolean;
}

// Administration DTOs
export interface WorkflowSummaryDto {
  totalWorkflowDefinitions: number;
  activeWorkflowDefinitions: number;
  totalWorkflowInstances: number;
  activeWorkflowInstances: number;
  completedWorkflowInstances: number;
  failedWorkflowInstances: number;
  instancesByEntityType: Record<string, number>;
  instancesByStatus: Record<string, number>;
  lastUpdated: Date;
}

// Filter DTOs
export interface WorkflowDefinitionFilterDto {
  page: number;
  pageSize: number;
  searchTerm?: string;
  entityType?: string;
  isActive?: boolean;
  createdAfter?: Date;
  createdBefore?: Date;
  sortBy: string;
  sortDescending: boolean;
}

export interface WorkflowInstanceFilterDto {
  page: number;
  pageSize: number;
  workflowDefinitionId?: string;
  entityType?: string;
  entityId?: string;
  status?: string;
  initiatedById?: string;
  startedAfter?: Date;
  startedBefore?: Date;
  completedAfter?: Date;
  completedBefore?: Date;
  sortBy: string;
  sortDescending: boolean;
}

// Request DTOs
export interface StartWorkflowRequest {
  workflowName: string;
  entityId: string;
  dataContext?: any;
}

export interface ExecuteStepRequest {
  stepData?: any;
}

export interface CancelWorkflowRequest {
  reason: string;
}

export interface RecallWorkflowRequest {
  reason?: string;
}

export interface ProcessStepRequest {
  action: WorkflowStepAction;
  resultData?: any;
  comments?: string;
}

export interface AssignStepRequest {
  assignedToId: string;
}

export interface ProcessApprovalRequest {
  action: WorkflowApprovalAction;
  comments?: string;
  checklistResponses?: WorkflowApprovalChecklistResponseDto[];
}

// Workflow Entities (for responses)
export interface WorkflowInstance {
  id: string;
  workflowDefinitionId: string;
  entityId: string;
  entityType: string;
  status: WorkflowInstanceStatus;
  initiatedById: string;
  startedDate: Date;
  completedDate?: Date;
  cancelledDate?: Date;
  dataContext?: string;
  currentStepId?: string;
  priority: WorkflowPriority;
  tenantId: string;
}

export interface WorkflowStepInstance {
  id: string;
  workflowInstanceId: string;
  workflowStepId: string;
  status: WorkflowStepInstanceStatus;
  assignedToId?: string;
  startedDate?: Date;
  completedDate?: Date;
  dueDate?: Date;
  resultData?: string;
  comments?: string;
  tenantId: string;
}

export interface WorkflowApproval {
  id: string;
  stepInstanceId: string;
  approverId?: string;
  approverRole?: string;
  status: WorkflowApprovalStatus;
  requestedDate: Date;
  processedDate?: Date;
  dueDate?: Date;
  comments?: string;
  tenantId: string;
}

// Workflow Enums (matching C# enums)
export enum WorkflowStepType {
  Manual = 0,
  Automatic = 1,
  Approval = 2,
  Decision = 3,
  Script = 4,
  Notification = 5,
  SubWorkflow = 6,
  Validation = 7,
  QualityControl = 8
}

export enum WorkflowInstanceStatus {
  Created = 0,
  InProgress = 1,
  Completed = 2,
  Cancelled = 3,
  Failed = 4,
  Suspended = 5,
  Waiting = 6
}

export enum WorkflowStepInstanceStatus {
  Pending = 0,
  InProgress = 1,
  Completed = 2,
  Cancelled = 3,
  Failed = 4
}

export enum WorkflowApprovalStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Delegated = 3,
  Expired = 4,
  MoreInfoRequested = 5
}

export enum WorkflowPriority {
  Low = 5,
  Normal = 3,
  High = 2,
  Critical = 1
}

export enum WorkflowApprovalAction {
  Approve = 0,
  Reject = 1,
  Delegate = 2,
  RequestMoreInfo = 3
}

export enum WorkflowStepAction {
  Complete = 0,
  Reject = 1,
  Delegate = 2,
  RequestInformation = 3,
  Skip = 4
}

export enum WorkflowConditionType {
  Expression = 0,
  Script = 1,
  Rule = 2,
  Always = 3,
  Never = 4
}

export enum WorkflowLogicalOperator {
  And = 0,
  Or = 1,
  Not = 2
}

export enum WorkflowAssignmentType {
  User = 0,
  Role = 1,
  Dynamic = 2,
  RequestorManager = 3,
  PreviousStepUser = 4
}

export enum WorkflowApprovalType {
  Single = 0,
  Multiple = 1,
  Consensus = 2,
  Majority = 3
}

export enum WorkflowRejectionHandling {
  StopWorkflow = 0,
  ReturnToPreviousStep = 1,
  ReturnToStart = 2,
  ContinueToNextStep = 3
}

export enum WorkflowNotificationEvent {
  StepAssigned = 0,
  StepCompleted = 1,
  ApprovalRequested = 2,
  WorkflowCompleted = 3,
  Escalation = 4,
  Overdue = 5,
  StepStarted = 6
}

export enum WorkflowNotificationChannel {
  Email = 0,
  InApp = 1,
  SMS = 2,
  Teams = 3
}

export enum WorkflowEscalationAction {
  Notify = 0,
  Reassign = 1,
  AutoApprove = 2,
  Cancel = 3,
  NotifyManager = 4
}

export enum WorkflowFieldType {
  Text = 0,
  Number = 1,
  Date = 2,
  Boolean = 3,
  Select = 4,
  MultiSelect = 5,
  TextArea = 6,
  File = 7
}
