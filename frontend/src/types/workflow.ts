// TypeScript interfaces matching the C# workflow DTOs

// Workflow Definition DTOs
export interface WorkflowDefinitionAdminDto {
  id: string;
  definitionKey: string;
  name: string;
  description?: string;
  entityType: string;
  version: number;
  isActive: boolean;
  lifecycleStatus: WorkflowDefinitionLifecycleStatus;
  changeSummary?: string;
  supersedesDefinitionId?: string;
  publishedAt?: Date;
  retiredAt?: Date;
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
  entityType?: string;
  isActive: boolean;
  configuration?: string;
  steps?: CreateWorkflowStepDto[];
  transitions?: CreateWorkflowTransitionDto[];
}

export interface WorkflowDefinitionDto {
  id: string;
  definitionKey: string;
  name: string;
  description?: string;
  entityType: string;
  configuration?: string;
  isActive: boolean;
  version: number;
  lifecycleStatus: WorkflowDefinitionLifecycleStatus;
  changeSummary?: string;
  supersedesDefinitionId?: string;
  publishedAt?: Date;
  retiredAt?: Date;
  createdById: string;
  createdByName: string;
  createdDate: Date;
  lastModifiedById?: string;
  lastModifiedByName?: string;
  lastModifiedDate?: Date;
  steps: WorkflowStepDto[];
  transitions: WorkflowTransitionDto[];
}

export interface WorkflowDefinitionVersionDto {
  id: string;
  definitionKey: string;
  name: string;
  version: number;
  lifecycleStatus: WorkflowDefinitionLifecycleStatus;
  isActive: boolean;
  changeSummary?: string;
  supersedesDefinitionId?: string;
  createdDate: Date;
  publishedAt?: Date;
  retiredAt?: Date;
  activeInstancesCount: number;
}

export interface WorkflowDefinitionComparisonDto {
  definitionKey: string;
  fromDefinitionId: string;
  fromVersion: number;
  toDefinitionId: string;
  toVersion: number;
  hasChanges: boolean;
  hasPotentiallyBreakingChanges: boolean;
  changes: string[];
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
  approvalGroup?: number;
  condition?: WorkflowConditionDto;
  assignmentType: WorkflowAssignmentType;
  userId?: string;
  role?: string;
  dynamicExpression?: string;
  priority: number;
}

export interface WorkflowApprovalConfigDto {
  approvalType: WorkflowApprovalType;
  activationMode?: WorkflowApprovalActivationMode;
  approverRules: WorkflowAssignmentRuleDto[];
  minApprovalsRequired: number;
  autoApprovalCondition?: WorkflowConditionDto;
  rejectionHandling: WorkflowRejectionHandling;
  preventInitiatorApproval: boolean;
  requireDistinctApprovers: boolean;
  conflictRules: WorkflowApprovalConflictRuleDto[];
  signaturePolicy?: WorkflowSignaturePolicyDto;
  evidenceRequirements?: WorkflowEvidenceRequirementDto[];
  allowEvidenceException?: boolean;
  evidenceExceptionApproverRole?: string;
  minimumExceptionReasonLength?: number;
  requiresManagingDirectorApproval?: boolean;
  managingDirectorApproverRole?: string;
}

export interface WorkflowEvidenceRequirementDto {
  requirementKey: string;
  documentName: string;
  documentType?: string;
  minimumDocuments: number;
  requireVerification: boolean;
}

export enum WorkflowSignatureMethod { Attestation = 0, DigitalCertificate = 1, ExternalProvider = 2 }
export interface WorkflowSignaturePolicyDto {
  isRequired: boolean;
  method: WorkflowSignatureMethod;
  requiredSigningRole?: string;
  requireValidCertificateChain: boolean;
  attestationText: string;
}
export interface WorkflowSignatureSubmissionDto {
  method: WorkflowSignatureMethod;
  attestation: string;
  certificateBase64?: string;
  externalReference?: string;
  signedAt: string;
}

export interface WorkflowApprovalConflictRuleDto {
  id: string;
  name: string;
  isEnabled: boolean;
  actorSource: WorkflowApprovalActorSource;
  sourceStepName?: string;
  contextField?: string;
  message?: string;
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
  requiresDocument: boolean;
  documentType?: string;
  documentName?: string;
  applicabilityCondition?: WorkflowConditionDto;
  expectedValue?: any;
  validationExpression?: string;
}

export interface WorkflowApprovalChecklistResponseDto {
  id?: string;
  name: string;
  isSatisfied: boolean;
  notes?: string;
  attachmentIds?: string[];
  completedById?: string;
  completedByName?: string;
  completedAt?: string;
}

