import { apiService as rawApiService } from './api.service';
import { compatibleApiService as apiService } from './compatibleApiService';

export interface CentralDocumentWorkspaceItem {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface CentralDocumentStage {
  name: string;
  owner: string;
  summary: string;
  checklist: string[];
}

export interface CentralDocumentField {
  key: string;
  label: string;
  type: string;
  options?: string[] | null;
}

export interface CentralDocumentHandoff {
  fromModule: string;
  toModule: string;
  trigger: string;
}

export interface CentralDocumentWorkspace {
  workspace: CentralDocumentWorkspaceItem;
  stages: CentralDocumentStage[];
  fields: CentralDocumentField[];
  outputs: string[];
  handoffs: CentralDocumentHandoff[];
}

export interface CentralDocumentMetric {
  label: string;
  value: string;
  detail: string;
  trend: string;
}

export interface CentralDocumentModuleQueue {
  module: string;
  sourceLabel: string;
  pendingMetadata: number;
  pendingVersion: number;
  openAnnotations: number;
  retentionReviews: number;
}

export interface CentralDocumentDashboard {
  metrics: CentralDocumentMetric[];
  readiness: CentralDocumentMetric[];
  moduleQueues: CentralDocumentModuleQueue[];
}

export interface CentralDocumentMetadataTemplate {
  id?: string;
  module: string;
  documentType: string;
  templateCode: string;
  requiredFields: string[];
  relationships: string[];
  retentionRule: string;
  accessProfile: string;
  sourceLabel: string;
  isActive?: boolean;
  publishedAt?: string | null;
}

export interface CentralDocumentMetadataCompletenessItem {
  field: string;
  captured: boolean;
  value?: string | null;
}

export interface CentralDocumentMetadataCompleteness {
  status: 'Complete' | 'Partial' | 'Missing template' | string;
  percentage: number;
  capturedCount: number;
  requiredCount: number;
  templateCode?: string | null;
  module?: string | null;
  documentType?: string | null;
  items: CentralDocumentMetadataCompletenessItem[];
  missingFields: string[];
}

export interface CentralDocumentMetadataValue {
  id: string;
  documentRecordId: string;
  templateCode?: string | null;
  fieldKey: string;
  fieldLabel: string;
  fieldValue?: string | null;
  valueType: string;
  source: string;
  capturedAt: string;
  capturedById?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CentralDocumentAccessRuleSummary {
  accessProfile: string;
  module?: string | null;
  roleName: string;
  permissionKey?: string | null;
  canView: boolean;
  canUpload: boolean;
  canAnnotate: boolean;
  canApprove: boolean;
  canArchive: boolean;
}

export interface CentralDocumentRetentionPolicySummary {
  policyCode: string;
  name: string;
  module?: string | null;
  documentType?: string | null;
  retentionDays: number;
  requiresLegalHoldReview: boolean;
  allowArchive: boolean;
  allowDestruction: boolean;
  notes?: string | null;
}

export interface CentralDocumentAccessRetentionCompliance {
  accessStatus: string;
  accessRuleCount: number;
  accessRules: CentralDocumentAccessRuleSummary[];
  retentionPolicyStatus: string;
  retentionNeedsAction: boolean;
  retentionPolicy?: CentralDocumentRetentionPolicySummary | null;
  currentRetentionStatus: string;
  reviewDate?: string | null;
  expiryDate?: string | null;
}

export interface CentralDocumentRegisterItem {
  id?: string;
  documentReference: string;
  title: string;
  module: string;
  sourceRecord: string;
  templateCode: string;
  version: string;
  repositoryStatus: string;
  annotationStatus: string;
  commentStatus: string;
  retentionStatus: string;
  sourceLabel: string;
  metadataCompleteness?: CentralDocumentMetadataCompleteness | null;
}

export interface CentralDocumentRecord {
  id: string;
  documentReference: string;
  title: string;
  sourceModule: string;
  sourceLabel: string;
  sourceEntityType?: string | null;
  sourceRecordReference?: string | null;
  sourceRecordId?: string | null;
  metadataTemplateCode?: string | null;
  repositoryStatus: string;
  repositoryPath?: string | null;
  externalDocumentUrl?: string | null;
  currentVersion?: string | null;
  versionStatus: string;
  annotationStatus: string;
  commentStatus: string;
  accessProfile: string;
  retentionStatus: string;
  lifecycleStatus: string;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  reviewDate?: string | null;
  publishedAt?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  metadataValues: CentralDocumentMetadataValue[];
  metadataCompleteness: CentralDocumentMetadataCompleteness;
  accessRetentionCompliance?: CentralDocumentAccessRetentionCompliance | null;
}

export interface CentralDocumentVersion {
  id: string;
  documentRecordId: string;
  versionNumber: string;
  status: string;
  repositoryPath?: string | null;
  renditionPath?: string | null;
  fileName?: string | null;
  contentType?: string | null;
  fileSize?: number | null;
  fileUploadRecordId?: string | null;
  publishedAt?: string | null;
  changeSummary?: string | null;
  createdAt: string;
}

export type CentralDocumentVersionDownloadFormat = 'pdf' | 'word';

export interface CentralDocumentAnnotationReview {
  id: string;
  documentRecordId: string;
  documentVersionId?: string | null;
  reviewTitle: string;
  status: string;
  syncfusionAnnotationStatus: string;
  assignedReviewerId?: string | null;
  dueDate?: string | null;
  closedAt?: string | null;
  reviewNotes?: string | null;
  annotationStateJson?: string | null;
  createdAt: string;
}

export interface CentralDocumentRecordDetail {
  record: CentralDocumentRecord;
  versions: CentralDocumentVersion[];
  annotationReviews: CentralDocumentAnnotationReview[];
}

export interface CentralDocumentIntegrationQueueItem {
  id: string;
  queueReference: string;
  sourceModule: string;
  sourceLabel: string;
  sourceRecord: string;
  documentType: string;
  templateCode: string;
  issue: string;
  receivedAt: string;
  status: 'Pending Review' | 'Accepted' | string;
  document: CentralDocumentRecord;
}

export interface CentralDocumentVersionQueueItem {
  id: string;
  documentRecordId: string;
  documentReference: string;
  title: string;
  sourceModule: string;
  sourceLabel: string;
  currentVersion: string;
  requestedVersion: string;
  reason: string;
  status:
    | 'Pending Publication'
    | 'Published'
    | 'Returned'
    | 'Superseded'
    | string;
  document: CentralDocumentRecord;
  version: CentralDocumentVersion;
}

export interface UpsertCentralDocumentVersion {
  versionNumber?: string;
  status?: string;
  repositoryPath?: string;
  renditionPath?: string;
  fileName?: string;
  contentType?: string;
  fileSize?: number;
  fileUploadRecordId?: string;
  changeSummary?: string;
}

export interface UpdateCentralDocumentVersionStatus {
  status: string;
  changeSummary?: string;
}

export interface CentralDocumentVersionStatusResult {
  record: CentralDocumentRecord;
  version: CentralDocumentVersion;
}

export interface UpsertCentralDocumentAnnotationReview {
  documentVersionId?: string;
  reviewTitle?: string;
  status?: string;
  syncfusionAnnotationStatus?: string;
  assignedReviewerId?: string;
  dueDate?: string;
  reviewNotes?: string;
  annotationStateJson?: string;
}

export interface UpdateCentralDocumentAnnotationReview {
  status?: string;
  syncfusionAnnotationStatus?: string;
  reviewNotes?: string | null;
  annotationStateJson?: string | null;
}

export interface CentralDocumentAnnotationReviewResult {
  record: CentralDocumentRecord;
  review: CentralDocumentAnnotationReview;
}

export interface UpdateCentralDocumentLifecycleControls {
  metadataTemplateCode?: string | null;
  repositoryStatus?: string;
  repositoryPath?: string | null;
  externalDocumentUrl?: string | null;
  currentVersion?: string | null;
  versionStatus?: string;
  annotationStatus?: string;
  commentStatus?: string;
  accessProfile?: string;
  retentionStatus?: string;
  lifecycleStatus?: string;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  reviewDate?: string | null;
  notes?: string | null;
}

export interface UpsertCentralDocumentMetadataValue {
  fieldKey?: string;
  fieldLabel: string;
  fieldValue?: string | null;
  valueType?: string;
}

export interface UpdateCentralDocumentMetadataValues {
  values: UpsertCentralDocumentMetadataValue[];
}

export interface UpsertCentralDocumentMetadataTemplate {
  module: string;
  documentType: string;
  templateCode: string;
  sourceLabel?: string;
  requiredFields: string[];
  relationships: string[];
  retentionRule?: string;
  accessProfile?: string;
  isActive?: boolean;
}

export interface CentralDocumentAccessRule {
  id: string;
  accessProfile: string;
  module?: string | null;
  roleName?: string | null;
  permissionKey?: string | null;
  canView: boolean;
  canUpload: boolean;
  canAnnotate: boolean;
  canApprove: boolean;
  canArchive: boolean;
  isActive: boolean;
}

export interface UpsertCentralDocumentAccessRule {
  accessProfile?: string;
  module?: string;
  roleName?: string;
  permissionKey?: string;
  canView?: boolean;
  canUpload?: boolean;
  canAnnotate?: boolean;
  canApprove?: boolean;
  canArchive?: boolean;
  isActive?: boolean;
}

export interface CentralDocumentRetentionPolicy {
  id: string;
  policyCode: string;
  name: string;
  module?: string | null;
  documentType?: string | null;
  retentionDays: number;
  requiresLegalHoldReview: boolean;
  allowArchive: boolean;
  allowDestruction: boolean;
  isActive: boolean;
  notes?: string | null;
}

export interface UpsertCentralDocumentRetentionPolicy {
  policyCode: string;
  name: string;
  module?: string;
  documentType?: string;
  retentionDays?: number;
  requiresLegalHoldReview?: boolean;
  allowArchive?: boolean;
  allowDestruction?: boolean;
  isActive?: boolean;
  notes?: string;
}

export interface CentralDocumentGenerationTemplate {
  id?: string;
  templateCode: string;
  title: string;
  titleTemplate: string;
  module: string;
  sourceLabel: string;
  documentType: string;
  metadataTemplateCode: string;
  accessProfile: string;
  mergeFields: string[];
  body: string;
  isActive: boolean;
  requiresApproval: boolean;
  approvalRole?: string | null;
  signatureRole?: string | null;
  defaultDispatchChannel?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface UpsertCentralDocumentGenerationTemplate {
  templateCode?: string;
  title?: string;
  titleTemplate?: string;
  module?: string;
  sourceLabel?: string;
  documentType?: string;
  metadataTemplateCode?: string;
  accessProfile?: string;
  mergeFields?: string[];
  body?: string;
  isActive?: boolean;
  requiresApproval?: boolean;
  approvalRole?: string | null;
  signatureRole?: string | null;
  defaultDispatchChannel?: string | null;
}

export interface GenerateCentralDocumentPayload {
  templateCode: string;
  sourceModule?: string;
  sourceLabel?: string;
  sourceEntityType?: string;
  sourceRecordReference?: string;
  sourceRecordId?: string;
  caseTitle?: string;
  caseReference?: string;
  applicantName?: string;
  preparedBy?: string;
  purpose?: string;
  mergeValues?: Record<string, string | null | undefined>;
}

export interface GeneratedCentralDocumentResult {
  template: CentralDocumentGenerationTemplate;
  record: CentralDocumentRecord;
  version: CentralDocumentVersion;
  content: string;
  pdfUrl: string;
  dmsReference: string;
  sourceLabel: string;
}

export interface GeneratedDocumentWorkflowAction {
  action: 'SubmitForApproval' | 'Approve' | 'Sign' | 'Dispatch' | 'Return' | string;
  notes?: string;
  signatureRole?: string;
  dispatchChannel?: string;
  dispatchedTo?: string;
  dispatchReference?: string;
}

export interface GeneratedDocumentWorkflowResult {
  record: CentralDocumentRecord;
  version?: CentralDocumentVersion | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class DocumentManagementService {
  async getWorkspaces(): Promise<CentralDocumentWorkspaceItem[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentWorkspaceItem[]>
    >('/document-management/workspaces');
    return response.data || [];
  }

  async getWorkspace(
    entityType: string
  ): Promise<CentralDocumentWorkspace | null> {
    const response = await apiService.silentGet<
      ApiResponse<CentralDocumentWorkspace>
    >(`/document-management/workspaces/${encodeURIComponent(entityType)}`);
    return response.data || null;
  }

  async getDashboard(): Promise<CentralDocumentDashboard | null> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentDashboard>
    >('/document-management/dashboard');
    return response.data || null;
  }

