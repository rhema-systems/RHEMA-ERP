/**
 * Tender Service
 * Main API service for Tender management
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

// ============================================================================
// TENDER INTERFACES
// ============================================================================

export interface TenderDto {
  id: string;
  tenderNumber: string;
  title: string;
  tenderType: string; // RFQ, RFP, ITB, EOI
  status: string; // Draft, Published, Closed, Awarded, Cancelled
  publishDate?: string;
  submissionDeadline?: string;
  estimatedValue?: number;
  currency?: string;
  bidCount: number;
  invitationCount: number;
  createdAt: string;
  createdByName?: string;
}

export interface TenderDetailDto extends TenderDto {
  description?: string;
  openingDate?: string;
  awardDate?: string;
  minimumPerformanceRating?: number;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson?: string;
  notes?: string;
  termsAndConditions?: string;
  requiredDocuments?: string; // JSON array of required document types
  requiresAcceptanceDeclaration?: boolean;
  acceptanceDeclarationDocumentPath?: string;
  acceptanceDeclarationDocumentName?: string;
  evaluationTemplateId?: string;
  evaluationTemplateName?: string;
  items: TenderItemDto[];
  documents: TenderDocumentDto[];
  invitations: TenderInvitationDto[];
  bids: TenderBidSummaryDto[];
  fees: TenderFeeDto[];
  evaluators: TenderEvaluatorDto[];
  clarifications: TenderClarificationDto[];
  revisions: TenderRevisionDto[];
  totalViews: number;
  totalDownloads: number;
}

export interface TenderDocumentRequirement {
  documentType: string;
  documentName: string;
  isRequired: boolean;
  description?: string;
  maxFileSizeMB?: number;
  allowedFileTypes?: string;
}

export interface CreateTenderDto {
  title: string;
  description?: string;
  tenderType: string;
  submissionDeadline?: string;
  openingDate?: string;
  estimatedValue?: number;
  currency?: string;
  minimumPerformanceRating?: number;
  requiresPrequalification?: boolean;
  allowPartialBids?: boolean;
  priceWeightage?: number;
  qualityWeightage?: number;
  deliveryWeightage?: number;
  experienceWeightage?: number;
  evaluationCriteriaJson?: string;
  notes?: string;
  termsAndConditions?: string;
  requiredDocuments?: string; // JSON string of TenderDocumentRequirement[]
  requiresAcceptanceDeclaration?: boolean;
  evaluationTemplateId?: string;
  items?: CreateTenderItemDto[];
}

export interface UpdateTenderDto {
  title: string;
  description?: string;
  submissionDeadline?: string;
  openingDate?: string;
  estimatedValue?: number;
  currency?: string;
  minimumPerformanceRating?: number;
  requiresPrequalification?: boolean;
  allowPartialBids?: boolean;
  priceWeightage?: number;
  qualityWeightage?: number;
  deliveryWeightage?: number;
  experienceWeightage?: number;
  evaluationCriteriaJson?: string;
  notes?: string;
  termsAndConditions?: string;
  requiredDocuments?: string; // JSON string of TenderDocumentRequirement[]
  requiresAcceptanceDeclaration?: boolean;
  evaluationTemplateId?: string;
}

export interface PublishTenderDto {
  submissionDeadline: string;
  openingDate?: string;
  invitedBusinessPartnerIds?: string[];
  sendNotifications?: boolean;
}

export interface TenderItemDto {
  id: string;
  tenderId: string;
  lineNumber: number;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  specifications?: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
}

export interface CreateTenderItemDto {
  lineNumber: number;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  specifications?: string;
  requiredDeliveryDate?: string | null;
  deliveryLocation?: string;
}

export interface TenderDocumentDto {
  id: string;
  tenderId: string;
  documentName: string;
  documentType: string;
  filePath: string;
  fileType?: string;
  fileSize?: number;
  uploadedDate: string;
  uploadedByName?: string;
  isPublic: boolean;
}

export interface TenderInvitationDto {
  id: string;
  tenderId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  invitedDate: string;
  invitedByName?: string;
  status: string; // Invited, Viewed, Accepted, Declined
  viewedDate?: string;
  responseDate?: string;
  declineReason?: string;
}

export interface TenderFeeDto {
  id: string;
  tenderId: string;
  feeType: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  isMandatory: boolean;
  dueDate?: string;
  description?: string;
  bankAccountDetails?: string;
  paymentCount: number;
}

export interface TenderEvaluatorDto {
  id: string;
  tenderId: string;
  userId: string;
  userName: string;
  role: string;
  assignedDate: string;
  assignedByName?: string;
  status?: string;
  acceptedDate?: string;
  completedDate?: string;
  weightagePercentage?: number;
  evaluationCount?: number;
}

export interface TenderClarificationDto {
  id: string;
  tenderId: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  question: string;
  questionDate: string;
  questionByName?: string;
  answer?: string;
  answerDate?: string;
  answeredByName?: string;
  status: string; // Pending, Answered, Closed
  isPublic: boolean;
  category?: string;
}

export interface TenderRevisionDto {
  id: string;
  tenderId: string;
  revisionNumber: string;
  revisionDate: string;
  revisedByName?: string;
  revisionType: string;
  description: string;
  newSubmissionDeadline?: string;
  requiresRebid: boolean;
  notificationSent: boolean;
}

export interface TenderBidSummaryDto {
  id: string;
  tenderId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  bidNumber: string;
  submittedDate: string;
  status: string;
  totalBidAmount: number;
  currency?: string;
}

export interface InviteTenderersDto {
  businessPartnerIds: string[];
  sendNotifications?: boolean;
}

export interface CreateTenderFeeDto {
  feeType: string;
  amount: number;
  currency?: string;
  paymentMethod: string;
  isMandatory?: boolean;
  dueDate?: string;
  description?: string;
  bankAccountDetails?: string;
}

export interface EvaluatorAssignmentDto {
  userId: string;
  role: string; // Evaluator, ChairPerson, Secretary, Observer
  weightagePercentage?: number; // 0-100
}

export interface AssignEvaluatorsDto {
  evaluators: EvaluatorAssignmentDto[];
}

export interface CreateClarificationDto {
  question: string;
  isPublic?: boolean;
  category?: string;
}

export interface AnswerClarificationDto {
  answer: string;
  isPublic?: boolean;
}

// Pagination and filtering
export interface TenderListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  tenderType?: string;
  fromDate?: string;
  toDate?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// TENDER SERVICE
// ============================================================================

class TenderService {
  /**
   * Get all tenders with pagination and filtering
   */
  async getTenders(params?: TenderListParams): Promise<PagedResult<TenderDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.tenderType) queryParams.append('tenderType', params.tenderType);
    if (params?.fromDate) queryParams.append('fromDate', params.fromDate);
    if (params?.toDate) queryParams.append('toDate', params.toDate);

    const response = await fetch(`${API_BASE_URL}/procurement/Tenders?${queryParams}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch tenders');
    }

    return response.json();
  }

  /**
   * Get tender by ID
   */
  async getTenderById(id: string): Promise<TenderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch tender');
    }

    return response.json();
  }

  /**
   * Get tenders assigned to current user as evaluator
   */
  async getMyAssignedTenders(): Promise<TenderDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/my-assigned`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch assigned tenders');
    }

    return response.json();
  }

  /**
   * Create a new tender
   */
  async createTender(data: CreateTenderDto): Promise<TenderDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create tender');
    }

    return response.json();
  }

  /**
   * Update a tender
   */
  async updateTender(id: string, data: UpdateTenderDto): Promise<TenderDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update tender');
    }

    return response.json();
  }

  /**
   * Publish a tender
   */
  async publishTender(id: string, data: PublishTenderDto): Promise<TenderDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}/publish`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to publish tender');
    }

    return response.json();
  }

  /**
   * Close a tender
   */
  async closeTender(id: string): Promise<TenderDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}/close`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to close tender');
    }

    return response.json();
  }

  /**
   * Delete a tender
   */
  async deleteTender(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete tender');
    }
  }

  /**
   * Add item to tender
   */
  async addTenderItem(tenderId: string, data: CreateTenderItemDto): Promise<TenderItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/items`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add tender item');
    }

    return response.json();
  }

  /**
   * Update tender item
   */
  async updateTenderItem(tenderId: string, itemId: string, data: CreateTenderItemDto): Promise<TenderItemDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/items/${itemId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update tender item');
    }

    return response.json();
  }

  /**
   * Delete tender item
   */
  async deleteTenderItem(tenderId: string, itemId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/items/${itemId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete tender item');
    }
  }

  /**
   * Upload tender document
   */
  async uploadTenderDocument(
    tenderId: string,
    file: File,
    documentType: string,
    documentName?: string,
    isPublic: boolean = false
  ): Promise<TenderDocumentDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    formData.append('isPublic', isPublic.toString());
    if (documentName) {
      formData.append('documentName', documentName);
    }

    const headers = getAuthHeaders();
    delete headers['Content-Type']; // Let browser set Content-Type with boundary for multipart/form-data

    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/documents`, {
      method: 'POST',
      headers,
      body: formData,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to upload document');
    }

    return response.json();
  }

  /**
   * Delete tender document
   */
  async deleteTenderDocument(tenderId: string, documentId: string): Promise<void> {
    const headers = getAuthHeaders();
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/documents/${documentId}`, {
      method: 'DELETE',
      headers,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete document');
    }
  }

  /**
   * Download tender document
   */
  downloadTenderDocument(tenderId: string, documentId: string, documentName: string): void {
    const headers = getAuthHeaders();
    const url = `${API_BASE_URL}/procurement/Tenders/${tenderId}/documents/${documentId}/download`;

    fetch(url, { headers })
      .then(response => {
        if (!response.ok) {
          throw new Error('Failed to download document');
        }
        return response.blob();
      })
      .then(blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = documentName;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
      })
      .catch(error => {
        console.error('Error downloading document:', error);
        throw error;
      });
  }

  /**
   * Invite tenderers
   */
  async inviteTenderers(tenderId: string, data: InviteTenderersDto): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/invitations`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to invite tenderers');
    }
  }

  /**
   * Add tender fee
   */
  async addTenderFee(tenderId: string, data: CreateTenderFeeDto): Promise<TenderFeeDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/fees`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add tender fee');
    }

    return response.json();
  }

  /**
   * Update tender fee
   */
  async updateTenderFee(tenderId: string, feeId: string, data: CreateTenderFeeDto): Promise<TenderFeeDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/fees/${feeId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update tender fee');
    }

    return response.json();
  }

  /**
   * Delete tender fee
   */
  async deleteTenderFee(tenderId: string, feeId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/fees/${feeId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete tender fee');
    }
  }

  /**
   * Get tender evaluators
   */
  async getTenderEvaluators(tenderId: string): Promise<TenderEvaluatorDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/evaluators`, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to get evaluators');
    }

    return response.json();
  }

  /**
   * Assign evaluators
   */
  async assignEvaluators(tenderId: string, data: AssignEvaluatorsDto): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/evaluators`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to assign evaluators');
    }
  }

  /**
   * Remove evaluator
   */
  async removeEvaluator(tenderId: string, evaluatorId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/evaluators/${evaluatorId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to remove evaluator');
    }
  }

  /**
   * Get tender clarifications
   */
  async getTenderClarifications(tenderId: string, publicOnly: boolean = true): Promise<TenderClarificationDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/clarifications?publicOnly=${publicOnly}`, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to get clarifications');
    }

    return response.json();
  }

  /**
   * Create clarification (ask question)
   */
  async createClarification(tenderId: string, data: CreateClarificationDto): Promise<TenderClarificationDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/clarifications`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create clarification');
    }

    return response.json();
  }

  /**
   * Answer clarification
   */
  async answerClarification(tenderId: string, clarificationId: string, data: AnswerClarificationDto): Promise<TenderClarificationDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/clarifications/${clarificationId}/answer`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to answer clarification');
    }

    return response.json();
  }
}

