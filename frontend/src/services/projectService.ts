const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(isJson: boolean = true): HeadersInit {
  const token =
    typeof window !== 'undefined'
      ? localStorage.getItem('token') || localStorage.getItem('authToken')
      : null;

  return {
    ...(isJson ? { 'Content-Type': 'application/json' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ProjectLookupDto {
  id: string;
  projectCode: string;
  title: string;
  status: string;
  projectTypeName?: string;
  portfolioName?: string;
  programName?: string;
}

export interface ProjectResourceLookupDto {
  id: string;
  displayName: string;
  employeeNumber?: string;
}

export interface ProjectDto {
  id: string;
  projectCode: string;
  title: string;
  status: string;
  summary?: string;
  projectTypeName?: string;
  projectPriorityName?: string;
  portfolioId?: string;
  portfolioName?: string;
  programId?: string;
  programName?: string;
  projectManagerId?: string;
  projectManagerDisplayName?: string;
  sponsorId?: string;
  sponsorDisplayName?: string;
  startDate?: string;
  targetEndDate?: string;
  slackMonths: number;
  trueEndDate?: string;
  estimatedBudget?: number;
  approvedBudget?: number;
  actualCost?: number;
  baseCurrencyCode?: string;
  progressPercent: number;
  openRiskCount: number;
  openIssueCount: number;
  overdueMilestoneCount: number;
  currentWorkflowStepName?: string;
  externalPortalAccessEnabled: boolean;
  externalCollaborationEnabled: boolean;
  createdAt: string;
}

export interface ProjectInitiationVersionDto {
  id: string;
  versionNumber: number;
  changeType: string;
  notes?: string;
  createdAt: string;
}

export interface ProjectMemberDto {
  id: string;
  userId: string;
  userDisplayName?: string;
  role: string;
  isActive: boolean;
  joinedAt: string;
}

export interface ProjectWorkItemDto {
  id: string;
  projectId: string;
  parentId?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  nodeType: string;
  title: string;
  description?: string;
  status: string;
  priority?: string;
  sortOrder: number;
  assignedToUserId?: string;
  assignedToUserDisplayName?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  percentComplete: number;
  isRollupEnabled: boolean;
  effortEstimateHours?: number;
  actualEffortHours?: number;
  baselinePlannedStartDate?: string;
  baselinePlannedEndDate?: string;
  baselineVarianceDays: number;
  isOffBaseline: boolean;
  canExternalUpdate?: boolean;
  canExternalComment?: boolean;
  children: ProjectWorkItemDto[];
}

export interface ProjectMilestoneDto {
  id: string;
  projectId: string;
  workItemId?: string;
  title: string;
  description?: string;
  targetDate: string;
  actualDate?: string;
  status: string;
  requiresApproval: boolean;
  totalWeightPercent: number;
  projectPhaseIds: string[];
  phases: ProjectMilestonePhaseSelectionDto[];
}

export interface ProjectMilestonePhaseSelectionDto {
  projectPhaseId: string;
  phaseName: string;
  phaseCode?: string;
  completionWeightPercent: number;
}

export interface ProjectResourceAllocationDto {
  id: string;
  projectId: string;
  workItemId?: string;
  userId: string;
  userDisplayName?: string;
  allocationRole: string;
  allocationType: string;
  allocationValue: number;
  plannedHours?: number;
  startDate: string;
  endDate: string;
  bookingType: string;
  status: string;
  notes?: string;
  requiredSkills: string[];
  requiredCertifications: string[];
  routingPolicy: string;
  sourceAllocationId?: string;
  replacementAllocationId?: string;
  substitutionReason?: string;
  canSubstitute: boolean;
  hasConflict: boolean;
  capacityUtilizationPercent: number;
  qualificationMatchPercent: number;
  qualificationRisk: string;
  missingSkills: string[];
  missingCertifications: string[];
  recommendedUserId?: string;
  recommendedUserDisplayName?: string;
  routingRecommendation?: string;
}

export interface ProjectRiskDto {
  id: string;
  title: string;
  description?: string;
  ownerId?: string;
  ownerDisplayName?: string;
  status: string;
  category?: string;
  probability: number;
  impact: number;
  exposure: number;
  responseStrategy?: string;
  mitigationPlan?: string;
  dueDate?: string;
}

export interface ProjectIssueDto {
  id: string;
  title: string;
  description?: string;
  ownerId?: string;
  ownerDisplayName?: string;
  status: string;
  severity?: string;
  targetResolutionDate?: string;
  rootCause?: string;
  correctiveAction?: string;
}

export interface ProjectQualityCheckpointDto {
  id: string;
  projectId: string;
  workItemId?: string;
  deliverableId?: string;
  qaOwnerId?: string;
  qaOwnerDisplayName?: string;
  title: string;
  description?: string;
  status: string;
  dueDate?: string;
  requiresQaSignOff: boolean;
  signedOffAt?: string;
  signedOffById?: string;
  signedOffByDisplayName?: string;
  signOffNotes?: string;
}

export interface ProjectNonConformanceDto {
  id: string;
  projectId: string;
  qualityCheckpointId?: string;
  deliverableId?: string;
  ownerId?: string;
  ownerDisplayName?: string;
  title: string;
  description?: string;
  severity: string;
  status: string;
  reportedAt: string;
  targetResolutionDate?: string;
  resolvedAt?: string;
  correctiveAction?: string;
  preventiveAction?: string;
  resolutionNotes?: string;
}

export interface ProjectChangeRequestDto {
  id: string;
  title: string;
  description?: string;
  changeType?: string;
  status: string;
  businessImpact?: string;
  riskImpact?: string;
  costImpact?: number;
  scheduleImpactDays?: number;
}

export interface ProjectBillingScheduleDto {
  id: string;
  projectId: string;
  contractId?: string;
  contractMilestoneId?: string;
  milestoneId?: string;
  name: string;
  billingType: string;
  amount: number;
  billingPercentage?: number;
  billingDate: string;
  status: string;
  description?: string;
  isBillable: boolean;
  contractMilestoneName?: string;
}

export interface ProjectInvoiceRequestDto {
  id: string;
  projectId: string;
  billingScheduleId?: string;
  contractId?: string;
  requestNumber: string;
  requestedAmount: number;
  currency: string;
  status: string;
  requestedAt: string;
  submittedAt?: string;
  externalReference?: string;
  notes?: string;
}

export interface ProjectDocumentDto {
  id: string;
  artifactType: string;
  artifactId?: string;
  documentName: string;
  category: string;
  documentType: string;
  filePath: string;
  publicUrl?: string;
  fileType?: string;
  fileSize?: number;
  versionLabel: string;
  status: string;
  isExternalVisible: boolean;
  createdAt: string;
  artifactLabel?: string;
}

export interface ProjectCommentDto {
  id: string;
  workItemId?: string;
  commentType: string;
  body: string;
  mentionedUsersJson?: string;
  createdAt: string;
  createdBy?: string;
}

export interface ProjectDeliverableDto {
  id: string;
  projectId: string;
  workItemId?: string;
  milestoneId?: string;
  submittedDocumentId?: string;
  title: string;
  description?: string;
  status: string;
  targetDate?: string;
  submittedAt?: string;
  submittedById?: string;
  approvedAt?: string;
  approvedById?: string;
  externalApprovedAt?: string;
  externalApprovedById?: string;
  externalSubmissionAllowed: boolean;
  externalSignOffRequired: boolean;
  isExternalVisible: boolean;
  acceptanceNotes?: string;
  externalApprovalNotes?: string;
  canExternalSubmit?: boolean;
  canExternalApprove?: boolean;
  externalReviews: ProjectDeliverableExternalReviewDto[];
}

export interface CreateProjectDeliverableDto {
  workItemId?: string;
  milestoneId?: string;
  title: string;
  description?: string;
  status?: string;
  targetDate?: string;
  externalSubmissionAllowed?: boolean;
  externalSignOffRequired?: boolean;
  isExternalVisible?: boolean;
}

export interface SubmitProjectDeliverableDto {
  submittedDocumentId?: string;
  notes?: string;
}

export interface ProjectDeliverableExternalReviewDto {
  id: string;
  projectId: string;
  deliverableId: string;
  reviewDate: string;
  reviewedById?: string;
  submittedDocumentId?: string;
  submittedDocumentName?: string;
  decision: string;
  statusSnapshot?: string;
  notes?: string;
}

export interface ProjectTaskDependencyDto {
  id: string;
  projectId: string;
  predecessorWorkItemId: string;
  successorWorkItemId: string;
  dependencyType: string;
  lagDays: number;
  isEnforced: boolean;
}

export interface CreateProjectTaskDependencyDto {
  predecessorWorkItemId: string;
  successorWorkItemId: string;
  dependencyType?: string;
  lagDays?: number;
  isEnforced?: boolean;
}

export interface ProjectInterdependencyDto {
  id: string;
  sourceProjectId: string;
  sourceProjectCode: string;
  sourceProjectTitle: string;
  targetProjectId: string;
  targetProjectCode: string;
  targetProjectTitle: string;
  dependencyType: string;
  status: string;
  impactLevel: string;
  ownerId?: string;
  dueDate?: string;
  title: string;
  description?: string;
  mitigationPlan?: string;
}

export interface CreateProjectInterdependencyDto {
  sourceProjectId: string;
  targetProjectId: string;
  dependencyType: string;
  status: string;
  impactLevel: string;
  ownerId?: string;
  dueDate?: string;
  title: string;
  description?: string;
  mitigationPlan?: string;
}

export interface ProjectBaselineDto {
  id: string;
  projectId: string;
  name: string;
  notes?: string;
  isLocked: boolean;
  createdOn: string;
  snapshotProgressPercent: number;
  snapshotApprovedBudget?: number;
  snapshotFinishDate?: string;
}

export interface CreateProjectBaselineDto {
  name: string;
  notes?: string;
}

export interface ProjectBaselineComparisonDto {
  baselineId: string;
  baselineName: string;
  baselineCreatedOn?: string;
  baselineProgressPercent: number;
  currentProgressPercent: number;
  baselineFinishDate?: string;
  currentFinishDate?: string;
  scheduleVarianceDays: number;
  budgetVariance: number;
  changedWorkItemCount: number;
  changedMilestoneCount: number;
  workItemChanges: ProjectBaselineWorkItemChangeDto[];
  milestoneChanges: ProjectBaselineMilestoneChangeDto[];
}

export interface ProjectBaselineWorkItemChangeDto {
  workItemId: string;
  workItemTitle: string;
  baselinePlannedStartDate?: string;
  currentPlannedStartDate?: string;
  baselinePlannedEndDate?: string;
  currentPlannedEndDate?: string;
  baselinePercentComplete: number;
  currentPercentComplete: number;
  scheduleVarianceDays: number;
}

export interface ProjectBaselineMilestoneChangeDto {
  milestoneId: string;
  milestoneTitle: string;
  baselineTargetDate: string;
  currentTargetDate: string;
  baselineStatus: string;
  currentStatus: string;
  scheduleVarianceDays: number;
}

export interface ProjectTimesheetEntryDto {
  id: string;
  projectId: string;
  projectCode?: string;
  projectTitle?: string;
  workItemId?: string;
  workItemTitle?: string;
  userId: string;
  userDisplayName?: string;
  entryDate: string;
  hours: number;
  isBillable: boolean;
  hourlyRate: number;
  costAmount: number;
  workType: string;
  notes?: string;
  status: string;
  approvedById?: string;
  approvedByDisplayName?: string;
  approvedAt?: string;
  canEdit: boolean;
  canDelete: boolean;
}

export interface CreateProjectTimesheetEntryDto {
  workItemId?: string;
  userId: string;
  entryDate?: string;
  hours: number;
  isBillable?: boolean;
  hourlyRate?: number;
  workType?: string;
  notes?: string;
  status?: string;
}

export interface ProjectExpenseDto {
  id: string;
  projectId: string;
  projectCode?: string;
  projectTitle?: string;
  workItemId?: string;
  workItemTitle?: string;
  userId: string;
  userDisplayName?: string;
  expenseDate: string;
  category: string;
  currency: string;
  amount: number;
  taxAmount: number;
  isBillable: boolean;
  status: string;
  receiptDocumentId?: string;
  notes?: string;
  approvedById?: string;
  approvedByDisplayName?: string;
  approvedAt?: string;
  canEdit: boolean;
  canDelete: boolean;
}

export interface CreateProjectExpenseDto {
  workItemId?: string;
  userId: string;
  expenseDate?: string;
  category?: string;
  currency?: string;
  amount: number;
  taxAmount?: number;
  isBillable?: boolean;
  receiptDocumentId?: string;
  notes?: string;
  status?: string;
}

export interface ProjectApprovalQueueSummaryDto {
  draftCount: number;
  submittedCount: number;
  approvedCount: number;
  rejectedCount: number;
  totalHours: number;
  totalAmount: number;
}

export interface ProjectTimesheetApprovalQueueItemDto {
  entryId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  userId: string;
  workItemId?: string;
  workItemTitle?: string;
  entryDate: string;
  hours: number;
  hourlyRate: number;
  costAmount: number;
  isBillable: boolean;
  workType: string;
  status: string;
  queueStage: string;
  daysOpen: number;
  notes?: string;
}

export interface ProjectExpenseApprovalQueueItemDto {
  expenseId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  userId: string;
  workItemId?: string;
  workItemTitle?: string;
  expenseDate: string;
  category: string;
  currency: string;
  amount: number;
  taxAmount: number;
  totalAmount: number;
  isBillable: boolean;
  status: string;
  queueStage: string;
  daysOpen: number;
  notes?: string;
}

export interface ProjectRevenueRecognitionDto {
  id: string;
  projectId: string;
  invoiceRequestId?: string;
  recognitionPeriod: string;
  recognizedRevenue: number;
  recognizedCost: number;
  grossMargin: number;
  cashCollected: number;
  status: string;
  notes?: string;
}

export interface ProjectAssetLinkDto {
  id: string;
  projectId: string;
  maintenanceAssetId?: string;
  fixedAssetId?: string;
  companyAssetId?: string;
  jobCardId?: string;
  linkType: string;
  status: string;
  notes?: string;
  assetName?: string;
  jobCardNumber?: string;
}

export interface CreateProjectAssetLinkDto {
  maintenanceAssetId?: string;
  fixedAssetId?: string;
  companyAssetId?: string;
  jobCardId?: string;
  linkType?: string;
  status?: string;
  notes?: string;
}

export interface ProjectExternalAccessPolicyDto {
  id: string;
  projectId: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  artifactType: string;
  artifactId?: string;
  artifactLabel?: string;
  accessLevel: string;
  canComment: boolean;
  canUpload: boolean;
  canApprove: boolean;
  notes?: string;
}

export interface CreateProjectExternalAccessPolicyDto {
  businessPartnerId: string;
  artifactType: string;
  artifactId?: string;
  accessLevel?: string;
  canComment?: boolean;
  canUpload?: boolean;
  canApprove?: boolean;
  notes?: string;
}

export interface ProjectDecisionDto {
  id: string;
  projectId: string;
  title: string;
  decisionDate: string;
  approverId?: string;
  approverDisplayName?: string;
  rationale?: string;
  alternativesConsidered?: string;
  impactSummary?: string;
  status: string;
  approvedAt?: string;
}

export interface CreateProjectDecisionDto {
  title: string;
  decisionDate?: string;
  approverId?: string;
  rationale?: string;
  alternativesConsidered?: string;
  impactSummary?: string;
  status?: string;
}

export interface ProjectMeetingMinuteDto {
  id: string;
  projectId: string;
  title: string;
  meetingDate: string;
  facilitatorId?: string;
  facilitatorDisplayName?: string;
  meetingType: string;
  minutes?: string;
  attendeesJson?: string;
}

export interface CreateProjectMeetingMinuteDto {
  title: string;
  meetingDate?: string;
  facilitatorId?: string;
  meetingType?: string;
  minutes?: string;
  attendeesJson?: string;
}

export interface ProjectActionItemDto {
  id: string;
  projectId: string;
  meetingMinuteId?: string;
  workItemId?: string;
  title: string;
  description?: string;
  ownerId?: string;
  ownerDisplayName?: string;
  dueDate?: string;
  completedAt?: string;
  status: string;
  priority: string;
  meetingTitle?: string;
  workItemTitle?: string;
}

export interface CreateProjectActionItemDto {
  meetingMinuteId?: string;
  workItemId?: string;
  title: string;
  description?: string;
  ownerId?: string;
  dueDate?: string;
  status?: string;
  priority?: string;
}

export interface ProjectLessonLearnedDto {
  id: string;
  projectId: string;
  title: string;
  category: string;
  description?: string;
  recommendation?: string;
  appliedPhase?: string;
  visibility: string;
}

export interface CreateProjectLessonLearnedDto {
  title: string;
  category?: string;
  description?: string;
  recommendation?: string;
  appliedPhase?: string;
  visibility?: string;
}

export interface ProjectClosureDto {
  id: string;
  projectId: string;
  status: string;
  submittedAt?: string;
  approvedAt?: string;
  approvedById?: string;
  finalBudget?: number;
  finalCost?: number;
  deliverablesAccepted: boolean;
  tasksCompletedOrWaived: boolean;
  assetsReconciled: boolean;
  openItemsDisposed: boolean;
  closureChecklistJson?: string;
  openItemsDisposition?: string;
  assetReconciliationNotes?: string;
  lessonsLearnedSummary?: string;
  postImplementationReview?: string;
  overrideReason?: string;
  rejectionReason?: string;
}

export interface UpsertProjectClosureDto {
  finalBudget?: number;
  finalCost?: number;
  deliverablesAccepted?: boolean;
  tasksCompletedOrWaived?: boolean;
  assetsReconciled?: boolean;
  openItemsDisposed?: boolean;
  closureChecklistJson?: string;
  openItemsDisposition?: string;
  assetReconciliationNotes?: string;
  lessonsLearnedSummary?: string;
  postImplementationReview?: string;
  overrideReason?: string;
}

export interface ProjectAiInsightDto {
  category: string;
  severity: string;
  title: string;
  recommendation: string;
}

export interface ProjectScheduleAnalysisDto {
  projectId: string;
  dependencyCount: number;
  criticalPathTaskCount: number;
  criticalPathWorkItemIds: string[];
  forecastFinishDate?: string;
  totalSlackDays: number;
  hasCircularDependencies: boolean;
  recalculatedItemCount: number;
  violations: ProjectScheduleViolationDto[];
}

export interface ProjectScheduleViolationDto {
  workItemId: string;
  workItemTitle: string;
  blockingWorkItemId?: string;
  blockingWorkItemTitle?: string;
  dependencyType: string;
  severity: string;
  message: string;
  expectedDate?: string;
}

export interface ProjectMobileAssignmentDto {
  projectId: string;
  workItemId: string;
  projectCode: string;
  projectTitle: string;
  workItemTitle: string;
  status: string;
  percentComplete: number;
  plannedEndDate?: string;
  civilDirectTaskId?: string;
  civilDirectTaskStatus?: string;
  civilDirectTaskRowVersion?: string;
}

export interface ProjectMobileSummaryDto {
  assignmentCount: number;
  overdueCount: number;
  pendingHours: number;
  pendingExpenses: number;
  assignments: ProjectMobileAssignmentDto[];
}

export interface ProjectDevelopmentProfileDto {
  id: string;
  projectId: string;
  deliveryStructure: string;
  developmentType?: string;
  siteName?: string;
  siteAddress?: string;
  landReference?: string;
  procurementRoute?: string;
  contractStrategy?: string;
  consultantTeam?: string;
  fundingArrangement?: string;
  handoverStrategy?: string;
  notes?: string;
}

export interface UpsertProjectDevelopmentProfileDto {
  deliveryStructure: string;
  developmentType?: string;
  siteName?: string;
  siteAddress?: string;
  landReference?: string;
  procurementRoute?: string;
  contractStrategy?: string;
  consultantTeam?: string;
  fundingArrangement?: string;
  handoverStrategy?: string;
  notes?: string;
}

export interface ProjectApprovalRegisterItemDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  approvalType: string;
  title: string;
  authorityName?: string;
  referenceNumber?: string;
  status: string;
  isRequired: boolean;
  submittedDate?: string;
  targetDecisionDate?: string;
  approvedDate?: string;
  expiryDate?: string;
  conditionSummary?: string;
  notes?: string;
}

export interface CreateProjectApprovalRegisterItemDto {
  projectPhaseId?: string;
  approvalType: string;
  title: string;
  authorityName?: string;
  referenceNumber?: string;
  status?: string;
  isRequired?: boolean;
  submittedDate?: string;
  targetDecisionDate?: string;
  approvedDate?: string;
  expiryDate?: string;
  conditionSummary?: string;
  notes?: string;
}

export interface UpdateProjectApprovalRegisterItemDto extends CreateProjectApprovalRegisterItemDto {}

export interface ProjectDrawingDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  supersedesDrawingId?: string;
  supersedesDrawingLabel?: string;
  drawingNumber: string;
  title: string;
  discipline: string;
  revision?: string;
  status: string;
  issuedDate?: string;
  reviewDueDate?: string;
  approvedDate?: string;
  isAsBuilt: boolean;
  responsibleParty?: string;
  notes?: string;
}

export interface CreateProjectDrawingDto {
  projectPhaseId?: string;
  supersedesDrawingId?: string;
  drawingNumber: string;
  title: string;
  discipline?: string;
  revision?: string;
  status?: string;
  issuedDate?: string;
  reviewDueDate?: string;
  approvedDate?: string;
  isAsBuilt?: boolean;
  responsibleParty?: string;
  notes?: string;
}

export interface UpdateProjectDrawingDto extends CreateProjectDrawingDto {}

export interface ProjectSubmittalDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  submittalType: string;
  referenceNumber?: string;
  title: string;
  status: string;
  submittedDate?: string;
  responseDueDate?: string;
  respondedDate?: string;
  submittedByName?: string;
  reviewedByName?: string;
  responsibleParty?: string;
  notes?: string;
}

export interface CreateProjectSubmittalDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  submittalType?: string;
  referenceNumber?: string;
  title: string;
  status?: string;
  submittedDate?: string;
  responseDueDate?: string;
  respondedDate?: string;
  submittedByName?: string;
  reviewedByName?: string;
  responsibleParty?: string;
  notes?: string;
}

