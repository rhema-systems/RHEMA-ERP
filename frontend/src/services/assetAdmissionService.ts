import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Asset Admission interfaces matching the backend DTOs
export interface AssetAdmission {
  id: string;
  admissionNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  jobCardId?: string;
  workOrderId?: string;
  admissionDate: string;
  admittedById: string;
  admittedBy: string;
  admissionType: 'Scheduled' | 'Emergency' | 'Breakdown';
  assetConditionOnAdmission: 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical';
  admissionNotes?: string;
  observedProblems?: string;
  mileageReading?: number;
  hoursReading?: number;
  fuelLevel?: number;
  admissionChecklist?: Record<string, any>;
  photoPaths?: string[];
  documentPaths?: string[];
  admissionLocation?: string;
  bayOrStation?: string;
  estimatedCompletionDate?: string;
  estimatedDischargeDate?: string;
  status: 'Active' | 'Completed' | 'Cancelled';
  dischargeId?: string;
  createdAt: string;
}

export interface AssetDischarge {
  id: string;
  dischargeNumber: string;
  admissionId: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  jobCardId?: string;
  workOrderId?: string;
  dischargeDate: string;
  dischargedById: string;
  dischargedBy: string;
  assetConditionOnDischarge: 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical';
  dischargeNotes?: string;
  workCompleted?: string;
  remainingIssues?: string;
  mileageReading?: number;
  hoursReading?: number;
  fuelLevel?: number;
  qualityCheckPassed: boolean;
  qualityCheckedById?: string;
  qualityCheckedBy?: string;
  qualityCheckDate?: string;
  qualityCheckNotes?: string;
  dischargeChecklist?: Record<string, any>;
  photoPaths?: string[];
  documentPaths?: string[];
  certificateGenerated: boolean;
  certificateGeneratedDate?: string;
  certificatePath?: string;
  customerAcceptance: boolean;
  acceptedById?: string;
  acceptedBy?: string;
  acceptedDate?: string;
  acceptanceNotes?: string;
  requiresFollowUp: boolean;
  followUpDate?: string;
  followUpInstructions?: string;
  warrantyDays: number;
  warrantyExpiration?: string;
  warrantyTerms?: string;
  createdAt: string;
}

export interface CreateAdmissionRequest {
  assetId: string;
  jobCardId?: string;
  workOrderId?: string;
  admissionType: 'Scheduled' | 'Emergency' | 'Breakdown';
  assetConditionOnAdmission: 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical';
  admissionNotes?: string;
  observedProblems?: string;
  mileageReading?: number;
  hoursReading?: number;
  fuelLevel?: number;
  admissionChecklist?: Record<string, any>;
  admissionLocation?: string;
  bayOrStation?: string;
  estimatedCompletionDate?: string;
  estimatedDischargeDate?: string;
}

export interface CreateDischargeRequest {
  admissionId: string;
  assetConditionOnDischarge: 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical';
  dischargeNotes?: string;
  workCompleted?: string;
  remainingIssues?: string;
  mileageReading?: number;
  hoursReading?: number;
  fuelLevel?: number;
  qualityCheckPassed: boolean;
  qualityCheckNotes?: string;
  dischargeChecklist?: Record<string, any>;
  customerAcceptance: boolean;
  acceptanceNotes?: string;
  requiresFollowUp: boolean;
  followUpDate?: string;
  followUpInstructions?: string;
  warrantyDays?: number;
  warrantyTerms?: string;
}

