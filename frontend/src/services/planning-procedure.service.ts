import { compatibleApiService as apiService } from './compatibleApiService';

export interface PlanningProcedure {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
}

export interface PlanningWorkspaceStage {
  name: string;
  owner: string;
  summary: string;
  checklist: string[];
}

export interface PlanningWorkspaceDocument {
  name: string;
  requiredFrom: string;
  isMandatory: boolean;
}

export interface PlanningWorkspaceField {
  key: string;
  label: string;
  type: string;
  options?: string[] | null;
}

export interface PlanningWorkspaceHandoff {
  fromRole: string;
  toRole: string;
  trigger: string;
}

export interface PlanningProcedureWorkspace {
  procedure: PlanningProcedure;
  stages: PlanningWorkspaceStage[];
  requiredDocuments: PlanningWorkspaceDocument[];
  intakeFields: PlanningWorkspaceField[];
  outputs: string[];
  handoffs: PlanningWorkspaceHandoff[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export const planningFallbackProcedures: PlanningProcedure[] = [
  { title: 'Vetting of Applications for Land Allocations or Temporary License', entityType: 'PlanningLandAllocationVetting', source: 'Planning Standard Operating Procedure', summary: 'Ensure land allocation or license applications align with approved master plans and TDC land use strategy.', icon: 'ClipboardCheck', stageCount: 5, accent: 'teal' },
  { title: 'Change of Use Review', entityType: 'PlanningChangeOfUseReview', source: 'Planning Standard Operating Procedure', summary: 'Confirm proposed change of use supports spatial development strategy and avoids land use conflicts.', icon: 'RefreshCw', stageCount: 5, accent: 'sky' },
  { title: 'Preparation of Planning Scheme / Layout', entityType: 'PlanningSchemeLayoutPreparation', source: 'Planning Standard Operating Procedure', summary: 'Prepare planning schemes and layouts for new company acquisitions.', icon: 'Map', stageCount: 5, accent: 'indigo' },
  { title: 'Site Report', entityType: 'PlanningSiteReport', source: 'Planning Standard Operating Procedure', summary: 'Visit site and report on the ground situation for a requested issue, file, or application.', icon: 'MapPin', stageCount: 5, accent: 'emerald' },
  { title: 'Preparation of Site Plan', entityType: 'PlanningSitePlanPreparation', source: 'Planning Standard Operating Procedure', summary: 'Prepare site plans for lessees or transferees and verify spatial and planning standards.', icon: 'FileText', stageCount: 5, accent: 'blue' },
  { title: 'Official Search and Provision of Data', entityType: 'PlanningOfficialSearchData', source: 'Planning Standard Operating Procedure', summary: 'Provide verified land use information based on available Planning Section records.', icon: 'Search', stageCount: 5, accent: 'cyan' },
  { title: 'Development Permit Conformity Review', entityType: 'PlanningDevelopmentPermitConformity', source: 'Planning Standard Operating Procedure', summary: 'Assess whether a development proposal conforms with the approved layout and planning controls.', icon: 'FileCheck2', stageCount: 5, accent: 'violet' },
  { title: 'Undertake Regularization', entityType: 'PlanningRegularization', source: 'Planning Standard Operating Procedure', summary: 'Guide regularization of unplanned or informally occupied TDC lands.', icon: 'BadgeCheck', stageCount: 6, accent: 'amber' },
  { title: 'Layout Review and Correction', entityType: 'PlanningLayoutReviewCorrection', source: 'Planning Standard Operating Procedure', summary: 'Identify and rectify anomalies in approved layouts.', icon: 'FilePenLine', stageCount: 5, accent: 'orange' },
  { title: 'Compliance Site Inspection and Reporting', entityType: 'PlanningComplianceInspection', source: 'Planning Standard Operating Procedure', summary: 'Physically inspect and verify ground situation for compliance matters.', icon: 'ClipboardList', stageCount: 5, accent: 'rose' },
  { title: 'Dispute Resolution and Client Complaint Management', entityType: 'PlanningDisputeComplaint', source: 'Planning Standard Operating Procedure', summary: 'Handle planning-related complaints and boundary disputes fairly.', icon: 'MessageSquare', stageCount: 5, accent: 'purple' },
  { title: 'District Assembly Spatial Planning Committee Meetings', entityType: 'PlanningAssemblySpatialCommittee', source: 'Planning Standard Operating Procedure', summary: "Represent TDC's interest in MMDAs permitting and spatial planning committee processes.", icon: 'Users', stageCount: 5, accent: 'slate' },
];

class PlanningProcedureService {
  async getProcedures(): Promise<PlanningProcedure[]> {
    const response = await apiService.get<ApiResponse<PlanningProcedure[]>>('/development/planning/procedures');
    return response.data || [];
  }

  async getProcedureWorkspace(entityType: string): Promise<PlanningProcedureWorkspace | null> {
    const response = await apiService.get<ApiResponse<PlanningProcedureWorkspace>>(
      `/development/planning/procedures/${encodeURIComponent(entityType)}`
    );
    return response.data || null;
  }
}

export const planningProcedureService = new PlanningProcedureService();
