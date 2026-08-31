// Tender Bid Service - API calls for tender bid management

import type { TenderBidInitiationStatus } from '@/lib/tender-bid-initiation';

// ==================== INTERFACES ====================

export interface TenderBidSummaryDto {
  id: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  bidNumber: string;
  submittedDate?: string;
  status: string;
  totalBidAmount?: number;
  currency?: string;
  hasPaidFees: boolean;
  paymentStatus?: string;
}

export interface TenderBidDetailDto {
  id: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  bidNumber: string;
  submittedDate: string;
  status: string;
  totalBidAmount: number;
  currency?: string;
  deliveryDays?: number;
  paymentTerms?: string;
  warrantyTerms?: string;
  technicalProposal?: string;
  commercialProposal?: string;
  associationType?: 'AllUsers' | 'Self' | 'SelectedUsers';
  acceptedDeclaration?: boolean;
  declarationAcceptedAt?: string;
  isCompliant: boolean;
  nonComplianceReasons?: string;

  // Evaluation Template
  evaluationTemplateId?: string;
  evaluationTemplateName?: string;

  // Evaluation Scores
  priceScore?: number;
  qualityScore?: number;
  deliveryScore?: number;
  experienceScore?: number;
  totalScore?: number;
  rank?: number;

  // Metadata
  openedDate?: string;
  openedByName?: string;
  evaluatedByName?: string;
  evaluatedDate?: string;
  evaluationNotes?: string;
  rejectionReason?: string;
  createdAt: string;
  updatedAt: string;

  // Related Data
  items?: TenderBidItemDto[];
  documents?: TenderBidDocumentDto[];
  evaluations?: TenderEvaluationDto[];
  interviews?: TenderInterviewDto[];
}

export interface TenderBidItemDto {
  id: string;
  tenderBidId: string;
  bidLotId?: string;
  lotCode?: string;
  tenderItemId: string;
  tenderItemDescription: string;
  requestedQuantity: number;
  offeredQuantity: number;
  unitOfMeasure?: string;
  unitPrice: number;
  totalPrice: number;
  deliveryDays?: number;
  specifications?: string;
  brand?: string;
  model?: string;
  technicalDetails?: string;
}

export interface TenderBidDocumentDto {
  id: string;
  tenderBidId: string;
  documentType: string;
  documentName: string;
  fileName?: string;
  filePath: string;
  fileType?: string;
  fileSize?: number;
  uploadedDate: string;
  uploadedAt?: string;
  uploadedByName?: string;
}

export interface TenderEvaluationDto {
  id: string;
  tenderBidId: string;
  tenderEvaluatorId: string;
  evaluatorName: string;
  evaluationDate: string;
  status: string;
  priceScore?: number;
  qualityScore?: number;
  deliveryScore?: number;
  experienceScore?: number;
  technicalScore?: number;
  complianceScore?: number;
  totalScore?: number;
  evaluationCriteriaJson?: string;
  technicalComments?: string;
  commercialComments?: string;
  overallComments?: string;
  isRecommended?: boolean;
  recommendation?: string;
}

export interface TenderInterviewDto {
  id: string;
  tenderBidId: string;
  interviewDate: string;
  location?: string;
  interviewerNames?: string;
  status: string;
  notes?: string;
  outcome?: string;
}

export interface TenderPaymentDto {
  id: string;
  tenderFeeId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  paymentReference: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  status: string;
  paymentDate: string;
  verifiedDate?: string;
  verifiedByName?: string;
  transactionId?: string;
  paymentProof?: string;
}

export interface VerifyTenderPaymentDto {
  isApproved: boolean;
  notes?: string;
}

export interface CreateTenderBidDto {
  tenderId: string;
  associationType?: 'AllUsers' | 'Self' | 'SelectedUsers';
  deliveryDays?: number;
  paymentTerms?: string;
  warrantyTerms?: string;
  technicalProposal?: string;
  commercialProposal?: string;
  acceptedDeclaration?: boolean;
  selectedLotIds: string[];
  items: CreateTenderBidItemDto[];
}