  async getMetadataTemplates(): Promise<CentralDocumentMetadataTemplate[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentMetadataTemplate[]>
    >('/document-management/metadata-templates');
    return response.data || [];
  }

  async getGenerationTemplates(
    module?: string
  ): Promise<CentralDocumentGenerationTemplate[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentGenerationTemplate[]>
    >(
      '/document-management/document-templates',
      module ? { module } : undefined
    );
    return response.data || [];
  }

  async generateDocumentFromTemplate(
    payload: GenerateCentralDocumentPayload
  ): Promise<GeneratedCentralDocumentResult> {
    const response = await apiService.post<
      ApiResponse<GeneratedCentralDocumentResult>
    >('/document-management/document-templates/generate', payload);
    return response.data;
  }

  async saveGenerationTemplate(
    templateCode: string,
    payload: UpsertCentralDocumentGenerationTemplate
  ): Promise<CentralDocumentGenerationTemplate> {
    const response = await apiService.put<
      ApiResponse<CentralDocumentGenerationTemplate>
    >(
      `/document-management/document-templates/${encodeURIComponent(
        templateCode
      )}`,
      payload
    );
    return response.data;
  }

  async updateGeneratedDocumentWorkflow(
    recordId: string,
    payload: GeneratedDocumentWorkflowAction
  ): Promise<GeneratedDocumentWorkflowResult> {
    const response = await apiService.post<
      ApiResponse<GeneratedDocumentWorkflowResult>
    >(
      `/document-management/generated-documents/${encodeURIComponent(
        recordId
      )}/workflow`,
      payload
    );
    return response.data;
  }

