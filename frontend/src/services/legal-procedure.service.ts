import { compatibleApiService as apiService } from './compatibleApiService';

export interface LegalProcedure {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface LegalWorkspaceStage {
  name: string;
  owner: string;
  summary: string;
  checklist: string[];
}

export interface LegalWorkspaceDocument {
  name: string;
  requiredFrom: string;
  isMandatory: boolean;
}

export interface LegalWorkspaceField {
  key: string;
  label: string;
  type: string;
  options?: string[] | null;
}

export interface LegalWorkspaceHandoff {
  fromRole: string;
  toRole: string;
  trigger: string;
}

export interface LegalProcedureWorkspace {
  procedure: LegalProcedure;
  stages: LegalWorkspaceStage[];
  requiredDocuments: LegalWorkspaceDocument[];
  intakeFields: LegalWorkspaceField[];
  outputs: string[];
  handoffs: LegalWorkspaceHandoff[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class LegalProcedureService {
  async getProcedures(): Promise<LegalProcedure[]> {
    const response = await apiService.get<ApiResponse<LegalProcedure[]>>('/legal/procedures');
    return response.data || [];
  }

  async getProcedureWorkspace(entityType: string): Promise<LegalProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<LegalProcedureWorkspace>>(
      `/legal/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const legalProcedureService = new LegalProcedureService();
