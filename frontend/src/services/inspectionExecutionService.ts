import { apiService } from './api.service';
import { QualityChecklist, QualityChecklistItem } from './qualityChecklistService';
import { UploadedFile } from './fileUploadService';

export interface ChecklistItemResponse {
  itemId: string;
  result: 'Pass' | 'Fail' | 'N/A' | 'Score';
  score?: number;
  comments?: string;
  photos?: UploadedFile[];
  inspectedAt: string;
  inspectedBy: string;
}

export interface InspectionExecution {
  id: string;
  workOrderId: string;
  workOrderNumber: string;
  workOrderTitle: string;
  assetId: string;
  assetName: string;
  assetLocation?: string;
  checklistId: string;
  checklist: QualityChecklist;
  inspectorId: string;
  inspectorName: string;
  scheduledDate: string;
  startedDate?: string;
  completedDate?: string;
  status: 'Scheduled' | 'In Progress' | 'Completed' | 'On Hold' | 'Cancelled';
  overallResult?: 'Pass' | 'Fail' | 'Conditional Pass';
  overallScore?: number;
  itemResponses: ChecklistItemResponse[];
  generalNotes?: string;
  regulatoryCompliance?: RegulatoryCompliance;
  attachments?: UploadedFile[];
  signatures?: InspectionSignature[];
  workflowStatus?: string;
  createdAt: string;
  updatedAt: string;
}

export interface RegulatoryCompliance {
  isRegulatory: boolean;
  complianceStandards: string[];
  certificationRequired: boolean;
  certificationNumber?: string;
  certificationValidUntil?: string;
  auditTrailRequired: boolean;
  nextInspectionDue?: string;
}

export interface InspectionSignature {
  type: 'Inspector' | 'Supervisor' | 'Customer';
  signedBy: string;
  signedAt: string;
  signatureImage?: string;
  required: boolean;
}

export interface StartInspectionRequest {
  workOrderId: string;
  checklistId: string;
  inspectorId: string;
  scheduledDate?: string;
  notes?: string;
}

export interface CompleteInspectionRequest {
  inspectionId: string;
  overallResult: 'Pass' | 'Fail' | 'Conditional Pass';
  overallScore: number;
  itemResponses: ChecklistItemResponse[];
  generalNotes?: string;
  signatures?: InspectionSignature[];
  workflowAction?: 'approve' | 'reject' | 'require_rework';
}

export interface WorkOrderInfo {
  id: string;
  workOrderNumber: string;
  title: string;
  description?: string;
  assetId: string;
  assetName: string;
  assetLocation?: string;
  workOrderType: string;
  maintenanceType?: string;
  assignedTechnicianId?: string;
  assignedTechnicianName?: string;
  status: string;
  priority: string;
  completedDate?: string;
  estimatedHours?: number;
  actualHours?: number;
}

// Mock data for fallback (empty - no mock data)
const mockInspections: InspectionExecution[] = [];

const mockWorkOrders: WorkOrderInfo[] = [
  {
    id: 'WO-2024-001',
    workOrderNumber: 'WO-2024-001',
    title: 'Vehicle Fleet Maintenance - Unit 42',
    description: 'Scheduled maintenance for Ford Transit Van 42',
    assetId: 'FLEET-042',
    assetName: 'Ford Transit Van 42',
    assetLocation: 'Main Parking Lot',
    workOrderType: 'Preventive',
    maintenanceType: 'Scheduled',
    assignedTechnicianId: 'TECH-001',
    assignedTechnicianName: 'Mike Johnson',
    status: 'Completed',
    priority: 'Medium',
    completedDate: '2024-01-20T09:30:00Z',
    estimatedHours: 2,
    actualHours: 1.5
  },
  {
    id: 'WO-2024-002',
    workOrderNumber: 'WO-2024-002',
    title: 'HVAC System Safety Inspection',
    description: 'Monthly safety inspection for main HVAC unit',
    assetId: 'HVAC-001',
    assetName: 'Central HVAC Unit Building A',
    assetLocation: 'Building A - Roof',
    workOrderType: 'Safety',
    maintenanceType: 'Inspection',
    assignedTechnicianId: 'TECH-002',
    assignedTechnicianName: 'Sarah Davis',
    status: 'Completed',
    priority: 'High',
    completedDate: '2024-01-21T14:00:00Z',
    estimatedHours: 3,
    actualHours: 2.5
  }
];