export interface UpdateProjectSubmittalDto extends CreateProjectSubmittalDto {}

export interface ProjectRfiDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  referenceNumber?: string;
  subject: string;
  question: string;
  priority: string;
  status: string;
  raisedDate: string;
  responseDueDate?: string;
  respondedDate?: string;
  raisedByName?: string;
  respondedByName?: string;
  impactSummary?: string;
  response?: string;
  notes?: string;
}

export interface CreateProjectRfiDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  referenceNumber?: string;
  subject: string;
  question: string;
  priority?: string;
  status?: string;
  raisedDate?: string;
  responseDueDate?: string;
  respondedDate?: string;
  raisedByName?: string;
  respondedByName?: string;
  impactSummary?: string;
  response?: string;
  notes?: string;
}

export interface UpdateProjectRfiDto extends CreateProjectRfiDto {}

export interface ProjectSiteInstructionDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  instructionType: string;
  referenceNumber?: string;
  title: string;
  description?: string;
  status: string;
  issuedDate: string;
  effectiveDate?: string;
  closedDate?: string;
  issuedByName?: string;
  responsibleParty?: string;
  estimatedCostImpact?: number;
  currency: string;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface CreateProjectSiteInstructionDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  instructionType?: string;
  referenceNumber?: string;
  title: string;
  description?: string;
  status?: string;
  issuedDate?: string;
  effectiveDate?: string;
  closedDate?: string;
  issuedByName?: string;
  responsibleParty?: string;
  estimatedCostImpact?: number;
  currency?: string;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface UpdateProjectSiteInstructionDto extends CreateProjectSiteInstructionDto {}

export interface ProjectVariationOrderDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  referenceNumber?: string;
  title: string;
  description?: string;
  variationType: string;
  status: string;
  requestedDate: string;
  approvedDate?: string;
  implementedDate?: string;
  requestedByName?: string;
  approvedByName?: string;
  estimatedAmount?: number;
  approvedAmount?: number;
  currency: string;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface CreateProjectVariationOrderDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  contractId?: string;
  referenceNumber?: string;
  title: string;
  description?: string;
  variationType?: string;
  status?: string;
  requestedDate?: string;
  approvedDate?: string;
  implementedDate?: string;
  requestedByName?: string;
  approvedByName?: string;
  estimatedAmount?: number;
  approvedAmount?: number;
  currency?: string;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface UpdateProjectVariationOrderDto extends CreateProjectVariationOrderDto {}

export interface ProjectInterimValuationDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  projectMilestoneId?: string;
  projectMilestoneTitle?: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  valuationNumber?: string;
  title: string;
  status: string;
  valuationDate: string;
  grossWorkValue: number;
  materialsOnSiteValue: number;
  variationValue: number;
  retentionPercentage: number;
  retentionAmount: number;
  previousCertifiedAmount: number;
  netValuationAmount: number;
  currency: string;
  notes?: string;
  completedProjectPackageIds: string[];
}

export interface CreateProjectInterimValuationDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  projectMilestoneId?: string;
  contractId?: string;
  valuationNumber?: string;
  title: string;
  status?: string;
  valuationDate?: string;
  grossWorkValue?: number;
  materialsOnSiteValue?: number;
  variationValue?: number;
  retentionPercentage?: number;
  retentionAmount?: number;
  previousCertifiedAmount?: number;
  netValuationAmount?: number;
  currency?: string;
  notes?: string;
  completedProjectPackageIds?: string[];
}

export interface UpdateProjectInterimValuationDto extends CreateProjectInterimValuationDto {}

export interface ProjectPaymentCertificateDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  projectInterimValuationId?: string;
  interimValuationNumber?: string;
  interimValuationTitle?: string;
  certificateNumber?: string;
  title: string;
  status: string;
  issueDate: string;
  paymentDueDate?: string;
  grossCertifiedAmount: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  otherDeductionsAmount: number;
  netCertifiedAmount: number;
  currency: string;
  notes?: string;
}

export interface CreateProjectPaymentCertificateDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  contractId?: string;
  projectInterimValuationId?: string;
  certificateNumber?: string;
  title: string;
  status?: string;
  issueDate?: string;
  paymentDueDate?: string;
  grossCertifiedAmount?: number;
  retentionHeldAmount?: number;
  retentionReleasedAmount?: number;
  otherDeductionsAmount?: number;
  netCertifiedAmount?: number;
  currency?: string;
  notes?: string;
}

export interface UpdateProjectPaymentCertificateDto extends CreateProjectPaymentCertificateDto {}

export interface ProjectExtensionOfTimeDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  projectPackageId?: string;
  projectPackageName?: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  referenceNumber?: string;
  title: string;
  reason?: string;
  status: string;
  requestedDate: string;
  decisionDate?: string;
  daysRequested?: number;
  daysApproved?: number;
  revisedCompletionDate?: string;
  requestedByName?: string;
  decidedByName?: string;
  notes?: string;
}

export interface CreateProjectExtensionOfTimeDto {
  projectPhaseId?: string;
  projectPackageId?: string;
  contractId?: string;
  referenceNumber?: string;
  title: string;
  reason?: string;
  status?: string;
  requestedDate?: string;
  decisionDate?: string;
  daysRequested?: number;
  daysApproved?: number;
  revisedCompletionDate?: string;
  requestedByName?: string;
  decidedByName?: string;
  notes?: string;
}

export interface UpdateProjectExtensionOfTimeDto extends CreateProjectExtensionOfTimeDto {}

export interface ProjectFinalAccountDto {
  id: string;
  projectId: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  status: string;
  settlementDate?: string;
  originalContractValue: number;
  approvedVariationAmount: number;
  claimAmount: number;
  deductionAmount: number;
  adjustmentAmount: number;
  certifiedToDate: number;
  retentionHeldAmount: number;
  retentionReleasedAmount: number;
  finalAccountValue: number;
  finalPaymentAmount: number;
  currency: string;
  notes?: string;
}

export interface UpsertProjectFinalAccountDto {
  contractId?: string;
  status?: string;
  settlementDate?: string;
  originalContractValue?: number;
  approvedVariationAmount?: number;
  certifiedToDate?: number;
  retentionHeldAmount?: number;
  retentionReleasedAmount?: number;
  finalAccountValue?: number;
  currency?: string;
  notes?: string;
}