export interface WorkflowTaskConfigDto {
  taskActionType: string;
  documentName?: string;
  requiresDocument: boolean;
  documentRequirementKey?: string;
  documentRequirements?: WorkflowDocumentRequirementDto[];
  instructions?: string;
}

export interface WorkflowDocumentRequirementDto {
  id?: string;
  requirementKey: string;
  documentName: string;
  documentType?: string;
  providedBy?: 'Customer' | 'Estate' | 'Legal' | 'Finance' | 'Internal';
  appliesTo?: 'All' | 'Rent' | 'Sale';
  isRequired: boolean;
}

export interface WorkflowTaskAttachmentDto {
  id: string;
  requirementKey?: string;
  checklistItemId?: string;
  documentType?: string;
  documentName?: string;
  fileName: string;
  filePath: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedById: string;
  uploadedByName?: string;
  documentOwnerId?: string;
  issueDate?: string;
  expiryDate?: string;
  version?: number;
  replacesAttachmentId?: string;
  verificationStatus?: number;
  malwareScanStatus?: number;
  isLegalHold?: boolean;
  sha256?: string;
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
  approvalGroup?: number;
  stepName: string;
  approverId: string;
  approverName: string;
  approverRole?: string;
  status: WorkflowApprovalStatus;
  requestedDate: Date;
  dueDate?: Date;
  isOverdue: boolean;
  isAdHoc?: boolean;
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
  approvalRequired: boolean;
  /** Retained approval history, including completed or cancelled instances. */
  hasWorkflowHistory?: boolean;
  workflowInstanceId?: string;
  workflowName?: string;
  status?: WorkflowInstanceStatus;
  currentStepName?: string;
  currentStepInstanceId?: string;
  currentStepType?: WorkflowStepType;
  canCurrentUserApprove: boolean;
  currentUserApprovalId?: string;
  currentUserCorrectionId?: string;
  canCurrentUserResubmit?: boolean;
  correctionInstructions?: string;
  canCurrentUserRecall?: boolean;
  canCurrentUserComplete?: boolean;
  pendingApprovers: WorkflowPendingApproverDto[];
  currentStepChecklist?: WorkflowQualityCheckDto[];
  currentStepTaskConfig?: WorkflowTaskConfigDto;
  currentStepTaskAttachments?: WorkflowTaskAttachmentDto[];
  currentStepSignaturePolicy?: WorkflowSignaturePolicyDto;
}

export interface WorkflowApprovalAuditDto {
  approvalId: string;
  approvalGroup?: number;
  isAdHoc?: boolean;
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
  checklistResponses?: WorkflowApprovalChecklistResponseDto[];
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

export interface WorkflowModuleConformanceReport {
  isConformant: boolean;
  activeEntityTypeCount: number;
  supportedEntityTypes: string[];
  missingStatusAdapters: string[];
}

export interface WorkflowApprovalPolicySetDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  module?: string;
  entityType?: string;
  category?: string;
  locationId?: string;
  legalEntityId?: string;
  minimumAmount?: number;
  maximumAmount?: number;
  currencyCode?: string;
  effectiveFrom: string;
  effectiveTo?: string;
  priority: number;
  isActive: boolean;
  lifecycleStatus: WorkflowDefinitionLifecycleStatus;
  approvalConfig: WorkflowApprovalConfigDto;
}

export type SaveWorkflowApprovalPolicyRequest = Omit<
  WorkflowApprovalPolicySetDto,
  'id' | 'lifecycleStatus'
>;

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
  delegateToId?: string;
  signature?: WorkflowSignatureSubmissionDto;
}

export enum WorkflowDelegationKind { Authority = 0, OutOfOffice = 1 }

export interface WorkflowDirectoryUser {
  id: string;
  firstName: string;
  lastName: string;
  email?: string;
  userName?: string;
}

export interface WorkflowDelegationDto {
  id: string;
  principalUserId: string;
  delegateUserId: string;
  kind: WorkflowDelegationKind;
  module?: string;
  entityType?: string;
  workflowDefinitionId?: string;
  workflowStepId?: string;
  maximumAmount?: number;
  currencyCode?: string;
  effectiveFrom: string;
  effectiveTo: string;
  reason: string;
  allowRedelegation: boolean;
  isActive: boolean;
  revokedAt?: string;
  revocationReason?: string;
}

export type SaveWorkflowDelegationRequest = Omit<
  WorkflowDelegationDto,
  'id' | 'principalUserId' | 'isActive' | 'revokedAt' | 'revocationReason'
> & { principalUserId?: string };

