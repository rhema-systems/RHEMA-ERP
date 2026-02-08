// Tender Negotiation Service - API calls for tender negotiation management

// Types
export interface TenderNegotiationItemDto {
  id: string;
  negotiationId: string;
  tenderBidItemId: string;
  itemDescription: string;
  quantity: number;
  unitOfMeasure?: string;
  originalUnitPrice: number;
  originalTotalPrice: number;
  negotiatedUnitPrice?: number;
  negotiatedTotalPrice?: number;
  notes?: string;
}

export interface TenderNegotiationDto {
  id: string;
  tenderId: string;
  tenderNumber?: string;
  tenderTitle?: string;
  tenderBidId: string;
  bidNumber?: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  lotId?: string;
  lotCode?: string;
  lotTitle?: string;
  bidLotId?: string;
  status: 'Invited' | 'InProgress' | 'Completed' | 'Cancelled';
  invitedDate: string;
  invitedById?: string;
  invitedByName?: string;
  completedDate?: string;
  completedById?: string;
  completedByName?: string;
  originalAmount: number;
  negotiatedAmount?: number;
  currency?: string;
  notes?: string;
  items: TenderNegotiationItemDto[];
  createdAt: string;
}

export interface CreateNegotiationDto {
  tenderId: string;
  tenderBidId: string;
  lotId?: string;
  bidLotId?: string;
  notes?: string;
}

export interface UpdateNegotiationItemDto {
  itemId: string;
  negotiatedUnitPrice?: number;
  notes?: string;
}

export interface CompleteNegotiationDto {
  items: UpdateNegotiationItemDto[];
  notes?: string;
}

export interface SaveDraftDto {
  items: UpdateNegotiationItemDto[];
  notes?: string;
}

// API Functions
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
const BASE_URL = `${API_BASE_URL}/procurement/negotiations`;

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export async function getNegotiationById(id: string): Promise<TenderNegotiationDto> {
  const response = await fetch(`${BASE_URL}/${id}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    throw new Error('Failed to fetch negotiation');
  }
  return response.json();
}

export async function getNegotiationByTenderAndBid(
  tenderId: string,
  bidId: string,
  lotId?: string
): Promise<TenderNegotiationDto | null> {
  const params = new URLSearchParams({ tenderId, bidId });
  if (lotId) {
    params.append('lotId', lotId);
  }
  const response = await fetch(`${BASE_URL}/by-tender-bid?${params.toString()}`, {
    headers: getAuthHeaders(),
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error('Failed to fetch negotiation');
  }
  return response.json();
}

export async function getNegotiationsByTenderId(tenderId: string): Promise<TenderNegotiationDto[]> {
  const response = await fetch(`${BASE_URL}/by-tender/${tenderId}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    throw new Error('Failed to fetch negotiations');
  }
  return response.json();
}

export async function getNegotiationsByBidId(bidId: string): Promise<TenderNegotiationDto[]> {
  const response = await fetch(`${BASE_URL}/by-bid/${bidId}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    throw new Error('Failed to fetch negotiations');
  }
  return response.json();
}

export async function createNegotiation(dto: CreateNegotiationDto): Promise<TenderNegotiationDto> {
  const response = await fetch(BASE_URL, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(dto),
  });
  if (!response.ok) {
    throw new Error('Failed to create negotiation');
  }
  return response.json();
}

export async function updateNegotiationItem(
  negotiationId: string,
  dto: UpdateNegotiationItemDto
): Promise<TenderNegotiationDto> {
  const response = await fetch(`${BASE_URL}/${negotiationId}/items`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(dto),
  });
  if (!response.ok) {
    throw new Error('Failed to update negotiation item');
  }
  return response.json();
}

export async function completeNegotiation(
  negotiationId: string,
  dto: CompleteNegotiationDto
): Promise<TenderNegotiationDto> {
  const response = await fetch(`${BASE_URL}/${negotiationId}/complete`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(dto),
  });
  if (!response.ok) {
    throw new Error('Failed to complete negotiation');
  }
  return response.json();
}

export async function cancelNegotiation(negotiationId: string): Promise<void> {
  const response = await fetch(`${BASE_URL}/${negotiationId}/cancel`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    throw new Error('Failed to cancel negotiation');
  }
}

export async function saveDraft(
  negotiationId: string,
  dto: SaveDraftDto
): Promise<TenderNegotiationDto> {
  const response = await fetch(`${BASE_URL}/${negotiationId}/save-draft`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(dto),
  });
  if (!response.ok) {
    throw new Error('Failed to save draft');
  }
  return response.json();
}

