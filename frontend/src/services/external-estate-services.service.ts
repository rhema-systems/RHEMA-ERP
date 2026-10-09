import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export interface ExternalEstateRequestType {
  code: string;
  title: string;
  module: string;
  entityType: string;
  category: string;
}

export interface ExternalEstateServiceRequest {
  id: string;
  module: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  sourceDepartment?: string | null;
  status: string;
  currentStageIndex: number;
  currentStageName: string;
  currentAssignedRole?: string | null;
  customerIntakeUploadClosed: boolean;
  documents?: ExternalEstateRequestDocument[];
  fieldValues?: Record<string, string | null>;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ExternalEstateRequestDocument {
  id: string;
  name: string;
  requiredFrom?: string | null;
  providedBy: string;
  isMandatory: boolean;
  fileName?: string | null;
  uploadedAt?: string | null;
  sourceLabel?: string | null;
}

export interface ExternalEstateRequestsPage {
  items: ExternalEstateServiceRequest[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ExternalPropertyPortfolio {
  customers: Array<{ id: string; partnerName: string; customerAccountNumber?: string | null }>;
  properties: Array<{
    id: string;
    assetCode: string;
    projectUnitCode?: string | null;
    name: string;
    status: string | number;
    transactionType: 'Rental' | 'Lease' | 'Purchased';
    location?: string | null;
    town?: string | null;
    district?: string | null;
    agreementReference?: string | null;
    agreementDate?: string | null;
    actualPossessionDate?: string | null;
    leaseTermYears?: number | null;
    monthlyRent?: number | null;
    fullTermLeaseAmount?: number | null;
    currencyCode: string;
    nextRentBillingDate?: string | null;
    rentGracePeriodDays: number;
    rentPenaltyMethod: string;
    rentPenaltyValue: number;
    rentPenaltyCapAmount?: number | null;
    customerBusinessPartnerId?: string | null;
  }>;
  invoices: Array<{
    id: string;
    invoiceNumber: string;
    businessPartnerId: string;
    invoiceDate: string;
    dueDate?: string | null;
    totalAmount: number;
    paidAmount: number;
    balanceAmount: number;
    currencyCode: string;
    status: string | number;
    reference?: string | null;
    description?: string | null;
    receipts: Array<{
      customerPaymentId: string;
      paymentNumber: string;
      paymentDate: string;
      amount: number;
      paymentCurrencyCode: string;
      paymentMethod: string;
      transactionReference?: string | null;
      status: string;
    }>;
  }>;
  legalTransfers: Array<{
    id: string;
    module: string;
    entityType: string;
    title: string;
    referenceNumber?: string | null;
    status: string;
    currentStageName: string;
    currentAssignedRole?: string | null;
    sourceProcedureCaseId?: string | null;
    sourceRecordReference?: string | null;
    propertyNumber?: string | null;
    transferFeePayable?: string | null;
    paymentStatus?: string | null;
    draftDocumentReference?: string | null;
    executedTransferFormFileName?: string | null;
    signedDocuments?: Array<{
      id: string;
      name: string;
      fileName?: string | null;
      uploadedAt?: string | null;
    }>;
    canDownloadDraft: boolean;
    canUploadExecutedTransferForm: boolean;
    fieldValues?: Record<string, string | null>;
    createdAt: string;
    updatedAt?: string | null;
  }>;
}

export interface CreateExternalEstateServiceRequest {
  requestType: string;
  applicantName?: string;
  contact?: string;
  propertyReference?: string;
  location?: string;
  category?: string;
  priority?: string;
  serviceImpact?: string;
  targetDate?: string;
  description?: string;
  additionalValues?: Record<string, string | null | undefined>;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  pagination?: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
  };
}

class ExternalEstateServicesService {
  async getRequestTypes(): Promise<ExternalEstateRequestType[]> {
    const response = await apiService.get<
      ApiResponse<ExternalEstateRequestType[]>
    >('/estate/external/request-types');
    return response.data || [];
  }

  async getMyRequests(): Promise<ExternalEstateServiceRequest[]> {
    const response = await apiService.get<
      ApiResponse<ExternalEstateServiceRequest[]>
    >('/estate/external/requests');
    return response.data || [];
  }

