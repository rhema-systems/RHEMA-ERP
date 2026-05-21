import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// JobCard interfaces matching the backend DTOs
export interface JobCard {
  id: string;
  jobCardNumber: string;
  title: string;
  description?: string;
  problemDescription?: string;
  assetId: string;
  assetName: string;
  assetCode: string;
  maintenanceTypeId: string;
  maintenanceType: string;
  priorityLevelId: string;
  priority: string;
  priorityColor: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  workOrderBillingType?: 'Maintenance' | 'Repairs';
  jobCardStatus: string;
  approvalStatus: string;
  requestedById: string;
  requestedBy: string;
  requestedDate: string;
  requiredCompletionDate?: string;
  estimatedHours: number;
  estimatedCost: number;
  requiresShutdown: boolean;
  requiresSafetyPermit: boolean;
  generatedWorkOrderId?: string;
  workOrderGeneratedAt?: string;
  createdAt: string;
}

export interface JobCardDetails extends JobCard {
  assetType: string;
  assetLocation: string;
  maintenanceCategory: string;
  priorityLevel: number;
  maintenanceLocation: string;
  preferredTechnicianId?: string;
  preferredTechnician?: string;
  preferredTeamId?: string;
  preferredTeam?: string;
  contractorId?: string;
  contractor?: string;
  requiresSpecialTools: boolean;
  specialInstructions?: string;
  safetyRequirements?: string;
  submittedAt?: string;
  approvedAt?: string;
  approvedById?: string;
  approvedBy?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  assignedTechnicianId?: string;
  assignedTechnician?: string;
  assignedTeamId?: string;
  assignedTeam?: string;
  generatedWorkOrderNumber?: string;
  documents: JobCardDocument[];
  comments: JobCardComment[];
  approvalSteps: JobCardApprovalStep[];
  customFieldValues?: Record<string, any>;
  updatedAt?: string;
}

export interface JobCardDocument {
  id: string;
  fileName: string;
  filePath: string;
  contentType?: string;
  fileSize: number;
  documentType: string;
  uploadedAt: string;
  uploadedBy: string;
}

export interface JobCardComment {
  id: string;
  comment: string;
  commentType: string;
  isInternal: boolean;
  commentDate: string;
  commentBy: string;
}

export interface JobCardApprovalStep {
  id: string;
  stepOrder: number;
  stepName: string;
  approverId?: string;
  approverName?: string;
  status: string;
  actionDate?: string;
  comments?: string;
  isRequired: boolean;
}

export interface CreateJobCardRequest {
  title: string;
  description?: string;
  problemDescription?: string;
  assetId: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  customerBusinessPartnerId?: string;
  workOrderBillingType?: 'Maintenance' | 'Repairs';
  maintenanceLocation?: string;
  requiredCompletionDate?: string;
  estimatedHours?: number;
  estimatedCost?: number;
  preferredTechnicianId?: string;
  preferredTeamId?: string;
  contractorId?: string;
  requiresSpecialTools?: boolean;
  requiresShutdown?: boolean;
  requiresSafetyPermit?: boolean;
  specialInstructions?: string;
  safetyRequirements?: string;
  customFieldValues?: Record<string, any>;
}

export interface UpdateJobCardRequest {
  title: string;
  description?: string;
  problemDescription?: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  customerBusinessPartnerId?: string;
  workOrderBillingType?: 'Maintenance' | 'Repairs';
  maintenanceLocation?: string;
  requiredCompletionDate?: string;
  estimatedHours?: number;
  estimatedCost?: number;
  preferredTechnicianId?: string;
  preferredTeamId?: string;
  contractorId?: string;
  requiresSpecialTools?: boolean;
  requiresShutdown?: boolean;
  requiresSafetyPermit?: boolean;
  specialInstructions?: string;
  safetyRequirements?: string;
  customFieldValues?: Record<string, any>;
}

export interface JobCardApprovalAction {
  action: 'Approve' | 'Reject' | 'RequestChanges';
  comments?: string;
  plannedStartDate?: string;
  plannedEndDate?: string;
  assignedTechnicianId?: string;
  assignedTeamId?: string;
  revisedEstimatedHours?: number;
  revisedEstimatedCost?: number;
  /** Work order billing type: "Maintenance" (fixed price) or "Repairs" (itemized costs) */
  billingType?: 'Maintenance' | 'Repairs';
}

export interface SubmitJobCardRequest {
  submissionNotes?: string;
  confirmReadiness?: boolean;
}

export interface AddJobCardCommentRequest {
  comment: string;
  commentType?: string;
  isInternal?: boolean;
}

