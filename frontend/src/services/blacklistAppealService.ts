const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

export interface BlacklistAppealDto {
  id: string;
  businessPartnerId: string;
  partnerName?: string;
  partnerCode?: string;
  appealNumber: string;
  appealDate: Date;
  status: string;
  appealReason: string;
  supportingDocuments?: string;
  correctiveActionsTaken?: string;
  preventiveMeasures?: string;
  reviewedById?: string;
  reviewedByName?: string;
  reviewedDate?: Date;
  reviewerComments?: string;
  rejectionReason?: string;
  approvedById?: string;
  approvedByName?: string;
  approvedDate?: Date;
  removeBlacklist?: boolean;
  newBlacklistExpiryDate?: Date;
  decisionNotes?: string;
  createdAt: Date;
}

export interface CreateBlacklistAppealDto {
  businessPartnerId: string;
  appealReason: string;
  supportingDocuments?: string;
  correctiveActionsTaken?: string;
  preventiveMeasures?: string;
}

export interface ReviewBlacklistAppealDto {
  reviewerComments?: string;
}

export interface ApproveBlacklistAppealDto {
  removeBlacklist: boolean;
  newBlacklistExpiryDate?: Date;
  decisionNotes?: string;
}

export interface RejectBlacklistAppealDto {
  rejectionReason: string;
  decisionNotes?: string;
}

export interface BlacklistHistoryDto {
  id: string;
  businessPartnerId: string;
  partnerName?: string;
  action: string;
  actionDate: Date;
  actionById?: string;
  actionByName?: string;
  reason?: string;
  blacklistDate?: Date;
  blacklistExpiryDate?: Date;
  relatedAppealId?: string;
  relatedAppealNumber?: string;
  notes?: string;
}

class BlacklistAppealService {
  private baseUrl = `${API_BASE_URL}/procurement/partner-blacklist`;

  async createAppeal(partnerId: string, appeal: Omit<CreateBlacklistAppealDto, 'businessPartnerId'>): Promise<BlacklistAppealDto> {
    const response = await fetch(`${this.baseUrl}/partners/${partnerId}/appeal`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(appeal)
    });
    if (!response.ok) throw new Error('Failed to create appeal');
    return response.json();
  }

  async getAppealById(id: string): Promise<BlacklistAppealDto> {
    const response = await fetch(`${this.baseUrl}/appeals/${id}`, {
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to get appeal');
    return response.json();
  }

  async getAppealsByPartner(partnerId: string): Promise<BlacklistAppealDto[]> {
    const response = await fetch(`${this.baseUrl}/partners/${partnerId}/appeals`, {
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to get appeals');
    return response.json();
  }

  async getPendingAppeals(): Promise<BlacklistAppealDto[]> {
    const response = await fetch(`${this.baseUrl}/appeals/pending`, {
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to get pending appeals');
    return response.json();
  }

  async reviewAppeal(id: string, review: ReviewBlacklistAppealDto): Promise<BlacklistAppealDto> {
    const response = await fetch(`${this.baseUrl}/appeals/${id}/review`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(review)
    });
    if (!response.ok) throw new Error('Failed to review appeal');
    return response.json();
  }

  async approveAppeal(id: string, approval: ApproveBlacklistAppealDto): Promise<BlacklistAppealDto> {
    const response = await fetch(`${this.baseUrl}/appeals/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(approval)
    });
    if (!response.ok) throw new Error('Failed to approve appeal');
    return response.json();
  }

  async rejectAppeal(id: string, rejection: RejectBlacklistAppealDto): Promise<BlacklistAppealDto> {
    const response = await fetch(`${this.baseUrl}/appeals/${id}/reject`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(rejection)
    });
    if (!response.ok) throw new Error('Failed to reject appeal');
    return response.json();
  }

  async getBlacklistHistory(partnerId: string): Promise<BlacklistHistoryDto[]> {
    const response = await fetch(`${this.baseUrl}/partners/${partnerId}/history`, {
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to get blacklist history');
    return response.json();
  }
}

export const blacklistAppealService = new BlacklistAppealService();

