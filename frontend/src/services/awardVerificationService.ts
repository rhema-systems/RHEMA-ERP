const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Checklist Template Types
export interface AwardVerificationChecklistItem {
  id: string;
  templateId: string;
  itemText: string;
  description?: string;
  displayOrder: number;
  isRequired: boolean;
  category?: string;
  isActive: boolean;
}

export interface AwardVerificationChecklistTemplate {
  id: string;
  name: string;
  description?: string;
  category?: string;
  minContractValue?: number;
  maxContractValue?: number;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  createdAt: string;
  items: AwardVerificationChecklistItem[];
}

export interface CreateChecklistItemDto {
  itemText: string;
  description?: string;
  displayOrder: number;
  isRequired: boolean;
  category?: string;
  isActive: boolean;
}

export interface CreateChecklistTemplateDto {
  name: string;
  description?: string;
  category?: string;
  minContractValue?: number;
  maxContractValue?: number;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  items: CreateChecklistItemDto[];
}

export interface UpdateChecklistTemplateDto {
  name: string;
  description?: string;
  category?: string;
  minContractValue?: number;
  maxContractValue?: number;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  items: CreateChecklistItemDto[];
}

// Verification Types
export interface TenderAwardVerificationBidder {
  id: string;
  verificationId: string;
  tenderBidId: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  status: string;
  verifiedDate?: string;
  verifiedById?: string;
  overallComments?: string;
  itemResults: TenderAwardVerificationItemResult[];
}

export interface TenderAwardVerificationItemDocument {
  id: string;
  itemResultId: string;
  fileName: string;
  filePath: string;
  fileSize?: number;
  contentType?: string;
  documentType?: string;
  description?: string;
  uploadedDate: string;
  uploadedByName?: string;
  fileUploadRecordId?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
}

export interface TenderAwardVerificationItemResult {
  id: string;
  bidderId: string;
  checklistItemId: string;
  checklistItemText?: string;
  itemText?: string;
  itemDescription?: string;
  itemCategory?: string;
  isRequired?: boolean;
  isVerified: boolean;
  status: string;
  comments?: string;
  verifiedDate?: string;
  verifiedById?: string;
  verifiedByName?: string;
  documents?: TenderAwardVerificationItemDocument[];
}

export interface TenderAwardVerification {
  id: string;
  tenderId: string;
  tenderNumber?: string;
  templateId?: string;
  status: string;
  startedDate?: string;
  completedDate?: string;
  startedById?: string;
  completedById?: string;
  notes?: string;
  bidders: TenderAwardVerificationBidder[];
}

export interface StartVerificationDto {
  tenderId: string;
  templateId?: string;
  selectedBidIds: string[];
}

export interface VerifyChecklistItemDto {
  bidderId: string;
  checklistItemId: string;
  status: string; // Passed, Failed, NotApplicable
  comments?: string;
}

export interface VerifyItemDto {
  checklistItemId: string;
  isVerified: boolean;
  status: string; // Passed, Failed, NotApplicable
  comments?: string;
}

export interface VerifyBidderDto {
  bidderId: string;
  overallComments?: string;
  itemResults: VerifyItemDto[];
}

export interface CompleteBidderVerificationDto {
  bidderId: string;
  overallComments?: string;
}

export interface CompleteVerificationDto {
  notes?: string;
}

export interface UploadVerificationDocumentDto {
  documentType: string;
  description?: string;
}

const getAuthHeaders = () => {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { Authorization: `Bearer ${token}` }),
  };
};