export const tenderService = new TenderService();

// Export individual functions for convenience
export const getTenders = (params?: TenderListParams) => tenderService.getTenders(params);
export const getTenderById = (id: string) => tenderService.getTenderById(id);
export const getMyAssignedTenders = () => tenderService.getMyAssignedTenders();
export const createTender = (data: CreateTenderDto) => tenderService.createTender(data);
export const updateTender = (id: string, data: UpdateTenderDto) => tenderService.updateTender(id, data);
export const publishTender = (id: string, data: PublishTenderDto) => tenderService.publishTender(id, data);
export const closeTender = (id: string) => tenderService.closeTender(id);
export const deleteTender = (id: string) => tenderService.deleteTender(id);
export const addTenderItem = (tenderId: string, data: CreateTenderItemDto) => tenderService.addTenderItem(tenderId, data);
export const updateTenderItem = (tenderId: string, itemId: string, data: CreateTenderItemDto) => tenderService.updateTenderItem(tenderId, itemId, data);
export const deleteTenderItem = (tenderId: string, itemId: string) => tenderService.deleteTenderItem(tenderId, itemId);
export const uploadTenderDocument = (tenderId: string, file: File, documentType: string, documentName?: string, isPublic?: boolean) => tenderService.uploadTenderDocument(tenderId, file, documentType, documentName, isPublic);
export const deleteTenderDocument = (tenderId: string, documentId: string) => tenderService.deleteTenderDocument(tenderId, documentId);
export const downloadTenderDocument = (tenderId: string, documentId: string, documentName: string) => tenderService.downloadTenderDocument(tenderId, documentId, documentName);
export const inviteTenderers = (tenderId: string, data: InviteTenderersDto) => tenderService.inviteTenderers(tenderId, data);
export const addTenderFee = (tenderId: string, data: CreateTenderFeeDto) => tenderService.addTenderFee(tenderId, data);
export const updateTenderFee = (tenderId: string, feeId: string, data: CreateTenderFeeDto) => tenderService.updateTenderFee(tenderId, feeId, data);
export const deleteTenderFee = (tenderId: string, feeId: string) => tenderService.deleteTenderFee(tenderId, feeId);
export const getTenderEvaluators = (tenderId: string) => tenderService.getTenderEvaluators(tenderId);
export const assignEvaluators = (tenderId: string, data: AssignEvaluatorsDto) => tenderService.assignEvaluators(tenderId, data);
export const removeEvaluator = (tenderId: string, evaluatorId: string) => tenderService.removeEvaluator(tenderId, evaluatorId);
export const getTenderClarifications = (tenderId: string, publicOnly?: boolean) => tenderService.getTenderClarifications(tenderId, publicOnly);
export const createClarification = (tenderId: string, data: CreateClarificationDto) => tenderService.createClarification(tenderId, data);
export const answerClarification = (tenderId: string, clarificationId: string, data: AnswerClarificationDto) => tenderService.answerClarification(tenderId, clarificationId, data);
