export type CivilEngineeringDesignStage =
  | 'DraftDirective'
  | 'SceInformationGathering'
  | 'CivilEngineerDesign'
  | 'SceDesignReview'
  | 'Drafting'
  | 'SceDrawingReview'
  | 'HodFinalReview'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled';

export type CivilEngineeringDesignAction =
  | 'DirectToSce'
  | 'AssignCivilEngineer'
  | 'SubmitDesign'
  | 'ReturnDesign'
  | 'AssignDraftsman'
  | 'SubmitDrawings'
  | 'ReturnDrawings'
  | 'SubmitPackage'
  | 'Approve'
  | 'Reject'
  | 'Cancel';

export type CivilEngineeringDesignEvidenceType =
  'Directive' | 'Reconnaissance' | 'Design' | 'Drawing' | 'SubmissionPackage';

export type CivilEngineeringWorksInitiationSource =
  | 'ApprovedCapitalProject'
  | 'MaintenanceEscalation'
  | 'PropertyDevelopmentNeed'
  | 'PlanningCondition'
  | 'ManagementDirective'
  | 'DefectMonitoring'
  | 'InfrastructureImprovementRequest';

export type CivilEngineeringWorkClassification =
  | 'NewProjectDesign'
  | 'ConstructionSupervision'
  | 'ScheduledMaintenance'
  | 'BreakdownMaintenance'
  | 'PermittingReview'
  | 'AssetComplaintResolution';

export interface CivilEngineeringDesignEvidence {
  id: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  documentTitle: string;
  versionNumber: string;
  evidenceType: CivilEngineeringDesignEvidenceType;
  stageSnapshot: string;
  linkedAt: string;
}

export interface CivilEngineeringDesignCase {
  id: string;
  projectId: string;
  initiationSource?: CivilEngineeringWorksInitiationSource | null;
  initiationSourceId?: string | null;
  initiationSourceDocumentVersionId?: string | null;
  initiationSourceReference?: string | null;
  estateManagedAssetId?: string | null;
  estateManagedAssetLabel?: string | null;
  engineeringCategoryId?: string | null;
  engineeringCategoryLabel?: string | null;
  workClassification?: CivilEngineeringWorkClassification | null;
  scopeSummary?: string | null;
  constraintSummary?: string | null;
  riskSummary?: string | null;
  recommendation?: string | null;
  referenceNumber: string;
  title: string;
  directive: string;
  stage: CivilEngineeringDesignStage;
  status: string;
  approvalStatus: string;
  requirePlanningGisValidation: boolean;
  hodUserId: string;
  supervisingCivilEngineerUserId: string;
  civilEngineerUserId?: string | null;
  draftsmanUserId?: string | null;
  currentAssigneeUserId?: string | null;
  currentDueAt?: string | null;
  workflowInstanceId?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
  evidence: CivilEngineeringDesignEvidence[];
}

export interface CivilEngineeringDesignMemberLookup {
  userId: string;
  displayName: string;
  role: string;
}

export interface CivilEngineeringDesignLookups {
  supervisingCivilEngineers: CivilEngineeringDesignMemberLookup[];
  civilEngineers: CivilEngineeringDesignMemberLookup[];
  draftsmen: CivilEngineeringDesignMemberLookup[];
  informationSourceSections: Array<{
    sectionId: string;
    departmentId: string;
    label: string;
  }>;
  workClassifications: CivilEngineeringWorkClassification[];
  engineeringCategories: CivilEngineeringDesignSourceLookup[];
  estateManagedAssets: CivilEngineeringDesignSourceLookup[];
  approvedCapitalProjects: CivilEngineeringDesignSourceLookup[];
  maintenanceEscalations: CivilEngineeringDesignSourceLookup[];
  propertyDevelopmentNeeds: CivilEngineeringDesignSourceLookup[];
  planningConditions: CivilEngineeringDesignSourceLookup[];
  managementDirectives: CivilEngineeringDesignSourceDocumentLookup[];
  defectMonitoringCases: CivilEngineeringDesignSourceLookup[];
  infrastructureImprovementRequests: CivilEngineeringDesignSourceLookup[];
}

export interface CivilEngineeringDesignSourceLookup {
  id: string;
  label: string;
  projectId?: string | null;
}

export interface CivilEngineeringDesignSourceDocumentLookup {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  label: string;
}

export interface CivilEngineeringDesignRevision {
  id: string;
  action: string;
  fromStage: string;
  toStage: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  timestamp: string;
}

export interface CivilEngineeringCommercialReadinessGate {
  code: string;
  title: string;
  isSatisfied: boolean;
  detail: string;
}

export interface CivilEngineeringCommercialReadinessLink {
  owner: string;
  recordType: string;
  recordId?: string | null;
  reference: string;
  status: string;
  detail: string;
}

export interface CivilEngineeringCommercialReadiness {
  designCaseId: string;
  projectId: string;
  revalidatedAt: string;
  readyForExecution: boolean;
  gates: CivilEngineeringCommercialReadinessGate[];
  links: CivilEngineeringCommercialReadinessLink[];
}

