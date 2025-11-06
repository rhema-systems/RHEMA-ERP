import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Quality Control interfaces
export interface QualityChecklist {
  id: string;
  workOrderId: string;
  workOrderNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  checklistName: string;
  description?: string;
  templateId?: string;
  status: 'Pending' | 'InProgress' | 'Completed' | 'Failed';
  inspectorId?: string;
  inspectorName?: string;
  startedAt?: string;
  completedAt?: string;
  totalCheckItems: number;
  passedCheckItems: number;
  failedCheckItems: number;
  completionPercentage: number;
  overallResult: 'Pass' | 'Fail' | 'Pending';
  createdAt: string;
  updatedAt?: string;
}

export interface QualityCheckItem {
  id: string;
  checklistId: string;
  itemName: string;
  description: string;
  checkType: 'Visual' | 'Measurement' | 'Test' | 'Documentation';
  isRequired: boolean;
  expectedValue?: string;
  actualValue?: string;
  result: 'Pass' | 'Fail' | 'NotApplicable' | 'Pending';
  notes?: string;
  inspectedBy?: string;
  inspectedAt?: string;
  evidencePhotos?: string[];
  order: number;
}

export interface QualityValidationResult {
  workOrderId: string;
  canComplete: boolean;
  validationDate: string;
  requiredInspections: RequiredInspection[];
  validationMessages: string[];
  validationFailures: string[];
  requiresInspectionOfficerApproval: boolean;
  qualityScore?: number;
  recommendedActions?: string[];
}

export interface RequiredInspection {
  inspectionTemplateId: string;
  inspectionType: string;
  inspectionName: string;
  isRegulatory: boolean;
  description: string;
  isCompleted?: boolean;
  completedDate?: string;
  result?: 'Pass' | 'Fail';
}

export interface CreateQualityChecklistRequest {
  workOrderId: string;
  checklistName: string;
  description?: string;
  templateId?: string;
  checkItems: CreateQualityCheckItemRequest[];
}

export interface CreateQualityCheckItemRequest {
  itemName: string;
  description: string;
  checkType: 'Visual' | 'Measurement' | 'Test' | 'Documentation';
  isRequired: boolean;
  expectedValue?: string;
  order: number;
}

export interface UpdateQualityCheckItemRequest {
  actualValue?: string;
  result: 'Pass' | 'Fail' | 'NotApplicable';
  notes?: string;
}

export interface QualityInspectionOfficer {
  id: string;
  employeeId: string;
  name: string;
  email: string;
  certifications: string[];
  specializations: string[];
  isActive: boolean;
}

export interface QualityTemplate {
  id: string;
  name: string;
  description?: string;
  assetTypeId?: string;
  workOrderTypeId?: string;
  isActive: boolean;
  checkItems: QualityTemplateItem[];
}

