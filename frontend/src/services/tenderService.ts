/**
 * Tender Service
 * Main API service for Tender management
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

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
  description?: string;
  tenderType: string; // RFQ, RFP, ITB, EOI
  status: string; // Draft, Published, Closed, Awarded, Cancelled
  publishDate?: string;
  submissionDeadline?: string;
  closingDate?: string;
  estimatedValue?: number;
  currency?: string;
  bidCount: number;
  invitationCount: number;
  createdAt: string;
  createdByName?: string;
  currentWorkflowStepName?: string;
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
  // QCBS Evaluation fields
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  lots: TenderLotDto[];
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
  lotCount: number;
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
  // QCBS Evaluation fields
  useQCBSEvaluation?: boolean;
  technicalWeight?: number;
  financialWeight?: number;
  minimumTechnicalScore?: number;
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
  // QCBS Evaluation fields
  useQCBSEvaluation?: boolean;
  technicalWeight?: number;
  financialWeight?: number;
  minimumTechnicalScore?: number;
}

export interface PublishTenderDto {
  submissionDeadline: string;
  openingDate?: string;
  invitedBusinessPartnerIds?: string[];
  externalRecipientEmails?: string[];
  sendNotifications?: boolean;
}

// ============================================================================
// TENDER LOT INTERFACES
// ============================================================================

export interface TenderLotDto {
  id: string;
  tenderId: string;
  lotNumber: number;
  lotCode: string;
  title: string;
  description?: string;
  estimatedValue?: number;
  currency?: string;
  status: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
  specifications?: string;
  notes?: string;
  displayOrder: number;
  itemCount: number;
  bidCount: number;
  isAwarded: boolean;
  awardedToPartnerName?: string;
  items: TenderItemDto[];
  createdAt?: string;
}

export interface CreateTenderLotDto {
  lotNumber?: number;
  lotCode: string;
  title: string;
  description?: string;
  estimatedValue?: number;
  currency?: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
  specifications?: string;
  notes?: string;
  displayOrder?: number;
}

export interface UpdateTenderLotDto {
  lotCode: string;
  title: string;
  description?: string;
  estimatedValue?: number;
  currency?: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
  specifications?: string;
  notes?: string;
  displayOrder?: number;
}

export interface TenderItemDto {
  id: string;
  tenderId: string;
  lotId?: string;
  lotCode?: string;
  lotTitle?: string;
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
  lotId?: string;
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
  uploadedAt?: string;
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
  notificationSent?: boolean;
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
  // QCBS Evaluation fields
  technicalScore?: number;
  financialScore?: number;
  combinedScore?: number;
  isQualifiedTechnically?: boolean;
  disqualificationReason?: string;
}

export interface InviteTenderersDto {
  businessPartnerIds: string[];
  externalRecipientEmails?: string[];
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
// QCBS EVALUATION INTERFACES
// ============================================================================

export interface ConfigureQCBSDto {
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
}

export interface QCBSBidScoreDto {
  bidId: string;
  bidNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  totalBidAmount: number;
  currency: string;
  technicalScore: number;
  financialScore: number;
  combinedScore: number;
  isQualifiedTechnically: boolean;
  disqualificationReason?: string;
  rank: number;
  isRecommendedForAward: boolean;
}

export interface QCBSEvaluationResultDto {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  evaluationDate: string;
  calculatedByName: string;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  lowestBidAmount: number;
  totalBidsEvaluated: number;
  qualifiedBidsCount: number;
  disqualifiedBidsCount: number;
  bidScores: QCBSBidScoreDto[];
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
   * Submit a tender for approval (unified workflow)
   */
  async submitTenderForApproval(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to submit tender for approval');
    }
  }

  /**
   * Approve a tender (unified workflow)
   */
  async approveTender(id: string, notes?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ notes: notes || undefined }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to approve tender');
    }
  }

  /**
   * Reject a tender (unified workflow)
   */
  async rejectTender(id: string, reason: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${id}/reject`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to reject tender');
    }
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

  // ============================================================================
  // TENDER LOT METHODS
  // ============================================================================

  /**
   * Get all LOTs for a tender
   */
  async getTenderLots(tenderId: string): Promise<TenderLotDto[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/lots`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch tender lots');
    }

    return response.json();
  }

  /**
   * Get a specific LOT by ID
   */
  async getTenderLot(lotId: string): Promise<TenderLotDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/lots/${lotId}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch tender lot');
    }

    return response.json();
  }

  /**
   * Add LOT to tender
   */
  async addTenderLot(tenderId: string, data: CreateTenderLotDto): Promise<TenderLotDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/${tenderId}/lots`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to add tender lot');
    }

    return response.json();
  }

  /**
   * Update tender LOT
   */
  async updateTenderLot(lotId: string, data: UpdateTenderLotDto): Promise<TenderLotDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/lots/${lotId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update tender lot');
    }

    return response.json();
  }

  /**
   * Delete tender LOT
   */
  async deleteTenderLot(lotId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/lots/${lotId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete tender lot');
    }
  }

  /**
   * Assign an item to a LOT
   */
  async assignItemToLot(itemId: string, lotId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/items/${itemId}/assign-lot/${lotId}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to assign item to lot');
    }
  }

  /**
   * Remove an item from its LOT
   */
  async removeItemFromLot(itemId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/Tenders/items/${itemId}/remove-from-lot`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to remove item from lot');
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

    const { ['Content-Type']: _contentType, ...headers } = getAuthHeaders() as Record<string, string>;

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

  // ============================================================================
  // QCBS EVALUATION METHODS
  // ============================================================================

  /**
   * Configure QCBS evaluation settings for a tender
   */
  async configureQCBS(tenderId: string, data: ConfigureQCBSDto): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluation/${tenderId}/configure-qcbs`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to configure QCBS evaluation');
    }
  }

  /**
   * Run QCBS evaluation for a tender
   */
  async evaluateQCBS(tenderId: string): Promise<QCBSEvaluationResultDto> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/qcbs/${tenderId}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to run QCBS evaluation');
    }

    return response.json();
  }

  /**
   * Get QCBS evaluation results for a tender
   */
  async getQCBSEvaluationResults(tenderId: string): Promise<QCBSEvaluationResultDto | null> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/qcbs/${tenderId}`, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    if (response.status === 404) {
      return null; // QCBS evaluation has not been run yet
    }

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to get QCBS evaluation results');
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
export const submitTenderForApproval = (id: string) => tenderService.submitTenderForApproval(id);
export const approveTender = (id: string, notes?: string) => tenderService.approveTender(id, notes);
export const rejectTender = (id: string, reason: string) => tenderService.rejectTender(id, reason);
export const publishTender = (id: string, data: PublishTenderDto) => tenderService.publishTender(id, data);
export const closeTender = (id: string) => tenderService.closeTender(id);
export const deleteTender = (id: string) => tenderService.deleteTender(id);
export const addTenderItem = (tenderId: string, data: CreateTenderItemDto) => tenderService.addTenderItem(tenderId, data);
export const updateTenderItem = (tenderId: string, itemId: string, data: CreateTenderItemDto) => tenderService.updateTenderItem(tenderId, itemId, data);
export const deleteTenderItem = (tenderId: string, itemId: string) => tenderService.deleteTenderItem(tenderId, itemId);
// LOT functions
export const getTenderLots = (tenderId: string) => tenderService.getTenderLots(tenderId);
export const getTenderLot = (lotId: string) => tenderService.getTenderLot(lotId);
export const addTenderLot = (tenderId: string, data: CreateTenderLotDto) => tenderService.addTenderLot(tenderId, data);
export const updateTenderLot = (lotId: string, data: UpdateTenderLotDto) => tenderService.updateTenderLot(lotId, data);
export const deleteTenderLot = (lotId: string) => tenderService.deleteTenderLot(lotId);
export const assignItemToLot = (itemId: string, lotId: string) => tenderService.assignItemToLot(itemId, lotId);
export const removeItemFromLot = (itemId: string) => tenderService.removeItemFromLot(itemId);
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
// QCBS Evaluation functions
export const configureQCBS = (tenderId: string, data: ConfigureQCBSDto) => tenderService.configureQCBS(tenderId, data);
export const evaluateQCBS = (tenderId: string) => tenderService.evaluateQCBS(tenderId);
export const getQCBSEvaluationResults = (tenderId: string) => tenderService.getQCBSEvaluationResults(tenderId);
