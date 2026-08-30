export type CivilEngineeringMaintenanceExecutionLookupOption = {
  id: string;
  label: string;
  status: string;
  maintenanceAssetId?: string | null;
};

export type CivilEngineeringMaintenanceExecutionLookups = {
  awardedHandoffs: CivilEngineeringMaintenanceExecutionLookupOption[];
  maintenanceTypes: CivilEngineeringMaintenanceExecutionLookupOption[];
  priorityLevels: CivilEngineeringMaintenanceExecutionLookupOption[];
  jobCards: CivilEngineeringMaintenanceExecutionLookupOption[];
  workOrders: CivilEngineeringMaintenanceExecutionLookupOption[];
};

export type CivilEngineeringMaintenanceExecutionLink = {
  id: string;
  handoffId: string;
  intakeNumber: string;
  projectId: string;
  projectLabel: string;
  maintenanceAssetId: string;
  maintenanceAssetLabel: string;
  jobCardId?: string | null;
  jobCardNumber?: string | null;
  jobCardStatus?: string | null;
  workOrderId?: string | null;
  workOrderNumber?: string | null;
  workOrderStatus?: string | null;
  linkMode: 'CreateJobCard' | 'LinkExisting';
  stage: string;
  status: string;
  lastOwnerStatusSummary?: string | null;
  lastRevalidatedAt?: string | null;
  rowVersion: string;
};

export type CreateCivilEngineeringMaintenanceExecutionLinkRequest = {
  clientRequestId: string;
  handoffId: string;
  linkMode: 'CreateJobCard' | 'LinkExisting';
  maintenanceTypeId?: string | null;
  priorityLevelId?: string | null;
  jobCardId?: string | null;
  workOrderId?: string | null;
};

export type RefreshCivilEngineeringMaintenanceExecutionLinkRequest = {
  clientRequestId: string;
  rowVersion: string;
};