export interface CreateCivilEngineeringDesignCaseRequest {
  clientRequestId: string;
  projectId: string;
  initiationSource: CivilEngineeringWorksInitiationSource;
  initiationSourceId: string;
  initiationSourceDocumentVersionId?: string;
  estateManagedAssetId?: string;
  engineeringCategoryId: string;
  workClassification: CivilEngineeringWorkClassification;
  scopeSummary: string;
  constraintSummary: string;
  riskSummary: string;
  recommendation: string;
  title: string;
  directive: string;
  supervisingCivilEngineerUserId: string;
  dueAt?: string;
}

export interface CivilEngineeringDesignTransitionRequest {
  clientRequestId: string;
  action: CivilEngineeringDesignAction;
  assigneeUserId?: string;
  dueAt?: string;
  reason?: string;
  rowVersion: string;
  evidence: Array<{
    centralDocumentRecordId: string;
    centralDocumentVersionId: string;
    evidenceType: CivilEngineeringDesignEvidenceType;
  }>;
}

export type CivilEngineeringLayoutConformity =
  | 'Conforms'
  | 'ConformsWithConditions'
  | 'NonConforming';

export type CivilEngineeringPlanningGisValidationStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Rejected';

export interface CivilEngineeringPlanningGisLookupOption {
  id: string;
  label: string;
}

export interface CivilEngineeringPlanningGisDocumentLookup {
  developmentApprovalFileId: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  label: string;
}

export interface CivilEngineeringPlanningGisLookups {
  estateManagedAssetId: string;
  estateManagedAssetLabel: string;
  developmentApprovalFiles: CivilEngineeringPlanningGisLookupOption[];
  planningConditions: CivilEngineeringPlanningGisLookupOption[];
  developmentConstraints: CivilEngineeringPlanningGisLookupOption[];
  landUseImpacts: CivilEngineeringPlanningGisLookupOption[];
  evidenceDocuments: CivilEngineeringPlanningGisDocumentLookup[];
  layoutConformities: CivilEngineeringLayoutConformity[];
}

export interface CivilEngineeringPlanningGisValidation {
  id: string;
  designCaseId: string;
  estateManagedAssetId: string;
  estateManagedAssetLabel: string;
  developmentApprovalFileId: string;
  developmentApprovalFileLabel: string;
  planningConditionId: string;
  planningConditionLabel: string;
  developmentConstraintId: string;
  developmentConstraintLabel: string;
  landUseImpactId: string;
  landUseImpactLabel: string;
  layoutConformity: CivilEngineeringLayoutConformity;
  spatialReference: string;
  boundaryCoordinates?: string | null;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  evidenceReference: string;
  status: CivilEngineeringPlanningGisValidationStatus;
  preparedByUserId: string;
  submittedAt?: string | null;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  rowVersion: string;
}

export interface CreateCivilEngineeringPlanningGisValidationRequest {
  clientRequestId: string;
  estateManagedAssetId: string;
  developmentApprovalFileId: string;
  planningConditionId: string;
  developmentConstraintId: string;
  landUseImpactId: string;
  layoutConformity: CivilEngineeringLayoutConformity;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
}

export type CivilEngineeringReconnaissanceItemKind =
  'Constraint' | 'InformationSource' | 'Photo';
export type CivilEngineeringConstraintCategory =
  | 'Access'
  | 'Topography'
  | 'Drainage'
  | 'Soil'
  | 'Utilities'
  | 'Environment'
  | 'Boundary'
  | 'ExistingStructure'
  | 'Safety'
  | 'Regulatory';
export type CivilEngineeringConstraintSeverity =
  'Low' | 'Medium' | 'High' | 'Critical';
export type CivilEngineeringConstraintResolutionStatus =
  'Open' | 'Mitigated' | 'Accepted';

export interface CivilEngineeringReconnaissanceItem {
  id: string;
  kind: CivilEngineeringReconnaissanceItemKind;
  description: string;
  informationSourceSectionId?: string | null;
  informationSourceSectionLabel?: string | null;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  documentReference?: string | null;
  documentTitle?: string | null;
  versionNumber?: string | null;
  constraintCategory?: CivilEngineeringConstraintCategory | null;
  severity?: CivilEngineeringConstraintSeverity | null;
  resolutionStatus?: CivilEngineeringConstraintResolutionStatus | null;
  blocksDesign: boolean;
  displayOrder: number;
}

export interface CivilEngineeringReconnaissanceReport {
  id: string;
  designCaseId: string;
  reportNumber: string;
  visitDate: string;
  siteLocation: string;
  summary: string;
  siteConditions: string;
  status: 'Draft' | 'Completed';
  siteReconnaissanceTemplateId: string;
  crossSectionTemplateId: string;
  preparedByUserId: string;
  completedByUserId?: string | null;
  completedAt?: string | null;
  rowVersion: string;
  items: CivilEngineeringReconnaissanceItem[];
}

export interface CivilEngineeringReconnaissanceItemRequest {
  kind: CivilEngineeringReconnaissanceItemKind;
  description: string;
  informationSourceSectionId?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  constraintCategory?: CivilEngineeringConstraintCategory;
  severity?: CivilEngineeringConstraintSeverity;
  resolutionStatus?: CivilEngineeringConstraintResolutionStatus;
  blocksDesign: boolean;
  displayOrder: number;
}

