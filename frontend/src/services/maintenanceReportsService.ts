import { apiService } from './api.service';

export interface AssetMovementReportRow {
  movementId: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  effectiveAtUtc: string;
  fromProject: string;
  fromSite: string;
  toProject: string;
  toSite: string;
  movementType: string;
  reason: string;
  notes?: string | null;
}

export interface InspectionServiceReportRow {
  recordId: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  recordType: string;
  sheetType: string;
  templateName: string;
  inspectionKind: string;
  performedAtUtc: string;
  status: string;
  result?: string | null;
  inspectorName: string;
  source: string;
  notes?: string | null;
}

export interface WorkOrderStatusReportRow {
  workOrderId: string;
  workOrderNumber: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  title: string;
  workOrderType: string;
  maintenanceType: string;
  priority: string;
  status: string;
  assignedTechnician: string;
  createdAtUtc: string;
  requestedCompletionAtUtc?: string | null;
  completedAtUtc?: string | null;
  isOverdue: boolean;
}

export interface PartIssuedReportRow {
  workOrderPartId: string;
  workOrderId: string;
  workOrderNumber: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  itemCode: string;
  itemName: string;
  quantityIssued: number;
  quantityUsed: number;
  quantityReturned: number;
  unitCost: number;
  totalCost: number;
  status: string;
  issuedAtUtc: string;
}

export interface WorkOrderCostReportRow {
  workOrderId: string;
  workOrderNumber: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  title: string;
  status: string;
  createdAtUtc: string;
  partsCost: number;
  laborCost: number;
  otherCost: number;
  totalCost: number;
}

export interface AssetCostReportRow {
  assetId: string;
  assetNumber: string;
  assetName: string;
  workOrderCount: number;
  partsCost: number;
  laborCost: number;
  otherCost: number;
  totalCost: number;
}

export interface MaintenanceOperationalReports {
  fromUtc: string;
  toUtc: string;
  assetMovements: AssetMovementReportRow[];
  inspectionServiceHistory: InspectionServiceReportRow[];
  workOrderStatus: WorkOrderStatusReportRow[];
  partsIssued: PartIssuedReportRow[];
  costsByWorkOrder: WorkOrderCostReportRow[];
  costsByAsset: AssetCostReportRow[];
}

export const maintenanceReportsService = {
  async getOperationalReports(params: { fromUtc?: string; toUtc?: string; assetId?: string } = {}) {
    return apiService.get<MaintenanceOperationalReports>('/maintenance/reports/operational', params);
  },
};

export default maintenanceReportsService;
