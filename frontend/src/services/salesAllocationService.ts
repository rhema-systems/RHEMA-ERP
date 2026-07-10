import { apiService } from './api.service';

export interface SalesAllocationHistoryDto {
  id: string;
  salesAllocationId: string;
  action: string;
  fromStatus?: string;
  toStatus: string;
  performedById?: string;
  performedByName?: string;
  performedAt: string;
  notes?: string;
}

export interface SalesAllocationDto {
  id: string;
  tenantId: string;
  saleableSourceId: string;
  sourceCode: string;
  sourceType: string;
  adapterKey: string;
  sourceItemId: string;
  sourceItemCode?: string;
  sourceItemName: string;
  sourceItemType?: string;
  businessPartnerId?: string;
  customerName?: string;
  leadId?: string;
  opportunityId?: string;
  salesOrderId?: string;
  salesOrderNumber?: string;
  salesAgreementId?: string;
  salesAgreementTitle?: string;
  allocationType: string;
  status: string;
  reservedUntil?: string;
  effectiveDate?: string;
  releasedDate?: string;
  estimatedValue?: number;
  agreedValue?: number;
  currency?: string;
  notes?: string;
  releaseReason?: string;
  createdAt: string;
  updatedAt?: string;
  history: SalesAllocationHistoryDto[];
}

export interface CreateSalesAllocationDto {
  saleableSourceId: string;
  sourceItemId: string;
  sourceItemCode?: string;
  sourceItemName: string;
  sourceItemType?: string;
  businessPartnerId?: string;
  customerName?: string;
  leadId?: string;
  opportunityId?: string;
  salesOrderId?: string;
  salesAgreementId?: string;
  allocationType?: string;
  status?: string;
  reservedUntil?: string;
  effectiveDate?: string;
  estimatedValue?: number;
  agreedValue?: number;
  currency?: string;
  notes?: string;
}

export interface UpdateSalesAllocationStatusDto {
  status: string;
  salesOrderId?: string;
  salesAgreementId?: string;
  agreedValue?: number;
  reservedUntil?: string;
  notes?: string;
  releaseReason?: string;
}

export interface SalesAllocationApprovalDto {
  isApproved: boolean;
  comments?: string;
  rejectionReason?: string;
}

export interface TransferSalesAllocationDto {
  businessPartnerId?: string;
  customerName?: string;
  leadId?: string;
  opportunityId?: string;
  clearLinkedDocuments?: boolean;
  notes?: string;
}

const endpoint = '/sales/allocations';

export const salesAllocationService = {
  getAllocations(params: {
    saleableSourceId?: string;
    sourceItemId?: string;
    status?: string;
    businessPartnerId?: string;
    salesOrderId?: string;
    salesAgreementId?: string;
    activeOnly?: boolean;
  } = {}) {
    return apiService.get<SalesAllocationDto[]>(endpoint, params);
  },

  getAllocationById(id: string) {
    return apiService.get<SalesAllocationDto>(`${endpoint}/${id}`);
  },

  hasActiveAllocation(saleableSourceId: string, sourceItemId: string) {
    return apiService.get<{ hasActiveAllocation: boolean }>(`${endpoint}/active-check`, {
      saleableSourceId,
      sourceItemId,
    });
  },

  createAllocation(dto: CreateSalesAllocationDto) {
    return apiService.post<SalesAllocationDto>(endpoint, dto);
  },

  updateAllocationStatus(id: string, dto: UpdateSalesAllocationStatusDto) {
    return apiService.put<SalesAllocationDto>(`${endpoint}/${id}/status`, dto);
  },

  submitForApproval(id: string) {
    return apiService.post<SalesAllocationDto>(`${endpoint}/${id}/submit`);
  },

  processApproval(id: string, dto: SalesAllocationApprovalDto) {
    return apiService.post<SalesAllocationDto>(`${endpoint}/${id}/approve`, dto);
  },

  transferAllocation(id: string, dto: TransferSalesAllocationDto) {
    return apiService.post<SalesAllocationDto>(`${endpoint}/${id}/transfer`, dto);
  },
};