export interface ProjectBuildingDto {
  id: string;
  projectId: string;
  code?: string;
  name: string;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectBuildingDto {
  code?: string;
  name: string;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectBuildingDto extends CreateProjectBuildingDto {}

export interface ProjectFloorDto {
  id: string;
  projectId: string;
  projectBuildingId?: string;
  projectBuildingCode?: string;
  projectBuildingName?: string;
  code?: string;
  name: string;
  levelNumber?: number;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectFloorDto {
  projectBuildingId?: string;
  code?: string;
  name: string;
  levelNumber?: number;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectFloorDto extends CreateProjectFloorDto {}

export interface ProjectUnitReleaseBatchDto {
  id: string;
  projectId: string;
  projectBuildingId?: string;
  projectBuildingCode?: string;
  projectBuildingName?: string;
  projectFloorId?: string;
  projectFloorCode?: string;
  projectFloorName?: string;
  code?: string;
  name: string;
  status: string;
  plannedReleaseDate?: string;
  actualReleaseDate?: string;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectUnitReleaseBatchDto {
  projectBuildingId?: string;
  projectFloorId?: string;
  code?: string;
  name: string;
  status?: string;
  plannedReleaseDate?: string;
  actualReleaseDate?: string;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectUnitReleaseBatchDto extends CreateProjectUnitReleaseBatchDto {}

export interface ProjectUnitHandoverBatchDto {
  id: string;
  projectId: string;
  projectBuildingId?: string;
  projectBuildingCode?: string;
  projectBuildingName?: string;
  projectFloorId?: string;
  projectFloorCode?: string;
  projectFloorName?: string;
  code?: string;
  name: string;
  status: string;
  plannedHandoverDate?: string;
  actualHandoverDate?: string;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectUnitHandoverBatchDto {
  projectBuildingId?: string;
  projectFloorId?: string;
  code?: string;
  name: string;
  status?: string;
  plannedHandoverDate?: string;
  actualHandoverDate?: string;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectUnitHandoverBatchDto extends CreateProjectUnitHandoverBatchDto {}

export interface ProjectUnitDto {
  id: string;
  projectId: string;
  projectBuildingId?: string;
  projectBuildingCode?: string;
  projectBuildingName?: string;
  projectFloorId?: string;
  projectFloorCode?: string;
  projectFloorName?: string;
  projectUnitReleaseBatchId?: string;
  projectUnitReleaseBatchCode?: string;
  projectUnitReleaseBatchName?: string;
  projectUnitReleaseBatchStatus?: string;
  projectUnitTypeTemplateId?: string;
  projectUnitTypeTemplateName?: string;
  isReleasedForMarket: boolean;
  releasedAt?: string;
  releasedByDisplayName?: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  salesAgreementId?: string;
  salesAgreementNumber?: string;
  salesAgreementTitle?: string;
  salesAgreementType?: string;
  salesAgreementStatus?: string;
  salesOrderId?: string;
  salesOrderNumber?: string;
  salesOrderStatus?: string;
  commercialStatus: string;
  commercialIntent?: string;
  inventoryStatus: string;
  handoverStatus: string;
  code?: string;
  name: string;
  unitType: string;
  status: string;
  blockName?: string;
  floorLabel?: string;
  areaSquareMeters?: number;
  valuationRate?: number;
  basePrice?: number;
  currency: string;
  handoverDate?: string;
  sortOrder: number;
  totalAmenityCost: number;
  notes?: string;
  amenities: ProjectUnitAmenityDto[];
}

export interface EstateManagedAssetDto {
  // Estate/Project integration: this DTO is the receiving shape after Project publishes a completed or market-ready unit.
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  location?: string;
  blockName?: string;
  floorLabel?: string;
  assetType: string;
  status: string;
  sourceType: string;
  landAcquisitionId?: string;
  projectId?: string;
  projectUnitId?: string;
  projectCode?: string;
  projectTitle?: string;
  projectUnitCode?: string;
  unitType?: string;
  areaSquareMeters?: number;
  valuationAmount?: number;
  currency: string;
  isAvailableForLease: boolean;
  isAvailableForSale: boolean;
  isPublishedFromProject: boolean;
  publishedFromProjectAt?: string;
  notes?: string;
}

export interface ProjectUnitAmenityDto {
  id: string;
  inventoryItemId?: string;
  itemCode?: string;
  amenityName: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  sortOrder: number;
}

export interface CreateProjectUnitAmenityDto {
  inventoryItemId?: string;
  itemCode?: string;
  amenityName: string;
  quantity?: number;
  unitCost?: number;
  sortOrder?: number;
}

export interface ProjectReleasedUnitSalesLookupDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectUnitId: string;
  projectUnitCode?: string;
  projectUnitName: string;
  projectUnitType: string;
  projectUnitStatus: string;
  commercialStatus: string;
  commercialIntent?: string;
  handoverStatus: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  areaSquareMeters?: number;
  basePrice?: number;
  currency: string;
  propertyReference?: string;
  suggestedAgreementTitle?: string;
  suggestedAgreementType?: string;
  suggestedLeaseAgreementTitle?: string;
  suggestedLeaseAgreementType?: string;
  suggestedOrderType?: string;
  suggestedPropertyType?: string;
  suggestedPropertyDescription?: string;
  suggestedPropertyLocation?: string;
  suggestedSalesOrderLineDescription?: string;
  canCreateSalesAgreement: boolean;
  canCreateLeaseAgreement: boolean;
  canCreateSalesOrder: boolean;
  salesAgreementNumber?: string;
  salesOrderNumber?: string;
}

export interface CreateProjectUnitDto {
  projectBuildingId?: string;
  projectFloorId?: string;
  projectUnitReleaseBatchId?: string;
  projectUnitTypeTemplateId?: string;
  isReleasedForMarket?: boolean;
  customerBusinessPartnerId?: string;
  salesAgreementId?: string;
  salesOrderId?: string;
  code?: string;
  name: string;
  unitType?: string;
  status?: string;
  blockName?: string;
  floorLabel?: string;
  areaSquareMeters?: number;
  valuationRate?: number;
  basePrice?: number;
  currency?: string;
  handoverDate?: string;
  sortOrder?: number;
  notes?: string;
  amenities?: CreateProjectUnitAmenityDto[];
}

export interface UpdateProjectUnitDto extends CreateProjectUnitDto {}

export interface ProjectCustomerVariationDto {
  id: string;
  projectId: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  salesAgreementId?: string;
  salesAgreementNumber?: string;
  salesAgreementTitle?: string;
  salesOrderId?: string;
  salesOrderNumber?: string;
  salesOrderStatus?: string;
  jobCardId?: string;
  jobCardNumber?: string;
  jobCardTitle?: string;
  workOrderId?: string;
  workOrderNumber?: string;
  workOrderTitle?: string;
  title: string;
  description?: string;
  variationType?: string;
  timing: string;
  status: string;
  requestDate: string;
  targetCompletionDate?: string;
  completedDate?: string;
  estimatedAmount?: number;
  quotedAmount?: number;
  approvedAmount?: number;
  billedAmount?: number;
  currency: string;
  requiresScheduleAdjustment: boolean;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface CreateProjectCustomerVariationDto {
  projectUnitId?: string;
  customerBusinessPartnerId?: string;
  salesAgreementId?: string;
  salesOrderId?: string;
  jobCardId?: string;
  workOrderId?: string;
  title: string;
  description?: string;
  variationType?: string;
  timing?: string;
  status?: string;
  requestDate?: string;
  targetCompletionDate?: string;
  completedDate?: string;
  estimatedAmount?: number;
  quotedAmount?: number;
  approvedAmount?: number;
  billedAmount?: number;
  currency?: string;
  requiresScheduleAdjustment?: boolean;
  scheduleImpactDays?: number;
  notes?: string;
}

export interface UpdateProjectCustomerVariationDto extends CreateProjectCustomerVariationDto {}

export interface CreateProjectMaintenanceFollowThroughDto {
  maintenanceAssetId?: string;
  maintenanceTypeId?: string;
  priorityLevelId?: string;
  workOrderTypeId?: string;
  assignedTechnicianId?: string;
  assignedTeamId?: string;
  billingType?: string;
}

export interface ProjectCommissioningItemDto {
  id: string;
  projectId: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  title: string;
  systemArea?: string;
  status: string;
  requiresRegulatoryInspection: boolean;
  plannedDate?: string;
  completedDate?: string;
  certificateReference?: string;
  responsibleParty?: string;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectCommissioningItemDto {
  projectUnitId?: string;
  title: string;
  systemArea?: string;
  status?: string;
  requiresRegulatoryInspection?: boolean;
  plannedDate?: string;
  completedDate?: string;
  certificateReference?: string;
  responsibleParty?: string;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectCommissioningItemDto extends CreateProjectCommissioningItemDto {}

export interface ProjectHandoverItemDto {
  id: string;
  projectId: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  projectUnitHandoverBatchId?: string;
  projectUnitHandoverBatchCode?: string;
  projectUnitHandoverBatchName?: string;
  projectUnitHandoverBatchStatus?: string;
  handoverType: string;
  title: string;
  status: string;
  responsibleParty?: string;
  referenceNumber?: string;
  targetDate?: string;
  completedDate?: string;
  sortOrder: number;
  notes?: string;
}

export interface CreateProjectHandoverItemDto {
  projectUnitId?: string;
  projectUnitHandoverBatchId?: string;
  handoverType?: string;
  title: string;
  status?: string;
  responsibleParty?: string;
  referenceNumber?: string;
  targetDate?: string;
  completedDate?: string;
  sortOrder?: number;
  notes?: string;
}

export interface UpdateProjectHandoverItemDto extends CreateProjectHandoverItemDto {}

export interface ProjectSnagItemDto {
  id: string;
  projectId: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  title: string;
  description?: string;
  severity: string;
  status: string;
  reportedDate: string;
  targetClosureDate?: string;
  closedDate?: string;
  raisedByName?: string;
  responsibleParty?: string;
  notes?: string;
}

export interface CreateProjectSnagItemDto {
  projectUnitId?: string;
  title: string;
  description?: string;
  severity?: string;
  status?: string;
  reportedDate?: string;
  targetClosureDate?: string;
  closedDate?: string;
  raisedByName?: string;
  responsibleParty?: string;
  notes?: string;
}

export interface UpdateProjectSnagItemDto extends CreateProjectSnagItemDto {}

export interface ProjectDefectLiabilityCaseDto {
  id: string;
  projectId: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  jobCardId?: string;
  jobCardNumber?: string;
  jobCardTitle?: string;
  workOrderId?: string;
  workOrderNumber?: string;
  workOrderTitle?: string;
  title: string;
  description?: string;
  status: string;
  reportedDate: string;
  targetResolutionDate?: string;
  resolvedDate?: string;
  isWarrantyRelated: boolean;
  warrantyCategory?: string;
  warrantyExpiryDate?: string;
  firstResponseDate?: string;
  responseSlaDays?: number;
  resolutionSlaDays?: number;
  rectificationCost?: number;
  chargeableAmount?: number;
  currency: string;
  notes?: string;
}

export interface CreateProjectDefectLiabilityCaseDto {
  projectUnitId?: string;
  customerBusinessPartnerId?: string;
  jobCardId?: string;
  workOrderId?: string;
  title: string;
  description?: string;
  status?: string;
  reportedDate?: string;
  targetResolutionDate?: string;
  resolvedDate?: string;
  isWarrantyRelated?: boolean;
  warrantyCategory?: string;
  warrantyExpiryDate?: string;
  firstResponseDate?: string;
  responseSlaDays?: number;
  resolutionSlaDays?: number;
  rectificationCost?: number;
  chargeableAmount?: number;
  currency?: string;
  notes?: string;
}

export interface UpdateProjectDefectLiabilityCaseDto extends CreateProjectDefectLiabilityCaseDto {}

export interface ProjectCommercialAlertDto {
  severity: string;
  message: string;
}

export interface ProjectPhaseCommercialRollupDto {
  projectPhaseId?: string;
  phaseName: string;
  phaseSortOrder: number;
  packageCount: number;
  boqItemCount: number;
  budgetAmount: number;
  committedAmount: number;
  actualAmount: number;
  forecastAmount: number;
  varianceAmount: number;
}

export interface ProjectCommercialSummaryDto {
  projectId: string;
  currency: string;
  packageConversionBasis: string;
  documentConversionBasis: string;
  missingExchangeRateCount: number;
  hasConversionGaps: boolean;
  estimatedBudget: number;
  approvedBudget: number;
  packageBudgetAmount: number;
  packageCommittedAmount: number;
  packageActualAmount: number;
  packageForecastAmount: number;
  forecastVarianceAmount: number;
  packageCount: number;
  boqItemCount: number;
  unassignedPackageCount: number;
  tenderLinkedPackageCount: number;
  contractLinkedPackageCount: number;
  purchaseRequisitionLinkedPackageCount: number;
  purchaseOrderLinkedPackageCount: number;
  phaseAlignedPackageCount: number;
  phaseLaggingPackageCount: number;
  variationOrderCount: number;
  approvedVariationAmount: number;
  interimValuationCount: number;
  netValuationAmount: number;
  paymentCertificateCount: number;
  netCertifiedAmount: number;
  retentionHeldAmount: number;
  extensionOfTimeCount: number;
  approvedExtensionDays: number;
  finalAccountStatus?: string;
  finalAccountValue?: number;
  phaseRollups: ProjectPhaseCommercialRollupDto[];
  alerts: ProjectCommercialAlertDto[];
}

export interface ProjectPhaseDto {
  id: string;
  projectId: string;
  parentPhaseId?: string;
  code?: string;
  name: string;
  description?: string;
  status: string;
  sortOrder: number;
  isOptional: boolean;
  isStageGateRequired: boolean;
  isTemplateSeeded: boolean;
  completionWeightPercent: number;
  plannedStartDate?: string;
  plannedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  children: ProjectPhaseDto[];
}

export interface CreateProjectPhaseDto {
  parentPhaseId?: string;
  code?: string;
  name: string;
  description?: string;
  status?: string;
  sortOrder?: number;
  isOptional?: boolean;
  isStageGateRequired?: boolean;
  completionWeightPercent?: number;
  plannedStartDate?: string;
  plannedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
}

export interface UpdateProjectPhaseDto extends CreateProjectPhaseDto {
  overrideStageGate?: boolean;
  overrideReason?: string;
}

export interface AdvanceProjectPhaseDto {
  overrideStageGate?: boolean;
  overrideReason?: string;
  startNextPhase?: boolean;
}

export interface ReorderProjectPhasesDto {
  parentPhaseId?: string;
  orderedIds: string[];
}

export interface ProjectPhaseGateRequirementResultDto {
  stageGateRuleId: string;
  ruleCode?: string;
  ruleName: string;
  requirementType: string;
  scope: string;
  minimumCount?: number;
  maximumCount?: number;
  actualCount: number;
  isBlocking: boolean;
  isSatisfied: boolean;
  message?: string;
}

export interface ProjectPhaseGateEvaluationDto {
  projectPhaseId: string;
  projectPhaseCode?: string;
  projectPhaseName: string;
  projectPhaseTemplateId?: string;
  projectPhaseTemplateName?: string;
  isStageGateRequired: boolean;
  hasConfiguredRules: boolean;
  isReady: boolean;
  blockingFailureCount: number;
  requirementResults: ProjectPhaseGateRequirementResultDto[];
}

export interface ProjectPhaseProgressionResultDto {
  action: string;
  message: string;
  overrideUsed: boolean;
  stageGateEvaluated: boolean;
  stageGatePassed: boolean;
  nextPhaseStarted: boolean;
  packageStatusUpdateCount: number;
  phase: ProjectPhaseDto;
  nextPhase?: ProjectPhaseDto;
  gateEvaluation?: ProjectPhaseGateEvaluationDto;
}

export interface ProjectPackageDto {
  id: string;
  projectId: string;
  projectPhaseId?: string;
  projectPhaseName?: string;
  code?: string;
  name: string;
  description?: string;
  packageType: string;
  status: string;
  isPhaseCommerciallyAligned: boolean;
  phaseCommercialSyncStatus: string;
  phaseCommercialSyncMessage?: string;
  recommendedNextStatus?: string;
  recommendedNextAction?: string;
  sortOrder: number;
  completionWeightPercent: number;
  plannedStartDate?: string;
  plannedEndDate?: string;
  procurementRoute?: string;
  contractStrategy?: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  tenderId?: string;
  tenderNumber?: string;
  tenderTitle?: string;
  contractId?: string;
  contractNumber?: string;
  contractTitle?: string;
  procurementPlanItemId?: string;
  procurementPlanItemLabel?: string;
  purchaseRequisitionId?: string;
  purchaseRequisitionNumber?: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
  budgetAmount?: number;
  committedAmount?: number;
  actualAmount?: number;
  forecastAmount?: number;
  currency: string;
  notes?: string;
  boqItems: ProjectBoqItemDto[];
}

export interface CreateProjectPackageDto {
  projectPhaseId?: string;
  code?: string;
  name: string;
  description?: string;
  packageType?: string;
  status?: string;
  sortOrder?: number;
  completionWeightPercent?: number;
  plannedStartDate?: string;
  plannedEndDate?: string;
  procurementRoute?: string;
  contractStrategy?: string;
  businessPartnerId?: string;
  tenderId?: string;
  contractId?: string;
  procurementPlanItemId?: string;
  purchaseRequisitionId?: string;
  purchaseOrderId?: string;
  budgetAmount?: number;
  committedAmount?: number;
  actualAmount?: number;
  forecastAmount?: number;
  currency?: string;
  notes?: string;
}

export interface UpdateProjectPackageDto extends CreateProjectPackageDto {}

export interface ProjectBoqItemDto {
  id: string;
  projectId: string;
  projectPackageId: string;
  packageCode?: string;
  packageName?: string;
  sectionCatalogEntryId?: string;
  sectionCode?: string;
  sectionName?: string;
  tradeCatalogEntryId?: string;
  tradeCode?: string;
  tradeName?: string;
  costCodeCatalogEntryId?: string;
  costCode?: string;
  costCodeName?: string;
  measurementCodeCatalogEntryId?: string;
  measurementStandard?: string;
  measurementCode?: string;
  measurementRule?: string;
  lineNumber?: string;
  itemCode?: string;
  itemType: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  unitRate?: number;
  budgetQuantity?: number;
  budgetUnitRate?: number;
  budgetAmount?: number;
  committedAmount?: number;
  actualAmount?: number;
  forecastAmount?: number;
  currency: string;
  inventoryItemId?: string;
  tenderItemId?: string;
  procurementPlanItemId?: string;
  purchaseRequisitionItemId?: string;
  purchaseOrderItemId?: string;
  notes?: string;
  sortOrder: number;
}

export interface CreateProjectBoqItemDto {
  projectPackageId: string;
  sectionCatalogEntryId?: string;
  tradeCatalogEntryId?: string;
  costCodeCatalogEntryId?: string;
  measurementCodeCatalogEntryId?: string;
  lineNumber?: string;
  itemCode?: string;
  itemType?: string;
  description: string;
  quantity?: number;
  unitOfMeasure?: string;
  unitRate?: number;
  budgetQuantity?: number;
  budgetUnitRate?: number;
  budgetAmount?: number;
  committedAmount?: number;
  actualAmount?: number;
  forecastAmount?: number;
  currency?: string;
  inventoryItemId?: string;
  tenderItemId?: string;
  procurementPlanItemId?: string;
  purchaseRequisitionItemId?: string;
  purchaseOrderItemId?: string;
  notes?: string;
  sortOrder?: number;
}

export interface UpdateProjectBoqItemDto extends CreateProjectBoqItemDto {}

export interface ProjectBoqClassificationOptionDto {
  id: string;
  catalogType: string;
  code: string;
  name: string;
  description?: string;
  standardCode?: string;
  measurementRule?: string;
  defaultUnitOfMeasure?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  sortOrder: number;
}

export interface ProjectBoqClassificationOptionsDto {
  effectiveAtUtc: string;
  sections: ProjectBoqClassificationOptionDto[];
  trades: ProjectBoqClassificationOptionDto[];
  costCodes: ProjectBoqClassificationOptionDto[];
  measurementCodes: ProjectBoqClassificationOptionDto[];
}

export type QuantitySurveyBoqVersionType =
  | 'Original'
  | 'Tender'
  | 'Approved'
  | 'Revised'
  | 'Remeasurement'
  | 'TerminatedRepackaged'
  | 'FinalAccount';

export interface ProjectBoqVersionSummaryDto {
  id: string;
  projectId: string;
  sourceVersionId?: string;
  versionNumber: number;
  versionType: QuantitySurveyBoqVersionType;
  status: string;
  approvalStatus: string;
  workflowInstanceId?: string;
  workflowDefinitionId?: string;
  submittedById?: string;
  submittedAt?: string;
  approvedById?: string;
  approvedAt?: string;
  publishedById?: string;
  publishedAt?: string;
  rejectionReason?: string;
  isPublished: boolean;
  canCurrentUserApprove: boolean;
  changeSummary: string;
  snapshotHash: string;
  lineCount: number;
  snapshotAt: string;
  createdBy?: string;
  createdById?: string;
}

export interface ProjectBoqVersionWorkspaceDto {
  projectId: string;
  workingSetHash: string;
  workingLineCount: number;
  workflowRequired: boolean;
  approvedVersionsImmutable: boolean;
  currentPublishedVersionId?: string;
  allowedVersionTypes: QuantitySurveyBoqVersionType[];
  versions: ProjectBoqVersionSummaryDto[];
}

export interface CreateProjectBoqVersionDto {
  versionType: QuantitySurveyBoqVersionType;
  sourceVersionId?: string;
  expectedWorkingSetHash: string;
  changeSummary: string;
}

export interface ProjectBoqVersionLineDto {
  id: string;
  lineKey: string;
  sourceBoqItemId?: string;
  projectPackageId?: string;
  packageCode?: string;
  packageName?: string;
  sectionCode?: string;
  sectionName?: string;
  tradeCode?: string;
  tradeName?: string;
  costCode?: string;
  costCodeName?: string;
  measurementStandard?: string;
  measurementCode?: string;
  measurementRule?: string;
  lineNumber?: string;
  itemCode?: string;
  itemType: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  unitRate?: number;
  lineAmount?: number;
  currency: string;
  sortOrder: number;
}

export interface ProjectBoqVersionDetailDto extends ProjectBoqVersionSummaryDto {
  lines: ProjectBoqVersionLineDto[];
}

export interface ProjectBoqVersionLineComparisonDto {
  lineKey: string;
  changeType: 'Added' | 'Removed' | 'Changed' | 'Unchanged';
  baselineLine?: ProjectBoqVersionLineDto;
  comparisonLine?: ProjectBoqVersionLineDto;
  quantityDelta: number;
  unitRateDelta?: number;
  amountDelta?: number;
  changedFields: string[];
}

export interface ProjectBoqVersionCurrencyDeltaDto {
  currency: string;
  baselineAmount: number;
  comparisonAmount: number;
  deltaAmount: number;
}

export interface ProjectBoqVersionComparisonDto {
  baseline: ProjectBoqVersionSummaryDto;
  comparison: ProjectBoqVersionSummaryDto;
  addedLineCount: number;
  removedLineCount: number;
  changedLineCount: number;
  unchangedLineCount: number;
  currencyTotals: ProjectBoqVersionCurrencyDeltaDto[];
  lines: ProjectBoqVersionLineComparisonDto[];
}

export interface ProjectBoqRemeasurementMeasurementDto {
  measurementSheetId: string;
  sheetReference: string;
  projectBoqVersionLineId: string;
  boqLineKey: string;
  boqLineLabel: string;
  unitOfMeasure?: string;
  previousQuantity: number;
  measuredQuantity: number;
  measurementDate: string;
  recordedAt: string;
  recordedByName?: string;
}

export interface ProjectBoqRemeasurementWorkspaceDto {
  projectId: string;
  sourceApprovedBoqVersionId: string;
  sourceApprovedBoqVersionNumber: number;
  openCandidateVersionId?: string;
  eligibleMeasurements: ProjectBoqRemeasurementMeasurementDto[];
}

export interface CreateProjectBoqRemeasurementDto {
  clientRequestId: string;
  measurementSheetIds: string[];
  changeSummary: string;
}

export type QuantitySurveyEstimateType =
  'CostPlan' | 'TenderEstimate' | 'BudgetEstimate';

export interface QuantitySurveyEstimateAssumptionRequest {
  code: string;
  description: string;
  value: string;
  unit?: string;
}

export interface QuantitySurveyEstimateMarkupRequest {
  component: number;
  percentage: number;
}

export interface CreateQuantitySurveyEstimateRequest {
  clientRequestId: string;
  projectBoqVersionId: string;
  sourceEstimateVersionId?: string;
  estimateType: QuantitySurveyEstimateType;
  name: string;
  estimateDate?: string;
  changeReason: string;
  centralDocumentVersionId?: string;
  assumptions: QuantitySurveyEstimateAssumptionRequest[];
  markups: QuantitySurveyEstimateMarkupRequest[];
}

export interface QuantitySurveyEstimateLineDto {
  id: string;
  sequence: number;
  projectBoqVersionLineId: string;
  sourceRateId?: string;
  lineNumber: string;
  itemCode?: string;
  description: string;
  unitOfMeasure?: string;
  quantity: number;
  unitRate: number;
  lineAmount: number;
  sourceRateItemCode?: string;
  sourceRateVersion?: number;
  rateSource: string;
}

export interface QuantitySurveyEstimateVersionDto {
  id: string;
  projectId: string;
  projectBoqVersionId: string;
  sourceEstimateVersionId?: string;
  versionNumber: number;
  estimateType: QuantitySurveyEstimateType;
  name: string;
  estimateDate: string;
  currencyId: string;
  currencyCode: string;
  directCost: number;
  markupTotal: number;
  totalAmount: number;
  status: string;
  approvalStatus: string;
  workflowInstanceId?: string;
  submittedAt?: string;
  approvedAt?: string;
  rejectionReason?: string;
  changeReason: string;
  snapshotHash: string;
  configurationProfileVersion: number;
  lines: QuantitySurveyEstimateLineDto[];
  assumptions: Array<{
    id: string;
    sequence: number;
    code: string;
    description: string;
    value: string;
    unit?: string;
  }>;
  markups: Array<{
    id: string;
    sequence: number;
    component: number;
    percentage: number;
    basisAmount: number;
    amount: number;
  }>;
  approvalHistory: Array<{
    id: string;
    action: string;
    actorName: string;
    actorRoles?: string;
    reason?: string;
    createdAt: string;
  }>;
}

export interface QuantitySurveyEstimateWorkspaceDto {
  versions: QuantitySurveyEstimateVersionDto[];
  approvedBoqVersionIds: string[];
  allowedTypes: QuantitySurveyEstimateType[];
}

export interface QuantitySurveyCostReconciliationLineDto {
  sequence: number;
  estimateLineId?: string;
  projectBoqVersionLineId?: string;
  sourceBoqItemId?: string;
  projectPackageId?: string;
  packageCode?: string;
  packageName?: string;
  lineNumber: string;
  itemCode?: string;
  description: string;
  mappingStatus: 'Direct' | 'SnapshotOnly' | 'Unallocated';
  estimateAmount: number;
  approvedBudgetAmount: number;
  committedAmount: number;
  certifiedAmount: number;
  actualAmount: number;
  forecastAmount: number;
  budgetVarianceAmount: number;
  forecastVarianceAmount: number;
}

export interface QuantitySurveyCostReconciliationDto {
  projectId: string;
  estimateVersionId: string;
  estimateName: string;
  estimateType: QuantitySurveyEstimateType;
  estimateVersionNumber: number;
  approvedBudgetRevisionId?: string;
  approvedBudgetRevisionName?: string;
  activeForecastVersionId?: string;
  activeForecastVersionName?: string;
  currencyCode: string;
  generatedAtUtc: string;
  estimateAmount: number;
  approvedBudgetAmount: number;
  committedAmount: number;
  certifiedAmount: number;
  actualAmount: number;
  forecastAmount: number;
  budgetVarianceAmount: number;
  forecastVarianceAmount: number;
  hasUnallocatedAmounts: boolean;
  warnings: string[];
  lines: QuantitySurveyCostReconciliationLineDto[];
}

export interface QuantitySurveyCostDashboardLineDto {
  boqItemId: string;
  projectPackageId: string;
  packageCode?: string;
  packageName?: string;
  sectionCode?: string;
  sectionName?: string;
  costCode?: string;
  costCodeName?: string;
  lineNumber?: string;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  budgetAmount: number;
  committedAmount: number;
  actualAmount: number;
  forecastAmount: number;
  forecastVarianceAmount: number;
}

export interface QuantitySurveyCostDashboardDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  contractId?: string;
  currencyCode: string;
  generatedAtUtc: string;
  approvedBudget: number;
  committedValue: number;
  certifiedValue: number;
  actualCost: number;
  approvedVariationValue: number;
  forecastCost: number;
  finalProjectedCost: number;
  costToComplete: number;
  budgetVariance: number;
  forecastBasis: string;
  hasConversionGaps: boolean;
  missingExchangeRateCount: number;
  warnings: string[];
  lines: QuantitySurveyCostDashboardLineDto[];
}

export interface QuantitySurveyBoqImportIssueDto {
  rowNumber?: number;
  clientLineKey?: string;
  field?: string;
  severity: string;
  code: string;
  message: string;
}

export interface QuantitySurveyBoqImportLinePreviewDto {
  rowNumber: number;
  clientLineKey: string;
  packageCode: string;
  sectionCode?: string;
  tradeCode?: string;
  costCode?: string;
  measurementStandard?: string;
  measurementCode?: string;
  lineNumber?: string;
  itemCode?: string;
  itemType: string;
  description: string;
  unitOfMeasure: string;
  quantity: number;
  unitRate?: number;
  currency: string;
  lineTotal?: number;
}

export interface QuantitySurveyBoqImportPreviewDto {
  sessionId: string;
  projectId: string;
  previewToken: string;
  status: string;
  expiresAt: string;
  lineCount: number;
  errorCount: number;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  lines: QuantitySurveyBoqImportLinePreviewDto[];
  issues: QuantitySurveyBoqImportIssueDto[];
}

export interface QuantitySurveyBoqImportCommitResultDto {
  sessionId: string;
  projectId: string;
  committedLineCount: number;
  committedAt: string;
  reconciledBy: string;
  centralDocumentRecordId?: string;
}

export interface ProjectDetailDto extends ProjectDto {
  projectTypeId?: string;
  projectPriorityId?: string;
  templateId?: string;
  portfolioId?: string;
  programId?: string;
  businessCase?: string;
  objectives?: string;
  strategicAlignment?: string;
  methodology?: string;
  departmentId?: string;
  locationId?: string;
  customerId?: string;
  businessPartnerId?: string;
  contractId?: string;
  tenderId?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  budgetStatus?: string;
  approvalRequired: boolean;
  submittedAt?: string;
  approvedAt?: string;
  scopeStatement?: string;
  assumptions?: string;
  constraints?: string;
  expectedBenefits?: string;
  fundingSource?: string;
  statusRemarks?: string;
  externalPortalAccessEnabled: boolean;
  externalCollaborationEnabled: boolean;
  activeBaselineId?: string;
  activeBaselineName?: string;
  activeBaselineCreatedOn?: string;
  hasLockedBaseline: boolean;
  developmentProfile?: ProjectDevelopmentProfileDto;
  members: ProjectMemberDto[];
  phases: ProjectPhaseDto[];
  packages: ProjectPackageDto[];
  boqItems: ProjectBoqItemDto[];
  approvalRegister: ProjectApprovalRegisterItemDto[];
  drawings: ProjectDrawingDto[];
  submittals: ProjectSubmittalDto[];
  rfis: ProjectRfiDto[];
  siteInstructions: ProjectSiteInstructionDto[];
  variationOrders: ProjectVariationOrderDto[];
  interimValuations: ProjectInterimValuationDto[];
  paymentCertificates: ProjectPaymentCertificateDto[];
  extensionOfTimeRequests: ProjectExtensionOfTimeDto[];
  finalAccount?: ProjectFinalAccountDto | null;
  buildings: ProjectBuildingDto[];
  floors: ProjectFloorDto[];
  unitReleaseBatches: ProjectUnitReleaseBatchDto[];
  unitHandoverBatches: ProjectUnitHandoverBatchDto[];
  units: ProjectUnitDto[];
  customerVariations: ProjectCustomerVariationDto[];
  commissioningItems: ProjectCommissioningItemDto[];
  handoverItems: ProjectHandoverItemDto[];
  snagItems: ProjectSnagItemDto[];
  defectLiabilityCases: ProjectDefectLiabilityCaseDto[];
  workItems: ProjectWorkItemDto[];
  milestones: ProjectMilestoneDto[];
  resourceAllocations: ProjectResourceAllocationDto[];
  risks: ProjectRiskDto[];
  issues: ProjectIssueDto[];
  qualityCheckpoints: ProjectQualityCheckpointDto[];
  nonConformances: ProjectNonConformanceDto[];
  changeRequests: ProjectChangeRequestDto[];
  billingSchedules: ProjectBillingScheduleDto[];
  invoiceRequests: ProjectInvoiceRequestDto[];
  deliverables: ProjectDeliverableDto[];
  taskDependencies: ProjectTaskDependencyDto[];
  baselines: ProjectBaselineDto[];
  timesheetEntries: ProjectTimesheetEntryDto[];
  expenses: ProjectExpenseDto[];
  materialCostEntries: ProjectMaterialCostEntryDto[];
  revenueRecognitions: ProjectRevenueRecognitionDto[];
  assetLinks: ProjectAssetLinkDto[];
  externalAccessPolicies: ProjectExternalAccessPolicyDto[];
  decisions: ProjectDecisionDto[];
  meetings: ProjectMeetingMinuteDto[];
  actionItems: ProjectActionItemDto[];
  lessonsLearned: ProjectLessonLearnedDto[];
  documents: ProjectDocumentDto[];
  comments: ProjectCommentDto[];
  initiationVersions: ProjectInitiationVersionDto[];
  closure?: ProjectClosureDto;
}

export interface ProjectWorkspaceDto {
  project: ProjectDetailDto;
  financialSummary?: ProjectFinancialControlSummaryDto | null;
  integrationSummary?: ProjectIntegrationSummaryDto | null;
  governanceSummary?: ProjectGovernanceSummaryDto | null;
  commercialSummary?: ProjectCommercialSummaryDto | null;
  postHandoverSummary?: ProjectPostHandoverSummaryDto | null;
  linkOptions?: ProjectLinkOptionsDto | null;
  phaseGateEvaluations?: ProjectPhaseGateEvaluationDto[] | null;
}

export interface ProjectPostHandoverSummaryDto {
  projectId: string;
  openHandoverItemCount: number;
  activeDefectLiabilityCount: number;
  warrantyCaseCount: number;
  chargeableCaseCount: number;
  responseBreachCount: number;
  resolutionBreachCount: number;
  warrantyExpiringSoonCount: number;
  totalRectificationExposure: number;
  chargeableExposure: number;
  warrantyExposure: number;
  alerts: ProjectPostHandoverAlertDto[];
}

export interface ProjectPostHandoverAlertDto {
  alertType: string;
  severity: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
  projectDefectLiabilityCaseId?: string;
  title: string;
  message: string;
  dueDate?: string;
}

export interface ProjectSalesAgreementLinkOptionDto {
  id: string;
  businessPartnerId: string;
  documentNumber: string;
  agreementTitle: string;
  customerName: string;
  propertyReference?: string;
  agreementType: string;
  agreementStatus: string;
}

export interface ProjectSalesOrderLinkOptionDto {
  id: string;
  businessPartnerId: string;
  orderNumber: string;
  customerName: string;
  propertyReference?: string;
  status: string;
}

export interface ProjectJobCardLinkOptionDto {
  id: string;
  assetId: string;
  jobCardNumber: string;
  title: string;
  status: string;
  assetName?: string;
}

export interface ProjectWorkOrderLinkOptionDto {
  id: string;
  assetId: string;
  workOrderNumber: string;
  title: string;
  status: string;
  assetName?: string;
  jobCardId?: string;
  jobCardNumber?: string;
}

export interface ProjectLinkOptionsDto {
  salesAgreements: ProjectSalesAgreementLinkOptionDto[];
  salesOrders: ProjectSalesOrderLinkOptionDto[];
  jobCards: ProjectJobCardLinkOptionDto[];
  workOrders: ProjectWorkOrderLinkOptionDto[];
}

export interface CreateProjectDto {
  projectCode?: string;
  title: string;
  summary?: string;
  businessCase?: string;
  objectives?: string;
  strategicAlignment?: string;
  projectTypeId?: string;
  projectPriorityId?: string;
  templateId?: string;
  portfolioId?: string;
  programId?: string;
  methodology?: string;
  sponsorId?: string;
  projectManagerId?: string;
  departmentId?: string;
  locationId?: string;
  customerId?: string;
  businessPartnerId?: string;
  contractId?: string;
  tenderId?: string;
  startDate?: string;
  targetEndDate?: string;
  slackMonths?: number;
  estimatedBudget?: number;
  baseCurrencyCode?: string;
  scopeStatement?: string;
  assumptions?: string;
  constraints?: string;
  expectedBenefits?: string;
  fundingSource?: string;
  approvalRequired?: boolean;
  externalPortalAccessEnabled?: boolean;
  externalCollaborationEnabled?: boolean;
  developmentProfile?: UpsertProjectDevelopmentProfileDto;
}

export interface UpdateProjectDto extends CreateProjectDto {
  approvedBudget?: number;
  actualCost?: number;
  budgetStatus?: string;
  statusRemarks?: string;
}

export interface AddProjectMemberDto {
  userId: string;
  role: string;
}

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface CreateProjectWorkItemDto {
  parentId?: string;
  projectPackageId?: string;
  nodeType: string;
  title: string;
  description?: string;
  status: string;
  priority?: string;
  assignedToUserId?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  percentComplete?: number;
  isRollupEnabled?: boolean;
  effortEstimateHours?: number;
  actualEffortHours?: number;
  scheduleChangeReason?: string;
}

export interface ReorderProjectWorkItemsDto {
  orderedIds: string[];
}

export interface UpdateProjectWorkItemProgressDto {
  status: string;
  percentComplete: number;
  actualStartDate?: string;
  actualEndDate?: string;
  notes?: string;
}

export interface CreateProjectMilestoneDto {
  workItemId?: string;
  title: string;
  description?: string;
  targetDate: string;
  actualDate?: string;
  status?: string;
  requiresApproval?: boolean;
  projectPhaseIds?: string[];
}

export interface CreateProjectResourceAllocationDto {
  workItemId?: string;
  userId: string;
  allocationRole: string;
  allocationType: string;
  allocationValue: number;
  plannedHours?: number;
  startDate: string;
  endDate: string;
  bookingType: string;
  status: string;
  notes?: string;
  requiredSkills?: string[];
  requiredCertifications?: string[];
  routingPolicy?: string;
}

export interface SubstituteProjectResourceAllocationDto {
  replacementUserId: string;
  workItemId?: string;
  transferAllocationValue?: number;
  transferPlannedHours?: number;
  startDate?: string;
  endDate?: string;
  fullReplacement?: boolean;
  approveReplacement?: boolean;
  bookingType?: string;
  reason?: string;
}

export interface ProjectResourceSubstitutionResultDto {
  sourceAllocation: ProjectResourceAllocationDto;
  replacementAllocation: ProjectResourceAllocationDto;
}

export interface CreateProjectRiskDto {
  title: string;
  description?: string;
  ownerId?: string;
  status?: string;
  category?: string;
  probability?: number;
  impact?: number;
  responseStrategy?: string;
  mitigationPlan?: string;
  dueDate?: string;
}

export interface CreateProjectIssueDto {
  title: string;
  description?: string;
  ownerId?: string;
  status?: string;
  severity?: string;
  targetResolutionDate?: string;
  rootCause?: string;
  correctiveAction?: string;
}

export interface CreateProjectQualityCheckpointDto {
  workItemId?: string;
  deliverableId?: string;
  qaOwnerId?: string;
  title: string;
  description?: string;
  status?: string;
  dueDate?: string;
  requiresQaSignOff?: boolean;
}

export interface CreateProjectNonConformanceDto {
  qualityCheckpointId?: string;
  deliverableId?: string;
  ownerId?: string;
  title: string;
  description?: string;
  severity?: string;
  status?: string;
  targetResolutionDate?: string;
  correctiveAction?: string;
  preventiveAction?: string;
}

export interface CreateProjectChangeRequestDto {
  title: string;
  description?: string;
  changeType?: string;
  status?: string;
  businessImpact?: string;
  riskImpact?: string;
  costImpact?: number;
  scheduleImpactDays?: number;
}

export interface CreateProjectBillingScheduleDto {
  contractId?: string;
  contractMilestoneId?: string;
  milestoneId?: string;
  name: string;
  billingType: string;
  amount: number;
  billingPercentage?: number;
  billingDate: string;
  status?: string;
  description?: string;
  isBillable?: boolean;
}

export interface CreateProjectInvoiceRequestDto {
  billingScheduleId?: string;
  contractId?: string;
  requestedAmount: number;
  currency?: string;
  status?: string;
  externalReference?: string;
  notes?: string;
}

export interface UpdateProjectInvoiceRequestWorkflowDto {
  externalReference?: string;
  comments?: string;
}

export interface AttachProjectDocumentDto {
  artifactType?: string;
  artifactId?: string;
  documentName: string;
  category?: string;
  documentType?: string;
  filePath: string;
  publicUrl?: string;
  fileType?: string;
  fileSize?: number;
  versionLabel?: string;
  status?: string;
  effectiveDate?: string;
  isExternalVisible?: boolean;
}

export interface CreateProjectCommentDto {
  workItemId?: string;
  commentType?: string;
  body: string;
  mentionedUsersJson?: string;
}

export interface ProjectTypeDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
  requiresSponsor: boolean;
  requiresApproval: boolean;
  mandatoryFieldsJson?: string;
}

export interface CreateProjectTypeDto {
  code: string;
  name: string;
  description?: string;
  isActive?: boolean;
  requiresSponsor?: boolean;
  requiresApproval?: boolean;
  mandatoryFieldsJson?: string;
}

export interface ProjectPriorityDto {
  id: string;
  code: string;
  name: string;
  colorHex?: string;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateProjectPriorityDto {
  code: string;
  name: string;
  colorHex?: string;
  sortOrder?: number;
  isActive?: boolean;
}

export interface ProjectTemplateDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  projectTypeId?: string;
  projectTypeName?: string;
  versionLabel: string;
  templateDefinitionJson?: string;
  isActive: boolean;
}

export interface CreateProjectTemplateDto {
  code: string;
  name: string;
  description?: string;
  projectTypeId?: string;
  versionLabel?: string;
  templateDefinitionJson?: string;
  isActive?: boolean;
}

export interface ProjectPhaseTemplateDto {
  id: string;
  parentPhaseTemplateId?: string;
  parentPhaseTemplateName?: string;
  projectTypeId?: string;
  projectTypeName?: string;
  code?: string;
  name: string;
  description?: string;
  defaultStatus: string;
  sortOrder: number;
  completionWeightPercent: number;
  isOptional: boolean;
  isStageGateRequired: boolean;
  isActive: boolean;
  appliesToDeliveryStructure?: string;
  appliesToDevelopmentType?: string;
  stageGateRules: ProjectStageGateRuleDto[];
  children: ProjectPhaseTemplateDto[];
}

export interface CreateProjectPhaseTemplateDto {
  parentPhaseTemplateId?: string;
  projectTypeId?: string;
  code?: string;
  name: string;
  description?: string;
  defaultStatus?: string;
  sortOrder?: number;
  completionWeightPercent?: number;
  isOptional?: boolean;
  isStageGateRequired?: boolean;
  isActive?: boolean;
  appliesToDeliveryStructure?: string;
  appliesToDevelopmentType?: string;
}

export interface UpdateProjectPhaseTemplateDto extends CreateProjectPhaseTemplateDto {}

export interface ProjectStageGateRuleDto {
  id: string;
  projectPhaseTemplateId: string;
  projectPhaseTemplateName?: string;
  code: string;
  name: string;
  description?: string;
  requirementType: string;
  scope: string;
  minimumCount?: number;
  maximumCount?: number;
  isBlocking: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateProjectStageGateRuleDto {
  projectPhaseTemplateId: string;
  code: string;
  name: string;
  description?: string;
  requirementType: string;
  scope?: string;
  minimumCount?: number;
  maximumCount?: number;
  isBlocking?: boolean;
  sortOrder?: number;
  isActive?: boolean;
}

export interface UpdateProjectStageGateRuleDto extends CreateProjectStageGateRuleDto {}

export interface ProjectManagementSettingsDto {
  id: string;
  tenantId: string;
  projectNumberFormat: string;
  requireSponsor: boolean;
  defaultApprovalRequired: boolean;
  defaultProjectTypeId?: string;
  defaultProjectPriorityId?: string;
  defaultTemplateId?: string;
  mandatoryFieldsByTypeJson?: string;
  notes?: string;
}

export interface ProjectCatalogItemDto {
  code: string;
  name: string;
}

export interface ProjectCatalogGroupDto {
  key: string;
  displayName: string;
  items: ProjectCatalogItemDto[];
}

export interface ProjectCatalogTypeSummaryDto {
  catalogType: string;
  displayName: string;
  configuredCount: number;
  recommendedCount: number;
}

export interface ProjectMasterDataOverviewDto {
  projectTypeCount: number;
  projectPriorityCount: number;
  projectTemplateCount: number;
  portfolioCount: number;
  programCount: number;
  projectPhaseTemplateCount: number;
  projectUnitTypeTemplateCount: number;
  projectStageGateRuleCount: number;
  recommendedCatalogs: ProjectCatalogGroupDto[];
  catalogCoverage: ProjectCatalogTypeSummaryDto[];
}

export interface ProjectUnitTypeTemplateAmenityDto {
  id: string;
  inventoryItemId: string;
  itemCode?: string;
  amenityName: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  sortOrder: number;
}

export interface CreateProjectUnitTypeTemplateAmenityDto {
  inventoryItemId: string;
  itemCode?: string;
  amenityName?: string;
  quantity?: number;
  unitCost?: number;
  sortOrder?: number;
}

export interface ProjectUnitTypeTemplateDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  defaultProjectUnitType: string;
  sortOrder: number;
  isActive: boolean;
  currency?: string;
  totalCost: number;
  amenities: ProjectUnitTypeTemplateAmenityDto[];
}

export interface CreateProjectUnitTypeTemplateDto {
  code: string;
  name: string;
  description?: string;
  defaultProjectUnitType?: string;
  sortOrder?: number;
  isActive?: boolean;
  currency?: string;
  amenities?: CreateProjectUnitTypeTemplateAmenityDto[];
}

export interface ProjectCatalogEntryDto {
  id: string;
  catalogType: string;
  code: string;
  name: string;
  description?: string;
  standardCode?: string;
  measurementRule?: string;
  defaultUnitOfMeasure?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateProjectCatalogEntryDto {
  catalogType: string;
  code: string;
  name: string;
  description?: string;
  standardCode?: string;
  measurementRule?: string;
  defaultUnitOfMeasure?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  sortOrder?: number;
  isActive?: boolean;
}

export interface ProjectFinancialAlertDto {
  severity: string;
  message: string;
}

export interface ProjectFinancialControlSummaryDto {
  projectId: string;
  estimatedBudget: number;
  approvedBudget: number;
  budgetBaseline: number;
  actualCost: number;
  committedCost: number;
  pendingCost: number;
  forecastCost: number;
  estimateAtCompletion: number;
  remainingBudget: number;
  budgetVariance: number;
  budgetConsumptionPercent: number;
  thresholdWarningPercent: number;
  thresholdCriticalPercent: number;
  procurementRequestedAmount: number;
  procurementCommittedAmount: number;
  procurementOpenCommitmentAmount: number;
  procurementReceivedAmount: number;
  procurementPendingInspectionAmount: number;
  totalExposureAmount: number;
  scheduledBillingAmount: number;
  invoiceRequestedAmount: number;
  recognizedRevenue: number;
  grossMargin: number;
  plannedValue: number;
  earnedValue: number;
  scheduleVariance: number;
  costVariance: number;
  costPerformanceIndex?: number;
  schedulePerformanceIndex?: number;
  toCompletePerformanceIndex?: number;
  profitabilityPercent: number;
  healthStatus: string;
  budgetRevisionCount: number;
  forecastVersionCount: number;
  currentBudgetRevisionName?: string;
  activeForecastVersionName?: string;
  thresholdExceeded: boolean;
  alerts: ProjectFinancialAlertDto[];
}

export interface ProjectBudgetRevisionDto {
  id: string;
  projectId: string;
  versionNumber: number;
  revisionName: string;
  revisionType: string;
  estimatedBudget: number;
  approvedBudget: number;
  committedCost: number;
  forecastCost: number;
  thresholdWarningPercent: number;
  thresholdCriticalPercent: number;
  status: string;
  effectiveDate: string;
  submittedAt?: string;
  approvedAt?: string;
  approvedById?: string;
  changeReason?: string;
  notes?: string;
  rejectionReason?: string;
}

export interface CreateProjectBudgetRevisionDto {
  revisionName: string;
  revisionType?: string;
  estimatedBudget: number;
  approvedBudget: number;
  committedCost: number;
  forecastCost: number;
  thresholdWarningPercent?: number;
  thresholdCriticalPercent?: number;
  effectiveDate?: string;
  changeReason?: string;
  notes?: string;
}

export interface ProjectForecastVersionDto {
  id: string;
  projectId: string;
  versionNumber: number;
  versionName: string;
  asOfDate: string;
  forecastCost: number;
  estimateAtCompletion: number;
  forecastRevenue: number;
  forecastMargin: number;
  isActive: boolean;
  notes?: string;
}

export interface CreateProjectForecastVersionDto {
  versionName: string;
  asOfDate?: string;
  forecastCost: number;
  estimateAtCompletion: number;
  forecastRevenue: number;
  forecastMargin: number;
  isActive?: boolean;
  notes?: string;
}

export interface ProjectIntegrationLinkDto {
  linkType: string;
  status: string;
  reference: string;
}

export interface ProjectIntegrationSummaryDto {
  projectId: string;
  hasBusinessPartner: boolean;
  hasContract: boolean;
  hasTender: boolean;
  hasPortfolio: boolean;
  hasProgram: boolean;
  linkedAssetCount: number;
  resourceAllocationCount: number;
  sharedExternalPolicyCount: number;
  purchaseRequisitionCount: number;
  pendingPurchaseRequisitionCount: number;
  purchaseRequisitionAmount: number;
  purchaseOrderCount: number;
  openPurchaseOrderCount: number;
  purchaseOrderAmount: number;
  purchaseReceiptCount: number;
  pendingPurchaseReceiptInspectionCount: number;
  inventoryRequisitionCount: number;
  pendingInventoryRequisitionCount: number;
  inventoryRequisitionValue: number;
  issuedInventoryRequisitionCount: number;
  issuedInventoryValue: number;
  returnedInventoryValue: number;
  netIssuedInventoryValue: number;
  invoiceRequestCount: number;
  revenueRecognitionCount: number;
  links: ProjectIntegrationLinkDto[];
  warnings: string[];
}

export interface ProjectPolicyViolationDto {
  area: string;
  severity: string;
  message: string;
}

export interface ProjectGovernanceSummaryDto {
  projectId: string;
  openRiskCount: number;
  openIssueCount: number;
  openChangeRequestCount: number;
  pendingDeliverableApprovalCount: number;
  pendingTimesheetApprovalCount: number;
  pendingExpenseApprovalCount: number;
  openActionItemCount: number;
  hasClosureDraft: boolean;
  hasApprovedClosure: boolean;
  hasLockedBaseline: boolean;
  violations: ProjectPolicyViolationDto[];
}

export interface ProjectPortfolioDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  status: string;
  strategicObjective?: string;
  ownerId?: string;
  sponsorId?: string;
  startDate?: string;
  targetEndDate?: string;
  budgetCap?: number;
  programCount: number;
  projectCount: number;
  activeProjectCount: number;
  totalEstimatedBudget: number;
  totalActualCost: number;
}

export interface CreateProjectPortfolioDto {
  code: string;
  name: string;
  description?: string;
  status?: string;
  strategicObjective?: string;
  ownerId?: string;
  sponsorId?: string;
  startDate?: string;
  targetEndDate?: string;
  budgetCap?: number;
}

export interface ProjectProgramDto {
  id: string;
  portfolioId?: string;
  portfolioName?: string;
  code: string;
  name: string;
  description?: string;
  status: string;
  programManagerId?: string;
  sponsorId?: string;
  startDate?: string;
  targetEndDate?: string;
  budgetCap?: number;
  projectCount: number;
  activeProjectCount: number;
  totalEstimatedBudget: number;
  totalActualCost: number;
}

export interface CreateProjectProgramDto {
  code: string;
  name: string;
  description?: string;
  portfolioId?: string;
  status?: string;
  programManagerId?: string;
  sponsorId?: string;
  startDate?: string;
  targetEndDate?: string;
  budgetCap?: number;
}

export interface UpdateProjectManagementSettingsDto {
  projectNumberFormat: string;
  requireSponsor: boolean;
  defaultApprovalRequired: boolean;
  defaultProjectTypeId?: string;
  defaultProjectPriorityId?: string;
  defaultTemplateId?: string;
  mandatoryFieldsByTypeJson?: string;
  notes?: string;
}

export interface ProjectDashboardDto {
  totalProjects: number;
  draftProjects: number;
  activeProjects: number;
  pendingApprovalProjects: number;
  completedProjects: number;
  overdueTasks: number;
  dueMilestonesThisMonth: number;
  overdueMilestones: number;
  openRisks: number;
  openIssues: number;
  totalEstimatedBudget: number;
  totalApprovedBudget: number;
  totalActualCost: number;
  atRiskProjects: ProjectDto[];
}

export interface ProjectTaskAgingReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  workItemId: string;
  workItemTitle: string;
  status: string;
  priority: string;
  plannedEndDate?: string;
  daysOverdue: number;
}

export interface ProjectMilestoneTrackerReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  milestoneId: string;
  milestoneTitle: string;
  status: string;
  targetDate: string;
  isOverdue: boolean;
  daysFromToday: number;
}

export interface ProjectBudgetActualReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  budgetStatus: string;
  estimatedBudget?: number;
  approvedBudget?: number;
  actualCost?: number;
  budgetVariance: number;
  progressPercent: number;
}

export interface ProjectRiskIssueSummaryReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  openRiskCount: number;
  highRiskCount: number;
  openIssueCount: number;
}

export interface ProjectPortfolioSummaryReportItemDto {
  portfolioId: string;
  portfolioCode: string;
  portfolioName: string;
  programCount: number;
  projectCount: number;
  activeProjectCount: number;
  totalEstimatedBudget: number;
  totalActualCost: number;
  highRiskProjectCount: number;
}

export interface ProjectProgramSummaryReportItemDto {
  programId: string;
  portfolioId?: string;
  programCode: string;
  programName: string;
  portfolioName?: string;
  projectCount: number;
  activeProjectCount: number;
  totalEstimatedBudget: number;
  totalActualCost: number;
  averageProgressPercent: number;
}

export interface ProjectPerformanceAnalyticsReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  budgetBaseline: number;
  progressPercent: number;
  earnedValue: number;
  plannedValue: number;
  actualCost: number;
  costPerformanceIndex?: number;
  estimateAtCompletion: number;
  estimateToComplete: number;
  projectedVariance: number;
  healthStatus: string;
}

export interface ProjectPortfolioPrioritizationReportItemDto {
  projectId: string;
  portfolioId?: string;
  programId?: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  portfolioName?: string;
  programName?: string;
  healthStatus: string;
  budgetBaseline: number;
  actualCost: number;
  projectedVariance: number;
  openRiskCount: number;
  openIssueCount: number;
  overdueMilestoneCount: number;
  priorityScore: number;
  priorityBand: string;
  recommendedAction: string;
}

export interface ProjectDependencyWatchReportItemDto {
  interdependencyId: string;
  sourceProjectId: string;
  targetProjectId: string;
  sourceProjectCode: string;
  sourceProjectTitle: string;
  targetProjectCode: string;
  targetProjectTitle: string;
  portfolioId?: string;
  portfolioName?: string;
  programId?: string;
  programName?: string;
  dependencyType: string;
  status: string;
  impactLevel: string;
  title: string;
  dueDate?: string;
  daysToDue: number;
  coordinationState: string;
}

export interface ProjectStrategicInitiativeReportItemDto {
  initiative: string;
  projectCount: number;
  activeProjectCount: number;
  atRiskProjectCount: number;
  delayedProjectCount: number;
  highRiskItemCount: number;
  totalEstimatedBudget: number;
  totalActualCost: number;
  averageProgressPercent: number;
  portfolioNames: string[];
  programNames: string[];
}

export interface ProjectMaterialReconciliationReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  requisitionCount: number;
  pendingRequisitionCount: number;
  issuedRequisitionCount: number;
  requestedValue: number;
  issuedValue: number;
  returnedValue: number;
  netIssuedValue: number;
  trackedMaterialCost: number;
  materialCostVariance: number;
  materialLedgerEntryCount: number;
  missingSourceLinkCount: number;
  reversalGapCount: number;
  reconciliationStatus: string;
}

export interface ProjectProcurementReconciliationReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  purchaseRequisitionCount: number;
  openPurchaseRequisitionCount: number;
  purchaseRequisitionAmount: number;
  purchaseOrderCount: number;
  openPurchaseOrderCount: number;
  purchaseOrderAmount: number;
  purchaseReceiptCount: number;
  receivedAmount: number;
  acceptedReceiptAmount: number;
  pendingInspectionAmount: number;
  supplierReturnAmount: number;
  issuedInventoryValue: number;
  netIssuedInventoryValue: number;
  postedMaterialCost: number;
  receiptToIssueVariance: number;
  issueToPostingVariance: number;
  procurementLedgerEntryCount: number;
  missingSourceLinkCount: number;
  reversalGapCount: number;
  reconciliationStatus: string;
}

export interface ProjectPhaseGateReadinessReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  projectPhaseId: string;
  projectPhaseCode: string;
  projectPhaseName: string;
  projectPhaseSortOrder: number;
  isStageGateRequired: boolean;
  hasConfiguredRules: boolean;
  isReady: boolean;
  configuredRuleCount: number;
  blockingRuleCount: number;
  blockingFailureCount: number;
  satisfiedRuleCount: number;
  gateStatus: string;
  topBlockingMessage?: string | null;
}

