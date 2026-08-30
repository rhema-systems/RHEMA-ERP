export type CivilEngineeringMaintenanceIntakeLookupOption = { id: string; label: string };

export type CivilEngineeringMaintenanceIntakeDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringMaintenanceIntakeLookups = {
  sources: string[];
  urgencies: string[];
  workClassifications: string[];
  projects: CivilEngineeringMaintenanceIntakeLookupOption[];
  maintenanceAssets: CivilEngineeringMaintenanceIntakeLookupOption[];
  buildingsOrProperties: CivilEngineeringMaintenanceIntakeLookupOption[];
  maintenanceSchedules: CivilEngineeringMaintenanceIntakeLookupOption[];
  complaintTickets: CivilEngineeringMaintenanceIntakeLookupOption[];
  requesters: CivilEngineeringMaintenanceIntakeLookupOption[];
  priorities: CivilEngineeringMaintenanceIntakeLookupOption[];
  documents: CivilEngineeringMaintenanceIntakeDocument[];
};

export type CivilEngineeringMaintenanceIntake = {
  id: string;
  intakeNumber: string;
  workClassification: string;
  source: string;
  urgency: string;
  title: string;
  description: string;
  projectId?: string | null;
  projectName?: string | null;
  maintenanceAssetId?: string | null;
  maintenanceAssetName?: string | null;
  estateManagedAssetId?: string | null;
  buildingOrPropertyName?: string | null;
  maintenanceScheduleId?: string | null;
  maintenanceScheduleName?: string | null;
  helpdeskTicketId?: string | null;
  complaintTicketNumber?: string | null;
  requesterName: string;
  priorityName: string;
  evidenceReference?: string | null;
  status: string;
  createdAt: string;
};

export type CreateCivilEngineeringMaintenanceIntakeRequest = {
  clientRequestId: string;
  workClassification: string;
  source: string;
  urgency: string;
  title: string;
  description: string;
  projectId?: string | null;
  maintenanceAssetId?: string | null;
  estateManagedAssetId?: string | null;
  maintenanceScheduleId?: string | null;
  helpdeskTicketId?: string | null;
  requesterUserId: string;
  priorityLevelId: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};