  async createMetadataTemplate(
    payload: UpsertCentralDocumentMetadataTemplate
  ): Promise<CentralDocumentMetadataTemplate> {
    const response = await apiService.post<
      ApiResponse<CentralDocumentMetadataTemplate>
    >('/document-management/metadata-templates', payload);
    return response.data;
  }

  async getRegister(): Promise<CentralDocumentRegisterItem[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentRegisterItem[]>
    >('/document-management/register');
    return response.data || [];
  }

  async getRecords(module?: string): Promise<CentralDocumentRecord[]> {
    const response = await apiService.get<ApiResponse<CentralDocumentRecord[]>>(
      '/document-management/records',
      module ? { module } : undefined
    );
    return response.data || [];
  }

  async getRecord(id: string): Promise<CentralDocumentRecordDetail | null> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentRecordDetail>
    >(`/document-management/records/${encodeURIComponent(id)}`);
    return response.data || null;
  }

  async getIntegrationQueue(): Promise<CentralDocumentIntegrationQueueItem[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentIntegrationQueueItem[]>
    >('/document-management/queues/integration');
    return response.data || [];
  }

  async getVersionQueue(): Promise<CentralDocumentVersionQueueItem[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentVersionQueueItem[]>
    >('/document-management/queues/versions');
    return response.data || [];
  }

