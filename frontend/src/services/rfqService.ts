/**
 * RFQ Service
 * Request For Quotation (RFQ) is intentionally separate from Tender.
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

const getAuthHeaders = () => {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
};

const readApiError = async (response: Response): Promise<string> => {
  const contentType = response.headers.get('content-type') || '';

  if (contentType.includes('application/json')) {
    try {
      const data: any = await response.json();
      return (
        data?.detail || data?.title || data?.message || JSON.stringify(data)
      );
    } catch {
      // fall through
    }
  }

  try {
    const text = await response.text();
    return text || response.statusText || 'Request failed';
  } catch {
    return response.statusText || 'Request failed';
  }
};

export interface RfqDto {
  id: string;
  rfqNumber: string;
  title: string;
  status: string; // Draft, Sent, Closed, Awarded, Cancelled
  submissionDeadline?: string;
  currency: string;
  estimatedValue: number;
  sourcePurchaseRequisitionId?: string;
  sourcingReleaseId?: string;
  sourcingCaseId?: string;
  createdAt: string;
  sentAt?: string;
  supplierCount: number;
  quoteCount: number;
}

export interface RfqItemDto {
  id: string;
  lineNumber: number;
  inventoryItemId?: string;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  specifications?: string;
  requiredDeliveryDate?: string;
}

export interface RfqInvitationDto {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  primaryEmail?: string;
  status: string; // Selected, Invited, Responded, Declined
  invitedAt: string;
}

export interface RfqQuoteItemDto {
  rfqItemId: string;
  lineNumber: number;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  lineTotal: number;
  isAwarded?: boolean;
}

export interface RfqQuoteDto {
  id: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  status: string;
  submittedAt?: string;
  revisionNumber: number;
  notes?: string;
  totalAmount: number;
  items: RfqQuoteItemDto[];
  history: RfqQuoteHistoryEntryDto[];
}

export interface RfqQuoteHistoryEntryDto {
  id: string;
  revisionNumber: number;
  action: string;
  status: string;
  performedBy: string;
  timestamp: string;
  totalAmount: number;
  description?: string;
}

export interface RfqDetailDto extends RfqDto {
  description?: string;
  externalRecipientEmails?: string;
  quoteDetailsVisible: boolean;
  items: RfqItemDto[];
  suppliers: RfqInvitationDto[];
  quotes: RfqQuoteDto[];
}

export interface CreatePurchaseOrdersFromRfqDto {
  mode: 'WinnerTakesAll' | 'SplitAward';
  quoteId?: string;
  lines?: { rfqItemId: string; quoteId: string; awardReason?: string }[];
}

export interface CreatePurchaseOrdersFromRfqResponseDto {
  purchaseOrders: Array<{
    purchaseOrderId: string;
    orderNumber: string;
    businessPartnerId: string;
    businessPartnerName: string;
    totalAmount: number;
  }>;
}

export interface UpdateRfqDto {
  title?: string;
  description?: string;
  submissionDeadline?: string;
  currency?: string;
  estimatedValue?: number;
  supplierIds?: string[];
  externalRecipientEmails?: string;
}

export interface SendRfqDto {
  supplierIds: string[];
  externalRecipientEmails?: string;
}

export type RfqReceiptDisposition = 'OnTimeAccepted' | 'LateRejected';
export type RfqEvaluationStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Rejected';

export interface ProcurementRfqReceiptDto {
  id: string;
  quoteId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  receiptNumber: string;
  submissionDeadlineUtc: string;
  receivedAtUtc: string;
  disposition: RfqReceiptDisposition;
  sealedAtUtc: string;
  openedAtUtc?: string;
  integrityHash: string;
}

export interface ProcurementRfqOpeningParticipantDto {
  id: string;
  participantUserId?: string;
  participantName: string;
  roleName: string;
  isObserver: boolean;
  signedAtUtc: string;
  signatureReference: string;
}

export interface ProcurementRfqOpeningEntryDto {
  id: string;
  receiptId: string;
  quoteId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  receiptNumber: string;
  receivedAtUtc: string;
  disposition: RfqReceiptDisposition;
  declaredAmount: number;
  securityReference?: string;
  rejectionReason?: string;
  quoteIntegrityHash: string;
}

export interface ProcurementRfqOpeningRegisterDto {
  id: string;
  openedAtUtc: string;
  closedAtUtc: string;
  openedByUserId: string;
  openedByName: string;
  evidenceReference: string;
  integrityHash: string;
  participants: ProcurementRfqOpeningParticipantDto[];
  entries: ProcurementRfqOpeningEntryDto[];
}

export interface ProcurementRfqEvaluationLineDto {
  id: string;
  rfqItemId: string;
  rfqLineNumber: number;
  itemDescription: string;
  quoteId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  unitPrice: number;
  lineTotal: number;
  technicalScore: number;
  commercialScore: number;
  totalScore: number;
  recommendationReason?: string;
}

export interface ProcurementRfqEvaluationDto {
  id: string;
  status: RfqEvaluationStatus;
  awardMode: 'WinnerTakesAll' | 'SplitAward';
  recommendationReason: string;
  evidenceReference: string;
  methodRuleId: string;
  methodRuleCode: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  approvalReference?: string;
  submittedAtUtc?: string;
  submittedByName?: string;
  approvedAtUtc?: string;
  approvedByName?: string;
  integrityHash: string;
  rowVersion: string;
  lines: ProcurementRfqEvaluationLineDto[];
}

export interface ProcurementRfqEvaluationOptionDto {
  rfqItemId: string;
  rfqLineNumber: number;
  itemDescription: string;
  quoteId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  unitPrice: number;
  lineTotal: number;
}

export interface ProcurementRfqControlDto {
  rfqId: string;
  rfqNumber: string;
  rfqStatus: string;
  methodRuleId: string;
  methodRuleCode: string;
  minimumQuotationCount: number;
  workflowDefinitionId?: string;
  qualifiedInvitationCount: number;
  onTimeReceiptCount: number;
  lateReceiptCount: number;
  submissionDeadlinePassed: boolean;
  quotesRemainSealed: boolean;
  minimumCompetitionMet: boolean;
  receipts: ProcurementRfqReceiptDto[];
  evaluationOptions: ProcurementRfqEvaluationOptionDto[];
  openingRegister?: ProcurementRfqOpeningRegisterDto;
  evaluation?: ProcurementRfqEvaluationDto;
}

export interface CompleteProcurementRfqOpeningRequest {
  evidenceReference: string;
  participants: Array<{
    participantUserId?: string;
    participantName: string;
    roleName: string;
    isObserver: boolean;
    signatureReference: string;
  }>;
  securities: Array<{ receiptId: string; securityReference: string }>;
}

export interface SaveProcurementRfqEvaluationRequest {
  awardMode: 'WinnerTakesAll' | 'SplitAward';
  recommendationReason: string;
  evidenceReference: string;
  rowVersion?: string;
  lines: Array<{
    rfqItemId: string;
    quoteId: string;
    technicalScore: number;
    commercialScore: number;
    totalScore: number;
    recommendationReason?: string;
  }>;
}

export const rfqService = {
  async getRfqs(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
  }) {
    const query = new URLSearchParams();
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());
    if (params?.search) query.append('search', params.search);
    if (params?.status) query.append('status', params.status);

    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs?${query.toString()}`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getRfqById(id: string): Promise<RfqDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getRfqPdf(id: string): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}/pdf`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.blob();
  },

  async updateRfq(id: string, dto: UpdateRfqDto): Promise<RfqDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },

  async sendRfq(id: string, dto: SendRfqDto): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/send`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
  },

  // Supplier portal endpoints
  async getMyRfqs(): Promise<RfqDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/rfqs/my-rfqs`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async getMyRfqDetail(id: string): Promise<RfqDetailDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/my-view`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async submitQuote(
    id: string,
    dto: { notes?: string; items: { rfqItemId: string; unitPrice: number }[] }
  ): Promise<RfqQuoteDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/quote`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },

  async awardAndCreatePurchaseOrders(
    id: string,
    dto: CreatePurchaseOrdersFromRfqDto
  ): Promise<CreatePurchaseOrdersFromRfqResponseDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/award`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) {
      throw new Error(await readApiError(response));
    }
    return response.json();
  },

  async getControls(id: string): Promise<ProcurementRfqControlDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/controls`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async completeOpening(
    id: string,
    dto: CompleteProcurementRfqOpeningRequest
  ): Promise<ProcurementRfqOpeningRegisterDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/opening-register`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async saveEvaluation(
    id: string,
    dto: SaveProcurementRfqEvaluationRequest
  ): Promise<ProcurementRfqEvaluationDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/evaluation`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async submitEvaluation(
    id: string,
    rowVersion: string
  ): Promise<ProcurementRfqEvaluationDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/evaluation/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ rowVersion }),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },

  async decideEvaluation(
    id: string,
    dto: {
      action: 'Approve' | 'Reject';
      comments?: string;
      approvalReference: string;
      rowVersion: string;
    }
  ): Promise<ProcurementRfqEvaluationDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/rfqs/${id}/evaluation/decision`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(dto),
      }
    );
    if (!response.ok) throw new Error(await readApiError(response));
    return response.json();
  },
};
