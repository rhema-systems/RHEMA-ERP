import { compatibleApiService as apiService } from './compatibleApiService';

export interface PlanningProcedure {
  title: string;
  entityType: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface PlanningWorkspaceStage {
  name: string;
  owner: string;
  checklist: string[];
}

export interface PlanningWorkspaceDocument {
  name: string;
  requiredFrom: string;
  isMandatory: boolean;
}

export interface PlanningWorkspaceField {
  key: string;
  label: string;
  type: string;
  options?: string[] | null;
}

export interface PlanningWorkspaceHandoff {
  fromRole: string;
  toRole: string;
  trigger: string;
}

export interface PlanningProcedureWorkspace {
  procedure: PlanningProcedure;
  stages: PlanningWorkspaceStage[];
  requiredDocuments: PlanningWorkspaceDocument[];
  intakeFields: PlanningWorkspaceField[];
  outputs: string[];
  handoffs: PlanningWorkspaceHandoff[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class PlanningProcedureService {
  async getProcedures(): Promise<PlanningProcedure[]> {
    const response = await apiService.get<ApiResponse<PlanningProcedure[]>>('/development/planning/procedures');
    return response.data || [];
  }

  async getProcedureWorkspace(entityType: string): Promise<PlanningProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<PlanningProcedureWorkspace>>(
      `/development/planning/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const planningProcedureService = new PlanningProcedureService();