class InspectionExecutionService {
  private useBackend = true; // Set to false to force mock data

  async getWorkOrdersForInspection(): Promise<WorkOrderInfo[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<WorkOrderInfo[]>('/work-orders/completed-for-inspection');
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock data as fallback
    return mockWorkOrders.filter(wo => wo.status === 'Completed');
  }

  async getWorkOrderById(workOrderId: string): Promise<WorkOrderInfo | null> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<any>(`/maintenance/quality-control/inspection-by-workorder/${workOrderId}`);
        // Map the response to WorkOrderInfo format
        if (response && response.workOrder) {
          return {
            id: response.workOrder.id,
            workOrderNumber: response.workOrder.workOrderNumber,
            title: response.workOrder.title,
            description: response.workOrder.description,
            assetId: response.workOrder.assetId,
            assetName: response.workOrder.assetName,
            assetLocation: response.workOrder.assetLocation,
            workOrderType: response.workOrder.workOrderType,
            maintenanceType: response.workOrder.maintenanceType,
            status: response.workOrder.status,
            priority: response.workOrder.priority,
            completedDate: response.workOrder.completedDate
          };
        }
        return null;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock data as fallback
    return mockWorkOrders.find(wo => wo.id === workOrderId) || null;
  }

  async startInspection(request: StartInspectionRequest): Promise<InspectionExecution> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<InspectionExecution>('/inspections/start', {
          method: 'POST',
          body: JSON.stringify(request),
        });
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Mock implementation for fallback
    const workOrder = mockWorkOrders.find(wo => wo.id === request.workOrderId);
    if (!workOrder) {
      throw new Error('Work order not found');
    }

    const newInspection: InspectionExecution = {
      id: `INS-${Date.now()}`,
      workOrderId: request.workOrderId,
      workOrderNumber: workOrder.workOrderNumber,
      workOrderTitle: workOrder.title,
      assetId: workOrder.assetId,
      assetName: workOrder.assetName,
      assetLocation: workOrder.assetLocation,
      checklistId: request.checklistId,
      checklist: {
        id: request.checklistId,
        name: 'Mock Checklist',
        workOrderType: workOrder.workOrderType,
        assetCategory: 'Vehicle',
        isMandatory: true,
        isActive: true,
        minimumPassingScore: 85,
        version: 1,
        items: [],
        createdDate: new Date().toISOString(),
        createdBy: 'System'
      },
      inspectorId: request.inspectorId,
      inspectorName: 'Current Inspector',
      scheduledDate: request.scheduledDate || new Date().toISOString(),
      startedDate: new Date().toISOString(),
      status: 'In Progress',
      itemResponses: [],
      generalNotes: request.notes,
      regulatoryCompliance: {
        isRegulatory: workOrder.workOrderType === 'Regulatory',
        complianceStandards: [],
        certificationRequired: false,
        auditTrailRequired: true
      },
      attachments: [],
      signatures: [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    mockInspections.push(newInspection);
    return newInspection;
  }

  async getInspectionById(inspectionId: string): Promise<InspectionExecution | null> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<InspectionExecution>(`/inspections/${inspectionId}`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock data as fallback
    return mockInspections.find(inspection => inspection.id === inspectionId) || null;
  }

  async updateInspectionProgress(inspectionId: string, itemResponses: ChecklistItemResponse[]): Promise<InspectionExecution> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<InspectionExecution>(`/inspections/${inspectionId}/progress`, {
          method: 'PUT',
          body: JSON.stringify({ itemResponses }),
        });
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Mock update for fallback
    const inspectionIndex = mockInspections.findIndex(i => i.id === inspectionId);
    if (inspectionIndex === -1) {
      throw new Error('Inspection not found');
    }

    mockInspections[inspectionIndex].itemResponses = itemResponses;
    mockInspections[inspectionIndex].updatedAt = new Date().toISOString();

    return mockInspections[inspectionIndex];
  }

  async completeInspection(request: CompleteInspectionRequest): Promise<InspectionExecution> {
    try {
      if (this.useBackend) {
        const response = await apiService.post('/api/inspections/complete', request);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Mock completion for fallback
    const inspectionIndex = mockInspections.findIndex(i => i.id === request.inspectionId);
    if (inspectionIndex === -1) {
      throw new Error('Inspection not found');
    }

    const inspection = mockInspections[inspectionIndex];
    inspection.status = 'Completed';
    inspection.completedDate = new Date().toISOString();
    inspection.overallResult = request.overallResult;
    inspection.overallScore = request.overallScore;
    inspection.itemResponses = request.itemResponses;
    inspection.generalNotes = request.generalNotes;
    inspection.signatures = request.signatures || [];
    inspection.updatedAt = new Date().toISOString();

    // Simulate workflow action
    if (request.workflowAction === 'approve') {
      inspection.workflowStatus = 'Approved';
    } else if (request.workflowAction === 'reject') {
      inspection.workflowStatus = 'Rejected';
    } else if (request.workflowAction === 'require_rework') {
      inspection.workflowStatus = 'Rework Required';
    }

    return inspection;
  }

  async getInspectionsByWorkOrder(workOrderId: string): Promise<InspectionExecution[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.get(`/api/inspections/work-order/${workOrderId}`);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock data as fallback
    return mockInspections.filter(inspection => inspection.workOrderId === workOrderId);
  }

  async getActiveInspections(inspectorId?: string): Promise<InspectionExecution[]> {
    if (this.useBackend) {
      const queryString = inspectorId ? `?inspectorId=${inspectorId}` : '';
      const response = await apiService.request<InspectionExecution[]>(`/inspections/active${queryString}`);
      return response;
    }
    
    // No mock data fallback - return empty array
    return [];
  }

  calculateInspectionScore(checklist: QualityChecklist, itemResponses: ChecklistItemResponse[]): number {
    let totalPossiblePoints = 0;
    let earnedPoints = 0;

    checklist.items.forEach(item => {
      const response = itemResponses.find(r => r.itemId === item.id);
      
      if (item.responseType === 'Score' && item.maxScore) {
        totalPossiblePoints += item.maxScore;
        if (response?.score) {
          earnedPoints += response.score;
        }
      } else {
        // Pass/Fail items are worth full points for pass, zero for fail
        const itemWeight = item.critical ? 2 : 1; // Critical items worth double
        totalPossiblePoints += itemWeight;
        
        if (response?.result === 'Pass') {
          earnedPoints += itemWeight;
        }
      }
    });

    return totalPossiblePoints > 0 ? Math.round((earnedPoints / totalPossiblePoints) * 100) : 0;
  }

  determineOverallResult(score: number, minimumPassingScore: number, hasFailedCriticalItems: boolean): 'Pass' | 'Fail' | 'Conditional Pass' {
    if (hasFailedCriticalItems) {
      return 'Fail';
    }
    
    if (score >= minimumPassingScore) {
      return 'Pass';
    } else if (score >= minimumPassingScore - 10) { // Within 10 points of passing
      return 'Conditional Pass';
    } else {
      return 'Fail';
    }
  }

  hasFailedCriticalItems(checklist: QualityChecklist, itemResponses: ChecklistItemResponse[]): boolean {
    return checklist.items.some(item => {
      if (!item.critical) return false;
      
      const response = itemResponses.find(r => r.itemId === item.id);
      return response?.result === 'Fail';
    });
  }

  async generateInspectionReport(inspectionId: string): Promise<Blob> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<Blob>(`/inspections/${inspectionId}/report`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, cannot generate report:', error);
    }

    throw new Error('Report generation not available in mock mode');
  }

  async getInspectionHistory(assetId: string, limit: number = 10): Promise<InspectionExecution[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<InspectionExecution[]>(`/inspections/history/${assetId}?limit=${limit}`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock data as fallback
    return mockInspections
      .filter(inspection => inspection.assetId === assetId)
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, limit);
  }

  async getRegulatoryComplianceStatus(assetId: string): Promise<RegulatoryCompliance[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<RegulatoryCompliance[]>(`/inspections/regulatory-compliance/${assetId}`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Return mock compliance data
    return [
      {
        isRegulatory: true,
        complianceStandards: ['ISO 9001', 'OSHA Safety Standards'],
        certificationRequired: true,
        certificationNumber: 'CERT-2024-001',
        certificationValidUntil: '2024-12-31',
        auditTrailRequired: true,
        nextInspectionDue: '2024-07-01'
      }
    ];
  }
}

export const inspectionExecutionService = new InspectionExecutionService();
