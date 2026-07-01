const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export interface TenderDocumentType {
  id: string;
  documentName: string;
  documentCode: string;
  category: string;
  description?: string;
  isRequired: boolean;
  maxFileSizeMB: number;
  allowedFileTypes: string;
  isActive: boolean;
  displayOrder: number;
  createdAt: string;
  createdByName?: string;
}

export interface CreateTenderDocumentTypeDto {
  documentName: string;
  documentCode: string;
  category: string;
  description?: string;
  isRequired: boolean;
  maxFileSizeMB: number;
  allowedFileTypes: string;
  isActive: boolean;
  displayOrder: number;
}

export interface UpdateTenderDocumentTypeDto {
  documentName: string;
  description?: string;
  isRequired: boolean;
  maxFileSizeMB: number;
  allowedFileTypes: string;
  isActive: boolean;
  displayOrder: number;
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

export const tenderDocumentTypeService = {
  async getAll(): Promise<TenderDocumentType[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch tender document types');
    }
    return response.json();
  },

  async getActive(): Promise<TenderDocumentType[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes/active`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch active tender document types');
    }
    return response.json();
  },

  async getById(id: string): Promise<TenderDocumentType> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch tender document type');
    }
    return response.json();
  },

  async getByCategory(category: string): Promise<TenderDocumentType[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes/category/${category}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch tender document types by category');
    }
    return response.json();
  },

  async create(data: CreateTenderDocumentTypeDto): Promise<TenderDocumentType> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to create tender document type');
    }
    return response.json();
  },

  async update(id: string, data: UpdateTenderDocumentTypeDto): Promise<TenderDocumentType> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to update tender document type');
    }
    return response.json();
  },

  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/TenderDocumentTypes/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to delete tender document type');
    }
  },
};
