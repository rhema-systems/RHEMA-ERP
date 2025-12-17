// Tender Award Service - API calls for tender award management

// ==================== INTERFACES ====================

export interface TenderAwardDto {
  id: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  tenderBidId: string;
  bidNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  awardDate: string;
  awardedAmount: number;
  currency?: string;
  status: string; // Awarded, Cancelled
  awardJustification?: string;
  awardedById?: string;
  awardedByName?: string;
  purchaseOrderId?: string;
  notes?: string;
  createdById?: string;
  createdAt: string;
}

export interface CreateAwardDto {
  tenderId: string;
  tenderBidId: string;
  awardedAmount: number;
  currency?: string;
  awardDate?: string;
  awardJustification?: string;
  notes?: string;
}

export interface AwardRecommendationDto {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  totalBids: number;
  evaluatedBids: number;
  recommendedBidId?: string;
  recommendedBidNumber?: string;
  recommendedBusinessPartner?: string;
  recommendedAmount: number;
  recommendedScore: number;
  bidRecommendations: BidRecommendationDto[];
  generatedAt: string;
  generatedById?: string;
}

export interface BidRecommendationDto {
  bidId: string;
  bidNumber: string;
  businessPartnerId: string;
  businessPartnerName: string;
  totalBidAmount: number;
  averageScore: number;
  evaluationCount: number;
  /** Number of evaluators who recommended this bid */
  recommendationCount: number;
  /** Total number of evaluators who submitted evaluations for this bid */
  totalEvaluators: number;
  recommendation?: string;
}

export interface ApproveAwardDto {
  notes?: string;
}

export interface RejectAwardDto {
  reason: string;
}

export interface AwardNotificationDto {
  awardId: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  awardAmount: number;
  awardDate: string;
  contractStartDate?: string;
  contractEndDate?: string;
  notificationDate: string;
}

export interface CancelAwardDto {
  reason: string;
  sendNotifications?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// Get all awards with pagination
export async function getAwards(
  page: number = 1,
  pageSize: number = 10,
  search?: string,
  status?: string
): Promise<PagedResult<TenderAwardDto>> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  
  if (search) params.append('search', search);
  if (status) params.append('status', status);

  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards?${params}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch awards');
  }

  return response.json();
}

// Get award by ID
export async function getAwardById(id: string): Promise<TenderAwardDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/${id}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch award');
  }

  return response.json();
}

// Get award by tender ID
export async function getAwardByTenderId(tenderId: string): Promise<TenderAwardDto | null> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/by-tender/${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Failed to fetch award by tender');
  }

  return response.json();
}

// Generate award recommendation
export async function generateAwardRecommendation(tenderId: string): Promise<AwardRecommendationDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/recommendation/${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to generate award recommendation');
  }

  return response.json();
}

// Create award
export async function createAward(data: CreateAwardDto): Promise<TenderAwardDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to create award');
  }

  return response.json();
}

// Update award
export async function updateAward(id: string, data: CreateAwardDto): Promise<TenderAwardDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to update award');
  }

  return response.json();
}

// Cancel award
export async function cancelAward(id: string, data: CancelAwardDto): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/${id}/cancel`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to cancel award');
  }
}

// Send award notifications
export async function sendAwardNotifications(tenderId: string, data: AwardNotificationDto): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderAwards/${tenderId}/notify`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to send award notifications');
  }
}