export interface CreateCivilEngineeringReconnaissanceRequest {
  clientRequestId: string;
  visitDate: string;
  siteLocation: string;
  summary: string;
  siteConditions: string;
  items: CivilEngineeringReconnaissanceItemRequest[];
}

export interface UpdateCivilEngineeringReconnaissanceRequest extends CreateCivilEngineeringReconnaissanceRequest {
  rowVersion: string;
  reason: string;
}

export type CivilEngineeringDesignInputPriority =
  'Low' | 'Medium' | 'High' | 'Critical';
export type CivilEngineeringDesignInputStatus =
  'Submitted' | 'Answered' | 'Closed';
export type CivilEngineeringDesignInputReviewAction = 'Accept' | 'Return';

export interface CivilEngineeringDesignInputResponse {
  id: string;
  responseSequence: number;
  responseText: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  documentTitle: string;
  versionNumber: string;
  respondedByUserId: string;
  respondedByName: string;
  respondedAt: string;
}

export interface CivilEngineeringDesignInputRequest {
  id: string;
  designCaseId: string;
  projectId: string;
  referenceNumber?: string | null;
  subject: string;
  question: string;
  priority: CivilEngineeringDesignInputPriority;
  status: CivilEngineeringDesignInputStatus;
  raisedDate: string;
  responseDueDate?: string | null;
  requestedSectionId: string;
  requestedSectionLabel: string;
  requestedByUserId: string;
  blocksDesignReadiness: boolean;
  isDesignReady: boolean;
  rowVersion: string;
  responses: CivilEngineeringDesignInputResponse[];
}

export interface CreateCivilEngineeringDesignInputRequest {
  clientRequestId: string;
  requestedSectionId: string;
  subject: string;
  question: string;
  priority: CivilEngineeringDesignInputPriority;
  responseDueDate: string;
  blocksDesignReadiness: boolean;
}

export interface SubmitCivilEngineeringDesignInputResponseRequest {
  clientRequestId: string;
  rowVersion: string;
  responseText: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
}

export interface ReviewCivilEngineeringDesignInputRequest {
  clientRequestId: string;
  rowVersion: string;
  action: CivilEngineeringDesignInputReviewAction;
  reason: string;
}

export type CivilEngineeringDesignDiscipline =
  | 'Civil'
  | 'Structural'
  | 'Architecture'
  | 'Geodetic'
  | 'TownPlanning'
  | 'Mechanical'
  | 'Electrical';
export type CivilEngineeringFileCategory =
  | 'AutoCad'
  | 'ProtaStructure'
  | 'Revit'
  | 'StaadPro'
  | 'Pdf'
  | 'Office'
  | 'Image'
  | 'Other';
export type CivilEngineeringDocumentStatus =
  'Draft' | 'ForReview' | 'Approved' | 'Returned' | 'Superseded' | 'Archived';
export type CivilEngineeringDocumentNamingPolicy =
  'ProjectDisciplineSequenceRevision' | 'ProjectWorkPackageSequenceRevision';

export interface CivilEngineeringDocumentMemberLookup {
  userId: string;
  displayName: string;
  roleName: string;
}

export interface CivilEngineeringDocumentVersionLookup {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
  fileName: string;
  fileExtension: string;
  fileCategory: CivilEngineeringFileCategory;
  fileSize: number;
}

export interface CivilEngineeringDocumentLookups {
  namingPolicy: CivilEngineeringDocumentNamingPolicy;
  allowedFileExtensions: string[];
  maximumFileSizeMb: number;
  metadataTemplateCode: string;
  allowAuthorizedPreview: boolean;
  allowAuthorizedDownload: boolean;
  owners: CivilEngineeringDocumentMemberLookup[];
  reviewers: CivilEngineeringDocumentMemberLookup[];
  workPackages: Array<{ id: string; code: string; name: string }>;
  documents: CivilEngineeringDocumentVersionLookup[];
}

export interface CivilEngineeringDocument {
  id: string;
  designCaseId: string;
  projectPackageId?: string | null;
  projectPackageLabel?: string | null;
  documentKey: string;
  supersedesDocumentId?: string | null;
  discipline: CivilEngineeringDesignDiscipline;
  fileCategory: CivilEngineeringFileCategory;
  fileExtension: string;
  sequenceNumber: number;
  revisionNumber: number;
  expectedDocumentReference: string;
  documentReference: string;
  documentTitle: string;
  dmsVersion: string;
  ownerUserId: string;
  ownerName: string;
  reviewerUserId: string;
  reviewerName: string;
  status: CivilEngineeringDocumentStatus;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  reviewReason?: string | null;
  rowVersion: string;
}

export interface CreateCivilEngineeringDocumentRequest {
  clientRequestId: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  projectPackageId?: string;
  supersedesDocumentId?: string;
  discipline: CivilEngineeringDesignDiscipline;
  sequenceNumber: number;
  revisionNumber: number;
  ownerUserId: string;
  reviewerUserId: string;
}