  async addVersion(
    recordId: string,
    payload: UpsertCentralDocumentVersion
  ): Promise<CentralDocumentVersion> {
    const response = await apiService.post<ApiResponse<CentralDocumentVersion>>(
      `/document-management/records/${encodeURIComponent(recordId)}/versions`,
      payload
    );
    return response.data;
  }

  async updateVersionStatus(
    recordId: string,
    versionId: string,
    payload: UpdateCentralDocumentVersionStatus
  ): Promise<CentralDocumentVersionStatusResult> {
    const response = await apiService.put<
      ApiResponse<CentralDocumentVersionStatusResult>
    >(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/versions/${encodeURIComponent(versionId)}/status`,
      payload
    );
    return response.data;
  }

  async uploadVersionFile(
    recordId: string,
    payload: {
      file: File;
      versionNumber?: string;
      status?: string;
      changeSummary?: string;
      renditionPath?: string;
    }
  ): Promise<CentralDocumentVersion> {
    const formData = new FormData();
    formData.append('file', payload.file);
    if (payload.versionNumber)
      formData.append('versionNumber', payload.versionNumber);
    if (payload.status) formData.append('status', payload.status);
    if (payload.changeSummary)
      formData.append('changeSummary', payload.changeSummary);
    if (payload.renditionPath)
      formData.append('renditionPath', payload.renditionPath);

    const response = await apiService.post<ApiResponse<CentralDocumentVersion>>(
      `/document-management/records/${encodeURIComponent(recordId)}/versions/upload`,
      formData
    );
    return response.data;
  }

  async updateVersionRendition(
    recordId: string,
    versionId: string,
    payload: {
      renditionPath: string;
      annotationStatus?: string;
      changeSummary?: string;
    }
  ): Promise<CentralDocumentVersion> {
    const response = await apiService.put<ApiResponse<CentralDocumentVersion>>(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/versions/${encodeURIComponent(versionId)}/rendition`,
      payload
    );
    return response.data;
  }

