import { compatibleApiService as apiService } from './compatibleApiService';

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
  status: string;
  currentStageName: string;
  currentAssignedRole?: string | null;
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
}

export const externalEstateServicesService =
  new ExternalEstateServicesService();
