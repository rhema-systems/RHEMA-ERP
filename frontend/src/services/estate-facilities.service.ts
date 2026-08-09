import { compatibleApiService as apiService } from './compatibleApiService';
import type { ProcedureWorkspaceType } from '@/lib/procedure-workspace';

export interface FacilitiesProcedure {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
  workspaceType?: ProcedureWorkspaceType;
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

export interface PublishFacilitiesBillingDocumentRequest {
  documentTitle: string;
  documentType?: string;
  billingDocumentKind?: string;
  facilitiesBillingReference?: string;
  financeArReference?: string;
  propertyUnit?: string;
  customerAccountReference?: string;
  repositoryPath?: string;
  externalDocumentUrl?: string;
  fileName?: string;
  contentType?: string;
  fileSize?: number;
  notes?: string;
}

export interface FacilitiesBillingDmsPublication {
  id: string;
  documentReference: string;
  title: string;
  sourceModule: string;
  sourceLabel: string;
  sourceEntityType?: string | null;
  sourceRecordReference?: string | null;
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
  publishedToCentralDmsAt?: string | null;
}

export interface CreateFacilitiesArInvoiceLineItem {
  lineItemType: 'Product' | 'GLAccount';
  productId?: string;
  glAccountId?: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxCode?: string;
  discountPercentage?: number;
}

export interface CreateFacilitiesArInvoiceRequest {
  customerId: string;
  invoiceDate: string;
  dueDate?: string;
  reference?: string;
  notes?: string;
  currencyCode: string;
  exchangeRate?: number;
  lineItems: CreateFacilitiesArInvoiceLineItem[];
}

export interface FacilitiesArInvoice {
  id: string;
  invoiceNumber: string;
  customerId: string;
  customerName: string;
  invoiceDate: string;
  dueDate?: string | null;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  status: string;
  currencyCode: string;
  notes?: string | null;
}

export interface CreateFacilitiesArPaymentRequest {
  customerId: string;
  paymentDate: string;
  totalAmount: number;
  paymentMethod: string;
  currencyCode: string;
  exchangeRate?: number;
  transactionReference?: string;
  notes?: string;
  isCreditNote?: boolean;
}

export interface FacilitiesArPayment {
  id: string;
  paymentNumber: string;
  customerId: string;
  customerName: string;
  paymentDate: string;
  totalAmount: number;
  allocatedAmount: number;
  unallocatedAmount: number;
  paymentMethod: string;
  status: string;
  currencyCode: string;
  notes?: string | null;
}

export interface FacilitiesArResultNotificationRequest {
  actionType: string;
  financeArEntityId?: string | null;
  financeArReference?: string | null;
  customerId?: string | null;
  customerName?: string | null;
  amount?: number | null;
  currencyCode?: string | null;
  sourceRecordReference?: string | null;
  propertyUnit?: string | null;
  notes?: string | null;
  actionUrl?: string | null;
}

export interface EstateFacilityDutyRosterItem {
  id: string;
  rosterReference: string;
  employeeProfileId?: string | null;
  employeeNumber?: string | null;
  staffName: string;
  staffType: string;
  dutyType: string;
  propertyReference?: string | null;
  propertyUnit?: string | null;
  serviceAreaType: string;
  serviceAreaName: string;
  frequency: string;
  dayPattern?: string | null;
  startDate: string;
  endDate?: string | null;
  shiftStart: string;
  shiftEnd: string;
  supervisorName?: string | null;
  toolsIssued?: string | null;
  suppliesIssued?: string | null;
  checklist?: string | null;
  attendanceStatus: string;
  completionStatus: string;
  qualityStatus: string;
  linkedMaintenanceReference?: string | null;
  linkedComplaintReference?: string | null;
  linkedProcedureCaseReference?: string | null;
  lastAttendanceAt?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface UpsertEstateFacilityDutyRosterRequest {
  employeeProfileId?: string | null;
  employeeNumber?: string | null;
  staffName: string;
  staffType: string;
  dutyType: string;
  propertyReference?: string | null;
  propertyUnit?: string | null;
  serviceAreaType: string;
  serviceAreaName: string;
  frequency: string;
  dayPattern?: string | null;
  startDate: string;
  endDate?: string | null;
  shiftStart: string;
  shiftEnd: string;
  supervisorName?: string | null;
  toolsIssued?: string | null;
  suppliesIssued?: string | null;
  checklist?: string | null;
  attendanceStatus: string;
  completionStatus: string;
  qualityStatus: string;
  linkedMaintenanceReference?: string | null;
  linkedComplaintReference?: string | null;
  linkedProcedureCaseReference?: string | null;
  notes?: string | null;
}

export interface UpdateEstateFacilityDutyAttendanceRequest {
  attendanceStatus: string;
  completionStatus: string;
  qualityStatus: string;
  linkedMaintenanceReference?: string | null;
  linkedComplaintReference?: string | null;
  notes?: string | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

class EstateFacilitiesService {
  async getProcedures(): Promise<FacilitiesProcedure[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProcedure[]>>(
      '/estate/facilities/procedures'
    );
    return response.data || [];
  }