export interface ProjectApprovalWatchReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  approvalRegisterItemId: string;
  projectPhaseId?: string | null;
  projectPhaseName?: string | null;
  approvalType: string;
  title: string;
  status: string;
  watchState: string;
  severity: string;
  isRequired: boolean;
  authorityName?: string | null;
  referenceNumber?: string | null;
  submittedDate?: string | null;
  targetDecisionDate?: string | null;
  approvedDate?: string | null;
  expiryDate?: string | null;
  daysToTargetDecision?: number | null;
  daysToExpiry?: number | null;
}

export interface ProjectCommercialAdministrationReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  currency: string;
  packageConversionBasis: string;
  documentConversionBasis: string;
  missingExchangeRateCount: number;
  approvedBudget: number;
  packageForecastAmount: number;
  forecastVarianceAmount: number;
  packageCount: number;
  unassignedPackageCount: number;
  variationOrderCount: number;
  approvedVariationAmount: number;
  interimValuationCount: number;
  netValuationAmount: number;
  paymentCertificateCount: number;
  netCertifiedAmount: number;
  retentionHeldAmount: number;
  extensionOfTimeCount: number;
  approvedExtensionDays: number;
  finalAccountStatus?: string | null;
  alertCount: number;
  watchState: string;
  topAlertMessage?: string | null;
}

export interface ProjectPostHandoverWatchReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  openHandoverItemCount: number;
  activeDefectLiabilityCount: number;
  warrantyCaseCount: number;
  chargeableCaseCount: number;
  responseBreachCount: number;
  resolutionBreachCount: number;
  warrantyExpiringSoonCount: number;
  alertCount: number;
  highestSeverity: string;
  watchState: string;
  totalRectificationExposure: number;
  chargeableExposure: number;
  warrantyExposure: number;
}

export interface ProjectDesignControlReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  itemType: string;
  recordId: string;
  projectPhaseId?: string | null;
  projectPhaseName?: string | null;
  projectPackageId?: string | null;
  projectPackageName?: string | null;
  referenceCode: string;
  title: string;
  category: string;
  status: string;
  watchState: string;
  severity: string;
  actionDueDate?: string | null;
  daysToActionDue?: number | null;
  responsibleParty?: string | null;
  isAsBuilt: boolean;
}

export interface ProjectSiteControlReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  itemType: string;
  recordId: string;
  projectPhaseId?: string | null;
  projectPhaseName?: string | null;
  projectPackageId?: string | null;
  projectPackageName?: string | null;
  referenceCode: string;
  title: string;
  category: string;
  status: string;
  watchState: string;
  severity: string;
  actionDueDate?: string | null;
  daysToActionDue?: number | null;
  responsibleParty?: string | null;
  estimatedCostImpact?: number | null;
  scheduleImpactDays?: number | null;
}

export interface ProjectUnitCommercializationReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectStatus: string;
  projectUnitId: string;
  projectBuildingName?: string | null;
  projectFloorName?: string | null;
  projectUnitReleaseBatchName?: string | null;
  unitCode?: string | null;
  unitName: string;
  unitType: string;
  status: string;
  commercialStatus: string;
  commercialIntent?: string | null;
  handoverStatus: string;
  isReleasedForMarket: boolean;
  releaseState: string;
  customerBusinessPartnerName?: string | null;
  salesAgreementNumber?: string | null;
  salesOrderNumber?: string | null;
  areaSquareMeters?: number | null;
  basePrice?: number | null;
  currency: string;
  watchState: string;
  severity: string;
  watchMessage?: string | null;
}

export interface ProjectMaterialCostEntryDto {
  id: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  entryDate: string;
  entryType: string;
  postingState: string;
  affectsActualCost: boolean;
  isReversed: boolean;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  sourceDocumentNumber?: string;
  sourceTransactionType?: string;
  sourceTransactionId?: string;
  inventoryItemId?: string;
  inventoryItemCode?: string;
  inventoryItemName?: string;
  quantity: number;
  unitOfMeasure?: string;
  unitCost: number;
  amount: number;
  currency: string;
  hasMissingSourceLink: boolean;
  hasReversalGap: boolean;
  notes?: string;
}

export interface ProjectResourceCapacityReportItemDto {
  userId: string;
  userDisplayName?: string;
  allocationCount: number;
  totalAllocatedHours: number;
  totalAllocatedPercent: number;
  standardCapacityHours: number;
  effectiveCapacityHours: number;
  approvedLeaveHours: number;
  approvedLeaveDays: number;
  leaveRequestCount: number;
  capacityUtilizationPercent: number;
  conflictCount: number;
  verifiedSkillCount: number;
  certifiedSkillCount: number;
  expiringCertificationCount: number;
  expiredCertificationCount: number;
  qualificationRisk: string;
  allocationIds: string[];
}

export interface ProjectResourceCapacityRecommendationDto {
  userId: string;
  userDisplayName?: string;
  capacityUtilizationPercent: number;
  effectiveCapacityHours: number;
  approvedLeaveHours: number;
  approvedLeaveDays: number;
  leaveRequestCount: number;
  conflictCount: number;
  verifiedSkillCount: number;
  certifiedSkillCount: number;
  expiringCertificationCount: number;
  expiredCertificationCount: number;
  qualificationRisk: string;
  severity: string;
  recommendation: string;
  suggestedReductionHours: number;
  projectCodes: string[];
  suggestedReplacementUserId?: string;
  suggestedReplacementUserDisplayName?: string;
  matchedSkills: string[];
  affectedAllocationIds: string[];
}

export interface ProjectResourceOptimizationSuggestionDto {
  userId: string;
  userDisplayName?: string;
  suggestedReplacementUserId?: string;
  suggestedReplacementUserDisplayName?: string;
  severity: string;
  recommendation: string;
  matchedSkills: string[];
  matchedSkillCount: number;
  matchedCertifiedSkillCount: number;
  replacementVerifiedSkillCount: number;
  replacementCertifiedSkillCount: number;
  replacementExpiringCertificationCount: number;
  replacementExpiredCertificationCount: number;
  replacementQualificationRisk: string;
  affectedAllocationIds: string[];
}

export interface ProjectBillingSummaryReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  contractId?: string;
  readyBillingScheduleCount: number;
  overdueBillingScheduleCount: number;
  draftInvoiceRequestCount: number;
  submittedInvoiceRequestCount: number;
  sentToFinanceInvoiceRequestCount: number;
  invoicedInvoiceRequestCount: number;
  paidInvoiceRequestCount: number;
  scheduledBillingAmount: number;
  invoiceRequestedAmount: number;
  collectedCashAmount: number;
  unbilledAmount: number;
  billingCoveragePercent: number;
  recognizedRevenue: number;
  revenueCoveragePercent: number;
  revenueGapAmount: number;
  actualCost: number;
  marginAmount: number;
  marginPercent: number;
}

export interface ProjectInvoiceRequestQueueItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  invoiceRequestId: string;
  requestNumber: string;
  billingScheduleId?: string;
  billingScheduleName?: string;
  billingDate?: string;
  contractId?: string;
  status: string;
  requestedAmount: number;
  currency: string;
  requestedAt: string;
  submittedAt?: string;
  externalReference?: string;
  notes?: string;
  daysOutstanding: number;
  queueStage: string;
  canMarkInvoiced: boolean;
  canMarkPaid: boolean;
}

export interface ProjectWorkflowApprovalQueueItemDto {
  entityType: string;
  entityId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  itemTitle: string;
  status: string;
  submittedAt?: string;
  approvedAt?: string;
  daysPending: number;
  queueStage: string;
}

export interface ProjectExternalCollaborationReportItemDto {
  projectId: string;
  projectCode: string;
  projectTitle: string;
  status: string;
  externalPortalAccessEnabled: boolean;
  externalCollaborationEnabled: boolean;
  policyCount: number;
  externalVisibleDocumentCount: number;
  externalVisibleDeliverableCount: number;
  pendingExternalSubmissionCount: number;
  pendingExternalSignOffCount: number;
  externalCommentCount: number;
  lastExternalCommentAt?: string;
  collaborationState: string;
}

export interface ProjectContractLookupDto {
  id: string;
  contractNumber: string;
  contractTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  status: string;
  contractValue: number;
  currency: string;
}

export interface ProjectContractMilestoneLookupDto {
  id: string;
  contractId: string;
  milestoneName: string;
  paymentPercentage: number;
  paymentAmount: number;
  plannedDate?: string;
  status: string;
  invoiceNumber?: string;
}

export interface ProjectTenderLookupDto {
  id: string;
  tenderNumber: string;
  title: string;
  status: string;
  currency?: string;
  estimatedValue?: number;
}

export interface ProjectProcurementPlanItemLookupDto {
  id: string;
  label: string;
  itemDescription: string;
  status: string;
  currency?: string;
  estimatedTotalCost?: number;
}

export interface ProjectPurchaseRequisitionLookupDto {
  id: string;
  requisitionNumber: string;
  status: string;
  currency?: string;
  totalAmount: number;
}

export interface ProjectPurchaseOrderLookupDto {
  id: string;
  orderNumber: string;
  status: string;
  currency?: string;
  totalAmount: number;
  businessPartnerId?: string;
  businessPartnerName?: string;
}

export interface ProjectExternalSummaryDto {
  id: string;
  projectCode: string;
  title: string;
  status: string;
  summary?: string;
  startDate?: string;
  targetEndDate?: string;
  slackMonths: number;
  trueEndDate?: string;
  progressPercent: number;
  externalCollaborationEnabled: boolean;
  openMilestoneCount: number;
}

export interface ProjectExternalDetailDto extends ProjectExternalSummaryDto {
  methodology: string;
  statusRemarks?: string;
  canCollaborate: boolean;
  canComment: boolean;
  canUploadDocuments: boolean;
  actionableWorkItemCount: number;
  blockedWorkItemCount: number;
  pendingExternalSubmissionCount: number;
  pendingExternalSignOffCount: number;
  workItems: ProjectWorkItemDto[];
  milestones: ProjectMilestoneDto[];
  deliverables: ProjectDeliverableDto[];
  documents: ProjectDocumentDto[];
  comments: ProjectCommentDto[];
}

export interface FileUploadResult {
  success: boolean;
  fileName: string;
  originalFileName: string;
  filePath: string;
  publicUrl?: string;
  fileSize: number;
  contentType?: string;
  category: string;
  tenantId?: string;
  uploadedAt: string;
}

