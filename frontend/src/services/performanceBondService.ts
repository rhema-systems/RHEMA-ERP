// Performance Bond Service - API calls for performance bond management

// ==================== INTERFACES ====================

export interface PerformanceBondRequestDto {
  id: string;
  tenderAwardId: string;
  tenderBidId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  tenderNumber: string;
  tenderTitle: string;
  bidNumber: string;
  status: string; // Pending, Submitted, Approved, Rejected
  templateFileName?: string;
  templateFileType?: string;
  templateFileSize?: number;
  hasTemplate: boolean;
  requestedDate: string;
  requestedByName?: string;
  submittedFileName?: string;
  submittedFileType?: string;
  submittedFileSize?: number;
  submittedDate?: string;
  submittedByName?: string;
  hasSubmission: boolean;
  reviewedDate?: string;
  reviewedByName?: string;
  rejectionReason?: string;
  notes?: string;
  createdAt: string;
}

export interface CreatePerformanceBondRequestDto {
  tenderAwardId: string;
  tenderBidId: string;
  businessPartnerId: string;
  notes?: string;
}

export interface SubmitPerformanceBondDto {
  notes?: string;
}

export interface ReviewPerformanceBondDto {
  isApproved: boolean;
  rejectionReason?: string;
  notes?: string;
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

function getAuthHeadersMultipart(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

// Get performance bond request by ID
export async function getPerformanceBondById(id: string): Promise<PerformanceBondRequestDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/${id}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch performance bond request');
  }

  return response.json();
}

// Get performance bond request by award ID
export async function getPerformanceBondByAwardId(awardId: string): Promise<PerformanceBondRequestDto | null> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/by-award/${awardId}`, {
    headers: getAuthHeaders(),
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Failed to fetch performance bond by award');
  }

  return response.json();
}

// Get performance bond request by bid ID
export async function getPerformanceBondByBidId(bidId: string): Promise<PerformanceBondRequestDto | null> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/by-bid/${bidId}`, {
    headers: getAuthHeaders(),
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error('Failed to fetch performance bond by bid');
  }

  return response.json();
}

// Get performance bond requests by business partner ID
export async function getPerformanceBondsByBusinessPartnerId(businessPartnerId: string): Promise<PerformanceBondRequestDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/by-business-partner/${businessPartnerId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch performance bonds by business partner');
  }

  return response.json();
}

// Get pending performance bond requests
export async function getPendingPerformanceBonds(): Promise<PerformanceBondRequestDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/pending`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch pending performance bonds');
  }

  return response.json();
}

// Get performance bond requests by status
export async function getPerformanceBondsByStatus(status: string): Promise<PerformanceBondRequestDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/by-status/${status}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch performance bonds by status');
  }

  return response.json();
}

// Create performance bond request (internal user sends to business partner)
export async function createPerformanceBondRequest(
  data: CreatePerformanceBondRequestDto,
  templateFile?: File
): Promise<PerformanceBondRequestDto> {
  const formData = new FormData();
  formData.append('tenderAwardId', data.tenderAwardId);
  formData.append('tenderBidId', data.tenderBidId);
  formData.append('businessPartnerId', data.businessPartnerId);
  if (data.notes) formData.append('notes', data.notes);
  if (templateFile) formData.append('templateFile', templateFile);

  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds`, {
    method: 'POST',
    headers: getAuthHeadersMultipart(),
    body: formData,
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to create performance bond request');
  }

  return response.json();
}

// Submit performance bond (business partner submits completed bond)
export async function submitPerformanceBond(
  requestId: string,
  file: File,
  notes?: string
): Promise<PerformanceBondRequestDto> {
  const formData = new FormData();
  formData.append('file', file);
  if (notes) formData.append('notes', notes);

  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/${requestId}/submit`, {
    method: 'POST',
    headers: getAuthHeadersMultipart(),
    body: formData,
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to submit performance bond');
  }

  return response.json();
}

// Review performance bond (internal user approves/rejects)
export async function reviewPerformanceBond(
  requestId: string,
  data: ReviewPerformanceBondDto
): Promise<PerformanceBondRequestDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/${requestId}/review`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || 'Failed to review performance bond');
  }

  return response.json();
}

// Download performance bond template
export async function downloadPerformanceBondTemplate(requestId: string): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/${requestId}/download-template`, {
    headers: getAuthHeadersMultipart(),
  });

  if (!response.ok) {
    throw new Error('Failed to download performance bond template');
  }

  return response.blob();
}

// Download submitted performance bond document
export async function downloadPerformanceBondSubmission(requestId: string): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/procurement/performance-bonds/${requestId}/download-submission`, {
    headers: getAuthHeadersMultipart(),
  });

  if (!response.ok) {
    throw new Error('Failed to download performance bond submission');
  }

  return response.blob();
}

// Helper function to trigger file download
export function triggerFileDownload(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
}

