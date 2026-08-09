import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export interface ExternalEstateRequestType {
  code: string;
  title: string;
  module: string;
  entityType: string;
  category: string;
}

export interface ExternalEstateServiceRequest {
  id: string;
  module: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  applicantName?: string | null;
  sourceDepartment?: string | null;
  status: string;
  currentStageName: string;
  currentAssignedRole?: string | null;
  fieldValues?: Record<string, string | null>;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateExternalEstateServiceRequest {
  requestType: string;
  applicantName?: string;
  contact?: string;
  propertyReference?: string;
  location?: string;
  category?: string;
  priority?: string;
  serviceImpact?: string;
  targetDate?: string;
  description?: string;
  additionalValues?: Record<string, string | null | undefined>;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

class ExternalEstateServicesService {
  async getRequestTypes(): Promise<ExternalEstateRequestType[]> {
    const response = await apiService.get<ApiResponse<ExternalEstateRequestType[]>>(
      '/estate/external/request-types'
    );
    return response.data || [];
  }

  async getMyRequests(): Promise<ExternalEstateServiceRequest[]> {
    const response = await apiService.get<ApiResponse<ExternalEstateServiceRequest[]>>(
      '/estate/external/requests'
    );
    return response.data || [];
  }

  async createRequest(
    payload: CreateExternalEstateServiceRequest
  ): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.post<ApiResponse<ExternalEstateServiceRequest>>(
      '/estate/external/requests',
      payload
    );
    return response.data;
  }

  async submitPropertyRequestDecision(
    requestId: string,
    payload: { decision: 'Accept' | 'Reject'; notes?: string | null }
  ): Promise<ExternalEstateServiceRequest> {
    const response = await apiService.post<ApiResponse<ExternalEstateServiceRequest>>(
      `/estate/external/requests/${requestId}/customer-decision`,
      payload
    );
    return response.data;
  }

  async uploadSignedAgreement(
    requestId: string,
    file: File,
    notes?: string | null
  ): Promise<ExternalEstateServiceRequest> {
    const formData = new FormData();
    formData.append('file', file);
    if (notes) {
      formData.append('notes', notes);
    }

    const response = await rawApiService.request<ApiResponse<ExternalEstateServiceRequest>>(
      `/estate/external/requests/${requestId}/signed-agreement`,
      {
        method: 'POST',
        body: formData,
      }
    );
    return response.data;
  }
}

export const externalEstateServicesService =
  new ExternalEstateServicesService();
