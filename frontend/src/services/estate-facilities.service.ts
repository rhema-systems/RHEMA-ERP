import { compatibleApiService as apiService } from './compatibleApiService';
import type { ProcedureWorkspaceType } from '@/lib/procedure-workspace';

export interface FacilitiesProcedure {
  title: string;
  entityType: string;
  icon: string;
  stageCount: number;
  accent: string;
  workspaceType?: ProcedureWorkspaceType;
}

export interface FacilitiesWorkspaceStage {
  name: string;
  owner: string;
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

export interface FacilitiesProviderOption {
  id: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  performanceRating?: number | null;
  phone?: string | null;
  email?: string | null;
  categories: string[];
  contracts: Array<{
    id: string;
    businessPartnerId: string;
    contractNumber: string;
    contractTitle: string;
    contractValue: number;
    currency: string;
    paymentTerms?: string | null;
    startDate?: string | null;
    endDate?: string | null;
  }>;
}

export interface FacilitiesProviderInvoice {
  id: string;
  invoiceNumber: string;
  supplierInvoiceNumber?: string | null;
  invoiceDate: string;
  dueDate?: string | null;
  purchaseOrderId?: string | null;
  totalAmount: number;
  paidAmount: number;
  currencyCode: string;
  status: string | number;
}

export interface FacilitiesProviderRate {
  id: string;
  businessPartnerId: string;
  contractId?: string | null;
  contractNumber?: string | null;
  serviceName: string;
  unitOfMeasure: string;
  rate: number;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export type FacilitiesProviderRateRequest = Omit<FacilitiesProviderRate, 'id' | 'businessPartnerId' | 'contractNumber'>;

export interface FacilitiesProviderAssignment {
  id: string;
  businessPartnerId: string;
  estateManagedAssetId: string;
  assetCode?: string | null;
  assetName?: string | null;
  assetLocation?: string | null;
  assetType?: string | null;
  contractId?: string | null;
  contractNumber?: string | null;
  contractTitle?: string | null;
  serviceScope: string;
  serviceArea?: string | null;
  assignmentStatus: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  schedulePattern?: string | null;
  supervisorName?: string | null;
  slaReference?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface FacilitiesProviderAssignmentRequest {
  estateManagedAssetId: string;
  contractId?: string | null;
  serviceScope: string;
  serviceArea?: string | null;
  assignmentStatus: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  schedulePattern?: string | null;
  supervisorName?: string | null;
  slaReference?: string | null;
  notes?: string | null;
}

export interface FacilitiesBudgetYear {
  id: string;
  fiscalYearName: string;
  fiscalYearCode: string;
  startDate: string;
  endDate: string;
  hasOfficialBudget: boolean;
}

export interface FacilitiesBudgetReport {
  fiscalYearId: string;
  fiscalYearName: string;
  currencyCode: string;
  scenarioName: string;
  plannedAmount: number;
  actualExpense: number;
  variance: number;
  lines: Array<{
    accountCode: string;
    accountName: string;
    periodCode: string;
    periodNumber: number;
    plannedAmount: number;
    actualExpense: number;
    variance: number;
  }>;
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
  propertyUnit: string;
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

type FinanceArInvoiceResponse = Omit<FacilitiesArInvoice, 'customerId'> & {
  businessPartnerId: string;
};

function facilitiesInvoice(invoice: FinanceArInvoiceResponse): FacilitiesArInvoice {
  const { businessPartnerId, ...details } = invoice;
  return { ...details, customerId: businessPartnerId };
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
  dutyDate?: string | null;
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
  inventoryIssueVoucherId?: string | null;
  inventoryIssueVoucherNumber?: string | null;
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
  inventoryIssueVoucherId?: string | null;
  inventoryIssueVoucherNumber?: string | null;
  checklist?: string | null;
  attendanceStatus: string;
  completionStatus: string;
  qualityStatus: string;
  linkedMaintenanceReference?: string | null;
  linkedComplaintReference?: string | null;
  linkedProcedureCaseReference?: string | null;
  notes?: string | null;
}

export interface FacilitiesPropertyInvoice {
  id: string;
  invoiceNumber: string;
  reference?: string | null;
  invoiceDate: string;
  dueDate?: string | null;
  status: string;
  totalAmount: number;
  paidAmount: number;
  currencyCode: string;
  canRelease: boolean;
}

export interface FacilitiesStaffOption {
  id: string;
  employeeProfileId: string | null;
  employeeNumber: string;
  staffName: string;
  department: string | null;
  position: string | null;
}

export interface FacilitiesIssueVoucherOption {
  id: string;
  voucherNumber: string;
  status: string;
  issuedAtUtc: string;
  supplies: string;
}

export interface FacilitiesPropertyOption {
  id: string;
  assetCode: string;
  name: string;
  location: string | null;
  projectCode: string | null;
  projectTitle: string | null;
  blockName: string | null;
  floorLabel: string | null;
  unitType: string | null;
  assetType: string | number;
}

export interface FacilitiesUnitOption {
  id: string;
  assetCode: string;
  name: string;
  projectUnitCode: string | null;
  blockName: string | null;
  floorLabel: string | null;
  unitType: string | null;
  assetType: string | number;
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
  async getBudgetYears(): Promise<FacilitiesBudgetYear[]> {
    const response = await apiService.get<ApiResponse<FacilitiesBudgetYear[]>>(
      '/estate/facilities/budget/fiscal-years'
    );
    return response.data || [];
  }