export interface QualityTemplateItem {
  id: string;
  itemName: string;
  description: string;
  checkType: 'Visual' | 'Measurement' | 'Test' | 'Documentation';
  isRequired: boolean;
  expectedValue?: string;
  order: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface WorkOrderQualityCheck {
  id: string;
  workOrderId: string;
  workOrderNumber: string;
  assetName: string;
  checklistName: string;
  status: string;
  inspectionDate: string;
  overallResult?: string;
  score: number;
  inspectorName?: string;
}

class QualityControlService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  }

  // Quality Validation
  async validateWorkOrderCompletion(workOrderId: string): Promise<QualityValidationResult> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/validate-work-order/${workOrderId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getRequiredInspections(assetId: string, workOrderType: string): Promise<RequiredInspection[]> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/required-inspections`, {
      params: { assetId, workOrderType },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Submit Work Order for QC Inspection
  async submitWorkOrderForInspection(workOrderId: string): Promise<WorkOrderQualityCheck> {
    const response = await axios.post(`${API_URL}/maintenance/quality-control/submit-for-inspection/${workOrderId}`, {}, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get Pending Quality Inspections
  async getPendingInspections(): Promise<WorkOrderQualityCheck[]> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/pending-inspections`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async startInspection(workOrderId: string): Promise<WorkOrderQualityCheck> {
    const response = await axios.post(`${API_URL}/maintenance/quality-control/start-inspection/${workOrderId}`, {}, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Get Completed Inspections
  async getCompletedInspections(): Promise<any[]> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/completed-inspections`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Quality Checklists
  async getQualityChecklists(params: {
    page?: number;
    pageSize?: number;
    workOrderId?: string;
    status?: string;
    inspectorId?: string;
  } = {}): Promise<PagedResult<QualityChecklist>> {
    const response = await axios.get(`${API_URL}/maintenance/quality-checklists`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getQualityChecklistById(id: string): Promise<QualityChecklist> {
    const response = await axios.get(`${API_URL}/maintenance/quality-checklists/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createQualityChecklist(data: CreateQualityChecklistRequest): Promise<QualityChecklist> {
    const response = await axios.post(`${API_URL}/maintenance/quality-checklists`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateQualityChecklist(id: string, data: Partial<CreateQualityChecklistRequest>): Promise<QualityChecklist> {
    const response = await axios.put(`${API_URL}/maintenance/quality-checklists/${id}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteQualityChecklist(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/quality-checklists/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Quality Check Items
  async getQualityCheckItems(checklistId: string): Promise<QualityCheckItem[]> {
    const response = await axios.get(`${API_URL}/maintenance/quality-checklists/${checklistId}/items`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateQualityCheckItem(itemId: string, data: UpdateQualityCheckItemRequest): Promise<QualityCheckItem> {
    const response = await axios.put(`${API_URL}/maintenance/quality-check-items/${itemId}`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async uploadCheckItemEvidence(itemId: string, file: File): Promise<{ filePath: string }> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await axios.post(`${API_URL}/maintenance/quality-check-items/${itemId}/evidence`, formData, {
      headers: {
        'Authorization': localStorage.getItem('authToken') ? `Bearer ${localStorage.getItem('authToken')}` : '',
        'Content-Type': 'multipart/form-data'
      }
    });
    return response.data;
  }

  // Checklist Operations
  async startQualityChecklist(checklistId: string): Promise<QualityChecklist> {
    const response = await axios.post(`${API_URL}/maintenance/quality-checklists/${checklistId}/start`, {}, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async completeQualityChecklist(checklistId: string, notes?: string): Promise<QualityChecklist> {
    const response = await axios.post(`${API_URL}/maintenance/quality-checklists/${checklistId}/complete`, 
      { notes }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async assignInspector(checklistId: string, inspectorId: string): Promise<QualityChecklist> {
    const response = await axios.post(`${API_URL}/maintenance/quality-checklists/${checklistId}/assign`, 
      { inspectorId }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Quality Templates
  async getQualityTemplates(params: {
    page?: number;
    pageSize?: number;
    assetTypeId?: string;
    workOrderTypeId?: string;
  } = {}): Promise<PagedResult<QualityTemplate>> {
    const response = await axios.get(`${API_URL}/maintenance/quality-templates`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getQualityTemplateById(id: string): Promise<QualityTemplate> {
    const response = await axios.get(`${API_URL}/maintenance/quality-templates/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createChecklistFromTemplate(workOrderId: string, templateId: string): Promise<QualityChecklist> {
    const response = await axios.post(`${API_URL}/maintenance/quality-checklists/from-template`, 
      { workOrderId, templateId }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Inspection Officers
  async getInspectionOfficers(): Promise<QualityInspectionOfficer[]> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/inspection-officers`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getQualifiedInspectors(specialization?: string): Promise<QualityInspectionOfficer[]> {
    const params = specialization ? { specialization } : {};
    const response = await axios.get(`${API_URL}/maintenance/quality-control/qualified-inspectors`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Reports and Statistics
  async getQualityDashboardStats(): Promise<{
    totalChecklists: number;
    completedChecklists: number;
    passedChecklists: number;
    failedChecklists: number;
    pendingInspections: number;
    averageQualityScore: number;
    byStatus: Array<{ status: string; count: number }>;
    byResult: Array<{ result: string; count: number }>;
  }> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/dashboard-stats`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getQualityTrends(fromDate?: string, toDate?: string): Promise<{
    trends: Array<{ date: string; totalChecks: number; passRate: number }>;
    averagePassRate: number;
    improvementTrend: 'improving' | 'declining' | 'stable';
  }> {
    const params: any = {};
    if (fromDate) params.fromDate = fromDate;
    if (toDate) params.toDate = toDate;

    const response = await axios.get(`${API_URL}/maintenance/quality-control/trends`, {
      params,
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getWorkOrderQualityReport(workOrderId: string): Promise<{
    workOrderNumber: string;
    assetName: string;
    checklists: QualityChecklist[];
    overallScore: number;
    totalCheckItems: number;
    passedCheckItems: number;
    failedCheckItems: number;
    canComplete: boolean;
    recommendations: string[];
  }> {
    const response = await axios.get(`${API_URL}/maintenance/quality-control/work-order-report/${workOrderId}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Helper Methods
  getStatusBadgeClass(status: QualityChecklist['status']): string {
    const statusClasses = {
      'Pending': 'bg-gray-100 text-gray-800',
      'InProgress': 'bg-blue-100 text-blue-800',
      'Completed': 'bg-green-100 text-green-800',
      'Failed': 'bg-red-100 text-red-800'
    };
    return statusClasses[status] || 'bg-gray-100 text-gray-800';
  }

  getResultBadgeClass(result: 'Pass' | 'Fail' | 'Pending'): string {
    const resultClasses = {
      'Pass': 'bg-green-100 text-green-800',
      'Fail': 'bg-red-100 text-red-800',
      'Pending': 'bg-yellow-100 text-yellow-800'
    };
    return resultClasses[result] || 'bg-gray-100 text-gray-800';
  }

  calculateCompletionPercentage(totalItems: number, completedItems: number): number {
    if (totalItems === 0) return 0;
    return Math.round((completedItems / totalItems) * 100);
  }

  formatDateTime(dateString: string): string {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
  }
}

export const qualityControlService = new QualityControlService();
export default qualityControlService;