export interface UpdateTenderBidDto {
  deliveryDays?: number;
  paymentTerms?: string;
  warrantyTerms?: string;
  technicalProposal?: string;
  commercialProposal?: string;
  associationType?: string;
  acceptedDeclaration?: boolean;
  selectedLotIds?: string[];
  items?: UpdateTenderBidItemDto[];
}

export interface UpdateTenderBidItemDto {
  tenderItemId: string;
  offeredQuantity: number;
  unitPrice: number;
  deliveryDays?: number;
  specifications?: string;
  brand?: string;
  model?: string;
  technicalDetails?: string;
}

export interface CreateTenderBidItemDto {
  tenderItemId: string;
  offeredQuantity: number;
  unitPrice: number;
  deliveryDays?: number;
  specifications?: string;
  brand?: string;
  model?: string;
  technicalDetails?: string;
}

export interface SubmitTenderBidDto {
  confirmSubmission: boolean;
}

export interface WithdrawTenderBidDto {
  reason: string;
}

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(): HeadersInit {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// Get all bids with pagination and filtering
export async function getBids(params: {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  tenderId?: string;
}): Promise<{
  items: TenderBidSummaryDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}> {
  const queryParams = new URLSearchParams();
  if (params.page) queryParams.append('page', params.page.toString());
  if (params.pageSize)
    queryParams.append('pageSize', params.pageSize.toString());
  if (params.search) queryParams.append('search', params.search);
  if (params.status) queryParams.append('status', params.status);
  if (params.tenderId) queryParams.append('tenderId', params.tenderId);

  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids?${queryParams}`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    console.error('getBids API error:', response.status, errorText);
    throw new Error(`Failed to fetch bids: ${response.status} - ${errorText}`);
  }

  return response.json();
}

// Get bid by ID
export async function getBidById(id: string): Promise<TenderBidDetailDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderBids/${id}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch bid details');
  }

  return response.json();
}

// Get my draft bid for a tender
export async function getMyDraftBidByTenderId(
  tenderId: string
): Promise<TenderBidDetailDto | null> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/my-draft-bid/${tenderId}`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Failed to fetch draft bid');
  }

  return response.json();
}

// Get bids by tender ID
export async function getBidsByTenderId(
  tenderId: string
): Promise<TenderBidSummaryDto[]> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/by-tender/${tenderId}`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to fetch tender bids');
  }

  return response.json();
}

// Get my bids (for business partners)
export async function getMyBids(): Promise<TenderBidSummaryDto[]> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/my-bids`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    console.error('getMyBids API error:', response.status, errorText);
    throw new Error(
      `Failed to fetch my bids: ${response.status} - ${errorText}`
    );
  }

  return response.json();
}

// Create a new bid
export async function createBid(
  data: CreateTenderBidDto
): Promise<TenderBidDetailDto> {
  console.log('Creating bid with data:', JSON.stringify(data, null, 2));

  const response = await fetch(`${API_BASE_URL}/procurement/TenderBids`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const errorText = await response.text();
    console.error('Create bid failed:', errorText);
    throw new Error(errorText || 'Failed to create bid');
  }

  return response.json();
}

// Update bid
export async function updateBid(
  id: string,
  data: UpdateTenderBidDto
): Promise<TenderBidDetailDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderBids/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to update bid');
  }

  return response.json();
}

// Submit bid
export async function submitBid(
  id: string,
  data: SubmitTenderBidDto
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${id}/submit`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to submit bid');
  }
}

// Withdraw bid
export async function withdrawBid(
  id: string,
  data: WithdrawTenderBidDto
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${id}/withdraw`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to withdraw bid');
  }
}

// Open bid (mark as opened)
export async function openBid(id: string): Promise<TenderBidDetailDto> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${id}/open`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to open bid');
  }

  return response.json();
}

// Delete bid
export async function deleteBid(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderBids/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete bid');
  }
}

// Add bid item
export async function addBidItem(
  bidId: string,
  data: CreateTenderBidItemDto
): Promise<TenderBidItemDto> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/items`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to add bid item');
  }

  return response.json();
}