export const awardVerificationService = {
  // Checklist Template Methods
  async getTemplates(
    includeInactive = false
  ): Promise<AwardVerificationChecklistTemplate[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates?includeInactive=${includeInactive}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error('Failed to fetch checklist templates');
    return response.json();
  },

  async getTemplateById(
    id: string
  ): Promise<AwardVerificationChecklistTemplate> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates/${id}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error('Failed to fetch checklist template');
    return response.json();
  },

  async getDefaultTemplate(): Promise<AwardVerificationChecklistTemplate | null> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates/default`,
      { headers: getAuthHeaders() }
    );
    if (response.status === 404) return null;
    if (!response.ok) throw new Error('Failed to fetch default template');
    return response.json();
  },

  async createTemplate(
    data: CreateChecklistTemplateDto
  ): Promise<AwardVerificationChecklistTemplate> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates`,
      { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create checklist template');
    }
    return response.json();
  },

  async updateTemplate(
    id: string,
    data: UpdateChecklistTemplateDto
  ): Promise<AwardVerificationChecklistTemplate> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates/${id}`,
      { method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(data) }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update checklist template');
    }
    return response.json();
  },

  async deleteTemplate(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/templates/${id}`,
      { method: 'DELETE', headers: getAuthHeaders() }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete checklist template');
    }
  },

  // Verification Methods
  async getVerificationByTender(
    tenderId: string
  ): Promise<TenderAwardVerification | null> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/by-tender/${tenderId}`,
      { headers: getAuthHeaders() }
    );
    if (response.status === 404) return null;
    if (!response.ok) throw new Error('Failed to fetch verification');
    return response.json();
  },

  async startVerification(
    data: StartVerificationDto
  ): Promise<TenderAwardVerification> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications`,
      { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to start verification');
    }
    return response.json();
  },

  async verifyChecklistItem(
    data: VerifyChecklistItemDto
  ): Promise<TenderAwardVerificationItemResult> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/verify-item`,
      { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to verify item');
    }
    return response.json();
  },

  async completeBidderVerification(
    data: CompleteBidderVerificationDto
  ): Promise<TenderAwardVerificationBidder> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/complete-bidder`,
      { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to complete bidder verification');
    }
    return response.json();
  },

  /**
   * Verify all items for a bidder and complete the bidder verification
   * This is a convenience method that verifies all items and then completes the bidder
   */
  async verifyBidder(
    verificationId: string,
    data: VerifyBidderDto
  ): Promise<TenderAwardVerificationBidder> {
    // First, verify each item
    for (const item of data.itemResults) {
      await this.verifyChecklistItem({
        bidderId: data.bidderId,
        checklistItemId: item.checklistItemId,
        status: item.status,
        comments: item.comments,
      });
    }

    // Then complete the bidder verification
    return this.completeBidderVerification({
      bidderId: data.bidderId,
      overallComments: data.overallComments,
    });
  },

  async completeVerification(
    verificationId: string,
    notes?: string
  ): Promise<TenderAwardVerification> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/${verificationId}/complete`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ notes }),
      }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to complete verification');
    }
    return response.json();
  },

  // Document Methods
  async uploadDocument(
    itemResultId: string,
    file: File,
    data: UploadVerificationDocumentDto
  ): Promise<TenderAwardVerificationItemDocument> {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', data.documentType);
    if (data.description?.trim())
      form.append('description', data.description.trim());
    const token =
      localStorage.getItem('token') || localStorage.getItem('authToken');
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/item-results/${itemResultId}/documents`,
      {
        method: 'POST',
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
        body: form,
      }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to upload document');
    }
    return response.json();
  },

  async getDocumentsByItemResult(
    itemResultId: string
  ): Promise<TenderAwardVerificationItemDocument[]> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/item-results/${itemResultId}/documents`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error('Failed to fetch documents');
    return response.json();
  },

  async getDocumentById(
    documentId: string
  ): Promise<TenderAwardVerificationItemDocument> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/documents/${documentId}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error('Failed to fetch document');
    return response.json();
  },

  async downloadDocument(documentId: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/documents/${documentId}/download`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to download document');
    }
    return response.blob();
  },

  async deleteDocument(documentId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/AwardVerifications/documents/${documentId}`,
      { method: 'DELETE', headers: getAuthHeaders() }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete document');
    }
  },
};