  async generateVersionRendition(
    recordId: string,
    versionId: string
  ): Promise<CentralDocumentVersion> {
    const response = await apiService.post<ApiResponse<CentralDocumentVersion>>(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/versions/${encodeURIComponent(versionId)}/rendition/generate`
    );
    return response.data;
  }

  async downloadVersionFile(
    recordId: string,
    versionId: string,
    format: CentralDocumentVersionDownloadFormat
  ): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/versions/${encodeURIComponent(versionId)}/download`,
      { format }
    );
  }

  async downloadRecordContent(recordId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/document-management/records/${encodeURIComponent(recordId)}/content`
    );
  }

  async addAnnotationReview(
    recordId: string,
    payload: UpsertCentralDocumentAnnotationReview
  ): Promise<CentralDocumentAnnotationReview> {
    const response = await apiService.post<
      ApiResponse<CentralDocumentAnnotationReview>
    >(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/annotation-reviews`,
      payload
    );
    return response.data;
  }

  async updateAnnotationReview(
    recordId: string,
    reviewId: string,
    payload: UpdateCentralDocumentAnnotationReview
  ): Promise<CentralDocumentAnnotationReviewResult> {
    const response = await apiService.put<
      ApiResponse<CentralDocumentAnnotationReviewResult>
    >(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/annotation-reviews/${encodeURIComponent(reviewId)}`,
      payload
    );
    return response.data;
  }

  async updateLifecycleControls(
    recordId: string,
    payload: UpdateCentralDocumentLifecycleControls
  ): Promise<CentralDocumentRecord> {
    const response = await apiService.put<ApiResponse<CentralDocumentRecord>>(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/lifecycle-controls`,
      payload
    );
    return response.data;
  }

  async updateMetadataValues(
    recordId: string,
    payload: UpdateCentralDocumentMetadataValues
  ): Promise<CentralDocumentRecord> {
    const response = await apiService.put<ApiResponse<CentralDocumentRecord>>(
      `/document-management/records/${encodeURIComponent(
        recordId
      )}/metadata-values`,
      payload
    );
    return response.data;
  }

  async getAccessRules(): Promise<CentralDocumentAccessRule[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentAccessRule[]>
    >('/document-management/access-rules');
    return response.data || [];
  }

  async createAccessRule(
    payload: UpsertCentralDocumentAccessRule
  ): Promise<CentralDocumentAccessRule> {
    const response = await apiService.post<
      ApiResponse<CentralDocumentAccessRule>
    >('/document-management/access-rules', payload);
    return response.data;
  }

  async getRetentionPolicies(): Promise<CentralDocumentRetentionPolicy[]> {
    const response = await apiService.get<
      ApiResponse<CentralDocumentRetentionPolicy[]>
    >('/document-management/retention-policies');
    return response.data || [];
  }

  async createRetentionPolicy(
    payload: UpsertCentralDocumentRetentionPolicy
  ): Promise<CentralDocumentRetentionPolicy> {
    const response = await apiService.post<
      ApiResponse<CentralDocumentRetentionPolicy>
    >('/document-management/retention-policies', payload);
    return response.data;
  }
}

export const documentManagementService = new DocumentManagementService();
