import { compatibleApiService as apiService } from './compatibleApiService';

export interface LegalProcedure {
  title: string;
  entityType: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface LegalWorkspaceStage {
  name: string;
  owner: string;
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

export interface LegalDashboardQueueItem {
  id: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  currentStageName: string;
  owner?: string | null;
  statusDetail: string;
  dueDate?: string | null;
  risk?: string | null;
}

export interface LegalSopControlItem {
  id: string;
  entityType: string;
  procedureTitle: string;
  category: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  currentStageName: string;
  owner?: string | null;
  statusDetail: string;
  dueDate?: string | null;
  reference?: string | null;
  risk?: string | null;
  updatedAt: string;
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
  templateDraftingControls: number;
  fileMovementControls: number;
  signatureDispatchControls: number;
  financePaymentControls: number;
  estateRegistrationControls: number;
  courtCalendarControls: number;
  externalCounselControls: number;
  legalOpinionControls: number;
  pendingSignatureMatters: LegalDashboardQueueItem[];
  pendingPaymentMatters: LegalDashboardQueueItem[];
  awaitingEstateReturnMatters: LegalDashboardQueueItem[];
  overdueCourtDeadlines: LegalDashboardQueueItem[];
  sopControlItems: LegalSopControlItem[];
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

export interface LegalMatterRegisterItem {
  id: string;
  entityType: string;
  procedureTitle: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  status: string;
  currentStageName: string;
  currentAssignedRole?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  matterNumber?: string | null;
  propertyFileReference?: string | null;
  propertyNumber?: string | null;
  assignedLegalOfficer?: string | null;
  sourceDepartment?: string | null;
  paymentStatus?: string | null;
  signatureStatus?: string | null;
  estateReturnStatus?: string | null;
  risk?: string | null;
  responseDeadline?: string | null;
  nextHearingDate?: string | null;
  courtName?: string | null;
  caseNumber?: string | null;
}

export interface LegalMatterRegister {
  totalCount: number;
  items: LegalMatterRegisterItem[];
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

  async getMatterRegister(params: {
    status?: string;
    entityType?: string;
    search?: string;
    pageSize?: number;
  } = {}): Promise<LegalMatterRegister> {
    const response = await apiService.get<ApiResponse<LegalMatterRegister>>(
      '/legal/procedures/matter-register',
      params
    );
    return response.data || { totalCount: 0, items: [] };
  }

  async getProcedureWorkspace(entityType: string): Promise<LegalProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<LegalProcedureWorkspace>>(
      `/legal/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const legalProcedureService = new LegalProcedureService();