  async getBudgetReport(fiscalYearId: string): Promise<FacilitiesBudgetReport> {
    const response = await apiService.get<ApiResponse<FacilitiesBudgetReport>>(
      `/estate/facilities/budget/fiscal-years/${encodeURIComponent(fiscalYearId)}`
    );
    return response.data;
  }

  async searchIssueVouchers(search: string): Promise<FacilitiesIssueVoucherOption[]> {
    const response = await apiService.get<ApiResponse<FacilitiesIssueVoucherOption[]>>(
      `/estate/facilities/duty-roster/issue-vouchers?search=${encodeURIComponent(search)}`
    );
    return response.data || [];
  }

  async assignSiteOfficer(assetId: string, employeeId: string): Promise<{
    id: string;
    responsibleOfficerEmployeeId: string;
    responsibleOfficerEmployeeNumber: string;
    responsibleOfficerName: string;
  }> {
    return apiService.put<{
      id: string;
      responsibleOfficerEmployeeId: string;
      responsibleOfficerEmployeeNumber: string;
      responsibleOfficerName: string;
    }>(`/estate/facilities/sites/${assetId}/responsible-officer`, { employeeId });
  }

  async getPropertyArInvoices(propertyUnit: string): Promise<FacilitiesPropertyInvoice[]> {
    return apiService.get<FacilitiesPropertyInvoice[]>(
      `/estate/facilities/ar-billing/invoices?propertyUnit=${encodeURIComponent(propertyUnit)}`
    );
  }

  async releaseArInvoice(id: string, propertyUnit: string): Promise<FacilitiesArInvoice> {
    const invoice = await apiService.post<FinanceArInvoiceResponse>(
      `/estate/facilities/ar-billing/invoices/${id}/release`, { propertyUnit }
    );
    return facilitiesInvoice(invoice);
  }

  async searchDutyStaff(search: string): Promise<FacilitiesStaffOption[]> {
    const response = await apiService.get<ApiResponse<FacilitiesStaffOption[]>>(
      `/estate/facilities/duty-roster/staff?search=${encodeURIComponent(search)}`
    );
    return response.data || [];
  }

  async searchDutyProperties(search: string): Promise<FacilitiesPropertyOption[]> {
    const response = await apiService.get<ApiResponse<FacilitiesPropertyOption[]>>(
      `/estate/facilities/duty-roster/properties?search=${encodeURIComponent(search)}`
    );
    return response.data || [];
  }