  async getMyRequestsPage(query: {
    page: number;
    pageSize: number;
    source?: 'estateServices' | 'all';
  }): Promise<ExternalEstateRequestsPage> {
    const params = new URLSearchParams({
      page: String(query.page),
      pageSize: String(query.pageSize),
    });
    if (query.source && query.source !== 'all') {
      params.set('source', query.source);
    }

    const response = await apiService.get<
      ApiResponse<ExternalEstateServiceRequest[]>
    >(`/estate/external/requests?${params.toString()}`);
    return {
      items: response.data || [],
      page: response.pagination?.page ?? query.page,
      pageSize: response.pagination?.pageSize ?? query.pageSize,
      totalCount: response.pagination?.totalCount ?? response.data?.length ?? 0,
      totalPages: response.pagination?.totalPages ?? 1,
      hasPreviousPage: response.pagination?.hasPreviousPage ?? query.page > 1,
      hasNextPage: response.pagination?.hasNextPage ?? false,
    };
  }

  async getRequest(requestId: string): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.get<
      ApiResponse<ExternalEstateServiceRequest>
    >(`/estate/external/requests/${encodeURIComponent(requestId)}`);
    return response.data;
  }

  async getMyProperties(): Promise<ExternalPropertyPortfolio> {
    const response = await apiService.get<ApiResponse<ExternalPropertyPortfolio>>(
      '/estate/external/my-properties'
    );
    return response.data;
  }

  async createRequest(
    payload: CreateExternalEstateServiceRequest
  ): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.post<
      ApiResponse<ExternalEstateServiceRequest>
    >('/estate/external/requests', payload);
    return response.data;
  }

  async submitPropertyRequestDecision(
    requestId: string,
    payload: { decision: 'Accept' | 'Reject'; notes?: string | null }
  ): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.post<
      ApiResponse<ExternalEstateServiceRequest>
    >(`/estate/external/requests/${requestId}/customer-decision`, payload);
    return response.data;
  }

  async submitClarificationResponse(
    requestId: string,
    responseText: string
  ): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.post<
      ApiResponse<ExternalEstateServiceRequest>
    >(`/estate/external/requests/${requestId}/clarification-response`, {
      response: responseText,
    });
    return response.data;
  }

  async uploadSignedAgreement(
    requestId: string,
    file: File,
    notes?: string | null
  ): Promise<ExternalEstateServiceRequest> {
    const formData = new FormData();
    formData.append('file', file);
    if (notes) {
      formData.append('notes', notes);
    }
    const response = await rawApiService.request<
      ApiResponse<ExternalEstateServiceRequest>
    >(`/estate/external/requests/${requestId}/signed-agreement`, {
      method: 'POST',
      body: formData,
    });
    return response.data;
  }

  async uploadCustomerIntakeDocument(
    requestId: string,
    documentId: string,
    file: File
  ): Promise<void> {
    const formData = new FormData();
    formData.append('file', file);
    await rawApiService.request(
      `/estate/external/requests/${requestId}/customer-intake-documents/${documentId}/upload`,
      { method: 'POST', body: formData }
    );
  }

  async downloadGeneratedAgreement(requestId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/estate/external/requests/${encodeURIComponent(requestId)}/agreement`
    );
  }

  async downloadPropertyInvoice(invoiceId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/estate/external/invoices/${encodeURIComponent(invoiceId)}/pdf?download=true`
    );
  }

  async downloadLegalTransferDraft(legalCaseId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/estate/external/legal-transfers/${encodeURIComponent(legalCaseId)}/draft`
    );
  }

  async downloadLegalTransferDocument(
    legalCaseId: string,
    documentId: string
  ): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/estate/external/legal-transfers/${encodeURIComponent(legalCaseId)}/documents/${encodeURIComponent(documentId)}/content?download=true`
    );
  }

  async uploadExecutedTransferForm(
    legalCaseId: string,
    file: File,
    notes?: string | null
  ): Promise<ExternalPropertyPortfolio['legalTransfers'][number]> {
    const formData = new FormData();
    formData.append('file', file);
    if (notes) {
      formData.append('notes', notes);
    }

    const response = await rawApiService.request<
      ApiResponse<ExternalPropertyPortfolio['legalTransfers'][number]>
    >(
      `/estate/external/legal-transfers/${encodeURIComponent(legalCaseId)}/executed-transfer-form`,
      { method: 'POST', body: formData }
    );
    return response.data;
  }
}

export const externalEstateServicesService =
  new ExternalEstateServicesService();