export interface JobCardFilterParams {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
  status?: string;
  approvalStatus?: string;
  assetId?: string;
  maintenanceTypeId?: string;
  priorityLevelId?: string;
  requestedById?: string;
  assignedTechnicianId?: string;
  requestedFrom?: string;
  requestedTo?: string;
  requiredFrom?: string;
  requiredTo?: string;
  requiresApproval?: boolean;
  hasWorkOrder?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface JobCardDashboardStats {
  totalJobCards: number;
  draftJobCards: number;
  submittedJobCards: number;
  underReviewJobCards: number;
  approvedJobCards: number;
  rejectedJobCards: number;
  cancelledJobCards: number;
  pendingApprovals: number;
  readyForWorkOrder: number;
  convertedToWorkOrders: number;
  totalEstimatedCost: number;
  totalEstimatedHours: number;
  byPriority: Array<{ priority: string; count: number; estimatedCost: number }>;
  byMaintenanceType: Array<{ maintenanceType: string; count: number; estimatedCost: number }>;
  byStatus: Array<{ status: string; count: number; estimatedCost: number }>;
}

class JobCardService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  }

  // Get paginated job cards with filtering
  async getJobCards(params: JobCardFilterParams = {}): Promise<PagedResult<JobCard>> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job card by ID
  async getJobCardById(id: string): Promise<JobCardDetails> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job card by number
  async getJobCardByNumber(jobCardNumber: string): Promise<JobCardDetails> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/by-number/${jobCardNumber}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Create new job card
  async createJobCard(data: CreateJobCardRequest): Promise<JobCardDetails> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Update existing job card
  async updateJobCard(id: string, data: UpdateJobCardRequest): Promise<JobCardDetails> {
    const response = await axios.put(`${API_URL}/maintenance/job-cards/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Delete job card
  async deleteJobCard(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/job-cards/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Submit job card for approval
  async submitJobCard(id: string, data: SubmitJobCardRequest): Promise<JobCardDetails> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/submit`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Process approval action
  async processApproval(id: string, action: JobCardApprovalAction): Promise<JobCardDetails> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/approve`, action, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Cancel job card
  async cancelJobCard(id: string, reason: string): Promise<JobCardDetails> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/cancel`, { reason }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Generate work order from job card
  async generateWorkOrder(id: string): Promise<{ workOrderId: string }> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/generate-work-order`, {}, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Add comment to job card
  async addComment(id: string, comment: AddJobCardCommentRequest): Promise<JobCardComment> {
    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/comments`, comment, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job card comments
  async getComments(id: string): Promise<JobCardComment[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/${id}/comments`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get approval history
  async getApprovalHistory(id: string): Promise<JobCardApprovalStep[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/${id}/approval-history`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get pending approvals
  async getPendingApprovals(params: { page?: number; pageSize?: number; searchTerm?: string } = {}): Promise<PagedResult<JobCard>> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/pending-approvals`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get approved job cards ready for work order generation
  async getApprovedJobCards(): Promise<JobCard[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/approved`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job cards by asset
  async getJobCardsByAsset(assetId: string): Promise<JobCard[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/by-asset/${assetId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job cards by requester
  async getJobCardsByRequester(requesterId: string): Promise<JobCard[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/by-requester/${requesterId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get job cards by technician
  async getJobCardsByTechnician(technicianId: string): Promise<JobCard[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/by-technician/${technicianId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get dashboard statistics
  async getDashboardStats(): Promise<JobCardDashboardStats> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/dashboard-stats`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Upload document
  async uploadDocument(id: string, file: File, documentType: string = 'General'): Promise<JobCardDocument> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);

    const response = await axios.post(`${API_URL}/maintenance/job-cards/${id}/documents`, formData, {
      headers: {
        'Authorization': localStorage.getItem('authToken') ? `Bearer ${localStorage.getItem('authToken')}` : '',
        'Content-Type': 'multipart/form-data'
      }
    });
    return response.data;
  }

  // Delete document
  async deleteDocument(id: string, documentId: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/job-cards/${id}/documents/${documentId}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Get documents
  async getDocuments(id: string): Promise<JobCardDocument[]> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/${id}/documents`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Download document
  async downloadDocument(id: string, documentId: string): Promise<Blob> {
    const response = await axios.get(`${API_URL}/maintenance/job-cards/${id}/documents/${documentId}/download`, {
      headers: this.getAuthHeaders(),
      responseType: 'blob'
    });
    return response.data;
  }
}

export const jobCardService = new JobCardService();
export default jobCardService;
