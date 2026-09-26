// ==================== TYPES ====================

export interface BusinessPartnerUserDto {
  id: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  userId: string;
  userName?: string;
  userEmail?: string;
  userFullName?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  role: string; // Admin, User, Viewer
  isActive: boolean;
  grantedAt: string;
  grantedByName?: string;
  notes?: string;
}

export interface CreateBusinessPartnerUserDto {
  businessPartnerId: string;
  email: string;
  firstName: string;
  lastName: string;
  userName: string; // Changed from username to userName to match backend
  password: string;
  phoneNumber?: string;
  role: string; // Admin, User, Viewer
  notes?: string;
}

export interface UpdateBusinessPartnerUserDto {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  role: string;
  isActive: boolean;
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

// Get all users for a business partner
export async function getUsersByBusinessPartnerId(businessPartnerId: string): Promise<BusinessPartnerUserDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/business-partner/${businessPartnerId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch business partner users');
  }

  return response.json();
}

// Get user by ID
export async function getUserById(id: string): Promise<BusinessPartnerUserDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch business partner user');
  }

  return response.json();
}

// Get user by user ID
export async function getUserByUserId(userId: string): Promise<BusinessPartnerUserDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/user/${userId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch business partner user');
  }

  return response.json();
}

// Create new user
export async function createUser(data: CreateBusinessPartnerUserDto): Promise<BusinessPartnerUserDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const errorText = await response.text();
    console.error('Create user error:', errorText);
    throw new Error(errorText || 'Failed to create business partner user');
  }

  return response.json();
}

export async function linkExistingExternalUser(businessPartnerId: string, userId: string): Promise<BusinessPartnerUserDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/business-partner/${businessPartnerId}/link/${userId}`, {
    method: 'POST', headers: getAuthHeaders(),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.detail || result.message || 'Unable to link the portal account.');
  return result;
}

// Update user
export async function updateUser(id: string, data: UpdateBusinessPartnerUserDto): Promise<BusinessPartnerUserDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to update business partner user');
  }

  return response.json();
}

// Activate user
export async function activateUser(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}/activate`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to activate user');
  }
}

// Deactivate user
export async function deactivateUser(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}/deactivate`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to deactivate user');
  }
}

// Reset password
export async function resetPassword(id: string, newPassword: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}/reset-password`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({ newPassword }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to reset password');
  }
}

// Delete user
export async function deleteUser(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete user');
  }
}

// Check if user has access
export async function hasAccess(userId: string, businessPartnerId: string): Promise<boolean> {
  const response = await fetch(`${API_BASE_URL}/procurement/business-partner-users/has-access?userId=${userId}&businessPartnerId=${businessPartnerId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to check access');
  }

  return response.json();
}