// Update bid item
export async function updateBidItem(
  bidId: string,
  itemId: string,
  data: CreateTenderBidItemDto
): Promise<TenderBidItemDto> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/items/${itemId}`,
    {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to update bid item');
  }

  return response.json();
}

// Delete bid item
export async function deleteBidItem(
  bidId: string,
  itemId: string
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/items/${itemId}`,
    {
      method: 'DELETE',
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to delete bid item');
  }
}

// Get bid documents
export async function getBidDocuments(
  bidId: string
): Promise<TenderBidDocumentDto[]> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/documents`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to fetch bid documents');
  }

  return response.json();
}

// Upload bid document
export async function uploadBidDocument(
  bidId: string,
  file: File,
  documentType: string,
  documentName?: string
): Promise<TenderBidDocumentDto> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('documentType', documentType);
  if (documentName) {
    formData.append('documentName', documentName);
  }

  const { ['Content-Type']: _contentType, ...headers } =
    getAuthHeaders() as Record<string, string>;

  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/documents`,
    {
      method: 'POST',
      headers,
      body: formData,
    }
  );

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || 'Failed to upload document');
  }

  return response.json();
}

// Download bid document
export function downloadBidDocument(
  bidId: string,
  documentId: string,
  documentName: string
): void {
  const headers = getAuthHeaders();
  const url = `${API_BASE_URL}/procurement/TenderBids/${bidId}/documents/${documentId}/download`;

  fetch(url, { headers })
    .then((response) => {
      if (!response.ok) {
        throw new Error('Failed to download document');
      }
      return response.blob();
    })
    .then((blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = documentName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    })
    .catch((error) => {
      console.error('Error downloading document:', error);
      throw error;
    });
}

// Get bid payments
export async function getBidPayments(
  bidId: string
): Promise<TenderPaymentDto[]> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/payments`,
    {
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to fetch bid payments');
  }

  return response.json();
}

export async function getInitiationStatus(
  tenderId: string
): Promise<TenderBidInitiationStatus> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/initiation-status/${tenderId}`,
    { headers: getAuthHeaders() }
  );
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail || 'Failed to load bid initiation status');
  }
  return response.json();
}

export async function recordBidPayment(
  bidId: string,
  input: {
    tenderFeeId: string;
    amount: number;
    currency: string;
    paymentMethod: string;
    transactionId: string;
    notes?: string;
  }
): Promise<TenderPaymentDto> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/payments`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(input),
    }
  );
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail || 'Failed to record tender fee payment');
  }
  return response.json();
}

async function readTenderPaymentError(
  response: Response,
  fallback: string
): Promise<string> {
  const body = await response.text();
  if (!body) return fallback;

  try {
    const problem = JSON.parse(body) as
      | string
      | {
          detail?: string;
          message?: string;
          code?: string;
          extensions?: { code?: string };
        };

    if (typeof problem === 'string') return problem || fallback;

    const detail = problem.detail || problem.message || fallback;
    const code = problem.code || problem.extensions?.code;
    return code && !detail.includes(code) ? `${detail} (${code})` : detail;
  } catch {
    return body;
  }
}

export async function verifyBidPayment(
  bidId: string,
  paymentId: string,
  input: VerifyTenderPaymentDto
): Promise<TenderPaymentDto> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/payments/${paymentId}/verify`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(input),
    }
  );

  if (!response.ok) {
    throw new Error(
      await readTenderPaymentError(
        response,
        'Failed to update the tender fee payment'
      )
    );
  }

  return response.json();
}

// Delete bid document
export async function deleteBidDocument(
  bidId: string,
  documentId: string
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/${bidId}/documents/${documentId}`,
    {
      method: 'DELETE',
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to delete bid document');
  }
}

// Open all bids for a tender
export async function openAllBidsByTender(
  tenderId: string
): Promise<{ openedCount: number; message: string }> {
  const response = await fetch(
    `${API_BASE_URL}/procurement/TenderBids/tender/${tenderId}/open-all`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to open bids');
  }

  return response.json();
}
