import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export interface ProcedureCaseSummary {
  id: string;
  module: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: string;
  currentStageIndex: number;
  currentStageName: string;
  currentAssignedRole?: string | null;
  usesConfiguredWorkflow: boolean;
  workflowInstanceId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  fieldValues?: Record<string, string | null> | null;
}

export interface ProcedureCaseField {
  id: string;
  key: string;
  label: string;
  fieldType: string;
  value?: string | null;
  options?: string[] | null;
}

export interface ProcedureCaseChecklistItem {
  id: string;
  stageIndex: number;
  stageName: string;
  text: string;
  isCompleted: boolean;
  completedById?: string | null;
  completedAt?: string | null;
}

export interface ProcedureCaseDocument {
  id: string;
  name: string;
  requiredFrom?: string | null;
  providedBy: string;
  isMandatory: boolean;
  fileName?: string | null;
  fileUrl?: string | null;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  centralDocumentVersion?: string | null;
  centralDocumentRepositoryPath?: string | null;
  centralDocumentRenditionPath?: string | null;
  centralDocumentContentType?: string | null;
  centralDocumentAnnotationStateJson?: string | null;
  canUploadAtCurrentStage: boolean;
  notes?: string | null;
  uploadedById?: string | null;
  uploadedAt?: string | null;
}

export interface ProcedureCaseSubmissionDocumentRequirement {
  name: string;
  documentType?: string | null;
  appliesTo: 'All' | 'Rent' | 'Sale';
  isMandatory: boolean;
}

export interface ProcedureCaseActivity {
  id: string;
  action: string;
  stageName?: string | null;
  details?: string | null;
  performedById: string;
  performedAt: string;
}

export interface ProcedureCaseDetail extends ProcedureCaseSummary {
  sourceDepartment?: string | null;
  receivedDate?: string | null;
  description?: string | null;
  currentStageOwner?: string | null;
  canEditCurrentStage: boolean;
  currentStageFieldKeys: string[];
  fields: ProcedureCaseField[];
  checklistItems: ProcedureCaseChecklistItem[];
  documents: ProcedureCaseDocument[];
  activities: ProcedureCaseActivity[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious?: boolean;
  hasNext?: boolean;
}

interface CreateCasePayload {
  module: string;
  entityType: string;
  title?: string;
  referenceNumber?: string;
  applicantName?: string;
  sourceDepartment?: string;
  organizationLevelId?: string;
  organizationUnitId?: string;
  receivedDate?: string;
  description?: string;
  fieldValues?: Record<string, string | null>;
}

class ProcedureCaseService {
  async getSubmissionDocumentRequirements(
    module: string,
    entityType: string
  ): Promise<ProcedureCaseSubmissionDocumentRequirement[]> {
    const response = await apiService.get<ApiResponse<ProcedureCaseSubmissionDocumentRequirement[]>>(
      '/procedure-cases/submission-document-requirements',
      { module, entityType }
    );
    return response.data || [];
  }

  async listCases(module: string, entityType: string): Promise<ProcedureCaseSummary[]> {
    const response = await apiService.get<ApiResponse<ProcedureCaseSummary[]>>('/procedure-cases', {
      module,
      entityType,
    });
    return response.data || [];
  }

  async listCasesPage(
    module: string,
    entityType: string,
    page = 1,
    pageSize = 10,
    mineOnly = false
  ): Promise<PagedResult<ProcedureCaseSummary>> {
    const response = await apiService.get<ApiResponse<PagedResult<ProcedureCaseSummary>>>(
      '/procedure-cases/paged',
      {
        module,
        entityType,
        page,
        pageSize,
        mineOnly,
      }
    );

    return (
      response.data || {
        items: [],
        totalCount: 0,
        page,
        pageSize,
        totalPages: 1,
      }
    );
  }

  async listModuleCases(module: string, mineOnly = false): Promise<ProcedureCaseSummary[]> {
    const response = await apiService.get<ApiResponse<ProcedureCaseSummary[]>>('/procedure-cases', {
      module,
      mineOnly,
    });
    return response.data || [];
  }

  async getCase(id: string): Promise<ProcedureCaseDetail | null> {
    const response = await apiService.get<ApiResponse<ProcedureCaseDetail>>(`/procedure-cases/${id}`);
    return response.data || null;
  }

  async createCase(payload: CreateCasePayload): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>('/procedure-cases', payload);
    return response.data;
  }

  async createLinkedLegalMatter(
    id: string,
    matterType: string,
    description?: string | null
  ): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/legal-matters`,
      { matterType, description }
    );
    return response.data;
  }

  async updateFields(
    id: string,
    payload: {
      fieldValues: Record<string, string | null>;
      referenceNumber?: string | null;
      applicantName?: string | null;
      sourceDepartment?: string | null;
      organizationLevelId?: string | null;
      organizationUnitId?: string | null;
      receivedDate?: string | null;
      description?: string | null;
    }
  ): Promise<ProcedureCaseDetail> {
    const response = await apiService.put<ApiResponse<ProcedureCaseDetail>>(`/procedure-cases/${id}/fields`, payload);
    return response.data;
  }

  async updateChecklistItem(id: string, checklistItemId: string, isCompleted: boolean): Promise<ProcedureCaseDetail> {
    const response = await apiService.put<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/checklist/${checklistItemId}`,
      { isCompleted }
    );
    return response.data;
  }

  async attachDocument(
    id: string,
    documentId: string,
    payload: { fileName?: string | null; fileUrl?: string | null; notes?: string | null }
  ): Promise<ProcedureCaseDetail> {
    const response = await apiService.put<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/documents/${documentId}`,
      payload
    );
    return response.data;
  }

  async uploadDocument(
    id: string,
    documentId: string,
    file: File,
    notes?: string | null
  ): Promise<ProcedureCaseDetail> {
    const formData = new FormData();
    formData.append('file', file);
    if (notes) {
      formData.append('notes', notes);
    }

    const response = await rawApiService.request<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/documents/${documentId}/upload`,
      {
        method: 'POST',
        body: formData,
      }
    );

    return response.data;
  }

  async downloadDocumentContent(id: string, documentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(`/procedure-cases/${id}/documents/${documentId}/content`);
  }

  async completeStage(id: string, notes?: string | null): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>(`/procedure-cases/${id}/complete-stage`, {
      notes,
    });
    return response.data;
  }

  async syncLegalTransferFeePaymentStatus(id: string): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/legal-transfer-fee-payment/sync`,
      {}
    );
    return response.data;
  }

  async signLegalTransferExecutedDocument(
    id: string,
    documentId: string,
    payload: { signatureRole: string; notes?: string | null }
  ): Promise<ProcedureCaseDetail> {
    const response = await rawApiService.request<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/documents/${documentId}/legal-transfer-signature`,
      {
        method: 'POST',
        body: JSON.stringify(payload),
        signal: new AbortController().signal,
      }
    );
    return response.data;
  }

  async applyReviewAction(
    id: string,
    action: 'Reject' | 'RequestClarification',
    reason: string
  ): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>(
      `/procedure-cases/${id}/review-action`,
      { action, reason }
    );
    return response.data;
  }
}

export const procedureCaseService = new ProcedureCaseService();
