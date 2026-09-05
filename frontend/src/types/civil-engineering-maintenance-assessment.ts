import type { CivilEngineeringMaintenanceIntakeDocument, CivilEngineeringMaintenanceIntakeLookupOption } from '@/types/civil-engineering-maintenance-intake';

export type CivilEngineeringMaintenanceAssessmentLookups = {
  supervisingCivilEngineers: CivilEngineeringMaintenanceIntakeLookupOption[];
  civilEngineers: CivilEngineeringMaintenanceIntakeLookupOption[];
  defectCategories: CivilEngineeringMaintenanceIntakeLookupOption[];
  documents: CivilEngineeringMaintenanceIntakeDocument[];
};

export type CivilEngineeringMaintenanceAssessment = {
  id: string;
  intakeId: string;
  intakeNumber: string;
  stage: string;
  status: string;
  approvalStatus: string;
  hodUserId: string;
  hodName: string;
  supervisingCivilEngineerUserId: string;
  supervisingCivilEngineerName: string;
  civilEngineerUserId?: string | null;
  civilEngineerName?: string | null;
  currentAssigneeUserId?: string | null;
  currentDueAt?: string | null;
  defectCategoryId?: string | null;
  defectCategoryLabel?: string | null;
  siteAssessment?: string | null;
  scopeRecommendation?: string | null;
  remedyRecommendation?: string | null;
  estimatedCost?: number | null;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  evidenceReference?: string | null;
  workflowInstanceId?: string | null;
  approvedById?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
};

export type StartCivilEngineeringMaintenanceAssessmentRequest = {
  clientRequestId: string;
  supervisingCivilEngineerUserId: string;
  direction: string;
  dueAt: string;
};

export type CivilEngineeringMaintenanceAssessmentTransitionRequest = {
  clientRequestId: string;
  action: 'AssignCivilEngineer' | 'SubmitAssessment' | 'ReturnAssessment' | 'SubmitToHod' | 'Approve' | 'Reject';
  assigneeUserId?: string | null;
  defectCategoryId?: string | null;
  siteAssessment?: string | null;
  scopeRecommendation?: string | null;
  remedyRecommendation?: string | null;
  estimatedCost?: number | null;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  dueAt?: string | null;
  reason?: string | null;
  rowVersion: string;
};