export interface AdmissionFilterParams {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
  status?: string;
  admissionType?: string;
  assetId?: string;
  workOrderId?: string;
  jobCardId?: string;
  admissionFrom?: string;
  admissionTo?: string;
  bayOrStation?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

class AssetAdmissionService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  }

  // Asset Admission Methods
  async getAdmissions(params: AdmissionFilterParams = {}): Promise<PagedResult<AssetAdmission>> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getAdmissionById(id: string): Promise<AssetAdmission> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createAdmission(data: CreateAdmissionRequest): Promise<AssetAdmission> {
    const response = await axios.post(`${API_URL}/maintenance/asset-admissions`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateAdmission(id: string, data: Partial<CreateAdmissionRequest>): Promise<AssetAdmission> {
    const response = await axios.put(`${API_URL}/maintenance/asset-admissions/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async cancelAdmission(id: string, reason: string): Promise<void> {
    await axios.post(`${API_URL}/maintenance/asset-admissions/${id}/cancel`, { reason }, {
      headers: this.getAuthHeaders()
    });
  }

  // Asset Discharge Methods
  async getDischarges(params: AdmissionFilterParams = {}): Promise<PagedResult<AssetDischarge>> {
    const response = await axios.get(`${API_URL}/maintenance/asset-discharges`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getDischargeById(id: string): Promise<AssetDischarge> {
    const response = await axios.get(`${API_URL}/maintenance/asset-discharges/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createDischarge(data: CreateDischargeRequest): Promise<AssetDischarge> {
    const response = await axios.post(`${API_URL}/maintenance/asset-discharges`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateDischarge(id: string, data: Partial<CreateDischargeRequest>): Promise<AssetDischarge> {
    const response = await axios.put(`${API_URL}/maintenance/asset-discharges/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Helper Methods
  async getActiveAdmissions(): Promise<AssetAdmission[]> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/active`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getAdmissionsByAsset(assetId: string): Promise<AssetAdmission[]> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/by-asset/${assetId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getAdmissionsByWorkOrder(workOrderId: string): Promise<AssetAdmission[]> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/by-work-order/${workOrderId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getDischargesByAdmission(admissionId: string): Promise<AssetDischarge[]> {
    const response = await axios.get(`${API_URL}/maintenance/asset-discharges/by-admission/${admissionId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async generateCompletionCertificate(dischargeId: string): Promise<{ certificateNumber: string }> {
    const response = await axios.post(`${API_URL}/maintenance/asset-discharges/${dischargeId}/generate-certificate`, {}, {
      headers: this.getAuthHeaders()
    });

    const cert = response.data as { certificateNumber: string };
    return { certificateNumber: cert.certificateNumber };
  }

  // Photo and Document Management
  async uploadAdmissionPhoto(admissionId: string, file: File): Promise<{ filePath: string }> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await axios.post(`${API_URL}/maintenance/asset-admissions/${admissionId}/photos`, formData, {
      headers: {
        'Authorization': localStorage.getItem('authToken') ? `Bearer ${localStorage.getItem('authToken')}` : '',
        'Content-Type': 'multipart/form-data'
      }
    });
    return response.data;
  }

  async uploadDischargePhoto(dischargeId: string, file: File): Promise<{ filePath: string }> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await axios.post(`${API_URL}/maintenance/asset-discharges/${dischargeId}/photos`, formData, {
      headers: {
        'Authorization': localStorage.getItem('authToken') ? `Bearer ${localStorage.getItem('authToken')}` : '',
        'Content-Type': 'multipart/form-data'
      }
    });
    return response.data;
  }

  // Dashboard and Statistics
  async getAdmissionStats(): Promise<{
    totalActive: number;
    totalCompleted: number;
    averageStayDays: number;
    byCondition: Array<{ condition: string; count: number }>;
    byType: Array<{ type: string; count: number }>;
  }> {
    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/stats`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getDowntimeReport(assetId?: string, fromDate?: string, toDate?: string): Promise<{
    totalDowntimeHours: number;
    averageDowntimeHours: number;
    downtimeByAsset: Array<{ assetId: string; assetName: string; assetNumber: string; downtimeHours: number; averageDowntimeHours: number; incidentCount: number }>;
  }> {
    const params: any = {};
    if (assetId) params.assetId = assetId;
    if (fromDate) params.fromDate = fromDate;
    if (toDate) params.toDate = toDate;

    const response = await axios.get(`${API_URL}/maintenance/asset-admissions/downtime-report`, {
      params,
      headers: this.getAuthHeaders()
    });

    const raw = response.data as {
      totalDowntimeHours: number;
      averageDowntimeHours: number;
      downtimeByAsset: Array<{ assetId: string; assetName: string; assetNumber: string; totalDowntimeHours: number; averageDowntimeHours: number; incidentCount: number }>;
    };

    return {
      totalDowntimeHours: raw.totalDowntimeHours,
      averageDowntimeHours: raw.averageDowntimeHours,
      downtimeByAsset: raw.downtimeByAsset.map(item => ({
        assetId: item.assetId,
        assetName: item.assetName,
        assetNumber: item.assetNumber,
        downtimeHours: item.totalDowntimeHours,
        averageDowntimeHours: item.averageDowntimeHours,
        incidentCount: item.incidentCount
      }))
    };
  }
}

export const assetAdmissionService = new AssetAdmissionService();
export default assetAdmissionService;
