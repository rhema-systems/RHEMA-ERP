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

export interface LegalDashboard {
  totalMatters: number;
  openMatters: number;
  completedMatters: number;
  propertyLinkedMatters: number;
  activeCourtCases: number;
  courtPending: number;
  courtWon: number;
  courtLost: number;
  courtSettled: number;
  courtWithdrawn: number;
  hearingsNext30Days: number;
  overdueResponseDeadlines: number;
  pendingSignatures: number;
  pendingPayments: number;
  awaitingEstateReturn: number;
  upcomingCourtEvents: Array<{
    id: string;
    title: string;
    referenceNumber?: string | null;
    courtName?: string | null;
    caseNumber?: string | null;
    responseDeadline?: string | null;
    nextHearingDate?: string | null;
    risk?: string | null;
    currentStageName: string;
  }>;
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

  async getDashboard(): Promise<LegalDashboard> {
    const response = await apiService.get<ApiResponse<LegalDashboard>>('/legal/procedures/dashboard');
    return response.data;
  }

  async getProcedureWorkspace(entityType: string): Promise<LegalProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<LegalProcedureWorkspace>>(
      `/legal/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const legalProcedureService = new LegalProcedureService();
