import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export interface ProcedureCaseSummary {
  id: string;
  module: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  status: string;
  currentStageIndex: number;
  currentStageName: string;
  currentAssignedRole?: string | null;
  usesConfiguredWorkflow: boolean;
  workflowInstanceId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
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
  isMandatory: boolean;
  fileName?: string | null;
  fileUrl?: string | null;
  notes?: string | null;
  uploadedById?: string | null;
  uploadedAt?: string | null;
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

interface FileUploadResult {
  success: boolean;
  fileName: string;
  originalFileName: string;
  filePath: string;
  publicUrl: string;
  fileSize: number;
  contentType: string;
  category: string;
  tenantId?: string | null;
  uploadedAt: string;
}

interface CreateCasePayload {
  module: string;
  entityType: string;
  title?: string;
  referenceNumber?: string;
  applicantName?: string;
  sourceDepartment?: string;
  receivedDate?: string;
  description?: string;
  fieldValues?: Record<string, string | null>;
}

const resolveUploadedFileUrl = (fileUrl: string, fallbackPath: string): string => {
  const url = fileUrl || fallbackPath;

  if (!url || url.startsWith('http://') || url.startsWith('https://')) {
    return url;
  }

  const apiBase = process.env.NEXT_PUBLIC_API_URL;
  if (!apiBase) {
    return url;
  }

  return `${apiBase.replace(/\/api\/?$/i, '').replace(/\/$/, '')}/${url.replace(/^\//, '')}`;
};

class ProcedureCaseService {
  async listCases(module: string, entityType: string): Promise<ProcedureCaseSummary[]> {
    const response = await apiService.get<ApiResponse<ProcedureCaseSummary[]>>('/procedure-cases', {
      module,
      entityType,
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

  async updateFields(
    id: string,
    payload: {
      fieldValues: Record<string, string | null>;
      referenceNumber?: string | null;
      applicantName?: string | null;
      sourceDepartment?: string | null;
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
    formData.append('category', 'procedure-case-documents');

    const upload = await rawApiService.request<FileUploadResult>('/FileUpload/single', {
      method: 'POST',
      body: formData,
    });

    return this.attachDocument(id, documentId, {
      fileName: upload.originalFileName || upload.fileName || file.name,
      fileUrl: resolveUploadedFileUrl(upload.publicUrl, upload.filePath),
      notes,
    });
  }

  async completeStage(id: string, notes?: string | null): Promise<ProcedureCaseDetail> {
    const response = await apiService.post<ApiResponse<ProcedureCaseDetail>>(`/procedure-cases/${id}/complete-stage`, {
      notes,
    });
    return response.data;
  }
}

export const procedureCaseService = new ProcedureCaseService();