export interface WorkflowWorkingCalendarDto {
  id?: string;
  name: string;
  timeZoneId: string;
  workingDaysMask: number;
  workDayStart: string;
  workDayEnd: string;
  holidays: string[];
}

export interface WorkflowModuleOptionDto {
  id: string;
  code: string;
  name: string;
  description?: string;
}

export interface WorkflowDelegationStepOptionDto {
  id: string;
  name: string;
  stepType: WorkflowStepType;
  order: number;
  isStartStep: boolean;
  isEndStep: boolean;
}

export interface WorkflowDelegationWorkflowOptionDto {
  id: string;
  name: string;
  version: number;
  lifecycleStatus: WorkflowDefinitionLifecycleStatus;
  entityType: string;
  entityTypeCode?: string;
  module?: string;
  steps: WorkflowDelegationStepOptionDto[];
}

export interface WorkflowDelegationScopeOptionsDto {
  modules: WorkflowModuleOptionDto[];
  entityTypes: WorkflowEntityTypeInfo[];
  workflowDefinitions: WorkflowDelegationWorkflowOptionDto[];
}

export interface WorkflowEvidenceCountDto {
  total: number;
  pending: number;
  verified: number;
  rejected: number;
  legalHold: number;
}

export interface WorkflowEvidenceReviewStepDto {
  stepInstanceId: string;
  workflowStepId: string;
  stepName: string;
  status: WorkflowStepInstanceStatus;
  startedDate?: string;
  completedDate?: string;
  dueDate?: string;
  evidence: WorkflowEvidenceCountDto;
}

export interface WorkflowEvidenceReviewInstanceDto {
  id: string;
  workflowDefinitionId: string;
  workflowName: string;
  entityType: string;
  entityId: string;
  status: WorkflowInstanceStatus;
  startedDate?: string;
  completedDate?: string;
  createdDate: string;
  currentStepInstanceId?: string;
  steps: WorkflowEvidenceReviewStepDto[];
}

export interface WorkflowSlaBreachApprovalDto {
  approvalId: string;
  workflowInstanceId: string;
  workflowName: string;
  entityType: string;
  entityId: string;
  stepInstanceId: string;
  stepName: string;
  approverId?: string;
  approverName?: string;
  approverRole?: string;
  dueDate?: string;
  hoursOverdue: number;
}

export interface WorkflowEscalationExecutionDto {
  id: string;
  approvalId: string;
  ruleIndex: number;
  action: WorkflowEscalationAction;
  targetUserId?: string;
  targetUserName?: string;
  targetRole?: string;
  executedAt: string;
  result?: string;
  approval?: WorkflowSlaBreachApprovalDto;
}

export interface WorkflowSlaBreachesDto {
  generatedAt: string;
  breaches: WorkflowSlaBreachApprovalDto[];
  escalations: WorkflowEscalationExecutionDto[];
}

export interface WorkflowCorrectionRequestDto {
  id: string;
  workflowInstanceId: string;
  approvalId: string;
  correctionOwnerId: string;
  targetStepInstanceId?: string;
  instructions: string;
  status: number;
  requestedAt: string;
  dueAt?: string;
}

export interface WorkflowEvidencePolicyDto {
  id?: string;
  allowedExtensions: string[];
  maximumFileSizeBytes: number;
  retentionDays: number;
  requireMalwareScan: boolean;
}

export interface WorkflowEvidenceDocumentDto {
  id: string;
  attachmentId: string;
  documentName?: string;
  documentType?: string;
  fileName: string;
  filePath: string;
  sha256: string;
  documentOwnerId: string;
  issueDate?: string;
  expiryDate?: string;
  isExpired: boolean;
  version: number;
  replacesEvidenceId?: string;
  isCurrent: boolean;
  verificationStatus: number;
  verifiedById?: string;
  verifiedAt?: string;
  verificationNotes?: string;
  canVerify?: boolean;
  verificationBlockedReason?: string;
  malwareScanStatus: number;
  retainUntil: string;
  isLegalHold: boolean;
  legalHoldReason?: string;
  legalHoldById?: string;
  legalHoldAt?: string;
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
  MoreInfoRequested = 5,
  Queued = 6,
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

export enum WorkflowApprovalActivationMode {
  Parallel = 0,
  Sequential = 1,
}

export enum WorkflowDefinitionLifecycleStatus {
  Draft = 'Draft',
  Published = 'Published',
  Retired = 'Retired',
}

export enum WorkflowApprovalActorSource {
  PreviousStepActor = 0,
  AnyPreviousApprover = 1,
  SpecificStepActor = 2,
  ContextUser = 3,
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
