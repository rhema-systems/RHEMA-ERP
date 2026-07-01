// ==================== TYPES ====================

export interface TenderAssignmentDto {
  id: string;
  tenderId: string;
  tenderNumber?: string;
  tenderTitle?: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  assignedToUserId?: string;
  assignedToUserName?: string;
  assignmentType: string; // AllUsers, Self, SelectedUsers
  assignedAt: string;
  assignedByName?: string;
  notes?: string;
}

export interface CreateTenderAssignmentDto {
  tenderId: string;
  businessPartnerId: string;
  assignmentType: string; // AllUsers, Self, SelectedUsers
  assignedUserIds?: string[]; // Required for SelectedUsers
  notes?: string;
}

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// Get assignments by tender ID
export async function getAssignmentsByTenderId(tenderId: string): Promise<TenderAssignmentDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/tender/${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch tender assignments');
  }

  return response.json();
}

// Get assignments by user ID
export async function getAssignmentsByUserId(userId: string): Promise<TenderAssignmentDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/user/${userId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch user assignments');
  }

  return response.json();
}

// Get assignments by business partner ID
export async function getAssignmentsByBusinessPartnerId(businessPartnerId: string): Promise<TenderAssignmentDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/business-partner/${businessPartnerId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch business partner assignments');
  }

  return response.json();
}

// Create assignment
export async function createAssignment(data: CreateTenderAssignmentDto): Promise<TenderAssignmentDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to create tender assignment');
  }

  return response.json();
}

// Delete assignment
export async function deleteAssignment(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete assignment');
  }
}

// Check if user has access to tender
export async function hasAccessToTender(userId: string, tenderId: string): Promise<boolean> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/has-access?userId=${userId}&tenderId=${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to check tender access');
  }

  return response.json();
}

// Get accessible tender IDs for user
export async function getAccessibleTenderIds(userId: string, businessPartnerId: string): Promise<string[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/tender-assignments/accessible-tenders?userId=${userId}&businessPartnerId=${businessPartnerId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch accessible tenders');
  }

  return response.json();
}
