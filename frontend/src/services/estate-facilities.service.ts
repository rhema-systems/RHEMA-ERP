import { compatibleApiService as apiService } from './compatibleApiService';

export interface FacilitiesProcedure {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface FacilitiesWorkspaceStage {
  name: string;
  owner: string;
  summary: string;
  checklist: string[];
}

export interface FacilitiesWorkspaceDocument {
  name: string;
  requiredFrom: string;
  isMandatory: boolean;
}

export interface FacilitiesWorkspaceField {
  key: string;
  label: string;
  type: string;
  options?: string[] | null;
}

export interface FacilitiesWorkspaceHandoff {
  fromRole: string;
  toRole: string;
  trigger: string;
}

export interface FacilitiesProcedureWorkspace {
  procedure: FacilitiesProcedure;
  stages: FacilitiesWorkspaceStage[];
  requiredDocuments: FacilitiesWorkspaceDocument[];
  intakeFields: FacilitiesWorkspaceField[];
  outputs: string[];
  handoffs: FacilitiesWorkspaceHandoff[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class EstateFacilitiesService {
  async getProcedures(): Promise<FacilitiesProcedure[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProcedure[]>>('/estate/facilities/procedures');
    return response.data || [];
  }

  async getProcedureWorkspace(entityType: string): Promise<FacilitiesProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<FacilitiesProcedureWorkspace>>(
      `/estate/facilities/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const estateFacilitiesService = new EstateFacilitiesService();