  async searchDutyUnits(propertyId: string, search: string): Promise<FacilitiesUnitOption[]> {
    const response = await apiService.get<ApiResponse<FacilitiesUnitOption[]>>(
      `/estate/facilities/duty-roster/properties/${encodeURIComponent(propertyId)}/units?search=${encodeURIComponent(search)}`
    );
    return response.data || [];
  }

  async getProviderInvoices(providerId: string): Promise<FacilitiesProviderInvoice[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProviderInvoice[]>>(
      `/estate/facilities/providers/${providerId}/invoices`
    );
    return response.data || [];
  }

  async getProviderRates(providerId: string): Promise<FacilitiesProviderRate[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProviderRate[]>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/rates`
    );
    return response.data || [];
  }

  async getProviderAssignments(providerId: string): Promise<FacilitiesProviderAssignment[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProviderAssignment[]>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/assignments`
    );
    return response.data || [];
  }

  async createProviderAssignment(providerId: string, request: FacilitiesProviderAssignmentRequest): Promise<FacilitiesProviderAssignment> {
    const response = await apiService.post<ApiResponse<FacilitiesProviderAssignment>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/assignments`, request
    );
    return response.data;
  }

  async updateProviderAssignment(providerId: string, assignmentId: string, request: FacilitiesProviderAssignmentRequest): Promise<FacilitiesProviderAssignment> {
    const response = await apiService.put<ApiResponse<FacilitiesProviderAssignment>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/assignments/${encodeURIComponent(assignmentId)}`, request
    );
    return response.data;
  }

  async createProviderRate(providerId: string, request: FacilitiesProviderRateRequest): Promise<FacilitiesProviderRate> {
    const response = await apiService.post<ApiResponse<FacilitiesProviderRate>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/rates`, request
    );
    return response.data;
  }

  async updateProviderRate(providerId: string, rateId: string, request: FacilitiesProviderRateRequest): Promise<FacilitiesProviderRate> {
    const response = await apiService.put<ApiResponse<FacilitiesProviderRate>>(
      `/estate/facilities/providers/${encodeURIComponent(providerId)}/rates/${encodeURIComponent(rateId)}`, request
    );
    return response.data;
  }

  async getApprovedProviders(): Promise<FacilitiesProviderOption[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProviderOption[]>>(
      '/estate/facilities/providers'
    );
    return response.data || [];
  }

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
    const { customerId, propertyUnit, ...details } = request;
    const invoice = await apiService.post<FinanceArInvoiceResponse>(
      '/estate/facilities/ar-billing/invoices',
      {
        invoice: { ...details, businessPartnerId: customerId },
        sourceRecordReference: request.reference || null,
        propertyUnit,
      }
    );
    return facilitiesInvoice(invoice);
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

  async getDutyRoster(date?: string): Promise<EstateFacilityDutyRosterItem[]> {
    const response = await apiService.get<ApiResponse<EstateFacilityDutyRosterItem[]>>(
      `/estate/facilities/duty-roster${date ? `?from=${encodeURIComponent(date)}&to=${encodeURIComponent(date)}` : ''}`
    );
    return response.data || [];
  }

  async getMyDutyRoster(from?: string, to?: string): Promise<EstateFacilityDutyRosterItem[]> {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const response = await apiService.get<ApiResponse<EstateFacilityDutyRosterItem[]>>(
      `/estate/facilities/duty-roster/mine${params.size ? `?${params.toString()}` : ''}`
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

  async updateDutyRosterItem(
    id: string,
    request: UpsertEstateFacilityDutyRosterRequest,
  ): Promise<EstateFacilityDutyRosterItem> {
    const response = await apiService.put<ApiResponse<EstateFacilityDutyRosterItem>>(
      `/estate/facilities/duty-roster/${encodeURIComponent(id)}`,
      request,
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