  async getProcedureWorkspace(
    entityType: string
  ): Promise<FacilitiesProcedureWorkspace | null> {
    const response = await apiService.get<
      ApiResponse<FacilitiesProcedureWorkspace>
    >(`/estate/facilities/procedures/${encodeURIComponent(entityType)}`);
    return response.data || null;
  }

  async publishBillingDocumentToDms(
    request: PublishFacilitiesBillingDocumentRequest
  ): Promise<FacilitiesBillingDmsPublication> {
    const response = await apiService.post<
      ApiResponse<FacilitiesBillingDmsPublication>
    >('/estate/facilities/billing-documents/publish-to-dms', request);
    return response.data;
  }

  async createArInvoice(
    request: CreateFacilitiesArInvoiceRequest
  ): Promise<FacilitiesArInvoice> {
    return apiService.post<FacilitiesArInvoice>(
      '/estate/facilities/ar-billing/invoices',
      {
        invoice: request,
        sourceRecordReference: request.reference || null,
        propertyUnit: null,
      }
    );
  }

  async createArPayment(
    request: CreateFacilitiesArPaymentRequest
  ): Promise<FacilitiesArPayment> {
    return apiService.post<FacilitiesArPayment>(
      '/estate/facilities/ar-billing/payments',
      {
        payment: request,
        sourceRecordReference: request.transactionReference || null,
        propertyUnit: null,
      }
    );
  }

  async notifyArResult(
    request: FacilitiesArResultNotificationRequest
  ): Promise<void> {
    await apiService.post('/estate/facilities/ar-billing/results', request);
  }

  async getDutyRoster(): Promise<EstateFacilityDutyRosterItem[]> {
    const response = await apiService.get<ApiResponse<EstateFacilityDutyRosterItem[]>>(
      '/estate/facilities/duty-roster'
    );
    return response.data || [];
  }

  async createDutyRosterItem(
    request: UpsertEstateFacilityDutyRosterRequest
  ): Promise<EstateFacilityDutyRosterItem> {
    const response = await apiService.post<ApiResponse<EstateFacilityDutyRosterItem>>(
      '/estate/facilities/duty-roster',
      request
    );
    return response.data;
  }

  async updateDutyAttendance(
    id: string,
    request: UpdateEstateFacilityDutyAttendanceRequest
  ): Promise<EstateFacilityDutyRosterItem> {
    const response = await apiService.post<ApiResponse<EstateFacilityDutyRosterItem>>(
      `/estate/facilities/duty-roster/${encodeURIComponent(id)}/attendance`,
      request
    );
    return response.data;
  }
}

export const estateFacilitiesService = new EstateFacilitiesService();