class ProjectService {
  async getProjects(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    projectTypeId?: string;
    portfolioId?: string;
    programId?: string;
  }): Promise<PagedResult<ProjectDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', String(params.page));
    if (params?.pageSize)
      queryParams.append('pageSize', String(params.pageSize));
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.projectTypeId)
      queryParams.append('projectTypeId', params.projectTypeId);
    if (params?.portfolioId)
      queryParams.append('portfolioId', params.portfolioId);
    if (params?.programId) queryParams.append('programId', params.programId);

    const response = await fetch(
      `${API_BASE_URL}/projects?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch projects');
    return response.json();
  }

  async getDashboard(): Promise<ProjectDashboardDto> {
    const response = await fetch(`${API_BASE_URL}/projects/dashboard`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project dashboard');
    return response.json();
  }

  async getProjectRegisterReport(params?: {
    search?: string;
    status?: string;
    projectTypeId?: string;
    take?: number;
  }): Promise<ProjectDto[]> {
    const queryParams = new URLSearchParams();
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.projectTypeId)
      queryParams.append('projectTypeId', params.projectTypeId);
    if (params?.take) queryParams.append('take', String(params.take));

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/register?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project register report');
    return response.json();
  }

  async getTaskAgingReport(
    projectId?: string,
    take: number = 100
  ): Promise<ProjectTaskAgingReportItemDto[]> {
    const queryParams = new URLSearchParams();
    if (projectId) queryParams.append('projectId', projectId);
    queryParams.append('take', String(take));

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/task-aging?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch task aging report');
    return response.json();
  }

  async getMilestoneTrackerReport(
    projectId?: string,
    take: number = 100
  ): Promise<ProjectMilestoneTrackerReportItemDto[]> {
    const queryParams = new URLSearchParams();
    if (projectId) queryParams.append('projectId', projectId);
    queryParams.append('take', String(take));

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/milestones?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch milestone tracker report');
    return response.json();
  }

  async getBudgetActualReport(
    take: number = 200
  ): Promise<ProjectBudgetActualReportItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/reports/budget-vs-actual?take=${take}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch budget vs actual report');
    return response.json();
  }

  async getRiskIssueSummaryReport(
    take: number = 200
  ): Promise<ProjectRiskIssueSummaryReportItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/reports/risk-issue-summary?take=${take}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch risk and issue summary report');
    return response.json();
  }

  async getPortfolioSummaryReport(
    take: number = 100
  ): Promise<ProjectPortfolioSummaryReportItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/reports/portfolio-summary?take=${take}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch portfolio summary report');
    return response.json();
  }

  async getProgramSummaryReport(
    portfolioId?: string,
    take: number = 100
  ): Promise<ProjectProgramSummaryReportItemDto[]> {
    const queryParams = new URLSearchParams();
    if (portfolioId) queryParams.append('portfolioId', portfolioId);
    queryParams.append('take', String(take));

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/program-summary?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch program summary report');
    return response.json();
  }

  async getPerformanceAnalyticsReport(
    take: number = 200
  ): Promise<ProjectPerformanceAnalyticsReportItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/reports/performance-analytics?take=${take}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch performance analytics report');
    return response.json();
  }

  async getPortfolioPrioritizationReport(
    portfolioId?: string,
    take: number = 100
  ): Promise<ProjectPortfolioPrioritizationReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (portfolioId) params.set('portfolioId', portfolioId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/portfolio-prioritization?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch portfolio prioritization report');
    return response.json();
  }

  async getDependencyWatchReport(
    portfolioId?: string,
    programId?: string,
    take: number = 100
  ): Promise<ProjectDependencyWatchReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (portfolioId) params.set('portfolioId', portfolioId);
    if (programId) params.set('programId', programId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/dependency-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch dependency watch report');
    return response.json();
  }

  async getStrategicInitiativeReport(
    portfolioId?: string,
    take: number = 100
  ): Promise<ProjectStrategicInitiativeReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (portfolioId) params.set('portfolioId', portfolioId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/strategic-initiatives?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch strategic initiative report');
    return response.json();
  }

  async getMaterialReconciliationReport(
    take: number = 200,
    reconciliationStatus?: string
  ): Promise<ProjectMaterialReconciliationReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (reconciliationStatus)
      params.set('reconciliationStatus', reconciliationStatus);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/material-reconciliation?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch material reconciliation report');
    return response.json();
  }

  async getMaterialCostLedgerReport(
    projectId?: string,
    take: number = 300,
    sourceDocumentType?: string,
    postingState?: string,
    isReversed?: boolean,
    exceptionsOnly?: boolean
  ): Promise<ProjectMaterialCostEntryDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);
    if (sourceDocumentType)
      params.set('sourceDocumentType', sourceDocumentType);
    if (postingState) params.set('postingState', postingState);
    if (typeof isReversed === 'boolean')
      params.set('isReversed', String(isReversed));
    if (typeof exceptionsOnly === 'boolean')
      params.set('exceptionsOnly', String(exceptionsOnly));

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/material-cost-ledger?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch material cost ledger report');
    return response.json();
  }

  async getProcurementReconciliationReport(
    take: number = 200,
    reconciliationStatus?: string
  ): Promise<ProjectProcurementReconciliationReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (reconciliationStatus)
      params.set('reconciliationStatus', reconciliationStatus);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/procurement-reconciliation?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch procurement reconciliation report');
    return response.json();
  }

  async getPhaseGateReadinessReport(
    projectId?: string,
    take: number = 250
  ): Promise<ProjectPhaseGateReadinessReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/phase-gate-readiness?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch phase gate readiness report');
    return response.json();
  }

  async getApprovalWatchReport(
    projectId?: string,
    take: number = 250
  ): Promise<ProjectApprovalWatchReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/approval-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch approval watch report');
    return response.json();
  }

  async getCommercialAdministrationReport(
    projectId?: string,
    take: number = 200
  ): Promise<ProjectCommercialAdministrationReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/construction-commercial?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch construction commercial report');
    return response.json();
  }

  async getPostHandoverWatchReport(
    projectId?: string,
    take: number = 200
  ): Promise<ProjectPostHandoverWatchReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/post-handover-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch post-handover watch report');
    return response.json();
  }

  async getDesignControlWatchReport(
    projectId?: string,
    take: number = 250
  ): Promise<ProjectDesignControlReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/design-control-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch design control watch report');
    return response.json();
  }

  async getSiteControlsWatchReport(
    projectId?: string,
    take: number = 250
  ): Promise<ProjectSiteControlReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/site-controls-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch site controls watch report');
    return response.json();
  }

  async getUnitCommercializationWatchReport(
    projectId?: string,
    take: number = 250
  ): Promise<ProjectUnitCommercializationReportItemDto[]> {
    const params = new URLSearchParams();
    params.set('take', String(take));
    if (projectId) params.set('projectId', projectId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/unit-commercialization-watch?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch unit commercialization watch report');
    return response.json();
  }

  async getResourceCapacityReport(
    startDate?: string,
    endDate?: string,
    userId?: string
  ): Promise<ProjectResourceCapacityReportItemDto[]> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);
    if (userId) queryParams.append('userId', userId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/resource-capacity?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch resource capacity report');
    return response.json();
  }

  async getResourceCapacityRecommendations(
    startDate?: string,
    endDate?: string,
    userId?: string
  ): Promise<ProjectResourceCapacityRecommendationDto[]> {
    const params = new URLSearchParams();
    if (startDate) params.set('startDate', startDate);
    if (endDate) params.set('endDate', endDate);
    if (userId) params.set('userId', userId);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/resource-capacity-recommendations?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch resource capacity recommendations');
    return response.json();
  }

  async getResourceOptimizationSuggestions(
    startDate?: string,
    endDate?: string
  ): Promise<ProjectResourceOptimizationSuggestionDto[]> {
    const params = new URLSearchParams();
    if (startDate) params.set('startDate', startDate);
    if (endDate) params.set('endDate', endDate);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/resource-optimization?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch resource optimization suggestions');
    return response.json();
  }

  async getBillingSummaryReport(
    take: number = 200
  ): Promise<ProjectBillingSummaryReportItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/reports/billing-summary?take=${take}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch billing summary report');
    return response.json();
  }

  async getInvoiceRequestQueueReport(
    take: number = 200,
    status?: string
  ): Promise<ProjectInvoiceRequestQueueItemDto[]> {
    const params = new URLSearchParams({ take: take.toString() });
    if (status) params.set('status', status);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/invoice-request-queue?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch invoice request queue');
    return response.json();
  }

  async getWorkflowApprovalQueueReport(
    take: number = 200,
    entityType?: string
  ): Promise<ProjectWorkflowApprovalQueueItemDto[]> {
    const params = new URLSearchParams({ take: take.toString() });
    if (entityType) params.set('entityType', entityType);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/workflow-approval-queue?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch workflow approval queue');
    return response.json();
  }

  async getExternalCollaborationReport(
    take: number = 200,
    collaborationState?: string
  ): Promise<ProjectExternalCollaborationReportItemDto[]> {
    const params = new URLSearchParams({ take: take.toString() });
    if (collaborationState)
      params.set('collaborationState', collaborationState);

    const response = await fetch(
      `${API_BASE_URL}/projects/reports/external-collaboration?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch external collaboration report');
    return response.json();
  }

  async getContractLookup(
    businessPartnerId?: string,
    search?: string
  ): Promise<ProjectContractLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (businessPartnerId)
      queryParams.append('businessPartnerId', businessPartnerId);
    if (search) queryParams.append('search', search);

    const response = await fetch(
      `${API_BASE_URL}/projects/contracts/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch contract lookup');
    return response.json();
  }

  async getContractMilestones(
    contractId: string
  ): Promise<ProjectContractMilestoneLookupDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/contracts/${contractId}/milestones`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch contract milestones');
    return response.json();
  }

  async getTenderLookup(search?: string): Promise<ProjectTenderLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);

    const response = await fetch(
      `${API_BASE_URL}/projects/tenders/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch tender lookup');
    return response.json();
  }

  async getProcurementPlanItemLookup(
    projectId: string,
    search?: string
  ): Promise<ProjectProcurementPlanItemLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);

    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/procurement-plan-items/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch procurement plan item lookup');
    return response.json();
  }

  async getPurchaseRequisitionLookup(
    projectId: string,
    search?: string
  ): Promise<ProjectPurchaseRequisitionLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);

    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/purchase-requisitions/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase requisition lookup');
    return response.json();
  }

  async getPurchaseOrderLookup(
    projectId: string,
    search?: string
  ): Promise<ProjectPurchaseOrderLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);

    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/purchase-orders/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch purchase order lookup');
    return response.json();
  }

  async lookupProjects(
    search?: string,
    status?: string,
    projectTypeId?: string,
    portfolioId?: string,
    programId?: string
  ): Promise<ProjectLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);
    if (status) queryParams.append('status', status);
    if (projectTypeId) queryParams.append('projectTypeId', projectTypeId);
    if (portfolioId) queryParams.append('portfolioId', portfolioId);
    if (programId) queryParams.append('programId', programId);

    const response = await fetch(
      `${API_BASE_URL}/projects/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to lookup projects');
    return response.json();
  }

  async lookupResources(
    search?: string,
    take: number = 50
  ): Promise<ProjectResourceLookupDto[]> {
    const queryParams = new URLSearchParams();
    if (search) queryParams.append('search', search);
    queryParams.append('take', String(take));

    const response = await fetch(
      `${API_BASE_URL}/projects/resources/lookup?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to lookup resources');
    return response.json();
  }

  async getProjectById(id: string): Promise<ProjectDetailDto> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project');
    return response.json();
  }

  async getProjectWorkspaceById(id: string): Promise<ProjectWorkspaceDto> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/workspace`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project workspace');
    return response.json();
  }

  async getProjectLinkOptions(id: string): Promise<ProjectLinkOptionsDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${id}/link-options`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project link options');
    return response.json();
  }

  async getProjectDevelopmentProfile(
    id: string
  ): Promise<ProjectDevelopmentProfileDto | null> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${id}/development-profile`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project development profile');
    return response.json();
  }

  async upsertProjectDevelopmentProfile(
    id: string,
    dto: UpsertProjectDevelopmentProfileDto
  ): Promise<ProjectDevelopmentProfileDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${id}/development-profile`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project development profile');
    }

    return response.json();
  }

  async getProjectPhases(id: string): Promise<ProjectPhaseDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/phases`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project phases');
    return response.json();
  }

  async getProjectPhaseGateEvaluations(
    id: string
  ): Promise<ProjectPhaseGateEvaluationDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/phase-gates`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok)
      throw new Error('Failed to fetch project phase gate evaluations');
    return response.json();
  }

  async addProjectPhase(
    projectId: string,
    dto: CreateProjectPhaseDto
  ): Promise<ProjectPhaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/phases`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project phase');
    }

    return response.json();
  }

  async updateProjectPhase(
    phaseId: string,
    dto: UpdateProjectPhaseDto
  ): Promise<ProjectPhaseDto> {
    const response = await fetch(`${API_BASE_URL}/projects/phases/${phaseId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project phase');
    }

    return response.json();
  }

  async advanceProjectPhase(
    phaseId: string,
    dto: AdvanceProjectPhaseDto = {}
  ): Promise<ProjectPhaseProgressionResultDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/phases/${phaseId}/advance`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to advance project phase');
    }

    return response.json();
  }

  async reorderProjectPhases(
    projectId: string,
    dto: ReorderProjectPhasesDto
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/phases/reorder`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reorder project phases');
    }
  }

  async deleteProjectPhase(phaseId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/phases/${phaseId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project phase');
    }
  }

  async getProjectPackages(projectId: string): Promise<ProjectPackageDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/packages`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project packages');
    return response.json();
  }

  async addProjectPackage(
    projectId: string,
    dto: CreateProjectPackageDto
  ): Promise<ProjectPackageDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/packages`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project package');
    }

    return response.json();
  }

  async updateProjectPackage(
    packageId: string,
    dto: UpdateProjectPackageDto
  ): Promise<ProjectPackageDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/packages/${packageId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project package');
    }

    return response.json();
  }

  async deleteProjectPackage(packageId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/packages/${packageId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project package');
    }
  }

  async getProjectBoqItems(projectId: string): Promise<ProjectBoqItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-items`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project BOQ items');
    return response.json();
  }

  async getProjectBoqClassifications(
    projectId: string,
    effectiveAtUtc?: string
  ): Promise<ProjectBoqClassificationOptionsDto> {
    const params = new URLSearchParams();
    if (effectiveAtUtc) params.set('effectiveAtUtc', effectiveAtUtc);
    const query = params.toString();
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-classifications${query ? `?${query}` : ''}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project BOQ classification options');
    return response.json();
  }

  async downloadProjectBoqImportTemplate(projectId: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-spreadsheet/template`,
      {
        headers: getAuthHeaders(false),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to download the BoQ import template'
        )
      );
    return response.blob();
  }

  async exportProjectBoqWorkbook(projectId: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-spreadsheet/export`,
      {
        headers: getAuthHeaders(false),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to export the project BoQ'
        )
      );
    return response.blob();
  }

  async previewProjectBoqImport(
    projectId: string,
    file: File
  ): Promise<QuantitySurveyBoqImportPreviewDto> {
    const formData = new FormData();
    formData.append('file', file);
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-spreadsheet/preview`,
      {
        method: 'POST',
        headers: getAuthHeaders(false),
        body: formData,
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to validate the BoQ workbook'
        )
      );
    return response.json();
  }

  async commitProjectBoqImport(
    projectId: string,
    sessionId: string,
    previewToken: string,
    idempotencyKey: string
  ): Promise<QuantitySurveyBoqImportCommitResultDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-spreadsheet/sessions/${sessionId}/commit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({
          previewToken,
          idempotencyKey,
          reconciliationConfirmed: true,
        }),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(response, 'Failed to post the staged BoQ')
      );
    return response.json();
  }

  async downloadProjectBoqValidationReport(
    projectId: string,
    sessionId: string
  ): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-spreadsheet/sessions/${sessionId}/validation-report`,
      {
        headers: getAuthHeaders(false),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to download the validation report'
        )
      );
    return response.blob();
  }

  private async readProblemMessage(
    response: Response,
    fallback: string
  ): Promise<string> {
    const contentType = response.headers.get('content-type') || '';
    if (
      contentType.includes('application/json') ||
      contentType.includes('application/problem+json')
    ) {
      const problem = (await response.json().catch(() => null)) as {
        detail?: string;
        title?: string;
      } | null;
      return problem?.detail || problem?.title || fallback;
    }
    return (await response.text()).trim() || fallback;
  }

  async addProjectBoqItem(
    projectId: string,
    dto: CreateProjectBoqItemDto
  ): Promise<ProjectBoqItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-items`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project BOQ item');
    }

    return response.json();
  }

  async updateProjectBoqItem(
    boqItemId: string,
    dto: UpdateProjectBoqItemDto
  ): Promise<ProjectBoqItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/boq-items/${boqItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project BOQ item');
    }

    return response.json();
  }

  async deleteProjectBoqItem(boqItemId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/boq-items/${boqItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project BOQ item');
    }
  }

  async getProjectBoqVersionWorkspace(
    projectId: string
  ): Promise<ProjectBoqVersionWorkspaceDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-versions`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to load the BoQ version workspace'
        )
      );
    return response.json();
  }

  async getProjectBoqVersion(
    projectId: string,
    versionId: string
  ): Promise<ProjectBoqVersionDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-versions/${versionId}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to load the BoQ version'
        )
      );
    return response.json();
  }

  async createProjectBoqVersion(
    projectId: string,
    dto: CreateProjectBoqVersionDto
  ): Promise<ProjectBoqVersionDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-versions`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to create the BoQ version'
        )
      );
    return response.json();
  }

  async submitProjectBoqVersion(
    projectId: string,
    versionId: string
  ): Promise<ProjectBoqVersionDetailDto> {
    return this.runProjectBoqWorkflowAction(
      projectId,
      versionId,
      'submit',
      undefined,
      'Failed to submit the BoQ version'
    );
  }

  async approveProjectBoqVersion(
    projectId: string,
    versionId: string,
    comments?: string
  ): Promise<ProjectBoqVersionDetailDto> {
    return this.runProjectBoqWorkflowAction(
      projectId,
      versionId,
      'approve',
      { comments: comments?.trim() || undefined },
      'Failed to approve the BoQ version'
    );
  }

  async rejectProjectBoqVersion(
    projectId: string,
    versionId: string,
    reason: string
  ): Promise<ProjectBoqVersionDetailDto> {
    return this.runProjectBoqWorkflowAction(
      projectId,
      versionId,
      'reject',
      { reason: reason.trim() },
      'Failed to reject the BoQ version'
    );
  }

  async recallProjectBoqVersion(
    projectId: string,
    versionId: string,
    reason: string
  ): Promise<ProjectBoqVersionDetailDto> {
    return this.runProjectBoqWorkflowAction(
      projectId,
      versionId,
      'recall',
      { reason: reason.trim() },
      'Failed to recall the BoQ version'
    );
  }

  private async runProjectBoqWorkflowAction(
    projectId: string,
    versionId: string,
    action: 'submit' | 'approve' | 'reject' | 'recall',
    body: Record<string, string | undefined> | undefined,
    fallback: string
  ): Promise<ProjectBoqVersionDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-versions/${versionId}/${action}`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        ...(body ? { body: JSON.stringify(body) } : {}),
      }
    );
    if (!response.ok)
      throw new Error(await this.readProblemMessage(response, fallback));
    return response.json();
  }

  async compareProjectBoqVersions(
    projectId: string,
    baselineVersionId: string,
    comparisonVersionId: string
  ): Promise<ProjectBoqVersionComparisonDto> {
    const params = new URLSearchParams({
      baselineVersionId,
      comparisonVersionId,
    });
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-versions/compare?${params.toString()}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to compare the selected BoQ versions'
        )
      );
    return response.json();
  }

  async getProjectBoqRemeasurementWorkspace(
    projectId: string
  ): Promise<ProjectBoqRemeasurementWorkspaceDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-remeasurements/workspace`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to load the remeasurement workspace'
        )
      );
    return response.json();
  }

  async createProjectBoqRemeasurement(
    projectId: string,
    dto: CreateProjectBoqRemeasurementDto
  ): Promise<ProjectBoqVersionDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/boq-remeasurements`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to create the governed remeasurement revision'
        )
      );
    return response.json();
  }

  async getQuantitySurveyEstimateWorkspace(
    projectId: string
  ): Promise<QuantitySurveyEstimateWorkspaceDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quantity-survey-estimates`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(response, 'Failed to load QS estimates')
      );
    return response.json();
  }

  async createQuantitySurveyEstimate(
    projectId: string,
    dto: CreateQuantitySurveyEstimateRequest
  ): Promise<QuantitySurveyEstimateVersionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quantity-survey-estimates`,
      { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(dto) }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(response, 'Failed to create QS estimate')
      );
    return response.json();
  }

  async getQuantitySurveyCostReconciliation(
    projectId: string,
    estimateVersionId: string
  ): Promise<QuantitySurveyCostReconciliationDto> {
    const params = new URLSearchParams({ estimateVersionId });
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quantity-survey-cost-reconciliation?${params.toString()}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to reconcile the approved estimate'
        )
      );
    return response.json();
  }

  async getQuantitySurveyCostDashboard(
    projectId: string
  ): Promise<QuantitySurveyCostDashboardDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quantity-survey-cost-dashboard`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          'Failed to load the Quantity Survey cost dashboard'
        )
      );
    return response.json();
  }

  async runQuantitySurveyEstimateAction(
    projectId: string,
    estimateVersionId: string,
    action: 'submit' | 'approve' | 'reject',
    body?: { comments?: string; reason?: string }
  ): Promise<QuantitySurveyEstimateVersionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quantity-survey-estimates/${estimateVersionId}/${action}`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        ...(body ? { body: JSON.stringify(body) } : {}),
      }
    );
    if (!response.ok)
      throw new Error(
        await this.readProblemMessage(
          response,
          `Failed to ${action} QS estimate`
        )
      );
    return response.json();
  }

  async getApprovalRegister(
    projectId: string
  ): Promise<ProjectApprovalRegisterItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/approval-register`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project approval register');
    return response.json();
  }

  async addApprovalRegisterItem(
    projectId: string,
    dto: CreateProjectApprovalRegisterItemDto
  ): Promise<ProjectApprovalRegisterItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/approval-register`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project approval register item');
    }

    return response.json();
  }

  async updateApprovalRegisterItem(
    approvalRegisterItemId: string,
    dto: UpdateProjectApprovalRegisterItemDto
  ): Promise<ProjectApprovalRegisterItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/approval-register/${approvalRegisterItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to update project approval register item'
      );
    }

    return response.json();
  }

  async deleteApprovalRegisterItem(
    approvalRegisterItemId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/approval-register/${approvalRegisterItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to delete project approval register item'
      );
    }
  }

  async getProjectDrawings(projectId: string): Promise<ProjectDrawingDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/drawings`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project drawings');
    return response.json();
  }

  async addProjectDrawing(
    projectId: string,
    dto: CreateProjectDrawingDto
  ): Promise<ProjectDrawingDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/drawings`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project drawing');
    }

    return response.json();
  }

  async updateProjectDrawing(
    drawingId: string,
    dto: UpdateProjectDrawingDto
  ): Promise<ProjectDrawingDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/drawings/${drawingId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project drawing');
    }

    return response.json();
  }

  async deleteProjectDrawing(drawingId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/drawings/${drawingId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project drawing');
    }
  }

  async getProjectSubmittals(
    projectId: string
  ): Promise<ProjectSubmittalDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/submittals`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project submittals');
    return response.json();
  }

  async addProjectSubmittal(
    projectId: string,
    dto: CreateProjectSubmittalDto
  ): Promise<ProjectSubmittalDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/submittals`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project submittal');
    }

    return response.json();
  }

  async updateProjectSubmittal(
    submittalId: string,
    dto: UpdateProjectSubmittalDto
  ): Promise<ProjectSubmittalDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/submittals/${submittalId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project submittal');
    }

    return response.json();
  }

  async deleteProjectSubmittal(submittalId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/submittals/${submittalId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project submittal');
    }
  }

  async getProjectRfis(projectId: string): Promise<ProjectRfiDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/${projectId}/rfis`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project RFIs');
    return response.json();
  }

  async addProjectRfi(
    projectId: string,
    dto: CreateProjectRfiDto
  ): Promise<ProjectRfiDto> {
    const response = await fetch(`${API_BASE_URL}/projects/${projectId}/rfis`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project RFI');
    }

    return response.json();
  }

  async updateProjectRfi(
    rfiId: string,
    dto: UpdateProjectRfiDto
  ): Promise<ProjectRfiDto> {
    const response = await fetch(`${API_BASE_URL}/projects/rfis/${rfiId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project RFI');
    }

    return response.json();
  }

  async deleteProjectRfi(rfiId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/rfis/${rfiId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project RFI');
    }
  }

  async getProjectSiteInstructions(
    projectId: string
  ): Promise<ProjectSiteInstructionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/site-instructions`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project site instructions');
    return response.json();
  }

  async addProjectSiteInstruction(
    projectId: string,
    dto: CreateProjectSiteInstructionDto
  ): Promise<ProjectSiteInstructionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/site-instructions`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project site instruction');
    }

    return response.json();
  }

  async updateProjectSiteInstruction(
    siteInstructionId: string,
    dto: UpdateProjectSiteInstructionDto
  ): Promise<ProjectSiteInstructionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/site-instructions/${siteInstructionId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project site instruction');
    }

    return response.json();
  }

  async deleteProjectSiteInstruction(siteInstructionId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/site-instructions/${siteInstructionId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project site instruction');
    }
  }

  async getProjectVariationOrders(
    projectId: string
  ): Promise<ProjectVariationOrderDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/variation-orders`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project variation orders');
    return response.json();
  }

  async addProjectVariationOrder(
    projectId: string,
    dto: CreateProjectVariationOrderDto
  ): Promise<ProjectVariationOrderDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/variation-orders`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project variation order');
    }

    return response.json();
  }

  async updateProjectVariationOrder(
    variationOrderId: string,
    dto: UpdateProjectVariationOrderDto
  ): Promise<ProjectVariationOrderDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/variation-orders/${variationOrderId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project variation order');
    }

    return response.json();
  }

  async deleteProjectVariationOrder(variationOrderId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/variation-orders/${variationOrderId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project variation order');
    }
  }

  async getProjectInterimValuations(
    projectId: string
  ): Promise<ProjectInterimValuationDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/interim-valuations`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project interim valuations');
    return response.json();
  }

  async addProjectInterimValuation(
    projectId: string,
    dto: CreateProjectInterimValuationDto
  ): Promise<ProjectInterimValuationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/interim-valuations`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project interim valuation');
    }

    return response.json();
  }

  async updateProjectInterimValuation(
    interimValuationId: string,
    dto: UpdateProjectInterimValuationDto
  ): Promise<ProjectInterimValuationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/interim-valuations/${interimValuationId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project interim valuation');
    }

    return response.json();
  }

  async deleteProjectInterimValuation(
    interimValuationId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/interim-valuations/${interimValuationId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project interim valuation');
    }
  }

  async getProjectPaymentCertificates(
    projectId: string
  ): Promise<ProjectPaymentCertificateDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/payment-certificates`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project payment certificates');
    return response.json();
  }

  async addProjectPaymentCertificate(
    projectId: string,
    dto: CreateProjectPaymentCertificateDto
  ): Promise<ProjectPaymentCertificateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/payment-certificates`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project payment certificate');
    }

    return response.json();
  }

  async updateProjectPaymentCertificate(
    paymentCertificateId: string,
    dto: UpdateProjectPaymentCertificateDto
  ): Promise<ProjectPaymentCertificateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/payment-certificates/${paymentCertificateId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project payment certificate');
    }

    return response.json();
  }

  async deleteProjectPaymentCertificate(
    paymentCertificateId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/payment-certificates/${paymentCertificateId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project payment certificate');
    }
  }

  async getProjectExtensionOfTimeRequests(
    projectId: string
  ): Promise<ProjectExtensionOfTimeDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/extension-of-time-requests`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project extension of time requests');
    return response.json();
  }

  async addProjectExtensionOfTimeRequest(
    projectId: string,
    dto: CreateProjectExtensionOfTimeDto
  ): Promise<ProjectExtensionOfTimeDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/extension-of-time-requests`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to add project extension of time request'
      );
    }

    return response.json();
  }

  async updateProjectExtensionOfTimeRequest(
    extensionOfTimeId: string,
    dto: UpdateProjectExtensionOfTimeDto
  ): Promise<ProjectExtensionOfTimeDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/extension-of-time-requests/${extensionOfTimeId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to update project extension of time request'
      );
    }

    return response.json();
  }

  async deleteProjectExtensionOfTimeRequest(
    extensionOfTimeId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/extension-of-time-requests/${extensionOfTimeId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to delete project extension of time request'
      );
    }
  }

  async getProjectFinalAccount(
    projectId: string
  ): Promise<ProjectFinalAccountDto | null> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/final-account`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project final account');
    return response.json();
  }

  async upsertProjectFinalAccount(
    projectId: string,
    dto: UpsertProjectFinalAccountDto
  ): Promise<ProjectFinalAccountDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/final-account`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to save project final account');
    }

    return response.json();
  }

  async getProjectBuildings(projectId: string): Promise<ProjectBuildingDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/buildings`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project buildings');
    return response.json();
  }

  async addProjectBuilding(
    projectId: string,
    dto: CreateProjectBuildingDto
  ): Promise<ProjectBuildingDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/buildings`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project building');
    }

    return response.json();
  }

  async updateProjectBuilding(
    buildingId: string,
    dto: UpdateProjectBuildingDto
  ): Promise<ProjectBuildingDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/buildings/${buildingId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project building');
    }

    return response.json();
  }

  async deleteProjectBuilding(buildingId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/buildings/${buildingId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project building');
    }
  }

  async getProjectFloors(projectId: string): Promise<ProjectFloorDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/floors`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project floors');
    return response.json();
  }

  async addProjectFloor(
    projectId: string,
    dto: CreateProjectFloorDto
  ): Promise<ProjectFloorDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/floors`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project floor');
    }

    return response.json();
  }

  async updateProjectFloor(
    floorId: string,
    dto: UpdateProjectFloorDto
  ): Promise<ProjectFloorDto> {
    const response = await fetch(`${API_BASE_URL}/projects/floors/${floorId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project floor');
    }

    return response.json();
  }

  async deleteProjectFloor(floorId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/floors/${floorId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project floor');
    }
  }

  async getProjectUnitReleaseBatches(
    projectId: string
  ): Promise<ProjectUnitReleaseBatchDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/unit-release-batches`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project unit release batches');
    return response.json();
  }

  async addProjectUnitReleaseBatch(
    projectId: string,
    dto: CreateProjectUnitReleaseBatchDto
  ): Promise<ProjectUnitReleaseBatchDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/unit-release-batches`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project unit release batch');
    }

    return response.json();
  }

  async updateProjectUnitReleaseBatch(
    unitReleaseBatchId: string,
    dto: UpdateProjectUnitReleaseBatchDto
  ): Promise<ProjectUnitReleaseBatchDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/unit-release-batches/${unitReleaseBatchId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project unit release batch');
    }

    return response.json();
  }

  async deleteProjectUnitReleaseBatch(
    unitReleaseBatchId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/unit-release-batches/${unitReleaseBatchId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project unit release batch');
    }
  }

  async getProjectUnitHandoverBatches(
    projectId: string
  ): Promise<ProjectUnitHandoverBatchDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/unit-handover-batches`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project unit handover batches');
    return response.json();
  }

  async addProjectUnitHandoverBatch(
    projectId: string,
    dto: CreateProjectUnitHandoverBatchDto
  ): Promise<ProjectUnitHandoverBatchDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/unit-handover-batches`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project unit handover batch');
    }

    return response.json();
  }

  async updateProjectUnitHandoverBatch(
    unitHandoverBatchId: string,
    dto: UpdateProjectUnitHandoverBatchDto
  ): Promise<ProjectUnitHandoverBatchDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/unit-handover-batches/${unitHandoverBatchId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project unit handover batch');
    }

    return response.json();
  }

  async deleteProjectUnitHandoverBatch(
    unitHandoverBatchId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/unit-handover-batches/${unitHandoverBatchId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project unit handover batch');
    }
  }

  async getProjectUnits(projectId: string): Promise<ProjectUnitDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/units`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project units');
    return response.json();
  }

  async getReleasedProjectUnitsForSales(
    search?: string,
    take: number = 50
  ): Promise<ProjectReleasedUnitSalesLookupDto[]> {
    const params = new URLSearchParams();
    params.append('take', String(take));
    if (search?.trim()) {
      params.append('search', search.trim());
    }

    const response = await fetch(
      `${API_BASE_URL}/projects/units/released-market?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      throw new Error('Failed to fetch released project units');
    }

    return response.json();
  }

  async addProjectUnit(
    projectId: string,
    dto: CreateProjectUnitDto
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/units`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project unit');
    }

    return response.json();
  }

  async updateProjectUnit(
    unitId: string,
    dto: UpdateProjectUnitDto
  ): Promise<ProjectUnitDto> {
    const response = await fetch(`${API_BASE_URL}/projects/units/${unitId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project unit');
    }

    return response.json();
  }

  async releaseProjectUnit(unitId: string): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/release`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to release project unit');
    }

    return response.json();
  }

  async withdrawProjectUnitRelease(unitId: string): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/withdraw-release`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to withdraw project unit release');
    }

    return response.json();
  }

  async publishProjectUnitToEstate(
    unitId: string
  ): Promise<EstateManagedAssetDto> {
    // Estate/Project integration: Project triggers the handoff; Estate creates/updates the managed asset record.
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/publish-to-estate`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to publish project unit to Estate management'
      );
    }

    return response.json();
  }

  async createSalesAgreementFromProjectUnit(
    unitId: string
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/create-sales-agreement`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create sales agreement from project unit'
      );
    }

    return response.json();
  }

  async createLeaseAgreementFromProjectUnit(
    unitId: string
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/create-lease-agreement`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create lease agreement from project unit'
      );
    }

    return response.json();
  }

  async createSalesOrderFromProjectUnit(
    unitId: string
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/create-sales-order`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create sales order from project unit'
      );
    }

    return response.json();
  }

  async linkSalesAgreementToProjectUnit(
    unitId: string,
    salesAgreementId: string
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/link-sales-agreement`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ salesAgreementId }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to link sales agreement to project unit'
      );
    }

    return response.json();
  }

  async linkSalesOrderToProjectUnit(
    unitId: string,
    salesOrderId: string
  ): Promise<ProjectUnitDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/units/${unitId}/link-sales-order`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ salesOrderId }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to link sales order to project unit');
    }

    return response.json();
  }

  async deleteProjectUnit(unitId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/units/${unitId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project unit');
    }
  }

  async getCustomerVariations(
    projectId: string
  ): Promise<ProjectCustomerVariationDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/customer-variations`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project customer variations');
    return response.json();
  }

  async addCustomerVariation(
    projectId: string,
    dto: CreateProjectCustomerVariationDto
  ): Promise<ProjectCustomerVariationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/customer-variations`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project customer variation');
    }

    return response.json();
  }

  async updateCustomerVariation(
    variationId: string,
    dto: UpdateProjectCustomerVariationDto
  ): Promise<ProjectCustomerVariationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/customer-variations/${variationId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project customer variation');
    }

    return response.json();
  }

  async createCustomerVariationJobCard(
    variationId: string,
    dto: CreateProjectMaintenanceFollowThroughDto = {}
  ): Promise<ProjectCustomerVariationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/customer-variations/${variationId}/create-job-card`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create job card from project customer variation'
      );
    }

    return response.json();
  }

  async createCustomerVariationWorkOrder(
    variationId: string,
    dto: CreateProjectMaintenanceFollowThroughDto = {}
  ): Promise<ProjectCustomerVariationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/customer-variations/${variationId}/create-work-order`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create work order from project customer variation'
      );
    }

    return response.json();
  }

  async deleteCustomerVariation(variationId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/customer-variations/${variationId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project customer variation');
    }
  }

  async getProjectCommissioningItems(
    projectId: string
  ): Promise<ProjectCommissioningItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/commissioning`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project commissioning items');
    return response.json();
  }

  async addProjectCommissioningItem(
    projectId: string,
    dto: CreateProjectCommissioningItemDto
  ): Promise<ProjectCommissioningItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/commissioning`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project commissioning item');
    }

    return response.json();
  }

  async updateProjectCommissioningItem(
    commissioningItemId: string,
    dto: UpdateProjectCommissioningItemDto
  ): Promise<ProjectCommissioningItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/commissioning/${commissioningItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project commissioning item');
    }

    return response.json();
  }

  async deleteProjectCommissioningItem(
    commissioningItemId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/commissioning/${commissioningItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project commissioning item');
    }
  }

  async getProjectHandoverItems(
    projectId: string
  ): Promise<ProjectHandoverItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/handover-items`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project handover items');
    return response.json();
  }

  async addProjectHandoverItem(
    projectId: string,
    dto: CreateProjectHandoverItemDto
  ): Promise<ProjectHandoverItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/handover-items`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project handover item');
    }

    return response.json();
  }

  async updateProjectHandoverItem(
    handoverItemId: string,
    dto: UpdateProjectHandoverItemDto
  ): Promise<ProjectHandoverItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/handover-items/${handoverItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project handover item');
    }

    return response.json();
  }

  async deleteProjectHandoverItem(handoverItemId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/handover-items/${handoverItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project handover item');
    }
  }

  async getProjectSnagItems(projectId: string): Promise<ProjectSnagItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/snag-items`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project snag items');
    return response.json();
  }

  async addProjectSnagItem(
    projectId: string,
    dto: CreateProjectSnagItemDto
  ): Promise<ProjectSnagItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/snag-items`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project snag item');
    }

    return response.json();
  }

  async updateProjectSnagItem(
    snagItemId: string,
    dto: UpdateProjectSnagItemDto
  ): Promise<ProjectSnagItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/snag-items/${snagItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project snag item');
    }

    return response.json();
  }

  async deleteProjectSnagItem(snagItemId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/snag-items/${snagItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project snag item');
    }
  }

  async getProjectDefectLiabilityCases(
    projectId: string
  ): Promise<ProjectDefectLiabilityCaseDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/defect-liability`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project defect liability cases');
    return response.json();
  }

  async addProjectDefectLiabilityCase(
    projectId: string,
    dto: CreateProjectDefectLiabilityCaseDto
  ): Promise<ProjectDefectLiabilityCaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/defect-liability`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project defect liability case');
    }

    return response.json();
  }

  async updateProjectDefectLiabilityCase(
    defectLiabilityCaseId: string,
    dto: UpdateProjectDefectLiabilityCaseDto
  ): Promise<ProjectDefectLiabilityCaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/defect-liability/${defectLiabilityCaseId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to update project defect liability case'
      );
    }

    return response.json();
  }

  async createDefectLiabilityJobCard(
    defectLiabilityCaseId: string,
    dto: CreateProjectMaintenanceFollowThroughDto = {}
  ): Promise<ProjectDefectLiabilityCaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/defect-liability/${defectLiabilityCaseId}/create-job-card`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to create job card from project defect liability case'
      );
    }

    return response.json();
  }

  async createDefectLiabilityWorkOrder(
    defectLiabilityCaseId: string,
    dto: CreateProjectMaintenanceFollowThroughDto = {}
  ): Promise<ProjectDefectLiabilityCaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/defect-liability/${defectLiabilityCaseId}/create-work-order`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error ||
          'Failed to create work order from project defect liability case'
      );
    }

    return response.json();
  }

  async deleteProjectDefectLiabilityCase(
    defectLiabilityCaseId: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/defect-liability/${defectLiabilityCaseId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to delete project defect liability case'
      );
    }
  }

  async getCommercialSummary(
    projectId: string
  ): Promise<ProjectCommercialSummaryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/commercial-summary`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project commercial summary');
    return response.json();
  }

  async getFinancialControlSummary(
    projectId: string
  ): Promise<ProjectFinancialControlSummaryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/financial-control-summary`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project financial control summary');
    return response.json();
  }

  async getIntegrationSummary(
    projectId: string
  ): Promise<ProjectIntegrationSummaryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/integration-summary`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project integration summary');
    return response.json();
  }

  async getGovernanceSummary(
    projectId: string
  ): Promise<ProjectGovernanceSummaryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/governance-summary`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project governance summary');
    return response.json();
  }

  async createProject(dto: CreateProjectDto): Promise<ProjectDetailDto> {
    const response = await fetch(`${API_BASE_URL}/projects`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project');
    }

    return response.json();
  }

  async updateProject(
    id: string,
    dto: UpdateProjectDto
  ): Promise<ProjectDetailDto> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project');
    }

    return response.json();
  }

  async submitProject(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit project for approval');
    }
  }

  async approveProject(id: string, comments?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ comments }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve project');
    }
  }

  async rejectProject(
    id: string,
    reason: string,
    comments?: string
  ): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/${id}/reject`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason, comments }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject project');
    }
  }

  async getMembers(projectId: string): Promise<ProjectMemberDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/members`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project members');
    return response.json();
  }

  async addMember(
    projectId: string,
    dto: AddProjectMemberDto
  ): Promise<ProjectMemberDto> {
    if (!GUID_PATTERN.test(dto.userId.trim())) {
      throw new Error(
        'Select a valid active user before adding a project member.'
      );
    }
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/members`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project member');
    }

    return response.json();
  }

  async removeMember(memberId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/members/${memberId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to remove project member');
    }
  }

  async addWorkItem(
    projectId: string,
    dto: CreateProjectWorkItemDto
  ): Promise<ProjectWorkItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/work-items`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add work item');
    }

    return response.json();
  }

  async updateWorkItem(
    workItemId: string,
    dto: CreateProjectWorkItemDto
  ): Promise<ProjectWorkItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/work-items/${workItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update work item');
    }

    return response.json();
  }

  async deleteWorkItem(workItemId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/work-items/${workItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete work item');
    }
  }

  async reorderWorkItems(
    projectId: string,
    dto: ReorderProjectWorkItemsDto
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/work-items/reorder`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reorder work items');
    }
  }

  async addMilestone(
    projectId: string,
    dto: CreateProjectMilestoneDto
  ): Promise<ProjectMilestoneDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/milestones`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add milestone');
    }

    return response.json();
  }

  async updateMilestone(
    milestoneId: string,
    dto: CreateProjectMilestoneDto
  ): Promise<ProjectMilestoneDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/milestones/${milestoneId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update milestone');
    }

    return response.json();
  }

  async deleteMilestone(milestoneId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/milestones/${milestoneId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete milestone');
    }
  }

  async getResourceAllocations(
    projectId: string
  ): Promise<ProjectResourceAllocationDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/resource-allocations`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project resource allocations');
    return response.json();
  }

  async addResourceAllocation(
    projectId: string,
    dto: CreateProjectResourceAllocationDto
  ): Promise<ProjectResourceAllocationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/resource-allocations`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add resource allocation');
    }

    return response.json();
  }

  async updateResourceAllocation(
    allocationId: string,
    dto: CreateProjectResourceAllocationDto
  ): Promise<ProjectResourceAllocationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/resource-allocations/${allocationId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update resource allocation');
    }

    return response.json();
  }

  async approveResourceAllocation(
    allocationId: string
  ): Promise<ProjectResourceAllocationDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/resource-allocations/${allocationId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve resource allocation');
    }

    return response.json();
  }

  async substituteResourceAllocation(
    allocationId: string,
    dto: SubstituteProjectResourceAllocationDto
  ): Promise<ProjectResourceSubstitutionResultDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/resource-allocations/${allocationId}/substitute`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to substitute resource allocation');
    }

    return response.json();
  }

  async deleteResourceAllocation(allocationId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/resource-allocations/${allocationId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete resource allocation');
    }
  }

  async addRisk(
    projectId: string,
    dto: CreateProjectRiskDto
  ): Promise<ProjectRiskDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/risks`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add risk');
    }

    return response.json();
  }

  async updateRisk(
    riskId: string,
    dto: CreateProjectRiskDto
  ): Promise<ProjectRiskDto> {
    const response = await fetch(`${API_BASE_URL}/projects/risks/${riskId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update risk');
    }

    return response.json();
  }

  async deleteRisk(riskId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/risks/${riskId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete risk');
    }
  }

  async addIssue(
    projectId: string,
    dto: CreateProjectIssueDto
  ): Promise<ProjectIssueDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/issues`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add issue');
    }

    return response.json();
  }

  async updateIssue(
    issueId: string,
    dto: CreateProjectIssueDto
  ): Promise<ProjectIssueDto> {
    const response = await fetch(`${API_BASE_URL}/projects/issues/${issueId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update issue');
    }

    return response.json();
  }

  async deleteIssue(issueId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/issues/${issueId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete issue');
    }
  }

  async addQualityCheckpoint(
    projectId: string,
    dto: CreateProjectQualityCheckpointDto
  ): Promise<ProjectQualityCheckpointDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/quality-checkpoints`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add quality checkpoint');
    }

    return response.json();
  }

  async signOffQualityCheckpoint(
    checkpointId: string,
    comments?: string
  ): Promise<ProjectQualityCheckpointDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/quality-checkpoints/${checkpointId}/sign-off`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to sign off quality checkpoint');
    }

    return response.json();
  }

  async deleteQualityCheckpoint(checkpointId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/quality-checkpoints/${checkpointId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete quality checkpoint');
    }
  }

  async addNonConformance(
    projectId: string,
    dto: CreateProjectNonConformanceDto
  ): Promise<ProjectNonConformanceDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/non-conformances`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add non-conformance');
    }

    return response.json();
  }

  async resolveNonConformance(
    nonConformanceId: string,
    comments?: string
  ): Promise<ProjectNonConformanceDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/non-conformances/${nonConformanceId}/resolve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to resolve non-conformance');
    }

    return response.json();
  }

  async deleteNonConformance(nonConformanceId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/non-conformances/${nonConformanceId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete non-conformance');
    }
  }

  async addChangeRequest(
    projectId: string,
    dto: CreateProjectChangeRequestDto
  ): Promise<ProjectChangeRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/changes`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add change request');
    }

    return response.json();
  }

  async updateChangeRequest(
    changeId: string,
    dto: CreateProjectChangeRequestDto
  ): Promise<ProjectChangeRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/changes/${changeId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update change request');
    }

    return response.json();
  }

  async deleteChangeRequest(changeId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/changes/${changeId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete change request');
    }
  }

  async getBillingSchedules(
    projectId: string
  ): Promise<ProjectBillingScheduleDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/billing-schedules`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch billing schedules');
    return response.json();
  }

  async addBillingSchedule(
    projectId: string,
    dto: CreateProjectBillingScheduleDto
  ): Promise<ProjectBillingScheduleDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/billing-schedules`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add billing schedule');
    }

    return response.json();
  }

  async updateBillingSchedule(
    billingScheduleId: string,
    dto: CreateProjectBillingScheduleDto
  ): Promise<ProjectBillingScheduleDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/billing-schedules/${billingScheduleId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update billing schedule');
    }

    return response.json();
  }

  async deleteBillingSchedule(billingScheduleId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/billing-schedules/${billingScheduleId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete billing schedule');
    }
  }

  async getInvoiceRequests(
    projectId: string
  ): Promise<ProjectInvoiceRequestDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/invoice-requests`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch invoice requests');
    return response.json();
  }

  async createInvoiceRequest(
    projectId: string,
    dto: CreateProjectInvoiceRequestDto
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/invoice-requests`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create invoice request');
    }

    return response.json();
  }

  async submitInvoiceRequest(
    invoiceRequestId: string,
    comments?: string
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/invoice-requests/${invoiceRequestId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit invoice request');
    }

    return response.json();
  }

  async sendInvoiceRequestToFinance(
    invoiceRequestId: string,
    dto: UpdateProjectInvoiceRequestWorkflowDto = {}
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/invoice-requests/${invoiceRequestId}/send-to-finance`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to send invoice request to finance');
    }

    return response.json();
  }

  async markInvoiceRequestInvoiced(
    invoiceRequestId: string,
    dto: UpdateProjectInvoiceRequestWorkflowDto = {}
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/invoice-requests/${invoiceRequestId}/mark-invoiced`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to mark invoice request as invoiced');
    }

    return response.json();
  }

  async markInvoiceRequestPaid(
    invoiceRequestId: string,
    dto: UpdateProjectInvoiceRequestWorkflowDto = {}
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/invoice-requests/${invoiceRequestId}/mark-paid`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to mark invoice request as paid');
    }

    return response.json();
  }

  async generateInvoiceRequestFromSchedule(
    billingScheduleId: string,
    notes?: string
  ): Promise<ProjectInvoiceRequestDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/billing-schedules/${billingScheduleId}/generate-invoice-request`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ notes }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to generate invoice request');
    }

    return response.json();
  }

  async getDeliverables(projectId: string): Promise<ProjectDeliverableDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/deliverables`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch deliverables');
    return response.json();
  }

  async addDeliverable(
    projectId: string,
    dto: CreateProjectDeliverableDto
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/deliverables`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add deliverable');
    }

    return response.json();
  }

  async updateDeliverable(
    deliverableId: string,
    dto: CreateProjectDeliverableDto
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/deliverables/${deliverableId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update deliverable');
    }

    return response.json();
  }

  async submitDeliverable(
    deliverableId: string,
    dto: SubmitProjectDeliverableDto
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/deliverables/${deliverableId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit deliverable');
    }

    return response.json();
  }

  async approveDeliverable(
    deliverableId: string,
    comments?: string
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/deliverables/${deliverableId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve deliverable');
    }

    return response.json();
  }

  async rejectDeliverable(
    deliverableId: string,
    comments?: string
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/deliverables/${deliverableId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject deliverable');
    }

    return response.json();
  }

  async deleteDeliverable(deliverableId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/deliverables/${deliverableId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete deliverable');
    }
  }

  async getTaskDependencies(
    projectId: string
  ): Promise<ProjectTaskDependencyDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/task-dependencies`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch task dependencies');
    return response.json();
  }

  async addTaskDependency(
    projectId: string,
    dto: CreateProjectTaskDependencyDto
  ): Promise<ProjectTaskDependencyDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/task-dependencies`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add task dependency');
    }

    return response.json();
  }

  async deleteTaskDependency(dependencyId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/task-dependencies/${dependencyId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete task dependency');
    }
  }

  async getInterdependencies(
    projectId?: string,
    portfolioId?: string,
    programId?: string
  ): Promise<ProjectInterdependencyDto[]> {
    const params = new URLSearchParams();
    if (projectId) params.set('projectId', projectId);
    if (portfolioId) params.set('portfolioId', portfolioId);
    if (programId) params.set('programId', programId);

    const response = await fetch(
      `${API_BASE_URL}/projects/interdependencies?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch interdependencies');
    return response.json();
  }

  async createInterdependency(
    dto: CreateProjectInterdependencyDto
  ): Promise<ProjectInterdependencyDto> {
    const response = await fetch(`${API_BASE_URL}/projects/interdependencies`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create interdependency');
    }

    return response.json();
  }

  async updateInterdependency(
    id: string,
    dto: CreateProjectInterdependencyDto
  ): Promise<ProjectInterdependencyDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/interdependencies/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update interdependency');
    }

    return response.json();
  }

  async deleteInterdependency(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/interdependencies/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete interdependency');
    }
  }

  async getBaselines(projectId: string): Promise<ProjectBaselineDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/baselines`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project baselines');
    return response.json();
  }

  async createBaseline(
    projectId: string,
    dto: CreateProjectBaselineDto
  ): Promise<ProjectBaselineDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/baselines`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project baseline');
    }

    return response.json();
  }

  async compareBaseline(
    baselineId: string
  ): Promise<ProjectBaselineComparisonDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/baselines/${baselineId}/compare`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to compare project baseline');
    return response.json();
  }

  async analyzeSchedule(
    projectId: string
  ): Promise<ProjectScheduleAnalysisDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/schedule-analysis`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to analyze project schedule');
    return response.json();
  }

  async recalculateSchedule(
    projectId: string
  ): Promise<ProjectScheduleAnalysisDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/schedule-analysis/recalculate`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to recalculate project schedule');
    }

    return response.json();
  }

  async getTimesheets(projectId: string): Promise<ProjectTimesheetEntryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/timesheets`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project timesheets');
    return response.json();
  }

  async getMyTimesheets(status?: string): Promise<ProjectTimesheetEntryDto[]> {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/my/timesheets${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch my timesheets');
    return response.json();
  }

  async getTimesheetApprovalSummary(
    projectId?: string
  ): Promise<ProjectApprovalQueueSummaryDto> {
    const query = projectId
      ? `?projectId=${encodeURIComponent(projectId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/approval-summary${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch timesheet approval summary');
    return response.json();
  }

  async getTimesheetApprovalQueue(
    projectId?: string,
    status?: string,
    userId?: string,
    take: number = 200
  ): Promise<ProjectTimesheetApprovalQueueItemDto[]> {
    const params = new URLSearchParams({ take: take.toString() });
    if (projectId) params.set('projectId', projectId);
    if (status) params.set('status', status);
    if (userId) params.set('userId', userId);

    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/approval-queue?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch timesheet approval queue');
    return response.json();
  }

  async addTimesheet(
    projectId: string,
    dto: CreateProjectTimesheetEntryDto
  ): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/timesheets`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add timesheet entry');
    }

    return response.json();
  }

  async updateTimesheet(
    entryId: string,
    dto: CreateProjectTimesheetEntryDto
  ): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/${entryId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update timesheet entry');
    }

    return response.json();
  }

  async submitTimesheet(entryId: string): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/${entryId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit timesheet entry');
    }

    return response.json();
  }

  async approveTimesheet(
    entryId: string,
    comments?: string
  ): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/${entryId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve timesheet entry');
    }

    return response.json();
  }

  async rejectTimesheet(
    entryId: string,
    comments?: string
  ): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/${entryId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject timesheet entry');
    }

    return response.json();
  }

  async deleteTimesheet(entryId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/timesheets/${entryId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete timesheet entry');
    }
  }

  async getExpenses(projectId: string): Promise<ProjectExpenseDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/expenses`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project expenses');
    return response.json();
  }

  async getMyExpenses(status?: string): Promise<ProjectExpenseDto[]> {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/my/expenses${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch my project expenses');
    return response.json();
  }

  async getExpenseApprovalSummary(
    projectId?: string
  ): Promise<ProjectApprovalQueueSummaryDto> {
    const query = projectId
      ? `?projectId=${encodeURIComponent(projectId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/approval-summary${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch expense approval summary');
    return response.json();
  }

  async getExpenseApprovalQueue(
    projectId?: string,
    status?: string,
    userId?: string,
    take: number = 200
  ): Promise<ProjectExpenseApprovalQueueItemDto[]> {
    const params = new URLSearchParams({ take: take.toString() });
    if (projectId) params.set('projectId', projectId);
    if (status) params.set('status', status);
    if (userId) params.set('userId', userId);

    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/approval-queue?${params.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch expense approval queue');
    return response.json();
  }

  async addExpense(
    projectId: string,
    dto: CreateProjectExpenseDto
  ): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/expenses`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project expense');
    }

    return response.json();
  }

  async updateExpense(
    expenseId: string,
    dto: CreateProjectExpenseDto
  ): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/${expenseId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update expense');
    }

    return response.json();
  }

  async submitExpense(expenseId: string): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/${expenseId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit expense');
    }

    return response.json();
  }

  async approveExpense(
    expenseId: string,
    comments?: string
  ): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/${expenseId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve project expense');
    }

    return response.json();
  }

  async rejectExpense(
    expenseId: string,
    comments?: string
  ): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/${expenseId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject expense');
    }

    return response.json();
  }

  async deleteExpense(expenseId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/expenses/${expenseId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete expense');
    }
  }

  async getRevenueRecognition(
    projectId: string
  ): Promise<ProjectRevenueRecognitionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/revenue-recognition`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch revenue recognition');
    return response.json();
  }

  async generateRevenueRecognition(
    projectId: string
  ): Promise<ProjectRevenueRecognitionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/revenue-recognition/generate`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to generate revenue recognition');
    }

    return response.json();
  }

  async getBudgetRevisions(
    projectId: string
  ): Promise<ProjectBudgetRevisionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/budget-revisions`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project budget revisions');
    return response.json();
  }

  async createBudgetRevision(
    projectId: string,
    dto: CreateProjectBudgetRevisionDto
  ): Promise<ProjectBudgetRevisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/budget-revisions`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project budget revision');
    }

    return response.json();
  }

  async submitBudgetRevision(
    revisionId: string
  ): Promise<ProjectBudgetRevisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/budget-revisions/${revisionId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit project budget revision');
    }

    return response.json();
  }

  async approveBudgetRevision(
    revisionId: string,
    comments?: string
  ): Promise<ProjectBudgetRevisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/budget-revisions/${revisionId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve project budget revision');
    }

    return response.json();
  }

  async rejectBudgetRevision(
    revisionId: string,
    reason: string,
    comments?: string
  ): Promise<ProjectBudgetRevisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/budget-revisions/${revisionId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ reason, comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject project budget revision');
    }

    return response.json();
  }

  async getForecastVersions(
    projectId: string
  ): Promise<ProjectForecastVersionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/forecast-versions`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project forecast versions');
    return response.json();
  }

  async createForecastVersion(
    projectId: string,
    dto: CreateProjectForecastVersionDto
  ): Promise<ProjectForecastVersionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/forecast-versions`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project forecast version');
    }

    return response.json();
  }

  async activateForecastVersion(
    forecastVersionId: string
  ): Promise<ProjectForecastVersionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/forecast-versions/${forecastVersionId}/activate`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to activate project forecast version');
    }

    return response.json();
  }

  async getAssetLinks(projectId: string): Promise<ProjectAssetLinkDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/asset-links`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project asset links');
    return response.json();
  }

  async addAssetLink(
    projectId: string,
    dto: CreateProjectAssetLinkDto
  ): Promise<ProjectAssetLinkDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/asset-links`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project asset link');
    }

    return response.json();
  }

  async getExternalAccessPolicies(
    projectId: string
  ): Promise<ProjectExternalAccessPolicyDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/external-access-policies`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch external access policies');
    return response.json();
  }

  async upsertExternalAccessPolicy(
    projectId: string,
    dto: CreateProjectExternalAccessPolicyDto
  ): Promise<ProjectExternalAccessPolicyDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/external-access-policies`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to save external access policy');
    }

    return response.json();
  }

  async deleteExternalAccessPolicy(policyId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external-access-policies/${policyId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete external access policy');
    }
  }

  async getDecisions(projectId: string): Promise<ProjectDecisionDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/decisions`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error('Failed to fetch project decisions');
    return response.json();
  }

  async addDecision(
    projectId: string,
    dto: CreateProjectDecisionDto
  ): Promise<ProjectDecisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/decisions`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to add project decision'
      );
    return response.json();
  }

  async updateDecision(
    decisionId: string,
    dto: CreateProjectDecisionDto
  ): Promise<ProjectDecisionDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/decisions/${decisionId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to update project decision'
      );
    return response.json();
  }

  async deleteDecision(decisionId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/decisions/${decisionId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to delete project decision'
      );
  }

  async getMeetings(projectId: string): Promise<ProjectMeetingMinuteDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/meetings`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error('Failed to fetch project meetings');
    return response.json();
  }

  async addMeeting(
    projectId: string,
    dto: CreateProjectMeetingMinuteDto
  ): Promise<ProjectMeetingMinuteDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/meetings`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to add project meeting'
      );
    return response.json();
  }

  async updateMeeting(
    meetingId: string,
    dto: CreateProjectMeetingMinuteDto
  ): Promise<ProjectMeetingMinuteDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/meetings/${meetingId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to update project meeting'
      );
    return response.json();
  }

  async deleteMeeting(meetingId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/meetings/${meetingId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to delete project meeting'
      );
  }

  async getActionItems(projectId: string): Promise<ProjectActionItemDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/action-items`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error('Failed to fetch project action items');
    return response.json();
  }

  async addActionItem(
    projectId: string,
    dto: CreateProjectActionItemDto
  ): Promise<ProjectActionItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/action-items`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to add project action item'
      );
    return response.json();
  }

  async updateActionItem(
    actionItemId: string,
    dto: CreateProjectActionItemDto
  ): Promise<ProjectActionItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/action-items/${actionItemId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to update project action item'
      );
    return response.json();
  }

  async deleteActionItem(actionItemId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/action-items/${actionItemId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to delete project action item'
      );
  }

  async getLessonsLearned(
    projectId: string
  ): Promise<ProjectLessonLearnedDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/lessons-learned`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error('Failed to fetch project lessons learned');
    return response.json();
  }

  async addLessonLearned(
    projectId: string,
    dto: CreateProjectLessonLearnedDto
  ): Promise<ProjectLessonLearnedDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/lessons-learned`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to add lesson learned'
      );
    return response.json();
  }

  async updateLessonLearned(
    lessonId: string,
    dto: CreateProjectLessonLearnedDto
  ): Promise<ProjectLessonLearnedDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/lessons-learned/${lessonId}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to update lesson learned'
      );
    return response.json();
  }

  async deleteLessonLearned(lessonId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/lessons-learned/${lessonId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to delete lesson learned'
      );
  }

  async getClosure(projectId: string): Promise<ProjectClosureDto | null> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/closure`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error('Failed to fetch project closure');
    return response.json();
  }

  async upsertClosure(
    projectId: string,
    dto: UpsertProjectClosureDto
  ): Promise<ProjectClosureDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/closure`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to save project closure'
      );
    return response.json();
  }

  async submitClosure(projectId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/closure/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to submit project closure'
      );
  }

  async approveClosure(closureId: string, comments?: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/closure/${closureId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to approve project closure'
      );
  }

  async rejectClosure(
    closureId: string,
    reason: string,
    comments?: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/closure/${closureId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ reason, comments }),
      }
    );
    if (!response.ok)
      throw new Error(
        (await response.text()) || 'Failed to reject project closure'
      );
  }

  async getAiInsights(projectId: string): Promise<ProjectAiInsightDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/ai-insights`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch AI insights');
    return response.json();
  }

  async attachDocument(
    projectId: string,
    dto: AttachProjectDocumentDto
  ): Promise<ProjectDocumentDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/documents`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to attach project document');
    }

    return response.json();
  }

  async uploadProjectDocument(
    projectId: string,
    file: File,
    metadata: Omit<
      AttachProjectDocumentDto,
      'filePath' | 'publicUrl' | 'fileType' | 'fileSize'
    >
  ): Promise<ProjectDocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('category', 'project-documents');

    const uploadResponse = await fetch(`${API_BASE_URL}/FileUpload/single`, {
      method: 'POST',
      headers: getAuthHeaders(false),
      body: formData,
    });

    if (!uploadResponse.ok) {
      const error = await uploadResponse.text();
      throw new Error(error || 'Failed to upload file');
    }

    const upload = (await uploadResponse.json()) as FileUploadResult;
    return this.attachDocument(projectId, {
      ...metadata,
      filePath: upload.filePath,
      publicUrl: upload.publicUrl,
      fileType: upload.contentType,
      fileSize: upload.fileSize,
    });
  }

  async deleteDocument(documentId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/documents/${documentId}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project document');
    }
  }

  async addComment(
    projectId: string,
    dto: CreateProjectCommentDto
  ): Promise<ProjectCommentDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/${projectId}/comments`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project comment');
    }

    return response.json();
  }

  async getExternalProjects(): Promise<ProjectExternalSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch external projects');
    return response.json();
  }

  async getExternalProjectById(
    projectId: string
  ): Promise<ProjectExternalDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch external project');
    return response.json();
  }

  async addExternalProjectComment(
    projectId: string,
    dto: CreateProjectCommentDto
  ): Promise<ProjectCommentDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/comments`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add project update');
    }

    return response.json();
  }

  async updateExternalProjectWorkItemProgress(
    projectId: string,
    workItemId: string,
    dto: UpdateProjectWorkItemProgressDto
  ): Promise<ProjectWorkItemDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/work-items/${workItemId}/progress`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update shared work item progress');
    }

    return response.json();
  }

  async submitExternalDeliverable(
    projectId: string,
    deliverableId: string,
    dto: SubmitProjectDeliverableDto
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/deliverables/${deliverableId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit external deliverable');
    }

    return response.json();
  }

  async approveExternalDeliverable(
    projectId: string,
    deliverableId: string,
    comments?: string
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/deliverables/${deliverableId}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve external deliverable');
    }

    return response.json();
  }

  async rejectExternalDeliverable(
    projectId: string,
    deliverableId: string,
    comments?: string
  ): Promise<ProjectDeliverableDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/deliverables/${deliverableId}/reject`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ comments }),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject external deliverable');
    }

    return response.json();
  }

  async attachExternalProjectDocument(
    projectId: string,
    dto: AttachProjectDocumentDto
  ): Promise<ProjectDocumentDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/external/my-projects/${projectId}/documents`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to attach external project document');
    }

    return response.json();
  }

  async uploadExternalProjectDocument(
    projectId: string,
    file: File,
    metadata: Omit<
      AttachProjectDocumentDto,
      'filePath' | 'publicUrl' | 'fileType' | 'fileSize'
    >
  ): Promise<ProjectDocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('category', 'project-documents');

    const uploadResponse = await fetch(`${API_BASE_URL}/FileUpload/single`, {
      method: 'POST',
      headers: getAuthHeaders(false),
      body: formData,
    });

    if (!uploadResponse.ok) {
      const error = await uploadResponse.text();
      throw new Error(error || 'Failed to upload file');
    }

    const upload = (await uploadResponse.json()) as FileUploadResult;
    return this.attachExternalProjectDocument(projectId, {
      ...metadata,
      filePath: upload.filePath,
      publicUrl: upload.publicUrl,
      fileType: upload.contentType,
      fileSize: upload.fileSize,
      isExternalVisible: true,
    });
  }

  async getMobileSummary(): Promise<ProjectMobileSummaryDto> {
    const response = await fetch(
      `${API_BASE_URL.replace('/api', '')}/api/mobile/projects/summary`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch mobile project summary');
    return response.json();
  }

  async submitMobileTimesheet(
    projectId: string,
    dto: CreateProjectTimesheetEntryDto
  ): Promise<ProjectTimesheetEntryDto> {
    const response = await fetch(
      `${API_BASE_URL.replace('/api', '')}/api/mobile/projects/${projectId}/timesheets`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit mobile timesheet');
    }

    return response.json();
  }

  async submitMobileExpense(
    projectId: string,
    dto: CreateProjectExpenseDto
  ): Promise<ProjectExpenseDto> {
    const response = await fetch(
      `${API_BASE_URL.replace('/api', '')}/api/mobile/projects/${projectId}/expenses`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit mobile expense');
    }

    return response.json();
  }

  async updateMobileWorkItemProgress(
    projectId: string,
    workItemId: string,
    dto: UpdateProjectWorkItemProgressDto
  ): Promise<ProjectWorkItemDto> {
    const response = await fetch(
      `${API_BASE_URL.replace('/api', '')}/api/mobile/projects/${projectId}/work-items/${workItemId}/progress`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update mobile work item progress');
    }

    return response.json();
  }

  async getProjectTypes(): Promise<ProjectTypeDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/types`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project types');
    return response.json();
  }

  async getMasterDataOverview(): Promise<ProjectMasterDataOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/master-data-overview`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project master data overview');
    return response.json();
  }

  async getProjectUnitTypeTemplates(
    includeInactive: boolean = false
  ): Promise<ProjectUnitTypeTemplateDto[]> {
    const query = includeInactive ? '?includeInactive=true' : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/unit-types${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project unit types');
    return response.json();
  }

  async createProjectUnitTypeTemplate(
    dto: CreateProjectUnitTypeTemplateDto
  ): Promise<ProjectUnitTypeTemplateDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/unit-types`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project unit type');
    }

    return response.json();
  }

  async updateProjectUnitTypeTemplate(
    id: string,
    dto: CreateProjectUnitTypeTemplateDto
  ): Promise<ProjectUnitTypeTemplateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/unit-types/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project unit type');
    }

    return response.json();
  }

  async deleteProjectUnitTypeTemplate(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/unit-types/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project unit type');
    }
  }

  async getCatalogEntries(
    catalogType: string
  ): Promise<ProjectCatalogEntryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/catalogs?catalogType=${encodeURIComponent(catalogType)}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project catalog entries');
    return response.json();
  }

  async createCatalogEntry(
    dto: CreateProjectCatalogEntryDto
  ): Promise<ProjectCatalogEntryDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/catalogs`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project catalog entry');
    }

    return response.json();
  }

  async updateCatalogEntry(
    id: string,
    dto: CreateProjectCatalogEntryDto
  ): Promise<ProjectCatalogEntryDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/catalogs/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project catalog entry');
    }

    return response.json();
  }

  async deleteCatalogEntry(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/catalogs/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project catalog entry');
    }
  }

  async seedCatalogDefaults(catalogType?: string): Promise<void> {
    const query = catalogType
      ? `?catalogType=${encodeURIComponent(catalogType)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/catalogs/seed-defaults${query}`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to seed project catalog defaults');
    }
  }

  async createProjectType(dto: CreateProjectTypeDto): Promise<ProjectTypeDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/types`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project type');
    }

    return response.json();
  }

  async updateProjectType(
    id: string,
    dto: CreateProjectTypeDto
  ): Promise<ProjectTypeDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/types/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project type');
    }

    return response.json();
  }

  async deleteProjectType(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/types/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project type');
    }
  }

  async getProjectPriorities(): Promise<ProjectPriorityDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/priorities`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project priorities');
    return response.json();
  }

  async createProjectPriority(
    dto: CreateProjectPriorityDto
  ): Promise<ProjectPriorityDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/priorities`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project priority');
    }

    return response.json();
  }

  async updateProjectPriority(
    id: string,
    dto: CreateProjectPriorityDto
  ): Promise<ProjectPriorityDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/priorities/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project priority');
    }

    return response.json();
  }

  async deleteProjectPriority(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/priorities/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project priority');
    }
  }

  async getProjectTemplates(
    projectTypeId?: string
  ): Promise<ProjectTemplateDto[]> {
    const query = projectTypeId
      ? `?projectTypeId=${encodeURIComponent(projectTypeId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/templates${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project templates');
    return response.json();
  }

  async createProjectTemplate(
    dto: CreateProjectTemplateDto
  ): Promise<ProjectTemplateDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/templates`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project template');
    }

    return response.json();
  }

  async updateProjectTemplate(
    id: string,
    dto: CreateProjectTemplateDto
  ): Promise<ProjectTemplateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/templates/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project template');
    }

    return response.json();
  }

  async deleteProjectTemplate(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/templates/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project template');
    }
  }

  async getProjectPhaseTemplates(
    projectTypeId?: string
  ): Promise<ProjectPhaseTemplateDto[]> {
    const query = projectTypeId
      ? `?projectTypeId=${encodeURIComponent(projectTypeId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/phase-templates${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project phase templates');
    return response.json();
  }

  async createProjectPhaseTemplate(
    dto: CreateProjectPhaseTemplateDto
  ): Promise<ProjectPhaseTemplateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/phase-templates`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project phase template');
    }

    return response.json();
  }

  async updateProjectPhaseTemplate(
    id: string,
    dto: UpdateProjectPhaseTemplateDto
  ): Promise<ProjectPhaseTemplateDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/phase-templates/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project phase template');
    }

    return response.json();
  }

  async deleteProjectPhaseTemplate(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/phase-templates/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project phase template');
    }
  }

  async getProjectStageGateRules(
    projectPhaseTemplateId?: string
  ): Promise<ProjectStageGateRuleDto[]> {
    const query = projectPhaseTemplateId
      ? `?projectPhaseTemplateId=${encodeURIComponent(projectPhaseTemplateId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/stage-gate-rules${query}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch project stage gate rules');
    return response.json();
  }

  async createProjectStageGateRule(
    dto: CreateProjectStageGateRuleDto
  ): Promise<ProjectStageGateRuleDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/stage-gate-rules`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project stage gate rule');
    }

    return response.json();
  }

  async updateProjectStageGateRule(
    id: string,
    dto: UpdateProjectStageGateRuleDto
  ): Promise<ProjectStageGateRuleDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/stage-gate-rules/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project stage gate rule');
    }

    return response.json();
  }

  async deleteProjectStageGateRule(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/stage-gate-rules/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project stage gate rule');
    }
  }

  async getPortfolios(): Promise<ProjectPortfolioDto[]> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/portfolios`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project portfolios');
    return response.json();
  }

  async createPortfolio(
    dto: CreateProjectPortfolioDto
  ): Promise<ProjectPortfolioDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/portfolios`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project portfolio');
    }

    return response.json();
  }

  async updatePortfolio(
    id: string,
    dto: CreateProjectPortfolioDto
  ): Promise<ProjectPortfolioDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/portfolios/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project portfolio');
    }

    return response.json();
  }

  async deletePortfolio(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/portfolios/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project portfolio');
    }
  }

  async getPrograms(portfolioId?: string): Promise<ProjectProgramDto[]> {
    const queryParams = new URLSearchParams();
    if (portfolioId) queryParams.append('portfolioId', portfolioId);

    const response = await fetch(
      `${API_BASE_URL}/projects/admin/programs?${queryParams.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch project programs');
    return response.json();
  }

  async createProgram(
    dto: CreateProjectProgramDto
  ): Promise<ProjectProgramDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/programs`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create project program');
    }

    return response.json();
  }

  async updateProgram(
    id: string,
    dto: CreateProjectProgramDto
  ): Promise<ProjectProgramDto> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/programs/${id}`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project program');
    }

    return response.json();
  }

  async deleteProgram(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/projects/admin/programs/${id}`,
      {
        method: 'DELETE',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete project program');
    }
  }

  async getSettings(): Promise<ProjectManagementSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/settings`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch project settings');
    return response.json();
  }

  async updateSettings(
    dto: UpdateProjectManagementSettingsDto
  ): Promise<ProjectManagementSettingsDto> {
    const response = await fetch(`${API_BASE_URL}/projects/admin/settings`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update project settings');
    }

    return response.json();
  }
}

export const projectService = new ProjectService();
